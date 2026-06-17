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
}
