using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LockKeyOverlay;

public partial class MainWindow : Window
{
    private const int ConfigSaveDebounceMs = 250;

    private static readonly RgbaStyle DefaultActiveStyle = RgbaStyle.FromOpacity(255, 140, 0, 0.85);
    private static readonly RgbaStyle DefaultInactiveStyle = RgbaStyle.FromOpacity(30, 144, 255, 0.75);

    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly KeyboardHookService _keyboardHookService = new();
    private readonly ForegroundHookService _foregroundHookService = new();
    private readonly ScreenPlacementService _screenPlacementService = new();
    private readonly AsusAuraBacklightBlinkService _asusAuraBacklightBlinkService;
    private readonly DispatcherTimer _configSaveDebounceTimer;

    private WindowInteropService? _windowInteropService;
    private TrayMenuService? _trayMenuService;

    private IntPtr _windowHandle = IntPtr.Zero;
    private bool _allowExit;
    private bool _movementEnabled = true;
    private bool _isDragging;
    private bool _suppressConfigSave;

    private Drawing.Point _dragStartMousePx;
    private Drawing.Point _dragStartWindowTopLeftPx;

    private BitmapImage? _numLockOnIcon;
    private BitmapImage? _numLockOffIcon;

    private RgbaStyle _activeStyle = DefaultActiveStyle;
    private RgbaStyle _inactiveStyle = DefaultInactiveStyle;

