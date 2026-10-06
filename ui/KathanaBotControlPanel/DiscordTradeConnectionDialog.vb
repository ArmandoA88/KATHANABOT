Imports DrawingPoint = System.Drawing.Point

Friend Class DiscordTradeConnectionDialog
    Inherits Form

    Private ReadOnly _channelBox As TextBox
    Private ReadOnly _tokenBox As TextBox
    Private ReadOnly _countBox As NumericUpDown

    Public ReadOnly Property ChannelUrl As String
        Get
            Return _channelBox.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property BotToken As String
        Get
            Return _tokenBox.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property ImportMessageCount As Integer
        Get
            Return CInt(_countBox.Value)
        End Get
    End Property

    Public Sub New(channelUrl As String, existingToken As String)
        Me.New(channelUrl, existingToken, 10000)
    End Sub

    Public Sub New(channelUrl As String, existingToken As String, importMessageCount As Integer)
        Text = "Trade Discord connection"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(660, 468)
        BackColor = Color.FromArgb(15, 21, 38)
        ForeColor = Color.FromArgb(222, 233, 250)
        Font = New Font("Segoe UI", 9.0F)
        Dim title As New Label With {.Text = "READ YOUR RECEIVING DISCORD CHANNEL", .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .ForeColor = Color.FromArgb(95, 205, 255), .Location = New DrawingPoint(20, 18)}
        Dim help As New Label With {.Text = "Follow delivers only announcements published by the source server. Install a reader bot in your own receiving server with View Channel and Read Message History. Enable Message Content access in the bot's Developer Portal (approval if required)." & vbCrLf & vbCrLf & "Enter that bot's token locally below. It is saved encrypted for this Windows account. Import reads posts only; Analyze and Start remain separate actions. Review forwarded names against exact game character names.", .Location = New DrawingPoint(22, 54), .Size = New Size(616, 112), .ForeColor = Color.FromArgb(150, 170, 200)}
        Dim channelLabel As New Label With {.Text = "Receiving channel link", .AutoSize = True, .Location = New DrawingPoint(22, 176)}
        _channelBox = New TextBox With {.Location = New DrawingPoint(22, 198), .Size = New Size(616, 25), .Text = If(channelUrl, ""), .MaxLength = 300}
        Dim tokenLabel As New Label With {.Text = "Reader BOT token", .AutoSize = True, .Location = New DrawingPoint(22, 234)}
        _tokenBox = New TextBox With {.Location = New DrawingPoint(22, 256), .Size = New Size(616, 25), .Text = If(existingToken, ""), .UseSystemPasswordChar = True, .MaxLength = 500}
        Dim tokenHelp As New Label With {.Text = "Use a bot token, not your personal-account token. Leave blank to remove the saved reader token.", .Location = New DrawingPoint(22, 287), .Size = New Size(616, 36), .ForeColor = Color.FromArgb(150, 170, 200)}
        Dim countLabel As New Label With {.Text = "Posts to import", .AutoSize = True, .Location = New DrawingPoint(22, 333)}
        _countBox = New NumericUpDown With {.Minimum = 1, .Maximum = 10000, .Value = Math.Clamp(importMessageCount, 1, 10000), .ThousandsSeparator = True, .Location = New DrawingPoint(164, 329), .Size = New Size(130, 25)}
        Dim countHelp As New Label With {.Text = "Import up to 10,000 recent posts. Auto checks for updates after the first full import. AI analysis supports 200,000 characters per action.", .Location = New DrawingPoint(22, 365), .Size = New Size(616, 34), .ForeColor = Color.FromArgb(150, 170, 200)}
        Dim save As New Button With {.Text = "Save connection", .Location = New DrawingPoint(390, 412), .Size = New Size(140, 34), .DialogResult = DialogResult.OK}
        Dim cancel As New Button With {.Text = "Cancel", .Location = New DrawingPoint(538, 412), .Size = New Size(100, 34), .DialogResult = DialogResult.Cancel}
        AddHandler save.Click,
            Sub()
                Try
                    Dim target = DiscordTradeService.ParseChannel(ChannelUrl)
                    _channelBox.Text = If(target.ChannelLink.Length > 0, target.ChannelLink, target.ChannelId)
                    If BotToken.Any(Function(c) Char.IsWhiteSpace(c) OrElse Char.IsControl(c)) Then Throw New ArgumentException()
                Catch
                    DialogResult = DialogResult.None
                    SilentMessageBox.Show(Me, "Enter a valid receiving Discord channel link and a bot token without spaces, or leave the token blank to remove it.", "Trade Discord", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End Try
            End Sub
        Controls.AddRange({title, help, channelLabel, _channelBox, tokenLabel, _tokenBox, tokenHelp, countLabel, _countBox, countHelp, save, cancel})
        AcceptButton = save
        CancelButton = cancel
    End Sub
End Class
