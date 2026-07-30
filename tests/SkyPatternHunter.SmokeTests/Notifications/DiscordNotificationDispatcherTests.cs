using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Domain.Notifications;
using SkyPatternHunter.Infrastructure.Notifications;

namespace SkyPatternHunter.SmokeTests.Notifications;

public class DiscordNotificationDispatcherTests
{
    [Fact]
    public void CreatePayload_ReturnsPayload_ForFirstOverheadEvent()
    {
        var provider = new DiscordDmNotificationProvider();
        var dispatcher = new DiscordNotificationDispatcher(
            "1234567890",
            new DiscordDmNotificationPreferences(true, TimeSpan.FromMinutes(10), "Sky Pattern Hunter"),
            provider);

        var overheadEvent = CreateEvent("A1B2C3", "2026-07-24T12:00:00+00:00");
        var result = dispatcher.CreatePayload(overheadEvent, DateTimeOffset.Parse("2026-07-24T12:00:01+00:00"));

        Assert.NotNull(result.Payload);
        Assert.False(result.SuppressedByCooldown);
        Assert.Equal("1234567890", result.Payload!.RecipientUserId);
        Assert.Contains("Sky Pattern Hunter", result.Payload.Content);
    }

    [Fact]
    public void CreatePayload_SuppressesRepeatedAircraft_WhenCooldownHasNotElapsed()
    {
        var provider = new DiscordDmNotificationProvider();
        var dispatcher = new DiscordNotificationDispatcher(
            "1234567890",
            new DiscordDmNotificationPreferences(true, TimeSpan.FromMinutes(10), "Sky Pattern Hunter"),
            provider);

        var firstEvent = CreateEvent("A1B2C3", "2026-07-24T12:00:00+00:00");
        var secondEvent = CreateEvent("A1B2C3", "2026-07-24T12:02:00+00:00");

        var firstResult = dispatcher.CreatePayload(firstEvent, DateTimeOffset.Parse("2026-07-24T12:00:00+00:00"));
        var secondResult = dispatcher.CreatePayload(secondEvent, DateTimeOffset.Parse("2026-07-24T12:02:00+00:00"));

        Assert.NotNull(firstResult.Payload);
        Assert.Null(secondResult.Payload);
        Assert.True(secondResult.SuppressedByCooldown);
    }

    [Fact]
    public void CreatePayload_AllowsDifferentAircraft_WithinCooldownWindow()
    {
        var provider = new DiscordDmNotificationProvider();
        var dispatcher = new DiscordNotificationDispatcher(
            "1234567890",
            new DiscordDmNotificationPreferences(true, TimeSpan.FromMinutes(10), "Sky Pattern Hunter"),
            provider);

        var firstEvent = CreateEvent("A1B2C3", "2026-07-24T12:00:00+00:00");
        var secondEvent = CreateEvent("D4E5F6", "2026-07-24T12:01:00+00:00");

        var firstResult = dispatcher.CreatePayload(firstEvent, DateTimeOffset.Parse("2026-07-24T12:00:00+00:00"));
        var secondResult = dispatcher.CreatePayload(secondEvent, DateTimeOffset.Parse("2026-07-24T12:01:00+00:00"));

        Assert.NotNull(firstResult.Payload);
        Assert.NotNull(secondResult.Payload);
        Assert.False(secondResult.SuppressedByCooldown);
    }

    private static OverheadEvent CreateEvent(string aircraftHex, string observedAt)
    {
        var aircraft = new Aircraft(aircraftHex, "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        return new OverheadEvent(aircraft, DateTimeOffset.Parse(observedAt));
    }
}
