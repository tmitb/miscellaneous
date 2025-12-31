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
    public static IGamepadProvider? TryCreate(int deviceId, string? deviceGuid = null)
    {
        
        var count = 0;
        while (RawGameController.RawGameControllers.Count == 0 && count < 10)
        {
            Thread.Sleep(1000);
            count++;

        }
        
        if (RawGameController.RawGameControllers.Count == 0)
            return null; // No raw gamepads available on this machine.

        // If a GUID is supplied, try to match it first.
        if (!string.IsNullOrWhiteSpace(deviceGuid))
        {
            foreach (var rc in RawGameController.RawGameControllers)
            {
                if (rc.NonRoamableId.Equals(deviceGuid, StringComparison.OrdinalIgnoreCase))
                    return new RealRawGamepadProvider(rc);
            }
        }

        // Otherwise select by index (or fall back to the first controller).
        var selected = deviceId >= 0 && deviceId < RawGameController.RawGameControllers.Count
            ? RawGameController.RawGameControllers[deviceId]
            : RawGameController.RawGameControllers[0];

        Console.WriteLine($"[INFO] RawGameController detected – Id: {selected.NonRoamableId}");
        return new RealRawGamepadProvider(selected);
    }
}

