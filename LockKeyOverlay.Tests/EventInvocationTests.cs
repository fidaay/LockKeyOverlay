namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class EventInvocationTests
{
    [TestMethod]
    public void Raise_ContinuesAfterFailingSubscriber()
    {
        int calls = 0;
        EventHandler? handler = null;
        handler += (_, _) =>
        {
            calls++;
            throw new InvalidOperationException("Subscriber failed.");
        };
        handler += (_, _) => calls++;

        EventInvocation.Raise(handler, this, EventArgs.Empty);

        Assert.AreEqual(2, calls);
    }
}
