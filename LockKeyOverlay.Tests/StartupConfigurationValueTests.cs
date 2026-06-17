namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupConfigurationValueTests
{
    [TestMethod]
    public void ResolveFromRegistryState_UsesRegistryStateWhenReadSucceeds()
    {
        ServiceResult<StartupRegistrationState> result = ServiceResult<StartupRegistrationState>.Success(
            new StartupRegistrationState(
                IsEnabled: false,
                HasRegistryValue: true,
                RegistryPathMatchesCurrentProcess: true,
                StartupApprovalState.Unknown,
                RegisteredExecutablePath: @"C:\App\LockKeyOverlay.exe"));

        bool value = StartupConfigurationValue.ResolveFromRegistryState(result, fallback: true);

        Assert.IsFalse(value);
    }

    [TestMethod]
    public void ResolveFromRegistryState_PreservesFallbackWhenReadFails()
    {
        ServiceResult<StartupRegistrationState> result = ServiceResult<StartupRegistrationState>.Failure(
            StartupRegistrationState.NotRegistered,
            "Startup registry state could not be read.");

        bool value = StartupConfigurationValue.ResolveFromRegistryState(result, fallback: true);

        Assert.IsTrue(value);
    }
}
