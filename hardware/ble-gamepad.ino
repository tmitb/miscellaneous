/* Simple button panel
    Copyright David Jung

    This project is designed to create a BLE gamepad with 11 buttons using ESP32-C3 Super mini
*/
#include <Arduino.h>
#include <GamepadDevice.h>
#include <BleCompositeHID.h>

BleCompositeHID compositeHID("11 Button Pad");
GamepadDevice* gamepad;
short enabledButtons[] = { 0, 1, 2, 3, 4, 5, 6, 7, 10, 20, 21 }; // All available GPIOs for ESP32-C3 super mini
int previousButtonStates[] = { HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH };

void setup() {
  // Setup buttons
  for (int ctr = 0; ctr < sizeof(enabledButtons) / sizeof(enabledButtons[0]); ctr++) {
    pinMode(enabledButtons[ctr], INPUT_PULLUP);
  }

  // Start game pad
  GamepadConfiguration config;
  config.setButtonCount(sizeof(enabledButtons) / sizeof(enabledButtons[0]));
  config.setHatSwitchCount(0);
  config.setIncludeXAxis(false);
  config.setIncludeYAxis(false);
  config.setIncludeZAxis(false);
  config.setIncludeRxAxis(false);
  config.setIncludeRyAxis(false);
  config.setIncludeRzAxis(false);
  config.setIncludeSlider1(false);
  config.setIncludeSlider2(false);
  config.setAutoReport(true);
  config.setAutoDefer(true);

  gamepad = new GamepadDevice(config);

  compositeHID.addDevice(gamepad);
  compositeHID.begin();
}

void loop() {
  if (compositeHID.isConnected()) {
    for (int ctr = 0; ctr < sizeof(enabledButtons) / sizeof(enabledButtons[0]); ctr++) {
      int state = digitalRead(enabledButtons[ctr]);
      if (state != previousButtonStates[ctr]) {
        if (previousButtonStates[ctr] == LOW) {
          gamepad->release(ctr + 1);
        } else {
          gamepad->press(ctr + 1);
        }
        previousButtonStates[ctr] = state;
      }

      // Update all buttons at once at the end of loop cycle.
      compositeHID.sendDeferredReports();
    }
  }
}