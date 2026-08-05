namespace SkyPatternHunter.Infrastructure.Configuration;

public sealed class ApplicationSettings
{
    public string LogFilePath { get; set; } = "logs/sky-pattern-hunter.log";
    public string LogLevel { get; set; } = "Information";
    public string? DataDirectory { get; set; }
    public double UserLatitude { get; set; }
    public double UserLongitude { get; set; }
    public double DetectionThresholdMiles { get; set; } = 10;
    public string ReadsbHost { get; set; } = "127.0.0.1";
    public int ReadsbPort { get; set; } = 30001;
    public string? ReadsbJsonUrl { get; set; }
    public int DashboardStaleAfterSeconds { get; set; } = 60;
    public int HistoryDays { get; set; } = 30;
    public int FlightHistorySampleSeconds { get; set; } = 30;
    public string AircraftDatabaseSourceUrl { get; set; } = "https://raw.githubusercontent.com/wiedehopf/tar1090-db/csv/aircraft.csv.gz";
    public string? AircraftDataDirectory { get; set; }
}
