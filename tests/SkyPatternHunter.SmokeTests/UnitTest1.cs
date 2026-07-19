using System.IO;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Logging;

namespace SkyPatternHunter.SmokeTests;

public class UnitTest1
{
    [Fact]
    public void JsonConfigurationService_UsesAppSettingsDefaults()
    {
        var service = new JsonConfigurationService();

        var settings = service.GetSettings();

        Assert.Equal("logs/sky-pattern-hunter.log", settings.LogFilePath);
        Assert.Equal("Information", settings.LogLevel);
    }

    [Fact]
    public void FileLogger_WritesMessagesToConfiguredFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sky-pattern-hunter-tests-{Guid.NewGuid():N}.log");
        var logger = new FileLogger(tempPath);

        logger.Information("hello");
        logger.Error("boom");

        Assert.True(File.Exists(tempPath));
        var contents = File.ReadAllText(tempPath);
        Assert.Contains("hello", contents);
        Assert.Contains("boom", contents);
    }
}