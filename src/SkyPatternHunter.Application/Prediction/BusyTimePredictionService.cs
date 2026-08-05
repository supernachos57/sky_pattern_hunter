using SkyPatternHunter.Infrastructure.MachineLearning;

namespace SkyPatternHunter.Application.Prediction;

public sealed class BusyTimePredictionService
{
    private readonly BusyTimeModelPipeline _pipeline;
    private readonly Action<string>? _log;

    public BusyTimePredictionService(string modelPath, IEnumerable<BusyTimeSample>? trainingSamples = null, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        _log = log;
        _pipeline = new BusyTimeModelPipeline(modelPath);

        if (trainingSamples is not null)
        {
            try
            {
                _pipeline.TrainAndSave(trainingSamples);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Prediction setup failed: {ex.Message}");
                throw;
            }
        }
    }

    public BusyTimePrediction Predict(BusyTimeSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        try
        {
            return _pipeline.Predict(sample);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Prediction failed: {ex.Message}");
            throw;
        }
    }
}
