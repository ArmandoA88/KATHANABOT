Imports System.Reflection
Imports System.Threading

Friend Module RoleRoutingTests
    Private assertions As Integer
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private ReadOnly StaticFlags As BindingFlags = BindingFlags.Static Or BindingFlags.NonPublic

    Public Function Verify() As Integer
        assertions = 0
        Dim game = New IntPtr(123), other = New IntPtr(456)
        Dim platform As New FakeBackgroundPlatform
        platform.Pids(game) = 11 : platform.Pids(other) = 22
        platform.Origins(game) = New System.Drawing.Point(0, 0)
        platform.Origins(other) = New System.Drawing.Point(300, 0)
        Dim input As New SdlBackgroundWindowsInput(platform, Sub(milliseconds) Return)
        WindowsInput.Current = input
        Try
            Assert(input.Activate(game), "selected background platform binds")
            VerifyKeyMatrix(platform, game)
            VerifyKeyTiming(platform, game)
            VerifyOffenseRoles(platform, game)
            VerifySupportRoles(platform, game)
            VerifyRepairRetargetStop(platform, input, game)
            VerifyHeldKeys(platform, input, game, other)
            VerifyCancellationAndFailures(platform, input, game)
            Return assertions
        Finally
            platform.Accept = True
            platform.FailKeyUp = 0
            platform.BeforeSend = Nothing
            input.ReleaseAll()
            WindowsInput.Current = Nothing
        End Try
    End Function

    Private Sub Assert(condition As Boolean, message As String)
        If Not condition Then Throw New Exception("Role routing: " & message)
        assertions += 1
    End Sub
    Private Function Invoke(engine As BotEngine, name As String, ParamArray arguments As Object()) As Object
        Dim method = GetType(BotEngine).GetMethod(name, InstanceFlags)
        If method Is Nothing Then Throw New Exception("Role dispatcher missing: " & name)
        Return method.Invoke(engine, arguments)
    End Function
    Private Function Configuration(role As String, key As String) As BotConfig
        Return New BotConfig With {.Actions = New List(Of ActionRule) From {
            New ActionRule With {.Role = role, .KeyName = key, .CooldownId = "role-test-" & role, .CooldownMs = 60000, .TriggerPercent = 60}}}
    End Function
    Private Sub AssertPairs(events As IEnumerable(Of TargetEvent), hwnd As IntPtr, key As Long, pairs As Integer, context As String)
        Dim values = events.ToArray()
        Assert(values.Length = pairs * 2, context & " produces complete down/up pairs")
        Assert(values.All(Function(value) value.Hwnd = hwnd AndAlso value.WParam = key), context & " reaches only the selected game and key")
        Assert(values.Where(Function(value, index) index Mod 2 = 0).All(Function(value) value.Message = &H100UI AndAlso value.LParam = 1) AndAlso
               values.Where(Function(value, index) index Mod 2 = 1).All(Function(value) value.Message = &H101UI AndAlso value.LParam = &HC0000001L), context & " uses the inventory virtual-key format")
    End Sub
    Private Sub AssertChord(values As TargetEvent(), hwnd As IntPtr, modifier As Long, digit As Long, context As String)
        Assert(values.Select(Function(value) value.WParam).SequenceEqual({modifier, digit, digit, modifier}), context & " preserves modifier/key/release order")
        Assert(values.Select(Function(value) value.Message).SequenceEqual({&H100UI, &H100UI, &H101UI, &H101UI}) AndAlso
               values.Select(Function(value) value.LParam).SequenceEqual({1L, 1L, &HC0000001L, &HC0000001L}), context & " has zero scan codes and complete cleanup")
        Assert(values.All(Function(value) value.Hwnd = hwnd), context & " remains target-only")
    End Sub

    Private Sub VerifyKeyMatrix(platform As FakeBackgroundPlatform, game As IntPtr)
        For Each key In {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
            platform.Events.Clear()
            Assert(BotEngine.SendKey(game, key, 5), "digit " & key & " sends through production helper")
            AssertPairs(platform.Events, game, AscW(key(0)), 1, "digit " & key)
        Next
        For index = 1 To 24
            platform.Events.Clear()
            Assert(BotEngine.SendKey(game, "F" & index, 5), "function key F" & index & " sends through production helper")
            AssertPairs(platform.Events, game, &H6FL + index, 1, "function key F" & index)
        Next
        For code = AscW("A"c) To AscW("Z"c)
            Dim key = ChrW(code).ToString()
            platform.Events.Clear()
            Assert(BotEngine.SendKey(game, key, 5), "letter " & key & " sends through production helper")
            AssertPairs(platform.Events, game, code, 1, "letter " & key)
        Next
        For Each sample In {("TAB", &H9L), ("SPACE", &H20L), ("ESC", &H1BL), ("ESCAPE", &H1BL),
                            ("ENTER", &HDL), ("RETURN", &HDL), ("RALT", &HA5L), ("RMENU", &HA5L),
                            ("COMMA", &HBCL), (",", &HBCL), ("MINUS", &HBDL), ("-", &HBDL),
                            ("PERIOD", &HBEL), (".", &HBEL), ("SLASH", &HBFL), ("/", &HBFL),
                            ("SEMICOLON", &HBAL), (";", &HBAL), ("APOSTROPHE", &HDEL), ("'", &HDEL),
                            ("EQUALS", &HBBL), ("=", &HBBL), ("GRAVE", &HC0L), ("BACKTICK", &HC0L), ("`", &HC0L)}
            platform.Events.Clear()
            Assert(BotEngine.SendKey(game, sample.Item1, 5), "named key " & sample.Item1 & " sends through production helper")
            AssertPairs(platform.Events, game, sample.Item2, 1, "named key " & sample.Item1)
        Next
        For Each modifier In {"CTRL", "ALT"}
            For Each digit In {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
                platform.Events.Clear()
                Dim key = modifier & "+" & digit
                Assert(BotEngine.SendKey(game, key, 5), key & " sends through the common background backend")
                AssertChord(platform.Events.ToArray(), game, If(modifier = "CTRL", 17L, 18L), AscW(digit(0)), key)
            Next
        Next
        For Each legacyFlags In {(False, False), (True, False), (False, True), (True, True)}
            platform.Events.Clear()
            Assert(BotEngine.SendKey(game, "I", 5, forceBackgroundPost:=legacyFlags.Item1, forcePhysicalKeyEvent:=legacyFlags.Item2), "legacy flags do not require foreground input")
            AssertPairs(platform.Events, game, 73, 1, "legacy flags " & legacyFlags.ToString())
        Next
        platform.Events.Clear()
        Assert(Not BotEngine.SendKey(game, "not-a-key", 5) AndAlso platform.Events.Count = 0, "unsupported keys cannot fall back to global input")
    End Sub

    Private Sub VerifyOffenseRoles(platform As FakeBackgroundPlatform, game As IntPtr)
        For Each role In {"attack", "buff", "special", "high_max_hp"}
            Dim engine As New BotEngine
            Dim cfg = Configuration(role, "1")
            Dim action = cfg.Actions(0)
            Dim args As Object() = {cfg, 100.0R, 100.0R, False, False, True, False, ""}
            platform.Events.Clear()
            Dim selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
            Assert(selected.Count = 0 AndAlso platform.Events.Count = 0, role & " preserves its missing-target condition")
            args(3) = True
            If role = "high_max_hp" Then
                args(5) = False
                selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
                Assert(selected.Count = 0, "high_max_hp preserves the life-reading condition")
                args(5) = True
            End If
            If role = "buff" OrElse role = "high_max_hp" Then
                args(6) = True
                selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
                Assert(selected.Count = 0, role & " preserves the target-filter condition")
                args(6) = False
            End If
            action.Enabled = False
            selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
            Assert(selected.Count = 0, role & " respects disabled rows")
            action.Enabled = True
            selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
            Assert(selected.Count = 1 AndAlso selected(0) Is action, role & " is eligible under its normal conditions")
            Assert(BotEngine.SendKey(game, selected(0).KeyName, 12), role & " uses production SendKey after selection")
            AssertPairs(platform.Events, game, 49, 1, role)
            Invoke(engine, "MarkActionUsed", action)
            selected = DirectCast(Invoke(engine, "ChooseAttackBurstActions", args), List(Of ActionRule))
            Assert(selected.Count = 0, role & " preserves its cooldown after sending")
        Next
    End Sub

    Private Sub VerifyKeyTiming(platform As FakeBackgroundPlatform, game As IntPtr)
        For Each sample In {("1", 12, 140.0R), ("CTRL+1", 12, 140.0R), ("ALT+1", 12, 140.0R), ("1", 300, 280.0R)}
            Dim downAt As Long = -1, upAt As Long = -1
            platform.Events.Clear()
            platform.BeforeSend = Sub(message)
                                      If message = &H100UI Then downAt = Diagnostics.Stopwatch.GetTimestamp()
                                      If message = &H101UI AndAlso upAt < 0 Then upAt = Diagnostics.Stopwatch.GetTimestamp()
                                  End Sub
            Try
                Assert(BotEngine.SendKey(game, sample.Item1, sample.Item2), sample.Item1 & " timed request succeeds")
            Finally
                platform.BeforeSend = Nothing
            End Try
            Assert(downAt >= 0 AndAlso upAt >= downAt, sample.Item1 & " has observable down/up delivery timestamps")
            Dim dwellMs = (upAt - downAt) * 1000.0R / Diagnostics.Stopwatch.Frequency
            Assert(dwellMs >= sample.Item3, $"{sample.Item1} requested {sample.Item2}ms preserves background dwell; observed {dwellMs:0.0}ms")
        Next
    End Sub

    Private Sub VerifySupportRoles(platform As FakeBackgroundPlatform, game As IntPtr)
        For Each role In {"heal", "max_health", "mana"}
            Dim engine As New BotEngine
            Dim cfg = Configuration(role, "F1")
            platform.Events.Clear()
            Assert(Not CBool(Invoke(engine, "TrySendSupportActions", cfg, game, 90.0R, 90.0R, Nothing, Nothing)) AndAlso platform.Events.Count = 0,
                   role & " does not run above its HP/MP threshold")
            cfg.Actions(0).Enabled = False
            Assert(Not CBool(Invoke(engine, "TrySendSupportActions", cfg, game, 50.0R, 50.0R, Nothing, Nothing)), role & " respects disabled rows")
            cfg.Actions(0).Enabled = True
            Assert(CBool(Invoke(engine, "TrySendSupportActions", cfg, game, 50.0R, 50.0R, Nothing, Nothing)), role & " dispatches when its threshold is reached")
            AssertPairs(platform.Events, game, 112, 1, role)
            Assert(engine.GetStatus().LastAction = "F1 (" & role & ")", role & " records the actual configured action")
            Dim before = platform.Events.Count
            Assert(Not CBool(Invoke(engine, "TrySendSupportActions", cfg, game, 50.0R, 50.0R, Nothing, Nothing)) AndAlso platform.Events.Count = before,
                   role & " cannot bypass cooldown")
        Next
    End Sub

    Private Sub VerifyRepairRetargetStop(platform As FakeBackgroundPlatform, input As SdlBackgroundWindowsInput, game As IntPtr)
        Dim repair As New BotEngine
        Dim repairCfg = Configuration("repair", "F2")
        platform.Events.Clear()
        GetType(BotEngine).GetField("_repairConfirmCount", InstanceFlags).SetValue(repair, 4)
        Assert(Not CBool(Invoke(repair, "TrySendRepairAction", repairCfg, game)) AndAlso platform.Events.Count = 0, "repair retains five-read confirmation")
        GetType(BotEngine).GetField("_repairConfirmCount", InstanceFlags).SetValue(repair, 5)
        Assert(CBool(Invoke(repair, "TrySendRepairAction", repairCfg, game)), "confirmed repair dispatches")
        AssertPairs(platform.Events, game, 113, 1, "repair")
        Dim before = platform.Events.Count
        Assert(Not CBool(Invoke(repair, "TrySendRepairAction", repairCfg, game)) AndAlso platform.Events.Count = before, "repair latch blocks repeated use")

        Dim retarget As New BotEngine
        Dim retargetCfg = Configuration("retarget", "F3")
        retargetCfg.NormalRetargetEnabled = False
        retargetCfg.ForcedRetargetEnabled = False
        retargetCfg.Actions(0).Enabled = False
        platform.Events.Clear()
        Assert(Not CBool(Invoke(retarget, "TrySendRetargetKey", game, retargetCfg, DateTime.UtcNow, "regression", False)), "configured retarget respects disabled row")
        retargetCfg.Actions(0).Enabled = True
        Assert(CBool(Invoke(retarget, "TrySendRetargetKey", game, retargetCfg, DateTime.UtcNow, "regression", False)), "configured retarget dispatches when automatic retarget is disabled")
        AssertPairs(platform.Events, game, 114, 1, "configured retarget")
        before = platform.Events.Count
        Assert(Not CBool(Invoke(retarget, "TrySendRetargetKey", game, retargetCfg, DateTime.UtcNow, "regression", False)) AndAlso platform.Events.Count = before, "configured retarget preserves cooldown")

        Dim stopEngine As New BotEngine
        Dim stopCfg = Configuration("stop", "F4")
        stopCfg.Actions(0).Enabled = False
        platform.Events.Clear()
        Assert(Not CBool(Invoke(stopEngine, "TrySendStopAction", stopCfg, game, "regression", False)), "disabled stop row cannot send")
        stopCfg.Actions(0).Enabled = True
        Assert(CBool(Invoke(stopEngine, "TrySendStopAction", stopCfg, game, "regression", False)), "stop role dispatches its three-press sequence")
        AssertPairs(platform.Events, game, 115, 3, "stop")
        platform.Events.Clear()
        input.Activate(game)
        input.Keyboard(&H25, 0, 1, UIntPtr.Zero)
        platform.Events.Clear()
        Assert(CBool(Invoke(stopEngine, "TrySendStopAction", New BotConfig With {.Actions = New List(Of ActionRule)}, game, "owned movement cleanup", True)), "stop fallback cleans up held movement")
        Assert(platform.Events.Count = 1 AndAlso platform.Events(0).Message = &H101UI AndAlso platform.Events(0).WParam = &H25 AndAlso platform.Events(0).LParam = &HC0000001L,
               "stop fallback releases only the bot-owned movement key through SDL")
    End Sub

    Private Sub VerifyHeldKeys(platform As FakeBackgroundPlatform, input As SdlBackgroundWindowsInput, game As IntPtr, other As IntPtr)
        For Each key As Byte In {CByte(&H25), CByte(&H26), CByte(&H27), CByte(&H28), CByte(&HA5)}
            platform.Events.Clear()
            input.Activate(game)
            input.Keyboard(key, 0, 1, UIntPtr.Zero)
            input.Activate(other)
            input.Keyboard(key, 0, 3, UIntPtr.Zero)
            AssertPairs(platform.Events, game, key, 1, "held key 0x" & key.ToString("X2"))
        Next
        input.Activate(game)
        platform.Events.Clear()
        input.Keyboard(&HA5, 0, 1, UIntPtr.Zero)
        input.ReleaseTarget(other)
        Assert(platform.Events.Count = 1, "stopping another game preserves the held RALT key")
        input.ReleaseTarget(game)
        AssertPairs(platform.Events, game, &HA5, 1, "target stop preserves RALT identity")
    End Sub

    Private Sub VerifyCancellationAndFailures(platform As FakeBackgroundPlatform, input As SdlBackgroundWindowsInput, game As IntPtr)
        platform.Events.Clear()
        Using cancelled As New CancellationTokenSource
            cancelled.Cancel()
            Assert(Not BotEngine.SendKey(game, "1", 5, cancellationToken:=cancelled.Token) AndAlso platform.Events.Count = 0, "pre-cancelled skill sends no input")
        End Using
        For Each key In {"1", "CTRL+1", "ALT+1"}
            platform.Events.Clear()
            Using cancellation As New CancellationTokenSource
                platform.BeforeSend = Sub(message)
                                          If message = &H100UI Then cancellation.Cancel()
                                      End Sub
                Assert(Not BotEngine.SendKey(game, key, 100, cancellationToken:=cancellation.Token), key & " reports cancellation during its hold")
                platform.BeforeSend = Nothing
                Dim events = platform.Events.ToArray()
                Assert(events.Length > 0 AndAlso events.Last().Message = &H101UI, key & " releases after cancellation")
                Assert(events.GroupBy(Function(value) value.WParam).All(Function(group) group.Count(Function(value) value.Message = &H100UI) = group.Count(Function(value) value.Message = &H101UI)),
                       key & " releases every owned digit and modifier")
                Dim before = platform.Events.Count
                input.ReleaseAll()
                Assert(platform.Events.Count = before, key & " leaves no pending held input")
            End Using
        Next
        platform.Events.Clear()
        platform.FailKeyUp = 49
        Dim reportedFailure As Boolean
        Try
            reportedFailure = Not BotEngine.SendKey(game, "CTRL+1", 5)
        Catch ex As InvalidOperationException
            reportedFailure = True
        End Try
        Assert(reportedFailure, "failed chord release is reported to its caller")
        Assert(platform.Events.Last().Message = &H101UI AndAlso platform.Events.Last().WParam = 17, "modifier cleanup still runs when digit release fails")
        platform.FailKeyUp = 0
        input.Maintain()
        Assert(platform.Events.Last().Message = &H101UI AndAlso platform.Events.Last().WParam = 49, "failed digit cleanup retries through its original game target")
        platform.Events.Clear()
        platform.Accept = False
        Assert(Not BotEngine.SendKey(game, "2", 5), "failed digit down is not reported as a successful skill")
        platform.Accept = True
        input.Maintain()
        Assert(platform.Events.Count = 1 AndAlso platform.Events(0).Message = &H101UI AndAlso platform.Events(0).WParam = 50, "uncertain failed digit down retains bounded cleanup")
    End Sub
End Module
