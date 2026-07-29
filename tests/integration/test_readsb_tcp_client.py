import socket
import threading
import time
import json
import tempfile
from pathlib import Path

from scripts.readsb_tcp_client import read_stream


def _start_test_server(bind_host, bind_port, messages, delay=0.01):
    def _server():
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as srv:
            srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            srv.bind((bind_host, bind_port))
            srv.listen(1)
            conn, _ = srv.accept()
            with conn:
                for m in messages:
                    conn.sendall((json.dumps(m) + "\n").encode("utf-8"))
                    time.sleep(delay)

    t = threading.Thread(target=_server, daemon=True)
    t.start()
    return t


def test_read_stream_local():
    host = "127.0.0.1"
    port = 30011
    messages = [
        {"hex": "A1B2C3", "flight": "DAL123", "alt_baro": 32000},
        {"hex": "D4E5F6", "flight": "UAL999", "alt_baro": 28000},
    ]

    received = []

    def on_message(obj):
        received.append(obj)

    with tempfile.TemporaryDirectory() as temp_dir:
        output_path = Path(temp_dir) / "frames.log"
        srv_thread = _start_test_server(host, port, messages)

        # allow server to start
        time.sleep(0.05)
        read_stream(host, port, on_message, max_messages=len(messages), output_path=output_path)

        assert len(received) == len(messages)
        assert received[0]["hex"] == "A1B2C3"
        assert output_path.exists()
        assert output_path.read_text(encoding="utf-8").splitlines()[0] == json.dumps(messages[0])