    public MainWindow()
    {
        InitializeComponent();

        _asusAuraBacklightBlinkService = new AsusAuraBacklightBlinkService(
            new Win32NumLockStateReader(),
            new AsusAuraBacklightController(),
            new DispatcherIntervalTimer(Dispatcher),
            new DispatcherIntervalTimer(Dispatcher));

        _configSaveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ConfigSaveDebounceMs)
        };
        _configSaveDebounceTimer.Tick += ConfigSaveDebounceTimer_Tick;

        _keyboardHookService.NumLockReleased += KeyboardHookService_NumLockReleased;
        _foregroundHookService.ForegroundChanged += ForegroundHookService_ForegroundChanged;
        _asusAuraBacklightBlinkService.IssueReported += AsusAuraBacklightBlinkService_IssueReported;

        LocationChanged += Window_LocationChanged;
        IsVisibleChanged += Window_IsVisibleChanged;
        Closing += Window_Closing;
        Closed += Window_Closed;

        SystemEvents.SessionEnding += SystemEvents_SessionEnding;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowInteropService = new WindowInteropService(_windowHandle);

        ReportNonFatalIssue(_windowInteropService.ApplyToolWindowStyle());
        ApplyClickThroughState();
        ApplyTopMostState();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Left = 20;
        Top = 20;

        try
        {
            LoadIndicatorIcons();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            Forms.MessageBox.Show(
                $"No se pudieron cargar los iconos de LockKeyOverlay.\n\n{ex.Message}",
                "LockKeyOverlay",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);

            _allowExit = true;
            Close();
            return;
        }

        ConfigureTray();
        StartHooks();

        bool configLoaded = LoadConfiguration();
        if (!configLoaded)
            CenterOverlayOnPrimaryScreen();

        Cursor = _movementEnabled ? WpfCursors.SizeAll : WpfCursors.Arrow;
        UpdateNumLockIndicator();
    }

    private void LoadIndicatorIcons()
    {
        string assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

        string onIconPath = Path.Combine(assetsDir, "lock_closed.png");
        string offIconPath = Path.Combine(assetsDir, "lock_open.png");

        if (!File.Exists(onIconPath))
            throw new FileNotFoundException($"No se encontró el archivo: {onIconPath}");

        if (!File.Exists(offIconPath))
            throw new FileNotFoundException($"No se encontró el archivo: {offIconPath}");

        _numLockOnIcon = LoadBitmap(onIconPath);
        _numLockOffIcon = LoadBitmap(offIconPath);
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

    private static string GetAssetPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
    }

    private Drawing.Icon? LoadTrayIcon()
    {
        string iconPath = GetAssetPath("lockkeyoverlay.ico");

        try
        {
            if (!File.Exists(iconPath))
                throw new FileNotFoundException($"No se encontró el archivo: {iconPath}");

            return new Drawing.Icon(iconPath);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or System.Runtime.InteropServices.ExternalException)
        {
            ReportNonFatalIssue(ServiceResult.Failure($"No se pudo cargar el icono del tray: {iconPath}", ex));
            return null;
        }
    }

    private void ConfigureTray()
    {
        ServiceResult<bool> startupState = _startupService.IsEnabled();
        ReportNonFatalIssue(startupState.ToServiceResult());

        _trayMenuService = new TrayMenuService(startupState.Value, LoadTrayIcon());
        _trayMenuService.VisibleChanged += (_, _) => ApplyVisibilityFromTray();
        _trayMenuService.MovementChanged += (_, _) => ApplyMovementFromTray();
        _trayMenuService.TopMostChanged += (_, _) =>
        {
            ApplyTopMostState();
            RequestSaveConfiguration();
        };
        _trayMenuService.RunAtStartupChanged += (_, _) => ApplyStartupFromTray();
        _trayMenuService.AsusAuraBacklightBlinkChanged += (_, _) => ApplyAsusAuraBacklightBlinkFromTray();
        _trayMenuService.ResetConfigurationRequested += (_, _) => ConfirmAndResetConfiguration();
        _trayMenuService.ActiveColorChangeRequested += (_, _) => EditActiveColor();
        _trayMenuService.InactiveColorChangeRequested += (_, _) => EditInactiveColor();
        _trayMenuService.ExitRequested += (_, _) =>
        {
            _allowExit = true;
            FlushPendingConfigurationSave();
            ReportNonFatalIssue(_asusAuraBacklightBlinkService.StopAndRestore());
            _trayMenuService.HideIcon();
            Close();
        };
    }

    private void StartHooks()
    {
        ServiceResult foregroundResult = _foregroundHookService.Start();
        ReportNonFatalIssue(foregroundResult, showDialog: !foregroundResult.Succeeded);

        ServiceResult keyboardResult = _keyboardHookService.Start();
        ReportNonFatalIssue(keyboardResult, showDialog: !keyboardResult.Succeeded);
    }

    private void KeyboardHookService_NumLockReleased(object? sender, EventArgs e)
    {
        DispatcherInvocation.TryBeginInvoke(Dispatcher, UpdateNumLockIndicator);
    }

    private void ForegroundHookService_ForegroundChanged(object? sender, EventArgs e)
    {
        if (_trayMenuService?.TopMostEnabled != true)
            return;

        if (!IsVisible)
            return;

        if (_windowHandle == IntPtr.Zero)
            return;

        DispatcherInvocation.TryBeginInvoke(Dispatcher, ApplyTopMostState);
    }

    private void AsusAuraBacklightBlinkService_IssueReported(object? sender, ServiceResultEventArgs e)
    {
        ReportNonFatalIssue(e.Result);
    }

    private void ApplyVisibilityFromTray()
    {
        if (_trayMenuService is null)
            return;

        if (_trayMenuService.IsVisibleChecked)
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

    private void ApplyMovementFromTray()
    {
        if (_trayMenuService is null)
            return;

        _movementEnabled = _trayMenuService.MovementEnabled;
        Cursor = _movementEnabled ? WpfCursors.SizeAll : WpfCursors.Arrow;
        ApplyClickThroughState();
        RequestSaveConfiguration();
    }

    private void ApplyStartupFromTray()
    {
        if (_trayMenuService is null)
            return;

        bool requestedState = _trayMenuService.RunAtStartupEnabled;
        ServiceResult result = _startupService.SetEnabled(requestedState);

        if (!result.Succeeded)
        {
            _trayMenuService.SetRunAtStartupEnabledSilently(!requestedState);
            ReportNonFatalIssue(result, showDialog: true);
            return;
        }

        RequestSaveConfiguration();
    }

    private void ApplyAsusAuraBacklightBlinkFromTray()
    {
        if (_trayMenuService is null)
            return;

        bool requestedState = _trayMenuService.AsusAuraBacklightBlinkWhenNumLockOnEnabled;
        ServiceResult result = _asusAuraBacklightBlinkService.SetEnabled(
            requestedState);

        if (!result.Succeeded)
            _trayMenuService.SetAsusAuraBacklightBlinkWhenNumLockOnEnabledSilently(_asusAuraBacklightBlinkService.Enabled);

        ReportNonFatalIssue(result, showDialog: !result.Succeeded);
        RequestSaveConfiguration();
    }

    internal void ShowFromExternalActivation()
    {
        if (!Dispatcher.CheckAccess())
        {
            DispatcherInvocation.TryBeginInvoke(Dispatcher, ShowFromExternalActivation);
            return;
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        _trayMenuService?.SetVisibleCheckedSilently(true);

        if (!IsVisible)
            Show();

        ApplyTopMostState();

        if (IsLoaded)
            FlushPendingConfigurationSave();
    }

    internal void ExitFromExternalShutdown()
    {
        if (!Dispatcher.CheckAccess())
        {
            DispatcherInvocation.TryBeginInvoke(Dispatcher, ExitFromExternalShutdown);
            return;
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        _allowExit = true;

        if (IsLoaded)
            FlushPendingConfigurationSave();

        ReportNonFatalIssue(_asusAuraBacklightBlinkService.StopAndRestore());
        _trayMenuService?.HideIcon();
        Close();
    }

    private void ConfirmAndResetConfiguration()
    {
        var result = Forms.MessageBox.Show(
            "Se eliminará la configuración guardada y se restaurarán los valores por defecto.\n\n¿Deseas continuar?",
            "Eliminar configuración",
            Forms.MessageBoxButtons.YesNo,
            Forms.MessageBoxIcon.Warning,
            Forms.MessageBoxDefaultButton.Button2);

        if (result != Forms.DialogResult.Yes)
            return;

        ServiceResult deleteResult = _configService.Delete();
        ReportNonFatalIssue(deleteResult, showDialog: !deleteResult.Succeeded);
        ApplyDefaultConfiguration();
    }

    private void EditActiveColor()
    {
        RgbaStyle? edited = ShowRgbaEditor("Color estado activo", _activeStyle);

        if (edited is null)
            return;

        _activeStyle = edited.Value;
        UpdateNumLockIndicator();
        RequestSaveConfiguration();
    }

    private void EditInactiveColor()
    {
        RgbaStyle? edited = ShowRgbaEditor("Color estado inactivo", _inactiveStyle);

        if (edited is null)
            return;

        _inactiveStyle = edited.Value;
        UpdateNumLockIndicator();
        RequestSaveConfiguration();
    }

    private void UpdateNumLockIndicator()
    {
        bool numLockOn = KeyboardHookService.IsNumLockOn();
        ApplyVisualState(numLockOn);
    }

    private void ApplyVisualState(bool numLockOn)
    {
        RgbaStyle style = numLockOn ? _activeStyle : _inactiveStyle;

        StateText.Text = numLockOn ? "NUM LK ON " : "NUM LK OFF";
        StateIcon.Source = numLockOn ? _numLockOnIcon : _numLockOffIcon;

        OverlayBorder.Opacity = style.OverlayOpacity;
        OverlayBorder.Background = new SolidColorBrush(WpfColor.FromRgb(style.R, style.G, style.B));
        StateText.Foreground = style.UsesDarkForeground ? WpfBrushes.Black : WpfBrushes.White;
    }

    private void RequestSaveConfiguration()
    {
        if (_suppressConfigSave)
            return;

        _configSaveDebounceTimer.Stop();
        _configSaveDebounceTimer.Start();
    }

    private void FlushPendingConfigurationSave()
    {
        if (_configSaveDebounceTimer.IsEnabled)
            _configSaveDebounceTimer.Stop();

        SaveConfiguration();
    }

    private void ConfigSaveDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _configSaveDebounceTimer.Stop();
        SaveConfiguration();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (IsLoaded)
            RequestSaveConfiguration();
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
            RequestSaveConfiguration();
    }

    private DpiScale GetWindowDpiScale()
    {
        var source = PresentationSource.FromVisual(this);

        if (source?.CompositionTarget is null)
            return DpiScale.One;

        return new DpiScale(
            source.CompositionTarget.TransformToDevice.M11,
            source.CompositionTarget.TransformToDevice.M22);
    }

    private Drawing.Point GetWindowTopLeftPxWithFallbackScale()
    {
        var (scaleX, scaleY) = GetWindowDpiScale();

        int x = (int)Math.Round(Left * scaleX);
        int y = (int)Math.Round(Top * scaleY);

        return new Drawing.Point(x, y);
    }

    private WindowPositionSnapshot? CaptureCurrentWindowPosition()
    {
        IReadOnlyList<ScreenSnapshot> screens = CaptureScreens();
        if (screens.Count == 0)
            return null;

        return _screenPlacementService.CaptureWindowPosition(
            Left,
            Top,
            screens,
            CapturePrimaryScreen(),
            GetWindowDpiScale());
    }

    private static IReadOnlyList<ScreenSnapshot> CaptureScreens()
    {
        return Forms.Screen.AllScreens
            .Select(ToScreenSnapshot)
            .ToArray();
    }

    private static ScreenSnapshot? CapturePrimaryScreen()
    {
        Forms.Screen? primary = Forms.Screen.PrimaryScreen;
        return primary is null ? null : ToScreenSnapshot(primary);
    }

    private static ScreenSnapshot ToScreenSnapshot(Forms.Screen screen)
    {
        return new ScreenSnapshot(screen.DeviceName, screen.Bounds, screen.WorkingArea);
    }

    private void RestoreWindowPositionSafely(AppConfig config)
    {
        IReadOnlyList<ScreenSnapshot> screens = CaptureScreens();
        ScreenSnapshot? primaryScreen = CapturePrimaryScreen();

        if (screens.Count == 0)
            return;

        WindowPlacement placement = _screenPlacementService.RestoreWindowPosition(
            config,
            screens,
            primaryScreen,
            GetWindowDpiScale(),
            ActualWidth,
            ActualHeight);

        Left = placement.Left;
        Top = placement.Top;
    }

    private void CenterOverlayOnPrimaryScreen()
    {
        ScreenSnapshot? primaryScreen = CapturePrimaryScreen();

        if (primaryScreen is null)
            return;

        WindowPlacement placement = _screenPlacementService.CenterInScreen(
            primaryScreen.Value,
            GetWindowDpiScale(),
            ActualWidth,
            ActualHeight);

        Left = placement.Left;
        Top = placement.Top;
    }

    private void SaveConfiguration()
    {
        if (_suppressConfigSave)
            return;

        WindowPositionSnapshot? currentWindowPosition = CaptureCurrentWindowPosition();
        Drawing.Point currentWindowTopLeftPx = currentWindowPosition?.TopLeftPx ?? GetWindowTopLeftPxWithFallbackScale();

        var config = new AppConfig
        {
            Left = Left,
            Top = Top,
            ScreenDeviceName = currentWindowPosition?.Screen.DeviceName,
            LeftPx = currentWindowTopLeftPx.X,
            TopPx = currentWindowTopLeftPx.Y,
            IsVisible = IsVisible,
            MovementEnabled = _trayMenuService?.MovementEnabled ?? _movementEnabled,
            TopMostEnabled = _trayMenuService?.TopMostEnabled ?? Topmost,
            RunAtStartupEnabled = _trayMenuService?.RunAtStartupEnabled ?? false,
            AsusAuraBacklightBlinkWhenNumLockOnEnabled =
                _trayMenuService?.AsusAuraBacklightBlinkWhenNumLockOnEnabled ?? _asusAuraBacklightBlinkService.Enabled,
            Active = RgbaConfig.FromStyle(_activeStyle),
            Inactive = RgbaConfig.FromStyle(_inactiveStyle)
        };

        ServiceResult result = _configService.Save(config);
        ReportNonFatalIssue(result);
    }

    private bool LoadConfiguration()
    {
        ConfigLoadResult result = _configService.Load();

        if (result.Status == ConfigLoadStatus.NotFound)
            return false;

        if (result.Status != ConfigLoadStatus.Loaded || result.Config is null)
        {
            ReportNonFatalIssue(
                ServiceResult.Failure(result.Message, result.Exception),
                showDialog: result.Status == ConfigLoadStatus.Failed);

            return false;
        }

        _suppressConfigSave = true;

        try
        {
            ApplyConfiguration(result.Config);
            return true;
        }
        finally
        {
            _suppressConfigSave = false;
        }
    }

    private void ApplyConfiguration(AppConfig config)
    {
        RestoreWindowPositionSafely(config);

        _activeStyle = config.Active?.ToStyle() ?? DefaultActiveStyle;
        _inactiveStyle = config.Inactive?.ToStyle() ?? DefaultInactiveStyle;

        if (_trayMenuService is null)
            return;

        _trayMenuService.SetMovementEnabledSilently(config.MovementEnabled);
        _trayMenuService.SetTopMostEnabledSilently(config.TopMostEnabled);
        _trayMenuService.SetVisibleCheckedSilently(config.IsVisible);
        _trayMenuService.SetAsusAuraBacklightBlinkWhenNumLockOnEnabledSilently(
            config.AsusAuraBacklightBlinkWhenNumLockOnEnabled);
        SynchronizeStartupRegistration(config);
        ServiceResult blinkResult = _asusAuraBacklightBlinkService.SetEnabled(config.AsusAuraBacklightBlinkWhenNumLockOnEnabled);
        if (!blinkResult.Succeeded)
            _trayMenuService.SetAsusAuraBacklightBlinkWhenNumLockOnEnabledSilently(_asusAuraBacklightBlinkService.Enabled);

        ReportNonFatalIssue(blinkResult);

        _movementEnabled = config.MovementEnabled;
        Cursor = _movementEnabled ? WpfCursors.SizeAll : WpfCursors.Arrow;

        ApplyClickThroughState();
        ApplyTopMostState();

        if (config.IsVisible)
            Show();
        else
            Hide();
    }

    private void SynchronizeStartupRegistration(AppConfig config)
    {
        if (_trayMenuService is null)
            return;

        ServiceResult<StartupRegistrationState> startupStateResult = _startupService.GetRegistrationState();
        ReportNonFatalIssue(startupStateResult.ToServiceResult());

        if (!startupStateResult.Succeeded)
            return;

        StartupRegistrationState startupState = startupStateResult.Value;
        _trayMenuService.SetRunAtStartupEnabledSilently(startupState.IsEnabled);

        if (!StartupRegistrationRepair.ShouldRepair(config, startupState, GetCurrentExecutableFileName()))
            return;

        ServiceResult repairResult = _startupService.SetEnabled(enabled: true);
        ReportNonFatalIssue(repairResult);

        if (repairResult.Succeeded)
            _trayMenuService.SetRunAtStartupEnabledSilently(true);
    }

    private static string GetCurrentExecutableFileName()
    {
        string? fileName = Path.GetFileName(Environment.ProcessPath);

        return string.IsNullOrWhiteSpace(fileName)
            ? "LockKeyOverlay.exe"
            : fileName;
    }

    private void ApplyDefaultConfiguration()
    {
        _suppressConfigSave = true;

        try
        {
            _activeStyle = DefaultActiveStyle;
            _inactiveStyle = DefaultInactiveStyle;

            CenterOverlayOnPrimaryScreen();

            _movementEnabled = true;
            _trayMenuService?.SetMovementEnabledSilently(true);
            _trayMenuService?.SetTopMostEnabledSilently(true);
            _trayMenuService?.SetVisibleCheckedSilently(true);
            _trayMenuService?.SetAsusAuraBacklightBlinkWhenNumLockOnEnabledSilently(false);
            ReportNonFatalIssue(_asusAuraBacklightBlinkService.SetEnabled(enabled: false));
            ServiceResult startupResetResult = DefaultConfigurationStartupReset.DisableRunAtStartup(
                _startupService.SetEnabled,
                enabled => _trayMenuService?.SetRunAtStartupEnabledSilently(enabled));
            ReportNonFatalIssue(startupResetResult, showDialog: !startupResetResult.Succeeded);

            Show();
        }
        finally
        {
            _suppressConfigSave = false;
        }

        Cursor = WpfCursors.SizeAll;
        ApplyClickThroughState();
        ApplyTopMostState();
        UpdateNumLockIndicator();
        SaveConfiguration();
    }

    private void Overlay_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (!_movementEnabled)
            return;

        _isDragging = true;
        _dragStartMousePx = Forms.Cursor.Position;
        _dragStartWindowTopLeftPx = CaptureCurrentWindowPosition()?.TopLeftPx ?? GetWindowTopLeftPxWithFallbackScale();

        Mouse.Capture(OverlayBorder);
    }

    private void Overlay_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_movementEnabled)
            return;

        if (!_isDragging)
            return;

        var currentMousePx = Forms.Cursor.Position;
        IReadOnlyList<ScreenSnapshot> screens = CaptureScreens();
        ScreenSnapshot? primaryScreen = CapturePrimaryScreen();

        WindowPlacement placement = _screenPlacementService.MoveWindowByPhysicalDelta(
            _dragStartWindowTopLeftPx,
            _dragStartMousePx,
            currentMousePx,
            screens,
            primaryScreen,
            GetWindowDpiScale(),
            ActualWidth,
            ActualHeight);

        Left = placement.Left;
        Top = placement.Top;
    }

    private void Overlay_MouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        _isDragging = false;
        Mouse.Capture(null);

        FlushPendingConfigurationSave();
    }

    private void ApplyClickThroughState()
    {
        if (_windowInteropService is null)
            return;

        ServiceResult result = _windowInteropService.ApplyClickThrough(!_movementEnabled);
        ReportNonFatalIssue(result);
    }

    private void ApplyTopMostState()
    {
        bool enabled = _trayMenuService?.TopMostEnabled ?? true;

        Topmost = enabled;

        if (_windowInteropService is null)
            return;

        ServiceResult result = _windowInteropService.ApplyTopMost(enabled);
        ReportNonFatalIssue(result);
    }

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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowExit)
            return;

        e.Cancel = true;
        _trayMenuService?.SetVisibleCheckedSilently(false);
        Hide();
        FlushPendingConfigurationSave();
    }

    private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
    {
        DispatcherInvocation.TryInvoke(Dispatcher, TryHandleSessionEnding, DispatcherPriority.Send);
    }

    private void HandleSessionEnding()
    {
        _allowExit = true;
        FlushPendingConfigurationSave();
        ReportNonFatalIssue(_asusAuraBacklightBlinkService.StopAndRestore());
        _trayMenuService?.HideIcon();
    }

    private void TryHandleSessionEnding()
    {
        try
        {
            HandleSessionEnding();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Session ending cleanup failed: {ex}");
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _configSaveDebounceTimer.Stop();
        _configSaveDebounceTimer.Tick -= ConfigSaveDebounceTimer_Tick;
        SystemEvents.SessionEnding -= SystemEvents_SessionEnding;

        _keyboardHookService.NumLockReleased -= KeyboardHookService_NumLockReleased;
        _foregroundHookService.ForegroundChanged -= ForegroundHookService_ForegroundChanged;
        _asusAuraBacklightBlinkService.IssueReported -= AsusAuraBacklightBlinkService_IssueReported;

        _keyboardHookService.Dispose();
        _foregroundHookService.Dispose();
        _asusAuraBacklightBlinkService.Dispose();

        _trayMenuService?.Dispose();
        _trayMenuService = null;
    }

    private static void ReportNonFatalIssue(ServiceResult result, bool showDialog = false)
    {
        if (result.Succeeded)
            return;

        Debug.WriteLine(result.DiagnosticMessage);

        if (!showDialog)
            return;

        Forms.MessageBox.Show(
            result.DiagnosticMessage,
            "LockKeyOverlay",
            Forms.MessageBoxButtons.OK,
            Forms.MessageBoxIcon.Warning);
    }
}
