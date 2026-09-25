namespace KathanaBotReloaded;

internal static class DetectionTests
{
    public static async Task Ocr()
    {
        using var image = new Bitmap(700, 90);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            using var font = new Font("Arial", 26);
            graphics.DrawString("Unable to reach target", font, Brushes.Black, 10, 15);
        }
        string text = await new VisionReader().ReadImageAsync(image);
        if (!RepairTrigger.Matches(text, "unable to reach target")) throw new Exception("OCR integration did not recognize the test prompt: " + text);
    }
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        foreach (int n in Enumerable.Range(1, 10))
            Check(ActionStyle.VirtualKey($"F{n}") == 0x70 + n - 1, $"F{n} mapping failed");
        Check(ActionStyle.VirtualKey("F") == 0x46, "F must remain distinct from F1");
        Check(ActionStyle.KeysAvailable.Distinct().Count() == 23, "Key catalog must include all old and function keys");
        Check(ActionStyle.ColorFor("Heal", false) == Color.Gray && ActionStyle.ColorFor("Mana") == Color.RoyalBlue, "Role colors failed");
        var focusSettings = ReloadedSettings.Parse(System.Text.Json.JsonSerializer.Serialize(new ReloadedSettings { KeepGameActive = true }));
        Check(focusSettings.KeepGameActive && !ReloadedSettings.Parse("{}").KeepGameActive, "Focus setting migration failed");
        using (var form = new BotForm(previewOnly: true))
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var armed = typeof(BotForm).GetField("running", flags)!;
            var waiting = typeof(BotForm).GetField("awaitingFocus", flags)!;
            armed.SetValue(form, true);
            form.UpdateFocus(true, false, 1000);
            Check((bool)armed.GetValue(form)! && (bool)waiting.GetValue(form)!, "Focus loss must retain resume intent");
            form.UpdateFocus(true, true, 2000);
            Check((bool)armed.GetValue(form)! && !(bool)waiting.GetValue(form)!, "Focus return must resume");
            typeof(BotForm).GetMethod("Stop", flags)!.Invoke(form, ["Manual stop"]);
            form.UpdateFocus(true, true, 3000);
            Check(!(bool)armed.GetValue(form)!, "Manual stop must not auto resume");
            armed.SetValue(form, true);
            form.UpdateFocus(false, false, 4000);
            Check(!(bool)armed.GetValue(form)!, "Closed target must stop");
        }
        foreach (bool hp in new[] { true, false })
        foreach (int percent in new[] { 10, 35, 70, 100 })
        {
            using var bar = new Bitmap(100, 12);
            using (var g = Graphics.FromImage(bar))
            { g.Clear(Color.FromArgb(30, 30, 30)); using var brush = new SolidBrush(hp ? Color.Red : Color.Blue); g.FillRectangle(brush, 0, 0, percent, 12); }
            Check(Math.Abs((VisionReader.BarPercent(bar, hp) ?? -100) - percent) < 1, "Incorrect HP/MP percentage");
            Check(VisionReader.BarPercent(bar, !hp) is null, "Wrong bar color accepted");
        }
        using (var blank = new Bitmap(100, 12)) Check(VisionReader.BarPercent(blank, true) is null, "Blank frame became zero HP");
        var repair = new RepairTrigger();
        Check(RepairTrigger.Matches("Unable to reach target!", "unable to reach target; needs repair"), "Unreachable prompt not matched");
        Check(!RepairTrigger.Matches("Attack completed", "unable to reach target; needs repair"), "Unrelated text matched");
        void Sighting(long at)
        {
            repair.Observe(false, at - 1000); repair.Observe(false, at - 500);
            repair.Observe(true, at); repair.Observe(true, at + 500);
        }
        repair.Observe(true, 1000);
        Check(repair.Count(1000) == 0, "Single OCR false positive counted");
        repair.Observe(true, 1500);
        for (long at = 2000; at < 10000; at += 500) repair.Observe(true, at);
        Check(repair.Count(10000) == 1 && !repair.IsReady(9500), "Persistent text counted multiple times");
        repair.Invalidate(); repair.Observe(true, 10500); repair.Observe(true, 11000);
        Check(repair.Count(11000) == 1, "Capture failure duplicated a sighting");
        Sighting(120000); Sighting(240000); Sighting(360000);
        Check(!repair.IsReady(360500) && repair.Count(360500) == 4, "Fewer than five sightings triggered");
        Sighting(480000);
        Check(repair.IsReady(480500), "Five sightings within ten minutes did not trigger");
        Check(!repair.IsReady(483000), "Stale repair triggered");
        repair.Consume(); repair.Observe(true, 483500); repair.Observe(true, 484000);
        Check(repair.Count(484000) == 0 && !repair.IsReady(484000), "Consumed sightings reused");
        repair.Reset(); Sighting(1000); Sighting(120000); Sighting(240000); Sighting(360000); Sighting(602000);
        Check(repair.Count(602500) == 4 && !repair.IsReady(602500), "Expired sightings counted");
        repair.Reset(); Check(repair.Count(603000) == 0, "Reset retained history");
        var reading = new DetectionReading(30, 60, "needs repair", "", 4000, true);
        var heal = new KeySlot("1") { Enabled = true, Role = "Heal", Threshold = 40 };
        var mana = new KeySlot("2") { Enabled = true, Role = "Mana", Threshold = 40 };
        Check(VisionReader.Eligible(heal, reading, repair, 4000), "Low HP does not enable heal");
        Check(!VisionReader.Eligible(mana, reading, repair, 4000), "High MP triggered mana");
        Check(!VisionReader.Eligible(heal, reading, repair, 6500) && !VisionReader.Eligible(heal, null, repair, 4000), "Stale/missing HP enabled heal");
        var attack = new KeySlot("R") { Enabled = true };
        var schedule = new KeySchedule([attack, heal, mana]); schedule.Start(0);
        Check(schedule.Due(4000, s => VisionReader.Eligible(s, reading, repair, 4000)) == heal, "Heal does not take priority");
        schedule.Complete(heal, 4000); Check(schedule.Due(4001, s => VisionReader.Eligible(s, reading, repair, 4001)) == attack, "Ready ordinary action was blocked after heal");
        var rect = new Rectangle(10, 20, 100, 10); var region = CaptureRegion.From(rect, new Size(200, 100));
        Check(region.Resolve(new Size(400, 200)) == new Rectangle(20, 40, 200, 20), "Overlay does not scale with game client");
        var old = ReloadedSettings.Parse("[{\"Key\":\"E\",\"Enabled\":true,\"Seconds\":2}]");
        Check(old.Keys.Single().Seconds == 2 && old.Keys.Single().Enabled, "Legacy settings lost");
        var settings = new ReloadedSettings { Keys = [heal], Hp = region, RepairPhrases = "needs repair" };
        var restored = ReloadedSettings.Parse(System.Text.Json.JsonSerializer.Serialize(settings));
        Check(restored.Keys.Single().Role == "Heal" && restored.Hp.Configured && restored.RepairPhrases == "needs repair", "New settings roundtrip failed");
    }
}
