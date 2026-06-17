using System.IO;
using System.Text.Json;

namespace LockKeyOverlay;

internal enum ConfigLoadStatus
{
    Loaded,
    NotFound,
    Invalid,
    Failed
}

internal sealed record ConfigLoadResult(
    ConfigLoadStatus Status,
    AppConfig? Config,
    string Message,
    Exception? Exception = null)
{
    public static ConfigLoadResult Loaded(AppConfig config)
    {
        return new ConfigLoadResult(ConfigLoadStatus.Loaded, config, "Configuration loaded.");
    }

    public static ConfigLoadResult NotFound(string path)
    {
        return new ConfigLoadResult(ConfigLoadStatus.NotFound, null, $"Configuration file was not found: {path}");
    }

    public static ConfigLoadResult Invalid(string path, Exception? exception = null)
    {
        return new ConfigLoadResult(ConfigLoadStatus.Invalid, null, $"Configuration file is invalid: {path}", exception);
    }

    public static ConfigLoadResult Failed(string path, Exception exception)
    {
        return new ConfigLoadResult(ConfigLoadStatus.Failed, null, $"Configuration file could not be loaded: {path}", exception);
    }
}

internal sealed class ConfigService
{
    public const string ConfigFolderName = "LockKeyOverlay";
    public const string ConfigFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configDirectoryPath;

    public ConfigService(string? configDirectoryPath = null)
    {
        _configDirectoryPath = configDirectoryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ConfigFolderName);
    }

    public string ConfigFilePath => Path.Combine(_configDirectoryPath, ConfigFileName);

    public ConfigLoadResult Load()
    {
        string path = ConfigFilePath;

        if (!File.Exists(path))
            return ConfigLoadResult.NotFound(path);

        try
        {
            string json = File.ReadAllText(path);
            AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);

            return config is null
                ? ConfigLoadResult.Invalid(path)
                : ConfigLoadResult.Loaded(config);
        }
        catch (JsonException ex)
        {
            return ConfigLoadResult.Invalid(path, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ConfigLoadResult.Failed(path, ex);
        }
    }

    public ServiceResult Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(_configDirectoryPath);

            string json = JsonSerializer.Serialize(config, SerializerOptions);
            File.WriteAllText(ConfigFilePath, json);

            return ServiceResult.Success("Configuration saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ServiceResult.Failure("Configuration could not be saved.", ex);
        }
    }

    public ServiceResult Delete()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
                File.Delete(ConfigFilePath);

            return ServiceResult.Success("Configuration deleted.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ServiceResult.Failure("Configuration could not be deleted.", ex);
        }
    }
}
