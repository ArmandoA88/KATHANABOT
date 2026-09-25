Partial Public Class Form1
    Private _backgroundOnlyButton As Button

    Private Function BuildBackgroundOnlyButton() As Control
        _backgroundOnlyButton = CreateResponsiveCenterButton("Foreground SendInput only", Color.FromArgb(35, 100, 145))
        _backgroundOnlyButton.Enabled = False
        Return _backgroundOnlyButton
    End Function

    Private Sub UpdateBackgroundOnlyButton()
        If _backgroundOnlyButton Is Nothing Then Return
        _backgroundOnlyButton.Text = "Foreground SendInput only"
        _backgroundOnlyButton.BackColor = Color.FromArgb(35, 100, 145)
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
            If _tradeRunning Then
                disabled.Add("Trade whisper queue")
                _tradeCancellation?.Cancel()
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
            SilentMessageBox.Show(Me, "Background Only is ON. The bot will not take focus or move your mouse. Ordinary combat, retarget and F pickup use background key messages." & Environment.NewLine & Environment.NewLine & changes & Environment.NewLine & Environment.NewLine & "Movement, Ctrl/Alt skill chords, foreground clicks, Quiz, RESU and Trade are unavailable in this mode. Keep the game open behind your other windows; minimized games may stop rendering.", "Background Only")
        End If
    End Sub

    Private Function RejectForegroundWorkflow(name As String) As Boolean
        If Not WindowsInput.BackgroundOnly Then Return False
        SilentMessageBox.Show(Me, name & " needs foreground control and is disabled by Background Only. Turn Background Only off to use it.", "Background Only")
        Return True
    End Function
End Class
