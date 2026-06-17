namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class DefaultConfigurationStartupResetTests
{
    [TestMethod]
    public void DisableRunAtStartup_ClearsTrayStateWhenStartupServiceSucceeds()
    {
        bool? requestedState = null;
        bool? trayState = true;

        ServiceResult result = DefaultConfigurationStartupReset.DisableRunAtStartup(
            enabled =>
            {
                requestedState = enabled;
                return ServiceResult.Success("Startup disabled.");
            },
            enabled => trayState = enabled);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsNotNull(requestedState);
        Assert.IsFalse(requestedState.Value);
        Assert.IsNotNull(trayState);
        Assert.IsFalse(trayState.Value);
    }

    [TestMethod]
    public void DisableRunAtStartup_PreservesTrayStateWhenStartupServiceFails()
    {
        bool? requestedState = null;
        bool? trayState = true;

        ServiceResult result = DefaultConfigurationStartupReset.DisableRunAtStartup(
            enabled =>
            {
                requestedState = enabled;
                return ServiceResult.Failure("Startup could not be disabled.");
            },
            enabled => trayState = enabled);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(requestedState);
        Assert.IsFalse(requestedState.Value);
        Assert.IsNotNull(trayState);
        Assert.IsTrue(trayState.Value);
    }
}
