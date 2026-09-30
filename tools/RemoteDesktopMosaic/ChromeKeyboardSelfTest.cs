using System.Text.Json;

internal sealed partial class Mosaic
{
    void TestKey(Keys key, int scan, bool down, bool extended = false)
    {
        if (!ProcessKeyboard((int)key, scan, extended, down, true, false))
            throw new Exception("Keyboard forwarding rejected " + key);
    }

    void TapTestKey(Keys key, int scan, bool extended = false)
    { TestKey(key, scan, true, extended); TestKey(key, scan, false, extended); }

    async Task AssertChromeKeyboard(Tile tile, ChromePointer pointer, string context)
    {
        ReleaseForwardedKeys(); physicalKeys.Clear();
        bool previousCaps = capsLockOn; capsLockOn = false;
        try
        {
            await Evaluate(pointer, "entry.style.display='block';entry.value='';keyEvents=[];document.activeElement.blur();'reset'");
            var area = DisplayedContentArea(tile);
            var point = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
            if (selected != tiles.IndexOf(tile)) throw new Exception(context + ": clicking the screen did not select its keyboard input.");
            // No Flush between clicking and typing: this catches separate queues
            // racing the click that establishes the remote input field's focus.
            TapTestKey(Keys.A, 0x1e);
            TestKey(Keys.LShiftKey, 0x2a, true); TapTestKey(Keys.B, 0x30); TestKey(Keys.LShiftKey, 0x2a, false);
            TapTestKey(Keys.CapsLock, 0x3a); TapTestKey(Keys.C, 0x2e); TapTestKey(Keys.CapsLock, 0x3a);
            TapTestKey(Keys.D, 0x20);
            TapTestKey(Keys.Left, 0x4b, true); TapTestKey(Keys.Back, 0x0e); TapTestKey(Keys.X, 0x2d);
            TapTestKey(Keys.End, 0x4f, true); TapTestKey(Keys.Enter, 0x1c);
            TestKey(Keys.W, 0x11, true); TestKey(Keys.W, 0x11, true); TestKey(Keys.W, 0x11, false);
            await pointer.Flush();
            string actual = await Evaluate(pointer, "entry.value");
            if (actual != "aBxd\nww") throw new Exception($"{context}: hidden Chrome typing/editing failed: {JsonSerializer.Serialize(actual)}");
            TestKey(Keys.LControlKey, 0x1d, true); TapTestKey(Keys.A, 0x1e); TestKey(Keys.LControlKey, 0x1d, false);
            TapTestKey(Keys.Z, 0x2c);
            TestKey(Keys.LMenu, 0x38, true); TapTestKey(Keys.Tab, 0x0f); TestKey(Keys.LMenu, 0x38, false);
            await pointer.Flush();
            if (await Evaluate(pointer, "entry.value") != "z") throw new Exception(context + ": Ctrl+A modifier/shortcut forwarding failed.");
            string json = await Evaluate(pointer, "JSON.stringify(keyEvents)");
            using var doc = JsonDocument.Parse(json);
            var keys = doc.RootElement.EnumerateArray().ToArray();
            bool Has(string code, string type, string? flag = null) => keys.Any(k =>
                k.GetProperty("code").GetString() == code && k.GetProperty("type").GetString() == type &&
                (flag == null || k.GetProperty(flag).GetBoolean()));
            if (keys.Any(k => !k.GetProperty("trusted").GetBoolean() || !k.GetProperty("focused").GetBoolean()) ||
                !Has("KeyB", "keydown", "shift") || !Has("KeyA", "keydown", "ctrl") ||
                !Has("Tab", "keydown", "alt") || !Has("KeyW", "keydown", "repeat") ||
                !Has("ArrowLeft", "keyup") || !Has("ControlLeft", "keyup") || !Has("AltLeft", "keyup"))
                throw new Exception(context + ": Chrome keyboard event metadata failed: " + json);
            foreach (var group in keys.GroupBy(k => k.GetProperty("code").GetString()))
                if (group.Count(k => k.GetProperty("type").GetString() == "keydown" && !k.GetProperty("repeat").GetBoolean()) !=
                    group.Count(k => k.GetProperty("type").GetString() == "keyup"))
                    throw new Exception(context + ": stuck key: " + group.Key);
            TestKey(Keys.W, 0x11, true);
            OnDeactivate(EventArgs.Empty);
            await pointer.Flush();
            if (forwardedKeys.Count != 0 || await Evaluate(pointer, "keyEvents.at(-1).type+':'+keyEvents.at(-1).code") != "keyup:KeyW")
                throw new Exception(context + ": focus loss did not release the held game key.");
            TestKey(Keys.W, 0x11, false);
            if (!Native.IsSourceHidden(tile.Source)) throw new Exception(context + ": keyboard exposed Chrome.");
        }
        finally
        {
            ReleaseForwardedKeys(); physicalKeys.Clear(); capsLockOn = previousCaps;
            await Evaluate(pointer, "entry.style.display='none';'done'");
        }
    }

