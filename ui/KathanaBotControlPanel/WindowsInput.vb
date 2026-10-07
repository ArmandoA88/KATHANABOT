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
    ' This release uses the OS foreground input stream for every gameplay request.
    ' Old profile/command-line background selections cannot change the production backend.
    Private Shared ReadOnly DefaultInput As New ForegroundWindowsInput(
        New NativeInputPlatform(useKathanaProcessNameValidation:=True), allowActivation:=False)
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
            DefaultInput.ReleaseUnfocused()
            ' Background mode: hand the keyboard back once the bot has stopped pressing keys.
            DefaultInput.ServiceFocusLease()
        Catch ex As Exception
            RuntimeJournal.Record("Input release error", ex.Message)
        End Try
    End Sub
    ' Background mode borrows the keyboard only while a key is pressed instead of keeping the game in
    ' front. It reuses the same SendInput path; only the focus handling around each key changes.
    Public Shared Sub ConfigureFocusBorrow(enabled As Boolean, yieldToUserMs As Integer, Optional pauseForFullScreen As Boolean = True)
        DefaultInput.FocusBorrow = If(enabled, New FocusBorrowSettings(True, yieldToUserMs, FocusBorrowSettings.DefaultLingerMs, pauseForFullScreen), FocusBorrowSettings.Disabled)
    End Sub
    Public Shared ReadOnly Property FocusBorrowEnabled As Boolean
        Get
            Dim foreground = TryCast(Current, ForegroundWindowsInput)
            Return foreground IsNot Nothing AndAlso foreground.BorrowEnabled
        End Get
    End Property
    ' True while the bot has the game in front only for a key press (not because the user chose to).
    Public Shared ReadOnly Property FocusBorrowActive As Boolean
        Get
            Dim foreground = TryCast(Current, ForegroundWindowsInput)
            Return foreground IsNot Nothing AndAlso foreground.BorrowActive
        End Get
    End Property
    Public Shared Function FocusBorrowStatus() As FocusBorrowSnapshot
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        Return If(foreground Is Nothing, New FocusBorrowSnapshot(), foreground.FocusBorrowStatus())
    End Function
    ' Process exit: release anything held and give the keyboard back before the watchdog stops.
    Public Shared Sub Shutdown()
        ReleaseAll()
        TryCast(Current, ForegroundWindowsInput)?.EndFocusLeaseNow()
    End Sub
    Public Shared Sub ReleaseAll()
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing Then
            foreground.ReleaseAll()
            Return
        End If
        TryCast(Current, SdlBackgroundWindowsInput)?.ReleaseAll()
    End Sub
    Public Shared Sub ReleaseTarget(hwnd As IntPtr)
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing Then
            foreground.ReleaseTarget(hwnd)
            Return
        End If
        TryCast(Current, SdlBackgroundWindowsInput)?.ReleaseTarget(hwnd)
    End Sub
    Public Shared Sub BindTarget(hwnd As IntPtr)
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing Then
            foreground.BindTarget(hwnd)
            Return
        End If
        TryCast(Current, SdlBackgroundWindowsInput)?.BindTarget(hwnd)
    End Sub
    Public Shared Function TryRestoreGameForeground(hwnd As IntPtr, expectedPid As UInteger) As Boolean
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground Is Nothing OrElse Not Monitor.TryEnter(SequenceLock) Then Return False
        Try
            Return foreground.TryRestoreGameForeground(hwnd, expectedPid)
        Finally
            Monitor.Exit(SequenceLock)
        End Try
    End Function
    Public Shared ReadOnly Property IsProductionForegroundInput As Boolean
        Get
            Return Object.ReferenceEquals(Current, DefaultInput)
        End Get
    End Property
    Public Shared ReadOnly Property LastConnectionError As String
        Get
            Return ""
        End Get
    End Property
    Public Shared Sub RequireConnection(hwnd As IntPtr)
        If Not Current.Activate(hwnd) Then
            Throw New InvalidOperationException("Selected Kathana window must be open, restored and in the foreground for input.")
        End If
    End Sub
    Public Shared Function TargetIsForeground(hwnd As IntPtr) As Boolean
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing Then Return foreground.IsWindowForeground(hwnd)
        Dim background = TryCast(Current, SdlBackgroundWindowsInput)
        If background IsNot Nothing Then Return background.CanReceive(hwnd)
        Return True ' Other injected test backends implement their own validation.
    End Function
    ' Eligible to receive a key now: in front already or, in Background mode, able to borrow the keyboard.
    Public Shared Function TargetCanReceiveInput(hwnd As IntPtr) As Boolean
        Dim foreground = TryCast(Current, ForegroundWindowsInput)
        If foreground IsNot Nothing AndAlso foreground.BorrowEnabled Then Return foreground.CanReceiveInput(hwnd)
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
    ' Keyboard-only runtime policy. Background mode never moves the mouse or clicks: the game is not in
    ' front, so a click would land on whatever window covers it.
    Public Shared ReadOnly Property KeyboardOnlyMode As Boolean
        Get
            If FocusBorrowEnabled Then Return True
            Dim background = TryCast(Current, SdlBackgroundWindowsInput)
            Return background IsNot Nothing AndAlso background.KeyboardOnly
        End Get
    End Property
    Public Shared ReadOnly Property BackgroundKeyMode As BackgroundKeyboardMode
        Get
            Dim background = TryCast(Current, SdlBackgroundWindowsInput)
            Return If(background Is Nothing, BackgroundKeyboardMode.PostedScanCode, background.KeyboardMode)
        End Get
    End Property
    Public Shared Function TrySetBackgroundKeyMode(mode As BackgroundKeyboardMode) As Boolean
        SyncLock SequenceLock
            Dim background = TryCast(Current, SdlBackgroundWindowsInput)
            Return background IsNot Nothing AndAlso background.SetKeyboardMode(mode)
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
            If background Is Nothing Then Return If(FocusBorrowEnabled, "foreground-borrow", "foreground")
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
