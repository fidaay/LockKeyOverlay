namespace LockKeyOverlay;

internal readonly struct RgbaStyle : IEquatable<RgbaStyle>
{
    private const double MinimumOverlayOpacity = 0.05;

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

    public double Opacity => A / 255.0;

    public double OverlayOpacity => Math.Max(MinimumOverlayOpacity, Opacity);

    public double Luminance => (0.299 * R) + (0.587 * G) + (0.114 * B);

    public bool UsesDarkForeground => Luminance > 150;

    public static RgbaStyle FromOpacity(byte r, byte g, byte b, double opacity)
    {
        opacity = Math.Max(0.0, Math.Min(1.0, opacity));
        byte a = (byte)Math.Round(opacity * 255.0);
        return new RgbaStyle(r, g, b, a);
    }

    public bool Equals(RgbaStyle other)
    {
        return R == other.R
            && G == other.G
            && B == other.B
            && A == other.A;
    }

    public override bool Equals(object? obj)
    {
        return obj is RgbaStyle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B, A);
    }
}
