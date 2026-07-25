using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Persistence;

namespace SkyPatternHunter.SmokeTests.Persistence;

public class JsonlDataStoreTests
{
    [Fact]
    public void AppendAndRead_WritesAndReadsJsonlRecords()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        var store = new JsonlDataStore(tempDirectory);

        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        const string rawMessage = "{\"hex\":\"A1B2C3\",\"flight\":\"DAL123\"}";

        store.Append("aircraft.jsonl", aircraft);
        store.Append("raw-messages.jsonl", rawMessage);

        var persistedAircraft = store.Read<Aircraft>("aircraft.jsonl");
        var persistedMessages = store.Read<string>("raw-messages.jsonl");

        Assert.Single(persistedAircraft);
        Assert.Equal("A1B2C3", persistedAircraft[0].Hex);
        Assert.Equal("DAL123", persistedAircraft[0].Flight);
        Assert.Single(persistedMessages);
        Assert.Equal(rawMessage, persistedMessages[0]);
    }
}
