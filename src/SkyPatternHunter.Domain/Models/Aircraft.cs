namespace SkyPatternHunter.Domain.Models;

public class Aircraft
{
    public Aircraft(string hex, string? flight, double latitude, double longitude, int altitude, int track, int speed, int? squawk)
    {
        Hex = hex;
        Flight = flight;
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        Track = track;
        Speed = speed;
        Squawk = squawk;
    }

    public string Hex { get; }
    public string? Flight { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public int Altitude { get; }
    public int Track { get; }
    public int Speed { get; }
    public int? Squawk { get; }

    public bool IsOverhead(double userLatitude, double userLongitude, double thresholdMiles)
    {
        var latDistance = Math.Abs(Latitude - userLatitude);
        var lonDistance = Math.Abs(Longitude - userLongitude);
        var distance = Math.Sqrt((latDistance * latDistance) + (lonDistance * lonDistance));
        return distance <= thresholdMiles;
    }
}
