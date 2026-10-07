Partial Public Class Form1
    Private _launchAutoStart As New LaunchAutoStartSchedule()
    Private _lastLaunchAutoStartReason As String = ""
    Private _launchGameSelectionPending As Boolean
    Private _nextLaunchGameSelectionTick As Long

    Private Function IsLaunchAutoStartPending() As Boolean
        Return _launchAutoStart IsNot Nothing AndAlso _launchAutoStart.Pending
    End Function

    Private Function GetLaunchAutoStartStatus() As String
        Return If(_launchAutoStart?.WaitingReason, "Starting the Full bot. Stop or F12 cancels automatic startup.")
    End Function

    Private Sub AutoStartOnLaunch()
        If _launchAutoStart Is Nothing Then _launchAutoStart = New LaunchAutoStartSchedule()
        _launchAutoStart.Arm(Environment.GetCommandLineArgs().Contains("--no-auto-start"))
        TryLaunchAutoStart()
    End Sub

    Private Sub CancelLaunchAutoStart()
        _launchAutoStart?.Cancel()
        _launchGameSelectionPending = False
        ResetGameFocusMaintenance()
    End Sub

    Private Function GetLaunchAutoStartWaitReason() As String
        If _extrasBusy AndAlso _extrasApplyingZoom Then Return "Waiting for the zoom config operation to finish. Stop or F12 cancels automatic startup."
        If _workflowModes.Current <> OperatingMode.Idle Then Return "Waiting for " & _workflowModes.Current.ToString() & " to finish. Stop or F12 cancels automatic startup."
        If _tradeRunning Then Return "Waiting for Trade to finish. Stop or F12 cancels automatic startup."

        Return ""
    End Function

    Private Sub TryLaunchAutoStart()
        If IsDisposed OrElse Disposing Then Return
        If Not IsLaunchAutoStartPending() Then
            TryResolveLaunchGameSelection()
            Return
        End If
        Dim nowTick = Environment.TickCount64
        If Not _launchAutoStart.TryBeginPoll(nowTick) Then Return
        Dim started As Boolean = False
        Dim waitingReason As String = ""
        Try
            If _fullEngine.IsRunning() OrElse _liteEngine.IsRunning() Then
                started = True
            Else
                ' Resolve an existing game before starting the worker. With no game, the worker
                ' runs its waiting loop; the input backend still requires the selected game focus.
                ' Refresh only while startup is pending; a stopped bot is never restarted here.
                RefreshProcessWindowList(False, IntPtr.Zero)
                SelectKathanaForLaunchIfNeeded()
                waitingReason = GetLaunchAutoStartWaitReason()
                If waitingReason = "" Then
                    StartEdition(BotEdition.Full, True)
                    started = _fullEngine.IsRunning()
                    If Not started Then waitingReason = "Waiting to start the Full bot. Another operation temporarily blocked startup. Stop or F12 cancels."
                End If
            End If
        Catch ex As Exception
            waitingReason = "Automatic startup is waiting: " & ex.Message & " Stop or F12 cancels."
        Finally
            _launchAutoStart.CompletePoll(nowTick, started, waitingReason)
        End Try

        If started Then
            _launchGameSelectionPending = Not IsPreferredKathanaWindow(GetSelectedProcessWindowForEdition(BotEdition.Full))
            _nextLaunchGameSelectionTick = nowTick + 2000
        End If

        If Not started AndAlso IsLaunchAutoStartPending() AndAlso waitingReason <> _lastLaunchAutoStartReason Then
            _lastLaunchAutoStartReason = waitingReason
            AppendLog(waitingReason)
        End If
        RefreshDashboardFromEngine()
        UpdateAttackButtonAppearance(False)
    End Sub

    Private Sub TryResolveLaunchGameSelection()
        If Not _launchGameSelectionPending Then Return
        If Not _fullEngine.IsRunning() Then
            _launchGameSelectionPending = False
            Return
        End If
        If Environment.TickCount64 < _nextLaunchGameSelectionTick Then Return
        _nextLaunchGameSelectionTick = Environment.TickCount64 + 2000
        RefreshProcessWindowList(False, IntPtr.Zero)
        SelectKathanaForLaunchIfNeeded()
        _launchGameSelectionPending = Not IsPreferredKathanaWindow(GetSelectedProcessWindowForEdition(BotEdition.Full))
    End Sub

    Private Sub SelectKathanaForLaunchIfNeeded()
        If IsPreferredKathanaWindow(GetSelectedProcessWindowForEdition(BotEdition.Full)) Then Return
        For Each processList As ListBox In {lstProcessWindows, lstLiteProcessWindows}
            If processList Is Nothing OrElse processList.IsDisposed Then Continue For
            For Each item In processList.Items
                Dim entry = TryCast(item, ProcessWindowEntry)
                If IsPreferredKathanaWindow(entry) AndAlso entry.MainWindowHandle <> IntPtr.Zero Then
                    SyncProcessSelectionAcrossLists(entry.MainWindowHandle)
                    Return
                End If
            Next
        Next
    End Sub
End Class
