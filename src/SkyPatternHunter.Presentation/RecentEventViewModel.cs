using System.Globalization;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Presentation;

public sealed record RecentEventViewModel(string AircraftHex, string? Flight, string ObservedAtText, string UtcOffsetText, string StatusText, string AltitudeText, string SpeedText, string MphText)
{
    public static RecentEventViewModel FromEvent(OverheadEvent overheadEvent, bool isStale)
    {
        var localObservedAt = overheadEvent.ObservedAt.ToLocalTime();
        var speedText = overheadEvent.Aircraft.Speed > 0 ? $"{overheadEvent.Aircraft.Speed} kt" : "Unknown";
        var mphText = overheadEvent.Aircraft.Speed > 0
            ? $"{overheadEvent.Aircraft.Speed * 1.15078:0} mph"
            : "Unknown";

        return new RecentEventViewModel(
            overheadEvent.Aircraft.Hex,
            overheadEvent.Aircraft.Flight,
            localObservedAt.ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.CurrentCulture),
            localObservedAt.ToString("zzz", CultureInfo.InvariantCulture),
            isStale ? "Stale" : "Active",
            $"{overheadEvent.Aircraft.Altitude} ft",
            speedText,
            mphText);
    }
}
