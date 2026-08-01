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

        if (settings.UserLatitude < -90 || settings.UserLatitude > 90)
        {
            errors.Add("UserLatitude must be between -90 and 90 degrees.");
        }

        if (settings.UserLongitude < -180 || settings.UserLongitude > 180)
        {
            errors.Add("UserLongitude must be between -180 and 180 degrees.");
        }

        if (settings.DetectionThresholdMiles <= 0)
        {
            errors.Add("DetectionThresholdMiles must be greater than 0.");
        }

        if (settings.DashboardStaleAfterSeconds <= 0)
        {
            errors.Add("DashboardStaleAfterSeconds must be greater than 0.");
        }

        if (settings.ReadsbPort <= 0 || settings.ReadsbPort > 65535)
        {
            errors.Add("ReadsbPort must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(settings.ReadsbHost))
        {
            errors.Add("ReadsbHost must not be empty.");
        }

        if (!string.IsNullOrWhiteSpace(settings.ReadsbJsonUrl) &&
            (!Uri.TryCreate(settings.ReadsbJsonUrl, UriKind.Absolute, out var readsbJsonUri) ||
             (readsbJsonUri.Scheme != Uri.UriSchemeHttp && readsbJsonUri.Scheme != Uri.UriSchemeHttps)))
        {
            errors.Add("ReadsbJsonUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(settings.DataDirectory))
        {
            errors.Add("DataDirectory must not be empty.");
        }

        return errors.Count == 0 ? ApplicationSettingsValidationResult.Success() : ApplicationSettingsValidationResult.Failure(errors);
    }
}
