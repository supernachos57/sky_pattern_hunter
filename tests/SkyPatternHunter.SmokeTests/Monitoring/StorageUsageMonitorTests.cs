using SkyPatternHunter.Infrastructure.Monitoring;

namespace SkyPatternHunter.SmokeTests.Monitoring;

public class StorageUsageMonitorTests
{
    [Fact]
    public void ScanDirectory_ReturnsFileCountAndTotalBytes()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var nestedDirectory = Path.Combine(rootDirectory, "nested");
        Directory.CreateDirectory(nestedDirectory);

        var firstFile = Path.Combine(rootDirectory, "first.txt");
        var secondFile = Path.Combine(nestedDirectory, "second.txt");
        File.WriteAllText(firstFile, "abc");
        File.WriteAllText(secondFile, "hello");

        var monitor = new StorageUsageMonitor();
        var snapshot = monitor.ScanDirectory(rootDirectory);

        Assert.Equal(rootDirectory, snapshot.DirectoryPath);
        Assert.Equal(2, snapshot.FileCount);
        Assert.Equal(8, snapshot.TotalBytes);
        Assert.Equal("8 bytes", snapshot.TotalBytesText);
    }
}
