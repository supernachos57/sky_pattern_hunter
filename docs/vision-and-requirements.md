# Sky Pattern Hunter — Vision & Requirements

## Vision
Sky Pattern Hunter is a Windows desktop application designed to observe, analyze, and predict aircraft activity overhead. As stated in the project notes — “I want to create a windows app that collects data on airplanes flying overhead” — the system will ingest ADS‑B signals from a Raspberry Pi + SDR setup, classify aircraft behavior using ML.NET, detect anomalies, and notify the user when a plane passes overhead.

The long‑term vision is a fully autonomous, low‑CPU, extendable system that runs continuously and supports multiple antenna types.

---

## Goals
- Real‑time aircraft detection and logging.
- Predict busy times and flight patterns using ML.NET.
- Classify aircraft behavior and detect anomalies.
- Notify the user via SMS when a plane passes overhead.
- Maintain high testability and clean architecture.
- Support future expansion to non‑commercial aircraft and additional antennas.

---

## Functional Requirements

### Data Ingestion
- Connect to Raspberry Pi ADS‑B feed over local network.
- Parse raw messages (e.g., from `readsb`).
- Normalize aircraft data into domain models.

### Real‑Time Processing
- Track aircraft positions relative to the user’s location.
- Determine overhead events based on configurable thresholds.
- Log events to console and structured log files.

### Machine Learning
- Predict busy times using historical data.
- Classify aircraft behavior (ascending, descending, circling, unusual).
- Detect anomalies using ML.NET.
- Predict next overhead pass.

### Notifications
- Send SMS via Twilio or local gateway.
- Configurable notification rules.

### Configuration
- All settings stored in JSON/YAML files.
- No database unless absolutely necessary.

### Testing
- xUnit test suite with high coverage.
- Mockable interfaces for all external dependencies.

---

## Non‑Functional Requirements
- Low CPU usage.
- Runs 24/7.
- Extendable architecture.
- Clean architecture + dependency injection.
- Asynchronous APIs.

---

## Hardware
- Raspberry Pi 5  
- ADS‑B antenna (1090 MHz)  
- NooElec RTL‑SDR v5  

