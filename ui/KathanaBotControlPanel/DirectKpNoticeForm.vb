Public NotInheritable Class DirectKpNoticeForm
    Inherits Form
    Private ReadOnly _timer As New Timer With {.Interval = 100}
    Private ReadOnly _watch As New Diagnostics.Stopwatch
    Private ReadOnly _countdown As New Label With {.Text = "Enabling in 5 seconds…", .AutoSize = True, .Dock = DockStyle.Fill}

    Public Sub New(warning As String)
        Text = "LITE Direct KP — read before enabling"
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(620, 310)
        BackColor = Color.FromArgb(14, 20, 36)
        ForeColor = Color.Gainsboro
        Font = New Font("Segoe UI", 10)
        AutoScaleMode = AutoScaleMode.Dpi
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18), .ColumnCount = 1, .RowCount = 4, .BackColor = BackColor, .ForeColor = ForeColor}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.Controls.Add(New Label With {.Text = "LITE Direct KP", .Dock = DockStyle.Fill, .ForeColor = Color.Turquoise, .Font = New Font("Segoe UI", 13, FontStyle.Bold)}, 0, 0)
        layout.Controls.Add(New Label With {.Text = warning, .Dock = DockStyle.Fill}, 0, 1)
        _countdown.ForeColor = Color.Gold
        layout.Controls.Add(_countdown, 0, 2)
        Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .AutoSize = True, .MinimumSize = New Size(110, 36), .FlatStyle = FlatStyle.Flat, .BackColor = Color.FromArgb(30, 45, 65), .ForeColor = Color.White}
        layout.Controls.Add(cancel, 0, 3)
        CancelButton = cancel
        Controls.Add(layout)
        AddHandler Shown, Sub()
                              _watch.Start()
                              _timer.Start()
                          End Sub
        AddHandler _timer.Tick, Sub()
                                   Dim seconds = Math.Max(0, CInt(Math.Ceiling((5000 - _watch.ElapsedMilliseconds) / 1000.0)))
                                   _countdown.Text = $"Enabling in {seconds} seconds…"
                                   If _watch.ElapsedMilliseconds >= 5000 Then
                                       _timer.Stop()
                                       DialogResult = DialogResult.OK
                                       Close()
                                   End If
                               End Sub
    End Sub
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then _timer.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
