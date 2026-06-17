namespace LockKeyOverlay;

internal static class DefaultConfigurationStartupReset
{
    public static ServiceResult DisableRunAtStartup(
        Func<bool, ServiceResult> setStartupEnabled,
        Action<bool> setTrayStateSilently)
    {
        ServiceResult result = setStartupEnabled(false);
        if (result.Succeeded)
            setTrayStateSilently(false);

        return result;
    }
}
