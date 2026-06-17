namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupRegistrationRepairTests
{
    private const string ExecutableFileName = "LockKeyOverlay.exe";

    [TestMethod]
    public void ShouldRepair_ReturnsTrueWhenConfigEnabledAndRegistryValueMissing()
    {
        AppConfig config = new()
        {
            RunAtStartupEnabled = true
        };

        bool shouldRepair = StartupRegistrationRepair.ShouldRepair(
            config,
            StartupRegistrationState.NotRegistered,
            ExecutableFileName);

        Assert.IsTrue(shouldRepair);
    }

    [TestMethod]
    public void ShouldRepair_ReturnsTrueWhenConfigEnabledAndRegistryPointsToOldAppPath()
    {
        AppConfig config = new()
        {
            RunAtStartupEnabled = true
        };
        StartupRegistrationState startupState = new(
            IsEnabled: false,
            HasRegistryValue: true,
            RegistryPathMatchesCurrentProcess: false,
            StartupApprovalState.Unknown,
            RegisteredExecutablePath: @"C:\Old\LockKeyOverlay\LockKeyOverlay.exe");

        bool shouldRepair = StartupRegistrationRepair.ShouldRepair(config, startupState, ExecutableFileName);

        Assert.IsTrue(shouldRepair);
    }

    [TestMethod]
    public void ShouldRepair_ReturnsFalseWhenWindowsStartupSettingsDisabledTheApp()
    {
        AppConfig config = new()
        {
            RunAtStartupEnabled = true
        };
        StartupRegistrationState startupState = new(
            IsEnabled: false,
            HasRegistryValue: true,
            RegistryPathMatchesCurrentProcess: true,
            StartupApprovalState.Disabled,
            RegisteredExecutablePath: @"C:\Programs\LockKeyOverlay\LockKeyOverlay.exe");

        bool shouldRepair = StartupRegistrationRepair.ShouldRepair(config, startupState, ExecutableFileName);

        Assert.IsFalse(shouldRepair);
    }

    [TestMethod]
    public void ShouldRepair_ReturnsFalseWhenConfigDisabled()
    {
        AppConfig config = new()
        {
            RunAtStartupEnabled = false
        };

        bool shouldRepair = StartupRegistrationRepair.ShouldRepair(
            config,
            StartupRegistrationState.NotRegistered,
            ExecutableFileName);

        Assert.IsFalse(shouldRepair);
    }

    [TestMethod]
    public void ShouldRepair_ReturnsFalseWhenRegistryPointsToDifferentExecutable()
    {
        AppConfig config = new()
        {
            RunAtStartupEnabled = true
        };
        StartupRegistrationState startupState = new(
            IsEnabled: false,
            HasRegistryValue: true,
            RegistryPathMatchesCurrentProcess: false,
            StartupApprovalState.Unknown,
            RegisteredExecutablePath: @"C:\Other\App.exe");

        bool shouldRepair = StartupRegistrationRepair.ShouldRepair(config, startupState, ExecutableFileName);

        Assert.IsFalse(shouldRepair);
    }
}
