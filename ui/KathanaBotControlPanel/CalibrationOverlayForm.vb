Imports System.Drawing.Drawing2D

Public Class CalibrationOverlayForm
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
    Private _dragStart As System.Drawing.Point
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

        Dim clientRect As System.Drawing.Rectangle
        If BotEngine.TryGetClientScreenRect(cfg.WindowTitle, clientRect) Then
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

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        DrawRegion(e.Graphics, "hp_bar", _currentConfig.HpBar, Color.FromArgb(170, 220, 70, 70), "HP")
        DrawRegion(e.Graphics, "mp_bar", _currentConfig.MpBar, Color.FromArgb(170, 70, 130, 240), "MP")
        DrawRegion(e.Graphics, "mob_name_rect", _currentConfig.MobNameRect, Color.FromArgb(170, 250, 230, 80), "Mob Name")
        DrawRegion(e.Graphics, "mob_hp_rect", _currentConfig.MobHpRect, Color.FromArgb(170, 255, 140, 60), "Mob HP")
        DrawRegion(e.Graphics, "unreachable_text_rect", _currentConfig.UnreachableTextRect, Color.FromArgb(170, 255, 90, 190), "Unreachable Text")
        DrawRegion(e.Graphics, "prana_exp_rect", _currentConfig.PranaExpRect, Color.FromArgb(170, 160, 220, 90), "Prana/EXP")
        DrawRegion(e.Graphics, "party_invite_scan_rect", _currentConfig.PartyInviteScanRect, Color.FromArgb(170, 180, 120, 240), "Party Scan")
        DrawRegion(e.Graphics, "party_invite_ok_rect", _currentConfig.PartyInviteOkRect, Color.FromArgb(170, 120, 220, 160), "Party OK")

        Dim tipRect As New Rectangle(8, 8, 520, 20)
        Using b As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            e.Graphics.FillRectangle(b, tipRect)
        End Using
        Dim tip As String = "Drag inside box to move. Drag white square to resize. Selected: " & _selectedRegion
        TextRenderer.DrawText(e.Graphics, tip, Font, tipRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
    End Sub

    Private Sub DrawRegion(g As Graphics, key As String, region As RectRegion, colorFill As Color, label As String)
        If region Is Nothing Then
            Return
        End If

        Dim rect As System.Drawing.Rectangle = region.Clamp(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height))
        Using b As New SolidBrush(colorFill)
            g.FillRectangle(b, rect)
        End Using

        Dim selected As Boolean = String.Equals(key, _selectedRegion, StringComparison.OrdinalIgnoreCase)
        Dim borderColor As Color = If(selected, Color.White, Color.FromArgb(235, colorFill.R, colorFill.G, colorFill.B))
        Dim borderWidth As Single = If(selected, 2.8F, 2.0F)
        Using p As New Pen(borderColor, borderWidth)
            g.DrawRectangle(p, rect)
        End Using

        Using textBack As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            Dim labelRect As New System.Drawing.Rectangle(rect.X, Math.Max(0, rect.Y - 18), Math.Min(140, Math.Max(80, rect.Width)), 18)
            g.FillRectangle(textBack, labelRect)
            TextRenderer.DrawText(g, label, Font, labelRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
        End Using

        If selected Then
            Dim h As Rectangle = GetResizeHandleRect(rect)
            Using hb As New SolidBrush(Color.White)
                g.FillRectangle(hb, h)
            End Using
            Using hp As New Pen(Color.Black, 1.0F)
                g.DrawRectangle(hp, h)
            End Using
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left OrElse _currentConfig Is Nothing Then
            Return
        End If

        Dim hitKey As String = HitTestRegion(e.Location)
        If String.IsNullOrWhiteSpace(hitKey) Then
            Return
        End If

        _selectedRegion = hitKey
        Dim selectedRect As System.Drawing.Rectangle = GetRegionRect(hitKey)
        If GetResizeHandleRect(selectedRect).Contains(e.Location) Then
            _dragMode = DragMode.ResizeBottomRight
        ElseIf selectedRect.Contains(e.Location) Then
            _dragMode = DragMode.Move
        Else
            _dragMode = DragMode.None
        End If

        If _dragMode <> DragMode.None Then
            _dragStart = e.Location
            _dragOriginal = CloneRegion(GetRegionByKey(hitKey))
            _isDragging = True
            Capture = True
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _currentConfig Is Nothing Then
            Return
        End If

        If _isDragging AndAlso _dragMode <> DragMode.None Then
            Dim dx As Integer = e.X - _dragStart.X
            Dim dy As Integer = e.Y - _dragStart.Y

            Dim edited As RectRegion = CloneRegion(_dragOriginal)
            If _dragMode = DragMode.Move Then
                edited.X += dx
                edited.Y += dy
            ElseIf _dragMode = DragMode.ResizeBottomRight Then
                edited.W += dx
                edited.H += dy
            End If

            ClampRegionToClient(edited)
            SetRegionByKey(_selectedRegion, edited)
            RaiseEvent OverlayRegionChanged(_selectedRegion, CloneRegion(edited))
            Invalidate()
            Return
        End If

        Dim hoverKey As String = HitTestRegion(e.Location)
        If String.IsNullOrWhiteSpace(hoverKey) Then
            Cursor = Cursors.Default
            Return
        End If

        Dim rect As System.Drawing.Rectangle = GetRegionRect(hoverKey)
        If GetResizeHandleRect(rect).Contains(e.Location) Then
            Cursor = Cursors.SizeNWSE
        ElseIf rect.Contains(e.Location) Then
            Cursor = Cursors.SizeAll
        Else
            Cursor = Cursors.Default
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not _isDragging Then
            Return
        End If

        Capture = False
        _isDragging = False
        _dragMode = DragMode.None

        Dim region As RectRegion = GetRegionByKey(_selectedRegion)
        If region IsNot Nothing Then
            RaiseEvent OverlayRegionCommitted(_selectedRegion, CloneRegion(region))
        End If
        Invalidate()
    End Sub

    Private Function HitTestRegion(pt As System.Drawing.Point) As String
        Dim keys As String() = {"party_invite_ok_rect", "party_invite_scan_rect", "prana_exp_rect", "unreachable_text_rect", "mob_hp_rect", "mob_name_rect", "mp_bar", "hp_bar"}
        For Each key In keys
            Dim rect As System.Drawing.Rectangle = GetRegionRect(key)
            If GetResizeHandleRect(rect).Contains(pt) OrElse rect.Contains(pt) Then
                Return key
            End If
        Next
        Return ""
    End Function

    Private Function GetRegionRect(regionKey As String) As System.Drawing.Rectangle
        Dim region As RectRegion = GetRegionByKey(regionKey)
        If region Is Nothing Then
            Return System.Drawing.Rectangle.Empty
        End If
        Return region.Clamp(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height))
    End Function

    Private Function GetResizeHandleRect(rect As System.Drawing.Rectangle) As System.Drawing.Rectangle
        Return New System.Drawing.Rectangle(rect.Right - HandleSize, rect.Bottom - HandleSize, HandleSize, HandleSize)
    End Function

    Private Function GetRegionByKey(regionKey As String) As RectRegion
        If _currentConfig Is Nothing Then
            Return Nothing
        End If

        Select Case regionKey.ToLowerInvariant()
            Case "hp_bar"
                Return _currentConfig.HpBar
            Case "mp_bar"
                Return _currentConfig.MpBar
            Case "mob_name_rect"
                Return _currentConfig.MobNameRect
            Case "mob_hp_rect"
                Return _currentConfig.MobHpRect
            Case "unreachable_text_rect"
                Return _currentConfig.UnreachableTextRect
            Case "prana_exp_rect"
                Return _currentConfig.PranaExpRect
            Case "party_invite_scan_rect"
                Return _currentConfig.PartyInviteScanRect
            Case "party_invite_ok_rect"
                Return _currentConfig.PartyInviteOkRect
            Case Else
                Return Nothing
        End Select
    End Function

    Private Sub SetRegionByKey(regionKey As String, value As RectRegion)
        Select Case regionKey.ToLowerInvariant()
            Case "hp_bar"
                _currentConfig.HpBar = value
            Case "mp_bar"
                _currentConfig.MpBar = value
            Case "mob_name_rect"
                _currentConfig.MobNameRect = value
            Case "mob_hp_rect"
                _currentConfig.MobHpRect = value
            Case "unreachable_text_rect"
                _currentConfig.UnreachableTextRect = value
            Case "prana_exp_rect"
                _currentConfig.PranaExpRect = value
            Case "party_invite_scan_rect"
                _currentConfig.PartyInviteScanRect = value
            Case "party_invite_ok_rect"
                _currentConfig.PartyInviteOkRect = value
        End Select
    End Sub

    Private Sub ClampRegionToClient(region As RectRegion)
        region.W = Math.Max(MinRegionSize, region.W)
        region.H = Math.Max(MinRegionSize, region.H)
        region.X = Math.Max(0, Math.Min(ClientSize.Width - region.W, region.X))
        region.Y = Math.Max(0, Math.Min(ClientSize.Height - region.H, region.Y))
    End Sub

    Private Function CloneConfig(src As BotConfig) As BotConfig
        Dim cfg As New BotConfig()
        cfg.WindowTitle = src.WindowTitle
        cfg.HpBar = CloneRegion(src.HpBar)
        cfg.MpBar = CloneRegion(src.MpBar)
        cfg.MobNameRect = CloneRegion(src.MobNameRect)
        cfg.MobHpRect = CloneRegion(src.MobHpRect)
        cfg.UnreachableTextRect = CloneRegion(src.UnreachableTextRect)
        cfg.PranaExpRect = CloneRegion(src.PranaExpRect)
        cfg.PartyInviteScanRect = CloneRegion(src.PartyInviteScanRect)
        cfg.PartyInviteOkRect = CloneRegion(src.PartyInviteOkRect)
        Return cfg
    End Function

    Private Function CloneRegion(src As RectRegion) As RectRegion
        If src Is Nothing Then
            Return New RectRegion(0, 0, 1, 1)
        End If
        Return New RectRegion(src.X, src.Y, src.W, src.H)
    End Function

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _timer.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
