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
            LogLevel = "Information"
        };

        var result = ApplicationSettingsValidator.Validate(settings);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsErrors_WhenSettingsAreInvalid()
    {
        var settings = new ApplicationSettings
        {
            LogFilePath = "",
            LogLevel = "Verbose"
        };

        var result = ApplicationSettingsValidator.Validate(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("LogFilePath", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("LogLevel", StringComparison.OrdinalIgnoreCase));
    }
}
