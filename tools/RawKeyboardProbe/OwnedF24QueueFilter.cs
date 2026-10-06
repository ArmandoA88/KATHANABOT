using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace RawKeyboardProbe;

/// <summary>
/// Ordinary WH_KEYBOARD filter on this controlled sentinel's own GUI thread only.
/// It first observes tagged F24 without suppression. Suppression may be armed only
/// after the expected baseline is verified. No other thread or game is hooked.
/// </summary>
internal sealed class OwnedF24QueueFilter : IOwnedF24QueueFilter
{
    private const int WhKeyboard = 2;
    private const int HcAction = 0;
    private const long VkF24 = 135;
    private static readonly object FailedRemovalGate = new();
    private static readonly List<OwnedF24QueueFilter> FailedRemovals = [];

    private readonly uint marker;
    private readonly nint sentinel;
    private readonly uint threadId;
    private readonly long deadline;
    private readonly HookProcedure callback;
    private readonly OwnedF24MarkerRecorder markerSamples = new();
    private nint hook;
    private int disposed;
    private int suppress;
    private int baselineVerified;
    private int baselineDownCount;
    private int baselineUpCount;
    private int suppressedDownCount;
    private int suppressedUpCount;
    private int markerMismatchCount;
    private int callbackErrors;
    private int expiredMatchCount;

