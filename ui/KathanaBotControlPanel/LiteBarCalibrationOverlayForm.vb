Imports System.Drawing.Drawing2D
Imports DrawingPoint = System.Drawing.Point

' Lite owns this overlay and its HP/MP rectangles. The Full calibration overlay is not involved.
Public Class LiteBarCalibrationOverlayForm
    Inherits Form

    Public Event OverlayRegionChanged(regionName As String, region As RectRegion)
    Public Event OverlayRegionCommitted(regionName As String, region As RectRegion)

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const HandleSize As Integer = 10
    Private Const MinRegionSize As Integer = 8

    Private Enum DragMode
        None
        Move
        ResizeBottomRight
    End Enum

    Private ReadOnly _configProvider As Func(Of BotConfig)
    Private ReadOnly _timer As New Timer()
    Private _currentConfig As BotConfig
    Private _selectedRegion As String = "hp_bar"
    Private _dragMode As DragMode = DragMode.None
    Private _dragStart As DrawingPoint
    Private _dragOriginal As RectRegion
    Private _isDragging As Boolean

    Public Sub New(configProvider As Func(Of BotConfig))
        _configProvider = configProvider
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        BackColor = Color.Black
        Opacity = 0.35
        DoubleBuffered = True

        _timer.Interval = 100
        AddHandler _timer.Tick, AddressOf TickUpdate
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
            cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW Or WS_EX_LAYERED
            Return cp
        End Get
    End Property

    Private Sub TickUpdate(sender As Object, e As EventArgs)
        If _configProvider Is Nothing Then
            Return
        End If

        Dim cfg As BotConfig = _configProvider.Invoke()
        If cfg Is Nothing Then
            Return
        End If

        If Not _isDragging Then
            _currentConfig = CloneConfig(cfg)
        End If

        Dim clientRect As Rectangle
        If BotEngine.TryGetClientScreenRect(cfg, clientRect) Then
            If Bounds <> clientRect Then
                Bounds = clientRect
            End If
            If Not Visible Then
                Show()
            End If
            Invalidate()
        Else
            Hide()
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _currentConfig Is Nothing Then
            Return
        End If

        e.Graphics.SmoothingMode = SmoothingMode.None
        DrawRegion(e.Graphics, "hp_bar", _currentConfig.HpBar, Color.FromArgb(170, 220, 70, 70), "Lite HP")
        DrawRegion(e.Graphics, "mp_bar", _currentConfig.MpBar, Color.FromArgb(170, 70, 130, 240), "Lite MP")

        Dim tipRect As New Rectangle(8, 8, Math.Min(620, Math.Max(200, ClientSize.Width - 16)), 20)
        Using background As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            e.Graphics.FillRectangle(background, tipRect)
        End Using
        Dim tip As String = "Lite AutoPots only: drag a bar to move it; drag its white square to resize. Selected: " & _selectedRegion
        TextRenderer.DrawText(e.Graphics, tip, Font, tipRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub

    Private Sub DrawRegion(g As Graphics, key As String, region As RectRegion, fillColor As Color, label As String)
        If region Is Nothing Then
            Return
        End If

        Dim rect As Rectangle = region.Clamp(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height))
        Using fill As New SolidBrush(fillColor)
            g.FillRectangle(fill, rect)
        End Using

        Dim selected As Boolean = String.Equals(key, _selectedRegion, StringComparison.OrdinalIgnoreCase)
        Using border As New Pen(If(selected, Color.White, Color.FromArgb(235, fillColor.R, fillColor.G, fillColor.B)), If(selected, 2.8F, 2.0F))
            g.DrawRectangle(border, rect)
        End Using

        Dim labelRect As New Rectangle(rect.X, Math.Max(0, rect.Y - 18), Math.Min(140, Math.Max(80, rect.Width)), 18)
        Using background As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            g.FillRectangle(background, labelRect)
        End Using
        TextRenderer.DrawText(g, label, Font, labelRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

        If selected Then
            Dim handle As Rectangle = GetResizeHandleRect(rect)
            Using fill As New SolidBrush(Color.White)
                g.FillRectangle(fill, handle)
            End Using
            Using border As New Pen(Color.Black, 1.0F)
                g.DrawRectangle(border, handle)
            End Using
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left OrElse _currentConfig Is Nothing Then
            Return
        End If

        Dim key As String = HitTestRegion(e.Location)
        If key = "" Then
            Return
        End If

        _selectedRegion = key
        Dim rect As Rectangle = GetRegionRect(key)
        _dragMode = If(GetResizeHandleRect(rect).Contains(e.Location), DragMode.ResizeBottomRight, DragMode.Move)
        _dragStart = e.Location
        _dragOriginal = CloneRegion(GetRegion(key))
        _isDragging = True
        Capture = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _currentConfig Is Nothing Then
            Return
        End If

        If _isDragging Then
            Dim edited As RectRegion = CloneRegion(_dragOriginal)
            Dim dx As Integer = e.X - _dragStart.X
            Dim dy As Integer = e.Y - _dragStart.Y
            If _dragMode = DragMode.Move Then
                edited.X += dx
                edited.Y += dy
            Else
                edited.W += dx
                edited.H += dy
            End If
            ClampRegion(edited)
            SetRegion(_selectedRegion, edited)
            RaiseEvent OverlayRegionChanged(_selectedRegion, CloneRegion(edited))
            Invalidate()
            Return
        End If

        Dim key As String = HitTestRegion(e.Location)
        If key = "" Then
            Cursor = Cursors.Default
            Return
        End If
        Dim rect As Rectangle = GetRegionRect(key)
        Cursor = If(GetResizeHandleRect(rect).Contains(e.Location), Cursors.SizeNWSE, Cursors.SizeAll)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not _isDragging Then
            Return
        End If

        Capture = False
        _isDragging = False
        _dragMode = DragMode.None
        Dim region As RectRegion = GetRegion(_selectedRegion)
        If region IsNot Nothing Then
            RaiseEvent OverlayRegionCommitted(_selectedRegion, CloneRegion(region))
        End If
        Invalidate()
    End Sub

    Private Function HitTestRegion(location As DrawingPoint) As String
        For Each key As String In New String() {"mp_bar", "hp_bar"}
            Dim rect As Rectangle = GetRegionRect(key)
            If GetResizeHandleRect(rect).Contains(location) OrElse rect.Contains(location) Then
                Return key
            End If
        Next
        Return ""
    End Function

    Private Function GetRegion(key As String) As RectRegion
        If _currentConfig Is Nothing Then
            Return Nothing
        End If
        Return If(String.Equals(key, "mp_bar", StringComparison.OrdinalIgnoreCase), _currentConfig.MpBar, _currentConfig.HpBar)
    End Function

    Private Sub SetRegion(key As String, region As RectRegion)
        If String.Equals(key, "mp_bar", StringComparison.OrdinalIgnoreCase) Then
            _currentConfig.MpBar = region
        Else
            _currentConfig.HpBar = region
        End If
    End Sub

    Private Function GetRegionRect(key As String) As Rectangle
        Dim region As RectRegion = GetRegion(key)
        If region Is Nothing Then
            Return Rectangle.Empty
        End If
        Return region.Clamp(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height))
    End Function

    Private Shared Function GetResizeHandleRect(rect As Rectangle) As Rectangle
        Return New Rectangle(rect.Right - HandleSize, rect.Bottom - HandleSize, HandleSize, HandleSize)
    End Function

    Private Sub ClampRegion(region As RectRegion)
        region.W = Math.Max(MinRegionSize, Math.Min(Math.Max(MinRegionSize, ClientSize.Width), region.W))
        region.H = Math.Max(MinRegionSize, Math.Min(Math.Max(MinRegionSize, ClientSize.Height), region.H))
        region.X = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Width - region.W), region.X))
        region.Y = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Height - region.H), region.Y))
    End Sub

    Private Shared Function CloneRegion(region As RectRegion) As RectRegion
        If region Is Nothing Then
            Return New RectRegion(0, 0, 1, 1)
        End If
        Return New RectRegion(region.X, region.Y, region.W, region.H)
    End Function

    Private Shared Function CloneConfig(source As BotConfig) As BotConfig
        Return New BotConfig With {
            .WindowTitle = source.WindowTitle,
            .SelectedWindowHandle = source.SelectedWindowHandle,
            .HpBar = CloneRegion(source.HpBar),
            .MpBar = CloneRegion(source.MpBar)
        }
    End Function

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _timer.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
