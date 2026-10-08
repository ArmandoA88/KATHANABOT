Imports DrawingPoint = System.Drawing.Point

Friend Class DiscordTradeCaptureDialog
    Inherits Form

    Private ReadOnly _sourceBox As TextBox
    Private ReadOnly _countBox As NumericUpDown
    Private ReadOnly _filterBox As TextBox
    Private ReadOnly _filterChanged As Action(Of String)
    Private ReadOnly _connectionBox As TextBox
    Private ReadOnly _startButton As Button
    Private ReadOnly _copyButton As Button
    Private ReadOnly _stopButton As Button
    Private ReadOnly _installButton As Button
    Private _installExtension As Action
    Private ReadOnly _status As Label
    Private ReadOnly _startConnection As Func(Of String, Integer, String)
    Private ReadOnly _stopConnection As Action
    Private _updating As Boolean

    Public Sub New(sourceUrl As String, messageCount As Integer, startConnection As Func(Of String, Integer, String), stopConnection As Action)
        Me.New(sourceUrl, messageCount, startConnection, stopConnection, "", Nothing)
    End Sub

    Public Sub New(sourceUrl As String, messageCount As Integer, startConnection As Func(Of String, Integer, String), stopConnection As Action, filterText As String, filterChanged As Action(Of String))
        _startConnection = startConnection
        _stopConnection = stopConnection
        Text = "Trade browser capture"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(700, 604)
        BackColor = Color.FromArgb(15, 21, 38)
        ForeColor = Color.FromArgb(222, 233, 250)
        Font = New Font("Segoe UI", 9.0F)
        Dim title As New Label With {.Text = "CAPTURE DISCORD POSTS LOADED BY SCROLLING", .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .ForeColor = Color.FromArgb(95, 205, 255), .Location = New DrawingPoint(20, 18)}
        Dim help As New Label With {.Text = "One-time setup: install the supplied extension and open the source channel in your signed-in Discord web tab. Paste the local setup below into the extension. It scrolls the channel and captures message bodies Discord loads. No Discord credentials are entered." & vbCrLf & vbCrLf & "Discord prohibits personal-account automation and may terminate your account. History that does not load produces a partial capture. Analyze the imported posts and review whispers separately.", .Location = New DrawingPoint(22, 54), .Size = New Size(656, 122), .ForeColor = Color.FromArgb(150, 170, 200)}
        Dim sourceLabel As New Label With {.Text = "Source channel link", .AutoSize = True, .Location = New DrawingPoint(22, 182)}
        _sourceBox = New TextBox With {.Text = If(sourceUrl, ""), .MaxLength = 300, .Location = New DrawingPoint(22, 204), .Size = New Size(656, 25)}
        Dim countLabel As New Label With {.Text = "Posts to capture", .AutoSize = True, .Location = New DrawingPoint(22, 244)}
        _countBox = New NumericUpDown With {.Minimum = 1, .Maximum = 10000, .Value = Math.Clamp(messageCount, 1, 10000), .ThousandsSeparator = True, .Location = New DrawingPoint(165, 240), .Size = New Size(130, 25)}
        Dim filterLabel As New Label With {.Text = "Search Discord for (optional, items separated by commas; leave empty to capture every post)", .AutoSize = True, .Location = New DrawingPoint(22, 280)}
        _filterChanged = filterChanged
        _filterBox = New TextBox With {.Text = If(filterText, ""), .MaxLength = 500, .Location = New DrawingPoint(22, 302), .Size = New Size(656, 25), .PlaceholderText = "e.g. ror asura, d.potra  (typed into Discord's own search box by the extension)"}
        Dim connectionLabel As New Label With {.Text = "Local connection setup (paste into the extension)", .AutoSize = True, .Location = New DrawingPoint(22, 340)}
        _connectionBox = New TextBox With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical, .Location = New DrawingPoint(22, 364), .Size = New Size(656, 118)}
        _status = New Label With {.Text = "Click Start capture to create a local connection. No bot token is needed.", .Location = New DrawingPoint(22, 490), .Size = New Size(656, 56), .ForeColor = Color.FromArgb(150, 170, 200)}
        _startButton = New Button With {.Text = "Start capture", .Location = New DrawingPoint(22, 556), .Size = New Size(128, 32)}
        _copyButton = New Button With {.Text = "Copy setup", .Enabled = False, .Location = New DrawingPoint(158, 556), .Size = New Size(116, 32)}
        _stopButton = New Button With {.Text = "Stop capture", .Enabled = False, .Location = New DrawingPoint(282, 556), .Size = New Size(120, 32)}
        Dim close As New Button With {.Text = "Close", .Location = New DrawingPoint(578, 556), .Size = New Size(100, 32), .DialogResult = DialogResult.Cancel}
        _installButton = New Button With {.Text = "Install extension...", .Location = New DrawingPoint(410, 556), .Size = New Size(160, 32), .Enabled = False}
        AddHandler _installButton.Click, Sub() InstallExtension?.Invoke()
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
        ' The search items travel inside the copied setup, so changing them stops an active connection like a source/count change.
        AddHandler _filterBox.TextChanged,
            Sub()
                _filterChanged?.Invoke(_filterBox.Text)
                ConnectionOptionsChanged()
            End Sub
        Controls.AddRange({title, help, sourceLabel, _sourceBox, countLabel, _countBox, filterLabel, _filterBox, connectionLabel, _connectionBox, _status, _startButton, _copyButton, _stopButton, _installButton, close})
        CancelButton = close
    End Sub

    ' Unpacks the embedded browser extension for installation; the button stays disabled until the host supplies this.
    <System.ComponentModel.Browsable(False), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property InstallExtension As Action
        Get
            Return _installExtension
        End Get
        Set(value As Action)
            _installExtension = value
            _installButton.Enabled = value IsNot Nothing
        End Set
    End Property

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
        _status.Text = "Capture stopped because source, count or search items changed. Click Start capture and copy the new setup into the extension."
    End Sub
End Class
