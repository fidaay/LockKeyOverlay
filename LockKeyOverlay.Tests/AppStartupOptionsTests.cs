namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class AppStartupOptionsTests
{
    [TestMethod]
    public void Parse_EnablesShutdownExistingForInternalArgument()
    {
        AppStartupOptions options = AppStartupOptions.Parse(["--SHUTDOWN-EXISTING"]);

        Assert.IsTrue(options.ShutdownExisting);
    }

    [TestMethod]
    public void Parse_LeavesShutdownExistingDisabledForRegularArguments()
    {
        AppStartupOptions options = AppStartupOptions.Parse(["--minimized", "--other"]);

        Assert.IsFalse(options.ShutdownExisting);
    }
}
