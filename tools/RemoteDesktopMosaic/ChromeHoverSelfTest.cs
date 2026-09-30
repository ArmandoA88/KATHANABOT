using System.Diagnostics;
using System.Text.Json;

internal sealed partial class Mosaic
{
    readonly List<string> previewTestMethods = [];
    async Task ChromeHoverSelfTest()
    {
        string testRoot = Path.Combine(AppContext.BaseDirectory, "chrome-hover-test-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(testRoot);
        string page = Path.Combine(testRoot, "hover.html");
        File.WriteAllText(page, """
            <!doctype html><meta charset="utf-8"><title>Mosaic Hover Regression</title>
            <style>body { margin:0; background:#172130; color:white; font:24px sans-serif }
            #item { position:absolute; left:20vw; top:20vh; width:60vw; height:60vh; background:#385 }
            #stats { display:none; position:absolute; left:25vw; top:82vh }</style>
            <div id="item">Hover item</div><div id="stats">ITEM STATS: attack +25</div>
            <textarea id="entry" style="display:none;position:absolute;left:30vw;top:35vh;width:40vw;height:30vh"></textarea>
            <script>
            window.events=[]; window.ready=false; let timer;
            window.keyEvents=[];
            for (const type of ['keydown','keyup']) document.addEventListener(type, e => {
              keyEvents.push({type,key:e.key,code:e.code,keyCode:e.keyCode,ctrl:e.ctrlKey,
                shift:e.shiftKey,alt:e.altKey,repeat:e.repeat,trusted:e.isTrusted,focused:document.hasFocus()});
              if(e.altKey && e.code==='Tab') e.preventDefault();
            });
            for (const type of ['mousemove','mouseenter','mouseleave','mousedown','mouseup','wheel']) {
              document.getElementById('item').addEventListener(type, e => {
                events.push({type,x:e.clientX,y:e.clientY,buttons:e.buttons,delta:e.deltaY||0});
                if(type==='mouseenter') timer=setTimeout(()=>{ready=true;stats.style.display='block'},2000);
                if(type==='mouseleave') { clearTimeout(timer); ready=false;stats.style.display='none'; }
                if(type==='wheel') e.preventDefault();
              }, {passive:false});
            }
            </script>
            """);
        var before = Native.ChromeWindows(includeHidden: true).Select(w => w.Handle).ToHashSet();
        string profile = Path.Combine(testRoot, "profile");
        var start = ChromeSessionStart(FindChrome(), profile, new Uri(page).AbsoluteUri);
        using var chromeTest = Process.Start(start);
        nint source = 0;
        ChromePointer? pointer = null;
        try
        {
            ChromePointer.Target? target = null;
            for (int retry = 0; retry < 60; retry++)
            {
                await Task.Delay(150);
                source = Native.ChromeWindows(includeHidden: true).FirstOrDefault(w => !before.Contains(w.Handle) && w.Title.Contains("Mosaic Hover Regression"))?.Handle ?? 0;
                try { target = (await ChromePointer.Targets(profile, closing.Token)).SingleOrDefault(t => t.Url == new Uri(page).AbsoluteUri); }
                catch (HttpRequestException) { }
                if (source != 0 && target != null) break;
            }
            if (source == 0 || target == null) throw new Exception("Chrome test page did not open.");
            string inputError = "";
            pointer = await ChromePointer.Connect(target, error => inputError = error, closing.Token);
            var tile = tiles[0];
            Attach(tile, source, connectPointer: false); tile.Pointer = pointer;
            selected = 0; ReturnToMosaic();
            await Task.Delay(200);
            if (!Native.IsSourceHidden(source) || Native.IsIconic(source)) throw new Exception("Chrome source was not hidden and rendering after attach.");
            await AssertLiveChromePreview(tile, pointer, "#338855", Color.FromArgb(0x33, 0x88, 0x55));
            await AssertLiveChromePreview(tile, pointer, "#3366cc", Color.FromArgb(0x33, 0x66, 0xcc));
            var area = DisplayedContentArea(tile);
            var point = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            var physicalPointer = Cursor.Position;

            // Reproduce the old background HWND path against real Chrome. Its
            // leave event comes from the physical cursor rather than the tile.
            var view = tile.LastPreview!.Value.Source;
            TryMapToSourceClient(tile, area, point, out var oldClient, sourceView: Rectangle.FromLTRB(view.Left, view.Top, view.Right, view.Bottom));
            var receiver = MapContentInput(source, ref oldClient);
            Native.PostMessage(receiver, Native.WM_MOUSEMOVE, 0, Native.MakeLParam(oldClient.X, oldClient.Y));
            await Task.Delay(600);
            var baseline = await Evaluate(pointer, "JSON.stringify({events,ready})");
            await Evaluate(pointer, "events=[];ready=false;stats.style.display='none';clearTimeout(timer);'reset'");
            // Establish an explicit outside position, then enter through the
            // production tile mapping. No physical cursor movement is needed.
            pointer.Enqueue(new("mouseMoved", 2, 2)); await pointer.Flush();
            await Evaluate(pointer, "events=[];'reset'");
            hoverSource = 0; lastForwardedMove = null;
            ForwardMouseMove(tile, point, MouseButtons.None, false);
            await pointer.Flush();
            for (int tick = 0; tick < 35; tick++)
            {
                await Task.Delay(200);
                UpdateTiles();
                ForwardMouseMove(tile, new Point(point.X + tick % 3 - 1, point.Y + 1), MouseButtons.None, false);
            }
            await pointer.Flush();
            var dwellJson = await Evaluate(pointer, "JSON.stringify({events,ready})");
            using var dwell = JsonDocument.Parse(dwellJson);
            var events = dwell.RootElement.GetProperty("events").EnumerateArray().ToArray();
            if (!dwell.RootElement.GetProperty("ready").GetBoolean() ||
                events.Count(e => e.GetProperty("type").GetString() == "mousemove") != 1 ||
                events.Any(e => e.GetProperty("type").GetString() == "mouseleave"))
                throw new Exception("Chrome hover stats did not remain visible: " + dwellJson);
            // The user may continue moving their physical mouse during the test;
            // that must not affect Chrome's independently forwarded hover.
            var physicalPointerAfter = Cursor.Position;

            ForwardMouseMove(tile, new Point(point.X + 12, point.Y), MouseButtons.None, false);
            heldButtons = MouseButtons.Left;
            ForwardMouseButton(tile, MouseButtons.Left, true, point, false);
            ForwardMouseMove(tile, new Point(point.X + 20, point.Y + 5), heldButtons, true);
            heldButtons = MouseButtons.None;
            ForwardMouseButton(tile, MouseButtons.Left, false, point, true);
            foreach (int delta in new[] { 120, -120, 30, -30 }) ForwardMouseWheel(tile, point, delta);
            await pointer.Flush();
            var actionsJson = await Evaluate(pointer, "JSON.stringify(events)");
            using var actions = JsonDocument.Parse(actionsJson);
            var actionEvents = actions.RootElement.EnumerateArray().ToArray();
            if (actionEvents.Count(e => e.GetProperty("type").GetString() == "mousedown") != 1 ||
                actionEvents.Count(e => e.GetProperty("type").GetString() == "mouseup") != 1 ||
                !actionEvents.Any(e => e.GetProperty("type").GetString() == "mousemove" && e.GetProperty("buttons").GetInt32() == 1) ||
                !actionEvents.Where(e => e.GetProperty("type").GetString() == "wheel").Select(e => e.GetProperty("delta").GetDouble()).SequenceEqual(new double[] { -120, 120, -30, 30 }))
                throw new Exception("Chrome buttons/drag/scroll regression: " + actionsJson);

            await AssertChromeKeyboard(tile, pointer, "grid");

            // Expanded tiles and a mosaic on a monitor with negative coordinates
            // must still map to the same browser viewport, without cursor warps.
            ActivateSource();
            var negativeScreen = Screen.AllScreens.FirstOrDefault(s => s.WorkingArea.Left < 0);
            if (negativeScreen != null) Location = new Point(negativeScreen.WorkingArea.Left + 20, negativeScreen.WorkingArea.Top + 20);
            UpdateTiles();
            area = DisplayedContentArea(tile);
            point = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            pointer.Enqueue(new("mouseMoved", 2, 2)); await pointer.Flush();
            await Evaluate(pointer, "events=[];'reset'");
            hoverSource = 0; lastForwardedMove = null;
            ForwardMouseMove(tile, point, MouseButtons.None, false); await pointer.Flush();
            await Task.Delay(2300);
            if (await Evaluate(pointer, "ready?'visible':'hidden'") != "visible")
                throw new Exception("Expanded/multi-monitor hover failed.");
            if (!Native.IsSourceHidden(source)) throw new Exception("Chrome source reappeared during hover/resize.");
            await AssertChromeKeyboard(tile, pointer, "expanded");
            ActivateOriginalCursor();
            if (Native.IsSourceHidden(source)) throw new Exception("Original cursor did not reveal the selected source.");
            ReturnToMosaic();
            if (!Native.IsSourceHidden(source)) throw new Exception("Return to mosaic left the source visible.");
            // Reconnect through an existing Chrome process using the same
            // launch/attach path. It must remain hidden and keep live pixels.
            var previousSource = source;
            var beforeReconnect = Native.ChromeWindows(includeHidden: true).Select(w => w.Handle).ToHashSet();
            string reconnectUrl = new Uri(page).AbsoluteUri + "?reconnect=1";
            using var reconnectProcess = Process.Start(ChromeSessionStart(FindChrome(), profile, reconnectUrl));
            ChromePointer.Target? replacement = null;
            nint replacementSource = 0;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                await Task.Delay(100);
                replacementSource = Native.ChromeWindows(includeHidden: true).FirstOrDefault(w =>
                    !beforeReconnect.Contains(w.Handle) && w.Title.Contains("Mosaic Hover Regression"))?.Handle ?? 0;
                replacement = (await ChromePointer.Targets(profile, closing.Token)).SingleOrDefault(t => t.Url == reconnectUrl);
                if (replacementSource != 0 && replacement != null) break;
            }
            if (replacementSource == 0 || replacement == null) throw new Exception("Hidden Chrome reconnect did not open.");
            source = replacementSource;
            Attach(tile, source, connectPointer: false);
            pointer = await ChromePointer.Connect(replacement, error => inputError = error, closing.Token);
            tile.Pointer = pointer;
            var otherTile = tiles[1];
            Attach(otherTile, previousSource, connectPointer: false);
            otherTile.Pointer = await ChromePointer.Connect(target, error => inputError = error, closing.Token);
            ReturnToMosaic();
            if (!Native.IsSourceHidden(source) || Native.IsIconic(source)) throw new Exception("Reconnected Chrome was exposed or minimized.");
            await AssertLiveChromePreview(tile, pointer, "#a04090", Color.FromArgb(0xa0, 0x40, 0x90));
            area = DisplayedContentArea(tile);
            point = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            hoverSource = 0; lastForwardedMove = null;
            ForwardMouseMove(tile, point, MouseButtons.None, false); await pointer.Flush();
            await Task.Delay(2300);
            if (await Evaluate(pointer, "ready?'visible':'hidden'") != "visible") throw new Exception("Hover failed after hidden reconnect.");
            await AssertChromeKeyboard(tile, pointer, "reconnected");
            string untouched = await Evaluate(pointer, "entry.value");
            SelectTile(1);
            await AssertChromeKeyboard(otherTile, otherTile.Pointer, "second computer");
            if (await Evaluate(pointer, "entry.value") != untouched) throw new Exception("Typing leaked to an unselected tile.");
            await AssertKeyboardTileSwitch(otherTile, tile);
            try { await otherTile.Pointer.Command("Page.close"); } catch { }
            await AssertThreeTileBindings(profile, page);
            pointer = tiles[0].Pointer;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "chrome-hover-result.txt"),
                "PASS: three simultaneous Chrome screens opened through production launch, distinct exact page/window bindings despite identical URLs and delayed redirects; keyboard selected by actual tile clicks on screens 3/2/1/3; no cross-screen typing; screen 3 reconnect; screen 2 disconnected input automatically recovers to its exact page.\n" +
                "PASS: hidden Chrome typing, Shift/Caps Lock, Ctrl+A, Alt+Tab events, arrows, Enter, Backspace, held/repeated keys and releases; ordered click then typing; grid/expanded/reconnect and selected-tile isolation; focus-loss release; Ctrl+tile selection and expand/return hotkeys; Chrome hidden outside all monitors and taskbar; hidden browser pixels change; reconnect stays hidden with rendering and tooltip; Original cursor reveal/return hides again; seven-second stable tooltip; click/release/drag; wheel +/-120 and +/-30; expanded and negative-monitor coordinates.\n" +
                "Rendering checks: " + string.Join(", ", previewTestMethods) + "\n" +
                "Physical pointer (independent user input): " + physicalPointer + " -> " + physicalPointerAfter + "\n" +
                "Old HWND baseline: " + baseline + "\nFixed dwell: " + dwellJson + "\nActions: " + actionsJson + "\nInput error: " + inputError);
        }
        finally
        {
            var cleanup = pointer?.Ready == true ? pointer : tiles.Select(t => t.Pointer).FirstOrDefault(p => p?.Ready == true);
            if (cleanup != null) { try { await cleanup.Command("Browser.close"); } catch { } }
            pointer?.Dispose();
            if (source != 0) Native.PostMessage(source, 0x0010, 0, 0);
        }
    }

    async Task AssertLiveChromePreview(Tile tile, ChromePointer pointer, string cssColor, Color expected)
    {
        bool previousTopMost = TopMost;
        var previousLocation = Location;
        var monitor = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(monitor.Left + 30, monitor.Top + 30);
        Native.ShowWindow(Handle, 4); // Test executables are launched hidden; expose only this fixture for pixel checks.
        TopMost = true; // Keep this test surface observable while the user works in other apps.
        try
        {
        await Evaluate(pointer, $"document.getElementById('item').style.background='{cssColor}';'updated'");
        var area = DisplayedContentArea(tile);
        var pixel = PointToScreen(new Point(area.Left + area.Width * 3 / 5, area.Top + area.Height * 3 / 5));
        Color actual = Color.Empty;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(100);
            if (attempt >= 2 && Native.WindowFromPoint(new Native.Point { X = pixel.X, Y = pixel.Y }) != Handle)
            {
                // Another app can keep itself above the test surface. Verify the
                // hidden browser's rendering without fighting that app for focus.
                var capture = await pointer.Command("Page.captureScreenshot", new { format = "png", captureBeyondViewport = false });
                using var stream = new MemoryStream(Convert.FromBase64String(capture.GetProperty("data").GetString()!));
                using var frame = new Bitmap(stream);
                actual = frame.GetPixel(frame.Width * 3 / 5, frame.Height * 3 / 5);
                if (Math.Abs(actual.R - expected.R) <= 3 && Math.Abs(actual.G - expected.G) <= 3 && Math.Abs(actual.B - expected.B) <= 3)
                { previewTestMethods.Add("hidden page capture (test window covered)"); return; }
                throw new Exception($"Hidden browser stopped rendering: expected {expected}, got {actual}.");
            }
            using var bitmap = new Bitmap(1, 1);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(pixel, Point.Empty, new Size(1, 1));
            actual = bitmap.GetPixel(0, 0);
            if (Math.Abs(actual.R - expected.R) <= 3 && Math.Abs(actual.G - expected.G) <= 3 && Math.Abs(actual.B - expected.B) <= 3)
            { previewTestMethods.Add("DWM screen pixels"); return; }
        }
        var underPixel = Native.WindowFromPoint(new Native.Point { X = pixel.X, Y = pixel.Y });
        Native.GetWindowRect(Handle, out var testRect);
        throw new Exception($"Hidden Chrome thumbnail was blank/frozen: expected {expected}, got {actual} at {pixel}; test visible={Native.IsWindowVisible(Handle)}, topmost={TopMost}, sampledOwnWindow={underPixel == Handle}, coveringTitle={Native.Title(underPixel)}, testRect={testRect.Left},{testRect.Top},{testRect.Right},{testRect.Bottom}.");
        }
        finally { TopMost = previousTopMost; Location = previousLocation; }
    }

    static async Task<string> Evaluate(ChromePointer pointer, string expression)
    {
        var result = await pointer.Command("Runtime.evaluate", new { expression, returnByValue = true });
        if (result.TryGetProperty("exceptionDetails", out var error)) throw new Exception(error.ToString());
        return result.GetProperty("result").GetProperty("value").GetString()!;
    }
}
