Imports System.Threading

Public Interface IWindowsInput
    ' Legacy message-shaped requests are translated by the selected input backend.
    Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    Function Activate(hwnd As IntPtr) As Boolean
    Function MoveCursor(x As Integer, y As Integer) As Boolean
    Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr)
    Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr)
End Interface

Public NotInheritable Class WindowsInput
    Private Shared ReadOnly LocalInput As New AsyncLocal(Of IWindowsInput)
    Private Shared ReadOnly DefaultInput As New SdlBackgroundWindowsInput(New NativeBackgroundInputPlatform(),
        keyboardMode:=InitialKeyboardMode(), keyboardOnly:=True)
    Private Shared ReadOnly ReleaseTimer As New System.Threading.Timer(AddressOf MonitorReleases, Nothing, 20, 20)
    Public Shared ReadOnly SequenceLock As New Object
    ' Retained for old profiles; the selected backend controls its supported inputs.
    Public Shared Property BackgroundOnly As Boolean
        Get
            Return False
        End Get
        Set(value As Boolean)
        End Set
    End Property
    Private Shared Sub MonitorReleases(state As Object)
        Try
            DefaultInput.Maintain()
        Catch ex As Exception
            RuntimeJournal.Record("Input release error", ex.Message)
        End Try
    End Sub
    Public Shared Sub ReleaseAll()
        DefaultInput.ReleaseAll()
    End Sub
    Public Shared Sub ReleaseTarget(hwnd As IntPtr)
        If LocalInput.Value Is Nothing Then DefaultInput.ReleaseTarget(hwnd)
    End Sub
    Public Shared Sub BindTarget(hwnd As IntPtr)
        DefaultInput.BindTarget(hwnd)
    End Sub
    Public Shared ReadOnly Property LastConnectionError As String
        Get
            Return ""
        End Get
    End Property
    Public Shared Sub RequireConnection(hwnd As IntPtr)
        If Not Current.Activate(hwnd) Then
            Throw New InvalidOperationException("Selected game is unavailable for background keys. Keep Kathana open and not minimized; another app can remain active.")
        End If
    End Sub
    Public Shared Function TargetIsForeground(hwnd As IntPtr) As Boolean
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing Then Return foreground.IsWindowForeground(hwnd)
        Dim background = TryCast(Current, SdlBackgroundWindowsInput)
        If background IsNot Nothing Then Return background.CanReceive(hwnd)
        Return True ' Other injected test backends implement their own validation.
    End Function
    Public Shared Function TargetCanReceiveInput(hwnd As IntPtr) As Boolean
        Return TargetIsForeground(hwnd)
    End Function
    Public Shared ReadOnly Property UsesInternalHook As Boolean
        Get
            Return TypeOf Current Is InternalHookWindowsInput
        End Get
    End Property
    Public Shared ReadOnly Property UsesTargetedInput As Boolean
        Get
            Return UsesInternalHook OrElse TypeOf Current Is SdlBackgroundWindowsInput
        End Get
    End Property
    Public Shared ReadOnly Property KeyboardOnlyMode As Boolean
        Get
            Dim background = TryCast(Current, SdlBackgroundWindowsInput)
            Return background IsNot Nothing AndAlso background.KeyboardOnly
        End Get
    End Property
    Public Shared ReadOnly Property BackgroundKeyMode As BackgroundKeyboardMode
        Get
            Return DefaultInput.KeyboardMode
        End Get
    End Property
    Public Shared Function TrySetBackgroundKeyMode(mode As BackgroundKeyboardMode) As Boolean
        SyncLock SequenceLock
            Return DefaultInput.SetKeyboardMode(mode)
        End SyncLock
    End Function
    Public Shared Function KeyboardModeId(mode As BackgroundKeyboardMode) As String
        Select Case mode
            Case BackgroundKeyboardMode.PostedScanCode : Return "posted-scan"
            Case BackgroundKeyboardMode.SynchronousScanCode : Return "send-scan"
            Case BackgroundKeyboardMode.PostedZeroScanCode : Return "posted-zero"
            Case BackgroundKeyboardMode.SynchronousZeroScanCode : Return "send-zero"
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(mode))
        End Select
    End Function
    Public Shared Function TryParseKeyboardMode(value As String, ByRef mode As BackgroundKeyboardMode) As Boolean
        Select Case If(value, "").Trim().ToLowerInvariant()
            Case "posted-scan" : mode = BackgroundKeyboardMode.PostedScanCode
            Case "send-scan" : mode = BackgroundKeyboardMode.SynchronousScanCode
            Case "posted-zero" : mode = BackgroundKeyboardMode.PostedZeroScanCode
            Case "send-zero" : mode = BackgroundKeyboardMode.SynchronousZeroScanCode
            Case Else : Return False
        End Select
        Return True
    End Function
    Private Shared Function InitialKeyboardMode() As BackgroundKeyboardMode
        Dim arguments = Environment.GetCommandLineArgs()
        Dim index = Array.IndexOf(arguments, "--background-key-mode")
        If index < 0 Then Return BackgroundKeyboardMode.PostedScanCode
        Dim mode As BackgroundKeyboardMode
        If index + 1 >= arguments.Length OrElse Not TryParseKeyboardMode(arguments(index + 1), mode) Then
            Throw New ArgumentException("--background-key-mode requires posted-scan, send-scan, posted-zero or send-zero.")
        End If
        Return mode
    End Function
    Public Shared Function KeyPressDurationMs(requested As Integer) As Integer
        If Not TypeOf Current Is SdlBackgroundWindowsInput AndAlso Not TypeOf Current Is ForegroundWindowsInput Then Return Math.Max(5, requested)
        ' Keep the 150 ms floor and sample each short tap independently in both backends.
        ' Configured holds of 250 ms or longer stay exact; cleanup adds no jitter.
        Dim minimumMs = Math.Max(150, requested)
        If minimumMs >= 250 Then Return minimumMs
        Return Random.Shared.Next(minimumMs, 251)
    End Function
    Public Shared ReadOnly Property InputMode As String
        Get
            If UsesInternalHook Then Return "internal-hook"
            Dim background = TryCast(Current, SdlBackgroundWindowsInput)
            If background Is Nothing Then Return "foreground"
            Return If(background.KeyboardOnly, "background-keys-" & KeyboardModeId(background.KeyboardMode), "sdl-background")
        End Get
    End Property
    Public Shared Property Current As IWindowsInput
        Get
            Return If(LocalInput.Value, DefaultInput)
        End Get
        Set(value As IWindowsInput)
            LocalInput.Value = value
        End Set
    End Property
End Class
