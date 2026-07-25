using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.SmokeTests.Events;

public class OverheadEventJournalTests
{
    [Fact]
    public void AppendAndReadAll_PersistsOverheadEventsAndReturnsSummary()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var journal = new OverheadEventJournal(tempDirectory);

        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        var observedAt = DateTimeOffset.Parse("2026-07-24T12:00:00+00:00");
        var overheadEvent = new OverheadEvent(aircraft, observedAt);

        journal.Append(overheadEvent);

        var events = journal.ReadAll();
        var summary = journal.GetSummary();

        Assert.Single(events);
        Assert.Equal("A1B2C3", events[0].Aircraft.Hex);
        Assert.Equal(observedAt, events[0].ObservedAt);
        Assert.Equal(1, summary.EventCount);
        Assert.Equal(observedAt, summary.LatestObservedAt);
        Assert.Equal("A1B2C3", summary.LatestAircraftHex);
    }
}