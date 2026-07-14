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
- SMS notification provider.
- File-based storage (JSONL).
- Configuration loader.

### 4. Presentation Layer
- Windows desktop UI (WPF or WinUI 3).
- Real‑time console log panel.
- Settings editor.
- Aircraft visualization.

---

## Subsystems

### ADS‑B Ingestion
- Connects to Raspberry Pi feed.
- Parses raw messages.
- Converts to domain models.

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
- SMS sender (Twilio or local gateway).
- Rate limiting.

### Storage Subsystem
**Recommended first-version storage:**  
**Append-only JSONL (JSON Lines)**  
- No database needed.  
- Easy to parse.  
- Works well with ML.NET.  
- Low CPU overhead.  

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

SkyPatternHunter/
src/
SkyPatternHunter.Domain/
SkyPatternHunter.Application/
SkyPatternHunter.Infrastructure/
SkyPatternHunter.Presentation/
tests/
SkyPatternHunter.Domain.Tests/
SkyPatternHunter.Application.Tests/
SkyPatternHunter.Infrastructure.Tests/
docs/
architecture/
roadmap/
vision/
config/
appsettings.json
ml/
busyTimeModel.zip
behaviorModel.zip


---

## Complexity Estimate
- Overall complexity: Medium‑High  
- ML components: Medium  
- ADS‑B ingestion: Medium  
- UI: Medium  
- Notifications: Low  
- Testing: Medium‑High  

