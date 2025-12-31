namespace ObsController.Services;

/// <summary>
/// A do‑nothing implementation of IGamepadProvider used when no physical controller is present.
/// It satisfies the interface so that the rest of the application can start and shut down cleanly.
/// </summary>
public sealed class NullGamepadProvider : IGamepadProvider
{
    public event System.Action<string>? ButtonDown;
    public event System.Action<string>? ButtonUp;

    public void Start() { /* no polling */ }
    public void Stop()  { /* no polling */ }
    public void Dispose() { /* nothing to clean up */ }
}
