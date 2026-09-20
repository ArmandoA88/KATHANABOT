Imports System.Threading
Imports System.Threading.Tasks

' One cancellable E/R stream; no catch-up bursts after a pause or slow input operation.
Public NotInheritable Class DirectKpSequence
    Public Shared Function SendPair(send As Func(Of String, Boolean), allowed As Func(Of Boolean), token As CancellationToken) As Boolean
        SyncLock WindowsInput.SequenceLock
            If token.IsCancellationRequested OrElse Not allowed() Then Return False
            Dim started = Diagnostics.Stopwatch.StartNew()
            If Not send("E") Then Return False
            Dim remaining = Math.Max(0, 100 - CInt(started.ElapsedMilliseconds))
            If token.WaitHandle.WaitOne(remaining) OrElse Not allowed() Then Return False
            Return send("R")
        End SyncLock
    End Function

    Public Shared Async Function RunAsync(send As Func(Of String, Boolean), allowed As Func(Of Boolean), interval As Func(Of Integer), token As CancellationToken) As Task
        While Not token.IsCancellationRequested
            If Not allowed() Then
                Await Task.Delay(20, token)
                Continue While
            End If
            Dim cycle = Diagnostics.Stopwatch.StartNew()
            SendPair(send, allowed, token)
            ' At least 100 ms after the R release, even at the minimum cycle setting.
            Dim delay = Math.Max(100, Math.Clamp(interval(), 200, 60000) - CInt(cycle.ElapsedMilliseconds))
            Await Task.Delay(delay, token)
        End While
    End Function
End Class
