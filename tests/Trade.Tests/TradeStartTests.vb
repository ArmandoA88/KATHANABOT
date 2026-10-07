Imports System.Diagnostics
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks

Module TradeStartTests
    Private ReadOnly PrivateInstance As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private ReadOnly PrivateStatic As BindingFlags = BindingFlags.Static Or BindingFlags.NonPublic
    Private checks As Integer

    Public Sub RunTests()
        Dim originalInput = WindowsInput.Current
        Dim originalMode = WindowsInput.InputMode
        Dim originalKeyMode = WindowsInput.BackgroundKeyMode
        Check(TypeOf originalInput Is ForegroundWindowsInput AndAlso WindowsInput.InputMode = "foreground" AndAlso Not WindowsInput.KeyboardOnlyMode,
              "Trade Start must be tested with the production foreground SendInput backend selected")
        TestStartGuards()
        Check(Object.ReferenceEquals(originalInput, WindowsInput.Current), "Building Trade or checking Start guards changed the production input backend")
        Dim noInput As New NoInput()
        Try
            WindowsInput.Current = noInput
            TestWorkerSnapshots()
            TestWorkerWaits()
            TestForegroundWaits()
            TestSubmissionOwnership()
            Check(noInput.Calls = 0, "Trade guard/wait/submission helpers emitted input or activated a window")
        Finally
            WindowsInput.Current = originalInput
        End Try
        Check(Object.ReferenceEquals(originalInput, WindowsInput.Current) AndAlso WindowsInput.InputMode = originalMode AndAlso
              WindowsInput.BackgroundKeyMode = originalKeyMode AndAlso Not WindowsInput.KeyboardOnlyMode,
              "Trade Start fixture changed the combat backend or its configured keyboard mode")
        Console.WriteLine($"PASS: {checks} offline Trade Start assertions: visible guards/caption, all worker references and bounded waits, focus/cancellation between recipients, and exact-case submission ownership without game input.")
    End Sub

    Private Sub TestStartGuards()
        Using fixture As New UiFixture()
            Check(DirectCast(fixture.Field("_tradeStart"), Button).Text = "Start whispers (foreground)", "Start does not expose foreground whisper behavior")
            Check(CStr(fixture.Invoke("GetTradeStartBlockReason")).Contains("locked", StringComparison.OrdinalIgnoreCase), "Locked Trade exits without an explicit reason")
            fixture.SetField("_quizUnlocked", True)
            Check(CStr(fixture.Invoke("GetTradeStartBlockReason")) = "" AndAlso WindowsInput.InputMode = "foreground" AndAlso Not WindowsInput.KeyboardOnlyMode,
                  "Unlocked idle Trade is still blocked with the foreground combat backend")
            fixture.SetField("_tradeRunning", True)
            Check(CStr(fixture.Invoke("GetTradeStartBlockReason")).Contains("already running", StringComparison.OrdinalIgnoreCase), "Running Trade has no explicit Start guard")
            fixture.SetField("_tradeRunning", False)
            fixture.SetField("_tradeAnalyzing", True)
            Check(CStr(fixture.Invoke("GetTradeStartBlockReason")).Contains("analysis", StringComparison.OrdinalIgnoreCase), "Analysis-in-progress has no explicit Start guard")
            fixture.SetField("_tradeAnalyzing", False)
            Dim modes As New OperatingModeController()
            GetType(OperatingModeController).GetField("_mode", PrivateInstance).SetValue(modes, OperatingMode.Resu)
            fixture.SetField("_workflowModeStore", modes)
            Dim blocked = CStr(fixture.Invoke("GetTradeStartBlockReason"))
            Check(blocked.Contains("Resu", StringComparison.OrdinalIgnoreCase) AndAlso blocked.Contains("Stop", StringComparison.OrdinalIgnoreCase),
                  "Another active workflow is not named in the Start guard")
            GetType(OperatingModeController).GetField("_mode", PrivateInstance).SetValue(modes, OperatingMode.Idle)
            Check(CStr(fixture.Invoke("GetTradeStartBlockReason")) = "", "The Start guard remains blocked after the isolated workflow becomes idle")
        End Using
    End Sub

    Private Sub TestWorkerSnapshots()
        Dim engine = NewEngine()
        Dim names = {"_task", "_directKpTask", "_autoLootArrowTask", "_lootScannerProcessingTask"}
        Dim sources = names.Select(Function(name) New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray()
        Check(engine.GetInputWorkerTasks().Length = 0, "An empty worker snapshot invents workers")
        For i = 0 To names.Length - 1
            SetEngineField(engine, names(i), sources(i).Task)
        Next
        Dim snapshot = engine.GetInputWorkerTasks()
        Check(snapshot.Length = 4 AndAlso sources.All(Function(source) snapshot.Any(Function(worker) Object.ReferenceEquals(worker, source.Task))),
              "Worker snapshot omitted or replaced a main, direct-KP, arrow, or scanner task")
        For i = 0 To sources.Length - 1
            sources(i).SetResult(True)
            Check(engine.GetInputWorkerTasks().All(Function(worker) worker.IsCompleted) = (i = sources.Length - 1),
                  "Worker completion ignored another incomplete input worker")
        Next
        Check(engine.GetInputWorkerTasks().Length = 4, "Completed worker references disappeared from the snapshot")
        SetEngineField(engine, "_lootScannerProcessingTask", Nothing)
        Check(snapshot.Length = 4 AndAlso Object.ReferenceEquals(snapshot(3), sources(3).Task) AndAlso engine.GetInputWorkerTasks().Length = 3,
              "Clearing the current scanner task also lost its previously captured reference")
        SetEngineField(engine, "_directKpTask", sources(0).Task)
        Check(engine.GetInputWorkerTasks().Length = 2, "Worker snapshots do not deduplicate identical task references")
    End Sub

    Private Sub TestWorkerWaits()
        Using fixture As New UiFixture(), cancelled As New CancellationTokenSource()
            fixture.SetRows()
            Dim before = fixture.QueueSnapshot()
            cancelled.Cancel()
            Dim faulted = Task.FromException(New InvalidOperationException("Synthetic stopped worker"))
            Dim observed = faulted.Exception
            Check(observed IsNot Nothing, "Synthetic faulted worker was not observed")
            WaitUi(fixture.WaitWorkers(CancellationToken.None, {Task.CompletedTask, Task.FromCanceled(cancelled.Token), faulted}))
            Check(fixture.QueueSnapshot() = before, "Completed/cancelled/faulted worker draining changed the reviewed queue")

            Dim scanner As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim engine = NewEngine()
            SetEngineField(engine, "_lootScannerProcessingTask", scanner.Task)
            fixture.SetField("_fullEngine", engine)
            Dim retained = engine.GetInputWorkerTasks()
            SetEngineField(engine, "_lootScannerProcessingTask", Nothing)
            Dim draining = fixture.WaitWorkers(CancellationToken.None, retained, 1000)
            PumpFor(40)
            Check(Not draining.IsCompleted, "A detached but retained scanner task was ignored before whispers")
            scanner.SetResult(True)
            WaitUi(draining)
            Check(fixture.QueueSnapshot() = before, "Retained scanner draining changed the reviewed queue")

            Dim initial As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim refreshed As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim refreshing = fixture.WaitWorkers(CancellationToken.None, {initial.Task}, 1000)
            SetEngineField(engine, "_lootScannerProcessingTask", refreshed.Task)
            PumpFor(40)
            initial.SetResult(True)
            PumpFor(40)
            Check(Not refreshing.IsCompleted, "Worker draining did not retain a task discovered in a refreshed engine snapshot")
            SetEngineField(engine, "_lootScannerProcessingTask", Nothing)
            refreshed.SetResult(True)
            WaitUi(refreshing)

            Dim pending As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim timed = Stopwatch.StartNew()
            ExpectInvalid(Sub() WaitUi(fixture.WaitWorkers(CancellationToken.None, {pending.Task}, 25)), "still stopping")
            Check(timed.ElapsedMilliseconds < 2000 AndAlso fixture.QueueSnapshot() = before,
                  "Worker timeout waited the production ten seconds or changed the reviewed queue")
            ExpectCancelled(Sub() WaitUi(fixture.WaitWorkers(cancelled.Token, {pending.Task}, 1000)))
            Using during As New CancellationTokenSource()
                Dim waiting = fixture.WaitWorkers(during.Token, {pending.Task}, 1000)
                during.Cancel()
                ExpectCancelled(Sub() WaitUi(waiting))
            End Using
            Check(Not pending.Task.IsCompleted AndAlso fixture.QueueSnapshot() = before, "Cancelling worker draining modified a worker or recipient queue")
            pending.SetResult(True)
        End Using
    End Sub

    Private Sub TestForegroundWaits()
        ' Create only an unshown owned HWND. The injected foreground lookup never activates it.
        Using window As New Form With {.Text = "Owned offline Trade delay fixture", .ShowInTaskbar = False}
            Dim hwnd = window.Handle
            Dim pid = CUInt(Environment.ProcessId)
            Dim probes As Integer
            Dim lookup As Func(Of IntPtr) = Function()
                                                   probes += 1
                                                   Return hwnd
                                               End Function
            WaitUi(WaitForeground(hwnd, pid, 0, CancellationToken.None, lookup))
            Check(probes = 1, "Current-focus validation did not check the owned target")
            probes = 0
            Dim elapsed = Stopwatch.StartNew()
            WaitUi(WaitForeground(hwnd, pid, 110, CancellationToken.None, lookup))
            Check(probes >= 3 AndAlso elapsed.ElapsedMilliseconds >= 100, "Between-recipient delay did not validate foreground throughout the wait")
            probes = 0
            Dim changing As Func(Of IntPtr) = Function()
                                                     probes += 1
                                                     Return If(probes = 2, IntPtr.Zero, hwnd)
                                                 End Function
            ExpectInvalid(Sub() WaitUi(WaitForeground(hwnd, pid, 160, CancellationToken.None, changing)), "lost focus")
            Check(probes = 2, "A transient focus loss was tolerated after the target regained focus")
            WaitUi(WaitForeground(hwnd, pid, 0, CancellationToken.None, changing))
            Check(probes = 3, "Owned focus restoration fixture did not recover after the already-stopped wait")
            Dim active = IntPtr.Zero
            Dim restoreCalls As Integer
            Dim recoveryLookup As Func(Of IntPtr) = Function() active
            Dim restore As Func(Of IntPtr, UInteger, Boolean) =
                Function(target, expectedPid)
                    Check(target = hwnd AndAlso expectedPid = pid, "Between-row recovery changed its frozen window or PID")
                    restoreCalls += 1
                    If restoreCalls = 2 Then active = hwnd
                    Return active = hwnd
                End Function
            WaitUi(WaitForeground(hwnd, pid, 0, CancellationToken.None, recoveryLookup, restore))
            Check(restoreCalls = 2 AndAlso active = hwnd, "Between-row focus wait did not retain the queue through temporary restoration failure")
            active = IntPtr.Zero
            Dim beforeInvalidRestore = restoreCalls
            ExpectInvalid(Sub() WaitUi(WaitForeground(hwnd, pid + 1UI, 0, CancellationToken.None, recoveryLookup, restore)), "window")
            Check(restoreCalls = beforeInvalidRestore, "Changed PID reached a focus restoration callback")
            ExpectInvalid(Sub() WaitUi(WaitForeground(hwnd, pid + 1UI, 0, CancellationToken.None, lookup)), "window")
            ExpectInvalid(Sub() WaitUi(WaitForeground(IntPtr.Zero, pid, 0, CancellationToken.None, lookup)), "window")
            Using before As New CancellationTokenSource(), during As New CancellationTokenSource()
                before.Cancel()
                Dim initialProbes = probes
                ExpectCancelled(Sub() WaitUi(WaitForeground(hwnd, pid, 100, before.Token, lookup)))
                Check(probes = initialProbes, "A pre-cancelled delay checked focus or continued waiting")
                Dim cancelLookup As Func(Of IntPtr) = Function()
                                                             during.Cancel()
                                                             Return hwnd
                                                         End Function
                ExpectCancelled(Sub() WaitUi(WaitForeground(hwnd, pid, 100, during.Token, cancelLookup)))
            End Using
            Using stopRecovery As New CancellationTokenSource()
                Dim stopCalls As Integer
                Dim stopRestore As Func(Of IntPtr, UInteger, Boolean) =
                    Function(target, expectedPid)
                        stopCalls += 1
                        stopRecovery.Cancel()
                        Return False
                    End Function
                active = IntPtr.Zero
                ExpectCancelled(Sub() WaitUi(WaitForeground(hwnd, pid, 0, stopRecovery.Token, recoveryLookup, stopRestore)))
                Check(stopCalls = 1, "Stop during focus recovery permitted another restoration attempt")
            End Using
        End Using
    End Sub

    Private Sub TestSubmissionOwnership()
        Using fixture As New UiFixture(), owner As New CancellationTokenSource(), other As New CancellationTokenSource()
            fixture.SetField("_tradeRunning", True)
            fixture.SetField("_tradeCancellation", owner)
            fixture.SetField("_tradeAnalysisGeneration", 12)
            fixture.SetRows()
            Dim before = fixture.QueueSnapshot()
            Check(Not fixture.Submit("PuLgA", 11, owner) AndAlso fixture.QueueSnapshot() = before, "A previous source generation marked the current same-name row sent")
            Check(Not fixture.Submit("PuLgA", 12, other) AndAlso fixture.QueueSnapshot() = before, "A different queue owner marked a reviewed row sent")
            fixture.SetField("_tradeLoading", True)
            Check(Not fixture.Submit("PuLgA", 12, owner) AndAlso fixture.QueueSnapshot() = before, "Submission marked a row while profile/source loading")
            fixture.SetField("_tradeLoading", False)
            fixture.SetField("_tradeRunning", False)
            Check(Not fixture.Submit("PuLgA", 12, owner) AndAlso fixture.QueueSnapshot() = before, "Submission marked a row after its queue stopped")
            fixture.SetField("_tradeRunning", True)
            owner.Cancel()
            Check(fixture.Submit("PuLgA", 12, owner), "A physically submitted final Enter was ignored because its current owner was cancelled")
            Check(fixture.RowStatus(0) = "Sent to game" AndAlso Not fixture.RowSelected(0) AndAlso fixture.RowSelected(1) AndAlso fixture.RowStatus(1) = "Ready",
                  "Submission failed to uncheck only the exact-case recipient")
            fixture.SetRows()
            Check(fixture.Submit("PuLgA", 12, owner, True) AndAlso fixture.RowStatus(0) = "Sent; input stopped" AndAlso Not fixture.RowSelected(0),
                  "Input failure after final Enter hid the submitted row or kept it checked for duplicate retry")

            fixture.SetRows()
            DirectCast(fixture.Field("_tradeSource"), TextBox).Text = "Replacement source"
            fixture.SetRows()
            Dim sourceChanged = fixture.QueueSnapshot()
            Check(Not fixture.Submit("PuLgA", 12, owner) AndAlso fixture.QueueSnapshot() = sourceChanged,
                  "A source change allowed an old asynchronous submission to mark a recreated same-name row")
            Dim sourceGeneration = CInt(fixture.Field("_tradeAnalysisGeneration"))
            fixture.Invoke("ApplyPersistedTradeState", New TradeSettings With {.DiscordText = "Restored profile", .Recipients = New List(Of TradeRecipient) From {
                New TradeRecipient With {.Selected = True, .CharacterName = "PuLgA", .Items = "YY3", .Message = "restored reviewed whisper"}}})
            Dim restored = fixture.QueueSnapshot()
            Check(Not fixture.Submit("PuLgA", sourceGeneration, owner) AndAlso fixture.QueueSnapshot() = restored AndAlso fixture.RowSelected(0),
                  "Profile restoration allowed an old asynchronous submission to uncheck the restored same-name row")
        End Using
    End Sub

    Private Function NewEngine() As BotEngine
        Dim engine = DirectCast(Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(BotEngine)), BotEngine)
        SetEngineField(engine, "_sync", New Object())
        Return engine
    End Function

    Private Sub SetEngineField(engine As BotEngine, name As String, value As Object)
        GetType(BotEngine).GetField(name, PrivateInstance).SetValue(engine, value)
    End Sub

    Private Function WaitForeground(hwnd As IntPtr, pid As UInteger, milliseconds As Integer, cancellation As CancellationToken, foreground As Func(Of IntPtr),
                                    Optional restoreForeground As Func(Of IntPtr, UInteger, Boolean) = Nothing) As Task
        Return DirectCast(GetType(Form1).GetMethod("WaitForTradeForegroundAsync", PrivateStatic).Invoke(Nothing, {hwnd, pid, milliseconds, cancellation, foreground, restoreForeground}), Task)
    End Function

    Private Sub WaitUi(work As Task)
        Dim started = Stopwatch.StartNew()
        While Not work.IsCompleted AndAlso started.ElapsedMilliseconds < 3000
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        If Not work.IsCompleted Then Throw New TimeoutException("Owned offline Trade helper did not complete within three seconds")
        work.GetAwaiter().GetResult()
    End Sub

    Private Sub PumpFor(milliseconds As Integer)
        Dim started = Stopwatch.StartNew()
        While started.ElapsedMilliseconds < milliseconds
            Application.DoEvents()
            Thread.Sleep(1)
        End While
    End Sub

    Private Sub ExpectInvalid(work As Action, fragment As String)
        Dim rejected As Boolean
        Try
            work()
        Catch ex As InvalidOperationException
            rejected = ex.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase)
        End Try
        Check(rejected, "Trade guard did not report its expected window or worker-stop reason")
    End Sub

    Private Sub ExpectCancelled(work As Action)
        Dim rejected As Boolean
        Try
            work()
        Catch ex As OperationCanceledException
            rejected = True
        End Try
        Check(rejected, "Cancelled Trade wait continued or returned success")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New Exception(reason)
    End Sub

    Private NotInheritable Class UiFixture
        Implements IDisposable
        Private ReadOnly form As Object = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Private ReadOnly timer As New System.Windows.Forms.Timer()
        Private ReadOnly page As TabPage

        Public Sub New()
            ' Match the existing owned reflection fixtures; bypass real Form1 startup/settings/native input.
            Dim windowField = GetType(Control).GetField("_window", PrivateInstance)
            Dim constructor = windowField.FieldType.GetConstructor(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic, Nothing, {GetType(Control)}, Nothing)
            windowField.SetValue(form, constructor.Invoke({form}))
            SetField("_tradeStopTimer", timer)
            SetField("_applyingSettings", True)
            page = DirectCast(Invoke("BuildTradeTab"), TabPage)
        End Sub

        Public Function Invoke(name As String, ParamArray values As Object()) As Object
            Return GetType(Form1).GetMethod(name, PrivateInstance).Invoke(form, values)
        End Function
        Public Function Field(name As String) As Object
            Return GetType(Form1).GetField(name, PrivateInstance).GetValue(form)
        End Function
        Public Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, PrivateInstance).SetValue(form, value)
        End Sub
        Public Function WaitWorkers(cancellation As CancellationToken, workers As IEnumerable(Of Task), Optional timeoutMs As Integer = 10000) As Task
            Return DirectCast(Invoke("WaitForTradeInputWorkersAsync", cancellation, workers, timeoutMs), Task)
        End Function
        Public Function Submit(name As String, generation As Integer, owner As CancellationTokenSource, Optional failed As Boolean = False) As Boolean
            Return CBool(Invoke("ApplyTradeWhisperSubmission", name, generation, owner, failed))
        End Function
        Public Sub SetRows()
            Invoke("SetTradeRows", New List(Of TradeRecipient) From {
                New TradeRecipient With {.Selected = True, .CharacterName = "PuLgA", .Items = "YY3", .Message = "reviewed first whisper"},
                New TradeRecipient With {.Selected = True, .CharacterName = "pulga", .Items = "ROR", .Message = "reviewed case-distinct whisper"}})
        End Sub
        Private ReadOnly Property Grid As DataGridView
            Get
                Return DirectCast(Field("_tradeGrid"), DataGridView)
            End Get
        End Property
        Public Function RowSelected(index As Integer) As Boolean
            Return Object.Equals(Grid.Rows(index).Cells("Send").Value, True)
        End Function
        Public Function RowStatus(index As Integer) As String
            Return Convert.ToString(Grid.Rows(index).Cells("Status").Value)
        End Function
        Public Function QueueSnapshot() As String
            Return String.Join(vbLf, Grid.Rows.Cast(Of DataGridViewRow)().Where(Function(row) Not row.IsNewRow).
                Select(Function(row) String.Join("|", row.Cells.Cast(Of DataGridViewCell)().Select(Function(cell) Convert.ToString(cell.Value)))))
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            page.Dispose()
            timer.Dispose()
        End Sub
    End Class

    Private NotInheritable Class NoInput
        Implements IWindowsInput
        Public Calls As Integer
        Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
            Calls += 1
            Return False
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Calls += 1
            Return False
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Calls += 1
            Return False
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            Calls += 1
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            Calls += 1
        End Sub
    End Class
End Module
