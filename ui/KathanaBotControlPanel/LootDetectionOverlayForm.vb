Imports System.Drawing.Drawing2D

Friend Class LootDetectionOverlayForm
    Inherits Form

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const WS_EX_TRANSPARENT As Integer = &H20
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const DisplayMilliseconds As Integer = 1600

    Private ReadOnly _windowProvider As Func(Of IntPtr)
    Private ReadOnly _timer As New Timer() With {.Interval = 50}
    Private _detection As LootGridDetection
    Private _expiresAtUtc As DateTime = DateTime.MinValue

    Public Sub New(windowProvider As Func(Of IntPtr))
        _windowProvider = windowProvider
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        BackColor = Color.Magenta
        TransparencyKey = Color.Magenta
        DoubleBuffered = True
        AddHandler _timer.Tick, AddressOf TimerTick
        _timer.Start()
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW Or WS_EX_LAYERED Or WS_EX_TRANSPARENT Or WS_EX_NOACTIVATE
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            ' Keep the overlay visible to desktop/display capture software so loot detections
            ' appear in recorded videos. The click-through and no-activate styles remain active.
            NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_NONE)
        Catch
        End Try
    End Sub

    Public Sub ShowDetection(detection As LootGridDetection)
        If detection Is Nothing Then
            Return
        End If

        _detection = New LootGridDetection With {
            .ItemName = If(detection.ItemName, ""),
            .Cell = detection.Cell,
            .ClickPoint = detection.ClickPoint,
            .ClickSucceeded = detection.ClickSucceeded,
            .DetectedAtUtc = detection.DetectedAtUtc
        }
        _expiresAtUtc = DateTime.UtcNow.AddMilliseconds(DisplayMilliseconds)
        RefreshTargetBounds()
        If Not Visible Then
            Show()
        End If
        Invalidate()
    End Sub

    Private Sub TimerTick(sender As Object, e As EventArgs)
        If _detection Is Nothing OrElse DateTime.UtcNow >= _expiresAtUtc Then
            Hide()
            Return
        End If
        If Not RefreshTargetBounds() Then
            Hide()
        End If
    End Sub

    Private Function RefreshTargetBounds() As Boolean
        If _windowProvider Is Nothing Then
            Return False
        End If
        Dim hwnd As IntPtr = _windowProvider.Invoke()
        If hwnd = IntPtr.Zero OrElse Not NativeMethods.IsWindowVisible(hwnd) OrElse NativeMethods.IsIconic(hwnd) Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        Dim origin As New NativeMethods.POINT With {.X = 0, .Y = 0}
        If Not NativeMethods.GetClientRect(hwnd, clientRect) OrElse Not NativeMethods.ClientToScreen(hwnd, origin) Then
            Return False
        End If
        Dim width As Integer = clientRect.Right - clientRect.Left
        Dim height As Integer = clientRect.Bottom - clientRect.Top
        If width <= 0 OrElse height <= 0 Then
            Return False
        End If

        Dim targetBounds As New Rectangle(origin.X, origin.Y, width, height)
        If Bounds <> targetBounds Then
            Bounds = targetBounds
        End If
        Return True
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _detection Is Nothing Then
            Return
        End If

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim accent As Color = If(_detection.ClickSucceeded, Color.FromArgb(255, 70, 245, 160), Color.FromArgb(255, 255, 105, 105))
        Dim cell As Rectangle = _detection.Cell
        cell.Intersect(ClientRectangle)
        If cell.Width > 0 AndAlso cell.Height > 0 Then
            Using fill As New SolidBrush(Color.FromArgb(48, accent)), border As New Pen(accent, 4.0F)
                e.Graphics.FillRectangle(fill, cell)
                e.Graphics.DrawRectangle(border, cell)
            End Using
        End If

        Dim point As System.Drawing.Point = _detection.ClickPoint
        Using markerPen As New Pen(Color.White, 3.0F), accentPen As New Pen(accent, 5.0F)
            e.Graphics.DrawEllipse(accentPen, point.X - 13, point.Y - 13, 26, 26)
            e.Graphics.DrawLine(markerPen, point.X - 20, point.Y, point.X + 20, point.Y)
            e.Graphics.DrawLine(markerPen, point.X, point.Y - 20, point.X, point.Y + 20)
        End Using

        Dim label As String = If(_detection.ClickSucceeded, "LOOT CLICK: ", "LOOT DETECTED: ") & If(String.IsNullOrWhiteSpace(_detection.ItemName), "Unknown item", _detection.ItemName.Trim())
        Using labelFont As New Font("Segoe UI", 10.0F, FontStyle.Bold), panel As New SolidBrush(Color.FromArgb(225, 5, 12, 22)), textBrush As New SolidBrush(accent), outline As New Pen(Color.White, 1.0F)
            Dim measured As SizeF = e.Graphics.MeasureString(label, labelFont)
            Dim labelWidth As Integer = Math.Min(ClientSize.Width, CInt(Math.Ceiling(measured.Width)) + 18)
            Dim labelHeight As Integer = CInt(Math.Ceiling(measured.Height)) + 10
            Dim labelX As Integer = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Width - labelWidth), cell.Left))
            Dim labelY As Integer = Math.Max(0, cell.Top - labelHeight - 5)
            If labelY = 0 AndAlso cell.Top < labelHeight + 5 Then
                labelY = Math.Min(Math.Max(0, ClientSize.Height - labelHeight), cell.Bottom + 5)
            End If
            Dim labelRect As New Rectangle(labelX, labelY, labelWidth, labelHeight)
            e.Graphics.FillRectangle(panel, labelRect)
            e.Graphics.DrawRectangle(outline, labelRect)
            e.Graphics.DrawString(label, labelFont, textBrush, labelRect.X + 8, labelRect.Y + 5)
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _timer.Stop()
            RemoveHandler _timer.Tick, AddressOf TimerTick
            _timer.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
