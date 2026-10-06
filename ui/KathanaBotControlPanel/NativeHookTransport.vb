Imports System.ComponentModel
Imports System.IO
Imports System.IO.MemoryMappedFiles
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

Public NotInheritable Class NativeHookTransport
    Implements IHookTransport
    Private Const Magic As UInteger = &H4B484931UI
    Private Const Capacity As Integer = 512
    Private Const MapSize As Integer = 64 + Capacity * 32
    Private Const RemoteLoaderAccess As UInteger = &H43AUI
    Private ReadOnly gate As New Object
    Private ReadOnly sessions As New Dictionary(Of UInteger, Session)
    Private ReadOnly errors As New Dictionary(Of UInteger, Long)
    Private connectionError As String = ""
    Public ReadOnly Property LastError As String
        Get
            SyncLock gate
                Return connectionError
            End SyncLock
        End Get
    End Property
    Public Shared ReadOnly GamePath As String = Path.GetFullPath("C:\Program Files (x86)\Steam\steamapps\common\Kathana\KathanaGame.exe")
    Private Class Session
        Public Pid As UInteger
        Public Hwnd As IntPtr
        Public Mapping As MemoryMappedFile
        Public View As MemoryMappedViewAccessor
        Public Active As Boolean
        Public Owned As Boolean
    End Class
    <DllImport("user32.dll")> Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function OpenProcess(access As UInteger, inherit As Boolean, pid As UInteger) As IntPtr
    End Function
    <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)> Private Shared Function QueryFullProcessImageName(process As IntPtr, flags As UInteger, path As StringBuilder, ByRef size As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll")> Private Shared Function CloseHandle(handle As IntPtr) As Boolean
    End Function
    <StructLayout(LayoutKind.Sequential)> Private Structure ObjectBasicInfo
        Public Attributes, GrantedAccess, HandleCount, PointerCount As UInteger
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=10)> Public Reserved As UInteger()
    End Structure
    <DllImport("ntdll.dll")> Private Shared Function NtQueryObject(handle As IntPtr, infoClass As Integer, ByRef info As ObjectBasicInfo, size As UInteger, ByRef returned As UInteger) As Integer
    End Function
    Private Shared Sub RequireRemoteLoaderAccess(process As IntPtr)
        Dim info As New ObjectBasicInfo With {.Reserved = New UInteger(9) {}}
        Dim returned As UInteger
        Dim status = NtQueryObject(process, 0, info, CUInt(Marshal.SizeOf(Of ObjectBasicInfo)()), returned)
        If status < 0 Then Throw New InvalidOperationException($"Cannot inspect DLL-loader process rights (NTSTATUS 0x{CUInt(CLng(status) And &HFFFFFFFFL):X8}).")
        If (info.GrantedAccess And RemoteLoaderAccess) <> RemoteLoaderAccess Then
            Throw New InvalidOperationException($"Game process access is insufficient for the remote DLL loader: requested 0x{RemoteLoaderAccess:X}, granted 0x{info.GrantedAccess:X}. The handle lacks the memory and thread permissions required by this loader.")
        End If
    End Sub
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function VirtualAllocEx(process As IntPtr, address As IntPtr, size As UIntPtr, allocation As UInteger, protect As UInteger) As IntPtr
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function VirtualFreeEx(process As IntPtr, address As IntPtr, size As UIntPtr, freeType As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function WriteProcessMemory(process As IntPtr, address As IntPtr, bytes As Byte(), size As UIntPtr, ByRef written As UIntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function CreateRemoteThread(process As IntPtr, attributes As IntPtr, stack As UIntPtr, start As IntPtr, parameter As IntPtr, flags As UInteger, threadId As IntPtr) As IntPtr
    End Function
    <DllImport("kernel32.dll")> Private Shared Function WaitForSingleObject(handle As IntPtr, timeout As UInteger) As UInteger
    End Function
    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode)> Private Shared Function GetModuleHandle(name As String) As IntPtr
    End Function
    <DllImport("kernel32.dll", CharSet:=CharSet.Ansi, ExactSpelling:=True)> Private Shared Function GetProcAddress(moduleHandle As IntPtr, name As String) As IntPtr
    End Function
    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, EntryPoint:="GetModuleHandleExW")> Private Shared Function ModuleFromAddress(flags As UInteger, address As IntPtr, ByRef moduleHandle As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode)> Private Shared Function GetModuleFileName(moduleHandle As IntPtr, path As StringBuilder, size As Integer) As UInteger
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function CreateToolhelp32Snapshot(flags As UInteger, pid As UInteger) As IntPtr
    End Function
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)> Private Structure ModuleEntry
        Public Size, ModuleId, Pid, GlobalUse, ProcessUse As UInteger
        Public Base As IntPtr
        Public BaseSize As UInteger
        Public Handle As IntPtr
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=256)> Public Name As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)> Public File As String
    End Structure
    <DllImport("kernel32.dll", EntryPoint:="Module32FirstW", SetLastError:=True)> Private Shared Function ModuleFirst(snapshot As IntPtr, ByRef entry As ModuleEntry) As Boolean
    End Function
    <DllImport("kernel32.dll", EntryPoint:="Module32NextW", SetLastError:=True)> Private Shared Function ModuleNext(snapshot As IntPtr, ByRef entry As ModuleEntry) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function IsWow64Process2(process As IntPtr, ByRef machine As UShort, ByRef nativeMachine As UShort) As Boolean
    End Function
    Private Shared Function Pid(hwnd As IntPtr) As UInteger
        Dim value As UInteger
        NativeMethods.GetWindowThreadProcessId(hwnd, value)
        Return value
    End Function
    Public Function Valid(hwnd As IntPtr) As Boolean Implements IHookTransport.Valid
        Return hwnd <> IntPtr.Zero AndAlso IsWindow(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd) AndAlso Pid(hwnd) <> Environment.ProcessId
    End Function
    Public Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Drawing.Point? Implements IHookTransport.ClientPoint
        Return New NativeInputPlatform().ClientPoint(hwnd, x, y)
    End Function
    Private Shared Function ExtractDll() As String
        Using resource = GetType(NativeHookTransport).Assembly.GetManifestResourceStream("KathanaBotControlPanel.KathanaInputHook.dll")
            If resource Is Nothing Then Throw New InvalidOperationException("The standalone executable is missing its internal input hook DLL. Rebuild with native/InputHook/build.ps1.")
            Using memory As New MemoryStream()
                resource.CopyTo(memory)
                Dim bytes = memory.ToArray()
                Dim hash = Convert.ToHexString(SHA256.HashData(bytes))
                Dim directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KathanaBot", "InputHook", hash)
                System.IO.Directory.CreateDirectory(directory)
                Dim destination = Path.Combine(directory, "KathanaInputHook.dll")
                If Not File.Exists(destination) OrElse Not SHA256.HashData(File.ReadAllBytes(destination)).SequenceEqual(SHA256.HashData(bytes)) Then
                    Dim temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") & ".tmp")
                    File.WriteAllBytes(temporary, bytes)
                    File.Move(temporary, destination, True)
                End If
                Return destination
            End Using
        End Using
    End Function
    Private Shared Function RemoteModule(pid As UInteger, moduleName As String) As IntPtr
        Dim snapshot = CreateToolhelp32Snapshot(&H18UI, pid)
        If snapshot = New IntPtr(-1) Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect target modules.")
        Try
            Dim entry As New ModuleEntry With {.Size = CUInt(Marshal.SizeOf(Of ModuleEntry)())}
            If ModuleFirst(snapshot, entry) Then
                Do
                    If String.Equals(entry.Name, moduleName, StringComparison.OrdinalIgnoreCase) Then Return entry.Base
                Loop While ModuleNext(snapshot, entry)
            End If
            Return IntPtr.Zero
        Finally
            CloseHandle(snapshot)
        End Try
    End Function
    Private Shared Sub Inject(pid As UInteger, dll As String)
        If Not Environment.Is64BitProcess Then Throw New InvalidOperationException("The input hook requires the win-x64 standalone executable.")
        Dim process = OpenProcess(RemoteLoaderAccess, False, pid)
        If process = IntPtr.Zero Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Cannot open KathanaGame for the internal input hook. If the game is elevated, run this bot as administrator too.")
        Dim remote = IntPtr.Zero, thread = IntPtr.Zero, completed = False
        Try
            Dim executable As New StringBuilder(32768), length As UInteger = CUInt(executable.Capacity)
            If Not QueryFullProcessImageName(process, 0, executable, length) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
            If Not String.Equals(Path.GetFullPath(executable.ToString()), GamePath, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("The internal hook only accepts the confirmed Steam KathanaGame.exe installation.")
            Dim machine As UShort, nativeMachine As UShort
            If Not IsWow64Process2(process, machine, nativeMachine) OrElse machine <> 0 OrElse nativeMachine <> &H8664US Then Throw New InvalidOperationException("The target must be a native x64 Kathana client.")
            RequireRemoteLoaderAccess(process)
            If RemoteModule(pid, "KathanaInputHook.dll") <> IntPtr.Zero Then Return
            Dim loader = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW")
            Dim localModule As IntPtr
            If loader = IntPtr.Zero OrElse Not ModuleFromAddress(6, loader, localModule) Then Throw New InvalidOperationException("Cannot resolve the DLL loader.")
            Dim modulePath As New StringBuilder(32768)
            GetModuleFileName(localModule, modulePath, modulePath.Capacity)
            Dim remoteBase = RemoteModule(pid, Path.GetFileName(modulePath.ToString()))
            If remoteBase = IntPtr.Zero Then Throw New InvalidOperationException("The target loader module was not found.")
            Dim remoteLoader = New IntPtr(remoteBase.ToInt64() + loader.ToInt64() - localModule.ToInt64())
            Dim bytes = Encoding.Unicode.GetBytes(dll & ChrW(0))
            remote = VirtualAllocEx(process, IntPtr.Zero, New UIntPtr(CUInt(bytes.Length)), &H3000UI, 4)
            If remote = IntPtr.Zero Then Throw New Win32Exception(Marshal.GetLastWin32Error())
            Dim written As UIntPtr
            If Not WriteProcessMemory(process, remote, bytes, New UIntPtr(CUInt(bytes.Length)), written) OrElse written.ToUInt64() <> CULng(bytes.Length) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
            thread = CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, remoteLoader, remote, 0, IntPtr.Zero)
            If thread = IntPtr.Zero Then Throw New Win32Exception(Marshal.GetLastWin32Error())
            completed = WaitForSingleObject(thread, 5000) = 0
            If Not completed Then Throw New TimeoutException("The game did not finish loading the input hook within five seconds.")
        Finally
            If thread <> IntPtr.Zero Then CloseHandle(thread)
            ' Do not free the DLL pathname while a timed-out remote loader may still read it.
            If remote <> IntPtr.Zero AndAlso (thread = IntPtr.Zero OrElse completed) Then VirtualFreeEx(process, remote, UIntPtr.Zero, &H8000UI)
            CloseHandle(process)
        End Try
    End Sub
    Public Function Connect(hwnd As IntPtr) As Boolean Implements IHookTransport.Connect
        SyncLock gate
            If Not Valid(hwnd) Then
                connectionError = "Selected game window is closed or minimized."
                Return False
            End If
            Dim id = Pid(hwnd), session As Session = Nothing
            If sessions.TryGetValue(id, session) Then
                If session.Hwnd <> hwnd Then session.View.Write(60, session.View.ReadUInt32(60) + 1UI)
                session.Hwnd = hwnd
                session.View.Write(8, hwnd.ToInt64())
                session.View.Write(32, Environment.TickCount64)
                session.Active = True
                Dim connected = session.View.ReadInt32(24) = 1 AndAlso session.View.ReadInt32(28) = 0
                connectionError = If(connected, "", $"Internal hook is unavailable (error {session.View.ReadInt32(28)}).")
                Return connected
            End If
            Dim retryAt As Long
            If errors.TryGetValue(id, retryAt) AndAlso Environment.TickCount64 < retryAt Then Return False
            session = New Session With {.Pid = id, .Hwnd = hwnd}
            Try
                session.Mapping = MemoryMappedFile.CreateOrOpen($"Local\KathanaInputHookV1_{id}", MapSize, MemoryMappedFileAccess.ReadWrite)
                session.View = session.Mapping.CreateViewAccessor(0, MapSize, MemoryMappedFileAccess.ReadWrite)
                Dim owner = session.View.ReadUInt32(56)
                If owner <> 0 AndAlso owner <> Environment.ProcessId Then
                    Try
                        If Not Process.GetProcessById(CInt(owner)).HasExited Then Throw New InvalidOperationException("Another bot owns this game's internal input hook.")
                    Catch ex As ArgumentException
                        ' The prior bot exited; its heartbeat has already expired or will be stopped below.
                    End Try
                End If
                session.Owned = True
                session.View.Write(32, 0L)
                Thread.Sleep(20)
                Dim ready = session.View.ReadInt32(24), generation = session.View.ReadUInt32(60) + 1UI
                session.View.WriteArray(0, New Byte(MapSize - 1) {}, 0, MapSize)
                session.View.Write(0, Magic) : session.View.Write(4, 1UI) : session.View.Write(8, hwnd.ToInt64())
                session.View.Write(24, ready) : session.View.Write(56, CUInt(Environment.ProcessId)) : session.View.Write(60, generation)
                session.View.Write(32, Environment.TickCount64)
                Inject(id, ExtractDll())
                Dim deadline = Environment.TickCount64 + 5000
                Do While session.View.ReadInt32(24) = 0 AndAlso Environment.TickCount64 < deadline
                    session.View.Write(32, Environment.TickCount64)
                    Thread.Sleep(10)
                Loop
                If session.View.ReadInt32(24) <> 1 Then Throw New InvalidOperationException($"Internal input hook failed its handshake (error {session.View.ReadInt32(28)}).")
                session.Active = True
                sessions.Add(id, session)
                connectionError = ""
                RuntimeJournal.Record("Internal input hook", $"Connected to KathanaGame PID {id}; keyboard, mouse and text use process-local hooks.")
                Return True
            Catch ex As Exception
                connectionError = ex.Message
                If TypeOf ex Is Win32Exception Then
                    Dim nativeError = DirectCast(ex, Win32Exception).NativeErrorCode
                    connectionError &= $" Windows error {nativeError}: {New Win32Exception(nativeError).Message}"
                End If
                If session.Owned AndAlso session.View IsNot Nothing Then session.View.Write(32, 0L)
                session.View?.Dispose() : session.Mapping?.Dispose()
                errors(id) = Environment.TickCount64 + 10000
                RuntimeJournal.Record("Internal input hook failed", connectionError)
                Return False
            End Try
        End SyncLock
    End Function
    Public Function Send(hwnd As IntPtr, kind As UInteger, key As UInteger, flags As UInteger, x As Integer, y As Integer, data As UInteger) As Boolean Implements IHookTransport.Send
        SyncLock gate
            Dim session As Session = Nothing
            If Not Valid(hwnd) OrElse Not sessions.TryGetValue(Pid(hwnd), session) Then Return False
            Dim view = session.View
            Dim head = view.ReadUInt32(16), tail = view.ReadUInt32(20)
            If head - tail >= Capacity OrElse view.ReadInt32(28) <> 0 Then Return False
            Dim offset = 64 + CInt(head Mod Capacity) * 32
            view.Write(offset, kind) : view.Write(offset + 4, key) : view.Write(offset + 8, flags)
            view.Write(offset + 12, x) : view.Write(offset + 16, y) : view.Write(offset + 20, data)
            view.Write(offset + 24, head + 1UI)
            Thread.MemoryBarrier()
            view.Write(32, Environment.TickCount64) : view.Write(16, head + 1UI)
            Dim deadline = Environment.TickCount64 + 1000
            Do While view.ReadUInt32(20) <= head AndAlso Environment.TickCount64 < deadline
                If Not Valid(hwnd) Then Exit Do
                Thread.Sleep(1)
            Loop
            If view.ReadUInt32(20) > head AndAlso view.ReadInt32(28) = 0 Then Return True
            view.Write(32, 0L) : session.Active = False
            connectionError = "The game did not acknowledge input; virtual keys are released and no OS input fallback is used."
            RuntimeJournal.Record("Internal input hook failed", connectionError)
            Return False
        End SyncLock
    End Function
    Public Sub Maintain() Implements IHookTransport.Maintain
        SyncLock gate
            For Each item In sessions.Values
                If item.Active AndAlso Valid(item.Hwnd) AndAlso Pid(item.Hwnd) = item.Pid Then
                    item.View.Write(32, Environment.TickCount64)
                Else
                    item.View.Write(32, 0L)
                    item.Active = False
                End If
            Next
        End SyncLock
    End Sub
    Public Sub ReleaseAll() Implements IHookTransport.ReleaseAll
        SyncLock gate
            For Each item In sessions.Values
                item.View.Write(32, 0L)
                item.Active = False
            Next
        End SyncLock
    End Sub
    Public Sub ReleaseTarget(hwnd As IntPtr) Implements IHookTransport.ReleaseTarget
        SyncLock gate
            Dim item As Session = Nothing
            If hwnd <> IntPtr.Zero AndAlso sessions.TryGetValue(Pid(hwnd), item) Then
                item.View.Write(32, 0L)
                item.Active = False
            End If
        End SyncLock
    End Sub
End Class
