Imports System.Media
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.IO

Public Class Form1
    Private Shared ReadOnly PrimaryKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
    Private Shared ReadOnly FunctionKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}

    Private ReadOnly _engine As New BotEngine()
    Private ReadOnly _uiTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _enterToggleTimer As New System.Windows.Forms.Timer()

    Private txtWindowTitle As TextBox
    Private nudLoopMs As NumericUpDown
    Private nudRetargetMs As NumericUpDown
    Private nudMobHpThreshold As NumericUpDown
    Private btnOverlayToggle As Button
    Private dgvRegions As DataGridView
    Private picSnapshot As PictureBox

    Private dgvCombat As DataGridView
    Private chkMonsterFilter As CheckBox
    Private chkLootPickup As CheckBox
    Private nudLootPickupSeconds As NumericUpDown
    Private lstMonsterFilter As ListBox
    Private lstLootFilter As ListBox
    Private txtMonsterName As TextBox
    Private txtLootName As TextBox

    Private lblState As Label
    Private lblSystem As Label
    Private lblHp As Label
    Private lblMp As Label
    Private lblMobName As Label
    Private lblExpRate As Label
    Private btnAttack As Button
    Private btnSaveSettings As Button
    Private btnStopBot As Button
    Private btnBypassLimits As Button
    Private btnBypassStuck As Button
    Private btnRetargetNow As Button
    Private btnPartyAutoAccept As Button
    Private rtbLog As RichTextBox
    Private dgvKeySummary As DataGridView
    Private lblKeySummaryInfo As Label
    Private txtDiagnostics As TextBox
    Private pnlHealthBanner As Panel

    Private nudAutoPotHp As NumericUpDown
    Private nudAutoPotMp As NumericUpDown
    Private nudAlarmVolume As NumericUpDown
    Private txtNtfyTopic As TextBox

    Private _lastAction As String = ""
    Private _lastState As String = ""
    Private _lastError As String = ""
    Private _lastNoAttackReason As String = ""
    Private _bypassHpMpLimits As Boolean = False
    Private _bypassStuckTarget As Boolean = True
    Private _partyAutoAccept As Boolean = True
    Private _overlayForm As CalibrationOverlayForm
    Private _autoStarted As Boolean = False
    Private _alarmVolumePercent As Integer = 85
    Private _hpZeroAlarmActive As Boolean = False
    Private _hpZeroPending As Boolean = False
    Private _hpAlarmCts As CancellationTokenSource = Nothing
    Private _hpAlarmTask As Task = Nothing
    Private _hpPendingCts As CancellationTokenSource = Nothing
    Private _hpPendingTask As Task = Nothing
    Private _lastHpZeroNotification As DateTime = DateTime.MinValue
    Private _enterWasDown As Boolean = False
    Private Const HpZeroAlarmGraceMs As Integer = 60000
    Private Const DefaultNtfyTopicName As String = "Katana12345"
    Private Shared ReadOnly NtfyClient As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(7)}
    Private Shared ReadOnly PersistDirectoryPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotControlPanel")
    Private Shared ReadOnly PersistFilePath As String = Path.Combine(PersistDirectoryPath, "user_lists.json")
    Private ReadOnly _baseBackColors As New Dictionary(Of Control, Color)()
    Private ReadOnly _gridThemeSnapshots As New Dictionary(Of DataGridView, GridThemeSnapshot)()
    Private ReadOnly _keyActionEvents As New List(Of KeyActionEvent)()

    Private Class GridThemeSnapshot
        Public Property BackgroundColor As Color
        Public Property HeaderBackColor As Color
        Public Property HeaderForeColor As Color
        Public Property DefaultBackColor As Color
        Public Property DefaultForeColor As Color
        Public Property SelectionBackColor As Color
        Public Property SelectionForeColor As Color
        Public Property GridColor As Color
    End Class

    Private Class KeyActionEvent
        Public Property TimestampUtc As DateTime
        Public Property KeyName As String = ""
        Public Property ActionText As String = ""
    End Class

    Private Class KeyActionSummaryRow
        Public Property KeyName As String = ""
        Public Property Last10Min As Integer
        Public Property Last30Min As Integer
        Public Property Last60Min As Integer
        Public Property LastActionText As String = ""
    End Class

    Private Class PersistedListState
        Public Property MonsterFilterEnabled As Boolean = True
        Public Property LootPickupEnabled As Boolean = False
        Public Property LootPickupSeconds As Decimal = 4D
        Public Property PromptAutoAcceptEnabled As Boolean = True
        Public Property MonsterNames As List(Of String) = New List(Of String)()
        Public Property LootNames As List(Of String) = New List(Of String)()
        Public Property CombatActions As List(Of PersistedCombatAction) = New List(Of PersistedCombatAction)()
    End Class

    Private Class PersistedCombatAction
        Public Property ActionKey As String = ""
        Public Property Enabled As Boolean = True
        Public Property Role As String = "attack"
        Public Property Priority As Integer = 100
        Public Property CooldownSec As Double = 1.0
        Public Property TriggerPercent As Integer = 40
        Public Property MinHpPercent As Integer = 1
        Public Property MinMpPercent As Integer = 1
    End Class

    <DllImport("winmm.dll")>
    Private Shared Function waveOutGetVolume(hwo As IntPtr, ByRef dwVolume As UInteger) As Integer
    End Function

    <DllImport("winmm.dll")>
    Private Shared Function waveOutSetVolume(hwo As IntPtr, dwVolume As UInteger) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(vKey As Integer) As Short
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetWindowText(hWnd As IntPtr, lpString As StringBuilder, nMaxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function

    Public Sub New()
        InitializeComponent()
        BuildUi()
        SeedDefaults()
        LoadPersistedListState()
        SetupLiveConfigBindings()
        ApplyDarkTheme(Me)

        AddHandler _engine.StatusUpdated, AddressOf OnEngineStatusUpdated
        AddHandler _engine.LogLine, AddressOf OnEngineLogLine

        _uiTimer.Interval = 1000
        AddHandler _uiTimer.Tick, AddressOf UiTimerTick
        _uiTimer.Start()

        _enterToggleTimer.Interval = 45
        AddHandler _enterToggleTimer.Tick, AddressOf EnterToggleTimerTick
        _enterToggleTimer.Start()
    End Sub

    Private Sub SetupLiveConfigBindings()
        AddHandler txtWindowTitle.TextChanged, AddressOf LiveConfigChanged
        If txtNtfyTopic IsNot Nothing Then
            AddHandler txtNtfyTopic.TextChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudLoopMs.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudRetargetMs.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudMobHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudAutoPotHp.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudAutoPotMp.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudAlarmVolume.ValueChanged, AddressOf LiveConfigChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvCombat.CellValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvCombat.CellEndEdit, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellEndEdit, AddressOf LiveConfigChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        AddHandler dgvCombat.CurrentCellDirtyStateChanged,
            Sub(_s As Object, _e As EventArgs)
                If dgvCombat.IsCurrentCellDirty Then
                    dgvCombat.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
            End Sub
    End Sub

    Private Sub LiveConfigChanged(_sender As Object, _e As EventArgs)
        PushLiveConfig()
    End Sub

    Private Sub PersistListSettingsChanged(_sender As Object, _e As EventArgs)
        SavePersistedListState(False)
    End Sub

    Private Sub PushLiveConfig()
        If dgvCombat IsNot Nothing AndAlso dgvCombat.IsCurrentCellInEditMode Then
            Return
        End If
        If dgvRegions IsNot Nothing AndAlso dgvRegions.IsCurrentCellInEditMode Then
            Return
        End If

        Try
            _engine.UpdateConfig(BuildConfig())
        Catch
        End Try
    End Sub

    Private Sub BuildUi()
        Text = "KATHANA GAMEBOT"
        Width = 1450
        Height = 900
        BackColor = Color.FromArgb(25, 25, 25)
        ForeColor = Color.Gainsboro

        Dim root As New Panel() With {.Dock = DockStyle.Fill}
        Controls.Add(root)

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)}
        root.Controls.Add(tabs)

        pnlHealthBanner = New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 12,
            .BackColor = Color.FromArgb(55, 55, 55)
        }
        root.Controls.Add(pnlHealthBanner)
        pnlHealthBanner.BringToFront()

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
        left.Controls.Add(BuildFiltersPanel(), 0, 1)

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
        nudRetargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 550}
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
        Dim group As New GroupBox() With {.Text = "Quick Pot Thresholds", .Dock = DockStyle.Top, .Height = 260, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 5}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 320.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        layout.Controls.Add(New Label() With {.Text = "Heal Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        nudAutoPotHp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 80, .Dock = DockStyle.Fill}
        AddHandler nudAutoPotHp.ValueChanged, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds(True)
        layout.Controls.Add(nudAutoPotHp, 1, 0)

        layout.Controls.Add(New Label() With {.Text = "Mana Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudAutoPotMp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 35, .Dock = DockStyle.Fill}
        AddHandler nudAutoPotMp.ValueChanged, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds(True)
        layout.Controls.Add(nudAutoPotMp, 1, 1)

        layout.Controls.Add(New Label() With {.Text = "HP=0 Alarm Volume %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudAlarmVolume = New NumericUpDown() With {.Minimum = 0, .Maximum = 100, .Value = 85, .Dock = DockStyle.Fill}
        AddHandler nudAlarmVolume.ValueChanged,
            Sub(_s As Object, _e As EventArgs)
                _alarmVolumePercent = CInt(nudAlarmVolume.Value)
            End Sub
        layout.Controls.Add(nudAlarmVolume, 1, 2)

        layout.Controls.Add(New Label() With {.Text = "ntfy Channel", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)
        txtNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultNtfyTopicName}
        layout.Controls.Add(txtNtfyTopic, 1, 3)

        Dim buttonRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        Dim btnApply As New Button() With {.Text = "Apply To Heal/Mana/Max-HP Rows", .Width = 220, .Height = 30, .BackColor = Color.FromArgb(42, 120, 80), .ForeColor = Color.White}
        AddHandler btnApply.Click, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds()
        Dim btnTestAlarm As New Button() With {.Text = "Test Alarm + Phone", .Width = 130, .Height = 30, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        AddHandler btnTestAlarm.Click, AddressOf TestAlarmClicked
        Dim btnTestPhone As New Button() With {.Text = "Test Phone Alert", .Width = 130, .Height = 30, .BackColor = Color.FromArgb(55, 110, 170), .ForeColor = Color.White}
        AddHandler btnTestPhone.Click, AddressOf TestPhoneAlertClicked
        buttonRow.Controls.Add(btnApply)
        buttonRow.Controls.Add(btnTestAlarm)
        buttonRow.Controls.Add(btnTestPhone)
        layout.Controls.Add(buttonRow, 1, 4)

        Dim note As New Label() With {.Text = "Use role 'max_health' in Combat Skills and set TriggerPercent for when the max-health potion should fire first. HP alarm triggers only at HP=0.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        layout.Controls.Add(note, 2, 0)
        layout.SetRowSpan(note, 5)
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

        Dim localRetarget As New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 550}
        AddHandler localRetarget.ValueChanged,
            Sub(_s As Object, _e As EventArgs)
                nudRetargetMs.Value = localRetarget.Value
                PushLiveConfig()
            End Sub
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
        roleColumn.Items.AddRange(New Object() {"attack", "heal", "max_health", "mana", "special", "stop"})
        dgvCombat.Columns.Add(roleColumn)
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Priority", .FillWeight = 75.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "TriggerPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinHpPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinMpPercent", .FillWeight = 85.0F})
        group.Controls.Add(dgvCombat)
        Return group
    End Function

    Private Function BuildFiltersPanel() As Control
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        root.Controls.Add(BuildMonsterFilterGroup(), 0, 0)
        root.Controls.Add(BuildLootFilterGroup(), 1, 0)
        Return root
    End Function

    Private Function BuildMonsterFilterGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Monster Filter", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 35.0F))
        group.Controls.Add(layout)

        chkMonsterFilter = New CheckBox() With {.Text = "Enable Monster Filter (blacklist)", .Dock = DockStyle.Fill, .Checked = True}
        layout.Controls.Add(chkMonsterFilter, 0, 0)

        lstMonsterFilter = New ListBox() With {.Dock = DockStyle.Fill}
        layout.Controls.Add(lstMonsterFilter, 0, 1)

        Dim actionRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        txtMonsterName = New TextBox() With {.Width = 140}
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

    Private Function BuildLootFilterGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Loot Filter", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 35.0F))
        group.Controls.Add(layout)

        chkLootPickup = New CheckBox() With {.Text = "Enable Loot Pickup (F)", .Dock = DockStyle.Fill, .Checked = False}
        layout.Controls.Add(chkLootPickup, 0, 0)

        Dim intervalRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        intervalRow.Controls.Add(New Label() With {.Text = "Every (sec):", .AutoSize = True, .Padding = New Padding(0, 6, 0, 0)})
        nudLootPickupSeconds = New NumericUpDown() With {.Minimum = 1, .Maximum = 20, .Value = 4, .Width = 55}
        intervalRow.Controls.Add(nudLootPickupSeconds)
        layout.Controls.Add(intervalRow, 0, 1)

        lstLootFilter = New ListBox() With {.Dock = DockStyle.Fill}
        layout.Controls.Add(lstLootFilter, 0, 2)

        Dim actionRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        txtLootName = New TextBox() With {.Width = 140}
        Dim btnAddLoot As New Button() With {.Text = "Add", .Width = 70}
        Dim btnRemoveLoot As New Button() With {.Text = "Remove", .Width = 80}
        AddHandler btnAddLoot.Click, AddressOf AddLootClicked
        AddHandler btnRemoveLoot.Click, AddressOf RemoveLootClicked
        actionRow.Controls.Add(txtLootName)
        actionRow.Controls.Add(btnAddLoot)
        actionRow.Controls.Add(btnRemoveLoot)
        layout.Controls.Add(actionRow, 0, 3)
        Return group
    End Function

    Private Function BuildCenterControlPanel() As Panel
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12)}
        lblState = New Label() With {.Text = "Status: Searching for target...", .Top = 16, .Left = 8, .Width = 260, .Height = 22}
        lblSystem = New Label() With {.Text = "System Active: False", .Top = 44, .Left = 8, .Width = 260, .Height = 22, .ForeColor = Color.LightGreen}
        lblHp = New Label() With {.Text = "HP%: 0", .Top = 72, .Left = 8, .Width = 120, .Height = 22, .ForeColor = Color.LimeGreen}
        lblMp = New Label() With {.Text = "MP%: 0", .Top = 72, .Left = 136, .Width = 120, .Height = 22, .ForeColor = Color.DeepSkyBlue}
        lblMobName = New Label() With {.Text = "Mob: (none)", .Top = 96, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.LightSkyBlue}
        lblExpRate = New Label() With {.Text = "Prana/EXP: 0.00% | Rate: Calculating (1m)", .Top = 118, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.Khaki}
        btnAttack = New Button() With {.Text = "Attack", .Top = 150, .Left = 8, .Width = 210, .Height = 42, .BackColor = Color.FromArgb(40, 180, 80), .ForeColor = Color.White}
        btnSaveSettings = New Button() With {.Text = "Save Settings", .Top = 204, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(55, 55, 55), .ForeColor = Color.White}
        btnStopBot = New Button() With {.Text = "Stop Bot", .Top = 254, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(20, 130, 210), .ForeColor = Color.White}
        btnBypassLimits = New Button() With {.Text = "Ignore Skill Min HP/MP: OFF", .Top = 304, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        btnBypassStuck = New Button() With {
            .Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF"),
            .Top = 354,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnRetargetNow = New Button() With {.Text = "Retarget Now (E)", .Top = 404, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        btnPartyAutoAccept = New Button() With {
            .Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF"),
            .Top = 454,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        AddHandler btnAttack.Click, AddressOf StartClicked
        AddHandler btnSaveSettings.Click, AddressOf SaveClicked
        AddHandler btnStopBot.Click, AddressOf StopClicked
        AddHandler btnBypassLimits.Click, AddressOf ToggleBypassLimitsClicked
        AddHandler btnBypassStuck.Click, AddressOf ToggleStuckTargetBypassClicked
        AddHandler btnRetargetNow.Click, AddressOf ManualRetargetClicked
        AddHandler btnPartyAutoAccept.Click, AddressOf TogglePartyAutoAcceptClicked
        panel.Controls.Add(lblState)
        panel.Controls.Add(lblSystem)
        panel.Controls.Add(lblHp)
        panel.Controls.Add(lblMp)
        panel.Controls.Add(lblMobName)
        panel.Controls.Add(lblExpRate)
        panel.Controls.Add(btnAttack)
        panel.Controls.Add(btnSaveSettings)
        panel.Controls.Add(btnStopBot)
        panel.Controls.Add(btnBypassLimits)
        panel.Controls.Add(btnBypassStuck)
        panel.Controls.Add(btnRetargetNow)
        panel.Controls.Add(btnPartyAutoAccept)
        Return panel
    End Function

    Private Function BuildLogPanel() As GroupBox
        Dim group As New GroupBox() With {.Text = "Bot Debug Log - Real-time", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}

        Dim realtimeTab As New TabPage("Real-time")
        Dim realtimeLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        realtimeLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        realtimeLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        rtbLog = New RichTextBox() With {.Dock = DockStyle.Fill, .ReadOnly = True, .BackColor = Color.Black, .ForeColor = Color.FromArgb(70, 255, 160), .Font = New Font("Consolas", 9.0F, FontStyle.Regular), .ScrollBars = RichTextBoxScrollBars.Vertical}
        realtimeLayout.Controls.Add(rtbLog, 0, 0)
        Dim btnClearLog As New Button() With {.Text = "Clear Log", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(130, 25, 25), .ForeColor = Color.White}
        AddHandler btnClearLog.Click, Sub(_s As Object, _e As EventArgs) rtbLog.Clear()
        realtimeLayout.Controls.Add(btnClearLog, 0, 1)
        realtimeTab.Controls.Add(realtimeLayout)

        Dim summaryTab As New TabPage("Key Summary")
        summaryTab.Controls.Add(BuildKeySummaryPanel())

        tabs.TabPages.Add(realtimeTab)
        tabs.TabPages.Add(summaryTab)
        layout.Controls.Add(tabs, 0, 0)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildKeySummaryPanel() As Control
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(6)}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))

        lblKeySummaryInfo = New Label() With {
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.LightSteelBlue,
            .Text = "Key press summary in rolling windows: 10m / 30m / 60m."
        }
        layout.Controls.Add(lblKeySummaryInfo, 0, 0)

        dgvKeySummary = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .MultiSelect = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .RowHeadersVisible = False,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        }
        dgvKeySummary.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Key", .HeaderText = "Key"})
        dgvKeySummary.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Last10Min", .HeaderText = "Last 10m"})
        dgvKeySummary.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Last30Min", .HeaderText = "Last 30m"})
        dgvKeySummary.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Last60Min", .HeaderText = "Last 60m"})
        dgvKeySummary.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "LastAction", .HeaderText = "Latest Action"})
        layout.Controls.Add(dgvKeySummary, 0, 1)

        Dim btnResetSummary As New Button() With {
            .Text = "Reset Key Summary",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(130, 70, 25),
            .ForeColor = Color.White
        }
        AddHandler btnResetSummary.Click,
            Sub(_s As Object, _e As EventArgs)
                _keyActionEvents.Clear()
                RefreshKeyActionSummary()
            End Sub
        layout.Controls.Add(btnResetSummary, 0, 2)

        Return layout
    End Function

    Private Sub SeedDefaults()
        txtWindowTitle.Text = "Kathana - The Coming of the Dark Ages"
        dgvRegions.Rows.Add("hp_bar", "11", "25", "151", "11")
        dgvRegions.Rows.Add("mp_bar", "3", "40", "161", "11")
        dgvRegions.Rows.Add("mob_name_rect", "860", "711", "162", "23")
        dgvRegions.Rows.Add("mob_hp_rect", "859", "737", "165", "11")
        dgvRegions.Rows.Add("unreachable_text_rect", "15", "582", "128", "22")
        dgvRegions.Rows.Add("prana_exp_rect", "472", "745", "78", "21")
        dgvRegions.Rows.Add("party_invite_scan_rect", "349", "318", "328", "124")
        dgvRegions.Rows.Add("party_invite_ok_rect", "463", "410", "59", "21")
        nudMobHpThreshold.Value = 1.0D
        nudRetargetMs.Value = 550D

        Dim keyIndex As Integer = 1
        For Each key In PrimaryKeys
            Dim enabled As Boolean = (key = "1" OrElse key = "2" OrElse key = "6")
            Dim role As String = If(key = "6", "heal", "attack")
            Dim trigger As Integer = If(key = "6", 80, 40)
            Dim cooldown As String
            If key = "1" Then
                cooldown = "0.6"
            ElseIf key = "2" Then
                cooldown = "0.45"
            Else
                cooldown = "1.0"
            End If
            dgvCombat.Rows.Add(enabled, key, cooldown, role, keyIndex * 10, trigger, 1, 1)
            keyIndex += 1
        Next
        For Each key In FunctionKeys
            dgvCombat.Rows.Add(False, key, "1.0", "special", keyIndex * 10, 40, 1, 1)
            keyIndex += 1
        Next
        If Not MonsterExists("avara kara") Then
            lstMonsterFilter.Items.Add("avara kara")
        End If
        If txtNtfyTopic IsNot Nothing Then
            txtNtfyTopic.Text = DefaultNtfyTopicName
        End If
        _alarmVolumePercent = CInt(nudAlarmVolume.Value)
        UpdateAttackButtonAppearance(False)
        RefreshKeyActionSummary()
        AppendLog("UI loaded. No API required.")
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        PushLiveConfig()
        SavePersistedListState(True)
        AppendLog("Settings saved to in-app engine.")
    End Sub

    Private Sub StartClicked(sender As Object, e As EventArgs)
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
            _overlayForm = Nothing
            btnOverlayToggle.Text = "Show Overlay"
            AppendLog("Overlay hidden while bot is running.")
        End If

        SavePersistedListState(False)
        ResetHpZeroAlarmState("Alarm state reset for bot start.")
        PushLiveConfig()
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
        SavePersistedListState(False)
        ResetHpZeroAlarmState("Alarm state reset for bot start.")
        PushLiveConfig()
        _engine.Start()
        UpdateAttackButtonAppearance(True)
        AppendLog("Auto-start on launch enabled.")
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        AutoStartOnLaunch()
    End Sub

    Private Sub StopClicked(sender As Object, e As EventArgs)
        Dim hardStopSent As Boolean = _engine.HardStopMovement(txtWindowTitle.Text.Trim(), "stop button")
        If hardStopSent Then
            AppendLog("Hard stop macro sent (movement key-up + stop key burst).")
        Else
            AppendLog("Hard stop macro not sent (window not found or input blocked).")
        End If
        _engine.Stop()
        ApplyHealthUiTint(100.0, False)
        ResetHpZeroAlarmState("Alarm state reset for bot stop.")
        UpdateAttackButtonAppearance(False)
    End Sub

    Private Sub SnapshotClicked(sender As Object, e As EventArgs)
        PushLiveConfig()
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
        btnBypassLimits.Text = If(_bypassHpMpLimits, "Ignore Skill Min HP/MP: ON", "Ignore Skill Min HP/MP: OFF")
        btnBypassLimits.BackColor = If(_bypassHpMpLimits, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        AppendLog(If(_bypassHpMpLimits, "Ignoring skill minimum HP/MP checks enabled.", "Ignoring skill minimum HP/MP checks disabled."))
    End Sub

    Private Sub ToggleStuckTargetBypassClicked(sender As Object, e As EventArgs)
        _bypassStuckTarget = Not _bypassStuckTarget
        btnBypassStuck.Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF")
        btnBypassStuck.BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        AppendLog(If(_bypassStuckTarget, "Auto-retarget for stuck targets enabled.", "Auto-retarget for stuck targets disabled."))
    End Sub

    Private Sub TogglePartyAutoAcceptClicked(sender As Object, e As EventArgs)
        _partyAutoAccept = Not _partyAutoAccept
        UpdatePromptAutoAcceptButton()
        PushLiveConfig()
        AppendLog(If(_partyAutoAccept, "Party/resurrection auto-accept enabled.", "Party/resurrection auto-accept disabled."))
    End Sub

    Private Sub UpdatePromptAutoAcceptButton()
        If btnPartyAutoAccept Is Nothing Then
            Return
        End If
        btnPartyAutoAccept.Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF")
        btnPartyAutoAccept.BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
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
        PushLiveConfig()
    End Sub

    Private Sub OverlayRegionCommitted(regionName As String, region As RectRegion)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String, RectRegion)(AddressOf OverlayRegionCommitted), regionName, region)
            Return
        End If

        UpdateRegionGridRow(regionName, region)
        PushLiveConfig()
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
        PushLiveConfig()
        Dim st As BotStatus = _engine.GetStatus()
        txtDiagnostics.Text =
            $"Running: {st.Running}{Environment.NewLine}" &
            $"BypassHpMpLimits: {_bypassHpMpLimits}{Environment.NewLine}" &
            $"BypassStuckTarget: {_bypassStuckTarget}{Environment.NewLine}" &
            $"PromptAutoAccept (Party/Ress): {_partyAutoAccept}{Environment.NewLine}" &
            $"NtfyTopic: {GetNtfyTopicName()}{Environment.NewLine}" &
            $"LootPickupEnabled: {If(chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked, "True", "False")}{Environment.NewLine}" &
            $"LootPickupIntervalSec: {If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value.ToString(), "4")}{Environment.NewLine}" &
            $"AlarmVolume%: {_alarmVolumePercent}{Environment.NewLine}" &
            $"HpZeroAlarm: {_hpZeroAlarmActive}{Environment.NewLine}" &
            $"HpZeroPending: {_hpZeroPending}{Environment.NewLine}" &
            $"Window Found: {st.WindowFound}{Environment.NewLine}" &
            $"HP%: {st.HpPercent:0.0}{Environment.NewLine}" &
            $"MP%: {st.MpPercent:0.0}{Environment.NewLine}" &
            $"Prana/EXP%: {st.ExpPercent:0.00}{Environment.NewLine}" &
            $"Prana/EXP Rate %/hr: {If(st.ExpPerHour < 0, "Calculating (1m)", st.ExpPerHour.ToString("0.00"))}{Environment.NewLine}" &
            $"MobName: {st.MobName}{Environment.NewLine}" &
            $"OcrError: {OcrReader.LastError()}{Environment.NewLine}" &
            $"MobHP%: {st.MobHpPercent:0.0}{Environment.NewLine}" &
            $"TargetValid: {st.TargetValid}{Environment.NewLine}" &
            $"LastAction: {st.LastAction}{Environment.NewLine}" &
             $"NotAttackingReason: {st.NotAttackingReason}{Environment.NewLine}" &
             $"Error: {st.ErrorMessage}"
        RefreshKeyActionSummary()
    End Sub

    Private Sub EnterToggleTimerTick(sender As Object, e As EventArgs)
        Dim isEnterDown As Boolean = (GetAsyncKeyState(CInt(Keys.Enter)) And &H8000S) <> 0
        If isEnterDown AndAlso Not _enterWasDown Then
            HandleEnterTogglePress()
        End If
        _enterWasDown = isEnterDown
    End Sub

    Private Sub HandleEnterTogglePress()
        If Not IsGameWindowForeground() Then
            Return
        End If

        If _engine.IsRunning() Then
            StopClicked(Nothing, EventArgs.Empty)
            AppendLog("Enter toggle: bot paused.")
        Else
            StartClicked(Nothing, EventArgs.Empty)
            AppendLog("Enter toggle: bot resumed.")
        End If
    End Sub

    Private Function IsGameWindowForeground() As Boolean
        Dim targetTitle As String = If(txtWindowTitle IsNot Nothing, txtWindowTitle.Text, "").Trim()
        If targetTitle = "" Then
            Return False
        End If

        Dim hwnd As IntPtr = GetForegroundWindow()
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim sb As New StringBuilder(512)
        Dim copied As Integer = GetWindowText(hwnd, sb, sb.Capacity)
        If copied <= 0 Then
            Return False
        End If

        Dim activeTitle As String = sb.ToString()
        Return activeTitle.IndexOf(targetTitle, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

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
        lblHp.Text = $"HP%: {status.HpPercent:0.0}"
        lblMp.Text = $"MP%: {status.MpPercent:0.0}"
        lblHp.ForeColor = HpColor(status.HpPercent)
        lblMp.ForeColor = MpColor(status.MpPercent)
        lblMobName.Text = $"Mob: {If(String.IsNullOrWhiteSpace(status.MobName), "(none)", status.MobName)}"
        lblExpRate.Text = $"Prana/EXP: {status.ExpPercent:0.00}% | Rate: {If(status.ExpPerHour < 0, "Calculating (1m)", status.ExpPerHour.ToString("0.00") & "%/hr")}"
        UpdateAttackButtonAppearance(status.Running)
        HandleHpZeroAlarm(status)
        ApplyHealthUiTint(status.HpPercent, status.Running AndAlso status.WindowFound)

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
        TrackKeyActionFromEngineLog(line)
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
            PushLiveConfig()
            SavePersistedListState(False)
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
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Function MonsterExists(name As String) As Boolean
        For Each item In lstMonsterFilter.Items
            If String.Equals(item.ToString(), name, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Sub AddLootClicked(sender As Object, e As EventArgs)
        Dim name As String = txtLootName.Text.Trim()
        If String.IsNullOrWhiteSpace(name) Then
            Return
        End If
        If Not LootExists(name) Then
            lstLootFilter.Items.Add(name)
            AppendLog("Loot filter added: " & name)
            PushLiveConfig()
            SavePersistedListState(False)
        End If
        txtLootName.Text = ""
    End Sub

    Private Sub RemoveLootClicked(sender As Object, e As EventArgs)
        If lstLootFilter.SelectedItem Is Nothing Then
            Return
        End If
        Dim removed As String = lstLootFilter.SelectedItem.ToString()
        lstLootFilter.Items.Remove(lstLootFilter.SelectedItem)
        AppendLog("Loot filter removed: " & removed)
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Function LootExists(name As String) As Boolean
        For Each item In lstLootFilter.Items
            If String.Equals(item.ToString(), name, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Sub ApplyQuickAutoPotThresholds(Optional silent As Boolean = False)
        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim role As String = SafeCell(row, "Role", "attack").ToLowerInvariant()
            If role = "heal" OrElse role = "max_health" Then
                row.Cells("TriggerPercent").Value = CInt(nudAutoPotHp.Value).ToString()
            ElseIf role = "mana" Then
                row.Cells("TriggerPercent").Value = CInt(nudAutoPotMp.Value).ToString()
            End If
        Next
        If Not silent Then
            AppendLog("Applied auto-pot thresholds to heal/max-health/mana rows.")
        End If
        PushLiveConfig()
    End Sub

    Private Sub TestAlarmClicked(sender As Object, e As EventArgs)
        _alarmVolumePercent = CInt(nudAlarmVolume.Value)
        AppendLog($"Testing HP=0 alarm + phone alert at {_alarmVolumePercent}% volume.")
        Task.Run(Sub() PlayAlarmPulse(_alarmVolumePercent))
        Task.Run(
            Async Function()
                Await SendPhoneNotificationAsync("KathanaBot Test", "Combined test: HP alarm sound + phone alert.")
            End Function)
    End Sub

    Private Sub TestPhoneAlertClicked(sender As Object, e As EventArgs)
        AppendLog($"Sending test phone alert to ntfy topic '{GetNtfyTopicName()}'.")
        Task.Run(
            Async Function()
                Await SendPhoneNotificationAsync("KathanaBot Test", "Test phone alert from Auto-Pot tab.")
            End Function)
    End Sub

    Private Function BuildConfig() As BotConfig
        Dim cfg As New BotConfig()
        cfg.WindowTitle = txtWindowTitle.Text.Trim()
        cfg.LoopMs = CInt(nudLoopMs.Value)
        cfg.RetargetMs = CInt(nudRetargetMs.Value)
        cfg.MobHpPresenceThreshold = CDbl(nudMobHpThreshold.Value)
        cfg.BypassHpMpLimits = _bypassHpMpLimits
        cfg.BypassStuckTarget = _bypassStuckTarget
        cfg.PartyAutoAcceptEnabled = _partyAutoAccept
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.MobNameRect = BuildRect("mob_name_rect")
        cfg.MobHpRect = BuildRect("mob_hp_rect")
        cfg.UnreachableTextRect = New RectRegion(15, 582, 128, 22)
        cfg.PranaExpRect = BuildRect("prana_exp_rect")
        cfg.PartyInviteScanRect = BuildRect("party_invite_scan_rect")
        cfg.PartyInviteOkRect = BuildRect("party_invite_ok_rect")
        cfg.LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked)
        cfg.LootPickupIntervalMs = CInt(Math.Round(CDbl(If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D)) * 1000.0))
        cfg.LootPickupVerifyDelayMs = 200

        cfg.DeniedMobs.Clear()
        cfg.LootAllowedNames.Clear()
        If chkMonsterFilter IsNot Nothing AndAlso chkMonsterFilter.Checked AndAlso lstMonsterFilter IsNot Nothing Then
            For Each item In lstMonsterFilter.Items
                cfg.DeniedMobs.Add(item.ToString().Trim().ToLowerInvariant())
            Next
        End If
        If lstLootFilter IsNot Nothing Then
            For Each item In lstLootFilter.Items
                Dim value As String = item.ToString().Trim().ToLowerInvariant()
                If value <> "" Then
                    cfg.LootAllowedNames.Add(value)
                End If
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

    Private Sub LoadPersistedListState()
        Try
            If Not File.Exists(PersistFilePath) Then
                Return
            End If

            Dim raw As String = File.ReadAllText(PersistFilePath, Encoding.UTF8)
            If String.IsNullOrWhiteSpace(raw) Then
                Return
            End If

            Dim state As PersistedListState = JsonSerializer.Deserialize(Of PersistedListState)(raw)
            If state Is Nothing Then
                Return
            End If

            If chkMonsterFilter IsNot Nothing Then
                chkMonsterFilter.Checked = state.MonsterFilterEnabled
            End If
            If chkLootPickup IsNot Nothing Then
                chkLootPickup.Checked = state.LootPickupEnabled
            End If
            If nudLootPickupSeconds IsNot Nothing Then
                Dim boundedSeconds As Decimal = Math.Max(nudLootPickupSeconds.Minimum, Math.Min(nudLootPickupSeconds.Maximum, state.LootPickupSeconds))
                nudLootPickupSeconds.Value = boundedSeconds
            End If
            _partyAutoAccept = state.PromptAutoAcceptEnabled
            UpdatePromptAutoAcceptButton()

            If state.MonsterNames IsNot Nothing AndAlso lstMonsterFilter IsNot Nothing Then
                lstMonsterFilter.Items.Clear()
                For Each entry As String In state.MonsterNames
                    Dim cleaned As String = If(entry, "").Trim()
                    If cleaned <> "" AndAlso Not MonsterExists(cleaned) Then
                        lstMonsterFilter.Items.Add(cleaned)
                    End If
                Next
            End If

            If state.LootNames IsNot Nothing AndAlso lstLootFilter IsNot Nothing Then
                lstLootFilter.Items.Clear()
                For Each entry As String In state.LootNames
                    Dim cleaned As String = If(entry, "").Trim()
                    If cleaned <> "" AndAlso Not LootExists(cleaned) Then
                        lstLootFilter.Items.Add(cleaned)
                    End If
                Next
            End If

            If state.CombatActions IsNot Nothing AndAlso state.CombatActions.Count > 0 Then
                ApplyPersistedCombatActions(state.CombatActions)
            End If
        Catch ex As Exception
            AppendLog("Unable to load saved lists: " & ex.Message)
        End Try
    End Sub

    Private Sub SavePersistedListState(Optional logFailure As Boolean = False)
        Try
            If Not Directory.Exists(PersistDirectoryPath) Then
                Directory.CreateDirectory(PersistDirectoryPath)
            End If

            Dim state As New PersistedListState With {
                .MonsterFilterEnabled = (chkMonsterFilter IsNot Nothing AndAlso chkMonsterFilter.Checked),
                .LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked),
                .LootPickupSeconds = If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D),
                .PromptAutoAcceptEnabled = _partyAutoAccept,
                .MonsterNames = GetListBoxItems(lstMonsterFilter),
                .LootNames = GetListBoxItems(lstLootFilter),
                .CombatActions = GetPersistedCombatActions()
            }

            Dim json As String = JsonSerializer.Serialize(state, New JsonSerializerOptions With {.WriteIndented = True})
            File.WriteAllText(PersistFilePath, json, Encoding.UTF8)
        Catch ex As Exception
            If logFailure Then
                AppendLog("Unable to save list state: " & ex.Message)
            End If
        End Try
    End Sub

    Private Shared Function GetListBoxItems(listBox As ListBox) As List(Of String)
        Dim result As New List(Of String)()
        If listBox Is Nothing Then
            Return result
        End If

        For Each item In listBox.Items
            Dim text As String = If(item, "").ToString().Trim()
            If text <> "" Then
                result.Add(text)
            End If
        Next
        Return result
    End Function

    Private Function GetPersistedCombatActions() As List(Of PersistedCombatAction)
        Dim result As New List(Of PersistedCombatAction)()
        If dgvCombat Is Nothing Then
            Return result
        End If

        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim actionKey As String = SafeCell(row, "Key", "").ToUpperInvariant()
            If actionKey = "" Then
                Continue For
            End If

            Dim enabled As Boolean = False
            Try
                enabled = Convert.ToBoolean(row.Cells("Enabled").Value)
            Catch
            End Try

            Dim cooldownSec As Double = Math.Max(0.05, ParseDouble(SafeCell(row, "CooldownSec", "1.0"), 1.0))
            Dim priority As Integer = Math.Max(1, ParseInt(SafeCell(row, "Priority", "100"), 100))
            Dim triggerPercent As Integer = Math.Min(99, Math.Max(1, ParseInt(SafeCell(row, "TriggerPercent", "40"), 40)))
            Dim minHpPercent As Integer = Math.Min(100, Math.Max(1, ParseInt(SafeCell(row, "MinHpPercent", "1"), 1)))
            Dim minMpPercent As Integer = Math.Min(100, Math.Max(1, ParseInt(SafeCell(row, "MinMpPercent", "1"), 1)))
            Dim role As String = NormalizePersistedRole(SafeCell(row, "Role", "attack").ToLowerInvariant())

            result.Add(New PersistedCombatAction With {
                .ActionKey = actionKey,
                .Enabled = enabled,
                .Role = role,
                .Priority = priority,
                .CooldownSec = cooldownSec,
                .TriggerPercent = triggerPercent,
                .MinHpPercent = minHpPercent,
                .MinMpPercent = minMpPercent
            })
        Next

        Return result
    End Function

    Private Sub ApplyPersistedCombatActions(actions As List(Of PersistedCombatAction))
        If dgvCombat Is Nothing OrElse actions Is Nothing OrElse actions.Count = 0 Then
            Return
        End If

        Dim keyed As New Dictionary(Of String, PersistedCombatAction)(StringComparer.OrdinalIgnoreCase)
        For Each action In actions
            If action Is Nothing Then
                Continue For
            End If
            Dim actionKey As String = If(action.ActionKey, "").Trim().ToUpperInvariant()
            If actionKey = "" Then
                Continue For
            End If
            keyed(actionKey) = action
        Next

        If keyed.Count = 0 Then
            Return
        End If

        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim actionKey As String = SafeCell(row, "Key", "").ToUpperInvariant()
            If actionKey = "" OrElse Not keyed.ContainsKey(actionKey) Then
                Continue For
            End If

            Dim item As PersistedCombatAction = keyed(actionKey)
            row.Cells("Enabled").Value = item.Enabled
            row.Cells("Role").Value = NormalizePersistedRole(item.Role)
            row.Cells("Priority").Value = Math.Max(1, item.Priority).ToString()

            Dim cooldownSec As Double = item.CooldownSec
            If Double.IsNaN(cooldownSec) OrElse Double.IsInfinity(cooldownSec) Then
                cooldownSec = 1.0
            End If
            cooldownSec = Math.Max(0.05, cooldownSec)
            row.Cells("CooldownSec").Value = cooldownSec.ToString("0.###")

            row.Cells("TriggerPercent").Value = Math.Min(99, Math.Max(1, item.TriggerPercent)).ToString()
            row.Cells("MinHpPercent").Value = Math.Min(100, Math.Max(1, item.MinHpPercent)).ToString()
            row.Cells("MinMpPercent").Value = Math.Min(100, Math.Max(1, item.MinMpPercent)).ToString()
        Next
    End Sub

    Private Shared Function NormalizePersistedRole(rawRole As String) As String
        Dim role As String = If(rawRole, "").Trim().ToLowerInvariant()
        Select Case role
            Case "attack", "heal", "max_health", "mana", "special", "stop"
                Return role
            Case Else
                Return "attack"
        End Select
    End Function

    Private Sub AppendLog(message As String)
        Dim stamp As String = DateTime.Now.ToString("HH:mm:ss")
        rtbLog.AppendText($"[{stamp}] {message}{Environment.NewLine}")
        rtbLog.SelectionStart = rtbLog.TextLength
        rtbLog.ScrollToCaret()
    End Sub

    Private Sub AppendLogSafe(message As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf AppendLogSafe), message)
            Return
        End If
        AppendLog(message)
    End Sub

    Private Sub TrackKeyActionFromEngineLog(line As String)
        Dim trimmedLine As String = If(line, "").Trim()
        Const prefix As String = "Key action:"
        If Not trimmedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

        Dim actionText As String = trimmedLine.Substring(prefix.Length).Trim()
        If actionText = "" Then
            Return
        End If

        Dim keyName As String = ExtractKeyNameFromAction(actionText)
        If keyName = "" Then
            Return
        End If

        _keyActionEvents.Add(New KeyActionEvent With {
            .TimestampUtc = DateTime.UtcNow,
            .KeyName = keyName,
            .ActionText = actionText
        })
        PruneKeyActionEvents(DateTime.UtcNow)
        RefreshKeyActionSummary()
    End Sub

    Private Shared Function ExtractKeyNameFromAction(actionText As String) As String
        Dim raw As String = If(actionText, "").Trim()
        If raw = "" Then
            Return ""
        End If

        Dim splitAt As Integer = raw.IndexOfAny(New Char() {" "c, "("c})
        Dim token As String = If(splitAt >= 0, raw.Substring(0, splitAt), raw).Trim().Trim(":"c).ToUpperInvariant()
        If IsLikelyKeyToken(token) Then
            Return token
        End If
        Return ""
    End Function

    Private Shared Function IsLikelyKeyToken(token As String) As Boolean
        If String.IsNullOrWhiteSpace(token) Then
            Return False
        End If

        If token.Length = 1 Then
            Dim ch As Char = token(0)
            If Char.IsDigit(ch) Then
                Return True
            End If
            Return ch >= "A"c AndAlso ch <= "Z"c
        End If

        If token.StartsWith("F", StringComparison.Ordinal) Then
            Dim fnNumber As Integer
            If Integer.TryParse(token.Substring(1), fnNumber) AndAlso fnNumber >= 1 AndAlso fnNumber <= 24 Then
                Return True
            End If
        End If

        Select Case token
            Case "ENTER", "TAB", "SPACE", "ESC", "ESCAPE", "SHIFT", "CTRL", "ALT"
                Return True
        End Select
        Return False
    End Function

    Private Sub RefreshKeyActionSummary()
        If dgvKeySummary Is Nothing OrElse dgvKeySummary.IsDisposed Then
            Return
        End If

        Dim nowUtc As DateTime = DateTime.UtcNow
        PruneKeyActionEvents(nowUtc)
        Dim cutoff10 As DateTime = nowUtc.AddMinutes(-10)
        Dim cutoff30 As DateTime = nowUtc.AddMinutes(-30)
        Dim cutoff60 As DateTime = nowUtc.AddHours(-1)

        Dim summaries As New Dictionary(Of String, KeyActionSummaryRow)(StringComparer.OrdinalIgnoreCase)
        For Each entry As KeyActionEvent In _keyActionEvents
            Dim row As KeyActionSummaryRow = Nothing
            If Not summaries.TryGetValue(entry.KeyName, row) Then
                row = New KeyActionSummaryRow With {.KeyName = entry.KeyName}
                summaries(entry.KeyName) = row
            End If

            If entry.TimestampUtc >= cutoff10 Then
                row.Last10Min += 1
            End If
            If entry.TimestampUtc >= cutoff30 Then
                row.Last30Min += 1
            End If
            If entry.TimestampUtc >= cutoff60 Then
                row.Last60Min += 1
            End If
            row.LastActionText = entry.ActionText
        Next

        Dim ordered As New List(Of KeyActionSummaryRow)(summaries.Values)
        ordered.Sort(
            Function(a As KeyActionSummaryRow, b As KeyActionSummaryRow) As Integer
                Dim byHourly As Integer = b.Last60Min.CompareTo(a.Last60Min)
                If byHourly <> 0 Then
                    Return byHourly
                End If
                Return StringComparer.OrdinalIgnoreCase.Compare(a.KeyName, b.KeyName)
            End Function)

        dgvKeySummary.SuspendLayout()
        Try
            dgvKeySummary.Rows.Clear()
            For Each row As KeyActionSummaryRow In ordered
                dgvKeySummary.Rows.Add(row.KeyName, row.Last10Min, row.Last30Min, row.Last60Min, row.LastActionText)
            Next
        Finally
            dgvKeySummary.ResumeLayout()
        End Try

        If lblKeySummaryInfo Is Nothing OrElse lblKeySummaryInfo.IsDisposed Then
            Return
        End If

        If ordered.Count = 0 Then
            lblKeySummaryInfo.Text = "No key presses tracked in the last 60 minutes."
        Else
            lblKeySummaryInfo.Text = $"Tracked keys: {ordered.Count} | Total presses (60m): {_keyActionEvents.Count} | Updated: {DateTime.Now:HH:mm:ss}"
        End If
    End Sub

    Private Sub PruneKeyActionEvents(nowUtc As DateTime)
        Dim cutoff As DateTime = nowUtc.AddHours(-1)
        _keyActionEvents.RemoveAll(Function(x As KeyActionEvent) x.TimestampUtc < cutoff)
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

    Private Shared Function HpColor(percent As Double) As Color
        If percent <= 0.1 Then
            Return Color.FromArgb(255, 70, 70)
        End If
        If percent < 35.0 Then
            Return Color.FromArgb(255, 140, 60)
        End If
        If percent < 70.0 Then
            Return Color.Khaki
        End If
        Return Color.LimeGreen
    End Function

    Private Shared Function MpColor(percent As Double) As Color
        If percent <= 0.1 Then
            Return Color.FromArgb(255, 95, 95)
        End If
        If percent < 25.0 Then
            Return Color.FromArgb(255, 170, 70)
        End If
        If percent < 60.0 Then
            Return Color.SkyBlue
        End If
        Return Color.DeepSkyBlue
    End Function

    Private Sub HandleHpZeroAlarm(status As BotStatus)
        Dim shouldAlarm As Boolean =
            status IsNot Nothing AndAlso
            status.Running AndAlso
            status.WindowFound AndAlso
            status.ErrorMessage = "" AndAlso
            status.HpPercent <= 0.1

        If shouldAlarm Then
            If Not _hpZeroAlarmActive AndAlso Not _hpZeroPending Then
                StartHpZeroPendingCountdown()
            End If
            Return
        End If

        If _hpZeroPending Then
            CancelHpZeroPendingCountdown(True)
        End If
        If _hpZeroAlarmActive Then
            StopHpZeroAlarm()
        End If
    End Sub

    Private Sub StartHpZeroPendingCountdown()
        _hpZeroPending = True
        If _hpPendingCts IsNot Nothing Then
            _hpPendingCts.Cancel()
            _hpPendingCts.Dispose()
        End If

        _hpPendingCts = New CancellationTokenSource()
        Dim token As CancellationToken = _hpPendingCts.Token
        AppendLog("HP reached 0. Waiting 60 seconds before alarm/notification.")

        _hpPendingTask = Task.Run(
            Async Function()
                Try
                    Await Task.Delay(HpZeroAlarmGraceMs, token)
                Catch ex As TaskCanceledException
                    Return
                End Try
                If token.IsCancellationRequested Then
                    Return
                End If

                If InvokeRequired Then
                    BeginInvoke(New Action(AddressOf PromotePendingHpZeroAlarm))
                Else
                    PromotePendingHpZeroAlarm()
                End If
            End Function, token)
    End Sub

    Private Sub PromotePendingHpZeroAlarm()
        If Not _hpZeroPending Then
            Return
        End If
        _hpZeroPending = False

        If _hpPendingCts IsNot Nothing Then
            _hpPendingCts.Dispose()
            _hpPendingCts = Nothing
        End If

        StartHpZeroAlarm()
    End Sub

    Private Sub CancelHpZeroPendingCountdown(logCancellation As Boolean)
        If Not _hpZeroPending AndAlso _hpPendingCts Is Nothing Then
            Return
        End If

        _hpZeroPending = False
        If _hpPendingCts IsNot Nothing Then
            _hpPendingCts.Cancel()
            _hpPendingCts.Dispose()
            _hpPendingCts = Nothing
        End If
        _lastHpZeroNotification = DateTime.MinValue

        If logCancellation Then
            AppendLog("HP recovered during 60-second grace period. Alarm canceled.")
        End If
    End Sub

    Private Sub StartHpZeroAlarm()
        _hpZeroAlarmActive = True
        AppendLog($"HP is zero. Alarm started at volume {_alarmVolumePercent}%.")
        SendHpZeroPhoneAlert()
        Task.Run(Sub() PlayAlarmPulse(_alarmVolumePercent))
        StopBotAfterDeathAlert()
    End Sub

    Private Sub StopBotAfterDeathAlert()
        If _engine.IsRunning() Then
            _engine.Stop()
        End If
        UpdateAttackButtonAppearance(False)
        StopHpZeroAlarm("Death confirmed by HP=0 alert. Bot stopped to prevent repeated alarms.")
    End Sub

    Private Sub StopHpZeroAlarm(Optional reason As String = "HP recovered. Alarm stopped.")
        CancelHpZeroPendingCountdown(False)

        If Not _hpZeroAlarmActive Then
            Return
        End If

        _hpZeroAlarmActive = False
        If _hpAlarmCts IsNot Nothing Then
            _hpAlarmCts.Cancel()
            _hpAlarmCts.Dispose()
            _hpAlarmCts = Nothing
        End If
        _lastHpZeroNotification = DateTime.MinValue
        AppendLog(reason)
    End Sub

    Private Sub ResetHpZeroAlarmState(Optional reason As String = "")
        _hpZeroPending = False
        If _hpPendingCts IsNot Nothing Then
            _hpPendingCts.Cancel()
            _hpPendingCts.Dispose()
            _hpPendingCts = Nothing
        End If

        _hpZeroAlarmActive = False
        If _hpAlarmCts IsNot Nothing Then
            _hpAlarmCts.Cancel()
            _hpAlarmCts.Dispose()
            _hpAlarmCts = Nothing
        End If

        _lastHpZeroNotification = DateTime.MinValue
        If reason <> "" Then
            AppendLog(reason)
        End If
    End Sub

    Private Sub SendHpZeroPhoneAlert()
        Dim now As DateTime = DateTime.UtcNow
        If _lastHpZeroNotification <> DateTime.MinValue AndAlso (now - _lastHpZeroNotification).TotalSeconds < 20 Then
            Return
        End If

        _lastHpZeroNotification = now
        Task.Run(
            Async Function()
                Await SendPhoneNotificationAsync("KathanaBot HP Alert", "HP reached zero. Check your character.")
            End Function)
    End Sub

    Private Function GetNtfyTopicName() As String
        Dim raw As String = ""
        If txtNtfyTopic IsNot Nothing Then
            raw = txtNtfyTopic.Text.Trim()
        End If
        If raw = "" Then
            Return DefaultNtfyTopicName
        End If

        Dim cleaned As String = raw.Replace(" ", "").Trim("/"c)
        If cleaned = "" Then
            Return DefaultNtfyTopicName
        End If
        Return cleaned
    End Function

    Private Async Function SendPhoneNotificationAsync(title As String, body As String) As Task
        Try
            Dim topic As String = GetNtfyTopicName()
            Dim url As String = $"https://ntfy.sh/{Uri.EscapeDataString(topic)}"
            Using request As New HttpRequestMessage(HttpMethod.Post, url)
                request.Content = New StringContent(body, Encoding.UTF8, "text/plain")
                request.Headers.Add("Title", title)
                request.Headers.Add("Priority", "urgent")
                request.Headers.Add("Tags", "warning,gamepad")

                Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                If response.IsSuccessStatusCode Then
                    AppendLogSafe($"Phone alert sent to ntfy topic '{topic}'.")
                Else
                    AppendLogSafe($"Phone alert failed ({CInt(response.StatusCode)}) for topic '{topic}'.")
                End If
            End Using
        Catch ex As Exception
            AppendLogSafe("Phone alert failed: " & ex.Message)
        End Try
    End Function

    Private Sub PlayAlarmPulse(volumePercent As Integer)
        Dim previous As UInteger = 0UI
        Try
            waveOutGetVolume(IntPtr.Zero, previous)
            Dim level As Integer = Math.Max(0, Math.Min(100, volumePercent))
            Dim scaled As UInteger = CUInt((65535 * level) \ 100)
            Dim stereo As UInteger = scaled Or (scaled << 16)
            waveOutSetVolume(IntPtr.Zero, stereo)

            SystemSounds.Hand.Play()
            Thread.Sleep(160)
            SystemSounds.Exclamation.Play()
            Thread.Sleep(160)
            SystemSounds.Hand.Play()
        Catch
        Finally
            Try
                waveOutSetVolume(IntPtr.Zero, previous)
            Catch
            End Try
        End Try
    End Sub

    Private Sub CaptureThemeSnapshot(control As Control)
        If control Is Nothing Then
            Return
        End If

        If Not _baseBackColors.ContainsKey(control) Then
            _baseBackColors(control) = control.BackColor
        End If

        If TypeOf control Is DataGridView Then
            Dim grid = CType(control, DataGridView)
            If Not _gridThemeSnapshots.ContainsKey(grid) Then
                _gridThemeSnapshots(grid) = New GridThemeSnapshot With {
                    .BackgroundColor = grid.BackgroundColor,
                    .HeaderBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor,
                    .HeaderForeColor = grid.ColumnHeadersDefaultCellStyle.ForeColor,
                    .DefaultBackColor = grid.DefaultCellStyle.BackColor,
                    .DefaultForeColor = grid.DefaultCellStyle.ForeColor,
                    .SelectionBackColor = grid.DefaultCellStyle.SelectionBackColor,
                    .SelectionForeColor = grid.DefaultCellStyle.SelectionForeColor,
                    .GridColor = grid.GridColor
                }
            End If
        End If

        For Each child As Control In control.Controls
            CaptureThemeSnapshot(child)
        Next
    End Sub

    Private Sub ApplyHealthUiTint(percent As Double, active As Boolean)
        If pnlHealthBanner Is Nothing Then
            Return
        End If

        If Not active Then
            pnlHealthBanner.BackColor = Color.FromArgb(55, 55, 55)
            Return
        End If

        Dim safePercent As Double = If(Double.IsNaN(percent) OrElse Double.IsInfinity(percent), 100.0, percent)
        Dim bounded As Double = Math.Max(0.0, Math.Min(100.0, safePercent))
        pnlHealthBanner.BackColor = HpColor(bounded)
    End Sub

    Private Sub RestoreThemeSnapshot(control As Control)
        If control Is Nothing Then
            Return
        End If

        If _baseBackColors.ContainsKey(control) Then
            control.BackColor = _baseBackColors(control)
        End If

        If TypeOf control Is DataGridView Then
            Dim grid = CType(control, DataGridView)
            If _gridThemeSnapshots.ContainsKey(grid) Then
                Dim snapshot As GridThemeSnapshot = _gridThemeSnapshots(grid)
                grid.BackgroundColor = snapshot.BackgroundColor
                grid.EnableHeadersVisualStyles = False
                grid.ColumnHeadersDefaultCellStyle.BackColor = snapshot.HeaderBackColor
                grid.ColumnHeadersDefaultCellStyle.ForeColor = snapshot.HeaderForeColor
                grid.DefaultCellStyle.BackColor = snapshot.DefaultBackColor
                grid.DefaultCellStyle.ForeColor = snapshot.DefaultForeColor
                grid.DefaultCellStyle.SelectionBackColor = snapshot.SelectionBackColor
                grid.DefaultCellStyle.SelectionForeColor = snapshot.SelectionForeColor
                grid.GridColor = snapshot.GridColor
            End If
        End If

        For Each child As Control In control.Controls
            RestoreThemeSnapshot(child)
        Next
    End Sub

    Private Sub ApplyTintRecursive(control As Control, tint As Color, blendAmount As Double)
        If control Is Nothing Then
            Return
        End If

        If _baseBackColors.ContainsKey(control) Then
            Dim adjustedBlend As Double = blendAmount
            If TypeOf control Is TextBox OrElse TypeOf control Is RichTextBox Then
                adjustedBlend = Math.Min(0.95, blendAmount + 0.15)
            ElseIf TypeOf control Is GroupBox Then
                adjustedBlend = Math.Min(0.95, blendAmount + 0.1)
            End If
            control.BackColor = BlendColors(_baseBackColors(control), tint, adjustedBlend)
        End If

        If TypeOf control Is DataGridView Then
            Dim grid = CType(control, DataGridView)
            If _gridThemeSnapshots.ContainsKey(grid) Then
                Dim snapshot As GridThemeSnapshot = _gridThemeSnapshots(grid)
                Dim gridBlend As Double = Math.Min(0.96, blendAmount + 0.16)
                grid.BackgroundColor = BlendColors(snapshot.BackgroundColor, tint, gridBlend)
                grid.EnableHeadersVisualStyles = False
                grid.ColumnHeadersDefaultCellStyle.BackColor = BlendColors(snapshot.HeaderBackColor, tint, gridBlend)
                grid.ColumnHeadersDefaultCellStyle.ForeColor = snapshot.HeaderForeColor
                grid.DefaultCellStyle.BackColor = BlendColors(snapshot.DefaultBackColor, tint, gridBlend)
                grid.DefaultCellStyle.ForeColor = snapshot.DefaultForeColor
                grid.DefaultCellStyle.SelectionBackColor = BlendColors(snapshot.SelectionBackColor, tint, Math.Min(0.98, gridBlend + 0.08))
                grid.DefaultCellStyle.SelectionForeColor = snapshot.SelectionForeColor
                grid.GridColor = BlendColors(snapshot.GridColor, tint, Math.Min(0.98, gridBlend + 0.05))
            End If
        End If

        For Each child As Control In control.Controls
            ApplyTintRecursive(child, tint, blendAmount)
        Next
    End Sub

    Private Shared Function HealthUiBlendAmount(percent As Double) As Double
        If percent <= 8.0 Then
            Return 0.94
        End If
        If percent <= 20.0 Then
            Return 0.90
        End If
        If percent <= 35.0 Then
            Return 0.86
        End If
        If percent <= 60.0 Then
            Return 0.80
        End If
        Return 0.74
    End Function

    Private Shared Function BlendColors(baseColor As Color, tint As Color, amount As Double) As Color
        Dim t As Double = amount
        If Double.IsNaN(t) OrElse Double.IsInfinity(t) Then
            t = 0.0
        End If
        t = Math.Max(0.0, Math.Min(1.0, t))

        Dim r As Integer = BlendChannel(baseColor.R, tint.R, t)
        Dim g As Integer = BlendChannel(baseColor.G, tint.G, t)
        Dim b As Integer = BlendChannel(baseColor.B, tint.B, t)
        Return Color.FromArgb(255, r, g, b)
    End Function

    Private Shared Function BlendChannel(baseValue As Integer, tintValue As Integer, factor As Double) As Integer
        Dim value As Double = baseValue + (tintValue - baseValue) * factor
        If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then
            Return Math.Max(0, Math.Min(255, baseValue))
        End If
        If value <= 0.0 Then
            Return 0
        End If
        If value >= 255.0 Then
            Return 255
        End If
        Return CInt(value)
    End Function

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
        _enterToggleTimer.Stop()
        SavePersistedListState(False)
        StopHpZeroAlarm()
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
        End If
        _engine.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
