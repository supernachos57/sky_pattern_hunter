using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SkyPatternHunter.Domain.Notifications;

namespace SkyPatternHunter.Infrastructure.Notifications;

public sealed class DiscordApiDmSender : IDiscordDmSender
{
    private const string DiscordApiBaseUrl = "https://discord.com/api/v10";

    private readonly HttpClient _httpClient;
    private readonly string _botToken;

    public DiscordApiDmSender(string botToken, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new ArgumentException("Discord bot token must not be empty.", nameof(botToken));
        }

        _botToken = botToken;
        _httpClient = httpClient ?? new HttpClient();
    }

    public DiscordDmSendResult Send(DiscordDmNotificationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var dmChannelId = CreateDmChannel(payload.RecipientUserId);
            if (string.IsNullOrWhiteSpace(dmChannelId))
            {
                return new DiscordDmSendResult(false, "Discord DM channel id was empty.");
            }

            var messageRequest = new HttpRequestMessage(HttpMethod.Post, $"{DiscordApiBaseUrl}/channels/{dmChannelId}/messages");
            messageRequest.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);
            messageRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { content = payload.Content }),
                Encoding.UTF8,
                "application/json");

            var response = _httpClient.SendAsync(messageRequest).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return new DiscordDmSendResult(false, $"Discord message send failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");
            }

            return new DiscordDmSendResult(true);
        }
        catch (Exception ex)
        {
            return new DiscordDmSendResult(false, $"Discord send exception: {ex.Message}");
        }
    }

    private string CreateDmChannel(string recipientUserId)
    {
        var channelRequest = new HttpRequestMessage(HttpMethod.Post, $"{DiscordApiBaseUrl}/users/@me/channels");
        channelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);
        channelRequest.Content = new StringContent(
            JsonSerializer.Serialize(new { recipient_id = recipientUserId }),
            Encoding.UTF8,
            "application/json");

        var response = _httpClient.SendAsync(channelRequest).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException($"Discord DM channel creation failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");
        }

        var responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(responseJson);
        return document.RootElement.GetProperty("id").GetString() ?? string.Empty;
    }
}