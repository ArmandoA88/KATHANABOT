Imports System.Drawing
Imports System.IO

Module Program
    Private count As Integer
    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
        count += 1
    End Sub
    Private Sub ExpectFailure(action As Action, message As String)
        Try
            action()
        Catch ex As InvalidOperationException
            Check(True, message)
            Return
        End Try
        Throw New Exception(message)
    End Sub
    Sub Main()
        Dim game = New IntPtr(123), second = New IntPtr(456)
        Dim backend As New FakeBackgroundPlatform
        backend.Pids(game) = 11 : backend.Pids(second) = 22
        backend.Origins(game) = New Point(-900, 200) : backend.Origins(second) = New Point(400, 300)
        Dim delays As New List(Of Integer)
        Dim input As New SdlBackgroundWindowsInput(backend, Sub(ms) delays.Add(ms), Function() New Rectangle(-1920, 0, 3840, 1080))
        Check(Not input.MoveCursor(-880, 220), "unbound cursor is rejected")
        Check(input.Activate(game) AndAlso input.CanReceive(game), "background target validates without any activation API")
        Check(input.Post(game, &H100UI, New IntPtr(73), New IntPtr(1 Or (&H17 << 16))), "inventory-style key down succeeds")
        Check(backend.Events.Last().Hwnd = game AndAlso backend.Events.Last().LParam = 1 AndAlso backend.Events.Last().Message = &H100UI, "incoming scan code stripped at target message boundary")
        Check(input.Post(game, &H101UI, New IntPtr(73), IntPtr.Zero), "owned key release succeeds")
        Check(backend.Events.Last().LParam = &HC0000001L AndAlso backend.Events.Last().Message = &H101UI, "virtual-key up uses repeat and transition bits with zero scan")
        Dim before = backend.Events.Count
        input.Keyboard(74, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Count = before, "unowned key up does not reach game")

        input.Keyboard(65, 0, 0, UIntPtr.Zero)
        input.Activate(second)
        input.Keyboard(65, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Last().Hwnd = game, "retargeted key release stays at original owner")
        input.Keyboard(66, 0, 0, UIntPtr.Zero)
        input.Activate(game)
        input.Keyboard(66, 0, 0, UIntPtr.Zero)
        input.Keyboard(66, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Last().Hwnd = game, "same key in two games releases current owner's hold")
        input.ReleaseTarget(game)
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before, "healthy hold in another game survives target stop")
        input.ReleaseTarget(second)
        Check(backend.Events.Last().Hwnd = second AndAlso backend.Events.Last().Message = &H101UI, "stop releases second game's own key")

        input.Activate(game)
        input.Keyboard(67, 0, 0, UIntPtr.Zero)
        backend.Pids(game) = 33
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before AndAlso Not input.CanReceive(game), "HWND PID reuse clears stale ownership without sending to replacement")
        Check(Not input.Post(game, &H100UI, New IntPtr(68), IntPtr.Zero) AndAlso backend.Events.Count = before, "repeating stale binding cannot authorize reused HWND")
        backend.Pids(game) = 11
        input.Keyboard(68, 0, 0, UIntPtr.Zero)
        backend.Available(game) = False
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before, "minimized target does not receive input")
        backend.Available(game) = True
        input.Maintain()
        Check(backend.Events.Last().Message = &H101UI AndAlso backend.Events.Last().WParam = 68, "temporarily unavailable original target gets deferred owned cleanup")

        backend.FailKeyUp = 69
        input.Keyboard(69, 0, 0, UIntPtr.Zero)
        ExpectFailure(Sub() input.Keyboard(69, 0, 2, UIntPtr.Zero), "failed up is reported")
        backend.FailKeyUp = 0
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before + 1 AndAlso backend.Events.Last().WParam = 69, "failed release retries while target remains available")
        backend.Accept = False
        ExpectFailure(Sub() input.Keyboard(70, 0, 0, UIntPtr.Zero), "timed-out down is treated as uncertain acceptance")
        backend.Accept = True
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before + 1 AndAlso backend.Events.Last().Message = &H101UI AndAlso backend.Events.Last().WParam = 70, "uncertain down retains only pending cleanup")

        Check(input.MoveCursor(-850, 260), "virtual cursor accepts negative screen coordinates within client")
        Check(backend.Events.Last().Message = &H200UI AndAlso backend.Events.Last().LParam = Pack(50, 60), "screen cursor converts into target client coordinates")
        before = backend.Events.Count
        Check(Not input.MoveCursor(0, 0) AndAlso backend.Events.Count = before, "outside client movement is blocked before dispatch")
        input.Mouse(2, 0, 0, 0, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H201UI AndAlso backend.Events.Last().WParam = 1 AndAlso backend.Events.Last().LParam = Pack(50, 60), "left down uses virtual client cursor and button mask")
        input.Activate(second)
        input.Mouse(4, 0, 0, 0, UIntPtr.Zero)
        Check(backend.Events.Last().Hwnd = game AndAlso backend.Events.Last().Message = &H202UI AndAlso backend.Events.Last().WParam = 0, "retargeted mouse up is sent only to original client")
        input.Activate(game)
        Check(input.Post(game, &H204UI, IntPtr.Zero, New IntPtr(Pack(12, 14))), "message-shaped right down respects client coordinates")
        Check(backend.Events.Last().Message = &H204UI AndAlso backend.Events.Last().LParam = Pack(12, 14), "right down reaches specified client point")
        Check(input.Post(game, &H205UI, IntPtr.Zero, IntPtr.Zero), "right owned release")
        input.Mouse(32, 0, 0, 0, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H207UI AndAlso backend.Events.Last().WParam = 16, "middle button supported")
        input.Mouse(64, 0, 0, 0, UIntPtr.Zero)
        input.Mouse(128, 0, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H20BUI AndAlso backend.Events.Last().WParam = (&H20000L Or 64L), "X button id and state encoded independently")
        input.Mouse(256, 0, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H20CUI AndAlso backend.Events.Last().WParam = &H20000L, "X release clears owned button state")
        input.Mouse(1, 5, CUInt(&HFFFFFFFDUI), 0, UIntPtr.Zero)
        Check(backend.Events.Last().LParam = Pack(17, 11), "relative motion applies signed offsets to virtual cursor")
        input.Mouse(&H800UI, 0, 0, 120, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H20AUI AndAlso backend.Events.Last().WParam = &H780000L AndAlso backend.Events.Last().LParam = Pack(-883, 211), "wheel carries virtual screen point and signed delta")
        input.Mouse(&H1000UI, 0, 0, &HFFFFFF88UI, UIntPtr.Zero)
        Check(backend.Events.Last().Message = &H20EUI AndAlso (backend.Events.Last().WParam >> 16) = &HFF88L, "negative horizontal wheel delta preserved")
        Dim nx = CUInt(Math.Round((-850 + 1920) * 65535.0 / 3839)), ny = CUInt(Math.Round(260 * 65535.0 / 1079))
        input.Mouse(&HC001UI, nx, ny, 0, UIntPtr.Zero)
        Check(backend.Events.Last().LParam = Pack(50, 60), "absolute virtual-desktop motion maps back to game client")
        backend.Origins(game) = New Point(-700, 100)
        input.Mouse(2, 0, 0, 0, UIntPtr.Zero)
        Check(backend.Events.Last().LParam = Pack(50, 60), "moving the window preserves virtual cursor position inside its client")
        input.Mouse(4, 0, 0, 0, UIntPtr.Zero)
        input.Mouse(&H800UI, 0, 0, 120, UIntPtr.Zero)
        Check(backend.Events.Last().LParam = Pack(-650, 160), "wheel screen coordinates follow the moved window")
        backend.Origins(game) = New Point(-900, 200)
        before = backend.Events.Count
        ExpectFailure(Sub() input.Mouse(&H2000UI, 0, 0, 0, UIntPtr.Zero), "unsupported flags fail before dispatch")
        Check(backend.Events.Count = before, "unsupported mouse flags have no partial effects")

        input.Keyboard(&HA3, 0, 0, UIntPtr.Zero)
        input.Keyboard(49, 0, 0, UIntPtr.Zero)
        input.Mouse(2, 0, 0, 0, UIntPtr.Zero)
        Check(backend.Events.Last().WParam = 9, "Ctrl chord reflected in mouse state")
        input.Mouse(4, 0, 0, 0, UIntPtr.Zero)
        input.Keyboard(49, 0, 2, UIntPtr.Zero)
        input.Keyboard(&HA3, 0, 2, UIntPtr.Zero)
        Check(backend.Events.TakeLast(2).Select(Function(e) e.WParam).SequenceEqual({49L, &HA3L}), "explicit right Ctrl uses ordered virtual-key releases")
        Check(input.Post(game, &H102UI, New IntPtr(&H263A), IntPtr.Zero), "full UTF-16 char accepted via Post")
        Check(backend.Events.Last().Message = &H102UI AndAlso backend.Events.Last().WParam = &H263A, "Unicode WM_CHAR payload preserves code unit")
        before = backend.Events.Count
        input.Keyboard(0, 233, 4, UIntPtr.Zero)
        input.Keyboard(0, 233, 6, UIntPtr.Zero)
        Check(backend.Events.Count = before + 1 AndAlso backend.Events.Last().Message = &H102UI, "Unicode compatibility emits one char on down and no duplicate up")
        input.Keyboard(80, 0, 0, UIntPtr.Zero)
        input.Mouse(8, 0, 0, 0, UIntPtr.Zero)
        input.ReleaseAll()
        Check(backend.Events.TakeLast(2).All(Function(e) e.Message = &H101UI OrElse e.Message = &H205UI), "shutdown releases all owned key and mouse inputs")
        before = backend.Events.Count
        input.Maintain()
        Check(backend.Events.Count = before, "successful cleanup is idempotent")
        Check(delays.Count > 0 AndAlso delays.All(Function(ms) ms >= 5 AndAlso ms <= 15), "action pacing remains bounded")
        Check(Not input.Post(game, &H999UI, IntPtr.Zero, IntPtr.Zero), "unsupported message has no generic dispatch fallback")
        VerifyMaintenanceDoesNotQueue(game)
        AuditPlatform()
        count += KeyHoldTimingTests.Verify()
        count += KeyboardModeTests.Verify()
        count += ModifierRoutingTests.Verify()
        count += RoleRoutingTests.Verify()
        Console.WriteLine($"PASS: {count} SDL background target ownership, four keyboard methods, keyboard-only scope, key hold timing, failure cleanup, coordinates, modifiers and Unicode assertions.")
    End Sub
    Private Function Pack(x As Integer, y As Integer) As Long
        Return (CLng(x) And &HFFFFL) Or ((CLng(y) And &HFFFFL) << 16)
    End Function
    Private Sub VerifyMaintenanceDoesNotQueue(game As IntPtr)
        Dim backend As New FakeBackgroundPlatform
        backend.Pids(game) = 11
        backend.Origins(game) = New Point(0, 0)
        Dim input As New SdlBackgroundWindowsInput(backend, Sub(ms) Return)
        Using entered As New Threading.ManualResetEventSlim, finish As New Threading.ManualResetEventSlim
            backend.BeforeSend = Sub(message)
                                     If message <> &H100UI Then Return
                                     entered.Set()
                                     If Not finish.Wait(3000) Then Throw New Exception("Controlled send did not resume")
                                 End Sub
            Dim sender = Threading.Tasks.Task.Run(Sub()
                                                     input.Activate(game)
                                                     input.Keyboard(65, 0, 0, UIntPtr.Zero)
                                                 End Sub)
            Try
                Check(entered.Wait(2000), "controlled send reaches its waiting state")
                Dim maintenance = Threading.Tasks.Task.Run(Sub() input.Maintain())
                Check(maintenance.Wait(500), "maintenance returns while another game send waits instead of queueing timer callbacks")
            Finally
                finish.Set()
                If Not sender.Wait(2000) Then Throw New Exception("Controlled send failed to finish")
            End Try
        End Using
        input.ReleaseAll()
    End Sub
    Private Sub AuditPlatform()
        Dim sourceRoot = New DirectoryInfo(AppContext.BaseDirectory)
        While sourceRoot IsNot Nothing AndAlso Not Directory.Exists(Path.Combine(sourceRoot.FullName, "ui", "KathanaBotControlPanel"))
            sourceRoot = sourceRoot.Parent
        End While
        If sourceRoot Is Nothing Then Throw New Exception("Cannot locate SDL input source")
        Dim source = File.ReadAllText(Path.Combine(sourceRoot.FullName, "ui", "KathanaBotControlPanel", "SdlBackgroundWindowsInput.vb"))
        For Each forbidden In {"SendInput(", "SetForegroundWindow(", "RawSetForegroundWindow(", "SetCursorPos(", "AttachThreadInput("}
            Check(Not source.Contains(forbidden), "background backend cannot invoke " & forbidden)
        Next
        Check(source.Contains("SendMessageTimeoutW") AndAlso source.Contains("&H23UI, 1000"), "target dispatch has bounded timeout and reentrancy guard")
        Check(source.Contains("EntryPoint:=""PostMessageW""") AndAlso source.Contains("Return PostTargetMessage(hwnd, message, wParam, lParam)"), "posted keyboard methods enqueue only to the selected target")
        Check(source.Contains("NativeHookTransport.GamePath") AndAlso source.Contains("OpenProcess(&H1000UI"), "native platform validates exact game path with limited query rights")
    End Sub
