using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed class JsonConfigurationService
{
    private readonly string _path;
    public string ConfigurationPath => _path;

    public JsonConfigurationService(string? path = null)
    {
        _path = path ?? ResolveDefaultPath();
    }

    private static string ResolveDefaultPath()
    {
        var currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var appBaseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(appBaseDirectoryPath))
        {
            return appBaseDirectoryPath;
        }

        // If no config exists yet, create it in the current working directory.
        return currentDirectoryPath;
    }

    public ApplicationSettings GetSettings()
    {
        if (!File.Exists(_path))
        {
            return new ApplicationSettings();
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ApplicationSettings();
        }

        var options = JsonSerializer.Deserialize<ApplicationSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return options ?? new ApplicationSettings();
    }

    public void SaveSettings(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_path, json);
    }
}
