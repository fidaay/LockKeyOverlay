namespace LockKeyOverlay;

internal static class OverlayClickThroughState
{
    public static bool ShouldApplyClickThrough(bool hiddenStartupPending, bool movementEnabled)
    {
        return hiddenStartupPending || !movementEnabled;
    }
}
