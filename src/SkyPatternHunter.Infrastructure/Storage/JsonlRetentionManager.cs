using SkyPatternHunter.Infrastructure.Persistence;

namespace SkyPatternHunter.Infrastructure.Storage;

public sealed record JsonlRetentionPolicy(TimeSpan RetentionPeriod, string ArchiveDirectory)
{
    public DateTimeOffset GetCutoff(DateTimeOffset now) => now - RetentionPeriod;
}

public sealed record JsonlRetentionResult(int KeptCount, int ArchivedCount, string? ArchiveFilePath);

public sealed class JsonlRetentionManager
{
    private readonly JsonlDataStore _dataStore;
    private readonly JsonlRetentionPolicy _policy;

    public JsonlRetentionManager(JsonlDataStore dataStore, JsonlRetentionPolicy policy)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public JsonlRetentionResult PruneAndArchive<T>(string fileName, Func<T, DateTimeOffset> timestampSelector, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(timestampSelector);

        var records = _dataStore.Read<T>(fileName).ToList();
        var cutoff = _policy.GetCutoff(now);
        var kept = new List<T>();
        var archived = new List<T>();

        foreach (var record in records)
        {
            if (timestampSelector(record) >= cutoff)
            {
                kept.Add(record);
                continue;
            }

            archived.Add(record);
        }

        _dataStore.WriteAll(fileName, kept);

        string? archiveFilePath = null;
        if (archived.Count > 0)
        {
            Directory.CreateDirectory(_policy.ArchiveDirectory);
            var archiveFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-{now.UtcDateTime:yyyyMMddHHmmss}.jsonl";
            var archiveStore = new JsonlDataStore(_policy.ArchiveDirectory);
            archiveFilePath = archiveStore.WriteAll(archiveFileName, archived);
        }

        return new JsonlRetentionResult(kept.Count, archived.Count, archiveFilePath);
    }
}
