using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
    private readonly HttpClient _hexDbHttpClient = new();
    private readonly FileLogger _logger;
    private readonly ApplicationSettings _settings;
    private Task? _runtimeTask;
    private bool _runtimeStarted;
    private bool _isRefreshing;

    public ObservableCollection<RecentEventViewModel> ActiveEvents { get; } = new();
    public ObservableCollection<RecentEventViewModel> TodayEvents { get; } = new();

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

        var journal = new OverheadEventJournal(_settings.DataDirectory);
        _dashboard = new LiveEventDashboard(
            journal,
            TimeSpan.FromSeconds(_settings.DashboardStaleAfterSeconds),
            aircraftClient: new HexDbAircraftClient(_hexDbHttpClient),
            userLatitude: _settings.UserLatitude,
            userLongitude: _settings.UserLongitude);
        _logger = new FileLogger(_settings.LogFilePath);

        InitializeComponent();
        DataContext = this;
        RuntimeStatusMessage = $"Ready. Waiting for live feed at {ReadsbJsonUrl}.";

        _ = RefreshFromJournalAsync();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshFromJournalAsync();
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
            var snapshot = await _dashboard.RefreshAsync();

            ActiveEvents.Clear();
            foreach (var eventViewModel in snapshot.ActiveEvents)
            {
                ActiveEvents.Add(eventViewModel);
            }

            TodayEvents.Clear();
            foreach (var eventViewModel in snapshot.TodayEvents)
            {
                TodayEvents.Add(eventViewModel);
            }

            EventCount = snapshot.ActiveAircraftCount;
            LatestAircraftHex = snapshot.LatestAircraftHex;
            LatestObservedAtText = snapshot.LatestObservedAtText;
        }
        finally
        {
            _isRefreshing = false;
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

        _hexDbHttpClient.Dispose();
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
