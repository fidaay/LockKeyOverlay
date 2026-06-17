using Microsoft.Win32;
using System.IO;
using System.Diagnostics;

namespace LockKeyOverlay;

internal sealed class StartupService
{
    private const string StartupValueName = "LockKeyOverlay";

    private readonly Func<string?> _processPathProvider;
    private readonly IStartupRegistry _startupRegistry;
    private readonly IStartupApprovalRegistry _startupApprovalRegistry;

    public StartupService(
        Func<string?>? processPathProvider = null,
        IStartupRegistry? startupRegistry = null,
        IStartupApprovalRegistry? startupApprovalRegistry = null)
    {
        _processPathProvider = processPathProvider ?? (() => Environment.ProcessPath);
        _startupRegistry = startupRegistry ?? new CurrentUserRunStartupRegistry();
        _startupApprovalRegistry = startupApprovalRegistry
            ?? (startupRegistry is null ? new CurrentUserStartupApprovalRegistry() : new UnknownStartupApprovalRegistry());
    }

    public ServiceResult<bool> IsEnabled()
    {
        ServiceResult<StartupRegistrationState> state = GetRegistrationState();

        return state.Succeeded
            ? ServiceResult<bool>.Success(state.Value.IsEnabled, state.Message)
            : ServiceResult<bool>.Failure(false, state.Message, state.Exception, state.NativeErrorCode);
    }

    public ServiceResult<StartupRegistrationState> GetRegistrationState()
    {
        try
        {
            string? value = _startupRegistry.GetValue(StartupValueName);
            bool hasRegistryValue = !string.IsNullOrWhiteSpace(value);

            if (!hasRegistryValue)
            {
                return ServiceResult<StartupRegistrationState>.Success(
                    StartupRegistrationState.NotRegistered,
                    "Startup registry value is not set.");
            }

            string? exePath = _processPathProvider();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return ServiceResult<StartupRegistrationState>.Failure(
                    StartupRegistrationState.NotRegistered,
                    "Current process path is unavailable.");
            }

            bool hasParsedExecutablePath = StartupCommandLine.TryParseExecutablePath(value, out string startupExecutablePath);
            bool matchesCurrentProcess = hasParsedExecutablePath &&
                StartupCommandLine.PathsEqual(startupExecutablePath, exePath);
            StartupApprovalState approvalState = _startupApprovalRegistry.GetState(StartupValueName);
            bool enabled = matchesCurrentProcess && approvalState != StartupApprovalState.Disabled;

            StartupRegistrationState state = new(
                enabled,
                hasRegistryValue,
                matchesCurrentProcess,
                approvalState,
                hasParsedExecutablePath ? startupExecutablePath : null);

            return ServiceResult<StartupRegistrationState>.Success(state, "Startup registry state read.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ServiceResult<StartupRegistrationState>.Failure(
                StartupRegistrationState.NotRegistered,
                "Startup registry state could not be read.",
                ex);
        }
    }

    public ServiceResult SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                string? exePath = _processPathProvider();

                if (string.IsNullOrWhiteSpace(exePath))
                    return ServiceResult.Failure("Current process path is unavailable.");

                string? previousValue = _startupRegistry.GetValue(StartupValueName);
                bool hadPreviousValue = !string.IsNullOrWhiteSpace(previousValue);

                _startupRegistry.SetValue(StartupValueName, StartupCommandLine.Build(exePath));
                try
                {
                    _startupApprovalRegistry.ClearValue(StartupValueName);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                {
                    RestoreStartupValueAfterFailedEnable(previousValue, hadPreviousValue);
                    return ServiceResult.Failure("Startup registry state could not be updated.", ex);
                }
            }
            else
            {
                _startupRegistry.DeleteValue(StartupValueName);
            }

            return ServiceResult.Success("Startup registry state updated.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ServiceResult.Failure("Startup registry state could not be updated.", ex);
        }
    }

    private void RestoreStartupValueAfterFailedEnable(string? previousValue, bool hadPreviousValue)
    {
        try
        {
            if (hadPreviousValue && previousValue is not null)
                _startupRegistry.SetValue(StartupValueName, previousValue);
            else
                _startupRegistry.DeleteValue(StartupValueName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Debug.WriteLine($"Startup registry rollback failed: {ex}");
        }
    }

}

internal readonly record struct StartupRegistrationState(
    bool IsEnabled,
    bool HasRegistryValue,
    bool RegistryPathMatchesCurrentProcess,
    StartupApprovalState ApprovalState,
    string? RegisteredExecutablePath)
{
    public static StartupRegistrationState NotRegistered { get; } = new(
        IsEnabled: false,
        HasRegistryValue: false,
        RegistryPathMatchesCurrentProcess: false,
        StartupApprovalState.Unknown,
        RegisteredExecutablePath: null);

    public bool IsBlockedByWindowsStartupSettings => ApprovalState == StartupApprovalState.Disabled;
}

internal enum StartupApprovalState
{
    Unknown,
    Enabled,
    Disabled
}

internal interface IStartupRegistry
{
    string? GetValue(string valueName);
    void SetValue(string valueName, string value);
    void DeleteValue(string valueName);
}

internal interface IStartupApprovalRegistry
{
    StartupApprovalState GetState(string valueName);
    void ClearValue(string valueName);
}

internal sealed class CurrentUserRunStartupRegistry : IStartupRegistry
{
    private const string StartupRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRunKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string valueName, string value)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupRunKeyPath);

        if (key is null)
            throw new IOException("Startup registry key could not be opened.");

        key.SetValue(valueName, value);
    }

    public void DeleteValue(string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupRunKeyPath);

        if (key is null)
            throw new IOException("Startup registry key could not be opened.");

        key.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

internal sealed class CurrentUserStartupApprovalRegistry : IStartupApprovalRegistry
{
    private const string StartupApprovedRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const byte EnabledValue = 0x02;
    private const byte DisabledValue = 0x03;

    public StartupApprovalState GetState(string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKeyPath, writable: false);
        byte[]? value = key?.GetValue(valueName) as byte[];

        if (value is not { Length: > 0 })
            return StartupApprovalState.Unknown;

        return value[0] switch
        {
            EnabledValue => StartupApprovalState.Enabled,
            DisabledValue => StartupApprovalState.Disabled,
            _ => StartupApprovalState.Unknown
        };
    }

    public void ClearValue(string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

internal sealed class UnknownStartupApprovalRegistry : IStartupApprovalRegistry
{
    public StartupApprovalState GetState(string valueName)
    {
        return StartupApprovalState.Unknown;
    }

    public void ClearValue(string valueName)
    {
    }
}
