using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.SmokeTests.Events;

public class AircraftSightingJournalTests
{
    [Fact]
    public void AppendAndReadAll_PersistsSightingsAndReturnsSummary()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var journal = new AircraftSightingJournal(tempDirectory);

        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        var observedAt = DateTimeOffset.Parse("2026-07-24T12:00:00+00:00");
        var sighting = new AircraftSighting(aircraft, observedAt, isOverhead: true);

        journal.Append(sighting);

        var sightings = journal.ReadAll();
        var summary = journal.GetSummary();

        Assert.Single(sightings);
        Assert.Equal("A1B2C3", sightings[0].Aircraft.Hex);
        Assert.True(sightings[0].IsOverhead);
        Assert.Equal(observedAt, sightings[0].ObservedAt);
        Assert.Equal(1, summary.SightingCount);
        Assert.Equal(observedAt, summary.LatestObservedAt);
        Assert.Equal("A1B2C3", summary.LatestAircraftHex);
    }
}