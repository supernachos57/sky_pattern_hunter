# Running Sky Pattern Hunter

## Prerequisites

- Windows with the .NET 8 SDK installed.
- A reachable `readsb` JSON endpoint.
- The Raspberry Pi hostname `adsb-pi` must resolve from this computer, or replace it with the Pi's IP address.

## Configure the feed

The repository's `appsettings.json` is copied beside the presentation executable during the build. Its current defaults poll the Pi's `tar1090` JSON snapshot:

```json
{
  "ReadsbHost": "adsb-pi",
  "ReadsbPort": 30001,
  "ReadsbJsonUrl": "http://adsb-pi/tar1090/data/aircraft.json"
}
```

For a different feed, set `ReadsbJsonUrl` to its absolute HTTP or HTTPS aircraft snapshot URL. The application reads each object from the snapshot's `aircraft` array once per second.

Set the observer location before expecting detected events. `0,0` is the default and will not detect aircraft near the Pi:

```json
{
  "UserLatitude": 28.1234,
  "UserLongitude": -81.2345,
  "DetectionThresholdMiles": 10
}
```

Replace the example latitude and longitude with the location where overhead events should be detected.

`DashboardStaleAfterSeconds` controls when an aircraft leaves the `Active` tab and appears as `Stale` at the end of the `Today` tab. The default is 60 seconds.

To verify the configured feed is reachable from PowerShell:

```powershell
Invoke-WebRequest -UseBasicParsing http://adsb-pi/tar1090/data/aircraft.json
```

The command should return JSON containing an `aircraft` array before the application can ingest data.

## Start the application

From the repository root, run:

```powershell
dotnet run --project src\SkyPatternHunter.Presentation\SkyPatternHunter.Presentation.csproj
```

The WPF window opens and starts live ingestion automatically. Leave it open while collecting events; closing the window stops the ingestion task.

## Confirm ingestion

- The dashboard updates when an aircraft meets the configured overhead threshold.
- The dashboard looks up six-character ICAO hex values through `https://hexdb.io/api/v1/aircraft/{hex}` and displays the registration, manufacturer, type, and registered owner when available. Lookups are cached for the lifetime of the app.
- Runtime activity and parse errors are written to `logs/sky-pattern-hunter.log`.
- Detected events are stored as JSONL under the application's `data` directory when no `DataDirectory` is configured.

An empty dashboard does not necessarily mean the connection failed. It can mean that no received aircraft met the configured location and `DetectionThresholdMiles` criteria.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Startup validation dialog | Ensure `ReadsbHost` is non-empty, `ReadsbPort` is between 1 and 65535, and `DetectionThresholdMiles` is greater than zero. |
| Repeated connection attempts | Request the configured `ReadsbJsonUrl` and confirm it returns JSON containing an `aircraft` array. |
| Parse errors in the log | Confirm each entry in the `aircraft` array is a readsb aircraft JSON object. |
| No detected events | Verify `UserLatitude`, `UserLongitude`, and `DetectionThresholdMiles` in `appsettings.json`. |