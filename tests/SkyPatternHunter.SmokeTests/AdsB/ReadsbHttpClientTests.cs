using System.Net;
using System.Text;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class ReadsbHttpClientTests
{
    [Fact]
    public async Task ReadMessagesAsync_EmitsEachAircraftFromSnapshot()
    {
        const string snapshot = """
        {
          "now": 1,
          "aircraft": [
            { "hex": "A1B2C3", "lat": 28.1, "lon": -81.2 },
            { "hex": "D4E5F6", "lat": 28.2, "lon": -81.3 }
          ]
        }
        """;
        var connectAttempts = 0;
        var connections = 0;
        using var httpClient = new HttpClient(new StaticHttpMessageHandler(snapshot));
        var client = new ReadsbHttpClient(
            new Uri("http://readsb.local/tar1090/data/aircraft.json"),
            httpClient,
            pollInterval: TimeSpan.FromMinutes(1),
            onConnectAttempt: () => connectAttempts++,
            onConnected: () => connections++);
        await using var enumerator = client.ReadMessagesAsync().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Contains("A1B2C3", enumerator.Current, StringComparison.Ordinal);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Contains("D4E5F6", enumerator.Current, StringComparison.Ordinal);
        Assert.Equal(1, connectAttempts);
        Assert.Equal(1, connections);
    }

    private sealed class StaticHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}