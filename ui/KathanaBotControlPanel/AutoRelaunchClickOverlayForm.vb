Imports System.Drawing.Drawing2D
Imports System.Linq

Friend Class AutoRelaunchOverlayStep
    Public Property StepNumber As Integer
    Public Property X As Integer
    Public Property Y As Integer
    Public Property RegionWidth As Integer
    Public Property RegionHeight As Integer
    Public Property DelaySeconds As Decimal
    Public Property TimingLabel As String = ""
    Public Property Description As String = ""
    Public Property MarkerColor As Color = Color.FromArgb(235, 20, 125, 205)
End Class

Friend Class AutoRelaunchClickOverlayForm
    Inherits Form

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const WS_EX_TRANSPARENT As Integer = &H20

    Private ReadOnly _windowProvider As Func(Of IntPtr)
    Private ReadOnly _stepsProvider As Func(Of List(Of AutoRelaunchOverlayStep))
    Private ReadOnly _timer As New Timer()
    Private _steps As New List(Of AutoRelaunchOverlayStep)()

    Public Sub New(windowProvider As Func(Of IntPtr), stepsProvider As Func(Of List(Of AutoRelaunchOverlayStep)))
        _windowProvider = windowProvider
        _stepsProvider = stepsProvider
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        BackColor = Color.Magenta
        TransparencyKey = Color.Magenta
        DoubleBuffered = True

        _timer.Interval = 120
        AddHandler _timer.Tick, AddressOf TickUpdate
        _timer.Start()
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            ' The bot's screen capture falls back to a raw desktop BitBlt (CopyFromScreen) for games
            ' that don't support PrintWindow, which would otherwise bake this always-on-top marker
            ' directly into the OCR crop it draws over (e.g. right on top of a dialog's OK button).
            ' WDA_EXCLUDEFROMCAPTURE keeps it visible on the physical display but invisible to any
            ' screen-capture API, so it can no longer corrupt what the bot reads.
            NativeMethods.SetWindowDisplayAffinity(Me.Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE)
        Catch
        End Try
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW Or WS_EX_LAYERED Or WS_EX_TRANSPARENT
            Return cp
        End Get
    End Property

    Private Sub TickUpdate(sender As Object, e As EventArgs)
        If _windowProvider Is Nothing OrElse _stepsProvider Is Nothing Then
            Hide()
            Return
        End If

        Dim hwnd As IntPtr = _windowProvider.Invoke()
        If hwnd = IntPtr.Zero OrElse Not NativeMethods.IsWindowVisible(hwnd) OrElse NativeMethods.IsIconic(hwnd) Then
            Hide()
            Return
        End If

        Dim clientRect As NativeMethods.RECT
        Dim origin As New NativeMethods.POINT With {.X = 0, .Y = 0}
        If Not NativeMethods.GetClientRect(hwnd, clientRect) OrElse Not NativeMethods.ClientToScreen(hwnd, origin) Then
            Hide()
            Return
        End If

        Dim width As Integer = clientRect.Right - clientRect.Left
        Dim height As Integer = clientRect.Bottom - clientRect.Top
        If width <= 0 OrElse height <= 0 Then
            Hide()
            Return
        End If

        _steps = _stepsProvider.Invoke().
            Where(Function(stepInfo) stepInfo IsNot Nothing).
            Select(Function(stepInfo) New AutoRelaunchOverlayStep With {
                .StepNumber = stepInfo.StepNumber,
                .X = stepInfo.X,
                .Y = stepInfo.Y,
                .RegionWidth = stepInfo.RegionWidth,
                .RegionHeight = stepInfo.RegionHeight,
                .DelaySeconds = stepInfo.DelaySeconds,
                .TimingLabel = If(stepInfo.TimingLabel, ""),
                .Description = If(stepInfo.Description, ""),
                .MarkerColor = stepInfo.MarkerColor
            }).
            ToList()

        If _steps.Count = 0 Then
            Hide()
            Return
        End If

        Dim targetBounds As New Rectangle(origin.X, origin.Y, width, height)
        If Bounds <> targetBounds Then
            Bounds = targetBounds
        End If
        If Not Visible Then
            Show()
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _steps Is Nothing OrElse _steps.Count = 0 Then
            Return
        End If

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using pathPen As New Pen(Color.FromArgb(220, 80, 210, 255), 3.0F),
              markerPen As New Pen(Color.White, 2.0F),
              panelBrush As New SolidBrush(Color.FromArgb(210, 10, 10, 10)),
              textBrush As New SolidBrush(Color.White),
              detailBrush As New SolidBrush(Color.FromArgb(255, 255, 225, 120)),
              stepFont As New Font("Segoe UI", 10.0F, FontStyle.Bold),
              detailFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)

            pathPen.CustomEndCap = New AdjustableArrowCap(5.0F, 6.0F, True)
            For i As Integer = 0 To _steps.Count - 2
                Dim current As AutoRelaunchOverlayStep = _steps(i)
                Dim following As AutoRelaunchOverlayStep = _steps(i + 1)
                e.Graphics.DrawLine(pathPen, current.X, current.Y, following.X, following.Y)
            Next

            For Each stepInfo As AutoRelaunchOverlayStep In _steps
                If stepInfo.RegionWidth > 0 AndAlso stepInfo.RegionHeight > 0 Then
                    Dim regionRect As New Rectangle(stepInfo.X - (stepInfo.RegionWidth \ 2), stepInfo.Y - (stepInfo.RegionHeight \ 2), stepInfo.RegionWidth, stepInfo.RegionHeight)
                    Using regionPen As New Pen(stepInfo.MarkerColor, 3.0F)
                        regionPen.DashStyle = DashStyle.Dash
                        e.Graphics.DrawRectangle(regionPen, regionRect)
                    End Using
                End If
                Dim markerRect As New Rectangle(stepInfo.X - 15, stepInfo.Y - 15, 30, 30)
                Using markerBrush As New SolidBrush(stepInfo.MarkerColor)
                    e.Graphics.FillEllipse(markerBrush, markerRect)
                End Using
                e.Graphics.DrawEllipse(markerPen, markerRect)

                Dim numberText As String = stepInfo.StepNumber.ToString()
                Dim numberSize As SizeF = e.Graphics.MeasureString(numberText, stepFont)
                e.Graphics.DrawString(numberText, stepFont, textBrush, stepInfo.X - (numberSize.Width / 2.0F), stepInfo.Y - (numberSize.Height / 2.0F))

                Dim timingText As String = If(String.IsNullOrWhiteSpace(stepInfo.TimingLabel), $"{stepInfo.DelaySeconds:0.###}s delay", stepInfo.TimingLabel.Trim())
                Dim delayText As String = $"Step {stepInfo.StepNumber}  •  ({stepInfo.X}, {stepInfo.Y})  •  {timingText}"
                If Not String.IsNullOrWhiteSpace(stepInfo.Description) Then
                    delayText &= $"  •  {stepInfo.Description.Trim()}"
                End If
                Dim detailSize As SizeF = e.Graphics.MeasureString(delayText, detailFont)
                Dim labelWidth As Integer = CInt(Math.Ceiling(detailSize.Width)) + 12
                Dim labelHeight As Integer = CInt(Math.Ceiling(detailSize.Height)) + 6
                Dim labelX As Integer = stepInfo.X + 20
                If labelX + labelWidth > ClientSize.Width Then
                    labelX = stepInfo.X - labelWidth - 20
                End If
                labelX = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Width - labelWidth), labelX))
                Dim labelY As Integer = Math.Max(0, Math.Min(Math.Max(0, ClientSize.Height - labelHeight), stepInfo.Y - (labelHeight \ 2)))
                Dim labelRect As New Rectangle(labelX, labelY, labelWidth, labelHeight)
                e.Graphics.FillRectangle(panelBrush, labelRect)
                e.Graphics.DrawRectangle(markerPen, labelRect)
                e.Graphics.DrawString(delayText, detailFont, detailBrush, labelRect.X + 6, labelRect.Y + 3)
            Next
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _timer.Stop()
            RemoveHandler _timer.Tick, AddressOf TickUpdate
            _timer.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
