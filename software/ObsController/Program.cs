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

        rootCommand.SetHandler(async (host, port, password) =>
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

            IGamepadProvider gp = RawGamepadProvider.TryCreate(mapping.DeviceIdentifier);
            ListAvailableGamepads(); // show what the OS sees so the user can adjust mapping.json

            if (gp == null)
            {
                Console.WriteLine("[WARN] No Windows.Gaming.Input gamepad detected – controller will be idle.");
                gp = new NullGamepadProvider(); // do‑nothing fallback to keep the app alive
            }

            gp.StateChanged += async states => await HandleStateChanges(states, mapping, obsBridge);
            gp.Start();
            Console.WriteLine($"Listening on gamepad device {mapping.DeviceIdentifier}… Press Ctrl+C to exit.");

            // Wait until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
        }, hostOption, portOption, passwordOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleStateChanges(ControllerDelta changes, Mapping mapping, ObsBridge bridge)
    {
        if (mapping.ButtonMap == null)
            return; // no mappings. skip further processing.


        foreach (var key in changes.ButtonsChanged.Keys)
        {
            if(!changes.ButtonsChanged[key]) // only run when it is pressed one can attach two actions to a button for each press and release respetively but we only care about a single action.
                continue;

            if (!mapping.ButtonMap.ContainsKey(key.ToString()))
                continue;

            var action = mapping.ButtonMap[key.ToString()];
            if (string.IsNullOrWhiteSpace(action.Action))
                continue;

            
            try
            {
                switch (action.Action)
                {
                    case "StartStreaming":
                        await bridge.StartStreamingAsync();
                        break;
                    case "StopStreaming":
                        await bridge.StopStreamingAsync();
                        break;
                    case "ToggleRecording":
                        await bridge.ToggleRecordingAsync();
                        break;
                    case "SwitchScene":
                        if (!string.IsNullOrEmpty(action.Parameter))
                            await bridge.SwitchSceneAsync(action.Parameter);
                        break;
                    default:
                        Console.WriteLine($"[WARN] Unknown action '{action.Action}' for button {key}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to execute action '{action.Action}' for button {key}: {ex.Message}");
            }
        }
        // No switch/axis handling because This app does not care.
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
                Console.WriteLine($"   [{index}] Id: {gp.NonRoamableId.Replace("\0", "")}");
                index++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to enumerate Gamepads: {ex.Message}");
        }
    }
}
