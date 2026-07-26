using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Domain.Notifications;

public sealed record DiscordDmNotificationPreferences(bool Enabled = true, TimeSpan Cooldown = default, string? MessagePrefix = null);

public sealed record DiscordDmNotificationRequest(
	string RecipientUserId,
	OverheadEvent OverheadEvent,
	DiscordDmNotificationPreferences Preferences,
	DateTimeOffset? LastSentAt = null);

public sealed record DiscordDmNotificationPayload(string RecipientUserId, string Content);
