using System.Text.Json;
using System.Text.Json.Serialization;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class AdsbParser
{
    public Aircraft Parse(string json)
    {
        var payload = JsonSerializer.Deserialize<AdsbMessage>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Unable to parse ADS-B payload.");

        return new Aircraft(
            payload.Hex ?? string.Empty,
            payload.Flight,
            payload.Lat ?? 0,
            payload.Lon ?? 0,
            payload.AltBaro ?? 0,
            payload.Track ?? 0,
            payload.Speed ?? 0,
            payload.Squawk);
    }

    private sealed class AdsbMessage
    {
        [JsonPropertyName("hex")]
        public string? Hex { get; set; }

        [JsonPropertyName("flight")]
        public string? Flight { get; set; }

        [JsonPropertyName("lat")]
        public double? Lat { get; set; }

        [JsonPropertyName("lon")]
        public double? Lon { get; set; }

        [JsonPropertyName("alt_baro")]
        public int? AltBaro { get; set; }

        [JsonPropertyName("track")]
        public int? Track { get; set; }

        [JsonPropertyName("speed")]
        public int? Speed { get; set; }

        [JsonPropertyName("squawk")]
        public int? Squawk { get; set; }
    }
}
