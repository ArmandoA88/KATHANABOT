using System.Diagnostics;

namespace KathanaBotReloaded;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--apply-update") return ReloadedUpdater.Apply(args);
        if (args.Contains("--self-test"))
        {
            try { KeySchedule.SelfTest(); StressRun.SelfTest(); ReloadedUpdater.SelfTest(); BotForm.HomeSelfTest(); if (args.Contains("--ocr")) DetectionTests.Ocr().GetAwaiter().GetResult(); DetectionTests.Run(); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-error.txt"), ex.ToString()); return 1; }
        }
        using var mutex = new Mutex(true, args.Contains("--preview") ? "Local\\KathanaBotReloadedPreview" + Environment.ProcessId : "Local\\KathanaBotReloaded", out bool first);
        if (!first)
        {
            foreach (var window in Native.Windows().Where(w => w.Title == "KATHANA BOT RELOADED"))
            {
                Native.ShowWindow(window.Handle, 9);
                Native.SetForegroundWindow(window.Handle);
            }
            return 0;
        }
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 2 && args[0] == "--preview")
        {
            if (args.Length > 2 && args[2] == "hud")
            {
                using var hud = new GameControlOverlay(new ReloadedSettings());
                hud.UpdateState(new Rectangle(-30000, -30000, 800, 600), true, 75, 48, false);
                hud.UpdateTelemetry("Preview target", "123", "456", 250, 4.2);
                hud.Show(); Application.DoEvents();
                using var snapshot = new Bitmap(hud.Width, hud.Height);
                hud.DrawToBitmap(snapshot, new Rectangle(Point.Empty, snapshot.Size)); snapshot.Save(args[1]);
                return 0;
            }
            using var preview = new BotForm(previewOnly: true);
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-30000, -30000);
            preview.PreparePreview(args.Length > 2 ? args[2] : "");
            preview.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            // WinForms' parent snapshot omits native edit controls; render those controls too.
            using (var graphics = Graphics.FromImage(bitmap))
                foreach (var field in BotForm.Descendants(preview).OfType<TextBox>().Where(c => c.Visible))
                {
                    var screen = field.Parent!.PointToScreen(field.Location);
                    var rect = new Rectangle(screen.X - preview.Left, screen.Y - preview.Top, field.Width, field.Height);
                    using var background = new SolidBrush(field.BackColor);
                    var savedGraphics = graphics.Save();
                    var visible = rect;
                    for (Control? parent = field.Parent; parent is not null; parent = parent.Parent)
                    {
                        var topLeft = parent.PointToScreen(Point.Empty);
                        visible.Intersect(new Rectangle(topLeft.X - preview.Left, topLeft.Y - preview.Top, parent.ClientSize.Width, parent.ClientSize.Height));
                    }
                    graphics.SetClip(visible);
                    graphics.FillRectangle(background, rect);
                    ControlPaint.DrawBorder3D(graphics, rect, Border3DStyle.Sunken);
                    rect.Inflate(-3, -2);
                    TextRenderer.DrawText(graphics, field.Text, field.Font, rect, field.ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | (field.Multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
                    graphics.Restore(savedGraphics);
                }
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }
        Application.Run(new BotForm());
        return 0;
    }
}
