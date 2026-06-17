namespace LockKeyOverlay;

internal readonly record struct ParsedHexColor(byte R, byte G, byte B, byte? A);

internal static class ColorHexParser
{
    public static bool IsHexString(string value)
    {
        foreach (char c in value)
        {
            bool isHex =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');

            if (!isHex)
                return false;
        }

        return true;
    }

    public static bool TryParse(string? input, out ParsedHexColor color)
    {
        color = default;

        string raw = (input ?? string.Empty).Trim();
        if (raw.StartsWith("#"))
            raw = raw[1..];

        if (raw.Length != 6 && raw.Length != 8)
            return false;

        if (!IsHexString(raw))
            return false;

        byte r = Convert.ToByte(raw.Substring(0, 2), 16);
        byte g = Convert.ToByte(raw.Substring(2, 2), 16);
        byte b = Convert.ToByte(raw.Substring(4, 2), 16);
        byte? a = raw.Length == 8
            ? Convert.ToByte(raw.Substring(6, 2), 16)
            : null;

        color = new ParsedHexColor(r, g, b, a);
        return true;
    }
}