End Module

Friend Structure TargetEvent
    Public Hwnd As IntPtr
    Public Message As UInteger
    Public WParam As Long
    Public LParam As Long
    Public KeyboardMode As BackgroundKeyboardMode?
End Structure
Friend NotInheritable Class FakeBackgroundPlatform
    Inherits BackgroundInputPlatform
    Public ReadOnly Pids As New Dictionary(Of IntPtr, UInteger)
    Public ReadOnly Origins As New Dictionary(Of IntPtr, Point)
    Public ReadOnly Available As New Dictionary(Of IntPtr, Boolean)
    Public ReadOnly Events As New List(Of TargetEvent)
    Public ReadOnly KeyboardAttempts As New List(Of TargetEvent)
    Public Accept As Boolean = True
    Public FailKeyUp As UInteger
    Public BeforeSend As Action(Of UInteger)
    Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
        Dim pid As UInteger
        Pids.TryGetValue(hwnd, pid)
        Return pid
    End Function
    Public Overrides Function Valid(hwnd As IntPtr) As Boolean
        Return Pids.ContainsKey(hwnd) AndAlso (Not Available.ContainsKey(hwnd) OrElse Available(hwnd))
    End Function
    Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Point?
        If Not Origins.ContainsKey(hwnd) OrElse x < 0 OrElse y < 0 OrElse x >= 200 OrElse y >= 150 Then Return Nothing
        Return New Point(Origins(hwnd).X + x, Origins(hwnd).Y + y)
    End Function
    Public Overrides Function ScreenToClient(hwnd As IntPtr, x As Integer, y As Integer) As Point?
        If Not Origins.ContainsKey(hwnd) Then Return Nothing
        Dim client As New Point(x - Origins(hwnd).X, y - Origins(hwnd).Y)
        If Not ClientPoint(hwnd, client.X, client.Y).HasValue Then Return Nothing
        Return client
    End Function
    Public Overrides Function Send(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
        BeforeSend?.Invoke(message)
        If Not Accept OrElse ((message = &H101UI OrElse message = &H105UI) AndAlso FailKeyUp <> 0 AndAlso wParam.ToInt64() = FailKeyUp) Then Return False
        Events.Add(New TargetEvent With {.Hwnd = hwnd, .Message = message, .WParam = wParam.ToInt64(), .LParam = lParam.ToInt64()})
        Return True
    End Function
    Public Overrides Function SendKeyboard(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr, mode As BackgroundKeyboardMode) As Boolean
        KeyboardAttempts.Add(New TargetEvent With {.Hwnd = hwnd, .Message = message, .WParam = wParam.ToInt64(), .LParam = lParam.ToInt64(), .KeyboardMode = mode})
        If Not Send(hwnd, message, wParam, lParam) Then Return False
        Dim value = Events(Events.Count - 1)
        value.KeyboardMode = mode
        Events(Events.Count - 1) = value
        Return True
    End Function
End Class
