Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading

' Independent of the engine's capture loop and locks. Never sends game input.
Public NotInheritable Class GameWindowResponsivenessMonitor
    Private ReadOnly _sync As New Object()
    Private ReadOnly _notify As Action(Of Boolean)
    Private ReadOnly _state As New GameWindowResponsivenessState()
    Private _timer As Timer
    Private _target As IntPtr
    Private _title As String = ""

    <DllImport("user32.dll")>
    Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
    End Function

    ' https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-ishungappwindow
    <DllImport("user32.dll")>
    Private Shared Function IsHungAppWindow(hwnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", EntryPoint:="SendMessageTimeoutW", SetLastError:=True)>
    Private Shared Function SendMessageTimeout(hwnd As IntPtr, message As UInteger, wParam As UIntPtr,
                                              lParam As IntPtr, flags As UInteger, timeout As UInteger,
                                              ByRef result As UIntPtr) As IntPtr
    End Function

    Public Sub New(notify As Action(Of Boolean))
        _notify = notify
    End Sub

    Public Sub UpdateTarget(cfg As BotConfig)
        SyncLock _sync
            Dim target = cfg.SelectedWindowHandle
            Dim title = If(cfg.WindowTitle, "")
            If target <> _target OrElse title <> _title Then _state.Reset()
            _target = target
            _title = title
        End SyncLock
    End Sub

    Public Sub Start(cfg As BotConfig)
        SyncLock _sync
            [Stop]()
            UpdateTarget(cfg)
            _timer = New Timer(AddressOf CheckWindow, Nothing, 5000, 5000)
        End SyncLock
    End Sub

    Public Sub [Stop]()
        SyncLock _sync
            If _timer IsNot Nothing Then _timer.Dispose()
            _timer = Nothing
            _state.Reset()
        End SyncLock
    End Sub

    Private Sub CheckWindow(unused As Object)
        ' Skip overlapping callbacks; never wait on an engine lock or an OCR operation.
        If Not Monitor.TryEnter(_sync) Then Return
        Try
            If _timer Is Nothing Then Return
            Dim hwnd = _target
            If hwnd = IntPtr.Zero Then hwnd = BotEngine.FindGameWindow(_title)
            Dim valid = hwnd <> IntPtr.Zero AndAlso IsWindow(hwnd)
            Dim hung As Boolean = False
            If valid Then
                ' A harmless WM_NULL ping also detects hangs before Windows creates a ghost.
                ' Timeout is bounded; access-denied/other probe failures are not freeze evidence.
                Dim result As UIntPtr
                Dim responded = SendMessageTimeout(hwnd, 0UI, UIntPtr.Zero, IntPtr.Zero, &H23UI, 1000UI, result) <> IntPtr.Zero
                Dim probeError = Marshal.GetLastWin32Error()
                valid = IsWindow(hwnd)
                hung = valid AndAlso Not responded AndAlso (probeError = 1460 OrElse IsHungAppWindow(hwnd))
                If Not responded AndAlso Not hung Then valid = False
            End If
            Dim transition = _state.Observe(If(valid, hwnd, IntPtr.Zero), hung, Environment.TickCount64)
            If transition <> 0 Then _notify(transition = 1)
        Catch ex As Exception
            ' An unavailable native probe is not evidence that the game is frozen.
            _state.Reset()
            Trace.WriteLine("Game responsiveness check failed: " & ex.Message)
        Finally
            Monitor.Exit(_sync)
        End Try
    End Sub
End Class

Public NotInheritable Class GameWindowResponsivenessState
    Public Const ReminderIntervalMs As Long = 5L * 60L * 1000L
    Private _window As IntPtr
    Private _firstHungAt As Long = -1
    Private _lastSampleAt As Long = -1
    Private _alerted As Boolean
    Private _lastAlertAt As Long = -1

    Public Sub Reset()
        _window = IntPtr.Zero
        _firstHungAt = -1
        _lastSampleAt = -1
        _alerted = False
        _lastAlertAt = -1
    End Sub

    ' 1 = freeze confirmed, -1 = recovered, 0 = no notification.
    Public Function Observe(hwnd As IntPtr, hung As Boolean, nowMs As Long) As Integer
        If hwnd = IntPtr.Zero OrElse hwnd <> _window Then
            Reset()
            _window = hwnd
        End If
        If hwnd = IntPtr.Zero Then Return 0
        ' Sleep/suspend or a long sampling gap cannot establish continuous unresponsiveness.
        If _lastSampleAt >= 0 AndAlso (nowMs < _lastSampleAt OrElse nowMs - _lastSampleAt > 10000) Then
            _firstHungAt = -1
        End If
        _lastSampleAt = nowMs
        If Not hung Then
            Dim recovered = _alerted
            _firstHungAt = -1
            _alerted = False
            _lastAlertAt = -1
            Return If(recovered, -1, 0)
        End If
        If _firstHungAt < 0 Then _firstHungAt = nowMs
        If Not _alerted AndAlso nowMs - _firstHungAt >= 15000 Then
            _alerted = True
            _lastAlertAt = nowMs
            Return 1
        End If
        If _alerted AndAlso _lastAlertAt >= 0 AndAlso nowMs - _lastAlertAt >= ReminderIntervalMs Then
            _lastAlertAt = nowMs
            Return 1
        End If
        Return 0
    End Function
End Class
