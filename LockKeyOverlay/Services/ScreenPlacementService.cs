using System.Drawing;
using System.Runtime.InteropServices;

namespace LockKeyOverlay;

internal readonly record struct DpiScale(double ScaleX, double ScaleY)
{
    public static DpiScale One { get; } = new(1.0, 1.0);

    public bool IsValid => ScaleX > 0 && ScaleY > 0;

    public static DpiScale FromDpi(double dpiX, double dpiY)
    {
        return new DpiScale(dpiX / 96.0, dpiY / 96.0);
    }
}

internal readonly record struct ScreenSnapshot(
    string DeviceName,
    Rectangle Bounds,
    Rectangle WorkingArea);

internal readonly record struct WindowPlacement(double Left, double Top);

internal sealed class ScreenPlacementService
{
    private readonly Func<ScreenSnapshot, DpiScale?> _dpiScaleProvider;

    public ScreenPlacementService(Func<ScreenSnapshot, DpiScale?>? dpiScaleProvider = null)
    {
        _dpiScaleProvider = dpiScaleProvider ?? TryGetMonitorDpiScale;
    }

    public WindowPlacement RestoreWindowPosition(
        AppConfig config,
        IReadOnlyList<ScreenSnapshot> screens,
        ScreenSnapshot? primaryScreen,
        DpiScale fallbackScale,
        double windowWidthDip,
        double windowHeightDip)
    {
        if (screens.Count == 0)
            return new WindowPlacement(config.Left, config.Top);

        DpiScale initialScale = EnsureValid(fallbackScale);

        double desiredLeftPx = config.LeftPx ?? (config.Left * initialScale.ScaleX);
        double desiredTopPx = config.TopPx ?? (config.Top * initialScale.ScaleY);

        ScreenSnapshot targetScreen =
            FindScreenByDeviceName(screens, config.ScreenDeviceName)
            ?? FindNearestScreen(screens, new Point((int)Math.Round(desiredLeftPx), (int)Math.Round(desiredTopPx)))
            ?? primaryScreen
            ?? screens[0];

        DpiScale targetScale = GetDpiScale(targetScreen, initialScale);

        if (!config.LeftPx.HasValue || !config.TopPx.HasValue)
        {
            desiredLeftPx = config.Left * targetScale.ScaleX;
            desiredTopPx = config.Top * targetScale.ScaleY;
        }

        return ClampToBounds(
            desiredLeftPx,
            desiredTopPx,
            windowWidthDip,
            windowHeightDip,
            targetScreen.Bounds,
            targetScale);
    }

    public WindowPlacement CenterInScreen(
        ScreenSnapshot screen,
        DpiScale fallbackScale,
        double windowWidthDip,
        double windowHeightDip)
    {
        DpiScale scale = GetDpiScale(screen, fallbackScale);
        Rectangle workArea = screen.WorkingArea;

        double windowWidthPx = Math.Max(1, windowWidthDip * scale.ScaleX);
        double windowHeightPx = Math.Max(1, windowHeightDip * scale.ScaleY);

        double leftPx = workArea.Left + ((workArea.Width - windowWidthPx) / 2.0);
        double topPx = workArea.Top + ((workArea.Height - windowHeightPx) / 2.0);

        return new WindowPlacement(leftPx / scale.ScaleX, topPx / scale.ScaleY);
    }

    private DpiScale GetDpiScale(ScreenSnapshot screen, DpiScale fallbackScale)
    {
        DpiScale? dpiScale = _dpiScaleProvider(screen);
        return EnsureValid(dpiScale ?? fallbackScale);
    }

    private static DpiScale EnsureValid(DpiScale scale)
    {
        return scale.IsValid ? scale : DpiScale.One;
    }

    private static WindowPlacement ClampToBounds(
        double desiredLeftPx,
        double desiredTopPx,
        double windowWidthDip,
        double windowHeightDip,
        Rectangle boundsPx,
        DpiScale scale)
    {
        double windowWidthPx = Math.Max(1, windowWidthDip * scale.ScaleX);
        double windowHeightPx = Math.Max(1, windowHeightDip * scale.ScaleY);

        double minLeftPx = boundsPx.Left;
        double minTopPx = boundsPx.Top;

        double maxLeftPx = Math.Max(minLeftPx, boundsPx.Right - windowWidthPx);
        double maxTopPx = Math.Max(minTopPx, boundsPx.Bottom - windowHeightPx);

        double clampedLeftPx = Math.Max(minLeftPx, Math.Min(desiredLeftPx, maxLeftPx));
        double clampedTopPx = Math.Max(minTopPx, Math.Min(desiredTopPx, maxTopPx));

        return new WindowPlacement(clampedLeftPx / scale.ScaleX, clampedTopPx / scale.ScaleY);
    }

    private static ScreenSnapshot? FindScreenByDeviceName(IReadOnlyList<ScreenSnapshot> screens, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        foreach (ScreenSnapshot screen in screens)
        {
            if (string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        return null;
    }

    private static ScreenSnapshot? FindNearestScreen(IReadOnlyList<ScreenSnapshot> screens, Point point)
    {
        ScreenSnapshot? nearest = null;
        long nearestDistance = long.MaxValue;

        foreach (ScreenSnapshot screen in screens)
        {
            if (screen.Bounds.Contains(point))
                return screen;

            int nearestX = Math.Max(screen.Bounds.Left, Math.Min(point.X, screen.Bounds.Right));
            int nearestY = Math.Max(screen.Bounds.Top, Math.Min(point.Y, screen.Bounds.Bottom));

            long dx = point.X - nearestX;
            long dy = point.Y - nearestY;
            long distance = (dx * dx) + (dy * dy);

            if (distance < nearestDistance)
            {
                nearest = screen;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static DpiScale? TryGetMonitorDpiScale(ScreenSnapshot screen)
    {
        try
        {
            RECT rect = new()
            {
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Right = screen.Bounds.Right,
                Bottom = screen.Bounds.Bottom
            };

            IntPtr monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return null;

            int result = GetDpiForMonitor(monitor, MonitorDpiType.EffectiveDpi, out uint dpiX, out uint dpiY);
            return result == 0 ? DpiScale.FromDpi(dpiX, dpiY) : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    private enum MonitorDpiType
    {
        EffectiveDpi = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
