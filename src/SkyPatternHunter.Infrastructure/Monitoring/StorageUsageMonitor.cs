namespace SkyPatternHunter.Infrastructure.Monitoring;

public sealed record StorageUsageSnapshot(string DirectoryPath, int FileCount, long TotalBytes)
{
    public string TotalBytesText => $"{TotalBytes:N0} bytes";
}

public sealed class StorageUsageMonitor
{
    public StorageUsageSnapshot ScanDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return new StorageUsageSnapshot(directoryPath, 0, 0);
        }

        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(file => new FileInfo(file).Length);
        return new StorageUsageSnapshot(directoryPath, files.Length, totalBytes);
    }
}
