using System.Globalization;
using System.Collections.Concurrent;
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
    private readonly double _userLatitude;
    private readonly double _userLongitude;
    private readonly ConcurrentDictionary<string, LookupState> _detailsLookupByHex = new(StringComparer.OrdinalIgnoreCase);

    public LiveEventDashboard(
        OverheadEventJournal journal,
        TimeSpan? staleAfter = null,
        Func<DateTimeOffset>? clock = null,
        HexDbAircraftClient? aircraftClient = null,
        double userLatitude = 0,
        double userLongitude = 0)
    {
        _journal = journal;
        _aircraftClient = aircraftClient;
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(60);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _userLatitude = userLatitude;
        _userLongitude = userLongitude;
    }

    public LiveEventDashboardSnapshot Refresh()
    {
        return CreateSnapshot(
            _journal.ReadAll(),
            new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase));
    }

    public Task<LiveEventDashboardSnapshot> RefreshAsync()
    {
        var events = _journal.ReadAll();
        if (_aircraftClient is null)
        {
            return Task.FromResult(CreateSnapshot(
                events,
                new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase)));
        }

        var aircraftHexes = events
            .Select(item => item.Aircraft.Hex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var hex in aircraftHexes)
        {
            _detailsLookupByHex.GetOrAdd(hex, StartLookup);
        }

        var detailsByHex = new Dictionary<string, HexDbAircraft?>(StringComparer.OrdinalIgnoreCase);
        foreach (var hex in aircraftHexes)
        {
            if (!_detailsLookupByHex.TryGetValue(hex, out var state))
            {
                continue;
            }

            if (state.IsCompleted)
            {
                detailsByHex[hex] = state.Details;
            }
        }

        return Task.FromResult(CreateSnapshot(events, detailsByHex));

        LookupState StartLookup(string hex)
        {
            _ = LookupAndStoreAsync(hex);
            return LookupState.Pending;
        }
    }

    private async Task LookupAndStoreAsync(string hex)
    {
        if (_aircraftClient is null)
        {
            return;
        }

        try
        {
            var details = await _aircraftClient.GetAircraftAsync(hex);
            _detailsLookupByHex[hex] = LookupState.Completed(details);
        }
        catch
        {
            _detailsLookupByHex[hex] = LookupState.Completed(null);
        }
    }

    private LiveEventDashboardSnapshot CreateSnapshot(
        IReadOnlyList<SkyPatternHunter.Domain.Models.OverheadEvent> events,
        IReadOnlyDictionary<string, HexDbAircraft?> detailsByHex)
    {
        var now = _clock();
        var today = now.ToLocalTime().Date;
        var trendByAircraftHex = BuildAltitudeTrendByAircraft(events);
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
            .Select(item => RecentEventViewModel.FromEvent(item, DetermineStatus(item), isStale: false, GetTrend(item, isStale: false), GetLookDirection(item), GetDetails(item)))
            .ToArray();
        var staleAircraft = todayEvents
            .Where(item => now - item.ObservedAt > _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, DetermineStatus(item), isStale: true, GetTrend(item, isStale: true), GetLookDirection(item), GetDetails(item)))
            .ToArray();
        var latestEvent = todayEvents.MaxBy(item => item.ObservedAt);

        return new LiveEventDashboardSnapshot(
            activeAircraft.Length,
            latestEvent?.Aircraft.Hex ?? "None",
            latestEvent is null
                ? "No events yet"
                : RecentEventViewModel.FromEvent(latestEvent, DetermineStatus(latestEvent), isStale: false, GetTrend(latestEvent, isStale: false), GetLookDirection(latestEvent)).ObservedAtText,
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

        string GetTrend(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent, bool isStale)
        {
            if (isStale)
            {
                return "Unknown";
            }

            return trendByAircraftHex.GetValueOrDefault(overheadEvent.Aircraft.Hex) ?? "Level";
        }

        string GetLookDirection(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent)
        {
            return LookDirectionCalculator.Calculate(
                _userLatitude,
                _userLongitude,
                overheadEvent.Aircraft.Latitude,
                overheadEvent.Aircraft.Longitude);
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

    private static Dictionary<string, string> BuildAltitudeTrendByAircraft(IReadOnlyList<SkyPatternHunter.Domain.Models.OverheadEvent> events)
    {
        return events
            .GroupBy(item => item.Aircraft.Hex, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ordered = group.OrderByDescending(item => item.ObservedAt).Take(2).ToArray();
                    if (ordered.Length < 2)
                    {
                        return "Level";
                    }

                    var latestAltitude = ordered[0].Aircraft.Altitude;
                    var previousAltitude = ordered[1].Aircraft.Altitude;

                    if (latestAltitude > previousAltitude)
                    {
                        return "Ascending";
                    }

                    if (latestAltitude < previousAltitude)
                    {
                        return "Descending";
                    }

                    return "Level";
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record LookupState(bool IsCompleted, HexDbAircraft? Details)
    {
        public static LookupState Pending { get; } = new(false, null);

        public static LookupState Completed(HexDbAircraft? details)
        {
            return new LookupState(true, details);
        }
    }
}
