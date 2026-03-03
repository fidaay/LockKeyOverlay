using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace LockKeyOverlay
{
    public partial class MainWindow : Window
    {
        // ---- Win32: teclado ----
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        private const int VK_NUMLOCK = 0x90;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        // ---- Win32: styles / click-through / toolwindow ----
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLongPtrCompat(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr newValue)
            => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, newValue) : new IntPtr(SetWindowLong32(hWnd, nIndex, newValue.ToInt32()));

        // ---- Win32: topmost reforzado (incluye taskbar) ----
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        // Windows registry para run at startup (HKCU)
        private const string StartupRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "LockKeyOverlay";

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);

        // ---- Estado ----
        private IntPtr windowHandle = IntPtr.Zero;

        private readonly LowLevelKeyboardProc keyboardProc;
        private IntPtr keyboardHook = IntPtr.Zero;

        private readonly WinEventDelegate foregroundChangedProc;
        private IntPtr foregroundEventHook = IntPtr.Zero;

        private Forms.NotifyIcon? trayIcon;
        private Forms.ContextMenuStrip? trayMenu;

        private Forms.ToolStripMenuItem? visibleMenuItem;
        private Forms.ToolStripMenuItem? movementMenuItem;
        private Forms.ToolStripMenuItem? topMostMenuItem;
        private Forms.ToolStripMenuItem? runAtStartupMenuItem;
        private Forms.ToolStripMenuItem? resetConfigMenuItem;

        private bool suppressConfigSave = false;

        private static readonly RgbaStyle DefaultActiveStyle = RgbaStyle.FromOpacity(255, 140, 0, 0.85);
        private static readonly RgbaStyle DefaultInactiveStyle = RgbaStyle.FromOpacity(30, 144, 255, 0.75);

        private const string ConfigFolderName = "LockKeyOverlay";
        private const string ConfigFileName = "settings.json";

        private bool allowExit = false;
        private bool movementEnabled = true;
        private bool isDragging = false;

        private const int ConfigSaveDebounceMs = 250;
        private readonly DispatcherTimer configSaveDebounceTimer;

        private System.Drawing.Point dragStartMousePx;
        private double dragStartLeftDip;
        private double dragStartTopDip;

        private BitmapImage? numLockOnIcon;
        private BitmapImage? numLockOffIcon;

        private RgbaStyle activeStyle = DefaultActiveStyle;
        private RgbaStyle inactiveStyle = DefaultInactiveStyle;

        public MainWindow()
        {
            InitializeComponent();

            keyboardProc = HookCallback;
            foregroundChangedProc = ForegroundChanged;

            configSaveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ConfigSaveDebounceMs)
            };
            configSaveDebounceTimer.Tick += ConfigSaveDebounceTimer_Tick;

            LocationChanged += Window_LocationChanged;
            IsVisibleChanged += Window_IsVisibleChanged;

            Closing += Window_Closing;
            Closed += Window_Closed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            windowHandle = new WindowInteropHelper(this).Handle;

            // Quitar Alt+Tab (toolwindow)
            ApplyToolWindowStyle();

            // Estado inicial de click-through según movementEnabled
            ApplyClickThroughState();

            // Estado inicial topmost reforzado
            ApplyTopMostState();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Left = 20;
            Top = 20;

            LoadIndicatorIcons();
            ConfigureTray();
            InstallForegroundEventHook();
            InstallKeyboardHook();

            bool configLoaded = LoadConfiguration();

            if (!configLoaded)
            {
                CenterOverlayOnPrimaryScreen();
            }

            Cursor = movementEnabled ? WpfCursors.SizeAll : WpfCursors.Arrow;
            UpdateNumLockIndicator();
        }

        // ----------------- UI / visual -----------------
        private void LoadIndicatorIcons()
        {
            string assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

            string onIconPath = Path.Combine(assetsDir, "lock_closed.png");
            string offIconPath = Path.Combine(assetsDir, "lock_open.png");

            if (!File.Exists(onIconPath))
                throw new FileNotFoundException($"No se encontró el archivo: {onIconPath}");

            if (!File.Exists(offIconPath))
                throw new FileNotFoundException($"No se encontró el archivo: {offIconPath}");

            numLockOnIcon = LoadBitmap(onIconPath);
            numLockOffIcon = LoadBitmap(offIconPath);
        }

        private static BitmapImage LoadBitmap(string filePath)
        {
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void UpdateNumLockIndicator()
        {
            bool numLockOn = IsNumLockOn();
            ApplyVisualState(numLockOn);
        }

        private void ApplyVisualState(bool numLockOn)
        {
            RgbaStyle style = numLockOn ? activeStyle : inactiveStyle;

            StateText.Text = numLockOn ? "NUM LK ON " : "NUM LK OFF";
            StateIcon.Source = numLockOn ? numLockOnIcon : numLockOffIcon;

            // Opacidad (como tu WinForms: afecta todo el overlay)
            OverlayBorder.Opacity = Math.Max(0.05, style.A / 255.0);
            OverlayBorder.Background = new SolidColorBrush(WpfColor.FromRgb(style.R, style.G, style.B));

            double luminance = (0.299 * style.R) + (0.587 * style.G) + (0.114 * style.B);
            StateText.Foreground = luminance > 150 ? WpfBrushes.Black : WpfBrushes.White;
        }

        private static bool IsNumLockOn()
            => (GetKeyState(VK_NUMLOCK) & 1) != 0;

        // ----------------- Tray -----------------
        private void ConfigureTray()
        {
            trayMenu = new Forms.ContextMenuStrip();

            visibleMenuItem = new Forms.ToolStripMenuItem("Ventana visible")
            {
                CheckOnClick = true,
                Checked = true
            };
            visibleMenuItem.CheckedChanged += (_, _) => ToggleVisibility();

            movementMenuItem = new Forms.ToolStripMenuItem("Activar movimiento")
            {
                CheckOnClick = true,
                Checked = true
            };
            movementMenuItem.CheckedChanged += (_, _) =>
            {
                movementEnabled = movementMenuItem.Checked;
                Cursor = movementEnabled ? WpfCursors.SizeAll : WpfCursors.Arrow;
                ApplyClickThroughState();
                RequestSaveConfiguration();
            };

            topMostMenuItem = new Forms.ToolStripMenuItem("Siempre encima")
            {
                CheckOnClick = true,
                Checked = true
            };
            topMostMenuItem.CheckedChanged += (_, _) =>
            {
                ApplyTopMostState();
                RequestSaveConfiguration();
            };

            runAtStartupMenuItem = new Forms.ToolStripMenuItem("Iniciar con Windows")
            {
                CheckOnClick = true,
                Checked = IsStartupEnabled()
            };
            runAtStartupMenuItem.CheckedChanged += (_, _) =>
            {
                SetStartupEnabled(runAtStartupMenuItem.Checked);
                RequestSaveConfiguration();
            };

            resetConfigMenuItem = new Forms.ToolStripMenuItem("Eliminar configuración...");
            resetConfigMenuItem.Click += (_, _) =>
            {
                var result = Forms.MessageBox.Show(
                    "Se eliminará la configuración guardada y se restaurarán los valores por defecto.\n\n¿Deseas continuar?",
                    "Eliminar configuración",
                    Forms.MessageBoxButtons.YesNo,
                    Forms.MessageBoxIcon.Warning,
                    Forms.MessageBoxDefaultButton.Button2);

                if (result != Forms.DialogResult.Yes)
                    return;

                DeleteConfigurationFile();
                ApplyDefaultConfiguration();
            };

            var activeColorMenuItem = new Forms.ToolStripMenuItem("Cambiar color estado activo...");

            activeColorMenuItem.Click += (_, _) =>
            {
                var edited = ShowRgbaEditor("Color estado activo", activeStyle);
                if (edited is not null)
                {
                    activeStyle = edited.Value;
                    UpdateNumLockIndicator();
                    RequestSaveConfiguration();
                }
            };

            var inactiveColorMenuItem = new Forms.ToolStripMenuItem("Cambiar color estado inactivo...");

            inactiveColorMenuItem.Click += (_, _) =>
            {
                var edited = ShowRgbaEditor("Color estado inactivo", inactiveStyle);

                if (edited is not null)
                {
                    inactiveStyle = edited.Value;
                    UpdateNumLockIndicator();
                    RequestSaveConfiguration();
                }
            };

            var exitMenuItem = new Forms.ToolStripMenuItem("Salir");
            exitMenuItem.Click += (_, _) =>
            {
                allowExit = true;
                trayIcon!.Visible = false;
                Close();
            };

            trayMenu.Items.Add(visibleMenuItem);
            trayMenu.Items.Add(movementMenuItem);
            trayMenu.Items.Add(topMostMenuItem);
            trayMenu.Items.Add(runAtStartupMenuItem);
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add(activeColorMenuItem);
            trayMenu.Items.Add(inactiveColorMenuItem);
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add(resetConfigMenuItem);
            trayMenu.Items.Add(exitMenuItem);

            trayIcon = new Forms.NotifyIcon
            {
                Icon = Drawing.SystemIcons.Information,
                Text = "LockKeyOverlay",
                Visible = true,
                ContextMenuStrip = trayMenu
            };

            trayIcon.DoubleClick += (_, _) =>
            {
                if (visibleMenuItem is null) return;
                visibleMenuItem.Checked = !visibleMenuItem.Checked;
            };
        }

        private void ToggleVisibility()
        {
            if (visibleMenuItem is null) return;

            if (visibleMenuItem.Checked)
            {
                Show();
                ApplyTopMostState();
            }
            else
            {
                Hide();
            }

            RequestSaveConfiguration();
        }

        private string GetConfigDirectoryPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, ConfigFolderName);
        }

        private string GetConfigFilePath()
        {
            return Path.Combine(GetConfigDirectoryPath(), ConfigFileName);
        }

        private void RequestSaveConfiguration()
        {
            if (suppressConfigSave)
                return;

            configSaveDebounceTimer.Stop();
            configSaveDebounceTimer.Start();
        }

        private void FlushPendingConfigurationSave()
        {
            if (configSaveDebounceTimer.IsEnabled)
                configSaveDebounceTimer.Stop();

            SaveConfiguration();
        }

        private void ConfigSaveDebounceTimer_Tick(object? sender, EventArgs e)
        {
            configSaveDebounceTimer.Stop();
            SaveConfiguration();
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (!IsLoaded)
                return;

            RequestSaveConfiguration();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            RequestSaveConfiguration();
        }

        private (double ScaleX, double ScaleY) GetWindowDpiScale()
        {
            var source = PresentationSource.FromVisual(this);

            if (source?.CompositionTarget is null)
                return (1.0, 1.0);

            return (
                source.CompositionTarget.TransformToDevice.M11,
                source.CompositionTarget.TransformToDevice.M22
            );
        }

        private Drawing.Point GetWindowTopLeftPx()
        {
            var (scaleX, scaleY) = GetWindowDpiScale();

            int x = (int)Math.Round(Left * scaleX);
            int y = (int)Math.Round(Top * scaleY);

            return new Drawing.Point(x, y);
        }

        private static Forms.Screen? FindScreenByDeviceName(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return null;

            foreach (var screen in Forms.Screen.AllScreens)
            {
                if (string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    return screen;
            }

            return null;
        }

        private void RestoreWindowPositionSafely(AppConfig config)
        {
            var (scaleX, scaleY) = GetWindowDpiScale();

            // Posición deseada en px (preferimos px si existe; fallback a DIP legacy)
            double desiredLeftPx;
            double desiredTopPx;

            if (config.LeftPx.HasValue && config.TopPx.HasValue)
            {
                desiredLeftPx = config.LeftPx.Value;
                desiredTopPx = config.TopPx.Value;
            }
            else
            {
                desiredLeftPx = config.Left * scaleX;
                desiredTopPx = config.Top * scaleY;
            }

            // Elegir monitor guardado si aún existe
            Forms.Screen? savedScreen = FindScreenByDeviceName(config.ScreenDeviceName);

            // Si no existe, usar monitor donde caería el punto; si no, primario
            Forms.Screen? targetScreen =
                savedScreen
                ?? Forms.Screen.FromPoint(new Drawing.Point(
                    (int)Math.Round(desiredLeftPx),
                    (int)Math.Round(desiredTopPx)))
                ?? Forms.Screen.PrimaryScreen;

            if (targetScreen is null)
            {
                return;
            }

            var boundsPx = targetScreen.Bounds;

            double windowWidthPx = Math.Max(1, ActualWidth * scaleX);
            double windowHeightPx = Math.Max(1, ActualHeight * scaleY);

            double minLeftPx = boundsPx.Left;
            double minTopPx = boundsPx.Top;

            double maxLeftPx = boundsPx.Right - windowWidthPx;
            double maxTopPx = boundsPx.Bottom - windowHeightPx;

            // Si por cualquier razón la ventana fuera más grande que la pantalla
            if (maxLeftPx < minLeftPx) maxLeftPx = minLeftPx;
            if (maxTopPx < minTopPx) maxTopPx = minTopPx;

            double clampedLeftPx = Math.Max(minLeftPx, Math.Min(desiredLeftPx, maxLeftPx));
            double clampedTopPx = Math.Max(minTopPx, Math.Min(desiredTopPx, maxTopPx));

            Left = clampedLeftPx / scaleX;
            Top = clampedTopPx / scaleY;
        }

        private void CenterOverlayOnPrimaryScreen()
        {
            var primary = Forms.Screen.PrimaryScreen;

            if (primary is null)
                return;

            var (scaleX, scaleY) = GetWindowDpiScale();
            var workAreaPx = primary.WorkingArea;

            double windowWidthPx = Math.Max(1, ActualWidth * scaleX);
            double windowHeightPx = Math.Max(1, ActualHeight * scaleY);

            double leftPx = workAreaPx.Left + ((workAreaPx.Width - windowWidthPx) / 2.0);
            double topPx = workAreaPx.Top + ((workAreaPx.Height - windowHeightPx) / 2.0);

            Left = leftPx / scaleX;
            Top = topPx / scaleY;
        }

        private void SaveConfiguration()
        {
            if (suppressConfigSave)
                return;

            try
            {
                var currentWindowTopLeftPx = GetWindowTopLeftPx();
                var currentScreen = Forms.Screen.FromPoint(currentWindowTopLeftPx);

                var config = new AppConfig
                {
                    Left = Left,
                    Top = Top,
                    ScreenDeviceName = currentScreen.DeviceName,
                    LeftPx = currentWindowTopLeftPx.X,
                    TopPx = currentWindowTopLeftPx.Y,
                    IsVisible = IsVisible,
                    MovementEnabled = movementMenuItem?.Checked ?? movementEnabled,
                    TopMostEnabled = topMostMenuItem?.Checked ?? Topmost,
                    RunAtStartupEnabled = runAtStartupMenuItem?.Checked ?? false,
                    Active = new RgbaConfig
                    {
                        R = activeStyle.R,
                        G = activeStyle.G,
                        B = activeStyle.B,
                        A = activeStyle.A
                    },
                    Inactive = new RgbaConfig
                    {
                        R = inactiveStyle.R,
                        G = inactiveStyle.G,
                        B = inactiveStyle.B,
                        A = inactiveStyle.A
                    }
                };

                string dir = GetConfigDirectoryPath();
                Directory.CreateDirectory(dir);

                string path = GetConfigFilePath();
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(path, json);
            }
            catch
            {
                // Intencionalmente silencioso para no romper la app por un fallo de IO.
            }
        }

        private bool LoadConfiguration()
        {
            string path = GetConfigFilePath();

            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config is null)
                    return false;

                suppressConfigSave = true;

                // Posición (restauración segura con monitor guardado)
                RestoreWindowPositionSafely(config);

                // Colores/alpha
                if (config.Active is not null)
                    activeStyle = new RgbaStyle(config.Active.R, config.Active.G, config.Active.B, config.Active.A);

                if (config.Inactive is not null)
                    inactiveStyle = new RgbaStyle(config.Inactive.R, config.Inactive.G, config.Inactive.B, config.Inactive.A);

                // Menús (esto dispara handlers y aplica estados)
                if (movementMenuItem is not null)
                    movementMenuItem.Checked = config.MovementEnabled;

                if (topMostMenuItem is not null)
                    topMostMenuItem.Checked = config.TopMostEnabled;

                if (visibleMenuItem is not null)
                    visibleMenuItem.Checked = config.IsVisible;

                // Este sí escribe en registry si cambia
                if (runAtStartupMenuItem is not null && runAtStartupMenuItem.Checked != config.RunAtStartupEnabled)
                {
                    runAtStartupMenuItem.Checked = config.RunAtStartupEnabled;
                }

                return true;
            }
            catch
            {
                // Si el JSON está corrupto, simplemente se ignora y se usan defaults.
                return false;
            }
            finally
            {
                suppressConfigSave = false;
            }
        }

        private void DeleteConfigurationFile()
        {
            try
            {
                string path = GetConfigFilePath();

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Intencionalmente silencioso.
            }
        }

        private void ApplyDefaultConfiguration()
        {
            suppressConfigSave = true;

            try
            {
                activeStyle = DefaultActiveStyle;
                inactiveStyle = DefaultInactiveStyle;

                CenterOverlayOnPrimaryScreen();

                if (movementMenuItem is not null)
                    movementMenuItem.Checked = true;
                else
                    movementEnabled = true;

                if (topMostMenuItem is not null)
                    topMostMenuItem.Checked = true;
                else
                    Topmost = true;

                if (visibleMenuItem is not null)
                    visibleMenuItem.Checked = true;
                else
                    Show();

                // No forzamos RunAtStartup aquí porque borrar config no necesariamente
                // debe tocar la preferencia de inicio en Windows si tú no quieres.
                // Si quieres que también se desactive, te digo la línea exacta.
            }
            finally
            {
                suppressConfigSave = false;
            }

            ApplyClickThroughState();
            ApplyTopMostState();
            UpdateNumLockIndicator();
        }

        private bool IsStartupEnabled()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRunKeyPath, writable: false);
            string? value = key?.GetValue(StartupValueName) as string;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            string expected = $"\"{exePath}\"";
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private void SetStartupEnabled(bool enabled)
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupRunKeyPath);

            if (key is null)
            {
                if (runAtStartupMenuItem is not null)
                    runAtStartupMenuItem.Checked = false;

                return;
            }

            if (enabled)
            {
                string? exePath = Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    if (runAtStartupMenuItem is not null)
                        runAtStartupMenuItem.Checked = false;

                    return;
                }

                key.SetValue(StartupValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }

        // ----------------- Movimiento (drag) con clamp a pantalla -----------------
        private void Overlay_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
        {
            if (!movementEnabled) return;

            isDragging = true;
            dragStartMousePx = Forms.Cursor.Position;
            dragStartLeftDip = Left;
            dragStartTopDip = Top;

            Mouse.Capture(OverlayBorder);
        }

        private void Overlay_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (!movementEnabled) return;
            if (!isDragging) return;

            var src = PresentationSource.FromVisual(this);
            if (src?.CompositionTarget is null) return;

            double scaleX = src.CompositionTarget.TransformToDevice.M11;
            double scaleY = src.CompositionTarget.TransformToDevice.M22;

            var currentMousePx = Forms.Cursor.Position;

            int dxPx = currentMousePx.X - dragStartMousePx.X;
            int dyPx = currentMousePx.Y - dragStartMousePx.Y;

            double dxDip = dxPx / scaleX;
            double dyDip = dyPx / scaleY;

            double targetLeftDip = dragStartLeftDip + dxDip;
            double targetTopDip = dragStartTopDip + dyDip;

            // Clamp usando la pantalla del cursor (Bounds incluye taskbar)
            var screen = Forms.Screen.FromPoint(currentMousePx);
            var boundsPx = screen.Bounds;

            double windowWidthPx = ActualWidth * scaleX;
            double windowHeightPx = ActualHeight * scaleY;

            double targetLeftPx = targetLeftDip * scaleX;
            double targetTopPx = targetTopDip * scaleY;

            double minLeftPx = boundsPx.Left;
            double maxLeftPx = boundsPx.Right - windowWidthPx;

            double minTopPx = boundsPx.Top;
            double maxTopPx = boundsPx.Bottom - windowHeightPx;

            double clampedLeftPx = Math.Max(minLeftPx, Math.Min(targetLeftPx, maxLeftPx));
            double clampedTopPx = Math.Max(minTopPx, Math.Min(targetTopPx, maxTopPx));

            Left = clampedLeftPx / scaleX;
            Top = clampedTopPx / scaleY;
        }

        private void Overlay_MouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            isDragging = false;
            Mouse.Capture(null);

            FlushPendingConfigurationSave();
        }

        // ----------------- Click-through -----------------
        private void ApplyClickThroughState()
        {
            if (windowHandle == IntPtr.Zero) return;

            IntPtr exStylePtr = GetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE);
            int exStyle = exStylePtr.ToInt32();

            if (!movementEnabled)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;

            SetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        private void ApplyToolWindowStyle()
        {
            if (windowHandle == IntPtr.Zero) return;

            IntPtr exStylePtr = GetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE);
            int exStyle = exStylePtr.ToInt32();

            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        // ----------------- Topmost reforzado -----------------
        private void InstallForegroundEventHook()
        {
            foregroundEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                foregroundChangedProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        private void ForegroundChanged(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (topMostMenuItem?.Checked != true) return;
            if (!IsVisible) return;
            if (windowHandle == IntPtr.Zero) return;

            Dispatcher.BeginInvoke(ApplyTopMostState);
        }

        private void ApplyTopMostState()
        {
            bool enabled = topMostMenuItem?.Checked ?? true;

            Topmost = enabled;

            if (windowHandle == IntPtr.Zero) return;

            SetWindowPos(
                windowHandle,
                enabled ? HWND_TOPMOST : HWND_NOTOPMOST,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        // ----------------- Hook teclado -----------------
        private void InstallKeyboardHook()
        {
            IntPtr moduleHandle = GetModuleHandle(null);

            keyboardHook = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                keyboardProc,
                moduleHandle,
                0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();

                if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    KbdLlHookStruct keyInfo = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

                    if (keyInfo.vkCode == VK_NUMLOCK)
                    {
                        Dispatcher.BeginInvoke(UpdateNumLockIndicator);
                    }
                }
            }

            return CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        private sealed class AppConfig
        {
            public double Left { get; set; }
            public double Top { get; set; }

            public string? ScreenDeviceName { get; set; }
            public int? LeftPx { get; set; }
            public int? TopPx { get; set; }

            public bool IsVisible { get; set; }
            public bool MovementEnabled { get; set; }
            public bool TopMostEnabled { get; set; }
            public bool RunAtStartupEnabled { get; set; }
            public RgbaConfig? Active { get; set; }
            public RgbaConfig? Inactive { get; set; }
        }

        private sealed class RgbaConfig
        {
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
            public byte A { get; set; }
        }

        // ----------------- RGBA editor (WinForms) -----------------
        private RgbaStyle? ShowRgbaEditor(string title, RgbaStyle current)
        {
            ColorEditorWindow dialog = new(title, current.R, current.G, current.B, current.A)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
                return null;

            return new RgbaStyle(
                dialog.SelectedR,
                dialog.SelectedG,
                dialog.SelectedB,
                dialog.SelectedA);
        }

        // ----------------- Cierre / cleanup -----------------
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!allowExit)
            {
                e.Cancel = true;
                if (visibleMenuItem is not null) visibleMenuItem.Checked = false;
                FlushPendingConfigurationSave();
                Hide();
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }

            if (foregroundEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(foregroundEventHook);
                foregroundEventHook = IntPtr.Zero;
            }

            if (trayIcon is not null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }

            trayMenu?.Dispose();
            trayMenu = null;
        }

        private readonly struct RgbaStyle
        {
            public byte R { get; }
            public byte G { get; }
            public byte B { get; }
            public byte A { get; }

            public RgbaStyle(byte r, byte g, byte b, byte a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public static RgbaStyle FromOpacity(byte r, byte g, byte b, double opacity)
            {
                opacity = Math.Max(0.0, Math.Min(1.0, opacity));
                byte a = (byte)Math.Round(opacity * 255.0);
                return new RgbaStyle(r, g, b, a);
            }
        }
    }
}