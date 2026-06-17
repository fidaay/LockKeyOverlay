namespace LockKeyOverlay;

internal sealed class StartupConfigurationState
{
    private bool? _trustedValue;

    public bool Value { get; private set; }

    public bool HasTrustedValue => _trustedValue.HasValue;

    public bool ApplyRegistryState(
        ServiceResult<StartupRegistrationState> startupStateResult,
        bool fallback)
    {
        if (startupStateResult.Succeeded)
        {
            SetTrusted(startupStateResult.Value.IsEnabled);
            return true;
        }

        Value = _trustedValue ?? fallback;
        return false;
    }

    public void SetTrusted(bool enabled)
    {
        _trustedValue = enabled;
        Value = enabled;
    }
}
