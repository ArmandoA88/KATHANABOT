Imports System.Threading.Tasks
Imports System.Text.Json
Imports DrawingPoint = System.Drawing.Point

Partial Public Class Form1
    Private _resuTab As TabPage
    Private _resuSettings As New ResuSettings()
    Private _resuService As ResuService
    Private ReadOnly _resuTimer As New System.Windows.Forms.Timer With {.Interval = 100}
    Private _resuBusy As Boolean
    Private _resuGeneration As Integer
    Private _resuNextScan As DateTime
    Private _resuNextSelectKeyAt As DateTime
    Private _resuNextPeriodicMessageAt As DateTime
    Private _resuTradeVisible As Boolean
    Private _resuWindow As IntPtr
    Private _resuOverlay As AutoRelaunchClickOverlayForm
    Private _resuRunning As Boolean
    Private _resuOptions As TableLayoutPanel
    Private _resuStart As Button
    Private _resuStatus As Label
    Private _resuCalibrationLabel As Label
    Private _resuOcr As TextBox
    Private _resuSelectKey As ComboBox
    Private _resuSelectKeyIntervalMs As NumericUpDown
    Private _resuCastKey As TextBox
    Private _resuPeriodicMessageEnabled As CheckBox
    Private _resuPeriodicMessageText As TextBox
    Private _resuPeriodicMessageIntervalSeconds As NumericUpDown
    Private _resuCastPressCount As NumericUpDown
    Private _resuCastBurstSeconds As NumericUpDown
    Private _resuScanMs As NumericUpDown
    Private _resuTimeout As NumericUpDown
    Private _resuMinimumPayment As NumericUpDown
    Private _resuBlacklist As DataGridView
    Private ReadOnly _resuPatterns As New Dictionary(Of String, TextBox)()

    Private Function BuildResuTab() As TabPage
        Dim tab As New TabPage("RESU") With {.BackColor = ThemeBg}
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(28, 20, 28, 20)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(New Label With {.Text = "RESU / PAID RESURRECTION", .AutoSize = True, .Font = New Font("Segoe UI", 17, FontStyle.Bold), .ForeColor = ThemeAccent, .Margin = New Padding(0, 0, 0, 12)})
        body.Controls.Add(New Label With {.Text = "Select a player, read their name, and resurrect only if they are not blacklisted. Accept that player's trade and repeat left-clicks on OK while the trade remains visible. Local OCR watches chat and the unreachable-text message region for resurrection, payment, and trade completion.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeTextSecondary, .Margin = New Padding(0, 0, 0, 12)})
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top}
        _resuStart = New Button With {.Text = "Start RESU", .Width = 130, .Height = 34}
        AddHandler _resuStart.Click, AddressOf ToggleResu
        Dim overlay As New CheckBox With {.Text = "Show trade click overlay", .AutoSize = True, .Margin = New Padding(14, 9, 6, 6)}
        AddHandler overlay.CheckedChanged, Sub() SetResuOverlay(overlay.Checked)
        actions.Controls.AddRange({_resuStart, overlay, New Label With {.Text = "F12 = stop   |   Background game input enabled", .AutoSize = True, .Margin = New Padding(14, 10, 0, 0)}})
        body.Controls.Add(actions)
        _resuStatus = New Label With {.Text = "Stopped. Calibrate and match the message examples to your server before starting.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeAccent, .Margin = New Padding(0, 10, 0, 14)}
        body.Controls.Add(_resuStatus)
        _resuOptions = New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2}
        _resuOptions.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 235))
        _resuOptions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        _resuSelectKey = ResuKeyPicker("TAB")
        _resuSelectKeyIntervalMs = New NumericUpDown With {.Minimum = 50, .Maximum = 10000, .Increment = 50, .Value = 500, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        _resuCastKey = New TextBox With {
            .Dock = DockStyle.Fill,
            .CharacterCasing = CharacterCasing.Upper,
            .MaxLength = 10,
            .PlaceholderText = "Type the resurrection key, for example 3 or F5"
        }
        AddResuRow("1. Select target key", _resuSelectKey)
        AddResuRow("Select target key interval (ms)", _resuSelectKeyIntervalMs)
        AddResuRow("2. Resurrection key", _resuCastKey)
        _resuCastPressCount = New NumericUpDown With {.Minimum = 1, .Maximum = 100, .Value = 10, .Dock = DockStyle.Fill}
        _resuCastBurstSeconds = New NumericUpDown With {.Minimum = 0D, .Maximum = 30D, .DecimalPlaces = 1, .Increment = 0.1D, .Value = 1D, .Dock = DockStyle.Fill}
        AddResuRow("Resurrection key presses", _resuCastPressCount)
        AddResuRow("Burst duration, first-to-last (sec)", _resuCastBurstSeconds)
        AddResuRow("Resurrection spam", New Label With {.Text = "The selected resurrection key is pressed repeatedly. Example: 10 presses over 1.0 second. Set duration to 0 for the fastest possible burst. F12 can stop a burst.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        _resuPeriodicMessageEnabled = New CheckBox With {.Text = "Send Enter → message → Enter repeatedly", .AutoSize = True}
        _resuPeriodicMessageText = New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 200, .PlaceholderText = "Type the message to send"}
        _resuPeriodicMessageIntervalSeconds = New NumericUpDown With {.Minimum = 1, .Maximum = 86400, .Value = 60, .Dock = DockStyle.Fill, .ThousandsSeparator = True}
        AddResuRow("Periodic typed message", _resuPeriodicMessageEnabled)
        AddResuRow("Message text", _resuPeriodicMessageText)
        AddResuRow("Message interval (seconds)", _resuPeriodicMessageIntervalSeconds)
        AddResuRow("Message sequence", New Label With {.Text = "At each interval RESU sends Enter, types this message one key at a time into game chat, then sends Enter again. It does not use Ctrl+V or alter the clipboard.", .AutoSize = True, .MaximumSize = New Size(750, 0), .ForeColor = ThemeTextSecondary})
        _resuScanMs = New NumericUpDown With {.Minimum = 100, .Maximum = 5000, .Increment = 100, .Value = 500, .Dock = DockStyle.Fill}
        _resuTimeout = New NumericUpDown With {.Minimum = 10, .Maximum = 600, .Value = 60, .Dock = DockStyle.Fill}
        _resuMinimumPayment = New NumericUpDown With {.Minimum = 1, .Maximum = 1000000000D, .Value = 1, .ThousandsSeparator = True, .Dock = DockStyle.Fill}
        AddResuRow("Scan / click interval (ms)", _resuScanMs)
        AddResuRow("Payment deadline (seconds)", _resuTimeout)
        AddResuRow("Minimum payment (rupiahs)", _resuMinimumPayment)
        Dim calibrate As New Button With {.Text = "Calibrate trade overlay and text regions", .AutoSize = True}
        AddHandler calibrate.Click, AddressOf CalibrateResu
        AddResuRow("Game screen calibration", calibrate)
        _resuCalibrationLabel = New Label With {.AutoSize = True, .MaximumSize = New Size(750, 0)}
        AddResuRow("Saved calibration", _resuCalibrationLabel)
        For Each item In New String() {"Invitation", "Trade window", "Resurrection confirmed", "Payment received", "Nonpayment", "Trade completed / cancelled"}
            Dim box As New TextBox With {.Dock = DockStyle.Fill}
            _resuPatterns.Add(item, box)
            AddResuRow(item & " pattern", box)
        Next
        AddResuRow("Message matching", New Label With {.Text = "Patterns below are examples. Invitation and player-event patterns use (?<user>...) for the username; payment also uses (?<amount>...). The trade-window pattern only identifies the open window and does not need a username. Resurrection and trade completion are read from the game-message region; payment/nonpayment also use chat.", .AutoSize = True, .MaximumSize = New Size(750, 0)})
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
        Dim add As New Button With {.Text = "Add to blacklist", .AutoSize = True, .Height = 34, .Margin = New Padding(8, 4, 0, 0)}
        Dim remove As New Button With {.Text = "Remove selected", .AutoSize = True, .Height = 34, .Margin = New Padding(8, 4, 0, 0)}
        AddHandler add.Click,
            Sub()
                Dim invalid As New List(Of String)()
                Dim names = ResuService.ParseManualCharacterNames(username.Text, invalid)
                If names.Count = 0 Then
                    Dim detail = If(invalid.Count > 0, " Invalid: " & String.Join(", ", invalid.Take(5)), "")
                    MessageBox.Show(Me, "Enter at least one valid character name. Names may contain letters, numbers, underscores, or hyphens." & detail, "RESU blacklist")
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
                _resuStatus.Text = $"Added {added} character name(s) to the blacklist. {names.Count - added} already existed."
                If invalid.Count > 0 Then MessageBox.Show(Me, "These entries were not added because their names are invalid: " & String.Join(", ", invalid.Take(10)), "RESU blacklist")
            End Sub
        AddHandler remove.Click,
            Sub()
                If _resuBlacklist.SelectedRows.Count = 0 Then Return
                Dim name = CStr(_resuBlacklist.SelectedRows(0).Cells(0).Value)
                _resuSettings.Blacklist.RemoveAll(Function(entry) String.Equals(entry.Username, name, StringComparison.OrdinalIgnoreCase))
                SaveResuBlacklist()
                _resuStatus.Text = $"Removed {name} from the blacklist."
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

    Private Function ReadResuOptions() As ResuSettings
        Dim settings = JsonSerializer.Deserialize(Of ResuSettings)(JsonSerializer.Serialize(_resuSettings))
        settings.SelectKey = CStr(_resuSelectKey.SelectedItem)
        settings.SelectKeyIntervalMs = CInt(_resuSelectKeyIntervalMs.Value)
        settings.ResurrectKey = _resuCastKey.Text.Trim().ToUpperInvariant()
        settings.PeriodicMessageEnabled = _resuPeriodicMessageEnabled.Checked
        settings.PeriodicMessageText = _resuPeriodicMessageText.Text.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        settings.PeriodicMessageIntervalSeconds = CInt(_resuPeriodicMessageIntervalSeconds.Value)
        settings.ResurrectPressCount = CInt(_resuCastPressCount.Value)
        settings.ResurrectBurstSeconds = _resuCastBurstSeconds.Value
        settings.ScanMs = CInt(_resuScanMs.Value)
        settings.PaymentTimeoutSeconds = CInt(_resuTimeout.Value)
        settings.MinimumPayment = CLng(_resuMinimumPayment.Value)
        settings.InvitePattern = _resuPatterns("Invitation").Text
        settings.TradePattern = _resuPatterns("Trade window").Text
        settings.ResurrectedPattern = _resuPatterns("Resurrection confirmed").Text
        settings.PaidPattern = _resuPatterns("Payment received").Text
        settings.UnpaidPattern = _resuPatterns("Nonpayment").Text
        settings.TradeClosedPattern = _resuPatterns("Trade completed / cancelled").Text
        Return settings
    End Function

    Private Sub SaveResuOptions()
        Try
            Dim settings = ReadResuOptions()
            ValidateResuKeys(settings)
            Dim validation As New ResuService(settings)
            _resuSettings = settings
            SavePersistedListState(True)
            _resuStatus.Text = "RESU settings saved."
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "RESU settings")
        End Try
    End Sub

    Private Shared Sub ValidateResuKeys(settings As ResuSettings)
        If Not BotEngine.IsSupportedKeyName(settings.ResurrectKey) Then Throw New InvalidOperationException("Type a valid resurrection key, such as 3, F5, SPACE, ENTER, or a letter.")
        If String.Equals(settings.ResurrectKey, "F12", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("F12 is reserved for stopping RESU. Type a different resurrection key.")
        If String.Equals(settings.SelectKey, settings.ResurrectKey, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("Choose different target-selection and resurrection keys.")
    End Sub

    Private Sub ApplyPersistedResuState(settings As ResuSettings)
        _resuSettings = If(settings, New ResuSettings())
        If String.Equals(_resuSettings.InvitePattern, ResuService.LegacyDefaultInvitePattern, StringComparison.Ordinal) Then
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
        _resuSelectKey.SelectedItem = If(_resuSelectKey.Items.Contains(_resuSettings.SelectKey), _resuSettings.SelectKey, "TAB")
        _resuSelectKeyIntervalMs.Value = Math.Clamp(_resuSettings.SelectKeyIntervalMs, 50, 10000)
        _resuCastKey.Text = If(_resuSettings.ResurrectKey, "").Trim().ToUpperInvariant()
        _resuPeriodicMessageEnabled.Checked = _resuSettings.PeriodicMessageEnabled
        _resuPeriodicMessageText.Text = If(_resuSettings.PeriodicMessageText, "")
        _resuPeriodicMessageIntervalSeconds.Value = Math.Clamp(_resuSettings.PeriodicMessageIntervalSeconds, 1, 86400)
        _resuCastPressCount.Value = Math.Clamp(_resuSettings.ResurrectPressCount, 1, 100)
        _resuCastBurstSeconds.Value = Math.Clamp(_resuSettings.ResurrectBurstSeconds, 0D, 30D)
        _resuScanMs.Value = Math.Clamp(_resuSettings.ScanMs, 100, 5000)
        _resuTimeout.Value = Math.Clamp(_resuSettings.PaymentTimeoutSeconds, 10, 600)
        _resuMinimumPayment.Value = Math.Clamp(_resuSettings.MinimumPayment, 1L, 1000000000L)
        _resuPatterns("Invitation").Text = _resuSettings.InvitePattern
        _resuPatterns("Trade window").Text = _resuSettings.TradePattern
        _resuPatterns("Resurrection confirmed").Text = _resuSettings.ResurrectedPattern
        _resuPatterns("Payment received").Text = _resuSettings.PaidPattern
        _resuPatterns("Nonpayment").Text = _resuSettings.UnpaidPattern
        _resuPatterns("Trade completed / cancelled").Text = _resuSettings.TradeClosedPattern
        UpdateResuCalibrationLabel()
        RefreshResuBlacklist()
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
            MessageBox.Show(Me, ex.Message, "RESU calibration")
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
        If _resuRunning Then
            StopResu("RESU stopped.")
            Return
        End If
        Try
            If Not _quizUnlocked Then Return
            If GetRunningEdition().HasValue Then Throw New InvalidOperationException("Stop the main bot before starting RESU. The Quiz solver may remain enabled.")
            Dim settings = ReadResuOptions()
            ValidateResuCalibration(settings)
            ValidateResuKeys(settings)
            _resuWindow = ResuSelectedWindow()
            If _resuWindow = IntPtr.Zero OrElse NativeMethods.IsIconic(_resuWindow) Then Throw New InvalidOperationException("Select and restore the Full game window first.")
            _resuService = New ResuService(settings)
            _resuSettings = settings
            _resuGeneration += 1
            _resuRunning = True
            _resuOptions.Enabled = False
            _resuStart.Text = "Stop RESU"
            _resuNextScan = DateTime.MinValue
            _resuNextSelectKeyAt = DateTime.MinValue
            _resuNextPeriodicMessageAt = If(settings.PeriodicMessageEnabled, DateTime.UtcNow.AddSeconds(settings.PeriodicMessageIntervalSeconds), DateTime.MaxValue)
            _resuTradeVisible = False
            SavePersistedListState(False)
            _resuTimer.Start()
            UpdateMainTabIndicators()
            AppendLog("RESU started. F12 stops; RESU input is posted directly to the selected game window and does not require foreground focus.")
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "RESU")
        End Try
    End Sub

    Private Sub StopResu(reason As String)
        _resuRunning = False
        _resuGeneration += 1
        _resuTimer.Stop()
        _resuOptions.Enabled = True
        _resuStart.Text = "Start RESU"
        _resuStatus.Text = reason
        UpdateMainTabIndicators()
        AppendLog(reason)
    End Sub

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
            _resuStatus.Text = "OCR preview updated. Match the patterns to the exact game messages shown below."
        Catch ex As Exception
            If Not IsDisposed AndAlso Not Disposing Then _resuStatus.Text = ex.Message
        Finally
            _resuBusy = False
        End Try
    End Function

    Private Function CanResuAct(generation As Integer, hwnd As IntPtr) As Boolean
        If Not _resuRunning OrElse generation <> _resuGeneration OrElse IsDisposed OrElse Disposing Then Return False
        If ResuSelectedWindow() <> hwnd OrElse NativeMethods.IsIconic(hwnd) Then Return False
        If GetRunningEdition().HasValue Then Return False
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
        If ResuSelectedWindow() <> hwnd OrElse GetRunningEdition().HasValue Then
            StopResu("RESU stopped: the selected window changed or the main bot started.")
            Return
        End If
        If Not CanResuAct(generation, hwnd) Then
            _resuStatus.Text = "Paused: restore the selected game at the calibrated client size."
            ' Invalidate a worker even if the window is restored before its OCR finishes.
            _resuGeneration += 1
            _resuService.PauseMonitoring()
            Return
        End If
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
            If _resuService.PendingUsername.Length > 0 AndAlso String.IsNullOrWhiteSpace(observation.ChatText) AndAlso String.IsNullOrWhiteSpace(observation.MessageText) Then
                _resuService.PauseMonitoring()
                _resuStatus.Text = "Payment monitoring paused: chat and game-message OCR are empty. Check the calibrated regions."
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
                Dim allowed = Await Task.Run(Function() RevalidateResuAction(hwnd, settings, decision))
                If Not CanResuAct(generation, hwnd) Then Return
                If allowed AndAlso (decision.Username.Length = 0 OrElse Not _resuService.IsBlocked(decision.Username)) Then
                    Dim sent As Boolean
                    Select Case decision.Action
                        Case ResuAction.SelectTarget
                            If DateTime.UtcNow >= _resuNextSelectKeyAt Then
                                sent = BotEngine.SendKey(hwnd, settings.SelectKey, 30, forceBackgroundPost:=True)
                                If sent Then _resuNextSelectKeyAt = DateTime.UtcNow.AddMilliseconds(settings.SelectKeyIntervalMs)
                            Else
                                sent = False
                                _resuStatus.Text = $"Waiting for the {settings.SelectKeyIntervalMs:N0} ms target-key interval."
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
            _resuStatus.Text = _resuService.Status
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
                _resuNextScan = DateTime.UtcNow.AddMilliseconds(_resuSettings.ScanMs)
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
                _resuStatus.Text = $"Periodic message sent. Next message in {settings.PeriodicMessageIntervalSeconds:N0} second(s)."
                AppendLog("RESU periodic message sent: " & settings.PeriodicMessageText)
            Else
                _resuStatus.Text = "Periodic message could not be sent; RESU will retry at the next interval."
                AppendLog("RESU periodic message failed to send.")
            End If
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
            _resuStatus.Text = $"Sending resurrection key for {decision.Username}: {sent}/{settings.ResurrectPressCount} press(es)."
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
        _resuRunning = False
        _resuGeneration += 1
        _resuTimer.Stop()
        _resuTimer.Dispose()
        If _resuOverlay IsNot Nothing Then _resuOverlay.Close()
    End Sub
End Class
