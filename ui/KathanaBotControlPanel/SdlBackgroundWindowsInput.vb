Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

Public Enum BackgroundKeyboardMode
    PostedScanCode
    SynchronousScanCode
    PostedZeroScanCode
    SynchronousZeroScanCode
End Enum

Public MustInherit Class BackgroundInputPlatform
    Public MustOverride Function Valid(hwnd As IntPtr) As Boolean
    Public MustOverride Function ProcessId(hwnd As IntPtr) As UInteger
    Public MustOverride Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
    Public MustOverride Function ScreenToClient(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
    Public MustOverride Function Send(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    Public Overridable Function SendKeyboard(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr, mode As BackgroundKeyboardMode) As Boolean
        Return Send(hwnd, message, wParam, lParam)
    End Function
End Class

' Keyboard messages stay target-only; each held key retains its original delivery mode.
Public NotInheritable Class SdlBackgroundWindowsInput
    Implements IWindowsInput
    Private ReadOnly platform As BackgroundInputPlatform
    Private ReadOnly delay As Action(Of Integer)
    Private ReadOnly desktop As Func(Of Rectangle)
    Private ReadOnly target As New AsyncLocal(Of Binding)
    Private ReadOnly gate As New Object
    Private ReadOnly held As New Dictionary(Of String, HeldInput)
    Private ReadOnly cursors As New Dictionary(Of String, System.Drawing.Point)
    Private ReadOnly _keyboardOnly As Boolean
    Private _keyboardMode As BackgroundKeyboardMode

    Private Class Binding
        Public Hwnd As IntPtr
        Public Pid As UInteger
        Public ReadOnly Property Id As String
            Get
                Return $"{Hwnd.ToInt64()}:{Pid}"
            End Get
        End Property
    End Class
    Private Class HeldInput
        Public Owner As Binding
        Public Token As String
        Public Message As UInteger
        Public Key As UInteger
        Public Point As System.Drawing.Point
        Public Button As UInteger
        Public ReleaseRequested As Boolean
        Public KeyboardMode As BackgroundKeyboardMode
        Public KeyboardReleaseLParam As IntPtr
    End Class

    Public Sub New(backend As BackgroundInputPlatform, Optional wait As Action(Of Integer) = Nothing, Optional bounds As Func(Of Rectangle) = Nothing,
                   Optional keyboardMode As BackgroundKeyboardMode = BackgroundKeyboardMode.SynchronousZeroScanCode, Optional keyboardOnly As Boolean = False)
        ValidateKeyboardMode(keyboardMode)
        platform = backend
        delay = If(wait, New Action(Of Integer)(AddressOf Thread.Sleep))
        desktop = If(bounds, Function() System.Windows.Forms.SystemInformation.VirtualScreen)
        _keyboardMode = keyboardMode
        _keyboardOnly = keyboardOnly
    End Sub
    Private Shared Sub ValidateKeyboardMode(mode As BackgroundKeyboardMode)
        If Not [Enum].IsDefined(GetType(BackgroundKeyboardMode), mode) Then Throw New ArgumentOutOfRangeException(NameOf(mode))
    End Sub
    Public ReadOnly Property KeyboardMode As BackgroundKeyboardMode
        Get
            SyncLock gate
                Return _keyboardMode
            End SyncLock
        End Get
    End Property
    Public ReadOnly Property KeyboardOnly As Boolean
        Get
            Return _keyboardOnly
        End Get
    End Property
    Public Function SetKeyboardMode(mode As BackgroundKeyboardMode) As Boolean
        ValidateKeyboardMode(mode)
        SyncLock gate
            If mode = _keyboardMode Then Return True
            For Each pair In held.ToArray()
                Release(pair)
            Next
            ' A failed up remains owned by this instance and must be retried using
            ' its original method before any new mode is selected.
            If held.Count > 0 Then Return False
            _keyboardMode = mode
            Return True
        End SyncLock
    End Function
    Public Sub BindTarget(hwnd As IntPtr)
        Dim current = target.Value
        ' Preserve the captured PID when the caller merely repeats the same binding.
        If current IsNot Nothing AndAlso current.Hwnd = hwnd Then Return
        target.Value = New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)}
    End Sub
    Private Function Eligible(owner As Binding) As Boolean
        Return owner IsNot Nothing AndAlso owner.Hwnd <> IntPtr.Zero AndAlso owner.Pid <> 0 AndAlso
            platform.ProcessId(owner.Hwnd) = owner.Pid AndAlso platform.Valid(owner.Hwnd)
    End Function
    Public Function CanReceive(hwnd As IntPtr) As Boolean
        Dim owner = target.Value
        If owner IsNot Nothing AndAlso owner.Hwnd = hwnd Then Return Eligible(owner)
        Return Eligible(New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)})
    End Function
    Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
        BindTarget(hwnd)
        Return Eligible(target.Value)
    End Function
    Private Function Emit(owner As Binding, message As UInteger, wParam As IntPtr, lParam As IntPtr, Optional deliveryMode As BackgroundKeyboardMode? = Nothing) As Boolean
        If Not Eligible(owner) Then Return False
        If deliveryMode.HasValue Then Return platform.SendKeyboard(owner.Hwnd, message, wParam, lParam, deliveryMode.Value)
        Return platform.Send(owner.Hwnd, message, wParam, lParam)
    End Function
    Private Function FindOwned(token As String) As KeyValuePair(Of String, HeldInput)?
        Dim current = target.Value
        Dim matches = held.Where(Function(pair) pair.Value.Token = token).ToArray()
        If current IsNot Nothing Then
            For Each pair In matches
                If pair.Value.Owner.Id = current.Id Then Return pair
            Next
        End If
        ' An up after retargeting may only release an input that this backend owns.
        If matches.Length = 1 Then Return matches(0)
        Return Nothing
    End Function
    Private Function MouseState(owner As Binding, Optional excluding As HeldInput = Nothing) As UInteger
        Dim value As UInteger
        For Each entry In held.Values
            If entry Is excluding OrElse entry.Owner.Id <> owner.Id Then Continue For
            value = value Or entry.Button
            If {16UI, &HA0UI, &HA1UI}.Contains(entry.Key) AndAlso entry.Button = 0 Then value = value Or 4UI
            If {17UI, &HA2UI, &HA3UI}.Contains(entry.Key) AndAlso entry.Button = 0 Then value = value Or 8UI
        Next
        Return value
    End Function
    Private Shared Function PackedPoint(point As System.Drawing.Point) As IntPtr
        If point.X < Short.MinValue OrElse point.X > Short.MaxValue OrElse point.Y < Short.MinValue OrElse point.Y > Short.MaxValue Then
            Throw New InvalidOperationException("Game message coordinates exceed the signed 16-bit range.")
        End If
        Return New IntPtr((CLng(point.X) And &HFFFFL) Or ((CLng(point.Y) And &HFFFFL) << 16))
    End Function
    Private Function Release(pair As KeyValuePair(Of String, HeldInput)) As Boolean
        Dim value = pair.Value
        value.ReleaseRequested = True
        If platform.ProcessId(value.Owner.Hwnd) <> value.Owner.Pid OrElse value.Owner.Pid = 0 Then
            held.Remove(pair.Key)
            cursors.Remove(value.Owner.Id)
            Return True ' Never send a release to a reused or destroyed HWND.
        End If
        Dim wp = If(value.Button = 0, New IntPtr(CLng(value.Key)), New IntPtr(CLng(MouseState(value.Owner, value)) Or If(value.Message = &H20CUI, CLng(value.Key) << 16, 0L)))
        Dim lp = If(value.Button = 0, value.KeyboardReleaseLParam, PackedPoint(value.Point))
        Dim released = If(value.Button = 0, Emit(value.Owner, value.Message, wp, lp, value.KeyboardMode), Emit(value.Owner, value.Message, wp, lp))
        If Not released Then Return False
        held.Remove(pair.Key)
        Return True
    End Function
    Private Shared Function KeyboardDownLParam(key As UInteger, mode As BackgroundKeyboardMode, system As Boolean) As IntPtr
        Dim bits As Long = 1
        If mode = BackgroundKeyboardMode.PostedScanCode OrElse mode = BackgroundKeyboardMode.SynchronousScanCode Then
            Dim scan = NativeMethods.MapVirtualKey(key, 0UI) And &HFFUI
            bits = bits Or (CLng(scan) << 16)
            If {&HA3UI, &HA5UI, &H21UI, &H22UI, &H23UI, &H24UI, &H25UI, &H26UI, &H27UI, &H28UI,
                &H2DUI, &H2EUI, &H5BUI, &H5CUI, &H5DUI, &H6FUI, &H90UI}.Contains(key) Then bits = bits Or &H1000000L
            If system Then bits = bits Or &H20000000L
        End If
        Return New IntPtr(bits)
    End Function
    Private Sub KeyEvent(key As UInteger, up As Boolean, Optional system As Boolean = False)
        ' Preserve explicit left/right virtual keys in every method and on release.
        If key = 0 OrElse key > 255 Then Throw New InvalidOperationException("Unsupported game virtual key.")
        If Not up Then delay(ForegroundWindowsInput.NextActionDelayMs())
        SyncLock gate
            Dim token = $"key:{key}"
            If up Then
                Dim owned = FindOwned(token)
                If owned.HasValue AndAlso Not Release(owned.Value) Then Throw New InvalidOperationException("Background game key release failed; cleanup will retry.")
                Return
            End If
            Dim owner = target.Value
            If Not Eligible(owner) Then Throw New InvalidOperationException("Selected background game is unavailable; key skipped.")
            Dim id = owner.Id & ":" & token
            If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot game key is already held.")
            Dim mode = _keyboardMode
            Dim downLParam = KeyboardDownLParam(key, mode, system)
            Dim value As New HeldInput With {.Owner = owner, .Token = token, .Message = If(system, &H105UI, &H101UI), .Key = key,
                                            .KeyboardMode = mode, .KeyboardReleaseLParam = New IntPtr(downLParam.ToInt64() Or &HC0000000L)}
            held.Add(id, value)
            If Not Emit(owner, If(system, &H104UI, &H100UI), New IntPtr(CLng(key)), downLParam, mode) Then
                Release(New KeyValuePair(Of String, HeldInput)(id, value))
                Throw New InvalidOperationException("Background game key down timed out or failed; acceptance is unknown.")
            End If
        End SyncLock
    End Sub
    Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
        If (flags And 4UI) <> 0 Then
            If _keyboardOnly Then Throw New InvalidOperationException("Background keyboard-only mode does not send text.")
            If (flags And 2UI) = 0 AndAlso Not TextEvent(scan) Then Throw New InvalidOperationException("Background game text failed.")
        Else
            KeyEvent(key, (flags And 2UI) <> 0)
        End If
    End Sub
    Private Function TextEvent(code As UInteger) As Boolean
        If _keyboardOnly Then Return False
        delay(ForegroundWindowsInput.NextActionDelayMs())
        SyncLock gate
            Return Emit(target.Value, &H102UI, New IntPtr(CLng(code And &HFFFFUI)), New IntPtr(1))
        End SyncLock
    End Function
    Private Function Cursor(owner As Binding) As System.Drawing.Point?
        Dim point As System.Drawing.Point
        If cursors.TryGetValue(owner.Id, point) Then Return platform.ClientPoint(owner.Hwnd, point.X, point.Y)
        Return platform.ClientPoint(owner.Hwnd, 0, 0)
    End Function
    Private Function Move(owner As Binding, screen As System.Drawing.Point) As Boolean
        If Not Eligible(owner) Then Return False
        Dim client = platform.ScreenToClient(owner.Hwnd, screen.X, screen.Y)
        If Not client.HasValue Then Return False
        If Not Emit(owner, &H200UI, New IntPtr(CLng(MouseState(owner))), PackedPoint(client.Value)) Then Return False
        cursors(owner.Id) = client.Value
        For Each value In held.Values
            If value.Owner.Id = owner.Id AndAlso value.Button <> 0 Then value.Point = client.Value
        Next
        Return True
    End Function
    Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
        If _keyboardOnly Then Return False
        SyncLock gate
            Return Move(target.Value, New System.Drawing.Point(x, y))
        End SyncLock
    End Function
    Private Sub ButtonEvent(button As Integer, up As Boolean, data As UInteger)
        Dim xbutton = If(button = 3, data, 0UI)
        If button = 3 AndAlso xbutton <> 1 AndAlso xbutton <> 2 Then Throw New InvalidOperationException("Unsupported game X button.")
        Dim token = $"mouse:{button}:{xbutton}"
        If up Then
            Dim owned = FindOwned(token)
            If owned.HasValue AndAlso Not Release(owned.Value) Then Throw New InvalidOperationException("Background game mouse release failed; cleanup will retry.")
            Return
        End If
        Dim owner = target.Value
        If Not Eligible(owner) Then Throw New InvalidOperationException("Selected background game is unavailable; click skipped.")
        Dim screen = Cursor(owner)
        If Not screen.HasValue Then Throw New InvalidOperationException("Game cursor position is unavailable.")
        Dim client = platform.ScreenToClient(owner.Hwnd, screen.Value.X, screen.Value.Y)
        If Not client.HasValue Then Throw New InvalidOperationException("Virtual cursor is outside the game client.")
        Dim masks = {1UI, 2UI, 16UI, If(xbutton = 1, 32UI, 64UI)}
        Dim downs = {&H201UI, &H204UI, &H207UI, &H20BUI}
        Dim ups = {&H202UI, &H205UI, &H208UI, &H20CUI}
        Dim id = owner.Id & ":" & token
        If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot game mouse button is already held.")
        Dim value As New HeldInput With {.Owner = owner, .Token = token, .Message = ups(button), .Point = client.Value, .Button = masks(button), .Key = xbutton}
        held.Add(id, value)
        Dim wp = New IntPtr(CLng(MouseState(owner)) Or (CLng(xbutton) << 16))
        If Not Emit(owner, downs(button), wp, PackedPoint(client.Value)) Then
            Release(New KeyValuePair(Of String, HeldInput)(id, value))
            Throw New InvalidOperationException("Background game mouse down timed out or failed; acceptance is unknown.")
        End If
    End Sub
    Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
        If _keyboardOnly Then Throw New InvalidOperationException("Background keyboard-only mode does not send mouse input.")
        If (flags And Not &HD9FFUI) <> 0 Then Throw New InvalidOperationException("Unsupported background game mouse flags.")
        If (flags And (2UI Or 8UI Or 32UI Or 128UI)) <> 0 Then delay(ForegroundWindowsInput.NextActionDelayMs())
        SyncLock gate
            If (flags And 1UI) <> 0 Then
                Dim owner = target.Value
                If Not Eligible(owner) Then Throw New InvalidOperationException("Selected background game is unavailable; movement skipped.")
                Dim point As System.Drawing.Point
                If (flags And &H8000UI) <> 0 Then
                    If x > 65535 OrElse y > 65535 Then Throw New InvalidOperationException("Invalid absolute game cursor coordinates.")
                    Dim bounds = desktop()
                    If bounds.Width <= 1 OrElse bounds.Height <= 1 Then Throw New InvalidOperationException("Desktop bounds are unavailable.")
                    point = New System.Drawing.Point(bounds.Left + CInt(Math.Round(CDbl(x) * (bounds.Width - 1) / 65535)), bounds.Top + CInt(Math.Round(CDbl(y) * (bounds.Height - 1) / 65535)))
                Else
                    Dim current = Cursor(owner)
                    If Not current.HasValue Then Throw New InvalidOperationException("Game cursor position is unavailable.")
                    Dim dx = BitConverter.ToInt32(BitConverter.GetBytes(x), 0), dy = BitConverter.ToInt32(BitConverter.GetBytes(y), 0)
                    point = New System.Drawing.Point(CInt(CLng(current.Value.X) + dx), CInt(CLng(current.Value.Y) + dy))
                End If
                If Not Move(owner, point) Then Throw New InvalidOperationException("Background game cursor movement failed.")
            End If
            Dim downs = {2UI, 8UI, 32UI, 128UI}, ups = {4UI, 16UI, 64UI, 256UI}
            For i = 0 To downs.Length - 1
                If (flags And ups(i)) <> 0 Then ButtonEvent(i, True, data)
                If (flags And downs(i)) <> 0 Then ButtonEvent(i, False, data)
            Next
            For Each wheel In {(&H800UI, &H20AUI), (&H1000UI, &H20EUI)}
                If (flags And wheel.Item1) = 0 Then Continue For
                Dim owner = target.Value
                If Not Eligible(owner) Then Throw New InvalidOperationException("Selected background game is unavailable; wheel skipped.")
                Dim screen = Cursor(owner)
                If Not screen.HasValue OrElse Not platform.ScreenToClient(owner.Hwnd, screen.Value.X, screen.Value.Y).HasValue Then Throw New InvalidOperationException("Game wheel cursor is outside the client.")
                Dim wp = New IntPtr(CLng(MouseState(owner)) Or (CLng(data And &HFFFFUI) << 16))
                If Not Emit(owner, wheel.Item2, wp, PackedPoint(screen.Value)) Then Throw New InvalidOperationException("Background game wheel failed.")
            Next
        End SyncLock
    End Sub
    Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
        If _keyboardOnly AndAlso Not {&H100UI, &H101UI, &H104UI, &H105UI}.Contains(message) Then Return False
        BindTarget(hwnd)
        Try
            Select Case message
                Case &H100UI, &H101UI, &H104UI, &H105UI
                    KeyEvent(CUInt(wParam.ToInt64() And &HFFFFL), message = &H101UI OrElse message = &H105UI, message = &H104UI OrElse message = &H105UI)
                Case &H102UI
                    Return TextEvent(CUInt(wParam.ToInt64() And &HFFFFL))
                Case &H200UI, &H201UI, &H202UI, &H204UI, &H205UI, &H207UI, &H208UI, &H20BUI, &H20CUI
                    Dim up = {&H202UI, &H205UI, &H208UI, &H20CUI}.Contains(message)
                    If Not up Then
                        Dim bits = lParam.ToInt64()
                        Dim x = CInt(bits And &HFFFFL), y = CInt((bits >> 16) And &HFFFFL)
                        If x >= 32768 Then x -= 65536
                        If y >= 32768 Then y -= 65536
                        Dim point = platform.ClientPoint(hwnd, x, y)
                        If Not point.HasValue OrElse Not MoveCursor(point.Value.X, point.Value.Y) Then Return False
                    End If
                    If message <> &H200UI Then
                        Dim index = If(message = &H201UI OrElse message = &H202UI, 0, If(message = &H204UI OrElse message = &H205UI, 1, If(message = &H207UI OrElse message = &H208UI, 2, 3)))
                        Mouse(If(up, {4UI, 16UI, 64UI, 256UI}(index), {2UI, 8UI, 32UI, 128UI}(index)), 0, 0, CUInt((wParam.ToInt64() >> 16) And &HFFFFL), UIntPtr.Zero)
                    End If
                Case Else : Return False
            End Select
            Return True
        Catch ex As Exception
            RuntimeJournal.Record("Background input skipped", ex.Message)
            Return False
        End Try
    End Function
    Public Sub Maintain()
        ' A hung game can consume the timeout. Timer callbacks must not accumulate waiting threads.
        If Not Monitor.TryEnter(gate) Then Return
        Try
            For Each pair In held.ToArray()
                If pair.Value.ReleaseRequested OrElse Not Eligible(pair.Value.Owner) Then Release(pair)
            Next
        Finally
            Monitor.Exit(gate)
        End Try
    End Sub
    Public Sub ReleaseAll()
        SyncLock gate
            For Each pair In held.ToArray()
                Release(pair)
            Next
        End SyncLock
    End Sub
    Public Sub ReleaseTarget(hwnd As IntPtr)
        SyncLock gate
            For Each pair In held.ToArray()
                If pair.Value.Owner.Hwnd = hwnd Then Release(pair)
            Next
        End SyncLock
    End Sub
