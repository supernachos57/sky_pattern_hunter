using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Threading;
using SkyPatternHunter.Infrastructure.AdsB;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;
using SkyPatternHunter.Infrastructure.Logging;

namespace SkyPatternHunter.Presentation;

public partial class MainWindow : INotifyPropertyChanged
{
    private readonly LiveEventDashboard _dashboard;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _runtimeCancellationTokenSource = new();
    private readonly AircraftDatabaseManager _aircraftDatabaseManager;
    private readonly SqliteFlightHistoryStore _flightHistoryStore;
    private readonly FileLogger _logger;
    private readonly ApplicationSettings _settings;
    private readonly Dictionary<GridViewColumn, double> _maximumEventColumnWidths = [];
    private Task? _runtimeTask;
    private bool _runtimeStarted;
    private bool _isRefreshing;
    private bool _isRefreshingHistory;
    private DateTimeOffset _nextHistoryRefreshAt = DateTimeOffset.MinValue;

    public ObservableCollection<RecentEventViewModel> ActiveEvents { get; } = new();
    public BatchObservableCollection<FlightHistorySessionViewModel> HistoryFlights { get; } = new();

    private int _eventCount;
    public int EventCount
    {
        get => _eventCount;
        private set => SetProperty(ref _eventCount, value);
    }

    private string _latestAircraftHex = "None";
    public string LatestAircraftHex
    {
        get => _latestAircraftHex;
        private set => SetProperty(ref _latestAircraftHex, value);
    }

    private string _latestObservedAtText = "No events yet";
    public string LatestObservedAtText
    {
        get => _latestObservedAtText;
        private set => SetProperty(ref _latestObservedAtText, value);
    }

    private string _runtimeStatusMessage = "Ready.";
    public string RuntimeStatusMessage
    {
        get => _runtimeStatusMessage;
        private set => SetProperty(ref _runtimeStatusMessage, value);
    }

