using SkyPatternHunter.Domain.Models;

namespace SkyPatternHunter.Infrastructure.Events;

public interface IOverheadEventStore
{
    void Append(OverheadEvent overheadEvent);

    IReadOnlyList<OverheadEvent> ReadAll();
}