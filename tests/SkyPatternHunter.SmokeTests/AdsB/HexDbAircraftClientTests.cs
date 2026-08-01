using System.Net;
using System.Text;
using SkyPatternHunter.Infrastructure.AdsB;

namespace SkyPatternHunter.SmokeTests.AdsB;

public class HexDbAircraftClientTests
{
    [Fact]
    public async Task GetAircraftAsync_MapsResponseAndCachesNormalizedHex()
    {
        const string response = """
        {
          "Registration": "G-EZBZ",
          "Manufacturer": "Airbus",
          "Type": "A319 111",
          "RegisteredOwners": "easyJet Airline"
        }
        """;
        var handler = new StaticHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = new HexDbAircraftClient(httpClient);

        var aircraft = await client.GetAircraftAsync("4010ee");
        var cachedAircraft = await client.GetAircraftAsync("4010EE");

        Assert.NotNull(aircraft);
        Assert.Equal("G-EZBZ", aircraft.Registration);
        Assert.Equal("Airbus", aircraft.Manufacturer);
        Assert.Equal("A319 111", aircraft.Type);
        Assert.Equal("easyJet Airline", aircraft.RegisteredOwners);
        Assert.Same(aircraft, cachedAircraft);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("https://hexdb.io/api/v1/aircraft/4010EE", handler.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetAircraftAsync_ReturnsNullWithoutCallingApi_ForInvalidHex()
    {
        var handler = new StaticHttpMessageHandler("{}");
        using var httpClient = new HttpClient(handler);
        var client = new HexDbAircraftClient(httpClient);

        var aircraft = await client.GetAircraftAsync("not-a-hex");

        Assert.Null(aircraft);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class StaticHttpMessageHandler(string content) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}