    public string ReadsbHost => _settings.ReadsbHost;
    public int ReadsbPort => _settings.ReadsbPort;
    public string ReadsbJsonUrl => _settings.ReadsbJsonUrl ?? "Not configured";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        _settings = new JsonConfigurationService().GetSettings();
        _settings.DataDirectory ??= Path.Combine(AppContext.BaseDirectory, "data");
        _settings.AircraftDataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyPatternHunter",
            "data");

        _aircraftDatabaseManager = new AircraftDatabaseManager(
            Path.Combine(AppContext.BaseDirectory, "data", "aircraft.csv.gz"),
            _settings.AircraftDataDirectory,
            _settings.AircraftDatabaseSourceUrl);
        _flightHistoryStore = new SqliteFlightHistoryStore(
            _settings.DataDirectory,
            _settings.HistoryDays,
            TimeSpan.FromSeconds(_settings.FlightHistorySampleSeconds),
            aircraftLookup: new SqliteAircraftLookup(_aircraftDatabaseManager.DatabasePath));
        _dashboard = new LiveEventDashboard(
            _flightHistoryStore,
            TimeSpan.FromSeconds(_settings.DashboardStaleAfterSeconds),
            aircraftClient: new SqliteAircraftLookup(_aircraftDatabaseManager.DatabasePath),
            userLatitude: _settings.UserLatitude,
            userLongitude: _settings.UserLongitude,
            historyDays: _settings.HistoryDays);
        _logger = new FileLogger(_settings.LogFilePath);

        InitializeComponent();
        DataContext = this;
        RuntimeStatusMessage = $"Ready. Waiting for live feed at {ReadsbJsonUrl}.";

        _ = RefreshHistoryFlightsAsync();
        _ = RefreshFromJournalAsync();
        _ = InitializeAircraftDatabaseAsync();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += async (_, _) =>
        {
            await RefreshFromJournalAsync();
            await RefreshHistoryFlightsAsync();
        };
        _refreshTimer.Start();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async Task RefreshFromJournalAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            var snapshot = await Task.Run(_dashboard.Refresh);

            ActiveEvents.Clear();
            foreach (var eventViewModel in snapshot.ActiveEvents)
            {
                ActiveEvents.Add(eventViewModel);
            }

            EventCount = snapshot.ActiveAircraftCount;
            LatestAircraftHex = snapshot.LatestAircraftHex;
            LatestObservedAtText = snapshot.LatestObservedAtText;
            await Dispatcher.InvokeAsync(
                () => AutoSizeEventColumns(),
                DispatcherPriority.Loaded);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshHistoryFlightsAsync()
    {
        if (_isRefreshingHistory || DateTimeOffset.UtcNow < _nextHistoryRefreshAt)
        {
            return;
        }

        _isRefreshingHistory = true;

        try
        {
            var refreshedFlights = await Task.Run(() => _flightHistoryStore.ReadFlightSessions()
                .Select(session => FlightHistorySessionViewModel.FromSession(session, _settings.UserLatitude, _settings.UserLongitude))
                .ToArray());

            HistoryFlights.ReplaceWith(refreshedFlights);

            _nextHistoryRefreshAt = DateTimeOffset.UtcNow.AddSeconds(15);
            _ = Dispatcher.BeginInvoke(AutoSizeHistoryColumns, DispatcherPriority.ContextIdle);
        }
        finally
        {
            _isRefreshingHistory = false;
        }
    }

    private void AutoSizeEventColumns()
    {
        SetColumnsToAuto(ActiveEventsList);
        ActiveEventsList.UpdateLayout();
        LockColumnsAtMaximumWidth(ActiveEventsList);
    }

    private void AutoSizeHistoryColumns()
    {
        SetColumnsToAuto(HistoryEventsList);
        HistoryEventsList.UpdateLayout();
        LockColumnsAtMaximumWidth(HistoryEventsList);
    }

    private static void SetColumnsToAuto(ListView listView)
    {
        if (listView.View is not GridView gridView)
        {
            return;
        }

        foreach (var column in gridView.Columns)
        {
            column.Width = double.NaN;
        }
    }

    private void LockColumnsAtMaximumWidth(ListView listView)
    {
        if (listView.View is not GridView gridView)
        {
            return;
        }

        foreach (var column in gridView.Columns)
        {
            var maximumWidth = Math.Max(
                _maximumEventColumnWidths.GetValueOrDefault(column),
                column.ActualWidth);
            _maximumEventColumnWidths[column] = maximumWidth;
            column.Width = maximumWidth;
        }
    }

    private async Task InitializeAircraftDatabaseAsync()
    {
        var build = await Task.Run(_aircraftDatabaseManager.EnsureDatabase);
        _logger.Information(build.Message);
        if (build.Succeeded)
        {
            _dashboard.ClearAircraftLookupCache();
            await RefreshFromJournalAsync();
        }

        var update = await _aircraftDatabaseManager.UpdateFromRemoteAsync(_runtimeCancellationTokenSource.Token);
        _logger.Information(update.Message);
        if (update.Updated)
        {
            _dashboard.ClearAircraftLookupCache();
            await RefreshFromJournalAsync();
        }
    }

    private void OnLoaded(object sender, EventArgs e)
    {
        if (_runtimeStarted)
        {
            return;
        }

        _runtimeStarted = true;
        _runtimeTask = RunRuntimeAsync(_runtimeCancellationTokenSource.Token);
    }

    private async Task RunRuntimeAsync(CancellationToken cancellationToken)
    {
        RuntimeStatusMessage = "Live feed runtime starting...";

        try
        {
            var host = AdsbStartupHost.CreateFromConfiguration();
            RuntimeStatusMessage = $"Live feed runtime started. Connecting to {ReadsbJsonUrl}.";
            await host.RunAsync(cancellationToken);
            RuntimeStatusMessage = "Live feed runtime stopped.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RuntimeStatusMessage = "Live feed runtime canceled.";
        }
        catch (Exception ex)
        {
            _logger.Error($"UI runtime failure: {ex.Message}");
            RuntimeStatusMessage = $"Runtime error: {ex.Message}";
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        _runtimeCancellationTokenSource.Cancel();

        if (_runtimeTask is not null)
        {
            try
            {
                await _runtimeTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _runtimeCancellationTokenSource.Dispose();
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record FlightTrackPointViewModel(string ObservedAtText, string PositionText, string AltitudeText, string TrackText, string SpeedText, string SquawkText)
{
    public static FlightTrackPointViewModel FromPoint(FlightTrackPoint point)
    {
        return new FlightTrackPointViewModel(
            point.ObservedAt.ToLocalTime().ToString("h:mm:ss tt", CultureInfo.CurrentCulture),
            $"{point.Latitude:F5}, {point.Longitude:F5}",
            $"{point.AltitudeFeet:N0} ft",
            $"{point.TrackDegrees} deg",
            $"{point.SpeedKnots} kt",
            point.Squawk?.ToString() ?? "-");
    }
}

public sealed record FlightHistorySessionViewModel(long SessionId, string ObservedAtText, string Flight, string AircraftHex, string DurationText, string AltitudeText, string SpeedText, string PositionText, string AircraftDescription)
{
    public static FlightHistorySessionViewModel FromSession(FlightHistorySessionSummary summary, double userLatitude, double userLongitude)
    {
        var session = summary.Session;
        var point = summary.LatestPoint;
        var duration = session.EndedAt - session.StartedAt;
        var distanceMiles = CalculateDistanceMiles(userLatitude, userLongitude, point.Latitude, point.Longitude);
        return new FlightHistorySessionViewModel(
            session.Id,
            session.EndedAt.ToLocalTime().ToString("yyyy-MM-dd h:mm:ss tt", CultureInfo.CurrentCulture),
            session.Callsign ?? "Unknown",
            session.IcaoHex,
            duration.TotalMinutes >= 1 ? $"{duration.TotalMinutes:0} min" : $"{duration.TotalSeconds:0} sec",
            $"{point.AltitudeFeet:N0} ft",
            $"{point.SpeedKnots} kt",
            $"{distanceMiles:0.0} mi",
            FormatAircraftDescription(summary.Metadata));
    }

    private static string FormatAircraftDescription(AircraftMetadataSnapshot? metadata)
    {
        if (metadata is null)
        {
            return "Unknown";
        }

        return string.Join(" | ", new[]
        {
            metadata.Registration,
            metadata.TypeCode,
            metadata.Description,
            metadata.Year,
            metadata.RegisteredOwner
        }.Where(value => !string.IsNullOrWhiteSpace(value)).DefaultIfEmpty("Unknown"));
    }

    private static double CalculateDistanceMiles(double startLatitude, double startLongitude, double endLatitude, double endLongitude)
    {
        const double EarthRadiusMiles = 3958.7613;
        var latitudeDeltaRadians = DegreesToRadians(endLatitude - startLatitude);
        var longitudeDeltaRadians = DegreesToRadians(endLongitude - startLongitude);
        var startLatitudeRadians = DegreesToRadians(startLatitude);
        var endLatitudeRadians = DegreesToRadians(endLatitude);
        var a = Math.Sin(latitudeDeltaRadians / 2) * Math.Sin(latitudeDeltaRadians / 2) +
                Math.Cos(startLatitudeRadians) * Math.Cos(endLatitudeRadians) *
                Math.Sin(longitudeDeltaRadians / 2) * Math.Sin(longitudeDeltaRadians / 2);
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}
