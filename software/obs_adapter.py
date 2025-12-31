"""Thin async wrapper around the ``obsws-python`` library.

The original code used the synchronous ``obs-websocket-py`` package.  The new
library is fully asynchronous, so this adapter provides a small, stable API that
the rest of the project can call without being aware of the underlying async
implementation.

Only the actions required by the existing mapping are implemented – additional
OBS requests can be added here later.
"""

from __future__ import annotations

import asyncio
from typing import Any, Dict

# ``obsws_python`` provides a synchronous request client (ReqClient) that we can use directly.
# The older code expected an ``ObsWS`` class; we alias the appropriate client here.
try:
    from obsws_python.reqs import ReqClient as ObsWS  # type: ignore
except Exception as exc:  # pragma: no cover – library missing will be caught early.
    raise RuntimeError(
        "obsws-python is required. Install it with 'pip install obsws-python'"
    ) from exc


class OBSClient:
    """Synchronous client for communicating with OBS via ``obsws_python``.

    The public interface mirrors what the previous ``ObsBridge`` offered so
    existing code (now updated) can use it without further changes.
    """

    def __init__(self, host: str = "localhost", port: int = 4455, password: str | None = "") -> None:
        self.host = host
        self.port = port
        self.password = password or ""
        # ``ObsWS`` here is actually the ReqClient from obsws_python.
        self._client: ObsWS | None = None

    def connect(self) -> None:
        """Instantiate the underlying request client (connects automatically)."""
        if self._client is not None:
            return
        try:
            self._client = ObsWS(host=self.host, port=self.port, password=self.password)
        except Exception as exc:  # pragma: no cover – connection failures are runtime.
            raise RuntimeError(f"Failed to connect to OBS WebSocket: {exc}") from exc

    def disconnect(self) -> None:
        if self._client is not None:
            try:
                self._client.disconnect()
            finally:
                self._client = None

    def dispatch(self, action: str, params: Dict[str, Any] | None = None) -> None:
        """Send a request to OBS.

        ``action`` is the name used in our mapping file. We forward it to the
        underlying client via its generic ``send`` method. Unsupported actions are
        reported but ignored, matching prior behaviour.
        """
        if self._client is None:
            raise RuntimeError("OBS client not connected – call connect() first")

        params = params or {}
        try:
            # The library expects the request type name exactly as in the OBS
            # protocol (e.g., "SetCurrentProgramScene", "StartRecord", …).
            # We'll map our simplified action names to those request types.
            mapping = {
                "SetCurrentScene": "SetCurrentProgramScene",
                "StartRecording": "StartRecord",
                "StopRecording": "StopRecord",
                "ToggleMute": "ToggleMute",
            }
            req_type = mapping.get(action, action)
            self._client.send(req_type, params)
        except Exception as exc:  # pragma: no cover – runtime communication errors
            print(f"[OBSClient] Failed to execute {action}: {exc}")
