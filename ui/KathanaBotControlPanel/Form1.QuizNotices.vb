Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private chkQuizNoticesEnabled As CheckBox
    Private lblQuizNoticeStatus As Label
    Private ReadOnly _quizNoticeMonitor As New QuizNoticeMonitor()
    Private _quizNoticeCancellation As New CancellationTokenSource()
    Private _quizNoticeScanInProgress As Boolean
    Private _quizScannerStarted As Boolean
    Private _quizNoticeNextScanUtc As DateTime = DateTime.MinValue
    Private _quizNoticeLastErrorUtc As DateTime = DateTime.MinValue
    Private _quizNoticeLastDelivery As String = ""

    Private Function BuildQuizNoticeControls() As Control
        Dim card As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .RowCount = 3,
            .BackColor = ThemeCard, .Padding = New Padding(14), .Margin = New Padding(0, 8, 0, 12)}
        chkQuizNoticesEnabled = New CheckBox With {.Text = "Quiz / raffle notice alerts", .Checked = True, .AutoSize = True,
            .Dock = DockStyle.Top, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .ForeColor = ThemeTextPrimary}
        card.Controls.Add(chkQuizNoticesEnabled)
        card.Controls.Add(New Label With {.Text = "Always watches the selected Full game window for quiz or raffle keywords. Sends the event and any time mentioned to ntfy Channel (Global). No API key or calibration needed; works with the solver off.",
            .Dock = DockStyle.Top, .Height = 48, .ForeColor = ThemeTextSecondary, .Margin = New Padding(0, 8, 0, 4)})
        lblQuizNoticeStatus = New Label With {.Text = "On — waiting for a game window.", .Dock = DockStyle.Top, .Height = 24, .ForeColor = ThemeAccent, .AutoEllipsis = True}
        card.Controls.Add(lblQuizNoticeStatus)
        AddHandler chkQuizNoticesEnabled.CheckedChanged,
            Sub()
                If _quizSettingsLoading Then Return
                ResetQuizNoticeCancellation()
                UpdateQuizScanTimer()
                SavePersistedListState(False)
            End Sub
        Return card
    End Function

    Private Sub StartQuizNoticeMonitoring()
        _quizScannerStarted = True
        UpdateQuizScanTimer()
    End Sub

    Private Sub ResetQuizNoticeCancellation()
        _quizNoticeCancellation.Cancel()
        _quizNoticeCancellation.Dispose()
        _quizNoticeCancellation = New CancellationTokenSource()
        _quizNoticeNextScanUtc = DateTime.MinValue
    End Sub

    Private Sub UpdateQuizScanTimer()
        Dim noticesOn = chkQuizNoticesEnabled IsNot Nothing AndAlso chkQuizNoticesEnabled.Checked
        Dim solverOn = chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked
        Dim interval = If(nudQuizScanMs IsNot Nothing, CInt(nudQuizScanMs.Value), 350)
        _quizScanTimer.Interval = If(noticesOn, Math.Min(interval, 2000), interval)
        If _quizScannerStarted AndAlso (noticesOn OrElse solverOn) Then _quizScanTimer.Start() Else _quizScanTimer.Stop()
        If lblQuizNoticeStatus IsNot Nothing Then lblQuizNoticeStatus.Text = If(noticesOn, "On — watching for quiz / raffle keywords.", "Notice alerts are off.")
    End Sub

    Private Async Function RunQuizNoticeScanAsync() As Task
        If Not _quizScannerStarted OrElse _quizSettingsLoading OrElse _quizNoticeScanInProgress OrElse
            chkQuizNoticesEnabled Is Nothing OrElse Not chkQuizNoticesEnabled.Checked OrElse DateTime.UtcNow < _quizNoticeNextScanUtc Then Return
        _quizNoticeNextScanUtc = DateTime.UtcNow.AddSeconds(2)
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse Not IsUsableQuizWindow(selected.MainWindowHandle) Then
            lblQuizNoticeStatus.Text = "On — waiting for a visible, non-minimized Full game window."
            Return
        End If
        Dim hwnd = selected.MainWindowHandle
        Dim processId = selected.ProcessId
        Dim cancellation = _quizNoticeCancellation.Token
        _quizNoticeScanInProgress = True
        Try
            ' Share the existing game capture and local Windows OCR pipeline.
            ' Scan the whole client: announcement/chat banners can sit outside
            ' the quiz-answer calibration. Never switch modes or send game input.
            Dim lines = Await Task.Run(Function()
                                           cancellation.ThrowIfCancellationRequested()
                                           Using frame = BotEngine.CaptureClient(hwnd)
                                               If frame Is Nothing Then Return Nothing
                                               Return OcrReader.ReadScreenTextRegions(frame).Select(Function(line) line.Text).ToArray()
                                           End Using
                                       End Function, cancellation)
            cancellation.ThrowIfCancellationRequested()
            If IsDisposed OrElse Not _quizScannerStarted Then Return
            Dim current = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If current Is Nothing OrElse current.ProcessId <> processId OrElse current.MainWindowHandle <> hwnd Then Return
            If lines Is Nothing Then
                lblQuizNoticeStatus.Text = "On — waiting for a readable game image."
                Return
            End If
            Await _quizNoticeMonitor.ProcessAsync(processId.ToString() & ":" & hwnd.ToInt64().ToString(), lines,
                GetNtfyTopicName(), DateTime.UtcNow, AddressOf SendQuizNoticeAsync, cancellation)
            cancellation.ThrowIfCancellationRequested()
            lblQuizNoticeStatus.Text = If(_quizNoticeLastDelivery.Length > 0, "On — last alert: " & _quizNoticeLastDelivery, "On — watching for quiz / raffle keywords.")
        Catch ex As OperationCanceledException
        Catch ex As Exception
            If Not IsDisposed AndAlso _quizScannerStarted Then
                lblQuizNoticeStatus.Text = "Notice scan will retry: " & ex.Message
                If DateTime.UtcNow - _quizNoticeLastErrorUtc >= TimeSpan.FromMinutes(1) Then
                    _quizNoticeLastErrorUtc = DateTime.UtcNow
                    AppendLog("Quiz/raffle notice scan: " & ex.Message)
                End If
            End If
        Finally
            _quizNoticeScanInProgress = False
        End Try
    End Function

    Private Async Function SendQuizNoticeAsync(notice As QuizRaffleNotice, topic As String, cancellation As CancellationToken) As Task(Of Boolean)
        cancellation.ThrowIfCancellationRequested()
        Dim delivered = Await SendPhoneNotificationToTopicAsync(notice.Title, notice.Body, topic,
            priority:="high", tags:="bell,gamepad", forceNtfy:=True, cancellationToken:=cancellation)
        If delivered Then
            _quizNoticeLastDelivery = notice.Kind & If(notice.Timing.Length > 0, " — " & notice.Timing, " — no time mentioned") & " (" & DateTime.Now.ToString("HH:mm") & ")"
            AppendLog("Quiz/raffle notice sent to ntfy Channel (Global): " & notice.Body)
        End If
        Return delivered
    End Function
End Class
