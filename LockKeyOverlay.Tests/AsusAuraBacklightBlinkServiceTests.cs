namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class AsusAuraBacklightBlinkServiceTests
{
    [TestMethod]
    public void SetEnabled_StartsBlinkTimerAfterPreparingController()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new();
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsTrue(service.Enabled);
        Assert.IsTrue(controller.PrepareCalled);
        Assert.IsTrue(blinkTimer.IsEnabled);
        Assert.AreEqual(AsusAuraBacklightBlinkService.BlinkInterval, blinkTimer.Interval);
        Assert.AreEqual(AsusAuraBacklightBlinkService.PulseDuration, restoreTimer.Interval);
    }

    [TestMethod]
    public void SetEnabled_ReturnsFailureWhenControllerCannotPrepare()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new()
        {
            PrepareResult = ServiceResult.Failure("Missing ASUS Aura")
        };
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(service.Enabled);
        Assert.IsFalse(blinkTimer.IsEnabled);
    }

    [TestMethod]
    public void BlinkTick_DoesNothingWhenNumLockIsOff()
    {
        FakeNumLockStateReader numLock = new(numLockOn: false);
        FakeKeyboardBacklightController controller = new();
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.IsEmpty(controller.AppliedColors);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void BlinkTick_AppliesOffAndRestoreTickAppliesRestoreColor()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new()
        {
            RestoreColor = new RgbColor(255, 43, 0)
        };
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        CollectionAssert.AreEqual(new[] { new RgbColor(0, 0, 0) }, controller.AppliedColors);
        Assert.IsTrue(restoreTimer.IsEnabled);

        restoreTimer.Trigger();

        CollectionAssert.AreEqual(
            new[] { new RgbColor(0, 0, 0), new RgbColor(255, 43, 0) },
            controller.AppliedColors);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void BlinkTick_ReportsFailureWhenPulseCannotStart()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new();
        controller.SetStaticColorResults.Enqueue(ServiceResult.Failure("Pulse failed."));
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);
        List<ServiceResult> issues = [];
        service.IssueReported += (_, e) => issues.Add(e.Result);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();

        Assert.HasCount(1, issues);
        Assert.AreEqual("Pulse failed.", issues[0].Message);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void RestoreTick_FailedRestoreKeepsPulsePendingAndReportsIssue()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new()
        {
            RestoreColor = new RgbColor(10, 20, 30)
        };
        controller.SetStaticColorResults.Enqueue(ServiceResult.Success("Off applied."));
        controller.SetStaticColorResults.Enqueue(ServiceResult.Failure("Restore failed."));
        controller.SetStaticColorResults.Enqueue(ServiceResult.Success("Restore applied."));
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);
        List<ServiceResult> issues = [];
        service.IssueReported += (_, e) => issues.Add(e.Result);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        restoreTimer.Trigger();

        Assert.HasCount(1, issues);
        Assert.AreEqual("Restore failed.", issues[0].Message);
        Assert.IsTrue(restoreTimer.IsEnabled);
        CollectionAssert.AreEqual(
            new[] { new RgbColor(0, 0, 0), new RgbColor(10, 20, 30) },
            controller.AppliedColors);

        restoreTimer.Trigger();

        CollectionAssert.AreEqual(
            new[] { new RgbColor(0, 0, 0), new RgbColor(10, 20, 30), new RgbColor(10, 20, 30) },
            controller.AppliedColors);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void SetEnabledFalse_RestoresWhenDisabledDuringPulse()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new()
        {
            RestoreColor = new RgbColor(10, 20, 30)
        };
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        using AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        ServiceResult result = service.SetEnabled(enabled: false);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsFalse(service.Enabled);
        CollectionAssert.AreEqual(
            new[] { new RgbColor(0, 0, 0), new RgbColor(10, 20, 30) },
            controller.AppliedColors);
        Assert.IsFalse(blinkTimer.IsEnabled);
        Assert.IsFalse(restoreTimer.IsEnabled);
    }

    [TestMethod]
    public void Dispose_RestoresWhenDisposedDuringPulse()
    {
        FakeNumLockStateReader numLock = new(numLockOn: true);
        FakeKeyboardBacklightController controller = new();
        FakeIntervalTimer blinkTimer = new();
        FakeIntervalTimer restoreTimer = new();
        AsusAuraBacklightBlinkService service = new(numLock, controller, blinkTimer, restoreTimer);

        service.SetEnabled(enabled: true);
        blinkTimer.Trigger();
        service.Dispose();

        Assert.HasCount(2, controller.AppliedColors);
        Assert.IsTrue(blinkTimer.Disposed);
        Assert.IsTrue(restoreTimer.Disposed);
    }

    private sealed class FakeNumLockStateReader : INumLockStateReader
    {
        private readonly bool _numLockOn;

        public FakeNumLockStateReader(bool numLockOn)
        {
            _numLockOn = numLockOn;
        }

        public bool IsNumLockOn()
        {
            return _numLockOn;
        }
    }

    private sealed class FakeKeyboardBacklightController : IKeyboardBacklightController
    {
        public ServiceResult PrepareResult { get; init; } = ServiceResult.Success("OK");
        public RgbColor RestoreColor { get; init; } = new(255, 43, 0);
        public bool PrepareCalled { get; private set; }
        public List<RgbColor> AppliedColors { get; } = [];
        public Queue<ServiceResult> SetStaticColorResults { get; } = [];

        public ServiceResult Prepare()
        {
            PrepareCalled = true;
            return PrepareResult;
        }

        public RgbColor GetRestoreColor()
        {
            return RestoreColor;
        }

        public ServiceResult SetStaticColor(RgbColor color)
        {
            AppliedColors.Add(color);
            return SetStaticColorResults.Count > 0
                ? SetStaticColorResults.Dequeue()
                : ServiceResult.Success("Color set.");
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
