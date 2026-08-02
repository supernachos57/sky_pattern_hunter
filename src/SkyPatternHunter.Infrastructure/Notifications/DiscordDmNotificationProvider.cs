using System.Globalization;
using SkyPatternHunter.Domain.Notifications;

namespace SkyPatternHunter.Infrastructure.Notifications;

public sealed class DiscordDmNotificationProvider
{
    private const double KnotsToMilesPerHour = 1.15078d;

    public DiscordDmNotificationPayload? CreatePayload(DiscordDmNotificationRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Preferences.Enabled)
        {
            return null;
        }

        if (request.LastSentAt.HasValue && now - request.LastSentAt.Value < request.Preferences.Cooldown)
        {
            return null;
        }

        var aircraft = request.OverheadEvent.Aircraft;
        var flight = string.IsNullOrWhiteSpace(aircraft.Flight) ? "unknown flight" : aircraft.Flight;
        var prefix = string.IsNullOrWhiteSpace(request.Preferences.MessagePrefix)
            ? "Overhead alert"
            : request.Preferences.MessagePrefix.Trim();
        var squawk = aircraft.Squawk.HasValue
            ? aircraft.Squawk.Value.ToString(CultureInfo.InvariantCulture)
            : "unknown";
        var speed = aircraft.Speed > 0
            ? $"{aircraft.Speed * KnotsToMilesPerHour:0} mph"
            : "unknown";

        var content = $"{prefix}: aircraft {aircraft.Hex} ({flight}) was overhead at {request.OverheadEvent.ObservedAt:O}. Altitude {aircraft.Altitude} ft, speed {speed}, squawk {squawk}.";

        return new DiscordDmNotificationPayload(request.RecipientUserId, content);
    }
}