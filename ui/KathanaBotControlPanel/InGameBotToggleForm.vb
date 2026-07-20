Friend Class InGameBotToggleForm
    Inherits Form

    Public Event ToggleRequested()
    Public Event SkillCooldownScaleChanged(multiplier As Decimal)
    Public Event OverlayLayoutChanged(clientX As Integer, clientY As Integer, overlayWidth As Integer, overlayHeight As Integer)

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const GA_ROOT As UInteger = 2UI
    Private Const DefaultOverlayWidth As Integer = 260
    Private Const DefaultOverlayHeight As Integer = 84
    Private Const OverlayMargin As Integer = 10
    Private Const ResizeGripSize As Integer = 13
    Private Const DragThreshold As Integer = 3
    Private Const MinimumOverlayWidth As Integer = 210
    Private Const MinimumOverlayHeight As Integer = 72
    Private Const MaximumOverlayWidth As Integer = 420
    Private Const MaximumOverlayHeight As Integer = 180

    Private Enum PointerInteraction
        None
        Drag
        Resize
    End Enum

    Private ReadOnly _windowProvider As Func(Of IntPtr)
    Private ReadOnly _editionProvider As Func(Of BotEdition)
    Private ReadOnly _runningProvider As Func(Of BotEdition, Boolean)
    Private ReadOnly _skillCooldownScaleProvider As Func(Of Decimal)
    Private ReadOnly _skillCooldownScaleApplier As Func(Of IntPtr, Decimal, Boolean)
    Private ReadOnly _timer As New Timer()
    Private ReadOnly _toggleButton As Button
    Private ReadOnly _skillCooldownTrack As TrackBar
    Private ReadOnly _skillCooldownLabel As Label
    Private ReadOnly _toolTip As New ToolTip()
    Private _lastRunning As Boolean? = Nothing
    Private _lastEdition As BotEdition? = Nothing
    Private _clientX As Integer
    Private _clientY As Integer
    Private _overlayWidth As Integer
    Private _overlayHeight As Integer
    Private _gameClientBounds As System.Drawing.Rectangle = System.Drawing.Rectangle.Empty
    Private _pointerInteraction As PointerInteraction = PointerInteraction.None
    Private _pointerDownScreen As System.Drawing.Point = System.Drawing.Point.Empty
    Private _interactionStartBounds As System.Drawing.Rectangle = System.Drawing.Rectangle.Empty
    Private _pointerMoved As Boolean = False
    Private _updatingSkillCooldownScale As Boolean = False

    Public Sub New(
        windowProvider As Func(Of IntPtr),
        editionProvider As Func(Of BotEdition),
        runningProvider As Func(Of BotEdition, Boolean),
        skillCooldownScaleProvider As Func(Of Decimal),
        skillCooldownScaleApplier As Func(Of IntPtr, Decimal, Boolean),
        clientX As Integer,
        clientY As Integer,
        overlayWidth As Integer,
        overlayHeight As Integer)

        _windowProvider = windowProvider
        _editionProvider = editionProvider
        _runningProvider = runningProvider
        _skillCooldownScaleProvider = skillCooldownScaleProvider
        _skillCooldownScaleApplier = skillCooldownScaleApplier
        ApplyLayout(clientX, clientY, overlayWidth, overlayHeight)

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        Size = New Size(_overlayWidth, _overlayHeight)
        BackColor = Color.FromArgb(12, 12, 12)
        Padding = New Padding(2)

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = BackColor
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 48.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 52.0F))

        _toggleButton = New Button() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 0, 2),
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .TabStop = False,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Cursor = Cursors.Hand
        }
        _toggleButton.FlatAppearance.BorderColor = Color.White
        _toggleButton.FlatAppearance.BorderSize = 1
        AddHandler _toggleButton.MouseDown, AddressOf ToggleButtonMouseDown
        AddHandler _toggleButton.MouseMove, AddressOf ToggleButtonMouseMove
        AddHandler _toggleButton.MouseUp, AddressOf ToggleButtonMouseUp
        AddHandler _toggleButton.MouseLeave, AddressOf ToggleButtonMouseLeave
        AddHandler _toggleButton.Paint, AddressOf ToggleButtonPaint
        layout.Controls.Add(_toggleButton, 0, 0)
        _toolTip.SetToolTip(_toggleButton, "Click to turn the selected Full/Lite bot on or off. Drag to move. Drag the grip to resize.")
        UpdateButtonAppearance(BotEdition.Full, False)

        Dim timeLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = Color.FromArgb(24, 24, 24)
        }
        timeLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 88.0F))
        timeLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        _skillCooldownLabel = New Label() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(2, 0, 0, 0),
            .Text = "SKILL 1.0x",
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .ForeColor = Color.Gainsboro
        }
        _skillCooldownTrack = New TrackBar() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Minimum = 1,
            .Maximum = 100,
            .Value = 10,
            .SmallChange = 1,
            .LargeChange = 1,
            .TickFrequency = 10,
            .TickStyle = TickStyle.BottomRight,
            .AutoSize = False,
            .BackColor = Color.FromArgb(24, 24, 24),
            .TabStop = False
        }
        AddHandler _skillCooldownTrack.Scroll, AddressOf SkillCooldownTrackScrolled
        timeLayout.Controls.Add(_skillCooldownLabel, 0, 0)
        timeLayout.Controls.Add(_skillCooldownTrack, 1, 0)
        layout.Controls.Add(timeLayout, 0, 1)
        Controls.Add(layout)
        _toolTip.SetToolTip(_skillCooldownTrack, "Skill cooldown speed: 0.1x is slower, 1.0x is normal, and 10.0x is faster.")
        UpdateSkillCooldownAppearance(1D)

        _timer.Interval = 120
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

        Dim multiplier As Decimal = If(_skillCooldownScaleProvider Is Nothing, 1D, Math.Max(0.1D, Math.Min(10D, _skillCooldownScaleProvider.Invoke())))
        Dim scaleApplied As Boolean = (multiplier = 1D)
        If _skillCooldownScaleApplier IsNot Nothing Then
            scaleApplied = _skillCooldownScaleApplier.Invoke(hwnd, multiplier)
        End If
        UpdateSkillCooldownAppearance(multiplier, scaleApplied)

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
        UpdateButtonAppearance(edition, _runningProvider.Invoke(edition))
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
        _toggleButton.Capture = True
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
        _toggleButton.Capture = False

        If completedInteraction = PointerInteraction.Drag AndAlso Not _pointerMoved Then
            RaiseEvent ToggleRequested()
            If _editionProvider IsNot Nothing AndAlso _runningProvider IsNot Nothing Then
                Dim edition As BotEdition = _editionProvider.Invoke()
                UpdateButtonAppearance(edition, _runningProvider.Invoke(edition))
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
    End Sub

    Private Sub ToggleButtonMouseLeave(sender As Object, e As EventArgs)
        If _pointerInteraction = PointerInteraction.None Then
            _toggleButton.Cursor = Cursors.Hand
        End If
    End Sub

    Private Function IsResizeGrip(location As System.Drawing.Point) As Boolean
        Return location.X >= Math.Max(0, _toggleButton.ClientSize.Width - ResizeGripSize) AndAlso
            location.Y >= Math.Max(0, _toggleButton.ClientSize.Height - ResizeGripSize)
    End Function

    Private Sub UpdatePointerCursor(location As System.Drawing.Point)
        _toggleButton.Cursor = If(_pointerInteraction = PointerInteraction.Resize OrElse IsResizeGrip(location), Cursors.SizeNWSE, Cursors.SizeAll)
    End Sub

    Private Sub ToggleButtonPaint(sender As Object, e As PaintEventArgs)
        Dim right As Integer = _toggleButton.ClientSize.Width - 3
        Dim bottom As Integer = _toggleButton.ClientSize.Height - 3
        Using gripPen As New Pen(Color.FromArgb(210, Color.White), 1.0F)
            e.Graphics.DrawLine(gripPen, right - 3, bottom, right, bottom - 3)
            e.Graphics.DrawLine(gripPen, right - 7, bottom, right, bottom - 7)
            e.Graphics.DrawLine(gripPen, right - 11, bottom, right, bottom - 11)
        End Using
    End Sub

    Private Sub UpdateButtonAppearance(edition As BotEdition, running As Boolean)
        If _lastEdition.HasValue AndAlso _lastEdition.Value = edition AndAlso
            _lastRunning.HasValue AndAlso _lastRunning.Value = running Then
            Return
        End If

        _lastEdition = edition
        _lastRunning = running
        Dim editionLabel As String = If(edition = BotEdition.Lite, "LITE BOT", "BOT")
        _toggleButton.Text = $"{editionLabel} {If(running, "ON", "OFF")}"
        _toggleButton.BackColor = If(running, Color.FromArgb(25, 155, 75), Color.FromArgb(190, 45, 45))
        _toggleButton.FlatAppearance.MouseOverBackColor = If(running, Color.FromArgb(35, 175, 90), Color.FromArgb(215, 60, 60))
        _toggleButton.FlatAppearance.MouseDownBackColor = If(running, Color.FromArgb(20, 125, 60), Color.FromArgb(155, 35, 35))
    End Sub

    Private Sub SkillCooldownTrackScrolled(sender As Object, e As EventArgs)
        If _updatingSkillCooldownScale Then
            Return
        End If
        Dim multiplier As Decimal = Math.Max(0.1D, Math.Min(10D, CDec(_skillCooldownTrack.Value) / 10D))
        UpdateSkillCooldownAppearance(multiplier)
        RaiseEvent SkillCooldownScaleChanged(multiplier)
    End Sub

    Private Sub UpdateSkillCooldownAppearance(multiplier As Decimal, Optional applied As Boolean = True)
        multiplier = Math.Max(0.1D, Math.Min(10D, multiplier))
        Dim sliderValue As Integer = Math.Max(1, Math.Min(100, CInt(Math.Round(multiplier * 10D))))
        _updatingSkillCooldownScale = True
        Try
            If _skillCooldownTrack.Value <> sliderValue Then
                _skillCooldownTrack.Value = sliderValue
            End If
        Finally
            _updatingSkillCooldownScale = False
        End Try
        If applied Then
            _skillCooldownLabel.Text = $"SKILL {multiplier:0.0}x"
            _skillCooldownLabel.ForeColor = If(multiplier = 1D, Color.Gainsboro, Color.FromArgb(90, 190, 255))
            _toolTip.SetToolTip(_skillCooldownLabel, $"Skill cooldown scaling is active at {multiplier:0.0}x.")
        Else
            _skillCooldownLabel.Text = $"SKILL {multiplier:0.0}x !"
            _skillCooldownLabel.ForeColor = Color.FromArgb(255, 105, 105)
            _toolTip.SetToolTip(_skillCooldownLabel, "Skill cooldown scaling is not active. See the control-panel log for the exact reason.")
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _timer.Stop()
            RemoveHandler _timer.Tick, AddressOf TickUpdate
            _timer.Dispose()
            _toolTip.Dispose()
            RemoveHandler _toggleButton.MouseDown, AddressOf ToggleButtonMouseDown
            RemoveHandler _toggleButton.MouseMove, AddressOf ToggleButtonMouseMove
            RemoveHandler _toggleButton.MouseUp, AddressOf ToggleButtonMouseUp
            RemoveHandler _toggleButton.MouseLeave, AddressOf ToggleButtonMouseLeave
            RemoveHandler _toggleButton.Paint, AddressOf ToggleButtonPaint
            RemoveHandler _skillCooldownTrack.Scroll, AddressOf SkillCooldownTrackScrolled
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
