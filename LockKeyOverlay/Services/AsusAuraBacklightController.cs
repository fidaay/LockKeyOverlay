using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace LockKeyOverlay;

internal sealed class AsusAuraBacklightController : IKeyboardBacklightController
{
    public static readonly RgbColor DefaultRestoreColor = new(255, 43, 0);

    private const string TufAuraCorePackageName = "B9ECED6F.TUFAuraCore";
    private const string TufAuraCorePackageSuffix = "__qmba6cd70vzyy";
    private const string AcpiWmiFileName = "ACPIWMI.dll";
    private const string HelperRelativeDirectory = "AsusAura";
    private const string HelperFileName = "LockKeyOverlay.AsusAuraHelper.exe";
    private const int HelperTimeoutMs = 3000;

    private readonly string _helperDirectory;
    private readonly string _helperPath;
    private readonly string _localAcpiWmiPath;

    public AsusAuraBacklightController(string? baseDirectory = null)
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        _helperDirectory = Path.Combine(root, HelperRelativeDirectory);
        _helperPath = Path.Combine(_helperDirectory, HelperFileName);
        _localAcpiWmiPath = Path.Combine(_helperDirectory, AcpiWmiFileName);
    }

    public ServiceResult Prepare()
    {
        try
        {
            if (!File.Exists(_helperPath))
                return ServiceResult.Failure($"ASUS Aura helper was not found: {_helperPath}");

            if (File.Exists(_localAcpiWmiPath))
                return ServiceResult.Success("ASUS Aura ACPIWMI library is ready.");

            string? sourcePath = LocateAcpiWmiLibrary();

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return ServiceResult.Failure("ASUS TUF Aura Core ACPIWMI.dll was not found.");

            Directory.CreateDirectory(_helperDirectory);
            File.Copy(sourcePath, _localAcpiWmiPath, overwrite: true);

            return ServiceResult.Success("ASUS Aura ACPIWMI library prepared.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return ServiceResult.Failure("ASUS Aura ACPIWMI library could not be prepared.", ex);
        }
    }

    public RgbColor GetRestoreColor()
    {
        string customConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ASUS",
            "TUFAuraCore",
            "UWPIni",
            "Custom0.Ini");

        return TryReadFirstColor(customConfigPath, out RgbColor color)
            ? color
            : DefaultRestoreColor;
    }

    public ServiceResult SetStaticColor(RgbColor color)
    {
        ServiceResult prepareResult = Prepare();

        if (!prepareResult.Succeeded)
            return prepareResult;

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _helperPath,
                    Arguments = $"set-static {color.Red} {color.Green} {color.Blue}",
                    WorkingDirectory = _helperDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();

            if (!process.WaitForExit(HelperTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                }

                return ServiceResult.Failure("ASUS Aura helper timed out.");
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();

            if (process.ExitCode != 0)
                return ServiceResult.Failure($"ASUS Aura helper failed. ExitCode={process.ExitCode}. {error} {output}".Trim());

            return ServiceResult.Success("ASUS Aura backlight color applied.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return ServiceResult.Failure("ASUS Aura helper could not be started.", ex);
        }
    }

    private static bool TryReadFirstColor(string path, out RgbColor color)
    {
        color = default;

        if (!File.Exists(path))
            return false;

        byte? red = null;
        byte? green = null;
        byte? blue = null;

        foreach (string line in File.ReadLines(path))
        {
            if (TryReadByteSetting(line, "ColorR", out byte redValue))
                red ??= redValue;
            else if (TryReadByteSetting(line, "ColorG", out byte greenValue))
                green ??= greenValue;
            else if (TryReadByteSetting(line, "ColorB", out byte blueValue))
                blue ??= blueValue;

            if (red.HasValue && green.HasValue && blue.HasValue)
            {
                color = new RgbColor(red.Value, green.Value, blue.Value);
                return true;
            }
        }

        return false;
    }

    private static bool TryReadByteSetting(string line, string key, out byte value)
    {
        value = 0;

        int separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
            return false;

        string currentKey = line[..separatorIndex].Trim();
        if (!string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
            return false;

        return byte.TryParse(line[(separatorIndex + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string? LocateAcpiWmiLibrary()
    {
        string? packagePath = LocateTufAuraCoreWithPowerShell();

        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            string acpiPath = Path.Combine(packagePath, AcpiWmiFileName);
            if (File.Exists(acpiPath))
                return acpiPath;
        }

        return LocateTufAuraCoreWithKnownPattern();
    }

    private static string? LocateTufAuraCoreWithPowerShell()
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name '{TufAuraCorePackageName}' | Select-Object -First 1 -ExpandProperty InstallLocation)\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            if (!process.WaitForExit(HelperTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            if (process.ExitCode != 0)
                return null;

            string path = process.StandardOutput.ReadToEnd().Trim();
            return Directory.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string? LocateTufAuraCoreWithKnownPattern()
    {
        string windowsAppsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");

        try
        {
            return Directory
                .EnumerateDirectories(windowsAppsPath, $"{TufAuraCorePackageName}_*{TufAuraCorePackageSuffix}")
                .Select(path => Path.Combine(path, AcpiWmiFileName))
                .FirstOrDefault(File.Exists);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
