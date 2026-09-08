Public Class AutoLootArrowHoldSettings
    Public Property LeftEnabled As Boolean
    Public Property RightEnabled As Boolean
    Public Property LeftHoldMs As Integer = 500
    Public Property RightHoldMs As Integer = 500
    Public Property LeftIntervalMs As Integer = 10000
    Public Property RightIntervalMs As Integer = 10000

    Public Function Enabled(key As Integer) As Boolean
        Return If(key = &H25, LeftEnabled, RightEnabled)
    End Function

    Public Function HoldMs(key As Integer) As Integer
        Return Math.Clamp(If(key = &H25, LeftHoldMs, RightHoldMs), 100, 60000)
    End Function

    Public Function IntervalMs(key As Integer) As Integer
        Return Math.Clamp(If(key = &H25, LeftIntervalMs, RightIntervalMs), 100, 3600000)
    End Function
End Class

' Called under the engine lock. Opposite keys never overlap; overdue directions take turns.
Public Class AutoLootArrowHoldSchedule
    Private _leftReleasedAt As DateTime = DateTime.MinValue
    Private _rightReleasedAt As DateTime = DateTime.MinValue
    Public Property ActiveKey As Integer
        Get
            Return _activeKey
        End Get
        Private Set(value As Integer)
            _activeKey = value
        End Set
    End Property
    Private _activeKey As Integer

    Public Function TryStart(settings As AutoLootArrowHoldSettings, now As DateTime) As Integer
        If ActiveKey <> 0 OrElse settings Is Nothing Then Return 0
        Dim leftDue = settings.LeftEnabled AndAlso (now - _leftReleasedAt).TotalMilliseconds >= settings.IntervalMs(&H25)
        Dim rightDue = settings.RightEnabled AndAlso (now - _rightReleasedAt).TotalMilliseconds >= settings.IntervalMs(&H27)
        If leftDue AndAlso (Not rightDue OrElse _leftReleasedAt <= _rightReleasedAt) Then
            ActiveKey = &H25
        ElseIf rightDue Then
            ActiveKey = &H27
        End If
        Return ActiveKey
    End Function

    Public Sub Complete(now As DateTime)
        If ActiveKey = &H25 Then _leftReleasedAt = now
        If ActiveKey = &H27 Then _rightReleasedAt = now
        ActiveKey = 0
    End Sub
End Class
