Imports System.Drawing
Imports System.IO

Module Program
    Private count As Integer
    Private Sub Check(ok As Boolean, message As String)
        If Not ok Then Throw New Exception(message)
        count += 1
    End Sub
    Sub Main()
        Dim game = New IntPtr(123), other = New IntPtr(456)
        Dim backend As New FakePlatform With {.Active = game}
        Dim input As New ForegroundWindowsInput(backend)
        Check(Not input.MoveCursor(10, 10), "unbound cursor move must fail")
        Check(input.Activate(game), "target binds and verifies foreground")
        Check(input.IsWindowForeground(game), "foreground eligibility does not require a prior binding")
        Check(input.Post(game, &H100UI, New IntPtr(65), New IntPtr(1 Or (&H1E << 16))), "key down")
        Check(backend.Events.Last().Keyboard AndAlso backend.Events.Last().Key = 65 AndAlso backend.Events.Last().Flags = 0, "SendInput keyboard payload")
        Check(input.Post(game, &H101UI, New IntPtr(65), IntPtr.Zero), "key release")
        Check(backend.Events.Last().Flags = 2, "key up flags")
        Check(input.Post(game, &H100UI, New IntPtr(&H25), New IntPtr(&H1000001)), "extended key down")
        Check(backend.Events.Last().Flags = 1, "extended key preserved")
        backend.Active = other
        Check(Not input.IsWindowForeground(game), "foreground eligibility rejects focus loss")
        input.ReleaseUnfocused()
        Check(backend.Events.Last().Flags = 3, "focus loss releases only bot-owned extended key")
        Dim before = backend.Events.Count
        Check(Not input.Post(game, &H100UI, New IntPtr(66), IntPtr.Zero), "background key blocked")
        Check(Not input.Post(game, &H102UI, New IntPtr(67), IntPtr.Zero), "background Unicode blocked")
        Check(Not input.MoveCursor(20, 20), "background cursor blocked")
        Check(Not input.Post(game, &H201UI, IntPtr.Zero, IntPtr.Zero), "background click blocked")
        input.Keyboard(99, 0, 2, UIntPtr.Zero)
        Check(backend.Events.Count = before, "no background events or unowned release leaked")
        backend.AllowActivate = False
        Check(Not input.Activate(game), "failed activation must not permit input")
        backend.AllowActivate = True
        Check(input.Activate(game), "successful activation resumes")
        Check(input.Post(game, &H102UI, New IntPtr(&H263A), IntPtr.Zero), "Unicode character sent")
        Check(backend.Events.TakeLast(2).Select(Function(e) e.Flags).SequenceEqual({4UI, 6UI}), "Unicode down/up pair")
        Check(backend.Events.Last().Key = 0 AndAlso backend.Events.Last().Scan = &H263A, "UTF-16 payload")
        Check(input.MoveCursor(-1000, 50), "negative desktop coordinates accepted")
        Check(backend.Events.Last().Flags = &HC001UI AndAlso backend.Events.Last().X >= 0 AndAlso backend.Events.Last().X <= 65535, "virtual desktop normalization")
        Check(Not input.MoveCursor(-5000, 0), "outside desktop rejected")
        Check(input.Post(game, &H201UI, IntPtr.Zero, New IntPtr(50 Or (60 << 16))), "client-space left down")
        Check(backend.Events.Last().Flags = 2 AndAlso Not backend.Events.Last().Keyboard, "mouse down payload")
        backend.Active = other
        input.ReleaseUnfocused()
        Check(backend.Events.Last().Flags = 4, "focus-loss mouse cleanup")
        backend.Active = game
        backend.Covered = True
        before = backend.Events.Count
        Check(Not input.Post(game, &H204UI, IntPtr.Zero, New IntPtr(50 Or (60 << 16))), "covered click rejected")
        Check(Not backend.Events.Skip(before).Any(Function(e) e.Flags = 8), "no covered right-button down")
        backend.Covered = False
        Check(input.Post(game, &H204UI, IntPtr.Zero, New IntPtr(10 Or (10 << 16))), "right click down")
        Check(input.Post(game, &H205UI, IntPtr.Zero, IntPtr.Zero), "right click up")
        Check(backend.Events.Last().Flags = 16, "right release payload")
        backend.Accept = False
        Check(Not input.Post(game, &H100UI, New IntPtr(70), IntPtr.Zero), "SendInput failure propagated")
        backend.Accept = True
        input.Keyboard(70, 0, 0, UIntPtr.Zero)
        backend.Pid = 2
        before = backend.Events.Count
        input.ReleaseUnfocused()
        Check(backend.Events.Count = before + 1 AndAlso backend.Events.Last().Flags = 2, "reused window PID releases owned key")
        before = backend.Events.Count
        Try
            input.Keyboard(71, 0, 0, UIntPtr.Zero)
            Throw New Exception("reused target accepted")
        Catch ex As InvalidOperationException
        End Try
        Check(backend.Events.Count = before, "PID change blocks stale physical scope")
        input.BindTarget(game)
        backend.Available = False
        Check(Not input.MoveCursor(1, 1), "minimized/closed game blocked")
        Check(Not input.Post(game, &H100UI, New IntPtr(70), IntPtr.Zero), "invalid target key blocked")
        backend.Available = True
        Check(Not input.Post(game, &H999UI, IntPtr.Zero, IntPtr.Zero), "unknown message cannot fall back to PostMessage")
        WindowsInput.BackgroundOnly = True
        Check(Not WindowsInput.BackgroundOnly, "background-only settings cannot reenable old path")
        backend.Available = True
        input.Activate(game)
        input.Keyboard(80, 0, 0, UIntPtr.Zero)
        backend.Accept = False
        Try
            input.Keyboard(80, 0, 2, UIntPtr.Zero)
            Throw New Exception("failed release reported success")
        Catch ex As InvalidOperationException
        End Try
        backend.Accept = True
        before = backend.Events.Count
        input.ReleaseUnfocused()
        Check(backend.Events.Count = before + 1 AndAlso backend.Events.Last().Flags = 2, "release failures retry even while still focused")
        input.Keyboard(81, 0, 0, UIntPtr.Zero)
        input.ReleaseAll()
        Check(backend.Events.Last().Key = 81 AndAlso backend.Events.Last().Flags = 2, "shutdown releases owned input")
        input.Keyboard(82, 0, 0, UIntPtr.Zero)
        input.ReleaseTarget(other)
        Check(backend.Events.Last().Flags = 0, "stopping another target does not release this target's key")
        input.ReleaseTarget(game)
        Check(backend.Events.Last().Key = 82 AndAlso backend.Events.Last().Flags = 2, "target stop releases its held key")
        backend.FailReleaseKey = 49
        input.BindTarget(game)
        WindowsInput.Current = input
        Try
            Dim chord = GetType(BotEngine).GetMethod("SendPhysicalSkillShortcut", Reflection.BindingFlags.Static Or Reflection.BindingFlags.NonPublic)
            Try
                chord.Invoke(Nothing, {17, 49, 5, Threading.CancellationToken.None})
                Throw New Exception("failed chord release was not reported")
            Catch ex As Reflection.TargetInvocationException When TypeOf ex.InnerException Is InvalidOperationException
            End Try
            Check(backend.Events.Last().Key = 17 AndAlso backend.Events.Last().Flags = 2, "modifier release must run even if digit release fails")
            backend.FailReleaseKey = 0
            input.ReleaseUnfocused()
            Check(backend.Events.Last().Key = 49 AndAlso backend.Events.Last().Flags = 2, "failed chord key release is retried")
        Finally
            WindowsInput.Current = Nothing
        End Try
        Dim packetType = GetType(WindowsInput).Assembly.GetType("KathanaBotControlPanel.NativeInputPlatform+InputPacket", True)
        Check(Runtime.InteropServices.Marshal.SizeOf(packetType) = If(IntPtr.Size = 8, 40, 28), "native INPUT structure layout")
        Dim delays As New List(Of Integer)
        Dim paced As New ForegroundWindowsInput(backend, Sub(ms) delays.Add(ms))
        paced.Activate(game)
        paced.Keyboard(65, 0, 0, UIntPtr.Zero)
        paced.Keyboard(65, 0, 2, UIntPtr.Zero)
        paced.Mouse(2, 0, 0, 0, UIntPtr.Zero)
        paced.Mouse(4, 0, 0, 0, UIntPtr.Zero)
        paced.MoveCursor(10, 10)
        Check(delays.Count = 2 AndAlso delays.All(Function(ms) ms >= 5 AndAlso ms <= 15), "only new key/button downs get 5-15 ms delay")
        Dim samples = Enumerable.Range(0, 1000).Select(Function(i) ForegroundWindowsInput.NextActionDelayMs()).ToArray()
        Check(samples.All(Function(ms) ms >= 5 AndAlso ms <= 15) AndAlso samples.Distinct().Count() > 1, "fresh bounded action jitter")
        Dim losingFocus As New ForegroundWindowsInput(backend, Sub(ms) backend.Active = other)
        losingFocus.Activate(game)
        before = backend.Events.Count
        Check(Not losingFocus.Post(game, &H100UI, New IntPtr(65), IntPtr.Zero) AndAlso backend.Events.Count = before, "focus must be rechecked after randomized wait")
        VerifyWindowsInputSelectorAndKeyTiming(game, other)
        AuditNativeImports()
        Console.WriteLine($"PASS: {count} foreground SendInput routing, selector eligibility, key hold timing, focus, releases, cursor, Unicode and native-import assertions.")
    End Sub

    Private Sub VerifyWindowsInputSelectorAndKeyTiming(game As IntPtr, other As IntPtr)
        Dim originalInput = WindowsInput.Current
        Dim backend As New FakePlatform With {.Active = game}
        Dim input As New ForegroundWindowsInput(backend, Sub(milliseconds) Return)
        WindowsInput.Current = input
        Try
            Check(WindowsInput.InputMode = "foreground" AndAlso Not WindowsInput.UsesInternalHook AndAlso Not WindowsInput.UsesTargetedInput,
                  "selected foreground backend reports physical input mode")
            Check(WindowsInput.TargetIsForeground(game) AndAlso WindowsInput.TargetCanReceiveInput(game),
                  "selector accepts the foreground game without an earlier binding")
            backend.Active = other
            Check(Not WindowsInput.TargetIsForeground(game) AndAlso Not WindowsInput.TargetCanReceiveInput(game),
                  "selector rejects a selected foreground backend after focus loss")
            backend.Active = game
            backend.Available = False
            Check(Not WindowsInput.TargetIsForeground(game) AndAlso Not WindowsInput.TargetCanReceiveInput(game),
                  "selector rejects an unavailable foreground target")
            backend.Available = True
            Check(backend.ActivateCalls = 0 AndAlso backend.Events.Count = 0,
                  "eligibility checks never activate a window or emit input")

            For Each sample In {("1", 12, 140.0R), ("CTRL+1", 12, 140.0R), ("ALT+1", 12, 140.0R), ("1", 300, 280.0R)}
                Dim downAt As Long = -1, upAt As Long = -1
                backend.Events.Clear()
                backend.BeforeSend = Sub(value)
                                         If value.Keyboard AndAlso (value.Flags And 2UI) = 0 Then downAt = Diagnostics.Stopwatch.GetTimestamp()
                                         If value.Keyboard AndAlso (value.Flags And 2UI) <> 0 AndAlso upAt < 0 Then upAt = Diagnostics.Stopwatch.GetTimestamp()
                                     End Sub
                Check(BotEngine.SendKey(game, sample.Item1, sample.Item2), sample.Item1 & " timed foreground request succeeds")
                Check(downAt >= 0 AndAlso upAt >= downAt, sample.Item1 & " has observable foreground down/up timestamps")
                Dim dwellMs = (upAt - downAt) * 1000.0R / Diagnostics.Stopwatch.Frequency
                Check(dwellMs >= sample.Item3, $"{sample.Item1} requested {sample.Item2}ms preserves foreground dwell; observed {dwellMs:0.0}ms")
                Check(backend.Events.All(Function(value) value.Keyboard) AndAlso
                      backend.Events.GroupBy(Function(value) value.Key).All(Function(group) group.Count(Function(value) (value.Flags And 2UI) = 0) = group.Count(Function(value) (value.Flags And 2UI) <> 0)),
                      sample.Item1 & " releases every foreground digit and modifier")
            Next

            For Each key In {"1", "CTRL+1", "ALT+1"}
                backend.Events.Clear()
                Using cancellation As New Threading.CancellationTokenSource
                    backend.BeforeSend = Sub(value)
                                             If value.Keyboard AndAlso value.Key = 49 AndAlso (value.Flags And 2UI) = 0 Then cancellation.Cancel()
                                         End Sub
                    Check(Not BotEngine.SendKey(game, key, 5000, cancellationToken:=cancellation.Token), key & " reports cancellation during its foreground hold")
                    Check(backend.Events.Count > 0 AndAlso
                          backend.Events.GroupBy(Function(value) value.Key).All(Function(group) group.Count(Function(value) (value.Flags And 2UI) = 0) = group.Count(Function(value) (value.Flags And 2UI) <> 0)),
                          key & " cancellation releases every owned foreground digit and modifier")
                    Dim before = backend.Events.Count
                    input.ReleaseAll()
                    Check(backend.Events.Count = before, key & " cancellation leaves no pending held input")
                End Using
            Next
        Finally
            backend.BeforeSend = Nothing
            input.ReleaseAll()
            WindowsInput.Current = originalInput
        End Try
    End Sub

    Private Sub AuditNativeImports()
        Dim root = New DirectoryInfo(AppContext.BaseDirectory)
        While root IsNot Nothing AndAlso Not Directory.Exists(Path.Combine(root.FullName, "ui", "KathanaBotControlPanel"))
            root = root.Parent
        End While
        If root Is Nothing Then Throw New Exception("Cannot locate source for input API audit")
        Dim source = Path.Combine(root.FullName, "ui", "KathanaBotControlPanel")
        ' This suite audits the retained foreground backend. The production SDL
        ' backend uses target messages and has its own API audit.
        Dim text = File.ReadAllText(Path.Combine(source, "ForegroundWindowsInput.vb"))
        For Each name In {"PostMessage", "keybd_event", "mouse_event", "SetCursorPos", "SendMessage", "SendKeys"}
            Check(Not System.Text.RegularExpressions.Regex.IsMatch(text, "(?i)DllImport\([^\r\n]*EntryPoint\s*:=\s*""" & name & "(?:A|W)?"""), "legacy native import remains: " & name)
        Next
        Check(System.Text.RegularExpressions.Regex.Matches(text, "Private Shared Function SendInput\(").Count = 1, "one native SendInput entry point required")
    End Sub
