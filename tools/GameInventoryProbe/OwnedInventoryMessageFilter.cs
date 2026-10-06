using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameInventoryProbe;

/// <summary>Eight-second WH_GETMESSAGE hook on this probe's own GUI thread only.</summary>
internal sealed class OwnedInventoryMessageFilter : IDisposable
{
    internal const long ReadyCookie = 0x494E5652;
    private static readonly List<OwnedInventoryMessageFilter> FailedRemovals = [];
    private readonly ulong marker;
    private readonly nint window;
    private readonly uint thread;
    private readonly long deadline;
    private readonly Native.HookProcedure callback;
    private readonly RawSample[] samples = new RawSample[8];
    private int sampleCount;
    private nint hook;
    private bool disposed;

    public OwnedInventoryMessageFilter(ulong marker, nint window)
    {
        if (marker == 0) throw new ArgumentOutOfRangeException(nameof(marker));
        Native.ValidateLayouts();
        this.marker = marker;
        this.window = window;
        thread = Native.GetCurrentThreadId();
        if (!Native.IsWindow(window) || Native.GetForegroundWindow() != window ||
            Native.GetWindowThreadProcessId(window, out var pid) != thread || pid != (uint)Environment.ProcessId)
            throw new InvalidOperationException("Only the current owned foreground form/thread may be filtered.");
        deadline = Environment.TickCount64 + 8000;
        callback = Filter;
        hook = Native.SetWindowsHookEx(3, callback, nint.Zero, thread);
        if (hook == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Owned inventory message filter installation failed.");
        Installed = true;
    }

    public bool Installed { get; }
    public bool Removed { get; private set; }
    public int RemovalError { get; private set; }
    public int ReadinessCount { get; private set; }
    public uint CallbackProcessId { get; private set; }
    public uint CallbackThreadId { get; private set; }
    public int DownCount { get; private set; }
    public int UpCount { get; private set; }
    public int MarkerMismatchCount { get; private set; }
    public int CallbackErrors { get; private set; }
    public int ExpiredMatchCount { get; private set; }

    private nint Filter(int code, nint removal, nint packet)
    {
        if (code != 0 || removal.ToInt64() != 1 || packet == nint.Zero || disposed)
            return Native.CallNextHookEx(nint.Zero, code, removal, packet);
        try
        {
            if (Marshal.ReadIntPtr(packet, 0) != window)
                return Native.CallNextHookEx(nint.Zero, code, removal, packet);
            var message = unchecked((uint)Marshal.ReadInt32(packet, 8));
            var key = unchecked((ulong)Marshal.ReadIntPtr(packet, 16).ToInt64());
            if (message == 0 && key == marker && Marshal.ReadIntPtr(packet, 24).ToInt64() == ReadyCookie)
            {
                if (Environment.TickCount64 < deadline)
                {
                    ReadinessCount++;
                    CallbackProcessId = (uint)Environment.ProcessId;
                    CallbackThreadId = Native.GetCurrentThreadId();
                }
                return Native.CallNextHookEx(nint.Zero, code, removal, packet);
            }
            if (!Native.IsKeyMessage(message) || key != 0x49)
                return Native.CallNextHookEx(nint.Zero, code, removal, packet);

            var actual = unchecked((ulong)Native.GetMessageExtraInfo().ToInt64());
            var up = message is 0x101 or 0x105;
            if (sampleCount < samples.Length)
                samples[sampleCount++] = new RawSample(Environment.TickCount64, actual, up, message);
            if (actual != marker)
            {
                MarkerMismatchCount++;
                return Native.CallNextHookEx(nint.Zero, code, removal, packet);
            }
            if (Environment.TickCount64 >= deadline)
            {
                ExpiredMatchCount++;
                return Native.CallNextHookEx(nint.Zero, code, removal, packet);
            }
            // Consume only our already-enqueued matching I events even if focus
            // changes afterwards. New global input still needs an exact guard.
            Marshal.WriteInt32(packet, 8, 0); // WM_NULL
            Marshal.WriteIntPtr(packet, 16, nint.Zero);
            Marshal.WriteIntPtr(packet, 24, nint.Zero);
            if (up) UpCount++; else DownCount++;
            return Native.CallNextHookEx(nint.Zero, code, removal, packet);
        }
        catch
        {
            CallbackErrors++;
            return Native.CallNextHookEx(nint.Zero, code, removal, packet);
        }
    }

    public FilterSnapshot Snapshot() => new()
    {
        Installed = Installed, Removed = Removed, RemovalError = RemovalError,
        ReadinessCount = ReadinessCount, CallbackProcessId = CallbackProcessId, CallbackThreadId = CallbackThreadId,
        DownCount = DownCount, UpCount = UpCount, MarkerMismatchCount = MarkerMismatchCount,
        CallbackErrors = CallbackErrors, ExpiredMatchCount = ExpiredMatchCount,
        MarkerSamples = samples.Take(sampleCount).Select(value => new MarkerSample
        {
            Tick = value.Tick, ActualFullExtraInfo = $"0x{value.Actual:X16}", MatchesOwnedMarker = value.Actual == marker,
            Up = value.Up, Message = value.Message
        }).ToArray()
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Removed = hook == nint.Zero || Native.UnhookWindowsHookEx(hook);
        RemovalError = Removed ? 0 : Marshal.GetLastWin32Error();
        if (Removed) hook = nint.Zero;
        else FailedRemovals.Add(this); // Lifetime retained; subsequent callbacks pass through.
        GC.KeepAlive(callback);
    }

    private readonly record struct RawSample(long Tick, ulong Actual, bool Up, uint Message);
}

internal sealed record MarkerSample
{
    public long Tick { get; init; }
    public string ActualFullExtraInfo { get; init; } = "";
    public bool MatchesOwnedMarker { get; init; }
    public bool Up { get; init; }
    public uint Message { get; init; }
}
internal sealed record FilterSnapshot
{
    public string HookKind { get; init; } = "WH_GETMESSAGE (owned probe thread only)";
    public bool Installed { get; init; }
    public bool Removed { get; init; }
    public int RemovalError { get; init; }
    public int ReadinessCount { get; init; }
    public uint CallbackProcessId { get; init; }
    public uint CallbackThreadId { get; init; }
    public int DownCount { get; init; }
    public int UpCount { get; init; }
    public int MarkerMismatchCount { get; init; }
    public int CallbackErrors { get; init; }
    public int ExpiredMatchCount { get; init; }
    public MarkerSample[] MarkerSamples { get; init; } = [];
}
