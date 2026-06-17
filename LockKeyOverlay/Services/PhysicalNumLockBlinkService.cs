using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace LockKeyOverlay;

internal sealed class PhysicalNumLockBlinkService : IDisposable
{
    public static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(150);

    private readonly ILockKeyHardware _hardware;
    private readonly IIntervalTimer _blinkTimer;
    private readonly IIntervalTimer _restoreTimer;

    private bool _enabled;
    private bool _pulseActive;
    private bool _disposed;
    private bool _targetStateBeforePulse;

    public PhysicalNumLockBlinkService(ILockKeyHardware hardware, IIntervalTimer blinkTimer, IIntervalTimer restoreTimer)
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
    public PhysicalBlinkTargetKey TargetKey { get; private set; } = PhysicalBlinkTargetKey.CapsLock;

    public ServiceResult SetTargetKey(PhysicalBlinkTargetKey targetKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Enum.IsDefined(targetKey))
            return ServiceResult.Failure($"Physical blink target is not supported: {targetKey}.");

        ServiceResult restoreResult = RestoreAfterPulse();
        if (!restoreResult.Succeeded)
            return restoreResult;

        TargetKey = targetKey;
        return ServiceResult.Success("Physical blink target updated.");
    }

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

        _targetStateBeforePulse = _hardware.IsLockKeyOn(TargetKey);

        ServiceResult toggleResult = _hardware.ToggleLockKey(TargetKey);

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

        if (_hardware.IsLockKeyOn(TargetKey) == _targetStateBeforePulse)
            return ServiceResult.Success("Physical lock key was already restored.");

        return _hardware.ToggleLockKey(TargetKey);
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

internal enum PhysicalBlinkTargetKey
{
    CapsLock = 0,
    NumLock = 1,
    ScrollLock = 2
}

internal interface ILockKeyHardware
{
    bool IsNumLockOn();
    bool IsLockKeyOn(PhysicalBlinkTargetKey targetKey);
    ServiceResult ToggleLockKey(PhysicalBlinkTargetKey targetKey);
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

internal sealed class Win32LockKeyHardware : ILockKeyHardware
{
    private const int INPUT_KEYBOARD = 1;
    private const ushort VK_CAPITAL = 0x14;
    private const ushort VK_NUMLOCK = 0x90;
    private const ushort VK_SCROLL = 0x91;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public bool IsNumLockOn()
    {
        return IsLockKeyOn(PhysicalBlinkTargetKey.NumLock);
    }

    public bool IsLockKeyOn(PhysicalBlinkTargetKey targetKey)
    {
        return (GetKeyState(ToVirtualKey(targetKey)) & 1) != 0;
    }

    public ServiceResult ToggleLockKey(PhysicalBlinkTargetKey targetKey)
    {
        ushort virtualKey = ToVirtualKey(targetKey);
        Input[] inputs =
        [
            CreateKeyboardInput(targetKey, virtualKey, keyUp: false),
            CreateKeyboardInput(targetKey, virtualKey, keyUp: true)
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());

        return sent == inputs.Length
            ? ServiceResult.Success("Physical lock key input sent.")
            : ServiceResult.Failure("Physical lock key input could not be sent.", nativeErrorCode: Marshal.GetLastWin32Error());
    }

    private static ushort ToVirtualKey(PhysicalBlinkTargetKey targetKey)
    {
        return targetKey switch
        {
            PhysicalBlinkTargetKey.CapsLock => VK_CAPITAL,
            PhysicalBlinkTargetKey.NumLock => VK_NUMLOCK,
            PhysicalBlinkTargetKey.ScrollLock => VK_SCROLL,
            _ => throw new ArgumentOutOfRangeException(nameof(targetKey), targetKey, null)
        };
    }

    private static Input CreateKeyboardInput(PhysicalBlinkTargetKey targetKey, ushort virtualKey, bool keyUp)
    {
        return new Input
        {
            Type = INPUT_KEYBOARD,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = GetKeyEventFlags(targetKey, keyUp)
                }
            }
        };
    }

    private static uint GetKeyEventFlags(PhysicalBlinkTargetKey targetKey, bool keyUp)
    {
        uint flags = targetKey == PhysicalBlinkTargetKey.NumLock
            ? KEYEVENTF_EXTENDEDKEY
            : 0;

        if (keyUp)
            flags |= KEYEVENTF_KEYUP;

        return flags;
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
