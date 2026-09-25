Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Drawing

Public Structure ForegroundInputEvent
    Public Keyboard As Boolean
    Public Key As UShort
    Public Scan As UShort
    Public Flags As UInteger
    Public X As Integer
    Public Y As Integer
    Public Data As UInteger
    Public Extra As UIntPtr
End Structure

Public MustInherit Class InputPlatform
    Public MustOverride Function Foreground() As IntPtr
    Public MustOverride Function ProcessId(hwnd As IntPtr) As UInteger
    Public MustOverride Function Valid(hwnd As IntPtr) As Boolean
    Public MustOverride Function Activate(hwnd As IntPtr) As Boolean
    Public MustOverride Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
    Public MustOverride Function CursorOnTarget(hwnd As IntPtr) As Boolean
    Public MustOverride Function Desktop() As Rectangle
    Public MustOverride Function Send(value As ForegroundInputEvent) As Boolean
End Class

Public NotInheritable Class ForegroundWindowsInput
    Implements IWindowsInput
    Private ReadOnly platform As InputPlatform
    Private ReadOnly actionDelay As Action(Of Integer)
    Private ReadOnly target As New AsyncLocal(Of Binding)
    Private ReadOnly gate As New Object
    Private ReadOnly held As New Dictionary(Of String, HeldInput)
    Private Class Binding
        Public Hwnd As IntPtr
        Public Pid As UInteger
    End Class
    Private Class HeldInput
        Public Owner As Binding
        Public Release As ForegroundInputEvent
        Public ReleaseRequested As Boolean
    End Class
    Public Sub New(backend As InputPlatform, Optional delay As Action(Of Integer) = Nothing)
        platform = backend
        actionDelay = If(delay, New Action(Of Integer)(AddressOf Thread.Sleep))
    End Sub
    Public Shared Function NextActionDelayMs() As Integer
        Return Random.Shared.Next(5, 16)
    End Function
    Public Sub BindTarget(hwnd As IntPtr)
        target.Value = New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)}
    End Sub
    Private Function Focused(owner As Binding) As Boolean
        Return owner IsNot Nothing AndAlso owner.Hwnd <> IntPtr.Zero AndAlso owner.Pid <> 0 AndAlso
            platform.Valid(owner.Hwnd) AndAlso platform.ProcessId(owner.Hwnd) = owner.Pid AndAlso platform.Foreground() = owner.Hwnd
    End Function
    Public Function IsTargetForeground(hwnd As IntPtr) As Boolean
        Return target.Value IsNot Nothing AndAlso target.Value.Hwnd = hwnd AndAlso Focused(target.Value)
    End Function
    Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
        BindTarget(hwnd)
        If hwnd = IntPtr.Zero Then Return False
        If platform.Foreground() <> hwnd Then platform.Activate(hwnd)
        Return Focused(target.Value)
    End Function
    Private Function Emit(value As ForegroundInputEvent) As Boolean
        If platform.Send(value) Then Return True
        RuntimeJournal.Record("Input failure", "SendInput was blocked or failed; no message fallback was used.")
        Return False
    End Function
    Private Sub SendKeyEvent(key As UShort, scan As UShort, flags As UInteger, extra As UIntPtr)
        ' Add pacing before down events, outside the release lock. Recheck focus afterward.
        If (flags And 2UI) = 0 Then actionDelay(NextActionDelayMs())
        SyncLock gate
            Dim id = $"key:{If((flags And 4UI) <> 0, "unicode", "vk")}:{If((flags And 4UI) <> 0, scan, key)}"
            Dim up = (flags And 2UI) <> 0
            If up Then
                Dim owned As HeldInput = Nothing
                If Not held.TryGetValue(id, owned) Then Return
                owned.ReleaseRequested = True
                If Not Emit(owned.Release) Then Throw New InvalidOperationException("SendInput key release failed.")
                held.Remove(id)
                Return
            End If
            If Not Focused(target.Value) Then Throw New InvalidOperationException("Selected game is not foreground; key skipped.")
            If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot key is already held.")
            Dim value As New ForegroundInputEvent With {.Keyboard = True, .Key = key, .Scan = scan, .Flags = flags, .Extra = extra}
            If Not Emit(value) Then Throw New InvalidOperationException("SendInput key down failed.")
            value.Flags = flags Or 2UI
            held(id) = New HeldInput With {.Owner = target.Value, .Release = value}
        End SyncLock
    End Sub
    Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
        SendKeyEvent(key, scan, flags, extra)
    End Sub
    Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
        If (flags And (2UI Or 8UI Or 32UI Or 128UI)) <> 0 AndAlso (flags And (4UI Or 16UI Or 64UI Or 256UI)) = 0 Then actionDelay(NextActionDelayMs())
        SyncLock gate
            Dim downFlags As UInteger() = {2UI, 8UI, 32UI, 128UI}
            Dim upFlags As UInteger() = {4UI, 16UI, 64UI, 256UI}
            For i = 0 To downFlags.Length - 1
                Dim id = $"mouse:{i}:{If(i = 3, data, 0UI)}"
                If (flags And upFlags(i)) <> 0 Then
                    Dim owned As HeldInput = Nothing
                    If held.TryGetValue(id, owned) Then
                        owned.ReleaseRequested = True
                        If Not Emit(owned.Release) Then Throw New InvalidOperationException("SendInput mouse release failed.")
                        held.Remove(id)
                    End If
                    Return
                End If
                If (flags And downFlags(i)) <> 0 Then
                    If Not Focused(target.Value) OrElse Not platform.CursorOnTarget(target.Value.Hwnd) Then Throw New InvalidOperationException("Game not foreground or cursor covered; click skipped.")
                    If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot mouse button is already held.")
                    Dim value As New ForegroundInputEvent With {.Flags = downFlags(i), .Data = data, .Extra = extra}
                    If Not Emit(value) Then Throw New InvalidOperationException("SendInput mouse down failed.")
                    value.Flags = upFlags(i)
                    held(id) = New HeldInput With {.Owner = target.Value, .Release = value}
                    Return
                End If
            Next
            If Not Focused(target.Value) Then Throw New InvalidOperationException("Game not foreground; mouse input skipped.")
            If Not Emit(New ForegroundInputEvent With {.Flags = flags, .X = CInt(x), .Y = CInt(y), .Data = data, .Extra = extra}) Then Throw New InvalidOperationException("SendInput mouse event failed.")
        End SyncLock
    End Sub
    Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
        SyncLock gate
            If Not Focused(target.Value) Then Return False
            Dim bounds = platform.Desktop()
            If bounds.Width <= 1 OrElse bounds.Height <= 1 OrElse Not bounds.Contains(x, y) Then Return False
            Dim nx = CInt(Math.Round((x - CDbl(bounds.Left)) * 65535 / (bounds.Width - 1)))
            Dim ny = CInt(Math.Round((y - CDbl(bounds.Top)) * 65535 / (bounds.Height - 1)))
            Return Emit(New ForegroundInputEvent With {.Flags = &HC001UI, .X = nx, .Y = ny})
        End SyncLock
    End Function
    Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
        BindTarget(hwnd)
        Try
            Select Case message
                Case &H100UI, &H101UI, &H104UI, &H105UI
                    Dim up = message = &H101UI OrElse message = &H105UI
                    Dim bits = lParam.ToInt64()
                    Dim flags As UInteger = If(up, 2UI, 0UI) Or If((bits And &H1000000L) <> 0, 1UI, 0UI)
                    SendKeyEvent(CUShort(wParam.ToInt64() And &HFFFFL), CUShort((bits >> 16) And &HFFL), flags, UIntPtr.Zero)
                Case &H102UI ' WM_CHAR compatibility -> UTF-16 SendInput pair
                    Dim code = CUShort(wParam.ToInt64() And &HFFFFL)
                    Try
                        SendKeyEvent(0, code, 4UI, UIntPtr.Zero)
                    Finally
                        SendKeyEvent(0, code, 6UI, UIntPtr.Zero)
                    End Try
                Case &H200UI, &H201UI, &H202UI, &H204UI, &H205UI
                    Dim up = message = &H202UI OrElse message = &H205UI
                    If Not up Then
                        Dim bits = lParam.ToInt64()
                        Dim x = CInt(bits And &HFFFFL), y = CInt((bits >> 16) And &HFFFFL)
                        If x >= 32768 Then x -= 65536
                        If y >= 32768 Then y -= 65536
                        Dim point = platform.ClientPoint(hwnd, x, y)
                        If Not point.HasValue OrElse Not MoveCursor(point.Value.X, point.Value.Y) Then Return False
                    End If
                    Select Case message
                        Case &H201UI : Mouse(2UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H202UI : Mouse(4UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H204UI : Mouse(8UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H205UI : Mouse(16UI, 0, 0, 0, UIntPtr.Zero)
                    End Select
                Case Else
                    Return False
            End Select
            Return Focused(target.Value)
        Catch ex As Exception
            RuntimeJournal.Record("Input skipped", ex.Message)
            Return False
        End Try
    End Function
    Public Sub ReleaseAll()
        SyncLock gate
            For Each pair In held.Values
                pair.ReleaseRequested = True
            Next
        End SyncLock
        ReleaseUnfocused()
    End Sub
    Public Sub ReleaseUnfocused()
        SyncLock gate
            For Each pair In held.ToArray()
                If pair.Value.ReleaseRequested OrElse Not Focused(pair.Value.Owner) Then
                    If Emit(pair.Value.Release) Then held.Remove(pair.Key)
                End If
            Next
        End SyncLock
    End Sub
End Class

Friend NotInheritable Class NativeInputPlatform
    Inherits InputPlatform
    <StructLayout(LayoutKind.Sequential)>
    Private Structure KeyboardPacket
        Public Key As UShort
        Public Scan As UShort
        Public Flags As UInteger
        Public Time As UInteger
        Public Extra As UIntPtr
    End Structure
    <StructLayout(LayoutKind.Sequential)>
    Private Structure MousePacket
        Public X As Integer
        Public Y As Integer
        Public Data As UInteger
        Public Flags As UInteger
        Public Time As UInteger
        Public Extra As UIntPtr
    End Structure
    <StructLayout(LayoutKind.Explicit)>
    Private Structure PacketUnion
        <FieldOffset(0)> Public Keyboard As KeyboardPacket
        <FieldOffset(0)> Public Mouse As MousePacket
    End Structure
    <StructLayout(LayoutKind.Sequential)>
    Private Structure InputPacket
        Public Kind As UInteger
        Public Value As PacketUnion
    End Structure
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SendInput(count As UInteger, packets As InputPacket(), size As Integer) As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Shared Function WindowFromPoint(point As NativeMethods.POINT) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
    End Function
    Public Overrides Function Foreground() As IntPtr
        Return NativeMethods.GetForegroundWindow()
    End Function
    Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
        Dim pid As UInteger
        NativeMethods.GetWindowThreadProcessId(hwnd, pid)
        Return pid
    End Function
    Public Overrides Function Valid(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero AndAlso IsWindow(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd) AndAlso ProcessId(hwnd) <> Environment.ProcessId
    End Function
    Public Overrides Function Activate(hwnd As IntPtr) As Boolean
        Return NativeMethods.RawSetForegroundWindow(hwnd)
    End Function
    Public Overrides Function Desktop() As Rectangle
        Return System.Windows.Forms.SystemInformation.VirtualScreen
    End Function
    Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
        Dim rect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rect) OrElse x < 0 OrElse y < 0 OrElse x >= rect.Right OrElse y >= rect.Bottom Then Return Nothing
        Dim point As New NativeMethods.POINT With {.X = x, .Y = y}
        If Not NativeMethods.ClientToScreen(hwnd, point) Then Return Nothing
        Return New System.Drawing.Point(point.X, point.Y)
    End Function
    Public Overrides Function CursorOnTarget(hwnd As IntPtr) As Boolean
        Dim point As NativeMethods.POINT
        If Not NativeMethods.GetCursorPos(point) Then Return False
        Return NativeMethods.GetAncestor(WindowFromPoint(point), 2UI) = hwnd
    End Function
    Public Overrides Function Send(value As ForegroundInputEvent) As Boolean
        Dim packet As New InputPacket With {.Kind = If(value.Keyboard, 1UI, 0UI)}
        If value.Keyboard Then
            packet.Value.Keyboard = New KeyboardPacket With {.Key = value.Key, .Scan = value.Scan, .Flags = value.Flags, .Extra = value.Extra}
        Else
            packet.Value.Mouse = New MousePacket With {.X = value.X, .Y = value.Y, .Flags = value.Flags, .Data = value.Data, .Extra = value.Extra}
        End If
        Return SendInput(1, {packet}, Marshal.SizeOf(Of InputPacket)()) = 1
    End Function
End Class
