using System.Text.Json;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class AdsbParserTests
{
    [Fact]
    public void Parse_MapsJsonPayloadToAircraftModel()
    {
        var parser = new AdsbParser();
        const string json = """
        {
          "hex": "A1B2C3",
          "flight": "DAL123",
          "alt_baro": 32000,
          "lat": 28.1234,
          "lon": -81.2345,
          "track": 270,
          "speed": 450,
          "squawk": 1234
        }
        """;

        Aircraft aircraft = parser.Parse(json);

        Assert.Equal("A1B2C3", aircraft.Hex);
        Assert.Equal("DAL123", aircraft.Flight);
        Assert.Equal(28.1234, aircraft.Latitude);
        Assert.Equal(-81.2345, aircraft.Longitude);
        Assert.Equal(32000, aircraft.Altitude);
        Assert.Equal(270, aircraft.Track);
        Assert.Equal(450, aircraft.Speed);
        Assert.Equal(1234, aircraft.Squawk);
    }

    [Fact]
    public void Parse_ThrowsForInvalidJson()
    {
        var parser = new AdsbParser();

        Assert.Throws<JsonException>(() => parser.Parse("not-json"));
    }
}
