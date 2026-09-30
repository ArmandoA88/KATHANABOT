using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// Everything the mosaic remembers - saved computers, their nicknames, and window position/size -
// lives in one JSON file next to the exe, so the app is a single portable pair (exe + this file)
// instead of a plain-text links file, a separate nicknames file, and a third window-state file
// under the user profile.
internal sealed class ComputerEntry
{
    public string Link { get; set; } = "";
    public string Nickname { get; set; } = "";
}
internal sealed class MosaicSettings
{
    public List<ComputerEntry> Computers { get; set; } = [];
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public string ExpandHotkey { get; set; } = "Ctrl+Shift+Enter";
    public string MosaicHotkey { get; set; } = "Ctrl+Shift+M";
}

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            bool demo = args.Contains("--self-test") || args.Contains("--chrome-input-test");
            using var instance = new Mutex(true, demo ? "Local\\RemoteDesktopMosaic.Test." + Environment.ProcessId : "Local\\RemoteDesktopMosaic.SingleInstance", out bool firstInstance);
            if (!firstInstance)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    nint existing = Native.FindMosaic();
                    if (existing != 0) { Native.ShowWindow(existing, 9); Native.SetForegroundWindow(existing); return; }
                    Thread.Sleep(50);
                }
                return; // The first instance is still starting; do not open another set.
            }
            string settingsPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(AppContext.BaseDirectory, "RemoteDesktopMosaic.settings.json");
            MosaicSettings settings;
            if (demo)
            {
                settings = new MosaicSettings { Computers = Enumerable.Repeat("", 4).Select(s => new ComputerEntry { Link = s }).ToList() };
            }
            else
            {
                settings = LoadOrMigrateSettings(settingsPath);
                settings.Computers.RemoveAll(c => string.IsNullOrWhiteSpace(c.Link));
                if (settings.Computers.Count == 0 && !PromptForFirstComputer(settings)) return; // User cancelled first-run setup.
                foreach (var c in settings.Computers)
                    if (!IsValidLink(c.Link)) throw new Exception("Invalid Chrome Remote Desktop session link stored in " + settingsPath);
            }
            Application.Run(new Mosaic(settings, demo, settingsPath) { TestChromeInput = args.Contains("--chrome-input-test") });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Remote Desktop Mosaic"); Environment.ExitCode = 1; }
    }

    internal static bool IsValidLink(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "remotedesktop.google.com" &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath, @"^/(?:u/\d+/)?access/session/[a-zA-Z0-9_-]+/?$");

    sealed class LegacyWindowState { public int X { get; set; } public int Y { get; set; } public int Width { get; set; } public int Height { get; set; } public bool Maximized { get; set; } }

    static MosaicSettings LoadOrMigrateSettings(string settingsPath)
    {
        try { if (File.Exists(settingsPath)) return JsonSerializer.Deserialize<MosaicSettings>(File.ReadAllText(settingsPath)) ?? new MosaicSettings(); }
        catch { /* Corrupt/unreadable settings file; fall through and start fresh rather than crash launch. */ }

        // One-time migration from the older scattered files (RemoteDesktopLinks.txt + RemoteDesktopNicknames.json
        // next to it, and window-state.json under LocalAppData), so upgrading doesn't lose an existing setup.
        var settings = new MosaicSettings();
        try
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? AppContext.BaseDirectory;
            string legacyLinks = Path.Combine(dir, "RemoteDesktopLinks.txt");
            string legacyNicknames = Path.Combine(dir, "RemoteDesktopNicknames.json");
            string legacyWindow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteDesktopMosaic", "window-state.json");
            var nicknames = File.Exists(legacyNicknames) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(legacyNicknames)) ?? [] : [];
            if (File.Exists(legacyLinks))
                foreach (var link in File.ReadAllLines(legacyLinks).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).Distinct())
                    settings.Computers.Add(new ComputerEntry { Link = link, Nickname = nicknames.TryGetValue(link, out var nick) ? nick : "" });
            if (File.Exists(legacyWindow))
            {
                var window = JsonSerializer.Deserialize<LegacyWindowState>(File.ReadAllText(legacyWindow));
                if (window != null)
                {
                    settings.WindowX = window.X; settings.WindowY = window.Y;
                    settings.WindowWidth = window.Width; settings.WindowHeight = window.Height;
                    settings.WindowMaximized = window.Maximized;
                }
            }
        }
        catch { /* Best-effort migration only. */ }
        return settings;
    }

    static bool PromptForFirstComputer(MosaicSettings settings)
    {
        MessageBox.Show("No computers saved yet. Add at least one Chrome Remote Desktop computer to continue.", "Remote Desktop Mosaic", MessageBoxButtons.OK, MessageBoxIcon.Information);
        while (true)
        {
            if (!Mosaic.PromptForText(null, "Add computer", "Paste the computer's Chrome Remote Desktop session link (copy it from the address bar after selecting a computer):", "", out var link))
                return settings.Computers.Count > 0;
            link = link.Trim();
            if (!IsValidLink(link)) { MessageBox.Show("Not a computer session link. Select a computer in Chrome Remote Desktop, then copy its address-bar link.", "Add computer"); continue; }
            Mosaic.PromptForText(null, "Add computer", "Optional nickname for this tile:", "", out var nickname);
            settings.Computers.Add(new ComputerEntry { Link = link, Nickname = nickname.Trim() });
            if (MessageBox.Show("Add another computer?", "Remote Desktop Mosaic", MessageBoxButtons.YesNo) != DialogResult.Yes) return true;
        }
    }
}

internal sealed partial class Mosaic : Form, IMessageFilter
{
    internal bool TestChromeInput;
    sealed class Tile(string link)
    {
        public string Link = link;
        public string Nickname = "";
        public nint Source, Thumbnail;
        public Rectangle Bounds;
        public Native.ThumbnailProperties? LastPreview;
        public string Status = "Waiting to open...";
        public ChromePointer? Pointer;
        public bool IsChrome;
        public string InputStatus = "";
        public string TargetId = "";
        public bool InputConnecting;
        public DateTime NextInputAttempt;
    }

    const int TitleStripHeight = 20, ContentMargin = 2, ContentBottomMargin = 4, ReorderDragThreshold = 6;
    const int TileButtonWidth = 28;
    const int SidebarWidth = 172, WorkspaceMargin = 6, InstructionBarHeight = 30;

