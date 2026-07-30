using System.IO;
using System.Text.Json;
using SkyPatternHunter.Application.Detection;
using SkyPatternHunter.Domain.Notifications;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Infrastructure.Logging;
using SkyPatternHunter.Infrastructure.Monitoring;
using SkyPatternHunter.Infrastructure.Notifications;

namespace SkyPatternHunter.Presentation;

public sealed class AdsbStartupHost
{
    private readonly ApplicationSettings _settings;
    private readonly OverheadEventJournal _journal;
    private readonly FileLogger _logger;
    private readonly RuntimeIngestionMonitor _monitor;
    private readonly DiscordNotificationDispatcher? _notificationDispatcher;
    private readonly AdsbParser _parser = new();
    private readonly IOverheadEventDetector _detector = new OverheadEventDetector();

    public AdsbStartupHost(ApplicationSettings settings, OverheadEventJournal journal)
        : this(settings, journal, new FileLogger(settings.LogFilePath), new RuntimeIngestionMonitor())
    {
    }

    public AdsbStartupHost(ApplicationSettings settings, OverheadEventJournal journal, FileLogger logger, RuntimeIngestionMonitor monitor)
    {
        _settings = settings;
        _journal = journal;
        _logger = logger;
        _monitor = monitor;
        _notificationDispatcher = NotificationDispatcherFactory.Create(settings);
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

        return new AdsbStartupHost(settings, new OverheadEventJournal(settings.DataDirectory));
    }

    public async Task<AdsbStartupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information($"ADS-B runtime started host={_settings.ReadsbHost} port={_settings.ReadsbPort}.");

        var client = new AdsbClient(
            _settings.ReadsbHost,
            _settings.ReadsbPort,
            onConnectAttempt: () =>
            {
                _monitor.RecordConnectAttempt(_settings.ReadsbHost, _settings.ReadsbPort);
                _logger.Information($"ADS-B connect attempt host={_settings.ReadsbHost} port={_settings.ReadsbPort}.");
            },
            onConnected: () =>
            {
                _monitor.RecordConnected(_settings.ReadsbHost, _settings.ReadsbPort);
                _logger.Information($"ADS-B connected host={_settings.ReadsbHost} port={_settings.ReadsbPort}.");
            },
            onDisconnected: reason =>
            {
                _logger.Information($"ADS-B disconnected: {reason}");
            },
            onError: ex =>
            {
                _monitor.RecordClientError(ex.Message);
                _logger.Error($"ADS-B client error: {ex.Message}");
            },
            onReconnectScheduled: delay =>
            {
                _monitor.RecordReconnectScheduled(delay);
                _logger.Information($"ADS-B reconnect scheduled in {delay.TotalMilliseconds:0} ms.");
            });

        var processedCount = 0;
        var detectedEventCount = 0;

        try
        {
            await foreach (var payload in client.ReadMessagesAsync(cancellationToken))
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
                _journal.Append(overheadEvent);
                _monitor.RecordEventPersisted(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event persisted hex={overheadEvent.Aircraft.Hex}.");

                if (_notificationDispatcher is not null)
                {
                    var notificationResult = _notificationDispatcher.Dispatch(overheadEvent, observedAt);
                    if (notificationResult.Payload is not null)
                    {
                        _logger.Information($"Discord notification payload created recipient={notificationResult.Payload.RecipientUserId} aircraft={overheadEvent.Aircraft.Hex}.");

                        if (notificationResult.Sent)
                        {
                            _logger.Information($"Discord DM sent recipient={notificationResult.Payload.RecipientUserId} aircraft={overheadEvent.Aircraft.Hex}.");
                        }
                        else if (!string.IsNullOrWhiteSpace(notificationResult.ErrorMessage))
                        {
                            _logger.Error($"Discord DM send skipped/failed aircraft={overheadEvent.Aircraft.Hex}: {notificationResult.ErrorMessage}");
                        }
                    }
                    else if (notificationResult.SuppressedByCooldown)
                    {
                        _logger.Information($"Discord notification suppressed by cooldown aircraft={overheadEvent.Aircraft.Hex}.");
                    }
                }

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
    private readonly OverheadEventJournal _journal;
    private readonly ApplicationSettings _settings;
    private readonly FileLogger _logger;
    private readonly RuntimeIngestionMonitor _monitor;
    private readonly DiscordNotificationDispatcher? _notificationDispatcher;

    public AdsbProcessingPipeline(ApplicationSettings settings, OverheadEventJournal journal)
        : this(settings, journal, new FileLogger(settings.LogFilePath), new RuntimeIngestionMonitor())
    {
    }

    public AdsbProcessingPipeline(ApplicationSettings settings, OverheadEventJournal journal, FileLogger logger, RuntimeIngestionMonitor monitor)
    {
        _settings = settings;
        _journal = journal;
        _logger = logger;
        _monitor = monitor;
        _notificationDispatcher = NotificationDispatcherFactory.Create(settings);
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
                _journal.Append(overheadEvent);
                _monitor.RecordEventPersisted(overheadEvent.Aircraft.Hex);
                _logger.Information($"Overhead event persisted hex={overheadEvent.Aircraft.Hex}.");

                if (_notificationDispatcher is not null)
                {
                    var notificationResult = _notificationDispatcher.Dispatch(overheadEvent, observedAt);
                    if (notificationResult.Payload is not null)
                    {
                        _logger.Information($"Discord notification payload created recipient={notificationResult.Payload.RecipientUserId} aircraft={overheadEvent.Aircraft.Hex}.");

                        if (notificationResult.Sent)
                        {
                            _logger.Information($"Discord DM sent recipient={notificationResult.Payload.RecipientUserId} aircraft={overheadEvent.Aircraft.Hex}.");
                        }
                        else if (!string.IsNullOrWhiteSpace(notificationResult.ErrorMessage))
                        {
                            _logger.Error($"Discord DM send skipped/failed aircraft={overheadEvent.Aircraft.Hex}: {notificationResult.ErrorMessage}");
                        }
                    }
                    else if (notificationResult.SuppressedByCooldown)
                    {
                        _logger.Information($"Discord notification suppressed by cooldown aircraft={overheadEvent.Aircraft.Hex}.");
                    }
                }

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

file static class NotificationDispatcherFactory
{
    public static DiscordNotificationDispatcher? Create(ApplicationSettings settings)
    {
        if (!settings.DiscordNotificationsEnabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordRecipientUserId))
        {
            return null;
        }

        var preferences = new DiscordDmNotificationPreferences(
            Enabled: true,
            Cooldown: TimeSpan.FromSeconds(settings.DiscordNotificationCooldownSeconds),
            MessagePrefix: settings.DiscordNotificationMessagePrefix);

        var botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(botToken))
        {
            botToken = settings.DiscordBotToken;
        }

        IDiscordDmSender? sender = null;
        if (!string.IsNullOrWhiteSpace(botToken))
        {
            sender = new DiscordApiDmSender(botToken);
        }

        return new DiscordNotificationDispatcher(settings.DiscordRecipientUserId, preferences, sender: sender);
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
