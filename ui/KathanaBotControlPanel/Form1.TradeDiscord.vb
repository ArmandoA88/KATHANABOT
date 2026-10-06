Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private Const DefaultTradeDiscordChannelUrl As String = "https://discord.com/channels/1483326612538654813/1556796091855278112"
    Private _tradeDiscordConfigure As Button
    Private _tradeDiscordImport As Button
    Private _tradeDiscordAuto As CheckBox
    Private _tradeDiscordCountLabel As Label
    Private _tradeDiscordTimer As System.Windows.Forms.Timer
    Private _tradeDiscordStopTimer As System.Windows.Forms.Timer
    Private _tradeDiscordCancellation As CancellationTokenSource
    Private _tradeDiscordGeneration As Long
    Private _tradeDiscordChannelUrl As String
    Private _tradeDiscordEncryptedBotToken As String
    Private _tradeDiscordImportMessageCount As Integer
    Private _tradeDiscordActiveSelectedCount As Integer
    Private _tradeDiscordActiveRequestCount As Integer
    Private _tradeDiscordProgress As IProgress(Of DiscordTradeImportProgress)
    Private _tradeDiscordProgressPhase As Integer
    Private _tradeDiscordProgressVisible As Boolean
    Private _tradeDiscordSnapshot As DiscordTradeSnapshot
    Private _tradeDiscordSnapshotChannelUrl As String
    Private _tradeDiscordSnapshotMessageCount As Integer
    Private _tradeDiscordSnapshotEncryptedToken As String
    Private _tradeDiscordSnapshotSourceText As String
    Private _tradeDiscordConfigurationReady As Boolean
    Private _tradeDiscordImporting As Boolean
    Private _tradeDiscordConfiguring As Boolean
    Private _tradeDiscordShutdown As Boolean
    Private _tradeDiscordRequestIsAutomatic As Boolean
    Private _tradeDiscordAutoPausedForReview As Boolean
    Private _tradeDiscordNextAttemptUtc As DateTime
    Private _tradeDiscordFetch As Func(Of String, String, CancellationToken, Task(Of DiscordTradeSnapshot))

    Private Function InitializeTradeDiscordControls() As Control
        ' These are initialized here because layout tests build the tab without running the Form constructor.
        _tradeDiscordChannelUrl = DefaultTradeDiscordChannelUrl
        _tradeDiscordEncryptedBotToken = ""
        _tradeDiscordImportMessageCount = 10000
        _tradeDiscordActiveSelectedCount = 10000
        _tradeDiscordActiveRequestCount = 10000
        ClearTradeDiscordCache()
        ' The retired reader never starts requests or restores saved automatic polling.
        _tradeDiscordShutdown = True
        _tradeDiscordConfigurationReady = False
        _tradeDiscordNextAttemptUtc = DateTime.MinValue
        Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .Margin = New Padding(0), .WrapContents = True}
        toolbar.Controls.Add(InitializeTradeBrowserCaptureControls())
        Dim tip As New ToolTip()
        tip.SetToolTip(_tradeCaptureButton, "Connect the local extension to scroll chat and capture posts loaded by Discord's web UI. No bot or personal-account token is extracted.")
        AddHandler toolbar.Disposed,
            Sub()
                ShutdownTradeBrowserCapture()
                ShutdownTradeDiscordImport()
                tip.Dispose()
            End Sub
        Return toolbar
    End Function

    Private Sub ConfigureTradeDiscord()
        If _tradeDiscordShutdown Then Return
        If _tradeAnalyzing OrElse _tradeRunning OrElse _tradeLoading Then
            SetTradeDiscordStatus("Finish or stop the current Trade action before changing Discord settings.")
            Return
        End If
        StopTradeBrowserCapture()
        StopTradeDiscordImport(False)
        Dim previousChannel = _tradeDiscordChannelUrl
        Dim previousEncryptedToken = _tradeDiscordEncryptedBotToken
        Dim previousCount = _tradeDiscordImportMessageCount
        Dim previousReady = _tradeDiscordConfigurationReady
        Dim previousAuto = _tradeDiscordAuto.Checked
        Dim currentToken As String = ""
        Try
            currentToken = QuizSecretStore.Unprotect(_tradeDiscordEncryptedBotToken)
        Catch
            ' Corrupt or foreign-account encrypted data is treated as an unconfigured token.
        End Try
        _tradeDiscordConfiguring = True
        Try
            Using dialog As New DiscordTradeConnectionDialog(_tradeDiscordChannelUrl, currentToken, _tradeDiscordImportMessageCount)
                currentToken = ""
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    Dim encrypted = QuizSecretStore.Protect(dialog.BotToken)
                    Dim target = DiscordTradeService.ParseChannel(dialog.ChannelUrl)
                    _tradeDiscordChannelUrl = If(target.ChannelLink.Length > 0, target.ChannelLink, target.ChannelId)
                    _tradeDiscordEncryptedBotToken = encrypted
                    _tradeDiscordImportMessageCount = Math.Clamp(dialog.ImportMessageCount, 1, 10000)
                    _tradeDiscordConfigurationReady = encrypted.Length > 0
                    ClearTradeDiscordCache()
                    Dim wasLoading = _tradeLoading
                    _tradeLoading = True
                    Try
                        _tradeDiscordAuto.Checked = False
                    Finally
                        _tradeLoading = wasLoading
                    End Try
                    SetTradeDiscordStatus(If(_tradeDiscordConfigurationReady,
                        "Discord connection saved. Click Import latest; enable Auto separately if desired. Follow imports published posts only.",
                        "Reader bot token removed. Paste posts or configure a reader bot to import."))
                    SavePersistedListState(True)
                Catch
                    _tradeDiscordChannelUrl = previousChannel
                    _tradeDiscordEncryptedBotToken = previousEncryptedToken
                    _tradeDiscordImportMessageCount = previousCount
                    _tradeDiscordConfigurationReady = previousReady
                    Dim wasLoading = _tradeLoading
                    _tradeLoading = True
                    Try
                        _tradeDiscordAuto.Checked = previousAuto
                    Finally
                        _tradeLoading = wasLoading
                    End Try
                    SetTradeDiscordStatus("Discord settings could not be saved securely. The previous connection is kept.")
                End Try
            End Using
        Finally
            _tradeDiscordConfiguring = False
            UpdateTradeDiscordPolling()
        End Try
    End Sub

    Private Sub ApplyTradeDiscordSettings(settings As TradeSettings)
        StopTradeBrowserCapture()
        ClearTradeDiscordCache()
        _tradeDiscordChannelUrl = If(String.IsNullOrWhiteSpace(settings.DiscordChannelUrl), DefaultTradeDiscordChannelUrl, settings.DiscordChannelUrl)
        _tradeDiscordEncryptedBotToken = If(settings.EncryptedDiscordBotToken, "")
        _tradeDiscordImportMessageCount = Math.Clamp(settings.DiscordImportMessageCount, 1, 10000)
        _tradeDiscordShutdown = True
        _tradeDiscordConfigurationReady = False
        If _tradeDiscordAuto IsNot Nothing Then _tradeDiscordAuto.Checked = False
        _tradeDiscordAutoPausedForReview = False
    End Sub

    Private Sub UpdateTradeDiscordPolling()
        If _tradeDiscordShutdown OrElse _tradeDiscordAuto Is Nothing OrElse _tradeDiscordAuto.IsDisposed OrElse IsDisposed OrElse Disposing Then Return
        _tradeDiscordConfigure.Enabled = Not _tradeRunning AndAlso Not _tradeAnalyzing AndAlso Not _tradeDiscordConfiguring
        _tradeDiscordImport.Enabled = _tradeCaptureServer Is Nothing AndAlso _tradeDiscordConfigurationReady AndAlso Not _tradeDiscordImporting AndAlso Not _tradeRunning AndAlso Not _tradeAnalyzing AndAlso Not _tradeDiscordConfiguring
        _tradeDiscordAuto.Enabled = _tradeCaptureServer Is Nothing AndAlso _tradeDiscordConfigurationReady
        If _tradeDiscordCountLabel IsNot Nothing AndAlso Not _tradeDiscordCountLabel.IsDisposed Then _tradeDiscordCountLabel.Text = $"Posts: {_tradeDiscordImportMessageCount:N0}"
        Dim polling = _tradeCaptureServer Is Nothing AndAlso _tradeDiscordAuto.Checked AndAlso _tradeDiscordConfigurationReady AndAlso Not _tradeLoading AndAlso Not _tradeRunning AndAlso Not _tradeAnalyzing AndAlso Not _tradeDiscordConfiguring
        _tradeDiscordTimer.Enabled = polling
        _tradeDiscordStopTimer.Enabled = polling OrElse _tradeDiscordImporting
    End Sub

    Private Sub StopTradeDiscordImport(Optional disableAutoImport As Boolean = True)
        _tradeDiscordGeneration += 1
        _tradeDiscordCancellation?.Cancel()
        _tradeDiscordTimer?.Stop()
        _tradeDiscordStopTimer?.Stop()
        _tradeDiscordAutoPausedForReview = False
        If disableAutoImport AndAlso _tradeDiscordAuto IsNot Nothing AndAlso _tradeDiscordAuto.Checked Then _tradeDiscordAuto.Checked = False
    End Sub

    Private Sub ShutdownTradeDiscordImport()
        _tradeDiscordShutdown = True
        StopTradeDiscordImport(False)
        ClearTradeDiscordCache()
        Dim pollingTimer = _tradeDiscordTimer
        Dim stopTimer = _tradeDiscordStopTimer
        _tradeDiscordTimer = Nothing
        _tradeDiscordStopTimer = Nothing
        pollingTimer?.Dispose()
        stopTimer?.Dispose()
    End Sub

    Private Function HasPendingTradeDiscordRecipients() As Boolean
        If _tradeGrid Is Nothing Then Return False
        For Each row As DataGridViewRow In _tradeGrid.Rows
            If Not row.IsNewRow AndAlso (Object.Equals(row.Cells("Send").Value, True) OrElse Object.Equals(row.Cells("Send").EditedFormattedValue, True)) Then Return True
        Next
        Return False
    End Function

    Private Async Function ImportTradeDiscordAsync(manual As Boolean) As Task
        If _tradeDiscordShutdown OrElse IsDisposed OrElse Disposing OrElse _tradeSource Is Nothing OrElse _tradeSource.IsDisposed Then Return
        If _tradeCaptureServer IsNot Nothing Then
            If manual Then SetTradeDiscordStatus("Stop browser capture before importing with the reader bot. Existing posts are kept.")
            Return
        End If
        ' Timer-driven imports require the real UI. Reflection-only fixtures never make a request.
        If Not manual AndAlso (Not IsHandleCreated OrElse Not _tradeSource.IsHandleCreated OrElse _tradeDiscordAuto Is Nothing OrElse Not _tradeDiscordAuto.Checked) Then Return
        If _tradeAnalyzing OrElse _tradeRunning OrElse _tradeLoading OrElse _tradeDiscordConfiguring Then
            If manual Then SetTradeDiscordStatus("Discord import is paused during analysis or whisper sending. Finish or stop that action first.")
            Return
        End If
        If _tradeDiscordImporting Then Return
        If Not manual AndAlso HasPendingTradeDiscordRecipients() Then
            If Not _tradeDiscordAutoPausedForReview Then SetTradeDiscordStatus("Auto import paused while checked whispers await review. Uncheck them, or use Import latest to replace posts.")
            _tradeDiscordAutoPausedForReview = True
            Return
        End If
        _tradeDiscordAutoPausedForReview = False
        If DateTime.UtcNow < _tradeDiscordNextAttemptUtc Then
            If manual Then SetTradeDiscordStatus($"Discord rate limit: wait {Math.Ceiling((_tradeDiscordNextAttemptUtc - DateTime.UtcNow).TotalSeconds):0} seconds before importing again. Existing posts are kept.")
            Return
        End If
        If Not _tradeDiscordConfigurationReady Then
            If manual Then SetTradeDiscordStatus("Configure Discord with your receiving channel and reader bot token first.")
            Return
        End If
        Dim token As String = ""
        Try
            token = QuizSecretStore.Unprotect(_tradeDiscordEncryptedBotToken)
        Catch
        End Try
        If String.IsNullOrWhiteSpace(token) Then
            _tradeDiscordConfigurationReady = False
            StopTradeDiscordImport()
            UpdateTradeDiscordPolling()
            SetTradeDiscordStatus("The reader bot token could not be opened for this Windows account. Configure Discord again; existing posts are kept.")
            Return
        End If
        Dim generation = _tradeDiscordGeneration
        Dim channel = _tradeDiscordChannelUrl
        Dim messageCount = _tradeDiscordImportMessageCount
        Dim sourceText = _tradeSource.Text
        Dim previous = If(Not manual AndAlso HasTradeDiscordSnapshot(), _tradeDiscordSnapshot, Nothing)
        Dim cancellation As New CancellationTokenSource()
        cancellation.CancelAfter(TimeSpan.FromMinutes(20))
        _tradeDiscordCancellation = cancellation
        _tradeDiscordActiveSelectedCount = messageCount
        _tradeDiscordImporting = True
        _tradeDiscordRequestIsAutomatic = Not manual
        UpdateTradeDiscordPolling()
        Try
            Dim requestCount = If(previous Is Nothing, messageCount, Math.Min(100, messageCount))
            Dim snapshot = Await RequestTradeDiscordSnapshotAsync(channel, token, cancellation.Token, messageCount, requestCount, generation, manual OrElse requestCount > 100)
            cancellation.Token.ThrowIfCancellationRequested()
            If snapshot IsNot Nothing AndAlso Double.IsFinite(snapshot.SuggestedRefreshDelaySeconds) AndAlso snapshot.SuggestedRefreshDelaySeconds > 0 Then DeferTradeDiscordImport(snapshot.SuggestedRefreshDelaySeconds)
            If previous IsNot Nothing AndAlso snapshot IsNot Nothing Then
                Dim recentIds = New HashSet(Of String)(snapshot.Messages.Select(Function(message) message.Id), StringComparer.Ordinal)
                Dim overlaps = previous.Messages.Any(Function(message) recentIds.Contains(message.Id))
                If Not snapshot.HistoryExhausted AndAlso Not overlaps AndAlso requestCount < messageCount Then
                    If Not IsCurrentTradeDiscordRequest(generation, channel, messageCount, sourceText) Then Return
                    Dim delay = _tradeDiscordNextAttemptUtc - DateTime.UtcNow
                    While delay > TimeSpan.Zero
                        SetTradeDiscordStatus("Waiting for Discord's retry delay before reading the full history... Stop or F12 cancels.")
                        Await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(delay.TotalMilliseconds, TimeSpan.FromMinutes(20).TotalMilliseconds))), cancellation.Token)
                        delay = _tradeDiscordNextAttemptUtc - DateTime.UtcNow
                    End While
                    cancellation.Token.ThrowIfCancellationRequested()
                    If Not IsCurrentTradeDiscordRequest(generation, channel, messageCount, sourceText) Then Return
                    snapshot = Await RequestTradeDiscordSnapshotAsync(channel, token, cancellation.Token, messageCount, messageCount, generation, True)
                Else
                    snapshot = DiscordTradeService.MergeRecentSnapshot(previous, snapshot, messageCount)
                End If
            End If
            token = ""
            cancellation.Token.ThrowIfCancellationRequested()
            If snapshot IsNot Nothing AndAlso Double.IsFinite(snapshot.SuggestedRefreshDelaySeconds) AndAlso snapshot.SuggestedRefreshDelaySeconds > 0 Then DeferTradeDiscordImport(snapshot.SuggestedRefreshDelaySeconds)
            If messageCount <> _tradeDiscordImportMessageCount Then Return
            If ApplyDiscordTradeImport(snapshot, generation, channel, sourceText) Then
                CacheTradeDiscordSnapshot(snapshot)
                If IsHandleCreated Then SavePersistedListState(False)
            End If
        Catch ex As OperationCanceledException
            If generation = _tradeDiscordGeneration AndAlso Not IsDisposed AndAlso Not Disposing Then SetTradeDiscordStatus("Discord import cancelled or timed out. Existing posts and whispers are kept.")
        Catch ex As DiscordTradeException
            If generation <> _tradeDiscordGeneration OrElse IsDisposed OrElse Disposing Then Return
            If ex.FailureKind = "rate-limited" Then
                Dim seconds = If(ex.RetryAfterSeconds, 30.0)
                If Double.IsNaN(seconds) OrElse seconds < 0 Then seconds = 30
                DeferTradeDiscordImport(seconds)
                SetTradeDiscordStatus("Discord rate limit reached. Import waits for Discord's retry delay; existing posts and whispers are kept.")
            Else
                If ex.ShouldStopPolling Then StopTradeDiscordImport()
                SetTradeDiscordStatus(TradeDiscordFailureStatus(ex.FailureKind))
            End If
        Catch
            If generation = _tradeDiscordGeneration AndAlso Not IsDisposed AndAlso Not Disposing Then SetTradeDiscordStatus("Discord import failed. Check the connection and try again; existing posts and whispers are kept.")
        Finally
            token = ""
            If Object.ReferenceEquals(_tradeDiscordCancellation, cancellation) Then _tradeDiscordCancellation = Nothing
            _tradeDiscordImporting = False
            _tradeDiscordRequestIsAutomatic = False
            _tradeDiscordProgress = Nothing
            _tradeDiscordProgressVisible = False
            cancellation.Dispose()
            If Not IsDisposed AndAlso Not Disposing Then UpdateTradeDiscordPolling()
        End Try
    End Function

    Private Async Function RequestTradeDiscordSnapshotAsync(channel As String, token As String, cancellation As CancellationToken, selectedCount As Integer,
                                                           requestedCount As Integer, generation As Long, showProgress As Boolean) As Task(Of DiscordTradeSnapshot)
        cancellation.ThrowIfCancellationRequested()
        _tradeDiscordActiveRequestCount = requestedCount
        _tradeDiscordProgressPhase += 1
        Dim phase = _tradeDiscordProgressPhase
        _tradeDiscordProgressVisible = showProgress
        _tradeDiscordProgress = New Progress(Of DiscordTradeImportProgress)(
            Sub(progress)
                If progress Is Nothing OrElse _tradeDiscordShutdown OrElse Not _tradeDiscordImporting OrElse cancellation.IsCancellationRequested OrElse
                    generation <> _tradeDiscordGeneration OrElse phase <> _tradeDiscordProgressPhase OrElse selectedCount <> _tradeDiscordImportMessageCount OrElse
                    Not String.Equals(channel, _tradeDiscordChannelUrl, StringComparison.Ordinal) OrElse IsDisposed OrElse Disposing Then Return
                If showProgress Then SetTradeDiscordStatus($"Reading {progress.FetchedMessageCount:N0} of {requestedCount:N0} posts... Stop or F12 cancels.")
            End Sub)
        If showProgress Then SetTradeDiscordStatus($"Reading 0 of {requestedCount:N0} posts... Stop or F12 cancels.")
        Return Await _tradeDiscordFetch(channel, token, cancellation)
    End Function

    Private Sub ClearTradeDiscordCache()
        _tradeDiscordSnapshot = Nothing
        _tradeDiscordSnapshotChannelUrl = ""
        _tradeDiscordSnapshotMessageCount = 0
        _tradeDiscordSnapshotEncryptedToken = ""
        _tradeDiscordSnapshotSourceText = ""
    End Sub

    Private Function IsCurrentTradeDiscordRequest(generation As Long, channel As String, messageCount As Integer, sourceText As String) As Boolean
        Return Not _tradeDiscordShutdown AndAlso Not IsDisposed AndAlso Not Disposing AndAlso Not _tradeLoading AndAlso Not _tradeAnalyzing AndAlso
            Not _tradeRunning AndAlso Not _tradeDiscordConfiguring AndAlso generation = _tradeDiscordGeneration AndAlso messageCount = _tradeDiscordImportMessageCount AndAlso
            String.Equals(channel, _tradeDiscordChannelUrl, StringComparison.Ordinal) AndAlso String.Equals(sourceText, _tradeSource.Text, StringComparison.Ordinal)
    End Function

    Private Function HasTradeDiscordSnapshot() As Boolean
        Return _tradeDiscordSnapshot IsNot Nothing AndAlso _tradeDiscordSnapshotMessageCount = _tradeDiscordImportMessageCount AndAlso
            String.Equals(_tradeDiscordSnapshotChannelUrl, _tradeDiscordChannelUrl, StringComparison.Ordinal) AndAlso
            String.Equals(_tradeDiscordSnapshotEncryptedToken, _tradeDiscordEncryptedBotToken, StringComparison.Ordinal) AndAlso
            String.Equals(_tradeDiscordSnapshotSourceText, _tradeSource.Text, StringComparison.Ordinal)
    End Function

    Private Sub CacheTradeDiscordSnapshot(snapshot As DiscordTradeSnapshot)
        _tradeDiscordSnapshot = snapshot
        _tradeDiscordSnapshotChannelUrl = _tradeDiscordChannelUrl
        _tradeDiscordSnapshotMessageCount = _tradeDiscordImportMessageCount
        _tradeDiscordSnapshotEncryptedToken = _tradeDiscordEncryptedBotToken
        _tradeDiscordSnapshotSourceText = _tradeSource.Text
    End Sub

    Private Function ApplyDiscordTradeImport(snapshot As DiscordTradeSnapshot, expectedGeneration As Long, expectedChannelUrl As String, expectedSourceText As String) As Boolean
        If _tradeDiscordShutdown OrElse IsDisposed OrElse Disposing OrElse _tradeSource Is Nothing OrElse _tradeSource.IsDisposed OrElse _tradeLoading OrElse _tradeAnalyzing OrElse _tradeRunning OrElse _tradeDiscordConfiguring Then Return False
        If _tradeCaptureServer IsNot Nothing Then Return False
        If expectedGeneration <> _tradeDiscordGeneration OrElse Not String.Equals(expectedChannelUrl, _tradeDiscordChannelUrl, StringComparison.Ordinal) Then Return False
        If _tradeDiscordImporting AndAlso _tradeDiscordActiveSelectedCount <> _tradeDiscordImportMessageCount Then Return False
        If _tradeDiscordCancellation IsNot Nothing AndAlso _tradeDiscordCancellation.IsCancellationRequested Then Return False
        If Not String.Equals(expectedSourceText, _tradeSource.Text, StringComparison.Ordinal) Then
            SetTradeDiscordStatus("Posts were edited during Discord import. Your edits and queue are kept; import again when ready.")
            Return False
        End If
        If _tradeDiscordRequestIsAutomatic AndAlso HasPendingTradeDiscordRecipients() Then Return False
        Return ApplyTradeSourceSnapshot(snapshot, _tradeDiscordImportMessageCount, Not _tradeDiscordRequestIsAutomatic OrElse _tradeDiscordProgressVisible, "Discord")
    End Function

    Private Function ApplyTradeSourceSnapshot(snapshot As DiscordTradeSnapshot, selectedCount As Integer, reportUnchanged As Boolean, sourceName As String) As Boolean
        If snapshot Is Nothing OrElse String.IsNullOrWhiteSpace(snapshot.ImportedText) OrElse snapshot.ImportedMessageCount = 0 Then
            SetTradeDiscordStatus(If(sourceName = "Discord",
                "The receiving channel has no readable posts yet. Await a source-published post and check the bot's Read Message History permission. Existing posts and whispers are kept.",
                "Browser capture has no readable posts yet. Existing posts and whispers are kept."))
            Return False
        End If
        Dim sourceText = NormalizeTradeSourceText(snapshot.ImportedText)
        If sourceText.Length > _tradeSource.MaxLength Then
            SetTradeDiscordStatus(sourceName & " posts exceed the import text limit. Existing posts and whispers are kept.")
            Return False
        End If
        Dim changed = Not String.Equals(_tradeSource.Text, sourceText, StringComparison.Ordinal)
        If changed Then _tradeSource.Text = sourceText
        Dim readable = If(snapshot.Messages, New List(Of DiscordTradeMessage)()).Where(Function(post) post IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(post.BodyText)).ToList()
        _tradeAnalysisSourcePosts = If(readable.Count = snapshot.ImportedMessageCount, readable, Nothing)
        _tradeAnalysisSourceText = If(_tradeAnalysisSourcePosts IsNot Nothing, _tradeSource.Text, Nothing)
        If changed Then
            Dim summary = If(String.IsNullOrWhiteSpace(snapshot.Status),
                $"Imported {snapshot.ImportedMessageCount:N0} of {snapshot.FetchedMessageCount:N0} messages; selected limit {selectedCount:N0}." &
                If(snapshot.HistoryExhausted, " Reached the end of available history.", "") & If(snapshot.TextWasLimited, " Text limit omitted older posts.", ""), snapshot.Status)
            SetTradeDiscordStatus(summary & " Click Analyze to process all posts in batches, then review exact game names before Start.")
        ElseIf reportUnchanged Then
            SetTradeDiscordStatus(sourceName & " posts are unchanged. Your analyzed items and whisper queue are kept.")
        End If
        Return True
    End Function

    Private Sub DeferTradeDiscordImport(seconds As Double)
        Dim deadline As DateTime
        Try
            deadline = DateTime.UtcNow.AddSeconds(seconds)
        Catch
            deadline = DateTime.MaxValue
        End Try
        If deadline > _tradeDiscordNextAttemptUtc Then _tradeDiscordNextAttemptUtc = deadline
    End Sub

    Private Shared Function TradeDiscordFailureStatus(kind As String) As String
        Select Case kind
            Case "unauthorized"
                Return "Discord rejected the reader bot token. Auto import stopped; configure the bot token again. Existing posts are kept."
            Case "forbidden"
                Return "The reader bot cannot read this channel. Install it in your receiving server with View Channel and Read Message History. Auto import stopped; existing posts are kept."
            Case "no-readable-content"
                Return "Discord withheld post content. Enable Message Content access for the reader bot, with approval if required. Auto import stopped; existing posts are kept."
            Case "invalid-channel", "channel-unavailable"
                Return "The receiving Discord channel is unavailable or unsupported. Check its channel link and reader bot access; existing posts are kept."
            Case Else
                Return "Discord did not return a usable response. Check the connection and try again; existing posts and whispers are kept."
        End Select
    End Function

    Private Sub SetTradeDiscordStatus(message As String)
        If _tradeStatus IsNot Nothing AndAlso Not IsDisposed AndAlso Not Disposing Then _tradeStatus.Text = message
    End Sub
End Class
