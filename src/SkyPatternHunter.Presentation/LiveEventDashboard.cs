using System.Globalization;
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
    private readonly TimeSpan _staleAfter;
    private readonly Func<DateTimeOffset> _clock;

    public LiveEventDashboard(OverheadEventJournal journal, TimeSpan? staleAfter = null, Func<DateTimeOffset>? clock = null)
    {
        _journal = journal;
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(60);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public LiveEventDashboardSnapshot Refresh()
    {
        var now = _clock();
        var today = now.ToLocalTime().Date;
        var latestByAircraft = _journal.ReadAll()
            .GroupBy(item => item.Aircraft.Hex, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MaxBy(item => item.ObservedAt)!)
            .ToArray();
        var todayEvents = latestByAircraft
            .Where(item => item.ObservedAt.ToLocalTime().Date == today)
            .ToArray();
        var activeAircraft = todayEvents
            .Where(item => now - item.ObservedAt <= _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: false))
            .ToArray();
        var staleAircraft = todayEvents
            .Where(item => now - item.ObservedAt > _staleAfter)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => RecentEventViewModel.FromEvent(item, isStale: true))
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
    }
}
