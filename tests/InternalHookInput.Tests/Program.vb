Imports System.Drawing
Imports System.IO
Imports System.Reflection
Module Program
    Sub Check(value As Boolean, label As String)
        If Not value Then Throw New Exception(label)
    End Sub
    Sub Main(args As String())
        If args.Contains("--probe-game-rights") Then
            Dim games = Process.GetProcessesByName("KathanaGame")
            If games.Length <> 1 Then Throw New InvalidOperationException("Open exactly one Kathana client to inspect its process rights.")
            Try
                InspectLoaderRights(CUInt(games(0).Id), &H43AUI)
                Console.WriteLine("The requested remote-loader rights were granted.")
            Catch ex As InvalidOperationException
                Console.WriteLine(ex.Message)
                Environment.ExitCode = 2
            End Try
            Return
        End If
        If args.Contains("--probe-game") Then
            Dim games = Process.GetProcessesByName("KathanaGame")
            If games.Length <> 1 Then Throw New InvalidOperationException("Open exactly one Kathana client to probe its hook connection.")
            Dim production As New NativeHookTransport()
            Dim result = production.Connect(games(0).MainWindowHandle)
            production.ReleaseAll()
            Console.WriteLine("Live hook handshake: " & result.ToString())
            For Each item In RuntimeJournal.Snapshot()
                Console.WriteLine(item.Kind & ": " & item.Detail)
            Next
            If Not result Then Environment.ExitCode = 2
            Return
        End If
        Dim bridge As New FakeBridge()
        Dim input As New InternalHookWindowsInput(bridge, Sub(ms) Check(ms >= 5 AndAlso ms <= 15, "action jitter"), Function() New Rectangle(-1920, 0, 3840, 1080))
        Dim hwnd As New IntPtr(123)
        Check(Not input.MoveCursor(30, 40), "unbound commands rejected")
        Check(input.Activate(hwnd), "connection replaces foreground activation")
        Check(input.Post(hwnd, &H100UI, New IntPtr(49), IntPtr.Zero), "key down without system focus")
        Check(bridge.Commands.Last().Kind = 1 AndAlso bridge.Commands.Last().Key = 49, "key command")
        Check(input.Post(hwnd, &H101UI, New IntPtr(49), IntPtr.Zero) AndAlso bridge.Commands.Last().Flags = 2, "key release")
        Check(input.Post(hwnd, &H104UI, New IntPtr(39), New IntPtr(&H1000000)), "extended key")
        Check(bridge.Commands.Last().Flags = &H101UI, "extended/system-key flags preserved")
        Check(input.Post(hwnd, &H102UI, New IntPtr(&H263A), IntPtr.Zero), "Unicode text")
        Check(bridge.Commands.Last().Kind = 4 AndAlso bridge.Commands.Last().Key = &H263A, "UTF16 command")
        Check(input.Post(hwnd, &H201UI, IntPtr.Zero, New IntPtr(50 Or (60 << 16))), "background client click")
        Check(bridge.Commands.TakeLast(2).First().Kind = 3 AndAlso bridge.Commands.TakeLast(2).First().X = 150 AndAlso bridge.Commands.TakeLast(2).First().Y = 260, "client to screen mapping")
        Check(bridge.Commands.Last().Kind = 2 AndAlso bridge.Commands.Last().Flags = 2, "mouse down")
        Check(input.Post(hwnd, &H202UI, IntPtr.Zero, IntPtr.Zero) AndAlso bridge.Commands.Last().Flags = 4, "mouse release")
        Check(input.MoveCursor(-1000, 80) AndAlso bridge.Commands.Last().X = -1000, "negative cursor origin")
        Check(Not input.Post(hwnd, &H201UI, IntPtr.Zero, New IntPtr(&HFFFF)), "invalid client point")
        Check(Not input.Post(hwnd, &H999UI, IntPtr.Zero, IntPtr.Zero), "unsupported request rejected")
        bridge.Accept = False
        Check(Not input.Post(hwnd, &H100UI, New IntPtr(65), IntPtr.Zero), "bridge failure propagated without OS fallback")
        bridge.Accept = True : bridge.Available = False
        Check(Not input.Activate(hwnd) AndAlso Not input.Post(hwnd, &H100UI, New IntPtr(65), IntPtr.Zero), "invalid window rejects commands")
        input.ReleaseAll() : Check(bridge.Released, "shutdown release")
        input.Maintain() : Check(bridge.Maintained, "heartbeat maintenance")
        Dim blocked As Boolean = False
        Try
            WindowsInput.RequireConnection(IntPtr.Zero)
        Catch ex As InvalidOperationException
            blocked = ex.Message.Contains("Selected game is unavailable")
        End Try
        Check(blocked, "invalid target reports an input availability error")
        Dim root = New DirectoryInfo(AppContext.BaseDirectory)
        While root IsNot Nothing AndAlso Not Directory.Exists(Path.Combine(root.FullName, "ui"))
            root = root.Parent
        End While
        Dim source = File.ReadAllText(Path.Combine(root.FullName, "ui", "KathanaBotControlPanel", "WindowsInput.vb"))
        Check(source.Contains("DefaultInput As New SdlBackgroundWindowsInput") AndAlso Not source.Contains("DefaultInput As New InternalHookWindowsInput"), "background keyboard production default bypasses the blocked hook")
        Dim defaultBackground = TryCast(WindowsInput.Current, SdlBackgroundWindowsInput)
        Check(defaultBackground IsNot Nothing AndAlso defaultBackground.KeyboardOnly AndAlso defaultBackground.KeyboardMode = BackgroundKeyboardMode.PostedScanCode,
              "production background backend starts with posted scan-code keys only")
        Check(Not WindowsInput.UsesInternalHook AndAlso WindowsInput.UsesTargetedInput AndAlso WindowsInput.InputMode = "background-keys-posted-scan",
              "target-only posted scan-code keyboard backend is selected without a DLL handshake")
        Using resource = GetType(NativeHookTransport).Assembly.GetManifestResourceStream("KathanaBotControlPanel.KathanaInputHook.dll")
            Check(resource Is Nothing, "production executable does not require the inaccessible hook DLL")
        End Using
        ' Exercise the real native marshalling on this controlled test process.
        ' Query-only handles must be rejected before any remote memory/thread call.
        InspectLoaderRights(CUInt(Environment.ProcessId), &H43AUI)
        Dim insufficient As Boolean = False
        Try
            InspectLoaderRights(CUInt(Environment.ProcessId), &H1000UI)
        Catch ex As InvalidOperationException
            insufficient = ex.Message.Contains("granted 0x1000")
        End Try
        Check(insufficient, "query-only handles cannot enter the remote loader")
        Console.WriteLine("PASS: experimental hook routing, coordinates, releases, posted scan-code keyboard-only production default and real native loader-rights validation.")
    End Sub
    Private Sub InspectLoaderRights(pid As UInteger, access As UInteger)
        Dim flags = BindingFlags.NonPublic Or BindingFlags.Static
        Dim transport = GetType(NativeHookTransport)
        Dim handle = DirectCast(transport.GetMethod("OpenProcess", flags).Invoke(Nothing, New Object() {access, False, pid}), IntPtr)
        Check(handle <> IntPtr.Zero, "controlled process handle opens")
        Try
            Try
                transport.GetMethod("RequireRemoteLoaderAccess", flags).Invoke(Nothing, New Object() {handle})
            Catch ex As TargetInvocationException When TypeOf ex.InnerException Is InvalidOperationException
                Throw DirectCast(ex.InnerException, InvalidOperationException)
            End Try
        Finally
            transport.GetMethod("CloseHandle", flags).Invoke(Nothing, New Object() {handle})
        End Try
    End Sub
