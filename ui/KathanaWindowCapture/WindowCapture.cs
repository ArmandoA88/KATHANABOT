using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace KathanaWindowCapture;

public sealed record CaptureStatus(bool Ready, long Frames, long AgeMs, string Note);

/// <summary>One owned latest frame. Readers receive copies; old, resized and foreign frames are rejected.</summary>
public sealed class LatestWindowFrame : IDisposable
{
    private readonly object gate = new();
    private Bitmap? frame;
    private long capturedAt;
    private long frames;
    private bool disposed;

    public void Publish(Bitmap value, long atMs)
    {
        lock (gate)
        {
            if (disposed) { value.Dispose(); return; }
            frame?.Dispose();
            frame = value;
            capturedAt = atMs;
            frames++;
        }
    }

    public Bitmap? Copy(int width, int height, long nowMs, int maximumAgeMs, out long atMs)
    {
        lock (gate)
        {
            atMs = 0;
            if (disposed || frame is null || frame.Width != width || frame.Height != height ||
                capturedAt > nowMs || nowMs - capturedAt > maximumAgeMs) return null;
            atMs = capturedAt;
            return (Bitmap)frame.Clone();
        }
    }

    public CaptureStatus Status(long nowMs, int maximumAgeMs, string note)
    {
        lock (gate)
        {
            var age = frame is null ? long.MaxValue : Math.Max(0, nowMs - capturedAt);
            return new(!disposed && frame is not null && age <= maximumAgeMs, frames, age, note);
        }
    }

    public void Clear()
    {
        lock (gate) { frame?.Dispose(); frame = null; }
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; frame?.Dispose(); frame = null; }
    }
}

/// <summary>Continuous WGC capture of an exact HWND/PID. Never captures the desktop or changes focus.</summary>
public sealed class WindowCapture : IDisposable
{
    private readonly object lifetimeGate = new();
    private readonly nint hwnd;
    private readonly uint pid;
    private readonly bool rejectBlackFrames;
    private readonly LatestWindowFrame latest = new();
    private IDirect3DDevice? device;
    private GraphicsCaptureItem? item;
    private Direct3D11CaptureFramePool? pool;
    private GraphicsCaptureSession? session;
    private volatile bool disposed;
    private int processing;
    private long lastCopyMs;
    private int poolWidth;
    private int poolHeight;
    private string note = "Starting window capture";
    private readonly Task initialization;

    public WindowCapture(nint window, uint processId, bool rejectBlackFrames = false)
    {
        hwnd = window;
        pid = processId;
        this.rejectBlackFrames = rejectBlackFrames;
        initialization = Task.Run(Initialize);
    }

    public bool Failed => initialization.IsCompleted && pool is null;

    private void Initialize()
    {
        lock (lifetimeGate)
        {
            if (disposed) return;
            try
            {
                if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows window capture is unavailable");
                if (!MatchesTarget()) throw new InvalidOperationException("Game window is closed or minimized");
                device = Native.CreateDevice();
                item = Native.CreateItem(hwnd);
                item.Closed += OnClosed;
                pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
                poolWidth = item.Size.Width;
                poolHeight = item.Size.Height;
                pool.FrameArrived += OnFrame;
                session = pool.CreateCaptureSession(item);
                session.IsCursorCaptureEnabled = false;
                session.StartCapture();
                Volatile.Write(ref note, "Waiting for a game frame");
            }
            catch (Exception ex)
            {
                Volatile.Write(ref note, "Capture unavailable: " + ex.Message);
                CloseNative();
            }
        }
    }

    private bool MatchesTarget()
    {
        Native.GetWindowThreadProcessId(hwnd, out var actualPid);
        return !disposed && hwnd != 0 && actualPid == pid && Native.IsWindow(hwnd) && !Native.IsIconic(hwnd);
    }

    public Bitmap? Copy(int width, int height, int maximumAgeMs, out long capturedAtMs)
    {
        capturedAtMs = 0;
        if (!MatchesTarget()) { latest.Clear(); return null; }
        return latest.Copy(width, height, Environment.TickCount64, maximumAgeMs, out capturedAtMs);
    }

