Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports DrawingPoint = System.Drawing.Point

Public Class RectRegion
    Public Property X As Integer
    Public Property Y As Integer
    Public Property W As Integer
    Public Property H As Integer

    Public Sub New()
    End Sub

    Public Sub New(x As Integer, y As Integer, w As Integer, h As Integer)
        Me.X = x
        Me.Y = y
        Me.W = w
        Me.H = h
    End Sub

    Public Function Clamp(maxWidth As Integer, maxHeight As Integer) As Rectangle
        Dim cx As Integer = Math.Max(0, Math.Min(maxWidth - 1, X))
        Dim cy As Integer = Math.Max(0, Math.Min(maxHeight - 1, Y))
        Dim cw As Integer = Math.Max(1, Math.Min(W, maxWidth - cx))
        Dim ch As Integer = Math.Max(1, Math.Min(H, maxHeight - cy))
        Return New Rectangle(cx, cy, cw, ch)
    End Function
End Class

Public Class LootScanPoint
    Public Property X As Integer
    Public Property Y As Integer

    Public Sub New()
    End Sub

    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
End Class

Public Class ActionRule
    Public Property KeyName As String = ""
    Public Property Enabled As Boolean = True
    Public Property Role As String = "attack"
    Public Property Priority As Integer = 100
    Public Property CooldownMs As Integer = 500
    Public Property TriggerPercent As Integer = 40
    Public Property MinHpPercent As Integer = 1
    Public Property MinMpPercent As Integer = 1
End Class

Public Enum LevelingAgentState
    Disabled
    Searching
    Engaging
    Fighting
    Looting
    Recovering
    Traveling
    Stuck
    GuardedStop
End Enum

Public Class NavigationNode
    Public Property Id As String = ""
    Public Property MapName As String = ""
    Public Property X As Integer
    Public Property Y As Integer
    Public Property Label As String = ""
    Public Property Tags As List(Of String) = New List(Of String)()
End Class

Public Class NavigationEdge
    Public Property FromNodeId As String = ""
    Public Property ToNodeId As String = ""
    Public Property TravelMode As String = "walk"
    Public Property Cost As Double = 1.0
    Public Property Notes As String = ""
End Class

Public Class NavigationPlan
    Public Property MapName As String = ""
    Public Property CurrentNode As NavigationNode = Nothing
    Public Property StartNode As NavigationNode = Nothing
    Public Property TargetNode As NavigationNode = Nothing
    Public Property NextWaypoint As NavigationNode = Nothing
    Public Property Route As List(Of NavigationNode) = New List(Of NavigationNode)()
    Public Property DistanceToNextWaypoint As Double = -1
    Public Property DistanceToTarget As Double = -1
    Public Property RouteReady As Boolean
    Public Property StatusText As String = ""
End Class

Public Class NavigationRouteSample
    Public Property X As Integer
    Public Property Y As Integer
    Public Property CapturedAtUtc As DateTime = DateTime.UtcNow
End Class

Public Class RecordedNavigationGraph
    Public Property MapName As String = ""
    Public Property RouteName As String = ""
    Public Property StartNodeId As String = ""
    Public Property EndNodeId As String = ""
    Public Property Nodes As List(Of NavigationNode) = New List(Of NavigationNode)()
    Public Property Edges As List(Of NavigationEdge) = New List(Of NavigationEdge)()
    Public Property Samples As List(Of NavigationRouteSample) = New List(Of NavigationRouteSample)()
    Public Property SavedAtUtc As DateTime = DateTime.UtcNow
End Class

Public Class RecordedNavigationRouteInfo
    Public Property MapName As String = ""
    Public Property RouteName As String = ""
    Public Property NodeCount As Integer
    Public Property SavedAtUtc As DateTime = DateTime.UtcNow
End Class

Public Class BotConfig
    Public Property WindowTitle As String = "Kathana - The Coming of the Dark Ages"
    Public Property LoopMs As Integer = 80
    Public Property RetargetMs As Integer = 550
    Public Property ForcedRetargetMs As Integer = 550
    Public Property MobHpPresenceThreshold As Double = 1.0
    Public Property HighMaxHpSpecialEnabled As Boolean = False
    Public Property HighMaxHpThreshold As Integer = 2000
    Public Property HpBar As RectRegion = New RectRegion(11, 25, 151, 11)
    Public Property MpBar As RectRegion = New RectRegion(3, 40, 161, 11)
    Public Property MobNameRect As RectRegion = New RectRegion(860, 711, 162, 23)
    Public Property MobHpRect As RectRegion = New RectRegion(859, 737, 165, 11)
    Public Property UnreachableTextRect As RectRegion = New RectRegion(15, 582, 128, 22)
    Public Property PranaExpRect As RectRegion = New RectRegion(472, 745, 78, 21)
    Public Property RupiahsRect As RectRegion = New RectRegion(560, 745, 110, 21)
    Public Property PartyInviteScanRect As RectRegion = New RectRegion(349, 318, 328, 124)
    Public Property PartyInviteOkRect As RectRegion = New RectRegion(463, 410, 59, 21)
    Public Property PartyListRect As RectRegion = New RectRegion(0, 24, 168, 244)
    Public Property MapRect As RectRegion = New RectRegion(0, 0, 1024, 768)
    Public Property MapCoordinateRect As RectRegion = New RectRegion(6, 744, 120, 22)
    Public Property ChatRect As RectRegion = New RectRegion(18, 548, 430, 144)
    Public Property LootScanRect As RectRegion = New RectRegion(220, 80, 584, 430)
    Public Property LootScanPoints As List(Of LootScanPoint) = CreateDefaultLootScanPoints()
    Public Property BypassHpMpLimits As Boolean = False
    Public Property BypassStuckTarget As Boolean = True
    Public Property StuckTargetMs As Integer = 2200
    Public Property DeniedMobs As List(Of String) = New List(Of String)()
    Public Property LootPickupEnabled As Boolean = False
    Public Property LootPickupIntervalMs As Integer = 4000
    Public Property LootPickupVerifyDelayMs As Integer = 80
    Public Property LootRejectClickEnabled As Boolean = False
    Public Property LootRejectPointX As Integer = -1
    Public Property LootRejectPointY As Integer = -1
    Public Property LootAllowedNames As List(Of String) = New List(Of String)()
    Public Property LootNameMatchThresholdPercent As Integer = 80
    Public Property PartyAutoAcceptEnabled As Boolean = True
    Public Property PartyAskEnabled As Boolean = False
    Public Property PartyAskIntervalMs As Integer = 30000
    Public Property PartyAskText As String = "add"
    Public Property LootScannerEnabled As Boolean = True
    Public Property ItemNtfyTopic As String = ""
    Public Property LevelingAgentEnabled As Boolean = False
    Public Property LevelingPreferredMobs As List(Of String) = New List(Of String)()
    Public Property LevelingStopHpPercent As Integer = 20
    Public Property LevelingStopMpPercent As Integer = 10
    Public Property LevelingMaxNoTargetSeconds As Integer = 45
    Public Property LevelingStopOnLowExpRate As Boolean = False
    Public Property LevelingMinExpPerHour As Double = 0.15
    Public Property LevelingStopOnRepeatedUnreachable As Boolean = True
    Public Property LevelingUnreachableLimit As Integer = 4
    Public Property NavigationEnabled As Boolean = False
    Public Property MapOpenKey As String = "M"
    Public Property NavigationMapName As String = "Jina Basin"
    Public Property NavigationStartNodeId As String = ""
    Public Property NavigationTargetNodeId As String = "farming_area"
    Public Property NavigationTravelPreviewEnabled As Boolean = False
    Public Property NavigationTravelExecutionEnabled As Boolean = False
    Public Property NavigationWaypointReachRadius As Integer = 36
    Public Property NavigationMoveBurstMs As Integer = 350
    Public Property NavigationResampleIntervalMs As Integer = 1800
    Public Property NavigationStallTimeoutMs As Integer = 6500
    Public Property NavigationRepathOnStuck As Boolean = True
    Public Property RouteRecordingEnabled As Boolean = False
    Public Property RouteRecordingName As String = "jina_route"
    Public Property RouteRecordingMinSampleDistance As Integer = 8
    Public Property RouteRecordingMinNodeSpacing As Integer = 28
    Public Property ChatTranslationEnabled As Boolean = False
    Public Property ChatTranslationOverlayEnabled As Boolean = True
    Public Property ChatTranslationTargetLanguage As String = "en"
    Public Property ChatTranslationScanIntervalMs As Integer = 700
    Public Property ChatTranslationMaxLines As Integer = 6
    Public Property Actions As List(Of ActionRule) = New List(Of ActionRule)()

    Public Shared Function CreateDefault() As BotConfig
        Dim cfg As New BotConfig()
        Dim keys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
        For i As Integer = 0 To keys.Length - 1
            Dim keyName As String = keys(i)
            Dim isPrimary As Boolean = i < 10
            Dim enabled As Boolean = (keyName = "1" OrElse keyName = "2" OrElse keyName = "6")
            Dim role As String
            If keyName = "6" Then
                role = "heal"
            ElseIf isPrimary Then
                role = "attack"
            Else
                role = "special"
            End If
            Dim trigger As Integer = If(keyName = "6", 80, 40)
            Dim cooldownMs As Integer
            If keyName = "1" Then
                cooldownMs = 600
            ElseIf keyName = "2" Then
                cooldownMs = 450
            Else
                cooldownMs = 1000
            End If
            cfg.Actions.Add(New ActionRule() With {
                .KeyName = keyName,
                .Enabled = enabled,
                .Role = role,
                .Priority = (i + 1) * 10,
                .CooldownMs = cooldownMs,
                .TriggerPercent = trigger,
                .MinHpPercent = 1,
                .MinMpPercent = 1
            })
        Next
        Return cfg
    End Function

    Public Shared Function CreateDefaultLootScanPoints() As List(Of LootScanPoint)
        Return New List(Of LootScanPoint) From {
            New LootScanPoint(220, 80),
            New LootScanPoint(804, 80),
            New LootScanPoint(804, 510),
            New LootScanPoint(220, 510)
        }
    End Function
End Class

Public Class BotStatus
    Public Property Running As Boolean
    Public Property WindowFound As Boolean
    Public Property HpPercent As Double
    Public Property MpPercent As Double
    Public Property MobHpPercent As Double
    Public Property MobMaxHp As Integer = -1
    Public Property MobHpText As String = ""
    Public Property ExpPercent As Double
    Public Property ExpPerHour As Double = -1
    Public Property RupiahsTotal As Long = -1
    Public Property RupiahsPerHour As Double = -1
    Public Property PartySize As Integer
    Public Property PartyAliveCount As Integer
    Public Property PartyAllAlive As Boolean
    Public Property MobName As String = ""
    Public Property TargetValid As Boolean
    Public Property MapCoordinateText As String = ""
    Public Property MapCoordinateX As Integer = -1
    Public Property MapCoordinateY As Integer = -1
    Public Property ChatOcrText As String = ""
    Public Property ChatOcrUpdatedAt As DateTime = DateTime.MinValue
    Public Property MapHeading As String = ""
    Public Property MapCoordinateConfidence As Integer = 0
    Public Property MapMarkerDetected As Boolean
    Public Property MapMarkerX As Integer = -1
    Public Property MapMarkerY As Integer = -1
    Public Property MapLocalizationConfidence As Integer = 0
    Public Property MapVisible As Boolean
    Public Property NavigationMapName As String = ""
    Public Property NavigationCurrentNodeId As String = ""
    Public Property NavigationCurrentNodeLabel As String = ""
    Public Property NavigationNextWaypointId As String = ""
    Public Property NavigationNextWaypointLabel As String = ""
    Public Property NavigationRouteText As String = ""
    Public Property NavigationRouteReady As Boolean
    Public Property NavigationTravelPreviewEnabled As Boolean
    Public Property NavigationTravelExecutionEnabled As Boolean
    Public Property NavigationTravelActive As Boolean
    Public Property NavigationTravelReason As String = ""
    Public Property NavigationDistanceToWaypoint As Double = -1
    Public Property NavigationTravelStalled As Boolean
    Public Property NavigationRecoveryCount As Integer
    Public Property NavigationDestinationReached As Boolean
    Public Property NavigationDestinationLabel As String = ""
    Public Property RouteRecordingEnabled As Boolean
    Public Property RouteRecordingActive As Boolean
    Public Property RouteRecordingMapName As String = ""
    Public Property RouteRecordingName As String = ""
    Public Property RouteRecordingSampleCount As Integer
    Public Property RouteRecordingStatus As String = ""
    Public Property RouteRecordingLastSavedPath As String = ""
    Public Property LastAction As String = ""
    Public Property NotAttackingReason As String = ""
    Public Property ErrorMessage As String = ""
    Public Property AgentEnabled As Boolean
    Public Property AgentState As String = "Disabled"
    Public Property AgentReason As String = ""
    Public Property AgentGuardrailTriggered As Boolean
    Public Property UpdatedAt As DateTime = DateTime.UtcNow
End Class

