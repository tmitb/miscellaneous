using System;
using System.Collections.Generic;

namespace ObsController.Models
{
    /// <summary>
    /// Represents a snapshot of the entire controller state using index‑based collections.
    /// All values are stored exactly as they come from <c>RawGameController.GetCurrentReading</c>:
    ///   * Buttons – bool (true = pressed)
    ///   * Switches – int (the underlying enum value of <c>GameControllerSwitchPosition</c>)
    ///   * Axes – double (range depends on the device, typically -1..1)
    /// </summary>
    public sealed class ControllerState
    {
        public Dictionary<int, bool> Buttons { get; } = new();
        public Dictionary<int, int> Switches { get; } = new();
        public Dictionary<int, double> Axes { get; } = new();

        /// <summary>
        /// Creates a deep copy of the current state. Used to keep the previous snapshot for diffing.
        /// </summary>
        public ControllerState Clone()
        {
            var copy = new ControllerState();
            foreach (var kv in Buttons)   copy.Buttons[kv.Key]   = kv.Value;
            foreach (var kv in Switches)  copy.Switches[kv.Key]  = kv.Value;
            foreach (var kv in Axes)      copy.Axes[kv.Key]       = kv.Value;
            return copy;
        }

        /// <summary>
        /// Computes the differences between this state and a previous snapshot.
        /// Only entries whose value changed are included in the returned <see cref="ControllerDelta"/>.
        /// </summary>
        public ControllerDelta Diff(ControllerState previous)
        {
            var btnChanged   = new Dictionary<int, bool>();
            var swChanged    = new Dictionary<int, int>();
            var axisChanged  = new Dictionary<int, double>();

            foreach (var kv in Buttons)
                if (!previous.Buttons.TryGetValue(kv.Key, out var prev) || prev != kv.Value)
                    btnChanged[kv.Key] = kv.Value;

            foreach (var kv in Switches)
                if (!previous.Switches.TryGetValue(kv.Key, out var prev) || prev != kv.Value)
                    swChanged[kv.Key] = kv.Value;

            const double Tolerance = 1e-6; // avoid noise on floating point
            foreach (var kv in Axes)
                if (!previous.Axes.TryGetValue(kv.Key, out var prev) || Math.Abs(prev - kv.Value) > Tolerance)
                    axisChanged[kv.Key] = kv.Value;

            return new ControllerDelta(btnChanged, swChanged, axisChanged);
        }
    }

    /// <summary>
    /// Holds the minimal set of changed entries between two <see cref="ControllerState"/> snapshots.
    /// Consumers can iterate over these dictionaries to know exactly what changed.
    /// </summary>
    public sealed class ControllerDelta
    {
        public IReadOnlyDictionary<int, bool>   ButtonsChanged  { get; }
        public IReadOnlyDictionary<int, int>    SwitchesChanged { get; }
        public IReadOnlyDictionary<int, double> AxesChanged     { get; }

        public ControllerDelta(
            IDictionary<int, bool> buttons,
            IDictionary<int, int> switches,
            IDictionary<int, double> axes)
        {
            ButtonsChanged  = new Dictionary<int, bool>(buttons);
            SwitchesChanged = new Dictionary<int, int>(switches);
            AxesChanged     = new Dictionary<int, double>(axes);
        }
    }
}

