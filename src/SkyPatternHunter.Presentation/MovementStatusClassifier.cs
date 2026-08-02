using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Presentation;

public sealed record MovementStatus(string Direction, string Climb);

public static class MovementStatusClassifier
{
    public static MovementStatus Classify(OverheadEvent currentEvent, OverheadEvent? previousEvent, double userLatitude, double userLongitude)
    {
        ArgumentNullException.ThrowIfNull(currentEvent);

        if (previousEvent is null || currentEvent.Aircraft.Speed <= 0)
        {
            return new MovementStatus("Unknown", "Unknown");
        }

        var previousDistance = DistanceToObserver(previousEvent.Aircraft, userLatitude, userLongitude);
        var currentDistance = DistanceToObserver(currentEvent.Aircraft, userLatitude, userLongitude);
        var altitudeDelta = currentEvent.Aircraft.Altitude - previousEvent.Aircraft.Altitude;

        var direction = currentDistance < previousDistance
            ? "Coming"
            : currentDistance > previousDistance
                ? "Going"
                : "Unknown";
        var climb = altitudeDelta > 0
            ? "Ascending"
            : altitudeDelta < 0
                ? "Descending"
                : "Level";

        return new MovementStatus(direction, climb);
    }

    private static double DistanceToObserver(Aircraft aircraft, double userLatitude, double userLongitude)
    {
        var latDistance = aircraft.Latitude - userLatitude;
        var lonDistance = aircraft.Longitude - userLongitude;
        return Math.Sqrt((latDistance * latDistance) + (lonDistance * lonDistance));
    }
}
