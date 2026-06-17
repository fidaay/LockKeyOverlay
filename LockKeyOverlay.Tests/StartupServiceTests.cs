namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupServiceTests
{
    private const string StartupValueName = "LockKeyOverlay";
    private const string ProcessPath = @"C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe";

    [TestMethod]
    public void IsEnabled_ReturnsTrueWhenRegistryMatchesQuotedProcessPath()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, Quote(ProcessPath));
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void IsEnabled_ReturnsFalseWhenRegistryValueIsMissing()
    {
        StartupService service = new(() => ProcessPath, new FakeStartupRegistry());

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void IsEnabled_ReturnsTrueWhenRegistryHasUnquotedProcessPath()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, ProcessPath);
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void IsEnabled_ReturnsTrueWhenRegistryHasQuotedProcessPathWithArguments()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, $"{Quote(ProcessPath)} --minimized");
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void IsEnabled_ReturnsFalseWhenRegistryPathDoesNotMatch()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, Quote(@"C:\Other\App.exe"));
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void IsEnabled_ReturnsFalseWhenWindowsStartupApprovalIsDisabled()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, Quote(ProcessPath));
        FakeStartupApprovalRegistry approvalRegistry = new()
        {
            State = StartupApprovalState.Disabled
        };
        StartupService service = new(() => ProcessPath, registry, approvalRegistry);

        ServiceResult<bool> result = service.IsEnabled();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void GetRegistrationState_ReturnsRegisteredExecutablePath()
    {
        const string oldPath = @"C:\Old\LockKeyOverlay\LockKeyOverlay.exe";
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, Quote(oldPath));
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult<StartupRegistrationState> result = service.GetRegistrationState();

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsFalse(result.Value.IsEnabled);
        Assert.IsTrue(result.Value.HasRegistryValue);
        Assert.AreEqual(oldPath, result.Value.RegisteredExecutablePath);
    }

    [TestMethod]
    public void SetEnabled_WritesQuotedProcessPath()
    {
        FakeStartupRegistry registry = new();
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.AreEqual(StartupCommandLine.Build(ProcessPath), registry.GetValue(StartupValueName));
    }

    [TestMethod]
    public void SetEnabledTrue_ClearsWindowsStartupApprovalValue()
    {
        FakeStartupRegistry registry = new();
        FakeStartupApprovalRegistry approvalRegistry = new()
        {
            State = StartupApprovalState.Disabled
        };
        StartupService service = new(() => ProcessPath, registry, approvalRegistry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsTrue(approvalRegistry.ClearValueCalled);
    }

    [TestMethod]
    public void SetEnabledTrue_RollsBackNewRegistryValueWhenStartupApprovalClearFails()
    {
        FakeStartupRegistry registry = new();
        FakeStartupApprovalRegistry approvalRegistry = new()
        {
            ThrowOnClearValue = true
        };
        StartupService service = new(() => ProcessPath, registry, approvalRegistry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(registry.GetValue(StartupValueName));
    }

    [TestMethod]
    public void SetEnabledTrue_RestoresPreviousRegistryValueWhenStartupApprovalClearFails()
    {
        const string previousValue = @"""C:\Old\LockKeyOverlay\LockKeyOverlay.exe""";
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, previousValue);
        FakeStartupApprovalRegistry approvalRegistry = new()
        {
            ThrowOnClearValue = true
        };
        StartupService service = new(() => ProcessPath, registry, approvalRegistry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(previousValue, registry.GetValue(StartupValueName));
    }

    [TestMethod]
    public void SetEnabled_ReturnsFailureWithoutProcessPathAndDoesNotWrite()
    {
        FakeStartupRegistry registry = new();
        StartupService service = new(() => null, registry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(registry.GetValue(StartupValueName));
    }

    [TestMethod]
    public void SetEnabledFalse_DeletesRegistryValue()
    {
        FakeStartupRegistry registry = new();
        registry.SetValue(StartupValueName, Quote(ProcessPath));
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult result = service.SetEnabled(enabled: false);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.IsNull(registry.GetValue(StartupValueName));
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        private readonly Dictionary<string, string> _values = [];

        public string? GetValue(string valueName)
        {
            return _values.TryGetValue(valueName, out string? value) ? value : null;
        }

        public void SetValue(string valueName, string value)
        {
            _values[valueName] = value;
        }

        public void DeleteValue(string valueName)
        {
            _values.Remove(valueName);
        }
    }

    private sealed class FakeStartupApprovalRegistry : IStartupApprovalRegistry
    {
        public StartupApprovalState State { get; init; } = StartupApprovalState.Unknown;
        public bool ClearValueCalled { get; private set; }
        public bool ThrowOnClearValue { get; init; }

        public StartupApprovalState GetState(string valueName)
        {
            return State;
        }

        public void ClearValue(string valueName)
        {
            ClearValueCalled = true;

            if (ThrowOnClearValue)
                throw new IOException("Startup approval registry could not be updated.");
        }
    }
}
