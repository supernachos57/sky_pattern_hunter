using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class ReadsbJsonHttpClient
{
    private readonly Dictionary<string, AircraftSnapshot> _lastKnownByHex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly Uri _aircraftUri;
    private readonly TimeSpan _pollInterval;
    private readonly Action? _onConnectAttempt;
    private readonly Action? _onConnected;
    private readonly Action<string>? _onDisconnected;
    private readonly Action<Exception>? _onError;
    private readonly Action<TimeSpan>? _onReconnectScheduled;

    public ReadsbJsonHttpClient(
        string host,
        int port,
        string path,
        TimeSpan pollInterval,
        Action? onConnectAttempt = null,
        Action? onConnected = null,
        Action<string>? onDisconnected = null,
        Action<Exception>? onError = null,
        Action<TimeSpan>? onReconnectScheduled = null,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host must not be empty.", nameof(host));
        }

        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        _httpClient = httpClient ?? new HttpClient();
        _aircraftUri = new Uri($"http://{host}:{port}{path}");
        _pollInterval = pollInterval;
        _onConnectAttempt = onConnectAttempt;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
        _onError = onError;
        _onReconnectScheduled = onReconnectScheduled;
    }

    public async IAsyncEnumerable<string> ReadMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var initialBackoff = TimeSpan.FromMilliseconds(250);
        var maximumBackoff = TimeSpan.FromSeconds(5);
        var currentBackoff = initialBackoff;

        while (!cancellationToken.IsCancellationRequested)
        {
            List<string>? payloadBatch = null;

            try
            {
                _onConnectAttempt?.Invoke();
                using var response = await _httpClient.GetAsync(_aircraftUri, cancellationToken);
                response.EnsureSuccessStatusCode();
                _onConnected?.Invoke();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(content);
                payloadBatch = new List<string>();

                if (document.RootElement.TryGetProperty("error", out var errorValue))
                {
                    var server = GetString(document.RootElement, "server") ?? "unknown";
                    var error = errorValue.ValueKind == JsonValueKind.String
                        ? errorValue.GetString()
                        : errorValue.GetRawText();

                    throw new InvalidOperationException(
                        $"HTTP endpoint returned an error payload from '{server}': {error}. Verify ReadsbHost/ReadsbPort/ReadsbJsonPath.");
                }

                if (!document.RootElement.TryGetProperty("aircraft", out var aircraftArray))
                {
                    throw new InvalidOperationException(
                        "HTTP endpoint response does not include an 'aircraft' array. Verify ReadsbHost/ReadsbPort/ReadsbJsonPath.");
                }

                if (aircraftArray.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        "HTTP endpoint 'aircraft' property is not an array. Verify ReadsbHost/ReadsbPort/ReadsbJsonPath.");
                }

                if (aircraftArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var aircraft in aircraftArray.EnumerateArray())
                    {
                        if (aircraft.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var hex = GetString(aircraft, "hex");
                        if (string.IsNullOrWhiteSpace(hex))
                        {
                            continue;
                        }

                        if (!_lastKnownByHex.TryGetValue(hex, out var lastKnown))
                        {
                            lastKnown = new AircraftSnapshot();
                        }

                        var flight = GetString(aircraft, "flight") ?? lastKnown.Flight;
                        var lat = GetDouble(aircraft, "lat") ?? lastKnown.Latitude;
                        var lon = GetDouble(aircraft, "lon") ?? lastKnown.Longitude;
                        var altitude = GetInt(aircraft, "alt_baro") ?? lastKnown.Altitude;
                        var track = GetInt(aircraft, "track") ?? lastKnown.Track;
                        var speed = GetInt(aircraft, "gs") ?? GetInt(aircraft, "speed") ?? lastKnown.Speed;
                        var squawk = GetInt(aircraft, "squawk") ?? lastKnown.Squawk;

                        _lastKnownByHex[hex] = new AircraftSnapshot
                        {
                            Flight = flight,
                            Latitude = lat,
                            Longitude = lon,
                            Altitude = altitude,
                            Track = track,
                            Speed = speed,
                            Squawk = squawk
                        };

                        var payload = new
                        {
                            hex,
                            flight,
                            lat,
                            lon,
                            alt_baro = altitude,
                            track,
                            speed,
                            squawk
                        };

                        payloadBatch.Add(JsonSerializer.Serialize(payload));
                    }
                }

                currentBackoff = initialBackoff;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
                _onDisconnected?.Invoke($"HTTP feed error: {ex.Message}");

                _onReconnectScheduled?.Invoke(currentBackoff);
                await Task.Delay(currentBackoff, cancellationToken);
                currentBackoff = TimeSpan.FromMilliseconds(
                    Math.Min(currentBackoff.TotalMilliseconds * 2, maximumBackoff.TotalMilliseconds));
                continue;
            }

            if (payloadBatch is not null)
            {
                foreach (var payload in payloadBatch)
                {
                    yield return payload;
                }
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static double? GetDouble(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt32(out var intNumber))
            {
                return intNumber;
            }

            if (value.TryGetDouble(out var doubleNumber))
            {
                return (int)Math.Round(doubleNumber);
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
            {
                return parsedInt;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
            {
                return (int)Math.Round(parsedDouble);
            }
        }

        return null;
    }

    private sealed class AircraftSnapshot
    {
        public string? Flight { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public int? Altitude { get; init; }
        public int? Track { get; init; }
        public int? Speed { get; init; }
        public int? Squawk { get; init; }
    }
}