using System.Runtime.InteropServices;
using KathanaWindowCapture;

internal static class Program
{
    private static int checks;
    [STAThread]
    private static void Main(string[] args)
    {
        using (var cache = new LatestWindowFrame())
        {
            var first = new Bitmap(101, 61);
            first.SetPixel(50, 30, Color.Blue);
            cache.Publish(first, 1000);
            using var copy = cache.Copy(101, 61, 1200, 300, out var at);
            Check(copy is not null && at == 1000 && copy.GetPixel(50, 30).ToArgb() == Color.Blue.ToArgb(), "fresh source frame and timestamp");
            copy!.SetPixel(50, 30, Color.Red);
            using var independent = cache.Copy(101, 61, 1200, 300, out _);
            Check(independent!.GetPixel(50, 30).ToArgb() == Color.Blue.ToArgb(), "readers cannot mutate the cache");
            Check(cache.Copy(101, 61, 1301, 300, out _) is null, "stale frame rejected");
            Check(cache.Copy(101, 61, 999, 300, out _) is null, "future timestamp rejected");
            Check(cache.Copy(102, 61, 1200, 300, out _) is null, "resized client cannot use old geometry");
            cache.Publish(new Bitmap(103, 63), 1250);
            Check(cache.Copy(101, 61, 1300, 300, out _) is null, "old dimensions removed after resize");
            Check(cache.Status(1300, 300, "live").Frames == 2, "latest buffer replaces frames instead of queueing them");
            cache.Clear();
            Check(!cache.Status(1300, 300, "closed").Ready, "cleared frame cannot authorize input");
            cache.Dispose();
            cache.Publish(new Bitmap(103, 63), 1350);
            Check(cache.Copy(103, 63, 1400, 300, out _) is null, "late callback after shutdown cannot republish");
        }
        if (args.Contains("--native")) RunNative();
        Console.WriteLine($"PASS: {checks} window-capture assertions{(args.Contains("--native") ? " including covered-window WGC, movement, resize, minimization, identity and close" : " (pure frame-cache checks; pass --native for the owned-window capture fixture)")}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void RunNative()
    {
        Exception? failure = null;
        using var target = new Fixture(Color.Blue) { Text = "Kathana capture test (owned fixture)", ClientSize = new Size(301, 181), Location = new Point(80, 80) };
        using var cover = new Fixture(Color.Lime) { Text = "Capture cover (owned fixture)", ClientSize = new Size(380, 250), Location = new Point(60, 60), TopMost = true };
        target.Shown += async (_, _) =>
        {
            try
            {
                using var capture = new WindowCapture(target.Handle, (uint)Environment.ProcessId, rejectBlackFrames: true);
                await AssertBlue(capture, target, "uncovered native frame");
                cover.Show();
                await Task.Delay(350);
                await AssertBlue(capture, target, "covered window still supplies blue game pixels, not the green cover");
                target.Location = new Point(100, 100);
                target.ClientSize = new Size(307, 187);
                await Task.Delay(350);
                await AssertBlue(capture, target, "move and odd-width resize produce correct fresh client pixels");
                target.SceneColor = Color.Black;
                target.Invalidate();
                await Task.Delay(350);
                Check(!capture.Status(300).Ready, "black source pixels cannot keep the continuous capture gate open");
                target.SceneColor = Color.Blue;
                await AssertBlue(capture, target, "usable pixels recover after a black frame");
                using var foreign = new WindowCapture(target.Handle, (uint)Environment.ProcessId + 1);
                await Task.Delay(200);
                Check(foreign.Copy(target.ClientSize.Width, target.ClientSize.Height, 500, out _) is null, "wrong process identity never supplies a frame");
                target.WindowState = FormWindowState.Minimized;
                Check(capture.Copy(307, 187, 500, out _) is null, "minimized window invalidates captured frames");
                target.Close();
                Check(!capture.Status(500).Ready, "closed window cannot supply a live frame");
            }
            catch (Exception ex) { failure = ex; }
            finally { cover.Close(); target.Close(); }
        };
        Application.Run(target);
        if (failure is not null) throw new InvalidOperationException("Native capture fixture failed", failure);
    }

    private static async Task AssertBlue(WindowCapture capture, Form target, string message)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            target.Invalidate(); // keeps the source producing frames during this test
            using var bitmap = capture.Copy(target.ClientSize.Width, target.ClientSize.Height, 300, out _);
            if (bitmap is not null)
            {
                var color = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
                Check(color.B > 200 && color.G < 30 && color.R < 30, message);
                var corner = bitmap.GetPixel(0, 0);
                // Windows 11 masks the very last corner pixel of rounded windows; sample inside the marker.
                var opposite = bitmap.GetPixel(bitmap.Width - 6, bitmap.Height - 6);
                Check(corner.R > 200 && corner.B < 30 && opposite.R > 200 && opposite.G > 200 && opposite.B < 30,
                    $"client crop excludes window decorations and preserves both corner markers; first={corner}, last={opposite}, size={bitmap.Size}; {capture.Status(300).Note}");
                return;
            }
            await Task.Delay(100);
        }
        throw new InvalidOperationException(message + ": " + capture.Status(300).Note);
    }

    private sealed class Fixture(Color color) : Form
    {
        public Color SceneColor = color;
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000; return value; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SceneColor);
            if (SceneColor == Color.Blue)
            {
                e.Graphics.FillRectangle(Brushes.Red, 0, 0, 8, 8);
                e.Graphics.FillRectangle(Brushes.Yellow, ClientSize.Width - 8, ClientSize.Height - 8, 8, 8);
            }
        }
    }
}
