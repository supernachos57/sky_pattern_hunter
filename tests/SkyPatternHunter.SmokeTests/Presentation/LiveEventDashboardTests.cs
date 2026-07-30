using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Presentation;

namespace SkyPatternHunter.SmokeTests.Presentation;

public class LiveEventDashboardTests
{
    [Fact]
    public void Refresh_ReturnsEmptyState_WhenJournalHasNoEvents()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            var dashboard = new LiveEventDashboard(journal);

            var snapshot = dashboard.Refresh();

            Assert.Equal(0, snapshot.EventCount);
            Assert.Equal("None", snapshot.LatestAircraftHex);
            Assert.Equal("No events yet", snapshot.LatestObservedAtText);
            Assert.Empty(snapshot.Events);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Refresh_ReturnsLatestEvents_FromJournal()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            var first = new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 1, 1, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00"));
            var second = new OverheadEvent(
                new Aircraft("BBBB02", "FLT2", 2, 2, 32000, 180, 430, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00"));

            journal.Append(first);
            journal.Append(second);

            var dashboard = new LiveEventDashboard(journal);
            var snapshot = dashboard.Refresh();

            Assert.Equal(2, snapshot.EventCount);
            Assert.Equal("BBBB02", snapshot.LatestAircraftHex);
            Assert.Equal("2026-07-29T12:01:00.0000000+00:00", snapshot.LatestObservedAtText);
            Assert.Equal(2, snapshot.Events.Count);
            Assert.Equal("BBBB02", snapshot.Events[0].AircraftHex);
            Assert.Equal("AAAA01", snapshot.Events[1].AircraftHex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
