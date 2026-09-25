using System.IO.Compression;
using System.Text.Json;

namespace KathanaBotReloaded;

internal sealed partial class BotForm
{
    internal sealed record RegionSpec(string Id, string Label, Color Color, string Kind = "text");
    internal static readonly RegionSpec[] GameRegions = [
        new("hp", "Character HP", Color.Firebrick, "hp"), new("mp", "Character MP", Color.RoyalBlue, "mp"),
        new("repair", "Unreachable / repair", Color.Goldenrod), new("disconnect", "Disconnect message", Color.IndianRed),
        new("mob_name_rect", "Target name", Color.Goldenrod), new("mob_hp_rect", "Target HP bar", Color.DarkOrange, "hp"),
        new("mob_life_rect", "Target HP numbers", Color.DarkSlateGray), new("prana_exp_rect", "Prana / EXP", Color.ForestGreen),
        new("rupiahs_rect", "Rupiahs", Color.DarkGoldenrod), new("map_coordinate_x_rect", "Map X", Color.Teal),
        new("map_coordinate_y_rect", "Map Y", Color.SteelBlue), new("chat_rect", "Chat", Color.Chocolate),
        new("buff_area_rect", "Buff area", Color.Green, "image"), new("party_invite_scan_rect", "Party invite", Color.MediumPurple),
        new("party_list_rect", "Party list", Color.IndianRed), new("resurrect_scan_rect", "Resurrection dialog", Color.DeepPink),
        new("death_message_rect", "Death message", Color.Crimson), new("disconnect_ok_rect", "Disconnect OK", Color.DarkOrange),
        new("loot_scan_rect", "Loot scan", Color.SeaGreen), new("relaunch_rect", "Relaunch screen", Color.SlateBlue),
        new("character_rect", "Character / level", Color.DarkSlateBlue), new("map_name_rect", "Map name", Color.Teal)
    ];
    private readonly Dictionary<string, DetectionReading> telemetry = new();
    private readonly Dictionary<string, Label> telemetryLabels = new();
    private readonly VisionReader homeVision = new();
    private Task<DetectionReading>? homeScan;
    private int homeScanIndex, telemetryEpoch, homeScanEpoch, regionCursor = 4;
    private long nextHomeScan, nextHomeUi, homeScanStarted, sessionStarted = Environment.TickCount64;
    private long sessionInputs;
    private string lastInput = "None yet";
    private readonly Dictionary<string, long> keyCounts = new();
    private readonly Queue<long> recentInputs = new();
    private readonly Label homeSummary = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12), BackColor = Color.FromArgb(226, 237, 249) };
    private readonly PictureBox buffPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Height = 100, Dock = DockStyle.Top, BackColor = Color.FromArgb(235, 239, 245) };
    private bool homeWasFocused;

    private void BuildHomeUi()
    {
        var page = new TabPage("Home") { AutoScroll = true, Padding = new Padding(10) };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int row = 0;
        foreach (var spec in GameRegions)
        {
            var value = new Label { Text = "Not calibrated", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4), MaximumSize = new Size(800, 100), AutoEllipsis = true };
            telemetryLabels[spec.Id] = value;
            table.Controls.Add(new Label { Text = spec.Label, AutoSize = true, ForeColor = spec.Color, Padding = new Padding(4) }, 0, row);
            table.Controls.Add(value, 1, row++);
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var export = new Button { Text = "Export results...", AutoSize = true };
        export.Click += (_, _) => ExportResults(); actions.Controls.Add(export);
        var reset = new Button { Text = "Reset session counters", AutoSize = true };
        reset.Click += (_, _) => { sessionStarted = Environment.TickCount64; sessionInputs = 0; keyCounts.Clear(); recentInputs.Clear(); lastInput = "None yet"; }; actions.Controls.Add(reset);
        page.Controls.Add(table); page.Controls.Add(buffPreview); page.Controls.Add(actions); page.Controls.Add(homeSummary);
        tabs.TabPages.Insert(0, page);
        RefreshHome(Environment.TickCount64);
    }
    private void BuildRegionUi()
    {
        var page = new TabPage("Overlays") { AutoScroll = true, Padding = new Padding(10) };
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        var controlsToggle = new CheckBox { Text = "Show in-game Go / Stop panel", Checked = settings.ShowGameControls, AutoSize = true };
        controlsToggle.CheckedChanged += (_, _) => { settings.ShowGameControls = controlsToggle.Checked; SaveSettings(); };
        panel.Controls.Add(controlsToggle, 0, 0); panel.SetColumnSpan(controlsToggle, 3);
        var outlinesToggle = new CheckBox { Text = "Show calibrated outlines", Checked = settings.ShowOverlays, AutoSize = true };
        outlinesToggle.CheckedChanged += (_, _) => showOverlays.Checked = outlinesToggle.Checked;
        showOverlays.CheckedChanged += (_, _) => outlinesToggle.Checked = showOverlays.Checked;
        panel.Controls.Add(outlinesToggle, 0, 1); panel.SetColumnSpan(outlinesToggle, 3);
        var info = new Label { Text = "Set each area over its game display. Checked areas appear as colored outlines. Home reads visible text and bars; buffs show an image. These additional areas monitor the game; they do not click dialogs or automate loot/party actions.", AutoSize = true, MaximumSize = new Size(650, 0), Padding = new Padding(0, 5, 0, 8) };
        panel.Controls.Add(info, 0, 2); panel.SetColumnSpan(info, 3);
        for (int i = 0; i < GameRegions.Length; i++)
        {
            int index = i; var spec = GameRegions[i];
            var enabled = new CheckBox { Text = spec.Label, ForeColor = spec.Color, Checked = !settings.HiddenRegions.Contains(spec.Id), AutoSize = true, Dock = DockStyle.Fill };
            enabled.CheckedChanged += (_, _) => { if (enabled.Checked) settings.HiddenRegions.Remove(spec.Id); else settings.HiddenRegions.Add(spec.Id); SaveSettings(); };
            var set = new Button { Text = GetRegion(i).Configured ? "Adjust" : "Set area", Dock = DockStyle.Fill };
            set.Click += (_, _) => { Calibrate(index); set.Text = GetRegion(index).Configured ? "Adjust" : "Set area"; }; setup.Add(set);
            var clear = new Button { Text = "Clear", Dock = DockStyle.Fill };
            clear.Click += (_, _) => { SetGameRegion(index, new()); telemetry.Remove(spec.Id); telemetryEpoch++; set.Text = "Set area"; SaveSettings(); }; setup.Add(clear);
            panel.Controls.Add(enabled, 0, i + 3); panel.Controls.Add(set, 1, i + 3); panel.Controls.Add(clear, 2, i + 3);
        }
        page.Controls.Add(panel); tabs.TabPages.Add(page);
    }
    private void SetGameRegion(int index, CaptureRegion region)
    {
        if (index == 0) settings.Hp = region;
        else if (index == 1) settings.Mp = region;
        else if (index == 2) settings.Repair = region;
        else if (index == 3) settings.Disconnect = region;
        else settings.GameRegions[GameRegions[index].Id] = region;
    }
    private string HomeValue(string id, long now) => telemetry.TryGetValue(id, out var value) && now - value.At <= 15000 && value.TextValid && value.Error.Length == 0
        ? value.Text.Replace('\r', ' ').Replace('\n', ' ') : "--";
    private void RecordInput(KeySlot slot)
    {
        sessionInputs++; keyCounts[slot.Key] = keyCounts.GetValueOrDefault(slot.Key) + 1;
        lastInput = $"{slot.Key} ({slot.Role}) at {DateTime.Now:HH:mm:ss}";
        recentInputs.Enqueue(Environment.TickCount64);
    }
    private bool HudCovers(CaptureRegion region, Rectangle client)
    {
        var rect = region.Resolve(client.Size); rect.Offset(client.Location);
        return gameControls is { Visible: true } && gameControls.Bounds.IntersectsWith(rect);
    }
    private void TickHome(long now)
    {
        bool focused = TargetFocused && !calibrating;
        if (homeWasFocused != focused) { telemetryEpoch++; homeWasFocused = focused; }
        if (now >= nextHomeUi) { nextHomeUi = now + 500; RefreshHome(now); }
        if (homeScan is { IsCompleted: true })
        {
            if (homeScanEpoch == telemetryEpoch && focused && homeScan.IsCompletedSuccessfully)
                telemetry[GameRegions[homeScanIndex].Id] = homeScan.Result;
            homeScan = null;
        }
        if (!focused || heldKey != 0 || homeScan is not null || now < nextHomeScan) return;
        for (int offset = 0; offset < GameRegions.Length; offset++)
        {
            int index = (regionCursor + offset) % GameRegions.Length;
            if (index < 4 || !GetRegion(index).Configured) continue;
            var spec = GameRegions[index]; var region = GetRegion(index); var client = Native.ClientBounds(target!.Handle);
            regionCursor = (index + 1) % GameRegions.Length; nextHomeScan = now + 250;
            if (HudCovers(region, client)) { telemetry[spec.Id] = new(null, null, "", "Move game controls away from this area", now, false); return; }
            if (spec.Kind == "image")
            {
                try { var image = VisionReader.Capture(client, region); var old = buffPreview.Image; buffPreview.Image = image; old?.Dispose(); telemetry[spec.Id] = new(null, null, "Preview above (icons are not classified)", "", now, true); }
                catch (Exception ex) { telemetry[spec.Id] = new(null, null, "", ex.Message, now, false); }
                return;
            }
            homeScanIndex = index; homeScanEpoch = telemetryEpoch; homeScanStarted = now;
            homeScan = Task.Run(async () => spec.Kind == "hp"
                ? VisionReader.ReadBars(client, new ReloadedSettings { Hp = region })
                : await homeVision.ReadAsync(client, new ReloadedSettings { Repair = region }));
            return;
        }
        nextHomeScan = now + 1000;
    }
    private void RefreshHome(long now)
    {
        while (recentInputs.TryPeek(out long at) && now - at > 10000) recentInputs.Dequeue();
        double seconds = Math.Max(1, (now - sessionStarted) / 1000d);
        homeSummary.Text = $"{(running ? awaitingFocus ? "WAITING FOR GAME" : "RUNNING" : "STOPPED")}   |   {(stressRun is null ? "Skill-driven mode" : $"Stress stage {stressRun.Stage}")}\n" +
            $"Game: {target?.Title ?? "Select a game window"}\nSession: {TimeSpan.FromSeconds(seconds):hh\\:mm\\:ss}   Inputs: {sessionInputs:N0}   Recent: {recentInputs.Count / Math.Min(10, seconds):0.00}/s\nLast key: {lastInput}\n" +
            $"{slots.Count(s => s.Enabled)} enabled skills   |   Focus: {(TargetFocused ? "game active" : "game not focused")}   |   Keep active: {(settings.KeepGameActive ? "on" : "off")}";
        foreach (var pair in telemetryLabels)
        {
            int index = Array.FindIndex(GameRegions, s => s.Id == pair.Key);
            if (!GetRegion(index).Configured) { pair.Value.Text = "Not calibrated - set area in Overlays"; continue; }
            if (index == 0) { pair.Value.Text = hpLabel.Text; continue; }
            if (index == 1) { pair.Value.Text = mpLabel.Text; continue; }
            if (index == 2) { pair.Value.Text = repairLabel.Text + " | " + (reading?.Text ?? "Waiting for scan"); continue; }
            if (index == 3) { pair.Value.Text = stressRun is null ? "Monitoring during stress tests" : disconnectMatches > 0 ? "Possible disconnect - confirming" : "Stress disconnect monitor active"; continue; }
            if (!telemetry.TryGetValue(pair.Key, out var value)) { pair.Value.Text = "Waiting for visible game"; continue; }
            var age = Math.Max(0, (now - value.At) / 1000);
            string text = value.Error.Length > 0 ? value.Error : value.Hp.HasValue ? $"{value.Hp:0}%" : string.IsNullOrWhiteSpace(value.Text) ? "No text detected" : value.Text.Replace('\r', ' ').Replace('\n', ' ');
            pair.Value.Text = $"{text}  [{age}s ago{(!TargetFocused || age > 15 ? ", stale" : "")}]";
        }
        if (homeScan is { IsCompleted: false } && now - homeScanStarted > 10000)
            homeSummary.Text += "\nGame information OCR is delayed; keyboard processing remains active.";
        buffPreview.Visible = GetRegion(Array.FindIndex(GameRegions, s => s.Kind == "image")).Configured && TargetFocused;
    }
    internal static Dictionary<string, JsonElement> ShareableRecord(JsonElement record) =>
        record.EnumerateObject().Where(p => p.Name is not ("target" or "evidence")).ToDictionary(p => p.Name, p => p.Value.Clone());
    internal static void HomeSelfTest()
    {
        if (GameRegions.Select(r => r.Id).Distinct().Count() != GameRegions.Length) throw new Exception("Duplicate game regions");
        var original = new ReloadedSettings { GameRegions = new() { ["mob_name_rect"] = new CaptureRegion { X = .2, Y = .1, Width = .3, Height = .1 } }, HiddenRegions = ["chat_rect"] };
        var copy = ReloadedSettings.Parse(JsonSerializer.Serialize(original));
        if (!copy.GameRegions["mob_name_rect"].Configured || !copy.HiddenRegions.Contains("chat_rect") || !ReloadedSettings.Parse("{}").AutoCheckUpdates)
            throw new Exception("Game region settings compatibility failed");
        using var doc = JsonDocument.Parse("{\"target\":\"private name\",\"evidence\":\"private chat\",\"measuredInputsPerSecond\":5,\"kind\":\"disconnect\"}");
        var clean = ShareableRecord(doc.RootElement);
        if (clean.ContainsKey("target") || clean.ContainsKey("evidence") || clean["measuredInputsPerSecond"].GetInt32() != 5)
            throw new Exception("Shared result filtering failed");
    }
    private void ExportResults()
    {
        Stop("Stopped for result export");
        using var dialog = new SaveFileDialog { Filter = "Results archive (*.zip)|*.zip", FileName = $"KathanaReloaded-results-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Directory.CreateDirectory(dataFolder);
            using var output = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(output, ZipArchiveMode.Create);
            var entry = zip.CreateEntry("session.json");
            using (var writer = new StreamWriter(entry.Open())) writer.Write(JsonSerializer.Serialize(new {
                version = ReloadedUpdater.Current.ToString(), exportedUtc = DateTime.UtcNow, sessionInputs, lastInput, keyCounts,
                enabledSkills = slots.Where(s => s.Enabled).Select(s => new { s.Key, s.Role, s.Seconds, s.Threshold }),
                note = "Counts measure Windows key-down events, not server-accepted actions. Game text and account settings are excluded."
            }, new JsonSerializerOptions { WriteIndented = true }));
            int n = 0;
            foreach (string file in Directory.EnumerateFiles(dataFolder, "stress-*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).Take(20))
            {
                var log = zip.CreateEntry($"stress/{Path.GetFileName(file)}");
                using var writer = new StreamWriter(log.Open());
                foreach (var line in File.ReadLines(file))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var clean = ShareableRecord(doc.RootElement);
                        writer.WriteLine(JsonSerializer.Serialize(clean));
                    }
                    catch (JsonException) { }
                }
                n++;
            }
            status.Text = $"Exported session and {n} test logs";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Export failed: " + ex.Message; }
    }
}
