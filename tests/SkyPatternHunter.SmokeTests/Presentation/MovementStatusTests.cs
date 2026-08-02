using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Presentation;

namespace SkyPatternHunter.SmokeTests.Presentation;

public class MovementStatusTests
{
    [Fact]
    public void Refresh_AnnotatesAircraftMovementDirectionFromObservedHistory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "sky-pattern-hunter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var journal = new OverheadEventJournal(tempDirectory);
            journal.Append(new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 28.45365, -81.08718, 30000, 90, 100, null),
                DateTimeOffset.Parse("2026-07-29T12:00:00+00:00")));
            journal.Append(new OverheadEvent(
                new Aircraft("AAAA01", "FLT1", 28.45375, -81.08718, 31000, 95, 105, null),
                DateTimeOffset.Parse("2026-07-29T12:01:00+00:00")));

            var snapshot = new LiveEventDashboard(
                journal,
                staleAfter: TimeSpan.FromMinutes(10),
                clock: () => DateTimeOffset.Parse("2026-07-29T12:02:00+00:00")).Refresh();

            var eventVm = Assert.Single(snapshot.TodayEvents);
            Assert.Equal("Coming", eventVm.DirectionText);
            Assert.Equal("Ascending", eventVm.ClimbText);
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
