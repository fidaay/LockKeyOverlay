namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class OverlayClickThroughStateTests
{
    [TestMethod]
    public void ShouldApplyClickThrough_ReturnsTrueDuringHiddenStartup()
    {
        bool clickThrough = OverlayClickThroughState.ShouldApplyClickThrough(
            hiddenStartupPending: true,
            movementEnabled: true);

        Assert.IsTrue(clickThrough);
    }

    [TestMethod]
    public void ShouldApplyClickThrough_ReturnsFalseAfterStartupWhenMovementIsEnabled()
    {
        bool clickThrough = OverlayClickThroughState.ShouldApplyClickThrough(
            hiddenStartupPending: false,
            movementEnabled: true);

        Assert.IsFalse(clickThrough);
    }

    [TestMethod]
    public void ShouldApplyClickThrough_ReturnsTrueAfterStartupWhenMovementIsDisabled()
    {
        bool clickThrough = OverlayClickThroughState.ShouldApplyClickThrough(
            hiddenStartupPending: false,
            movementEnabled: false);

        Assert.IsTrue(clickThrough);
    }
}
