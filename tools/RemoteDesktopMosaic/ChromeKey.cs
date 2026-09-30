using System.Runtime.InteropServices;
using System.Text;

// Preserve both the physical key (Remote Desktop/game controls) and its text
// (PIN fields, chat and ordinary browser controls). Never inject into the OS.
internal sealed record ChromeKey(int VirtualKey, string Code, string Key, string Text,
    int Location, bool Down, int Modifiers, bool Repeat = false)
{
    internal object Parameters => new
    {
        type = Down ? Text.Length > 0 ? "keyDown" : "rawKeyDown" : "keyUp",
        code = Code, key = Key, text = Down ? Text : "", unmodifiedText = Down ? Text : "",
        windowsVirtualKeyCode = VirtualKey, nativeVirtualKeyCode = VirtualKey,
        modifiers = Modifiers, autoRepeat = Down && Repeat,
        isKeypad = Location == 3, location = Location == 3 ? 0 : Location,
        isSystemKey = (Modifiers & 1) != 0
    };

    internal static int Modifier(int vk) => (Keys)vk switch
    {
        Keys.Menu or Keys.LMenu or Keys.RMenu => 1,
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => 2,
        Keys.LWin or Keys.RWin => 4,
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => 8,
        _ => 0
    };

    internal static ChromeKey Create(int vk, int scan, bool extended, bool down, int modifiers, bool caps, bool repeat)
    {
        string code = PhysicalCode(scan, extended);
        if (code.Length == 0)
            code = vk is >= 0x41 and <= 0x5a ? "Key" + (char)vk :
                vk is >= 0x30 and <= 0x39 ? "Digit" + (char)vk : "";
        int location = code.StartsWith("Numpad") ? 3 :
            Modifier(vk) != 0 ? code.EndsWith("Right") ? 2 : 1 : 0;
        int normalized = (Keys)vk switch
        {
            Keys.LShiftKey or Keys.RShiftKey => (int)Keys.ShiftKey,
            Keys.LControlKey or Keys.RControlKey => (int)Keys.ControlKey,
            Keys.LMenu or Keys.RMenu => (int)Keys.Menu,
            _ => vk
        };
        string name = (Keys)normalized switch
        {
            Keys.Back => "Backspace", Keys.Tab => "Tab", Keys.Enter => "Enter",
            Keys.ShiftKey => "Shift", Keys.ControlKey => "Control", Keys.Menu => "Alt",
            Keys.LWin or Keys.RWin => "Meta", Keys.CapsLock => "CapsLock",
            Keys.Escape => "Escape", Keys.Left => "ArrowLeft", Keys.Right => "ArrowRight",
            Keys.Up => "ArrowUp", Keys.Down => "ArrowDown", Keys.Prior => "PageUp",
            Keys.Next => "PageDown", Keys.End => "End", Keys.Home => "Home",
            Keys.Insert => "Insert", Keys.Delete => "Delete", Keys.NumLock => "NumLock",
            Keys.Scroll => "ScrollLock", Keys.Pause => "Pause", Keys.PrintScreen => "PrintScreen",
            Keys.Apps => "ContextMenu",
            >= Keys.F1 and <= Keys.F24 => "F" + (normalized - (int)Keys.F1 + 1),
            _ => ""
        };
        var state = new byte[256];
        state[(int)Keys.ShiftKey] = (modifiers & 8) != 0 ? (byte)0x80 : (byte)0;
        state[(int)Keys.Capital] = caps ? (byte)1 : (byte)0;
        state[(int)Keys.NumLock] = Control.IsKeyLocked(Keys.NumLock) ? (byte)1 : (byte)0;
        var buffer = new StringBuilder(16);
        // Flag 4 prevents translation from consuming the user's dead-key state.
        int length = ToUnicodeEx((uint)vk, (uint)scan, state, buffer, buffer.Capacity, 4, GetKeyboardLayout(0));
        string translated = length > 0 ? buffer.ToString(0, length) : "";
        if (name.Length == 0) name = length < 0 ? "Dead" : translated.Length > 0 ? translated : "Unidentified";
        string text = down && (modifiers & 7) == 0 && (normalized == (int)Keys.Enter || translated.All(c => !char.IsControl(c))) ? translated : "";
        return new(normalized, code, name, text, location, down, modifiers, repeat);
    }

    static string PhysicalCode(int scan, bool extended)
    {
        if (extended) return scan switch
        {
            0x1c => "NumpadEnter", 0x1d => "ControlRight", 0x35 => "NumpadDivide",
            0x37 => "PrintScreen", 0x38 => "AltRight", 0x47 => "Home", 0x48 => "ArrowUp",
            0x49 => "PageUp", 0x4b => "ArrowLeft", 0x4d => "ArrowRight", 0x4f => "End",
            0x50 => "ArrowDown", 0x51 => "PageDown", 0x52 => "Insert", 0x53 => "Delete",
            0x5b => "MetaLeft", 0x5c => "MetaRight", 0x5d => "ContextMenu", _ => ""
        };
        return scan switch
        {
            0x01 => "Escape", >= 0x02 and <= 0x0a => "Digit" + (scan - 1), 0x0b => "Digit0",
            0x0c => "Minus", 0x0d => "Equal", 0x0e => "Backspace", 0x0f => "Tab",
            >= 0x10 and <= 0x19 => "Key" + "QWERTYUIOP"[scan - 0x10],
            0x1a => "BracketLeft", 0x1b => "BracketRight", 0x1c => "Enter", 0x1d => "ControlLeft",
            >= 0x1e and <= 0x26 => "Key" + "ASDFGHJKL"[scan - 0x1e],
            0x27 => "Semicolon", 0x28 => "Quote", 0x29 => "Backquote", 0x2a => "ShiftLeft",
            0x2b => "Backslash", >= 0x2c and <= 0x32 => "Key" + "ZXCVBNM"[scan - 0x2c],
            0x33 => "Comma", 0x34 => "Period", 0x35 => "Slash", 0x36 => "ShiftRight",
            0x37 => "NumpadMultiply", 0x38 => "AltLeft", 0x39 => "Space", 0x3a => "CapsLock",
            >= 0x3b and <= 0x44 => "F" + (scan - 0x3a), 0x45 => "NumLock", 0x46 => "ScrollLock",
            0x47 => "Numpad7", 0x48 => "Numpad8", 0x49 => "Numpad9", 0x4a => "NumpadSubtract",
            0x4b => "Numpad4", 0x4c => "Numpad5", 0x4d => "Numpad6", 0x4e => "NumpadAdd",
            0x4f => "Numpad1", 0x50 => "Numpad2", 0x51 => "Numpad3", 0x52 => "Numpad0",
            0x53 => "NumpadDecimal", 0x56 => "IntlBackslash", 0x57 => "F11", 0x58 => "F12", _ => ""
        };
    }

    [DllImport("user32.dll")] static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicodeEx(uint vk, uint scan,
        byte[] state, StringBuilder buffer, int size, uint flags, nint layout);
}