    readonly List<Tile> tiles;
    readonly bool demo;
    readonly string settingsPath;
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 500 };
    readonly List<Form> demoSources = [];
    readonly CancellationTokenSource closing = new();
    readonly OwnedSessionWindows ownedSessions = new();
    int selected;
    bool opening;
    bool focusMode;
    string chrome = "";
    string? testChromeProfile;
    string InputProfile => demo && testChromeProfile != null ? testChromeProfile : DedicatedProfileDir;
    nint expandedSource;
    nint keyboardHook;
    string expandHotkey;
    string mosaicHotkey;
    bool capsLockOn = Control.IsKeyLocked(Keys.CapsLock);
    readonly HashSet<int> consumedHotkeyKeys = [];
    Native.KeyboardProc? keyboardProc;
    int? inputDragTile;
    MouseButtons heldButtons;
    (nint Source, Point Position, nint Buttons)? lastForwardedMove;
    nint hoverSource;
    Point hoverAnchor;
    const int HoverJitterPixels = 4;
    int? reorderCandidate;
    System.Drawing.Point reorderStartPoint;
    int? reorderDragTile;
    int? dropTargetTile;
    readonly Label instructions = new() { AutoSize = false, AutoEllipsis = true, ForeColor = Color.LightSteelBlue };

    public Mosaic(MosaicSettings settings, bool selfTest, string settingsPath)
    {
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        demo = selfTest;
        this.settingsPath = settingsPath;
        expandHotkey = string.IsNullOrWhiteSpace(settings.ExpandHotkey) ? "Ctrl+Shift+Enter" : settings.ExpandHotkey;
        mosaicHotkey = string.IsNullOrWhiteSpace(settings.MosaicHotkey) ? "Ctrl+Shift+M" : settings.MosaicHotkey;
        tiles = settings.Computers.Select(c => new Tile(c.Link) { Nickname = c.Nickname }).ToList();
        Text = "Remote Desktop Mosaic";
        BackColor = Color.FromArgb(15, 20, 29); ForeColor = Color.White;
        MinimumSize = new Size(800, 550); Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
        if (!demo && settings.WindowWidth >= 400 && settings.WindowHeight >= 300 &&
            IsOnAnyScreen(new Rectangle(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight)))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight);
            if (settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        }
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = SidebarWidth, Padding = new Padding(8),
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.FromArgb(25, 33, 46)
        };
        var open = new Button { Text = "Maximize selected", Width = SidebarWidth - 24, Height = 34 };
        var originalCursor = new Button { Text = "Original cursor", Width = SidebarWidth - 24, Height = 34 };
        var minimizeSelected = new Button { Text = "Minimize selected", Width = SidebarWidth - 24, Height = 34 };
        var hotkeys = new Button { Text = "Hotkeys...", Width = SidebarWidth - 24, Height = 34 };
        var reconnect = new Button { Text = "Reconnect selected", Width = SidebarWidth - 24, Height = 34 };
        var reconnectAll = new Button { Text = "Reconnect all", Width = SidebarWidth - 24, Height = 34 };
        var attach = new Button { Text = "Choose window...", Width = SidebarWidth - 24, Height = 34 };
        var addComputer = new Button { Text = "Add computer", Width = SidebarWidth - 24, Height = 34 };
        var removeComputer = new Button { Text = "Remove computer", Width = SidebarWidth - 24, Height = 34 };
        foreach (var button in new[] { open, originalCursor, minimizeSelected, reconnect, reconnectAll, attach, addComputer, removeComputer, hotkeys })
        {
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(40, 58, 80);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(105, 150, 190);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 85, 115);
            bar.Controls.Add(button);
        }
        open.Click += (_, _) => ActivateSource();
        originalCursor.Click += (_, _) => ActivateOriginalCursor();
        originalCursor.BackColor = Color.FromArgb(20, 115, 95);
        minimizeSelected.Click += (_, _) => ReturnToMosaic();
        reconnect.Click += async (_, _) => { if (!opening) await OpenTile(selected, replace: true); };
        reconnectAll.Click += async (_, _) => { for (int i = 0; i < tiles.Count; i++) if (!opening) await OpenTile(i, replace: true); };
        attach.Click += (_, _) => ChooseWindow();
        addComputer.Click += async (_, _) => await AddComputerPrompt();
        removeComputer.Click += (_, _) => { if (!opening) RemoveTile(selected); };
        hotkeys.Click += (_, _) => ConfigureHotkeys();
        instructions.Text = "Hover stays fixed until you move over 4 px | Original cursor: direct session | " +
            $"Ctrl+1-9 select tile | Expand: {expandHotkey} | Return to mosaic: {mosaicHotkey}";
        instructions.Dock = DockStyle.Bottom; instructions.Height = InstructionBarHeight; instructions.Padding = new Padding(SidebarWidth + 10, 6, 0, 0);
        Controls.Add(bar); Controls.Add(instructions);
        Activated += (_, _) => { if (expandedSource != 0) { expandedSource = 0; lastForwardedMove = null; refresh.Start(); UpdateTiles(); } };
        Deactivate += (_, _) => ReleaseForwardedKeys();
        Resize += (_, _) => UpdateTiles();
        refresh.Tick += (_, _) => UpdateTiles();
        Application.AddMessageFilter(this);
        MouseDown += (_, e) =>
        {
            int index = tiles.FindIndex(t => t.Bounds.Contains(e.Location));
            if (index < 0) return;
            SelectTile(index);
            if (TitleStrip(tiles[index].Bounds).Contains(e.Location))
            {
                if (e.Button == MouseButtons.Right) { ShowTileMenu(index, PointToScreen(e.Location)); Invalidate(); return; }
                if (e.Button == MouseButtons.Left && TileMaximizeButton(tiles[index].Bounds).Contains(e.Location))
                { ActivateSource(); return; }
                if (e.Button == MouseButtons.Left && TileMinimizeButton(tiles[index].Bounds).Contains(e.Location))
                { ReturnToMosaic(); return; }
                reorderCandidate = index; reorderStartPoint = e.Location;
            }
            else
            {
                inputDragTile = index; heldButtons |= e.Button;
                ForwardMouseButton(tiles[index], e.Button, down: true, e.Location, dragging: false);
            }
            Invalidate();
        };
        MouseUp += (_, e) =>
        {
            if (reorderCandidate.HasValue)
            {
                if (reorderDragTile is int from && dropTargetTile is int to && from != to && from < tiles.Count && to < tiles.Count)
                {
                    var moved = tiles[from]; tiles.RemoveAt(from); tiles.Insert(to, moved);
                    selected = to; SaveSettings();
                }
                reorderCandidate = null; reorderDragTile = null; dropTargetTile = null;
                UpdateTiles(); Invalidate();
                return;
            }
            heldButtons &= ~e.Button;
            if (inputDragTile is int index)
            {
                ForwardMouseButton(tiles[index], e.Button, down: false, e.Location, dragging: true);
                if (heldButtons == MouseButtons.None) inputDragTile = null;
            }
        };
        // Leaving the mosaic/entering its toolbar must not disturb the last
        // remote hover. A different tile, click, drag, or layout resets it.
        MouseMove += (_, e) =>
        {
            if (demo) return; // Self-tests provide explicit input independently of the user's mouse.
            if (reorderCandidate.HasValue && !reorderDragTile.HasValue)
            {
                int dx = e.Location.X - reorderStartPoint.X, dy = e.Location.Y - reorderStartPoint.Y;
                if (dx * dx + dy * dy >= ReorderDragThreshold * ReorderDragThreshold) reorderDragTile = reorderCandidate;
            }
            if (reorderDragTile.HasValue)
            {
                int over = tiles.FindIndex(t => t.Bounds.Contains(e.Location));
                if (over != dropTargetTile) { dropTargetTile = over; Invalidate(); }
                return;
            }
            int index = inputDragTile ?? tiles.FindIndex(t => t.Bounds.Contains(e.Location));
            if (index >= 0) ForwardMouseMove(tiles[index], e.Location, heldButtons, dragging: inputDragTile.HasValue);
        };
        MouseWheel += (_, e) =>
        {
            int index = tiles.FindIndex(t => t.Bounds.Contains(e.Location));
            if (index >= 0) ForwardMouseWheel(tiles[index], e.Location, e.Delta);
        };
        Shown += async (_, _) =>
        {
            if (!demo) Native.SetProp(Handle, "RemoteDesktopMosaic.MainWindow", 1);
            keyboardProc = KeyboardCallback;
            keyboardHook = Native.SetWindowsHookEx(13, keyboardProc, Native.GetModuleHandle(null), 0);
            if (keyboardHook == 0) instructions.Text = "Keyboard shortcuts unavailable; use the toolbar buttons.";
            refresh.Start();
            if (demo) { await SelfTest(); return; }
            try
            {
                chrome = FindChrome();
                EnsureDedicatedProfileSeeded(this);
                await PrepareChromePointer();
                for (int i = 0; i < tiles.Count && !closing.IsCancellationRequested; i++) await OpenTile(i);
                if (!IsDisposed) { Activate(); UpdateTiles(); WriteInputStatus(); }
            }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, Text); }
        };
        FormClosing += (_, _) =>
        {
            Application.RemoveMessageFilter(this);
            SaveSettings();
            closing.Cancel(); refresh.Stop();
            if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
            keyboardHook = 0;
            if (!demo) Native.RemoveProp(Handle, "RemoteDesktopMosaic.MainWindow");
            foreach (var t in tiles) Unregister(t);
            foreach (var f in demoSources) f.Close();
        };
        FormClosed += (_, _) => { refresh.Dispose(); closing.Dispose(); };
    }

    static string FindChrome()
    {
        foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
        { var file = Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe"); if (File.Exists(file)) return file; }
        throw new Exception("Google Chrome was not found.");
    }

    static string DedicatedProfileDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteDesktopMosaic", "ChromeProfile");

    static bool IsOnAnyScreen(Rectangle bounds) => Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds));

    // Every computer, nickname, and the window's own position/size are written back together as one
    // file on every change, so the exe never depends on more than this single companion file.
    void SaveSettings()
    {
        if (demo) return;
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var settings = new MosaicSettings
            {
                Computers = tiles.Select(t => new ComputerEntry { Link = t.Link, Nickname = t.Nickname }).ToList(),
                WindowX = bounds.X, WindowY = bounds.Y, WindowWidth = bounds.Width, WindowHeight = bounds.Height,
                WindowMaximized = WindowState == FormWindowState.Maximized,
                ExpandHotkey = expandHotkey,
                MosaicHotkey = mosaicHotkey
            };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* Best-effort; worst case the next launch keeps the previous saved setup. */ }
    }

    // A window's background-rendering flags only take effect on the process that first opens it.
    // If the mosaic reused the user's already-running everyday Chrome, these switches are silently
    // ignored and every covered/backgrounded tile freezes on its last-rendered frame. A dedicated
    // profile directory forces Chrome to start a genuinely new, separate process every time, so the
    // flags always apply and every tile keeps rendering live.
    //
    // To avoid a fresh Google sign-in in that empty profile, it is seeded once (only if still empty)
    // from the user's real Chrome profile's login cookies and encryption key. Chrome opens its own
    // Cookies database with an exclusive lock while running, so this file cannot be read by another
    // process no matter what sharing flags that process requests - copying while Chrome is running
    // always fails silently. The only reliable way to seed it is to briefly close Chrome first, which
    // is disruptive enough that it requires the user's explicit one-time permission.
    static void EnsureDedicatedProfileSeeded(IWin32Window owner)
    {
        try
        {
            string destRoot = DedicatedProfileDir;
            string destDefault = Path.Combine(destRoot, "Default");
            if (File.Exists(Path.Combine(destDefault, "Network", "Cookies")) || File.Exists(Path.Combine(destDefault, "Cookies")))
                return; // Never overwrite a profile that has already been used/signed in.

            string sourceRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
            string sourceDefault = Path.Combine(sourceRoot, "Default");
            if (!Directory.Exists(sourceDefault)) return;

            var runningChrome = Process.GetProcessesByName("chrome");
            if (runningChrome.Length > 0)
            {
                var choice = MessageBox.Show(owner,
                    "To keep every session automatically signed in (instead of asking every time), " +
                    "Remote Desktop Mosaic can briefly close your regular Chrome windows, copy your Google " +
                    "sign-in into its own profile, then Chrome will reopen on its own. This only needs to " +
                    "happen once.\n\nClose Chrome briefly and copy the sign-in now?\n\n" +
                    "Choose No to skip this and just sign in manually, once, inside the mosaic instead.",
                    "Remote Desktop Mosaic - one-time setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (choice != DialogResult.Yes) return;

                foreach (var window in Native.ChromeWindows()) Native.PostMessage(window.Handle, 0x10 /* WM_CLOSE */, 0, 0);
                for (int i = 0; i < 40 && Process.GetProcessesByName("chrome").Length > 0; i++) Thread.Sleep(250);
                if (Process.GetProcessesByName("chrome").Length > 0)
                {
                    // Still running - e.g. an "unsaved changes"/background-apps prompt held it open.
                    // Never force-kill the user's regular browser; just fall back to manual sign-in.
                    return;
                }
            }

            Directory.CreateDirectory(Path.Combine(destDefault, "Network"));
            // Local State holds the OS-encryption key used to decrypt Cookies; it must come from
            // the same source as the cookie files or the copied cookies cannot be decrypted.
            CopyBestEffort(Path.Combine(sourceRoot, "Local State"), Path.Combine(destRoot, "Local State"));
            CopyBestEffort(Path.Combine(sourceDefault, "Network", "Cookies"), Path.Combine(destDefault, "Network", "Cookies"));
            CopyBestEffort(Path.Combine(sourceDefault, "Cookies"), Path.Combine(destDefault, "Cookies"));
            CopyBestEffort(Path.Combine(sourceDefault, "Preferences"), Path.Combine(destDefault, "Preferences"));

            if (runningChrome.Length > 0) Process.Start(new ProcessStartInfo(FindChrome()) { UseShellExecute = true });
        }
        catch { /* Best-effort only; worst case the user signs in once, same as before. */ }
    }

    static void CopyBestEffort(string source, string destination)
    {
        try
        {
            if (!File.Exists(source)) return;
            using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            src.CopyTo(dst);
        }
        catch { /* Source locked/unavailable; skip this file and continue with the rest. */ }
    }

    async Task OpenTile(int index, bool replace = false)
    {
        if (opening || closing.IsCancellationRequested || index < 0 || index >= tiles.Count) return;
        opening = true;
        var t = tiles[index];
        try
        {
            if (!replace && Native.IsWindow(t.Source)) { KeepSourceBehindMosaic(t.Source); return; }
            t.Status = "Opening Chrome..."; Invalidate();
            var before = Native.ChromeWindows(includeHidden: true).Select(w => w.Handle).ToHashSet();
            // Snapshot page identities before launch. A previous window can
            // have the same session URL, while the new page starts at a redirect
            // or sign-in page. Matching by URL can silently control the old one.
            HashSet<string> beforeTargets;
            try { beforeTargets = (await ChromePointer.Targets(InputProfile, closing.Token)).Select(p => p.Id).ToHashSet(); }
            catch (HttpRequestException) { beforeTargets = []; }
            catch (IOException) { beforeTargets = []; }
            var start = ChromeSessionStart(chrome, InputProfile, t.Link);
            using var process = Process.Start(start);
            nint openedSource = 0;
            ChromePointer.Target? openedTarget = null;
            for (int attempt = 0; attempt < 150; attempt++)
            {
                await Task.Delay(100, closing.Token);
                var candidates = Native.ChromeWindows(includeHidden: true).Where(w => w.Title.Length > 0 && !before.Contains(w.Handle) && !tiles.Any(tile => tile.Source == w.Handle)).ToArray();
                if (openedSource == 0 && candidates.Length == 1)
                {
                    Attach(t, candidates[0].Handle, connectPointer: false);
                    if (t.Source != candidates[0].Handle) return;
                    openedSource = t.Source;
                    t.InputStatus = "Connecting this screen's keyboard and mouse...";
                }
                try { openedTarget ??= ChromePointer.FindOpenedTarget(await ChromePointer.Targets(InputProfile, closing.Token), beforeTargets); }
                catch (HttpRequestException) { }
                catch (IOException) { }
                if (openedSource != 0 && openedTarget != null)
                {
                    t.TargetId = openedTarget.Id;
                    ownedSessions.RecordAndCloseOlder(t.Link, openedSource, openedTarget.Id);
                    await ConnectChromePointer(t, openedSource);
                    return;
                }
                if (openedSource == 0 && candidates.Length > 1) break;
            }
            if (openedSource != 0) t.InputStatus = "This screen's input page did not open. Reconnect selected.";
            else t.Status = "Select this tile, then Choose window to attach Chrome.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { t.Status = ex.Message; }
        finally { opening = false; if (!IsDisposed) { UpdateTiles(); if (expandedSource == 0) Activate(); } }
    }

    static ProcessStartInfo ChromeSessionStart(string executable, string profile, string link)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        // A dedicated, auto-seeded profile (see EnsureDedicatedProfileSeeded) guarantees a
        // genuinely new Chrome process every time, so these switches reliably apply and every
        // tile keeps rendering live instead of freezing on its last frame while covered.
        start.ArgumentList.Add("--user-data-dir=" + profile);
        start.ArgumentList.Add("--remote-debugging-port=0");
        start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        start.ArgumentList.Add("--disable-backgrounding-occluded-windows");
        start.ArgumentList.Add("--disable-background-timer-throttling");
        start.ArgumentList.Add("--disable-renderer-backgrounding");
        // Native occlusion can otherwise leave a hidden-start/off-desktop
        // Chrome compositor blank even though the page is processing input.
        start.ArgumentList.Add("--disable-features=CalculateNativeWinOcclusion");
        start.ArgumentList.Add("--no-first-run");
        start.ArgumentList.Add("--no-default-browser-check");
        // Give Chrome Remote Desktop enough real rendering pixels. The expanded mosaic view
        // otherwise upscales Chrome's small remembered window and looks blurry.
        start.ArgumentList.Add("--window-size=1600,940");
        // Also covers reconnects routed through an already running Chrome
        // process, where the new process's STARTUPINFO may be ignored.
        start.ArgumentList.Add("--start-minimized");
        start.ArgumentList.Add("--app=" + link);
        start.ArgumentList.Add("--new-window");
        return start;
    }

    async Task PrepareChromePointer()
    {
        try { if ((await ChromePointer.Targets(DedicatedProfileDir, closing.Token)).Length > 0) return; }
        catch (Exception) when (!closing.IsCancellationRequested) { }
        // The port switch only applies when Chrome's dedicated process starts.
        // Gracefully close only marked session windows from earlier Mosaic builds.
        // Their saved computers are reopened below using the same profile.
        var oldWindows = ownedSessions.CloseForInputUpgrade(tiles.Select(t => t.Link));
        for (int attempt = 0; attempt < 40 && oldWindows.Any(Native.IsWindow); attempt++)
            await Task.Delay(100, closing.Token);
        if (oldWindows.Any(Native.IsWindow))
            throw new IOException("Close the previous Mosaic session windows, then reopen Mosaic to finish the cursor update.");
        if (oldWindows.Length > 0) await Task.Delay(1000, closing.Token);
    }

    async Task ConnectChromePointer(Tile tile, nint source)
    {
        if (!tile.IsChrome || tile.InputConnecting || tile.TargetId.Length == 0) return;
        tile.InputConnecting = true;
        string targetId = tile.TargetId;
        try
        {
            tile.InputStatus = "Connecting keyboard and mouse input...";
            string failure = "Waiting for this screen's Chrome page.";
            for (int attempt = 0; attempt < 20 && !closing.IsCancellationRequested && tile.Source == source; attempt++)
            {
                try
                {
                    var targets = await ChromePointer.Targets(InputProfile, closing.Token);
                    var matches = targets.Where(t => t.Id == targetId).ToArray();
                    if (matches.Length == 1)
                    {
                        var pointer = await ChromePointer.Connect(matches[0], error =>
                        {
                            if (tile.Source != source || tile.TargetId != targetId || IsDisposed) return;
                            tile.InputStatus = "Reconnecting this screen's keyboard and mouse...";
                            tile.NextInputAttempt = DateTime.UtcNow.AddSeconds(2);
                            lastForwardedMove = null; hoverSource = 0; UpdateTiles(); WriteInputStatus();
                        }, closing.Token);
                        if (tile.Source != source || tile.TargetId != targetId || closing.IsCancellationRequested) { pointer.Dispose(); return; }
                        tile.Pointer?.Dispose(); tile.Pointer = pointer;
                        tile.InputStatus = ""; lastForwardedMove = null; hoverSource = 0;
                        UpdateTiles(); WriteInputStatus();
                        return;
                    }
                }
                catch (Exception ex) when (!closing.IsCancellationRequested) { failure = ex.Message; }
                await Task.Delay(150, closing.Token);
            }
            if (tile.Source == source && !IsDisposed)
            {
                tile.InputStatus = "Retrying this screen's keyboard/mouse connection. " + failure;
                UpdateTiles(); WriteInputStatus();
            }
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested) { }
        finally
        {
            if (tile.Source == source && tile.TargetId == targetId)
            { tile.InputConnecting = false; tile.NextInputAttempt = DateTime.UtcNow.AddSeconds(3); }
        }
    }

    void ReconnectInputIfNeeded(Tile tile)
    {
        if (tile.IsChrome && tile.Pointer?.Ready != true && tile.TargetId.Length > 0 &&
            !tile.InputConnecting && DateTime.UtcNow >= tile.NextInputAttempt)
            _ = ConnectChromePointer(tile, tile.Source);
    }

    void WriteInputStatus()
    {
        if (demo) return;
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteDesktopMosaic");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "input-status.txt"),
                $"{DateTimeOffset.Now:O} | {Environment.ProcessPath}\n" +
                string.Join("\n", tiles.Select((t, i) => $"Tile {i + 1}: " +
                    (t.Pointer?.Ready == true ? "direct keyboard and mouse input ready" : t.InputStatus.Length > 0 ? t.InputStatus : t.Status) +
                    $" | exact page: {t.TargetId.Length > 0 && t.Pointer?.TargetId == t.TargetId}" +
                    (Native.IsWindow(t.Source) ? $" | source hidden: {Native.IsSourceHidden(t.Source)}" : ""))));
        }
        catch { }
    }

    void Attach(Tile tile, nint source, bool connectPointer = true)
    {
        Unregister(tile);
        // Keep the full-resolution rendering surface outside every monitor.
        // SW_HIDE/minimize would stop the live DWM thumbnail from rendering.
        var parking = Native.SourceParkingLocation();
        Native.SetWindowPos(source, Native.HWND_BOTTOM, parking.X, parking.Y, 1600, 940,
            Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        Native.HideFromTaskbar(source);
        KeepSourceBehindMosaic(source);
        int hr = Native.DwmRegisterThumbnail(Handle, source, out var thumb);
        if (hr != 0) { Native.RevealSource(source, Screen.FromControl(this).WorkingArea); tile.Status = $"Preview unavailable (0x{hr:X8}). Use Choose window to retry."; return; }
        tile.Source = source; tile.Thumbnail = thumb; tile.Status = "";
        tile.IsChrome = Native.ChromeWindows().Any(w => w.Handle == source);
        if (connectPointer && tile.IsChrome)
        {
            tile.InputStatus = "Use Reconnect selected to enable keyboard and mouse input for this computer.";
            tile.TargetId = ownedSessions.TargetForWindow(tile.Link, source);
            if (tile.TargetId.Length > 0) _ = ConnectChromePointer(tile, source);
        }
        UpdateTiles();
    }
    static void KeepSourceBehindMosaic(nint source)
    {
        Native.HideFromTaskbar(source);
        // Restore without activation, then park outside all monitors. Restoring
        // can replace a minimized window's position, so parking must happen last.
        if (!Native.IsWindowVisible(source) || Native.IsIconic(source)) Native.ShowWindow(source, 4);
        var parking = Native.SourceParkingLocation();
        Native.SetWindowPos(source, Native.HWND_BOTTOM, parking.X, parking.Y, 0, 0,
            Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER | 0x0001 /* NOSIZE */);
    }

    void Unregister(Tile t)
    {
        if (forwardedKeys.Values.Any(k => k.Tile == t)) ReleaseForwardedKeys();
        t.Pointer?.Dispose(); t.Pointer = null; t.IsChrome = false; t.InputStatus = "";
        t.TargetId = ""; t.InputConnecting = false; t.NextInputAttempt = default;
        if (t.Thumbnail != 0) Native.DwmUnregisterThumbnail(t.Thumbnail);
        t.Thumbnail = 0; t.Source = 0; t.LastPreview = null;
    }

    // The thin strip along the top of a tile is mosaic "chrome": drag it to reorder tiles, right-click
    // it to rename/remove the tile. Everything below it is the remote content itself, where every
    // click, drag, and keystroke is forwarded straight through to the real session instead.
    static Rectangle TitleStrip(Rectangle bounds) => new(bounds.X, bounds.Y, bounds.Width, TitleStripHeight);
    static Rectangle TileMaximizeButton(Rectangle bounds) => new(bounds.Right - TileButtonWidth - 2, bounds.Top + 1, TileButtonWidth, TitleStripHeight - 2);
    static Rectangle TileMinimizeButton(Rectangle bounds) => new(bounds.Right - TileButtonWidth * 2 - 2, bounds.Top + 1, TileButtonWidth, TitleStripHeight - 2);
    static Rectangle ContentArea(Rectangle bounds) => new(bounds.X + ContentMargin, bounds.Y + TitleStripHeight,
        Math.Max(1, bounds.Width - ContentMargin * 2), Math.Max(1, bounds.Height - TitleStripHeight - ContentBottomMargin));

    // Fill the whole content area without distorting the image: rather than letterboxing (blank bars)
    // or stretching (warped aspect ratio), crop a same-aspect-ratio slice out of the source window and
    // display that slice at the tile's full size. Shared by the DWM thumbnail's Source rectangle and by
    // the mouse-forwarding coordinate mapping, so a click always lands on the same pixel it visually hits.
    static Rectangle ComputeCropRect(Native.Point size, Rectangle area)
    {
        double areaAspect = area.Width / (double)area.Height;
        double sourceAspect = size.X / (double)size.Y;
        if (sourceAspect > areaAspect)
        {
            int cropWidth = Math.Max(1, (int)Math.Round(size.Y * areaAspect));
            return new Rectangle((size.X - cropWidth) / 2, 0, cropWidth, size.Y);
        }
        int cropHeight = Math.Max(1, (int)Math.Round(size.X / areaAspect));
        return new Rectangle(0, (size.Y - cropHeight) / 2, size.X, cropHeight);
    }

    // Expanded mode must show the whole remote screen. Fit it inside the app and center any
    // unused space instead of cropping the source as mosaic tiles do.
    static Rectangle ComputeFitRect(Native.Point size, Rectangle area)
    {
        if (size.X <= 0 || size.Y <= 0 || area.Width <= 0 || area.Height <= 0) return area;
        double scale = Math.Min(area.Width / (double)size.X, area.Height / (double)size.Y);
        int width = Math.Max(1, (int)Math.Round(size.X * scale));
        int height = Math.Max(1, (int)Math.Round(size.Y * scale));
        return new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }

    static Rectangle ExpandedRemoteSourceRect(nint source, Native.Point fullSize)
    {
        var fallback = new Rectangle(0, 0, fullSize.X, fullSize.Y);
        if (!Native.GetWindowRect(source, out var window) || !Native.GetClientRect(source, out var client)) return fallback;
        var clientOrigin = new Native.Point();
        if (!Native.ClientToScreen(source, ref clientOrigin)) return fallback;
        int clientWidth = Math.Max(1, client.Right - client.Left), clientHeight = Math.Max(1, client.Bottom - client.Top);
        int offsetX = clientOrigin.X - window.Left, offsetY = clientOrigin.Y - window.Top;

        // Chrome Remote Desktop centers the complete remote desktop inside its client area. Most
        // configured game desktops are 16:9, so selecting that centered viewport removes Chrome's
        // gray letterbox bands without cutting any part of the remote screen itself.
        const double remoteAspect = 16.0 / 9.0;
        int width = clientWidth, height = clientHeight;
        if (clientWidth / (double)clientHeight > remoteAspect)
            width = Math.Max(1, (int)Math.Round(clientHeight * remoteAspect));
        else
            height = Math.Max(1, (int)Math.Round(clientWidth / remoteAspect));
        return new Rectangle(offsetX + (clientWidth - width) / 2, offsetY + (clientHeight - height) / 2, width, height);
    }

    Rectangle DisplayedContentArea(Tile t)
    {
        return ContentArea(t.Bounds);
    }

    // The top-level Chrome surface stays outside the visible desktop while DWM
    // mirrors it into the tile. Mouse input uses ChromePointer's page coordinates;
    // keyboard input continues through the existing window-message forwarding.
    void ActivateSource()
    {
        var source = tiles[selected].Source;
        if (!Native.IsWindow(source)) return;
        expandedSource = 0;
        focusMode = true;
        Activate(); Native.SetForegroundWindow(Handle);
        UpdateTiles();
    }

    void ActivateOriginalCursor()
    {
        ReleaseForwardedKeys();
        if (selected < 0 || selected >= tiles.Count) return;
        var source = tiles[selected].Source;
        if (!Native.IsWindow(source)) return;
        // Reveal this session for normal OS input without moving the pointer.
        expandedSource = source;
        lastForwardedMove = null;
        inputDragTile = null;
        heldButtons = MouseButtons.None;
        refresh.Stop();
        Native.RevealSource(source, Screen.FromControl(this).WorkingArea);
        Native.ShowWindow(source, 9);
        Native.SetForegroundWindow(source);
    }

    void ReturnToMosaic()
    {
        var previousSource = expandedSource;
        expandedSource = 0;
        if (Native.IsWindow(previousSource)) KeepSourceBehindMosaic(previousSource);
        lastForwardedMove = null;
        refresh.Start();
        focusMode = false;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate(); Native.SetForegroundWindow(Handle);
        UpdateTiles();
    }

    void ToggleFocusMode()
    {
        if (expandedSource != 0) return; // Maximize already gives full focus; nothing to toggle there.
        focusMode = !focusMode;
        UpdateTiles();
    }

    // Translates a point in the mosaic Form's own client coordinates (matching Tile.Bounds/MouseEventArgs.Location)
    // into the real source window's client coordinates, using the same aspect-preserving crop math as the
    // DWM thumbnail's own Source/Destination rectangles, so a forwarded click lands on the same on-screen
    // spot the user is actually looking at. When dragging, the point is clamped into the tile instead of
    // rejected outside it, so a drag that wanders past the tile's edge keeps feeding the remote session.
    static bool TryMapToSourceClient(Tile t, Rectangle area, System.Drawing.Point formPoint, out System.Drawing.Point clientPoint, bool clamp = false, Rectangle? sourceView = null)
    {
        clientPoint = default;
        if (t.Thumbnail == 0 || !Native.IsWindow(t.Source)) return false;
        var point = formPoint;
        if (clamp) point = new System.Drawing.Point(Math.Clamp(point.X, area.Left, area.Right - 1), Math.Clamp(point.Y, area.Top, area.Bottom - 1));
        else if (!area.Contains(point)) return false;
        if (Native.DwmQueryThumbnailSourceSize(t.Thumbnail, out var size) != 0 || size.X <= 0 || size.Y <= 0) return false;
        var crop = sourceView ?? ComputeCropRect(size, area);
        double relativeX = (point.X - area.X) / (double)area.Width;
        double relativeY = (point.Y - area.Y) / (double)area.Height;
        double sourceX = crop.X + relativeX * crop.Width;
        double sourceY = crop.Y + relativeY * crop.Height;

        // DWM's source coordinates are relative to the whole window (ClientOnly=false), not just its
        // client area, so translate through the window/client offset to get real client coordinates.
        if (!Native.GetWindowRect(t.Source, out var windowRect)) return false;
        var clientOrigin = new Native.Point();
        Native.ClientToScreen(t.Source, ref clientOrigin);
        int offsetX = clientOrigin.X - windowRect.Left, offsetY = clientOrigin.Y - windowRect.Top;
        clientPoint = new System.Drawing.Point((int)Math.Round(sourceX) - offsetX, (int)Math.Round(sourceY) - offsetY);
        return true;
    }

    void ForwardMouseButton(Tile t, MouseButtons button, bool down, System.Drawing.Point location, bool dragging)
    {
        if (expandedSource != 0) return;
        var area = DisplayedContentArea(t);
        Rectangle? sourceView = t.LastPreview is { } preview && (preview.Flags & 2) != 0
            ? Rectangle.FromLTRB(preview.Source.Left, preview.Source.Top, preview.Source.Right, preview.Source.Bottom) : null;
        if (!TryMapToSourceClient(t, area, location, out var client, clamp: dragging, sourceView: sourceView)) return;
        uint message = (button, down) switch
        {
            (MouseButtons.Right, true) => Native.WM_RBUTTONDOWN,
            (MouseButtons.Right, false) => Native.WM_RBUTTONUP,
            (MouseButtons.Middle, true) => Native.WM_MBUTTONDOWN,
            (MouseButtons.Middle, false) => Native.WM_MBUTTONUP,
            (_, true) => Native.WM_LBUTTONDOWN,
            (_, false) => Native.WM_LBUTTONUP,
        };
        nint wParam = down ? button switch { MouseButtons.Right => Native.MK_RBUTTON, MouseButtons.Middle => Native.MK_MBUTTON, _ => Native.MK_LBUTTON } : 0;
        lastForwardedMove = null;
        hoverSource = 0;
        var receiver = MapContentInput(t.Source, ref client);
        if (t.IsChrome)
        {
            SendChromePointer(t, client, down ? "mousePressed" : "mouseReleased", heldButtons, button);
            return;
        }
        Native.PostMessage(receiver, message, wParam, Native.MakeLParam(client.X, client.Y));
    }

    void ForwardMouseMove(Tile t, System.Drawing.Point location, MouseButtons held, bool dragging)
    {
        if (expandedSource != 0) return;
        var area = DisplayedContentArea(t);
        Rectangle? sourceView = t.LastPreview is { } preview && (preview.Flags & 2) != 0
            ? Rectangle.FromLTRB(preview.Source.Left, preview.Source.Top, preview.Source.Right, preview.Source.Bottom) : null;
        if (!TryMapToSourceClient(t, area, location, out var client, clamp: dragging, sourceView: sourceView)) return;
        nint wParam = 0;
        if ((held & MouseButtons.Left) != 0) wParam |= Native.MK_LBUTTON;
        if ((held & MouseButtons.Right) != 0) wParam |= Native.MK_RBUTTON;
        if ((held & MouseButtons.Middle) != 0) wParam |= Native.MK_MBUTTON;
        if (!dragging && held == MouseButtons.None && hoverSource == t.Source &&
            Math.Abs(location.X - hoverAnchor.X) <= HoverJitterPixels && Math.Abs(location.Y - hoverAnchor.Y) <= HoverJitterPixels)
            return;
        var receiver = MapContentInput(t.Source, ref client);
        var move = (receiver, client, wParam);
        // Windows can generate WM_MOUSEMOVE after repaint/layout without pointer movement.
        // Do not restart the remote hover timer for the same pixel and button state.
        if (lastForwardedMove == move) return;
        if (t.IsChrome ? SendChromePointer(t, client, "mouseMoved", held) :
            Native.PostMessage(receiver, Native.WM_MOUSEMOVE, wParam, Native.MakeLParam(client.X, client.Y)))
        {
            lastForwardedMove = move;
            hoverSource = !dragging && held == MouseButtons.None ? t.Source : 0;
            hoverAnchor = location;
        }
    }

    bool SendChromePointer(Tile tile, Point client, string type, MouseButtons held,
        MouseButtons button = MouseButtons.None, int delta = 0)
    {
        int buttons = ((held & MouseButtons.Left) != 0 ? 1 : 0) |
            ((held & MouseButtons.Right) != 0 ? 2 : 0) | ((held & MouseButtons.Middle) != 0 ? 4 : 0);
        int modifiers = KeyboardModifiers;
        string name = button switch { MouseButtons.Left => "left", MouseButtons.Right => "right", MouseButtons.Middle => "middle", _ => "none" };
        tile.Pointer?.SetWindowDpi(Native.GetDpiForWindow(tile.Source));
        return tile.Pointer?.Enqueue(new(type, client.X, client.Y, name, buttons, modifiers, delta)) == true;
    }

    static nint MapContentInput(nint source, ref Point client)
    {
        var receiver = Native.ContentInputWindow(source);
        if (receiver == source) return source;
        var translated = new Native.Point { X = client.X, Y = client.Y };
        if (!Native.ClientToScreen(source, ref translated) || !Native.ScreenToClient(receiver, ref translated)) return source;
        client = new Point(translated.X, translated.Y);
        return receiver;
    }

    public bool PreFilterMessage(ref Message message)
    {
        // Wheel messages can target a focused toolbar child. Route by the pointer's tile instead.
        if (message.Msg != Native.WM_MOUSEWHEEL || !ContainsFocus) return false;
        long packed = (long)message.LParam;
        var location = PointToClient(new Point(unchecked((short)packed), unchecked((short)(packed >> 16))));
        int index = tiles.FindIndex(t => !t.Bounds.IsEmpty && DisplayedContentArea(t).Contains(location));
        if (index < 0) return false;
        ForwardMouseWheel(tiles[index], location, unchecked((short)((long)message.WParam >> 16)));
        return true;
    }

    void ForwardMouseWheel(Tile t, System.Drawing.Point location, int delta)
    {
        if (expandedSource != 0) return;
        var area = DisplayedContentArea(t);
        Rectangle? sourceView = t.LastPreview is { } preview && (preview.Flags & 2) != 0
            ? Rectangle.FromLTRB(preview.Source.Left, preview.Source.Top, preview.Source.Right, preview.Source.Bottom) : null;
        if (!TryMapToSourceClient(t, area, location, out var client, sourceView: sourceView)) return;
        if (t.IsChrome)
        {
            MapContentInput(t.Source, ref client);
            hoverSource = 0; lastForwardedMove = null;
            SendChromePointer(t, client, "mouseWheel", heldButtons, delta: delta);
            return;
        }
        var screen = new Native.Point { X = client.X, Y = client.Y };
        if (!Native.ClientToScreen(t.Source, ref screen)) return;
        uint flags = 0;
        if ((heldButtons & MouseButtons.Left) != 0) flags |= 1;
        if ((heldButtons & MouseButtons.Right) != 0) flags |= 2;
        if ((heldButtons & MouseButtons.Middle) != 0) flags |= 16;
        if ((Native.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0) flags |= 4;
        if ((Native.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0) flags |= 8;
        nint wParam = unchecked((nint)(flags | ((uint)(ushort)(short)delta << 16)));
        // Chromium's renderer child receives browser content input; posting only to its
        // top-level frame can discard wheel events. Wheel coordinates remain screen-based.
        hoverSource = 0;
        lastForwardedMove = null;
        var receiver = Native.ContentInputWindow(t.Source);
        var hover = screen;
        if (Native.ScreenToClient(receiver, ref hover))
            Native.PostMessage(receiver, Native.WM_MOUSEMOVE, (nint)flags, Native.MakeLParam(hover.X, hover.Y));
        Native.PostMessage(receiver, Native.WM_MOUSEWHEEL, wParam, Native.MakeLParam(screen.X, screen.Y));
    }

    // Legacy Win32 forwarding is only for attached non-Chrome windows. Managed
    // Chrome sessions receive ordered page input through MosaicKeyboard instead.
    static char LetterCharacter(int vk, bool shift, bool capsLock)
    {
        char lower = (char)('a' + (vk - (int)Keys.A));
        return shift ^ capsLock ? char.ToUpperInvariant(lower) : lower;
    }

    static bool IsExtendedKey(int vk) => vk is
        (int)Keys.Left or (int)Keys.Up or (int)Keys.Right or (int)Keys.Down or
        (int)Keys.Insert or (int)Keys.Delete or (int)Keys.Home or (int)Keys.End or
        (int)Keys.PageUp or (int)Keys.PageDown or (int)Keys.NumLock or (int)Keys.Divide;

    static void ForwardKeyDown(nint source, int vk, int scan, bool shift, bool capsLock, bool ctrl, bool alt, bool systemKey = false)
    {
        Native.PostMessage(source, systemKey ? Native.WM_SYSKEYDOWN : Native.WM_KEYDOWN, vk, Native.MakeKeyLParam(scan, up: false, extended: IsExtendedKey(vk)));
        if (vk >= (int)Keys.A && vk <= (int)Keys.Z && !ctrl && !alt)
        {
            Native.PostMessage(source, Native.WM_CHAR, LetterCharacter(vk, shift, capsLock), Native.MakeKeyLParam(scan, up: false));
            return;
        }
        var state = new byte[256];
        Native.GetKeyboardState(state);
        state[(int)Keys.ShiftKey] = shift ? (byte)0x80 : (byte)0;
        state[(int)Keys.Capital] = capsLock ? (byte)1 : (byte)0;
        var buffer = new StringBuilder(4);
        int result = Native.ToUnicode((uint)vk, (uint)scan, state, buffer, buffer.Capacity, 0);
        if (result > 0)
            foreach (char c in buffer.ToString(0, result))
                Native.PostMessage(source, Native.WM_CHAR, c, Native.MakeKeyLParam(scan, up: false));
    }
    static void ForwardKeyUp(nint source, int vk, int scan, bool systemKey = false) => Native.PostMessage(source, systemKey ? Native.WM_SYSKEYUP : Native.WM_KEYUP, vk, Native.MakeKeyLParam(scan, up: true, extended: IsExtendedKey(vk)));

    static bool HotkeyMatches(string configured, int vk, bool ctrl, bool alt, bool shift, bool win = false)
    {
        var parts = configured.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1 || !Enum.TryParse<Keys>(parts[^1], true, out var key)) return false;
        bool wantsCtrl = parts.Any(p => p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase));
        bool wantsAlt = parts.Any(p => p.Equals("Alt", StringComparison.OrdinalIgnoreCase));
        bool wantsShift = parts.Any(p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase));
        bool wantsWin = parts.Any(p => p.Equals("Win", StringComparison.OrdinalIgnoreCase));
        return vk == (int)(key & Keys.KeyCode) && ctrl == wantsCtrl && alt == wantsAlt && shift == wantsShift && win == wantsWin;
    }

    static string FormatHotkey(Keys key, bool ctrl, bool alt, bool shift, bool win = false)
    {
        var parts = new List<string>();
        if (win) parts.Add("Win");
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    sealed class HotkeyTextBox : TextBox
    {
        protected override bool IsInputKey(Keys keyData) => false;
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key is not (Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
                Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or Keys.Menu or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin))
            {
                bool win = (Native.GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 ||
                    (Native.GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0;
                Text = FormatHotkey(key, (keyData & Keys.Control) != 0, (keyData & Keys.Alt) != 0, (keyData & Keys.Shift) != 0, win);
            }
            return true;
        }
    }

    void ConfigureHotkeys()
    {
        using var form = new Form { Text = "Mosaic hotkeys", ClientSize = new Size(480, 190), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var help = new Label { Text = "Click a box, then press a key or combination. Modifiers are optional. Click Save to apply.", Location = new Point(12, 12), Size = new Size(456, 34) };
        var expandLabel = new Label { Text = "Expand selected in app", Location = new Point(12, 58), Size = new Size(175, 24) };
        var returnLabel = new Label { Text = "Return to mosaic", Location = new Point(12, 94), Size = new Size(175, 24) };
        var expandBox = new HotkeyTextBox { Text = expandHotkey, ReadOnly = true, Location = new Point(192, 55), Size = new Size(276, 26) };
        var returnBox = new HotkeyTextBox { Text = mosaicHotkey, ReadOnly = true, Location = new Point(192, 91), Size = new Size(276, 26) };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Location = new Point(312, 145), Size = new Size(75, 30) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(393, 145), Size = new Size(75, 30) };
        form.Controls.AddRange([help, expandLabel, returnLabel, expandBox, returnBox, save, cancel]);
        form.AcceptButton = save; form.CancelButton = cancel;
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (expandBox.Text.Equals(returnBox.Text, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Choose two different hotkeys.", "Mosaic hotkeys"); return; }
        expandHotkey = expandBox.Text; mosaicHotkey = returnBox.Text;
        instructions.Text = $"Content: click/type/drag/scroll = remote input | Ctrl+1-9 select tile | Expand: {expandHotkey} | Return: {mosaicHotkey}";
        SaveSettings();
    }

    nint KeyboardCallback(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0 && !demo && !IsDisposed && !closing.IsCancellationRequested &&
                (message == 0x100 || message == 0x104 || message == 0x101 || message == 0x105))
            {
                int vk = Marshal.ReadInt32(data), scan = Marshal.ReadInt32(data, 4);
                bool extended = (Marshal.ReadInt32(data, 8) & 1) != 0;
                var foreground = Native.GetForegroundWindow();
                if (ProcessKeyboard(vk, scan, extended, message == 0x100 || message == 0x104,
                    expandedSource == 0 && foreground == Handle,
                    expandedSource != 0 && foreground == expandedSource)) return 1;
            }
        }
        catch { /* Never let an exception escape the native keyboard hook. */ }
        return Native.CallNextHookEx(keyboardHook, code, message, data);
    }

    void ChooseWindow()
    {
        using var picker = new Form { Text = "Choose a Chrome remote desktop window", Size = new Size(650, 350), StartPosition = FormStartPosition.CenterParent };
        var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Title" };
        foreach (var window in Native.ChromeWindows()) list.Items.Add(window);
        var use = new Button { Dock = DockStyle.Bottom, Height = 36, Text = "Use selected window" };
        use.Click += (_, _) => { if (list.SelectedItem is Native.WindowInfo window) { Attach(tiles[selected], window.Handle); picker.Close(); } };
        picker.Controls.Add(list); picker.Controls.Add(use); picker.ShowDialog(this);
    }

    // Two reentrancy traps here, both now avoided: (1) opening a modal dialog (RenameTile/RemoveTile
    // both do) directly from a ContextMenuStrip item's Click handler races the strip's own auto-close
    // against the dialog's nested message loop, so the actual action is deferred via BeginInvoke to run
    // only after the strip has finished closing; (2) disposing the strip from its own Closed event races
    // WinForms' own internal close continuation, which keeps touching the strip after Closed returns -
    // so it is simply never disposed manually. It is small, short-lived, and never added to any
    // Controls collection, so the garbage collector reclaims it like any other unparented object.
    void ShowTileMenu(int index, System.Drawing.Point screenLocation)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Rename tile...", null, (_, _) => BeginInvoke(new Action(() => RenameTile(index))));
        menu.Items.Add("Remove this computer", null, (_, _) => BeginInvoke(new Action(() => RemoveTile(index))));
        menu.Show(this, screenLocation);
    }

    void RenameTile(int index)
    {
        if (index < 0 || index >= tiles.Count) return;
        if (!PromptForText(this, "Rename tile", "Nickname shown on this tile (blank to use the window title):", tiles[index].Nickname, out var value)) return;
        tiles[index].Nickname = value.Trim();
        SaveSettings();
        Invalidate();
    }

    void RemoveTile(int index)
    {
        if (index < 0 || index >= tiles.Count) return;
        if (MessageBox.Show(this, "Remove this computer from the mosaic? Its Chrome window, if open, is left running.",
            "Remote Desktop Mosaic", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Unregister(tiles[index]);
        tiles.RemoveAt(index);
        if (tiles.Count == 0) { Close(); return; }
        if (selected >= tiles.Count) selected = tiles.Count - 1;
        if (inputDragTile == index) inputDragTile = null;
        SaveSettings();
        UpdateTiles();
    }

    async Task AddComputerPrompt()
    {
        if (!PromptForText(this, "Add computer", "Paste the computer's Chrome Remote Desktop session link (copy it from the address bar after selecting a computer):", "", out var link)) return;
        link = link.Trim();
        if (!Program.IsValidLink(link))
        {
            MessageBox.Show(this, "Not a computer session link. Select a computer in Chrome Remote Desktop, then copy its address-bar link.", "Add computer");
            return;
        }
        if (tiles.Any(t => t.Link == link)) { MessageBox.Show(this, "That computer is already in the mosaic.", "Add computer"); return; }
        PromptForText(this, "Add computer", "Optional nickname for this tile:", "", out var nickname);
        tiles.Add(new Tile(link) { Nickname = nickname.Trim() });
        SaveSettings();
        UpdateTiles();
        Invalidate();
        await OpenTile(tiles.Count - 1);
    }

    internal static bool PromptForText(IWin32Window? owner, string title, string label, string initialValue, out string value)
    {
        using var form = new Form { Text = title, ClientSize = new Size(460, 130), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var promptLabel = new Label { Text = label, AutoSize = false, Location = new System.Drawing.Point(12, 12), Size = new Size(436, 40) };
        var textBox = new TextBox { Location = new System.Drawing.Point(12, 55), Size = new Size(436, 24), Text = initialValue };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(292, 90), Size = new Size(75, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(373, 90), Size = new Size(75, 28) };
        form.Controls.Add(promptLabel); form.Controls.Add(textBox); form.Controls.Add(ok); form.Controls.Add(cancel);
        form.AcceptButton = ok; form.CancelButton = cancel;
        bool result = form.ShowDialog(owner) == DialogResult.OK;
        value = textBox.Text;
        return result;
    }

    void UpdateTiles()
    {
        if (!IsHandleCreated || IsDisposed || expandedSource != 0) return;
        bool changed = false;
        int cols = (int)Math.Ceiling(Math.Sqrt(tiles.Count)), rows = (int)Math.Ceiling(tiles.Count / (double)cols);
        int workspaceX = SidebarWidth + WorkspaceMargin;
        int workspaceY = WorkspaceMargin;
        int workspaceWidth = Math.Max(1, ClientSize.Width - SidebarWidth - WorkspaceMargin * 2);
        int workspaceHeight = Math.Max(1, ClientSize.Height - InstructionBarHeight - WorkspaceMargin * 2);
        int width = Math.Max(1, workspaceWidth / cols), height = Math.Max(1, workspaceHeight / rows);
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var oldBounds = t.Bounds;
            var oldStatus = t.Status;
            bool hiddenByFocus = focusMode && i != selected;
            t.Bounds = hiddenByFocus ? Rectangle.Empty
                : focusMode ? new Rectangle(workspaceX, workspaceY, workspaceWidth, workspaceHeight)
                : new Rectangle(workspaceX + i % cols * width, workspaceY + i / cols * height, width - 4, height - 4);
            changed |= oldBounds != t.Bounds;
            if (t.Thumbnail == 0) continue;
            if (!Native.IsWindow(t.Source)) { Unregister(t); t.Status = "Session window closed. Select tile and Reconnect."; changed = true; continue; }
            if (!demo) ReconnectInputIfNeeded(t);
            // Chrome can change its shell styles while reconnecting. Reapply
            // hiding only when needed, without touching the live input stream.
            if (Native.IsIconic(t.Source) || !Native.IsSourceHidden(t.Source)) KeepSourceBehindMosaic(t.Source);
            if (Native.GetForegroundWindow() == t.Source) { Activate(); Native.SetForegroundWindow(Handle); }
            t.Status = t.InputStatus;
            changed |= oldStatus != t.Status;
            if (hiddenByFocus)
            {
                // Keep the thumbnail registered (still tracked/staleness-checked) but invisible while focus mode hides it.
                var hiddenProps = new Native.ThumbnailProperties { Flags = 8 /* DWM_TNP_VISIBLE */, Visible = false };
                changed |= ApplyPreview(t, hiddenProps);
                continue;
            }
            // Fill the whole content area without distorting the image: rather than letterboxing
            // (blank bars) or stretching (warped aspect ratio), crop a same-aspect-ratio slice out of
            // the source window - via the DWM thumbnail's own Source rectangle - and display that
            // slice at the tile's full size. Any remaining blank margin at that point is the remote
            // session's own on-screen padding (e.g. Chrome Remote Desktop centering a smaller remote
            // resolution within its browser window), not ours.
            var area = ContentArea(t.Bounds);
            var sourceRect = new Native.Rect(new Rectangle(0, 0, 1, 1));
            uint flags = 1 | 4 | 8 | 16;
            if (Native.DwmQueryThumbnailSourceSize(t.Thumbnail, out var size) == 0 && size.X > 0 && size.Y > 0)
            {
                if (focusMode && i == selected)
                {
                    var view = ExpandedRemoteSourceRect(t.Source, size);
                    sourceRect = new Native.Rect(view);
                }
                else sourceRect = new Native.Rect(ComputeCropRect(size, area));
                flags |= 2; // DWM_TNP_RECTSOURCE
            }
            var props = new Native.ThumbnailProperties { Flags = flags, Destination = new Native.Rect(area), Source = sourceRect, Opacity = 255, Visible = !Native.IsIconic(t.Source), ClientOnly = false };
            bool previewChanged = ApplyPreview(t, props);
            changed |= previewChanged;
            if (t.Source == hoverSource && (oldBounds != t.Bounds || previewChanged)) { hoverSource = 0; lastForwardedMove = null; }
        }
        if (expandedSource != 0 && !Native.IsWindow(expandedSource)) expandedSource = 0;
        if (changed) Invalidate();
    }

    static bool ApplyPreview(Tile tile, Native.ThumbnailProperties props)
    {
        if (tile.LastPreview is { } previous && previous.Equals(props)) return false;
        int hr = Native.DwmUpdateThumbnailProperties(tile.Thumbnail, ref props);
        if (hr == 0) tile.LastPreview = props;
        else tile.Status = $"Preview update failed: 0x{hr:X8}";
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.Bounds.Width <= 0 || t.Bounds.Height <= 0) continue;
            using var pen = new Pen(i == selected ? Color.DeepSkyBlue : Color.FromArgb(60, 75, 95), i == selected ? 2 : 1);
            e.Graphics.DrawRectangle(pen, t.Bounds);
            if (reorderDragTile.HasValue && dropTargetTile == i && dropTargetTile != reorderDragTile)
            {
                using var highlight = new Pen(Color.Gold, 3);
                e.Graphics.DrawRectangle(highlight, t.Bounds);
            }
            string title = $"{i + 1}  " + (t.Nickname.Length > 0 ? t.Nickname : (t.Source == 0 ? "Remote desktop" : Native.Title(t.Source)));
            TextRenderer.DrawText(e.Graphics, title, Font, new Rectangle(t.Bounds.X + 6, t.Bounds.Y + 2, Math.Max(1, t.Bounds.Width - 12 - TileButtonWidth * 2), 16), Color.White, TextFormatFlags.EndEllipsis);
            foreach (var button in new[] { TileMinimizeButton(t.Bounds), TileMaximizeButton(t.Bounds) })
            {
                using var fill = new SolidBrush(Color.FromArgb(40, 58, 80));
                e.Graphics.FillRectangle(fill, button);
            }
            var minimize = TileMinimizeButton(t.Bounds);
            var maximize = TileMaximizeButton(t.Bounds);
            using var glyphPen = new Pen(Color.White);
            e.Graphics.DrawLine(glyphPen, minimize.Left + 9, minimize.Top + 12, minimize.Right - 9, minimize.Top + 12);
            e.Graphics.DrawRectangle(glyphPen, maximize.Left + 9, maximize.Top + 4, 9, 9);
            if (t.Status.Length > 0) TextRenderer.DrawText(e.Graphics, t.Status, Font, new Rectangle(t.Bounds.X + 10, t.Bounds.Y + 26, Math.Max(1, t.Bounds.Width - 20), Math.Max(1, t.Bounds.Height - 36)), Color.LightSteelBlue, TextFormatFlags.WordBreak);
        }
    }

    sealed class WheelTestWindow : Form
    {
        public readonly List<(int Delta, Point Position)> Wheels = [];
        public readonly List<nint> Moves = [];
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEMOVE) Moves.Add(m.LParam);
            if (m.Msg == Native.WM_MOUSEWHEEL)
            {
                Wheels.Add((unchecked((short)((long)m.WParam >> 16)),
                    new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)))));
                return;
            }
            base.WndProc(ref m);
        }
    }

    async Task SelfTest()
    {
        try
        {
            foreach (Color color in new[] { Color.DarkSlateBlue, Color.DarkGreen, Color.Maroon, Color.DarkGoldenrod })
            {
                var f = new WheelTestWindow { Text = $"Demo desktop {demoSources.Count + 1}", Size = new Size(800, 500), BackColor = color, ShowInTaskbar = false };
                f.Controls.Add(new Label { Text = f.Text + " — LIVE PREVIEW", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 28), TextAlign = ContentAlignment.MiddleCenter });
                f.Show(); demoSources.Add(f); Attach(tiles[demoSources.Count - 1], f.Handle);
            }
            Activate(); UpdateTiles();
            await Task.Delay(1000);
            if (tiles.Any(t => t.Thumbnail == 0 || t.Status.Length > 0)) throw new Exception("DWM registration/update failed.");
            if (tiles.Any(t => !Native.IsSourceHidden(t.Source))) throw new Exception("Attached sources were visible outside the mosaic.");
            var hoverTile = tiles[0];
            var hoverArea = DisplayedContentArea(hoverTile);
            var hoverPoint = new Point(hoverArea.Left + hoverArea.Width / 2, hoverArea.Top + hoverArea.Height / 2);
            var hoverWindow = (WheelTestWindow)demoSources[0];
            lastForwardedMove = null;
            hoverWindow.Moves.Clear();
            for (int repeat = 0; repeat < 10; repeat++)
            {
                UpdateTiles();
                ForwardMouseMove(hoverTile, hoverPoint, MouseButtons.None, false);
            }
            await Task.Delay(100);
            if (hoverWindow.Moves.Count != 1) throw new Exception("Stationary hover sent repeated movement.");
            ForwardMouseMove(hoverTile, new Point(hoverPoint.X + 8, hoverPoint.Y), MouseButtons.None, false);
            await Task.Delay(100);
            if (hoverWindow.Moves.Count != 2) throw new Exception("Real pointer movement was suppressed.");
            // Small local jitter must not keep resetting remote tooltip dwell time.
            hoverSource = 0;
            lastForwardedMove = null;
            hoverWindow.Moves.Clear();
            ForwardMouseMove(hoverTile, hoverPoint, MouseButtons.None, false);
            for (int tick = 0; tick < 35; tick++)
            {
                await Task.Delay(200);
                ForwardMouseMove(hoverTile, new Point(hoverPoint.X + (tick % 3) - 1, hoverPoint.Y + 1), MouseButtons.None, false);
            }
            if (hoverWindow.Moves.Count != 1) throw new Exception("Hover jitter interrupted the seven-second dwell.");
            ForwardMouseMove(hoverTile, new Point(hoverPoint.X + 12, hoverPoint.Y), MouseButtons.None, false);
            await Task.Delay(50);
            if (hoverWindow.Moves.Count != 2) throw new Exception("Deliberate movement did not release hover.");
            ForwardMouseMove(hoverTile, new Point(hoverPoint.X + 13, hoverPoint.Y), MouseButtons.Left, true);
            await Task.Delay(50);
            if (hoverWindow.Moves.Count != 3) throw new Exception("Hover stabilization blocked dragging.");
            OwnedSessionWindows.SelfTest();
            if (keyboardHook == 0) throw new Exception("Escape keyboard hook was not installed.");
            for (int i = 0; i < tiles.Count; i++)
            {
                var maximizeButton = TileMaximizeButton(tiles[i].Bounds);
                OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, maximizeButton.Left + 12, maximizeButton.Top + 8, 0));
                if (selected != i || reorderCandidate.HasValue || inputDragTile.HasValue) throw new Exception("Tile maximize button did not select its desktop cleanly.");
                if (!focusMode || tiles.Where((_, index) => index != i).Any(t => !t.Bounds.IsEmpty)) throw new Exception("Selected desktop did not expand inside the app.");
                if (tiles[i].Bounds.Left < SidebarWidth || tiles[i].Bounds.Bottom > instructions.Top)
                    throw new Exception("Expanded desktop overlaps the left toolbar or bottom instruction bar.");
                if (Native.IsZoomed(tiles[i].Source)) throw new Exception("Expanding in app must not maximize the Chrome source window.");
                var minimizeButton = TileMinimizeButton(tiles[i].Bounds);
                OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, minimizeButton.Left + 12, minimizeButton.Top + 8, 0));
                if (focusMode || tiles.Any(t => t.Bounds.IsEmpty)) throw new Exception("Return hotkey did not restore the mosaic layout.");
            }
            selected = 0;
            foreach (bool expanded in new[] { false, true })
            {
                if (expanded) ActivateSource(); else ReturnToMosaic();
                var tile = tiles[0];
                var area = DisplayedContentArea(tile);
                var center = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
                Native.GetWindowRect(tile.Source, out var sourceBounds);
                var probe = (WheelTestWindow)demoSources[0];
                probe.Wheels.Clear();
                foreach (int delta in new[] { 120, -120, 30, -30 }) ForwardMouseWheel(tile, center, delta);
                ForwardMouseWheel(tile, new Point(tile.Bounds.Left + 4, tile.Bounds.Top + 4), 120);
                await Task.Delay(100);
                if (!probe.Wheels.Select(w => w.Delta).SequenceEqual(new[] { 120, -120, 30, -30 }))
                    throw new Exception("Wheel direction/delta forwarding or title exclusion failed.");
                if (probe.Wheels.Any(w => w.Position.X < sourceBounds.Left || w.Position.X >= sourceBounds.Right ||
                    w.Position.Y < sourceBounds.Top || w.Position.Y >= sourceBounds.Bottom))
                    throw new Exception("Wheel coordinates did not land inside the source window.");
            }
            ReturnToMosaic();
            foreach (var key in new[] { Keys.A, Keys.F8, Keys.Enter, Keys.Escape, Keys.Tab, Keys.Space, Keys.Oemplus })
                for (int modifiers = 0; modifiers < 16; modifiers++)
                {
                    bool ctrl = (modifiers & 1) != 0, alt = (modifiers & 2) != 0, shift = (modifiers & 4) != 0, win = (modifiers & 8) != 0;
                    string configured = FormatHotkey(key, ctrl, alt, shift, win);
                    if (!HotkeyMatches(configured, (int)key, ctrl, alt, shift, win) ||
                        HotkeyMatches(configured, (int)key, !ctrl, alt, shift, win))
                        throw new Exception("Optional-modifier hotkey matching failed: " + configured);
                }
            if (!HotkeyMatches("Ctrl+Shift+Enter", (int)Keys.Enter, true, false, true)) throw new Exception("Combination hotkey matching failed.");
            if (HotkeyMatches("Ctrl+Shift+Enter", (int)Keys.Enter, true, false, false)) throw new Exception("Hotkey accepted missing modifiers.");
            if (HotkeyMatches("Ctrl+Shift+Enter", (int)Keys.Escape, true, false, true)) throw new Exception("Escape unexpectedly matched expand hotkey.");
            if (LetterCharacter((int)Keys.A, false, false) != 'a') throw new Exception("Plain lowercase forwarding failed.");
            if (LetterCharacter((int)Keys.A, true, false) != 'A') throw new Exception("Shift capitalization forwarding failed.");
            if (LetterCharacter((int)Keys.A, false, true) != 'A') throw new Exception("Caps Lock capitalization forwarding failed.");
            if (LetterCharacter((int)Keys.A, true, true) != 'a') throw new Exception("Shift plus Caps Lock inversion failed.");
            long arrowKeyMessage = (long)Native.MakeKeyLParam(0x4B, up: false, extended: true);
            if ((arrowKeyMessage & (1L << 24)) == 0) throw new Exception("Arrow key forwarding is missing the extended-key flag.");
            long letterKeyMessage = (long)Native.MakeKeyLParam(0x1E, up: false);
            if ((letterKeyMessage & (1L << 24)) != 0) throw new Exception("Regular key forwarding unexpectedly has the extended-key flag.");
            WindowState = FormWindowState.Maximized;
            await Task.Delay(100);
            selected = 0; ActivateSource(); ReturnToMosaic();
            await Task.Delay(100);
            if (WindowState != FormWindowState.Maximized) throw new Exception("Returning a selected desktop to the mosaic changed the main app window state.");
            WindowState = FormWindowState.Normal;
            var fitted = ComputeFitRect(new Native.Point { X = 1920, Y = 1080 }, new Rectangle(0, 0, 1000, 700));
            if (fitted.Width != 1000 || fitted.Height != 562 || fitted.Y != 69) throw new Exception("Expanded full-screen fit geometry is incorrect.");
            foreach (var tile in tiles) Native.ShowWindow(tile.Source, 6);
            UpdateTiles();
            if (tiles.Any(t => Native.IsIconic(t.Source))) throw new Exception("Sources remained minimized.");
            Size = new Size(1000, 700); UpdateTiles();
            if (tiles.Any(t => t.Bounds.Width < 1 || t.Bounds.Height < 1)) throw new Exception("Invalid resized tile geometry.");
            selected = 0;
            ActivateOriginalCursor();
            if (expandedSource != tiles[0].Source || refresh.Enabled) throw new Exception("Original cursor mode did not suspend synthetic updates.");
            if (Native.IsSourceHidden(tiles[0].Source)) throw new Exception("Original cursor did not reveal its source.");
            await Task.Delay(100);
            int nativeMoveCount = ((WheelTestWindow)demoSources[0]).Moves.Count;
            ForwardMouseMove(tiles[0], hoverPoint, MouseButtons.None, false);
            UpdateTiles();
            await Task.Delay(100);
            if (((WheelTestWindow)demoSources[0]).Moves.Count != nativeMoveCount) throw new Exception("Original cursor mode forwarded synthetic movement.");
            ReturnToMosaic();
            if (expandedSource != 0 || !refresh.Enabled) throw new Exception("Original cursor mode did not return cleanly.");
            if (!Native.IsSourceHidden(tiles[0].Source)) throw new Exception("Return to mosaic did not hide the source again.");
            demoSources[0].Close(); UpdateTiles();
            if (tiles[0].Thumbnail != 0) throw new Exception("Closed window did not detach.");
            if (TestChromeInput) await ChromeHoverSelfTest();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), "PASS: seven-second hover dwell under jitter, deliberate movement and drag release; original cursor mode suppresses synthetic mouse input and refresh, clean return; single-key and optional Ctrl/Alt/Shift/Win hotkeys, wheel forwarding in both directions at full and partial deltas in grid/expanded views, title wheel exclusion, per-tile maximize/minimize clicks, four previews, in-app expand/return round trips with the main app still maximized, arrow and text key forwarding, configurable combination hotkeys, Escape unchanged, automatic restore of minimized sources, layout resize, and source-window close cleanup." + (TestChromeInput ? " Real Chrome input/tooltip regression: PASS (see chrome-hover-result.txt)." : ""));
        }
        catch (Exception ex) { Environment.ExitCode = 1; File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), "FAIL: " + ex); }
        finally { Close(); }
    }
}

