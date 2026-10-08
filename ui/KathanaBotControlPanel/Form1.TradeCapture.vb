Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private Const DefaultTradeCaptureSourceUrl As String = "https://discord.com/channels/1363839027114934315/1486808135883555037"
    Private _tradeCaptureButton As Button
    Private _tradeCaptureTimer As System.Windows.Forms.Timer
    Private _tradeCaptureServer As DiscordBrowserCaptureServer
    Private _tradeCaptureCancellation As CancellationTokenSource
    Private _tradeCaptureGeneration As Long
    Private _tradeCaptureSourceUrl As String
    Private _tradeCaptureMessageCount As Integer
    Private _tradeCaptureExpectedSourceText As String
    Private _tradeCaptureApplying As Boolean
    Private _tradeCapturePaused As Boolean
    Private _tradeCaptureCompleted As Boolean
    Private _tradeCaptureShutdown As Boolean
    Private _tradeCaptureDialog As DiscordTradeCaptureDialog
    ' Optional items typed in the capture dialog. The extension types them into Discord's own search box, so Discord filters the
    ' posts; empty means the channel is captured normally. Kept for this app session only.
    Private _tradeCaptureFilter As String = ""

    Private Function InitializeTradeBrowserCaptureControls() As Button
        _tradeCaptureSourceUrl = DefaultTradeCaptureSourceUrl
        _tradeCaptureMessageCount = 10000
        _tradeCaptureExpectedSourceText = ""
        _tradeCaptureShutdown = False
        _tradeCaptureTimer = New System.Windows.Forms.Timer With {.Interval = 100}
        _tradeCaptureButton = New Button With {.Text = "Browser capture", .AutoSize = True, .Margin = New Padding(0, 0, 3, 2)}
        AddHandler _tradeCaptureButton.Click, Sub() ConfigureTradeBrowserCapture()
        AddHandler _tradeCaptureTimer.Tick,
            Sub()
                If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
                    StopTradeBrowserCapture()
                    SetTradeDiscordStatus("Browser capture stopped. Existing posts and whispers are kept.")
                    Return
                End If
                UpdateTradeBrowserCaptureState()
            End Sub
        Return _tradeCaptureButton
    End Function

    Private Sub ConfigureTradeBrowserCapture()
        If _tradeCaptureShutdown OrElse _tradeLoading OrElse _tradeAnalyzing OrElse _tradeRunning OrElse _tradeDiscordConfiguring Then Return
        If _tradeCaptureDialog IsNot Nothing AndAlso Not _tradeCaptureDialog.IsDisposed Then
            _tradeCaptureDialog.Activate()
            Return
        End If
        Using dialog As New DiscordTradeCaptureDialog(_tradeCaptureSourceUrl, Math.Clamp(_tradeCaptureMessageCount, 1, 10000),
            AddressOf StartTradeBrowserCapture, AddressOf StopTradeBrowserCaptureFromDialog, _tradeCaptureFilter, Sub(text) _tradeCaptureFilter = If(text, ""))
            _tradeCaptureDialog = dialog
            dialog.InstallExtension = AddressOf InstallDiscordCaptureExtension
            If _tradeCaptureCompleted Then
                dialog.ShowCompletedCapture()
            Else
                dialog.UpdateConnection(If(_tradeCaptureServer IsNot Nothing, _tradeCaptureServer.ConnectionJson, ""))
            End If
            Try
                dialog.ShowDialog(Me)
            Finally
                _tradeCaptureDialog = Nothing
            End Try
        End Using
    End Sub

    ' The extension is embedded in this EXE. Unpack it to a stable folder (path copied, folder opened) and explain the
    ' one-time browser step; after an app update, installing again refreshes the files and Reload updates the browser.
    Private Sub InstallDiscordCaptureExtension()
        Dim folder = DiscordCaptureExtensionPackage.DefaultFolder()
        Try
            DiscordCaptureExtensionPackage.Extract(folder)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
            SetTradeDiscordStatus("The browser extension could not be saved: " & ex.Message)
            SilentMessageBox.Show(Me, "The browser extension could not be saved to:" & vbCrLf & folder & vbCrLf & vbCrLf & ex.Message, "Install browser extension", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try
        Try
            Clipboard.SetText(folder)
        Catch
            ' The path is also shown below.
        End Try
        Try
            Process.Start(New ProcessStartInfo("explorer.exe", """" & folder & """") With {.UseShellExecute = True})
        Catch
            ' Opening the folder is a convenience only.
        End Try
        SetTradeDiscordStatus($"Browser extension {DiscordCaptureExtensionPackage.Version()} saved to {folder} (path copied). Load it once in your browser's extensions page.")
        SilentMessageBox.Show(Me, $"The Kathana Discord Capture extension (version {DiscordCaptureExtensionPackage.Version()}) was saved to:" & vbCrLf & folder & vbCrLf & vbCrLf &
            "The folder path is copied to your clipboard and the folder is open in Explorer." & vbCrLf & vbCrLf &
            "Install it once:" & vbCrLf &
            "1. In Chrome or Edge, open chrome://extensions (or edge://extensions)." & vbCrLf &
            "2. Turn on Developer mode." & vbCrLf &
            "3. Click Load unpacked and choose that folder." & vbCrLf &
            "4. Pin Kathana Discord Capture to the toolbar." & vbCrLf & vbCrLf &
            "After a KathanaBot update, click Install extension again, then press Reload on the extension's card.",
            "Install browser extension", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub StopTradeBrowserCaptureFromDialog()
        StopTradeBrowserCapture()
        SetTradeDiscordStatus("Browser capture stopped. Existing posts and whispers are kept.")
    End Sub

    Private Function StartTradeBrowserCapture(sourceUrl As String, messageCount As Integer) As String
        If _tradeCaptureShutdown OrElse IsDisposed OrElse Disposing OrElse _tradeLoading OrElse _tradeAnalyzing OrElse _tradeRunning OrElse _tradeDiscordConfiguring Then
            Throw New InvalidOperationException("Finish or stop the current Trade action before starting browser capture.")
        End If
        Dim target = DiscordTradeService.ParseChannel(sourceUrl)
        If String.IsNullOrWhiteSpace(target.GuildId) OrElse String.IsNullOrWhiteSpace(target.ChannelLink) Then
            Throw New ArgumentException("Enter the full source Discord server/channel link.")
        End If
        StopTradeBrowserCapture()
        StopTradeDiscordImport()
        ClearTradeDiscordCache()
        _tradeCaptureSourceUrl = target.ChannelLink
        _tradeCaptureMessageCount = Math.Clamp(messageCount, 1, 10000)
        _tradeCaptureExpectedSourceText = _tradeSource.Text
        Dim generation = _tradeCaptureGeneration
        Dim cancellation As New CancellationTokenSource()
        Dim cancellationToken = cancellation.Token
        _tradeCaptureCancellation = cancellation
        Try
            Dim searchTerms = DiscordTradeService.ParseSearchTerms(_tradeCaptureFilter)
            Dim server As New DiscordBrowserCaptureServer(_tradeCaptureSourceUrl, _tradeCaptureMessageCount,
                Function(snapshot) ReceiveTradeBrowserCaptureAsync(snapshot, generation, cancellationToken), searchTerms)
            _tradeCaptureServer = server
            server.Paused = TradeBrowserCaptureIsBusy()
            server.Start()
            _tradeCapturePaused = server.Paused
            _tradeCaptureTimer.Start()
            UpdateTradeDiscordPolling()
            SetTradeDiscordStatus(If(_tradeCapturePaused,
                "Browser capture connected but paused while Trade or checked whispers await review. Copy setup into the extension; Analyze and Start stay separate.",
                $"Browser capture is ready for up to {_tradeCaptureMessageCount:N0} posts" &
                If(searchTerms.Count > 0, $", filtered by Discord's own search for {String.Join(", ", searchTerms)}", "") &
                ". Copy setup into the extension, then start it on the source channel. Stop or F12 disconnects."))
            Return server.ConnectionJson
        Catch
            StopTradeBrowserCapture()
            Throw New InvalidOperationException("The local browser capture connection could not start. Existing posts are kept.")
        End Try
    End Function

    Private Function TradeBrowserCaptureIsBusy() As Boolean
        Return _tradeLoading OrElse _tradeAnalyzing OrElse _tradeRunning OrElse _tradeDiscordConfiguring OrElse _tradeDiscordImporting OrElse HasPendingTradeDiscordRecipients()
    End Function

    Private Sub UpdateTradeBrowserCaptureState()
        If _tradeCaptureShutdown OrElse IsDisposed OrElse Disposing Then Return
        If _tradeCaptureButton IsNot Nothing AndAlso Not _tradeCaptureButton.IsDisposed Then
            _tradeCaptureButton.Enabled = Not _tradeLoading AndAlso Not _tradeAnalyzing AndAlso Not _tradeRunning AndAlso Not _tradeDiscordConfiguring
        End If
        If _tradeCaptureServer Is Nothing Then Return
        If Not _tradeCaptureServer.IsRunning Then
            Dim completed = _tradeCaptureCompleted
            StopTradeBrowserCapture()
            If Not completed AndAlso Not _tradeAnalyzing AndAlso Not _tradeRunning Then SetTradeDiscordStatus("Browser capture connection expired. Open Browser capture and start a new connection. Existing posts are kept.")
            Return
        End If
        If _tradeCaptureCompleted Then Return
        Dim paused = TradeBrowserCaptureIsBusy()
        _tradeCaptureServer.Paused = paused
        If paused <> _tradeCapturePaused Then
            _tradeCapturePaused = paused
            If Not _tradeAnalyzing AndAlso Not _tradeRunning Then
                SetTradeDiscordStatus(If(paused,
                    "Browser capture paused while checked whispers await review. Uncheck them to resume; Stop or F12 disconnects.",
                    "Browser capture resumed. Newly captured posts need Analyze and review before Start."))
            End If
        End If
    End Sub

    Private Async Function ReceiveTradeBrowserCaptureAsync(snapshot As DiscordTradeSnapshot, expectedGeneration As Long, cancellation As CancellationToken) As Task(Of Boolean)
        If cancellation.IsCancellationRequested OrElse _tradeCaptureShutdown OrElse IsDisposed OrElse Disposing OrElse Not IsHandleCreated Then Return False
        If Not InvokeRequired Then Return ApplyTradeBrowserCapture(snapshot, expectedGeneration)
        Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Using delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellation)
            delivery.CancelAfter(TimeSpan.FromSeconds(15))
            Dim deliveryToken = delivery.Token
            Using registration = deliveryToken.Register(Sub() completion.TrySetResult(False))
                Try
                    BeginInvoke(New Action(Sub()
                        If deliveryToken.IsCancellationRequested Then
                            completion.TrySetResult(False)
                            Return
                        End If
                        Try
                            completion.TrySetResult(ApplyTradeBrowserCapture(snapshot, expectedGeneration))
                        Catch
                            completion.TrySetResult(False)
                        End Try
                    End Sub))
                Catch ex As InvalidOperationException
                    completion.TrySetResult(False)
                End Try
                Return Await completion.Task.ConfigureAwait(False)
            End Using
        End Using
    End Function

    Private Function ApplyTradeBrowserCapture(snapshot As DiscordTradeSnapshot, expectedGeneration As Long) As Boolean
        If _tradeCaptureShutdown OrElse _tradeCaptureServer Is Nothing OrElse Not _tradeCaptureServer.IsRunning OrElse IsDisposed OrElse Disposing OrElse _tradeSource Is Nothing OrElse _tradeSource.IsDisposed Then Return False
        If expectedGeneration <> _tradeCaptureGeneration OrElse _tradeCaptureCompleted OrElse _tradeCaptureCancellation Is Nothing OrElse _tradeCaptureCancellation.IsCancellationRequested Then Return False
        If snapshot Is Nothing OrElse snapshot.RequestedMessageCount <> _tradeCaptureMessageCount OrElse
            snapshot.FetchedMessageCount < 0 OrElse snapshot.FetchedMessageCount > _tradeCaptureMessageCount OrElse
            snapshot.ImportedMessageCount < 0 OrElse snapshot.ImportedMessageCount > snapshot.FetchedMessageCount OrElse
            snapshot.Messages Is Nothing OrElse snapshot.Messages.Count > _tradeCaptureMessageCount Then Return False
        If TradeBrowserCaptureIsBusy() Then
            _tradeCaptureServer.Paused = True
            Return False
        End If
        If Not String.Equals(_tradeCaptureExpectedSourceText, _tradeSource.Text, StringComparison.Ordinal) Then
            StopTradeBrowserCapture()
            SetTradeDiscordStatus("Browser capture stopped because posts were edited. Your edits and whisper queue are kept.")
            Return False
        End If
        Dim target = DiscordTradeService.ParseChannel(_tradeCaptureSourceUrl)
        If Not String.Equals(snapshot.ChannelId, target.ChannelId, StringComparison.Ordinal) OrElse Not String.Equals(snapshot.GuildId, target.GuildId, StringComparison.Ordinal) Then Return False
        _tradeCaptureApplying = True
        Try
            If Not ApplyTradeSourceSnapshot(snapshot, _tradeCaptureMessageCount, True, "Browser capture") Then Return False
            _tradeCaptureExpectedSourceText = _tradeSource.Text
            _tradeCaptureCompleted = True
            If _tradeCaptureDialog IsNot Nothing AndAlso Not _tradeCaptureDialog.IsDisposed Then _tradeCaptureDialog.ShowCompletedCapture()
            If IsHandleCreated Then
                Try
                    SavePersistedListState(False)
                Catch
                    SetTradeDiscordStatus("Browser capture imported. Settings could not be saved; current posts remain available for Analyze and review.")
                End Try
            End If
            Return True
        Finally
            _tradeCaptureApplying = False
        End Try
    End Function

    Private Sub StopTradeBrowserCapture()
        _tradeCaptureGeneration += 1
        Dim server = _tradeCaptureServer
        Dim cancellation = _tradeCaptureCancellation
        _tradeCaptureServer = Nothing
        _tradeCaptureCancellation = Nothing
        _tradeCaptureTimer?.Stop()
        _tradeCapturePaused = False
        _tradeCaptureCompleted = False
        cancellation?.Cancel()
        server?.Dispose()
        cancellation?.Dispose()
        If _tradeCaptureDialog IsNot Nothing AndAlso Not _tradeCaptureDialog.IsDisposed Then _tradeCaptureDialog.UpdateConnection("")
        If Not _tradeCaptureShutdown Then UpdateTradeDiscordPolling()
    End Sub

    Private Sub ShutdownTradeBrowserCapture()
        _tradeCaptureShutdown = True
        StopTradeBrowserCapture()
        Dim timer = _tradeCaptureTimer
        _tradeCaptureTimer = Nothing
        timer?.Dispose()
    End Sub
End Class
