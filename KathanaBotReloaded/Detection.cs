using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace KathanaBotReloaded;

public sealed class CaptureRegion
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Configured => Width > 0 && Height > 0;
    public Rectangle Resolve(Size size) => Rectangle.Intersect(new Rectangle(Point.Empty, size),
        new Rectangle((int)(X * size.Width), (int)(Y * size.Height), Math.Max(1, (int)(Width * size.Width)), Math.Max(1, (int)(Height * size.Height))));
    public static CaptureRegion From(Rectangle rect, Size size) => new()
    { X = rect.X / (double)size.Width, Y = rect.Y / (double)size.Height, Width = rect.Width / (double)size.Width, Height = rect.Height / (double)size.Height };
}

public sealed class ReloadedSettings
{
    public Dictionary<string, CaptureRegion> GameRegions { get; set; } = new();
    public HashSet<string> HiddenRegions { get; set; } = [];
    public bool AutoCheckUpdates { get; set; } = true;
    public bool ShowGameControls { get; set; } = true;
    public List<KeySlot> Keys { get; set; } = [];
    public CaptureRegion Hp { get; set; } = new();
    public CaptureRegion Mp { get; set; } = new();
    public int StressStageSeconds { get; set; } = 300;
    public decimal StressStart { get; set; } = 1;
    public decimal StressStep { get; set; } = 1;
    public decimal StressMax { get; set; } = 10;
    public string DisconnectPhrases { get; set; } = "disconnected; connection lost; connection to server lost";
    public CaptureRegion Disconnect { get; set; } = new();
    public CaptureRegion Repair { get; set; } = new();
    public string RepairPhrases { get; set; } = "unable to reach target; cannot reach target; unreachable; about to break; almost broken; needs repair; repair required; low durability";
    public bool KeepGameActive { get; set; }
    public bool ShowOverlays { get; set; } = true;
    public int ControlX { get; set; } = -1;
    public int ControlY { get; set; } = 10;
    public int ControlWidth { get; set; } = 240;
    public int ControlHeight { get; set; } = 76;
    public int WindowWidth { get; set; } = 860;
    public int WindowHeight { get; set; } = 680;
    public static ReloadedSettings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new ReloadedSettings { Keys = JsonSerializer.Deserialize<List<KeySlot>>(json) ?? [] }
            : JsonSerializer.Deserialize<ReloadedSettings>(json) ?? new();
    }
}

public sealed record DetectionReading(double? Hp, double? Mp, string Text, string Error, long At, bool TextValid);

public sealed class RepairTrigger
{
    private const long WindowMs = 10 * 60 * 1000;
    private readonly Queue<long> sightings = new();
    private int matches, clears;
    private bool counted;
    public bool Ready { get; private set; }
    public long LastSeen { get; private set; }
    public int Count(long now)
    {
        while (sightings.TryPeek(out long at) && now - at > WindowMs) sightings.Dequeue();
        return sightings.Count;
    }
    public static bool Matches(string text, string phrases)
    {
        static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", "");
        string normalized = Normalize(text);
        return phrases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize).Any(p => p.Length >= 4 && normalized.Contains(p));
    }
    public void Observe(bool match, long now)
    {
        Count(now);
        if (match)
        {
            if (now - LastSeen > 2500) matches = 0;
            LastSeen = now;
            clears = 0;
            if (++matches >= 2 && !counted)
            {
                sightings.Enqueue(now);
                counted = true;
            }
            Ready = sightings.Count >= 5;
        }
        else
        {
            Ready = false;
            matches = 0;
            if (++clears >= 2) counted = false;
        }
    }
    public bool IsReady(long now) => Ready && now - LastSeen <= 2000 && Count(now) >= 5;
    // Failed captures cannot create a new occurrence or erase valid recent history.
    public void Invalidate() { matches = clears = 0; Ready = false; }
    public void Consume() { sightings.Clear(); counted = true; Ready = false; }
    public void Reset() { sightings.Clear(); matches = clears = 0; counted = Ready = false; LastSeen = 0; }

}

internal sealed class VisionReader
{
    private OcrEngine? engine;
    private bool attempted;

