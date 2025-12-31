using System;
using System.Collections.Generic;
using SharpDX.XInput;

namespace ObsController.Services;

/// <summary>
/// Detects a specific XInput gamepad (by index) and raises events when buttons are pressed.
/// The button names correspond to the enum values of SharpDX.XInput.GamepadButtonFlags, but are
/// exposed as simple string keys that match the original Python mapping (e.g., "A", "B", "X", "Y").
/// </summary>
public class GamepadService : IDisposable
{
    private readonly Controller _controller;
    private readonly int _pollIntervalMs = 30; // ~33 Hz polling
    private bool _running;
    private System.Threading.CancellationTokenSource _cts;

    // Keep previous state to detect edge transitions (press/release)
    private GamepadButtonFlags _previousButtons = GamepadButtonFlags.None;

    public event Action<string> ButtonDown; // button name
    public event Action<string> ButtonUp;

    /// <summary>
    /// Creates a service for the controller with the given XInput index (0‑3).
    /// </summary>
    public GamepadService(int deviceId)
    {
        if (deviceId < 0 || deviceId > 3)
            throw new ArgumentOutOfRangeException(nameof(deviceId), "XInput supports devices 0‑3.");

        _controller = new Controller((UserIndex)deviceId);
        if (!_controller.IsConnected)
            throw new InvalidOperationException($"Gamepad device {deviceId} is not connected.");
    }

    /// <summary>
    /// Starts polling the controller for button state changes. Call Stop() or Dispose() to end.
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;
        System.Threading.Tasks.Task.Run(() => PollLoop(token), token);
    }

    private void PollLoop(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_controller.GetState(out var state))
            {
                var current = state.Gamepad.Buttons;
                // Detect newly pressed buttons
                var down = (current & ~_previousButtons);
                foreach (var flag in Enum.GetValues<GamepadButtonFlags>())
                {
                    if (flag == GamepadButtonFlags.None) continue;
                    if ((down & flag) != 0)
                        ButtonDown?.Invoke(ButtonFlagToName(flag));
                }

                // Detect released buttons
                var up = (_previousButtons & ~current);
                foreach (var flag in Enum.GetValues<GamepadButtonFlags>())
                {
                    if (flag == GamepadButtonFlags.None) continue;
                    if ((up & flag) != 0)
                        ButtonUp?.Invoke(ButtonFlagToName(flag));
                }

                _previousButtons = current;
            }
            System.Threading.Thread.Sleep(_pollIntervalMs);
        }
    }

    private static string ButtonFlagToName(GamepadButtonFlags flag) => flag switch
    {
        GamepadButtonFlags.A => "A",
        GamepadButtonFlags.B => "B",
        GamepadButtonFlags.X => "X",
        GamepadButtonFlags.Y => "Y",
        GamepadButtonFlags.Start => "Start",
        GamepadButtonFlags.Back => "Back",
        GamepadButtonFlags.LeftShoulder => "LB",
        GamepadButtonFlags.RightShoulder => "RB",
        GamepadButtonFlags.LeftThumb => "LThumb",
        GamepadButtonFlags.RightThumb => "RThumb",
        GamepadButtonFlags.DPadUp => "DPadUp",
        GamepadButtonFlags.DPadDown => "DPadDown",
        GamepadButtonFlags.DPadLeft => "DPadLeft",
        GamepadButtonFlags.DPadRight => "DPadRight",
        _ => flag.ToString()
    };

    public void Stop()
    {
        if (!_running) return;
        _cts?.Cancel();
        _running = false;
    }

    public void Dispose()
    {
        Stop();
        //_controller.Dispose();
    }
}