    public CaptureStatus Status(int maximumAgeMs)
    {
        if (!MatchesTarget()) return new(false, 0, long.MaxValue, "Game window is closed or minimized");
        return latest.Status(Environment.TickCount64, maximumAgeMs, Volatile.Read(ref note));
    }

    private void OnClosed(GraphicsCaptureItem sender, object args)
    {
        latest.Clear();
        Dispose();
    }

    private async void OnFrame(Direct3D11CaptureFramePool sender, object args)
    {
        // One GPU readback at a time, at most ten per second. There is no work/frame backlog.
        if (disposed || Interlocked.CompareExchange(ref processing, 1, 0) != 0) return;
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null || disposed) return;
            var size = frame.ContentSize;
            if (!MatchesTarget()) { latest.Clear(); return; }
            if (size.Width != poolWidth || size.Height != poolHeight)
            {
                latest.Clear();
                lock (lifetimeGate)
                {
                    if (!disposed && device is not null && size.Width > 0 && size.Height > 0)
                    {
                        sender.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
                        poolWidth = size.Width;
                        poolHeight = size.Height;
                    }
                }
                return;
            }
            if (Native.DwmGetWindowAttribute(hwnd, 9, out var bounds, Marshal.SizeOf<Native.Rect>()) != 0)
                throw new InvalidOperationException("Cannot locate captured window bounds");
            if (size.Width != bounds.Right - bounds.Left || size.Height != bounds.Bottom - bounds.Top)
            {
                latest.Clear();
                return;
            }
            var now = Environment.TickCount64;
            if (now - lastCopyMs < 100) return;
            lastCopyMs = now;
            // Use the source's QPC timestamp, not completion time, so a slow readback cannot look fresh.
            var sourceAgeMs = Math.Max(0, Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency - frame.SystemRelativeTime.TotalMilliseconds);
            var capturedAt = now - (long)sourceAgeMs;
            if (!Native.GetClientRect(hwnd, out var client)) return;
            var origin = new Native.Point();
            if (!Native.ClientToScreen(hwnd, ref origin)) return;
            var crop = new Rectangle(origin.X - bounds.Left, origin.Y - bounds.Top, client.Right, client.Bottom);
            if (crop.Width <= 0 || crop.Height <= 0 || crop.Left < 0 || crop.Top < 0 || crop.Right > size.Width || crop.Bottom > size.Height) return;
            using var software = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Ignore);
            using var bgra = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            if (crop.Right > bgra.PixelWidth || crop.Bottom > bgra.PixelHeight) return;
            if (disposed || !MatchesTarget()) return;
            // A move/resize during readback invalidates the crop instead of publishing misaligned pixels.
            if (!Native.GetClientRect(hwnd, out var currentClient) || currentClient.Right != crop.Width || currentClient.Bottom != crop.Height) return;
            var currentOrigin = new Native.Point();
            if (!Native.ClientToScreen(hwnd, ref currentOrigin) || currentOrigin.X != origin.X || currentOrigin.Y != origin.Y) return;
            var bitmap = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
            try
            {
                Native.CopyPixels(bgra, crop, bitmap);
                if (rejectBlackFrames && !IsUsable(bitmap))
                {
                    latest.Clear();
                    Volatile.Write(ref note, "Waiting for usable game pixels");
                    return;
                }
                latest.Publish(bitmap, capturedAt);
                bitmap = null!; // the cache now owns this bitmap
                Volatile.Write(ref note, "Continuous game-window capture");
            }
            finally { bitmap?.Dispose(); }
        }
        catch (Exception ex)
        {
            latest.Clear();
            if (!disposed) Volatile.Write(ref note, "Capture unavailable: " + ex.Message);
        }
        finally { Interlocked.Exchange(ref processing, 0); }
    }

    private static bool IsUsable(Bitmap bitmap)
    {
        var samples = 0;
        var dark = 0;
        long sum = 0;
        for (var y = 0; y < bitmap.Height; y += Math.Max(1, bitmap.Height / 10))
        for (var x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 10))
        {
            var pixel = bitmap.GetPixel(x, y);
            var luma = (pixel.R * 30 + pixel.G * 59 + pixel.B * 11) / 100;
            samples++;
            sum += luma;
            if (luma <= 8) dark++;
        }
        return samples > 0 && !(dark / (double)samples >= 0.96 && sum / (double)samples <= 10.0);
    }

    private void CloseNative()
    {
        if (item is not null) item.Closed -= OnClosed;
        if (pool is not null) pool.FrameArrived -= OnFrame;
        session?.Dispose();
        pool?.Dispose();
        device?.Dispose();
        session = null;
        pool = null;
        device = null;
        item = null;
    }

    public void Dispose()
    {
        lock (lifetimeGate)
        {
            if (disposed) return;
            disposed = true;
        }
        latest.Dispose();
        // Native shutdown can wait for callbacks; never hold the lock those callbacks use for resize.
        CloseNative();
    }
}

