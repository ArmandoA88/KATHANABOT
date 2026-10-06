using System.Runtime.InteropServices;
using System.Text;

namespace GameInventoryProbe;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
    {
        public ushort VirtualKey, Scan;
        public uint Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public nuint ExtraInfo;
    }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct InputPacket { public uint Type; public InputUnion Value; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }
    internal static void ValidateLayouts()
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf<InputPacket>() != 40 || Marshal.SizeOf<Message>() != 48 ||
            Marshal.OffsetOf<Message>(nameof(Message.Window)).ToInt32() != 0 ||
            Marshal.OffsetOf<Message>(nameof(Message.Id)).ToInt32() != 8 ||
            Marshal.OffsetOf<Message>(nameof(Message.WParam)).ToInt32() != 16 ||
            Marshal.OffsetOf<Message>(nameof(Message.LParam)).ToInt32() != 24)
            throw new PlatformNotSupportedException("This probe requires validated Windows x64 INPUT and MSG layouts.");
    }
    internal static string Hex(nint value) => $"0x{unchecked((ulong)value.ToInt64()):X}";
    internal static uint WindowPid(nint window) { GetWindowThreadProcessId(window, out var pid); return pid; }
    internal static bool IsKeyMessage(uint message) => message is 0x100 or 0x101 or 0x104 or 0x105;
    internal static bool KeyIsDown() => (GetAsyncKeyState(0x49) & 0x8000) != 0;
    internal static bool ModifiersAreDown() => (GetAsyncKeyState(0x10) & 0x8000) != 0 || (GetAsyncKeyState(0x11) & 0x8000) != 0 ||
        (GetAsyncKeyState(0x12) & 0x8000) != 0 || (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint HookProcedure(int code, nint removal, nint message);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate bool EnumWindowProcedure(nint window, nint parameter);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] internal static extern nint SetWindowsHookEx(int id, HookProcedure procedure, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint removal, nint message);
    [DllImport("user32.dll")] internal static extern nint GetMessageExtraInfo();
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowProcedure procedure, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] internal static extern uint MapVirtualKey(uint key, uint type);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, [In] InputPacket[] input, int size);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint window, out Rect rectangle);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWow64Process2(nint process, out ushort machine, out ushort nativeMachine);
}
