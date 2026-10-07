Imports System.Reflection

' Background mode (borrowed focus). Only FakePlatform receives requests here: no window is activated,
' no native SendInput call is made and no real clock or user input is read. The fake clock and the
' simulated user/window stack make every hand-off decision deterministic.
Friend Module FocusBorrowTests
    Private assertions As Integer
    Private ReadOnly Game As New IntPtr(41001)
    Private ReadOnly Game2 As New IntPtr(41003)
    Private ReadOnly Other As New IntPtr(41002)
    Private ReadOnly Third As New IntPtr(41004)

    Friend Function Verify() As Integer
        assertions = 0
        VerifySettings()
        VerifyEligibilityDoesNotTakeFocus()
        VerifyBorrowCycleAndLinger()
        VerifyBurstSharesOneBorrow()
        VerifyGameAlreadyInFront()
        VerifyYieldToUser()
        VerifyBlockers()
        VerifyRefusalBackoff()
        VerifyUserTakesFocus()
        VerifyUserClickAndTypingDuringBorrow()
        VerifyHeldKeysKeepTheBorrow()
        VerifyMouseAndTextNeverBorrow()
        VerifyRestackFallback()
        VerifyVanishedPreviousWindow()
        VerifyStopAndDisableHandBack()
        VerifySecondGameWindow()
        VerifyWatchdogSampling()
        VerifyFacadeAndEnginePaths()
        VerifyUserActivityTracker()
        VerifyShellSurfaceClasses()
        Return assertions
    End Function

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New InvalidOperationException("Focus borrow: " & message)
        assertions += 1
    End Sub

    Private Function NewBackground(Optional settings As FocusBorrowSettings = Nothing) As (Input As ForegroundWindowsInput, Platform As FakePlatform)
        Dim platform As New FakePlatform With {.Active = Other}
        Dim input As New ForegroundWindowsInput(platform, Sub(milliseconds) Return, allowActivation:=False)
        input.FocusBorrow = If(settings, New FocusBorrowSettings(True, 800, 250, True))
        Return (input, platform)
    End Function

    Private Function Down(input As ForegroundWindowsInput, hwnd As IntPtr, key As Integer) As Boolean
        Return input.Post(hwnd, &H100UI, New IntPtr(key), New IntPtr(1))
    End Function

    Private Function Up(input As ForegroundWindowsInput, hwnd As IntPtr, key As Integer) As Boolean
        Return input.Post(hwnd, &H101UI, New IntPtr(key), IntPtr.Zero)
    End Function

    Private Sub VerifySettings()
        Check(Not FocusBorrowSettings.Disabled.Enabled, "default settings keep Background mode off")
        Dim clamped As New FocusBorrowSettings(True, 99999999, 99999999, False)
        Check(clamped.YieldToUserMs = FocusBorrowSettings.MaximumYieldMs AndAlso clamped.LingerMs = 5000 AndAlso Not clamped.PauseForFullScreen,
              "wait and linger are clamped to sane bounds")
        Check(New FocusBorrowSettings(True, -5, -5).YieldToUserMs = 0 AndAlso New FocusBorrowSettings(True, -5, -5).LingerMs = 0, "negative values clamp to zero")
        Dim plain As New ForegroundWindowsInput(New FakePlatform With {.Active = Game})
        Check(Not plain.BorrowEnabled AndAlso Not plain.FocusBorrow.Enabled, "a new foreground backend never borrows unless configured")
    End Sub

    Private Sub VerifyEligibilityDoesNotTakeFocus()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        Check(input.Activate(Game), "game behind other windows is eligible in Background mode")
        Check(platform.ActivateCalls = 0 AndAlso platform.ActivateOtherCalls = 0 AndAlso platform.Events.Count = 0,
              "an Activate request that sends nothing never takes the keyboard or emits input")
        Check(Not input.IsWindowForeground(Game) AndAlso input.CanReceiveInput(Game), "strict focus stays false while eligibility is true")
        platform.Available = False
        Check(Not input.Activate(Game) AndAlso Not input.CanReceiveInput(Game), "a closed or minimized game is not eligible")
        platform.Available = True

        ' Without Background mode the same request stays strictly foreground.
        Dim strict As New FakePlatform With {.Active = Other}
        Dim strictInput As New ForegroundWindowsInput(strict, Sub(milliseconds) Return, allowActivation:=False)
        Check(Not strictInput.Activate(Game) AndAlso Not strictInput.CanReceiveInput(Game), "foreground backend still requires real focus")
        Check(Not Down(strictInput, Game, 65) AndAlso strict.ActivateCalls = 0 AndAlso strict.Events.Count = 0, "foreground backend never borrows")
    End Sub

    Private Sub VerifyBorrowCycleAndLinger()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Check(Down(input, Game, 65), "a key press borrows the keyboard and is sent")
        Check(platform.ActivateCalls = 1 AndAlso platform.Active = Game, "the game was activated exactly once")
        Check(platform.CaptureCalls = 1 AndAlso platform.RestoreZCalls = 1, "the game window is put back in the window stack right after activation")
        Check(platform.Events.Count = 1 AndAlso platform.Events(0).Key = 65 AndAlso platform.Events(0).Flags = 0, "the key reached the platform after focus moved")
        Check(input.FocusBorrowStatus().Active AndAlso input.FocusBorrowStatus().Borrowed = 1, "status reports one active borrow")
        Check(input.BorrowActive, "the borrow is visible to overlays that follow the foreground window")
        platform.Clock += 150
        Check(Up(input, Game, 65) AndAlso platform.Events.Last().Flags = 2, "the key is released while the game still has the keyboard")
        platform.Clock += 100
        input.ServiceFocusLease()
        Check(platform.Active = Game AndAlso platform.ActivateOtherCalls = 0, "focus is kept during the linger so a burst shares one borrow")
        platform.Clock += 200
        input.ServiceFocusLease()
        Check(platform.Active = Other AndAlso platform.ActivateOtherCalls = 1 AndAlso platform.LastActivatedOther = Other,
              "focus goes back to the window the user was in")
        Check(platform.RestoreZCalls = 2, "the game window is put back in the stack again after the hand-back")
        Check(Not input.FocusBorrowStatus().Active AndAlso Not input.BorrowActive, "status reports no active borrow after the hand-back")
        input.ServiceFocusLease()
        Check(platform.ActivateOtherCalls = 1, "an idle watchdog does nothing")
    End Sub

    Private Sub VerifyBurstSharesOneBorrow()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        For Each key As Integer In {65, 66, 67}
            Check(Down(input, Game, key), "burst key down")
            platform.Clock += 120
            Check(Up(input, Game, key), "burst key up")
            platform.Clock += 90
            input.ServiceFocusLease()
        Next
        Check(platform.ActivateCalls = 1 AndAlso platform.ActivateOtherCalls = 0, "keys separated by less than the linger share one borrow")
        platform.Clock += 400
        input.ServiceFocusLease()
        Check(platform.ActivateOtherCalls = 1 AndAlso platform.Active = Other, "the borrow ends once the bot is quiet")
        Check(Down(input, Game, 68) AndAlso platform.ActivateCalls = 2, "a later key borrows again")
    End Sub

    Private Sub VerifyGameAlreadyInFront()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        platform.Active = Game
        input.Activate(Game)
        Check(Down(input, Game, 65), "a game the user already focused receives keys directly")
        Up(input, Game, 65)
        platform.Clock += 5000
        input.ServiceFocusLease()
        Check(platform.ActivateCalls = 0 AndAlso platform.ActivateOtherCalls = 0 AndAlso platform.Active = Game,
              "no borrow means nothing is taken or handed back from a game the user chose to focus")
    End Sub

    Private Sub VerifyYieldToUser()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        platform.UserIdle = 100
        Check(Not Down(input, Game, 65), "the bot skips the key while the user is typing")
        Check(platform.ActivateCalls = 0 AndAlso platform.Events.Count = 0 AndAlso platform.Active = Other, "nothing is taken or sent while the user types")
        Check(input.FocusBorrowStatus().WaitedForUser = 1 AndAlso input.FocusBorrowStatus().LastNote.Contains("stop typing"), "the skip is counted and explained")
        platform.UserIdle = 799
        Check(Not Down(input, Game, 65), "799 ms of quiet is still inside the 800 ms wait")
        platform.UserIdle = 800
        Check(Down(input, Game, 65), "the bot borrows once the user has been quiet for the full wait")
        Up(input, Game, 65)
        input.ReleaseAll()
        input.ServiceFocusLease()

        Dim eager = NewBackground(New FocusBorrowSettings(True, 0, 250, True))
        eager.Platform.UserIdle = 0
        Check(Down(eager.Input, Game, 65) AndAlso eager.Platform.ActivateCalls = 1, "a wait of 0 gives the bot priority over the user's typing")
    End Sub

    Private Sub VerifyBlockers()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        platform.Blocker = "a full-screen app is in front"
        Check(Not Down(input, Game, 65) AndAlso platform.ActivateCalls = 0 AndAlso platform.Events.Count = 0, "a full-screen app is not interrupted")
        Check(platform.LastIncludeFullScreen, "the full-screen setting is passed to the platform")
        Check(input.FocusBorrowStatus().Paused = 1 AndAlso input.FocusBorrowStatus().LastNote.Contains("full-screen"), "the pause is counted and explained")
        platform.Blocker = ""
        Check(Down(input, Game, 65), "the bot continues once the blocker is gone")

        Dim relaxed = NewBackground(New FocusBorrowSettings(True, 800, 250, False))
        relaxed.Platform.Blocker = "ignored"
        Check(Not Down(relaxed.Input, Game, 65) AndAlso Not relaxed.Platform.LastIncludeFullScreen, "the platform is told when full-screen apps may be interrupted")
    End Sub

    Private Sub VerifyRefusalBackoff()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        platform.AllowActivate = False
        Check(Not Down(input, Game, 65), "a refused activation skips the key")
        Check(platform.ActivateCalls = 1 AndAlso platform.Events.Count = 0, "no key is sent when the keyboard was not obtained")
        Check(platform.RestoreZCalls = 1, "a refused request puts the window stack back as it was")
        Check(Not input.BorrowActive, "a refused request leaves no borrow behind (the overlay must not think the game is borrowed)")
        Check(Not Down(input, Game, 66) AndAlso platform.ActivateCalls = 1, "retries are paced instead of hammering Windows")
        Check(input.FocusBorrowStatus().Refused = 1, "the refusal is counted")
        platform.Clock += 760
        Check(Not Down(input, Game, 67) AndAlso platform.ActivateCalls = 2, "after the backoff the bot tries again")
        platform.AllowActivate = True
        platform.Clock += 760
        Check(Down(input, Game, 68) AndAlso platform.ActivateCalls = 3 AndAlso platform.Active = Game, "a later success borrows normally")
    End Sub

    Private Sub VerifyUserTakesFocus()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Check(Down(input, Game, 65), "borrowed key down")
        platform.Active = Third ' the user clicked another window
        platform.Clock += 30
        input.ServiceFocusLease()
        Check(platform.ActivateOtherCalls = 0 AndAlso platform.Active = Third, "the bot never fights the user for focus")
        Check(platform.Events.Last().Key = 65 AndAlso platform.Events.Last().Flags = 2, "the held key is released when focus is lost")
        Check(input.FocusBorrowStatus().TakenByUser = 1 AndAlso platform.NotedActivity = 1 AndAlso Not input.FocusBorrowStatus().Active, "the takeover is counted and treated as user activity")
        Check(Not Down(input, Game, 66) AndAlso platform.ActivateCalls = 1, "the bot waits for the user after they switch windows")
    End Sub

    Private Sub VerifyUserClickAndTypingDuringBorrow()
        ' A click while borrowed: the user is steering, so focus must not be moved back.
        Dim click = NewBackground()
        click.Input.Activate(Game)
        Down(click.Input, Game, 65) : Up(click.Input, Game, 65)
        click.Platform.Clock += 100
        click.Platform.UserButtonIdle = 10
        click.Input.ServiceFocusLease()
        Check(click.Platform.ActivateOtherCalls = 0 AndAlso click.Platform.Active = Game, "a click on the game while borrowed leaves focus where the user put it")
        Check(Not click.Input.FocusBorrowStatus().Active AndAlso click.Input.FocusBorrowStatus().TakenByUser = 1, "the borrow ends quietly")

        ' Typing while borrowed: focus goes straight back to what they were typing in.
        Dim typing = NewBackground()
        typing.Input.Activate(Game)
        Down(typing.Input, Game, 65)
        typing.Platform.Clock += 100
        typing.Platform.UserIdle = 30
        typing.Input.ServiceFocusLease()
        Check(typing.Platform.ActivateOtherCalls = 1 AndAlso typing.Platform.Active = Other, "typing during a borrow hands the keyboard back before the linger ends")
        Check(typing.Platform.Events.Last().Key = 65 AndAlso typing.Platform.Events.Last().Flags = 2, "the bot's key is released first, while the game still has the keyboard")
        Check(typing.Input.FocusBorrowStatus().WaitedForUser = 1, "the hand-back is counted")

        ' Activity from before the borrow began is not mistaken for typing during it.
        Dim older = NewBackground()
        older.Input.Activate(Game)
        older.Platform.UserIdle = 800
        Down(older.Input, Game, 65) : Up(older.Input, Game, 65)
        older.Platform.Clock += 100
        older.Platform.UserIdle = 900
        older.Input.ServiceFocusLease()
        Check(older.Platform.ActivateOtherCalls = 0 AndAlso older.Input.FocusBorrowStatus().Active, "earlier quiet time does not end the borrow")
    End Sub

    Private Sub VerifyHeldKeysKeepTheBorrow()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Check(Down(input, Game, 87), "movement key down borrows")
        platform.Clock += 5000
        input.ServiceFocusLease()
        Check(platform.Active = Game AndAlso platform.ActivateOtherCalls = 0, "a held key keeps the keyboard however long it is held")
        Check(Up(input, Game, 87), "release")
        platform.Clock += 300
        input.ServiceFocusLease()
        Check(platform.Active = Other AndAlso platform.ActivateOtherCalls = 1, "the keyboard goes back once the held key is released")

        ' Mid-hold, eligibility is strict: a key that is down cannot continue after focus moved away.
        Down(input, Game, 87)
        platform.Active = Third
        Check(Not input.CanReceiveInput(Game), "a held key cannot continue once the keyboard has moved elsewhere")
        input.ReleaseUnfocused()
        Check(input.CanReceiveInput(Game), "after the release the game is eligible to borrow again")
    End Sub

    Private Sub VerifyMouseAndTextNeverBorrow()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Check(Not input.Post(Game, &H201UI, IntPtr.Zero, New IntPtr(50 Or (60 << 16))), "a click is not delivered to a game that is not in front")
        Check(Not input.MoveCursor(10, 10), "the cursor is not moved for a game that is not in front")
        Check(Not input.Post(Game, &H102UI, New IntPtr(67), IntPtr.Zero), "text never borrows the keyboard")
        Check(platform.ActivateCalls = 0 AndAlso platform.Events.Count = 0 AndAlso platform.Active = Other, "mouse and text requests change nothing")
    End Sub

    Private Sub VerifyRestackFallback()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        platform.RestoreZMovesActivationTo = Third ' this "Windows" moves activation when the window is re-stacked
        Check(Down(input, Game, 65), "the key is still sent when re-stacking moved activation")
        Check(platform.ActivateCalls = 2 AndAlso platform.RestoreZCalls = 1 AndAlso platform.Active = Game,
              "activation is taken again and the key goes to the game")
        Check(platform.Events.Count = 1 AndAlso platform.Events(0).Key = 65, "exactly one key was sent")
        platform.RestoreZMovesActivationTo = IntPtr.Zero
        Up(input, Game, 65)
        platform.Clock += 400
        input.ServiceFocusLease()
        Check(platform.ActivateOtherCalls = 1, "focus is still handed back")
        Dim restacks = platform.RestoreZCalls
        Check(Down(input, Game, 66) AndAlso platform.RestoreZCalls = restacks, "once re-stacking proved unsafe it is not attempted again")
    End Sub

    Private Sub VerifyVanishedPreviousWindow()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Down(input, Game, 65) : Up(input, Game, 65)
        platform.Restorable = False
        platform.Clock += 400
        input.ServiceFocusLease()
        Check(platform.ActivateOtherCalls = 0 AndAlso platform.Active = Game, "when the previous window is gone the game simply keeps the keyboard")
        Check(Not input.FocusBorrowStatus().Active, "the borrow is over either way")
        platform.Restorable = True
        platform.ActivateOtherResult = False
        platform.Active = Other
        Down(input, Game, 66) : Up(input, Game, 66)
        platform.Clock += 400
        input.ServiceFocusLease()
        Check(input.FocusBorrowStatus().Refused >= 1 AndAlso input.FocusBorrowStatus().LastNote.Contains("hand focus back"), "a refused hand-back is reported")
    End Sub

    Private Sub VerifyStopAndDisableHandBack()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Down(input, Game, 65)
        input.ReleaseAll()
        Check(platform.Events.Last().Flags = 2, "stopping releases the held key")
        input.ServiceFocusLease()
        Check(platform.Active = Other AndAlso platform.ActivateOtherCalls = 1, "stopping hands the keyboard back without waiting out the linger")

        ' A stop request that found no borrow must not cut a later borrow short.
        input.ReleaseAll()
        platform.Clock += 10
        Check(Down(input, Game, 66), "borrow after an idle stop request")
        input.ServiceFocusLease()
        Check(platform.Active = Game AndAlso input.FocusBorrowStatus().Active, "a stale stop request does not end the next borrow")
        Up(input, Game, 66)

        ' Turning Background mode off hands the keyboard back too.
        input.FocusBorrow = FocusBorrowSettings.Disabled
        input.ServiceFocusLease()
        Check(platform.Active = Other AndAlso Not input.BorrowEnabled, "turning Background mode off returns the keyboard")
        Check(Not input.Activate(Game) AndAlso Not Down(input, Game, 67), "with Background mode off the game must be in front again")
    End Sub

    Private Sub VerifySecondGameWindow()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.Activate(Game)
        Down(input, Game, 65) : Up(input, Game, 65)
        input.Activate(Game2)
        Check(Down(input, Game2, 66), "a second game window borrows after the first hands back")
        Check(platform.ActivateOtherCalls = 1 AndAlso platform.LastActivatedOther = Other AndAlso platform.Active = Game2,
              "the first borrow returned the keyboard before the second took it")
        platform.Clock += 400
        Up(input, Game2, 66)
        platform.Clock += 400
        input.ServiceFocusLease()
        Check(platform.Active = Other, "the second borrow also returns to the user's window")
    End Sub

    Private Sub VerifyWatchdogSampling()
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        input.ServiceFocusLease()
        Check(platform.SampleCalls = 1, "the watchdog samples user activity while Background mode is on")
        input.Activate(Game)
        Down(input, Game, 87)
        input.ServiceFocusLease()
        Check(platform.LastSampledKeys.SequenceEqual({87}), "keys the bot holds are reported so they are not mistaken for the user")
        Up(input, Game, 87)
        input.FocusBorrow = FocusBorrowSettings.Disabled
        Dim before = platform.SampleCalls
        input.ServiceFocusLease()
        Check(platform.SampleCalls = before, "no sampling happens when Background mode is off")
    End Sub

    Private Sub VerifyFacadeAndEnginePaths()
        Dim original = WindowsInput.Current
        Dim fixture = NewBackground()
        Dim input = fixture.Input, platform = fixture.Platform
        WindowsInput.Current = input
        Try
            Check(WindowsInput.FocusBorrowEnabled AndAlso WindowsInput.KeyboardOnlyMode AndAlso WindowsInput.InputMode = "foreground-borrow",
                  "Background mode reports keyboard-only operation and its own input mode")
            Check(Not WindowsInput.UsesTargetedInput AndAlso Not WindowsInput.UsesInternalHook, "it is still the foreground SendInput backend")
            Check(WindowsInput.TargetCanReceiveInput(Game) AndAlso Not WindowsInput.TargetIsForeground(Game),
                  "the selector accepts a background game but still reports it is not foreground")
            Check(BotEngine.SendKey(Game, "1", 12), "a normal skill key works with the game behind other windows")
            Check(platform.Events.Count = 2 AndAlso platform.Events.All(Function(item) item.Keyboard AndAlso item.Key = 49) AndAlso platform.ActivateCalls = 1,
                  "the skill key is one owned down/up pair after one borrow")
            Check(WindowsInput.FocusBorrowActive, "the facade reports an active borrow while the game is only in front for a key")
            platform.Events.Clear()
            Check(BotEngine.SendKey(Game, "CTRL+2", 12), "a Ctrl chord works too")
            Check(platform.Events.Select(Function(item) CInt(item.Key)).SequenceEqual({17, 50, 50, 17}) AndAlso platform.ActivateCalls = 1,
                  "the chord shares the existing borrow and releases the digit before its modifier")
            ' The wait applies when the keyboard has to be taken, not mid-borrow: end this borrow first.
            input.ReleaseAll()
            input.ServiceFocusLease()
            Check(platform.Active = Other AndAlso Not WindowsInput.FocusBorrowActive, "the borrow ended")
            platform.Events.Clear()
            platform.UserIdle = 50
            Check(Not BotEngine.SendKey(Game, "1", 12) AndAlso platform.Events.Count = 0 AndAlso platform.Active = Other,
                  "the engine reports a skipped key while the user is typing")
            platform.UserIdle = Long.MaxValue
            Check(BotEngine.SendKey(Game, "1", 12) AndAlso platform.Events.Count = 2, "and sends it once the user has stopped")
        Finally
            input.ReleaseAll()
            input.ServiceFocusLease()
            WindowsInput.Current = original
        End Try
        Check(Object.ReferenceEquals(WindowsInput.Current, original) AndAlso Not WindowsInput.FocusBorrowEnabled,
              "the production backend keeps Background mode off unless the control panel turns it on")
    End Sub

    Private Sub VerifyUserActivityTracker()
        Dim tracker As New UserActivityTracker()
        Check(tracker.IdleMs(1000) = Long.MaxValue AndAlso tracker.ButtonIdleMs(1000) = Long.MaxValue, "no activity is reported before anything was sampled")
        tracker.Sample(1000, 100000, False, True, False, 0)
        Check(tracker.IdleMs(1000) = 0, "a key held down by someone other than the bot is user activity")
        tracker.Sample(1300, 100000, False, False, False, 0)
        Check(tracker.IdleMs(1300) = 300, "idle time grows once the key is released")
        tracker.Sample(2000, 5, False, False, False, 1996)
        Check(tracker.IdleMs(2000) = 1000, "an input event right beside the bot's own SendInput is not the user")
        tracker.Sample(3000, 5, True, False, False, 0)
        Check(tracker.IdleMs(3000) = 2000, "pure mouse motion is not typing")
        tracker.Sample(4000, 5, False, False, False, 0)
        Check(tracker.IdleMs(4000) = 5, "an unexplained input event between samples is the user (a key tapped faster than the sampling)")
        Check(tracker.ButtonIdleMs(4000) = Long.MaxValue, "a keyboard event is not a mouse-button event")
        tracker.Sample(5000, 1000000, False, False, True, 0)
        Check(tracker.IdleMs(5000) = 0 AndAlso tracker.ButtonIdleMs(5000) = 0, "a pressed mouse button is user activity")
        tracker.Sample(5300, 1000000, False, False, False, 0)
        Check(tracker.ButtonIdleMs(5300) = 300, "button idle time grows after release")

        Dim stuck As New UserActivityTracker()
        For tick As Long = 6000 To 10000 Step 500
            stuck.Sample(tick, 1000000, False, True, False, 0)
        Next
        stuck.Sample(10500, 1000000, False, True, False, 0)
        stuck.Sample(12000, 1000000, False, True, False, 0)
        Check(stuck.IdleMs(12000) = 2000, "a key held for seconds is treated as stuck, not as typing")
        stuck.Sample(12100, 1000000, False, False, False, 0)
        stuck.Sample(12200, 1000000, False, True, False, 0)
        Check(stuck.IdleMs(12200) = 0, "a new key press after the release counts again")

        tracker.MarkActivity(20000)
        Check(tracker.IdleMs(20000) = 0 AndAlso tracker.IdleMs(20250) = 250, "the user switching windows counts as activity")
    End Sub

    Private Sub VerifyShellSurfaceClasses()
        Dim native = GetType(WindowsInput).Assembly.GetType("KathanaBotControlPanel.NativeInputPlatform", throwOnError:=True)
        Dim method = native.GetMethod("IsShellSurfaceClass", BindingFlags.Static Or BindingFlags.NonPublic Or BindingFlags.Public)
        If method Is Nothing Then Throw New MissingMethodException(native.FullName, "IsShellSurfaceClass")
        For Each name As String In {"Shell_TrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "#32768", "shell_traywnd"}
            Check(CBool(method.Invoke(Nothing, New Object() {name})), name & " is a shell surface the bot must not take the keyboard from")
        Next
        For Each name As String In {"Chrome_WidgetWin_1", "SDL_app", "WindowsForms10.Window.8.app.0.1f550a4_r3_ad1", "CabinetWClass", "", Nothing}
            Check(Not CBool(method.Invoke(Nothing, New Object() {name})), "ordinary window class '" & If(name, "(null)") & "' is not a shell surface")
        Next
    End Sub
End Module
