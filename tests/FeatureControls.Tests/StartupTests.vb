Imports System.Reflection
Imports System.Runtime.CompilerServices

Friend Module StartupTests
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private ReadOnly ScheduleType As Type = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.LaunchAutoStartSchedule", throwOnError:=True)
    Private assertions As Integer

    Public Sub Run()
        assertions = 0
        TestLaunchRetryAndCompletion()
        TestExplicitStopsAndDisabledLaunch()
        TestStartupPrerequisites()
        TestPreferredSelection()
        TestHomeStaysSelected()
        Console.WriteLine($"PASS: {assertions} startup assertions: blocked-start retries, monotonic pacing, launch-only completion, Stop/F12 cancellation, no-auto-start, and selected-game resolution, Home tab selection (owned offline state only).")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        assertions += 1
        If Not condition Then Throw New InvalidOperationException("Startup test: " & reason)
    End Sub

    Private Function NewSchedule() As Object
        Return Activator.CreateInstance(ScheduleType, nonPublic:=True)
    End Function

    Private Function InvokeSchedule(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Return ScheduleType.GetMethod(name).Invoke(owner, arguments)
    End Function

    Private Function PropertyValue(Of T)(owner As Object, name As String) As T
        Return DirectCast(ScheduleType.GetProperty(name).GetValue(owner), T)
    End Function

    Private Sub TestLaunchRetryAndCompletion()
        Dim state = NewSchedule()
        InvokeSchedule(state, "Arm", False)
        Check(PropertyValue(Of Boolean)(state, "Pending") AndAlso Not PropertyValue(Of Boolean)(state, "Started"), "launch must request a start without claiming the engine is running")
        Check(CBool(InvokeSchedule(state, "TryBeginPoll", 1000L)), "the first launch attempt must be immediate")
        Check(Not CBool(InvokeSchedule(state, "TryBeginPoll", 1000L)), "a nested timer tick must not start a second worker")
        InvokeSchedule(state, "CompletePoll", 1000L, False, "Waiting for zoom replacement")
        Check(PropertyValue(Of Boolean)(state, "Pending") AndAlso Not PropertyValue(Of Boolean)(state, "Started"), "a blocked attempt must remain pending")
        Check(PropertyValue(Of String)(state, "WaitingReason") = "Waiting for zoom replacement", "Home must receive the actual startup block reason")
        For Each tick In {1000L, 1001L, 2999L}
            Check(Not CBool(InvokeSchedule(state, "TryBeginPoll", tick)), "retry must be paced rather than poll/start every UI event")
        Next
        Check(CBool(InvokeSchedule(state, "TryBeginPoll", 3000L)), "temporary blocks must be retried")
        InvokeSchedule(state, "CompletePoll", 3000L, False, "Waiting for Trade")
        Check(PropertyValue(Of Boolean)(state, "Pending") AndAlso PropertyValue(Of String)(state, "WaitingReason") = "Waiting for Trade", "a different blocked operation must not consume launch startup")
        Check(CBool(InvokeSchedule(state, "TryBeginPoll", 5000L)), "startup must retry after another rejected start")
        InvokeSchedule(state, "CompletePoll", 5000L, True, "")
        Check(PropertyValue(Of Boolean)(state, "Started") AndAlso Not PropertyValue(Of Boolean)(state, "Pending"), "only confirmed engine startup completes the request")
        Check(PropertyValue(Of String)(state, "WaitingReason") = "", "successful startup must clear the waiting message")
        Check(Not CBool(InvokeSchedule(state, "TryBeginPoll", 1000000L)), "the launch scheduler must never restart a later stopped engine")
        InvokeSchedule(state, "Arm", False)
        Check(Not PropertyValue(Of Boolean)(state, "Pending"), "a repeated Shown event must not arm a completed launch again")

        Dim nextLaunch = NewSchedule()
        InvokeSchedule(nextLaunch, "Arm", False)
        Check(CBool(InvokeSchedule(nextLaunch, "TryBeginPoll", 1000L)), "each new app instance must independently start")
    End Sub

    Private Sub TestExplicitStopsAndDisabledLaunch()
        For Each stopAction In {"Stop", "F12"}
            Dim state = NewSchedule()
            InvokeSchedule(state, "Arm", False)
            Check(CBool(InvokeSchedule(state, "TryBeginPoll", 0L)), stopAction & " fixture must begin pending startup")
            InvokeSchedule(state, "CompletePoll", 0L, False, "Waiting for a workflow")
            InvokeSchedule(state, "Cancel")
            Check(Not PropertyValue(Of Boolean)(state, "Pending") AndAlso Not PropertyValue(Of Boolean)(state, "Started"), stopAction & " must cancel pending startup without fabricating a start")
            Check(PropertyValue(Of String)(state, "WaitingReason") = "" AndAlso Not CBool(InvokeSchedule(state, "TryBeginPoll", 3000L)), stopAction & " must clear the waiting message and prevent later automatic restart")
            InvokeSchedule(state, "Arm", False)
            Check(Not PropertyValue(Of Boolean)(state, "Pending"), stopAction & " cancellation must last for the current launch")
        Next

        Dim inFlight = NewSchedule()
        InvokeSchedule(inFlight, "Arm", False)
        InvokeSchedule(inFlight, "TryBeginPoll", 0L)
        InvokeSchedule(inFlight, "Cancel")
        InvokeSchedule(inFlight, "CompletePoll", 0L, False, "Late blocked result")
        Check(Not PropertyValue(Of Boolean)(inFlight, "Pending") AndAlso PropertyValue(Of String)(inFlight, "WaitingReason") = "", "a late rejected attempt must not undo explicit stop")

        Dim disabled = NewSchedule()
        InvokeSchedule(disabled, "Arm", True)
        Check(Not PropertyValue(Of Boolean)(disabled, "Pending") AndAlso Not CBool(InvokeSchedule(disabled, "TryBeginPoll", 0L)), "--no-auto-start must suppress launch input workers for diagnostics and owned UI fixtures")
        InvokeSchedule(disabled, "Arm", False)
        Check(Not PropertyValue(Of Boolean)(disabled, "Pending"), "disabled diagnostic startup cannot be rearmed by a repeated lifecycle event")

        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim waiting = NewSchedule()
        InvokeSchedule(waiting, "Arm", False)
        SetField(owner, "_launchAutoStart", waiting)
        SetField(owner, "_launchGameSelectionPending", True)
        Check(CBool(InvokeForm(owner, "IsLaunchAutoStartPending")), "Form must expose the real pending scheduler state")
        InvokeForm(owner, "CancelLaunchAutoStart")
        Check(Not CBool(InvokeForm(owner, "IsLaunchAutoStartPending")) AndAlso Not CBool(GetField(owner, "_launchGameSelectionPending")), "Form stop/shutdown hook must cancel startup and late game selection together")
    End Sub

    Private Sub TestStartupPrerequisites()
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")) = "", "engine startup must be allowed without a game or foreground focus; actual input validates its target separately")
        SetField(owner, "_extrasBusy", True)
        SetField(owner, "_extrasApplyingZoom", True)
        Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")).Contains("zoom"), "config replacement must defer startup")
        SetField(owner, "_extrasApplyingZoom", False)
        Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")) = "", "a Mosaic download must not unnecessarily block combat startup")
        SetField(owner, "_extrasBusy", False)
        For Each mode In {OperatingMode.Resu, OperatingMode.Quiz, OperatingMode.Trade}
            Dim workflow As New OperatingModeController()
            workflow.Transition(mode, "Owned startup prerequisite test")
            SetField(owner, "_workflowModeStore", workflow)
            Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")).Contains(mode.ToString()), "startup must defer to an active " & mode.ToString() & " workflow")
        Next
        SetField(owner, "_workflowModeStore", New OperatingModeController())
        SetField(owner, "_tradeRunning", True)
        Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")).Contains("Trade"), "the active whisper flag must block startup even before workflow state settles")
        SetField(owner, "_tradeRunning", False)
        Check(CStr(InvokeForm(owner, "GetLaunchAutoStartWaitReason")) = "", "resolved transient startup blocks must allow a later attempt")
    End Sub


    ' A removed or reentrant tab selection once left the sidebar with no selected page (index -1),
    ' so Home never became visible and kept its first stopped paint while the bot ran.
    Private Sub TestHomeStaysSelected()
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim tabsType = GetType(Form1).GetNestedType("SidebarTabControl", BindingFlags.NonPublic)
        Using tabs = DirectCast(Activator.CreateInstance(tabsType, nonPublic:=True), TabControl), home As New TabPage("Home"), combat As New TabPage("Combat")
            tabs.TabPages.Add(home)
            tabs.TabPages.Add(combat)
            SetField(owner, "_mainTabs", tabs)
            SetField(owner, "_dashboardTab", home)
            tabs.SelectedIndex = -1
            Check(tabs.SelectedIndex = -1, "fixture must start with no selected page")
            InvokeForm(owner, "EnsureMainTabSelected")
            Check(tabs.SelectedTab Is home, "an empty tab selection must fall back to Home")
            tabs.SelectedTab = combat
            InvokeForm(owner, "EnsureMainTabSelected")
            Check(tabs.SelectedTab Is combat, "an explicit user tab choice must not be overridden")
            SetField(owner, "_isRefreshingMainTabsVisibility", True)
            tabs.SelectedIndex = -1
            InvokeForm(owner, "EnsureMainTabSelected")
            Check(tabs.SelectedIndex = -1, "the guard must leave a tab rebuild in progress alone")
            SetField(owner, "_isRefreshingMainTabsVisibility", False)
        End Using
    End Sub
    Private Sub TestPreferredSelection()
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Using fullList As New ListBox(), liteList As New ListBox()
            SetField(owner, "lstProcessWindows", fullList)
            SetField(owner, "lstLiteProcessWindows", liteList)
            Dim other = NewProcessEntry("OtherApplication", 1)
            Dim firstGame = NewProcessEntry("KathanaGame", 2)
            Dim secondGame = NewProcessEntry("KathanaGame", 3)
            fullList.Items.AddRange({other, firstGame, secondGame})
            liteList.Items.AddRange({other, firstGame, secondGame})
            fullList.SelectedIndex = 0
            liteList.SelectedIndex = 0
            InvokeForm(owner, "SelectKathanaForLaunchIfNeeded")
            Check(fullList.SelectedIndex = 1 AndAlso liteList.SelectedIndex = 1, "a remembered unrelated window must not prevent automatic game selection")
            fullList.SelectedIndex = 2
            liteList.SelectedIndex = 2
            InvokeForm(owner, "SelectKathanaForLaunchIfNeeded")
            Check(fullList.SelectedIndex = 2 AndAlso liteList.SelectedIndex = 2, "an already selected Kathana client must be retained")
            fullList.Items.Clear()
            liteList.Items.Clear()
            fullList.Items.Add(other)
            liteList.Items.Add(other)
            fullList.SelectedIndex = 0
            liteList.SelectedIndex = 0
            InvokeForm(owner, "SelectKathanaForLaunchIfNeeded")
            Check(fullList.SelectedIndex = 0 AndAlso liteList.SelectedIndex = 0, "a missing game must not select a fabricated process")
        End Using
    End Sub

    Private Function NewProcessEntry(name As String, handle As Integer) As Object
        Dim type = GetType(Form1).GetNestedType("ProcessWindowEntry", BindingFlags.NonPublic)
        Dim entry = Activator.CreateInstance(type, nonPublic:=True)
        type.GetProperty("ProcessName").SetValue(entry, name)
        type.GetProperty("MainWindowHandle").SetValue(entry, New IntPtr(handle))
        Return entry
    End Function

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, InstanceFlags).SetValue(owner, value)
    End Sub

    Private Function GetField(owner As Object, name As String) As Object
        Return GetType(Form1).GetField(name, InstanceFlags).GetValue(owner)
    End Function

    Private Function InvokeForm(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Return GetType(Form1).GetMethod(name, InstanceFlags).Invoke(owner, arguments)
    End Function
End Module
