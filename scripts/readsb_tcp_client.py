#!/usr/bin/env python3
"""Simple readsb JSON-over-TCP client for testing and integration.

Connects to a TCP host/port, reads newline-delimited JSON objects,
parses them, and calls an `on_message` callback with the parsed dict.

This is intended as a small reference client/harness for Windows UI developers.
"""

import argparse
import json
import logging
import socket
import time
from typing import Callable, Optional

logger = logging.getLogger("readsb_tcp_client")


def read_json_stream(host: str, port: int, on_message: Callable[[dict], None], max_messages: Optional[int] = None):
    backoff = 1.0
    received = 0
    while True:
        try:
            with socket.create_connection((host, port), timeout=10) as s:
                s_file = s.makefile("r", encoding="utf-8", newline="\n")
                logger.info("Connected to %s:%d", host, port)
                backoff = 1.0
                for line in s_file:
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        obj = json.loads(line)
                    except Exception as ex:
                        logger.warning("Skipping malformed JSON line: %s", ex)
                        continue
                    on_message(obj)
                    received += 1
                    if max_messages and received >= max_messages:
                        return
        except Exception as ex:
            logger.warning("Connection error: %s — reconnecting in %.1fs", ex, backoff)
            time.sleep(backoff)
            backoff = min(backoff * 2, 30.0)


def _print_message(obj: dict):
    print(json.dumps(obj, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description="readsb JSON-over-TCP sample client")
    parser.add_argument("--host", default="127.0.0.1", help="readsb host")
    parser.add_argument("--port", type=int, default=30001, help="readsb TCP port")
    parser.add_argument("--max", type=int, help="exit after N messages (for tests)")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    logging.basicConfig(level=logging.DEBUG if args.verbose else logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    read_json_stream(args.host, args.port, _print_message, max_messages=args.max)


if __name__ == "__main__":
    main()
