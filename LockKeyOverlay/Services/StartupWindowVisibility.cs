namespace LockKeyOverlay;

internal static class StartupWindowVisibility
{
    public static bool ShouldShowOnStartup(ConfigLoadResult? configLoadResult)
    {
        if (configLoadResult?.Status != ConfigLoadStatus.Loaded)
            return true;

        return configLoadResult.Config?.IsVisible != false;
    }

    public static bool ResolveMovementEnabledOnStartup(ConfigLoadResult? configLoadResult)
    {
        if (configLoadResult?.Status != ConfigLoadStatus.Loaded)
            return true;

        return configLoadResult.Config?.MovementEnabled != false;
    }
}
