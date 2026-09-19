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
        Console.WriteLine($"PASS: {passed} combat scheduling, timing, mode and injected-input assertions.")
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
        Public Messages As New List(Of UInteger)
        Public Accept As Boolean = True
        Public Function Post(hwnd As IntPtr, message As UInteger, w As IntPtr, l As IntPtr) As Boolean Implements IWindowsInput.Post
            Messages.Add(message)
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
