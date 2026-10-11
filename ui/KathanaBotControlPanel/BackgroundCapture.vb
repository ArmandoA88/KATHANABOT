Imports System.Threading
Imports KathanaWindowCapture

' Capture is independent of focus and input. A loop pins the timestamp of its first observation;
' later reads cannot silently make decisions from that observation appear newer.
Public NotInheritable Class BackgroundCapture
    Public Const MaximumFrameAgeMs As Integer = 300
    Public Const MaximumDecisionAgeMs As Integer = 500
    Private Shared ReadOnly gate As New Object
    Private Shared ReadOnly sessions As New Dictionary(Of IntPtr, CaptureEntry)
    Private Shared ReadOnly observation As New AsyncLocal(Of DecisionObservation)
    Private Shared lastWindow As IntPtr

    Private Class CaptureEntry
        Public Pid As UInteger
        Public Session As WindowCapture
        Public UsedAt As Long
        Public RetryAt As Long
    End Class

    Private Class DecisionObservation
        Public Hwnd As IntPtr
        Public CapturedAt As Long = -1
    End Class

    Private NotInheritable Class DecisionScope
        Implements IDisposable
        Private ReadOnly previous As DecisionObservation
        Private disposed As Boolean
        Public Sub New()
            previous = observation.Value
            observation.Value = New DecisionObservation()
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            observation.Value = previous
        End Sub
    End Class

    Public Shared Function BeginDecision() As IDisposable
        Return New DecisionScope()
    End Function

    Public Shared Function TryCopy(hwnd As IntPtr, width As Integer, height As Integer, ByRef capturedAt As Long) As Bitmap
        capturedAt = -1
        Dim pid As UInteger
        NativeMethods.GetWindowThreadProcessId(hwnd, pid)
        If hwnd = IntPtr.Zero OrElse pid = 0 OrElse NativeMethods.IsIconic(hwnd) Then Return Nothing
        Dim entry As CaptureEntry = Nothing
        Dim retired As New List(Of WindowCapture)
        SyncLock gate
            Dim now = Environment.TickCount64
            lastWindow = hwnd
            For Each pair In sessions.ToArray()
                If pair.Key <> hwnd AndAlso (now - pair.Value.UsedAt > 10000 OrElse sessions.Count >= 4) Then
                    sessions.Remove(pair.Key)
                    retired.Add(pair.Value.Session)
                End If
            Next
            If sessions.TryGetValue(hwnd, entry) AndAlso
                (entry.Pid <> pid OrElse (entry.Session.Failed AndAlso now >= entry.RetryAt)) Then
                sessions.Remove(hwnd)
                retired.Add(entry.Session)
                entry = Nothing
            End If
            If entry Is Nothing Then
                entry = New CaptureEntry With {.Pid = pid, .Session = New WindowCapture(hwnd, pid, rejectBlackFrames:=True), .RetryAt = now + 2000}
                sessions(hwnd) = entry
            End If
            entry.UsedAt = now
        End SyncLock
        For Each old In retired
            Dim closing = old
            Task.Run(Sub() closing.Dispose())
        Next
        Dim frame = entry.Session.Copy(width, height, MaximumFrameAgeMs, capturedAt)
        Return frame
    End Function

    Public Shared Sub AcceptFrame(hwnd As IntPtr, capturedAt As Long)
        Dim current = observation.Value
        If current IsNot Nothing AndAlso current.CapturedAt < 0 Then
            current.Hwnd = hwnd
            current.CapturedAt = capturedAt
        End If
    End Sub

    Public Shared Function DecisionIsFresh(capturedAt As Long, now As Long) As Boolean
        Return capturedAt >= 0 AndAlso capturedAt <= now AndAlso now - capturedAt <= MaximumDecisionAgeMs
    End Function

    Public Shared Function InputBlocker(hwnd As IntPtr) As String
        Dim current = observation.Value
        If current IsNot Nothing AndAlso (current.Hwnd <> hwnd OrElse Not DecisionIsFresh(current.CapturedAt, Environment.TickCount64)) Then
            Return "waiting for a fresh action decision"
        End If
        Dim entry As CaptureEntry = Nothing
        SyncLock gate
            sessions.TryGetValue(hwnd, entry)
        End SyncLock
        If entry Is Nothing OrElse Not entry.Session.Status(MaximumFrameAgeMs).Ready Then Return "game-window capture is unavailable or stale"
        Return ""
    End Function

    Public Shared Function StatusText() As String
        Dim entry As CaptureEntry = Nothing
        SyncLock gate
            sessions.TryGetValue(lastWindow, entry)
        End SyncLock
        If entry Is Nothing Then Return "Capture waiting for game"
        Dim status = entry.Session.Status(MaximumFrameAgeMs)
        Return If(status.Ready, $"Capture live ({status.AgeMs} ms old)", If(status.AgeMs = Long.MaxValue, status.Note, $"Capture paused ({status.AgeMs} ms old)"))
    End Function

    Public Shared Sub [Stop]()
        Dim closing As WindowCapture()
        SyncLock gate
            closing = sessions.Values.Select(Function(entry) entry.Session).ToArray()
            sessions.Clear()
            lastWindow = IntPtr.Zero
        End SyncLock
        For Each capture In closing
            Dim old = capture
            Task.Run(Sub() old.Dispose())
        Next
    End Sub
End Class
