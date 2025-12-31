using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Gaming.Input;
using ObsController.Models;

namespace ObsController.Services;

/// <summary>
/// Example (non‑functional) implementation that demonstrates how a real <see cref="IGamepadProvider"/> could be built
/// on top of <c>Windows.Gaming.Input.RawGameController</c>. The code shows the typical steps:
///   1. Locate the desired <c>RawGameController</c> instance.
///   2. Periodically request an input report (the raw HID data).
///   3. Decode that byte array according to the device’s HID report descriptor.
///   4. Translate decoded button/axis values into logical button names that match <c>mapping.json</c>.
///
/// Because we do not have the actual HID specification for the BLE controller, this implementation contains
/// placeholder logic – it assumes a very simple report where the first byte is a bit‑mask of eight buttons:
///   0x01 = A, 0x02 = B, 0x04 = X, 0x08 = Y, 0x10 = LB, 0x20 = RB, 0x40 = DPadUp, 0x80 = DPadDown.
/// Replace the <c>DecodeReport</c> method with the real parsing needed for your device.
/// </summary>
public sealed class RealRawGamepadProvider : IGamepadProvider
{
    private readonly RawGameController _controller;
    private readonly Timer _timer; // poll at a modest rate (e.g., 30 Hz)
    private bool _running;
    // Previous raw state used for diffing. Null until the first successful poll.
    private ObsController.Models.ControllerState _previousState;

    // Legacy per‑button events (kept for backward compatibility)
    public event Action<string> ButtonDown;
    public event Action<string> ButtonUp;
    // New high‑level event that delivers only the differences between two snapshots.
    public event Action<ObsController.Models.ControllerDelta> StateChanged;

    /// <summary>
    /// Constructs the provider for a concrete <c>RawGameController</c> instance.
    /// </summary>
    public RealRawGamepadProvider(RawGameController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        // 33 ms ≈ 30 Hz polling – sufficient for most game‑pad use cases.
        _timer = new Timer(Poll, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _timer.Change(0, 33);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Poll(object state)
    {
        // RawGameController exposes GetCurrentReading which returns a timestamp and an IReadOnlyList<object>
        // representing the raw report bytes. The exact type depends on the controller – for many BLE devices it is
        // returned as a byte[] wrapped in a Windows.Gaming.Input.GamepadReading.
        try
        {
            // Gather raw values directly from the controller into index‑based collections.
            bool[]   buttonVals = new bool[_controller.ButtonCount];
            // The RawGameController exposes a SwitchCount property. Use it if present; otherwise fall back to ButtonCount.
            int switchCount = (_controller.GetType().GetProperty("SwitchCount")?.GetValue(_controller) as int?) ?? _controller.ButtonCount;
            GameControllerSwitchPosition[] switchVals = new GameControllerSwitchPosition[switchCount];
            double[] axisVals   = new double[_controller.AxisCount];

            _controller.GetCurrentReading(buttonVals, switchVals, axisVals);

            var currentState = new ObsController.Models.ControllerState();

            // Populate dictionaries with index → value.
            for (int i = 0; i < buttonVals.Length; i++)
                currentState.Buttons[i] = buttonVals[i];

            for (int i = 0; i < switchVals.Length; i++)
                currentState.Switches[i] = (int)switchVals[i];

            for (int i = 0; i < axisVals.Length; i++)
                currentState.Axes[i] = axisVals[i];

            // If we have a previous snapshot, compute the delta and raise events.
            if (_previousState != null)
            {
                var delta = currentState.Diff(_previousState);
                StateChanged?.Invoke(delta);
                RaiseLegacyButtonEvents(delta.ButtonsChanged);
            }

            // Store a copy for the next poll.
            _previousState = currentState.Clone();
        }
        catch (Exception ex)
        {
            // In production you would log this rather than swallow it.
            Console.WriteLine($"[ERROR] Exception while polling RawGameController: {ex.Message}");
        }
    }

    // Helper that preserves the original per‑button events using the diff dictionary.
    private void RaiseLegacyButtonEvents(IReadOnlyDictionary<int, bool> changedButtons)
    {
        foreach (var kv in changedButtons)
        {
            int index = kv.Key;
            bool pressed = kv.Value;
            // Convert the button index to the bit flag used by ButtonMap.
            byte flag = (byte)(1 << index);
            if (!ButtonMap.TryGetValue(flag, out var name))
                continue; // unknown mapping – ignore

            if (pressed) ButtonDown?.Invoke(name);
            else          ButtonUp?.Invoke(name);
        }
    }

    // Mapping of bit positions to logical button names – adjust to match your device's report format.
    private static readonly Dictionary<byte, string> ButtonMap = new()
    {
        { 0x01, "A" },
        { 0x02, "B" },
        { 0x04, "X" },
        { 0x08, "Y" },
        { 0x10, "LB" },
        { 0x20, "RB" },
        { 0x40, "DPadUp" },
        { 0x80, "DPadDown" }
    };

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
        // RawGameController does not implement IDisposable, so nothing else is required.
    }
}
