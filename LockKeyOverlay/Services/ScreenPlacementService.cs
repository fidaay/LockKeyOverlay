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

internal readonly record struct WindowPositionSnapshot(
    ScreenSnapshot Screen,
    Point TopLeftPx,
    DpiScale Scale);

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

    public WindowPositionSnapshot? CaptureWindowPosition(
        double leftDip,
        double topDip,
        IReadOnlyList<ScreenSnapshot> screens,
        ScreenSnapshot? fallbackScreen,
        DpiScale fallbackScale)
    {
        if (screens.Count == 0)
            return null;

        DpiScale initialScale = EnsureValid(fallbackScale);
        ScreenSnapshot targetScreen =
            FindScreenForDipPoint(screens, leftDip, topDip, initialScale)
            ?? fallbackScreen
            ?? screens[0];
        DpiScale targetScale = GetDpiScale(targetScreen, initialScale);
        Point topLeftPx = ToPhysicalPoint(leftDip, topDip, targetScale);

        return new WindowPositionSnapshot(targetScreen, topLeftPx, targetScale);
    }

    public WindowPlacement MoveWindowByPhysicalDelta(
        Point startTopLeftPx,
        Point startMousePx,
        Point currentMousePx,
        IReadOnlyList<ScreenSnapshot> screens,
        ScreenSnapshot? fallbackScreen,
        DpiScale fallbackScale,
        double windowWidthDip,
        double windowHeightDip)
    {
        DpiScale initialScale = EnsureValid(fallbackScale);

        Point desiredTopLeftPx = new(
            startTopLeftPx.X + currentMousePx.X - startMousePx.X,
            startTopLeftPx.Y + currentMousePx.Y - startMousePx.Y);

        ScreenSnapshot? targetScreen = FindScreenForPhysicalPoint(screens, currentMousePx, fallbackScreen);
        if (targetScreen is null)
            return new WindowPlacement(desiredTopLeftPx.X / initialScale.ScaleX, desiredTopLeftPx.Y / initialScale.ScaleY);

        return PlaceWindowFromPhysicalPosition(
            desiredTopLeftPx,
            targetScreen.Value,
            initialScale,
            windowWidthDip,
            windowHeightDip);
    }

    public WindowPlacement PlaceWindowFromPhysicalPosition(
        Point desiredTopLeftPx,
        ScreenSnapshot targetScreen,
        DpiScale fallbackScale,
        double windowWidthDip,
        double windowHeightDip)
    {
        DpiScale targetScale = GetDpiScale(targetScreen, fallbackScale);

        return ClampToBounds(
            desiredTopLeftPx.X,
            desiredTopLeftPx.Y,
            windowWidthDip,
            windowHeightDip,
            targetScreen.Bounds,
            targetScale);
    }

    public static ScreenSnapshot? FindScreenForPhysicalPoint(
        IReadOnlyList<ScreenSnapshot> screens,
        Point point,
        ScreenSnapshot? fallbackScreen = null)
    {
        return FindScreenContainingPoint(screens, point)
            ?? FindNearestScreen(screens, point)
            ?? fallbackScreen;
    }

    private DpiScale GetDpiScale(ScreenSnapshot screen, DpiScale fallbackScale)
    {
        try
        {
            DpiScale? dpiScale = _dpiScaleProvider(screen);
            return EnsureValid(dpiScale ?? fallbackScale);
        }
        catch (Exception ex) when (IsExpectedDpiFallbackException(ex))
        {
            return EnsureValid(fallbackScale);
        }
    }

    private static DpiScale EnsureValid(DpiScale scale)
    {
        return scale.IsValid ? scale : DpiScale.One;
    }

    private ScreenSnapshot? FindScreenForDipPoint(
        IReadOnlyList<ScreenSnapshot> screens,
        double leftDip,
        double topDip,
        DpiScale fallbackScale)
    {
        ScreenSnapshot? best = null;
        double bestScore = double.MaxValue;
        DpiScale preferredScale = EnsureValid(fallbackScale);

        foreach (ScreenSnapshot screen in screens)
        {
            DpiScale scale = GetDpiScale(screen, preferredScale);
            Point point = ToPhysicalPoint(leftDip, topDip, scale);

            if (!screen.Bounds.Contains(point))
                continue;

            double score = Math.Abs(scale.ScaleX - preferredScale.ScaleX) + Math.Abs(scale.ScaleY - preferredScale.ScaleY);
            if (score < bestScore)
            {
                best = screen;
                bestScore = score;
            }
        }

        if (best is not null)
            return best;

        return FindNearestScreen(screens, ToPhysicalPoint(leftDip, topDip, preferredScale));
    }

    private static Point ToPhysicalPoint(double leftDip, double topDip, DpiScale scale)
    {
        return new Point(
            (int)Math.Round(leftDip * scale.ScaleX),
            (int)Math.Round(topDip * scale.ScaleY));
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

    private static ScreenSnapshot? FindScreenContainingPoint(IReadOnlyList<ScreenSnapshot> screens, Point point)
    {
        foreach (ScreenSnapshot screen in screens)
        {
            if (screen.Bounds.Contains(point))
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
        catch (Exception ex) when (IsExpectedDpiFallbackException(ex))
        {
            return null;
        }
    }

    private static bool IsExpectedDpiFallbackException(Exception ex)
    {
        return ex is DllNotFoundException
            or EntryPointNotFoundException
            or COMException
            or SEHException
            or UnauthorizedAccessException;
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
