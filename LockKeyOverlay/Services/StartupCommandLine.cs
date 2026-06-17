using System.IO;

namespace LockKeyOverlay;

internal static class StartupCommandLine
{
    public static string Build(string executablePath)
    {
        return $"\"{executablePath}\"";
    }

    public static bool TryParseExecutablePath(string? value, out string executablePath)
    {
        executablePath = string.Empty;

        string commandLine = (value ?? string.Empty).Trim();
        if (commandLine.Length == 0)
            return false;

        if (commandLine[0] == '"')
            return TryParseQuotedExecutablePath(commandLine, out executablePath);

        return TryParseUnquotedExecutablePath(commandLine, out executablePath);
    }

    public static bool PathsEqual(string left, string right)
    {
        if (!TryNormalizePath(left, out string normalizedLeft) ||
            !TryNormalizePath(right, out string normalizedRight))
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseQuotedExecutablePath(string commandLine, out string executablePath)
    {
        executablePath = string.Empty;

        int closingQuoteIndex = commandLine.IndexOf('"', startIndex: 1);
        if (closingQuoteIndex <= 1)
            return false;

        executablePath = commandLine[1..closingQuoteIndex].Trim();
        return executablePath.Length > 0;
    }

    private static bool TryParseUnquotedExecutablePath(string commandLine, out string executablePath)
    {
        executablePath = string.Empty;

        int exeIndex = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            executablePath = commandLine[..(exeIndex + 4)].Trim();
            return executablePath.Length > 0;
        }

        int firstWhitespaceIndex = commandLine.IndexOfAny([' ', '\t']);
        executablePath = firstWhitespaceIndex >= 0
            ? commandLine[..firstWhitespaceIndex].Trim()
            : commandLine;

        return executablePath.Length > 0;
    }

    private static bool TryNormalizePath(string value, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        string trimmed = value.Trim().Trim('"');
        if (trimmed.Length == 0)
            return false;

        try
        {
            normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(trimmed));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
