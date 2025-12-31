using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
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
        if (!string.IsNullOrEmpty(_password))
        {
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
                // Expect a response; ignore its content – if auth fails OBS will close the socket.
                await ReceiveMessageAsync(ct);
            }
        }
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
        var id = Interlocked.Increment(ref _requestId);
        var payload = new JObject
        {
            ["op"] = 6, // Request (per OBS v5 spec)
            ["d"] = new JObject
            {
                ["requestType"] = requestType,
                ["requestId"] = id.ToString(),
                ["requestData"] = @params ?? new JObject()
            }
        };
        await SendMessageAsync(payload, CancellationToken.None);
        // The response will come with op=7 (RequestResponse). For simplicity we just read the next message.
        var resp = await ReceiveMessageAsync(CancellationToken.None);
        return resp;
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
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None);
        _ws.Dispose();
    }
}
