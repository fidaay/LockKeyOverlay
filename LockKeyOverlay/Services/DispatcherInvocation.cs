using System.Diagnostics;
using System.Windows.Threading;

namespace LockKeyOverlay;

internal static class DispatcherInvocation
{
    public static bool TryBeginInvoke(Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return false;

        try
        {
            dispatcher.BeginInvoke(action, priority);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            Debug.WriteLine($"Dispatcher begin invoke was skipped: {ex.Message}");
            return false;
        }
    }

    public static bool TryInvoke(Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Send)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return false;

        try
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action, priority);

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            Debug.WriteLine($"Dispatcher invoke was skipped: {ex.Message}");
            return false;
        }
    }
}
