namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class PhysicalNumLockBlinkServiceTests
{
    [TestMethod]
    public void SetEnabled_StartsBlinkTimer()
    {
        FakeNumLockHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsTrue(service.Enabled);
        Assert.IsTrue(blinkTimer.IsEnabled);
        Assert.AreEqual(PhysicalNumLockBlinkService.BlinkInterval, blinkTimer.Interval);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), PhysicalNumLockBlinkService.PulseDuration);
        Assert.AreEqual(PhysicalNumLockBlinkService.PulseDuration, restoreTimer.Interval);
    }

    [TestMethod]
    public void BlinkTick_DoesNothingWhenNumLockIsOff()
    {
        FakeNumLockHardware hardware = new(numLockOn: false);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.IsFalse(hardware.NumLockOn);
        Assert.AreEqual(0, hardware.ToggleCount);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void BlinkTick_PulsesNumLockOffAndRestoreTickRestoresOn()
    {
        FakeNumLockHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.IsFalse(hardware.NumLockOn);
        Assert.AreEqual(1, hardware.ToggleCount);
        Assert.IsTrue(restoreTimer.IsEnabled);

        restoreTimer.Trigger();

        Assert.IsTrue(hardware.NumLockOn);
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void SetEnabledFalse_RestoresOnWhenDisabledDuringPulse()
    {
        FakeNumLockHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        ServiceResult result = service.SetEnabled(enabled: false);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsFalse(service.Enabled);
        Assert.IsTrue(hardware.NumLockOn);
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsFalse(blinkTimer.IsEnabled);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void Dispose_RestoresOnWhenDisposedDuringPulse()
    {
        FakeNumLockHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        service.Dispose();

        Assert.IsTrue(hardware.NumLockOn);
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsTrue(blinkTimer.Disposed);
        Assert.IsTrue(restoreTimer.Disposed);
    }

    [TestMethod]
    public void BlinkTick_DoesNotStartSecondPulseWhilePulseIsActive()
    {
        FakeNumLockHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        blinkTimer.Trigger();

        Assert.IsFalse(hardware.NumLockOn);
        Assert.AreEqual(1, hardware.ToggleCount);
    }

    private sealed class FakeNumLockHardware : INumLockHardware
    {
        public FakeNumLockHardware(bool numLockOn)
        {
            NumLockOn = numLockOn;
        }

        public bool NumLockOn { get; private set; }
        public int ToggleCount { get; private set; }

        public bool IsNumLockOn()
        {
            return NumLockOn;
        }

        public ServiceResult ToggleNumLock()
        {
            ToggleCount++;
            NumLockOn = !NumLockOn;
            return ServiceResult.Success("Fake Num Lock toggled.");
        }
    }

    private sealed class FakeIntervalTimer : IIntervalTimer
    {
        public event EventHandler? Tick;

        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public bool Disposed { get; private set; }

        public void Start()
        {
            IsEnabled = true;
        }

        public void Stop()
        {
            IsEnabled = false;
        }

        public void Trigger()
        {
            if (IsEnabled)
                Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Disposed = true;
            Stop();
        }
    }
}
