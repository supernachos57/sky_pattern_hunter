using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed class JsonConfigurationService
{
    private readonly string _path;

    public JsonConfigurationService(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
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
}
