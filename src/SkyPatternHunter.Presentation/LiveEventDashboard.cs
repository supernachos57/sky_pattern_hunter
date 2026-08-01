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
            .Select(group => group.MaxBy(item => item.ObservedAt)!)
            .ToArray();
        var todayEvents = latestByAircraft
            .Where(item => item.ObservedAt.ToLocalTime().Date == today)
            .ToArray();
        var activeAircraft = todayEvents
            .Where(item => now - item.ObservedAt <= _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: false, GetDetails(item)))
            .ToArray();
        var staleAircraft = todayEvents
            .Where(item => now - item.ObservedAt > _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: true, GetDetails(item)))
            .ToArray();
        var latestEvent = todayEvents.MaxBy(item => item.ObservedAt);

        return new LiveEventDashboardSnapshot(
            activeAircraft.Length,
            latestEvent?.Aircraft.Hex ?? "None",
            latestEvent is null
                ? "No events yet"
                : RecentEventViewModel.FromEvent(latestEvent, isStale: false).ObservedAtText,
            activeAircraft,
            activeAircraft.Concat(staleAircraft).ToArray());

        HexDbAircraft? GetDetails(SkyPatternHunter.Domain.Models.OverheadEvent overheadEvent)
        {
            return detailsByHex.GetValueOrDefault(overheadEvent.Aircraft.Hex);
        }
    }
}
