using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using ObsController.Models;
using ObsController.Services;

namespace ObsController;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Define CLI options (all optional – they override mapping.json values)
        var hostOption = new Option<string>("--host", description: "OBS websocket host (default from mapping.json)");
        var portOption = new Option<int?>("--port", description: "OBS websocket port (default from mapping.json)");
        var passwordOption = new Option<string>("--password", description: "OBS websocket password (overrides mapping.json)");

        var rootCommand = new RootCommand("BLE‑OBS controller – reads a configurable gamepad and triggers OBS actions.")
        {
            hostOption,
            portOption,
            passwordOption
        };

        rootCommand.SetHandler(async (string? host, int? port, string? password) =>
        {
            // Load configuration from mapping.json
            Mapping mapping = ConfigLoader.Load();
            ConfigLoader.ApplyOverrides(mapping, host, port, password);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Initialise OBS bridge
            await using var obsBridge = new ObsBridge(mapping.Host, mapping.Port, mapping.Password);
            await obsBridge.ConnectAsync(cts.Token);
            Console.WriteLine($"Connected to OBS at {mapping.Host}:{mapping.Port}");

            // Initialise controller provider using the RawGameController API (covers BLE devices)

            IGamepadProvider? gp = RawGamepadProvider.TryCreate(mapping.DeviceId, mapping.DeviceGuid);

            if (gp == null)
            {
                Console.WriteLine("[WARN] No Windows.Gaming.Input gamepad detected – controller will be idle.");
                ListAvailableGamepads(); // show what the OS sees so the user can adjust mapping.json
                gp = new NullGamepadProvider(); // do‑nothing fallback to keep the app alive
            }
            else
            {
                Console.WriteLine($"[INFO] Gamepad detected (deviceId={mapping.DeviceId}).");
            }

            gp.ButtonDown += async btnName => await HandleButtonAsync(btnName, mapping, obsBridge);
            gp.Start();
            Console.WriteLine($"Listening on gamepad device {mapping.DeviceId}… Press Ctrl+C to exit.");

            // Wait until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }, hostOption, portOption, passwordOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleButtonAsync(string buttonName, Mapping mapping, ObsBridge obs)
    {
        if (mapping.ButtonMap == null || !mapping.ButtonMap.TryGetValue(buttonName, out var action))
            return; // unmapped button – ignore

        if (string.IsNullOrWhiteSpace(action.Action))
            return;

        try
        {
            switch (action.Action)
            {
                case "StartStreaming":
                    await obs.StartStreamingAsync();
                    break;
                case "StopStreaming":
                    await obs.StopStreamingAsync();
                    break;
                case "ToggleRecording":
                    await obs.ToggleRecordingAsync();
                    break;
                case "SwitchScene":
                    if (!string.IsNullOrEmpty(action.Parameter))
                        await obs.SwitchSceneAsync(action.Parameter);
                    break;
                default:
                    Console.WriteLine($"[WARN] Unknown action '{action.Action}' for button {buttonName}.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to execute action '{action.Action}' for button {buttonName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper that enumerates all Gamepad objects visible via Windows.Gaming.Input and prints their index
    /// together with the Id string. This is useful when WinGamingGamepadProvider cannot find a device –
    /// the user can then adjust `deviceId` (or `DeviceGuid`) in mapping.json.
    /// </summary>
    private static void ListAvailableGamepads()
    {
        try
        {

            if (Windows.Gaming.Input.RawGameController.RawGameControllers.Count == 0)
            {
                Console.WriteLine("[INFO] No Gamepad objects reported by Windows.Gaming.Input.");
                return;
            }

            Console.WriteLine("[INFO] Detected Gamepads:");
            int index = 0;
            foreach (var gp in Windows.Gaming.Input.RawGameController.RawGameControllers)
            {
                // The Id property is a GUID string that uniquely identifies the device.
                Console.WriteLine($"   [{index}] Id: {gp.HardwareVendorId}");
                index++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to enumerate Gamepads: {ex.Message}");
        }
    }
}