    public OwnedF24QueueFilter(uint marker, nint sentinel)
    {
        if (marker == 0) throw new ArgumentOutOfRangeException(nameof(marker), "Use the fixture's unique nonzero F24 marker.");
        this.marker = marker;
        this.sentinel = sentinel;
        threadId = GetCurrentThreadId();
        if (!OwnSentinelIsForeground())
            throw new InvalidOperationException("The sentinel must belong to the current process/thread and be foreground before installing its queue filter.");

        deadline = Environment.TickCount64 + 8000;
        callback = KeyboardCallback;
        // A current-process, current-thread hook needs no foreign module or DLL.
        hook = SetWindowsHookEx(WhKeyboard, callback, nint.Zero, threadId);
        if (hook == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Installing the owned sentinel's F24 queue filter failed.");
        Installed = true;
    }

    public bool Installed { get; }
    public bool Removed { get; private set; }
    public int RemovalError { get; private set; }
    public int BaselineDownCount => Volatile.Read(ref baselineDownCount);
    public int BaselineUpCount => Volatile.Read(ref baselineUpCount);
    public int SuppressedDownCount => Volatile.Read(ref suppressedDownCount);
    public int SuppressedUpCount => Volatile.Read(ref suppressedUpCount);
    public int MarkerMismatchCount => Volatile.Read(ref markerMismatchCount);
    public int CallbackErrors => Volatile.Read(ref callbackErrors);
    public int ExpiredMatchCount => Volatile.Read(ref expiredMatchCount);
    public bool BaselineVerified => Volatile.Read(ref baselineVerified) != 0;

    public bool EnableSuppression(int expectedBaselinePairs)
    {
        if (expectedBaselinePairs <= 0 || Volatile.Read(ref disposed) != 0 || Environment.TickCount64 >= deadline ||
            BaselineDownCount != expectedBaselinePairs || BaselineUpCount != expectedBaselinePairs ||
            MarkerMismatchCount != 0 || CallbackErrors != 0 || ExpiredMatchCount != 0 ||
            GetCurrentThreadId() != threadId || !OwnSentinelIsForeground())
            return false;

        Volatile.Write(ref baselineVerified, 1);
        Volatile.Write(ref suppress, 1);
        return true;
    }

    public OwnedF24QueueFilterSnapshot Snapshot() => new()
    {
        Installed = Installed, Removed = Removed, RemovalError = RemovalError,
        BaselineDownCount = BaselineDownCount, BaselineUpCount = BaselineUpCount,
        SuppressedDownCount = SuppressedDownCount, SuppressedUpCount = SuppressedUpCount,
        MarkerMismatchCount = MarkerMismatchCount, CallbackErrors = CallbackErrors,
        ExpiredMatchCount = ExpiredMatchCount, BaselineVerified = BaselineVerified,
        HookKind = "WH_KEYBOARD", MarkerSamples = markerSamples.Snapshot()
    };

    private bool OwnSentinelIsForeground()
    {
        if (sentinel == nint.Zero || !IsWindow(sentinel) || GetForegroundWindow() != sentinel) return false;
        return GetWindowThreadProcessId(sentinel, out var pid) == threadId && pid == (uint)Environment.ProcessId;
    }

    private nint KeyboardCallback(int code, nint key, nint flags)
    {
        // HC_NOREMOVE, negative codes and every other code must pass unchanged.
        // Never count them: one queued key can trigger non-removal observations.
        if (code != HcAction || Volatile.Read(ref disposed) != 0 || key.ToInt64() != VkF24)
            return CallNextHookEx(nint.Zero, code, key, flags);

        try
        {
            // WH_KEYBOARD has no LLKHF_INJECTED field. Verify actual queue marker
            // behavior in the pass-through baseline rather than assuming it survives.
            var actualExtraInfo = unchecked((ulong)GetMessageExtraInfo().ToInt64());
            var up = (unchecked((ulong)flags.ToInt64()) & 0x80000000UL) != 0;
            // WH_KEYBOARD supplies key/state, not an actual MSG message number.
            markerSamples.Record(actualExtraInfo, actualExtraInfo == marker, up, 0, Volatile.Read(ref suppress) != 0);
            if (actualExtraInfo != marker)
            {
                Interlocked.Increment(ref markerMismatchCount);
                return CallNextHookEx(nint.Zero, code, key, flags);
            }
            if (Environment.TickCount64 >= deadline)
            {
                Interlocked.Increment(ref expiredMatchCount);
                return CallNextHookEx(nint.Zero, code, key, flags);
            }

            if (Volatile.Read(ref suppress) == 0)
            {
                if (up) Interlocked.Increment(ref baselineUpCount);
                else Interlocked.Increment(ref baselineDownCount);
                return CallNextHookEx(nint.Zero, code, key, flags);
            }
            if (up) Interlocked.Increment(ref suppressedUpCount);
            else Interlocked.Increment(ref suppressedDownCount);
            return new nint(1);
        }
        catch
        {
            Interlocked.Increment(ref callbackErrors);
            return CallNextHookEx(nint.Zero, code, key, flags);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Volatile.Write(ref suppress, 0);
        if (hook == nint.Zero) { Removed = true; return; }
        Removed = UnhookWindowsHookEx(hook);
        RemovalError = Removed ? 0 : Marshal.GetLastWin32Error();
        if (Removed) hook = nint.Zero;
        // Keep a failed-removal delegate alive, but pass every subsequent callback.
        else lock (FailedRemovalGate) FailedRemovals.Add(this);
        GC.KeepAlive(callback);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nint key, nint flags);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProcedure procedure, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint key, nint flags);
    [DllImport("user32.dll")]
    private static extern nint GetMessageExtraInfo();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

internal sealed record OwnedF24QueueFilterSnapshot
{
    public string HookKind { get; init; } = "WH_KEYBOARD";
    public OwnedF24MarkerSample[] MarkerSamples { get; init; } = [];
    public bool Installed { get; init; }
    public bool Removed { get; init; }
    public int RemovalError { get; init; }
    public int BaselineDownCount { get; init; }
    public int BaselineUpCount { get; init; }
    public int SuppressedDownCount { get; init; }
    public int SuppressedUpCount { get; init; }
    public int MarkerMismatchCount { get; init; }
    public int CallbackErrors { get; init; }
    public int ExpiredMatchCount { get; init; }
    public bool BaselineVerified { get; init; }
}
