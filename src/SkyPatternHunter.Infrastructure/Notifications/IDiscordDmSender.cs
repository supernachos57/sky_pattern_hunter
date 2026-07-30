using SkyPatternHunter.Domain.Notifications;

namespace SkyPatternHunter.Infrastructure.Notifications;

public interface IDiscordDmSender
{
    DiscordDmSendResult Send(DiscordDmNotificationPayload payload);
}

public sealed record DiscordDmSendResult(bool Sent, string? ErrorMessage = null);