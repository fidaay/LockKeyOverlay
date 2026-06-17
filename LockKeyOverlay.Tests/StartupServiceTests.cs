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
    public void SetEnabled_WritesQuotedProcessPath()
    {
        FakeStartupRegistry registry = new();
        StartupService service = new(() => ProcessPath, registry);

        ServiceResult result = service.SetEnabled(enabled: true);

        Assert.IsTrue(result.Succeeded, result.DiagnosticMessage);
        Assert.AreEqual(Quote(ProcessPath), registry.GetValue(StartupValueName));
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
}
