Imports System.Drawing.Drawing2D
Imports DrawingPoint = System.Drawing.Point

Friend Class QuizApiKeyDialog
    Inherits Form

    Private ReadOnly _keyBox As TextBox
    Public ReadOnly Property ApiKey As String
        Get
            Return _keyBox.Text.Trim()
        End Get
    End Property

    Public Sub New(existingKey As String)
        Text = "Quiz Solver API Key"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(560, 205)
        BackColor = Color.FromArgb(15, 21, 38)
        ForeColor = Color.FromArgb(222, 233, 250)

        Dim title As New Label With {
            .Text = "OPENAI API KEY",
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(95, 205, 255),
            .AutoSize = True,
            .Location = New DrawingPoint(22, 18)
        }
        Dim help As New Label With {
            .Text = "The key is encrypted for your Windows account before it is remembered. Only the calibrated quiz image is sent to OpenAI.",
            .Font = New Font("Segoe UI", 9.0F),
            .ForeColor = Color.FromArgb(150, 170, 200),
            .Location = New DrawingPoint(22, 50),
            .Size = New Size(515, 42)
        }
        _keyBox = New TextBox With {
            .Location = New DrawingPoint(24, 100),
            .Size = New Size(512, 25),
            .UseSystemPasswordChar = True,
            .Text = If(existingKey, "")
        }
        Dim okButton As New Button With {.Text = "Save key", .Location = New DrawingPoint(344, 151), .Size = New Size(92, 34), .DialogResult = DialogResult.OK}
        Dim cancelButton As New Button With {.Text = "Cancel", .Location = New DrawingPoint(444, 151), .Size = New Size(92, 34), .DialogResult = DialogResult.Cancel}
        AddHandler okButton.Click,
            Sub()
                If String.IsNullOrWhiteSpace(_keyBox.Text) Then
                    MessageBox.Show(Me, "Enter an OpenAI API key.", "Quiz Solver", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    DialogResult = DialogResult.None
                End If
            End Sub
        Controls.AddRange({title, help, _keyBox, okButton, cancelButton})
        AcceptButton = okButton
        CancelButton = cancelButton
    End Sub
End Class

Friend Class QuizCalibrationForm
    Inherits Form

    Private Enum SelectionKind
        Quiz
        Answers
    End Enum

    Private ReadOnly _source As Bitmap
    Private ReadOnly _picture As PictureBox
    Private ReadOnly _instruction As Label
    Private _activeKind As SelectionKind = SelectionKind.Quiz
    Private _quizArea As Rectangle
    Private _answersArea As Rectangle
    Private _dragStart As DrawingPoint
    Private _dragCurrent As DrawingPoint
    Private _dragging As Boolean

    Public ReadOnly Property QuizRegionResult As RectRegion
        Get
            Return New RectRegion(_quizArea.X, _quizArea.Y, _quizArea.Width, _quizArea.Height)
        End Get
    End Property

    Public ReadOnly Property AnswersRegionResult As RectRegion
        Get
            Return New RectRegion(_answersArea.X, _answersArea.Y, _answersArea.Width, _answersArea.Height)
        End Get
    End Property

    Public Sub New(clientScreenshot As Bitmap, existingQuiz As Rectangle, existingAnswers As Rectangle)
        If clientScreenshot Is Nothing Then Throw New ArgumentNullException(NameOf(clientScreenshot))
        _source = New Bitmap(clientScreenshot)
        _quizArea = existingQuiz
        _answersArea = existingAnswers

        Text = "Quiz Overlay Calibration"
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Size(900, 650)
        Size = New Size(1180, 790)
        BackColor = Color.FromArgb(9, 13, 24)
        ForeColor = Color.FromArgb(222, 233, 250)

        Dim top As New Panel With {.Dock = DockStyle.Top, .Height = 92, .Padding = New Padding(16, 12, 16, 8), .BackColor = Color.FromArgb(15, 21, 38)}
        _instruction = New Label With {
            .Text = "1. Drag around the entire quiz panel (question and every possible answer position).",
            .Dock = DockStyle.Top,
            .Height = 28,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.White
        }
        Dim quizButton As New Button With {.Text = "1  Set Quiz Area", .Location = New DrawingPoint(16, 46), .Size = New Size(145, 34), .BackColor = Color.FromArgb(180, 125, 35), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        Dim answersButton As New Button With {.Text = "2  Set Answers Area", .Location = New DrawingPoint(169, 46), .Size = New Size(165, 34), .BackColor = Color.FromArgb(35, 145, 100), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        AddHandler quizButton.Click, Sub() SetSelectionKind(SelectionKind.Quiz)
        AddHandler answersButton.Click, Sub() SetSelectionKind(SelectionKind.Answers)
        top.Controls.AddRange({_instruction, quizButton, answersButton})

        _picture = New PictureBox With {.Dock = DockStyle.Fill, .BackColor = Color.Black, .SizeMode = PictureBoxSizeMode.Zoom, .Image = _source}
        AddHandler _picture.Paint, AddressOf PicturePaint
        AddHandler _picture.MouseDown, AddressOf PictureMouseDown
        AddHandler _picture.MouseMove, AddressOf PictureMouseMove
        AddHandler _picture.MouseUp, AddressOf PictureMouseUp

        Dim bottom As New Panel With {.Dock = DockStyle.Bottom, .Height = 58, .Padding = New Padding(12), .BackColor = Color.FromArgb(15, 21, 38)}
        Dim saveButton As New Button With {.Text = "Save Calibration", .Dock = DockStyle.Right, .Width = 145, .BackColor = Color.FromArgb(35, 145, 100), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        Dim cancelButton As New Button With {.Text = "Cancel", .Dock = DockStyle.Right, .Width = 100, .DialogResult = DialogResult.Cancel}
        Dim legend As New Label With {.Text = "Gold = full quiz   Green = answer buttons only", .Dock = DockStyle.Left, .Width = 390, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.FromArgb(170, 190, 220)}
        AddHandler saveButton.Click, AddressOf SaveClicked
        bottom.Controls.AddRange({saveButton, cancelButton, legend})

        Controls.Add(_picture)
        Controls.Add(bottom)
        Controls.Add(top)
        CancelButton = cancelButton
    End Sub

    Private Sub SetSelectionKind(kind As SelectionKind)
        _activeKind = kind
        If kind = SelectionKind.Quiz Then
            _instruction.Text = "1. Drag around the entire quiz panel (question and every possible answer position)."
        Else
            _instruction.Text = "2. Drag around only the complete answer-button area. Include every row and column where an answer can appear."
        End If
    End Sub

    Private Function ImageDisplayRectangle() As RectangleF
        If _picture.ClientSize.Width <= 0 OrElse _picture.ClientSize.Height <= 0 Then Return RectangleF.Empty
        Dim scale As Single = Math.Min(_picture.ClientSize.Width / CSng(_source.Width), _picture.ClientSize.Height / CSng(_source.Height))
        Dim width As Single = _source.Width * scale
        Dim height As Single = _source.Height * scale
        Return New RectangleF((_picture.ClientSize.Width - width) / 2.0F, (_picture.ClientSize.Height - height) / 2.0F, width, height)
    End Function

    Private Function ClientToImage(point As DrawingPoint) As DrawingPoint
        Dim display = ImageDisplayRectangle()
        If display.Width <= 0 Then Return DrawingPoint.Empty
        Dim x = CInt(Math.Round((point.X - display.X) * _source.Width / display.Width))
        Dim y = CInt(Math.Round((point.Y - display.Y) * _source.Height / display.Height))
        Return New DrawingPoint(Math.Max(0, Math.Min(_source.Width - 1, x)), Math.Max(0, Math.Min(_source.Height - 1, y)))
    End Function

    Private Function ImageToClient(rectangle As Rectangle) As RectangleF
        Dim display = ImageDisplayRectangle()
        Return New RectangleF(
            display.X + rectangle.X * display.Width / _source.Width,
            display.Y + rectangle.Y * display.Height / _source.Height,
            rectangle.Width * display.Width / _source.Width,
            rectangle.Height * display.Height / _source.Height)
    End Function

    Private Sub PictureMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse Not ImageDisplayRectangle().Contains(e.Location) Then Return
        _dragStart = ClientToImage(e.Location)
        _dragCurrent = _dragStart
        _dragging = True
        _picture.Capture = True
    End Sub

    Private Sub PictureMouseMove(sender As Object, e As MouseEventArgs)
        If Not _dragging Then Return
        _dragCurrent = ClientToImage(e.Location)
        _picture.Invalidate()
    End Sub

    Private Sub PictureMouseUp(sender As Object, e As MouseEventArgs)
        If Not _dragging Then Return
        _dragging = False
        _picture.Capture = False
        _dragCurrent = ClientToImage(e.Location)
        Dim selected = NormalizeRectangle(_dragStart, _dragCurrent)
        If selected.Width >= 20 AndAlso selected.Height >= 15 Then
            If _activeKind = SelectionKind.Quiz Then
                _quizArea = selected
                SetSelectionKind(SelectionKind.Answers)
            Else
                _answersArea = selected
            End If
        End If
        _picture.Invalidate()
    End Sub

    Private Shared Function NormalizeRectangle(first As DrawingPoint, second As DrawingPoint) As Rectangle
        Dim left = Math.Min(first.X, second.X)
        Dim top = Math.Min(first.Y, second.Y)
        Return New Rectangle(left, top, Math.Abs(second.X - first.X) + 1, Math.Abs(second.Y - first.Y) + 1)
    End Function

    Private Sub PicturePaint(sender As Object, e As PaintEventArgs)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        DrawArea(e.Graphics, _quizArea, Color.Gold, "QUIZ")
        DrawArea(e.Graphics, _answersArea, Color.Lime, "ANSWERS")
        If _dragging Then
            DrawArea(e.Graphics, NormalizeRectangle(_dragStart, _dragCurrent), If(_activeKind = SelectionKind.Quiz, Color.Gold, Color.Lime), "NEW")
        End If
    End Sub

    Private Sub DrawArea(graphics As Graphics, area As Rectangle, color As Color, label As String)
        If area.Width <= 0 OrElse area.Height <= 0 Then Return
        Dim shown = ImageToClient(area)
        Using pen As New Pen(color, 3.0F), fill As New SolidBrush(Color.FromArgb(32, color)), textFill As New SolidBrush(Color.FromArgb(220, 0, 0, 0)), font As New Font("Segoe UI", 9.0F, FontStyle.Bold)
            graphics.FillRectangle(fill, shown)
            graphics.DrawRectangle(pen, shown.X, shown.Y, shown.Width, shown.Height)
            Dim size = graphics.MeasureString(label, font)
            graphics.FillRectangle(textFill, shown.X + 3, shown.Y + 3, size.Width + 8, size.Height + 3)
            graphics.DrawString(label, font, Brushes.White, shown.X + 7, shown.Y + 4)
        End Using
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        If _quizArea.Width < 20 OrElse _quizArea.Height < 20 Then
            MessageBox.Show(Me, "Set the full quiz area first.", "Quiz Calibration", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If _answersArea.Width < 20 OrElse _answersArea.Height < 15 Then
            MessageBox.Show(Me, "Set the answer-button area.", "Quiz Calibration", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not _quizArea.Contains(_answersArea) Then
            MessageBox.Show(Me, "The answer-button area must be completely inside the full quiz area.", "Quiz Calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _picture IsNot Nothing Then _picture.Image = Nothing
            If _source IsNot Nothing Then _source.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
