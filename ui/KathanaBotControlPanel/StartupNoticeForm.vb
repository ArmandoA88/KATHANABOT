Friend NotInheritable Class StartupNoticeForm
    Inherits Form

    Private Const AutoCloseSeconds As Integer = 15

    Private ReadOnly _closeTimer As New Timer() With {.Interval = 1000}
    Private ReadOnly _okButton As Button
    Private ReadOnly _noticeIcon As Bitmap
    Private _secondsRemaining As Integer = AutoCloseSeconds

    Public Sub New(message As String)
        Text = "Hey, I Need Your Feedback!"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        MaximizeBox = False
        MinimizeBox = False
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New Size(760, 600)
        BackColor = Color.FromArgb(12, 18, 30)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 3,
            .Padding = New Padding(0),
            .BackColor = Color.FromArgb(12, 18, 30)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 62.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 62.0F))

        Dim headerLabel As New Label() With {
            .Text = "HEY, I NEED YOUR FEEDBACK!",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(255, 184, 44),
            .ForeColor = Color.FromArgb(25, 25, 25),
            .Font = New Font("Segoe UI", 17.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(22, 0, 12, 0),
            .Margin = New Padding(0)
        }
        root.Controls.Add(headerLabel, 0, 0)
        root.SetColumnSpan(headerLabel, 2)

        _noticeIcon = SystemIcons.Warning.ToBitmap()
        Dim iconBox As New PictureBox() With {
            .Image = _noticeIcon,
            .SizeMode = PictureBoxSizeMode.CenterImage,
            .Dock = DockStyle.Top,
            .Height = 48,
            .Margin = New Padding(10, 20, 4, 0)
        }
        root.Controls.Add(iconBox, 0, 1)

        Dim messageBox As New RichTextBox() With {
            .Text = If(message, ""),
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .BorderStyle = BorderStyle.None,
            .BackColor = Color.FromArgb(12, 18, 30),
            .ForeColor = Color.FromArgb(232, 238, 247),
            .Font = New Font("Segoe UI", 10.25F, FontStyle.Regular),
            .ScrollBars = RichTextBoxScrollBars.Vertical,
            .DetectUrls = False,
            .TabStop = False,
            .Margin = New Padding(0, 18, 18, 8)
        }
        ApplyAttentionFormatting(messageBox)
        root.Controls.Add(messageBox, 1, 1)

        Dim contactLabel As New Label() With {
            .Text = "IN-GAME PM: xSAITAMAx",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(80, 210, 255),
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(18, 0, 0, 0),
            .Margin = New Padding(0)
        }

        Dim footerRow As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(19, 29, 46),
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        footerRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        footerRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 204.0F))
        footerRow.Controls.Add(contactLabel, 0, 0)

        _okButton = New Button() With {
            .Text = $"I UNDERSTAND ({AutoCloseSeconds})",
            .Anchor = AnchorStyles.Right Or AnchorStyles.Bottom,
            .AutoSize = False,
            .Size = New Size(172, 36),
            .DialogResult = DialogResult.OK,
            .BackColor = Color.FromArgb(255, 184, 44),
            .ForeColor = Color.FromArgb(25, 25, 25),
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Margin = New Padding(0, 0, 18, 13),
            .UseVisualStyleBackColor = False
        }
        _okButton.FlatAppearance.BorderColor = Color.FromArgb(255, 216, 120)
        footerRow.Controls.Add(_okButton, 1, 0)
        root.Controls.Add(footerRow, 0, 2)
        root.SetColumnSpan(footerRow, 2)

        Controls.Add(root)
        AcceptButton = _okButton
        CancelButton = _okButton

        AddHandler _closeTimer.Tick, AddressOf CloseTimerTick
        AddHandler Shown, AddressOf NoticeShown
    End Sub

    Private Shared Sub ApplyAttentionFormatting(target As RichTextBox)
        HighlightAll(target, "ENGLISH", Color.FromArgb(255, 202, 74), FontStyle.Bold)
        HighlightAll(target, "ESPAÑOL", Color.FromArgb(255, 122, 92), FontStyle.Bold)
        HighlightAll(target, "TAGALOG", Color.FromArgb(100, 220, 160), FontStyle.Bold)
        HighlightAll(target, "xSAITAMAx", Color.FromArgb(80, 210, 255), FontStyle.Bold)
        target.Select(0, 0)
    End Sub

    Private Shared Sub HighlightAll(target As RichTextBox, phrase As String, color As Color, style As FontStyle)
        Dim searchFrom As Integer = 0
        While searchFrom < target.TextLength
            Dim matchAt As Integer = target.Text.IndexOf(phrase, searchFrom, StringComparison.Ordinal)
            If matchAt < 0 Then
                Exit While
            End If
            target.Select(matchAt, phrase.Length)
            target.SelectionColor = color
            target.SelectionFont = New Font(target.Font, style)
            searchFrom = matchAt + phrase.Length
        End While
    End Sub

    Private Sub NoticeShown(sender As Object, e As EventArgs)
        _secondsRemaining = AutoCloseSeconds
        _okButton.Text = $"I UNDERSTAND ({_secondsRemaining})"
        _closeTimer.Start()
    End Sub

    Private Sub CloseTimerTick(sender As Object, e As EventArgs)
        _secondsRemaining -= 1
        If _secondsRemaining <= 0 Then
            _closeTimer.Stop()
            DialogResult = DialogResult.OK
            Close()
            Return
        End If

        _okButton.Text = $"I UNDERSTAND ({_secondsRemaining})"
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _closeTimer.Stop()
            RemoveHandler _closeTimer.Tick, AddressOf CloseTimerTick
            RemoveHandler Shown, AddressOf NoticeShown
            _closeTimer.Dispose()
            _noticeIcon.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
