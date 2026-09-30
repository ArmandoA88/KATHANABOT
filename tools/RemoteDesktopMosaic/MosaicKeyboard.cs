internal sealed partial class Mosaic
{
    readonly Dictionary<int, (int Scan, bool Extended)> physicalKeys = [];
    sealed record HeldKey(Tile Tile, int Scan, bool Extended, ChromeKey Input);
    readonly Dictionary<int, HeldKey> forwardedKeys = [];
    int KeyboardModifiers => physicalKeys.Keys.Aggregate(0, (bits, vk) => bits | ChromeKey.Modifier(vk));

    void SelectTile(int index)
    {
        if (selected != index) ReleaseForwardedKeys();
        selected = index;
    }

    void ReleaseForwardedKeys()
    {
        // Release ordinary keys before their modifiers, including when a dialog
        // opens or another local app takes focus while a game key is held.
        foreach (var pair in forwardedKeys.OrderBy(p => ChromeKey.Modifier(p.Key) != 0).ToArray())
        {
            forwardedKeys.Remove(pair.Key);
            int modifiers = forwardedKeys.Keys.Aggregate(0, (bits, vk) => bits | ChromeKey.Modifier(vk));
            SendKeyUp(pair.Key, pair.Value, modifiers);
        }
    }

    static void SendKeyUp(int vk, HeldKey held, int modifiers)
    {
        if (held.Tile.IsChrome)
            held.Tile.Pointer?.EnqueueKey(held.Input with { Down = false, Text = "", Modifiers = modifiers, Repeat = false });
        else if (Native.IsWindow(held.Tile.Source))
            ForwardKeyUp(held.Tile.Source, vk, held.Scan, (modifiers & 1) != 0);
    }

    void SendKeyDown(Tile tile, int vk, int scan, bool extended, int modifiers)
    {
        bool repeat = forwardedKeys.ContainsKey(vk);
        var input = ChromeKey.Create(vk, scan, extended, true, modifiers, capsLockOn, repeat);
        if (tile.IsChrome)
        {
            if (tile.Pointer?.EnqueueKey(input) != true) return;
        }
        else ForwardKeyDown(tile.Source, vk, scan, (modifiers & 8) != 0, capsLockOn,
            (modifiers & 2) != 0, (modifiers & 1) != 0, (modifiers & 1) != 0);
        forwardedKeys[vk] = new(tile, scan, extended, input);
    }

    // The hook calls this synchronously, before Windows updates asynchronous key
    // state. Suppressed modifiers never reach GetAsyncKeyState, so track them here.
    bool ProcessKeyboard(int vk, int scan, bool extended, bool down, bool inForwardZone, bool inOriginalSource)
    {
        bool repeat = physicalKeys.ContainsKey(vk);
        if (down) physicalKeys[vk] = (scan, extended); else physicalKeys.Remove(vk);
        if (vk == (int)Keys.CapsLock && down && !repeat) capsLockOn = !capsLockOn;
        if (!down && consumedHotkeyKeys.Remove(vk)) return true;
        if (down && consumedHotkeyKeys.Contains(vk)) return true;
        int modifiers = KeyboardModifiers;
        bool ctrl = (modifiers & 2) != 0, alt = (modifiers & 1) != 0;
        bool shift = (modifiers & 8) != 0, win = (modifiers & 4) != 0;
        if (down && inForwardZone && HotkeyMatches(expandHotkey, vk, ctrl, alt, shift, win))
        { consumedHotkeyKeys.Add(vk); BeginInvoke(new Action(ActivateSource)); return true; }
        if (down && (inForwardZone || inOriginalSource) && HotkeyMatches(mosaicHotkey, vk, ctrl, alt, shift, win))
        { consumedHotkeyKeys.Add(vk); BeginInvoke(new Action(ReturnToMosaic)); return true; }
        if (down && inForwardZone && ctrl && !alt && !win && (vk == 0x46 || vk is >= 0x31 and <= 0x39))
        {
            consumedHotkeyKeys.Add(vk);
            if (vk == 0x46) BeginInvoke(new Action(ToggleFocusMode));
            else
            {
                int index = vk - 0x31;
                if (index < tiles.Count) BeginInvoke(new Action(() =>
                { SelectTile(index); if (focusMode) UpdateTiles(); else Invalidate(); }));
            }
            return true;
        }
        // Key-up belongs to the tile that received key-down, even after a focus
        // change. Local Windows-key shortcuts remain available.
        if (!down && forwardedKeys.Remove(vk, out var held))
        { SendKeyUp(vk, held, modifiers); return true; }
        if (!inForwardZone || win || vk is 0x5b or 0x5c) return false;
        if (selected < 0 || selected >= tiles.Count || !Native.IsWindow(tiles[selected].Source)) return false;
        var tile = tiles[selected];
        if (tile.IsChrome && tile.Pointer?.Ready != true) return false;
        if (!down) return true;
        if (forwardedKeys.Values.Any(k => k.Tile != tile)) ReleaseForwardedKeys();
        // A modifier can already be held when selecting a tile or returning from
        // a dialog. Give the new remote computer its complete key sequence.
        foreach (var pair in physicalKeys.Where(p => p.Key != vk && ChromeKey.Modifier(p.Key) != 0).ToArray())
            if (!forwardedKeys.ContainsKey(pair.Key)) SendKeyDown(tile, pair.Key, pair.Value.Scan, pair.Value.Extended, modifiers);
        SendKeyDown(tile, vk, scan, extended, modifiers);
        return true;
    }
}
