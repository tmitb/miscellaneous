using System.Collections.Generic;

namespace ObsController.Models;

/// <summary>
/// Represents the JSON structure used by the original Python project.
/// Example (mapping.json):
/// {
///   "deviceIdentifier": "my-controller-id",
///   "host": "localhost",
///   "port": 4455,
///   "password": "secret",
///   "buttonMap": { "0": {"action":"StartStreaming"} },
///   "switchMap": { "0": {"action":"SetSourceVisibility", "parameter":"Camera,true"} },
///   "axisMap":   { "0": {"action":"SetVolume", "parameter":"Mic,{{value}}"} }
/// }
/// </summary>
public class Mapping
{
    // Identifier that matches RawGameController.NonRoamableId. This is a plain character string.
    public string DeviceIdentifier { get; set; }

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4455;
    public string Password { get; set; }

    // Index‑based dictionaries for buttons, switches and axes (keys are stringified integers)
    public Dictionary<string, ButtonAction> ButtonMap   { get; set; }
    public Dictionary<string, ButtonAction> SwitchMap   { get; set; }
    public Dictionary<string, ButtonAction> AxisMap     { get; set; }
}

public class ButtonAction
{
    /// <summary>
    /// Name of the OBS action to invoke.
    /// Supported values: StartStreaming, StopStreaming, ToggleRecording, SwitchScene, etc.
    /// </summary>
    public string Action { get; set; }

    // Optional parameter for actions that need extra data (e.g., scene name)
    public string Parameter { get; set; }
}

