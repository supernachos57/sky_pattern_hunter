using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using SkyPatternHunter.Domain.Models;
using SkyPatternHunter.Infrastructure.Configuration;
using SkyPatternHunter.Infrastructure.Events;

namespace SkyPatternHunter.Presentation;

public partial class MainWindow : INotifyPropertyChanged
{
    private readonly JsonConfigurationService _configurationService = new();
    private readonly ApplicationSettings _settings;
    private readonly DispatcherTimer _eventsRefreshTimer = new();
    private readonly string _eventsDirectory;
    private CancellationTokenSource? _runtimeCancellation;
    private Task? _runtimeTask;

    private int _eventCount;
    private string _latestAircraftHex = "None";
    private string _latestObservedAtText = "No events yet";

    public ObservableCollection<RecentEventViewModel> Events { get; } = new();

    public int EventCount
    {
        get => _eventCount;
        private set => SetProperty(ref _eventCount, value);
    }

    public string LatestAircraftHex
    {
        get => _latestAircraftHex;
        private set => SetProperty(ref _latestAircraftHex, value);
    }

    public string LatestObservedAtText
    {
        get => _latestObservedAtText;
        private set => SetProperty(ref _latestObservedAtText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _configurationService.GetSettings();
        _eventsDirectory = string.IsNullOrWhiteSpace(_settings.DataDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : _settings.DataDirectory;

        LoadSettings();
        DataContext = this;

        _eventsRefreshTimer.Interval = TimeSpan.FromSeconds(2);
        _eventsRefreshTimer.Tick += (_, _) => LoadEvents();

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadEvents();
        _eventsRefreshTimer.Start();
        StartRuntime();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _eventsRefreshTimer.Stop();

        if (_runtimeCancellation is not null)
        {
            _runtimeCancellation.Cancel();
        }
    }

    private void StartRuntime()
    {
        if (_runtimeTask is not null)
        {
            return;
        }

        _runtimeCancellation = new CancellationTokenSource();
        var cancellationToken = _runtimeCancellation.Token;

        _runtimeTask = Task.Run(async () =>
        {
            try
            {
                var host = AdsbStartupHost.CreateFromConfiguration();
                await host.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    SettingsStatusTextBlock.Text = $"Runtime error: {ex.Message}";
                });
            }
        }, cancellationToken);
    }

    private void LoadSettings()
    {
        ReadsbHostTextBox.Text = _settings.ReadsbHost;
        ReadsbPortTextBox.Text = _settings.ReadsbPort.ToString(CultureInfo.InvariantCulture);
        NotificationPrefixTextBox.Text = _settings.DiscordNotificationMessagePrefix;
        DiscordNotificationsCheckBox.IsChecked = _settings.DiscordNotificationsEnabled;
        SettingsStatusTextBlock.Text = $"Settings loaded from {_configurationService.ConfigurationPath}.";
    }

    private void SaveSettings_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!int.TryParse(ReadsbPortTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var readsbPort))
        {
            SettingsStatusTextBlock.Text = "Readsb port must be a valid number.";
            return;
        }

        _settings.ReadsbHost = ReadsbHostTextBox.Text.Trim();
        _settings.ReadsbPort = readsbPort;
        _settings.DiscordNotificationMessagePrefix = NotificationPrefixTextBox.Text.Trim();
        _settings.DiscordNotificationsEnabled = DiscordNotificationsCheckBox.IsChecked == true;

        var validation = ApplicationSettingsValidator.Validate(_settings);
        if (!validation.IsValid)
        {
            SettingsStatusTextBlock.Text = string.Join(" ", validation.Errors);
            return;
        }

        _configurationService.SaveSettings(_settings);
        SettingsStatusTextBlock.Text = "Settings saved. Restart the app to apply runtime changes.";
        MessageBox.Show(
            $"Settings were saved to {_configurationService.ConfigurationPath}. Restart the app to apply runtime changes.",
            "Sky Pattern Hunter Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void LoadEvents()
    {
        var journal = new AircraftSightingJournal(_eventsDirectory);
        var sightings = journal.ReadAll();
        var latestSighting = sightings.LastOrDefault();

        Events.Clear();
        foreach (var sighting in sightings.OrderByDescending(item => item.ObservedAt).Take(100))
        {
            Events.Add(RecentEventViewModel.FromSighting(sighting));
        }

        EventCount = sightings.Count;
        LatestAircraftHex = latestSighting?.Aircraft.Hex ?? "None";
        LatestObservedAtText = latestSighting is null
            ? "No events yet"
            : UiTimeDisplay.FormatObservedAtEastern(latestSighting.ObservedAt);
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

public sealed record RecentEventViewModel(string EventTypeText, string AircraftHex, string? Flight, string ObservedAtText, string AltitudeText, string SpeedText)
{
    public static RecentEventViewModel FromSighting(AircraftSighting sighting)
    {
        return new RecentEventViewModel(
            sighting.IsOverhead ? "OVERHEAD" : "TRACK",
            sighting.Aircraft.Hex,
            sighting.Aircraft.Flight,
            UiTimeDisplay.FormatObservedAtEastern(sighting.ObservedAt),
            $"{sighting.Aircraft.Altitude} ft",
            $"{sighting.Aircraft.Speed} kt");
    }
}

file static class UiTimeDisplay
{
    private static readonly TimeZoneInfo EasternTimeZone = ResolveEasternTimeZone();

    public static string FormatObservedAtEastern(DateTimeOffset observedAt)
    {
        var easternTime = TimeZoneInfo.ConvertTime(observedAt, EasternTimeZone);
        return easternTime.ToString("yyyy-MM-dd hh:mm:ss tt 'EST'", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
