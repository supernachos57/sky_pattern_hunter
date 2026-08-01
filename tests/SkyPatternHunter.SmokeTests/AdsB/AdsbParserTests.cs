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
    public void Parse_RawPiAdsbPayload_MapsHexToAircraftModel()
    {
        var parser = new AdsbParser();
        const string payload = "*8DAA6ADEEA3CA858013C08A09877;";

        Aircraft aircraft = parser.Parse(payload);

        Assert.Equal("AA6ADE", aircraft.Hex);
        Assert.Null(aircraft.Flight);
        Assert.Equal(0, aircraft.Latitude);
        Assert.Equal(0, aircraft.Longitude);
        Assert.Equal(0, aircraft.Altitude);
        Assert.Equal(0, aircraft.Track);
        Assert.Equal(0, aircraft.Speed);
        Assert.Null(aircraft.Squawk);
    }

    [Fact]
    public void Parse_RawAirbornePositionPair_DecodesAltitudeAndPosition()
    {
        var parser = new AdsbParser();

        // Known DF17 airborne position CPR example frames for ICAO 40621D.
        _ = parser.Parse("*8D40621D58C382D690C8AC2863A7;"); // even
        Aircraft aircraft = parser.Parse("*8D40621D58C386435CC412692AD6;"); // odd

        Assert.Equal("40621D", aircraft.Hex);
        Assert.Equal(38000, aircraft.Altitude);
        Assert.InRange(aircraft.Latitude, 52.20, 52.30);
        Assert.InRange(aircraft.Longitude, 3.85, 3.98);
    }

    [Fact]
    public void Parse_ThrowsForInvalidJson()
    {
        var parser = new AdsbParser();

        Assert.Throws<JsonException>(() => parser.Parse("not-json"));
    }
}