Friend Module NativeMethods
    Friend Const PW_CLIENTONLY As UInteger = 1UI
    Friend Const PW_RENDERFULLCONTENT As UInteger = 2UI
    Friend Const CAPTUREBLT As CopyPixelOperation = CType(&H40000000, CopyPixelOperation)
    Friend Const WM_KEYDOWN As Integer = &H100
    Friend Const WM_KEYUP As Integer = &H101
    Friend Const WM_MOUSEMOVE As Integer = &H200
    Friend Const WM_LBUTTONDOWN As Integer = &H201
    Friend Const WM_LBUTTONUP As Integer = &H202
    Friend Const MK_LBUTTON As Integer = &H1

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure POINT
        Public X As Integer
        Public Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetForegroundWindow() As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    Friend Delegate Function EnumWindowsProc(hWnd As IntPtr, lParam As IntPtr) As Boolean

    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Friend Function FindWindow(lpClassName As String, lpWindowName As String) As IntPtr
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function EnumWindows(lpEnumFunc As EnumWindowsProc, lParam As IntPtr) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function IsWindowVisible(hWnd As IntPtr) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function IsIconic(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Friend Function GetWindowText(hWnd As IntPtr, lpString As StringBuilder, nMaxCount As Integer) As Integer
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetClientRect(hWnd As IntPtr, ByRef lpRect As RECT) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function ClientToScreen(hWnd As IntPtr, ByRef lpPoint As POINT) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function PostMessage(hWnd As IntPtr, msg As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function MapVirtualKey(uCode As UInteger, uMapType As UInteger) As UInteger
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function PrintWindow(hwnd As IntPtr, hdcBlt As IntPtr, nFlags As UInteger) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef lpdwProcessId As UInteger) As UInteger
    End Function
End Module

Public Class BotEngine
    Public Event StatusUpdated(status As BotStatus)
    Public Event LogLine(line As String)
    Private Const AllowBlindAttackWhenTargetMissing As Boolean = False
    Private Const FirstHitWindowMs As Integer = 800
    Private Const BlacklistLockWindowMs As Integer = 800
    Private Const TargetNameConfirmMinGapMs As Integer = 120
    Private Const TargetNameConfirmRequiredCount As Integer = 2
    Private Const ExpRateSampleMs As Integer = 60000
    Private Const ExpOcrMinIntervalMs As Integer = 900
    Private Const RupiahsOcrMinIntervalMs As Integer = 900
    Private Const MapCoordinateOcrMinIntervalMs As Integer = 900
    Private Const MapMarkerScanMinIntervalMs As Integer = 250
    Private Const NavigationMapToggleCooldownMs As Integer = 450
    Private Const NavigationMapSampleWindowMs As Integer = 950
    Private Const NavigationMapLocalizationRetryDelayMs As Integer = 5000
    Private Const NavigationMapLocalizationFailureLimit As Integer = 2
    Private Const NavigationKnownPoseMaxAgeMs As Integer = 15000
    Private Const NavigationProgressImprovementThreshold As Double = 8.0
    Private Const NavigationRecoveryCooldownMs As Integer = 1500
    Private Const RouteRecordingMinSamplesToSave As Integer = 6
    Private Const RouteRecordingMinSampleIntervalMs As Integer = 250
    Private Const NavigationRotationConfirmationsRequired As Integer = 2
    Private Const NavigationRotationChangeCooldownMs As Integer = 1200
    Private Const MobHpTextOcrMinIntervalMs As Integer = 450
    Private Const PartyInviteOcrMinIntervalMs As Integer = 900
    Private Const PartyListScanMinIntervalMs As Integer = 700
    Private Const UnreachableOcrMinIntervalMs As Integer = 260
    Private Const UnreachableConfirmWindowMs As Integer = 900
    Private Const UnreachableConfirmRequiredCount As Integer = 2
    Private Const UnreachableClearRequiredCount As Integer = 2
    Private Const SustainedSingleZeroConfirmRequiredCount As Integer = 6
    Private Const RetargetBufferMs As Integer = 300
    Private Const BaseClientWidth As Integer = 1024
    Private Const BaseClientHeight As Integer = 768
    Private Const MaxPartyMembers As Integer = 7
    Private Const PartyListBarBandGapRows As Integer = 2
    Private Const FastKeyPressMs As Integer = 12
    Private Const AttackBurstKeysPerLoop As Integer = 3
    Private Const AttackBurstGapMs As Integer = 4
    Private Const StopKeyRepeatGapMs As Integer = 10
    Private Const ForegroundInputSettleMs As Integer = 15
    Private Const CombatLockLostTargetConfirmFrames As Integer = 4

    Private ReadOnly _sync As New Object()
    Private ReadOnly _frameSync As New Object()
    Private Shared ReadOnly NavigationRouteStorageRoot As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotControlPanel", "navigation_routes")
    Private Shared ReadOnly NavigationRouteJsonOptions As New JsonSerializerOptions With {.WriteIndented = True}
    Private Shared ReadOnly _recordedGraphCache As New Dictionary(Of String, List(Of RecordedNavigationGraph))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _recordedGraphCacheSync As New Object()
    Private _config As BotConfig = BotConfig.CreateDefault()
    Private _status As New BotStatus()
    Private _cts As CancellationTokenSource
    Private _task As Task
    Private _lastNormalRetarget As DateTime = DateTime.MinValue
    Private _lastForcedRetarget As DateTime = DateTime.MinValue
    Private _lastTargetWindowSeen As DateTime = DateTime.MinValue
    Private _noTargetBeganAt As DateTime = DateTime.MinValue
    Private _lastAttackAction As DateTime = DateTime.MinValue
    Private _combatLockActive As Boolean = False
    Private _combatLockTargetSignature As String = ""
    Private _combatLockLostSignalCount As Integer = 0
    Private _combatLockLastSeenAt As DateTime = DateTime.MinValue
    Private _lastMobHpSample As Double = -1
    Private _lastMobHpMovement As DateTime = DateTime.MinValue
    Private _noDamageTargetSignature As String = ""
    Private _noDamageAttackCount As Integer = 0
    Private _lastMobNameRead As DateTime = DateTime.MinValue
    Private _cachedMobName As String = ""
    Private _lastMobHpTextScan As DateTime = DateTime.MinValue
    Private _mobHpTextOcrTask As Task(Of String) = Nothing
    Private _lastMobHpText As String = ""
    Private _lastMobDetectedMaxHp As Integer = -1
    Private _latestLoopFrame As Bitmap = Nothing
    Private _latestLoopFrameCapturedAt As DateTime = DateTime.MinValue
    Private _lastLootPickup As DateTime = DateTime.MinValue
    Private _pendingLootPickupVerifyAt As DateTime = DateTime.MinValue
    Private _firstHitPending As Boolean = False
    Private _firstHitTargetSignature As String = ""
    Private _firstHitWindowUntil As DateTime = DateTime.MinValue
    Private _blacklistLockUntil As DateTime = DateTime.MinValue
    Private _nameConfirmCandidate As String = ""
    Private _nameConfirmCount As Integer = 0
    Private _nameConfirmConfirmedName As String = ""
    Private _nameConfirmLastSampleAt As DateTime = DateTime.MinValue
    Private _nameConfirmLastReadProcessedAt As DateTime = DateTime.MinValue
    Private _lastPartyInviteScan As DateTime = DateTime.MinValue
    Private _lastPartyInviteAccept As DateTime = DateTime.MinValue
    Private _partyInviteOcrTask As Task(Of String) = Nothing
    Private _lastPartyInviteCandidate As String = ""
    Private _lastPartyAskAt As DateTime = DateTime.MinValue
    Private _partyAskSuppressedInParty As Boolean = False
    Private _partyAskWasEnabled As Boolean = False
    Private _partyAskPauseLogged As Boolean = False
    Private _lastPartyListScanAt As DateTime = DateTime.MinValue
    Private _lastPartySize As Integer = 0
    Private _lastPartyAliveCount As Integer = 0
    Private _lastPartyAllAlive As Boolean = False
    Private _lastUnreachableScan As DateTime = DateTime.MinValue
    Private _unreachableOcrTask As Task(Of String) = Nothing
    Private _lastUnreachableCandidate As String = ""
    Private _unreachableConfirmCount As Integer = 0
    Private _unreachableLastMatchAt As DateTime = DateTime.MinValue
    Private _unreachableLockUntil As DateTime = DateTime.MinValue
    Private _lastUnreachableTrigger As DateTime = DateTime.MinValue
    Private _unreachableLatched As Boolean = False
    Private _unreachableClearCount As Integer = 0
    Private _lastExpPercent As Double = -1
    Private _lastExpOcrAt As DateTime = DateTime.MinValue
    Private _expOcrTask As Task(Of Double) = Nothing
    Private _lastExpRateSampleAt As DateTime = DateTime.MinValue
    Private _lastExpRateSamplePercent As Double = -1
    Private _lastExpPerHour As Double = -1
    Private _lastRupiahsTotal As Long = -1
    Private _lastRupiahsOcrAt As DateTime = DateTime.MinValue
    Private _rupiahsOcrTask As Task(Of Long) = Nothing
    Private _lastRupiahsRateSampleAt As DateTime = DateTime.MinValue
    Private _lastRupiahsRateSampleTotal As Long = -1
    Private _lastRupiahsPerHour As Double = -1
    Private _lastMapCoordinateOcrAt As DateTime = DateTime.MinValue
    Private _lastMapCoordinateText As String = ""
    Private _lastMapCoordinateX As Integer = -1
    Private _lastMapCoordinateY As Integer = -1
    Private _lastMapCoordinateConfidence As Integer = 0
    Private _lastChatOcrAt As DateTime = DateTime.MinValue
    Private _lastChatOcrText As String = ""
    Private _lastChatOcrNormalized As String = ""
    Private _lastChatOcrUpdatedAt As DateTime = DateTime.MinValue
    Private _lastMapMarkerScanAt As DateTime = DateTime.MinValue
    Private _lastMapMarkerDetected As Boolean = False
    Private _lastMapMarkerX As Integer = -1
    Private _lastMapMarkerY As Integer = -1
    Private _lastMapLocalizationConfidence As Integer = 0
    Private _lastMapVisible As Boolean = False
    Private _lastNavigationMapName As String = ""
    Private _lastNavigationCurrentNodeId As String = ""
    Private _lastNavigationCurrentNodeLabel As String = ""
    Private _lastNavigationNextWaypointId As String = ""
    Private _lastNavigationNextWaypointLabel As String = ""
    Private _lastNavigationRouteText As String = ""
    Private _lastNavigationRouteReady As Boolean = False
    Private _lastNavigationTravelActive As Boolean = False
    Private _lastNavigationTravelReason As String = ""
    Private _lastNavigationDistanceToWaypoint As Double = -1
    Private _lastNavigationTravelStalled As Boolean = False
    Private _lastNavigationRecoveryCount As Integer = 0
    Private _lastNavigationDestinationReached As Boolean = False
    Private _lastNavigationDestinationLabel As String = ""
    Private _lastNavigationProgressWaypointId As String = ""
    Private _lastNavigationProgressDistance As Double = -1
    Private _lastNavigationProgressAt As DateTime = DateTime.MinValue
    Private _lastNavigationRecoveryAt As DateTime = DateTime.MinValue
    Private _lastNavigationKnownPoseAt As DateTime = DateTime.MinValue
    Private _lastNavigationKnownX As Integer = -1
    Private _lastNavigationKnownY As Integer = -1
    Private _lastNavigationPreviousX As Integer = -1
    Private _lastNavigationPreviousY As Integer = -1
    Private _lastNavigationKnownHeading As String = ""
    Private _navigationRotationQuarterTurns As Integer = 0
    Private _navigationRotationCandidateQuarterTurns As Integer = -1
    Private _navigationRotationCandidateCount As Integer = 0
    Private _lastNavigationRotationChangeAt As DateTime = DateTime.MinValue
    Private _lastTravelInputKey As String = ""
    Private _lastTravelInputDesiredDirection As String = ""
    Private _lastTravelInputPoseX As Integer = -1
    Private _lastTravelInputPoseY As Integer = -1
    Private _lastTravelInputAt As DateTime = DateTime.MinValue
    Private _navigationCommittedWaypointId As String = ""
    Private _navigationCommittedWaypointLabel As String = ""
    Private _lastNavigationMapToggleAt As DateTime = DateTime.MinValue
    Private _lastNavigationMoveCommandAt As DateTime = DateTime.MinValue
    Private _navigationMapExpectedOpen As Boolean = False
    Private _navigationAwaitingLocalization As Boolean = False
    Private _navigationLocalizationRetryAfter As DateTime = DateTime.MinValue
    Private _navigationLocalizationFailureCount As Integer = 0
    Private _navigationLocalizationPaused As Boolean = False
    Private _routeRecordingCaptureActive As Boolean = False
    Private _routeRecordingMapName As String = ""
    Private _routeRecordingName As String = ""
    Private _routeRecordingStatus As String = ""
    Private _routeRecordingLastSavedPath As String = ""
    Private _routeRecordingLastSampleAt As DateTime = DateTime.MinValue
    Private ReadOnly _routeRecordingSamples As New List(Of NavigationRouteSample)()
    Private ReadOnly _lootRandom As New Random()
    Private ReadOnly _lastKeyTime As New Dictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
    Private _lastGoodHpPercent As Double = -1
    Private _lastGoodMpPercent As Double = -1
    Private _lastGoodMobHpPercent As Double = -1
    Private _lastGoodMobName As String = ""
    Private _zeroSpikeHoldCount As Integer = 0
    Private _zeroPairConfirmCount As Integer = 0
    Private _singleHpZeroConfirmCount As Integer = 0
    Private _singleMpZeroConfirmCount As Integer = 0
    Private _lastRightAltAt As DateTime = DateTime.MinValue
    Private _lootScannerCapturePending As Boolean = False
    Private _lootScannerCaptureRequestedAt As DateTime = DateTime.MinValue
    Private _lootScannerAltHeld As Boolean = False
    Private _agentState As LevelingAgentState = LevelingAgentState.Disabled
    Private _agentReason As String = ""
    Private _agentGuardrailTriggered As Boolean = False
    Private _agentUnreachableEvents As Integer = 0
    Private Shared ReadOnly MovementStopVks As Integer() = {&H57, &H41, &H53, &H44, &H26, &H28, &H25, &H27}

    Private Shared ReadOnly KeyMap As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
        {"0", &H30}, {"1", &H31}, {"2", &H32}, {"3", &H33}, {"4", &H34}, {"5", &H35},
        {"6", &H36}, {"7", &H37}, {"8", &H38}, {"9", &H39},
        {"A", &H41}, {"B", &H42}, {"C", &H43}, {"D", &H44}, {"E", &H45}, {"F", &H46}, {"G", &H47},
        {"H", &H48}, {"I", &H49}, {"J", &H4A}, {"K", &H4B}, {"L", &H4C}, {"M", &H4D}, {"N", &H4E},
        {"O", &H4F}, {"P", &H50}, {"Q", &H51}, {"R", &H52}, {"S", &H53}, {"T", &H54}, {"U", &H55},
        {"V", &H56}, {"W", &H57}, {"X", &H58}, {"Y", &H59}, {"Z", &H5A},
        {"RMENU", &HA5}, {"RALT", &HA5},
        {"SPACE", &H20}, {" ", &H20},
        {"COMMA", &HBC}, {",", &HBC},
        {"MINUS", &HBD}, {"-", &HBD},
        {"PERIOD", &HBE}, {".", &HBE},
        {"SLASH", &HBF}, {"/", &HBF},
        {"SEMICOLON", &HBA}, {";", &HBA},
        {"APOSTROPHE", &HDE}, {"'", &HDE},
        {"EQUALS", &HBB}, {"=", &HBB},
        {"ESC", &H1B}, {"ESCAPE", &H1B},
        {"ENTER", &HD}, {"RETURN", &HD},
        {"F1", &H70}, {"F2", &H71}, {"F3", &H72}, {"F4", &H73}, {"F5", &H74},
        {"F6", &H75}, {"F7", &H76}, {"F8", &H77}, {"F9", &H78}, {"F10", &H79},
        {"F11", &H7A}, {"F12", &H7B}, {"F13", &H7C}, {"F14", &H7D}, {"F15", &H7E},
        {"F16", &H7F}, {"F17", &H80}, {"F18", &H81}, {"F19", &H82}, {"F20", &H83},
        {"F21", &H84}, {"F22", &H85}, {"F23", &H86}, {"F24", &H87}
    }

    Public Sub UpdateConfig(cfg As BotConfig)
        SyncLock _sync
            _config = cfg
        End SyncLock
    End Sub

    Public Function GetStatus() As BotStatus
        SyncLock _sync
            Return CloneStatus(_status)
        End SyncLock
    End Function

    Public Function IsRunning() As Boolean
        SyncLock _sync
            Return _status.Running
        End SyncLock
    End Function

    Public Sub Start()
        SyncLock _sync
            If _task IsNot Nothing AndAlso Not _task.IsCompleted Then
                Return
            End If
            _cts = New CancellationTokenSource()
            _status.Running = True
            _status.ErrorMessage = ""
            _lastNormalRetarget = DateTime.MinValue
            _lastForcedRetarget = DateTime.MinValue
            _lastTargetWindowSeen = DateTime.MinValue
            _noTargetBeganAt = DateTime.MinValue
            _lastAttackAction = DateTime.MinValue
            _combatLockActive = False
            _combatLockTargetSignature = ""
            _combatLockLostSignalCount = 0
            _combatLockLastSeenAt = DateTime.MinValue
            _lastMobHpSample = -1
            _lastMobHpMovement = DateTime.MinValue
            _noDamageTargetSignature = ""
            _noDamageAttackCount = 0
            _lastMobNameRead = DateTime.MinValue
            _cachedMobName = ""
            _lastMobHpTextScan = DateTime.MinValue
            _mobHpTextOcrTask = Nothing
            _lastMobHpText = ""
            _lastMobDetectedMaxHp = -1
            _lastLootPickup = DateTime.MinValue
            _pendingLootPickupVerifyAt = DateTime.MinValue
            _firstHitPending = False
            _firstHitTargetSignature = ""
            _firstHitWindowUntil = DateTime.MinValue
            _blacklistLockUntil = DateTime.MinValue
            _nameConfirmCandidate = ""
            _nameConfirmCount = 0
            _nameConfirmConfirmedName = ""
            _nameConfirmLastSampleAt = DateTime.MinValue
            _nameConfirmLastReadProcessedAt = DateTime.MinValue
            _lastPartyInviteScan = DateTime.MinValue
            _lastPartyInviteAccept = DateTime.MinValue
            _partyInviteOcrTask = Nothing
            _lastPartyInviteCandidate = ""
            _lastPartyAskAt = DateTime.MinValue
            _partyAskSuppressedInParty = False
            _partyAskWasEnabled = False
            _partyAskPauseLogged = False
            _lastPartyListScanAt = DateTime.MinValue
            _lastPartySize = 0
            _lastPartyAliveCount = 0
            _lastPartyAllAlive = False
            _lastUnreachableScan = DateTime.MinValue
            _unreachableOcrTask = Nothing
            _lastUnreachableCandidate = ""
            _unreachableConfirmCount = 0
            _unreachableLastMatchAt = DateTime.MinValue
            _unreachableLockUntil = DateTime.MinValue
            _lastUnreachableTrigger = DateTime.MinValue
            _unreachableLatched = False
            _unreachableClearCount = 0
            _lastExpPercent = -1
            _lastExpOcrAt = DateTime.MinValue
            _expOcrTask = Nothing
            _lastExpRateSampleAt = DateTime.MinValue
            _lastExpRateSamplePercent = -1
            _lastExpPerHour = -1
            _lastRupiahsTotal = -1
            _lastRupiahsOcrAt = DateTime.MinValue
            _rupiahsOcrTask = Nothing
            _lastRupiahsRateSampleAt = DateTime.MinValue
            _lastRupiahsRateSampleTotal = -1
            _lastRupiahsPerHour = -1
            _lastMapCoordinateOcrAt = DateTime.MinValue
            _lastMapCoordinateText = ""
            _lastMapCoordinateX = -1
            _lastMapCoordinateY = -1
            _lastMapCoordinateConfidence = 0
            _lastChatOcrAt = DateTime.MinValue
            _lastChatOcrText = ""
            _lastChatOcrNormalized = ""
            _lastChatOcrUpdatedAt = DateTime.MinValue
            _lastMapMarkerScanAt = DateTime.MinValue
            _lastMapMarkerDetected = False
            _lastMapMarkerX = -1
            _lastMapMarkerY = -1
            _lastMapLocalizationConfidence = 0
            _lastMapVisible = False
            _navigationAwaitingLocalization = False
            _navigationLocalizationRetryAfter = DateTime.MinValue
            _navigationLocalizationFailureCount = 0
            _navigationLocalizationPaused = False
            _routeRecordingCaptureActive = False
            _routeRecordingMapName = ""
            _routeRecordingName = ""
            _routeRecordingStatus = ""
            _routeRecordingLastSavedPath = ""
            _routeRecordingLastSampleAt = DateTime.MinValue
            _routeRecordingSamples.Clear()
            _lastNavigationMapName = ""
            _lastNavigationCurrentNodeId = ""
            _lastNavigationCurrentNodeLabel = ""
            _lastNavigationNextWaypointId = ""
            _lastNavigationNextWaypointLabel = ""
            _lastNavigationRouteText = ""
            _lastNavigationRouteReady = False
            _lastNavigationTravelActive = False
            _lastNavigationTravelReason = ""
            _lastNavigationDistanceToWaypoint = -1
            _lastNavigationTravelStalled = False
            _lastNavigationRecoveryCount = 0
            _lastNavigationDestinationReached = False
            _lastNavigationDestinationLabel = ""
            _lastNavigationProgressWaypointId = ""
            _lastNavigationProgressDistance = -1
            _lastNavigationProgressAt = DateTime.MinValue
            _lastNavigationRecoveryAt = DateTime.MinValue
            _lastNavigationKnownPoseAt = DateTime.MinValue
            _lastNavigationKnownX = -1
            _lastNavigationKnownY = -1
            _lastNavigationPreviousX = -1
            _lastNavigationPreviousY = -1
            _lastNavigationKnownHeading = ""
            _navigationRotationQuarterTurns = 0
            _navigationRotationCandidateQuarterTurns = -1
            _navigationRotationCandidateCount = 0
            _lastNavigationRotationChangeAt = DateTime.MinValue
            _lastTravelInputKey = ""
            _lastTravelInputDesiredDirection = ""
            _lastTravelInputPoseX = -1
            _lastTravelInputPoseY = -1
            _lastTravelInputAt = DateTime.MinValue
            _navigationCommittedWaypointId = ""
            _navigationCommittedWaypointLabel = ""
            _lastNavigationMapToggleAt = DateTime.MinValue
            _lastNavigationMoveCommandAt = DateTime.MinValue
            _navigationMapExpectedOpen = False
            _lastGoodHpPercent = -1
            _lastGoodMpPercent = -1
            _lastGoodMobHpPercent = -1
            _lastGoodMobName = ""
            _zeroSpikeHoldCount = 0
            _zeroPairConfirmCount = 0
            _singleHpZeroConfirmCount = 0
            _singleMpZeroConfirmCount = 0
            _lastRightAltAt = DateTime.MinValue
            _lootScannerCapturePending = False
            _lootScannerCaptureRequestedAt = DateTime.MinValue
            _lootScannerAltHeld = False
            _agentState = If(_config.LevelingAgentEnabled, LevelingAgentState.Searching, LevelingAgentState.Disabled)
            _agentReason = ""
            _agentGuardrailTriggered = False
            _agentUnreachableEvents = 0
            _task = Task.Run(Sub() LoopAsync(_cts.Token).GetAwaiter().GetResult())
        End SyncLock
        ClearLatestLoopFrame()
        RaiseEvent LogLine("Bot loop started.")
    End Sub

    Public Sub [Stop]()
        Dim localTask As Task = Nothing
        SyncLock _sync
            If _cts IsNot Nothing Then
                _cts.Cancel()
            End If
            localTask = _task
        End SyncLock

        If localTask IsNot Nothing Then
            Try
                localTask.Wait(1500)
            Catch
            End Try
        End If

        SyncLock _sync
            _status.Running = False
            _lootScannerCapturePending = False
            _lootScannerCaptureRequestedAt = DateTime.MinValue
            _lootScannerAltHeld = False
            _pendingLootPickupVerifyAt = DateTime.MinValue
        End SyncLock
        ReleaseLootScannerAltKey()
        ClearLatestLoopFrame()
        RaiseEvent LogLine("Bot loop stopped.")
    End Sub

    Public Function CaptureSnapshot() As Bitmap
        Dim cfg As BotConfig
        Dim running As Boolean
        SyncLock _sync
            cfg = _config
            running = _status.Running
        End SyncLock

        If running Then
            Dim cachedFrame As Bitmap = GetLatestLoopFrameClone(Math.Max(200, cfg.LoopMs * 3))
            If cachedFrame IsNot Nothing Then
                Return cachedFrame
            End If
            Return Nothing
        End If

        Dim hwnd As IntPtr = FindGameWindow(cfg.WindowTitle)
        If hwnd = IntPtr.Zero Then
            Return Nothing
        End If

        Return CaptureClient(hwnd)
    End Function

    Private Sub ReplaceLatestLoopFrame(frame As Bitmap)
        If frame Is Nothing Then
            ClearLatestLoopFrame()
            Return
        End If

        Dim frameClone As Bitmap = DirectCast(frame.Clone(), Bitmap)
        Dim oldFrame As Bitmap = Nothing
        SyncLock _frameSync
            oldFrame = _latestLoopFrame
            _latestLoopFrame = frameClone
            _latestLoopFrameCapturedAt = DateTime.UtcNow
        End SyncLock

        If oldFrame IsNot Nothing Then
            oldFrame.Dispose()
        End If
    End Sub

    Private Function GetLatestLoopFrameClone(Optional maxAgeMs As Integer = -1) As Bitmap
        SyncLock _frameSync
            If _latestLoopFrame Is Nothing Then
                Return Nothing
            End If
            If maxAgeMs >= 0 AndAlso _latestLoopFrameCapturedAt <> DateTime.MinValue AndAlso
                (DateTime.UtcNow - _latestLoopFrameCapturedAt).TotalMilliseconds > maxAgeMs Then
                Return Nothing
            End If
            Return DirectCast(_latestLoopFrame.Clone(), Bitmap)
        End SyncLock
    End Function

    Private Sub ClearLatestLoopFrame()
        Dim oldFrame As Bitmap = Nothing
        SyncLock _frameSync
            oldFrame = _latestLoopFrame
            _latestLoopFrame = Nothing
            _latestLoopFrameCapturedAt = DateTime.MinValue
        End SyncLock

        If oldFrame IsNot Nothing Then
            oldFrame.Dispose()
        End If
    End Sub

    Private Async Function LoopAsync(token As CancellationToken) As Task
        While Not token.IsCancellationRequested
            Dim cfg As BotConfig
            SyncLock _sync
                cfg = _config
            End SyncLock
            Dim loopDelayMs As Integer = Math.Max(1, cfg.LoopMs)
            Dim retargetDelayMs As Integer = GetRetargetCooldownMs(cfg, loopDelayMs)
            Dim noTargetStableMs As Integer = retargetDelayMs

            Dim hwnd As IntPtr = FindGameWindow(cfg.WindowTitle)
            If hwnd = IntPtr.Zero Then
                ClearLatestLoopFrame()
                ReleaseLootScannerAltKey()
                ClearMapLocalizationRuntime()
                ClearChatTranslationRuntime()
                ClearPartyListRuntimeState()
                UpdateLevelingAgentState(cfg, LevelingAgentState.Searching, "Game window not found.")
                SetStatus(Sub(s)
                              s.WindowFound = False
                              s.HpPercent = 0
                              s.MpPercent = 0
                              s.MobHpPercent = 0
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.ExpPercent = 0
                              s.ExpPerHour = -1
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.MobName = ""
                              s.TargetValid = False
                              s.NotAttackingReason = "Window not found."
                              s.ErrorMessage = "Game window not found."
                          End Sub)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If

            Dim frame As Bitmap = CaptureClient(hwnd)
            If frame Is Nothing Then
                ClearLatestLoopFrame()
                ReleaseLootScannerAltKey()
                ClearMapLocalizationRuntime()
                ClearChatTranslationRuntime()
                ClearPartyListRuntimeState()
                UpdateLevelingAgentState(cfg, LevelingAgentState.Searching, "Unable to capture game client.")
                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.NotAttackingReason = "Capture failed."
                              s.ErrorMessage = "Unable to capture game client."
                          End Sub)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If
            ReplaceLatestLoopFrame(frame)

            Dim hpRegion As New RectRegion(0, 0, 1, 1)
            Dim mpRegion As New RectRegion(0, 0, 1, 1)
            Dim mobNameRegion As New RectRegion(0, 0, 1, 1)
            Dim mobHpRegion As New RectRegion(0, 0, 1, 1)
            Dim unreachableTextRegion As New RectRegion(0, 0, 1, 1)
            Dim pranaExpRegion As New RectRegion(0, 0, 1, 1)
            Dim rupiahsRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteScanRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteOkRegion As New RectRegion(0, 0, 1, 1)
            Dim partyListRegion As New RectRegion(0, 0, 1, 1)
            Dim mapRegion As New RectRegion(0, 0, 1, 1)
            Dim mapCoordinateRegion As New RectRegion(0, 0, 1, 1)
            Dim chatRegion As New RectRegion(0, 0, 1, 1)
            ResolveVisionRegions(cfg, frame.Width, frame.Height, hpRegion, mpRegion, mobNameRegion, mobHpRegion, unreachableTextRegion, pranaExpRegion, rupiahsRegion, partyInviteScanRegion, partyInviteOkRegion, partyListRegion, mapRegion, mapCoordinateRegion, chatRegion)
            Dim lootScanPolygon As List(Of DrawingPoint) = ResolveLootScanPolygon(cfg, frame.Width, frame.Height)

            Dim hpPct As Double = ComputeBarPercent(frame, hpRegion, True)
            Dim mpPct As Double = ComputeBarPercent(frame, mpRegion, False)
            Dim mobHpPct As Double = ComputeBarPercent(frame, mobHpRegion, True)
            Dim expPct As Double = ReadPranaExpPercent(frame, pranaExpRegion)
            Dim rupiahsTotal As Long = ReadRupiahsTotal(frame, rupiahsRegion)
            Dim captureGlitch As Boolean = IsLikelyVisionCaptureGlitch(frame, hpRegion, mpRegion, hpPct, mpPct)

            Dim now As DateTime = DateTime.UtcNow
            Dim activeHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            TryHandlePendingLootScannerCapture(cfg, hwnd, activeHwnd, frame, lootScanPolygon, now)
            TryHandlePendingLootPickupVerification(cfg, hwnd, frame, now, mobNameRegion)
            If cfg.LootScannerEnabled AndAlso activeHwnd = hwnd AndAlso (Not _lootScannerCapturePending) AndAlso (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then
                BeginLootScannerCapture(now)
            End If
            Dim monsterFilterActive As Boolean = (cfg.DeniedMobs IsNot Nothing AndAlso cfg.DeniedMobs.Count > 0)
            Dim targetWindowSignalNoName As Boolean = HasTargetWindowSignal(frame, mobHpRegion, "", mobHpPct)
            Dim shouldReadMobName As Boolean = targetWindowSignalNoName OrElse (mobHpPct >= Math.Max(0.6, cfg.MobHpPresenceThreshold * 0.7))
            Dim forceMobNameRefresh As Boolean = monsterFilterActive AndAlso targetWindowSignalNoName AndAlso ((now - _lastMobNameRead).TotalMilliseconds >= 180)
            Dim mobName As String
            If shouldReadMobName Then
                mobName = ReadMobNameIfNeeded(frame, mobNameRegion, now, forceMobNameRefresh)
            Else
                ' Avoid stale-name attacks after target switches.
                _cachedMobName = ""
                _lastMobNameRead = DateTime.MinValue
                mobName = ""
            End If
            ApplyVisionStabilityFilter(hpPct, mpPct, mobHpPct, mobName, captureGlitch)
            Dim expPerHour As Double = UpdateExpRate(expPct, now)
            Dim rupiahsPerHour As Double = UpdateRupiahsRate(rupiahsTotal, now)
            If cfg.NavigationEnabled Then
                ReadMapCoordinateIfNeeded(frame, mapCoordinateRegion, now)
                ScanMapPlayerMarkerIfNeeded(now)
                UpdateMapLocalizationConfidence()
                UpdateMapVisibleState()
                UpdateLastKnownNavigationPose(now)
                UpdateRouteRecording(cfg, now)
                UpdateNavigationPreview(cfg, now)
            Else
                ClearMapLocalizationRuntime()
                ClearNavigationPreviewRuntime()
                ClearNavigationTravelRuntime()
            End If
            If cfg.ChatTranslationEnabled Then
                ReadChatTextIfNeeded(frame, chatRegion, cfg, now)
            Else
                ClearChatTranslationRuntime()
            End If
            ReadPartyListIfNeeded(frame, partyListRegion, now)
            Dim targetWindowVisible As Boolean = HasTargetWindowSignal(frame, mobHpRegion, mobName, mobHpPct)
            Dim hasHighMaxHpAction As Boolean = HasHighMaxHpAttackAction(cfg)
            Dim mobMaxHp As Integer = UpdateMobMaxHpTracking(cfg, frame, mobHpRegion, targetWindowVisible, mobHpPct, now)
            Dim highMaxHpAttackActive As Boolean =
                cfg.HighMaxHpSpecialEnabled AndAlso
                hasHighMaxHpAction AndAlso
                mobMaxHp >= Math.Max(1, cfg.HighMaxHpThreshold)
            If targetWindowVisible Then
                _lastTargetWindowSeen = now
                _noTargetBeganAt = DateTime.MinValue
            ElseIf _noTargetBeganAt = DateTime.MinValue Then
                _noTargetBeganAt = now
            End If
            Dim deniedTarget As Boolean = IsDeniedMob(mobName, cfg.DeniedMobs)
            Dim normMobName As String = NormalizeMobName(mobName)
            Dim preferredMobFilterActive As Boolean = cfg.LevelingAgentEnabled AndAlso cfg.LevelingPreferredMobs IsNot Nothing AndAlso cfg.LevelingPreferredMobs.Count > 0
            Dim missingNameBlockedByPreference As Boolean = preferredMobFilterActive AndAlso targetWindowVisible AndAlso normMobName = ""
            Dim preferredTargetMismatch As Boolean = preferredMobFilterActive AndAlso normMobName <> "" AndAlso Not IsPreferredMob(mobName, cfg.LevelingPreferredMobs)
            Dim unreachableTriggered As Boolean = TryHandleUnreachableTarget(cfg, hwnd, frame, now, unreachableTextRegion)
            Dim unreachableLockActive As Boolean = (_unreachableLockUntil <> DateTime.MinValue AndAlso now < _unreachableLockUntil)
            If unreachableTriggered Then
                _agentUnreachableEvents += 1
            End If

            If monsterFilterActive AndAlso deniedTarget Then
                _blacklistLockUntil = now.AddMilliseconds(BlacklistLockWindowMs)
                _nameConfirmCandidate = ""
                _nameConfirmCount = 0
                _nameConfirmConfirmedName = ""
                _nameConfirmLastSampleAt = DateTime.MinValue
                _nameConfirmLastReadProcessedAt = DateTime.MinValue
            End If

            Dim nameSampleUpdated As Boolean = (_lastMobNameRead <> DateTime.MinValue AndAlso _lastMobNameRead > _nameConfirmLastReadProcessedAt)
            If nameSampleUpdated Then
                _nameConfirmLastReadProcessedAt = _lastMobNameRead
            End If

            If Not monsterFilterActive Then
                _nameConfirmCandidate = ""
                _nameConfirmCount = 0
                _nameConfirmConfirmedName = ""
                _nameConfirmLastSampleAt = DateTime.MinValue
                _nameConfirmLastReadProcessedAt = DateTime.MinValue
            ElseIf Not targetWindowVisible OrElse normMobName = "" Then
                _nameConfirmCandidate = ""
                _nameConfirmCount = 0
                _nameConfirmConfirmedName = ""
                _nameConfirmLastSampleAt = DateTime.MinValue
            ElseIf deniedTarget Then
                ' Already handled above; keep state reset while denied.
            ElseIf _nameConfirmConfirmedName.Equals(normMobName, StringComparison.OrdinalIgnoreCase) Then
                ' Keep confirmed state for current stable target name.
            ElseIf nameSampleUpdated Then
                If _nameConfirmCandidate.Equals(normMobName, StringComparison.OrdinalIgnoreCase) Then
                    If _nameConfirmLastSampleAt = DateTime.MinValue OrElse (_lastMobNameRead - _nameConfirmLastSampleAt).TotalMilliseconds >= TargetNameConfirmMinGapMs Then
                        _nameConfirmCount += 1
                        _nameConfirmLastSampleAt = _lastMobNameRead
                    End If
                Else
                    _nameConfirmCandidate = normMobName
                    _nameConfirmCount = 1
                    _nameConfirmLastSampleAt = _lastMobNameRead
                End If

                If _nameConfirmCount >= TargetNameConfirmRequiredCount Then
                    _nameConfirmConfirmedName = normMobName
                End If
            End If

            Dim blacklistLockActive As Boolean = monsterFilterActive AndAlso _blacklistLockUntil <> DateTime.MinValue AndAlso now < _blacklistLockUntil
            Dim nameConfirmedForAttack As Boolean = (Not monsterFilterActive) OrElse (normMobName <> "" AndAlso _nameConfirmConfirmedName.Equals(normMobName, StringComparison.OrdinalIgnoreCase))
            Dim missingNameBlockedByFilter As Boolean = monsterFilterActive AndAlso targetWindowVisible AndAlso normMobName = ""
            Dim nameConfirmationBlockedByFilter As Boolean = monsterFilterActive AndAlso targetWindowVisible AndAlso (Not missingNameBlockedByFilter) AndAlso (Not deniedTarget) AndAlso (Not nameConfirmedForAttack)
            Dim currentTargetAliveSignal As Boolean = HasLivingTargetSignal(targetWindowVisible, mobHpPct, cfg)
            Dim combatLockActive As Boolean = UpdateCombatLockState(now, cfg, currentTargetAliveSignal, normMobName)
            Dim canTrackFirstHitTarget As Boolean = currentTargetAliveSignal AndAlso (Not deniedTarget) AndAlso (Not missingNameBlockedByFilter)
            Dim currentFirstHitSignature As String = normMobName
            If canTrackFirstHitTarget Then
                Dim isNewFirstHitTarget As Boolean = (Not _firstHitPending) OrElse ((currentFirstHitSignature <> "") AndAlso (Not _firstHitTargetSignature.Equals(currentFirstHitSignature, StringComparison.OrdinalIgnoreCase)))
                If isNewFirstHitTarget Then
                    _firstHitPending = True
                    _firstHitTargetSignature = currentFirstHitSignature
                    _firstHitWindowUntil = now.AddMilliseconds(FirstHitWindowMs)
                End If
            ElseIf Not targetWindowVisible OrElse deniedTarget Then
                _firstHitPending = False
                _firstHitTargetSignature = ""
                _firstHitWindowUntil = DateTime.MinValue
            End If
            Dim firstHitWindowActive As Boolean = _firstHitPending AndAlso now < _firstHitWindowUntil
            Dim targetValid As Boolean =
                currentTargetAliveSignal AndAlso
                (Not deniedTarget) AndAlso
                (Not missingNameBlockedByFilter) AndAlso
                (Not missingNameBlockedByPreference) AndAlso
                (Not preferredTargetMismatch) AndAlso
                (Not nameConfirmationBlockedByFilter) AndAlso
                (Not blacklistLockActive) AndAlso
                (Not unreachableLockActive)
            TrackMobHpMovement(targetValid, mobHpPct, now)

            Dim guardrailReason As String = ""
            If ShouldTriggerLevelingGuardrail(cfg, hpPct, mpPct, expPerHour, now, targetWindowVisible, guardrailReason) Then
                frame.Dispose()
                TriggerLevelingGuardrailStop(cfg, guardrailReason)
                Exit While
            End If

            Dim reason As String = ""
            Dim actionSent As Boolean = TryHandleAutoAcceptPrompts(cfg, hwnd, frame, now, partyInviteScanRegion, partyInviteOkRegion)
            If actionSent Then
                reason = "Auto-accept prompt detected and accepted."
            End If
            If Not actionSent Then
                actionSent = TryHandlePartyAsk(cfg, hwnd, now)
                If actionSent Then
                    reason = "Party ask command sent."
                End If
            End If
            If unreachableTriggered AndAlso Not actionSent Then
                actionSent = True
                reason = "Unable to reach target detected. Forced retarget."
            End If
            Dim forcedRetarget As Boolean = False

            If ShouldBypassStuckTarget(cfg, targetWindowVisible, targetValid, now) Then
                If TrySendRetargetKey(hwnd, cfg, now, "E (stuck target bypass)", forced:=True) Then
                    _noDamageTargetSignature = ""
                    _noDamageAttackCount = 0
                    _firstHitPending = False
                    _firstHitTargetSignature = ""
                    _firstHitWindowUntil = DateTime.MinValue
                    reason = "Stuck target bypass sent retarget."
                    forcedRetarget = True
                End If
            End If

            If Not forcedRetarget AndAlso Not actionSent Then
                Dim supportSent As Boolean = TrySendSupportActions(cfg, hwnd, hpPct, mpPct)
                If supportSent Then
                    actionSent = True
                    reason = ""
                End If

                If Not actionSent AndAlso Not targetWindowVisible AndAlso Not combatLockActive Then
                    Dim travelReason As String = ""
                    If TryHandleNavigationTravel(cfg, hwnd, now, targetWindowVisible, targetValid, travelReason) Then
                        actionSent = True
                        reason = travelReason
                    ElseIf String.IsNullOrWhiteSpace(reason) AndAlso Not String.IsNullOrWhiteSpace(travelReason) Then
                        reason = travelReason
                    End If
                End If

                ' Support keys can fire without blocking attack/special in the same loop.
                Dim allowBlindAttack As Boolean = AllowBlindAttackWhenTargetMissing AndAlso (Not deniedTarget) AndAlso (Not _lastNavigationTravelActive)
                Dim attackBurst As List(Of ActionRule) = ChooseAttackBurstActions(cfg, hpPct, mpPct, targetValid, allowBlindAttack, highMaxHpAttackActive, reason)
                If attackBurst.Count > 0 Then
                    Dim sentKeys As New List(Of String)()
                    Dim targetSignature As String = If(normMobName <> "", normMobName, If(mobName <> "", mobName, $"{mobHpPct:0.0}"))
                    For Each attackAction As ActionRule In attackBurst
                        If sentKeys.Count > 0 Then
                            Thread.Sleep(AttackBurstGapMs)
                        End If

                        If Not SendKey(hwnd, attackAction.KeyName, FastKeyPressMs) Then
                            Continue For
                        End If

                        MarkKeyUsed(attackAction.KeyName)
                        sentKeys.Add(attackAction.KeyName)
                        _lastAttackAction = DateTime.UtcNow
                        BeginCombatLock(targetSignature, DateTime.UtcNow)
                        _firstHitPending = False
                        _firstHitWindowUntil = DateTime.MinValue
                        RecordAttackWithoutDamage(targetSignature)
                    Next

                    If sentKeys.Count > 0 Then
                        Dim actionLabel As String = If(sentKeys.Count = 1, $"{sentKeys(0)} ({attackBurst(0).Role})", $"{String.Join("/", sentKeys)} (attack burst)")
                        SetLastAction(actionLabel)
                        actionSent = True
                        reason = ""
                    End If
                End If
            End If

            If Not targetValid AndAlso Not actionSent AndAlso Not _lastNavigationTravelActive Then
                Dim filterBlockedRetarget As Boolean = deniedTarget OrElse blacklistLockActive OrElse missingNameBlockedByFilter OrElse missingNameBlockedByPreference OrElse preferredTargetMismatch OrElse nameConfirmationBlockedByFilter
                If _firstHitPending Then
                    If String.IsNullOrWhiteSpace(reason) Then
                        If firstHitWindowActive Then
                            reason = $"First-hit attack window active ({FirstHitWindowMs}ms). Waiting to send first attack."
                        Else
                            reason = "Waiting to send first attack before retarget."
                        End If
                    End If
                ElseIf combatLockActive Then
                    If String.IsNullOrWhiteSpace(reason) Then
                        reason = "Current mob still considered engaged. Waiting for death confirmation before retarget."
                    End If
                Else
                    If (Not filterBlockedRetarget) AndAlso _lastTargetWindowSeen <> DateTime.MinValue AndAlso (now - _lastTargetWindowSeen).TotalMilliseconds < retargetDelayMs Then
                        If String.IsNullOrWhiteSpace(reason) Then
                            reason = $"Target window just changed. Waiting {retargetDelayMs}ms before retarget."
                        End If
                    ElseIf _noTargetBeganAt <> DateTime.MinValue AndAlso (now - _noTargetBeganAt).TotalMilliseconds < noTargetStableMs Then
                        If String.IsNullOrWhiteSpace(reason) Then
                            reason = $"No target not stable yet. Waiting {noTargetStableMs}ms."
                        End If
                    ElseIf (_lastNormalRetarget = DateTime.MinValue) OrElse (now - _lastNormalRetarget).TotalMilliseconds >= retargetDelayMs Then
                        If TrySendRetargetKey(hwnd, cfg, now, "E (retarget)", forced:=False) Then
                            _noDamageTargetSignature = ""
                            _noDamageAttackCount = 0
                            If String.IsNullOrWhiteSpace(reason) Then
                                If deniedTarget Then
                                    reason = $"Monster filter blocked target '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent."
                                ElseIf blacklistLockActive Then
                                    reason = $"Monster filter lock active ({BlacklistLockWindowMs}ms). Retarget key sent."
                                ElseIf missingNameBlockedByFilter Then
                                    reason = "Monster filter waiting for mob name OCR. Retarget key sent."
                                ElseIf missingNameBlockedByPreference Then
                                    reason = "Leveling agent waiting for mob name OCR before preferred-mob check. Retarget key sent."
                                ElseIf preferredTargetMismatch Then
                                    reason = $"Leveling agent skipped non-preferred mob '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent."
                                ElseIf nameConfirmationBlockedByFilter Then
                                    reason = "Monster filter waiting for 2x name confirmation. Retarget key sent."
                                ElseIf unreachableLockActive Then
                                    reason = "Unable-to-reach lock active. Retarget key sent."
                                ElseIf Not targetWindowVisible Then
                                    reason = "No target window detected. Retarget key sent."
                                Else
                                    reason = "No target detected. Retarget key sent."
                                End If
                            End If
                        End If
                    ElseIf String.IsNullOrWhiteSpace(reason) Then
                        If deniedTarget Then
                            reason = "Monster filter blocked target. Waiting retarget cooldown."
                        ElseIf blacklistLockActive Then
                            reason = $"Monster filter lock active ({BlacklistLockWindowMs}ms). Waiting retarget cooldown."
                        ElseIf missingNameBlockedByFilter Then
                            reason = "Monster filter waiting for mob name OCR. Waiting retarget cooldown."
                        ElseIf missingNameBlockedByPreference Then
                            reason = "Leveling agent waiting for mob name OCR before preferred-mob check."
                        ElseIf preferredTargetMismatch Then
                            reason = "Leveling agent is searching for a preferred mob."
                        ElseIf nameConfirmationBlockedByFilter Then
                            reason = "Monster filter waiting for 2x name confirmation. Waiting retarget cooldown."
                        ElseIf unreachableLockActive Then
                            reason = "Unable-to-reach lock active. Waiting retarget cooldown."
                        ElseIf Not targetWindowVisible Then
                            reason = $"No target window detected. Waiting {retargetDelayMs}ms retarget cooldown."
                        Else
                            reason = $"No target detected. Waiting {retargetDelayMs}ms retarget cooldown."
                        End If
                    End If
                End If
            End If

            TryHandleLootPickup(cfg, hwnd, now, actionSent OrElse _firstHitPending)
            UpdateLevelingAgentRuntimeState(cfg, now, hpPct, mpPct, targetWindowVisible, targetValid, actionSent, forcedRetarget OrElse unreachableTriggered, unreachableLockActive, reason)

            frame.Dispose()

            SetStatus(Sub(s)
                          s.WindowFound = True
                          s.HpPercent = Math.Round(hpPct, 1)
                          s.MpPercent = Math.Round(mpPct, 1)
                          s.MobHpPercent = Math.Round(mobHpPct, 1)
                          s.MobMaxHp = mobMaxHp
                          s.MobHpText = _lastMobHpText
                          s.ExpPercent = Math.Round(Math.Max(0, If(expPct < 0, 0, expPct)), 2)
                          s.ExpPerHour = If(expPerHour < 0, -1, Math.Round(expPerHour, 2))
                          s.RupiahsTotal = rupiahsTotal
                          s.RupiahsPerHour = If(rupiahsPerHour < 0, -1, Math.Round(rupiahsPerHour, 0))
                          s.MobName = mobName
                          s.TargetValid = targetValid
                          s.NotAttackingReason = If(actionSent, "", reason)
                          s.ErrorMessage = ""
                      End Sub)

            Await Task.Delay(loopDelayMs, token)
        End While

        ReleaseLootScannerAltKey()
        ClearLatestLoopFrame()
    End Function

    Private Sub BeginLootScannerCapture(now As DateTime)
        Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&H12), 0UI))
        Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1

        Try
            keybd_event(&HA5, scan, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero)
            _lootScannerCapturePending = True
            _lootScannerCaptureRequestedAt = now
            _lootScannerAltHeld = True
            _lastRightAltAt = now
        Catch
            ReleaseLootScannerAltKey()
        End Try
    End Sub

    Private Sub TryHandlePendingLootScannerCapture(cfg As BotConfig, hwnd As IntPtr, activeHwnd As IntPtr, frame As Bitmap, lootScanPolygon As List(Of DrawingPoint), now As DateTime)
        If Not _lootScannerCapturePending Then
            Return
        End If

        If frame Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return
        End If

        If activeHwnd <> hwnd Then
            If _lootScannerCaptureRequestedAt <> DateTime.MinValue AndAlso (now - _lootScannerCaptureRequestedAt).TotalMilliseconds >= 500 Then
                ReleaseLootScannerAltKey()
                _lootScannerCapturePending = False
                _lootScannerCaptureRequestedAt = DateTime.MinValue
            End If
            Return
        End If

        Dim loopMs As Integer = 80
        If cfg IsNot Nothing Then
            loopMs = Math.Max(20, cfg.LoopMs)
        End If
        If _lootScannerCaptureRequestedAt <> DateTime.MinValue AndAlso (now - _lootScannerCaptureRequestedAt).TotalMilliseconds < loopMs Then
            Return
        End If

        Dim frameClone As Bitmap = DirectCast(frame.Clone(), Bitmap)
        Dim allowedNames As List(Of String) = If(cfg.LootAllowedNames, New List(Of String)()).ToList()
        Dim lootMatchThresholdPercent As Integer = ClampLootMatchThresholdPercent(cfg.LootNameMatchThresholdPercent)
        Dim lootScanPolygonCopy As List(Of DrawingPoint) = ClonePointList(lootScanPolygon)
        Dim topic As String = If(cfg.ItemNtfyTopic, "")

        Task.Run(Sub()
            Dim scanFrame As Bitmap = frameClone
            Dim lootScanFrame As Bitmap = Nothing
            Try
                lootScanFrame = CropBitmapToPolygon(scanFrame, lootScanPolygonCopy)
                If lootScanFrame Is Nothing Then
                    lootScanFrame = DirectCast(scanFrame.Clone(), Bitmap)
                End If

                Dim ocrText As String = OcrReader.ReadScreenText(lootScanFrame)
                If Not String.IsNullOrWhiteSpace(ocrText) AndAlso allowedNames IsNot Nothing Then
                    Dim matchedItem As String = ""
                    If TryFindAllowedLootMatch(ocrText, allowedNames, lootMatchThresholdPercent, matchedItem) Then
                        System.Media.SystemSounds.Exclamation.Play()
                        Console.Beep(800, 1000)
                        Console.Beep(800, 1000)
                        RaiseEvent LogLine($"LOOT ALARM: Found {matchedItem} (fuzzy {lootMatchThresholdPercent}%).")

                        If Not String.IsNullOrWhiteSpace(topic) Then
                            Task.Run(Async Function()
                                Try
                                    Using client As New System.Net.Http.HttpClient()
                                        Dim request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://ntfy.sh/" & Uri.EscapeDataString(topic))
                                        request.Content = New System.Net.Http.StringContent("Found important item: " & matchedItem)
                                        request.Headers.Add("Title", "KathanaBot Loot Finder")
                                        Await client.SendAsync(request)
                                    End Using
                                Catch ex As Exception
                                    RaiseEvent LogLine("Item Ntfy send failed: " & ex.Message)
                                End Try
                            End Function)
                        End If
                    End If
                End If
            Catch ex As Exception
                RaiseEvent LogLine("Loot scanner processing failed: " & ex.Message)
            Finally
                If lootScanFrame IsNot Nothing Then
                    lootScanFrame.Dispose()
                End If
                scanFrame.Dispose()
            End Try
        End Sub)

        SetLastAction("RMENU (scan items)")
        RaiseEvent LogLine("Auto right-alt scan processed from vision loop frame.")
        ReleaseLootScannerAltKey()
        _lootScannerCapturePending = False
        _lootScannerCaptureRequestedAt = DateTime.MinValue
    End Sub

    Private Sub ReleaseLootScannerAltKey()
        If Not _lootScannerAltHeld Then
            Return
        End If

        Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&H12), 0UI))
        Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1
        Dim KEYEVENTF_KEYUP As UInteger = &H2

        Try
            keybd_event(&HA5, scan, KEYEVENTF_EXTENDEDKEY Or KEYEVENTF_KEYUP, UIntPtr.Zero)
        Catch
        Finally
            _lootScannerAltHeld = False
        End Try
    End Sub

    Private Sub TryHandlePendingLootPickupVerification(cfg As BotConfig, hwnd As IntPtr, frame As Bitmap, now As DateTime, mobNameRegion As RectRegion)
        If _pendingLootPickupVerifyAt = DateTime.MinValue OrElse now < _pendingLootPickupVerifyAt Then
            Return
        End If

        _pendingLootPickupVerifyAt = DateTime.MinValue
        If frame Is Nothing OrElse hwnd = IntPtr.Zero Then
            RaiseEvent LogLine("Loot scan skipped: vision loop frame unavailable.")
            Return
        End If

        Try
            Dim selectedName As String = ReadMobNameIfNeeded(frame, mobNameRegion, now, True)
            If IsAllowedLootName(selectedName, cfg.LootAllowedNames, cfg.LootNameMatchThresholdPercent) Then
                SetLastAction($"F (loot accepted: {If(String.IsNullOrWhiteSpace(selectedName), "unknown", selectedName)})")
                Return
            End If

            Dim rejectedName As String = If(String.IsNullOrWhiteSpace(selectedName), "unknown", selectedName)
            Dim rejectContext As String = $"loot rejected: {rejectedName}"
            Dim preStopSent As Boolean = TrySendStopAction(cfg, hwnd, rejectContext & " (pre-stop)", includeMovementFallback:=True)
            Dim clickSent As Boolean = False
            Dim rejectHandled As Boolean = False

            If cfg.LootRejectClickEnabled AndAlso cfg.LootRejectPointX >= 0 AndAlso cfg.LootRejectPointY >= 0 Then
                Dim clickX As Integer = Math.Max(0, Math.Min(frame.Width - 1, cfg.LootRejectPointX))
                Dim clickY As Integer = Math.Max(0, Math.Min(frame.Height - 1, cfg.LootRejectPointY))
                For i As Integer = 1 To 2
                    If ClickClientPoint(hwnd, clickX, clickY, 0, 0) Then
                        clickSent = True
                    End If
                    Thread.Sleep(8)
                Next
                If clickSent Then
                    SetLastAction($"Click loot reject ({clickX},{clickY})")
                End If
            End If

            rejectHandled = TrySendStopAction(cfg, hwnd, rejectContext, includeMovementFallback:=False)

            If Not rejectHandled Then
                Dim stopSent As Boolean = False
                For i As Integer = 1 To 2
                    If SendKey(hwnd, "S", 50) Then
                        stopSent = True
                        MarkKeyUsed("S")
                    End If
                    Thread.Sleep(25)
                Next

                If stopSent Then
                    SetLastAction($"S (loot reject: {rejectedName})")
                    rejectHandled = True
                End If
            End If

            If Not rejectHandled Then
                rejectHandled = TrySendStopAction(cfg, hwnd, rejectContext, includeMovementFallback:=True)
            End If

            If Not (rejectHandled OrElse preStopSent OrElse clickSent) Then
                RaiseEvent LogLine($"Loot rejected ({rejectedName}) and cancel action failed to send.")
            End If
        Catch ex As Exception
            RaiseEvent LogLine("Loot scan error: " & ex.Message)
        End Try
    End Sub

    Private Function IsLikelyVisionCaptureGlitch(frame As Bitmap, hpRegion As RectRegion, mpRegion As RectRegion, hpPct As Double, mpPct As Double) As Boolean
        If frame Is Nothing Then
            Return True
        End If
        If IsLikelyBlackFrame(frame) Then
            Return True
        End If
        If IsHudRegionVeryDark(frame, hpRegion) AndAlso IsHudRegionVeryDark(frame, mpRegion) Then
            Return True
        End If

        Dim hasBaseline As Boolean = _lastGoodHpPercent >= 0 AndAlso _lastGoodMpPercent >= 0
        If hasBaseline AndAlso hpPct <= 0.25 AndAlso mpPct <= 0.25 AndAlso (_lastGoodHpPercent >= 5.0 OrElse _lastGoodMpPercent >= 5.0) Then
            Return True
        End If

        Return False
    End Function

    Private Function IsSuspiciousSingleResourceZero(currentPct As Double, lastGoodPct As Double, companionPct As Double, lastGoodCompanionPct As Double) As Boolean
        If currentPct > 0.25 Then
            Return False
        End If
        If lastGoodPct < 5.0 Then
            Return False
        End If

        Dim companionLooksHealthy As Boolean = companionPct >= 3.0
        Dim companionLooksStable As Boolean = lastGoodCompanionPct >= 0 AndAlso Math.Abs(companionPct - lastGoodCompanionPct) <= 35.0
        Return companionLooksHealthy OrElse companionLooksStable
    End Function

    Private Shared Function IsHudRegionVeryDark(frame As Bitmap, region As RectRegion) As Boolean
        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return True
        End If

        Dim stepX As Integer = Math.Max(1, rect.Width \ 14)
        Dim stepY As Integer = Math.Max(1, rect.Height \ 6)
        Dim samples As Integer = 0
        Dim brightSamples As Integer = 0
        Dim sumLuma As Long = 0

        For y As Integer = rect.Top To rect.Bottom - 1 Step stepY
            For x As Integer = rect.Left To rect.Right - 1 Step stepX
                Dim c As Color = frame.GetPixel(x, y)
                Dim luma As Integer = (CInt(c.R) * 30 + CInt(c.G) * 59 + CInt(c.B) * 11) \ 100
                samples += 1
                sumLuma += luma
                If luma >= 28 Then
                    brightSamples += 1
                End If
            Next
        Next

        If samples = 0 Then
            Return True
        End If

        Dim avgLuma As Double = sumLuma / CDbl(samples)
        Dim brightRatio As Double = brightSamples / CDbl(samples)
        Return avgLuma <= 15.0 AndAlso brightRatio <= 0.04
    End Function

    Private Sub ApplyVisionStabilityFilter(ByRef hpPct As Double, ByRef mpPct As Double, ByRef mobHpPct As Double, ByRef mobName As String, captureGlitch As Boolean)
        Dim hasBaseline As Boolean = _lastGoodHpPercent >= 0 AndAlso _lastGoodMpPercent >= 0
        Dim bothNearZero As Boolean = hpPct <= 0.25 AndAlso mpPct <= 0.25
        Dim suspiciousSingleHpZero As Boolean =
            hasBaseline AndAlso
            IsSuspiciousSingleResourceZero(hpPct, _lastGoodHpPercent, mpPct, _lastGoodMpPercent)
        Dim suspiciousSingleMpZero As Boolean =
            hasBaseline AndAlso
            IsSuspiciousSingleResourceZero(mpPct, _lastGoodMpPercent, hpPct, _lastGoodHpPercent)

        If suspiciousSingleHpZero Then
            _singleHpZeroConfirmCount += 1
        Else
            _singleHpZeroConfirmCount = 0
        End If

        If suspiciousSingleMpZero Then
            _singleMpZeroConfirmCount += 1
        Else
            _singleMpZeroConfirmCount = 0
        End If

        Dim sustainedSingleHpZero As Boolean = _singleHpZeroConfirmCount >= SustainedSingleZeroConfirmRequiredCount
        Dim sustainedSingleMpZero As Boolean = _singleMpZeroConfirmCount >= SustainedSingleZeroConfirmRequiredCount

        If sustainedSingleHpZero Then
            suspiciousSingleHpZero = False
        End If
        If sustainedSingleMpZero Then
            suspiciousSingleMpZero = False
        End If

        If bothNearZero Then
            _zeroPairConfirmCount += 1
        Else
            _zeroPairConfirmCount = 0
        End If

        Dim suspiciousZeroSpike As Boolean =
            hasBaseline AndAlso
            bothNearZero AndAlso
            (_lastGoodHpPercent >= 5.0 OrElse _lastGoodMpPercent >= 5.0) AndAlso
            _zeroPairConfirmCount < 12

        If captureGlitch OrElse suspiciousZeroSpike OrElse suspiciousSingleHpZero OrElse suspiciousSingleMpZero Then
            _zeroSpikeHoldCount += 1
            If hasBaseline Then
                hpPct = _lastGoodHpPercent
                mpPct = _lastGoodMpPercent
                If _lastGoodMobHpPercent >= 0 Then
                    mobHpPct = _lastGoodMobHpPercent
                End If
                If String.IsNullOrWhiteSpace(mobName) AndAlso Not String.IsNullOrWhiteSpace(_lastGoodMobName) Then
                    mobName = _lastGoodMobName
                End If
                Return
            End If
        Else
            _zeroSpikeHoldCount = 0
        End If

        _lastGoodHpPercent = hpPct
        _lastGoodMpPercent = mpPct
        _lastGoodMobHpPercent = mobHpPct
        If Not String.IsNullOrWhiteSpace(mobName) Then
            _lastGoodMobName = mobName
        End If
    End Sub

    Private Sub UpdateLevelingAgentState(cfg As BotConfig, state As LevelingAgentState, reason As String, Optional guardrailTriggered As Boolean = False)
        SyncLock _sync
            If cfg Is Nothing OrElse Not cfg.LevelingAgentEnabled Then
                _agentState = LevelingAgentState.Disabled
                _agentReason = ""
                _agentGuardrailTriggered = False
                Return
            End If

            _agentState = state
            _agentReason = If(reason, "").Trim()
            _agentGuardrailTriggered = guardrailTriggered
        End SyncLock
    End Sub

    Private Function ShouldTriggerLevelingGuardrail(cfg As BotConfig, hpPct As Double, mpPct As Double, expPerHour As Double, now As DateTime, targetWindowVisible As Boolean, ByRef guardrailReason As String) As Boolean
        guardrailReason = ""
        If cfg Is Nothing OrElse Not cfg.LevelingAgentEnabled Then
            Return False
        End If

        If hpPct <= Math.Max(1, cfg.LevelingStopHpPercent) Then
            guardrailReason = $"HP reached leveling stop threshold ({hpPct:0.0}% <= {cfg.LevelingStopHpPercent}%)."
            Return True
        End If

        If mpPct <= Math.Max(1, cfg.LevelingStopMpPercent) Then
            guardrailReason = $"MP reached leveling stop threshold ({mpPct:0.0}% <= {cfg.LevelingStopMpPercent}%)."
            Return True
        End If

        If cfg.LevelingStopOnLowExpRate AndAlso expPerHour >= 0 AndAlso expPerHour < Math.Max(0.01, cfg.LevelingMinExpPerHour) Then
            guardrailReason = $"EXP/hour fell below threshold ({expPerHour:0.00}%/hr < {cfg.LevelingMinExpPerHour:0.00}%/hr)."
            Return True
        End If

        If cfg.LevelingStopOnRepeatedUnreachable AndAlso _agentUnreachableEvents >= Math.Max(1, cfg.LevelingUnreachableLimit) Then
            guardrailReason = $"Unreachable target limit hit ({_agentUnreachableEvents}/{Math.Max(1, cfg.LevelingUnreachableLimit)})."
            Return True
        End If

        If cfg.LevelingMaxNoTargetSeconds > 0 AndAlso Not targetWindowVisible AndAlso _noTargetBeganAt <> DateTime.MinValue Then
            If (now - _noTargetBeganAt).TotalSeconds >= cfg.LevelingMaxNoTargetSeconds Then
                guardrailReason = $"No target detected for {Math.Round((now - _noTargetBeganAt).TotalSeconds, 1):0.0}s."
                Return True
            End If
        End If

        Return False
    End Function

    Private Sub TriggerLevelingGuardrailStop(cfg As BotConfig, reason As String)
        Dim snapshot As BotStatus
        SyncLock _sync
            _agentState = LevelingAgentState.GuardedStop
            _agentReason = If(reason, "").Trim()
            _agentGuardrailTriggered = True
            _status.Running = False
            _status.NotAttackingReason = _agentReason
            _status.ErrorMessage = ""
            _status.UpdatedAt = DateTime.UtcNow
            _status.AgentEnabled = cfg IsNot Nothing AndAlso cfg.LevelingAgentEnabled
            _status.AgentState = _agentState.ToString()
            _status.AgentReason = _agentReason
            _status.AgentGuardrailTriggered = True
            snapshot = CloneStatus(_status)
            If _cts IsNot Nothing AndAlso Not _cts.IsCancellationRequested Then
                _cts.Cancel()
            End If
        End SyncLock

        RaiseEvent LogLine("Leveling agent guardrail stop: " & reason)
        RaiseEvent StatusUpdated(snapshot)
    End Sub

    Private Sub UpdateLevelingAgentRuntimeState(cfg As BotConfig, now As DateTime, hpPct As Double, mpPct As Double, targetWindowVisible As Boolean, targetValid As Boolean, actionSent As Boolean, retargeting As Boolean, unreachableLockActive As Boolean, reason As String)
        If cfg Is Nothing OrElse Not cfg.LevelingAgentEnabled Then
            UpdateLevelingAgentState(cfg, LevelingAgentState.Disabled, "")
            Return
        End If

        If _agentGuardrailTriggered Then
            Return
        End If

        Dim nextState As LevelingAgentState
        Dim nextReason As String = If(reason, "").Trim()
        Dim lastActionText As String = ""
        SyncLock _sync
            lastActionText = If(_status.LastAction, "")
        End SyncLock

        If hpPct <= Math.Max(cfg.LevelingStopHpPercent + 5, 1) OrElse mpPct <= Math.Max(cfg.LevelingStopMpPercent + 5, 1) Then
            nextState = LevelingAgentState.Recovering
            If nextReason = "" Then
                nextReason = "HP/MP is near leveling guardrails."
            End If
        ElseIf _lastNavigationTravelActive AndAlso _lastNavigationTravelStalled Then
            nextState = LevelingAgentState.Stuck
            If nextReason = "" Then
                nextReason = If(String.IsNullOrWhiteSpace(_lastNavigationTravelReason), "Navigation travel stalled.", _lastNavigationTravelReason)
            End If
        ElseIf _lastNavigationTravelActive AndAlso Not targetWindowVisible AndAlso Not targetValid Then
            nextState = LevelingAgentState.Traveling
            If nextReason = "" Then
                nextReason = If(String.IsNullOrWhiteSpace(_lastNavigationTravelReason), "Traveling between route waypoints.", _lastNavigationTravelReason)
            End If
        ElseIf retargeting OrElse unreachableLockActive Then
            nextState = LevelingAgentState.Stuck
            If nextReason = "" Then
                nextReason = "Recovering from a stuck or unreachable target."
            End If
        ElseIf actionSent AndAlso lastActionText.IndexOf("loot", StringComparison.OrdinalIgnoreCase) >= 0 Then
            nextState = LevelingAgentState.Looting
            If nextReason = "" Then
                nextReason = "Processing loot."
            End If
        ElseIf targetValid AndAlso (actionSent OrElse (_lastAttackAction <> DateTime.MinValue AndAlso (now - _lastAttackAction).TotalMilliseconds < Math.Max(500, cfg.RetargetMs * 2))) Then
            nextState = LevelingAgentState.Fighting
            If nextReason = "" Then
                nextReason = "Target acquired and attack loop is active."
            End If
        ElseIf targetWindowVisible OrElse targetValid Then
            nextState = LevelingAgentState.Engaging
            If nextReason = "" Then
                nextReason = "Preparing to engage current target."
            End If
        Else
            nextState = LevelingAgentState.Searching
            If nextReason = "" Then
                nextReason = "Searching for a valid target."
            End If
        End If

        UpdateLevelingAgentState(cfg, nextState, nextReason)
    End Sub

    Private Sub ClearMapLocalizationRuntime()
        _lastMapCoordinateOcrAt = DateTime.MinValue
        _lastMapCoordinateText = ""
        _lastMapCoordinateX = -1
        _lastMapCoordinateY = -1
        _lastMapCoordinateConfidence = 0
        _lastMapMarkerScanAt = DateTime.MinValue
        _lastMapMarkerDetected = False
        _lastMapMarkerX = -1
        _lastMapMarkerY = -1
        _lastMapLocalizationConfidence = 0
        _lastMapVisible = False
        _navigationAwaitingLocalization = False
        _navigationLocalizationRetryAfter = DateTime.MinValue
        _navigationLocalizationFailureCount = 0
        _navigationLocalizationPaused = False
        _routeRecordingCaptureActive = False
        _routeRecordingMapName = ""
        _routeRecordingName = ""
        _routeRecordingStatus = ""
        _routeRecordingLastSampleAt = DateTime.MinValue
        _routeRecordingSamples.Clear()
    End Sub

    Private Sub ClearNavigationPreviewRuntime()
        _lastNavigationMapName = ""
        _lastNavigationCurrentNodeId = ""
        _lastNavigationCurrentNodeLabel = ""
        _lastNavigationNextWaypointId = ""
        _lastNavigationNextWaypointLabel = ""
        _lastNavigationRouteText = ""
        _lastNavigationRouteReady = False
    End Sub

    Private Sub ClearNavigationTravelRuntime()
        _lastNavigationTravelActive = False
        _lastNavigationTravelReason = ""
        _lastNavigationDistanceToWaypoint = -1
        _lastNavigationTravelStalled = False
        _lastNavigationRecoveryCount = 0
        _lastNavigationDestinationReached = False
        _lastNavigationDestinationLabel = ""
        _lastNavigationProgressWaypointId = ""
        _lastNavigationProgressDistance = -1
        _lastNavigationProgressAt = DateTime.MinValue
        _lastNavigationRecoveryAt = DateTime.MinValue
        _lastNavigationKnownPoseAt = DateTime.MinValue
        _lastNavigationKnownX = -1
        _lastNavigationKnownY = -1
        _lastNavigationPreviousX = -1
        _lastNavigationPreviousY = -1
        _lastNavigationKnownHeading = ""
        _navigationRotationQuarterTurns = 0
        _navigationRotationCandidateQuarterTurns = -1
        _navigationRotationCandidateCount = 0
        _lastNavigationRotationChangeAt = DateTime.MinValue
        _lastTravelInputKey = ""
        _lastTravelInputDesiredDirection = ""
        _lastTravelInputPoseX = -1
        _lastTravelInputPoseY = -1
        _lastTravelInputAt = DateTime.MinValue
        _navigationCommittedWaypointId = ""
        _navigationCommittedWaypointLabel = ""
        _lastNavigationMapToggleAt = DateTime.MinValue
        _lastNavigationMoveCommandAt = DateTime.MinValue
        _navigationMapExpectedOpen = False
        _navigationAwaitingLocalization = False
        _navigationLocalizationRetryAfter = DateTime.MinValue
        _navigationLocalizationFailureCount = 0
        _navigationLocalizationPaused = False
    End Sub

    Private Structure PartyListSummary
        Public Property Size As Integer
        Public Property AliveCount As Integer
        Public Property AllAlive As Boolean
    End Structure

    Private Structure PartyListBarBandInfo
        Public Property Top As Integer
        Public Property Bottom As Integer
        Public Property MaxPixels As Integer

        Public ReadOnly Property Height As Integer
            Get
                Return Math.Max(0, Bottom - Top + 1)
            End Get
        End Property
    End Structure

    Private Sub ReadMapCoordinateIfNeeded(frame As Bitmap, region As RectRegion, now As DateTime)
        If _lastMapCoordinateOcrAt <> DateTime.MinValue AndAlso (now - _lastMapCoordinateOcrAt).TotalMilliseconds < MapCoordinateOcrMinIntervalMs Then
            Return
        End If

        _lastMapCoordinateOcrAt = now
        If frame Is Nothing OrElse region Is Nothing Then
            _lastMapCoordinateText = ""
            _lastMapCoordinateX = -1
            _lastMapCoordinateY = -1
            _lastMapCoordinateConfidence = 0
            Return
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            _lastMapCoordinateText = ""
            _lastMapCoordinateX = -1
            _lastMapCoordinateY = -1
            _lastMapCoordinateConfidence = 0
            Return
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            Dim rawText As String = ReadMapCoordinateTextForOcr(crop)
            Dim x As Integer = -1
            Dim y As Integer = -1
            Dim confidence As Integer = 0
            Dim normalized As String = ""
            If TryParseMapCoordinate(rawText, x, y, normalized, confidence) Then
                _lastMapCoordinateText = normalized
                _lastMapCoordinateX = x
                _lastMapCoordinateY = y
                _lastMapCoordinateConfidence = confidence
            Else
                _lastMapCoordinateText = If(rawText, "").Trim()
                _lastMapCoordinateX = -1
                _lastMapCoordinateY = -1
                _lastMapCoordinateConfidence = 0
            End If
        End Using
    End Sub

    Private Shared Function ReadMapCoordinateTextForOcr(crop As Bitmap) As String
        If crop Is Nothing Then
            Return ""
        End If

        Using enlarged As New Bitmap(Math.Max(1, crop.Width * 3), Math.Max(1, crop.Height * 3), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(enlarged)
                g.Clear(Color.Black)
                g.InterpolationMode = InterpolationMode.NearestNeighbor
                g.PixelOffsetMode = PixelOffsetMode.Half
                g.DrawImage(crop, New Rectangle(0, 0, enlarged.Width, enlarged.Height), New Rectangle(0, 0, crop.Width, crop.Height), GraphicsUnit.Pixel)
            End Using

            Dim rawText As String = OcrReader.ReadScreenText(enlarged)
            If Regex.IsMatch(If(rawText, ""), "\d{3}\s*[/,]\s*\d{3}") Then
                Return rawText
            End If

            Using thresholded As New Bitmap(enlarged.Width, enlarged.Height, PixelFormat.Format24bppRgb)
                For y As Integer = 0 To enlarged.Height - 1
                    For x As Integer = 0 To enlarged.Width - 1
                        Dim px As Color = enlarged.GetPixel(x, y)
                        Dim luma As Integer = (CInt(px.R) * 30 + CInt(px.G) * 59 + CInt(px.B) * 11) \ 100
                        thresholded.SetPixel(x, y, If(luma >= 140, Color.White, Color.Black))
                    Next
                Next

                Dim thresholdText As String = OcrReader.ReadScreenText(thresholded)
                If Regex.IsMatch(If(thresholdText, ""), "\d{3}\s*[/,]\s*\d{3}") Then
                    Return thresholdText
                End If

                If String.IsNullOrWhiteSpace(rawText) Then
                    Return thresholdText
                End If

                Return rawText
            End Using
        End Using
    End Function

    Private Sub ReadChatTextIfNeeded(frame As Bitmap, region As RectRegion, cfg As BotConfig, now As DateTime)
        Dim minIntervalMs As Integer = Math.Max(250, If(cfg?.ChatTranslationScanIntervalMs, 700))
        If _lastChatOcrAt <> DateTime.MinValue AndAlso (now - _lastChatOcrAt).TotalMilliseconds < minIntervalMs Then
            Return
        End If

        _lastChatOcrAt = now
        If frame Is Nothing OrElse region Is Nothing Then
            ClearChatTranslationRuntime()
            Return
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            ClearChatTranslationRuntime()
            Return
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            Using enlarged As New Bitmap(Math.Max(1, crop.Width * 2), Math.Max(1, crop.Height * 2), PixelFormat.Format24bppRgb)
                Using g As Graphics = Graphics.FromImage(enlarged)
                    g.Clear(Color.Black)
                    g.InterpolationMode = InterpolationMode.NearestNeighbor
                    g.PixelOffsetMode = PixelOffsetMode.Half
                    g.DrawImage(crop, New Rectangle(0, 0, enlarged.Width, enlarged.Height), New Rectangle(0, 0, crop.Width, crop.Height), GraphicsUnit.Pixel)
                End Using

                Dim rawText As String = OcrReader.ReadScreenText(enlarged)
                Dim normalized As String = NormalizeChatOcrText(rawText)
                If normalized = "" Then
                    _lastChatOcrText = ""
                    _lastChatOcrNormalized = ""
                    Return
                End If

                If _lastChatOcrNormalized.Equals(normalized, StringComparison.Ordinal) Then
                    Return
                End If

                _lastChatOcrText = normalized
                _lastChatOcrNormalized = normalized
                _lastChatOcrUpdatedAt = now
            End Using
        End Using
    End Sub

    Private Sub ClearChatTranslationRuntime()
        _lastChatOcrText = ""
        _lastChatOcrNormalized = ""
        _lastChatOcrUpdatedAt = DateTime.MinValue
    End Sub

    Private Sub ReadPartyListIfNeeded(frame As Bitmap, region As RectRegion, now As DateTime)
        If _lastPartyListScanAt <> DateTime.MinValue AndAlso (now - _lastPartyListScanAt).TotalMilliseconds < PartyListScanMinIntervalMs Then
            Return
        End If

        _lastPartyListScanAt = now
        If frame Is Nothing OrElse region Is Nothing Then
            ClearPartyListRuntimeState()
            Return
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width < 40 OrElse rect.Height < 20 Then
            ClearPartyListRuntimeState()
            Return
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            Dim summary As PartyListSummary = AnalyzePartyListVisuals(crop)
            _lastPartySize = summary.Size
            _lastPartyAliveCount = summary.AliveCount
            _lastPartyAllAlive = summary.AllAlive
        End Using
    End Sub

    Private Sub ClearPartyListRuntimeState()
        _lastPartySize = 0
        _lastPartyAliveCount = 0
        _lastPartyAllAlive = False
    End Sub

    Private Shared Function AnalyzePartyListVisuals(crop As Bitmap) As PartyListSummary
        Dim summary As New PartyListSummary With {
            .Size = 0,
            .AliveCount = 0,
            .AllAlive = False
        }
        If crop Is Nothing OrElse crop.Width <= 0 OrElse crop.Height <= 0 Then
            Return summary
        End If

        Dim rowRed(crop.Height - 1) As Integer
        Dim rowBlue(crop.Height - 1) As Integer

        For y As Integer = 0 To crop.Height - 1
            Dim redCount As Integer = 0
            Dim blueCount As Integer = 0
            For x As Integer = 0 To crop.Width - 1
                Dim px As Color = crop.GetPixel(x, y)
                If IsPartyHpBarPixel(px) Then
                    redCount += 1
                ElseIf IsPartyMpBarPixel(px) Then
                    blueCount += 1
                End If
            Next

            rowRed(y) = redCount
            rowBlue(y) = blueCount
        Next

        Dim redBands As List(Of PartyListBarBandInfo) = FindPartyBarBands(rowRed, crop.Width)
        Dim blueBands As List(Of PartyListBarBandInfo) = FindPartyBarBands(rowBlue, crop.Width)
        summary.AliveCount = Math.Min(MaxPartyMembers, redBands.Count)
        summary.Size = Math.Min(MaxPartyMembers, Math.Max(redBands.Count, blueBands.Count))
        summary.AllAlive = summary.Size > 0 AndAlso summary.AliveCount >= summary.Size
        Return summary
    End Function

    Private Shared Function FindPartyBarBands(rowCounts() As Integer, width As Integer) As List(Of PartyListBarBandInfo)
        Dim bands As New List(Of PartyListBarBandInfo)()
        If rowCounts Is Nothing OrElse rowCounts.Length = 0 OrElse width <= 0 Then
            Return bands
        End If

        Dim minBarRowPixels As Integer = Math.Max(4, CInt(Math.Ceiling(width * 0.02R)))
        Dim maxBandHeight As Integer = Math.Max(6, CInt(Math.Ceiling(width * 0.04R)))
        Dim currentTop As Integer = -1
        Dim currentBottom As Integer = -1
        Dim currentMax As Integer = 0
        Dim gapRows As Integer = 0

        For y As Integer = 0 To rowCounts.Length - 1
            If rowCounts(y) >= minBarRowPixels Then
                If currentTop < 0 Then
                    currentTop = y
                End If

                currentBottom = y
                currentMax = Math.Max(currentMax, rowCounts(y))
                gapRows = 0
            ElseIf currentTop >= 0 Then
                gapRows += 1
                If gapRows > PartyListBarBandGapRows Then
                    bands.Add(New PartyListBarBandInfo With {
                        .Top = currentTop,
                        .Bottom = currentBottom,
                        .MaxPixels = currentMax
                    })
                    currentTop = -1
                    currentBottom = -1
                    currentMax = 0
                    gapRows = 0
                End If
            End If
        Next

        If currentTop >= 0 Then
            bands.Add(New PartyListBarBandInfo With {
                .Top = currentTop,
                .Bottom = currentBottom,
                .MaxPixels = currentMax
            })
        End If

        Return bands.
            Where(Function(band) band.MaxPixels >= minBarRowPixels AndAlso
                                 band.Height <= maxBandHeight AndAlso
                                 band.MaxPixels >= (band.Height * 2)).
            OrderBy(Function(band) band.Top).
            Take(MaxPartyMembers).
            ToList()
    End Function

    Private Shared Function IsPartyHpBarPixel(px As Color) As Boolean
        Return px.R >= 90 AndAlso px.R >= (px.G + 16) AndAlso px.R >= (px.B + 16)
    End Function

    Private Shared Function IsPartyMpBarPixel(px As Color) As Boolean
        Return px.B >= 90 AndAlso px.B >= (px.R + 12) AndAlso px.B >= (px.G + 8)
    End Function

    Private Shared Function NormalizeChatOcrText(rawText As String) As String
        Dim cleanedLines As New List(Of String)()
        Dim source As String = If(rawText, "")
        For Each rawLine As String In source.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim line As String = Regex.Replace(rawLine, "\s+", " ").Trim()
            If line.Length < 2 Then
                Continue For
            End If
            cleanedLines.Add(line)
        Next

        Dim deduped As New List(Of String)()
        Dim previous As String = ""
        For Each line As String In cleanedLines
            If previous.Equals(line, StringComparison.OrdinalIgnoreCase) Then
                Continue For
            End If
            deduped.Add(line)
            previous = line
        Next

        Return String.Join(Environment.NewLine, deduped.Take(8))
    End Function

    Private Sub ScanMapPlayerMarkerIfNeeded(now As DateTime)
        If _lastMapMarkerScanAt <> DateTime.MinValue AndAlso (now - _lastMapMarkerScanAt).TotalMilliseconds < MapMarkerScanMinIntervalMs Then
            Return
        End If

        _lastMapMarkerScanAt = now
        _lastMapMarkerDetected = False
        _lastMapMarkerX = -1
        _lastMapMarkerY = -1

        If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 Then
            Return
        End If

        If _lastMapCoordinateConfidence < 70 Then
            Return
        End If

        ' Marker state is derived from the parsed XXX/YYY map coordinates.
        _lastMapMarkerDetected = True
        _lastMapMarkerX = _lastMapCoordinateX
        _lastMapMarkerY = _lastMapCoordinateY
    End Sub

    Private Sub UpdateMapLocalizationConfidence()
        Dim confidence As Integer = 0
        confidence += Math.Max(0, Math.Min(100, _lastMapCoordinateConfidence))
        _lastMapLocalizationConfidence = Math.Max(0, Math.Min(100, confidence))
    End Sub

    Private Sub UpdateMapVisibleState()
        Dim strongCoordinate As Boolean = _lastMapCoordinateConfidence >= 70 AndAlso _lastMapCoordinateX >= 0 AndAlso _lastMapCoordinateY >= 0
        _lastMapVisible = strongCoordinate OrElse (_lastMapLocalizationConfidence >= 80)
    End Sub

    Private Function IsNavigationMapOpen(now As DateTime) As Boolean
        If _lastMapVisible Then
            _navigationMapExpectedOpen = True
            Return True
        End If

        If _navigationMapExpectedOpen Then
            If _lastNavigationMapToggleAt <> DateTime.MinValue AndAlso (now - _lastNavigationMapToggleAt).TotalMilliseconds < NavigationMapSampleWindowMs Then
                Return True
            End If
            _navigationMapExpectedOpen = False
        End If

        Return False
    End Function

    Private Sub UpdateLastKnownNavigationPose(now As DateTime)
        If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 Then
            If _lastTravelInputAt <> DateTime.MinValue AndAlso (now - _lastTravelInputAt).TotalMilliseconds > Math.Max(2000, If(_config Is Nothing, 1800, _config.NavigationResampleIntervalMs * 2)) Then
                ClearPendingNavigationTravelInput()
            End If
            Return
        End If
        If _lastMapLocalizationConfidence < 45 Then
            Return
        End If

        ObserveNavigationOrientation(now, _lastMapCoordinateX, _lastMapCoordinateY)

        If _lastNavigationKnownX >= 0 AndAlso _lastNavigationKnownY >= 0 Then
            _lastNavigationPreviousX = _lastNavigationKnownX
            _lastNavigationPreviousY = _lastNavigationKnownY
            Dim inferredHeading As String = InferHeadingFromCoordinateDelta(_lastNavigationKnownX, _lastNavigationKnownY, _lastMapCoordinateX, _lastMapCoordinateY)
            If Not String.IsNullOrWhiteSpace(inferredHeading) Then
                _lastNavigationKnownHeading = inferredHeading
            End If
        End If

        _lastNavigationKnownPoseAt = now
        _lastNavigationKnownX = _lastMapCoordinateX
        _lastNavigationKnownY = _lastMapCoordinateY
        _navigationAwaitingLocalization = False
        _navigationLocalizationRetryAfter = DateTime.MinValue
        _navigationLocalizationFailureCount = 0
        _navigationLocalizationPaused = False
    End Sub

    Private Sub ObserveNavigationOrientation(now As DateTime, newX As Integer, newY As Integer)
        If _lastTravelInputAt = DateTime.MinValue OrElse String.IsNullOrWhiteSpace(_lastTravelInputKey) Then
            Return
        End If

        Dim timeoutMs As Integer = Math.Max(1000, If(_config Is Nothing, 1800, _config.NavigationResampleIntervalMs * 2))
        If (now - _lastTravelInputAt).TotalMilliseconds > timeoutMs Then
            ClearPendingNavigationTravelInput()
            Return
        End If

        If _lastTravelInputPoseX < 0 OrElse _lastTravelInputPoseY < 0 Then
            Return
        End If

        If newX = _lastTravelInputPoseX AndAlso newY = _lastTravelInputPoseY Then
            Return
        End If

        Dim actualDirection As String = InferHeadingFromCoordinateDelta(_lastTravelInputPoseX, _lastTravelInputPoseY, newX, newY)
        If actualDirection = "" Then
            Return
        End If

        Dim defaultDirection As String = GetDefaultDirectionForKey(_lastTravelInputKey)
        Dim defaultIndex As Integer = CardinalDirectionIndex(defaultDirection)
        Dim actualIndex As Integer = CardinalDirectionIndex(actualDirection)
        If defaultIndex >= 0 AndAlso actualIndex >= 0 Then
            Dim observedRotation As Integer = (actualIndex - defaultIndex + 4) Mod 4
            If observedRotation = _navigationRotationQuarterTurns Then
                _navigationRotationCandidateQuarterTurns = -1
                _navigationRotationCandidateCount = 0
            ElseIf _lastNavigationRotationChangeAt <> DateTime.MinValue AndAlso (now - _lastNavigationRotationChangeAt).TotalMilliseconds < NavigationRotationChangeCooldownMs Then
                ' Hold the current mapping briefly so a single noisy sample does not jerk travel.
            Else
                If _navigationRotationCandidateQuarterTurns <> observedRotation Then
                    _navigationRotationCandidateQuarterTurns = observedRotation
                    _navigationRotationCandidateCount = 1
                Else
                    _navigationRotationCandidateCount += 1
                End If

                If _navigationRotationCandidateCount >= NavigationRotationConfirmationsRequired Then
                    _navigationRotationQuarterTurns = observedRotation
                    _lastNavigationRotationChangeAt = now
                    _navigationRotationCandidateQuarterTurns = -1
                    _navigationRotationCandidateCount = 0
                End If
            End If
        End If

        _lastNavigationKnownHeading = actualDirection
        ClearPendingNavigationTravelInput()
    End Sub

    Private Sub ClearPendingNavigationTravelInput()
        _lastTravelInputKey = ""
        _lastTravelInputDesiredDirection = ""
        _lastTravelInputPoseX = -1
        _lastTravelInputPoseY = -1
        _lastTravelInputAt = DateTime.MinValue
    End Sub

    Private Sub UpdateRouteRecording(cfg As BotConfig, now As DateTime)
        If cfg Is Nothing OrElse Not cfg.NavigationEnabled Then
            _routeRecordingCaptureActive = False
            If _routeRecordingSamples.Count = 0 Then
                _routeRecordingStatus = ""
            End If
            Return
        End If

        Dim desiredMapName As String = NormalizeNavigationMapName(cfg.NavigationMapName)
        Dim desiredRouteName As String = NormalizeRecordedRouteName(cfg.RouteRecordingName)
        If cfg.RouteRecordingEnabled Then
            If (Not _routeRecordingCaptureActive) OrElse
               (Not _routeRecordingMapName.Equals(desiredMapName, StringComparison.OrdinalIgnoreCase)) OrElse
               (Not _routeRecordingName.Equals(desiredRouteName, StringComparison.OrdinalIgnoreCase)) Then
                _routeRecordingCaptureActive = True
                _routeRecordingMapName = desiredMapName
                _routeRecordingName = desiredRouteName
                _routeRecordingStatus = $"Recording route '{_routeRecordingName}' on {If(_routeRecordingMapName = "", "current map", _routeRecordingMapName)}."
                _routeRecordingLastSampleAt = DateTime.MinValue
                _routeRecordingSamples.Clear()
            End If

            TryAppendRouteRecordingSample(cfg, now)
            Return
        End If

        If _routeRecordingCaptureActive Then
            _routeRecordingCaptureActive = False
            If _routeRecordingSamples.Count > 0 Then
                _routeRecordingStatus = $"Recording paused with {_routeRecordingSamples.Count} samples for '{_routeRecordingName}'. Save to reuse this path."
            Else
                _routeRecordingStatus = "Route recording idle."
            End If
        End If
    End Sub

    Private Sub TryAppendRouteRecordingSample(cfg As BotConfig, now As DateTime)
        If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 OrElse _lastMapCoordinateConfidence < 70 Then
            Return
        End If
        Dim effectiveSampleIntervalMs As Integer = Math.Max(100, RouteRecordingMinSampleIntervalMs \ 2)
        If _routeRecordingLastSampleAt <> DateTime.MinValue AndAlso (now - _routeRecordingLastSampleAt).TotalMilliseconds < effectiveSampleIntervalMs Then
            Return
        End If

        Dim minDistance As Double = Math.Max(1, cfg.RouteRecordingMinSampleDistance / 2.0)
        If _routeRecordingSamples.Count > 0 Then
            Dim lastSample As NavigationRouteSample = _routeRecordingSamples(_routeRecordingSamples.Count - 1)
            If CalculateDistance(lastSample.X, lastSample.Y, _lastMapCoordinateX, _lastMapCoordinateY) < minDistance Then
                Return
            End If
        End If

        _routeRecordingSamples.Add(New NavigationRouteSample With {
            .X = _lastMapCoordinateX,
            .Y = _lastMapCoordinateY,
            .CapturedAtUtc = now
        })
        _routeRecordingLastSampleAt = now
        _routeRecordingStatus = $"Recording route '{_routeRecordingName}': {_routeRecordingSamples.Count} samples."
    End Sub

    Public Function SaveRecordedRoute(Optional cfg As BotConfig = Nothing) As String
        Dim routeName As String = ""
        Dim mapName As String = ""
        Dim minNodeSpacing As Integer = 28
        Dim samples As List(Of NavigationRouteSample)

        SyncLock _sync
            Dim effectiveCfg As BotConfig = If(cfg, _config)
            routeName = NormalizeRecordedRouteName(If(effectiveCfg Is Nothing, "", effectiveCfg.RouteRecordingName))
            mapName = NormalizeNavigationMapName(If(effectiveCfg Is Nothing, "", effectiveCfg.NavigationMapName))
            minNodeSpacing = Math.Max(8, If(effectiveCfg Is Nothing, 28, effectiveCfg.RouteRecordingMinNodeSpacing))
            samples = _routeRecordingSamples.Select(Function(sample) New NavigationRouteSample With {
                .X = sample.X,
                .Y = sample.Y,
                .CapturedAtUtc = sample.CapturedAtUtc
            }).ToList()
        End SyncLock

        If samples.Count < RouteRecordingMinSamplesToSave Then
            SyncLock _sync
                _routeRecordingStatus = $"Not enough samples to save route '{routeName}'. Walk the path with the map coordinates visible first."
            End SyncLock
            Return ""
        End If

        Dim graph As RecordedNavigationGraph = BuildRecordedNavigationGraph(mapName, routeName, samples, minNodeSpacing)
        If graph Is Nothing OrElse graph.Nodes.Count < 2 OrElse graph.Edges.Count = 0 Then
            SyncLock _sync
                _routeRecordingStatus = $"Unable to build a reusable route graph from '{routeName}'."
            End SyncLock
            Return ""
        End If

        Dim savedPath As String = SaveRecordedNavigationGraph(graph)
        If savedPath <> "" Then
            SyncLock _sync
                _routeRecordingLastSavedPath = savedPath
                _routeRecordingStatus = $"Saved route '{routeName}' with {graph.Nodes.Count} nodes."
            End SyncLock
        End If

        Return savedPath
    End Function

    Private Shared Function BuildRecordedNavigationGraph(mapName As String, routeName As String, samples As List(Of NavigationRouteSample), minNodeSpacing As Integer) As RecordedNavigationGraph
        If samples Is Nothing OrElse samples.Count < 2 Then
            Return Nothing
        End If

        Dim simplified As List(Of NavigationRouteSample) = SimplifyRecordedRouteSamples(samples, minNodeSpacing)
        If simplified.Count < 2 Then
            Return Nothing
        End If

        Dim graph As New RecordedNavigationGraph With {
            .MapName = NormalizeNavigationMapName(mapName),
            .RouteName = NormalizeRecordedRouteName(routeName),
            .Samples = samples.Select(Function(sample) New NavigationRouteSample With {
                .X = sample.X,
                .Y = sample.Y,
                .CapturedAtUtc = sample.CapturedAtUtc
            }).ToList(),
            .SavedAtUtc = DateTime.UtcNow
        }

        Dim prefix As String = $"recorded_{SanitizeIdentifier(graph.RouteName)}"
        For i As Integer = 0 To simplified.Count - 1
            Dim sample As NavigationRouteSample = simplified(i)
            Dim coordinateSuffix As String = $" {sample.X:000}/{sample.Y:000}"
            Dim label As String
            If i = 0 Then
                label = $"{graph.RouteName} Start{coordinateSuffix}"
            ElseIf i = simplified.Count - 1 Then
                label = $"{graph.RouteName} End{coordinateSuffix}"
            Else
                label = $"{graph.RouteName} {i:00}{coordinateSuffix}"
            End If

            graph.Nodes.Add(New NavigationNode With {
                .Id = $"{prefix}_{i:000}",
                .MapName = graph.MapName,
                .X = sample.X,
                .Y = sample.Y,
                .Label = label,
                .Tags = New List(Of String) From {"recorded", graph.RouteName}
            })
        Next

        graph.StartNodeId = graph.Nodes(0).Id
        graph.EndNodeId = graph.Nodes(graph.Nodes.Count - 1).Id
        For i As Integer = 0 To graph.Nodes.Count - 2
            Dim fromNode As NavigationNode = graph.Nodes(i)
            Dim toNode As NavigationNode = graph.Nodes(i + 1)
            Dim cost As Double = Math.Max(0.01, CalculateDistance(fromNode.X, fromNode.Y, toNode.X, toNode.Y))
            graph.Edges.Add(New NavigationEdge With {.FromNodeId = fromNode.Id, .ToNodeId = toNode.Id, .Cost = cost, .TravelMode = "walk", .Notes = graph.RouteName})
            graph.Edges.Add(New NavigationEdge With {.FromNodeId = toNode.Id, .ToNodeId = fromNode.Id, .Cost = cost, .TravelMode = "walk", .Notes = graph.RouteName})
        Next

        Return graph
    End Function

    Private Shared Function SimplifyRecordedRouteSamples(samples As List(Of NavigationRouteSample), minNodeSpacing As Integer) As List(Of NavigationRouteSample)
        Dim result As New List(Of NavigationRouteSample)()
        If samples Is Nothing OrElse samples.Count = 0 Then
            Return result
        End If

        Dim minSpacing As Double = Math.Max(6, minNodeSpacing)
        result.Add(samples(0))
        For i As Integer = 1 To samples.Count - 2
            Dim candidate As NavigationRouteSample = samples(i)
            Dim lastKept As NavigationRouteSample = result(result.Count - 1)
            If CalculateDistance(lastKept.X, lastKept.Y, candidate.X, candidate.Y) >= minSpacing Then
                result.Add(candidate)
            End If
        Next

        Dim finalSample As NavigationRouteSample = samples(samples.Count - 1)
        Dim lastResult As NavigationRouteSample = result(result.Count - 1)
        If CalculateDistance(lastResult.X, lastResult.Y, finalSample.X, finalSample.Y) >= Math.Max(3, minSpacing / 2.0) Then
            result.Add(finalSample)
        Else
            result(result.Count - 1) = finalSample
        End If

        Return result
    End Function

    Private Shared Function SaveRecordedNavigationGraph(graph As RecordedNavigationGraph) As String
        If graph Is Nothing OrElse graph.Nodes.Count = 0 Then
            Return ""
        End If

        Try
            Dim mapDirectory As String = Path.Combine(NavigationRouteStorageRoot, SanitizeIdentifier(graph.MapName))
            Directory.CreateDirectory(mapDirectory)
            Dim filePath As String = Path.Combine(mapDirectory, SanitizeIdentifier(graph.RouteName) & ".json")
            Dim json As String = JsonSerializer.Serialize(graph, NavigationRouteJsonOptions)
            File.WriteAllText(filePath, json, Encoding.UTF8)
            InvalidateRecordedGraphCache(graph.MapName)
            Return filePath
        Catch
            Return ""
        End Try
    End Function

    Private Shared Function GetRecordedNavigationGraphPath(mapName As String, routeName As String) As String
        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        Dim normalizedRoute As String = NormalizeRecordedRouteName(routeName)
        Dim mapDirectory As String = Path.Combine(NavigationRouteStorageRoot, SanitizeIdentifier(normalizedMap))
        Return Path.Combine(mapDirectory, SanitizeIdentifier(normalizedRoute) & ".json")
    End Function

    Private Shared Function LoadRecordedNavigationGraphs(mapName As String) As List(Of RecordedNavigationGraph)
        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        SyncLock _recordedGraphCacheSync
            If _recordedGraphCache.ContainsKey(normalizedMap) Then
                Return _recordedGraphCache(normalizedMap).Select(Function(graph) graph).ToList()
            End If
        End SyncLock

        Dim loaded As New List(Of RecordedNavigationGraph)()
        Try
            Dim mapDirectory As String = Path.Combine(NavigationRouteStorageRoot, SanitizeIdentifier(normalizedMap))
            If Directory.Exists(mapDirectory) Then
                For Each filePath As String In Directory.GetFiles(mapDirectory, "*.json")
                    Dim raw As String = File.ReadAllText(filePath, Encoding.UTF8)
                    Dim graph As RecordedNavigationGraph = JsonSerializer.Deserialize(Of RecordedNavigationGraph)(raw)
                    If graph IsNot Nothing AndAlso graph.Nodes IsNot Nothing AndAlso graph.Nodes.Count > 0 Then
                        loaded.Add(graph)
                    End If
                Next
            End If
        Catch
        End Try

        SyncLock _recordedGraphCacheSync
            _recordedGraphCache(normalizedMap) = loaded
        End SyncLock
        Return loaded.Select(Function(graph) graph).ToList()
    End Function

    Public Shared Sub InvalidateRecordedGraphCache(Optional mapName As String = Nothing)
        SyncLock _recordedGraphCacheSync
            If String.IsNullOrWhiteSpace(mapName) Then
                _recordedGraphCache.Clear()
            Else
                _recordedGraphCache.Remove(NormalizeNavigationMapName(mapName))
            End If
        End SyncLock
    End Sub

    Public Shared Function GetRecordedRouteOptions(Optional mapName As String = "Jina Basin") As List(Of RecordedNavigationRouteInfo)
        Return LoadRecordedNavigationGraphs(mapName).
            Where(Function(graph) graph IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(graph.RouteName)).
            Select(Function(graph) New RecordedNavigationRouteInfo With {
                .MapName = NormalizeNavigationMapName(graph.MapName),
                .RouteName = graph.RouteName,
                .NodeCount = If(graph.Nodes Is Nothing, 0, graph.Nodes.Count),
                .SavedAtUtc = graph.SavedAtUtc
            }).
            OrderBy(Function(info) info.RouteName).
            ToList()
    End Function

    Public Shared Function GetRecordedRouteNodeOptions(routeName As String, Optional mapName As String = "Jina Basin") As List(Of NavigationNode)
        Dim normalizedRouteName As String = NormalizeRecordedRouteName(routeName)
        Dim graph As RecordedNavigationGraph = GetRecordedGraphByRouteName(normalizedRouteName, mapName)
        If graph Is Nothing OrElse graph.Nodes Is Nothing Then
            Return New List(Of NavigationNode)()
        End If

        Return graph.Nodes.Where(Function(node) node IsNot Nothing).
            Select(Function(node) New NavigationNode With {
                .Id = node.Id,
                .MapName = node.MapName,
                .X = node.X,
                .Y = node.Y,
                .Label = node.Label,
                .Tags = If(node.Tags, New List(Of String)()).ToList()
            }).
            ToList()
    End Function

    Public Shared Function GetRecordedRouteEndNode(routeName As String, Optional mapName As String = "Jina Basin") As NavigationNode
        Dim graph As RecordedNavigationGraph = GetRecordedGraphByRouteName(routeName, mapName)
        If graph Is Nothing OrElse graph.Nodes Is Nothing OrElse graph.Nodes.Count = 0 Then
            Return Nothing
        End If

        Dim node As NavigationNode = graph.Nodes.FirstOrDefault(Function(candidate) candidate IsNot Nothing AndAlso candidate.Id.Equals(graph.EndNodeId, StringComparison.OrdinalIgnoreCase))
        If node Is Nothing Then
            node = graph.Nodes(graph.Nodes.Count - 1)
        End If
        If node Is Nothing Then
            Return Nothing
        End If

        Return New NavigationNode With {
            .Id = node.Id,
            .MapName = node.MapName,
            .X = node.X,
            .Y = node.Y,
            .Label = node.Label,
            .Tags = If(node.Tags, New List(Of String)()).ToList()
        }
    End Function

    Public Shared Function DeleteRecordedRoute(routeName As String, Optional mapName As String = "Jina Basin") As Boolean
        If String.IsNullOrWhiteSpace(routeName) Then
            Return False
        End If

        Dim path As String = GetRecordedNavigationGraphPath(mapName, routeName)
        Try
            If File.Exists(path) Then
                File.Delete(path)
            End If
            InvalidateRecordedGraphCache(mapName)
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Shared Function DeleteRecordedRouteNode(routeName As String, nodeId As String, Optional mapName As String = "Jina Basin") As Boolean
        If String.IsNullOrWhiteSpace(routeName) OrElse String.IsNullOrWhiteSpace(nodeId) Then
            Return False
        End If

        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        Dim normalizedRoute As String = NormalizeRecordedRouteName(routeName)
        Dim graph As RecordedNavigationGraph = GetRecordedGraphByRouteName(normalizedRoute, normalizedMap)
        If graph Is Nothing OrElse graph.Nodes Is Nothing Then
            Return False
        End If

        Dim remainingNodes As List(Of NavigationNode) = graph.Nodes.
            Where(Function(node) node IsNot Nothing AndAlso Not node.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase)).
            Select(Function(node) New NavigationNode With {
                .Id = node.Id,
                .MapName = node.MapName,
                .X = node.X,
                .Y = node.Y,
                .Label = node.Label,
                .Tags = If(node.Tags, New List(Of String)()).ToList()
            }).
            ToList()

        If remainingNodes.Count = graph.Nodes.Count Then
            Return False
        End If

        If remainingNodes.Count < 2 Then
            Return DeleteRecordedRoute(normalizedRoute, normalizedMap)
        End If

        graph.Nodes = remainingNodes
        graph.StartNodeId = graph.Nodes(0).Id
        graph.EndNodeId = graph.Nodes(graph.Nodes.Count - 1).Id
        graph.Edges = New List(Of NavigationEdge)()
        For i As Integer = 0 To graph.Nodes.Count - 2
            Dim fromNode As NavigationNode = graph.Nodes(i)
            Dim toNode As NavigationNode = graph.Nodes(i + 1)
            Dim cost As Double = Math.Max(0.01, CalculateDistance(fromNode.X, fromNode.Y, toNode.X, toNode.Y))
            graph.Edges.Add(New NavigationEdge With {.FromNodeId = fromNode.Id, .ToNodeId = toNode.Id, .Cost = cost, .TravelMode = "walk", .Notes = graph.RouteName})
            graph.Edges.Add(New NavigationEdge With {.FromNodeId = toNode.Id, .ToNodeId = fromNode.Id, .Cost = cost, .TravelMode = "walk", .Notes = graph.RouteName})
        Next

        Dim savedPath As String = SaveRecordedNavigationGraph(graph)
        Return savedPath <> ""
    End Function

    Private Shared Function GetRecordedGraphByRouteName(routeName As String, mapName As String) As RecordedNavigationGraph
        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        Dim normalizedRoute As String = NormalizeRecordedRouteName(routeName)
        Return LoadRecordedNavigationGraphs(normalizedMap).
            FirstOrDefault(Function(candidate) candidate IsNot Nothing AndAlso candidate.RouteName.Equals(normalizedRoute, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Function NormalizeNavigationMapName(rawMapName As String) As String
        Dim normalized As String = If(rawMapName, "").Trim()
        If normalized = "" Then
            Return "Jina Basin"
        End If
        Return normalized
    End Function

    Private Shared Function NormalizeRecordedRouteName(rawRouteName As String) As String
        Dim normalized As String = If(rawRouteName, "").Trim()
        If normalized = "" Then
            Return "recorded_route"
        End If
        Return normalized
    End Function

    Private Shared Function SanitizeIdentifier(rawValue As String) As String
        Dim cleaned As String = Regex.Replace(If(rawValue, "").Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim("_"c)
        If cleaned = "" Then
            Return "route"
        End If
        Return cleaned
    End Function

    Private Function BuildNavigationPlan(cfg As BotConfig, now As DateTime, allowStaleLocalization As Boolean) As NavigationPlan
        Dim plan As New NavigationPlan()
        If cfg Is Nothing OrElse Not cfg.NavigationEnabled Then
            plan.StatusText = "Navigation is disabled."
            Return plan
        End If

        plan.MapName = If(String.IsNullOrWhiteSpace(cfg.NavigationMapName), "Jina Basin", cfg.NavigationMapName.Trim())
        Dim nodes As List(Of NavigationNode) = GetNavigationNodesForMap(plan.MapName)
        Dim edges As List(Of NavigationEdge) = GetNavigationEdgesForMap(plan.MapName)
        If nodes.Count = 0 OrElse edges.Count = 0 Then
            plan.StatusText = "No recorded routes loaded for this map."
            Return plan
        End If

        Dim poseX As Integer = -1
        Dim poseY As Integer = -1
        If _lastMapCoordinateX >= 0 AndAlso _lastMapCoordinateY >= 0 AndAlso _lastMapLocalizationConfidence >= 45 Then
            poseX = _lastMapCoordinateX
            poseY = _lastMapCoordinateY
        ElseIf allowStaleLocalization AndAlso _lastNavigationKnownPoseAt <> DateTime.MinValue AndAlso (now - _lastNavigationKnownPoseAt).TotalMilliseconds <= NavigationKnownPoseMaxAgeMs Then
            poseX = _lastNavigationKnownX
            poseY = _lastNavigationKnownY
        End If

        If poseX >= 0 AndAlso poseY >= 0 Then
            plan.CurrentNode = FindNearestNode(nodes, poseX, poseY)
        End If

        If plan.CurrentNode IsNot Nothing Then
            plan.StartNode = plan.CurrentNode
        ElseIf Not String.IsNullOrWhiteSpace(cfg.NavigationStartNodeId) Then
            plan.StartNode = FindNodeById(nodes, cfg.NavigationStartNodeId)
        End If

        plan.TargetNode = FindNodeById(nodes, cfg.NavigationTargetNodeId)
        If plan.StartNode Is Nothing OrElse plan.TargetNode Is Nothing Then
            plan.StatusText = "Waiting for a selected recorded route destination."
            Return plan
        End If

        plan.Route = FindShortestRoute(nodes, edges, plan.StartNode.Id, plan.TargetNode.Id)
        If plan.Route.Count = 0 Then
            plan.StatusText = $"No route found from {plan.StartNode.Label} to {plan.TargetNode.Label}."
            Return plan
        End If

        Dim proposedNextWaypoint As NavigationNode = If(plan.Route.Count > 1, plan.Route(1), plan.Route(0))
        If Not String.IsNullOrWhiteSpace(_navigationCommittedWaypointId) Then
            Dim committedWaypoint As NavigationNode = FindNodeById(nodes, _navigationCommittedWaypointId)
            If committedWaypoint IsNot Nothing AndAlso Not IsExactNavigationNodeMatch(committedWaypoint) Then
                plan.NextWaypoint = committedWaypoint
            Else
                _navigationCommittedWaypointId = ""
                _navigationCommittedWaypointLabel = ""
            End If
        End If
        If plan.NextWaypoint Is Nothing Then
            plan.NextWaypoint = proposedNextWaypoint
        End If
        If poseX >= 0 AndAlso poseY >= 0 AndAlso plan.TargetNode IsNot Nothing Then
            plan.DistanceToTarget = CalculateDistance(poseX, poseY, plan.TargetNode.X, plan.TargetNode.Y)
        End If
        If poseX >= 0 AndAlso poseY >= 0 AndAlso plan.NextWaypoint IsNot Nothing Then
            plan.DistanceToNextWaypoint = CalculateDistance(poseX, poseY, plan.NextWaypoint.X, plan.NextWaypoint.Y)
        End If
        plan.RouteReady = True
        plan.StatusText = String.Join(" -> ", plan.Route.Select(Function(node) node.Label))
        Return plan
    End Function

    Private Sub UpdateNavigationPreview(cfg As BotConfig, now As DateTime)
        If cfg Is Nothing OrElse Not cfg.NavigationEnabled OrElse Not cfg.NavigationTravelPreviewEnabled Then
            ClearNavigationPreviewRuntime()
            Return
        End If

        Dim plan As NavigationPlan = BuildNavigationPlan(cfg, now, allowStaleLocalization:=True)
        ClearNavigationPreviewRuntime()
        _lastNavigationMapName = plan.MapName
        _lastNavigationCurrentNodeId = If(plan.CurrentNode Is Nothing, "", plan.CurrentNode.Id)
        _lastNavigationCurrentNodeLabel = If(plan.CurrentNode Is Nothing, "", plan.CurrentNode.Label)

        If Not plan.RouteReady Then
            _lastNavigationRouteText = plan.StatusText
            Return
        End If

        _lastNavigationNextWaypointId = If(plan.NextWaypoint Is Nothing, "", plan.NextWaypoint.Id)
        _lastNavigationNextWaypointLabel = If(plan.NextWaypoint Is Nothing, "", plan.NextWaypoint.Label)
        _lastNavigationRouteText = plan.StatusText
        _lastNavigationRouteReady = True
    End Sub

    Public Shared Function GetNavigationNodeOptions(Optional mapName As String = "Jina Basin") As List(Of NavigationNode)
        Return GetNavigationNodesForMap(mapName)
    End Function

    Private Shared Function GetNavigationNodesForMap(mapName As String) As List(Of NavigationNode)
        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        Dim result As New List(Of NavigationNode)()

        For Each graph As RecordedNavigationGraph In LoadRecordedNavigationGraphs(normalizedMap)
            If graph IsNot Nothing AndAlso graph.Nodes IsNot Nothing Then
                result.AddRange(graph.Nodes.Where(Function(node) node IsNot Nothing))
            End If
        Next

        Return result.GroupBy(Function(node) node.Id, StringComparer.OrdinalIgnoreCase).Select(Function(group) group.First()).OrderBy(Function(node) node.Label).ToList()
    End Function

    Private Shared Function GetNavigationEdgesForMap(mapName As String) As List(Of NavigationEdge)
        Dim normalizedMap As String = NormalizeNavigationMapName(mapName)
        Dim edges As New List(Of NavigationEdge)()

        For Each graph As RecordedNavigationGraph In LoadRecordedNavigationGraphs(normalizedMap)
            If graph IsNot Nothing AndAlso graph.Edges IsNot Nothing Then
                edges.AddRange(graph.Edges.Where(Function(edge) edge IsNot Nothing))
            End If
        Next

        Return edges
    End Function

    Private Shared Function CreateBidirectionalEdge(fromId As String, toId As String, cost As Double) As List(Of NavigationEdge)
        Return New List(Of NavigationEdge) From {
            New NavigationEdge With {.FromNodeId = fromId, .ToNodeId = toId, .Cost = cost, .TravelMode = "walk"},
            New NavigationEdge With {.FromNodeId = toId, .ToNodeId = fromId, .Cost = cost, .TravelMode = "walk"}
        }
    End Function

    Private Shared Function FindNodeById(nodes As IEnumerable(Of NavigationNode), nodeId As String) As NavigationNode
        Return nodes.FirstOrDefault(Function(node) node IsNot Nothing AndAlso node.Id.Equals(If(nodeId, "").Trim(), StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Function FindNearestNode(nodes As IEnumerable(Of NavigationNode), x As Integer, y As Integer) As NavigationNode
        Dim bestNode As NavigationNode = Nothing
        Dim bestDistance As Double = Double.MaxValue
        For Each node As NavigationNode In nodes
            If node Is Nothing Then
                Continue For
            End If
            Dim dx As Double = node.X - x
            Dim dy As Double = node.Y - y
            Dim distance As Double = Math.Sqrt((dx * dx) + (dy * dy))
            If distance < bestDistance Then
                bestDistance = distance
                bestNode = node
            End If
        Next
        Return bestNode
    End Function

    Private Shared Function FindShortestRoute(nodes As List(Of NavigationNode), edges As List(Of NavigationEdge), startNodeId As String, targetNodeId As String) As List(Of NavigationNode)
        If nodes Is Nothing OrElse edges Is Nothing OrElse String.IsNullOrWhiteSpace(startNodeId) OrElse String.IsNullOrWhiteSpace(targetNodeId) Then
            Return New List(Of NavigationNode)()
        End If

        Dim nodeMap As Dictionary(Of String, NavigationNode) = nodes.Where(Function(node) node IsNot Nothing).ToDictionary(Function(node) node.Id, StringComparer.OrdinalIgnoreCase)
        If Not nodeMap.ContainsKey(startNodeId) OrElse Not nodeMap.ContainsKey(targetNodeId) Then
            Return New List(Of NavigationNode)()
        End If

        Dim distances As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)
        Dim previous As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim unvisited As New HashSet(Of String)(nodeMap.Keys, StringComparer.OrdinalIgnoreCase)

        For Each nodeId As String In nodeMap.Keys
            distances(nodeId) = Double.MaxValue
        Next
        distances(startNodeId) = 0

        While unvisited.Count > 0
            Dim currentId As String = unvisited.OrderBy(Function(nodeId) distances(nodeId)).First()
            unvisited.Remove(currentId)
            If currentId.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase) Then
                Exit While
            End If

            Dim outgoing As IEnumerable(Of NavigationEdge) = edges.Where(Function(edge) edge IsNot Nothing AndAlso edge.FromNodeId.Equals(currentId, StringComparison.OrdinalIgnoreCase))
            For Each edge As NavigationEdge In outgoing
                If Not unvisited.Contains(edge.ToNodeId) Then
                    Continue For
                End If

                Dim altDistance As Double = distances(currentId) + Math.Max(0.01, edge.Cost)
                If altDistance < distances(edge.ToNodeId) Then
                    distances(edge.ToNodeId) = altDistance
                    previous(edge.ToNodeId) = currentId
                End If
            Next
        End While

        If startNodeId.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase) Then
            Return New List(Of NavigationNode) From {nodeMap(startNodeId)}
        End If
        If Not previous.ContainsKey(targetNodeId) Then
            Return New List(Of NavigationNode)()
        End If

        Dim path As New List(Of NavigationNode)()
        Dim walkId As String = targetNodeId
        path.Add(nodeMap(walkId))
        While previous.ContainsKey(walkId)
            walkId = previous(walkId)
            path.Add(nodeMap(walkId))
            If walkId.Equals(startNodeId, StringComparison.OrdinalIgnoreCase) Then
                Exit While
            End If
        End While

        path.Reverse()
        Return path
    End Function

    Private Shared Function CalculateDistance(x1 As Integer, y1 As Integer, x2 As Integer, y2 As Integer) As Double
        Dim dx As Double = x2 - x1
        Dim dy As Double = y2 - y1
        Return Math.Sqrt((dx * dx) + (dy * dy))
    End Function

    Private Shared Function InferHeadingFromCoordinateDelta(fromX As Integer, fromY As Integer, toX As Integer, toY As Integer) As String
        Dim dx As Integer = toX - fromX
        Dim dy As Integer = toY - fromY
        If dx = 0 AndAlso dy = 0 Then
            Return ""
        End If

        If Math.Abs(dx) >= Math.Abs(dy) Then
            Return If(dx >= 0, "E", "W")
        End If

        Return If(dy >= 0, "S", "N")
    End Function

    Private Shared Function DescribeTravelDirection(keyName As String) As String
        Select Case If(keyName, "").Trim().ToUpperInvariant()
            Case "W"
                Return "north"
            Case "A"
                Return "west"
            Case "S"
                Return "south"
            Case "D"
                Return "east"
            Case Else
                Return keyName
        End Select
    End Function

    Private Shared Function CardinalDirectionIndex(direction As String) As Integer
        Select Case If(direction, "").Trim().ToUpperInvariant()
            Case "N"
                Return 0
            Case "E"
                Return 1
            Case "S"
                Return 2
            Case "W"
                Return 3
            Case Else
                Return -1
        End Select
    End Function

    Private Shared Function RotateCardinalDirection(direction As String, quarterTurns As Integer) As String
        Dim index As Integer = CardinalDirectionIndex(direction)
        If index < 0 Then
            Return ""
        End If

        Dim normalizedTurns As Integer = ((quarterTurns Mod 4) + 4) Mod 4
        Dim rotated As Integer = (index + normalizedTurns) Mod 4
        Select Case rotated
            Case 0
                Return "N"
            Case 1
                Return "E"
            Case 2
                Return "S"
            Case 3
                Return "W"
            Case Else
                Return ""
        End Select
    End Function

    Private Shared Function GetDefaultDirectionForKey(keyName As String) As String
        Select Case If(keyName, "").Trim().ToUpperInvariant()
            Case "W"
                Return "N"
            Case "D"
                Return "E"
            Case "S"
                Return "S"
            Case "A"
                Return "W"
            Case Else
                Return ""
        End Select
    End Function

    Private Function GetKeyForDesiredDirection(desiredDirection As String) As String
        Dim normalizedDesired As String = If(desiredDirection, "").Trim().ToUpperInvariant()
        If normalizedDesired = "" Then
            Return ""
        End If

        For Each keyName As String In New String() {"W", "A", "S", "D"}
            Dim actualDirection As String = RotateCardinalDirection(GetDefaultDirectionForKey(keyName), _navigationRotationQuarterTurns)
            If actualDirection.Equals(normalizedDesired, StringComparison.OrdinalIgnoreCase) Then
                Return keyName
            End If
        Next

        Return ""
    End Function

    Private Shared Function GetPreciseTravelBurstMs(baseBurstMs As Integer, axisDistance As Integer, Optional isSecondaryAxis As Boolean = False) As Integer
        Dim distance As Integer = Math.Max(0, axisDistance)
        If distance <= 1 Then
            Return If(isSecondaryAxis, 18, 24)
        End If
        If distance <= 2 Then
            Return If(isSecondaryAxis, 24, 32)
        End If
        If distance <= 4 Then
            Return If(isSecondaryAxis, 32, 45)
        End If
        If distance <= 8 Then
            Return If(isSecondaryAxis, 42, 60)
        End If
        If distance <= 15 Then
            Return If(isSecondaryAxis, 55, 85)
        End If

        Dim maxScale As Double = If(isSecondaryAxis, 0.55, 1.0)
        Dim minScale As Double = If(isSecondaryAxis, 0.16, 0.22)
        Dim scaled As Integer = CInt(Math.Round(baseBurstMs * Math.Min(maxScale, Math.Max(minScale, distance / 60.0))))
        Return Math.Max(If(isSecondaryAxis, 18, 24), scaled)
    End Function

    Private Shared Function ParseHeadingAngle(heading As String) As Double
        Select Case If(heading, "").Trim().ToUpperInvariant()
            Case "N"
                Return 0
            Case "NE"
                Return 45
            Case "E"
                Return 90
            Case "SE"
                Return 135
            Case "S"
                Return 180
            Case "SW"
                Return 225
            Case "W"
                Return 270
            Case "NW"
                Return 315
            Case Else
                Return Double.NaN
        End Select
    End Function

    Private Shared Function NormalizeAngleDelta(delta As Double) As Double
        While delta <= -180
            delta += 360
        End While
        While delta > 180
            delta -= 360
        End While
        Return delta
    End Function

    Private Shared Function CalculateDesiredHeadingAngle(fromX As Integer, fromY As Integer, toX As Integer, toY As Integer) As Double
        Dim dx As Double = toX - fromX
        Dim dy As Double = toY - fromY
        If Math.Abs(dx) < Double.Epsilon AndAlso Math.Abs(dy) < Double.Epsilon Then
            Return Double.NaN
        End If

        Dim radians As Double = Math.Atan2(dx, -dy)
        Dim degrees As Double = radians * (180.0 / Math.PI)
        If degrees < 0 Then
            degrees += 360
        End If
        Return degrees
    End Function

    Private Function IsExactNavigationNodeMatch(node As NavigationNode) As Boolean
        If node Is Nothing Then
            Return False
        End If

        If _lastMapCoordinateX >= 0 AndAlso _lastMapCoordinateY >= 0 AndAlso _lastMapLocalizationConfidence >= 45 Then
            Return _lastMapCoordinateX = node.X AndAlso _lastMapCoordinateY = node.Y
        End If

        If _lastNavigationKnownX >= 0 AndAlso _lastNavigationKnownY >= 0 Then
            Return _lastNavigationKnownX = node.X AndAlso _lastNavigationKnownY = node.Y
        End If

        Return False
    End Function

    Private Function TryToggleNavigationMap(cfg As BotConfig, hwnd As IntPtr, now As DateTime, actionLabel As String, expectMapOpen As Boolean) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return False
        End If
        If _lastNavigationMapToggleAt <> DateTime.MinValue AndAlso (now - _lastNavigationMapToggleAt).TotalMilliseconds < NavigationMapToggleCooldownMs Then
            Return False
        End If

        Dim keyName As String = If(String.IsNullOrWhiteSpace(cfg.MapOpenKey), "M", cfg.MapOpenKey.Trim().ToUpperInvariant())
        If Not SendKey(hwnd, keyName, 70) Then
            Return False
        End If

        MarkKeyUsed(keyName)
        SetLastAction($"{keyName} ({actionLabel})")
        _lastNavigationMapToggleAt = now
        _navigationMapExpectedOpen = expectMapOpen
        If expectMapOpen Then
            _navigationAwaitingLocalization = True
            _navigationLocalizationRetryAfter = now.AddMilliseconds(NavigationMapLocalizationRetryDelayMs)
        Else
            _navigationAwaitingLocalization = False
            _navigationLocalizationRetryAfter = DateTime.MinValue
        End If
        Return True
    End Function

    Private Sub UpdateNavigationTravelProgress(plan As NavigationPlan, now As DateTime)
        If plan Is Nothing OrElse Not plan.RouteReady OrElse plan.NextWaypoint Is Nothing OrElse plan.DistanceToNextWaypoint < 0 Then
            _lastNavigationTravelStalled = False
            Return
        End If

        If Not _lastNavigationProgressWaypointId.Equals(plan.NextWaypoint.Id, StringComparison.OrdinalIgnoreCase) Then
            _lastNavigationProgressWaypointId = plan.NextWaypoint.Id
            _lastNavigationProgressDistance = plan.DistanceToNextWaypoint
            _lastNavigationProgressAt = now
            _lastNavigationTravelStalled = False
            Return
        End If

        If _lastNavigationProgressDistance < 0 OrElse plan.DistanceToNextWaypoint <= (_lastNavigationProgressDistance - NavigationProgressImprovementThreshold) Then
            _lastNavigationProgressDistance = plan.DistanceToNextWaypoint
            _lastNavigationProgressAt = now
            _lastNavigationTravelStalled = False
            Return
        End If

        If _lastNavigationProgressAt = DateTime.MinValue Then
            _lastNavigationProgressAt = now
            _lastNavigationTravelStalled = False
            Return
        End If

        _lastNavigationTravelStalled = False
    End Sub

    Private Function IsNavigationTravelStalled(cfg As BotConfig, now As DateTime) As Boolean
        If cfg Is Nothing OrElse Not cfg.NavigationTravelExecutionEnabled Then
            Return False
        End If
        If _lastNavigationProgressAt = DateTime.MinValue OrElse String.IsNullOrWhiteSpace(_lastNavigationProgressWaypointId) Then
            Return False
        End If

        Dim stallTimeoutMs As Integer = Math.Max(1500, cfg.NavigationStallTimeoutMs)
        Return (now - _lastNavigationProgressAt).TotalMilliseconds >= stallTimeoutMs
    End Function

    Private Function TryRecoverNavigationTravel(cfg As BotConfig, hwnd As IntPtr, now As DateTime, plan As NavigationPlan, ByRef reason As String) As Boolean
        reason = ""
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return False
        End If
        If Not cfg.NavigationRepathOnStuck Then
            _lastNavigationTravelReason = "Travel stalled, but repath on stuck is disabled."
            reason = _lastNavigationTravelReason
            Return False
        End If
        If _lastNavigationRecoveryAt <> DateTime.MinValue AndAlso (now - _lastNavigationRecoveryAt).TotalMilliseconds < NavigationRecoveryCooldownMs Then
            _lastNavigationTravelReason = "Travel recovery cooldown active."
            reason = _lastNavigationTravelReason
            Return False
        End If

        ReleaseMovementKeys(hwnd)

        Dim turnKey As String = If((_lastNavigationRecoveryCount Mod 2) = 0, "A", "D")
        Dim sentAny As Boolean = False

        If SendKey(hwnd, "S", 120) Then
            MarkKeyUsed("S")
            sentAny = True
        End If
        If SendKey(hwnd, turnKey, 140) Then
            MarkKeyUsed(turnKey)
            sentAny = True
        End If
        If SendKey(hwnd, "W", 150) Then
            MarkKeyUsed("W")
            sentAny = True
        End If

        If Not sentAny Then
            _lastNavigationTravelReason = If(plan IsNot Nothing AndAlso plan.NextWaypoint IsNot Nothing,
                                             $"Travel stalled near {plan.NextWaypoint.Label}, but recovery input failed.",
                                             "Travel stalled, but recovery input failed.")
            reason = _lastNavigationTravelReason
            Return False
        End If

        _lastNavigationRecoveryAt = now
        _lastNavigationRecoveryCount += 1
        _lastNavigationProgressAt = now
        _lastNavigationProgressDistance = If(plan IsNot Nothing, plan.DistanceToNextWaypoint, _lastNavigationProgressDistance)
        _lastNavigationMoveCommandAt = now.AddMilliseconds(-Math.Max(250, cfg.NavigationResampleIntervalMs))
        _lastNavigationTravelStalled = True
        SetLastAction($"{turnKey}/S/W (travel recovery)")
        _lastNavigationTravelReason = If(plan IsNot Nothing AndAlso plan.NextWaypoint IsNot Nothing,
                                         $"Travel stalled near {plan.NextWaypoint.Label}. Running recovery #{_lastNavigationRecoveryCount}.",
                                         $"Travel stalled. Running recovery #{_lastNavigationRecoveryCount}.")
        reason = _lastNavigationTravelReason
        Return True
    End Function

    Private Function TrySendTravelMovement(cfg As BotConfig, hwnd As IntPtr, plan As NavigationPlan, ByRef reason As String) As Boolean
        reason = ""
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse plan Is Nothing OrElse plan.NextWaypoint Is Nothing Then
            Return False
        End If
        If _lastNavigationKnownX < 0 OrElse _lastNavigationKnownY < 0 Then
            Return False
        End If

        Dim dx As Integer = plan.NextWaypoint.X - _lastNavigationKnownX
        Dim dy As Integer = plan.NextWaypoint.Y - _lastNavigationKnownY
        If dx = 0 AndAlso dy = 0 Then
            Return False
        End If

        Dim primaryDirection As String
        Dim primaryDistance As Integer
        If Math.Abs(dx) >= Math.Abs(dy) Then
            primaryDirection = If(dx >= 0, "E", "W")
            primaryDistance = Math.Abs(dx)
        Else
            primaryDirection = If(dy >= 0, "S", "N")
            primaryDistance = Math.Abs(dy)
        End If

        Dim primaryKey As String = GetKeyForDesiredDirection(primaryDirection)
        If primaryKey = "" Then
            primaryKey = If(primaryDirection = "N", "W",
                        If(primaryDirection = "S", "S",
                        If(primaryDirection = "E", "D", "A")))
        End If

        Dim baseBurstMs As Integer = Math.Max(100, Math.Min(1200, cfg.NavigationMoveBurstMs))
        Dim primaryBurstMs As Integer = GetPreciseTravelBurstMs(baseBurstMs, primaryDistance)
        If SendKey(hwnd, primaryKey, primaryBurstMs) Then
            MarkKeyUsed(primaryKey)
            SetLastAction($"{primaryKey} (travel move: {plan.NextWaypoint.Label})")
            _lastTravelInputKey = primaryKey
            _lastTravelInputDesiredDirection = primaryDirection
            _lastTravelInputPoseX = _lastNavigationKnownX
            _lastTravelInputPoseY = _lastNavigationKnownY
            _lastTravelInputAt = DateTime.UtcNow
            reason = $"Moving toward {plan.NextWaypoint.Label}: want {primaryDirection}, using {primaryKey}."
            Return True
        End If

        Return False
    End Function

    Private Function TryHandleNavigationTravel(cfg As BotConfig, hwnd As IntPtr, now As DateTime, targetWindowVisible As Boolean, targetValid As Boolean, ByRef reason As String) As Boolean
        _lastNavigationTravelActive = False
        _lastNavigationTravelReason = ""
        _lastNavigationDistanceToWaypoint = -1
        _lastNavigationTravelStalled = False
        _lastNavigationDestinationReached = False
        _lastNavigationDestinationLabel = ""
        reason = ""

        If cfg Is Nothing OrElse Not cfg.LevelingAgentEnabled OrElse Not cfg.NavigationEnabled OrElse Not cfg.NavigationTravelExecutionEnabled Then
            _navigationMapExpectedOpen = False
            _navigationAwaitingLocalization = False
            _navigationLocalizationRetryAfter = DateTime.MinValue
            _navigationLocalizationFailureCount = 0
            _navigationLocalizationPaused = False
            Return False
        End If

        _lastNavigationTravelActive = True
        If _navigationLocalizationPaused Then
            _lastNavigationTravelActive = False
            _lastNavigationTravelReason = "Navigation paused: map localization failed repeatedly. Recalibrate the map coordinate region."
            reason = _lastNavigationTravelReason
            Return False
        End If

        Dim plan As NavigationPlan = BuildNavigationPlan(cfg, now, allowStaleLocalization:=True)
        _lastNavigationDistanceToWaypoint = plan.DistanceToNextWaypoint

        If targetWindowVisible OrElse targetValid Then
            _lastNavigationTravelReason = "Travel execution paused while a combat target is active."
            Return False
        End If

        If Not plan.RouteReady OrElse plan.NextWaypoint Is Nothing Then
            _navigationAwaitingLocalization = False
            _navigationLocalizationRetryAfter = DateTime.MinValue
            _lastNavigationTravelReason = If(plan.StatusText = "", "Waiting for a usable navigation route from visible map coordinates.", plan.StatusText)
            reason = _lastNavigationTravelReason
            Return False
        End If

        If _lastNavigationKnownX < 0 OrElse _lastNavigationKnownY < 0 Then
            _navigationAwaitingLocalization = False
            _navigationLocalizationRetryAfter = DateTime.MinValue
            _lastNavigationTravelReason = "Waiting for visible map coordinates."
            reason = _lastNavigationTravelReason
            Return False
        End If

        If String.IsNullOrWhiteSpace(_navigationCommittedWaypointId) AndAlso plan.NextWaypoint IsNot Nothing Then
            _navigationCommittedWaypointId = plan.NextWaypoint.Id
            _navigationCommittedWaypointLabel = plan.NextWaypoint.Label
        End If

        If plan.TargetNode IsNot Nothing AndAlso IsExactNavigationNodeMatch(plan.TargetNode) Then
            _lastNavigationDestinationReached = True
            _lastNavigationDestinationLabel = plan.TargetNode.Label
            _navigationCommittedWaypointId = ""
            _navigationCommittedWaypointLabel = ""
            _lastNavigationTravelReason = $"Destination reached with exact coordinate match: {plan.TargetNode.Label}."
            _lastNavigationTravelActive = False
            _lastNavigationDistanceToWaypoint = 0
            _lastNavigationTravelStalled = False
            _lastNavigationProgressWaypointId = ""
            _lastNavigationProgressDistance = -1
            _lastNavigationProgressAt = now
            reason = _lastNavigationTravelReason
            Return False
        End If

        UpdateNavigationTravelProgress(plan, now)
        If IsNavigationTravelStalled(cfg, now) Then
            If TryRecoverNavigationTravel(cfg, hwnd, now, plan, reason) Then
                Return True
            End If
            _lastNavigationTravelStalled = True
        Else
            _lastNavigationTravelStalled = False
        End If

        If plan.NextWaypoint IsNot Nothing AndAlso IsExactNavigationNodeMatch(plan.NextWaypoint) Then
            _navigationCommittedWaypointId = ""
            _navigationCommittedWaypointLabel = ""
            _lastNavigationTravelReason = $"Exact waypoint match: {plan.NextWaypoint.Label}. Advancing to the next node."
            reason = _lastNavigationTravelReason
            Return False
        End If

        Dim resampleIntervalMs As Integer = Math.Max(250, cfg.NavigationResampleIntervalMs)
        Dim moveCooldownMs As Integer = Math.Max(250, Math.Min(resampleIntervalMs, cfg.NavigationMoveBurstMs + 180))
        If _lastNavigationMoveCommandAt <> DateTime.MinValue AndAlso (now - _lastNavigationMoveCommandAt).TotalMilliseconds < moveCooldownMs Then
            _lastNavigationTravelReason = $"Continuing travel toward {plan.NextWaypoint.Label}."
            reason = _lastNavigationTravelReason
            Return False
        End If

        If _lastNavigationMoveCommandAt <> DateTime.MinValue AndAlso (now - _lastNavigationMoveCommandAt).TotalMilliseconds >= resampleIntervalMs Then
            _lastNavigationTravelReason = $"Watching live map coordinates toward {plan.NextWaypoint.Label}."
        End If

        Dim moveReason As String = ""
        If TrySendTravelMovement(cfg, hwnd, plan, moveReason) Then
            _lastNavigationMoveCommandAt = now
            _lastNavigationTravelReason = moveReason
            reason = moveReason
            Return True
        End If

        _lastNavigationTravelReason = $"Unable to issue travel movement toward {plan.NextWaypoint.Label}."
        reason = _lastNavigationTravelReason
        Return False
    End Function

    Private Shared Function TryParseMapCoordinate(rawText As String, ByRef x As Integer, ByRef y As Integer, ByRef normalized As String, ByRef confidence As Integer) As Boolean
        x = -1
        y = -1
        normalized = ""
        confidence = 0
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim normalizedRaw As String = rawText.ToUpperInvariant()
        normalizedRaw = normalizedRaw.Replace("O", "0").Replace("I", "1").Replace("L", "1").Replace("|", "/")
        normalizedRaw = Regex.Replace(normalizedRaw, "[^0-9/,\- ]", " ")
        normalizedRaw = Regex.Replace(normalizedRaw, "\s+", " ").Trim()
        If normalizedRaw = "" Then
            Return False
        End If

        Dim explicitMatch As Match = Regex.Match(normalizedRaw, "(\d{3})\s*[/,]\s*(\d{3})")
        If explicitMatch.Success Then
            x = Integer.Parse(explicitMatch.Groups(1).Value)
            y = Integer.Parse(explicitMatch.Groups(2).Value)
            normalized = $"{x:000}/{y:000}"
            confidence = 99
            Return True
        End If

        Dim fallbackMatch As Match = Regex.Match(normalizedRaw, "(\d{1,3})\D+(\d{1,3})")
        If fallbackMatch.Success Then
            x = Integer.Parse(fallbackMatch.Groups(1).Value)
            y = Integer.Parse(fallbackMatch.Groups(2).Value)
            If x >= 0 AndAlso x <= 999 AndAlso y >= 0 AndAlso y <= 999 Then
                normalized = $"{x:000}/{y:000}"
                confidence = 78
                Return True
            End If
        End If

        Return False
    End Function

    Private Shared Function IsMapMarkerColor(c As Color) As Boolean
        Dim sat As Double = c.GetSaturation()
        Dim bright As Double = c.GetBrightness()
        If sat < 0.35 OrElse bright < 0.16 Then
            Return False
        End If

        Dim hue As Double = c.GetHue()
        Dim redHue As Boolean = hue <= 20.0 OrElse hue >= 340.0
        Dim yellowHue As Boolean = hue >= 35.0 AndAlso hue <= 68.0
        Dim redDominant As Boolean = c.R >= c.G + 25 AndAlso c.R >= c.B + 25
        Dim yellowDominant As Boolean = c.R >= 170 AndAlso c.G >= 120 AndAlso c.B <= 140
        Return (redHue AndAlso redDominant) OrElse (yellowHue AndAlso yellowDominant)
    End Function

    Private Shared Function CropBitmapToPolygon(frame As Bitmap, points As List(Of DrawingPoint)) As Bitmap
        If frame Is Nothing OrElse points Is Nothing OrElse points.Count < 3 Then
            Return Nothing
        End If

        Dim normalized As List(Of DrawingPoint) = points.Select(Function(pt) New DrawingPoint(Math.Max(0, Math.Min(frame.Width - 1, pt.X)), Math.Max(0, Math.Min(frame.Height - 1, pt.Y)))).ToList()
        If normalized.Count < 3 Then
            Return Nothing
        End If

        Dim minX As Integer = normalized.Min(Function(pt) pt.X)
        Dim minY As Integer = normalized.Min(Function(pt) pt.Y)
        Dim maxX As Integer = normalized.Max(Function(pt) pt.X)
        Dim maxY As Integer = normalized.Max(Function(pt) pt.Y)
        If maxX <= minX OrElse maxY <= minY Then
            Return Nothing
        End If

        Dim bounds As New Rectangle(minX, minY, Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1))
        Dim result As New Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb)
        Using g As Graphics = Graphics.FromImage(result)
            g.Clear(Color.Black)
            Using path As New GraphicsPath()
                Dim localPoints As DrawingPoint() = normalized.Select(Function(pt) New DrawingPoint(pt.X - bounds.X, pt.Y - bounds.Y)).ToArray()
                path.AddPolygon(localPoints)
                g.SetClip(path)
                g.DrawImageUnscaled(frame, -bounds.X, -bounds.Y)
                g.ResetClip()
            End Using
        End Using
        Return result
    End Function

    Private Function ReadMobNameIfNeeded(frame As Bitmap, region As RectRegion, now As DateTime, Optional forceRefresh As Boolean = False) As String
        If frame Is Nothing Then
            Return ""
        End If

        If (Not forceRefresh) AndAlso (now - _lastMobNameRead).TotalMilliseconds < 650 Then
            Return _cachedMobName
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            Dim candidate As String = OcrReader.ReadName(crop)
            If Not String.IsNullOrWhiteSpace(candidate) Then
                _cachedMobName = candidate.Trim()
            ElseIf (now - _lastMobNameRead).TotalMilliseconds > 1200 Then
                _cachedMobName = ""
            End If
            _lastMobNameRead = now
            Return _cachedMobName
        Finally
            crop.Dispose()
        End Try
    End Function

    Private Shared Function HasHighMaxHpAttackAction(cfg As BotConfig) As Boolean
        Return cfg IsNot Nothing AndAlso
            cfg.Actions IsNot Nothing AndAlso
            cfg.Actions.Any(Function(a) a IsNot Nothing AndAlso a.Enabled AndAlso a.Role = "high_max_hp")
    End Function

    Private Function UpdateMobMaxHpTracking(cfg As BotConfig, frame As Bitmap, region As RectRegion, targetWindowVisible As Boolean, mobHpPercent As Double, now As DateTime) As Integer
        If _mobHpTextOcrTask IsNot Nothing AndAlso _mobHpTextOcrTask.IsCompleted Then
            Try
                _lastMobHpText = NormalizeMobHpText(If(_mobHpTextOcrTask.Result, "").Trim())
                _lastMobDetectedMaxHp = ParseMobMaxHpFromText(_lastMobHpText)
            Catch
                _lastMobHpText = ""
                _lastMobDetectedMaxHp = -1
            End Try
            _mobHpTextOcrTask = Nothing
        End If

        Dim canTrack As Boolean =
            frame IsNot Nothing AndAlso
            targetWindowVisible AndAlso
            mobHpPercent >= Math.Max(0.6, cfg.MobHpPresenceThreshold * 0.7)

        If Not canTrack Then
            If _mobHpTextOcrTask Is Nothing Then
                _lastMobHpText = ""
                _lastMobDetectedMaxHp = -1
            End If
            Return _lastMobDetectedMaxHp
        End If

        If _mobHpTextOcrTask IsNot Nothing Then
            Return _lastMobDetectedMaxHp
        End If

        If _lastMobHpTextScan <> DateTime.MinValue AndAlso (now - _lastMobHpTextScan).TotalMilliseconds < MobHpTextOcrMinIntervalMs Then
            Return _lastMobDetectedMaxHp
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        Dim paddedRect As Rectangle = Rectangle.FromLTRB(
            Math.Max(0, rect.Left - 2),
            Math.Max(0, rect.Top - 8),
            Math.Min(frame.Width, rect.Right + 2),
            Math.Min(frame.Height, rect.Bottom + 8))
        If paddedRect.Width <= 1 OrElse paddedRect.Height <= 1 Then
            Return _lastMobDetectedMaxHp
        End If

        Dim crop As New Bitmap(Math.Max(1, paddedRect.Width), Math.Max(1, paddedRect.Height), PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), paddedRect, GraphicsUnit.Pixel)
            End Using

            Dim workerCrop As Bitmap = DirectCast(crop.Clone(), Bitmap)
            _lastMobHpTextScan = now
            _mobHpTextOcrTask = Task.Run(
                Function()
                    Using workerCrop
                        Return OcrReader.ReadHpFraction(workerCrop)
                    End Using
                End Function)
        Finally
            crop.Dispose()
        End Try

        Return _lastMobDetectedMaxHp
    End Function

    Private Shared Function ParseMobMaxHpFromText(raw As String) As Integer
        If String.IsNullOrWhiteSpace(raw) Then
            Return -1
        End If

        Dim normalized As String = NormalizeMobHpText(raw)
        If normalized = "" Then
            Return -1
        End If

        Dim fractionMatch As Match = Regex.Match(normalized, "(\d{2,9})\s*/\s*(\d{2,9})")
        If fractionMatch.Success Then
            Dim maxValue As Integer
            If Integer.TryParse(fractionMatch.Groups(2).Value, maxValue) AndAlso maxValue > 0 Then
                Return maxValue
            End If
        End If

        Dim numbers As List(Of Integer) = Regex.Matches(normalized, "\d{2,9}").
            Cast(Of Match)().
            Select(Function(m)
                       Dim value As Integer = -1
                       Integer.TryParse(m.Value, value)
                       Return value
                   End Function).
            Where(Function(v) v > 0).
            ToList()
        If numbers.Count >= 2 Then
            Return numbers.Max()
        End If

        Return -1
    End Function

    Private Shared Function NormalizeMobHpText(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If

        Dim normalized As String = raw.ToUpperInvariant()
        normalized = normalized.Replace("O", "0").Replace("I", "1").Replace("L", "1").Replace("|", "1")
        normalized = normalized.Replace(",", "").Replace(".", "")
        normalized = Regex.Replace(normalized, "[^0-9/ ]", " ")
        normalized = Regex.Replace(normalized, "/{2,}", "/")
        normalized = Regex.Replace(normalized, "\s+", " ").Trim()
        Return normalized
    End Function

    Private Sub TryHandleLootPickup(cfg As BotConfig, hwnd As IntPtr, now As DateTime, actionSent As Boolean)
        If Not cfg.LootPickupEnabled Then
            Return
        End If
        If hwnd = IntPtr.Zero Then
            Return
        End If
        ' Do not fully block looting when combat/support keys fire in the same loop.
        ' Only skip if an attack/special key was just sent to avoid immediate input collision.
        If actionSent AndAlso _lastAttackAction <> DateTime.MinValue AndAlso (now - _lastAttackAction).TotalMilliseconds < 180 Then
            Return
        End If
        If cfg.LootAllowedNames Is Nothing OrElse cfg.LootAllowedNames.Count = 0 Then
            Return
        End If

        Dim intervalMs As Integer = Math.Max(1000, cfg.LootPickupIntervalMs)
        If _lastLootPickup <> DateTime.MinValue AndAlso (now - _lastLootPickup).TotalMilliseconds < intervalMs Then
            Return
        End If

        Dim lastAnyRetarget As DateTime = GetLatestRetargetAt()
        If lastAnyRetarget <> DateTime.MinValue AndAlso (now - lastAnyRetarget).TotalMilliseconds < Math.Min(GetRetargetCooldownMs(cfg, 1, forced:=False), GetRetargetCooldownMs(cfg, 1, forced:=True)) Then
            Return
        End If

        _lastLootPickup = now
        If Not SendKey(hwnd, "F", FastKeyPressMs) Then
            Return
        End If

        RaiseEvent LogLine("Loot scan sent (F).")
        _pendingLootPickupVerifyAt = now.AddMilliseconds(Math.Max(120, cfg.LootPickupVerifyDelayMs))
    End Sub

    Private Function ReadPranaExpPercent(frame As Bitmap, pranaExpRegion As RectRegion) As Double
        Dim now As DateTime = DateTime.UtcNow
        If _expOcrTask IsNot Nothing AndAlso _expOcrTask.IsCompleted Then
            Try
                Dim parsed As Double = _expOcrTask.Result
                If parsed >= 0 AndAlso parsed <= 100 Then
                    _lastExpPercent = parsed
                End If
            Catch
            End Try
            _expOcrTask = Nothing
        End If

        If _expOcrTask IsNot Nothing Then
            Return _lastExpPercent
        End If

        If _lastExpOcrAt <> DateTime.MinValue AndAlso (now - _lastExpOcrAt).TotalMilliseconds < ExpOcrMinIntervalMs Then
            Return _lastExpPercent
        End If

        If frame Is Nothing OrElse pranaExpRegion Is Nothing Then
            Return _lastExpPercent
        End If

        Dim rect As Rectangle = pranaExpRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return _lastExpPercent
        End If

        Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using
            _lastExpOcrAt = now
            _expOcrTask = Task.Run(
                Function()
                    Try
                        Return OcrReader.ReadPercent(crop)
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
            Return _lastExpPercent
        Catch
            crop.Dispose()
        End Try

        Return _lastExpPercent
    End Function

    Private Function UpdateExpRate(expPercent As Double, now As DateTime) As Double
        If expPercent < 0 Then
            Return _lastExpPerHour
        End If

        If _lastExpRateSampleAt = DateTime.MinValue Then
            _lastExpRateSampleAt = now
            _lastExpRateSamplePercent = expPercent
            _lastExpPerHour = -1
            Return _lastExpPerHour
        End If

        Dim elapsedMs As Double = (now - _lastExpRateSampleAt).TotalMilliseconds
        If elapsedMs < ExpRateSampleMs Then
            Return _lastExpPerHour
        End If

        Dim delta As Double = expPercent - _lastExpRateSamplePercent
        If delta < -50.0 Then
            delta += 100.0
        End If
        If delta < 0 Then
            delta = 0
        End If

        Dim hours As Double = elapsedMs / 3600000.0
        If hours > 0 Then
            _lastExpPerHour = delta / hours
        End If

        _lastExpRateSampleAt = now
        _lastExpRateSamplePercent = expPercent
        Return _lastExpPerHour
    End Function

    Private Function ReadRupiahsTotal(frame As Bitmap, rupiahsRegion As RectRegion) As Long
        Dim now As DateTime = DateTime.UtcNow
        If _rupiahsOcrTask IsNot Nothing AndAlso _rupiahsOcrTask.IsCompleted Then
            Try
                Dim parsed As Long = _rupiahsOcrTask.Result
                If parsed >= 0 Then
                    _lastRupiahsTotal = parsed
                End If
            Catch
            End Try
            _rupiahsOcrTask = Nothing
        End If

        If _rupiahsOcrTask IsNot Nothing Then
            Return _lastRupiahsTotal
        End If

        If _lastRupiahsOcrAt <> DateTime.MinValue AndAlso (now - _lastRupiahsOcrAt).TotalMilliseconds < RupiahsOcrMinIntervalMs Then
            Return _lastRupiahsTotal
        End If

        If frame Is Nothing OrElse rupiahsRegion Is Nothing Then
            Return _lastRupiahsTotal
        End If

        Dim rect As Rectangle = rupiahsRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return _lastRupiahsTotal
        End If

        Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using
            _lastRupiahsOcrAt = now
            _rupiahsOcrTask = Task.Run(
                Function()
                    Try
                        Return OcrReader.ReadInteger(crop)
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
            Return _lastRupiahsTotal
        Catch
            crop.Dispose()
        End Try

        Return _lastRupiahsTotal
    End Function

    Private Function UpdateRupiahsRate(rupiahsTotal As Long, now As DateTime) As Double
        If rupiahsTotal < 0 Then
            Return _lastRupiahsPerHour
        End If

        If _lastRupiahsRateSampleAt = DateTime.MinValue Then
            _lastRupiahsRateSampleAt = now
            _lastRupiahsRateSampleTotal = rupiahsTotal
            _lastRupiahsPerHour = -1
            Return _lastRupiahsPerHour
        End If

        Dim elapsedMs As Double = (now - _lastRupiahsRateSampleAt).TotalMilliseconds
        If elapsedMs < ExpRateSampleMs Then
            Return _lastRupiahsPerHour
        End If

        Dim delta As Long = rupiahsTotal - _lastRupiahsRateSampleTotal
        If delta < 0 Then
            _lastRupiahsRateSampleAt = now
            _lastRupiahsRateSampleTotal = rupiahsTotal
            _lastRupiahsPerHour = -1
            Return _lastRupiahsPerHour
        End If

        Dim hours As Double = elapsedMs / 3600000.0
        If hours > 0 Then
            _lastRupiahsPerHour = delta / hours
        End If

        _lastRupiahsRateSampleAt = now
        _lastRupiahsRateSampleTotal = rupiahsTotal
        Return _lastRupiahsPerHour
    End Function

    Private Function TryHandleAutoAcceptPrompts(cfg As BotConfig, hwnd As IntPtr, frame As Bitmap, now As DateTime, partyInviteScanRegion As RectRegion, partyInviteOkRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse (Not cfg.PartyAutoAcceptEnabled) Then
            _lastPartyInviteCandidate = ""
            Return False
        End If
        If hwnd = IntPtr.Zero OrElse frame Is Nothing Then
            Return False
        End If
        If _lastPartyInviteAccept <> DateTime.MinValue AndAlso (now - _lastPartyInviteAccept).TotalMilliseconds < 2000 Then
            Return False
        End If

        If _partyInviteOcrTask IsNot Nothing AndAlso _partyInviteOcrTask.IsCompleted Then
            Try
                _lastPartyInviteCandidate = If(_partyInviteOcrTask.Result, "").Trim()
            Catch
                _lastPartyInviteCandidate = ""
            End Try
            _partyInviteOcrTask = Nothing
        End If

        If IsAlreadyInPartyPrompt(_lastPartyInviteCandidate) Then
            _partyAskSuppressedInParty = True
            _partyAskPauseLogged = False
        End If

        Dim promptKind As String = DetectAutoAcceptPromptKind(_lastPartyInviteCandidate)
        If promptKind <> "" Then
            If ClickClientRegionCenter(hwnd, partyInviteOkRegion, frame.Width, frame.Height) Then
                _lastPartyInviteAccept = now
                Dim promptLabel As String = If(promptKind = "ress", "resurrection prompt", "party invite")
                SetLastAction($"Click OK ({promptLabel} accepted: {If(String.IsNullOrWhiteSpace(_lastPartyInviteCandidate), "detected", _lastPartyInviteCandidate)})")
                RaiseEvent LogLine($"{promptLabel} detected and auto-accepted.")
                If promptKind = "party" Then
                    _partyAskSuppressedInParty = True
                    _partyAskPauseLogged = False
                    RaiseEvent LogLine("Party detected. Auto party asking paused.")
                End If
                _lastPartyInviteCandidate = ""
                Return True
            End If
        End If

        If _partyInviteOcrTask IsNot Nothing Then
            Return False
        End If

        If _lastPartyInviteScan <> DateTime.MinValue AndAlso (now - _lastPartyInviteScan).TotalMilliseconds < PartyInviteOcrMinIntervalMs Then
            Return False
        End If

        Dim rect As Rectangle = partyInviteScanRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return False
        End If

        Dim crop As New Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            _lastPartyInviteScan = now
            _partyInviteOcrTask = Task.Run(
                Function()
                    Try
                        Return OcrReader.ReadName(crop)
                    Catch
                        Return ""
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
        Catch
            crop.Dispose()
        End Try

        Return False
    End Function

    Private Function TryHandlePartyAsk(cfg As BotConfig, hwnd As IntPtr, now As DateTime) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return False
        End If

        If Not cfg.PartyAskEnabled Then
            _partyAskWasEnabled = False
            _partyAskPauseLogged = False
            Return False
        End If

        If Not _partyAskWasEnabled Then
            _partyAskWasEnabled = True
            _partyAskSuppressedInParty = False
            _partyAskPauseLogged = False
            _lastPartyAskAt = DateTime.MinValue
        End If

        If _partyAskSuppressedInParty Then
            If Not _partyAskPauseLogged Then
                RaiseEvent LogLine("Party ask skipped: already in a party.")
                _partyAskPauseLogged = True
            End If
            Return False
        End If

        Dim intervalMs As Integer = Math.Max(5000, cfg.PartyAskIntervalMs)
        If _lastPartyAskAt <> DateTime.MinValue AndAlso (now - _lastPartyAskAt).TotalMilliseconds < intervalMs Then
            Return False
        End If

        Dim commandText As String = NormalizePartyAskCommand(cfg.PartyAskText)
        If Not SendKey(hwnd, "ENTER", FastKeyPressMs) Then
            Return False
        End If
        Thread.Sleep(60)

        Dim typedOk As Boolean = SendPartyAskCommand(hwnd, commandText)
        Thread.Sleep(55)

        Dim sentFinalEnter As Boolean = SendKey(hwnd, "ENTER", FastKeyPressMs)
        If sentFinalEnter Then
            _lastPartyAskAt = now
            _partyAskPauseLogged = False
            SetLastAction($"ENTER {commandText} ENTER (party ask)")
            RaiseEvent LogLine($"Party ask command sent: {commandText}")
            Return True
        End If

        If typedOk Then
            SetLastAction($"ENTER {commandText} (party ask partial)")
            RaiseEvent LogLine($"Party ask command partially sent: {commandText}")
            _lastPartyAskAt = now
            _partyAskPauseLogged = False
            Return True
        End If
        Return False
    End Function

    Private Shared Function NormalizePartyAskCommand(rawText As String) As String
        Dim cleaned As String = If(rawText, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        If cleaned = "" Then
            Return "add"
        End If
        Return cleaned
    End Function

    Private Shared Function SendPartyAskCommand(hwnd As IntPtr, rawText As String) As Boolean
        Dim commandText As String = NormalizePartyAskCommand(rawText)
        Dim typedAny As Boolean = False
        For Each ch As Char In commandText
            Dim keyName As String = PartyAskCharToKeyName(ch)
            If keyName = "" Then
                Continue For
            End If
            If Not SendKey(hwnd, keyName, 20, True) Then
                Return typedAny
            End If
            typedAny = True
            Thread.Sleep(20)
        Next
        Return typedAny
    End Function

    Private Shared Function PartyAskCharToKeyName(ch As Char) As String
        Select Case ch
            Case " "c
                Return "SPACE"
            Case "/"c
                Return "SLASH"
            Case "."c
                Return "PERIOD"
            Case ","c
                Return "COMMA"
            Case "-"c
                Return "MINUS"
            Case ";"c
                Return "SEMICOLON"
            Case "'"c
                Return "APOSTROPHE"
            Case "="c
                Return "EQUALS"
        End Select
        If Char.IsLetterOrDigit(ch) Then
            Return Char.ToUpperInvariant(ch).ToString()
        End If
        Return ""
    End Function

    Private Function TryHandleUnreachableTarget(cfg As BotConfig, hwnd As IntPtr, frame As Bitmap, now As DateTime, unreachableTextRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse frame Is Nothing Then
            Return False
        End If

        If _unreachableOcrTask IsNot Nothing AndAlso _unreachableOcrTask.IsCompleted Then
            Try
                _lastUnreachableCandidate = If(_unreachableOcrTask.Result, "").Trim()
            Catch
                _lastUnreachableCandidate = ""
            End Try
            _unreachableOcrTask = Nothing

            Dim matched As Boolean = IsUnreachablePrompt(_lastUnreachableCandidate)
            If matched Then
                _unreachableClearCount = 0
                If Not _unreachableLatched Then
                    If _unreachableLastMatchAt = DateTime.MinValue OrElse (now - _unreachableLastMatchAt).TotalMilliseconds > UnreachableConfirmWindowMs Then
                        _unreachableConfirmCount = 1
                    Else
                        _unreachableConfirmCount += 1
                    End If
                    _unreachableLastMatchAt = now
                Else
                    ' Stale unreachable text can stay on screen; ignore retriggers until OCR sees it clear.
                    _unreachableConfirmCount = 0
                End If
            Else
                _unreachableConfirmCount = 0
                _unreachableLastMatchAt = DateTime.MinValue
                _lastUnreachableCandidate = ""
                If _unreachableLatched Then
                    _unreachableClearCount += 1
                    If _unreachableClearCount >= UnreachableClearRequiredCount Then
                        _unreachableLatched = False
                        _unreachableClearCount = 0
                    End If
                End If
            End If
        End If

        If (Not _unreachableLatched) AndAlso _unreachableConfirmCount >= UnreachableConfirmRequiredCount Then
            Dim unreachableRetargetMs As Integer = GetRetargetCooldownMs(cfg, 1, forced:=True)
            Dim triggerReady As Boolean = (_lastUnreachableTrigger = DateTime.MinValue) OrElse ((now - _lastUnreachableTrigger).TotalMilliseconds >= unreachableRetargetMs)
            If triggerReady Then
                _lastUnreachableTrigger = now
                _unreachableLockUntil = now.AddMilliseconds(unreachableRetargetMs)
                _unreachableConfirmCount = 0
                _unreachableLastMatchAt = DateTime.MinValue
                _lastUnreachableCandidate = ""
                _unreachableLatched = True
                _unreachableClearCount = 0
                _firstHitPending = False
                _firstHitTargetSignature = ""
                _firstHitWindowUntil = DateTime.MinValue
                _nameConfirmCandidate = ""
                _nameConfirmCount = 0
                _nameConfirmConfirmedName = ""
                _nameConfirmLastSampleAt = DateTime.MinValue
                _nameConfirmLastReadProcessedAt = DateTime.MinValue

                If TrySendRetargetKey(hwnd, cfg, now, "E (unreachable target)", forced:=True) Then
                    RaiseEvent LogLine("Unreachable target detected by OCR. Forced retarget.")
                    Return True
                End If
            End If
        End If

        If _unreachableConfirmCount > 0 AndAlso _unreachableLastMatchAt <> DateTime.MinValue AndAlso (now - _unreachableLastMatchAt).TotalMilliseconds > UnreachableConfirmWindowMs Then
            _unreachableConfirmCount = 0
            _unreachableLastMatchAt = DateTime.MinValue
        End If

        If _unreachableOcrTask IsNot Nothing Then
            Return False
        End If

        If _lastUnreachableScan <> DateTime.MinValue AndAlso (now - _lastUnreachableScan).TotalMilliseconds < UnreachableOcrMinIntervalMs Then
            Return False
        End If

        Dim rect As Rectangle = unreachableTextRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return False
        End If

        Dim crop As New Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            _lastUnreachableScan = now
            _unreachableOcrTask = Task.Run(
                Function()
                    Try
                        Return OcrReader.ReadName(crop)
                    Catch
                        Return ""
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
        Catch
            crop.Dispose()
        End Try

        Return False
    End Function

    Private Shared Function IsUnreachablePrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeMobName(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("unabletoreachtarget", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("cannotreachtarget", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("cantreachtarget", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        If AreTextsClose(norm, "unable to reach target") OrElse AreTextsClose(norm, "cannot reach target") Then
            Return True
        End If

        Dim hasReach As Boolean = norm.Contains("reach", StringComparison.OrdinalIgnoreCase)
        Dim hasTarget As Boolean = norm.Contains("target", StringComparison.OrdinalIgnoreCase)
        Dim hasUnable As Boolean =
            norm.Contains("unable", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("cannot", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("cant", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("can't", StringComparison.OrdinalIgnoreCase)
        Return hasReach AndAlso hasTarget AndAlso hasUnable
    End Function

    Private Shared Function DetectAutoAcceptPromptKind(rawText As String) As String
        If IsPartyInvitePrompt(rawText) Then
            Return "party"
        End If
        If IsRessPrompt(rawText) Then
            Return "ress"
        End If
        Return ""
    End Function

    Private Shared Function IsPartyInvitePrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeMobName(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("invitedyoutojointheparty", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Dim hasParty As Boolean = norm.Contains("party", StringComparison.OrdinalIgnoreCase) OrElse norm.Contains("parly", StringComparison.OrdinalIgnoreCase)
        Dim hasInvite As Boolean = norm.Contains("invited", StringComparison.OrdinalIgnoreCase) OrElse norm.Contains("invite", StringComparison.OrdinalIgnoreCase)
        Dim hasJoin As Boolean = norm.Contains("join", StringComparison.OrdinalIgnoreCase)
        Return hasParty AndAlso (hasInvite OrElse hasJoin)
    End Function

    Private Shared Function IsAlreadyInPartyPrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeMobName(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("alreadyinparty", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("alreadyinaparty", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("youarealreadyinparty", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Dim hasAlready As Boolean = norm.Contains("already", StringComparison.OrdinalIgnoreCase)
        Dim hasParty As Boolean = norm.Contains("party", StringComparison.OrdinalIgnoreCase) OrElse norm.Contains("parly", StringComparison.OrdinalIgnoreCase)
        Return hasAlready AndAlso hasParty
    End Function

    Private Shared Function IsRessPrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeMobName(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("resurrect", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("resurrection", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("resurect", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("ressurect", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("revive", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("revival", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Dim hasRes As Boolean =
            norm.Contains("resur", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("revive", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("revival", StringComparison.OrdinalIgnoreCase)
        Dim hasPrompt As Boolean =
            norm.Contains("accept", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("request", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("yes", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("ok", StringComparison.OrdinalIgnoreCase) OrElse
            norm.Contains("want", StringComparison.OrdinalIgnoreCase)
        Return hasRes AndAlso hasPrompt
    End Function

    Private Shared Function IsDeniedMob(mobName As String, denied As List(Of String)) As Boolean
        If String.IsNullOrWhiteSpace(mobName) OrElse denied Is Nothing OrElse denied.Count = 0 Then
            Return False
        End If

        Dim normMob As String = NormalizeMobName(mobName)
        If normMob = "" Then
            Return False
        End If

        For Each item In denied
            Dim normDenied As String = NormalizeMobName(item)
            If normDenied = "" Then
                Continue For
            End If
            If normMob.Equals(normDenied, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            If normMob.Contains(normDenied, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            If IsFuzzyBlacklistMatch(normMob, normDenied) Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function IsPreferredMob(mobName As String, preferred As List(Of String)) As Boolean
        If String.IsNullOrWhiteSpace(mobName) OrElse preferred Is Nothing OrElse preferred.Count = 0 Then
            Return True
        End If

        Dim normMob As String = NormalizeMobName(mobName)
        If normMob = "" Then
            Return False
        End If

        For Each item In preferred
            Dim normPreferred As String = NormalizeMobName(item)
            If normPreferred = "" Then
                Continue For
            End If
            If normMob.Equals(normPreferred, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            If normMob.Contains(normPreferred, StringComparison.OrdinalIgnoreCase) OrElse normPreferred.Contains(normMob, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            If AreTextsClose(normMob, normPreferred) Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function IsFuzzyBlacklistMatch(normMob As String, normDenied As String) As Boolean
        If String.IsNullOrWhiteSpace(normMob) OrElse String.IsNullOrWhiteSpace(normDenied) Then
            Return False
        End If

        If AreTextsClose(normMob, normDenied) Then
            Return True
        End If

        Dim mobTokens As String() = normMob.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        Dim deniedTokens As String() = normDenied.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If mobTokens.Length = 0 OrElse deniedTokens.Length = 0 Then
            Return False
        End If

        If mobTokens.Length >= deniedTokens.Length Then
            For start As Integer = 0 To mobTokens.Length - deniedTokens.Length
                Dim window As String = String.Join(" ", mobTokens.Skip(start).Take(deniedTokens.Length))
                If AreTextsClose(window, normDenied) Then
                    Return True
                End If
            Next
        End If

        Return False
    End Function

    Private Shared Function AreTextsClose(a As String, b As String) As Boolean
        If String.IsNullOrWhiteSpace(a) OrElse String.IsNullOrWhiteSpace(b) Then
            Return False
        End If

        Dim aa As String = Regex.Replace(a.ToLowerInvariant(), "\s+", " ").Trim()
        Dim bb As String = Regex.Replace(b.ToLowerInvariant(), "\s+", " ").Trim()
        If aa = "" OrElse bb = "" Then
            Return False
        End If

        If aa.Equals(bb, StringComparison.Ordinal) Then
            Return True
        End If
        If aa.Contains(bb, StringComparison.Ordinal) OrElse bb.Contains(aa, StringComparison.Ordinal) Then
            Dim compactContainsA As String = aa.Replace(" ", "")
            Dim compactContainsB As String = bb.Replace(" ", "")
            Dim shortLen As Integer = Math.Min(compactContainsA.Length, compactContainsB.Length)
            Dim longLen As Integer = Math.Max(compactContainsA.Length, compactContainsB.Length)
            Dim coverage As Double = shortLen / Math.Max(1.0, CDbl(longLen))
            ' Avoid token-level partial matches (e.g., "tara" vs "kaulitara").
            If coverage >= 0.78 Then
                Return True
            End If
        End If

        Dim compactA As String = aa.Replace(" ", "")
        Dim compactB As String = bb.Replace(" ", "")
        If compactA = "" OrElse compactB = "" Then
            Return False
        End If

        Dim lenA As Integer = compactA.Length
        Dim lenB As Integer = compactB.Length
        Dim maxLen As Integer = Math.Max(lenA, lenB)
        Dim lenDiff As Integer = Math.Abs(lenA - lenB)

        Dim tolerance As Integer
        If maxLen <= 6 Then
            tolerance = 1
        ElseIf maxLen <= 12 Then
            tolerance = 2
        Else
            tolerance = 3
        End If

        If lenDiff > tolerance Then
            Return False
        End If

        Dim distance As Integer = LevenshteinDistance(compactA, compactB)
        If distance > tolerance Then
            Return False
        End If

        Dim similarity As Double = 1.0 - (distance / Math.Max(1.0, CDbl(maxLen)))
        Return similarity >= 0.72
    End Function

    Private Shared Function LevenshteinDistance(a As String, b As String) As Integer
        If String.IsNullOrEmpty(a) Then
            Return If(b Is Nothing, 0, b.Length)
        End If
        If String.IsNullOrEmpty(b) Then
            Return a.Length
        End If

        Dim n As Integer = a.Length
        Dim m As Integer = b.Length
        Dim d(n, m) As Integer

        For i As Integer = 0 To n
            d(i, 0) = i
        Next
        For j As Integer = 0 To m
            d(0, j) = j
        Next

        For i As Integer = 1 To n
            Dim ca As Char = a(i - 1)
            For j As Integer = 1 To m
                Dim cb As Char = b(j - 1)
                Dim cost As Integer = If(ca = cb, 0, 1)
                d(i, j) = Math.Min(
                    Math.Min(d(i - 1, j) + 1, d(i, j - 1) + 1),
                    d(i - 1, j - 1) + cost)
            Next
        Next

        Return d(n, m)
    End Function

    Private Shared Function ClampLootMatchThresholdPercent(value As Integer) As Integer
        Return Math.Max(50, Math.Min(100, value))
    End Function

    Private Shared Function IsAllowedLootName(rawName As String, allowList As List(Of String), thresholdPercent As Integer) As Boolean
        Dim matchedAllowedName As String = ""
        Return TryFindAllowedLootMatch(rawName, allowList, thresholdPercent, matchedAllowedName)
    End Function

    Private Shared Function TryFindAllowedLootMatch(rawObservedText As String, allowList As List(Of String), thresholdPercent As Integer, ByRef matchedAllowedName As String) As Boolean
        matchedAllowedName = ""
        If String.IsNullOrWhiteSpace(rawObservedText) OrElse allowList Is Nothing OrElse allowList.Count = 0 Then
            Return False
        End If

        Dim candidates As List(Of String) = BuildLootMatchCandidates(rawObservedText)
        If candidates.Count = 0 Then
            Return False
        End If

        Dim threshold As Double = ClampLootMatchThresholdPercent(thresholdPercent) / 100.0
        For Each entry As String In allowList
            Dim originalAllowed As String = If(entry, "").Trim()
            Dim normAllowed As String = NormalizeMobName(originalAllowed)
            If normAllowed = "" Then
                Continue For
            End If

            For Each candidate As String In candidates
                Dim score As Double = GetLootMatchScore(candidate, normAllowed)
                If score >= threshold Then
                    matchedAllowedName = If(originalAllowed = "", normAllowed, originalAllowed)
                    Return True
                End If
            Next
        Next

        Return False
    End Function

    Private Shared Function BuildLootMatchCandidates(rawObservedText As String) As List(Of String)
        Dim candidates As New List(Of String)()
        If String.IsNullOrWhiteSpace(rawObservedText) Then
            Return candidates
        End If

        Dim normFull As String = NormalizeMobName(rawObservedText)
        If normFull <> "" Then
            candidates.Add(normFull)
        End If

        Dim lines As String() = rawObservedText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)
        For Each rawLine As String In lines
            Dim normLine As String = NormalizeMobName(rawLine)
            If normLine = "" Then
                Continue For
            End If
            If Not candidates.Contains(normLine, StringComparer.OrdinalIgnoreCase) Then
                candidates.Add(normLine)
            End If
        Next

        Return candidates
    End Function

    Private Shared Function GetLootMatchScore(normObserved As String, normAllowed As String) As Double
        If String.IsNullOrWhiteSpace(normObserved) OrElse String.IsNullOrWhiteSpace(normAllowed) Then
            Return 0.0
        End If

        Dim compactObserved As String = ToCompactLootText(normObserved)
        Dim compactAllowed As String = ToCompactLootText(normAllowed)
        If compactObserved = "" OrElse compactAllowed = "" Then
            Return 0.0
        End If

        If compactObserved = compactAllowed Then
            Return 1.0
        End If

        Dim bestScore As Double = CalculateCompactSimilarity(compactObserved, compactAllowed)
        If compactObserved.Contains(compactAllowed, StringComparison.Ordinal) OrElse compactAllowed.Contains(compactObserved, StringComparison.Ordinal) Then
            Dim coverage As Double = Math.Min(compactObserved.Length, compactAllowed.Length) / Math.Max(1.0, CDbl(Math.Max(compactObserved.Length, compactAllowed.Length)))
            bestScore = Math.Max(bestScore, coverage)
        End If

        Dim observedTokens As String() = normObserved.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        Dim allowedTokens As String() = normAllowed.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If observedTokens.Length = 0 OrElse allowedTokens.Length = 0 Then
            Return bestScore
        End If

        Dim minWindow As Integer = Math.Max(1, allowedTokens.Length - 1)
        Dim maxWindow As Integer = Math.Min(observedTokens.Length, allowedTokens.Length + 1)
        For windowLen As Integer = minWindow To maxWindow
            For start As Integer = 0 To observedTokens.Length - windowLen
                Dim window As String = String.Join(" ", observedTokens.Skip(start).Take(windowLen))
                Dim windowCompact As String = ToCompactLootText(window)
                If windowCompact = "" Then
                    Continue For
                End If
                bestScore = Math.Max(bestScore, CalculateCompactSimilarity(windowCompact, compactAllowed))
                If bestScore >= 0.999 Then
                    Return 1.0
                End If
            Next
        Next

        Return bestScore
    End Function

    Private Shared Function ToCompactLootText(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return ""
        End If
        Return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", "")
    End Function

    Private Shared Function CalculateCompactSimilarity(compactA As String, compactB As String) As Double
        If String.IsNullOrWhiteSpace(compactA) OrElse String.IsNullOrWhiteSpace(compactB) Then
            Return 0.0
        End If
        If compactA.Equals(compactB, StringComparison.Ordinal) Then
            Return 1.0
        End If
        Dim distance As Integer = LevenshteinDistance(compactA, compactB)
        Dim maxLen As Integer = Math.Max(compactA.Length, compactB.Length)
        Return 1.0 - (distance / Math.Max(1.0, CDbl(maxLen)))
    End Function

    Private Shared Function NormalizeMobName(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If
        Dim cleaned As String = Regex.Replace(raw, "[^A-Za-z0-9 '\-]", " ")
        cleaned = Regex.Replace(cleaned, "\s+", " ").Trim().ToLowerInvariant()
        Return cleaned
    End Function

    Private Sub BeginCombatLock(targetSignature As String, now As DateTime)
        _combatLockActive = True
        _combatLockLostSignalCount = 0
        _combatLockLastSeenAt = now
        Dim normalizedSignature As String = If(targetSignature, "").Trim().ToLowerInvariant()
        If normalizedSignature <> "" Then
            _combatLockTargetSignature = normalizedSignature
        End If
    End Sub

    Private Sub ClearCombatLock()
        _combatLockActive = False
        _combatLockTargetSignature = ""
        _combatLockLostSignalCount = 0
        _combatLockLastSeenAt = DateTime.MinValue
    End Sub

    Private Function UpdateCombatLockState(now As DateTime, cfg As BotConfig, currentTargetAliveSignal As Boolean, normMobName As String) As Boolean
        If Not _combatLockActive Then
            Return False
        End If

        Dim normalizedName As String = If(normMobName, "").Trim().ToLowerInvariant()
        Dim lockTargetMatches As Boolean =
            _combatLockTargetSignature = "" OrElse
            normalizedName = "" OrElse
            _combatLockTargetSignature.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)

        If currentTargetAliveSignal AndAlso lockTargetMatches Then
            _combatLockLostSignalCount = 0
            _combatLockLastSeenAt = now
            If _combatLockTargetSignature = "" AndAlso normalizedName <> "" Then
                _combatLockTargetSignature = normalizedName
            End If
            Return True
        End If

        Dim minHoldMs As Integer = Math.Max(900, Math.Max(1, If(cfg?.RetargetMs, 550)) * 2)
        If _lastAttackAction <> DateTime.MinValue AndAlso (now - _lastAttackAction).TotalMilliseconds < minHoldMs Then
            Return True
        End If

        _combatLockLostSignalCount += 1
        If _combatLockLostSignalCount < CombatLockLostTargetConfirmFrames Then
            Return True
        End If

        ClearCombatLock()
        Return False
    End Function

    Private Shared Function HasLivingTargetSignal(targetWindowVisible As Boolean, mobHpPct As Double, cfg As BotConfig) As Boolean
        If targetWindowVisible Then
            Return True
        End If

        Dim configuredThreshold As Double = If(cfg Is Nothing, 1.0, Math.Max(0.0, cfg.MobHpPresenceThreshold))
        Dim lowHpKeepLockThreshold As Double = Math.Max(0.05, Math.Min(0.25, configuredThreshold * 0.25))
        Return mobHpPct >= lowHpKeepLockThreshold
    End Function

    Private Shared Function GetRetargetCooldownMs(cfg As BotConfig, Optional minimumMs As Integer = 1, Optional forced As Boolean = False) As Integer
        If cfg Is Nothing Then
            Return Math.Max(minimumMs, 1) + RetargetBufferMs
        End If
        Dim configuredMs As Integer = If(forced, cfg.ForcedRetargetMs, cfg.RetargetMs)
        Return Math.Max(Math.Max(1, configuredMs), minimumMs) + RetargetBufferMs
    End Function

    Private Function TrySendRetargetKey(hwnd As IntPtr, cfg As BotConfig, now As DateTime, actionText As String, Optional forced As Boolean = False) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim cooldownMs As Integer = GetRetargetCooldownMs(cfg, 1, forced)
        Dim lastRetargetAt As DateTime = If(forced, _lastForcedRetarget, _lastNormalRetarget)
        If lastRetargetAt <> DateTime.MinValue AndAlso (now - lastRetargetAt).TotalMilliseconds < cooldownMs Then
            Return False
        End If

        If forced Then
            _lastForcedRetarget = now
        Else
            _lastNormalRetarget = now
        End If
        If SendKey(hwnd, "E", FastKeyPressMs) Then
            ClearCombatLock()
            SetLastAction(actionText)
            Return True
        End If

        Return False
    End Function

    Private Function GetLatestRetargetAt() As DateTime
        If _lastNormalRetarget = DateTime.MinValue Then
            Return _lastForcedRetarget
        End If
        If _lastForcedRetarget = DateTime.MinValue Then
            Return _lastNormalRetarget
        End If
        Return If(_lastNormalRetarget >= _lastForcedRetarget, _lastNormalRetarget, _lastForcedRetarget)
    End Function

    Private Sub TrackMobHpMovement(targetValid As Boolean, mobHpPct As Double, now As DateTime)
        If Not targetValid Then
            _lastMobHpSample = -1
            _lastMobHpMovement = DateTime.MinValue
            _noDamageTargetSignature = ""
            _noDamageAttackCount = 0
            Return
        End If

        If _lastMobHpSample < 0 Then
            _lastMobHpSample = mobHpPct
            _lastMobHpMovement = now
            Return
        End If

        Dim hpDrop As Double = _lastMobHpSample - mobHpPct
        If hpDrop >= 0.8 Then
            _lastMobHpSample = mobHpPct
            _lastMobHpMovement = now
            _noDamageAttackCount = 0
            Return
        End If

        ' Ignore small OCR jitter, but re-baseline large upward jumps.
        If mobHpPct >= (_lastMobHpSample + 1.8) Then
            _lastMobHpSample = mobHpPct
        End If
    End Sub

    Private Function ShouldBypassStuckTarget(cfg As BotConfig, targetWindowVisible As Boolean, targetValid As Boolean, now As DateTime) As Boolean
        If Not cfg.BypassStuckTarget Then
            Return False
        End If
        If _lastAttackAction = DateTime.MinValue Then
            Return False
        End If
        If _combatLockActive Then
            Return False
        End If

        Dim stuckMs As Integer = Math.Max(1, cfg.StuckTargetMs)
        Dim sinceAttackMs As Double = (now - _lastAttackAction).TotalMilliseconds
        Dim staleAttackWindowMs As Integer = Math.Max(stuckMs * 5, Math.Max(1, cfg.ForcedRetargetMs) * 6)
        If sinceAttackMs > staleAttackWindowMs Then
            Return False
        End If

        Dim retargetCooldownMs As Integer = GetRetargetCooldownMs(cfg, 1, forced:=True)
        If Not targetValid Then
            Return False
        End If

        If _lastMobHpMovement = DateTime.MinValue Then
            Return False
        End If

        Dim sinceHpMoveMs As Double = (now - _lastMobHpMovement).TotalMilliseconds
        Dim requiredNoProgressMs As Integer = Math.Max(6000, stuckMs * 3)
        If sinceHpMoveMs < requiredNoProgressMs Then
            Return False
        End If

        Return (_lastForcedRetarget = DateTime.MinValue) OrElse (now - _lastForcedRetarget).TotalMilliseconds >= retargetCooldownMs
    End Function

    Private Sub RecordAttackWithoutDamage(targetSignature As String)
        Dim normalizedSignature As String = If(targetSignature, "").Trim().ToLowerInvariant()
        If normalizedSignature = "" Then
            normalizedSignature = "__unknown_target__"
        End If

        If Not _noDamageTargetSignature.Equals(normalizedSignature, StringComparison.OrdinalIgnoreCase) Then
            _noDamageTargetSignature = normalizedSignature
            _noDamageAttackCount = 1
            Return
        End If

        _noDamageAttackCount += 1
    End Sub

    Private Shared Function IsSupportRole(role As String) As Boolean
        Return role = "heal" OrElse role = "max_health" OrElse role = "mana"
    End Function

    Private Shared Function IsSupportTriggered(action As ActionRule, hpPercent As Double, mpPercent As Double) As Boolean
        Select Case action.Role
            Case "heal", "max_health"
                Return hpPercent <= action.TriggerPercent
            Case "mana"
                Return mpPercent <= action.TriggerPercent
            Case Else
                Return False
        End Select
    End Function

    Private Function TrySendSupportActions(cfg As BotConfig, hwnd As IntPtr, hpPercent As Double, mpPercent As Double) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim ordered = cfg.Actions.
            Where(Function(a) a.Enabled AndAlso IsSupportRole(a.Role)).
            OrderBy(Function(a) a.Priority).
            ToList()
        If ordered.Count = 0 Then
            Return False
        End If

        ' Prioritize max-health consumables at low HP, but still allow regular heal/mana fallback.
        Dim maxHealthActions = ordered.
            Where(Function(a) a.Role = "max_health" AndAlso IsSupportTriggered(a, hpPercent, mpPercent)).
            ToList()

        Dim sentAny As Boolean = False
        For Each action In maxHealthActions
            If Not IsReady(action) Then
                Continue For
            End If
            If Not SendKey(hwnd, action.KeyName, FastKeyPressMs) Then
                Continue For
            End If

            MarkKeyUsed(action.KeyName)
            SetLastAction($"{action.KeyName} ({action.Role})")
            sentAny = True
        Next

        For Each action In ordered
            If action.Role = "max_health" Then
                Continue For
            End If
            If Not IsSupportTriggered(action, hpPercent, mpPercent) Then
                Continue For
            End If
            If Not IsReady(action) Then
                Continue For
            End If
            If Not SendKey(hwnd, action.KeyName, FastKeyPressMs) Then
                Continue For
            End If

            MarkKeyUsed(action.KeyName)
            SetLastAction($"{action.KeyName} ({action.Role})")
            sentAny = True
        Next

        Return sentAny
    End Function

    Private Function TrySendStopAction(cfg As BotConfig, hwnd As IntPtr, context As String, Optional includeMovementFallback As Boolean = True) As Boolean
        If hwnd = IntPtr.Zero OrElse cfg Is Nothing OrElse cfg.Actions Is Nothing Then
            Return False
        End If

        Dim releasedMovement As Boolean = False
        If includeMovementFallback Then
            releasedMovement = ReleaseMovementKeys(hwnd)
        End If

        Dim ordered = cfg.Actions.
            Where(Function(a) a.Enabled AndAlso String.Equals(a.Role, "stop", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(a) a.Priority).
            ToList()

        If ordered.Count = 0 Then
            If includeMovementFallback AndAlso releasedMovement Then
                SetLastAction($"movement release (stop fallback: {context})")
            End If
            Return includeMovementFallback AndAlso releasedMovement
        End If

        For Each action In ordered
            If String.IsNullOrWhiteSpace(action.KeyName) Then
                Continue For
            End If

            Dim sentCount As Integer = 0
            For i As Integer = 1 To 3
                If SendKey(hwnd, action.KeyName, FastKeyPressMs) Then
                    sentCount += 1
                    MarkKeyUsed(action.KeyName)
                End If
                Thread.Sleep(StopKeyRepeatGapMs)
            Next

            If sentCount > 0 Then
                SetLastAction($"{action.KeyName} (stop x{sentCount}: {context})")
                Return True
            End If
        Next

        Return includeMovementFallback AndAlso releasedMovement
    End Function

    Public Function HardStopMovement(windowTitle As String, Optional context As String = "manual hard stop") As Boolean
        Dim title As String = If(windowTitle, "").Trim()
        If title = "" Then
            Return False
        End If

        Dim hwnd As IntPtr = FindGameWindow(title)
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim cfg As BotConfig
        SyncLock _sync
            cfg = _config
        End SyncLock
        Return TrySendStopAction(cfg, hwnd, context)
    End Function

    Public Function ManualRetarget(windowTitle As String) As Boolean
        Dim title As String = If(windowTitle, "").Trim()
        If title = "" Then
            Return False
        End If

        Dim hwnd As IntPtr = FindGameWindow(title)
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        If SendKey(hwnd, "E", FastKeyPressMs) Then
            _lastNormalRetarget = DateTime.UtcNow
            SetLastAction("E (manual retarget)")
            Return True
        End If
        Return False
    End Function

    Private Function ChooseAttackBurstActions(cfg As BotConfig, hpPercent As Double, mpPercent As Double, targetValid As Boolean, allowBlindAttack As Boolean, highMaxHpAttackActive As Boolean, ByRef reason As String) As List(Of ActionRule)
        Dim ordered = cfg.Actions.Where(Function(a) a.Enabled).OrderBy(Function(a) a.Priority).ToList()
        If ordered.Count = 0 Then
            reason = "No enabled keys."
            Return New List(Of ActionRule)()
        End If

        Dim hasAttackKey As Boolean = False
        Dim statBlocked As Boolean = False
        Dim cooldownBlocked As Boolean = False
        Dim selected As New List(Of ActionRule)()
        Dim usedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each action In ordered
            Dim isAttackLike As Boolean =
                action.Role = "attack" OrElse
                action.Role = "special" OrElse
                (action.Role = "high_max_hp" AndAlso highMaxHpAttackActive)
            If Not isAttackLike Then
                Continue For
            End If
            hasAttackKey = True

            If (Not cfg.BypassHpMpLimits) AndAlso (hpPercent < action.MinHpPercent OrElse mpPercent < action.MinMpPercent) Then
                statBlocked = True
                Continue For
            End If

            If Not IsReady(action) Then
                cooldownBlocked = True
                Continue For
            End If

            If targetValid OrElse allowBlindAttack Then
                If Not usedKeys.Add(action.KeyName) Then
                    Continue For
                End If

                selected.Add(action)
                If selected.Count >= AttackBurstKeysPerLoop Then
                    Exit For
                End If
            End If
        Next

        If selected.Count > 0 Then
            reason = ""
            Return selected
        End If

        If Not hasAttackKey Then
            reason = "No enabled attack/special/high_max_hp keys."
        ElseIf Not targetValid AndAlso Not allowBlindAttack Then
            reason = "No target detected."
        ElseIf statBlocked AndAlso (Not cfg.BypassHpMpLimits) Then
            reason = "HP/MP limits blocked all attack keys."
        ElseIf cooldownBlocked Then
            reason = "All attack keys are on cooldown."
        Else
            reason = "No eligible attack key."
        End If

        Return selected
    End Function

    Private Function IsReady(action As ActionRule) As Boolean
        SyncLock _sync
            If Not _lastKeyTime.ContainsKey(action.KeyName) Then
                Return True
            End If
            Dim elapsedMs As Double = (DateTime.UtcNow - _lastKeyTime(action.KeyName)).TotalMilliseconds
            Return elapsedMs >= action.CooldownMs
        End SyncLock
    End Function

    Private Sub MarkKeyUsed(keyName As String)
        If String.IsNullOrWhiteSpace(keyName) Then
            Return
        End If
        SyncLock _sync
            _lastKeyTime(keyName) = DateTime.UtcNow
        End SyncLock
    End Sub

    Private Sub SetLastAction(text As String)
        SetStatus(Sub(s)
                      s.LastAction = text
                  End Sub)
        RaiseEvent LogLine($"Key action: {text}")
    End Sub

    Private Sub SetStatus(updateAction As Action(Of BotStatus))
        Dim snapshot As BotStatus
        SyncLock _sync
            updateAction(_status)
            _status.AgentEnabled = _config IsNot Nothing AndAlso _config.LevelingAgentEnabled
            _status.AgentState = _agentState.ToString()
            _status.AgentReason = _agentReason
            _status.AgentGuardrailTriggered = _agentGuardrailTriggered
            _status.MapCoordinateText = _lastMapCoordinateText
            _status.MapCoordinateX = _lastMapCoordinateX
            _status.MapCoordinateY = _lastMapCoordinateY
            _status.ChatOcrText = _lastChatOcrText
            _status.ChatOcrUpdatedAt = _lastChatOcrUpdatedAt
            _status.MapHeading = If(String.IsNullOrWhiteSpace(_lastNavigationKnownHeading), "", $"{_lastNavigationKnownHeading} (from coordinates)")
            _status.MapCoordinateConfidence = _lastMapCoordinateConfidence
            _status.MapMarkerDetected = _lastMapMarkerDetected
            _status.MapMarkerX = _lastMapMarkerX
            _status.MapMarkerY = _lastMapMarkerY
            _status.MapLocalizationConfidence = _lastMapLocalizationConfidence
            _status.MapVisible = _lastMapVisible
            _status.NavigationMapName = _lastNavigationMapName
            _status.NavigationCurrentNodeId = _lastNavigationCurrentNodeId
            _status.NavigationCurrentNodeLabel = _lastNavigationCurrentNodeLabel
            _status.NavigationNextWaypointId = _lastNavigationNextWaypointId
            _status.NavigationNextWaypointLabel = _lastNavigationNextWaypointLabel
            _status.NavigationRouteText = _lastNavigationRouteText
            _status.NavigationRouteReady = _lastNavigationRouteReady
            _status.NavigationTravelPreviewEnabled = _config IsNot Nothing AndAlso _config.NavigationTravelPreviewEnabled
            _status.NavigationTravelExecutionEnabled = _config IsNot Nothing AndAlso _config.NavigationTravelExecutionEnabled
            _status.NavigationTravelActive = _lastNavigationTravelActive
            _status.NavigationTravelReason = _lastNavigationTravelReason
            _status.NavigationDistanceToWaypoint = _lastNavigationDistanceToWaypoint
            _status.NavigationTravelStalled = _lastNavigationTravelStalled
            _status.NavigationRecoveryCount = _lastNavigationRecoveryCount
            _status.NavigationDestinationReached = _lastNavigationDestinationReached
            _status.NavigationDestinationLabel = _lastNavigationDestinationLabel
            _status.RouteRecordingEnabled = _config IsNot Nothing AndAlso _config.RouteRecordingEnabled
            _status.RouteRecordingActive = _routeRecordingCaptureActive
            _status.RouteRecordingMapName = _routeRecordingMapName
            _status.RouteRecordingName = _routeRecordingName
            _status.RouteRecordingSampleCount = _routeRecordingSamples.Count
            _status.RouteRecordingStatus = _routeRecordingStatus
            _status.RouteRecordingLastSavedPath = _routeRecordingLastSavedPath
            _status.PartySize = _lastPartySize
            _status.PartyAliveCount = _lastPartyAliveCount
            _status.PartyAllAlive = _lastPartyAllAlive
            _status.UpdatedAt = DateTime.UtcNow
            snapshot = CloneStatus(_status)
        End SyncLock
        RaiseEvent StatusUpdated(snapshot)
    End Sub

    Private Function CloneStatus(src As BotStatus) As BotStatus
        Return New BotStatus With {
            .Running = src.Running,
            .WindowFound = src.WindowFound,
            .HpPercent = src.HpPercent,
            .MpPercent = src.MpPercent,
            .MobHpPercent = src.MobHpPercent,
            .MobMaxHp = src.MobMaxHp,
            .MobHpText = src.MobHpText,
            .ExpPercent = src.ExpPercent,
            .ExpPerHour = src.ExpPerHour,
            .RupiahsTotal = src.RupiahsTotal,
            .RupiahsPerHour = src.RupiahsPerHour,
            .PartySize = src.PartySize,
            .PartyAliveCount = src.PartyAliveCount,
            .PartyAllAlive = src.PartyAllAlive,
            .MobName = src.MobName,
            .TargetValid = src.TargetValid,
            .MapCoordinateText = src.MapCoordinateText,
            .MapCoordinateX = src.MapCoordinateX,
            .MapCoordinateY = src.MapCoordinateY,
            .ChatOcrText = src.ChatOcrText,
            .ChatOcrUpdatedAt = src.ChatOcrUpdatedAt,
            .MapHeading = src.MapHeading,
            .MapCoordinateConfidence = src.MapCoordinateConfidence,
            .MapMarkerDetected = src.MapMarkerDetected,
            .MapMarkerX = src.MapMarkerX,
            .MapMarkerY = src.MapMarkerY,
            .MapLocalizationConfidence = src.MapLocalizationConfidence,
            .MapVisible = src.MapVisible,
            .NavigationMapName = src.NavigationMapName,
            .NavigationCurrentNodeId = src.NavigationCurrentNodeId,
            .NavigationCurrentNodeLabel = src.NavigationCurrentNodeLabel,
            .NavigationNextWaypointId = src.NavigationNextWaypointId,
            .NavigationNextWaypointLabel = src.NavigationNextWaypointLabel,
            .NavigationRouteText = src.NavigationRouteText,
            .NavigationRouteReady = src.NavigationRouteReady,
            .NavigationTravelPreviewEnabled = src.NavigationTravelPreviewEnabled,
            .NavigationTravelExecutionEnabled = src.NavigationTravelExecutionEnabled,
            .NavigationTravelActive = src.NavigationTravelActive,
            .NavigationTravelReason = src.NavigationTravelReason,
            .NavigationDistanceToWaypoint = src.NavigationDistanceToWaypoint,
            .NavigationTravelStalled = src.NavigationTravelStalled,
            .NavigationRecoveryCount = src.NavigationRecoveryCount,
            .NavigationDestinationReached = src.NavigationDestinationReached,
            .NavigationDestinationLabel = src.NavigationDestinationLabel,
            .RouteRecordingEnabled = src.RouteRecordingEnabled,
            .RouteRecordingActive = src.RouteRecordingActive,
            .RouteRecordingMapName = src.RouteRecordingMapName,
            .RouteRecordingName = src.RouteRecordingName,
            .RouteRecordingSampleCount = src.RouteRecordingSampleCount,
            .RouteRecordingStatus = src.RouteRecordingStatus,
            .RouteRecordingLastSavedPath = src.RouteRecordingLastSavedPath,
            .LastAction = src.LastAction,
            .NotAttackingReason = src.NotAttackingReason,
            .ErrorMessage = src.ErrorMessage,
            .AgentEnabled = src.AgentEnabled,
            .AgentState = src.AgentState,
            .AgentReason = src.AgentReason,
            .AgentGuardrailTriggered = src.AgentGuardrailTriggered,
            .UpdatedAt = src.UpdatedAt
        }
    End Function

    Public Shared Function FindGameWindow(windowTitle As String) As IntPtr
        If String.IsNullOrWhiteSpace(windowTitle) Then
            Return IntPtr.Zero
        End If

        Dim myPid As Integer = Process.GetCurrentProcess().Id
        Dim best As IntPtr = IntPtr.Zero
        Dim bestScore As Long = Long.MinValue
        Dim needle As String = windowTitle.Trim()

        NativeMethods.EnumWindows(
            Function(hWnd As IntPtr, _lParam As IntPtr) As Boolean
                If Not NativeMethods.IsWindowVisible(hWnd) Then
                    Return True
                End If
                If NativeMethods.IsIconic(hWnd) Then
                    Return True
                End If

                Dim sb As New StringBuilder(512)
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity)
                Dim title As String = sb.ToString()
                If title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 Then
                    Return True
                End If

                Dim pid As UInteger = 0UI
                NativeMethods.GetWindowThreadProcessId(hWnd, pid)
                If CInt(pid) = myPid Then
                    Return True
                End If

                Dim rc As NativeMethods.RECT
                If Not NativeMethods.GetClientRect(hWnd, rc) Then
                    Return True
                End If

                Dim w As Integer = Math.Max(0, rc.Right - rc.Left)
                Dim h As Integer = Math.Max(0, rc.Bottom - rc.Top)
                If w = 0 OrElse h = 0 Then
                    Return True
                End If

                Dim score As Long = CLng(w) * CLng(h)
                If title.Equals(needle, StringComparison.OrdinalIgnoreCase) Then
                    score += 1000000000000L
                End If
                If score > bestScore Then
                    bestScore = score
                    best = hWnd
                End If

                Return True
            End Function, IntPtr.Zero)
        Return best
    End Function

    Public Shared Function TryGetClientScreenRect(windowTitle As String, ByRef rect As Rectangle) As Boolean
        Dim hwnd As IntPtr = FindGameWindow(windowTitle)
        If hwnd = IntPtr.Zero Then
            rect = Rectangle.Empty
            Return False
        End If

        Dim rc As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rc) Then
            rect = Rectangle.Empty
            Return False
        End If

        Dim pt As New NativeMethods.POINT With {.X = rc.Left, .Y = rc.Top}
        If Not NativeMethods.ClientToScreen(hwnd, pt) Then
            rect = Rectangle.Empty
            Return False
        End If

        Dim w As Integer = Math.Max(1, rc.Right - rc.Left)
        Dim h As Integer = Math.Max(1, rc.Bottom - rc.Top)
        rect = New Rectangle(pt.X, pt.Y, w, h)
        Return True
    End Function

    Public Shared Function CaptureClient(hwnd As IntPtr) As Bitmap
        Dim rc As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rc) Then
            Return Nothing
        End If

        Dim width As Integer = Math.Max(1, rc.Right - rc.Left)
        Dim height As Integer = Math.Max(1, rc.Bottom - rc.Top)
        Dim bmp As New Bitmap(width, height, PixelFormat.Format24bppRgb)

        Try
            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_RENDERFULLCONTENT) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY Or NativeMethods.PW_RENDERFULLCONTENT) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, 0UI) Then
                Return bmp
            End If

            If TryCaptureWithCopyFromScreen(hwnd, bmp, width, height) Then
                Return bmp
            End If

            Thread.Sleep(10)
            If TryCaptureWithCopyFromScreen(hwnd, bmp, width, height) Then
                Return bmp
            End If

            bmp.Dispose()
            Return Nothing
        Catch
            bmp.Dispose()
            Return Nothing
        End Try
    End Function

    Private Shared Function TryCaptureWithPrintWindow(hwnd As IntPtr, bmp As Bitmap, flags As UInteger) As Boolean
        Using g As Graphics = Graphics.FromImage(bmp)
            Dim hdc As IntPtr = g.GetHdc()
            Try
                Dim ok As Boolean = NativeMethods.PrintWindow(hwnd, hdc, flags)
                Return ok AndAlso (Not IsLikelyBlackFrame(bmp))
            Finally
                g.ReleaseHdc(hdc)
            End Try
        End Using
    End Function

    Private Shared Function TryCaptureWithCopyFromScreen(hwnd As IntPtr, bmp As Bitmap, width As Integer, height As Integer) As Boolean
        Dim pt As New NativeMethods.POINT With {.X = 0, .Y = 0}
        If Not NativeMethods.ClientToScreen(hwnd, pt) Then
            Return False
        End If

        Using g As Graphics = Graphics.FromImage(bmp)
            g.CopyFromScreen(pt.X, pt.Y, 0, 0, New Size(width, height), CopyPixelOperation.SourceCopy Or NativeMethods.CAPTUREBLT)
        End Using
        Return Not IsLikelyBlackFrame(bmp)
    End Function

    Private Shared Function IsLikelyBlackFrame(bmp As Bitmap) As Boolean
        Dim stepX As Integer = Math.Max(1, bmp.Width \ 10)
        Dim stepY As Integer = Math.Max(1, bmp.Height \ 10)
        Dim samples As Integer = 0
        Dim darkSamples As Integer = 0
        Dim sumLuma As Long = 0

        For y As Integer = 0 To bmp.Height - 1 Step stepY
            For x As Integer = 0 To bmp.Width - 1 Step stepX
                Dim c As Color = bmp.GetPixel(x, y)
                samples += 1
                Dim luma As Integer = (CInt(c.R) * 30 + CInt(c.G) * 59 + CInt(c.B) * 11) \ 100
                sumLuma += luma
                If luma <= 8 Then
                    darkSamples += 1
                End If
            Next
        Next

        If samples = 0 Then
            Return True
        End If

        Dim darkRatio As Double = darkSamples / CDbl(samples)
        Dim avgLuma As Double = sumLuma / CDbl(samples)
        Return darkRatio >= 0.96 AndAlso avgLuma <= 10.0
    End Function

    Private Shared Function ComputeBarPercent(frame As Bitmap, region As RectRegion, isHp As Boolean) As Double
        If frame Is Nothing Then
            Return 0
        End If

        Dim outerRect As Rectangle = region.Clamp(frame.Width, frame.Height)
        Dim rect As Rectangle = outerRect
        If rect.Width > 3 Then
            rect.X += 1
            rect.Width -= 2
        End If
        If rect.Height > 3 Then
            rect.Y += 1
            rect.Height -= 2
        End If

        Dim leadingEdgeRatio As Double = ComputeLeadingEdgeFillRatio(frame, rect, isHp)

        Dim columnMinPixels As Integer = Math.Max(1, CInt(Math.Ceiling(rect.Height * 0.1)))
        Dim gapTolerance As Integer = Math.Max(2, CInt(Math.Ceiling(rect.Width * 0.02)))
        Dim rightMost As Integer = -1
        Dim activeStarted As Boolean = False
        Dim gapCount As Integer = 0

        For x As Integer = 0 To rect.Width - 1
            Dim colored As Integer = 0
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim c As Color = frame.GetPixel(px, y)
                If isHp Then
                    If IsHpColor(c) Then
                        colored += 1
                    End If
                Else
                    If IsMpColor(c) Then
                        colored += 1
                    End If
                End If
            Next

            Dim isActive As Boolean = colored >= columnMinPixels
            If isActive Then
                activeStarted = True
                rightMost = x
                gapCount = 0
            ElseIf activeStarted Then
                gapCount += 1
                If gapCount > gapTolerance Then
                    Exit For
                End If
            End If
        Next

        If rightMost < 0 OrElse rect.Width <= 0 Then
            Return ComputeBarPercentAdaptive(frame, rect, isHp)
        End If

        Dim colorPercent As Double = Math.Max(0, Math.Min(100, (rightMost + 1) * 100.0 / rect.Width))
        If colorPercent >= 3.0 AndAlso leadingEdgeRatio < 0.02 Then
            Return 0
        End If
        If colorPercent < 2.0 Then
            Dim adaptive As Double = ComputeBarPercentAdaptive(frame, rect, isHp)
            If adaptive > colorPercent Then
                Return adaptive
            End If
        End If
        Return colorPercent
    End Function

    Private Shared Function ComputeBarPercentAdaptive(frame As Bitmap, rect As Rectangle, isHp As Boolean) As Double
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim leadingEdgeRatio As Double = ComputeLeadingEdgeFillRatio(frame, rect, isHp)

        Dim scores(rect.Width - 1) As Long
        Dim maxScore As Long = 0

        For x As Integer = 0 To rect.Width - 1
            Dim score As Long = 0
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim c As Color = frame.GetPixel(px, y)
                Dim r As Integer = CInt(c.R)
                Dim g As Integer = CInt(c.G)
                Dim b As Integer = CInt(c.B)
                Dim dominance As Integer
                If isHp Then
                    dominance = r - ((g + b) \ 2)
                Else
                    dominance = b - ((r + g) \ 2)
                End If
                If dominance > 0 Then
                    score += dominance
                End If
            Next
            scores(x) = score
            If score > maxScore Then
                maxScore = score
            End If
        Next

        If maxScore <= 0 Then
            Return 0
        End If

        Dim threshold As Long = Math.Max(CLng(Math.Round(maxScore * 0.24R)), Math.Max(8L, CLng(rect.Height) * 3L))
        Dim localGapTolerance As Integer = Math.Max(2, CInt(Math.Ceiling(rect.Width * 0.03)))
        Dim rightMost As Integer = -1
        Dim activeStarted As Boolean = False
        Dim gapCount As Integer = 0

        For x As Integer = 0 To rect.Width - 1
            Dim isActive As Boolean = scores(x) >= threshold
            If isActive Then
                activeStarted = True
                rightMost = x
                gapCount = 0
            ElseIf activeStarted Then
                gapCount += 1
                If gapCount > localGapTolerance Then
                    Exit For
                End If
            End If
        Next

        If rightMost < 0 Then
            Return 0
        End If
        Dim adaptivePercent As Double = Math.Max(0, Math.Min(100, (rightMost + 1) * 100.0 / rect.Width))
        If adaptivePercent >= 3.0 AndAlso leadingEdgeRatio < 0.02 Then
            Return 0
        End If
        Return adaptivePercent
    End Function

    Private Shared Function ComputeLeadingEdgeFillRatio(frame As Bitmap, rect As Rectangle, isHp As Boolean) As Double
        If frame Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim edgeCols As Integer = Math.Max(2, Math.Min(rect.Width, CInt(Math.Ceiling(rect.Width * 0.12))))
        Dim colored As Integer = 0
        Dim total As Integer = edgeCols * rect.Height
        If total <= 0 Then
            Return 0
        End If

        For x As Integer = 0 To edgeCols - 1
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim c As Color = frame.GetPixel(px, y)
                If isHp Then
                    If IsHpColor(c) Then
                        colored += 1
                    End If
                Else
                    If IsMpColor(c) Then
                        colored += 1
                    End If
                End If
            Next
        Next

        Return colored / CDbl(total)
    End Function

    Private Shared Function HasTargetWindowSignal(frame As Bitmap, mobHpRegion As RectRegion, mobName As String, mobHpPct As Double) As Boolean
        If frame Is Nothing Then
            Return False
        End If

        Dim outerRect As Rectangle = mobHpRegion.Clamp(frame.Width, frame.Height)
        Dim rect As Rectangle = outerRect
        If rect.Width > 3 Then
            rect.X += 1
            rect.Width -= 2
        End If
        If rect.Height > 3 Then
            rect.Y += 1
            rect.Height -= 2
        End If

        Dim edgeFill As Double = ComputeLeadingEdgeFillRatio(frame, rect, True)
        Dim colorFill As Double = ComputeColorFillRatio(frame, rect, True)
        Dim hasName As Boolean = Not String.IsNullOrWhiteSpace(mobName)

        If edgeFill >= 0.04 AndAlso colorFill >= 0.01 Then
            Return True
        End If

        If hasName AndAlso mobHpPct > 0.0 AndAlso edgeFill >= 0.015 AndAlso colorFill >= 0.004 Then
            Return True
        End If

        Return False
    End Function

    Private Shared Function ComputeColorFillRatio(frame As Bitmap, rect As Rectangle, isHp As Boolean) As Double
        If frame Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim colored As Integer = 0
        Dim total As Integer = rect.Width * rect.Height
        If total <= 0 Then
            Return 0
        End If

        For y As Integer = rect.Top To rect.Bottom - 1
            For x As Integer = rect.Left To rect.Right - 1
                Dim c As Color = frame.GetPixel(x, y)
                If isHp Then
                    If IsHpColor(c) Then
                        colored += 1
                    End If
                Else
                    If IsMpColor(c) Then
                        colored += 1
                    End If
                End If
            Next
        Next

        Return colored / CDbl(total)
    End Function

    Private Shared Sub ResolveVisionRegions(cfg As BotConfig, frameWidth As Integer, frameHeight As Integer, ByRef hpBar As RectRegion, ByRef mpBar As RectRegion, ByRef mobNameRect As RectRegion, ByRef mobHpRect As RectRegion, ByRef unreachableTextRect As RectRegion, ByRef pranaExpRect As RectRegion, ByRef rupiahsRect As RectRegion, ByRef partyInviteScanRect As RectRegion, ByRef partyInviteOkRect As RectRegion, ByRef partyListRect As RectRegion, ByRef mapRect As RectRegion, ByRef mapCoordinateRect As RectRegion, ByRef chatRect As RectRegion)
        hpBar = CloneRegion(cfg.HpBar)
        mpBar = CloneRegion(cfg.MpBar)
        mobNameRect = CloneRegion(cfg.MobNameRect)
        mobHpRect = CloneRegion(cfg.MobHpRect)
        unreachableTextRect = CloneRegion(cfg.UnreachableTextRect)
        pranaExpRect = CloneRegion(cfg.PranaExpRect)
        rupiahsRect = CloneRegion(cfg.RupiahsRect)
        partyInviteScanRect = CloneRegion(cfg.PartyInviteScanRect)
        partyInviteOkRect = CloneRegion(cfg.PartyInviteOkRect)
        partyListRect = CloneRegion(cfg.PartyListRect)
        mapRect = CloneRegion(cfg.MapRect)
        mapCoordinateRect = CloneRegion(cfg.MapCoordinateRect)
        chatRect = CloneRegion(cfg.ChatRect)

        If frameWidth <= 0 OrElse frameHeight <= 0 Then
            Exit Sub
        End If
        If Not IsDefaultVisionLayout(cfg) Then
            Exit Sub
        End If
        If frameWidth = BaseClientWidth AndAlso frameHeight = BaseClientHeight Then
            Exit Sub
        End If

        Dim sx As Double = frameWidth / CDbl(BaseClientWidth)
        Dim sy As Double = frameHeight / CDbl(BaseClientHeight)
        hpBar = ScaleRegionLeftTop(cfg.HpBar, sx, sy)
        mpBar = ScaleRegionLeftTop(cfg.MpBar, sx, sy)
        mobNameRect = ScaleRegionRightTop(cfg.MobNameRect, sx, sy, frameWidth)
        mobHpRect = ScaleRegionRightTop(cfg.MobHpRect, sx, sy, frameWidth)
        unreachableTextRect = ScaleRegionLeftTop(cfg.UnreachableTextRect, sx, sy)
        pranaExpRect = ScaleRegionLeftTop(cfg.PranaExpRect, sx, sy)
        rupiahsRect = ScaleRegionLeftTop(cfg.RupiahsRect, sx, sy)
        partyInviteScanRect = ScaleRegionLeftTop(cfg.PartyInviteScanRect, sx, sy)
        partyInviteOkRect = ScaleRegionLeftTop(cfg.PartyInviteOkRect, sx, sy)
        partyListRect = ScaleRegionLeftTop(cfg.PartyListRect, sx, sy)
        mapRect = ScaleRegionLeftTop(cfg.MapRect, sx, sy)
        mapCoordinateRect = ScaleRegionLeftTop(cfg.MapCoordinateRect, sx, sy)
        chatRect = ScaleRegionLeftTop(cfg.ChatRect, sx, sy)
    End Sub

    Private Shared Function ResolveLootScanPolygon(cfg As BotConfig, frameWidth As Integer, frameHeight As Integer) As List(Of DrawingPoint)
        Dim points As List(Of DrawingPoint) = GetEffectiveLootScanPolygon(cfg)
        If frameWidth <= 0 OrElse frameHeight <= 0 Then
            Return points
        End If

        If IsDefaultVisionLayout(cfg) AndAlso Not (frameWidth = BaseClientWidth AndAlso frameHeight = BaseClientHeight) Then
            Dim sx As Double = frameWidth / CDbl(BaseClientWidth)
            Dim sy As Double = frameHeight / CDbl(BaseClientHeight)
            points = points.Select(Function(pt) New DrawingPoint(CInt(Math.Round(pt.X * sx)), CInt(Math.Round(pt.Y * sy)))).ToList()
        End If

        Return points.Select(Function(pt) New DrawingPoint(Math.Max(0, Math.Min(frameWidth - 1, pt.X)), Math.Max(0, Math.Min(frameHeight - 1, pt.Y)))).ToList()
    End Function

    Private Shared Function IsDefaultVisionLayout(cfg As BotConfig) As Boolean
        Return SameRegion(cfg.HpBar, New RectRegion(11, 25, 151, 11)) AndAlso
               SameRegion(cfg.MpBar, New RectRegion(3, 40, 161, 11)) AndAlso
               SameRegion(cfg.MobNameRect, New RectRegion(860, 711, 162, 23)) AndAlso
               SameRegion(cfg.MobHpRect, New RectRegion(859, 737, 165, 11)) AndAlso
               SameRegion(cfg.UnreachableTextRect, New RectRegion(15, 582, 128, 22)) AndAlso
               SameRegion(cfg.PranaExpRect, New RectRegion(472, 745, 78, 21)) AndAlso
               SameRegion(cfg.RupiahsRect, New RectRegion(560, 745, 110, 21)) AndAlso
               SameRegion(cfg.PartyInviteScanRect, New RectRegion(349, 318, 328, 124)) AndAlso
               SameRegion(cfg.PartyInviteOkRect, New RectRegion(463, 410, 59, 21)) AndAlso
               SameRegion(cfg.PartyListRect, New RectRegion(0, 24, 168, 244)) AndAlso
               SameRegion(cfg.MapRect, New RectRegion(0, 0, 1024, 768)) AndAlso
               SameRegion(cfg.MapCoordinateRect, New RectRegion(6, 744, 120, 22)) AndAlso
               SameRegion(cfg.ChatRect, New RectRegion(18, 548, 430, 144)) AndAlso
               SameLootScanPolygon(cfg.LootScanPoints, BotConfig.CreateDefaultLootScanPoints())
    End Function

    Private Shared Function SameLootScanPolygon(a As List(Of LootScanPoint), b As List(Of LootScanPoint)) As Boolean
        If a Is Nothing OrElse b Is Nothing OrElse a.Count <> b.Count Then
            Return False
        End If

        For i As Integer = 0 To a.Count - 1
            If a(i) Is Nothing OrElse b(i) Is Nothing Then
                Return False
            End If
            If a(i).X <> b(i).X OrElse a(i).Y <> b(i).Y Then
                Return False
            End If
        Next

        Return True
    End Function

    Private Shared Function GetEffectiveLootScanPolygon(cfg As BotConfig) As List(Of DrawingPoint)
        Dim points As List(Of LootScanPoint) = If(cfg?.LootScanPoints, Nothing)
        If points IsNot Nothing AndAlso points.Count >= 3 Then
            Return points.Where(Function(pt) pt IsNot Nothing).Select(Function(pt) New DrawingPoint(pt.X, pt.Y)).ToList()
        End If

        Dim legacyRect As RectRegion = If(cfg?.LootScanRect, Nothing)
        If legacyRect IsNot Nothing AndAlso legacyRect.W > 0 AndAlso legacyRect.H > 0 Then
            Return New List(Of DrawingPoint) From {
                New DrawingPoint(legacyRect.X, legacyRect.Y),
                New DrawingPoint(legacyRect.X + legacyRect.W, legacyRect.Y),
                New DrawingPoint(legacyRect.X + legacyRect.W, legacyRect.Y + legacyRect.H),
                New DrawingPoint(legacyRect.X, legacyRect.Y + legacyRect.H)
            }
        End If

        Return BotConfig.CreateDefaultLootScanPoints().Select(Function(pt) New DrawingPoint(pt.X, pt.Y)).ToList()
    End Function

    Private Shared Function ClonePointList(points As List(Of DrawingPoint)) As List(Of DrawingPoint)
        If points Is Nothing Then
            Return New List(Of DrawingPoint)()
        End If
        Return points.Select(Function(pt) New DrawingPoint(pt.X, pt.Y)).ToList()
    End Function

    Private Shared Function SameRegion(a As RectRegion, b As RectRegion) As Boolean
        Return a IsNot Nothing AndAlso b IsNot Nothing AndAlso a.X = b.X AndAlso a.Y = b.Y AndAlso a.W = b.W AndAlso a.H = b.H
    End Function

    Private Shared Function CloneRegion(src As RectRegion) As RectRegion
        If src Is Nothing Then
            Return New RectRegion(0, 0, 1, 1)
        End If
        Return New RectRegion(src.X, src.Y, Math.Max(1, src.W), Math.Max(1, src.H))
    End Function

    Private Shared Function ScaleRegionLeftTop(src As RectRegion, sx As Double, sy As Double) As RectRegion
        Return New RectRegion(
            CInt(Math.Round(src.X * sx)),
            CInt(Math.Round(src.Y * sy)),
            Math.Max(1, CInt(Math.Round(src.W * sx))),
            Math.Max(1, CInt(Math.Round(src.H * sy))))
    End Function

    Private Shared Function ScaleRegionRightTop(src As RectRegion, sx As Double, sy As Double, frameWidth As Integer) As RectRegion
        Dim scaledW As Integer = Math.Max(1, CInt(Math.Round(src.W * sx)))
        Dim scaledH As Integer = Math.Max(1, CInt(Math.Round(src.H * sy)))
        Dim baseMarginRight As Integer = Math.Max(0, BaseClientWidth - (src.X + src.W))
        Dim scaledMarginRight As Integer = Math.Max(0, CInt(Math.Round(baseMarginRight * sx)))
        Dim scaledX As Integer = Math.Max(0, frameWidth - scaledMarginRight - scaledW)
        Dim scaledY As Integer = CInt(Math.Round(src.Y * sy))
        Return New RectRegion(scaledX, scaledY, scaledW, scaledH)
    End Function

    Private Shared Function IsHpColor(c As Color) As Boolean
        Dim sat As Double = c.GetSaturation()
        Dim bright As Double = c.GetBrightness()
        If sat < 0.18 OrElse bright < 0.06 Then
            Return False
        End If

        Dim hue As Double = c.GetHue()
        Dim redHue As Boolean = (hue <= 22.0 OrElse hue >= 338.0)
        Dim redDominant As Boolean = c.R >= (c.G + 12) AndAlso c.R >= (c.B + 12)
        Return redHue AndAlso redDominant
    End Function

    Private Shared Function IsMpColor(c As Color) As Boolean
        Dim sat As Double = c.GetSaturation()
        Dim bright As Double = c.GetBrightness()
        If sat < 0.16 OrElse bright < 0.06 Then
            Return False
        End If

        Dim hue As Double = c.GetHue()
        Dim blueHue As Boolean = (hue >= 185.0 AndAlso hue <= 255.0)
        Dim blueDominant As Boolean = c.B >= (c.R + 8) AndAlso c.B >= (c.G + 6)
        Return blueHue AndAlso blueDominant
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Shared Sub keybd_event(bVk As Byte, bScan As Byte, dwFlags As UInteger, dwExtraInfo As UIntPtr)
    End Sub

    Public Shared Function SendKey(hwnd As IntPtr, keyName As String, pressMs As Integer, Optional forceBackgroundPost As Boolean = False) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim vk As Integer
        If Not KeyMap.TryGetValue(keyName.ToUpperInvariant(), vk) Then
            Return False
        End If

        Dim usePhysicalKeyEvent As Boolean =
            vk = &HA4 OrElse
            vk = &HA5 OrElse
            vk = &H12 OrElse
            vk = &H57 OrElse
            vk = &H41 OrElse
            vk = &H53 OrElse
            vk = &H44

        ' Use keybd_event for ALT and movement keys because many games ignore PostMessage for them.
        If usePhysicalKeyEvent AndAlso Not forceBackgroundPost Then
            Dim foregroundHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            If foregroundHwnd <> hwnd Then
                NativeMethods.SetForegroundWindow(hwnd)
                Thread.Sleep(ForegroundInputSettleMs)
            End If

            Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(vk), 0UI))
            Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1
            Dim KEYEVENTF_KEYUP As UInteger = &H2
            
            Dim flagsDown As UInteger = 0
            Dim flagsUp As UInteger = KEYEVENTF_KEYUP
            If vk = &HA5 Then ' RMENU
                flagsDown = flagsDown Or KEYEVENTF_EXTENDEDKEY
                flagsUp = flagsUp Or KEYEVENTF_EXTENDEDKEY
            End If

            Try
                keybd_event(CByte(vk), scan, flagsDown, UIntPtr.Zero)
                Thread.Sleep(Math.Max(5, pressMs))
                keybd_event(CByte(vk), scan, flagsUp, UIntPtr.Zero)
                Return True
            Catch
                Return False
            End Try
        End If

        Dim scanPost As UInteger = NativeMethods.MapVirtualKey(CUInt(vk), 0UI)
        Dim lparamDown As Integer = 1 Or (CInt(scanPost) << 16)
        Dim lparamUp As Integer = lparamDown Or (1 << 30) Or (1 << 31)

        Try
            NativeMethods.PostMessage(hwnd, CUInt(&H100), New IntPtr(vk), New IntPtr(lparamDown))
            Thread.Sleep(Math.Max(5, pressMs))
            NativeMethods.PostMessage(hwnd, CUInt(&H101), New IntPtr(vk), New IntPtr(lparamUp))
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ReleaseMovementKeys(hwnd As IntPtr) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim sentAny As Boolean = False
        For Each vk As Integer In MovementStopVks
            If SendVirtualKeyUp(hwnd, vk) Then
                sentAny = True
            End If
        Next
        Return sentAny
    End Function

    Private Shared Function SendVirtualKeyUp(hwnd As IntPtr, vk As Integer) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim scan As UInteger = NativeMethods.MapVirtualKey(CUInt(vk), 0UI)
        Dim lparamUp As Integer = 1 Or (CInt(scan) << 16) Or (1 << 30) Or (1 << 31)
        Try
            Return NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_KEYUP), New IntPtr(vk), New IntPtr(lparamUp))
        Catch
            Return False
        End Try
    End Function

    Public Shared Function ClickClientRegionCenter(hwnd As IntPtr, region As RectRegion, clientWidth As Integer, clientHeight As Integer) As Boolean
        If hwnd = IntPtr.Zero OrElse region Is Nothing Then
            Return False
        End If

        Dim rect As Rectangle = region.Clamp(Math.Max(1, clientWidth), Math.Max(1, clientHeight))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return False
        End If

        Dim x As Integer = rect.Left + (rect.Width \ 2)
        Dim y As Integer = rect.Top + (rect.Height \ 2)
        Return ClickClientPoint(hwnd, x, y)
    End Function

    Public Shared Function ClickClientPoint(hwnd As IntPtr, x As Integer, y As Integer, Optional moveDelayMs As Integer = 10, Optional downUpDelayMs As Integer = 25) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim lParam As Integer = (x And &HFFFF) Or ((y And &HFFFF) << 16)
        Try
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_MOUSEMOVE), IntPtr.Zero, New IntPtr(lParam))
            If moveDelayMs > 0 Then
                Thread.Sleep(moveDelayMs)
            End If
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_LBUTTONDOWN), New IntPtr(NativeMethods.MK_LBUTTON), New IntPtr(lParam))
            If downUpDelayMs > 0 Then
                Thread.Sleep(downUpDelayMs)
            End If
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_LBUTTONUP), IntPtr.Zero, New IntPtr(lParam))
            Return True
        Catch
            Return False
        End Try
    End Function
End Class
