using System.Globalization;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.Presentation;

public sealed record LiveEventDashboardSnapshot(
    int EventCount,
    string LatestAircraftHex,
    string LatestObservedAtText,
    IReadOnlyList<RecentEventViewModel> Events);

public sealed class LiveEventDashboard
{
    private readonly OverheadEventJournal _journal;
    private readonly int _maxEvents;

    public LiveEventDashboard(OverheadEventJournal journal, int maxEvents = 25)
    {
        _journal = journal;
        _maxEvents = maxEvents;
    }

    public LiveEventDashboardSnapshot Refresh()
    {
        var events = _journal.ReadAll();
        var latestEvent = events.LastOrDefault();

        var recent = events
            .OrderByDescending(item => item.ObservedAt)
            .Take(_maxEvents)
            .Select(RecentEventViewModel.FromEvent)
            .ToArray();

        return new LiveEventDashboardSnapshot(
            events.Count,
            latestEvent?.Aircraft.Hex ?? "None",
            latestEvent?.ObservedAt.ToString("O", CultureInfo.InvariantCulture) ?? "No events yet",
            recent);
    }
}
