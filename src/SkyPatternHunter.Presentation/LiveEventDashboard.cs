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
            .Select(group => group.MaxBy(item => item.ObservedAt)!)
            .ToArray();
        var todayEvents = latestByAircraft
            .Where(item => item.ObservedAt.ToLocalTime().Date == today)
            .ToArray();
        var activeAircraft = todayEvents
            .Where(item => now - item.ObservedAt <= _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: false, GetTrend(item, isStale: false), GetLookDirection(item), GetDetails(item)))
            .ToArray();
        var staleAircraft = todayEvents
            .Where(item => now - item.ObservedAt > _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: true, GetTrend(item, isStale: true), GetLookDirection(item), GetDetails(item)))
            .ToArray();
        var latestEvent = todayEvents.MaxBy(item => item.ObservedAt);

        return new LiveEventDashboardSnapshot(
            activeAircraft.Length,
            latestEvent?.Aircraft.Hex ?? "None",
            latestEvent is null
                ? "No events yet"
                : RecentEventViewModel.FromEvent(latestEvent, isStale: false, GetTrend(latestEvent, isStale: false), GetLookDirection(latestEvent)).ObservedAtText,
            activeAircraft,
            activeAircraft.Concat(staleAircraft).ToArray());

        HexDbAircraft? GetDetails(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent)
        {
            return detailsByHex.GetValueOrDefault(overheadEvent.Aircraft.Hex);
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
