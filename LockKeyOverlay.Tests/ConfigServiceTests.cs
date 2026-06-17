namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class ConfigServiceTests
{
    private string? _tempDirectory;

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempDirectory is not null && Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [TestMethod]
    public void SaveAndLoad_RoundTripsConfiguration()
    {
        ConfigService service = CreateService();
        AppConfig expected = new()
        {
            Left = 12.5,
            Top = 44.25,
            ScreenDeviceName = @"\\.\DISPLAY1",
            LeftPx = 25,
            TopPx = 88,
            IsVisible = false,
            MovementEnabled = false,
            TopMostEnabled = true,
            RunAtStartupEnabled = true,
            Active = new RgbaConfig { R = 255, G = 140, B = 0, A = 217 },
            Inactive = new RgbaConfig { R = 30, G = 144, B = 255, A = 191 }
        };

        ServiceResult saveResult = service.Save(expected);
        ConfigLoadResult loadResult = service.Load();

        Assert.IsTrue(saveResult.Succeeded, saveResult.DiagnosticMessage);
        Assert.AreEqual(ConfigLoadStatus.Loaded, loadResult.Status);
        Assert.IsNotNull(loadResult.Config);
        Assert.AreEqual(expected.Left, loadResult.Config.Left);
        Assert.AreEqual(expected.Top, loadResult.Config.Top);
        Assert.AreEqual(expected.ScreenDeviceName, loadResult.Config.ScreenDeviceName);
        Assert.AreEqual(expected.LeftPx, loadResult.Config.LeftPx);
        Assert.AreEqual(expected.TopPx, loadResult.Config.TopPx);
        Assert.AreEqual(expected.IsVisible, loadResult.Config.IsVisible);
        Assert.AreEqual(expected.MovementEnabled, loadResult.Config.MovementEnabled);
        Assert.AreEqual(expected.TopMostEnabled, loadResult.Config.TopMostEnabled);
        Assert.AreEqual(expected.RunAtStartupEnabled, loadResult.Config.RunAtStartupEnabled);
        Assert.AreEqual(expected.Active!.ToStyle(), loadResult.Config.Active!.ToStyle());
        Assert.AreEqual(expected.Inactive!.ToStyle(), loadResult.Config.Inactive!.ToStyle());
    }

    [TestMethod]
    public void Load_ReturnsInvalidForMalformedJson()
    {
        ConfigService service = CreateService();

        Directory.CreateDirectory(_tempDirectory!);
        File.WriteAllText(service.ConfigFilePath, "{ not-json");

        ConfigLoadResult result = service.Load();

        Assert.AreEqual(ConfigLoadStatus.Invalid, result.Status);
        Assert.IsNull(result.Config);
        Assert.IsNotNull(result.Exception);
    }

    [TestMethod]
    public void Delete_RemovesConfigurationFile()
    {
        ConfigService service = CreateService();
        ServiceResult saveResult = service.Save(new AppConfig());
        ServiceResult deleteResult = service.Delete();

        Assert.IsTrue(saveResult.Succeeded, saveResult.DiagnosticMessage);
        Assert.IsTrue(deleteResult.Succeeded, deleteResult.DiagnosticMessage);
        Assert.IsFalse(File.Exists(service.ConfigFilePath));
    }

    [TestMethod]
    public void Save_ReplacesMalformedFileAndLeavesNoTemporaryFiles()
    {
        ConfigService service = CreateService();

        Directory.CreateDirectory(_tempDirectory!);
        File.WriteAllText(service.ConfigFilePath, "{ malformed");

        ServiceResult saveResult = service.Save(new AppConfig { Left = 42, Top = 24 });
        ConfigLoadResult loadResult = service.Load();

        Assert.IsTrue(saveResult.Succeeded, saveResult.DiagnosticMessage);
        Assert.AreEqual(ConfigLoadStatus.Loaded, loadResult.Status);
        Assert.IsNotNull(loadResult.Config);
        Assert.AreEqual(42, loadResult.Config.Left);
        Assert.AreEqual(24, loadResult.Config.Top);
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            Directory.GetFiles(_tempDirectory!, $"{ConfigService.ConfigFileName}.*.tmp"));
    }

    private ConfigService CreateService()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "LockKeyOverlay.Tests", Guid.NewGuid().ToString("N"));
        return new ConfigService(_tempDirectory);
    }
}
