Imports System.Drawing.Drawing2D

Friend Class InGameBotToggleForm
    Inherits Form

    Public Event ToggleRequested()
    Public Event OverlayLayoutChanged(clientX As Integer, clientY As Integer, overlayWidth As Integer, overlayHeight As Integer)

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const GA_ROOT As UInteger = 2UI
    Private Const DefaultOverlayWidth As Integer = 220
    Private Const DefaultOverlayHeight As Integer = 76
    Private Const OverlayMargin As Integer = 10
    Private Const ResizeGripSize As Integer = 13
    Private Const DragThreshold As Integer = 3
    Private Const MinimumOverlayWidth As Integer = 188
    Private Const MinimumOverlayHeight As Integer = 68
    Private Const MaximumOverlayWidth As Integer = 360
    Private Const MaximumOverlayHeight As Integer = 140

    Private Enum PointerInteraction
        None
        Drag
        Resize
    End Enum

    Private NotInheritable Class OverlaySurface
        Inherits Control

        Private _renderer As Action(Of PaintEventArgs)

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw, True)
            TabStop = False
            Cursor = Cursors.SizeAll
        End Sub

        Public Sub SetRenderer(renderer As Action(Of PaintEventArgs))
            _renderer = renderer
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' The renderer always paints the full surface. Skipping the native erase prevents a
            ' white intermediate frame while this top-level HUD is being dragged.
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Try
                If _renderer IsNot Nothing Then
                    _renderer.Invoke(e)
                Else
                    PaintFallback(e.Graphics)
                End If
            Catch ex As Exception
                ' WinForms replaces a control with a white/red X when Paint throws. A graphics
                ' resource can become transiently unavailable while a top-most window is moving,
                ' so always leave behind a valid compact fallback and retry on the next tick.
                System.Diagnostics.Debug.WriteLine("In-game HUD paint recovered: " & ex.Message)
                PaintFallback(e.Graphics)
            End Try
        End Sub

        Private Sub PaintFallback(g As Graphics)
            Try
                g.ResetTransform()
                g.ResetClip()
                g.Clear(Color.FromArgb(9, 15, 26))
                Using borderPen As New Pen(Color.FromArgb(80, 220, 165), 1.0F)
                    g.DrawRectangle(borderPen, 0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1))
                End Using
                Using fallbackFont As New Font("Segoe UI Semibold", 8.0F, FontStyle.Bold)
                    TextRenderer.DrawText(g, "BOT STATUS", fallbackFont, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
                End Using
            Catch
                ' Never allow the framework's red-X error surface to replace the HUD.
            End Try
        End Sub
    End Class

    Private ReadOnly _windowProvider As Func(Of IntPtr)
    Private ReadOnly _editionProvider As Func(Of BotEdition)
    Private ReadOnly _runningProvider As Func(Of BotEdition, Boolean)
    Private ReadOnly _statusProvider As Func(Of BotEdition, BotStatus)
    Private ReadOnly _timer As New Timer()
    Private ReadOnly _toggleButton As OverlaySurface
    Private ReadOnly _toolTip As New ToolTip()
    Private _lastRunning As Boolean? = Nothing
    Private _lastEdition As BotEdition? = Nothing
    Private _lastStatusSignature As String = ""
    Private _displayHp As Double = -1
    Private _displayMp As Double = -1
    Private _displayExpPerHour As Double = -1
    Private _displayMobHp As Double = -1
    Private _displayTarget As String = ""
    Private _displayTargetValid As Boolean = False
    Private _hovered As Boolean = False
    Private _pressed As Boolean = False
    Private _hoverBlend As Single = 0.0F
    Private _clientX As Integer
    Private _clientY As Integer
    Private _overlayWidth As Integer
    Private _overlayHeight As Integer
    Private _gameClientBounds As System.Drawing.Rectangle = System.Drawing.Rectangle.Empty
    Private _pointerInteraction As PointerInteraction = PointerInteraction.None
    Private _pointerDownScreen As System.Drawing.Point = System.Drawing.Point.Empty
    Private _interactionStartBounds As System.Drawing.Rectangle = System.Drawing.Rectangle.Empty
    Private _pointerMoved As Boolean = False

    Public Sub New(
        windowProvider As Func(Of IntPtr),
        editionProvider As Func(Of BotEdition),
        runningProvider As Func(Of BotEdition, Boolean),
        statusProvider As Func(Of BotEdition, BotStatus),
        clientX As Integer,
        clientY As Integer,
        overlayWidth As Integer,
        overlayHeight As Integer)

        _windowProvider = windowProvider
        _editionProvider = editionProvider
        _runningProvider = runningProvider
        _statusProvider = statusProvider
        ApplyLayout(clientX, clientY, overlayWidth, overlayHeight)

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        Size = New Size(_overlayWidth, _overlayHeight)
        BackColor = Color.FromArgb(9, 15, 26)
        Padding = New Padding(0)

        _toggleButton = New OverlaySurface() With {
            .Dock = DockStyle.Fill,
            .AccessibleName = "In-game bot status and power control"
        }
        _toggleButton.SetRenderer(AddressOf PaintOverlaySurface)
        AddHandler _toggleButton.MouseDown, AddressOf ToggleButtonMouseDown
        AddHandler _toggleButton.MouseMove, AddressOf ToggleButtonMouseMove
        AddHandler _toggleButton.MouseUp, AddressOf ToggleButtonMouseUp
        AddHandler _toggleButton.MouseEnter, AddressOf ToggleButtonMouseEnter
        AddHandler _toggleButton.MouseLeave, AddressOf ToggleButtonMouseLeave
        Controls.Add(_toggleButton)
        _toolTip.SetToolTip(_toggleButton, "Click to turn the selected Full/Lite bot on or off. Drag anywhere to move; drag the bottom-right grip to resize.")
        UpdateOverlayAppearance(BotEdition.Full, False, Nothing)

        _timer.Interval = 60
        AddHandler _timer.Tick, AddressOf TickUpdate
        _timer.Start()
    End Sub

    Public Sub ApplyLayout(clientX As Integer, clientY As Integer, overlayWidth As Integer, overlayHeight As Integer)
        _clientX = clientX
        _clientY = Math.Max(0, clientY)
        _overlayWidth = Math.Max(MinimumOverlayWidth, Math.Min(MaximumOverlayWidth, If(overlayWidth > 0, overlayWidth, DefaultOverlayWidth)))
        _overlayHeight = Math.Max(MinimumOverlayHeight, Math.Min(MaximumOverlayHeight, If(overlayHeight > 0, overlayHeight, DefaultOverlayHeight)))

        If IsHandleCreated Then
            Size = New Size(_overlayWidth, _overlayHeight)
        End If
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        If Width <= 1 OrElse Height <= 1 Then
            Return
        End If

        Using path As GraphicsPath = CreateRoundedRectanglePath(New Rectangle(0, 0, Width, Height), 13)
            Dim oldRegion As Region = Region
            Region = New Region(path)
            If oldRegion IsNot Nothing Then
                oldRegion.Dispose()
            End If
        End Using
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW Or WS_EX_NOACTIVATE
            Return cp
        End Get
    End Property

    Private Sub TickUpdate(sender As Object, e As EventArgs)
        If _windowProvider Is Nothing OrElse _editionProvider Is Nothing OrElse _runningProvider Is Nothing Then
            Hide()
            Return
        End If

        Dim hwnd As IntPtr = _windowProvider.Invoke()
        If hwnd = IntPtr.Zero OrElse Not NativeMethods.IsWindowVisible(hwnd) OrElse NativeMethods.IsIconic(hwnd) Then
            Hide()
            Return
        End If

        Dim foreground As IntPtr = NativeMethods.GetForegroundWindow()
        If foreground = IntPtr.Zero OrElse NativeMethods.GetAncestor(foreground, GA_ROOT) <> NativeMethods.GetAncestor(hwnd, GA_ROOT) Then
            Hide()
            Return
        End If

        Dim clientRect As NativeMethods.RECT
        Dim origin As New NativeMethods.POINT With {.X = 0, .Y = 0}
        If Not NativeMethods.GetClientRect(hwnd, clientRect) OrElse Not NativeMethods.ClientToScreen(hwnd, origin) Then
            Hide()
            Return
        End If

        Dim clientWidth As Integer = clientRect.Right - clientRect.Left
        Dim clientHeight As Integer = clientRect.Bottom - clientRect.Top
        If clientWidth < MinimumOverlayWidth OrElse clientHeight < MinimumOverlayHeight Then
            Hide()
            Return
        End If

        _gameClientBounds = New System.Drawing.Rectangle(origin.X, origin.Y, clientWidth, clientHeight)
        _overlayWidth = Math.Max(MinimumOverlayWidth, Math.Min(Math.Min(MaximumOverlayWidth, clientWidth), _overlayWidth))
        _overlayHeight = Math.Max(MinimumOverlayHeight, Math.Min(Math.Min(MaximumOverlayHeight, clientHeight), _overlayHeight))
        If _clientX < 0 Then
            _clientX = Math.Max(0, clientWidth - _overlayWidth - OverlayMargin)
        End If
        _clientX = Math.Max(0, Math.Min(Math.Max(0, clientWidth - _overlayWidth), _clientX))
        _clientY = Math.Max(0, Math.Min(Math.Max(0, clientHeight - _overlayHeight), _clientY))

        If _pointerInteraction = PointerInteraction.None Then
            Dim targetBounds As New System.Drawing.Rectangle(
                origin.X + _clientX,
                origin.Y + _clientY,
                _overlayWidth,
                _overlayHeight)
            If Bounds <> targetBounds Then
                Bounds = targetBounds
            End If
        End If

        Dim edition As BotEdition = _editionProvider.Invoke()
        Dim running As Boolean = _runningProvider.Invoke(edition)
        Dim status As BotStatus = If(_statusProvider Is Nothing, Nothing, _statusProvider.Invoke(edition))
        UpdateOverlayAppearance(edition, running, status)
        AnimateHover()
        If Not Visible Then
            Show()
        End If
    End Sub

    Private Sub ToggleButtonMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then
            Return
        End If

        _pointerInteraction = If(IsResizeGrip(e.Location), PointerInteraction.Resize, PointerInteraction.Drag)
        _pointerDownScreen = Control.MousePosition
        _interactionStartBounds = Bounds
        _pointerMoved = False
        _pressed = True
        _toggleButton.Capture = True
        _toggleButton.Invalidate()
        UpdatePointerCursor(e.Location)
    End Sub

    Private Sub ToggleButtonMouseMove(sender As Object, e As MouseEventArgs)
        If _pointerInteraction = PointerInteraction.None Then
            UpdatePointerCursor(e.Location)
            Return
        End If

        Dim currentScreen As System.Drawing.Point = Control.MousePosition
        Dim deltaX As Integer = currentScreen.X - _pointerDownScreen.X
        Dim deltaY As Integer = currentScreen.Y - _pointerDownScreen.Y
        If Math.Abs(deltaX) > DragThreshold OrElse Math.Abs(deltaY) > DragThreshold Then
            _pointerMoved = True
        End If
        If Not _pointerMoved OrElse _gameClientBounds.IsEmpty Then
            Return
        End If

        If _pointerInteraction = PointerInteraction.Drag Then
            Dim newX As Integer = Math.Max(_gameClientBounds.Left, Math.Min(_gameClientBounds.Right - Width, _interactionStartBounds.X + deltaX))
            Dim newY As Integer = Math.Max(_gameClientBounds.Top, Math.Min(_gameClientBounds.Bottom - Height, _interactionStartBounds.Y + deltaY))
            Location = New System.Drawing.Point(newX, newY)
            _toggleButton.Invalidate()
            _toggleButton.Update()
        Else
            Dim maxWidth As Integer = Math.Min(MaximumOverlayWidth, _gameClientBounds.Right - _interactionStartBounds.Left)
            Dim maxHeight As Integer = Math.Min(MaximumOverlayHeight, _gameClientBounds.Bottom - _interactionStartBounds.Top)
            Dim newWidth As Integer = Math.Max(MinimumOverlayWidth, Math.Min(maxWidth, _interactionStartBounds.Width + deltaX))
            Dim newHeight As Integer = Math.Max(MinimumOverlayHeight, Math.Min(maxHeight, _interactionStartBounds.Height + deltaY))
            Size = New Size(newWidth, newHeight)
        End If
    End Sub

    Private Sub ToggleButtonMouseUp(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse _pointerInteraction = PointerInteraction.None Then
            Return
        End If

        Dim completedInteraction As PointerInteraction = _pointerInteraction
        _pointerInteraction = PointerInteraction.None
        _pressed = False
        _toggleButton.Capture = False

        If completedInteraction = PointerInteraction.Drag AndAlso Not _pointerMoved Then
            RaiseEvent ToggleRequested()
            If _editionProvider IsNot Nothing AndAlso _runningProvider IsNot Nothing Then
                Dim edition As BotEdition = _editionProvider.Invoke()
                Dim status As BotStatus = If(_statusProvider Is Nothing, Nothing, _statusProvider.Invoke(edition))
                UpdateOverlayAppearance(edition, _runningProvider.Invoke(edition), status)
            End If
        ElseIf _gameClientBounds.IsEmpty Then
            Return
        Else
            _clientX = Math.Max(0, Left - _gameClientBounds.Left)
            _clientY = Math.Max(0, Top - _gameClientBounds.Top)
            _overlayWidth = Width
            _overlayHeight = Height
            RaiseEvent OverlayLayoutChanged(_clientX, _clientY, _overlayWidth, _overlayHeight)
        End If

        UpdatePointerCursor(e.Location)
        _toggleButton.Invalidate()
    End Sub

    Private Sub ToggleButtonMouseEnter(sender As Object, e As EventArgs)
        _hovered = True
    End Sub

    Private Sub ToggleButtonMouseLeave(sender As Object, e As EventArgs)
        _hovered = False
        If _pointerInteraction = PointerInteraction.None Then
            _toggleButton.Cursor = Cursors.SizeAll
        End If
    End Sub

    Private Function IsResizeGrip(location As System.Drawing.Point) As Boolean
        Return location.X >= Math.Max(0, _toggleButton.ClientSize.Width - ResizeGripSize) AndAlso
            location.Y >= Math.Max(0, _toggleButton.ClientSize.Height - ResizeGripSize)
    End Function

    Private Sub UpdatePointerCursor(location As System.Drawing.Point)
        _toggleButton.Cursor = If(_pointerInteraction = PointerInteraction.Resize OrElse IsResizeGrip(location), Cursors.SizeNWSE, Cursors.SizeAll)
    End Sub

    Private Sub PaintOverlaySurface(e As PaintEventArgs)
        Dim bounds As New Rectangle(0, 0, Math.Max(1, _toggleButton.ClientSize.Width - 1), Math.Max(1, _toggleButton.ClientSize.Height - 1))
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality

        Dim running As Boolean = _lastRunning.GetValueOrDefault(False)
        Dim accent As Color = If(running, Color.FromArgb(45, 224, 157), Color.FromArgb(248, 113, 113))
        Dim topColor As Color = BlendColor(Color.FromArgb(17, 29, 48), Color.FromArgb(25, 43, 68), _hoverBlend)
        Dim bottomColor As Color = BlendColor(Color.FromArgb(8, 15, 27), Color.FromArgb(12, 24, 39), _hoverBlend)
        If _pressed Then
            topColor = BlendColor(topColor, Color.Black, 0.16F)
            bottomColor = BlendColor(bottomColor, Color.Black, 0.16F)
        End If

        Using panelPath As GraphicsPath = CreateRoundedRectanglePath(bounds, 13),
              panelBrush As New LinearGradientBrush(bounds, topColor, bottomColor, LinearGradientMode.Vertical),
              borderPen As New Pen(Color.FromArgb(If(_hovered, 210, 145), accent), If(_hovered, 1.6F, 1.0F))
            e.Graphics.FillPath(panelBrush, panelPath)
            e.Graphics.DrawPath(borderPen, panelPath)
        End Using

        Using accentPath As GraphicsPath = CreateRoundedRectanglePath(New Rectangle(9, 8, 4, Math.Max(10, bounds.Height - 16)), 2),
              accentBrush As New SolidBrush(accent)
            e.Graphics.FillPath(accentBrush, accentPath)
        End Using

        Dim contentLeft As Integer = 21
        Dim powerSize As Integer = Math.Min(27, Math.Max(22, bounds.Height - 40))
        Dim powerRect As New Rectangle(bounds.Right - powerSize - 10, 8, powerSize, powerSize)
        Dim headingWidth As Integer = Math.Max(30, powerRect.Left - contentLeft - 5)
        Dim editionText As String = If(_lastEdition.GetValueOrDefault(BotEdition.Full) = BotEdition.Lite, "LITE BOT", "FULL BOT")
        Dim stateText As String = If(running, "ACTIVE", "STOPPED")

        Using headingFont As New Font("Segoe UI Semibold", 8.4F, FontStyle.Bold),
              stateFont As New Font("Segoe UI", 7.4F, FontStyle.Bold),
              detailFont As New Font("Segoe UI", 7.2F, FontStyle.Regular),
              headingBrush As New SolidBrush(Color.FromArgb(244, 248, 255)),
              stateBrush As New SolidBrush(accent),
              secondaryBrush As New SolidBrush(Color.FromArgb(166, 184, 211))

            Dim editionSize As Size = TextRenderer.MeasureText(editionText, headingFont, New Size(Integer.MaxValue, 18), TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            TextRenderer.DrawText(e.Graphics, editionText, headingFont, New Rectangle(contentLeft, 7, Math.Min(headingWidth, editionSize.Width + 2), 18), headingBrush.Color, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
            Dim stateX As Integer = Math.Min(powerRect.Left - 42, contentLeft + editionSize.Width + 7)
            TextRenderer.DrawText(e.Graphics, stateText, stateFont, New Rectangle(stateX, 7, Math.Max(35, powerRect.Left - stateX - 3), 18), stateBrush.Color, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)

            DrawPowerIcon(e.Graphics, powerRect, accent, running)

            Dim statsTop As Integer = 28
            Dim statsWidth As Integer = Math.Max(20, bounds.Width - contentLeft - 11)
            Dim hpText As String = If(running AndAlso _displayHp >= 0, $"HP {_displayHp:0}%", "HP --")
            Dim mpText As String = If(running AndAlso _displayMp >= 0, $"MP {_displayMp:0}%", "MP --")
            Dim expText As String = If(Not running, "EXP --", If(_displayExpPerHour < 0, "EXP CALC", $"EXP {_displayExpPerHour:0.0}/h"))
            Dim statsText As String = $"{hpText}   {mpText}   {expText}"
            TextRenderer.DrawText(e.Graphics, statsText, detailFont, New Rectangle(contentLeft, statsTop, statsWidth, 17), secondaryBrush.Color, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)

            If bounds.Height >= 61 Then
                Dim targetText As String
                If running AndAlso _displayTargetValid AndAlso Not String.IsNullOrWhiteSpace(_displayTarget) Then
                    Dim targetHpText As String = If(_displayMobHp >= 0, $" | {_displayMobHp:0}%", "")
                    targetText = $"TARGET  {_displayTarget}{targetHpText}"
                ElseIf running Then
                    targetText = "TARGET  Searching..."
                Else
                    targetText = "Click to start  |  Drag to move"
                End If
                TextRenderer.DrawText(e.Graphics, targetText, detailFont, New Rectangle(contentLeft, 46, statsWidth, Math.Max(14, bounds.Height - 48)), If(running, Color.FromArgb(218, 229, 246), Color.FromArgb(139, 155, 178)), TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
            End If
        End Using

        Dim right As Integer = bounds.Right - 3
        Dim bottom As Integer = bounds.Bottom - 3
        Using gripPen As New Pen(Color.FromArgb(100, 190, 205, 225), 1.0F)
            e.Graphics.DrawLine(gripPen, right - 3, bottom, right, bottom - 3)
            e.Graphics.DrawLine(gripPen, right - 7, bottom, right, bottom - 7)
        End Using
    End Sub

    Private Sub UpdateOverlayAppearance(edition As BotEdition, running As Boolean, status As BotStatus)
        Dim hp As Double = If(status Is Nothing, -1, ClampPercent(status.HpPercent))
        Dim mp As Double = If(status Is Nothing, -1, ClampPercent(status.MpPercent))
        Dim expRate As Double = If(status Is Nothing, -1, status.ExpPerHour)
        Dim mobHp As Double = If(status Is Nothing, -1, ClampPercent(status.MobHpPercent))
        Dim mobName As String = If(status Is Nothing, "", If(status.MobName, "").Trim())
        Dim targetValid As Boolean = status IsNot Nothing AndAlso status.TargetValid AndAlso mobName <> ""
        Dim signature As String = $"{edition}|{running}|{hp:0.0}|{mp:0.0}|{expRate:0.00}|{mobHp:0.0}|{targetValid}|{mobName}"
        If _lastStatusSignature.Equals(signature, StringComparison.Ordinal) Then
            Return
        End If

        _lastStatusSignature = signature
        _lastEdition = edition
        _lastRunning = running
        _displayHp = hp
        _displayMp = mp
        _displayExpPerHour = expRate
        _displayMobHp = mobHp
        _displayTarget = mobName
        _displayTargetValid = targetValid
        _toggleButton.AccessibleDescription = If(running, $"Active. HP {hp:0} percent, MP {mp:0} percent, target {If(targetValid, mobName, "searching")}. Click to stop.", "Stopped. Click to start.")
        _toggleButton.Invalidate()
    End Sub

    Private Sub AnimateHover()
        Dim target As Single = If(_hovered, 1.0F, 0.0F)
        If Math.Abs(_hoverBlend - target) < 0.02F Then
            _hoverBlend = target
            Return
        End If
        _hoverBlend += (target - _hoverBlend) * 0.28F
        _toggleButton.Invalidate()
    End Sub

    Private Shared Sub DrawPowerIcon(g As Graphics, rect As Rectangle, accent As Color, running As Boolean)
        Using fillBrush As New SolidBrush(Color.FromArgb(If(running, 32, 22), accent)),
              outlinePen As New Pen(Color.FromArgb(If(running, 210, 150), accent), 1.25F),
              powerPen As New Pen(Color.FromArgb(238, 245, 250), 1.65F)
            g.FillEllipse(fillBrush, rect)
            g.DrawEllipse(outlinePen, rect)
            Dim inset As Rectangle = Rectangle.Inflate(rect, -7, -7)
            g.DrawArc(powerPen, inset, -45.0F, 270.0F)
            Dim centerX As Single = rect.Left + (rect.Width / 2.0F)
            g.DrawLine(powerPen, centerX, rect.Top + 5, centerX, rect.Top + (rect.Height / 2.0F) + 1)
        End Using
    End Sub

    Private Shared Function CreateRoundedRectanglePath(rect As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(2, Math.Min(Math.Min(rect.Width, rect.Height), radius * 2))
        Dim arc As New Rectangle(rect.X, rect.Y, diameter, diameter)
        path.AddArc(arc, 180, 90)
        arc.X = rect.Right - diameter
        path.AddArc(arc, 270, 90)
        arc.Y = rect.Bottom - diameter
        path.AddArc(arc, 0, 90)
        arc.X = rect.X
        path.AddArc(arc, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Shared Function BlendColor(fromColor As Color, toColor As Color, amount As Single) As Color
        amount = Math.Max(0.0F, Math.Min(1.0F, amount))
        Return Color.FromArgb(
            CInt(fromColor.A + ((toColor.A - fromColor.A) * amount)),
            CInt(fromColor.R + ((toColor.R - fromColor.R) * amount)),
            CInt(fromColor.G + ((toColor.G - fromColor.G) * amount)),
            CInt(fromColor.B + ((toColor.B - fromColor.B) * amount)))
    End Function

    Private Shared Function ClampPercent(value As Double) As Double
        If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then
            Return -1
        End If
        Return Math.Max(0.0, Math.Min(100.0, value))
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _timer.Stop()
            RemoveHandler _timer.Tick, AddressOf TickUpdate
            _timer.Dispose()
            _toolTip.Dispose()
            RemoveHandler _toggleButton.MouseDown, AddressOf ToggleButtonMouseDown
            RemoveHandler _toggleButton.MouseMove, AddressOf ToggleButtonMouseMove
            RemoveHandler _toggleButton.MouseUp, AddressOf ToggleButtonMouseUp
            RemoveHandler _toggleButton.MouseEnter, AddressOf ToggleButtonMouseEnter
            RemoveHandler _toggleButton.MouseLeave, AddressOf ToggleButtonMouseLeave
            _toggleButton.SetRenderer(Nothing)
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