End Class

Public NotInheritable Class NativeBackgroundInputPlatform
    Inherits BackgroundInputPlatform
    Private ReadOnly fixedKeyboardMode As BackgroundKeyboardMode
    Public Sub New(Optional keyboardMode As BackgroundKeyboardMode = BackgroundKeyboardMode.SynchronousZeroScanCode)
        If Not [Enum].IsDefined(GetType(BackgroundKeyboardMode), keyboardMode) Then Throw New ArgumentOutOfRangeException(NameOf(keyboardMode))
        fixedKeyboardMode = keyboardMode
    End Sub
    <DllImport("user32.dll")> Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)> Private Shared Function OpenProcess(access As UInteger, inherit As Boolean, pid As UInteger) As IntPtr
    End Function
    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)> Private Shared Function QueryFullProcessImageName(process As IntPtr, flags As UInteger, path As StringBuilder, ByRef size As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll")> Private Shared Function CloseHandle(handle As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", EntryPoint:="SendMessageTimeoutW", SetLastError:=True)> Private Shared Function SendTargetMessage(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr, flags As UInteger, timeout As UInteger, ByRef result As UIntPtr) As IntPtr
    End Function
    <DllImport("user32.dll", EntryPoint:="PostMessageW", SetLastError:=True)> Private Shared Function PostTargetMessage(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function
    Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
        Dim pid As UInteger
        NativeMethods.GetWindowThreadProcessId(hwnd, pid)
        Return pid
    End Function
    Public Overrides Function Valid(hwnd As IntPtr) As Boolean
        If hwnd = IntPtr.Zero OrElse Not IsWindow(hwnd) OrElse NativeMethods.IsIconic(hwnd) Then Return False
        Dim pid = ProcessId(hwnd)
        If pid = 0 OrElse pid = Environment.ProcessId Then Return False
        Dim process = OpenProcess(&H1000UI, False, pid)
        If process = IntPtr.Zero Then Return False
        Try
            Dim imagePath As New StringBuilder(32768), size As UInteger = 32768
            Return QueryFullProcessImageName(process, 0, imagePath, size) AndAlso String.Equals(Path.GetFullPath(imagePath.ToString()), NativeHookTransport.GamePath, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        Finally
            CloseHandle(process)
        End Try
    End Function
    Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
        Dim rect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rect) OrElse x < 0 OrElse y < 0 OrElse x >= rect.Right OrElse y >= rect.Bottom Then Return Nothing
        Dim point As New NativeMethods.POINT With {.X = x, .Y = y}
        If Not NativeMethods.ClientToScreen(hwnd, point) Then Return Nothing
        Return New System.Drawing.Point(point.X, point.Y)
    End Function
    Public Overrides Function ScreenToClient(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
        Dim point As New NativeMethods.POINT With {.X = x, .Y = y}
        If Not NativeMethods.ScreenToClient(hwnd, point) OrElse Not ClientPoint(hwnd, point.X, point.Y).HasValue Then Return Nothing
        Return New System.Drawing.Point(point.X, point.Y)
    End Function
    Public Overrides Function Send(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
        Return SendKeyboard(hwnd, message, wParam, lParam, fixedKeyboardMode)
    End Function
    Public Overrides Function SendKeyboard(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr, mode As BackgroundKeyboardMode) As Boolean
        If Not [Enum].IsDefined(GetType(BackgroundKeyboardMode), mode) Then Return False
        If Not Valid(hwnd) Then Return False
        If mode = BackgroundKeyboardMode.PostedScanCode OrElse mode = BackgroundKeyboardMode.PostedZeroScanCode Then
            Return PostTargetMessage(hwnd, message, wParam, lParam)
        End If
        Dim result As UIntPtr
        ' BLOCK avoids reentrant nonqueued messages; ABORTIFHUNG and ERRORONEXIT fail closed.
        Return SendTargetMessage(hwnd, message, wParam, lParam, &H23UI, 1000, result) <> IntPtr.Zero
    End Function
End Class
