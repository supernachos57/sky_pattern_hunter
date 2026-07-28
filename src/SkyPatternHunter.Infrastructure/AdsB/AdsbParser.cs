using System.Text.Json;
using System.Text.Json.Serialization;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class AdsbParser
{
    public Aircraft Parse(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var trimmedPayload = payload.Trim();
        if (LooksLikeRawAdsbMessage(trimmedPayload))
        {
            var normalizedHex = trimmedPayload.Trim('*', ';');
            return new Aircraft(normalizedHex, null, 0, 0, 0, 0, 0, null);
        }

        var message = JsonSerializer.Deserialize<AdsbMessage>(trimmedPayload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Unable to parse ADS-B payload.");

        return new Aircraft(
            message.Hex ?? string.Empty,
            message.Flight,
            message.Lat ?? 0,
            message.Lon ?? 0,
            message.AltBaro ?? 0,
            message.Track ?? 0,
            message.Speed ?? 0,
            message.Squawk);
    }

    private static bool LooksLikeRawAdsbMessage(string payload)
    {
        if (payload.StartsWith("*", StringComparison.Ordinal) && payload.EndsWith(";", StringComparison.Ordinal))
        {
            return true;
        }

        return payload.All(char.IsAsciiHexDigit);
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