    public static bool Eligible(KeySlot key, DetectionReading? reading, RepairTrigger repair, long now)
    {
        bool fresh = reading is not null && now - reading.At <= 2000;
        return key.Role switch
        {
            "Heal" => fresh && reading!.Hp is double hp && hp <= (double)key.Threshold,
            "Mana" => fresh && reading!.Mp is double mp && mp <= (double)key.Threshold,
            "Repair" => fresh && reading!.TextValid && repair.IsReady(now),
            _ => true
        };
    }

    public static DetectionReading ReadBars(Rectangle client, ReloadedSettings settings)
    {
        double? hp = null, mp = null;
        var errors = new List<string>();
        try { if (settings.Hp.Configured) { using var crop = Capture(client, settings.Hp); hp = BarPercent(crop, true); } }
        catch (Exception ex) { errors.Add("HP: " + ex.Message); }
        try { if (settings.Mp.Configured) { using var crop = Capture(client, settings.Mp); mp = BarPercent(crop, false); } }
        catch (Exception ex) { errors.Add("MP: " + ex.Message); }
        return new(hp, mp, "", string.Join(" ", errors), Environment.TickCount64, false);
    }

    public async Task<DetectionReading> ReadAsync(Rectangle client, ReloadedSettings settings)
    {
        long capturedAt = Environment.TickCount64;
        try
        {
            using var crop = Capture(client, settings.Repair);
            string text = await ReadImageAsync(crop);
            return new(null, null, text, "", capturedAt, true);
        }
        catch (Exception ex) { return new(null, null, "", "OCR unavailable: " + ex.Message, capturedAt, false); }
    }

    internal async Task<string> ReadImageAsync(Bitmap bitmap)
    {
        if (!attempted)
        {
            attempted = true;
            engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }
        if (engine is null) throw new InvalidOperationException("Install a Windows OCR language to enable repair detection.");
        return await ReadTextAsync(bitmap);
    }

    internal static Bitmap Capture(Rectangle client, CaptureRegion region)
    {
        var local = region.Resolve(client.Size);
        if (local.Width < 3 || local.Height < 3) throw new InvalidOperationException("Calibrate a larger detection area.");
        var rect = new Rectangle(client.Location + (Size)local.Location, local.Size);
        if (!SystemInformation.VirtualScreen.Contains(rect)) throw new InvalidOperationException("Detection area is outside the visible desktop.");
        var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        try { using var g = Graphics.FromImage(bitmap); g.CopyFromScreen(rect.Location, Point.Empty, rect.Size); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }

    public static double? BarPercent(Bitmap image, bool hp)
    {
        if (image.Width < 3 || image.Height < 3) return null;
        int first = -1, last = -1, filled = 0;
        for (int x = 0; x < image.Width; x++)
        {
            int matches = 0;
            for (int y = 0; y < image.Height; y++)
            {
                var c = image.GetPixel(x, y);
                bool color = hp ? c.R >= c.G + 12 && c.R >= c.B + 12 && (c.GetHue() <= 22 || c.GetHue() >= 338)
                    : c.B >= c.R + 10 && c.B >= c.G + 5 && c.GetHue() >= 185 && c.GetHue() <= 255;
                if (color && c.GetSaturation() >= 0.18f && c.GetBrightness() >= 0.06f) matches++;
            }
            if (matches >= Math.Max(2, image.Height / 5))
            { if (first < 0) first = x; last = x; filled++; }
        }
        // Empty/obscured/unrecognizable captures are unknown, never a false 0% emergency.
        if (first < 0 || first > Math.Max(5, image.Width / 12) || filled < Math.Max(2, (last + 1) * 0.45)) return null;
        return Math.Clamp(100d * (last + 1) / image.Width, 0, 100);
    }

    private async Task<string> ReadTextAsync(Bitmap bitmap)
    {
        int scale = bitmap.Width * 2 <= OcrEngine.MaxImageDimension && bitmap.Height * 2 <= OcrEngine.MaxImageDimension ? 2 : 1;
        using var enlarged = new Bitmap(bitmap, new Size(bitmap.Width * scale, bitmap.Height * scale));
        using var stream = new MemoryStream();
        enlarged.Save(stream, ImageFormat.Bmp);
        using var ras = new InMemoryRandomAccessStream();
        await ras.WriteAsync(stream.ToArray().AsBuffer());
        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return (await engine!.RecognizeAsync(software)).Text;
    }
}
