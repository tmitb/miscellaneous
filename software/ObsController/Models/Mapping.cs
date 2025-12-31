using System.Collections.Generic;

namespace ObsController.Models;

/// <summary>
/// Represents the JSON structure used by the original Python project.
/// Example (mapping.json):
/// {
///   "deviceId": 0,
///   "host": "localhost",
///   "port": 4455,
///   "password": "secret",
///   "buttonMap": {
///     "A": { "action": "StartStreaming" },
///     "B": { "action": "StopStreaming" }
///   }
/// }
/// </summary>
public class Mapping
{
    public int DeviceId { get; set; } = 0;
    /// <summary>
    /// Optional GUID of the gamepad as exposed by Windows.Gaming.Input.Gamepad.Id.
    /// Allows disambiguation when multiple controllers are present.
    /// </summary>
    public string? DeviceGuid { get; set; }

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4455;
    public string? Password { get; set; }

    // Button name (as reported by the provider) -> action definition
    public Dictionary<string, ButtonAction>? ButtonMap { get; set; }
}

public class ButtonAction
{
    /// <summary>
    /// Name of the OBS action to invoke. The controller translates this to a concrete OBS request.
    /// Supported values: StartStreaming, StopStreaming, ToggleRecording, SwitchScene, etc.
    /// </summary>
    public string? Action { get; set; }

    // Optional parameter for actions that need extra data (e.g., scene name)
    public string? Parameter { get; set; }
}
