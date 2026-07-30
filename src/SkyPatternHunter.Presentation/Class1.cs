using System.IO;
using System.Text.Json;
using SkyPatternHunter.Application.Detection;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Infrastructure.Logging;

namespace SkyPatternHunter.Presentation;

public sealed class AdsbStartupHost
{
    private readonly ApplicationSettings _settings;
    private readonly OverheadEventJournal _journal;
    private readonly AdsbParser _parser = new();
    private readonly IOverheadEventDetector _detector = new OverheadEventDetector();

    public AdsbStartupHost(ApplicationSettings settings, OverheadEventJournal journal)
    {
        _settings = settings;
        _journal = journal;
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
        var client = new AdsbClient(_settings.ReadsbHost, _settings.ReadsbPort);
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

        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    public async Task<AdsbStartupResult> ProcessPayloadsAsync(IEnumerable<string> payloads, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payloads);

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
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    private bool TryProcessPayload(string payload, DateTimeOffset observedAt, out bool eventDetected)
    {
        eventDetected = false;

        try
        {
            var aircraft = _parser.Parse(payload);

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                observedAt);

            if (overheadEvent is not null)
            {
                _journal.Append(overheadEvent);
                eventDetected = true;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
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

    public AdsbProcessingPipeline(ApplicationSettings settings, OverheadEventJournal journal)
    {
        _settings = settings;
        _journal = journal;
    }

    public async Task<AdsbStartupResult> ProcessAsync(IEnumerable<string> payloads, CancellationToken cancellationToken = default)
    {
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
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }

    private bool TryProcessPayload(string payload, DateTimeOffset observedAt, out bool eventDetected)
    {
        eventDetected = false;

        try
        {
            var aircraft = _parser.Parse(payload);

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                observedAt);

            if (overheadEvent is not null)
            {
                _journal.Append(overheadEvent);
                eventDetected = true;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
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
