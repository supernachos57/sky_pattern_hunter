using SkyPatternHunter.Presentation;

namespace SkyPatternHunter.SmokeTests.Presentation;

public class StartupConfigurationValidatorTests
{
    [Fact]
    public void ValidateAtStartup_ReturnsFailure_WhenConfigurationIsInvalid()
    {
        var configPath = WriteConfigFile(
            """
            {
              "LogFilePath": "logs/sky-pattern-hunter.log",
              "LogLevel": "Information",
              "DataDirectory": "",
              "UserLatitude": 28.1,
              "UserLongitude": -81.2,
              "DetectionThresholdMiles": 0,
              "ReadsbHost": "",
              "ReadsbPort": 70000
            }
            """);

        try
        {
            var result = StartupConfigurationValidator.ValidateAtStartup(configPath);

            Assert.False(result.IsValid);
            Assert.Equal(1, result.ExitCode);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("ReadsbHost", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ReadsbPort", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DataDirectory", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DetectionThresholdMiles", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteConfigFile(configPath);
        }
    }

    [Fact]
    public void ValidateAtStartup_ReturnsSuccess_WhenConfigurationIsValid()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var escapedDirectory = dataDirectory.Replace("\\", "\\\\", StringComparison.Ordinal);
        var configPath = WriteConfigFile(
            $$"""
            {
              "LogFilePath": "logs/sky-pattern-hunter.log",
              "LogLevel": "Information",
              "DataDirectory": "{{escapedDirectory}}",
              "UserLatitude": 28.1,
              "UserLongitude": -81.2,
              "DetectionThresholdMiles": 10,
              "ReadsbHost": "127.0.0.1",
              "ReadsbPort": 30002
            }
            """);

        try
        {
            var result = StartupConfigurationValidator.ValidateAtStartup(configPath);

            Assert.True(result.IsValid);
            Assert.Equal(0, result.ExitCode);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            TryDeleteConfigFile(configPath);
        }
    }

    private static string WriteConfigFile(string json)
    {
        var configPath = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"), "appsettings.json");
        var directory = Path.GetDirectoryName(configPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(configPath, json);
        return configPath;
    }

    private static void TryDeleteConfigFile(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}