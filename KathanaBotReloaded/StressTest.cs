using System.Text.Json;

namespace KathanaBotReloaded;

internal sealed partial class BotForm
{
    private readonly CheckBox stressEnabled = new() { Text = "Enable stress test", AutoSize = true };
    private readonly NumericUpDown stressStart = RateControl(1), stressStep = RateControl(1), stressMax = RateControl(10);
    private readonly NumericUpDown stressDuration = new() { Minimum = 1, Maximum = 86400, Value = 300, Width = 100 };
    private readonly TextBox disconnectPhrases = new() { Text = "disconnected; connection lost; connection to server lost", Width = 380 };
    private readonly Label stressStatus = new() { Text = "No test running", AutoSize = true, MaximumSize = new Size(420, 0) };
    private readonly VisionReader disconnectVision = new();
    private Task<DetectionReading>? disconnectTask;
    private StressRun? stressRun;
    private long nextDisconnectScan, nextStressInput;
    private int disconnectMatches;
    private long disconnectScanStarted;
    private int disconnectEpoch, pendingDisconnectEpoch;
    private long lastDisconnectMatch, disconnectValidAt, nextStressSave;
    private static NumericUpDown RateControl(decimal value) => new() { Minimum = .1m, Maximum = 20, DecimalPlaces = 1, Increment = .5m, Value = value, Width = 100 };

