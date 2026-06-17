using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace LockKeyOverlay;

internal sealed class PhysicalNumLockBlinkService : IDisposable
{
    public static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(500);

    private readonly INumLockHardware _hardware;
    private readonly IIntervalTimer _blinkTimer;
    private readonly IIntervalTimer _restoreTimer;

    private bool _enabled;
    private bool _pulseActive;
    private bool _disposed;

    public PhysicalNumLockBlinkService(INumLockHardware hardware, IIntervalTimer blinkTimer, IIntervalTimer restoreTimer)
    {
        _hardware = hardware;
        _blinkTimer = blinkTimer;
        _restoreTimer = restoreTimer;

        _blinkTimer.Interval = BlinkInterval;
        _restoreTimer.Interval = PulseDuration;

        _blinkTimer.Tick += BlinkTimer_Tick;
        _restoreTimer.Tick += RestoreTimer_Tick;
    }

    public bool Enabled => _enabled;

    public ServiceResult SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (enabled)
        {
            _enabled = true;

            if (!_blinkTimer.IsEnabled)
                _blinkTimer.Start();

            return ServiceResult.Success("Physical Num Lock blink enabled.");
        }

        _enabled = false;
        _blinkTimer.Stop();
        return RestoreAfterPulse();
    }

    public ServiceResult StopAndRestore()
    {
        if (_disposed)
            return ServiceResult.Success("Physical Num Lock blink already disposed.");

        _enabled = false;
        _blinkTimer.Stop();
        return RestoreAfterPulse();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAndRestore();

        _blinkTimer.Tick -= BlinkTimer_Tick;
        _restoreTimer.Tick -= RestoreTimer_Tick;
        _blinkTimer.Dispose();
        _restoreTimer.Dispose();

        _disposed = true;
    }

    internal ServiceResult TryStartPulse()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_enabled)
            return ServiceResult.Success("Physical Num Lock blink is disabled.");

        if (_pulseActive)
            return ServiceResult.Success("Physical Num Lock pulse is already active.");

        if (!_hardware.IsNumLockOn())
            return ServiceResult.Success("Num Lock is off; physical blink skipped.");

        ServiceResult toggleResult = _hardware.ToggleNumLock();

        if (!toggleResult.Succeeded)
            return toggleResult;

        _pulseActive = true;
        _restoreTimer.Stop();
        _restoreTimer.Start();
        return ServiceResult.Success("Physical Num Lock pulse started.");
    }

    internal ServiceResult RestoreAfterPulse()
    {
        _restoreTimer.Stop();

        if (!_pulseActive)
            return ServiceResult.Success("No physical Num Lock pulse to restore.");

        _pulseActive = false;

        if (_hardware.IsNumLockOn())
            return ServiceResult.Success("Num Lock was already restored.");

        return _hardware.ToggleNumLock();
    }

    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        TryStartPulse();
    }

    private void RestoreTimer_Tick(object? sender, EventArgs e)
    {
        RestoreAfterPulse();
    }
}

internal interface INumLockHardware
{
    bool IsNumLockOn();
    ServiceResult ToggleNumLock();
}

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

internal sealed class Win32NumLockHardware : INumLockHardware
{
    private const int INPUT_KEYBOARD = 1;
    private const ushort VK_NUMLOCK = 0x90;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public bool IsNumLockOn()
    {
        return (GetKeyState(VK_NUMLOCK) & 1) != 0;
    }

    public ServiceResult ToggleNumLock()
    {
        Input[] inputs =
        [
            CreateKeyboardInput(keyUp: false),
            CreateKeyboardInput(keyUp: true)
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());

        return sent == inputs.Length
            ? ServiceResult.Success("Num Lock key input sent.")
            : ServiceResult.Failure("Num Lock key input could not be sent.", nativeErrorCode: Marshal.GetLastWin32Error());
    }

    private static Input CreateKeyboardInput(bool keyUp)
    {
        return new Input
        {
            Type = INPUT_KEYBOARD,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = VK_NUMLOCK,
                    Flags = KEYEVENTF_EXTENDEDKEY | (keyUp ? KEYEVENTF_KEYUP : 0)
                }
            }
        };
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