    async Task AssertThreeTileBindings(string profile, string page)
    {
        testChromeProfile = profile; chrome = FindChrome();
        ReturnToMosaic();
        string delayed = Path.Combine(Path.GetDirectoryName(page)!, "delayed.html");
        File.WriteAllText(delayed, "<title>Mosaic delayed session</title><script>setTimeout(()=>location.replace(" +
            JsonSerializer.Serialize(new Uri(page).AbsoluteUri) + "),1800)</script>");
        var originalIds = (await ChromePointer.Targets(profile, closing.Token)).Select(t => t.Id).ToHashSet();
        for (int index = 0; index < 3; index++)
        {
            tiles[index].Link = new Uri(delayed).AbsoluteUri;
            await OpenTile(index, replace: true);
            var tile = tiles[index];
            if (tile.Pointer?.Ready != true || tile.TargetId != tile.Pointer.TargetId || originalIds.Contains(tile.TargetId))
                throw new Exception($"Screen {index + 1}: production launch attached an old page or missed input.");
            originalIds.Add(tile.TargetId);
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (await Evaluate(tile.Pointer, "typeof keyEvents") == "object") break;
                await Task.Delay(100);
            }
            // Prove the protocol connection controls the HWND displayed in this
            // tile, rather than another page with an identical session URL.
            string marker = "Mosaic page binding " + index;
            await Evaluate(tile.Pointer, "document.title=" + JsonSerializer.Serialize(marker));
            for (int attempt = 0; attempt < 20 && Native.Title(tile.Source) != marker; attempt++) await Task.Delay(50);
            if (Native.Title(tile.Source) != marker) throw new Exception($"Screen {index + 1}: keyboard page and displayed window differ.");
        }
        if (tiles.Take(3).Select(t => t.TargetId).Distinct().Count() != 3) throw new Exception("Multiple tiles shared an input page.");
        for (int round = 0; round < 2; round++)
        {
            foreach (int index in new[] { 2, 1, 0, 2 })
            {
                var untouched = new Dictionary<Tile, string>();
                foreach (var tile in tiles.Take(3).Where(t => t != tiles[index])) untouched[tile] = await Evaluate(tile.Pointer!, "entry.value");
                await AssertChromeKeyboard(tiles[index], tiles[index].Pointer!, $"exact page screen {index + 1}, round {round + 1}");
                foreach (var pair in untouched)
                    if (await Evaluate(pair.Key.Pointer!, "entry.value") != pair.Value) throw new Exception("Typing changed a different screen.");
            }
            if (round == 0)
            {
                string oldTarget = tiles[2].TargetId;
                await OpenTile(2, replace: true);
                if (tiles[2].TargetId == oldTarget || tiles[2].Pointer?.Ready != true) throw new Exception("Reconnect reused the stale page.");
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    if (await Evaluate(tiles[2].Pointer!, "typeof keyEvents") == "object") break;
                    await Task.Delay(100);
                }
                string expectedTarget = tiles[1].TargetId;
                tiles[1].Pointer!.Dispose(); tiles[1].NextInputAttempt = default;
                ReconnectInputIfNeeded(tiles[1]);
                for (int attempt = 0; attempt < 60 && tiles[1].Pointer?.Ready != true; attempt++) await Task.Delay(100);
                if (tiles[1].Pointer?.Ready != true || tiles[1].Pointer!.TargetId != expectedTarget)
                    throw new Exception("Input recovery did not reconnect to the same displayed page.");
            }
        }
    }

    async Task AssertKeyboardTileSwitch(Tile first, Tile second)
    {
        ReturnToMosaic(); SelectTile(tiles.IndexOf(first));
        var a = first.Pointer!; var b = second.Pointer!;
        await Evaluate(a, "keyEvents=[];'reset'"); await Evaluate(b, "keyEvents=[];'reset'");
        TestKey(Keys.LControlKey, 0x1d, true);
        TapTestKey(Keys.D1, 0x02);
        await Task.Delay(80); // Run the same deferred selection used by the hook.
        if (selected != 0) throw new Exception("Ctrl+1 did not select the first tile.");
        TapTestKey(Keys.C, 0x2e); TestKey(Keys.LControlKey, 0x1d, false);
        await a.Flush(); await b.Flush();
        if (await Evaluate(a, "keyEvents.some(e=>e.code==='Digit1'||e.code==='KeyC')?'leak':'ok'") != "ok" ||
            await Evaluate(a, "keyEvents.at(-1).type+':'+keyEvents.at(-1).code") != "keyup:ControlLeft" ||
            await Evaluate(b, "keyEvents.some(e=>e.code==='KeyC'&&e.type==='keydown'&&e.ctrl)?'ok':'missing'") != "ok")
            throw new Exception("Tile switch lost a modifier, stuck a key, or forwarded the local shortcut.");
        TestKey(Keys.LControlKey, 0x1d, true); TestKey(Keys.LShiftKey, 0x2a, true);
        TapTestKey(Keys.Enter, 0x1c); await Task.Delay(60);
        if (!focusMode) throw new Exception("Expand hotkey did not work with intercepted modifiers.");
        TapTestKey(Keys.M, 0x32); await Task.Delay(60);
        if (focusMode) throw new Exception("Return hotkey did not work with intercepted modifiers.");
        TestKey(Keys.LShiftKey, 0x2a, false); TestKey(Keys.LControlKey, 0x1d, false);
        await b.Flush();
        if (await Evaluate(b, "keyEvents.some(e=>e.code==='Enter'||e.code==='KeyM')?'leak':'ok'") != "ok")
            throw new Exception("Mosaic expand/return hotkeys leaked into the remote computer.");
    }
}
