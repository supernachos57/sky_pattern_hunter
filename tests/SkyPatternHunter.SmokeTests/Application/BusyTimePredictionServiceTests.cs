using SkyPatternHunter.Application.Prediction;
using SkyPatternHunter.Infrastructure.MachineLearning;

namespace SkyPatternHunter.SmokeTests.Application;

public class BusyTimePredictionServiceTests
{
    [Fact]
    public void Predict_UsesTrainingSamplesToCreateAndReuseModel()
    {
        var modelPath = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"), "busyTimeModel.zip");
        var trainingSamples = new List<BusyTimeSample>
        {
            new() { HourOfDay = 7, DayOfWeek = 1, IsBusy = true },
            new() { HourOfDay = 8, DayOfWeek = 1, IsBusy = true },
            new() { HourOfDay = 18, DayOfWeek = 2, IsBusy = true },
            new() { HourOfDay = 2, DayOfWeek = 1, IsBusy = false },
            new() { HourOfDay = 3, DayOfWeek = 5, IsBusy = false }
        };

        var loggedMessages = new List<string>();
        var service = new BusyTimePredictionService(modelPath, trainingSamples, loggedMessages.Add);

        var prediction = service.Predict(new BusyTimeSample
        {
            HourOfDay = 18,
            DayOfWeek = 2
        });

        Assert.True(File.Exists(modelPath));
        Assert.True(prediction.Probability is >= 0 and <= 1);
        Assert.True(prediction.PredictedLabel);
        Assert.DoesNotContain(loggedMessages, message => message.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }
}
