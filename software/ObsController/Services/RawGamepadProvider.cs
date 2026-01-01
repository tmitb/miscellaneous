using System;
using Windows.Gaming.Input;

namespace ObsController.Services;

/// <summary>
/// Helper that selects a <see cref="RawGameController"/> from the system and wraps it in a <see cref="RealRawGamepadProvider"/>.
/// If no raw controllers are present, <c>null</c> is returned so callers can fall back to other strategies.
/// </summary>
public static class RawGamepadProvider
{
    /// <summary>
    /// Attempts to locate a controller based on the optional GUID or index supplied in the mapping file.
    /// Returns an <see cref="IGamepadProvider"/> implementation (the real one) or <c>null</c> if none are found.
    /// </summary>
    public static IGamepadProvider TryCreate(string deviceId)
    {
        
        var count = 0;
        // RawGameController.RawGameControllers are designed to be asynchronous internally and the values are not available]
        // instantly at the end of the function calls. The code keeps calling it for up to 10 seconds to make sure it gets
        // updated before giving up.
        while (RawGameController.RawGameControllers.Count == 0 && count < 10)
        {
            Thread.Sleep(1000);
            count++;
        }
        
        if (RawGameController.RawGameControllers.Count == 0)
            return new NullGamepadProvider(); // No raw gamepads available on this machine.

        // If an Id is supplied, try to match it first.
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            foreach (var rc in RawGameController.RawGameControllers)
            {
                if (rc.NonRoamableId.Replace("\0", "").Equals(deviceId))
                    return new RealRawGamepadProvider(rc);
            }
        }

        // Otherwise select by index (or fall back to the first controller).
        var selected = RawGameController.RawGameControllers[0];

        Console.WriteLine($"[INFO] RawGameController selection failed. Using the first controller – Id: {selected.NonRoamableId}");
        return new RealRawGamepadProvider(selected);
    }
}

