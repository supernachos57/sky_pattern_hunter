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

            Assert.Equal(0, snapshot.ActiveAircraftCount);
            Assert.Equal("None", snapshot.LatestAircraftHex);
            Assert.Equal("No events yet", snapshot.LatestObservedAtText);
            Assert.Empty(snapshot.ActiveEvents);
            Assert.Empty(snapshot.TodayEvents);
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

            var dashboard = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"));
            var snapshot = dashboard.Refresh();

            Assert.Equal(2, snapshot.ActiveAircraftCount);
            Assert.Equal("BBBB02", snapshot.LatestAircraftHex);
            var expectedLocalTime = second.ObservedAt.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt");
            Assert.Equal(expectedLocalTime, snapshot.LatestObservedAtText);
            Assert.Equal(2, snapshot.TodayEvents.Count);
            Assert.Equal("BBBB02", snapshot.TodayEvents[0].AircraftHex);
            Assert.Equal("AAAA01", snapshot.TodayEvents[1].AircraftHex);
            Assert.Equal(second.ObservedAt.ToLocalTime().ToString("zzz"), snapshot.TodayEvents[0].UtcOffsetText);
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
    public void Refresh_KeepsOnlyTheNewestEventForEachAircraft()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            var first = new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 1, 1, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00"));
            var update = new OverheadEvent(
                new Aircraft("aaaa01", "FLT1", 1, 1, 31000, 95, 410, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00"));

            journal.Append(first);
            journal.Append(update);

            var dashboard = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"));
            var snapshot = dashboard.Refresh();

            Assert.Equal(1, snapshot.ActiveAircraftCount);
            Assert.Single(snapshot.TodayEvents);
            Assert.Equal("aaaa01", snapshot.TodayEvents[0].AircraftHex);
            Assert.Equal("31000 ft", snapshot.TodayEvents[0].AltitudeText);
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
    public void Refresh_ShowsActiveAircraftBeforeStaleAircraftInToday()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("STALE1", "OLD1", 1, 1, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("ACTIVE", "NEW1", 1, 1, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:01:30+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromSeconds(60),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00")).Refresh();

            Assert.Equal(1, snapshot.ActiveAircraftCount);
            Assert.Single(snapshot.ActiveEvents);
            Assert.Equal("ACTIVE", snapshot.ActiveEvents[0].AircraftHex);
            Assert.Equal(new[] { "ACTIVE", "STALE1" }, snapshot.TodayEvents.Select(item => item.AircraftHex));
            Assert.Equal("Active", snapshot.TodayEvents[0].StatusText);
            Assert.Equal("Stale", snapshot.TodayEvents[1].StatusText);
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
    public void Refresh_FormatsSpeedInKnotsAndMph_AndMarksUnavailableSpeed()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 1, 1, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("BBBB02", "FLT2", 1, 1, 30000, 90, 0, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00")).Refresh();

            Assert.Equal("Unknown", snapshot.TodayEvents[0].SpeedText);
            Assert.Equal("Unknown", snapshot.TodayEvents[0].MphText);
            Assert.Equal("100 kt", snapshot.TodayEvents[1].SpeedText);
            Assert.Equal("115 mph", snapshot.TodayEvents[1].MphText);
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
