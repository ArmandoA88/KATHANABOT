using System.ComponentModel;
using System.Text.Json;

namespace KathanaBotReloaded;

internal sealed partial class BotForm : Form
{
    private static readonly string[] Roles = ["Attack", "Heal", "Mana", "Repair", "Target", "Loot", "Buff"];
    private readonly string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotReloaded");
    private ReloadedSettings settings = new();
    private readonly List<KeySlot> slots;
    private readonly KeySchedule schedule;
    private readonly bool previewOnly;
    private readonly TextBox targetText = new() { ReadOnly = true, Dock = DockStyle.Fill, PlaceholderText = "Select the game window" };
    private readonly Button start = new() { Text = "Start", Width = 92, Height = 30, BackColor = Color.FromArgb(30, 110, 220), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Label status = new() { Text = "Paused", AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label hotkeys = new() { Text = "Start button always available", AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label hpLabel = new() { Text = "HP  —", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Firebrick };
    private readonly Label mpLabel = new() { Text = "MP  —", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.RoyalBlue };
    private readonly Label repairLabel = new() { Text = "Repair  —", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = ActionStyle.ColorFor("Repair") };
    private readonly TextBox phrases = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox ocrText = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly DataGridView grid = new();
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Margin = new Padding(0), DrawMode = TabDrawMode.OwnerDrawFixed, Padding = new Point(15, 7) };
    private GameControlOverlay? gameControls;
    private long nextBlink;
    private bool blink;
    private long nextFocusAttempt;
    private readonly CheckBox keepActive = new() { Text = "Keep Game Active", AutoSize = true };
    private readonly CheckBox showOverlays = new() { Text = "Show calibrated areas on game", AutoSize = true };
    private readonly ToolTip tips = new();
    private readonly List<Control> setup = [];
    private readonly List<Label> regionLabels = [];
    private readonly RegionOverlay?[] overlays = new RegionOverlay?[GameRegions.Length];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 20 };
    private readonly VisionReader vision = new();
    private readonly RepairTrigger repair = new();
    private DetectionReading? reading;
    private Task<DetectionReading>? scanTask;
    private long nextScan;
    private int generation, scanGeneration;
    private Native.Target? target;
    private KeySlot? active;
    private ushort heldKey;
    private long releaseAt;
    private bool running, awaitingFocus, calibrating;

    public BotForm(bool previewOnly = false)
    {
        this.previewOnly = previewOnly;
        if (!previewOnly) LoadSettings();
        slots = ActionStyle.KeysAvailable.Select(c =>
        {
            var saved = settings.Keys?.FirstOrDefault(s => s is not null && s.Key == c.ToString());
            var slot = saved ?? new KeySlot(c.ToString()) { Role = c == "E" ? "Target" : c == "F" ? "Loot" : "Attack" };
            slot.Seconds = Math.Clamp(slot.Seconds, 0.1m, 3600m);
            slot.Threshold = Math.Clamp(slot.Threshold, 1, 99);
            if (!Roles.Contains(slot.Role)) slot.Role = "Attack";
            return slot;
        }).ToList();
        settings.Keys = slots;
        settings.Hp ??= new(); settings.Mp ??= new(); settings.Repair ??= new();
        settings.Disconnect ??= new(); settings.GameRegions ??= new(); settings.HiddenRegions ??= [];
        settings.RepairPhrases ??= "unable to reach target; needs repair";
        schedule = new KeySchedule(slots);
        Text = "KATHANA BOT RELOADED";
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(246, 248, 251);
        ForeColor = Color.FromArgb(35, 43, 57);
        ClientSize = new Size(Math.Clamp(settings.WindowWidth, 470, 1200), Math.Clamp(settings.WindowHeight, 340, 1000));
        MinimumSize = new Size(486, 379);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
        BuildUi();
        BuildStressUi();
        BuildHomeUi(); BuildRegionUi(); BuildUpdateUi();
        tabs.SelectedIndex = 0;
        if (!previewOnly) Shown += async (_, _) => { if (settings.AutoCheckUpdates) await CheckUpdate(); };
        timer.Tick += (_, _) => TickBot();
        if (!previewOnly) timer.Start();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        Controls.Add(root);
        var targetRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        var select = new Button { Text = "Select window", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 7) };
        select.Click += (_, _) => SelectWindow(); setup.Add(select);
        targetRow.Controls.Add(targetText, 0, 0); targetRow.Controls.Add(select, 1, 0); root.Controls.Add(targetRow, 0, 0);
        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.White, Margin = new Padding(0, 0, 0, 7) };
        for (int i = 0; i < 3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        metrics.Controls.Add(hpLabel, 0, 0); metrics.Controls.Add(mpLabel, 1, 0); metrics.Controls.Add(repairLabel, 2, 0); root.Controls.Add(metrics, 0, 1);
        tabs.DrawItem += (_, e) =>
        {
            bool missing = Enumerable.Range(0, 3).Any(i => !GetRegion(i).Configured);
            bool alert = tabs.TabPages[e.Index].Text == "Detection" && missing;
            using var brush = new SolidBrush(alert && blink ? Color.FromArgb(255, 202, 92) : SystemColors.Control);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, (alert ? "! " : "") + tabs.TabPages[e.Index].Text, Font, e.Bounds, alert ? Color.Maroon : ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
        var keysPage = new TabPage("Keys"); var detectPage = new TabPage("Detection") { Padding = new Padding(10), AutoScroll = true };
        tabs.TabPages.AddRange([keysPage, detectPage]); root.Controls.Add(tabs, 0, 2);
        ConfigureGrid(); keysPage.Controls.Add(grid);
        var detection = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 8 };
        detection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); detection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        string[] labels = ["HP bar", "MP bar", "Unreachable / repair"];
        for (int i = 0; i < 3; i++)
        {
            int region = i;
            var button = new Button { Text = "Set " + labels[i], Dock = DockStyle.Fill, Height = 32, AutoSize = true };
            button.Click += (_, _) => Calibrate(region); setup.Add(button);
            var label = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = GetRegion(i).Configured ? "Calibrated — click to adjust" : "Not calibrated" };
            regionLabels.Add(label); detection.Controls.Add(button, 0, i); detection.Controls.Add(label, 1, i);
        }
        showOverlays.Checked = settings.ShowOverlays;
        showOverlays.CheckedChanged += (_, _) => { settings.ShowOverlays = showOverlays.Checked; UpdateOverlays(); SaveSettings(); };
        detection.Controls.Add(showOverlays, 0, 3); detection.SetColumnSpan(showOverlays, 2);
        var info = new Label { AutoSize = true, MaximumSize = new Size(470, 0), Text = "Select the full colored bar interior, excluding labels and borders. HP uses red; MP uses blue. Unknown or stale readings never trigger Heal or Mana." };
        detection.Controls.Add(info, 0, 4); detection.SetColumnSpan(info, 2);
        detection.Controls.Add(new Label { Text = "Repair phrases (separate with ;)", AutoSize = true }, 0, 5); detection.SetColumnSpan(detection.GetControlFromPosition(0, 5)!, 2);
        phrases.Text = settings.RepairPhrases; phrases.Height = 62; phrases.MinimumSize = new Size(0, 60); setup.Add(phrases);
        detection.Controls.Add(phrases, 0, 6); detection.SetColumnSpan(phrases, 2);
        ocrText.MinimumSize = new Size(0, 70); ocrText.Text = "OCR preview appears while the selected game is in the foreground. Repair requires 5 separate sightings within 10 minutes. Each sighting needs 2 matching scans; the text must clear before another counts.";
        detection.Controls.Add(ocrText, 0, 7); detection.SetColumnSpan(ocrText, 2); detectPage.Controls.Add(detection);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 5, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 18)); bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        start.Dock = DockStyle.Fill; start.Margin = new Padding(0, 0, 7, 0); start.Click += (_, _) => Toggle();
        bottom.Controls.Add(start, 0, 0); bottom.Controls.Add(status, 1, 0); bottom.Controls.Add(hotkeys, 0, 1); bottom.SetColumnSpan(hotkeys, 2); root.Controls.Add(bottom, 0, 3);
        keepActive.Checked = settings.KeepGameActive;
        keepActive.CheckedChanged += (_, _) => { settings.KeepGameActive = keepActive.Checked; nextFocusAttempt = 0; SaveSettings(); };
        bottom.Controls.Add(keepActive, 0, 2); bottom.SetColumnSpan(keepActive, 2);
        tips.SetToolTip(keepActive, "While started, restore and refocus the selected game. Stop disables focus recovery.");
        tips.SetToolTip(start, "One key at a time. Fresh ±0.5 s timing variation. Switch away to pause; return to resume. Stop stays stopped.");
    }

    internal void PreparePreview(string mode)
    {
        if (mode == "stress") tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Stress test");
        if (mode == "overlays") tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Overlays");
        if (mode == "updates") tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Updates");
        if (mode is "colors" or "functions") tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Keys");
        if (mode == "colors")
        {
            for (int i = 0; i < Roles.Length; i++) { slots[i].Enabled = true; slots[i].Role = Roles[i]; }
            grid.Invalidate();
        }
        if (mode == "functions") Shown += (_, _) => grid.FirstDisplayedScrollingRowIndex = 13;
        if (mode == "small") Size = MinimumSize;
        if (mode == "detection") tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Detection");
    }
    internal static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowTemplate.Height = 23;
        grid.AllowUserToResizeRows = false; grid.ColumnHeadersHeight = 28; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.MultiSelect = false;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 251);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "On", DataPropertyName = "Enabled", FillWeight = 12, MinimumWidth = 38 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Key", DataPropertyName = "Key", ReadOnly = true, FillWeight = 14, MinimumWidth = 40 });
        grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Role", DataPropertyName = "Role", DataSource = Roles, FillWeight = 34, MinimumWidth = 88, FlatStyle = FlatStyle.Flat });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Interval (s)", DataPropertyName = "Seconds", FillWeight = 26, MinimumWidth = 85 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "At / below %", DataPropertyName = "Threshold", FillWeight = 30, MinimumWidth = 96 });
        grid.DataSource = new BindingList<KeySlot>(slots);
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValidating += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 3 || (e.ColumnIndex == 4 && slots[e.RowIndex].Role is not ("Heal" or "Mana"))) return;
            bool interval = e.ColumnIndex == 3;
            if (!decimal.TryParse(Convert.ToString(e.FormattedValue), out decimal n) || n < (interval ? .1m : 1) || n > (interval ? 3600 : 99))
            { e.Cancel = true; grid.Rows[e.RowIndex].ErrorText = interval ? "Interval must be 0.1–3600 seconds." : "Threshold must be 1–99%."; }
            else grid.Rows[e.RowIndex].ErrorText = "";
        };
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var slot = slots[e.RowIndex];
            var color = ActionStyle.ColorFor(slot.Role, slot.Enabled);
            e.CellStyle!.ForeColor = color;
            e.CellStyle.SelectionForeColor = color;
            e.CellStyle.SelectionBackColor = Color.FromArgb(229, 235, 244);
            if (e.ColumnIndex != 4) return;
            bool conditional = slots[e.RowIndex].Role is "Heal" or "Mana";
            grid.Rows[e.RowIndex].Cells[4].ReadOnly = !conditional;
            if (!conditional) { e.Value = "—"; e.FormattingApplied = true; e.CellStyle!.ForeColor = Color.Silver; }
        };
        grid.CellValueChanged += (_, _) => grid.Invalidate();
        grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is ComboBox combo)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem -= DrawRole; combo.DrawItem += DrawRole;
            }
        };
        grid.DataError += (_, e) => { e.ThrowException = false; status.Text = "Check the highlighted key setting."; };
        setup.Add(grid);
    }

    private void DrawRole(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0) return;
        e.DrawBackground();
        TextRenderer.DrawText(e.Graphics, Convert.ToString(combo.Items[e.Index]), e.Font, e.Bounds,
            ActionStyle.ColorFor(Convert.ToString(combo.Items[e.Index]) ?? ""), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); if (previewOnly) return;
        bool f8 = Native.RegisterHotKey(Handle, 1, 0x4003, (uint)Keys.F11), f12 = Native.RegisterHotKey(Handle, 2, 0x4000, (uint)Keys.F12);
        hotkeys.Text = $"{(f8 ? "Ctrl+Alt+F11 start/stop" : "Use Start button")}  ·  {(f12 ? "F12 stop" : "Switch away to pause")}  ·  SendInput only";
    }
    protected override void WndProc(ref Message m)
    { if (m.Msg == 0x0312) { if (m.WParam.ToInt32() == 2) Stop("Stopped"); else Toggle(); } base.WndProc(ref m); }
    private bool TargetExists()
    {
        if (target is null || !Native.IsWindow(target.Handle)) return false;
        Native.GetWindowThreadProcessId(target.Handle, out uint pid);
        return pid == target.Pid && pid != Environment.ProcessId;
    }
    private bool TargetFocused => TargetExists() && !Native.IsIconic(target!.Handle) && Native.GetForegroundWindow() == target!.Handle;
    private bool ReadSettings()
    {
        if (!ValidateChildren() || !grid.EndEdit()) return false;
        settings.RepairPhrases = phrases.Text;
        if (slots.Any(s => s.Enabled && ((s.Role == "Heal" && !settings.Hp.Configured) || (s.Role == "Mana" && !settings.Mp.Configured) || (s.Role == "Repair" && !settings.Repair.Configured))))
        { status.Text = "Calibrate the area for each enabled Heal / Mana / Repair key."; return false; }
        if (slots.Any(s => s.Enabled && s.Role == "Repair") && !settings.RepairPhrases.Split(';').Any(p => p.Trim().Length >= 4))
        { status.Text = "Enter at least one repair phrase."; return false; }
        return true;
    }
    private void Toggle()
    {
        if (running) { Stop("Paused"); return; }
        if (calibrating || updateInstalling || !ReadSettings()) return;
        if (disconnectTask is { IsCompleted: false }) { status.Text = "Wait for previous disconnect scan to finish."; return; }
        disconnectTask = null;
        if (!slots.Any(s => s.Enabled)) { status.Text = "Enable at least one key."; return; }
        if (!TargetExists()) { SelectWindow(); return; }
        if (heldKey != 0 && !ReleaseKey()) return;
        if (!BeginStress()) return;
        InvalidateReadings(); awaitingFocus = !TargetFocused;
        schedule.Start(Environment.TickCount64); nextFocusAttempt = 0; running = true; start.Text = "Stop";
        status.Text = awaitingFocus ? "Switch to the game to begin" : "Running";
        foreach (var control in setup) control.Enabled = false;
        SaveSettings();
    }
    private void TickBot()
    {
        long now = Environment.TickCount64;
        TickHome(now);
        if (now >= nextBlink)
        {
            nextBlink = now + 650; blink = !blink; tabs.Invalidate();
            for (int i = 0; i < regionLabels.Count; i++)
                regionLabels[i].ForeColor = GetRegion(i).Configured ? Color.DarkGreen : (blink ? Color.Firebrick : Color.DarkOrange);
        }
        UpdateFocus(TargetExists(), TargetFocused, now);
        if (heldKey != 0 && (!running || awaitingFocus || now >= releaseAt))
        {
            if (!ReleaseKey()) { Stop("Windows blocked key release"); return; }
            if (active is not null) { if (stressRun is { } run) CompleteStressKey(active, now, (double)stressStart.Value / run.Rate); else schedule.Complete(active, now); }
            active = null;
        }
        // Recover focus before OCR, including while a stress scan is pending.
        if (settings.KeepGameActive && running && !calibrating && TargetExists() && !TargetFocused && now >= nextFocusAttempt)
        {
            if (!ReleaseKey()) { Stop("Windows blocked key release"); return; }
            nextFocusAttempt = now + 1000;
            Native.FocusWindow(target!.Handle);
            UpdateFocus(TargetExists(), TargetFocused, now);
            if (awaitingFocus) status.Text = "Waiting for Windows to focus the game";
        }
        if (heldKey != 0) { UpdateGameControls(now); return; }
        if (stressRun is not null) TickStress(now);
        UpdateGameControls(now); UpdateOverlays(); UpdateDetection(now);
        if (!running || awaitingFocus || !TargetFocused) return;
        if (new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (Native.GetAsyncKeyState(k) & 0x8000) != 0)) return;
        var slot = stressRun is not null
            ? (disconnectValidAt > 0 && now - disconnectValidAt <= 2000 && disconnectMatches == 0 && now >= nextStressInput ? StressDue(slots, now, s => VisionReader.Eligible(s, reading, repair, now) && (Native.GetAsyncKeyState(ActionStyle.VirtualKey(s.Key)) & 0x8000) == 0) : null)
            : schedule.Due(now, s => VisionReader.Eligible(s, reading, repair, now) && (Native.GetAsyncKeyState(ActionStyle.VirtualKey(s.Key)) & 0x8000) == 0);
        if (slot is null)
        {
            if (stressRun is not null) status.Text = disconnectMatches > 0 ? "Checking disconnect message" : disconnectValidAt == 0 || now - disconnectValidAt > 2000 ? "Waiting for disconnect scan" : "Waiting for next eligible key";
            return;
        }
        ushort vk = ActionStyle.VirtualKey(slot.Key); if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0) return;
        // Capture/OCR setup may have taken time; recheck focus immediately before injection.
        if (!TargetFocused || !Native.Key(vk, false)) { Stop("Input unavailable — paused"); return; }
        RecordInput(slot);
        heldKey = vk; active = slot; releaseAt = Environment.TickCount64 + (stressRun is null ? 60 : StressHoldMs(stressRun.Rate));
        if (stressRun is { } test) { test.Press(); nextStressInput = Environment.TickCount64 + (long)Math.Ceiling(1000 / test.Rate); }
        if (slot.Role == "Repair") repair.Consume();
        status.ForeColor = ActionStyle.ColorFor(slot.Role);
        status.Text = $"Running · {slot.Key} ({slot.Role})"; Log($"Key {slot.Key} ({slot.Role})");
    }
    internal void UpdateFocus(bool exists, bool focused, long now)
    {
        if (running && !awaitingFocus && !focused)
        {
            awaitingFocus = true; InvalidateReadings(preserveRepair: true);
            status.Text = "Focus paused · return to game to resume";
        }
        if (awaitingFocus)
        {
            if (!exists) Stop("Target unavailable");
            else if (focused) { awaitingFocus = false; schedule.Start(now); status.Text = "Running"; }
        }
    }
    private void UpdateDetection(long now)
    {
        if (!TargetFocused || calibrating)
        {
            if (reading is not null || (scanTask is not null && scanGeneration == generation)) InvalidateReadings(preserveRepair: true);
            return;
        }
        if (scanTask is { IsCompleted: true })
        {
            if (scanGeneration == generation && scanTask.IsCompletedSuccessfully)
            {
                var text = scanTask.Result;
                bool freshText = text.TextValid && now - text.At <= 2000;
                if (freshText) repair.Observe(RepairTrigger.Matches(text.Text, settings.RepairPhrases), now);
                else repair.Invalidate();
                reading = new(reading?.Hp, reading?.Mp, text.Text, text.Error, reading?.At ?? 0, freshText);
                ocrText.Text = text.Error.Length > 0 ? text.Error : text.Text.Length > 0 ? text.Text : "No matching text visible.";
            }
            scanTask = null;
        }
        if (now >= nextScan)
        {
            nextScan = now + 500;
            var client = Native.ClientBounds(target!.Handle);
            // Health readings never wait for the separate OCR task.
            bool Covered(CaptureRegion region) { var rect = region.Resolve(client.Size); rect.Offset(client.Location); return region.Configured && gameControls is { Visible: true } && gameControls.Bounds.IntersectsWith(rect); }
            var scanSettings = new ReloadedSettings { Hp = Covered(settings.Hp) ? new() : settings.Hp, Mp = Covered(settings.Mp) ? new() : settings.Mp, Repair = settings.Repair };
            var bars = VisionReader.ReadBars(client, scanSettings);
            if (Covered(settings.Repair)) { repair.Invalidate(); generation++; }
            if (Covered(settings.Hp) || Covered(settings.Mp) || Covered(settings.Repair)) status.Text = "Move game controls away from detection areas";
            reading = bars with { Text = reading?.Text ?? "", TextValid = reading?.TextValid ?? false };
            if (bars.Error.Length > 0) status.Text = bars.Error;
            if (scanTask is null && settings.Repair.Configured && !Covered(settings.Repair))
            { scanGeneration = generation; scanTask = vision.ReadAsync(client, settings); }
        }
        bool fresh = reading is not null && now - reading.At <= 2000;
        hpLabel.Text = fresh && reading!.Hp is double hp ? $"HP  {hp:0}%" : "HP  ?";
        mpLabel.Text = fresh && reading!.Mp is double mp ? $"MP  {mp:0}%" : "MP  ?";
        repairLabel.Text = repair.IsReady(now) ? "Repair  ready" : $"Repair  {repair.Count(now)}/5 (10 min)";
    }
    private void InvalidateReadings(bool preserveRepair = false)
    { generation++; reading = null; if (preserveRepair) repair.Invalidate(); else repair.Reset(); hpLabel.Text = "HP  —"; mpLabel.Text = "MP  —"; repairLabel.Text = "Repair  —"; }
    private bool ReleaseKey() { if (heldKey == 0) return true; if (!Native.Key(heldKey, true)) return false; heldKey = 0; return true; }
    private void Stop(string reason)
    {
        EndStress(reason);
        running = awaitingFocus = false; if (!ReleaseKey()) reason = "Key release blocked — retrying";
        active = null; status.ForeColor = ForeColor; start.Text = "Start"; status.Text = reason;
        foreach (var control in setup) control.Enabled = true;
    }
    private CaptureRegion GetRegion(int index) => index switch {
        0 => settings.Hp, 1 => settings.Mp, 2 => settings.Repair, 3 => settings.Disconnect,
        _ => settings.GameRegions.GetValueOrDefault(GameRegions[index].Id) ?? new()
    };
    private void Calibrate(int index)
    {
        Stop("Calibrating"); if (!TargetExists()) { SelectWindow(); if (!TargetExists()) return; }
        calibrating = true; telemetryEpoch++; gameControls?.Hide(); InvalidateReadings(); foreach (var overlay in overlays) overlay?.Hide();
        try
        {
            var client = Native.ClientBounds(target!.Handle);
            Native.SetForegroundWindow(target.Handle);
            using var editor = new RegionOverlay(client, GetRegion(index).Configured ? GetRegion(index).Resolve(client.Size) : Rectangle.Empty,
                GameRegions[index].Label, GameRegions[index].Color, true);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                var region = CaptureRegion.From(editor.Selected, client.Size);
                if (index == 0) settings.Hp = region; else if (index == 1) settings.Mp = region; else if (index == 2) settings.Repair = region; else if (index == 3) settings.Disconnect = region; else settings.GameRegions[GameRegions[index].Id] = region;
                telemetry.Remove(GameRegions[index].Id);
                if (index < regionLabels.Count) regionLabels[index].Text = $"Calibrated · {editor.Selected.Width} × {editor.Selected.Height}";
                SaveSettings();
            }
        }
        finally { calibrating = false; status.Text = "Paused · calibration saved"; }
    }
    private void UpdateGameControls(long now)
    {
        if (previewOnly || calibrating || !settings.ShowGameControls || !TargetFocused) { gameControls?.Hide(); return; }
        if (gameControls is null)
        {
            gameControls = new GameControlOverlay(settings);
            gameControls.ToggleRequested += Toggle;
            gameControls.LayoutSaved += SaveSettings;
        }
        bool fresh = reading is not null && now - reading.At <= 2000;
        gameControls.UpdateState(Native.ClientBounds(target!.Handle), running && !awaitingFocus,
            fresh ? reading!.Hp : null, fresh ? reading!.Mp : null, repair.IsReady(now));
        gameControls.UpdateTelemetry(HomeValue("mob_name_rect", now), HomeValue("map_coordinate_x_rect", now), HomeValue("map_coordinate_y_rect", now), sessionInputs, recentInputs.Count / Math.Min(10d, Math.Max(1, (now - sessionStarted) / 1000d)));
        if (!gameControls.Visible) gameControls.Show();
    }
    private void UpdateOverlays()
    {
        for (int i = 0; i < overlays.Length; i++)
        {
            if (previewOnly || calibrating || !settings.ShowOverlays || settings.HiddenRegions.Contains(GameRegions[i].Id) || !TargetFocused || !GetRegion(i).Configured) { overlays[i]?.Hide(); continue; }
            var client = Native.ClientBounds(target!.Handle); var rect = GetRegion(i).Resolve(client.Size);
            overlays[i] ??= new RegionOverlay(client, rect, GameRegions[i].Label, GameRegions[i].Color, false);
            overlays[i]!.SetRegion(client, rect, Enumerable.Range(0, GameRegions.Length).Where(n => GetRegion(n).Configured).Select(n => GetRegion(n).Resolve(client.Size)).ToArray());
            if (!overlays[i]!.Visible) overlays[i]!.Show();
        }
    }
    private void SelectWindow()
    {
        Stop("Paused"); InvalidateReadings();
        using var dialog = new Form { Text = "Select game window", ClientSize = new Size(450, 260), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var list = new ListBox { Dock = DockStyle.Fill }; list.Items.AddRange(Native.Windows().Cast<object>().ToArray());
        var choose = new Button { Text = "Select", Dock = DockStyle.Bottom, Height = 34 };
        void Pick() { if (list.SelectedItem is Native.Target picked) { telemetry.Clear(); telemetryEpoch++; target = picked; targetText.Text = picked.Title; dialog.Close(); } }
        choose.Click += (_, _) => Pick(); list.DoubleClick += (_, _) => Pick(); dialog.Controls.AddRange([list, choose]); dialog.ShowDialog(this);
    }
    private void LoadSettings()
    {
        try { settings = ReloadedSettings.Parse(File.ReadAllText(Path.Combine(dataFolder, "settings.json"))); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }
    private void SaveSettings()
    {
        if (previewOnly) return;
        try
        {
            settings.StressStageSeconds = (int)stressDuration.Value; settings.StressStart = stressStart.Value; settings.StressStep = stressStep.Value; settings.StressMax = stressMax.Value; settings.DisconnectPhrases = disconnectPhrases.Text;
            settings.Keys = slots; settings.RepairPhrases = phrases.Text; settings.WindowWidth = ClientSize.Width; settings.WindowHeight = ClientSize.Height;
            Directory.CreateDirectory(dataFolder); var path = Path.Combine(dataFolder, "settings.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(settings)); File.Move(path + ".tmp", path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Settings could not be saved"; }
    }
    private void Log(string text)
    {
        try { Directory.CreateDirectory(dataFolder); string path = Path.Combine(dataFolder, "keys.log"); if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true); File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Stop("Stopped"); if (heldKey != 0) { e.Cancel = true; return; }
        grid.EndEdit(); SaveSettings(); generation++;
        timer.Dispose(); buffPreview.Image?.Dispose(); gameControls?.Dispose(); foreach (var overlay in overlays) overlay?.Dispose();
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2); tips.Dispose();
        base.OnFormClosing(e);
    }
}
