Imports System.Threading

Public Interface IWindowsInput
    Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    Function Activate(hwnd As IntPtr) As Boolean
    Function MoveCursor(x As Integer, y As Integer) As Boolean
    Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr)
    Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr)
End Interface

Public NotInheritable Class WindowsInput
    Private Shared ReadOnly LocalInput As New AsyncLocal(Of IWindowsInput)
    Private Shared ReadOnly DefaultInput As New Win32Input
    Public Shared ReadOnly SequenceLock As New Object
    Public Shared Property Current As IWindowsInput
        Get
            Return If(LocalInput.Value, DefaultInput)
        End Get
        Set(value As IWindowsInput)
            LocalInput.Value = value
        End Set
    End Property
    Private Class Win32Input
        Implements IWindowsInput
        Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
            Dim ok = NativeMethods.RawPostMessage(hwnd, message, wParam, lParam)
            If Not ok Then RuntimeJournal.Record("Input failure", $"Window {hwnd}, message {message}: Win32 {Runtime.InteropServices.Marshal.GetLastWin32Error()}. Check window and privilege level.")
            Return ok
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Return NativeMethods.RawSetForegroundWindow(hwnd)
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Return NativeMethods.RawSetCursorPos(x, y)
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            NativeMethods.Rawkeybd_event(key, scan, flags, extra)
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            NativeMethods.Rawmouse_event(flags, x, y, data, extra)
        End Sub
    End Class
End Class
