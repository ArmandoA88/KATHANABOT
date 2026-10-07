' Background mode: the game window may stay behind other windows while the bot runs. The input
' backend borrows keyboard focus for each key press and hands it back (see FocusBorrow.vb), using the
' same foreground SendInput path as before. It is keyboard-only: nothing moves your mouse or clicks.
Partial Public Class Form1
    Private _backgroundModeEnabled As Boolean = True
    Private _backgroundYieldMs As Integer = FocusBorrowSettings.DefaultYieldMs
    Private _backgroundPauseFullScreen As Boolean = True
    Private _backgroundModeSyncing As Boolean
    Private _nextBackgroundStatusTick As Long
    Private btnBackgroundMode As Button
    Private nudBackgroundYield As NumericUpDown
    Private chkBackgroundFullScreenPause As CheckBox
    Private lblBackgroundModeStatus As Label

    Private Const BackgroundModeHelp As String =
        "Background mode lets the game stay behind your other windows (keep it open, not minimized). " &
        "Windows only delivers key presses to the active window, so the bot takes the keyboard for the " &
        "moment a key is pressed, keeps the game window where it was in the window stack, and gives focus " &
        "back to what you were using. It never moves your mouse or clicks, so mouse features are paused " &
        "while it is on. Full-screen apps and presentations are not interrupted, and the bot waits for you " &
        "to stop typing first. Turn it off to keep the game in front instead."

    Private Function BuildBackgroundModeControls() As Control
        Dim panel As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 3, .RowCount = 3, .Margin = New Padding(0)}
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 60))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 74))
        btnBackgroundMode = New Button With {.Dock = DockStyle.Fill, .MinimumSize = New Size(0, 38), .Margin = New Padding(3, 3, 3, 3),
                                             .ForeColor = Color.White, .UseVisualStyleBackColor = False}
        AddHandler btnBackgroundMode.Click, AddressOf ToggleBackgroundModeClicked
        nudBackgroundYield = New NumericUpDown With {.Minimum = 0D, .Maximum = 30D, .DecimalPlaces = 1, .Increment = 0.1D, .Value = FocusBorrowSettings.DefaultYieldMs / 1000D,
                                                     .Dock = DockStyle.Fill, .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
        AddHandler nudBackgroundYield.ValueChanged, AddressOf BackgroundModeSettingChanged
        ' Fixed heights, wrapping and ellipsis: a long "last reason" note must never widen the panel.
        chkBackgroundFullScreenPause = New CheckBox With {.Text = "Don't interrupt full-screen apps or presentations", .Dock = DockStyle.Fill,
                                                          .AutoSize = False, .AutoEllipsis = True, .Checked = True,
                                                          .Margin = New Padding(6, 0, 3, 0), .MinimumSize = New Size(0, 26), .Height = 26}
        AddHandler chkBackgroundFullScreenPause.CheckedChanged, AddressOf BackgroundModeSettingChanged
        lblBackgroundModeStatus = New Label With {.Dock = DockStyle.Fill, .AutoSize = False, .AutoEllipsis = True, .ForeColor = Color.LightSteelBlue,
                                                  .Margin = New Padding(6, 0, 3, 2), .MinimumSize = New Size(0, 40), .Height = 40}
        panel.Controls.Add(btnBackgroundMode, 0, 0)
        panel.Controls.Add(nudBackgroundYield, 1, 0)
        panel.Controls.Add(New Label With {.Text = "wait (s)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 0)
        panel.Controls.Add(chkBackgroundFullScreenPause, 0, 1)
        panel.SetColumnSpan(chkBackgroundFullScreenPause, 3)
        panel.Controls.Add(lblBackgroundModeStatus, 0, 2)
        panel.SetColumnSpan(lblBackgroundModeStatus, 3)
        Dim tips As New ToolTip With {.AutoPopDelay = 30000, .InitialDelay = 300}
        tips.SetToolTip(btnBackgroundMode, BackgroundModeHelp)
        tips.SetToolTip(nudBackgroundYield, "Seconds without typing or clicking before the bot may take the keyboard (default 1). A longer wait makes it less likely that the first key you press lands in the game as the bot borrows the keyboard. 0 never waits, which gives the bot priority over your typing.")
        tips.SetToolTip(chkBackgroundFullScreenPause, "While a full-screen app, game or presentation is in front, the bot pauses its key presses instead of taking the keyboard.")
        AddHandler panel.Disposed, Sub() tips.Dispose()
        SyncBackgroundModeControls()
        UpdateBackgroundModeControls()
        Return panel
    End Function

    ' Applies the saved state to the input backend. Called once after settings load, and on every change.
    Private Sub InitializeBackgroundMode()
        Dim args = Environment.GetCommandLineArgs()
        If args.Contains("--foreground-mode") Then _backgroundModeEnabled = False
        If args.Contains("--background-mode") Then _backgroundModeEnabled = True
        WindowsInput.ConfigureFocusBorrow(_backgroundModeEnabled, _backgroundYieldMs, _backgroundPauseFullScreen)
        SyncBackgroundModeControls()
        UpdateBackgroundModeControls()
    End Sub

    Private Sub LoadBackgroundModeState(appState As PersistedAppState)
        _backgroundModeEnabled = appState Is Nothing OrElse appState.BackgroundFocusModeEnabled
        _backgroundYieldMs = If(appState Is Nothing, FocusBorrowSettings.DefaultYieldMs,
                                Math.Clamp(appState.BackgroundYieldMs, 0, FocusBorrowSettings.MaximumYieldMs))
        _backgroundPauseFullScreen = appState Is Nothing OrElse appState.BackgroundPauseForFullScreen
        InitializeBackgroundMode()
    End Sub

    Private Sub SyncBackgroundModeControls()
        If nudBackgroundYield Is Nothing Then Return
        _backgroundModeSyncing = True
        Try
            nudBackgroundYield.Value = Math.Max(nudBackgroundYield.Minimum, Math.Min(nudBackgroundYield.Maximum, _backgroundYieldMs / 1000D))
            If chkBackgroundFullScreenPause IsNot Nothing Then chkBackgroundFullScreenPause.Checked = _backgroundPauseFullScreen
        Finally
            _backgroundModeSyncing = False
        End Try
    End Sub

    Private Sub UpdateBackgroundModeControls()
        If btnBackgroundMode Is Nothing Then Return
        btnBackgroundMode.Text = If(_backgroundModeEnabled, "Background mode: ON", "Background mode: OFF")
        btnBackgroundMode.BackColor = If(_backgroundModeEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        If nudBackgroundYield IsNot Nothing Then nudBackgroundYield.Enabled = _backgroundModeEnabled
        If chkBackgroundFullScreenPause IsNot Nothing Then chkBackgroundFullScreenPause.Enabled = _backgroundModeEnabled
        _nextBackgroundStatusTick = 0
        RefreshBackgroundModeStatus()
    End Sub

    Private Sub ToggleBackgroundModeClicked(sender As Object, e As EventArgs)
        _backgroundModeEnabled = Not _backgroundModeEnabled
        ApplyBackgroundMode(True)
    End Sub

    Private Sub BackgroundModeSettingChanged(sender As Object, e As EventArgs)
        If _backgroundModeSyncing OrElse _applyingSettings Then Return
        _backgroundYieldMs = CInt(Math.Round(nudBackgroundYield.Value * 1000D))
        _backgroundPauseFullScreen = chkBackgroundFullScreenPause.Checked
        ApplyBackgroundMode(False)
    End Sub

    Private Sub ApplyBackgroundMode(announce As Boolean)
        WindowsInput.ConfigureFocusBorrow(_backgroundModeEnabled, _backgroundYieldMs, _backgroundPauseFullScreen)
        UpdateBackgroundModeControls()
        If _applyingSettings Then Return
        ' Re-applies the keyboard-only feature policy to a running bot (and lifts it when turned off).
        PushLiveConfig()
        SavePersistedListState(False)
        If Not announce Then Return
        If _backgroundModeEnabled Then
            Dim paused As New List(Of String)
            Try
                paused.AddRange(BackgroundModePolicy.ApplyKeyboardOnly(BuildFullConfigCore(False)).Select(AddressOf BackgroundModePolicy.DescribeFeature))
            Catch ex As Exception
                RuntimeJournal.Record("Background mode", "Could not list the paused mouse features: " & ex.Message)
            End Try
            AppendLog("Background mode ON: the game can stay behind other windows; the bot borrows the keyboard for each key press and hands it back." &
                      If(paused.Count > 0, " Mouse features paused: " & String.Join(", ", paused) & ".", ""))
        Else
            AppendLog("Background mode OFF: the bot keeps the game in front and refocuses it when needed.")
        End If
    End Sub

    ' Shown under the toggle; updated about once a second from the fast UI timer.
    Private Sub RefreshBackgroundModeStatus()
        If lblBackgroundModeStatus Is Nothing OrElse lblBackgroundModeStatus.IsDisposed Then Return
        Dim nowTick = Environment.TickCount64
        If nowTick < _nextBackgroundStatusTick Then Return
        _nextBackgroundStatusTick = nowTick + 1000
        If Not WindowsInput.FocusBorrowEnabled Then
            lblBackgroundModeStatus.Text = "Off: the bot keeps the game in front and refocuses it."
            lblBackgroundModeStatus.ForeColor = Color.LightSteelBlue
            Return
        End If
        Dim status = WindowsInput.FocusBorrowStatus()
        Dim line = $"Borrowed {status.Borrowed}x | waited for you {status.WaitedForUser}x | paused {status.Paused}x | refused {status.Refused}x"
        If status.LastNote <> "" Then line &= Environment.NewLine & status.LastNote
        lblBackgroundModeStatus.Text = line
        lblBackgroundModeStatus.ForeColor = If(status.Refused > 0 AndAlso status.Borrowed = 0, Color.Gold, Color.LightSteelBlue)
    End Sub
End Class
