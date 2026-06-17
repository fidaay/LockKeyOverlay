namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupConfigurationStateTests
{
    [TestMethod]
    public void ApplyRegistryState_UsesRegistryStateWhenReadSucceeds()
    {
        StartupConfigurationState state = new();

        bool read = state.ApplyRegistryState(Success(enabled: false), fallback: true);

        Assert.IsTrue(read);
        Assert.IsTrue(state.HasTrustedValue);
        Assert.IsFalse(state.Value);
    }

    [TestMethod]
    public void ApplyRegistryState_KeepsTrustedValueWhenLaterReadFails()
    {
        StartupConfigurationState state = new();
        state.ApplyRegistryState(Success(enabled: true), fallback: false);

        bool read = state.ApplyRegistryState(Failure(), fallback: false);

        Assert.IsFalse(read);
        Assert.IsTrue(state.HasTrustedValue);
        Assert.IsTrue(state.Value);
    }

    [TestMethod]
    public void ApplyRegistryState_UsesConfigFallbackWhenReadFailsWithoutTrustedValue()
    {
        StartupConfigurationState state = new();

        bool read = state.ApplyRegistryState(Failure(), fallback: true);

        Assert.IsFalse(read);
        Assert.IsFalse(state.HasTrustedValue);
        Assert.IsTrue(state.Value);
    }

    [TestMethod]
    public void SetTrusted_UpdatesValueAfterTrayWrite()
    {
        StartupConfigurationState state = new();

        state.SetTrusted(true);

        Assert.IsTrue(state.HasTrustedValue);
        Assert.IsTrue(state.Value);
    }

    [TestMethod]
    public void SetTrusted_MarksResetValueAsTrusted()
    {
        StartupConfigurationState state = new();
        state.SetTrusted(true);

        state.SetTrusted(false);

        Assert.IsTrue(state.HasTrustedValue);
        Assert.IsFalse(state.Value);
    }

    private static ServiceResult<StartupRegistrationState> Success(bool enabled)
    {
        return ServiceResult<StartupRegistrationState>.Success(
            new StartupRegistrationState(
                IsEnabled: enabled,
                HasRegistryValue: enabled,
                RegistryPathMatchesCurrentProcess: enabled,
                ApprovalState: StartupApprovalState.Unknown,
                RegisteredExecutablePath: enabled ? @"C:\App\LockKeyOverlay.exe" : null));
    }

    private static ServiceResult<StartupRegistrationState> Failure()
    {
        return ServiceResult<StartupRegistrationState>.Failure(
            StartupRegistrationState.NotRegistered,
            "Startup registry state could not be read.");
    }
}