    private void BuildStressUi()
    {
        stressStart.Value = Math.Clamp(settings.StressStart, .1m, 20m);
        stressStep.Value = Math.Clamp(settings.StressStep, .1m, 20m);
        stressMax.Value = Math.Clamp(settings.StressMax, .1m, 20m);
        stressDuration.Value = Math.Clamp(settings.StressStageSeconds, 1, 86400);
        disconnectPhrases.Text = settings.DisconnectPhrases;
        var page = new TabPage("Stress test") { AutoScroll = true, Padding = new Padding(10) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(stressEnabled);
        void Rate(string label, Control input) { panel.Controls.Add(new Label { Text = label, AutoSize = true }); panel.Controls.Add(input); setup.Add(input); }
        Rate("Starting inputs / second", stressStart); Rate("Increase inputs / second after each stage", stressStep); Rate("Duration per stage (active seconds)", stressDuration); Rate("Maximum inputs / second (stop after final stage)", stressMax);
        var calibrate = new Button { Text = "Set disconnect message area", AutoSize = true };
        calibrate.Click += (_, _) => Calibrate(3); panel.Controls.Add(calibrate); setup.Add(calibrate);
        Rate("Disconnect phrases (separate with ;)", disconnectPhrases);
        panel.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = "Uses enabled keys, intervals and role priorities from the Keys tab. Holds are 60 ms, shortened only for high rates. Heal, Mana and Repair still require their detection conditions. Each rate increase shortens key intervals proportionally; actual throughput depends on enabled keys. Focus loss pauses the test. Two matching OCR scans stop it. Use only on a server you may test. F12 stops." });
        panel.Controls.Add(stressStatus); setup.Add(stressEnabled);
        page.Controls.Add(panel); tabs.TabPages.Add(page);
    }

    private bool BeginStress()
    {
        if (!stressEnabled.Checked) return true;
        if (!settings.Disconnect.Configured || stressStart.Value > stressMax.Value || !disconnectPhrases.Text.Split(';').Any(p => p.Trim().Length >= 4) || !slots.Any(s => s.Enabled))
        { status.Text = "Set disconnect area, phrases, valid rates and an enabled key."; return false; }
        try
        {
            Directory.CreateDirectory(dataFolder);
            string path = Path.Combine(dataFolder, $"stress-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl");
            stressRun = new StressRun((double)stressStart.Value, (double)stressStep.Value, (double)stressMax.Value, path, (int)stressDuration.Value);
            stressRun.Save("started", "", target!.Title);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stressRun = null; status.Text = "Cannot create stress log: " + ex.Message; return false; }
        nextStressInput = nextDisconnectScan = 0; disconnectMatches = 0; disconnectEpoch++; lastDisconnectMatch = disconnectValidAt = nextStressSave = 0;
        // Any scan from an earlier test must finish before this test can start.
        stressStatus.Text = "Test armed. Results: " + stressRun.Path;
        return true;
    }
    internal static int StressHoldMs(double rate) => Math.Clamp((int)(1000 / rate * .6), 20, 60);
    internal static KeySlot? StressDue(IEnumerable<KeySlot> keys, long now, Func<KeySlot, bool> eligible) =>
        keys.Where(k => k.Enabled && k.Due <= now && eligible(k))
            .OrderBy(k => k.Role switch { "Heal" => 0, "Mana" => 1, "Repair" => 2, _ => 3 })
            .ThenBy(k => k.Due).FirstOrDefault();
    internal static void CompleteStressKey(KeySlot key, long now, double scale) =>
        key.Due = now + Math.Max(20, (long)((double)key.Seconds * 1000 * scale));

    private void TickStress(long now)
    {
        var run = stressRun;
        if (run is null) return;
        try
        {
            if (!TargetFocused) { run.Advance(now, false); disconnectMatches = 0; disconnectValidAt = 0; disconnectEpoch++; return; }
            if (disconnectTask is { IsCompleted: false } && now - disconnectScanStarted > 10000)
            { Stop("Disconnect OCR timed out; restart the test"); return; }
            if (disconnectTask is { IsCompleted: true })
            {
                var result = disconnectTask.IsCompletedSuccessfully ? disconnectTask.Result : null;
                disconnectTask = null;
                if (pendingDisconnectEpoch != disconnectEpoch) { nextDisconnectScan = 0; return; }
                if (result is null || !result.TextValid || now - result.At > 10000)
                { Stop("Disconnect detection unavailable; test stopped"); return; }
                disconnectValidAt = now;
                bool match = RepairTrigger.Matches(result.Text, disconnectPhrases.Text);
                disconnectMatches = match ? (now - lastDisconnectMatch <= 2500 ? disconnectMatches + 1 : 1) : 0;
                if (match) lastDisconnectMatch = now;
                if (disconnectMatches >= 2)
                {
                    run.Save("disconnect", result.Text, target!.Title);
                    stressStatus.Text = $"Disconnect detected: target {run.Rate:0.##}/s; measured {run.MeasuredRate:0.###}/s. Saved: {run.Path}";
                    stressRun = null; Stop("Disconnect detected - test stopped"); return;
                }
            }
            if (now >= nextDisconnectScan && disconnectTask is null)
            {
                nextDisconnectScan = now + 500;
                var client = Native.ClientBounds(target!.Handle);
                var rect = settings.Disconnect.Resolve(client.Size); rect.Offset(client.Location);
                if (gameControls is { Visible: true } && gameControls.Bounds.IntersectsWith(rect)) { Stop("Move game controls away from disconnect area"); return; }
                disconnectScanStarted = now; pendingDisconnectEpoch = disconnectEpoch;
                var scanSettings = new ReloadedSettings { Repair = settings.Disconnect };
                // OCR encoding/recognition must not block the input and focus timer.
                disconnectTask = Task.Run(() => disconnectVision.ReadAsync(client, scanSettings));
            }
            if (run.Advance(now, disconnectValidAt > 0 && now - disconnectValidAt <= 2000 && disconnectMatches == 0))
            {
                run.Save("stage_completed", "", target!.Title);
                if (!run.NextStage()) { Stop("Maximum test rate completed without detected disconnect"); return; }
                run.Save("stage_started", "", target!.Title);
            }
            if (now >= nextStressSave) { run.Save("sample", "", target!.Title); nextStressSave = now + 1000; }
            stressStatus.Text = $"Stage {run.Stage}: target {run.Rate:0.##}/s; measured {run.MeasuredRate:0.###}/s; {run.StageMs / 1000:0}/{run.StageDurationMs / 1000} active seconds";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Stop("Stress log failed: " + ex.Message); }
    }
    private void EndStress(string reason)
    {
        if (stressRun is not { } run) return;
        stressRun = null;
        try { run.Save("stopped", reason, target?.Title ?? ""); stressStatus.Text = $"{reason}. Last target {run.Rate:0.##}/s; measured {run.MeasuredRate:0.###}/s. Saved: {run.Path}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stressStatus.Text = "RESULT NOT SAVED: " + ex.Message; }
    }
}

