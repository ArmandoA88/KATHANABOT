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
Imports System.Linq
Imports System.Drawing.Imaging
Imports System.Security.Cryptography
Imports Velopack
Imports Velopack.Sources

Public Class Form1
    Private Shared ReadOnly PrimaryKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0"}
    Private Shared ReadOnly FunctionKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
    Private Shared ReadOnly LitePrimarySkillKeys As String() = {"1", "2", "3", "4", "5", "6", "7", "8"}
    Private Shared ReadOnly LiteSecondarySkillKeys As String() = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
    Private Shared ReadOnly CustomCombatDefaultKeys As String() = {"F11", "F12", "F13"}
    Private Shared ReadOnly DefaultGameWindowTitle As String = "Kathana - The Reign of Shadow"
    Private Const PreferredProcessName As String = "KathanaGame"
    Private Shared ReadOnly LiteWindowSize As New Size(920, 660)
    Private Shared ReadOnly FullWindowSize As New Size(1450, 900)

    Private _edition As BotEdition = BotEdition.Full
    Private ReadOnly _fullEngine As New BotEngine()
    Private ReadOnly _liteEngine As New BotEngine()
    Private ReadOnly _uiTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _enterToggleTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _logFlushTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _rollingScreenshotTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _periodicScreenshotTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _discordShotTimer As New System.Windows.Forms.Timer()
    Private ReadOnly _logQueueSync As New Object()
    Private ReadOnly _logThrottleSync As New Object()
    Private ReadOnly _logQueue As New Queue(Of String)()
    Private _droppedLogLineCount As Integer = 0
    Private _lastLogTrimUtc As DateTime = DateTime.MinValue
    Private Const LogFlushIntervalMs As Integer = 250
    Private Const MaxPendingLogLines As Integer = 2000
    Private Const MaxLogFlushLines As Integer = 120
    Private Const MaxRealtimeLogChars As Integer = 160000
    Private Const TargetRealtimeLogChars As Integer = 120000
    Private Const MaxLogLineChars As Integer = 2000
    Private Const LogTrimIntervalSeconds As Integer = 5
    Private Const HighFrequencyLogMinIntervalMs As Integer = 1000
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
    Private _autoRelaunchTab As TabPage
    Private _autoLootTab As TabPage
    Private _levelingTab As TabPage
    Private _holdPlaceTab As TabPage
    Private _diagnosticsTab As TabPage
    Private _updateTab As TabPage
    Private Const HelpScopeAll As String = "all"
    Private Const HelpScopeLite As String = "lite"
    Private Const HelpScopeCombat As String = "combat"
    Private Const HelpScopeVision As String = "vision"
    Private Const HelpScopeAutoPot As String = "auto-pot"
    Private Const HelpScopeAutoLoot As String = "auto-loot"
    Private Const HelpScopeLeveling As String = "leveling"
    Private Const HelpScopeDiagnostics As String = "diagnostics"
    Private Const DefaultUpdateRepositoryUrl As String = "https://github.com/ArmandoA88/KATHANABOT"

    Private lblSelectedProcess As Label
    Private nudLoopMs As NumericUpDown
    Private nudRetargetMs As NumericUpDown
    Private nudForcedRetargetMs As NumericUpDown
    Private nudMobHpThreshold As NumericUpDown
    Private chkHighMaxHpSpecial As CheckBox
    Private nudHighMaxHpThreshold As NumericUpDown
    Private chkAvoidHighMaxHpTargets As CheckBox
    Private nudAvoidHighMaxHpThreshold As NumericUpDown
    Private chkEvadeDadati As CheckBox
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
    Private chkPeriodicScreenshots As CheckBox
    Private nudPeriodicScreenshotMinutes As NumericUpDown
    Private txtPeriodicScreenshotDirectory As TextBox
    Private btnBrowsePeriodicScreenshotDirectory As Button
    Private lblPeriodicScreenshotStatus As Label
    Private pnlWindowFrame As Panel
    Private btnPickLootRejectPoint As Button
    Private btnClearLootRejectPoint As Button
    Private lblLootRejectPoint As Label
    Private btnPickLootNamePickupPoint As Button
    Private btnClearLootNamePickupPoint As Button
    Private lblLootNamePickupPoint As Label
    Private btnPickArrowUnbundlePoint As Button
    Private btnRemoveArrowUnbundlePoint As Button
    Private btnClearArrowUnbundlePoints As Button
    Private chkArrowUnbundleOverlay As CheckBox
    Private lblArrowUnbundlePoints As Label

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
    Private chkMonsterWhitelistMode As CheckBox
    Private chkMonsterConfirmOnce As CheckBox
    Private chkLootPickup As CheckBox
    Private chkLootNameAutoPickup As CheckBox
    Private chkLootNamePickupRestoreCursor As CheckBox
    Private chkArrowUnbundleEnabled As CheckBox
    Private nudLootNamePickupOffsetX As NumericUpDown
    Private nudLootNamePickupOffsetY As NumericUpDown
    Private nudLootPickupSeconds As NumericUpDown
    Private nudLootNamePickupClickDelayMs As NumericUpDown
    Private nudLootNamePickupFPressCount As NumericUpDown
    Private nudLootNamePickupFPressGapMs As NumericUpDown
    Private nudLootNamePickupMouseHoldMs As NumericUpDown
    Private nudArrowUnbundleSeconds As NumericUpDown
    Private lstMonsterFilter As ListBox
    Private lstLootFilter As ListBox
    Private lstArrowUnbundlePoints As ListBox
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
    Private chkNavigationReturnToStart As CheckBox
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
    Private _holdPlaceAnchorSet As Boolean = False
    Private _updatingHoldPlacePreset As Boolean = False
    Private chkHoldPlaceEnabled As CheckBox
    Private cboHoldPlaceRestrictiveness As ComboBox
    Private nudHoldPlaceTargetX As NumericUpDown
    Private nudHoldPlaceTargetY As NumericUpDown
    Private nudHoldPlaceRadius As NumericUpDown
    Private nudHoldPlaceMoveBurstMs As NumericUpDown
    Private nudHoldPlaceCorrectionMs As NumericUpDown
    Private chkHoldPlacePostFightReturn As CheckBox
    Private chkHoldPlaceCombatSafe As CheckBox
    Private nudHoldPlaceEmergencyLeash As NumericUpDown
    Private chkHoldPlaceDirectionLearning As CheckBox
    Private btnHoldPlaceUseCurrent As Button
    Private btnHoldPlaceOverlay As Button
    Private btnHoldPlaceOpenOcrCrops As Button
    Private lblHoldPlaceStatus As Label
    Private lblHoldPlaceCurrent As Label
    Private lblHoldPlaceTarget As Label
    Private txtHoldPlaceCoordinateLog As TextBox

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
    Private btnVisionLootScanner As Button
    Private btnLootScanner As Button
    Private tblNotificationSettings As TableLayoutPanel
    Private cboNotificationProvider As ComboBox
    Private lblDiscordGlobalWebhook As Label
    Private lblDiscordItemWebhook As Label
    Private lblDiscordStatsWebhook As Label
    Private lblDiscordShotBotToken As Label
    Private lblDiscordShotChannelId As Label
    Private txtDiscordGlobalWebhookUrl As TextBox
    Private txtDiscordItemWebhookUrl As TextBox
    Private txtDiscordStatsWebhookUrl As TextBox
    Private txtDiscordShotBotToken As TextBox
    Private txtDiscordShotChannelId As TextBox
    Private lblNtfyGlobalTopic As Label
    Private txtItemNtfyTopic As TextBox
    Private lblNtfyItemTopic As Label
    Private txtStatsNtfyTopic As TextBox
    Private lblNtfyStatsTopic As Label
    Private chkAutoRelaunchGame As CheckBox
    Private txtAutoRelaunchExePath As TextBox
    Private btnBrowseAutoRelaunchExe As Button
    Private nudAutoRelaunchDelaySeconds As NumericUpDown
    Private dgvAutoRelaunchClicks As DataGridView
    Private btnAutoRelaunchUseCursor As Button
    Private btnAutoRelaunchClearClicks As Button
    Private chkAutoRelaunchClickOverlay As CheckBox
    Private btnHelp As Button
    Private nudPartyAskSeconds As NumericUpDown
    Private txtPartyAskText As TextBox
    Private rtbLog As RichTextBox
    Private dgvKeySummary As DataGridView
    Private lblKeySummaryInfo As Label
    Private txtDiagnostics As TextBox
    Private pnlHealthBanner As Panel
    Private chkAdaptivePerformance As CheckBox
    Private chkPixelChangeGate As CheckBox
    Private nudAdaptiveSlowMinMs As NumericUpDown
    Private nudAdaptiveSlowMultiplier As NumericUpDown
    Private nudAdaptiveRecoveryMultiplier As NumericUpDown
    Private nudAdaptiveSlowConfirm As NumericUpDown
    Private nudAdaptiveRecoveryConfirm As NumericUpDown
    Private cboCaptureBackend As ComboBox
    Private btnRunBenchmark As Button
    Private btnExportDiagnostics As Button
    Private nudFullFrameScanMs As NumericUpDown
    Private nudLootScannerSeconds As NumericUpDown
    Private nudMapScanMs As NumericUpDown
    Private nudPartyScanMs As NumericUpDown
    Private nudMobNameScanMs As NumericUpDown
    Private chkLogCombat As CheckBox
    Private chkLogLoot As CheckBox
    Private chkLogOcrVision As CheckBox
    Private chkLogNavigation As CheckBox
    Private chkLogWarnings As CheckBox
    Private chkLogMisc As CheckBox
    Private dgvLootHistory As DataGridView

    Private lblUpdateCurrentVersion As Label
    Private lblUpdateInstallMode As Label
    Private txtUpdateRepositoryUrl As TextBox
    Private chkUpdateCheckAtStartup As CheckBox
    Private chkUpdateIncludePrereleases As CheckBox
    Private btnCheckForUpdates As Button
    Private btnUpdateAndRestart As Button
    Private btnOpenUpdateReleases As Button
    Private progressUpdateDownload As ProgressBar
    Private lblUpdateStatus As Label
    Private txtUpdateDetails As TextBox

    Private nudAutoPotHp As NumericUpDown
    Private nudAutoPotMp As NumericUpDown
    Private nudStuckTargetMs As NumericUpDown
    Private nudStuckNoProgressRetargetMs As NumericUpDown
    Private nudLootNameMatchThreshold As NumericUpDown
    Private nudAlarmVolume As NumericUpDown
    Private nudStatsNtfyIntervalMinutes As NumericUpDown
    Private txtNtfyTopic As TextBox
    Private chkCustomBarColors As CheckBox
    Private btnHpBarColor As Button
    Private btnMpBarColor As Button
    Private btnPickHpBarColor As Button
    Private btnPickMpBarColor As Button
    Private nudBarColorTolerance As NumericUpDown

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
    Private _autoRelaunchClickOverlayForm As AutoRelaunchClickOverlayForm
    Private _arrowUnbundleOverlayForm As AutoRelaunchClickOverlayForm
    Private _chatTranslationOverlayForm As ChatTranslationOverlayForm
    Private _inGameBotToggleForm As InGameBotToggleForm
    Private _inGameBotToggleX As Integer = -1
    Private _inGameBotToggleY As Integer = 10
    Private _inGameBotToggleWidth As Integer = 104
    Private _inGameBotToggleHeight As Integer = 38
    Private _inGameBotToggleEdition As BotEdition = BotEdition.Full
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
    Private _deadHpFirstSeenUtc As DateTime = DateTime.MinValue
    Private _windowMissingConfirmCount As Integer = 0
    Private _windowMissingFirstSeenUtc As DateTime = DateTime.MinValue
    Private _deathNotificationLatched As Boolean = False
    Private _windowMissingNotificationLatched As Boolean = False
    Private _gameDisconnectedNotificationLatched As Boolean = False
    Private _autoRelaunchPending As Boolean = False
    Private _lastAutoRelaunchAttemptUtc As DateTime = DateTime.MinValue
    Private _ctrlShiftWasDown As Boolean = False
    Private _isPickingLootRejectPoint As Boolean = False
    Private _isPickingLootNamePickupPoint As Boolean = False
    Private _isPickingArrowUnbundlePoint As Boolean = False
    Private _arrowUnbundleLeftMouseWasDown As Boolean = False
    Private _isPickingAutoRelaunchClick As Boolean = False
    Private _autoRelaunchRightMouseWasDown As Boolean = False
    Private _pendingAutoRelaunchClickRowIndex As Integer = -1
    Private _autoRelaunchDragRowIndex As Integer = -1
    Private _autoRelaunchDragStartPoint As System.Drawing.Point = System.Drawing.Point.Empty
    Private _autoRelaunchDragInProgress As Boolean = False
    Private _arrowUnbundleUiSyncInProgress As Boolean = False
    Private _lootRejectPointX As Integer = -1
    Private _lootRejectPointY As Integer = -1
    Private _lootNamePickupPointX As Integer = -1
    Private _lootNamePickupPointY As Integer = -1
    Private ReadOnly _arrowUnbundlePoints As New List(Of LootScanPoint)()
    Private _liteAutoPotHpPointX As Integer = -1
    Private _liteAutoPotHpPointY As Integer = -1
    Private _liteAutoPotHpColorEnabled As Boolean = False
    Private _liteAutoPotHpColorArgb As Integer = 0
    Private _liteAutoPotMpPointX As Integer = -1
    Private _liteAutoPotMpPointY As Integer = -1
    Private _liteAutoPotMpColorEnabled As Boolean = False
    Private _liteAutoPotMpColorArgb As Integer = 0
    Private _pendingLitePointCapture As LitePointCaptureKind = LitePointCaptureKind.None
    Private _liteRightMouseWasDown As Boolean = False
    Private _customBarColorsEnabled As Boolean = False
    Private _hpBarColor As Color = Color.FromArgb(BotConfig.DefaultHpBarColorArgb())
    Private _mpBarColor As Color = Color.FromArgb(BotConfig.DefaultMpBarColorArgb())
    Private _barColorTolerance As Integer = BotConfig.DefaultBarColorTolerance
    Private _pendingBarColorPick As BarColorPickKind = BarColorPickKind.None
    Private _barColorSyncInProgress As Boolean = False
    Private _themeSnapshotCaptured As Boolean = False
    Private _lastUiTintActive As Boolean = False
    Private _lastUiTintColorArgb As Integer = Integer.MinValue
    Private _lastUiTintBlend As Double = -1.0
    Private _fullStatus As New BotStatus()
    Private _liteStatus As New BotStatus()
    Private Const HpZeroAlarmGraceMs As Integer = 60000
    Private Const DeadZeroThreshold As Double = 0.1
    Private Const DeadRecoverThreshold As Double = 2.0
    Private Const CriticalAlertConfirmMs As Integer = 60000
    Private Const CriticalAlertConfirmFrames As Integer = 100
    Private Const DeathNotificationRetryCount As Integer = 3
    Private Const StartupNotificationWarmupSeconds As Integer = 20
    Private Const NotificationProviderNtfy As String = "ntfy"
    Private Const NotificationProviderDiscord As String = "discord"
    Private Shared ReadOnly StatusAliveColor As Color = Color.FromArgb(0, 230, 65)
    Private Shared ReadOnly StatusStoppedOrDeadColor As Color = Color.FromArgb(235, 0, 0)
    Private Shared ReadOnly BotRunningColor As Color = Color.FromArgb(0, 170, 70)
    Private Const DefaultNtfyTopicName As String = "Katana12345"
    Private Const DefaultPartyAskCommand As String = "add"
    Private Const DefaultLootNameMatchThresholdPercent As Integer = 80
    Private Const DefaultMapOpenKey As String = "M"
    Private Const DefaultLevelingMinExpPerHour As Decimal = 0.15D
    Private Const RollingScreenshotIntervalMs As Integer = 30000
    Private Const RollingScreenshotRetainCount As Integer = 10
    Private Const DiscordShotPollIntervalMs As Integer = 5000
    Private Shared ReadOnly NtfyClient As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(7)}
    Private Shared ReadOnly PersistDirectoryPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotControlPanel")
    Private Shared ReadOnly PersistFilePath As String = Path.Combine(PersistDirectoryPath, "user_lists.json")
    Private Shared ReadOnly RollingScreenshotDirectoryPath As String = Path.Combine(PersistDirectoryPath, "screenshots")
    Private Shared ReadOnly DefaultPeriodicScreenshotDirectoryPath As String = ResolveDefaultPeriodicScreenshotDirectory()
    Private ReadOnly _baseBackColors As New Dictionary(Of Control, Color)()
    Private ReadOnly _gridThemeSnapshots As New Dictionary(Of DataGridView, GridThemeSnapshot)()
    Private ReadOnly _keyActionEvents As New List(Of KeyActionEvent)()
    Private ReadOnly _keyActionEventsSync As New Object()
    Private Const MaxKeyActionEvents As Integer = 30000
    Private ReadOnly _lootHistoryEvents As New List(Of LootHistoryEvent)()
    Private ReadOnly _lootHistoryEventsSync As New Object()
    Private Const MaxLootHistoryEvents As Integer = 500
    Private _lootHistoryVersion As Long = 0
    Private _lastLootHistoryRenderedVersion As Long = -1
    Private _lastEngineKeyActionLogUtc As DateTime = DateTime.MinValue
    Private _suppressedEngineKeyActionLogCount As Integer = 0
    Private _lastStateLogUtc As DateTime = DateTime.MinValue
    Private _suppressedStateLogCount As Integer = 0
    Private _lastNoAttackReasonLogUtc As DateTime = DateTime.MinValue
    Private _suppressedNoAttackReasonLogCount As Integer = 0
    Private _totalDroppedLogLineCount As Long = 0
    Private _lastLogFlushBatchCount As Integer = 0
    Private _lastLogFlushAt As DateTime = DateTime.MinValue
    Private _rollingScreenshotInProgress As Boolean = False
    Private _rollingScreenshotSaveCount As Integer = 0
    Private _lastRollingScreenshotErrorLogUtc As DateTime = DateTime.MinValue
    Private _periodicScreenshotInProgress As Boolean = False
    Private _lastPeriodicScreenshotErrorLogUtc As DateTime = DateTime.MinValue
    Private _periodicScreenshotSettingsLoading As Boolean = False
    Private _discordShotPollInProgress As Boolean = False
    Private _discordShotInitialized As Boolean = False
    Private _lastDiscordShotMessageId As String = ""
    Private _lastDiscordShotErrorLogUtc As DateTime = DateTime.MinValue
    Private _logFilterCombatEnabled As Boolean = True
    Private _logFilterLootEnabled As Boolean = True
    Private _logFilterOcrVisionEnabled As Boolean = True
    Private _logFilterNavigationEnabled As Boolean = True
    Private _logFilterWarningsEnabled As Boolean = True
    Private _logFilterMiscEnabled As Boolean = True
    Private ReadOnly _chatTranslator As New TranslationService()
    Private ReadOnly _chatTranslationLock As New SemaphoreSlim(1, 1)
    Private ReadOnly _chatOverlayEntries As New List(Of ChatOverlayLine)()
    Private _chatScreenGeneration As Integer = 0
    Private _lastChatOcrText As String = ""
    Private _lastChatTargetLanguage As String = ""
    Private _updateManager As UpdateManager = Nothing
    Private _pendingUpdateInfo As UpdateInfo = Nothing
    Private _pendingStandaloneUpdate As StandaloneUpdateRelease = Nothing
    Private _updateOperationInProgress As Boolean = False
    Private _updateCancellation As CancellationTokenSource = Nothing
    Private _updateSettingsLoading As Boolean = False
    Private _taskbarList As ITaskbarList3 = Nothing
    Private _taskbarUnavailable As Boolean = False
    Private _uiTimingCount As Long = 0
    Private _uiTimingAverageMs As Double = 0
    Private _uiTimingMaxMs As Double = 0
    Private ReadOnly _diagnosticsHistory As New Queue(Of String)()
    Private Const DiagnosticsHistoryLimit As Integer = 600

    Private NotInheritable Class StandaloneUpdateRelease
        Public Property Version As Version
        Public Property VersionText As String = ""
        Public Property FileName As String = ""
        Public Property DownloadUrl As String = ""
        Public Property Sha256Url As String = ""
        Public Property Size As Long
        Public Property ReleaseUrl As String = ""
        Public Property ReleaseNotes As String = ""
    End Class

    Private Enum TaskbarProgressState
        NoProgress = 0
        Indeterminate = 1
        Normal = 2
        [Error] = 4
        Paused = 8
    End Enum

    <ComImport>
    <Guid("56FDF344-FD6D-11D0-958A-006097C9A090")>
    <ClassInterface(ClassInterfaceType.None)>
    Private Class TaskbarList
    End Class

    <ComImport>
    <Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface ITaskbarList3
        Sub HrInit()
        Sub AddTab(hwnd As IntPtr)
        Sub DeleteTab(hwnd As IntPtr)
        Sub ActivateTab(hwnd As IntPtr)
        Sub SetActiveAlt(hwnd As IntPtr)
        Sub MarkFullscreenWindow(hwnd As IntPtr, <MarshalAs(UnmanagedType.Bool)> fullscreen As Boolean)
        Sub SetProgressValue(hwnd As IntPtr, completed As ULong, total As ULong)
        Sub SetProgressState(hwnd As IntPtr, state As TaskbarProgressState)
    End Interface

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

    Private Class LootHistoryEvent
        Public Property TimestampLocal As DateTime
        Public Property Edition As BotEdition
        Public Property ItemName As String = ""
        Public Property ActionText As String = ""
        Public Property DetailText As String = ""
    End Class

    Private Class ProcessWindowEntry
        Public Property ProcessId As Integer
        Public Property ProcessName As String = ""
        Public Property WindowTitle As String = ""
        Public Property MainWindowHandle As IntPtr = IntPtr.Zero

        Public Overrides Function ToString() As String
            Return $"{WindowTitle} - {ProcessName}"
        End Function
    End Class

    Private Enum LitePointCaptureKind
        None
        Hp
        Mp
    End Enum

    Private Enum BarColorPickKind
        None
        Hp
        Mp
    End Enum

    Private Class PersistedAppState
        Public Property WindowTitle As String = DefaultGameWindowTitle
        Public Property PeriodicScreenshotsEnabled As Boolean = False
        Public Property PeriodicScreenshotIntervalMinutes As Decimal = 15D
        Public Property PeriodicScreenshotDirectory As String = ""
        Public Property InGameBotToggleX As Integer = -1
        Public Property InGameBotToggleY As Integer = 10
        Public Property InGameBotToggleWidth As Integer = 104
        Public Property InGameBotToggleHeight As Integer = 38
        Public Property UpdateRepositoryUrl As String = DefaultUpdateRepositoryUrl
        Public Property UpdateCheckAtStartup As Boolean = True
        Public Property UpdateIncludePrereleases As Boolean = False
        Public Property Full As PersistedListState = New PersistedListState()
        Public Property Lite As PersistedLiteState = New PersistedLiteState()
    End Class

    Private Class PersistedListState
        Public Property MonsterFilterEnabled As Boolean = True
        Public Property MonsterFilterMode As String = "blacklist"
        Public Property MonsterFilterConfirmReads As Integer = 2
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
        Public Property ArrowUnbundleEnabled As Boolean = False
        Public Property ArrowUnbundleSeconds As Decimal = 60D
        Public Property ArrowUnbundleOverlayEnabled As Boolean = False
        Public Property ArrowUnbundlePoints As List(Of LootScanPoint) = New List(Of LootScanPoint)()
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
        Public Property DiscordShotBotToken As String = ""
        Public Property DiscordShotChannelId As String = ""
        Public Property ItemNtfyTopic As String = "add"
        Public Property NtfyTopic As String = ""
        Public Property StatsNtfyTopic As String = ""
        Public Property StatsNtfyIntervalMinutes As Decimal = 30D
        Public Property AutoRelaunchGameEnabled As Boolean = False
        Public Property AutoRelaunchGameExePath As String = ""
        Public Property AutoRelaunchDelaySeconds As Decimal = 5D
        Public Property AutoRelaunchClickOverlayEnabled As Boolean = False
        Public Property AutoRelaunchClicks As List(Of PersistedAutoRelaunchClick) = New List(Of PersistedAutoRelaunchClick)()
        Public Property AutoPotHpPercent As Decimal = 80D
        Public Property AutoPotMpPercent As Decimal = 35D
        Public Property AlarmVolumePercent As Integer = 85
        Public Property SavedConfig As BotConfig = Nothing
        Public Property MonsterNames As List(Of String) = New List(Of String)()
        Public Property LootNames As List(Of String) = New List(Of String)()
        Public Property CombatActions As List(Of PersistedCombatAction) = New List(Of PersistedCombatAction)()
    End Class

    Private Class PersistedAutoRelaunchClick
        Public Property Enabled As Boolean = False
        Public Property X As Integer = 0
        Public Property Y As Integer = 0
        Public Property DelaySeconds As Decimal = 5D
        Public Property Description As String = ""
    End Class

    Private Class PersistedLiteState
        Public Property AutoPotsEnabled As Boolean = False
        Public Property HpPointEnabled As Boolean = False
        Public Property HpPointX As Integer = -1
        Public Property HpPointY As Integer = -1
        Public Property HpPointColorEnabled As Boolean = False
        Public Property HpPointColorArgb As Integer = 0
        Public Property MpPointEnabled As Boolean = False
        Public Property MpPointX As Integer = -1
        Public Property MpPointY As Integer = -1
        Public Property MpPointColorEnabled As Boolean = False
        Public Property MpPointColorArgb As Integer = 0
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
        ApplyApplicationIcon()
        BuildUi()
        AddHandler _periodicScreenshotTimer.Tick, AddressOf PeriodicScreenshotTimerTick
        SeedDefaults()
        LoadPersistedListState()
        ForceLevelingAgentOffForStartup()
        SetupLiveConfigBindings()
        RefreshUpdateInstallMode()
        SetAutoRelaunchClickOverlayVisible(chkAutoRelaunchClickOverlay IsNot Nothing AndAlso chkAutoRelaunchClickOverlay.Checked)
        SetArrowUnbundleOverlayVisible(chkArrowUnbundleOverlay IsNot Nothing AndAlso chkArrowUnbundleOverlay.Checked)
        ApplyDarkTheme(Me)
        UpdateBarColorUi()
        CaptureThemeSnapshot(Me)
        _themeSnapshotCaptured = True

        AddHandler _fullEngine.StatusUpdated, Sub(status As BotStatus) OnEngineStatusUpdated(BotEdition.Full, status)
        AddHandler _liteEngine.StatusUpdated, Sub(status As BotStatus) OnEngineStatusUpdated(BotEdition.Lite, status)
        AddHandler _fullEngine.LogLine, Sub(line As String) OnEngineLogLine(BotEdition.Full, line)
        AddHandler _liteEngine.LogLine, Sub(line As String) OnEngineLogLine(BotEdition.Lite, line)
        InitializeInGameBotToggle()

        _uiTimer.Interval = 1000
        AddHandler _uiTimer.Tick, AddressOf UiTimerTick
        _uiTimer.Start()

        _enterToggleTimer.Interval = 45
        AddHandler _enterToggleTimer.Tick, AddressOf EnterToggleTimerTick
        _enterToggleTimer.Start()

        _logFlushTimer.Interval = LogFlushIntervalMs
        AddHandler _logFlushTimer.Tick, AddressOf LogFlushTimerTick
        _logFlushTimer.Start()

        _rollingScreenshotTimer.Interval = RollingScreenshotIntervalMs
        AddHandler _rollingScreenshotTimer.Tick, AddressOf RollingScreenshotTimerTick
        _rollingScreenshotTimer.Start()

        ConfigurePeriodicScreenshotTimer()

        _discordShotTimer.Interval = DiscordShotPollIntervalMs
        AddHandler _discordShotTimer.Tick, AddressOf DiscordShotTimerTick
        _discordShotTimer.Start()

        UpdateEditionUiState(False)
        PushLiveConfig()
    End Sub

    Private Sub ForceLevelingAgentOffForStartup()
        If chkLevelingAgent IsNot Nothing Then
            chkLevelingAgent.Checked = False
        End If
    End Sub

    Private Sub ApplyApplicationIcon()
        Try
            Dim appIcon As Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            If appIcon IsNot Nothing Then
                Icon = DirectCast(appIcon.Clone(), Icon)
                ShowIcon = True
                appIcon.Dispose()
            End If
        Catch
        End Try
    End Sub

    Private Sub SetupLiveConfigBindings()
        If txtUpdateRepositoryUrl IsNot Nothing Then
            AddHandler txtUpdateRepositoryUrl.TextChanged, AddressOf UpdateSettingsChanged
        End If
        If chkUpdateCheckAtStartup IsNot Nothing Then
            AddHandler chkUpdateCheckAtStartup.CheckedChanged, AddressOf UpdateSettingsChanged
        End If
        If chkUpdateIncludePrereleases IsNot Nothing Then
            AddHandler chkUpdateIncludePrereleases.CheckedChanged, AddressOf UpdateSettingsChanged
        End If
        If chkPeriodicScreenshots IsNot Nothing Then
            AddHandler chkPeriodicScreenshots.CheckedChanged, AddressOf PeriodicScreenshotSettingsChanged
        End If
        If nudPeriodicScreenshotMinutes IsNot Nothing Then
            AddHandler nudPeriodicScreenshotMinutes.ValueChanged, AddressOf PeriodicScreenshotSettingsChanged
        End If
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
        If txtDiscordShotBotToken IsNot Nothing Then
            AddHandler txtDiscordShotBotToken.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If txtDiscordShotChannelId IsNot Nothing Then
            AddHandler txtDiscordShotChannelId.TextChanged, AddressOf PersistListSettingsChanged
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
        If chkAutoRelaunchGame IsNot Nothing Then
            AddHandler chkAutoRelaunchGame.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If txtAutoRelaunchExePath IsNot Nothing Then
            AddHandler txtAutoRelaunchExePath.TextChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAutoRelaunchDelaySeconds IsNot Nothing Then
            AddHandler nudAutoRelaunchDelaySeconds.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkAutoRelaunchClickOverlay IsNot Nothing Then
            AddHandler chkAutoRelaunchClickOverlay.CheckedChanged, AddressOf AutoRelaunchClickOverlayChanged
            AddHandler chkAutoRelaunchClickOverlay.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If dgvAutoRelaunchClicks IsNot Nothing Then
            AddHandler dgvAutoRelaunchClicks.CellValueChanged, AddressOf PersistListSettingsChanged
            AddHandler dgvAutoRelaunchClicks.CellEndEdit, AddressOf PersistListSettingsChanged
            AddHandler dgvAutoRelaunchClicks.CurrentCellDirtyStateChanged, AddressOf AutoRelaunchClicksCurrentCellDirtyStateChanged
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
        If chkAdaptivePerformance IsNot Nothing Then
            AddHandler chkAdaptivePerformance.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkAdaptivePerformance.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkPixelChangeGate IsNot Nothing Then
            AddHandler chkPixelChangeGate.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkPixelChangeGate.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAdaptiveSlowMinMs IsNot Nothing Then
            AddHandler nudAdaptiveSlowMinMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudAdaptiveSlowMinMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAdaptiveSlowMultiplier IsNot Nothing Then
            AddHandler nudAdaptiveSlowMultiplier.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudAdaptiveSlowMultiplier.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAdaptiveRecoveryMultiplier IsNot Nothing Then
            AddHandler nudAdaptiveRecoveryMultiplier.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudAdaptiveRecoveryMultiplier.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAdaptiveSlowConfirm IsNot Nothing Then
            AddHandler nudAdaptiveSlowConfirm.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudAdaptiveSlowConfirm.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudAdaptiveRecoveryConfirm IsNot Nothing Then
            AddHandler nudAdaptiveRecoveryConfirm.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudAdaptiveRecoveryConfirm.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If cboCaptureBackend IsNot Nothing Then
            AddHandler cboCaptureBackend.SelectedIndexChanged, AddressOf LiveConfigChanged
            AddHandler cboCaptureBackend.SelectedIndexChanged, AddressOf PersistListSettingsChanged
        End If
        If btnRunBenchmark IsNot Nothing Then
            AddHandler btnRunBenchmark.Click, AddressOf RunBenchmarkClicked
        End If
        If btnExportDiagnostics IsNot Nothing Then
            AddHandler btnExportDiagnostics.Click, AddressOf ExportDiagnosticsClicked
        End If
        If nudFullFrameScanMs IsNot Nothing Then
            AddHandler nudFullFrameScanMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudFullFrameScanMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudLootScannerSeconds IsNot Nothing Then
            AddHandler nudLootScannerSeconds.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudLootScannerSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudMapScanMs IsNot Nothing Then
            AddHandler nudMapScanMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudMapScanMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudPartyScanMs IsNot Nothing Then
            AddHandler nudPartyScanMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudPartyScanMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudMobNameScanMs IsNot Nothing Then
            AddHandler nudMobNameScanMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudMobNameScanMs.ValueChanged, AddressOf PersistListSettingsChanged
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
        If chkEvadeDadati IsNot Nothing Then
            AddHandler chkEvadeDadati.CheckedChanged, AddressOf LiveConfigChanged
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
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf MonsterFilterOptionChanged
        AddHandler chkMonsterFilter.CheckedChanged, AddressOf LiveConfigChanged
        If chkMonsterWhitelistMode IsNot Nothing Then
            AddHandler chkMonsterWhitelistMode.CheckedChanged, AddressOf MonsterFilterOptionChanged
            AddHandler chkMonsterWhitelistMode.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If chkMonsterConfirmOnce IsNot Nothing Then
            AddHandler chkMonsterConfirmOnce.CheckedChanged, AddressOf MonsterFilterOptionChanged
            AddHandler chkMonsterConfirmOnce.CheckedChanged, AddressOf LiveConfigChanged
        End If
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
        If chkArrowUnbundleEnabled IsNot Nothing Then
            AddHandler chkArrowUnbundleEnabled.CheckedChanged, AddressOf LiveConfigChanged
        End If
        If nudArrowUnbundleSeconds IsNot Nothing Then
            AddHandler nudArrowUnbundleSeconds.ValueChanged, AddressOf LiveConfigChanged
        End If
        If chkArrowUnbundleOverlay IsNot Nothing Then
            AddHandler chkArrowUnbundleOverlay.CheckedChanged, AddressOf ArrowUnbundleOverlayChanged
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
        If chkNavigationReturnToStart IsNot Nothing Then
            AddHandler chkNavigationReturnToStart.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkNavigationReturnToStart.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkHoldPlaceEnabled IsNot Nothing Then
            AddHandler chkHoldPlaceEnabled.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkHoldPlaceEnabled.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If cboHoldPlaceRestrictiveness IsNot Nothing Then
            AddHandler cboHoldPlaceRestrictiveness.SelectedIndexChanged, AddressOf HoldPlaceRestrictivenessChanged
            AddHandler cboHoldPlaceRestrictiveness.SelectedIndexChanged, AddressOf LiveConfigChanged
            AddHandler cboHoldPlaceRestrictiveness.SelectedIndexChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceTargetX IsNot Nothing Then
            AddHandler nudHoldPlaceTargetX.ValueChanged, AddressOf HoldPlaceAnchorValueChanged
            AddHandler nudHoldPlaceTargetX.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceTargetX.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceTargetY IsNot Nothing Then
            AddHandler nudHoldPlaceTargetY.ValueChanged, AddressOf HoldPlaceAnchorValueChanged
            AddHandler nudHoldPlaceTargetY.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceTargetY.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceRadius IsNot Nothing Then
            AddHandler nudHoldPlaceRadius.ValueChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler nudHoldPlaceRadius.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceRadius.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceMoveBurstMs IsNot Nothing Then
            AddHandler nudHoldPlaceMoveBurstMs.ValueChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler nudHoldPlaceMoveBurstMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceMoveBurstMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceCorrectionMs IsNot Nothing Then
            AddHandler nudHoldPlaceCorrectionMs.ValueChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler nudHoldPlaceCorrectionMs.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceCorrectionMs.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkHoldPlacePostFightReturn IsNot Nothing Then
            AddHandler chkHoldPlacePostFightReturn.CheckedChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler chkHoldPlacePostFightReturn.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkHoldPlacePostFightReturn.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkHoldPlaceCombatSafe IsNot Nothing Then
            AddHandler chkHoldPlaceCombatSafe.CheckedChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler chkHoldPlaceCombatSafe.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkHoldPlaceCombatSafe.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudHoldPlaceEmergencyLeash IsNot Nothing Then
            AddHandler nudHoldPlaceEmergencyLeash.ValueChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler nudHoldPlaceEmergencyLeash.ValueChanged, AddressOf LiveConfigChanged
            AddHandler nudHoldPlaceEmergencyLeash.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkHoldPlaceDirectionLearning IsNot Nothing Then
            AddHandler chkHoldPlaceDirectionLearning.CheckedChanged, AddressOf HoldPlaceCustomValueChanged
            AddHandler chkHoldPlaceDirectionLearning.CheckedChanged, AddressOf LiveConfigChanged
            AddHandler chkHoldPlaceDirectionLearning.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If btnHoldPlaceUseCurrent IsNot Nothing Then
            AddHandler btnHoldPlaceUseCurrent.Click, AddressOf HoldPlaceUseCurrentClicked
        End If
        If btnHoldPlaceOverlay IsNot Nothing Then
            AddHandler btnHoldPlaceOverlay.Click, AddressOf ToggleOverlayClicked
        End If
        If btnHoldPlaceOpenOcrCrops IsNot Nothing Then
            AddHandler btnHoldPlaceOpenOcrCrops.Click, AddressOf HoldPlaceOpenOcrCropsClicked
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
        If chkMonsterWhitelistMode IsNot Nothing Then
            AddHandler chkMonsterWhitelistMode.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If chkMonsterConfirmOnce IsNot Nothing Then
            AddHandler chkMonsterConfirmOnce.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
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
        If chkArrowUnbundleEnabled IsNot Nothing Then
            AddHandler chkArrowUnbundleEnabled.CheckedChanged, AddressOf PersistListSettingsChanged
        End If
        If nudArrowUnbundleSeconds IsNot Nothing Then
            AddHandler nudArrowUnbundleSeconds.ValueChanged, AddressOf PersistListSettingsChanged
        End If
        If chkArrowUnbundleOverlay IsNot Nothing Then
            AddHandler chkArrowUnbundleOverlay.CheckedChanged, AddressOf PersistListSettingsChanged
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
        If chkEvadeDadati IsNot Nothing Then
            AddHandler chkEvadeDadati.CheckedChanged, AddressOf PersistListSettingsChanged
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
        AddHandler dgvCombat.CurrentCellDirtyStateChanged, AddressOf CombatGridCurrentCellDirtyStateChanged
        AddHandler dgvCombat.EditingControlShowing, AddressOf CombatGridEditingControlShowing
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

    Private Sub BarColorSettingsChanged(_sender As Object, _e As EventArgs)
        If _barColorSyncInProgress Then
            Return
        End If

        _customBarColorsEnabled = (chkCustomBarColors IsNot Nothing AndAlso chkCustomBarColors.Checked)
        If nudBarColorTolerance IsNot Nothing Then
            _barColorTolerance = CInt(nudBarColorTolerance.Value)
        End If

        UpdateBarColorUi()
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Sub ChooseHpBarColorClicked(sender As Object, e As EventArgs)
        ChooseBarColor(BarColorPickKind.Hp)
    End Sub

    Private Sub ChooseMpBarColorClicked(sender As Object, e As EventArgs)
        ChooseBarColor(BarColorPickKind.Mp)
    End Sub

    Private Sub PickHpBarColorFromSnapshotClicked(sender As Object, e As EventArgs)
        BeginBarColorSnapshotPick(BarColorPickKind.Hp)
    End Sub

    Private Sub PickMpBarColorFromSnapshotClicked(sender As Object, e As EventArgs)
        BeginBarColorSnapshotPick(BarColorPickKind.Mp)
    End Sub

    Private Sub ChooseBarColor(kind As BarColorPickKind)
        If kind = BarColorPickKind.None Then
            Return
        End If

        Using dialog As New ColorDialog()
            dialog.FullOpen = True
            dialog.Color = If(kind = BarColorPickKind.Hp, _hpBarColor, _mpBarColor)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                SetBarColor(kind, dialog.Color, "manual picker")
            End If
        End Using
    End Sub

    Private Sub BeginBarColorSnapshotPick(kind As BarColorPickKind)
        If kind = BarColorPickKind.None Then
            Return
        End If

        _pendingBarColorPick = kind
        _isPickingLootRejectPoint = False
        _isPickingLootNamePickupPoint = False
        _isPickingArrowUnbundlePoint = False
        UpdateLootRejectPointUi()
        UpdateLootNamePickupPointUi()
        UpdateArrowUnbundleUi()
        UpdateBarColorUi()
        FocusVisionSnapshotForPick(If(kind = BarColorPickKind.Hp, "HP bar color", "MP bar color"))
    End Sub

    Private Sub SetBarColor(kind As BarColorPickKind, color As Color, source As String)
        If kind = BarColorPickKind.None Then
            Return
        End If

        Dim normalized As Color = Color.FromArgb(255, color.R, color.G, color.B)
        If kind = BarColorPickKind.Hp Then
            _hpBarColor = normalized
        Else
            _mpBarColor = normalized
        End If

        _customBarColorsEnabled = True
        If chkCustomBarColors IsNot Nothing AndAlso Not chkCustomBarColors.Checked Then
            chkCustomBarColors.Checked = True
        End If

        UpdateBarColorUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog($"{If(kind = BarColorPickKind.Hp, "HP", "MP")} bar color set to {FormatBarColor(normalized)} from {source}.")
    End Sub

    Private Sub UpdateBarColorUi()
        If chkCustomBarColors IsNot Nothing AndAlso chkCustomBarColors.Checked <> _customBarColorsEnabled Then
            chkCustomBarColors.Checked = _customBarColorsEnabled
        End If

        If nudBarColorTolerance IsNot Nothing Then
            Dim bounded As Decimal = Math.Max(nudBarColorTolerance.Minimum, Math.Min(nudBarColorTolerance.Maximum, CDec(_barColorTolerance)))
            If nudBarColorTolerance.Value <> bounded Then
                nudBarColorTolerance.Value = bounded
            End If
            nudBarColorTolerance.Enabled = _customBarColorsEnabled
        End If

        UpdateBarColorButton(btnHpBarColor, _hpBarColor)
        UpdateBarColorButton(btnMpBarColor, _mpBarColor)

        If btnPickHpBarColor IsNot Nothing Then
            btnPickHpBarColor.Text = If(_pendingBarColorPick = BarColorPickKind.Hp, "Click Snapshot...", "Pick Snapshot")
            btnPickHpBarColor.BackColor = If(_pendingBarColorPick = BarColorPickKind.Hp, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
            btnPickHpBarColor.ForeColor = Color.White
        End If

        If btnPickMpBarColor IsNot Nothing Then
            btnPickMpBarColor.Text = If(_pendingBarColorPick = BarColorPickKind.Mp, "Click Snapshot...", "Pick Snapshot")
            btnPickMpBarColor.BackColor = If(_pendingBarColorPick = BarColorPickKind.Mp, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
            btnPickMpBarColor.ForeColor = Color.White
        End If

        If picSnapshot IsNot Nothing Then
            picSnapshot.Cursor = If(IsSnapshotPickActive(), Cursors.Cross, Cursors.Default)
        End If
    End Sub

    Private Sub UpdateBarColorButton(button As Button, color As Color)
        If button Is Nothing Then
            Return
        End If

        button.Text = FormatBarColor(color)
        button.BackColor = color
        button.ForeColor = GetReadableForeground(color)
    End Sub

    Private Function IsSnapshotPickActive() As Boolean
        Return _isPickingLootRejectPoint OrElse
               _isPickingLootNamePickupPoint OrElse
               _pendingBarColorPick <> BarColorPickKind.None
    End Function

    Private Shared Function FormatBarColor(color As Color) As String
        Return $"#{color.R:X2}{color.G:X2}{color.B:X2}"
    End Function

    Private Shared Function GetReadableForeground(background As Color) As Color
        Dim luma As Integer = (background.R * 30 + background.G * 59 + background.B * 11) \ 100
        Return If(luma >= 130, Color.Black, Color.White)
    End Function

    Private Sub MonsterFilterOptionChanged(_sender As Object, _e As EventArgs)
        UpdateMonsterFilterUi()
    End Sub

    Private Function GetMonsterFilterMode() As String
        If chkMonsterWhitelistMode IsNot Nothing AndAlso chkMonsterWhitelistMode.Checked Then
            Return "whitelist"
        End If
        Return "blacklist"
    End Function

    Private Function GetMonsterFilterConfirmReads() As Integer
        If chkMonsterConfirmOnce IsNot Nothing AndAlso chkMonsterConfirmOnce.Checked Then
            Return 1
        End If
        Return 2
    End Function

    Private Shared Function NormalizeMonsterFilterMode(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim().ToLowerInvariant()
        If cleaned = "whitelist" OrElse cleaned = "white" OrElse cleaned = "allow" OrElse cleaned = "allowlist" Then
            Return "whitelist"
        End If
        Return "blacklist"
    End Function

    Private Sub SelectMonsterFilterMode(raw As String)
        If chkMonsterWhitelistMode IsNot Nothing Then
            chkMonsterWhitelistMode.Checked = NormalizeMonsterFilterMode(raw).Equals("whitelist", StringComparison.OrdinalIgnoreCase)
        End If
        UpdateMonsterFilterUi()
    End Sub

    Private Sub UpdateMonsterFilterUi()
        Dim enabled As Boolean = chkMonsterFilter Is Nothing OrElse chkMonsterFilter.Checked
        Dim whitelist As Boolean = chkMonsterWhitelistMode IsNot Nothing AndAlso chkMonsterWhitelistMode.Checked
        Dim oneRead As Boolean = chkMonsterConfirmOnce IsNot Nothing AndAlso chkMonsterConfirmOnce.Checked

        If chkMonsterFilter IsNot Nothing Then
            chkMonsterFilter.Text = If(enabled, "Enable Monster Filter", "Monster Filter Disabled")
        End If

        If chkMonsterWhitelistMode IsNot Nothing Then
            chkMonsterWhitelistMode.Text = If(whitelist, "Mode: Whitelist", "Mode: Blacklist")
            chkMonsterWhitelistMode.BackColor = If(whitelist, Color.FromArgb(0, 185, 80), Color.FromArgb(220, 35, 35))
            chkMonsterWhitelistMode.ForeColor = Color.White
        End If

        If chkMonsterConfirmOnce IsNot Nothing Then
            chkMonsterConfirmOnce.Text = If(oneRead, "Name Check: 1 read", "Name Check: 2 reads")
            chkMonsterConfirmOnce.BackColor = If(oneRead, Color.FromArgb(130, 95, 25), Color.FromArgb(42, 84, 130))
            chkMonsterConfirmOnce.ForeColor = Color.White
        End If

        If lstMonsterFilter IsNot Nothing Then
            If Not enabled Then
                lstMonsterFilter.BackColor = Color.FromArgb(35, 35, 35)
                lstMonsterFilter.ForeColor = Color.LightGray
            ElseIf whitelist Then
                lstMonsterFilter.BackColor = Color.FromArgb(0, 118, 48)
                lstMonsterFilter.ForeColor = Color.White
            Else
                lstMonsterFilter.BackColor = Color.FromArgb(150, 0, 0)
                lstMonsterFilter.ForeColor = Color.White
            End If
        End If
    End Sub

    Private Sub HoldPlaceRestrictivenessChanged(_sender As Object, _e As EventArgs)
        If _updatingHoldPlacePreset Then
            Return
        End If

        Dim mode As String = GetHoldPlaceRestrictivenessMode()
        If Not mode.Equals("custom", StringComparison.OrdinalIgnoreCase) Then
            ApplyHoldPlaceRestrictivenessPreset(mode)
        End If
    End Sub

    Private Sub HoldPlaceCustomValueChanged(_sender As Object, _e As EventArgs)
        If _updatingHoldPlacePreset Then
            Return
        End If

        SelectHoldPlaceRestrictivenessMode("custom", applyPreset:=False)
    End Sub

    Private Function GetHoldPlaceRestrictivenessMode() As String
        If cboHoldPlaceRestrictiveness IsNot Nothing AndAlso cboHoldPlaceRestrictiveness.SelectedItem IsNot Nothing Then
            Return NormalizeHoldPlaceRestrictivenessMode(cboHoldPlaceRestrictiveness.SelectedItem.ToString())
        End If
        Return "custom"
    End Function

    Private Shared Function NormalizeHoldPlaceRestrictivenessMode(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim().ToLowerInvariant()
        If cleaned.StartsWith("low", StringComparison.OrdinalIgnoreCase) Then
            Return "low"
        End If
        If cleaned.StartsWith("medium", StringComparison.OrdinalIgnoreCase) Then
            Return "medium"
        End If
        If cleaned.StartsWith("extra", StringComparison.OrdinalIgnoreCase) OrElse cleaned.Contains("extra high") Then
            Return "extra_high"
        End If
        If cleaned.StartsWith("high", StringComparison.OrdinalIgnoreCase) Then
            Return "high"
        End If
        Return "custom"
    End Function

    Private Shared Function GetHoldPlaceRestrictivenessLabel(mode As String) As String
        Select Case NormalizeHoldPlaceRestrictivenessMode(mode)
            Case "low"
                Return "Low"
            Case "medium"
                Return "Medium (Recommended)"
            Case "high"
                Return "High"
            Case "extra_high"
                Return "Extra High"
            Case Else
                Return "Custom"
        End Select
    End Function

    Private Sub SelectHoldPlaceRestrictivenessMode(raw As String, Optional applyPreset As Boolean = False)
        If cboHoldPlaceRestrictiveness Is Nothing Then
            Return
        End If

        Dim mode As String = NormalizeHoldPlaceRestrictivenessMode(raw)
        Dim label As String = GetHoldPlaceRestrictivenessLabel(mode)
        Dim selectedIndex As Integer = -1
        For i As Integer = 0 To cboHoldPlaceRestrictiveness.Items.Count - 1
            If NormalizeHoldPlaceRestrictivenessMode(cboHoldPlaceRestrictiveness.Items(i).ToString()).Equals(mode, StringComparison.OrdinalIgnoreCase) Then
                selectedIndex = i
                Exit For
            End If
        Next

        If selectedIndex < 0 Then
            cboHoldPlaceRestrictiveness.Items.Add(label)
            selectedIndex = cboHoldPlaceRestrictiveness.Items.Count - 1
        End If

        _updatingHoldPlacePreset = True
        Try
            If cboHoldPlaceRestrictiveness.SelectedIndex <> selectedIndex Then
                cboHoldPlaceRestrictiveness.SelectedIndex = selectedIndex
            End If
        Finally
            _updatingHoldPlacePreset = False
        End Try

        If applyPreset AndAlso Not mode.Equals("custom", StringComparison.OrdinalIgnoreCase) Then
            ApplyHoldPlaceRestrictivenessPreset(mode)
        End If
    End Sub

    Private Sub ApplyHoldPlaceRestrictivenessPreset(mode As String)
        Dim tolerance As Integer = 4
        Dim moveBurstMs As Integer = 750
        Dim correctionMs As Integer = 900
        Dim emergencyLeash As Integer = 60
        Dim postFightReturn As Boolean = True
        Dim combatSafe As Boolean = True
        Dim directionLearning As Boolean = True

        If Not TryGetHoldPlaceRestrictivenessPreset(mode, tolerance, moveBurstMs, correctionMs, emergencyLeash, postFightReturn, combatSafe, directionLearning) Then
            Return
        End If

        _updatingHoldPlacePreset = True
        Try
            SetNumericControlValue(nudHoldPlaceRadius, CDec(tolerance))
            SetNumericControlValue(nudHoldPlaceMoveBurstMs, CDec(moveBurstMs))
            SetNumericControlValue(nudHoldPlaceCorrectionMs, CDec(correctionMs))
            SetNumericControlValue(nudHoldPlaceEmergencyLeash, CDec(emergencyLeash))
            If chkHoldPlacePostFightReturn IsNot Nothing Then
                chkHoldPlacePostFightReturn.Checked = postFightReturn
            End If
            If chkHoldPlaceCombatSafe IsNot Nothing Then
                chkHoldPlaceCombatSafe.Checked = combatSafe
            End If
            If chkHoldPlaceDirectionLearning IsNot Nothing Then
                chkHoldPlaceDirectionLearning.Checked = directionLearning
            End If
        Finally
            _updatingHoldPlacePreset = False
        End Try
    End Sub

    Private Function HoldPlaceControlsMatchPreset(mode As String) As Boolean
        Dim tolerance As Integer = 0
        Dim moveBurstMs As Integer = 0
        Dim correctionMs As Integer = 0
        Dim emergencyLeash As Integer = 0
        Dim postFightReturn As Boolean = False
        Dim combatSafe As Boolean = False
        Dim directionLearning As Boolean = False

        If Not TryGetHoldPlaceRestrictivenessPreset(mode, tolerance, moveBurstMs, correctionMs, emergencyLeash, postFightReturn, combatSafe, directionLearning) Then
            Return False
        End If
        If nudHoldPlaceRadius Is Nothing OrElse nudHoldPlaceMoveBurstMs Is Nothing OrElse nudHoldPlaceCorrectionMs Is Nothing OrElse nudHoldPlaceEmergencyLeash Is Nothing Then
            Return False
        End If

        Return CInt(nudHoldPlaceRadius.Value) = tolerance AndAlso
               CInt(nudHoldPlaceMoveBurstMs.Value) = moveBurstMs AndAlso
               CInt(nudHoldPlaceCorrectionMs.Value) = correctionMs AndAlso
               CInt(nudHoldPlaceEmergencyLeash.Value) = emergencyLeash AndAlso
               (chkHoldPlacePostFightReturn IsNot Nothing AndAlso chkHoldPlacePostFightReturn.Checked = postFightReturn) AndAlso
               (chkHoldPlaceCombatSafe IsNot Nothing AndAlso chkHoldPlaceCombatSafe.Checked = combatSafe) AndAlso
               (chkHoldPlaceDirectionLearning IsNot Nothing AndAlso chkHoldPlaceDirectionLearning.Checked = directionLearning)
    End Function

    Private Shared Function TryGetHoldPlaceRestrictivenessPreset(mode As String,
                                                                ByRef tolerance As Integer,
                                                                ByRef moveBurstMs As Integer,
                                                                ByRef correctionMs As Integer,
                                                                ByRef emergencyLeash As Integer,
                                                                ByRef postFightReturn As Boolean,
                                                                ByRef combatSafe As Boolean,
                                                                ByRef directionLearning As Boolean) As Boolean
        postFightReturn = True
        combatSafe = True
        directionLearning = True

        Select Case NormalizeHoldPlaceRestrictivenessMode(mode)
            Case "low"
                tolerance = 8
                moveBurstMs = 700
                correctionMs = 1600
                emergencyLeash = 95
                Return True
            Case "medium"
                tolerance = 4
                moveBurstMs = 750
                correctionMs = 900
                emergencyLeash = 60
                Return True
            Case "high"
                tolerance = 2
                moveBurstMs = 800
                correctionMs = 650
                emergencyLeash = 40
                Return True
            Case "extra_high"
                tolerance = 1
                moveBurstMs = 800
                correctionMs = 350
                emergencyLeash = 25
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub HoldPlaceAnchorValueChanged(_sender As Object, _e As EventArgs)
        _holdPlaceAnchorSet = True
    End Sub

    Private Sub HoldPlaceUseCurrentClicked(_sender As Object, _e As EventArgs)
        Dim status As BotStatus = _fullStatus
        If status Is Nothing OrElse status.MapCoordinateX < 0 OrElse status.MapCoordinateY < 0 Then
            AppendLog("Hold on place anchor not set: current map coordinates are not available yet.")
            Return
        End If

        _holdPlaceAnchorSet = True
        SetNumericControlValue(nudHoldPlaceTargetX, CDec(Math.Max(0, Math.Min(999, status.MapCoordinateX))))
        SetNumericControlValue(nudHoldPlaceTargetY, CDec(Math.Max(0, Math.Min(999, status.MapCoordinateY))))
        If chkHoldPlaceEnabled IsNot Nothing Then
            chkHoldPlaceEnabled.Checked = True
        End If
        AppendLog($"Hold on place anchor set to {status.MapCoordinateX:000}/{status.MapCoordinateY:000}.")
        PushLiveConfig()
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Sub HoldPlaceOpenOcrCropsClicked(_sender As Object, _e As EventArgs)
        Try
            Dim diagnosticsDir As String = BotEngine.GetMapCoordinateOcrDiagnosticsDirectory()
            Directory.CreateDirectory(diagnosticsDir)
            Process.Start(New ProcessStartInfo(diagnosticsDir) With {.UseShellExecute = True})
            AppendLog("Opened map coordinate OCR crop folder: " & diagnosticsDir)
        Catch ex As Exception
            AppendLog("Unable to open map coordinate OCR crop folder: " & ex.Message)
        End Try
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
        SetNotificationRowVisible(7, lblDiscordShotBotToken, txtDiscordShotBotToken, useDiscord)
        SetNotificationRowVisible(8, lblDiscordShotChannelId, txtDiscordShotChannelId, useDiscord)
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

    Private Sub ApplyBarColorSettingsToConfig(cfg As BotConfig)
        If cfg Is Nothing Then
            Return
        End If

        Dim tolerance As Integer = _barColorTolerance
        If nudBarColorTolerance IsNot Nothing Then
            tolerance = CInt(nudBarColorTolerance.Value)
        End If

        cfg.CustomBarColorsEnabled = _customBarColorsEnabled
        cfg.HpBarColorArgb = _hpBarColor.ToArgb()
        cfg.MpBarColorArgb = _mpBarColor.ToArgb()
        cfg.BarColorTolerance = Math.Max(8, Math.Min(120, tolerance))
    End Sub

    Private Sub ApplyBarColorConfigToUi(cfg As BotConfig)
        If cfg Is Nothing Then
            Return
        End If

        _barColorSyncInProgress = True
        Try
            _customBarColorsEnabled = cfg.CustomBarColorsEnabled
            Try
                _hpBarColor = Color.FromArgb(cfg.HpBarColorArgb)
            Catch
                _hpBarColor = Color.FromArgb(BotConfig.DefaultHpBarColorArgb())
            End Try
            Try
                _mpBarColor = Color.FromArgb(cfg.MpBarColorArgb)
            Catch
                _mpBarColor = Color.FromArgb(BotConfig.DefaultMpBarColorArgb())
            End Try
            _barColorTolerance = Math.Max(8, Math.Min(120, cfg.BarColorTolerance))
            UpdateBarColorUi()
        Finally
            _barColorSyncInProgress = False
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
            .ItemSize = New Size(135, 42)
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
        _autoRelaunchTab = BuildAutoRelaunchTab()
        _mainTabs.TabPages.Add(_liteTab)
        _mainTabs.TabPages.Add(_combatTab)
        _mainTabs.TabPages.Add(_visionTab)
        _mainTabs.TabPages.Add(_autoPotTab)
        _mainTabs.TabPages.Add(_autoRelaunchTab)
        _autoLootTab = BuildAutoLootTab()
        _mainTabs.TabPages.Add(_autoLootTab)
        _levelingTab = BuildLevelingTab()
        _mainTabs.TabPages.Add(_levelingTab)
        _holdPlaceTab = BuildHoldPlaceTab()
        _mainTabs.TabPages.Add(_holdPlaceTab)
        _diagnosticsTab = BuildDiagnosticsTab()
        _mainTabs.TabPages.Add(_diagnosticsTab)
        _updateTab = BuildUpdateTab()
        _mainTabs.TabPages.Add(_updateTab)
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

        If Not GetRunningEdition().HasValue Then
            _inGameBotToggleEdition = _edition
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
        If tab Is _holdPlaceTab Then
            Return IsHoldPlaceTabActive()
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

    Private Function IsHoldPlaceTabActive() As Boolean
        Return chkHoldPlaceEnabled IsNot Nothing AndAlso chkHoldPlaceEnabled.Checked
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
            Return "buff"
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
            _liteAutoPotHpColorEnabled = False
            _liteAutoPotHpColorArgb = 0
            _liteAutoPotMpPointX = -1
            _liteAutoPotMpPointY = -1
            _liteAutoPotMpColorEnabled = False
            _liteAutoPotMpColorArgb = 0
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
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 21.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 29.0F))
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
        Dim generalLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 12}
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        generalLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        generalLayout.Controls.Add(New Label() With {.Text = "Selected Process", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        lblSelectedProcess = New Label() With {
            .Text = "No process selected",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .BorderStyle = BorderStyle.Fixed3D,
            .AutoEllipsis = True,
            .Padding = New Padding(4, 0, 4, 0)
        }
        generalLayout.Controls.Add(lblSelectedProcess, 1, 0)
        generalLayout.SetColumnSpan(lblSelectedProcess, 3)

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

        chkHighMaxHpSpecial = New CheckBox() With {.Text = "Use buff key on high max HP mobs", .Dock = DockStyle.Fill}
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

        chkEvadeDadati = New CheckBox() With {
            .Text = "Evade Dadatis (tap W/S, then retarget; game window must be on)",
            .Dock = DockStyle.Fill,
            .Checked = False
        }
        generalLayout.Controls.Add(chkEvadeDadati, 0, 6)
        generalLayout.SetColumnSpan(chkEvadeDadati, 4)

        Dim hint As New Label() With {.Text = "Mob HP Presence % = red-fill in mob_hp_rect. High max HP buff and avoid-high-HP use mob_life_rect to read Max HP numbers.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.LightGreen}
        generalLayout.Controls.Add(hint, 0, 7)
        generalLayout.SetColumnSpan(hint, 4)

        chkChatTranslationEnabled = New CheckBox() With {.Text = "Enable chat translation OCR", .Dock = DockStyle.Fill}
        generalLayout.Controls.Add(chkChatTranslationEnabled, 0, 8)
        generalLayout.SetColumnSpan(chkChatTranslationEnabled, 2)

        chkChatTranslationOverlay = New CheckBox() With {.Text = "Show translated overlay", .Dock = DockStyle.Fill, .Checked = True}
        generalLayout.Controls.Add(chkChatTranslationOverlay, 2, 8)
        generalLayout.SetColumnSpan(chkChatTranslationOverlay, 2)

        generalLayout.Controls.Add(New Label() With {.Text = "Target Lang", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 9)
        cboChatTargetLanguage = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        cboChatTargetLanguage.DisplayMember = NameOf(ChatLanguageOption.Label)
        cboChatTargetLanguage.ValueMember = NameOf(ChatLanguageOption.Code)
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("English", "en"))
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("Espanol", "es"))
        cboChatTargetLanguage.Items.Add(New ChatLanguageOption("Filipino", "tl"))
        SelectChatTargetLanguage("en")
        generalLayout.Controls.Add(cboChatTargetLanguage, 1, 9)

        generalLayout.Controls.Add(New Label() With {.Text = "Chat Scan (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 9)
        nudChatScanMs = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 250, .Maximum = 5000, .Value = 700}
        generalLayout.Controls.Add(nudChatScanMs, 3, 9)

        generalLayout.Controls.Add(New Label() With {.Text = "Overlay Lines", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 10)
        nudChatMaxLines = New NumericUpDown() With {.Dock = DockStyle.Fill, .Minimum = 1, .Maximum = 12, .Value = 6}
        generalLayout.Controls.Add(nudChatMaxLines, 1, 10)

        lblChatTranslationStatus = New Label() With {
            .Text = "Chat Translation: idle. Calibrate chat_rect in Regions, then keep the chat window visible.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        generalLayout.Controls.Add(lblChatTranslationStatus, 0, 11)
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

        Dim lootAreaPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Margin = New Padding(0, 6, 0, 0)}
        lootAreaPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        lootAreaPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        lootAreaPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        lootAreaPanel.Controls.Add(New Label() With {.Text = "Loot Scan Area", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        txtLootScanAreaPoints = New TextBox() With {.Dock = DockStyle.Fill}
        lootAreaPanel.Controls.Add(txtLootScanAreaPoints, 1, 0)
        btnVisionLootScanner = New Button() With {
            .Text = If(_lootScannerEnabled, "Loot Scan Area: ON", "Loot Scan Area: OFF"),
            .Dock = DockStyle.Fill,
            .Margin = New Padding(8, 0, 0, 0),
            .BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        AddHandler btnVisionLootScanner.Click, AddressOf ToggleLootScannerClicked
        lootAreaPanel.Controls.Add(btnVisionLootScanner, 2, 0)
        regionLayout.Controls.Add(lootAreaPanel, 0, 2)
        left.Controls.Add(regionGroup, 0, 1)

        root.Controls.Add(left, 0, 0)

        Dim right As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        right.RowStyles.Add(New RowStyle(SizeType.Absolute, 260.0F))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        right.RowStyles.Add(New RowStyle(SizeType.Absolute, 118.0F))
        right.Controls.Add(BuildProcessListGroup(), 0, 0)

        Dim snapshotGroup As New GroupBox() With {.Text = "Snapshot", .Dock = DockStyle.Fill}
        Dim snapshotLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .Padding = New Padding(6)}
        snapshotLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        picSnapshot = New PictureBox() With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Black}
        AddHandler picSnapshot.MouseClick, AddressOf SnapshotMouseClick
        snapshotLayout.Controls.Add(picSnapshot, 0, 0)

        snapshotGroup.Controls.Add(snapshotLayout)
        right.Controls.Add(snapshotGroup, 0, 1)
        right.Controls.Add(BuildPeriodicScreenshotGroup(), 0, 2)

        root.Controls.Add(right, 1, 0)

        AddTabExplanationButton(tab, HelpScopeVision)
        Return tab
    End Function

    Private Function BuildPeriodicScreenshotGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Automatic Screenshots", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6,
            .RowCount = 2,
            .Padding = New Padding(6)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 72.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 70.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 88.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        chkPeriodicScreenshots = New CheckBox() With {
            .Text = "Enable automatic",
            .Dock = DockStyle.Fill
        }
        layout.Controls.Add(chkPeriodicScreenshots, 0, 0)

        layout.Controls.Add(New Label() With {
            .Text = "Every (min)",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0)
        nudPeriodicScreenshotMinutes = New NumericUpDown() With {
            .Dock = DockStyle.Fill,
            .Minimum = 1,
            .Maximum = 999,
            .Value = 15,
            .ThousandsSeparator = True
        }
        layout.Controls.Add(nudPeriodicScreenshotMinutes, 2, 0)

        lblPeriodicScreenshotStatus = New Label() With {
            .Text = "Off",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        layout.Controls.Add(lblPeriodicScreenshotStatus, 3, 0)
        layout.SetColumnSpan(lblPeriodicScreenshotStatus, 3)

        layout.Controls.Add(New Label() With {
            .Text = "Save folder",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1)

        txtPeriodicScreenshotDirectory = New TextBox() With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .Text = DefaultPeriodicScreenshotDirectoryPath
        }
        layout.Controls.Add(txtPeriodicScreenshotDirectory, 1, 1)
        layout.SetColumnSpan(txtPeriodicScreenshotDirectory, 3)

        btnBrowsePeriodicScreenshotDirectory = New Button() With {
            .Text = "Browse...",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(45, 95, 140),
            .ForeColor = Color.White
        }
        AddHandler btnBrowsePeriodicScreenshotDirectory.Click, AddressOf BrowsePeriodicScreenshotDirectoryClicked
        layout.Controls.Add(btnBrowsePeriodicScreenshotDirectory, 4, 1)

        Dim btnOpenFolder As New Button() With {
            .Text = "Open Folder",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(55, 105, 75),
            .ForeColor = Color.White
        }
        AddHandler btnOpenFolder.Click, AddressOf OpenPeriodicScreenshotDirectoryClicked
        layout.Controls.Add(btnOpenFolder, 5, 1)

        group.Controls.Add(layout)
        Return group
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
        Dim thresholdsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 8}
        thresholdsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        thresholdsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        thresholdsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
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

        chkCustomBarColors = New CheckBox() With {.Text = "Use Custom Bar Colors", .Dock = DockStyle.Fill, .Checked = False}
        AddHandler chkCustomBarColors.CheckedChanged, AddressOf BarColorSettingsChanged
        thresholdsLayout.Controls.Add(chkCustomBarColors, 0, 3)
        thresholdsLayout.SetColumnSpan(chkCustomBarColors, 2)

        thresholdsLayout.Controls.Add(New Label() With {.Text = "HP Bar Color", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 4)
        Dim hpColorPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(0)}
        btnHpBarColor = New Button() With {.Text = "HP Color", .Width = 94, .Height = 28, .BackColor = _hpBarColor}
        AddHandler btnHpBarColor.Click, AddressOf ChooseHpBarColorClicked
        btnPickHpBarColor = New Button() With {.Text = "Pick Snapshot", .Width = 112, .Height = 28, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnPickHpBarColor.Click, AddressOf PickHpBarColorFromSnapshotClicked
        hpColorPanel.Controls.Add(btnHpBarColor)
        hpColorPanel.Controls.Add(btnPickHpBarColor)
        thresholdsLayout.Controls.Add(hpColorPanel, 1, 4)

        thresholdsLayout.Controls.Add(New Label() With {.Text = "MP Bar Color", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 5)
        Dim mpColorPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(0)}
        btnMpBarColor = New Button() With {.Text = "MP Color", .Width = 94, .Height = 28, .BackColor = _mpBarColor}
        AddHandler btnMpBarColor.Click, AddressOf ChooseMpBarColorClicked
        btnPickMpBarColor = New Button() With {.Text = "Pick Snapshot", .Width = 112, .Height = 28, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnPickMpBarColor.Click, AddressOf PickMpBarColorFromSnapshotClicked
        mpColorPanel.Controls.Add(btnMpBarColor)
        mpColorPanel.Controls.Add(btnPickMpBarColor)
        thresholdsLayout.Controls.Add(mpColorPanel, 1, 5)

        thresholdsLayout.Controls.Add(New Label() With {.Text = "Color Tolerance", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 6)
        nudBarColorTolerance = New NumericUpDown() With {.Minimum = 8D, .Maximum = 120D, .Value = BotConfig.DefaultBarColorTolerance, .Dock = DockStyle.Left, .Width = 100}
        AddHandler nudBarColorTolerance.ValueChanged, AddressOf BarColorSettingsChanged
        thresholdsLayout.Controls.Add(nudBarColorTolerance, 1, 6)

        Dim thresholdsHint As New Label() With {
            .Text = "Custom colors affect Full combat HP, MP, and mob HP bar detection. Use Capture Snapshot in Vision, then Pick Snapshot on a solid bar pixel.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        thresholdsLayout.Controls.Add(thresholdsHint, 0, 7)
        thresholdsLayout.SetColumnSpan(thresholdsHint, 2)
        thresholdsGroup.Controls.Add(thresholdsLayout)
        UpdateBarColorUi()

        Dim notifyGroup As New GroupBox() With {.Text = "Notifications + Loot Matching", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim notifyLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 13}
        tblNotificationSettings = notifyLayout
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        notifyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For i As Integer = 0 To 10
            notifyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        Next
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

        lblDiscordShotBotToken = New Label() With {.Text = "Discord Bot Token (Shot)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblDiscordShotBotToken, 0, 7)
        txtDiscordShotBotToken = New TextBox() With {.Dock = DockStyle.Fill, .Text = "", .UseSystemPasswordChar = True}
        notifyLayout.Controls.Add(txtDiscordShotBotToken, 1, 7)

        lblDiscordShotChannelId = New Label() With {.Text = "Discord Data Channel ID", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        notifyLayout.Controls.Add(lblDiscordShotChannelId, 0, 8)
        txtDiscordShotChannelId = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        notifyLayout.Controls.Add(txtDiscordShotChannelId, 1, 8)

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

        notifyLayout.Controls.Add(New Label() With {.Text = "Stats Interval (min)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 9)
        nudStatsNtfyIntervalMinutes = New NumericUpDown() With {.Minimum = 1D, .Maximum = 1440D, .DecimalPlaces = 0, .Value = 30D, .Dock = DockStyle.Left, .Width = 100}
        notifyLayout.Controls.Add(nudStatsNtfyIntervalMinutes, 1, 9)

        notifyLayout.Controls.Add(New Label() With {.Text = "Loot Matching", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 10)
        notifyLayout.Controls.Add(New Label() With {.Text = "Moved to Auto-Loot tab", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .TextAlign = ContentAlignment.MiddleLeft}, 1, 10)

        Dim note As New Label() With {
            .Text = "Use provider 'discord' with one webhook per alert stream (global, items, stats), or provider 'ntfy' with the topic fields below." & Environment.NewLine &
                    "Use role 'max_health' in Combat Skills if you want the max-health potion threshold controlled here. HP alarm only triggers at HP=0." & Environment.NewLine &
                    "Stats alerts send Prana/EXP %, EXP/hr, Rupiahs total, and Rupiahs/hr on the interval you choose while the bot is running." & Environment.NewLine &
                    "Type shot in the Discord data channel to upload the latest rolling screenshot to the Stats webhook channel.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        notifyLayout.Controls.Add(note, 0, 11)
        notifyLayout.SetColumnSpan(note, 2)

        Dim notifyFoot As New Label() With {
            .Text = "Discord and ntfy both use separate global/items/stats destinations.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.Gray,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        notifyLayout.Controls.Add(notifyFoot, 0, 12)
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

    Private Function BuildAutoRelaunchTab() As TabPage
        Dim tab As New TabPage("Auto Relaunch") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .Padding = New Padding(12)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(BuildAutoRelaunchGroup(), 0, 0)
        tab.Controls.Add(root)
        Return tab
    End Function

    Private Function BuildAutoRelaunchGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Auto Relaunch Game", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 7}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))

        chkAutoRelaunchGame = New CheckBox() With {.Text = "Enable when game closes, crashes, or disconnects", .Dock = DockStyle.Fill, .Checked = False}
        layout.Controls.Add(chkAutoRelaunchGame, 0, 0)
        layout.SetColumnSpan(chkAutoRelaunchGame, 2)

        layout.Controls.Add(New Label() With {.Text = "Game EXE", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        Dim pathPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        pathPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        pathPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84.0F))
        txtAutoRelaunchExePath = New TextBox() With {.Dock = DockStyle.Fill}
        btnBrowseAutoRelaunchExe = New Button() With {.Text = "Browse", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnBrowseAutoRelaunchExe.Click, AddressOf BrowseAutoRelaunchExeClicked
        pathPanel.Controls.Add(txtAutoRelaunchExePath, 0, 0)
        pathPanel.Controls.Add(btnBrowseAutoRelaunchExe, 1, 0)
        layout.Controls.Add(pathPanel, 1, 1)

        layout.Controls.Add(New Label() With {.Text = "Delay Seconds", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 2)
        nudAutoRelaunchDelaySeconds = New NumericUpDown() With {.Minimum = 0D, .Maximum = 300D, .Value = 5D, .Dock = DockStyle.Left, .Width = 100}
        layout.Controls.Add(nudAutoRelaunchDelaySeconds, 1, 2)

        Dim btnTestRelaunch As New Button() With {.Text = "Test Launch", .Width = 110, .Height = 30, .BackColor = Color.FromArgb(42, 120, 80), .ForeColor = Color.White}
        AddHandler btnTestRelaunch.Click, AddressOf TestAutoRelaunchClicked
        layout.Controls.Add(btnTestRelaunch, 1, 3)

        dgvAutoRelaunchClicks = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .RowHeadersVisible = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .MultiSelect = False,
            .AllowDrop = True,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .BackgroundColor = Color.FromArgb(24, 24, 24),
            .BorderStyle = BorderStyle.FixedSingle,
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            .ColumnHeadersHeight = 24
        }
        AddHandler dgvAutoRelaunchClicks.MouseDown, AddressOf AutoRelaunchClicksMouseDown
        AddHandler dgvAutoRelaunchClicks.MouseMove, AddressOf AutoRelaunchClicksMouseMove
        AddHandler dgvAutoRelaunchClicks.DragOver, AddressOf AutoRelaunchClicksDragOver
        AddHandler dgvAutoRelaunchClicks.DragDrop, AddressOf AutoRelaunchClicksDragDrop
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Step", .HeaderText = "#", .FillWeight = 32.0F, .ReadOnly = True})
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled", .HeaderText = "On", .FillWeight = 42.0F})
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "X", .HeaderText = "Game X", .FillWeight = 72.0F})
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Y", .HeaderText = "Game Y", .FillWeight = 72.0F})
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Delay", .HeaderText = "Delay s", .FillWeight = 72.0F})
        dgvAutoRelaunchClicks.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Description", .HeaderText = "Description", .FillWeight = 160.0F})
        For i As Integer = 1 To 5
            dgvAutoRelaunchClicks.Rows.Add(i.ToString(), False, "0", "0", If(i = 1, "15", "5"), "")
        Next
        layout.Controls.Add(dgvAutoRelaunchClicks, 0, 4)
        layout.SetColumnSpan(dgvAutoRelaunchClicks, 2)

        Dim clickButtonRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(0)}
        btnAutoRelaunchUseCursor = New Button() With {.Text = "Use Cursor", .Width = 98, .Height = 28, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnAutoRelaunchUseCursor.Click, AddressOf AutoRelaunchUseCursorClicked
        btnAutoRelaunchClearClicks = New Button() With {.Text = "Clear Clicks", .Width = 98, .Height = 28, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        AddHandler btnAutoRelaunchClearClicks.Click, AddressOf AutoRelaunchClearClicksClicked
        chkAutoRelaunchClickOverlay = New CheckBox() With {.Text = "Show Click Overlay", .AutoSize = True, .Height = 28, .Padding = New Padding(8, 3, 0, 0), .ForeColor = Color.LightSkyBlue}
        clickButtonRow.Controls.Add(btnAutoRelaunchUseCursor)
        clickButtonRow.Controls.Add(btnAutoRelaunchClearClicks)
        clickButtonRow.Controls.Add(chkAutoRelaunchClickOverlay)
        layout.Controls.Add(clickButtonRow, 0, 5)
        layout.SetColumnSpan(clickButtonRow, 2)

        Dim note As New Label() With {
            .Text = "Post-launch clicks use game-window coordinates after each row's delay. Drag rows to change order. Select a row, click Use Cursor, then RIGHT click the desired spot in game. Show Click Overlay displays every enabled location, execution step, path, and delay.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        layout.Controls.Add(note, 0, 6)
        layout.SetColumnSpan(note, 2)

        group.Controls.Add(layout)
        Return group
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

        Dim right As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 62.0F))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 38.0F))
        right.Controls.Add(BuildLootNameAutoPickupGroup(), 0, 0)
        right.Controls.Add(BuildArrowUnbundleGroup(), 0, 1)

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

    Private Function BuildArrowUnbundleGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Arrow Unbundle Double Right-Click", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 6}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))

        chkArrowUnbundleEnabled = New CheckBox() With {.Text = "Enable arrow unbundle", .Dock = DockStyle.Fill, .Checked = False}
        layout.Controls.Add(chkArrowUnbundleEnabled, 0, 0)
        layout.SetColumnSpan(chkArrowUnbundleEnabled, 2)

        layout.Controls.Add(New Label() With {.Text = "Every Seconds", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 1)
        nudArrowUnbundleSeconds = New NumericUpDown() With {.Dock = DockStyle.Left, .Minimum = 1D, .Maximum = 9999D, .Value = 60D, .Width = 120}
        layout.Controls.Add(nudArrowUnbundleSeconds, 1, 1)

        lblArrowUnbundlePoints = New Label() With {.Text = "Arrow Points: 0", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        layout.Controls.Add(lblArrowUnbundlePoints, 0, 2)
        layout.SetColumnSpan(lblArrowUnbundlePoints, 2)

        lstArrowUnbundlePoints = New ListBox() With {.Dock = DockStyle.Fill, .IntegralHeight = False}
        AddHandler lstArrowUnbundlePoints.SelectedIndexChanged,
            Sub(_s As Object, _e As EventArgs)
                If Not _arrowUnbundleUiSyncInProgress Then
                    UpdateArrowUnbundleUi()
                End If
            End Sub
        layout.Controls.Add(lstArrowUnbundlePoints, 0, 3)
        layout.SetColumnSpan(lstArrowUnbundlePoints, 2)

        Dim note As New Label() With {
            .Text = "For arrows: the bot double right-clicks these inventory spots on the interval to unbundle arrow stacks. Multiple points are used in order. Show Click Overlay displays the numbered path, coordinates, and interval over the game.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.TopLeft
        }
        layout.Controls.Add(note, 0, 4)
        layout.SetColumnSpan(note, 2)

        Dim buttons As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(0)}
        btnPickArrowUnbundlePoint = New Button() With {.Text = "Add Point", .Width = 92, .Height = 30, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White}
        AddHandler btnPickArrowUnbundlePoint.Click, AddressOf PickArrowUnbundlePointClicked
        btnRemoveArrowUnbundlePoint = New Button() With {.Text = "Remove", .Width = 86, .Height = 30, .BackColor = Color.FromArgb(105, 80, 45), .ForeColor = Color.White}
        AddHandler btnRemoveArrowUnbundlePoint.Click, AddressOf RemoveArrowUnbundlePointClicked
        btnClearArrowUnbundlePoints = New Button() With {.Text = "Clear", .Width = 74, .Height = 30, .BackColor = Color.FromArgb(110, 45, 45), .ForeColor = Color.White}
        AddHandler btnClearArrowUnbundlePoints.Click, AddressOf ClearArrowUnbundlePointsClicked
        chkArrowUnbundleOverlay = New CheckBox() With {.Text = "Show Click Overlay", .AutoSize = True, .Height = 30, .Padding = New Padding(8, 3, 0, 0), .ForeColor = Color.LightSkyBlue}
        buttons.Controls.Add(btnPickArrowUnbundlePoint)
        buttons.Controls.Add(btnRemoveArrowUnbundlePoint)
        buttons.Controls.Add(btnClearArrowUnbundlePoints)
        buttons.Controls.Add(chkArrowUnbundleOverlay)
        layout.Controls.Add(buttons, 0, 5)
        layout.SetColumnSpan(buttons, 2)

        group.Controls.Add(layout)
        UpdateArrowUnbundleUi()
        Return group
    End Function

    Private Function BuildDiagnosticsTab() As TabPage
        Dim tab As New TabPage("Diagnostics") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 154))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim controls As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 8, .RowCount = 4, .Padding = New Padding(6), .BackColor = Color.FromArgb(20, 20, 20)}
        For i As Integer = 1 To 8
            controls.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 12.5F))
        Next
        controls.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
        controls.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
        controls.RowStyles.Add(New RowStyle(SizeType.Absolute, 36))
        controls.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))

        chkAdaptivePerformance = New CheckBox() With {.Text = "Adaptive performance", .Checked = True, .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro}
        controls.Controls.Add(chkAdaptivePerformance, 0, 0)
        controls.SetColumnSpan(chkAdaptivePerformance, 2)
        chkPixelChangeGate = New CheckBox() With {.Text = "Skip unchanged OCR", .Checked = True, .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro}
        controls.Controls.Add(chkPixelChangeGate, 2, 0)
        controls.SetColumnSpan(chkPixelChangeGate, 2)
        controls.Controls.Add(New Label() With {.Text = "Capture backend", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro}, 4, 0)
        cboCaptureBackend = New ComboBox() With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        cboCaptureBackend.Items.AddRange(New Object() {"Auto", "Cached GDI", "Windows Graphics Capture"})
        cboCaptureBackend.SelectedIndex = 0
        controls.Controls.Add(cboCaptureBackend, 5, 0)
        controls.SetColumnSpan(cboCaptureBackend, 2)

        controls.Controls.Add(New Label() With {.Text = "Slow min ms", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro}, 0, 1)
        nudAdaptiveSlowMinMs = New NumericUpDown() With {.Minimum = 40, .Maximum = 2000, .Value = 140, .Increment = 10, .Dock = DockStyle.Fill}
        controls.Controls.Add(nudAdaptiveSlowMinMs, 1, 1)
        controls.Controls.Add(New Label() With {.Text = "Slow x loop", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro}, 2, 1)
        nudAdaptiveSlowMultiplier = New NumericUpDown() With {.Minimum = 1D, .Maximum = 10D, .DecimalPlaces = 2, .Increment = 0.1D, .Value = 1.8D, .Dock = DockStyle.Fill}
        controls.Controls.Add(nudAdaptiveSlowMultiplier, 3, 1)
        controls.Controls.Add(New Label() With {.Text = "Recover x loop", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro}, 4, 1)
        nudAdaptiveRecoveryMultiplier = New NumericUpDown() With {.Minimum = 1D, .Maximum = 10D, .DecimalPlaces = 2, .Increment = 0.1D, .Value = 1.25D, .Dock = DockStyle.Fill}
        controls.Controls.Add(nudAdaptiveRecoveryMultiplier, 5, 1)
        controls.Controls.Add(New Label() With {.Text = "Confirm", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro}, 6, 1)
        Dim confirmLayout As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .Margin = New Padding(0)}
        nudAdaptiveSlowConfirm = New NumericUpDown() With {.Minimum = 1, .Maximum = 60, .Value = 5, .Width = 58}
        nudAdaptiveRecoveryConfirm = New NumericUpDown() With {.Minimum = 1, .Maximum = 120, .Value = 14, .Width = 58}
        confirmLayout.Controls.Add(nudAdaptiveSlowConfirm)
        confirmLayout.Controls.Add(nudAdaptiveRecoveryConfirm)
        controls.Controls.Add(confirmLayout, 7, 1)

        btnRunBenchmark = New Button() With {.Text = "Run Benchmark", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 95, 150), .ForeColor = Color.White}
        controls.Controls.Add(btnRunBenchmark, 0, 2)
        controls.SetColumnSpan(btnRunBenchmark, 2)
        btnExportDiagnostics = New Button() With {.Text = "Export Diagnostics", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(65, 85, 105), .ForeColor = Color.White}
        controls.Controls.Add(btnExportDiagnostics, 2, 2)
        controls.SetColumnSpan(btnExportDiagnostics, 2)

        Dim scanTimerPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .AutoScroll = True, .Margin = New Padding(0)}
        nudFullFrameScanMs = AddScanTimerInput(scanTimerPanel, "Full ms", 100, 5000, 50, 500D)
        nudLootScannerSeconds = AddScanTimerInput(scanTimerPanel, "Loot sec", 1, 120, 1, 10D)
        nudMapScanMs = AddScanTimerInput(scanTimerPanel, "Map ms", 250, 10000, 50, 900D)
        nudPartyScanMs = AddScanTimerInput(scanTimerPanel, "Party ms", 250, 10000, 50, 700D)
        nudMobNameScanMs = AddScanTimerInput(scanTimerPanel, "Mob OCR ms", 120, 5000, 25, 650D)
        controls.Controls.Add(scanTimerPanel, 0, 3)
        controls.SetColumnSpan(scanTimerPanel, 8)

        txtDiagnostics = New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Both, .ReadOnly = True, .Font = New Font("Consolas", 9.5F, FontStyle.Regular), .BackColor = Color.FromArgb(10, 10, 10), .ForeColor = Color.LightGray}
        root.Controls.Add(controls, 0, 0)
        root.Controls.Add(txtDiagnostics, 0, 1)
        tab.Controls.Add(root)
        AddTabExplanationButton(tab, HelpScopeDiagnostics)
        Return tab
    End Function

    Private Function BuildUpdateTab() As TabPage
        Dim tab As New TabPage("Update") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 6,
            .Padding = New Padding(18)
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 82.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 142.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim header As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1}
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 62.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38.0F))
        Dim title As New Label() With {
            .Text = "KathanaBot Automatic Updates",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold),
            .ForeColor = Color.LightSkyBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        header.Controls.Add(title, 0, 0)
        lblUpdateCurrentVersion = New Label() With {
            .Text = "Current version: " & GetCurrentApplicationVersionText(),
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .ForeColor = Color.Gainsboro,
            .TextAlign = ContentAlignment.MiddleRight
        }
        header.Controls.Add(lblUpdateCurrentVersion, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim settingsGroup As New GroupBox() With {.Text = "GitHub Releases", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim settings As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 3}
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))

        settings.Controls.Add(New Label() With {.Text = "Repository URL", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        txtUpdateRepositoryUrl = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultUpdateRepositoryUrl}
        settings.Controls.Add(txtUpdateRepositoryUrl, 1, 0)
        settings.SetColumnSpan(txtUpdateRepositoryUrl, 2)
        btnOpenUpdateReleases = New Button() With {.Text = "Open Releases", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(55, 95, 140), .ForeColor = Color.White}
        AddHandler btnOpenUpdateReleases.Click, AddressOf OpenUpdateReleasesClicked
        settings.Controls.Add(btnOpenUpdateReleases, 3, 0)

        chkUpdateCheckAtStartup = New CheckBox() With {.Text = "Check automatically at startup", .Dock = DockStyle.Fill, .Checked = True}
        settings.Controls.Add(chkUpdateCheckAtStartup, 0, 1)
        settings.SetColumnSpan(chkUpdateCheckAtStartup, 2)
        chkUpdateIncludePrereleases = New CheckBox() With {.Text = "Include prerelease versions", .Dock = DockStyle.Fill, .Checked = False}
        settings.Controls.Add(chkUpdateIncludePrereleases, 2, 1)
        settings.SetColumnSpan(chkUpdateIncludePrereleases, 2)

        lblUpdateInstallMode = New Label() With {
            .Text = "Install mode: checking...",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        settings.Controls.Add(lblUpdateInstallMode, 0, 2)
        settings.SetColumnSpan(lblUpdateInstallMode, 4)
        settingsGroup.Controls.Add(settings)
        root.Controls.Add(settingsGroup, 0, 1)

        Dim actions As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Padding = New Padding(0, 8, 0, 8)}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.333F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.333F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.334F))
        btnCheckForUpdates = New Button() With {.Text = "Check Now", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 95, 150), .ForeColor = Color.White, .Margin = New Padding(0, 0, 8, 0)}
        AddHandler btnCheckForUpdates.Click, AddressOf CheckForUpdatesClicked
        actions.Controls.Add(btnCheckForUpdates, 0, 0)
        btnUpdateAndRestart = New Button() With {.Text = "Update and Restart", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(35, 130, 75), .ForeColor = Color.White, .Enabled = False, .Margin = New Padding(8, 0, 8, 0)}
        AddHandler btnUpdateAndRestart.Click, AddressOf UpdateAndRestartClicked
        actions.Controls.Add(btnUpdateAndRestart, 1, 0)
        Dim safetyNote As New Label() With {.Text = "Works with installed and standalone EXE builds.", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(8, 0, 0, 0)}
        actions.Controls.Add(safetyNote, 2, 0)
        root.Controls.Add(actions, 0, 2)

        progressUpdateDownload = New ProgressBar() With {.Dock = DockStyle.Fill, .Minimum = 0, .Maximum = 100, .Value = 0, .Style = ProgressBarStyle.Continuous, .Margin = New Padding(0, 6, 0, 6)}
        root.Controls.Add(progressUpdateDownload, 0, 3)

        lblUpdateStatus = New Label() With {
            .Text = "Ready to check GitHub Releases.",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        root.Controls.Add(lblUpdateStatus, 0, 4)

        txtUpdateDetails = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .BackColor = Color.FromArgb(10, 10, 10),
            .ForeColor = Color.Gainsboro,
            .Font = New Font("Consolas", 10.0F, FontStyle.Regular),
            .Text = BuildBundledUpdateHistoryText()
        }
        root.Controls.Add(txtUpdateDetails, 0, 5)

        tab.Controls.Add(root)
        Return tab
    End Function

    Private Function AddScanTimerInput(parent As FlowLayoutPanel, labelText As String, minimum As Decimal, maximum As Decimal, increment As Decimal, defaultValue As Decimal) As NumericUpDown
        Dim label As New Label() With {.Text = labelText, .AutoSize = True, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Color.Gainsboro, .Margin = New Padding(8, 8, 2, 0)}
        Dim editor As New NumericUpDown() With {.Minimum = minimum, .Maximum = maximum, .Increment = increment, .Value = defaultValue, .Width = 72, .Margin = New Padding(0, 4, 8, 0)}
        parent.Controls.Add(label)
        parent.Controls.Add(editor)
        Return editor
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

        chkNavigationReturnToStart = New CheckBox() With {.Text = "Return to route start after destination", .Dock = DockStyle.Fill, .Checked = False, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkNavigationReturnToStart, 0, 26)
        settingsLayout.SetColumnSpan(chkNavigationReturnToStart, 2)

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

    Private Function BuildHoldPlaceTab() As TabPage
        Dim tab As New TabPage("Hold on place") With {.BackColor = Color.FromArgb(20, 20, 20)}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(8)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        tab.Controls.Add(root)

        Dim settingsGroup As New GroupBox() With {.Text = "Hold on place", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim settingsLayout As New TableLayoutPanel() With {.Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 13}
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
        settingsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For i As Integer = 0 To 12
            settingsLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        settingsGroup.Controls.Add(settingsLayout)

        chkHoldPlaceEnabled = New CheckBox() With {.Text = "Enable Hold on place", .Dock = DockStyle.Fill, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkHoldPlaceEnabled, 0, 0)
        settingsLayout.SetColumnSpan(chkHoldPlaceEnabled, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Restrictiveness", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 1)
        cboHoldPlaceRestrictiveness = New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 190, .Margin = New Padding(2)}
        cboHoldPlaceRestrictiveness.Items.AddRange(New Object() {"Low", "Medium (Recommended)", "High", "Extra High", "Custom"})
        cboHoldPlaceRestrictiveness.SelectedIndex = 1
        settingsLayout.Controls.Add(cboHoldPlaceRestrictiveness, 1, 1)

        settingsLayout.Controls.Add(New Label() With {.Text = "Anchor X", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 2)
        nudHoldPlaceTargetX = New NumericUpDown() With {.Minimum = 0, .Maximum = 999, .Value = 0, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceTargetX, 1, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Anchor Y", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 3)
        nudHoldPlaceTargetY = New NumericUpDown() With {.Minimum = 0, .Maximum = 999, .Value = 0, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceTargetY, 1, 3)

        settingsLayout.Controls.Add(New Label() With {.Text = "Tolerance", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 4)
        nudHoldPlaceRadius = New NumericUpDown() With {.Minimum = 0, .Maximum = 25, .Value = 4, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceRadius, 1, 4)

        settingsLayout.Controls.Add(New Label() With {.Text = "Move Burst (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 5)
        nudHoldPlaceMoveBurstMs = New NumericUpDown() With {.Minimum = 20, .Maximum = 800, .Increment = 10, .Value = 750, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceMoveBurstMs, 1, 5)

        settingsLayout.Controls.Add(New Label() With {.Text = "Correction (ms)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 6)
        nudHoldPlaceCorrectionMs = New NumericUpDown() With {.Minimum = 150, .Maximum = 5000, .Increment = 50, .Value = 900, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceCorrectionMs, 1, 6)

        chkHoldPlacePostFightReturn = New CheckBox() With {.Text = "Return before retargeting after fight", .Dock = DockStyle.Fill, .Checked = True, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkHoldPlacePostFightReturn, 0, 7)
        settingsLayout.SetColumnSpan(chkHoldPlacePostFightReturn, 2)

        chkHoldPlaceCombatSafe = New CheckBox() With {.Text = "During combat: emergency leash only", .Dock = DockStyle.Fill, .Checked = True, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkHoldPlaceCombatSafe, 0, 8)
        settingsLayout.SetColumnSpan(chkHoldPlaceCombatSafe, 2)

        settingsLayout.Controls.Add(New Label() With {.Text = "Emergency Leash", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(2)}, 0, 9)
        nudHoldPlaceEmergencyLeash = New NumericUpDown() With {.Minimum = 5, .Maximum = 200, .Increment = 5, .Value = 60, .Width = 90, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(nudHoldPlaceEmergencyLeash, 1, 9)

        chkHoldPlaceDirectionLearning = New CheckBox() With {.Text = "Learn direction after corrections", .Dock = DockStyle.Fill, .Checked = True, .Margin = New Padding(2)}
        settingsLayout.Controls.Add(chkHoldPlaceDirectionLearning, 0, 10)
        settingsLayout.SetColumnSpan(chkHoldPlaceDirectionLearning, 2)

        Dim buttonPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(2)}
        btnHoldPlaceUseCurrent = New Button() With {.Text = "Use Current", .AutoSize = True, .BackColor = Color.FromArgb(30, 120, 80), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        btnHoldPlaceOverlay = New Button() With {.Text = "Show Overlay", .AutoSize = True, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        btnHoldPlaceOpenOcrCrops = New Button() With {.Text = "Open OCR Crops", .AutoSize = True, .BackColor = Color.FromArgb(65, 85, 105), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        buttonPanel.Controls.Add(btnHoldPlaceUseCurrent)
        buttonPanel.Controls.Add(btnHoldPlaceOverlay)
        buttonPanel.Controls.Add(btnHoldPlaceOpenOcrCrops)
        settingsLayout.Controls.Add(buttonPanel, 1, 11)

        Dim note As New Label() With {
            .Text = "Presets: Low = loose, Medium = recommended, High/Extra High = tighter leash and faster return. Editing values switches to Custom.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .AutoSize = True,
            .Margin = New Padding(2, 8, 2, 2)
        }
        settingsLayout.Controls.Add(note, 0, 12)
        settingsLayout.SetColumnSpan(note, 2)

        Dim statusGroup As New GroupBox() With {.Text = "Runtime", .Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim statusLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        statusLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        statusLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        lblHoldPlaceStatus = New Label() With {.Text = "Hold: disabled", .Dock = DockStyle.Fill, .ForeColor = Color.Khaki, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .AutoSize = True, .Margin = New Padding(2)}
        lblHoldPlaceCurrent = New Label() With {.Text = "Current: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightGreen, .AutoSize = True, .Margin = New Padding(2)}
        lblHoldPlaceTarget = New Label() With {.Text = "Anchor: n/a", .Dock = DockStyle.Fill, .ForeColor = Color.LightSteelBlue, .AutoSize = True, .Margin = New Padding(2)}
        txtHoldPlaceCoordinateLog = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .WordWrap = False,
            .BackColor = Color.FromArgb(12, 12, 12),
            .ForeColor = Color.Gainsboro,
            .BorderStyle = BorderStyle.FixedSingle,
            .Font = New Font("Consolas", 8.25F),
            .Margin = New Padding(2, 8, 2, 2),
            .Text = "Coordinate log: waiting for bot status..."
        }
        statusLayout.Controls.Add(lblHoldPlaceStatus, 0, 0)
        statusLayout.Controls.Add(lblHoldPlaceCurrent, 0, 1)
        statusLayout.Controls.Add(lblHoldPlaceTarget, 0, 2)
        statusLayout.Controls.Add(txtHoldPlaceCoordinateLog, 0, 3)
        statusGroup.Controls.Add(statusLayout)

        root.Controls.Add(settingsGroup, 0, 0)
        root.Controls.Add(statusGroup, 1, 0)
        Return tab
    End Function

    Private Function BuildCombatSkillsGroup() As GroupBox
        Dim group As New GroupBox() With {.Text = "Combat Skills", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        dgvCombat = New DataGridView() With {
            .Dock = DockStyle.Fill,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .RowHeadersVisible = False,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .EditMode = DataGridViewEditMode.EditOnEnter
        }
        dgvCombat.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Enabled"})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Key", .ReadOnly = True, .FillWeight = 60.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CooldownSec", .FillWeight = 90.0F})
        Dim roleColumn As New DataGridViewComboBoxColumn() With {.Name = "Role", .FillWeight = 80.0F}
        roleColumn.Items.AddRange(New Object() {"attack", "heal", "max_health", "mana", "buff", "high_max_hp", "repair", "stop"})
        dgvCombat.Columns.Add(roleColumn)
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Priority", .FillWeight = 75.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "TriggerPercent", .HeaderText = "Trigger%", .FillWeight = 62.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinHpPercent", .HeaderText = "MinHp%", .FillWeight = 62.0F})
        dgvCombat.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MinMpPercent", .HeaderText = "MinMp%", .FillWeight = 62.0F})
        layout.Controls.Add(dgvCombat, 0, 0)
        layout.Controls.Add(New Label() With {
            .Text = "repair role: watches unreachable_text_rect for about-to-break, broken-soon, needs-repair, or low/critical-durability warnings (with OCR tolerance). After 5 OCR reads inside a 10-minute rolling window it sends the key once, then waits for the warning to clear. TriggerPercent is ignored.",
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
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 35.0F))
        group.Controls.Add(layout)

        Dim toggleRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        chkMonsterFilter = New CheckBox() With {.Text = "Enable Monster Filter", .AutoSize = True, .Checked = True, .Margin = New Padding(3, 7, 14, 3)}
        chkMonsterWhitelistMode = New CheckBox() With {.Text = "Mode: Blacklist", .Appearance = Appearance.Button, .Width = 125, .Height = 25, .TextAlign = ContentAlignment.MiddleCenter, .Margin = New Padding(0, 4, 8, 3), .UseVisualStyleBackColor = False}
        chkMonsterConfirmOnce = New CheckBox() With {.Text = "Name Check: 2 reads", .Appearance = Appearance.Button, .Width = 145, .Height = 25, .TextAlign = ContentAlignment.MiddleCenter, .Margin = New Padding(0, 4, 3, 3), .UseVisualStyleBackColor = False}
        toggleRow.Controls.Add(chkMonsterFilter)
        toggleRow.Controls.Add(chkMonsterWhitelistMode)
        toggleRow.Controls.Add(chkMonsterConfirmOnce)
        layout.Controls.Add(toggleRow, 0, 0)

        layout.Controls.Add(New Label() With {
            .Text = "Blacklist skips listed names. Whitelist only attacks listed names. 2 reads is safer; 1 read attacks sooner.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1)

        lstMonsterFilter = New ListBox() With {.Dock = DockStyle.Fill}
        layout.Controls.Add(lstMonsterFilter, 0, 2)

        Dim actionRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        txtMonsterName = New TextBox() With {.Width = 140, .PlaceholderText = "name1, name2, name3"}
        Dim btnAddMonster As New Button() With {.Text = "Add", .Width = 70}
        Dim btnRemoveMonster As New Button() With {.Text = "Remove", .Width = 80}
        AddHandler btnAddMonster.Click, AddressOf AddMonsterClicked
        AddHandler btnRemoveMonster.Click, AddressOf RemoveMonsterClicked
        actionRow.Controls.Add(txtMonsterName)
        actionRow.Controls.Add(btnAddMonster)
        actionRow.Controls.Add(btnRemoveMonster)
        layout.Controls.Add(actionRow, 0, 3)
        UpdateMonsterFilterUi()
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
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(8), .AutoScroll = True}
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1,
            .RowCount = 22,
            .GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            .Margin = New Padding(0),
            .Padding = New Padding(4)
        }
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For rowIndex As Integer = 0 To content.RowCount - 1
            content.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next

        lblFullEdition = New Label() With {
            .Text = "FULL VERSION - for more powerful computers",
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .MinimumSize = New Size(0, 24),
            .ForeColor = Color.FromArgb(80, 170, 255),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleCenter,
            .AutoEllipsis = True
        }
        lblRunState = New Label() With {
            .Text = "BOT PAUSED",
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, 30),
            .BackColor = Color.FromArgb(110, 45, 45),
            .ForeColor = Color.White,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .Margin = New Padding(3, 2, 3, 4)
        }
        lblShortcutHint = New Label() With {
            .Text = "Shortcut: Ctrl+Shift -> Pause / Resume",
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .MinimumSize = New Size(0, 24),
            .ForeColor = Color.Gold,
            .TextAlign = ContentAlignment.MiddleCenter,
            .AutoEllipsis = True
        }
        lblState = CreateResponsiveCenterLabel("Status: Searching for target...", Color.White)
        lblSystem = CreateResponsiveCenterLabel("System Active: False", Color.LightGreen)
        lblHp = CreateResponsiveCenterLabel("HP%: 0", Color.LimeGreen)
        lblMp = CreateResponsiveCenterLabel("MP%: 0", Color.DeepSkyBlue)
        lblMobName = CreateResponsiveCenterLabel("Mob: (none) | Life: n/a", Color.LightSkyBlue)
        lblExpRate = CreateResponsiveCenterLabel("Prana/EXP: 0.00% | Rate: Calculating (1m)", Color.Khaki)
        lblRupiahsRate = CreateResponsiveCenterLabel("Rupiahs: n/a | Rate: Calculating (1m)", Color.Gold)
        btnAttack = CreateResponsiveCenterButton("Attack", Color.FromArgb(40, 180, 80), 42)
        btnSaveSettings = CreateResponsiveCenterButton("Save Settings", Color.FromArgb(55, 55, 55))
        btnStopBot = CreateResponsiveCenterButton("Stop Bot", Color.FromArgb(20, 130, 210))
        btnBypassLimits = CreateResponsiveCenterButton("Ignore Skill Min HP/MP: OFF", Color.FromArgb(110, 45, 45))
        btnBypassStuck = New Button() With {
            .Text = If(_bypassStuckTarget, "Auto Retarget If Stuck: ON", "Auto Retarget If Stuck: OFF"),
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, 38),
            .Margin = New Padding(3, 3, 3, 3),
            .BackColor = If(_bypassStuckTarget, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnRetargetNow = CreateResponsiveCenterButton("Retarget Now (E)", Color.FromArgb(155, 90, 25))
        btnPartyAutoAccept = New Button() With {
            .Text = If(_partyAutoAccept, "Auto Accept Party/Ress: ON", "Auto Accept Party/Ress: OFF"),
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, 38),
            .Margin = New Padding(3, 3, 3, 3),
            .BackColor = If(_partyAutoAccept, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        Dim lblPartyAskEvery As Label = CreateResponsiveCenterLabel("Ask Party Every (sec)", Color.White)
        nudPartyAskSeconds = New NumericUpDown() With {.Dock = DockStyle.Fill, .MinimumSize = New Size(0, 28), .Minimum = 5, .Maximum = 600, .Value = 30, .Margin = New Padding(3, 0, 3, 4)}
        Dim lblPartyAskText As Label = CreateResponsiveCenterLabel("Auto Ask Party Text", Color.White)
        txtPartyAskText = New TextBox() With {.Dock = DockStyle.Fill, .MinimumSize = New Size(0, 28), .Text = DefaultPartyAskCommand, .Margin = New Padding(3, 0, 3, 4)}
        btnPartyAsk = New Button() With {
            .Text = If(_partyAskEnabled, "Auto Ask Party (add): ON", "Auto Ask Party (add): OFF"),
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, 38),
            .Margin = New Padding(3, 3, 3, 3),
            .BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnHelp = New Button() With {
            .Text = "Explanation (EN/ES/FIL)",
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, 38),
            .Margin = New Padding(3, 3, 3, 3),
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
        Dim hpMpLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        hpMpLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        hpMpLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        hpMpLayout.Controls.Add(lblHp, 0, 0)
        hpMpLayout.Controls.Add(lblMp, 1, 0)

        Dim controls As Control() = {
            lblFullEdition, lblRunState, lblShortcutHint, lblState, lblSystem, hpMpLayout,
            lblMobName, lblExpRate, lblRupiahsRate, btnAttack, btnSaveSettings, btnStopBot,
            btnBypassLimits, btnBypassStuck, btnRetargetNow, btnPartyAutoAccept,
            lblPartyAskEvery, nudPartyAskSeconds, lblPartyAskText, txtPartyAskText, btnPartyAsk, btnHelp
        }
        For rowIndex As Integer = 0 To controls.Length - 1
            content.Controls.Add(controls(rowIndex), 0, rowIndex)
        Next

        panel.Controls.Add(content)
        AddHandler panel.ClientSizeChanged,
            Sub(_sender As Object, _args As EventArgs)
                UpdateFullCenterPanelScale(panel, content)
            End Sub
        UpdateFullCenterPanelScale(panel, content)
        Return panel
    End Function

    Private Shared Function CreateResponsiveCenterLabel(text As String, foreColor As Color) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .MinimumSize = New Size(0, 22),
            .ForeColor = foreColor,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(3, 1, 3, 1)
        }
    End Function

    Private Shared Function CreateResponsiveCenterButton(text As String, backColor As Color, Optional minimumHeight As Integer = 38) As Button
        Return New Button() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .MinimumSize = New Size(0, minimumHeight),
            .BackColor = backColor,
            .ForeColor = Color.White,
            .Margin = New Padding(3, 3, 3, 3)
        }
    End Function

    Private Sub UpdateFullCenterPanelScale(panel As Panel, content As TableLayoutPanel)
        If panel Is Nothing OrElse content Is Nothing OrElse panel.ClientSize.Width < 80 Then
            Return
        End If

        Dim usableWidth As Integer = Math.Max(80, panel.ClientSize.Width - panel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)
        Dim scale As Single = CSng(Math.Max(0.78R, Math.Min(1.15R, usableWidth / 275.0R)))
        Dim normalSize As Single = CSng(8.6F * scale)
        Dim headerSize As Single = CSng(8.8F * scale)
        Dim runSize As Single = CSng(10.0F * scale)

        SetScaledControlFont(content, normalSize)
        SetScaledControlFont(lblFullEdition, headerSize)
        SetScaledControlFont(lblRunState, runSize)
        content.Padding = New Padding(Math.Max(2, CInt(Math.Round(usableWidth * 0.025R))))
    End Sub

    Private Shared Sub SetScaledControlFont(control As Control, size As Single)
        If control Is Nothing Then
            Return
        End If

        Dim boundedSize As Single = Math.Max(6.75F, Math.Min(11.5F, size))
        If Math.Abs(control.Font.Size - boundedSize) >= 0.15F Then
            control.Font = New Font(control.Font.FontFamily, boundedSize, control.Font.Style, GraphicsUnit.Point)
        End If
        For Each child As Control In control.Controls
            SetScaledControlFont(child, size)
        Next
    End Sub

    Private Function BuildLogPanel() As GroupBox
        Dim group As New GroupBox() With {.Text = "Bot Debug Log - Real-time", .Dock = DockStyle.Fill}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}

        Dim realtimeTab As New TabPage("Real-time")
        Dim realtimeLayout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        realtimeLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        realtimeLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        realtimeLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        realtimeLayout.Controls.Add(BuildLogFilterPanel(), 0, 0)
        rtbLog = New RichTextBox() With {.Dock = DockStyle.Fill, .ReadOnly = True, .BackColor = Color.Black, .ForeColor = Color.FromArgb(70, 255, 160), .Font = New Font("Consolas", 9.0F, FontStyle.Regular), .ScrollBars = RichTextBoxScrollBars.Vertical}
        realtimeLayout.Controls.Add(rtbLog, 0, 1)
        Dim btnClearLog As New Button() With {.Text = "Clear Log", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(130, 25, 25), .ForeColor = Color.White}
        AddHandler btnClearLog.Click, Sub(_s As Object, _e As EventArgs) ClearRealtimeLog()
        realtimeLayout.Controls.Add(btnClearLog, 0, 2)
        realtimeTab.Controls.Add(realtimeLayout)

        Dim summaryTab As New TabPage("Key Summary")
        summaryTab.Controls.Add(BuildKeySummaryPanel())
        Dim lootHistoryTab As New TabPage("Loot History")
        lootHistoryTab.Controls.Add(BuildLootHistoryPanel())

        tabs.TabPages.Add(realtimeTab)
        tabs.TabPages.Add(summaryTab)
        tabs.TabPages.Add(lootHistoryTab)
        layout.Controls.Add(tabs, 0, 0)
        group.Controls.Add(layout)
        Return group
    End Function

    Private Function BuildLogFilterPanel() As Control
        Dim panel As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .AutoScroll = True, .Padding = New Padding(4, 3, 4, 0)}
        chkLogCombat = CreateLogFilterCheckBox("Combat", Sub(value) _logFilterCombatEnabled = value)
        chkLogLoot = CreateLogFilterCheckBox("Loot", Sub(value) _logFilterLootEnabled = value)
        chkLogOcrVision = CreateLogFilterCheckBox("OCR/Vision", Sub(value) _logFilterOcrVisionEnabled = value)
        chkLogNavigation = CreateLogFilterCheckBox("Navigation", Sub(value) _logFilterNavigationEnabled = value)
        chkLogWarnings = CreateLogFilterCheckBox("Warnings", Sub(value) _logFilterWarningsEnabled = value)
        chkLogMisc = CreateLogFilterCheckBox("Misc", Sub(value) _logFilterMiscEnabled = value)
        panel.Controls.Add(chkLogCombat)
        panel.Controls.Add(chkLogLoot)
        panel.Controls.Add(chkLogOcrVision)
        panel.Controls.Add(chkLogNavigation)
        panel.Controls.Add(chkLogWarnings)
        panel.Controls.Add(chkLogMisc)
        Return panel
    End Function

    Private Function CreateLogFilterCheckBox(text As String, setter As Action(Of Boolean)) As CheckBox
        Dim box As New CheckBox() With {.Text = text, .Checked = True, .AutoSize = True, .ForeColor = Color.Gainsboro, .Margin = New Padding(4, 3, 8, 0)}
        AddHandler box.CheckedChanged,
            Sub(_s As Object, _e As EventArgs)
                setter(box.Checked)
            End Sub
        setter(True)
        Return box
    End Function

    Private Function BuildLootHistoryPanel() As Control
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(6)}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))

        dgvLootHistory = New DataGridView() With {
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
        dgvLootHistory.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Time", .HeaderText = "Time"})
        dgvLootHistory.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Edition", .HeaderText = "Bot"})
        dgvLootHistory.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Item", .HeaderText = "Item"})
        dgvLootHistory.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Action", .HeaderText = "Action"})
        dgvLootHistory.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Detail", .HeaderText = "Detail"})
        layout.Controls.Add(dgvLootHistory, 0, 0)

        Dim btnClearLootHistory As New Button() With {.Text = "Clear Loot History", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(85, 95, 120), .ForeColor = Color.White}
        AddHandler btnClearLootHistory.Click,
            Sub(_s As Object, _e As EventArgs)
                SyncLock _lootHistoryEventsSync
                    _lootHistoryEvents.Clear()
                    _lootHistoryVersion += 1
                End SyncLock
                _lastLootHistoryRenderedVersion = -1
                RefreshLootHistoryGrid()
            End Sub
        layout.Controls.Add(btnClearLootHistory, 0, 1)
        Return layout
    End Function

    Private Function BuildKeySummaryPanel() As Control
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(6)}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
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
                SyncLock _keyActionEventsSync
                    _keyActionEvents.Clear()
                End SyncLock
                RefreshKeyActionSummary()
            End Sub
        layout.Controls.Add(btnResetSummary, 0, 2)

        Return layout
    End Function

    Private Sub SeedDefaults()
        UpdateSelectedProcessDisplay(GetSelectedProcessWindow())
        dgvRegions.Rows.Add(True, "hp_bar", "1", "22", "218", "14")
        dgvRegions.Rows.Add(True, "mp_bar", "3", "39", "216", "10")
        dgvRegions.Rows.Add(True, "mob_name_rect", "0", "53", "218", "22")
        dgvRegions.Rows.Add(True, "mob_hp_rect", "0", "78", "215", "12")
        dgvRegions.Rows.Add(True, "mob_life_rect", "0", "78", "215", "12")
        dgvRegions.Rows.Add(True, "unreachable_text_rect", "15", "582", "430", "22")
        dgvRegions.Rows.Add(True, "prana_exp_rect", "472", "745", "78", "21")
        dgvRegions.Rows.Add(True, "rupiahs_rect", "560", "745", "110", "21")
        dgvRegions.Rows.Add(True, "party_invite_scan_rect", "349", "318", "328", "124")
        dgvRegions.Rows.Add(True, "party_invite_ok_rect", "463", "410", "59", "21")
        dgvRegions.Rows.Add(True, "party_list_rect", "0", "24", "168", "244")
        Dim defaultDisconnect As RectRegion = BotConfig.DefaultDisconnectMessageRect()
        dgvRegions.Rows.Add(True, "disconnect_message_rect", defaultDisconnect.X.ToString(), defaultDisconnect.Y.ToString(), defaultDisconnect.W.ToString(), defaultDisconnect.H.ToString())
        Dim defaultDisconnectOk As RectRegion = BotConfig.DefaultDisconnectOkRect()
        dgvRegions.Rows.Add(True, "disconnect_ok_rect", defaultDisconnectOk.X.ToString(), defaultDisconnectOk.Y.ToString(), defaultDisconnectOk.W.ToString(), defaultDisconnectOk.H.ToString())
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
        If chkEvadeDadati IsNot Nothing Then
            chkEvadeDadati.Checked = False
        End If

        Dim keyIndex As Integer = 1
        dgvCombat.Rows.Clear()
        _partyAutoAccept = False
        _partyAskEnabled = False
        _litePartyAskEnabled = False
        _lootScannerEnabled = False
        UpdateLootScannerButtons()
        For Each key In PrimaryKeys
            dgvCombat.Rows.Add(False, key, "1", "attack", keyIndex * 10, 1, 1, 1)
            keyIndex += 1
        Next
        For Each key In FunctionKeys
            dgvCombat.Rows.Add(False, key, "1", "buff", keyIndex * 10, 1, 1, 1)
            keyIndex += 1
        Next
        For i As Integer = 0 To CustomCombatDefaultKeys.Length - 1
            Dim customKey As String = CustomCombatDefaultKeys(i)
            dgvCombat.Rows.Add(False, customKey, "1", "buff", keyIndex * 10, 1, 1, 1)
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
        If chkArrowUnbundleEnabled IsNot Nothing Then
            chkArrowUnbundleEnabled.Checked = False
        End If
        If nudArrowUnbundleSeconds IsNot Nothing Then
            nudArrowUnbundleSeconds.Value = 60D
        End If
        If chkArrowUnbundleOverlay IsNot Nothing Then
            chkArrowUnbundleOverlay.Checked = False
        End If
        _lootNamePickupPointX = -1
        _lootNamePickupPointY = -1
        _isPickingLootNamePickupPoint = False
        _isPickingArrowUnbundlePoint = False
        _arrowUnbundlePoints.Clear()
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
        If chkAutoRelaunchGame IsNot Nothing Then
            chkAutoRelaunchGame.Checked = False
        End If
        If txtAutoRelaunchExePath IsNot Nothing Then
            txtAutoRelaunchExePath.Text = ""
        End If
        If nudAutoRelaunchDelaySeconds IsNot Nothing Then
            nudAutoRelaunchDelaySeconds.Value = 5D
        End If
        ResetAutoRelaunchClickGrid()
        If nudPartyAskSeconds IsNot Nothing Then
            nudPartyAskSeconds.Value = 30
        End If
        If txtPartyAskText IsNot Nothing Then
            txtPartyAskText.Text = DefaultPartyAskCommand
        End If
        _alarmVolumePercent = CInt(nudAlarmVolume.Value)
        UpdateAttackButtonAppearance(False)
        UpdateLootNamePickupPointUi()
        UpdateArrowUnbundleUi()
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

        If edition = BotEdition.Full Then
            ResetHpZeroAlarmState("Alarm state reset for bot start.")
            BeginNotificationWarmup()
        End If
        CommitPendingGridEdits()
        PushLiveConfig()
        engine.Start()
        UpdateAttackButtonAppearance(False)
        If autoStart Then
            AppendLog($"Auto-start on launch enabled for {edition}.")
        End If
        BeginInvoke(New Action(Sub()
                                   SavePersistedListState(False)
                               End Sub))
    End Sub

    Private Sub StopEdition(edition As BotEdition, triggeredByButton As Boolean, context As String)
        Dim engine As BotEngine = GetEngineForEdition(edition)
        Dim hardStopSent As Boolean = engine.HardStopMovement(GetSelectedWindowTitleForFallback(edition), context)
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

    Private Sub InitializeInGameBotToggle()
        If _inGameBotToggleForm IsNot Nothing AndAlso Not _inGameBotToggleForm.IsDisposed Then
            Return
        End If

        _inGameBotToggleForm = New InGameBotToggleForm(
            AddressOf GetInGameBotToggleWindowHandle,
            AddressOf ResolveInGameBotToggleEdition,
            AddressOf IsEditionRunning,
            _inGameBotToggleX,
            _inGameBotToggleY,
            _inGameBotToggleWidth,
            _inGameBotToggleHeight)
        AddHandler _inGameBotToggleForm.ToggleRequested, AddressOf InGameBotToggleRequested
        AddHandler _inGameBotToggleForm.OverlayLayoutChanged, AddressOf InGameBotToggleLayoutChanged
    End Sub

    Private Function ResolveInGameBotToggleEdition() As BotEdition
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If runningEdition.HasValue Then
            _inGameBotToggleEdition = runningEdition.Value
        End If
        Return _inGameBotToggleEdition
    End Function

    Private Function GetInGameBotToggleWindowHandle() As IntPtr
        Dim targetEdition As BotEdition = ResolveInGameBotToggleEdition()
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(targetEdition)
        If selected Is Nothing OrElse Not IsPreferredKathanaWindow(selected) Then
            Return IntPtr.Zero
        End If
        Return selected.MainWindowHandle
    End Function

    Private Sub InGameBotToggleRequested()
        Dim targetEdition As BotEdition = ResolveInGameBotToggleEdition()
        If IsEditionRunning(targetEdition) Then
            StopEdition(targetEdition, True, "in-game toggle")
            Return
        End If

        StartEdition(targetEdition, False)
    End Sub

    Private Sub InGameBotToggleLayoutChanged(clientX As Integer, clientY As Integer, overlayWidth As Integer, overlayHeight As Integer)
        _inGameBotToggleX = Math.Max(0, clientX)
        _inGameBotToggleY = Math.Max(0, clientY)
        _inGameBotToggleWidth = Math.Max(80, Math.Min(320, overlayWidth))
        _inGameBotToggleHeight = Math.Max(30, Math.Min(120, overlayHeight))
        SavePersistedListState(False)
    End Sub

    Private Shared Function GetCurrentApplicationVersionText() As String
        Dim version As Version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        If version Is Nothing Then
            Return "1.0.50"
        End If
        Return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}"
    End Function

    Private Function GetUpdateRepositoryUrl() As String
        Dim raw As String = If(txtUpdateRepositoryUrl IsNot Nothing, txtUpdateRepositoryUrl.Text, DefaultUpdateRepositoryUrl)
        raw = If(raw, "").Trim().TrimEnd("/"c)
        If raw.EndsWith(".git", StringComparison.OrdinalIgnoreCase) Then
            raw = raw.Substring(0, raw.Length - 4)
        End If
        Return raw
    End Function

    Private Function TryCreateUpdateManager(ByRef errorMessage As String) As UpdateManager
        errorMessage = ""
        Dim repositoryUrl As String = GetUpdateRepositoryUrl()
        Dim parsed As Uri = Nothing
        If Not Uri.TryCreate(repositoryUrl, UriKind.Absolute, parsed) OrElse
           (Not parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) AndAlso
            Not parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) OrElse
           parsed.Segments.Length < 3 Then
            errorMessage = "Enter a complete GitHub repository URL, for example https://github.com/ArmandoA88/KATHANABOT."
            Return Nothing
        End If

        Try
            Dim includePrereleases As Boolean = chkUpdateIncludePrereleases IsNot Nothing AndAlso chkUpdateIncludePrereleases.Checked
            Dim source As New GithubSource(repositoryUrl, Nothing, includePrereleases, Nothing)
            Return New UpdateManager(source, Nothing, Nothing)
        Catch ex As Exception
            errorMessage = "Unable to initialize Velopack: " & ex.Message
            Return Nothing
        End Try
    End Function

    Private Sub RefreshUpdateInstallMode()
        If lblUpdateInstallMode Is Nothing Then
            Return
        End If

        Dim errorMessage As String = ""
        Dim manager As UpdateManager = TryCreateUpdateManager(errorMessage)
        If manager Is Nothing Then
            lblUpdateInstallMode.Text = "Install mode: configuration error - " & errorMessage
            lblUpdateInstallMode.ForeColor = Color.LightCoral
        ElseIf manager.IsInstalled Then
            Dim installedVersion As String = If(manager.CurrentVersion IsNot Nothing, manager.CurrentVersion.ToString(), GetCurrentApplicationVersionText())
            lblUpdateInstallMode.Text = $"Install mode: Velopack installed ({installedVersion}) - automatic updates enabled."
            lblUpdateInstallMode.ForeColor = Color.LightGreen
        Else
            lblUpdateInstallMode.Text = "Install mode: standalone EXE - direct GitHub check and verified self-replacement enabled."
            lblUpdateInstallMode.ForeColor = Color.LightGreen
        End If
    End Sub

    Private Sub UpdateSettingsChanged(sender As Object, e As EventArgs)
        If _updateSettingsLoading Then
            Return
        End If

        _updateManager = Nothing
        _pendingUpdateInfo = Nothing
        _pendingStandaloneUpdate = Nothing
        If btnUpdateAndRestart IsNot Nothing Then
            btnUpdateAndRestart.Enabled = False
        End If
        If _updateTab IsNot Nothing Then
            _updateTab.Text = "Update"
        End If
        RefreshUpdateInstallMode()
        SavePersistedListState(False)
    End Sub

    Private Sub OpenUpdateReleasesClicked(sender As Object, e As EventArgs)
        Dim repositoryUrl As String = GetUpdateRepositoryUrl()
        Dim parsed As Uri = Nothing
        If Not Uri.TryCreate(repositoryUrl, UriKind.Absolute, parsed) Then
            MessageBox.Show(Me, "Enter a valid repository URL first.", "Open Releases", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            Process.Start(New ProcessStartInfo(repositoryUrl & "/releases") With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, "Unable to open GitHub Releases: " & ex.Message, "Open Releases", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Async Sub CheckForUpdatesClicked(sender As Object, e As EventArgs)
        Await CheckForUpdatesAsync(True)
    End Sub

    Private Async Function CheckForUpdatesAsync(showErrors As Boolean) As Task
        If _updateOperationInProgress Then
            Return
        End If

        Dim errorMessage As String = ""
        Dim manager As UpdateManager = TryCreateUpdateManager(errorMessage)
        If manager Is Nothing Then
            SetUpdateStatus(errorMessage, Color.LightCoral)
            If showErrors Then
                MessageBox.Show(Me, errorMessage, "KathanaBot Update", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
            Return
        End If

        _updateManager = manager
        RefreshUpdateInstallMode()
        _updateOperationInProgress = True
        SetUpdateControlsBusy(True)
        SetUpdateStatus("Checking GitHub Releases...", Color.LightSkyBlue)
        If progressUpdateDownload IsNot Nothing Then
            progressUpdateDownload.Value = 0
        End If

        Try
            If manager.IsInstalled Then
                Dim updateInfo As UpdateInfo = Await manager.CheckForUpdatesAsync()
                _pendingUpdateInfo = updateInfo
                _pendingStandaloneUpdate = Nothing
                If updateInfo Is Nothing Then
                    ShowNoUpdateAvailable(If(manager.CurrentVersion IsNot Nothing, manager.CurrentVersion.ToString(), GetCurrentApplicationVersionText()), "Velopack installation")
                Else
                    Dim target = updateInfo.TargetFullRelease
                    Dim sizeMb As Double = Math.Max(0.0, CDbl(target.Size)) / 1024.0 / 1024.0
                    SetUpdateStatus($"Version {target.Version} is available.", Color.Gold)
                    If txtUpdateDetails IsNot Nothing Then
                        Dim releaseNotes As String = If(String.IsNullOrWhiteSpace(target.NotesMarkdown), BuildBundledUpdateHistoryText(), target.NotesMarkdown.Trim())
                        txtUpdateDetails.Text = $"Available version: {target.Version}{Environment.NewLine}Current version: {manager.CurrentVersion}{Environment.NewLine}Download: {target.FileName} ({sizeMb:0.00} MB){Environment.NewLine}Mode: Velopack installation{Environment.NewLine}Repository: {GetUpdateRepositoryUrl()}{Environment.NewLine}{Environment.NewLine}WHAT CHANGED{Environment.NewLine}{releaseNotes}{Environment.NewLine}{Environment.NewLine}Press Update and Restart to download and install it."
                    End If
                    MarkUpdateAvailable()
                End If
            Else
                _pendingUpdateInfo = Nothing
                Dim standaloneRelease As StandaloneUpdateRelease = Await CheckStandaloneReleaseAsync()
                _pendingStandaloneUpdate = standaloneRelease
                If standaloneRelease Is Nothing Then
                    ShowNoUpdateAvailable(GetCurrentApplicationVersionText(), "standalone EXE")
                Else
                    Dim sizeMb As Double = Math.Max(0.0, CDbl(standaloneRelease.Size)) / 1024.0 / 1024.0
                    SetUpdateStatus($"Version {standaloneRelease.VersionText} is available.", Color.Gold)
                    If txtUpdateDetails IsNot Nothing Then
                        Dim releaseNotes As String = If(String.IsNullOrWhiteSpace(standaloneRelease.ReleaseNotes), BuildBundledUpdateHistoryText(), standaloneRelease.ReleaseNotes.Trim())
                        txtUpdateDetails.Text = $"Available version: {standaloneRelease.VersionText}{Environment.NewLine}Current version: {GetCurrentApplicationVersionText()}{Environment.NewLine}Download: {standaloneRelease.FileName} ({sizeMb:0.00} MB){Environment.NewLine}Mode: standalone EXE self-replacement{Environment.NewLine}Security: SHA-256 checksum required{Environment.NewLine}Repository: {GetUpdateRepositoryUrl()}{Environment.NewLine}{Environment.NewLine}WHAT CHANGED{Environment.NewLine}{releaseNotes}{Environment.NewLine}{Environment.NewLine}Press Update and Restart. No Setup installation is needed."
                    End If
                    MarkUpdateAvailable()
                End If
            End If
        Catch ex As Exception
            _pendingUpdateInfo = Nothing
            _pendingStandaloneUpdate = Nothing
            SetUpdateStatus("Update check failed: " & ex.Message, Color.LightCoral)
            If txtUpdateDetails IsNot Nothing Then
                txtUpdateDetails.Text = "GitHub update check failed." & Environment.NewLine & Environment.NewLine & ex.ToString()
            End If
            If showErrors Then
                MessageBox.Show(Me, "Unable to check for updates: " & ex.Message, "KathanaBot Update", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        Finally
            _updateOperationInProgress = False
            SetUpdateControlsBusy(False)
        End Try
    End Function

    Private Sub ShowNoUpdateAvailable(currentVersion As String, mode As String)
        SetUpdateStatus("You already have the latest version.", Color.LightGreen)
        If txtUpdateDetails IsNot Nothing Then
            txtUpdateDetails.Text = $"Current version: {currentVersion}{Environment.NewLine}Mode: {mode}{Environment.NewLine}Repository: {GetUpdateRepositoryUrl()}{Environment.NewLine}Checked: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{Environment.NewLine}{BuildBundledUpdateHistoryText()}"
        End If
        If _updateTab IsNot Nothing Then
            _updateTab.Text = "Update"
        End If
    End Sub

    Private Sub MarkUpdateAvailable()
        If _updateTab IsNot Nothing Then
            _updateTab.Text = "Update !"
        End If
    End Sub

    Private Async Function CheckStandaloneReleaseAsync() As Task(Of StandaloneUpdateRelease)
        Dim repositoryUri As New Uri(GetUpdateRepositoryUrl())
        Dim parts As String() = repositoryUri.AbsolutePath.Trim("/"c).Split("/"c, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length < 2 Then
            Throw New InvalidOperationException("The GitHub repository URL must contain an owner and repository name.")
        End If

        Dim owner As String = Uri.EscapeDataString(parts(0))
        Dim repository As String = Uri.EscapeDataString(parts(1))
        Dim apiUrl As String = $"https://api.github.com/repos/{owner}/{repository}/releases?per_page=20"
        Dim includePrereleases As Boolean = chkUpdateIncludePrereleases IsNot Nothing AndAlso chkUpdateIncludePrereleases.Checked
        Dim currentVersion As Version = Nothing
        If Not Version.TryParse(GetCurrentApplicationVersionText(), currentVersion) Then
            Throw New InvalidOperationException("The current application version could not be read.")
        End If

        Using client As New HttpClient()
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KathanaBot-Updater/" & GetCurrentApplicationVersionText())
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json")
            Using response As HttpResponseMessage = Await client.GetAsync(apiUrl)
                response.EnsureSuccessStatusCode()
                Dim raw As String = Await response.Content.ReadAsStringAsync()
                Using document As JsonDocument = JsonDocument.Parse(raw)
                    For Each releaseElement As JsonElement In document.RootElement.EnumerateArray()
                        If GetUpdateJsonBoolean(releaseElement, "draft") Then
                            Continue For
                        End If
                        If GetUpdateJsonBoolean(releaseElement, "prerelease") AndAlso Not includePrereleases Then
                            Continue For
                        End If

                        Dim targetBranch As String = GetUpdateJsonString(releaseElement, "target_commitish")
                        If Not String.IsNullOrWhiteSpace(targetBranch) AndAlso
                           Not targetBranch.Equals("agent-ai", StringComparison.OrdinalIgnoreCase) Then
                            Continue For
                        End If

                        Dim tagName As String = GetUpdateJsonString(releaseElement, "tag_name")
                        Dim releaseVersion As Version = ParseReleaseVersion(tagName)
                        If releaseVersion Is Nothing OrElse releaseVersion <= currentVersion Then
                            Continue For
                        End If

                        Dim executableAssetName As String = "KathanaBotControlPanel-win-x64-standalone.exe"
                        Dim checksumAssetName As String = executableAssetName & ".sha256"
                        Dim executableUrl As String = ""
                        Dim checksumUrl As String = ""
                        Dim executableSize As Long = 0
                        Dim assetsElement As JsonElement
                        If releaseElement.TryGetProperty("assets", assetsElement) Then
                            For Each assetElement As JsonElement In assetsElement.EnumerateArray()
                                Dim assetName As String = GetUpdateJsonString(assetElement, "name")
                                If assetName.Equals(executableAssetName, StringComparison.OrdinalIgnoreCase) Then
                                    executableUrl = GetUpdateJsonString(assetElement, "browser_download_url")
                                    Dim sizeElement As JsonElement
                                    If assetElement.TryGetProperty("size", sizeElement) AndAlso sizeElement.ValueKind = JsonValueKind.Number Then
                                        sizeElement.TryGetInt64(executableSize)
                                    End If
                                ElseIf assetName.Equals(checksumAssetName, StringComparison.OrdinalIgnoreCase) Then
                                    checksumUrl = GetUpdateJsonString(assetElement, "browser_download_url")
                                End If
                            Next
                        End If

                        If String.IsNullOrWhiteSpace(executableUrl) OrElse String.IsNullOrWhiteSpace(checksumUrl) Then
                            Throw New InvalidOperationException($"Release {tagName} does not contain both {executableAssetName} and its .sha256 file. Run the updated release workflow on agent-ai.")
                        End If

                        Return New StandaloneUpdateRelease With {
                            .Version = releaseVersion,
                            .VersionText = tagName.TrimStart("v"c, "V"c),
                            .FileName = executableAssetName,
                            .DownloadUrl = executableUrl,
                            .Sha256Url = checksumUrl,
                            .Size = executableSize,
                            .ReleaseUrl = GetUpdateJsonString(releaseElement, "html_url"),
                            .ReleaseNotes = GetUpdateJsonString(releaseElement, "body")
                        }
                    Next
                End Using
            End Using
        End Using

        Return Nothing
    End Function

    Private Shared Function GetUpdateJsonString(element As JsonElement, propertyName As String) As String
        Dim value As JsonElement
        If element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.String Then
            Return If(value.GetString(), "")
        End If
        Return ""
    End Function

    Private Shared Function BuildBundledUpdateHistoryText() As String
        Return String.Join(Environment.NewLine, New String() {
            "WHAT CHANGED IN 1.0.50",
            "- Added Evade Dadatis in Vision. Fresh Dadati OCR blocks attacks, taps W/S, and forces an E retarget while the game window is on.",
            "- Improved party counting for partial parties, dead members, dark-red HP bars, and terrain-heavy backgrounds.",
            "- Added release notes and recent change history directly to Automatic Updates.",
            "",
            "RECENT CHANGE HISTORY (LAST 5)",
            "1. Dadati evade: avoids the unkillable Dadati target by moving and retargeting instead of attacking forever.",
            "2. Party status: counts non-full parties more accurately and separates living from dead members.",
            "3. Mob-name OCR: compares multiple enhanced samples and keeps the strongest complete name through capture flicker.",
            "4. Combat cooldowns: uses monotonic per-skill timing so attacks do not remain incorrectly stuck on cooldown.",
            "5. Startup Notice: automatically closes after five seconds while keeping the OK button available."
        })
    End Function

    Private Shared Function GetUpdateJsonBoolean(element As JsonElement, propertyName As String) As Boolean
        Dim value As JsonElement
        Return element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.True
    End Function

    Private Shared Function ParseReleaseVersion(tagName As String) As Version
        Dim match As Match = Regex.Match(If(tagName, ""), "(?i)^v?(\d+)\.(\d+)\.(\d+)")
        If Not match.Success Then
            Return Nothing
        End If
        Return New Version(Integer.Parse(match.Groups(1).Value), Integer.Parse(match.Groups(2).Value), Integer.Parse(match.Groups(3).Value))
    End Function

    Private Async Sub UpdateAndRestartClicked(sender As Object, e As EventArgs)
        Dim velopackUpdateReady As Boolean = _updateManager IsNot Nothing AndAlso _updateManager.IsInstalled AndAlso _pendingUpdateInfo IsNot Nothing
        Dim standaloneUpdateReady As Boolean = _pendingStandaloneUpdate IsNot Nothing
        If _updateOperationInProgress OrElse (Not velopackUpdateReady AndAlso Not standaloneUpdateReady) Then
            Return
        End If

        Dim runningEdition As BotEdition? = GetRunningEdition()
        Dim targetVersion As String = If(standaloneUpdateReady, _pendingStandaloneUpdate.VersionText, _pendingUpdateInfo.TargetFullRelease.Version.ToString())
        Dim prompt As String = $"Download and install KathanaBot {targetVersion}?"
        If standaloneUpdateReady Then
            prompt &= Environment.NewLine & Environment.NewLine & "The standalone EXE will verify the SHA-256 checksum, replace itself, and reopen automatically."
        End If
        If runningEdition.HasValue Then
            prompt &= Environment.NewLine & Environment.NewLine & $"The running {runningEdition.Value} bot will be stopped safely before the application restarts."
        End If
        If MessageBox.Show(Me, prompt, "Update and Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        _updateOperationInProgress = True
        _updateCancellation?.Cancel()
        _updateCancellation?.Dispose()
        _updateCancellation = New CancellationTokenSource()
        SetUpdateControlsBusy(True)
        SetUpdateStatus("Downloading update...", Color.LightSkyBlue)
        If progressUpdateDownload IsNot Nothing Then
            progressUpdateDownload.Value = 0
        End If

        Try
            If standaloneUpdateReady Then
                Await DownloadStandaloneUpdateAndRestartAsync(_pendingStandaloneUpdate, _updateCancellation.Token)
            Else
                Dim progress As Action(Of Integer) = AddressOf ReportUpdateDownloadProgress
                Await _updateManager.DownloadUpdatesAsync(_pendingUpdateInfo, progress, _updateCancellation.Token)
                progressUpdateDownload.Value = 100
                SetUpdateStatus("Download complete. Stopping safely and restarting...", Color.LightGreen)
                StopRunningBotForUpdate()
                SavePersistedListState(False)
                FlushPendingLogLines()
                _updateManager.ApplyUpdatesAndRestart(_pendingUpdateInfo.TargetFullRelease, Array.Empty(Of String)())
            End If
        Catch ex As OperationCanceledException
            SetUpdateStatus("Update download canceled.", Color.Khaki)
        Catch ex As Exception
            SetUpdateStatus("Update failed: " & ex.Message, Color.LightCoral)
            MessageBox.Show(Me, "Unable to install the update: " & ex.Message, "KathanaBot Update", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _updateOperationInProgress = False
            SetUpdateControlsBusy(False)
        End Try
    End Sub

    Private Sub ReportUpdateDownloadProgress(value As Integer)
        If IsDisposed OrElse Not IsHandleCreated Then
            Return
        End If
        BeginInvoke(New Action(Sub()
                                   Dim bounded As Integer = Math.Max(0, Math.Min(100, value))
                                   progressUpdateDownload.Value = bounded
                                   SetUpdateStatus($"Downloading update... {bounded}%", Color.LightSkyBlue)
                               End Sub))
    End Sub

    Private Sub StopRunningBotForUpdate()
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If runningEdition.HasValue Then
            StopEdition(runningEdition.Value, True, "software update")
        End If
    End Sub

    Private Async Function DownloadStandaloneUpdateAndRestartAsync(release As StandaloneUpdateRelease, cancellationToken As CancellationToken) As Task
        Dim currentExecutable As String = Environment.ProcessPath
        If String.IsNullOrWhiteSpace(currentExecutable) OrElse Not File.Exists(currentExecutable) Then
            Throw New InvalidOperationException("The running standalone EXE path could not be determined.")
        End If

        Dim temporaryExecutable As String = Path.Combine(Path.GetTempPath(), $"KathanaBot-{release.VersionText}-{Guid.NewGuid():N}.exe")
        Dim expectedHash As String = ""
        Try
            Using client As New HttpClient()
                client.DefaultRequestHeaders.UserAgent.ParseAdd("KathanaBot-Updater/" & GetCurrentApplicationVersionText())
                client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream")

                Dim expectedHashText As String = Await client.GetStringAsync(release.Sha256Url, cancellationToken)
                expectedHash = Regex.Match(expectedHashText, "(?i)\b[0-9a-f]{64}\b").Value.ToUpperInvariant()
                If expectedHash.Length <> 64 Then
                    Throw New InvalidDataException("The release checksum file is invalid.")
                End If

                Using response As HttpResponseMessage = Await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    response.EnsureSuccessStatusCode()
                    Dim totalBytes As Long = If(response.Content.Headers.ContentLength.HasValue, response.Content.Headers.ContentLength.Value, release.Size)
                    Using source As Stream = Await response.Content.ReadAsStreamAsync(cancellationToken)
                        Using destination As New FileStream(temporaryExecutable, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, True)
                            Dim buffer(1024 * 128 - 1) As Byte
                            Dim received As Long = 0
                            Do
                                Dim count As Integer = Await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                                If count = 0 Then
                                    Exit Do
                                End If
                                Await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken)
                                received += count
                                If totalBytes > 0 Then
                                    ReportUpdateDownloadProgress(CInt(Math.Min(100, received * 100L \ totalBytes)))
                                End If
                            Loop
                        End Using
                    End Using
                End Using
            End Using

            SetUpdateStatus("Verifying SHA-256 checksum...", Color.LightSkyBlue)
            Dim actualHash As String
            Using updateStream As FileStream = File.OpenRead(temporaryExecutable)
                Dim hashBytes As Byte() = Await SHA256.HashDataAsync(updateStream, cancellationToken)
                actualHash = Convert.ToHexString(hashBytes)
            End Using
            If Not actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidDataException("The downloaded EXE failed SHA-256 verification and was not installed.")
            End If

            progressUpdateDownload.Value = 100
            SetUpdateStatus("Verified. Stopping safely, replacing the EXE, and restarting...", Color.LightGreen)
            StopRunningBotForUpdate()
            SavePersistedListState(False)
            FlushPendingLogLines()
            StartStandaloneReplacement(currentExecutable, temporaryExecutable)
            Application.Exit()
        Catch
            Try
                If File.Exists(temporaryExecutable) Then
                    File.Delete(temporaryExecutable)
                End If
            Catch
            End Try
            Throw
        End Try
    End Function

    Private Shared Sub StartStandaloneReplacement(currentExecutable As String, downloadedExecutable As String)
        Dim escapedCurrent As String = currentExecutable.Replace("'", "''")
        Dim escapedDownloaded As String = downloadedExecutable.Replace("'", "''")
        Dim escapedWorkingDirectory As String = Path.GetDirectoryName(currentExecutable).Replace("'", "''")
        Dim processId As Integer = Environment.ProcessId
        Dim script As String =
            $"$ErrorActionPreference='Stop'; Wait-Process -Id {processId} -ErrorAction SilentlyContinue; " &
            $"$source='{escapedDownloaded}'; $target='{escapedCurrent}'; $copied=$false; " &
            "for($i=0; $i -lt 60 -and -not $copied; $i++){ try { Copy-Item -LiteralPath $source -Destination $target -Force; $copied=$true } catch { Start-Sleep -Milliseconds 500 } }; " &
            "if(-not $copied){ exit 1 }; Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue; " &
            $"Start-Process -FilePath $target -WorkingDirectory '{escapedWorkingDirectory}'"
        Dim encodedCommand As String = Convert.ToBase64String(Encoding.Unicode.GetBytes(script))
        Process.Start(New ProcessStartInfo("powershell.exe") With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden,
            .Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " & encodedCommand
        })
    End Sub

    Private Sub SetUpdateStatus(message As String, color As Color)
        If lblUpdateStatus IsNot Nothing Then
            lblUpdateStatus.Text = message
            lblUpdateStatus.ForeColor = color
        End If
    End Sub

    Private Sub SetUpdateControlsBusy(busy As Boolean)
        If btnCheckForUpdates IsNot Nothing Then
            btnCheckForUpdates.Enabled = Not busy
        End If
        If btnUpdateAndRestart IsNot Nothing Then
            Dim velopackReady As Boolean = _pendingUpdateInfo IsNot Nothing AndAlso _updateManager IsNot Nothing AndAlso _updateManager.IsInstalled
            btnUpdateAndRestart.Enabled = Not busy AndAlso (velopackReady OrElse _pendingStandaloneUpdate IsNot Nothing)
        End If
        If txtUpdateRepositoryUrl IsNot Nothing Then
            txtUpdateRepositoryUrl.Enabled = Not busy
        End If
        If chkUpdateIncludePrereleases IsNot Nothing Then
            chkUpdateIncludePrereleases.Enabled = Not busy
        End If
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
        AutoStartOnLaunch()
        RefreshProcessWindowList(False, IntPtr.Zero)
        BeginInvoke(New Action(AddressOf ShowStartupNoticeAndCheckUpdates))
    End Sub

    Private Async Sub ShowStartupNoticeAndCheckUpdates()
        Using notice As New StartupNoticeForm(Program.StartupNotice)
            notice.ShowDialog(Me)
        End Using
        RefreshUpdateInstallMode()
        If chkUpdateCheckAtStartup IsNot Nothing AndAlso chkUpdateCheckAtStartup.Checked Then
            Await CheckForUpdatesAsync(False)
        End If
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

        If dgvAutoRelaunchClicks IsNot Nothing Then
            Try
                If dgvAutoRelaunchClicks.IsCurrentCellDirty Then
                    dgvAutoRelaunchClicks.CommitEdit(DataGridViewDataErrorContexts.Commit)
                End If
                dgvAutoRelaunchClicks.EndEdit()
            Catch
            End Try
        End If
    End Sub

    Private Sub CombatGridCurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If dgvCombat Is Nothing OrElse dgvCombat.CurrentCell Is Nothing OrElse Not dgvCombat.IsCurrentCellDirty Then
            Return
        End If

        Dim column As DataGridViewColumn = dgvCombat.Columns(dgvCombat.CurrentCell.ColumnIndex)
        If TypeOf column Is DataGridViewCheckBoxColumn OrElse TypeOf column Is DataGridViewComboBoxColumn Then
            dgvCombat.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub AutoRelaunchClicksCurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If dgvAutoRelaunchClicks Is Nothing OrElse dgvAutoRelaunchClicks.CurrentCell Is Nothing OrElse Not dgvAutoRelaunchClicks.IsCurrentCellDirty Then
            Return
        End If

        Dim column As DataGridViewColumn = dgvAutoRelaunchClicks.Columns(dgvAutoRelaunchClicks.CurrentCell.ColumnIndex)
        If TypeOf column Is DataGridViewCheckBoxColumn Then
            dgvAutoRelaunchClicks.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub CombatGridEditingControlShowing(sender As Object, e As DataGridViewEditingControlShowingEventArgs)
        Dim textEditor As TextBox = TryCast(e.Control, TextBox)
        If textEditor Is Nothing Then
            Return
        End If

        textEditor.MaxLength = 1024
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

    Private Shared Function ResolveDefaultPeriodicScreenshotDirectory() As String
        Dim picturesDirectory As String = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        If String.IsNullOrWhiteSpace(picturesDirectory) Then
            picturesDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        End If
        If String.IsNullOrWhiteSpace(picturesDirectory) Then
            picturesDirectory = PersistDirectoryPath
        End If
        Return Path.Combine(picturesDirectory, "KathanaBot")
    End Function

    Private Function GetPeriodicScreenshotDirectory() As String
        If txtPeriodicScreenshotDirectory Is Nothing OrElse String.IsNullOrWhiteSpace(txtPeriodicScreenshotDirectory.Text) Then
            Return DefaultPeriodicScreenshotDirectoryPath
        End If
        Return txtPeriodicScreenshotDirectory.Text.Trim()
    End Function

    Private Sub BrowsePeriodicScreenshotDirectoryClicked(sender As Object, e As EventArgs)
        Using dialog As New FolderBrowserDialog()
            dialog.Description = "Select where automatic game screenshots will be saved."
            dialog.ShowNewFolderButton = True
            Dim currentDirectory As String = GetPeriodicScreenshotDirectory()
            If Directory.Exists(currentDirectory) Then
                dialog.SelectedPath = currentDirectory
            Else
                Dim parentDirectory As String = Path.GetDirectoryName(currentDirectory)
                If Not String.IsNullOrWhiteSpace(parentDirectory) AndAlso Directory.Exists(parentDirectory) Then
                    dialog.SelectedPath = parentDirectory
                End If
            End If

            If dialog.ShowDialog(Me) <> DialogResult.OK OrElse String.IsNullOrWhiteSpace(dialog.SelectedPath) Then
                Return
            End If

            txtPeriodicScreenshotDirectory.Text = dialog.SelectedPath.Trim()
            ConfigurePeriodicScreenshotTimer()
            SavePersistedListState(False)
        End Using
    End Sub

    Private Sub OpenPeriodicScreenshotDirectoryClicked(sender As Object, e As EventArgs)
        Dim screenshotDirectory As String = GetPeriodicScreenshotDirectory()
        Try
            Directory.CreateDirectory(screenshotDirectory)
            Process.Start(New ProcessStartInfo(screenshotDirectory) With {.UseShellExecute = True})
        Catch ex As Exception
            Dim message As String = "Unable to open the automatic screenshot folder: " & ex.Message
            AppendLog(message)
            MessageBox.Show(Me, message, "Open Screenshot Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub PeriodicScreenshotSettingsChanged(sender As Object, e As EventArgs)
        If _periodicScreenshotSettingsLoading Then
            Return
        End If
        ConfigurePeriodicScreenshotTimer()
        SavePersistedListState(False)
    End Sub

    Private Sub ConfigurePeriodicScreenshotTimer()
        _periodicScreenshotTimer.Stop()

        Dim enabled As Boolean = chkPeriodicScreenshots IsNot Nothing AndAlso chkPeriodicScreenshots.Checked
        Dim intervalMinutes As Integer = 15
        If nudPeriodicScreenshotMinutes IsNot Nothing Then
            intervalMinutes = Math.Max(1, Math.Min(999, CInt(nudPeriodicScreenshotMinutes.Value)))
        End If

        If enabled Then
            _periodicScreenshotTimer.Interval = intervalMinutes * 60 * 1000
            _periodicScreenshotTimer.Start()
        End If

        If lblPeriodicScreenshotStatus IsNot Nothing Then
            lblPeriodicScreenshotStatus.Text = If(enabled, $"On - next capture in {intervalMinutes} min", "Off")
            lblPeriodicScreenshotStatus.ForeColor = If(enabled, Color.LightGreen, Color.LightSteelBlue)
        End If
    End Sub

    Private Sub PeriodicScreenshotTimerTick(sender As Object, e As EventArgs)
        If chkPeriodicScreenshots Is Nothing OrElse Not chkPeriodicScreenshots.Checked OrElse _periodicScreenshotInProgress Then
            Return
        End If

        _periodicScreenshotInProgress = True
        PushLiveConfig()
        Dim engine As BotEngine = GetRollingScreenshotEngine()
        Dim screenshotDirectory As String = GetPeriodicScreenshotDirectory()
        Task.Run(
            Sub()
                Try
                    SavePeriodicScreenshot(engine, screenshotDirectory)
                Finally
                    _periodicScreenshotInProgress = False
                End Try
            End Sub)
    End Sub

    Private Sub SavePeriodicScreenshot(engine As BotEngine, screenshotDirectory As String)
        If engine Is Nothing Then
            Return
        End If

        Try
            Directory.CreateDirectory(screenshotDirectory)
            Using bmp As Bitmap = engine.CaptureSnapshot()
                If bmp Is Nothing Then
                    LogPeriodicScreenshotIssue("Automatic screenshot skipped: game capture unavailable.")
                    Return
                End If

                Dim fileName As String = $"kathana_auto_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"
                Dim screenshotPath As String = Path.Combine(screenshotDirectory, fileName)
                bmp.Save(screenshotPath, ImageFormat.Png)
                AppendLogSafe("Automatic screenshot saved: " & screenshotPath)
            End Using
        Catch ex As Exception
            LogPeriodicScreenshotIssue("Automatic screenshot failed: " & ex.Message)
        End Try
    End Sub

    Private Sub LogPeriodicScreenshotIssue(message As String)
        Dim now As DateTime = DateTime.UtcNow
        If _lastPeriodicScreenshotErrorLogUtc <> DateTime.MinValue AndAlso (now - _lastPeriodicScreenshotErrorLogUtc).TotalMinutes < 5 Then
            Return
        End If

        _lastPeriodicScreenshotErrorLogUtc = now
        AppendLogSafe(message)
    End Sub

    Private Sub RollingScreenshotTimerTick(sender As Object, e As EventArgs)
        If _rollingScreenshotInProgress Then
            Return
        End If

        _rollingScreenshotInProgress = True
        PushLiveConfig()
        Dim engine As BotEngine = GetRollingScreenshotEngine()
        Task.Run(
            Sub()
                Try
                    SaveRollingScreenshot(engine)
                Finally
                    _rollingScreenshotInProgress = False
                End Try
            End Sub)
    End Sub

    Private Function GetRollingScreenshotEngine() As BotEngine
        If _fullEngine.IsRunning() Then
            Return _fullEngine
        End If
        If _liteEngine.IsRunning() Then
            Return _liteEngine
        End If
        Return If(IsLiteModeActive(), _liteEngine, _fullEngine)
    End Function

    Private Sub SaveRollingScreenshot(engine As BotEngine)
        If engine Is Nothing Then
            Return
        End If

        Try
            Directory.CreateDirectory(RollingScreenshotDirectoryPath)

            Using bmp As Bitmap = engine.CaptureSnapshot()
                If bmp Is Nothing Then
                    LogRollingScreenshotIssue("Rolling screenshot skipped: game capture unavailable.")
                    PruneRollingScreenshots()
                    Return
                End If

                Dim fileName As String = $"kathana_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"
                Dim screenshotPath As String = Path.Combine(RollingScreenshotDirectoryPath, fileName)
                bmp.Save(screenshotPath, ImageFormat.Png)
                PruneRollingScreenshots()

                _rollingScreenshotSaveCount += 1
                If _rollingScreenshotSaveCount = 1 Then
                    AppendLogSafe($"Rolling screenshots enabled: saving every 30 seconds to {RollingScreenshotDirectoryPath}.")
                End If
            End Using
        Catch ex As Exception
            LogRollingScreenshotIssue("Rolling screenshot failed: " & ex.Message)
        End Try
    End Sub

    Private Sub PruneRollingScreenshots()
        Try
            If Not Directory.Exists(RollingScreenshotDirectoryPath) Then
                Return
            End If

            Dim files As List(Of FileInfo) =
                Directory.GetFiles(RollingScreenshotDirectoryPath, "kathana_*.png").
                    Select(Function(filePath) New FileInfo(filePath)).
                    OrderByDescending(Function(file) file.Name).
                    ToList()

            For Each oldFile As FileInfo In files.Skip(RollingScreenshotRetainCount)
                Try
                    oldFile.Delete()
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

    Private Function GetLatestRollingScreenshotPath() As String
        Try
            If Not Directory.Exists(RollingScreenshotDirectoryPath) Then
                Return ""
            End If

            Return Directory.GetFiles(RollingScreenshotDirectoryPath, "kathana_*.png").
                OrderByDescending(Function(filePath) Path.GetFileName(filePath)).
                FirstOrDefault()
        Catch
            Return ""
        End Try
    End Function

    Private Sub DiscordShotTimerTick(sender As Object, e As EventArgs)
        If _discordShotPollInProgress Then
            Return
        End If
        If GetNotificationProviderName() <> NotificationProviderDiscord Then
            _discordShotInitialized = False
            _lastDiscordShotMessageId = ""
            Return
        End If

        Dim botToken As String = GetDiscordShotBotToken()
        Dim channelId As String = GetDiscordShotChannelId()
        Dim webhookUrl As String = GetDiscordStatsWebhookUrl()
        If botToken = "" OrElse channelId = "" OrElse webhookUrl = "" Then
            _discordShotInitialized = False
            _lastDiscordShotMessageId = ""
            Return
        End If

        _discordShotPollInProgress = True
        Task.Run(
            Async Function()
                Try
                    Await PollDiscordShotCommandAsync(botToken, channelId, webhookUrl)
                Finally
                    _discordShotPollInProgress = False
                End Try
            End Function)
    End Sub

    Private Async Function PollDiscordShotCommandAsync(botToken As String, channelId As String, webhookUrl As String) As Task
        Try
            Dim requestUrl As String = $"https://discord.com/api/v10/channels/{Uri.EscapeDataString(channelId)}/messages?limit=10"
            Using request As New HttpRequestMessage(HttpMethod.Get, requestUrl)
                request.Headers.Authorization = New System.Net.Http.Headers.AuthenticationHeaderValue("Bot", botToken)
                Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                If Not response.IsSuccessStatusCode Then
                    LogDiscordShotIssue($"Discord shot poll failed ({CInt(response.StatusCode)}). Check bot token, channel ID, and channel permissions.")
                    Return
                End If

                Dim rawJson As String = Await response.Content.ReadAsStringAsync()
                Using doc As JsonDocument = JsonDocument.Parse(rawJson)
                    If doc.RootElement.ValueKind <> JsonValueKind.Array OrElse doc.RootElement.GetArrayLength() = 0 Then
                        Return
                    End If

                    Dim newestId As String = GetJsonString(doc.RootElement(0), "id")
                    If newestId = "" Then
                        Return
                    End If

                    If Not _discordShotInitialized OrElse _lastDiscordShotMessageId = "" Then
                        _lastDiscordShotMessageId = newestId
                        _discordShotInitialized = True
                        Return
                    End If

                    Dim pendingMessages As New List(Of JsonElement)()
                    For Each message As JsonElement In doc.RootElement.EnumerateArray()
                        Dim messageId As String = GetJsonString(message, "id")
                        If messageId = "" OrElse messageId = _lastDiscordShotMessageId Then
                            Exit For
                        End If
                        pendingMessages.Add(message.Clone())
                    Next

                    _lastDiscordShotMessageId = newestId
                    pendingMessages.Reverse()
                    For Each message As JsonElement In pendingMessages
                        If IsDiscordShotCommand(message) Then
                            Await SendLatestRollingScreenshotToDiscordAsync(webhookUrl)
                        End If
                    Next
                End Using
            End Using
        Catch ex As Exception
            LogDiscordShotIssue("Discord shot poll failed: " & ex.Message)
        End Try
    End Function

    Private Shared Function GetJsonString(element As JsonElement, propertyName As String) As String
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.String Then
            Return If(value.GetString(), "")
        End If
        Return ""
    End Function

    Private Shared Function IsDiscordShotCommand(message As JsonElement) As Boolean
        Dim author As JsonElement
        If message.TryGetProperty("author", author) Then
            Dim botValue As JsonElement
            If author.TryGetProperty("bot", botValue) AndAlso botValue.ValueKind = JsonValueKind.True Then
                Return False
            End If
        End If

        Dim content As String = GetJsonString(message, "content").Trim()
        Return content.Equals("shot", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Async Function SendLatestRollingScreenshotToDiscordAsync(webhookUrl As String) As Task(Of Boolean)
        Dim screenshotPath As String = GetLatestRollingScreenshotPath()
        If String.IsNullOrWhiteSpace(screenshotPath) OrElse Not File.Exists(screenshotPath) Then
            Return Await SendDiscordNotificationAsync("KathanaBot Shot", "No rolling screenshot is available yet. Wait for the next 30-second capture.", webhookUrl, "Discord stats webhook")
        End If

        Dim rawWebhookUrl As String = If(webhookUrl, "").Trim()
        If rawWebhookUrl = "" OrElse Not IsLikelyDiscordWebhookUrl(rawWebhookUrl) Then
            LogDiscordShotIssue("Discord shot upload skipped: Stats/Data webhook URL is missing or invalid.")
            Return False
        End If

        Try
            Using request As New HttpRequestMessage(HttpMethod.Post, NormalizeDiscordWebhookUrl(rawWebhookUrl))
                Using content As New MultipartFormDataContent()
                    Dim payload = New With {
                        .username = "KathanaBot",
                        .content = $"Latest screenshot: {Path.GetFileName(screenshotPath)}",
                        .allowed_mentions = New With {
                            .parse = Array.Empty(Of String)()
                        }
                    }
                    content.Add(New StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), "payload_json")

                    Dim fileBytes As Byte() = File.ReadAllBytes(screenshotPath)
                    Dim fileContent As New ByteArrayContent(fileBytes)
                    fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("image/png")
                    content.Add(fileContent, "files[0]", Path.GetFileName(screenshotPath))
                    request.Content = content

                    Dim response As HttpResponseMessage = Await NtfyClient.SendAsync(request)
                    If response.IsSuccessStatusCode Then
                        AppendLogSafe("Discord shot command uploaded latest rolling screenshot.")
                        Return True
                    End If

                    Dim responseText As String = ""
                    If response.Content IsNot Nothing Then
                        responseText = (Await response.Content.ReadAsStringAsync()).Trim()
                    End If
                    If responseText <> "" Then
                        LogDiscordShotIssue($"Discord shot upload failed ({CInt(response.StatusCode)}): {responseText}")
                    Else
                        LogDiscordShotIssue($"Discord shot upload failed ({CInt(response.StatusCode)}).")
                    End If
                End Using
            End Using
        Catch ex As Exception
            LogDiscordShotIssue("Discord shot upload failed: " & ex.Message)
        End Try

        Return False
    End Function

    Private Sub LogRollingScreenshotIssue(message As String)
        Dim now As DateTime = DateTime.UtcNow
        If _lastRollingScreenshotErrorLogUtc <> DateTime.MinValue AndAlso (now - _lastRollingScreenshotErrorLogUtc).TotalMinutes < 5 Then
            Return
        End If

        _lastRollingScreenshotErrorLogUtc = now
        AppendLogSafe(message)
    End Sub

    Private Sub LogDiscordShotIssue(message As String)
        Dim now As DateTime = DateTime.UtcNow
        If _lastDiscordShotErrorLogUtc <> DateTime.MinValue AndAlso (now - _lastDiscordShotErrorLogUtc).TotalMinutes < 2 Then
            Return
        End If

        _lastDiscordShotErrorLogUtc = now
        AppendLogSafe(message)
    End Sub

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
        _isPickingArrowUnbundlePoint = False
        UpdateLootNamePickupPointUi()
        UpdateArrowUnbundleUi()
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
        _isPickingArrowUnbundlePoint = False
        UpdateLootRejectPointUi()
        UpdateArrowUnbundleUi()
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

    Private Sub PickArrowUnbundlePointClicked(sender As Object, e As EventArgs)
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
            AppendLog("Arrow unbundle: select a Full game process window first.")
            Return
        End If

        _isPickingArrowUnbundlePoint = True
        _arrowUnbundleLeftMouseWasDown = False
        _isPickingLootRejectPoint = False
        _isPickingLootNamePickupPoint = False
        UpdateLootRejectPointUi()
        UpdateLootNamePickupPointUi()
        UpdateArrowUnbundleUi()
        AppendLog("Arrow unbundle: click the inventory arrow stack spot directly inside the selected game window.")
        NativeMethods.SetForegroundWindow(selected.MainWindowHandle)
    End Sub

    Private Sub RemoveArrowUnbundlePointClicked(sender As Object, e As EventArgs)
        If lstArrowUnbundlePoints Is Nothing OrElse lstArrowUnbundlePoints.SelectedIndex < 0 OrElse lstArrowUnbundlePoints.SelectedIndex >= _arrowUnbundlePoints.Count Then
            Return
        End If

        Dim removed As LootScanPoint = _arrowUnbundlePoints(lstArrowUnbundlePoints.SelectedIndex)
        _arrowUnbundlePoints.RemoveAt(lstArrowUnbundlePoints.SelectedIndex)
        UpdateArrowUnbundleUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog($"Arrow unbundle point removed: x={removed.X}, y={removed.Y}.")
    End Sub

    Private Sub ClearArrowUnbundlePointsClicked(sender As Object, e As EventArgs)
        _isPickingArrowUnbundlePoint = False
        _arrowUnbundleLeftMouseWasDown = False
        _arrowUnbundlePoints.Clear()
        UpdateArrowUnbundleUi()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog("Arrow unbundle points cleared.")
    End Sub

    Private Sub HandlePendingArrowUnbundlePointCapture()
        Try
            If Not _isPickingArrowUnbundlePoint Then
                Return
            End If

            Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
                Return
            End If

            Dim leftDown As Boolean = (GetAsyncKeyState(CInt(Keys.LButton)) And &H8000S) <> 0
            If leftDown AndAlso Not _arrowUnbundleLeftMouseWasDown Then
                Dim screenPoint As NativeMethods.POINT
                If NativeMethods.GetCursorPos(screenPoint) Then
                    Dim hoveredWindow As IntPtr = NativeMethods.WindowFromPoint(screenPoint)
                    Dim hoveredRoot As IntPtr = If(hoveredWindow <> IntPtr.Zero, NativeMethods.GetAncestor(hoveredWindow, NativeMethods.GA_ROOT), IntPtr.Zero)
                    If hoveredRoot <> selected.MainWindowHandle Then
                        _arrowUnbundleLeftMouseWasDown = leftDown
                        Return
                    End If

                    Dim clientPoint As NativeMethods.POINT = screenPoint
                    If NativeMethods.ScreenToClient(selected.MainWindowHandle, clientPoint) Then
                        Dim clientRect As NativeMethods.RECT
                        If Not NativeMethods.GetClientRect(selected.MainWindowHandle, clientRect) Then
                            _arrowUnbundleLeftMouseWasDown = leftDown
                            Return
                        End If

                        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
                        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
                        If clientPoint.X < 0 OrElse clientPoint.Y < 0 OrElse clientPoint.X >= clientWidth OrElse clientPoint.Y >= clientHeight Then
                            AppendLog("Arrow unbundle: click must be inside the selected game window.")
                            _arrowUnbundleLeftMouseWasDown = leftDown
                            Return
                        End If

                        _arrowUnbundlePoints.Add(New LootScanPoint(Math.Max(0, clientPoint.X), Math.Max(0, clientPoint.Y)))
                        _isPickingArrowUnbundlePoint = False
                        _arrowUnbundleLeftMouseWasDown = leftDown
                        UpdateArrowUnbundleUi()
                        PushLiveConfig()
                        SavePersistedListState(False)
                        AppendLog($"Arrow unbundle point added from game window: x={clientPoint.X}, y={clientPoint.Y}.")
                    End If
                End If
            End If

            _arrowUnbundleLeftMouseWasDown = leftDown
        Catch ex As Exception
            _isPickingArrowUnbundlePoint = False
            _arrowUnbundleLeftMouseWasDown = False
            UpdateArrowUnbundleUi()
            AppendLog("Arrow unbundle point capture failed: " & ex.Message)
        End Try
    End Sub

    Private Sub SnapshotMouseClick(sender As Object, e As MouseEventArgs)
        If Not IsSnapshotPickActive() Then
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

        If _pendingBarColorPick <> BarColorPickKind.None Then
            Dim sampled As Color
            Using bmp As New Bitmap(picSnapshot.Image)
                sampled = bmp.GetPixel(mapped.X, mapped.Y)
            End Using

            Dim pickedKind As BarColorPickKind = _pendingBarColorPick
            _pendingBarColorPick = BarColorPickKind.None
            UpdateBarColorUi()
            SetBarColor(pickedKind, sampled, $"snapshot x={mapped.X}, y={mapped.Y}")
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

        If _isPickingArrowUnbundlePoint Then
            _isPickingArrowUnbundlePoint = False
            _arrowUnbundleLeftMouseWasDown = False
            UpdateArrowUnbundleUi()
            AppendLog("Arrow unbundle point pick canceled from snapshot; use Add Point, then click directly inside the game window.")
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
            picSnapshot.Cursor = If(IsSnapshotPickActive(), Cursors.Cross, Cursors.Default)
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
            picSnapshot.Cursor = If(IsSnapshotPickActive(), Cursors.Cross, Cursors.Default)
        End If
    End Sub

    Private Sub ArrowUnbundleOverlayChanged(sender As Object, e As EventArgs)
        SetArrowUnbundleOverlayVisible(chkArrowUnbundleOverlay IsNot Nothing AndAlso chkArrowUnbundleOverlay.Checked)
    End Sub

    Private Sub SetArrowUnbundleOverlayVisible(visible As Boolean)
        If Not visible Then
            If _arrowUnbundleOverlayForm IsNot Nothing AndAlso Not _arrowUnbundleOverlayForm.IsDisposed Then
                _arrowUnbundleOverlayForm.Close()
            End If
            _arrowUnbundleOverlayForm = Nothing
            Return
        End If

        If _arrowUnbundleOverlayForm IsNot Nothing AndAlso Not _arrowUnbundleOverlayForm.IsDisposed Then
            Return
        End If

        _arrowUnbundleOverlayForm = New AutoRelaunchClickOverlayForm(
            Function() ResolveAutoRelaunchClickWindow(IntPtr.Zero, ""),
            Function() GetArrowUnbundleOverlaySteps())
        AddHandler _arrowUnbundleOverlayForm.FormClosed,
            Sub(_s As Object, _e As FormClosedEventArgs)
                _arrowUnbundleOverlayForm = Nothing
            End Sub
        _arrowUnbundleOverlayForm.Show(Me)
    End Sub

    Private Function GetArrowUnbundleOverlaySteps() As List(Of AutoRelaunchOverlayStep)
        Dim overlaySteps As New List(Of AutoRelaunchOverlayStep)()
        Dim intervalSeconds As Decimal = If(nudArrowUnbundleSeconds IsNot Nothing, nudArrowUnbundleSeconds.Value, 60D)
        For i As Integer = 0 To _arrowUnbundlePoints.Count - 1
            Dim pointInfo As LootScanPoint = _arrowUnbundlePoints(i)
            If pointInfo Is Nothing OrElse pointInfo.X < 0 OrElse pointInfo.Y < 0 Then
                Continue For
            End If
            overlaySteps.Add(New AutoRelaunchOverlayStep With {
                .StepNumber = overlaySteps.Count + 1,
                .X = pointInfo.X,
                .Y = pointInfo.Y,
                .DelaySeconds = intervalSeconds,
                .TimingLabel = $"every {intervalSeconds:0.###}s",
                .Description = "double right-click"
            })
        Next
        Return overlaySteps
    End Function

    Private Sub UpdateArrowUnbundleUi()
        If lblArrowUnbundlePoints IsNot Nothing Then
            lblArrowUnbundlePoints.Text = $"Arrow Points: {_arrowUnbundlePoints.Count}"
        End If

        If lstArrowUnbundlePoints IsNot Nothing Then
            Dim selectedIndex As Integer = lstArrowUnbundlePoints.SelectedIndex
            _arrowUnbundleUiSyncInProgress = True
            lstArrowUnbundlePoints.BeginUpdate()
            Try
                lstArrowUnbundlePoints.Items.Clear()
                For i As Integer = 0 To _arrowUnbundlePoints.Count - 1
                    Dim pt As LootScanPoint = _arrowUnbundlePoints(i)
                    If pt IsNot Nothing Then
                        lstArrowUnbundlePoints.Items.Add($"{i + 1}. {pt.X}, {pt.Y}")
                    End If
                Next
                If selectedIndex >= 0 AndAlso selectedIndex < lstArrowUnbundlePoints.Items.Count Then
                    If lstArrowUnbundlePoints.SelectedIndex <> selectedIndex Then
                        lstArrowUnbundlePoints.SelectedIndex = selectedIndex
                    End If
                ElseIf lstArrowUnbundlePoints.Items.Count > 0 Then
                    Dim lastIndex As Integer = lstArrowUnbundlePoints.Items.Count - 1
                    If lstArrowUnbundlePoints.SelectedIndex <> lastIndex Then
                        lstArrowUnbundlePoints.SelectedIndex = lastIndex
                    End If
                End If
            Finally
                lstArrowUnbundlePoints.EndUpdate()
                _arrowUnbundleUiSyncInProgress = False
            End Try
        End If

        If btnPickArrowUnbundlePoint IsNot Nothing Then
            btnPickArrowUnbundlePoint.Text = If(_isPickingArrowUnbundlePoint, "Click Game...", "Add Point")
            btnPickArrowUnbundlePoint.BackColor = If(_isPickingArrowUnbundlePoint, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
        End If

        If btnRemoveArrowUnbundlePoint IsNot Nothing Then
            btnRemoveArrowUnbundlePoint.Enabled = (lstArrowUnbundlePoints IsNot Nothing AndAlso lstArrowUnbundlePoints.SelectedIndex >= 0)
        End If

        If btnClearArrowUnbundlePoints IsNot Nothing Then
            btnClearArrowUnbundlePoints.Enabled = _arrowUnbundlePoints.Count > 0
        End If

        If picSnapshot IsNot Nothing Then
            picSnapshot.Cursor = If(IsSnapshotPickActive(), Cursors.Cross, Cursors.Default)
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
            UpdateSelectedProcessDisplay(Nothing)
            Return
        End If

        UpdateSelectedProcessDisplay(selected)
        If txtProcessRename IsNot Nothing AndAlso Not txtProcessRename.IsDisposed Then
            txtProcessRename.Text = selected.WindowTitle
        End If
        If txtLiteProcessRename IsNot Nothing AndAlso Not txtLiteProcessRename.IsDisposed Then
            txtLiteProcessRename.Text = selected.WindowTitle
        End If
        SyncProcessSelectionAcrossLists(selected.MainWindowHandle)
        PushLiveConfig()
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
                        If IsPreferredKathanaWindow(entries(i)) Then
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

    Private Sub UpdateSelectedProcessDisplay(selected As ProcessWindowEntry)
        If lblSelectedProcess Is Nothing OrElse lblSelectedProcess.IsDisposed Then
            Return
        End If
        If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
            lblSelectedProcess.Text = "No process selected"
            Return
        End If

        lblSelectedProcess.Text = $"{selected.ProcessName} (PID {selected.ProcessId}) - {selected.WindowTitle}"
    End Sub

    Private Function GetSelectedWindowTitleForFallback(edition As BotEdition) As String
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(edition)
        If selected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selected.WindowTitle) Then
            Return selected.WindowTitle.Trim()
        End If
        Return DefaultGameWindowTitle
    End Function

    Private Shared Function IsPreferredKathanaWindow(entry As ProcessWindowEntry) As Boolean
        Return entry IsNot Nothing AndAlso
            entry.ProcessName.Equals(PreferredProcessName, StringComparison.OrdinalIgnoreCase)
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
        _isPickingArrowUnbundlePoint = False
        _arrowUnbundleLeftMouseWasDown = False
        UpdateArrowUnbundleUi()
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

                    Dim expectedRegion As RectRegion = GetLiteAutoPotBarRegion(_pendingLitePointCapture)
                    If Not IsPointInsideRegion(expectedRegion, clientPoint.X, clientPoint.Y) Then
                        Dim barName As String = If(_pendingLitePointCapture = LitePointCaptureKind.Hp, "HP", "Mana")
                        AppendLog($"Lite AutoPots: right click must be inside the configured {barName} bar ({expectedRegion.X}, {expectedRegion.Y}, {expectedRegion.W}, {expectedRegion.H}).")
                        _liteRightMouseWasDown = rightDown
                        Return
                    End If

                    Dim sampledColor As Color
                    If Not TrySampleLiteAutoPotPointColor(selected.MainWindowHandle, clientPoint.X, clientPoint.Y, sampledColor) Then
                        AppendLog("Lite AutoPots: unable to sample the selected pixel color. Keep the Tantra window visible and try again.")
                        _liteRightMouseWasDown = rightDown
                        Return
                    End If

                    If _pendingLitePointCapture = LitePointCaptureKind.Hp Then
                        _liteAutoPotHpPointX = Math.Max(0, clientPoint.X)
                        _liteAutoPotHpPointY = Math.Max(0, clientPoint.Y)
                        _liteAutoPotHpColorEnabled = True
                        _liteAutoPotHpColorArgb = sampledColor.ToArgb()
                        AppendLog($"Lite AutoPots: HP point saved at {_liteAutoPotHpPointX}, {_liteAutoPotHpPointY}; sampled RGB {sampledColor.R}, {sampledColor.G}, {sampledColor.B}.")
                    ElseIf _pendingLitePointCapture = LitePointCaptureKind.Mp Then
                        _liteAutoPotMpPointX = Math.Max(0, clientPoint.X)
                        _liteAutoPotMpPointY = Math.Max(0, clientPoint.Y)
                        _liteAutoPotMpColorEnabled = True
                        _liteAutoPotMpColorArgb = sampledColor.ToArgb()
                        AppendLog($"Lite AutoPots: Mana point saved at {_liteAutoPotMpPointX}, {_liteAutoPotMpPointY}; sampled RGB {sampledColor.R}, {sampledColor.G}, {sampledColor.B}.")
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

    Private Shared Function TrySampleLiteAutoPotPointColor(hwnd As IntPtr, clientX As Integer, clientY As Integer, ByRef sampledColor As Color) As Boolean
        sampledColor = Color.Empty
        If hwnd = IntPtr.Zero OrElse clientX < 0 OrElse clientY < 0 Then
            Return False
        End If

        Using frame As Bitmap = BotEngine.CaptureClient(hwnd)
            If frame Is Nothing OrElse clientX >= frame.Width OrElse clientY >= frame.Height Then
                Return False
            End If

            sampledColor = frame.GetPixel(clientX, clientY)
            Return True
        End Using
    End Function

    Private Function GetLiteAutoPotBarRegion(kind As LitePointCaptureKind) As RectRegion
        If kind = LitePointCaptureKind.Hp Then
            Return BuildRectOrFallback("hp_bar", BotConfig.DefaultHpBarRect())
        End If
        Return BuildRectOrFallback("mp_bar", BotConfig.DefaultMpBarRect())
    End Function

    Private Shared Function IsPointInsideRegion(region As RectRegion, pointX As Integer, pointY As Integer) As Boolean
        Return region IsNot Nothing AndAlso pointX >= region.X AndAlso pointX < region.X + region.W AndAlso pointY >= region.Y AndAlso pointY < region.Y + region.H
    End Function

    Private Shared Function NormalizeLiteAutoPotPoint(region As RectRegion, ByRef pointX As Integer, ByRef pointY As Integer) As Boolean
        If region Is Nothing OrElse pointX < 0 OrElse pointY < 0 Then
            Return False
        End If

        Dim originalX As Integer = pointX
        Dim originalY As Integer = pointY
        pointX = Math.Max(region.X, Math.Min(region.X + region.W - 1, pointX))
        If pointY < region.Y OrElse pointY >= region.Y + region.H Then
            pointY = region.Y + Math.Max(0, region.H \ 2)
        End If
        Return pointX <> originalX OrElse pointY <> originalY
    End Function

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
        UpdateLootScannerButtons()
        PushLiveConfig()
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Invalidate()
        End If
        SavePersistedListState(False)
        UpdateMainTabIndicators()
    End Sub

    Private Sub UpdateLootScannerButtons()
        UpdateLootScannerButtonCore(btnVisionLootScanner, "Loot Scan Area", _lootScannerEnabled)
        UpdateLootScannerButtonCore(btnLootScanner, "Loot Scanner (Alt)", _lootScannerEnabled)
    End Sub

    Private Shared Sub UpdateLootScannerButtonCore(target As Button, label As String, isEnabled As Boolean)
        If target Is Nothing Then
            Return
        End If
        target.Text = If(isEnabled, $"{label}: ON", $"{label}: OFF")
        target.BackColor = If(isEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
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
            "- Select the game process in Process List.",
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
            "- Role: attack, heal, max_health, mana, buff, high_max_hp, repair, stop.",
            "- Priority: lower values act first inside same category checks.",
            "- TriggerPercent: role threshold (heal/mana/max_health use this heavily).",
            "- MinHpPercent / MinMpPercent: minimum self HP/MP to allow this action.",
            "- high_max_hp only fires when enabled in Vision and mob_life_rect OCR reads Max HP above your threshold.",
            "- Avoid mobs over max HP uses the same mob_life_rect OCR, but retargets instead of attacking when Max HP is over your avoid threshold.",
            "- repair watches unreachable_text_rect for '___ is about to break'. After 5 OCR reads inside 10 minutes it sends the configured key once, resets the repair OCR count, then waits until the warning clears before it can trigger again. TriggerPercent is ignored for repair.",
            "",
            "3) COMBAT FULL TAB - MONSTER FILTER",
            "- Monster Filter: blacklist skips listed mobs; whitelist only attacks listed mobs.",
            "- Name Check: 2 reads is safer; 1 read attacks sooner after one OCR match.",
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
            "- Selected Process: read-only status showing the process used by Vision and combat.",
            "- Loop (ms): bot loop delay.",
            "- Retarget (ms): baseline retarget interval.",
            "- Mob HP Presence %: threshold for valid target HP bar signal.",
            "- Show Overlay: live region calibration overlay.",
            "- Capture Snapshot: captures current client image.",
            "- Automatic Screenshots: beneath Snapshot, enable timed game captures, choose 1 to 999 minutes, select the save folder, or open it in File Explorer.",
            "- In-game BOT ON/OFF button: click to toggle, drag to move, or drag its bottom-right grip to resize; its game-relative layout is saved.",
            "",
            "7) VISION TAB - CALIBRATION REGIONS",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, mob_life_rect, unreachable_text_rect,",
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
            "- OCR based repair warning detection from unreachable_text_rect with 5-read / 10-minute rolling confirmation.",
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
            "- Selecciona el proceso del juego en Process List.",
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
            "- Role: attack, heal, max_health, mana, buff, high_max_hp, repair, stop.",
            "- Priority: orden de prioridad.",
            "- TriggerPercent: umbral principal para roles de soporte.",
            "- MinHpPercent / MinMpPercent: minimos para permitir la accion.",
            "- high_max_hp solo dispara si esta activo en Vision y el OCR de mob_life_rect lee Max HP arriba del umbral.",
            "- repair vigila unreachable_text_rect para '___ is about to break'. Despues de 5 lecturas OCR envia la tecla una vez y espera a que el aviso desaparezca antes de volver a activarse. TriggerPercent no se usa en repair.",
            "",
            "3) FILTRO DE MONSTRUOS",
            "- Monster Filter: blacklist evita mobs listados; whitelist solo ataca mobs listados.",
            "- Name Check: 2 lecturas es mas seguro; 1 lectura ataca mas rapido despues de una coincidencia OCR.",
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
            "- Selected Process, Loop(ms), Retarget(ms), Mob HP Presence%.",
            "- Show Overlay para calibracion visual.",
            "- Capture Snapshot para capturar imagen del cliente.",
            "- Automatic Screenshots permite capturas programadas de 1 a 999 minutos y elegir la carpeta de destino.",
            "",
            "7) REGIONES DE CALIBRACION",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, mob_life_rect, unreachable_text_rect,",
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
            "- Sin acciones: selecciona el proceso correcto y prueba Capture Snapshot.",
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
            "- Piliin ang game process sa Process List.",
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
            "- Role: attack, heal, max_health, mana, buff, high_max_hp, repair, stop.",
            "- Priority: pagkakasunod ng aksyon.",
            "- TriggerPercent: pangunahing threshold ng support actions.",
            "- MinHpPercent / MinMpPercent: minimum HP/MP para payagan ang action.",
            "- high_max_hp gagana lang kapag naka-enable sa Vision at nabasa ng mob_life_rect OCR ang Max HP lampas sa threshold mo.",
            "- repair nagbabantay sa unreachable_text_rect para sa '___ is about to break'. Pag nabasa ito ng OCR ng 5 beses, isang beses nitong ipapadala ang repair key at maghihintay munang mawala ang warning bago puwedeng mag-trigger ulit. Hindi ginagamit ang TriggerPercent sa repair.",
            "",
            "3) MONSTER FILTER",
            "- Monster Filter: blacklist iiwas sa listed mobs; whitelist listed mobs lang ang aatakihin.",
            "- Name Check: 2 reads mas safe; 1 read mas mabilis umatake after isang OCR match.",
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
            "- Selected Process, Loop(ms), Retarget(ms), Mob HP Presence%.",
            "- Show Overlay para madaling calibration ng regions.",
            "- Capture Snapshot para kumuha ng current game image.",
            "- Automatic Screenshots para sa timed captures mula 1 hanggang 999 minuto at pagpili ng save folder.",
            "",
            "7) CALIBRATION REGIONS",
            "- hp_bar, mp_bar, mob_name_rect, mob_hp_rect, mob_life_rect, unreachable_text_rect,",
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
            "- OCR para sa repair warning sa unreachable_text_rect na may 5-read / 10-minute rolling confirmation.",
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
            "- Walang action: piliin ang tamang process at subukan ang Capture Snapshot.",
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
                    "- Selected Process shows the process currently used by Vision and combat; change it in Process List.",
                    "- Loop (ms) sets the main scan and action speed.",
                    "- Normal Retarget (ms) and Forced Retarget (ms) tune when the bot sends E again.",
                    "- Mob HP Presence % is the minimum HP-bar signal required to trust the current target.",
                    "- Show Overlay opens the live calibration overlay.",
                    "- Capture Snapshot stores the current client image for region checking.",
                    "- Automatic Screenshots is beneath Snapshot; choose 1 to 999 minutes, select the destination with Browse, or open it with Open Folder.",
                    "- Use buff key on high max HP mobs plus Max HP >= work together with the high_max_hp combat role.",
                    "- Avoid mobs over max HP plus Avoid Max HP >= skips targets above that detected Max HP and retargets.",
                    "- Evade Dadatis blocks attacks on a freshly recognized Dadati, taps W and S to shift position, then forces an E retarget.",
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
                    "- Role meanings: attack = normal damage, heal = healing action, max_health = HP support threshold, mana = MP support threshold, buff = extra combat skill, high_max_hp = buff branch for high-Max-HP targets, repair = one-shot repair key when the OCR warning is confirmed, stop = stop-movement key burst.",
                    "- Use lower Priority numbers for more important rows within the same role group.",
                    "- high_max_hp only works well if Vision reads the mob_life_rect numbers correctly.",
                    "- repair only works when unreachable_text_rect is calibrated to detect the 'is about to break' warning.",
                    "- Monster Filter can run as blacklist or whitelist. Blacklist skips listed names; whitelist only attacks listed names. Name Check 2 reads requires two separate OCR reads before attacking; 1 read attacks after the first matching OCR name.",
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
                    "- Selected Process muestra el proceso usado por Vision y combate; cambialo en Process List.",
                    "- Loop (ms) define la velocidad del ciclo principal.",
                    "- Normal Retarget (ms) y Forced Retarget (ms) ajustan cuando el bot vuelve a usar E.",
                    "- Mob HP Presence % es la senal minima para confiar en la barra de HP del objetivo.",
                    "- Show Overlay abre la capa de calibracion.",
                    "- Capture Snapshot captura la imagen actual del cliente.",
                    "- Automatic Screenshots activa capturas programadas; elige de 1 a 999 minutos y la carpeta con Browse.",
                    "- Use buff key on high max HP mobs junto con Max HP >= trabaja con el role high_max_hp.",
                    "- Evade Dadatis bloquea ataques contra Dadati, pulsa W y S para mover al personaje y luego fuerza un retarget con E.",
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
                    "- Roles: attack = dano normal, heal = curacion, max_health = soporte de HP, mana = soporte de MP, buff = skill ofensiva extra, high_max_hp = rama buff para mobs con mucho Max HP, repair = repair por warning OCR, stop = tecla de parada.",
                    "- Usa prioridades numericamente mas bajas para filas mas importantes dentro del mismo tipo.",
                    "- high_max_hp depende de una lectura correcta de mob_life_rect en Vision.",
                    "- repair depende de que unreachable_text_rect detecte bien el warning de equipo.",
                    "- Monster Filter puede ser blacklist o whitelist. Blacklist evita nombres listados; whitelist solo ataca nombres listados. Name Check 2 reads requiere dos lecturas OCR separadas antes de atacar; 1 read ataca con la primera coincidencia.",
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
                    "- Selected Process ang kasalukuyang process na gamit ng Vision at combat; piliin ito sa Process List.",
                    "- Loop (ms) ang bilis ng main scan/action cycle.",
                    "- Normal Retarget (ms) at Forced Retarget (ms) ang timing kung kailan muling mag-E ang bot.",
                    "- Mob HP Presence % ang minimum signal para paniwalaan ang HP bar ng target.",
                    "- Show Overlay bubukas sa live calibration overlay.",
                    "- Capture Snapshot kukuha ng kasalukuyang image ng client.",
                    "- Automatic Screenshots ay timed game captures; pumili ng 1 hanggang 999 minuto at save folder gamit ang Browse.",
                    "- Use buff key on high max HP mobs kasama ng Max HP >= ay para sa high_max_hp combat role.",
                    "- Evade Dadatis hinaharang ang pag-atake sa Dadati, tina-tap ang W at S para gumalaw, at saka nagfo-force ng E retarget.",
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
                    "- Mga role: attack = normal damage, heal = heal action, max_health = HP support threshold, mana = MP support threshold, buff = extra skill, high_max_hp = buff branch para sa high-Max-HP mobs, repair = one-shot repair kapag confirmed ang OCR warning, stop = stop-movement key burst.",
                    "- Gumamit ng mas mababang priority number para sa mas importanteng rows sa parehong role group.",
                    "- high_max_hp ay gagana lang kung tama ang OCR reading ng mob_life_rect sa Vision.",
                    "- repair ay nakadepende sa tamang calibration ng unreachable_text_rect warning text.",
                    "- Monster Filter puwedeng blacklist o whitelist. Blacklist iiwas sa listed names; whitelist listed names lang ang aatakihin. Name Check 2 reads kailangan ng dalawang OCR reads bago umatake; 1 read unang match pa lang puwede na.",
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
        Dim title As String = GetSelectedWindowTitleForFallback(BotEdition.Full)
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

    Private Sub RecordUiTiming(elapsedMs As Double)
        Dim safeMs As Double = If(Double.IsNaN(elapsedMs) OrElse Double.IsInfinity(elapsedMs), 0.0R, Math.Max(0.0R, elapsedMs))
        _uiTimingCount += 1
        If _uiTimingCount = 1 Then
            _uiTimingAverageMs = safeMs
            _uiTimingMaxMs = safeMs
        Else
            _uiTimingAverageMs += (safeMs - _uiTimingAverageMs) * 0.12R
            If safeMs > _uiTimingMaxMs Then
                _uiTimingMaxMs = safeMs
            End If
        End If
    End Sub

    Private Function FormatUiTiming() As String
        Return $"UI Update: avg {_uiTimingAverageMs:0.0}ms | max {_uiTimingMaxMs:0.0}ms | n={_uiTimingCount}"
    End Function

    Private Function FormatUiHealth() As String
        Dim pendingLogs As Integer = 0
        Dim totalDropped As Long = 0
        SyncLock _logQueueSync
            pendingLogs = _logQueue.Count
            totalDropped = _totalDroppedLogLineCount
        End SyncLock

        Dim lootHistoryCount As Integer = 0
        SyncLock _lootHistoryEventsSync
            lootHistoryCount = _lootHistoryEvents.Count
        End SyncLock

        Dim keyActionCount As Integer = 0
        SyncLock _keyActionEventsSync
            keyActionCount = _keyActionEvents.Count
        End SyncLock

        Dim managedMb As Double = GC.GetTotalMemory(False) / 1024.0R / 1024.0R
        Dim privateMb As Double = 0.0R
        Dim threadCount As Integer = 0
        Try
            Using proc As Process = Process.GetCurrentProcess()
                privateMb = proc.PrivateMemorySize64 / 1024.0R / 1024.0R
                threadCount = proc.Threads.Count
            End Using
        Catch
        End Try

        Dim logChars As Integer = If(rtbLog Is Nothing OrElse rtbLog.IsDisposed, 0, rtbLog.TextLength)
        Dim lastFlush As String = If(_lastLogFlushAt = DateTime.MinValue, "n/a", _lastLogFlushAt.ToString("HH:mm:ss"))
        Return $"UI Health: pendingLogs {pendingLogs} | droppedLogs {totalDropped} | lastFlush {lastFlush} x{_lastLogFlushBatchCount} | logChars {logChars:N0} | keyEvents {keyActionCount:N0} | lootHistory {lootHistoryCount:N0} | managed {managedMb:0.0}MB | private {privateMb:0.0}MB | threads {threadCount}"
    End Function

    Private Function GetSelectedCaptureBackendCode() As String
        Dim label As String = If(cboCaptureBackend IsNot Nothing AndAlso cboCaptureBackend.SelectedItem IsNot Nothing, cboCaptureBackend.SelectedItem.ToString(), "Auto")
        Select Case label.Trim().ToLowerInvariant()
            Case "cached gdi"
                Return "gdi"
            Case "windows graphics capture"
                Return "wgc"
            Case Else
                Return "auto"
        End Select
    End Function

    Private Sub SelectCaptureBackend(raw As String)
        If cboCaptureBackend Is Nothing Then
            Return
        End If

        Dim normalized As String = If(raw, "").Trim().ToLowerInvariant()
        Dim label As String = "Auto"
        If normalized = "gdi" OrElse normalized = "cached_gdi" Then
            label = "Cached GDI"
        ElseIf normalized = "wgc" OrElse normalized = "windows_graphics_capture" Then
            label = "Windows Graphics Capture"
        End If
        cboCaptureBackend.SelectedItem = label
    End Sub

    Private Sub RunBenchmarkClicked(_sender As Object, _e As EventArgs)
        btnRunBenchmark.Enabled = False
        AppendLog("Performance benchmark started.")
        Dim cfg As BotConfig = BuildFullConfig()
        Task.Run(
            Sub()
                Dim report As String = BotEngine.RunPerformanceBenchmark(cfg, 30)
                BeginInvoke(
                    New Action(
                        Sub()
                            AppendLog("Performance benchmark completed.")
                            txtDiagnostics.Text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Benchmark{Environment.NewLine}{report}{Environment.NewLine}{Environment.NewLine}{txtDiagnostics.Text}"
                            btnRunBenchmark.Enabled = True
                        End Sub))
            End Sub)
    End Sub

    Private Sub ExportDiagnosticsClicked(_sender As Object, _e As EventArgs)
        Try
            Dim exportDir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "KathanaBotDiagnostics")
            Directory.CreateDirectory(exportDir)
            Dim exportPath As String = Path.Combine(exportDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt")
            Dim history As String = String.Join(Environment.NewLine & Environment.NewLine, _diagnosticsHistory.Reverse())
            Dim currentText As String = If(txtDiagnostics IsNot Nothing, txtDiagnostics.Text, "")
            File.WriteAllText(exportPath, currentText & Environment.NewLine & Environment.NewLine & "History" & Environment.NewLine & history, Encoding.UTF8)
            AppendLog("Diagnostics exported: " & exportPath)
        Catch ex As Exception
            AppendLog("Diagnostics export failed: " & ex.Message)
        End Try
    End Sub

    Private Sub AddDiagnosticsHistory(snapshotText As String)
        If String.IsNullOrWhiteSpace(snapshotText) Then
            Return
        End If

        _diagnosticsHistory.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{snapshotText}")
        While _diagnosticsHistory.Count > DiagnosticsHistoryLimit
            _diagnosticsHistory.Dequeue()
        End While
    End Sub

    Private Sub UiTimerTick(sender As Object, e As EventArgs)
        Dim uiWatch As Stopwatch = Stopwatch.StartNew()
        MonitorEngineWorkers()
        PushLiveConfig()
        Dim st As BotStatus = GetStatusForEdition(_edition)
        HandlePendingLitePointCapture()
        HandlePendingArrowUnbundlePointCapture()
        HandlePendingAutoRelaunchClickCapture()
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
            $"MonsterFilter: {If(chkMonsterFilter IsNot Nothing AndAlso chkMonsterFilter.Checked, "Enabled", "Disabled")} | Mode: {GetMonsterFilterMode()} | NameConfirmReads: {GetMonsterFilterConfirmReads()}{Environment.NewLine}" &
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
            $"EvadeDadati: {If(chkEvadeDadati IsNot Nothing AndAlso chkEvadeDadati.Checked, "True", "False")}{Environment.NewLine}" &
            $"ChatTranslationEnabled: {If(chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked, "True", "False")}{Environment.NewLine}" &
            $"ChatTranslationOverlay: {If(chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked, "True", "False")}{Environment.NewLine}" &
            $"DisabledRegionOverlays: {String.Join(", ", BuildDisabledCalibrationRegionOverlays())}{Environment.NewLine}" &
            $"ChatTargetLanguage: {GetSelectedChatTargetLanguageCode()}{Environment.NewLine}" &
            $"ChatScanMs: {If(nudChatScanMs IsNot Nothing, nudChatScanMs.Value.ToString(), "700")}{Environment.NewLine}" &
            $"ChatMaxLines: {If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value.ToString(), "6")}{Environment.NewLine}" &
            $"AdaptivePerformanceEnabled: {If(chkAdaptivePerformance Is Nothing OrElse chkAdaptivePerformance.Checked, "True", "False")}{Environment.NewLine}" &
            $"PixelChangeGateEnabled: {If(chkPixelChangeGate Is Nothing OrElse chkPixelChangeGate.Checked, "True", "False")}{Environment.NewLine}" &
            $"AdaptiveSlowMinMs: {If(nudAdaptiveSlowMinMs IsNot Nothing, nudAdaptiveSlowMinMs.Value.ToString(), "140")}{Environment.NewLine}" &
            $"AdaptiveSlowMultiplier: {If(nudAdaptiveSlowMultiplier IsNot Nothing, nudAdaptiveSlowMultiplier.Value.ToString("0.00"), "1.80")}{Environment.NewLine}" &
            $"AdaptiveRecoveryMultiplier: {If(nudAdaptiveRecoveryMultiplier IsNot Nothing, nudAdaptiveRecoveryMultiplier.Value.ToString("0.00"), "1.25")}{Environment.NewLine}" &
            $"AdaptiveConfirmSlow/Recover: {If(nudAdaptiveSlowConfirm IsNot Nothing, nudAdaptiveSlowConfirm.Value.ToString(), "5")}/{If(nudAdaptiveRecoveryConfirm IsNot Nothing, nudAdaptiveRecoveryConfirm.Value.ToString(), "14")}{Environment.NewLine}" &
            $"CaptureBackendPreference: {GetSelectedCaptureBackendCode()}{Environment.NewLine}" &
            $"FullFrameScanMs: {If(nudFullFrameScanMs IsNot Nothing, nudFullFrameScanMs.Value.ToString(), "500")}{Environment.NewLine}" &
            $"LootScannerIntervalSec: {If(nudLootScannerSeconds IsNot Nothing, nudLootScannerSeconds.Value.ToString("0.0"), "10")}{Environment.NewLine}" &
            $"MapScanMs: {If(nudMapScanMs IsNot Nothing, nudMapScanMs.Value.ToString(), "900")}{Environment.NewLine}" &
            $"PartyScanMs: {If(nudPartyScanMs IsNot Nothing, nudPartyScanMs.Value.ToString(), "700")}{Environment.NewLine}" &
            $"MobNameScanMs: {If(nudMobNameScanMs IsNot Nothing, nudMobNameScanMs.Value.ToString(), "650")}{Environment.NewLine}" &
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
            $"ReturnToStart: {If(chkNavigationReturnToStart IsNot Nothing AndAlso chkNavigationReturnToStart.Checked, "True", "False")}{Environment.NewLine}" &
            $"HoldPlace: {If(chkHoldPlaceEnabled IsNot Nothing AndAlso chkHoldPlaceEnabled.Checked, "Enabled", "Disabled")} | Preset: {GetHoldPlaceRestrictivenessLabel(GetHoldPlaceRestrictivenessMode())} | " &
            $"PostFight: {If(chkHoldPlacePostFightReturn Is Nothing OrElse chkHoldPlacePostFightReturn.Checked, "True", "False")} | " &
            $"CombatSafe: {If(chkHoldPlaceCombatSafe Is Nothing OrElse chkHoldPlaceCombatSafe.Checked, "True", "False")} | " &
            $"Leash: {If(nudHoldPlaceEmergencyLeash IsNot Nothing, nudHoldPlaceEmergencyLeash.Value.ToString(), "60")} | " &
            $"DirectionLearning: {If(chkHoldPlaceDirectionLearning Is Nothing OrElse chkHoldPlaceDirectionLearning.Checked, "True", "False")}{Environment.NewLine}" &
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
            $"ArrowUnbundleEnabled: {If(chkArrowUnbundleEnabled IsNot Nothing AndAlso chkArrowUnbundleEnabled.Checked, "True", "False")}{Environment.NewLine}" &
            $"ArrowUnbundleSeconds: {If(nudArrowUnbundleSeconds IsNot Nothing, nudArrowUnbundleSeconds.Value.ToString(), "60")}{Environment.NewLine}" &
            $"ArrowUnbundlePoints: {FormatLootScanPoints(_arrowUnbundlePoints)}{Environment.NewLine}" &
            $"AlarmVolume%: {_alarmVolumePercent}{Environment.NewLine}" &
            $"HpZeroAlarm: {_hpZeroAlarmActive}{Environment.NewLine}" &
            $"HpZeroPending: {_hpZeroPending}{Environment.NewLine}" &
            $"HpZeroConfirm: {_deadHpConfirmCount}/{CriticalAlertConfirmFrames} frames, {FormatPendingAlertSeconds(_deadHpFirstSeenUtc)}s/{CriticalAlertConfirmMs \ 1000}s{Environment.NewLine}" &
            $"WindowMissingConfirm: {_windowMissingConfirmCount}/{CriticalAlertConfirmFrames} frames, {FormatPendingAlertSeconds(_windowMissingFirstSeenUtc)}s/{CriticalAlertConfirmMs \ 1000}s{Environment.NewLine}" &
            $"Window Found: {st.WindowFound}{Environment.NewLine}" &
            $"HP%: {st.HpPercent:0.0}{Environment.NewLine}" &
            $"MP%: {st.MpPercent:0.0}{Environment.NewLine}" &
            $"CharacterName: {If(String.IsNullOrWhiteSpace(st.CharacterName), "n/a", st.CharacterName)}{Environment.NewLine}" &
            $"Prana/EXP%: {st.ExpPercent:0.00}{Environment.NewLine}" &
            $"Prana/EXP Rate %/hr: {If(st.ExpPerHour < 0, "Calculating (1m)", st.ExpPerHour.ToString("0.00"))}{Environment.NewLine}" &
            $"MobName: {st.MobName}{Environment.NewLine}" &
            $"MobHpText: {If(String.IsNullOrWhiteSpace(st.MobHpText), "n/a", st.MobHpText)}{Environment.NewLine}" &
            $"Performance:{Environment.NewLine}{If(String.IsNullOrWhiteSpace(st.PerformanceDiagnostics), "n/a", st.PerformanceDiagnostics)}{Environment.NewLine}" &
            $"{FormatUiTiming()}{Environment.NewLine}" &
            $"{FormatUiHealth()}{Environment.NewLine}" &
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
            $"NavigationReturningToStart: {st.NavigationReturningToStart}{Environment.NewLine}" &
            $"NavigationReturnTarget: {If(String.IsNullOrWhiteSpace(st.NavigationReturnTargetLabel), "n/a", st.NavigationReturnTargetLabel)}{Environment.NewLine}" &
            $"EngineRestartCount: {st.EngineRestartCount}{Environment.NewLine}" &
            $"EngineLastRestartUtc: {If(st.EngineLastRestartUtc = DateTime.MinValue, "n/a", st.EngineLastRestartUtc.ToString("yyyy-MM-dd HH:mm:ss"))}{Environment.NewLine}" &
            $"LastAction: {st.LastAction}{Environment.NewLine}" &
            $"RepairOCR: {st.RepairConfirmCount}/{Math.Max(1, st.RepairConfirmRequiredCount)} in {Math.Max(1, st.RepairConfirmWindowMinutes)}m | Triggers: {st.RepairTriggerCount}{Environment.NewLine}" &
             $"NotAttackingReason: {st.NotAttackingReason}{Environment.NewLine}" &
             $"Error: {st.ErrorMessage}"
        AddDiagnosticsHistory(txtDiagnostics.Text)
        RefreshKeyActionSummary()
        RefreshLootHistoryGrid()
        uiWatch.Stop()
        RecordUiTiming(uiWatch.Elapsed.TotalMilliseconds)
    End Sub

    Private Sub MonitorEngineWorkers()
        If _fullEngine.EnsureLoopWorkerRunning() Then
            AppendLog("Full engine worker watchdog restarted the loop.")
        End If
        If _liteEngine.EnsureLoopWorkerRunning() Then
            AppendLog("Lite engine worker watchdog restarted the loop.")
        End If
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
        HandlePendingArrowUnbundlePointCapture()
        HandlePendingAutoRelaunchClickCapture()
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
        Dim selected As ProcessWindowEntry = GetSelectedProcessWindow()
        Dim hwnd As IntPtr = GetForegroundWindow()
        If hwnd = IntPtr.Zero Then
            Return False
        End If
        If selected IsNot Nothing AndAlso selected.MainWindowHandle <> IntPtr.Zero Then
            Return hwnd = selected.MainWindowHandle
        End If

        Dim targetTitle As String = GetSelectedWindowTitleForFallback(If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full))
        If targetTitle = "" Then
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
            If IsDisposed OrElse Not IsHandleCreated Then
                Return
            End If
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
            HandleGameDisconnectedAlert(status)
            UpdateTaskbarStatusIndicator()
            Return
        End If

        _fullStatus = status

        lblState.Text = statusText
        lblSystem.Text = $"System Active: {status.Running}"
        lblHp.Text = $"HP%: {status.HpPercent:0.0}"
        lblMp.Text = $"MP%: {status.MpPercent:0.0}"
        lblHp.ForeColor = HpColor(status.HpPercent)
        lblMp.ForeColor = MpColor(status.MpPercent)
        lblMobName.Text = FormatFullMobStatusText(status)
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
            If status.NavigationReturningToStart AndAlso Not String.IsNullOrWhiteSpace(status.NavigationReturnTargetLabel) Then
                travelReason = $"returning to start: {status.NavigationReturnTargetLabel}"
            End If
            Dim distanceText As String = If(status.NavigationDistanceToWaypoint < 0, "n/a", status.NavigationDistanceToWaypoint.ToString("0.0"))
            Dim stallText As String = If(status.NavigationTravelStalled, $" | stalled x{Math.Max(1, status.NavigationRecoveryCount)}", If(status.NavigationRecoveryCount > 0, $" | recoveries {status.NavigationRecoveryCount}", ""))
            If status.NavigationDestinationReached AndAlso Not String.IsNullOrWhiteSpace(status.NavigationDestinationLabel) Then
                travelReason = $"destination reached: {status.NavigationDestinationLabel}"
                distanceText = "0.0"
            End If
            lblTravelStatus.Text = $"Travel: {travelReason} | Dist: {distanceText}{stallText}"
            lblTravelStatus.ForeColor = If(status.NavigationDestinationReached, Color.LightGreen, If(status.NavigationTravelStalled, Color.OrangeRed, If(status.NavigationTravelActive, Color.LightSteelBlue, Color.DimGray)))
        End If
        If lblHoldPlaceStatus IsNot Nothing Then
            Dim holdReason As String = If(String.IsNullOrWhiteSpace(status.HoldPlaceReason), If(status.HoldPlaceEnabled, "waiting", "disabled"), status.HoldPlaceReason)
            lblHoldPlaceStatus.Text = $"Hold: {holdReason}"
            lblHoldPlaceStatus.ForeColor = If(status.HoldPlaceActive, Color.LightSteelBlue, If(status.HoldPlaceEnabled, Color.Khaki, Color.DimGray))
        End If
        If lblHoldPlaceCurrent IsNot Nothing Then
            If status.MapCoordinateX >= 0 AndAlso status.MapCoordinateY >= 0 Then
                lblHoldPlaceCurrent.Text = $"Current: {status.MapCoordinateX:000}/{status.MapCoordinateY:000} (confidence {status.MapCoordinateConfidence}%)"
                lblHoldPlaceCurrent.ForeColor = If(status.MapCoordinateConfidence >= 70, Color.LightGreen, Color.Khaki)
            Else
                Dim rawCoordinateText As String = If(String.IsNullOrWhiteSpace(status.MapCoordinateText), "no OCR text", status.MapCoordinateText)
                lblHoldPlaceCurrent.Text = $"Current: waiting for X/Y read ({rawCoordinateText})"
                lblHoldPlaceCurrent.ForeColor = Color.Khaki
            End If
        End If
        If txtHoldPlaceCoordinateLog IsNot Nothing Then
            Dim coordinateLog As String = If(String.IsNullOrWhiteSpace(status.MapCoordinateDebugLog), "Coordinate log: no coordinate checks reported yet.", status.MapCoordinateDebugLog)
            If Not String.Equals(txtHoldPlaceCoordinateLog.Text, coordinateLog, StringComparison.Ordinal) Then
                txtHoldPlaceCoordinateLog.Text = coordinateLog
                txtHoldPlaceCoordinateLog.SelectionStart = txtHoldPlaceCoordinateLog.TextLength
                txtHoldPlaceCoordinateLog.ScrollToCaret()
            End If
        End If
        If lblHoldPlaceTarget IsNot Nothing Then
            If status.HoldPlaceTargetX >= 0 AndAlso status.HoldPlaceTargetY >= 0 Then
                Dim holdDistance As String = If(status.HoldPlaceDistance < 0, "n/a", status.HoldPlaceDistance.ToString("0.0"))
                lblHoldPlaceTarget.Text = $"Anchor: {status.HoldPlaceTargetX:000}/{status.HoldPlaceTargetY:000} | Distance: {holdDistance}"
                lblHoldPlaceTarget.ForeColor = If(status.HoldPlaceEnabled, Color.LightSteelBlue, Color.DimGray)
            ElseIf _holdPlaceAnchorSet Then
                Dim targetX As Integer = CInt(If(nudHoldPlaceTargetX IsNot Nothing, nudHoldPlaceTargetX.Value, 0D))
                Dim targetY As Integer = CInt(If(nudHoldPlaceTargetY IsNot Nothing, nudHoldPlaceTargetY.Value, 0D))
                lblHoldPlaceTarget.Text = $"Anchor: {targetX:000}/{targetY:000} | Distance: n/a"
                lblHoldPlaceTarget.ForeColor = If(status.HoldPlaceEnabled, Color.LightSteelBlue, Color.DimGray)
            Else
                lblHoldPlaceTarget.Text = "Anchor: not set"
                lblHoldPlaceTarget.ForeColor = Color.DimGray
            End If
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
        HandleGameDisconnectedAlert(status)
        HandleHpZeroAlarm(status)
        HandleWindowMissingAlarm(status)
        ApplyHealthUiTint(status.HpPercent, status.Running)
        UpdateTaskbarStatusIndicator()

        If Not String.IsNullOrWhiteSpace(status.RouteRecordingLastSavedPath) AndAlso Not status.RouteRecordingLastSavedPath.Equals(_lastRouteRecordingSavedPath, StringComparison.OrdinalIgnoreCase) Then
            _lastRouteRecordingSavedPath = status.RouteRecordingLastSavedPath
            AppendLog("Recorded route saved: " & status.RouteRecordingLastSavedPath)
            PopulateNavigationNodeCombos()
        End If

        If status.LastAction <> "" AndAlso status.LastAction <> _lastAction Then
            _lastAction = status.LastAction
        End If
        If statusText <> _lastState Then
            Dim stateLog As String = BuildRateLimitedLogMessage(
                statusText.Replace("Status: ", "State changed to: "),
                _lastStateLogUtc,
                _suppressedStateLogCount,
                HighFrequencyLogMinIntervalMs,
                "state change")
            If stateLog IsNot Nothing Then
                AppendLog(stateLog)
            End If
            _lastState = statusText
        End If
        If status.ErrorMessage <> "" AndAlso status.ErrorMessage <> _lastError Then
            AppendLog("Warning: " & status.ErrorMessage)
            _lastError = status.ErrorMessage
        End If
        If status.NotAttackingReason <> "" AndAlso status.NotAttackingReason <> _lastNoAttackReason Then
            Dim reasonLog As String = BuildRateLimitedLogMessage(
                "No attack reason: " & status.NotAttackingReason,
                _lastNoAttackReasonLogUtc,
                _suppressedNoAttackReasonLogCount,
                HighFrequencyLogMinIntervalMs,
                "no-attack reason")
            If reasonLog IsNot Nothing Then
                AppendLog(reasonLog)
            End If
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

    Private Shared Function FormatFullMobStatusText(status As BotStatus) As String
        If status Is Nothing Then
            Return "Mob: (none) | Life: n/a"
        End If

        Dim mobName As String = If(String.IsNullOrWhiteSpace(status.MobName), "(none)", status.MobName.Trim())
        Dim lifeText As String = If(String.IsNullOrWhiteSpace(status.MobHpText), "n/a", status.MobHpText.Trim())
        If lifeText = "n/a" AndAlso status.MobMaxHp > 0 Then
            lifeText = status.MobMaxHp.ToString("N0")
        End If

        Return $"Mob: {mobName} | Life: {lifeText}"
    End Function

    Private Sub HandleChatTranslation(status As BotStatus)
        Dim translationEnabled As Boolean = (chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked)
        Dim overlayEnabled As Boolean = (chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked)
        If Not translationEnabled Then
            _lastChatOcrText = ""
            _lastChatTargetLanguage = ""
            _chatScreenGeneration += 1
            _chatOverlayEntries.Clear()
            HideChatTranslationOverlay()
            Return
        End If

        UpdateChatTranslationOverlayVisibility(overlayEnabled)

        Dim rawText As String = If(status.ChatOcrText, "").Trim()
        Dim targetLanguage As String = GetSelectedChatTargetLanguageCode()
        If rawText = "" Then
            _lastChatOcrText = ""
            _lastChatTargetLanguage = targetLanguage
            _chatScreenGeneration += 1
            _chatOverlayEntries.Clear()
            RefreshChatTranslationOverlayContent()
            Return
        End If

        If rawText.Equals(_lastChatOcrText, StringComparison.Ordinal) AndAlso targetLanguage.Equals(_lastChatTargetLanguage, StringComparison.OrdinalIgnoreCase) Then
            RefreshChatTranslationOverlayContent()
            Return
        End If

        _lastChatOcrText = rawText
        _lastChatTargetLanguage = targetLanguage
        _chatScreenGeneration += 1
        Dim generation As Integer = _chatScreenGeneration
        Dim lines As List(Of String) = ParseChatOcrLines(rawText)
        _chatOverlayEntries.Clear()

        For Each line As String In lines
            Dim lineText As String = If(line, "").Trim()
            If lineText = "" Then
                Continue For
            End If

            _chatOverlayEntries.Add(New ChatOverlayLine With {
                .SourceText = lineText,
                .TranslatedText = lineText,
                .CreatedAtUtc = DateTime.UtcNow
            })
        Next

        RefreshChatTranslationOverlayContent()

        For i As Integer = 0 To _chatOverlayEntries.Count - 1
            QueueChatTranslation(_chatOverlayEntries(i).SourceText, targetLanguage, generation, i)
        Next
    End Sub

    Private Shared Function ParseChatOcrLines(rawText As String) As List(Of String)
        Dim results As New List(Of String)()
        For Each rawLine As String In If(rawText, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleaned As String = Regex.Replace(rawLine, "\s+", " ").Trim()
            If cleaned.Length < 2 Then
                Continue For
            End If
            results.AddRange(SplitJoinedChatMessages(cleaned))
        Next
        Return results
    End Function

    Private Shared Function SplitJoinedChatMessages(line As String) As IEnumerable(Of String)
        Dim cleaned As String = If(line, "").Trim()
        If cleaned = "" Then
            Return Enumerable.Empty(Of String)()
        End If

        cleaned = Regex.Replace(cleaned, "(?i)(?<=\s)m\]\s+(?=[A-Za-z0-9_\- ]{2,32}\s+has\s+)", " [System] ")
        Dim starts As List(Of Integer) = FindChatMessageStartIndexes(cleaned)
        If starts.Count <= 1 Then
            Return New String() {NormalizeChatSystemPrefix(cleaned)}.
                Where(Function(part) part.Length >= 2)
        End If

        Dim parts As New List(Of String)()
        For i As Integer = 0 To starts.Count - 1
            Dim startIndex As Integer = starts(i)
            Dim endIndex As Integer = If(i + 1 < starts.Count, starts(i + 1), cleaned.Length)
            If endIndex <= startIndex Then
                Continue For
            End If

            parts.Add(cleaned.Substring(startIndex, endIndex - startIndex))
        Next

        Return parts.
            Select(Function(part) NormalizeChatSystemPrefix(Regex.Replace(part, "\s+", " ").Trim())).
            Where(Function(part) part.Length >= 2)
    End Function

    Private Shared Function FindChatMessageStartIndexes(line As String) As List(Of Integer)
        Dim starts As New SortedSet(Of Integer)()
        Dim source As String = If(line, "")
        If source.Trim() = "" Then
            Return starts.ToList()
        End If

        starts.Add(0)
        For Each match As Match In Regex.Matches(source, "(?:^|\s)(?:\[[^\]]+\]\s*[^:\[]{1,32}:|[A-Za-z][A-Za-z0-9_\-]{1,23}:|m\]\s+|\[[^\]]+\]\s+)", RegexOptions.IgnoreCase)
            Dim value As String = match.Value
            Dim index As Integer = match.Index
            If value.Length > 0 AndAlso Char.IsWhiteSpace(value(0)) Then
                index += 1
            End If

            If index >= 0 AndAlso index < source.Length Then
                starts.Add(index)
            End If
        Next

        Return starts.ToList()
    End Function

    Private Shared Function NormalizeChatSystemPrefix(line As String) As String
        Dim cleaned As String = If(line, "").Trim()
        If cleaned = "" Then
            Return ""
        End If

        Return Regex.Replace(cleaned, "^(?:\[[^\]]*syst[^\]]*\]|m\])\s*", "[System] ", RegexOptions.IgnoreCase)
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

        Dim knownSystemTranslation As String = TranslateKnownSystemChatLine(lineText, targetLanguage)
        If knownSystemTranslation <> "" Then
            Return knownSystemTranslation
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

    Private Shared Function TranslateKnownSystemChatLine(sourceLine As String, targetLanguage As String) As String
        Dim lineText As String = NormalizeChatSystemPrefix(If(sourceLine, ""))
        Dim match As Match = Regex.Match(lineText, "^\[System\]\s+(.+?)\s+has\s+(logged\s+in|logged\s+out)\.?\s*$", RegexOptions.IgnoreCase)
        If Not match.Success Then
            Return ""
        End If

        Dim actorName As String = Regex.Replace(match.Groups(1).Value.Trim(), "\s+", " ")
        If actorName = "" Then
            Return ""
        End If

        Dim actionText As String = Regex.Replace(match.Groups(2).Value.Trim().ToLowerInvariant(), "\s+", " ")
        Select Case NormalizeChatTargetLanguageCode(targetLanguage)
            Case "es"
                If actionText = "logged in" Then
                    Return $"[System] {actorName} ha iniciado sesion."
                End If
                Return $"[System] {actorName} se ha desconectado."
            Case "tl"
                If actionText = "logged in" Then
                    Return $"[System] {actorName} nag-login."
                End If
                Return $"[System] {actorName} nag-logout."
            Case Else
                If actionText = "logged in" Then
                    Return $"[System] {actorName} has logged in."
                End If
                Return $"[System] {actorName} has logged out."
        End Select
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

    Private Sub QueueChatTranslation(sourceLine As String, targetLanguage As String, generation As Integer, entryIndex As Integer)
        Dim lineCopy As String = If(sourceLine, "").Trim()
        If lineCopy = "" Then
            Return
        End If

        Task.Run(
            Async Function()
                If generation <> _chatScreenGeneration Then
                    Return
                End If

                Await _chatTranslationLock.WaitAsync()
                Try
                    If generation <> _chatScreenGeneration Then
                        Return
                    End If

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
                                ApplyTranslatedChatEntry(generation, entryIndex, lineCopy, translated)
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

    Private Sub ApplyTranslatedChatEntry(generation As Integer, entryIndex As Integer, sourceText As String, translatedText As String)
        If generation <> _chatScreenGeneration OrElse entryIndex < 0 OrElse entryIndex >= _chatOverlayEntries.Count Then
            Return
        End If

        Dim entry As ChatOverlayLine = _chatOverlayEntries(entryIndex)
        If entry Is Nothing OrElse Not NormalizeChatLineKey(entry.SourceText).Equals(NormalizeChatLineKey(sourceText), StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

        entry.TranslatedText = If(String.IsNullOrWhiteSpace(translatedText), sourceText, translatedText.Trim())

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

        Dim visibleEntries As List(Of ChatOverlayLine) = _chatOverlayEntries.
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
        _deadHpConfirmCount = 0
        _deadHpFirstSeenUtc = DateTime.MinValue
        _windowMissingConfirmCount = 0
        _windowMissingFirstSeenUtc = DateTime.MinValue
        _deathNotificationLatched = False
        _windowMissingNotificationLatched = False
        AppendLog($"Startup guard: suppressing death/window alerts for {StartupNotificationWarmupSeconds} seconds.")
    End Sub

    Private Function IsNotificationWarmupActive() As Boolean
        Return DateTime.UtcNow < _notificationWarmupUntilUtc
    End Function

    Private Shared Function FormatPendingAlertSeconds(firstSeenUtc As DateTime) As String
        If firstSeenUtc = DateTime.MinValue Then
            Return "0"
        End If
        Return CInt(Math.Max(0, (DateTime.UtcNow - firstSeenUtc).TotalSeconds)).ToString()
    End Function

    Private Shared Function IsCriticalAlertConfirmed(firstSeenUtc As DateTime, confirmCount As Integer) As Boolean
        If firstSeenUtc = DateTime.MinValue Then
            Return False
        End If
        Return confirmCount >= CriticalAlertConfirmFrames OrElse
               (DateTime.UtcNow - firstSeenUtc).TotalMilliseconds >= CriticalAlertConfirmMs
    End Function

    Private Sub BrowseAutoRelaunchExeClicked(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "Select game executable"
            dialog.Filter = "Launch files (*.exe;*.lnk;*.url;*.bat;*.cmd)|*.exe;*.lnk;*.url;*.bat;*.cmd|All files (*.*)|*.*"
            dialog.CheckFileExists = True
            dialog.Multiselect = False
            If txtAutoRelaunchExePath IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(txtAutoRelaunchExePath.Text) Then
                Try
                    Dim currentPath As String = txtAutoRelaunchExePath.Text.Trim()
                    Dim currentDir As String = Path.GetDirectoryName(currentPath)
                    If Not String.IsNullOrWhiteSpace(currentDir) AndAlso Directory.Exists(currentDir) Then
                        dialog.InitialDirectory = currentDir
                    End If
                    dialog.FileName = Path.GetFileName(currentPath)
                Catch
                End Try
            End If

            If dialog.ShowDialog(Me) = DialogResult.OK Then
                txtAutoRelaunchExePath.Text = dialog.FileName
                If chkAutoRelaunchGame IsNot Nothing Then
                    chkAutoRelaunchGame.Checked = True
                End If
                SavePersistedListState(False)
            End If
        End Using
    End Sub

    Private Sub TestAutoRelaunchClicked(sender As Object, e As EventArgs)
        ScheduleGameRelaunch("manual test", stopRunningBots:=False, force:=True)
    End Sub

    Private Sub AutoRelaunchClicksMouseDown(sender As Object, e As MouseEventArgs)
        _autoRelaunchDragRowIndex = -1
        _autoRelaunchDragStartPoint = System.Drawing.Point.Empty
        If e.Button <> MouseButtons.Left OrElse dgvAutoRelaunchClicks Is Nothing Then
            Return
        End If

        Dim hit As DataGridView.HitTestInfo = dgvAutoRelaunchClicks.HitTest(e.X, e.Y)
        If hit.RowIndex < 0 OrElse hit.RowIndex >= dgvAutoRelaunchClicks.Rows.Count Then
            Return
        End If

        _autoRelaunchDragRowIndex = hit.RowIndex
        _autoRelaunchDragStartPoint = New System.Drawing.Point(e.X, e.Y)
    End Sub

    Private Sub AutoRelaunchClicksMouseMove(sender As Object, e As MouseEventArgs)
        If dgvAutoRelaunchClicks Is Nothing OrElse _autoRelaunchDragRowIndex < 0 Then
            Return
        End If
        If (e.Button And MouseButtons.Left) <> MouseButtons.Left Then
            Return
        End If
        If dgvAutoRelaunchClicks.IsCurrentCellInEditMode Then
            Return
        End If

        Dim dragSize As Size = SystemInformation.DragSize
        Dim dragBounds As New Rectangle(
            _autoRelaunchDragStartPoint.X - (dragSize.Width \ 2),
            _autoRelaunchDragStartPoint.Y - (dragSize.Height \ 2),
            dragSize.Width,
            dragSize.Height)
        If dragBounds.Contains(e.Location) Then
            Return
        End If

        _autoRelaunchDragInProgress = True
        Try
            dgvAutoRelaunchClicks.DoDragDrop(_autoRelaunchDragRowIndex, DragDropEffects.Move)
        Finally
            _autoRelaunchDragInProgress = False
            _autoRelaunchDragRowIndex = -1
            _autoRelaunchDragStartPoint = System.Drawing.Point.Empty
        End Try
    End Sub

    Private Sub AutoRelaunchClicksDragOver(sender As Object, e As DragEventArgs)
        e.Effect = If(_autoRelaunchDragInProgress AndAlso _autoRelaunchDragRowIndex >= 0, DragDropEffects.Move, DragDropEffects.None)
    End Sub

    Private Sub AutoRelaunchClicksDragDrop(sender As Object, e As DragEventArgs)
        If dgvAutoRelaunchClicks Is Nothing OrElse _autoRelaunchDragRowIndex < 0 OrElse _autoRelaunchDragRowIndex >= dgvAutoRelaunchClicks.Rows.Count Then
            Return
        End If

        Dim clientPoint As System.Drawing.Point = dgvAutoRelaunchClicks.PointToClient(New System.Drawing.Point(e.X, e.Y))
        Dim hit As DataGridView.HitTestInfo = dgvAutoRelaunchClicks.HitTest(clientPoint.X, clientPoint.Y)
        Dim dropIndex As Integer = If(hit.RowIndex >= 0, hit.RowIndex, dgvAutoRelaunchClicks.Rows.Count)
        ReorderAutoRelaunchClickRow(_autoRelaunchDragRowIndex, dropIndex)
    End Sub

    Private Sub ReorderAutoRelaunchClickRow(sourceIndex As Integer, dropIndex As Integer)
        If dgvAutoRelaunchClicks Is Nothing OrElse sourceIndex < 0 OrElse sourceIndex >= dgvAutoRelaunchClicks.Rows.Count Then
            Return
        End If

        If dropIndex > sourceIndex Then
            dropIndex -= 1
        End If
        dropIndex = Math.Max(0, Math.Min(dgvAutoRelaunchClicks.Rows.Count - 1, dropIndex))
        If dropIndex = sourceIndex Then
            Return
        End If

        CommitPendingGridEdits()
        Dim sourceRow As DataGridViewRow = dgvAutoRelaunchClicks.Rows(sourceIndex)
        Dim values(dgvAutoRelaunchClicks.Columns.Count - 1) As Object
        For i As Integer = 0 To dgvAutoRelaunchClicks.Columns.Count - 1
            values(i) = sourceRow.Cells(i).Value
        Next

        dgvAutoRelaunchClicks.Rows.RemoveAt(sourceIndex)
        dgvAutoRelaunchClicks.Rows.Insert(dropIndex, values)
        RenumberAutoRelaunchClickRows()
        dgvAutoRelaunchClicks.ClearSelection()
        dgvAutoRelaunchClicks.Rows(dropIndex).Selected = True
        dgvAutoRelaunchClicks.CurrentCell = dgvAutoRelaunchClicks.Rows(dropIndex).Cells("Step")
        SavePersistedListState(False)
        AppendLog($"Auto relaunch click step moved to row {dropIndex + 1}.")
    End Sub

    Private Sub RenumberAutoRelaunchClickRows()
        If dgvAutoRelaunchClicks Is Nothing Then
            Return
        End If

        For i As Integer = 0 To dgvAutoRelaunchClicks.Rows.Count - 1
            dgvAutoRelaunchClicks.Rows(i).Cells("Step").Value = (i + 1).ToString()
        Next
    End Sub

    Private Sub AutoRelaunchUseCursorClicked(sender As Object, e As EventArgs)
        If dgvAutoRelaunchClicks Is Nothing Then
            Return
        End If

        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
            AppendLog("Auto relaunch click setup: select a Full game process window first.")
            Return
        End If

        Dim rowIndex As Integer = If(dgvAutoRelaunchClicks.CurrentCell IsNot Nothing, dgvAutoRelaunchClicks.CurrentCell.RowIndex, 0)
        If rowIndex < 0 OrElse rowIndex >= dgvAutoRelaunchClicks.Rows.Count Then
            rowIndex = 0
        End If

        _isPickingAutoRelaunchClick = True
        _autoRelaunchRightMouseWasDown = False
        _pendingAutoRelaunchClickRowIndex = rowIndex
        _isPickingArrowUnbundlePoint = False
        _arrowUnbundleLeftMouseWasDown = False
        UpdateAutoRelaunchUseCursorUi()
        UpdateArrowUnbundleUi()
        AppendLog($"Auto relaunch click step {rowIndex + 1}: RIGHT click the desired spot inside the selected game window.")
        NativeMethods.SetForegroundWindow(selected.MainWindowHandle)
    End Sub

    Private Sub HandlePendingAutoRelaunchClickCapture()
        Try
            If Not _isPickingAutoRelaunchClick Then
                Return
            End If

            Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
                Return
            End If

            Dim rightDown As Boolean = (GetAsyncKeyState(CInt(Keys.RButton)) And &H8000S) <> 0
            If rightDown AndAlso Not _autoRelaunchRightMouseWasDown Then
                Dim screenPoint As NativeMethods.POINT
                If NativeMethods.GetCursorPos(screenPoint) Then
                    Dim hoveredWindow As IntPtr = NativeMethods.WindowFromPoint(screenPoint)
                    Dim hoveredRoot As IntPtr = If(hoveredWindow <> IntPtr.Zero, NativeMethods.GetAncestor(hoveredWindow, NativeMethods.GA_ROOT), IntPtr.Zero)
                    If hoveredRoot <> selected.MainWindowHandle Then
                        AppendLog("Auto relaunch click setup: right click must be inside the selected game window.")
                        _autoRelaunchRightMouseWasDown = rightDown
                        Return
                    End If

                    Dim clientPoint As NativeMethods.POINT = screenPoint
                    If NativeMethods.ScreenToClient(selected.MainWindowHandle, clientPoint) Then
                        Dim clientRect As NativeMethods.RECT
                        If Not NativeMethods.GetClientRect(selected.MainWindowHandle, clientRect) Then
                            _autoRelaunchRightMouseWasDown = rightDown
                            Return
                        End If

                        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
                        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
                        If clientPoint.X < 0 OrElse clientPoint.Y < 0 OrElse clientPoint.X >= clientWidth OrElse clientPoint.Y >= clientHeight Then
                            AppendLog("Auto relaunch click setup: right click must be inside the selected game window.")
                            _autoRelaunchRightMouseWasDown = rightDown
                            Return
                        End If

                        Dim rowIndex As Integer = _pendingAutoRelaunchClickRowIndex
                        If rowIndex < 0 OrElse dgvAutoRelaunchClicks Is Nothing OrElse rowIndex >= dgvAutoRelaunchClicks.Rows.Count Then
                            rowIndex = 0
                        End If

                        Dim row As DataGridViewRow = dgvAutoRelaunchClicks.Rows(rowIndex)
                        row.Cells("Enabled").Value = True
                        row.Cells("X").Value = Math.Max(0, clientPoint.X).ToString()
                        row.Cells("Y").Value = Math.Max(0, clientPoint.Y).ToString()
                        If String.IsNullOrWhiteSpace(If(row.Cells("Delay").Value, "").ToString()) Then
                            row.Cells("Delay").Value = If(rowIndex = 0, "15", "5")
                        End If

                        _isPickingAutoRelaunchClick = False
                        _autoRelaunchRightMouseWasDown = rightDown
                        _pendingAutoRelaunchClickRowIndex = -1
                        UpdateAutoRelaunchUseCursorUi()
                        SavePersistedListState(False)
                        AppendLog($"Auto relaunch click step {rowIndex + 1} set to game {clientPoint.X},{clientPoint.Y}.")
                    End If
                End If
            End If

            _autoRelaunchRightMouseWasDown = rightDown
        Catch ex As Exception
            _isPickingAutoRelaunchClick = False
            _autoRelaunchRightMouseWasDown = False
            _pendingAutoRelaunchClickRowIndex = -1
            UpdateAutoRelaunchUseCursorUi()
            AppendLog("Auto relaunch click capture failed: " & ex.Message)
        End Try
    End Sub

    Private Sub UpdateAutoRelaunchUseCursorUi()
        If btnAutoRelaunchUseCursor Is Nothing Then
            Return
        End If

        btnAutoRelaunchUseCursor.Text = If(_isPickingAutoRelaunchClick, "RIGHT click...", "Use Cursor")
        btnAutoRelaunchUseCursor.BackColor = If(_isPickingAutoRelaunchClick, Color.FromArgb(175, 110, 30), Color.FromArgb(45, 95, 140))
    End Sub

    Private Sub AutoRelaunchClickOverlayChanged(sender As Object, e As EventArgs)
        SetAutoRelaunchClickOverlayVisible(chkAutoRelaunchClickOverlay IsNot Nothing AndAlso chkAutoRelaunchClickOverlay.Checked)
    End Sub

    Private Sub SetAutoRelaunchClickOverlayVisible(visible As Boolean)
        If Not visible Then
            If _autoRelaunchClickOverlayForm IsNot Nothing AndAlso Not _autoRelaunchClickOverlayForm.IsDisposed Then
                _autoRelaunchClickOverlayForm.Close()
            End If
            _autoRelaunchClickOverlayForm = Nothing
            Return
        End If

        If _autoRelaunchClickOverlayForm IsNot Nothing AndAlso Not _autoRelaunchClickOverlayForm.IsDisposed Then
            Return
        End If

        _autoRelaunchClickOverlayForm = New AutoRelaunchClickOverlayForm(
            Function() ResolveAutoRelaunchClickWindow(IntPtr.Zero, ""),
            Function() GetAutoRelaunchOverlaySteps())
        AddHandler _autoRelaunchClickOverlayForm.FormClosed,
            Sub(_s As Object, _e As FormClosedEventArgs)
                _autoRelaunchClickOverlayForm = Nothing
            End Sub
        _autoRelaunchClickOverlayForm.Show(Me)
    End Sub

    Private Function GetAutoRelaunchOverlaySteps() As List(Of AutoRelaunchOverlayStep)
        Dim enabledSteps As List(Of PersistedAutoRelaunchClick) = GetEnabledAutoRelaunchClickSteps()
        Dim overlaySteps As New List(Of AutoRelaunchOverlayStep)()
        For i As Integer = 0 To enabledSteps.Count - 1
            Dim stepInfo As PersistedAutoRelaunchClick = enabledSteps(i)
            overlaySteps.Add(New AutoRelaunchOverlayStep With {
                .StepNumber = i + 1,
                .X = stepInfo.X,
                .Y = stepInfo.Y,
                .DelaySeconds = stepInfo.DelaySeconds,
                .Description = If(stepInfo.Description, "").Trim()
            })
        Next
        Return overlaySteps
    End Function

    Private Sub AutoRelaunchClearClicksClicked(sender As Object, e As EventArgs)
        _isPickingAutoRelaunchClick = False
        _autoRelaunchRightMouseWasDown = False
        _pendingAutoRelaunchClickRowIndex = -1
        UpdateAutoRelaunchUseCursorUi()
        ResetAutoRelaunchClickGrid()
        SavePersistedListState(False)
        AppendLog("Auto relaunch post-launch clicks cleared.")
    End Sub

    Private Sub ResetAutoRelaunchClickGrid()
        If dgvAutoRelaunchClicks Is Nothing Then
            Return
        End If

        dgvAutoRelaunchClicks.Rows.Clear()
        For i As Integer = 1 To 5
            dgvAutoRelaunchClicks.Rows.Add(i.ToString(), False, "0", "0", If(i = 1, "15", "5"), "")
        Next
    End Sub

    Private Function IsAutoRelaunchGameEnabled() As Boolean
        Return chkAutoRelaunchGame IsNot Nothing AndAlso chkAutoRelaunchGame.Checked
    End Function

    Private Function GetAutoRelaunchGamePath() As String
        Return If(txtAutoRelaunchExePath IsNot Nothing, txtAutoRelaunchExePath.Text.Trim(), "")
    End Function

    Private Function GetAutoRelaunchDelaySeconds() As Integer
        If nudAutoRelaunchDelaySeconds Is Nothing Then
            Return 5
        End If
        Return CInt(Math.Max(nudAutoRelaunchDelaySeconds.Minimum, Math.Min(nudAutoRelaunchDelaySeconds.Maximum, nudAutoRelaunchDelaySeconds.Value)))
    End Function

    Private Function GetAutoRelaunchClickSteps() As List(Of PersistedAutoRelaunchClick)
        Dim steps As New List(Of PersistedAutoRelaunchClick)()
        If dgvAutoRelaunchClicks Is Nothing Then
            Return steps
        End If

        For Each row As DataGridViewRow In dgvAutoRelaunchClicks.Rows
            If row Is Nothing OrElse row.IsNewRow Then
                Continue For
            End If

            Dim enabled As Boolean = False
            Boolean.TryParse(If(row.Cells("Enabled").Value, "False").ToString(), enabled)

            Dim x As Integer = 0
            Dim y As Integer = 0
            Dim delaySeconds As Decimal = 0D
            Integer.TryParse(If(row.Cells("X").Value, "0").ToString(), x)
            Integer.TryParse(If(row.Cells("Y").Value, "0").ToString(), y)
            Decimal.TryParse(If(row.Cells("Delay").Value, "0").ToString(), delaySeconds)
            Dim description As String = If(row.Cells("Description").Value, "").ToString().Trim()

            steps.Add(New PersistedAutoRelaunchClick With {
                .Enabled = enabled,
                .X = Math.Max(0, Math.Min(32767, x)),
                .Y = Math.Max(0, Math.Min(32767, y)),
                .DelaySeconds = Math.Max(0D, Math.Min(600D, delaySeconds)),
                .Description = description
            })
        Next

        While steps.Count < 5
            steps.Add(New PersistedAutoRelaunchClick With {.Enabled = False, .X = 0, .Y = 0, .DelaySeconds = If(steps.Count = 0, 15D, 5D), .Description = ""})
        End While
        If steps.Count > 5 Then
            steps = steps.Take(5).ToList()
        End If

        Return steps
    End Function

    Private Function GetEnabledAutoRelaunchClickSteps() As List(Of PersistedAutoRelaunchClick)
        Return GetAutoRelaunchClickSteps().
            Where(Function(stepInfo) stepInfo IsNot Nothing AndAlso stepInfo.Enabled).
            Select(Function(stepInfo) New PersistedAutoRelaunchClick With {
                .Enabled = True,
                .X = Math.Max(0, Math.Min(32767, stepInfo.X)),
                .Y = Math.Max(0, Math.Min(32767, stepInfo.Y)),
                .DelaySeconds = Math.Max(0D, Math.Min(600D, stepInfo.DelaySeconds)),
                .Description = If(stepInfo.Description, "").Trim()
            }).
            ToList()
    End Function

    Private Sub ApplyAutoRelaunchClickSteps(steps As List(Of PersistedAutoRelaunchClick))
        If dgvAutoRelaunchClicks Is Nothing Then
            Return
        End If

        dgvAutoRelaunchClicks.Rows.Clear()
        Dim normalized As List(Of PersistedAutoRelaunchClick) = If(steps, New List(Of PersistedAutoRelaunchClick)())
        For i As Integer = 0 To 4
            Dim stepInfo As PersistedAutoRelaunchClick = If(i < normalized.Count AndAlso normalized(i) IsNot Nothing, normalized(i), New PersistedAutoRelaunchClick With {.Enabled = False, .X = 0, .Y = 0, .DelaySeconds = If(i = 0, 15D, 5D), .Description = ""})
            dgvAutoRelaunchClicks.Rows.Add(
                (i + 1).ToString(),
                stepInfo.Enabled,
                Math.Max(0, Math.Min(32767, stepInfo.X)).ToString(),
                Math.Max(0, Math.Min(32767, stepInfo.Y)).ToString(),
                Math.Max(0D, Math.Min(600D, stepInfo.DelaySeconds)).ToString("0.###"),
                If(stepInfo.Description, "").Trim())
        Next
    End Sub

    Private Sub ExecuteAutoRelaunchClickSteps(steps As List(Of PersistedAutoRelaunchClick), trigger As String, preferredHwnd As IntPtr, preferredTitle As String)
        If steps Is Nothing OrElse steps.Count = 0 Then
            Return
        End If

        Dim previousCursor As NativeMethods.POINT
        Dim hadCursor As Boolean = NativeMethods.GetCursorPos(previousCursor)
        For i As Integer = 0 To steps.Count - 1
            Dim stepInfo As PersistedAutoRelaunchClick = steps(i)
            Dim delayMs As Integer = CInt(Math.Min(600000D, Math.Max(0D, stepInfo.DelaySeconds) * 1000D))
            If delayMs > 0 Then
                Thread.Sleep(delayMs)
            End If

            Dim clickPoint As NativeMethods.POINT
            Dim clickHwnd As IntPtr = ResolveAutoRelaunchClickWindow(preferredHwnd, preferredTitle)
            Dim mappedToClient As Boolean = TryMapAutoRelaunchClientPointToScreen(clickHwnd, stepInfo.X, stepInfo.Y, clickPoint)
            If Not mappedToClient Then
                clickPoint = New NativeMethods.POINT With {.X = stepInfo.X, .Y = stepInfo.Y}
            Else
                EnsureAutoRelaunchClickWindowForeground(clickHwnd, i + 1)
            End If

            If NativeMethods.SetCursorPos(clickPoint.X, clickPoint.Y) Then
                Thread.Sleep(80)
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, CUInt(clickPoint.X), CUInt(clickPoint.Y), 0UI, UIntPtr.Zero)
                Thread.Sleep(70)
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, CUInt(clickPoint.X), CUInt(clickPoint.Y), 0UI, UIntPtr.Zero)
                Dim description As String = If(String.IsNullOrWhiteSpace(stepInfo.Description), "", $" [{stepInfo.Description.Trim()}]")
                Dim coordinateNote As String = If(mappedToClient, $"game {stepInfo.X},{stepInfo.Y} -> screen {clickPoint.X},{clickPoint.Y}", $"screen {clickPoint.X},{clickPoint.Y}")
                AppendLogSafe($"Auto relaunch post-launch click {i + 1}{description} sent at {coordinateNote} ({trigger}).")
            Else
                AppendLogSafe($"Auto relaunch post-launch click {i + 1} skipped: SetCursorPos failed.")
            End If
        Next

        If hadCursor Then
            NativeMethods.SetCursorPos(previousCursor.X, previousCursor.Y)
        End If
    End Sub

    Private Function ResolveAutoRelaunchClickWindow(preferredHwnd As IntPtr, preferredTitle As String) As IntPtr
        If IsUsableClientWindow(preferredHwnd) Then
            Return preferredHwnd
        End If

        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected IsNot Nothing AndAlso IsUsableClientWindow(selected.MainWindowHandle) Then
            Return selected.MainWindowHandle
        End If

        Dim title As String = If(preferredTitle, "").Trim()
        If title <> "" Then
            Dim titleHwnd As IntPtr = NativeMethods.FindWindow(Nothing, title)
            If IsUsableClientWindow(titleHwnd) Then
                Return titleHwnd
            End If
        End If

        Dim defaultHwnd As IntPtr = NativeMethods.FindWindow(Nothing, DefaultGameWindowTitle)
        If IsUsableClientWindow(defaultHwnd) Then
            Return defaultHwnd
        End If

        Return IntPtr.Zero
    End Function

    Private Sub EnsureAutoRelaunchClickWindowForeground(hwnd As IntPtr, stepNumber As Integer)
        If hwnd = IntPtr.Zero Then
            Return
        End If

        Try
            If NativeMethods.IsIconic(hwnd) Then
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE)
                Thread.Sleep(250)
            End If

            For attempt As Integer = 1 To 3
                NativeMethods.SetForegroundWindow(hwnd)
                Thread.Sleep(180)

                Dim foreground As IntPtr = NativeMethods.GetForegroundWindow()
                Dim foregroundRoot As IntPtr = If(foreground <> IntPtr.Zero, NativeMethods.GetAncestor(foreground, NativeMethods.GA_ROOT), IntPtr.Zero)
                If foreground = hwnd OrElse foregroundRoot = hwnd Then
                    Return
                End If
            Next

            AppendLogSafe($"Auto relaunch post-launch click {stepNumber}: game window did not become foreground; clicking mapped game coordinates anyway.")
        Catch ex As Exception
            AppendLogSafe($"Auto relaunch post-launch click {stepNumber}: unable to foreground game window: {ex.Message}")
        End Try
    End Sub

    Private Shared Function IsUsableClientWindow(hwnd As IntPtr) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            Return False
        End If

        Return (clientRect.Right - clientRect.Left) > 0 AndAlso (clientRect.Bottom - clientRect.Top) > 0
    End Function

    Private Shared Function TryMapAutoRelaunchClientPointToScreen(hwnd As IntPtr, clientX As Integer, clientY As Integer, ByRef screenPoint As NativeMethods.POINT) As Boolean
        screenPoint = New NativeMethods.POINT With {.X = 0, .Y = 0}
        If hwnd = IntPtr.Zero OrElse clientX < 0 OrElse clientY < 0 Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            Return False
        End If

        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
        If clientX >= clientWidth OrElse clientY >= clientHeight Then
            Return False
        End If

        Dim pt As New NativeMethods.POINT With {.X = clientX, .Y = clientY}
        If Not NativeMethods.ClientToScreen(hwnd, pt) Then
            Return False
        End If

        screenPoint = pt
        Return True
    End Function

    Private Sub ScheduleGameRelaunch(trigger As String, Optional stopRunningBots As Boolean = True, Optional force As Boolean = False)
        If (Not force) AndAlso Not IsAutoRelaunchGameEnabled() Then
            Return
        End If

        Dim launchPath As String = GetAutoRelaunchGamePath()
        If String.IsNullOrWhiteSpace(launchPath) Then
            AppendLog("Auto relaunch skipped: choose the game EXE path first.")
            Return
        End If

        If Not File.Exists(launchPath) Then
            AppendLog($"Auto relaunch skipped: launch path does not exist: {launchPath}")
            Return
        End If

        Dim now As DateTime = DateTime.UtcNow
        Dim delaySeconds As Integer = GetAutoRelaunchDelaySeconds()
        Dim postLaunchClicks As List(Of PersistedAutoRelaunchClick) = GetEnabledAutoRelaunchClickSteps()
        Dim selectedForClicks As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        Dim postLaunchClickHwnd As IntPtr = If(selectedForClicks IsNot Nothing, selectedForClicks.MainWindowHandle, IntPtr.Zero)
        Dim postLaunchClickTitle As String = If(selectedForClicks IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selectedForClicks.WindowTitle), selectedForClicks.WindowTitle.Trim(), GetSelectedWindowTitleForFallback(BotEdition.Full))
        Dim restartEdition As BotEdition? = If(stopRunningBots, GetRunningEdition(), Nothing)
        Dim cooldownSeconds As Integer = Math.Max(30, delaySeconds + 10)
        If Not force Then
            If _autoRelaunchPending Then
                Return
            End If
            If _lastAutoRelaunchAttemptUtc <> DateTime.MinValue AndAlso (now - _lastAutoRelaunchAttemptUtc).TotalSeconds < cooldownSeconds Then
                Return
            End If
        End If

        _autoRelaunchPending = True
        _lastAutoRelaunchAttemptUtc = now

        If stopRunningBots Then
            If _fullEngine.IsRunning() Then
                StopEdition(BotEdition.Full, False, $"auto relaunch: {trigger}")
            End If
            If _liteEngine.IsRunning() Then
                StopEdition(BotEdition.Lite, False, $"auto relaunch: {trigger}")
            End If
        End If

        AppendLog($"Auto relaunch scheduled after {delaySeconds}s ({trigger}): {launchPath}")
        If postLaunchClicks.Count > 0 Then
            AppendLog($"Auto relaunch will run {postLaunchClicks.Count} post-launch click step(s).")
        End If
        Task.Run(
            Async Function()
                Try
                    If delaySeconds > 0 Then
                        Await Task.Delay(TimeSpan.FromSeconds(delaySeconds))
                    End If

                    Dim psi As New ProcessStartInfo() With {
                        .FileName = launchPath,
                        .UseShellExecute = True
                    }
                    Dim extension As String = Path.GetExtension(launchPath)
                    If extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) Then
                        Dim workingDir As String = Path.GetDirectoryName(launchPath)
                        If Not String.IsNullOrWhiteSpace(workingDir) AndAlso Directory.Exists(workingDir) Then
                            psi.WorkingDirectory = workingDir
                        End If
                    End If

                    Dim launchedProcess As Process = Process.Start(psi)
                    AppendLogSafe($"Auto relaunch started game ({trigger}).")
                    If launchedProcess IsNot Nothing Then
                        Try
                            For attempt As Integer = 1 To 20
                                launchedProcess.Refresh()
                                If launchedProcess.MainWindowHandle <> IntPtr.Zero Then
                                    postLaunchClickHwnd = launchedProcess.MainWindowHandle
                                    Exit For
                                End If
                                Await Task.Delay(250)
                            Next
                        Catch
                        End Try
                    End If
                    ExecuteAutoRelaunchClickSteps(postLaunchClicks, trigger, postLaunchClickHwnd, postLaunchClickTitle)
                    If restartEdition.HasValue Then
                        Await RestartEditionAfterAutoRelaunchAsync(restartEdition.Value, postLaunchClickHwnd, postLaunchClickTitle, trigger)
                    End If
                Catch ex As Exception
                    AppendLogSafe("Auto relaunch failed: " & ex.Message)
                Finally
                    _autoRelaunchPending = False
                End Try
            End Function)
    End Sub

    Private Async Function RestartEditionAfterAutoRelaunchAsync(edition As BotEdition, preferredHwnd As IntPtr, preferredTitle As String, trigger As String) As Task
        AppendLogSafe($"Auto relaunch waiting for game to be ready before restarting {edition}.")

        Dim readyHwnd As IntPtr = Await WaitForAutoRelaunchReadyWindowAsync(preferredHwnd, preferredTitle, TimeSpan.FromSeconds(90))
        If readyHwnd = IntPtr.Zero Then
            AppendLogSafe($"Auto relaunch did not restart {edition}: game window was not capture-ready after relaunch.")
            Return
        End If

        If IsDisposed OrElse Not IsHandleCreated Then
            Return
        End If

        BeginInvoke(New Action(
            Sub()
                Try
                    RefreshProcessWindowList(False, readyHwnd)
                    SyncProcessSelectionAcrossLists(readyHwnd)
                    UpdateSelectedProcessDisplay(GetSelectedProcessWindowForEdition(edition))
                    If IsEditionRunning(edition) Then
                        AppendLog($"Auto relaunch restart skipped: {edition} is already running.")
                        Return
                    End If

                    AppendLog($"Auto relaunch restarting {edition} after {trigger}.")
                    StartEdition(edition, False)
                Catch ex As Exception
                    AppendLog("Auto relaunch restart failed: " & ex.Message)
                End Try
            End Sub))
    End Function

    Private Async Function WaitForAutoRelaunchReadyWindowAsync(preferredHwnd As IntPtr, preferredTitle As String, timeout As TimeSpan) As Task(Of IntPtr)
        Dim deadline As DateTime = DateTime.UtcNow.Add(timeout)
        Dim lastLogUtc As DateTime = DateTime.MinValue

        Do
            Dim hwnd As IntPtr = ResolveAutoRelaunchRestartWindow(preferredHwnd, preferredTitle)
            If hwnd <> IntPtr.Zero Then
                Using frame As Bitmap = BotEngine.CaptureClient(hwnd)
                    If frame IsNot Nothing AndAlso frame.Width > 10 AndAlso frame.Height > 10 Then
                        Return hwnd
                    End If
                End Using
            End If

            If lastLogUtc = DateTime.MinValue OrElse (DateTime.UtcNow - lastLogUtc).TotalSeconds >= 10 Then
                AppendLogSafe("Auto relaunch restart waiting: game capture is not ready yet.")
                lastLogUtc = DateTime.UtcNow
            End If

            Await Task.Delay(1000)
        Loop While DateTime.UtcNow < deadline

        Return IntPtr.Zero
    End Function

    Private Function ResolveAutoRelaunchRestartWindow(preferredHwnd As IntPtr, preferredTitle As String) As IntPtr
        If IsUsableClientWindow(preferredHwnd) Then
            Return preferredHwnd
        End If

        Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
        If selected IsNot Nothing AndAlso IsUsableClientWindow(selected.MainWindowHandle) Then
            Return selected.MainWindowHandle
        End If

        Dim title As String = If(preferredTitle, "").Trim()
        If title <> "" Then
            Dim titleHwnd As IntPtr = NativeMethods.FindWindow(Nothing, title)
            If IsUsableClientWindow(titleHwnd) Then
                Return titleHwnd
            End If
        End If

        Dim defaultHwnd As IntPtr = NativeMethods.FindWindow(Nothing, DefaultGameWindowTitle)
        If IsUsableClientWindow(defaultHwnd) Then
            Return defaultHwnd
        End If

        Return IntPtr.Zero
    End Function

    Private Sub HandleGameDisconnectedAlert(status As BotStatus)
        If status Is Nothing Then
            Return
        End If

        If status.Running AndAlso status.WindowFound AndAlso status.GameDisconnected Then
            If Not _gameDisconnectedNotificationLatched Then
                _gameDisconnectedNotificationLatched = True
                AppendLog($"Game disconnect detected. Sending alert via {GetNotificationDestinationSummary()}.")
                TryClickDisconnectOkBeforeRelaunch()
                ScheduleGameRelaunch("server disconnect")
                Task.Run(
                    Async Function()
                        Dim sent As Boolean = Await SendPhoneNotificationAsync("KathanaBot Game Disconnected", "The game reported: connection to server has failed. Please try again.", DeathNotificationRetryCount)
                        If sent Then
                            AppendLogSafe("Game disconnect alert sent.")
                        Else
                            AppendLogSafe("Game disconnect alert failed.")
                        End If
                    End Function)
            End If
            Return
        End If

        If _gameDisconnectedNotificationLatched AndAlso ((Not status.Running) OrElse (Not status.GameDisconnected)) Then
            _gameDisconnectedNotificationLatched = False
            AppendLog("Game disconnect alert reset.")
        End If
    End Sub

    Private Sub TryClickDisconnectOkBeforeRelaunch()
        Try
            Dim selected As ProcessWindowEntry = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then
                AppendLog("Disconnect OK skipped: select a Full game process window first.")
                Return
            End If

            Dim cfg As BotConfig = BuildConfig()
            Dim okRegion As RectRegion = If(cfg.DisconnectOkRect, BotConfig.DefaultDisconnectOkRect())
            Dim clientRect As NativeMethods.RECT
            If Not NativeMethods.GetClientRect(selected.MainWindowHandle, clientRect) Then
                AppendLog("Disconnect OK skipped: game client rect unavailable.")
                Return
            End If

            Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
            Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
            Dim rect As Rectangle = okRegion.Clamp(clientWidth, clientHeight)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then
                AppendLog("Disconnect OK skipped: OK rectangle is outside the game client.")
                Return
            End If

            Dim clientX As Integer = rect.Left + (rect.Width \ 2)
            Dim clientY As Integer = rect.Top + (rect.Height \ 2)
            Dim screenPoint As New NativeMethods.POINT With {.X = clientX, .Y = clientY}
            If Not NativeMethods.ClientToScreen(selected.MainWindowHandle, screenPoint) Then
                AppendLog("Disconnect OK skipped: unable to map OK rectangle to screen.")
                Return
            End If

            Dim previousCursor As NativeMethods.POINT
            Dim hadCursor As Boolean = NativeMethods.GetCursorPos(previousCursor)
            Dim clicked As Boolean = False
            Try
                For attempt As Integer = 1 To 3
                    NativeMethods.SetForegroundWindow(selected.MainWindowHandle)
                    Thread.Sleep(180)
                    If Not NativeMethods.SetCursorPos(screenPoint.X, screenPoint.Y) Then
                        Continue For
                    End If

                    Thread.Sleep(80)
                    NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, CUInt(screenPoint.X), CUInt(screenPoint.Y), 0UI, UIntPtr.Zero)
                    Thread.Sleep(90)
                    NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, CUInt(screenPoint.X), CUInt(screenPoint.Y), 0UI, UIntPtr.Zero)
                    clicked = True
                    Thread.Sleep(220)

                    If attempt = 1 Then
                        BotEngine.ClickClientRegionCenter(selected.MainWindowHandle, okRegion, clientWidth, clientHeight)
                    End If
                Next

                BotEngine.SendKey(selected.MainWindowHandle, "ENTER", 60, True)
            Finally
                If hadCursor Then
                    NativeMethods.SetCursorPos(previousCursor.X, previousCursor.Y)
                End If
            End Try

            If clicked Then
                AppendLog($"Disconnect OK physical click sent at game {clientX},{clientY} -> screen {screenPoint.X},{screenPoint.Y}.")
            Else
                AppendLog("Disconnect OK click failed.")
            End If
        Catch ex As Exception
            AppendLog("Disconnect OK click failed: " & ex.Message)
        End Try
    End Sub

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
            _windowMissingConfirmCount = 0
            _windowMissingFirstSeenUtc = DateTime.MinValue
            Return
        End If

        If missingWindow OrElse captureUnavailable Then
            If _windowMissingFirstSeenUtc = DateTime.MinValue Then
                _windowMissingFirstSeenUtc = DateTime.UtcNow
                _windowMissingConfirmCount = 0
                AppendLog($"Game-window alert pending: waiting {CriticalAlertConfirmMs \ 1000} seconds or {CriticalAlertConfirmFrames} consecutive status samples before notification.")
            End If
            _windowMissingConfirmCount += 1

            If Not _windowMissingNotificationLatched AndAlso IsCriticalAlertConfirmed(_windowMissingFirstSeenUtc, _windowMissingConfirmCount) Then
                _windowMissingNotificationLatched = True
                ScheduleGameRelaunch(If(captureUnavailable, "capture unavailable", "game window missing/crash"))
                SendWindowMissingPhoneAlert(captureUnavailable)
            End If
            Return
        End If

        If status.WindowFound OrElse (Not status.Running) Then
            If _windowMissingFirstSeenUtc <> DateTime.MinValue AndAlso Not _windowMissingNotificationLatched Then
                AppendLog("Game-window alert canceled before confirmation.")
            End If
            _windowMissingConfirmCount = 0
            _windowMissingFirstSeenUtc = DateTime.MinValue
            _windowMissingNotificationLatched = False
        End If
    End Sub

    Private Sub OnEngineLogLine(edition As BotEdition, line As String)
        Dim prefixed As String = $"[{edition}] {line}"
        RecordLootHistoryFromEngineLog(edition, line)
        Dim isKeyAction As Boolean = IsKeyActionLogLine(line)
        If edition = BotEdition.Full AndAlso isKeyAction Then
            TrackKeyActionFromEngineLog(line)
        End If
        If isKeyAction Then
            prefixed = BuildRateLimitedLogMessage(
                prefixed,
                _lastEngineKeyActionLogUtc,
                _suppressedEngineKeyActionLogCount,
                HighFrequencyLogMinIntervalMs,
                "key action")
            If prefixed Is Nothing Then
                Return
            End If
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
            Dim role As String = NormalizePersistedRole(SafeCell(row, "Role", "attack"))
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
        cfg.WindowTitle = If(selected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selected.WindowTitle), selected.WindowTitle.Trim(), DefaultGameWindowTitle)
        cfg.SelectedWindowHandle = If(selected IsNot Nothing, selected.MainWindowHandle, IntPtr.Zero)
        cfg.LiteHpCheckPointX = _liteAutoPotHpPointX
        cfg.LiteHpCheckPointY = _liteAutoPotHpPointY
        cfg.LiteHpCheckColorEnabled = _liteAutoPotHpColorEnabled
        cfg.LiteHpCheckColorArgb = _liteAutoPotHpColorArgb
        cfg.LiteMpCheckPointX = _liteAutoPotMpPointX
        cfg.LiteMpCheckPointY = _liteAutoPotMpPointY
        cfg.LiteMpCheckColorEnabled = _liteAutoPotMpColorEnabled
        cfg.LiteMpCheckColorArgb = _liteAutoPotMpColorArgb
        ApplyBarColorSettingsToConfig(cfg)
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
        cfg.NtfyTopic = If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text.Trim(), "")
        cfg.Actions = New List(Of ActionRule)()

        For Each action As PersistedCombatAction In GetPersistedLiteActions()
            If action Is Nothing OrElse Not action.Enabled Then
                Continue For
            End If

            cfg.Actions.Add(New ActionRule With {
                .CooldownId = $"lite-action:{action.ActionKey.ToUpperInvariant()}",
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
                    .CooldownId = "lite-autopot:hp",
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
                    .CooldownId = "lite-autopot:mp",
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
        cfg.WindowTitle = If(selected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selected.WindowTitle), selected.WindowTitle.Trim(), DefaultGameWindowTitle)
        cfg.SelectedWindowHandle = If(selected IsNot Nothing, selected.MainWindowHandle, IntPtr.Zero)
        ApplyBarColorSettingsToConfig(cfg)
        cfg.LoopMs = CInt(nudLoopMs.Value)
        cfg.RetargetMs = CInt(nudRetargetMs.Value)
        cfg.ForcedRetargetMs = CInt(If(nudForcedRetargetMs IsNot Nothing, nudForcedRetargetMs.Value, nudRetargetMs.Value))
        cfg.StuckTargetMs = CInt(If(nudStuckTargetMs IsNot Nothing, nudStuckTargetMs.Value, 2200D))
        cfg.StuckTargetNoProgressRetargetMs = CInt(If(nudStuckNoProgressRetargetMs IsNot Nothing, nudStuckNoProgressRetargetMs.Value, 6000D))
        cfg.MobHpPresenceThreshold = CDbl(nudMobHpThreshold.Value)
        cfg.MonsterFilterMode = GetMonsterFilterMode()
        cfg.MonsterFilterConfirmReads = GetMonsterFilterConfirmReads()
        cfg.HighMaxHpSpecialEnabled = (chkHighMaxHpSpecial IsNot Nothing AndAlso chkHighMaxHpSpecial.Checked)
        cfg.HighMaxHpThreshold = CInt(If(nudHighMaxHpThreshold IsNot Nothing, nudHighMaxHpThreshold.Value, 2000D))
        cfg.AvoidHighMaxHpEnabled = (chkAvoidHighMaxHpTargets IsNot Nothing AndAlso chkAvoidHighMaxHpTargets.Checked)
        cfg.AvoidHighMaxHpThreshold = CInt(If(nudAvoidHighMaxHpThreshold IsNot Nothing, nudAvoidHighMaxHpThreshold.Value, 2000D))
        cfg.EvadeDadatiEnabled = (chkEvadeDadati IsNot Nothing AndAlso chkEvadeDadati.Checked)
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
        cfg.NtfyTopic = If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text.Trim(), "")
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
        cfg.NavigationReturnToStartEnabled = (chkNavigationReturnToStart IsNot Nothing AndAlso chkNavigationReturnToStart.Checked)
        cfg.HoldPlaceEnabled = (chkHoldPlaceEnabled IsNot Nothing AndAlso chkHoldPlaceEnabled.Checked)
        cfg.HoldPlaceAnchorSet = _holdPlaceAnchorSet
        cfg.HoldPlaceTargetX = If(_holdPlaceAnchorSet, CInt(If(nudHoldPlaceTargetX IsNot Nothing, nudHoldPlaceTargetX.Value, -1D)), -1)
        cfg.HoldPlaceTargetY = If(_holdPlaceAnchorSet, CInt(If(nudHoldPlaceTargetY IsNot Nothing, nudHoldPlaceTargetY.Value, -1D)), -1)
        cfg.HoldPlaceRestrictivenessMode = GetHoldPlaceRestrictivenessMode()
        cfg.HoldPlaceRadius = CInt(If(nudHoldPlaceRadius IsNot Nothing, nudHoldPlaceRadius.Value, 4D))
        cfg.HoldPlaceMoveBurstMs = CInt(If(nudHoldPlaceMoveBurstMs IsNot Nothing, nudHoldPlaceMoveBurstMs.Value, 750D))
        cfg.HoldPlaceCorrectionIntervalMs = CInt(If(nudHoldPlaceCorrectionMs IsNot Nothing, nudHoldPlaceCorrectionMs.Value, 900D))
        cfg.HoldPlacePostFightReturnEnabled = (chkHoldPlacePostFightReturn Is Nothing OrElse chkHoldPlacePostFightReturn.Checked)
        cfg.HoldPlaceCombatSafeEnabled = (chkHoldPlaceCombatSafe Is Nothing OrElse chkHoldPlaceCombatSafe.Checked)
        cfg.HoldPlaceEmergencyLeashDistance = CInt(If(nudHoldPlaceEmergencyLeash IsNot Nothing, nudHoldPlaceEmergencyLeash.Value, 60D))
        cfg.HoldPlaceDirectionLearningEnabled = (chkHoldPlaceDirectionLearning Is Nothing OrElse chkHoldPlaceDirectionLearning.Checked)
        cfg.ChatTranslationEnabled = (chkChatTranslationEnabled IsNot Nothing AndAlso chkChatTranslationEnabled.Checked)
        cfg.ChatTranslationOverlayEnabled = (chkChatTranslationOverlay IsNot Nothing AndAlso chkChatTranslationOverlay.Checked)
        cfg.DisabledCalibrationRegionOverlays = BuildDisabledCalibrationRegionOverlays()
        cfg.ChatTranslationTargetLanguage = GetSelectedChatTargetLanguageCode()
        cfg.ChatTranslationScanIntervalMs = CInt(If(nudChatScanMs IsNot Nothing, nudChatScanMs.Value, 700D))
        cfg.ChatTranslationMaxLines = CInt(If(nudChatMaxLines IsNot Nothing, nudChatMaxLines.Value, 6D))
        cfg.AdaptivePerformanceEnabled = (chkAdaptivePerformance Is Nothing OrElse chkAdaptivePerformance.Checked)
        cfg.PixelChangeGateEnabled = (chkPixelChangeGate Is Nothing OrElse chkPixelChangeGate.Checked)
        cfg.AdaptiveSlowLoopMinMs = CInt(If(nudAdaptiveSlowMinMs IsNot Nothing, nudAdaptiveSlowMinMs.Value, 140D))
        cfg.AdaptiveSlowLoopMultiplier = CDbl(If(nudAdaptiveSlowMultiplier IsNot Nothing, nudAdaptiveSlowMultiplier.Value, 1.8D))
        cfg.AdaptiveRecoveryLoopMultiplier = CDbl(If(nudAdaptiveRecoveryMultiplier IsNot Nothing, nudAdaptiveRecoveryMultiplier.Value, 1.25D))
        cfg.AdaptiveSlowConfirmCount = CInt(If(nudAdaptiveSlowConfirm IsNot Nothing, nudAdaptiveSlowConfirm.Value, 5D))
        cfg.AdaptiveRecoveryConfirmCount = CInt(If(nudAdaptiveRecoveryConfirm IsNot Nothing, nudAdaptiveRecoveryConfirm.Value, 14D))
        cfg.CaptureBackendPreference = GetSelectedCaptureBackendCode()
        cfg.FullFrameRefreshIntervalMs = CInt(If(nudFullFrameScanMs IsNot Nothing, nudFullFrameScanMs.Value, 500D))
        cfg.LootScannerIntervalMs = CInt(Math.Round(CDbl(If(nudLootScannerSeconds IsNot Nothing, nudLootScannerSeconds.Value, 10D)) * 1000.0R))
        cfg.MapCoordinateScanIntervalMs = CInt(If(nudMapScanMs IsNot Nothing, nudMapScanMs.Value, 900D))
        cfg.PartyListScanIntervalMs = CInt(If(nudPartyScanMs IsNot Nothing, nudPartyScanMs.Value, 700D))
        cfg.PartyInviteScanIntervalMs = CInt(If(nudPartyScanMs IsNot Nothing, nudPartyScanMs.Value, 900D))
        cfg.MobNameScanIntervalMs = CInt(If(nudMobNameScanMs IsNot Nothing, nudMobNameScanMs.Value, 650D))
        cfg.HpBar = BuildRect("hp_bar")
        cfg.MpBar = BuildRect("mp_bar")
        cfg.MobNameRect = BuildRect("mob_name_rect")
        cfg.MobHpRect = BuildRect("mob_hp_rect")
        cfg.MobLifeRect = BuildRectOrFallback("mob_life_rect", cfg.MobHpRect)
        cfg.UnreachableTextRect = BuildRect("unreachable_text_rect")
        cfg.PranaExpRect = BuildRect("prana_exp_rect")
        cfg.RupiahsRect = BuildRect("rupiahs_rect")
        cfg.PartyInviteScanRect = BuildRect("party_invite_scan_rect")
        cfg.PartyInviteOkRect = BuildRect("party_invite_ok_rect")
        cfg.PartyListRect = BuildRect("party_list_rect")
        cfg.DisconnectMessageRect = BuildRectOrFallback("disconnect_message_rect", BotConfig.DefaultDisconnectMessageRect())
        cfg.DisconnectOkRect = BuildRectOrFallback("disconnect_ok_rect", BotConfig.DefaultDisconnectOkRect())
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
        cfg.ArrowUnbundleEnabled = (chkArrowUnbundleEnabled IsNot Nothing AndAlso chkArrowUnbundleEnabled.Checked)
        cfg.ArrowUnbundleIntervalMs = CInt(Math.Round(CDbl(If(nudArrowUnbundleSeconds IsNot Nothing, nudArrowUnbundleSeconds.Value, 60D)) * 1000.0R))
        cfg.ArrowUnbundlePoints = CloneLootScanPoints(_arrowUnbundlePoints)

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
                .CooldownId = $"full-action:{row.Index}",
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
            Else
                state = JsonSerializer.Deserialize(Of PersistedListState)(raw)
                liteState = New PersistedLiteState()
            End If
            If state Is Nothing Then
                Return
            End If

            _periodicScreenshotSettingsLoading = True
            Try
                If chkPeriodicScreenshots IsNot Nothing Then
                    chkPeriodicScreenshots.Checked = (appState IsNot Nothing AndAlso appState.PeriodicScreenshotsEnabled)
                End If
                If nudPeriodicScreenshotMinutes IsNot Nothing Then
                    Dim savedInterval As Decimal = If(appState IsNot Nothing, appState.PeriodicScreenshotIntervalMinutes, 15D)
                    nudPeriodicScreenshotMinutes.Value = Math.Max(nudPeriodicScreenshotMinutes.Minimum, Math.Min(nudPeriodicScreenshotMinutes.Maximum, savedInterval))
                End If
                If txtPeriodicScreenshotDirectory IsNot Nothing Then
                    Dim savedDirectory As String = If(appState IsNot Nothing, appState.PeriodicScreenshotDirectory, "")
                    txtPeriodicScreenshotDirectory.Text = If(String.IsNullOrWhiteSpace(savedDirectory), DefaultPeriodicScreenshotDirectoryPath, savedDirectory.Trim())
                End If
            Finally
                _periodicScreenshotSettingsLoading = False
            End Try

            _updateSettingsLoading = True
            Try
                If txtUpdateRepositoryUrl IsNot Nothing Then
                    Dim savedRepositoryUrl As String = If(appState IsNot Nothing, appState.UpdateRepositoryUrl, DefaultUpdateRepositoryUrl)
                    txtUpdateRepositoryUrl.Text = If(String.IsNullOrWhiteSpace(savedRepositoryUrl), DefaultUpdateRepositoryUrl, savedRepositoryUrl.Trim())
                End If
                If chkUpdateCheckAtStartup IsNot Nothing Then
                    chkUpdateCheckAtStartup.Checked = (appState Is Nothing OrElse appState.UpdateCheckAtStartup)
                End If
                If chkUpdateIncludePrereleases IsNot Nothing Then
                    chkUpdateIncludePrereleases.Checked = (appState IsNot Nothing AndAlso appState.UpdateIncludePrereleases)
                End If
            Finally
                _updateSettingsLoading = False
            End Try
            RefreshUpdateInstallMode()

            Dim savedToggleX As Integer = If(appState IsNot Nothing, appState.InGameBotToggleX, -1)
            _inGameBotToggleX = If(savedToggleX < 0, -1, savedToggleX)
            _inGameBotToggleY = Math.Max(0, If(appState IsNot Nothing, appState.InGameBotToggleY, 10))
            _inGameBotToggleWidth = Math.Max(80, Math.Min(320, If(appState IsNot Nothing, appState.InGameBotToggleWidth, 104)))
            _inGameBotToggleHeight = Math.Max(30, Math.Min(120, If(appState IsNot Nothing, appState.InGameBotToggleHeight, 38)))
            If _inGameBotToggleForm IsNot Nothing AndAlso Not _inGameBotToggleForm.IsDisposed Then
                _inGameBotToggleForm.ApplyLayout(_inGameBotToggleX, _inGameBotToggleY, _inGameBotToggleWidth, _inGameBotToggleHeight)
            End If
            ConfigurePeriodicScreenshotTimer()

            If state.SavedConfig IsNot Nothing Then
                BotConfig.MigrateLegacyVisionLayout(state.SavedConfig)
                ApplySavedConfigToUi(state.SavedConfig)
            End If

            If chkMonsterFilter IsNot Nothing Then
                chkMonsterFilter.Checked = state.MonsterFilterEnabled
            End If
            SelectMonsterFilterMode(state.MonsterFilterMode)
            If chkMonsterConfirmOnce IsNot Nothing Then
                chkMonsterConfirmOnce.Checked = Math.Max(1, state.MonsterFilterConfirmReads) <= 1
            End If
            UpdateMonsterFilterUi()
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
            If chkArrowUnbundleEnabled IsNot Nothing Then
                chkArrowUnbundleEnabled.Checked = state.ArrowUnbundleEnabled
            End If
            If nudArrowUnbundleSeconds IsNot Nothing Then
                nudArrowUnbundleSeconds.Value = Math.Max(nudArrowUnbundleSeconds.Minimum, Math.Min(nudArrowUnbundleSeconds.Maximum, state.ArrowUnbundleSeconds))
            End If
            If chkArrowUnbundleOverlay IsNot Nothing Then
                chkArrowUnbundleOverlay.Checked = state.ArrowUnbundleOverlayEnabled
            End If
            _arrowUnbundlePoints.Clear()
            If state.ArrowUnbundlePoints IsNot Nothing Then
                _arrowUnbundlePoints.AddRange(CloneLootScanPoints(state.ArrowUnbundlePoints))
            End If
            _isPickingLootRejectPoint = False
            _isPickingLootNamePickupPoint = False
            _isPickingArrowUnbundlePoint = False
            UpdateLootRejectPointUi()
            UpdateLootNamePickupPointUi()
            UpdateArrowUnbundleUi()
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
            UpdateLootScannerButtons()
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
            If txtDiscordShotBotToken IsNot Nothing Then
                txtDiscordShotBotToken.Text = If(state.DiscordShotBotToken, "").Trim()
            End If
            If txtDiscordShotChannelId IsNot Nothing Then
                txtDiscordShotChannelId.Text = If(state.DiscordShotChannelId, "").Trim()
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
            If chkAutoRelaunchGame IsNot Nothing Then
                chkAutoRelaunchGame.Checked = state.AutoRelaunchGameEnabled
            End If
            If txtAutoRelaunchExePath IsNot Nothing Then
                txtAutoRelaunchExePath.Text = If(state.AutoRelaunchGameExePath, "").Trim()
            End If
            If nudAutoRelaunchDelaySeconds IsNot Nothing Then
                nudAutoRelaunchDelaySeconds.Value = Math.Max(nudAutoRelaunchDelaySeconds.Minimum, Math.Min(nudAutoRelaunchDelaySeconds.Maximum, state.AutoRelaunchDelaySeconds))
            End If
            If chkAutoRelaunchClickOverlay IsNot Nothing Then
                chkAutoRelaunchClickOverlay.Checked = state.AutoRelaunchClickOverlayEnabled
            End If
            ApplyAutoRelaunchClickSteps(state.AutoRelaunchClicks)
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
                .MonsterFilterMode = GetMonsterFilterMode(),
                .MonsterFilterConfirmReads = GetMonsterFilterConfirmReads(),
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
                .ArrowUnbundleEnabled = (chkArrowUnbundleEnabled IsNot Nothing AndAlso chkArrowUnbundleEnabled.Checked),
                .ArrowUnbundleSeconds = If(nudArrowUnbundleSeconds IsNot Nothing, nudArrowUnbundleSeconds.Value, 60D),
                .ArrowUnbundleOverlayEnabled = (chkArrowUnbundleOverlay IsNot Nothing AndAlso chkArrowUnbundleOverlay.Checked),
                .ArrowUnbundlePoints = CloneLootScanPoints(_arrowUnbundlePoints),
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
                .DiscordShotBotToken = GetDiscordShotBotToken(),
                .DiscordShotChannelId = GetDiscordShotChannelId(),
                .NtfyTopic = If(txtNtfyTopic IsNot Nothing, txtNtfyTopic.Text.Trim(), ""),
                .ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), ""),
                .StatsNtfyTopic = If(txtStatsNtfyTopic IsNot Nothing, txtStatsNtfyTopic.Text.Trim(), ""),
                .StatsNtfyIntervalMinutes = If(nudStatsNtfyIntervalMinutes IsNot Nothing, nudStatsNtfyIntervalMinutes.Value, 30D),
                .AutoRelaunchGameEnabled = (chkAutoRelaunchGame IsNot Nothing AndAlso chkAutoRelaunchGame.Checked),
                .AutoRelaunchGameExePath = If(txtAutoRelaunchExePath IsNot Nothing, txtAutoRelaunchExePath.Text.Trim(), ""),
                .AutoRelaunchDelaySeconds = If(nudAutoRelaunchDelaySeconds IsNot Nothing, nudAutoRelaunchDelaySeconds.Value, 5D),
                .AutoRelaunchClickOverlayEnabled = (chkAutoRelaunchClickOverlay IsNot Nothing AndAlso chkAutoRelaunchClickOverlay.Checked),
                .AutoRelaunchClicks = GetAutoRelaunchClickSteps(),
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
                .HpPointColorEnabled = _liteAutoPotHpColorEnabled,
                .HpPointColorArgb = _liteAutoPotHpColorArgb,
                .MpPointEnabled = (_liteAutoPotMpPointX >= 0 AndAlso _liteAutoPotMpPointY >= 0),
                .MpPointX = _liteAutoPotMpPointX,
                .MpPointY = _liteAutoPotMpPointY,
                .MpPointColorEnabled = _liteAutoPotMpColorEnabled,
                .MpPointColorArgb = _liteAutoPotMpColorArgb,
                .PromptAutoAcceptEnabled = _litePartyAutoAccept,
                .AskForPartyEnabled = _litePartyAskEnabled,
                .AskForPartySeconds = If(nudLitePartyAskSeconds IsNot Nothing, nudLitePartyAskSeconds.Value, 30D),
                .AskForPartyText = GetLitePartyAskCommandText(),
                .Actions = GetPersistedLiteActions()
            }

            Dim appState As New PersistedAppState With {
                .WindowTitle = GetSelectedWindowTitleForFallback(If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full)),
                .PeriodicScreenshotsEnabled = (chkPeriodicScreenshots IsNot Nothing AndAlso chkPeriodicScreenshots.Checked),
                .PeriodicScreenshotIntervalMinutes = If(nudPeriodicScreenshotMinutes IsNot Nothing, nudPeriodicScreenshotMinutes.Value, 15D),
                .PeriodicScreenshotDirectory = GetPeriodicScreenshotDirectory(),
                .InGameBotToggleX = _inGameBotToggleX,
                .InGameBotToggleY = _inGameBotToggleY,
                .InGameBotToggleWidth = _inGameBotToggleWidth,
                .InGameBotToggleHeight = _inGameBotToggleHeight,
                .UpdateRepositoryUrl = GetUpdateRepositoryUrl(),
                .UpdateCheckAtStartup = (chkUpdateCheckAtStartup IsNot Nothing AndAlso chkUpdateCheckAtStartup.Checked),
                .UpdateIncludePrereleases = (chkUpdateIncludePrereleases IsNot Nothing AndAlso chkUpdateIncludePrereleases.Checked),
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
                _liteAutoPotHpColorEnabled = source.HpPointColorEnabled
                _liteAutoPotHpColorArgb = source.HpPointColorArgb
            Else
                _liteAutoPotHpPointX = -1
                _liteAutoPotHpPointY = -1
                _liteAutoPotHpColorEnabled = False
                _liteAutoPotHpColorArgb = 0
            End If
            If source.MpPointEnabled Then
                _liteAutoPotMpPointX = Math.Max(0, source.MpPointX)
                _liteAutoPotMpPointY = Math.Max(0, source.MpPointY)
                _liteAutoPotMpColorEnabled = source.MpPointColorEnabled
                _liteAutoPotMpColorArgb = source.MpPointColorArgb
            Else
                _liteAutoPotMpPointX = -1
                _liteAutoPotMpPointY = -1
                _liteAutoPotMpColorEnabled = False
                _liteAutoPotMpColorArgb = 0
            End If
            Dim hpPointAdjusted As Boolean = NormalizeLiteAutoPotPoint(GetLiteAutoPotBarRegion(LitePointCaptureKind.Hp), _liteAutoPotHpPointX, _liteAutoPotHpPointY)
            Dim mpPointAdjusted As Boolean = NormalizeLiteAutoPotPoint(GetLiteAutoPotBarRegion(LitePointCaptureKind.Mp), _liteAutoPotMpPointX, _liteAutoPotMpPointY)
            If hpPointAdjusted Then
                _liteAutoPotHpColorEnabled = False
                _liteAutoPotHpColorArgb = 0
                AppendLog($"Lite AutoPots: adjusted saved HP point to {_liteAutoPotHpPointX}, {_liteAutoPotHpPointY} for the current HP bar.")
            End If
            If mpPointAdjusted Then
                _liteAutoPotMpColorEnabled = False
                _liteAutoPotMpColorArgb = 0
                AppendLog($"Lite AutoPots: adjusted saved Mana point to {_liteAutoPotMpPointX}, {_liteAutoPotMpPointY} for the current Mana bar.")
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

        ApplyBarColorConfigToUi(cfg)
        SetNumericControlValue(nudLoopMs, cfg.LoopMs)
        SetNumericControlValue(nudRetargetMs, cfg.RetargetMs)
        SetNumericControlValue(nudForcedRetargetMs, If(cfg.ForcedRetargetMs > 0, cfg.ForcedRetargetMs, cfg.RetargetMs))
        SetNumericControlValue(nudStuckTargetMs, cfg.StuckTargetMs)
        SetNumericControlValue(nudStuckNoProgressRetargetMs, If(cfg.StuckTargetNoProgressRetargetMs > 0, cfg.StuckTargetNoProgressRetargetMs, 6000))
        SetNumericControlValue(nudMobHpThreshold, CDec(cfg.MobHpPresenceThreshold))
        SelectMonsterFilterMode(cfg.MonsterFilterMode)
        If chkMonsterConfirmOnce IsNot Nothing Then
            chkMonsterConfirmOnce.Checked = Math.Max(1, cfg.MonsterFilterConfirmReads) <= 1
        End If
        UpdateMonsterFilterUi()
        If chkHighMaxHpSpecial IsNot Nothing Then
            chkHighMaxHpSpecial.Checked = cfg.HighMaxHpSpecialEnabled
        End If
        SetNumericControlValue(nudHighMaxHpThreshold, CDec(Math.Max(100, cfg.HighMaxHpThreshold)))
        If chkAvoidHighMaxHpTargets IsNot Nothing Then
            chkAvoidHighMaxHpTargets.Checked = cfg.AvoidHighMaxHpEnabled
        End If
        SetNumericControlValue(nudAvoidHighMaxHpThreshold, CDec(Math.Max(100, cfg.AvoidHighMaxHpThreshold)))
        If chkEvadeDadati IsNot Nothing Then
            chkEvadeDadati.Checked = cfg.EvadeDadatiEnabled
        End If

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
        UpdateLootScannerButtons()
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
        If txtNtfyTopic IsNot Nothing Then
            Dim globalTopic As String = If(cfg.NtfyTopic, "").Trim()
            txtNtfyTopic.Text = If(globalTopic = "", DefaultNtfyTopicName, globalTopic)
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
        If chkNavigationReturnToStart IsNot Nothing Then
            chkNavigationReturnToStart.Checked = cfg.NavigationReturnToStartEnabled
        End If
        If chkHoldPlaceEnabled IsNot Nothing Then
            chkHoldPlaceEnabled.Checked = cfg.HoldPlaceEnabled
        End If
        _holdPlaceAnchorSet = cfg.HoldPlaceAnchorSet OrElse (cfg.HoldPlaceEnabled AndAlso cfg.HoldPlaceTargetX >= 0 AndAlso cfg.HoldPlaceTargetY >= 0)
        SetNumericControlValue(nudHoldPlaceTargetX, CDec(Math.Max(0, Math.Min(999, If(cfg.HoldPlaceTargetX >= 0, cfg.HoldPlaceTargetX, 0)))))
        SetNumericControlValue(nudHoldPlaceTargetY, CDec(Math.Max(0, Math.Min(999, If(cfg.HoldPlaceTargetY >= 0, cfg.HoldPlaceTargetY, 0)))))
        SetNumericControlValue(nudHoldPlaceRadius, CDec(Math.Max(0, Math.Min(25, cfg.HoldPlaceRadius))))
        SetNumericControlValue(nudHoldPlaceMoveBurstMs, CDec(Math.Max(20, Math.Min(800, cfg.HoldPlaceMoveBurstMs))))
        SetNumericControlValue(nudHoldPlaceCorrectionMs, CDec(Math.Max(150, Math.Min(5000, cfg.HoldPlaceCorrectionIntervalMs))))
        If chkHoldPlacePostFightReturn IsNot Nothing Then
            chkHoldPlacePostFightReturn.Checked = cfg.HoldPlacePostFightReturnEnabled
        End If
        If chkHoldPlaceCombatSafe IsNot Nothing Then
            chkHoldPlaceCombatSafe.Checked = cfg.HoldPlaceCombatSafeEnabled
        End If
        SetNumericControlValue(nudHoldPlaceEmergencyLeash, CDec(Math.Max(5, Math.Min(200, If(cfg.HoldPlaceEmergencyLeashDistance > 0, cfg.HoldPlaceEmergencyLeashDistance, 60)))))
        If chkHoldPlaceDirectionLearning IsNot Nothing Then
            chkHoldPlaceDirectionLearning.Checked = cfg.HoldPlaceDirectionLearningEnabled
        End If
        Dim savedHoldPlaceMode As String = NormalizeHoldPlaceRestrictivenessMode(cfg.HoldPlaceRestrictivenessMode)
        If Not savedHoldPlaceMode.Equals("custom", StringComparison.OrdinalIgnoreCase) AndAlso Not HoldPlaceControlsMatchPreset(savedHoldPlaceMode) Then
            savedHoldPlaceMode = "custom"
        End If
        SelectHoldPlaceRestrictivenessMode(savedHoldPlaceMode, applyPreset:=False)
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
        If chkAdaptivePerformance IsNot Nothing Then
            chkAdaptivePerformance.Checked = cfg.AdaptivePerformanceEnabled
        End If
        If chkPixelChangeGate IsNot Nothing Then
            chkPixelChangeGate.Checked = cfg.PixelChangeGateEnabled
        End If
        SetNumericControlValue(nudAdaptiveSlowMinMs, CDec(Math.Max(40, cfg.AdaptiveSlowLoopMinMs)))
        SetNumericControlValue(nudAdaptiveSlowMultiplier, CDec(Math.Max(1.0R, cfg.AdaptiveSlowLoopMultiplier)))
        SetNumericControlValue(nudAdaptiveRecoveryMultiplier, CDec(Math.Max(1.0R, cfg.AdaptiveRecoveryLoopMultiplier)))
        SetNumericControlValue(nudAdaptiveSlowConfirm, CDec(Math.Max(1, cfg.AdaptiveSlowConfirmCount)))
        SetNumericControlValue(nudAdaptiveRecoveryConfirm, CDec(Math.Max(1, cfg.AdaptiveRecoveryConfirmCount)))
        SelectCaptureBackend(cfg.CaptureBackendPreference)
        SetNumericControlValue(nudFullFrameScanMs, CDec(Math.Max(100, cfg.FullFrameRefreshIntervalMs)))
        SetNumericControlValue(nudLootScannerSeconds, CDec(Math.Max(1.0R, cfg.LootScannerIntervalMs / 1000.0R)))
        SetNumericControlValue(nudMapScanMs, CDec(Math.Max(250, cfg.MapCoordinateScanIntervalMs)))
        SetNumericControlValue(nudPartyScanMs, CDec(Math.Max(250, cfg.PartyListScanIntervalMs)))
        SetNumericControlValue(nudMobNameScanMs, CDec(Math.Max(120, cfg.MobNameScanIntervalMs)))
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
        If chkArrowUnbundleEnabled IsNot Nothing Then
            chkArrowUnbundleEnabled.Checked = cfg.ArrowUnbundleEnabled
        End If
        SetNumericControlValue(nudArrowUnbundleSeconds, CDec(Math.Max(1.0R, cfg.ArrowUnbundleIntervalMs / 1000.0R)))
        _arrowUnbundlePoints.Clear()
        _arrowUnbundlePoints.AddRange(CloneLootScanPoints(If(cfg.ArrowUnbundlePoints, New List(Of LootScanPoint)())))
        _isPickingArrowUnbundlePoint = False
        UpdateArrowUnbundleUi()

        UpsertRegionRow("hp_bar", cfg.HpBar)
        UpsertRegionRow("mp_bar", cfg.MpBar)
        UpsertRegionRow("mob_name_rect", cfg.MobNameRect)
        UpsertRegionRow("mob_hp_rect", cfg.MobHpRect)
        UpsertRegionRow("mob_life_rect", If(cfg.MobLifeRect, cfg.MobHpRect))
        UpsertRegionRow("unreachable_text_rect", cfg.UnreachableTextRect)
        UpsertRegionRow("prana_exp_rect", cfg.PranaExpRect)
        UpsertRegionRow("rupiahs_rect", cfg.RupiahsRect)
        UpsertRegionRow("party_invite_scan_rect", cfg.PartyInviteScanRect)
        UpsertRegionRow("party_invite_ok_rect", cfg.PartyInviteOkRect)
        UpsertRegionRow("party_list_rect", cfg.PartyListRect)
        UpsertRegionRow("disconnect_message_rect", If(cfg.DisconnectMessageRect, BotConfig.DefaultDisconnectMessageRect()))
        UpsertRegionRow("disconnect_ok_rect", If(cfg.DisconnectOkRect, BotConfig.DefaultDisconnectOkRect()))
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
            Case "special"
                Return "buff"
            Case "attack", "heal", "max_health", "mana", "buff", "high_max_hp", "repair", "stop"
                Return role
            Case Else
                Return "attack"
        End Select
    End Function

    Private Sub AppendLog(message As String)
        Dim text As String = If(message, "")
        If text.Length > MaxLogLineChars Then
            text = text.Substring(0, MaxLogLineChars) & " ... [truncated]"
        End If

        If Not IsLogCategoryEnabled(text) Then
            Return
        End If

        Dim stamped As String = $"[{DateTime.Now:HH:mm:ss}] {text}"
        SyncLock _logQueueSync
            If _logQueue.Count >= MaxPendingLogLines Then
                _logQueue.Dequeue()
                If _droppedLogLineCount < Integer.MaxValue Then
                    _droppedLogLineCount += 1
                End If
                If _totalDroppedLogLineCount < Long.MaxValue Then
                    _totalDroppedLogLineCount += 1
                End If
            End If
            _logQueue.Enqueue(stamped)
        End SyncLock
    End Sub

    Private Sub AppendLogSafe(message As String)
        AppendLog(message)
    End Sub

    Private Sub LogFlushTimerTick(sender As Object, e As EventArgs)
        FlushPendingLogLines()
    End Sub

    Private Sub FlushPendingLogLines()
        If rtbLog Is Nothing OrElse rtbLog.IsDisposed Then
            Return
        End If

        Dim batch As New List(Of String)()
        Dim droppedCount As Integer = 0
        SyncLock _logQueueSync
            droppedCount = _droppedLogLineCount
            _droppedLogLineCount = 0

            While _logQueue.Count > 0 AndAlso batch.Count < MaxLogFlushLines
                batch.Add(_logQueue.Dequeue())
            End While
        End SyncLock

        If droppedCount > 0 Then
            batch.Insert(0, $"[{DateTime.Now:HH:mm:ss}] UI log queue dropped {droppedCount} older line(s) to keep the control responsive.")
        End If

        If batch.Count = 0 Then
            Return
        End If

        rtbLog.SuspendLayout()
        Try
            rtbLog.AppendText(String.Join(Environment.NewLine, batch) & Environment.NewLine)
            TrimRealtimeLogIfNeeded(False)
            rtbLog.SelectionStart = rtbLog.TextLength
            rtbLog.ScrollToCaret()
            _lastLogFlushBatchCount = batch.Count
            _lastLogFlushAt = DateTime.Now
        Finally
            rtbLog.ResumeLayout()
        End Try
    End Sub

    Private Sub TrimRealtimeLogIfNeeded(force As Boolean)
        If rtbLog Is Nothing OrElse rtbLog.IsDisposed Then
            Return
        End If

        If Not force AndAlso rtbLog.TextLength <= MaxRealtimeLogChars Then
            Return
        End If

        Dim nowUtc As DateTime = DateTime.UtcNow
        If Not force AndAlso (nowUtc - _lastLogTrimUtc).TotalSeconds < LogTrimIntervalSeconds Then
            Return
        End If
        _lastLogTrimUtc = nowUtc

        If rtbLog.TextLength <= TargetRealtimeLogChars Then
            Return
        End If

        Dim text As String = rtbLog.Text
        Dim keepStart As Integer = Math.Max(0, text.Length - TargetRealtimeLogChars)
        Dim newlineIndex As Integer = text.IndexOf(Environment.NewLine, keepStart, StringComparison.Ordinal)
        If newlineIndex >= 0 AndAlso newlineIndex + Environment.NewLine.Length < text.Length Then
            keepStart = newlineIndex + Environment.NewLine.Length
        End If

        rtbLog.Text = text.Substring(keepStart)
    End Sub

    Private Sub ClearRealtimeLog()
        SyncLock _logQueueSync
            _logQueue.Clear()
            _droppedLogLineCount = 0
        End SyncLock
        If rtbLog IsNot Nothing AndAlso Not rtbLog.IsDisposed Then
            rtbLog.Clear()
        End If
    End Sub

    Private Function IsLogCategoryEnabled(message As String) As Boolean
        Dim category As String = ClassifyLogMessage(message)
        Select Case category
            Case "combat"
                Return _logFilterCombatEnabled
            Case "loot"
                Return _logFilterLootEnabled
            Case "ocr"
                Return _logFilterOcrVisionEnabled
            Case "navigation"
                Return _logFilterNavigationEnabled
            Case "warning"
                Return _logFilterWarningsEnabled
            Case Else
                Return _logFilterMiscEnabled
        End Select
    End Function

    Private Shared Function ClassifyLogMessage(message As String) As String
        Dim text As String = If(message, "").ToLowerInvariant()
        If text.Contains("warning") OrElse text.Contains("failed") OrElse text.Contains("error") OrElse text.Contains("glitch") OrElse text.Contains("not responding") Then
            Return "warning"
        End If
        If text.Contains("loot") OrElse text.Contains("item notification") OrElse text.Contains("right-alt scan") Then
            Return "loot"
        End If
        If text.Contains("ocr") OrElse text.Contains("vision") OrElse text.Contains("capture") OrElse text.Contains("black frame") OrElse text.Contains("screen text") Then
            Return "ocr"
        End If
        If text.Contains("route") OrElse text.Contains("navigation") OrElse text.Contains("travel") OrElse text.Contains("waypoint") OrElse text.Contains("coordinate") Then
            Return "navigation"
        End If
        If text.Contains("key action") OrElse text.Contains("attack") OrElse text.Contains("retarget") OrElse text.Contains("hp") OrElse text.Contains("mp") Then
            Return "combat"
        End If
        Return "misc"
    End Function

    Private Shared Function IsKeyActionLogLine(line As String) As Boolean
        Dim trimmedLine As String = If(line, "").Trim()
        Return trimmedLine.StartsWith("Key action:", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function BuildRateLimitedLogMessage(message As String, ByRef lastLoggedUtc As DateTime, ByRef suppressedCount As Integer, minIntervalMs As Integer, suppressedLabel As String) As String
        Dim nowUtc As DateTime = DateTime.UtcNow
        SyncLock _logThrottleSync
            If lastLoggedUtc = DateTime.MinValue OrElse (nowUtc - lastLoggedUtc).TotalMilliseconds >= minIntervalMs Then
                Dim skipped As Integer = suppressedCount
                suppressedCount = 0
                lastLoggedUtc = nowUtc
                If skipped > 0 Then
                    Return $"{message} (+{skipped} {suppressedLabel} log(s) coalesced)"
                End If
                Return message
            End If

            If suppressedCount < Integer.MaxValue Then
                suppressedCount += 1
            End If
            Return Nothing
        End SyncLock
    End Function

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

        Dim nowUtc As DateTime = DateTime.UtcNow
        SyncLock _keyActionEventsSync
            _keyActionEvents.Add(New KeyActionEvent With {
                .TimestampUtc = nowUtc,
                .KeyName = keyName,
                .ActionText = actionText
            })
            PruneKeyActionEventsLocked(nowUtc)
        End SyncLock
    End Sub

    Private Sub RecordLootHistoryFromEngineLog(edition As BotEdition, line As String)
        Dim text As String = If(line, "").Trim()
        If text = "" Then
            Return
        End If

        Dim actionText As String = ""
        Dim itemName As String = ""
        Dim detailText As String = text

        Dim alarmMatch As Match = Regex.Match(text, "LOOT ALARM:\s*Found\s+(.+?)\s*\(fuzzy\s+(\d+)%\)", RegexOptions.IgnoreCase)
        If alarmMatch.Success Then
            itemName = alarmMatch.Groups(1).Value.Trim()
            actionText = "Found"
            detailText = $"fuzzy {alarmMatch.Groups(2).Value}%"
        Else
            Dim autoPickMatch As Match = Regex.Match(text, "Loot auto-pick clicked matched label '([^']+)'", RegexOptions.IgnoreCase)
            If autoPickMatch.Success Then
                itemName = autoPickMatch.Groups(1).Value.Trim()
                actionText = "Auto-pick clicked"
            Else
                Dim autoPickSkippedMatch As Match = Regex.Match(text, "Loot auto-pick skipped for ([^:]+):\s*(.+)", RegexOptions.IgnoreCase)
                If autoPickSkippedMatch.Success Then
                    itemName = autoPickSkippedMatch.Groups(1).Value.Trim()
                    actionText = "Auto-pick skipped"
                    detailText = autoPickSkippedMatch.Groups(2).Value.Trim()
                Else
                    Dim acceptedMatch As Match = Regex.Match(text, "loot accepted:\s*([^)]+)", RegexOptions.IgnoreCase)
                    If acceptedMatch.Success Then
                        itemName = acceptedMatch.Groups(1).Value.Trim()
                        actionText = "Accepted"
                    Else
                        Dim rejectedMatch As Match = Regex.Match(text, "loot rejected:\s*([^)]+)", RegexOptions.IgnoreCase)
                        If rejectedMatch.Success Then
                            itemName = rejectedMatch.Groups(1).Value.Trim()
                            actionText = "Rejected"
                        ElseIf text.IndexOf("loot", StringComparison.OrdinalIgnoreCase) >= 0 Then
                            itemName = "n/a"
                            actionText = "Loot event"
                        Else
                            Return
                        End If
                    End If
                End If
            End If
        End If

        SyncLock _lootHistoryEventsSync
            _lootHistoryEvents.Add(New LootHistoryEvent With {
                .TimestampLocal = DateTime.Now,
                .Edition = edition,
                .ItemName = If(String.IsNullOrWhiteSpace(itemName), "unknown", itemName),
                .ActionText = actionText,
                .DetailText = detailText
            })
            If _lootHistoryEvents.Count > MaxLootHistoryEvents Then
                _lootHistoryEvents.RemoveRange(0, _lootHistoryEvents.Count - MaxLootHistoryEvents)
            End If
            _lootHistoryVersion += 1
        End SyncLock
    End Sub

    Private Sub RefreshLootHistoryGrid()
        If dgvLootHistory Is Nothing OrElse dgvLootHistory.IsDisposed Then
            Return
        End If

        Dim eventsSnapshot As List(Of LootHistoryEvent)
        SyncLock _lootHistoryEventsSync
            If _lootHistoryVersion = _lastLootHistoryRenderedVersion Then
                Return
            End If
            eventsSnapshot = _lootHistoryEvents.Select(Function(entry) New LootHistoryEvent With {
                .TimestampLocal = entry.TimestampLocal,
                .Edition = entry.Edition,
                .ItemName = entry.ItemName,
                .ActionText = entry.ActionText,
                .DetailText = entry.DetailText
            }).ToList()
            _lastLootHistoryRenderedVersion = _lootHistoryVersion
        End SyncLock

        dgvLootHistory.SuspendLayout()
        Try
            dgvLootHistory.Rows.Clear()
            For Each entry As LootHistoryEvent In eventsSnapshot.OrderByDescending(Function(item) item.TimestampLocal).Take(200)
                dgvLootHistory.Rows.Add(entry.TimestampLocal.ToString("HH:mm:ss"), entry.Edition.ToString(), entry.ItemName, entry.ActionText, entry.DetailText)
            Next
        Finally
            dgvLootHistory.ResumeLayout()
        End Try
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
        Dim actionEvents As List(Of KeyActionEvent)
        SyncLock _keyActionEventsSync
            PruneKeyActionEventsLocked(nowUtc)
            actionEvents = _keyActionEvents.Select(Function(entry) New KeyActionEvent With {
                .TimestampUtc = entry.TimestampUtc,
                .KeyName = entry.KeyName,
                .ActionText = entry.ActionText
            }).ToList()
        End SyncLock
        Dim cutoff10 As DateTime = nowUtc.AddMinutes(-10)
        Dim cutoff30 As DateTime = nowUtc.AddMinutes(-30)
        Dim cutoff60 As DateTime = nowUtc.AddHours(-1)

        Dim summaries As New Dictionary(Of String, KeyActionSummaryRow)(StringComparer.OrdinalIgnoreCase)
        For Each entry As KeyActionEvent In actionEvents
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

        Dim status As BotStatus = GetStatusForEdition(_edition)
        Dim repairRequired As Integer = Math.Max(1, status.RepairConfirmRequiredCount)
        Dim repairWindow As Integer = Math.Max(1, status.RepairConfirmWindowMinutes)
        Dim repairText As String = $"Repair OCR: {Math.Max(0, status.RepairConfirmCount)}/{repairRequired} in {repairWindow}m | repair triggers: {Math.Max(0, status.RepairTriggerCount)}"
        Dim runtimeText As String = FormatBotRuntimeSummary()
        If ordered.Count = 0 Then
            lblKeySummaryInfo.Text = $"No key presses tracked in the last 60 minutes. | {repairText}{Environment.NewLine}{runtimeText}"
        Else
            Dim capText As String = If(actionEvents.Count >= MaxKeyActionEvents, " | capped", "")
            lblKeySummaryInfo.Text = $"Tracked keys: {ordered.Count} | Total presses (60m): {actionEvents.Count}{capText} | {repairText} | Updated: {DateTime.Now:HH:mm:ss}{Environment.NewLine}{runtimeText}"
        End If
    End Sub

    Private Function FormatBotRuntimeSummary() As String
        Dim runningEdition As BotEdition? = GetRunningEdition()
        If Not runningEdition.HasValue Then
            Return "Bot running time: stopped"
        End If

        Dim status As BotStatus = GetStatusForEdition(runningEdition.Value)
        If status Is Nothing OrElse Not status.Running OrElse status.RunStartedAtUtc = DateTime.MinValue Then
            Return $"Bot running time ({runningEdition.Value}): starting..."
        End If

        Dim elapsed As TimeSpan = DateTime.UtcNow - status.RunStartedAtUtc
        If elapsed < TimeSpan.Zero Then
            elapsed = TimeSpan.Zero
        End If

        Return $"Bot running time ({runningEdition.Value}): {FormatElapsedRuntime(elapsed)}"
    End Function

    Private Shared Function FormatElapsedRuntime(elapsed As TimeSpan) As String
        Dim totalHours As Integer = CInt(Math.Floor(elapsed.TotalHours))
        If totalHours >= 24 Then
            Return $"{elapsed.Days}d {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
        End If
        Return $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
    End Function

    Private Sub PruneKeyActionEvents(nowUtc As DateTime)
        SyncLock _keyActionEventsSync
            PruneKeyActionEventsLocked(nowUtc)
        End SyncLock
    End Sub

    Private Sub PruneKeyActionEventsLocked(nowUtc As DateTime)
        Dim cutoff As DateTime = nowUtc.AddHours(-1)
        _keyActionEvents.RemoveAll(Function(x As KeyActionEvent) x.TimestampUtc < cutoff)
        If _keyActionEvents.Count > MaxKeyActionEvents Then
            _keyActionEvents.RemoveRange(0, _keyActionEvents.Count - MaxKeyActionEvents)
        End If
    End Sub

    Private Sub UpdateAttackButtonAppearance(_ignored As Boolean)
        Dim fullRunning As Boolean = _fullEngine.IsRunning()
        Dim liteRunning As Boolean = _liteEngine.IsRunning()
        Dim runningEdition As BotEdition? = GetRunningEdition()
        Dim selectedEdition As BotEdition = If(IsLiteModeActive(), BotEdition.Lite, BotEdition.Full)

        If btnAttack IsNot Nothing Then
            If fullRunning Then
                btnAttack.Text = "RUNNING"
                btnAttack.BackColor = BotRunningColor
                btnAttack.ForeColor = Color.White
            Else
                btnAttack.Text = "STOPPED"
                btnAttack.BackColor = StatusStoppedOrDeadColor
                btnAttack.ForeColor = Color.White
            End If
        End If

        If btnLiteAttack IsNot Nothing Then
            btnLiteAttack.Text = If(liteRunning, "RUNNING", If(fullRunning, "Start Lite", "Start"))
            btnLiteAttack.BackColor = If(liteRunning, BotRunningColor, StatusStoppedOrDeadColor)
            btnLiteAttack.ForeColor = Color.White
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
            lblRunState.Text = If(fullRunning, "FULL BOT RUNNING", "FULL BOT STOPPED")
            lblRunState.BackColor = If(fullRunning, BotRunningColor, StatusStoppedOrDeadColor)
            lblRunState.ForeColor = Color.White
        End If
        If lblFullEdition IsNot Nothing Then
            lblFullEdition.Text = If(liteRunning, "FULL VERSION - LITE BOT RUNNING", "FULL VERSION - for more powerful computers")
        End If

        If lblLiteRunState IsNot Nothing Then
            lblLiteRunState.Text = If(liteRunning, "LITE BOT RUNNING", "LITE BOT STOPPED")
            lblLiteRunState.BackColor = If(liteRunning, BotRunningColor, StatusStoppedOrDeadColor)
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
        UpdateTaskbarStatusIndicator()
    End Sub

    Private Function GetBotRunStateLabel(status As BotStatus, running As Boolean) As String
        If Not running Then
            Return "PAUSED"
        End If
        If status Is Nothing Then
            Return "RUNNING"
        End If
        If Not status.WindowFound Then
            Return "WAIT WINDOW"
        End If
        If Not String.IsNullOrWhiteSpace(status.ErrorMessage) Then
            Dim err As String = status.ErrorMessage.ToLowerInvariant()
            If err.Contains("black") OrElse err.Contains("capture") OrElse err.Contains("glitch") OrElse err.Contains("restarted") Then
                Return "RECOVERING"
            End If
            Return "WARNING"
        End If
        If status.NavigationReturningToStart Then
            Return "RETURNING"
        End If
        If status.NavigationTravelActive Then
            Return "TRAVELING"
        End If
        If Not String.IsNullOrWhiteSpace(status.NotAttackingReason) Then
            Dim reason As String = status.NotAttackingReason.ToLowerInvariant()
            If reason.Contains("window") Then
                Return "WAIT WINDOW"
            End If
            If reason.Contains("black") OrElse reason.Contains("capture") OrElse reason.Contains("recovery") Then
                Return "RECOVERING"
            End If
            If reason.Contains("cooldown") OrElse reason.Contains("waiting") Then
                Return "WAITING"
            End If
        End If
        Return "RUNNING"
    End Function

    Private Shared Function GetBotRunStateColor(stateLabel As String) As Color
        Select Case If(stateLabel, "").Trim().ToUpperInvariant()
            Case "RUNNING"
                Return BotRunningColor
            Case "TRAVELING", "RETURNING"
                Return BotRunningColor
            Case "WAIT WINDOW", "WAITING"
                Return BotRunningColor
            Case "RECOVERING"
                Return BotRunningColor
            Case "WARNING"
                Return BotRunningColor
            Case Else
                Return BotRunningColor
        End Select
    End Function

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
            _deadHpFirstSeenUtc = DateTime.MinValue
            If _hpZeroPending Then
                CancelHpZeroPendingCountdown(False)
            End If
            Return
        End If

        ' Only count usable (non-black / non-failed) frames toward death confirmation.
        Dim errorText As String = If(status.ErrorMessage, "")
        Dim unusableVisionFrame As Boolean =
            errorText.IndexOf("capture failed", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
            errorText.IndexOf("unable to capture", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
            errorText.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
            errorText.IndexOf("glitch", StringComparison.OrdinalIgnoreCase) >= 0
        Dim isUsableFrame As Boolean =
            status.Running AndAlso
            status.WindowFound AndAlso
            Not unusableVisionFrame

        Dim isDeadHp As Boolean =
            isUsableFrame AndAlso
            status.HpPercent <= DeadZeroThreshold

        If isDeadHp Then
            If _deadHpFirstSeenUtc = DateTime.MinValue Then
                _deadHpFirstSeenUtc = DateTime.UtcNow
                _deadHpConfirmCount = 0
                AppendLog($"HP reached 0. Waiting {CriticalAlertConfirmMs \ 1000} seconds or {CriticalAlertConfirmFrames} consecutive valid status samples before alarm/notification.")
            End If
            _deadHpConfirmCount += 1
        Else
            If _deadHpFirstSeenUtc <> DateTime.MinValue AndAlso Not _deathNotificationLatched Then
                AppendLog("HP=0 alert canceled before confirmation.")
            End If
            _deadHpConfirmCount = 0
            _deadHpFirstSeenUtc = DateTime.MinValue
        End If

        Dim recovered As Boolean = status.HpPercent >= DeadRecoverThreshold
        If recovered Then
            _deathNotificationLatched = False
        End If

        If IsCriticalAlertConfirmed(_deadHpFirstSeenUtc, _deadHpConfirmCount) Then
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
        _deadHpConfirmCount = 0
        _deadHpFirstSeenUtc = DateTime.MinValue

        If logCancellation Then
            AppendLog("HP recovered before death-alert confirmation. Alarm canceled.")
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
        _deadHpFirstSeenUtc = DateTime.MinValue
        _deathNotificationLatched = False
        _lastHpZeroNotification = DateTime.MinValue
        _lastWindowMissingNotification = DateTime.MinValue
        _windowMissingConfirmCount = 0
        _windowMissingFirstSeenUtc = DateTime.MinValue
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
        _deadHpFirstSeenUtc = DateTime.MinValue
        _deathNotificationLatched = False
        _lastHpZeroNotification = DateTime.MinValue
        _lastWindowMissingNotification = DateTime.MinValue
        _windowMissingConfirmCount = 0
        _windowMissingFirstSeenUtc = DateTime.MinValue
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
                Dim sent As Boolean = Await SendPhoneNotificationAsync("KathanaBot HP Alert", $"HP stayed at zero for {CriticalAlertConfirmMs \ 1000} seconds or {CriticalAlertConfirmFrames} consecutive valid status samples. Character may be dead.", DeathNotificationRetryCount)
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
               $"Game capture failed for {CriticalAlertConfirmMs \ 1000} seconds or {CriticalAlertConfirmFrames} consecutive status samples. The game may be hidden, black-screened, minimized, or the screen is unavailable.",
               $"Game window was not found for {CriticalAlertConfirmMs \ 1000} seconds or {CriticalAlertConfirmFrames} consecutive status samples. The game may have crashed or been closed.")
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

    Private Function GetDiscordShotBotToken() As String
        Return If(txtDiscordShotBotToken IsNot Nothing, txtDiscordShotBotToken.Text, "").Trim()
    End Function

    Private Function GetDiscordShotChannelId() As String
        Return If(txtDiscordShotChannelId IsNot Nothing, txtDiscordShotChannelId.Text, "").Trim()
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

        Dim tint As Color = If(active, BotRunningColor, StatusStoppedOrDeadColor)
        pnlWindowFrame.BackColor = tint
        If pnlHealthBanner IsNot Nothing Then
            pnlHealthBanner.BackColor = tint
        End If
    End Sub

    Private Shared Function NormalizePercent(value As Double, fallback As Double) As Double
        Dim safeValue As Double = If(Double.IsNaN(value) OrElse Double.IsInfinity(value), fallback, value)
        If safeValue < 0.0 Then
            Return 0.0
        End If
        If safeValue > 100.0 Then
            Return 100.0
        End If
        Return safeValue
    End Function

    Private Shared Function HealthPercentColor(percent As Double) As Color
        Dim bounded As Double = NormalizePercent(percent, 0.0)
        If bounded <= DeadZeroThreshold Then
            Return StatusStoppedOrDeadColor
        End If

        If bounded < 50.0 Then
            Return BlendColor(Color.FromArgb(235, 0, 0), Color.FromArgb(255, 215, 0), bounded / 50.0)
        End If

        Return BlendColor(Color.FromArgb(255, 215, 0), StatusAliveColor, (bounded - 50.0) / 50.0)
    End Function

    Private Shared Function BlendColor(low As Color, high As Color, amount As Double) As Color
        Dim t As Double = NormalizePercent(amount * 100.0, 0.0) / 100.0
        Dim r As Integer = BlendChannel(CInt(low.R), CInt(high.R), t)
        Dim g As Integer = BlendChannel(CInt(low.G), CInt(high.G), t)
        Dim b As Integer = BlendChannel(CInt(low.B), CInt(high.B), t)
        Return Color.FromArgb(r, g, b)
    End Function

    Private Sub UpdateTaskbarStatusIndicator()
        Dim runningEdition As BotEdition? = GetRunningEdition()
        Dim active As Boolean = runningEdition.HasValue
        Dim status As BotStatus = If(active, GetStatusForEdition(runningEdition.Value), Nothing)
        Dim hpPercent As Double = If(status Is Nothing, 0.0, NormalizePercent(status.HpPercent, 0.0))

        If active AndAlso status IsNot Nothing Then
            ApplyHealthUiTint(hpPercent, status.Running)
        Else
            ApplyHealthUiTint(0.0, False)
        End If

        SetTaskbarProgressSolid(If(active, TaskbarProgressState.Normal, TaskbarProgressState.NoProgress))
    End Sub

    Private Function TryGetTaskbarList() As ITaskbarList3
        If _taskbarUnavailable Then
            Return Nothing
        End If
        If _taskbarList IsNot Nothing Then
            Return _taskbarList
        End If

        Try
            _taskbarList = CType(New TaskbarList(), ITaskbarList3)
            _taskbarList.HrInit()
            Return _taskbarList
        Catch
            _taskbarUnavailable = True
            _taskbarList = Nothing
            Return Nothing
        End Try
    End Function

    Private Sub SetTaskbarProgressSolid(state As TaskbarProgressState)
        Dim taskbar As ITaskbarList3 = TryGetTaskbarList()
        If taskbar Is Nothing OrElse IsDisposed OrElse Not IsHandleCreated Then
            Return
        End If

        Try
            taskbar.SetProgressState(Handle, state)
            If state <> TaskbarProgressState.NoProgress Then
                taskbar.SetProgressValue(Handle, 100UL, 100UL)
            End If
        Catch
            _taskbarUnavailable = True
            _taskbarList = Nothing
        End Try
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
        If _updateCancellation IsNot Nothing Then
            _updateCancellation.Cancel()
            _updateCancellation.Dispose()
            _updateCancellation = Nothing
        End If
        _uiTimer.Stop()
        _enterToggleTimer.Stop()
        _logFlushTimer.Stop()
        _rollingScreenshotTimer.Stop()
        _periodicScreenshotTimer.Stop()
        _discordShotTimer.Stop()
        FlushPendingLogLines()
        SavePersistedListState(False)
        StopHpZeroAlarm()
        If _overlayForm IsNot Nothing AndAlso Not _overlayForm.IsDisposed Then
            _overlayForm.Close()
        End If
        If _autoRelaunchClickOverlayForm IsNot Nothing AndAlso Not _autoRelaunchClickOverlayForm.IsDisposed Then
            _autoRelaunchClickOverlayForm.Close()
        End If
        If _arrowUnbundleOverlayForm IsNot Nothing AndAlso Not _arrowUnbundleOverlayForm.IsDisposed Then
            _arrowUnbundleOverlayForm.Close()
        End If
        If _inGameBotToggleForm IsNot Nothing AndAlso Not _inGameBotToggleForm.IsDisposed Then
            RemoveHandler _inGameBotToggleForm.ToggleRequested, AddressOf InGameBotToggleRequested
            RemoveHandler _inGameBotToggleForm.OverlayLayoutChanged, AddressOf InGameBotToggleLayoutChanged
            _inGameBotToggleForm.Close()
        End If
        _fullEngine.Stop()
        _liteEngine.Stop()
        MyBase.OnFormClosing(e)
    End Sub
End Class
