Imports System.Threading.Tasks
Imports System.Text.Json
Imports DrawingPoint = System.Drawing.Point

Partial Public Class Form1
    Private _resuApplyingSettings As Boolean
    Private _resuShowOverlay As CheckBox
    Private _resuBlacklistDraft As TextBox
    Private _resuTab As TabPage
    Private _resuSettings As New ResuSettings()
    Private _resuService As ResuService
    Private ReadOnly _resuTimer As New System.Windows.Forms.Timer With {.Interval = 100}
    Private _resuBusy As Boolean
    Private _resuGeneration As Integer
    Private _resuNextScan As DateTime
    Private _resuNextSelectKeyAt As DateTime
    Private _resuNextPeriodicMessageAt As DateTime
    Private _resuNextKeepStandingAt As DateTime
    Private _resuKeywordTriggerUntilUtc As DateTime
    Private _resuTradeVisible As Boolean
    Private _resuWindow As IntPtr
    Private _resuOverlay As AutoRelaunchClickOverlayForm
    Private _resuRunning As Boolean
    Private _resuOptions As TableLayoutPanel
    Private _resuStart As Button
    Private _resuStatus As Label
    Private ReadOnly _resuStatusHistory As New List(Of String)()
    Private ReadOnly _resuStatusTimes As New List(Of DateTime)()
    Private _resuCalibrationLabel As Label
    Private _resuOcr As TextBox
    Private _resuSelectKey As ComboBox
    Private _resuSelectKeyIntervalMs As NumericUpDown
    Private _resuCastKey As TextBox
    Private _resuBuffEnabled(2) As CheckBox
    Private _resuBuffKey(2) As TextBox
    Private _resuPeriodicMessageEnabled As CheckBox
    Private _resuPeriodicMessageText As TextBox
    Private _resuPeriodicMessageIntervalSeconds As NumericUpDown
    Private _resuCastPressCount As NumericUpDown
    Private _resuCastBurstSeconds As NumericUpDown
    Private _resuScanMs As NumericUpDown
    Private _resuTimeout As NumericUpDown
    Private _resuMinimumPayment As NumericUpDown
    Private _resuBlacklistEnabled As CheckBox
    Private _resuBlacklist As DataGridView
    Private ReadOnly _resuPatterns As New Dictionary(Of String, TextBox)()
    Private _resuChatAlarmEnabled As CheckBox
    Private _resuChatAlarmKeywords As TextBox
    Private _resuChatAutoReplyEnabled As CheckBox
    Private _resuChatAutoReplyText As TextBox
    Private _resuNtfyTopic As TextBox
    Private _resuKeywordTriggerSeconds As NumericUpDown
    Private _resuKeepStandingEnabled As CheckBox
    Private _resuKeepStandingKey As ComboBox
    Private _resuKeepStandingIntervalSeconds As NumericUpDown
    Private _lastResuChatAlarmText As String = ""
    Private _lastResuChatAlarmSentAtUtc As DateTime = DateTime.MinValue
    Private Const ResuChatAlarmCooldownSeconds As Integer = 20

    Private Function BuildResuTab() As TabPage
        Dim tab As New TabPage("RESU") With {.BackColor = ThemeBg}
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(28, 20, 28, 20)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(New Label With {.Text = "RESU / PAID RESURRECTION", .AutoSize = True, .Font = New Font("Segoe UI", 17, FontStyle.Bold), .ForeColor = ThemeAccent, .Margin = New Padding(0, 0, 0, 12)})
        body.Controls.Add(New Label With {.Text = "Wait for a RESU request in chat, then select players and resurrect only while the configurable trigger window is active. Blacklisted players are skipped. Trade invitations, payment, and completion remain monitored after a cast.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeTextSecondary, .Margin = New Padding(0, 0, 0, 12)})
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top}
        _resuStart = New Button With {.Text = "Start RESU", .Width = 130, .Height = 34}
        AddHandler _resuStart.Click, AddressOf ToggleResu
        Dim overlay As New CheckBox With {.Text = "Show trade click overlay", .AutoSize = True, .Margin = New Padding(14, 9, 6, 6)}
        _resuShowOverlay = overlay
        AddHandler overlay.CheckedChanged, Sub() SetResuOverlay(overlay.Checked)
        actions.Controls.AddRange({_resuStart, overlay, New Label With {.Text = "F12 = stop   |   Background game input enabled", .AutoSize = True, .Margin = New Padding(14, 10, 0, 0)}})
        body.Controls.Add(actions)
        _resuStatus = New Label With {.Text = "Stopped. Calibrate and match the message examples to your server before starting.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeAccent, .Margin = New Padding(0, 10, 0, 14)}
        body.Controls.Add(_resuStatus)
        _resuOptions = New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2}
        _resuOptions.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 235))
        _resuOptions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        _resuSelectKey = ResuKeyPicker("TAB")
        _resuSelectKeyIntervalMs = New ResuNumericUpDown With {.Minimum = 50, .Maximum = 10000, .Increment = 50, .Value = 500, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        _resuCastKey = New TextBox With {
            .Width = 165,
            .CharacterCasing = CharacterCasing.Upper,
            .MaxLength = 10,
            .PlaceholderText = "Resurrection key"
        }
        Dim resurrectionAndBuffKeys As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True}
        resurrectionAndBuffKeys.Controls.Add(_resuCastKey)
        For index As Integer = 0 To 2
            Dim capturedIndex As Integer = index
            _resuBuffEnabled(index) = New CheckBox With {.Text = $"Buff {index + 1}", .AutoSize = True, .Margin = New Padding(14, 7, 3, 0)}
            _resuBuffKey(index) = New TextBox With {
                .Width = 72,
                .CharacterCasing = CharacterCasing.Upper,
                .MaxLength = 10,
                .Enabled = False,
                .PlaceholderText = "Key"
            }
            AddHandler _resuBuffEnabled(index).CheckedChanged,
                Sub()
                    _resuBuffKey(capturedIndex).Enabled = _resuBuffEnabled(capturedIndex).Checked
                End Sub
            resurrectionAndBuffKeys.Controls.Add(_resuBuffEnabled(index))
            resurrectionAndBuffKeys.Controls.Add(_resuBuffKey(index))
        Next
        AddResuRow("1. Select target key", _resuSelectKey)
        AddResuRow("Select target key interval (ms)", _resuSelectKeyIntervalMs)
        AddResuRow("2. Resurrection + buff keys", resurrectionAndBuffKeys)
        _resuCastPressCount = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 100, .Value = 10, .Dock = DockStyle.Fill}
        _resuCastBurstSeconds = New ResuNumericUpDown With {.Minimum = 0D, .Maximum = 30D, .DecimalPlaces = 1, .Increment = 0.1D, .Value = 1D, .Dock = DockStyle.Fill}
        AddResuRow("Resurrection key presses", _resuCastPressCount)
        AddResuRow("Burst duration, first-to-last (sec)", _resuCastBurstSeconds)
        AddResuRow("Resurrection spam", New Label With {.Text = "The selected resurrection key is pressed repeatedly. Example: 10 presses over 1.0 second. Set duration to 0 for the fastest possible burst. F12 can stop a burst.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        _resuPeriodicMessageEnabled = New CheckBox With {.Text = "Send Enter → message → Enter repeatedly", .AutoSize = True}
        _resuPeriodicMessageText = New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 200, .PlaceholderText = "Type the message to send"}
        _resuPeriodicMessageIntervalSeconds = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 86400, .Value = 60, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        AddResuRow("Periodic typed message", _resuPeriodicMessageEnabled)
        AddResuRow("Message text", _resuPeriodicMessageText)
        AddResuRow("Message interval (seconds)", _resuPeriodicMessageIntervalSeconds)
        AddResuRow("Message sequence", New Label With {.Text = "At each interval RESU sends Enter, types this message one key at a time into game chat, then sends Enter again. It does not use Ctrl+V or alter the clipboard.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        _resuScanMs = New ResuNumericUpDown With {.Minimum = 100, .Maximum = 5000, .Increment = 100, .Value = 500, .Dock = DockStyle.Fill}
        _resuTimeout = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 600, .Value = 1, .Dock = DockStyle.Fill}
        _resuMinimumPayment = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 1000000000D, .Value = 1, .ThousandsSeparator = True, .Dock = DockStyle.Fill}
        _resuBlacklistEnabled = New CheckBox With {.Text = "Enable blacklist filtering and automatic nonpayment entries", .AutoSize = True, .Checked = True}
        AddResuRow("Scan / click interval (ms)", _resuScanMs)
        AddResuRow("Payment deadline (seconds)", _resuTimeout)
        AddResuRow("Late payments", New Label With {.Text = "When the deadline passes, RESU continues to other targets without blacklisting the player. A matching payment can still be accepted later.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        AddResuRow("Minimum payment (rupiahs)", _resuMinimumPayment)
        AddResuRow("Blacklist", _resuBlacklistEnabled)
        Dim calibrate As New Button With {.Text = "Calibrate trade overlay and text regions", .AutoSize = True}
        AddHandler calibrate.Click, AddressOf CalibrateResu
        AddResuRow("Game screen calibration", calibrate)
        _resuCalibrationLabel = New Label With {.AutoSize = True, .MaximumSize = New Size(750, 0)}
        AddResuRow("Saved calibration", _resuCalibrationLabel)
        For Each item In New String() {"Invitation", "Trade window", "Payment received", "Nonpayment", "Trade completed / cancelled"}
            Dim box As New TextBox With {.Dock = DockStyle.Fill}
            _resuPatterns.Add(item, box)
            AddResuRow(item & " pattern", box)
        Next
        AddResuRow("Message matching", New Label With {.Text = "Patterns below are examples. Invitation and player-event patterns use (?<user>...) for the username; payment also uses (?<amount>...). The trade-window pattern only identifies the open window and does not need a username. Resurrection and trade completion are read from the game-message region; payment/nonpayment also use chat.", .AutoSize = True, .MaximumSize = New Size(750, 0)})
        _resuChatAlarmEnabled = New CheckBox With {.Text = "Notify me when chat mentions any keyword below", .AutoSize = True}
        _resuChatAlarmKeywords = New TextBox With {.Dock = DockStyle.Fill, .PlaceholderText = "ress, ressu, resu, res"}
        _resuChatAutoReplyEnabled = New CheckBox With {.Text = "Automatically reply when a keyword is detected", .AutoSize = True, .Checked = True}
        _resuChatAutoReplyText = New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 200, .Text = "I'm here I'm vidya soy vidya", .PlaceholderText = "Reply sent to game chat"}
        AddResuRow("Chat mention alarm", _resuChatAlarmEnabled)
        AddResuRow("Alarm keywords (comma-separated)", _resuChatAlarmKeywords)
        AddResuRow("Automatic chat reply", _resuChatAutoReplyEnabled)
        AddResuRow("Reply text", _resuChatAutoReplyText)
        _resuNtfyTopic = New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 200, .PlaceholderText = "Private ntfy topic for RESU alarms"}
        Dim resuNtfyRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True}
        _resuNtfyTopic.Width = 430
        Dim testResuNtfy As New Button With {.Text = "Test RESU ntfy", .AutoSize = True}
        AddHandler testResuNtfy.Click, Async Sub() Await TestResuNtfyAsync()
        resuNtfyRow.Controls.AddRange({_resuNtfyTopic, testResuNtfy})
        AddResuRow("RESU ntfy channel", resuNtfyRow)
        _resuKeywordTriggerSeconds = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 3600, .Value = 60, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        AddResuRow("Resurrection trigger time (seconds)", _resuKeywordTriggerSeconds)
        AddResuRow("Chat detection", New Label With {.Text = "RESU waits until one of these words appears in chat. A match enables target selection and resurrection spam for the configured time, sends the editable reply, and can send a phone notification. A new keyword mention refreshes the trigger time.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        _resuKeepStandingEnabled = New CheckBox With {.Text = "Press one key repeatedly while waiting for a RESU keyword", .AutoSize = True, .Checked = True}
        _resuKeepStandingKey = ResuKeyPicker("SPACE")
        _resuKeepStandingIntervalSeconds = New ResuNumericUpDown With {.Minimum = 1, .Maximum = 3600, .Value = 5, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        AddResuRow("Keep character standing", _resuKeepStandingEnabled)
        AddResuRow("Keep-standing key", _resuKeepStandingKey)
        AddResuRow("Keep-standing interval (seconds)", _resuKeepStandingIntervalSeconds)
        Dim save As New Button With {.Text = "Save settings", .AutoSize = True}
        AddHandler save.Click, Sub() SaveResuOptions()
        Dim preview As New Button With {.Text = "Read OCR once", .AutoSize = True}
        AddHandler preview.Click, Async Sub() Await PreviewResuAsync()
        Dim optionsActions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top}
        optionsActions.Controls.AddRange({save, preview})
        AddResuRow("", optionsActions)
        body.Controls.Add(_resuOptions)
        _resuOcr = New TextBox With {.ReadOnly = True, .Multiline = True, .ScrollBars = ScrollBars.Both, .WordWrap = False, .Dock = DockStyle.Top, .Height = 155, .Text = "OCR readings appear here. No OpenAI/API requests are used."}
        body.Controls.Add(_resuOcr)
        body.Controls.Add(New Label With {.Text = "BLACKLIST — ADD CHARACTER NAMES BY HAND", .AutoSize = True, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .ForeColor = ThemeAccent, .Margin = New Padding(0, 16, 0, 8)})
        body.Controls.Add(New Label With {.Text = "Paste one or several exact character names below. Separate names with a new line, comma, or semicolon. Blacklisted characters are skipped before the resurrection key is sent.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeTextSecondary, .Margin = New Padding(0, 0, 0, 8)})
        Dim blacklistActions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top, .WrapContents = True}
        Dim username As New TextBox With {.Width = 390, .Height = 58, .Multiline = True, .ScrollBars = ScrollBars.Vertical, .PlaceholderText = "Character names, one per line"}
        _resuBlacklistDraft = username
        Dim add As New Button With {.Text = "Add to blacklist", .AutoSize = True, .Height = 34, .Margin = New Padding(8, 4, 0, 0)}
        Dim remove As New Button With {.Text = "Remove selected", .AutoSize = True, .Height = 34, .Margin = New Padding(8, 4, 0, 0)}
        AddHandler add.Click,
            Sub()
                Dim invalid As New List(Of String)()
                Dim names = ResuService.ParseManualCharacterNames(username.Text, invalid)
                If names.Count = 0 Then
                    Dim detail = If(invalid.Count > 0, " Invalid: " & String.Join(", ", invalid.Take(5)), "")
                    SilentMessageBox.Show(Me, "Enter at least one valid character name. Names may contain letters, numbers, underscores, or hyphens." & detail, "RESU blacklist")
                    Return
                End If
                Dim added As Integer = 0
                For Each characterName In names
                    If Not _resuSettings.Blacklist.Any(Function(entry) String.Equals(entry.Username, characterName, StringComparison.OrdinalIgnoreCase)) Then
                        _resuSettings.Blacklist.Add(New ResuBlacklistEntry With {.Username = characterName, .Reason = "Manually blocked", .AddedUtc = DateTime.UtcNow})
                        added += 1
                    End If
                Next
                SaveResuBlacklist()
                username.Clear()
                SetResuStatus($"Added {added} character name(s) to the blacklist. {names.Count - added} already existed.")
                If invalid.Count > 0 Then SilentMessageBox.Show(Me, "These entries were not added because their names are invalid: " & String.Join(", ", invalid.Take(10)), "RESU blacklist")
            End Sub
        AddHandler remove.Click,
            Sub()
                If _resuBlacklist.SelectedRows.Count = 0 Then Return
                Dim name = CStr(_resuBlacklist.SelectedRows(0).Cells(0).Value)
                _resuSettings.Blacklist.RemoveAll(Function(entry) String.Equals(entry.Username, name, StringComparison.OrdinalIgnoreCase))
                SaveResuBlacklist()
                SetResuStatus($"Removed {name} from the blacklist.")
            End Sub
        blacklistActions.Controls.AddRange({username, add, remove})
        body.Controls.Add(blacklistActions)
        _resuBlacklist = New DataGridView With {.Dock = DockStyle.Top, .Height = 180, .ReadOnly = True, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .SelectionMode = DataGridViewSelectionMode.FullRowSelect, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .MultiSelect = False}
        _resuBlacklist.Columns.Add("Username", "Username")
        _resuBlacklist.Columns.Add("Reason", "Reason")
        _resuBlacklist.Columns.Add("Added", "Added")
        body.Controls.Add(_resuBlacklist)
        scroll.Controls.Add(body)
        tab.Controls.Add(scroll)
        AddHandler _resuTimer.Tick, Async Sub() Await TickResuAsync()
        ApplyPersistedResuState(Nothing)
        WireResuPersistence(body)
        Return tab
    End Function

    Private Sub AddResuRow(label As String, control As Control)
        Dim row = _resuOptions.RowCount
        _resuOptions.RowCount += 1
        _resuOptions.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        _resuOptions.Controls.Add(New Label With {.Text = label, .AutoSize = True, .Margin = New Padding(0, 7, 8, 7)}, 0, row)
        control.Margin = New Padding(0, 4, 0, 7)
        _resuOptions.Controls.Add(control, 1, row)
    End Sub

    Private Sub SetResuStatus(message As String)
        Dim clean = If(message, "").Trim()
        If clean.Length = 0 Then Return
        Dim category = ResuStatusCategory(clean)
        If _resuStatusHistory.Count > 0 AndAlso String.Equals(ResuStatusCategory(_resuStatusHistory(0)), category, StringComparison.OrdinalIgnoreCase) Then
            _resuStatusHistory(0) = clean
            _resuStatusTimes(0) = DateTime.Now
        Else
            _resuStatusHistory.Insert(0, clean)
            _resuStatusTimes.Insert(0, DateTime.Now)
            If _resuStatusHistory.Count > 5 Then
                _resuStatusHistory.RemoveRange(5, _resuStatusHistory.Count - 5)
                _resuStatusTimes.RemoveRange(5, _resuStatusTimes.Count - 5)
            End If
        End If
        _resuStatus.Text = String.Join(Environment.NewLine, _resuStatusHistory.Select(Function(item, index) $"{If(index = 0, "▶", "•")} {_resuStatusTimes(index):HH:mm:ss}  {item}"))
    End Sub

    Private Shared Function ResuStatusCategory(message As String) As String
        If message.StartsWith("Waiting for payment from ", StringComparison.OrdinalIgnoreCase) Then Return "waiting-payment"
        If message.StartsWith("Waiting for a RESU keyword", StringComparison.OrdinalIgnoreCase) Then Return "waiting-keyword"
        If message.StartsWith("Waiting ", StringComparison.OrdinalIgnoreCase) AndAlso message.EndsWith("target-key interval.", StringComparison.OrdinalIgnoreCase) Then Return "waiting-target-key"
        If message.StartsWith("Sending resurrection key for ", StringComparison.OrdinalIgnoreCase) Then Return "resurrection-progress"
        If message.StartsWith("Buffing ", StringComparison.OrdinalIgnoreCase) Then Return "buff-progress"
        Return message
    End Function

    Private Shared Function ResuKeyPicker(selected As String) As ComboBox
        Dim box As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        box.Items.Add("TAB")
        For index = 1 To 11
            box.Items.Add("F" & index)
        Next
        For Each character In "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            box.Items.Add(character.ToString())
        Next
        box.Items.Add("SPACE")
        box.SelectedItem = selected
        Return box
    End Function

    Private Sub WireResuPersistence(parent As Control)
        For Each child As Control In parent.Controls
            If TypeOf child Is NumericUpDown Then
                AddHandler DirectCast(child, NumericUpDown).ValueChanged, AddressOf ResuInputChanged
            ElseIf TypeOf child Is TextBox Then
                If Not DirectCast(child, TextBox).ReadOnly Then AddHandler child.TextChanged, AddressOf ResuInputChanged
            ElseIf TypeOf child Is CheckBox Then
                AddHandler DirectCast(child, CheckBox).CheckedChanged, AddressOf ResuInputChanged
            ElseIf TypeOf child Is ComboBox Then
                AddHandler DirectCast(child, ComboBox).SelectedIndexChanged, AddressOf ResuInputChanged
            Else
                WireResuPersistence(child)
            End If
        Next
    End Sub

    Private Sub ResuInputChanged(sender As Object, e As EventArgs)
        If _resuApplyingSettings OrElse Not IsHandleCreated OrElse IsDisposed Then Return
        SavePersistedListState(False)
    End Sub

    Private Function BuildPersistedResuState() As ResuSettings
        If _resuKeepStandingIntervalSeconds Is Nothing Then Return _resuSettings
        ' Snapshot current edits without changing the running service or requiring a valid runnable setup.
        Return ReadResuOptions()
    End Function

    Private Function ReadResuOptions() As ResuSettings
        Dim settings = JsonSerializer.Deserialize(Of ResuSettings)(JsonSerializer.Serialize(_resuSettings))
        settings.ShowTradeClickOverlay = _resuShowOverlay.Checked
        settings.BlacklistDraft = _resuBlacklistDraft.Text
        settings.SelectKey = CStr(_resuSelectKey.SelectedItem)
        settings.SelectKeyIntervalMs = CInt(_resuSelectKeyIntervalMs.Value)
        settings.ResurrectKey = _resuCastKey.Text.Trim().ToUpperInvariant()
        settings.BuffKeys = New List(Of ResuBuffKeySetting)()
        For index As Integer = 0 To 2
            settings.BuffKeys.Add(New ResuBuffKeySetting With {
                .Enabled = _resuBuffEnabled(index).Checked,
                .KeyName = _resuBuffKey(index).Text.Trim().ToUpperInvariant()
            })
        Next
        settings.PeriodicMessageEnabled = _resuPeriodicMessageEnabled.Checked
        settings.PeriodicMessageText = _resuPeriodicMessageText.Text.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        settings.PeriodicMessageIntervalSeconds = CInt(_resuPeriodicMessageIntervalSeconds.Value)
        settings.ResurrectPressCount = CInt(_resuCastPressCount.Value)
        settings.ResurrectBurstSeconds = _resuCastBurstSeconds.Value
        settings.ScanMs = CInt(_resuScanMs.Value)
        settings.PaymentTimeoutSeconds = CInt(_resuTimeout.Value)
        settings.MinimumPayment = CLng(_resuMinimumPayment.Value)
        settings.BlacklistEnabled = _resuBlacklistEnabled.Checked
        settings.InvitePattern = _resuPatterns("Invitation").Text
        settings.TradePattern = _resuPatterns("Trade window").Text
        settings.PaidPattern = _resuPatterns("Payment received").Text
        settings.UnpaidPattern = _resuPatterns("Nonpayment").Text
        settings.TradeClosedPattern = _resuPatterns("Trade completed / cancelled").Text
        settings.ChatAlarmEnabled = _resuChatAlarmEnabled.Checked
        settings.ChatAlarmKeywords = ParseBulkFilterNames(_resuChatAlarmKeywords.Text)
        settings.ChatAutoReplyEnabled = _resuChatAutoReplyEnabled.Checked
        settings.ChatAutoReplyText = _resuChatAutoReplyText.Text.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        settings.ResuNtfyTopic = _resuNtfyTopic.Text.Trim()
        settings.KeywordTriggerDurationSeconds = CInt(_resuKeywordTriggerSeconds.Value)
        settings.KeepStandingEnabled = _resuKeepStandingEnabled.Checked
        settings.KeepStandingKey = CStr(_resuKeepStandingKey.SelectedItem)
        settings.KeepStandingIntervalSeconds = CInt(_resuKeepStandingIntervalSeconds.Value)
        Return settings
    End Function

    Private Sub SaveResuOptions()
        Try
            Dim settings = ReadResuOptions()
            _resuSettings = settings
            SavePersistedListState(True)
            SetResuStatus("RESU settings saved.")
        Catch ex As Exception
            SilentMessageBox.Show(Me, ex.Message, "RESU settings")
        End Try
    End Sub

    Private Shared Sub ValidateResuKeys(settings As ResuSettings)
        If Not BotEngine.IsSupportedKeyName(settings.ResurrectKey) Then Throw New InvalidOperationException("Type a valid resurrection key, such as 3, F5, SPACE, ENTER, or a letter.")
        If String.Equals(settings.ResurrectKey, "F12", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("F12 is reserved for stopping RESU. Type a different resurrection key.")
        If String.Equals(settings.SelectKey, settings.ResurrectKey, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("Choose different target-selection and resurrection keys.")
        If settings.KeepStandingEnabled AndAlso Not BotEngine.IsSupportedKeyName(settings.KeepStandingKey) Then Throw New InvalidOperationException("Choose a valid keep-standing key.")
        Dim buffKeys As List(Of ResuBuffKeySetting) = If(settings.BuffKeys, New List(Of ResuBuffKeySetting)())
        For index As Integer = 0 To Math.Min(2, buffKeys.Count - 1)
            Dim buff As ResuBuffKeySetting = buffKeys(index)
            If buff Is Nothing OrElse Not buff.Enabled Then Continue For
            If Not BotEngine.IsSupportedKeyName(buff.KeyName) Then Throw New InvalidOperationException($"Type a valid key for Buff {index + 1}, or turn that buff off.")
            If String.Equals(buff.KeyName, "F12", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException($"F12 is reserved for stopping RESU. Choose a different key for Buff {index + 1}.")
        Next
    End Sub

    Private Sub ApplyPersistedResuState(settings As ResuSettings)
        _resuApplyingSettings = True
        Try
        _resuSettings = If(settings, New ResuSettings())
        If String.Equals(_resuSettings.InvitePattern, ResuService.LegacyDefaultInvitePattern, StringComparison.Ordinal) OrElse String.Equals(_resuSettings.InvitePattern, ResuService.PreviousDefaultInvitePattern, StringComparison.Ordinal) Then
            _resuSettings.InvitePattern = ResuService.DefaultInvitePattern
        End If
        If String.Equals(_resuSettings.TradePattern, ResuService.LegacyDefaultTradePattern, StringComparison.Ordinal) Then
            _resuSettings.TradePattern = ResuService.DefaultTradePattern
        End If
        If (_resuSettings.OpenTradeRegion Is Nothing OrElse _resuSettings.OpenTradeRegion.W < 15 OrElse _resuSettings.OpenTradeRegion.H < 10) AndAlso _resuSettings.TradeRegion IsNot Nothing Then
            _resuSettings.OpenTradeRegion = New RectRegion(_resuSettings.TradeRegion.X, _resuSettings.TradeRegion.Y, _resuSettings.TradeRegion.W, _resuSettings.TradeRegion.H)
        End If
        _resuSettings.Blacklist = If(_resuSettings.Blacklist, New List(Of ResuBlacklistEntry)()).Where(Function(entry) entry IsNot Nothing AndAlso ResuService.CleanUsername(entry.Username).Length > 0).GroupBy(Function(entry) entry.Username.Trim(), StringComparer.OrdinalIgnoreCase).Select(Function(group) group.First()).ToList()
        For Each entry In _resuSettings.Blacklist
            entry.Username = entry.Username.Trim()
        Next
        Dim savedAlarmKeywords = If(_resuSettings.ChatAlarmKeywords, New List(Of String)())
        If savedAlarmKeywords.Count = 3 AndAlso savedAlarmKeywords.Any(Function(value) String.Equals(value, "ress", StringComparison.OrdinalIgnoreCase)) AndAlso savedAlarmKeywords.Any(Function(value) String.Equals(value, "resu", StringComparison.OrdinalIgnoreCase)) AndAlso savedAlarmKeywords.Any(Function(value) String.Equals(value, "res", StringComparison.OrdinalIgnoreCase)) Then
            _resuSettings.ChatAlarmKeywords = New List(Of String) From {"ress", "ressu", "resu", "res"}
        End If
        _resuSelectKey.SelectedItem = If(_resuSelectKey.Items.Contains(_resuSettings.SelectKey), _resuSettings.SelectKey, "TAB")
        _resuSelectKeyIntervalMs.Value = Math.Clamp(_resuSettings.SelectKeyIntervalMs, 50, 10000)
        _resuCastKey.Text = If(_resuSettings.ResurrectKey, "").Trim().ToUpperInvariant()
        Dim savedBuffKeys As List(Of ResuBuffKeySetting) = If(_resuSettings.BuffKeys, New List(Of ResuBuffKeySetting)())
        For index As Integer = 0 To 2
            Dim savedBuff As ResuBuffKeySetting = If(index < savedBuffKeys.Count AndAlso savedBuffKeys(index) IsNot Nothing, savedBuffKeys(index), New ResuBuffKeySetting())
            _resuBuffEnabled(index).Checked = savedBuff.Enabled
            _resuBuffKey(index).Text = If(savedBuff.KeyName, "").Trim().ToUpperInvariant()
            _resuBuffKey(index).Enabled = savedBuff.Enabled
        Next
        _resuPeriodicMessageEnabled.Checked = _resuSettings.PeriodicMessageEnabled
        _resuPeriodicMessageText.Text = If(_resuSettings.PeriodicMessageText, "")
        _resuPeriodicMessageIntervalSeconds.Value = Math.Clamp(_resuSettings.PeriodicMessageIntervalSeconds, 1, 86400)
        _resuCastPressCount.Value = Math.Clamp(_resuSettings.ResurrectPressCount, 1, 100)
        _resuCastBurstSeconds.Value = Math.Clamp(_resuSettings.ResurrectBurstSeconds, 0D, 30D)
        _resuScanMs.Value = Math.Clamp(_resuSettings.ScanMs, 100, 5000)
        _resuTimeout.Value = Math.Clamp(_resuSettings.PaymentTimeoutSeconds, 1, 600)
        _resuMinimumPayment.Value = Math.Clamp(_resuSettings.MinimumPayment, 1L, 1000000000L)
        _resuBlacklistEnabled.Checked = _resuSettings.BlacklistEnabled
        _resuPatterns("Invitation").Text = _resuSettings.InvitePattern
        _resuPatterns("Trade window").Text = _resuSettings.TradePattern
        _resuPatterns("Payment received").Text = _resuSettings.PaidPattern
        _resuPatterns("Nonpayment").Text = _resuSettings.UnpaidPattern
        _resuPatterns("Trade completed / cancelled").Text = _resuSettings.TradeClosedPattern
        _resuChatAlarmEnabled.Checked = _resuSettings.ChatAlarmEnabled
        _resuChatAlarmKeywords.Text = String.Join(", ", If(_resuSettings.ChatAlarmKeywords, New List(Of String) From {"ress", "ressu", "resu", "res"}))
        _resuChatAutoReplyEnabled.Checked = _resuSettings.ChatAutoReplyEnabled
        _resuChatAutoReplyText.Text = If(_resuSettings.ChatAutoReplyText, "I'm here I'm vidya soy vidya")
        _resuNtfyTopic.Text = If(_resuSettings.ResuNtfyTopic, "").Trim()
        _resuKeywordTriggerSeconds.Value = Math.Clamp(_resuSettings.KeywordTriggerDurationSeconds, 1, 3600)
        _resuKeepStandingEnabled.Checked = _resuSettings.KeepStandingEnabled
        _resuKeepStandingKey.SelectedItem = If(_resuKeepStandingKey.Items.Contains(_resuSettings.KeepStandingKey), _resuSettings.KeepStandingKey, "SPACE")
        _resuKeepStandingIntervalSeconds.Value = Math.Clamp(_resuSettings.KeepStandingIntervalSeconds, 1, 3600)
        _resuBlacklistDraft.Text = If(_resuSettings.BlacklistDraft, "")
        _resuShowOverlay.Checked = _resuSettings.ShowTradeClickOverlay
        UpdateResuCalibrationLabel()
        RefreshResuBlacklist()
        Finally
            _resuApplyingSettings = False
        End Try
    End Sub

    Private Sub UpdateResuCalibrationLabel()
        _resuCalibrationLabel.Text = If(_resuSettings.ReferenceWidth > 0, $"Client {_resuSettings.ReferenceWidth} x {_resuSettings.ReferenceHeight}; overlay 5 invitation click {_resuSettings.InvitePoint}; overlay 6 open-trade detection; overlay 7 trade click {_resuSettings.AcceptPoint}.", "Not calibrated")
    End Sub

    Private Sub RefreshResuBlacklist()
        _resuBlacklist.Rows.Clear()
        For Each entry In _resuSettings.Blacklist.OrderBy(Function(item) item.Username)
            _resuBlacklist.Rows.Add(entry.Username, entry.Reason, entry.AddedUtc.ToLocalTime().ToString("g"))
        Next
    End Sub

    Private Sub SaveResuBlacklist()
        RefreshResuBlacklist()
        SavePersistedListState(False)
    End Sub

    Private Function ResuSelectedWindow() As IntPtr
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        Return If(selected Is Nothing, IntPtr.Zero, selected.MainWindowHandle)
    End Function

    Private Sub CalibrateResu(sender As Object, e As EventArgs)
        Try
            Dim hwnd = ResuSelectedWindow()
            If hwnd = IntPtr.Zero OrElse NativeMethods.IsIconic(hwnd) Then Throw New InvalidOperationException("Select and restore the Full game window first.")
            ForceSetForegroundWindow(hwnd)
            Using frame = BotEngine.CaptureClient(hwnd)
                Dim settings = ReadResuOptions()
                If settings.ReferenceWidth = 0 Then
                    Dim cfg = BuildConfig()
                    settings.ReferenceWidth = frame.Width
                    settings.ReferenceHeight = frame.Height
                    settings.TargetRegion = CloneQuizRegion(cfg.MobNameRect)
                    settings.ChatRegion = CloneQuizRegion(cfg.ChatRect)
                    settings.MessageRegion = CloneQuizRegion(cfg.UnreachableTextRect)
                End If
                Using dialog As New ResuCalibrationForm(frame, settings)
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                    _resuSettings = dialog.Settings
                End Using
            End Using
            UpdateResuCalibrationLabel()
            SavePersistedListState(True)
        Catch ex As Exception
            SilentMessageBox.Show(Me, ex.Message, "RESU calibration")
        End Try
    End Sub

    Private Shared Sub ValidateResuCalibration(settings As ResuSettings)
        Dim bounds As New Rectangle(0, 0, settings.ReferenceWidth, settings.ReferenceHeight)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Throw New InvalidOperationException("Calibrate RESU first.")
        For Each region As RectRegion In {settings.TargetRegion, settings.TradeRegion, settings.ChatRegion, settings.MessageRegion, settings.OpenTradeRegion}
            If region Is Nothing OrElse region.W < 15 OrElse region.H < 10 OrElse Not bounds.Contains(New Rectangle(region.X, region.Y, region.W, region.H)) Then Throw New InvalidOperationException("Calibrate all five RESU detection regions inside the game client.")
        Next
        Dim invitation As New Rectangle(settings.TradeRegion.X, settings.TradeRegion.Y, settings.TradeRegion.W, settings.TradeRegion.H)
        Dim openTrade As New Rectangle(settings.OpenTradeRegion.X, settings.OpenTradeRegion.Y, settings.OpenTradeRegion.W, settings.OpenTradeRegion.H)
        If Not invitation.Contains(settings.InvitePoint) Then Throw New InvalidOperationException("Set the invitation accept point inside the invitation detection region.")
        If Not openTrade.Contains(settings.AcceptPoint) Then Throw New InvalidOperationException("Set overlay 7 inside overlay 6, the open-trade detection region.")
    End Sub

    Private Sub ToggleResu(sender As Object, e As EventArgs)
        If RejectForegroundWorkflow("RESU") Then Return
        If _tradeRunning Then
            SilentMessageBox.Show(Me, "Stop Trade before starting RESU.", "Trade")
            Return
        End If
        If _resuRunning Then
            StopResu("RESU stopped.")
            Return
        End If
        Try
            If Not _quizUnlocked Then Return
            If _liteEngine.IsRunning() Then StopEdition(BotEdition.Lite, False, "starting RESU")
            If _fullEngine.IsRunning() Then StopEdition(BotEdition.Full, False, "starting RESU")
            Dim settings = ReadResuOptions()
            ValidateResuCalibration(settings)
            ValidateResuKeys(settings)
            _resuWindow = ResuSelectedWindow()
            If _resuWindow = IntPtr.Zero OrElse NativeMethods.IsIconic(_resuWindow) Then Throw New InvalidOperationException("Select and restore the Full game window first.")
            _resuService = New ResuService(settings)
            _resuSettings = settings
            _resuStatusHistory.Clear()
            _resuStatusTimes.Clear()
            _resuGeneration += 1
            If Not _workflowModes.Transition(OperatingMode.Resu, "RESU started") Then Throw New InvalidOperationException("Stop the other active workflow first.")
            _resuRunning = True
            PushLiveConfig()
            _resuOptions.Enabled = False
            _resuStart.Text = "Stop RESU"
            _resuNextScan = DateTime.MinValue
            _resuNextSelectKeyAt = DateTime.MinValue
            _resuNextPeriodicMessageAt = If(settings.PeriodicMessageEnabled, DateTime.UtcNow.AddSeconds(settings.PeriodicMessageIntervalSeconds), DateTime.MaxValue)
            _resuNextKeepStandingAt = If(settings.KeepStandingEnabled, DateTime.UtcNow, DateTime.MaxValue)
            _resuKeywordTriggerUntilUtc = DateTime.MinValue
            _lastResuChatAlarmText = ""
            _lastResuChatAlarmSentAtUtc = DateTime.MinValue
            _resuTradeVisible = False
            SavePersistedListState(False)
            _resuTimer.Start()
            UpdateMainTabIndicators()
            SetResuStatus("RESU started; waiting for a configured chat keyword.")
            AppendLog("RESU started. Any running Full or Lite bot was stopped first. F12 stops RESU; background input is enabled.")
        Catch ex As Exception
            SilentMessageBox.Show(Me, ex.Message, "RESU")
        End Try
    End Sub

    Private Sub StopResu(reason As String)
        If _workflowModes.Current = OperatingMode.Resu Then _workflowModes.Transition(OperatingMode.Idle, "RESU stopped")
        _resuRunning = False
        _resuGeneration += 1
        _resuTimer.Stop()
        _resuOptions.Enabled = True
        _resuStart.Text = "Start RESU"
        SetResuStatus(reason)
        If Not IsDisposed AndAlso Not Disposing AndAlso _fullEngine.IsRunning() Then PushLiveConfig()
        UpdateMainTabIndicators()
        AppendLog(reason)
    End Sub

    Private Function IsResuCompatibleBotState(hwnd As IntPtr) As Boolean
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If Not runningEdition.HasValue Then Return True
        If runningEdition.Value <> BotEdition.Full OrElse hwnd = IntPtr.Zero Then Return False
        If chkHoldPlaceEnabled Is Nothing OrElse Not chkHoldPlaceEnabled.Checked Then Return False
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        Return selected IsNot Nothing AndAlso selected.MainWindowHandle = hwnd
    End Function

    Private Shared Function ReadResuRegion(frame As Bitmap, region As RectRegion) As String
        Using crop = QuizImageTools.Crop(frame, New Rectangle(region.X, region.Y, region.W, region.H))
            Return OcrReader.ReadScreenTextIsolated(crop)
        End Using
    End Function

    Private Shared Function ReadResuTargetName(frame As Bitmap, region As RectRegion) As String
        Using crop = QuizImageTools.Crop(frame, New Rectangle(region.X, region.Y, region.W, region.H))
            Return OcrReader.ReadName(crop)
        End Using
    End Function

    Private Shared Function ReadResuTradeText(frame As Bitmap, region As RectRegion) As String
        Using crop = QuizImageTools.Crop(frame, New Rectangle(region.X, region.Y, region.W, region.H))
            Using enlarged As New Bitmap(Math.Max(1, crop.Width * 3), Math.Max(1, crop.Height * 3), Imaging.PixelFormat.Format24bppRgb)
                Using graphics = Drawing.Graphics.FromImage(enlarged)
                    graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                    graphics.PixelOffsetMode = Drawing.Drawing2D.PixelOffsetMode.Half
                    graphics.DrawImage(crop, New Rectangle(0, 0, enlarged.Width, enlarged.Height), New Rectangle(0, 0, crop.Width, crop.Height), Drawing.GraphicsUnit.Pixel)
                End Using
                Dim text = OcrReader.ReadScreenTextIsolated(enlarged)
                If Not String.IsNullOrWhiteSpace(text) Then Return text
            End Using
            Return OcrReader.ReadScreenTextIsolated(crop)
        End Using
    End Function

    Private Shared Function CaptureResuObservation(hwnd As IntPtr, settings As ResuSettings) As ResuObservation
        Using frame = BotEngine.CaptureClient(hwnd)
            If frame.Width <> settings.ReferenceWidth OrElse frame.Height <> settings.ReferenceHeight Then Throw New InvalidOperationException("Game size changed. Recalibrate RESU before continuing.")
            Dim observation As New ResuObservation With {
                .TargetName = ReadResuTargetName(frame, settings.TargetRegion),
                .InvitationText = ReadResuTradeText(frame, settings.TradeRegion),
                .TradeText = ReadResuTradeText(frame, settings.OpenTradeRegion),
                .ChatText = ReadResuRegion(frame, settings.ChatRegion),
                .MessageText = ReadResuRegion(frame, settings.MessageRegion)
            }
            Return observation
        End Using
    End Function

    Private Sub ShowResuObservation(observation As ResuObservation)
        _resuOcr.Text = $"TARGET: {observation.TargetName}{vbCrLf}INVITATION WINDOW (overlay 2):{vbCrLf}{observation.InvitationText}{vbCrLf}OPEN TRADE WINDOW (overlay 6):{vbCrLf}{observation.TradeText}{vbCrLf}CHAT:{vbCrLf}{observation.ChatText}{vbCrLf}UNREACHABLE TEXT / MESSAGES:{vbCrLf}{observation.MessageText}"
    End Sub

    Private Async Function PreviewResuAsync() As Task
        If _resuBusy OrElse _resuRunning Then Return
        _resuBusy = True
        Try
            Dim settings = ReadResuOptions()
            ValidateResuCalibration(settings)
            Dim hwnd = ResuSelectedWindow()
            If hwnd = IntPtr.Zero OrElse NativeMethods.IsIconic(hwnd) Then Throw New InvalidOperationException("Select and restore the Full game window first.")
            ForceSetForegroundWindow(hwnd)
            Dim observation = Await Task.Run(Function() CaptureResuObservation(hwnd, settings))
            If IsDisposed OrElse Disposing Then Return
            ShowResuObservation(observation)
            SetResuStatus("OCR preview updated. Match the patterns to the exact game messages shown below.")
        Catch ex As Exception
            If Not IsDisposed AndAlso Not Disposing Then SetResuStatus(ex.Message)
        Finally
            _resuBusy = False
        End Try
    End Function

    Private Function CanResuAct(generation As Integer, hwnd As IntPtr) As Boolean
        If Not _resuRunning OrElse generation <> _resuGeneration OrElse IsDisposed OrElse Disposing Then Return False
        If ResuSelectedWindow() <> hwnd OrElse NativeMethods.IsIconic(hwnd) Then Return False
        If Not IsResuCompatibleBotState(hwnd) Then Return False
        Dim rect As NativeMethods.RECT
        Return NativeMethods.GetClientRect(hwnd, rect) AndAlso rect.Right - rect.Left = _resuSettings.ReferenceWidth AndAlso rect.Bottom - rect.Top = _resuSettings.ReferenceHeight
    End Function

    Private Async Function TickResuAsync() As Task
        If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
            StopResu("RESU stopped with F12.")
            Return
        End If
        If Not _resuRunning Then Return
        Dim generation = _resuGeneration
        Dim hwnd = _resuWindow
        If ResuSelectedWindow() <> hwnd OrElse Not IsResuCompatibleBotState(hwnd) Then
            StopResu("RESU stopped: the selected window changed, Lite started, or Full is running without Max Range.")
            Return
        End If
        If Not CanResuAct(generation, hwnd) Then
            SetResuStatus("Paused: restore the selected game at the calibrated client size.")
            ' Invalidate a worker even if the window is restored before its OCR finishes.
            _resuGeneration += 1
            _resuService.PauseMonitoring()
            Return
        End If
        If Not _resuBusy AndAlso Await TrySendResuKeepStandingKeyAsync(hwnd, generation) Then Return
        If Not _resuBusy AndAlso Await TrySendResuPeriodicMessageAsync(hwnd, generation) Then Return
        If _resuBusy OrElse DateTime.UtcNow < _resuNextScan Then Return
        _resuBusy = True
        Dim scanStartedAt = DateTime.UtcNow
        Dim tradeVisibleThisScan As Boolean = False
        Try
            Dim settings = _resuSettings
            Dim observation = Await Task.Run(Function() CaptureResuObservation(hwnd, settings))
            If Not CanResuAct(generation, hwnd) Then Return
            tradeVisibleThisScan = ResuService.HasTradeType(settings, observation.InvitationText, True) OrElse ResuService.HasTradeType(settings, observation.TradeText, False)
            _resuTradeVisible = tradeVisibleThisScan
            ShowResuObservation(observation)
            Await CheckResuChatAlarmAsync(hwnd, generation, settings, observation.ChatText)
            observation.ResurrectionTriggered = ResuService.IsKeywordTriggerActive(_resuKeywordTriggerUntilUtc, DateTime.UtcNow)
            If _resuService.PendingUsername.Length > 0 AndAlso String.IsNullOrWhiteSpace(observation.ChatText) AndAlso String.IsNullOrWhiteSpace(observation.MessageText) Then
                _resuService.PauseMonitoring()
                SetResuStatus("Payment monitoring paused: chat and game-message OCR are empty. Check the calibrated regions.")
                Return
            End If
            Dim decision = _resuService.Observe(observation, DateTime.UtcNow)
            If _resuService.BlacklistChanged Then
                _resuService.BlacklistChanged = False
                SaveResuBlacklist()
                AppendLog("RESU: " & _resuService.Status)
            End If
            If decision.Action <> ResuAction.None Then
                ' OCR is asynchronous. Re-read the relevant region immediately before input and
                ' check the window, generation, identity and blacklist again after the await.
                ' Selecting another target has no identity to validate. Avoid two additional OCR
                ' passes here; they made every configured target-key interval several seconds late.
                Dim allowed = If(decision.Action = ResuAction.SelectTarget,
                                 CanResuAct(generation, hwnd),
                                 Await Task.Run(Function() RevalidateResuAction(hwnd, settings, decision)))
                If Not CanResuAct(generation, hwnd) Then Return
                If allowed AndAlso (decision.Username.Length = 0 OrElse Not _resuService.IsBlocked(decision.Username)) Then
                    Dim sent As Boolean
                    Select Case decision.Action
                        Case ResuAction.SelectTarget
                            Dim remainingMs = ResuService.SelectKeyWaitMilliseconds(_resuNextSelectKeyAt, DateTime.UtcNow)
                            If remainingMs > 0 Then
                                SetResuStatus($"Waiting {remainingMs:N0} ms for the target-key interval.")
                                Await Task.Delay(remainingMs)
                            End If
                            If Not CanResuAct(generation, hwnd) Then Return
                            sent = BotEngine.SendKey(hwnd, settings.SelectKey, 30, forceBackgroundPost:=True)
                            If sent Then
                                _resuNextSelectKeyAt = DateTime.UtcNow.AddMilliseconds(settings.SelectKeyIntervalMs)
                                Dim selectionBurst = Await SendUntrackedResurrectionBurstAsync(hwnd, settings, generation)
                                AppendLog($"RESU: target key sent with {selectionBurst}/{settings.ResurrectPressCount} resurrection-key press(es).")
                            End If
                        Case ResuAction.Resurrect
                            Dim pressesSent = Await SendResurrectionBurstAsync(hwnd, settings, decision, generation)
                            sent = pressesSent > 0
                            If sent Then
                                AppendLog($"RESU: sent resurrection key {pressesSent}/{settings.ResurrectPressCount} time(s) across {settings.ResurrectBurstSeconds:0.0} second(s) for {decision.Username}.")
                            End If
                        Case ResuAction.AcceptInvite
                            sent = ClickResuPoint(hwnd, settings.InvitePoint)
                        Case ResuAction.AcceptTrade
                            sent = ClickResuPoint(hwnd, settings.AcceptPoint)
                    End Select
                    If sent AndAlso decision.Action <> ResuAction.Resurrect Then
                        _resuService.ActionSucceeded(decision)
                    End If
                End If
            End If
            SetResuStatus(_resuService.Status)
        Catch ex As Exception
            If generation = _resuGeneration AndAlso Not IsDisposed AndAlso Not Disposing Then StopResu("RESU stopped: " & ex.Message)
        Finally
            _resuBusy = False
            If tradeVisibleThisScan Then
                ' Start visible-trade scans on a fixed 500 ms cadence. Measuring from scan start
                ' prevents OCR/revalidation time from being added to every interval.
                _resuNextScan = scanStartedAt.AddMilliseconds(500)
                If _resuNextScan < DateTime.UtcNow Then _resuNextScan = DateTime.UtcNow
            Else
                ' Anchor the cadence to when this scan started so OCR duration is not added to the
                ' configured scan and target-selection intervals.
                _resuNextScan = scanStartedAt.AddMilliseconds(_resuSettings.ScanMs)
                If _resuNextScan < DateTime.UtcNow Then _resuNextScan = DateTime.UtcNow
            End If
        End Try
    End Function

    Private Async Function TrySendResuPeriodicMessageAsync(hwnd As IntPtr, generation As Integer) As Task(Of Boolean)
        Dim settings = _resuSettings
        If Not settings.PeriodicMessageEnabled OrElse _resuTradeVisible OrElse DateTime.UtcNow < _resuNextPeriodicMessageAt Then Return False
        _resuBusy = True
        _resuNextPeriodicMessageAt = DateTime.UtcNow.AddSeconds(settings.PeriodicMessageIntervalSeconds)
        Try
            Dim sent = Await Task.Run(Function() BotEngine.SendChatMessageSequence(hwnd, settings.PeriodicMessageText))
            If Not CanResuAct(generation, hwnd) Then Return True
            If sent Then
                SetResuStatus($"Periodic message sent. Next message in {settings.PeriodicMessageIntervalSeconds:N0} second(s).")
                AppendLog("RESU periodic message sent: " & settings.PeriodicMessageText)
            Else
                SetResuStatus("Periodic message could not be sent; RESU will retry at the next interval.")
                AppendLog("RESU periodic message failed to send.")
            End If
            Return True
        Finally
            _resuBusy = False
        End Try
    End Function

    Private Async Function TrySendResuKeepStandingKeyAsync(hwnd As IntPtr, generation As Integer) As Task(Of Boolean)
        Dim settings = _resuSettings
        Dim now = DateTime.UtcNow
        If Not ResuService.ShouldPressKeepStanding(settings.KeepStandingEnabled, _resuTradeVisible, _resuKeywordTriggerUntilUtc, _resuNextKeepStandingAt, now) Then Return False
        _resuBusy = True
        _resuNextKeepStandingAt = DateTime.UtcNow.AddSeconds(settings.KeepStandingIntervalSeconds)
        Try
            Dim sent = Await Task.Run(Function() BotEngine.SendKey(hwnd, settings.KeepStandingKey, 30, forceBackgroundPost:=True))
            If Not CanResuAct(generation, hwnd) Then Return True
            If Not sent Then AppendLog($"RESU keep-standing key {settings.KeepStandingKey} could not be sent.")
            Return True
        Finally
            _resuBusy = False
        End Try
    End Function

    Private Async Function SendResurrectionBurstAsync(hwnd As IntPtr, settings As ResuSettings, decision As ResuDecision, generation As Integer) As Task(Of Integer)
        Dim sent As Integer = 0
        Dim schedule = Diagnostics.Stopwatch.StartNew()
        For pressIndex = 0 To settings.ResurrectPressCount - 1
            Dim dueMs = ResuService.ResurrectionBurstOffsetMs(pressIndex, settings.ResurrectPressCount, settings.ResurrectBurstSeconds)
            Dim waitMs = dueMs - CInt(schedule.ElapsedMilliseconds)
            If waitMs > 0 Then Await Task.Delay(waitMs)
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
                StopResu("RESU stopped with F12 during the resurrection-key burst.")
                Return sent
            End If
            If Not CanResuAct(generation, hwnd) OrElse _resuService.IsBlocked(decision.Username) Then Return sent
            If Not BotEngine.SendKey(hwnd, settings.ResurrectKey, 30, forceBackgroundPost:=True) Then Return sent
            sent += 1
            If sent = 1 Then
                _resuService.ActionSucceeded(decision)
                AppendLog("RESU: " & _resuService.Status)
            End If
            SetResuStatus($"Sending resurrection key for {decision.Username}: {sent}/{settings.ResurrectPressCount} press(es).")
        Next
        Dim buffKeysSent As Integer = Await SendResuBuffKeysAsync(hwnd, settings, decision, generation)
        If buffKeysSent > 0 Then AppendLog($"RESU: sent {buffKeysSent} enabled buff key(s) to {decision.Username} after resurrection.")
        Return sent
    End Function

    Private Async Function SendResuBuffKeysAsync(hwnd As IntPtr, settings As ResuSettings, decision As ResuDecision, generation As Integer) As Task(Of Integer)
        Dim enabledBuffs As List(Of ResuBuffKeySetting) = If(settings.BuffKeys, New List(Of ResuBuffKeySetting)()).
            Where(Function(buff) buff IsNot Nothing AndAlso buff.Enabled AndAlso Not String.IsNullOrWhiteSpace(buff.KeyName)).
            Take(3).
            ToList()
        If enabledBuffs.Count = 0 Then Return 0

        ' Give the game a brief moment to apply the resurrection while keeping the resurrected
        ' character selected, then cast each enabled buff once through background window input.
        Dim schedule = Diagnostics.Stopwatch.StartNew()
        Dim sent As Integer = 0
        For buffIndex = 0 To enabledBuffs.Count - 1
            Dim buff = enabledBuffs(buffIndex)
            Dim dueMs = ResuService.BuffKeyOffsetMs(buffIndex)
            Dim waitMs = dueMs - CInt(schedule.ElapsedMilliseconds)
            If waitMs > 0 Then Await Task.Delay(waitMs)
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
                StopResu("RESU stopped with F12 during the buff-key sequence.")
                Return sent
            End If
            If Not CanResuAct(generation, hwnd) OrElse _resuService.IsBlocked(decision.Username) Then Return sent
            If BotEngine.SendKey(hwnd, buff.KeyName.Trim().ToUpperInvariant(), 30, forceBackgroundPost:=True) Then
                sent += 1
                SetResuStatus($"Buffing {decision.Username}: {buffIndex + 1}/{enabledBuffs.Count} key(s).")
            Else
                AppendLog($"RESU: Buff {buffIndex + 1} key {buff.KeyName} could not be sent; continuing with the remaining enabled buffs.")
            End If
        Next
        Return sent
    End Function

    Private Shared Function RevalidateResuAction(hwnd As IntPtr, settings As ResuSettings, decision As ResuDecision) As Boolean
        Using frame = BotEngine.CaptureClient(hwnd)
            If frame.Width <> settings.ReferenceWidth OrElse frame.Height <> settings.ReferenceHeight Then Return False
            If decision.Action = ResuAction.SelectTarget OrElse decision.Action = ResuAction.Resurrect Then
                Dim invitation = ReadResuTradeText(frame, settings.TradeRegion)
                Dim openTrade = ReadResuTradeText(frame, settings.OpenTradeRegion)
                If ResuService.HasTradeType(settings, invitation, True) OrElse ResuService.HasTradeType(settings, openTrade, False) Then Return False
                If decision.Action = ResuAction.SelectTarget Then Return True
                Return String.Equals(ResuService.ExtractTargetUsername(ReadResuTargetName(frame, settings.TargetRegion)), decision.Username, StringComparison.OrdinalIgnoreCase)
            End If
            Dim region = If(decision.Action = ResuAction.AcceptInvite, settings.TradeRegion, settings.OpenTradeRegion)
            Return ResuService.HasTradeType(settings, ReadResuTradeText(frame, region), decision.Action = ResuAction.AcceptInvite)
        End Using
    End Function

    Private Shared Function ClickResuPoint(hwnd As IntPtr, point As DrawingPoint) As Boolean
        Return BotEngine.ClickClientPoint(hwnd, point.X, point.Y)
    End Function

    ' A case-insensitive keyword watch over the calibrated chat OCR. A new matching line opens the
    ' resurrection trigger window immediately. Reply and phone-notification output share a cooldown
    ' so lingering OCR cannot spam, while another new mention can still refresh the trigger window.
    Private Async Function CheckResuChatAlarmAsync(hwnd As IntPtr, generation As Integer, settings As ResuSettings, chatText As String) As Task
        Dim text As String = If(chatText, "").Trim()
        If text.Length = 0 OrElse String.Equals(text, _lastResuChatAlarmText, StringComparison.Ordinal) Then Return
        _lastResuChatAlarmText = text
        Dim matched As String = ResuService.FindChatAlarmKeyword(text, settings.ChatAlarmKeywords)
        If matched Is Nothing Then Return
        _resuKeywordTriggerUntilUtc = DateTime.UtcNow.AddSeconds(settings.KeywordTriggerDurationSeconds)
        ' Keep-standing pauses during the active resurrection window. Its existing schedule is
        ' already due by expiry, so waiting-mode key presses resume immediately afterward.
        SetResuStatus($"RESU keyword detected. Resurrection enabled for {settings.KeywordTriggerDurationSeconds:N0} seconds.")
        AppendLog($"RESU keyword trigger active for {settings.KeywordTriggerDurationSeconds:N0} seconds after matching ""{matched}"".")
        If (DateTime.UtcNow - _lastResuChatAlarmSentAtUtc).TotalSeconds < ResuChatAlarmCooldownSeconds Then Return
        _lastResuChatAlarmSentAtUtc = DateTime.UtcNow
        AppendLog($"RESU chat keyword matched ""{matched}"" in chat - ""{text}"".")
        If settings.ChatAutoReplyEnabled AndAlso CanResuAct(generation, hwnd) Then
            Dim replySent = Await Task.Run(Function() BotEngine.SendChatMessageSequence(hwnd, settings.ChatAutoReplyText))
            If replySent Then
                AppendLog("RESU automatic chat reply sent: " & settings.ChatAutoReplyText)
            Else
                AppendLog("RESU automatic chat reply could not be sent.")
            End If
        End If
        If settings.ChatAlarmEnabled Then Await SendResuNotificationAsync("RESU chat alarm", $"Chat mentioned ""{matched}"": {text}")
    End Function

    Private Async Function SendResuNotificationAsync(title As String, body As String) As Task(Of Boolean)
        Dim topic = If(_resuSettings.ResuNtfyTopic, "").Trim()
        If topic.Length = 0 Then Return Await SendPhoneNotificationAsync(title, body)
        Dim sent = Await SendPhoneNotificationToTopicAsync(title, body, topic, forceNtfy:=True)
        If sent Then AppendLogSafe($"RESU notification sent to ntfy topic '{topic}'.")
        Return sent
    End Function

    Private Async Function TestResuNtfyAsync() As Task
        Dim topic = If(_resuNtfyTopic.Text, "").Trim()
        If topic.Length = 0 Then
            SilentMessageBox.Show(Me, "Enter a private ntfy topic for RESU first.", "RESU ntfy")
            Return
        End If
        Dim sent = Await SendPhoneNotificationToTopicAsync("KathanaBot RESU test", "Your separate RESU ntfy channel is working.", topic, forceNtfy:=True)
        SetResuStatus(If(sent, $"RESU ntfy test sent to '{topic}'.", $"RESU ntfy test failed for '{topic}'."))
    End Function

    Private Async Function SendUntrackedResurrectionBurstAsync(hwnd As IntPtr, settings As ResuSettings, generation As Integer) As Task(Of Integer)
        Dim sent As Integer = 0
        Dim schedule = Diagnostics.Stopwatch.StartNew()
        For pressIndex = 0 To settings.ResurrectPressCount - 1
            Dim waitMs = ResuService.ResurrectionBurstOffsetMs(pressIndex, settings.ResurrectPressCount, settings.ResurrectBurstSeconds) - CInt(schedule.ElapsedMilliseconds)
            If waitMs > 0 Then Await Task.Delay(waitMs)
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
                StopResu("RESU stopped with F12 during the target-selection resurrection burst.")
                Return sent
            End If
            If Not CanResuAct(generation, hwnd) Then Return sent
            If Not BotEngine.SendKey(hwnd, settings.ResurrectKey, 30, forceBackgroundPost:=True) Then Return sent
            sent += 1
        Next
        Return sent
    End Function

    Private Sub SetResuOverlay(visible As Boolean)
        If _resuOverlay IsNot Nothing Then
            _resuOverlay.Close()
            _resuOverlay = Nothing
        End If
        If Not visible Then Return
        _resuOverlay = New AutoRelaunchClickOverlayForm(AddressOf ResuSelectedWindow,
            Function()
                Dim steps As New List(Of AutoRelaunchOverlayStep)
                If _resuSettings.InvitePoint.X >= 0 Then steps.Add(New AutoRelaunchOverlayStep With {.StepNumber = 5, .X = _resuSettings.InvitePoint.X, .Y = _resuSettings.InvitePoint.Y, .TimingLabel = "On matching invitation", .Description = "RESU: invitation OK click"})
                If _resuSettings.OpenTradeRegion IsNot Nothing AndAlso _resuSettings.OpenTradeRegion.W > 0 AndAlso _resuSettings.OpenTradeRegion.H > 0 Then
                    steps.Add(New AutoRelaunchOverlayStep With {
                        .StepNumber = 6,
                        .X = _resuSettings.OpenTradeRegion.X + (_resuSettings.OpenTradeRegion.W \ 2),
                        .Y = _resuSettings.OpenTradeRegion.Y + (_resuSettings.OpenTradeRegion.H \ 2),
                        .RegionWidth = _resuSettings.OpenTradeRegion.W,
                        .RegionHeight = _resuSettings.OpenTradeRegion.H,
                        .TimingLabel = "Detect before clicking",
                        .Description = "RESU: open Trade/Rupiah/Cancel window"
                    })
                End If
                If _resuSettings.AcceptPoint.X >= 0 Then steps.Add(New AutoRelaunchOverlayStep With {.StepNumber = 7, .X = _resuSettings.AcceptPoint.X, .Y = _resuSettings.AcceptPoint.Y, .TimingLabel = "Every 500ms while overlay 6 matches", .Description = "RESU: Trade confirmation click"})
                Return steps
            End Function)
        _resuOverlay.Show(Me)
    End Sub

    Private Sub ShutdownResu()
        If _workflowModes.Current = OperatingMode.Resu Then _workflowModes.Transition(OperatingMode.Idle, "RESU stopped")
        _resuRunning = False
        _resuGeneration += 1
        _resuTimer.Stop()
        _resuTimer.Dispose()
        If _resuOverlay IsNot Nothing Then _resuOverlay.Close()
    End Sub
End Class

' RESU has many closely stacked numeric fields. Ignore wheel input at the control itself so an
' ordinary tab scroll cannot silently change whichever number happens to be under the pointer.
Friend NotInheritable Class ResuNumericUpDown
    Inherits NumericUpDown

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Dim handled = TryCast(e, HandledMouseEventArgs)
        If handled IsNot Nothing Then handled.Handled = True
        ' Intentionally do not call MyBase: the spinner value must change only through typing or
        ' its arrow buttons.
    End Sub
End Class

