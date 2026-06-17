using System.Windows.Threading;

namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class DispatcherInvocationTests
{
    [TestMethod]
    public void TryInvoke_RunsActionOnCurrentDispatcher()
    {
        bool ran = false;

        bool invoked = DispatcherInvocation.TryInvoke(
            Dispatcher.CurrentDispatcher,
            () => ran = true);

        Assert.IsTrue(invoked);
        Assert.IsTrue(ran);
    }

    [TestMethod]
    public void TryBeginInvoke_RunsActionOnActiveDispatcher()
    {
        bool scheduled = false;
        bool ran = false;

        Thread thread = new(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            scheduled = DispatcherInvocation.TryBeginInvoke(
                dispatcher,
                () =>
                {
                    ran = true;
                    dispatcher.InvokeShutdown();
                },
                DispatcherPriority.Send);

            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduled);
        Assert.IsTrue(ran);
    }

    [TestMethod]
    public void TryInvoke_ReturnsFalseWhenDispatcherShutdownStarted()
    {
        bool invoked = true;
        bool ran = false;

        Thread thread = new(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.InvokeShutdown();

            invoked = DispatcherInvocation.TryInvoke(dispatcher, () => ran = true);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsFalse(invoked);
        Assert.IsFalse(ran);
    }
}
