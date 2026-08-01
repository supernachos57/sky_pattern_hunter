using SkyPatternHunter.Infrastructure.Configuration;

namespace SkyPatternHunter.SmokeTests.Configuration;

public class ApplicationSettingsValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_WhenSettingsAreSupported()
    {
        var settings = new ApplicationSettings
        {
            LogFilePath = "logs/sky-pattern-hunter.log",
            LogLevel = "Information",
            ReadsbHost = "127.0.0.1",
            ReadsbPort = 30002,
            DetectionThresholdMiles = 10,
            DataDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"))
        };

        var result = ApplicationSettingsValidator.Validate(settings);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsErrors_WhenGeographicAndNetworkValuesAreInvalid()
    {
        var settings = new ApplicationSettings
        {
            LogFilePath = "logs/sky-pattern-hunter.log",
            LogLevel = "Information",
            UserLatitude = 100,
            UserLongitude = -181,
            DetectionThresholdMiles = 0,
            DashboardStaleAfterSeconds = 0,
            ReadsbPort = 70000,
            ReadsbJsonUrl = "not a URL"
        };

        var result = ApplicationSettingsValidator.Validate(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("UserLatitude", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("UserLongitude", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("DetectionThresholdMiles", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("DashboardStaleAfterSeconds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ReadsbPort", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ReadsbJsonUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsErrors_WhenSettingsAreInvalid()
    {
        var settings = new ApplicationSettings
        {
            LogFilePath = "",
            LogLevel = "Verbose",
            ReadsbHost = "",
            ReadsbPort = 0,
            DataDirectory = ""
        };

        var result = ApplicationSettingsValidator.Validate(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("LogFilePath", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("LogLevel", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ReadsbHost", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ReadsbPort", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("DataDirectory", StringComparison.OrdinalIgnoreCase));
    }
}
