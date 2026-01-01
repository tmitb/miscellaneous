namespace ObsController.Services;

/// <summary>
/// A do‑nothing implementation of IGamepadProvider used when no physical controller is present.
/// It satisfies the interface so that the rest of the application can start and shut down cleanly.
/// </summary>
using ObsController.Models;

public sealed class NullGamepadProvider : IGamepadProvider
{
    // No actual controller, so no state changes will ever be emitted.
    public event System.Action<ControllerDelta> StateChanged;

    public void Start() { /* no polling */ }
    public void Stop()  { /* no polling */ }
    public void Dispose() { /* nothing to clean up */ }
}
