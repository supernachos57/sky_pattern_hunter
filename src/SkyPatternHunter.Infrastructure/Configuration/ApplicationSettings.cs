namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed class ApplicationSettings
{
    public string LogFilePath { get; set; } = "logs/sky-pattern-hunter.log";
    public string LogLevel { get; set; } = "Information";
}
