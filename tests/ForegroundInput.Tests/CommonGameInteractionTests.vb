Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks

' Only FakePlatform receives the requests in this fixture. No window is activated,
' clipboard is changed, or native SendInput call is made.
Friend Module CommonGameInteractionTests
    Private assertions As Integer
    Private ReadOnly Game As New IntPtr(31001)
    Private ReadOnly Other As New IntPtr(31002)

    Friend Function Verify() As Integer
        assertions = 0
        Dim original = WindowsInput.Current
        WindowsInput.Current = Nothing
        Try
            Check(WindowsInput.IsProductionForegroundInput AndAlso TypeOf WindowsInput.Current Is ForegroundWindowsInput AndAlso WindowsInput.InputMode = "foreground" AndAlso
                  Not WindowsInput.KeyboardOnlyMode AndAlso Not WindowsInput.UsesTargetedInput AndAlso Not WindowsInput.UsesInternalHook,
                  "production default must use foreground SendInput for keyboard, text and mouse")
            Check(Not BotEngine.SendKey(Game, " F12 ", 5000), "production F12 is reserved for Stop and must not send a gameplay key")
            Dim f12Blocked As Boolean
            Try
                WindowsInput.Current.Keyboard(&H7B, 0, 0UI, UIntPtr.Zero)
            Catch ex As InvalidOperationException
                f12Blocked = ex.Message.Contains("F12", StringComparison.OrdinalIgnoreCase)
            End Try
            Check(f12Blocked, "raw production F12 down must be rejected before any target or native input query")
            Dim production = WindowsInput.Current
            For Each mode In [Enum].GetValues(Of BackgroundKeyboardMode)()
                Check(Not WindowsInput.TrySetBackgroundKeyMode(mode) AndAlso Object.ReferenceEquals(WindowsInput.Current, production),
                      "legacy method preferences must not reactivate a background backend")
            Next

            Dim platform As New SelectedGamePlatform With {.Active = Game}
            Dim input As New ForegroundWindowsInput(platform, Sub(milliseconds) Return, allowActivation:=False)
            WindowsInput.Current = input
            Check(Not WindowsInput.IsProductionForegroundInput, "owned injected input must not impersonate the production backend")
            VerifyKeysAndLegacyFlags(platform)
            VerifyNativeWrappers(platform)
            VerifyChatAndClicks(platform)
            VerifyFocusAndFailures(platform, input)
            VerifyOwnedCleanup(platform)
            VerifyExplicitFocusMaintenance(platform, input)
            VerifyPendingReleaseRecovery(platform, input)
        Finally
            WindowsInput.ReleaseAll()
            WindowsInput.Current = original
        End Try
        Check(Object.ReferenceEquals(WindowsInput.Current, original), "common helper fixture must restore the production backend")
        Return assertions
    End Function

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New InvalidOperationException("Common foreground interactions: " & message)
        assertions += 1
    End Sub

    Private Sub VerifyKeysAndLegacyFlags(platform As FakePlatform)
        ' Shared key helpers used by combat, post-kill pickup, navigation and RESU.
        For Each sample In {("W", 87US), ("F", 70US), ("I", 73US), ("E", 69US), ("F1", 112US), ("F12", 123US), ("RALT", 165US)}
            platform.Events.Clear()
            Check(BotEngine.SendKey(Game, sample.Item1, 12, forceBackgroundPost:=True), sample.Item1 & " legacy flag still routes through foreground")
            CheckKeyPair(platform.Events, sample.Item2, sample.Item1)
        Next
        For Each flags In {(False, False), (True, False), (False, True), (True, True)}
            platform.Events.Clear()
            Check(BotEngine.SendKey(Game, "1", 12, forceBackgroundPost:=flags.Item1, forcePhysicalKeyEvent:=flags.Item2),
                  "all legacy key flags share the selected foreground backend")
            CheckKeyPair(platform.Events, 49US, "legacy flags")
        Next
        For Each sample In {("CTRL+1", 17US, 49US), ("ALT+2", 18US, 50US)}
            platform.Events.Clear()
            Check(BotEngine.SendKey(Game, sample.Item1, 12), sample.Item1 & " shared chord succeeds")
            Check(platform.Events.Select(Function(value) value.Key).SequenceEqual({sample.Item2, sample.Item3, sample.Item3, sample.Item2}),
                  sample.Item1 & " foreground chord releases the digit before its modifier")
            CheckKeyPair(platform.Events.Where(Function(value) value.Key = sample.Item2), sample.Item2, sample.Item1 & " modifier")
            CheckKeyPair(platform.Events.Where(Function(value) value.Key = sample.Item3), sample.Item3, sample.Item1 & " digit")
        Next
        Check(platform.ActivateCalls = 0, "foreground key helpers must not steal focus")
        platform.Events.Clear()
        Using cancellation As New CancellationTokenSource
            cancellation.Cancel()
            Check(Not BotEngine.SendKey(Game, "F", 5000, cancellationToken:=cancellation.Token) AndAlso platform.Events.Count = 0,
                  "pre-cancelled pickup produces no key event")
        End Using
    End Sub

    Private Sub CheckKeyPair(events As IEnumerable(Of ForegroundInputEvent), key As UShort, context As String)
        Dim pair = events.ToArray()
        Check(pair.Length = 2 AndAlso pair.All(Function(value) value.Keyboard AndAlso value.Key = key) AndAlso
              (pair(0).Flags And 2UI) = 0 AndAlso pair(1).Flags = (pair(0).Flags Or 2UI), context & " uses an owned foreground down/up pair")
    End Sub

    Private Function Native(name As String, ParamArray arguments As Object()) As Object
        Dim nativeType = GetType(WindowsInput).Assembly.GetType("KathanaBotControlPanel.NativeMethods", throwOnError:=True)
        Dim method = nativeType.GetMethod(name, BindingFlags.Static Or BindingFlags.Public Or BindingFlags.NonPublic)
        If method Is Nothing Then Throw New MissingMethodException(nativeType.FullName, name)
        Return method.Invoke(Nothing, arguments)
    End Function

    Private Sub VerifyNativeWrappers(platform As FakePlatform)
        platform.Events.Clear()
        WindowsInput.BindTarget(Game)
        Check(CBool(Native("SetForegroundWindow", Game)) AndAlso platform.ActivateCalls = 0,
              "activation compatibility wrapper only accepts the existing foreground target")
        Check(CBool(Native("MoveCursorInput", 40, 60)), "quiz/navigation cursor wrapper uses the selected foreground backend")
        Native("SendMouseInput", 2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        Native("SendMouseInput", 4UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        Check(platform.Events.Count = 3 AndAlso platform.Events.Select(Function(value) value.Flags).SequenceEqual({&HC001UI, 2UI, 4UI}) AndAlso
              platform.Events.All(Function(value) Not value.Keyboard), "quiz/mouse compatibility wrappers emit foreground move and owned click events")
        platform.Events.Clear()
        Native("SendKeyboardInput", CByte(65), CByte(30), 0UI, UIntPtr.Zero)
        Native("SendKeyboardInput", CByte(65), CByte(30), 2UI, UIntPtr.Zero)
        CheckKeyPair(platform.Events, 65US, "raw key wrapper")
        platform.Events.Clear()
        Check(CBool(Native("SendForegroundInputRequest", Game, &H102UI, New IntPtr(&H20AC), IntPtr.Zero)),
              "message-shaped Unicode wrapper uses foreground events")
        Check(platform.Events.Select(Function(value) value.Flags).SequenceEqual({4UI, 6UI}) AndAlso
              platform.Events.All(Function(value) value.Keyboard AndAlso value.Key = 0 AndAlso value.Scan = &H20AC),
              "compatibility character dispatch becomes an exact UTF-16 SendInput pair")
    End Sub

    Private Sub VerifyChatAndClicks(platform As FakePlatform)
        platform.Events.Clear()
        Dim text = "A" & ChrW(&HE9) & ChrW(&H2603)
        Check(BotEngine.SendChatMessageSequence(Game, text), "shared RESU/party chat sender completes foreground text")
        Dim characters = platform.Events.Where(Function(value) value.Keyboard AndAlso (value.Flags And 4UI) <> 0).ToArray()
        Check(characters.Length = text.Length * 2 AndAlso characters.Where(Function(value) (value.Flags And 2UI) = 0).
              Select(Function(value) ChrW(value.Scan)).SequenceEqual(text), "shared chat preserves exact Unicode and case")
        Check(platform.Events.Where(Function(value) value.Keyboard AndAlso value.Key = 13).
              Select(Function(value) value.Flags).SequenceEqual({0UI, 2UI, 0UI, 2UI}), "chat opens and submits with complete foreground Enter pairs")

        platform.Events.Clear()
        Check(BotEngine.ClickClientPoint(Game, 25, 35, moveDelayMs:=0, downUpDelayMs:=0), "shared RESU/loot click helper works in foreground mode")
        Check(platform.Events.Count = 4 AndAlso platform.Events.Select(Function(value) value.Flags).SequenceEqual({&HC001UI, &HC001UI, 2UI, 4UI}) AndAlso
              platform.Events.All(Function(value) Not value.Keyboard), "client click translates movement and button requests into foreground events")
        Check(platform.ActivateCalls = 0, "chat and click helpers must not activate another window")
    End Sub

    Private Sub VerifyFocusAndFailures(platform As FakePlatform, input As ForegroundWindowsInput)
        platform.Events.Clear()
        platform.Active = Other
        Dim attempts = platform.SendAttempts
        Check(Not input.Activate(Game) AndAlso platform.ActivateCalls = 0, "strict foreground activation cannot steal focus")
        Check(Not BotEngine.SendKey(Game, "F", 12) AndAlso Not BotEngine.ClickClientPoint(Game, 25, 35),
              "off-focus shared keyboard and click helpers stop")
        Check(Not input.Post(Game, &H102UI, New IntPtr(65), IntPtr.Zero) AndAlso Not input.MoveCursor(40, 60),
              "off-focus direct Unicode and cursor helpers stop")
        Check(Not CBool(Native("BringWindowToTop", Game)), "automatic foreground bring-to-top wrapper cannot reclaim focus")
        Check(Not CBool(Native("ShowWindow", Game, 9)), "automatic foreground restore wrapper cannot reclaim focus")
        Dim forceForeground = GetType(Form1).GetMethod("ForceSetForegroundWindow", BindingFlags.Static Or BindingFlags.NonPublic)
        If forceForeground Is Nothing Then Throw New MissingMethodException(GetType(Form1).FullName, "ForceSetForegroundWindow")
        forceForeground.Invoke(Nothing, New Object() {Game})
        Check(platform.Active = Other AndAlso platform.Events.Count = 0 AndAlso platform.SendAttempts = attempts AndAlso platform.ActivateCalls = 0,
              "automatic quiz force-foreground helper exits without emitting input or activating a window")
        Dim diagnostic As String = ""
        Check(Not BotEngine.LeftClickVerifiedAtClientPoint(Game, 25, 35, diagnostic) AndAlso
              diagnostic.Contains("foreground", StringComparison.OrdinalIgnoreCase),
              "off-focus verified left click must fail before native cursor or client-point queries")
        diagnostic = ""
        Check(Not BotEngine.DoubleRightClickVerifiedAtClientPoint(Game, 25, 35, diagnostic) AndAlso
              diagnostic.Contains("foreground", StringComparison.OrdinalIgnoreCase),
              "off-focus verified double right click must fail before native cursor or client-point queries")
        Check(platform.Events.Count = 0 AndAlso platform.SendAttempts = attempts AndAlso platform.ActivateCalls = 0,
              "off-focus operations produce neither new input nor focus changes")

        platform.Active = Game
        platform.Covered = True
        Check(Not BotEngine.ClickClientPoint(Game, 25, 35, moveDelayMs:=0, downUpDelayMs:=0) AndAlso
              Not platform.Events.Any(Function(value) Not value.Keyboard AndAlso value.Flags = 2UI),
              "covered client point cannot receive a foreground mouse down")
        platform.Covered = False
        platform.Events.Clear()
        platform.Accept = False
        attempts = platform.SendAttempts
        Check(Not BotEngine.SendKey(Game, "F", 12) AndAlso platform.SendAttempts = attempts + 1 AndAlso platform.Events.Count = 0,
              "failed SendInput key down stops without a second transport")
        attempts = platform.SendAttempts
        Check(Not BotEngine.ClickClientPoint(Game, 25, 35, moveDelayMs:=0, downUpDelayMs:=0) AndAlso
              platform.SendAttempts = attempts + 1 AndAlso platform.Events.Count = 0,
              "failed SendInput cursor move stops without message/mouse fallback")
        platform.Accept = True
        platform.BeforeSend = Sub(value)
                                  If value.Keyboard AndAlso value.Key = 70 AndAlso (value.Flags And 2UI) = 0 Then platform.Active = Other
                              End Sub
        Check(Not BotEngine.SendKey(Game, "F", 5000), "focus loss immediately after delivered F down prevents a successful pickup")
        CheckKeyPair(platform.Events, 70US, "F post-emission focus loss")
        Check(platform.ActivateCalls = 0, "F cleanup must not reacquire focus")
        Dim afterF = platform.Events.Count
        WindowsInput.ReleaseAll()
        Check(platform.Events.Count = afterF, "F post-emission focus loss must complete owned cleanup before returning")
        platform.BeforeSend = Nothing
        platform.Active = Game
        platform.Events.Clear()
        platform.BeforeSend = Sub(value)
                                  If Not value.Keyboard AndAlso value.Flags = 2UI Then platform.Active = Other
                              End Sub
        Check(Not BotEngine.ClickClientPoint(Game, 25, 35, moveDelayMs:=0, downUpDelayMs:=0),
              "focus loss during client mouse down prevents success")
        Check(platform.Events.TakeLast(2).Select(Function(value) value.Flags).SequenceEqual({2UI, 4UI}) AndAlso platform.ActivateCalls = 0,
              "focus loss releases the owned click without reacquiring focus")
        platform.BeforeSend = Nothing
        platform.Active = Game
    End Sub

    Private Sub VerifyOwnedCleanup(platform As FakePlatform)
        platform.Events.Clear()
        WindowsInput.BindTarget(Game)
        WindowsInput.Current.Keyboard(70, 0, 0UI, UIntPtr.Zero)
        WindowsInput.Current.Mouse(2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        Dim count = platform.Events.Count
        WindowsInput.ReleaseTarget(Other)
        Check(platform.Events.Count = count, "stopping another target must not release this target's input")
        platform.Active = Other
        WindowsInput.ReleaseTarget(Game)
        Check(platform.Events.Count = count + 2 AndAlso platform.Events.Skip(count).
              All(Function(value) (value.Keyboard AndAlso value.Key = 70 AndAlso value.Flags = 2UI) OrElse (Not value.Keyboard AndAlso value.Flags = 4UI)),
              "target stop releases both bot-owned keyboard and mouse events even after focus changes")
        platform.Active = Game
        WindowsInput.BindTarget(Game)
        WindowsInput.Current.Keyboard(71, 0, 0UI, UIntPtr.Zero)
        WindowsInput.ReleaseAll()
        Check(platform.Events.Last().Keyboard AndAlso platform.Events.Last().Key = 71 AndAlso platform.Events.Last().Flags = 2UI,
              "shared shutdown releases the injected foreground input's held key")
        count = platform.Events.Count
        WindowsInput.ReleaseAll()
        Check(platform.Events.Count = count, "completed cleanup never emits unowned key or button releases")
    End Sub

    Private Sub VerifyExplicitFocusMaintenance(platform As FakePlatform, input As ForegroundWindowsInput)
        platform.Events.Clear()
        platform.Active = Other
        Dim activations = platform.ActivateCalls
        Dim sends = platform.SendAttempts
        Check(Not input.Activate(Game) AndAlso platform.ActivateCalls = activations,
              "ordinary gameplay activation still cannot reclaim focus")
        Check(Not WindowsInput.TryRestoreGameForeground(Game, platform.Pid + 1UI) AndAlso
              Not WindowsInput.TryRestoreGameForeground(Other, platform.Pid) AndAlso
              Not WindowsInput.TryRestoreGameForeground(IntPtr.Zero, platform.Pid) AndAlso
              Not WindowsInput.TryRestoreGameForeground(Game, 0UI) AndAlso platform.ActivateCalls = activations,
              "focus supervisor rejects zero handles, zero PIDs and changed process ownership before activation")
        platform.Available = False
        Check(Not WindowsInput.TryRestoreGameForeground(Game, platform.Pid) AndAlso platform.ActivateCalls = activations,
              "focus supervisor cannot activate an unavailable target")
        platform.Available = True
        Check(WindowsInput.TryRestoreGameForeground(Game, platform.Pid) AndAlso platform.Active = Game AndAlso
              platform.ActivateCalls = activations + 1 AndAlso platform.SendAttempts = sends AndAlso platform.Events.Count = 0,
              "explicit supervisor restores the validated target without gameplay key or mouse events")

        platform.Active = Other
        platform.AllowActivate = False
        sends = platform.SendAttempts
        Check(Not WindowsInput.TryRestoreGameForeground(Game, platform.Pid) AndAlso platform.Active = Other AndAlso
              platform.SendAttempts = sends AndAlso platform.Events.Count = 0,
              "failed focus restoration must not emit gameplay input")
        platform.AllowActivate = True
        platform.Active = Game
        WindowsInput.BindTarget(Game)
        input.Keyboard(70, 0, 0UI, UIntPtr.Zero)
        platform.Active = Other
        activations = platform.ActivateCalls
        Dim releasedBeforeActivation As Boolean
        platform.BeforeSend = Sub(value)
                                  If value.Keyboard AndAlso value.Key = 70 AndAlso value.Flags = 2UI Then
                                      releasedBeforeActivation = platform.Active = Other AndAlso platform.ActivateCalls = activations
                                  End If
                              End Sub
        Check(WindowsInput.TryRestoreGameForeground(Game, platform.Pid) AndAlso releasedBeforeActivation,
              "supervisor releases the original owned held key before reacquiring game focus")
        platform.BeforeSend = Nothing
        CheckKeyPair(platform.Events, 70US, "supervisor held-key cleanup")

        platform.Events.Clear()
        platform.Active = Other
        activations = platform.ActivateCalls
        sends = platform.SendAttempts
        Using entered As New ManualResetEventSlim(False), release As New ManualResetEventSlim(False)
            Dim worker = Task.Run(Sub()
                                      SyncLock WindowsInput.SequenceLock
                                          entered.Set()
                                          release.Wait(3000)
                                      End SyncLock
                                  End Sub)
            Try
                Check(entered.Wait(1000), "owned worker must acquire the input sequence lock")
                Dim watch = Diagnostics.Stopwatch.StartNew()
                Check(Not WindowsInput.TryRestoreGameForeground(Game, platform.Pid) AndAlso watch.ElapsedMilliseconds < 500 AndAlso
                      platform.ActivateCalls = activations AndAlso platform.SendAttempts = sends AndAlso platform.Events.Count = 0,
                      "occupied input sequence makes focus maintenance return promptly without activation or input")
            Finally
                release.Set()
                worker.GetAwaiter().GetResult()
            End Try
        End Using
        platform.Active = Game
    End Sub

    Private Sub VerifyPendingReleaseRecovery(platform As FakePlatform, input As ForegroundWindowsInput)
        platform.Events.Clear()
        platform.Active = Game
        WindowsInput.BindTarget(Game)
        input.Keyboard(72, 0, 0UI, UIntPtr.Zero)
        input.Mouse(2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        platform.Active = Other
        platform.Accept = False
        Dim attempts = platform.SendAttempts
        input.ReleaseUnfocused()
        Check(platform.Events.Count = 2 AndAlso platform.SendAttempts = attempts + 2,
              "focus-loss cleanup must try both owned releases when the platform rejects them")

        ' Focus may return before a later successful maintenance pass. Failed releases
        ' must remain requested even though the original target is foreground again.
        platform.Active = Game
        platform.Accept = True
        Check(WindowsInput.TryRestoreGameForeground(Game, platform.Pid), "explicit restoration must recover after temporarily rejected cleanup")
        input.ReleaseUnfocused()
        Check(platform.Events.Count = 4 AndAlso platform.Events.Where(Function(value) value.Keyboard AndAlso value.Key = 72 AndAlso value.Flags = 2UI).Count() = 1 AndAlso
              platform.Events.Where(Function(value) Not value.Keyboard AndAlso value.Flags = 4UI).Count() = 1,
              "returned focus must not strand pending owned key or button releases")
        Dim completed = platform.Events.Count
        input.ReleaseUnfocused()
        Check(platform.Events.Count = completed, "recovered pending releases must never emit another unowned up")

        input.Keyboard(72, 0, 0UI, UIntPtr.Zero)
        input.Mouse(2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        input.ReleaseAll()
        Check(platform.Events.Count = completed + 4 AndAlso platform.Events.Skip(completed).Where(Function(value) value.Keyboard).
              Select(Function(value) value.Flags).SequenceEqual({0UI, 2UI}) AndAlso platform.Events.Skip(completed).Where(Function(value) Not value.Keyboard).
              Select(Function(value) value.Flags).SequenceEqual({2UI, 4UI}),
              "successful pending cleanup must permit fresh key and mouse downs with complete owned releases")
    End Sub

    Private NotInheritable Class SelectedGamePlatform
        Inherits FakePlatform
        Public Overrides Function Valid(hwnd As IntPtr) As Boolean
            Return hwnd = Game AndAlso MyBase.Valid(hwnd)
        End Function
    End Class
End Module
