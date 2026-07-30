namespace SkyPatternHunter.Infrastructure.Monitoring;

public sealed record RuntimeIngestionSnapshot(
    int ConnectAttemptCount,
    int ConnectedCount,
    int ReconnectScheduledCount,
    int PayloadParsedCount,
    int PayloadParseFailureCount,
    int DetectedEventCount,
    int PersistedEventCount,
    int ClientErrorCount,
    DateTimeOffset? LastActivityAt,
    IReadOnlyList<string> RecentActivity);

public sealed class RuntimeIngestionMonitor
{
    private readonly object _sync = new();
    private readonly Queue<string> _recentActivity = new();
    private const int MaxRecentActivity = 30;

    private int _connectAttemptCount;
    private int _connectedCount;
    private int _reconnectScheduledCount;
    private int _payloadParsedCount;
    private int _payloadParseFailureCount;
    private int _detectedEventCount;
    private int _persistedEventCount;
    private int _clientErrorCount;
    private DateTimeOffset? _lastActivityAt;

    public void RecordConnectAttempt(string host, int port)
    {
        lock (_sync)
        {
            _connectAttemptCount++;
            AddActivity($"CONNECT_ATTEMPT host={host} port={port}");
        }
    }

    public void RecordConnected(string host, int port)
    {
        lock (_sync)
        {
            _connectedCount++;
            AddActivity($"CONNECTED host={host} port={port}");
        }
    }

    public void RecordReconnectScheduled(TimeSpan delay)
    {
        lock (_sync)
        {
            _reconnectScheduledCount++;
            AddActivity($"RECONNECT_SCHEDULED delayMs={delay.TotalMilliseconds:0}");
        }
    }

    public void RecordPayloadParsed(string aircraftHex)
    {
        lock (_sync)
        {
            _payloadParsedCount++;
            AddActivity($"PAYLOAD_PARSED hex={aircraftHex}");
        }
    }

    public void RecordPayloadParseFailure(string reason)
    {
        lock (_sync)
        {
            _payloadParseFailureCount++;
            AddActivity($"PAYLOAD_PARSE_FAILED reason={reason}");
        }
    }

    public void RecordEventDetected(string aircraftHex)
    {
        lock (_sync)
        {
            _detectedEventCount++;
            AddActivity($"EVENT_DETECTED hex={aircraftHex}");
        }
    }

    public void RecordEventPersisted(string aircraftHex)
    {
        lock (_sync)
        {
            _persistedEventCount++;
            AddActivity($"EVENT_PERSISTED hex={aircraftHex}");
        }
    }

    public void RecordClientError(string reason)
    {
        lock (_sync)
        {
            _clientErrorCount++;
            AddActivity($"CLIENT_ERROR reason={reason}");
        }
    }

    public void RecordRunStopped(int processedCount, int detectedEventCount)
    {
        lock (_sync)
        {
            AddActivity($"RUN_STOPPED processed={processedCount} detected={detectedEventCount}");
        }
    }

    public RuntimeIngestionSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new RuntimeIngestionSnapshot(
                _connectAttemptCount,
                _connectedCount,
                _reconnectScheduledCount,
                _payloadParsedCount,
                _payloadParseFailureCount,
                _detectedEventCount,
                _persistedEventCount,
                _clientErrorCount,
                _lastActivityAt,
                _recentActivity.ToArray());
        }
    }

    private void AddActivity(string message)
    {
        _lastActivityAt = DateTimeOffset.UtcNow;
        _recentActivity.Enqueue($"[{_lastActivityAt:O}] {message}");

        while (_recentActivity.Count > MaxRecentActivity)
        {
            _recentActivity.Dequeue();
        }
    }
}