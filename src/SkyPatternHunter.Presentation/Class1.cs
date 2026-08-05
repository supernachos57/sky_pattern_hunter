using System.IO;
using System.Text.Json;
using SkyPatternHunter.Application.Detection;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Infrastructure.Logging;
using SkyPatternHunter.Infrastructure.Monitoring;

namespace SkyPatternHunter.Presentation;

public sealed class AdsbStartupHost
{
    private readonly ApplicationSettings _settings;
    private readonly IOverheadEventStore _eventStore;
    private readonly FileLogger _logger;
    private readonly RuntimeIngestionMonitor _monitor;
    private readonly AdsbParser _parser = new();
    private readonly IOverheadEventDetector _detector = new OverheadEventDetector();

    public AdsbStartupHost(ApplicationSettings settings, IOverheadEventStore eventStore)
        : this(settings, eventStore, new FileLogger(settings.LogFilePath), new RuntimeIngestionMonitor())
    {
    }

    public AdsbStartupHost(ApplicationSettings settings, IOverheadEventStore eventStore, FileLogger logger, RuntimeIngestionMonitor monitor)
    {
        _settings = settings;
        _eventStore = eventStore;
        _logger = logger;
        _monitor = monitor;
    }

    public static AdsbStartupHost CreateFromConfiguration(string? configPath = null)
    {
        var configurationService = new JsonConfigurationService(configPath);
        var settings = configurationService.GetSettings();

        if (settings.DataDirectory is null)
        {
            settings.DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        }

        var validation = ApplicationSettingsValidator.Validate(settings);
        if (!validation.IsValid)
        {
            var message = "Application startup configuration is invalid: " + string.Join(" ", validation.Errors);
            new FileLogger().Error(message);
            throw new InvalidOperationException(message);
        }

        return new AdsbStartupHost(settings, CreateEventStore(settings));
    }

    private static IOverheadEventStore CreateEventStore(ApplicationSettings settings)
    {
        settings.AircraftDataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyPatternHunter",
            "data");
        var aircraftDatabase = new AircraftDatabaseManager(
            Path.Combine(AppContext.BaseDirectory, "data", "aircraft.csv.gz"),
            settings.AircraftDataDirectory,
            settings.AircraftDatabaseSourceUrl);
        _ = aircraftDatabase.EnsureDatabase();

