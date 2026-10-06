Imports DrawingPoint = System.Drawing.Point

Friend Class DiscordTradeCaptureDialog
    Inherits Form

    Private ReadOnly _sourceBox As TextBox
    Private ReadOnly _countBox As NumericUpDown
    Private ReadOnly _connectionBox As TextBox
    Private ReadOnly _startButton As Button
    Private ReadOnly _copyButton As Button
    Private ReadOnly _stopButton As Button
    Private ReadOnly _status As Label
    Private ReadOnly _startConnection As Func(Of String, Integer, String)
    Private ReadOnly _stopConnection As Action
    Private _updating As Boolean

    Public Sub New(sourceUrl As String, messageCount As Integer, startConnection As Func(Of String, Integer, String), stopConnection As Action)
        _startConnection = startConnection
        _stopConnection = stopConnection
        Text = "Trade browser capture"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(700, 570)
        BackColor = Color.FromArgb(15, 21, 38)
        ForeColor = Color.FromArgb(222, 233, 250)
        Font = New Font("Segoe UI", 9.0F)
        Dim title As New Label With {.Text = "CAPTURE DISCORD POSTS LOADED BY SCROLLING", .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .ForeColor = Color.FromArgb(95, 205, 255), .Location = New DrawingPoint(20, 18)}
        Dim help As New Label With {.Text = "One-time setup: install the supplied extension and open the source channel in your signed-in Discord web tab. Paste the local setup below into the extension. It scrolls the channel and captures message bodies Discord loads. No Discord credentials are entered." & vbCrLf & vbCrLf & "Discord prohibits personal-account automation and may terminate your account. History that does not load produces a partial capture. Analyze the imported posts and review whispers separately.", .Location = New DrawingPoint(22, 54), .Size = New Size(656, 122), .ForeColor = Color.FromArgb(150, 170, 200)}
        Dim sourceLabel As New Label With {.Text = "Source channel link", .AutoSize = True, .Location = New DrawingPoint(22, 182)}
        _sourceBox = New TextBox With {.Text = If(sourceUrl, ""), .MaxLength = 300, .Location = New DrawingPoint(22, 204), .Size = New Size(656, 25)}
        Dim countLabel As New Label With {.Text = "Posts to capture", .AutoSize = True, .Location = New DrawingPoint(22, 244)}
        _countBox = New NumericUpDown With {.Minimum = 1, .Maximum = 10000, .Value = Math.Clamp(messageCount, 1, 10000), .ThousandsSeparator = True, .Location = New DrawingPoint(165, 240), .Size = New Size(130, 25)}
        Dim connectionLabel As New Label With {.Text = "Local connection setup (paste into the extension)", .AutoSize = True, .Location = New DrawingPoint(22, 279)}
        _connectionBox = New TextBox With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical, .Location = New DrawingPoint(22, 303), .Size = New Size(656, 138)}
        _status = New Label With {.Text = "Click Start capture to create a local connection. No bot token is needed.", .Location = New DrawingPoint(22, 450), .Size = New Size(656, 60), .ForeColor = Color.FromArgb(150, 170, 200)}
        _startButton = New Button With {.Text = "Start capture", .Location = New DrawingPoint(22, 524), .Size = New Size(128, 32)}
        _copyButton = New Button With {.Text = "Copy setup", .Enabled = False, .Location = New DrawingPoint(158, 524), .Size = New Size(116, 32)}
        _stopButton = New Button With {.Text = "Stop capture", .Enabled = False, .Location = New DrawingPoint(282, 524), .Size = New Size(120, 32)}
        Dim close As New Button With {.Text = "Close", .Location = New DrawingPoint(578, 524), .Size = New Size(100, 32), .DialogResult = DialogResult.Cancel}
        AddHandler _startButton.Click,
            Sub()
                Try
                    UpdateConnection(_startConnection(_sourceBox.Text.Trim(), CInt(_countBox.Value)))
                Catch
                    _status.Text = "Capture could not start. Enter a full source channel link and finish or stop any active Trade action. Existing posts are kept."
                End Try
            End Sub
        AddHandler _copyButton.Click,
            Sub()
                Try
                    Clipboard.SetText(_connectionBox.Text)
                    _status.Text = "Setup copied. Paste it into the extension on the source channel, then start the collector. Stop / F12 disconnects; closing this dialog keeps capture running."
                Catch
                    _status.Text = "Clipboard is unavailable. Select and copy the connection setup text above."
                End Try
            End Sub
        AddHandler _stopButton.Click, Sub() _stopConnection()
        AddHandler _sourceBox.TextChanged, Sub() ConnectionOptionsChanged()
        AddHandler _countBox.ValueChanged, Sub() ConnectionOptionsChanged()
        Controls.AddRange({title, help, sourceLabel, _sourceBox, countLabel, _countBox, connectionLabel, _connectionBox, _status, _startButton, _copyButton, _stopButton, close})
        CancelButton = close
    End Sub

    Public Sub UpdateConnection(connectionJson As String)
        _updating = True
        Try
            _connectionBox.Text = If(connectionJson, "")
            Dim active = _connectionBox.TextLength > 0
            _startButton.Enabled = Not active
            _copyButton.Enabled = active
            _stopButton.Enabled = active
            _status.Text = If(active,
                "Copy setup into the extension. Capture pauses during Analyze, sending, or a checked review queue. Closing this dialog keeps capture running. Stop / F12 disconnects.",
                "Capture is disconnected. Click Start capture for a new local connection; existing posts and whispers are kept.")
        Finally
            _updating = False
        End Try
    End Sub

    Public Sub ShowCompletedCapture()
        UpdateConnection("")
        _stopButton.Enabled = True
        _status.Text = "Capture imported. Analyze the posts and review exact game names before Start. Click Start capture and copy a new setup to collect again; Stop disconnects the completed session."
    End Sub

    Private Sub ConnectionOptionsChanged()
        If _updating OrElse Not _stopButton.Enabled Then Return
        _stopConnection()
        _status.Text = "Capture stopped because source or count changed. Click Start capture and copy the new setup into the extension."
    End Sub
End Class
