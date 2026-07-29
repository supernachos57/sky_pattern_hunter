using System.IO;
using SkyPatternHunter.Application.Detection;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;

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

        var dataDirectory = string.IsNullOrWhiteSpace(settings.DataDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : settings.DataDirectory;

        return new AdsbStartupHost(settings, new OverheadEventJournal(dataDirectory));
    }

    public async Task<AdsbStartupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var client = new AdsbClient(_settings.ReadsbHost, _settings.ReadsbPort);
        var messages = await client.ReadMessagesAsync(cancellationToken);
        return await ProcessPayloadsAsync(messages, cancellationToken);
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

            var aircraft = _parser.Parse(payload);
            processedCount++;

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                DateTimeOffset.UtcNow);

            if (overheadEvent is not null)
            {
                _journal.Append(overheadEvent);
                detectedEventCount++;
            }
        }

        await Task.CompletedTask;
        return new AdsbStartupResult(processedCount, detectedEventCount);
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

            var aircraft = _parser.Parse(payload);
            processedCount++;

            var overheadEvent = _detector.Detect(
                aircraft,
                _settings.UserLatitude,
                _settings.UserLongitude,
                _settings.DetectionThresholdMiles,
                DateTimeOffset.UtcNow);

            if (overheadEvent is not null)
            {
                _journal.Append(overheadEvent);
                detectedEventCount++;
            }
        }

        await Task.CompletedTask;
        return new AdsbStartupResult(processedCount, detectedEventCount);
    }
}

public sealed record AdsbStartupResult(int ProcessedCount, int DetectedEventCount);
