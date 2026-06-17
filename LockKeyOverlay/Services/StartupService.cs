using Microsoft.Win32;
using System.IO;

namespace LockKeyOverlay;

internal sealed class StartupService
{
    private const string StartupValueName = "LockKeyOverlay";

    private readonly Func<string?> _processPathProvider;
    private readonly IStartupRegistry _startupRegistry;

    public StartupService(Func<string?>? processPathProvider = null, IStartupRegistry? startupRegistry = null)
    {
        _processPathProvider = processPathProvider ?? (() => Environment.ProcessPath);
        _startupRegistry = startupRegistry ?? new CurrentUserRunStartupRegistry();
    }

    public ServiceResult<bool> IsEnabled()
    {
        try
        {
            string? value = _startupRegistry.GetValue(StartupValueName);

            if (string.IsNullOrWhiteSpace(value))
                return ServiceResult<bool>.Success(false, "Startup registry value is not set.");

            string? exePath = _processPathProvider();
            if (string.IsNullOrWhiteSpace(exePath))
                return ServiceResult<bool>.Failure(false, "Current process path is unavailable.");

            string expected = Quote(exePath);
            bool enabled = string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

            return ServiceResult<bool>.Success(enabled, "Startup registry state read.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ServiceResult<bool>.Failure(false, "Startup registry state could not be read.", ex);
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

                _startupRegistry.SetValue(StartupValueName, Quote(exePath));
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

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}

internal interface IStartupRegistry
{
    string? GetValue(string valueName);
    void SetValue(string valueName, string value);
    void DeleteValue(string valueName);
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
