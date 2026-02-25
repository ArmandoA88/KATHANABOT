Public Class Form1
    Private Shared ReadOnly PrimaryKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
    Private Shared ReadOnly FunctionKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}

    Private ReadOnly _engine As New BotEngine()
    Private ReadOnly _uiTimer As New Timer()

    Private txtWindowTitle As TextBox
    Private nudLoopMs As NumericUpDown
    Private nudRetargetMs As NumericUpDown
    Private nudMobHpThreshold As NumericUpDown
    Private btnOverlayToggle As Button
    Private dgvRegions As DataGridView
    Private picSnapshot As PictureBox

    Private dgvCombat As DataGridView
    Private chkMonsterFilter As CheckBox
    Private lstMonsterFilter As ListBox
    Private txtMonsterName As TextBox

    Private lblState As Label
    Private lblSystem As Label
    Private lblVitals As Label
    Private lblMobName As Label
    Private btnAttack As Button
    Private btnSaveSettings As Button
    Private btnStopBot As Button
    Private btnBypassLimits As Button
    Private btnBypassStuck As Button
    Private btnRetargetNow As Button
    Private rtbLog As RichTextBox
    Private txtDiagnostics As TextBox

    Private nudAutoPotHp As NumericUpDown
    Private nudAutoPotMp As NumericUpDown

    Private _lastAction As String = ""
    Private _lastState As String = ""
    Private _lastError As String = ""
    Private _lastNoAttackReason As String = ""
    Private _bypassHpMpLimits As Boolean = False
    Private _bypassStuckTarget As Boolean = False
    Private _overlayForm As CalibrationOverlayForm
    Private _autoStarted As Boolean = False

    Public Sub New()
        InitializeComponent()
        BuildUi()
        SeedDefaults()
        ApplyDarkTheme(Me)

        AddHandler _engine.StatusUpdated, AddressOf OnEngineStatusUpdated
        AddHandler _engine.LogLine, AddressOf OnEngineLogLine

        _uiTimer.Interval = 1000
        AddHandler _uiTimer.Tick, AddressOf UiTimerTick
        _uiTimer.Start()
    End Sub

    Private Sub BuildUi()
        Text = "KATHANA GAMEBOT"
        Width = 1450
        Height = 900
        BackColor = Color.FromArgb(25, 25, 25)
        ForeColor = Color.Gainsboro

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)}
        Controls.Add(tabs)

        tabs.TabPages.Add(BuildCombatTab())
        tabs.TabPages.Add(BuildVisionTab())
        tabs.TabPages.Add(BuildAutoPotTab())
        tabs.TabPages.Add(BuildUnstuckTab())
        tabs.TabPages.Add(BuildDiagnosticsTab())
    End Sub

    Private Function BuildCombatTab() As TabPage
        Dim tab As New TabPage("Combat") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Padding = New Padding(8)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 18.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        tab.Controls.Add(root)

        Dim left As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 72.0F))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 28.0F))
        left.Controls.Add(BuildCombatSkillsGroup(), 0, 0)
        left.Controls.Add(BuildMonsterFilterGroup(), 0, 1)

        root.Controls.Add(left, 0, 0)
        root.Controls.Add(BuildCenterControlPanel(), 1, 0)
        root.Controls.Add(BuildLogPanel(), 2, 0)
        Return tab
    End Function

    Private Function BuildVisionTab() As TabPage
        Dim tab As New TabPage("Vision") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(8)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42.0F))
        tab.Controls.Add(root)

        Dim left As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, 220.0F))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim generalGroup As New GroupBox() With {.Text = "Vision + Window Setup", .Dock = DockStyle.Fill}
        Dim generalLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 4}
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        generalLayout.Controls.Add(New Label() With {.Text = "Window Title", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        txtWindowTitle = New TextBox() With {.Dock = DockStyle.Fill}
        generalLayout.Controls.Add(txtWindowTitle, 1, 0)
        generalLayout.SetColumnSpan(txtWindowTitle, 3)

        generalLayout.Controls.Add(New Label() With {.Text = "Loop (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudLoopMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 20, .Maximum = 1000, .Value = 80}
        generalLayout.Controls.Add(nudLoopMs, 1, 1)

        generalLayout.Controls.Add(New Label() With {.Text = "Retarget (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 1)
        nudRetargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 700}
        generalLayout.Controls.Add(nudRetargetMs, 3, 1)

        generalLayout.Controls.Add(New Label() With {.Text = "Mob HP Presence %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudMobHpThreshold = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 0.1D, .Maximum = 100, .DecimalPlaces = 1, .Increment = 0.1D, .Value = 1.0D}
        generalLayout.Controls.Add(nudMobHpThreshold, 1, 2)

        btnOverlayToggle = New Button() With {.Text = "Show Overlay", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White}
        AddHandler btnOverlayToggle.Click, AddressOf ToggleOverlayClicked
        generalLayout.Controls.Add(btnOverlayToggle, 2, 2)

        Dim btnCaptureSnapshot As New Button() With {.Text = "Capture Snapshot", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(30, 80, 120), .ForeColor = Color.White}
        AddHandler btnCaptureSnapshot.Click, AddressOf SnapshotClicked
        generalLayout.Controls.Add(btnCaptureSnapshot, 3, 2)

        Dim hint As New Label() With {.Text = "Mob HP Presence % = minimum red-fill detected in Mob HP bar. Lower value = easier target detection.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}
        generalLayout.Controls.Add(hint, 0, 3)
        generalLayout.SetColumnSpan(hint, 4)

        generalGroup.Controls.Add(generalLayout)
        left.Controls.Add(generalGroup, 0, 0)

        Dim regionGroup As New GroupBox() With {.Text = "Calibration Regions (client coordinates)", .Dock = DockStyle.Fill}
        dgvRegions = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Region", .ReadOnly = True})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "X"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Y"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "W"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "H"})
        regionGroup.Controls.Add(dgvRegions)
        left.Controls.Add(regionGroup, 0, 1)

        root.Controls.Add(left, 0, 0)

        Dim snapshotGroup As New GroupBox() With {.Text = "Snapshot", .Dock = DockStyle.Fill}
        picSnapshot = New PictureBox() With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Black}
        snapshotGroup.Controls.Add(picSnapshot)
        root.Controls.Add(snapshotGroup, 1, 0)

        Return tab
    End Function

    Private Function BuildAutoPotTab() As TabPage
        Dim tab As New TabPage("Auto-Pot") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim group As New GroupBox() With {.Text = "Quick Pot Thresholds", .Dock = DockStyle.Top, .Height = 190, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 3}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        layout.Controls.Add(New Label() With {.Text = "Heal Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        nudAutoPotHp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 45, .Dock = DockStyle.Fill}
        layout.Controls.Add(nudAutoPotHp, 1, 0)

        layout.Controls.Add(New Label() With {.Text = "Mana Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudAutoPotMp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 35, .Dock = DockStyle.Fill}
        layout.Controls.Add(nudAutoPotMp, 1, 1)

        Dim btnApply As New Button() With {.Text = "Apply To Heal/Mana Rows", .Width = 220, .Height = 34, .BackColor = Color.FromArgb(42, 120, 80), .ForeColor = Color.White}
        AddHandler btnApply.Click, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds()
        layout.Controls.Add(btnApply, 1, 2)

        Dim note As New Label() With {.Text = "Updates Trigger% values for Role=heal and Role=mana rows in Combat tab.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        layout.Controls.Add(note, 2, 0)
        layout.SetRowSpan(note, 3)
        group.Controls.Add(layout)
        tab.Controls.Add(group)
        Return tab
    End Function

    Private Function BuildUnstuckTab() As TabPage
        Dim tab As New TabPage("Unstuck") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim group As New GroupBox() With {.Text = "Unstuck / Retarget", .Dock = DockStyle.Top, .Height = 160}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 2}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        layout.Controls.Add(New Label() With {.Text = "Retarget Key", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        layout.Controls.Add(New Label() With {.Text = "E", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}, 1, 0)
        layout.Controls.Add(New Label() With {.Text = "Retarget Interval (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)

        Dim localRetarget As New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 700}
        layout.Controls.Add(localRetarget, 1, 1)

        Dim btnApply As New Button() With {.Text = "Use This Interval", .Width = 160, .BackColor = Color.FromArgb(45, 85, 135), .ForeColor = Color.White}
        AddHandler btnApply.Click, Sub(_s As Object, _e As EventArgs)
                                       nudRetargetMs.Value = localRetarget.Value
                                       AppendLog("Updated retarget interval from Unstuck tab.")
                                   End Sub
        layout.Controls.Add(btnApply, 2, 1)
        group.Controls.Add(layout)
        tab.Controls.Add(group)
        Return tab
    End Function

    Private Function BuildDiagnosticsTab() As TabPage
        Dim tab As New TabPage("Diagnostics") With {.BackColor = Color.FromArgb(20, 20, 20)}
        txtDiagnostics = New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Both, .ReadOnly = True, .Font = New Font("Consolas", 9.5F, FontStyle.Regular), .BackColor = Color.FromArgb(10, 10, 10), .ForeColor = Color.LightGray}
        tab.Controls.Add(txtDiagnostics)
        Return tab
    End Function

    Private Function BuildCombatSkillsGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Combat Skills", .Dock = DockStyle.Fill}
        dgvCombat = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvCombat.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled"})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Key", .ReadOnly = True, .FillWeight = 60.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CooldownSec", .FillWeight = 90.0F})
        Dim roleColumn As New DataGridViewComboBoxColumn() With {.Name = "Role", .FillWeight = 80.0F}
        roleColumn.Items.AddRange(New Object() {"attack", "heal", "mana", "special"})
        dgvCombat.Columns.Add(roleColumn)
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Priority", .FillWeight = 75.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "TriggerPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinHpPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinMpPercent", .FillWeight = 85.0F})
        group.Controls.Add(dgvCombat)
        Return group
    End Function

    Private Function BuildMonsterFilterGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Monster Filter", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 35.0F))
        group.Controls.Add(layout)

        chkMonsterFilter = New CheckBox() With {.Text = "Enable Monster Filter", .Dock = DockStyle.Fill, .Checked = True}
        layout.Controls.Add(chkMonsterFilter, 0, 0)

        lstMonsterFilter = New ListBox() With {.Dock = DockStyle.Fill}
        layout.Controls.Add(lstMonsterFilter, 0, 1)

        Dim actionRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        txtMonsterName = New TextBox() With {.Width = 180}
        Dim btnAddMonster As New Button() With {.Text = "Add", .Width = 70}
        Dim btnRemoveMonster As New Button() With {.Text = "Remove", .Width = 80}
        AddHandler btnAddMonster.Click, AddressOf AddMonsterClicked
        AddHandler btnRemoveMonster.Click, AddressOf RemoveMonsterClicked
        actionRow.Controls.Add(txtMonsterName)
        actionRow.Controls.Add(btnAddMonster)
        actionRow.Controls.Add(btnRemoveMonster)
        layout.Controls.Add(actionRow, 0, 2)
        Return group
    End Function

    Private Function BuildCenterControlPanel() As Panel
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12)}
        lblState = New Label() With {.Text = "Status: Searching for target...", .Top = 16, .Left = 8, .Width = 260, .Height = 22}
        lblSystem = New Label() With {.Text = "System Active: False", .Top = 44, .Left = 8, .Width = 260, .Height = 22, .ForeColor = Color.LightGreen}
        lblVitals = New Label() With {.Text = "HP%: 0   MP%: 0", .Top = 72, .Left = 8, .Width = 260, .Height = 22}
        lblMobName = New Label() With {.Text = "Mob: (none)", .Top = 96, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.LightSkyBlue}
        btnAttack = New Button() With {.Text = "Attack", .Top = 126, .Left = 8, .Width = 210, .Height = 42, .BackColor = Color.FromArgb(40, 180, 80), .ForeColor = Color.White}
        btnSaveSettings = New Button() With {.Text = "Save Settings", .Top = 180, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(55, 55, 55), .ForeColor = Color.White}
        btnStopBot = New Button() With {.Text = "Stop Bot", .Top = 230, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(20, 130, 210), .ForeColor = Color.White}
        btnBypassLimits = New Button() With {.Text = "Bypass HP/MP Limits: OFF", .Top = 280, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        btnBypassStuck = New Button() With {
            .Text = If(_bypassStuckTarget, "Bypass Stuck Target: ON", "Bypass Stuck Target: OFF"),
            .Top = 330,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnRetargetNow = New Button() With {.Text = "Retarget Now (E)", .Top = 380, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        AddHandler btnAttack.Click, AddressOf StartClicked
        AddHandler btnSaveSettings.Click, AddressOf SaveClicked
        AddHandler btnStopBot.Click, AddressOf StopClicked
        AddHandler btnBypassLimits.Click, AddressOf ToggleBypassLimitsClicked
        AddHandler btnBypassStuck.Click, AddressOf ToggleStuckTargetBypassClicked
        AddHandler btnRetargetNow.Click, AddressOf ManualRetargetClicked
        panel.Controls.Add(lblState)
        panel.Controls.Add(lblSystem)
        panel.Controls.Add(lblVitals)
        panel.Controls.Add(lblMobName)
        panel.Controls.Add(btnAttack)
        panel.Controls.Add(btnSaveSettings)
        panel.Controls.Add(btnStopBot)
        panel.Controls.Add(btnBypassLimits)
        panel.Controls.Add(btnBypassStuck)
        panel.Controls.Add(btnRetargetNow)
        Return panel
    End Function

    Private Function BuildLogPanel() As GroupBox
        Dim group As New GroupBox() With {.Text = "Bot Debug Log - Real-time", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        rtbLog = New RichTextBox() With {.Dock = DockStyle.Fill, .ReadOnly = True, .BackColor = Color.Black, .ForeColor = Color.FromArgb(70, 255, 160), .Font = New Font("Consolas", 9.0F, FontStyle.Regular), .ScrollBars = RichTextBoxScrollBars.Vertical}
        layout.Controls.Add(rtbLog, 0, 0)
        Dim btnClearLog As New Button() With {.Text = "Clear Log", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(130, 25, 25), .ForeColor = Color.White}
        AddHandler btnClearLog.Click, Sub(_s As Object, _e As EventArgs) rtbLog.Clear()
        layout.Controls.Add(btnClearLog, 0, 1)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Sub SeedDefaults()
        txtWindowTitle.Text = "Kathana - The Coming of the Dark Ages"
        dgvRegions.Rows.Add("hp_bar", "11", "25", "151", "11")
        dgvRegions.Rows.Add("mp_bar", "3", "40", "161", "11")
        dgvRegions.Rows.Add("mob_name_rect", "862", "0", "162", "23")
        dgvRegions.Rows.Add("mob_hp_rect", "859", "20", "165", "11")
        nudMobHpThreshold.Value = 1.0D

        Dim keyIndex As Integer = 1
        For Each key In PrimaryKeys
            Dim enabled As Boolean = (key = "1" OrElse key = "6")
            Dim role As String = If(key = "6", "heal", "attack")
            Dim trigger As Integer = If(key = "6", 75, 40)
            dgvCombat.Rows.Add(enabled, key, "1.0", role, keyIndex * 10, trigger, 1, 1)
            keyIndex += 1
        Next
        For Each key In FunctionKeys
            dgvCombat.Rows.Add(False, key, "1.0", "special", keyIndex * 10, 40, 1, 1)
            keyIndex += 1
        Next
        UpdateAttackButtonAppearance(False)
        AppendLog("UI loaded. No API required.")
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        _engine.UpdateConfig(BuildConfig())
        AppendLog("Settings saved to in-app engine.")
    End Sub

    Private Sub StartClicked(sender As Object, e As EventArgs)
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
            _overlayForm = Nothing
            btnOverlayToggle.Text = "Show Overlay"
            AppendLog("Overlay hidden while bot is running.")
        End If

        _engine.UpdateConfig(BuildConfig())
        _engine.Start()
        UpdateAttackButtonAppearance(True)
    End Sub

    Private Sub AutoStartOnLaunch()
        If _autoStarted Then
            Return
        End If
        _autoStarted = True
        If _engine.IsRunning() Then
            Return
        End If
        _engine.UpdateConfig(BuildConfig())
        _engine.Start()
        UpdateAttackButtonAppearance(True)
        AppendLog("Auto-start on launch enabled.")
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        AutoStartOnLaunch()
    End Sub

    Private Sub StopClicked(sender As Object, e As EventArgs)
        _engine.Stop()
        UpdateAttackButtonAppearance(False)
    End Sub

    Private Sub SnapshotClicked(sender As Object, e As EventArgs)
        _engine.UpdateConfig(BuildConfig())
        Dim bmp As Bitmap = _engine.CaptureSnapshot()
        If bmp Is Nothing Then
            AppendLog("Snapshot failed. Window not found or capture failed.")
            Return
        End If

        Dim oldImage = picSnapshot.Image
        picSnapshot.Image = bmp
        If oldImage IsNot Nothing Then
            oldImage.Dispose()
        End If
        AppendLog("Snapshot captured.")
    End Sub

    Private Sub ToggleBypassLimitsClicked(sender As Object, e As EventArgs)
        _bypassHpMpLimits = Not _bypassHpMpLimits
        btnBypassLimits.Text = If(_bypassHpMpLimits, "Bypass HP/MP Limits: ON", "Bypass HP/MP Limits: OFF")
        btnBypassLimits.BackColor = If(_bypassHpMpLimits, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        _engine.UpdateConfig(BuildConfig())
        AppendLog(If(_bypassHpMpLimits, "Bypass HP/MP limits enabled.", "Bypass HP/MP limits disabled."))
    End Sub

    Private Sub ToggleStuckTargetBypassClicked(sender As Object, e As EventArgs)
        _bypassStuckTarget = Not _bypassStuckTarget
        btnBypassStuck.Text = If(_bypassStuckTarget, "Bypass Stuck Target: ON", "Bypass Stuck Target: OFF")
        btnBypassStuck.BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        _engine.UpdateConfig(BuildConfig())
        AppendLog(If(_bypassStuckTarget, "Stuck target bypass enabled.", "Stuck target bypass disabled."))
    End Sub

    Private Sub ManualRetargetClicked(sender As Object, e As EventArgs)
        Dim title As String = txtWindowTitle.Text.Trim()
        If title = "" Then
            AppendLog("Manual retarget failed: window title is empty.")
            Return
        End If

        If _engine.ManualRetarget(title) Then
            AppendLog("Manual retarget requested (E sent).")
        Else
            AppendLog("Manual retarget failed: game window not found.")
        End If
    End Sub

    Private Sub ToggleOverlayClicked(sender As Object, e As EventArgs)
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
            _overlayForm = Nothing
            btnOverlayToggle.Text = "Show Overlay"
            AppendLog("Calibration overlay hidden.")
            Return
        End If

        _overlayForm = New CalibrationOverlayForm(Function() BuildConfig())
        AddHandler _overlayForm.OverlayRegionChanged, AddressOf OverlayRegionChanged
        AddHandler _overlayForm.OverlayRegionCommitted, AddressOf OverlayRegionCommitted
        AddHandler _overlayForm.FormClosed,
            Sub(_s As Object, _e As FormClosedEventArgs)
                _overlayForm = Nothing
                If btnOverlayToggle IsNot Nothing AndAlso Not btnOverlayToggle.IsDisposed Then
                    btnOverlayToggle.Text = "Show Overlay"
                End If
            End Sub
        _overlayForm.Show(Me)
        btnOverlayToggle.Text = "Hide Overlay"
        AppendLog("Calibration overlay shown.")
    End Sub

    Private Sub OverlayRegionChanged(regionName As String, region As RectRegion)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String, RectRegion)(AddressOf OverlayRegionChanged), regionName, region)
            Return
        End If

        UpdateRegionGridRow(regionName, region)
        _engine.UpdateConfig(BuildConfig())
    End Sub

    Private Sub OverlayRegionCommitted(regionName As String, region As RectRegion)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String, RectRegion)(AddressOf OverlayRegionCommitted), regionName, region)
            Return
        End If

        UpdateRegionGridRow(regionName, region)
        _engine.UpdateConfig(BuildConfig())
        AppendLog($"Overlay updated {regionName}: x={region.X}, y={region.Y}, w={region.W}, h={region.H}")
    End Sub

    Private Sub UpdateRegionGridRow(regionName As String, region As RectRegion)
        For Each row As DataGridViewRow In dgvRegions.Rows
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                row.Cells("X").Value = region.X.ToString()
                row.Cells("Y").Value = region.Y.ToString()
                row.Cells("W").Value = region.W.ToString()
                row.Cells("H").Value = region.H.ToString()
                Exit For
            End If
        Next
    End Sub

    Private Sub UiTimerTick(sender As Object, e As EventArgs)
        Dim st As BotStatus = _engine.GetStatus()
        txtDiagnostics.Text =
            $"Running: {st.Running}{Environment.NewLine}" &
            $"BypassHpMpLimits: {_bypassHpMpLimits}{Environment.NewLine}" &
            $"BypassStuckTarget: {_bypassStuckTarget}{Environment.NewLine}" &
            $"Window Found: {st.WindowFound}{Environment.NewLine}" &
            $"HP%: {st.HpPercent:0.0}{Environment.NewLine}" &
            $"MP%: {st.MpPercent:0.0}{Environment.NewLine}" &
            $"MobName: {st.MobName}{Environment.NewLine}" &
            $"OcrError: {OcrReader.LastError()}{Environment.NewLine}" &
            $"MobHP%: {st.MobHpPercent:0.0}{Environment.NewLine}" &
            $"TargetValid: {st.TargetValid}{Environment.NewLine}" &
            $"LastAction: {st.LastAction}{Environment.NewLine}" &
            $"NotAttackingReason: {st.NotAttackingReason}{Environment.NewLine}" &
            $"Error: {st.ErrorMessage}"
    End Sub

    Private Sub OnEngineStatusUpdated(status As BotStatus)
        If InvokeRequired Then
            BeginInvoke(New Action(Of BotStatus)(AddressOf OnEngineStatusUpdated), status)
            Return
        End If

        Dim statusText As String
        If status.ErrorMessage <> "" Then
            statusText = "Status: " & status.ErrorMessage
        ElseIf status.NotAttackingReason <> "" Then
            statusText = "Status: " & status.NotAttackingReason
        Else
            statusText = "Status: Attacking target..."
        End If

        lblState.Text = statusText
        lblSystem.Text = $"System Active: {status.Running}"
        lblVitals.Text = $"HP%: {status.HpPercent:0.0}    MP%: {status.MpPercent:0.0}"
        lblMobName.Text = $"Mob: {If(String.IsNullOrWhiteSpace(status.MobName), "(none)", status.MobName)}"
        UpdateAttackButtonAppearance(status.Running)

        If status.LastAction <> "" AndAlso status.LastAction <> _lastAction Then
            AppendLog("Key action: " & status.LastAction)
            _lastAction = status.LastAction
        End If
        If statusText <> _lastState Then
            AppendLog(statusText.Replace("Status: ", "State changed to: "))
            _lastState = statusText
        End If
        If status.ErrorMessage <> "" AndAlso status.ErrorMessage <> _lastError Then
            AppendLog("Warning: " & status.ErrorMessage)
            _lastError = status.ErrorMessage
        End If
        If status.NotAttackingReason <> "" AndAlso status.NotAttackingReason <> _lastNoAttackReason Then
            AppendLog("No attack reason: " & status.NotAttackingReason)
            _lastNoAttackReason = status.NotAttackingReason
        ElseIf status.NotAttackingReason = "" Then
            _lastNoAttackReason = ""
        End If
    End Sub

    Private Sub OnEngineLogLine(line As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf OnEngineLogLine), line)
            Return
        End If
        AppendLog(line)
    End Sub

    Private Sub AddMonsterClicked(sender As Object, e As EventArgs)
        Dim name As String = txtMonsterName.Text.Trim()
        If String.IsNullOrWhiteSpace(name) Then
            Return
        End If
        If Not MonsterExists(name) Then
            lstMonsterFilter.Items.Add(name)
            AppendLog("Monster filter added: " & name)
        End If
        txtMonsterName.Text = ""
    End Sub

    Private Sub RemoveMonsterClicked(sender As Object, e As EventArgs)
        If lstMonsterFilter.SelectedItem Is Nothing Then
            Return
        End If
        Dim removed As String = lstMonsterFilter.SelectedItem.ToString()
        lstMonsterFilter.Items.Remove(lstMonsterFilter.SelectedItem)
        AppendLog("Monster filter removed: " & removed)
    End Sub

    Private Function MonsterExists(name As String) As Boolean
        For Each item In lstMonsterFilter.Items
            If String.Equals(item.ToString(), name, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Sub ApplyQuickAutoPotThresholds()
        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim role As String = SafeCell(row, "Role", "attack").ToLowerInvariant()
            If role = "heal" Then
                row.Cells("TriggerPercent").Value = CInt(nudAutoPotHp.Value).ToString()
            ElseIf role = "mana" Then
                row.Cells("TriggerPercent").Value = CInt(nudAutoPotMp.Value).ToString()
            End If
        Next
        AppendLog("Applied auto-pot thresholds to heal/mana rows.")
    End Sub

    Private Function BuildConfig() As BotConfig
        Dim cfg As New BotConfig()
        cfg.WindowTitle = txtWindowTitle.Text.Trim()
        cfg.LoopMs = CInt(nudLoopMs.Value)
        cfg.RetargetMs = CInt(nudRetargetMs.Value)
        cfg.MobHpPresenceThreshold = CDbl(nudMobHpThreshold.Value)
        cfg.BypassHpMpLimits = _bypassHpMpLimits
        cfg.BypassStuckTarget = _bypassStuckTarget
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.MobNameRect = BuildRect("mob_name_rect")
        cfg.MobHpRect = BuildRect("mob_hp_rect")

        cfg.DeniedMobs.Clear()
        If chkMonsterFilter.Checked Then
            For Each item In lstMonsterFilter.Items
                cfg.DeniedMobs.Add(item.ToString().Trim().ToLowerInvariant())
            Next
        End If

        cfg.Actions = BuildActionList()
        Return cfg
    End Function

    Private Function BuildActionList() As List(Of ActionRule)
        Dim actions As New List(Of ActionRule)()
        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim enabled As Boolean = False
            Try
                enabled = Convert.ToBoolean(row.Cells("Enabled").Value)
            Catch
            End Try

            Dim cooldownSec As Double = Math.Max(0.05, ParseDouble(SafeCell(row, "CooldownSec", "1.0"), 1.0))
            actions.Add(New ActionRule With {
                .KeyName = SafeCell(row, "Key", "").ToUpperInvariant(),
                .Enabled = enabled,
                .Role = SafeCell(row, "Role", "attack").ToLowerInvariant(),
                .Priority = ParseInt(SafeCell(row, "Priority", "100"), 100),
                .CooldownMs = CInt(Math.Round(cooldownSec * 1000.0)),
                .TriggerPercent = Math.Min(99, Math.Max(1, ParseInt(SafeCell(row, "TriggerPercent", "40"), 40))),
                .MinHpPercent = Math.Min(100, Math.Max(1, ParseInt(SafeCell(row, "MinHpPercent", "1"), 1))),
                .MinMpPercent = Math.Min(100, Math.Max(1, ParseInt(SafeCell(row, "MinMpPercent", "1"), 1)))
            })
        Next
        Return actions
    End Function

    Private Function BuildRect(regionName As String) As RectRegion
        For Each row As DataGridViewRow In dgvRegions.Rows
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                Return New RectRegion(
                    ParseInt(SafeCell(row, "X", "0"), 0),
                    ParseInt(SafeCell(row, "Y", "0"), 0),
                    Math.Max(1, ParseInt(SafeCell(row, "W", "1"), 1)),
                    Math.Max(1, ParseInt(SafeCell(row, "H", "1"), 1)))
            End If
        Next
        Return New RectRegion(0, 0, 1, 1)
    End Function

    Private Function SafeCell(row As DataGridViewRow, column As String, fallback As String) As String
        Try
            Return If(row.Cells(column).Value, fallback).ToString().Trim()
        Catch
            Return fallback
        End Try
    End Function

    Private Function ParseInt(textValue As String, fallback As Integer) As Integer
        Dim value As Integer
        If Integer.TryParse(textValue, value) Then
            Return value
        End If
        Return fallback
    End Function

    Private Function ParseDouble(textValue As String, fallback As Double) As Double
        Dim value As Double
        If Double.TryParse(textValue, value) Then
            Return value
        End If
        Return fallback
    End Function

    Private Sub AppendLog(message As String)
        Dim stamp As String = DateTime.Now.ToString("HH:mm:ss")
        rtbLog.AppendText($"[{stamp}] {message}{Environment.NewLine}")
        rtbLog.SelectionStart = rtbLog.TextLength
        rtbLog.ScrollToCaret()
    End Sub

    Private Sub UpdateAttackButtonAppearance(isRunning As Boolean)
        If btnAttack Is Nothing Then
            Return
        End If

        If isRunning Then
            btnAttack.Text = "ATTACKING"
            btnAttack.BackColor = Color.FromArgb(220, 70, 55)
            btnAttack.ForeColor = Color.White
        Else
            btnAttack.Text = "Attack"
            btnAttack.BackColor = Color.FromArgb(40, 180, 80)
            btnAttack.ForeColor = Color.White
        End If
    End Sub

    Private Sub ApplyDarkTheme(control As Control)
        control.BackColor = Color.FromArgb(28, 28, 28)
        control.ForeColor = Color.Gainsboro

        If TypeOf control Is GroupBox Then
            control.BackColor = Color.FromArgb(20, 20, 20)
            control.ForeColor = Color.FromArgb(80, 170, 255)
        ElseIf TypeOf control Is TabPage Then
            control.BackColor = Color.FromArgb(20, 20, 20)
            control.ForeColor = Color.Gainsboro
        ElseIf TypeOf control Is TextBox Then
            Dim tb = CType(control, TextBox)
            If Not tb.ReadOnly Then
                tb.BackColor = Color.FromArgb(35, 35, 35)
                tb.ForeColor = Color.Gainsboro
            End If
        ElseIf TypeOf control Is ListBox Then
            control.BackColor = Color.FromArgb(10, 10, 10)
            control.ForeColor = Color.Gainsboro
        ElseIf TypeOf control Is DataGridView Then
            Dim grid = CType(control, DataGridView)
            grid.BackgroundColor = Color.FromArgb(15, 15, 15)
            grid.EnableHeadersVisualStyles = False
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35)
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            grid.DefaultCellStyle.BackColor = Color.FromArgb(18, 18, 18)
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 95, 150)
            grid.DefaultCellStyle.SelectionForeColor = Color.White
            grid.GridColor = Color.FromArgb(45, 45, 45)
        End If

        For Each child As Control In control.Controls
            ApplyDarkTheme(child)
        Next
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _uiTimer.Stop()
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
        End If
        _engine.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
