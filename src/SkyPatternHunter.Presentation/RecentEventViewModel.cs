using System.Globalization;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.Presentation;

public sealed record RecentEventViewModel(string AircraftHex, string? Flight, string AircraftDescription, string ObservedAtText, string UtcOffsetText, string StatusText, string AltitudeText, string AltitudeTrendText, string DirectionText, string DistanceText, string SpeedText, string MphText, string LookDirectionText)
{
    public static RecentEventViewModel FromEvent(
        OverheadEvent overheadEvent,
        bool isStale,
        string? altitudeTrendText = null,
        string? directionText = null,
        string? lookDirectionText = null,
        string? distanceText = null,
        AircraftDetails? aircraftDetails = null)
    {
        var localObservedAt = overheadEvent.ObservedAt.ToLocalTime();
        var speedText = overheadEvent.Aircraft.Speed > 0 ? $"{overheadEvent.Aircraft.Speed} kt" : "Unknown";
        var mphText = overheadEvent.Aircraft.Speed > 0
            ? $"{overheadEvent.Aircraft.Speed * 1.15078:0} mph"
            : "Unknown";
        var aircraftDescription = FormatAircraftDescription(aircraftDetails);
        var trendText = altitudeTrendText ?? "Level";
        var movementDirectionText = directionText ?? "Coming";
        var lookText = lookDirectionText ?? "Unknown";
        var distance = distanceText ?? "Unknown";

        return new RecentEventViewModel(
            overheadEvent.Aircraft.Hex,
            overheadEvent.Aircraft.Flight,
            aircraftDescription,
            localObservedAt.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.CurrentCulture),
            localObservedAt.ToString("zzz", CultureInfo.InvariantCulture),
            isStale ? "Stale" : "Active",
            $"{overheadEvent.Aircraft.Altitude} ft",
            trendText,
            movementDirectionText,
            distance,
            speedText,
            mphText,
            lookText);
    }

    private static string FormatAircraftDescription(AircraftDetails? aircraftDetails)
    {
        if (aircraftDetails is null)
        {
            return "Unknown";
        }

        var details = new[]
        {
            aircraftDetails.Registration,
            aircraftDetails.TypeCode,
            aircraftDetails.Description,
            aircraftDetails.Year,
            aircraftDetails.RegisteredOwner
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" | ", details.DefaultIfEmpty("Unknown"));
    }
}
