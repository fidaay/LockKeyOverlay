namespace LockKeyOverlay;

internal static class StartupConfigurationValue
{
    public static bool ResolveFromRegistryState(
        ServiceResult<StartupRegistrationState> startupStateResult,
        bool fallback)
    {
        return startupStateResult.Succeeded
            ? startupStateResult.Value.IsEnabled
            : fallback;
    }
}
