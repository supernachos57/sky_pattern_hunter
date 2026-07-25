using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Domain.Notifications;
using SkyPatternHunter.Infrastructure.Notifications;

namespace SkyPatternHunter.SmokeTests.Notifications;

public class DiscordDmNotificationProviderTests
{
    [Fact]
    public void CreatePayload_FormatsOverheadAlertMessage()
    {
        var provider = new DiscordDmNotificationProvider();
        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        var overheadEvent = new OverheadEvent(aircraft, DateTimeOffset.Parse("2026-07-24T12:00:00+00:00"));
        var request = new DiscordDmNotificationRequest(
            "1234567890",
            overheadEvent,
            new DiscordDmNotificationPreferences(true, TimeSpan.Zero, "Sky Pattern Hunter"));

        var payload = provider.CreatePayload(request, DateTimeOffset.Parse("2026-07-24T12:05:00+00:00"));

        Assert.NotNull(payload);
        Assert.Equal("1234567890", payload!.RecipientUserId);
        Assert.Contains("Sky Pattern Hunter", payload.Content);
        Assert.Contains("A1B2C3", payload.Content);
        Assert.Contains("DAL123", payload.Content);
        Assert.Contains("Altitude 32000 ft", payload.Content);
        Assert.Contains("speed 450 kt", payload.Content);
    }

    [Fact]
    public void CreatePayload_ReturnsNull_WhenCooldownHasNotElapsed()
    {
        var provider = new DiscordDmNotificationProvider();
        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        var overheadEvent = new OverheadEvent(aircraft, DateTimeOffset.Parse("2026-07-24T12:00:00+00:00"));
        var request = new DiscordDmNotificationRequest(
            "1234567890",
            overheadEvent,
            new DiscordDmNotificationPreferences(true, TimeSpan.FromMinutes(10), "Sky Pattern Hunter"),
            DateTimeOffset.Parse("2026-07-24T12:00:30+00:00"));

        var payload = provider.CreatePayload(request, DateTimeOffset.Parse("2026-07-24T12:05:00+00:00"));

        Assert.Null(payload);
    }

    [Fact]
    public void CreatePayload_ReturnsNull_WhenNotificationsAreDisabled()
    {
        var provider = new DiscordDmNotificationProvider();
        var aircraft = new Aircraft("A1B2C3", "DAL123", 28.1234, -81.2345, 32000, 270, 450, 1234);
        var overheadEvent = new OverheadEvent(aircraft, DateTimeOffset.Parse("2026-07-24T12:00:00+00:00"));
        var request = new DiscordDmNotificationRequest(
            "1234567890",
            overheadEvent,
            new DiscordDmNotificationPreferences(false, TimeSpan.Zero, "Sky Pattern Hunter"));

        var payload = provider.CreatePayload(request, DateTimeOffset.Parse("2026-07-24T12:05:00+00:00"));

        Assert.Null(payload);
    }
}