"""OBS WebSocket bridge.

Provides a thin wrapper around ``obs-websocket-py`` that loads a JSON mapping of
button indices → OBS actions and sends the appropriate command when requested.
The public class :class:`ObsBridge` maintains a persistent connection to the
OBS WebSocket server (default ``ws://localhost:4455``) and exposes a single method
``handle_button(idx, pressed)`` which should be called by the gamepad listener.

Only *press* events trigger actions – releases are ignored because most OBS
commands are idempotent (e.g., start/stop recording). The mapping format matches
the ``software/mapping.json`` file created earlier.
"""

from __future__ import annotations

import json
import pathlib
from typing import Any, Dict

# Import obswebsocket lazily; the library may not be installed in all environments.
try:
    from obswebsocket import obsws, requests  # type: ignore
except Exception:  # pragma: no cover – optional dependency
    obsws = None  # type: ignore[assignment]
    requests = None  # type: ignore[assignment]

_DEFAULT_MAPPING_PATH = pathlib.Path(__file__).with_name("mapping.json")


class ObsBridge:
    """Maintain a connection to OBS and dispatch commands based on button presses.

    Supports loading the mapping either from a JSON file (legacy) or directly from
    a dictionary supplied via ``mapping_dict``. This enables a unified config
    file that contains both connection settings and the button‑to‑OBS mapping."""

    def __init__(
        self,
        host: str = "localhost",
        port: int = 4455,
        password: str | None = None,
        mapping_path: pathlib.Path | None = None,
        mapping_dict: Dict[str, Dict[str, Any]] | None = None,
    ) -> None:
        self.host = host
        self.port = port
        self.password = password or ""
        # ``mapping_path`` retained for compatibility; may be unused when a dict is provided.
        self.mapping_path = mapping_path or _DEFAULT_MAPPING_PATH
        self._ws: obsws | None = None
        # Use supplied dict if given, otherwise load lazily from file.
        self._mapping: Dict[str, Dict[str, Any]] = mapping_dict or {}

    # ------------------------------------------------------------------
    # Connection handling
    # ------------------------------------------------------------------
    def connect(self) -> None:
        """Open the WebSocket connection and load the button mapping.

        This method is idempotent – calling it when already connected does nothing.
        """
        if self._ws is not None:
            return
        # Ensure the optional dependency is available before creating a client.
        if obsws is None:
            raise RuntimeError(
                "obs-websocket-py is required to connect to OBS. Install it via\n"
                "    pip install obs-websocket-py"
            )
        self._ws = obsws(self.host, self.port, self.password)
        try:
            self._ws.connect()
        except Exception as exc:  # pragma: no cover – connection failures are runtime
            self._ws = None
            raise RuntimeError(f"Failed to connect to OBS WebSocket: {exc}") from exc

        # Load mapping JSON only if a mapping hasn't been supplied already.
        if not self._mapping:
            try:
                with open(self.mapping_path, "r", encoding="utf-8") as f:
                    self._mapping = json.load(f)
            except FileNotFoundError as exc:
                raise RuntimeError(
                    f"Mapping file not found at {self.mapping_path}. Create it first."
                ) from exc

    def disconnect(self) -> None:
        if self._ws is not None:
            try:
                self._ws.disconnect()
            finally:
                self._ws = None

    # ------------------------------------------------------------------
    # Public API used by the gamepad listener
    # ------------------------------------------------------------------
    def handle_button(self, idx: int, pressed: bool) -> None:
        """Dispatch an OBS command for a *pressed* button.

        ``idx`` is the zero‑based button index from :class:`GamepadListener`.
        If the mapping does not contain the index or if ``pressed`` is ``False``
        the call is ignored.
        """
        if not pressed:
            return

        entry = self._mapping.get(str(idx))
        if not entry:
            # Silently ignore unmapped buttons – useful during development.
            return

        action: str = entry["action"]
        params: Dict[str, Any] = entry.get("params", {})
        self._dispatch(action, params)

    # ------------------------------------------------------------------
    # Internal command dispatch helper
    # ------------------------------------------------------------------
    def _dispatch(self, action: str, params: Dict[str, Any]) -> None:
        if self._ws is None:
            raise RuntimeError("OBS bridge not connected. Call connect() first.")

        # Map a limited set of actions to obs-websocket-py request objects.
        try:
            if requests is None:
                raise RuntimeError(
                    "obs-websocket-py is required to dispatch OBS actions. Install it via\n"
                    "    pip install obs-websocket-py"
                )
            if action == "SetCurrentScene":
                self._ws.call(requests.SetCurrentProgramScene(**params))
            elif action == "StartRecording":
                self._ws.call(requests.StartRecord())
            elif action == "StopRecording":
                self._ws.call(requests.StopRecord())
            elif action == "ToggleMute":
                # Expected param: source name.
                self._ws.call(requests.ToggleMute(**params))
            else:
                print(f"[ObsBridge] Unknown action '{action}'. Ignored.")
        except Exception as exc:  # pragma: no cover – runtime communication errors
            print(f"[ObsBridge] Failed to execute {action}: {exc}")
