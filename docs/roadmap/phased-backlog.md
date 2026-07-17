# Phased Implementation Backlog

This backlog is organized as a series of small, working phases so the project can be built incrementally without trying to deliver the entire system at once.

## Recommended approach

Use a vertical-slice strategy rather than building by layer alone:

- Each phase should produce a usable increment.
- Keep each issue narrow enough to complete in one PR or a few days of work.
- Prefer a working data pipeline before adding ML or UI complexity.
- Delay advanced polish until the core workflow is proven end to end.

## Phase 1 — Foundation and baseline plumbing

Detailed issue drafts for this phase are available in [phase-1-issues.md](phase-1-issues.md).

### Issue 1: Create solution skeleton and project structure
- Create or verify the solution, project files, and folder structure.
- Ensure the domain, application, infrastructure, and presentation projects are wired together.
- Acceptance criteria:
  - The solution builds successfully.
  - Projects can reference each other cleanly.

### Issue 2: Add configuration and logging infrastructure
- Add configuration loading from appsettings and environment overrides.
- Add structured logging with file output.
- Acceptance criteria:
  - Settings can be read from configuration files.
  - Application logs are written to disk.

### Issue 3: Define core domain models and interfaces
- Create the core aircraft, event, and notification domain models.
- Define interfaces for ingestion, detection, persistence, and notifications.
- Acceptance criteria:
  - Domain models are available for use by other layers.
  - Interfaces are dependency-injection friendly.

## Phase 2 — Ingest and persist aircraft data

### Issue 4: Implement ADS-B TCP client for Raspberry Pi feed
- Connect to the Raspberry Pi feed over TCP.
- Handle reconnects and malformed input gracefully.
- Acceptance criteria:
  - The app can connect to a test feed and receive messages.
  - Connection failures do not crash the app.

### Issue 5: Parse and normalize ADS-B messages
- Convert incoming feed messages into domain models.
- Validate and normalize fields such as aircraft ID, location, altitude, and timestamp.
- Acceptance criteria:
  - Example feed data produces normalized aircraft records.
  - Invalid messages are skipped or logged clearly.

### Issue 6: Persist raw and normalized data to JSONL storage
- Write incoming messages and normalized records to JSONL files.
- Support a basic storage path and file rotation strategy.
- Acceptance criteria:
  - Data is durable across app restarts.
  - Files are written in a predictable format.

## Phase 3 — Event detection and local insight

### Issue 7: Implement baseline overhead event detection
- Detect aircraft events when the aircraft is likely overhead based on simple rules.
- Emit domain events with enough metadata to inspect them later.
- Acceptance criteria:
  - Test data produces detected overhead events.
  - Events are logged and stored.

### Issue 8: Add event persistence and simple reporting
- Save detected events to a structured event log.
- Expose a basic in-memory or file-based event history view.
- Acceptance criteria:
  - Events can be retrieved for review.
  - The flow from ingestion to event detection is visible.

## Phase 4 — Notifications and simple UI

### Issue 9: Implement Discord DM notification provider
- Add a Discord bot integration for direct-message notifications.
- Support message formatting, throttling, and notification preferences.
- Acceptance criteria:
  - A test event produces a notification payload.
  - Rate limiting or cooldowns are enforced.

### Issue 10: Build a simple desktop UI shell
- Create a simple first-version UI with a dashboard, event list, and settings view.
- Keep the UX intentionally minimal and easy to use.
- Acceptance criteria:
  - The app opens and shows live or recent data.
  - Users can view events and settings.

## Phase 5 — ML and hardening

### Issue 11: Add initial ML.NET prediction pipeline
- Create a baseline model pipeline for busy-time or behavior prediction.
- Train or load a simple model from stored historical data.
- Acceptance criteria:
  - The app can run a prediction using historical data.
  - Model artifacts are stored in the expected location.

### Issue 12: Add retention, roll-up, and storage management
- Implement configurable retention periods and pruning logic.
- Add roll-up or summarization of older data to reduce disk usage.
- Acceptance criteria:
  - The app can keep storage under configured limits.
  - Older data is archived or summarized automatically.

### Issue 13: Add testing, monitoring, and documentation
- Add unit and integration tests around the main workflows.
- Add performance monitoring and configuration validation.
- Acceptance criteria:
  - Core workflows are covered by tests.
  - The project is documented enough for future contributors.

## Suggested ordering

The best order is:

1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4
5. Phase 5

That sequence keeps the project grounded in a working end-to-end pipeline before introducing the more experimental pieces such as ML and UI polish.
