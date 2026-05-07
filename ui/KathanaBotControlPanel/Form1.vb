Imports System.Media
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.IO
Imports System.Diagnostics

Public Class Form1
    Private Shared ReadOnly PrimaryKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
    Private Shared ReadOnly FunctionKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
    Private Shared ReadOnly LitePrimarySkillKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8"}
    Private Shared ReadOnly LiteSecondarySkillKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
    Private Shared ReadOnly CustomCombatDefaultKeys As String() = {"F11", "F12", "F13"}
    Private Shared ReadOnly DefaultGameWindowTitle As String = "Kathana   The Coming of the Dark Ages"
    Private Shared ReadOnly PreferredProcessWindowTitle As String = "Kathana - The Coming of the Dark Ages"
    Private Shared ReadOnly LiteWindowSize As New Size(920, 660)
    Private Shared ReadOnly FullWindowSize As New Size(1450, 900)

    Private _edition As BotEdition = BotEdition.Full
    Private ReadOnly _fullEngine As New BotEngine()
    Private ReadOnly _liteEngine As New BotEngine()
    Private ReadOnly _uiTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _enterToggleTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _liteActionEnabledChecks As New Dictionary(Of String, CheckBox)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _liteActionCooldownInputs As New Dictionary(Of String, NumericUpDown)(StringComparer.OrdinalIgnoreCase)
    Private _liteSyncInProgress As Boolean = False
    Private chkLiteBasicAttack As CheckBox
    Private nudLiteBasicAttack As NumericUpDown
    Private chkLiteMage As CheckBox
    Private nudLiteMage As NumericUpDown
    Private chkLitePick As CheckBox
    Private nudLitePick As NumericUpDown
    Private lstLiteProcessWindows As ListBox
    Private txtLiteProcessRename As TextBox
    Private btnLiteAttack As Button
    Private btnLiteStop As Button
    Private btnLiteHelp As Button
    Private lblLiteRunState As Label
    Private lblLiteShortcutHint As Label
    Private lblLiteActiveMode As Label
    Private lblLiteState As Label
    Private lblLiteSystem As Label
    Private lblLiteHp As Label
    Private lblLiteMp As Label
    Private chkLiteAutoPots As CheckBox
    Private btnLiteSelectHpLevel As Button
    Private btnLiteSelectMpLevel As Button
    Private btnLiteAutoPotHelp As Button
    Private btnLitePartyAutoAccept As Button
    Private btnLitePartyAsk As Button
    Private lblLiteHpPoint As Label
    Private lblLiteMpPoint As Label
    Private nudLitePartyAskSeconds As NumericUpDown
    Private txtLitePartyAskText As TextBox
    Private txtLiteAutoPotHelp As TextBox
    Private _mainTabs As TabControl
    Private _liteTab As TabPage
    Private _combatTab As TabPage
    Private _visionTab As TabPage
    Private _autoPotTab As TabPage
    Private _autoLootTab As TabPage
    Private _levelingTab As TabPage
    Private _diagnosticsTab As TabPage
    Private Const HelpScopeAll As String = "all"
    Private Const HelpScopeLite As String = "lite"
    Private Const HelpScopeCombat As String = "combat"
    Private Const HelpScopeVision As String = "vision"
    Private Const HelpScopeAutoPot As String = "auto-pot"
    Private Const HelpScopeAutoLoot As String = "auto-loot"
    Private Const HelpScopeLeveling As String = "leveling"
    Private Const HelpScopeDiagnostics As String = "diagnostics"

    Private txtWindowTitle As TextBox
    Private nudLoopMs As NumericUpDown
    Private nudRetargetMs As NumericUpDown
    Private nudForcedRetargetMs As NumericUpDown
    Private nudMobHpThreshold As NumericUpDown
    Private chkHighMaxHpSpecial As CheckBox
    Private nudHighMaxHpThreshold As NumericUpDown
    Private chkAvoidHighMaxHpTargets As CheckBox
    Private nudAvoidHighMaxHpThreshold As NumericUpDown
    Private lstProcessWindows As ListBox
    Private txtProcessRename As TextBox
    Private btnOverlayToggle As Button
    Private dgvRegions As DataGridView
    Private txtLootScanAreaPoints As TextBox
    Private chkChatTranslationEnabled As CheckBox
    Private chkChatTranslationOverlay As CheckBox
    Private cboChatTargetLanguage As ComboBox
    Private nudChatScanMs As NumericUpDown
    Private nudChatMaxLines As NumericUpDown
    Private lblChatTranslationStatus As Label
    Private picSnapshot As PictureBox
    Private pnlWindowFrame As Panel
    Private btnPickLootRejectPoint As Button
    Private btnClearLootRejectPoint As Button
    Private lblLootRejectPoint As Label
    Private btnPickLootNamePickupPoint As Button
    Private btnClearLootNamePickupPoint As Button
    Private lblLootNamePickupPoint As Label

    Private NotInheritable Class ChatLanguageOption
        Public Property Label As String
        Public Property Code As String

        Public Sub New(label As String, code As String)
            Me.Label = label
            Me.Code = code
        End Sub

        Public Overrides Function ToString() As String
            Return Label
        End Function
    End Class

    Private dgvCombat As DataGridView
    Private chkMonsterFilter As CheckBox
    Private chkLootPickup As CheckBox
    Private chkLootNameAutoPickup As CheckBox
    Private chkLootNamePickupRestoreCursor As CheckBox
    Private nudLootNamePickupOffsetX As NumericUpDown
    Private nudLootNamePickupOffsetY As NumericUpDown
    Private nudLootPickupSeconds As NumericUpDown
    Private nudLootNamePickupClickDelayMs As NumericUpDown
    Private nudLootNamePickupFPressCount As NumericUpDown
    Private nudLootNamePickupFPressGapMs As NumericUpDown
    Private nudLootNamePickupMouseHoldMs As NumericUpDown
    Private lstMonsterFilter As ListBox
    Private lstLootFilter As ListBox
    Private txtMonsterName As TextBox
    Private txtLootName As TextBox
    Private chkLevelingAgent As CheckBox
    Private txtLevelingPreferredMobs As TextBox
    Private chkLevelingStopHp As CheckBox
    Private nudLevelingStopHp As NumericUpDown
    Private chkLevelingStopMp As CheckBox
    Private nudLevelingStopMp As NumericUpDown
    Private chkLevelingMaxNoTarget As CheckBox
    Private nudLevelingMaxNoTargetSeconds As NumericUpDown
    Private chkLevelingStopOnLowExp As CheckBox
    Private nudLevelingMinExpPerHour As NumericUpDown
    Private chkLevelingStopOnRepeatedUnreachable As CheckBox
    Private nudLevelingUnreachableLimit As NumericUpDown
    Private chkNavigationEnabled As CheckBox
    Private txtMapOpenKey As TextBox
    Private chkTravelPreview As CheckBox
    Private chkTravelExecute As CheckBox
    Private btnStartRouteRecording As Button
    Private btnStopRouteRecording As Button
    Private nudManualRouteNodeX As NumericUpDown
    Private nudManualRouteNodeY As NumericUpDown
    Private btnAddManualRouteNode As Button
    Private btnDeleteManualBreadcrumb As Button
    Private btnClearManualBreadcrumbs As Button
    Private btnReplayRoute As Button
    Private nudRouteRecordingIntervalMs As NumericUpDown
    Private nudRouteRecordingMinConfidence As NumericUpDown
    Private nudRouteRecordingNodeSpacing As NumericUpDown
    Private dgvBreadcrumbs As DataGridView
    Private _routeRecordingActive As Boolean = False
    Private _routeRecordingAutoStartedBot As Boolean = False
    Private _breadcrumbsManualEditMode As Boolean = False
    Private _updatingBreadcrumbsGrid As Boolean = False
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
    Private lblFullEdition As Label
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
    Private tblNotificationSettings As TableLayoutPanel
    Private cboNotificationProvider As ComboBox
    Private lblDiscordGlobalWebhook As Label
    Private lblDiscordItemWebhook As Label
    Private lblDiscordStatsWebhook As Label
    Private txtDiscordGlobalWebhookUrl As TextBox
    Private txtDiscordItemWebhookUrl As TextBox
    Private txtDiscordStatsWebhookUrl As TextBox
    Private lblNtfyGlobalTopic As Label
    Private txtItemNtfyTopic As TextBox
    Private lblNtfyItemTopic As Label
    Private txtStatsNtfyTopic As TextBox
    Private lblNtfyStatsTopic As Label
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
    Private nudStuckNoProgressRetargetMs As NumericUpDown
    Private nudLootNameMatchThreshold As NumericUpDown
    Private nudAlarmVolume As NumericUpDown
    Private nudStatsNtfyIntervalMinutes As NumericUpDown
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
    Private _litePartyAutoAccept As Boolean = False
    Private _litePartyAskEnabled As Boolean = False
    Private _lootScannerEnabled As Boolean = True
    Private _overlayForm As CalibrationOverlayForm
    Private _chatTranslationOverlayForm As ChatTranslationOverlayForm
    Private _autoStarted As Boolean = False
    Private _alarmVolumePercent As Integer = 85
    Private _hpZeroAlarmActive As Boolean = False
    Private _lastStatsNotificationUtc As DateTime = DateTime.MinValue
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
    Private _isPickingLootNamePickupPoint As Boolean = False
    Private _lootRejectPointX As Integer = -1
    Private _lootRejectPointY As Integer = -1
    Private _lootNamePickupPointX As Integer = -1
    Private _lootNamePickupPointY As Integer = -1
    Private _liteAutoPotHpPointX As Integer = -1
    Private _liteAutoPotHpPointY As Integer = -1
    Private _liteAutoPotMpPointX As Integer = -1
    Private _liteAutoPotMpPointY As Integer = -1
    Private _pendingLitePointCapture As LitePointCaptureKind = LitePointCaptureKind.None
    Private _liteRightMouseWasDown As Boolean = False
    Private _themeSnapshotCaptured As Boolean = False
    Private _lastUiTintActive As Boolean = False
    Private _lastUiTintColorArgb As Integer = Integer.MinValue
    Private _lastUiTintBlend As Double = -1.0
    Private _fullStatus As New BotStatus()
    Private _liteStatus As New BotStatus()
    Private Const HpZeroAlarmGraceMs As Integer = 60000
    Private Const DeadZeroThreshold As Double = 0.1
    Private Const DeadRecoverThreshold As Double = 2.0
    Private Const DeadConfirmRequiredCount As Integer = 5
    Private Const DeathNotificationRetryCount As Integer = 3
    Private Const StartupNotificationWarmupSeconds As Integer = 20
    Private Const NotificationProviderNtfy As String = "ntfy"
    Private Const NotificationProviderDiscord As String = "discord"
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
    Private ReadOnly _chatTranslator As New TranslationService()
    Private ReadOnly _chatTranslationLock As New SemaphoreSlim(1, 1)
    Private ReadOnly _chatSeenLineKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _chatSeenLineOrder As New Queue(Of String)()
    Private ReadOnly _chatOverlayEntries As New List(Of ChatOverlayLine)()
    Private _lastChatOcrText As String = ""

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

    Private Enum LitePointCaptureKind
        None
        Hp
        Mp
    End Enum

    Private Class PersistedAppState
        Public Property WindowTitle As String = DefaultGameWindowTitle
        Public Property Full As PersistedListState = New PersistedListState()
        Public Property Lite As PersistedLiteState = New PersistedLiteState()
    End Class

    Private Class PersistedListState
        Public Property MonsterFilterEnabled As Boolean = True
        Public Property LootPickupEnabled As Boolean = False
        Public Property LootPickupSeconds As Decimal = 4D
        Public Property LootNameMatchThresholdPercent As Decimal = 80D
        Public Property LootNameAutoPickupEnabled As Boolean = False
        Public Property LootNamePickupOffsetX As Decimal = 0D
        Public Property LootNamePickupOffsetY As Decimal = 18D
        Public Property LootNamePickupPointEnabled As Boolean = False
        Public Property LootNamePickupPointX As Integer = -1
        Public Property LootNamePickupPointY As Integer = -1
        Public Property LootNamePickupClickDelayMs As Decimal = 180D
        Public Property LootNamePickupFPressCount As Decimal = 3D
        Public Property LootNamePickupFPressGapMs As Decimal = 110D
        Public Property LootNamePickupMouseHoldMs As Decimal = 35D
        Public Property LootNamePickupRestoreCursor As Boolean = True
        Public Property LootRejectPointEnabled As Boolean = False
        Public Property LootRejectPointX As Integer = -1
        Public Property LootRejectPointY As Integer = -1
        Public Property PromptAutoAcceptEnabled As Boolean = True
        Public Property AskForPartyEnabled As Boolean = False
        Public Property AskForPartySeconds As Decimal = 30D
        Public Property AskForPartyText As String
        Public Property LootScannerEnabled As Boolean = True
        Public Property NotificationProvider As String = NotificationProviderNtfy
        Public Property DiscordWebhookUrl As String = ""
        Public Property DiscordGlobalWebhookUrl As String = ""
        Public Property DiscordItemWebhookUrl As String = ""
        Public Property DiscordStatsWebhookUrl As String = ""
        Public Property ItemNtfyTopic As String = "add"
        Public Property NtfyTopic As String = ""
        Public Property StatsNtfyTopic As String = ""
        Public Property StatsNtfyIntervalMinutes As Decimal = 30D
        Public Property AutoPotHpPercent As Decimal = 80D
        Public Property AutoPotMpPercent As Decimal = 35D
        Public Property AlarmVolumePercent As Integer = 85
        Public Property SavedConfig As BotConfig = Nothing
        Public Property MonsterNames As List(Of String) = New List(Of String)()
        Public Property LootNames As List(Of String) = New List(Of String)()
        Public Property CombatActions As List(Of PersistedCombatAction) = New List(Of PersistedCombatAction)()
    End Class

    Private Class PersistedLiteState
        Public Property AutoPotsEnabled As Boolean = False
        Public Property HpPointEnabled As Boolean = False
        Public Property HpPointX As Integer = -1
        Public Property HpPointY As Integer = -1
        Public Property MpPointEnabled As Boolean = False
        Public Property MpPointX As Integer = -1
        Public Property MpPointY As Integer = -1
        Public Property PromptAutoAcceptEnabled As Boolean = False
        Public Property AskForPartyEnabled As Boolean = False
        Public Property AskForPartySeconds As Decimal = 30D
        Public Property AskForPartyText As String = DefaultPartyAskCommand
        Public Property Actions As List(Of PersistedCombatAction) = New List(Of PersistedCombatAction)()
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
        Me.DoubleBuffered = True
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint, True)
        UpdateStyles()
        BuildUi()
        SeedDefaults()
        LoadPersistedListState()
        ForceLevelingAgentOffForStartup()
        SetupLiveConfigBindings()
        ApplyDarkTheme(Me)
        CaptureThemeSnapshot(Me)
        _themeSnapshotCaptured = True

        AddHandler _fullEngine.StatusUpdated, Sub(status As BotStatus) OnEngineStatusUpdated(BotEdition.Full, status)
        AddHandler _liteEngine.StatusUpdated, Sub(status As BotStatus) OnEngineStatusUpdated(BotEdition.Lite, status)
        AddHandler _fullEngine.LogLine, Sub(line As String) OnEngineLogLine(BotEdition.Full, line)
        AddHandler _liteEngine.LogLine, Sub(line As String) OnEngineLogLine(BotEdition.Lite, line)

        _uiTimer.Interval = 1000
        AddHandler _uiTimer.Tick, AddressOf UiTimerTick
        _uiTimer.Start()

        _enterToggleTimer.Interval = 45
        AddHandler _enterToggleTimer.Tick, AddressOf EnterToggleTimerTick
        _enterToggleTimer.Start()

        UpdateEditionUiState(False)
        PushLiveConfig()
    End Sub

    Private Sub ForceLevelingAgentOffForStartup()
        If chkLevelingAgent IsNot Nothing Then
            chkLevelingAgent.Checked = False
        End If
    End Sub

    Private Sub SetupLiveConfigBindings()
        AddHandler txtWindowTitle.TextChanged, AddressOf LiveConfigChanged
        If cboNotificationProvider IsNot Nothing Then
            AddHandler cboNotificationProvider.SelectedIndexChanged, AddressOf NotificationProviderChanged
        End If
        If txtDiscordGlobalWebhookUrl IsNot Nothing Then
            AddHandler txtDiscordGlobalWebhookUrl.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtDiscordGlobalWebhookUrl.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtDiscordItemWebhookUrl IsNot Nothing Then
            AddHandler txtDiscordItemWebhookUrl.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtDiscordItemWebhookUrl.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtDiscordStatsWebhookUrl IsNot Nothing Then
            AddHandler txtDiscordStatsWebhookUrl.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtDiscordStatsWebhookUrl.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtNtfyTopic IsNot Nothing Then
            AddHandler txtNtfyTopic.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtNtfyTopic.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtItemNtfyTopic IsNot Nothing Then
            AddHandler txtItemNtfyTopic.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtItemNtfyTopic.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtStatsNtfyTopic IsNot Nothing Then
            AddHandler txtStatsNtfyTopic.TextChanged, AddressOf LiveConfigChanged
            AddHandler txtStatsNtfyTopic.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If nudStatsNtfyIntervalMinutes IsNot Nothing Then
            AddHandler nudStatsNtfyIntervalMinutes.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudStatsNtfyIntervalMinutes.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If txtLootScanAreaPoints IsNot Nothing Then
            AddHandler txtLootScanAreaPoints.TextChanged, AddressOf LiveConfigChanged
        End If
        If chkChatTranslationEnabled IsNot Nothing Then
            AddHandler chkChatTranslationEnabled.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If chkChatTranslationOverlay IsNot Nothing Then
            AddHandler chkChatTranslationOverlay.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If cboChatTargetLanguage IsNot Nothing Then
            AddHandler cboChatTargetLanguage.SelectedIndexChanged, AddressOf LiveConfigChanged
        End If
        If nudChatScanMs IsNot Nothing Then
            AddHandler nudChatScanMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudChatMaxLines IsNot Nothing Then
            AddHandler nudChatMaxLines.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudLoopMs.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudRetargetMs.ValueChanged, AddressOf LiveConfigChanged
        If nudForcedRetargetMs IsNot Nothing Then
            AddHandler nudForcedRetargetMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudMobHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        If chkHighMaxHpSpecial IsNot Nothing Then
            AddHandler chkHighMaxHpSpecial.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudHighMaxHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkAvoidHighMaxHpTargets IsNot Nothing Then
            AddHandler chkAvoidHighMaxHpTargets.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudAvoidHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudAvoidHighMaxHpThreshold.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudAutoPotHp.ValueChanged, AddressOf LiveConfigChanged
        AddHandler nudAutoPotMp.ValueChanged, AddressOf LiveConfigChanged
        If nudStuckTargetMs IsNot Nothing Then
            AddHandler nudStuckTargetMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudStuckNoProgressRetargetMs IsNot Nothing Then
            AddHandler nudStuckNoProgressRetargetMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNameMatchThreshold IsNot Nothing Then
            AddHandler nudLootNameMatchThreshold.ValueChanged, AddressOf LiveConfigChanged
        End If
        AddHandler nudAlarmVolume.ValueChanged, AddressOf LiveConfigChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf LiveConfigChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf LiveConfigChanged
        If chkLootNameAutoPickup IsNot Nothing Then
            AddHandler chkLootNameAutoPickup.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupOffsetX IsNot Nothing Then
            AddHandler nudLootNamePickupOffsetX.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupOffsetY IsNot Nothing Then
            AddHandler nudLootNamePickupOffsetY.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupClickDelayMs IsNot Nothing Then
            AddHandler nudLootNamePickupClickDelayMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupFPressCount IsNot Nothing Then
            AddHandler nudLootNamePickupFPressCount.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupFPressGapMs IsNot Nothing Then
            AddHandler nudLootNamePickupFPressGapMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudLootNamePickupMouseHoldMs IsNot Nothing Then
            AddHandler nudLootNamePickupMouseHoldMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkLootNamePickupRestoreCursor IsNot Nothing Then
            AddHandler chkLootNamePickupRestoreCursor.CheckedChanged, AddressOf LiveConfigChanged
        End If
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
        If chkLevelingStopHp IsNot Nothing Then
            AddHandler chkLevelingStopHp.CheckedChanged, AddressOf LevelingGuardrailToggleChanged
        End If
        If nudLevelingStopHp IsNot Nothing Then
            AddHandler nudLevelingStopHp.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkLevelingStopMp IsNot Nothing Then
            AddHandler chkLevelingStopMp.CheckedChanged, AddressOf LevelingGuardrailToggleChanged
        End If
        If nudLevelingStopMp IsNot Nothing Then
            AddHandler nudLevelingStopMp.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkLevelingMaxNoTarget IsNot Nothing Then
            AddHandler chkLevelingMaxNoTarget.CheckedChanged, AddressOf LevelingGuardrailToggleChanged
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
        If btnStartRouteRecording IsNot Nothing Then
            AddHandler btnStartRouteRecording.Click, AddressOf StartRouteRecordingClicked
        End If
        If btnStopRouteRecording IsNot Nothing Then
            AddHandler btnStopRouteRecording.Click, AddressOf StopRouteRecordingClicked
        End If
        If nudRouteRecordingIntervalMs IsNot Nothing Then
            AddHandler nudRouteRecordingIntervalMs.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudRouteRecordingMinConfidence IsNot Nothing Then
            AddHandler nudRouteRecordingMinConfidence.ValueChanged, AddressOf LiveConfigChanged
        End If
        If nudRouteRecordingNodeSpacing IsNot Nothing Then
            AddHandler nudRouteRecordingNodeSpacing.ValueChanged, AddressOf LiveConfigChanged
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
        AddHandler dgvCombat.CellValueChanged, AddressOf PersistListSettingsChanged
        AddHandler dgvCombat.CellEndEdit, AddressOf PersistListSettingsChanged
        AddHandler dgvRegions.CellValueChanged, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellEndEdit, AddressOf LiveConfigChanged
        AddHandler dgvRegions.CellValueChanged, AddressOf PersistListSettingsChanged
        AddHandler dgvRegions.CellEndEdit, AddressOf PersistListSettingsChanged
        AddHandler dgvRegions.CurrentCellDirtyStateChanged,
            Sub(_s As Object, _e As EventArgs)
                If dgvRegions.IsCurrentCellDirty Then
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
            End Sub
        If chkChatTranslationEnabled IsNot Nothing Then
            AddHandler chkChatTranslationEnabled.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkChatTranslationOverlay IsNot Nothing Then
            AddHandler chkChatTranslationOverlay.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If cboChatTargetLanguage IsNot Nothing Then
            AddHandler cboChatTargetLanguage.SelectedIndexChanged, AddressOf PersistListSettingsChanged
        End If
        If nudChatScanMs IsNot Nothing Then
            AddHandler nudChatScanMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudChatMaxLines IsNot Nothing Then
            AddHandler nudChatMaxLines.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler chkLootPickup.CheckedChanged, AddressOf PersistListSettingsChanged
        AddHandler nudLootPickupSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        If chkLootNameAutoPickup IsNot Nothing Then
            AddHandler chkLootNameAutoPickup.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupOffsetX IsNot Nothing Then
            AddHandler nudLootNamePickupOffsetX.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupOffsetY IsNot Nothing Then
            AddHandler nudLootNamePickupOffsetY.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupClickDelayMs IsNot Nothing Then
            AddHandler nudLootNamePickupClickDelayMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupFPressCount IsNot Nothing Then
            AddHandler nudLootNamePickupFPressCount.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupFPressGapMs IsNot Nothing Then
            AddHandler nudLootNamePickupFPressGapMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootNamePickupMouseHoldMs IsNot Nothing Then
            AddHandler nudLootNamePickupMouseHoldMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLootNamePickupRestoreCursor IsNot Nothing Then
            AddHandler chkLootNamePickupRestoreCursor.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkHighMaxHpSpecial IsNot Nothing Then
            AddHandler chkHighMaxHpSpecial.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudHighMaxHpThreshold.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkAvoidHighMaxHpTargets IsNot Nothing Then
            AddHandler chkAvoidHighMaxHpTargets.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAvoidHighMaxHpThreshold IsNot Nothing Then
            AddHandler nudAvoidHighMaxHpThreshold.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudForcedRetargetMs IsNot Nothing Then
            AddHandler nudForcedRetargetMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudStuckTargetMs IsNot Nothing Then
            AddHandler nudStuckTargetMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudStuckNoProgressRetargetMs IsNot Nothing Then
            AddHandler nudStuckNoProgressRetargetMs.ValueChanged, AddressOf PersistListSettingsChanged
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
        If chkLevelingStopHp IsNot Nothing Then
            AddHandler chkLevelingStopHp.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingStopHp IsNot Nothing Then
            AddHandler nudLevelingStopHp.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLevelingStopMp IsNot Nothing Then
            AddHandler chkLevelingStopMp.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLevelingStopMp IsNot Nothing Then
            AddHandler nudLevelingStopMp.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkLevelingMaxNoTarget IsNot Nothing Then
            AddHandler chkLevelingMaxNoTarget.CheckedChanged, AddressOf PersistListSettingsChanged
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
        If nudRouteRecordingIntervalMs IsNot Nothing Then
            AddHandler nudRouteRecordingIntervalMs.ValueChanged, AddressOf PersistListSettingsChanged
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
        If btnAddManualRouteNode IsNot Nothing Then
            AddHandler btnAddManualRouteNode.Click, AddressOf AddManualRouteNodeClicked
        End If
        If btnDeleteManualBreadcrumb IsNot Nothing Then
            AddHandler btnDeleteManualBreadcrumb.Click, AddressOf DeleteManualBreadcrumbClicked
        End If
        If btnClearManualBreadcrumbs IsNot Nothing Then
            AddHandler btnClearManualBreadcrumbs.Click, AddressOf ClearManualBreadcrumbsClicked
        End If
        If dgvBreadcrumbs IsNot Nothing Then
            AddHandler dgvBreadcrumbs.CellValueChanged, AddressOf BreadcrumbsGridEdited
            AddHandler dgvBreadcrumbs.UserAddedRow, AddressOf BreadcrumbsGridUserAddedRow
            AddHandler dgvBreadcrumbs.UserDeletedRow, AddressOf BreadcrumbsGridUserDeletedRow
            AddHandler dgvBreadcrumbs.CellEndEdit, AddressOf BreadcrumbsGridEdited
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
        If btnReplayRoute IsNot Nothing Then
            AddHandler btnReplayRoute.Click, AddressOf ReplayRouteClicked
        End If
    End Sub

    Private Sub LiveConfigChanged(_sender As Object, _e As EventArgs)
        PushLiveConfig()
        UpdateMainTabIndicators()
    End Sub

    Private Sub PersistListSettingsChanged(_sender As Object, _e As EventArgs)
        SavePersistedListState(False)
    End Sub

    Private Sub LevelingGuardrailToggleChanged(_sender As Object, _e As EventArgs)
        UpdateLevelingGuardrailToggleUi()
        LiveConfigChanged(_sender, _e)
    End Sub

    Private Sub UpdateLevelingGuardrailToggleUi()
        If nudLevelingStopHp IsNot Nothing Then
            nudLevelingStopHp.Enabled = (chkLevelingStopHp Is Nothing OrElse chkLevelingStopHp.Checked)
        End If
        If nudLevelingStopMp IsNot Nothing Then
            nudLevelingStopMp.Enabled = (chkLevelingStopMp Is Nothing OrElse chkLevelingStopMp.Checked)
        End If
        If nudLevelingMaxNoTargetSeconds IsNot Nothing Then
            nudLevelingMaxNoTargetSeconds.Enabled = (chkLevelingMaxNoTarget Is Nothing OrElse chkLevelingMaxNoTarget.Checked)
        End If
    End Sub

    Private Sub NotificationProviderChanged(_sender As Object, _e As EventArgs)
        UpdateNotificationProviderUi()
        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Sub UpdateNotificationProviderUi()
        Dim provider As String = GetNotificationProviderName()
        Dim useDiscord As Boolean = provider = NotificationProviderDiscord
        SetNotificationRowVisible(1, lblDiscordGlobalWebhook, txtDiscordGlobalWebhookUrl, useDiscord)
        SetNotificationRowVisible(2, lblDiscordItemWebhook, txtDiscordItemWebhookUrl, useDiscord)
        SetNotificationRowVisible(3, lblDiscordStatsWebhook, txtDiscordStatsWebhookUrl, useDiscord)
        SetNotificationRowVisible(4, lblNtfyGlobalTopic, txtNtfyTopic, Not useDiscord)
        SetNotificationRowVisible(5, lblNtfyItemTopic, txtItemNtfyTopic, Not useDiscord)
        SetNotificationRowVisible(6, lblNtfyStatsTopic, txtStatsNtfyTopic, Not useDiscord)
    End Sub

    Private Sub SetNotificationRowVisible(rowIndex As Integer, label As Control, editor As Control, visible As Boolean)
        If tblNotificationSettings Is Nothing OrElse rowIndex < 0 OrElse rowIndex >= tblNotificationSettings.RowStyles.Count Then
            Return
        End If

        If label IsNot Nothing Then
            label.Visible = visible
        End If
        If editor IsNot Nothing Then
            editor.Visible = visible
            editor.Enabled = visible
        End If

        tblNotificationSettings.RowStyles(rowIndex).SizeType = SizeType.Absolute
        tblNotificationSettings.RowStyles(rowIndex).Height = If(visible, 42.0F, 0.0F)
        tblNotificationSettings.PerformLayout()
    End Sub

    Private Sub PushLiveConfig()
        If dgvCombat IsNot Nothing AndAlso dgvCombat.IsCurrentCellInEditMode Then
            Return
        End If
        If dgvRegions IsNot Nothing AndAlso dgvRegions.IsCurrentCellInEditMode Then
            Return
        End If

        Try
            _fullEngine.UpdateConfig(BuildFullConfig())
        Catch
        End Try
        Try
            _liteEngine.UpdateConfig(BuildLiteConfig())
        Catch
        End Try
    End Sub

    Private Sub BuildUi()
        BuildFullUi()
    End Sub

    Private Sub BuildFullUi()
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

        _mainTabs = New TabControl() With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .DrawMode = TabDrawMode.OwnerDrawFixed,
            .SizeMode = TabSizeMode.Fixed,
            .ItemSize = New Size(180, 42)
        }
        AddHandler _mainTabs.DrawItem, AddressOf MainTabsDrawItem
        AddHandler _mainTabs.SelectedIndexChanged, AddressOf MainTabsSelectedIndexChanged
        pnlWindowFrame.Controls.Add(_mainTabs)

        pnlHealthBanner = New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 12,
            .BackColor = Color.FromArgb(55, 55, 55)
        }
        pnlWindowFrame.Controls.Add(pnlHealthBanner)
        pnlHealthBanner.BringToFront()

        _liteTab = BuildLiteTab()
        _combatTab = BuildCombatTab()
        _visionTab = BuildVisionTab()
        _autoPotTab = BuildAutoPotTab()
        _mainTabs.TabPages.Add(_liteTab)
        _mainTabs.TabPages.Add(_combatTab)
        _mainTabs.TabPages.Add(_visionTab)
        _mainTabs.TabPages.Add(_autoPotTab)
        _autoLootTab = BuildAutoLootTab()
        _mainTabs.TabPages.Add(_autoLootTab)
        _levelingTab = BuildLevelingTab()
        _mainTabs.TabPages.Add(_levelingTab)
        _diagnosticsTab = BuildDiagnosticsTab()
        _mainTabs.TabPages.Add(_diagnosticsTab)
        _mainTabs.SelectedTab = _combatTab
        UpdateMainTabIndicators()
    End Sub

    Private Function BuildLiteTab() As TabPage
        Dim tab As New TabPage("Lite") With {
            .BackColor = Color.FromArgb(238, 238, 238),
            .ForeColor = Color.FromArgb(45, 45, 45),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular),
            .Tag = "lite-scope"
        }
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(10), .Tag = "lite-scope"}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        tab.Controls.Add(root)

        Dim banner As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(12, 6, 12, 6), .Tag = "lite-scope"}
        Dim lblEdition As New Label() With {
            .Dock = DockStyle.Left,
            .Width = 420,
            .Text = "KathanaBot Lite Version - for slower computers",
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(46, 72, 102),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblLiteActiveMode = New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "ACTIVE BOT: NONE",
            .ForeColor = Color.FromArgb(140, 70, 120),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleRight
        }
        banner.Controls.Add(lblLiteActiveMode)
        banner.Controls.Add(lblEdition)
        root.Controls.Add(banner, 0, 0)

        Dim content As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1}
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 250.0F))
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(content, 0, 1)

        content.Controls.Add(BuildLiteProcessPanel(), 0, 0)
        content.Controls.Add(BuildLiteMainPanel(), 1, 0)
        Return tab
    End Function

    Private Function IsLiteModeActive() As Boolean
        Return _edition = BotEdition.Lite
    End Function

    Private Sub MainTabsSelectedIndexChanged(sender As Object, e As EventArgs)
        UpdateEditionUiState(True)
        UpdateMainTabIndicators()
    End Sub

    Private Sub UpdateEditionUiState(logChange As Boolean)
        Dim previousEdition As BotEdition = _edition
        If _mainTabs IsNot Nothing AndAlso _liteTab IsNot Nothing AndAlso _mainTabs.SelectedTab Is _liteTab Then
            _edition = BotEdition.Lite
        Else
            _edition = BotEdition.Full
        End If

        If _edition = BotEdition.Lite Then
            Text = "KATHANA GAMEBOT - LITE ACTIVE"
        Else
            Text = "KATHANA GAMEBOT - FULL ACTIVE"
        End If

        If WindowState = FormWindowState.Normal Then
            Size = If(_edition = BotEdition.Lite, LiteWindowSize, FullWindowSize)
        End If

        UpdateAttackButtonAppearance(False)

        If logChange AndAlso previousEdition <> _edition Then
            AppendLog($"Edition tab switched to {_edition}. Running bot remains unchanged until Start is pressed.")
        End If
    End Sub

    Private Sub MainTabsDrawItem(sender As Object, e As DrawItemEventArgs)
        If _mainTabs Is Nothing OrElse e.Index < 0 OrElse e.Index >= _mainTabs.TabPages.Count Then
            Return
        End If

        Dim tab As TabPage = _mainTabs.TabPages(e.Index)
        Dim isSelected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Dim isActive As Boolean = IsMainTabActive(tab)
        Dim backColor As Color = GetMainTabBackColor(isActive, isSelected)
        Dim foreColor As Color = If(isActive OrElse isSelected, Color.White, Color.Gainsboro)
        Dim bounds As Rectangle = e.Bounds

        Using backgroundBrush As New SolidBrush(backColor)
            e.Graphics.FillRectangle(backgroundBrush, bounds)
        End Using

        Using borderPen As New Pen(Color.FromArgb(28, 28, 28))
            e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
        End Using

        Dim textBounds As Rectangle = Rectangle.Inflate(bounds, -8, -3)
        TextRenderer.DrawText(e.Graphics, tab.Text, e.Font, textBounds, foreColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub

    Private Function GetMainTabBackColor(isActive As Boolean, isSelected As Boolean) As Color
        If isActive Then
            Return If(isSelected, Color.FromArgb(47, 154, 87), Color.FromArgb(34, 118, 68))
        End If

        Return If(isSelected, Color.FromArgb(92, 92, 92), Color.FromArgb(64, 64, 64))
    End Function

    Private Sub UpdateMainTabIndicators()
        If _mainTabs IsNot Nothing AndAlso Not _mainTabs.IsDisposed Then
            _mainTabs.Invalidate()
        End If
    End Sub

    Private Function IsMainTabActive(tab As TabPage) As Boolean
        If tab Is Nothing Then
            Return False
        End If

        If tab Is _liteTab Then
            Return IsLiteTabActive()
        End If
        If tab Is _combatTab Then
            Return IsCombatTabActive()
        End If
        If tab Is _visionTab Then
            Return IsVisionTabActive()
        End If
        If tab Is _autoPotTab Then
            Return IsAutoPotTabActive()
        End If
        If tab Is _autoLootTab Then
            Return IsAutoLootTabActive()
        End If
        If tab Is _levelingTab Then
            Return IsLevelingTabActive()
        End If

        Return False
    End Function

    Private Function IsLiteTabActive() As Boolean
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If runningEdition.HasValue Then
            Return runningEdition.Value = BotEdition.Lite
        End If

        Return _edition = BotEdition.Lite
    End Function

    Private Function IsCombatTabActive() As Boolean
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If runningEdition.HasValue Then
            Return runningEdition.Value = BotEdition.Full
        End If

        Return _edition = BotEdition.Full
    End Function

    Private Function IsVisionTabActive() As Boolean
        Return (chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked) OrElse
               (_overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed AndAlso _overlayForm.Visible)
    End Function

    Private Function IsAutoPotTabActive() As Boolean
        If GetNotificationProviderName() = NotificationProviderDiscord Then
            Return Not String.IsNullOrWhiteSpace(If(txtDiscordGlobalWebhookUrl IsNot Nothing, txtDiscordGlobalWebhookUrl.Text, "")) OrElse
                   Not String.IsNullOrWhiteSpace(If(txtDiscordItemWebhookUrl IsNot Nothing, txtDiscordItemWebhookUrl.Text, "")) OrElse
                   Not String.IsNullOrWhiteSpace(If(txtDiscordStatsWebhookUrl IsNot Nothing, txtDiscordStatsWebhookUrl.Text, ""))
        End If

        Return Not String.IsNullOrWhiteSpace(If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text, "")) OrElse
               Not String.IsNullOrWhiteSpace(If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text, "")) OrElse
               Not String.IsNullOrWhiteSpace(If(txtStatsNtfyTopic IsNot Nothing, txtStatsNtfyTopic.Text, ""))
    End Function

    Private Function IsAutoLootTabActive() As Boolean
        Return _lootScannerEnabled OrElse
               (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked) OrElse
               (chkLootNameAutoPickup IsNot Nothing AndAlso chkLootNameAutoPickup.Checked)
    End Function

    Private Function IsLevelingTabActive() As Boolean
        Return (chkLevelingAgent IsNot Nothing AndAlso chkLevelingAgent.Checked) OrElse
               (chkNavigationEnabled IsNot Nothing AndAlso chkNavigationEnabled.Checked) OrElse
               (chkTravelPreview IsNot Nothing AndAlso chkTravelPreview.Checked) OrElse
               (chkTravelExecute IsNot Nothing AndAlso chkTravelExecute.Checked) OrElse
               _routeRecordingActive
    End Function

    Private Function HasEnabledCombatActions() As Boolean
        If dgvCombat Is Nothing Then
            Return False
        End If

        For Each row As DataGridViewRow In dgvCombat.Rows
            If row Is Nothing OrElse row.IsNewRow Then
                Continue For
            End If

            Try
                If Convert.ToBoolean(row.Cells("Enabled").Value) Then
                    Return True
                End If
            Catch
            End Try
        Next

        Return False
    End Function

    Private Function BuildLiteProcessPanel() As Control
        Dim panel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(0, 0, 10, 0), .Tag = "lite-scope"}
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 58.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 88.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 74.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 74.0F))

        Dim processGroup As New GroupBox() With {.Text = "Process List", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10), .Tag = "lite-scope"}
        Dim processLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Tag = "lite-scope"}
        processLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        processLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        processLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        processLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        processLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        processGroup.Controls.Add(processLayout)

        lstLiteProcessWindows = New ListBox() With {
            .Dock = DockStyle.Fill,
            .IntegralHeight = False,
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular),
            .Tag = "lite-scope"
        }
        AddHandler lstLiteProcessWindows.SelectedIndexChanged, AddressOf ProcessSelectionChanged
        processLayout.Controls.Add(lstLiteProcessWindows, 0, 0)

        Dim btnRefresh As New Button() With {.Text = "Update", .Dock = DockStyle.Fill, .BackColor = Color.White, .ForeColor = Color.FromArgb(55, 55, 55), .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)}
        AddHandler btnRefresh.Click, AddressOf RefreshProcessListClicked
        processLayout.Controls.Add(btnRefresh, 0, 1)

        processLayout.Controls.Add(New Label() With {.Text = "Rename Process", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)}, 0, 2)

        txtLiteProcessRename = New TextBox() With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        processLayout.Controls.Add(txtLiteProcessRename, 0, 3)

        Dim btnApply As New Button() With {.Text = "Apply", .Dock = DockStyle.Fill, .BackColor = Color.White, .ForeColor = Color.FromArgb(55, 55, 55), .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)}
        AddHandler btnApply.Click, AddressOf ApplyProcessRenameClicked
        processLayout.Controls.Add(btnApply, 0, 4)

        Dim presetGroup As New GroupBox() With {.Text = "Preset", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10)}
        Dim presetLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2}
        presetLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        presetLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        Dim btnSave As New Button() With {.Text = "Save", .Dock = DockStyle.Fill, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)}
        Dim btnLoad As New Button() With {.Text = "Load", .Dock = DockStyle.Fill, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)}
        AddHandler btnSave.Click, AddressOf SaveClicked
        AddHandler btnLoad.Click, AddressOf LoadPresetClicked
        presetLayout.Controls.Add(btnSave, 0, 0)
        presetLayout.Controls.Add(btnLoad, 1, 0)
        presetGroup.Controls.Add(presetLayout)

        Dim modeGroup As New GroupBox() With {.Text = "Version", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10)}
        Dim modeLabel As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Lite is intended for slower computers." & Environment.NewLine &
                    "Full is for the complete feature set on more powerful computers." & Environment.NewLine &
                    "Lite and Full keep separate settings." & Environment.NewLine &
                    "Switching tabs does not stop Full." & Environment.NewLine &
                    "Starting Lite will stop Full first.",
            .ForeColor = Color.FromArgb(150, 78, 118),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        modeGroup.Controls.Add(modeLabel)

        Dim statusGroup As New GroupBox() With {.Text = "Selected Window", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10)}
        Dim statusLabel As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Select the Tantra window here. Lite only uses the process list plus the character HP/MP bars.",
            .ForeColor = Color.FromArgb(90, 90, 90),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        statusGroup.Controls.Add(statusLabel)

        panel.Controls.Add(processGroup, 0, 0)
        panel.Controls.Add(presetGroup, 0, 1)
        panel.Controls.Add(modeGroup, 0, 2)
        panel.Controls.Add(statusGroup, 0, 3)

        Return panel
    End Function

    Private Function BuildLiteMainPanel() As Control
        Dim panel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Tag = "lite-scope"}
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 132.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 86.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))

        Dim topRow As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .Tag = "lite-scope"}
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 76.0F))
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 24.0F))

        Dim modesGroup As New GroupBox() With {.Text = "Attack Modes", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10), .Tag = "lite-scope"}
        Dim modesLayout As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Tag = "lite-scope"}
        modesLayout.Controls.Add(BuildLiteModePanel("Basic Attack", "E", "bullseye", chkLiteBasicAttack, nudLiteBasicAttack, Color.FromArgb(212, 170, 88)))
        modesLayout.Controls.Add(BuildLiteModePanel("Mage", "R", "runner", chkLiteMage, nudLiteMage, Color.FromArgb(88, 138, 210)))
        modesLayout.Controls.Add(BuildLiteModePanel("Pick", "F", "loot", chkLitePick, nudLitePick, Color.FromArgb(228, 176, 77)))
        modesGroup.Controls.Add(modesLayout)

        Dim commandGroup As New GroupBox() With {.Text = "Control", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10), .Tag = "lite-scope"}
        Dim commandLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 3, .Tag = "lite-scope"}
        commandLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        commandLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        commandLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        commandLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        commandLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        lblLiteRunState = New Label() With {.Text = "LITE BOT PAUSED", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White, .Font = New Font("Segoe UI", 8.25F, FontStyle.Bold), .Tag = "lite-scope"}
        btnLiteAttack = New Button() With {.Text = "Start", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(40, 180, 80), .ForeColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .Tag = "lite-scope"}
        btnLiteStop = New Button() With {.Text = "Stop", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(220, 70, 55), .ForeColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .Tag = "lite-scope"}
        btnLiteHelp = New Button() With {.Text = "Explanation (EN/ES/FIL)", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White, .Font = New Font("Segoe UI", 7.75F, FontStyle.Bold), .Tag = "lite-scope", .AccessibleDescription = HelpScopeLite}
        AddHandler btnLiteAttack.Click, AddressOf StartClicked
        AddHandler btnLiteStop.Click, AddressOf StopClicked
        AddHandler btnLiteHelp.Click, AddressOf HelpClicked
        commandLayout.Controls.Add(lblLiteRunState, 0, 0)
        commandLayout.SetColumnSpan(lblLiteRunState, 2)
        commandLayout.Controls.Add(btnLiteAttack, 0, 1)
        commandLayout.Controls.Add(btnLiteStop, 1, 1)
        commandLayout.Controls.Add(btnLiteHelp, 0, 2)
        commandLayout.SetColumnSpan(btnLiteHelp, 2)
        commandGroup.Controls.Add(commandLayout)

        topRow.Controls.Add(modesGroup, 0, 0)
        topRow.Controls.Add(commandGroup, 1, 0)

        Dim statusGroup As New GroupBox() With {.Text = "Status", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(10), .Tag = "lite-scope"}
        Dim statusLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 4, .Tag = "lite-scope"}
        statusLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        statusLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        lblLiteShortcutHint = New Label() With {.Text = "Ctrl+Shift -> Start selected tab", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(187, 88, 138), .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        lblLiteState = New Label() With {.Text = "Status: Lite is ready.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        lblLiteSystem = New Label() With {.Text = "Lite Active: False", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        lblLiteHp = New Label() With {.Text = "HP%: 0.0", .Dock = DockStyle.Fill, .ForeColor = Color.LimeGreen, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        lblLiteMp = New Label() With {.Text = "MP%: 0.0", .Dock = DockStyle.Fill, .ForeColor = Color.DeepSkyBlue, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        statusLayout.Controls.Add(lblLiteShortcutHint, 0, 0)
        statusLayout.Controls.Add(lblLiteState, 1, 0)
        statusLayout.Controls.Add(lblLiteSystem, 0, 1)
        statusLayout.Controls.Add(New Label() With {.Text = "Lite HP/MP points are for AutoPots only. Attacks stay active.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.FromArgb(90, 90, 90), .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}, 1, 1)
        statusLayout.Controls.Add(lblLiteHp, 0, 2)
        statusLayout.Controls.Add(lblLiteMp, 1, 2)
        statusLayout.Controls.Add(New Label() With {.Text = "Selected process controls Lite key send.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.FromArgb(90, 90, 90), .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}, 0, 3)
        statusGroup.Controls.Add(statusLayout)

        Dim lowerArea As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Tag = "lite-scope"}
        lowerArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 66.0F))
        lowerArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34.0F))

        Dim skillArea As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Tag = "lite-scope"}
        skillArea.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        skillArea.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        skillArea.Controls.Add(BuildLiteSkillGroup("Primary Skills", LitePrimarySkillKeys, Color.FromArgb(141, 112, 71)), 0, 0)
        skillArea.Controls.Add(BuildLiteSkillGroup("Secondary Skills", LiteSecondarySkillKeys, Color.FromArgb(111, 123, 140)), 0, 1)
        lowerArea.Controls.Add(skillArea, 0, 0)

        Dim sideArea As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Tag = "lite-scope"}
        sideArea.RowStyles.Add(New RowStyle(SizeType.Percent, 72.0F))
        sideArea.RowStyles.Add(New RowStyle(SizeType.Percent, 28.0F))
        sideArea.Controls.Add(BuildLiteAutoPotsGroup(), 0, 0)
        sideArea.Controls.Add(BuildLitePartyGroup(), 0, 1)
        lowerArea.Controls.Add(sideArea, 1, 0)

        Dim foot As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Lite uses E / R / F plus the Lite skill timers only. Lite timers now allow 1-9999 seconds. Potions use key 9 for Heal and key 0 for Mana (Tantra slot 10).",
            .ForeColor = Color.FromArgb(120, 120, 120),
            .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Tag = "lite-scope"
        }

        panel.Controls.Add(topRow, 0, 0)
        panel.Controls.Add(statusGroup, 0, 1)
        panel.Controls.Add(lowerArea, 0, 2)
        panel.Controls.Add(foot, 0, 3)
        Return panel
    End Function

    Private Function BuildLiteModePanel(title As String, keyName As String, iconKind As String, ByRef check As CheckBox, ByRef input As NumericUpDown, accentColor As Color) As Control
        Dim panel As New Panel() With {.Width = 154, .Height = 66, .BackColor = Color.White, .Margin = New Padding(3, 0, 5, 0), .Tag = "lite-scope"}
        Dim titleLabel As New Label() With {.Left = 8, .Top = 4, .Width = 126, .Height = 15, .Text = $"{title} ({keyName})", .Font = New Font("Segoe UI", 7.75F, FontStyle.Bold), .ForeColor = Color.FromArgb(55, 55, 55), .Tag = "lite-scope"}
        Dim localInput As New NumericUpDown() With {.Left = 8, .Top = 20, .Width = 56, .Minimum = 1D, .Maximum = 9999D, .DecimalPlaces = 0, .Increment = 1D, .Value = 1D, .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}
        Dim colorSwatch As New Panel() With {.Left = 8, .Top = 40, .Width = 126, .Height = 20, .BackColor = accentColor, .Tag = "lite-scope"}
        Dim iconPanel As Panel = BuildLiteIconPanel(iconKind)
        iconPanel.Left = 58
        iconPanel.Top = 2
        Dim localCheck As New CheckBox() With {.Left = 4, .Top = 2, .Width = 18, .Height = 18, .Checked = False, .BackColor = accentColor, .Tag = "lite-scope"}
        Dim keyLabel As New Label() With {.Left = 108, .Top = 2, .Width = 24, .Height = 18, .Text = keyName, .ForeColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleCenter, .BackColor = Color.Transparent, .Tag = "lite-scope"}
        colorSwatch.Controls.Add(localCheck)
        colorSwatch.Controls.Add(iconPanel)
        colorSwatch.Controls.Add(keyLabel)
        panel.Controls.Add(titleLabel)
        panel.Controls.Add(localInput)
        panel.Controls.Add(colorSwatch)

        RegisterLiteActionControl(keyName, localCheck, localInput)
        check = localCheck
        input = localInput
        Return panel
    End Function

    Private Function BuildLiteAutoPotsGroup() As Control
        Dim group As New GroupBox() With {.Text = "AutoPots", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(8), .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .Tag = "lite-scope"}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 8, .Tag = "lite-scope"}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        chkLiteAutoPots = New CheckBox() With {.Text = "Enable AutoPots", .Dock = DockStyle.Fill, .Checked = False, .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .Tag = "lite-scope"}
        AddHandler chkLiteAutoPots.CheckedChanged,
            Sub()
                UpdateLiteAutoPotUi()
                PushLiveConfig()
                SavePersistedListState(False)
            End Sub
        layout.Controls.Add(chkLiteAutoPots, 0, 0)

        btnLiteSelectHpLevel = New Button() With {.Text = "Select HP Level", .Dock = DockStyle.Fill, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        AddHandler btnLiteSelectHpLevel.Click, Sub(_s As Object, _e As EventArgs) BeginLitePointCapture(LitePointCaptureKind.Hp)
        layout.Controls.Add(btnLiteSelectHpLevel, 0, 1)

        btnLiteSelectMpLevel = New Button() With {.Text = "Select Mana level", .Dock = DockStyle.Fill, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        AddHandler btnLiteSelectMpLevel.Click, Sub(_s As Object, _e As EventArgs) BeginLitePointCapture(LitePointCaptureKind.Mp)
        layout.Controls.Add(btnLiteSelectMpLevel, 0, 2)

        lblLiteHpPoint = New Label() With {.Text = "HP X/Y: not set", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(65, 65, 65), .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}
        layout.Controls.Add(lblLiteHpPoint, 0, 3)

        lblLiteMpPoint = New Label() With {.Text = "MP X/Y: not set", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(65, 65, 65), .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}
        layout.Controls.Add(lblLiteMpPoint, 0, 4)

        layout.Controls.Add(New Label() With {.Text = "Potion keys: 9 = Heal, 0 = Mana (Tantra slot 10).", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(160, 82, 82), .Font = New Font("Segoe UI", 7.75F, FontStyle.Bold), .Tag = "lite-scope"}, 0, 5)

        btnLiteAutoPotHelp = New Button() With {.Text = "Help", .Dock = DockStyle.Left, .Width = 72, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        AddHandler btnLiteAutoPotHelp.Click,
            Sub()
                txtLiteAutoPotHelp.Visible = Not txtLiteAutoPotHelp.Visible
                btnLiteAutoPotHelp.Text = If(txtLiteAutoPotHelp.Visible, "Hide Help", "Help")
            End Sub
        layout.Controls.Add(btnLiteAutoPotHelp, 0, 6)

        txtLiteAutoPotHelp = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .Visible = False,
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(70, 70, 70),
            .BorderStyle = BorderStyle.FixedSingle,
            .Font = New Font("Segoe UI", 7.5F, FontStyle.Regular),
            .Text = "EN: Click Select HP Level or Select Mana Level. The app redirects to Tantra. Make sure HP and Mana are full before taking the sample so Lite can learn the bar colors to compare later. RIGHT click the exact HP or MP point where you want the potion to trigger. Lite checks whether red is still present on the HP point and whether blue is still present on the MP point. If the color is missing, it uses potion key 9 for Heal or key 0 for Mana (Tantra slot 10)." & Environment.NewLine & Environment.NewLine &
                    "ES: Haz clic en Select HP Level o Select Mana Level. La app te redirige a Tantra. Asegurate de que el HP y el Mana esten llenos antes de tomar la muestra para que Lite aprenda los colores de la barra y los compare despues. Haz clic DERECHO en el punto exacto del HP o MP donde quieres que se active la pocion. Lite revisa si todavia hay rojo en el punto de HP y si todavia hay azul en el punto de MP. Si el color no esta, usa la tecla 9 para Heal o la tecla 0 para Mana (slot 10 de Tantra).",
            .Tag = "lite-scope"
        }
        layout.Controls.Add(txtLiteAutoPotHelp, 0, 7)

        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildLitePartyGroup() As Control
        Dim group As New GroupBox() With {.Text = "Party Ask", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(8), .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold), .Tag = "lite-scope"}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Tag = "lite-scope"}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 26.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 18.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 26.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))

        btnLitePartyAutoAccept = New Button() With {
            .Text = "Auto Accept Party/Ress: OFF",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(110, 45, 45),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .Tag = "lite-scope"
        }
        AddHandler btnLitePartyAutoAccept.Click, AddressOf TogglePartyAutoAcceptClicked
        layout.Controls.Add(btnLitePartyAutoAccept, 0, 0)

        layout.Controls.Add(New Label() With {.Text = "Ask every (sec)", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(70, 70, 70), .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}, 0, 1)

        nudLitePartyAskSeconds = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 5D, .Maximum = 600D, .Value = 30D, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        AddHandler nudLitePartyAskSeconds.ValueChanged,
            Sub()
                PushLiveConfig()
                SavePersistedListState(False)
            End Sub
        layout.Controls.Add(nudLitePartyAskSeconds, 0, 2)

        layout.Controls.Add(New Label() With {.Text = "Message text", .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(70, 70, 70), .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}, 0, 3)

        txtLitePartyAskText = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultPartyAskCommand, .BackColor = Color.White, .Font = New Font("Segoe UI", 8.0F, FontStyle.Regular), .Tag = "lite-scope"}
        AddHandler txtLitePartyAskText.TextChanged, AddressOf PartyAskTextChanged
        AddHandler txtLitePartyAskText.TextChanged,
            Sub()
                PushLiveConfig()
                SavePersistedListState(False)
            End Sub
        layout.Controls.Add(txtLitePartyAskText, 0, 4)

        btnLitePartyAsk = New Button() With {
            .Text = "Auto Ask Party (add): OFF",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(110, 45, 45),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .Tag = "lite-scope"
        }
        AddHandler btnLitePartyAsk.Click, AddressOf TogglePartyAskClicked
        layout.Controls.Add(btnLitePartyAsk, 0, 5)

        group.Controls.Add(layout)
        UpdateLitePromptAutoAcceptButton()
        UpdateLitePartyAskButton()
        Return group
    End Function

    Private Function BuildLiteIconPanel(iconKind As String) As Panel
        Dim iconPanel As New Panel() With {.Width = 22, .Height = 18, .BackColor = Color.Transparent, .Tag = "lite-scope"}
        AddHandler iconPanel.Paint,
            Sub(_sender As Object, e As PaintEventArgs)
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
                Using pen As New Pen(Color.White, 1.6F)
                    Select Case iconKind
                        Case "bullseye"
                            e.Graphics.DrawEllipse(pen, 3, 2, 12, 12)
                            e.Graphics.DrawEllipse(pen, 6, 5, 6, 6)
                            e.Graphics.DrawLine(pen, 9, 0, 9, 4)
                            e.Graphics.DrawLine(pen, 9, 12, 9, 16)
                            e.Graphics.DrawLine(pen, 1, 8, 5, 8)
                            e.Graphics.DrawLine(pen, 13, 8, 17, 8)
                        Case "runner"
                            e.Graphics.DrawEllipse(pen, 8, 1, 4, 4)
                            e.Graphics.DrawLine(pen, 10, 5, 8, 10)
                            e.Graphics.DrawLine(pen, 8, 10, 4, 12)
                            e.Graphics.DrawLine(pen, 8, 10, 13, 11)
                            e.Graphics.DrawLine(pen, 8, 7, 4, 8)
                            e.Graphics.DrawLine(pen, 9, 7, 14, 5)
                        Case "loot"
                            e.Graphics.DrawLine(pen, 4, 13, 10, 4)
                            e.Graphics.DrawLine(pen, 10, 4, 15, 6)
                            e.Graphics.DrawLine(pen, 10, 4, 14, 1)
                            e.Graphics.DrawLine(pen, 9, 5, 6, 2)
                        Case Else
                            e.Graphics.DrawRectangle(pen, 4, 3, 10, 10)
                    End Select
                End Using
            End Sub
        Return iconPanel
    End Function

    Private Function BuildLiteSkillGroup(title As String, keys As IEnumerable(Of String), accentColor As Color) As Control
        Dim group As New GroupBox() With {.Text = title, .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(251, 251, 251), .Padding = New Padding(8), .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold)}
        Dim flow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .AutoScroll = True}
        For Each keyName As String In keys
            flow.Controls.Add(BuildLiteSkillSlot(keyName, accentColor))
        Next
        group.Controls.Add(flow)
        Return group
    End Function

    Private Function BuildLiteSkillSlot(keyName As String, accentColor As Color) As Control
        Dim panel As New Panel() With {.Width = 72, .Height = 74, .BackColor = Color.White, .Margin = New Padding(2), .Tag = "lite-scope"}
        Dim input As New NumericUpDown() With {.Left = 10, .Top = 6, .Width = 52, .Minimum = 1D, .Maximum = 9999D, .DecimalPlaces = 0, .Increment = 1D, .Value = 1D, .Font = New Font("Segoe UI", 7.75F, FontStyle.Regular), .Tag = "lite-scope"}
        Dim frame As New Panel() With {.Left = 10, .Top = 34, .Width = 52, .Height = 28, .BackColor = accentColor, .Tag = "lite-scope"}
        Dim enabledCheck As New CheckBox() With {
            .Width = 16,
            .Height = 16,
            .Left = 1,
            .Top = 1,
            .Checked = False,
            .BackColor = accentColor,
            .ForeColor = Color.White,
            .Tag = "lite-scope"
        }
        Dim keyLabel As New Label() With {.Left = 0, .Top = 12, .Width = 52, .Height = 14, .Text = keyName, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Color.White, .Font = New Font("Segoe UI", 6.75F, FontStyle.Bold), .BackColor = Color.Transparent, .Tag = "lite-scope"}
        panel.Controls.Add(input)
        panel.Controls.Add(frame)
        frame.Controls.Add(keyLabel)
        frame.Controls.Add(enabledCheck)

        RegisterLiteActionControl(keyName, enabledCheck, input)
        Return panel
    End Function

    Private Sub RegisterLiteActionControl(keyName As String, enabledCheck As CheckBox, input As NumericUpDown)
        _liteActionEnabledChecks(keyName) = enabledCheck
        _liteActionCooldownInputs(keyName) = input
        AddHandler enabledCheck.CheckedChanged, Sub() LiteActionChanged(keyName)
        AddHandler input.ValueChanged, Sub() LiteActionChanged(keyName)
    End Sub

    Private Sub LiteActionChanged(_keyName As String)
        If _liteSyncInProgress Then
            Return
        End If

        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Shared Function GetLiteDefaultRole(keyName As String) As String
        If keyName.Equals("R", StringComparison.OrdinalIgnoreCase) OrElse
           keyName.Equals("F", StringComparison.OrdinalIgnoreCase) OrElse
           keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) Then
            Return "special"
        End If

        Return "attack"
    End Function

    Private Sub ApplyLiteDefaults()
        _liteSyncInProgress = True
        Try
            For Each entry In _liteActionEnabledChecks
                entry.Value.Checked = False
            Next
            For Each entry In _liteActionCooldownInputs
                entry.Value.Value = Math.Max(entry.Value.Minimum, Math.Min(entry.Value.Maximum, 1D))
            Next
            If chkLiteAutoPots IsNot Nothing Then
                chkLiteAutoPots.Checked = False
            End If
            _litePartyAutoAccept = False
            _litePartyAskEnabled = False
            If nudLitePartyAskSeconds IsNot Nothing Then
                nudLitePartyAskSeconds.Value = Math.Max(nudLitePartyAskSeconds.Minimum, Math.Min(nudLitePartyAskSeconds.Maximum, 30D))
            End If
            If txtLitePartyAskText IsNot Nothing Then
                txtLitePartyAskText.Text = DefaultPartyAskCommand
            End If
            _liteAutoPotHpPointX = -1
            _liteAutoPotHpPointY = -1
            _liteAutoPotMpPointX = -1
            _liteAutoPotMpPointY = -1
            _pendingLitePointCapture = LitePointCaptureKind.None
            UpdateLiteAutoPotUi()
            UpdateLitePromptAutoAcceptButton()
            UpdateLitePartyAskButton()
        Finally
            _liteSyncInProgress = False
        End Try
        UpdateMainTabIndicators()
    End Sub

    Private Function GetPersistedLiteActions() As List(Of PersistedCombatAction)
        Dim actions As New List(Of PersistedCombatAction)()
        For Each keyName As String In GetLiteActionKeys()
            If Not _liteActionEnabledChecks.ContainsKey(keyName) OrElse Not _liteActionCooldownInputs.ContainsKey(keyName) Then
                Continue For
            End If

            actions.Add(New PersistedCombatAction With {
                .ActionKey = keyName,
                .Enabled = _liteActionEnabledChecks(keyName).Checked,
                .Role = GetLiteDefaultRole(keyName),
                .Priority = 10 + actions.Count,
                .CooldownSec = Math.Max(1.0, CDbl(_liteActionCooldownInputs(keyName).Value)),
                .TriggerPercent = 1,
                .MinHpPercent = 1,
                .MinMpPercent = 1
            })
        Next
        Return actions
    End Function

    Private Sub ApplyPersistedLiteActions(actions As List(Of PersistedCombatAction))
        ApplyLiteDefaults()
        If actions Is Nothing OrElse actions.Count = 0 Then
            Return
        End If

        Dim keyed As New Dictionary(Of String, PersistedCombatAction)(StringComparer.OrdinalIgnoreCase)
        For Each action As PersistedCombatAction In actions
            If action Is Nothing OrElse String.IsNullOrWhiteSpace(action.ActionKey) Then
                Continue For
            End If
            keyed(action.ActionKey.Trim()) = action
        Next

        _liteSyncInProgress = True
        Try
            For Each keyName As String In GetLiteActionKeys()
                If Not keyed.ContainsKey(keyName) OrElse Not _liteActionEnabledChecks.ContainsKey(keyName) OrElse Not _liteActionCooldownInputs.ContainsKey(keyName) Then
                    Continue For
                End If

                Dim action As PersistedCombatAction = keyed(keyName)
                _liteActionEnabledChecks(keyName).Checked = action.Enabled
                Dim bounded As Decimal = CDec(Math.Max(1, Math.Round(action.CooldownSec, MidpointRounding.AwayFromZero)))
                _liteActionCooldownInputs(keyName).Value = Math.Max(_liteActionCooldownInputs(keyName).Minimum, Math.Min(_liteActionCooldownInputs(keyName).Maximum, bounded))
            Next
        Finally
            _liteSyncInProgress = False
        End Try
    End Sub

    Private Function GetLiteActionKeys() As List(Of String)
        Dim keys As New List(Of String) From {"E", "R", "F"}
        keys.AddRange(LitePrimarySkillKeys)
        keys.AddRange(LiteSecondarySkillKeys)
        Return keys
    End Function

    Private Sub UpdateLiteAutoPotUi()
        If lblLiteHpPoint IsNot Nothing Then
            lblLiteHpPoint.Text = If(_liteAutoPotHpPointX >= 0 AndAlso _liteAutoPotHpPointY >= 0, $"HP X/Y: {_liteAutoPotHpPointX}, {_liteAutoPotHpPointY}", "HP X/Y: not set")
        End If
        If lblLiteMpPoint IsNot Nothing Then
            lblLiteMpPoint.Text = If(_liteAutoPotMpPointX >= 0 AndAlso _liteAutoPotMpPointY >= 0, $"MP X/Y: {_liteAutoPotMpPointX}, {_liteAutoPotMpPointY}", "MP X/Y: not set")
        End If
        If btnLiteSelectHpLevel IsNot Nothing Then
            btnLiteSelectHpLevel.Text = If(_pendingLitePointCapture = LitePointCaptureKind.Hp, "RIGHT click HP bar...", "Select HP Level")
        End If
        If btnLiteSelectMpLevel IsNot Nothing Then
            btnLiteSelectMpLevel.Text = If(_pendingLitePointCapture = LitePointCaptureKind.Mp, "RIGHT click Mana bar...", "Select Mana level")
        End If
        UpdateMainTabIndicators()
    End Sub

    Private Sub LoadPresetClicked(sender As Object, e As EventArgs)
        LoadPersistedListState()
        PushLiveConfig()
        AppendLog("Preset loaded from disk.")
    End Sub

    Private Function BuildCombatTab() As TabPage
        Dim tab As New TabPage("Combat Full") With {.BackColor = Color.FromArgb(20, 20, 20)}
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

    Private Sub AddTabExplanationButton(tab As TabPage, helpScope As String)
        Dim buttonHost As New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 36,
            .Padding = New Padding(8, 4, 8, 4),
            .BackColor = If(String.Equals(helpScope, HelpScopeLite, StringComparison.OrdinalIgnoreCase), Color.FromArgb(244, 244, 244), Color.FromArgb(28, 28, 28))
        }

        Dim button As New Button() With {
            .Text = "Explanation (EN/ES/FIL)",
            .Dock = DockStyle.Right,
            .Width = 190,
            .BackColor = If(String.Equals(helpScope, HelpScopeLite, StringComparison.OrdinalIgnoreCase), Color.White, Color.FromArgb(55, 95, 145)),
            .ForeColor = If(String.Equals(helpScope, HelpScopeLite, StringComparison.OrdinalIgnoreCase), Color.FromArgb(45, 45, 45), Color.White),
            .AccessibleDescription = helpScope
        }
        AddHandler button.Click, AddressOf HelpClicked
        buttonHost.Controls.Add(button)
        tab.Controls.Add(buttonHost)
        buttonHost.BringToFront()
    End Sub

    Private Function BuildVisionTab() As TabPage
        Dim tab As New TabPage("Vision") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(8)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42.0F))
        tab.Controls.Add(root)

        Dim left As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, 380.0F))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim generalGroup As New GroupBox() With {.Text = "Vision + Window Setup", .Dock = DockStyle.Fill}
        Dim generalLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 11}
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

        generalLayout.Controls.Add(New Label() With {.Text = "Normal Retarget (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 1)
        nudRetargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 550}
        generalLayout.Controls.Add(nudRetargetMs, 3, 1)

        generalLayout.Controls.Add(New Label() With {.Text = "Mob HP Presence %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudMobHpThreshold = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 0.1D, .Maximum = 100, .DecimalPlaces = 1, .Increment = 0.1D, .Value = 1.0D}
        generalLayout.Controls.Add(nudMobHpThreshold, 1, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Forced Retarget (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 2)
        nudForcedRetargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 100, .Maximum = 5000, .Value = 550}
        generalLayout.Controls.Add(nudForcedRetargetMs, 3, 2)

        btnOverlayToggle = New Button() With {.Text = "Show Overlay", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White}
        AddHandler btnOverlayToggle.Click, AddressOf ToggleOverlayClicked
        generalLayout.Controls.Add(btnOverlayToggle, 2, 3)

        Dim btnCaptureSnapshot As New Button() With {.Text = "Capture Snapshot", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(30, 80, 120), .ForeColor = Color.White}
        AddHandler btnCaptureSnapshot.Click, AddressOf SnapshotClicked
        generalLayout.Controls.Add(btnCaptureSnapshot, 3, 3)

        chkHighMaxHpSpecial = New CheckBox() With {.Text = "Use special key on high max HP mobs", .Dock = DockStyle.Fill}
        generalLayout.Controls.Add(chkHighMaxHpSpecial, 0, 4)
        generalLayout.SetColumnSpan(chkHighMaxHpSpecial, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Max HP >=", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 4)
        nudHighMaxHpThreshold = New NumericUpDown() With {
            .Dock = DockStyle.Fill,
            .Minimum = 100,
            .Maximum = 50000000,
            .Increment = 100,
            .ThousandsSeparator = True,
            .Value = 2000
        }
        generalLayout.Controls.Add(nudHighMaxHpThreshold, 3, 4)

        chkAvoidHighMaxHpTargets = New CheckBox() With {.Text = "Avoid mobs over max HP", .Dock = DockStyle.Fill}
        generalLayout.Controls.Add(chkAvoidHighMaxHpTargets, 0, 5)
        generalLayout.SetColumnSpan(chkAvoidHighMaxHpTargets, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Avoid Max HP >=", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 5)
        nudAvoidHighMaxHpThreshold = New NumericUpDown() With {
            .Dock = DockStyle.Fill,
            .Minimum = 100,
            .Maximum = 50000000,
            .Increment = 100,
            .ThousandsSeparator = True,
            .Value = 2000
        }
        generalLayout.Controls.Add(nudAvoidHighMaxHpThreshold, 3, 5)

        Dim hint As New Label() With {.Text = "Mob HP Presence % = minimum red-fill detected in Mob HP bar. High max HP special and avoid-high-HP both need mob_hp_rect to include the HP numbers.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}
        generalLayout.Controls.Add(hint, 0, 6)
        generalLayout.SetColumnSpan(hint, 4)

        chkChatTranslationEnabled = New CheckBox() With {.Text = "Enable chat translation OCR", .Dock = DockStyle.Fill}
        generalLayout.Controls.Add(chkChatTranslationEnabled, 0, 7)
        generalLayout.SetColumnSpan(chkChatTranslationEnabled, 2)

        chkChatTranslationOverlay = New CheckBox() With {.Text = "Show translated overlay", .Dock = DockStyle.Fill, .Checked = True}
        generalLayout.Controls.Add(chkChatTranslationOverlay, 2, 7)
        generalLayout.SetColumnSpan(chkChatTranslationOverlay, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Target Lang", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 8)
        cboChatTargetLanguage = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        cboChatTargetLanguage.DisplayMember = NameOf(ChatLanguageOption.Label)
        cboChatTargetLanguage.ValueMember = NameOf(ChatLanguageOption.Code)
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("English", "en"))
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("Espanol", "es"))
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("Filipino", "tl"))
        SelectChatTargetLanguage("en")
        generalLayout.Controls.Add(cboChatTargetLanguage, 1, 8)

        generalLayout.Controls.Add(New Label() With {.Text = "Chat Scan (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 8)
        nudChatScanMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 250, .Maximum = 5000, .Value = 700}
        generalLayout.Controls.Add(nudChatScanMs, 3, 8)

        generalLayout.Controls.Add(New Label() With {.Text = "Overlay Lines", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 9)
        nudChatMaxLines = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 1, .Maximum = 12, .Value = 6}
        generalLayout.Controls.Add(nudChatMaxLines, 1, 9)

        lblChatTranslationStatus = New Label() With {
            .Text = "Chat Translation: idle. Calibrate chat_rect in Regions, then keep the chat window visible.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        generalLayout.Controls.Add(lblChatTranslationStatus, 0, 10)
        generalLayout.SetColumnSpan(lblChatTranslationStatus, 4)

        generalGroup.Controls.Add(generalLayout)
        left.Controls.Add(generalGroup, 0, 0)

        Dim regionGroup As New GroupBox() With {.Text = "Calibration Regions", .Dock = DockStyle.Fill}
        Dim regionLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(6)}
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        regionLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60.0F))
        regionGroup.Controls.Add(regionLayout)

        Dim regionHint As New Label() With {
            .Text = "Rectangle regions stay in the grid. Map coordinates use separate 3-digit X and 3-digit Y boxes. Loot Scan uses points below: x,y | x,y | x,y | x,y",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        regionLayout.Controls.Add(regionHint, 0, 0)

        dgvRegions = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvRegions.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled", .HeaderText = "On", .FillWeight = 28.0F})
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

        AddTabExplanationButton(tab, HelpScopeVision)
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
        Dim notifyLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 11}
        tblNotificationSettings = notifyLayout
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))

        notifyLayout.Controls.Add(New Label() With {.Text = "Notification Provider", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        cboNotificationProvider = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        cboNotificationProvider.Items.AddRange(New Object() {"ntfy", "discord"})
        cboNotificationProvider.SelectedItem = NotificationProviderNtfy
        notifyLayout.Controls.Add(cboNotificationProvider, 1, 0)

        lblDiscordGlobalWebhook = New Label() With {.Text = "Discord Webhook (Global)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblDiscordGlobalWebhook, 0, 1)
        txtDiscordGlobalWebhookUrl = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtDiscordGlobalWebhookUrl, 1, 1)

        lblDiscordItemWebhook = New Label() With {.Text = "Discord Webhook (Items)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblDiscordItemWebhook, 0, 2)
        txtDiscordItemWebhookUrl = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtDiscordItemWebhookUrl, 1, 2)

        lblDiscordStatsWebhook = New Label() With {.Text = "Discord Webhook (Stats)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblDiscordStatsWebhook, 0, 3)
        txtDiscordStatsWebhookUrl = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtDiscordStatsWebhookUrl, 1, 3)

        lblNtfyGlobalTopic = New Label() With {.Text = "ntfy Channel (Global)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblNtfyGlobalTopic, 0, 4)
        txtNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultNtfyTopicName}
        notifyLayout.Controls.Add(txtNtfyTopic, 1, 4)

        lblNtfyItemTopic = New Label() With {.Text = "ntfy Channel (Items)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblNtfyItemTopic, 0, 5)
        txtItemNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtItemNtfyTopic, 1, 5)

        lblNtfyStatsTopic = New Label() With {.Text = "ntfy Channel (Stats)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblNtfyStatsTopic, 0, 6)
        txtStatsNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtStatsNtfyTopic, 1, 6)

        notifyLayout.Controls.Add(New Label() With {.Text = "Stats Interval (min)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 7)
        nudStatsNtfyIntervalMinutes = New NumericUpDown() With {.Minimum = 1D, .Maximum = 1440D, .DecimalPlaces = 0, .Value = 30D, .Dock = DockStyle.Left, .Width = 100}
        notifyLayout.Controls.Add(nudStatsNtfyIntervalMinutes, 1, 7)

        notifyLayout.Controls.Add(New Label() With {.Text = "Loot Matching", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 8)
        notifyLayout.Controls.Add(New Label() With {.Text = "Moved to Auto-Loot tab", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .TextAlign = ContentAlignment.MiddleLeft}, 1, 8)

        Dim note As New Label() With {
            .Text = "Use provider 'discord' with one webhook per alert stream (global, items, stats), or provider 'ntfy' with the topic fields below." & Environment.NewLine &
                    "Use role 'max_health' in Combat Skills if you want the max-health potion threshold controlled here. HP alarm only triggers at HP=0." & Environment.NewLine &
                    "Stats alerts send Prana/EXP %, EXP/hr, Rupiahs total, and Rupiahs/hr on the interval you choose while the bot is running.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        notifyLayout.Controls.Add(note, 0, 9)
        notifyLayout.SetColumnSpan(note, 2)

        Dim notifyFoot As New Label() With {
            .Text = "Discord and ntfy both use separate global/items/stats destinations.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.Gray,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        notifyLayout.Controls.Add(notifyFoot, 0, 10)
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
        Dim btnTestAlarm As New Button() With {.Text = "Test Alarm + Notify", .Width = 150, .Height = 30, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        AddHandler btnTestAlarm.Click, AddressOf TestAlarmClicked
        Dim btnTestPhone As New Button() With {.Text = "Test Notification", .Width = 130, .Height = 30, .BackColor = Color.FromArgb(55, 110, 170), .ForeColor = Color.White}
        AddHandler btnTestPhone.Click, AddressOf TestPhoneAlertClicked
        buttonRow.Controls.Add(btnApply)
        buttonRow.Controls.Add(btnTestAlarm)
        buttonRow.Controls.Add(btnTestPhone)
        settingsLayout.Controls.Add(buttonRow, 0, 1)
        settingsLayout.SetColumnSpan(buttonRow, 2)

        settingsGroup.Controls.Add(settingsLayout)
        root.Controls.Add(settingsGroup, 0, 0)
        root.Controls.Add(BuildAutoPotUnstuckGroup(), 0, 1)
        UpdateNotificationProviderUi()
        tab.Controls.Add(root)
        AddTabExplanationButton(tab, HelpScopeAutoPot)
        Return tab
    End Function

    Private Function BuildAutoPotUnstuckGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Unstuck / Retarget", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))

        Dim controlsPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 4}
        controlsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200.0F))
        controlsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        controlsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))

        controlsPanel.Controls.Add(New Label() With {.Text = "Retarget Key", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        controlsPanel.Controls.Add(New Label() With {.Text = "E", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}, 1, 0)
        controlsPanel.Controls.Add(New Label() With {.Text = "Search Retarget Delay (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        controlsPanel.Controls.Add(New Label() With {.Text = "Stuck Detection Delay (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        controlsPanel.Controls.Add(New Label() With {.Text = "No-Progress Delay (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)

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

        nudStuckNoProgressRetargetMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 1000, .Maximum = 60000, .Increment = 250, .Value = 6000}
        AddHandler nudStuckNoProgressRetargetMs.ValueChanged, Sub(_s As Object, _e As EventArgs) PushLiveConfig()
        controlsPanel.Controls.Add(nudStuckNoProgressRetargetMs, 1, 3)

        Dim note As New Label() With {
            .Text =
                "How these settings work:" & Environment.NewLine &
                "1. Search Retarget Delay: how long the bot waits before pressing E again when it has no usable target." & Environment.NewLine &
                "2. Stuck Detection Delay: minimum combat time before the current target can be treated as stuck." & Environment.NewLine &
                "3. No-Progress Delay: how long the target's HP can stay unchanged before a stuck retarget is allowed." & Environment.NewLine & Environment.NewLine &
                "Recommended starting points:" & Environment.NewLine &
                "- Crowded / fast maps: 4000 to 6000" & Environment.NewLine &
                "- Tanky mobs or longer run-in distance: 7000 to 12000",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.TopLeft,
            .ForeColor = Color.LightSteelBlue,
            .Padding = New Padding(0, 4, 8, 0)
        }
        layout.Controls.Add(controlsPanel, 0, 0)
        layout.Controls.Add(note, 1, 0)

        Dim foot As New Label() With {
            .Text = "Higher values reduce accidental target switching. Lower values make the bot give up on stuck targets faster.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.Gray
        }
        layout.Controls.Add(foot, 0, 1)
        layout.SetColumnSpan(foot, 2)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildAutoLootTab() As TabPage
        Dim tab As New TabPage("Auto-Loot") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(10)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 46.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim left As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 58.0F))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 42.0F))
        left.Controls.Add(BuildLootFilterGroup(), 0, 0)
        left.Controls.Add(BuildLootScanSettingsGroup(), 0, 1)

        Dim right As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1}
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        right.Controls.Add(BuildLootNameAutoPickupGroup(), 0, 0)

        root.Controls.Add(left, 0, 0)
        root.Controls.Add(right, 1, 0)
        tab.Controls.Add(root)
        AddTabExplanationButton(tab, HelpScopeAutoLoot)
        Return tab
    End Function

    Private Function BuildLootScanSettingsGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Loot Scan Matching", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 5}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        layout.Controls.Add(New Label() With {.Text = "Loot Name Match %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        nudLootNameMatchThreshold = New NumericUpDown() With {.Minimum = 50, .Maximum = 100, .Value = DefaultLootNameMatchThresholdPercent, .Dock = DockStyle.Fill}
        layout.Controls.Add(nudLootNameMatchThreshold, 1, 0)

        layout.Controls.Add(New Label() With {.Text = "Loot Scan Area", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        layout.Controls.Add(New Label() With {.Text = "Configured in Vision tab", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .TextAlign = ContentAlignment.MiddleLeft}, 1, 1)

        btnLootScanner = New Button() With {
            .Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF"),
            .Dock = DockStyle.Left,
            .Width = 220,
            .Height = 34,
            .BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        AddHandler btnLootScanner.Click, AddressOf ToggleLootScannerClicked
        layout.Controls.Add(btnLootScanner, 0, 2)
        layout.SetColumnSpan(btnLootScanner, 2)

        Dim note As New Label() With {
            .Text = "Loot Scanner (Alt) reads the loot text from the Vision tab scan area. When an allowed loot name matches here, the pickup-by-name sequence can click the game window on a user-selected client point, wait, and then press F multiple times.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        layout.Controls.Add(note, 0, 4)
        layout.SetColumnSpan(note, 2)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildLootNameAutoPickupGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Pickup By Name (Dynamic Label Click)", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 9}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        chkLootNameAutoPickup = New CheckBox() With {.Text = "Enable pickup by matched loot name", .Dock = DockStyle.Fill, .Checked = False}
        layout.Controls.Add(chkLootNameAutoPickup, 0, 0)
        layout.SetColumnSpan(chkLootNameAutoPickup, 2)

        layout.Controls.Add(New Label() With {.Text = "Click Offset X", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudLootNamePickupOffsetX = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = -300, .Maximum = 300, .Increment = 1, .Value = 0, .Width = 120}
        layout.Controls.Add(nudLootNamePickupOffsetX, 1, 1)

        layout.Controls.Add(New Label() With {.Text = "Click Offset Y", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudLootNamePickupOffsetY = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = -300, .Maximum = 300, .Increment = 1, .Value = 18, .Width = 120}
        layout.Controls.Add(nudLootNamePickupOffsetY, 1, 2)

        layout.Controls.Add(New Label() With {.Text = "Wait Before F (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)
        nudLootNamePickupClickDelayMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 5000, .Increment = 10, .Value = 180, .Width = 120}
        layout.Controls.Add(nudLootNamePickupClickDelayMs, 1, 3)

        layout.Controls.Add(New Label() With {.Text = "Mouse Hold (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 4)
        nudLootNamePickupMouseHoldMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 1000, .Increment = 5, .Value = 35, .Width = 120}
        layout.Controls.Add(nudLootNamePickupMouseHoldMs, 1, 4)

        layout.Controls.Add(New Label() With {.Text = "Press F Count", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 5)
        nudLootNamePickupFPressCount = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 10, .Value = 3, .Width = 120}
        layout.Controls.Add(nudLootNamePickupFPressCount, 1, 5)

        layout.Controls.Add(New Label() With {.Text = "F Gap (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 6)
        nudLootNamePickupFPressGapMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 2000, .Increment = 10, .Value = 110, .Width = 120}
        layout.Controls.Add(nudLootNamePickupFPressGapMs, 1, 6)

        chkLootNamePickupRestoreCursor = New CheckBox() With {.Text = "Restore mouse cursor after click", .Dock = DockStyle.Fill, .Checked = True}
        layout.Controls.Add(chkLootNamePickupRestoreCursor, 0, 7)
        layout.SetColumnSpan(chkLootNamePickupRestoreCursor, 2)

        Dim note As New Label() With {
            .Text = "The bot now uses the matched loot label position from OCR. It clicks at the label's bottom-center plus your X/Y offsets, then waits and presses F. Use Offset Y to move the click lower than the text if the item is on the ground below the label.",
            .Dock = DockStyle.Bottom,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft,
            .AutoSize = False,
            .Height = 68
        }
        layout.Controls.Add(note, 0, 8)
        layout.SetColumnSpan(note, 2)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildDiagnosticsTab() As TabPage
        Dim tab As New TabPage("Diagnostics") With {.BackColor = Color.FromArgb(20, 20, 20)}
        txtDiagnostics = New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Both, .ReadOnly = True, .Font = New Font("Consolas", 9.5F, FontStyle.Regular), .BackColor = Color.FromArgb(10, 10, 10), .ForeColor = Color.LightGray}
        tab.Controls.Add(txtDiagnostics)
        AddTabExplanationButton(tab, HelpScopeDiagnostics)
        Return tab
    End Function

    Private Function BuildLevelingTab() As TabPage
        Dim tab As New TabPage("Leveling") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim scrollPanel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(4), .AutoScroll = True}
        ' Side-by-side root: settings left (55%), agent runtime right (45%)
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        scrollPanel.Controls.Add(root)
        tab.Controls.Add(scrollPanel)

        ' ── LEFT: Leveling Agent Settings ──
        Dim settingsGroup As New GroupBox() With {.Text = "Leveling Agent", .Dock = DockStyle.Fill, .Padding = New Padding(4)}
        Dim settingsScroll As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True}
        Dim settingsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 27}
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For i As Integer = 0 To 26
            settingsLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        settingsScroll.Controls.Add(settingsLayout)
        settingsGroup.Controls.Add(settingsScroll)

        chkLevelingAgent = New CheckBox() With {.Text = "Enable leveling agent", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkLevelingAgent, 0, 0)
        settingsLayout.SetColumnSpan(chkLevelingAgent, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Preferred Mobs", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 1)
        txtLevelingPreferredMobs = New TextBox() With {.Dock = DockStyle.Fill, .PlaceholderText = "mob1, mob2, mob3", .Margin = New Padding(2)}
        settingsLayout.Controls.Add(txtLevelingPreferredMobs, 1, 1)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stop HP %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 2)
        Dim stopHpPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(2)}
        chkLevelingStopHp = New CheckBox() With {.Text = "On", .AutoSize = True, .Checked = True, .Margin = New Padding(0, 4, 8, 0)}
        nudLevelingStopHp = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 100, .Value = 20, .Width = 90, .Margin = New Padding(2)}
        stopHpPanel.Controls.Add(chkLevelingStopHp)
        stopHpPanel.Controls.Add(nudLevelingStopHp)
        settingsLayout.Controls.Add(stopHpPanel, 1, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stop MP %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 3)
        Dim stopMpPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(2)}
        chkLevelingStopMp = New CheckBox() With {.Text = "On", .AutoSize = True, .Checked = True, .Margin = New Padding(0, 4, 8, 0)}
        nudLevelingStopMp = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 100, .Value = 10, .Width = 90, .Margin = New Padding(2)}
        stopMpPanel.Controls.Add(chkLevelingStopMp)
        stopMpPanel.Controls.Add(nudLevelingStopMp)
        settingsLayout.Controls.Add(stopMpPanel, 1, 3)

        settingsLayout.Controls.Add(New Label() With {.Text = "Max No Target (sec)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 4)
        Dim maxNoTargetPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(2)}
        chkLevelingMaxNoTarget = New CheckBox() With {.Text = "On", .AutoSize = True, .Checked = True, .Margin = New Padding(0, 4, 8, 0)}
        nudLevelingMaxNoTargetSeconds = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 5, .Maximum = 600, .Value = 45, .Width = 90, .Margin = New Padding(2)}
        maxNoTargetPanel.Controls.Add(chkLevelingMaxNoTarget)
        maxNoTargetPanel.Controls.Add(nudLevelingMaxNoTargetSeconds)
        settingsLayout.Controls.Add(maxNoTargetPanel, 1, 4)

        chkNavigationEnabled = New CheckBox() With {.Text = "Enable map localization", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkNavigationEnabled, 0, 5)
        settingsLayout.SetColumnSpan(chkNavigationEnabled, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Map Open Key", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 6)
        txtMapOpenKey = New TextBox() With {.Dock = DockStyle.Left, .Width = 90, .Text = DefaultMapOpenKey, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(txtMapOpenKey, 1, 6)

        chkTravelPreview = New CheckBox() With {.Text = "Enable travel preview", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkTravelPreview, 0, 7)
        settingsLayout.SetColumnSpan(chkTravelPreview, 2)

        chkTravelExecute = New CheckBox() With {.Text = "Enable travel execution (guarded)", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkTravelExecute, 0, 8)
        settingsLayout.SetColumnSpan(chkTravelExecute, 2)

        ' ── Route Recording: Start / Stop buttons ──
        Dim recordInstructionsLabel As New Label() With {
            .Text = "COORDINATES: X/Y boxes each read 3 digits. Breadcrumbs add live route nodes when localization confidence is at least Min Confidence %. Lower it to record more but expect more OCR mistakes. Node Spacing is the minimum coordinate distance between saved nodes; lower = more nodes. Map Marker is derived from confident X/Y, so unavailable means no trusted coordinate yet.",
            .Dock = DockStyle.Fill, .ForeColor = Color.FromArgb(255, 200, 120), .AutoSize = True, .Margin = New Padding(2, 4, 2, 4),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Italic)
        }
        settingsLayout.Controls.Add(recordInstructionsLabel, 0, 9)
        settingsLayout.SetColumnSpan(recordInstructionsLabel, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Route Recording", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold), .ForeColor = Color.Plum, .Margin = New Padding(2)}, 0, 10)
        Dim recordBtnPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(2)}
        btnStartRouteRecording = New Button() With {.Text = ChrW(&H23FA) & " Start Recording", .AutoSize = True, .BackColor = Color.FromArgb(30, 140, 60), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .Margin = New Padding(0, 0, 4, 0)}
        btnStopRouteRecording = New Button() With {.Text = ChrW(&H23F9) & " Stop Recording", .AutoSize = True, .BackColor = Color.FromArgb(180, 40, 40), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .Enabled = False}
        recordBtnPanel.Controls.Add(btnStartRouteRecording)
        recordBtnPanel.Controls.Add(btnStopRouteRecording)
        recordBtnPanel.Controls.Add(New Label() With {.Text = "Manual X", .AutoSize = True, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(12, 5, 2, 0)})
        nudManualRouteNodeX = New NumericUpDown() With {.Minimum = 0, .Maximum = 999, .Value = 0, .Width = 58, .Margin = New Padding(0, 0, 4, 0)}
        recordBtnPanel.Controls.Add(nudManualRouteNodeX)
        recordBtnPanel.Controls.Add(New Label() With {.Text = "Y", .AutoSize = True, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2, 5, 2, 0)})
        nudManualRouteNodeY = New NumericUpDown() With {.Minimum = 0, .Maximum = 999, .Value = 0, .Width = 58, .Margin = New Padding(0, 0, 4, 0)}
        recordBtnPanel.Controls.Add(nudManualRouteNodeY)
        btnAddManualRouteNode = New Button() With {.Text = "Add Node", .AutoSize = True, .BackColor = Color.FromArgb(80, 80, 120), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .Margin = New Padding(0, 0, 4, 0)}
        recordBtnPanel.Controls.Add(btnAddManualRouteNode)
        btnDeleteManualBreadcrumb = New Button() With {.Text = "Delete Row", .AutoSize = True, .Margin = New Padding(0, 0, 4, 0)}
        recordBtnPanel.Controls.Add(btnDeleteManualBreadcrumb)
        btnClearManualBreadcrumbs = New Button() With {.Text = "Clear Table", .AutoSize = True, .Margin = New Padding(0, 0, 4, 0)}
        recordBtnPanel.Controls.Add(btnClearManualBreadcrumbs)
        settingsLayout.Controls.Add(recordBtnPanel, 1, 10)

        settingsLayout.Controls.Add(New Label() With {.Text = "Sample Interval (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 11)
        nudRouteRecordingIntervalMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 10, .Maximum = 5000, .Increment = 10, .Value = 100, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudRouteRecordingIntervalMs, 1, 11)

        settingsLayout.Controls.Add(New Label() With {.Text = "Min Confidence %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 12)
        nudRouteRecordingMinConfidence = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 100, .Value = 90, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudRouteRecordingMinConfidence, 1, 12)

        settingsLayout.Controls.Add(New Label() With {.Text = "Node Spacing", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 13)
        nudRouteRecordingNodeSpacing = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1, .Maximum = 100, .Value = 2, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudRouteRecordingNodeSpacing, 1, 13)

        settingsLayout.Controls.Add(New Label() With {.Text = "Route Name", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 14)
        Dim recordingPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(2)}
        txtRouteRecordingName = New TextBox() With {.Width = 160, .Text = "jina_route"}
        recordingPanel.Controls.Add(txtRouteRecordingName)
        btnSaveRouteRecording = New Button() With {.Text = "Save Route", .AutoSize = True}
        recordingPanel.Controls.Add(btnSaveRouteRecording)
        settingsLayout.Controls.Add(recordingPanel, 1, 14)

        settingsLayout.Controls.Add(New Label() With {.Text = "Recorded Routes", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 15)
        Dim recordedRoutePanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(2)}
        cboRecordedRoute = New ComboBox() With {.Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
        recordedRoutePanel.Controls.Add(cboRecordedRoute)
        btnDeleteRecordedRoute = New Button() With {.Text = "Delete", .AutoSize = True}
        recordedRoutePanel.Controls.Add(btnDeleteRecordedRoute)
        btnReplayRoute = New Button() With {.Text = "Replay", .AutoSize = True, .BackColor = Color.FromArgb(30, 100, 180), .ForeColor = Color.White}
        recordedRoutePanel.Controls.Add(btnReplayRoute)
        settingsLayout.Controls.Add(recordedRoutePanel, 1, 15)

        settingsLayout.Controls.Add(New Label() With {.Text = "Route Nodes", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 16)
        Dim recordedNodePanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(2)}
        cboRecordedRouteNode = New ComboBox() With {.Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
        recordedNodePanel.Controls.Add(cboRecordedRouteNode)
        btnDeleteRecordedRouteNode = New Button() With {.Text = "Delete Node", .AutoSize = True}
        recordedNodePanel.Controls.Add(btnDeleteRecordedRouteNode)
        settingsLayout.Controls.Add(recordedNodePanel, 1, 16)

        settingsLayout.Controls.Add(New Label() With {.Text = "Waypoint Radius", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 17)
        nudNavigationWaypointRadius = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0, .Maximum = 250, .Value = 36, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudNavigationWaypointRadius, 1, 17)

        settingsLayout.Controls.Add(New Label() With {.Text = "Move Burst (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 18)
        nudNavigationMoveBurstMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 10, .Maximum = 1500, .Increment = 25, .Value = 350, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudNavigationMoveBurstMs, 1, 18)

        settingsLayout.Controls.Add(New Label() With {.Text = "Re-sample (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 19)
        nudNavigationResampleMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 50, .Maximum = 10000, .Increment = 50, .Value = 1800, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudNavigationResampleMs, 1, 19)

        settingsLayout.Controls.Add(New Label() With {.Text = "Stall Timeout (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 20)
        nudNavigationStallTimeoutMs = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1500, .Maximum = 30000, .Increment = 250, .Value = 6500, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudNavigationStallTimeoutMs, 1, 20)

        chkNavigationRepathOnStuck = New CheckBox() With {.Text = "Repath when travel stalls", .Dock = DockStyle.Fill, .Checked = True, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkNavigationRepathOnStuck, 0, 21)
        settingsLayout.SetColumnSpan(chkNavigationRepathOnStuck, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Route Start", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 22)
        cboNavigationStartNode = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .Enabled = False, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(cboNavigationStartNode, 1, 22)

        settingsLayout.Controls.Add(New Label() With {.Text = "Travel Route", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 23)
        cboNavigationTargetNode = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(cboNavigationTargetNode, 1, 23)

        chkLevelingStopOnLowExp = New CheckBox() With {.Text = "Stop when EXP/hr below threshold", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkLevelingStopOnLowExp, 0, 24)
        settingsLayout.SetColumnSpan(chkLevelingStopOnLowExp, 2)
        settingsLayout.Controls.Add(New Label() With {.Text = "Min EXP/hr %", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 25)
        nudLevelingMinExpPerHour = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 0.01D, .Maximum = 100D, .DecimalPlaces = 2, .Increment = 0.05D, .Value = DefaultLevelingMinExpPerHour, .Width = 90, .Margin = New Padding(2)}
        Dim lowExpPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(2)}
        lowExpPanel.Controls.Add(nudLevelingMinExpPerHour)
        chkLevelingStopOnRepeatedUnreachable = New CheckBox() With {.Text = "Stop after repeated unreachable", .AutoSize = True, .Margin = New Padding(8, 4, 0, 0)}
        lowExpPanel.Controls.Add(chkLevelingStopOnRepeatedUnreachable)
        nudLevelingUnreachableLimit = New NumericUpDown() With {.Minimum = 1, .Maximum = 20, .Value = 4, .Width = 55, .Margin = New Padding(4, 0, 0, 0)}
        lowExpPanel.Controls.Add(nudLevelingUnreachableLimit)
        settingsLayout.Controls.Add(lowExpPanel, 1, 25)

        root.Controls.Add(settingsGroup, 0, 0)

        ' ── RIGHT: Agent Runtime + Breadcrumb Table ──
        Dim rightPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
        rightPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rightPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim statusGroup As New GroupBox() With {.Text = "Agent Runtime", .Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Padding = New Padding(4)}
        Dim statusLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 1, .RowCount = 11, .Padding = New Padding(2)}
        For i As Integer = 0 To 10
            statusLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        statusGroup.Controls.Add(statusLayout)

        lblLevelingState = New Label() With {.Text = "Agent State: Disabled", .Dock = DockStyle.Fill, .ForeColor = Color.Khaki, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}
        lblLevelingReason = New Label() With {.Text = "Reason: Leveling agent is disabled.", .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro, .AutoSize = True, .Margin = New Padding(2)}
        lblMapCoordinate = New Label() With {.Text = "Coordinates X axis: n/a | Coordinates Y axis: n/a | Route node: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightGreen, .AutoSize = True, .Margin = New Padding(2)}
        lblMapHeading = New Label() With {.Text = "Map Heading: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightSkyBlue, .AutoSize = True, .Margin = New Padding(2)}
        lblMapMarker = New Label() With {.Text = "Map Marker: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.Salmon, .AutoSize = True, .Margin = New Padding(2)}
        lblMapLocalizationConfidence = New Label() With {.Text = "Localization Confidence: 0%", .Dock = DockStyle.Fill, .ForeColor = Color.Khaki, .AutoSize = True, .Margin = New Padding(2)}
        lblTravelStatus = New Label() With {.Text = "Travel: idle", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .AutoSize = True, .Margin = New Padding(2)}
        lblRoutePreview = New Label() With {.Text = "Route Preview: disabled", .Dock = DockStyle.Fill, .ForeColor = Color.LightCyan, .AutoSize = True, .Margin = New Padding(2)}
        lblRouteRecording = New Label() With {.Text = "Route Recording: idle", .Dock = DockStyle.Fill, .ForeColor = Color.Plum, .AutoSize = True, .Margin = New Padding(2)}
        Dim hintLabel As New Label() With {.Text = "Mobs filter: agent skips non-matching targets when set.", .Dock = DockStyle.Fill, .ForeColor = Color.LightSkyBlue, .AutoSize = True, .Margin = New Padding(2)}
        Dim guardrailLabel As New Label() With {.Text = "Travel is guarded: map samples, waypoint routes, short bursts when combat idle.", .Dock = DockStyle.Fill, .ForeColor = Color.Silver, .AutoSize = True, .Margin = New Padding(2)}
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

        rightPanel.Controls.Add(statusGroup, 0, 0)

        ' Breadcrumb coordinate table
        Dim breadcrumbGroup As New GroupBox() With {.Text = "Breadcrumbs (Recorded Coordinates)", .Dock = DockStyle.Fill, .Padding = New Padding(4)}
        dgvBreadcrumbs = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .ReadOnly = False,
            .AllowUserToAddRows = True,
            .AllowUserToDeleteRows = True,
            .AllowUserToResizeRows = False,
            .MultiSelect = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .RowHeadersVisible = False,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .BackgroundColor = Color.FromArgb(30, 30, 30),
            .ForeColor = Color.Gainsboro,
            .GridColor = Color.FromArgb(50, 50, 50),
            .BorderStyle = BorderStyle.None
        }
        dgvBreadcrumbs.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Idx", .HeaderText = "#", .FillWeight = 30.0F, .ReadOnly = True})
        dgvBreadcrumbs.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "X", .HeaderText = "X", .FillWeight = 35.0F})
        dgvBreadcrumbs.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Y", .HeaderText = "Y", .FillWeight = 35.0F})
        dgvBreadcrumbs.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "At", .HeaderText = "Captured At", .FillWeight = 70.0F, .ReadOnly = True})
        breadcrumbGroup.Controls.Add(dgvBreadcrumbs)
        rightPanel.Controls.Add(breadcrumbGroup, 0, 1)

        root.Controls.Add(rightPanel, 1, 0)

        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        AddTabExplanationButton(tab, HelpScopeLeveling)
        Return tab
    End Function

    Private Function BuildCombatSkillsGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Combat Skills", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        dgvCombat = New DataGridView() With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        dgvCombat.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled"})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Key", .ReadOnly = True, .FillWeight = 60.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CooldownSec", .FillWeight = 90.0F})
        Dim roleColumn As New DataGridViewComboBoxColumn() With {.Name = "Role", .FillWeight = 80.0F}
        roleColumn.Items.AddRange(New Object() {"attack", "heal", "max_health", "mana", "special", "high_max_hp", "repair", "stop"})
        dgvCombat.Columns.Add(roleColumn)
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Priority", .FillWeight = 75.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "TriggerPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinHpPercent", .FillWeight = 85.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinMpPercent", .FillWeight = 85.0F})
        layout.Controls.Add(dgvCombat, 0, 0)
        layout.Controls.Add(New Label() With {
            .Text = "repair role: watches unreachable_text_rect for '___ is about to break'. After 5 OCR reads it sends the key once, then waits for the warning text to clear before allowing another repair trigger. TriggerPercent is ignored for repair.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildFiltersPanel() As Control
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .Margin = New Padding(0)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(BuildMonsterFilterGroup(), 0, 0)
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
        lblFullEdition = New Label() With {
            .Text = "FULL VERSION - for more powerful computers",
            .Top = 0,
            .Left = 8,
            .Width = 320,
            .Height = 24,
            .ForeColor = Color.FromArgb(80, 170, 255),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblRunState = New Label() With {
            .Text = "BOT PAUSED",
            .Top = 28,
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
            .Top = 62,
            .Left = 8,
            .Width = 280,
            .Height = 28,
            .ForeColor = Color.Gold,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblState = New Label() With {.Text = "Status: Searching for target...", .Top = 94, .Left = 8, .Width = 300, .Height = 22}
        lblSystem = New Label() With {.Text = "System Active: False", .Top = 122, .Left = 8, .Width = 260, .Height = 22, .ForeColor = Color.LightGreen}
        lblHp = New Label() With {.Text = "HP%: 0", .Top = 150, .Left = 8, .Width = 120, .Height = 22, .ForeColor = Color.LimeGreen}
        lblMp = New Label() With {.Text = "MP%: 0", .Top = 150, .Left = 136, .Width = 120, .Height = 22, .ForeColor = Color.DeepSkyBlue}
        lblMobName = New Label() With {.Text = "Mob: (none)", .Top = 174, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.LightSkyBlue}
        lblExpRate = New Label() With {.Text = "Prana/EXP: 0.00% | Rate: Calculating (1m)", .Top = 196, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.Khaki}
        lblRupiahsRate = New Label() With {.Text = "Rupiahs: n/a | Rate: Calculating (1m)", .Top = 218, .Left = 8, .Width = 300, .Height = 22, .ForeColor = Color.Gold}
        btnAttack = New Button() With {.Text = "Attack", .Top = 252, .Left = 8, .Width = 210, .Height = 42, .BackColor = Color.FromArgb(40, 180, 80), .ForeColor = Color.White}
        btnSaveSettings = New Button() With {.Text = "Save Settings", .Top = 306, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(55, 55, 55), .ForeColor = Color.White}
        btnStopBot = New Button() With {.Text = "Stop Bot", .Top = 356, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(20, 130, 210), .ForeColor = Color.White}
        btnBypassLimits = New Button() With {.Text = "Ignore Skill Min HP/MP: OFF", .Top = 406, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        btnBypassStuck = New Button() With {
            .Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF"),
            .Top = 456,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnRetargetNow = New Button() With {.Text = "Retarget Now (E)", .Top = 506, .Left = 8, .Width = 210, .Height = 38, .BackColor = Color.FromArgb(155, 90, 25), .ForeColor = Color.White}
        btnPartyAutoAccept = New Button() With {
            .Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF"),
            .Top = 556,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        Dim lblPartyAskEvery As New Label() With {.Text = "Ask Party Every (sec)", .Top = 602, .Left = 8, .Width = 210, .Height = 22}
        nudPartyAskSeconds = New NumericUpDown() With {.Top = 624, .Left = 8, .Width = 210, .Height = 28, .Minimum = 5, .Maximum = 600, .Value = 30}
        Dim lblPartyAskText As New Label() With {.Text = "Auto Ask Party Text", .Top = 658, .Left = 8, .Width = 210, .Height = 22}
        txtPartyAskText = New TextBox() With {.Top = 680, .Left = 8, .Width = 210, .Height = 28, .Text = DefaultPartyAskCommand}
        btnPartyAsk = New Button() With {
            .Text = If(_partyAskEnabled, "Auto Ask Party (add): ON", "Auto Ask Party (add): OFF"),
            .Top = 714,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnHelp = New Button() With {
            .Text = "Explanation (EN/ES/FIL)",
            .Top = 764,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = Color.FromArgb(70, 70, 70),
            .ForeColor = Color.White,
            .AccessibleDescription = HelpScopeCombat
        }
        AddHandler btnAttack.Click, AddressOf StartClicked
        AddHandler btnSaveSettings.Click, AddressOf SaveClicked
        AddHandler btnStopBot.Click, AddressOf StopClicked
        AddHandler btnBypassLimits.Click, AddressOf ToggleBypassLimitsClicked
        AddHandler btnBypassStuck.Click, AddressOf ToggleStuckTargetBypassClicked
        AddHandler btnRetargetNow.Click, AddressOf ManualRetargetClicked
        AddHandler btnPartyAutoAccept.Click, AddressOf TogglePartyAutoAcceptClicked
        AddHandler btnPartyAsk.Click, AddressOf TogglePartyAskClicked
        AddHandler txtPartyAskText.TextChanged, AddressOf PartyAskTextChanged
        AddHandler btnHelp.Click, AddressOf HelpClicked
        panel.Controls.Add(lblFullEdition)
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
        txtWindowTitle.Text = DefaultGameWindowTitle
        dgvRegions.Rows.Add(True, "hp_bar", "11", "25", "151", "11")
        dgvRegions.Rows.Add(True, "mp_bar", "3", "40", "161", "11")
        dgvRegions.Rows.Add(True, "mob_name_rect", "860", "711", "162", "23")
        dgvRegions.Rows.Add(True, "mob_hp_rect", "859", "737", "165", "11")
        dgvRegions.Rows.Add(True, "unreachable_text_rect", "15", "582", "128", "22")
        dgvRegions.Rows.Add(True, "prana_exp_rect", "472", "745", "78", "21")
        dgvRegions.Rows.Add(True, "rupiahs_rect", "560", "745", "110", "21")
        dgvRegions.Rows.Add(True, "party_invite_scan_rect", "349", "318", "328", "124")
        dgvRegions.Rows.Add(True, "party_invite_ok_rect", "463", "410", "59", "21")
        dgvRegions.Rows.Add(True, "party_list_rect", "0", "24", "168", "244")
        Dim defaultMapX As RectRegion = BotConfig.DefaultMapCoordinateXRect()
        Dim defaultMapY As RectRegion = BotConfig.DefaultMapCoordinateYRect()
        dgvRegions.Rows.Add(True, "map_coordinate_x_rect", defaultMapX.X.ToString(), defaultMapX.Y.ToString(), defaultMapX.W.ToString(), defaultMapX.H.ToString())
        dgvRegions.Rows.Add(True, "map_coordinate_y_rect", defaultMapY.X.ToString(), defaultMapY.Y.ToString(), defaultMapY.W.ToString(), defaultMapY.H.ToString())
        dgvRegions.Rows.Add(True, "chat_rect", "18", "548", "430", "144")
        If txtLootScanAreaPoints IsNot Nothing Then
            txtLootScanAreaPoints.Text = FormatLootScanPoints(BotConfig.CreateDefaultLootScanPoints())
        End If
        If txtMapOpenKey IsNot Nothing Then
            txtMapOpenKey.Text = DefaultMapOpenKey
        End If
        nudMobHpThreshold.Value = 1.0D
        nudRetargetMs.Value = 550D
        If nudForcedRetargetMs IsNot Nothing Then
            nudForcedRetargetMs.Value = 550D
        End If
        If chkHighMaxHpSpecial IsNot Nothing Then
            chkHighMaxHpSpecial.Checked = True
        End If
        If chkAvoidHighMaxHpTargets IsNot Nothing Then
            chkAvoidHighMaxHpTargets.Checked = False
        End If
        If nudAvoidHighMaxHpThreshold IsNot Nothing Then
            nudAvoidHighMaxHpThreshold.Value = 2000D
        End If

        Dim keyIndex As Integer = 1
        dgvCombat.Rows.Clear()
        _partyAutoAccept = False
        _partyAskEnabled = False
        _litePartyAskEnabled = False
        _lootScannerEnabled = False
        For Each key In PrimaryKeys
            dgvCombat.Rows.Add(False, key, "1", "attack", keyIndex * 10, 1, 1, 1)
            keyIndex += 1
        Next
        For Each key In FunctionKeys
            dgvCombat.Rows.Add(False, key, "1", "special", keyIndex * 10, 1, 1, 1)
            keyIndex += 1
        Next
        For i As Integer = 0 To CustomCombatDefaultKeys.Length - 1
            Dim customKey As String = CustomCombatDefaultKeys(i)
            dgvCombat.Rows.Add(False, customKey, "1", "special", keyIndex * 10, 1, 1, 1)
            Dim customRow As DataGridViewRow = dgvCombat.Rows(dgvCombat.Rows.Count - 1)
            customRow.Cells("Key").ReadOnly = False
            keyIndex += 1
        Next
        chkLootPickup.Checked = False
        nudLootPickupSeconds.Value = 1D
        If chkLootNameAutoPickup IsNot Nothing Then
            chkLootNameAutoPickup.Checked = False
        End If
        If nudLootNamePickupOffsetX IsNot Nothing Then
            nudLootNamePickupOffsetX.Value = 0D
        End If
        If nudLootNamePickupOffsetY IsNot Nothing Then
            nudLootNamePickupOffsetY.Value = 18D
        End If
        If nudLootNamePickupClickDelayMs IsNot Nothing Then
            nudLootNamePickupClickDelayMs.Value = 180D
        End If
        If nudLootNamePickupFPressCount IsNot Nothing Then
            nudLootNamePickupFPressCount.Value = 3D
        End If
        If nudLootNamePickupFPressGapMs IsNot Nothing Then
            nudLootNamePickupFPressGapMs.Value = 110D
        End If
        If nudLootNamePickupMouseHoldMs IsNot Nothing Then
            nudLootNamePickupMouseHoldMs.Value = 35D
        End If
        If chkLootNamePickupRestoreCursor IsNot Nothing Then
            chkLootNamePickupRestoreCursor.Checked = True
        End If
        _lootNamePickupPointX = -1
        _lootNamePickupPointY = -1
        _isPickingLootNamePickupPoint = False
        nudAutoPotHp.Value = 1D
        nudAutoPotMp.Value = 1D
        nudAlarmVolume.Value = 1D
        If Not MonsterExists("avara kara") Then
            lstMonsterFilter.Items.Add("avara kara")
        End If
        If txtNtfyTopic IsNot Nothing Then
            txtNtfyTopic.Text = DefaultNtfyTopicName
        End If
        If cboNotificationProvider IsNot Nothing Then
            cboNotificationProvider.SelectedItem = NotificationProviderNtfy
        End If
        If txtDiscordGlobalWebhookUrl IsNot Nothing Then
            txtDiscordGlobalWebhookUrl.Text = ""
        End If
        If txtDiscordItemWebhookUrl IsNot Nothing Then
            txtDiscordItemWebhookUrl.Text = ""
        End If
        If txtDiscordStatsWebhookUrl IsNot Nothing Then
            txtDiscordStatsWebhookUrl.Text = ""
        End If
        If nudPartyAskSeconds IsNot Nothing Then
            nudPartyAskSeconds.Value = 30
        End If
        If txtPartyAskText IsNot Nothing Then
            txtPartyAskText.Text = DefaultPartyAskCommand
        End If
        _alarmVolumePercent = CInt(nudAlarmVolume.Value)
        UpdateAttackButtonAppearance(False)
        UpdateLootNamePickupPointUi()
        UpdateNotificationProviderUi()
        UpdatePromptAutoAcceptButton()
        UpdatePartyAskButton()
        ApplyLiteDefaults()
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

    Private Function ResolveTargetEdition(sender As Object) As BotEdition
        If sender Is btnLiteAttack OrElse sender Is btnLiteStop Then
            Return BotEdition.Lite
        End If
        Return If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full)
    End Function

    Private Function GetEngineForEdition(edition As BotEdition) As BotEngine
        Return If(edition = BotEdition.Lite, _liteEngine, _fullEngine)
    End Function

    Private Function GetStatusForEdition(edition As BotEdition) As BotStatus
        Return If(edition = BotEdition.Lite, _liteStatus, _fullStatus)
    End Function

    Private Function GetRunningEdition() As BotEdition?
        If _liteEngine.IsRunning() Then
            Return BotEdition.Lite
        End If
        If _fullEngine.IsRunning() Then
            Return BotEdition.Full
        End If
        Return Nothing
    End Function

    Private Function IsEditionRunning(edition As BotEdition) As Boolean
        Return GetEngineForEdition(edition).IsRunning()
    End Function

    Private Sub StartEdition(edition As BotEdition, autoStart As Boolean)
        Dim otherEdition As BotEdition = If(edition = BotEdition.Lite, BotEdition.Full, BotEdition.Lite)
        If IsEditionRunning(otherEdition) Then
            StopEdition(otherEdition, False, $"starting {edition.ToString().ToLowerInvariant()}")
        End If

        Dim engine As BotEngine = GetEngineForEdition(edition)
        If engine.IsRunning() Then
            UpdateAttackButtonAppearance(False)
            Return
        End If

        If edition = BotEdition.Full AndAlso _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
            _overlayForm = Nothing
            btnOverlayToggle.Text = "Show Overlay"
            AppendLog("Overlay hidden while bot is running.")
        End If

        SavePersistedListState(False)
        If edition = BotEdition.Full Then
            ResetHpZeroAlarmState("Alarm state reset for bot start.")
            BeginNotificationWarmup()
        End If
        PushLiveConfig()
        engine.Start()
        UpdateAttackButtonAppearance(False)
        If autoStart Then
            AppendLog($"Auto-start on launch enabled for {edition}.")
        End If
    End Sub

    Private Sub StopEdition(edition As BotEdition, triggeredByButton As Boolean, context As String)
        Dim engine As BotEngine = GetEngineForEdition(edition)
        Dim hardStopSent As Boolean = engine.HardStopMovement(txtWindowTitle.Text.Trim(), context)
        If triggeredByButton Then
            If hardStopSent Then
                AppendLog($"Hard stop macro sent for {edition} ({context}).")
            Else
                AppendLog($"Hard stop macro not sent for {edition} ({context}).")
            End If
        End If

        engine.Stop()
        If edition = BotEdition.Full Then
            _notificationWarmupUntilUtc = DateTime.MinValue
            ApplyHealthUiTint(100.0, False)
            ResetHpZeroAlarmState("Alarm state reset for bot stop.")
        End If
        UpdateAttackButtonAppearance(False)
    End Sub

    Private Sub StartClicked(sender As Object, e As EventArgs)
        StartEdition(ResolveTargetEdition(sender), False)
    End Sub

    Private Sub AutoStartOnLaunch()
        If _autoStarted Then
            Return
        End If
        _autoStarted = True
        If _fullEngine.IsRunning() OrElse _liteEngine.IsRunning() Then
            Return
        End If
        StartEdition(BotEdition.Full, True)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        RefreshProcessWindowList(False, IntPtr.Zero)
        AutoStartOnLaunch()
    End Sub

    Private Sub StopClicked(sender As Object, e As EventArgs)
        Dim targetEdition As BotEdition = ResolveTargetEdition(sender)
        If sender Is Nothing Then
            Dim runningEdition As BotEdition? = GetRunningEdition()
            If runningEdition.HasValue Then
                targetEdition = runningEdition.Value
            End If
        End If
        StopEdition(targetEdition, True, "stop button")
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

    Private Function CaptureSnapshotIntoPreview(Optional logWhenUnavailable As Boolean = True) As Boolean
        PushLiveConfig()
        Dim bmp As Bitmap = _fullEngine.CaptureSnapshot()
        If bmp Is Nothing Then
            If logWhenUnavailable Then
                If _fullEngine.IsRunning() Then
                    AppendLog("Snapshot unavailable yet. Wait for the next Vision loop frame.")
                Else
                    AppendLog("Snapshot failed. Window not found or capture failed.")
                End If
            End If
            Return False
        End If

        Dim oldImage = picSnapshot.Image
        picSnapshot.Image = bmp
        If oldImage IsNot Nothing Then
            oldImage.Dispose()
        End If
        AppendLog("Snapshot captured.")
        Return True
    End Function

    Private Sub SnapshotClicked(sender As Object, e As EventArgs)
        CaptureSnapshotIntoPreview(True)
    End Sub

    Private Sub FocusVisionSnapshotForPick(pickDescription As String)
        If _mainTabs IsNot Nothing AndAlso _visionTab IsNot Nothing Then
            _mainTabs.SelectedTab = _visionTab
        End If

        Dim captured As Boolean = CaptureSnapshotIntoPreview(False)
        If captured Then
            AppendLog($"{pickDescription} mode enabled. Click the target point on Snapshot in the Vision tab.")
        Else
            AppendLog($"{pickDescription} mode enabled. Snapshot is not available yet. Capture Snapshot in the Vision tab, then click the target point.")
        End If
    End Sub

    Private Sub PickLootRejectPointClicked(sender As Object, e As EventArgs)
        _isPickingLootRejectPoint = True
        _isPickingLootNamePickupPoint = False
        UpdateLootNamePickupPointUi()
        UpdateLootRejectPointUi()
        FocusVisionSnapshotForPick("Loot reject")
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

    Private Sub PickLootNamePickupPointClicked(sender As Object, e As EventArgs)
        _isPickingLootNamePickupPoint = True
        _isPickingLootRejectPoint = False
        UpdateLootRejectPointUi()
        UpdateLootNamePickupPointUi()
        FocusVisionSnapshotForPick("Loot pickup-point")
    End Sub

    Private Sub ClearLootNamePickupPointClicked(sender As Object, e As EventArgs)
        _isPickingLootNamePickupPoint = False
        _lootNamePickupPointX = -1
        _lootNamePickupPointY = -1
        UpdateLootNamePickupPointUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog("Loot name auto-pickup point cleared.")
    End Sub

    Private Sub SnapshotMouseClick(sender As Object, e As MouseEventArgs)
        If Not _isPickingLootRejectPoint AndAlso Not _isPickingLootNamePickupPoint Then
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

        If _isPickingLootRejectPoint Then
            _lootRejectPointX = mapped.X
            _lootRejectPointY = mapped.Y
            _isPickingLootRejectPoint = False
            UpdateLootRejectPointUi()
            PushLiveConfig()
            SavePersistedListState(False)
            AppendLog($"Loot reject point set: x={_lootRejectPointX}, y={_lootRejectPointY}.")
            Return
        End If

        _lootNamePickupPointX = mapped.X
        _lootNamePickupPointY = mapped.Y
        _isPickingLootNamePickupPoint = False
        UpdateLootNamePickupPointUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog($"Loot name pickup point set: x={_lootNamePickupPointX}, y={_lootNamePickupPointY}.")
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
            picSnapshot.Cursor = If(_isPickingLootRejectPoint OrElse _isPickingLootNamePickupPoint, Cursors.Cross, Cursors.Default)
        End If
    End Sub

    Private Sub UpdateLootNamePickupPointUi()
        If lblLootNamePickupPoint IsNot Nothing Then
            If _lootNamePickupPointX >= 0 AndAlso _lootNamePickupPointY >= 0 Then
                lblLootNamePickupPoint.Text = $"Fixed Click Point: {_lootNamePickupPointX}, {_lootNamePickupPointY}"
            Else
                lblLootNamePickupPoint.Text = "Fixed Click Point: (not set)"
            End If
        End If

        If btnPickLootNamePickupPoint IsNot Nothing Then
            btnPickLootNamePickupPoint.Text = If(_isPickingLootNamePickupPoint, "Click Snapshot...", "Pick Fixed Point")
            btnPickLootNamePickupPoint.BackColor = If(_isPickingLootNamePickupPoint, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
        End If

        If btnClearLootNamePickupPoint IsNot Nothing Then
            btnClearLootNamePickupPoint.Enabled = (_lootNamePickupPointX >= 0 AndAlso _lootNamePickupPointY >= 0)
        End If

        If picSnapshot IsNot Nothing Then
            picSnapshot.Cursor = If(_isPickingLootRejectPoint OrElse _isPickingLootNamePickupPoint, Cursors.Cross, Cursors.Default)
        End If
    End Sub

    Private Sub RefreshProcessListClicked(sender As Object, e As EventArgs)
        RefreshProcessWindowList(True, IntPtr.Zero)
    End Sub

    Private Sub ProcessSelectionChanged(sender As Object, e As EventArgs)
        Dim sourceList As ListBox = TryCast(sender, ListBox)
        Dim selected As ProcessWindowEntry = Nothing
        If sourceList IsNot Nothing Then
            selected = TryCast(sourceList.SelectedItem, ProcessWindowEntry)
        End If
        If selected Is Nothing Then
            selected = GetSelectedProcessWindow()
        End If
        If selected Is Nothing Then
            Return
        End If

        If txtWindowTitle IsNot Nothing AndAlso Not txtWindowTitle.IsDisposed AndAlso String.IsNullOrWhiteSpace(txtWindowTitle.Text) Then
            txtWindowTitle.Text = selected.WindowTitle
        End If
        If txtProcessRename IsNot Nothing AndAlso Not txtProcessRename.IsDisposed Then
            txtProcessRename.Text = selected.WindowTitle
        End If
        If txtLiteProcessRename IsNot Nothing AndAlso Not txtLiteProcessRename.IsDisposed Then
            txtLiteProcessRename.Text = selected.WindowTitle
        End If
        SyncProcessSelectionAcrossLists(selected.MainWindowHandle)
    End Sub

    Private Sub ApplyProcessRenameClicked(sender As Object, e As EventArgs)
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindow()
        If selected Is Nothing Then
            AppendLog("Rename failed: select a process window first.")
            Return
        End If

        Dim newTitle As String = GetProcessRenameText()
        If newTitle = "" Then
            AppendLog("Rename failed: title cannot be empty.")
            Return
        End If

        If SetWindowText(selected.MainWindowHandle, newTitle) Then
            AppendLog($"Window renamed for PID {selected.ProcessId}: '{newTitle}'.")
            txtWindowTitle.Text = newTitle
            If txtProcessRename IsNot Nothing Then
                txtProcessRename.Text = newTitle
            End If
            If txtLiteProcessRename IsNot Nothing Then
                txtLiteProcessRename.Text = newTitle
            End If
            RefreshProcessWindowList(False, selected.MainWindowHandle)
            Return
        End If

        Dim errorCode As Integer = Marshal.GetLastWin32Error()
        AppendLog($"Rename failed for PID {selected.ProcessId}. Win32 error {errorCode}.")
    End Sub

    Private Sub RefreshProcessWindowList(logResult As Boolean, preferredHandle As IntPtr)
        If (lstProcessWindows Is Nothing OrElse lstProcessWindows.IsDisposed) AndAlso (lstLiteProcessWindows Is Nothing OrElse lstLiteProcessWindows.IsDisposed) Then
            Return
        End If

        Dim rememberedHandle As IntPtr = preferredHandle
        If rememberedHandle = IntPtr.Zero Then
            Dim existing As ProcessWindowEntry = GetSelectedProcessWindow()
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

        PopulateProcessListBox(lstProcessWindows, entries, rememberedHandle)
        PopulateProcessListBox(lstLiteProcessWindows, entries, rememberedHandle)

        If logResult Then
            AppendLog($"Process list updated. Found {entries.Count} windows.")
        End If
    End Sub

    Private Sub PopulateProcessListBox(listBox As ListBox, entries As List(Of ProcessWindowEntry), rememberedHandle As IntPtr)
        If listBox Is Nothing OrElse listBox.IsDisposed Then
            Return
        End If

        listBox.BeginUpdate()
        Try
            listBox.Items.Clear()
            For Each entry As ProcessWindowEntry In entries
                listBox.Items.Add(entry)
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
                    For i As Integer = 0 To entries.Count - 1
                        If IsPreferredKathanaWindowTitle(entries(i).WindowTitle) Then
                            targetIndex = i
                            Exit For
                        End If
                    Next
                End If

                If targetIndex < 0 Then
                    targetIndex = 0
                End If
                listBox.SelectedIndex = targetIndex
            End If
        Finally
            listBox.EndUpdate()
        End Try
    End Sub

    Private Function GetSelectedProcessWindowForEdition(edition As BotEdition) As ProcessWindowEntry
        Dim selected As ProcessWindowEntry = Nothing
        If edition = BotEdition.Lite Then
            If lstLiteProcessWindows IsNot Nothing Then
                selected = TryCast(lstLiteProcessWindows.SelectedItem, ProcessWindowEntry)
            End If
            If selected Is Nothing AndAlso lstProcessWindows IsNot Nothing Then
                selected = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
            End If
        Else
            If lstProcessWindows IsNot Nothing Then
                selected = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
            End If
            If selected Is Nothing AndAlso lstLiteProcessWindows IsNot Nothing Then
                selected = TryCast(lstLiteProcessWindows.SelectedItem, ProcessWindowEntry)
            End If
        End If
        If selected IsNot Nothing Then
            Return selected
        End If
        If lstProcessWindows IsNot Nothing Then
            selected = TryCast(lstProcessWindows.SelectedItem, ProcessWindowEntry)
        End If
        If selected Is Nothing AndAlso lstLiteProcessWindows IsNot Nothing Then
            selected = TryCast(lstLiteProcessWindows.SelectedItem, ProcessWindowEntry)
        End If
        Return selected
    End Function

    Private Function GetSelectedProcessWindow() As ProcessWindowEntry
        Return GetSelectedProcessWindowForEdition(If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full))
    End Function

    Private Shared Function IsPreferredKathanaWindowTitle(title As String) As Boolean
        Dim value As String = If(title, "").Trim()
        If value = "" Then
            Return False
        End If

        Return value.Equals(PreferredProcessWindowTitle, StringComparison.OrdinalIgnoreCase) OrElse
               value.Equals(DefaultGameWindowTitle, StringComparison.OrdinalIgnoreCase) OrElse
               value.IndexOf("The Coming of the Dark Ages", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function GetProcessRenameText() As String
        Dim preferred As String = If(IsLiteModeActive(), If(txtLiteProcessRename IsNot Nothing, txtLiteProcessRename.Text, ""), If(txtProcessRename IsNot Nothing, txtProcessRename.Text, ""))
        preferred = preferred.Trim()
        If preferred <> "" Then
            Return preferred
        End If
        If txtProcessRename IsNot Nothing Then
            preferred = txtProcessRename.Text.Trim()
            If preferred <> "" Then
                Return preferred
            End If
        End If
        If txtLiteProcessRename IsNot Nothing Then
            Return txtLiteProcessRename.Text.Trim()
        End If
        Return ""
    End Function

    Private Sub SyncProcessSelectionAcrossLists(handle As IntPtr)
        If handle = IntPtr.Zero Then
            Return
        End If
        SyncProcessSelectionInList(lstProcessWindows, handle)
        SyncProcessSelectionInList(lstLiteProcessWindows, handle)
    End Sub

    Private Sub SyncProcessSelectionInList(listBox As ListBox, handle As IntPtr)
        If listBox Is Nothing OrElse listBox.IsDisposed Then
            Return
        End If
        For i As Integer = 0 To listBox.Items.Count - 1
            Dim entry As ProcessWindowEntry = TryCast(listBox.Items(i), ProcessWindowEntry)
            If entry IsNot Nothing AndAlso entry.MainWindowHandle = handle Then
                If listBox.SelectedIndex <> i Then
                    listBox.SelectedIndex = i
                End If
                Exit For
            End If
        Next
    End Sub

    Private Sub BeginLitePointCapture(kind As LitePointCaptureKind)
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindow()
        If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
            AppendLog("Lite AutoPots: select a process window first.")
            Return
        End If

        _pendingLitePointCapture = kind
        _liteRightMouseWasDown = False
        UpdateLiteAutoPotUi()
        AppendLog($"Lite AutoPots: switching to Tantra. Make sure HP and Mana are full, then RIGHT click the {(If(kind = LitePointCaptureKind.Hp, "HP", "Mana"))} bar where the potion should be used.")
        AppendLog("Lite AutoPots: click inside the bar where there are no numbers or letters so Lite can sample the full bar color, then keep the HP window in the same place.")
        NativeMethods.SetForegroundWindow(selected.MainWindowHandle)
    End Sub

    Private Sub HandlePendingLitePointCapture()
        If _pendingLitePointCapture = LitePointCaptureKind.None Then
            Return
        End If

        Dim selected As ProcessWindowEntry = GetSelectedProcessWindow()
        If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
            Return
        End If

        Dim rightDown As Boolean = (GetAsyncKeyState(CInt(Keys.RButton)) And &H8000S) <> 0
        If rightDown AndAlso Not _liteRightMouseWasDown Then
            Dim screenPoint As NativeMethods.POINT
            If NativeMethods.GetCursorPos(screenPoint) Then
                Dim hoveredWindow As IntPtr = NativeMethods.WindowFromPoint(screenPoint)
                Dim hoveredRoot As IntPtr = If(hoveredWindow <> IntPtr.Zero, NativeMethods.GetAncestor(hoveredWindow, NativeMethods.GA_ROOT), IntPtr.Zero)
                If hoveredRoot <> selected.MainWindowHandle Then
                    _liteRightMouseWasDown = rightDown
                    Return
                End If

                Dim clientPoint As NativeMethods.POINT = screenPoint
                If NativeMethods.ScreenToClient(selected.MainWindowHandle, clientPoint) Then
                    Dim clientRect As NativeMethods.RECT
                    If Not NativeMethods.GetClientRect(selected.MainWindowHandle, clientRect) Then
                        _liteRightMouseWasDown = rightDown
                        Return
                    End If

                    Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
                    Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
                    If clientPoint.X < 0 OrElse clientPoint.Y < 0 OrElse clientPoint.X >= clientWidth OrElse clientPoint.Y >= clientHeight Then
                        AppendLog("Lite AutoPots: right click must be inside the selected Tantra window.")
                        _liteRightMouseWasDown = rightDown
                        Return
                    End If

                    If _pendingLitePointCapture = LitePointCaptureKind.Hp Then
                        _liteAutoPotHpPointX = Math.Max(0, clientPoint.X)
                        _liteAutoPotHpPointY = Math.Max(0, clientPoint.Y)
                        AppendLog($"Lite AutoPots: HP point saved at {_liteAutoPotHpPointX}, {_liteAutoPotHpPointY}.")
                    ElseIf _pendingLitePointCapture = LitePointCaptureKind.Mp Then
                        _liteAutoPotMpPointX = Math.Max(0, clientPoint.X)
                        _liteAutoPotMpPointY = Math.Max(0, clientPoint.Y)
                        AppendLog($"Lite AutoPots: Mana point saved at {_liteAutoPotMpPointX}, {_liteAutoPotMpPointY}.")
                    End If
                    _pendingLitePointCapture = LitePointCaptureKind.None
                    UpdateLiteAutoPotUi()
                    PushLiveConfig()
                    SavePersistedListState(False)
                End If
            End If
        End If
        _liteRightMouseWasDown = rightDown
    End Sub

    Private Shared Function GetLiteAutoPotTriggerPercent(barRegion As RectRegion, pointX As Integer) As Integer
        If barRegion Is Nothing OrElse pointX < 0 OrElse barRegion.W <= 0 Then
            Return 0
        End If

        Dim relativeX As Integer = Math.Max(0, Math.Min(barRegion.W, pointX - barRegion.X))
        If relativeX <= 0 Then
            Return 1
        End If

        Dim pct As Integer = CInt(Math.Round((relativeX / CDbl(Math.Max(1, barRegion.W))) * 100.0, MidpointRounding.AwayFromZero))
        Return Math.Min(99, Math.Max(1, pct))
    End Function

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
        If sender Is btnLitePartyAutoAccept Then
            _litePartyAutoAccept = Not _litePartyAutoAccept
            UpdateLitePromptAutoAcceptButton()
        Else
            _partyAutoAccept = Not _partyAutoAccept
            UpdatePromptAutoAcceptButton()
        End If
        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
        AppendLog(If(If(sender Is btnLitePartyAutoAccept, _litePartyAutoAccept, _partyAutoAccept), "Party/resurrection auto-accept enabled.", "Party/resurrection auto-accept disabled."))
    End Sub

    Private Sub UpdatePromptAutoAcceptButton()
        UpdatePromptAutoAcceptButtonCore(btnPartyAutoAccept, _partyAutoAccept)
    End Sub

    Private Sub UpdateLitePromptAutoAcceptButton()
        UpdatePromptAutoAcceptButtonCore(btnLitePartyAutoAccept, _litePartyAutoAccept)
    End Sub

    Private Shared Sub UpdatePromptAutoAcceptButtonCore(target As Button, isEnabled As Boolean)
        If target Is Nothing Then
            Return
        End If
        target.Text = If(isEnabled, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF")
        target.BackColor = If(isEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
    End Sub

    Private Sub TogglePartyAskClicked(sender As Object, e As EventArgs)
        If sender Is btnLitePartyAsk Then
            _litePartyAskEnabled = Not _litePartyAskEnabled
            UpdateLitePartyAskButton()
        Else
            _partyAskEnabled = Not _partyAskEnabled
            UpdatePartyAskButton()
        End If
        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Sub ToggleLootScannerClicked(sender As Object, e As EventArgs)
        _lootScannerEnabled = Not _lootScannerEnabled
        If btnLootScanner IsNot Nothing Then
            btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
            btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        End If
        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Sub UpdatePartyAskButton()
        UpdatePartyAskButtonCore(btnPartyAsk, _partyAskEnabled, GetPartyAskCommandText())
    End Sub

    Private Sub UpdateLitePartyAskButton()
        UpdatePartyAskButtonCore(btnLitePartyAsk, _litePartyAskEnabled, GetLitePartyAskCommandText())
    End Sub

    Private Shared Sub UpdatePartyAskButtonCore(target As Button, isEnabled As Boolean, commandText As String)
        If target Is Nothing Then
            Return
        End If
        Dim commandLabel As String = If(commandText, "").Trim()
        If commandLabel = "" Then
            commandLabel = DefaultPartyAskCommand
        End If
        If commandLabel.Length > 14 Then
            commandLabel = commandLabel.Substring(0, 11) & "..."
        End If
        target.Text = If(isEnabled, $"Auto Ask Party ({commandLabel}): ON", $"Auto Ask Party ({commandLabel}): OFF")
        target.BackColor = If(isEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
    End Sub

    Private Sub PartyAskTextChanged(sender As Object, _e As EventArgs)
        If sender Is txtLitePartyAskText Then
            UpdateLitePartyAskButton()
        Else
            UpdatePartyAskButton()
        End If
    End Sub

    Private Function GetPartyAskCommandText() As String
        Dim rawText As String = If(txtPartyAskText IsNot Nothing, txtPartyAskText.Text, DefaultPartyAskCommand)
        Return NormalizePartyAskUiText(rawText)
    End Function

    Private Function GetLitePartyAskCommandText() As String
        Dim rawText As String = If(txtLitePartyAskText IsNot Nothing, txtLitePartyAskText.Text, DefaultPartyAskCommand)
        Return NormalizePartyAskUiText(rawText)
    End Function

    Private Shared Function NormalizePartyAskUiText(rawText As String) As String
        rawText = If(rawText, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        If rawText = "" Then
            Return DefaultPartyAskCommand
        End If
        Return rawText
    End Function

    Private Sub HelpClicked(sender As Object, e As EventArgs)
        Dim helpScope As String = ResolveHelpScope(sender)
        Dim helpForm As New Form() With {
            .Text = GetHelpWindowTitle(helpScope),
            .StartPosition = FormStartPosition.CenterParent,
            .Width = 980,
            .Height = 760,
            .MinimizeBox = False,
            .MaximizeBox = True,
            .BackColor = Color.FromArgb(20, 20, 20),
            .ForeColor = Color.Gainsboro
        }

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        tabs.TabPages.Add(CreateHelpTabPage("English", BuildScopedHelpTextEnglish(helpScope)))
        tabs.TabPages.Add(CreateHelpTabPage("Espanol", BuildScopedHelpTextSpanish(helpScope)))
        tabs.TabPages.Add(CreateHelpTabPage("Filipino", BuildScopedHelpTextFilipino(helpScope)))
        helpForm.Controls.Add(tabs)

        helpForm.ShowDialog(Me)
    End Sub

    Private Function ResolveHelpScope(sender As Object) As String
        Dim control As Control = TryCast(sender, Control)
        Dim requestedScope As String = NormalizeHelpScope(If(If(control IsNot Nothing, control.AccessibleDescription, Nothing), ""))
        If requestedScope <> HelpScopeAll Then
            Return requestedScope
        End If

        If _mainTabs IsNot Nothing AndAlso _mainTabs.SelectedTab IsNot Nothing Then
            If _mainTabs.SelectedTab Is _liteTab Then
                Return HelpScopeLite
            End If
            If _mainTabs.SelectedTab Is _combatTab Then
                Return HelpScopeCombat
            End If
            If _mainTabs.SelectedTab Is _visionTab Then
                Return HelpScopeVision
            End If
            If _mainTabs.SelectedTab Is _autoPotTab Then
                Return HelpScopeAutoPot
            End If
            If _mainTabs.SelectedTab Is _autoLootTab Then
                Return HelpScopeAutoLoot
            End If
            If _mainTabs.SelectedTab Is _levelingTab Then
                Return HelpScopeLeveling
            End If
            If _mainTabs.SelectedTab Is _diagnosticsTab Then
                Return HelpScopeDiagnostics
            End If
        End If

        Return HelpScopeCombat
    End Function

    Private Shared Function NormalizeHelpScope(rawScope As String) As String
        Select Case If(rawScope, "").Trim().ToLowerInvariant()
            Case HelpScopeLite
                Return HelpScopeLite
            Case HelpScopeCombat
                Return HelpScopeCombat
            Case HelpScopeVision
                Return HelpScopeVision
            Case HelpScopeAutoPot
                Return HelpScopeAutoPot
            Case HelpScopeAutoLoot
                Return HelpScopeAutoLoot
            Case HelpScopeLeveling
                Return HelpScopeLeveling
            Case HelpScopeDiagnostics
                Return HelpScopeDiagnostics
            Case Else
                Return HelpScopeAll
        End Select
    End Function

    Private Shared Function GetHelpWindowTitle(helpScope As String) As String
        Select Case NormalizeHelpScope(helpScope)
            Case HelpScopeLite
                Return "KathanaBot Explanation - Lite"
            Case HelpScopeVision
                Return "KathanaBot Explanation - Vision"
            Case HelpScopeAutoPot
                Return "KathanaBot Explanation - Auto-Pot"
            Case HelpScopeAutoLoot
                Return "KathanaBot Explanation - Auto-Loot"
            Case HelpScopeLeveling
                Return "KathanaBot Explanation - Leveling"
            Case HelpScopeDiagnostics
                Return "KathanaBot Explanation - Diagnostics"
            Case Else
                Return "KathanaBot Explanation - Combat Full"
        End Select
    End Function

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
            "- Full mode opens on Combat Full by default. Lite and Full cannot run at the same time.",
            "- Lite attack mode timers and Lite skill timers now allow 1 to 9999 seconds.",
            "",
            "2) COMBAT FULL TAB - COMBAT SKILLS GRID",
            "- Enabled: if checked, action is available.",
            "- Key: keyboard key sent to game (1-0, F1-F10 plus 3 custom rows after F10).",
            "- CooldownSec: minimum seconds between sends of this key.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, repair, stop.",
            "- Priority: lower values act first inside same category checks.",
            "- TriggerPercent: role threshold (heal/mana/max_health use this heavily).",
            "- MinHpPercent / MinMpPercent: minimum self HP/MP to allow this action.",
            "- high_max_hp only fires when enabled in Vision and mob_hp_rect OCR reads Max HP above your threshold.",
            "- Avoid mobs over max HP uses the same mob_hp_rect OCR, but retargets instead of attacking when Max HP is over your avoid threshold.",
            "- repair watches unreachable_text_rect for '___ is about to break'. After 5 OCR reads it sends the configured key once, then waits until the warning clears before it can trigger again. TriggerPercent is ignored for repair.",
            "",
            "3) COMBAT FULL TAB - MONSTER FILTER",
            "- Enable Monster Filter (blacklist): active deny list for mob names.",
            "- Add / Remove: manage blocked mob names.",
            "- OCR + confirmation logic avoids stale or wrong-name attacks.",
            "",
            "4) AUTO-LOOT TAB",
            "- Loot pickup toggle and interval seconds.",
            "- Add / Remove loot names to allow-list.",
            "- Loot Name Match % (Auto-Loot tab) sets fuzzy OCR matching threshold for loot names (default 80%).",
            "- Dynamic loot pickup clicks near the matched loot label using OCR plus your X/Y click offsets.",
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
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect, party_list_rect.",
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
            "- Notification Provider selects ntfy or Discord webhook delivery.",
            "- Discord has separate Global, Items, and Stats webhook fields.",
            "- ntfy Channel fields are used only when provider is set to ntfy.",
            "- Apply To Heal/Mana/Max-HP Rows applies quick thresholds to matching roles.",
            "- Test Alarm + Notify tests sound and the selected notification provider.",
            "- Test Notification sends only the selected notification test.",
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
            "- Death alert: plays sound, sends a notification, then stops bot to avoid repeats.",
            "- Window missing/crash alert: sends a separate notification when game window",
            "  is not found while running (one-shot latch until recovery).",
            "",
            "16) ENGINE AUTOMATION BEHAVIORS",
            "- Auto retarget when no valid target.",
            "- First-hit window logic to avoid premature retarget on fresh target.",
            "- Vision stability filter to reduce capture glitch spikes.",
            "- OCR based target name reading with confirmation.",
            "- OCR based unreachable target detection and forced retarget.",
            "- OCR based repair warning detection from unreachable_text_rect with 5-read confirmation.",
            "- Party invite / resurrection prompt OCR and auto accept click.",
            "- Party ask command automation with cooldown and in-party suppression.",
            "- Loot scan with fuzzy OCR allow-list matching (Loot Name Match %), dynamic label clicking, and reject handling by click point or fallback key.",
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
            "- If no notifications: verify the selected provider settings and internet access.",
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
            "- Full abre por defecto en Combat Full. Lite y Full no pueden correr al mismo tiempo.",
            "- Los tiempos de Lite para modos de ataque y skills ahora aceptan de 1 a 9999 segundos.",
            "",
            "2) PESTANA COMBAT FULL - TABLA COMBAT SKILLS",
            "- Enabled: activa/desactiva la accion.",
            "- Key: tecla enviada al juego.",
            "- CooldownSec: tiempo minimo entre envios de la tecla.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, repair, stop.",
            "- Priority: orden de prioridad.",
            "- TriggerPercent: umbral principal para roles de soporte.",
            "- MinHpPercent / MinMpPercent: minimos para permitir la accion.",
            "- high_max_hp solo dispara si esta activo en Vision y el OCR de mob_hp_rect lee Max HP arriba del umbral.",
            "- repair vigila unreachable_text_rect para '___ is about to break'. Despues de 5 lecturas OCR envia la tecla una vez y espera a que el aviso desaparezca antes de volver a activarse. TriggerPercent no se usa en repair.",
            "",
            "3) FILTRO DE MONSTRUOS",
            "- Enable Monster Filter (blacklist): lista negra de mobs.",
            "- Add / Remove: agrega o elimina nombres.",
            "- El OCR y confirmacion reducen ataques por nombre incorrecto.",
            "",
            "4) PESTANA AUTO-LOOT",
            "- Activar loot y definir intervalo en segundos.",
            "- Lista de nombres permitidos para recoger.",
            "- Loot Name Match % (pestana Auto-Loot) define el umbral de coincidencia OCR difusa para loot (80% por defecto).",
            "- El click dinamico de loot usa la posicion OCR del nombre del item mas tus offsets X/Y.",
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
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect, party_list_rect.",
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
            "- Notification Provider elige entre ntfy o Discord webhook.",
            "- Discord tiene webhooks separados para Global, Items y Stats.",
            "- Los campos ntfy solo se usan cuando el proveedor es ntfy.",
            "- Apply To Heal/Mana/Max-HP Rows aplica umbrales rapidos.",
            "- Test Alarm + Notify y Test Notification para pruebas.",
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
            "- Reproduce alarma, envia una notificacion y detiene bot para evitar repeticion.",
            "- Alerta separada cuando no se encuentra ventana del juego (posible crash).",
            "",
            "16) AUTOMATIZACION DEL MOTOR",
            "- Retarget automatico sin objetivo valido.",
            "- Logica de primera accion para evitar retarget prematuro.",
            "- Filtro de estabilidad de vision contra capturas defectuosas.",
            "- OCR para nombre de mob y confirmacion.",
            "- OCR para objetivo inalcanzable y retarget forzado.",
            "- OCR para aviso de repair en unreachable_text_rect con confirmacion de 5 lecturas.",
            "- OCR para party/ress y click de auto-aceptar.",
            "- Auto comando add con cooldown y pausa si ya esta en party.",
            "- Escaneo de loot con coincidencia OCR difusa configurable (Loot Name Match %), click dinamico sobre la etiqueta detectada y rechazo por click o tecla.",
            "- Solo usa la captura del Vision loop; no guarda screenshots automaticos extra.",
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
            "- Sin notificaciones: revisa el proveedor seleccionado, webhook/canal y el internet.",
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
            "- Default na bukas ang Full sa Combat Full tab. Hindi puwedeng sabay tumakbo ang Lite at Full.",
            "- Ang Lite timers para sa attack modes at skills ay puwede na mula 1 hanggang 9999 seconds.",
            "",
            "2) COMBAT FULL TAB - COMBAT SKILLS TABLE",
            "- Enabled: naka-on o naka-off ang action.",
            "- Key: key na ipapadala sa game.",
            "- CooldownSec: minimum na pagitan bago ulitin ang key.",
            "- Role: attack, heal, max_health, mana, special, high_max_hp, repair, stop.",
            "- Priority: pagkakasunod ng aksyon.",
            "- TriggerPercent: pangunahing threshold ng support actions.",
            "- MinHpPercent / MinMpPercent: minimum HP/MP para payagan ang action.",
            "- high_max_hp gagana lang kapag naka-enable sa Vision at nabasa ng mob_hp_rect OCR ang Max HP lampas sa threshold mo.",
            "- repair nagbabantay sa unreachable_text_rect para sa '___ is about to break'. Pag nabasa ito ng OCR ng 5 beses, isang beses nitong ipapadala ang repair key at maghihintay munang mawala ang warning bago puwedeng mag-trigger ulit. Hindi ginagamit ang TriggerPercent sa repair.",
            "",
            "3) MONSTER FILTER",
            "- Enable Monster Filter (blacklist): listahan ng bawal at i-skip na mobs.",
            "- Add / Remove: dagdag o tanggal ng pangalan.",
            "- May OCR confirm para iwas maling target dahil sa stale text.",
            "",
            "4) AUTO-LOOT TAB",
            "- Toggle ng loot pickup at interval in seconds.",
            "- Allowed loot names list.",
            "- Loot Name Match % (Auto-Loot tab) sets fuzzy OCR match threshold for loot names (default 80%).",
            "- Ang dynamic loot click ay gumagamit ng OCR position ng matched loot label kasama ang X/Y offsets mo.",
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
            "  prana_exp_rect, rupiahs_rect, party_invite_scan_rect, party_invite_ok_rect, party_list_rect.",
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
            "- Notification Provider pumipili sa ntfy o Discord webhook.",
            "- May hiwalay na Discord webhooks para sa Global, Items, at Stats.",
            "- Ang ntfy fields ay ginagamit lang kapag ntfy ang provider.",
            "- Apply To Heal/Mana/Max-HP Rows para sa mabilis na threshold apply.",
            "- Test Alarm + Notify at Test Notification para sa testing.",
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
            "- Magpapatunog, magpapadala ng notification, at hihinto ang bot para iwas repeat.",
            "- Hiwalay na crash/window-missing alert kapag hindi makita ang game window.",
            "",
            "16) ENGINE AUTOMATION",
            "- Auto retarget kapag invalid o walang target.",
            "- First-hit window para hindi agad mali ang retarget timing.",
            "- Vision stability filter laban sa capture glitches.",
            "- OCR para sa mob name + confirmation logic.",
            "- OCR para sa unreachable text at forced retarget.",
            "- OCR para sa repair warning sa unreachable_text_rect na may 5-read confirmation.",
            "- OCR party/ress detection at auto accept click.",
            "- Auto add party command na may cooldown at suppression kapag nasa party na.",
            "- Loot scan with configurable fuzzy OCR allow-list matching (Loot Name Match %), dynamic label clicking, at reject handling (click point/fallback key).",
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
            "- Walang notification: i-check ang napiling provider, webhook/channel, at internet.",
            "- Rename fail: may apps na hindi pumapayag sa window title change."
        })
    End Function

    Private Shared Function BuildScopedHelpTextEnglish(helpScope As String) As String
        Select Case NormalizeHelpScope(helpScope)
            Case HelpScopeLite
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - LITE TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Lite is the simpler mode intended for slower computers. It uses the selected game window plus HP and MP sample points.",
                    "- Update refreshes the process list. Always select the correct Tantra window before starting Lite.",
                    "- Rename Process and Apply are optional. They only try to rename the selected window title.",
                    "- Save stores the Lite preset. Load restores the saved Lite settings.",
                    "- Basic Attack (E), Mage (R), and Pick (F) each use: checkbox = enabled, number box = seconds between sends.",
                    "- Primary Skills are keys 1 to 8. Secondary Skills are F1 to F10. Each tile works the same way: checkbox enables the key and the number sets the cooldown in seconds.",
                    "- Lite timers now allow 1 to 9999 seconds.",
                    "- Start begins Lite using the selected process and the enabled Lite actions.",
                    "- Stop ends the Lite loop.",
                    "- Status shows whether Lite is ready or running. Lite Active tells you whether Lite is the active engine.",
                    "- HP% and MP% are read from the Lite sample points, not from the Full OCR rectangles.",
                    "- Enable AutoPots turns on the Lite HP and MP color check.",
                    "- Select HP Level or Select Mana Level starts sampling. Take the sample when the bar is full, then right-click the exact point where you want the potion to trigger.",
                    "- HP X/Y and MP X/Y show the saved trigger points.",
                    "- Lite potion keys are fixed: 9 for Heal and 0 for Mana.",
                    "- Auto Accept Party/Ress accepts detected party or resurrection prompts automatically.",
                    "- Ask every (sec) sets the repeat delay for the custom party command.",
                    "- Message text is the exact command Lite will type.",
                    "- Auto Ask Party turns that repeating chat command on or off.",
                    "- Recommended setup order: select the window, enable only the Lite actions you need, sample HP/MP carefully, then save the preset."
                })
            Case HelpScopeVision
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - VISION TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Window Title is the name used to find the game client.",
                    "- Loop (ms) sets the main scan and action speed.",
                    "- Normal Retarget (ms) and Forced Retarget (ms) tune when the bot sends E again.",
                    "- Mob HP Presence % is the minimum HP-bar signal required to trust the current target.",
                    "- Show Overlay opens the live calibration overlay.",
                    "- Capture Snapshot stores the current client image for region checking.",
                    "- Use special key on high max HP mobs plus Max HP >= work together with the high_max_hp combat role.",
                    "- Avoid mobs over max HP plus Avoid Max HP >= skips targets above that detected Max HP and retargets.",
                    "- Chat translation settings control OCR of the chat box, overlay visibility, target language, scan speed, and number of visible translated lines.",
                    "- Calibration Regions is the editable rectangle list for HP, MP, target name, target HP, map coordinates, chat, and other OCR areas; each On checkbox controls that region's overlay.",
                    "- Map coordinate OCR is split into map_coordinate_x_rect for the 3-digit X axis and map_coordinate_y_rect for the 3-digit Y axis.",
                    "- Loot Scan Area is the 4-point polygon used by Auto-Loot scanning.",
                    "- Process List is where you refresh windows, select one, and optionally rename it.",
                    "- Snapshot helps verify whether your rectangles actually cover the right UI areas."
                })
            Case HelpScopeAutoPot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - AUTO-POT TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Heal Trigger % and Mana Trigger % are quick values for heal and mana rows.",
                    "- HP=0 Alarm Volume % only changes the death alarm volume.",
                    "- Apply To Heal/Mana/Max-HP Rows copies those quick values into matching combat rows.",
                    "- Notification Provider chooses ntfy or Discord.",
                    "- Discord Webhook fields are separate endpoints for Global, Items, and Stats alerts.",
                    "- ntfy Channel fields are separate topics for the same three alert groups.",
                    "- Stats Interval (min) controls how often the running bot sends stat summaries.",
                    "- Test Alarm + Notify tests both sound and the selected provider.",
                    "- Test Notification sends only the notification test.",
                    "- In Unstuck / Retarget, Search Retarget Delay mirrors the normal retarget delay, Stuck Detection Delay is the minimum time before a target can be considered stuck, and No-Progress Delay is how long HP can stay unchanged before retarget is allowed."
                })
            Case HelpScopeAutoLoot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - AUTO-LOOT TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Enable Loot Pickup (F) turns on timed F pickup.",
                    "- Every (sec) sets that basic pickup interval.",
                    "- The loot name list is an allow-list. Add names you want and remove names you do not want.",
                    "- Loot Name Match % is the fuzzy OCR threshold used to decide whether text matches an allowed loot name.",
                    "- Loot Scan Area comes from the Vision tab.",
                    "- Loot Scanner (Alt) toggles the OCR-based loot scanner.",
                    "- Enable pickup by matched loot name turns on the dynamic label click flow.",
                    "- Click Offset X and Y move the click relative to the matched loot label.",
                    "- Wait Before F, Mouse Hold, Press F Count, and F Gap tune the pickup sequence after the click.",
                    "- Restore mouse cursor after click returns the mouse to the previous position."
                })
            Case HelpScopeLeveling
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - LEVELING TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Enable leveling agent turns on the target preference and travel logic.",
                    "- Preferred Mobs is a comma-separated allow-list.",
                    "- Stop HP %, Stop MP %, and Max No Target (sec) each have an On toggle; turn one off to ignore that guardrail.",
                    "- Enable map localization plus Map Open Key are required for map-based travel.",
                    "- Coordinates are route nodes: X axis is 3 digits, Y axis is 3 digits, and the bot only updates leveling travel when both are read together as X/Y.",
                    "- Min Confidence % is the localization confidence required before adding a live breadcrumb; lower values record more coordinates but can save OCR mistakes.",
                    "- Node Spacing is the minimum coordinate distance between saved route nodes; lower values save more route nodes from the breadcrumbs.",
                    "- Manual X/Y and Add Node append hand-entered 3-digit coordinates to Breadcrumbs; Save Route turns every valid table row into a route node.",
                    "- Map Marker is the current position derived from trusted X/Y coordinates. It says not available until the bot has a coordinate read it trusts.",
                    "- Enable travel preview shows route planning. Enable travel execution allows guarded movement.",
                    "- While traveling with no target, the bot sends the retarget key as a mob scan using the Vision tab Normal Retarget (ms) interval.",
                    "- Preferred Mobs accepts correctly spelled partial names, so a substring such as vasha can match Vashabum.",
                    "- Start Recording, Stop Recording, Sample Interval, Route Name, and Save Route are for route capture.",
                    "- Recorded Routes, Replay, Delete, Route Nodes, and Delete Node manage saved routes.",
                    "- Waypoint Radius, Move Burst, Re-sample, Stall Timeout, and Repath when travel stalls tune travel behavior.",
                    "- Stop when EXP/hr below threshold and Stop after repeated unreachable are extra safety exits.",
                    "- Agent Runtime and Breadcrumbs on the right show live map state and recorded coordinates."
                })
            Case HelpScopeDiagnostics
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - DIAGNOSTICS TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Diagnostics is a live read-only text panel.",
                    "- It shows internal runtime state, OCR values, target state, alerts, and recent engine decisions.",
                    "- Use it together with Snapshot and Log when the bot behaves differently from your expectations."
                })
            Case Else
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - COMBAT FULL TAB EXPLANATION (ENGLISH)",
                    "============================================================",
                    "",
                    "- Combat Full is the complete mode intended for more powerful computers. This tab controls the combat rotation, safety actions, monster filtering, start/stop flow, and live full-mode status.",
                    "- In the Combat Skills grid: Enabled decides whether the row can run, Key is the game key, CooldownSec is the minimum delay between sends, Role defines when the row is considered, Priority orders rows inside the same category, TriggerPercent is the main threshold, and MinHpPercent / MinMpPercent are self-safety gates.",
                    "- Role meanings: attack = normal damage, heal = healing action, max_health = HP support threshold, mana = MP support threshold, special = extra combat skill, high_max_hp = special branch for high-Max-HP targets, repair = one-shot repair key when the OCR warning is confirmed, stop = stop-movement key burst.",
                    "- Use lower Priority numbers for more important rows within the same role group.",
                    "- high_max_hp only works well if Vision reads the mob_hp_rect numbers correctly.",
                    "- repair only works when unreachable_text_rect is calibrated to detect the 'is about to break' warning.",
                    "- Monster Filter is a blacklist. If enabled, the engine rejects targets whose OCR name matches the blocked list.",
                    "- Add lets you insert names, including comma-separated names typed in the box. Remove deletes the selected names.",
                    "- In the center panel, Attack starts Full mode, Save Settings stores the live config, Stop Bot sends the hard-stop flow, Ignore Skill Min HP/MP bypasses row minimums, Auto Retarget If Stuck enables stuck-target recovery, and Retarget Now (E) forces a manual retarget.",
                    "- Auto Accept Party/Ress toggles OCR-based prompt acceptance.",
                    "- Ask Party Every (sec), Auto Ask Party Text, and Auto Ask Party control the repeating party command.",
                    "- The live labels show run state, status, HP, MP, current mob, EXP rate, and rupiahs rate.",
                    "- In the Log panel, Real-time shows engine events, Clear Log clears visible text only, Key Summary tracks rolling key counts, and Reset Key Summary clears that history.",
                    "- Recommended setup order: calibrate Vision first, then build the combat rows, then tune Auto-Pot and Auto-Loot support behavior."
                })
        End Select
    End Function

    Private Shared Function BuildScopedHelpTextSpanish(helpScope As String) As String
        Select Case NormalizeHelpScope(helpScope)
            Case HelpScopeLite
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA LITE (ESPANOL)",
                    "============================================================",
                    "",
                    "- Lite es el modo simple pensado para computadoras mas lentas. Usa la ventana seleccionada y puntos de muestra de HP y MP.",
                    "- Update refresca la lista de procesos. Selecciona primero la ventana correcta de Tantra.",
                    "- Rename Process + Apply solo intenta cambiar el titulo de la ventana seleccionada. Es opcional.",
                    "- Save guarda el preset Lite. Load recupera la configuracion Lite guardada.",
                    "- Basic Attack (E), Mage (R) y Pick (F) usan la misma logica: casilla = activado, numero = segundos entre usos.",
                    "- Primary Skills cubre 1 a 8. Secondary Skills cubre F1 a F10. En cada cuadro, la casilla activa la tecla y el numero define el cooldown en segundos.",
                    "- Los timers Lite aceptan de 1 a 9999 segundos.",
                    "- Start inicia Lite usando el proceso seleccionado y las acciones Lite activadas.",
                    "- Stop detiene el loop Lite.",
                    "- Status muestra si Lite esta listo o corriendo. Lite Active indica si Lite es el motor activo.",
                    "- HP% y MP% salen de los puntos de muestra Lite, no de los rectangulos OCR del modo Full.",
                    "- Enable AutoPots activa el control de color de HP y MP.",
                    "- Select HP Level y Select Mana Level inician la captura. Toma la muestra con la barra llena y luego haz clic derecho en el punto exacto donde quieres activar la pocion.",
                    "- HP X/Y y MP X/Y muestran los puntos guardados.",
                    "- En Lite las teclas de pocion son fijas: 9 para Heal y 0 para Mana.",
                    "- Auto Accept Party/Ress acepta automaticamente prompts de party o ress detectados por OCR.",
                    "- Ask every (sec) define el intervalo del comando repetido.",
                    "- Message text es el texto exacto que Lite escribira.",
                    "- Auto Ask Party activa o desactiva ese comando repetido."
                })
            Case HelpScopeVision
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA VISION (ESPANOL)",
                    "============================================================",
                    "",
                    "- Window Title es el nombre usado para encontrar el cliente del juego.",
                    "- Loop (ms) define la velocidad del ciclo principal.",
                    "- Normal Retarget (ms) y Forced Retarget (ms) ajustan cuando el bot vuelve a usar E.",
                    "- Mob HP Presence % es la senal minima para confiar en la barra de HP del objetivo.",
                    "- Show Overlay abre la capa de calibracion.",
                    "- Capture Snapshot captura la imagen actual del cliente.",
                    "- Use special key on high max HP mobs junto con Max HP >= trabaja con el role high_max_hp.",
                    "- Los controles de chat translation manejan OCR del chat, overlay, idioma destino, velocidad y numero de lineas.",
                    "- Calibration Regions contiene los rectangulos OCR editables, incluidas las coordenadas del mapa; cada checkbox On controla el overlay de esa region.",
                    "- Las coordenadas del mapa se dividen en map_coordinate_x_rect para el eje X de 3 digitos y map_coordinate_y_rect para el eje Y de 3 digitos.",
                    "- Loot Scan Area es el poligono de 4 puntos usado por Auto-Loot.",
                    "- Process List sirve para refrescar, seleccionar y renombrar ventanas.",
                    "- Snapshot ayuda a verificar si las regiones cubren bien la UI."
                })
            Case HelpScopeAutoPot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA AUTO-POT (ESPANOL)",
                    "============================================================",
                    "",
                    "- Heal Trigger % y Mana Trigger % son valores rapidos para filas heal y mana.",
                    "- HP=0 Alarm Volume % solo cambia el volumen de la alarma de muerte.",
                    "- Apply To Heal/Mana/Max-HP Rows copia esos valores a las filas compatibles.",
                    "- Notification Provider elige entre ntfy y Discord.",
                    "- Los campos Discord Webhook separan alertas Global, Items y Stats.",
                    "- Los campos ntfy Channel hacen lo mismo mediante topics.",
                    "- Stats Interval (min) controla cada cuanto se envian estadisticas mientras el bot corre.",
                    "- Test Alarm + Notify prueba sonido y notificacion.",
                    "- Test Notification prueba solo la notificacion.",
                    "- En Unstuck / Retarget, Search Retarget Delay replica el retarget normal, Stuck Detection Delay marca cuando un objetivo puede considerarse atascado y No-Progress Delay es el tiempo sin cambio de HP antes de permitir retarget."
                })
            Case HelpScopeAutoLoot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA AUTO-LOOT (ESPANOL)",
                    "============================================================",
                    "",
                    "- Enable Loot Pickup (F) activa el pickup temporizado con F.",
                    "- Every (sec) define ese intervalo basico.",
                    "- La lista de nombres de loot es una allow-list.",
                    "- Loot Name Match % es el umbral de OCR difuso para aceptar coincidencias.",
                    "- Loot Scan Area se configura en Vision.",
                    "- Loot Scanner (Alt) activa el scanner OCR.",
                    "- Enable pickup by matched loot name activa el click dinamico sobre la etiqueta detectada.",
                    "- Click Offset X/Y mueve el punto de click.",
                    "- Wait Before F, Mouse Hold, Press F Count y F Gap ajustan la secuencia de pickup.",
                    "- Restore mouse cursor after click devuelve el mouse a su posicion anterior."
                })
            Case HelpScopeLeveling
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA LEVELING (ESPANOL)",
                    "============================================================",
                    "",
                    "- Enable leveling agent activa la logica de mobs preferidos y viaje.",
                    "- Preferred Mobs es una allow-list separada por comas.",
                    "- Stop HP %, Stop MP % y Max No Target (sec) tienen un toggle On; apaga uno para ignorar esa parada.",
                    "- Enable map localization y Map Open Key son necesarios para viajar con mapa.",
                    "- Las coordenadas son nodos de ruta: eje X de 3 digitos, eje Y de 3 digitos, y el bot actualiza el viaje solo cuando lee ambos juntos como X/Y.",
                    "- Min Confidence % es la confianza minima para agregar un breadcrumb; bajarlo graba mas coordenadas pero puede guardar errores de OCR.",
                    "- Node Spacing es la distancia minima entre nodos guardados; bajarlo guarda mas nodos desde los breadcrumbs.",
                    "- Manual X/Y y Add Node agregan coordenadas de 3 digitos a Breadcrumbs; Save Route convierte cada fila valida en un nodo.",
                    "- Map Marker es la posicion actual derivada de coordenadas X/Y confiables. Sale not available hasta que haya una lectura confiable.",
                    "- Enable travel preview muestra la ruta. Enable travel execution permite movimiento protegido.",
                    "- Mientras viaja sin objetivo, el bot usa la tecla de retarget como escaneo de mobs segun Normal Retarget (ms) en Vision.",
                    "- Preferred Mobs acepta nombres parciales bien escritos; por ejemplo vasha puede coincidir con Vashabum.",
                    "- Start Recording, Stop Recording, Sample Interval, Route Name y Save Route sirven para grabar rutas.",
                    "- Recorded Routes, Replay, Delete, Route Nodes y Delete Node administran rutas guardadas.",
                    "- Waypoint Radius, Move Burst, Re-sample, Stall Timeout y Repath ajustan el movimiento.",
                    "- Agent Runtime y Breadcrumbs muestran estado y coordenadas en vivo."
                })
            Case HelpScopeDiagnostics
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA DIAGNOSTICS (ESPANOL)",
                    "============================================================",
                    "",
                    "- Diagnostics es un panel de texto en vivo y solo lectura.",
                    "- Muestra estado interno, OCR, objetivo, alertas y decisiones recientes del motor.",
                    "- Usalo junto con Snapshot y Log cuando el comportamiento del bot no coincide con lo esperado."
                })
            Case Else
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - EXPLICACION DE LA PESTANA COMBAT FULL (ESPANOL)",
                    "============================================================",
                    "",
                    "- Combat Full es el modo completo pensado para computadoras mas potentes. Esta pestana controla la rotacion de combate, acciones de seguridad, filtro de monstruos, inicio/parada y estado en vivo.",
                    "- En Combat Skills: Enabled decide si la fila puede correr, Key es la tecla del juego, CooldownSec es el tiempo minimo entre envios, Role define cuando se considera la fila, Priority ordena filas del mismo grupo, TriggerPercent es el umbral principal y MinHpPercent / MinMpPercent son filtros de seguridad propios.",
                    "- Roles: attack = dano normal, heal = curacion, max_health = soporte de HP, mana = soporte de MP, special = skill ofensiva extra, high_max_hp = rama especial para mobs con mucho Max HP, repair = repair por warning OCR, stop = tecla de parada.",
                    "- Usa prioridades numericamente mas bajas para filas mas importantes dentro del mismo tipo.",
                    "- high_max_hp depende de una lectura correcta de mob_hp_rect en Vision.",
                    "- repair depende de que unreachable_text_rect detecte bien el warning de equipo.",
                    "- Monster Filter funciona como blacklist. Si esta activo, el motor rechaza nombres de mobs que coincidan con la lista.",
                    "- Add agrega nombres, incluso varios separados por coma. Remove elimina los seleccionados.",
                    "- En el panel central, Attack inicia Full, Save Settings guarda la configuracion en vivo, Stop Bot ejecuta hard-stop, Ignore Skill Min HP/MP ignora minimos de fila, Auto Retarget If Stuck activa recuperacion por objetivo atascado y Retarget Now (E) envia retarget manual.",
                    "- Auto Accept Party/Ress maneja prompts detectados por OCR.",
                    "- Ask Party Every (sec), Auto Ask Party Text y Auto Ask Party controlan el comando repetido de party.",
                    "- En Log, Real-time muestra eventos, Clear Log limpia solo el texto visible, Key Summary guarda conteo de teclas y Reset Key Summary limpia ese historial.",
                    "- Orden recomendado: calibra Vision primero, luego arma las filas de combate y despues ajusta Auto-Pot y Auto-Loot."
                })
        End Select
    End Function

    Private Shared Function BuildScopedHelpTextFilipino(helpScope As String) As String
        Select Case NormalizeHelpScope(helpScope)
            Case HelpScopeLite
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG LITE TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Ang Lite ay mas simpleng mode na para sa mas mababagal na computer. Gumagamit ito ng selected na game window at HP/MP sample points.",
                    "- Update nagre-refresh ng process list. Piliin muna ang tamang Tantra window bago simulan ang Lite.",
                    "- Rename Process + Apply ay optional lang at susubok lang magpalit ng title ng napiling window.",
                    "- Save nagsa-save ng Lite preset. Load nagbabalik ng na-save na Lite settings.",
                    "- Basic Attack (E), Mage (R), at Pick (F) ay pare-pareho ang gamit: checkbox = enabled, number box = seconds sa pagitan ng gamit.",
                    "- Primary Skills ay 1 hanggang 8. Secondary Skills ay F1 hanggang F10. Sa bawat tile, ang checkbox ang on/off at ang numero ang cooldown in seconds.",
                    "- Ang Lite timers ay puwedeng 1 hanggang 9999 seconds.",
                    "- Start nagsisimula ng Lite gamit ang selected process at enabled Lite actions.",
                    "- Stop humihinto sa Lite loop.",
                    "- Status nagpapakita kung ready o running ang Lite. Lite Active nagsasabi kung Lite ang active engine.",
                    "- HP% at MP% ay galing sa Lite sample points, hindi sa Full OCR regions.",
                    "- Enable AutoPots binubuksan ang Lite HP/MP color check.",
                    "- Select HP Level at Select Mana Level nagsisimula ng capture. Kumuha ng sample kapag puno ang bar, tapos right-click ang eksaktong trigger point.",
                    "- HP X/Y at MP X/Y ang saved points.",
                    "- Fixed ang Lite potion keys: 9 para sa Heal at 0 para sa Mana.",
                    "- Auto Accept Party/Ress awtomatikong tumatanggap ng OCR-detected prompts.",
                    "- Ask every (sec) ang pagitan ng paulit-ulit na party command.",
                    "- Message text ang eksaktong ita-type ni Lite.",
                    "- Auto Ask Party ang on/off ng paulit-ulit na command na iyon."
                })
            Case HelpScopeVision
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG VISION TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Window Title ang pangalan na gamit para hanapin ang game client.",
                    "- Loop (ms) ang bilis ng main scan/action cycle.",
                    "- Normal Retarget (ms) at Forced Retarget (ms) ang timing kung kailan muling mag-E ang bot.",
                    "- Mob HP Presence % ang minimum signal para paniwalaan ang HP bar ng target.",
                    "- Show Overlay bubukas sa live calibration overlay.",
                    "- Capture Snapshot kukuha ng kasalukuyang image ng client.",
                    "- Use special key on high max HP mobs kasama ng Max HP >= ay para sa high_max_hp combat role.",
                    "- Ang chat translation controls ay para sa OCR ng chat, overlay visibility, target language, bilis ng scan, at dami ng visible lines.",
                    "- Calibration Regions ang editable OCR rectangles, kasama ang map coordinates; bawat On checkbox ang control ng overlay ng region.",
                    "- Hiwalay ang map coordinates: map_coordinate_x_rect para sa 3-digit X axis at map_coordinate_y_rect para sa 3-digit Y axis.",
                    "- Loot Scan Area ang 4-point polygon na gamit ng Auto-Loot.",
                    "- Process List ay para mag-refresh, pumili, at mag-rename ng windows.",
                    "- Snapshot ang pang-check kung tama ang coverage ng regions."
                })
            Case HelpScopeAutoPot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG AUTO-POT TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Heal Trigger % at Mana Trigger % ay quick values para sa heal at mana rows.",
                    "- HP=0 Alarm Volume % ay para lang sa lakas ng death alarm.",
                    "- Apply To Heal/Mana/Max-HP Rows kokopyahin ang quick values sa tugmang combat rows.",
                    "- Notification Provider pumipili sa ntfy o Discord.",
                    "- Ang Discord Webhook fields ay hiwalay para sa Global, Items, at Stats.",
                    "- Ang ntfy Channel fields ay hiwalay na topics para rin sa tatlong alert group.",
                    "- Stats Interval (min) ang pagitan ng stats summary habang tumatakbo ang bot.",
                    "- Test Alarm + Notify susubok sa tunog at provider.",
                    "- Test Notification notification lang ang tine-test.",
                    "- Sa Unstuck / Retarget, Search Retarget Delay ay katulad ng normal retarget, Stuck Detection Delay ang minimum time bago masabing stuck ang target, at No-Progress Delay ang tagal na walang HP change bago payagan ang retarget."
                })
            Case HelpScopeAutoLoot
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG AUTO-LOOT TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Enable Loot Pickup (F) nagpapapindot ng F sa takdang interval.",
                    "- Every (sec) ang basic pickup interval.",
                    "- Ang loot name list ay allow-list ng mga gusto mong pulutin.",
                    "- Loot Name Match % ang fuzzy OCR threshold para sa loot text.",
                    "- Sa Vision tine-setup ang Loot Scan Area.",
                    "- Loot Scanner (Alt) ang OCR scanner flow.",
                    "- Enable pickup by matched loot name ang dynamic label click system.",
                    "- Click Offset X/Y ina-adjust ang click point mula sa matched label.",
                    "- Wait Before F, Mouse Hold, Press F Count, at F Gap ang pickup timing controls.",
                    "- Restore mouse cursor after click ibinabalik ang dating pwesto ng mouse."
                })
            Case HelpScopeLeveling
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG LEVELING TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Enable leveling agent nagpapagana sa preferred-mob at travel logic.",
                    "- Preferred Mobs ay comma-separated allow-list.",
                    "- Stop HP %, Stop MP %, at Max No Target (sec) ay may On toggle; i-off para hindi gamitin ang guardrail na iyon.",
                    "- Enable map localization at Map Open Key ay kailangan para sa map-based travel.",
                    "- Ang coordinates ang route nodes: 3 digits sa X axis, 3 digits sa Y axis, at nag-uupdate lang ang bot kapag sabay nabasa ang X/Y.",
                    "- Min Confidence % ang minimum confidence bago magdagdag ng breadcrumb; ibaba ito para mas maraming coordinates pero mas mataas ang chance ng OCR mistake.",
                    "- Node Spacing ang minimum distance ng saved route nodes; ibaba ito para mas maraming nodes mula sa breadcrumbs.",
                    "- Manual X/Y at Add Node ay nagdadagdag ng 3-digit coordinates sa Breadcrumbs; Save Route gagawing route node ang bawat valid row.",
                    "- Map Marker ang current position galing sa trusted X/Y coordinates. Not available ito hanggat walang trusted coordinate read.",
                    "- Enable travel preview nagpapakita ng route. Enable travel execution nagpapagalaw kapag ligtas.",
                    "- Habang traveling at walang target, ginagamit ng bot ang retarget key bilang mob scan ayon sa Normal Retarget (ms) sa Vision.",
                    "- Preferred Mobs tumatanggap ng correctly spelled partial names; halimbawa vasha puwedeng tumugma sa Vashabum.",
                    "- Start Recording, Stop Recording, Sample Interval, Route Name, at Save Route ay para sa route capture.",
                    "- Recorded Routes, Replay, Delete, Route Nodes, at Delete Node ay para sa route management.",
                    "- Waypoint Radius, Move Burst, Re-sample, Stall Timeout, at Repath ang movement tuning settings.",
                    "- Agent Runtime at Breadcrumbs ang live state at recorded coordinates sa kanang side."
                })
            Case HelpScopeDiagnostics
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG DIAGNOSTICS TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Ang Diagnostics ay live at read-only na text panel.",
                    "- Ipinapakita nito ang internal state, OCR values, target state, alerts, at recent engine decisions.",
                    "- Gamitin ito kasama ng Snapshot at Log kapag hindi tugma ang kilos ng bot sa inaasahan mo."
                })
            Case Else
                Return String.Join(Environment.NewLine, New String() {
                    "KATHANABOT - PALIWANAG NG COMBAT FULL TAB (FILIPINO)",
                    "============================================================",
                    "",
                    "- Ang Combat Full ay ang kumpletong mode na para sa mas malalakas na computer. Dito kino-control ang combat rotation, safety actions, monster filtering, start/stop flow, at live full-mode status.",
                    "- Sa Combat Skills grid: Enabled ang on/off ng row, Key ang key na ipapadala sa game, CooldownSec ang minimum pagitan ng gamit, Role ang nagsasabi kung kailan susuriin ang row, Priority ang order sa loob ng parehong role group, TriggerPercent ang pangunahing threshold, at MinHpPercent / MinMpPercent ang sariling safety gates.",
                    "- Mga role: attack = normal damage, heal = heal action, max_health = HP support threshold, mana = MP support threshold, special = extra skill, high_max_hp = special branch para sa high-Max-HP mobs, repair = one-shot repair kapag confirmed ang OCR warning, stop = stop-movement key burst.",
                    "- Gumamit ng mas mababang priority number para sa mas importanteng rows sa parehong role group.",
                    "- high_max_hp ay gagana lang kung tama ang OCR reading ng mob_hp_rect sa Vision.",
                    "- repair ay nakadepende sa tamang calibration ng unreachable_text_rect warning text.",
                    "- Ang Monster Filter ay blacklist. Kapag naka-enable, iiwasan ng engine ang mga target na tugma sa blocked names.",
                    "- Add puwedeng magdagdag ng isa o maraming comma-separated names. Remove mag-aalis ng selected names.",
                    "- Sa center panel, Attack nagsisimula ng Full mode, Save Settings nagsa-save ng live config, Stop Bot naghihinto sa hard-stop flow, Ignore Skill Min HP/MP nagba-bypass sa row minimums, Auto Retarget If Stuck nag-o-on ng stuck-target recovery, at Retarget Now (E) ay manual retarget.",
                    "- Auto Accept Party/Ress ay para sa OCR-based prompt acceptance.",
                    "- Ask Party Every (sec), Auto Ask Party Text, at Auto Ask Party ay para sa paulit-ulit na party command.",
                    "- Sa Log panel, Real-time ang live events, Clear Log ang visible text lang ang nililinis, Key Summary ang rolling key counts, at Reset Key Summary ang naglilinis ng history.",
                    "- Recommended order: unahin ang Vision calibration, sunod ang combat rows, tapos i-tune ang Auto-Pot at Auto-Loot."
                })
        End Select
    End Function

    Private Sub ManualRetargetClicked(sender As Object, e As EventArgs)
        Dim title As String = txtWindowTitle.Text.Trim()
        If title = "" Then
            AppendLog("Manual retarget failed: window title is empty.")
            Return
        End If

        If _fullEngine.ManualRetarget(title) Then
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
            UpdateMainTabIndicators()
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
                UpdateMainTabIndicators()
            End Sub
        _overlayForm.Show(Me)
        btnOverlayToggle.Text = "Hide Overlay"
        UpdateMainTabIndicators()
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
        Dim st As BotStatus = GetStatusForEdition(_edition)
        HandlePendingLitePointCapture()
        If _fullEngine.IsRunning() Then
            HandlePeriodicStatsNotification(_fullStatus)
        End If
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
            $"LevelingStopHpEnabled: {If(chkLevelingStopHp Is Nothing OrElse chkLevelingStopHp.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingStopHp%: {If(nudLevelingStopHp IsNot Nothing, nudLevelingStopHp.Value.ToString(), "20")}{Environment.NewLine}" &
            $"LevelingStopMpEnabled: {If(chkLevelingStopMp Is Nothing OrElse chkLevelingStopMp.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingStopMp%: {If(nudLevelingStopMp IsNot Nothing, nudLevelingStopMp.Value.ToString(), "10")}{Environment.NewLine}" &
            $"LevelingMaxNoTargetEnabled: {If(chkLevelingMaxNoTarget Is Nothing OrElse chkLevelingMaxNoTarget.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingMaxNoTargetSec: {If(nudLevelingMaxNoTargetSeconds IsNot Nothing, nudLevelingMaxNoTargetSeconds.Value.ToString(), "45")}{Environment.NewLine}" &
            $"LevelingStopOnLowExp: {If(chkLevelingStopOnLowExp IsNot Nothing AndAlso chkLevelingStopOnLowExp.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingMinExpPerHour%: {If(nudLevelingMinExpPerHour IsNot Nothing, nudLevelingMinExpPerHour.Value.ToString("0.00"), DefaultLevelingMinExpPerHour.ToString("0.00"))}{Environment.NewLine}" &
            $"LevelingStopOnRepeatedUnreachable: {If(chkLevelingStopOnRepeatedUnreachable IsNot Nothing AndAlso chkLevelingStopOnRepeatedUnreachable.Checked, "True", "False")}{Environment.NewLine}" &
            $"LevelingUnreachableLimit: {If(nudLevelingUnreachableLimit IsNot Nothing, nudLevelingUnreachableLimit.Value.ToString(), "4")}{Environment.NewLine}" &
            $"HighMaxHpSpecial: {If(chkHighMaxHpSpecial IsNot Nothing AndAlso chkHighMaxHpSpecial.Checked, "True", "False")}{Environment.NewLine}" &
            $"HighMaxHpThreshold: {If(nudHighMaxHpThreshold IsNot Nothing, nudHighMaxHpThreshold.Value.ToString("N0"), "2000")}{Environment.NewLine}" &
            $"AvoidHighMaxHp: {If(chkAvoidHighMaxHpTargets IsNot Nothing AndAlso chkAvoidHighMaxHpTargets.Checked, "True", "False")}{Environment.NewLine}" &
            $"AvoidHighMaxHpThreshold: {If(nudAvoidHighMaxHpThreshold IsNot Nothing, nudAvoidHighMaxHpThreshold.Value.ToString("N0"), "2000")}{Environment.NewLine}" &
            $"ChatTranslationEnabled: {If(chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked, "True", "False")}{Environment.NewLine}" &
            $"ChatTranslationOverlay: {If(chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked, "True", "False")}{Environment.NewLine}" &
            $"DisabledRegionOverlays: {String.Join(", ", BuildDisabledCalibrationRegionOverlays())}{Environment.NewLine}" &
            $"ChatTargetLanguage: {GetSelectedChatTargetLanguageCode()}{Environment.NewLine}" &
            $"ChatScanMs: {If(nudChatScanMs IsNot Nothing, nudChatScanMs.Value.ToString(), "700")}{Environment.NewLine}" &
            $"ChatMaxLines: {If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value.ToString(), "6")}{Environment.NewLine}" &
            $"NavigationEnabled: {If(chkNavigationEnabled IsNot Nothing AndAlso chkNavigationEnabled.Checked, "True", "False")}{Environment.NewLine}" &
            $"MapOpenKey: {If(txtMapOpenKey IsNot Nothing AndAlso txtMapOpenKey.Text.Trim() <> "", txtMapOpenKey.Text.Trim().ToUpperInvariant(), DefaultMapOpenKey)}{Environment.NewLine}" &
            $"TravelPreviewEnabled: {If(chkTravelPreview IsNot Nothing AndAlso chkTravelPreview.Checked, "True", "False")}{Environment.NewLine}" &
            $"TravelExecutionEnabled: {If(chkTravelExecute IsNot Nothing AndAlso chkTravelExecute.Checked, "True", "False")}{Environment.NewLine}" &
            $"RouteRecordingEnabled: {If(_routeRecordingActive, "True", "False")}{Environment.NewLine}" &
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
            $"NotificationProvider: {GetNotificationProviderName()}{Environment.NewLine}" &
            $"NotificationDestination: {GetNotificationDestinationSummary()}{Environment.NewLine}" &
            $"DiscordGlobalWebhookConfigured: {If(GetDiscordGlobalWebhookUrl() <> "", "True", "False")}{Environment.NewLine}" &
            $"DiscordItemWebhookConfigured: {If(GetDiscordItemWebhookUrl() <> "", "True", "False")}{Environment.NewLine}" &
            $"DiscordStatsWebhookConfigured: {If(GetDiscordStatsWebhookUrl() <> "", "True", "False")}{Environment.NewLine}" &
            $"NtfyTopic: {GetNtfyTopicName()}{Environment.NewLine}" &
            $"ItemNtfyTopic: {If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), "")}{Environment.NewLine}" &
            $"StatsNtfyTopic: {GetStatsNtfyTopicName()}{Environment.NewLine}" &
            $"StatsNtfyIntervalMinutes: {GetStatsNotificationIntervalMinutes()}{Environment.NewLine}" &
            $"LastStatsNtfyUtc: {If(_lastStatsNotificationUtc = DateTime.MinValue, "n/a", _lastStatsNotificationUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))}{Environment.NewLine}" &
            $"LootPickupEnabled: {If(chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked, "True", "False")}{Environment.NewLine}" &
            $"LootPickupIntervalSec: {If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value.ToString(), "4")}{Environment.NewLine}" &
            $"LootNameAutoPickupEnabled: {If(chkLootNameAutoPickup IsNot Nothing AndAlso chkLootNameAutoPickup.Checked, "True", "False")}{Environment.NewLine}" &
            $"LootNamePickupPoint: {If(_lootNamePickupPointX >= 0 AndAlso _lootNamePickupPointY >= 0, _lootNamePickupPointX.ToString() & "," & _lootNamePickupPointY.ToString(), "not set")}{Environment.NewLine}" &
            $"LootNameMatchThreshold%: {If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value.ToString(), DefaultLootNameMatchThresholdPercent.ToString())}{Environment.NewLine}" &
            $"LootRejectPoint: {If(_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0, _lootRejectPointX.ToString() & "," & _lootRejectPointY.ToString(), "not set")}{Environment.NewLine}" &
            $"AlarmVolume%: {_alarmVolumePercent}{Environment.NewLine}" &
            $"HpZeroAlarm: {_hpZeroAlarmActive}{Environment.NewLine}" &
            $"HpZeroPending: {_hpZeroPending}{Environment.NewLine}" &
            $"Window Found: {st.WindowFound}{Environment.NewLine}" &
            $"HP%: {st.HpPercent:0.0}{Environment.NewLine}" &
            $"MP%: {st.MpPercent:0.0}{Environment.NewLine}" &
            $"CharacterName: {If(String.IsNullOrWhiteSpace(st.CharacterName), "n/a", st.CharacterName)}{Environment.NewLine}" &
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
            $"ChatOcrUpdatedAt: {If(st.ChatOcrUpdatedAt = DateTime.MinValue, "n/a", st.ChatOcrUpdatedAt.ToLocalTime().ToString("HH:mm:ss"))}{Environment.NewLine}" &
            $"ChatOcrText: {If(String.IsNullOrWhiteSpace(st.ChatOcrText), "n/a", st.ChatOcrText.Replace(Environment.NewLine, " | "))}{Environment.NewLine}" &
            $"MapCoordinateText: {If(String.IsNullOrWhiteSpace(st.MapCoordinateText), "n/a", st.MapCoordinateText)}{Environment.NewLine}" &
            $"MapCoordinateXY: {If(st.MapCoordinateX >= 0 AndAlso st.MapCoordinateY >= 0, st.MapCoordinateX.ToString("000") & "," & st.MapCoordinateY.ToString("000"), "n/a")}{Environment.NewLine}" &
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
        HandlePendingLitePointCapture()
    End Sub

    Private Sub HandleCtrlShiftTogglePress()
        If Not (IsGameWindowForeground() OrElse IsControlPanelForeground()) Then
            Return
        End If

        Dim runningEdition As BotEdition? = GetRunningEdition()
        If runningEdition.HasValue Then
            StopEdition(runningEdition.Value, False, "ctrl+shift toggle")
            AppendLog($"Ctrl+Shift toggle: {runningEdition.Value} bot paused.")
        Else
            Dim selectedEdition As BotEdition = If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full)
            StartEdition(selectedEdition, False)
            AppendLog($"Ctrl+Shift toggle: {selectedEdition} bot resumed.")
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

    Private Sub OnEngineStatusUpdated(edition As BotEdition, status As BotStatus)
        If InvokeRequired Then
            BeginInvoke(New Action(Of BotEdition, BotStatus)(AddressOf OnEngineStatusUpdated), edition, status)
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

        If edition = BotEdition.Lite Then
            _liteStatus = status
            UpdateLiteStatus(statusText, status)
            UpdateAttackButtonAppearance(False)
            Return
        End If

        _fullStatus = status

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
            If status.MapCoordinateX >= 0 AndAlso status.MapCoordinateY >= 0 Then
                lblMapCoordinate.Text = $"Coordinates X axis: {status.MapCoordinateX:000} | Coordinates Y axis: {status.MapCoordinateY:000} | Route node: {status.MapCoordinateX:000}/{status.MapCoordinateY:000} (confidence {status.MapCoordinateConfidence}%)"
            Else
                Dim rawCoordinateText As String = If(String.IsNullOrWhiteSpace(status.MapCoordinateText), "n/a", status.MapCoordinateText)
                lblMapCoordinate.Text = $"Coordinates X axis: n/a | Coordinates Y axis: n/a | Route node waits for both 3-digit reads ({rawCoordinateText})"
            End If
        End If
        If lblMapHeading IsNot Nothing Then
            lblMapHeading.Text = $"Map Heading: {If(String.IsNullOrWhiteSpace(status.MapHeading), "n/a", status.MapHeading)}"
        End If
        If lblMapMarker IsNot Nothing Then
            Dim markerText As String = If(status.MapMarkerDetected, $"{status.MapMarkerX}/{status.MapMarkerY} (from coordinates)", "not available (waiting for trusted X/Y coordinates)")
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
        UpdateRouteRecordingButtonStates()
        UpdateBreadcrumbsGrid(status.RouteRecordingSamples)
        If lblChatTranslationStatus IsNot Nothing Then
            Dim chatState As String
            If chkChatTranslationEnabled Is Nothing OrElse Not chkChatTranslationEnabled.Checked Then
                chatState = "Chat Translation: disabled."
                lblChatTranslationStatus.ForeColor = Color.DimGray
            ElseIf String.IsNullOrWhiteSpace(status.ChatOcrText) Then
                chatState = "Chat Translation: waiting for readable chat text in chat_rect."
                lblChatTranslationStatus.ForeColor = Color.Khaki
            Else
                Dim lineCount As Integer = status.ChatOcrText.Split({Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).Length
                chatState = $"Chat Translation: OCR captured {lineCount} line(s)."
                lblChatTranslationStatus.ForeColor = Color.LightGreen
            End If
            lblChatTranslationStatus.Text = chatState
        End If
        HandleChatTranslation(status)
        UpdateAttackButtonAppearance(False)
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

    Private Sub HandleChatTranslation(status As BotStatus)
        Dim translationEnabled As Boolean = (chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked)
        Dim overlayEnabled As Boolean = (chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked)
        If Not translationEnabled Then
            _lastChatOcrText = ""
            _chatSeenLineKeys.Clear()
            _chatSeenLineOrder.Clear()
            _chatOverlayEntries.Clear()
            HideChatTranslationOverlay()
            Return
        End If

        UpdateChatTranslationOverlayVisibility(overlayEnabled)

        Dim rawText As String = If(status.ChatOcrText, "").Trim()
        If rawText = "" OrElse rawText.Equals(_lastChatOcrText, StringComparison.Ordinal) Then
            RefreshChatTranslationOverlayContent()
            Return
        End If

        _lastChatOcrText = rawText
        Dim targetLanguage As String = GetSelectedChatTargetLanguageCode()
        Dim lines As List(Of String) = ParseChatOcrLines(rawText)
        For Each line As String In lines
            Dim key As String = NormalizeChatLineKey(line)
            If key = "" OrElse _chatSeenLineKeys.Contains(key) Then
                Continue For
            End If

            _chatSeenLineKeys.Add(key)
            _chatSeenLineOrder.Enqueue(key)
            While _chatSeenLineOrder.Count > 80
                Dim expired As String = _chatSeenLineOrder.Dequeue()
                _chatSeenLineKeys.Remove(expired)
            End While

            QueueChatTranslation(line, targetLanguage)
        Next
    End Sub

    Private Shared Function ParseChatOcrLines(rawText As String) As List(Of String)
        Dim results As New List(Of String)()
        For Each rawLine As String In If(rawText, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleaned As String = Regex.Replace(rawLine, "\s+", " ").Trim()
            If cleaned.Length < 2 Then
                Continue For
            End If
            results.Add(cleaned)
        Next
        Return results
    End Function

    Private Shared Function NormalizeChatLineKey(line As String) As String
        Return Regex.Replace(If(line, "").Trim().ToLowerInvariant(), "\s+", " ")
    End Function

    Private Shared Function NormalizeChatTargetLanguageCode(raw As String) As String
        Dim cleaned As String = Regex.Replace(If(raw, "").Trim().ToLowerInvariant(), "[^a-z]", "")
        Select Case cleaned
            Case "es", "spanish", "espanol"
                Return "es"
            Case "tl", "fil", "filipino", "tagalog", "philipino"
                Return "tl"
            Case Else
                Return "en"
        End Select
    End Function

    Private Function GetSelectedChatTargetLanguageCode() As String
        If cboChatTargetLanguage Is Nothing Then
            Return "en"
        End If

        Dim selected As ChatLanguageOption = TryCast(cboChatTargetLanguage.SelectedItem, ChatLanguageOption)
        If selected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selected.Code) Then
            Return selected.Code
        End If

        If cboChatTargetLanguage.SelectedValue IsNot Nothing Then
            Return NormalizeChatTargetLanguageCode(cboChatTargetLanguage.SelectedValue.ToString())
        End If

        Return "en"
    End Function

    Private Sub SelectChatTargetLanguage(raw As String)
        If cboChatTargetLanguage Is Nothing Then
            Return
        End If

        Dim targetCode As String = NormalizeChatTargetLanguageCode(raw)
        For i As Integer = 0 To cboChatTargetLanguage.Items.Count - 1
            Dim optionItem As ChatLanguageOption = TryCast(cboChatTargetLanguage.Items(i), ChatLanguageOption)
            If optionItem IsNot Nothing AndAlso optionItem.Code.Equals(targetCode, StringComparison.OrdinalIgnoreCase) Then
                cboChatTargetLanguage.SelectedIndex = i
                Exit Sub
            End If
        Next

        If cboChatTargetLanguage.Items.Count > 0 Then
            cboChatTargetLanguage.SelectedIndex = 0
        End If
    End Sub

    Private Async Function TranslateChatLineAsync(sourceLine As String, targetLanguage As String) As Task(Of String)
        Dim lineText As String = If(sourceLine, "")
        If String.IsNullOrWhiteSpace(lineText) Then
            Return ""
        End If

        Dim colonIndex As Integer = lineText.IndexOf(":"c)
        If colonIndex > 0 Then
            Dim prefix As String = lineText.Substring(0, colonIndex + 1)
            Dim messageText As String = lineText.Substring(colonIndex + 1)
            Dim translatedMessage As String = Await TranslateChatMessageBodyAsync(messageText, targetLanguage)
            Return prefix & translatedMessage
        End If

        Return Await TranslateChatMessageBodyAsync(lineText, targetLanguage)
    End Function

    Private Async Function TranslateChatMessageBodyAsync(messageText As String, targetLanguage As String) As Task(Of String)
        Dim rawText As String = If(messageText, "")
        If rawText = "" Then
            Return ""
        End If

        Dim leadingWhitespace As String = Regex.Match(rawText, "^\s*").Value
        Dim trailingWhitespace As String = Regex.Match(rawText, "\s*$").Value
        Dim coreText As String = rawText.Trim()
        If coreText = "" Then
            Return rawText
        End If

        Dim matches As MatchCollection = Regex.Matches(coreText, "\[[^\]]*\]|[^\[]+")
        Dim parts As New List(Of String)()
        For Each piece As Match In matches
            Dim value As String = piece.Value
            If value.StartsWith("[", StringComparison.Ordinal) AndAlso value.EndsWith("]", StringComparison.Ordinal) Then
                parts.Add(value)
            Else
                parts.Add(Await TranslateChatSegmentAsync(value, targetLanguage))
            End If
        Next

        Return leadingWhitespace & String.Concat(parts) & trailingWhitespace
    End Function

    Private Async Function TranslateChatSegmentAsync(segment As String, targetLanguage As String) As Task(Of String)
        Dim rawSegment As String = If(segment, "")
        If rawSegment = "" Then
            Return ""
        End If

        Dim leadingWhitespace As String = Regex.Match(rawSegment, "^\s*").Value
        Dim trailingWhitespace As String = Regex.Match(rawSegment, "\s*$").Value
        Dim coreText As String = rawSegment.Trim()
        If coreText = "" Then
            Return rawSegment
        End If

        Dim translated As String = Await _chatTranslator.TranslateTextAsync(coreText, targetLanguage)
        If String.IsNullOrWhiteSpace(translated) Then
            translated = coreText
        End If

        Return leadingWhitespace & translated.Trim() & trailingWhitespace
    End Function

    Private Sub QueueChatTranslation(sourceLine As String, targetLanguage As String)
        Dim lineCopy As String = If(sourceLine, "").Trim()
        If lineCopy = "" Then
            Return
        End If

        Task.Run(
            Async Function()
                Await _chatTranslationLock.WaitAsync()
                Try
                    Dim translated As String = Await TranslateChatLineAsync(lineCopy, targetLanguage)
                    If String.IsNullOrWhiteSpace(translated) Then
                        translated = lineCopy
                    End If

                    If IsDisposed Then
                        Return
                    End If

                    BeginInvoke(
                        New Action(
                            Sub()
                                AddTranslatedChatEntry(lineCopy, translated)
                            End Sub))
                Catch ex As Exception
                    If Not IsDisposed Then
                        BeginInvoke(New Action(Of String)(AddressOf AppendLogSafe), "Chat translation failed: " & ex.Message)
                    End If
                Finally
                    _chatTranslationLock.Release()
                End Try
            End Function)
    End Sub

    Private Sub AddTranslatedChatEntry(sourceText As String, translatedText As String)
        Dim entry As New ChatOverlayLine With {
            .SourceText = sourceText,
            .TranslatedText = translatedText,
            .CreatedAtUtc = DateTime.UtcNow
        }

        _chatOverlayEntries.Add(entry)
        Dim maxEntries As Integer = Math.Max(1, CInt(If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value, 6D)) * 4)
        While _chatOverlayEntries.Count > maxEntries
            _chatOverlayEntries.RemoveAt(0)
        End While

        RefreshChatTranslationOverlayContent()
    End Sub

    Private Sub UpdateChatTranslationOverlayVisibility(overlayEnabled As Boolean)
        If Not overlayEnabled Then
            HideChatTranslationOverlay()
            Return
        End If

        If _chatTranslationOverlayForm Is Nothing OrElse _chatTranslationOverlayForm.IsDisposed Then
            _chatTranslationOverlayForm = New ChatTranslationOverlayForm(Function() BuildConfig())
        End If

        RefreshChatTranslationOverlayContent()
    End Sub

    Private Sub RefreshChatTranslationOverlayContent()
        If _chatTranslationOverlayForm Is Nothing OrElse _chatTranslationOverlayForm.IsDisposed Then
            Return
        End If

        Dim maxLines As Integer = Math.Max(1, CInt(If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value, 6D)))
        Dim visibleEntries As List(Of ChatOverlayLine) = _chatOverlayEntries.
            Skip(Math.Max(0, _chatOverlayEntries.Count - maxLines)).
            Select(Function(entry) New ChatOverlayLine With {
                .SourceText = entry.SourceText,
                .TranslatedText = entry.TranslatedText,
                .CreatedAtUtc = entry.CreatedAtUtc
            }).
            ToList()

        _chatTranslationOverlayForm.UpdateContent(visibleEntries, chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked)
    End Sub

    Private Sub HideChatTranslationOverlay()
        If _chatTranslationOverlayForm Is Nothing OrElse _chatTranslationOverlayForm.IsDisposed Then
            Return
        End If

        _chatTranslationOverlayForm.UpdateContent(New List(Of ChatOverlayLine)(), False)
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

    Private Sub OnEngineLogLine(edition As BotEdition, line As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of BotEdition, String)(AddressOf OnEngineLogLine), edition, line)
            Return
        End If
        Dim prefixed As String = $"[{edition}] {line}"
        If edition = BotEdition.Full Then
            TrackKeyActionFromEngineLog(line)
        End If
        AppendLog(prefixed)
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
        AppendLog($"Testing HP=0 alarm + notification via {GetNotificationProviderName()} at {_alarmVolumePercent}% volume.")
        Task.Run(Sub() PlayAlarmPulse(_alarmVolumePercent))
        Task.Run(
            Async Function()
                Await SendPhoneNotificationAsync("KathanaBot Test", "Combined test: HP alarm sound + notification.")
            End Function)
    End Sub

    Private Sub TestPhoneAlertClicked(sender As Object, e As EventArgs)
        AppendLog($"Sending test notification via {GetNotificationDestinationSummary()}.")
        Task.Run(
            Async Function()
                Await SendPhoneNotificationAsync("KathanaBot Test", "Test notification from Auto-Pot tab.")
            End Function)
    End Sub

    Private Function BuildFullConfig() As BotConfig
        Return BuildConfig()
    End Function

    Private Function BuildLiteConfig() As BotConfig
        Dim cfg As New BotConfig()
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Lite)
        cfg.LiteModeEnabled = True
        cfg.WindowTitle = If(txtWindowTitle IsNot Nothing, txtWindowTitle.Text.Trim(), DefaultGameWindowTitle)
        cfg.SelectedWindowHandle = If(selected IsNot Nothing, selected.MainWindowHandle, IntPtr.Zero)
        cfg.LiteHpCheckPointX = _liteAutoPotHpPointX
        cfg.LiteHpCheckPointY = _liteAutoPotHpPointY
        cfg.LiteMpCheckPointX = _liteAutoPotMpPointX
        cfg.LiteMpCheckPointY = _liteAutoPotMpPointY
        cfg.LoopMs = 80
        cfg.RetargetMs = 550
        cfg.ForcedRetargetMs = 550
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.BypassHpMpLimits = True
        cfg.PartyAutoAcceptEnabled = _litePartyAutoAccept
        cfg.PartyAskEnabled = _litePartyAskEnabled
        cfg.PartyAskIntervalMs = CInt(Math.Round(CDbl(If(nudLitePartyAskSeconds IsNot Nothing, nudLitePartyAskSeconds.Value, 30D)) * 1000.0))
        cfg.PartyAskText = GetLitePartyAskCommandText()
        cfg.NotificationProvider = GetNotificationProviderName()
        cfg.DiscordWebhookUrl = GetDiscordWebhookUrl()
        cfg.DiscordGlobalWebhookUrl = GetDiscordGlobalWebhookUrl()
        cfg.DiscordItemWebhookUrl = GetDiscordItemWebhookUrl()
        cfg.DiscordStatsWebhookUrl = GetDiscordStatsWebhookUrl()
        cfg.Actions = New List(Of ActionRule)()

        For Each action As PersistedCombatAction In GetPersistedLiteActions()
            If action Is Nothing OrElse Not action.Enabled Then
                Continue For
            End If

            cfg.Actions.Add(New ActionRule With {
                .KeyName = action.ActionKey,
                .Enabled = action.Enabled,
                .Role = GetLiteDefaultRole(action.ActionKey),
                .Priority = action.Priority,
                .CooldownMs = CInt(Math.Round(Math.Max(1.0, action.CooldownSec) * 1000.0)),
                .TriggerPercent = 1,
                .MinHpPercent = 1,
                .MinMpPercent = 1
            })
        Next

        If chkLiteAutoPots IsNot Nothing AndAlso chkLiteAutoPots.Checked Then
            If _liteAutoPotHpPointX >= 0 AndAlso _liteAutoPotHpPointY >= 0 Then
                cfg.Actions.Add(New ActionRule With {
                    .KeyName = "9",
                    .Enabled = True,
                    .Role = "heal",
                    .Priority = 500,
                    .CooldownMs = 1000,
                    .TriggerPercent = 1,
                    .MinHpPercent = 1,
                    .MinMpPercent = 1
                })
            End If

            If _liteAutoPotMpPointX >= 0 AndAlso _liteAutoPotMpPointY >= 0 Then
                cfg.Actions.Add(New ActionRule With {
                    .KeyName = "0",
                    .Enabled = True,
                    .Role = "mana",
                    .Priority = 510,
                    .CooldownMs = 1000,
                    .TriggerPercent = 1,
                    .MinHpPercent = 1,
                    .MinMpPercent = 1
                })
            End If
        End If

        Return cfg
    End Function

    Private Function BuildConfig() As BotConfig
        Dim cfg As New BotConfig()
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        cfg.WindowTitle = txtWindowTitle.Text.Trim()
        cfg.SelectedWindowHandle = If(selected IsNot Nothing, selected.MainWindowHandle, IntPtr.Zero)
        cfg.LoopMs = CInt(nudLoopMs.Value)
        cfg.RetargetMs = CInt(nudRetargetMs.Value)
        cfg.ForcedRetargetMs = CInt(If(nudForcedRetargetMs IsNot Nothing, nudForcedRetargetMs.Value, nudRetargetMs.Value))
        cfg.StuckTargetMs = CInt(If(nudStuckTargetMs IsNot Nothing, nudStuckTargetMs.Value, 2200D))
        cfg.StuckTargetNoProgressRetargetMs = CInt(If(nudStuckNoProgressRetargetMs IsNot Nothing, nudStuckNoProgressRetargetMs.Value, 6000D))
        cfg.MobHpPresenceThreshold = CDbl(nudMobHpThreshold.Value)
        cfg.HighMaxHpSpecialEnabled = (chkHighMaxHpSpecial IsNot Nothing AndAlso chkHighMaxHpSpecial.Checked)
        cfg.HighMaxHpThreshold = CInt(If(nudHighMaxHpThreshold IsNot Nothing, nudHighMaxHpThreshold.Value, 2000D))
        cfg.AvoidHighMaxHpEnabled = (chkAvoidHighMaxHpTargets IsNot Nothing AndAlso chkAvoidHighMaxHpTargets.Checked)
        cfg.AvoidHighMaxHpThreshold = CInt(If(nudAvoidHighMaxHpThreshold IsNot Nothing, nudAvoidHighMaxHpThreshold.Value, 2000D))
        cfg.BypassHpMpLimits = _bypassHpMpLimits
        cfg.BypassStuckTarget = _bypassStuckTarget
        cfg.PartyAutoAcceptEnabled = _partyAutoAccept
        cfg.PartyAskEnabled = _partyAskEnabled
        cfg.PartyAskIntervalMs = CInt(Math.Round(CDbl(If(nudPartyAskSeconds IsNot Nothing, nudPartyAskSeconds.Value, 30D)) * 1000.0))
        cfg.PartyAskText = GetPartyAskCommandText()
        cfg.LootScannerEnabled = _lootScannerEnabled
        cfg.NotificationProvider = GetNotificationProviderName()
        cfg.DiscordWebhookUrl = GetDiscordWebhookUrl()
        cfg.DiscordGlobalWebhookUrl = GetDiscordGlobalWebhookUrl()
        cfg.DiscordItemWebhookUrl = GetDiscordItemWebhookUrl()
        cfg.DiscordStatsWebhookUrl = GetDiscordStatsWebhookUrl()
        cfg.ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), "")
        cfg.LevelingAgentEnabled = (chkLevelingAgent IsNot Nothing AndAlso chkLevelingAgent.Checked)
        cfg.LevelingPreferredMobs = ParseCommaSeparatedList(If(txtLevelingPreferredMobs IsNot Nothing, txtLevelingPreferredMobs.Text, ""))
        cfg.LevelingStopHpEnabled = (chkLevelingStopHp Is Nothing OrElse chkLevelingStopHp.Checked)
        cfg.LevelingStopHpPercent = CInt(If(nudLevelingStopHp IsNot Nothing, nudLevelingStopHp.Value, 20D))
        cfg.LevelingStopMpEnabled = (chkLevelingStopMp Is Nothing OrElse chkLevelingStopMp.Checked)
        cfg.LevelingStopMpPercent = CInt(If(nudLevelingStopMp IsNot Nothing, nudLevelingStopMp.Value, 10D))
        cfg.LevelingMaxNoTargetEnabled = (chkLevelingMaxNoTarget Is Nothing OrElse chkLevelingMaxNoTarget.Checked)
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
        cfg.RouteRecordingEnabled = _routeRecordingActive
        cfg.RouteRecordingName = If(txtRouteRecordingName IsNot Nothing AndAlso txtRouteRecordingName.Text.Trim() <> "", txtRouteRecordingName.Text.Trim(), "jina_route")
        cfg.RouteRecordingSampleIntervalMs = CInt(If(nudRouteRecordingIntervalMs IsNot Nothing, nudRouteRecordingIntervalMs.Value, 250D))
        cfg.RouteRecordingMinConfidencePercent = CInt(If(nudRouteRecordingMinConfidence IsNot Nothing, nudRouteRecordingMinConfidence.Value, 90D))
        cfg.RouteRecordingMinNodeSpacing = CInt(If(nudRouteRecordingNodeSpacing IsNot Nothing, nudRouteRecordingNodeSpacing.Value, 2D))
        cfg.NavigationWaypointReachRadius = CInt(If(nudNavigationWaypointRadius IsNot Nothing, nudNavigationWaypointRadius.Value, 36D))
        cfg.NavigationMoveBurstMs = CInt(If(nudNavigationMoveBurstMs IsNot Nothing, nudNavigationMoveBurstMs.Value, 350D))
        cfg.NavigationResampleIntervalMs = CInt(If(nudNavigationResampleMs IsNot Nothing, nudNavigationResampleMs.Value, 1800D))
        cfg.NavigationStallTimeoutMs = CInt(If(nudNavigationStallTimeoutMs IsNot Nothing, nudNavigationStallTimeoutMs.Value, 6500D))
        cfg.NavigationRepathOnStuck = (chkNavigationRepathOnStuck IsNot Nothing AndAlso chkNavigationRepathOnStuck.Checked)
        cfg.ChatTranslationEnabled = (chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked)
        cfg.ChatTranslationOverlayEnabled = (chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked)
        cfg.DisabledCalibrationRegionOverlays = BuildDisabledCalibrationRegionOverlays()
        cfg.ChatTranslationTargetLanguage = GetSelectedChatTargetLanguageCode()
        cfg.ChatTranslationScanIntervalMs = CInt(If(nudChatScanMs IsNot Nothing, nudChatScanMs.Value, 700D))
        cfg.ChatTranslationMaxLines = CInt(If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value, 6D))
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.MobNameRect = BuildRect("mob_name_rect")
        cfg.MobHpRect = BuildRect("mob_hp_rect")
        cfg.UnreachableTextRect = BuildRect("unreachable_text_rect")
        cfg.PranaExpRect = BuildRect("prana_exp_rect")
        cfg.RupiahsRect = BuildRect("rupiahs_rect")
        cfg.PartyInviteScanRect = BuildRect("party_invite_scan_rect")
        cfg.PartyInviteOkRect = BuildRect("party_invite_ok_rect")
        cfg.PartyListRect = BuildRect("party_list_rect")
        cfg.MapRect = BuildRectOrFallback("map_rect", New RectRegion(0, 0, 1024, 768))
        Dim legacyMapCoordinateRect As RectRegion = BuildRectOrFallback("map_coordinate_rect", BotConfig.DefaultMapCoordinateRect())
        cfg.MapCoordinateXRect = BuildRectOrFallback("map_coordinate_x_rect", BotConfig.SplitMapCoordinateRect(legacyMapCoordinateRect, True))
        cfg.MapCoordinateYRect = BuildRectOrFallback("map_coordinate_y_rect", BotConfig.SplitMapCoordinateRect(legacyMapCoordinateRect, False))
        cfg.MapCoordinateRect = BotConfig.CombineMapCoordinateRects(cfg.MapCoordinateXRect, cfg.MapCoordinateYRect)
        cfg.ChatRect = BuildRect("chat_rect")
        cfg.LootScanPoints = BuildLootScanPoints()
        cfg.LootScanRect = BuildLootScanBoundingRect(cfg.LootScanPoints)
        cfg.LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked)
        cfg.LootPickupIntervalMs = CInt(Math.Round(CDbl(If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D)) * 1000.0))
        cfg.LootNameMatchThresholdPercent = CInt(If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value, CDec(DefaultLootNameMatchThresholdPercent)))
        cfg.LootNameAutoPickupEnabled = (chkLootNameAutoPickup IsNot Nothing AndAlso chkLootNameAutoPickup.Checked)
        cfg.LootNamePickupOffsetX = CInt(If(nudLootNamePickupOffsetX IsNot Nothing, nudLootNamePickupOffsetX.Value, 0D))
        cfg.LootNamePickupOffsetY = CInt(If(nudLootNamePickupOffsetY IsNot Nothing, nudLootNamePickupOffsetY.Value, 18D))
        cfg.LootNamePickupPointX = _lootNamePickupPointX
        cfg.LootNamePickupPointY = _lootNamePickupPointY
        cfg.LootNamePickupClickDelayMs = CInt(If(nudLootNamePickupClickDelayMs IsNot Nothing, nudLootNamePickupClickDelayMs.Value, 180D))
        cfg.LootNamePickupFPressCount = CInt(If(nudLootNamePickupFPressCount IsNot Nothing, nudLootNamePickupFPressCount.Value, 3D))
        cfg.LootNamePickupFPressGapMs = CInt(If(nudLootNamePickupFPressGapMs IsNot Nothing, nudLootNamePickupFPressGapMs.Value, 110D))
        cfg.LootNamePickupMouseHoldMs = CInt(If(nudLootNamePickupMouseHoldMs IsNot Nothing, nudLootNamePickupMouseHoldMs.Value, 35D))
        cfg.LootNamePickupRestoreCursor = (chkLootNamePickupRestoreCursor Is Nothing OrElse chkLootNamePickupRestoreCursor.Checked)
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
            Dim role As String = SafeCell(row, "Role", "attack").ToLowerInvariant()
            actions.Add(New ActionRule With {
                .KeyName = keyName,
                .Enabled = enabled,
                .Role = role,
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
        Dim region As RectRegion = Nothing
        If TryBuildRect(regionName, region) Then
            Return region
        End If
        Return New RectRegion(0, 0, 1, 1)
    End Function

    Private Function BuildRectOrFallback(regionName As String, fallback As RectRegion) As RectRegion
        Dim region As RectRegion = Nothing
        If TryBuildRect(regionName, region) Then
            Return region
        End If

        Dim source As RectRegion = If(fallback, New RectRegion(0, 0, 1, 1))
        Return New RectRegion(source.X, source.Y, Math.Max(1, source.W), Math.Max(1, source.H))
    End Function

    Private Function TryBuildRect(regionName As String, ByRef region As RectRegion) As Boolean
        For Each row As DataGridViewRow In dgvRegions.Rows
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                region = New RectRegion(
                    ParseInt(SafeCell(row, "X", "0"), 0),
                    ParseInt(SafeCell(row, "Y", "0"), 0),
                    Math.Max(1, ParseInt(SafeCell(row, "W", "1"), 1)),
                    Math.Max(1, ParseInt(SafeCell(row, "H", "1"), 1)))
                Return True
            End If
        Next
        region = Nothing
        Return False
    End Function

    Private Function BuildDisabledCalibrationRegionOverlays() As List(Of String)
        Dim disabled As New List(Of String)()
        If dgvRegions Is Nothing Then
            Return disabled
        End If

        For Each row As DataGridViewRow In dgvRegions.Rows
            If row.IsNewRow Then
                Continue For
            End If

            Dim regionName As String = SafeCell(row, "Region", "").Trim()
            If regionName = "" Then
                Continue For
            End If

            Dim enabled As Boolean = True
            If dgvRegions.Columns.Contains("Enabled") Then
                Dim raw As Object = row.Cells("Enabled").Value
                If raw IsNot Nothing Then
                    Boolean.TryParse(raw.ToString(), enabled)
                End If
            End If

            If Not enabled Then
                disabled.Add(regionName)
            End If
        Next

        Return disabled
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

    Private Shared Function CloneRectRegion(region As RectRegion) As RectRegion
        If region Is Nothing Then
            Return New RectRegion(0, 0, 1, 1)
        End If
        Return New RectRegion(region.X, region.Y, Math.Max(1, region.W), Math.Max(1, region.H))
    End Function

    Private Shared Function SameRegion(a As RectRegion, b As RectRegion) As Boolean
        Return a IsNot Nothing AndAlso b IsNot Nothing AndAlso a.X = b.X AndAlso a.Y = b.Y AndAlso a.W = b.W AndAlso a.H = b.H
    End Function

    Private Shared Function ShouldSplitLegacyMapCoordinateRect(cfg As BotConfig) As Boolean
        If cfg Is Nothing OrElse cfg.MapCoordinateRect Is Nothing Then
            Return False
        End If

        Dim xMissingOrDefault As Boolean = cfg.MapCoordinateXRect Is Nothing OrElse SameRegion(cfg.MapCoordinateXRect, BotConfig.DefaultMapCoordinateXRect())
        Dim yMissingOrDefault As Boolean = cfg.MapCoordinateYRect Is Nothing OrElse SameRegion(cfg.MapCoordinateYRect, BotConfig.DefaultMapCoordinateYRect())
        Return xMissingOrDefault AndAlso yMissingOrDefault AndAlso Not SameRegion(cfg.MapCoordinateRect, BotConfig.DefaultMapCoordinateRect())
    End Function

    Private Shared Function ResolveMapCoordinateXRect(cfg As BotConfig) As RectRegion
        If ShouldSplitLegacyMapCoordinateRect(cfg) Then
            Return BotConfig.SplitMapCoordinateRect(cfg.MapCoordinateRect, True)
        End If
        Return CloneRectRegion(If(cfg?.MapCoordinateXRect, BotConfig.DefaultMapCoordinateXRect()))
    End Function

    Private Shared Function ResolveMapCoordinateYRect(cfg As BotConfig) As RectRegion
        If ShouldSplitLegacyMapCoordinateRect(cfg) Then
            Return BotConfig.SplitMapCoordinateRect(cfg.MapCoordinateRect, False)
        End If
        Return CloneRectRegion(If(cfg?.MapCoordinateYRect, BotConfig.DefaultMapCoordinateYRect()))
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
        Dim tableSamples As List(Of NavigationRouteSample) = ReadBreadcrumbSamplesFromGrid()
        Dim savedPath As String = If(tableSamples.Count >= 2,
            _fullEngine.SaveRecordedRouteSamples(cfg, tableSamples),
            _fullEngine.SaveRecordedRoute(cfg))
        If String.IsNullOrWhiteSpace(savedPath) Then
            AppendLog("Recorded route save failed. Add at least two valid X/Y breadcrumb rows or record enough coordinate samples first.")
            Return
        End If

        AppendLog("Recorded route saved: " & savedPath)
        _lastRouteRecordingSavedPath = savedPath
        PopulateNavigationNodeCombos()
        PopulateRecordedRouteManager()
        SavePersistedListState(False)
    End Sub

    Private Sub AddManualRouteNodeClicked(sender As Object, e As EventArgs)
        If dgvBreadcrumbs Is Nothing Then
            Return
        End If

        _breadcrumbsManualEditMode = True
        AppendBreadcrumbRow(CInt(nudManualRouteNodeX.Value), CInt(nudManualRouteNodeY.Value), "manual " & DateTime.Now.ToString("HH:mm:ss"))
        AppendLog($"Manual route node added: X={CInt(nudManualRouteNodeX.Value):000}, Y={CInt(nudManualRouteNodeY.Value):000}.")
    End Sub

    Private Sub DeleteManualBreadcrumbClicked(sender As Object, e As EventArgs)
        If dgvBreadcrumbs Is Nothing OrElse dgvBreadcrumbs.SelectedRows.Count = 0 Then
            AppendLog("Select a breadcrumb row first.")
            Return
        End If

        _breadcrumbsManualEditMode = True
        For Each row As DataGridViewRow In dgvBreadcrumbs.SelectedRows
            If Not row.IsNewRow Then
                dgvBreadcrumbs.Rows.Remove(row)
            End If
        Next
        RenumberBreadcrumbRows()
    End Sub

    Private Sub ClearManualBreadcrumbsClicked(sender As Object, e As EventArgs)
        If dgvBreadcrumbs Is Nothing Then
            Return
        End If

        _breadcrumbsManualEditMode = True
        dgvBreadcrumbs.Rows.Clear()
        AppendLog("Breadcrumb table cleared for manual route entry.")
    End Sub

    Private Sub BreadcrumbsGridEdited(sender As Object, e As EventArgs)
        If _updatingBreadcrumbsGrid Then
            Return
        End If

        _breadcrumbsManualEditMode = True
        RenumberBreadcrumbRows()
    End Sub

    Private Sub BreadcrumbsGridUserAddedRow(sender As Object, e As DataGridViewRowEventArgs)
        If _updatingBreadcrumbsGrid Then
            Return
        End If

        _breadcrumbsManualEditMode = True
        RenumberBreadcrumbRows()
    End Sub

    Private Sub BreadcrumbsGridUserDeletedRow(sender As Object, e As DataGridViewRowEventArgs)
        If _updatingBreadcrumbsGrid Then
            Return
        End If

        _breadcrumbsManualEditMode = True
        RenumberBreadcrumbRows()
    End Sub

    Private Sub StartRouteRecordingClicked(sender As Object, e As EventArgs)
        _routeRecordingActive = True
        _breadcrumbsManualEditMode = False
        ' Auto-enable navigation if not already enabled (needed for coordinate OCR)
        If chkNavigationEnabled IsNot Nothing AndAlso Not chkNavigationEnabled.Checked Then
            chkNavigationEnabled.Checked = True
        End If
        UpdateRouteRecordingButtonStates()
        LiveConfigChanged(sender, e)
        ' Auto-start bot if not running
        If Not _fullEngine.IsRunning() Then
            _routeRecordingAutoStartedBot = True
            StartEdition(BotEdition.Full, False)
            AppendLog("Route recording: auto-started bot for coordinate capture.")
        End If
        AppendLog("Route recording started.")
    End Sub

    Private Sub StopRouteRecordingClicked(sender As Object, e As EventArgs)
        _routeRecordingActive = False
        UpdateRouteRecordingButtonStates()
        LiveConfigChanged(sender, e)
        AppendLog("Route recording stopped.")
        ' Auto-stop bot if we auto-started it
        If _routeRecordingAutoStartedBot Then
            _routeRecordingAutoStartedBot = False
            StopEdition(BotEdition.Full, False, "route recording stopped")
            AppendLog("Route recording: auto-stopped bot.")
        End If
    End Sub

    Private Sub UpdateRouteRecordingButtonStates()
        If btnStartRouteRecording IsNot Nothing Then
            btnStartRouteRecording.Enabled = Not _routeRecordingActive
        End If
        If btnStopRouteRecording IsNot Nothing Then
            btnStopRouteRecording.Enabled = _routeRecordingActive
        End If
    End Sub

    Private Sub UpdateBreadcrumbsGrid(samples As List(Of NavigationRouteSample))
        If dgvBreadcrumbs Is Nothing Then Return
        Dim src As List(Of NavigationRouteSample) = If(samples, New List(Of NavigationRouteSample)())
        If _breadcrumbsManualEditMode AndAlso Not _routeRecordingActive Then
            Return
        End If
        ' Only update if count changed to avoid flicker during editing
        If CountBreadcrumbDataRows() = src.Count AndAlso Not _routeRecordingActive Then
            Return
        End If
        _updatingBreadcrumbsGrid = True
        dgvBreadcrumbs.SuspendLayout()
        dgvBreadcrumbs.Rows.Clear()
        For i As Integer = 0 To src.Count - 1
            dgvBreadcrumbs.Rows.Add((i + 1).ToString(), src(i).X.ToString("000"), src(i).Y.ToString("000"), src(i).CapturedAtUtc.ToLocalTime().ToString("HH:mm:ss.fff"))
        Next
        If dgvBreadcrumbs.Rows.Count > 0 Then
            dgvBreadcrumbs.FirstDisplayedScrollingRowIndex = dgvBreadcrumbs.Rows.Count - 1
        End If
        dgvBreadcrumbs.ResumeLayout()
        _updatingBreadcrumbsGrid = False
    End Sub

    Private Sub AppendBreadcrumbRow(x As Integer, y As Integer, capturedAt As String)
        If dgvBreadcrumbs Is Nothing Then
            Return
        End If

        _updatingBreadcrumbsGrid = True
        dgvBreadcrumbs.Rows.Add((CountBreadcrumbDataRows() + 1).ToString(), x.ToString("000"), y.ToString("000"), capturedAt)
        If dgvBreadcrumbs.Rows.Count > 0 Then
            dgvBreadcrumbs.FirstDisplayedScrollingRowIndex = Math.Max(0, dgvBreadcrumbs.Rows.Count - 1)
        End If
        _updatingBreadcrumbsGrid = False
    End Sub

    Private Function CountBreadcrumbDataRows() As Integer
        If dgvBreadcrumbs Is Nothing Then
            Return 0
        End If

        Dim count As Integer = 0
        For Each row As DataGridViewRow In dgvBreadcrumbs.Rows
            If Not row.IsNewRow Then
                count += 1
            End If
        Next
        Return count
    End Function

    Private Sub RenumberBreadcrumbRows()
        If dgvBreadcrumbs Is Nothing Then
            Return
        End If

        _updatingBreadcrumbsGrid = True
        Dim idx As Integer = 1
        For Each row As DataGridViewRow In dgvBreadcrumbs.Rows
            If row.IsNewRow Then
                Continue For
            End If
            row.Cells("Idx").Value = idx.ToString()
            If String.IsNullOrWhiteSpace(Convert.ToString(row.Cells("At").Value)) Then
                row.Cells("At").Value = "manual"
            End If
            idx += 1
        Next
        _updatingBreadcrumbsGrid = False
    End Sub

    Private Function ReadBreadcrumbSamplesFromGrid() As List(Of NavigationRouteSample)
        Dim samples As New List(Of NavigationRouteSample)()
        If dgvBreadcrumbs Is Nothing Then
            Return samples
        End If

        For Each row As DataGridViewRow In dgvBreadcrumbs.Rows
            If row.IsNewRow Then
                Continue For
            End If

            Dim x As Integer
            Dim y As Integer
            If Integer.TryParse(Convert.ToString(row.Cells("X").Value).Trim(), x) AndAlso
               Integer.TryParse(Convert.ToString(row.Cells("Y").Value).Trim(), y) AndAlso
               x >= 0 AndAlso x <= 999 AndAlso y >= 0 AndAlso y <= 999 Then
                samples.Add(New NavigationRouteSample With {
                    .X = x,
                    .Y = y,
                    .CapturedAtUtc = DateTime.UtcNow.AddMilliseconds(samples.Count)
                })
            End If
        Next

        Return samples
    End Function

    Private Sub ReplayRouteClicked(sender As Object, e As EventArgs)
        Dim routeName As String = ExtractRecordedRouteName(If(cboRecordedRoute Is Nothing, Nothing, cboRecordedRoute.SelectedItem))
        If routeName = "" Then
            AppendLog("Select a recorded route first to replay.")
            Return
        End If
        Dim mapName As String = GetSelectedNavigationMapName()
        Dim nodes As List(Of NavigationNode) = BotEngine.GetRecordedRouteNodeOptions(routeName, mapName)
        If nodes Is Nothing OrElse nodes.Count = 0 Then
            AppendLog($"No nodes found for route '{routeName}'.")
            Return
        End If
        If dgvBreadcrumbs IsNot Nothing Then
            _breadcrumbsManualEditMode = True
            _updatingBreadcrumbsGrid = True
            dgvBreadcrumbs.SuspendLayout()
            dgvBreadcrumbs.Rows.Clear()
            For i As Integer = 0 To nodes.Count - 1
                dgvBreadcrumbs.Rows.Add((i + 1).ToString(), nodes(i).X.ToString("000"), nodes(i).Y.ToString("000"), nodes(i).Label)
            Next
            If dgvBreadcrumbs.Rows.Count > 0 Then
                dgvBreadcrumbs.FirstDisplayedScrollingRowIndex = 0
            End If
            dgvBreadcrumbs.ResumeLayout()
            _updatingBreadcrumbsGrid = False
        End If
        AppendLog($"Replaying route '{routeName}' with {nodes.Count} nodes.")
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

            Dim state As PersistedListState = Nothing
            Dim liteState As PersistedLiteState = Nothing
            Dim appState As PersistedAppState = Nothing
            Try
                appState = JsonSerializer.Deserialize(Of PersistedAppState)(raw)
            Catch
            End Try

            Dim hasSeparatedState As Boolean =
                raw.IndexOf("""Full""", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                raw.IndexOf("""Lite""", StringComparison.OrdinalIgnoreCase) >= 0

            If hasSeparatedState AndAlso appState IsNot Nothing Then
                state = If(appState.Full, New PersistedListState())
                liteState = If(appState.Lite, New PersistedLiteState())
                If txtWindowTitle IsNot Nothing Then
                    Dim sharedTitle As String = If(appState.WindowTitle, "").Trim()
                    txtWindowTitle.Text = If(sharedTitle = "", DefaultGameWindowTitle, sharedTitle)
                End If
            Else
                state = JsonSerializer.Deserialize(Of PersistedListState)(raw)
                liteState = New PersistedLiteState()
                If txtWindowTitle IsNot Nothing AndAlso state IsNot Nothing AndAlso state.SavedConfig IsNot Nothing Then
                    Dim legacyTitle As String = If(state.SavedConfig.WindowTitle, "").Trim()
                    txtWindowTitle.Text = If(legacyTitle = "", DefaultGameWindowTitle, legacyTitle)
                End If
            End If
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
            If chkLootNameAutoPickup IsNot Nothing Then
                chkLootNameAutoPickup.Checked = state.LootNameAutoPickupEnabled
            End If
            If state.LootNamePickupPointEnabled Then
                _lootNamePickupPointX = Math.Max(0, state.LootNamePickupPointX)
                _lootNamePickupPointY = Math.Max(0, state.LootNamePickupPointY)
            Else
                _lootNamePickupPointX = -1
                _lootNamePickupPointY = -1
            End If
            If nudLootNamePickupOffsetX IsNot Nothing Then
                nudLootNamePickupOffsetX.Value = Math.Max(nudLootNamePickupOffsetX.Minimum, Math.Min(nudLootNamePickupOffsetX.Maximum, state.LootNamePickupOffsetX))
            End If
            If nudLootNamePickupOffsetY IsNot Nothing Then
                nudLootNamePickupOffsetY.Value = Math.Max(nudLootNamePickupOffsetY.Minimum, Math.Min(nudLootNamePickupOffsetY.Maximum, state.LootNamePickupOffsetY))
            End If
            If nudLootNamePickupClickDelayMs IsNot Nothing Then
                nudLootNamePickupClickDelayMs.Value = Math.Max(nudLootNamePickupClickDelayMs.Minimum, Math.Min(nudLootNamePickupClickDelayMs.Maximum, state.LootNamePickupClickDelayMs))
            End If
            If nudLootNamePickupFPressCount IsNot Nothing Then
                nudLootNamePickupFPressCount.Value = Math.Max(nudLootNamePickupFPressCount.Minimum, Math.Min(nudLootNamePickupFPressCount.Maximum, state.LootNamePickupFPressCount))
            End If
            If nudLootNamePickupFPressGapMs IsNot Nothing Then
                nudLootNamePickupFPressGapMs.Value = Math.Max(nudLootNamePickupFPressGapMs.Minimum, Math.Min(nudLootNamePickupFPressGapMs.Maximum, state.LootNamePickupFPressGapMs))
            End If
            If nudLootNamePickupMouseHoldMs IsNot Nothing Then
                nudLootNamePickupMouseHoldMs.Value = Math.Max(nudLootNamePickupMouseHoldMs.Minimum, Math.Min(nudLootNamePickupMouseHoldMs.Maximum, state.LootNamePickupMouseHoldMs))
            End If
            If chkLootNamePickupRestoreCursor IsNot Nothing Then
                chkLootNamePickupRestoreCursor.Checked = state.LootNamePickupRestoreCursor
            End If
            If state.LootRejectPointEnabled Then
                _lootRejectPointX = Math.Max(0, state.LootRejectPointX)
                _lootRejectPointY = Math.Max(0, state.LootRejectPointY)
            Else
                _lootRejectPointX = -1
                _lootRejectPointY = -1
            End If
            _isPickingLootRejectPoint = False
            _isPickingLootNamePickupPoint = False
            UpdateLootRejectPointUi()
            UpdateLootNamePickupPointUi()
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
            If cboNotificationProvider IsNot Nothing Then
                cboNotificationProvider.SelectedItem = NormalizeNotificationProviderName(state.NotificationProvider)
            End If
            If txtDiscordGlobalWebhookUrl IsNot Nothing Then
                Dim globalWebhook As String = If(state.DiscordGlobalWebhookUrl, "").Trim()
                If globalWebhook = "" Then
                    globalWebhook = If(state.DiscordWebhookUrl, "").Trim()
                End If
                txtDiscordGlobalWebhookUrl.Text = globalWebhook
            End If
            If txtDiscordItemWebhookUrl IsNot Nothing Then
                Dim itemWebhook As String = If(state.DiscordItemWebhookUrl, "").Trim()
                If itemWebhook = "" Then
                    itemWebhook = If(state.DiscordWebhookUrl, "").Trim()
                End If
                txtDiscordItemWebhookUrl.Text = itemWebhook
            End If
            If txtDiscordStatsWebhookUrl IsNot Nothing Then
                Dim statsWebhook As String = If(state.DiscordStatsWebhookUrl, "").Trim()
                If statsWebhook = "" Then
                    statsWebhook = If(state.DiscordWebhookUrl, "").Trim()
                End If
                txtDiscordStatsWebhookUrl.Text = statsWebhook
            End If
            If txtItemNtfyTopic IsNot Nothing Then
                txtItemNtfyTopic.Text = If(state.ItemNtfyTopic, "").Trim()
            End If
            If txtStatsNtfyTopic IsNot Nothing Then
                txtStatsNtfyTopic.Text = If(state.StatsNtfyTopic, "").Trim()
            End If
            If nudStatsNtfyIntervalMinutes IsNot Nothing Then
                Dim boundedStatsInterval As Decimal = Math.Max(nudStatsNtfyIntervalMinutes.Minimum, Math.Min(nudStatsNtfyIntervalMinutes.Maximum, state.StatsNtfyIntervalMinutes))
                nudStatsNtfyIntervalMinutes.Value = boundedStatsInterval
            End If
            UpdateNotificationProviderUi()
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
            ApplyPersistedLiteState(liteState)
        Catch ex As Exception
            AppendLog("Unable to load saved lists: " & ex.Message)
        End Try
    End Sub

    Private Sub SavePersistedListState(Optional logFailure As Boolean = False, Optional includeFullConfig As Boolean = True)
        Try
            CommitPendingGridEdits()
            If Not Directory.Exists(PersistDirectoryPath) Then
                Directory.CreateDirectory(PersistDirectoryPath)
            End If

            Dim fullState As New PersistedListState With {
                .MonsterFilterEnabled = (chkMonsterFilter IsNot Nothing AndAlso chkMonsterFilter.Checked),
                .LootPickupEnabled = (chkLootPickup IsNot Nothing AndAlso chkLootPickup.Checked),
                .LootPickupSeconds = If(nudLootPickupSeconds IsNot Nothing, nudLootPickupSeconds.Value, 4D),
                .LootNameMatchThresholdPercent = If(nudLootNameMatchThreshold IsNot Nothing, nudLootNameMatchThreshold.Value, CDec(DefaultLootNameMatchThresholdPercent)),
                .LootNameAutoPickupEnabled = (chkLootNameAutoPickup IsNot Nothing AndAlso chkLootNameAutoPickup.Checked),
                .LootNamePickupOffsetX = If(nudLootNamePickupOffsetX IsNot Nothing, nudLootNamePickupOffsetX.Value, 0D),
                .LootNamePickupOffsetY = If(nudLootNamePickupOffsetY IsNot Nothing, nudLootNamePickupOffsetY.Value, 18D),
                .LootNamePickupPointEnabled = (_lootNamePickupPointX >= 0 AndAlso _lootNamePickupPointY >= 0),
                .LootNamePickupPointX = _lootNamePickupPointX,
                .LootNamePickupPointY = _lootNamePickupPointY,
                .LootNamePickupClickDelayMs = If(nudLootNamePickupClickDelayMs IsNot Nothing, nudLootNamePickupClickDelayMs.Value, 180D),
                .LootNamePickupFPressCount = If(nudLootNamePickupFPressCount IsNot Nothing, nudLootNamePickupFPressCount.Value, 3D),
                .LootNamePickupFPressGapMs = If(nudLootNamePickupFPressGapMs IsNot Nothing, nudLootNamePickupFPressGapMs.Value, 110D),
                .LootNamePickupMouseHoldMs = If(nudLootNamePickupMouseHoldMs IsNot Nothing, nudLootNamePickupMouseHoldMs.Value, 35D),
                .LootNamePickupRestoreCursor = (chkLootNamePickupRestoreCursor Is Nothing OrElse chkLootNamePickupRestoreCursor.Checked),
                .LootRejectPointEnabled = (_lootRejectPointX >= 0 AndAlso _lootRejectPointY >= 0),
                .LootRejectPointX = _lootRejectPointX,
                .LootRejectPointY = _lootRejectPointY,
                .PromptAutoAcceptEnabled = _partyAutoAccept,
                .AskForPartyEnabled = _partyAskEnabled,
                .AskForPartySeconds = If(nudPartyAskSeconds IsNot Nothing, nudPartyAskSeconds.Value, 30D),
                                .AskForPartyText = GetPartyAskCommandText(),
                .LootScannerEnabled = _lootScannerEnabled,
                .NotificationProvider = GetNotificationProviderName(),
                .DiscordWebhookUrl = GetDiscordWebhookUrl(),
                .DiscordGlobalWebhookUrl = GetDiscordGlobalWebhookUrl(),
                .DiscordItemWebhookUrl = GetDiscordItemWebhookUrl(),
                .DiscordStatsWebhookUrl = GetDiscordStatsWebhookUrl(),
                .NtfyTopic = If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text.Trim(), ""),
                .ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), ""),
                .StatsNtfyTopic = If(txtStatsNtfyTopic IsNot Nothing, txtStatsNtfyTopic.Text.Trim(), ""),
                .StatsNtfyIntervalMinutes = If(nudStatsNtfyIntervalMinutes IsNot Nothing, nudStatsNtfyIntervalMinutes.Value, 30D),
                .AutoPotHpPercent = If(nudAutoPotHp IsNot Nothing, nudAutoPotHp.Value, 80D),
                .AutoPotMpPercent = If(nudAutoPotMp IsNot Nothing, nudAutoPotMp.Value, 35D),
                .AlarmVolumePercent = CInt(If(nudAlarmVolume IsNot Nothing, nudAlarmVolume.Value, CDec(_alarmVolumePercent))),
                .SavedConfig = If(includeFullConfig, BuildFullConfig(), Nothing),
                .MonsterNames = GetListBoxItems(lstMonsterFilter),
                .LootNames = GetListBoxItems(lstLootFilter),
                .CombatActions = GetPersistedCombatActions()
            }

            Dim liteState As New PersistedLiteState With {
                .AutoPotsEnabled = (chkLiteAutoPots IsNot Nothing AndAlso chkLiteAutoPots.Checked),
                .HpPointEnabled = (_liteAutoPotHpPointX >= 0 AndAlso _liteAutoPotHpPointY >= 0),
                .HpPointX = _liteAutoPotHpPointX,
                .HpPointY = _liteAutoPotHpPointY,
                .MpPointEnabled = (_liteAutoPotMpPointX >= 0 AndAlso _liteAutoPotMpPointY >= 0),
                .MpPointX = _liteAutoPotMpPointX,
                .MpPointY = _liteAutoPotMpPointY,
                .PromptAutoAcceptEnabled = _litePartyAutoAccept,
                .AskForPartyEnabled = _litePartyAskEnabled,
                .AskForPartySeconds = If(nudLitePartyAskSeconds IsNot Nothing, nudLitePartyAskSeconds.Value, 30D),
                .AskForPartyText = GetLitePartyAskCommandText(),
                .Actions = GetPersistedLiteActions()
            }

            Dim appState As New PersistedAppState With {
                .WindowTitle = If(txtWindowTitle IsNot Nothing AndAlso txtWindowTitle.Text.Trim() <> "", txtWindowTitle.Text.Trim(), DefaultGameWindowTitle),
                .Full = fullState,
                .Lite = liteState
            }

            Dim json As String = JsonSerializer.Serialize(appState, New JsonSerializerOptions With {.WriteIndented = True})
            File.WriteAllText(PersistFilePath, json, Encoding.UTF8)
        Catch ex As Exception
            If logFailure Then
                AppendLog("Unable to save list state: " & ex.Message)
            End If
        End Try
    End Sub

    Private Sub ApplyPersistedLiteState(state As PersistedLiteState)
        Dim source As PersistedLiteState = If(state, New PersistedLiteState())
        ApplyPersistedLiteActions(source.Actions)

        _liteSyncInProgress = True
        Try
            If chkLiteAutoPots IsNot Nothing Then
                chkLiteAutoPots.Checked = source.AutoPotsEnabled
            End If
            _litePartyAutoAccept = source.PromptAutoAcceptEnabled
            _litePartyAskEnabled = source.AskForPartyEnabled
            If nudLitePartyAskSeconds IsNot Nothing Then
                Dim boundedAskSeconds As Decimal = Math.Max(nudLitePartyAskSeconds.Minimum, Math.Min(nudLitePartyAskSeconds.Maximum, source.AskForPartySeconds))
                nudLitePartyAskSeconds.Value = boundedAskSeconds
            End If
            If txtLitePartyAskText IsNot Nothing Then
                txtLitePartyAskText.Text = If(String.IsNullOrWhiteSpace(source.AskForPartyText), DefaultPartyAskCommand, source.AskForPartyText.Trim())
            End If
            If source.HpPointEnabled Then
                _liteAutoPotHpPointX = Math.Max(0, source.HpPointX)
                _liteAutoPotHpPointY = Math.Max(0, source.HpPointY)
            Else
                _liteAutoPotHpPointX = -1
                _liteAutoPotHpPointY = -1
            End If
            If source.MpPointEnabled Then
                _liteAutoPotMpPointX = Math.Max(0, source.MpPointX)
                _liteAutoPotMpPointY = Math.Max(0, source.MpPointY)
            Else
                _liteAutoPotMpPointX = -1
                _liteAutoPotMpPointY = -1
            End If
            _pendingLitePointCapture = LitePointCaptureKind.None
            UpdateLiteAutoPotUi()
            UpdateLitePromptAutoAcceptButton()
            UpdateLitePartyAskButton()
        Finally
            _liteSyncInProgress = False
        End Try
    End Sub

    Private Sub ApplySavedConfigToUi(cfg As BotConfig)
        If cfg Is Nothing Then
            Return
        End If

        If txtWindowTitle IsNot Nothing AndAlso String.IsNullOrWhiteSpace(txtWindowTitle.Text) Then
            txtWindowTitle.Text = DefaultGameWindowTitle
        End If
        SetNumericControlValue(nudLoopMs, cfg.LoopMs)
        SetNumericControlValue(nudRetargetMs, cfg.RetargetMs)
        SetNumericControlValue(nudForcedRetargetMs, If(cfg.ForcedRetargetMs > 0, cfg.ForcedRetargetMs, cfg.RetargetMs))
        SetNumericControlValue(nudStuckTargetMs, cfg.StuckTargetMs)
        SetNumericControlValue(nudStuckNoProgressRetargetMs, If(cfg.StuckTargetNoProgressRetargetMs > 0, cfg.StuckTargetNoProgressRetargetMs, 6000))
        SetNumericControlValue(nudMobHpThreshold, CDec(cfg.MobHpPresenceThreshold))
        If chkHighMaxHpSpecial IsNot Nothing Then
            chkHighMaxHpSpecial.Checked = cfg.HighMaxHpSpecialEnabled
        End If
        SetNumericControlValue(nudHighMaxHpThreshold, CDec(Math.Max(100, cfg.HighMaxHpThreshold)))
        If chkAvoidHighMaxHpTargets IsNot Nothing Then
            chkAvoidHighMaxHpTargets.Checked = cfg.AvoidHighMaxHpEnabled
        End If
        SetNumericControlValue(nudAvoidHighMaxHpThreshold, CDec(Math.Max(100, cfg.AvoidHighMaxHpThreshold)))

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
        If cboNotificationProvider IsNot Nothing Then
            cboNotificationProvider.SelectedItem = NormalizeNotificationProviderName(cfg.NotificationProvider)
        End If
        If txtDiscordGlobalWebhookUrl IsNot Nothing Then
            Dim globalWebhook As String = If(cfg.DiscordGlobalWebhookUrl, "").Trim()
            If globalWebhook = "" Then
                globalWebhook = If(cfg.DiscordWebhookUrl, "").Trim()
            End If
            txtDiscordGlobalWebhookUrl.Text = globalWebhook
        End If
        If txtDiscordItemWebhookUrl IsNot Nothing Then
            Dim itemWebhook As String = If(cfg.DiscordItemWebhookUrl, "").Trim()
            If itemWebhook = "" Then
                itemWebhook = If(cfg.DiscordWebhookUrl, "").Trim()
            End If
            txtDiscordItemWebhookUrl.Text = itemWebhook
        End If
        If txtDiscordStatsWebhookUrl IsNot Nothing Then
            Dim statsWebhook As String = If(cfg.DiscordStatsWebhookUrl, "").Trim()
            If statsWebhook = "" Then
                statsWebhook = If(cfg.DiscordWebhookUrl, "").Trim()
            End If
            txtDiscordStatsWebhookUrl.Text = statsWebhook
        End If
        UpdateNotificationProviderUi()
        If txtItemNtfyTopic IsNot Nothing Then
            txtItemNtfyTopic.Text = If(cfg.ItemNtfyTopic, "").Trim()
        End If
        If chkLevelingAgent IsNot Nothing Then
            chkLevelingAgent.Checked = cfg.LevelingAgentEnabled
        End If
        If txtLevelingPreferredMobs IsNot Nothing Then
            txtLevelingPreferredMobs.Text = String.Join(", ", If(cfg.LevelingPreferredMobs, New List(Of String)()))
        End If
        If chkLevelingStopHp IsNot Nothing Then
            chkLevelingStopHp.Checked = cfg.LevelingStopHpEnabled
        End If
        SetNumericControlValue(nudLevelingStopHp, CDec(Math.Max(1, cfg.LevelingStopHpPercent)))
        If chkLevelingStopMp IsNot Nothing Then
            chkLevelingStopMp.Checked = cfg.LevelingStopMpEnabled
        End If
        SetNumericControlValue(nudLevelingStopMp, CDec(Math.Max(1, cfg.LevelingStopMpPercent)))
        If chkLevelingMaxNoTarget IsNot Nothing Then
            chkLevelingMaxNoTarget.Checked = cfg.LevelingMaxNoTargetEnabled
        End If
        SetNumericControlValue(nudLevelingMaxNoTargetSeconds, CDec(Math.Max(5, cfg.LevelingMaxNoTargetSeconds)))
        UpdateLevelingGuardrailToggleUi()
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
        _routeRecordingActive = cfg.RouteRecordingEnabled
        UpdateRouteRecordingButtonStates()
        SetNumericControlValue(nudRouteRecordingIntervalMs, CDec(Math.Max(100, cfg.RouteRecordingSampleIntervalMs)))
        SetNumericControlValue(nudRouteRecordingMinConfidence, CDec(Math.Max(0, Math.Min(100, cfg.RouteRecordingMinConfidencePercent))))
        SetNumericControlValue(nudRouteRecordingNodeSpacing, CDec(Math.Max(1, cfg.RouteRecordingMinNodeSpacing)))
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
        If chkChatTranslationEnabled IsNot Nothing Then
            chkChatTranslationEnabled.Checked = cfg.ChatTranslationEnabled
        End If
        If chkChatTranslationOverlay IsNot Nothing Then
            chkChatTranslationOverlay.Checked = cfg.ChatTranslationOverlayEnabled
        End If
        If cboChatTargetLanguage IsNot Nothing Then
            SelectChatTargetLanguage(cfg.ChatTranslationTargetLanguage)
        End If
        SetNumericControlValue(nudChatScanMs, CDec(Math.Max(250, cfg.ChatTranslationScanIntervalMs)))
        SetNumericControlValue(nudChatMaxLines, CDec(Math.Max(1, cfg.ChatTranslationMaxLines)))
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
        If chkLootNameAutoPickup IsNot Nothing Then
            chkLootNameAutoPickup.Checked = cfg.LootNameAutoPickupEnabled
        End If
        SetNumericControlValue(nudLootNamePickupOffsetX, CDec(cfg.LootNamePickupOffsetX))
        SetNumericControlValue(nudLootNamePickupOffsetY, CDec(cfg.LootNamePickupOffsetY))
        If cfg.LootNamePickupPointX >= 0 AndAlso cfg.LootNamePickupPointY >= 0 Then
            _lootNamePickupPointX = cfg.LootNamePickupPointX
            _lootNamePickupPointY = cfg.LootNamePickupPointY
        Else
            _lootNamePickupPointX = -1
            _lootNamePickupPointY = -1
        End If
        _isPickingLootNamePickupPoint = False
        SetNumericControlValue(nudLootNamePickupClickDelayMs, CDec(Math.Max(0, cfg.LootNamePickupClickDelayMs)))
        SetNumericControlValue(nudLootNamePickupFPressCount, CDec(Math.Max(1, cfg.LootNamePickupFPressCount)))
        SetNumericControlValue(nudLootNamePickupFPressGapMs, CDec(Math.Max(0, cfg.LootNamePickupFPressGapMs)))
        SetNumericControlValue(nudLootNamePickupMouseHoldMs, CDec(Math.Max(0, cfg.LootNamePickupMouseHoldMs)))
        If chkLootNamePickupRestoreCursor IsNot Nothing Then
            chkLootNamePickupRestoreCursor.Checked = cfg.LootNamePickupRestoreCursor
        End If
        UpdateLootNamePickupPointUi()

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
        UpsertRegionRow("party_list_rect", cfg.PartyListRect)
        RemoveRegionRow("map_rect")
        Dim mapCoordinateXRect As RectRegion = ResolveMapCoordinateXRect(cfg)
        Dim mapCoordinateYRect As RectRegion = ResolveMapCoordinateYRect(cfg)
        cfg.MapCoordinateXRect = mapCoordinateXRect
        cfg.MapCoordinateYRect = mapCoordinateYRect
        cfg.MapCoordinateRect = BotConfig.CombineMapCoordinateRects(mapCoordinateXRect, mapCoordinateYRect)
        UpsertRegionRow("map_coordinate_x_rect", mapCoordinateXRect)
        UpsertRegionRow("map_coordinate_y_rect", mapCoordinateYRect)
        RemoveRegionRow("map_coordinate_rect")
        UpsertRegionRow("chat_rect", cfg.ChatRect)
        ApplyCalibrationRegionOverlayStates(cfg)
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
        UpdateMainTabIndicators()
    End Sub

    Private Sub ApplyCalibrationRegionOverlayStates(cfg As BotConfig)
        If dgvRegions Is Nothing OrElse Not dgvRegions.Columns.Contains("Enabled") Then
            Return
        End If

        Dim disabled As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If cfg IsNot Nothing AndAlso cfg.DisabledCalibrationRegionOverlays IsNot Nothing Then
            For Each regionName As String In cfg.DisabledCalibrationRegionOverlays
                If Not String.IsNullOrWhiteSpace(regionName) Then
                    disabled.Add(regionName.Trim())
                End If
            Next
        End If

        For Each row As DataGridViewRow In dgvRegions.Rows
            If row.IsNewRow Then
                Continue For
            End If

            Dim regionName As String = SafeCell(row, "Region", "").Trim()
            row.Cells("Enabled").Value = Not disabled.Contains(regionName)
        Next
    End Sub

    Private Sub UpsertRegionRow(regionName As String, region As RectRegion)
        If dgvRegions Is Nothing OrElse String.IsNullOrWhiteSpace(regionName) OrElse region Is Nothing Then
            Return
        End If

        For Each row As DataGridViewRow In dgvRegions.Rows
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                If dgvRegions.Columns.Contains("Enabled") AndAlso row.Cells("Enabled").Value Is Nothing Then
                    row.Cells("Enabled").Value = True
                End If
                row.Cells("X").Value = region.X.ToString()
                row.Cells("Y").Value = region.Y.ToString()
                row.Cells("W").Value = Math.Max(1, region.W).ToString()
                row.Cells("H").Value = Math.Max(1, region.H).ToString()
                Return
            End If
        Next

        dgvRegions.Rows.Add(True, regionName, region.X.ToString(), region.Y.ToString(), Math.Max(1, region.W).ToString(), Math.Max(1, region.H).ToString())
    End Sub

    Private Sub RemoveRegionRow(regionName As String)
        If dgvRegions Is Nothing OrElse String.IsNullOrWhiteSpace(regionName) Then
            Return
        End If

        For i As Integer = dgvRegions.Rows.Count - 1 To 0 Step -1
            Dim row As DataGridViewRow = dgvRegions.Rows(i)
            Dim name As String = SafeCell(row, "Region", "").ToLowerInvariant()
            If name = regionName.ToLowerInvariant() Then
                dgvRegions.Rows.RemoveAt(i)
            End If
        Next
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
            Case "attack", "heal", "max_health", "mana", "special", "high_max_hp", "repair", "stop"
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

    Private Sub UpdateAttackButtonAppearance(_ignored As Boolean)
        Dim fullRunning As Boolean = _fullEngine.IsRunning()
        Dim liteRunning As Boolean = _liteEngine.IsRunning()
        Dim runningEdition As BotEdition? = GetRunningEdition()
        Dim selectedEdition As BotEdition = If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full)

        If btnAttack IsNot Nothing Then
            If fullRunning Then
                btnAttack.Text = "RUNNING"
                btnAttack.BackColor = Color.FromArgb(220, 70, 55)
                btnAttack.ForeColor = Color.White
            Else
                btnAttack.Text = "PAUSED"
                btnAttack.BackColor = Color.FromArgb(40, 180, 80)
                btnAttack.ForeColor = Color.White
            End If
        End If

        If btnLiteAttack IsNot Nothing Then
            btnLiteAttack.Text = If(liteRunning, "Running", If(fullRunning, "Start Lite", "Start"))
            btnLiteAttack.BackColor = If(liteRunning, Color.FromArgb(255, 179, 179), Color.FromArgb(40, 180, 80))
            btnLiteAttack.ForeColor = If(liteRunning, Color.FromArgb(120, 25, 25), Color.White)
        End If

        If btnStopBot IsNot Nothing Then
            btnStopBot.Enabled = fullRunning
        End If
        If btnLiteStop IsNot Nothing Then
            btnLiteStop.Enabled = liteRunning
            btnLiteStop.BackColor = If(liteRunning, Color.FromArgb(230, 92, 92), Color.FromArgb(220, 220, 220))
            btnLiteStop.ForeColor = If(liteRunning, Color.White, Color.FromArgb(120, 120, 120))
        End If

        If lblRunState IsNot Nothing Then
            lblRunState.Text = If(fullRunning, "FULL BOT RUNNING", "FULL BOT PAUSED")
            lblRunState.BackColor = If(fullRunning, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
            lblRunState.ForeColor = Color.White
        End If
        If lblFullEdition IsNot Nothing Then
            lblFullEdition.Text = If(liteRunning, "FULL VERSION - LITE BOT RUNNING", "FULL VERSION - for more powerful computers")
        End If

        If lblLiteRunState IsNot Nothing Then
            lblLiteRunState.Text = If(liteRunning, "LITE BOT RUNNING", "LITE BOT PAUSED")
            lblLiteRunState.BackColor = If(liteRunning, Color.FromArgb(86, 168, 123), Color.FromArgb(187, 108, 108))
            lblLiteRunState.ForeColor = Color.White
        End If

        If lblLiteActiveMode IsNot Nothing Then
            Dim activeText As String = "ACTIVE BOT: NONE"
            If runningEdition.HasValue Then
                activeText = $"ACTIVE BOT: {runningEdition.Value.ToString().ToUpperInvariant()}"
            End If
            lblLiteActiveMode.Text = activeText
        End If

        If lblShortcutHint IsNot Nothing Then
            lblShortcutHint.Text = If(fullRunning, "Ctrl+Shift -> Pause Full Bot", $"Ctrl+Shift -> Start {selectedEdition}")
        End If
        If lblLiteShortcutHint IsNot Nothing Then
            If liteRunning Then
                lblLiteShortcutHint.Text = "Ctrl+Shift -> Pause Lite Bot"
            ElseIf fullRunning Then
                lblLiteShortcutHint.Text = "Full is running. Start Lite to switch modes."
            Else
                lblLiteShortcutHint.Text = "Ctrl+Shift -> Start selected tab"
            End If
        End If
        UpdateMainTabIndicators()
    End Sub

    Private Sub UpdateLiteStatus(statusText As String, status As BotStatus)
        If lblLiteState IsNot Nothing Then
            lblLiteState.Text = statusText
        End If
        If lblLiteSystem IsNot Nothing Then
            lblLiteSystem.Text = $"Lite Active: {status.Running}"
        End If
        If lblLiteHp IsNot Nothing Then
            lblLiteHp.Text = $"HP%: {status.HpPercent:0.0}"
            lblLiteHp.ForeColor = HpColor(status.HpPercent)
        End If
        If lblLiteMp IsNot Nothing Then
            lblLiteMp.Text = $"MP%: {status.MpPercent:0.0}"
            lblLiteMp.ForeColor = MpColor(status.MpPercent)
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
                    AppendLogSafe("Notification failed after retries. Check notification settings/network.")
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
                    AppendLogSafe("Game-window alert failed after retries. Check notification settings/network.")
                End If
            End Function)
    End Sub

    Private Shared Function NormalizeNotificationProviderName(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim().ToLowerInvariant()
        If cleaned = NotificationProviderDiscord Then
            Return NotificationProviderDiscord
        End If
        Return NotificationProviderNtfy
    End Function

    Private Function GetNotificationProviderName() As String
        Dim raw As String = ""
        If cboNotificationProvider IsNot Nothing AndAlso cboNotificationProvider.SelectedItem IsNot Nothing Then
            raw = cboNotificationProvider.SelectedItem.ToString()
        End If
        Return NormalizeNotificationProviderName(raw)
    End Function

    Private Function GetDiscordWebhookUrl() As String
        Return GetDiscordGlobalWebhookUrl()
    End Function

    Private Function GetDiscordGlobalWebhookUrl() As String
        Return If(txtDiscordGlobalWebhookUrl IsNot Nothing, txtDiscordGlobalWebhookUrl.Text, "").Trim()
    End Function

    Private Function GetDiscordItemWebhookUrl() As String
        Dim raw As String = If(txtDiscordItemWebhookUrl IsNot Nothing, txtDiscordItemWebhookUrl.Text, "").Trim()
        If raw = "" Then
            Return GetDiscordGlobalWebhookUrl()
        End If
        Return raw
    End Function

    Private Function GetDiscordStatsWebhookUrl() As String
        Dim raw As String = If(txtDiscordStatsWebhookUrl IsNot Nothing, txtDiscordStatsWebhookUrl.Text, "").Trim()
        If raw = "" Then
            Return GetDiscordGlobalWebhookUrl()
        End If
        Return raw
    End Function

    Private Shared Function IsLikelyDiscordWebhookUrl(rawUrl As String) As Boolean
        Dim trimmed As String = If(rawUrl, "").Trim()
        If trimmed = "" Then
            Return False
        End If

        Dim parsed As Uri = Nothing
        If Not Uri.TryCreate(trimmed, UriKind.Absolute, parsed) OrElse parsed Is Nothing Then
            Return False
        End If

        Dim host As String = parsed.Host.ToLowerInvariant()
        If host <> "discord.com" AndAlso host <> "www.discord.com" AndAlso host <> "discordapp.com" Then
            Return False
        End If

        Return parsed.AbsolutePath.IndexOf("/api/webhooks/", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Shared Function NormalizeDiscordWebhookUrl(rawUrl As String) As String
        Dim trimmed As String = If(rawUrl, "").Trim()
        If trimmed = "" Then
            Return ""
        End If

        If Regex.IsMatch(trimmed, "(^|[?&])wait=", RegexOptions.IgnoreCase) Then
            Return trimmed
        End If

        If trimmed.Contains("?"c) Then
            Return trimmed & "&wait=true"
        End If
        Return trimmed & "?wait=true"
    End Function

    Private Function GetNotificationDestinationSummary() As String
        If GetNotificationProviderName() = NotificationProviderDiscord Then
            Return "Discord global webhook"
        End If
        Return $"ntfy topic '{GetNtfyTopicName()}'"
    End Function

    Private Function GetStatsNotificationDestinationSummary() As String
        If GetNotificationProviderName() = NotificationProviderDiscord Then
            Return "Discord stats webhook"
        End If
        Return $"ntfy topic '{GetStatsNtfyTopicName()}'"
    End Function

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

    Private Function GetStatsNtfyTopicName() As String
        Dim raw As String = ""
        If txtStatsNtfyTopic IsNot Nothing Then
            raw = txtStatsNtfyTopic.Text.Trim()
        End If
        If raw = "" Then
            Return ""
        End If

        Return raw.Replace(" ", "").Trim("/"c)
    End Function

    Private Function FormatExpRateForNotification(status As BotStatus) As String
        If status Is Nothing OrElse status.ExpPerHour < 0 Then
            Return "Calculating (1m)"
        End If
        Return status.ExpPerHour.ToString("0.00") & "%/hr"
    End Function

    Private Function FormatRupiahsRateForNotification(status As BotStatus) As String
        If status Is Nothing OrElse status.RupiahsPerHour < 0 Then
            Return "Calculating (1m)"
        End If
        Return status.RupiahsPerHour.ToString("N0") & "/hr"
    End Function

    Private Function FormatPartyForNotification(status As BotStatus) As String
        If status Is Nothing OrElse status.PartySize <= 0 Then
            Return "0 member(s) | Alive: 0/0 | All alive: n/a"
        End If

        Dim aliveCount As Integer = Math.Max(0, Math.Min(status.PartyAliveCount, status.PartySize))
        Dim allAliveText As String = If(status.PartyAllAlive, "Yes", "No")
        Return $"{status.PartySize} member(s) | Alive: {aliveCount}/{status.PartySize} | All alive: {allAliveText}"
    End Function

    Private Function GetStatsNotificationIntervalMinutes() As Integer
        If nudStatsNtfyIntervalMinutes Is Nothing Then
            Return 30
        End If
        Return Math.Max(1, CInt(Math.Truncate(nudStatsNtfyIntervalMinutes.Value)))
    End Function

    Private Sub HandlePeriodicStatsNotification(status As BotStatus)
        Dim provider As String = GetNotificationProviderName()
        Dim topic As String = GetStatsNtfyTopicName()
        If status Is Nothing Then
            Return
        End If
        If provider = NotificationProviderNtfy AndAlso topic = "" Then
            Return
        End If

        If Not status.Running OrElse Not status.WindowFound OrElse IsNotificationWarmupActive() Then
            Return
        End If

        If _lastStatsNotificationUtc = DateTime.MinValue Then
            _lastStatsNotificationUtc = DateTime.UtcNow
            Return
        End If

        Dim intervalMinutes As Integer = GetStatsNotificationIntervalMinutes()
        Dim nextAllowedUtc As DateTime = _lastStatsNotificationUtc.AddMinutes(intervalMinutes)
        If DateTime.UtcNow < nextAllowedUtc Then
            Return
        End If

        Dim body As String =
            $"Character: {If(String.IsNullOrWhiteSpace(status.CharacterName), "n/a", status.CharacterName)}{Environment.NewLine}" &
            $"Prana/EXP: {status.ExpPercent:0.00}% | Rate: {FormatExpRateForNotification(status)}{Environment.NewLine}" &
            $"Rupiahs: {If(status.RupiahsTotal >= 0, status.RupiahsTotal.ToString("N0"), "n/a")} | Rate: {FormatRupiahsRateForNotification(status)}{Environment.NewLine}" &
            $"Party: {FormatPartyForNotification(status)}"

        _lastStatsNotificationUtc = DateTime.UtcNow
        Dim destinationSummary As String = GetStatsNotificationDestinationSummary()
        Task.Run(
            Async Function()
                Dim sent As Boolean = Await SendPhoneNotificationToTopicAsync($"KathanaBot {intervalMinutes}m Stats", body, topic, 1, "default", "chart_with_upwards_trend,moneybag", GetDiscordStatsWebhookUrl(), "Discord stats webhook")
                If sent Then
                    AppendLogSafe($"{intervalMinutes}-minute stats sent via {destinationSummary}.")
                Else
                    AppendLogSafe($"{intervalMinutes}-minute stats alert failed via {destinationSummary}.")
                End If
            End Function)
    End Sub

    Private Async Function SendPhoneNotificationToTopicAsync(title As String, body As String, topic As String, Optional maxAttempts As Integer = 1, Optional priority As String = "urgent", Optional tags As String = "warning,gamepad", Optional discordWebhookUrl As String = Nothing, Optional discordDestinationLabel As String = "Discord webhook") As Task(Of Boolean)
        If GetNotificationProviderName() = NotificationProviderDiscord Then
            Return Await SendDiscordNotificationAsync(title, body, If(String.IsNullOrWhiteSpace(discordWebhookUrl), GetDiscordGlobalWebhookUrl(), discordWebhookUrl), discordDestinationLabel, maxAttempts)
        End If

        Dim cleanedTopic As String = If(topic, "").Trim()
        If cleanedTopic = "" Then
            Return False
        End If

        Dim attempts As Integer = Math.Max(1, maxAttempts)
        Dim url As String = $"https://ntfy.sh/{Uri.EscapeDataString(cleanedTopic)}"

        For attempt As Integer = 1 To attempts
            Try
                Using request As New HttpRequestMessage(HttpMethod.Post, url)
                    request.Content = New StringContent(body, Encoding.UTF8, "text/plain")
                    request.Headers.Add("Title", title)
                    request.Headers.Add("Priority", priority)
                    request.Headers.Add("Tags", tags)

                    Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                    If response.IsSuccessStatusCode Then
                        Return True
                    End If

                    AppendLogSafe($"Phone alert failed ({CInt(response.StatusCode)}) for topic '{cleanedTopic}' (attempt {attempt}/{attempts}).")
                End Using
            Catch ex As Exception
                AppendLogSafe($"Phone alert failed (attempt {attempt}/{attempts}) for topic '{cleanedTopic}': {ex.Message}")
            End Try

            If attempt < attempts Then
                Await Task.Delay(1500)
            End If
        Next

        Return False
    End Function

    Private Async Function SendDiscordNotificationAsync(title As String, body As String, webhookUrl As String, destinationLabel As String, Optional maxAttempts As Integer = 1) As Task(Of Boolean)
        Dim rawWebhookUrl As String = If(webhookUrl, "").Trim()
        If rawWebhookUrl = "" Then
            AppendLogSafe($"{destinationLabel} skipped: webhook URL is empty.")
            Return False
        End If
        If Not IsLikelyDiscordWebhookUrl(rawWebhookUrl) Then
            AppendLogSafe($"{destinationLabel} skipped: webhook URL format is invalid. Use the full Discord webhook URL from Server Settings -> Integrations -> Webhooks.")
            Return False
        End If

        Dim attempts As Integer = Math.Max(1, maxAttempts)
        Dim normalizedWebhookUrl As String = NormalizeDiscordWebhookUrl(rawWebhookUrl)
        Dim payloadText As String = $"{title}{Environment.NewLine}{body}".Trim()
        If payloadText.Length > 1900 Then
            payloadText = payloadText.Substring(0, 1897) & "..."
        End If

        For attempt As Integer = 1 To attempts
            Try
                Using request As New HttpRequestMessage(HttpMethod.Post, normalizedWebhookUrl)
                    Dim payload = New With {
                        .username = "KathanaBot",
                        .content = payloadText,
                        .allowed_mentions = New With {
                            .parse = Array.Empty(Of String)()
                        }
                    }
                    request.Content = New StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")

                    Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                    If response.IsSuccessStatusCode Then
                        Return True
                    End If

                    Dim responseText As String = ""
                    If response.Content IsNot Nothing Then
                        responseText = (Await response.Content.ReadAsStringAsync()).Trim()
                    End If
                    If responseText <> "" Then
                        AppendLogSafe($"{destinationLabel} failed ({CInt(response.StatusCode)}) (attempt {attempt}/{attempts}): {responseText}")
                    Else
                        AppendLogSafe($"{destinationLabel} failed ({CInt(response.StatusCode)}) (attempt {attempt}/{attempts}).")
                    End If
                End Using
            Catch ex As Exception
                AppendLogSafe($"{destinationLabel} failed (attempt {attempt}/{attempts}): {ex.Message}")
            End Try

            If attempt < attempts Then
                Await Task.Delay(1500)
            End If
        Next

        Return False
    End Function

    Private Async Function SendPhoneNotificationAsync(title As String, body As String, Optional maxAttempts As Integer = 1) As Task(Of Boolean)
        Dim sent As Boolean
        If GetNotificationProviderName() = NotificationProviderDiscord Then
            sent = Await SendDiscordNotificationAsync(title, body, GetDiscordGlobalWebhookUrl(), "Discord global webhook", maxAttempts)
        Else
            Dim topic As String = GetNtfyTopicName()
            sent = Await SendPhoneNotificationToTopicAsync(title, body, topic, maxAttempts)
            If sent Then
                AppendLogSafe($"Notification sent to ntfy topic '{topic}'.")
            End If
            Return sent
        End If
        If sent Then
            AppendLogSafe("Notification sent to Discord global webhook.")
        End If
        Return sent
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
        If String.Equals(If(control.Tag, "").ToString(), "lite-scope", StringComparison.OrdinalIgnoreCase) Then
            Return
        End If
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
        _fullEngine.Stop()
        _liteEngine.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
