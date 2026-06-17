namespace LockKeyOverlay;

internal sealed class AppConfig
{
    public double Left { get; set; }
    public double Top { get; set; }

    public string? ScreenDeviceName { get; set; }
    public int? LeftPx { get; set; }
    public int? TopPx { get; set; }

    public bool IsVisible { get; set; } = true;
    public bool MovementEnabled { get; set; } = true;
    public bool TopMostEnabled { get; set; } = true;
    public bool RunAtStartupEnabled { get; set; }

    public RgbaConfig? Active { get; set; }
    public RgbaConfig? Inactive { get; set; }
}
