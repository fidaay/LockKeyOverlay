namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class PhysicalNumLockBlinkServiceTests
{
    [TestMethod]
    public void SetEnabled_StartsBlinkTimer()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsTrue(service.Enabled);
        Assert.AreEqual(PhysicalBlinkTargetKey.CapsLock, service.TargetKey);
        Assert.IsTrue(blinkTimer.IsEnabled);
        Assert.AreEqual(PhysicalNumLockBlinkService.BlinkInterval, blinkTimer.Interval);
        Assert.AreEqual(PhysicalNumLockBlinkService.PulseDuration, restoreTimer.Interval);
    }

    [TestMethod]
    public void BlinkTick_DoesNothingWhenNumLockIsOff()
    {
        FakeLockKeyHardware hardware = new(numLockOn: false);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.NumLock));
        Assert.AreEqual(0, hardware.ToggleCount);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void BlinkTick_PulsesDefaultCapsLockTargetAndRestoreTickRestoresOriginalState()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.NumLock));
        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.AreEqual(1, hardware.ToggleCount);
        Assert.IsTrue(restoreTimer.IsEnabled);

        restoreTimer.Trigger();

        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.NumLock));
        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void SetTargetKey_UsesSelectedTarget()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        service.SetTargetKey(PhysicalBlinkTargetKey.ScrollLock);
        blinkTimer.Trigger();

        Assert.AreEqual(PhysicalBlinkTargetKey.ScrollLock, service.TargetKey);
        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.ScrollLock));
        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
    }

    [TestMethod]
    public void SetTargetKey_RestoresOldTargetWhenChangedDuringPulse()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        ServiceResult result = service.SetTargetKey(PhysicalBlinkTargetKey.NumLock);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.NumLock));
        Assert.AreEqual(2, hardware.ToggleCount);
    }

    [TestMethod]
    public void SetEnabledFalse_RestoresTargetWhenDisabledDuringPulse()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        ServiceResult result = service.SetEnabled(enabled: false);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsFalse(service.Enabled);
        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsFalse(blinkTimer.IsEnabled);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void Dispose_RestoresTargetWhenDisposedDuringPulse()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        service.Dispose();

        Assert.IsFalse(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.AreEqual(2, hardware.ToggleCount);
        Assert.IsTrue(blinkTimer.Disposed);
        Assert.IsTrue(restoreTimer.Disposed);
    }

    [TestMethod]
    public void BlinkTick_DoesNotStartSecondPulseWhilePulseIsActive()
    {
        FakeLockKeyHardware hardware = new(numLockOn: true);
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using PhysicalNumLockBlinkService service = new(hardware, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        blinkTimer.Trigger();

        Assert.IsTrue(hardware.IsLockKeyOn(PhysicalBlinkTargetKey.CapsLock));
        Assert.AreEqual(1, hardware.ToggleCount);
    }

    private sealed class FakeLockKeyHardware : ILockKeyHardware
    {
        private readonly Dictionary<PhysicalBlinkTargetKey, bool> _keyStates = new()
        {
            [PhysicalBlinkTargetKey.CapsLock] = false,
            [PhysicalBlinkTargetKey.NumLock] = false,
            [PhysicalBlinkTargetKey.ScrollLock] = false
        };

        public FakeLockKeyHardware(bool numLockOn)
        {
            _keyStates[PhysicalBlinkTargetKey.NumLock] = numLockOn;
        }

        public int ToggleCount { get; private set; }

        public bool IsNumLockOn()
        {
            return IsLockKeyOn(PhysicalBlinkTargetKey.NumLock);
        }

        public bool IsLockKeyOn(PhysicalBlinkTargetKey targetKey)
        {
            return _keyStates[targetKey];
        }

        public ServiceResult ToggleLockKey(PhysicalBlinkTargetKey targetKey)
        {
            ToggleCount++;
            _keyStates[targetKey] = !_keyStates[targetKey];
            return ServiceResult.Success("Fake lock key toggled.");
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
