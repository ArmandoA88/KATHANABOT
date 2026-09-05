Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private Const QuizUnlockSequence As String = "126974"
    Private Const DefaultQuizModel As String = "gpt-5.4-mini"
    Private ReadOnly _quizScanTimer As New System.Windows.Forms.Timer()
    Private _quizCancellation As CancellationTokenSource
    Private _quizSolveInProgress As Boolean
    Private _quizUnlocked As Boolean
    Private _quizUnlockProgress As String = ""
    Private _quizUnlockLastKeyUtc As DateTime = DateTime.MinValue
    Private _quizSettingsLoading As Boolean
    Private _quizEncryptedApiKey As String = ""
    Private _quizApiKey As String = ""
    Private _quizReferenceWidth As Integer
    Private _quizReferenceHeight As Integer
    Private _quizRegion As RectRegion
    Private _quizAnswersRegion As RectRegion
    Private _quizLastClickedHash As String = ""
    Private _quizLastClickedUtc As DateTime = DateTime.MinValue
    Private _quizRetryAfterUtc As DateTime = DateTime.MinValue
    Private _quizLastClickNormalizedX As Double = -1.0R
    Private _quizLastClickNormalizedY As Double = -1.0R
    Private _quizLastClickedButtonNumber As Integer
    Private ReadOnly _quizAnswerDatabase As New List(Of PersistedQuizAnswer)()
    Private chkQuizSolverEnabled As CheckBox
    Private nudQuizScanMs As NumericUpDown
    Private cboQuizModel As ComboBox
    Private lblQuizApiKey As Label
    Private lblQuizCalibration As Label
    Private lblQuizStatus As Label
    Private lblQuizEvidence As LinkLabel
    Private picQuizPreview As PictureBox
    Private dgvQuizAnswerDatabase As DataGridView
    Private lblQuizDatabaseCount As Label

    Private Class PersistedQuizAnswer
        Public Property SolvedAtLocal As DateTime
        Public Property Question As String = ""
        Public Property Answer As String = ""
        Public Property ButtonNumber As Integer
        Public Property Confidence As Double
        Public Property WasGuess As Boolean
        Public Property AnswerMethod As String = "Legacy"
        Public Property Evidence As String = ""
        Public Property SourceUrl As String = ""
    End Class

    Private Class PersistedQuizState
        Public Property EncryptedApiKey As String = ""
        Public Property ScanIntervalMs As Decimal = 350D
        Public Property Model As String = DefaultQuizModel
        Public Property ReferenceClientWidth As Integer
        Public Property ReferenceClientHeight As Integer
        Public Property QuizRegion As RectRegion
        Public Property AnswersRegion As RectRegion
        Public Property AnswerDatabase As List(Of PersistedQuizAnswer) = New List(Of PersistedQuizAnswer)()
    End Class

    Private Function BuildQuizTab() As TabPage
        Dim tab As New TabPage("Quiz") With {.BackColor = ThemeBg}
        Dim scroller As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = ThemeBg, .Padding = New Padding(34, 24, 34, 24)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .RowCount = 9, .BackColor = ThemeBg}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        scroller.Controls.Add(body)
        tab.Controls.Add(scroller)

        Dim heading As New Label With {
            .Text = "QUIZ SOLVER",
            .Dock = DockStyle.Top,
            .Height = 38,
            .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold),
            .ForeColor = ThemeTextPrimary
        }
        Dim description As New Label With {
            .Text = "Checks Kathana / Tantra Online answers with a fast web lookup. Confident general knowledge is answered directly. Only personal GM trivia may be guessed; unsupported game answers are skipped. Web lookups use your OpenAI API key and incur search charges.",
            .Dock = DockStyle.Top,
            .Height = 54,
            .Font = New Font("Segoe UI", 9.5F),
            .ForeColor = ThemeTextSecondary
        }
        body.Controls.Add(heading)
        body.Controls.Add(description)

        Dim settings As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 120, .ColumnCount = 6, .RowCount = 2, .Padding = New Padding(14), .BackColor = ThemeCard, .Margin = New Padding(0, 8, 0, 12)}
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 115))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 80))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))

        chkQuizSolverEnabled = New CheckBox With {.Text = "Quiz Solver", .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .ForeColor = ThemeTextPrimary}
        nudQuizScanMs = New NumericUpDown With {.Minimum = 100D, .Maximum = 10000D, .Increment = 50D, .Value = 350D, .Dock = DockStyle.Fill}
        cboQuizModel = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        cboQuizModel.Items.AddRange({DefaultQuizModel, "gpt-5-mini"})
        cboQuizModel.SelectedIndex = 0
        settings.Controls.Add(chkQuizSolverEnabled, 0, 0)
        settings.Controls.Add(New Label With {.Text = "Scan every", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleRight, .ForeColor = ThemeTextSecondary}, 1, 0)
        settings.Controls.Add(nudQuizScanMs, 2, 0)
        settings.Controls.Add(New Label With {.Text = "milliseconds", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = ThemeTextSecondary}, 3, 0)
        settings.Controls.Add(New Label With {.Text = "Model", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleRight, .ForeColor = ThemeTextSecondary}, 0, 1)
        settings.Controls.Add(cboQuizModel, 1, 1)
        settings.SetColumnSpan(cboQuizModel, 2)
        body.Controls.Add(settings)

        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .BackColor = ThemeBg, .Margin = New Padding(0, 0, 0, 8)}
        Dim keyButton = CreateQuizButton("Set API Key")
        Dim calibrationButton = CreateQuizButton("Calibrate Overlay")
        Dim previewButton = CreateQuizButton("Preview Mapping")
        Dim solveButton = CreateQuizButton("Solve Now")
        actions.Controls.AddRange({keyButton, calibrationButton, previewButton, solveButton})
        body.Controls.Add(actions)

        Dim information As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 94, .ColumnCount = 1, .RowCount = 3, .BackColor = ThemeCard, .Padding = New Padding(14), .Margin = New Padding(0, 4, 0, 10)}
        lblQuizApiKey = New Label With {.Dock = DockStyle.Fill, .ForeColor = ThemeTextSecondary, .Text = "API key: not configured"}
        lblQuizCalibration = New Label With {.Dock = DockStyle.Fill, .ForeColor = ThemeTextSecondary, .Text = "Calibration: not configured"}
        lblQuizStatus = New Label With {.Dock = DockStyle.Fill, .ForeColor = ThemeAccent, .Text = "Solver is off."}
        information.Controls.Add(lblQuizApiKey)
        information.Controls.Add(lblQuizCalibration)
        information.Controls.Add(lblQuizStatus)
        body.Controls.Add(information)

        lblQuizEvidence = New LinkLabel With {.Text = "Answer sources will appear here. Historical guesses are not reused as verified answers.", .Dock = DockStyle.Top, .Height = 56, .ForeColor = ThemeTextSecondary, .LinkColor = ThemeAccent, .AutoEllipsis = True}
        AddHandler lblQuizEvidence.LinkClicked, Sub(sender, args) OpenQuizSource(TryCast(args.Link.LinkData, String))
        body.Controls.Add(lblQuizEvidence)

        Dim safety As New Label With {
            .Text = "Click safety: the button layout is detected again immediately before input. The selected answer receives 10 complete left-clicks, 50 ms apart. The cursor is restored afterward.",
            .Dock = DockStyle.Top,
            .Height = 44,
            .ForeColor = ThemeTextSecondary
        }
        body.Controls.Add(safety)
        picQuizPreview = New PictureBox With {.Dock = DockStyle.Top, .Height = 390, .BackColor = Color.Black, .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom}
        body.Controls.Add(picQuizPreview)

        Dim databaseHeader As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 42, .ColumnCount = 2, .BackColor = ThemeBg, .Margin = New Padding(0, 16, 0, 4)}
        databaseHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        databaseHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
        databaseHeader.Controls.Add(New Label With {.Text = "QUESTION / ANSWER DATABASE", .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .ForeColor = ThemeTextPrimary, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        lblQuizDatabaseCount = New Label With {.Text = "0 solved questions", .Dock = DockStyle.Fill, .ForeColor = ThemeTextSecondary, .TextAlign = ContentAlignment.MiddleRight}
        databaseHeader.Controls.Add(lblQuizDatabaseCount, 1, 0)
        body.Controls.Add(databaseHeader)

        dgvQuizAnswerDatabase = New DataGridView With {
            .Dock = DockStyle.Top,
            .Height = 250,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .RowHeadersVisible = False,
            .AutoGenerateColumns = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .MultiSelect = False,
            .BackgroundColor = ThemeSurface,
            .BorderStyle = BorderStyle.FixedSingle
        }
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "SolvedAt", .HeaderText = "Solved", .Width = 145})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Question", .HeaderText = "Question", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 52.0F})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Answer", .HeaderText = "Answer", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 30.0F})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Button", .HeaderText = "Button", .Width = 68})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Confidence", .HeaderText = "Confidence", .Width = 92})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Guess", .HeaderText = "Guess", .Width = 58})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Method", .HeaderText = "Method", .Width = 115})
        dgvQuizAnswerDatabase.Columns.Add(New DataGridViewLinkColumn With {.Name = "Source", .HeaderText = "Source", .Width = 150, .LinkColor = ThemeAccent, .VisitedLinkColor = ThemeAccent})
        AddHandler dgvQuizAnswerDatabase.CellContentClick,
            Sub(sender, args)
                If args.RowIndex >= 0 AndAlso args.ColumnIndex = dgvQuizAnswerDatabase.Columns("Source").Index Then
                    OpenQuizSource(TryCast(dgvQuizAnswerDatabase.Rows(args.RowIndex).Cells("Source").Value, String))
                End If
            End Sub
        body.Controls.Add(dgvQuizAnswerDatabase)

        AddHandler chkQuizSolverEnabled.CheckedChanged, AddressOf QuizSolverEnabledChanged
        AddHandler nudQuizScanMs.ValueChanged, AddressOf QuizScanIntervalChanged
        AddHandler cboQuizModel.SelectedIndexChanged, Sub() If Not _quizSettingsLoading Then SavePersistedListState(False)
        AddHandler keyButton.Click, Sub() ConfigureQuizApiKey()
        AddHandler calibrationButton.Click, Sub() CalibrateQuizOverlay()
        AddHandler previewButton.Click, Sub() RefreshQuizPreview()
        AddHandler solveButton.Click, Async Sub() Await RunQuizSolverOnceAsync(True)
        AddHandler _quizScanTimer.Tick, AddressOf QuizScanTimerTick
        _quizScanTimer.Interval = CInt(nudQuizScanMs.Value)
        Return tab
    End Function

    Private Async Sub QuizScanTimerTick(sender As Object, e As EventArgs)
        ' Keep the visual preview live while an API request is in flight. The solver request itself
        ' remains single-flight, but a 100 ms timer can still refresh the calibrated game image.
        If _quizSolveInProgress Then
            RefreshQuizLivePreview(False)
            Return
        End If
        Await RunQuizSolverOnceAsync(False)
    End Sub

    Private Shared Function CreateQuizButton(text As String) As Button
        Return New Button With {
            .Text = text,
            .Size = New Size(150, 38),
            .Margin = New Padding(0, 0, 10, 8),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(31, 81, 125),
            .ForeColor = Color.White
        }
    End Function

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If Not _quizUnlocked AndAlso _mainTabs IsNot Nothing AndAlso _mainTabs.SelectedTab Is _dashboardTab Then
            Dim key = keyData And Keys.KeyCode
            Dim digit As String = Nothing
            If key >= Keys.D0 AndAlso key <= Keys.D9 Then
                digit = ChrW(AscW("0"c) + CInt(key - Keys.D0)).ToString()
            ElseIf key >= Keys.NumPad0 AndAlso key <= Keys.NumPad9 Then
                digit = ChrW(AscW("0"c) + CInt(key - Keys.NumPad0)).ToString()
            End If
            If digit IsNot Nothing Then ObserveQuizUnlockDigit(digit)
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub ObserveQuizUnlockDigit(digit As String)
        If (DateTime.UtcNow - _quizUnlockLastKeyUtc).TotalSeconds > 4 Then _quizUnlockProgress = ""
        _quizUnlockLastKeyUtc = DateTime.UtcNow
        Dim candidate = _quizUnlockProgress & digit
        If QuizUnlockSequence.StartsWith(candidate, StringComparison.Ordinal) Then
            _quizUnlockProgress = candidate
        Else
            _quizUnlockProgress = If(QuizUnlockSequence.StartsWith(digit, StringComparison.Ordinal), digit, "")
        End If
        If _quizUnlockProgress = QuizUnlockSequence Then
            _quizUnlockProgress = ""
            UnlockQuizTab()
        End If
    End Sub

    Private Sub UnlockQuizTab()
        If _quizUnlocked OrElse _quizTab Is Nothing OrElse _mainTabs Is Nothing Then Return
        _quizUnlocked = True
        ApplyDarkTheme(_quizTab)
        ApplyDarkTheme(_resuTab)
        ' Use the same authoritative tab builder as edition/developer-mode changes. It inserts Quiz
        ' after Diagnostics (when visible) and immediately before Update, and now preserves it on
        ' every later sidebar refresh for the rest of this executable session.
        RefreshMainTabsVisibility()
        _mainTabs.FitTabsToHeight()
        _mainTabs.SelectedTab = _quizTab
        SyncSidebarRailOverlayBounds()
        _sidebarRailOverlay?.Invalidate()
        UpdateMainTabIndicators()
        UpdateTabIndicatorTarget()
        ' The sequence also reveals RESU, which uses local OCR and needs no API key.
        ' Quiz already exposes its own Configure API Key button.
    End Sub

    Private Sub ConfigureQuizApiKey()
        Using dialog As New QuizApiKeyDialog(_quizApiKey)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                _quizApiKey = dialog.ApiKey
                _quizEncryptedApiKey = QuizSecretStore.Protect(_quizApiKey)
                UpdateQuizUiState()
                SavePersistedListState(True)
            Catch ex As Exception
                _quizApiKey = ""
                _quizEncryptedApiKey = ""
                MessageBox.Show(Me, "The API key could not be encrypted: " & ex.Message, "Quiz Solver", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub CalibrateQuizOverlay()
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse Not IsUsableQuizWindow(selected.MainWindowHandle) Then
            MessageBox.Show(Me, "Select a running Full game window first.", "Quiz Calibration", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Using screenshot = BotEngine.CaptureClient(selected.MainWindowHandle)
                Dim existingQuiz As Rectangle = Rectangle.Empty
                Dim existingAnswers As Rectangle = Rectangle.Empty
                If _quizReferenceWidth > 0 AndAlso _quizReferenceHeight > 0 Then
                    existingQuiz = QuizImageTools.ScaleRegion(_quizRegion, _quizReferenceWidth, _quizReferenceHeight, screenshot.Width, screenshot.Height)
                    existingAnswers = QuizImageTools.ScaleRegion(_quizAnswersRegion, _quizReferenceWidth, _quizReferenceHeight, screenshot.Width, screenshot.Height)
                End If
                Using dialog As New QuizCalibrationForm(screenshot, existingQuiz, existingAnswers)
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                    _quizReferenceWidth = screenshot.Width
                    _quizReferenceHeight = screenshot.Height
                    _quizRegion = dialog.QuizRegionResult
                    _quizAnswersRegion = dialog.AnswersRegionResult
                End Using
            End Using
            UpdateQuizUiState()
            SavePersistedListState(True)
            RefreshQuizPreview()
        Catch ex As Exception
            MessageBox.Show(Me, "Unable to capture the selected game window: " & ex.Message, "Quiz Calibration", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub QuizSolverEnabledChanged(sender As Object, e As EventArgs)
        If _quizSettingsLoading Then Return
        If chkQuizSolverEnabled.Checked Then
            If Not ValidateQuizSetup(True) Then
                _quizSettingsLoading = True
                chkQuizSolverEnabled.Checked = False
                _quizSettingsLoading = False
                Return
            End If
            _quizScanTimer.Interval = CInt(nudQuizScanMs.Value)
            _quizScanTimer.Start()
            SetQuizStatus("Solver is watching for a quiz.", ThemeGood)
            BeginInvoke(New Action(Async Sub() Await RunQuizSolverOnceAsync(False)))
        Else
            _quizScanTimer.Stop()
            If _quizCancellation IsNot Nothing Then _quizCancellation.Cancel()
            SetQuizStatus("Solver is off.", ThemeTextSecondary)
        End If
    End Sub

    Private Sub QuizScanIntervalChanged(sender As Object, e As EventArgs)
        If nudQuizScanMs Is Nothing Then Return
        _quizScanTimer.Interval = CInt(nudQuizScanMs.Value)
        If Not _quizSettingsLoading Then SavePersistedListState(False)
    End Sub

    Private Function ValidateQuizSetup(showMessages As Boolean) As Boolean
        If String.IsNullOrWhiteSpace(_quizApiKey) Then
            If showMessages Then ConfigureQuizApiKey()
            If String.IsNullOrWhiteSpace(_quizApiKey) Then Return False
        End If
        If _quizReferenceWidth <= 0 OrElse _quizReferenceHeight <= 0 OrElse _quizRegion Is Nothing OrElse _quizAnswersRegion Is Nothing Then
            If showMessages Then
                MessageBox.Show(Me, "Calibrate the quiz and answer areas before enabling the solver.", "Quiz Solver", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
            Return False
        End If
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse Not IsUsableQuizWindow(selected.MainWindowHandle) Then
            If showMessages Then MessageBox.Show(Me, "Select a running Full game window first.", "Quiz Solver", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If
        Return True
    End Function

    Private Shared Function IsUsableQuizWindow(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero AndAlso NativeMethods.IsWindowVisible(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd)
    End Function

    Private Async Function RunQuizSolverOnceAsync(manual As Boolean) As Task
        If _quizSolveInProgress Then Return
        If Not manual AndAlso (chkQuizSolverEnabled Is Nothing OrElse Not chkQuizSolverEnabled.Checked) Then Return
        If Not manual AndAlso DateTime.UtcNow < _quizRetryAfterUtc Then
            RefreshQuizLivePreview(False)
            Return
        End If
        If Not ValidateQuizSetup(manual) Then Return
        _quizSolveInProgress = True
        _quizCancellation?.Cancel()
        _quizCancellation?.Dispose()
        _quizCancellation = New CancellationTokenSource()
        Dim cancellationToken = _quizCancellation.Token
        Dim lookupStarted As Boolean = False
        Dim answerClicked As Boolean = False
        Try
            Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
            Dim hwnd = selected.MainWindowHandle
            Dim clientRect As New NativeMethods.RECT()
            If Not NativeMethods.GetClientRect(hwnd, clientRect) Then Throw New InvalidOperationException("Could not read the game client size.")
            Dim clientWidth = clientRect.Right - clientRect.Left
            Dim clientHeight = clientRect.Bottom - clientRect.Top
            Dim quizArea = QuizImageTools.ScaleRegion(_quizRegion, _quizReferenceWidth, _quizReferenceHeight, clientWidth, clientHeight)
            Dim answersArea = QuizImageTools.ScaleRegion(_quizAnswersRegion, _quizReferenceWidth, _quizReferenceHeight, clientWidth, clientHeight)
            If quizArea.IsEmpty OrElse answersArea.IsEmpty OrElse Not quizArea.Contains(answersArea) Then Throw New InvalidOperationException("The scaled answer area is outside the quiz area. Recalibrate it.")

            Dim visualHash As String
            Dim buttons As List(Of Rectangle)
            Using frame = BotEngine.CaptureClient(hwnd),
                  quizImage = QuizImageTools.Crop(frame, quizArea),
                  answerImage = QuizImageTools.Crop(frame, answersArea)
                buttons = QuizImageTools.DetectAnswerButtons(answerImage)
                Dim relativeAnswers As New Rectangle(answersArea.X - quizArea.X, answersArea.Y - quizArea.Y, answersArea.Width, answersArea.Height)
                Dim marker = GetQuizClickMarker(quizArea, clientWidth, clientHeight)
                Dim markedButton = If(marker.HasValue, _quizLastClickedButtonNumber, 0)
                Using annotated = QuizImageTools.CreateAnnotatedQuiz(quizImage, relativeAnswers, buttons, markedButton, marker)
                    SetQuizPreview(annotated)
                End Using
                If Not QuizImageTools.IsPlausibleQuizLayout(buttons) Then
                    If (DateTime.UtcNow - _quizLastClickedUtc).TotalSeconds >= 2 Then _quizLastClickedHash = ""
                    SetQuizStatus($"Watching — no complete quiz layout detected ({buttons.Count} button candidate(s)).", ThemeTextSecondary)
                    Return
                End If
                visualHash = QuizImageTools.PerceptualHash(answerImage)
                Dim expectedQuizHash = QuizImageTools.PerceptualHash(quizImage)
                Dim quizIdentity = expectedQuizHash & ":" & visualHash
                If quizIdentity = _quizLastClickedHash OrElse (DateTime.UtcNow - _quizLastClickedUtc).TotalSeconds < 15 Then
                    SetQuizStatus("Already answered this visible quiz; waiting for the next one.", ThemeTextSecondary)
                    Return
                End If
                Using annotated = QuizImageTools.CreateAnnotatedQuiz(quizImage, relativeAnswers, buttons)
                    SetQuizPreview(annotated)
                    SetQuizStatus($"Reading {buttons.Count} choices; checking Kathana / Tantra sources when needed...", ThemeAccent)
                    lblQuizEvidence.Links.Clear()
                    lblQuizEvidence.Text = "Looking up the current question..."
                    Dim model = If(cboQuizModel.SelectedItem?.ToString(), DefaultQuizModel)
                    lookupStarted = True
                    Dim answer = Await QuizOpenAiClient.SolveAsync(_quizApiKey, model, quizImage, annotated, cancellationToken)
                    cancellationToken.ThrowIfCancellationRequested()
                    ShowQuizEvidence(answer)
                    Dim skipReason As String = ""
                    If Not QuizAnswerPolicy.CanClick(answer, buttons.Count, skipReason) Then
                        SetQuizStatus("Skipped: " & skipReason, ThemeWarn)
                        AppendLog($"Quiz solver skipped: {answer.QuestionText}; {skipReason} {answer.Evidence}")
                        Return
                    End If
                    Dim targetIndex = answer.ButtonNumber - 1
                    Dim currentWindow = GetSelectedProcessWindowForEdition(BotEdition.Full)
                    If currentWindow Is Nothing OrElse currentWindow.MainWindowHandle <> hwnd Then
                        SetQuizStatus("Skipped: the selected game changed during lookup.", ThemeWarn)
                        Return
                    End If
                    Dim currentClient As NativeMethods.RECT
                    If Not NativeMethods.GetClientRect(hwnd, currentClient) OrElse currentClient.Right - currentClient.Left <> clientWidth OrElse currentClient.Bottom - currentClient.Top <> clientHeight Then
                        SetQuizStatus("Skipped: game window size changed during lookup.", ThemeWarn)
                        Return
                    End If

                    Dim clickedClientPoint As System.Drawing.Point
                    If Not RevalidateAndClickQuizAnswer(hwnd, quizArea, answersArea, targetIndex, visualHash, expectedQuizHash, clickedClientPoint) Then
                        SetQuizStatus("Quiz changed before the click; skipped it and will scan again.", ThemeWarn)
                        Return
                    End If
                    _quizLastClickedHash = quizIdentity
                    _quizLastClickedUtc = DateTime.UtcNow
                    answerClicked = True
                    _quizLastClickNormalizedX = clickedClientPoint.X / CDbl(Math.Max(1, clientWidth))
                    _quizLastClickNormalizedY = clickedClientPoint.Y / CDbl(Math.Max(1, clientHeight))
                    _quizLastClickedButtonNumber = targetIndex + 1
                    Dim guessText = $" ({QuizAnswerPolicy.MethodLabel(answer)})"
                    SetQuizStatus($"Clicked answer {targetIndex + 1} x10 (50 ms): {answer.AnswerText}{guessText} — {answer.Confidence:P0} confidence", ThemeGood)
                    AppendLog($"Quiz solver: {answer.QuestionText} -> {answer.AnswerText}; button {targetIndex + 1}; sent 10 left-clicks 50 ms apart; confidence {answer.Confidence:P0}{guessText}. {answer.Evidence} {answer.SourceUrl}")
                    AddQuizDatabaseEntry(answer, targetIndex + 1)
                    RefreshQuizLivePreview(False)
                End Using
            End Using
        Catch ex As OperationCanceledException
        Catch ex As Exception
            SetQuizStatus("Solver error: " & ex.Message, ThemeWarn)
            AppendLog("Quiz solver error: " & ex.Message)
        Finally
            _quizSolveInProgress = False
            ' Keep screen refreshes cheap after an unresolved answer, timeout, or API error.
            If lookupStarted AndAlso Not answerClicked Then _quizRetryAfterUtc = DateTime.UtcNow.AddSeconds(15)
        End Try
    End Function

    Private Function RevalidateAndClickQuizAnswer(hwnd As IntPtr,
                                                  quizArea As Rectangle,
                                                  answersArea As Rectangle,
                                                  targetIndex As Integer,
                                                  expectedHash As String,
                                                  expectedQuizHash As String,
                                                  ByRef clickedClientPoint As System.Drawing.Point) As Boolean
        clickedClientPoint = System.Drawing.Point.Empty
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse selected.MainWindowHandle <> hwnd OrElse Not IsUsableQuizWindow(hwnd) Then Return False
        Using liveQuiz = BotEngine.CaptureClientRegion(hwnd, New RectRegion(quizArea.X, quizArea.Y, quizArea.Width, quizArea.Height)),
              liveAnswer = QuizImageTools.Crop(liveQuiz, New Rectangle(answersArea.X - quizArea.X, answersArea.Y - quizArea.Y, answersArea.Width, answersArea.Height))
            If PerceptualHashDistance(expectedQuizHash, QuizImageTools.PerceptualHash(liveQuiz)) > 6 Then Return False
            Dim liveButtons = QuizImageTools.DetectAnswerButtons(liveAnswer)
            If targetIndex < 0 OrElse targetIndex >= liveButtons.Count Then Return False
            Dim liveHash = QuizImageTools.PerceptualHash(liveAnswer)
            If PerceptualHashDistance(expectedHash, liveHash) > 12 Then Return False
            Dim target = liveButtons(targetIndex)
            Dim clientPoint As New NativeMethods.POINT With {
                .X = answersArea.Left + target.Left + target.Width \ 2,
                .Y = answersArea.Top + target.Top + target.Height \ 2
            }
            clickedClientPoint = New System.Drawing.Point(clientPoint.X, clientPoint.Y)
            If Not NativeMethods.ClientToScreen(hwnd, clientPoint) Then Return False
            Return PerformQuizClickBurst(hwnd, clientPoint)
        End Using
    End Function

    Private Shared Function PerceptualHashDistance(first As String, second As String) As Integer
        Dim a As ULong
        Dim b As ULong
        If Not ULong.TryParse(first, Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, a) OrElse
           Not ULong.TryParse(second, Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, b) Then Return 64
        Dim value = a Xor b
        Dim count As Integer = 0
        While value <> 0
            count += CInt(value And 1UL)
            value >>= 1
        End While
        Return count
    End Function

    Private Shared Function PerformQuizClickBurst(hwnd As IntPtr, screenPoint As NativeMethods.POINT) As Boolean
        Dim previous As New NativeMethods.POINT()
        Dim hadCursor = NativeMethods.GetCursorPos(previous)
        Try
            If NativeMethods.IsIconic(hwnd) Then
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE)
            End If
            ForceSetForegroundWindow(hwnd)
            NativeMethods.BringWindowToTop(hwnd)
            If Not NativeMethods.SetCursorPos(screenPoint.X, screenPoint.Y) Then Return False
            For clickIndex = 1 To 10
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0UI, 0UI, 0UI, UIntPtr.Zero)
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0UI, 0UI, 0UI, UIntPtr.Zero)
                If clickIndex < 10 Then Thread.Sleep(50)
            Next
            Return True
        Finally
            If hadCursor Then NativeMethods.SetCursorPos(previous.X, previous.Y)
        End Try
    End Function

    Private Sub RefreshQuizPreview()
        RefreshQuizLivePreview(True)
    End Sub

    Private Sub RefreshQuizLivePreview(showMessages As Boolean)
        If _quizReferenceWidth <= 0 OrElse _quizReferenceHeight <= 0 OrElse _quizRegion Is Nothing OrElse _quizAnswersRegion Is Nothing Then
            If showMessages Then MessageBox.Show(Me, "Calibrate the quiz and answer areas first.", "Quiz Preview", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse Not IsUsableQuizWindow(selected.MainWindowHandle) Then
            If showMessages Then MessageBox.Show(Me, "Select a running Full game window first.", "Quiz Preview", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Using frame = BotEngine.CaptureClient(selected.MainWindowHandle)
                Dim quizArea = QuizImageTools.ScaleRegion(_quizRegion, _quizReferenceWidth, _quizReferenceHeight, frame.Width, frame.Height)
                Dim answersArea = QuizImageTools.ScaleRegion(_quizAnswersRegion, _quizReferenceWidth, _quizReferenceHeight, frame.Width, frame.Height)
                If quizArea.IsEmpty OrElse answersArea.IsEmpty OrElse Not quizArea.Contains(answersArea) Then Return
                Using quizImage = QuizImageTools.Crop(frame, quizArea), answerImage = QuizImageTools.Crop(frame, answersArea)
                    Dim buttons = QuizImageTools.DetectAnswerButtons(answerImage)
                    Dim relativeAnswers As New Rectangle(answersArea.X - quizArea.X, answersArea.Y - quizArea.Y, answersArea.Width, answersArea.Height)
                    Dim marker = GetQuizClickMarker(quizArea, frame.Width, frame.Height)
                    Dim markedButton = If(marker.HasValue, _quizLastClickedButtonNumber, 0)
                    Using annotated = QuizImageTools.CreateAnnotatedQuiz(quizImage, relativeAnswers, buttons, markedButton, marker)
                        SetQuizPreview(annotated)
                    End Using
                    If showMessages Then
                        Dim plausible = QuizImageTools.IsPlausibleQuizLayout(buttons)
                        SetQuizStatus($"Preview updated: {buttons.Count} candidate(s); quiz layout {If(plausible, "recognized", "not recognized")}.", If(plausible, ThemeGood, ThemeWarn))
                    End If
                End Using
            End Using
        Catch ex As Exception
            If showMessages OrElse Not _quizSolveInProgress Then SetQuizStatus("Preview error: " & ex.Message, ThemeWarn)
        End Try
    End Sub

    Private Function GetQuizClickMarker(quizArea As Rectangle, clientWidth As Integer, clientHeight As Integer) As System.Drawing.Point?
        If _quizLastClickNormalizedX < 0 OrElse _quizLastClickNormalizedY < 0 OrElse
           (DateTime.UtcNow - _quizLastClickedUtc).TotalSeconds > 30 Then Return Nothing
        Dim clientX = CInt(Math.Round(_quizLastClickNormalizedX * clientWidth))
        Dim clientY = CInt(Math.Round(_quizLastClickNormalizedY * clientHeight))
        Dim local As New System.Drawing.Point(clientX - quizArea.Left, clientY - quizArea.Top)
        If local.X < 0 OrElse local.Y < 0 OrElse local.X >= quizArea.Width OrElse local.Y >= quizArea.Height Then Return Nothing
        Return local
    End Function

    Private Sub AddQuizDatabaseEntry(answer As QuizSolveResult, buttonNumber As Integer)
        If answer Is Nothing Then Return
        _quizAnswerDatabase.Insert(0, New PersistedQuizAnswer With {
            .SolvedAtLocal = DateTime.Now,
            .Question = If(answer.QuestionText, "").Trim(),
            .Answer = If(answer.AnswerText, "").Trim(),
            .ButtonNumber = buttonNumber,
            .Confidence = Math.Max(0.0R, Math.Min(1.0R, answer.Confidence)),
            .WasGuess = answer.IsGuess,
            .AnswerMethod = QuizAnswerPolicy.MethodLabel(answer),
            .Evidence = If(answer.Evidence, ""),
            .SourceUrl = If(answer.SourceUrl, "")
        })
        If _quizAnswerDatabase.Count > 2000 Then _quizAnswerDatabase.RemoveRange(2000, _quizAnswerDatabase.Count - 2000)
        RefreshQuizDatabaseGrid()
        SavePersistedListState(False)
    End Sub

    Private Sub RefreshQuizDatabaseGrid()
        If dgvQuizAnswerDatabase Is Nothing Then Return
        dgvQuizAnswerDatabase.Rows.Clear()
        For Each entry In _quizAnswerDatabase
            dgvQuizAnswerDatabase.Rows.Add(
                entry.SolvedAtLocal.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.Question,
                entry.Answer,
                entry.ButtonNumber,
                entry.Confidence.ToString("P0"),
                entry.WasGuess,
                entry.AnswerMethod,
                entry.SourceUrl)
            dgvQuizAnswerDatabase.Rows(dgvQuizAnswerDatabase.Rows.Count - 1).Cells("Answer").ToolTipText = If(entry.Evidence, "")
        Next
        If lblQuizDatabaseCount IsNot Nothing Then lblQuizDatabaseCount.Text = $"{_quizAnswerDatabase.Count:N0} solved question(s)"
    End Sub

    Private Shared Function CloneQuizAnswer(entry As PersistedQuizAnswer) As PersistedQuizAnswer
        If entry Is Nothing Then Return Nothing
        Return New PersistedQuizAnswer With {
            .SolvedAtLocal = entry.SolvedAtLocal,
            .Question = If(entry.Question, ""),
            .Answer = If(entry.Answer, ""),
            .ButtonNumber = entry.ButtonNumber,
            .Confidence = entry.Confidence,
            .WasGuess = entry.WasGuess,
            .AnswerMethod = If(entry.AnswerMethod, "Legacy"),
            .Evidence = If(entry.Evidence, ""),
            .SourceUrl = QuizOpenAiClient.NormalizeSourceUrl(entry.SourceUrl)
        }
    End Function

    Private Sub ShowQuizEvidence(answer As QuizSolveResult)
        lblQuizEvidence.Links.Clear()
        Dim description = If(answer.CanAnswer, QuizAnswerPolicy.MethodLabel(answer), "Unresolved") & ": " & If(answer.Evidence, "")
        lblQuizEvidence.Text = description
        If answer.SourceVerified AndAlso Not String.IsNullOrWhiteSpace(answer.SourceUrl) Then
            lblQuizEvidence.Text &= Environment.NewLine & answer.SourceUrl
            lblQuizEvidence.Links.Add(description.Length + Environment.NewLine.Length, answer.SourceUrl.Length, answer.SourceUrl)
        End If
    End Sub

    Private Sub OpenQuizSource(value As String)
        Dim source = QuizOpenAiClient.NormalizeSourceUrl(value)
        If source.Length = 0 Then Return
        Try
            Process.Start(New ProcessStartInfo(source) With {.UseShellExecute = True})
        Catch ex As Exception
            AppendLog("Unable to open quiz source: " & ex.Message)
        End Try
    End Sub

    Private Sub SetQuizPreview(image As Bitmap)
        If picQuizPreview Is Nothing OrElse image Is Nothing Then Return
        Dim replacement As New Bitmap(image)
        Dim previous = picQuizPreview.Image
        picQuizPreview.Image = replacement
        If previous IsNot Nothing Then previous.Dispose()
    End Sub

    Private Sub SetQuizStatus(text As String, color As Color)
        If lblQuizStatus Is Nothing Then Return
        lblQuizStatus.Text = text
        lblQuizStatus.ForeColor = color
    End Sub

    Private Sub UpdateQuizUiState()
        If lblQuizApiKey IsNot Nothing Then lblQuizApiKey.Text = If(String.IsNullOrWhiteSpace(_quizApiKey), "API key: not configured", "API key: encrypted and remembered for this Windows account")
        If lblQuizCalibration IsNot Nothing Then
            If _quizReferenceWidth > 0 AndAlso _quizReferenceHeight > 0 AndAlso _quizRegion IsNot Nothing AndAlso _quizAnswersRegion IsNot Nothing Then
                lblQuizCalibration.Text = $"Calibration: client {_quizReferenceWidth}x{_quizReferenceHeight}; quiz {_quizRegion.X},{_quizRegion.Y} {_quizRegion.W}x{_quizRegion.H}; answers {_quizAnswersRegion.X},{_quizAnswersRegion.Y} {_quizAnswersRegion.W}x{_quizAnswersRegion.H}"
            Else
                lblQuizCalibration.Text = "Calibration: not configured"
            End If
        End If
    End Sub

    Private Sub ApplyPersistedQuizState(state As PersistedQuizState)
        _quizSettingsLoading = True
        Try
            state = If(state, New PersistedQuizState())
            _quizEncryptedApiKey = If(state.EncryptedApiKey, "")
            _quizApiKey = QuizSecretStore.Unprotect(_quizEncryptedApiKey)
            _quizReferenceWidth = Math.Max(0, state.ReferenceClientWidth)
            _quizReferenceHeight = Math.Max(0, state.ReferenceClientHeight)
            _quizRegion = CloneQuizRegion(state.QuizRegion)
            _quizAnswersRegion = CloneQuizRegion(state.AnswersRegion)
            _quizAnswerDatabase.Clear()
            If state.AnswerDatabase IsNot Nothing Then
                _quizAnswerDatabase.AddRange(state.AnswerDatabase.Where(Function(entry) entry IsNot Nothing).Take(2000).Select(Function(entry) CloneQuizAnswer(entry)))
            End If
            If nudQuizScanMs IsNot Nothing Then nudQuizScanMs.Value = Math.Max(nudQuizScanMs.Minimum, Math.Min(nudQuizScanMs.Maximum, state.ScanIntervalMs))
            If cboQuizModel IsNot Nothing Then
                Dim wanted = If(String.IsNullOrWhiteSpace(state.Model), DefaultQuizModel, state.Model.Trim())
                Dim index = cboQuizModel.FindStringExact(wanted)
                cboQuizModel.SelectedIndex = If(index >= 0, index, 0)
            End If
            If chkQuizSolverEnabled IsNot Nothing Then chkQuizSolverEnabled.Checked = False
            _quizScanTimer.Interval = If(nudQuizScanMs IsNot Nothing, CInt(nudQuizScanMs.Value), 350)
        Finally
            _quizSettingsLoading = False
        End Try
        UpdateQuizUiState()
        RefreshQuizDatabaseGrid()
    End Sub

    Private Function BuildPersistedQuizState() As PersistedQuizState
        Return New PersistedQuizState With {
            .EncryptedApiKey = If(_quizEncryptedApiKey, ""),
            .ScanIntervalMs = If(nudQuizScanMs IsNot Nothing, nudQuizScanMs.Value, 350D),
            .Model = If(cboQuizModel IsNot Nothing AndAlso cboQuizModel.SelectedItem IsNot Nothing, cboQuizModel.SelectedItem.ToString(), DefaultQuizModel),
            .ReferenceClientWidth = _quizReferenceWidth,
            .ReferenceClientHeight = _quizReferenceHeight,
            .QuizRegion = CloneQuizRegion(_quizRegion),
            .AnswersRegion = CloneQuizRegion(_quizAnswersRegion),
            .AnswerDatabase = _quizAnswerDatabase.Where(Function(entry) entry IsNot Nothing).Take(2000).Select(Function(entry) CloneQuizAnswer(entry)).ToList()
        }
    End Function

    Private Shared Function CloneQuizRegion(region As RectRegion) As RectRegion
        If region Is Nothing Then Return Nothing
        Return New RectRegion(region.X, region.Y, region.W, region.H)
    End Function

    Private Sub ShutdownQuizSolver()
        _quizScanTimer.Stop()
        If _quizCancellation IsNot Nothing Then
            _quizCancellation.Cancel()
            _quizCancellation.Dispose()
            _quizCancellation = Nothing
        End If
        If picQuizPreview IsNot Nothing AndAlso picQuizPreview.Image IsNot Nothing Then
            picQuizPreview.Image.Dispose()
            picQuizPreview.Image = Nothing
        End If
    End Sub
End Class
