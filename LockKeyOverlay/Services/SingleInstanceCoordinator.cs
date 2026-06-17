namespace LockKeyOverlay;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string DefaultNamePrefix = @"Local\LockKeyOverlay.8B1A9D9D-4C16-4D7E-9A6E-6F9D7A0A2D11";

    private readonly string _mutexName;
    private readonly string _activationEventName;

    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _registeredWaitHandle;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceCoordinator(string namePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        _mutexName = $"{namePrefix}.Mutex";
        _activationEventName = $"{namePrefix}.Activation";
    }

    public event EventHandler? ActivationRequested;

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
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out bool createdNew);

        if (!createdNew)
        {
            _activationEvent.Dispose();
            _activationEvent = null;
            return false;
        }

        _ownsMutex = true;
        return _ownsMutex;
    }

    public bool SignalPrimary()
    {
        ThrowIfDisposed();

        try
        {
            using EventWaitHandle activationEvent = EventWaitHandle.OpenExisting(_activationEventName);
            return activationEvent.Set();
        }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool StartListening()
    {
        ThrowIfDisposed();

        if (!_ownsMutex || _activationEvent is null)
            return false;

        if (_registeredWaitHandle is not null)
            return true;

        _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            ActivationCallback,
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

        _registeredWaitHandle?.Unregister(null);
        _registeredWaitHandle = null;

        _activationEvent?.Dispose();
        _activationEvent = null;

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

    private void ActivationCallback(object? state, bool timedOut)
    {
        if (timedOut || _disposed)
            return;

        ActivationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
