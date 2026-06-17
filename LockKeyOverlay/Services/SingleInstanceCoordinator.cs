namespace LockKeyOverlay;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string DefaultNamePrefix = @"Local\LockKeyOverlay.8B1A9D9D-4C16-4D7E-9A6E-6F9D7A0A2D11";

    private readonly string _mutexName;
    private readonly string _activationEventName;
    private readonly string _shutdownEventName;

    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _shutdownEvent;
    private RegisteredWaitHandle? _activationRegisteredWaitHandle;
    private RegisteredWaitHandle? _shutdownRegisteredWaitHandle;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceCoordinator(string namePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        _mutexName = $"{namePrefix}.Mutex";
        _activationEventName = $"{namePrefix}.Activation";
        _shutdownEventName = $"{namePrefix}.Shutdown";
    }

    public event EventHandler? ActivationRequested;
    public event EventHandler? ShutdownRequested;

    public static SingleInstanceCoordinator CreateDefault()
    {
        return new SingleInstanceCoordinator(DefaultNamePrefix);
    }

    public bool TryClaimPrimary()
    {
        ThrowIfDisposed();

        if (_mutex is not null)
            return _ownsMutex;

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _activationEventName);
        _shutdownEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _shutdownEventName);
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out bool createdNew);

        if (!createdNew)
        {
            _activationEvent.Dispose();
            _activationEvent = null;
            _shutdownEvent.Dispose();
            _shutdownEvent = null;
            return false;
        }

        _ownsMutex = true;
        return _ownsMutex;
    }

    public bool SignalPrimary()
    {
        ThrowIfDisposed();

        return SignalEvent(_activationEventName);
    }

    public bool SignalPrimaryShutdown()
    {
        ThrowIfDisposed();

        return SignalEvent(_shutdownEventName);
    }

    public bool WaitForPrimaryExit(TimeSpan timeout)
    {
        ThrowIfDisposed();

        try
        {
            using Mutex mutex = Mutex.OpenExisting(_mutexName);
            bool acquired = false;

            try
            {
                acquired = mutex.WaitOne(timeout);
                return acquired;
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
                return true;
            }
            finally
            {
                if (acquired)
                    mutex.ReleaseMutex();
            }
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool StartListening()
    {
        ThrowIfDisposed();

        if (!_ownsMutex || _activationEvent is null || _shutdownEvent is null)
            return false;

        if (_activationRegisteredWaitHandle is not null && _shutdownRegisteredWaitHandle is not null)
            return true;

        _activationRegisteredWaitHandle ??= ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            ActivationCallback,
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);

        _shutdownRegisteredWaitHandle ??= ThreadPool.RegisterWaitForSingleObject(
            _shutdownEvent,
            ShutdownCallback,
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _activationRegisteredWaitHandle?.Unregister(null);
        _activationRegisteredWaitHandle = null;

        _shutdownRegisteredWaitHandle?.Unregister(null);
        _shutdownRegisteredWaitHandle = null;

        _activationEvent?.Dispose();
        _activationEvent = null;

        _shutdownEvent?.Dispose();
        _shutdownEvent = null;

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
        _mutex = null;
        _ownsMutex = false;
    }

    private static bool SignalEvent(string eventName)
    {
        try
        {
            using EventWaitHandle targetEvent = EventWaitHandle.OpenExisting(eventName);
            return targetEvent.Set();
        }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ActivationCallback(object? state, bool timedOut)
    {
        if (timedOut || _disposed)
            return;

        EventInvocation.Raise(ActivationRequested, this, EventArgs.Empty);
    }

    private void ShutdownCallback(object? state, bool timedOut)
    {
        if (timedOut || _disposed)
            return;

        EventInvocation.Raise(ShutdownRequested, this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