        return new SqliteFlightHistoryStore(
            settings.DataDirectory!,
            settings.HistoryDays,
            TimeSpan.FromSeconds(settings.FlightHistorySampleSeconds),
            aircraftLookup: new SqliteAircraftLookup(aircraftDatabase.DatabasePath));
    }

    public async Task<AdsbStartupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var sourceUri = Uri.TryCreate(_settings.ReadsbJsonUrl, UriKind.Absolute, out var configuredUri)
            ? configuredUri
            : null;
        var sourceHost = sourceUri?.Host ?? _settings.ReadsbHost;
        var sourcePort = sourceUri?.IsDefaultPort == true
            ? sourceUri.Scheme == Uri.UriSchemeHttps ? 443 : 80
            : sourceUri?.Port ?? _settings.ReadsbPort;

        _logger.Information($"ADS-B runtime started host={sourceHost} port={sourcePort}.");

        Action onConnectAttempt = () =>
        {
            _monitor.RecordConnectAttempt(sourceHost, sourcePort);
            _logger.Information($"ADS-B connect attempt host={sourceHost} port={sourcePort}.");
        };
        Action onConnected = () =>
        {
            _monitor.RecordConnected(sourceHost, sourcePort);
            _logger.Information($"ADS-B connected host={sourceHost} port={sourcePort}.");
        };
        Action<Exception> onError = ex =>
        {
            _monitor.RecordClientError(ex.Message);
            _logger.Error($"ADS-B client error: {ex.Message}");
        };
        Action<TimeSpan> onReconnectScheduled = delay =>
        {
            _monitor.RecordReconnectScheduled(delay);
            _logger.Information($"ADS-B reconnect scheduled in {delay.TotalMilliseconds:0} ms.");
        };

        IAsyncEnumerable<string> payloads = sourceUri is null
            ? new AdsbClient(
                sourceHost,
                sourcePort,
                onConnectAttempt,
                onConnected,
                onDisconnected: reason => _logger.Information($"ADS-B disconnected: {reason}"),
                onError,
                onReconnectScheduled).ReadMessagesAsync(cancellationToken)
            : new ReadsbHttpClient(
                sourceUri,
                onConnectAttempt: onConnectAttempt,
                onConnected: onConnected,
                onError: onError,
                onReconnectScheduled: onReconnectScheduled).ReadMessagesAsync(cancellationToken);

        var processedCount = 0;
        var detectedEventCount = 0;

        try
        {
            await foreach (var payload in payloads)
            {
                if (TryProcessPayload(payload, DateTimeOffset.UtcNow, out var eventDetected))
                {
                    processedCount++;

                    if (eventDetected)
                    {
                        detectedEventCount++;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        _monitor.RecordRunStopped(processedCount, detectedEventCount);
        _logger.Information($"ADS-B runtime stopped processed={processedCount} detected={detectedEventCount}.");
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    public async Task<AdsbStartupResult> ProcessPayloadsAsync(IEnumerable<string> payloads, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        _logger.Information("Batch payload processing started.");

        var processedCount = 0;
        var detectedEventCount = 0;

        foreach (var payload in payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            if (TryProcessPayload(payload, DateTimeOffset.UtcNow, out var eventDetected))
            {
                processedCount++;

                if (eventDetected)
                {
                    detectedEventCount++;
                }
            }
        }

        await Task.CompletedTask;
        _monitor.RecordRunStopped(processedCount, detectedEventCount);
        _logger.Information($"Batch payload processing completed processed={processedCount} detected={detectedEventCount}.");
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    public RuntimeIngestionSnapshot GetMonitoringSnapshot() => _monitor.GetSnapshot();

    private bool TryProcessPayload(string payload, DateTimeOffset observedAt, out bool eventDetected)
    {
        eventDetected = false;

        try
        {
            var aircraft = _parser.Parse(payload);
            _monitor.RecordPayloadParsed(aircraft.Hex);
            _logger.Information($"Payload parsed hex={aircraft.Hex}.");

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                observedAt);

            if (overheadEvent is not null)
            {
                _monitor.RecordEventDetected(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event detected hex={overheadEvent.Aircraft.Hex}.");
                _eventStore.Append(overheadEvent);
                _monitor.RecordEventPersisted(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event persisted hex={overheadEvent.Aircraft.Hex}.");
                eventDetected = true;
            }

            return true;
        }
        catch (JsonException ex)
        {
            _monitor.RecordPayloadParseFailure(ex.Message);
            _logger.Error($"Payload parse failure: {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _monitor.RecordPayloadParseFailure(ex.Message);
            _logger.Error($"Payload parse failure: {ex.Message}");
            return false;
        }
    }
}

public sealed class AdsbProcessingPipeline
{
    private readonly AdsbParser _parser = new();
    private readonly IOverheadEventDetector _detector = new OverheadEventDetector();
    private readonly IOverheadEventStore _eventStore;
    private readonly ApplicationSettings _settings;
    private readonly FileLogger _logger;
    private readonly RuntimeIngestionMonitor _monitor;

    public AdsbProcessingPipeline(ApplicationSettings settings, IOverheadEventStore eventStore)
        : this(settings, eventStore, new FileLogger(settings.LogFilePath), new RuntimeIngestionMonitor())
    {
    }

    public AdsbProcessingPipeline(ApplicationSettings settings, IOverheadEventStore eventStore, FileLogger logger, RuntimeIngestionMonitor monitor)
    {
        _settings = settings;
        _eventStore = eventStore;
        _logger = logger;
        _monitor = monitor;
    }

    public async Task<AdsbStartupResult> ProcessAsync(IEnumerable<string> payloads, CancellationToken cancellationToken = default)
    {
        _logger.Information("Processing pipeline started.");

        var processedCount = 0;
        var detectedEventCount = 0;

        foreach (var payload in payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            if (TryProcessPayload(payload, DateTimeOffset.UtcNow, out var eventDetected))
            {
                processedCount++;

                if (eventDetected)
                {
                    detectedEventCount++;
                }
            }
        }

        await Task.CompletedTask;
        _monitor.RecordRunStopped(processedCount, detectedEventCount);
        _logger.Information($"Processing pipeline stopped processed={processedCount} detected={detectedEventCount}.");
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    public RuntimeIngestionSnapshot GetMonitoringSnapshot() => _monitor.GetSnapshot();

    private bool TryProcessPayload(string payload, DateTimeOffset observedAt, out bool eventDetected)
    {
        eventDetected = false;

        try
        {
            var aircraft = _parser.Parse(payload);
            _monitor.RecordPayloadParsed(aircraft.Hex);
            _logger.Information($"Payload parsed hex={aircraft.Hex}.");

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                observedAt);

            if (overheadEvent is not null)
            {
                _monitor.RecordEventDetected(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event detected hex={overheadEvent.Aircraft.Hex}.");
                _eventStore.Append(overheadEvent);
                _monitor.RecordEventPersisted(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event persisted hex={overheadEvent.Aircraft.Hex}.");
                eventDetected = true;
            }

            return true;
        }
        catch (JsonException ex)
        {
            _monitor.RecordPayloadParseFailure(ex.Message);
            _logger.Error($"Payload parse failure: {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _monitor.RecordPayloadParseFailure(ex.Message);
            _logger.Error($"Payload parse failure: {ex.Message}");
            return false;
        }
    }
}

public sealed record AdsbStartupResult(int ProcessedCount, int DetectedEventCount);

public sealed record StartupValidationResult(bool IsValid, int ExitCode, string? ErrorMessage)
{
    public static StartupValidationResult Success() => new(true, 0, null);

    public static StartupValidationResult Failure(string message) => new(false, 1, message);
}

public static class StartupConfigurationValidator
{
    public static StartupValidationResult ValidateAtStartup(string? configPath = null)
    {
        try
        {
            _ = AdsbStartupHost.CreateFromConfiguration(configPath);
            return StartupValidationResult.Success();
        }
        catch (InvalidOperationException ex)
        {
            return StartupValidationResult.Failure(ex.Message);
        }
    }
}
