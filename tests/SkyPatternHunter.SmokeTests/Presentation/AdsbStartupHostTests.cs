using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Presentation;

namespace SkyPatternHunter.SmokeTests.Presentation;

public class AdsbStartupHostTests
{
    [Fact]
    public async Task ProcessPayloadsAsync_SkipsMalformedPayloadsAndContinues()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var settings = new ApplicationSettings
            {
                DataDirectory = tempDirectory,
                UserLatitude = 0,
                UserLongitude = 0,
                DetectionThresholdMiles = 25
            };

            var journal = new OverheadEventJournal(settings.DataDirectory);
            var pipeline = new AdsbProcessingPipeline(settings, journal);

            var result = await pipeline.ProcessAsync(new[]
            {
                "not-json",
                "",
                "   ",
                """
                {"hex":"A1B2C3","lat":0,"lon":0,"alt_baro":32000,"track":270,"speed":450}
                """
            });

            Assert.Equal(1, result.ProcessedCount);
            Assert.Equal(1, result.DetectedEventCount);

            var persistedEvents = journal.ReadAll();
            Assert.Single(persistedEvents);
            Assert.Equal("A1B2C3", persistedEvents.Single().Aircraft.Hex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessPayloadsAsync_ParsesAndPersistsOverheadEvent()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var settings = new ApplicationSettings
            {
                DataDirectory = tempDirectory,
                UserLatitude = 0,
                UserLongitude = 0,
                DetectionThresholdMiles = 25
            };

            var journal = new OverheadEventJournal(settings.DataDirectory);
            var pipeline = new AdsbProcessingPipeline(settings, journal);

            var result = await pipeline.ProcessAsync(new[]
            {
                """
                {"hex":"A1B2C3","lat":0,"lon":0,"alt_baro":32000,"track":270,"speed":450}
                """
            });

            Assert.Equal(1, result.ProcessedCount);
            Assert.Equal(1, result.DetectedEventCount);

            var persistedEvents = journal.ReadAll();
            Assert.Single(persistedEvents);
            Assert.Equal("A1B2C3", persistedEvents.Single().Aircraft.Hex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessAsync_RecordsLifecycleLogsAndMonitoringSummary()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "logs", "runtime.log");

        try
        {
            var settings = new ApplicationSettings
            {
                DataDirectory = tempDirectory,
                LogFilePath = logPath,
                UserLatitude = 0,
                UserLongitude = 0,
                DetectionThresholdMiles = 25
            };

            var journal = new OverheadEventJournal(settings.DataDirectory);
            var pipeline = new AdsbProcessingPipeline(settings, journal);

            var result = await pipeline.ProcessAsync(new[]
            {
                "not-json",
                """
                {"hex":"A1B2C3","lat":0,"lon":0,"alt_baro":32000,"track":270,"speed":450}
                """
            });

            Assert.Equal(1, result.ProcessedCount);
            Assert.Equal(1, result.DetectedEventCount);

            var monitoringSnapshot = pipeline.GetMonitoringSnapshot();
            Assert.Equal(1, monitoringSnapshot.PayloadParsedCount);
            Assert.Equal(1, monitoringSnapshot.PayloadParseFailureCount);
            Assert.Equal(1, monitoringSnapshot.DetectedEventCount);
            Assert.Equal(1, monitoringSnapshot.PersistedEventCount);
            Assert.NotEmpty(monitoringSnapshot.RecentActivity);

            Assert.True(File.Exists(logPath));
            var logOutput = await File.ReadAllTextAsync(logPath);
            Assert.Contains("Processing pipeline started", logOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Payload parse failure", logOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Overhead event persisted", logOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Processing pipeline stopped", logOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessAsync_CreatesNotificationPayload_AndSuppressesRepeatedEventByCooldown()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "logs", "runtime.log");

        try
        {
            var settings = new ApplicationSettings
            {
                DataDirectory = tempDirectory,
                LogFilePath = logPath,
                UserLatitude = 0,
                UserLongitude = 0,
                DetectionThresholdMiles = 25,
                DiscordNotificationsEnabled = true,
                DiscordRecipientUserId = "1234567890",
                DiscordNotificationCooldownSeconds = 300,
                DiscordNotificationMessagePrefix = "Sky Pattern Hunter"
            };

            var journal = new OverheadEventJournal(settings.DataDirectory);
            var pipeline = new AdsbProcessingPipeline(settings, journal);

            _ = await pipeline.ProcessAsync(new[]
            {
                """
                {"hex":"A1B2C3","lat":0,"lon":0,"alt_baro":32000,"track":270,"speed":450}
                """,
                """
                {"hex":"A1B2C3","lat":0,"lon":0,"alt_baro":32000,"track":270,"speed":450}
                """
            });

            Assert.True(File.Exists(logPath));
            var logOutput = await File.ReadAllTextAsync(logPath);
            Assert.Contains("Discord notification payload created", logOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Discord notification suppressed by cooldown", logOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
