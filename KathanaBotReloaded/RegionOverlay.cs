namespace KathanaBotReloaded;

internal sealed class RegionOverlay : Form
{
    private readonly bool editing;
    private readonly string label;
    private readonly Color color;
    private Point origin;
    private Rectangle original;
    private int dragMode;
    private Rectangle[] avoid = [];
    public Rectangle Selected { get; private set; }

    public RegionOverlay(Rectangle client, Rectangle selection, string label, Color color, bool editing)
    {
        this.editing = editing; this.label = label; this.color = color;
        Selected = selection;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = client;
        DoubleBuffered = true;
        BackColor = editing ? Color.Black : Color.Magenta;
        if (editing) Opacity = 0.50; else TransparencyKey = Color.Magenta;
        KeyPreview = true;
        if (editing)
        {
            var save = new Button { Text = "Save area (Enter)", Bounds = new Rectangle(Math.Max(0, client.Width - 270), Math.Max(0, client.Height - 42), 135, 30), BackColor = Color.White };
            var cancel = new Button { Text = "Cancel (Esc)", Bounds = new Rectangle(Math.Max(140, client.Width - 125), Math.Max(0, client.Height - 42), 110, 30), BackColor = Color.White };
            save.Click += (_, _) => Commit();
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange([save, cancel]);
        }
    }
    protected override bool ShowWithoutActivation => !editing;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80; if (!editing) p.ExStyle |= 0x20 | 0x08000000; return p; }
    }
    public void SetRegion(Rectangle client, Rectangle selection, Rectangle[] areas)
    {
        if (Bounds == client && Selected == selection && avoid.SequenceEqual(areas)) return;
        if (Bounds != client) Bounds = client;
        Selected = selection; avoid = areas; Invalidate();
    }
    private void Commit() { if (Selected.Width < 4 || Selected.Height < 4) return; DialogResult = DialogResult.OK; Close(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (editing && e.KeyCode == Keys.Enter) Commit();
        if (editing && e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        base.OnKeyDown(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!editing || e.Button != MouseButtons.Left) return;
        origin = e.Location; original = Selected;
        dragMode = new Rectangle(Selected.Right - 12, Selected.Bottom - 12, 18, 18).Contains(e.Location) ? 2 : Selected.Contains(e.Location) ? 1 : 3;
        Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!editing || !Capture) return;
        int dx = e.X - origin.X, dy = e.Y - origin.Y;
        Selected = dragMode switch
        {
            1 => new Rectangle(Math.Clamp(original.X + dx, 0, Math.Max(0, ClientSize.Width - original.Width)), Math.Clamp(original.Y + dy, 0, Math.Max(0, ClientSize.Height - original.Height)), original.Width, original.Height),
            2 => new Rectangle(original.X, original.Y, Math.Max(4, original.Width + dx), Math.Max(4, original.Height + dy)),
            _ => Rectangle.FromLTRB(Math.Min(origin.X, e.X), Math.Min(origin.Y, e.Y), Math.Max(origin.X, e.X), Math.Max(origin.Y, e.Y))
        };
        Selected = Rectangle.Intersect(ClientRectangle, Selected);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Selected.Width > 0 && Selected.Height > 0)
        {
            using var pen = new Pen(color, 2);
            // Outside the crop so the overlay's own red/blue pixels are not measured.
            var outline = Selected; outline.Inflate(2, 2);
            e.Graphics.DrawRectangle(pen, outline);
            using var brush = new SolidBrush(color);
            var candidates = new[] {
                new Rectangle(Selected.Left, Selected.Top - 21, 180, 18),
                new Rectangle(Selected.Left, Selected.Bottom + 4, 180, 18),
                new Rectangle(Selected.Right + 4, Selected.Top, 180, 18)
            };
            var labelArea = candidates.FirstOrDefault(r => ClientRectangle.Contains(r) && (editing || !avoid.Any(a => a.IntersectsWith(r))));
            if (!labelArea.IsEmpty) e.Graphics.DrawString(label, Font, brush, labelArea.Location);
            if (editing) e.Graphics.FillRectangle(brush, Selected.Right - 6, Selected.Bottom - 6, 8, 8);
        }
        if (editing) e.Graphics.DrawString("Drag to draw an area. Drag inside to move; drag the corner to resize.", Font, Brushes.White, 12, Math.Max(0, ClientSize.Height - 65));
    }
}
