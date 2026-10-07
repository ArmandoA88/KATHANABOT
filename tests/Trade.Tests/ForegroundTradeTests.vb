Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks

Module ForegroundTradeTests
    Private checks As Integer
    Private ReadOnly ForeignWindow As New IntPtr(&HF02456)

    Public Sub RunTests()
        Dim originalInput = WindowsInput.Current
        Dim originalMode = WindowsInput.InputMode
        Dim originalKeyMode = WindowsInput.BackgroundKeyMode
        Check(TypeOf originalInput Is ForegroundWindowsInput AndAlso WindowsInput.InputMode = "foreground" AndAlso Not WindowsInput.KeyboardOnlyMode,
              "Foreground Trade fixture must retain the foreground production backend")
        Using window As New Form With {.Text = "Owned offline foreground Trade fixture", .ShowInTaskbar = False,
                .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}
            window.Show()
            Application.DoEvents()
            TestExactTextAndActivation(window.Handle)
            TestWindowAndActivationGuards(window)
            TestFirstForegroundAcquisition(window.Handle)
            TestCancellationAndCommit(window.Handle)
            TestFocusLossDuringPacing(window.Handle)
            TestFocusChangeAfterEnterEmission(window.Handle)
            TestInputFailuresAndOwnedReleases(window.Handle)
            TestSequenceLock(window.Handle)
        End Using
        Check(Object.ReferenceEquals(WindowsInput.Current, originalInput) AndAlso WindowsInput.InputMode = originalMode AndAlso
              WindowsInput.BackgroundKeyMode = originalKeyMode AndAlso Not WindowsInput.KeyboardOnlyMode, "Foreground Trade switched the combat input object or legacy keyboard preference")
        Console.WriteLine($"PASS: {checks} offline dedicated foreground Trade assertions: exact UTF-16 text, activation/strict focus, HWND/PID/minimized guards, cancellation/commit, owned releases and combat backend isolation.")
    End Sub

    Private Sub TestFocusChangeAfterEnterEmission(hwnd As IntPtr)
        Dim opening As New TradePlatform(hwnd)
        opening.AfterSend =
            Sub(value)
                If IsKeyDown(value, Keys.Enter) Then
                    opening.Active = ForeignWindow
                ElseIf value.Keyboard AndAlso value.Key = CInt(Keys.Enter) AndAlso (value.Flags And 2UI) <> 0 Then
                    ' Focus returns while the owned Enter-up is released. The opening receipt
                    ' must still allow guarded Escape to close the known unfinished chat.
                    opening.Active = opening.Target
                End If
            End Sub
        Check(Not TradeService.SendForegroundWhisper(hwnd, opening.Pid, "PuLgA", "no partial command", CancellationToken.None, CreateInput(opening), delay:=AddressOf SkipDelay), "Opening Enter focus race did not stop Trade")
        Check(opening.Messages.Count = 0 AndAlso opening.CompletedCharacters = 0 AndAlso Not opening.ChatOpen AndAlso
              opening.Events.Any(Function(packet) IsKeyDown(packet.Value, Keys.Escape)) AndAlso opening.ActivationCalls = 1,
              "Confirmed opening Enter was forgotten after its focus result changed; unfinished chat was not safely closed")
        CheckOwnedReleases(opening)

        For Each restoreOnRelease In {False, True}
            Dim platform As New TradePlatform(hwnd)
            platform.AfterSend =
                Sub(value)
                    If IsKeyDown(value, Keys.Enter) AndAlso platform.Messages.Count = 1 Then
                        platform.Active = ForeignWindow
                    ElseIf restoreOnRelease AndAlso value.Keyboard AndAlso value.Key = CInt(Keys.Enter) AndAlso (value.Flags And 2UI) <> 0 AndAlso platform.Messages.Count = 1 Then
                        platform.Active = platform.Target
                    End If
                End Sub
            Dim submitted As Boolean
            Try
                TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "committed before focus changed", CancellationToken.None, CreateInput(platform), delay:=AddressOf SkipDelay)
            Catch ex As TradeWhisperSubmittedException
                submitted = True
            End Try
            Check(submitted AndAlso platform.Messages.SequenceEqual({"/whisper PuLgA committed before focus changed"}) AndAlso Not platform.ChatOpen AndAlso platform.ActivationCalls = 1,
                  "After-submit focus race returned unsent or continued instead of preserving the submitted row and stopping")
            Check(Not platform.Events.Any(Function(packet) IsKeyDown(packet.Value, Keys.Escape)), "Committed focus race sent Escape or attempted to reopen completed chat")
            CheckOwnedReleases(platform)
        Next

        Dim legacy As New TradePlatform(hwnd)
        Dim legacyInput = CreateInput(legacy)
        legacyInput.Activate(hwnd)
        legacy.AfterSend = Sub(value) legacy.Active = ForeignWindow
        Check(Not legacyInput.Post(hwnd, &H100UI, New IntPtr(CInt(Keys.Enter)), New IntPtr(1)), "Narrow Trade receipt edit changed legacy Post's final focus-result semantics")
        legacyInput.ReleaseTarget(hwnd)
        CheckOwnedReleases(legacy)
    End Sub

    Private Sub TestExactTextAndActivation(hwnd As IntPtr)
        Dim platform As New TradePlatform(hwnd)
        Dim input = CreateInput(platform)
        Dim name = "ÉMiXeD"
        Dim message = "Hi! ROR +9? Price €1.5 — 😊."
        Dim command = TradeService.BuildWhisper(name, message)
        Dim pauses As New List(Of Integer)()
        Dim delay As Action(Of Integer, CancellationToken) = Sub(milliseconds, cancellation) pauses.Add(milliseconds)
        Check(TradeService.SendForegroundWhisper(hwnd, platform.Pid, name, message, CancellationToken.None, input, delay:=delay), "Explicit foreground Trade did not deliver the reviewed command")
        Check(platform.ActivationCalls = 1 AndAlso platform.Messages.SequenceEqual({command}) AndAlso Not platform.ChatOpen, "Foreground activation or Enter/text/Enter lost exact Unicode, case or punctuation")
        Check(platform.Events.Where(Function(packet) IsKeyDown(packet.Value, Keys.Enter)).Count() = 2 AndAlso
              platform.Events.Where(Function(packet) IsUnicodeDown(packet.Value)).Count() = command.Length AndAlso pauses.Where(Function(milliseconds) milliseconds = 150).Count() = 2,
              "Foreground command did not use two separate Enter holds and one Unicode pair per UTF-16 unit")
        Check(TradeService.SendForegroundWhisper(hwnd, platform.Pid, "Second", "Hi", CancellationToken.None, input, activateTarget:=False, delay:=AddressOf SkipDelay), "Second foreground recipient failed with activation disabled")
        Check(platform.ActivationCalls = 1 AndAlso platform.Messages.Last() = "/whisper Second Hi", "Later recipient activated a window or changed text")
        Dim before = platform.Events.Count
        platform.Active = ForeignWindow
        ExpectInvalid(Sub() TradeService.SendForegroundWhisper(hwnd, platform.Pid, "Third", "must stop", CancellationToken.None, input, activateTarget:=False, delay:=AddressOf SkipDelay), "lost focus")
        Check(platform.Events.Count = before AndAlso platform.ActivationCalls = 1, "Later recipient reclaimed focus or emitted input into another app")
        CheckOwnedReleases(platform)
    End Sub

    Private Sub TestWindowAndActivationGuards(window As Form)
        Dim platform As New TradePlatform(window.Handle) With {.AllowActivation = False}
        Dim input = CreateInput(platform)
        ExpectInvalid(Sub() TradeService.SendForegroundWhisper(window.Handle, platform.Pid, "PuLgA", "Hi", CancellationToken.None, input, delay:=AddressOf SkipDelay, foregroundTimeoutMs:=0), "foreground")
        Check(platform.ActivationCalls = 1 AndAlso platform.Events.Count = 0, "Failed activation opened chat or sent characters")
        platform.AllowActivation = True
        ExpectInvalid(Sub() TradeService.SendForegroundWhisper(window.Handle, platform.Pid + 1UI, "PuLgA", "Hi", CancellationToken.None, input, delay:=AddressOf SkipDelay), "window")
        ExpectInvalid(Sub() TradeService.SendForegroundWhisper(IntPtr.Zero, platform.Pid, "PuLgA", "Hi", CancellationToken.None, input, delay:=AddressOf SkipDelay), "window")
        Check(platform.ActivationCalls = 1 AndAlso platform.Events.Count = 0, "Stale PID or invalid HWND reached foreground activation/input")
        window.WindowState = FormWindowState.Minimized
        Try
            ExpectInvalid(Sub() TradeService.SendForegroundWhisper(window.Handle, platform.Pid, "PuLgA", "Hi", CancellationToken.None, input, delay:=AddressOf SkipDelay), "minimized")
            Check(platform.Events.Count = 0 AndAlso platform.ActivationCalls = 1, "Minimized target received foreground Trade input")
        Finally
            window.WindowState = FormWindowState.Normal
        End Try
        Using closed As New Form()
            Dim closedHwnd = closed.Handle
            closed.Dispose()
            ExpectInvalid(Sub() TradeService.SendForegroundWhisper(closedHwnd, platform.Pid, "PuLgA", "Hi", CancellationToken.None, input, delay:=AddressOf SkipDelay), "window")
        End Using
        Check(platform.Events.Count = 0, "Closed target received foreground Trade input")
    End Sub

    Private Sub TestFirstForegroundAcquisition(hwnd As IntPtr)
        Dim focused As New TradePlatform(hwnd) With {.Active = hwnd, .AllowActivation = False}
        Check(TradeService.SendForegroundWhisper(hwnd, focused.Pid, "PuLgA", "already focused", CancellationToken.None, CreateInput(focused), delay:=AddressOf SkipDelay),
              "Already foreground target was rejected because activation would be denied")
        Check(focused.ActivationCalls = 0 AndAlso focused.Messages.Single() = "/whisper PuLgA already focused", "Already focused target was unnecessarily activated")
        CheckOwnedReleases(focused)

        Dim settling As New TradePlatform(hwnd) With {.AllowActivation = False}
        Dim acquisitionPauses As Integer
        Dim delayedFocus As Action(Of Integer, CancellationToken) =
            Sub(milliseconds, token)
                If milliseconds <> 50 Then Return
                acquisitionPauses += 1
                Check(settling.Events.Count = 0, "First-focus wait emitted input before the exact target gained focus")
                If acquisitionPauses = 2 Then settling.Active = hwnd
            End Sub
        Check(TradeService.SendForegroundWhisper(hwnd, settling.Pid, "PuLgA", "focus settled", CancellationToken.None, CreateInput(settling), delay:=delayedFocus, foregroundTimeoutMs:=300),
              "Delayed activation or a user's foreground click was rejected before focus could settle")
        Check(acquisitionPauses = 2 AndAlso settling.ActivationCalls = 1 AndAlso settling.Messages.Single() = "/whisper PuLgA focus settled", "Acquisition retried activation or changed the reviewed command")
        CheckOwnedReleases(settling)

        Dim denied As New TradePlatform(hwnd) With {.AllowActivation = False}
        Dim boundedPauses As Integer
        Dim noFocus As Action(Of Integer, CancellationToken) =
            Sub(milliseconds, token)
                boundedPauses += 1
                Check(denied.Events.Count = 0, "Denied acquisition emitted input into another app")
            End Sub
        ExpectInvalid(Sub() TradeService.SendForegroundWhisper(hwnd, denied.Pid, "PuLgA", "must not send", CancellationToken.None, CreateInput(denied), delay:=noFocus, foregroundTimeoutMs:=150), "foreground")
        Check(boundedPauses = 3 AndAlso denied.ActivationCalls = 1 AndAlso denied.Events.Count = 0, "First-focus wait was unbounded or repeatedly reclaimed focus")

        Using cancellation As New CancellationTokenSource()
            Dim cancelled As New TradePlatform(hwnd) With {.AllowActivation = False}
            Dim stopWaiting As Action(Of Integer, CancellationToken) = Sub(milliseconds, token) cancellation.Cancel()
            ExpectCancelled(Sub() TradeService.SendForegroundWhisper(hwnd, cancelled.Pid, "PuLgA", "must not send", cancellation.Token, CreateInput(cancelled), delay:=stopWaiting, foregroundTimeoutMs:=1000))
            Check(cancelled.ActivationCalls = 1 AndAlso cancelled.Events.Count = 0, "Stop during acquisition activated again or opened chat")
        End Using
    End Sub

    Private Sub TestCancellationAndCommit(hwnd As IntPtr)
        Using cancellation As New CancellationTokenSource()
            Dim platform As New TradePlatform(hwnd)
            Dim input = CreateInput(platform)
            cancellation.Cancel()
            ExpectCancelled(Sub() TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "Hi", cancellation.Token, input, delay:=AddressOf SkipDelay))
            Check(platform.ActivationCalls = 0 AndAlso platform.Events.Count = 0, "Pre-cancelled Trade activated a window or sent input")
        End Using
        Using cancellation As New CancellationTokenSource()
            Dim platform As New TradePlatform(hwnd)
            Dim input = CreateInput(platform)
            Dim delay As Action(Of Integer, CancellationToken) =
                Sub(milliseconds, token)
                    If milliseconds = 150 AndAlso token.CanBeCanceled Then cancellation.Cancel()
                End Sub
            ExpectCancelled(Sub() TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "Hi", cancellation.Token, input, delay:=delay))
            Check(platform.Messages.Count = 0 AndAlso Not platform.ChatOpen AndAlso platform.Events.Any(Function(packet) IsKeyDown(packet.Value, Keys.Escape)), "Cancellation during opening Enter left chat open or submitted a draft")
            CheckOwnedReleases(platform)
        End Using
        Using cancellation As New CancellationTokenSource()
            Dim platform As New TradePlatform(hwnd)
            platform.AfterSend =
                Sub(value)
                    If IsUnicodeUp(value) AndAlso platform.CompletedCharacters = 5 Then cancellation.Cancel()
                End Sub
            Dim input = CreateInput(platform)
            ExpectCancelled(Sub() TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "must not submit a partial message", cancellation.Token, input, delay:=AddressOf SkipDelay))
            Check(platform.Messages.Count = 0 AndAlso Not platform.ChatOpen AndAlso platform.CompletedCharacters = 5, "Mid-text cancellation submitted partial text or continued characters")
            CheckOwnedReleases(platform)
        End Using
        Using cancellation As New CancellationTokenSource()
            Dim platform As New TradePlatform(hwnd)
            platform.AfterSend =
                Sub(value)
                    If IsKeyDown(value, Keys.Enter) AndAlso platform.Messages.Count = 1 Then cancellation.Cancel()
                End Sub
            Dim input = CreateInput(platform)
            Check(TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "complete command", cancellation.Token, input, delay:=AddressOf SkipDelay) AndAlso cancellation.IsCancellationRequested,
                  "Stop after submit Enter did not report the completed row as sent")
            Check(platform.Messages.SequenceEqual({"/whisper PuLgA complete command"}) AndAlso Not platform.ChatOpen, "Post-submit cancellation duplicated or changed the complete command")
            CheckOwnedReleases(platform)
        End Using
    End Sub

    Private Sub TestFocusLossDuringPacing(hwnd As IntPtr)
        Dim platform As New TradePlatform(hwnd)
        Dim input As New ForegroundWindowsInput(platform,
            Sub(milliseconds)
                If platform.CompletedCharacters = 4 Then platform.Active = ForeignWindow
            End Sub)
        Check(Not TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "focus must stop", CancellationToken.None, input, delay:=AddressOf SkipDelay), "Foreground Trade ignored focus loss during key-down pacing")
        Check(platform.CompletedCharacters = 4 AndAlso platform.Messages.Count = 0 AndAlso platform.ActivationCalls = 1 AndAlso
              Not platform.Events.Any(Function(packet) IsKeyDown(packet.Value, Keys.Escape)), "Focus loss continued text, submitted a partial command, reactivated the game or sent Escape elsewhere")
        CheckOwnedReleases(platform)
    End Sub

    Private Sub TestInputFailuresAndOwnedReleases(hwnd As IntPtr)
        Dim textFailure As New TradePlatform(hwnd) With {.RejectUnicodeDownNumber = 5}
        Check(Not TradeService.SendForegroundWhisper(hwnd, textFailure.Pid, "PuLgA", "failure must stop", CancellationToken.None, CreateInput(textFailure), delay:=AddressOf SkipDelay), "Failed Unicode send was reported as success")
        Check(textFailure.Messages.Count = 0 AndAlso Not textFailure.ChatOpen AndAlso textFailure.CompletedCharacters = 4, "Failed Unicode send submitted partial text or left foreground chat open")
        CheckOwnedReleases(textFailure)
        Dim releaseFailure As New TradePlatform(hwnd) With {.RejectEnterUps = 1}
        Check(Not TradeService.SendForegroundWhisper(hwnd, releaseFailure.Pid, "PuLgA", "Hi", CancellationToken.None, CreateInput(releaseFailure), delay:=AddressOf SkipDelay), "Failed opening Enter release was reported as success")
        Check(releaseFailure.Messages.Count = 0 AndAlso Not releaseFailure.ChatOpen AndAlso releaseFailure.EnterUpAttempts = 2, "Finally did not retry the owned Enter release after a failed up")
        CheckOwnedReleases(releaseFailure)
        Dim unicodeReleaseFailure As New TradePlatform(hwnd) With {.RejectUnicodeUps = 1}
        Check(Not TradeService.SendForegroundWhisper(hwnd, unicodeReleaseFailure.Pid, "PuLgA", "Hi", CancellationToken.None, CreateInput(unicodeReleaseFailure), delay:=AddressOf SkipDelay), "Failed Unicode release was reported as success")
        Check(unicodeReleaseFailure.Messages.Count = 0 AndAlso Not unicodeReleaseFailure.ChatOpen, "Unicode release failure submitted an unfinished command")
        CheckOwnedReleases(unicodeReleaseFailure)
        Dim submittedReleaseRetry As New TradePlatform(hwnd) With {.RejectSubmittedEnterUps = 1}
        Check(TradeService.SendForegroundWhisper(hwnd, submittedReleaseRetry.Pid, "PuLgA", "submit once", CancellationToken.None, CreateInput(submittedReleaseRetry), delay:=AddressOf SkipDelay), "Successful final Enter release retry did not preserve the submitted row result")
        Check(submittedReleaseRetry.Messages.SequenceEqual({"/whisper PuLgA submit once"}) AndAlso submittedReleaseRetry.EnterUpAttempts = 3, "Final release retry resubmitted text or omitted the owned key-up retry")
        CheckOwnedReleases(submittedReleaseRetry)
        Dim submittedReleaseFailure As New TradePlatform(hwnd) With {.RejectSubmittedEnterUps = 3}
        Dim failedInput = CreateInput(submittedReleaseFailure)
        Dim committedFailure As Boolean
        Try
            TradeService.SendForegroundWhisper(hwnd, submittedReleaseFailure.Pid, "PuLgA", "already submitted", CancellationToken.None, failedInput, delay:=AddressOf SkipDelay)
        Catch ex As TradeWhisperSubmittedException
            committedFailure = True
        Finally
            submittedReleaseFailure.RejectSubmittedEnterUps = 0
            failedInput.ReleaseTarget(hwnd)
        End Try
        Check(committedFailure AndAlso submittedReleaseFailure.Messages.SequenceEqual({"/whisper PuLgA already submitted"}) AndAlso Not submittedReleaseFailure.ChatOpen,
              "Persistent final release failure hid the submitted row or sent Escape/duplicate text")
        CheckOwnedReleases(submittedReleaseFailure)
    End Sub

    Private Sub TestSequenceLock(hwnd As IntPtr)
        Dim platform As New TradePlatform(hwnd)
        Dim input = CreateInput(platform)
        Using opened As New ManualResetEventSlim(), release As New ManualResetEventSlim(), attempted As New ManualResetEventSlim(), acquired As New ManualResetEventSlim()
            Dim delay As Action(Of Integer, CancellationToken) =
                Sub(milliseconds, cancellation)
                    If milliseconds = 150 AndAlso platform.Messages.Count = 0 Then
                        opened.Set()
                        If Not release.Wait(3000) Then Throw New TimeoutException("Offline Trade lock fixture did not release")
                    End If
                End Sub
            Dim whisper = Task.Run(Function() TradeService.SendForegroundWhisper(hwnd, platform.Pid, "PuLgA", "serialized", CancellationToken.None, input, delay:=delay))
            Dim competitor As Task = Nothing
            Try
                Check(opened.Wait(3000), "Offline Trade did not enter its complete-message input scope")
                competitor = Task.Run(Sub()
                                          attempted.Set()
                                          SyncLock WindowsInput.SequenceLock
                                              acquired.Set()
                                          End SyncLock
                                      End Sub)
                Check(attempted.Wait(3000) AndAlso Not acquired.Wait(50), "Background input scope interleaved while foreground Trade chat was open")
            Finally
                release.Set()
            End Try
            Check(whisper.Wait(3000) AndAlso whisper.Result AndAlso competitor IsNot Nothing AndAlso competitor.Wait(3000) AndAlso acquired.IsSet, "Complete-message lock did not release after the whisper")
        End Using
        Check(platform.Messages.SequenceEqual({"/whisper PuLgA serialized"}), "Serialized Trade scope altered or duplicated the command")
        CheckOwnedReleases(platform)
    End Sub

    Private Function CreateInput(platform As TradePlatform) As ForegroundWindowsInput
        Return New ForegroundWindowsInput(platform,
            Sub(milliseconds)
                ' Fake platform input has no physical pacing or delivery.
            End Sub)
    End Function

    Private Sub SkipDelay(milliseconds As Integer, cancellation As CancellationToken)
        ' All actual key delivery is recorded by the fake platform; this never sleeps or injects input.
    End Sub

    Private Sub ExpectCancelled(work As Action)
        Dim rejected As Boolean
        Try
            work()
        Catch ex As OperationCanceledException
            rejected = True
        End Try
        Check(rejected, "Cancelled foreground Trade continued or returned a sent result")
    End Sub

    Private Sub ExpectInvalid(work As Action, text As String)
        Dim rejected As Boolean
        Try
            work()
        Catch ex As InvalidOperationException
            rejected = ex.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
        End Try
        Check(rejected, "Foreground Trade did not explain its window/focus/activation guard")
    End Sub

    Private Function IsKeyDown(value As ForegroundInputEvent, key As Keys) As Boolean
        Return value.Keyboard AndAlso value.Key = CUShort(key) AndAlso (value.Flags And (2UI Or 4UI)) = 0
    End Function

    Private Function IsUnicodeDown(value As ForegroundInputEvent) As Boolean
        Return value.Keyboard AndAlso (value.Flags And (2UI Or 4UI)) = 4UI
    End Function

    Private Function IsUnicodeUp(value As ForegroundInputEvent) As Boolean
        Return value.Keyboard AndAlso (value.Flags And (2UI Or 4UI)) = 6UI
    End Function

    Private Sub CheckOwnedReleases(platform As TradePlatform)
        Dim balance As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
        For Each packet In platform.Events
            Dim value = packet.Value
            Check(value.Keyboard, "Foreground Trade emitted mouse input")
            Dim token = If((value.Flags And 4UI) <> 0, "unicode:" & value.Scan.ToString(), "key:" & value.Key.ToString())
            Dim amount As Integer
            balance.TryGetValue(token, amount)
            balance(token) = amount + If((value.Flags And 2UI) <> 0, -1, 1)
            Check((value.Flags And 2UI) <> 0 OrElse packet.ActiveWindow = platform.Target, "Foreground Trade sent a new key/character down into another app")
        Next
        Check(balance.Values.All(Function(amount) amount = 0), "Foreground Trade left an owned key or Unicode unit held")
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Class RecordedPacket
        Public Property Value As ForegroundInputEvent
        Public Property ActiveWindow As IntPtr
    End Class

    Private Class TradePlatform
        Inherits InputPlatform
        Public ReadOnly Target As IntPtr
        Public ReadOnly Pid As UInteger = CUInt(Environment.ProcessId)
        Public Active As IntPtr = ForeignWindow
        Public AllowActivation As Boolean = True
        Public ActivationCalls As Integer
        Public ChatOpen As Boolean
        Public CompletedCharacters As Integer
        Public UnicodeDownAttempts As Integer
        Public EnterUpAttempts As Integer
        Public RejectUnicodeDownNumber As Integer
        Public RejectEnterUps As Integer
        Public RejectSubmittedEnterUps As Integer
        Public RejectUnicodeUps As Integer
        Public AfterSend As Action(Of ForegroundInputEvent)
        Public ReadOnly Events As New List(Of RecordedPacket)()
        Public ReadOnly Messages As New List(Of String)()
        Private buffer As String = ""
        Public Sub New(hwnd As IntPtr)
            Target = hwnd
        End Sub
        Public Overrides Function Foreground() As IntPtr
            Return Active
        End Function
        Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
            Return If(hwnd = Target, Pid, Pid + 1UI)
        End Function
        Public Overrides Function Valid(hwnd As IntPtr) As Boolean
            Return hwnd = Target AndAlso hwnd <> IntPtr.Zero
        End Function
        Public Overrides Function Activate(hwnd As IntPtr) As Boolean
            ActivationCalls += 1
            If AllowActivation Then Active = hwnd
            Return AllowActivation
        End Function
        Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Point?
            Throw New InvalidOperationException("Trade must not convert mouse points")
        End Function
        Public Overrides Function CursorOnTarget(hwnd As IntPtr) As Boolean
            Throw New InvalidOperationException("Trade must not inspect the cursor")
        End Function
        Public Overrides Function Desktop() As Rectangle
            Throw New InvalidOperationException("Trade must not move the cursor")
        End Function
        Public Overrides Function Send(value As ForegroundInputEvent) As Boolean
            If IsUnicodeDown(value) Then
                UnicodeDownAttempts += 1
                If UnicodeDownAttempts = RejectUnicodeDownNumber Then Return False
            ElseIf value.Keyboard AndAlso value.Key = CInt(Keys.Enter) AndAlso (value.Flags And 2UI) <> 0 Then
                EnterUpAttempts += 1
                If Messages.Count > 0 AndAlso RejectSubmittedEnterUps > 0 Then
                    RejectSubmittedEnterUps -= 1
                    Return False
                End If
                If RejectEnterUps > 0 Then
                    RejectEnterUps -= 1
                    Return False
                End If
            ElseIf IsUnicodeUp(value) AndAlso RejectUnicodeUps > 0 Then
                RejectUnicodeUps -= 1
                Return False
            End If
            Events.Add(New RecordedPacket With {.Value = value, .ActiveWindow = Active})
            If Active = Target Then
                If IsUnicodeUp(value) Then
                    CompletedCharacters += 1
                    If ChatOpen Then buffer &= ChrW(value.Scan)
                ElseIf IsKeyDown(value, Keys.Enter) Then
                    If ChatOpen Then Messages.Add(buffer)
                    ChatOpen = Not ChatOpen
                    buffer = ""
                ElseIf IsKeyDown(value, Keys.Escape) Then
                    ChatOpen = False
                    buffer = ""
                End If
            End If
            AfterSend?.Invoke(value)
            Return True
        End Function
    End Class
End Module
