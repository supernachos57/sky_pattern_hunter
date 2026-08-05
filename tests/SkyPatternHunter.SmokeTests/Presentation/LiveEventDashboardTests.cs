using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Presentation;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

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
            Assert.Equal("Unknown", snapshot.TodayEvents[1].AltitudeTrendText);
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

    [Fact]
    public void Refresh_SetsLookDirection_FromConfiguredUserPosition()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("LOOK01", "FLT1", 28.45365, -80.98718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Single(snapshot.TodayEvents);
            Assert.Equal("East", snapshot.TodayEvents[0].LookDirectionText);
            Assert.Equal("East", snapshot.ActiveEvents[0].LookDirectionText);
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
    public void Refresh_SetsDistance_FromConfiguredUserPosition()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("DIST01", "FLT1", 29.45365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Equal("69.1 mi", snapshot.TodayEvents[0].DistanceText);
            Assert.Equal("69.1 mi", snapshot.ActiveEvents[0].DistanceText);
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
    public async Task RefreshAsync_ReturnsQuickly_WhenAircraftLookupIsSlow()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 28.45365, -80.98718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));

            var dashboard = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"),
                aircraftClient: new DelayedAircraftLookup(TimeSpan.FromSeconds(10)),
                userLatitude: 28.45365,
                userLongitude: -81.08718);

            var stopwatch = Stopwatch.StartNew();
            var snapshot = await dashboard.RefreshAsync();
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"RefreshAsync took {stopwatch.Elapsed}.");
            Assert.Single(snapshot.ActiveEvents);
            Assert.Equal("East", snapshot.ActiveEvents[0].LookDirectionText);
            Assert.Equal("Level", snapshot.ActiveEvents[0].AltitudeTrendText);
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
    public void Refresh_SetsIntermediateLookDirection_FromConfiguredUserPosition()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("LOOK02", "FLT2", 28.55365, -80.98718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Single(snapshot.TodayEvents);
            Assert.Equal("North-East", snapshot.TodayEvents[0].LookDirectionText);
            Assert.Equal("North-East", snapshot.ActiveEvents[0].LookDirectionText);
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
    public void Refresh_SetsDirection_FromLatestAndPreviousReading()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("COME01", "IN01", 28.47365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("COME01", "IN01", 28.46365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00")));

            journal.Append(new OverheadEvent(
                new Aircraft("GO01", "OUT1", 28.44365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("GO01", "OUT1", 28.43365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("GO01", "OUT1", 28.42365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:02:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("GO01", "OUT1", 28.41365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:03:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:04:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Equal("Coming", snapshot.TodayEvents.Single(item => item.AircraftHex == "COME01").DirectionText);
            Assert.Equal("Going", snapshot.TodayEvents.Single(item => item.AircraftHex == "GO01").DirectionText);
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
    public void Refresh_KeepsDirection_ForStaleAircraft()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("STALE1", "OLD1", 28.44365, -81.08718, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("STALE1", "OLD1", 28.43365, -81.08718, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("STALE1", "OLD1", 28.42365, -81.08718, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:02:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("STALE1", "OLD1", 28.41365, -81.08718, 30000, 90, 400, null),
                DateTimeOffset.Parse("2026-07-29T12:03:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromSeconds(30),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:04:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Single(snapshot.TodayEvents);
            Assert.Equal("Stale", snapshot.TodayEvents[0].StatusText);
            Assert.Equal("Going", snapshot.TodayEvents[0].DirectionText);
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
    public void Refresh_KeepsDirectionGoing_AfterConfirmedDeparture()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            var observations = new[]
            {
                28.46365,
                28.45365,
                28.45765,
                28.46165,
                28.46565,
                28.45565
            };

            for (var index = 0; index < observations.Length; index++)
            {
                journal.Append(new OverheadEvent(
                    new Aircraft("STABLE1", "OUT1", observations[index], -81.08718, 30000, 90, 100, null),
                    DateTimeOffset.Parse("2026-07-29T12:00:00+00:00").AddMinutes(index)));
            }

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:06:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Equal("Going", snapshot.TodayEvents.Single().DirectionText);
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
    public void Refresh_DefaultsDirectionToComing_WhenOnlyOneReadingExists()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("ONE01", "SOLO", 28.55365, -80.98718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00"),
                userLatitude: 28.45365,
                userLongitude: -81.08718).Refresh();

            Assert.Single(snapshot.TodayEvents);
            Assert.Equal("Coming", snapshot.TodayEvents[0].DirectionText);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private sealed class DelayedAircraftLookup : IAircraftLookup
    {
        private readonly TimeSpan _delay;

        public DelayedAircraftLookup(TimeSpan delay)
        {
            _delay = delay;
        }

        public async Task<AircraftDetails?> GetAircraftAsync(string hex)
        {
            await Task.Delay(_delay);
            return null;
        }
    }
}
