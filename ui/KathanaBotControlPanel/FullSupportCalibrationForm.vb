Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms
Imports DrawingPoint = System.Drawing.Point

Public Class FullSupportCalibrationForm
    Inherits Form

    Private ReadOnly _fullFrame As Bitmap
    Private _partyRect As RectRegion
    Private ReadOnly _members As New List(Of FullSupportPartyMember)()
    Private _stage As Integer = 1
    Private _dragging As Boolean
    Private _dragStart As DrawingPoint
    Private _dragOriginal As Rectangle
    Private _selectedMember As Integer
    Private _syncing As Boolean
    Private _memberEditorHandlersWired As Boolean
    Private _memberEditorLoaded As Boolean
    Private _membersReferenceWidth As Integer
    Private _membersReferenceHeight As Integer

    Private ReadOnly _titleLabel As New Label()
    Private ReadOnly _hintLabel As New Label()
    Private ReadOnly _preview As New PictureBox()
    Private ReadOnly _rightPanel As New Panel()
    Private ReadOnly _membersList As New ListBox()
    Private ReadOnly _memberCount As New NumericUpDown()
    Private ReadOnly _memberName As New TextBox()
    Private ReadOnly _memberEnabled As New CheckBox()
    Private ReadOnly _xValue As New NumericUpDown()
    Private ReadOnly _yValue As New NumericUpDown()
    Private ReadOnly _wValue As New NumericUpDown()
    Private ReadOnly _hValue As New NumericUpDown()
    Private ReadOnly _selectXValue As New NumericUpDown()
    Private ReadOnly _selectYValue As New NumericUpDown()
    Private ReadOnly _backButton As New Button()
    Private ReadOnly _nextButton As New Button()

    Public Sub New(fullFrame As Bitmap, currentPartyRect As RectRegion, currentMembers As IEnumerable(Of FullSupportPartyMember))
        If fullFrame Is Nothing Then
            Throw New ArgumentNullException(NameOf(fullFrame))
        End If
        _fullFrame = DirectCast(fullFrame.Clone(), Bitmap)
        Dim fallback As New RectRegion(0, 24, Math.Min(190, _fullFrame.Width), Math.Min(244, Math.Max(1, _fullFrame.Height - 24)))
        _partyRect = CloneAndClampRect(If(currentPartyRect, fallback), _fullFrame.Width, _fullFrame.Height)
        _membersReferenceWidth = _partyRect.W
        _membersReferenceHeight = _partyRect.H
        If currentMembers IsNot Nothing Then
            For Each member As FullSupportPartyMember In currentMembers.Take(7)
                If member IsNot Nothing Then
                    _members.Add(CloneMember(member))
                End If
            Next
        End If

        Text = "Full Support Party Calibration"
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Size(980, 700)
        Size = New Size(1180, 780)
        BackColor = Color.FromArgb(8, 14, 28)
        ForeColor = Color.FromArgb(235, 242, 255)
        Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)

        BuildUi()
        ShowStage(1)
    End Sub

    Public ReadOnly Property PartyRectResult As RectRegion
        Get
            Return New RectRegion(_partyRect.X, _partyRect.Y, _partyRect.W, _partyRect.H)
        End Get
    End Property

    Public ReadOnly Property MembersResult As List(Of FullSupportPartyMember)
        Get
            Return _members.Select(Function(member) CloneMember(member)).ToList()
        End Get
    End Property

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _fullFrame.Dispose()
            If _preview.Image IsNot Nothing Then
                _preview.Image.Dispose()
                _preview.Image = Nothing
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Sub BuildUi()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(20),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 78.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(root)

        Dim header As New Panel() With {.Dock = DockStyle.Fill, .BackColor = BackColor}
        _titleLabel.SetBounds(0, 0, 1000, 30)
        _titleLabel.Font = New Font("Segoe UI Semibold", 16.0F, FontStyle.Bold)
        _titleLabel.ForeColor = Color.White
        _hintLabel.SetBounds(0, 38, 1060, 28)
        _hintLabel.ForeColor = Color.FromArgb(142, 170, 214)
        header.Controls.Add(_titleLabel)
        header.Controls.Add(_hintLabel)
        root.Controls.Add(header, 0, 0)

        Dim content As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = BackColor}
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 70.0F))
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        root.Controls.Add(content, 0, 1)

        Dim previewHost As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(10), .BackColor = Color.FromArgb(15, 26, 48)}
        _preview.Dock = DockStyle.Fill
        _preview.BackColor = Color.Black
        _preview.SizeMode = PictureBoxSizeMode.Zoom
        AddHandler _preview.Paint, AddressOf PreviewPaint
        AddHandler _preview.MouseDown, AddressOf PreviewMouseDown
        AddHandler _preview.MouseMove, AddressOf PreviewMouseMove
        AddHandler _preview.MouseUp, AddressOf PreviewMouseUp
        previewHost.Controls.Add(_preview)
        content.Controls.Add(previewHost, 0, 0)

        _rightPanel.Dock = DockStyle.Fill
        _rightPanel.Padding = New Padding(16, 4, 0, 4)
        _rightPanel.BackColor = BackColor
        content.Controls.Add(_rightPanel, 1, 0)

        Dim footer As New Panel() With {.Dock = DockStyle.Fill, .BackColor = BackColor}
        _backButton.Text = "Back"
        _backButton.SetBounds(0, 12, 120, 38)
        StyleButton(_backButton, Color.FromArgb(32, 50, 78))
        AddHandler _backButton.Click, Sub() ShowStage(1)
        _nextButton.Text = "Next: member HP bars"
        _nextButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        _nextButton.SetBounds(820, 12, 240, 38)
        StyleButton(_nextButton, Color.FromArgb(22, 150, 111))
        AddHandler _nextButton.Click, AddressOf NextClicked
        AddHandler footer.Resize, Sub() _nextButton.Left = Math.Max(130, footer.ClientSize.Width - _nextButton.Width)
        footer.Controls.Add(_backButton)
        footer.Controls.Add(_nextButton)
        root.Controls.Add(footer, 0, 2)
    End Sub

    Private Sub ShowStage(stage As Integer)
        _stage = stage
        _rightPanel.Controls.Clear()
        _backButton.Visible = stage = 2
        If stage = 1 Then
            _titleLabel.Text = "1  Select the complete party panel"
            _hintLabel.Text = "Drag over the party list in the game screenshot. Include every member name and the full red HP bars."
            _nextButton.Text = "Next: member HP bars"
            ReplacePreviewImage(DirectCast(_fullFrame.Clone(), Bitmap))
            BuildPartyAreaHelp()
        Else
            _titleLabel.Text = "2  Calibrate each member HP bar"
            _hintLabel.Text = "Move rows individually, or use ALL X and GAP for optional bulk alignment. Width and height changes resize every row together."
            _nextButton.Text = "Save calibration"
            ScaleMembersToCurrentPartyRect()
            EnsureMemberCount(Math.Max(1, If(_members.Count = 0, 7, _members.Count)), _members.Count = 0)
            NormalizeMemberRectangleSizes()
            RefreshPartyCrop()
            BuildMemberEditor()
            _memberEditorLoaded = False
            SelectMember(Math.Min(_selectedMember, _members.Count - 1))
        End If
        _preview.Invalidate()
    End Sub

    Private Sub BuildPartyAreaHelp()
        Dim box As New Panel() With {.Dock = DockStyle.Top, .Height = 220, .BackColor = Color.FromArgb(17, 29, 52), .Padding = New Padding(16)}
        Dim label As New Label() With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(190, 210, 239),
            .Text = "PARTY AREA" & Environment.NewLine & Environment.NewLine &
                    "• Drag from one corner of the party list to the opposite corner." & Environment.NewLine &
                    "• Keep the red HP bars inside the selection." & Environment.NewLine &
                    "• The selection uses game-client coordinates, so moving the game window later is safe." & Environment.NewLine & Environment.NewLine &
                    $"Current: X {_partyRect.X}, Y {_partyRect.Y}, W {_partyRect.W}, H {_partyRect.H}",
            .AutoSize = False
        }
        box.Controls.Add(label)
        _rightPanel.Controls.Add(box)
    End Sub

    Private Sub BuildMemberEditor()
        Dim editor As New TableLayoutPanel() With {.Dock = DockStyle.Top, .Height = 570, .ColumnCount = 2, .RowCount = 16, .BackColor = BackColor}
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        For row As Integer = 0 To 15
            editor.RowStyles.Add(New RowStyle(SizeType.Absolute, If(row = 2, 150.0F, 30.0F)))
        Next

        AddEditorLabel(editor, "Members", 0)
        ConfigureNumeric(_memberCount, 1, 7)
        _memberCount.Value = Math.Max(1, Math.Min(7, _members.Count))
        If Not _memberEditorHandlersWired Then AddHandler _memberCount.ValueChanged, AddressOf MemberCountChanged
        editor.Controls.Add(_memberCount, 1, 0)

        Dim autoButton As New Button() With {.Text = "Auto place rows", .Dock = DockStyle.Fill}
        StyleButton(autoButton, Color.FromArgb(39, 75, 119))
        AddHandler autoButton.Click, Sub()
                                         CommitMemberEditor()
                                         AutoPlaceMembers(CInt(_memberCount.Value))
                                         RefreshMembersList()
                                         _memberEditorLoaded = False
                                         SelectMember(Math.Min(_selectedMember, _members.Count - 1))
                                     End Sub
        editor.SetColumnSpan(autoButton, 2)
        editor.Controls.Add(autoButton, 0, 1)

        _membersList.Dock = DockStyle.Fill
        _membersList.BackColor = Color.FromArgb(13, 23, 43)
        _membersList.ForeColor = Color.White
        _membersList.BorderStyle = BorderStyle.FixedSingle
        If Not _memberEditorHandlersWired Then AddHandler _membersList.SelectedIndexChanged, Sub() If Not _syncing Then SelectMember(_membersList.SelectedIndex)
        editor.SetColumnSpan(_membersList, 2)
        editor.Controls.Add(_membersList, 0, 2)

        AddEditorLabel(editor, "Name", 3)
        _memberName.Dock = DockStyle.Fill
        If Not _memberEditorHandlersWired Then AddHandler _memberName.TextChanged, AddressOf MemberEditorChanged
        editor.Controls.Add(_memberName, 1, 3)
        _memberEnabled.Text = "Monitor this member"
        _memberEnabled.Dock = DockStyle.Fill
        _memberEnabled.ForeColor = Color.FromArgb(80, 230, 184)
        If Not _memberEditorHandlersWired Then AddHandler _memberEnabled.CheckedChanged, AddressOf MemberEditorChanged
        editor.SetColumnSpan(_memberEnabled, 2)
        editor.Controls.Add(_memberEnabled, 0, 4)

        AddNumericRow(editor, "HP X (this row)", _xValue, 5, Not _memberEditorHandlersWired)
        AddNumericRow(editor, "HP Y (this row)", _yValue, 6, Not _memberEditorHandlersWired)
        AddNumericRow(editor, "HP width (all rows)", _wValue, 7, Not _memberEditorHandlersWired)
        AddNumericRow(editor, "HP height (all rows)", _hValue, 8, Not _memberEditorHandlersWired)
        AddNumericRow(editor, "Click X", _selectXValue, 9, Not _memberEditorHandlersWired)
        AddNumericRow(editor, "Click Y", _selectYValue, 10, Not _memberEditorHandlersWired)

        Dim nudgeGrid As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 2, .Margin = New Padding(0, 8, 0, 0)}
        For i As Integer = 0 To 3
            nudgeGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        For Each spec In New (String, Integer, Integer, Integer)() {
            ("X −", 0, 0, -1), ("X +", 1, 0, 1), ("Y −", 2, 0, -1), ("Y +", 3, 0, 1),
            ("W −", 0, 1, -1), ("W +", 1, 1, 1), ("H −", 2, 1, -1), ("H +", 3, 1, 1)}
            Dim button As New Button() With {.Text = spec.Item1, .Dock = DockStyle.Fill, .Margin = New Padding(2)}
            StyleButton(button, Color.FromArgb(31, 54, 87))
            Dim caption As String = spec.Item1
            Dim amount As Integer = spec.Item4
            AddHandler button.Click, Sub() Nudge(caption(0), amount)
            nudgeGrid.Controls.Add(button, spec.Item2, spec.Item3)
        Next
        editor.SetColumnSpan(nudgeGrid, 2)
        editor.Controls.Add(nudgeGrid, 0, 11)
        editor.SetRowSpan(nudgeGrid, 2)

        Dim bulkGrid As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 1, .Margin = New Padding(0, 2, 0, 0)}
        For i As Integer = 0 To 3
            bulkGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        Dim allXMinus As New Button() With {.Text = "ALL X −", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        Dim allXPlus As New Button() With {.Text = "ALL X +", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        Dim gapMinus As New Button() With {.Text = "GAP −", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        Dim gapPlus As New Button() With {.Text = "GAP +", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        For Each button As Button In New Button() {allXMinus, allXPlus, gapMinus, gapPlus}
            StyleButton(button, Color.FromArgb(49, 72, 112))
        Next
        AddHandler allXMinus.Click, Sub() NudgeAllBarsHorizontal(-1)
        AddHandler allXPlus.Click, Sub() NudgeAllBarsHorizontal(1)
        AddHandler gapMinus.Click, Sub() NudgeAllBarSpacing(-1)
        AddHandler gapPlus.Click, Sub() NudgeAllBarSpacing(1)
        bulkGrid.Controls.Add(allXMinus, 0, 0)
        bulkGrid.Controls.Add(allXPlus, 1, 0)
        bulkGrid.Controls.Add(gapMinus, 2, 0)
        bulkGrid.Controls.Add(gapPlus, 3, 0)
        editor.SetColumnSpan(bulkGrid, 2)
        editor.Controls.Add(bulkGrid, 0, 13)

        Dim note As New Label() With {
            .Text = "X/Y adjust this row; W/H resize all rows. ALL X shifts every bar together. GAP changes every vertical bar-to-bar offset while keeping custom differences.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(142, 170, 214)
        }
        editor.SetColumnSpan(note, 2)
        editor.Controls.Add(note, 0, 14)
        editor.SetRowSpan(note, 2)
        _rightPanel.Controls.Add(editor)
        _memberEditorHandlersWired = True
        RefreshMembersList()
    End Sub

    Private Sub AddNumericRow(editor As TableLayoutPanel, caption As String, control As NumericUpDown, row As Integer, wireHandler As Boolean)
        AddEditorLabel(editor, caption, row)
        ConfigureNumeric(control, 0, 2000)
        If wireHandler Then AddHandler control.ValueChanged, AddressOf MemberEditorChanged
        editor.Controls.Add(control, 1, row)
    End Sub

    Private Shared Sub AddEditorLabel(editor As TableLayoutPanel, caption As String, row As Integer)
        editor.Controls.Add(New Label() With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.FromArgb(164, 190, 227)}, 0, row)
    End Sub

    Private Shared Sub ConfigureNumeric(control As NumericUpDown, minimum As Integer, maximum As Integer)
        control.Minimum = minimum
        control.Maximum = maximum
        control.Dock = DockStyle.Fill
        control.BackColor = Color.FromArgb(18, 31, 56)
        control.ForeColor = Color.White
        control.BorderStyle = BorderStyle.FixedSingle
    End Sub

    Private Shared Sub StyleButton(button As Button, color As Color)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderSize = 0
        button.BackColor = color
        button.ForeColor = Color.White
        button.Cursor = Cursors.Hand
    End Sub

    Private Sub NextClicked(sender As Object, e As EventArgs)
        If _stage = 1 Then
            If _partyRect.W < 40 OrElse _partyRect.H < 25 Then
                MessageBox.Show(Me, "Select the complete party panel before continuing.", "Full Support", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            ShowStage(2)
            Return
        End If

        CommitMemberEditor()
        If _members.Count = 0 OrElse Not _members.Any(Function(member) member.Enabled) Then
            MessageBox.Show(Me, "Enable and calibrate at least one party member.", "Full Support", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub PreviewMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        Dim imagePoint As DrawingPoint
        If Not TryControlToImagePoint(e.Location, imagePoint) Then Return
        _dragging = True
        _dragStart = imagePoint
        If _stage = 1 Then
            _dragOriginal = Rectangle.Empty
            _partyRect = New RectRegion(imagePoint.X, imagePoint.Y, 1, 1)
        ElseIf _selectedMember >= 0 AndAlso _selectedMember < _members.Count Then
            _dragOriginal = ToRectangle(_members(_selectedMember).HpBarRect)
        End If
        _preview.Capture = True
    End Sub

    Private Sub PreviewMouseMove(sender As Object, e As MouseEventArgs)
        If Not _dragging Then Return
        Dim imagePoint As DrawingPoint
        If Not TryControlToImagePoint(e.Location, imagePoint) Then Return
        If _stage = 1 Then
            Dim left As Integer = Math.Min(_dragStart.X, imagePoint.X)
            Dim top As Integer = Math.Min(_dragStart.Y, imagePoint.Y)
            _partyRect = New RectRegion(left, top, Math.Max(1, Math.Abs(imagePoint.X - _dragStart.X)), Math.Max(1, Math.Abs(imagePoint.Y - _dragStart.Y)))
        ElseIf _selectedMember >= 0 AndAlso _selectedMember < _members.Count Then
            Dim dx As Integer = imagePoint.X - _dragStart.X
            Dim dy As Integer = imagePoint.Y - _dragStart.Y
            Dim moved As Rectangle = _dragOriginal
            moved.X += dx
            moved.Y += dy
            moved.X = Math.Max(0, Math.Min(Math.Max(0, _partyRect.W - moved.Width), moved.X))
            moved.Y = Math.Max(0, Math.Min(Math.Max(0, _partyRect.H - moved.Height), moved.Y))
            _members(_selectedMember).HpBarRect = New RectRegion(moved.X, moved.Y, moved.Width, moved.Height)
            LoadMemberEditor()
        End If
        _preview.Invalidate()
    End Sub

    Private Sub PreviewMouseUp(sender As Object, e As MouseEventArgs)
        _dragging = False
        _preview.Capture = False
    End Sub

    Private Sub PreviewPaint(sender As Object, e As PaintEventArgs)
        If _preview.Image Is Nothing Then Return
        Dim display As RectangleF = GetImageDisplayRectangle()
        If display.Width <= 0 OrElse display.Height <= 0 Then Return
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        If _stage = 1 Then
            DrawImageRect(e.Graphics, ToRectangle(_partyRect), display, _fullFrame.Size, Color.FromArgb(58, 226, 174), 2.5F)
        Else
            For index As Integer = 0 To _members.Count - 1
                Dim member As FullSupportPartyMember = _members(index)
                Dim color As Color = If(index = _selectedMember, Color.FromArgb(255, 203, 74), Color.FromArgb(58, 226, 174))
                DrawImageRect(e.Graphics, ToRectangle(member.HpBarRect), display, _preview.Image.Size, color, If(index = _selectedMember, 2.6F, 1.5F))
                Dim clickPoint As PointF = ImageToControlPoint(New DrawingPoint(member.SelectPointX, member.SelectPointY), display, _preview.Image.Size)
                Using brush As New SolidBrush(Color.FromArgb(45, 204, 255))
                    e.Graphics.FillEllipse(brush, clickPoint.X - 4, clickPoint.Y - 4, 8, 8)
                End Using
            Next
        End If
    End Sub

    Private Shared Sub DrawImageRect(graphics As Graphics, imageRect As Rectangle, display As RectangleF, imageSize As Size, color As Color, width As Single)
        Dim sx As Single = display.Width / Math.Max(1, imageSize.Width)
        Dim sy As Single = display.Height / Math.Max(1, imageSize.Height)
        Dim target As New RectangleF(display.Left + imageRect.X * sx, display.Top + imageRect.Y * sy, Math.Max(1, imageRect.Width * sx), Math.Max(1, imageRect.Height * sy))
        Using fill As New SolidBrush(Color.FromArgb(36, color)), pen As New Pen(color, width)
            graphics.FillRectangle(fill, target)
            graphics.DrawRectangle(pen, target.X, target.Y, target.Width, target.Height)
        End Using
    End Sub

    Private Sub RefreshPartyCrop()
        Dim cropRect As Rectangle = ToRectangle(_partyRect)
        cropRect.Intersect(New Rectangle(0, 0, _fullFrame.Width, _fullFrame.Height))
        Dim crop As New Bitmap(Math.Max(1, cropRect.Width), Math.Max(1, cropRect.Height))
        Using g As Graphics = Graphics.FromImage(crop)
            g.DrawImage(_fullFrame, New Rectangle(0, 0, crop.Width, crop.Height), cropRect, GraphicsUnit.Pixel)
        End Using
        ReplacePreviewImage(crop)
    End Sub

    Private Sub ReplacePreviewImage(image As Bitmap)
        Dim oldImage As Image = _preview.Image
        _preview.Image = image
        If oldImage IsNot Nothing Then oldImage.Dispose()
    End Sub

    Private Sub EnsureMemberCount(count As Integer, autoPlaceWhenEmpty As Boolean)
        count = Math.Max(1, Math.Min(7, count))
        While _members.Count < count
            _members.Add(New FullSupportPartyMember() With {.Name = $"Member {_members.Count + 1}"})
        End While
        While _members.Count > count
            _members.RemoveAt(_members.Count - 1)
        End While
        If autoPlaceWhenEmpty Then AutoPlaceMembers(count)
    End Sub

    Private Sub AutoPlaceMembers(count As Integer)
        EnsureMemberCount(count, False)
        Dim averageRowHeight As Integer = Math.Max(8, _partyRect.H \ Math.Max(1, count))
        Dim sharedWidth As Integer = Math.Max(2, Math.Min(_partyRect.W, Math.Max(8, _partyRect.W - 10)))
        Dim sharedHeight As Integer = Math.Max(2, Math.Min(_partyRect.H, Math.Max(4, Math.Min(10, CInt(Math.Round(averageRowHeight * 0.22R))))))
        For index As Integer = 0 To count - 1
            Dim rowTop As Integer = CInt(Math.Floor(index * _partyRect.H / CDbl(count)))
            Dim rowBottom As Integer = CInt(Math.Floor((index + 1) * _partyRect.H / CDbl(count)))
            Dim rowHeight As Integer = Math.Max(8, rowBottom - rowTop)
            Dim hpY As Integer = Math.Min(Math.Max(0, _partyRect.H - sharedHeight), rowTop + Math.Max(4, CInt(Math.Round(rowHeight * 0.52R))))
            _members(index).Name = If(String.IsNullOrWhiteSpace(_members(index).Name), $"Member {index + 1}", _members(index).Name)
            _members(index).Enabled = True
            _members(index).HpBarRect = New RectRegion(Math.Min(5, Math.Max(0, _partyRect.W - sharedWidth)), hpY, sharedWidth, sharedHeight)
            _members(index).SelectPointX = Math.Max(0, _partyRect.W \ 2)
            _members(index).SelectPointY = Math.Min(_partyRect.H - 1, rowTop + rowHeight \ 2)
        Next
        _membersReferenceWidth = _partyRect.W
        _membersReferenceHeight = _partyRect.H
    End Sub

    Private Sub ScaleMembersToCurrentPartyRect()
        Dim oldWidth As Integer = Math.Max(1, _membersReferenceWidth)
        Dim oldHeight As Integer = Math.Max(1, _membersReferenceHeight)
        If oldWidth = _partyRect.W AndAlso oldHeight = _partyRect.H Then Return
        Dim sx As Double = _partyRect.W / CDbl(oldWidth)
        Dim sy As Double = _partyRect.H / CDbl(oldHeight)
        For Each member As FullSupportPartyMember In _members
            Dim hp As RectRegion = If(member.HpBarRect, New RectRegion(5, 18, Math.Max(8, oldWidth - 10), 7))
            member.HpBarRect = New RectRegion(
                Math.Max(0, CInt(Math.Round(hp.X * sx))),
                Math.Max(0, CInt(Math.Round(hp.Y * sy))),
                Math.Max(2, CInt(Math.Round(hp.W * sx))),
                Math.Max(2, CInt(Math.Round(hp.H * sy))))
            member.SelectPointX = Math.Max(0, Math.Min(_partyRect.W - 1, CInt(Math.Round(member.SelectPointX * sx))))
            member.SelectPointY = Math.Max(0, Math.Min(_partyRect.H - 1, CInt(Math.Round(member.SelectPointY * sy))))
        Next
        _membersReferenceWidth = _partyRect.W
        _membersReferenceHeight = _partyRect.H
    End Sub

    Private Sub NormalizeMemberRectangleSizes()
        If _members.Count = 0 Then Return
        Dim sourceIndex As Integer = Math.Max(0, Math.Min(_selectedMember, _members.Count - 1))
        Dim sourceRect As RectRegion = If(_members(sourceIndex).HpBarRect, New RectRegion(5, 18, Math.Max(2, _partyRect.W - 10), 7))
        Dim sharedWidth As Integer = Math.Max(2, Math.Min(_partyRect.W, sourceRect.W))
        Dim sharedHeight As Integer = Math.Max(2, Math.Min(_partyRect.H, sourceRect.H))
        For Each member As FullSupportPartyMember In _members
            Dim current As RectRegion = If(member.HpBarRect, New RectRegion(0, 0, sharedWidth, sharedHeight))
            Dim x As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.W - sharedWidth), current.X))
            Dim y As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.H - sharedHeight), current.Y))
            member.HpBarRect = New RectRegion(x, y, sharedWidth, sharedHeight)
        Next
    End Sub

    Private Sub MemberCountChanged(sender As Object, e As EventArgs)
        If _syncing Then Return
        CommitMemberEditor()
        EnsureMemberCount(CInt(_memberCount.Value), False)
        NormalizeMemberRectangleSizes()
        RefreshMembersList()
        _memberEditorLoaded = False
        SelectMember(Math.Min(_selectedMember, _members.Count - 1))
    End Sub

    Private Sub RefreshMembersList()
        _syncing = True
        Try
            Dim selected As Integer = _selectedMember
            _membersList.Items.Clear()
            For index As Integer = 0 To _members.Count - 1
                _membersList.Items.Add($"{index + 1}. {_members(index).Name}{If(_members(index).Enabled, "", " (off)")}")
            Next
            If _members.Count > 0 Then _membersList.SelectedIndex = Math.Max(0, Math.Min(selected, _members.Count - 1))
        Finally
            _syncing = False
        End Try
        _memberEditorLoaded = True
    End Sub

    Private Sub SelectMember(index As Integer)
        If index < 0 OrElse index >= _members.Count Then Return
        CommitMemberEditor()
        _selectedMember = index
        _syncing = True
        Try
            _membersList.SelectedIndex = index
        Finally
            _syncing = False
        End Try
        LoadMemberEditor()
        _preview.Invalidate()
    End Sub

    Private Sub LoadMemberEditor()
        If _selectedMember < 0 OrElse _selectedMember >= _members.Count Then Return
        Dim member As FullSupportPartyMember = _members(_selectedMember)
        _syncing = True
        Try
            _memberName.Text = member.Name
            _memberEnabled.Checked = member.Enabled
            SetNumeric(_xValue, member.HpBarRect.X)
            SetNumeric(_yValue, member.HpBarRect.Y)
            SetNumeric(_wValue, member.HpBarRect.W)
            SetNumeric(_hValue, member.HpBarRect.H)
            SetNumeric(_selectXValue, member.SelectPointX)
            SetNumeric(_selectYValue, member.SelectPointY)
        Finally
            _syncing = False
        End Try
    End Sub

    Private Sub MemberEditorChanged(sender As Object, e As EventArgs)
        If _syncing Then Return
        Dim sharedSizeChanged As Boolean = ReferenceEquals(sender, _wValue) OrElse ReferenceEquals(sender, _hValue)
        CommitMemberEditor(sharedSizeChanged)
        RefreshMembersList()
        _preview.Invalidate()
    End Sub

    Private Sub CommitMemberEditor(Optional applySizeToAllRows As Boolean = False)
        If _syncing OrElse Not _memberEditorLoaded OrElse _selectedMember < 0 OrElse _selectedMember >= _members.Count Then Return
        Dim member As FullSupportPartyMember = _members(_selectedMember)
        member.Name = If(String.IsNullOrWhiteSpace(_memberName.Text), $"Member {_selectedMember + 1}", _memberName.Text.Trim())
        member.Enabled = _memberEnabled.Checked
        Dim width As Integer = Math.Max(2, Math.Min(_partyRect.W, CInt(_wValue.Value)))
        Dim height As Integer = Math.Max(2, Math.Min(_partyRect.H, CInt(_hValue.Value)))
        Dim x As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.W - width), CInt(_xValue.Value)))
        Dim y As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.H - height), CInt(_yValue.Value)))
        member.HpBarRect = New RectRegion(x, y, width, height)
        If applySizeToAllRows Then
            For index As Integer = 0 To _members.Count - 1
                If index = _selectedMember Then Continue For
                Dim other As FullSupportPartyMember = _members(index)
                Dim otherRect As RectRegion = If(other.HpBarRect, New RectRegion(0, 0, width, height))
                Dim otherX As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.W - width), otherRect.X))
                Dim otherY As Integer = Math.Max(0, Math.Min(Math.Max(0, _partyRect.H - height), otherRect.Y))
                other.HpBarRect = New RectRegion(otherX, otherY, width, height)
            Next
        End If
        member.SelectPointX = Math.Max(0, Math.Min(Math.Max(0, _partyRect.W - 1), CInt(_selectXValue.Value)))
        member.SelectPointY = Math.Max(0, Math.Min(Math.Max(0, _partyRect.H - 1), CInt(_selectYValue.Value)))
    End Sub

    Private Sub Nudge(axis As Char, amount As Integer)
        If _selectedMember < 0 OrElse _selectedMember >= _members.Count Then Return
        Select Case Char.ToUpperInvariant(axis)
            Case "X"c : SetNumeric(_xValue, CInt(_xValue.Value) + amount)
            Case "Y"c : SetNumeric(_yValue, CInt(_yValue.Value) + amount)
            Case "W"c : SetNumeric(_wValue, CInt(_wValue.Value) + amount)
            Case "H"c : SetNumeric(_hValue, CInt(_hValue.Value) + amount)
        End Select
        CommitMemberEditor()
        _preview.Invalidate()
    End Sub

    Private Sub NudgeAllBarsHorizontal(amount As Integer)
        If _members.Count = 0 OrElse amount = 0 Then Return
        Dim actualAmount As Integer = amount
        For Each member As FullSupportPartyMember In _members
            Dim rect As RectRegion = If(member.HpBarRect, New RectRegion(0, 0, 2, 2))
            If amount < 0 Then
                actualAmount = Math.Max(actualAmount, -Math.Max(0, rect.X))
            Else
                actualAmount = Math.Min(actualAmount, Math.Max(0, _partyRect.W - rect.W - rect.X))
            End If
        Next
        If actualAmount = 0 Then Return

        For Each member As FullSupportPartyMember In _members
            Dim rect As RectRegion = member.HpBarRect
            member.HpBarRect = New RectRegion(rect.X + actualAmount, rect.Y, rect.W, rect.H)
        Next
        LoadMemberEditor()
        _preview.Invalidate()
    End Sub

    Private Sub NudgeAllBarSpacing(amount As Integer)
        If _members.Count < 2 OrElse amount = 0 Then Return
        Dim minimumTop As Integer = Integer.MaxValue
        Dim maximumBottom As Integer = Integer.MinValue
        For index As Integer = 0 To _members.Count - 1
            Dim rect As RectRegion = If(_members(index).HpBarRect, New RectRegion(0, 0, 2, 2))
            Dim offset As Integer = index * amount
            minimumTop = Math.Min(minimumTop, Math.Min(rect.Y + offset, _members(index).SelectPointY + offset))
            maximumBottom = Math.Max(maximumBottom, Math.Max(rect.Y + offset + rect.H, _members(index).SelectPointY + offset + 1))
        Next

        If maximumBottom - minimumTop > _partyRect.H Then Return
        Dim translation As Integer = If(minimumTop < 0, -minimumTop, 0)
        If maximumBottom + translation > _partyRect.H Then
            translation -= (maximumBottom + translation - _partyRect.H)
        End If

        For index As Integer = 0 To _members.Count - 1
            Dim member As FullSupportPartyMember = _members(index)
            Dim rect As RectRegion = member.HpBarRect
            Dim offset As Integer = index * amount + translation
            member.HpBarRect = New RectRegion(rect.X, rect.Y + offset, rect.W, rect.H)
            member.SelectPointY += offset
        Next
        LoadMemberEditor()
        _preview.Invalidate()
    End Sub

    Private Shared Sub SetNumeric(control As NumericUpDown, value As Integer)
        control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, CDec(value)))
    End Sub

    Private Function TryControlToImagePoint(controlPoint As DrawingPoint, ByRef imagePoint As DrawingPoint) As Boolean
        If _preview.Image Is Nothing Then Return False
        Dim display As RectangleF = GetImageDisplayRectangle()
        If Not display.Contains(controlPoint) Then Return False
        Dim x As Integer = CInt(Math.Floor((controlPoint.X - display.Left) * _preview.Image.Width / display.Width))
        Dim y As Integer = CInt(Math.Floor((controlPoint.Y - display.Top) * _preview.Image.Height / display.Height))
        imagePoint = New DrawingPoint(Math.Max(0, Math.Min(_preview.Image.Width - 1, x)), Math.Max(0, Math.Min(_preview.Image.Height - 1, y)))
        Return True
    End Function

    Private Function GetImageDisplayRectangle() As RectangleF
        If _preview.Image Is Nothing OrElse _preview.ClientSize.Width <= 0 OrElse _preview.ClientSize.Height <= 0 Then Return RectangleF.Empty
        Dim scale As Single = Math.Min(_preview.ClientSize.Width / CSng(_preview.Image.Width), _preview.ClientSize.Height / CSng(_preview.Image.Height))
        Dim width As Single = _preview.Image.Width * scale
        Dim height As Single = _preview.Image.Height * scale
        Return New RectangleF((_preview.ClientSize.Width - width) / 2.0F, (_preview.ClientSize.Height - height) / 2.0F, width, height)
    End Function

    Private Shared Function ImageToControlPoint(point As DrawingPoint, display As RectangleF, imageSize As Size) As PointF
        Return New PointF(display.Left + point.X * display.Width / Math.Max(1, imageSize.Width), display.Top + point.Y * display.Height / Math.Max(1, imageSize.Height))
    End Function

    Private Shared Function ToRectangle(region As RectRegion) As Rectangle
        Return New Rectangle(region.X, region.Y, Math.Max(1, region.W), Math.Max(1, region.H))
    End Function

    Private Shared Function CloneAndClampRect(region As RectRegion, maxWidth As Integer, maxHeight As Integer) As RectRegion
        Dim rect As Rectangle = region.Clamp(maxWidth, maxHeight)
        Return New RectRegion(rect.X, rect.Y, rect.Width, rect.Height)
    End Function

    Private Shared Function CloneMember(source As FullSupportPartyMember) As FullSupportPartyMember
        Dim hp As RectRegion = If(source.HpBarRect, New RectRegion(5, 18, 150, 7))
        Return New FullSupportPartyMember With {
            .Name = source.Name,
            .Enabled = source.Enabled,
            .HpBarRect = New RectRegion(hp.X, hp.Y, hp.W, hp.H),
            .SelectPointX = source.SelectPointX,
            .SelectPointY = source.SelectPointY
        }
    End Function
End Class
