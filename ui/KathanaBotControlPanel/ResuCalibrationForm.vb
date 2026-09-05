Imports DrawingPoint = System.Drawing.Point

Friend Class ResuCalibrationForm
    Inherits Form

    Private ReadOnly _image As Bitmap
    Private ReadOnly _picture As PictureBox
    Private ReadOnly _instructions As Label
    Private ReadOnly _regions As Rectangle()
    Private ReadOnly _points As DrawingPoint()
    Private ReadOnly _labels As String() = {"Target name", "Trade invitation detection", "Chat", "Unreachable text / messages", "Invitation accept", "Open trade window detection", "Trade confirm click"}
    Private _step As Integer
    Private _start As DrawingPoint
    Private _dragging As Boolean
    Public ReadOnly Property Settings As ResuSettings

    Public Sub New(screenshot As Bitmap, settings As ResuSettings)
        Me.Settings = settings
        _image = New Bitmap(screenshot)
        _regions = {ScaleSavedRegion(settings.TargetRegion), ScaleSavedRegion(settings.TradeRegion), ScaleSavedRegion(settings.ChatRegion), ScaleSavedRegion(settings.MessageRegion), ScaleSavedRegion(settings.OpenTradeRegion)}
        _points = {ScalePoint(settings.InvitePoint), ScalePoint(settings.AcceptPoint)}
        Text = "RESU trade overlay calibration"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1150, 800)
        MinimumSize = New Size(700, 500)
        BackColor = Color.FromArgb(15, 21, 38)
        ForeColor = Color.White
        Dim top As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 115, .Padding = New Padding(10), .AutoScroll = True}
        For index = 0 To 6
            Dim selectedStep = index
            Dim button As New Button With {.Text = $"{index + 1}. {_labels(index)}", .AutoSize = True, .Height = 30}
            AddHandler button.Click, Sub() SetStep(selectedStep)
            top.Controls.Add(button)
        Next
        _instructions = New Label With {.AutoSize = False, .Width = 1030, .Height = 42}
        top.SetFlowBreak(top.Controls(top.Controls.Count - 1), True)
        top.Controls.Add(_instructions)
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = Color.Black}
        _picture = New PictureBox With {.Image = _image, .Size = _image.Size, .Location = DrawingPoint.Empty, .SizeMode = PictureBoxSizeMode.Normal}
        scroll.Controls.Add(_picture)
        AddHandler _picture.MouseDown, AddressOf PictureDown
        AddHandler _picture.MouseMove, AddressOf PictureMove
        AddHandler _picture.MouseUp, AddressOf PictureUp
        AddHandler _picture.Paint, AddressOf PicturePaint
        Dim bottom As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 48, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(6)}
        Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .AutoSize = True}
        Dim save As New Button With {.Text = "Save calibration", .AutoSize = True}
        AddHandler save.Click, AddressOf SaveClicked
        bottom.Controls.AddRange({cancel, save})
        Controls.Add(scroll)
        Controls.Add(top)
        Controls.Add(bottom)
        CancelButton = cancel
        SetStep(0)
    End Sub

    Private Function ScaleSavedRegion(region As RectRegion) As Rectangle
        Return QuizImageTools.ScaleRegion(region, Settings.ReferenceWidth, Settings.ReferenceHeight, _image.Width, _image.Height)
    End Function

    Private Function ScalePoint(point As DrawingPoint) As DrawingPoint
        If point.X < 0 OrElse point.Y < 0 OrElse Settings.ReferenceWidth <= 0 OrElse Settings.ReferenceHeight <= 0 Then Return New DrawingPoint(-1, -1)
        Return New DrawingPoint(CInt(point.X * _image.Width / CDbl(Settings.ReferenceWidth)), CInt(point.Y * _image.Height / CDbl(Settings.ReferenceHeight)))
    End Function

    Private Sub SetStep(value As Integer)
        _step = value
        Dim regionIndex = RegionIndexForStep(value)
        If regionIndex >= 0 Then
            _instructions.Text = If(value = 5,
                "Drag overlay 6 around the entire open trade window shown after accepting the invitation. Include its Trade buttons, Rupiah labels, and Cancel button.",
                $"Drag a rectangle around {_labels(value)}. Scroll to reach the rest of the game image.")
        Else
            _instructions.Text = If(value = 6,
                "Left-click overlay 7 on the Trade confirmation button. This point must be inside overlay 6.",
                "Left-click the invitation OK button. This point must be inside overlay 2.")
        End If
        _picture.Invalidate()
    End Sub

    Private Shared Function RegionIndexForStep(stepIndex As Integer) As Integer
        If stepIndex >= 0 AndAlso stepIndex <= 3 Then Return stepIndex
        If stepIndex = 5 Then Return 4
        Return -1
    End Function

    Private Shared Function PointIndexForStep(stepIndex As Integer) As Integer
        If stepIndex = 4 Then Return 0
        If stepIndex = 6 Then Return 1
        Return -1
    End Function

    Private Function Bounded(point As DrawingPoint) As DrawingPoint
        Return New DrawingPoint(Math.Clamp(point.X, 0, _image.Width - 1), Math.Clamp(point.Y, 0, _image.Height - 1))
    End Function

    Private Sub PictureDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        Dim pointIndex = PointIndexForStep(_step)
        If pointIndex >= 0 Then
            _points(pointIndex) = Bounded(e.Location)
            If _step < 6 Then SetStep(_step + 1)
            _picture.Invalidate()
            Return
        End If
        Dim regionIndex = RegionIndexForStep(_step)
        If regionIndex < 0 Then Return
        _start = Bounded(e.Location)
        _dragging = True
        _picture.Capture = True
    End Sub

    Private Sub PictureMove(sender As Object, e As MouseEventArgs)
        If Not _dragging Then Return
        Dim current = Bounded(e.Location)
        Dim regionIndex = RegionIndexForStep(_step)
        If regionIndex < 0 Then Return
        _regions(regionIndex) = New Rectangle(Math.Min(_start.X, current.X), Math.Min(_start.Y, current.Y), Math.Abs(current.X - _start.X) + 1, Math.Abs(current.Y - _start.Y) + 1)
        _picture.Invalidate()
    End Sub

    Private Sub PictureUp(sender As Object, e As MouseEventArgs)
        If Not _dragging Then Return
        PictureMove(sender, e)
        _dragging = False
        _picture.Capture = False
        SetStep(Math.Min(6, _step + 1))
    End Sub

    Private Sub PicturePaint(sender As Object, e As PaintEventArgs)
        Dim regionSteps As Integer() = {0, 1, 2, 3, 5}
        For index = 0 To _regions.Length - 1
            Dim rectangle = _regions(index)
            If rectangle.IsEmpty Then Continue For
            Dim stepIndex = regionSteps(index)
            Using pen As New Pen(If(stepIndex = _step, Color.Gold, Color.LimeGreen), 2)
                e.Graphics.DrawRectangle(pen, rectangle)
                e.Graphics.FillRectangle(Brushes.Black, rectangle.X, rectangle.Y, Math.Min(rectangle.Width, 240), 20)
                e.Graphics.DrawString($"{stepIndex + 1}. {_labels(stepIndex)}", Font, Brushes.White, rectangle.Location)
            End Using
        Next
        For index = 0 To 1
            Dim point = _points(index)
            If point.X < 0 Then Continue For
            Dim stepIndex = If(index = 0, 4, 6)
            Using pen As New Pen(Color.DeepSkyBlue, 3)
                e.Graphics.DrawEllipse(pen, point.X - 8, point.Y - 8, 16, 16)
                e.Graphics.DrawString($"{stepIndex + 1}. {_labels(stepIndex)}", Font, Brushes.DeepSkyBlue, point.X + 10, point.Y)
            End Using
        Next
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        If _regions.Any(Function(rect) rect.Width < 15 OrElse rect.Height < 10) OrElse Not _regions(1).Contains(_points(0)) OrElse Not _regions(4).Contains(_points(1)) Then
            MessageBox.Show(Me, "Set all five detection regions and both click points. Overlay 5 must be inside overlay 2, and overlay 7 must be inside overlay 6.", "RESU calibration")
            Return
        End If
        Settings.ReferenceWidth = _image.Width
        Settings.ReferenceHeight = _image.Height
        Settings.TargetRegion = ToRegion(_regions(0))
        Settings.TradeRegion = ToRegion(_regions(1))
        Settings.ChatRegion = ToRegion(_regions(2))
        Settings.MessageRegion = ToRegion(_regions(3))
        Settings.OpenTradeRegion = ToRegion(_regions(4))
        Settings.InvitePoint = _points(0)
        Settings.AcceptPoint = _points(1)
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Function ToRegion(rect As Rectangle) As RectRegion
        Return New RectRegion(rect.X, rect.Y, rect.Width, rect.Height)
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _picture.Image = Nothing
            _image.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
