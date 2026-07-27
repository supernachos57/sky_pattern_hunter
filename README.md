
Sky Pattern Hunter
===================

Sky Pattern Hunter ingests ADS‑B data (from a Raspberry Pi running `readsb` or similar) to detect and log aircraft patterns and events for downstream ML analysis and notifications.

Key features
- Real-time ADS‑B ingestion (supports `readsb` JSON-over-TCP).
- Normalization to domain `Aircraft` models and append-only JSONL storage for ML training.
- Event detection and hooks for notifications and UI integration.

Prerequisites
- .NET SDK (for the main application) — recommended 6.0 or later.
- Python 3.8+ (for helper scripts in `scripts/`).
- `pytest` (optional, to run Python integration tests).

Quickstart

1. Read the architecture and roadmap in `docs/` to understand design and milestones.
2. Run the sample readsb TCP client (helps validate connectivity):

```bash
python scripts/readsb_tcp_client.py --host 192.168.48.83 --port 30001
```

3. Run the integration test for the sample client:

```bash
pytest tests/integration/test_readsb_tcp_client.py -q
```

Configuration
The ingestion component should be configurable. Recommended keys:

- `readsb.host` — Raspberry Pi host (default: `127.0.0.1`).
- `readsb.port` — TCP port for JSON stream (default: `30001`).
- `readsb.mode` — `json` | `raw` (choose parser behavior).
- `readsb.reconnect` — backoff and retry settings.

`readsb` JSON-over-TCP notes
- `readsb` commonly exposes a newline-delimited JSON stream on port `30001` (one JSON object per line).
- Use a TCP client that reads lines and parses each JSON object; this avoids stream-framing issues.
- Skip malformed JSON lines and reconnect with exponential backoff on disconnects.

Developer notes
- Architecture: see `docs/architecture-and-technical-design.md` for the ingestion flow and recommended config keys.
- Roadmap: see `docs/implementation-roadmap.md` for milestones and epics.
- Sample client: `scripts/readsb_tcp_client.py` is a minimal reference implementation for Windows developers.

Testing and monitoring
- Run `dotnet test` from the repo root to validate the current solution.
- Use `ApplicationSettingsValidator` to check that configuration values are supported before loading them into the app.
- Use `StorageUsageMonitor` to inspect how much disk space the JSONL data, archive, or log folders are using.

Contributing
- Fork, create a feature branch, and open a pull request with tests and a short description of changes.

License
- This repository does not include a license file. Add a `LICENSE` if you intend to publish.


