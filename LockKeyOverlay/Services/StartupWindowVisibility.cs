namespace LockKeyOverlay;

internal static class StartupWindowVisibility
{
    public static bool ShouldShowOnStartup(ConfigLoadResult? configLoadResult)
    {
        if (configLoadResult?.Status != ConfigLoadStatus.Loaded)
            return true;

        return configLoadResult.Config?.IsVisible != false;
    }
}
