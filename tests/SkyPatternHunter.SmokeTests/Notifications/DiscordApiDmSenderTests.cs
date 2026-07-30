using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SkyPatternHunter.Domain.Notifications;
using SkyPatternHunter.Infrastructure.Notifications;

namespace SkyPatternHunter.SmokeTests.Notifications;

public class DiscordApiDmSenderTests
{
    [Fact]
    public async Task Send_CreatesDmChannelAndPostsMessage()
    {
        var handler = new RecordingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var sender = new DiscordApiDmSender("bot-token", httpClient);

        var payload = new DiscordDmNotificationPayload("464232557710540800", "Sky Pattern Hunter test message");
        var result = sender.Send(payload);

        Assert.True(result.Sent);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, handler.Requests.Count);

        var createChannelRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, createChannelRequest.Method);
        Assert.EndsWith("/users/@me/channels", createChannelRequest.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("Bot", createChannelRequest.Headers.Authorization?.Scheme);
        Assert.Equal("bot-token", createChannelRequest.Headers.Authorization?.Parameter);

        var createBody = await createChannelRequest.Content!.ReadAsStringAsync();
        Assert.Contains("recipient_id", createBody, StringComparison.Ordinal);
        Assert.Contains(payload.RecipientUserId, createBody, StringComparison.Ordinal);

        var sendMessageRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, sendMessageRequest.Method);
        Assert.EndsWith("/channels/123456789/messages", sendMessageRequest.RequestUri!.AbsoluteUri, StringComparison.Ordinal);

        var sendBody = await sendMessageRequest.Content!.ReadAsStringAsync();
        Assert.Contains(payload.Content, sendBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Send_ReturnsFailure_WhenDiscordReturnsError()
    {
        var handler = new RecordingHttpMessageHandler(returnMessageFailure: true);
        var httpClient = new HttpClient(handler);
        var sender = new DiscordApiDmSender("bot-token", httpClient);

        var payload = new DiscordDmNotificationPayload("464232557710540800", "Sky Pattern Hunter test message");
        var result = sender.Send(payload);

        Assert.False(result.Sent);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly bool _returnMessageFailure;

        public RecordingHttpMessageHandler(bool returnMessageFailure = false)
        {
            _returnMessageFailure = returnMessageFailure;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = Clone(request);
            Requests.Add(clone);

            if (request.RequestUri is not null && request.RequestUri.AbsoluteUri.EndsWith("/users/@me/channels", StringComparison.Ordinal))
            {
                var success = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"123456789\"}", Encoding.UTF8, "application/json")
                };

                return Task.FromResult(success);
            }

            if (_returnMessageFailure)
            {
                var failed = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    ReasonPhrase = "Bad Request",
                    Content = new StringContent("{\"message\":\"bad payload\"}", Encoding.UTF8, "application/json")
                };

                return Task.FromResult(failed);
            }

            var sent = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"999\"}", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(sent);
        }

        private static HttpRequestMessage Clone(HttpRequestMessage source)
        {
            var clone = new HttpRequestMessage(source.Method, source.RequestUri)
            {
                Content = source.Content is null
                    ? null
                    : new StringContent(source.Content.ReadAsStringAsync().GetAwaiter().GetResult(), Encoding.UTF8, source.Content.Headers.ContentType?.MediaType ?? "application/json")
            };

            foreach (var header in source.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (clone.Content is not null)
            {
                foreach (var header in source.Content!.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }
}
