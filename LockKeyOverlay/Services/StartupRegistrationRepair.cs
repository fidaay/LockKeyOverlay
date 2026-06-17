using System.IO;

namespace LockKeyOverlay;

internal static class StartupRegistrationRepair
{
    public static bool ShouldRepair(
        AppConfig config,
        StartupRegistrationState startupState,
        string expectedExecutableFileName)
    {
        if (!config.RunAtStartupEnabled)
            return false;

        if (startupState.IsEnabled)
            return false;

        if (startupState.IsBlockedByWindowsStartupSettings)
            return false;

        if (!startupState.HasRegistryValue)
            return true;

        if (string.IsNullOrWhiteSpace(startupState.RegisteredExecutablePath))
            return false;

        string registeredFileName = Path.GetFileName(startupState.RegisteredExecutablePath);

        return string.Equals(
            registeredFileName,
            expectedExecutableFileName,
            StringComparison.OrdinalIgnoreCase);
    }
}
