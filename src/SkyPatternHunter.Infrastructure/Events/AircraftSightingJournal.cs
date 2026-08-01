using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Persistence;

namespace SkyPatternHunter.Infrastructure.Events;

public sealed class AircraftSightingJournal
{
    private const string FileName = "aircraft-sightings.jsonl";
    private readonly JsonlDataStore _dataStore;

    public AircraftSightingJournal(string? baseDirectory = null)
    {
        _dataStore = new JsonlDataStore(baseDirectory);
    }

    public void Append(AircraftSighting sighting)
    {
        ArgumentNullException.ThrowIfNull(sighting);

        _dataStore.Append(FileName, sighting);
    }

    public IReadOnlyList<AircraftSighting> ReadAll()
    {
        return _dataStore.Read<AircraftSighting>(FileName);
    }

    public AircraftSightingJournalSummary GetSummary()
    {
        var sightings = ReadAll();
        var latestSighting = sightings.LastOrDefault();

        return new AircraftSightingJournalSummary(
            sightings.Count,
            latestSighting?.ObservedAt,
            latestSighting?.Aircraft.Hex);
    }
}

public sealed record AircraftSightingJournalSummary(int SightingCount, DateTimeOffset? LatestObservedAt, string? LatestAircraftHex);