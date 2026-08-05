# Sky Pattern Hunter — Living Architecture Document
Version: 0.1.0  
Status: Draft  
Maintainer: Ryan W.

This document evolves with the system. Every architectural decision, change, and rationale is recorded here.

---

# 1. System Overview

Sky Pattern Hunter is a modular aircraft-tracking intelligence system built using Clean Architecture. It ingests ADS‑B signals, processes aircraft telemetry, applies ML.NET models, and triggers notifications through Discord direct messages.

The system is designed to run continuously with low CPU usage, high extensibility, and a simple user experience.

---

# 2. Architecture Goals

- High testability  
- Clean separation of concerns  
- Asynchronous APIs  
- Extendable to new antennas and data sources  
- Minimal external dependencies  
- Predictive analytics using ML.NET  
- Real-time event detection  
- Discord-based notifications for overhead events  
- A simple, approachable desktop UI for first-version usability  

---

# 3. Architecture Layers

## 3.1 Domain Layer
- Pure business logic  
- Aircraft models  
- Event models  
- ML prediction interfaces  
- No external dependencies  

## 3.2 Application Layer
- Use cases  
- Orchestrates workflows  
- Interfaces for infrastructure  

## 3.3 Infrastructure Layer
- ADS‑B ingestion  
- ML.NET pipelines  
- Discord DM provider  
- SQLite flight-session storage with retention controls and sampled path points
- Configuration loader  

## 3.4 Presentation Layer
- Simple Windows UI  
- Real-time aircraft list  
- Event viewer  
- Settings editor  

---

# 4. Data Flow Diagram (Mermaid)

```mermaid
flowchart LR
    PiFeed["ADS-B Feed from Raspberry Pi"] --> IngestService["ADS-B Ingestion Service"]
    IngestService --> NormalizeStep["Normalization"]
    NormalizeStep --> DomainModels["Domain Models"]
    DomainModels --> EventDetect["Event Detection"]
    EventDetect --> MLPredict["ML.NET Predictions"]
    MLPredict --> EventStream["Event Stream"]
    EventStream --> NotifyService["Discord DM Notification Service"]
    EventStream --> DesktopUI["Simple Desktop UI"]
    EventStream --> FlightStorage["SQLite Flight History"]
```
---

# 5. ML Architecture Diagram (Mermaid)
```mermaid
flowchart TD
    FlightData["SQLite Flight History / JSONL Export"] --> PrepData["Data Preparation"]
    PrepData --> BusyModel["Busy Time Regression Model"]
    PrepData --> BehaviorModel["Behavior Classification Model"]
    PrepData --> AnomalyModel["Anomaly Detection Model"]

    BusyModel --> ModelStore["Model Store"]
    BehaviorModel --> ModelStore
    AnomalyModel --> ModelStore

    ModelStore --> PredictEngine["Prediction Engine"]
```

---

# 6. System Context Diagram (Mermaid)

```mermaid
flowchart LR
    User["User (Ryan)"] --> DesktopApp["Sky Pattern Hunter Desktop App"]
    DesktopApp --> ADSBIngest["ADS-B Ingestion Module"]
    ADSBIngest --> PiDevice["Raspberry Pi + RTL-SDR"]
    DesktopApp --> MLModule["ML.NET Module"]
    DesktopApp --> NotifyModule["Discord Notification Module"]
    DesktopApp --> StorageModule["SQLite Flight History"]
```

---

# 7. Component Diagram (Mermaid)
```mermaid
flowchart TD
    Domain["Domain Layer"] --> Application["Application Layer"]
    Application --> Infrastructure["Infrastructure Layer"]
    Application --> Presentation["Presentation Layer"]

    Infrastructure --> ADSB["ADS-B Reader"]
    Infrastructure --> ML["ML.NET Pipelines"]
    Infrastructure --> Discord["Discord DM Provider"]
    Infrastructure --> Config["Configuration Loader"]
    Infrastructure --> Storage["SQLite Flight History"]
```

---

# 8. ADS‑B Ingestion Sequence Diagram (Mermaid)
```mermaid
sequenceDiagram
    participant Pi as Raspberry Pi
    participant Ingest as Ingestion Service
    participant Normalize as Normalizer
    participant Domain as Domain Layer
    participant Detect as Event Detection

    Pi->>Ingest: Send ADS-B messages
    Ingest->>Normalize: Parse & normalize message
    Normalize->>Domain: Create domain aircraft model
    Domain->>Detect: Evaluate for events
    Detect->>Domain: Emit event (if overhead/unusual)
```

---
# 9. ML Prediction Sequence Method (Mermaid)
```mermaid
sequenceDiagram
    participant App as Application Layer
    participant ML as ML.NET Engine
    participant Store as Model Store

    App->>Store: Load trained models
    Store->>ML: Provide model instances
    App->>ML: Request prediction
    ML->>App: Return prediction result
```

---

# 10. Deployment Diagram (Mermaid)
```mermaid
flowchart LR
    Windows["Windows Desktop (Sky Pattern Hunter)"] --> LocalNet["Local Network"]
    LocalNet --> Pi["Raspberry Pi 5"]
    Pi --> SDR["RTL-SDR v5 + 1090 MHz Antenna"]

    Windows --> DiscordAPI["Discord Bot / API"]
```