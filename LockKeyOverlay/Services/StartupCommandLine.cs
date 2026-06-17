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

        int searchIndex = 0;
        bool sawExeSuffix = false;

        while (searchIndex < commandLine.Length)
        {
            int exeIndex = commandLine.IndexOf(".exe", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (exeIndex < 0)
                break;

            sawExeSuffix = true;

            int executableEndIndex = exeIndex + 4;
            if (executableEndIndex == commandLine.Length || char.IsWhiteSpace(commandLine[executableEndIndex]))
            {
                executablePath = commandLine[..executableEndIndex].Trim();
                return executablePath.Length > 0;
            }

            searchIndex = executableEndIndex;
        }

        if (sawExeSuffix)
            return false;

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
