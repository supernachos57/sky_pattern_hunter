readsb TCP JSON sample client
=================================

This folder contains a small Python reference client that connects to a `readsb` JSON-over-TCP stream and prints each parsed JSON object to stdout.

Requirements: Python 3.8+

Run locally (example):

```bash
python scripts/readsb_tcp_client.py --host 192.168.48.83 --port 30001
```

Run for a fixed number of messages (useful for tests):

```bash
python scripts/readsb_tcp_client.py --host 127.0.0.1 --port 30001 --max 5
```

Troubleshooting:
- Ensure the Pi's firewall allows the TCP port (default 30001).
- On Windows, use `Test-NetConnection <pi-ip> -Port 30001` (PowerShell) or `telnet <pi-ip> 30001` if available.
- You can simulate a feed with `nc -l 30001` on Linux and paste newline-delimited JSON lines.
