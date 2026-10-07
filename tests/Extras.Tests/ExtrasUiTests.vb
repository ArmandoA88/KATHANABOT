Imports System.Diagnostics
Imports System.Collections.Concurrent
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend Module ExtrasUiTests
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private checks As Integer
    Private fixtureStage As String

    Private NotInheritable Class OwnedUiSynchronizationContext
        Inherits SynchronizationContext

        Private ReadOnly ownerThread As Integer = Thread.CurrentThread.ManagedThreadId
        Private ReadOnly callbacks As New ConcurrentQueue(Of (Callback As SendOrPostCallback, State As Object))()

        Public Overrides Sub Post(callback As SendOrPostCallback, state As Object)
            callbacks.Enqueue((callback, state))
        End Sub

        Public Overrides Function CreateCopy() As SynchronizationContext
            Return Me
        End Function

        Public Sub Drain()
            If Thread.CurrentThread.ManagedThreadId <> ownerThread Then Throw New InvalidOperationException("Owned UI callbacks must run on the fixture STA thread")
            Dim work As (Callback As SendOrPostCallback, State As Object) = Nothing
            While callbacks.TryDequeue(work)
                work.Callback(work.State)
            End While
        End Sub
    End Class

    Public Sub Run()
        checks = 0
        fixtureStage = "construct owned UI"
        Dim failure As Exception = Nothing
        Dim worker As New Thread(Sub()
                                     Try
                                         ' A worker fixture must own its message pump rather than inherit
                                         ' a context whose UI thread is waiting in Main's Join call.
                                         SynchronizationContext.SetSynchronizationContext(Nothing)
                                         RunOwnedUi()
                                     Catch ex As Exception
                                         failure = New InvalidOperationException("Extras UI fixture failed during " & fixtureStage, ex)
                                     Finally
                                         SynchronizationContext.SetSynchronizationContext(Nothing)
                                     End Try
                                 End Sub)
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        worker.Join()
        If failure IsNot Nothing Then Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw()
        Console.WriteLine($"PASS: {checks} Extras UI assertions: hidden/unlocked tab retention, x2/x3 embedded presets, guarded actions, operation lifecycle/cancellation and 1100x850 layout (owned offline UI only).")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Extras UI test: " & reason)
    End Sub

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, InstanceFlags).SetValue(owner, value)
    End Sub

    Private Function Field(Of T)(owner As Object, name As String) As T
        Return DirectCast(GetType(Form1).GetField(name, InstanceFlags).GetValue(owner), T)
    End Function

    Private Function Invoke(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Try
            Return GetType(Form1).GetMethod(name, InstanceFlags).Invoke(owner, arguments)
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function

    Private Function ButtonNamed(page As TabPage, name As String) As Button
        Return page.Controls.Find(name, True).OfType(Of Button)().Single()
    End Function

    Private Function StartOperation(owner As Object, operation As Func(Of CancellationToken, Task(Of String)), requiresStoppedGame As Boolean) As Task
        Return DirectCast(Invoke(owner, "RunExtrasOperationAsync", "Owned test operation", operation, requiresStoppedGame), Task)
    End Function

    Private Sub CompleteTask(task As Task, Optional stage As String = "blocked guard")
        Dim timer = Stopwatch.StartNew()
        While Not task.IsCompleted AndAlso timer.ElapsedMilliseconds < 5000
            TryCast(SynchronizationContext.Current, OwnedUiSynchronizationContext)?.Drain()
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Check(task.IsCompleted, stage & ": owned operation must finish without blocking the UI thread; context=" & If(SynchronizationContext.Current?.GetType().Name, "none") & ", state=" & task.Status.ToString())
        task.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Sub RunOwnedUi()
        ' This fixture never constructs the app, loads profiles or accesses game settings.
        ' Only the Extras page and an off-screen host are created. Every real action click
        ' runs with its lock/busy guard already asserted, and all permitted operations
        ' use local delegates instead of file, download or process-launch services.
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim full As New BotEngine()
        Dim lite As New BotEngine()
        SetField(owner, "_fullEngine", full)
        SetField(owner, "_liteEngine", lite)
        Using page = DirectCast(Invoke(owner, "BuildExtrasTab"), TabPage),
              quizEnabled As New CheckBox(),
              host As New Form With {.ClientSize = New Size(1100, 850), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)},
              tabs As New TabControl With {.Dock = DockStyle.Fill}
            SetField(owner, "_extrasTab", page)
            SetField(owner, "chkQuizSolverEnabled", quizEnabled)
            Invoke(owner, "ApplyDarkTheme", page)
            tabs.TabPages.Add(page)
            host.Controls.Add(tabs)
            host.Show()
            Application.DoEvents()
            ' Keep async continuations on this fixture's STA without relying on
            ' Windows Forms' hidden marshalling control or an Application.Run loop.
            SynchronizationContext.SetSynchronizationContext(New OwnedUiSynchronizationContext())

            Check(page.Text = "EXTRAS", "the unlocked sidebar page must be named EXTRAS")
            Dim selector = Field(Of ComboBox)(owner, "_extrasZoomPreset")
            Check(selector.DropDownStyle = ComboBoxStyle.DropDownList, "zoom selection must be limited to supplied presets")
            Check(selector.Items.Count = 2, "exactly two supplied zoom presets must be offered")
            Check(selector.Items(0).ToString().Contains("x2") AndAlso selector.Items(1).ToString().Contains("x3"), "the two choices must identify x2 and x3")
            Check(selector.SelectedIndex = 0, "x2 must be the initial selection")
            Check(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2).Length > 0, "x2 must travel inside the app")
            Check(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3).Length > 0, "x3 must travel inside the app")
            Check(Not ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3)), "x2 and x3 must remain distinct presets")
            Check(Field(Of TextBox)(owner, "_extrasInstallationPath").PlaceholderText.Contains("KathanaGame.exe"), "the folder picker must explain which installation to select")
            Dim mosaicPath = page.Controls.Find("ExtrasMosaicPath", True).OfType(Of TextBox)().Single()
            Check(mosaicPath.ReadOnly AndAlso mosaicPath.Text.Length > 0, "the installed Mosaic location must be visible and read-only")
            Check(Not Field(Of Button)(owner, "_extrasCancel").Enabled, "cancel must begin disabled")

            fixtureStage = "locked actions"
            TestBlockedActions(owner, page, False)
            SetField(owner, "_quizUnlocked", True)
            SetField(owner, "_extrasBusy", True)
            fixtureStage = "busy actions"
            TestBlockedActions(owner, page, True)
            SetField(owner, "_extrasBusy", False)
            fixtureStage = "automation guards"
            TestAutomationGuards(owner, full, lite, quizEnabled)
            fixtureStage = "operation lifecycle"
            TestLifecycle(owner, page)
            fixtureStage = "authoritative tab visibility"
            TestAuthoritativeVisibility(owner, page, tabs, full, lite)

            Field(Of Label)(owner, "_extrasStatus").Text = "Choose a zoom preset or save Remote Desktop Mosaic."
            host.PerformLayout()
            Application.DoEvents()
            fixtureStage = "layout and render"
            CheckLayoutAndRender(page, host)
            fixtureStage = "dispose owned UI"
        End Using
    End Sub

    Private Sub TestBlockedActions(owner As Object, page As TabPage, busy As Boolean)
        Dim expected = If(busy, "already running", "locked")
        For Each stoppedGame In {False, True}
            Check(CStr(Invoke(owner, "GetExtrasBlockReason", stoppedGame)).Contains(expected), "lock/busy must guard both operation kinds")
            Dim executions = 0
            Dim fixture As Func(Of CancellationToken, Task(Of String)) = Function(token)
                                                                              executions += 1
                                                                              Return Task.FromResult("This delegate must not run")
                                                                          End Function
            CompleteTask(StartOperation(owner, fixture, stoppedGame))
            Check(executions = 0, "blocked work must not execute a file/download delegate")
            Check(Field(Of CancellationTokenSource)(owner, "_extrasCancellation") Is Nothing, "blocked work must not create a cancellation source")
        Next

        Dim path = Field(Of TextBox)(owner, "_extrasInstallationPath").Text
        For Each name In {"ExtrasBrowse", "ExtrasSelectedGame", "ExtrasApplyZoom", "ExtrasRestoreZoom", "ExtrasDownloadMosaic", "ExtrasBundledMosaic", "ExtrasOpenMosaic"}
            Dim status = Field(Of Label)(owner, "_extrasStatus")
            status.Text = "Owned fixture sentinel"
            Dim action = ButtonNamed(page, name)
            Check(action.Visible AndAlso action.Enabled, name & " must be reachable in the owned action fixture")
            action.PerformClick()
            Application.DoEvents()
            Check(status.Text.Contains(expected), name & " must enforce the shared lock/busy guard before I/O or opening a dialog")
            Check(Field(Of TextBox)(owner, "_extrasInstallationPath").Text = path, "blocked actions must not replace the installation path")
        Next
    End Sub

    Private Sub TestAutomationGuards(owner As Object, full As BotEngine, lite As BotEngine, quizEnabled As CheckBox)
        Check(CStr(Invoke(owner, "GetExtrasBlockReason", True)) = "", "idle unlocked zoom work must be available")
        For Each flag In {"_tradeRunning", "_tradeAnalyzing", "_quizSolveInProgress", "_resuRunning", "_resuBusy"}
            SetField(owner, flag, True)
            AssertAutomationBlocked(owner, flag)
            SetField(owner, flag, False)
        Next
        quizEnabled.Checked = True
        AssertAutomationBlocked(owner, "enabled quiz solver")
        quizEnabled.Checked = False
        For Each engine In {full, lite}
            SetEngineRunning(engine, True)
            AssertAutomationBlocked(owner, "running combat engine")
            SetEngineRunning(engine, False)
        Next
        Dim mode = Field(Of OperatingModeController)(owner, "_workflowModeStore")
        For Each runningMode In {OperatingMode.Full, OperatingMode.Lite, OperatingMode.Trade, OperatingMode.Resu, OperatingMode.Quiz}
            ' Assign only the owned controller state; do not trigger a real workflow or journal.
            GetType(OperatingModeController).GetField("_mode", InstanceFlags).SetValue(mode, runningMode)
            AssertAutomationBlocked(owner, "workflow " & runningMode.ToString())
        Next
        GetType(OperatingModeController).GetField("_mode", InstanceFlags).SetValue(mode, OperatingMode.Idle)
        Check(CStr(Invoke(owner, "GetExtrasBlockReason", True)) = "", "stopping all automation must restore zoom availability")
    End Sub

    Private Sub AssertAutomationBlocked(owner As Object, source As String)
        Check(CStr(Invoke(owner, "GetExtrasBlockReason", True)).Contains("Stop automation"), source & " must guard zoom changes")
        Check(CStr(Invoke(owner, "GetExtrasBlockReason", False)) = "", source & " must leave Mosaic downloads available")
        Dim executions = 0
        Dim operation As Func(Of CancellationToken, Task(Of String)) = Function(token)
                                                                         executions += 1
                                                                         Return Task.FromResult("Owned fixture")
                                                                     End Function
        CompleteTask(StartOperation(owner, operation, True))
        Check(executions = 0, source & " must prevent the zoom delegate from running")
    End Sub

    Private Sub SetEngineRunning(engine As BotEngine, running As Boolean)
        Dim status = DirectCast(GetType(BotEngine).GetField("_status", InstanceFlags).GetValue(engine), BotStatus)
        status.Running = running
    End Sub

    Private Sub TestLifecycle(owner As Object, page As TabPage)
        For Each zoom In {False, True}
            Dim result As New TaskCompletionSource(Of String)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim calls = 0
            Dim tokenSeen As CancellationToken
            Dim operation As Func(Of CancellationToken, Task(Of String)) = Function(token)
                                                                             calls += 1
                                                                             tokenSeen = token
                                                                             Return result.Task
                                                                         End Function
            Dim pending = StartOperation(owner, operation, zoom)
            Check(calls = 1 AndAlso Not pending.IsCompleted, "permitted work must start exactly once")
            Check(Field(Of Boolean)(owner, "_extrasBusy"), "work must own the busy guard before its first await")
            Check(Field(Of Boolean)(owner, "_extrasApplyingZoom") = zoom, "only zoom work must block automation startup")
            Check(Not Field(Of Control)(owner, "_extrasActions").Enabled, "action controls must be disabled during work")
            Check(Field(Of Button)(owner, "_extrasCancel").Enabled = Not zoom, "only downloads may offer cancel in the UI")
            Check(tokenSeen.CanBeCanceled, "permitted operations must receive their own cancellation token")
            Dim original = Field(Of CancellationTokenSource)(owner, "_extrasCancellation")
            CompleteTask(StartOperation(owner, operation, zoom))
            Check(calls = 1, "a second operation must be rejected while the first is pending")
            Check(Field(Of CancellationTokenSource)(owner, "_extrasCancellation") Is original, "a rejected operation must not replace the active cancellation source")
            result.SetResult("Owned operation completed")
            CompleteTask(pending, "successful " & If(zoom, "zoom", "download"))
            Check(Field(Of Label)(owner, "_extrasStatus").Text = "Owned operation completed", "successful work must report its result")
            AssertIdle(owner)
        Next

        Dim cancelled As New TaskCompletionSource(Of String)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim cancellationRegistration As CancellationTokenRegistration
        Dim cancelOperation As Func(Of CancellationToken, Task(Of String)) = Function(token)
                                                                                  cancellationRegistration = token.Register(Sub() cancelled.TrySetCanceled(token))
                                                                                  Return cancelled.Task
                                                                              End Function
        Dim download = StartOperation(owner, cancelOperation, False)
        Dim cancelButton = ButtonNamed(page, "ExtrasCancel")
        Check(cancelButton.Visible AndAlso cancelButton.Enabled, "the running download must expose its cancel action")
        cancelButton.PerformClick()
        Check(cancelled.Task.IsCanceled, "clicking cancel must reach the active operation token")
        CompleteTask(download, "cancel download")
        cancellationRegistration.Dispose()
        Check(Field(Of Label)(owner, "_extrasStatus").Text.Contains("Cancelled"), "cancel must report cancellation without failing the UI")
        AssertIdle(owner)

        For Each failure As Exception In New Exception() {New UnauthorizedAccessException("Owned denied fixture"), New IOException("Owned failed fixture")}
            CompleteTask(StartOperation(owner, Function(token) Task.FromException(Of String)(failure), False), "failed " & failure.GetType().Name)
            Check(Field(Of Label)(owner, "_extrasStatus").Text.Contains(failure.Message), "failures must remain visible to the user")
            AssertIdle(owner)
        Next

        Dim stopped As New TaskCompletionSource(Of String)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim shutdownRegistration As CancellationTokenRegistration
        Dim onShutdown As Func(Of CancellationToken, Task(Of String)) = Function(token)
                                                                           shutdownRegistration = token.Register(Sub() stopped.TrySetCanceled(token))
                                                                           Return stopped.Task
                                                                       End Function
        Dim shutdown = StartOperation(owner, onShutdown, False)
        Invoke(owner, "ShutdownExtras")
        Check(stopped.Task.IsCanceled, "shutdown must reach the active operation token")
        CompleteTask(shutdown, "shutdown download")
        shutdownRegistration.Dispose()
        Check(Field(Of Label)(owner, "_extrasStatus").Text.Contains("Cancelled"), "closing the app must cancel pending Extras work")
        AssertIdle(owner)
    End Sub

    Private Sub AssertIdle(owner As Object)
        Check(Not Field(Of Boolean)(owner, "_extrasBusy") AndAlso Not Field(Of Boolean)(owner, "_extrasApplyingZoom"), "completion must clear both operation flags")
        Check(Field(Of CancellationTokenSource)(owner, "_extrasCancellation") Is Nothing, "completion must clear its cancellation source")
        Check(Field(Of Control)(owner, "_extrasActions").Enabled, "completion must restore the actions")
        Check(Not Field(Of Button)(owner, "_extrasCancel").Enabled, "completion must disable cancel")
    End Sub

    Private Sub TestAuthoritativeVisibility(owner As Object, extras As TabPage, renderTabs As TabControl, full As BotEngine, lite As BotEngine)
        renderTabs.TabPages.Remove(extras)
        Dim tabsType = GetType(Form1).GetField("_mainTabs", InstanceFlags).FieldType
        Using inheritedControlState As New Form(),
              main = DirectCast(Activator.CreateInstance(tabsType, True), TabControl),
              home As New TabPage("Home"), quiz As New TabPage("Quiz"),
              resu As New TabPage("Resu"), trade As New TabPage("Trade"),
              diagnostics As New TabPage("Diagnostics"), updates As New TabPage("Updates")
            ' RefreshMainTabsVisibility queries the base IsHandleCreated property.
            ' Supply only its inherited native-window backing from an owned vanilla
            ' Form, without running Form1's constructor or creating a real owner handle.
            Dim nativeWindow = GetType(Control).GetField("_window", InstanceFlags)
            Check(nativeWindow IsNot Nothing, "the owned base-Control window seam must be available")
            nativeWindow.SetValue(owner, nativeWindow.GetValue(inheritedControlState))
            Check(Not DirectCast(owner, Control).IsHandleCreated, "the uninitialized owner must remain without an app handle")
            SetField(owner, "_mainTabs", main)
            SetField(owner, "_dashboardTab", home)
            SetField(owner, "_quizTab", quiz)
            SetField(owner, "_resuTab", resu)
            SetField(owner, "_tradeTab", trade)
            SetField(owner, "_diagnosticsTab", diagnostics)
            SetField(owner, "_updateTab", updates)
            SetField(owner, "_quizUnlocked", False)
            Invoke(owner, "RefreshMainTabsVisibility")
            Check(Not main.TabPages.Contains(extras), "Extras must stay hidden before the shared Home unlock flag")
            Check(main.TabPages.Count = 2, "only owned Home and Updates fixtures should appear while locked")
            SetField(owner, "_quizUnlocked", True)
            Invoke(owner, "RefreshMainTabsVisibility")
            Check(main.TabPages.Contains(extras), "Extras must join the same authoritative list as the unlocked tabs")
            Check(main.TabPages.IndexOf(extras) > main.TabPages.IndexOf(trade), "Extras must follow the Trade tab")
            main.SelectedTab = extras
            Invoke(owner, "RefreshMainTabsVisibility")
            Check(main.SelectedTab Is extras, "the normal sidebar refresh must preserve selected Extras")
            SetField(owner, "_developerModeEnabled", True)
            Invoke(owner, "RefreshMainTabsVisibility")
            Check(main.TabPages.Contains(extras) AndAlso main.SelectedTab Is extras, "rebuilding for developer tabs must preserve Extras")
            For Each engine In {full, lite}
                SetEngineRunning(engine, True)
                Invoke(owner, "RefreshMainTabsVisibility")
                Check(main.TabPages.Contains(extras) AndAlso main.SelectedTab Is extras, "edition changes must not remove unlocked Extras")
                SetEngineRunning(engine, False)
            Next
            SetField(owner, "_quizUnlocked", False)
            Invoke(owner, "RefreshMainTabsVisibility")
            Check(Not main.TabPages.Contains(extras) AndAlso main.SelectedTab Is home, "clearing the unlock flag must hide Extras and safely select Home")
            SetField(owner, "_quizUnlocked", True)
            main.TabPages.Remove(extras)
            SetField(owner, "_mainTabs", Nothing)
            nativeWindow.SetValue(owner, Nothing)
        End Using
        renderTabs.TabPages.Add(extras)
        renderTabs.SelectedTab = extras
        Application.DoEvents()
    End Sub

    Private Sub CheckLayoutAndRender(page As TabPage, host As Form)
        For Each name In {"ExtrasInstallationPath", "ExtrasZoomPreset", "ExtrasBrowse", "ExtrasSelectedGame", "ExtrasApplyZoom", "ExtrasRestoreZoom", "ExtrasDownloadMosaic", "ExtrasBundledMosaic", "ExtrasOpenMosaic", "ExtrasMosaicPath", "ExtrasStatus", "ExtrasCancel"}
            Dim control = page.Controls.Find(name, True).Single()
            Check(control.Visible AndAlso control.Width > 0 AndAlso control.Height > 0, name & " must be laid out and visible")
            Dim topLeft = host.PointToClient(control.PointToScreen(Point.Empty))
            Dim bounds As New Rectangle(topLeft, control.Size)
            Check(host.ClientRectangle.Contains(bounds), name & " must fit in the 1100x850 window")
            If TypeOf control Is Button Then
                Check(control.Width >= TextRenderer.MeasureText(control.Text, control.Font).Width + 6, name & " must not clip its label")
            End If
        Next
        Dim selector = page.Controls.Find("ExtrasZoomPreset", True).OfType(Of ComboBox)().Single()
        selector.SelectedIndex = 1
        Check(selector.Width >= TextRenderer.MeasureText(selector.SelectedItem.ToString(), selector.Font).Width + 28, "the x3 label must fit in its selector")
        selector.SelectedIndex = 0
        Using bitmap As New Bitmap(host.ClientSize.Width, host.ClientSize.Height)
            host.DrawToBitmap(bitmap, New Rectangle(Point.Empty, host.ClientSize))
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "extras-tab.png"))
        End Using
    End Sub
End Module
