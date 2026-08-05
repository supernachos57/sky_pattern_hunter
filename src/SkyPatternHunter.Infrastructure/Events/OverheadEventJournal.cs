using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Persistence;

namespace SkyPatternHunter.Infrastructure.Events;

public sealed class OverheadEventJournal : IOverheadEventStore
{
    private const string FileName = "overhead-events.jsonl";
    private readonly JsonlDataStore _dataStore;
    private readonly int? _historyDays;

    public OverheadEventJournal(string? baseDirectory = null, int? historyDays = null)
    {
        if (historyDays is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(historyDays), "History days must be greater than 0.");
        }

        _dataStore = new JsonlDataStore(baseDirectory);
        _historyDays = historyDays;

        if (_historyDays is { } configuredHistoryDays)
        {
            PruneOlderThan(DateTimeOffset.UtcNow.AddDays(-configuredHistoryDays));
        }
    }

    public void Append(OverheadEvent overheadEvent)
    {
        ArgumentNullException.ThrowIfNull(overheadEvent);

        if (_historyDays is { } historyDays)
        {
            PruneOlderThan(overheadEvent.ObservedAt.AddDays(-historyDays));
        }

        _dataStore.Append(FileName, overheadEvent);
    }

    public void PruneOlderThan(DateTimeOffset cutoff)
    {
        var events = ReadAll();
        var retainedEvents = events
            .Where(overheadEvent => overheadEvent.ObservedAt >= cutoff)
            .ToArray();

        if (retainedEvents.Length != events.Count)
        {
            _dataStore.WriteAll(FileName, retainedEvents);
        }
    }

    public IReadOnlyList<OverheadEvent> ReadAll()
    {
        return _dataStore.Read<OverheadEvent>(FileName);
    }

    public OverheadEventJournalSummary GetSummary()
    {
        var events = ReadAll();
        var latestEvent = events.LastOrDefault();

        return new OverheadEventJournalSummary(
            events.Count,
            latestEvent?.ObservedAt,
            latestEvent?.Aircraft.Hex);
    }
}

public sealed record OverheadEventJournalSummary(int EventCount, DateTimeOffset? LatestObservedAt, string? LatestAircraftHex);