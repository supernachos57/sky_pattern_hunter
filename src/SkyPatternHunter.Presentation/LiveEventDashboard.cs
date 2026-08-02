using System.Globalization;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.Presentation;

public sealed record LiveEventDashboardSnapshot(
    int ActiveAircraftCount,
    string LatestAircraftHex,
    string LatestObservedAtText,
    IReadOnlyList<RecentEventViewModel> ActiveEvents,
    IReadOnlyList<RecentEventViewModel> TodayEvents);

public sealed class LiveEventDashboard
{
    private const double McoLatitude = 28.4312;
    private const double McoLongitude = -81.3081;
    private const int AltitudeTrendThresholdFeet = 500;
    private readonly OverheadEventJournal _journal;
    private readonly HexDbAircraftClient? _aircraftClient;
    private readonly TimeSpan _staleAfter;
    private readonly Func<DateTimeOffset> _clock;

    public LiveEventDashboard(OverheadEventJournal journal, TimeSpan? staleAfter = null, Func<DateTimeOffset>? clock = null, HexDbAircraftClient? aircraftClient = null)
    {
        _journal = journal;
        _aircraftClient = aircraftClient;
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(60);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public LiveEventDashboardSnapshot Refresh()
    {
        return CreateSnapshot(_journal.ReadAll(), new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase));
    }

    public async Task<LiveEventDashboardSnapshot> RefreshAsync()
    {
        var events = _journal.ReadAll();
        if (_aircraftClient is null)
        {
            return CreateSnapshot(events, new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase));
        }

