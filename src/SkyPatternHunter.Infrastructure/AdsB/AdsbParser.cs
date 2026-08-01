using System.Globalization;
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
                GetIntValue(message.AltBaro) ?? 0,
                GetIntValue(message.Track) ?? 0,
            GetIntValue(message.GroundSpeed) ??
                GetIntValue(message.TrueAirSpeed) ??
                GetIntValue(message.IndicatedAirSpeed) ??
                GetIntValue(message.Speed) ?? 0,
                GetIntValue(message.Squawk));
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
        public JsonElement? AltBaro { get; set; }

        [JsonPropertyName("track")]
        public JsonElement? Track { get; set; }

        [JsonPropertyName("speed")]
        public JsonElement? Speed { get; set; }

        [JsonPropertyName("gs")]
        public JsonElement? GroundSpeed { get; set; }

        [JsonPropertyName("tas")]
        public JsonElement? TrueAirSpeed { get; set; }

        [JsonPropertyName("ias")]
        public JsonElement? IndicatedAirSpeed { get; set; }

        [JsonPropertyName("squawk")]
        public JsonElement? Squawk { get; set; }
    }

    private static int? GetIntValue(JsonElement? value)
    {
        if (value is not { } element)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            return (int)Math.Round(number, MidpointRounding.AwayFromZero);
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
