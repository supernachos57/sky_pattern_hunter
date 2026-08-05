# Sky Pattern Hunter — Architecture & Technical Design

## High‑Level Architecture
Sky Pattern Hunter follows Clean Architecture with four layers:

### 1. Domain Layer
- Core business logic.
- Aircraft models, events, ML prediction interfaces.
- No external dependencies.

### 2. Application Layer
- Use cases: ingest ADS‑B data, detect overhead events, run ML predictions.
- Interfaces for infrastructure.
- Orchestrates workflows.

### 3. Infrastructure Layer
- ADS‑B data reader (TCP/HTTP from Raspberry Pi).
- ML.NET model trainers + predictors.
- Discord notification provider.
- SQLite flight history with retention, sampled path points, and metadata snapshots.
- Configuration loader.

### 4. Presentation Layer
- Simple, easy-to-use Windows desktop UI (WPF or WinUI 3).
- Real‑time console log panel.
- Settings editor.
- Aircraft visualization.

---

## Subsystems

### ADS‑B Ingestion
- Connects to Raspberry Pi feed.
- Parses raw messages.
- Converts to domain models.

#### Ingestion: `readsb` JSON-over-TCP

Modern `readsb` setups on a Raspberry Pi can expose a continuous, newline-delimited JSON stream over TCP (commonly port `30001`). The recommended ingestion flow is:

- `readsb` on Pi → TCP stream (`tcp://<pi-ip>:30001`) sending one JSON object per line.
- Ingestion component: a small TCP client that reads each line, parses JSON, and maps fields into the existing normalization pipeline.
- Normalizer/validator converts raw JSON fields (e.g., `hex`, `flight`, `alt_baro`, `lat`, `lon`, `track`, `speed`) into domain `Aircraft` models.
- Hand the normalized domain objects to the event detection pipeline; retained flight sessions and path points are persisted in SQLite, with JSONL available as a compatibility export for ML training.

Configuration keys to add or document:

- `readsb.host` — hostname or IP of the Raspberry Pi (default: `127.0.0.1`).
- `readsb.port` — TCP port for JSON stream (default: `30001`).
- `readsb.mode` — `json` | `raw` (choose parser behavior).
- `readsb.reconnect` — backoff and retry policy settings.

Notes:

- The ingestion client should be resilient: reconnect on disconnect, skip malformed JSON lines with logs, and optionally buffer a small number of messages when downstream is back‑pressured.
- For Windows UI or services, prefer reading and parsing JSON line-by-line rather than trying to parse a continuous byte stream into discrete JSON objects.


### ML Subsystem
- ML.NET pipelines:
  - Busy time prediction (regression).
  - Behavior classification (multi-class).
  - Anomaly detection (clustering or isolation forest).
- Model training + retraining jobs.

### Event Detection
- Overhead detection logic.
- Unusual behavior detection.
- Event logging.

### Notification Subsystem
- Discord DM sender using a bot integration.
- Rate limiting and per-user opt-in/opt-out support.
- Event-to-notification mapping and cooldown rules.

### Storage Subsystem
**Flight-history storage:**
**SQLite**
- Flight sessions are grouped by ICAO hex and callsign, with an inactivity boundary.
- Sampled latitude, longitude, altitude, speed, and heading points retain compact flight paths.
- Aircraft metadata snapshots are stored once per ICAO hex rather than duplicated for every point.
- JSONL remains available as a compatibility/export format for ML workflows.

To manage limited disk space:
- Apply a configurable rolling retention window to sessions and track points.
- Sample path points at a configurable interval while retaining the latest observation for the live dashboard.
- Export selected sessions to JSONL for ML preparation when needed.
- Rotate logs and make storage limits configurable so the app can self-prune when disk usage approaches a threshold.

### Configuration Subsystem
- JSON/YAML config files.
- Strongly typed options classes.

---

## Recommended Packages
- `Microsoft.ML`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Configuration.Json`
- `Serilog` + `Serilog.Sinks.File`
- `System.Text.Json`
- `xUnit`, `Moq`

---

## Solution Structure

```text
SkyPatternHunter/
├── src/
│   ├── SkyPatternHunter.Domain/
│   ├── SkyPatternHunter.Application/
│   ├── SkyPatternHunter.Infrastructure/
│   └── SkyPatternHunter.Presentation/
├── tests/
│   ├── SkyPatternHunter.Domain.Tests/
│   ├── SkyPatternHunter.Application.Tests/
│   └── SkyPatternHunter.Infrastructure.Tests/
├── docs/
│   ├── architecture/
│   ├── roadmap/
│   ├── vision/
│   └── config/
├── appsettings.json
├── ml/
│   ├── busyTimeModel.zip
│   └── behaviorModel.zip
```


---

## Complexity Estimate
- Overall complexity: Medium‑High  
- ML components: Medium  
- ADS‑B ingestion: Medium  
- UI: Medium  
- Notifications: Low  
- Testing: Medium‑High  

