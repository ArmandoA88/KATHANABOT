Imports System.Drawing
Imports System.Threading

' Every platform in this module is fake; no game input or window activation occurs.
Friend Module KeyboardModeTests
    Private assertions As Integer
    Private ReadOnly Game As New IntPtr(901)
    Private ReadOnly Other As New IntPtr(902)

    Friend Function Verify() As Integer
        assertions = 0
        VerifyModeIdsAndSelector()
        For Each mode In {BackgroundKeyboardMode.PostedScanCode, BackgroundKeyboardMode.SynchronousScanCode,
                          BackgroundKeyboardMode.PostedZeroScanCode, BackgroundKeyboardMode.SynchronousZeroScanCode}
            VerifySupportedKeys(mode)
            VerifyModifierAndSystemFormat(mode)
            VerifyProductionKeysAndCancellation(mode)
            VerifyKeyboardOnlyScope(mode)
            VerifyModeSwitch(mode)
        Next
        Return assertions
    End Function

    Private Sub VerifyModeIdsAndSelector()
        Dim originalInput = WindowsInput.Current
        Dim originalMode = WindowsInput.BackgroundKeyMode
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(BackgroundKeyboardMode.PostedScanCode, platform)
        WindowsInput.Current = input
        Try
            For Each sample In {(BackgroundKeyboardMode.PostedScanCode, "posted-scan"), (BackgroundKeyboardMode.SynchronousScanCode, "send-scan"),
                                (BackgroundKeyboardMode.PostedZeroScanCode, "posted-zero"), (BackgroundKeyboardMode.SynchronousZeroScanCode, "send-zero")}
                Check(WindowsInput.KeyboardModeId(sample.Item1) = sample.Item2, "stable method ID " & sample.Item2)
                Dim parsed As BackgroundKeyboardMode
                Check(WindowsInput.TryParseKeyboardMode(sample.Item2, parsed) AndAlso parsed = sample.Item1, "method ID roundtrip " & sample.Item2)
                Check(WindowsInput.TryParseKeyboardMode("  " & sample.Item2.ToUpperInvariant() & "  ", parsed) AndAlso parsed = sample.Item1,
                      "method parser tolerates casing and surrounding whitespace for " & sample.Item2)
            Next
            For Each value In New String() {Nothing, "", " ", "posted", "post-scan", "send", "foreground", "999"}
                Dim parsed = BackgroundKeyboardMode.SynchronousZeroScanCode
                Check(Not WindowsInput.TryParseKeyboardMode(value, parsed) AndAlso parsed = BackgroundKeyboardMode.SynchronousZeroScanCode,
                      "invalid method ID cannot overwrite a selected method")
            Next
            Dim rejected As Boolean
            Try
                WindowsInput.KeyboardModeId(CType(99, BackgroundKeyboardMode))
            Catch ex As ArgumentOutOfRangeException
                rejected = True
            End Try
            Check(rejected, "invalid enum has no public method ID")
            rejected = False
            Try
                input.SetKeyboardMode(CType(99, BackgroundKeyboardMode))
            Catch ex As ArgumentOutOfRangeException
                rejected = True
            End Try
            Check(rejected AndAlso input.KeyboardMode = BackgroundKeyboardMode.PostedScanCode, "invalid enum cannot change a background backend")
            Check(platform.Events.Count = 0 AndAlso platform.KeyboardAttempts.Count = 0, "method helpers and invalid values emit no target input")

            ' The production default owns no keys in this controlled test program.
            ' Switching its delivery setting must not activate any real target.
            WindowsInput.Current = Nothing
            For Each sample In {(BackgroundKeyboardMode.PostedScanCode, "posted-scan"), (BackgroundKeyboardMode.SynchronousScanCode, "send-scan"),
                                (BackgroundKeyboardMode.PostedZeroScanCode, "posted-zero"), (BackgroundKeyboardMode.SynchronousZeroScanCode, "send-zero")}
                Check(WindowsInput.TrySetBackgroundKeyMode(sample.Item1) AndAlso WindowsInput.BackgroundKeyMode = sample.Item1,
                      "production selector accepts method " & sample.Item2 & " with no owned keys")
                Check(WindowsInput.KeyboardOnlyMode AndAlso WindowsInput.InputMode = "background-keys-" & sample.Item2,
                      "production selector reports its complete keyboard-only method ID")
            Next
        Finally
            WindowsInput.TrySetBackgroundKeyMode(originalMode)
            WindowsInput.Current = originalInput
        End Try
        Check(WindowsInput.BackgroundKeyMode = originalMode AndAlso Object.ReferenceEquals(WindowsInput.Current, originalInput),
              "production method and injected backend are restored after selector checks")
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception("Background keyboard methods: " & message)
        assertions += 1
    End Sub

    Private Function CreateInput(mode As BackgroundKeyboardMode, ByRef platform As FakeBackgroundPlatform) As SdlBackgroundWindowsInput
        platform = New FakeBackgroundPlatform
        platform.Pids(Game) = 44
        platform.Pids(Other) = 45
        platform.Origins(Game) = New Point(0, 0)
        platform.Origins(Other) = New Point(300, 0)
        Dim input As New SdlBackgroundWindowsInput(platform, Sub(milliseconds) Return, keyboardMode:=mode, keyboardOnly:=True)
        Check(input.Activate(Game) AndAlso input.KeyboardMode = mode AndAlso input.KeyboardOnly, mode.ToString() & " binds a keyboard-only target")
        Return input
    End Function

    Private Function UsesScan(mode As BackgroundKeyboardMode) As Boolean
        Return mode = BackgroundKeyboardMode.PostedScanCode OrElse mode = BackgroundKeyboardMode.SynchronousScanCode
    End Function

    Private Sub CheckPair(values As IEnumerable(Of TargetEvent), mode As BackgroundKeyboardMode, hwnd As IntPtr, key As Long, context As String)
        Dim pair = values.ToArray()
        Check(pair.Length = 2 AndAlso pair.Select(Function(value) value.Message).SequenceEqual({&H100UI, &H101UI}), context & " has a complete down/up pair")
        Check(pair.All(Function(value) value.Hwnd = hwnd AndAlso value.WParam = key AndAlso value.KeyboardMode.HasValue AndAlso value.KeyboardMode.Value = mode), context & " retains target, key and delivery method")
        Check((pair(0).LParam And &HFFFFL) = 1 AndAlso pair(1).LParam = (pair(0).LParam Or &HC0000000L), context & " release preserves its exact down format and transition bits")
        If Not UsesScan(mode) Then Check(pair(0).LParam = 1L, context & " zero-scan method keeps the verified virtual-key format")
    End Sub

    Private Sub VerifySupportedKeys(mode As BackgroundKeyboardMode)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(mode, platform)
        Dim keys = Enumerable.Range(&H30, 10).Concat(Enumerable.Range(&H41, 26)).Concat(Enumerable.Range(&H70, 24)).Concat(
            {&H9, &H20, &H1B, &HD, &HA5, &HBC, &HBD, &HBE, &HBF, &HBA, &HDE, &HBB, &HC0}).Distinct()
        For Each key In keys
            platform.Events.Clear()
            input.Keyboard(CByte(key), 255, 1UI, UIntPtr.Zero)
            input.Keyboard(CByte(key), 255, 3UI, UIntPtr.Zero)
            CheckPair(platform.Events, mode, Game, key, mode.ToString() & " supported VK " & key.ToString())
        Next
        input.ReleaseAll()
    End Sub

    Private Sub VerifyModifierAndSystemFormat(mode As BackgroundKeyboardMode)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(mode, platform)
        For Each sample In {(&HA0, False), (&HA1, False), (&HA2, False), (&HA3, True), (&HA4, False), (&HA5, True), (&H25, True)}
            platform.Events.Clear()
            ' Deliberately supply the opposite extended flag and a bogus scan.
            ' Method selection determines the message format, while VK identity stays explicit.
            input.Keyboard(CByte(sample.Item1), 255, If(sample.Item2, 0UI, 1UI), UIntPtr.Zero)
            input.Keyboard(CByte(sample.Item1), 0, 2UI, UIntPtr.Zero)
            CheckPair(platform.Events, mode, Game, sample.Item1, mode.ToString() & " explicit modifier/navigation VK " & sample.Item1.ToString())
            Dim down = platform.Events(0).LParam
            Check(If(UsesScan(mode), (down And &HFF0000L) <> 0 AndAlso ((down And &H1000000L) <> 0) = sample.Item2 AndAlso (down And &H20000000L) = 0,
                     down = 1), mode.ToString() & " chooses scan/extended bits from the explicit virtual key")
        Next
        platform.Events.Clear()
        Check(input.Post(Game, &H104UI, New IntPtr(&HA5), New IntPtr(-1)), mode.ToString() & " accepts explicit RALT system-key down")
        Dim systemDown = platform.Events.Last()
        Check(systemDown.Message = &H104UI AndAlso systemDown.WParam = &HA5 AndAlso systemDown.KeyboardMode.Value = mode,
              mode.ToString() & " preserves RALT system-key identity")
        Check(If(UsesScan(mode), (systemDown.LParam And &HFF0000L) <> 0 AndAlso (systemDown.LParam And &H21000000L) = &H21000000L,
                 systemDown.LParam = 1), mode.ToString() & " system down uses the selected scan/context format")
        ' The original held down determines the release message even if callers ask for WM_KEYUP.
        Check(input.Post(Game, &H101UI, New IntPtr(&HA5), IntPtr.Zero), mode.ToString() & " releases an owned system key")
        Check(platform.Events.Last().Message = &H105UI AndAlso platform.Events.Last().LParam = (systemDown.LParam Or &HC0000000L) AndAlso
              platform.Events.Last().KeyboardMode.Value = mode, mode.ToString() & " system release preserves original context, method and transition bits")
    End Sub

    Private Sub VerifyProductionKeysAndCancellation(mode As BackgroundKeyboardMode)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(mode, platform)
        Dim originalInput = WindowsInput.Current
        WindowsInput.Current = input
        Try
            For Each sample In {("1", 49L), ("F12", &H7BL), ("A", 65L), ("RALT", &HA5L)}
                platform.Events.Clear()
                Check(BotEngine.SendKey(Game, sample.Item1, 12), mode.ToString() & " production helper sends " & sample.Item1)
                CheckPair(platform.Events, mode, Game, sample.Item2, mode.ToString() & " production " & sample.Item1)
            Next
            For Each sample In {("CTRL+1", 17L, 49L), ("ALT+0", 18L, 48L)}
                platform.Events.Clear()
                Check(BotEngine.SendKey(Game, sample.Item1, 12), mode.ToString() & " production helper sends " & sample.Item1)
                Check(platform.Events.Select(Function(value) value.WParam).SequenceEqual({sample.Item2, sample.Item3, sample.Item3, sample.Item2}),
                      mode.ToString() & " chord releases digit before modifier")
                CheckPair(platform.Events.Where(Function(value) value.WParam = sample.Item2), mode, Game, sample.Item2, mode.ToString() & " chord modifier")
                CheckPair(platform.Events.Where(Function(value) value.WParam = sample.Item3), mode, Game, sample.Item3, mode.ToString() & " chord digit")
            Next
            platform.Events.Clear()
            Check(Not BotEngine.SendKey(Game, "not-a-key", 12) AndAlso platform.Events.Count = 0, mode.ToString() & " unsupported production keys emit nothing")
            Using cancelled As New CancellationTokenSource
                cancelled.Cancel()
                Check(Not BotEngine.SendKey(Game, "1", 12, cancellationToken:=cancelled.Token) AndAlso platform.Events.Count = 0,
                      mode.ToString() & " pre-cancelled keys emit nothing")
            End Using
            For Each key In {"1", "CTRL+1", "ALT+1"}
                platform.Events.Clear()
                Dim downs = If(key.Contains("+"), 2, 1)
                Using cancellation As New CancellationTokenSource
                    platform.BeforeSend = Sub(message)
                                              If message = &H100UI AndAlso platform.Events.Count = downs - 1 Then cancellation.Cancel()
                                          End Sub
                    Check(Not BotEngine.SendKey(Game, key, 5000, cancellationToken:=cancellation.Token), mode.ToString() & " reports cancellation for " & key)
                    platform.BeforeSend = Nothing
                    Check(platform.Events.Count = downs * 2, mode.ToString() & " cancellation finishes every down/up pair for " & key)
                    For Each group In platform.Events.GroupBy(Function(value) value.WParam)
                        CheckPair(group, mode, Game, group.Key, mode.ToString() & " cancelled " & key)
                    Next
                    Dim before = platform.Events.Count
                    input.ReleaseAll()
                    Check(platform.Events.Count = before, mode.ToString() & " cancelled " & key & " leaves no pending hold")
                End Using
            Next
        Finally
            platform.BeforeSend = Nothing
            input.ReleaseAll()
            WindowsInput.Current = originalInput
        End Try
    End Sub

    Private Sub ExpectBlocked(action As Action, context As String)
        Try
            action()
        Catch ex As InvalidOperationException
            Check(True, context)
            Return
        End Try
        Throw New Exception("Background keyboard methods: " & context)
    End Sub

    Private Sub VerifyKeyboardOnlyScope(mode As BackgroundKeyboardMode)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(mode, platform)
        Check(Not input.MoveCursor(20, 20), mode.ToString() & " keyboard-only rejects cursor movement")
        For Each message In {&H102UI, &H200UI, &H201UI, &H202UI, &H204UI, &H205UI, &H207UI, &H208UI, &H20AUI, &H20BUI, &H20CUI, &H20EUI, &H999UI}
            Check(Not input.Post(Game, message, New IntPtr(65), IntPtr.Zero), mode.ToString() & " keyboard-only rejects message " & message.ToString())
        Next
        For Each flags In {1UI, 2UI, 4UI, 8UI, 16UI, 32UI, 64UI, 128UI, 256UI, &H800UI, &H1000UI, &HC001UI}
            ExpectBlocked(Sub() input.Mouse(flags, 0, 0, 1, UIntPtr.Zero), mode.ToString() & " keyboard-only blocks mouse flags " & flags.ToString())
        Next
        For Each flags In {4UI, 6UI}
            ExpectBlocked(Sub() input.Keyboard(0, 65, flags, UIntPtr.Zero), mode.ToString() & " keyboard-only blocks Unicode flags " & flags.ToString())
        Next
        Check(platform.Events.Count = 0 AndAlso platform.KeyboardAttempts.Count = 0, mode.ToString() & " blocked non-key operations produce zero platform events")
    End Sub

    Private Sub VerifyModeSwitch(mode As BackgroundKeyboardMode)
        Dim nextMode = CType((CInt(mode) + 1) Mod 4, BackgroundKeyboardMode)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(mode, platform)
        input.Keyboard(65, 255, 0, UIntPtr.Zero)
        Dim originalDown = platform.Events.Last()
        input.Activate(Other)
        Check(input.SetKeyboardMode(nextMode) AndAlso input.KeyboardMode = nextMode, mode.ToString() & " switches after releasing held keys")
        CheckPair(platform.Events, mode, Game, 65, mode.ToString() & " switched release belongs to original game and method")
        platform.Events.Clear()
        input.Keyboard(66, 0, 0, UIntPtr.Zero)
        input.Keyboard(66, 0, 2, UIntPtr.Zero)
        CheckPair(platform.Events, nextMode, Other, 66, mode.ToString() & " next key uses the new method and current game")

        input.SetKeyboardMode(mode)
        input.Activate(Game)
        platform.Events.Clear()
        input.Keyboard(65, 255, 0, UIntPtr.Zero)
        originalDown = platform.Events.Last()
        platform.FailKeyUp = 65
        input.Activate(Other)
        Check(Not input.SetKeyboardMode(nextMode) AndAlso input.KeyboardMode = mode, mode.ToString() & " refuses a switch while release failed")
        Dim failed = platform.KeyboardAttempts.Last()
        Check(platform.Events.Count = 1 AndAlso failed.Hwnd = Game AndAlso failed.Message = &H101UI AndAlso failed.KeyboardMode.Value = mode AndAlso
              failed.LParam = (originalDown.LParam Or &HC0000000L), mode.ToString() & " failed release retains its original owner, method and format")
        platform.FailKeyUp = 0
        input.Maintain()
        CheckPair(platform.Events, mode, Game, 65, mode.ToString() & " maintenance retries original release before selecting another method")
        Check(input.SetKeyboardMode(nextMode) AndAlso input.KeyboardMode = nextMode, mode.ToString() & " permits switch after successful release retry")

        input.Activate(Game)
        platform.Events.Clear()
        input.Keyboard(67, 0, 0, UIntPtr.Zero)
        platform.Pids(Game) = 99
        Check(input.SetKeyboardMode(mode) AndAlso platform.Events.Count = 1, mode.ToString() & " mode switch never sends cleanup to a reused HWND")
        Check(Not input.Activate(Game), mode.ToString() & " repeated binding cannot authorize a reused HWND")
    End Sub
End Module
