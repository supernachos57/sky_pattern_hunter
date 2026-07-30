using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Domain.Notifications;

namespace SkyPatternHunter.Infrastructure.Notifications;

public sealed class DiscordNotificationDispatcher
{
    private readonly DiscordDmNotificationProvider _provider;
    private readonly DiscordDmNotificationPreferences _preferences;
    private readonly string _recipientUserId;
    private readonly Dictionary<string, DateTimeOffset> _lastSentByAircraftHex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public DiscordNotificationDispatcher(
        string recipientUserId,
        DiscordDmNotificationPreferences preferences,
        DiscordDmNotificationProvider? provider = null)
    {
        _recipientUserId = recipientUserId;
        _preferences = preferences;
        _provider = provider ?? new DiscordDmNotificationProvider();
    }

    public DiscordNotificationDispatchResult CreatePayload(OverheadEvent overheadEvent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(overheadEvent);

        DateTimeOffset? lastSentAt;
        lock (_sync)
        {
            lastSentAt = _lastSentByAircraftHex.GetValueOrDefault(overheadEvent.Aircraft.Hex);
        }

        var request = new DiscordDmNotificationRequest(_recipientUserId, overheadEvent, _preferences, lastSentAt);
        var payload = _provider.CreatePayload(request, now);

        if (payload is null)
        {
            var suppressedByCooldown = _preferences.Enabled
                && lastSentAt.HasValue
                && now - lastSentAt.Value < _preferences.Cooldown;

            return new DiscordNotificationDispatchResult(null, suppressedByCooldown);
        }

        lock (_sync)
        {
            _lastSentByAircraftHex[overheadEvent.Aircraft.Hex] = now;
        }

        return new DiscordNotificationDispatchResult(payload, false);
    }
}

public sealed record DiscordNotificationDispatchResult(DiscordDmNotificationPayload? Payload, bool SuppressedByCooldown);