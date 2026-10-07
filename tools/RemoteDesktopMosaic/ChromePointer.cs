using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

// Input goes straight to the page. WM_MOUSEMOVE on a covered Chrome HWND starts
// OS leave tracking, which immediately substitutes the physical desktop cursor
// for the translated tile position. Repeating that message cannot sustain hover.
internal sealed class ChromePointer : IDisposable
{
    internal sealed record Target(string Id, string Url, string WebSocketDebuggerUrl);
    internal sealed record Input(string Type, double X, double Y, string Button = "none",
        int Buttons = 0, int Modifiers = 0, int Delta = 0);
    readonly ClientWebSocket socket = new();
    readonly SemaphoreSlim commands = new(1);
    readonly CancellationTokenSource lifetime = new();
    readonly LinkedList<object> pending = new();
    readonly Action<string> failed;
    bool draining, disposed;
    int sequence;
    double scale = 1;
    uint windowDpi;
    bool refreshScale;
    public bool Ready => !disposed && socket.State == WebSocketState.Open;
    internal string TargetId { get; private set; } = "";

    internal static Target? FindOpenedTarget(IEnumerable<Target> targets, ISet<string> before)
    {
        var opened = targets.Where(t => !before.Contains(t.Id)).Take(2).ToArray();
        return opened.Length == 1 ? opened[0] : null;
    }

    ChromePointer(Action<string> failed) => this.failed = failed;

    internal static async Task<Target[]> Targets(string profile, CancellationToken cancellation)
    {
        string file = Path.Combine(profile, "DevToolsActivePort");
        if (!File.Exists(file)) return [];
        var lines = await File.ReadAllLinesAsync(file, cancellation);
        if (lines.Length < 2 || !int.TryParse(lines[0], out int port) || port is < 1 or > 65535) return [];
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
        string body;
        // A stale DevToolsActivePort (Chrome no longer running) makes Windows take ~2s to refuse
        // the loopback connect, so HttpClient's timeout fires first as a TaskCanceledException.
        // Report it as an IOException so callers don't mistake it for the app shutting down.
        try { body = await http.GetStringAsync($"http://127.0.0.1:{port}/json/list", cancellation); }
        catch (OperationCanceledException ex) when (!cancellation.IsCancellationRequested)
        { throw new IOException("Chrome input endpoint did not respond.", ex); }
        using var json = JsonDocument.Parse(body);
        return json.RootElement.EnumerateArray()
            .Where(t => t.GetProperty("type").GetString() == "page" && t.TryGetProperty("webSocketDebuggerUrl", out _))
            .Select(t => new Target(t.GetProperty("id").GetString()!, t.GetProperty("url").GetString()!,
                t.GetProperty("webSocketDebuggerUrl").GetString()!)).ToArray();
    }

    internal static async Task<ChromePointer> Connect(Target target, Action<string> failed, CancellationToken cancellation)
    {
        var uri = new Uri(target.WebSocketDebuggerUrl);
        if (uri.Scheme != "ws" || !uri.IsLoopback) throw new IOException("Chrome input endpoint must be local.");
        var input = new ChromePointer(failed) { TargetId = target.Id };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await input.socket.ConnectAsync(uri, timeout.Token);
            // Remote Desktop listens for keys only while its page has focus.
            // Keep DOM focus without raising or moving the hidden Chrome window.
            await input.Command("Emulation.setFocusEmulationEnabled", new { enabled = true });
            await input.RefreshScale();
            return input;
        }
        catch { input.Dispose(); throw; }
    }

    internal async Task<JsonElement> Command(string method, object? parameters = null)
    {
        await commands.WaitAsync(lifetime.Token);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            int id = ++sequence;
            byte[] message = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } });
            await socket.SendAsync(message, WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[8192];
            while (true)
            {
                using var response = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    if (part.MessageType == WebSocketMessageType.Close) throw new IOException("Chrome input connection closed.");
                    response.Write(buffer, 0, part.Count);
                    if (response.Length > 4 * 1024 * 1024) throw new IOException("Chrome input response too large.");
                } while (!part.EndOfMessage);
                using var json = JsonDocument.Parse(response.ToArray());
                var root = json.RootElement;
                if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out var error)) throw new IOException(error.ToString());
                return root.GetProperty("result").Clone();
            }
        }
        finally { commands.Release(); }
    }

    internal async Task RefreshScale()
    {
        var result = await Command("Runtime.evaluate", new { expression = "window.devicePixelRatio", returnByValue = true });
        scale = result.GetProperty("result").GetProperty("value").GetDouble();
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;
    }

    public bool Enqueue(Input input)
    {
        if (!Ready) return false;
        // Coalesce only consecutive moves with the same button/modifier state.
        // Never drop or reorder a press, release, wheel, or drag transition.
        if (pending.Last is { Value: Input last } tail && input.Type == "mouseMoved" &&
            last.Type == input.Type && last.Buttons == input.Buttons && last.Modifiers == input.Modifiers)
            tail.Value = input;
        else pending.AddLast(input);
        if (!draining) _ = Drain();
        return true;
    }

    public bool EnqueueKey(ChromeKey input)
    {
        if (!Ready) return false;
        pending.AddLast(input);
        if (!draining) _ = Drain();
        return true;
    }

    internal void SetWindowDpi(uint dpi)
    {
        if (dpi == windowDpi) return;
        windowDpi = dpi;
        refreshScale = true;
    }

    async Task Drain()
    {
        draining = true;
        try
        {
            while (pending.First is { } first && Ready)
            {
                var next = first.Value;
                pending.RemoveFirst();
                if (next is ChromeKey key)
                {
                    await Command("Input.dispatchKeyEvent", key.Parameters);
                    continue;
                }
                var input = (Input)next;
                if (refreshScale) { refreshScale = false; await RefreshScale(); }
                await Command("Input.dispatchMouseEvent", new
                {
                    type = input.Type, x = input.X / scale, y = input.Y / scale,
                    button = input.Button, buttons = input.Buttons, modifiers = input.Modifiers,
                    clickCount = input.Type is "mousePressed" or "mouseReleased" ? 1 : 0,
                    deltaX = 0, deltaY = -input.Delta, pointerType = "mouse"
                });
            }
        }
        catch (Exception ex)
        {
            if (!disposed) { Dispose(); failed(ex.Message); }
        }
        finally { draining = false; }
    }

    internal async Task Flush()
    {
        while (draining) await Task.Delay(10);
        if (!Ready) throw new IOException("Chrome input connection is not available.");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pending.Clear();
        lifetime.Cancel();
        socket.Dispose();
    }
}
