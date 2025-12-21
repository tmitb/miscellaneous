# ESP32 BLE Gamepad
This branch contains a project that is composed of both hardware and software stacks for a gamepad based on ESP32-C3. This project is started based on a personal need for a button pad to control OBS while playing games.

The information and experience gathered from this project will be used for the following potential projects
- Racing sim wheel
- Racing sim button stack
- Flight sim controls

## Hardware
Because I have a ton laying around, I picked ESP32-C3.

### Design consideration
- The actual Dev board I have is ESP32-C3 Super mini. Due to its size, it only has 11 GPIO to safely use after removing two buttons, for the Boot button and the LED. 11 buttons will be more than enough for the initial version for controlling OBS.
- The shell will be 3d printed
- Mechnical keyboard switches are going to be used for buttons

### BOM
- [*] ESP32-C3 Super mini
- [ ] Shell
- [*] Switches
- [ ] Key caps

## Software
The software stack will be a background service that detects game pad inputs and convert them into OBS websocket commands. It will only be designed for Windows OS, because I am not planning to use it in other OSes.


