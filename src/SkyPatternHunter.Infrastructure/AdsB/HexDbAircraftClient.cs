using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed record HexDbAircraft(string? Registration, string? Manufacturer, string? Type, string? RegisteredOwners);

public sealed class HexDbAircraftClient
{
    private static readonly Uri BaseUri = new("https://hexdb.io/api/v1/aircraft/");
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Task<HexDbAircraft?>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lookupThrottle = new(4, 4);

    public HexDbAircraftClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HexDbAircraft?> GetAircraftAsync(string hex)
    {
        var normalizedHex = hex.Trim().ToUpperInvariant();
        if (normalizedHex.Length != 6 || normalizedHex.Any(character => !Uri.IsHexDigit(character)))
        {
            return Task.FromResult<HexDbAircraft?>(null);
        }

        return _cache.GetOrAdd(normalizedHex, LookupAircraftAsync);
    }

    private async Task<HexDbAircraft?> LookupAircraftAsync(string hex)
    {
        await _lookupThrottle.WaitAsync();

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(BaseUri, hex));
            if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<HexDbAircraft>(responseStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            _lookupThrottle.Release();
        }
    }
}