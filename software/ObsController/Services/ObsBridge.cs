using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ObsController.Models;

namespace ObsController.Services;

/// <summary>
/// Minimal OBS‑WebSocket client supporting the v5 JSON‑RPC protocol.
/// It handles authentication (if a password is configured) and provides helpers to send
/// generic requests as well as high‑level actions used by the button mapping.
/// </summary>
public class ObsBridge : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly string _password;
    private ClientWebSocket _ws = new();
    private int _requestId = 1; // monotonically increasing request identifiers

    // ---------------------------------------------------------------------
    // Concurrency helpers for the OBS‑WS protocol (v5)
    // ---------------------------------------------------------------------
    // Channel that receives unsolicited event messages (op=5). Consumers can read from it if they care about events.
    private readonly Channel<JObject> _eventChannel = Channel.CreateUnbounded<JObject>();
    // Pending request map – key is the requestId as string, value is a TCS completed when the matching response arrives.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pendingRequests = new();
    // Cancellation source used to stop the background receive loop on Dispose.
    private CancellationTokenSource _receiveCts;

    public ObsBridge(string host, int port, string password)
    {
        _uri = new Uri($"ws://{host}:{port}");
        _password = password;
    }

    /// <summary>
    /// Connects to OBS and performs the optional authentication handshake.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _ws.ConnectAsync(_uri, ct);
        // If a password is provided, we must perform the challenge/response flow (v5).

        var hello = await ReceiveMessageAsync(ct);
        var auth = hello["d"]?["authentication"];
        if (auth != null)
        {
            // The server sends {salt, challenge}. Compute response per OBS spec.
            var salt = auth["salt"].ToString();
            var challenge = auth["challenge"].ToString();
            var secret = ComputeHash(_password + salt);
            var authentication = ComputeHash(secret + challenge);

            var authReq = new JObject
            {
                ["op"] = 1,
                ["d"] = new JObject { ["rpcVersion"] = 1, ["authentication"] = authentication }
            };
            await SendMessageAsync(authReq, ct);

        }
        else
        {
            var authReq = new JObject
            {
                ["op"] = 1,
                ["d"] = new JObject { ["rpcVersion"] = 1 }
            };
            await SendMessageAsync(authReq, ct);

        }
        await ReceiveMessageAsync(ct);

        // Start the continuous receive loop after any handshake messages have been processed.
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private async Task SendMessageAsync(JObject payload, CancellationToken ct)
    {
        var json = payload.ToString();
        var buffer = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
    }

    private async Task<JObject> ReceiveMessageAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new byte[4096];
        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, ct);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage) break;
        }
        return JObject.Parse(sb.ToString());
    }

    private async Task<JObject> SendRequestAsync(string requestType, JObject @params = null)
    {
        // Generate a unique requestId and register a TCS that will be completed by the background receive loop.
        var id = Interlocked.Increment(ref _requestId).ToString();
        var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        var payload = new JObject
        {
            ["op"] = 6, // Request (per OBS v5 spec)
            ["d"] = new JObject
            {
                ["requestType"] = requestType,
                ["requestId"] = id,
                ["requestData"] = @params ?? new JObject()
            }
        };

        await SendMessageAsync(payload, CancellationToken.None);

        // Wait for the response – give a reasonable timeout to avoid indefinite hangs.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            // Ensure we clean up the pending entry even on timeout/cancel.
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Continuously reads from the WebSocket and routes messages to either
    /// the pending‑request dictionary (op=7) or the event channel (op=5).
    /// Any unexpected message is ignored but logged for debugging.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                try
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                }
                catch (OperationCanceledException)
                {
                    return; // cancellation requested – exit loop.
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            JObject message;
            try
            {
                message = JObject.Parse(sb.ToString());
            }
            catch (Exception ex)
            {
                // Malformed JSON – ignore but could be logged.
                System.Diagnostics.Debug.WriteLine($"ObsBridge: failed to parse message: {ex}");
                continue;
            }

            int op = (int)(message["op"] ?? -1);
            switch (op)
            {
                case 5: // Event – push to the event channel.
                    _eventChannel.Writer.TryWrite(message);
                    break;
                case 7: // RequestResponse
                    var respId = message["d"]?["requestId"]?.ToString();
                    if (respId != null && _pendingRequests.TryRemove(respId, out var pending))
                        pending.SetResult(message);
                    else
                        System.Diagnostics.Debug.WriteLine($"ObsBridge: unexpected response id {respId}");
                    break;
                default:
                    // For op codes we don't explicitly handle (e.g., Hello, Identified), just ignore.
                    break;
            }
        }
    }

    // ---------------------------------------------------------------------
    // High‑level helpers matching actions defined in Mapping.ButtonMap
    // ---------------------------------------------------------------------
    public Task StartStreamingAsync() => SendRequestAsync("StartStream");
    public Task StopStreamingAsync() => SendRequestAsync("StopStream");
    public Task ToggleRecordingAsync() => SendRequestAsync("ToggleRecord");
    public Task SwitchSceneAsync(string sceneName) => SendRequestAsync("SetCurrentProgramScene", new JObject { ["sceneName"] = sceneName });

    // Dispose pattern – close the websocket gracefully.
    public async ValueTask DisposeAsync()
    {
        // Cancel the background receive loop first.
        if (_receiveCts != null && !_receiveCts.IsCancellationRequested)
            _receiveCts.Cancel();

        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None);
        _ws.Dispose();
    }
}
