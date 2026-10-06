using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace RawKeyboardProbe;

/// <summary>
/// WH_GETMESSAGE on the controlled sentinel's own GUI thread only. Baseline
/// removed F24 messages pass through. Once verified, only an exactly tagged F24
/// message for this owned window is replaced with WM_NULL at retrieval time.
/// </summary>
internal sealed class OwnedF24MessageFilter : IOwnedF24QueueFilter
{
    private const int WhGetMessage = 3;
    private const int HcAction = 0;
    private const long PmRemove = 1;
    private const long VkF24 = 135;
    private const uint WmKeyDown = 0x100;
    private const uint WmKeyUp = 0x101;
    private const uint WmSysKeyDown = 0x104;
    private const uint WmSysKeyUp = 0x105;
    private static readonly object FailedRemovalGate = new();
    private static readonly List<OwnedF24MessageFilter> FailedRemovals = [];

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

    public OwnedF24MessageFilter(uint marker, nint sentinel)
    {
        if (marker == 0) throw new ArgumentOutOfRangeException(nameof(marker), "Use the fixture's unique nonzero F24 marker.");
        if (IntPtr.Size != 8 || Marshal.SizeOf<NativeMessage>() != 48 ||
            Marshal.OffsetOf<NativeMessage>(nameof(NativeMessage.Window)).ToInt32() != 0 ||
            Marshal.OffsetOf<NativeMessage>(nameof(NativeMessage.Message)).ToInt32() != 8 ||
            Marshal.OffsetOf<NativeMessage>(nameof(NativeMessage.WParam)).ToInt32() != 16 ||
            Marshal.OffsetOf<NativeMessage>(nameof(NativeMessage.LParam)).ToInt32() != 24)
            throw new PlatformNotSupportedException("The owned message filter requires the validated Windows x64 MSG layout.");

        this.marker = marker;
        this.sentinel = sentinel;
        threadId = GetCurrentThreadId();
        if (!OwnSentinelIsForeground())
            throw new InvalidOperationException("The sentinel must belong to the current process/thread and be foreground before installing its message filter.");

        deadline = Environment.TickCount64 + 8000;
        callback = MessageCallback;
        // Current-process/current-thread only; no foreign module or DLL.
        hook = SetWindowsHookEx(WhGetMessage, callback, nint.Zero, threadId);
        if (hook == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Installing the owned sentinel's F24 message filter failed.");
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
        HookKind = "WH_GETMESSAGE", MarkerSamples = markerSamples.Snapshot(),
        Installed = Installed, Removed = Removed, RemovalError = RemovalError,
        BaselineDownCount = BaselineDownCount, BaselineUpCount = BaselineUpCount,
        SuppressedDownCount = SuppressedDownCount, SuppressedUpCount = SuppressedUpCount,
        MarkerMismatchCount = MarkerMismatchCount, CallbackErrors = CallbackErrors,
        ExpiredMatchCount = ExpiredMatchCount, BaselineVerified = BaselineVerified
    };

    private bool OwnSentinelIsForeground()
    {
        if (sentinel == nint.Zero || !IsWindow(sentinel) || GetForegroundWindow() != sentinel) return false;
        return GetWindowThreadProcessId(sentinel, out var pid) == threadId && pid == (uint)Environment.ProcessId;
    }

    private nint MessageCallback(int code, nint removal, nint packet)
    {
        // PM_NOREMOVE and all other hook codes pass without observations.
        if (code != HcAction || removal.ToInt64() != PmRemove || packet == nint.Zero || Volatile.Read(ref disposed) != 0)
            return CallNextHookEx(nint.Zero, code, removal, packet);

        try
        {
            if (Marshal.ReadIntPtr(packet, 0) != sentinel)
                return CallNextHookEx(nint.Zero, code, removal, packet);
            var message = unchecked((uint)Marshal.ReadInt32(packet, 8));
            if (message != WmKeyDown && message != WmKeyUp && message != WmSysKeyDown && message != WmSysKeyUp)
                return CallNextHookEx(nint.Zero, code, removal, packet);
            if (Marshal.ReadIntPtr(packet, 16).ToInt64() != VkF24)
                return CallNextHookEx(nint.Zero, code, removal, packet);

            var actualExtraInfo = unchecked((ulong)GetMessageExtraInfo().ToInt64());
            var matches = actualExtraInfo == marker;
            var up = message == WmKeyUp || message == WmSysKeyUp;
            markerSamples.Record(actualExtraInfo, matches, up, message, Volatile.Read(ref suppress) != 0);
            if (!matches)
            {
                Interlocked.Increment(ref markerMismatchCount);
                return CallNextHookEx(nint.Zero, code, removal, packet);
            }
            if (Environment.TickCount64 >= deadline)
            {
                Interlocked.Increment(ref expiredMatchCount);
                return CallNextHookEx(nint.Zero, code, removal, packet);
            }
            if (Volatile.Read(ref suppress) == 0)
            {
                if (up) Interlocked.Increment(ref baselineUpCount);
                else Interlocked.Increment(ref baselineDownCount);
                return CallNextHookEx(nint.Zero, code, removal, packet);
            }

            // A WH_GETMESSAGE return value cannot suppress a message. Replace
            // only these three fields; keep HWND/time/POINT/lPrivate untouched.
            Marshal.WriteInt32(packet, 8, 0); // WM_NULL
            Marshal.WriteIntPtr(packet, 16, nint.Zero);
            Marshal.WriteIntPtr(packet, 24, nint.Zero);
            if (up) Interlocked.Increment(ref suppressedUpCount);
            else Interlocked.Increment(ref suppressedDownCount);
            return CallNextHookEx(nint.Zero, code, removal, packet);
        }
        catch
        {
            Interlocked.Increment(ref callbackErrors);
            return CallNextHookEx(nint.Zero, code, removal, packet);
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
        else lock (FailedRemovalGate) FailedRemovals.Add(this);
        GC.KeepAlive(callback);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nint removal, nint packet);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProcedure procedure, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint removal, nint packet);
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
