namespace ObsController.Services;

using ObsController.Models;

/// <summary>
/// Minimal abstraction over a gamepad source. Allows the rest of the application to remain agnostic
/// about whether we are using XInput, Windows.Gaming.Input, DirectInput or a test mock.
/// </summary>
public interface IGamepadProvider : System.IDisposable
{
    // NOTE: The per‑button ButtonDown / ButtonUp events have been removed.
    // Consumers should use the high‑level StateChanged event instead.

    /// <summary>
    /// Raised when the controller state changes. Provides a <see cref="ControllerDelta"/> describing
    /// differences between two consecutive snapshots.
    /// </summary>
    event System.Action<ControllerDelta> StateChanged;

    /// <summary>Start polling / listening for input events.</summary>
    void Start();

    /// <summary>Stop receiving input events.</summary>
    void Stop();
}
