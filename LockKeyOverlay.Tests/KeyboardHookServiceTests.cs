namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class KeyboardHookServiceTests
{
    [TestMethod]
    public void IsInjectedKeyEvent_ReturnsFalseWithoutInjectedFlag()
    {
        Assert.IsFalse(KeyboardHookService.IsInjectedKeyEvent(flags: 0));
    }

    [TestMethod]
    public void IsInjectedKeyEvent_ReturnsTrueWithInjectedFlag()
    {
        Assert.IsTrue(KeyboardHookService.IsInjectedKeyEvent(flags: 0x10));
    }
}
