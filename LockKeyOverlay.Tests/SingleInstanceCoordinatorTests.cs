namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class SingleInstanceCoordinatorTests
{
    [TestMethod]
    public void TryClaimPrimary_ReturnsFalseForSecondCoordinator()
    {
        string namePrefix = CreateNamePrefix();

        using SingleInstanceCoordinator primary = new(namePrefix);
        using SingleInstanceCoordinator secondary = new(namePrefix);

        Assert.IsTrue(primary.TryClaimPrimary());
        Assert.IsFalse(secondary.TryClaimPrimary());
    }

    [TestMethod]
    public void SignalPrimary_RaisesActivationRequestedOnPrimaryCoordinator()
    {
        string namePrefix = CreateNamePrefix();

        using SingleInstanceCoordinator primary = new(namePrefix);
        using SingleInstanceCoordinator secondary = new(namePrefix);
        using ManualResetEventSlim activationReceived = new();

        primary.ActivationRequested += (_, _) => activationReceived.Set();

        Assert.IsTrue(primary.TryClaimPrimary());
        Assert.IsTrue(primary.StartListening());
        Assert.IsFalse(secondary.TryClaimPrimary());
        Assert.IsTrue(secondary.SignalPrimary());
        Assert.IsTrue(activationReceived.Wait(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void SignalPrimaryShutdown_RaisesShutdownRequestedOnly()
    {
        string namePrefix = CreateNamePrefix();

        using SingleInstanceCoordinator primary = new(namePrefix);
        using SingleInstanceCoordinator secondary = new(namePrefix);
        using ManualResetEventSlim activationReceived = new();
        using ManualResetEventSlim shutdownReceived = new();

        primary.ActivationRequested += (_, _) => activationReceived.Set();
        primary.ShutdownRequested += (_, _) => shutdownReceived.Set();

        Assert.IsTrue(primary.TryClaimPrimary());
        Assert.IsTrue(primary.StartListening());
        Assert.IsFalse(secondary.TryClaimPrimary());
        Assert.IsTrue(secondary.SignalPrimaryShutdown());
        Assert.IsTrue(shutdownReceived.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsFalse(activationReceived.Wait(TimeSpan.FromMilliseconds(200)));
    }

    [TestMethod]
    public void SignalPrimary_ReturnsFalseWhenNoPrimaryCoordinatorExists()
    {
        using SingleInstanceCoordinator coordinator = new(CreateNamePrefix());

        Assert.IsFalse(coordinator.SignalPrimary());
    }

    [TestMethod]
    public void SignalPrimaryShutdown_ReturnsFalseWhenNoPrimaryCoordinatorExists()
    {
        using SingleInstanceCoordinator coordinator = new(CreateNamePrefix());

        Assert.IsFalse(coordinator.SignalPrimaryShutdown());
    }

    [TestMethod]
    public void WaitForPrimaryExit_ReturnsTrueWhenNoPrimaryCoordinatorExists()
    {
        using SingleInstanceCoordinator coordinator = new(CreateNamePrefix());

        Assert.IsTrue(coordinator.WaitForPrimaryExit(TimeSpan.FromMilliseconds(10)));
    }

    private static string CreateNamePrefix()
    {
        return $@"Local\LockKeyOverlay.Tests.{Guid.NewGuid():N}";
    }
}
