using System.Drawing.Drawing2D;

namespace KathanaBotReloaded;

internal sealed class GameControlOverlay : Form
{
    private readonly ReloadedSettings settings;
    private Rectangle game;
    private Point pointer, origin;
    private Size originalSize;
    private bool dragging, resizing, moved, running;
    private string detail = "", stats = "", targetSummary = "", inputSummary = "";
    public event Action? ToggleRequested;
    public event Action? LayoutSaved;
    public GameControlOverlay(ReloadedSettings settings)
    {
        this.settings = settings;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
        BackColor = Color.FromArgb(9, 15, 26);
        Size = new Size(Math.Clamp(settings.ControlWidth, 220, 360), Math.Clamp(settings.ControlHeight, 116, 180));
        AccessibleName = "RELOADED game controls";
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    protected override void WndProc(ref Message m)
    { if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } base.WndProc(ref m); }
    public void UpdateState(Rectangle client, bool isRunning, double? hp, double? mp, bool repair)
    {
        game = client;
        if (!dragging)
        {
            int x = settings.ControlX < 0 ? client.Width - Width - 10 : settings.ControlX;
            Location = new Point(client.X + Math.Clamp(x, 0, Math.Max(0, client.Width - Width)),
                client.Y + Math.Clamp(settings.ControlY, 0, Math.Max(0, client.Height - Height)));
        }
        string nextStats = $"HP {(hp.HasValue ? $"{hp:0}%" : "--")}   MP {(mp.HasValue ? $"{mp:0}%" : "--")}";
        string nextDetail = isRunning ? (repair ? "Repair ready · Click to stop" : "Click to stop · Drag to move") : "Click to resume · Drag to move";
        if (running != isRunning || stats != nextStats || detail != nextDetail)
        { running = isRunning; stats = nextStats; detail = nextDetail; Invalidate(); }
        AccessibleDescription = $"{(running ? "Running. Stop" : "Paused. Go")}. {stats}. Drag to move; bottom-right corner to resize.";
    }
    public void UpdateTelemetry(string target, string x, string y, long inputs, double rate)
    {
        string nextTarget = $"Target: {target}";
        string nextInputs = $"X {x}  Y {y} | {inputs:N0} inputs | {rate:0.0}/s";
        if (targetSummary == nextTarget && inputSummary == nextInputs) return;
        targetSummary = nextTarget; inputSummary = nextInputs; Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
        dragging = true; moved = false; resizing = e.X >= Width - 14 && e.Y >= Height - 14;
        pointer = MousePosition; origin = Location; originalSize = Size; Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = e.X >= Width - 14 && e.Y >= Height - 14 ? Cursors.SizeNWSE : Cursors.SizeAll;
        if (!dragging) return;
        int dx = MousePosition.X - pointer.X, dy = MousePosition.Y - pointer.Y;
        moved |= Math.Abs(dx) >= 3 || Math.Abs(dy) >= 3;
        if (!moved) return;
        if (resizing) Size = new Size(Math.Clamp(originalSize.Width + dx, 220, 360), Math.Clamp(originalSize.Height + dy, 116, 180));
        else Location = new Point(Math.Clamp(origin.X + dx, game.Left, Math.Max(game.Left, game.Right - Width)), Math.Clamp(origin.Y + dy, game.Top, Math.Max(game.Top, game.Bottom - Height)));
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); if (!dragging || e.Button != MouseButtons.Left) return;
        dragging = false; Capture = false;
        if (moved)
        { settings.ControlX = Left - game.Left; settings.ControlY = Top - game.Top; settings.ControlWidth = Width; settings.ControlHeight = Height; LayoutSaved?.Invoke(); }
        else if (!resizing && ClientRectangle.Contains(e.Location)) ToggleRequested?.Invoke();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        Color accent = running ? Color.FromArgb(45, 224, 157) : Color.FromArgb(248, 113, 113);
        using var fill = new LinearGradientBrush(bounds, Color.FromArgb(17, 29, 48), Color.FromArgb(8, 15, 27), 90f);
        g.FillRectangle(fill, bounds); using var pen = new Pen(accent); g.DrawRectangle(pen, bounds);
        using var bar = new SolidBrush(accent); g.FillRectangle(bar, 9, 8, 4, Height - 16);
        using var heading = new Font("Segoe UI", 8.4f, FontStyle.Bold);
        using var small = new Font("Segoe UI", 7.5f);
        void TextAt(string text, Font font, Rectangle rect, Color color) => TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        TextAt("RELOADED", heading, new Rectangle(21, 8, 77, 18), Color.White);
        TextAt(running ? "RUNNING" : "PAUSED", small, new Rectangle(100, 10, Width - 148, 17), accent);
        TextAt(running ? "STOP" : "GO", heading, new Rectangle(Width - 43, 8, 38, 20), accent);
        var parts = stats.Split("   ");
        TextAt(parts.ElementAtOrDefault(0) ?? "HP --", small, new Rectangle(21, 29, 85, 18), Color.LightCoral);
        TextAt(parts.ElementAtOrDefault(1) ?? "MP --", small, new Rectangle(110, 29, Width - 122, 18), Color.CornflowerBlue);
        TextAt(detail, small, new Rectangle(21, 49, Width - 36, Height - 51), Color.FromArgb(218, 229, 246));
        TextAt(targetSummary, small, new Rectangle(21, 70, Width - 36, 18), Color.Gold);
        TextAt(inputSummary, small, new Rectangle(21, 90, Width - 36, 18), Color.LightSteelBlue);
        g.DrawLine(pen, Width - 10, Height - 4, Width - 4, Height - 10);
        g.DrawLine(pen, Width - 6, Height - 4, Width - 4, Height - 6);
    }
}