internal sealed class StressRun(double start, double step, double max, string path, int stageSeconds = 300)
{
    public string Path { get; } = path;
    public long StageDurationMs { get; } = checked(Math.Clamp(stageSeconds, 1, 86400) * 1000L);
    public double Rate { get; private set; } = start;
    public int Stage { get; private set; } = 1;
    public long StageMs { get; private set; }
    public long Inputs { get; private set; }
    public long TotalInputs { get; private set; }
    private long? last;
    private bool wasActive;
    public double MeasuredRate => StageMs > 0 ? Inputs * 1000d / StageMs : 0;
    public bool Advance(long now, bool active)
    {
        if (active && wasActive && last.HasValue) StageMs += Math.Max(0, now - last.Value);
        last = now; wasActive = active;
        return StageMs >= StageDurationMs;
    }
    public void Press() { Inputs++; TotalInputs++; }
    public bool NextStage()
    {
        if (Rate >= max) return false;
        Rate = Math.Min(max, Rate + step); Stage++; StageMs = Inputs = 0; return true;
    }
    public void Save(string kind, string evidence, string target) => File.AppendAllText(Path, JsonSerializer.Serialize(new
    {
        timestampUtc = DateTime.UtcNow, kind, target, stage = Stage, requestedInputsPerSecond = Rate,
        measuredInputsPerSecond = MeasuredRate, stageInputs = Inputs, stageActiveSeconds = StageMs / 1000d,
        totalInputs = TotalInputs, stageDurationSeconds = StageDurationMs / 1000, evidence, measurement = "Successful SendInput key-downs; server acceptance and disconnect cause unverified"
    }) + Environment.NewLine);
    public static void SelfTest()
    {
        foreach (int seconds in new[] { 1, 60, 420, 86400 })
        {
            var custom = new StressRun(1, 1, 3, "unused", seconds);
            custom.Advance(0, true);
            if (custom.Advance(seconds * 1000L - 1, true) || !custom.Advance(seconds * 1000L, true))
                throw new Exception("Custom stage duration boundary failed");
            custom.NextStage();
            if (custom.StageMs != 0 || custom.StageDurationMs != seconds * 1000L)
                throw new Exception("Custom duration was not retained across stages");
        }
        var keys = new List<KeySlot> {
            new("A") { Enabled = false }, new("B") { Enabled = true, Role = "Heal" },
            new("C") { Enabled = true, Role = "Mana" }, new("D") { Enabled = true, Role = "Repair" },
            new("E") { Enabled = true, Role = "Attack" }
        };
        keys[1].Due = 100;
        if (BotForm.StressDue(keys, 99, _ => true) != keys[2] ||
            BotForm.StressDue(keys, 100, _ => true) != keys[1] ||
            BotForm.StressDue(keys, 100, k => k.Role == "Attack") != keys[4] ||
            BotForm.StressDue(keys, 100, _ => false) != null)
            throw new Exception("Stress role priority, due time or eligibility failed");
        var first = new KeySlot("4") { Enabled = true, Seconds = .6m };
        var second = new KeySlot("E") { Enabled = true, Seconds = .2m };
        BotForm.CompleteStressKey(first, 1000, .5);
        BotForm.CompleteStressKey(second, 1000, .5);
        if (first.Due != 1300 || second.Due != 1100 || BotForm.StressDue([first, second], 1100, _ => true) != second)
            throw new Exception("Stress must retain and scale individual key intervals");
        // Simulate several seconds with the user's ordinary actions and a held key.
        var actions = new List<KeySlot> { first, second, new("6") { Enabled = true, Seconds = .6m }, new("R") { Enabled = true } };
        var sent = new HashSet<string>();
        for (long time = 2000; time < 12000; time += 200)
        {
            var key = BotForm.StressDue(actions, time, k => k.Key != "4");
            if (key is null) continue;
            sent.Add(key.Key); BotForm.CompleteStressKey(key, time + 60, 1);
        }
        if (!sent.SetEquals(new[] { "E", "6", "R" })) throw new Exception("Held key must not block other actions");
        if (BotForm.StressHoldMs(1) != 60 || BotForm.StressHoldMs(5) != 60 || BotForm.StressHoldMs(20) != 30)
            throw new Exception("Stress holds must remain detectable and fit the rate");
        if (ReloadedSettings.Parse("{}").StressStageSeconds != 300 ||
            ReloadedSettings.Parse(JsonSerializer.Serialize(new ReloadedSettings { StressStageSeconds = 45 })).StressStageSeconds != 45)
            throw new Exception("Stress duration settings compatibility failed");
        var run = new StressRun(1, 2, 4, "unused");
        run.Advance(0, true); run.Press(); run.Advance(1000, true);
        if (run.MeasuredRate != 1) throw new Exception("Stress rate measurement");
        run.Advance(2000, false); run.Advance(100000, true);
        if (run.StageMs != 1000) throw new Exception("Stress focus pause");
        if (run.Advance(398999, true) || !run.Advance(399000, true)) throw new Exception("Stress stage duration");
        if (!run.NextStage() || run.Rate != 3 || run.Inputs != 0 || !run.NextStage() || run.Rate != 4 || run.NextStage()) throw new Exception("Stress ramp limit");
    }
}
