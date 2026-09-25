Imports System.Threading

Public Interface IWindowsInput
    ' Legacy message-shaped requests are translated to foreground SendInput, never posted.
    Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    Function Activate(hwnd As IntPtr) As Boolean
    Function MoveCursor(x As Integer, y As Integer) As Boolean
    Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr)
    Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr)
End Interface

Public NotInheritable Class WindowsInput
    Private Shared ReadOnly LocalInput As New AsyncLocal(Of IWindowsInput)
    Private Shared ReadOnly DefaultInput As New ForegroundWindowsInput(New NativeInputPlatform())
    Private Shared ReadOnly ReleaseTimer As New System.Threading.Timer(AddressOf MonitorReleases, Nothing, 20, 20)
    Public Shared ReadOnly SequenceLock As New Object
    ' Retained for old profile deserialization; background injection cannot be enabled.
    Public Shared Property BackgroundOnly As Boolean
        Get
            Return False
        End Get
        Set(value As Boolean)
        End Set
    End Property
    Private Shared Sub MonitorReleases(state As Object)
        Try
            DefaultInput.ReleaseUnfocused()
        Catch ex As Exception
            RuntimeJournal.Record("Input release error", ex.Message)
        End Try
    End Sub
    Public Shared Sub ReleaseAll()
        DefaultInput.ReleaseAll()
    End Sub
    Public Shared Sub BindTarget(hwnd As IntPtr)
        DefaultInput.BindTarget(hwnd)
    End Sub
    Public Shared Function TargetIsForeground(hwnd As IntPtr) As Boolean
        Return If(LocalInput.Value Is Nothing, DefaultInput.IsTargetForeground(hwnd), True)
    End Function
    Public Shared Property Current As IWindowsInput
        Get
            Return If(LocalInput.Value, DefaultInput)
        End Get
        Set(value As IWindowsInput)
            LocalInput.Value = value
        End Set
    End Property
End Class
