using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.SmokeTests.Events;

public class SqliteFlightHistoryStoreTests
{
    [Fact]
    public void Append_SamplesTrackPointsAtConfiguredInterval()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-08-05T12:00:00+00:00");

        store.Append(CreateEvent("ABC123", "FLT1", 28.45, start));
        store.Append(CreateEvent("ABC123", "FLT1", 28.46, start.AddSeconds(10)));
        store.Append(CreateEvent("ABC123", "FLT1", 28.47, start.AddSeconds(30)));

        var events = store.ReadAll();

        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { start, start.AddSeconds(30) }, events.Select(overheadEvent => overheadEvent.ObservedAt));
    }

    [Fact]
    public void Append_StartsNewSessionWhenCallsignChanges()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-08-05T12:00:00+00:00");

        store.Append(CreateEvent("ABC123", "FLT1", 28.45, start));
        store.Append(CreateEvent("ABC123", "FLT2", 28.46, start.AddSeconds(10)));

        var events = store.ReadAll();

        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { "FLT1", "FLT2" }, events.Select(overheadEvent => overheadEvent.Aircraft.Flight));
    }

    [Fact]
    public void ReadAll_IncludesLatestObservationBeforeTheNextSampleIsDue()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-08-05T12:00:00+00:00");

        store.Append(CreateEvent("ABC123", "FLT1", 28.45, start));
        store.Append(CreateEvent("ABC123", "FLT1", 28.46, start.AddSeconds(10)));

        var latestEvent = store.ReadAll().MaxBy(overheadEvent => overheadEvent.ObservedAt);

        Assert.NotNull(latestEvent);
        Assert.Equal(start.AddSeconds(10), latestEvent.ObservedAt);
        Assert.Equal(28.46, latestEvent.Aircraft.Latitude);
    }

    [Fact]
    public void GetLatestFlightDetails_ReturnsSessionAndOrderedSampledPath()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-08-05T12:00:00+00:00");

        store.Append(CreateEvent("ABC123", "FLT1", 28.45, start));
        store.Append(CreateEvent("ABC123", "FLT1", 28.47, start.AddSeconds(30)));

        var details = store.GetLatestFlightDetails("ABC123", "FLT1");

        Assert.NotNull(details);
        Assert.Equal("FLT1", details.Session.Callsign);
        Assert.Equal(start, details.Session.StartedAt);
        Assert.Equal(start.AddSeconds(30), details.Session.EndedAt);
        Assert.Equal(new[] { 28.45, 28.47 }, details.TrackPoints.Select(point => point.Latitude));
    }

    [Fact]
    public void ReadFlightSessions_ReturnsEachRetainedSessionForTheSameAircraft()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));
        var start = DateTimeOffset.Parse("2026-08-04T12:00:00+00:00");

        store.Append(CreateEvent("ABC123", "FLT1", 28.45, start));
        store.Append(CreateEvent("ABC123", "FLT2", 28.47, start.AddDays(1)));

        var sessions = store.ReadFlightSessions();

        Assert.Equal(2, sessions.Count);
        Assert.Equal(new[] { "FLT2", "FLT1" }, sessions.Select(summary => summary.Session.Callsign));
        Assert.Equal(start.AddDays(1), sessions[0].LatestPoint.ObservedAt);
    }

    [Fact]
    public void Append_PrunesTrackPointsOutsideHistoryWindow()
    {
        var tempDirectory = CreateTempDirectory();
        var store = new SqliteFlightHistoryStore(tempDirectory, historyDays: 30, sampleInterval: TimeSpan.FromSeconds(30));

        store.Append(CreateEvent("OLD001", "OLD1", 28.45, DateTimeOffset.Parse("2026-01-01T12:00:00+00:00")));
        store.Append(CreateEvent("NEW001", "NEW1", 28.46, DateTimeOffset.Parse("2026-02-01T12:00:00+00:00")));

        var events = store.ReadAll();

        Assert.Single(events);
        Assert.Equal("NEW001", events[0].Aircraft.Hex);
    }

    private static OverheadEvent CreateEvent(string hex, string flight, double latitude, DateTimeOffset observedAt)
    {
        return new OverheadEvent(new Aircraft(hex, flight, latitude, -81.08, 30000, 90, 400, 1234), observedAt);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}