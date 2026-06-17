namespace LockKeyOverlay;

internal sealed class RgbaConfig
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public static RgbaConfig FromStyle(RgbaStyle style)
    {
        return new RgbaConfig
        {
            R = style.R,
            G = style.G,
            B = style.B,
            A = style.A
        };
    }

    public RgbaStyle ToStyle()
    {
        return new RgbaStyle(R, G, B, A);
    }
}
