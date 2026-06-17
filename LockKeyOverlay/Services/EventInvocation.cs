using System.Diagnostics;

namespace LockKeyOverlay;

internal static class EventInvocation
{
    public static void Raise(EventHandler? handler, object sender, EventArgs args)
    {
        if (handler is null)
            return;

        foreach (EventHandler subscriber in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(sender, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Event handler failed: {ex}");
            }
        }
    }

    public static void Raise<TEventArgs>(EventHandler<TEventArgs>? handler, object sender, TEventArgs args)
        where TEventArgs : EventArgs
    {
        if (handler is null)
            return;

        foreach (EventHandler<TEventArgs> subscriber in handler.GetInvocationList().Cast<EventHandler<TEventArgs>>())
        {
            try
            {
                subscriber(sender, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Event handler failed: {ex}");
            }
        }
    }
}
