namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed class ApplicationSettings
{
    public string LogFilePath { get; set; } = "logs/sky-pattern-hunter.log";
    public string LogLevel { get; set; } = "Information";
    public string? DataDirectory { get; set; }
    public double UserLatitude { get; set; }
    public double UserLongitude { get; set; }
    public double DetectionThresholdMiles { get; set; } = 10;
    public string ReadsbIngestionMode { get; set; } = "tcp";
    public string ReadsbHost { get; set; } = "127.0.0.1";
    public int ReadsbPort { get; set; } = 30002;
    public string ReadsbJsonPath { get; set; } = "/data/aircraft.json";
    public int ReadsbJsonPollIntervalSeconds { get; set; } = 1;
    public bool DiscordNotificationsEnabled { get; set; }
    public string? DiscordRecipientUserId { get; set; }
    public string? DiscordBotToken { get; set; }
    public int DiscordNotificationCooldownSeconds { get; set; } = 300;
    public string DiscordNotificationMessagePrefix { get; set; } = "Overhead alert";
}
