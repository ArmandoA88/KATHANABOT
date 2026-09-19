Imports System.Collections.Concurrent
Imports System.Threading

Public Interface IBotClock
    ReadOnly Property Tick As Long
    ReadOnly Property UtcNow As DateTime
    Sub Delay(milliseconds As Integer)
End Interface

Public Class SystemBotClock
    Implements IBotClock
    Public ReadOnly Property Tick As Long Implements IBotClock.Tick
        Get
            Return Environment.TickCount64
        End Get
    End Property
    Public ReadOnly Property UtcNow As DateTime Implements IBotClock.UtcNow
        Get
            Return DateTime.UtcNow
        End Get
    End Property
    Public Sub Delay(milliseconds As Integer) Implements IBotClock.Delay
        Thread.Sleep(Math.Max(0, milliseconds))
    End Sub
End Class

Public Enum OperatingMode
    Idle
    Full
    Lite
    Trade
    Resu
    Quiz
End Enum

Public Class OperatingModeController
    Private _mode As OperatingMode = OperatingMode.Idle
    Public ReadOnly Property Current As OperatingMode
        Get
            SyncLock Me
                Return _mode
            End SyncLock
        End Get
    End Property
    Public Function Transition(nextMode As OperatingMode, reason As String) As Boolean
        SyncLock Me
            If _mode = nextMode Then Return True
            If _mode <> OperatingMode.Idle AndAlso nextMode <> OperatingMode.Idle Then
                RuntimeJournal.Record("Mode blocked", $"Stop {_mode} before starting {nextMode}: {reason}")
                Return False
            End If
            RuntimeJournal.Record("Mode", $"{_mode} -> {nextMode}: {reason}")
            _mode = nextMode
            Return True
        End SyncLock
    End Function
End Class

Public NotInheritable Class ActionPriorityPolicy
    Public Shared Function Rank(role As String) As Integer
        Select Case If(role, "").ToLowerInvariant()
            Case "stop" : Return 0
            Case "max_health" : Return 10
            Case "heal" : Return 20
            Case "mana" : Return 30
            Case "repair" : Return 40
            Case "high_max_hp" : Return 50
            Case "attack", "special" : Return 60
            Case "buff" : Return 70
            Case Else : Return 100
        End Select
    End Function
    Public Shared Function Ordered(actions As IEnumerable(Of ActionRule)) As List(Of ActionRule)
        Return actions.OrderBy(Function(a) Rank(a.Role)).ThenBy(Function(a) a.Priority).ToList()
    End Function
    Public Shared Function UrgentHealth(hp As Double, actions As IEnumerable(Of ActionRule)) As Boolean
        Return hp <= 0 OrElse actions.Any(Function(a) a.Enabled AndAlso (a.Role = "heal" OrElse a.Role = "max_health") AndAlso hp <= a.TriggerPercent)
    End Function
    Public Shared Function CanRunOffense(dead As Boolean, recoverySent As Boolean, movementSent As Boolean) As Boolean
        Return Not dead AndAlso Not recoverySent AndAlso Not movementSent
    End Function
    Public Shared Function CanRunOptional(dead As Boolean, urgent As Boolean, actionSent As Boolean) As Boolean
        Return Not dead AndAlso Not urgent AndAlso Not actionSent
    End Function
End Class

Public Class RuntimeEvent
    Public Property At As DateTime
    Public Property Kind As String
    Public Property Detail As String
End Class

Public NotInheritable Class RuntimeJournal
    Private Shared ReadOnly Events As New Queue(Of RuntimeEvent)
    Private Shared ReadOnly LastDetails As New Dictionary(Of String, (Text As String, At As DateTime))
    Public Shared Sub Record(kind As String, detail As String)
        SyncLock Events
            Dim previous As (Text As String, At As DateTime) = ("", DateTime.MinValue)
            Dim identity = kind & ":" & detail.Split(":"c)(0)
            If LastDetails.TryGetValue(identity, previous) AndAlso (DateTime.UtcNow - previous.At).TotalSeconds < 2 Then Return
            If LastDetails.Count > 600 Then LastDetails.Clear()
            LastDetails(identity) = (detail, DateTime.UtcNow)
            Events.Enqueue(New RuntimeEvent With {.At = DateTime.UtcNow, .Kind = kind, .Detail = detail})
            While Events.Count > 300
                Events.Dequeue()
            End While
        End SyncLock
    End Sub
    Public Shared Function Snapshot() As List(Of RuntimeEvent)
        SyncLock Events
            Return Events.Reverse().ToList()
        End SyncLock
    End Function
End Class

Public Class OcrObservation
    Public Property At As DateTime
    Public Property Text As String
    Public Property Confidence As String = "Not supplied by Windows OCR"
End Class

Public NotInheritable Class ReadingMonitor
    Private Shared ReadOnly Values As New ConcurrentDictionary(Of String, OcrObservation)
    Public Shared Sub Record(kind As String, text As String, Optional confidence As String = "Not supplied by Windows OCR", Optional capturedAt As DateTime = Nothing)
        Values(kind) = New OcrObservation With {.At = If(capturedAt = DateTime.MinValue, DateTime.UtcNow, capturedAt), .Text = text, .Confidence = confidence}
    End Sub
    Public Shared Function Snapshot() As Dictionary(Of String, OcrObservation)
        Return Values.ToDictionary(Function(p) p.Key, Function(p) p.Value)
    End Function
End Class

Public Class FrameDeadline
    Private ReadOnly _clock As IBotClock
    Private ReadOnly _captured As Long
    Private ReadOnly _lifetime As Integer
    Public Sub New(clock As IBotClock, lifetimeMs As Integer)
        _clock = clock
        _captured = clock.Tick
        _lifetime = Math.Max(1, lifetimeMs)
    End Sub
    Public ReadOnly Property IsFresh As Boolean
        Get
            Return _clock.Tick >= _captured AndAlso _clock.Tick - _captured < _lifetime
        End Get
    End Property
End Class
