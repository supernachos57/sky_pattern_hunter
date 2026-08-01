using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class ReadsbHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _snapshotUri;
    private readonly TimeSpan _pollInterval;
    private readonly Action? _onConnectAttempt;
    private readonly Action? _onConnected;
    private readonly Action<Exception>? _onError;
    private readonly Action<TimeSpan>? _onReconnectScheduled;

    public ReadsbHttpClient(
        Uri snapshotUri,
        HttpClient? httpClient = null,
        TimeSpan? pollInterval = null,
        Action? onConnectAttempt = null,
        Action? onConnected = null,
        Action<Exception>? onError = null,
        Action<TimeSpan>? onReconnectScheduled = null)
    {
        _snapshotUri = snapshotUri ?? throw new ArgumentNullException(nameof(snapshotUri));
        _httpClient = httpClient ?? new HttpClient();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _onConnectAttempt = onConnectAttempt;
        _onConnected = onConnected;
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
            List<string> payloads;

            try
            {
                payloads = await FetchSnapshotAsync(cancellationToken);
                currentBackoff = initialBackoff;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
            {
                _onError?.Invoke(ex);
                _onReconnectScheduled?.Invoke(currentBackoff);
                await Task.Delay(currentBackoff, cancellationToken);
                currentBackoff = TimeSpan.FromMilliseconds(
                    Math.Min(currentBackoff.TotalMilliseconds * 2, maximumBackoff.TotalMilliseconds));
                continue;
            }

            foreach (var payload in payloads)
            {
                yield return payload;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    private async Task<List<string>> FetchSnapshotAsync(CancellationToken cancellationToken)
    {
        _onConnectAttempt?.Invoke();

        using var response = await _httpClient.GetAsync(_snapshotUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);

        if (!document.RootElement.TryGetProperty("aircraft", out var aircraft) ||
            aircraft.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("readsb snapshot does not contain an aircraft array.");
        }

        _onConnected?.Invoke();
        return aircraft.EnumerateArray().Select(item => item.GetRawText()).ToList();
    }
}