using System.Globalization;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Presentation;

public sealed record RecentEventViewModel(string AircraftHex, string? Flight, string ObservedAtText, string AltitudeText, string SpeedText)
{
    public static RecentEventViewModel FromEvent(OverheadEvent overheadEvent)
    {
        return new RecentEventViewModel(
            overheadEvent.Aircraft.Hex,
            overheadEvent.Aircraft.Flight,
            overheadEvent.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
            $"{overheadEvent.Aircraft.Altitude} ft",
            $"{overheadEvent.Aircraft.Speed} kt");
    }
}
