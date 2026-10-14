Imports System.Reflection
Imports System.Runtime.CompilerServices

Friend Module WindowCaptureAlertTests
    Private Const InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private Const StaticFlags As BindingFlags = BindingFlags.Static Or BindingFlags.NonPublic
    Private checks As Integer

    Public Sub Run()
        checks = 0
        TestConfirmationBoundaries()
        TestConsecutiveFailuresAndRecovery()
        TestNotificationText()
        Console.WriteLine($"PASS: {checks} capture-alert assertions: 60-second/100-sample OR boundaries, healthy-sample reset, separate failure streaks, warmup/recovery suppression and matching notification text (offline; no notifications sent).")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Capture-alert test: " & reason)
    End Sub

    Private Function Confirmed(firstSeen As DateTime, samples As Integer, captureFailure As Boolean, now As DateTime) As Boolean
        Return CBool(GetType(Form1).GetMethod("IsGameWindowAlertConfirmed", StaticFlags).Invoke(Nothing, {firstSeen, samples, captureFailure, now}))
    End Function

    Private Sub TestConfirmationBoundaries()
        Dim now As New DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)
        Check(Not Confirmed(DateTime.MinValue, 100, True, now), "a cleared streak must never alert")
        Check(Not Confirmed(now.AddSeconds(-30), 50, True, now), "the old 30-second/50-sample threshold must not alert on capture failure")
        Check(Not Confirmed(now.AddMilliseconds(-59999), 99, True, now), "59.999 seconds and 99 samples must remain pending")
        Check(Confirmed(now.AddSeconds(-60), 1, True, now), "exactly 60 seconds must confirm even with fewer than 100 samples")
        Check(Confirmed(now, 100, True, now), "exactly 100 samples must confirm even before 60 seconds")
        Check(Confirmed(now.AddSeconds(-61), 101, True, now), "long sustained failures must confirm")
        Check(Not Confirmed(now.AddMilliseconds(-29999), 49, False, now), "missing-window alerts must keep their prior pending boundary")
        Check(Confirmed(now.AddSeconds(-30), 1, False, now), "missing-window time confirmation must remain 30 seconds")
        Check(Confirmed(now, 50, False, now), "missing-window sample confirmation must remain 50 samples")
        Dim critical = GetType(Form1).GetMethod("IsCriticalAlertConfirmed", StaticFlags)
        Check(CBool(critical.Invoke(Nothing, {DateTime.UtcNow, 50})), "HP/death confirmation must retain the original sample threshold")
    End Sub

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, InstanceFlags).SetValue(owner, value)
    End Sub

    Private Function Field(Of T)(owner As Object, name As String) As T
        Return DirectCast(GetType(Form1).GetField(name, InstanceFlags).GetValue(owner), T)
    End Function

    Private Sub Observe(owner As Object, status As BotStatus)
        GetType(Form1).GetMethod("HandleWindowMissingAlarm", InstanceFlags).Invoke(owner, {status})
    End Sub

    Private Sub TestConsecutiveFailuresAndRecovery()
        ' No Form constructor, message pump, engines or transport: only the status
        ' handler runs, and all observations stay below the notification boundary.
        Dim owner = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim failed As New BotStatus With {.Running = True, .WindowFound = True, .ErrorMessage = "Capture failed.", .HpPercent = 100}
        For sample = 1 To 99
            Observe(owner, failed)
            Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = sample AndAlso Not Field(Of Boolean)(owner, "_windowMissingNotificationLatched"), "capture failure must stay pending through sample " & sample)
        Next
        Check(Field(Of Boolean)(owner, "_windowMissingIsCaptureFailure"), "the handler must use the capture threshold for capture failures")
        Observe(owner, New BotStatus With {.Running = True, .WindowFound = True, .HpPercent = 100})
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 0 AndAlso Field(Of DateTime)(owner, "_windowMissingFirstSeenUtc") = DateTime.MinValue, "one healthy capture must cancel the entire streak")
        Observe(owner, failed)
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 1, "a new failure after recovery must start at one sample")
        Observe(owner, New BotStatus With {.Running = True, .WindowFound = False, .ErrorMessage = "Window not found."})
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 1 AndAlso Not Field(Of Boolean)(owner, "_windowMissingIsCaptureFailure"), "window-missing samples must not inherit a capture-failure streak")
        Observe(owner, failed)
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 1 AndAlso Field(Of Boolean)(owner, "_windowMissingIsCaptureFailure"), "capture-failure samples must not inherit a window-missing streak")
        SetField(owner, "_notificationWarmupUntilUtc", DateTime.UtcNow.AddMinutes(1))
        Observe(owner, failed)
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 0 AndAlso Field(Of DateTime)(owner, "_windowMissingFirstSeenUtc") = DateTime.MinValue, "startup warmup must clear pending capture failures")
        SetField(owner, "_notificationWarmupUntilUtc", DateTime.MinValue)
        Observe(owner, failed)
        SetField(owner, "_disconnectRecoveryPending", True)
        Observe(owner, failed)
        Check(Field(Of Integer)(owner, "_windowMissingConfirmCount") = 0, "disconnect recovery must clear pending capture failures")
        SetField(owner, "_disconnectRecoveryPending", False)
        SetField(owner, "_windowMissingNotificationLatched", True)
        Observe(owner, New BotStatus With {.Running = False})
        Check(Not Field(Of Boolean)(owner, "_windowMissingNotificationLatched") AndAlso Field(Of DateTime)(owner, "_windowMissingFirstSeenUtc") = DateTime.MinValue, "stopping must clear the alert latch and streak")
    End Sub

    Private Sub TestNotificationText()
        Dim body = GetType(Form1).GetMethod("BuildGameWindowAlertBody", StaticFlags)
        Dim captureText = CStr(body.Invoke(Nothing, {True}))
        Check(captureText.Contains("60 seconds or 100 consecutive status samples"), "capture notifications must state the actual raised threshold")
        Dim missingText = CStr(body.Invoke(Nothing, {False}))
        Check(missingText.Contains("30 seconds or 50 consecutive status samples"), "missing-window notifications must retain their own threshold")
    End Sub
End Module
