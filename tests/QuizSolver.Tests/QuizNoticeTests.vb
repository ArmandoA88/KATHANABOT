Imports System.Drawing
Imports System.Reflection
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Module QuizNoticeTests
    Public Sub Run()
        Detection()
        Persistence()
        NoticeUiAndTimer()
        Delivery().GetAwaiter().GetResult()
        LocalOcr()
        Console.WriteLine("PASS quiz/raffle keyword matching, time extraction, saved default-on toggle, global-topic delivery, deduplication, retry, cancellation and local OCR (no live notifications).")
    End Sub

    Private Sub NoticeUiAndTimer()
        Dim failure As Exception = Nothing
        Dim worker As New Thread(Sub()
                                     Try
                                         Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
                                         Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
                                         GetType(Form1).GetField("_quizSettingsLoading", flags).SetValue(owner, True)
                                         Using timer As New System.Windows.Forms.Timer(), solver As New CheckBox(), interval As New NumericUpDown With {.Maximum = 10000, .Value = 350}
                                             GetType(Form1).GetField("_quizScanTimer", flags).SetValue(owner, timer)
                                             GetType(Form1).GetField("chkQuizSolverEnabled", flags).SetValue(owner, solver)
                                             GetType(Form1).GetField("nudQuizScanMs", flags).SetValue(owner, interval)
                                             GetType(Form1).GetField("_quizScannerStarted", flags).SetValue(owner, True)
                                             Using card = DirectCast(GetType(Form1).GetMethod("BuildQuizNoticeControls", flags).Invoke(owner, Nothing), Control)
                                                 Dim toggle = DirectCast(GetType(Form1).GetField("chkQuizNoticesEnabled", flags).GetValue(owner), CheckBox)
                                                 Dim updateTimer = GetType(Form1).GetMethod("UpdateQuizScanTimer", flags)
                                                 Check(toggle.Checked, "notice toggle did not default on")
                                                 updateTimer.Invoke(owner, Nothing)
                                                 Check(timer.Enabled AndAlso Not solver.Checked, "monitoring depends on the solver")
                                                 toggle.Checked = False : updateTimer.Invoke(owner, Nothing)
                                                 Check(Not timer.Enabled, "disabled monitoring kept the scanner running")
                                                 solver.Checked = True : updateTimer.Invoke(owner, Nothing)
                                                 Check(timer.Enabled, "disabling alerts stopped the enabled solver")
                                                 solver.Checked = False : toggle.Checked = True : updateTimer.Invoke(owner, Nothing)
                                                 interval.Value = 10000 : updateTimer.Invoke(owner, Nothing)
                                                 Check(timer.Interval <= 2000, "slow solver interval delayed notices")
                                                 timer.Stop()
                                                 Using host As New Form With {.ClientSize = New Size(740, 220), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}
                                                     host.Controls.Add(card) : host.Show() : Application.DoEvents() : card.PerformLayout()
                                                     Check(card.Height <= host.ClientSize.Height AndAlso toggle.Right <= card.ClientSize.Width, "notice controls clipped")
                                                     Using bitmap As New Bitmap(740, 220)
                                                         host.DrawToBitmap(bitmap, New Rectangle(0, 0, 740, 220))
                                                         bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "quiz-notice-controls.png"))
                                                     End Using
                                                 End Using
                                             End Using
                                         End Using
                                     Catch ex As Exception
                                         failure = ex
                                     End Try
                                 End Sub)
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start() : worker.Join()
        If failure IsNot Nothing Then Throw failure
    End Sub

    Private Sub Check(value As Boolean, message As String)
        If Not value Then Throw New Exception("Quiz notices: " & message)
    End Sub

    Private Sub Expect(line As String, kind As String, timing As String)
        Dim notices = QuizNoticeDetector.Detect({line})
        Check(notices.Count = 1 AndAlso notices(0).Kind = kind AndAlso notices(0).Timing = timing,
            "bad match for " & line & " -> " & JsonSerializer.Serialize(notices))
    End Sub

    Private Sub Detection()
        Check(QuizNoticeDetector.Detect(Nothing).Count = 0, "empty text")
        Check(QuizNoticeDetector.Detect({"Skill cooldown 00:30", "Damage 1234", "Quizmaster traded rafflecoins"}).Count = 0, "keyword boundaries")
        Expect("[GM] Quiz starts in 5 minutes!", "Quiz", "starts in 5 minutes")
        Expect("RAFFLE begins in 30 seconds", "Raffle", "begins in 30 seconds")
        Expect("Quizzes in ten minutes", "Quiz", "in ten minutes")
        Expect("Quiz in thirty-five minutes", "Quiz", "in thirty-five minutes")
        Expect("Raffle in 5m30s", "Raffle", "in 5m30s")
        Expect("Quizes in 2 mins", "Quiz", "in 2 mins")
        Expect("Raffles after 1 hour and 30 minutes", "Raffle", "after 1 hour and 30 minutes")
        Expect("Quiz in 01:30", "Quiz", "in 01:30")
        Expect("Raffle starts at 20:30", "Raffle", "starts at 20:30")
        Expect("Quiz at 8 PM", "Quiz", "at 8 PM")
        Expect("Quiz: 45 seconds left", "Quiz", "45 seconds left")
        Expect("Quiz prize: 500 rupiah", "Quiz", "")
        Expect("Quiz notice. Buff duration 5 minutes", "Quiz", "")
        Dim both = QuizNoticeDetector.Detect({"Quiz in 5 minutes and raffle in 30 seconds"})
        Check(both.Count = 2 AndAlso both(0).Timing = "in 5 minutes" AndAlso both(1).Timing = "in 30 seconds", "times mixed between events")
        Dim wrapped = QuizNoticeDetector.Detect({"[GM] Raffle", "starts in 2 minutes", "HP 1000"})
        Check(wrapped.Single().Timing = "starts in 2 minutes", "wrapped notice")
        Dim unrelated = QuizNoticeDetector.Detect({"Quiz notification", "Buff time left 10 minutes", "Raffle in 1 minute"})
        Check(unrelated.First().Timing = "" AndAlso unrelated.Last().Timing = "in 1 minute", "unrelated timer copied")
        Check(unrelated.First().Body.Contains("No time mentioned") AndAlso unrelated.Last().Title = "Raffle notice", "notification text")
    End Sub

    Private Sub Persistence()
        Dim settingsType = GetType(Form1).GetNestedType("PersistedQuizState", BindingFlags.NonPublic)
        Dim enabled = settingsType.GetProperty("KeywordNoticesEnabled")
        Dim defaults = JsonSerializer.Deserialize("{}", settingsType)
        Check(CBool(enabled.GetValue(defaults)), "old profiles must default to enabled without an API key")
        Dim disabled = JsonSerializer.Deserialize("{""KeywordNoticesEnabled"":false}", settingsType)
        Check(Not CBool(enabled.GetValue(disabled)), "saved off state was lost")
        Dim roundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(disabled, settingsType), settingsType)
        Check(Not CBool(enabled.GetValue(roundTrip)), "off state did not round-trip")
    End Sub

    Private Async Function Delivery() As Task
        Dim monitor As New QuizNoticeMonitor()
        Dim delivered As New List(Of QuizRaffleNotice)()
        Dim topics As New List(Of String)()
        Dim send As Func(Of QuizRaffleNotice, String, CancellationToken, Task(Of Boolean)) =
            Function(notice, topic, token)
                token.ThrowIfCancellationRequested()
                delivered.Add(notice) : topics.Add(topic)
                Return Task.FromResult(True)
            End Function
        Dim now As New DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc)
        Check(Await monitor.ProcessAsync("window-1", {"Unrelated game message"}, "global-fixture", now, send, CancellationToken.None) = 0, "sent without a keyword")
        Check(Await monitor.ProcessAsync("window-1", {"Quiz in 5 minutes", "Raffle in 30 seconds"}, "global-fixture", now, send, CancellationToken.None) = 2, "missing event delivery")
        Check(topics.All(Function(t) t = "global-fixture"), "did not use configured global topic")
        For elapsedSeconds As Integer = 2 To 200 Step 2
            Check(Await monitor.ProcessAsync("window-1", {"Quiz in " & (300 - elapsedSeconds).ToString() & " seconds", "Raffle in 30 seconds"}, "global-fixture", now.AddSeconds(elapsedSeconds), send, CancellationToken.None) = 0, "visible countdown caused notification spam")
        Next
        Check(Await monitor.ProcessAsync("window-1", {"Quiz in 2 minutes"}, "global-fixture", now.AddSeconds(240), send, CancellationToken.None) = 1, "new notice did not rearm after disappearance")
        Check(Await monitor.ProcessAsync("window-2", {"Quiz in 5 minutes"}, "global-fixture", now.AddSeconds(241), send, CancellationToken.None) = 1, "other game window was suppressed")

        Dim enrich As New QuizNoticeMonitor()
        Check(Await enrich.ProcessAsync("window", {"Quiz"}, "global-fixture", now, send, CancellationToken.None) = 1, "notice without time")
        Check(Await enrich.ProcessAsync("window", {"Quiz in 1 minute"}, "global-fixture", now.AddSeconds(11), send, CancellationToken.None) = 1, "newly legible time was not sent")
        Check(Await enrich.ProcessAsync("window", {"Quiz in 59 seconds"}, "global-fixture", now.AddSeconds(22), send, CancellationToken.None) = 0, "time enrichment repeated")

        Dim retry As New QuizNoticeMonitor()
        Dim attempts = 0
        Dim flaky As Func(Of QuizRaffleNotice, String, CancellationToken, Task(Of Boolean)) =
            Function(n, t, c)
                attempts += 1
                Return Task.FromResult(attempts > 1)
            End Function
        Check(Await retry.ProcessAsync("window", {"Raffle"}, "global-fixture", now, flaky, CancellationToken.None) = 0, "failed delivery counted as sent")
        Await retry.ProcessAsync("window", {"Raffle"}, "global-fixture", now.AddSeconds(2), flaky, CancellationToken.None)
        Check(attempts = 1, "delivery retry storm")
        Check(Await retry.ProcessAsync("window", {"Raffle"}, "global-fixture", now.AddSeconds(61), flaky, CancellationToken.None) = 1, "failed notification not retried")
        Using cancel As New CancellationTokenSource()
            cancel.Cancel()
            Try
                Await retry.ProcessAsync("new-window", {"Quiz"}, "global-fixture", now, send, cancel.Token)
                Throw New Exception("disabled monitoring was allowed to publish")
            Catch ex As OperationCanceledException
            End Try
        End Using
        Using cancel As New CancellationTokenSource()
            Dim calls = 0
            Dim cancelDuring As Func(Of QuizRaffleNotice, String, CancellationToken, Task(Of Boolean)) =
                Function(n, t, c)
                    calls += 1 : cancel.Cancel()
                    Return Task.FromResult(True)
                End Function
            Try
                Await New QuizNoticeMonitor().ProcessAsync("window", {"Quiz", "Raffle"}, "global-fixture", now, cancelDuring, cancel.Token)
                Throw New Exception("cancellation did not stop remaining alerts")
            Catch ex As OperationCanceledException
                Check(calls = 1, "sent another alert after toggle-off")
            End Try
        End Using
    End Function

    Private Sub LocalOcr()
        Using bitmap As New Bitmap(1050, 210), canvas = Graphics.FromImage(bitmap), font As New Font("Segoe UI", 28.0F)
            canvas.Clear(Color.White)
            canvas.DrawString("Quiz starts in 5 minutes", font, Brushes.Black, 25, 25)
            canvas.DrawString("Raffle begins in 30 seconds", font, Brushes.Black, 25, 100)
            Dim lines = OcrReader.ReadScreenTextRegions(bitmap).Select(Function(r) r.Text).ToArray()
            Dim notices = QuizNoticeDetector.Detect(lines)
            Check(notices.Count = 2 AndAlso notices.Any(Function(n) n.Kind = "Quiz" AndAlso n.Timing.Contains("5 minutes")) AndAlso
                notices.Any(Function(n) n.Kind = "Raffle" AndAlso n.Timing.Contains("30 seconds")), "local OCR fixture failed: " & String.Join(" | ", lines))
        End Using
    End Sub
End Module
