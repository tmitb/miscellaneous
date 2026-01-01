# BLE‑OBS Controller

## Overview

This repository contains the source code for a **BLE gamepad → OBS** controller. A small ESP32‑C3 board runs custom firmware that emulates a Bluetooth Low Energy (BLE) gamepad. The companion desktop application, written in C#, reads the BLE gamepad input on Windows and forwards button presses to **OBS Studio** over its WebSocket API.

The project is focusing on the software portion of the entire solution.

* `software/` – a .NET console application located in the **ObsController** solution. It handles:
  * Loading a JSON mapping file (`mapping.json`).
  * Connecting to OBS via WebSocket.
  * Listening for button state changes from the BLE gamepad (through Windows.Gaming.Input).
  * Executing the configured OBS actions (start/stop streaming, toggle recording, switch scenes, …).

The README below explains how to build, configure and run the software on a Windows machine.

---

## Prerequisites

* **Windows 10/11** – the controller uses `Windows.Gaming.Input.RawGameController` which is only available on Windows.
* **.NET 8 SDK** – required to build the C# project (`dotnet build`).
* **OBS Studio** with the *WebSocket* plugin enabled (default in OBS 28+). Make sure you note the WebSocket port and password.
* (Optional) **ESP32‑C3 board** flashed with `hardware/ble-gamepad/ble-gamepad.ino` if you want to use the hardware version.

---

## Getting Started

### 1. Clone the repository

```powershell
git clone https://github.com/tmitb/ble-obs-controller.git
cd ble-obs-controller
```

### 2. Build the .NET application

```powershell
dotnet restore                # restores NuGet packages
dotnet build -c Release       # builds ObsController.exe in `bin/Release/net8.0-windows`
```

If you prefer a single‑file executable you can also run:

```powershell
dotnet publish -c Release /p:PublishSingleFile=true /p:SelfContained=true
```

The resulting `ObsController.exe` will be placed in the publish folder.

---

## Configuration (`mapping.json`)

`mapping.json` defines how button indices map to OBS actions and also stores the connection details for OBS.

```json
{
  "deviceIdentifier": "<NonRoamableId of your BLE gamepad>",
  "host": "localhost",
  "port": 4455,
  "password": "your‑obs‑websocket‑password",
  "buttonMap": {
    "0": { "action": "StartStreaming" },
    "1": { "action": "StopStreaming" },
    "2": { "action": "ToggleRecording" },
    "3": { "action": "SwitchScene", "parameter": "Game" }
  }
}
```

* **`deviceIdentifier`** – the id reported by Windows for the BLE gamepad. When you first run the app it prints a list of detected controllers; copy the `Id` value that matches your hardware. If not sure, try each one of them until you find the one you want.
* **Button map keys** are *zero‑based indices* as defined by the controller firmware. Windows test app is using 1 based button scheme so you may see the numbers differ by 1.
* Supported actions (as of today) are:
  * `StartStreaming`
  * `StopStreaming`
  * `ToggleRecording`
  * `SwitchScene` – requires a `parameter` containing the target scene name.
* New OBS action can be implemented based on the official document [found here](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md#requests)
  * There are two places that must be edited. One switch statement in `Programs.cs` file and a new function in `ObsBridge.cs` file must be added to support a new function.
  * No plan to add more unless I need them. Contribution are welcome.

You can extend the JSON schema to add more actions by editing `ObsController/Models/Mapping.cs` and implementing the corresponding case in `Program.HandleStateChanges`.


---

## Running the controller

```powershell
# From the project root (or wherever ObsController.exe lives)
.\ObsController\bin\Release\net8.0-windows\ObsController.exe \
    --host localhost \
    --port 4455 \
    --password "mySecret"
```

All three command‑line options are *optional* – if omitted the values from `mapping.json` are used. The program will:

1. Load `mapping.json`.
2. Connect to OBS via WebSocket.
3. Enumerate available gamepads and print their IDs (helpful for setting `deviceIdentifier`).
4. Listen for button presses and forward the configured actions to OBS.
5. Keep running until you press **Ctrl +C**.

---

## Building a Windows executable (optional)

If you want a portable single‑file binary you can use `dotnet publish` as shown earlier, or the classic *PyInstaller* approach from the original Python project is no longer needed because this implementation ships as a native .NET app.

---

## Contributing

Please follow the repository guidelines in `AGENTS.md`:

* Use **4‑space indentation** and PEP 8 / C# coding conventions.
* Keep commit messages concise (≤50 characters) with an optional body.
* Update `mapping.json` or the code when adding new OBS actions.
* Do not commit real OBS passwords – use environment variables (`OBS_PASSWORD`) or the command‑line overrides.

Open a PR that references an existing issue, includes a clear description of the changes and any new dependencies.

---

## License

This project is licensed under the **MIT License** – see `LICENSE` for details.
