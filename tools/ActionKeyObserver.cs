using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

// Observes only the configured key while the selected process is foreground.
// Does not block, synthesize, or record any other keyboard input.
public sealed class KathanaActionKeyObserver : IDisposable
{
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    public readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();
    private readonly uint processId;
    private readonly int key;
    private readonly Timer timer;
    private readonly object gate = new object();
    private bool held, disposed;
    public KathanaActionKeyObserver(uint processId, int key)
    {
        this.processId = processId; this.key = key;
        timer = new Timer(Poll, null, 0, 5);
    }
    private void Poll(object state)
    {
        lock (gate)
        {
            if (disposed) return;
            uint foreground;
            GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
            bool focused = foreground == processId;
            bool down = focused && (GetAsyncKeyState(key) & 0x8000) != 0;
            if (down != held)
            {
                Events.Enqueue(DateTime.UtcNow.ToString("o") + "," + key + "," +
                    (down ? "down" : (focused ? "up" : "focus-lost")));
                held = down;
            }
        }
    }
    public void Dispose()
    {
        lock (gate) { disposed = true; timer.Dispose(); }
    }
}
