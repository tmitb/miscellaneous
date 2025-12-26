/*
 * A simple sketch that maps a single pin on the ESP32 to a single button on the controller
 */

#include <Arduino.h>
#include <GamepadDevice.h>
#include <BleCompositeHID.h>

BleCompositeHID compositeHID("2x5 PAD");
GamepadDevice* gamepad;
short enabledButtons[] = { 0, 1, 2, 3, 4, 5, 6, 7, 10, 20, 21 };
int previousButtonStates[] = { HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH };

void setup() {
  // Start the serial connection. It will be probably removed once the code is stabilized
  Serial.begin(115200);

  // Setup buttons
  Serial.println("Setup pins");
  for (int ctr = 0; ctr < sizeof(enabledButtons) / sizeof(enabledButtons[0]); ctr++) {
    pinMode(enabledButtons[ctr], INPUT_PULLUP);
  }

  // Start game pad
  Serial.println("Setup the game pad");
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
          Serial.print(enabledButtons[ctr]);
          Serial.println(" Pressed");
        } else {
          gamepad->press(ctr + 1);
          Serial.print(enabledButtons[ctr]);
          Serial.println(" Released");
        }
        previousButtonStates[ctr] = state;
      }

      // Update all buttons at once.
      compositeHID.sendDeferredReports();
    }
  }
}