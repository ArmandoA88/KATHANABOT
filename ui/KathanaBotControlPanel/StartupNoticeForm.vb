Friend NotInheritable Class StartupNoticeForm
    Inherits Form

    Private Const AutoCloseSeconds As Integer = 5

    Private ReadOnly _closeTimer As New Timer() With {.Interval = 1000}
    Private ReadOnly _okButton As Button
    Private ReadOnly _noticeIcon As Bitmap
    Private _secondsRemaining As Integer = AutoCloseSeconds

    Public Sub New(message As String)
        Text = "Notice"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        MaximizeBox = False
        MinimizeBox = False
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New Size(560, 270)
        BackColor = SystemColors.Window

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .Padding = New Padding(18, 18, 14, 12),
            .BackColor = SystemColors.Window
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 42.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))

        _noticeIcon = SystemIcons.Information.ToBitmap()
        Dim iconBox As New PictureBox() With {
            .Image = _noticeIcon,
            .SizeMode = PictureBoxSizeMode.CenterImage,
            .Dock = DockStyle.Top,
            .Height = 36,
            .Margin = New Padding(0, 4, 8, 0)
        }
        root.Controls.Add(iconBox, 0, 0)

        Dim messageLabel As New Label() With {
            .Text = If(message, ""),
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular),
            .ForeColor = SystemColors.ControlText,
            .TextAlign = ContentAlignment.TopLeft,
            .Margin = New Padding(0, 0, 0, 4)
        }
        root.Controls.Add(messageLabel, 1, 0)

        _okButton = New Button() With {
            .Text = $"OK ({AutoCloseSeconds})",
            .Anchor = AnchorStyles.Right Or AnchorStyles.Bottom,
            .AutoSize = False,
            .Size = New Size(92, 28),
            .DialogResult = DialogResult.OK,
            .Margin = New Padding(0, 6, 0, 0)
        }
        root.Controls.Add(_okButton, 1, 1)

        Controls.Add(root)
        AcceptButton = _okButton
        CancelButton = _okButton

        AddHandler _closeTimer.Tick, AddressOf CloseTimerTick
        AddHandler Shown, AddressOf NoticeShown
    End Sub

    Private Sub NoticeShown(sender As Object, e As EventArgs)
        _secondsRemaining = AutoCloseSeconds
        _okButton.Text = $"OK ({_secondsRemaining})"
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

        _okButton.Text = $"OK ({_secondsRemaining})"
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