internal static class Native
{
    public static readonly nint HWND_BOTTOM = 1;
    public const uint SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetProp(nint hwnd, string name, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint GetProp(nint hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint RemoveProp(nint hwnd, string name);
    [DllImport("user32.dll")] public static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    public static nint FindMosaic()
    {
        nint found = 0;
        EnumWindows((hwnd, _) => { if (GetProp(hwnd, "RemoteDesktopMosaic.MainWindow") != 1) return true; found = hwnd; return false; }, 0);
        return found;
    }
    public record WindowInfo(nint Handle, string Title);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public Rect(Rectangle r) { Left = r.Left; Top = r.Top; Right = r.Right; Bottom = r.Bottom; } }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct ThumbnailProperties { public uint Flags; public Rect Destination, Source; public byte Opacity; [MarshalAs(UnmanagedType.Bool)] public bool Visible; [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly; }
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Point size);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(Point point);
    public delegate nint KeyboardProc(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetWindowsHookEx(int type, KeyboardProc callback, nint module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(nint hwnd, int index, int value);
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_APPWINDOW = 0x40000;
    public static System.Drawing.Point SourceParkingLocation()
    {
        var desktop = SystemInformation.VirtualScreen;
        return new System.Drawing.Point(desktop.Right + 64, desktop.Top);
    }
    public static bool IsSourceHidden(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect)) return false;
        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return !Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(bounds)) &&
            (GetWindowLong(hwnd, GWL_EXSTYLE) & (WS_EX_APPWINDOW | WS_EX_TOOLWINDOW)) == WS_EX_TOOLWINDOW;
    }
    public static void RevealSource(nint hwnd, Rectangle desktop)
    {
        if (!GetWindowRect(hwnd, out var rect)) return;
        int width = Math.Min(Math.Max(400, rect.Right - rect.Left), desktop.Width);
        int height = Math.Min(Math.Max(300, rect.Bottom - rect.Top), desktop.Height);
        SetWindowPos(hwnd, 0, desktop.Left + (desktop.Width - width) / 2,
            desktop.Top + (desktop.Height - height) / 2, width, height, SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    // Input-forwarding messages/flags: translate a click or keystroke aimed at a tile into the same
    // synthetic Win32 messages the real window would receive if the click/keystroke had landed on it
    // directly, so the real window itself never needs to be moved, resized, or reparented.
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern bool GetKeyboardState(byte[] keyState);
    [DllImport("user32.dll")] public static extern int ToUnicode(uint vk, uint scanCode, byte[] keyboardState, StringBuilder buffer, int bufferSize, uint flags);
    public const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_MOUSEWHEEL = 0x020A, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_CHAR = 0x0102,
        WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    public const nint MK_LBUTTON = 0x0001, MK_RBUTTON = 0x0002, MK_MBUTTON = 0x0010;
    public static nint MakeLParam(int x, int y) => unchecked((nint)(((uint)(ushort)y << 16) | (uint)(ushort)x));
    public static nint MakeKeyLParam(int scanCode, bool up, bool extended = false) =>
        unchecked((nint)(1u | ((uint)(scanCode & 0xFF) << 16) | (extended ? 1u << 24 : 0u) | (up ? (1u << 30) | (1u << 31) : 0u)));

    // Chrome opens each session as a normal top-level window, which the taskbar shows by default.
    // WS_EX_TOOLWINDOW/removing WS_EX_APPWINDOW keeps a window off the taskbar (and Alt+Tab); the
    // hide-then-show cycle is required because Windows only re-evaluates taskbar presence on a
    // visibility transition, not on a bare style change.
    public static void HideFromTaskbar(nint hwnd)
    {
        bool wasVisible = IsWindowVisible(hwnd);
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((style & (WS_EX_APPWINDOW | WS_EX_TOOLWINDOW)) == WS_EX_TOOLWINDOW) return;
        SetWindowLong(hwnd, GWL_EXSTYLE, (style & ~WS_EX_APPWINDOW) | WS_EX_TOOLWINDOW);
        ShowWindow(hwnd, 0); // SW_HIDE
        if (wasVisible) ShowWindow(hwnd, 8); // SW_SHOWNA: show without activating or changing z-order.
    }

    [DllImport("user32.dll")] public static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint parent, EnumProc callback, nint parameter);
    public static nint ContentInputWindow(nint source)
    {
        nint renderer = 0;
        EnumChildWindows(source, (child, _) =>
        {
            var name = new StringBuilder(256);
            GetClassName(child, name, name.Capacity);
            if (name.ToString() == "Chrome_RenderWidgetHostHWND" && IsWindowVisible(child))
            { renderer = child; return false; }
            return true;
        }, 0);
        return renderer != 0 ? renderer : source;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    delegate bool EnumProc(nint hwnd, nint unused);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, nint unused);
    public static string Title(nint hwnd) { var sb = new StringBuilder(512); GetWindowText(hwnd, sb, sb.Capacity); return sb.ToString(); }
    public static List<WindowInfo> ChromeWindows(bool includeHidden = false)
    {
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!includeHidden && !IsWindowVisible(hwnd)) return true;
            var name = new StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
            if (!name.ToString().StartsWith("Chrome_WidgetWin")) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            try { using var process = Process.GetProcessById((int)pid); if (process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase)) result.Add(new WindowInfo(hwnd, Title(hwnd))); } catch { }
            return true;
        }, 0);
        return result;
    }
}
