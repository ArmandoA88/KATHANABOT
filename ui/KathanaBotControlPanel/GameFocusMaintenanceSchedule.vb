' UI focus recovery is independent of the engine's running state. Failed or denied
' native activation leaves the request pending and never restarts/stops an engine.
Friend NotInheritable Class GameFocusMaintenanceSchedule
    Private Const RetryIntervalMs As Long = 750
    Private Const ControlPanelGraceMs As Long = 3000
    Private _targetWindow As IntPtr
    Private _targetPid As UInteger
    Private _nextAttemptTick As Long
    Private _controlPanelWasForeground As Boolean
    Private _controlPanelGraceUntil As Long
    Private _waitingForForeground As Boolean

    Public ReadOnly Property WaitingForForeground As Boolean
        Get
            Return _waitingForForeground
        End Get
    End Property

    Public Sub Reset()
        _targetWindow = IntPtr.Zero
        _targetPid = 0
        _nextAttemptTick = 0
        _controlPanelWasForeground = False
        _controlPanelGraceUntil = 0
        _waitingForForeground = False
    End Sub

    Public Function TryBeginAttempt(nowTick As Long, gameplayRunning As Boolean, closing As Boolean,
                                    targetWindow As IntPtr, targetPid As UInteger,
                                    targetForeground As Boolean, interactionSuspended As Boolean,
                                    controlPanelForeground As Boolean) As Boolean
        If Not gameplayRunning OrElse closing OrElse targetWindow = IntPtr.Zero OrElse targetPid = 0 Then
            Reset()
            Return False
        End If
        If targetWindow <> _targetWindow OrElse targetPid <> _targetPid Then
            Reset()
            _targetWindow = targetWindow
            _targetPid = targetPid
        End If
        _waitingForForeground = Not targetForeground
        If interactionSuspended Then
            ' A closed modal dialog must leave time to use Stop on the main panel.
            _controlPanelWasForeground = False
            _controlPanelGraceUntil = 0
            Return False
        End If
        If controlPanelForeground AndAlso Not _controlPanelWasForeground Then
            _controlPanelGraceUntil = nowTick + ControlPanelGraceMs
        End If
        _controlPanelWasForeground = controlPanelForeground
        If targetForeground Then Return False
        If controlPanelForeground AndAlso nowTick < _controlPanelGraceUntil Then Return False
        If nowTick < _nextAttemptTick Then Return False
        _nextAttemptTick = nowTick + RetryIntervalMs
        Return True
    End Function
End Class
