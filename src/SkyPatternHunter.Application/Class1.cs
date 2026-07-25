using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Application.Detection;

public interface IOverheadEventDetector
{
	OverheadEvent? Detect(Aircraft aircraft, double userLatitude, double userLongitude, double thresholdMiles, DateTimeOffset observedAt);
}

public sealed class OverheadEventDetector : IOverheadEventDetector
{
	public OverheadEvent? Detect(Aircraft aircraft, double userLatitude, double userLongitude, double thresholdMiles, DateTimeOffset observedAt)
	{
		if (!aircraft.IsOverhead(userLatitude, userLongitude, thresholdMiles))
		{
			return null;
		}

		return new OverheadEvent(aircraft, observedAt);
	}
}
