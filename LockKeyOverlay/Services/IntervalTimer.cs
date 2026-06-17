using System.Windows.Threading;

namespace LockKeyOverlay;

internal interface IIntervalTimer : IDisposable
{
    event EventHandler? Tick;
    TimeSpan Interval { get; set; }
    bool IsEnabled { get; }
    void Start();
    void Stop();
}

internal sealed class DispatcherIntervalTimer : IIntervalTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherIntervalTimer(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
    }

    public event EventHandler? Tick
    {
        add => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsEnabled => _timer.IsEnabled;

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
