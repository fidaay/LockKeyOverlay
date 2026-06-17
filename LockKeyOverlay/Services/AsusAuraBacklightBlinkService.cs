namespace LockKeyOverlay;

internal sealed class AsusAuraBacklightBlinkService : IDisposable
{
    public static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(500);

    private static readonly RgbColor OffColor = new(0, 0, 0);

    private readonly INumLockStateReader _numLockStateReader;
    private readonly IKeyboardBacklightController _backlightController;
    private readonly IIntervalTimer _blinkTimer;
    private readonly IIntervalTimer _restoreTimer;

    private bool _enabled;
    private bool _pulseActive;
    private bool _disposed;
    private RgbColor _restoreColor = AsusAuraBacklightController.DefaultRestoreColor;

    public AsusAuraBacklightBlinkService(
        INumLockStateReader numLockStateReader,
        IKeyboardBacklightController backlightController,
        IIntervalTimer blinkTimer,
        IIntervalTimer restoreTimer)
    {
        _numLockStateReader = numLockStateReader;
        _backlightController = backlightController;
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
            ServiceResult prepareResult = _backlightController.Prepare();

            if (!prepareResult.Succeeded)
                return prepareResult;

            _restoreColor = _backlightController.GetRestoreColor();
            _enabled = true;

            if (!_blinkTimer.IsEnabled)
                _blinkTimer.Start();

            return ServiceResult.Success("ASUS Aura backlight blink enabled.");
        }

        _enabled = false;
        _blinkTimer.Stop();
        return RestoreAfterPulse();
    }

    public ServiceResult StopAndRestore()
    {
        if (_disposed)
            return ServiceResult.Success("ASUS Aura backlight blink already disposed.");

        _enabled = false;
        _blinkTimer.Stop();
        return RestoreAfterPulse(forceRestore: _pulseActive);
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
            return ServiceResult.Success("ASUS Aura backlight blink is disabled.");

        if (_pulseActive)
            return ServiceResult.Success("ASUS Aura backlight pulse is already active.");

        if (!_numLockStateReader.IsNumLockOn())
            return RestoreAfterPulse();

        ServiceResult result = _backlightController.SetStaticColor(OffColor);

        if (!result.Succeeded)
            return result;

        _pulseActive = true;
        _restoreTimer.Stop();
        _restoreTimer.Start();
        return ServiceResult.Success("ASUS Aura backlight pulse started.");
    }

    internal ServiceResult RestoreAfterPulse(bool forceRestore = false)
    {
        _restoreTimer.Stop();

        if (!_pulseActive && !forceRestore)
            return ServiceResult.Success("No ASUS Aura backlight pulse to restore.");

        _pulseActive = false;
        return _backlightController.SetStaticColor(_restoreColor);
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

internal readonly record struct RgbColor(byte Red, byte Green, byte Blue);

internal interface INumLockStateReader
{
    bool IsNumLockOn();
}

internal sealed class Win32NumLockStateReader : INumLockStateReader
{
    public bool IsNumLockOn()
    {
        return KeyboardHookService.IsNumLockOn();
    }
}

internal interface IKeyboardBacklightController
{
    ServiceResult Prepare();
    RgbColor GetRestoreColor();
    ServiceResult SetStaticColor(RgbColor color);
}