internal static class Native
{
    [ComImport, Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        void GetBuffer(out nint data, out uint capacity);
    }

    internal static void CopyPixels(SoftwareBitmap source, Rectangle crop, Bitmap destination)
    {
        using var buffer = source.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        var plane = buffer.GetPlaneDescription(0);
        var abi = WinRT.MarshalInterface<Windows.Foundation.IMemoryBufferReference>.FromManaged(reference);
        nint access = 0;
        object? interop = null;
        try
        {
            var iid = typeof(IMemoryBufferByteAccess).GUID;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(abi, in iid, out access));
            interop = Marshal.GetObjectForIUnknown(access);
            ((IMemoryBufferByteAccess)interop).GetBuffer(out var pixels, out var capacity);
            var rowBytes = checked(crop.Width * 4);
            var lastOffset = checked(plane.StartIndex + (crop.Bottom - 1) * plane.Stride + crop.Left * 4);
            if (pixels == 0 || plane.Stride <= 0 || lastOffset < 0 || (long)lastOffset + rowBytes > capacity)
                throw new InvalidOperationException("Captured pixel buffer has invalid bounds");
            var row = new byte[rowBytes];
            var data = destination.LockBits(new Rectangle(0, 0, destination.Width, destination.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < crop.Height; y++)
                {
                    Marshal.Copy(pixels + plane.StartIndex + (crop.Top + y) * plane.Stride + crop.Left * 4, row, 0, rowBytes);
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, rowBytes);
                }
            }
            finally { destination.UnlockBits(data); }
        }
        finally
        {
            if (interop is not null) Marshal.ReleaseComObject(interop);
            if (access != 0) Marshal.Release(access);
            if (abi != 0) Marshal.Release(abi);
        }
    }

    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out Rect rect, int size);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software,
        uint flags, nint levels, uint levelCount, uint sdkVersion, out nint device, out int level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi, out nint device);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out nint hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint hstring);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name, in Guid iid, out nint factory);

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint window, in Guid iid, out nint item);
        [PreserveSig] int CreateForMonitor(nint monitor, in Guid iid, out nint item);
    }

    internal static GraphicsCaptureItem CreateItem(nint window)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(name, name.Length, out var hstring));
        nint factory = 0, item = 0;
        object? interop = null;
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, typeof(IGraphicsCaptureItemInterop).GUID, out factory));
            interop = Marshal.GetObjectForIUnknown(factory);
            var iid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            Marshal.ThrowExceptionForHR(((IGraphicsCaptureItemInterop)interop).CreateForWindow(window, iid, out item));
            return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
        }
        finally
        {
            if (item != 0) Marshal.Release(item);
            if (interop is not null) Marshal.ReleaseComObject(interop);
            if (factory != 0) Marshal.Release(factory);
            WindowsDeleteString(hstring);
        }
    }

    internal static IDirect3DDevice CreateDevice()
    {
        nint d3d = 0, context = 0, dxgi = 0, projected = 0;
        try
        {
            var result = D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out d3d, out _, out context);
            if (result < 0)
            {
                if (context != 0) { Marshal.Release(context); context = 0; }
                if (d3d != 0) { Marshal.Release(d3d); d3d = 0; }
                result = D3D11CreateDevice(0, 5, 0, 0x20, 0, 0, 7, out d3d, out _, out context);
            }
            Marshal.ThrowExceptionForHR(result);
            var iid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out projected));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(projected);
        }
        finally
        {
            if (projected != 0) Marshal.Release(projected);
            if (dxgi != 0) Marshal.Release(dxgi);
            if (context != 0) Marshal.Release(context);
            if (d3d != 0) Marshal.Release(d3d);
        }
    }
}
