using System.Net;
using System.Text;
using System.Text.Json;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class ReadsbJsonHttpClientTests
{
  [Fact]
  public async Task ReadMessagesAsync_ReportsAuthenticationPayloadAsError()
  {
    var responses = new Queue<string>(new[]
    {
      "{\"server\":\"iSpy\",\"error\":\"Authentication failed\",\"errorType\":\"authentication\"}",
      "{\"aircraft\":[]}"
    });

    Exception? capturedError = null;
    var httpClient = new HttpClient(new QueueHttpMessageHandler(responses));
    var client = new ReadsbJsonHttpClient(
      "127.0.0.1",
      8080,
      "/data/aircraft.json",
      TimeSpan.FromMilliseconds(1),
      onError: ex => capturedError = ex,
      httpClient: httpClient);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

    try
    {
      await foreach (var _ in client.ReadMessagesAsync(cancellation.Token))
      {
      }
    }
    catch (OperationCanceledException)
    {
      // Expected: stop the infinite polling loop after enough time for first error handling.
    }

    Assert.NotNull(capturedError);
    Assert.Contains("Authentication failed", capturedError!.Message);
    Assert.Contains("iSpy", capturedError.Message);
  }

    [Fact]
    public async Task ReadMessagesAsync_BackfillsMissingFields_FromLastKnownSnapshot()
    {
        var responses = new Queue<string>(new[]
        {
            """
            {
              "aircraft": [
                {
                  "hex": "A1B2C3",
                  "flight": "DAL123",
                  "lat": 28.12,
                  "lon": -81.23,
                  "alt_baro": 32000,
                  "track": 270,
                  "gs": 450,
                  "squawk": "1234"
                }
              ]
            }
            """,
            """
            {
              "aircraft": [
                {
                  "hex": "A1B2C3",
                  "lat": 28.13,
                  "lon": -81.24
                }
              ]
            }
            """
        });

        var httpClient = new HttpClient(new QueueHttpMessageHandler(responses));
        var client = new ReadsbJsonHttpClient(
            "127.0.0.1",
            8080,
            "/data/aircraft.json",
            TimeSpan.FromMilliseconds(1),
            httpClient: httpClient);

        var received = new List<string>();

        await foreach (var payload in client.ReadMessagesAsync())
        {
            received.Add(payload);
            if (received.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(2, received.Count);

        using var first = JsonDocument.Parse(received[0]);
        using var second = JsonDocument.Parse(received[1]);

        Assert.Equal("DAL123", first.RootElement.GetProperty("flight").GetString());
        Assert.Equal(32000, first.RootElement.GetProperty("alt_baro").GetInt32());
        Assert.Equal(450, first.RootElement.GetProperty("speed").GetInt32());

        // Second snapshot omitted several fields, so last known values should be retained.
        Assert.Equal("DAL123", second.RootElement.GetProperty("flight").GetString());
        Assert.Equal(32000, second.RootElement.GetProperty("alt_baro").GetInt32());
        Assert.Equal(450, second.RootElement.GetProperty("speed").GetInt32());
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueueHttpMessageHandler(Queue<string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _responses.Count > 0 ? _responses.Dequeue() : "{\"aircraft\":[]}";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