End Module
Class FakeBridge
    Implements IHookTransport
    Public Available As Boolean = True, Accept As Boolean = True, Released As Boolean, Maintained As Boolean
    Public Commands As New List(Of (Kind As UInteger, Key As UInteger, Flags As UInteger, X As Integer, Y As Integer, Data As UInteger))
    Public Function Valid(hwnd As IntPtr) As Boolean Implements IHookTransport.Valid
        Return Available AndAlso hwnd <> IntPtr.Zero
    End Function
    Public Function Connect(hwnd As IntPtr) As Boolean Implements IHookTransport.Connect
        Return Valid(hwnd)
    End Function
    Public Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Point? Implements IHookTransport.ClientPoint
        If x < 0 OrElse y < 0 OrElse x >= 640 OrElse y >= 480 Then Return Nothing
        Return New Point(x + 100, y + 200)
    End Function
    Public Function Send(hwnd As IntPtr, kind As UInteger, key As UInteger, flags As UInteger, x As Integer, y As Integer, data As UInteger) As Boolean Implements IHookTransport.Send
        If Not Accept Then Return False
        Commands.Add((kind, key, flags, x, y, data))
        Return True
    End Function
    Public Sub Maintain() Implements IHookTransport.Maintain
        Maintained = True
    End Sub
    Public Sub ReleaseAll() Implements IHookTransport.ReleaseAll
        Released = True
    End Sub
    Public Sub ReleaseTarget(hwnd As IntPtr) Implements IHookTransport.ReleaseTarget
        Released = True
    End Sub
End Class
