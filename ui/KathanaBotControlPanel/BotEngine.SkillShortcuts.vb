Imports System.Threading

Partial Public Class BotEngine
    Private Shared Function TryParseSkillShortcut(value As String, ByRef modifier As Integer, ByRef digit As Integer) As Boolean
        Dim parts = If(value, "").ToUpperInvariant().Replace(" ", "").Split("+"c)
        If parts.Length <> 2 OrElse parts(1).Length <> 1 OrElse parts(1)(0) < "0"c OrElse parts(1)(0) > "9"c Then Return False
        Select Case parts(0)
            Case "CTRL", "CONTROL" : modifier = &H11
            Case "ALT" : modifier = &H12
            Case Else : Return False
        End Select
        digit = AscW(parts(1)(0))
        Return True
    End Function

    Private Shared Function SendSkillShortcut(hwnd As IntPtr, modifier As Integer, digit As Integer, holdMs As Integer, token As CancellationToken) As Boolean
        If hwnd = IntPtr.Zero OrElse WindowsInput.BackgroundOnly OrElse token.IsCancellationRequested Then Return False
        SyncLock _keyOutputPauseSync
            If _keyOutputPausedWindows.Contains(hwnd) Then Return False
        End SyncLock
        WindowsInput.BindTarget(hwnd)
        ' Background mode borrows the keyboard when the modifier goes down, so eligibility (not current focus) gates it.
        If Not WindowsInput.Current.Activate(hwnd) OrElse Not WindowsInput.TargetCanReceiveInput(hwnd) Then Return False
        Return SendPhysicalSkillShortcut(modifier, digit, holdMs, token)
    End Function

    Private Shared Function SendPhysicalSkillShortcut(modifier As Integer, digit As Integer, holdMs As Integer, token As CancellationToken) As Boolean
        If WindowsInput.BackgroundOnly OrElse token.IsCancellationRequested Then Return False
        Dim backgroundClient = TypeOf WindowsInput.Current Is SdlBackgroundWindowsInput
        Dim modifierScan = If(backgroundClient, CByte(0), CByte(NativeMethods.MapVirtualKey(CUInt(modifier), 0)))
        Dim digitScan = If(backgroundClient, CByte(0), CByte(NativeMethods.MapVirtualKey(CUInt(digit), 0)))
        Dim pressMs = WindowsInput.KeyPressDurationMs(Math.Clamp(holdMs, 5, 5000))
        Try
            SendKeyboardInput(CByte(modifier), modifierScan, 0, UIntPtr.Zero)
            SendKeyboardInput(CByte(digit), digitScan, 0, UIntPtr.Zero)
            If token.CanBeCanceled Then
                token.WaitHandle.WaitOne(pressMs)
            Else
                Thread.Sleep(pressMs)
            End If
            Return Not token.IsCancellationRequested
        Finally
            Try
                SendKeyboardInput(CByte(digit), digitScan, 2, UIntPtr.Zero)
            Finally
                SendKeyboardInput(CByte(modifier), modifierScan, 2, UIntPtr.Zero)
            End Try
        End Try
    End Function
End Class
