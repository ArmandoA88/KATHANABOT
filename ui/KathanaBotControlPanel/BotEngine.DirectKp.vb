Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class BotEngine
    Private _directKpAllowedUntil As Long
    Private _directKpWindow As IntPtr
    Private _directKpTask As Task
    Private _directKpCts As CancellationTokenSource

    Private Sub PermitDirectKp(hwnd As IntPtr, cfg As BotConfig, token As CancellationToken)
        If token.IsCancellationRequested OrElse Not Volatile.Read(_config).DirectKpEnabled Then Return
        If _directKpWindow <> hwnd Then StopDirectKpWorker()
        _directKpWindow = hwnd
        Interlocked.Exchange(_directKpAllowedUntil, Environment.TickCount64 + Math.Max(1000, cfg.LoopMs * 2 + 500))
        If _directKpTask IsNot Nothing AndAlso Not _directKpTask.IsCompleted Then Return
        _directKpCts?.Dispose()
        _directKpCts = CancellationTokenSource.CreateLinkedTokenSource(token)
        Dim runToken = _directKpCts.Token
        Dim generation = _runGeneration
        Dim allowed As Func(Of Boolean) =
            Function()
                Dim current = Volatile.Read(_config)
                Return Not runToken.IsCancellationRequested AndAlso generation = _runGeneration AndAlso
                    current IsNot Nothing AndAlso current.DirectKpEnabled AndAlso Not current.LiteModeEnabled AndAlso
                    Not current.ResuHoldPlaceOnlyModeEnabled AndAlso _status.Running AndAlso _status.WindowFound AndAlso
                    Not IsChatInputPaused() AndAlso Not _combatPausedForDeath AndAlso Not _status.GameDisconnected AndAlso
                    Environment.TickCount64 < Interlocked.Read(_directKpAllowedUntil) AndAlso
                    Not NativeMethods.IsIconic(hwnd) AndAlso ResolveGameWindow(current) = hwnd
            End Function
        _directKpTask = Task.Run(
            Async Function()
                Try
                    Await DirectKpSequence.RunAsync(
                        Function(key) If(key = "E" AndAlso Volatile.Read(_config).AutoAssistOnlyEnabled, True, SendKey(hwnd, key, 5, forceBackgroundPost:=True)),
                        allowed, Function() Volatile.Read(_config).DirectKpIntervalMs, runToken)
                Catch ex As OperationCanceledException
                    ' Stop/toggle-off cancels the pending R as well as future cycles.
                Catch ex As Exception
                    RuntimeJournal.Record("Direct KP", "Input stopped: " & ex.Message)
                End Try
            End Function)
    End Sub

    Public Sub StopDirectKpWorker()
        Interlocked.Exchange(_directKpAllowedUntil, 0)
        _directKpCts?.Cancel()
        Try
            _directKpTask?.Wait(1000)
        Catch ex As AggregateException
            RuntimeJournal.Record("Direct KP", "Worker stopped: " & ex.GetBaseException().Message)
        End Try
    End Sub
End Class
