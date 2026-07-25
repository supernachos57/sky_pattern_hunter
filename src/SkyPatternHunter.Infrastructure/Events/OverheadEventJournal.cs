using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Persistence;

namespace SkyPatternHunter.Infrastructure.Events;

public sealed class OverheadEventJournal
{
    private const string FileName = "overhead-events.jsonl";
    private readonly JsonlDataStore _dataStore;

    public OverheadEventJournal(string? baseDirectory = null)
    {
        _dataStore = new JsonlDataStore(baseDirectory);
    }

    public void Append(OverheadEvent overheadEvent)
    {
        ArgumentNullException.ThrowIfNull(overheadEvent);

        _dataStore.Append(FileName, overheadEvent);
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