namespace ObsController.Services;

/// <summary>
/// Minimal abstraction over a game‑pad source. Allows the rest of the application to remain agnostic
/// about whether we are using XInput, Windows.Gaming.Input, DirectInput or a test mock.
/// </summary>
public interface IGamepadProvider : System.IDisposable
{
    /// <summary>Raised when a logical button becomes pressed.</summary>
    event System.Action<string>? ButtonDown;

    /// <summary>Raised when a logical button is released.</summary>
    event System.Action<string>? ButtonUp;

    /// <summary>Start polling / listening for input events.</summary>
    void Start();

    /// <summary>Stop receiving input events.</summary>
    void Stop();
}
