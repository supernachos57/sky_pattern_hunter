namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed record ApplicationSettingsValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ApplicationSettingsValidationResult Success() => new(true, Array.Empty<string>());

    public static ApplicationSettingsValidationResult Failure(IEnumerable<string> errors)
    {
        return new ApplicationSettingsValidationResult(false, errors.ToArray());
    }
}

public static class ApplicationSettingsValidator
{
    private static readonly HashSet<string> AllowedLogLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trace",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Critical",
        "None"
    };

    public static ApplicationSettingsValidationResult Validate(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.LogFilePath))
        {
            errors.Add("LogFilePath must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(settings.LogLevel))
        {
            errors.Add("LogLevel must not be empty.");
        }
        else if (!AllowedLogLevels.Contains(settings.LogLevel.Trim()))
        {
            errors.Add($"LogLevel '{settings.LogLevel}' is not supported.");
        }

        return errors.Count == 0 ? ApplicationSettingsValidationResult.Success() : ApplicationSettingsValidationResult.Failure(errors);
    }
}
