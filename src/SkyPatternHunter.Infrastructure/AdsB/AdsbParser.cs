using System.Text.Json;
using System.Text.Json.Serialization;
using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class AdsbParser
{
    private readonly RawAdsbFrameDecoder _rawFrameDecoder = new();

    public Aircraft Parse(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var trimmedPayload = payload.Trim();
        if (LooksLikeRawAdsbMessage(trimmedPayload))
        {
            var normalizedHex = trimmedPayload.Trim('*', ';').ToUpperInvariant();
            var decodedFrame = _rawFrameDecoder.Decode(normalizedHex, DateTimeOffset.UtcNow);

            return new Aircraft(
                decodedFrame.Hex,
                decodedFrame.Flight,
                decodedFrame.Latitude ?? 0,
                decodedFrame.Longitude ?? 0,
                decodedFrame.Altitude ?? 0,
                decodedFrame.Track ?? 0,
                decodedFrame.Speed ?? 0,
                decodedFrame.Squawk);
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
        return payload.StartsWith("*", StringComparison.Ordinal) && payload.EndsWith(";", StringComparison.Ordinal);
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
