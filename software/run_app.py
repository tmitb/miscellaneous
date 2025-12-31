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

from gamepad import GamepadListener
# Direct use of the async OBS client wrapper instead of the legacy ObsBridge.
from obs_adapter import OBSClient
import asyncio  # retained for potential future async needs (currently not used)
import asyncio


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
        "--config",
        type=Path,
        default=Path(__file__).with_name("config.json"),
        help="Path to unified configuration JSON (host, port, password, mapping)",
    )

    args = parser.parse_args()

    # Load unified config file (JSON) – contains host/port/password and optional mapping.
    try:
        import json

        with open(args.config, "r", encoding="utf-8") as f:
            cfg = json.load(f)
    except FileNotFoundError:
        print(f"Config file not found at {args.config}. Using defaults.")
        cfg = {}

    # Resolve parameters, CLI args take precedence over config values.
    host = cfg.get("host", args.host)
    port = cfg.get("port", args.port)
    password = cfg.get("password", args.password)
    mapping_dict: dict | None = cfg.get("mapping")

    # Initialise OBS client and connect.
    obs_client = OBSClient(host=host, port=int(port), password=password)
    try:
        # ``OBSClient.connect`` is synchronous now.
        obs_client.connect()
    except RuntimeError as exc:
        print(f"Failed to connect to OBS: {exc}")
        sys.exit(1)

    # Helper to dispatch a button press using the mapping (if any).
    def _handle_button(idx: int, pressed: bool) -> None:
        if not pressed:
            return
        # Prefer mapping supplied via config; fall back to loading from default file.
        mapping = mapping_dict or {}
        if not mapping:
            # Load the legacy mapping.json file on‑demand.
            try:
                with open(Path(__file__).with_name("mapping.json"), "r", encoding="utf-8") as f:
                    mapping = json.load(f)
            except FileNotFoundError:
                print("No button mapping found – ignoring button events.")
                return
        entry = mapping.get(str(idx))
        if not entry:
            return
        action: str = entry["action"]
        params: dict = entry.get("params", {})
        try:
            # ``dispatch`` is synchronous.
            obs_client.dispatch(action, params)
        except Exception as exc:
            print(f"[run_app] Failed to execute {action}: {exc}")

    # Initialise gamepad listener.
    listener = GamepadListener()
    listener.register_callback(_handle_button)
    listener.start()

    print("Gamepad → OBS bridge running. Press Ctrl‑C to exit.")
    # Keep the main thread alive; signal handler will terminate.
    signal.signal(signal.SIGINT, _signal_handler)
    try:
        # ``signal.pause`` is not available on Windows.
        signal.pause()
    except AttributeError:
        # Simple cross‑platform wait loop.
        import time
        while True:
            time.sleep(1)


if __name__ == "__main__":
    main()
