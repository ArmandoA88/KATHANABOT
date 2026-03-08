Imports System.Media
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.IO
Imports System.Diagnostics

Public Class Form1
    Private Shared ReadOnly PrimaryKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
    Private Shared ReadOnly FunctionKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
    Private Shared ReadOnly CustomCombatDefaultKeys As String() = {"F11", "F12", "F13"}

    Private ReadOnly _engine As New BotEngine()
    Private ReadOnly _uiTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _enterToggleTimer As New System.Windows.Forms.Timer()

    Private txtWindowTitle As TextBox
    Private nudLoopMs As NumericUpDown
    Private nudRetargetMs As NumericUpDown
    Private nudMobHpThreshold As NumericUpDown
    Private chkHighMaxHpSpecial As CheckBox
    Private nudHighMaxHpThreshold As NumericUpDown
    Private lstProcessWindows As ListBox
    Private txtProcessRename As TextBox
    Private btnOverlayToggle As Button
    Private dgvRegions As DataGridView
    Private txtLootScanAreaPoints As TextBox
    Private picSnapshot As PictureBox
    Private pnlWindowFrame As Panel
    Private btnPickLootRejectPoint As Button
    Private btnClearLootRejectPoint As Button
    Private lblLootRejectPoint As Label

    Private dgvCombat As DataGridView
    Private chkMonsterFilter As CheckBox
    Private chkLootPickup As CheckBox
    Private nudLootPickupSeconds As NumericUpDown
    Private lstMonsterFilter As ListBox
    Private lstLootFilter As ListBox
    Private txtMonsterName As TextBox
    Private txtLootName As TextBox
    Private chkLevelingAgent As CheckBox
    Private txtLevelingPreferredMobs As TextBox
    Private nudLevelingStopHp As NumericUpDown
    Private nudLevelingStopMp As NumericUpDown
    Private nudLevelingMaxNoTargetSeconds As NumericUpDown
    Private chkLevelingStopOnLowExp As CheckBox
    Private nudLevelingMinExpPerHour As NumericUpDown
    Private chkLevelingStopOnRepeatedUnreachable As CheckBox
    Private nudLevelingUnreachableLimit As NumericUpDown
    Private chkNavigationEnabled As CheckBox
    Private txtMapOpenKey As TextBox
    Private chkTravelPreview As CheckBox
    Private chkTravelExecute As CheckBox
    Private chkRouteRecording As CheckBox
    Private txtRouteRecordingName As TextBox
    Private btnSaveRouteRecording As Button
    Private cboRecordedRoute As ComboBox
    Private cboRecordedRouteNode As ComboBox
    Private btnDeleteRecordedRoute As Button
    Private btnDeleteRecordedRouteNode As Button
    Private cboNavigationStartNode As ComboBox
    Private cboNavigationTargetNode As ComboBox
    Private nudNavigationWaypointRadius As NumericUpDown
    Private nudNavigationMoveBurstMs As NumericUpDown
    Private nudNavigationResampleMs As NumericUpDown
    Private nudNavigationStallTimeoutMs As NumericUpDown
    Private chkNavigationRepathOnStuck As CheckBox
    Private lblLevelingState As Label
    Private lblLevelingReason As Label
    Private lblMapCoordinate As Label
    Private lblMapHeading As Label
    Private lblMapMarker As Label
    Private lblMapLocalizationConfidence As Label
    Private lblTravelStatus As Label
    Private lblRoutePreview As Label
    Private lblRouteRecording As Label

    Private lblState As Label
    Private lblSystem As Label
    Private lblRunState As Label
    Private lblShortcutHint As Label
    Private lblHp As Label
    Private lblMp As Label
    Private lblMobName As Label
    Private lblExpRate As Label
    Private lblRupiahsRate As Label
    Private btnAttack As Button
    Private btnSaveSettings As Button
    Private btnStopBot As Button
    Private btnBypassLimits As Button
    Private btnBypassStuck As Button
    Private btnRetargetNow As Button
    Private btnPartyAutoAccept As Button
        Private btnPartyAsk As Button
    Private btnLootScanner As Button
    Private txtItemNtfyTopic As TextBox
    Private btnHelp As Button
    Private nudPartyAskSeconds As NumericUpDown
    Private txtPartyAskText As TextBox
    Private rtbLog As RichTextBox
    Private dgvKeySummary As DataGridView
    Private lblKeySummaryInfo As Label
    Private txtDiagnostics As TextBox
    Private pnlHealthBanner As Panel

    Private nudAutoPotHp As NumericUpDown
    Private nudAutoPotMp As NumericUpDown
    Private nudStuckTargetMs As NumericUpDown
    Private nudLootNameMatchThreshold As NumericUpDown
    Private nudAlarmVolume As NumericUpDown
    Private txtNtfyTopic As TextBox

    Private _lastAction As String = ""
    Private _lastState As String = ""
    Private _lastError As String = ""
    Private _lastNoAttackReason As String = ""
    Private _lastAgentState As String = ""
    Private _lastRouteRecordingSavedPath As String = ""
    Private _bypassHpMpLimits As Boolean = False
    Private _bypassStuckTarget As Boolean = True
    Private _partyAutoAccept As Boolean = True
        Private _partyAskEnabled As Boolean = False
    Private _lootScannerEnabled As Boolean = True
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
    Private _lastWindowMissingNotification As DateTime = DateTime.MinValue
    Private _notificationWarmupUntilUtc As DateTime = DateTime.MinValue
    Private _deadHpConfirmCount As Integer = 0
    Private _deathNotificationLatched As Boolean = False
    Private _windowMissingNotificationLatched As Boolean = False
    Private _ctrlShiftWasDown As Boolean = False
    Private _isPickingLootRejectPoint As Boolean = False
    Private _lootRejectPointX As Integer = -1
    Private _lootRejectPointY As Integer = -1
    Private _themeSnapshotCaptured As Boolean = False
    Private _lastUiTintActive As Boolean = False
    Private _lastUiTintColorArgb As Integer = Integer.MinValue
    Private _lastUiTintBlend As Double = -1.0
    Private Const HpZeroAlarmGraceMs As Integer = 60000
    Private Const DeadZeroThreshold As Double = 0.1
    Private Const DeadRecoverThreshold As Double = 2.0
    Private Const DeadConfirmRequiredCount As Integer = 5
    Private Const DeathNotificationRetryCount As Integer = 3
    Private Const StartupNotificationWarmupSeconds As Integer = 20
    Private Const DefaultNtfyTopicName As String = "Katana12345"
    Private Const DefaultPartyAskCommand As String = "add"
    Private Const DefaultLootNameMatchThresholdPercent As Integer = 80
    Private Const DefaultMapOpenKey As String = "M"
    Private Const DefaultLevelingMinExpPerHour As Decimal = 0.15D
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

    Private Class ProcessWindowEntry
        Public Property ProcessId As Integer
        Public Property ProcessName As String = ""
        Public Property WindowTitle As String = ""
        Public Property MainWindowHandle As IntPtr = IntPtr.Zero

        Public Overrides Function ToString() As String
            Return $"{WindowTitle} - {ProcessName} ({ProcessId})"
        End Function
    End Class

    Private Class PersistedListState
        Public Property MonsterFilterEnabled As Boolean = True
        Public Property LootPickupEnabled As Boolean = False
        Public Property LootPickupSeconds As Decimal = 4D
        Public Property LootNameMatchThresholdPercent As Decimal = 80D
        Public Property LootRejectPointEnabled As Boolean = False
        Public Property LootRejectPointX As Integer = -1
        Public Property LootRejectPointY As Integer = -1
        Public Property PromptAutoAcceptEnabled As Boolean = True
        Public Property AskForPartyEnabled As Boolean = False
        Public Property AskForPartySeconds As Decimal = 30D
            Public Property AskForPartyText As String
        Public Property LootScannerEnabled As Boolean = True
        Public Property ItemNtfyTopic As String = "add"
        Public Property NtfyTopic As String = ""
        Public Property AutoPotHpPercent As Decimal = 80D
        Public Property AutoPotMpPercent As Decimal = 35D
        Public Property AlarmVolumePercent As Integer = 85
        Public Property SavedConfig As BotConfig = Nothing
        Public Property MonsterNames As List(Of String) = New List(Of String)()
        Public Property LootNames As List(Of String) = New List(Of String)()
        Public Property CombatActions As List(Of PersistedCombatAction) = New List(Of PersistedCombatAction)()
    End Class

    Private Class PersistedCombatAction
        Public Property ActionKey As String = ""
        Public Property RowIndex As Integer = -1
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

    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function SetWindowText(hWnd As IntPtr, lpString As String) As Boolean
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
        CaptureThemeSnapshot(Me)
        _themeSnapshotCaptured = True

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
        If txtLootScanAreaPoints IsNot Nothing Then
            AddHandler txtLootScanAreaPoints.TextChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudLoopMs.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudRetargetMs.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudMobHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        If chkHighMaxHpSpecial IsNot Nothing Then
            AddHandler chkHighMaxHpSpecial.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudHighMaxHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudAutoPotHp.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudAutoPotMp.ValueChanged, AddressOf LiveConfigChanged
        If nudStuckTargetMs IsNot Nothing Then
            AddHandler nudStuckTargetMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNameMatchThreshold IsNot Nothing Then
            AddHandler nudLootNameMatchThreshold.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudAlarmVolume.ValueChanged, AddressOf LiveConfigChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf LiveConfigChanged
        If nudPartyAskSeconds IsNot Nothing Then
            AddHandler nudPartyAskSeconds.ValueChanged, AddressOf LiveConfigChanged
        End If
        If txtPartyAskText IsNot Nothing Then
            AddHandler txtPartyAskText.TextChanged, AddressOf LiveConfigChanged
        End If
        If chkLevelingAgent IsNot Nothing Then
            AddHandler chkLevelingAgent.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If txtLevelingPreferredMobs IsNot Nothing Then
            AddHandler txtLevelingPreferredMobs.TextChanged, AddressOf LiveConfigChanged
        End If
        If nudLevelingStopHp IsNot Nothing Then
            AddHandler nudLevelingStopHp.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLevelingStopMp IsNot Nothing Then
            AddHandler nudLevelingStopMp.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLevelingMaxNoTargetSeconds IsNot Nothing Then
            AddHandler nudLevelingMaxNoTargetSeconds.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkLevelingStopOnLowExp IsNot Nothing Then
            AddHandler chkLevelingStopOnLowExp.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudLevelingMinExpPerHour IsNot Nothing Then
            AddHandler nudLevelingMinExpPerHour.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkLevelingStopOnRepeatedUnreachable IsNot Nothing Then
            AddHandler chkLevelingStopOnRepeatedUnreachable.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudLevelingUnreachableLimit IsNot Nothing Then
            AddHandler nudLevelingUnreachableLimit.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkNavigationEnabled IsNot Nothing Then
            AddHandler chkNavigationEnabled.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If txtMapOpenKey IsNot Nothing Then
            AddHandler txtMapOpenKey.TextChanged, AddressOf LiveConfigChanged
        End If
        If chkTravelPreview IsNot Nothing Then
            AddHandler chkTravelPreview.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If chkTravelExecute IsNot Nothing Then
            AddHandler chkTravelExecute.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If chkRouteRecording IsNot Nothing Then
            AddHandler chkRouteRecording.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If txtRouteRecordingName IsNot Nothing Then
            AddHandler txtRouteRecordingName.TextChanged, AddressOf LiveConfigChanged
        End If
        If cboNavigationStartNode IsNot Nothing Then
            AddHandler cboNavigationStartNode.SelectedIndexChanged, AddressOf LiveConfigChanged
        End If
        If cboNavigationTargetNode IsNot Nothing Then
            AddHandler cboNavigationTargetNode.SelectedIndexChanged, AddressOf LiveConfigChanged
        End If
        If nudNavigationWaypointRadius IsNot Nothing Then
            AddHandler nudNavigationWaypointRadius.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudNavigationMoveBurstMs IsNot Nothing Then
            AddHandler nudNavigationMoveBurstMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudNavigationResampleMs IsNot Nothing Then
            AddHandler nudNavigationResampleMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudNavigationStallTimeoutMs IsNot Nothing Then
            AddHandler nudNavigationStallTimeoutMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkNavigationRepathOnStuck IsNot Nothing Then
            AddHandler chkNavigationRepathOnStuck.CheckedChanged, AddressOf LiveConfigChanged
        End If
        AddHandler dgvCombat.CellValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvCombat.CellEndEdit, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellEndEdit, AddressOf LiveConfigChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        If chkHighMaxHpSpecial IsNot Nothing Then
            AddHandler chkHighMaxHpSpecial.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudHighMaxHpThreshold.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudStuckTargetMs IsNot Nothing Then
            AddHandler nudStuckTargetMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNameMatchThreshold IsNot Nothing Then
            AddHandler nudLootNameMatchThreshold.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudPartyAskSeconds IsNot Nothing Then
            AddHandler nudPartyAskSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If txtPartyAskText IsNot Nothing Then
            AddHandler txtPartyAskText.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLevelingAgent IsNot Nothing Then
            AddHandler chkLevelingAgent.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If txtLevelingPreferredMobs IsNot Nothing Then
            AddHandler txtLevelingPreferredMobs.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingStopHp IsNot Nothing Then
            AddHandler nudLevelingStopHp.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingStopMp IsNot Nothing Then
            AddHandler nudLevelingStopMp.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingMaxNoTargetSeconds IsNot Nothing Then
            AddHandler nudLevelingMaxNoTargetSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLevelingStopOnLowExp IsNot Nothing Then
            AddHandler chkLevelingStopOnLowExp.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingMinExpPerHour IsNot Nothing Then
            AddHandler nudLevelingMinExpPerHour.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLevelingStopOnRepeatedUnreachable IsNot Nothing Then
            AddHandler chkLevelingStopOnRepeatedUnreachable.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingUnreachableLimit IsNot Nothing Then
            AddHandler nudLevelingUnreachableLimit.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkNavigationEnabled IsNot Nothing Then
            AddHandler chkNavigationEnabled.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If txtMapOpenKey IsNot Nothing Then
            AddHandler txtMapOpenKey.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If chkTravelPreview IsNot Nothing Then
            AddHandler chkTravelPreview.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkTravelExecute IsNot Nothing Then
            AddHandler chkTravelExecute.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkRouteRecording IsNot Nothing Then
            AddHandler chkRouteRecording.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If txtRouteRecordingName IsNot Nothing Then
            AddHandler txtRouteRecordingName.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If cboNavigationStartNode IsNot Nothing Then
            AddHandler cboNavigationStartNode.SelectedIndexChanged, AddressOf PersistListSettingsChanged
        End If
        If cboNavigationTargetNode IsNot Nothing Then
            AddHandler cboNavigationTargetNode.SelectedIndexChanged, AddressOf PersistListSettingsChanged
        End If
        If nudNavigationWaypointRadius IsNot Nothing Then
            AddHandler nudNavigationWaypointRadius.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudNavigationMoveBurstMs IsNot Nothing Then
            AddHandler nudNavigationMoveBurstMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudNavigationResampleMs IsNot Nothing Then
            AddHandler nudNavigationResampleMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudNavigationStallTimeoutMs IsNot Nothing Then
            AddHandler nudNavigationStallTimeoutMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkNavigationRepathOnStuck IsNot Nothing Then
            AddHandler chkNavigationRepathOnStuck.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If txtLootScanAreaPoints IsNot Nothing Then
            AddHandler txtLootScanAreaPoints.TextChanged, AddressOf PersistListSettingsChanged
        End If
        AddHandler dgvCombat.CurrentCellDirtyStateChanged,
            Sub(_s As Object, _e As EventArgs)
                If dgvCombat.IsCurrentCellDirty Then
                    dgvCombat.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
            End Sub
        If btnSaveRouteRecording IsNot Nothing Then
            AddHandler btnSaveRouteRecording.Click, AddressOf SaveRouteRecordingClicked
        End If
        If cboRecordedRoute IsNot Nothing Then
            AddHandler cboRecordedRoute.SelectedIndexChanged, AddressOf RecordedRouteSelectionChanged
        End If
        If btnDeleteRecordedRoute IsNot Nothing Then
            AddHandler btnDeleteRecordedRoute.Click, AddressOf DeleteRecordedRouteClicked
        End If
        If btnDeleteRecordedRouteNode IsNot Nothing Then
            AddHandler btnDeleteRecordedRouteNode.Click, AddressOf DeleteRecordedRouteNodeClicked
        End If
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

        pnlWindowFrame = New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(9),
            .BackColor = Color.FromArgb(55, 55, 55)
        }
        Controls.Add(pnlWindowFrame)

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)}
        pnlWindowFrame.Controls.Add(tabs)

        pnlHealthBanner = New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 12,
            .BackColor = Color.FromArgb(55, 55, 55)
        }
        pnlWindowFrame.Controls.Add(pnlHealthBanner)
        pnlHealthBanner.BringToFront()

        tabs.TabPages.Add(BuildCombatTab())
        tabs.TabPages.Add(BuildVisionTab())
        tabs.TabPages.Add(BuildAutoPotTab())
        tabs.TabPages.Add(BuildLevelingTab())
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
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, 260.0F))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim generalGroup As New GroupBox() With {.Text = "Vision + Window Setup", .Dock = DockStyle.Fill}
        Dim generalLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 5}
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

        chkHighMaxHpSpecial = New CheckBox() With {.Text = "Use special key on high max HP mobs", .Dock = DockStyle.Fill}
        generalLayout.Controls.Add(chkHighMaxHpSpecial, 0, 3)
        generalLayout.SetColumnSpan(chkHighMaxHpSpecial, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Max HP >=", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 3)
        nudHighMaxHpThreshold = New NumericUpDown() With {
            .Dock = DockStyle.Fill,
            .Minimum = 100,
            .Maximum = 50000000,
            .Increment = 100,
            .ThousandsSeparator = True,
            .Value = 2000
        }
        generalLayout.Controls.Add(nudHighMaxHpThreshold, 3, 3)

        Dim hint As New Label() With {.Text = "Mob HP Presence % = minimum red-fill detected in Mob HP bar. For high max HP special, make mob_hp_rect include the HP numbers and assign a Combat Skill row role to high_max_hp.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}
        generalLayout.Controls.Add(hint, 0, 4)
        generalLayout.SetColumnSpan(hint, 4)

        generalGroup.Controls.Add(generalLayout)
        left.Controls.Add(generalGroup, 0, 0)

        Dim regionGroup As New GroupBox() With {.Text = "Calibration Regions", .Dock = DockStyle.Fill}
        Dim regionLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(6)}
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60.0F))
        regionGroup.Controls.Add(regionLayout)

        Dim regionHint As New Label() With {
            .Text = "Rectangle regions stay in the grid. Loot Scan uses freeform points below: x,y | x,y | x,y | x,y",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        regionLayout.Controls.Add(regionHint, 0, 0)

        dgvRegions = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Region", .ReadOnly = True})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "X"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Y"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "W"})
        dgvRegions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "H"})
        regionLayout.Controls.Add(dgvRegions, 0, 1)

        Dim lootAreaPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0, 6, 0, 0)}
        lootAreaPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        lootAreaPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        lootAreaPanel.Controls.Add(New Label() With {.Text = "Loot Scan Area", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        txtLootScanAreaPoints = New TextBox() With {.Dock = DockStyle.Fill}
        lootAreaPanel.Controls.Add(txtLootScanAreaPoints, 1, 0)
        regionLayout.Controls.Add(lootAreaPanel, 0, 2)
        left.Controls.Add(regionGroup, 0, 1)

        root.Controls.Add(left, 0, 0)

        Dim right As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        right.RowStyles.Add(New RowStyle(SizeType.Absolute, 260.0F))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        right.Controls.Add(BuildProcessListGroup(), 0, 0)

        Dim snapshotGroup As New GroupBox() With {.Text = "Snapshot", .Dock = DockStyle.Fill}
        Dim snapshotLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .Padding = New Padding(6)}
        snapshotLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        picSnapshot = New PictureBox() With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Black}
        AddHandler picSnapshot.MouseClick, AddressOf SnapshotMouseClick
        snapshotLayout.Controls.Add(picSnapshot, 0, 0)

        snapshotGroup.Controls.Add(snapshotLayout)
        right.Controls.Add(snapshotGroup, 0, 1)

        root.Controls.Add(right, 1, 0)

        Return tab
    End Function

    Private Function BuildProcessListGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Process List", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Padding = New Padding(6)}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        group.Controls.Add(layout)

        Dim lblProcess As New Label() With {.Text = "Process List", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        layout.Controls.Add(lblProcess, 0, 0)

        lstProcessWindows = New ListBox() With {.Dock = DockStyle.Fill, .IntegralHeight = False}
        AddHandler lstProcessWindows.SelectedIndexChanged, AddressOf ProcessSelectionChanged
        layout.Controls.Add(lstProcessWindows, 0, 1)

        Dim btnRefresh As New Button() With {.Text = "Update", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(55, 55, 55), .ForeColor = Color.White}
        AddHandler btnRefresh.Click, AddressOf RefreshProcessListClicked
        layout.Controls.Add(btnRefresh, 0, 2)

        Dim lblRename As New Label() With {.Text = "Rename Process", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        layout.Controls.Add(lblRename, 0, 3)

        txtProcessRename = New TextBox() With {.Dock = DockStyle.Fill}
        layout.Controls.Add(txtProcessRename, 0, 4)

        Dim btnApply As New Button() With {.Text = "Apply", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnApply.Click, AddressOf ApplyProcessRenameClicked
        layout.Controls.Add(btnApply, 0, 5)

        RefreshProcessWindowList(False, IntPtr.Zero)
        Return group
    End Function

    Private Function BuildAutoPotTab() As TabPage
        Dim tab As New TabPage("Auto-Pot") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(10)}
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 62.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 38.0F))

        Dim settingsGroup As New GroupBox() With {.Text = "Auto-Pot Controls", .Dock = DockStyle.Fill, .Padding = New Padding(12)}
        Dim settingsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2}
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48.0F))
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52.0F))
        settingsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        settingsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))

        Dim thresholdsGroup As New GroupBox() With {.Text = "Thresholds", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim thresholdsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 4}
        thresholdsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        thresholdsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        thresholdsLayout.Controls.Add(New Label() With {.Text = "Heal Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        nudAutoPotHp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 80, .Dock = DockStyle.Fill}
        AddHandler nudAutoPotHp.ValueChanged, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds(True)
        thresholdsLayout.Controls.Add(nudAutoPotHp, 1, 0)

        thresholdsLayout.Controls.Add(New Label() With {.Text = "Mana Trigger %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudAutoPotMp = New NumericUpDown() With {.Minimum = 1, .Maximum = 99, .Value = 35, .Dock = DockStyle.Fill}
        AddHandler nudAutoPotMp.ValueChanged, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds(True)
        thresholdsLayout.Controls.Add(nudAutoPotMp, 1, 1)

        thresholdsLayout.Controls.Add(New Label() With {.Text = "HP=0 Alarm Volume %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudAlarmVolume = New NumericUpDown() With {.Minimum = 0, .Maximum = 100, .Value = 85, .Dock = DockStyle.Fill}
        AddHandler nudAlarmVolume.ValueChanged,
            Sub(_s As Object, _e As EventArgs)
                _alarmVolumePercent = CInt(nudAlarmVolume.Value)
            End Sub
        thresholdsLayout.Controls.Add(nudAlarmVolume, 1, 2)

        Dim thresholdsHint As New Label() With {
            .Text = "These quick values mirror your heal, mana, and max-health rows. HP alarm volume only affects the death alarm sound.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        thresholdsLayout.Controls.Add(thresholdsHint, 0, 3)
        thresholdsLayout.SetColumnSpan(thresholdsHint, 2)
        thresholdsGroup.Controls.Add(thresholdsLayout)

        Dim notifyGroup As New GroupBox() With {.Text = "Notifications + Loot Matching", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim notifyLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 5}
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))

        notifyLayout.Controls.Add(New Label() With {.Text = "ntfy Channel (Global)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        txtNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultNtfyTopicName}
        notifyLayout.Controls.Add(txtNtfyTopic, 1, 0)

        notifyLayout.Controls.Add(New Label() With {.Text = "ntfy Channel (Items)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        txtItemNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtItemNtfyTopic, 1, 1)

        notifyLayout.Controls.Add(New Label() With {.Text = "Loot Name Match %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudLootNameMatchThreshold = New NumericUpDown() With {.Minimum = 50, .Maximum = 100, .Value = DefaultLootNameMatchThresholdPercent, .Dock = DockStyle.Fill}
        notifyLayout.Controls.Add(nudLootNameMatchThreshold, 1, 2)

        Dim note As New Label() With {
            .Text = "Loot Name Match % controls fuzzy OCR matching for Loot Filter names. Use a higher value for stricter matching and a lower value when OCR is inconsistent." & Environment.NewLine &
                    "Use role 'max_health' in Combat Skills if you want the max-health potion threshold controlled here. HP alarm only triggers at HP=0.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        notifyLayout.Controls.Add(note, 0, 3)
        notifyLayout.SetColumnSpan(note, 2)

        Dim notifyFoot As New Label() With {
            .Text = "Item alerts use the item channel; death/window alerts use the global channel.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.Gray,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        notifyLayout.Controls.Add(notifyFoot, 0, 4)
        notifyLayout.SetColumnSpan(notifyFoot, 2)
        notifyGroup.Controls.Add(notifyLayout)

        settingsLayout.Controls.Add(thresholdsGroup, 0, 0)
        settingsLayout.Controls.Add(notifyGroup, 1, 0)

        Dim buttonRow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(0, 4, 0, 0)
        }
        Dim btnApply As New Button() With {.Text = "Apply To Heal/Mana/Max-HP Rows", .Width = 220, .Height = 30, .BackColor = Color.FromArgb(42, 120, 80), .ForeColor = Color.White}
        AddHandler btnApply.Click, Sub(_s As Object, _e As EventArgs) ApplyQuickAutoPotThresholds()
        Dim btnTestAlarm As New Button() With {.Text = "Test Alarm + Phone", .Width = 150, .Height = 30, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        AddHandler btnTestAlarm.Click, AddressOf TestAlarmClicked
        Dim btnTestPhone As New Button() With {.Text = "Test Phone Alert", .Width = 130, .Height = 30, .BackColor = Color.FromArgb(55, 110, 170), .ForeColor = Color.White}
        AddHandler btnTestPhone.Click, AddressOf TestPhoneAlertClicked
        buttonRow.Controls.Add(btnApply)
        buttonRow.Controls.Add(btnTestAlarm)
        buttonRow.Controls.Add(btnTestPhone)
        settingsLayout.Controls.Add(buttonRow, 0, 1)
        settingsLayout.SetColumnSpan(buttonRow, 2)

        settingsGroup.Controls.Add(settingsLayout)
        root.Controls.Add(settingsGroup, 0, 0)
        root.Controls.Add(BuildAutoPotUnstuckGroup(), 0, 1)
        tab.Controls.Add(root)
        Return tab
    End Function

    Private Function BuildAutoPotUnstuckGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Unstuck / Retarget", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))

        Dim controlsPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 3}
        controlsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200.0F))
        controlsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))

        controlsPanel.Controls.Add(New Label() With {.Text = "Retarget Key", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        controlsPanel.Controls.Add(New Label() With {.Text = "E", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}, 1, 0)
        controlsPanel.Controls.Add(New Label() With {.Text = "Retarget Interval (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        controlsPanel.Controls.Add(New Label() With {.Text = "Stuck Target Timeout (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)

        Dim nudAutoPotRetarget As New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 50, .Maximum = 10000, .Value = 550}
        If nudRetargetMs IsNot Nothing Then
            nudAutoPotRetarget.Value = nudRetargetMs.Value
        End If
        AddHandler nudAutoPotRetarget.ValueChanged,
            Sub(_s As Object, _e As EventArgs)
                If nudRetargetMs IsNot Nothing AndAlso nudRetargetMs.Value <> nudAutoPotRetarget.Value Then
                    nudRetargetMs.Value = nudAutoPotRetarget.Value
                End If
            End Sub
        If nudRetargetMs IsNot Nothing Then
            AddHandler nudRetargetMs.ValueChanged,
                Sub(_s As Object, _e As EventArgs)
                    If nudAutoPotRetarget.Value <> nudRetargetMs.Value Then
                        nudAutoPotRetarget.Value = nudRetargetMs.Value
                    End If
                End Sub
        End If
        controlsPanel.Controls.Add(nudAutoPotRetarget, 1, 1)

        nudStuckTargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 500, .Maximum = 30000, .Value = 2200}
        AddHandler nudStuckTargetMs.ValueChanged, Sub(_s As Object, _e As EventArgs) PushLiveConfig()
        controlsPanel.Controls.Add(nudStuckTargetMs, 1, 2)

        Dim note As New Label() With {
            .Text = "Retarget Interval is mirrored from Vision. Stuck Target Timeout controls when the bot decides a target is stalled and forces a retarget.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.TopLeft,
            .ForeColor = Color.LightSteelBlue
        }
        layout.Controls.Add(controlsPanel, 0, 0)
        layout.Controls.Add(note, 1, 0)

        Dim foot As New Label() With {
            .Text = "Use a shorter timeout for crowded spots and a longer timeout for tankier mobs.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.Gray
        }
        layout.Controls.Add(foot, 0, 1)
        layout.SetColumnSpan(foot, 2)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildDiagnosticsTab() As TabPage
        Dim tab As New TabPage("Diagnostics") With {.BackColor = Color.FromArgb(20, 20, 20)}
        txtDiagnostics = New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Both, .ReadOnly = True, .Font = New Font("Consolas", 9.5F, FontStyle.Regular), .BackColor = Color.FromArgb(10, 10, 10), .ForeColor = Color.LightGray}
        tab.Controls.Add(txtDiagnostics)
        Return tab
    End Function

    Private Function BuildLevelingTab() As TabPage
        Dim tab As New TabPage("Leveling") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim scrollPanel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(8), .AutoScroll = True}
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        scrollPanel.Controls.Add(root)
        tab.Controls.Add(scrollPanel)

        Dim settingsGroup As New GroupBox() With {.Text = "Leveling Agent", .Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Padding = New Padding(10)}
        Dim settingsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 23}
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220.0F))
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For i As Integer = 0 To 22
            settingsLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        settingsGroup.Controls.Add(settingsLayout)

        chkLevelingAgent = New CheckBox() With {.Text = "Enable leveling agent", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkLevelingAgent, 0, 0)
        settingsLayout.SetColumnSpan(chkLevelingAgent, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Preferred Mobs", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        txtLevelingPreferredMobs = New TextBox() With {.Dock = DockStyle.Fill, .PlaceholderText = "mob1, mob2, mob3"}
        settingsLayout.Controls.Add(txtLevelingPreferredMobs, 1, 1)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stop HP %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudLevelingStopHp = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 100, .Value = 20, .Width = 120}
        settingsLayout.Controls.Add(nudLevelingStopHp, 1, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stop MP %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)
        nudLevelingStopMp = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 100, .Value = 10, .Width = 120}
        settingsLayout.Controls.Add(nudLevelingStopMp, 1, 3)

        settingsLayout.Controls.Add(New Label() With {.Text = "Max No Target (sec)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 4)
        nudLevelingMaxNoTargetSeconds = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 5, .Maximum = 600, .Value = 45, .Width = 120}
        settingsLayout.Controls.Add(nudLevelingMaxNoTargetSeconds, 1, 4)

        chkNavigationEnabled = New CheckBox() With {.Text = "Enable map localization", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkNavigationEnabled, 0, 5)
        settingsLayout.SetColumnSpan(chkNavigationEnabled, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Map Open Key", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 6)
        txtMapOpenKey = New TextBox() With {.Dock = DockStyle.Left, .Width = 120, .Text = DefaultMapOpenKey}
        settingsLayout.Controls.Add(txtMapOpenKey, 1, 6)

        chkTravelPreview = New CheckBox() With {.Text = "Enable travel preview route planning", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkTravelPreview, 0, 7)
        settingsLayout.SetColumnSpan(chkTravelPreview, 2)

        chkTravelExecute = New CheckBox() With {.Text = "Enable travel execution (guarded)", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkTravelExecute, 0, 8)
        settingsLayout.SetColumnSpan(chkTravelExecute, 2)

        chkRouteRecording = New CheckBox() With {.Text = "Enable route recording mode", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkRouteRecording, 0, 9)
        settingsLayout.SetColumnSpan(chkRouteRecording, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Recorded Route Name", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 10)
        Dim recordingPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True}
        txtRouteRecordingName = New TextBox() With {.Width = 220, .Text = "jina_route"}
        recordingPanel.Controls.Add(txtRouteRecordingName)
        btnSaveRouteRecording = New Button() With {.Text = "Save Recorded Route", .AutoSize = True}
        recordingPanel.Controls.Add(btnSaveRouteRecording)
        settingsLayout.Controls.Add(recordingPanel, 1, 10)

        settingsLayout.Controls.Add(New Label() With {.Text = "Recorded Routes", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 11)
        Dim recordedRoutePanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True}
        cboRecordedRoute = New ComboBox() With {.Width = 280, .DropDownStyle = ComboBoxStyle.DropDownList}
        recordedRoutePanel.Controls.Add(cboRecordedRoute)
        btnDeleteRecordedRoute = New Button() With {.Text = "Delete Route", .AutoSize = True}
        recordedRoutePanel.Controls.Add(btnDeleteRecordedRoute)
        settingsLayout.Controls.Add(recordedRoutePanel, 1, 11)

        settingsLayout.Controls.Add(New Label() With {.Text = "Recorded Route Nodes", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 12)
        Dim recordedNodePanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True}
        cboRecordedRouteNode = New ComboBox() With {.Width = 280, .DropDownStyle = ComboBoxStyle.DropDownList}
        recordedNodePanel.Controls.Add(cboRecordedRouteNode)
        btnDeleteRecordedRouteNode = New Button() With {.Text = "Delete Node", .AutoSize = True}
        recordedNodePanel.Controls.Add(btnDeleteRecordedRouteNode)
        settingsLayout.Controls.Add(recordedNodePanel, 1, 12)

        settingsLayout.Controls.Add(New Label() With {.Text = "Waypoint Radius", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 13)
        nudNavigationWaypointRadius = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 250, .Value = 36, .Width = 120}
        settingsLayout.Controls.Add(nudNavigationWaypointRadius, 1, 13)

        settingsLayout.Controls.Add(New Label() With {.Text = "Move Burst (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 14)
        nudNavigationMoveBurstMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 100, .Maximum = 1500, .Increment = 25, .Value = 350, .Width = 120}
        settingsLayout.Controls.Add(nudNavigationMoveBurstMs, 1, 14)

        settingsLayout.Controls.Add(New Label() With {.Text = "Re-sample (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 15)
        nudNavigationResampleMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 250, .Maximum = 10000, .Increment = 50, .Value = 1800, .Width = 120}
        settingsLayout.Controls.Add(nudNavigationResampleMs, 1, 15)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stall Timeout (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 16)
        nudNavigationStallTimeoutMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1500, .Maximum = 30000, .Increment = 250, .Value = 6500, .Width = 120}
        settingsLayout.Controls.Add(nudNavigationStallTimeoutMs, 1, 16)

        chkNavigationRepathOnStuck = New CheckBox() With {.Text = "Run recovery/repath when travel stalls", .Dock = DockStyle.Fill, .Checked = True}
        settingsLayout.Controls.Add(chkNavigationRepathOnStuck, 0, 17)
        settingsLayout.SetColumnSpan(chkNavigationRepathOnStuck, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Route Start", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 18)
        cboNavigationStartNode = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .Enabled = False}
        settingsLayout.Controls.Add(cboNavigationStartNode, 1, 18)

        settingsLayout.Controls.Add(New Label() With {.Text = "Travel Route", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 19)
        cboNavigationTargetNode = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        settingsLayout.Controls.Add(cboNavigationTargetNode, 1, 19)

        chkLevelingStopOnLowExp = New CheckBox() With {.Text = "Stop when EXP/hour is below threshold", .Dock = DockStyle.Fill}
        settingsLayout.Controls.Add(chkLevelingStopOnLowExp, 0, 20)
        settingsLayout.SetColumnSpan(chkLevelingStopOnLowExp, 2)
        settingsLayout.Controls.Add(New Label() With {.Text = "Min EXP/hour %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 21)
        nudLevelingMinExpPerHour = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0.01D, .Maximum = 100D, .DecimalPlaces = 2, .Increment = 0.05D, .Value = DefaultLevelingMinExpPerHour, .Width = 120}
        Dim lowExpPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True}
        lowExpPanel.Controls.Add(nudLevelingMinExpPerHour)
        chkLevelingStopOnRepeatedUnreachable = New CheckBox() With {.Text = "Stop after repeated unreachable targets", .AutoSize = True, .Margin = New Padding(16, 4, 0, 0)}
        lowExpPanel.Controls.Add(chkLevelingStopOnRepeatedUnreachable)
        nudLevelingUnreachableLimit = New NumericUpDown() With {.Minimum = 1, .Maximum = 20, .Value = 4, .Width = 70, .Margin = New Padding(8, 0, 0, 0)}
        lowExpPanel.Controls.Add(nudLevelingUnreachableLimit)
        settingsLayout.Controls.Add(lowExpPanel, 1, 21)

        Dim statusGroup As New GroupBox() With {.Text = "Agent Runtime", .Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Padding = New Padding(10)}
        Dim statusLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 1, .RowCount = 11, .Padding = New Padding(6)}
        For i As Integer = 0 To 10
            statusLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        statusGroup.Controls.Add(statusLayout)

        lblLevelingState = New Label() With {.Text = "Agent State: Disabled", .Dock = DockStyle.Fill, .ForeColor = Color.Khaki, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft}
        lblLevelingReason = New Label() With {.Text = "Reason: Leveling agent is disabled.", .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro, .AutoSize = True}
        lblMapCoordinate = New Label() With {.Text = "Map Coordinate: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightGreen, .AutoSize = True}
        lblMapHeading = New Label() With {.Text = "Map Heading: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightSkyBlue, .AutoSize = True}
        lblMapMarker = New Label() With {.Text = "Map Marker: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.Salmon, .AutoSize = True}
        lblMapLocalizationConfidence = New Label() With {.Text = "Localization Confidence: 0%", .Dock = DockStyle.Fill, .ForeColor = Color.Khaki, .AutoSize = True}
        lblTravelStatus = New Label() With {.Text = "Travel: idle", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .AutoSize = True}
        lblRoutePreview = New Label() With {.Text = "Route Preview: disabled", .Dock = DockStyle.Fill, .ForeColor = Color.LightCyan, .AutoSize = True}
        lblRouteRecording = New Label() With {.Text = "Route Recording: idle", .Dock = DockStyle.Fill, .ForeColor = Color.Plum, .AutoSize = True}
        Dim hintLabel As New Label() With {.Text = "Preferred mobs are a positive filter. When the list is not empty, the agent will skip non-matching targets.", .Dock = DockStyle.Fill, .ForeColor = Color.LightSkyBlue, .AutoSize = True}
        Dim guardrailLabel As New Label() With {.Text = "Travel execution is still guarded: it samples the map, plans a waypoint route, and sends short movement bursts only when combat is idle.", .Dock = DockStyle.Fill, .ForeColor = Color.Silver, .AutoSize = True}
        statusLayout.Controls.Add(lblLevelingState, 0, 0)
        statusLayout.Controls.Add(lblLevelingReason, 0, 1)
        statusLayout.Controls.Add(lblMapCoordinate, 0, 2)
        statusLayout.Controls.Add(lblMapHeading, 0, 3)
        statusLayout.Controls.Add(lblMapMarker, 0, 4)
        statusLayout.Controls.Add(lblMapLocalizationConfidence, 0, 5)
        statusLayout.Controls.Add(lblTravelStatus, 0, 6)
        statusLayout.Controls.Add(lblRoutePreview, 0, 7)
        statusLayout.Controls.Add(lblRouteRecording, 0, 8)
        statusLayout.Controls.Add(hintLabel, 0, 9)
        statusLayout.Controls.Add(guardrailLabel, 0, 10)

        root.Controls.Add(settingsGroup, 0, 0)
        root.Controls.Add(statusGroup, 0, 1)
        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        Return tab
    End Function

    Private Function BuildCombatSkillsGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Combat Skills", .Dock = DockStyle.Fill}
        dgvCombat = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvCombat.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled"})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Key", .ReadOnly = True, .FillWeight = 60.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CooldownSec", .FillWeight = 90.0F})
        Dim roleColumn As New DataGridViewComboBoxColumn() With {.Name = "Role", .FillWeight = 80.0F}
        roleColumn.Items.AddRange(New Object() {"attack", "heal", "max_health", "mana", "special", "high_max_hp", "stop"})
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
        txtMonsterName = New TextBox() With {.Width = 140, .PlaceholderText = "name1, name2, name3"}
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
        txtLootName = New TextBox() With {.Width = 140, .PlaceholderText = "item1, item2, item3"}
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
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12), .AutoScroll = True}
        lblRunState = New Label() With {
            .Text = "BOT PAUSED",
            .Top = 10,
            .Left = 8,
            .Width = 210,
            .Height = 30,
            .BackColor = Color.FromArgb(110, 45, 45),
            .ForeColor = Color.White,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        }
        lblShortcutHint = New Label() With {
            .Text = "Shortcut: Ctrl+Shift -> Pause / Resume",
            .Top = 44,
            .Left = 8,
            .Width = 280,
            .Height = 28,
            .ForeColor = Color.Gold,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblState = New Label() With {.Text = "Status: Searching for target...", .Top = 76, .Left = 8, .Width = 300, .Height = 22}
        lblSystem = New Label() With {.Text = "System Active: False", .Top = 104, .Left = 8, .Width = 260, .Height = 22, .ForeColor = Color.LightGreen}
        lblHp = New Label() With {.Text = "HP%: 0", .Top = 132, .Left = 8, .Width = 120, .Height = 22, .ForeColor = Color.LimeGreen}
        lblMp = New Label() With {.Text = "MP%: 0", .Top = 132, .Left = 136, .Width = 120, .Height = 22, .ForeColor = Color.DeepSkyBlue}
        lblMobName = New Label() With {.Text = "Mob: (none)", .Top = 156, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.LightSkyBlue}
        lblExpRate = New Label() With {.Text = "Prana/EXP: 0.00% | Rate: Calculating (1m)", .Top = 178, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.Khaki}
        lblRupiahsRate = New Label() With {.Text = "Rupiahs: n/a | Rate: Calculating (1m)", .Top = 200, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.Gold}
        btnAttack = New Button() With {.Text = "Attack", .Top = 234, .Left = 8, .Width = 210, .Height = 42, .BackColor = Color.FromArgb(40, 180, 80), .ForeColor = Color.White}
        btnSaveSettings = New Button() With {.Text = "Save Settings", .Top = 288, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(55, 55, 55), .ForeColor = Color.White}
        btnStopBot = New Button() With {.Text = "Stop Bot", .Top = 338, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(20, 130, 210), .ForeColor = Color.White}
        btnBypassLimits = New Button() With {.Text = "Ignore Skill Min HP/MP: OFF", .Top = 388, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        btnBypassStuck = New Button() With {
            .Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF"),
            .Top = 438,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnRetargetNow = New Button() With {.Text = "Retarget Now (E)", .Top = 488, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        btnPartyAutoAccept = New Button() With {
            .Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF"),
            .Top = 538,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        Dim lblPartyAskEvery As New Label() With {.Text = "Ask Party Every (sec)", .Top = 584, .Left = 8, .Width = 210, .Height = 22}
        nudPartyAskSeconds = New NumericUpDown() With {.Top = 606, .Left = 8, .Width = 210, .Height = 28, .Minimum = 5, .Maximum = 600, .Value = 30}
        Dim lblPartyAskText As New Label() With {.Text = "Auto Ask Party Text", .Top = 640, .Left = 8, .Width = 210, .Height = 22}
        txtPartyAskText = New TextBox() With {.Top = 662, .Left = 8, .Width = 210, .Height = 28, .Text = DefaultPartyAskCommand}
        btnPartyAsk = New Button() With {
            .Text = If(_partyAskEnabled, "Auto Ask Party (add): ON", "Auto Ask Party (add): OFF"),
            .Top = 696,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnLootScanner = New Button() With {
            .Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF"),
            .Top = 746,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnHelp = New Button() With {
            .Text = "Help (EN/ES/FIL)",
            .Top = 796,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = Color.FromArgb(70, 70, 70),
            .ForeColor = Color.White
        }
        AddHandler btnAttack.Click, AddressOf StartClicked
        AddHandler btnSaveSettings.Click, AddressOf SaveClicked
        AddHandler btnStopBot.Click, AddressOf StopClicked
        AddHandler btnBypassLimits.Click, AddressOf ToggleBypassLimitsClicked
        AddHandler btnBypassStuck.Click, AddressOf ToggleStuckTargetBypassClicked
        AddHandler btnRetargetNow.Click, AddressOf ManualRetargetClicked
        AddHandler btnPartyAutoAccept.Click, AddressOf TogglePartyAutoAcceptClicked
                AddHandler btnPartyAsk.Click, AddressOf TogglePartyAskClicked
        AddHandler btnLootScanner.Click, AddressOf ToggleLootScannerClicked
        AddHandler txtPartyAskText.TextChanged, AddressOf PartyAskTextChanged
        AddHandler btnHelp.Click, AddressOf HelpClicked
        panel.Controls.Add(lblRunState)
        panel.Controls.Add(lblShortcutHint)
        panel.Controls.Add(lblState)
        panel.Controls.Add(lblSystem)
        panel.Controls.Add(lblHp)
        panel.Controls.Add(lblMp)
        panel.Controls.Add(lblMobName)
        panel.Controls.Add(lblExpRate)
        panel.Controls.Add(lblRupiahsRate)
        panel.Controls.Add(btnAttack)
        panel.Controls.Add(btnSaveSettings)
        panel.Controls.Add(btnStopBot)
        panel.Controls.Add(btnBypassLimits)
        panel.Controls.Add(btnBypassStuck)
        panel.Controls.Add(btnRetargetNow)
        panel.Controls.Add(btnPartyAutoAccept)
        panel.Controls.Add(lblPartyAskEvery)
        panel.Controls.Add(nudPartyAskSeconds)
        panel.Controls.Add(lblPartyAskText)
        panel.Controls.Add(txtPartyAskText)
                panel.Controls.Add(btnPartyAsk)
        panel.Controls.Add(btnLootScanner)
        panel.Controls.Add(btnHelp)
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
        dgvRegions.Rows.Add("rupiahs_rect", "560", "745", "110", "21")
        dgvRegions.Rows.Add("party_invite_scan_rect", "349", "318", "328", "124")
        dgvRegions.Rows.Add("party_invite_ok_rect", "463", "410", "59", "21")
        dgvRegions.Rows.Add("map_rect", "0", "0", "1024", "768")
        dgvRegions.Rows.Add("map_coordinate_rect", "6", "744", "120", "22")
        If txtLootScanAreaPoints IsNot Nothing Then
            txtLootScanAreaPoints.Text = FormatLootScanPoints(BotConfig.CreateDefaultLootScanPoints())
        End If
        If txtMapOpenKey IsNot Nothing Then
            txtMapOpenKey.Text = DefaultMapOpenKey
        End If
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
        For i As Integer = 0 To CustomCombatDefaultKeys.Length - 1
            Dim customKey As String = CustomCombatDefaultKeys(i)
            dgvCombat.Rows.Add(False, customKey, "1.0", "special", keyIndex * 10, 40, 1, 1)
            Dim customRow As DataGridViewRow = dgvCombat.Rows(dgvCombat.Rows.Count - 1)
            customRow.Cells("Key").ReadOnly = False
            keyIndex += 1
        Next
        If Not MonsterExists("avara kara") Then
            lstMonsterFilter.Items.Add("avara kara")
        End If
        If txtNtfyTopic IsNot Nothing Then
            txtNtfyTopic.Text = DefaultNtfyTopicName
        End If
        If nudPartyAskSeconds IsNot Nothing Then
            nudPartyAskSeconds.Value = 30
        End If
        If txtPartyAskText IsNot Nothing Then
            txtPartyAskText.Text = DefaultPartyAskCommand
        End If
        _alarmVolumePercent = CInt(nudAlarmVolume.Value)
        UpdateAttackButtonAppearance(False)
        UpdatePromptAutoAcceptButton()
        UpdatePartyAskButton()
        UpdateLootRejectPointUi()
        RefreshKeyActionSummary()
        AppendLog("UI loaded. No API required.")
        AppendLog("Shortcut active: Ctrl+Shift toggles pause/resume.")
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        CommitPendingGridEdits()
        PushLiveConfig()
        SavePersistedListState(True, True)
        AppendLog("Settings saved (engine + disk).")
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
        BeginNotificationWarmup()
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
        BeginNotificationWarmup()
        PushLiveConfig()
        _engine.Start()
        UpdateAttackButtonAppearance(True)
        AppendLog("Auto-start on launch enabled.")
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        RefreshProcessWindowList(False, IntPtr.Zero)
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
        _notificationWarmupUntilUtc = DateTime.MinValue
        ApplyHealthUiTint(100.0, False)
        ResetHpZeroAlarmState("Alarm state reset for bot stop.")
        UpdateAttackButtonAppearance(False)
    End Sub

    Private Sub CommitPendingGridEdits()
        If dgvCombat IsNot Nothing Then
            Try
                If dgvCombat.IsCurrentCellDirty Then
                    dgvCombat.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
                dgvCombat.EndEdit()
            Catch
            End Try
        End If

        If dgvRegions IsNot Nothing Then
            Try
                If dgvRegions.IsCurrentCellDirty Then
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
                dgvRegions.EndEdit()
            Catch
            End Try
        End If
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

    Private Sub PickLootRejectPointClicked(sender As Object, e As EventArgs)
        _isPickingLootRejectPoint = True
        UpdateLootRejectPointUi()
        If picSnapshot Is Nothing OrElse picSnapshot.Image Is Nothing Then
            AppendLog("Pick mode enabled. Capture Snapshot first, then click the reject button point.")
            Return
        End If
        AppendLog("Pick mode enabled. Click the reject button point on Snapshot.")
    End Sub

    Private Sub ClearLootRejectPointClicked(sender As Object, e As EventArgs)
        _isPickingLootRejectPoint = False
        _lootRejectPointX = -1
        _lootRejectPointY = -1
        UpdateLootRejectPointUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog("Loot reject click point cleared.")
    End Sub

    Private Sub SnapshotMouseClick(sender As Object, e As MouseEventArgs)
        If Not _isPickingLootRejectPoint Then
            Return
        End If
        If picSnapshot Is Nothing OrElse picSnapshot.Image Is Nothing Then
            AppendLog("Pick failed: capture a snapshot first.")
            Return
        End If

        Dim mapped As System.Drawing.Point
        If Not TryMapPictureBoxPointToImage(picSnapshot, e.Location, mapped) Then
            AppendLog("Pick failed: click inside the snapshot image area.")
            Return
        End If

        _lootRejectPointX = mapped.X
        _lootRejectPointY = mapped.Y
        _isPickingLootRejectPoint = False
        UpdateLootRejectPointUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog($"Loot reject point set: x={_lootRejectPointX}, y={_lootRejectPointY}.")
    End Sub

    Private Shared Function TryMapPictureBoxPointToImage(picture As PictureBox, clientPoint As System.Drawing.Point, ByRef imagePoint As System.Drawing.Point) As Boolean
        imagePoint = New System.Drawing.Point(0, 0)
        If picture Is Nothing OrElse picture.Image Is Nothing Then
            Return False
        End If

        Dim imageWidth As Integer = picture.Image.Width
        Dim imageHeight As Integer = picture.Image.Height
        Dim boxWidth As Integer = Math.Max(1, picture.ClientSize.Width)
        Dim boxHeight As Integer = Math.Max(1, picture.ClientSize.Height)
        Dim scale As Double = Math.Min(boxWidth / CDbl(imageWidth), boxHeight / CDbl(imageHeight))
        If scale <= 0 Then
            Return False
        End If

        Dim drawWidth As Integer = CInt(Math.Round(imageWidth * scale))
        Dim drawHeight As Integer = CInt(Math.Round(imageHeight * scale))
        Dim offsetX As Integer = (boxWidth - drawWidth) \ 2
        Dim offsetY As Integer = (boxHeight - drawHeight) \ 2
        Dim drawRect As New System.Drawing.Rectangle(offsetX, offsetY, drawWidth, drawHeight)
        If Not drawRect.Contains(clientPoint) Then
            Return False
        End If

        Dim px As Integer = CInt(Math.Floor((clientPoint.X - offsetX) / scale))
        Dim py As Integer = CInt(Math.Floor((clientPoint.Y - offsetY) / scale))
        px = Math.Max(0, Math.Min(imageWidth - 1, px))
        py = Math.Max(0, Math.Min(imageHeight - 1, py))
        imagePoint = New System.Drawing.Point(px, py)
        Return True
    End Function

    Private Sub UpdateLootRejectPointUi()
        If lblLootRejectPoint IsNot Nothing Then
            If _lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0 Then
                lblLootRejectPoint.Text = $"Loot Reject Point: {_lootRejectPointX}, {_lootRejectPointY}"
            Else
                lblLootRejectPoint.Text = "Loot Reject Point: (not set)"
            End If
        End If

        If btnPickLootRejectPoint IsNot Nothing Then
            btnPickLootRejectPoint.Text = If(_isPickingLootRejectPoint, "Click Snapshot...", "Pick Loot Reject Point")
            btnPickLootRejectPoint.BackColor = If(_isPickingLootRejectPoint, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
        End If

        If btnClearLootRejectPoint IsNot Nothing Then
            btnClearLootRejectPoint.Enabled = (_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0)
        End If

        If picSnapshot IsNot Nothing Then
            picSnapshot.Cursor = If(_isPickingLootRejectPoint, Cursors.Cross, Cursors.Default)
        End If
    End Sub

    Private Sub RefreshProcessListClicked(sender As Object, e As EventArgs)
        RefreshProcessWindowList(True, IntPtr.Zero)
    End Sub

    Private Sub ProcessSelectionChanged(sender As Object, e As EventArgs)
        Dim selected As ProcessWindowEntry = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
        If selected Is Nothing Then
            Return
        End If

        If txtProcessRename IsNot Nothing AndAlso Not txtProcessRename.IsDisposed Then
            txtProcessRename.Text = selected.WindowTitle
        End If
    End Sub

    Private Sub ApplyProcessRenameClicked(sender As Object, e As EventArgs)
        If lstProcessWindows Is Nothing OrElse lstProcessWindows.IsDisposed Then
            Return
        End If

        Dim selected As ProcessWindowEntry = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
        If selected Is Nothing Then
            AppendLog("Rename failed: select a process window first.")
            Return
        End If

        Dim newTitle As String = If(txtProcessRename IsNot Nothing, txtProcessRename.Text, "").Trim()
        If newTitle = "" Then
            AppendLog("Rename failed: title cannot be empty.")
            Return
        End If

        If SetWindowText(selected.MainWindowHandle, newTitle) Then
            AppendLog($"Window renamed for PID {selected.ProcessId}: '{newTitle}'.")
            txtWindowTitle.Text = newTitle
            RefreshProcessWindowList(False, selected.MainWindowHandle)
            Return
        End If

        Dim errorCode As Integer = Marshal.GetLastWin32Error()
        AppendLog($"Rename failed for PID {selected.ProcessId}. Win32 error {errorCode}.")
    End Sub

    Private Sub RefreshProcessWindowList(logResult As Boolean, preferredHandle As IntPtr)
        If lstProcessWindows Is Nothing OrElse lstProcessWindows.IsDisposed Then
            Return
        End If

        Dim rememberedHandle As IntPtr = preferredHandle
        If rememberedHandle = IntPtr.Zero Then
            Dim existing As ProcessWindowEntry = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
            If existing IsNot Nothing Then
                rememberedHandle = existing.MainWindowHandle
            End If
        End If

        Dim entries As New List(Of ProcessWindowEntry)()
        Dim processes As Process() = Process.GetProcesses()
        For Each proc As Process In processes
            Try
                Dim hwnd As IntPtr = proc.MainWindowHandle
                Dim title As String = If(proc.MainWindowTitle, "").Trim()
                If hwnd = IntPtr.Zero OrElse title = "" Then
                    Continue For
                End If

                entries.Add(New ProcessWindowEntry With {
                    .ProcessId = proc.Id,
                    .ProcessName = proc.ProcessName,
                    .WindowTitle = title,
                    .MainWindowHandle = hwnd
                })
            Catch
            Finally
                proc.Dispose()
            End Try
        Next

        entries.Sort(
            Function(a As ProcessWindowEntry, b As ProcessWindowEntry) As Integer
                Dim byTitle As Integer = StringComparer.OrdinalIgnoreCase.Compare(a.WindowTitle, b.WindowTitle)
                If byTitle <> 0 Then
                    Return byTitle
                End If
                Dim byProcess As Integer = StringComparer.OrdinalIgnoreCase.Compare(a.ProcessName, b.ProcessName)
                If byProcess <> 0 Then
                    Return byProcess
                End If
                Return a.ProcessId.CompareTo(b.ProcessId)
            End Function)

        lstProcessWindows.BeginUpdate()
        Try
            lstProcessWindows.Items.Clear()
            For Each entry As ProcessWindowEntry In entries
                lstProcessWindows.Items.Add(entry)
            Next

            If entries.Count > 0 Then
                Dim targetIndex As Integer = -1
                If rememberedHandle <> IntPtr.Zero Then
                    For i As Integer = 0 To entries.Count - 1
                        If entries(i).MainWindowHandle = rememberedHandle Then
                            targetIndex = i
                            Exit For
                        End If
                    Next
                End If

                If targetIndex < 0 Then
                    targetIndex = 0
                End If
                lstProcessWindows.SelectedIndex = targetIndex
            End If
        Finally
            lstProcessWindows.EndUpdate()
        End Try

        If logResult Then
            AppendLog($"Process list updated. Found {entries.Count} windows.")
        End If
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
        SavePersistedListState(False)
        AppendLog(If(_partyAutoAccept, "Party/resurrection auto-accept enabled.", "Party/resurrection auto-accept disabled."))
    End Sub

    Private Sub UpdatePromptAutoAcceptButton()
        If btnPartyAutoAccept Is Nothing Then
            Return
        End If
        btnPartyAutoAccept.Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF")
        btnPartyAutoAccept.BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
    End Sub

    Private Sub TogglePartyAskClicked(sender As Object, e As EventArgs)
        _partyAskEnabled = Not _partyAskEnabled
        btnPartyAsk.Text = If(_partyAskEnabled, "Auto Ask Party (add): ON", "Auto Ask Party (add): OFF")
        btnPartyAsk.BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Sub ToggleLootScannerClicked(sender As Object, e As EventArgs)
        _lootScannerEnabled = Not _lootScannerEnabled
        btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
        btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Sub UpdatePartyAskButton()
        If btnPartyAsk Is Nothing Then
            Return
        End If
        Dim commandLabel As String = GetPartyAskCommandText()
        If commandLabel.Length > 14 Then
            commandLabel = commandLabel.Substring(0, 11) & "..."
        End If
        btnPartyAsk.Text = If(_partyAskEnabled, $"Auto Ask Party ({commandLabel}): ON", $"Auto Ask Party ({commandLabel}): OFF")
        btnPartyAsk.BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
    End Sub

    Private Sub PartyAskTextChanged(_sender As Object, _e As EventArgs)
        UpdatePartyAskButton()
    End Sub

    Private Function GetPartyAskCommandText() As String
        Dim rawText As String = If(txtPartyAskText IsNot Nothing, txtPartyAskText.Text, DefaultPartyAskCommand)
        rawText = rawText.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        If rawText = "" Then
            Return DefaultPartyAskCommand
        End If
        Return rawText
    End Function

    Private Sub HelpClicked(sender As Object, e As EventArgs)
        Dim helpForm As New Form() With {
            .Text = "KathanaBot Help",
            .StartPosition = FormStartPosition.CenterParent,
            .Width = 980,
            .Height = 760,
            .MinimizeBox = False,
            .MaximizeBox = True,
            .BackColor = Color.FromArgb(20, 20, 20),
            .ForeColor = Color.Gainsboro
        }

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        tabs.TabPages.Add(CreateHelpTabPage("English", BuildHelpTextEnglish()))
        tabs.TabPages.Add(CreateHelpTabPage("Espanol", BuildHelpTextSpanish()))
        tabs.TabPages.Add(CreateHelpTabPage("Filipino", BuildHelpTextFilipino()))
        helpForm.Controls.Add(tabs)

        helpForm.ShowDialog(Me)
    End Sub

    Private Function CreateHelpTabPage(title As String, body As String) As TabPage
        Dim tab As New TabPage(title) With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim text As New TextBox() With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .WordWrap = False,
            .Font = New Font("Consolas", 9.5F, FontStyle.Regular),
            .BackColor = Color.FromArgb(10, 10, 10),
            .ForeColor = Color.Gainsboro,
            .Text = body
        }
        tab.Controls.Add(text)
        Return tab
    End Function

    Private Shared Function BuildHelpTextEnglish() As String
        Return String.Join(Environment.NewLine, New String() {
            "KATHANABOT - COMPLETE FEATURE GUIDE (ENGLISH)",
            "============================================================",
            "",
            "1) QUICK START",
            "- Open game in windowed mode.",
            "- Verify Window Title in Vision tab.",
            "- Press Attack to start bot loop.",
            "- Press Stop Bot to hard stop movement and stop loop.",
            "- You can also toggle pause/resume with Ctrl+Shift when game or control panel is focused.",
            "",
            "2) COMBAT TAB - COMBAT SKILLS GRID",
            "- Enabled: if checked, action is available.",
            "- Key: keyboard key sent to game (1-0, F1-F10 plus 3 custom rows after F10).",
            "- CooldownSec: minimum seconds between sends of this key.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, stop.",
            "- Priority: lower values act first inside same category checks.",
            "- TriggerPercent: role threshold (heal/mana/max_health use this heavily).",
            "- MinHpPercent / MinMpPercent: minimum self HP/MP to allow this action.",
            "- high_max_hp only fires when enabled in Vision and mob_hp_rect OCR reads Max HP above your threshold.",
            "",
            "3) COMBAT TAB - MONSTER FILTER",
            "- Enable Monster Filter (blacklist): active deny list for mob names.",
            "- Add / Remove: manage blocked mob names.",
            "- OCR + confirmation logic avoids stale or wrong-name attacks.",
            "",
            "4) COMBAT TAB - LOOT FILTER",
            "- Loot pickup toggle and interval seconds.",
            "- Add / Remove loot names to allow-list.",
            "- Loot Name Match % (Auto-Pot tab) sets fuzzy OCR matching threshold for loot names (default 80%).",
            "- Loot reject point can be picked from snapshot to click reject button.",
            "",
            "5) CENTER CONTROL PANEL",
            "- Attack: starts engine.",
            "- Save Settings: pushes live config and persists list settings.",
            "- Stop Bot: sends hard stop macro then stops engine.",
            "- Ignore Skill Min HP/MP: bypasses min HP/MP row checks.",
            "- Auto Retarget If Stuck: allows stuck-target bypass logic.",
            "- Retarget Now (E): manual retarget key.",
            "- Auto Accept Party/Ress: toggle OCR prompt auto accept.",
            "- Ask Party Every (sec) + Auto Ask Party Text + Auto Ask Party: periodic custom command.",
            "- Help (EN/ES/FIL): opens this multilingual guide.",
            "",
            "6) VISION TAB - VISION + WINDOW SETUP",
            "- Window Title: used to find game window.",
            "- Loop (ms): bot loop delay.",
            "- Retarget (ms): baseline retarget interval.",
            "- Mob HP Presence %: threshold for valid target HP bar signal.",
            "- Show Overlay: live region calibration overlay.",
            "- Capture Snapshot: captures current client image.",
            "",
            "7) VISION TAB - CALIBRATION REGIONS",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, unreachable_text_rect,",
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect.",
            "- Loot Scan Area uses 4 freeform points: x,y | x,y | x,y | x,y.",
            "- You can edit coordinates directly in grid or through overlay.",
            "",
            "8) VISION TAB - PROCESS LIST",
            "- Update: refreshes window/process list.",
            "- Selecting process fills Rename Process input.",
            "- Apply: attempts SetWindowText rename for selected process.",
            "",
            "9) SNAPSHOT PANEL",
            "- Displays latest captured frame.",
            "",
            "10) AUTO-POT TAB",
            "- Heal Trigger %, Mana Trigger % quick sliders.",
            "- HP=0 Alarm Volume % sets system alarm pulse volume for death alert.",
            "- ntfy Channel is used for phone notifications (ntfy.sh topic).",
            "- Apply To Heal/Mana/Max-HP Rows applies quick thresholds to matching roles.",
            "- Test Alarm + Phone tests sound and ntfy message.",
            "- Test Phone Alert sends only ntfy test.",
            "",
            "11) AUTO-POT TAB - UNSTUCK / RETARGET",
            "- Retarget Interval (ms) mirrors Vision tab Retarget (ms).",
            "- Stuck Target Timeout (ms) controls when stuck-target bypass retarget can fire.",
            "",
            "12) DIAGNOSTICS TAB",
            "- Live internal state: running flags, hp/mp, target state, OCR error,",
            "  alerts state, recent action and error fields.",
            "",
            "13) LOG PANEL - REAL-TIME",
            "- Real-time event log from engine and UI.",
            "- Clear Log button resets visible log output only.",
            "",
            "14) LOG PANEL - KEY SUMMARY",
            "- Rolling counts for key actions: 10m / 30m / 60m.",
            "- Latest action text per key.",
            "- Reset Key Summary clears tracked key events.",
            "",
            "15) ALERTS AND SAFETY",
            "- HP zero death detection: requires stable confirmation before death alarm.",
            "- Death alert: plays sound, sends ntfy alert, then stops bot to avoid repeats.",
            "- Window missing/crash alert: sends separate ntfy message when game window",
            "  is not found while running (one-shot latch until recovery).",
            "",
            "16) ENGINE AUTOMATION BEHAVIORS",
            "- Auto retarget when no valid target.",
            "- First-hit window logic to avoid premature retarget on fresh target.",
            "- Vision stability filter to reduce capture glitch spikes.",
            "- OCR based target name reading with confirmation.",
            "- OCR based unreachable target detection and forced retarget.",
            "- Party invite / resurrection prompt OCR and auto accept click.",
            "- Party ask command automation with cooldown and in-party suppression.",
            "- Loot scan with fuzzy OCR allow-list matching (Loot Name Match %), reject handling by click point or fallback key.",
            "- Periodic snapshot save every ~15 minutes to Pictures/KathanaBot.",
            "- Prana/EXP OCR reading and hourly rate calculation.",
            "",
            "17) PERSISTENCE",
            "- User list state saved to AppData/KathanaBotControlPanel/user_lists.json.",
            "- Includes filter toggles, lists, loot reject point, party settings, combat rows.",
            "",
            "18) HOTKEY BEHAVIOR",
            "- Ctrl+Shift toggles pause/resume when game or control panel is focused.",
            "",
            "19) TROUBLESHOOTING",
            "- If no actions happen: verify window title and capture snapshot first.",
            "- If wrong targets: recalibrate regions and review monster filter list.",
            "- If no phone alerts: verify ntfy channel text and internet access.",
            "- If process rename fails: target app may reject window title changes."
        })
    End Function

    Private Shared Function BuildHelpTextSpanish() As String
        Return String.Join(Environment.NewLine, New String() {
            "KATHANABOT - GUIA COMPLETA DE FUNCIONES (ESPANOL)",
            "============================================================",
            "",
            "1) INICIO RAPIDO",
            "- Abre el juego en modo ventana.",
            "- Verifica Window Title en la pestana Vision.",
            "- Presiona Attack para iniciar el bot.",
            "- Presiona Stop Bot para detener movimiento y loop.",
            "- Tambien puedes usar Ctrl+Shift para pausar/reanudar cuando el juego o el panel tienen foco.",
            "",
            "2) PESTANA COMBAT - TABLA COMBAT SKILLS",
            "- Enabled: activa/desactiva la accion.",
            "- Key: tecla enviada al juego.",
            "- CooldownSec: tiempo minimo entre envios de la tecla.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, stop.",
            "- Priority: orden de prioridad.",
            "- TriggerPercent: umbral principal para roles de soporte.",
            "- MinHpPercent / MinMpPercent: minimos para permitir la accion.",
            "- high_max_hp solo dispara si esta activo en Vision y el OCR de mob_hp_rect lee Max HP arriba del umbral.",
            "",
            "3) FILTRO DE MONSTRUOS",
            "- Enable Monster Filter (blacklist): lista negra de mobs.",
            "- Add / Remove: agrega o elimina nombres.",
            "- El OCR y confirmacion reducen ataques por nombre incorrecto.",
            "",
            "4) FILTRO DE LOOT",
            "- Activar loot y definir intervalo en segundos.",
            "- Lista de nombres permitidos para recoger.",
            "- Loot Name Match % (pestana Auto-Pot) define el umbral de coincidencia OCR difusa para loot (80% por defecto).",
            "- Punto de rechazo de loot configurable desde snapshot.",
            "",
            "5) PANEL CENTRAL",
            "- Attack, Save Settings, Stop Bot.",
            "- Ignore Skill Min HP/MP: ignora minimos de filas de skills.",
            "- Auto Retarget If Stuck: recuperacion por objetivo atascado.",
            "- Retarget Now (E): retarget manual.",
            "- Auto Accept Party/Ress: aceptar prompts por OCR.",
            "- Ask Party Every (sec) + Auto Ask Party Text + Auto Ask Party: comando periodico personalizable.",
            "- Help (EN/ES/FIL): abre esta guia.",
            "",
            "6) PESTANA VISION",
            "- Window Title, Loop(ms), Retarget(ms), Mob HP Presence%.",
            "- Show Overlay para calibracion visual.",
            "- Capture Snapshot para capturar imagen del cliente.",
            "",
            "7) REGIONES DE CALIBRACION",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, unreachable_text_rect,",
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect.",
            "- Loot Scan Area usa 4 puntos libres: x,y | x,y | x,y | x,y.",
            "- Puedes editar coordenadas en tabla o con overlay.",
            "",
            "8) PROCESS LIST",
            "- Update refresca ventanas/procesos.",
            "- Rename Process + Apply intenta renombrar la ventana seleccionada.",
            "",
            "9) SNAPSHOT",
            "- Muestra la ultima captura.",
            "",
            "10) PESTANA AUTO-POT",
            "- Heal Trigger %, Mana Trigger %. ",
            "- HP=0 Alarm Volume % para volumen de alarma.",
            "- ntfy Channel para notificaciones al telefono.",
            "- Apply To Heal/Mana/Max-HP Rows aplica umbrales rapidos.",
            "- Test Alarm + Phone y Test Phone Alert para pruebas.",
            "",
            "11) PESTANA AUTO-POT - UNSTUCK / RETARGET",
            "- Retarget Interval (ms) refleja el valor Retarget(ms) de Vision.",
            "- Stuck Target Timeout (ms) controla cuando se activa el bypass por objetivo atascado.",
            "",
            "12) PESTANA DIAGNOSTICS",
            "- Muestra estado interno completo en tiempo real.",
            "",
            "13) PANEL LOG - REAL-TIME",
            "- Eventos de motor y UI en vivo.",
            "- Clear Log limpia solo la vista del log.",
            "",
            "14) PANEL LOG - KEY SUMMARY",
            "- Conteo por tecla en ventanas 10m/30m/60m.",
            "- Reset Key Summary reinicia estadisticas.",
            "",
            "15) ALERTAS",
            "- Alerta de muerte por HP=0 con confirmacion estable.",
            "- Reproduce alarma, envia ntfy y detiene bot para evitar repeticion.",
            "- Alerta separada cuando no se encuentra ventana del juego (posible crash).",
            "",
            "16) AUTOMATIZACION DEL MOTOR",
            "- Retarget automatico sin objetivo valido.",
            "- Logica de primera accion para evitar retarget prematuro.",
            "- Filtro de estabilidad de vision contra capturas defectuosas.",
            "- OCR para nombre de mob y confirmacion.",
            "- OCR para objetivo inalcanzable y retarget forzado.",
            "- OCR para party/ress y click de auto-aceptar.",
            "- Auto comando add con cooldown y pausa si ya esta en party.",
            "- Escaneo de loot con coincidencia OCR difusa configurable (Loot Name Match %), rechazo por click o tecla.",
            "- Snapshot periodico cada ~15 minutos.",
            "- Lectura OCR de Prana/EXP y calculo de tasa por hora.",
            "",
            "17) PERSISTENCIA",
            "- Guarda estado en AppData/KathanaBotControlPanel/user_lists.json.",
            "",
            "18) ATAJO CTRL+SHIFT",
            "- Ctrl+Shift alterna pausa/reanudar cuando el juego o el panel estan en foco.",
            "",
            "19) SOLUCION DE PROBLEMAS",
            "- Sin acciones: valida Window Title y prueba Capture Snapshot.",
            "- Objetivos incorrectos: recalibra regiones y revisa filtro de monstruos.",
            "- Sin alertas al telefono: revisa canal ntfy e internet.",
            "- Error al renombrar proceso: algunas apps bloquean SetWindowText."
        })
    End Function

    Private Shared Function BuildHelpTextFilipino() As String
        Return String.Join(Environment.NewLine, New String() {
            "KATHANABOT - KUMPLETONG GABAY SA MGA FUNCTION (FILIPINO)",
            "============================================================",
            "",
            "1) MABILIS NA SETUP",
            "- Buksan ang game sa windowed mode.",
            "- I-check ang Window Title sa Vision tab.",
            "- Pindutin ang Attack para simulan ang bot.",
            "- Pindutin ang Stop Bot para ihinto ang movement at loop.",
            "- Puwede ring Ctrl+Shift para pause/resume kapag focused ang game o control panel.",
            "",
            "2) COMBAT TAB - COMBAT SKILLS TABLE",
            "- Enabled: naka-on o naka-off ang action.",
            "- Key: key na ipapadala sa game.",
            "- CooldownSec: minimum na pagitan bago ulitin ang key.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, stop.",
            "- Priority: pagkakasunod ng aksyon.",
            "- TriggerPercent: pangunahing threshold ng support actions.",
            "- MinHpPercent / MinMpPercent: minimum HP/MP para payagan ang action.",
            "- high_max_hp gagana lang kapag naka-enable sa Vision at nabasa ng mob_hp_rect OCR ang Max HP lampas sa threshold mo.",
            "",
            "3) MONSTER FILTER",
            "- Enable Monster Filter (blacklist): listahan ng bawal at i-skip na mobs.",
            "- Add / Remove: dagdag o tanggal ng pangalan.",
            "- May OCR confirm para iwas maling target dahil sa stale text.",
            "",
            "4) LOOT FILTER",
            "- Toggle ng loot pickup at interval in seconds.",
            "- Allowed loot names list.",
            "- Loot Name Match % (Auto-Pot tab) sets fuzzy OCR match threshold for loot names (default 80%).",
            "- Loot reject point na puwedeng piliin mula sa snapshot image.",
            "",
            "5) CENTER CONTROL PANEL",
            "- Attack, Save Settings, Stop Bot.",
            "- Ignore Skill Min HP/MP: i-bypass ang minimum requirements ng skills.",
            "- Auto Retarget If Stuck: auto recover kapag stuck ang target.",
            "- Retarget Now (E): manual retarget.",
            "- Auto Accept Party/Ress: auto accept prompts gamit OCR.",
            "- Ask Party Every (sec) + Auto Ask Party Text + Auto Ask Party: periodic custom command.",
            "- Help (EN/ES/FIL): bubuksan ang multilingual guide.",
            "",
            "6) VISION TAB",
            "- Window Title, Loop(ms), Retarget(ms), Mob HP Presence%.",
            "- Show Overlay para madaling calibration ng regions.",
            "- Capture Snapshot para kumuha ng current game image.",
            "",
            "7) CALIBRATION REGIONS",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, unreachable_text_rect,",
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect.",
            "- Loot Scan Area ay 4 na freeform points: x,y | x,y | x,y | x,y.",
            "- Puwedeng i-edit sa grid o sa overlay.",
            "",
            "8) PROCESS LIST",
            "- Update: refresh process/window list.",
            "- Rename Process + Apply: tangkang palitan ang window title ng selected app.",
            "",
            "9) SNAPSHOT PANEL",
            "- Ipinapakita ang latest capture.",
            "",
            "10) AUTO-POT TAB",
            "- Heal Trigger %, Mana Trigger % quick controls.",
            "- HP=0 Alarm Volume % para sa death alarm volume.",
            "- ntfy Channel para sa phone notifications.",
            "- Apply To Heal/Mana/Max-HP Rows para sa mabilis na threshold apply.",
            "- Test Alarm + Phone at Test Phone Alert para sa testing.",
            "",
            "11) AUTO-POT TAB - UNSTUCK / RETARGET",
            "- Retarget Interval (ms) naka-sync sa Retarget(ms) ng Vision tab.",
            "- Stuck Target Timeout (ms) ang threshold para mag-fire ang stuck-target bypass retarget.",
            "",
            "12) DIAGNOSTICS TAB",
            "- Real-time internal status: running flags, hp/mp, target info, OCR error,",
            "  action/error fields at alert states.",
            "",
            "13) LOG PANEL - REAL-TIME",
            "- Live logs mula engine at UI.",
            "- Clear Log para linisin ang visible log.",
            "",
            "14) LOG PANEL - KEY SUMMARY",
            "- Rolling stats ng key usage sa 10m / 30m / 60m.",
            "- Reset Key Summary para i-reset ang counters.",
            "",
            "15) ALERTS",
            "- Death alert kapag HP=0 na confirmed.",
            "- Magpapatunog, magpapadala ng ntfy, at hihinto ang bot para iwas repeat.",
            "- Hiwalay na crash/window-missing alert kapag hindi makita ang game window.",
            "",
            "16) ENGINE AUTOMATION",
            "- Auto retarget kapag invalid o walang target.",
            "- First-hit window para hindi agad mali ang retarget timing.",
            "- Vision stability filter laban sa capture glitches.",
            "- OCR para sa mob name + confirmation logic.",
            "- OCR para sa unreachable text at forced retarget.",
            "- OCR party/ress detection at auto accept click.",
            "- Auto add party command na may cooldown at suppression kapag nasa party na.",
            "- Loot scan with configurable fuzzy OCR allow-list matching (Loot Name Match %), reject handling (click point/fallback key).",
            "- Periodic snapshot save bawat ~15 minuto.",
            "- Prana/EXP OCR at hourly rate calculation.",
            "",
            "17) SAVE/PERSISTENCE",
            "- Naka-save ang list/config state sa AppData/KathanaBotControlPanel/user_lists.json.",
            "",
            "18) CTRL+SHIFT HOTKEY",
            "- Ctrl+Shift pause/resume toggle gumagana kapag active ang game o control panel.",
            "",
            "19) TROUBLESHOOTING",
            "- Walang action: i-check Window Title at subukan ang Capture Snapshot.",
            "- Maling target: i-recalibrate regions at ayusin monster filter.",
            "- Walang phone alert: i-check ntfy channel at internet.",
            "- Rename fail: may apps na hindi pumapayag sa window title change."
        })
    End Function

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
        AddHandler _overlayForm.OverlayLootScanAreaChanged, AddressOf OverlayLootScanAreaChanged
        AddHandler _overlayForm.OverlayLootScanAreaCommitted, AddressOf OverlayLootScanAreaCommitted
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

    Private Sub OverlayLootScanAreaChanged(points As List(Of LootScanPoint))
        If InvokeRequired Then
            BeginInvoke(New Action(Of List(Of LootScanPoint))(AddressOf OverlayLootScanAreaChanged), points)
            Return
        End If

        UpdateLootScanAreaText(points)
        PushLiveConfig()
    End Sub

    Private Sub OverlayLootScanAreaCommitted(points As List(Of LootScanPoint))
        If InvokeRequired Then
            BeginInvoke(New Action(Of List(Of LootScanPoint))(AddressOf OverlayLootScanAreaCommitted), points)
            Return
        End If

        UpdateLootScanAreaText(points)
        PushLiveConfig()
        AppendLog("Overlay updated loot_scan_area: " & FormatLootScanPoints(points))
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

    Private Sub UpdateLootScanAreaText(points As List(Of LootScanPoint))
        If txtLootScanAreaPoints Is Nothing Then
            Return
        End If

        txtLootScanAreaPoints.Text = FormatLootScanPoints(points)
    End Sub

    Private Sub UiTimerTick(sender As Object, e As EventArgs)
        PushLiveConfig()
        Dim st As BotStatus = _engine.GetStatus()
        txtDiagnostics.Text =
            $"Running: {st.Running}{Environment.NewLine}" &
            $"BypassHpMpLimits: {_bypassHpMpLimits}{Environment.NewLine}" &
            $"BypassStuckTarget: {_bypassStuckTarget}{Environment.NewLine}" &
            $"PromptAutoAccept (Party/Ress): {_partyAutoAccept}{Environment.NewLine}" &
            $"AutoAskPartyEnabled: {_partyAskEnabled}{Environment.NewLine}" &
            $"AutoAskPartyIntervalSec: {If(nudPartyAskSeconds IsNot Nothing, nudPartyAskSeconds.Value.ToString(), "30")}{Environment.NewLine}" &
            $"AutoAskPartyText: {GetPartyAskCommandText()}{Environment.NewLine}" &
            $"LevelingAgentEnabled: {st.AgentEnabled}{Environment.NewLine}" &
            $"LevelingPreferredMobs: {If(txtLevelingPreferredMobs IsNot Nothing, txtLevelingPreferredMobs.Text.Trim(), "")}{Environment.NewLine}" &
            $"LevelingStopHp%: {If(nudLevelingStopHp IsNot Nothing, nudLevelingStopHp.Value.ToString(), "20")}{Environment.NewLine}" &
            $"LevelingStopMp%: {If(nudLevelingStopMp IsNot Nothing, nudLevelingStopMp.Value.ToString(), "10")}{Environment.NewLine}" &
            $"LevelingMaxNoTargetSec: {If(nudLevelingMaxNoTargetSeconds IsNot Nothing, nudLevelingMaxNoTargetSeconds.Value.ToString(), "45")}{Environment.NewLine}" &
            $"LevelingStopOnLowExp: {If(chkLevelingStopOnLowExp IsNot Nothing AndAlso chkLevelingStopOnLowExp.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingMinExpPerHour%: {If(nudLevelingMinExpPerHour IsNot Nothing, nudLevelingMinExpPerHour.Value.ToString("0.00"), DefaultLevelingMinExpPerHour.ToString("0.00"))}{Environment.NewLine}" &
            $"LevelingStopOnRepeatedUnreachable: {If(chkLevelingStopOnRepeatedUnreachable IsNot Nothing AndAlso chkLevelingStopOnRepeatedUnreachable.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingUnreachableLimit: {If(nudLevelingUnreachableLimit IsNot Nothing, nudLevelingUnreachableLimit.Value.ToString(), "4")}{Environment.NewLine}" &
            $"NavigationEnabled: {If(chkNavigationEnabled IsNot Nothing AndAlso chkNavigationEnabled.Checked, "True", "False")}{Environment.NewLine}" &
            $"MapOpenKey: {If(txtMapOpenKey IsNot Nothing AndAlso txtMapOpenKey.Text.Trim() <> "", txtMapOpenKey.Text.Trim().ToUpperInvariant(), DefaultMapOpenKey)}{Environment.NewLine}" &
            $"TravelPreviewEnabled: {If(chkTravelPreview IsNot Nothing AndAlso chkTravelPreview.Checked, "True", "False")}{Environment.NewLine}" &
            $"TravelExecutionEnabled: {If(chkTravelExecute IsNot Nothing AndAlso chkTravelExecute.Checked, "True", "False")}{Environment.NewLine}" &
            $"RouteRecordingEnabled: {If(chkRouteRecording IsNot Nothing AndAlso chkRouteRecording.Checked, "True", "False")}{Environment.NewLine}" &
            $"RouteRecordingName: {If(txtRouteRecordingName IsNot Nothing, txtRouteRecordingName.Text.Trim(), "jina_route")}{Environment.NewLine}" &
            $"WaypointRadius: {If(nudNavigationWaypointRadius IsNot Nothing, nudNavigationWaypointRadius.Value.ToString(), "36")}{Environment.NewLine}" &
            $"MoveBurstMs: {If(nudNavigationMoveBurstMs IsNot Nothing, nudNavigationMoveBurstMs.Value.ToString(), "350")}{Environment.NewLine}" &
            $"ResampleMs: {If(nudNavigationResampleMs IsNot Nothing, nudNavigationResampleMs.Value.ToString(), "1800")}{Environment.NewLine}" &
            $"StallTimeoutMs: {If(nudNavigationStallTimeoutMs IsNot Nothing, nudNavigationStallTimeoutMs.Value.ToString(), "6500")}{Environment.NewLine}" &
            $"RepathOnStuck: {If(chkNavigationRepathOnStuck IsNot Nothing AndAlso chkNavigationRepathOnStuck.Checked, "True", "False")}{Environment.NewLine}" &
            $"RouteStartNode: {ExtractNavigationNodeId(If(cboNavigationStartNode IsNot Nothing, cboNavigationStartNode.SelectedItem, Nothing))}{Environment.NewLine}" &
            $"RouteTargetNode: {ExtractNavigationNodeId(If(cboNavigationTargetNode IsNot Nothing, cboNavigationTargetNode.SelectedItem, Nothing))}{Environment.NewLine}" &
            $"RouteRecordingActive: {st.RouteRecordingActive}{Environment.NewLine}" &
            $"RouteRecordingMap: {If(String.IsNullOrWhiteSpace(st.RouteRecordingMapName), "n/a", st.RouteRecordingMapName)}{Environment.NewLine}" &
            $"RouteRecordingName: {If(String.IsNullOrWhiteSpace(st.RouteRecordingName), "n/a", st.RouteRecordingName)}{Environment.NewLine}" &
            $"RouteRecordingSamples: {st.RouteRecordingSampleCount}{Environment.NewLine}" &
            $"RouteRecordingStatus: {If(String.IsNullOrWhiteSpace(st.RouteRecordingStatus), "n/a", st.RouteRecordingStatus)}{Environment.NewLine}" &
            $"RouteRecordingLastSavedPath: {If(String.IsNullOrWhiteSpace(st.RouteRecordingLastSavedPath), "n/a", st.RouteRecordingLastSavedPath)}{Environment.NewLine}" &
            $"NtfyTopic: {GetNtfyTopicName()}{Environment.NewLine}" &
            $"LootPickupEnabled: {If(chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked, "True", "False")}{Environment.NewLine}" &
            $"LootPickupIntervalSec: {If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value.ToString(), "4")}{Environment.NewLine}" &
            $"LootNameMatchThreshold%: {If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value.ToString(), DefaultLootNameMatchThresholdPercent.ToString())}{Environment.NewLine}" &
            $"LootRejectPoint: {If(_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0, _lootRejectPointX.ToString() & "," & _lootRejectPointY.ToString(), "not set")}{Environment.NewLine}" &
            $"AlarmVolume%: {_alarmVolumePercent}{Environment.NewLine}" &
            $"HpZeroAlarm: {_hpZeroAlarmActive}{Environment.NewLine}" &
            $"HpZeroPending: {_hpZeroPending}{Environment.NewLine}" &
            $"Window Found: {st.WindowFound}{Environment.NewLine}" &
            $"HP%: {st.HpPercent:0.0}{Environment.NewLine}" &
            $"MP%: {st.MpPercent:0.0}{Environment.NewLine}" &
            $"Prana/EXP%: {st.ExpPercent:0.00}{Environment.NewLine}" &
            $"Prana/EXP Rate %/hr: {If(st.ExpPerHour < 0, "Calculating (1m)", st.ExpPerHour.ToString("0.00"))}{Environment.NewLine}" &
            $"MobName: {st.MobName}{Environment.NewLine}" &
            $"MobHpText: {If(String.IsNullOrWhiteSpace(st.MobHpText), "n/a", st.MobHpText)}{Environment.NewLine}" &
            $"OcrError: {OcrReader.LastError()}{Environment.NewLine}" &
            $"MobHP%: {st.MobHpPercent:0.0}{Environment.NewLine}" &
            $"MobMaxHP: {If(st.MobMaxHp > 0, st.MobMaxHp.ToString(), "n/a")}{Environment.NewLine}" &
            $"RupiahsTotal: {If(st.RupiahsTotal >= 0, st.RupiahsTotal.ToString("N0"), "n/a")}{Environment.NewLine}" &
            $"RupiahsPerHour: {If(st.RupiahsPerHour < 0, "Calculating (1m)", st.RupiahsPerHour.ToString("N0"))}{Environment.NewLine}" &
            $"TargetValid: {st.TargetValid}{Environment.NewLine}" &
            $"AgentState: {st.AgentState}{Environment.NewLine}" &
            $"AgentReason: {st.AgentReason}{Environment.NewLine}" &
            $"AgentGuardrailTriggered: {st.AgentGuardrailTriggered}{Environment.NewLine}" &
            $"MapCoordinateText: {If(String.IsNullOrWhiteSpace(st.MapCoordinateText), "n/a", st.MapCoordinateText)}{Environment.NewLine}" &
            $"MapCoordinateXY: {If(st.MapCoordinateX >= 0 AndAlso st.MapCoordinateY >= 0, st.MapCoordinateX.ToString() & "," & st.MapCoordinateY.ToString(), "n/a")}{Environment.NewLine}" &
            $"MapHeading: {If(String.IsNullOrWhiteSpace(st.MapHeading), "n/a", st.MapHeading)}{Environment.NewLine}" &
            $"MapCoordinateConfidence: {st.MapCoordinateConfidence}{Environment.NewLine}" &
            $"MapVisible: {st.MapVisible}{Environment.NewLine}" &
            $"MapMarkerDetected: {st.MapMarkerDetected}{Environment.NewLine}" &
            $"MapMarkerXY: {If(st.MapMarkerX >= 0 AndAlso st.MapMarkerY >= 0, st.MapMarkerX.ToString() & "," & st.MapMarkerY.ToString(), "n/a")}{Environment.NewLine}" &
            $"MapLocalizationConfidence: {st.MapLocalizationConfidence}{Environment.NewLine}" &
            $"NavigationCurrentNode: {If(String.IsNullOrWhiteSpace(st.NavigationCurrentNodeLabel), "n/a", st.NavigationCurrentNodeLabel)}{Environment.NewLine}" &
            $"NavigationNextWaypoint: {If(String.IsNullOrWhiteSpace(st.NavigationNextWaypointLabel), "n/a", st.NavigationNextWaypointLabel)}{Environment.NewLine}" &
            $"NavigationRouteReady: {st.NavigationRouteReady}{Environment.NewLine}" &
            $"NavigationRoute: {If(String.IsNullOrWhiteSpace(st.NavigationRouteText), "n/a", st.NavigationRouteText)}{Environment.NewLine}" &
            $"NavigationTravelActive: {st.NavigationTravelActive}{Environment.NewLine}" &
            $"NavigationTravelReason: {If(String.IsNullOrWhiteSpace(st.NavigationTravelReason), "n/a", st.NavigationTravelReason)}{Environment.NewLine}" &
            $"NavigationDistanceToWaypoint: {If(st.NavigationDistanceToWaypoint < 0, "n/a", st.NavigationDistanceToWaypoint.ToString("0.0"))}{Environment.NewLine}" &
            $"NavigationTravelStalled: {st.NavigationTravelStalled}{Environment.NewLine}" &
            $"NavigationRecoveryCount: {st.NavigationRecoveryCount}{Environment.NewLine}" &
            $"NavigationDestinationReached: {st.NavigationDestinationReached}{Environment.NewLine}" &
            $"NavigationDestinationLabel: {If(String.IsNullOrWhiteSpace(st.NavigationDestinationLabel), "n/a", st.NavigationDestinationLabel)}{Environment.NewLine}" &
            $"LastAction: {st.LastAction}{Environment.NewLine}" &
             $"NotAttackingReason: {st.NotAttackingReason}{Environment.NewLine}" &
             $"Error: {st.ErrorMessage}"
        RefreshKeyActionSummary()
    End Sub

    Private Sub EnterToggleTimerTick(sender As Object, e As EventArgs)
        Dim ctrlDown As Boolean = (GetAsyncKeyState(CInt(Keys.LControlKey)) And &H8000S) <> 0 OrElse (GetAsyncKeyState(CInt(Keys.RControlKey)) And &H8000S) <> 0
        Dim shiftDown As Boolean = (GetAsyncKeyState(CInt(Keys.LShiftKey)) And &H8000S) <> 0 OrElse (GetAsyncKeyState(CInt(Keys.RShiftKey)) And &H8000S) <> 0
        Dim comboDown As Boolean = ctrlDown AndAlso shiftDown
        If comboDown AndAlso Not _ctrlShiftWasDown Then
            HandleCtrlShiftTogglePress()
        End If
        _ctrlShiftWasDown = comboDown
    End Sub

    Private Sub HandleCtrlShiftTogglePress()
        If Not (IsGameWindowForeground() OrElse IsControlPanelForeground()) Then
            Return
        End If

        If _engine.IsRunning() Then
            StopClicked(Nothing, EventArgs.Empty)
            AppendLog("Ctrl+Shift toggle: bot paused.")
        Else
            StartClicked(Nothing, EventArgs.Empty)
            AppendLog("Ctrl+Shift toggle: bot resumed.")
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

    Private Function IsControlPanelForeground() As Boolean
        Dim hwnd As IntPtr = GetForegroundWindow()
        If hwnd = IntPtr.Zero Then
            Return False
        End If
        If hwnd = Me.Handle Then
            Return True
        End If
        Return ContainsFocus
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
        Dim mobDisplayName As String = If(String.IsNullOrWhiteSpace(status.MobName), "(none)", status.MobName)
        If Not String.IsNullOrWhiteSpace(status.MobHpText) Then
            mobDisplayName &= $" ({status.MobHpText})"
        End If
        lblMobName.Text = $"Mob: {mobDisplayName}"
        lblExpRate.Text = $"Prana/EXP: {status.ExpPercent:0.00}% | Rate: {If(status.ExpPerHour < 0, "Calculating (1m)", status.ExpPerHour.ToString("0.00") & "%/hr")}"
        lblRupiahsRate.Text = $"Rupiahs: {If(status.RupiahsTotal >= 0, status.RupiahsTotal.ToString("N0"), "n/a")} | Rate: {If(status.RupiahsPerHour < 0, "Calculating (1m)", status.RupiahsPerHour.ToString("N0") & "/hr")}"
        If lblLevelingState IsNot Nothing Then
            lblLevelingState.Text = $"Agent State: {status.AgentState}"
            lblLevelingState.ForeColor = If(status.AgentGuardrailTriggered, Color.FromArgb(255, 120, 120), If(status.AgentEnabled, Color.Khaki, Color.DimGray))
        End If
        If lblLevelingReason IsNot Nothing Then
            Dim agentReason As String = If(String.IsNullOrWhiteSpace(status.AgentReason), "No active leveling-agent reason.", status.AgentReason)
            lblLevelingReason.Text = $"Reason: {agentReason}"
            lblLevelingReason.ForeColor = If(status.AgentGuardrailTriggered, Color.FromArgb(255, 160, 160), Color.Gainsboro)
        End If
        If lblMapCoordinate IsNot Nothing Then
            Dim coordText As String = If(status.MapCoordinateX >= 0 AndAlso status.MapCoordinateY >= 0, $"{status.MapCoordinateX}/{status.MapCoordinateY}", If(String.IsNullOrWhiteSpace(status.MapCoordinateText), "n/a", status.MapCoordinateText))
            lblMapCoordinate.Text = $"Map Coordinate: {coordText} (confidence {status.MapCoordinateConfidence}%)"
        End If
        If lblMapHeading IsNot Nothing Then
            lblMapHeading.Text = $"Map Heading: {If(String.IsNullOrWhiteSpace(status.MapHeading), "n/a", status.MapHeading)}"
        End If
        If lblMapMarker IsNot Nothing Then
            Dim markerText As String = If(status.MapMarkerDetected, $"{status.MapMarkerX}/{status.MapMarkerY} (from coordinates)", "not available")
            lblMapMarker.Text = $"Map Marker: {markerText}"
            lblMapMarker.ForeColor = If(status.MapMarkerDetected, Color.FromArgb(255, 140, 120), Color.DimGray)
        End If
        If lblMapLocalizationConfidence IsNot Nothing Then
            lblMapLocalizationConfidence.Text = $"Localization Confidence: {status.MapLocalizationConfidence}%"
            lblMapLocalizationConfidence.ForeColor = If(status.MapLocalizationConfidence >= 80, Color.LightGreen, If(status.MapLocalizationConfidence >= 50, Color.Khaki, Color.OrangeRed))
        End If
        If lblTravelStatus IsNot Nothing Then
            Dim travelReason As String = If(String.IsNullOrWhiteSpace(status.NavigationTravelReason), "idle", status.NavigationTravelReason)
            Dim distanceText As String = If(status.NavigationDistanceToWaypoint < 0, "n/a", status.NavigationDistanceToWaypoint.ToString("0.0"))
            Dim stallText As String = If(status.NavigationTravelStalled, $" | stalled x{Math.Max(1, status.NavigationRecoveryCount)}", If(status.NavigationRecoveryCount > 0, $" | recoveries {status.NavigationRecoveryCount}", ""))
            If status.NavigationDestinationReached AndAlso Not String.IsNullOrWhiteSpace(status.NavigationDestinationLabel) Then
                travelReason = $"destination reached: {status.NavigationDestinationLabel}"
                distanceText = "0.0"
            End If
            lblTravelStatus.Text = $"Travel: {travelReason} | Dist: {distanceText}{stallText}"
            lblTravelStatus.ForeColor = If(status.NavigationDestinationReached, Color.LightGreen, If(status.NavigationTravelStalled, Color.OrangeRed, If(status.NavigationTravelActive, Color.LightSteelBlue, Color.DimGray)))
        End If
        If lblRoutePreview IsNot Nothing Then
            Dim routeText As String
            If Not status.NavigationTravelPreviewEnabled Then
                routeText = "Route Preview: disabled"
            ElseIf status.NavigationDestinationReached Then
                routeText = $"Route Preview: destination reached{Environment.NewLine}{status.NavigationRouteText}"
            ElseIf Not status.NavigationRouteReady Then
                routeText = "Route Preview: waiting for route"
            Else
                Dim currentNodeText As String = If(String.IsNullOrWhiteSpace(status.NavigationCurrentNodeLabel), "unknown start", status.NavigationCurrentNodeLabel)
                Dim nextNodeText As String = If(String.IsNullOrWhiteSpace(status.NavigationNextWaypointLabel), "destination reached", status.NavigationNextWaypointLabel)
                routeText = $"Route Preview: {currentNodeText} -> {nextNodeText}{Environment.NewLine}{status.NavigationRouteText}"
            End If
            lblRoutePreview.Text = routeText
            lblRoutePreview.ForeColor = If(status.NavigationRouteReady, Color.LightCyan, Color.DimGray)
        End If
        If lblRouteRecording IsNot Nothing Then
            Dim recordingText As String = If(String.IsNullOrWhiteSpace(status.RouteRecordingStatus), "idle", status.RouteRecordingStatus)
            lblRouteRecording.Text = $"Route Recording: {recordingText}"
            lblRouteRecording.ForeColor = If(status.RouteRecordingActive, Color.Plum, If(status.RouteRecordingSampleCount >= 1, Color.LightPink, Color.DimGray))
        End If
        UpdateAttackButtonAppearance(status.Running)
        HandleHpZeroAlarm(status)
        HandleWindowMissingAlarm(status)
        ApplyHealthUiTint(status.HpPercent, status.Running AndAlso status.WindowFound)

        If Not String.IsNullOrWhiteSpace(status.RouteRecordingLastSavedPath) AndAlso Not status.RouteRecordingLastSavedPath.Equals(_lastRouteRecordingSavedPath, StringComparison.OrdinalIgnoreCase) Then
            _lastRouteRecordingSavedPath = status.RouteRecordingLastSavedPath
            AppendLog("Recorded route saved: " & status.RouteRecordingLastSavedPath)
            PopulateNavigationNodeCombos()
        End If

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
        Dim agentStateText As String = $"{status.AgentState}|{status.AgentReason}|{status.AgentGuardrailTriggered}"
        If agentStateText <> _lastAgentState Then
            AppendLog($"Leveling agent: {status.AgentState}{If(String.IsNullOrWhiteSpace(status.AgentReason), "", " - " & status.AgentReason)}")
            _lastAgentState = agentStateText
        End If
    End Sub

    Private Sub BeginNotificationWarmup()
        _notificationWarmupUntilUtc = DateTime.UtcNow.AddSeconds(StartupNotificationWarmupSeconds)
        AppendLog($"Startup guard: suppressing death/window alerts for {StartupNotificationWarmupSeconds} seconds.")
    End Sub

    Private Function IsNotificationWarmupActive() As Boolean
        Return DateTime.UtcNow < _notificationWarmupUntilUtc
    End Function

    Private Sub HandleWindowMissingAlarm(status As BotStatus)
        If status Is Nothing Then
            Return
        End If

        Dim errorText As String = If(status.ErrorMessage, "")
        Dim missingWindow As Boolean =
            status.Running AndAlso
            ((Not status.WindowFound) OrElse errorText.IndexOf("window not found", StringComparison.OrdinalIgnoreCase) >= 0)
        Dim captureUnavailable As Boolean =
            status.Running AndAlso
            status.WindowFound AndAlso
            (errorText.IndexOf("capture failed", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
             errorText.IndexOf("unable to capture", StringComparison.OrdinalIgnoreCase) >= 0)

        If status.Running AndAlso IsNotificationWarmupActive() AndAlso (missingWindow OrElse captureUnavailable) Then
            Return
        End If

        If missingWindow OrElse captureUnavailable Then
            If Not _windowMissingNotificationLatched Then
                _windowMissingNotificationLatched = True
                SendWindowMissingPhoneAlert(captureUnavailable)
            End If
            Return
        End If

        If status.WindowFound OrElse (Not status.Running) Then
            _windowMissingNotificationLatched = False
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
        Dim names As List(Of String) = ParseBulkFilterNames(If(txtMonsterName IsNot Nothing, txtMonsterName.Text, ""))
        If names.Count = 0 Then
            Return
        End If

        Dim added As New List(Of String)()
        Dim skipped As New List(Of String)()
        For Each name As String In names
            If MonsterExists(name) Then
                skipped.Add(name)
            Else
                lstMonsterFilter.Items.Add(name)
                added.Add(name)
            End If
        Next

        If added.Count > 0 Then
            AppendLog("Monster filter added: " & String.Join(", ", added))
            PushLiveConfig()
            SavePersistedListState(False)
        End If
        If skipped.Count > 0 Then
            AppendLog("Monster filter skipped (already exists): " & String.Join(", ", skipped))
        End If

        If txtMonsterName IsNot Nothing Then
            txtMonsterName.Text = ""
        End If
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
        Dim names As List(Of String) = ParseBulkFilterNames(If(txtLootName IsNot Nothing, txtLootName.Text, ""))
        If names.Count = 0 Then
            Return
        End If

        Dim added As New List(Of String)()
        Dim skipped As New List(Of String)()
        For Each name As String In names
            If LootExists(name) Then
                skipped.Add(name)
            Else
                lstLootFilter.Items.Add(name)
                added.Add(name)
            End If
        Next

        If added.Count > 0 Then
            AppendLog("Loot filter added: " & String.Join(", ", added))
            PushLiveConfig()
            SavePersistedListState(False)
        End If
        If skipped.Count > 0 Then
            AppendLog("Loot filter skipped (already exists): " & String.Join(", ", skipped))
        End If

        If txtLootName IsNot Nothing Then
            txtLootName.Text = ""
        End If
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

    Private Shared Function ParseBulkFilterNames(rawInput As String) As List(Of String)
        Dim result As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If String.IsNullOrWhiteSpace(rawInput) Then
            Return result
        End If

        Dim normalized As String = rawInput.Replace(vbCrLf, ",").Replace(vbCr, ",").Replace(vbLf, ",")
        Dim chunks As String() = normalized.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
        For Each chunk As String In chunks
            Dim cleaned As String = chunk.Trim()
            If cleaned <> "" AndAlso seen.Add(cleaned) Then
                result.Add(cleaned)
            End If
        Next

        Return result
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
        cfg.StuckTargetMs = CInt(If(nudStuckTargetMs IsNot Nothing, nudStuckTargetMs.Value, 2200D))
        cfg.MobHpPresenceThreshold = CDbl(nudMobHpThreshold.Value)
        cfg.HighMaxHpSpecialEnabled = (chkHighMaxHpSpecial IsNot Nothing AndAlso chkHighMaxHpSpecial.Checked)
        cfg.HighMaxHpThreshold = CInt(If(nudHighMaxHpThreshold IsNot Nothing, nudHighMaxHpThreshold.Value, 2000D))
        cfg.BypassHpMpLimits = _bypassHpMpLimits
        cfg.BypassStuckTarget = _bypassStuckTarget
        cfg.PartyAutoAcceptEnabled = _partyAutoAccept
        cfg.PartyAskEnabled = _partyAskEnabled
        cfg.PartyAskIntervalMs = CInt(Math.Round(CDbl(If(nudPartyAskSeconds IsNot Nothing, nudPartyAskSeconds.Value, 30D)) * 1000.0))
                cfg.PartyAskText = GetPartyAskCommandText()
        cfg.LootScannerEnabled = _lootScannerEnabled
        cfg.ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), "")
        cfg.LevelingAgentEnabled = (chkLevelingAgent IsNot Nothing AndAlso chkLevelingAgent.Checked)
        cfg.LevelingPreferredMobs = ParseCommaSeparatedList(If(txtLevelingPreferredMobs IsNot Nothing, txtLevelingPreferredMobs.Text, ""))
        cfg.LevelingStopHpPercent = CInt(If(nudLevelingStopHp IsNot Nothing, nudLevelingStopHp.Value, 20D))
        cfg.LevelingStopMpPercent = CInt(If(nudLevelingStopMp IsNot Nothing, nudLevelingStopMp.Value, 10D))
        cfg.LevelingMaxNoTargetSeconds = CInt(If(nudLevelingMaxNoTargetSeconds IsNot Nothing, nudLevelingMaxNoTargetSeconds.Value, 45D))
        cfg.LevelingStopOnLowExpRate = (chkLevelingStopOnLowExp IsNot Nothing AndAlso chkLevelingStopOnLowExp.Checked)
        cfg.LevelingMinExpPerHour = CDbl(If(nudLevelingMinExpPerHour IsNot Nothing, nudLevelingMinExpPerHour.Value, DefaultLevelingMinExpPerHour))
        cfg.LevelingStopOnRepeatedUnreachable = (chkLevelingStopOnRepeatedUnreachable IsNot Nothing AndAlso chkLevelingStopOnRepeatedUnreachable.Checked)
        cfg.LevelingUnreachableLimit = CInt(If(nudLevelingUnreachableLimit IsNot Nothing, nudLevelingUnreachableLimit.Value, 4D))
        cfg.NavigationEnabled = (chkNavigationEnabled IsNot Nothing AndAlso chkNavigationEnabled.Checked)
        cfg.MapOpenKey = If(txtMapOpenKey IsNot Nothing AndAlso txtMapOpenKey.Text.Trim() <> "", txtMapOpenKey.Text.Trim().ToUpperInvariant(), DefaultMapOpenKey)
        cfg.NavigationMapName = "Jina Basin"
        cfg.NavigationStartNodeId = ""
        Dim selectedRouteName As String = ExtractRecordedRouteNameFromNavigationSelection(If(cboNavigationTargetNode IsNot Nothing, cboNavigationTargetNode.SelectedItem, Nothing))
        Dim selectedRouteEndNode As NavigationNode = If(selectedRouteName = "", Nothing, BotEngine.GetRecordedRouteEndNode(selectedRouteName, cfg.NavigationMapName))
        cfg.NavigationTargetNodeId = If(selectedRouteEndNode Is Nothing, "", selectedRouteEndNode.Id)
        cfg.NavigationTravelPreviewEnabled = (chkTravelPreview IsNot Nothing AndAlso chkTravelPreview.Checked)
        cfg.NavigationTravelExecutionEnabled = (chkTravelExecute IsNot Nothing AndAlso chkTravelExecute.Checked)
        cfg.RouteRecordingEnabled = (chkRouteRecording IsNot Nothing AndAlso chkRouteRecording.Checked)
        cfg.RouteRecordingName = If(txtRouteRecordingName IsNot Nothing AndAlso txtRouteRecordingName.Text.Trim() <> "", txtRouteRecordingName.Text.Trim(), "jina_route")
        cfg.NavigationWaypointReachRadius = CInt(If(nudNavigationWaypointRadius IsNot Nothing, nudNavigationWaypointRadius.Value, 36D))
        cfg.NavigationMoveBurstMs = CInt(If(nudNavigationMoveBurstMs IsNot Nothing, nudNavigationMoveBurstMs.Value, 350D))
        cfg.NavigationResampleIntervalMs = CInt(If(nudNavigationResampleMs IsNot Nothing, nudNavigationResampleMs.Value, 1800D))
        cfg.NavigationStallTimeoutMs = CInt(If(nudNavigationStallTimeoutMs IsNot Nothing, nudNavigationStallTimeoutMs.Value, 6500D))
        cfg.NavigationRepathOnStuck = (chkNavigationRepathOnStuck IsNot Nothing AndAlso chkNavigationRepathOnStuck.Checked)
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.MobNameRect = BuildRect("mob_name_rect")
        cfg.MobHpRect = BuildRect("mob_hp_rect")
        cfg.UnreachableTextRect = BuildRect("unreachable_text_rect")
        cfg.PranaExpRect = BuildRect("prana_exp_rect")
        cfg.RupiahsRect = BuildRect("rupiahs_rect")
        cfg.PartyInviteScanRect = BuildRect("party_invite_scan_rect")
        cfg.PartyInviteOkRect = BuildRect("party_invite_ok_rect")
        cfg.MapRect = BuildRect("map_rect")
        cfg.MapCoordinateRect = BuildRect("map_coordinate_rect")
        cfg.LootScanPoints = BuildLootScanPoints()
        cfg.LootScanRect = BuildLootScanBoundingRect(cfg.LootScanPoints)
        cfg.LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked)
        cfg.LootPickupIntervalMs = CInt(Math.Round(CDbl(If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D)) * 1000.0))
        cfg.LootNameMatchThresholdPercent = CInt(If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value, CDec(DefaultLootNameMatchThresholdPercent)))
        cfg.LootPickupVerifyDelayMs = 80
        cfg.LootRejectClickEnabled = (_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0)
        cfg.LootRejectPointX = _lootRejectPointX
        cfg.LootRejectPointY = _lootRejectPointY

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
            Dim keyName As String = SafeCell(row, "Key", "").ToUpperInvariant()
            If keyName = "" Then
                Continue For
            End If

            Dim enabled As Boolean = False
            Try
                enabled = Convert.ToBoolean(row.Cells("Enabled").Value)
            Catch
            End Try

            Dim cooldownSec As Double = Math.Max(0.05, ParseDouble(SafeCell(row, "CooldownSec", "1.0"), 1.0))
            actions.Add(New ActionRule With {
                .KeyName = keyName,
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

    Private Function BuildLootScanPoints() As List(Of LootScanPoint)
        Dim parsed As List(Of LootScanPoint) = ParseLootScanPoints(If(txtLootScanAreaPoints IsNot Nothing, txtLootScanAreaPoints.Text, ""))
        If parsed.Count >= 3 Then
            Return parsed
        End If
        Return CloneLootScanPoints(BotConfig.CreateDefaultLootScanPoints())
    End Function

    Private Shared Function BuildLootScanBoundingRect(points As List(Of LootScanPoint)) As RectRegion
        Dim source As List(Of LootScanPoint) = If(points, New List(Of LootScanPoint)())
        If source.Count = 0 Then
            source = BotConfig.CreateDefaultLootScanPoints()
        End If

        Dim valid As List(Of LootScanPoint) = source.Where(Function(pt) pt IsNot Nothing).ToList()
        If valid.Count = 0 Then
            valid = BotConfig.CreateDefaultLootScanPoints()
        End If

        Dim minX As Integer = valid.Min(Function(pt) pt.X)
        Dim minY As Integer = valid.Min(Function(pt) pt.Y)
        Dim maxX As Integer = valid.Max(Function(pt) pt.X)
        Dim maxY As Integer = valid.Max(Function(pt) pt.Y)
        Return New RectRegion(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY))
    End Function

    Private Shared Function ParseLootScanPoints(raw As String) As List(Of LootScanPoint)
        Dim result As New List(Of LootScanPoint)()
        Dim normalized As String = If(raw, "").Replace(vbCrLf, "|").Replace(vbCr, "|").Replace(vbLf, "|")
        Dim chunks As String() = normalized.Split({"|"}, StringSplitOptions.RemoveEmptyEntries)

        For Each chunk As String In chunks
            Dim pair As String() = chunk.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
            If pair.Length <> 2 Then
                Continue For
            End If

            Dim x As Integer
            Dim y As Integer
            If Integer.TryParse(pair(0).Trim(), x) AndAlso Integer.TryParse(pair(1).Trim(), y) Then
                result.Add(New LootScanPoint(x, y))
            End If
        Next

        Return result
    End Function

    Private Shared Function FormatLootScanPoints(points As IEnumerable(Of LootScanPoint)) As String
        Dim source As IEnumerable(Of LootScanPoint) = If(points, Enumerable.Empty(Of LootScanPoint)())
        Return String.Join(" | ", source.Where(Function(pt) pt IsNot Nothing).Select(Function(pt) $"{pt.X},{pt.Y}"))
    End Function

    Private Shared Function CloneLootScanPoints(points As IEnumerable(Of LootScanPoint)) As List(Of LootScanPoint)
        Dim source As IEnumerable(Of LootScanPoint) = If(points, Enumerable.Empty(Of LootScanPoint)())
        Return source.Where(Function(pt) pt IsNot Nothing).Select(Function(pt) New LootScanPoint(pt.X, pt.Y)).ToList()
    End Function

    Private Shared Function ParseCommaSeparatedList(raw As String) As List(Of String)
        Dim results As New List(Of String)()
        For Each part As String In If(raw, "").Split({","c}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleaned As String = part.Trim()
            If cleaned = "" Then
                Continue For
            End If
            If Not results.Any(Function(existing) existing.Equals(cleaned, StringComparison.OrdinalIgnoreCase)) Then
                results.Add(cleaned)
            End If
        Next
        Return results
    End Function

    Private Sub PopulateNavigationNodeCombos()
        Dim mapName As String = GetSelectedNavigationMapName()
        Dim routes As List(Of RecordedNavigationRouteInfo) = BotEngine.GetRecordedRouteOptions(mapName)

        If cboNavigationStartNode IsNot Nothing Then
            cboNavigationStartNode.Items.Clear()
            cboNavigationStartNode.Items.Add("(auto from map)")
            cboNavigationStartNode.SelectedIndex = 0
        End If

        If cboNavigationTargetNode IsNot Nothing Then
            Dim previousRoute As String = ExtractRecordedRouteNameFromNavigationSelection(cboNavigationTargetNode.SelectedItem)
            cboNavigationTargetNode.Items.Clear()
            For Each routeInfo As RecordedNavigationRouteInfo In routes
                Dim endNode As NavigationNode = BotEngine.GetRecordedRouteEndNode(routeInfo.RouteName, mapName)
                If endNode IsNot Nothing Then
                    cboNavigationTargetNode.Items.Add($"{routeInfo.RouteName} -> {endNode.Label}")
                End If
            Next
            If cboNavigationTargetNode.Items.Count > 0 Then
                Dim selectedIndex As Integer = -1
                If previousRoute <> "" Then
                    For i As Integer = 0 To cboNavigationTargetNode.Items.Count - 1
                        If ExtractRecordedRouteNameFromNavigationSelection(cboNavigationTargetNode.Items(i)).Equals(previousRoute, StringComparison.OrdinalIgnoreCase) Then
                            selectedIndex = i
                            Exit For
                        End If
                    Next
                End If
                cboNavigationTargetNode.SelectedIndex = If(selectedIndex >= 0, selectedIndex, 0)
            End If
        End If
    End Sub

    Private Function GetSelectedNavigationMapName() As String
        Dim cfg As BotConfig = BuildConfig()
        If cfg Is Nothing OrElse String.IsNullOrWhiteSpace(cfg.NavigationMapName) Then
            Return "Jina Basin"
        End If
        Return cfg.NavigationMapName.Trim()
    End Function

    Private Sub PopulateRecordedRouteManager()
        Dim mapName As String = GetSelectedNavigationMapName()
        Dim routes As List(Of RecordedNavigationRouteInfo) = BotEngine.GetRecordedRouteOptions(mapName)

        If cboRecordedRoute IsNot Nothing Then
            Dim previousRoute As String = ExtractRecordedRouteName(cboRecordedRoute.SelectedItem)
            cboRecordedRoute.Items.Clear()
            cboRecordedRoute.Items.Add("(select recorded route)")
            For Each routeInfo As RecordedNavigationRouteInfo In routes
                cboRecordedRoute.Items.Add($"{routeInfo.RouteName} ({routeInfo.NodeCount} nodes)")
            Next

            Dim restoreIndex As Integer = 0
            If previousRoute <> "" Then
                For i As Integer = 1 To cboRecordedRoute.Items.Count - 1
                    If ExtractRecordedRouteName(cboRecordedRoute.Items(i)).Equals(previousRoute, StringComparison.OrdinalIgnoreCase) Then
                        restoreIndex = i
                        Exit For
                    End If
                Next
            End If
            cboRecordedRoute.SelectedIndex = Math.Max(0, restoreIndex)
        End If

        PopulateRecordedRouteNodeManager()
    End Sub

    Private Sub PopulateRecordedRouteNodeManager()
        If cboRecordedRouteNode Is Nothing Then
            Return
        End If

        Dim mapName As String = GetSelectedNavigationMapName()
        Dim routeName As String = ExtractRecordedRouteName(If(cboRecordedRoute Is Nothing, Nothing, cboRecordedRoute.SelectedItem))
        Dim nodes As List(Of NavigationNode) = If(routeName = "", New List(Of NavigationNode)(), BotEngine.GetRecordedRouteNodeOptions(routeName, mapName))

        cboRecordedRouteNode.Items.Clear()
        cboRecordedRouteNode.Items.Add("(select recorded node)")
        For Each node As NavigationNode In nodes
            cboRecordedRouteNode.Items.Add($"{node.Id} - {node.Label}")
        Next
        cboRecordedRouteNode.SelectedIndex = 0
    End Sub

    Private Sub SaveRouteRecordingClicked(sender As Object, e As EventArgs)
        Dim cfg As BotConfig = BuildConfig()
        Dim savedPath As String = _engine.SaveRecordedRoute(cfg)
        If String.IsNullOrWhiteSpace(savedPath) Then
            AppendLog("Recorded route save failed. Make sure recording mode captured enough coordinate samples first.")
            Return
        End If

        AppendLog("Recorded route saved: " & savedPath)
        _lastRouteRecordingSavedPath = savedPath
        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        SavePersistedListState(False)
    End Sub

    Private Sub RecordedRouteSelectionChanged(sender As Object, e As EventArgs)
        PopulateRecordedRouteNodeManager()
    End Sub

    Private Sub DeleteRecordedRouteClicked(sender As Object, e As EventArgs)
        Dim routeName As String = ExtractRecordedRouteName(If(cboRecordedRoute Is Nothing, Nothing, cboRecordedRoute.SelectedItem))
        If routeName = "" Then
            AppendLog("Select a recorded route first.")
            Return
        End If

        Dim mapName As String = GetSelectedNavigationMapName()
        If Not BotEngine.DeleteRecordedRoute(routeName, mapName) Then
            AppendLog("Recorded route delete failed: " & routeName)
            Return
        End If

        AppendLog("Recorded route deleted: " & routeName)
        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        SavePersistedListState(False)
    End Sub

    Private Sub DeleteRecordedRouteNodeClicked(sender As Object, e As EventArgs)
        Dim routeName As String = ExtractRecordedRouteName(If(cboRecordedRoute Is Nothing, Nothing, cboRecordedRoute.SelectedItem))
        If routeName = "" Then
            AppendLog("Select a recorded route first.")
            Return
        End If

        Dim nodeId As String = ExtractNavigationNodeId(If(cboRecordedRouteNode Is Nothing, Nothing, cboRecordedRouteNode.SelectedItem))
        If nodeId = "" Then
            AppendLog("Select a recorded node first.")
            Return
        End If

        Dim mapName As String = GetSelectedNavigationMapName()
        If Not BotEngine.DeleteRecordedRouteNode(routeName, nodeId, mapName) Then
            AppendLog($"Recorded route node delete failed: {routeName} / {nodeId}")
            Return
        End If

        AppendLog($"Recorded route node deleted: {routeName} / {nodeId}")
        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        SavePersistedListState(False)
    End Sub

    Private Shared Function ExtractRecordedRouteName(selectedItem As Object) As String
        Dim raw As String = If(selectedItem, "").ToString().Trim()
        If raw = "" OrElse raw.StartsWith("(select", StringComparison.OrdinalIgnoreCase) Then
            Return ""
        End If

        Dim suffixStart As Integer = raw.LastIndexOf(" (", StringComparison.Ordinal)
        If suffixStart > 0 Then
            Return raw.Substring(0, suffixStart).Trim()
        End If
        Return raw
    End Function

    Private Shared Function ExtractRecordedRouteNameFromNavigationSelection(selectedItem As Object) As String
        Dim raw As String = If(selectedItem, "").ToString().Trim()
        If raw = "" Then
            Return ""
        End If

        Dim separatorIndex As Integer = raw.IndexOf(" -> ", StringComparison.Ordinal)
        If separatorIndex > 0 Then
            Return raw.Substring(0, separatorIndex).Trim()
        End If
        Return raw
    End Function

    Private Shared Function ExtractNavigationNodeId(selectedItem As Object) As String
        Dim raw As String = If(selectedItem, "").ToString().Trim()
        If raw = "" OrElse raw.StartsWith("(auto", StringComparison.OrdinalIgnoreCase) Then
            Return ""
        End If

        Dim separatorIndex As Integer = raw.IndexOf(" - ", StringComparison.Ordinal)
        If separatorIndex > 0 Then
            Return raw.Substring(0, separatorIndex).Trim()
        End If
        Return raw
    End Function

    Private Shared Function GetEffectiveLootScanPoints(cfg As BotConfig) As List(Of LootScanPoint)
        Dim fromConfig As List(Of LootScanPoint) = CloneLootScanPoints(If(cfg?.LootScanPoints, Nothing))
        If fromConfig.Count >= 3 Then
            Return fromConfig
        End If

        Dim legacyRect As RectRegion = If(cfg?.LootScanRect, Nothing)
        If legacyRect IsNot Nothing AndAlso legacyRect.W > 0 AndAlso legacyRect.H > 0 Then
            Return New List(Of LootScanPoint) From {
                New LootScanPoint(legacyRect.X, legacyRect.Y),
                New LootScanPoint(legacyRect.X + legacyRect.W, legacyRect.Y),
                New LootScanPoint(legacyRect.X + legacyRect.W, legacyRect.Y + legacyRect.H),
                New LootScanPoint(legacyRect.X, legacyRect.Y + legacyRect.H)
            }
        End If

        Return CloneLootScanPoints(BotConfig.CreateDefaultLootScanPoints())
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

            If state.SavedConfig IsNot Nothing Then
                ApplySavedConfigToUi(state.SavedConfig)
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
            If nudLootNameMatchThreshold IsNot Nothing Then
                Dim boundedLootMatch As Decimal = Math.Max(nudLootNameMatchThreshold.Minimum, Math.Min(nudLootNameMatchThreshold.Maximum, state.LootNameMatchThresholdPercent))
                nudLootNameMatchThreshold.Value = boundedLootMatch
            End If
            If state.LootRejectPointEnabled Then
                _lootRejectPointX = Math.Max(0, state.LootRejectPointX)
                _lootRejectPointY = Math.Max(0, state.LootRejectPointY)
            Else
                _lootRejectPointX = -1
                _lootRejectPointY = -1
            End If
            _isPickingLootRejectPoint = False
            UpdateLootRejectPointUi()
            _partyAutoAccept = state.PromptAutoAcceptEnabled
            UpdatePromptAutoAcceptButton()
            _partyAskEnabled = state.AskForPartyEnabled
            If nudPartyAskSeconds IsNot Nothing Then
                Dim boundedAskSeconds As Decimal = Math.Max(nudPartyAskSeconds.Minimum, Math.Min(nudPartyAskSeconds.Maximum, state.AskForPartySeconds))
                nudPartyAskSeconds.Value = boundedAskSeconds
            End If
            If txtPartyAskText IsNot Nothing Then
                txtPartyAskText.Text = If(String.IsNullOrWhiteSpace(state.AskForPartyText), DefaultPartyAskCommand, state.AskForPartyText.Trim())
            End If
            UpdatePartyAskButton()

            _lootScannerEnabled = state.LootScannerEnabled
            If btnLootScanner IsNot Nothing Then
                btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
                btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
            End If
            If txtNtfyTopic IsNot Nothing Then
                Dim topic As String = If(state.NtfyTopic, "").Trim()
                txtNtfyTopic.Text = If(topic = "", DefaultNtfyTopicName, topic)
            End If
            If txtItemNtfyTopic IsNot Nothing Then
                txtItemNtfyTopic.Text = If(state.ItemNtfyTopic, "").Trim()
            End If
            If nudAutoPotHp IsNot Nothing Then
                Dim boundedAutoHp As Decimal = Math.Max(nudAutoPotHp.Minimum, Math.Min(nudAutoPotHp.Maximum, state.AutoPotHpPercent))
                nudAutoPotHp.Value = boundedAutoHp
            End If
            If nudAutoPotMp IsNot Nothing Then
                Dim boundedAutoMp As Decimal = Math.Max(nudAutoPotMp.Minimum, Math.Min(nudAutoPotMp.Maximum, state.AutoPotMpPercent))
                nudAutoPotMp.Value = boundedAutoMp
            End If
            If nudAlarmVolume IsNot Nothing Then
                Dim boundedVolume As Decimal = Math.Max(nudAlarmVolume.Minimum, Math.Min(nudAlarmVolume.Maximum, state.AlarmVolumePercent))
                nudAlarmVolume.Value = boundedVolume
                _alarmVolumePercent = CInt(boundedVolume)
            End If

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

    Private Sub SavePersistedListState(Optional logFailure As Boolean = False, Optional includeFullConfig As Boolean = True)
        Try
            If Not Directory.Exists(PersistDirectoryPath) Then
                Directory.CreateDirectory(PersistDirectoryPath)
            End If

            Dim state As New PersistedListState With {
                .MonsterFilterEnabled = (chkMonsterFilter IsNot Nothing AndAlso chkMonsterFilter.Checked),
                .LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked),
                .LootPickupSeconds = If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D),
                .LootNameMatchThresholdPercent = If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value, CDec(DefaultLootNameMatchThresholdPercent)),
                .LootRejectPointEnabled = (_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0),
                .LootRejectPointX = _lootRejectPointX,
                .LootRejectPointY = _lootRejectPointY,
                .PromptAutoAcceptEnabled = _partyAutoAccept,
                .AskForPartyEnabled = _partyAskEnabled,
                .AskForPartySeconds = If(nudPartyAskSeconds IsNot Nothing, nudPartyAskSeconds.Value, 30D),
                                .AskForPartyText = GetPartyAskCommandText(),
                .LootScannerEnabled = _lootScannerEnabled,
                .NtfyTopic = If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text.Trim(), ""),
                .ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), ""),
                .AutoPotHpPercent = If(nudAutoPotHp IsNot Nothing, nudAutoPotHp.Value, 80D),
                .AutoPotMpPercent = If(nudAutoPotMp IsNot Nothing, nudAutoPotMp.Value, 35D),
                .AlarmVolumePercent = CInt(If(nudAlarmVolume IsNot Nothing, nudAlarmVolume.Value, CDec(_alarmVolumePercent))),
                .SavedConfig = If(includeFullConfig, BuildConfig(), Nothing),
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

    Private Sub ApplySavedConfigToUi(cfg As BotConfig)
        If cfg Is Nothing Then
            Return
        End If

        If txtWindowTitle IsNot Nothing Then
            txtWindowTitle.Text = If(cfg.WindowTitle, "").Trim()
        End If
        SetNumericControlValue(nudLoopMs, cfg.LoopMs)
        SetNumericControlValue(nudRetargetMs, cfg.RetargetMs)
        SetNumericControlValue(nudStuckTargetMs, cfg.StuckTargetMs)
        SetNumericControlValue(nudMobHpThreshold, CDec(cfg.MobHpPresenceThreshold))
        If chkHighMaxHpSpecial IsNot Nothing Then
            chkHighMaxHpSpecial.Checked = cfg.HighMaxHpSpecialEnabled
        End If
        SetNumericControlValue(nudHighMaxHpThreshold, CDec(Math.Max(100, cfg.HighMaxHpThreshold)))

        _bypassHpMpLimits = cfg.BypassHpMpLimits
        If btnBypassLimits IsNot Nothing Then
            btnBypassLimits.Text = If(_bypassHpMpLimits, "Ignore Skill Min HP/MP: ON", "Ignore Skill Min HP/MP: OFF")
            btnBypassLimits.BackColor = If(_bypassHpMpLimits, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        End If

        _bypassStuckTarget = cfg.BypassStuckTarget
        If btnBypassStuck IsNot Nothing Then
            btnBypassStuck.Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF")
            btnBypassStuck.BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        End If

        _partyAutoAccept = cfg.PartyAutoAcceptEnabled
        UpdatePromptAutoAcceptButton()
        _partyAskEnabled = cfg.PartyAskEnabled
        SetNumericControlValue(nudPartyAskSeconds, CDec(Math.Max(1, cfg.PartyAskIntervalMs) / 1000.0))
        If txtPartyAskText IsNot Nothing Then
            txtPartyAskText.Text = If(String.IsNullOrWhiteSpace(cfg.PartyAskText), DefaultPartyAskCommand, cfg.PartyAskText.Trim())
        End If
        UpdatePartyAskButton()

        _lootScannerEnabled = cfg.LootScannerEnabled
        If btnLootScanner IsNot Nothing Then
            btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
            btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        End If
        If txtItemNtfyTopic IsNot Nothing Then
            txtItemNtfyTopic.Text = If(cfg.ItemNtfyTopic, "").Trim()
        End If
        If chkLevelingAgent IsNot Nothing Then
            chkLevelingAgent.Checked = cfg.LevelingAgentEnabled
        End If
        If txtLevelingPreferredMobs IsNot Nothing Then
            txtLevelingPreferredMobs.Text = String.Join(", ", If(cfg.LevelingPreferredMobs, New List(Of String)()))
        End If
        SetNumericControlValue(nudLevelingStopHp, CDec(Math.Max(1, cfg.LevelingStopHpPercent)))
        SetNumericControlValue(nudLevelingStopMp, CDec(Math.Max(1, cfg.LevelingStopMpPercent)))
        SetNumericControlValue(nudLevelingMaxNoTargetSeconds, CDec(Math.Max(5, cfg.LevelingMaxNoTargetSeconds)))
        If chkLevelingStopOnLowExp IsNot Nothing Then
            chkLevelingStopOnLowExp.Checked = cfg.LevelingStopOnLowExpRate
        End If
        SetNumericControlValue(nudLevelingMinExpPerHour, CDec(Math.Max(0.01, cfg.LevelingMinExpPerHour)))
        If chkLevelingStopOnRepeatedUnreachable IsNot Nothing Then
            chkLevelingStopOnRepeatedUnreachable.Checked = cfg.LevelingStopOnRepeatedUnreachable
        End If
        SetNumericControlValue(nudLevelingUnreachableLimit, CDec(Math.Max(1, cfg.LevelingUnreachableLimit)))
        If chkNavigationEnabled IsNot Nothing Then
            chkNavigationEnabled.Checked = cfg.NavigationEnabled
        End If
        If txtMapOpenKey IsNot Nothing Then
            txtMapOpenKey.Text = If(String.IsNullOrWhiteSpace(cfg.MapOpenKey), DefaultMapOpenKey, cfg.MapOpenKey.Trim().ToUpperInvariant())
        End If
        If chkTravelPreview IsNot Nothing Then
            chkTravelPreview.Checked = cfg.NavigationTravelPreviewEnabled
        End If
        If chkTravelExecute IsNot Nothing Then
            chkTravelExecute.Checked = cfg.NavigationTravelExecutionEnabled
        End If
        If chkRouteRecording IsNot Nothing Then
            chkRouteRecording.Checked = cfg.RouteRecordingEnabled
        End If
        If txtRouteRecordingName IsNot Nothing Then
            txtRouteRecordingName.Text = If(String.IsNullOrWhiteSpace(cfg.RouteRecordingName), "jina_route", cfg.RouteRecordingName.Trim())
        End If
        SetNumericControlValue(nudNavigationWaypointRadius, CDec(Math.Max(0, cfg.NavigationWaypointReachRadius)))
        SetNumericControlValue(nudNavigationMoveBurstMs, CDec(Math.Max(100, cfg.NavigationMoveBurstMs)))
        SetNumericControlValue(nudNavigationResampleMs, CDec(Math.Max(250, cfg.NavigationResampleIntervalMs)))
        SetNumericControlValue(nudNavigationStallTimeoutMs, CDec(Math.Max(1500, cfg.NavigationStallTimeoutMs)))
        If chkNavigationRepathOnStuck IsNot Nothing Then
            chkNavigationRepathOnStuck.Checked = cfg.NavigationRepathOnStuck
        End If
        PopulateNavigationNodeCombos()
        If cboNavigationStartNode IsNot Nothing Then
            cboNavigationStartNode.SelectedIndex = 0
        End If
        If cboNavigationTargetNode IsNot Nothing Then
            Dim targetNodeId As String = If(cfg.NavigationTargetNodeId, "").Trim()
            For i As Integer = 0 To cboNavigationTargetNode.Items.Count - 1
                Dim routeName As String = ExtractRecordedRouteNameFromNavigationSelection(cboNavigationTargetNode.Items(i))
                Dim endNode As NavigationNode = If(routeName = "", Nothing, BotEngine.GetRecordedRouteEndNode(routeName, cfg.NavigationMapName))
                If endNode IsNot Nothing AndAlso endNode.Id.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase) Then
                    cboNavigationTargetNode.SelectedIndex = i
                    Exit For
                End If
            Next
        End If

        If chkLootPickup IsNot Nothing Then
            chkLootPickup.Checked = cfg.LootPickupEnabled
        End If
        SetNumericControlValue(nudLootPickupSeconds, CDec(Math.Max(100, cfg.LootPickupIntervalMs) / 1000.0))
        SetNumericControlValue(nudLootNameMatchThreshold, CDec(cfg.LootNameMatchThresholdPercent))

        If cfg.LootRejectClickEnabled AndAlso cfg.LootRejectPointX >= 0 AndAlso cfg.LootRejectPointY >= 0 Then
            _lootRejectPointX = cfg.LootRejectPointX
            _lootRejectPointY = cfg.LootRejectPointY
        Else
            _lootRejectPointX = -1
            _lootRejectPointY = -1
        End If
        _isPickingLootRejectPoint = False
        UpdateLootRejectPointUi()

        UpsertRegionRow("hp_bar", cfg.HpBar)
        UpsertRegionRow("mp_bar", cfg.MpBar)
        UpsertRegionRow("mob_name_rect", cfg.MobNameRect)
        UpsertRegionRow("mob_hp_rect", cfg.MobHpRect)
        UpsertRegionRow("unreachable_text_rect", cfg.UnreachableTextRect)
        UpsertRegionRow("prana_exp_rect", cfg.PranaExpRect)
        UpsertRegionRow("rupiahs_rect", cfg.RupiahsRect)
        UpsertRegionRow("party_invite_scan_rect", cfg.PartyInviteScanRect)
        UpsertRegionRow("party_invite_ok_rect", cfg.PartyInviteOkRect)
        UpsertRegionRow("map_rect", cfg.MapRect)
        UpsertRegionRow("map_coordinate_rect", cfg.MapCoordinateRect)
        If txtLootScanAreaPoints IsNot Nothing Then
            Dim lootPoints As List(Of LootScanPoint) = GetEffectiveLootScanPoints(cfg)
            txtLootScanAreaPoints.Text = FormatLootScanPoints(lootPoints)
        End If

        If cfg.Actions IsNot Nothing AndAlso cfg.Actions.Count > 0 Then
            Dim persisted As New List(Of PersistedCombatAction)()
            Dim actionIndex As Integer = 0
            For Each action As ActionRule In cfg.Actions
                If action Is Nothing Then
                    actionIndex += 1
                    Continue For
                End If

                Dim keyName As String = If(action.KeyName, "").Trim().ToUpperInvariant()
                If keyName = "" Then
                    actionIndex += 1
                    Continue For
                End If

                persisted.Add(New PersistedCombatAction With {
                    .ActionKey = keyName,
                    .RowIndex = actionIndex,
                    .Enabled = action.Enabled,
                    .Role = NormalizePersistedRole(action.Role),
                    .Priority = Math.Max(1, action.Priority),
                    .CooldownSec = Math.Max(0.05, Math.Max(1, action.CooldownMs) / 1000.0),
                    .TriggerPercent = Math.Min(99, Math.Max(1, action.TriggerPercent)),
                    .MinHpPercent = Math.Min(100, Math.Max(1, action.MinHpPercent)),
                    .MinMpPercent = Math.Min(100, Math.Max(1, action.MinMpPercent))
                })
                actionIndex += 1
            Next

            If persisted.Count > 0 Then
                ApplyPersistedCombatActions(persisted)
            End If
        End If
    End Sub

    Private Sub UpsertRegionRow(regionName As String, region As RectRegion)
        If dgvRegions Is Nothing OrElse String.IsNullOrWhiteSpace(regionName) OrElse region Is Nothing Then
            Return
        End If

        For Each row As DataGridViewRow In dgvRegions.Rows
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                row.Cells("X").Value = region.X.ToString()
                row.Cells("Y").Value = region.Y.ToString()
                row.Cells("W").Value = Math.Max(1, region.W).ToString()
                row.Cells("H").Value = Math.Max(1, region.H).ToString()
                Return
            End If
        Next

        dgvRegions.Rows.Add(regionName, region.X.ToString(), region.Y.ToString(), Math.Max(1, region.W).ToString(), Math.Max(1, region.H).ToString())
    End Sub

    Private Shared Sub SetNumericControlValue(control As NumericUpDown, rawValue As Decimal)
        If control Is Nothing Then
            Return
        End If

        Dim bounded As Decimal = Math.Max(control.Minimum, Math.Min(control.Maximum, rawValue))
        control.Value = bounded
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
                .RowIndex = row.Index,
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
        Dim indexed As New Dictionary(Of Integer, PersistedCombatAction)()
        For Each action In actions
            If action Is Nothing Then
                Continue For
            End If
            If action.RowIndex >= 0 AndAlso Not indexed.ContainsKey(action.RowIndex) Then
                indexed(action.RowIndex) = action
            End If
            Dim actionKey As String = If(action.ActionKey, "").Trim().ToUpperInvariant()
            If actionKey = "" Then
                Continue For
            End If
            keyed(actionKey) = action
        Next

        If keyed.Count = 0 AndAlso indexed.Count = 0 Then
            Return
        End If

        For Each row As DataGridViewRow In dgvCombat.Rows
            Dim actionKey As String = SafeCell(row, "Key", "").ToUpperInvariant()
            Dim item As PersistedCombatAction = Nothing
            If indexed.ContainsKey(row.Index) Then
                item = indexed(row.Index)
            ElseIf actionKey <> "" AndAlso keyed.ContainsKey(actionKey) Then
                item = keyed(actionKey)
            End If
            If item Is Nothing Then
                Continue For
            End If

            Dim restoredKey As String = If(item.ActionKey, "").Trim().ToUpperInvariant()
            If restoredKey <> "" Then
                row.Cells("Key").Value = restoredKey
            End If
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
            Case "attack", "heal", "max_health", "mana", "special", "high_max_hp", "stop"
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
        If btnAttack IsNot Nothing Then
            If isRunning Then
                btnAttack.Text = "RUNNING"
                btnAttack.BackColor = Color.FromArgb(220, 70, 55)
                btnAttack.ForeColor = Color.White
            Else
                btnAttack.Text = "PAUSED"
                btnAttack.BackColor = Color.FromArgb(40, 180, 80)
                btnAttack.ForeColor = Color.White
            End If
        End If

        If lblRunState IsNot Nothing Then
            lblRunState.Text = If(isRunning, "BOT RUNNING", "BOT PAUSED")
            lblRunState.BackColor = If(isRunning, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
            lblRunState.ForeColor = Color.White
        End If

        If lblShortcutHint IsNot Nothing Then
            lblShortcutHint.Text = If(isRunning, "Ctrl+Shift -> Pause Bot", "Ctrl+Shift -> Resume Bot")
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
        If status Is Nothing Then
            Return
        End If

        If status.Running AndAlso IsNotificationWarmupActive() Then
            _deadHpConfirmCount = 0
            If _hpZeroPending Then
                CancelHpZeroPendingCountdown(False)
            End If
            Return
        End If

        ' Only count usable (non-black / non-failed) frames toward death confirmation.
        Dim errorText As String = If(status.ErrorMessage, "")
        Dim captureUnavailable As Boolean =
            errorText.IndexOf("capture failed", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
            errorText.IndexOf("unable to capture", StringComparison.OrdinalIgnoreCase) >= 0
        Dim isUsableFrame As Boolean =
            status.Running AndAlso
            status.WindowFound AndAlso
            (errorText = "" OrElse captureUnavailable)

        Dim isDeadHp As Boolean =
            isUsableFrame AndAlso
            status.HpPercent <= DeadZeroThreshold

        If isDeadHp Then
            _deadHpConfirmCount += 1
        Else
            _deadHpConfirmCount = 0
        End If

        Dim recovered As Boolean = status.HpPercent >= DeadRecoverThreshold
        If recovered Then
            _deathNotificationLatched = False
        End If

        If _deadHpConfirmCount >= DeadConfirmRequiredCount Then
            If Not _deathNotificationLatched Then
                _deathNotificationLatched = True
                If _hpZeroPending Then
                    CancelHpZeroPendingCountdown(False)
                End If
                If Not _hpZeroAlarmActive Then
                    StartHpZeroAlarm()
                End If
            End If
            Return
        End If

        If _hpZeroPending Then
            CancelHpZeroPendingCountdown(True)
        End If
        If _hpZeroAlarmActive AndAlso isUsableFrame AndAlso (Not isDeadHp) Then
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
        AppendLog($"HP is zero. Death alert started at volume {_alarmVolumePercent}%.")
        SendHpZeroPhoneAlert()
        Task.Run(Sub() PlayAlarmPulse(_alarmVolumePercent))
        AppendLog("Death confirmed by HP=0 on consecutive frames. Bot will keep running.")
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
        _deadHpConfirmCount = 0
        _deathNotificationLatched = False
        _lastHpZeroNotification = DateTime.MinValue
        _lastWindowMissingNotification = DateTime.MinValue
        _windowMissingNotificationLatched = False
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

        _deadHpConfirmCount = 0
        _deathNotificationLatched = False
        _lastHpZeroNotification = DateTime.MinValue
        _lastWindowMissingNotification = DateTime.MinValue
        _windowMissingNotificationLatched = False
        If reason <> "" Then
            AppendLog(reason)
        End If
    End Sub

    Private Sub SendHpZeroPhoneAlert()
        Dim now As DateTime = DateTime.UtcNow
        If _lastHpZeroNotification <> DateTime.MinValue AndAlso (now - _lastHpZeroNotification).TotalSeconds < 5 Then
            Return
        End If

        _lastHpZeroNotification = now
        Task.Run(
            Async Function()
                Dim sent As Boolean = Await SendPhoneNotificationAsync("KathanaBot HP Alert", "HP reached zero on 5 consecutive valid frames. Character is dead.", DeathNotificationRetryCount)
                If Not sent Then
                    AppendLogSafe("Phone alert failed after retries. Check ntfy topic/network.")
                End If
            End Function)
    End Sub

    Private Sub SendWindowMissingPhoneAlert(Optional captureUnavailable As Boolean = False)
        Dim now As DateTime = DateTime.UtcNow
        If _lastWindowMissingNotification <> DateTime.MinValue AndAlso (now - _lastWindowMissingNotification).TotalSeconds < 5 Then
            Return
        End If

        _lastWindowMissingNotification = now
        Dim body As String =
            If(captureUnavailable,
               "Game capture failed. The game may be hidden, black-screened, minimized, or the screen is unavailable.",
               "Game window not found. The game may have crashed or been closed.")
        Task.Run(
            Async Function()
                Dim sent As Boolean = Await SendPhoneNotificationAsync("KathanaBot Game Alert", body, DeathNotificationRetryCount)
                If Not sent Then
                    AppendLogSafe("Game-window alert failed after retries. Check ntfy topic/network.")
                End If
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

    Private Async Function SendPhoneNotificationAsync(title As String, body As String, Optional maxAttempts As Integer = 1) As Task(Of Boolean)
        Dim attempts As Integer = Math.Max(1, maxAttempts)
        Dim topic As String = GetNtfyTopicName()
        Dim url As String = $"https://ntfy.sh/{Uri.EscapeDataString(topic)}"

        For attempt As Integer = 1 To attempts
            Try
                Using request As New HttpRequestMessage(HttpMethod.Post, url)
                    request.Content = New StringContent(body, Encoding.UTF8, "text/plain")
                    request.Headers.Add("Title", title)
                    request.Headers.Add("Priority", "urgent")
                    request.Headers.Add("Tags", "warning,gamepad")

                    Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                    If response.IsSuccessStatusCode Then
                        AppendLogSafe($"Phone alert sent to ntfy topic '{topic}'.")
                        Return True
                    End If

                    AppendLogSafe($"Phone alert failed ({CInt(response.StatusCode)}) for topic '{topic}' (attempt {attempt}/{attempts}).")
                End Using
            Catch ex As Exception
                AppendLogSafe($"Phone alert failed (attempt {attempt}/{attempts}): {ex.Message}")
            End Try

            If attempt < attempts Then
                Await Task.Delay(1500)
            End If
        Next

        Return False
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
        If pnlWindowFrame Is Nothing Then
            Return
        End If

        If Not active Then
            pnlWindowFrame.BackColor = Color.FromArgb(55, 55, 55)
            If pnlHealthBanner IsNot Nothing Then
                pnlHealthBanner.BackColor = Color.FromArgb(55, 55, 55)
            End If
            Return
        End If

        Dim safePercent As Double = If(Double.IsNaN(percent) OrElse Double.IsInfinity(percent), 100.0, percent)
        Dim bounded As Double = Math.Max(0.0, Math.Min(100.0, safePercent))
        Dim tint As Color = HpColor(bounded)
        pnlWindowFrame.BackColor = tint
        If pnlHealthBanner IsNot Nothing Then
            pnlHealthBanner.BackColor = tint
        End If
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
