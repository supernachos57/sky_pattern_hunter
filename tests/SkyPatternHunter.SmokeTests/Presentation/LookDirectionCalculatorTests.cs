using SkyPatternHunter.Presentation;

namespace SkyPatternHunter.SmokeTests.Presentation;

public class LookDirectionCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsHere_WhenAircraftMatchesUserPosition()
    {
        var direction = LookDirectionCalculator.Calculate(28.45365, -81.08718, 28.45365, -81.08718);

        Assert.Equal("Here", direction);
    }

    [Fact]
    public void Calculate_ReturnsNorth_WhenAircraftIsNorthOfUser()
    {
        var direction = LookDirectionCalculator.Calculate(28.45365, -81.08718, 28.55365, -81.08718);

        Assert.Equal("North", direction);
    }

    [Fact]
    public void Calculate_ReturnsNorthEast_WhenAircraftIsNorthEastOfUser()
    {
        var direction = LookDirectionCalculator.Calculate(28.45365, -81.08718, 28.55365, -80.98718);

        Assert.Equal("North-East", direction);
    }

    [Fact]
    public void Calculate_ReturnsUnknown_WhenAnyCoordinateIsInvalid()
    {
        var direction = LookDirectionCalculator.Calculate(double.NaN, -81.08718, 28.55365, -81.08718);

        Assert.Equal("Unknown", direction);
    }
}
