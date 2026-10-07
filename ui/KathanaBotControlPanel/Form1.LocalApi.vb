Imports System.IO
Imports System.Text.Json
Imports System.Threading.Tasks

Partial Public Class Form1
    Private _localApi As LocalBotApi
    Private _apiClosing As Boolean
    Private _apiSessionFile As String

    Private Async Sub StartLocalApi()
        Try
            _localApi = New LocalBotApi(AddressOf DispatchApiCommand)
            Await _localApi.StartAsync()
            If _apiClosing Then
                Await _localApi.StopAsync()
                Return
            End If
            Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KathanaBot", "api")
            Directory.CreateDirectory(folder)
            _apiSessionFile = Path.Combine(folder, Environment.ProcessId.ToString() & ".json")
            File.WriteAllText(_apiSessionFile, JsonSerializer.Serialize(New With {
                .address = _localApi.Address, .token = _localApi.Token, .botProcessId = Environment.ProcessId}))
            AppendLog("Local API: " & _localApi.Address & " — session file: " & _apiSessionFile)
        Catch ex As Exception
            AppendLog("Local API unavailable: " & ex.Message)
        End Try
    End Sub

    Private Async Sub StopLocalApi()
        _apiClosing = True
        Try
            If _apiSessionFile IsNot Nothing Then File.Delete(_apiSessionFile)
            If _apiSessionFile IsNot Nothing AndAlso _localApi IsNot Nothing Then Await _localApi.StopAsync()
        Catch ex As Exception
            RuntimeJournal.Record("Local API shutdown", ex.Message)
        End Try
    End Sub

    Private Function DispatchApiCommand(route As String, command As ApiCommand) As Task(Of Object)
        Dim completion As New TaskCompletionSource(Of Object)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim queuedAt = Environment.TickCount64
        If _apiClosing OrElse IsDisposed Then Throw New InvalidOperationException("Bot is closing.")
        BeginInvoke(New Action(Sub()
                                  Try
                                      If _apiClosing Then Throw New InvalidOperationException("Bot is closing.")
                                      If Environment.TickCount64 - queuedAt >= 10000 Then Throw New InvalidOperationException("Command expired before execution.")
                                      completion.SetResult(ExecuteApiCommand(route, command))
                                  Catch ex As Exception
                                      completion.SetException(ex)
                                  End Try
                              End Sub))
        Return completion.Task.WaitAsync(TimeSpan.FromSeconds(10))
    End Function

    Private Function ExecuteApiCommand(route As String, command As ApiCommand) As Object
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        Select Case route
            Case "/start"
                If selected Is Nothing Then Throw New InvalidOperationException("Select a game process in the bot first.")
                StartEdition(BotEdition.Full, False)
                If Not _fullEngine.IsRunning() Then Throw New InvalidOperationException("Start was blocked by the current bot mode.")
            Case "/stop"
                CancelLaunchAutoStart()
                StopEdition(BotEdition.Full, False, "local API")
            Case "/key", "/click"
                If route = "/click" AndAlso WindowsInput.KeyboardOnlyMode Then Throw New InvalidOperationException("The selected input backend supports key presses only.")
                If selected Is Nothing OrElse selected.ProcessId <> command.ProcessId Then Throw New InvalidOperationException("Selected process does not match processId.")
                If _fullEngine.IsRunning() OrElse _liteEngine.IsRunning() OrElse _workflowModes.Current <> OperatingMode.Idle OrElse _tradeRunning Then Throw New InvalidOperationException("Stop active automation before manual API input.")
                If route = "/key" AndAlso (command.Key < 8 OrElse command.Key > 254) Then Throw New InvalidOperationException("key must be a Windows virtual-key code from 8 to 254.")
                SyncLock WindowsInput.SequenceLock
                    WindowsInput.BindTarget(selected.MainWindowHandle)
                    If Not WindowsInput.Current.Activate(selected.MainWindowHandle) Then Throw New InvalidOperationException("Selected game is unavailable for input. Restore Kathana and select its window in the foreground.")
                    If Not WindowsInput.TargetCanReceiveInput(selected.MainWindowHandle) Then Throw New InvalidOperationException("Selected game is not ready for input. Keep its window in the foreground; input skipped.")
                    Dim input = WindowsInput.Current
                    Dim flags As UInteger = If({33, 34, 35, 36, 37, 38, 39, 40, 45, 46, 91, 92, 93, 111, 144, 163, 165}.Contains(command.Key), 1UI, 0UI)
                    Dim pressed = False
                    Try
                        If route = "/key" Then
                            input.Keyboard(CByte(command.Key), 0, flags, UIntPtr.Zero)
                        Else
                            input.Mouse(2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
                        End If
                        pressed = True
                        ' Games that poll state once per frame can miss an immediate down/up pair.
                        Threading.Thread.Sleep(If(route = "/key", WindowsInput.KeyPressDurationMs(120), 120))
                    Finally
                        If pressed Then
                            If route = "/key" Then
                                input.Keyboard(CByte(command.Key), 0, flags Or 2UI, UIntPtr.Zero)
                            Else
                                input.Mouse(4UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
                            End If
                        End If
                    End Try
                End SyncLock
        End Select
        Return New With {.running = _fullEngine.IsRunning(),
            .processId = If(selected Is Nothing, 0, selected.ProcessId),
            .windowTitle = If(selected Is Nothing, "", selected.WindowTitle),
            .inputMode = WindowsInput.InputMode,
            .inputError = WindowsInput.LastConnectionError, .botProcessId = Environment.ProcessId}
    End Function
End Class
