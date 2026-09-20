Imports System.Reflection

Module Program
    Private ReadOnly Flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private passed As Integer
    Sub Main()
        Dim clock As New FakeClock
        Dim engine As New BotEngine With {.Clock = clock}
        Dim attack As New ActionRule With {.KeyName = "1", .Role = "attack", .CooldownMs = 1000, .CooldownId = "attack"}
        Check(Ready(engine, attack), "new action is ready")
        Used(engine, attack)
        clock.NowTick = 999
        Check(Not Ready(engine, attack), "cooldown remains blocked before boundary")
        clock.NowTick = 1000
        Check(Ready(engine, attack), "cooldown ready at exact boundary without sleeping")
        Used(engine, attack)
        Dim heal As New ActionRule With {.KeyName = "1", .Role = "heal", .CooldownMs = 1, .CooldownId = "heal", .Priority = 999}
        Check(Not Ready(engine, heal), "different roles cannot reuse a busy physical key")
        Dim independent As New ActionRule With {.KeyName = "2", .CooldownMs = 100}
        Check(Ready(engine, independent), "independent keys keep independent cooldowns")
        Check(ActionPriorityPolicy.Ordered({attack, heal})(0) Is heal, "urgent healing precedes attack regardless of user priority")
        Check(ActionPriorityPolicy.UrgentHealth(0, {attack}), "death defers optional OCR")
        Check(ActionPriorityPolicy.UrgentHealth(15, {heal}), "triggered healing defers optional OCR")
        Check(Not ActionPriorityPolicy.UrgentHealth(90, {heal}), "healthy frame allows optional work")
        Check(Not ActionPriorityPolicy.CanRunOffense(False, True, False), "recovery reserves the frame before attacks")
        Check(Not ActionPriorityPolicy.CanRunOffense(False, False, True), "movement cannot interrupt an attack in the same frame")
        Check(Not ActionPriorityPolicy.CanRunOptional(False, True, False), "urgent health blocks optional loot and buffs")
        Check(Not ActionPriorityPolicy.CanRunOptional(True, False, False), "death blocks optional actions")
        Check(ActionPriorityPolicy.CanRunOptional(False, False, False), "idle healthy frame permits optional actions")
        Dim deadline As New FrameDeadline(clock, 200)
        clock.NowTick += 199
        Check(deadline.IsFresh, "OCR frame accepted before deadline")
        clock.NowTick += 1
        Check(Not deadline.IsFresh, "queued/finished OCR rejected at deadline")
        Dim mode As New OperatingModeController
        Check(mode.Transition(OperatingMode.Trade, "test"), "idle can enter Trade")
        Check(Not mode.Transition(OperatingMode.Resu, "competing"), "competing workflow rejected")
        Check(mode.Transition(OperatingMode.Idle, "completed") AndAlso mode.Transition(OperatingMode.Full, "resume"), "explicit completion permits combat resume")
        Dim fake As New FakeInput
        WindowsInput.Current = fake
        Try
            Check(BotEngine.SendKey(New IntPtr(123), "1", 5, forceBackgroundPost:=True), "fake input accepts key")
            Check(fake.Messages.SequenceEqual({&H100UI, &H101UI}), "key down/up reach injected input in order")
            fake.Accept = False
            Check(Not BotEngine.SendKey(New IntPtr(123), "1", 5, forceBackgroundPost:=True), "failed Windows input is not reported as success")
        Finally
            WindowsInput.Current = Nothing
        End Try
        Dim cfg As New BotConfig With {.Actions = New List(Of ActionRule) From {
            New ActionRule With {.KeyName = "3", .Role = "buff", .Priority = 1},
            New ActionRule With {.KeyName = "2", .Role = "attack", .Priority = 100},
            New ActionRule With {.KeyName = "2", .Role = "attack", .Priority = 101}}}
        Dim args As Object() = {cfg, 100.0R, 100.0R, True, False, False, False, ""}
        Dim chosen = DirectCast(GetType(BotEngine).GetMethod("ChooseAttackBurstActions", Flags).Invoke(engine, args), List(Of ActionRule))
        Check(chosen.Count > 0 AndAlso chosen(0).KeyName = "2", "production attack selection uses central category priority")
        Check(chosen.Select(Function(a) a.KeyName).Distinct().Count() = chosen.Count, "production burst deduplicates competing keys")
        args(3) = False
        chosen = DirectCast(GetType(BotEngine).GetMethod("ChooseAttackBurstActions", Flags).Invoke(engine, args), List(Of ActionRule))
        Check(chosen.Count = 0 AndAlso CStr(args(7)).Contains("No target"), "missing target blocks attack with a reason")
        Check(RuntimeJournal.Snapshot().Any(Function(item) item.Kind = "Skill skipped"), "skip reason reaches timeline")
        TestAutoAssistOnly()
        TestLootAfterKill()
        Console.WriteLine($"PASS: {passed} combat scheduling, timing, mode and injected-input assertions.")
    End Sub
    Private Sub TestAutoAssistOnly()
        Dim engine As New BotEngine
        Dim input As New FakeInput
        Dim cfg As New BotConfig With {.AutoAssistOnlyEnabled = True}
        Dim update = GetType(BotEngine).GetMethod("UpdateAutoAssistOutput", Flags)
        WindowsInput.Current = input
        Try
            update.Invoke(engine, {New IntPtr(321), cfg})
            Check(Not BotEngine.SendKey(New IntPtr(321), "E", 5), "assist-only blocks retarget E")
            Check(Not BotEngine.SendKey(New IntPtr(321), " e ", 5, forcePhysicalKeyEvent:=True), "assist-only blocks physical E and alternate casing")
            Check(input.Messages.Count = 0, "blocked E never reaches Windows")
            For Each key In {"R", "F", "1", "F1"}
                Check(BotEngine.SendKey(New IntPtr(321), key, 5), "assist-only preserves " & key)
            Next
            Check(BotEngine.SendKey(New IntPtr(999), "E", 5), "assist-only is scoped to selected game")
            cfg.AutoAssistOnlyEnabled = False
            update.Invoke(engine, {New IntPtr(321), cfg})
            Check(BotEngine.SendKey(New IntPtr(321), "E", 5), "toggle off restores E")
            cfg.AutoAssistOnlyEnabled = True
            update.Invoke(engine, {New IntPtr(321), cfg})
            update.Invoke(engine, {New IntPtr(654), cfg})
            Check(BotEngine.SendKey(New IntPtr(321), "E", 5) AndAlso Not BotEngine.SendKey(New IntPtr(654), "E", 5), "window switch moves E block")
        Finally
            update.Invoke(engine, {IntPtr.Zero, Nothing})
            WindowsInput.Current = Nothing
        End Try
    End Sub
    Private Sub TestLootAfterKill()
        Dim engine As New BotEngine
        Dim input As New FakeInput
        Dim cfg As New BotConfig With {.LootAfterKillEnabled = True, .LootScannerEnabled = False, .LootPickupEnabled = False}
        Dim method = GetType(BotEngine).GetMethod("TryHandleLootAfterKill", Flags)
        Dim now = DateTime.UtcNow
        Dim tick As Action(Of Boolean, Integer, Boolean) = Sub(alive, ms, reliable) method.Invoke(engine, {cfg, New IntPtr(123), alive, now.AddMilliseconds(ms), reliable})
        WindowsInput.Current = input
        Try
            tick(False, 0, True)
            Check(input.Messages.Count = 0, "no post-kill pickup without an attacked living mob")
            GetType(BotEngine).GetField("_lastAttackAction", Flags).SetValue(engine, now)
            tick(True, 0, True)
            tick(True, 10000, True)
            tick(False, 10100, False)
            Check(input.Messages.Count = 0, "unreliable HP does not trigger pickup")
            tick(False, 10200, True)
            Check(input.Keys.SequenceEqual({70, 70}), "zero/absent HP sends F without scanner after a long fight")
            tick(False, 10300, True)
            Check(input.Messages.Count = 2, "one F per death transition")
            GetType(BotEngine).GetField("_lastAttackAction", Flags).SetValue(engine, now.AddMilliseconds(11000))
            tick(True, 11000, True)
            input.Accept = False
            tick(False, 11100, True)
            input.Accept = True
            tick(False, 11200, True)
            Check(input.Messages.Count = 5, "failed F is retried rather than dropping the pickup")
            tick(True, 12000, True)
            cfg.LootAfterKillEnabled = False
            tick(False, 12100, True)
            cfg.LootAfterKillEnabled = True
            tick(False, 12200, True)
            Check(input.Messages.Count = 5, "disabled pickup clears pending death")
            tick(True, 13000, True)
            tick(False, 17000, True)
            Check(input.Messages.Count = 5, "stale target disappearance does not loot")
        Finally
            WindowsInput.Current = Nothing
        End Try
    End Sub
    Private Function Ready(engine As BotEngine, action As ActionRule) As Boolean
        Return CBool(GetType(BotEngine).GetMethod("IsReady", Flags, Nothing, {GetType(ActionRule)}, Nothing).Invoke(engine, {action}))
    End Function
    Private Sub Used(engine As BotEngine, action As ActionRule)
        GetType(BotEngine).GetMethod("MarkActionUsed", Flags).Invoke(engine, {action})
    End Sub
    Private Sub Check(value As Boolean, message As String)
        If Not value Then Throw New Exception(message)
        passed += 1
    End Sub
    Private Class FakeClock
        Implements IBotClock
        Public NowTick As Long
        Public ReadOnly Property Tick As Long Implements IBotClock.Tick
            Get
                Return NowTick
            End Get
        End Property
        Public ReadOnly Property UtcNow As DateTime Implements IBotClock.UtcNow
            Get
                Return New DateTime(2026, 1, 1).AddMilliseconds(NowTick)
            End Get
        End Property
        Public Sub Delay(milliseconds As Integer) Implements IBotClock.Delay
            NowTick += milliseconds
        End Sub
    End Class
    Private Class FakeInput
        Implements IWindowsInput
        Public Keys As New List(Of Integer)
        Public Messages As New List(Of UInteger)
        Public Accept As Boolean = True
        Public Function Post(hwnd As IntPtr, message As UInteger, w As IntPtr, l As IntPtr) As Boolean Implements IWindowsInput.Post
            Messages.Add(message)
            Keys.Add(w.ToInt32())
            Return Accept
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Return True
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Return True
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
        End Sub
    End Class
End Module
