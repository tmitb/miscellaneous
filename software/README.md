# ESP32 BLE Gamepad → OBS Controller (Python implementation)

This sub‑directory contains the **software side** of the project – a small Python
application that reads button presses from an ESP32‑C3 Bluetooth gamepad and
translates them into OBS WebSocket commands.

## Quick start

1. **Create a virtual environment** (optional but recommended)
   ```bash
   python -m venv .venv
   source .venv/bin/activate  # on Windows use `.venv\\Scripts\\activate`
   ```
2. **Install dependencies** using the provided requirements file:
   ```bash
   pip install -r requirements.txt
   ```
3. **Edit the button mapping** (`mapping.json`) if you want custom actions.
4. **Run the bridge**
   ```bash
   python run_app.py            # uses default OBS host/port/password
   # or specify options:
   python run_app.py --host 127.0.0.1 --port 4455 --password mypass
   ```

Press a button on the paired gamepad – you should see the corresponding action
performed in OBS (scene change, start/stop recording, mute toggle, …).

## Building a standalone Windows executable

If you want to distribute the controller without requiring users to install
Python, use **PyInstaller**:
```bash
pyinstaller --onefile --name ble-obs-controller run_app.py
```
The generated `dist/ble-obs-controller.exe` can be copied to any Windows PC
running OBS with the WebSocket plugin enabled.

## Project structure (under `software/`)

- `mapping.json` – JSON file that maps button indices (0‑9) to OBS actions.
- `gamepad.py`   – Minimal wrapper around the `inputs` library that emits
  `(index, pressed)` callbacks.
- `obs_bridge.py` – Handles the WebSocket connection and dispatches commands
  based on the mapping.
- `run_app.py`   – CLI entry point that ties everything together.

## Known limitations

- Only Windows is currently supported (the `inputs` library works cross‑platform,
  but HID access for background services varies by OS).
- The listener assumes a single gamepad; additional devices are ignored.

---

Feel free to open issues or pull requests if you want to add more OBS actions
or improve the UI. Happy hacking!
