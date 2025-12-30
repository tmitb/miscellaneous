"""Gamepad input handling for Windows.

This module provides a lightweight wrapper around the `inputs` library that reads
button events from the first detected gamepad device. It normalises button
indices to ``0``‑based integers and forwards them via a user supplied callback.

The implementation is deliberately simple – it runs in its own thread and
continues reading until the process exits. Errors are logged to stdout; the
caller can decide how to handle missing devices.
"""

from __future__ import annotations

import threading
import time
from typing import Callable, Dict, List

try:
    # The `inputs` package works on Windows and Linux. It provides ``get_gamepad``
    # which yields ``InputEvent`` objects.
    from inputs import get_gamepad  # type: ignore
except Exception as exc:  # pragma: no cover – imported lazily during runtime
    raise RuntimeError(
        "The 'inputs' library is required for gamepad support. Install it via\n"
        "    pip install inputs"
    ) from exc

# Mapping of raw button codes to sequential indices.
# The `inputs` library reports names like "BTN_SOUTH", "BTN_EAST" etc.
_BUTTON_ORDER: List[str] = [
    "BTN_SOUTH",
    "BTN_EAST",
    "BTN_NORTH",
    "BTN_WEST",
    "BTN_TL",
    "BTN_TR",
    "BTN_SELECT",
    "BTN_START",
    "BTN_THUMBL",
    "BTN_THUMBR",
]

_code_to_index: Dict[str, int] = {name: idx for idx, name in enumerate(_BUTTON_ORDER)}


class GamepadListener:
    """Continuously read button presses and invoke a callback.

    The callback receives the *zero‑based* button index (int) and a boolean
    indicating whether the button was pressed (`True`) or released (`False`).
    """

    def __init__(self) -> None:
        self._callback: Callable[[int, bool], None] | None = None
        self._thread: threading.Thread | None = None
        self._running = False

    # ---------------------------------------------------------------------
    # Public API
    # ---------------------------------------------------------------------
    def register_callback(self, func: Callable[[int, bool], None]) -> None:
        """Set the function that will be called for each button event."""
        self._callback = func

    def start(self) -> None:
        """Start the background thread that reads events.

        The method is idempotent – calling it multiple times has no effect after
        the first successful start.
        """
        if self._running:
            return
        self._running = True
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Signal the background thread to terminate gracefully."""
        self._running = False
        if self._thread is not None:
            self._thread.join(timeout=2)

    # ---------------------------------------------------------------------
    # Internal helpers
    # ---------------------------------------------------------------------
    def _run(self) -> None:
        """Main loop – fetch events from ``inputs.get_gamepad``.

        The function tolerates temporary device disconnects by sleeping a short
        interval before retrying.
        """
        while self._running:
            try:
                for event in get_gamepad():
                    if not self._running:
                        break
                    # We're only interested in button events.
                    if event.code not in _code_to_index:
                        continue
                    idx = _code_to_index[event.code]
                    pressed = bool(event.state)  # 1 == press, 0 == release
                    if self._callback is not None:
                        try:
                            self._callback(idx, pressed)
                        except Exception as exc:  # pragma: no cover – defensive
                            print(f"Gamepad callback error: {exc}")
            except OSError:
                # Device may be unplugged; wait and retry.
                time.sleep(1.0)
            except Exception as exc:  # pragma: no cover – unexpected errors
                print(f"Unexpected gamepad read error: {exc}")
                time.sleep(0.5)

