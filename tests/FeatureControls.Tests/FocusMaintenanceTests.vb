Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Threading

Friend Module FocusMaintenanceTests
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private ReadOnly ScheduleType As Type = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.GameFocusMaintenanceSchedule", throwOnError:=True)
    Private checks As Integer

    Public Sub Run()
        checks = 0
        TestRecoveryAndRetryPacing()
        TestMainGraceAndModalSuspension()
        TestTargetChangesStopAndClosing()
        TestFormLifecycleAndCancelledWorkflows()
        TestBackgroundModeDoesNotPinTheGame()
        TestBackgroundModeSettingsPersistence()
        Console.WriteLine($"PASS: {checks} foreground-maintenance assertions: continued focus recovery, 750-ms retry pacing, 3-second panel grace, modal deferral, selected-target changes, stop/closing, cancelled workflow guards, and Background-mode supervisor/persistence behavior (pure state only; no native focus/input calls).")
    End Sub

    ' Background mode borrows the keyboard per key press, so the supervisor that pins the game in front
    ' (and yanks focus back from the user every 750 ms) must stand down while it is on.
    Private Sub TestBackgroundModeDoesNotPinTheGame()
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        SetField(owner, "_resuRunning", True) ' any running workflow authorizes focus maintenance
        Dim schedule = NewSchedule()
        Attempt(schedule, 0)
        Check(Waiting(schedule), "fixture starts with a pending focus recovery")
        SetField(owner, "_gameFocusSchedule", schedule)
        WindowsInput.ConfigureFocusBorrow(True, FocusBorrowSettings.DefaultYieldMs)
        Try
            Check(WindowsInput.FocusBorrowEnabled, "the production input reports Background mode")
            InvokeForm(owner, "MaintainSelectedGameForeground")
            Check(Not Waiting(schedule), "Background mode must clear pending focus recovery instead of pulling the game forward")
            Attempt(schedule, 5000)
            InvokeForm(owner, "MaintainSelectedGameForeground")
            Check(Not Waiting(schedule), "and keep doing so on every supervisor tick")
        Finally
            WindowsInput.ConfigureFocusBorrow(False, 0)
            SetField(owner, "_resuRunning", False)
        End Try
        Check(Not WindowsInput.FocusBorrowEnabled AndAlso Not WindowsInput.KeyboardOnlyMode, "the test leaves the production input in foreground mode")
    End Sub

    ' Settings files from earlier versions have none of the Background-mode fields.
    Private Sub TestBackgroundModeSettingsPersistence()
        Dim stateType = GetType(Form1).GetNestedType("PersistedAppState", BindingFlags.NonPublic)
        Check(stateType IsNot Nothing, "persisted app state type is present")
        Dim fresh = Activator.CreateInstance(stateType, nonPublic:=True)
        Check(CBool(stateType.GetProperty("BackgroundFocusModeEnabled").GetValue(fresh)), "Background mode defaults to on for new settings")
        Check(CInt(stateType.GetProperty("BackgroundYieldMs").GetValue(fresh)) = FocusBorrowSettings.DefaultYieldMs, "the wait before borrowing defaults to 1 s")
        Check(FocusBorrowSettings.DefaultYieldMs = 1000, "the documented default wait is one second")
        Check(CBool(stateType.GetProperty("BackgroundPauseForFullScreen").GetValue(fresh)), "full-screen apps are not interrupted by default")
        Dim older = Text.Json.JsonSerializer.Deserialize("{""ActiveProfileName"":""x"",""BackgroundOnlyEnabled"":false}", stateType)
        Check(CBool(stateType.GetProperty("BackgroundFocusModeEnabled").GetValue(older)) AndAlso
              CInt(stateType.GetProperty("BackgroundYieldMs").GetValue(older)) = FocusBorrowSettings.DefaultYieldMs,
              "a settings file written by an earlier version loads with Background mode defaults")
        Dim saved = Text.Json.JsonSerializer.Deserialize("{""BackgroundFocusModeEnabled"":false,""BackgroundYieldMs"":2500,""BackgroundPauseForFullScreen"":false}", stateType)
        Check(Not CBool(stateType.GetProperty("BackgroundFocusModeEnabled").GetValue(saved)) AndAlso
              CInt(stateType.GetProperty("BackgroundYieldMs").GetValue(saved)) = 2500 AndAlso
              Not CBool(stateType.GetProperty("BackgroundPauseForFullScreen").GetValue(saved)),
              "saved Background-mode choices round-trip")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Focus maintenance test: " & reason)
    End Sub

    Private Function NewSchedule() As Object
        Return Activator.CreateInstance(ScheduleType, nonPublic:=True)
    End Function

    Private Function Attempt(owner As Object, tick As Long, Optional running As Boolean = True,
                             Optional closing As Boolean = False, Optional hwnd As Long = 101,
                             Optional pid As UInteger = 201, Optional focused As Boolean = False,
                             Optional suspended As Boolean = False, Optional panel As Boolean = False) As Boolean
        Return CBool(ScheduleType.GetMethod("TryBeginAttempt").Invoke(owner,
            {tick, running, closing, New IntPtr(hwnd), pid, focused, suspended, panel}))
    End Function

    Private Function Waiting(owner As Object) As Boolean
        Return CBool(ScheduleType.GetProperty("WaitingForForeground").GetValue(owner))
    End Function

    Private Sub Reset(owner As Object)
        ScheduleType.GetMethod("Reset").Invoke(owner, Nothing)
    End Sub

    Private Sub TestRecoveryAndRetryPacing()
        Dim state = NewSchedule()
        Check(Not Attempt(state, 0, focused:=True) AndAlso Not Waiting(state), "an already focused game must not trigger activation")
        Check(Attempt(state, 1) AndAlso Waiting(state), "focus loss must request recovery without changing engine running state")
        For Each tick In {1L, 45L, 100L, 750L}
            Check(Not Attempt(state, tick), "failed restoration must retry at bounded intervals, not every timer event")
        Next
        Check(Attempt(state, 751) AndAlso Waiting(state), "a denied native focus request must remain eligible for retry")
        Check(Attempt(state, 1501) AndAlso Waiting(state), "continued focus denial must keep the running workflow pending rather than stop it")
        Check(Not Attempt(state, 1550, focused:=True) AndAlso Not Waiting(state), "verified returned focus must clear the pending state")
        Check(Not Attempt(state, 1600), "another rapid focus change must preserve the retry limit")
        Check(Attempt(state, 2251), "focus lost again after recovery must start another restoration attempt")
    End Sub

    Private Sub TestMainGraceAndModalSuspension()
        Dim state = NewSchedule()
        Check(Not Attempt(state, 1000, panel:=True), "opening the panel must allow time to press Stop")
        Check(Waiting(state), "panel grace must leave focus recovery pending")
        For Each tick In {1045L, 2000L, 3999L}
            Check(Not Attempt(state, tick, panel:=True), "main panel grace must last three seconds")
        Next
        Check(Attempt(state, 4000, panel:=True), "main panel focus must not suspend forced foreground indefinitely")
        Check(Not Attempt(state, 4500, panel:=True), "main panel reclamation must also honor retry pacing")
        Check(Attempt(state, 4750, panel:=True), "focus restoration must continue after the main panel grace expires")

        Check(Not Attempt(state, 5500, suspended:=True, panel:=True), "a modal or config dialog must block restoration")
        Check(Not Attempt(state, 20000, suspended:=True, panel:=True), "long-running dialogs must remain usable without focus theft")
        Check(Not Attempt(state, 20001, panel:=True), "closing a modal must provide a fresh main panel Stop grace")
        Check(Not Attempt(state, 23000, panel:=True), "the post-dialog grace must not expire early")
        Check(Attempt(state, 23001, panel:=True), "foreground maintenance must resume after the dialog and grace finish")
        Check(Attempt(state, 24000, panel:=False), "another app must not inherit the main panel grace")
        Check(Not Attempt(state, 24001, panel:=True), "returning to the main panel must grant another bounded Stop opportunity")
        Check(Attempt(state, 27001, panel:=True), "another main panel visit must also expire instead of permanently disabling maintenance")
    End Sub

    Private Sub TestTargetChangesStopAndClosing()
        Dim state = NewSchedule()
        Check(Attempt(state, 0), "fixture must start restoring its selected target")
        Check(Attempt(state, 1, hwnd:=102), "selecting a different target must discard the previous target's retry state")
        Check(Not Attempt(state, 2, hwnd:=102), "the new target must receive its own rate limit")
        Check(Attempt(state, 3, hwnd:=102, pid:=202), "a new captured PID must not reuse state owned by an older PID")
        Check(Not Attempt(state, 4, hwnd:=0) AndAlso Not Waiting(state), "no HWND must prevent all recovery requests")
        Check(Not Attempt(state, 5, pid:=0) AndAlso Not Waiting(state), "no captured process identity must prevent all recovery requests")
        Check(Attempt(state, 6), "restoring a valid selected target must be possible after an unavailable target")
        Check(Not Attempt(state, 7, running:=False) AndAlso Not Waiting(state), "explicit stop must clear pending recovery")
        Check(Not Attempt(state, 100000, running:=False), "a stopped engine must never be restarted or have its game focused by this scheduler")
        Check(Attempt(state, 100001), "a later explicit manual start must establish a fresh recovery lifecycle")
        Check(Not Attempt(state, 100002, closing:=True) AndAlso Not Waiting(state), "shutdown must prevent new focus requests")
        Reset(state)
        Check(Not Waiting(state), "cancellation reset must clear pending focus state")
    End Sub

    Private Sub TestFormLifecycleAndCancelledWorkflows()
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Check(Not CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "stopped/default form state must not authorize focus restoration")
        SetField(owner, "_resuRunning", True)
        Check(CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "running RESU must authorize its selected game")
        SetField(owner, "_resuRunning", False)
        Using cancellation As New CancellationTokenSource()
            SetField(owner, "_quizSolveInProgress", True)
            SetField(owner, "_quizCancellation", cancellation)
            Check(CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "active Quiz solving must authorize its game")
            cancellation.Cancel()
            Check(Not CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "F12-cancelled Quiz must stop restoring focus before async cleanup finishes")
            SetField(owner, "_quizSolveInProgress", False)
        End Using
        Using cancellation As New CancellationTokenSource()
            SetField(owner, "_tradeRunning", True)
            SetField(owner, "_tradeCancellation", cancellation)
            Check(CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "a running Trade queue must authorize its frozen target")
            cancellation.Cancel()
            Check(Not CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "cancelled Trade must not keep stealing focus while its sender unwinds")
            SetField(owner, "_tradeRunning", False)
        End Using

        Using tradeCancel As New CancellationTokenSource(), quizCancel As New CancellationTokenSource(), quizEnabled As New CheckBox With {.Checked = True}
            SetField(owner, "_tradeRunning", True)
            SetField(owner, "_tradeCancellation", tradeCancel)
            SetField(owner, "_quizSolveInProgress", True)
            SetField(owner, "_quizCancellation", quizCancel)
            SetField(owner, "chkQuizSolverEnabled", quizEnabled)
            InvokeForm(owner, "CancelActiveGameplayOnUserStop")
            Check(tradeCancel.IsCancellationRequested AndAlso quizCancel.IsCancellationRequested AndAlso Not quizEnabled.Checked,
                  "user Stop/API stop must cancel active Trade/Quiz and turn Quiz monitoring off")
            Check(Not CBool(InvokeForm(owner, "HasActiveGameplayForFocus")), "stopped workflows must not reauthorize focus on the next timer tick while their tasks unwind")
            SetField(owner, "_tradeRunning", False)
            SetField(owner, "_quizSolveInProgress", False)
        End Using

        Dim schedule = NewSchedule()
        Attempt(schedule, 0)
        SetField(owner, "_gameFocusSchedule", schedule)
        InvokeForm(owner, "CancelLaunchAutoStart")
        Check(Not Waiting(schedule), "the existing Stop/API cancellation hook must clear foreground maintenance too")
        Attempt(schedule, 1000)
        InvokeForm(owner, "ShutdownGameFocusMaintenance")
        Check(Not Waiting(schedule) AndAlso CBool(GetType(Form1).GetField("_gameFocusClosing", InstanceFlags).GetValue(owner)), "form shutdown must clear recovery state and permanently mark the supervisor closed")
    End Sub

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, InstanceFlags).SetValue(owner, value)
    End Sub

    Private Function InvokeForm(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Return GetType(Form1).GetMethod(name, InstanceFlags).Invoke(owner, arguments)
    End Function
End Module
