using System.Globalization;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.Presentation;

public sealed record RecentEventViewModel(string AircraftHex, string? Flight, string AircraftDescription, string ObservedAtText, string UtcOffsetText, string StatusText, string AltitudeText, string AltitudeTrendText, string DirectionText, string CardinalDirectionText, string SpeedText, string MphText)
{
    public static RecentEventViewModel FromEvent(OverheadEvent overheadEvent, bool isStale, string? altitudeTrendText = null, HexDbAircraft? aircraftDetails = null, string? directionText = null, string? cardinalDirectionText = null)
    {
        var localObservedAt = overheadEvent.ObservedAt.ToLocalTime();
        var speedText = overheadEvent.Aircraft.Speed > 0 ? $"{overheadEvent.Aircraft.Speed} kt" : "Unknown";
        var mphText = overheadEvent.Aircraft.Speed > 0
            ? $"{overheadEvent.Aircraft.Speed * 1.15078:0} mph"
            : "Unknown";
        var aircraftDescription = FormatAircraftDescription(aircraftDetails);
        var trendText = altitudeTrendText ?? "Level";
        var movementDirection = directionText ?? "Unknown";
        var compassDirection = cardinalDirectionText ?? "Unknown";

        return new RecentEventViewModel(
            overheadEvent.Aircraft.Hex,
            overheadEvent.Aircraft.Flight,
            aircraftDescription,
            localObservedAt.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.CurrentCulture),
            localObservedAt.ToString("zzz", CultureInfo.InvariantCulture),
            isStale ? "Stale" : "Active",
            $"{overheadEvent.Aircraft.Altitude} ft",
            trendText,
            movementDirection,
                compassDirection,
            speedText,
            mphText);
    }

    private static string FormatAircraftDescription(HexDbAircraft? aircraftDetails)
    {
        if (aircraftDetails is null)
        {
            return "Unknown";
        }

        var details = new[]
        {
            aircraftDetails.Registration,
            aircraftDetails.Manufacturer,
            aircraftDetails.Type,
            aircraftDetails.RegisteredOwners
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" | ", details.DefaultIfEmpty("Unknown"));
    }
}
