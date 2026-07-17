using SkyPatternHunter.Domain.Models;
using Xunit;

namespace SkyPatternHunter.SmokeTests.Domain;

public class AircraftDomainTests
{
    [Fact]
    public void Aircraft_IsOverhead_WhenDistanceIsWithinThreshold()
    {
        var aircraft = new Aircraft(
            "A1B2C3",
            "DAL123",
            28.1234,
            -81.2345,
            32000,
            270,
            450,
            10);

        var result = aircraft.IsOverhead(28.1234, -81.2345, 5);

        Assert.True(result);
    }

    [Fact]
    public void Aircraft_IsNotOverhead_WhenDistanceExceedsThreshold()
    {
        var aircraft = new Aircraft(
            "A1B2C3",
            "DAL123",
            28.1234,
            -81.2345,
            32000,
            270,
            450,
            10);

        var result = aircraft.IsOverhead(30.0, -83.0, 0.0000001);

        Assert.False(result);
    }
}
