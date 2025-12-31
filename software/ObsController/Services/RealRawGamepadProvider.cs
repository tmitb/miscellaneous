using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Gaming.Input;

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
    private byte _previousMask = 0;

    public event Action<string>? ButtonDown;
    public event Action<string>? ButtonUp;

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

    private void Poll(object? state)
    {
        // RawGameController exposes GetCurrentReading which returns a timestamp and an IReadOnlyList<object>
        // representing the raw report bytes. The exact type depends on the controller – for many BLE devices it is
        // returned as a byte[] wrapped in a Windows.Gaming.Input.GamepadReading.
        try
        {
            bool[] buttons = new bool[_controller.ButtonCount];
            GameControllerSwitchPosition[] switches = new GameControllerSwitchPosition[_controller.ButtonCount];
            double[] axes = new double[_controller.AxisCount];
            _controller.GetCurrentReading(buttons, switches, axes);


            // The API gives us an IReadOnlyList<object>. Most often each element is a byte.
            // Convert to a plain byte[] for easier handling.
            var reportBytes = new List<byte>();
            foreach (var obj in buttons)
            {
                reportBytes.Add(Convert.ToByte(obj));
            }
            foreach(var obj in switches)
            {
                reportBytes.Add(Convert.ToByte((int)obj));
            }
            foreach(var obj in axes)
            {
                reportBytes.Add(Convert.ToByte(obj));
            }

            DecodeAndRaiseEvents(reportBytes.ToArray());
        }
        catch (Exception ex)
        {
            // In a production implementation you would log this rather than swallow it.
            Console.WriteLine($"[ERROR] Exception while polling RawGameController: {ex.Message}");
        }
    }

    /// <summary>
    /// Decodes the raw HID report and raises ButtonDown/ButtonUp events based on edge detection.
    /// The placeholder implementation looks at the first byte as a simple button mask.
    /// </summary>
    private void DecodeAndRaiseEvents(byte[] report)
    {
        if (report.Length == 0) return;

        byte currentMask = report[0]; // first byte = button bitmap in our example

        // Detect newly pressed bits
        byte down = (byte)(currentMask & ~_previousMask);
        // Detect released bits
        byte up = (byte)(_previousMask & ~currentMask);

        foreach (var kvp in ButtonMap)
        {
            var flag = kvp.Key;
            var name = kvp.Value;
            if ((down & flag) != 0) ButtonDown?.Invoke(name);
            if ((up & flag) != 0) ButtonUp?.Invoke(name);
        }

        _previousMask = currentMask;
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

