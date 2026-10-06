using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace RawKeyboardProbe;

/// <summary>
/// Controlled-fixture filter for the caller's uniquely tagged injected F24 only.
/// WH_KEYBOARD_LL is desktop-wide; its callback runs in this installing process.
/// The caller must first verify its own started sentinel process/window and stop
/// new SendInput requests if foreground ownership changes. This is not a game hook.
/// </summary>
internal sealed class OwnedF24LegacyFilter : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint VkF24 = 135;
    private const uint LlkhfInjected = 0x10;
    private static readonly object FailedRemovalGate = new();
    // A failed native removal must not leave Windows with a collected delegate.
    // Such callbacks pass everything through until this bounded probe process exits.
    private static readonly List<OwnedF24LegacyFilter> FailedRemovals = [];

    private readonly uint marker;
    private readonly long deadline;
    private readonly HookProcedure callback;
    private nint hook;
    private int disposed;
    private int downCount;
    private int upCount;
    private int callbackErrors;
    private int expiredMatchCount;
    private int injectedMarkerMatchCount;

    public OwnedF24LegacyFilter(uint marker, nint sentinel, uint sentinelPid)
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf<KeyboardLowLevelPacket>() != 24)
            throw new InvalidOperationException("The owned F24 filter requires the 24-byte x64 KBDLLHOOKSTRUCT layout.");
        if (marker == 0) throw new ArgumentOutOfRangeException(nameof(marker), "Use the fixture's unique nonzero injection marker.");
        if (sentinel == nint.Zero || sentinelPid == 0 || !IsWindow(sentinel) ||
            GetWindowThreadProcessId(sentinel, out var actualPid) == 0 || actualPid != sentinelPid ||
            GetForegroundWindow() != sentinel)
            throw new InvalidOperationException("The verified owned sentinel HWND/PID must be foreground before filter installation.");

        this.marker = marker;
        deadline = Environment.TickCount64 + 8000;
        callback = KeyboardCallback;
        hook = SetWindowsHookEx(WhKeyboardLl, callback, GetModuleHandle(null), 0);
        if (hook == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Installing the controlled F24 legacy filter failed.");
    }

    public int DownCount => Volatile.Read(ref downCount);
    public int UpCount => Volatile.Read(ref upCount);
    public int CallbackErrors => Volatile.Read(ref callbackErrors);
    public int ExpiredMatchCount => Volatile.Read(ref expiredMatchCount);
    public int InjectedMarkerMatchCount => Volatile.Read(ref injectedMarkerMatchCount);
    public bool Expired => Environment.TickCount64 >= deadline;
    public bool Removed { get; private set; }
    public int RemovalError { get; private set; }

    private nint KeyboardCallback(int code, nint message, nint packet)
    {
        if (code != 0 || Volatile.Read(ref disposed) != 0)
            return CallNextHookEx(nint.Zero, code, message, packet);
        var keyMessage = message.ToInt64();
        if (keyMessage is not (0x100 or 0x101 or 0x104 or 0x105))
            return CallNextHookEx(nint.Zero, code, message, packet);

        try
        {
            if (packet == nint.Zero)
            {
                Interlocked.Increment(ref callbackErrors);
                return CallNextHookEx(nint.Zero, code, message, packet);
            }
            // Read only the fields needed to discard unrelated activity immediately.
            // No data or counters about any other key/injection are retained.
            if (unchecked((uint)Marshal.ReadInt32(packet, 0)) != VkF24 ||
                (unchecked((uint)Marshal.ReadInt32(packet, 8)) & LlkhfInjected) == 0 ||
                unchecked((ulong)Marshal.ReadIntPtr(packet, 16).ToInt64()) != marker)
                return CallNextHookEx(nint.Zero, code, message, packet);

            Interlocked.Increment(ref injectedMarkerMatchCount);
            if (Environment.TickCount64 >= deadline)
            {
                Interlocked.Increment(ref expiredMatchCount);
                return CallNextHookEx(nint.Zero, code, message, packet);
            }
            if (keyMessage is 0x100 or 0x104) Interlocked.Increment(ref downCount);
            else Interlocked.Increment(ref upCount);

            // Consume the tagged event even if focus changed after its enqueue.
            // New injection must still be stopped by the caller's foreground guard.
            return new nint(1);
        }
        catch
        {
            Interlocked.Increment(ref callbackErrors);
            return CallNextHookEx(nint.Zero, code, message, packet);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (hook == nint.Zero) { Removed = true; return; }
        Removed = UnhookWindowsHookEx(hook);
        RemovalError = Removed ? 0 : Marshal.GetLastWin32Error();
        if (Removed) hook = nint.Zero;
        else lock (FailedRemovalGate) FailedRemovals.Add(this);
        GC.KeepAlive(callback);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardLowLevelPacket
    {
        public uint VirtualKey, ScanCode, Flags, Time;
        public nuint ExtraInformation;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nint message, nint packet);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProcedure procedure, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint packet);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
