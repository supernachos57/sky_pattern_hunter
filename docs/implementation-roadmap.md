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
- Integrate SMS provider.
- Add rate limiting.
- Add configuration for notification rules.

### Milestone 6 — UI
- Build Windows desktop UI.
- Real‑time aircraft list.
- Event log viewer.
- Settings editor.

### Milestone 7 — Hardening
- Add high‑coverage tests.
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
- [ ] Add SMS provider  
- [ ] Add notification rules  
- [ ] Add rate limiting  
- [ ] Write notification tests  

### Epic: UI
- [ ] Build main dashboard  
- [ ] Build event viewer  
- [ ] Build settings editor  
- [ ] UI tests  

### Epic: Hardening
- [ ] Add performance monitoring  
- [ ] Add configuration validation  
- [ ] Add documentation  

---

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

