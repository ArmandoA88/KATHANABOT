Partial Public Class Form1
    ' Preserve this preference for older background EXEs without enabling it here.
    Private _legacyBackgroundKeyboardMethod As String = "posted-scan"

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
            AppendLog(name & " needs mouse or text input" & If(WindowsInput.FocusBorrowEnabled,
                      " and is unavailable while Background mode is on. Turn Background mode off to use it.",
                      " and is unavailable in this background keyboard build."))
            Return True
        End If
        If Not WindowsInput.BackgroundOnly Then Return False
        SilentMessageBox.Show(Me, name & " needs foreground control and is disabled by Background Only. Turn Background Only off to use it.", "Background Only")
        Return True
    End Function
End Class
