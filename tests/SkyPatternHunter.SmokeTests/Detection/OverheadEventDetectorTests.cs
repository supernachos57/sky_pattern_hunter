using SkyPatternHunter.Application.Detection;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.SmokeTests.Detection;

public class OverheadEventDetectorTests
{
    [Fact]
    public void Detect_ReturnsEvent_WhenAircraftIsOverhead()
    {
        var detector = new OverheadEventDetector();
        var aircraft = new Aircraft(
            "A1B2C3",
            "DAL123",
            28.1234,
            -81.2345,
            32000,
            270,
            450,
            1234);
        var observedAt = DateTimeOffset.Parse("2026-07-24T12:00:00+00:00");

        var result = detector.Detect(aircraft, 28.1234, -81.2345, 5, observedAt);

        Assert.NotNull(result);
        Assert.Same(aircraft, result!.Aircraft);
        Assert.Equal(observedAt, result.ObservedAt);
    }

    [Fact]
    public void Detect_ReturnsNull_WhenAircraftIsNotOverhead()
    {
        var detector = new OverheadEventDetector();
        var aircraft = new Aircraft(
            "A1B2C3",
            "DAL123",
            28.1234,
            -81.2345,
            32000,
            270,
            450,
            1234);
        var observedAt = DateTimeOffset.Parse("2026-07-24T12:00:00+00:00");

        var result = detector.Detect(aircraft, 30.0, -83.0, 0.0000001, observedAt);

        Assert.Null(result);
    }
}
