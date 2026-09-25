using System.Runtime.InteropServices;
using System.Text;

namespace KathanaBotReloaded;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref POINT point);
    public static Rectangle ClientBounds(IntPtr window)
    {
        var point = new POINT();
        if (!IsWindow(window) || IsIconic(window) || !GetClientRect(window, out var rect) || !ClientToScreen(window, ref point)) return Rectangle.Empty;
        return new Rectangle(point.x, point.y, rect.right - rect.left, rect.bottom - rect.top);
    }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public Union data; }
    [StructLayout(LayoutKind.Explicit)] private struct Union
    {
        [FieldOffset(0)] public Keyboard keyboard;
        [FieldOffset(0)] public Mouse mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard
    { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse
    { public int x, y; public uint data, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    // Every generated keyboard event goes through this single SendInput entry point.
    public static bool Key(ushort vk, bool up) => SendInput(1,
        [new INPUT { type = 1, data = new Union { keyboard = new Keyboard { vk = vk, flags = up ? 2u : 0u } } }],
        Marshal.SizeOf<INPUT>()) == 1;

    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
    public static bool FocusWindow(IntPtr window)
    {
        if (!IsWindow(window)) return false;
        if (IsIconic(window)) ShowWindow(window, 9);
        if (SetForegroundWindow(window) && GetForegroundWindow() == window) return true;
        uint current = GetCurrentThreadId();
        uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        bool attached = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
        try { BringWindowToTop(window); SetForegroundWindow(window); }
        finally { if (attached) AttachThreadInput(current, foreground, false); }
        return GetForegroundWindow() == window;
    }

    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);

    public sealed record Target(IntPtr Handle, uint Pid, string Title)
    { public override string ToString() => Title; }
    public static List<Target> Windows()
    {
        var result = new List<Target>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint pid);
            var text = new StringBuilder(512);
            GetWindowText(window, text, text.Capacity);
            if (pid != Environment.ProcessId && IsWindowVisible(window) && text.Length > 0)
                result.Add(new Target(window, pid, text.ToString()));
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(w => w.Title).ToList();
    }
}
