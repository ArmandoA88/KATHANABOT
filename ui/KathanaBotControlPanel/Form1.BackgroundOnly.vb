Partial Public Class Form1
    Private _backgroundOnlyButton As Button
    Private _backgroundKeyboardMenu As ContextMenuStrip

    Private Function BuildBackgroundOnlyButton() As Control
        _backgroundOnlyButton = CreateResponsiveCenterButton("Background keys", Color.FromArgb(35, 100, 145))
        _backgroundKeyboardMenu = New ContextMenuStrip()
        AddHandler Disposed, Sub() _backgroundKeyboardMenu.Dispose()
        For Each mode In {BackgroundKeyboardMode.PostedScanCode, BackgroundKeyboardMode.SynchronousScanCode,
                          BackgroundKeyboardMode.PostedZeroScanCode, BackgroundKeyboardMode.SynchronousZeroScanCode}
            Dim selectedMode = mode
            Dim item As New ToolStripMenuItem(BackgroundKeyModeLabel(mode)) With {.Tag = mode}
            AddHandler item.Click, Sub() SelectBackgroundKeyMode(selectedMode)
            _backgroundKeyboardMenu.Items.Add(item)
        Next
        _backgroundOnlyButton.ContextMenuStrip = _backgroundKeyboardMenu
        AddHandler _backgroundOnlyButton.Click, Sub() _backgroundKeyboardMenu.Show(_backgroundOnlyButton, New System.Drawing.Point(0, _backgroundOnlyButton.Height))
        UpdateBackgroundOnlyButton()
        Return _backgroundOnlyButton
    End Function

    Private Sub UpdateBackgroundOnlyButton()
        If _backgroundOnlyButton Is Nothing Then Return
        _backgroundOnlyButton.Text = "Background keys: " & BackgroundKeyModeLabel(WindowsInput.BackgroundKeyMode)
        _backgroundOnlyButton.BackColor = Color.FromArgb(35, 100, 145)
        If _backgroundKeyboardMenu IsNot Nothing Then
            For Each item As ToolStripMenuItem In _backgroundKeyboardMenu.Items
                item.Checked = DirectCast(item.Tag, BackgroundKeyboardMode) = WindowsInput.BackgroundKeyMode
            Next
        End If
    End Sub

    Private Shared Function BackgroundKeyModeLabel(mode As BackgroundKeyboardMode) As String
        Select Case mode
            Case BackgroundKeyboardMode.PostedScanCode : Return "Queued / scan"
            Case BackgroundKeyboardMode.SynchronousScanCode : Return "Direct / scan"
            Case BackgroundKeyboardMode.PostedZeroScanCode : Return "Queued / virtual"
            Case BackgroundKeyboardMode.SynchronousZeroScanCode : Return "Direct / virtual"
            Case Else : Return "Unknown"
        End Select
    End Function

    Private Sub SelectBackgroundKeyMode(mode As BackgroundKeyboardMode)
        If _fullEngine.IsRunning() OrElse _liteEngine.IsRunning() OrElse _resuRunning OrElse _tradeRunning OrElse
            _workflowModes.Current <> OperatingMode.Idle Then
            AppendLog("Stop automation before changing the background key method.")
            Return
        End If
        If Not WindowsInput.TrySetBackgroundKeyMode(mode) Then
            AppendLog("Background key method unchanged: a key release is still pending.")
            Return
        End If
        UpdateBackgroundOnlyButton()
        SavePersistedListState(False)
        AppendLog("Background key method: " & BackgroundKeyModeLabel(mode) & ". Keep the game open and not minimized; other apps can stay active.")
    End Sub

    Private Sub EnforceBackgroundOnly(showReport As Boolean)
        If Not WindowsInput.BackgroundOnly OrElse _applyingSettings Then Return
        Dim disabled As New List(Of String)
        _applyingSettings = True
        Try
            Dim cfg = BuildConfig()
            cfg.DirectKpIntervalMs = CInt(If(_directKpInterval Is Nothing, 1000D, _directKpInterval.Value))
            disabled.AddRange(BackgroundModePolicy.Apply(cfg))
            If disabled.Count > 0 Then ApplySavedConfigToUi(cfg)
            If chkAutoRelaunchGame IsNot Nothing AndAlso chkAutoRelaunchGame.Checked Then
                disabled.Add("Automatic game relaunch")
                chkAutoRelaunchGame.Checked = False
            End If
            If _holdToShowGameWindowEnabled Then
                disabled.Add("Hold to show game window")
                _holdToShowGameWindowEnabled = False
                UpdateHoldToShowGameWindowUi()
            End If
            If chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked Then
                disabled.Add("Quiz solver clicks")
                chkQuizSolverEnabled.Checked = False
            End If
            _quizCancellation?.Cancel()
            If _resuRunning Then
                disabled.Add("RESU automation")
                StopResu("Background-only mode")
            End If
            For Each pair In _liteActionEnabledChecks
                If pair.Value.Checked AndAlso BackgroundModePolicy.RequiresForeground(pair.Key) Then
                    pair.Value.Checked = False
                    disabled.Add("Lite skill " & pair.Key)
                End If
            Next
        Finally
            _applyingSettings = False
        End Try
        If disabled.Count > 0 Then AppendLog("Background Only disabled: " & String.Join(", ", disabled.Distinct()))
        If showReport Then
            Dim changes = If(disabled.Count = 0, "No currently enabled features needed to be disabled.", "Disabled:" & Environment.NewLine & String.Join(Environment.NewLine, disabled.Distinct().Select(Function(name) "• " & name)))
            SilentMessageBox.Show(Me, "Background combat uses key messages without taking focus or moving your mouse." & Environment.NewLine & Environment.NewLine & changes & Environment.NewLine & Environment.NewLine & "Movement, Ctrl/Alt skill chords, foreground clicks, Quiz and RESU are unavailable in this mode. Keep the game open behind your other windows; minimized games may stop rendering. Start whispers (foreground) separately pauses combat and focuses the selected game while sending reviewed messages.", "Background Only")
        End If
    End Sub

    Private Function RejectForegroundWorkflow(name As String) As Boolean
        If WindowsInput.KeyboardOnlyMode Then
            AppendLog(name & " needs mouse or text input and is unavailable in this background keyboard build.")
            Return True
        End If
        If Not WindowsInput.BackgroundOnly Then Return False
        SilentMessageBox.Show(Me, name & " needs foreground control and is disabled by Background Only. Turn Background Only off to use it.", "Background Only")
        Return True
    End Function
End Class