        var lookupTasks = events
            .Select(item => item.Aircraft.Hex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(hex => hex, hex => _aircraftClient.GetAircraftAsync(hex), StringComparer.OrdinalIgnoreCase);
        var detailsByHex = new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hex, lookupTask) in lookupTasks)
        {
            detailsByHex[hex] = await lookupTask;
        }

        return CreateSnapshot(events, detailsByHex);
    }

    private LiveEventDashboardSnapshot CreateSnapshot(IReadOnlyList<SkyPatternHunter.Domain.Models.OverheadEvent> events, IReadOnlyDictionary<string, HexDbAircraft?> detailsByHex)
    {
        var now = _clock();
        var today = now.ToLocalTime().Date;
        var latestByAircraft = events
            .GroupBy(item => item.Aircraft.Hex, StringComparer.OrdinalIgnoreCase)
            .Select(MergeLatestEvent)
            .ToArray();
        var todayEvents = latestByAircraft
            .Where(item => item.ObservedAt.ToLocalTime().Date == today)
            .ToArray();
        var activeAircraft = todayEvents
            .Where(item => now - item.ObservedAt <= _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, DetermineStatus(item), isStale: false, GetDetails(item)))
            .ToArray();
        var staleAircraft = todayEvents
            .Where(item => now - item.ObservedAt > _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, DetermineStatus(item), isStale: true, GetDetails(item)))
            .ToArray();
        var latestEvent = todayEvents.MaxBy(item => item.ObservedAt);

        return new LiveEventDashboardSnapshot(
            activeAircraft.Length,
            latestEvent?.Aircraft.Hex ?? "None",
            latestEvent is null
                ? "No events yet"
                : RecentEventViewModel.FromEvent(latestEvent, DetermineStatus(latestEvent), isStale: false).ObservedAtText,
            activeAircraft,
            activeAircraft.Concat(staleAircraft).ToArray());

        HexDbAircraft? GetDetails(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent)
        {
            return detailsByHex.GetValueOrDefault(overheadEvent.Aircraft.Hex);
        }

        string DetermineStatus(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent)
        {
            var aircraftEvents = events
                .Where(item => item.Aircraft.Hex.Equals(overheadEvent.Aircraft.Hex, StringComparison.OrdinalIgnoreCase) && item.ObservedAt <= overheadEvent.ObservedAt)
                .OrderBy(item => item.ObservedAt)
                .ToArray();
            var previousEvent = aircraftEvents
                .LastOrDefault(item => item.ObservedAt < overheadEvent.ObservedAt);

            if (previousEvent is not null)
            {
                var altitudeChange = overheadEvent.Aircraft.Altitude - previousEvent.Aircraft.Altitude;
                if (altitudeChange <= -AltitudeTrendThresholdFeet)
                {
                    return "Descending";
                }

                if (altitudeChange >= AltitudeTrendThresholdFeet)
                {
                    return "Ascending";
                }
            }

            var knownDirection = aircraftEvents
                .Select(item => TryDetermineDirectionStatus(item.Aircraft))
                .FirstOrDefault(status => status is not null);

            return knownDirection ?? "Unknown";
        }
    }

    private static string? TryDetermineDirectionStatus(SkyPatternHunter.Domain.Models.Aircraft aircraft)
    {
        if (aircraft.Track <= 0 || aircraft.Latitude == 0 || aircraft.Longitude == 0)
        {
            return null;
        }

        return IsHeadingTowardMco(aircraft)
            ? "Going to MCO"
            : "Leaving";
    }

    private static bool IsHeadingTowardMco(SkyPatternHunter.Domain.Models.Aircraft aircraft)
    {
        var bearingToMco = CalculateBearingDegrees(aircraft.Latitude, aircraft.Longitude, McoLatitude, McoLongitude);
        var headingDifference = Math.Abs(((aircraft.Track - bearingToMco + 540) % 360) - 180);
        return headingDifference <= 45;
    }

    private static double CalculateBearingDegrees(double startLatitude, double startLongitude, double endLatitude, double endLongitude)
    {
        var startLatitudeRadians = DegreesToRadians(startLatitude);
        var endLatitudeRadians = DegreesToRadians(endLatitude);
        var longitudeDeltaRadians = DegreesToRadians(endLongitude - startLongitude);
        var y = Math.Sin(longitudeDeltaRadians) * Math.Cos(endLatitudeRadians);
        var x = (Math.Cos(startLatitudeRadians) * Math.Sin(endLatitudeRadians))
            - (Math.Sin(startLatitudeRadians) * Math.Cos(endLatitudeRadians) * Math.Cos(longitudeDeltaRadians));
        return (RadiansToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * 180d / Math.PI;
    }

    private static SkyPatternHunter.Domain.Models.OverheadEvent MergeLatestEvent(
        IGrouping<string, SkyPatternHunter.Domain.Models.OverheadEvent> group)
    {
        var orderedEvents = group
            .OrderByDescending(item => item.ObservedAt)
            .ToArray();
        var latestEvent = orderedEvents[0];
        var latestAircraft = latestEvent.Aircraft;
        var mergedAircraft = new SkyPatternHunter.Domain.Models.Aircraft(
            latestAircraft.Hex,
            ResolveValue(latestAircraft.Flight, orderedEvents.Select(item => item.Aircraft.Flight), string.IsNullOrWhiteSpace),
            ResolveValue(latestAircraft.Latitude, orderedEvents.Select(item => item.Aircraft.Latitude), value => value == 0),
            ResolveValue(latestAircraft.Longitude, orderedEvents.Select(item => item.Aircraft.Longitude), value => value == 0),
            ResolveValue(latestAircraft.Altitude, orderedEvents.Select(item => item.Aircraft.Altitude), value => value <= 0),
            ResolveValue(latestAircraft.Track, orderedEvents.Select(item => item.Aircraft.Track), value => value == 0),
            ResolveValue(latestAircraft.Speed, orderedEvents.Select(item => item.Aircraft.Speed), value => value <= 0),
            ResolveNullableValue(latestAircraft.Squawk, orderedEvents.Select(item => item.Aircraft.Squawk)));

        return new SkyPatternHunter.Domain.Models.OverheadEvent(mergedAircraft, latestEvent.ObservedAt);
    }

    private static T ResolveValue<T>(T currentValue, IEnumerable<T> values, Func<T, bool> isMissing)
    {
        if (!isMissing(currentValue))
        {
            return currentValue;
        }

        return values.FirstOrDefault(value => !isMissing(value)) ?? currentValue;
    }

    private static T? ResolveNullableValue<T>(T? currentValue, IEnumerable<T?> values)
        where T : struct
    {
        return currentValue ?? values.FirstOrDefault(value => value.HasValue);
    }
}
