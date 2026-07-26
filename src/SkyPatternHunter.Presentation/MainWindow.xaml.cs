using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.Presentation;

public partial class MainWindow
{
    public ObservableCollection<RecentEventViewModel> Events { get; } = new();

    public int EventCount { get; private set; }

    public string LatestAircraftHex { get; private set; } = "None";

    public string LatestObservedAtText { get; private set; } = "No events yet";

    public MainWindow()
    {
        InitializeComponent();
        LoadEvents();
        DataContext = this;
    }

    private void LoadEvents()
    {
        var journal = new OverheadEventJournal(Path.Combine(AppContext.BaseDirectory, "data"));
        var events = journal.ReadAll();
        var latestEvent = events.LastOrDefault();

        Events.Clear();
        foreach (var overheadEvent in events.OrderByDescending(item => item.ObservedAt).Take(25))
        {
            Events.Add(RecentEventViewModel.FromEvent(overheadEvent));
        }

        EventCount = events.Count;
        LatestAircraftHex = latestEvent?.Aircraft.Hex ?? "None";
        LatestObservedAtText = latestEvent?.ObservedAt.ToString("O", CultureInfo.InvariantCulture) ?? "No events yet";
    }
}

public sealed record RecentEventViewModel(string AircraftHex, string? Flight, string ObservedAtText, string AltitudeText, string SpeedText)
{
    public static RecentEventViewModel FromEvent(OverheadEvent overheadEvent)
    {
        return new RecentEventViewModel(
            overheadEvent.Aircraft.Hex,
            overheadEvent.Aircraft.Flight,
            overheadEvent.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
            $"{overheadEvent.Aircraft.Altitude} ft",
            $"{overheadEvent.Aircraft.Speed} kt");
    }
}
