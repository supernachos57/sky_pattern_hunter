# Sky Pattern Hunter — Implementation Roadmap

## Milestones

### Milestone 1 — Foundation
- Create solution structure.
- Implement DI container.
- Implement configuration loader.
- Implement domain models.
- Implement basic console logging.

### Milestone 2 — ADS‑B Ingestion
- Connect to Raspberry Pi feed.
- Parse ADS‑B messages.
- Normalize aircraft data.
- Unit tests for parsing.

Note: Modern `readsb` setups can provide a continuous JSON stream over TCP (default port `30001`). A Windows app can open a TCP connection to `tcp://<pi-ip>:30001` and read newline-delimited JSON objects for real-time ingestion. Example message:

```json
{
  "hex": "A1B2C3",
  "flight": "DAL123",
  "alt_baro": 32000,
  "lat": 28.1234,
  "lon": -81.2345,
  "track": 270,
  "speed": 450
}
```

### Milestone 3 — Event Detection
- Implement overhead detection.
- Implement behavior classification rules (non‑ML baseline).
- Log events to console + JSONL.
- Unit tests for detection logic.

### Milestone 4 — ML Integration
- Build ML.NET pipelines.
- Train initial models using stored JSONL data.
- Integrate predictions into application layer.
- Add anomaly detection.
- Unit tests using ML.NET test data.

### Milestone 5 — Notifications
- Integrate Discord direct-message provider.
- Add rate limiting and notification preferences.
- Add configuration for notification rules.

### Milestone 6 — UI
- Build a simple Windows desktop UI.
- Choose a relatively simple, easy-to-use layout for the first version.
- Real‑time aircraft list.
- Event log viewer.
- Settings editor.

### Milestone 7 — Hardening
- Add high‑coverage tests.
- Implement retention and roll-up policies to control disk usage.
- Add performance monitoring.
- Add configuration validation.
- Add documentation.

---

## GitHub Issues (Epics → Tasks)

### Epic: Foundation
- [ ] Create solution structure  
- [ ] Add DI container  
- [ ] Add configuration loader  
- [ ] Add logging subsystem  

### Epic: ADS‑B Ingestion
- [ ] Implement Pi connection client  
- [ ] Implement ADS‑B parser  
- [ ] Implement domain models  
- [ ] Write ingestion tests  

### Epic: Event Detection
- [ ] Implement overhead detection  
- [ ] Implement behavior rules  
- [ ] Implement JSONL logging  
- [ ] Write detection tests  

### Epic: ML Integration
- [ ] Build busy-time model  
- [ ] Build behavior classifier  
- [ ] Build anomaly detector  
- [ ] Integrate ML predictions  
- [ ] Write ML tests  

### Epic: Notifications
- [ ] Add Discord DM provider  
- [ ] Add notification rules  
- [ ] Add rate limiting and preferences  
- [ ] Write notification tests  

### Epic: UI
- [ ] Build main dashboard  
- [ ] Build event viewer  
- [ ] Build settings editor  
- [ ] UI tests  

### Epic: Hardening
- [ ] Add performance monitoring  
- [ ] Add configuration validation  
- [ ] Add data retention and storage management  
- [ ] Add documentation  

---

## Phased Backlog

For implementation, a phased approach is recommended. The project should be built in small, testable increments rather than attempting all major subsystems at once. A detailed backlog organized by phase is available in [phased-backlog.md](phased-backlog.md), and the first implementation-ready issue set is in [phase-1-issues.md](phase-1-issues.md).

## Testing Strategy

### Unit Tests
- xUnit + Moq  
- Coverage goal: 85%+  
- Test categories:
  - Domain logic tests
  - ADS‑B parsing tests
  - ML model tests (fixed datasets)
  - Notification tests (mocked)
  - Configuration tests
  - UI logic tests (MVVM)

### Integration Tests
- ADS‑B ingestion end‑to‑end
- ML prediction pipeline
- Notification pipeline

### Load Tests
- Simulated high aircraft density
- 24‑hour continuous run

