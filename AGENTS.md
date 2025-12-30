# Repository Guidelines

## Project Structure & Module Organization
```
. 
├─ hardware/        # 3‑D models and Arduino sketch
│   ├─ 3d-models/  # STL / PNG files for the shell
│   └─ ble-gamepad/ble-gamepad.ino  # ESP32‑C3 firmware
├─ images/          # Photos used in documentation
└─ software/        # Python OBS controller
    ├─ mapping.json      # Button → OBS action map
    ├─ gamepad.py        # Wrapper around `inputs` library
    ├─ obs_bridge.py     # WebSocket client for OBS
    ├─ run_app.py        # CLI entry point
    └─ requirements.txt  # Python dependencies
```
Source code lives under `software/`; tests (if added) should mirror this layout.

## Build, Test & Development Commands
- **Setup virtual environment**
  ```bash
  python -m venv .venv && source .venv/bin/activate
  ```
- **Install dependencies**: `pip install -r software/requirements.txt`
- **Run the controller**:
  ```bash
  python software/run_app.py [--host HOST] [--port PORT] [--password PASS]
  ```
  Starts the BLE listener and connects to OBS.
- **Create a Windows executable** (optional):
  `pyinstaller --onefile --name ble-obs-controller software/run_app.py`

## Coding Style & Naming Conventions
* Indentation: 4 spaces, no tabs.
* Python files follow PEP 8; use `flake8` for linting (`pip install flake8`).
* Constants in ALL_CAPS, classes in PascalCase, functions/variables in snake_case.
* Modules are named after their purpose (`gamepad.py`, `obs_bridge.py`).

## Testing Guidelines
* **Framework** – `pytest` (already listed in `requirements.txt`).
* Test files live alongside code or under a `tests/` directory, named `test_*.py`.
* Run all tests with `pytest`. Aim for ≥80 % coverage; use `pytest‑cov` to measure.

## Commit & Pull Request Guidelines
* **Commit messages** – short (≤50 chars) summary, followed by optional body. Example:
  ```
  feat: add mapping for start/stop recording
  
  Updated `mapping.json` to include OBS record toggle.
  ```
* PRs must reference an issue, include a brief description of changes, and list any new dependencies.
* If UI or behavior changes, attach screenshots or logs.

## Additional Tips
* **Security** – never commit actual OBS passwords; use environment variables (`OBS_PASSWORD`).
* **Configuration** – command‑line flags override values in `mapping.json`.
* **Hardware updates** – keep STL files versioned; any pin changes must be reflected in both the Arduino sketch and `software/mapping.json`.

---
These guidelines aim to keep contributions consistent and the repo easy to navigate. Feel free to suggest improvements via issues or PRs.
