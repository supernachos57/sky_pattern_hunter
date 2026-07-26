using SkyPatternHunter.Infrastructure.MachineLearning;

namespace SkyPatternHunter.SmokeTests.MachineLearning;

public class BusyTimeModelPipelineTests
{
    [Fact]
    public void TrainAndSave_CreatesModelArtifact_AndPredictsBusyTime()
    {
        var modelPath = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"), "busyTimeModel.zip");
        var pipeline = new BusyTimeModelPipeline(modelPath);
        var samples = new List<BusyTimeSample>
        {
            new() { HourOfDay = 7, DayOfWeek = 1, IsBusy = true },
            new() { HourOfDay = 8, DayOfWeek = 1, IsBusy = true },
            new() { HourOfDay = 18, DayOfWeek = 2, IsBusy = true },
            new() { HourOfDay = 19, DayOfWeek = 3, IsBusy = true },
            new() { HourOfDay = 2, DayOfWeek = 1, IsBusy = false },
            new() { HourOfDay = 3, DayOfWeek = 5, IsBusy = false },
            new() { HourOfDay = 12, DayOfWeek = 6, IsBusy = false },
            new() { HourOfDay = 14, DayOfWeek = 0, IsBusy = false },
            new() { HourOfDay = 6, DayOfWeek = 1, IsBusy = true },
            new() { HourOfDay = 20, DayOfWeek = 4, IsBusy = true },
            new() { HourOfDay = 1, DayOfWeek = 2, IsBusy = false },
            new() { HourOfDay = 22, DayOfWeek = 6, IsBusy = false }
        };

        pipeline.TrainAndSave(samples);

        var prediction = pipeline.Predict(new BusyTimeSample
        {
            HourOfDay = 18,
            DayOfWeek = 2
        });

        Assert.True(File.Exists(modelPath));
        Assert.True(prediction.Probability is >= 0 and <= 1);
        Assert.True(prediction.PredictedLabel);
    }
}
