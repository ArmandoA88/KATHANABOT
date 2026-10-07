Partial Public Class Form1
    Private _gameFocusSchedule As New GameFocusMaintenanceSchedule()
    Private _gameFocusClosing As Boolean
    Private _gameFocusF12WasDown As Boolean

    Private Sub ResetGameFocusMaintenance()
        _gameFocusSchedule?.Reset()
    End Sub

    Private Sub ShutdownGameFocusMaintenance()
        _gameFocusClosing = True
        ResetGameFocusMaintenance()
    End Sub

    Private Sub CancelActiveGameplayOnUserStop()
        _tradeCancellation?.Cancel()
        _quizCancellation?.Cancel()
        If chkQuizSolverEnabled IsNot Nothing Then chkQuizSolverEnabled.Checked = False
        If _resuRunning Then StopResu("RESU stopped by the user.")
    End Sub

    Private Function HasActiveGameplayForFocus() As Boolean
        Return (_fullEngine IsNot Nothing AndAlso _fullEngine.IsRunning()) OrElse
            (_liteEngine IsNot Nothing AndAlso _liteEngine.IsRunning()) OrElse
            _resuRunning OrElse
            (_quizSolveInProgress AndAlso _quizCancellation IsNot Nothing AndAlso Not _quizCancellation.IsCancellationRequested) OrElse
            (_tradeRunning AndAlso _tradeCancellation IsNot Nothing AndAlso Not _tradeCancellation.IsCancellationRequested)
    End Function

    Private Function GameplayFocusInteractionSuspended() As Boolean
        If Not Enabled OrElse (_extrasBusy AndAlso _extrasApplyingZoom) Then Return True
        For Each opened As Form In Application.OpenForms
            If opened IsNot Nothing AndAlso Not opened.IsDisposed AndAlso opened.Visible AndAlso opened.Modal Then Return True
        Next
        Return False
    End Function

    ' Shared by the UI supervisor and Trade's safe between-message wait. Native
    ' restoration checks the captured HWND/PID again and dispatches no game input.
    Private Function TryMaintainGameplayForeground(hwnd As IntPtr, expectedPid As UInteger) As Boolean
        If _gameFocusSchedule Is Nothing Then _gameFocusSchedule = New GameFocusMaintenanceSchedule()
        Dim running = HasActiveGameplayForFocus()
        Dim closing = _gameFocusClosing OrElse IsDisposed OrElse Disposing OrElse _apiClosing
        Dim foreground = NativeMethods.GetForegroundWindow()
        Dim actualPid As UInteger
        Dim targetForeground = hwnd <> IntPtr.Zero AndAlso foreground = hwnd AndAlso
            NativeMethods.GetWindowThreadProcessId(hwnd, actualPid) <> 0 AndAlso actualPid = expectedPid
        Dim foregroundPid As UInteger
        Dim controlPanelForeground = foreground <> IntPtr.Zero AndAlso
            NativeMethods.GetWindowThreadProcessId(foreground, foregroundPid) <> 0 AndAlso foregroundPid = CUInt(Environment.ProcessId)
        If Not _gameFocusSchedule.TryBeginAttempt(Environment.TickCount64, running, closing, hwnd, expectedPid,
                                                  targetForeground, GameplayFocusInteractionSuspended(), controlPanelForeground) Then
            Return running AndAlso Not closing AndAlso targetForeground
        End If
        Return WindowsInput.TryRestoreGameForeground(hwnd, expectedPid)
    End Function

    Private Sub MaintainSelectedGameForeground()
        If _gameFocusClosing OrElse IsDisposed OrElse Disposing OrElse Not HasActiveGameplayForFocus() Then
            ResetGameFocusMaintenance()
            Return
        End If
        ' Background mode borrows the keyboard for each key press instead of pinning the game in front.
        ' Trade is the exception: it types chat text, so it still asks for the game to stay focused.
        If WindowsInput.FocusBorrowEnabled AndAlso Not _tradeRunning Then
            ResetGameFocusMaintenance()
            Return
        End If
        If _tradeRunning Then
            If _tradeForegroundMaintenanceAllowed AndAlso _tradeCancellation IsNot Nothing AndAlso Not _tradeCancellation.IsCancellationRequested Then
                TryMaintainGameplayForeground(_tradeForegroundMaintenanceWindow, _tradeForegroundMaintenancePid)
            End If
            Return
        End If

        Dim edition = GetRunningEdition().GetValueOrDefault(BotEdition.Full)
        Dim selected = GetSelectedProcessWindowForEdition(edition)
        If Not IsPreferredKathanaWindow(selected) OrElse selected.MainWindowHandle = IntPtr.Zero OrElse selected.ProcessId <= 0 Then
            ResetGameFocusMaintenance()
            Return
        End If
        If _resuRunning AndAlso selected.MainWindowHandle <> _resuWindow Then Return
        TryMaintainGameplayForeground(selected.MainWindowHandle, CUInt(selected.ProcessId))
    End Sub

    Private Function HandleGameplayF12Stop() As Boolean
        Dim down = (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0
        Dim justPressed = down AndAlso Not _gameFocusF12WasDown
        _gameFocusF12WasDown = down
        If Not justPressed Then Return False
        Dim active = HasActiveGameplayForFocus() OrElse IsLaunchAutoStartPending() OrElse _tradeAnalyzing OrElse
            _tradeCaptureServer IsNot Nothing OrElse (chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked)
        If Not active Then Return False

        CancelLaunchAutoStart()
        _tradeCancellation?.Cancel()
        _tradeAnalysisCancellation?.Cancel()
        _quizCancellation?.Cancel()
        If chkQuizSolverEnabled IsNot Nothing Then chkQuizSolverEnabled.Checked = False
        If _resuRunning Then StopResu("RESU stopped with F12.")
        If _tradeCaptureServer IsNot Nothing Then StopTradeBrowserCapture()
        StopTradeDiscordImport()
        If _tradePriceScan IsNot Nothing Then _tradePriceScan.Checked = False
        If _fullEngine.IsRunning() Then StopEdition(BotEdition.Full, True, "F12")
        If _liteEngine.IsRunning() Then StopEdition(BotEdition.Lite, True, "F12")
        WindowsInput.ReleaseAll()
        RefreshDashboardFromEngine()
        UpdateAttackButtonAppearance(False)
        AppendLog("F12 stopped active gameplay and cancelled automatic startup.")
        Return True
    End Function
End Class
