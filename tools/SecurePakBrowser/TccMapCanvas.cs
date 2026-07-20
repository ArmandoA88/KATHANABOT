using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KathanaSecurePakBrowser;

internal enum TccMapViewMode
{
    Flags,
    MapValue
}

internal sealed class TccMapCellMouseEventArgs(int x, int y, MouseButtons button) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public MouseButtons Button { get; } = button;
}

internal sealed class TccMapCanvas : Control
{
    private TccMapDocument? document;
    private Bitmap? mapImage;
    private int zoom = 1;
    private int selectedX = -1;
    private int selectedY = -1;
    private TccMapViewMode viewMode;
    private ushort maximumMapValue = 1;

    public TccMapCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Cross;
    }

    public event EventHandler<TccMapCellMouseEventArgs>? CellMouseDown;
    public event EventHandler<TccMapCellMouseEventArgs>? CellMouseMove;
    public event EventHandler<TccMapCellMouseEventArgs>? CellMouseUp;

    public int Zoom => zoom;

    public void LoadDocument(TccMapDocument value)
    {
        document = value;
        maximumMapValue = FindMaximumMapValue(value);
        BuildImage();
        UpdateControlSize();
    }

    public void SetViewMode(TccMapViewMode mode)
    {
        if (viewMode == mode) return;
        viewMode = mode;
        if (document is not null) BuildImage();
    }

    public void SetZoom(int value)
    {
        zoom = Math.Clamp(value, 1, 16);
        UpdateControlSize();
        Invalidate();
    }

    public void SelectCell(int x, int y)
    {
        if (document is null || (uint)x >= document.Width || (uint)y >= document.Height) return;
        Rectangle oldSelection = GetCellRectangle(selectedX, selectedY);
        selectedX = x;
        selectedY = y;
        if (!oldSelection.IsEmpty) Invalidate(Rectangle.Inflate(oldSelection, 2, 2));
        Invalidate(Rectangle.Inflate(GetCellRectangle(x, y), 2, 2));
    }

    public void RefreshCell(int x, int y)
    {
        if (document is null || mapImage is null) return;
        TccMapCell cell = document.GetCell(x, y);
        mapImage.SetPixel(x, y, GetCellColor(cell));
        Invalidate(Rectangle.Inflate(GetCellRectangle(x, y), 1, 1));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Color.FromArgb(24, 24, 24));
        if (document is null || mapImage is null) return;

        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(mapImage, new Rectangle(0, 0, Width, Height),
            0, 0, mapImage.Width, mapImage.Height, GraphicsUnit.Pixel);

        if (zoom >= 8)
        {
            using Pen gridPen = new(Color.FromArgb(55, Color.White), 1);
            int firstX = Math.Max(0, e.ClipRectangle.Left / zoom);
            int lastX = Math.Min(document.Width, e.ClipRectangle.Right / zoom + 1);
            int firstY = Math.Max(0, e.ClipRectangle.Top / zoom);
            int lastY = Math.Min(document.Height, e.ClipRectangle.Bottom / zoom + 1);
            for (int x = firstX; x <= lastX; x++)
            {
                int pixel = x * zoom;
                e.Graphics.DrawLine(gridPen, pixel, firstY * zoom, pixel, lastY * zoom);
            }
            for (int y = firstY; y <= lastY; y++)
            {
                int pixel = y * zoom;
                e.Graphics.DrawLine(gridPen, firstX * zoom, pixel, lastX * zoom, pixel);
            }
        }

        if (selectedX >= 0 && selectedY >= 0)
        {
            Rectangle selection = GetCellRectangle(selectedX, selectedY);
            using Pen dark = new(Color.Black, Math.Max(1, zoom >= 4 ? 3 : 1));
            using Pen bright = new(Color.White, Math.Max(1, zoom >= 4 ? 1 : 1));
            e.Graphics.DrawRectangle(dark, selection);
            Rectangle inner = Rectangle.Inflate(selection, -1, -1);
            if (inner.Width > 0 && inner.Height > 0) e.Graphics.DrawRectangle(bright, inner);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Capture = true;
        if (TryGetCell(e.Location, out int x, out int y))
            CellMouseDown?.Invoke(this, new TccMapCellMouseEventArgs(x, y, e.Button));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (TryGetCell(e.Location, out int x, out int y))
            CellMouseMove?.Invoke(this, new TccMapCellMouseEventArgs(x, y, e.Button));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Capture = false;
        if (document is null) return;
        int x = Math.Clamp(e.X / zoom, 0, document.Width - 1);
        int y = Math.Clamp(e.Y / zoom, 0, document.Height - 1);
        CellMouseUp?.Invoke(this, new TccMapCellMouseEventArgs(x, y, e.Button));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) mapImage?.Dispose();
        base.Dispose(disposing);
    }

    private void BuildImage()
    {
        mapImage?.Dispose();
        mapImage = new Bitmap(document!.Width, document.Height, PixelFormat.Format32bppArgb);
        int[] colors = new int[document.CellCount];
        for (int y = 0; y < document.Height; y++)
        {
            for (int x = 0; x < document.Width; x++)
            {
                colors[y * document.Width + x] = GetCellColor(document.GetCell(x, y)).ToArgb();
            }
        }

        BitmapData data = mapImage.LockBits(new Rectangle(0, 0, mapImage.Width, mapImage.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(colors, 0, data.Scan0, colors.Length);
        }
        finally
        {
            mapImage.UnlockBits(data);
        }
        Invalidate();
    }

    private Color GetCellColor(TccMapCell cell)
    {
        if (viewMode == TccMapViewMode.MapValue)
        {
            if (cell.MapValue == 0) return Color.FromArgb(22, 25, 31);
            double ratio = Math.Clamp((double)cell.MapValue / maximumMapValue, 0, 1);
            int red = (int)(35 + ratio * 220);
            int green = (int)(75 + ratio * 170);
            int blue = (int)(190 - ratio * 150);
            return Color.FromArgb(red, green, blue);
        }

        return cell.Flags switch
        {
            0x0000 => Color.FromArgb(70, 130, 180),
            0x0010 => Color.FromArgb(35, 39, 46),
            0x4000 => Color.FromArgb(65, 165, 90),
            0x4010 => Color.FromArgb(230, 145, 45),
            _ => Color.Magenta
        };
    }

    private static ushort FindMaximumMapValue(TccMapDocument document)
    {
        ushort maximum = 1;
        for (int y = 0; y < document.Height; y++)
        for (int x = 0; x < document.Width; x++)
            maximum = Math.Max(maximum, document.GetCell(x, y).MapValue);
        return maximum;
    }

    private bool TryGetCell(Point location, out int x, out int y)
    {
        x = location.X / zoom;
        y = location.Y / zoom;
        return document is not null && (uint)x < document.Width && (uint)y < document.Height;
    }

    private Rectangle GetCellRectangle(int x, int y) =>
        x < 0 || y < 0 ? Rectangle.Empty : new Rectangle(x * zoom, y * zoom, zoom, zoom);

    private void UpdateControlSize()
    {
        if (document is null) return;
        Size = new Size(checked(document.Width * zoom), checked(document.Height * zoom));
    }
}
