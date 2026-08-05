using System.IO.Compression;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class AircraftDatabaseTests
{
    [Fact]
    public async Task EnsureDatabase_ImportsNormalizedHexAndLetsFinalDuplicateWin()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var bundledCsvPath = Path.Combine(directory, "bundled.csv.gz");
            WriteGzip(bundledCsvPath, [
                "a1b2c3;N123AB;C172;001;CESSNA 172 Skyhawk;2005;FIRST OWNER;",
                "A1B2C3;N123CD;C172;002;CESSNA 172 Skyhawk;2006;FINAL OWNER;",
                "D4E5F6;N99999;C172;003;CESSNA 172 Skyhawk;2007;OTHER OWNER;"
            ]);

            var manager = new AircraftDatabaseManager(
                bundledCsvPath,
                Path.Combine(directory, "runtime"),
                "https://example.test/aircraft.csv.gz",
                minimumValidRows: 2);

            var result = manager.EnsureDatabase();

            Assert.True(result.Succeeded, result.Message);
            var aircraft = await new SqliteAircraftLookup(manager.DatabasePath).GetAircraftAsync(" a1b2c3 ");
            Assert.NotNull(aircraft);
            Assert.Equal("N123CD", aircraft.Registration);
            Assert.Equal("FINAL OWNER", aircraft.RegisteredOwner);
            Assert.Equal("CESSNA 172 Skyhawk", aircraft.Description);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("a1b2c3", "A1B2C3")]
    [InlineData(" A1B2C3 ", "A1B2C3")]
    [InlineData("A1B2C", null)]
    [InlineData("A1B2CZ", null)]
    public void Normalize_OnlyAcceptsSixCharacterIcaoHex(string input, string? expected)
    {
        Assert.Equal(expected, AircraftHex.Normalize(input));
    }

    private static void WriteGzip(string path, IEnumerable<string> rows)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionMode.Compress);
        using var writer = new StreamWriter(gzip);
        foreach (var row in rows)
        {
            writer.WriteLine(row);
        }
    }
}
