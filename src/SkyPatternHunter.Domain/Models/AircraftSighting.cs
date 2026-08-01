namespace SkyPatternHunter.Domain.Models;

public class AircraftSighting
{
    public AircraftSighting(Aircraft aircraft, DateTimeOffset observedAt, bool isOverhead)
    {
        Aircraft = aircraft;
        ObservedAt = observedAt;
        IsOverhead = isOverhead;
    }

    public Aircraft Aircraft { get; }
    public DateTimeOffset ObservedAt { get; }
    public bool IsOverhead { get; }
}