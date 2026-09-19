Imports System.IO
Imports System.Text

Partial Public Class Form1
    Private ReadOnly _modes As New OperatingModeController
    Private _workflowModeStore As OperatingModeController
    Private ReadOnly Property _workflowModes As OperatingModeController
        Get
            If _workflowModeStore Is Nothing Then _workflowModeStore = New OperatingModeController()
            Return _workflowModeStore
        End Get
    End Property
    Private _runtimeBanner As Label
    Private _changesLabel As Label
    Private _runtimeTimer As System.Windows.Forms.Timer
    Private _activityText As TextBox
    Private _readingText As TextBox
    Private _routePreview As Panel
    Private _routeNodes As New List(Of NavigationNode)
    Private _saveState As String = "Saved"
    Private _applyState As String = "Applied"
    Private _setupStep As Integer
    Private _setupText As Label
    Private _setupConfirmed As CheckBox

    Private Sub InitializeRuntimeTools()
        Dim bar As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Height = 72, .ColumnCount = 2, .BackColor = Color.FromArgb(20, 35, 52)}
        bar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        bar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190))
        _runtimeBanner = New Label With {.Dock = DockStyle.Fill, .ForeColor = Color.White, .Font = New Font("Segoe UI", 11, FontStyle.Bold), .AutoEllipsis = True}
        _changesLabel = New Label With {.Dock = DockStyle.Fill, .ForeColor = Color.Gold}
        bar.Controls.Add(_runtimeBanner, 0, 0)
        bar.Controls.Add(_changesLabel, 0, 1)
        Dim tab As New TabPage("Activity & Setup") With {.BackColor = ThemeBg}
        Dim open As New Button With {.Text = "Activity / setup", .Dock = DockStyle.Fill}
        AddHandler open.Click, Sub() _mainTabs.SelectedTab = tab
        bar.Controls.Add(open, 1, 0)
        bar.SetRowSpan(open, 2)
        Dim diagnosticsContent = _diagnosticsTab.Controls(0)
        _diagnosticsTab.Controls.Remove(diagnosticsContent)
        Dim diagnosticsLayout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
        diagnosticsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        diagnosticsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 72))
        diagnosticsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        diagnosticsLayout.Controls.Add(bar, 0, 0)
        diagnosticsLayout.Controls.Add(diagnosticsContent, 0, 1)
        _diagnosticsTab.Controls.Add(diagnosticsLayout)
        Dim sections As New TabControl With {.Dock = DockStyle.Fill}
        tab.Controls.Add(sections)
        _activityText = RuntimeTextPage(sections, "Timeline / skipped skills")
        _readingText = RuntimeTextPage(sections, "OCR readings")
        Dim routeTab As New TabPage("Route preview")
        _routePreview = New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(18, 24, 36)}
        AddHandler _routePreview.Paint, AddressOf PaintRuntimeRoute
        routeTab.Controls.Add(_routePreview)
        Dim refresh As New Button With {.Dock = DockStyle.Top, .Height = 36, .Text = "Preview route selected in Leveling (red = jump over 100 coordinate units)"}
        AddHandler refresh.Click,
            Sub()
                Dim name = ExtractRecordedRouteName(If(cboRecordedRoute Is Nothing, Nothing, cboRecordedRoute.SelectedItem))
                _routeNodes = If(name = "", New List(Of NavigationNode), BotEngine.GetRecordedRouteNodeOptions(name, GetSelectedNavigationMapName()))
                _routePreview.Invalidate()
            End Sub
        routeTab.Controls.Add(refresh)
        sections.TabPages.Add(routeTab)
        Dim setup As New TabPage("Guided setup")
        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .Padding = New Padding(24), .AutoScroll = True}
        _setupText = New Label With {.Width = 720, .Height = 130, .ForeColor = ThemeTextPrimary}
        _setupConfirmed = New CheckBox With {.Text = "I checked this step in the game", .AutoSize = True}
        Dim go As New Button With {.Text = "Open this setting", .AutoSize = True}
        Dim nextStep As New Button With {.Text = "Validate and continue", .AutoSize = True}
        Dim back As New Button With {.Text = "Back / restart", .AutoSize = True}
        AddHandler go.Click, Sub() _mainTabs.SelectedTab = If(_setupStep < 2, _visionTab, _combatTab)
        AddHandler back.Click, Sub()
                                   _setupStep = Math.Max(0, _setupStep - 1)
                                   UpdateSetupText()
                               End Sub
        AddHandler nextStep.Click,
            Sub()
                Try
                    If Not _setupConfirmed.Checked Then Throw New InvalidOperationException("Check the confirmation after reviewing this step.")
                    Dim window = GetSelectedProcessWindowForEdition(BotEdition.Full)
                    If window Is Nothing OrElse window.MainWindowHandle = IntPtr.Zero Then Throw New InvalidOperationException("Select the game window in Vision first.")
                    Dim cfg = BuildFullConfig()
                    If _setupStep = 1 Then
                        Using frame = BotEngine.CaptureClient(window.MainWindowHandle)
                            If frame Is Nothing Then Throw New InvalidOperationException("Capture failed. Restore the game and check Capture Snapshot.")
                            For Each scanRegion As RectRegion In {cfg.HpBar, cfg.MpBar, cfg.MobNameRect, cfg.ChatRect}
                                If scanRegion Is Nothing OrElse scanRegion.W <= 0 OrElse scanRegion.H <= 0 OrElse scanRegion.X < 0 OrElse scanRegion.Y < 0 OrElse scanRegion.X + scanRegion.W > frame.Width OrElse scanRegion.Y + scanRegion.H > frame.Height Then Throw New InvalidOperationException("An essential region is outside the game image. Recalibrate Vision.")
                            Next
                        End Using
                    End If
                    If _setupStep >= 2 AndAlso (Not cfg.Actions.Any(Function(a) a.Enabled) OrElse cfg.Actions.Any(Function(a) a.Enabled AndAlso Not BotEngine.IsSupportedKeyName(a.KeyName))) Then Throw New InvalidOperationException("Enable at least one valid combat key and check its binding in the game.")
                    If _setupStep >= 2 Then
                        PushLiveConfig()
                        SavePersistedListState(True)
                        If _saveState.StartsWith("Save failed") OrElse _applyState.StartsWith("Apply failed") Then Throw New InvalidOperationException(_saveState & "; " & _applyState)
                        Directory.CreateDirectory(PersistDirectoryPath)
                        File.WriteAllText(Path.Combine(PersistDirectoryPath, "setup-complete.txt"), DateTime.UtcNow.ToString("O"))
                    End If
                    _setupStep = Math.Min(3, _setupStep + 1)
                    UpdateSetupText()
                Catch ex As Exception
                    _setupText.Text &= Environment.NewLine & ex.Message
                End Try
            End Sub
        flow.Controls.AddRange({_setupText, go, _setupConfirmed, nextStep, back})
        setup.Controls.Add(flow)
        sections.TabPages.Add(setup)
        _mainTabs.TabPages.Add(tab)
        UpdateSetupText()
        WatchSettingEdits(Me)
        If Not File.Exists(Path.Combine(PersistDirectoryPath, "setup-complete.txt")) Then
            AddHandler Shown, Sub()
                                  _mainTabs.SelectedTab = tab
                                  sections.SelectedTab = setup
                              End Sub
        End If
        _runtimeTimer = New System.Windows.Forms.Timer With {.Interval = 500}
        AddHandler _runtimeTimer.Tick, AddressOf RefreshRuntimeTools
        AddHandler FormClosed, Sub() _runtimeTimer.Dispose()
        _runtimeTimer.Start()
    End Sub

    Private Sub WatchSettingEdits(parent As Control)
        For Each child As Control In parent.Controls
            If TypeOf child Is DataGridView AndAlso Not DirectCast(child, DataGridView).ReadOnly Then
                AddHandler DirectCast(child, DataGridView).CellBeginEdit, Sub()
                                                                           _saveState = "Unsaved edit"
                                                                           _applyState = "Unapplied cell edit - finish editing to apply"
                                                                       End Sub
            End If
            WatchSettingEdits(child)
        Next
    End Sub

    Private Function RuntimeTextPage(tabs As TabControl, title As String) As TextBox
        Dim page As New TabPage(title)
        Dim text As New TextBox With {.Dock = DockStyle.Fill, .ReadOnly = True, .Multiline = True, .ScrollBars = ScrollBars.Both, .WordWrap = False, .Font = New Font("Consolas", 10)}
        page.Controls.Add(text)
        tabs.TabPages.Add(page)
        Return text
    End Function

    Private Sub UpdateSetupText()
        _setupConfirmed.Checked = False
        _setupText.Text = {
            "Step 1 of 3: Select the Full game window in Vision > Process List. Refresh the list and use Capture Snapshot to confirm the character. Return to Activity & Setup to continue.",
            "Step 2 of 3: In Vision, use Show Overlay and Capture Snapshot. Fit HP, MP, monster name and chat rectangles to the game. Keep the game resolution stable. Confirm the rectangles visually; validation also checks image boundaries.",
            "Step 3 of 3: In Combat Full, set enabled keys to match your in-game shortcuts. Check role, trigger and cooldown. Validate to apply and save. No keys are sent by this guide.",
            "Setup saved. Test a short combat session using Attack and Stop Bot. Reopen this guide after changing game resolution or character bindings. Back lets you review any step."
        }(_setupStep)
    End Sub

    Private Sub RefreshRuntimeTools(sender As Object, e As EventArgs)
        Dim status = If(_liteEngine.IsRunning(), _liteEngine.GetStatus(), _fullEngine.GetStatus())
        Dim reason = If(status.ErrorMessage <> "", status.ErrorMessage, If(status.NotAttackingReason <> "", status.NotAttackingReason, If(status.Running, "Running: " & status.LastAction, "Stopped: press Attack or Start when ready")))
        If _tradeRunning Then reason = _tradeStatus.Text
        If _tradeAnalyzing Then reason = "Waiting for AI listing analysis"
        If _resuRunning Then reason = _resuStatus.Text
        If _quizSolveInProgress Then reason = "Quiz: " & lblQuizStatus.Text
        If Not status.Running AndAlso Not _tradeRunning AndAlso Not _tradeAnalyzing AndAlso Not _resuRunning AndAlso Not _quizSolveInProgress Then reason = "Stopped: " & If(status.ErrorMessage <> "", status.ErrorMessage, "press Attack or Start when ready")
        _runtimeBanner.Text = reason
        _changesLabel.Text = _saveState & " | " & _applyState
        If _readingText.Visible Then
            _readingText.Text = "Confidence is shown only when supplied by a detector; Windows OCR does not provide calibrated confidence." & Environment.NewLine &
                $"Map localization: {status.MapLocalizationConfidence}%; coordinate confidence: {status.MapCoordinateConfidence}%" & Environment.NewLine &
                String.Join(Environment.NewLine, ReadingMonitor.Snapshot().Select(Function(item) $"{item.Key}: age {(DateTime.UtcNow - item.Value.At).TotalSeconds:0.0}s | confidence: {item.Value.Confidence} | {item.Value.Text}"))
        End If
        If _routePreview.Visible Then _routePreview.Invalidate()
        If Not _activityText.Visible Then Return
        _activityText.Text = String.Join(Environment.NewLine, RuntimeJournal.Snapshot().Select(Function(item) $"{item.At.ToLocalTime():HH:mm:ss.fff} [{item.Kind}] {item.Detail}"))
    End Sub

    Private Sub PaintRuntimeRoute(sender As Object, e As PaintEventArgs)
        Dim status = _fullEngine.GetStatus()
        e.Graphics.DrawString($"Localization confidence: {status.MapLocalizationConfidence}% | {status.NavigationTravelReason}", Font, Brushes.White, 12, 12)
        If _routeNodes.Count = 0 Then
            e.Graphics.DrawString("Select a recorded route in Leveling, then click Preview above.", Font, Brushes.White, 12, 45)
            Return
        End If
        Dim minX = _routeNodes.Min(Function(n) n.X), minY = _routeNodes.Min(Function(n) n.Y)
        Dim sx = Math.Max(1, _routePreview.Width - 100) / CDbl(Math.Max(1, _routeNodes.Max(Function(n) n.X) - minX))
        Dim sy = Math.Max(1, _routePreview.Height - 130) / CDbl(Math.Max(1, _routeNodes.Max(Function(n) n.Y) - minY))
        Dim scale = Math.Min(sx, sy)
        Dim points = _routeNodes.Select(Function(n) New PointF(CSng(45 + (n.X - minX) * scale), CSng(65 + (n.Y - minY) * scale))).ToList()
        For i = 0 To points.Count - 1
            If i > 0 Then
                Dim distance = Math.Sqrt(CDbl(_routeNodes(i).X - _routeNodes(i - 1).X) ^ 2 + CDbl(_routeNodes(i).Y - _routeNodes(i - 1).Y) ^ 2)
                Using pen As New Pen(If(distance > 100, Color.OrangeRed, Color.LightGreen), 2)
                    pen.CustomEndCap = New Drawing2D.AdjustableArrowCap(5, 6)
                    e.Graphics.DrawLine(pen, points(i - 1), points(i))
                End Using
                If distance > 100 Then e.Graphics.DrawString($"Jump {distance:0}", Font, Brushes.OrangeRed, points(i))
            End If
            e.Graphics.FillEllipse(Brushes.Gold, points(i).X - 4, points(i).Y - 4, 8, 8)
            e.Graphics.DrawString($"{i + 1}: {_routeNodes(i).X}/{_routeNodes(i).Y}", Font, Brushes.White, points(i).X + 6, points(i).Y + 6)
        Next
    End Sub
End Class
