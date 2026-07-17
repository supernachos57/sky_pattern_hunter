using System;

namespace SkyPatternHunter.Domain.Models;

public class OverheadEvent
{
    public OverheadEvent(Aircraft aircraft, DateTimeOffset observedAt)
    {
        Aircraft = aircraft;
        ObservedAt = observedAt;
    }

    public Aircraft Aircraft { get; }
    public DateTimeOffset ObservedAt { get; }
}
