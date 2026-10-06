Imports System.Drawing
Imports System.Threading

Public Interface IHookTransport
    Function Valid(hwnd As IntPtr) As Boolean
    Function Connect(hwnd As IntPtr) As Boolean
    Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
    Function Send(hwnd As IntPtr, kind As UInteger, key As UInteger, flags As UInteger, x As Integer, y As Integer, data As UInteger) As Boolean
    Sub Maintain()
    Sub ReleaseAll()
    Sub ReleaseTarget(hwnd As IntPtr)
End Interface

' All production bot output goes to the target process; no SendInput or PostMessage fallback.
Public NotInheritable Class InternalHookWindowsInput
    Implements IWindowsInput
    Private ReadOnly transport As IHookTransport
    Private ReadOnly target As New AsyncLocal(Of IntPtr)
    Private ReadOnly delay As Action(Of Integer)
    Private ReadOnly desktop As Func(Of Rectangle)
    Public Sub New(bridge As IHookTransport, Optional wait As Action(Of Integer) = Nothing, Optional bounds As Func(Of Rectangle) = Nothing)
        transport = bridge
        delay = If(wait, New Action(Of Integer)(AddressOf Thread.Sleep))
        desktop = If(bounds, Function() System.Windows.Forms.SystemInformation.VirtualScreen)
    End Sub
    Public Sub BindTarget(hwnd As IntPtr)
        target.Value = hwnd
    End Sub
    Public Function CanReceive(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero AndAlso transport.Valid(hwnd)
    End Function
    Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
        BindTarget(hwnd)
        Return transport.Connect(hwnd)
    End Function
    Private Function Emit(kind As UInteger, key As UInteger, flags As UInteger, x As Integer, y As Integer, data As UInteger) As Boolean
        Dim hwnd = target.Value
        Return CanReceive(hwnd) AndAlso transport.Connect(hwnd) AndAlso transport.Send(hwnd, kind, key, flags, x, y, data)
    End Function
    Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
        If (flags And 2UI) = 0 Then delay(ForegroundWindowsInput.NextActionDelayMs())
        If (flags And 4UI) <> 0 Then
            If (flags And 2UI) = 0 AndAlso Not Emit(4, scan, 0, 0, 0, 0) Then Throw New InvalidOperationException("Internal hook text command failed.")
        ElseIf Not Emit(1, key, flags, 0, 0, 0) Then
            Throw New InvalidOperationException("Internal hook keyboard command failed.")
        End If
    End Sub
    Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
        If (flags And (2UI Or 8UI Or 32UI Or 128UI)) <> 0 Then delay(ForegroundWindowsInput.NextActionDelayMs())
        If (flags And &H8000UI) <> 0 Then
            Dim rect = desktop()
            Dim px = rect.Left + CInt(CDbl(x) * (rect.Width - 1) / 65535)
            Dim py = rect.Top + CInt(CDbl(y) * (rect.Height - 1) / 65535)
            If Not MoveCursor(px, py) Then Throw New InvalidOperationException("Internal hook pointer command failed.")
            flags = flags And Not (&H8000UI Or &H4000UI Or 1UI)
            If flags = 0 Then Return
        End If
        Dim dx = BitConverter.ToInt32(BitConverter.GetBytes(x), 0)
        Dim dy = BitConverter.ToInt32(BitConverter.GetBytes(y), 0)
        If Not Emit(2, 0, flags, dx, dy, data) Then Throw New InvalidOperationException("Internal hook mouse command failed.")
    End Sub
    Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
        Return Emit(3, 0, 0, x, y, 0)
    End Function
    Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
        BindTarget(hwnd)
        Try
            Select Case message
                Case &H100UI, &H101UI, &H104UI, &H105UI
                    Dim up = message = &H101UI OrElse message = &H105UI
                    If Not up Then delay(ForegroundWindowsInput.NextActionDelayMs())
                    Dim flags = If(up, 2UI, 0UI) Or If((lParam.ToInt64() And &H1000000L) <> 0, 1UI, 0UI)
                    If message = &H104UI OrElse message = &H105UI Then flags = flags Or &H100UI
                    Return Emit(1, CUInt(wParam.ToInt64() And &HFFL), flags, 0, 0, 0)
                Case &H102UI
                    delay(ForegroundWindowsInput.NextActionDelayMs())
                    Return Emit(4, CUInt(wParam.ToInt64() And &HFFFFL), 0, 0, 0, 0)
                Case &H200UI, &H201UI, &H202UI, &H204UI, &H205UI
                    Dim up = message = &H202UI OrElse message = &H205UI
                    If Not up Then
                        Dim bits = lParam.ToInt64()
                        Dim x = CInt(bits And &HFFFFL), y = CInt((bits >> 16) And &HFFFFL)
                        If x >= 32768 Then x -= 65536
                        If y >= 32768 Then y -= 65536
                        Dim point = transport.ClientPoint(hwnd, x, y)
                        If Not point.HasValue OrElse Not MoveCursor(point.Value.X, point.Value.Y) Then Return False
                    End If
                    Dim flags As UInteger = 0
                    Select Case message
                        Case &H201UI : flags = 2
                        Case &H202UI : flags = 4
                        Case &H204UI : flags = 8
                        Case &H205UI : flags = 16
                    End Select
                    If flags = 0 Then Return True
                    Mouse(flags, 0, 0, 0, UIntPtr.Zero)
                    Return True
                Case Else : Return False
            End Select
        Catch ex As Exception
            RuntimeJournal.Record("Internal input hook", ex.Message)
            Return False
        End Try
    End Function
    Public Sub Maintain()
        transport.Maintain()
    End Sub
    Public Sub ReleaseAll()
        transport.ReleaseAll()
    End Sub
    Public Sub ReleaseTarget(hwnd As IntPtr)
        transport.ReleaseTarget(hwnd)
    End Sub
End Class