End Module

Friend Class FakePlatform
    Inherits InputPlatform
    Public Active As IntPtr
    Public Pid As UInteger = 1
    Public Available As Boolean = True
    Public AllowActivate As Boolean = True
    Public Accept As Boolean = True
    Public Covered As Boolean
    Public FailReleaseKey As UShort
    Public ActivateCalls As Integer
    Public BeforeSend As Action(Of ForegroundInputEvent)
    Public Events As New List(Of ForegroundInputEvent)
    Public Overrides Function Foreground() As IntPtr
        Return Active
    End Function
    Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
        Return Pid
    End Function
    Public Overrides Function Valid(hwnd As IntPtr) As Boolean
        Return Available AndAlso hwnd <> IntPtr.Zero
    End Function
    Public Overrides Function Activate(hwnd As IntPtr) As Boolean
        ActivateCalls += 1
        If AllowActivate Then Active = hwnd
        Return AllowActivate
    End Function
    Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Point?
        Return New Point(x, y)
    End Function
    Public Overrides Function CursorOnTarget(hwnd As IntPtr) As Boolean
        Return Not Covered
    End Function
    Public Overrides Function Desktop() As Rectangle
        Return New Rectangle(-1920, -1080, 3840, 2160)
    End Function
    Public Overrides Function Send(value As ForegroundInputEvent) As Boolean
        BeforeSend?.Invoke(value)
        If FailReleaseKey <> 0 AndAlso value.Keyboard AndAlso value.Key = FailReleaseKey AndAlso (value.Flags And 2UI) <> 0 Then Return False
        If Accept Then Events.Add(value)
        Return Accept
    End Function
End Class
