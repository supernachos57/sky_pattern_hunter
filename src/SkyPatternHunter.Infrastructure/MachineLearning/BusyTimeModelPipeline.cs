using Microsoft.ML;

namespace SkyPatternHunter.Infrastructure.MachineLearning;

public sealed class BusyTimeModelPipeline
{
	private const string FeatureColumnName = "Features";
	private readonly MLContext _mlContext;

	public BusyTimeModelPipeline(string modelPath)
	{
		if (string.IsNullOrWhiteSpace(modelPath))
		{
			throw new ArgumentException("A model path is required.", nameof(modelPath));
		}

		ModelPath = modelPath;
		_mlContext = new MLContext(seed: 42);
	}

	public string ModelPath { get; }

	public void TrainAndSave(IEnumerable<BusyTimeSample> samples)
	{
		ArgumentNullException.ThrowIfNull(samples);

		var trainingData = samples.ToList();
		if (trainingData.Count == 0)
		{
			throw new ArgumentException("At least one training sample is required.", nameof(samples));
		}

		var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
		var pipeline = _mlContext.Transforms.Concatenate(
				FeatureColumnName,
				nameof(BusyTimeSample.HourOfDay),
				nameof(BusyTimeSample.DayOfWeek))
			.Append(_mlContext.Transforms.NormalizeMinMax(FeatureColumnName))
			.Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
				labelColumnName: nameof(BusyTimeSample.IsBusy),
				featureColumnName: FeatureColumnName));

		var model = pipeline.Fit(dataView);
		var directory = Path.GetDirectoryName(ModelPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		_mlContext.Model.Save(model, dataView.Schema, ModelPath);
	}

	public BusyTimePrediction Predict(BusyTimeSample sample)
	{
		ArgumentNullException.ThrowIfNull(sample);

		var model = LoadModel();
		var engine = _mlContext.Model.CreatePredictionEngine<BusyTimeSample, BusyTimePrediction>(model);
		return engine.Predict(sample);
	}

	private ITransformer LoadModel()
	{
		if (!File.Exists(ModelPath))
		{
			throw new FileNotFoundException("The busy-time model has not been trained yet.", ModelPath);
		}

		using var modelStream = File.OpenRead(ModelPath);
		return _mlContext.Model.Load(modelStream, out _);
	}
}

public sealed class BusyTimeSample
{
	public float HourOfDay { get; set; }

	public float DayOfWeek { get; set; }

	public bool IsBusy { get; set; }
}

public sealed class BusyTimePrediction
{
	public bool PredictedLabel { get; set; }

	public float Probability { get; set; }

	public float Score { get; set; }
}
