"""Entry point for the ESP32 BLE gamepad → OBS controller.

The script performs three tasks:
1. Connects to the OBS WebSocket server using :class:`ObsBridge`.
2. Starts a :class:`GamepadListener` that forwards button *press* events to the
   bridge.
3. Runs until the user aborts with Ctrl‑C.

All configuration (host, port, password) can be supplied via command‑line
arguments; defaults match the standard OBS WebSocket plugin settings.
"""

from __future__ import annotations

import argparse
import signal
import sys
from pathlib import Path

from .gamepad import GamepadListener
from .obs_bridge import ObsBridge


def _signal_handler(sig, frame):  # pragma: no cover – simple graceful exit
    print("\nInterrupted – shutting down...")
    sys.exit(0)


def main() -> None:
    parser = argparse.ArgumentParser(description="Bridge ESP32 BLE gamepad to OBS via WebSocket")
    parser.add_argument(
        "--host",
        default="localhost",
        help="OBS WebSocket host (default: localhost)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=4455,
        help="OBS WebSocket port (default: 4455)",
    )
    parser.add_argument(
        "--password",
        default="",
        help="Password for OBS WebSocket if enabled",
    )
    parser.add_argument(
        "--mapping",
        type=Path,
        default=Path(__file__).with_name("mapping.json"),
        help="Path to JSON button‑to‑OBS mapping file",
    )

    args = parser.parse_args()

    # Initialise OBS bridge and connect.
    bridge = ObsBridge(
        host=args.host,
        port=args.port,
        password=args.password,
        mapping_path=args.mapping,
    )
    try:
        bridge.connect()
    except RuntimeError as exc:
        print(f"Failed to connect to OBS: {exc}")
        sys.exit(1)

    # Initialise gamepad listener.
    listener = GamepadListener()
    listener.register_callback(lambda idx, pressed: bridge.handle_button(idx, pressed))
    listener.start()

    print("Gamepad → OBS bridge running. Press Ctrl‑C to exit.")
    # Keep the main thread alive; signal handler will terminate.
    signal.signal(signal.SIGINT, _signal_handler)
    signal.pause()  # wait indefinitely until a signal arrives


if __name__ == "__main__":
    main()

