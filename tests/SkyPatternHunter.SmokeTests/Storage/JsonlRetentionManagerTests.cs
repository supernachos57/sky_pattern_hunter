using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Persistence;
using SkyPatternHunter.Infrastructure.Storage;

namespace SkyPatternHunter.SmokeTests.Storage;

public class JsonlRetentionManagerTests
{
    [Fact]
    public void PruneAndArchive_RemovesExpiredRecordsAndArchivesThem()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(rootDirectory, "data");
        var archiveDirectory = Path.Combine(rootDirectory, "archive");
        var store = new JsonlDataStore(dataDirectory);
        var manager = new JsonlRetentionManager(store, new JsonlRetentionPolicy(TimeSpan.FromDays(7), archiveDirectory));
        var now = DateTimeOffset.Parse("2026-07-26T12:00:00+00:00");
        var oldEvent = new OverheadEvent(
            new Aircraft("OLD123", "OLD1", 28.1, -81.2, 31000, 180, 420, 1234),
            now.AddDays(-10));
        var recentEvent = new OverheadEvent(
            new Aircraft("NEW456", "NEW2", 28.2, -81.3, 32000, 190, 430, 5678),
            now.AddDays(-2));

        store.WriteAll("overhead-events.jsonl", new[] { oldEvent, recentEvent });

        var result = manager.PruneAndArchive<OverheadEvent>("overhead-events.jsonl", item => item.ObservedAt, now);
        var remaining = store.Read<OverheadEvent>("overhead-events.jsonl");
        var archiveFileName = Path.GetFileName(result.ArchiveFilePath!);
        var archiveStore = new JsonlDataStore(archiveDirectory);
        var archived = archiveStore.Read<OverheadEvent>(archiveFileName);

        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(1, result.KeptCount);
        Assert.NotNull(result.ArchiveFilePath);
        Assert.Single(remaining);
        Assert.Equal("NEW456", remaining[0].Aircraft.Hex);
        Assert.Single(archived);
        Assert.Equal("OLD123", archived[0].Aircraft.Hex);
    }
}
