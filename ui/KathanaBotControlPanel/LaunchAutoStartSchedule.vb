' Launch-only retries use the monotonic clock. A rejected start remains pending;
' an actual start or explicit stop consumes the request for this app session.
Friend NotInheritable Class LaunchAutoStartSchedule
    Private Const RetryIntervalMs As Long = 2000
    Private _nextPollTick As Long
    Private _pollInProgress As Boolean
    Private _armed As Boolean
    Private _pendingValue As Boolean
    Private _startedValue As Boolean
    Private _waitingReasonValue As String = ""

    Public ReadOnly Property Pending As Boolean
        Get
            Return _pendingValue
        End Get
    End Property
    Public ReadOnly Property Started As Boolean
        Get
            Return _startedValue
        End Get
    End Property
    Public ReadOnly Property WaitingReason As String
        Get
            Return _waitingReasonValue
        End Get
    End Property

    Public Sub Arm(disabled As Boolean)
        If _armed Then Return
        _armed = True
        _pendingValue = Not disabled
        _waitingReasonValue = If(disabled, "", "Starting the Full bot. Stop or F12 cancels automatic startup.")
    End Sub

    Public Function TryBeginPoll(nowTick As Long) As Boolean
        If Not Pending OrElse _pollInProgress OrElse nowTick < _nextPollTick Then Return False
        _pollInProgress = True
        Return True
    End Function

    Public Sub CompletePoll(nowTick As Long, didStart As Boolean, waitingReason As String)
        _pollInProgress = False
        If didStart Then
            _startedValue = True
            _pendingValue = False
            _waitingReasonValue = ""
            Return
        End If
        If Not Pending Then Return
        _nextPollTick = nowTick + RetryIntervalMs
        _waitingReasonValue = If(String.IsNullOrWhiteSpace(waitingReason), "Waiting to start the Full bot.", waitingReason)
    End Sub

    Public Sub Cancel()
        _pendingValue = False
        _waitingReasonValue = ""
    End Sub
End Class
