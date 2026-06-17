using Microsoft.Win32;
using System.IO;

namespace LockKeyOverlay;

internal sealed class StartupService
{
    private const string StartupRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "LockKeyOverlay";

    private readonly Func<string?> _processPathProvider;

    public StartupService(Func<string?>? processPathProvider = null)
    {
        _processPathProvider = processPathProvider ?? (() => Environment.ProcessPath);
    }

    public ServiceResult<bool> IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRunKeyPath, writable: false);
            string? value = key?.GetValue(StartupValueName) as string;

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
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupRunKeyPath);

            if (key is null)
                return ServiceResult.Failure("Startup registry key could not be opened.");

            if (enabled)
            {
                string? exePath = _processPathProvider();

                if (string.IsNullOrWhiteSpace(exePath))
                    return ServiceResult.Failure("Current process path is unavailable.");

                key.SetValue(StartupValueName, Quote(exePath));
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
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
