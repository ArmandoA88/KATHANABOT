Friend Class InGameBotToggleForm
    Inherits Form

    Public Event ToggleRequested()
    Public Event OverlayLayoutChanged(clientX As Integer, clientY As Integer, overlayWidth As Integer, overlayHeight As Integer)

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const GA_ROOT As UInteger = 2UI
    Private Const DefaultOverlayWidth As Integer = 104
    Private Const DefaultOverlayHeight As Integer = 38
    Private Const OverlayMargin As Integer = 10
    Private Const ResizeGripSize As Integer = 13
    Private Const DragThreshold As Integer = 3
    Private Const MinimumOverlayWidth As Integer = 80
    Private Const MinimumOverlayHeight As Integer = 30
    Private Const MaximumOverlayWidth As Integer = 320
    Private Const MaximumOverlayHeight As Integer = 120

    Private Enum PointerInteraction
        None
        Drag
        Resize
    End Enum

    Private ReadOnly _windowProvider As Func(Of IntPtr)
    Private ReadOnly _runningProvider As Func(Of Boolean)
    Private ReadOnly _timer As New Timer()
    Private ReadOnly _toggleButton As Button
    Private ReadOnly _toolTip As New ToolTip()
    Private _lastRunning As Boolean? = Nothing
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
        runningProvider As Func(Of Boolean),
        clientX As Integer,
        clientY As Integer,
        overlayWidth As Integer,
        overlayHeight As Integer)

        _windowProvider = windowProvider
        _runningProvider = runningProvider
        ApplyLayout(clientX, clientY, overlayWidth, overlayHeight)

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        Size = New Size(_overlayWidth, _overlayHeight)
        BackColor = Color.FromArgb(12, 12, 12)
        Padding = New Padding(2)

        _toggleButton = New Button() With {
            .Dock = DockStyle.Fill,
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
        Controls.Add(_toggleButton)
        _toolTip.SetToolTip(_toggleButton, "Click to turn the bot on/off. Drag to move. Drag the bottom-right grip to resize.")
        UpdateButtonAppearance(False)

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
        If _windowProvider Is Nothing OrElse _runningProvider Is Nothing Then
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

        UpdateButtonAppearance(_runningProvider.Invoke())
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
            If _runningProvider IsNot Nothing Then
                UpdateButtonAppearance(_runningProvider.Invoke())
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

    Private Sub UpdateButtonAppearance(running As Boolean)
        If _lastRunning.HasValue AndAlso _lastRunning.Value = running Then
            Return
        End If

        _lastRunning = running
        _toggleButton.Text = If(running, "BOT ON", "BOT OFF")
        _toggleButton.BackColor = If(running, Color.FromArgb(25, 155, 75), Color.FromArgb(190, 45, 45))
        _toggleButton.FlatAppearance.MouseOverBackColor = If(running, Color.FromArgb(35, 175, 90), Color.FromArgb(215, 60, 60))
        _toggleButton.FlatAppearance.MouseDownBackColor = If(running, Color.FromArgb(20, 125, 60), Color.FromArgb(155, 35, 35))
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
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
