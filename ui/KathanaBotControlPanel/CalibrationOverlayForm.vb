Imports System.Drawing.Drawing2D
Imports System.Linq
Imports DrawingPoint = System.Drawing.Point

Public Class CalibrationOverlayForm
    Inherits Form

    Public Event OverlayRegionChanged(regionName As String, region As RectRegion)
    Public Event OverlayRegionCommitted(regionName As String, region As RectRegion)
    Public Event OverlayLootScanAreaChanged(points As List(Of LootScanPoint))
    Public Event OverlayLootScanAreaCommitted(points As List(Of LootScanPoint))

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const HandleSize As Integer = 10
    Private Const MinRegionSize As Integer = 8

    Private Enum DragMode
        None
        Move
        ResizeBottomRight
        MoveLootScanArea
        MoveLootScanVertex
    End Enum

    Private ReadOnly _configProvider As Func(Of BotConfig)
    Private ReadOnly _timer As New Timer()

    Private _currentConfig As BotConfig
    Private _selectedRegion As String = "hp_bar"
    Private _dragMode As DragMode = DragMode.None
    Private _dragStart As System.Drawing.Point
    Private _dragOriginal As RectRegion
    Private _dragLootScanPoints As List(Of LootScanPoint)
    Private _activeLootScanVertexIndex As Integer = -1
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
        If IsLootScanOverlayEnabled() Then
            DrawLootScanArea(e.Graphics, "loot_scan_area", GetLootScanPoints(), Color.FromArgb(80, 70, 255, 255), "Loot Scan")
        End If
        DrawRegion(e.Graphics, "hp_bar", _currentConfig.HpBar, Color.FromArgb(170, 220, 70, 70), "HP")
        DrawRegion(e.Graphics, "mp_bar", _currentConfig.MpBar, Color.FromArgb(170, 70, 130, 240), "MP")
        DrawRegion(e.Graphics, "mob_name_rect", _currentConfig.MobNameRect, Color.FromArgb(170, 250, 230, 80), "Mob Name")
        DrawRegion(e.Graphics, "mob_hp_rect", _currentConfig.MobHpRect, Color.FromArgb(170, 255, 140, 60), "Mob HP")
        DrawRegion(e.Graphics, "mob_life_rect", _currentConfig.MobLifeRect, Color.FromArgb(170, 255, 255, 255), "Mob Life")
        DrawRegion(e.Graphics, "unreachable_text_rect", _currentConfig.UnreachableTextRect, Color.FromArgb(170, 255, 90, 190), "Unreachable Text")
        DrawRegion(e.Graphics, "prana_exp_rect", _currentConfig.PranaExpRect, Color.FromArgb(170, 160, 220, 90), "Prana/EXP")
        DrawRegion(e.Graphics, "rupiahs_rect", _currentConfig.RupiahsRect, Color.FromArgb(170, 255, 215, 90), "Rupiahs")
        DrawRegion(e.Graphics, "party_invite_scan_rect", _currentConfig.PartyInviteScanRect, Color.FromArgb(170, 180, 120, 240), "Party Scan")
        DrawRegion(e.Graphics, "party_invite_ok_rect", _currentConfig.PartyInviteOkRect, Color.FromArgb(170, 120, 220, 160), "Party OK")
        DrawRegion(e.Graphics, "party_list_rect", _currentConfig.PartyListRect, Color.FromArgb(120, 255, 90, 90), "Party List")
        DrawRegion(e.Graphics, "disconnect_message_rect", _currentConfig.DisconnectMessageRect, Color.FromArgb(170, 255, 120, 120), "Disconnect")
        DrawRegion(e.Graphics, "map_coordinate_x_rect", _currentConfig.MapCoordinateXRect, Color.FromArgb(170, 70, 255, 170), "Map X")
        DrawRegion(e.Graphics, "map_coordinate_y_rect", _currentConfig.MapCoordinateYRect, Color.FromArgb(170, 90, 230, 255), "Map Y")
        DrawRegion(e.Graphics, "chat_rect", _currentConfig.ChatRect, Color.FromArgb(170, 255, 200, 110), "Chat")

        Dim tipRect As New Rectangle(8, 8, 520, 20)
        Using b As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            e.Graphics.FillRectangle(b, tipRect)
        End Using
        Dim tip As String = "Rectangles: drag inside to move, white square to resize. Loot Scan: drag polygon or its corner points. Selected: " & _selectedRegion
        TextRenderer.DrawText(e.Graphics, tip, Font, tipRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
    End Sub

    Private Sub DrawLootScanArea(g As Graphics, key As String, points As List(Of DrawingPoint), colorFill As Color, label As String)
        If points Is Nothing OrElse points.Count < 3 Then
            Return
        End If

        Dim selected As Boolean = String.Equals(key, _selectedRegion, StringComparison.OrdinalIgnoreCase)
        Using fillBrush As New SolidBrush(colorFill)
            g.FillPolygon(fillBrush, points.ToArray())
        End Using

        Dim borderColor As Color = If(selected, Color.White, Color.FromArgb(235, colorFill.R, colorFill.G, colorFill.B))
        Dim borderWidth As Single = If(selected, 2.8F, 2.0F)
        Using p As New Pen(borderColor, borderWidth)
            g.DrawPolygon(p, points.ToArray())
        End Using

        Dim bounds As Rectangle = GetLootScanBounds(points)
        Using textBack As New SolidBrush(Color.FromArgb(185, 0, 0, 0))
            Dim labelRect As New Rectangle(bounds.X, Math.Max(0, bounds.Y - 18), Math.Min(140, Math.Max(90, bounds.Width)), 18)
            g.FillRectangle(textBack, labelRect)
            TextRenderer.DrawText(g, label, Font, labelRect, Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
        End Using

        If selected Then
            For i As Integer = 0 To points.Count - 1
                Dim handle As Rectangle = GetLootScanHandleRect(points(i))
                Using hb As New SolidBrush(Color.White)
                    g.FillRectangle(hb, handle)
                End Using
                Using hp As New Pen(Color.Black, 1.0F)
                    g.DrawRectangle(hp, handle)
                End Using
            Next
        End If
    End Sub

    Private Sub DrawRegion(g As Graphics, key As String, region As RectRegion, colorFill As Color, label As String)
        If Not IsRegionOverlayEnabled(key) Then
            Return
        End If

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

        Dim lootVertexIndex As Integer = If(IsLootScanOverlayEnabled(), HitTestLootScanHandle(e.Location), -1)
        If lootVertexIndex >= 0 Then
            _selectedRegion = "loot_scan_area"
            _dragMode = DragMode.MoveLootScanVertex
            _dragStart = e.Location
            _dragLootScanPoints = CloneLootScanPoints(_currentConfig.LootScanPoints)
            _activeLootScanVertexIndex = lootVertexIndex
            _isDragging = True
            Capture = True
            Invalidate()
            Return
        End If

        If IsLootScanOverlayEnabled() AndAlso IsPointInLootScanArea(e.Location) Then
            _selectedRegion = "loot_scan_area"
            _dragMode = DragMode.MoveLootScanArea
            _dragStart = e.Location
            _dragLootScanPoints = CloneLootScanPoints(_currentConfig.LootScanPoints)
            _activeLootScanVertexIndex = -1
            _isDragging = True
            Capture = True
            Invalidate()
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

            If _dragMode = DragMode.MoveLootScanArea OrElse _dragMode = DragMode.MoveLootScanVertex Then
                Dim editedPoints As List(Of LootScanPoint) = CloneLootScanPoints(_dragLootScanPoints)
                If _dragMode = DragMode.MoveLootScanArea Then
                    For Each pt In editedPoints
                        pt.X += dx
                        pt.Y += dy
                    Next
                    ClampLootScanPointsToClient(editedPoints)
                ElseIf _activeLootScanVertexIndex >= 0 AndAlso _activeLootScanVertexIndex < editedPoints.Count Then
                    editedPoints(_activeLootScanVertexIndex).X += dx
                    editedPoints(_activeLootScanVertexIndex).Y += dy
                    ClampLootScanPointToClient(editedPoints(_activeLootScanVertexIndex))
                End If

                _currentConfig.LootScanPoints = editedPoints
                RaiseEvent OverlayLootScanAreaChanged(CloneLootScanPoints(editedPoints))
            Else
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
            End If
            Invalidate()
            Return
        End If

        If IsLootScanOverlayEnabled() AndAlso HitTestLootScanHandle(e.Location) >= 0 Then
            Cursor = Cursors.SizeAll
            Return
        End If
        If IsLootScanOverlayEnabled() AndAlso IsPointInLootScanArea(e.Location) Then
            Cursor = Cursors.SizeAll
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
        Dim finishedMode As DragMode = _dragMode
        _dragMode = DragMode.None

        If finishedMode = DragMode.MoveLootScanArea OrElse finishedMode = DragMode.MoveLootScanVertex Then
            RaiseEvent OverlayLootScanAreaCommitted(CloneLootScanPoints(_currentConfig.LootScanPoints))
        Else
            Dim region As RectRegion = GetRegionByKey(_selectedRegion)
            If region IsNot Nothing Then
                RaiseEvent OverlayRegionCommitted(_selectedRegion, CloneRegion(region))
            End If
        End If
        Invalidate()
    End Sub

    Private Function HitTestRegion(pt As System.Drawing.Point) As String
        Dim keys As String() = {"chat_rect", "map_coordinate_y_rect", "map_coordinate_x_rect", "disconnect_message_rect", "party_list_rect", "party_invite_ok_rect", "party_invite_scan_rect", "rupiahs_rect", "prana_exp_rect", "unreachable_text_rect", "mob_life_rect", "mob_hp_rect", "mob_name_rect", "mp_bar", "hp_bar"}
        For Each key In keys
            If Not IsRegionOverlayEnabled(key) Then
                Continue For
            End If

            Dim rect As System.Drawing.Rectangle = GetRegionRect(key)
            If GetResizeHandleRect(rect).Contains(pt) OrElse rect.Contains(pt) Then
                Return key
            End If
        Next
        Return ""
    End Function

    Private Function IsRegionOverlayEnabled(regionKey As String) As Boolean
        If _currentConfig Is Nothing Then
            Return True
        End If

        Return _currentConfig.IsCalibrationRegionOverlayEnabled(regionKey)
    End Function

    Private Function IsLootScanOverlayEnabled() As Boolean
        Return _currentConfig IsNot Nothing AndAlso _currentConfig.LootScannerEnabled
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

    Private Function GetLootScanPoints() As List(Of DrawingPoint)
        Dim source As List(Of LootScanPoint) = CloneLootScanPoints(_currentConfig?.LootScanPoints)
        If source.Count < 3 Then
            source = New List(Of LootScanPoint) From {
                New LootScanPoint(_currentConfig.LootScanRect.X, _currentConfig.LootScanRect.Y),
                New LootScanPoint(_currentConfig.LootScanRect.X + _currentConfig.LootScanRect.W, _currentConfig.LootScanRect.Y),
                New LootScanPoint(_currentConfig.LootScanRect.X + _currentConfig.LootScanRect.W, _currentConfig.LootScanRect.Y + _currentConfig.LootScanRect.H),
                New LootScanPoint(_currentConfig.LootScanRect.X, _currentConfig.LootScanRect.Y + _currentConfig.LootScanRect.H)
            }
        End If

        Return source.Select(Function(pt) New DrawingPoint(pt.X, pt.Y)).ToList()
    End Function

    Private Function GetLootScanBounds(points As List(Of DrawingPoint)) As Rectangle
        If points Is Nothing OrElse points.Count = 0 Then
            Return Rectangle.Empty
        End If

        Dim minX As Integer = points.Min(Function(pt) pt.X)
        Dim minY As Integer = points.Min(Function(pt) pt.Y)
        Dim maxX As Integer = points.Max(Function(pt) pt.X)
        Dim maxY As Integer = points.Max(Function(pt) pt.Y)
        Return New Rectangle(minX, minY, Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1))
    End Function

    Private Function GetLootScanHandleRect(pt As DrawingPoint) As Rectangle
        Return New Rectangle(pt.X - (HandleSize \ 2), pt.Y - (HandleSize \ 2), HandleSize, HandleSize)
    End Function

    Private Function HitTestLootScanHandle(pt As DrawingPoint) As Integer
        Dim points As List(Of DrawingPoint) = GetLootScanPoints()
        For i As Integer = 0 To points.Count - 1
            If GetLootScanHandleRect(points(i)).Contains(pt) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function IsPointInLootScanArea(pt As DrawingPoint) As Boolean
        Dim points As List(Of DrawingPoint) = GetLootScanPoints()
        If points.Count < 3 Then
            Return False
        End If

        Using path As New GraphicsPath()
            path.AddPolygon(points.ToArray())
            Return path.IsVisible(pt)
        End Using
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
            Case "mob_life_rect"
                Return _currentConfig.MobLifeRect
            Case "unreachable_text_rect"
                Return _currentConfig.UnreachableTextRect
            Case "prana_exp_rect"
                Return _currentConfig.PranaExpRect
            Case "rupiahs_rect"
                Return _currentConfig.RupiahsRect
            Case "party_invite_scan_rect"
                Return _currentConfig.PartyInviteScanRect
            Case "party_invite_ok_rect"
                Return _currentConfig.PartyInviteOkRect
            Case "party_list_rect"
                Return _currentConfig.PartyListRect
            Case "disconnect_message_rect"
                Return _currentConfig.DisconnectMessageRect
            Case "map_coordinate_x_rect"
                Return _currentConfig.MapCoordinateXRect
            Case "map_coordinate_y_rect"
                Return _currentConfig.MapCoordinateYRect
            Case "chat_rect"
                Return _currentConfig.ChatRect
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
            Case "mob_life_rect"
                _currentConfig.MobLifeRect = value
            Case "unreachable_text_rect"
                _currentConfig.UnreachableTextRect = value
            Case "prana_exp_rect"
                _currentConfig.PranaExpRect = value
            Case "rupiahs_rect"
                _currentConfig.RupiahsRect = value
            Case "party_invite_scan_rect"
                _currentConfig.PartyInviteScanRect = value
            Case "party_invite_ok_rect"
                _currentConfig.PartyInviteOkRect = value
            Case "party_list_rect"
                _currentConfig.PartyListRect = value
            Case "disconnect_message_rect"
                _currentConfig.DisconnectMessageRect = value
            Case "map_coordinate_x_rect"
                _currentConfig.MapCoordinateXRect = value
                _currentConfig.MapCoordinateRect = BotConfig.CombineMapCoordinateRects(_currentConfig.MapCoordinateXRect, _currentConfig.MapCoordinateYRect)
            Case "map_coordinate_y_rect"
                _currentConfig.MapCoordinateYRect = value
                _currentConfig.MapCoordinateRect = BotConfig.CombineMapCoordinateRects(_currentConfig.MapCoordinateXRect, _currentConfig.MapCoordinateYRect)
            Case "chat_rect"
                _currentConfig.ChatRect = value
        End Select
    End Sub

    Private Sub ClampRegionToClient(region As RectRegion)
        region.W = Math.Max(MinRegionSize, region.W)
        region.H = Math.Max(MinRegionSize, region.H)
        region.X = Math.Max(0, Math.Min(ClientSize.Width - region.W, region.X))
        region.Y = Math.Max(0, Math.Min(ClientSize.Height - region.H, region.Y))
    End Sub

    Private Sub ClampLootScanPointToClient(point As LootScanPoint)
        If point Is Nothing Then
            Return
        End If

        point.X = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Width - 1), point.X))
        point.Y = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Height - 1), point.Y))
    End Sub

    Private Sub ClampLootScanPointsToClient(points As List(Of LootScanPoint))
        If points Is Nothing OrElse points.Count = 0 Then
            Return
        End If

        Dim minX As Integer = points.Min(Function(pt) pt.X)
        Dim minY As Integer = points.Min(Function(pt) pt.Y)
        Dim maxX As Integer = points.Max(Function(pt) pt.X)
        Dim maxY As Integer = points.Max(Function(pt) pt.Y)

        Dim offsetX As Integer = 0
        Dim offsetY As Integer = 0
        If minX < 0 Then
            offsetX = -minX
        ElseIf maxX >= ClientSize.Width Then
            offsetX = (ClientSize.Width - 1) - maxX
        End If
        If minY < 0 Then
            offsetY = -minY
        ElseIf maxY >= ClientSize.Height Then
            offsetY = (ClientSize.Height - 1) - maxY
        End If

        For Each pt In points
            pt.X += offsetX
            pt.Y += offsetY
            ClampLootScanPointToClient(pt)
        Next
    End Sub

    Private Function CloneConfig(src As BotConfig) As BotConfig
        Dim cfg As New BotConfig()
        cfg.WindowTitle = src.WindowTitle
        cfg.SelectedWindowHandle = src.SelectedWindowHandle
        cfg.HpBar = CloneRegion(src.HpBar)
        cfg.MpBar = CloneRegion(src.MpBar)
        cfg.MobNameRect = CloneRegion(src.MobNameRect)
        cfg.MobHpRect = CloneRegion(src.MobHpRect)
        cfg.MobLifeRect = CloneRegion(src.MobLifeRect)
        cfg.UnreachableTextRect = CloneRegion(src.UnreachableTextRect)
        cfg.PranaExpRect = CloneRegion(src.PranaExpRect)
        cfg.RupiahsRect = CloneRegion(src.RupiahsRect)
        cfg.PartyInviteScanRect = CloneRegion(src.PartyInviteScanRect)
        cfg.PartyInviteOkRect = CloneRegion(src.PartyInviteOkRect)
        cfg.PartyListRect = CloneRegion(src.PartyListRect)
        cfg.DisconnectMessageRect = CloneRegion(src.DisconnectMessageRect)
        cfg.MapRect = CloneRegion(src.MapRect)
        cfg.MapCoordinateRect = CloneRegion(src.MapCoordinateRect)
        cfg.MapCoordinateXRect = CloneRegion(src.MapCoordinateXRect)
        cfg.MapCoordinateYRect = CloneRegion(src.MapCoordinateYRect)
        cfg.ChatRect = CloneRegion(src.ChatRect)
        cfg.LootScanRect = CloneRegion(src.LootScanRect)
        cfg.LootScanPoints = CloneLootScanPoints(src.LootScanPoints)
        cfg.LootScannerEnabled = src.LootScannerEnabled
        cfg.DisabledCalibrationRegionOverlays = If(src.DisabledCalibrationRegionOverlays, New List(Of String)()).ToList()
        Return cfg
    End Function

    Private Function CloneRegion(src As RectRegion) As RectRegion
        If src Is Nothing Then
            Return New RectRegion(0, 0, 1, 1)
        End If
        Return New RectRegion(src.X, src.Y, src.W, src.H)
    End Function

    Private Function CloneLootScanPoints(points As IEnumerable(Of LootScanPoint)) As List(Of LootScanPoint)
        Dim source As IEnumerable(Of LootScanPoint) = If(points, Enumerable.Empty(Of LootScanPoint)())
        Return source.Where(Function(pt) pt IsNot Nothing).Select(Function(pt) New LootScanPoint(pt.X, pt.Y)).ToList()
    End Function

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _timer.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
