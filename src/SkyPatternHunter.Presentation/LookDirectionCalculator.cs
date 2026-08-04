namespace SkyPatternHunter.Presentation;

public static class LookDirectionCalculator
{
    public static string Calculate(double userLatitude, double userLongitude, double aircraftLatitude, double aircraftLongitude)
    {
        if (!IsFinite(userLatitude) || !IsFinite(userLongitude) || !IsFinite(aircraftLatitude) || !IsFinite(aircraftLongitude))
        {
            return "Unknown";
        }

        var latDelta = aircraftLatitude - userLatitude;
        var lonDelta = aircraftLongitude - userLongitude;
        if (Math.Abs(latDelta) < 1e-9 && Math.Abs(lonDelta) < 1e-9)
        {
            return "Here";
        }

        var bearing = CalculateInitialBearing(userLatitude, userLongitude, aircraftLatitude, aircraftLongitude);
        var directions = new[] { "North", "North-East", "East", "South-East", "South", "South-West", "West", "North-West" };
        var index = (int)Math.Floor((bearing + 22.5) / 45) % directions.Length;
        return directions[index];
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static double CalculateInitialBearing(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        var lat1 = DegreesToRadians(fromLatitude);
        var lat2 = DegreesToRadians(toLatitude);
        var dLon = DegreesToRadians(toLongitude - fromLongitude);

        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        var bearing = RadiansToDegrees(Math.Atan2(y, x));
        return (bearing + 360) % 360;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * 180 / Math.PI;
    }
}
