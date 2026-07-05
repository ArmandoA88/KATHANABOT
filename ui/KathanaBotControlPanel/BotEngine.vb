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
Imports System.Text.Json.Serialization
Imports System.Buffers
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
    Public Const DefaultBarColorTolerance As Integer = 48

    Public Shared Function DefaultHpBarRect() As RectRegion
        Return New RectRegion(1, 22, 218, 14)
    End Function

    Public Shared Function DefaultMpBarRect() As RectRegion
        Return New RectRegion(3, 39, 216, 10)
    End Function

    Public Shared Function DefaultMobNameRect() As RectRegion
        Return New RectRegion(0, 53, 218, 22)
    End Function

    Public Shared Function DefaultMobHpRect() As RectRegion
        Return New RectRegion(0, 78, 215, 12)
    End Function

    Public Shared Function DefaultDisconnectMessageRect() As RectRegion
        Return New RectRegion(0, 0, 360, 130)
    End Function

    Public Shared Function DefaultDisconnectOkRect() As RectRegion
        Return New RectRegion(151, 98, 59, 22)
    End Function

    Public Shared Function DefaultHpBarColorArgb() As Integer
        Return Color.FromArgb(230, 0, 0).ToArgb()
    End Function

    Public Shared Function DefaultMpBarColorArgb() As Integer
        Return Color.FromArgb(24, 62, 235).ToArgb()
    End Function

    Public Shared Function DefaultMapCoordinateRect() As RectRegion
        Return New RectRegion(6, 744, 120, 22)
    End Function

    Public Shared Function DefaultMapCoordinateXRect() As RectRegion
        Return SplitMapCoordinateRect(DefaultMapCoordinateRect(), True)
    End Function

    Public Shared Function DefaultMapCoordinateYRect() As RectRegion
        Return SplitMapCoordinateRect(DefaultMapCoordinateRect(), False)
    End Function

    Public Shared Function SplitMapCoordinateRect(combined As RectRegion, leftAxis As Boolean) As RectRegion
        Dim source As RectRegion = If(combined, DefaultMapCoordinateRect())
        Dim width As Integer = Math.Max(2, source.W)
        Dim gap As Integer = If(width >= 80, 4, If(width >= 24, 2, 0))
        If width - gap < 2 Then
            gap = 0
        End If

        Dim axisWidth As Integer = Math.Max(1, (width - gap) \ 2)
        If leftAxis Then
            Return New RectRegion(source.X, source.Y, axisWidth, Math.Max(1, source.H))
        End If

        Dim rightX As Integer = source.X + axisWidth + gap
        Dim rightW As Integer = Math.Max(1, source.W - axisWidth - gap)
        Return New RectRegion(rightX, source.Y, rightW, Math.Max(1, source.H))
    End Function

    Public Shared Function CombineMapCoordinateRects(xRect As RectRegion, yRect As RectRegion) As RectRegion
        If xRect Is Nothing AndAlso yRect Is Nothing Then
            Return DefaultMapCoordinateRect()
        End If
        If xRect Is Nothing Then
            Return New RectRegion(yRect.X, yRect.Y, Math.Max(1, yRect.W), Math.Max(1, yRect.H))
        End If
        If yRect Is Nothing Then
            Return New RectRegion(xRect.X, xRect.Y, Math.Max(1, xRect.W), Math.Max(1, xRect.H))
        End If

        Dim left As Integer = Math.Min(xRect.X, yRect.X)
        Dim top As Integer = Math.Min(xRect.Y, yRect.Y)
        Dim right As Integer = Math.Max(xRect.X + Math.Max(1, xRect.W), yRect.X + Math.Max(1, yRect.W))
        Dim bottom As Integer = Math.Max(xRect.Y + Math.Max(1, xRect.H), yRect.Y + Math.Max(1, yRect.H))
        Return New RectRegion(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))
    End Function

    Public Property WindowTitle As String = "Kathana - The Reign of Shadow"
    <JsonIgnore>
    Public Property SelectedWindowHandle As IntPtr = IntPtr.Zero
    Public Property LiteModeEnabled As Boolean = False
    Public Property LiteHpCheckPointX As Integer = -1
    Public Property LiteHpCheckPointY As Integer = -1
    Public Property LiteHpCheckColorEnabled As Boolean = False
    Public Property LiteHpCheckColorArgb As Integer = 0
    Public Property LiteMpCheckPointX As Integer = -1
    Public Property LiteMpCheckPointY As Integer = -1
    Public Property LiteMpCheckColorEnabled As Boolean = False
    Public Property LiteMpCheckColorArgb As Integer = 0
    Public Property CustomBarColorsEnabled As Boolean = False
    Public Property HpBarColorArgb As Integer = DefaultHpBarColorArgb()
    Public Property MpBarColorArgb As Integer = DefaultMpBarColorArgb()
    Public Property BarColorTolerance As Integer = DefaultBarColorTolerance
    Public Property LoopMs As Integer = 80
    Public Property RetargetMs As Integer = 550
    Public Property ForcedRetargetMs As Integer = 550
    Public Property MobHpPresenceThreshold As Double = 1.0
    Public Property HighMaxHpSpecialEnabled As Boolean = True
    Public Property HighMaxHpThreshold As Integer = 2000
    Public Property AvoidHighMaxHpEnabled As Boolean = False
    Public Property AvoidHighMaxHpThreshold As Integer = 2000
    Public Property HpBar As RectRegion = DefaultHpBarRect()
    Public Property MpBar As RectRegion = DefaultMpBarRect()
    Public Property MobNameRect As RectRegion = DefaultMobNameRect()
    Public Property MobHpRect As RectRegion = DefaultMobHpRect()
    Public Property MobLifeRect As RectRegion = DefaultMobHpRect()
    Public Property UnreachableTextRect As RectRegion = New RectRegion(15, 582, 128, 22)
    Public Property PranaExpRect As RectRegion = New RectRegion(472, 745, 78, 21)
    Public Property RupiahsRect As RectRegion = New RectRegion(560, 745, 110, 21)
    Public Property PartyInviteScanRect As RectRegion = New RectRegion(349, 318, 328, 124)
    Public Property PartyInviteOkRect As RectRegion = New RectRegion(463, 410, 59, 21)
    Public Property PartyListRect As RectRegion = New RectRegion(0, 24, 168, 244)
    Public Property DisconnectMessageRect As RectRegion = DefaultDisconnectMessageRect()
    Public Property DisconnectOkRect As RectRegion = DefaultDisconnectOkRect()
    Public Property MapRect As RectRegion = New RectRegion(0, 0, 1024, 768)
    Public Property MapCoordinateRect As RectRegion = DefaultMapCoordinateRect()
    Public Property MapCoordinateXRect As RectRegion = DefaultMapCoordinateXRect()
    Public Property MapCoordinateYRect As RectRegion = DefaultMapCoordinateYRect()
    Public Property ChatRect As RectRegion = New RectRegion(18, 548, 430, 144)
    Public Property LootScanRect As RectRegion = New RectRegion(220, 80, 584, 430)
    Public Property LootScanPoints As List(Of LootScanPoint) = CreateDefaultLootScanPoints()
    Public Property BypassHpMpLimits As Boolean = False
    Public Property BypassStuckTarget As Boolean = True
    Public Property StuckTargetMs As Integer = 2200
    Public Property StuckTargetNoProgressRetargetMs As Integer = 6000
    Public Property BlackScreenProtectionEnabled As Boolean = True
    Public Property DeniedMobs As List(Of String) = New List(Of String)()
    Public Property MonsterFilterMode As String = "blacklist"
    Public Property MonsterFilterConfirmReads As Integer = 2
    Public Property LootPickupEnabled As Boolean = False
    Public Property LootPickupIntervalMs As Integer = 4000
    Public Property LootPickupVerifyDelayMs As Integer = 80
    Public Property LootNameAutoPickupEnabled As Boolean = False
    Public Property LootNamePickupOffsetX As Integer = 0
    Public Property LootNamePickupOffsetY As Integer = 18
    Public Property LootNamePickupPointX As Integer = -1
    Public Property LootNamePickupPointY As Integer = -1
    Public Property LootNamePickupClickDelayMs As Integer = 180
    Public Property LootNamePickupFPressCount As Integer = 3
    Public Property LootNamePickupFPressGapMs As Integer = 110
    Public Property LootNamePickupMouseHoldMs As Integer = 35
    Public Property LootNamePickupRestoreCursor As Boolean = True
    Public Property LootRejectClickEnabled As Boolean = False
    Public Property LootRejectPointX As Integer = -1
    Public Property LootRejectPointY As Integer = -1
    Public Property ArrowUnbundleEnabled As Boolean = False
    Public Property ArrowUnbundleIntervalMs As Integer = 60000
    Public Property ArrowUnbundlePoints As List(Of LootScanPoint) = New List(Of LootScanPoint)()
    Public Property LootAllowedNames As List(Of String) = New List(Of String)()
    Public Property LootNameMatchThresholdPercent As Integer = 80
    Public Property PartyAutoAcceptEnabled As Boolean = True
    Public Property PartyAskEnabled As Boolean = False
    Public Property PartyAskIntervalMs As Integer = 30000
    Public Property PartyAskText As String = "add"
    Public Property LootScannerEnabled As Boolean = True
    Public Property NotificationProvider As String = "ntfy"
    Public Property DiscordWebhookUrl As String = ""
    Public Property DiscordGlobalWebhookUrl As String = ""
    Public Property DiscordItemWebhookUrl As String = ""
    Public Property DiscordStatsWebhookUrl As String = ""
    Public Property ItemNtfyTopic As String = ""
    Public Property NtfyTopic As String = ""
    Public Property LevelingAgentEnabled As Boolean = False
    Public Property LevelingPreferredMobs As List(Of String) = New List(Of String)()
    Public Property LevelingStopHpEnabled As Boolean = True
    Public Property LevelingStopHpPercent As Integer = 20
    Public Property LevelingStopMpEnabled As Boolean = True
    Public Property LevelingStopMpPercent As Integer = 10
    Public Property LevelingMaxNoTargetEnabled As Boolean = True
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
    Public Property NavigationReturnToStartEnabled As Boolean = False
    Public Property HoldPlaceEnabled As Boolean = False
    Public Property HoldPlaceAnchorSet As Boolean = False
    Public Property HoldPlaceTargetX As Integer = -1
    Public Property HoldPlaceTargetY As Integer = -1
    Public Property HoldPlaceRestrictivenessMode As String = "medium"
    Public Property HoldPlaceRadius As Integer = 4
    Public Property HoldPlaceMoveBurstMs As Integer = 750
    Public Property HoldPlaceCorrectionIntervalMs As Integer = 900
    Public Property HoldPlacePostFightReturnEnabled As Boolean = True
    Public Property HoldPlaceCombatSafeEnabled As Boolean = True
    Public Property HoldPlaceEmergencyLeashDistance As Integer = 60
    Public Property HoldPlaceDirectionLearningEnabled As Boolean = True
    Public Property RouteRecordingEnabled As Boolean = False
    Public Property RouteRecordingName As String = "jina_route"
    Public Property RouteRecordingMinConfidencePercent As Integer = 90
    Public Property RouteRecordingMinSampleDistance As Integer = 2
    Public Property RouteRecordingMinNodeSpacing As Integer = 2
    Public Property RouteRecordingSampleIntervalMs As Integer = 100
    Public Property FullFrameRefreshIntervalMs As Integer = 500
    Public Property LootScannerIntervalMs As Integer = 10000
    Public Property MapCoordinateScanIntervalMs As Integer = 900
    Public Property PartyListScanIntervalMs As Integer = 700
    Public Property PartyInviteScanIntervalMs As Integer = 900
    Public Property MobNameScanIntervalMs As Integer = 650
    Public Property ChatTranslationEnabled As Boolean = False
    Public Property ChatTranslationOverlayEnabled As Boolean = True
    Public Property DisabledCalibrationRegionOverlays As List(Of String) = New List(Of String)()
    Public Property ChatTranslationTargetLanguage As String = "en"
    Public Property ChatTranslationScanIntervalMs As Integer = 700
    Public Property ChatTranslationMaxLines As Integer = 6
    Public Property AdaptivePerformanceEnabled As Boolean = True
    Public Property AdaptiveSlowLoopMinMs As Integer = 140
    Public Property AdaptiveSlowLoopMultiplier As Double = 1.8
    Public Property AdaptiveRecoveryLoopMultiplier As Double = 1.25
    Public Property AdaptiveSlowConfirmCount As Integer = 5
    Public Property AdaptiveRecoveryConfirmCount As Integer = 14
    Public Property PixelChangeGateEnabled As Boolean = True
    Public Property CaptureBackendPreference As String = "auto"
    Public Property Actions As List(Of ActionRule) = New List(Of ActionRule)()

    Public Function IsCalibrationRegionOverlayEnabled(regionName As String) As Boolean
        If String.IsNullOrWhiteSpace(regionName) Then
            Return True
        End If

        If DisabledCalibrationRegionOverlays Is Nothing OrElse DisabledCalibrationRegionOverlays.Count = 0 Then
            Return True
        End If

        Return Not DisabledCalibrationRegionOverlays.Any(Function(item) regionName.Equals(If(item, "").Trim(), StringComparison.OrdinalIgnoreCase))
    End Function

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
                role = "buff"
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

    Public Shared Sub MigrateLegacyVisionLayout(cfg As BotConfig)
        If cfg Is Nothing Then
            Return
        End If

        Dim configuredTitle As String = If(cfg.WindowTitle, "").Trim()
        If configuredTitle = "" OrElse
           configuredTitle.Equals("Kathana   The Coming of the Dark Ages", StringComparison.OrdinalIgnoreCase) OrElse
           configuredTitle.Equals("Kathana - The Coming of the Dark Ages", StringComparison.OrdinalIgnoreCase) Then
            cfg.WindowTitle = "Kathana - The Reign of Shadow"
        End If

        Dim legacyHp As New RectRegion(11, 25, 151, 11)
        Dim legacyMp As New RectRegion(3, 40, 161, 11)
        Dim legacyMobName As New RectRegion(860, 711, 162, 23)
        Dim legacyMobHp As New RectRegion(859, 737, 165, 11)
        Dim usesLegacyHud As Boolean =
            SameRect(cfg.HpBar, legacyHp) AndAlso
            SameRect(cfg.MpBar, legacyMp) AndAlso
            SameRect(cfg.MobNameRect, legacyMobName) AndAlso
            SameRect(cfg.MobHpRect, legacyMobHp)

        If usesLegacyHud Then
            cfg.HpBar = DefaultHpBarRect()
            cfg.MpBar = DefaultMpBarRect()
            cfg.MobNameRect = DefaultMobNameRect()
            cfg.MobHpRect = DefaultMobHpRect()
        End If

        ' mob_life_rect was added after mob_hp_rect. Older saved configs can retain the
        ' obsolete bottom-right rectangle even after the other target regions were moved.
        If cfg.MobLifeRect Is Nothing OrElse
           (SameRect(cfg.MobLifeRect, legacyMobHp) AndAlso Not SameRect(cfg.MobHpRect, legacyMobHp)) Then
            cfg.MobLifeRect = CloneRect(cfg.MobHpRect, DefaultMobHpRect())
        End If
    End Sub

    Private Shared Function SameRect(a As RectRegion, b As RectRegion) As Boolean
        Return a IsNot Nothing AndAlso b IsNot Nothing AndAlso
            a.X = b.X AndAlso a.Y = b.Y AndAlso a.W = b.W AndAlso a.H = b.H
    End Function

    Private Shared Function CloneRect(source As RectRegion, fallback As RectRegion) As RectRegion
        Dim value As RectRegion = If(source, fallback)
        Return New RectRegion(value.X, value.Y, Math.Max(1, value.W), Math.Max(1, value.H))
    End Function
End Class

Public Class BotStatus
    Public Property Running As Boolean
    Public Property RunStartedAtUtc As DateTime = DateTime.MinValue
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
    Public Property CharacterName As String = ""
    Public Property TargetValid As Boolean
    Public Property MapCoordinateText As String = ""
    Public Property MapCoordinateX As Integer = -1
    Public Property MapCoordinateY As Integer = -1
    Public Property MapCoordinateDebugLog As String = ""
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
    Public Property NavigationReturningToStart As Boolean
    Public Property NavigationReturnTargetLabel As String = ""
    Public Property HoldPlaceEnabled As Boolean
    Public Property HoldPlaceActive As Boolean
    Public Property HoldPlaceTargetX As Integer = -1
    Public Property HoldPlaceTargetY As Integer = -1
    Public Property HoldPlaceDistance As Double = -1
    Public Property HoldPlaceReason As String = ""
    Public Property RouteRecordingEnabled As Boolean
    Public Property RouteRecordingActive As Boolean
    Public Property RouteRecordingMapName As String = ""
    Public Property RouteRecordingName As String = ""
    Public Property RouteRecordingSampleCount As Integer
    Public Property RouteRecordingSamples As List(Of NavigationRouteSample) = New List(Of NavigationRouteSample)()
    Public Property RouteRecordingStatus As String = ""
    Public Property RouteRecordingLastSavedPath As String = ""
    Public Property LastAction As String = ""
    Public Property RepairConfirmCount As Integer
    Public Property RepairConfirmRequiredCount As Integer
    Public Property RepairConfirmWindowMinutes As Integer
    Public Property RepairTriggerCount As Integer
    Public Property NotAttackingReason As String = ""
    Public Property ErrorMessage As String = ""
    Public Property GameDisconnected As Boolean
    Public Property AgentEnabled As Boolean
    Public Property AgentState As String = "Disabled"
    Public Property AgentReason As String = ""
    Public Property AgentGuardrailTriggered As Boolean
    Public Property PerformanceDiagnostics As String = ""
    Public Property EngineRestartCount As Integer
    Public Property EngineLastRestartUtc As DateTime = DateTime.MinValue
    Public Property UpdatedAt As DateTime = DateTime.UtcNow
End Class

Friend Module NativeMethods
    Friend Const PW_CLIENTONLY As UInteger = 1UI
    Friend Const PW_RENDERFULLCONTENT As UInteger = 2UI
    Friend Const CAPTUREBLT As CopyPixelOperation = CType(&H40000000, CopyPixelOperation)
    Friend Const SRCCOPY As UInteger = &HCC0020UI
    Friend Const CAPTUREBLT_ROP As UInteger = &H40000000UI
    Friend Const GA_ROOT As UInteger = 2UI
    Friend Const WM_KEYDOWN As Integer = &H100
    Friend Const WM_KEYUP As Integer = &H101
    Friend Const WM_MOUSEMOVE As Integer = &H200
    Friend Const WM_LBUTTONDOWN As Integer = &H201
    Friend Const WM_LBUTTONUP As Integer = &H202
    Friend Const WM_RBUTTONDOWN As Integer = &H204
    Friend Const WM_RBUTTONUP As Integer = &H205
    Friend Const MK_LBUTTON As Integer = &H1
    Friend Const MK_RBUTTON As Integer = &H2
    Friend Const MOUSEEVENTF_LEFTDOWN As UInteger = &H2UI
    Friend Const MOUSEEVENTF_LEFTUP As UInteger = &H4UI
    Friend Const SW_RESTORE As Integer = 9

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

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetCursorPos(ByRef lpPoint As POINT) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function SetCursorPos(X As Integer, Y As Integer) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Sub mouse_event(dwFlags As UInteger, dx As UInteger, dy As UInteger, dwData As UInteger, dwExtraInfo As UIntPtr)
    End Sub

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
    Friend Function GetWindowRect(hWnd As IntPtr, ByRef lpRect As RECT) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function ClientToScreen(hWnd As IntPtr, ByRef lpPoint As POINT) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function ScreenToClient(hWnd As IntPtr, ByRef lpPoint As POINT) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function WindowFromPoint(pt As POINT) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetAncestor(hWnd As IntPtr, gaFlags As UInteger) As IntPtr
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
    Friend Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function ReleaseDC(hWnd As IntPtr, hDC As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll", SetLastError:=True)>
    Friend Function GetPixel(hdc As IntPtr, nXPos As Integer, nYPos As Integer) As UInteger
    End Function

    <DllImport("gdi32.dll", SetLastError:=True)>
    Friend Function BitBlt(hdcDest As IntPtr, nXDest As Integer, nYDest As Integer, nWidth As Integer, nHeight As Integer, hdcSrc As IntPtr, nXSrc As Integer, nYSrc As Integer, dwRop As UInteger) As Boolean
    End Function



    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef lpdwProcessId As UInteger) As UInteger
    End Function
End Module

Public Class BotEngine
    Public Event StatusUpdated(status As BotStatus)
    Public Event LogLine(line As String)
    Private Const NotificationProviderNtfy As String = "ntfy"
    Private Const NotificationProviderDiscord As String = "discord"
    Private Const AllowBlindAttackWhenTargetMissing As Boolean = False
    Private Const FirstHitWindowMs As Integer = 800
    Private Const BlacklistLockWindowMs As Integer = 800
    Private Const HardcodedVisionStatsDiscordWebhookUrl As String = "https://discord.com/api/webhooks/1499115336904085626/5x6UMV3hfFO2U2fAMwWSkk4j3spnfJZvlohn6Rpt98ub7BdvbPu-rpiXHVwXSWvnM583"
    Private Const HardcodedVisionStatsIntervalMinutes As Integer = 30
    Private Const TargetNameConfirmMinGapMs As Integer = 120
    Private Const TargetNameConfirmRequiredCount As Integer = 2
    Private Const ExpRateSampleMs As Integer = 60000
    Private Const ExpOcrMinIntervalMs As Integer = 5000
    Private Const RupiahsOcrMinIntervalMs As Integer = 5000
    Private Const MapCoordinateOcrMinIntervalMs As Integer = 900
    Private Const HoldPlaceMaxCoordinateAcceptanceDistance As Double = 100.0R
    Private Const MapCoordinateFarJumpConfirmRequiredCount As Integer = 2
    Private Const MapCoordinateFarJumpConfirmMaxDistance As Double = 4.0R
    Private Const MapCoordinateFarJumpConfirmWindowMs As Integer = 8000
    Private Const MapCoordinateFarJumpMinConfidence As Integer = 20
    Private Const MaxMapCoordinateDebugLines As Integer = 80
    Private Const MapMarkerScanMinIntervalMs As Integer = 250
    Private Shared ReadOnly MapCoordinateOcrDiagnosticsDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "KathanaBotCoordinateOcr")
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
    Private Const DisconnectOcrMinIntervalMs As Integer = 1000
    Private Const DisconnectConfirmWindowMs As Integer = 5000
    Private Const DisconnectConfirmRequiredCount As Integer = 2
    Private Const UnreachableConfirmWindowMs As Integer = 900
    Private Const UnreachableConfirmRequiredCount As Integer = 2
    Private Const RepairConfirmRequiredCount As Integer = 5
    Private Const RepairConfirmWindowMs As Integer = 600000
    Private Const UnreachableClearRequiredCount As Integer = 2
    Private Const SustainedSingleZeroConfirmRequiredCount As Integer = 3
    Private Const SustainedSingleManaZeroConfirmRequiredCount As Integer = 6
    Private Const NearZeroSupportConfirmRequiredCount As Integer = 3
    Private Const NearZeroManaConfirmRequiredCount As Integer = 6
    Private Const FreshZeroSupportConfirmSamples As Integer = 3
    Private Const FreshZeroSupportConfirmDelayMs As Integer = 35
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
    Private Const TargetSignalGraceMinMs As Integer = 1800
    Private Const TargetSignalGraceMaxMs As Integer = 5000
    Private Const LootScannerIntervalMs As Integer = 10000
    Private Const StartupCombatPriorityMs As Integer = 3000
    Private Const FullFrameRefreshMs As Integer = 500
    Private Const StatusUpdateMinIntervalMs As Integer = 200
    Private Const AdaptiveSlowLoopConfirmCount As Integer = 5
    Private Const AdaptiveRecoveryLoopConfirmCount As Integer = 14

    Private Enum CaptureClientMethod
        PrintClientOnly
        PrintRenderFullContent
        PrintClientAndRenderFullContent
        PrintDefault
        CopyFromScreen
    End Enum

    Private NotInheritable Class TimingBucket
        Public Property Count As Long
        Public Property AverageMs As Double
        Public Property MaxMs As Double

        Public Sub Add(elapsedMs As Double)
            Dim safeMs As Double = If(Double.IsNaN(elapsedMs) OrElse Double.IsInfinity(elapsedMs), 0.0R, Math.Max(0.0R, elapsedMs))
            Count += 1
            If Count = 1 Then
                AverageMs = safeMs
                MaxMs = safeMs
            Else
                AverageMs += (safeMs - AverageMs) * 0.12R
                If safeMs > MaxMs Then
                    MaxMs = safeMs
                End If
            End If
        End Sub

        Public Function Format(label As String) As String
            Return $"{label}: avg {AverageMs:0.0}ms | max {MaxMs:0.0}ms | n={Count}"
        End Function
    End Class

    Private ReadOnly _sync As New Object()
    Private ReadOnly _frameSync As New Object()
    Private ReadOnly _perfSync As New Object()
    Private ReadOnly _loopTiming As New TimingBucket()
    Private ReadOnly _captureTiming As New TimingBucket()
    Private ReadOnly _hpMpScanTiming As New TimingBucket()
    Private ReadOnly _mobOcrTiming As New TimingBucket()
    Private ReadOnly _chatOcrTiming As New TimingBucket()
    Private ReadOnly _lootScanTiming As New TimingBucket()
    Private _adaptivePerformanceActive As Boolean = False
    Private _adaptiveSlowLoopCount As Integer = 0
    Private _adaptiveRecoveryLoopCount As Integer = 0
    Private _adaptiveDeferredOptionalScans As Long = 0
    Private _lastCaptureMethodName As String = "none"
    Private _lastStatusRaisedAt As DateTime = DateTime.MinValue
    Private _lastStatusRaisedSignature As String = ""
    Private Shared ReadOnly _captureMethodSync As New Object()
    Private Shared ReadOnly _captureMethodByWindow As New Dictionary(Of IntPtr, CaptureClientMethod)()
    Private Shared _captureBackendPreference As String = "auto"
    Private Shared ReadOnly NavigationRouteStorageRoot As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotControlPanel", "navigation_routes")
    Private Shared ReadOnly NavigationRouteJsonOptions As New JsonSerializerOptions With {.WriteIndented = True}
    Private Shared ReadOnly _recordedGraphCache As New Dictionary(Of String, List(Of RecordedNavigationGraph))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _recordedGraphCacheSync As New Object()
    Private Shared ReadOnly HardcodedVisionStatsHttpClient As New System.Net.Http.HttpClient()
    Private _config As BotConfig = BotConfig.CreateDefault()
    Private _status As New BotStatus()
    Private _cts As CancellationTokenSource
    Private _task As Task
    Private _lastNormalRetarget As DateTime = DateTime.MinValue
    Private _lastForcedRetarget As DateTime = DateTime.MinValue
    Private _lastTargetWindowSeen As DateTime = DateTime.MinValue
    Private _lastLivingTargetSignalAt As DateTime = DateTime.MinValue
    Private _lastTargetValidAt As DateTime = DateTime.MinValue
    Private _noTargetBeganAt As DateTime = DateTime.MinValue
    Private _lastAttackAction As DateTime = DateTime.MinValue
    Private _loopStartedAt As DateTime = DateTime.MinValue
    Private _combatLockActive As Boolean = False
    Private _combatLockTargetSignature As String = ""
    Private _combatLockLostSignalCount As Integer = 0
    Private _combatLockLastSeenAt As DateTime = DateTime.MinValue
    Private _lastMobHpSample As Double = -1
    Private _lastMobHpMovement As DateTime = DateTime.MinValue
    Private _noDamageTargetSignature As String = ""
    Private _noDamageAttackCount As Integer = 0
    Private _lastMobNameRead As DateTime = DateTime.MinValue
    Private _lastMobNameDetectedAt As DateTime = DateTime.MinValue
    Private _cachedMobName As String = ""
    Private _mobNameOcrStartedAt As DateTime = DateTime.MinValue
    Private _mobNameOcrTask As Task(Of String) = Nothing
    Private _lastMobHpTextScan As DateTime = DateTime.MinValue
    Private _mobHpTextOcrTask As Task(Of String) = Nothing
    Private _lastMobHpText As String = ""
    Private _lastMobDetectedMaxHp As Integer = -1
    Private _lastHardcodedVisionStatsSentAt As DateTime = DateTime.MinValue
    Private _hardcodedVisionStatsInitialSent As Boolean = False
    Private _hardcodedVisionStatsInFlight As Boolean = False
    Private _lastCharacterName As String = ""
    Private _latestLoopFrame As Bitmap = Nothing
    Private _latestLoopFrameCapturedAt As DateTime = DateTime.MinValue
    Private _lastFullFrameCaptureAttemptAt As DateTime = DateTime.MinValue
    Private _lastLootPickup As DateTime = DateTime.MinValue
    Private _pendingLootPickupVerifyAt As DateTime = DateTime.MinValue
    Private _lastArrowUnbundleAt As DateTime = DateTime.MinValue
    Private _arrowUnbundleNextIndex As Integer = 0
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
    Private _lastDisconnectScan As DateTime = DateTime.MinValue
    Private _disconnectOcrTask As Task(Of String) = Nothing
    Private _lastDisconnectCandidate As String = ""
    Private _disconnectConfirmCount As Integer = 0
    Private _disconnectLastMatchAt As DateTime = DateTime.MinValue
    Private _disconnectLatched As Boolean = False
    Private _disconnectClearCount As Integer = 0
    Private _repairConfirmCount As Integer = 0
    Private _repairLastMatchAt As DateTime = DateTime.MinValue
    Private ReadOnly _repairMatchTimes As New Queue(Of DateTime)()
    Private _repairLatched As Boolean = False
    Private _repairClearCount As Integer = 0
    Private _repairTriggerCount As Integer = 0
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
    Private _pendingFarMapCoordinateX As Integer = -1
    Private _pendingFarMapCoordinateY As Integer = -1
    Private _pendingFarMapCoordinateCount As Integer = 0
    Private _pendingFarMapCoordinateFirstAt As DateTime = DateTime.MinValue
    Private _pendingFarMapCoordinateLastAt As DateTime = DateTime.MinValue
    Private ReadOnly _mapCoordinateDebugLines As New Queue(Of String)()
    Private _lastMapCoordinateDebugLog As String = ""
    Private _lastChatOcrAt As DateTime = DateTime.MinValue
    Private _lastChatOcrText As String = ""
    Private _lastChatOcrNormalized As String = ""
    Private _lastChatOcrUpdatedAt As DateTime = DateTime.MinValue
    Private _lastChatVisualSignature As ULong = 0UL
    Private _lastPartyListVisualSignature As ULong = 0UL
    Private _lastPartyInviteVisualSignature As ULong = 0UL
    Private _lastLootScannerVisualSignature As ULong = 0UL
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
    Private _navigationReturnToStartActive As Boolean = False
    Private _navigationReturnTargetNodeId As String = ""
    Private _navigationReturnTargetNodeLabel As String = ""
    Private _navigationOutboundStartNodeId As String = ""
    Private _navigationOutboundStartNodeLabel As String = ""
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
    Private _lastTravelInputIsHoldCorrection As Boolean = False
    Private _lastHoldPlaceMoveAt As DateTime = DateTime.MinValue
    Private _lastHoldPlaceActive As Boolean = False
    Private _lastHoldPlaceTargetX As Integer = -1
    Private _lastHoldPlaceTargetY As Integer = -1
    Private _lastHoldPlaceDistance As Double = -1
    Private _lastHoldPlaceReason As String = ""

    Public Shared Function GetMapCoordinateOcrDiagnosticsDirectory() As String
        Return MapCoordinateOcrDiagnosticsDirectory
    End Function
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
    Private _hpZeroSupportConfirmCount As Integer = 0
    Private _mpZeroSupportConfirmCount As Integer = 0
    Private _lastRightAltAt As DateTime = DateTime.MinValue
    Private _lootScannerCapturePending As Boolean = False
    Private _lootScannerCaptureRequestedAt As DateTime = DateTime.MinValue
    Private _lootScannerAltHeld As Boolean = False
    Private _lootScannerProcessingTask As Task = Nothing
    Private _engineRestartCount As Integer = 0
    Private _engineLastRestartUtc As DateTime = DateTime.MinValue
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
        BotConfig.MigrateLegacyVisionLayout(cfg)
        SyncLock _sync
            _config = cfg
        End SyncLock
        SetCaptureBackendPreference(If(cfg?.CaptureBackendPreference, "auto"))
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

    Public Function EnsureLoopWorkerRunning() As Boolean
        Dim restarted As Boolean = False
        Dim faultMessage As String = ""
        SyncLock _sync
            If Not _status.Running Then
                Return False
            End If
            If _task IsNot Nothing AndAlso Not _task.IsCompleted Then
                Return False
            End If

            If _task IsNot Nothing AndAlso _task.IsFaulted AndAlso _task.Exception IsNot Nothing Then
                faultMessage = _task.Exception.GetBaseException().Message
            End If

            If _cts Is Nothing OrElse _cts.IsCancellationRequested Then
                _cts = New CancellationTokenSource()
            End If

            _engineRestartCount += 1
            _engineLastRestartUtc = DateTime.UtcNow
            _status.EngineRestartCount = _engineRestartCount
            _status.EngineLastRestartUtc = _engineLastRestartUtc
            _status.ErrorMessage = "Engine worker restarted."
            _task = Task.Run(Sub() LoopAsync(_cts.Token).GetAwaiter().GetResult())
            restarted = True
        End SyncLock

        If restarted Then
            If String.IsNullOrWhiteSpace(faultMessage) Then
                RaiseEvent LogLine("Engine worker restarted automatically.")
            Else
                RaiseEvent LogLine("Engine worker restarted automatically after fault: " & faultMessage)
            End If
        End If
        Return restarted
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
            _lastLivingTargetSignalAt = DateTime.MinValue
            _lastTargetValidAt = DateTime.MinValue
            _noTargetBeganAt = DateTime.MinValue
            _lastAttackAction = DateTime.MinValue
            _loopStartedAt = DateTime.UtcNow
            _status.RunStartedAtUtc = _loopStartedAt
            _combatLockActive = False
            _combatLockTargetSignature = ""
            _combatLockLostSignalCount = 0
            _combatLockLastSeenAt = DateTime.MinValue
            _lastMobHpSample = -1
            _lastMobHpMovement = DateTime.MinValue
            _noDamageTargetSignature = ""
            _noDamageAttackCount = 0
            _lastMobNameRead = DateTime.MinValue
            _lastMobNameDetectedAt = DateTime.MinValue
            _cachedMobName = ""
            _mobNameOcrStartedAt = DateTime.MinValue
            _mobNameOcrTask = Nothing
            _lastMobHpTextScan = DateTime.MinValue
            _mobHpTextOcrTask = Nothing
            _lastMobHpText = ""
            _lastMobDetectedMaxHp = -1
            _lastHardcodedVisionStatsSentAt = DateTime.MinValue
            _hardcodedVisionStatsInitialSent = False
            _hardcodedVisionStatsInFlight = False
            _lastCharacterName = ""
            _lastLootPickup = DateTime.MinValue
            _pendingLootPickupVerifyAt = DateTime.MinValue
            _lastArrowUnbundleAt = DateTime.MinValue
            _arrowUnbundleNextIndex = 0
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
            _lastDisconnectScan = DateTime.MinValue
            _disconnectOcrTask = Nothing
            _lastDisconnectCandidate = ""
            _disconnectConfirmCount = 0
            _disconnectLastMatchAt = DateTime.MinValue
            _disconnectLatched = False
            _disconnectClearCount = 0
            _repairConfirmCount = 0
            _repairLastMatchAt = DateTime.MinValue
            _repairMatchTimes.Clear()
            _repairLatched = False
            _repairClearCount = 0
            _repairTriggerCount = 0
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
            _mapCoordinateDebugLines.Clear()
            _lastMapCoordinateDebugLog = ""
            _lastChatOcrAt = DateTime.MinValue
            _lastChatOcrText = ""
            _lastChatOcrNormalized = ""
            _lastChatOcrUpdatedAt = DateTime.MinValue
            _lastChatVisualSignature = 0UL
            _lastPartyListVisualSignature = 0UL
            _lastPartyInviteVisualSignature = 0UL
            _lastLootScannerVisualSignature = 0UL
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
            _navigationReturnToStartActive = False
            _navigationReturnTargetNodeId = ""
            _navigationReturnTargetNodeLabel = ""
            _navigationOutboundStartNodeId = ""
            _navigationOutboundStartNodeLabel = ""
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
            _lastTravelInputIsHoldCorrection = False
            ClearHoldPlaceRuntime()
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
            _hpZeroSupportConfirmCount = 0
            _mpZeroSupportConfirmCount = 0
            _lastRightAltAt = DateTime.MinValue
            _lootScannerCapturePending = False
            _lootScannerCaptureRequestedAt = DateTime.MinValue
            _lootScannerAltHeld = False
            _lootScannerProcessingTask = Nothing
            _lastFullFrameCaptureAttemptAt = DateTime.MinValue
            _lastKeyTime.Clear()
            _lastStatusRaisedAt = DateTime.MinValue
            _lastStatusRaisedSignature = ""
            SyncLock _perfSync
                _adaptivePerformanceActive = False
                _adaptiveSlowLoopCount = 0
                _adaptiveRecoveryLoopCount = 0
                _adaptiveDeferredOptionalScans = 0
                _lastCaptureMethodName = "none"
            End SyncLock
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
            _status.RunStartedAtUtc = DateTime.MinValue
            _lootScannerCapturePending = False
            _lootScannerCaptureRequestedAt = DateTime.MinValue
            _lootScannerAltHeld = False
            _lootScannerProcessingTask = Nothing
            _pendingLootPickupVerifyAt = DateTime.MinValue
            _lastArrowUnbundleAt = DateTime.MinValue
            _arrowUnbundleNextIndex = 0
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

        Dim hwnd As IntPtr = ResolveGameWindow(cfg)
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
            Dim loopWatch As Stopwatch = Stopwatch.StartNew()
            Dim cfg As BotConfig = Nothing
            Dim loopDelayMs As Integer = 80
            Try
                SyncLock _sync
                    cfg = _config
                End SyncLock
                If cfg Is Nothing Then
                    cfg = BotConfig.CreateDefault()
                End If
                loopDelayMs = Math.Max(1, cfg.LoopMs)
                Dim retargetDelayMs As Integer = GetRetargetCooldownMs(cfg, loopDelayMs)
                Dim noTargetStableMs As Integer = retargetDelayMs

                Dim hwnd As IntPtr = ResolveGameWindow(cfg)
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
                                  s.GameDisconnected = False
                              End Sub)
                    RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                    Await Task.Delay(loopDelayMs, token)
                    Continue While
                End If

                Dim now As DateTime = DateTime.UtcNow
                Dim startupCombatPriorityActive As Boolean =
                    _loopStartedAt <> DateTime.MinValue AndAlso
                    (now - _loopStartedAt).TotalMilliseconds < StartupCombatPriorityMs
                Dim deferOptionalWork As Boolean = IsAdaptiveOptionalWorkDeferred()

                If cfg.LiteModeEnabled Then
                Dim clientRect As NativeMethods.RECT
                If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
                    ClearLatestLoopFrame()
                    SetStatus(Sub(s)
                                  s.WindowFound = True
                                  s.HpPercent = 0
                                  s.MpPercent = 0
                                  s.TargetValid = False
                                  s.NotAttackingReason = "Lite HP/MP scan failed."
                                  s.ErrorMessage = "Unable to read Lite bar coordinates."
                                  s.GameDisconnected = False
                              End Sub)
                    RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                    Await Task.Delay(loopDelayMs, token)
                    Continue While
                End If

                Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
                Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
                Dim liteHpRegion As New RectRegion(0, 0, 1, 1)
                Dim liteMpRegion As New RectRegion(0, 0, 1, 1)
                Dim liteMobNameRegion As New RectRegion(0, 0, 1, 1)
                Dim liteMobHpRegion As New RectRegion(0, 0, 1, 1)
                Dim liteUnreachableTextRegion As New RectRegion(0, 0, 1, 1)
                Dim litePranaExpRegion As New RectRegion(0, 0, 1, 1)
                Dim liteRupiahsRegion As New RectRegion(0, 0, 1, 1)
                Dim litePartyInviteScanRegion As New RectRegion(0, 0, 1, 1)
                Dim litePartyInviteOkRegion As New RectRegion(0, 0, 1, 1)
                Dim litePartyListRegion As New RectRegion(0, 0, 1, 1)
                Dim liteDisconnectMessageRegion As New RectRegion(0, 0, 1, 1)
                Dim liteMapCoordinateXRegion As New RectRegion(0, 0, 1, 1)
                Dim liteMapCoordinateYRegion As New RectRegion(0, 0, 1, 1)
                Dim liteChatRegion As New RectRegion(0, 0, 1, 1)
                ResolveVisionRegions(cfg, clientWidth, clientHeight, liteHpRegion, liteMpRegion, liteMobNameRegion, liteMobHpRegion, liteUnreachableTextRegion, litePranaExpRegion, liteRupiahsRegion, litePartyInviteScanRegion, litePartyInviteOkRegion, litePartyListRegion, liteDisconnectMessageRegion, liteMapCoordinateXRegion, liteMapCoordinateYRegion, liteChatRegion)

                Dim hasLiteHpPoint As Boolean = cfg.LiteHpCheckPointX >= 0 AndAlso cfg.LiteHpCheckPointY >= 0
                Dim hasLiteMpPoint As Boolean = cfg.LiteMpCheckPointX >= 0 AndAlso cfg.LiteMpCheckPointY >= 0
                Dim needsLiteFrame As Boolean = cfg.PartyAskEnabled OrElse hasLiteHpPoint OrElse hasLiteMpPoint
                Dim liteFrame As Bitmap = Nothing
                Dim liteScanWarning As String = ""
                Dim liteFullFrameGlitch As Boolean = False
                If needsLiteFrame Then
                    Dim captureWatch As Stopwatch = Stopwatch.StartNew()
                    liteFrame = CaptureClient(hwnd)
                    captureWatch.Stop()
                    RecordTiming(_captureTiming, captureWatch.Elapsed.TotalMilliseconds)
                    SyncLock _perfSync
                        _lastCaptureMethodName = GetCachedCaptureMethodName(hwnd)
                    End SyncLock
                    If liteFrame IsNot Nothing Then
                        If cfg.BlackScreenProtectionEnabled AndAlso IsLikelyBlackFrame(liteFrame) Then
                            ClearCachedCaptureMethod(hwnd)
                            ClearLatestLoopFrame()
                            liteFrame.Dispose()
                            liteFrame = Nothing
                            liteFullFrameGlitch = True
                            liteScanWarning = "Vision glitch: black full-frame capture skipped; Lite vision actions were skipped."
                        Else
                            ReplaceLatestLoopFrame(liteFrame)
                        End If
                    Else
                        ClearLatestLoopFrame()
                    End If
                Else
                    ClearLatestLoopFrame()
                End If

                Dim hpScanOk As Boolean = False
                Dim mpScanOk As Boolean = False
                Dim liteHpPct As Double = If(hasLiteHpPoint, 0, 100)
                Dim liteMpPct As Double = If(hasLiteMpPoint, 0, 100)

                If hasLiteHpPoint Then
                    liteHpPct = ComputeClientPotionPointPercent(liteFrame, cfg.LiteHpCheckPointX, cfg.LiteHpCheckPointY, True, cfg, cfg.LiteHpCheckColorEnabled, cfg.LiteHpCheckColorArgb, hpScanOk)
                End If

                If hasLiteMpPoint Then
                    liteMpPct = ComputeClientPotionPointPercent(liteFrame, cfg.LiteMpCheckPointX, cfg.LiteMpCheckPointY, False, cfg, cfg.LiteMpCheckColorEnabled, cfg.LiteMpCheckColorArgb, mpScanOk)
                End If

                Dim liteAttackHpPct As Double = If(hpScanOk, liteHpPct, 100.0)
                Dim liteAttackMpPct As Double = If(mpScanOk, liteMpPct, 100.0)
                Dim liteCaptureGlitch As Boolean = liteFullFrameGlitch OrElse (hasLiteHpPoint AndAlso Not hpScanOk) OrElse (hasLiteMpPoint AndAlso Not mpScanOk)

                Dim liteReason As String = ""
                Dim liteActionSent As Boolean = False
                Dim liteGameDisconnected As Boolean = TryHandleDisconnectMessageFromClientRegion(cfg, hwnd, now, liteDisconnectMessageRegion)
                If liteGameDisconnected Then
                    liteReason = "Game disconnected message detected."
                    liteScanWarning = If(liteScanWarning = "", "Game disconnected from server.", liteScanWarning & " Game disconnected from server.")
                End If
                If liteFrame IsNot Nothing Then
                    liteActionSent = TryHandleAutoAcceptPrompts(cfg, hwnd, liteFrame, now, litePartyInviteScanRegion, litePartyInviteOkRegion)
                    If liteActionSent Then
                        liteReason = "Auto-accept prompt detected and accepted."
                    End If
                End If

                If Not liteGameDisconnected AndAlso Not liteActionSent Then
                    liteActionSent = TryHandlePartyAsk(cfg, hwnd, now)
                    If liteActionSent Then
                        liteReason = "Party ask command sent."
                    End If
                End If

                If Not liteGameDisconnected AndAlso Not liteActionSent AndAlso (hpScanOk OrElse mpScanOk) Then
                    liteActionSent = TrySendSupportActions(cfg, hwnd, liteAttackHpPct, liteAttackMpPct)
                End If

                If Not liteGameDisconnected AndAlso Not liteActionSent Then
                    Dim liteBurst As List(Of ActionRule) = ChooseAttackBurstActions(cfg, liteAttackHpPct, liteAttackMpPct, True, True, False, False, liteReason)
                    If liteBurst.Count > 0 Then
                        Dim sentKeys As New List(Of String)()
                        For Each attackAction As ActionRule In liteBurst
                            If sentKeys.Count > 0 Then
                                Thread.Sleep(AttackBurstGapMs)
                            End If

                            If Not SendKey(hwnd, attackAction.KeyName, FastKeyPressMs) Then
                                Continue For
                            End If

                            MarkKeyUsed(attackAction.KeyName)
                            sentKeys.Add(attackAction.KeyName)
                            _lastAttackAction = DateTime.UtcNow
                        Next

                        If sentKeys.Count > 0 Then
                            SetLastAction(If(sentKeys.Count = 1, $"{sentKeys(0)} ({liteBurst(0).Role})", $"{String.Join("/", sentKeys)} (lite burst)"))
                            liteActionSent = True
                            liteReason = ""
                        End If
                    End If
                End If

                If cfg.PartyAutoAcceptEnabled AndAlso liteFrame Is Nothing Then
                    liteScanWarning = If(liteScanWarning = "", "Unable to capture Lite window for party prompt scan.", liteScanWarning & " Party prompt scan skipped.")
                End If
                If hasLiteHpPoint AndAlso Not hpScanOk Then
                    liteScanWarning = If(liteScanWarning = "", "Unable to read Lite HP AutoPots point.", liteScanWarning & " Unable to read Lite HP AutoPots point.")
                End If
                If hasLiteMpPoint AndAlso Not mpScanOk Then
                    liteScanWarning = If(liteScanWarning = "", "Unable to read Lite Mana AutoPots point.", liteScanWarning & " Unable to read Lite Mana AutoPots point.")
                End If

                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.HpPercent = Math.Round(liteAttackHpPct, 1)
                              s.MpPercent = Math.Round(liteAttackMpPct, 1)
                              s.MobHpPercent = 0
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.ExpPercent = 0
                              s.ExpPerHour = -1
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.MobName = ""
                              s.TargetValid = Not liteGameDisconnected
                              s.NotAttackingReason = If(liteActionSent, "", If(String.IsNullOrWhiteSpace(liteReason), "No enabled Lite action is ready.", liteReason))
                              s.ErrorMessage = liteScanWarning
                              s.GameDisconnected = liteGameDisconnected
                          End Sub)
                If liteFrame IsNot Nothing Then
                    liteFrame.Dispose()
                End If
                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If

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
            Dim disconnectMessageRegion As New RectRegion(0, 0, 1, 1)
            Dim mapCoordinateXRegion As New RectRegion(0, 0, 1, 1)
            Dim mapCoordinateYRegion As New RectRegion(0, 0, 1, 1)
            Dim chatRegion As New RectRegion(0, 0, 1, 1)
            Dim fullClientRect As NativeMethods.RECT
            If Not NativeMethods.GetClientRect(hwnd, fullClientRect) Then
                ClearLatestLoopFrame()
                ReleaseLootScannerAltKey()
                ClearMapLocalizationRuntime()
                ClearChatTranslationRuntime()
                ClearPartyListRuntimeState()
                UpdateLevelingAgentState(cfg, LevelingAgentState.Searching, "Unable to read game client size.")
                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.NotAttackingReason = "Capture failed."
                              s.ErrorMessage = "Unable to read game client size."
                              s.GameDisconnected = False
                          End Sub)
                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If

            Dim fullClientWidth As Integer = Math.Max(1, fullClientRect.Right - fullClientRect.Left)
            Dim fullClientHeight As Integer = Math.Max(1, fullClientRect.Bottom - fullClientRect.Top)
            ResolveVisionRegions(cfg, fullClientWidth, fullClientHeight, hpRegion, mpRegion, mobNameRegion, mobHpRegion, unreachableTextRegion, pranaExpRegion, rupiahsRegion, partyInviteScanRegion, partyInviteOkRegion, partyListRegion, disconnectMessageRegion, mapCoordinateXRegion, mapCoordinateYRegion, chatRegion)
            Dim mobLifeRegion As RectRegion = ResolveMobLifeRegion(cfg, fullClientWidth, fullClientHeight)
            Dim lootScanPolygon As List(Of DrawingPoint) = ResolveLootScanPolygon(cfg, fullClientWidth, fullClientHeight)
            Dim activeHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            Dim configuredFullFrameMs As Integer = Math.Max(100, If(cfg Is Nothing, FullFrameRefreshMs, cfg.FullFrameRefreshIntervalMs))
            Dim fullFrameIntervalMs As Integer = If(deferOptionalWork, configuredFullFrameMs * 3, configuredFullFrameMs)
            Dim lastFullFrameTime As DateTime = If(_latestLoopFrameCapturedAt <> DateTime.MinValue, _latestLoopFrameCapturedAt, _lastFullFrameCaptureAttemptAt)
            Dim fullFrameDue As Boolean =
                lastFullFrameTime = DateTime.MinValue OrElse
                (now - lastFullFrameTime).TotalMilliseconds >= fullFrameIntervalMs
            Dim pendingLootScannerReady As Boolean =
                _lootScannerCapturePending AndAlso
                activeHwnd = hwnd AndAlso
                _lootScannerCaptureRequestedAt <> DateTime.MinValue AndAlso
                (now - _lootScannerCaptureRequestedAt).TotalMilliseconds >= Math.Max(20, loopDelayMs)
            Dim pendingLootVerifyReady As Boolean =
                _pendingLootPickupVerifyAt <> DateTime.MinValue AndAlso
                now >= _pendingLootPickupVerifyAt
            Dim shouldCaptureFullFrame As Boolean = fullFrameDue OrElse pendingLootScannerReady OrElse pendingLootVerifyReady
            Dim frame As Bitmap = Nothing
            Dim visionWarning As String = ""

            If shouldCaptureFullFrame Then
                _lastFullFrameCaptureAttemptAt = now
                Dim mainCaptureWatch As Stopwatch = Stopwatch.StartNew()
                frame = CaptureClient(hwnd)
                mainCaptureWatch.Stop()
                RecordTiming(_captureTiming, mainCaptureWatch.Elapsed.TotalMilliseconds)
                SyncLock _perfSync
                    _lastCaptureMethodName = GetCachedCaptureMethodName(hwnd)
                End SyncLock

                If frame IsNot Nothing Then
                    If cfg.BlackScreenProtectionEnabled AndAlso IsLikelyBlackFrame(frame) Then
                        ClearCachedCaptureMethod(hwnd)
                        ClearLatestLoopFrame()
                        frame.Dispose()
                        frame = Nothing
                        visionWarning = "Vision glitch: black full-frame capture skipped; combat is using direct region reads."
                    Else
                        ReplaceLatestLoopFrame(frame)
                    End If
                End If
            End If

            If frame Is Nothing Then
                frame = GetLatestLoopFrameClone(Math.Max(1000, fullFrameIntervalMs * 3))
            End If

            Dim hpScanWatch As Stopwatch = Stopwatch.StartNew()
            Dim hpPct As Double = 0
            Dim mpPct As Double = 0
            Dim mobHpPct As Double = 0
            Dim fullHpScanOk As Boolean = True
            Dim fullMpScanOk As Boolean = True
            Dim mobHpScanOk As Boolean = True
            Dim mobHpRegionFrame As Bitmap = Nothing
            Dim localMobHpRegion As RectRegion = Nothing

            If frame IsNot Nothing Then
                hpPct = ComputeBarPercent(frame, hpRegion, True, cfg)
                mpPct = ComputeBarPercent(frame, mpRegion, False, cfg)
                mobHpPct = ComputeMobHpPercent(frame, mobHpRegion, cfg)
            Else
                hpPct = ComputeClientBarPercent(hwnd, hpRegion, True, cfg, fullHpScanOk)
                mpPct = ComputeClientBarPercent(hwnd, mpRegion, False, cfg, fullMpScanOk)
                mobHpRegionFrame = CaptureClientRegion(hwnd, mobHpRegion)
                If mobHpRegionFrame IsNot Nothing Then
                    localMobHpRegion = New RectRegion(0, 0, mobHpRegionFrame.Width, mobHpRegionFrame.Height)
                    mobHpPct = ComputeMobHpPercent(mobHpRegionFrame, localMobHpRegion, cfg)
                Else
                    mobHpScanOk = False
                End If
            End If
            hpScanWatch.Stop()
            RecordTiming(_hpMpScanTiming, hpScanWatch.Elapsed.TotalMilliseconds)
            Dim expPct As Double = GetCachedPranaExpPercent()
            Dim rupiahsTotal As Long = GetCachedRupiahsTotal()
            If frame Is Nothing AndAlso Not (fullHpScanOk OrElse fullMpScanOk OrElse mobHpScanOk) Then
                ClearLatestLoopFrame()
                ReleaseLootScannerAltKey()
                ClearMapLocalizationRuntime()
                ClearChatTranslationRuntime()
                ClearPartyListRuntimeState()
                If mobHpRegionFrame IsNot Nothing Then
                    mobHpRegionFrame.Dispose()
                End If
                UpdateLevelingAgentState(cfg, LevelingAgentState.Searching, "Unable to capture game client.")
                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.NotAttackingReason = "Capture failed."
                              s.ErrorMessage = "Unable to capture game client."
                              s.GameDisconnected = False
                          End Sub)
                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If
            Dim captureGlitch As Boolean = If(frame IsNot Nothing, IsLikelyVisionCaptureGlitch(frame, hpRegion, mpRegion, hpPct, mpPct), (Not fullHpScanOk OrElse Not fullMpScanOk))
            Dim gameDisconnected As Boolean =
                If(frame IsNot Nothing,
                   TryHandleDisconnectMessage(cfg, hwnd, frame, now, disconnectMessageRegion),
                   TryHandleDisconnectMessageFromClientRegion(cfg, hwnd, now, disconnectMessageRegion))
            If gameDisconnected Then
                ReleaseLootScannerAltKey()
                Dim disconnectWarning As String = If(visionWarning = "", "Game disconnected from server.", visionWarning & " Game disconnected from server.")
                UpdateLevelingAgentState(cfg, LevelingAgentState.GuardedStop, "Game disconnected from server.")
                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.HpPercent = Math.Round(hpPct, 1)
                              s.MpPercent = Math.Round(mpPct, 1)
                              s.MobHpPercent = Math.Round(mobHpPct, 1)
                              s.MobMaxHp = -1
                              s.MobHpText = ""
                              s.RupiahsTotal = -1
                              s.RupiahsPerHour = -1
                              s.TargetValid = False
                              s.NotAttackingReason = "Game disconnected message detected."
                              s.ErrorMessage = disconnectWarning
                              s.GameDisconnected = True
                          End Sub)
                If frame IsNot Nothing Then
                    frame.Dispose()
                End If
                If mobHpRegionFrame IsNot Nothing Then
                    mobHpRegionFrame.Dispose()
                End If
                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Await Task.Delay(loopDelayMs, token)
                Continue While
            End If

            Dim lootScanWatch As Stopwatch = Stopwatch.StartNew()
            TryHandlePendingLootScannerCapture(cfg, hwnd, activeHwnd, frame, lootScanPolygon, now)
            lootScanWatch.Stop()
            RecordTiming(_lootScanTiming, lootScanWatch.Elapsed.TotalMilliseconds)
            TryHandlePendingLootPickupVerification(cfg, hwnd, frame, now, mobNameRegion)
            If cfg.LootScannerEnabled AndAlso deferOptionalWork AndAlso activeHwnd = hwnd AndAlso (Not _lootScannerCapturePending) Then
                MarkOptionalWorkDeferred()
            ElseIf cfg.LootScannerEnabled AndAlso activeHwnd = hwnd AndAlso (Not _lootScannerCapturePending) AndAlso (now - _lastRightAltAt).TotalMilliseconds >= Math.Max(1000, cfg.LootScannerIntervalMs) Then
                BeginLootScannerCapture(now)
            End If
            Dim mobOcrWatch As Stopwatch = Stopwatch.StartNew()
            Dim monsterFilterActive As Boolean = (cfg.DeniedMobs IsNot Nothing AndAlso cfg.DeniedMobs.Count > 0)
            Dim monsterFilterWhitelistMode As Boolean = IsMonsterFilterWhitelistMode(cfg)
            Dim monsterFilterConfirmRequired As Integer = GetMonsterFilterConfirmRequiredCount(cfg)
            Dim targetWindowSignalNoName As Boolean =
                If(frame IsNot Nothing,
                   HasTargetWindowSignal(frame, mobHpRegion, "", mobHpPct, cfg),
                   If(mobHpRegionFrame IsNot Nothing AndAlso localMobHpRegion IsNot Nothing,
                      HasTargetWindowSignal(mobHpRegionFrame, localMobHpRegion, "", mobHpPct, cfg),
                      mobHpPct >= Math.Max(0.6, cfg.MobHpPresenceThreshold * 0.7)))
            Dim shouldReadMobName As Boolean =
                frame IsNot Nothing OrElse
                targetWindowSignalNoName OrElse
                (mobHpPct >= Math.Max(0.6, cfg.MobHpPresenceThreshold * 0.7))
            Dim forceMobNameRefresh As Boolean = monsterFilterActive AndAlso targetWindowSignalNoName AndAlso ((now - _lastMobNameRead).TotalMilliseconds >= 180)
            Dim mobName As String
            If shouldReadMobName Then
                mobName = If(frame IsNot Nothing,
                             ReadMobNameIfNeeded(frame, mobNameRegion, now, forceMobNameRefresh, cfg.MobNameScanIntervalMs),
                             ReadMobNameFromClientRegionIfNeeded(hwnd, mobNameRegion, now, forceMobNameRefresh, cfg.MobNameScanIntervalMs))
            Else
                ' Avoid stale-name attacks after target switches.
                _cachedMobName = ""
                _lastMobNameRead = DateTime.MinValue
                _lastMobNameDetectedAt = DateTime.MinValue
                _mobNameOcrStartedAt = DateTime.MinValue
                _mobNameOcrTask = Nothing
                mobName = ""
            End If
            ApplyVisionStabilityFilter(hpPct, mpPct, mobHpPct, mobName, captureGlitch)
            Dim expPerHour As Double = UpdateExpRate(expPct, now)
            Dim rupiahsPerHour As Double = UpdateRupiahsRate(rupiahsTotal, now)
            Dim mapCoordinateFeaturesEnabled As Boolean = cfg.NavigationEnabled OrElse cfg.HoldPlaceEnabled
            Dim mapCoordinateReadRequired As Boolean = cfg.HoldPlaceEnabled
            If mapCoordinateFeaturesEnabled AndAlso Not startupCombatPriorityActive AndAlso (Not deferOptionalWork OrElse mapCoordinateReadRequired) Then
                ReadMapCoordinateIfNeeded(hwnd, frame, mapCoordinateXRegion, mapCoordinateYRegion, cfg, now)
                ScanMapPlayerMarkerIfNeeded(now)
                UpdateMapLocalizationConfidence()
                UpdateMapVisibleState()
                UpdateLastKnownNavigationPose(now)
                If cfg.NavigationEnabled Then
                    UpdateRouteRecording(cfg, now)
                    UpdateNavigationPreview(cfg, now)
                Else
                    ClearNavigationPreviewRuntime()
                    _routeRecordingCaptureActive = False
                    _routeRecordingStatus = ""
                End If
            ElseIf mapCoordinateFeaturesEnabled AndAlso Not startupCombatPriorityActive AndAlso deferOptionalWork Then
                AppendMapCoordinateDebug(now, "not checking: adaptive performance deferred coordinate OCR this loop.")
                MarkOptionalWorkDeferred()
            Else
                If mapCoordinateFeaturesEnabled AndAlso startupCombatPriorityActive Then
                    AppendMapCoordinateDebug(now, "not checking: startup combat-priority window is active.")
                ElseIf Not mapCoordinateFeaturesEnabled Then
                    AppendMapCoordinateDebug(now, "not checking: navigation and Hold on place are disabled.")
                End If
                ClearMapLocalizationRuntime()
                ClearNavigationPreviewRuntime()
                ClearNavigationTravelRuntime()
                ClearHoldPlaceRuntime()
            End If
            If Not cfg.HoldPlaceEnabled Then
                ClearHoldPlaceRuntime()
            End If
            If Not cfg.NavigationEnabled AndAlso Not cfg.HoldPlaceEnabled Then
                ClearNavigationTravelRuntime()
            End If
            If cfg.ChatTranslationEnabled AndAlso Not startupCombatPriorityActive AndAlso Not deferOptionalWork Then
                Dim chatOcrWatch As Stopwatch = Stopwatch.StartNew()
                ReadChatTextIfNeeded(frame, chatRegion, cfg, now)
                chatOcrWatch.Stop()
                RecordTiming(_chatOcrTiming, chatOcrWatch.Elapsed.TotalMilliseconds)
            ElseIf cfg.ChatTranslationEnabled AndAlso Not startupCombatPriorityActive AndAlso deferOptionalWork Then
                MarkOptionalWorkDeferred()
            Else
                ClearChatTranslationRuntime()
            End If
            If Not startupCombatPriorityActive AndAlso Not deferOptionalWork Then
                ReadPartyListIfNeeded(frame, partyListRegion, cfg, now)
            ElseIf Not startupCombatPriorityActive AndAlso deferOptionalWork Then
                MarkOptionalWorkDeferred()
            End If
            Dim targetWindowVisible As Boolean =
                If(frame IsNot Nothing,
                   HasTargetWindowSignal(frame, mobHpRegion, mobName, mobHpPct, cfg),
                   If(mobHpRegionFrame IsNot Nothing AndAlso localMobHpRegion IsNot Nothing,
                      HasTargetWindowSignal(mobHpRegionFrame, localMobHpRegion, mobName, mobHpPct, cfg),
                      targetWindowSignalNoName))
            Dim hasHighMaxHpAction As Boolean = HasHighMaxHpAttackAction(cfg)
            Dim mobMaxHp As Integer =
                If(startupCombatPriorityActive,
                   _lastMobDetectedMaxHp,
                   If(frame IsNot Nothing,
                      UpdateMobMaxHpTracking(cfg, frame, mobLifeRegion, targetWindowVisible, mobHpPct, now),
                      UpdateMobMaxHpTrackingFromClientRegion(cfg, hwnd, mobLifeRegion, targetWindowVisible, mobHpPct, now)))
            mobOcrWatch.Stop()
            RecordTiming(_mobOcrTiming, mobOcrWatch.Elapsed.TotalMilliseconds)
            Dim highMaxHpAttackActive As Boolean =
                cfg.HighMaxHpSpecialEnabled AndAlso
                hasHighMaxHpAction AndAlso
                mobMaxHp >= Math.Max(1, cfg.HighMaxHpThreshold)
            Dim avoidHighMaxHpTarget As Boolean =
                cfg.AvoidHighMaxHpEnabled AndAlso
                mobMaxHp >= Math.Max(1, cfg.AvoidHighMaxHpThreshold)
            If targetWindowVisible Then
                _lastTargetWindowSeen = now
                _noTargetBeganAt = DateTime.MinValue
            ElseIf _noTargetBeganAt = DateTime.MinValue Then
                _noTargetBeganAt = now
            End If
            Dim normMobName As String = NormalizeMobName(mobName)
            Dim listedMonsterTarget As Boolean = IsDeniedMob(mobName, cfg.DeniedMobs)
            Dim monsterFilterBlockedTarget As Boolean =
                monsterFilterActive AndAlso
                normMobName <> "" AndAlso
                If(monsterFilterWhitelistMode, Not listedMonsterTarget, listedMonsterTarget)
            Dim preferredMobFilterActive As Boolean = cfg.LevelingAgentEnabled AndAlso cfg.LevelingPreferredMobs IsNot Nothing AndAlso cfg.LevelingPreferredMobs.Count > 0
            Dim missingNameBlockedByPreference As Boolean = preferredMobFilterActive AndAlso targetWindowVisible AndAlso normMobName = ""
            Dim preferredTargetMismatch As Boolean = preferredMobFilterActive AndAlso normMobName <> "" AndAlso Not IsPreferredMob(mobName, cfg.LevelingPreferredMobs)
            Dim unreachableTriggered As Boolean =
                If(startupCombatPriorityActive,
                   False,
                   If(frame IsNot Nothing,
                      TryHandleUnreachableTarget(cfg, hwnd, frame, now, unreachableTextRegion),
                      TryHandleUnreachableTargetFromClientRegion(cfg, hwnd, now, unreachableTextRegion)))
            Dim unreachableLockActive As Boolean = (_unreachableLockUntil <> DateTime.MinValue AndAlso now < _unreachableLockUntil)
            If unreachableTriggered Then
                _agentUnreachableEvents += 1
            End If

            If monsterFilterActive AndAlso monsterFilterBlockedTarget Then
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
            ElseIf monsterFilterBlockedTarget Then
                ' Already handled above; keep state reset while the filter blocks the current name.
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

                If _nameConfirmCount >= monsterFilterConfirmRequired Then
                    _nameConfirmConfirmedName = normMobName
                End If
            End If

            Dim blacklistLockActive As Boolean = monsterFilterActive AndAlso _blacklistLockUntil <> DateTime.MinValue AndAlso now < _blacklistLockUntil
            Dim nameConfirmedForAttack As Boolean = (Not monsterFilterActive) OrElse (normMobName <> "" AndAlso _nameConfirmConfirmedName.Equals(normMobName, StringComparison.OrdinalIgnoreCase))
            Dim missingNameBlockedByFilter As Boolean = monsterFilterActive AndAlso targetWindowVisible AndAlso normMobName = ""
            Dim nameConfirmationBlockedByFilter As Boolean = monsterFilterActive AndAlso targetWindowVisible AndAlso (Not missingNameBlockedByFilter) AndAlso (Not monsterFilterBlockedTarget) AndAlso (Not nameConfirmedForAttack)
            Dim targetHasHpSignal As Boolean = HasLivingTargetSignal(targetWindowVisible, mobHpPct, cfg)
            Dim nameOnlyNonMobTarget As Boolean = normMobName <> "" AndAlso (Not targetHasHpSignal) AndAlso mobMaxHp <= 0
            If nameOnlyNonMobTarget Then
                ClearCombatLock()
                _firstHitPending = False
                _firstHitTargetSignature = ""
                _firstHitWindowUntil = DateTime.MinValue
                _lastTargetValidAt = DateTime.MinValue
                _lastLivingTargetSignalAt = DateTime.MinValue
                _lastTargetWindowSeen = DateTime.MinValue
                _noDamageTargetSignature = ""
                _noDamageAttackCount = 0
            End If
            Dim currentTargetAliveSignal As Boolean = targetHasHpSignal
            If currentTargetAliveSignal Then
                _lastLivingTargetSignalAt = now
                _noTargetBeganAt = DateTime.MinValue
            End If
            Dim combatLockActive As Boolean = UpdateCombatLockState(now, cfg, currentTargetAliveSignal, normMobName)
            Dim canTrackFirstHitTarget As Boolean = currentTargetAliveSignal AndAlso (Not monsterFilterBlockedTarget) AndAlso (Not missingNameBlockedByFilter) AndAlso (Not avoidHighMaxHpTarget)
            Dim currentFirstHitSignature As String = normMobName
            If canTrackFirstHitTarget Then
                Dim isNewFirstHitTarget As Boolean = (Not _firstHitPending) OrElse ((currentFirstHitSignature <> "") AndAlso (Not _firstHitTargetSignature.Equals(currentFirstHitSignature, StringComparison.OrdinalIgnoreCase)))
                If isNewFirstHitTarget Then
                    _firstHitPending = True
                    _firstHitTargetSignature = currentFirstHitSignature
                    _firstHitWindowUntil = now.AddMilliseconds(FirstHitWindowMs)
                End If
            ElseIf Not targetWindowVisible OrElse monsterFilterBlockedTarget OrElse avoidHighMaxHpTarget Then
                _firstHitPending = False
                _firstHitTargetSignature = ""
                _firstHitWindowUntil = DateTime.MinValue
            End If
            Dim firstHitWindowActive As Boolean = _firstHitPending AndAlso now < _firstHitWindowUntil
            Dim targetValid As Boolean =
                currentTargetAliveSignal AndAlso
                (Not monsterFilterBlockedTarget) AndAlso
                (Not missingNameBlockedByFilter) AndAlso
                (Not missingNameBlockedByPreference) AndAlso
                (Not preferredTargetMismatch) AndAlso
                (Not nameConfirmationBlockedByFilter) AndAlso
                (Not avoidHighMaxHpTarget) AndAlso
                (Not blacklistLockActive) AndAlso
                (Not unreachableLockActive)
            If targetValid Then
                _lastTargetValidAt = now
            End If
            Dim targetActionBlocked As Boolean =
                monsterFilterBlockedTarget OrElse
                avoidHighMaxHpTarget OrElse
                blacklistLockActive OrElse
                missingNameBlockedByFilter OrElse
                missingNameBlockedByPreference OrElse
                preferredTargetMismatch OrElse
                nameConfirmationBlockedByFilter OrElse
                unreachableLockActive
            Dim targetSignalHoldActive As Boolean = (Not targetActionBlocked) AndAlso IsRecentTargetSignalHoldActive(now, cfg)
            If nameOnlyNonMobTarget Then
                targetSignalHoldActive = False
            End If
            Dim effectiveTargetValid As Boolean = targetValid OrElse targetSignalHoldActive OrElse ((Not nameOnlyNonMobTarget) AndAlso combatLockActive AndAlso Not targetActionBlocked)
            TrackMobHpMovement(targetValid, mobHpPct, now)

            Dim guardrailReason As String = ""
            If ShouldTriggerLevelingGuardrail(cfg, hpPct, mpPct, expPerHour, now, targetWindowVisible, guardrailReason) Then
                If frame IsNot Nothing Then
                    frame.Dispose()
                End If
                If mobHpRegionFrame IsNot Nothing Then
                    mobHpRegionFrame.Dispose()
                End If
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
            If Not actionSent Then
                actionSent = TrySendRepairAction(cfg, hwnd)
                If actionSent Then
                    reason = "Repair warning detected in unreachable text. Repair key sent."
                End If
            End If
            Dim forcedRetarget As Boolean = False

            If nameOnlyNonMobTarget AndAlso Not actionSent Then
                If TrySendRetargetKey(hwnd, cfg, now, "E (non-mob target without HP bar)", forced:=True) Then
                    reason = $"Selected target '{mobName}' has no mob HP bar/life numbers. Retarget key sent."
                    forcedRetarget = True
                    actionSent = True
                Else
                    reason = $"Selected target '{mobName}' has no mob HP bar/life numbers. Waiting retarget cooldown."
                End If
            End If

            If Not forcedRetarget AndAlso ShouldBypassStuckTarget(cfg, targetWindowVisible, targetValid, now) Then
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

            If Not forcedRetarget AndAlso Not actionSent AndAlso avoidHighMaxHpTarget Then
                If TrySendRetargetKey(hwnd, cfg, now, "E (avoid high max HP target)", forced:=True) Then
                    _noDamageTargetSignature = ""
                    _noDamageAttackCount = 0
                    _firstHitPending = False
                    _firstHitTargetSignature = ""
                    _firstHitWindowUntil = DateTime.MinValue
                    reason = $"Avoided high Max HP mob ({mobMaxHp:N0} >= {Math.Max(1, cfg.AvoidHighMaxHpThreshold):N0}). Retarget key sent."
                    forcedRetarget = True
                    actionSent = True
                Else
                    reason = $"Avoiding high Max HP mob ({mobMaxHp:N0} >= {Math.Max(1, cfg.AvoidHighMaxHpThreshold):N0}). Waiting retarget cooldown."
                End If
            End If

            If Not forcedRetarget AndAlso Not actionSent Then
                Dim supportSent As Boolean = TrySendSupportActions(cfg, hwnd, hpPct, mpPct, hpRegion, mpRegion)
                If supportSent Then
                    actionSent = True
                    reason = ""
                End If

                If Not actionSent Then
                    If cfg.HoldPlaceEnabled Then
                        Dim holdReason As String = ""
                        Dim holdCombatActive As Boolean = targetWindowVisible OrElse targetValid OrElse effectiveTargetValid OrElse combatLockActive OrElse targetSignalHoldActive
                        Dim holdBlocksRetarget As Boolean = False
                        If TryHandleHoldPlace(cfg, hwnd, now, holdCombatActive, holdReason, holdBlocksRetarget) Then
                            actionSent = True
                            reason = holdReason
                        ElseIf holdBlocksRetarget Then
                            actionSent = True
                            reason = holdReason
                        ElseIf String.IsNullOrWhiteSpace(reason) AndAlso Not String.IsNullOrWhiteSpace(holdReason) Then
                            reason = holdReason
                        End If
                    ElseIf Not targetWindowVisible AndAlso Not combatLockActive AndAlso Not targetSignalHoldActive Then
                        Dim travelReason As String = ""
                        If TryHandleNavigationTravel(cfg, hwnd, now, targetWindowVisible, targetValid, travelReason) Then
                            actionSent = True
                            reason = travelReason
                        ElseIf String.IsNullOrWhiteSpace(reason) AndAlso Not String.IsNullOrWhiteSpace(travelReason) Then
                            reason = travelReason
                        End If

                        If _lastNavigationTravelActive AndAlso Not targetWindowVisible AndAlso Not targetValid Then
                            Dim travelScanReason As String = ""
                            If TryScanForMobDuringTravel(cfg, hwnd, now, travelScanReason) Then
                                actionSent = True
                                If String.IsNullOrWhiteSpace(reason) Then
                                    reason = travelScanReason
                                ElseIf Not String.IsNullOrWhiteSpace(travelScanReason) Then
                                    reason &= " " & travelScanReason
                                End If
                            End If
                        End If
                    End If
                End If

                ' Support keys can fire without blocking attack/buff in the same loop.
                Dim allowBlindAttack As Boolean = AllowBlindAttackWhenTargetMissing AndAlso (Not monsterFilterActive) AndAlso (Not monsterFilterBlockedTarget) AndAlso (Not avoidHighMaxHpTarget) AndAlso (Not _lastNavigationTravelActive)
                Dim suppressOffensiveBuffsForBlacklist As Boolean =
                    monsterFilterActive AndAlso
                    (Not monsterFilterWhitelistMode) AndAlso
                    (monsterFilterBlockedTarget OrElse blacklistLockActive)
                Dim attackBurst As List(Of ActionRule) = ChooseAttackBurstActions(cfg, hpPct, mpPct, effectiveTargetValid, allowBlindAttack, highMaxHpAttackActive, suppressOffensiveBuffsForBlacklist, reason)
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

            If Not effectiveTargetValid AndAlso Not actionSent AndAlso Not _lastNavigationTravelActive Then
                Dim filterBlockedRetarget As Boolean = targetActionBlocked
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
                    Dim firstRetargetReady As Boolean = _lastNormalRetarget = DateTime.MinValue AndAlso _lastForcedRetarget = DateTime.MinValue AndAlso _lastTargetWindowSeen = DateTime.MinValue
                    If (Not filterBlockedRetarget) AndAlso _lastTargetWindowSeen <> DateTime.MinValue AndAlso (now - _lastTargetWindowSeen).TotalMilliseconds < retargetDelayMs Then
                        If String.IsNullOrWhiteSpace(reason) Then
                            reason = $"Target window just changed. Waiting {retargetDelayMs}ms before retarget."
                        End If
                    ElseIf (Not firstRetargetReady) AndAlso _noTargetBeganAt <> DateTime.MinValue AndAlso (now - _noTargetBeganAt).TotalMilliseconds < noTargetStableMs Then
                        If String.IsNullOrWhiteSpace(reason) Then
                            reason = $"No target not stable yet. Waiting {noTargetStableMs}ms."
                        End If
                    ElseIf (_lastNormalRetarget = DateTime.MinValue) OrElse (now - _lastNormalRetarget).TotalMilliseconds >= retargetDelayMs Then
                        If TrySendRetargetKey(hwnd, cfg, now, "E (retarget)", forced:=False) Then
                            _noDamageTargetSignature = ""
                            _noDamageAttackCount = 0
                            If String.IsNullOrWhiteSpace(reason) Then
                                If monsterFilterBlockedTarget Then
                                    reason = If(monsterFilterWhitelistMode,
                                                $"Monster whitelist skipped non-listed mob '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent.",
                                                $"Monster blacklist blocked target '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent.")
                                ElseIf avoidHighMaxHpTarget Then
                                    reason = $"Avoided high Max HP mob ({mobMaxHp:N0} >= {Math.Max(1, cfg.AvoidHighMaxHpThreshold):N0}). Retarget key sent."
                                ElseIf blacklistLockActive Then
                                    reason = $"Monster filter lock active ({BlacklistLockWindowMs}ms). Retarget key sent."
                                ElseIf missingNameBlockedByFilter Then
                                    reason = "Monster filter waiting for mob name OCR. Retarget key sent."
                                ElseIf missingNameBlockedByPreference Then
                                    reason = "Leveling agent waiting for mob name OCR before preferred-mob check. Retarget key sent."
                                ElseIf preferredTargetMismatch Then
                                    reason = $"Leveling agent skipped non-preferred mob '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent."
                                ElseIf nameConfirmationBlockedByFilter Then
                                    reason = $"Monster filter waiting for {monsterFilterConfirmRequired}x name confirmation. Retarget key sent."
                                ElseIf unreachableLockActive Then
                                    reason = "Unable-to-reach lock active. Retarget key sent."
                                ElseIf nameOnlyNonMobTarget Then
                                    reason = "Selected target has no mob HP bar/life numbers. Retarget key sent."
                                ElseIf Not targetWindowVisible Then
                                    reason = "No target window detected. Retarget key sent."
                                Else
                                    reason = "No target detected. Retarget key sent."
                                End If
                            End If
                        End If
                    ElseIf String.IsNullOrWhiteSpace(reason) Then
                        If monsterFilterBlockedTarget Then
                            reason = If(monsterFilterWhitelistMode,
                                        "Monster whitelist skipped non-listed mob. Waiting retarget cooldown.",
                                        "Monster blacklist blocked target. Waiting retarget cooldown.")
                        ElseIf avoidHighMaxHpTarget Then
                            reason = $"Avoiding high Max HP mob ({mobMaxHp:N0} >= {Math.Max(1, cfg.AvoidHighMaxHpThreshold):N0}). Waiting retarget cooldown."
                        ElseIf blacklistLockActive Then
                            reason = $"Monster filter lock active ({BlacklistLockWindowMs}ms). Waiting retarget cooldown."
                        ElseIf missingNameBlockedByFilter Then
                            reason = "Monster filter waiting for mob name OCR. Waiting retarget cooldown."
                        ElseIf missingNameBlockedByPreference Then
                            reason = "Leveling agent waiting for mob name OCR before preferred-mob check."
                        ElseIf preferredTargetMismatch Then
                            reason = "Leveling agent is searching for a preferred mob."
                        ElseIf nameConfirmationBlockedByFilter Then
                            reason = $"Monster filter waiting for {monsterFilterConfirmRequired}x name confirmation. Waiting retarget cooldown."
                        ElseIf unreachableLockActive Then
                            reason = "Unable-to-reach lock active. Waiting retarget cooldown."
                        ElseIf nameOnlyNonMobTarget Then
                            reason = "Selected target has no mob HP bar/life numbers. Waiting retarget cooldown."
                        ElseIf Not targetWindowVisible Then
                            reason = $"No target window detected. Waiting {retargetDelayMs}ms retarget cooldown."
                        Else
                            reason = $"No target detected. Waiting {retargetDelayMs}ms retarget cooldown."
                        End If
                    End If
                End If
            End If

            TryHandleLootPickup(cfg, hwnd, now, actionSent OrElse _firstHitPending)
            TryHandleArrowUnbundle(cfg, hwnd, fullClientWidth, fullClientHeight, now, actionSent OrElse _firstHitPending)
            UpdateLevelingAgentRuntimeState(cfg, now, hpPct, mpPct, targetWindowVisible, effectiveTargetValid, actionSent, forcedRetarget OrElse unreachableTriggered, unreachableLockActive, reason)

            Dim statsOcrDue As Boolean = IsStatsOcrDue(now)
            If deferOptionalWork AndAlso Not statsOcrDue Then
                MarkOptionalWorkDeferred()
            Else
                If frame IsNot Nothing Then
                    TryQueueHardcodedVisionStats(cfg, hwnd, now, frame, hpRegion, mpRegion, hpPct, mpPct, mobName)
                End If

                expPct = ReadPranaExpPercent(hwnd, frame, pranaExpRegion)
                rupiahsTotal = ReadRupiahsTotal(hwnd, frame, rupiahsRegion)
                expPerHour = UpdateExpRate(expPct, now)
                rupiahsPerHour = UpdateRupiahsRate(rupiahsTotal, now)
            End If

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
                          s.TargetValid = effectiveTargetValid
                          s.NotAttackingReason = If(actionSent, "", reason)
                          s.ErrorMessage = visionWarning
                          s.GameDisconnected = False
                      End Sub)
            If frame IsNot Nothing Then
                frame.Dispose()
            End If
            If mobHpRegionFrame IsNot Nothing Then
                mobHpRegionFrame.Dispose()
            End If

                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Await Task.Delay(loopDelayMs, token)
            Catch ex As OperationCanceledException When token.IsCancellationRequested
                Exit While
            Catch ex As Exception
                ReleaseLootScannerAltKey()
                ClearLatestLoopFrame()
                RaiseEvent LogLine("Bot loop recovered from unexpected error: " & ex.Message)
                SetStatus(Sub(s)
                              s.NotAttackingReason = "Loop recovered from error."
                              s.ErrorMessage = ex.Message
                              s.GameDisconnected = False
                          End Sub)
                RecordLoopCompletion(loopWatch.Elapsed.TotalMilliseconds, loopDelayMs)
                Thread.Sleep(Math.Max(50, loopDelayMs))
            End Try
        End While

        ReleaseLootScannerAltKey()
        ClearLatestLoopFrame()
    End Function

    Private Sub BeginLootScannerCapture(now As DateTime)
        Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&HA5), 0UI))
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
            If _lootScannerCaptureRequestedAt <> DateTime.MinValue AndAlso (now - _lootScannerCaptureRequestedAt).TotalMilliseconds >= 500 Then
                ReleaseLootScannerAltKey()
                _lootScannerCapturePending = False
                _lootScannerCaptureRequestedAt = DateTime.MinValue
            End If
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

        If _lootScannerProcessingTask IsNot Nothing Then
            If Not _lootScannerProcessingTask.IsCompleted Then
                RaiseEvent LogLine("Loot scanner skipped: previous scan is still running.")
                ReleaseLootScannerAltKey()
                _lootScannerCapturePending = False
                _lootScannerCaptureRequestedAt = DateTime.MinValue
                Return
            End If
            _lootScannerProcessingTask = Nothing
        End If

        Dim frameClone As Bitmap = DirectCast(frame.Clone(), Bitmap)
        Dim allowedNames As List(Of String) = If(cfg.LootAllowedNames, New List(Of String)()).ToList()
        Dim lootMatchThresholdPercent As Integer = ClampLootMatchThresholdPercent(cfg.LootNameMatchThresholdPercent)
        Dim lootScanPolygonCopy As List(Of DrawingPoint) = ClonePointList(lootScanPolygon)
        Dim topic As String = If(cfg.ItemNtfyTopic, "").Trim()
        Dim notificationProvider As String = NormalizeNotificationProviderName(cfg.NotificationProvider)
        Dim discordWebhookUrl As String = GetDiscordItemWebhookUrl(cfg)
        Dim pixelGateEnabled As Boolean = cfg.PixelChangeGateEnabled

        _lootScannerProcessingTask = Task.Run(Sub()
            Dim scanFrame As Bitmap = frameClone
            Dim lootScanFrame As Bitmap = Nothing
            Try
                Dim lootScanBounds As Rectangle = GetPolygonBounds(scanFrame, lootScanPolygonCopy)
                lootScanFrame = CropBitmapToPolygon(scanFrame, lootScanPolygonCopy)
                If lootScanFrame Is Nothing Then
                    lootScanFrame = DirectCast(scanFrame.Clone(), Bitmap)
                    lootScanBounds = New Rectangle(0, 0, lootScanFrame.Width, lootScanFrame.Height)
                End If

                If pixelGateEnabled Then
                    Dim signature As ULong = ComputeVisualSignature(lootScanFrame)
                    If signature <> 0UL AndAlso signature = _lastLootScannerVisualSignature Then
                        Return
                    End If
                    _lastLootScannerVisualSignature = signature
                End If

                Dim ocrRegions As List(Of OcrReader.OcrTextRegion) = OcrReader.ReadScreenTextRegionsIsolated(lootScanFrame)
                Dim ocrText As String = String.Join(Environment.NewLine, ocrRegions.Select(Function(region) region.Text))
                If Not String.IsNullOrWhiteSpace(ocrText) AndAlso allowedNames IsNot Nothing Then
                    Dim matchedItem As String = ""
                    Dim matchedRegion As OcrReader.OcrTextRegion = Nothing
                    If TryFindAllowedLootRegionMatch(ocrRegions, allowedNames, lootMatchThresholdPercent, matchedItem, matchedRegion) OrElse
                       TryFindAllowedLootMatch(ocrText, allowedNames, lootMatchThresholdPercent, matchedItem) Then
                        System.Media.SystemSounds.Exclamation.Play()
                        Console.Beep(800, 1000)
                        Console.Beep(800, 1000)
                        RaiseEvent LogLine($"LOOT ALARM: Found {matchedItem} (fuzzy {lootMatchThresholdPercent}%).")

                        If cfg.LootNameAutoPickupEnabled Then
                            TryExecuteLootNameAutoPickup(hwnd, cfg, matchedItem, matchedRegion, lootScanBounds)
                        End If

                        If notificationProvider = NotificationProviderDiscord Then
                            Task.Run(Async Function()
                                Try
                                    If String.IsNullOrWhiteSpace(discordWebhookUrl) Then
                                        RaiseEvent LogLine("Item notification skipped: Discord webhook URL is empty.")
                                        Return
                                    End If
                                    If Not IsLikelyDiscordWebhookUrl(discordWebhookUrl) Then
                                        RaiseEvent LogLine("Item notification skipped: Discord webhook URL format is invalid.")
                                        Return
                                    End If

                                    Using client As New System.Net.Http.HttpClient()
                                        Using request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, NormalizeDiscordWebhookUrl(discordWebhookUrl))
                                            Dim payload = New With {
                                                .username = "KathanaBot",
                                                .content = $"KathanaBot Loot Finder{Environment.NewLine}Found important item: {matchedItem}",
                                                .allowed_mentions = New With {
                                                    .parse = Array.Empty(Of String)()
                                                }
                                            }
                                            request.Content = New System.Net.Http.StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                                            Dim response As System.Net.Http.HttpResponseMessage = Await client.SendAsync(request)
                                            If Not response.IsSuccessStatusCode Then
                                                Dim responseText As String = ""
                                                If response.Content IsNot Nothing Then
                                                    responseText = (Await response.Content.ReadAsStringAsync()).Trim()
                                                End If
                                                If responseText <> "" Then
                                                    RaiseEvent LogLine($"Item notification failed via Discord ({CInt(response.StatusCode)}): {responseText}")
                                                Else
                                                    RaiseEvent LogLine($"Item notification failed via Discord ({CInt(response.StatusCode)}).")
                                                End If
                                            End If
                                        End Using
                                    End Using
                                Catch ex As Exception
                                    RaiseEvent LogLine("Item notification failed via Discord: " & ex.Message)
                                End Try
                            End Function)
                        ElseIf Not String.IsNullOrWhiteSpace(topic) Then
                            Task.Run(Async Function()
                                Try
                                    Using client As New System.Net.Http.HttpClient()
                                        Using request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://ntfy.sh/" & Uri.EscapeDataString(topic))
                                            request.Content = New System.Net.Http.StringContent("Found important item: " & matchedItem)
                                            request.Headers.Add("Title", "KathanaBot Loot Finder")
                                            Dim response As System.Net.Http.HttpResponseMessage = Await client.SendAsync(request)
                                            If Not response.IsSuccessStatusCode Then
                                                RaiseEvent LogLine($"Item notification failed via ntfy ({CInt(response.StatusCode)}) for topic '{topic}'.")
                                            End If
                                        End Using
                                    End Using
                                Catch ex As Exception
                                    RaiseEvent LogLine("Item notification failed via ntfy: " & ex.Message)
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

        Dim rightAltScan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&HA5), 0UI))
        Dim genericAltScan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&H12), 0UI))
        Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1
        Dim KEYEVENTF_KEYUP As UInteger = &H2

        Try
            keybd_event(&HA5, rightAltScan, KEYEVENTF_EXTENDEDKEY Or KEYEVENTF_KEYUP, UIntPtr.Zero)
            keybd_event(&H12, genericAltScan, KEYEVENTF_KEYUP, UIntPtr.Zero)
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

        Using buffer As New BitmapReadBuffer(frame)
            For y As Integer = rect.Top To rect.Bottom - 1 Step stepY
                For x As Integer = rect.Left To rect.Right - 1 Step stepX
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    Dim luma As Integer = (r * 30 + g * 59 + b * 11) \ 100
                    samples += 1
                    sumLuma += luma
                    If luma >= 28 Then
                        brightSamples += 1
                    End If
                Next
            Next
        End Using

        If samples = 0 Then
            Return True
        End If

        Dim avgLuma As Double = sumLuma / CDbl(samples)
        Dim brightRatio As Double = brightSamples / CDbl(samples)
        Return avgLuma <= 15.0 AndAlso brightRatio <= 0.04
    End Function

    Private Sub ApplyVisionStabilityFilter(ByRef hpPct As Double, ByRef mpPct As Double, ByRef mobHpPct As Double, ByRef mobName As String, captureGlitch As Boolean)
        UpdateNearZeroSupportConfirmations(hpPct, mpPct, captureGlitch)

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
        Dim sustainedSingleMpZero As Boolean = _singleMpZeroConfirmCount >= SustainedSingleManaZeroConfirmRequiredCount

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

        Dim invalidZeroPair As Boolean =
            bothNearZero AndAlso
            (captureGlitch OrElse
             Not hasBaseline OrElse
             _lastGoodHpPercent >= 5.0 OrElse
             _lastGoodMpPercent >= 5.0)

        If captureGlitch OrElse invalidZeroPair OrElse suspiciousSingleHpZero OrElse suspiciousSingleMpZero Then
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

            hpPct = 100.0
            mpPct = 100.0
            mobHpPct = 0.0
            mobName = ""
            Return
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

    Private Sub UpdateNearZeroSupportConfirmations(hpPct As Double, mpPct As Double, captureGlitch As Boolean)
        Dim hpNearZero As Boolean = hpPct <= 0.25R
        Dim mpNearZero As Boolean = mpPct <= 0.25R
        Dim impossibleZeroPair As Boolean =
            hpNearZero AndAlso
            mpNearZero AndAlso
            (_lastGoodHpPercent >= 5.0R OrElse _lastGoodMpPercent >= 5.0R)

        If captureGlitch OrElse impossibleZeroPair Then
            _hpZeroSupportConfirmCount = 0
            _mpZeroSupportConfirmCount = 0
            Return
        End If

        If hpNearZero Then
            _hpZeroSupportConfirmCount += 1
        Else
            _hpZeroSupportConfirmCount = 0
        End If

        If mpNearZero Then
            _mpZeroSupportConfirmCount += 1
        Else
            _mpZeroSupportConfirmCount = 0
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

        If cfg.LevelingStopHpEnabled AndAlso hpPct <= Math.Max(1, cfg.LevelingStopHpPercent) Then
            guardrailReason = $"HP reached leveling stop threshold ({hpPct:0.0}% <= {cfg.LevelingStopHpPercent}%)."
            Return True
        End If

        If cfg.LevelingStopMpEnabled AndAlso mpPct <= Math.Max(1, cfg.LevelingStopMpPercent) Then
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

        If cfg.LevelingMaxNoTargetEnabled AndAlso cfg.LevelingMaxNoTargetSeconds > 0 AndAlso Not targetWindowVisible AndAlso _noTargetBeganAt <> DateTime.MinValue Then
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

        Dim hpNearGuardrail As Boolean = cfg.LevelingStopHpEnabled AndAlso hpPct <= Math.Max(cfg.LevelingStopHpPercent + 5, 1)
        Dim mpNearGuardrail As Boolean = cfg.LevelingStopMpEnabled AndAlso mpPct <= Math.Max(cfg.LevelingStopMpPercent + 5, 1)
        If hpNearGuardrail OrElse mpNearGuardrail Then
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
        ClearPendingFarMapCoordinate()
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
        _navigationReturnToStartActive = False
        _navigationReturnTargetNodeId = ""
        _navigationReturnTargetNodeLabel = ""
        _navigationOutboundStartNodeId = ""
        _navigationOutboundStartNodeLabel = ""
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
        _lastTravelInputIsHoldCorrection = False
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

    Private Sub ClearHoldPlaceRuntime()
        _lastHoldPlaceMoveAt = DateTime.MinValue
        _lastHoldPlaceActive = False
        _lastHoldPlaceTargetX = -1
        _lastHoldPlaceTargetY = -1
        _lastHoldPlaceDistance = -1
        _lastHoldPlaceReason = ""
    End Sub

    Private Sub SetHoldPlaceRuntime(active As Boolean, targetX As Integer, targetY As Integer, distance As Double, reason As String)
        _lastHoldPlaceActive = active
        _lastHoldPlaceTargetX = targetX
        _lastHoldPlaceTargetY = targetY
        _lastHoldPlaceDistance = distance
        _lastHoldPlaceReason = If(reason, "").Trim()
    End Sub

    Private Sub AppendMapCoordinateDebug(now As DateTime, message As String)
        Dim line As String = $"{now:HH:mm:ss.fff} {If(message, "").Trim()}"
        If line.Trim() = "" Then
            Return
        End If

        _mapCoordinateDebugLines.Enqueue(line)
        While _mapCoordinateDebugLines.Count > MaxMapCoordinateDebugLines
            _mapCoordinateDebugLines.Dequeue()
        End While
        _lastMapCoordinateDebugLog = String.Join(Environment.NewLine, _mapCoordinateDebugLines)
    End Sub

    Private Sub ClearPendingFarMapCoordinate()
        _pendingFarMapCoordinateX = -1
        _pendingFarMapCoordinateY = -1
        _pendingFarMapCoordinateCount = 0
        _pendingFarMapCoordinateFirstAt = DateTime.MinValue
        _pendingFarMapCoordinateLastAt = DateTime.MinValue
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

    Private Sub ReadMapCoordinateIfNeeded(hwnd As IntPtr, frame As Bitmap, xRegion As RectRegion, yRegion As RectRegion, cfg As BotConfig, now As DateTime)
        Dim minIntervalMs As Integer = Math.Max(250, If(cfg Is Nothing, MapCoordinateOcrMinIntervalMs, cfg.MapCoordinateScanIntervalMs))
        If _lastMapCoordinateOcrAt <> DateTime.MinValue AndAlso (now - _lastMapCoordinateOcrAt).TotalMilliseconds < minIntervalMs Then
            Dim elapsedMs As Integer = CInt(Math.Max(0, (now - _lastMapCoordinateOcrAt).TotalMilliseconds))
            AppendMapCoordinateDebug(now, $"not checking: OCR throttle {elapsedMs}/{minIntervalMs}ms.")
            Return
        End If

        If xRegion Is Nothing OrElse yRegion Is Nothing Then
            AppendMapCoordinateDebug(now, "not checking: coordinate OCR region is missing.")
            Return
        End If

        Dim ocrFrame As Bitmap = frame
        Dim disposeOcrFrame As Boolean = False
        If ocrFrame Is Nothing Then
            Dim configuredFullFrameMs As Integer = Math.Max(100, If(cfg Is Nothing, FullFrameRefreshMs, cfg.FullFrameRefreshIntervalMs))
            Dim maxCachedAgeMs As Integer = Math.Min(2500, Math.Max(minIntervalMs, configuredFullFrameMs * 2))
            ocrFrame = GetLatestLoopFrameClone(maxCachedAgeMs)
            disposeOcrFrame = ocrFrame IsNot Nothing
            If ocrFrame IsNot Nothing Then
                AppendMapCoordinateDebug(now, $"checking: using latest Vision full-frame cache for coordinate OCR (age <= {maxCachedAgeMs}ms).")
            End If
        End If

        If ocrFrame Is Nothing Then
            AppendMapCoordinateDebug(now, "not checking: waiting for Vision full-frame capture before coordinate OCR.")
            Return
        End If

        Try
            _lastMapCoordinateOcrAt = now
            AppendMapCoordinateDebug(now, $"checking: OCR X={xRegion.X},{xRegion.Y},{xRegion.W},{xRegion.H} Y={yRegion.X},{yRegion.Y},{yRegion.W},{yRegion.H} frame={ocrFrame.Width}x{ocrFrame.Height}.")
            If hwnd <> IntPtr.Zero Then
                SaveMapCoordinateDiagnosticFrame(hwnd, xRegion, yRegion)
            End If

            Dim rawX As String = ""
            Dim rawY As String = ""
            Dim x As Integer = -1
            Dim y As Integer = -1
            Dim xConfidence As Integer = 0
            Dim yConfidence As Integer = 0
            Dim referenceX As Integer = -1
            Dim referenceY As Integer = -1
            TryGetMapCoordinateAcceptanceReference(cfg, referenceX, referenceY)
            Dim pairRaw As String = ""
            Dim pairConfidence As Integer = 0
            Dim pairOk As Boolean = If(hwnd <> IntPtr.Zero,
                                       TryReadMapCoordinatePairFromClient(hwnd, xRegion, yRegion, pairRaw, x, y, pairConfidence, referenceX, referenceY),
                                       TryReadMapCoordinatePair(ocrFrame, xRegion, yRegion, pairRaw, x, y, pairConfidence, referenceX, referenceY))
            AppendMapCoordinateDebug(now, $"pair OCR: {If(String.IsNullOrWhiteSpace(pairRaw), "<blank>", Regex.Replace(pairRaw, "\s+", " ").Trim())}; ok={pairOk}.")
            Dim xOk As Boolean = False
            Dim yOk As Boolean = False
            If pairOk Then
                rawX = pairRaw
                rawY = pairRaw
                xConfidence = pairConfidence
                yConfidence = pairConfidence
                xOk = True
                yOk = True
            Else
                rawX = pairRaw
                rawY = pairRaw
                xOk = If(hwnd <> IntPtr.Zero,
                         TryReadMapCoordinateAxisFromClient(hwnd, xRegion, rawX, x, xConfidence, referenceX, "x"),
                         TryReadMapCoordinateAxis(ocrFrame, xRegion, rawX, x, xConfidence, referenceX, "x"))
                yOk = If(hwnd <> IntPtr.Zero,
                         TryReadMapCoordinateAxisFromClient(hwnd, yRegion, rawY, y, yConfidence, referenceY, "y"),
                         TryReadMapCoordinateAxis(ocrFrame, yRegion, rawY, y, yConfidence, referenceY, "y"))
                AppendMapCoordinateDebug(now, $"axis OCR: X={If(String.IsNullOrWhiteSpace(rawX), "<blank>", Regex.Replace(rawX, "\s+", " ").Trim())} ok={xOk} val={If(xOk, x.ToString("000"), "n/a")} | Y={If(String.IsNullOrWhiteSpace(rawY), "<blank>", Regex.Replace(rawY, "\s+", " ").Trim())} ok={yOk} val={If(yOk, y.ToString("000"), "n/a")}.")
            End If

            If xOk AndAlso yOk Then
                Dim rejectionText As String = ""
                Dim acceptedByConfirmedJump As Boolean = False
                Dim coordinateConfidence As Integer = Math.Min(xConfidence, yConfidence)
                If Not IsMapCoordinateCandidateAccepted(x, y, cfg, now, coordinateConfidence, acceptedByConfirmedJump, rejectionText) Then
                    AppendMapCoordinateDebug(now, rejectionText)
                    _lastMapCoordinateText = rejectionText
                    If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 Then
                        _lastMapCoordinateConfidence = 0
                    End If
                    Return
                End If

                _lastMapCoordinateText = $"{x:000}/{y:000}"
                _lastMapCoordinateX = x
                _lastMapCoordinateY = y
                _lastMapCoordinateConfidence = coordinateConfidence
                If acceptedByConfirmedJump Then
                    AppendMapCoordinateDebug(now, $"accepted confirmed far jump: {_lastMapCoordinateText} confidence {_lastMapCoordinateConfidence}%.")
                Else
                    AppendMapCoordinateDebug(now, $"accepted: {_lastMapCoordinateText} confidence {_lastMapCoordinateConfidence}%.")
                End If
            Else
                _lastMapCoordinateText = FormatRawMapCoordinateText(rawX, rawY)
                AppendMapCoordinateDebug(now, $"not accepted: need exactly 3 digits for X and 3 digits for Y; raw={If(String.IsNullOrWhiteSpace(_lastMapCoordinateText), "<blank>", _lastMapCoordinateText)}.")
                AppendMapCoordinateDebug(now, $"diagnostic crops: {MapCoordinateOcrDiagnosticsDirectory}")
                If cfg Is Nothing OrElse Not cfg.HoldPlaceEnabled OrElse _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 Then
                    _lastMapCoordinateX = -1
                    _lastMapCoordinateY = -1
                    _lastMapCoordinateConfidence = 0
                End If
            End If
        Finally
            If disposeOcrFrame AndAlso ocrFrame IsNot Nothing Then
                ocrFrame.Dispose()
            End If
        End Try
    End Sub

    Private Function TryGetMapCoordinateAcceptanceReference(cfg As BotConfig, ByRef referenceX As Integer, ByRef referenceY As Integer) As Boolean
        referenceX = -1
        referenceY = -1
        If _lastMapCoordinateX >= 0 AndAlso _lastMapCoordinateY >= 0 Then
            referenceX = _lastMapCoordinateX
            referenceY = _lastMapCoordinateY
            Return True
        End If

        If cfg IsNot Nothing AndAlso cfg.HoldPlaceEnabled AndAlso cfg.HoldPlaceAnchorSet AndAlso cfg.HoldPlaceTargetX >= 0 AndAlso cfg.HoldPlaceTargetY >= 0 Then
            referenceX = cfg.HoldPlaceTargetX
            referenceY = cfg.HoldPlaceTargetY
            Return True
        End If

        Return False
    End Function

    Private Function IsMapCoordinateCandidateAccepted(x As Integer, y As Integer, cfg As BotConfig, now As DateTime, confidence As Integer, ByRef acceptedByConfirmedJump As Boolean, ByRef rejectionText As String) As Boolean
        rejectionText = ""
        acceptedByConfirmedJump = False
        If x < 0 OrElse x > 999 OrElse y < 0 OrElse y > 999 Then
            rejectionText = $"rejected map coordinate {x:000}/{y:000}: outside 000-999."
            Return False
        End If

        If cfg Is Nothing OrElse Not cfg.HoldPlaceEnabled Then
            ClearPendingFarMapCoordinate()
            Return True
        End If

        Dim referenceX As Integer = -1
        Dim referenceY As Integer = -1
        If Not TryGetMapCoordinateAcceptanceReference(cfg, referenceX, referenceY) Then
            ClearPendingFarMapCoordinate()
            Return True
        End If

        Dim distance As Double = CalculateDistance(x, y, referenceX, referenceY)
        If distance <= HoldPlaceMaxCoordinateAcceptanceDistance Then
            ClearPendingFarMapCoordinate()
            Return True
        End If

        If x = 0 AndAlso y = 0 Then
            rejectionText = $"rejected map coordinate 000/000: blank-looking coordinate read; keeping previous coordinate."
            Return False
        End If

        Dim confirmationCount As Integer = 0
        Dim confirmationReason As String = ""
        If IsConfirmedFarMapCoordinateJump(x, y, now, confidence, confirmationCount, confirmationReason) Then
            acceptedByConfirmedJump = True
            ClearPendingFarMapCoordinate()
            Return True
        End If

        rejectionText = $"rejected map coordinate {x:000}/{y:000}: {distance:0} units from {referenceX:000}/{referenceY:000} (> {HoldPlaceMaxCoordinateAcceptanceDistance:0}); {confirmationReason}"
        Return False
    End Function

    Private Function IsConfirmedFarMapCoordinateJump(x As Integer, y As Integer, now As DateTime, confidence As Integer, ByRef confirmationCount As Integer, ByRef confirmationReason As String) As Boolean
        confirmationCount = 0
        confirmationReason = ""

        If confidence < MapCoordinateFarJumpMinConfidence Then
            confirmationReason = $"far-jump confirmation skipped: confidence {confidence}% < {MapCoordinateFarJumpMinConfidence}%."
            Return False
        End If

        Dim resetCandidate As Boolean =
            _pendingFarMapCoordinateX < 0 OrElse
            _pendingFarMapCoordinateY < 0 OrElse
            _pendingFarMapCoordinateFirstAt = DateTime.MinValue OrElse
            (now - _pendingFarMapCoordinateFirstAt).TotalMilliseconds > MapCoordinateFarJumpConfirmWindowMs

        If Not resetCandidate Then
            Dim pendingDistance As Double = CalculateDistance(x, y, _pendingFarMapCoordinateX, _pendingFarMapCoordinateY)
            resetCandidate = pendingDistance > MapCoordinateFarJumpConfirmMaxDistance
        End If

        If resetCandidate Then
            _pendingFarMapCoordinateX = x
            _pendingFarMapCoordinateY = y
            _pendingFarMapCoordinateCount = 1
            _pendingFarMapCoordinateFirstAt = now
        Else
            _pendingFarMapCoordinateCount += 1
        End If

        _pendingFarMapCoordinateLastAt = now
        confirmationCount = _pendingFarMapCoordinateCount
        If confirmationCount >= MapCoordinateFarJumpConfirmRequiredCount Then
            confirmationReason = $"far-jump confirmation {confirmationCount}/{MapCoordinateFarJumpConfirmRequiredCount} within {MapCoordinateFarJumpConfirmMaxDistance:0} units."
            Return True
        End If

        confirmationReason = $"waiting for far-jump confirmation {confirmationCount}/{MapCoordinateFarJumpConfirmRequiredCount} within {MapCoordinateFarJumpConfirmWindowMs \ 1000}s."
        Return False
    End Function

    Private Shared Function TryReadMapCoordinatePairFromClient(hwnd As IntPtr, xRegion As RectRegion, yRegion As RectRegion, ByRef rawText As String, ByRef x As Integer, ByRef y As Integer, ByRef confidence As Integer, referenceX As Integer, referenceY As Integer) As Boolean
        rawText = ""
        x = -1
        y = -1
        confidence = 0
        If hwnd = IntPtr.Zero OrElse xRegion Is Nothing OrElse yRegion Is Nothing Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            Return False
        End If

        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
        Dim xRect As Rectangle = xRegion.Clamp(clientWidth, clientHeight)
        Dim yRect As Rectangle = yRegion.Clamp(clientWidth, clientHeight)
        If xRect.Width <= 0 OrElse xRect.Height <= 0 OrElse yRect.Width <= 0 OrElse yRect.Height <= 0 Then
            Return False
        End If

        Dim left As Integer = Math.Max(0, Math.Min(xRect.Left, yRect.Left) - 12)
        Dim top As Integer = Math.Max(0, Math.Min(xRect.Top, yRect.Top) - 8)
        Dim right As Integer = Math.Min(clientWidth, Math.Max(xRect.Right, yRect.Right) + 12)
        Dim bottom As Integer = Math.Min(clientHeight, Math.Max(xRect.Bottom, yRect.Bottom) + 8)
        Dim captureRegion As New RectRegion(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))

        Using crop As Bitmap = CaptureClientRegion(hwnd, captureRegion)
            If crop Is Nothing Then
                Return False
            End If

            SaveMapCoordinateDiagnosticCrop(crop, "pair")
            rawText = ReadMapCoordinateTextForOcr(crop, False)
            Dim normalized As String = ""
            If TryParseMapCoordinate(rawText, x, y, normalized, confidence, referenceX, referenceY) Then
                rawText = If(String.IsNullOrWhiteSpace(rawText), normalized, rawText)
                Return True
            End If
        End Using

        Return False
    End Function

    Private Shared Function TryReadMapCoordinateAxisFromClient(hwnd As IntPtr, region As RectRegion, ByRef rawText As String, ByRef value As Integer, ByRef confidence As Integer, referenceValue As Integer, diagnosticLabel As String) As Boolean
        rawText = ""
        value = -1
        confidence = 0
        If hwnd = IntPtr.Zero OrElse region Is Nothing Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            Return False
        End If

        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
        Dim baseRect As Rectangle = region.Clamp(clientWidth, clientHeight)
        Dim left As Integer = Math.Max(0, baseRect.Left - 8)
        Dim top As Integer = Math.Max(0, baseRect.Top - 6)
        Dim right As Integer = Math.Min(clientWidth, baseRect.Right + 8)
        Dim bottom As Integer = Math.Min(clientHeight, baseRect.Bottom + 6)
        Dim captureRegion As New RectRegion(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))

        Using crop As Bitmap = CaptureClientRegion(hwnd, captureRegion)
            If crop Is Nothing Then
                Return False
            End If

            Return TryReadMapCoordinateAxisCrop(crop, rawText, value, confidence, referenceValue, diagnosticLabel)
        End Using
    End Function

    Private Shared Function TryReadMapCoordinatePair(frame As Bitmap, xRegion As RectRegion, yRegion As RectRegion, ByRef rawText As String, ByRef x As Integer, ByRef y As Integer, ByRef confidence As Integer, referenceX As Integer, referenceY As Integer) As Boolean
        rawText = ""
        x = -1
        y = -1
        confidence = 0
        If frame Is Nothing OrElse xRegion Is Nothing OrElse yRegion Is Nothing Then
            Return False
        End If

        Dim xRect As Rectangle = xRegion.Clamp(frame.Width, frame.Height)
        Dim yRect As Rectangle = yRegion.Clamp(frame.Width, frame.Height)
        If xRect.Width <= 0 OrElse xRect.Height <= 0 OrElse yRect.Width <= 0 OrElse yRect.Height <= 0 Then
            Return False
        End If

        Dim left As Integer = Math.Max(0, Math.Min(xRect.Left, yRect.Left) - 12)
        Dim top As Integer = Math.Max(0, Math.Min(xRect.Top, yRect.Top) - 8)
        Dim right As Integer = Math.Min(frame.Width, Math.Max(xRect.Right, yRect.Right) + 12)
        Dim bottom As Integer = Math.Min(frame.Height, Math.Max(xRect.Bottom, yRect.Bottom) + 8)
        Dim rect As New Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            SaveMapCoordinateDiagnosticCrop(crop, "pair")
            rawText = ReadMapCoordinateTextForOcr(crop, False)
            Dim normalized As String = ""
            If TryParseMapCoordinate(rawText, x, y, normalized, confidence, referenceX, referenceY) Then
                rawText = If(String.IsNullOrWhiteSpace(rawText), normalized, rawText)
                Return True
            End If

            Return False
        End Using
    End Function

    Private Shared Function TryReadMapCoordinateAxis(frame As Bitmap, region As RectRegion, ByRef rawText As String, ByRef value As Integer, ByRef confidence As Integer, Optional referenceValue As Integer = -1, Optional diagnosticLabel As String = "axis") As Boolean
        rawText = ""
        value = -1
        confidence = 0
        If frame Is Nothing OrElse region Is Nothing Then
            Return False
        End If

        Dim baseRect As Rectangle = region.Clamp(frame.Width, frame.Height)
        Dim left As Integer = Math.Max(0, baseRect.Left - 8)
        Dim top As Integer = Math.Max(0, baseRect.Top - 6)
        Dim right As Integer = Math.Min(frame.Width, baseRect.Right + 8)
        Dim bottom As Integer = Math.Min(frame.Height, baseRect.Bottom + 6)
        Dim rect As New Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return False
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            Return TryReadMapCoordinateAxisCrop(crop, rawText, value, confidence, referenceValue, diagnosticLabel)
        End Using
    End Function

    Private Shared Function TryReadMapCoordinateAxisCrop(crop As Bitmap, ByRef rawText As String, ByRef value As Integer, ByRef confidence As Integer, referenceValue As Integer, diagnosticLabel As String) As Boolean
        rawText = ""
        value = -1
        confidence = 0
        If crop Is Nothing Then
            Return False
        End If

        SaveMapCoordinateDiagnosticCrop(crop, diagnosticLabel)
        rawText = ReadMapCoordinateTextForOcr(crop, True)
        If TryParseMapCoordinateAxis(rawText, value, confidence, referenceValue) Then
            Return True
        End If

        Dim integerValue As Long = OcrReader.ReadInteger(crop)
        If integerValue >= 0 AndAlso integerValue <= 999 Then
            Dim integerText As String = integerValue.ToString()
            If TryParseMapCoordinateAxis(integerText, value, confidence, referenceValue) Then
                rawText = If(String.IsNullOrWhiteSpace(rawText), integerText, $"{rawText} [{integerText}]")
                confidence = Math.Max(confidence, 70)
                Return True
            End If
        End If

        Dim pixelDigits As String = ""
        Dim pixelConfidence As Integer = 0
        If TryReadMapCoordinateDigitsByPixels(crop, 3, pixelDigits, pixelConfidence) AndAlso
           TryParseMapCoordinateAxis(pixelDigits, value, confidence, referenceValue) Then
            rawText = If(String.IsNullOrWhiteSpace(rawText), $"pixel:{pixelDigits}", $"{rawText} [pixel:{pixelDigits}]")
            confidence = Math.Max(35, Math.Min(68, pixelConfidence))
            Return True
        End If

        Return False
    End Function

    Private Shared Sub SaveMapCoordinateDiagnosticCrop(crop As Bitmap, label As String)
        If crop Is Nothing Then
            Return
        End If

        Try
            Directory.CreateDirectory(MapCoordinateOcrDiagnosticsDirectory)
            Dim safeLabel As String = Regex.Replace(If(label, "crop"), "[^A-Za-z0-9_-]+", "-")
            crop.Save(Path.Combine(MapCoordinateOcrDiagnosticsDirectory, $"latest-{safeLabel}.png"), ImageFormat.Png)
            Using enlarged As Bitmap = EnlargeBitmap(crop, 5)
                enlarged.Save(Path.Combine(MapCoordinateOcrDiagnosticsDirectory, $"latest-{safeLabel}-enlarged.png"), ImageFormat.Png)
                Using thresholded As Bitmap = ThresholdLumaBitmap(enlarged, 145)
                    thresholded.Save(Path.Combine(MapCoordinateOcrDiagnosticsDirectory, $"latest-{safeLabel}-threshold.png"), ImageFormat.Png)
                End Using
                Using inverted As Bitmap = ThresholdLumaBitmap(enlarged, 145, True)
                    inverted.Save(Path.Combine(MapCoordinateOcrDiagnosticsDirectory, $"latest-{safeLabel}-threshold-invert.png"), ImageFormat.Png)
                End Using
            End Using
        Catch
        End Try
    End Sub

    Private Shared Sub SaveMapCoordinateDiagnosticFrame(hwnd As IntPtr, xRegion As RectRegion, yRegion As RectRegion)
        If hwnd = IntPtr.Zero Then
            Return
        End If

        Try
            Dim rc As NativeMethods.RECT
            If Not NativeMethods.GetClientRect(hwnd, rc) Then
                Return
            End If

            Dim clientWidth As Integer = Math.Max(1, rc.Right - rc.Left)
            Dim clientHeight As Integer = Math.Max(1, rc.Bottom - rc.Top)
            Using frame As Bitmap = CaptureClientRegion(hwnd, New RectRegion(0, 0, clientWidth, clientHeight))
                If frame Is Nothing Then
                    Return
                End If

                Using marked As New Bitmap(frame.Width, frame.Height, PixelFormat.Format24bppRgb)
                    Using g As Graphics = Graphics.FromImage(marked)
                        g.DrawImageUnscaled(frame, 0, 0)
                        DrawDiagnosticRegion(g, xRegion, frame.Width, frame.Height, Color.Lime, "X")
                        DrawDiagnosticRegion(g, yRegion, frame.Width, frame.Height, Color.DeepSkyBlue, "Y")
                    End Using

                    Directory.CreateDirectory(MapCoordinateOcrDiagnosticsDirectory)
                    marked.Save(Path.Combine(MapCoordinateOcrDiagnosticsDirectory, "latest-client-marked.png"), ImageFormat.Png)
                End Using
            End Using
        Catch
        End Try
    End Sub

    Private Shared Sub DrawDiagnosticRegion(g As Graphics, region As RectRegion, width As Integer, height As Integer, color As Color, label As String)
        If g Is Nothing OrElse region Is Nothing Then
            Return
        End If

        Dim rect As Rectangle = region.Clamp(Math.Max(1, width), Math.Max(1, height))
        Using pen As New Pen(color, 2.0F)
            g.DrawRectangle(pen, rect)
        End Using
        Using back As New SolidBrush(Color.FromArgb(180, 0, 0, 0))
            Dim textRect As New Rectangle(rect.Left, Math.Max(0, rect.Top - 18), 70, 18)
            g.FillRectangle(back, textRect)
            TextRenderer.DrawText(g, label, SystemFonts.DefaultFont, textRect, color, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
        End Using
    End Sub

    Private Shared Function FormatRawMapCoordinateText(rawX As String, rawY As String) As String
        Dim xText As String = Regex.Replace(If(rawX, ""), "\s+", " ").Trim()
        Dim yText As String = Regex.Replace(If(rawY, ""), "\s+", " ").Trim()
        If xText = "" AndAlso yText = "" Then
            Return ""
        End If
        Return $"{If(xText = "", "?", xText)}/{If(yText = "", "?", yText)}"
    End Function

    Private Shared Function ReadMapCoordinateTextForOcr(crop As Bitmap, Optional axisOnly As Boolean = False) As String
        If crop Is Nothing Then
            Return ""
        End If

        Dim preferredPattern As String = If(axisOnly, "(?<!\d)\d{3}(?!\d)", "\d{3}\s*[/,]\s*\d{3}")
        Dim acceptablePattern As String = If(axisOnly, "(?<!\d)\d{1,3}(?!\d)", "\d{1,3}\D+\d{1,3}")
        Dim acceptableCandidate As String = ""
        Dim firstCandidate As String = ""
        Dim selectedCandidate As String = ""

        Using enlarged As Bitmap = EnlargeBitmap(crop, If(axisOnly, 5, 4))
            If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenText(enlarged), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                Return selectedCandidate
            End If
            If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenTextIsolated(enlarged), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                Return selectedCandidate
            End If

            For Each threshold As Integer In New Integer() {105, 125, 145, 165}
                Using thresholded As Bitmap = ThresholdLumaBitmap(enlarged, threshold)
                    If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenText(thresholded), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                        Return selectedCandidate
                    End If
                    If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenTextIsolated(thresholded), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                        Return selectedCandidate
                    End If
                End Using
            Next

            For Each threshold As Integer In New Integer() {125, 165}
                Using thresholded As Bitmap = ThresholdLumaBitmap(enlarged, threshold, True)
                    If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenText(thresholded), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                        Return selectedCandidate
                    End If
                    If TrySelectMapCoordinateOcrCandidate(OcrReader.ReadScreenTextIsolated(thresholded), preferredPattern, acceptablePattern, acceptableCandidate, firstCandidate, selectedCandidate) Then
                        Return selectedCandidate
                    End If
                End Using
            Next
        End Using

        Return If(acceptableCandidate <> "", acceptableCandidate, firstCandidate)
    End Function

    Private Shared Function TrySelectMapCoordinateOcrCandidate(candidate As String, preferredPattern As String, acceptablePattern As String, ByRef acceptableCandidate As String, ByRef firstCandidate As String, ByRef selectedCandidate As String) As Boolean
        Dim cleaned As String = If(candidate, "").Trim()
        If cleaned = "" Then
            Return False
        End If

        If firstCandidate = "" Then
            firstCandidate = cleaned
        End If

        If Regex.IsMatch(cleaned, preferredPattern) Then
            selectedCandidate = cleaned
            Return True
        End If

        If acceptableCandidate = "" AndAlso Regex.IsMatch(cleaned, acceptablePattern) Then
            acceptableCandidate = cleaned
        End If

        Return False
    End Function

    Private Shared Function TryReadMapCoordinateDigitsByPixels(crop As Bitmap, expectedDigits As Integer, ByRef digits As String, ByRef confidence As Integer) As Boolean
        digits = ""
        confidence = 0
        If crop Is Nothing OrElse expectedDigits <= 0 Then
            Return False
        End If

        Dim bestDigits As String = ""
        Dim bestConfidence As Integer = 0
        Using enlarged As Bitmap = EnlargeBitmap(crop, 4)
            For Each threshold As Integer In New Integer() {85, 105, 125, 145, 165, 185, 205}
                For Each invert As Boolean In New Boolean() {False, True}
                    Dim mask(,) As Boolean = BuildDigitMask(enlarged, threshold, invert)
                    Dim candidate As String = ""
                    Dim candidateConfidence As Integer = 0
                    If TryReadDigitMask(mask, enlarged.Width, enlarged.Height, expectedDigits, candidate, candidateConfidence) Then
                        If candidateConfidence > bestConfidence Then
                            bestDigits = candidate
                            bestConfidence = candidateConfidence
                        End If
                    End If
                Next
            Next
        End Using

        If bestDigits.Length <> expectedDigits OrElse bestConfidence < 55 Then
            Return False
        End If

        digits = bestDigits
        confidence = bestConfidence
        Return True
    End Function

    Private Shared Function BuildDigitMask(source As Bitmap, threshold As Integer, invert As Boolean) As Boolean(,)
        Dim mask(source.Width - 1, source.Height - 1) As Boolean
        Using buffer As New BitmapReadBuffer(source)
            For y As Integer = 0 To source.Height - 1
                For x As Integer = 0 To source.Width - 1
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    Dim luma As Integer = (r * 30 + g * 59 + b * 11) \ 100
                    Dim foreground As Boolean = luma >= threshold
                    If invert Then
                        foreground = Not foreground
                    End If
                    mask(x, y) = foreground
                Next
            Next
        End Using
        Return mask
    End Function

    Private Shared Function TryReadDigitMask(mask(,) As Boolean, width As Integer, height As Integer, expectedDigits As Integer, ByRef digits As String, ByRef confidence As Integer) As Boolean
        digits = ""
        confidence = 0
        Dim bounds As Rectangle = GetMaskBounds(mask, width, height)
        If bounds = Rectangle.Empty OrElse bounds.Width < expectedDigits * 2 OrElse bounds.Height < 8 Then
            Return False
        End If

        Dim runs As List(Of Rectangle) = SegmentDigitRuns(mask, bounds)
        NormalizeDigitRunCount(runs, expectedDigits)
        If runs.Count <> expectedDigits Then
            Return False
        End If

        Dim parts As New List(Of Char)()
        Dim minConfidence As Integer = 100
        For Each run As Rectangle In runs
            Dim digit As Char = "0"c
            Dim digitConfidence As Integer = 0
            If Not RecognizePixelDigit(mask, run, digit, digitConfidence) Then
                Return False
            End If
            parts.Add(digit)
            minConfidence = Math.Min(minConfidence, digitConfidence)
        Next

        digits = New String(parts.ToArray())
        confidence = minConfidence
        Return confidence >= 52
    End Function

    Private Shared Function GetMaskBounds(mask(,) As Boolean, width As Integer, height As Integer) As Rectangle
        Dim left As Integer = width
        Dim top As Integer = height
        Dim right As Integer = -1
        Dim bottom As Integer = -1
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                If Not mask(x, y) Then
                    Continue For
                End If
                left = Math.Min(left, x)
                top = Math.Min(top, y)
                right = Math.Max(right, x)
                bottom = Math.Max(bottom, y)
            Next
        Next

        If right < left OrElse bottom < top Then
            Return Rectangle.Empty
        End If
        Return New Rectangle(left, top, right - left + 1, bottom - top + 1)
    End Function

    Private Shared Function SegmentDigitRuns(mask(,) As Boolean, bounds As Rectangle) As List(Of Rectangle)
        Dim runs As New List(Of Rectangle)()
        Dim minColumnPixels As Integer = Math.Max(1, bounds.Height \ 18)
        Dim gapTolerance As Integer = Math.Max(1, bounds.Width \ 80)
        Dim runStart As Integer = -1
        Dim runEnd As Integer = -1
        Dim gap As Integer = 0

        For x As Integer = bounds.Left To bounds.Right - 1
            Dim count As Integer = 0
            For y As Integer = bounds.Top To bounds.Bottom - 1
                If mask(x, y) Then
                    count += 1
                End If
            Next

            If count >= minColumnPixels Then
                If runStart < 0 Then
                    runStart = x
                End If
                runEnd = x
                gap = 0
            ElseIf runStart >= 0 Then
                gap += 1
                If gap > gapTolerance Then
                    AddDigitRun(mask, bounds, runStart, runEnd, runs)
                    runStart = -1
                    runEnd = -1
                    gap = 0
                End If
            End If
        Next

        If runStart >= 0 Then
            AddDigitRun(mask, bounds, runStart, runEnd, runs)
        End If

        Return runs.OrderBy(Function(r) r.Left).ToList()
    End Function

    Private Shared Sub AddDigitRun(mask(,) As Boolean, bounds As Rectangle, left As Integer, right As Integer, runs As List(Of Rectangle))
        If right < left Then
            Return
        End If

        Dim top As Integer = bounds.Bottom
        Dim bottom As Integer = bounds.Top - 1
        Dim area As Integer = 0
        For x As Integer = left To right
            For y As Integer = bounds.Top To bounds.Bottom - 1
                If mask(x, y) Then
                    area += 1
                    top = Math.Min(top, y)
                    bottom = Math.Max(bottom, y)
                End If
            Next
        Next

        If area < Math.Max(8, bounds.Height) OrElse bottom < top Then
            Return
        End If
        runs.Add(New Rectangle(left, top, right - left + 1, bottom - top + 1))
    End Sub

    Private Shared Sub NormalizeDigitRunCount(runs As List(Of Rectangle), expectedDigits As Integer)
        If runs Is Nothing Then
            Return
        End If

        While runs.Count > expectedDigits
            Dim mergeIndex As Integer = -1
            Dim bestGap As Integer = Integer.MaxValue
            For i As Integer = 0 To runs.Count - 2
                Dim gap As Integer = runs(i + 1).Left - runs(i).Right
                If gap < bestGap Then
                    bestGap = gap
                    mergeIndex = i
                End If
            Next
            If mergeIndex < 0 Then
                Exit While
            End If
            runs(mergeIndex) = Rectangle.Union(runs(mergeIndex), runs(mergeIndex + 1))
            runs.RemoveAt(mergeIndex + 1)
        End While

        While runs.Count < expectedDigits
            Dim widestIndex As Integer = -1
            Dim widest As Integer = 0
            For i As Integer = 0 To runs.Count - 1
                If runs(i).Width > widest Then
                    widest = runs(i).Width
                    widestIndex = i
                End If
            Next
            If widestIndex < 0 OrElse widest < 10 Then
                Exit While
            End If

            Dim source As Rectangle = runs(widestIndex)
            Dim leftWidth As Integer = Math.Max(1, source.Width \ 2)
            Dim rightWidth As Integer = Math.Max(1, source.Width - leftWidth)
            runs(widestIndex) = New Rectangle(source.Left, source.Top, leftWidth, source.Height)
            runs.Insert(widestIndex + 1, New Rectangle(source.Left + leftWidth, source.Top, rightWidth, source.Height))
        End While

        runs.Sort(Function(a, b) a.Left.CompareTo(b.Left))
    End Sub

    Private Shared Function RecognizePixelDigit(mask(,) As Boolean, rect As Rectangle, ByRef digit As Char, ByRef confidence As Integer) As Boolean
        digit = "0"c
        confidence = 0
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return False
        End If

        Dim cells(4, 6) As Boolean
        For gy As Integer = 0 To 6
            For gx As Integer = 0 To 4
                Dim x0 As Integer = rect.Left + CInt(Math.Floor(gx * rect.Width / 5.0R))
                Dim x1 As Integer = rect.Left + CInt(Math.Floor((gx + 1) * rect.Width / 5.0R)) - 1
                Dim y0 As Integer = rect.Top + CInt(Math.Floor(gy * rect.Height / 7.0R))
                Dim y1 As Integer = rect.Top + CInt(Math.Floor((gy + 1) * rect.Height / 7.0R)) - 1
                x1 = Math.Max(x0, x1)
                y1 = Math.Max(y0, y1)

                Dim total As Integer = 0
                Dim filled As Integer = 0
                For y As Integer = y0 To y1
                    For x As Integer = x0 To x1
                        total += 1
                        If mask(x, y) Then
                            filled += 1
                        End If
                    Next
                Next
                cells(gx, gy) = total > 0 AndAlso filled / CDbl(total) >= 0.18R
            Next
        Next

        Dim bestDigit As Char = "0"c
        Dim bestScore As Double = -1.0R
        For Each candidate As Char In "0123456789"
            Dim template As String() = GetDigitTemplate(candidate)
            Dim matchedWeight As Double = 0.0R
            Dim totalWeight As Double = 0.0R
            For gy As Integer = 0 To 6
                For gx As Integer = 0 To 4
                    Dim expected As Boolean = template(gy).Chars(gx) = "1"c
                    Dim actual As Boolean = cells(gx, gy)
                    Dim weight As Double = If(expected, 1.25R, 0.75R)
                    totalWeight += weight
                    If expected = actual Then
                        matchedWeight += weight
                    End If
                Next
            Next

            Dim score As Double = If(totalWeight <= 0, 0.0R, matchedWeight / totalWeight)
            If score > bestScore Then
                bestScore = score
                bestDigit = candidate
            End If
        Next

        digit = bestDigit
        confidence = CInt(Math.Round(bestScore * 100.0R))
        Return confidence >= 52
    End Function

    Private Shared Function GetDigitTemplate(digit As Char) As String()
        Select Case digit
            Case "0"c
                Return New String() {"01110", "10001", "10011", "10101", "11001", "10001", "01110"}
            Case "1"c
                Return New String() {"00100", "01100", "00100", "00100", "00100", "00100", "01110"}
            Case "2"c
                Return New String() {"01110", "10001", "00001", "00010", "00100", "01000", "11111"}
            Case "3"c
                Return New String() {"11110", "00001", "00001", "01110", "00001", "00001", "11110"}
            Case "4"c
                Return New String() {"00010", "00110", "01010", "10010", "11111", "00010", "00010"}
            Case "5"c
                Return New String() {"11111", "10000", "10000", "11110", "00001", "00001", "11110"}
            Case "6"c
                Return New String() {"01110", "10000", "10000", "11110", "10001", "10001", "01110"}
            Case "7"c
                Return New String() {"11111", "00001", "00010", "00100", "01000", "01000", "01000"}
            Case "8"c
                Return New String() {"01110", "10001", "10001", "01110", "10001", "10001", "01110"}
            Case "9"c
                Return New String() {"01110", "10001", "10001", "01111", "00001", "00001", "01110"}
            Case Else
                Return New String() {"00000", "00000", "00000", "00000", "00000", "00000", "00000"}
        End Select
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

            If cfg IsNot Nothing AndAlso cfg.PixelChangeGateEnabled Then
                Dim signature As ULong = ComputeVisualSignature(crop)
                If signature <> 0UL AndAlso signature = _lastChatVisualSignature Then
                    Return
                End If
                _lastChatVisualSignature = signature
            End If

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
        _lastChatVisualSignature = 0UL
    End Sub

    Private Sub ReadPartyListIfNeeded(frame As Bitmap, region As RectRegion, cfg As BotConfig, now As DateTime)
        Dim minIntervalMs As Integer = Math.Max(250, If(cfg Is Nothing, PartyListScanMinIntervalMs, cfg.PartyListScanIntervalMs))
        If _lastPartyListScanAt <> DateTime.MinValue AndAlso (now - _lastPartyListScanAt).TotalMilliseconds < minIntervalMs Then
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

            If cfg IsNot Nothing AndAlso cfg.PixelChangeGateEnabled Then
                Dim signature As ULong = ComputeVisualSignature(crop)
                If signature <> 0UL AndAlso signature = _lastPartyListVisualSignature Then
                    Return
                End If
                _lastPartyListVisualSignature = signature
            End If

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
        _lastPartyListVisualSignature = 0UL
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

        Using buffer As New BitmapReadBuffer(crop)
            For y As Integer = 0 To crop.Height - 1
                Dim redCount As Integer = 0
                Dim blueCount As Integer = 0
                For x As Integer = 0 To crop.Width - 1
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    If IsPartyHpBarPixelRgb(r, g, b) Then
                        redCount += 1
                    ElseIf IsPartyMpBarPixelRgb(r, g, b) Then
                        blueCount += 1
                    End If
                Next

                rowRed(y) = redCount
                rowBlue(y) = blueCount
            Next
        End Using

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

    Private Shared Function IsPartyHpBarPixelRgb(r As Integer, g As Integer, b As Integer) As Boolean
        Return r >= 90 AndAlso r >= (g + 16) AndAlso r >= (b + 16)
    End Function

    Private Shared Function IsPartyMpBarPixel(px As Color) As Boolean
        Return px.B >= 90 AndAlso px.B >= (px.R + 12) AndAlso px.B >= (px.G + 8)
    End Function

    Private Shared Function IsPartyMpBarPixelRgb(r As Integer, g As Integer, b As Integer) As Boolean
        Return b >= 90 AndAlso b >= (r + 12) AndAlso b >= (g + 8)
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

        If _lastMapCoordinateConfidence < 10 Then
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
        Dim holdDirectionLearning As Boolean =
            _lastTravelInputIsHoldCorrection AndAlso
            _config IsNot Nothing AndAlso
            _config.HoldPlaceDirectionLearningEnabled
        Dim requiredConfirmations As Integer = If(holdDirectionLearning, 1, NavigationRotationConfirmationsRequired)
        If defaultIndex >= 0 AndAlso actualIndex >= 0 Then
            Dim observedRotation As Integer = (actualIndex - defaultIndex + 4) Mod 4
            If observedRotation = _navigationRotationQuarterTurns Then
                _navigationRotationCandidateQuarterTurns = -1
                _navigationRotationCandidateCount = 0
            ElseIf (Not holdDirectionLearning) AndAlso _lastNavigationRotationChangeAt <> DateTime.MinValue AndAlso (now - _lastNavigationRotationChangeAt).TotalMilliseconds < NavigationRotationChangeCooldownMs Then
                ' Hold the current mapping briefly so a single noisy sample does not jerk travel.
            Else
                If _navigationRotationCandidateQuarterTurns <> observedRotation Then
                    _navigationRotationCandidateQuarterTurns = observedRotation
                    _navigationRotationCandidateCount = 1
                Else
                    _navigationRotationCandidateCount += 1
                End If

                If _navigationRotationCandidateCount >= requiredConfirmations Then
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
        _lastTravelInputIsHoldCorrection = False
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
        Dim requiredConfidence As Integer = Math.Max(0, Math.Min(100, If(cfg Is Nothing, 90, cfg.RouteRecordingMinConfidencePercent)))
        If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 OrElse _lastMapLocalizationConfidence < requiredConfidence Then
            If _routeRecordingCaptureActive Then
                Dim rawText As String = If(String.IsNullOrWhiteSpace(_lastMapCoordinateText), "no OCR text", _lastMapCoordinateText)
                _routeRecordingStatus = $"Recording route '{_routeRecordingName}': waiting for X/Y confidence >= {requiredConfidence}% ({rawText}, confidence {_lastMapLocalizationConfidence}%)."
            End If
            Return
        End If
        Dim effectiveSampleIntervalMs As Integer = Math.Max(10, If(cfg IsNot Nothing AndAlso cfg.RouteRecordingSampleIntervalMs > 0, cfg.RouteRecordingSampleIntervalMs, RouteRecordingMinSampleIntervalMs))
        If _routeRecordingLastSampleAt <> DateTime.MinValue AndAlso (now - _routeRecordingLastSampleAt).TotalMilliseconds < effectiveSampleIntervalMs Then
            Return
        End If

        Dim minDistance As Double = Math.Max(0.5, cfg.RouteRecordingMinSampleDistance / 4.0)
        If _routeRecordingSamples.Count > 0 Then
            Dim lastSample As NavigationRouteSample = _routeRecordingSamples(_routeRecordingSamples.Count - 1)
            Dim dist As Double = CalculateDistance(lastSample.X, lastSample.Y, _lastMapCoordinateX, _lastMapCoordinateY)
            If dist < minDistance Then
                Return
            End If
            ' OCR coordinate validation: reject jumps > 20 units as likely misreads
            If dist > 20 Then
                RaiseEvent LogLine($"Route recording: rejected coordinate ({_lastMapCoordinateX},{_lastMapCoordinateY}) – jump of {dist:0.0} units from ({lastSample.X},{lastSample.Y}) exceeds 20-unit limit.")
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
            minNodeSpacing = Math.Max(1, If(effectiveCfg Is Nothing, 2, effectiveCfg.RouteRecordingMinNodeSpacing))
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

    Public Function SaveRecordedRouteSamples(cfg As BotConfig, samples As List(Of NavigationRouteSample)) As String
        Dim effectiveCfg As BotConfig = If(cfg, _config)
        Dim routeName As String = NormalizeRecordedRouteName(If(effectiveCfg Is Nothing, "", effectiveCfg.RouteRecordingName))
        Dim mapName As String = NormalizeNavigationMapName(If(effectiveCfg Is Nothing, "", effectiveCfg.NavigationMapName))
        Dim minNodeSpacing As Integer = Math.Max(1, If(effectiveCfg Is Nothing, 2, effectiveCfg.RouteRecordingMinNodeSpacing))
        Dim cleanSamples As List(Of NavigationRouteSample) = If(samples, New List(Of NavigationRouteSample)()).
            Where(Function(sample) sample IsNot Nothing AndAlso sample.X >= 0 AndAlso sample.X <= 999 AndAlso sample.Y >= 0 AndAlso sample.Y <= 999).
            Select(Function(sample) New NavigationRouteSample With {
                .X = sample.X,
                .Y = sample.Y,
                .CapturedAtUtc = If(sample.CapturedAtUtc = DateTime.MinValue, DateTime.UtcNow, sample.CapturedAtUtc)
            }).
            ToList()

        If cleanSamples.Count < 2 Then
            SyncLock _sync
                _routeRecordingStatus = $"Not enough manual breadcrumb rows to save route '{routeName}'. Add at least two valid X/Y rows."
            End SyncLock
            Return ""
        End If

        Dim graph As RecordedNavigationGraph = BuildRecordedNavigationGraph(mapName, routeName, cleanSamples, minNodeSpacing, True)
        If graph Is Nothing OrElse graph.Nodes.Count < 2 OrElse graph.Edges.Count = 0 Then
            SyncLock _sync
                _routeRecordingStatus = $"Unable to build a reusable route graph from manual breadcrumbs for '{routeName}'."
            End SyncLock
            Return ""
        End If

        Dim savedPath As String = SaveRecordedNavigationGraph(graph)
        If savedPath <> "" Then
            SyncLock _sync
                _routeRecordingLastSavedPath = savedPath
                _routeRecordingStatus = $"Saved route '{routeName}' with {graph.Nodes.Count} manual/table nodes."
                _routeRecordingSamples.Clear()
                _routeRecordingSamples.AddRange(cleanSamples.Select(Function(sample) New NavigationRouteSample With {
                    .X = sample.X,
                    .Y = sample.Y,
                    .CapturedAtUtc = sample.CapturedAtUtc
                }))
            End SyncLock
        End If

        Return savedPath
    End Function

    Private Shared Function BuildRecordedNavigationGraph(mapName As String, routeName As String, samples As List(Of NavigationRouteSample), minNodeSpacing As Integer, Optional preserveAllSamples As Boolean = False) As RecordedNavigationGraph
        If samples Is Nothing OrElse samples.Count < 2 Then
            Return Nothing
        End If

        Dim simplified As List(Of NavigationRouteSample) = If(preserveAllSamples,
            samples.Select(Function(sample) New NavigationRouteSample With {
                .X = sample.X,
                .Y = sample.Y,
                .CapturedAtUtc = sample.CapturedAtUtc
            }).ToList(),
            SimplifyRecordedRouteSamples(samples, minNodeSpacing))
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

        Dim effectiveTargetNodeId As String = If(_navigationReturnToStartActive AndAlso Not String.IsNullOrWhiteSpace(_navigationReturnTargetNodeId),
                                                _navigationReturnTargetNodeId,
                                                cfg.NavigationTargetNodeId)
        plan.TargetNode = FindNodeById(nodes, effectiveTargetNodeId)
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
        Dim repathReason As String = ""
        If ForceNavigationRepath(cfg, now, repathReason) AndAlso Not String.IsNullOrWhiteSpace(repathReason) Then
            RaiseEvent LogLine(repathReason)
        End If

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
        Dim baseReason As String = If(plan IsNot Nothing AndAlso plan.NextWaypoint IsNot Nothing,
                                      $"Travel stalled near {plan.NextWaypoint.Label}. Running recovery #{_lastNavigationRecoveryCount}.",
                                      $"Travel stalled. Running recovery #{_lastNavigationRecoveryCount}.")
        _lastNavigationTravelReason = If(String.IsNullOrWhiteSpace(repathReason), baseReason, repathReason & " " & baseReason)
        reason = _lastNavigationTravelReason
        Return True
    End Function

    Private Function ForceNavigationRepath(cfg As BotConfig, now As DateTime, ByRef reason As String) As Boolean
        reason = ""
        If cfg Is Nothing OrElse Not cfg.NavigationRepathOnStuck Then
            Return False
        End If

        _navigationCommittedWaypointId = ""
        _navigationCommittedWaypointLabel = ""
        _lastNavigationProgressWaypointId = ""
        _lastNavigationProgressDistance = -1
        _lastNavigationProgressAt = now

        Dim repathPlan As NavigationPlan = BuildNavigationPlan(cfg, now, allowStaleLocalization:=True)
        If repathPlan Is Nothing OrElse Not repathPlan.RouteReady OrElse repathPlan.NextWaypoint Is Nothing Then
            reason = "Navigation repath requested after stall, but no alternate route is ready yet."
            Return True
        End If

        _navigationCommittedWaypointId = repathPlan.NextWaypoint.Id
        _navigationCommittedWaypointLabel = repathPlan.NextWaypoint.Label
        Dim startLabel As String = If(repathPlan.StartNode Is Nothing, "current node", repathPlan.StartNode.Label)
        Dim targetLabel As String = If(repathPlan.TargetNode Is Nothing, "target", repathPlan.TargetNode.Label)
        reason = $"Navigation repath after stall: {startLabel} -> {targetLabel}; next {repathPlan.NextWaypoint.Label}."
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
            _lastTravelInputIsHoldCorrection = False
            reason = $"Moving toward {plan.NextWaypoint.Label}: want {primaryDirection}, using {primaryKey}."
            Return True
        End If

        Return False
    End Function

    Private Function TryHandleHoldPlace(cfg As BotConfig, hwnd As IntPtr, now As DateTime, combatActive As Boolean, ByRef reason As String, ByRef blocksRetarget As Boolean) As Boolean
        reason = ""
        blocksRetarget = False
        If cfg Is Nothing OrElse Not cfg.HoldPlaceEnabled OrElse hwnd = IntPtr.Zero Then
            ClearHoldPlaceRuntime()
            Return False
        End If

        Dim targetX As Integer = cfg.HoldPlaceTargetX
        Dim targetY As Integer = cfg.HoldPlaceTargetY
        If Not cfg.HoldPlaceAnchorSet Then
            reason = "Hold on place: set an anchor X/Y coordinate."
            SetHoldPlaceRuntime(False, -1, -1, -1, reason)
            Return False
        End If

        If targetX < 0 OrElse targetY < 0 OrElse targetX > 999 OrElse targetY > 999 Then
            reason = "Hold on place: set an anchor X/Y coordinate."
            SetHoldPlaceRuntime(False, targetX, targetY, -1, reason)
            Return False
        End If

        If _lastMapCoordinateX < 0 OrElse _lastMapCoordinateY < 0 OrElse _lastMapLocalizationConfidence < 30 Then
            reason = $"Hold on place: waiting for map coordinates near anchor {targetX:000}/{targetY:000}."
            SetHoldPlaceRuntime(False, targetX, targetY, -1, reason)
            Return False
        End If

        Dim dx As Integer = targetX - _lastMapCoordinateX
        Dim dy As Integer = targetY - _lastMapCoordinateY
        Dim distance As Double = CalculateDistance(_lastMapCoordinateX, _lastMapCoordinateY, targetX, targetY)
        Dim radius As Integer = Math.Max(0, cfg.HoldPlaceRadius)
        If Math.Abs(dx) <= radius AndAlso Math.Abs(dy) <= radius Then
            reason = $"Hold on place: anchored at {targetX:000}/{targetY:000}; current {_lastMapCoordinateX:000}/{_lastMapCoordinateY:000}."
            SetHoldPlaceRuntime(False, targetX, targetY, distance, reason)
            Return False
        End If

        Dim configuredLeash As Integer = Math.Max(0, cfg.HoldPlaceEmergencyLeashDistance)
        Dim emergencyLeash As Integer = Math.Max(radius + 1, configuredLeash)
        Dim emergencyCorrection As Boolean = configuredLeash > 0 AndAlso distance >= emergencyLeash
        Dim postFightReturn As Boolean = cfg.HoldPlacePostFightReturnEnabled AndAlso Not combatActive
        blocksRetarget = postFightReturn

        If combatActive AndAlso cfg.HoldPlaceCombatSafeEnabled AndAlso Not emergencyCorrection Then
            reason = $"Hold on place: combat active; normal correction waits until target clears or distance reaches leash {emergencyLeash}."
            SetHoldPlaceRuntime(False, targetX, targetY, distance, reason)
            Return False
        End If

        Dim correctionIntervalMs As Integer = Math.Max(150, cfg.HoldPlaceCorrectionIntervalMs)
        If _lastHoldPlaceMoveAt <> DateTime.MinValue AndAlso (now - _lastHoldPlaceMoveAt).TotalMilliseconds < correctionIntervalMs Then
            Dim correctionMode As String = If(emergencyCorrection, "emergency leash", If(postFightReturn, "post-fight return", "correction"))
            reason = $"Hold on place: {correctionMode} waiting for correction interval; current {_lastMapCoordinateX:000}/{_lastMapCoordinateY:000}, distance {distance:0.0}."
            SetHoldPlaceRuntime(True, targetX, targetY, distance, reason)
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

        Dim baseBurstMs As Integer = Math.Max(20, Math.Min(800, cfg.HoldPlaceMoveBurstMs))
        Dim primaryBurstMs As Integer = GetPreciseTravelBurstMs(baseBurstMs, primaryDistance)
        If SendKey(hwnd, primaryKey, primaryBurstMs) Then
            MarkKeyUsed(primaryKey)
            Dim correctionMode As String = If(emergencyCorrection, "emergency leash", If(postFightReturn, "post-fight return", If(combatActive, "combat correction", "hold correction")))
            SetLastAction($"{primaryKey} (hold {correctionMode})")
            _lastHoldPlaceMoveAt = now
            If cfg.HoldPlaceDirectionLearningEnabled Then
                _lastTravelInputKey = primaryKey
                _lastTravelInputDesiredDirection = primaryDirection
                _lastTravelInputPoseX = _lastMapCoordinateX
                _lastTravelInputPoseY = _lastMapCoordinateY
                _lastTravelInputAt = now
                _lastTravelInputIsHoldCorrection = True
            Else
                ClearPendingNavigationTravelInput()
            End If
            reason = $"Hold on place: {correctionMode} to {targetX:000}/{targetY:000}; current {_lastMapCoordinateX:000}/{_lastMapCoordinateY:000}, distance {distance:0.0}, using {primaryKey}."
            SetHoldPlaceRuntime(True, targetX, targetY, distance, reason)
            Return True
        End If

        reason = $"Hold on place: failed to send movement toward {targetX:000}/{targetY:000}."
        SetHoldPlaceRuntime(False, targetX, targetY, distance, reason)
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

        If Not _navigationReturnToStartActive AndAlso String.IsNullOrWhiteSpace(_navigationOutboundStartNodeId) AndAlso plan.StartNode IsNot Nothing AndAlso plan.TargetNode IsNot Nothing AndAlso Not plan.StartNode.Id.Equals(plan.TargetNode.Id, StringComparison.OrdinalIgnoreCase) Then
            _navigationOutboundStartNodeId = plan.StartNode.Id
            _navigationOutboundStartNodeLabel = plan.StartNode.Label
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
            If cfg.NavigationReturnToStartEnabled AndAlso Not _navigationReturnToStartActive AndAlso Not String.IsNullOrWhiteSpace(_navigationOutboundStartNodeId) AndAlso Not _navigationOutboundStartNodeId.Equals(plan.TargetNode.Id, StringComparison.OrdinalIgnoreCase) Then
                _navigationReturnToStartActive = True
                _navigationReturnTargetNodeId = _navigationOutboundStartNodeId
                _navigationReturnTargetNodeLabel = If(String.IsNullOrWhiteSpace(_navigationOutboundStartNodeLabel), "route start", _navigationOutboundStartNodeLabel)
                _navigationCommittedWaypointId = ""
                _navigationCommittedWaypointLabel = ""
                _lastNavigationProgressWaypointId = ""
                _lastNavigationProgressDistance = -1
                _lastNavigationProgressAt = now
                _lastNavigationDestinationReached = False
                _lastNavigationDestinationLabel = ""
                _lastNavigationTravelReason = $"Destination reached: {plan.TargetNode.Label}. Returning to start: {_navigationReturnTargetNodeLabel}."
                _lastNavigationTravelActive = True
                reason = _lastNavigationTravelReason
                RaiseEvent LogLine(_lastNavigationTravelReason)
                Return False
            End If

            _lastNavigationDestinationReached = True
            _lastNavigationDestinationLabel = If(_navigationReturnToStartActive,
                                                 $"Returned to start: {plan.TargetNode.Label}",
                                                 plan.TargetNode.Label)
            _navigationCommittedWaypointId = ""
            _navigationCommittedWaypointLabel = ""
            If _navigationReturnToStartActive Then
                _lastNavigationTravelReason = $"Returned to route start with exact coordinate match: {plan.TargetNode.Label}."
                _navigationReturnToStartActive = False
                _navigationReturnTargetNodeId = ""
                _navigationReturnTargetNodeLabel = ""
                _navigationOutboundStartNodeId = ""
                _navigationOutboundStartNodeLabel = ""
            Else
                _lastNavigationTravelReason = $"Destination reached with exact coordinate match: {plan.TargetNode.Label}."
            End If
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

    Private Function TryScanForMobDuringTravel(cfg As BotConfig, hwnd As IntPtr, now As DateTime, ByRef reason As String) As Boolean
        reason = ""
        If cfg Is Nothing OrElse Not cfg.LevelingAgentEnabled OrElse Not cfg.NavigationTravelExecutionEnabled Then
            Return False
        End If

        If TrySendRetargetKey(hwnd, cfg, now, "E (travel mob scan)", forced:=False) Then
            reason = $"Travel scan: checking for mobs every {Math.Max(1, cfg.RetargetMs)}ms."
            Return True
        End If

        Return False
    End Function

    Private Shared Function TryParseMapCoordinateAxis(rawText As String, ByRef value As Integer, ByRef confidence As Integer, Optional referenceValue As Integer = -1) As Boolean
        value = -1
        confidence = 0
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim normalizedRaw As String = rawText.ToUpperInvariant()
        normalizedRaw = NormalizeMapCoordinateOcrText(normalizedRaw)
        normalizedRaw = Regex.Replace(normalizedRaw, "[^0-9]", " ")
        normalizedRaw = Regex.Replace(normalizedRaw, "\s+", " ").Trim()
        If normalizedRaw = "" Then
            Return False
        End If

        Dim bestValue As Integer = -1
        Dim bestConfidence As Integer = 0
        Dim bestDelta As Integer = Integer.MaxValue
        For Each m As Match In Regex.Matches(normalizedRaw, "\d+")
            Dim candidateValue As Integer = -1
            Dim candidateConfidence As Integer = 0
            If Not TryParseMapCoordinateToken(m.Value, referenceValue, candidateValue, candidateConfidence) Then
                Continue For
            End If

            Dim delta As Integer = If(referenceValue >= 0 AndAlso referenceValue <= 999, Math.Abs(candidateValue - referenceValue), 0)
            If bestValue < 0 OrElse candidateConfidence > bestConfidence OrElse (candidateConfidence = bestConfidence AndAlso delta < bestDelta) Then
                bestValue = candidateValue
                bestConfidence = candidateConfidence
                bestDelta = delta
            End If
        Next

        If bestValue >= 0 Then
            value = bestValue
            confidence = bestConfidence
            Return True
        End If

        Return False
    End Function

    Private Shared Function TryParseMapCoordinate(rawText As String, ByRef x As Integer, ByRef y As Integer, ByRef normalized As String, ByRef confidence As Integer, Optional referenceX As Integer = -1, Optional referenceY As Integer = -1) As Boolean
        x = -1
        y = -1
        normalized = ""
        confidence = 0
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim normalizedRaw As String = rawText.ToUpperInvariant()
        normalizedRaw = NormalizeMapCoordinateOcrText(normalizedRaw).Replace("|", "/")
        normalizedRaw = Regex.Replace(normalizedRaw, "[^0-9/,\- ]", " ")
        normalizedRaw = Regex.Replace(normalizedRaw, "\s+", " ").Trim()
        If normalizedRaw = "" Then
            Return False
        End If

        Dim explicitMatch As Match = Regex.Match(normalizedRaw, "(\d+)\s*[/,]\s*(\d+)")
        If explicitMatch.Success Then
            Dim parsedX As Integer = -1
            Dim parsedY As Integer = -1
            Dim xConfidence As Integer = 0
            Dim yConfidence As Integer = 0
            If TryParseMapCoordinateToken(explicitMatch.Groups(1).Value, referenceX, parsedX, xConfidence) AndAlso
               TryParseMapCoordinateToken(explicitMatch.Groups(2).Value, referenceY, parsedY, yConfidence) Then
                x = parsedX
                y = parsedY
                normalized = $"{x:000}/{y:000}"
                confidence = Math.Min(xConfidence, yConfidence)
                Return True
            End If
        End If

        Dim numberMatches As MatchCollection = Regex.Matches(normalizedRaw, "\d+")
        If numberMatches.Count >= 2 Then
            Dim bestX As Integer = -1
            Dim bestY As Integer = -1
            Dim bestConfidence As Integer = -1
            Dim bestDistance As Double = Double.MaxValue

            For i As Integer = 0 To numberMatches.Count - 2
                Dim parsedX As Integer = -1
                Dim parsedY As Integer = -1
                Dim xConfidence As Integer = 0
                Dim yConfidence As Integer = 0
                If Not TryParseMapCoordinateToken(numberMatches(i).Value, referenceX, parsedX, xConfidence) Then
                    Continue For
                End If
                If Not TryParseMapCoordinateToken(numberMatches(i + 1).Value, referenceY, parsedY, yConfidence) Then
                    Continue For
                End If

                Dim pairConfidence As Integer = Math.Min(xConfidence, yConfidence)
                Dim pairDistance As Double =
                    If(referenceX >= 0 AndAlso referenceY >= 0,
                       CalculateDistance(parsedX, parsedY, referenceX, referenceY),
                       0.0R)
                If bestX < 0 OrElse pairConfidence > bestConfidence OrElse (pairConfidence = bestConfidence AndAlso pairDistance < bestDistance) Then
                    bestX = parsedX
                    bestY = parsedY
                    bestConfidence = pairConfidence
                    bestDistance = pairDistance
                End If
            Next

            If bestX >= 0 AndAlso bestY >= 0 Then
                x = bestX
                y = bestY
                normalized = $"{x:000}/{y:000}"
                confidence = bestConfidence
                Return True
            End If
        End If

        Return False
    End Function

    Private Shared Function NormalizeMapCoordinateOcrText(raw As String) As String
        Dim normalized As String = If(raw, "").ToUpperInvariant()
        normalized = normalized.Replace("O", "0").
                                Replace("Q", "0").
                                Replace("D", "0").
                                Replace("I", "1").
                                Replace("L", "1").
                                Replace("|", "1").
                                Replace("S", "5").
                                Replace("B", "8").
                                Replace("G", "6").
                                Replace("Z", "2")
        Return normalized
    End Function

    Private Shared Function TryParseMapCoordinateToken(rawToken As String, referenceValue As Integer, ByRef value As Integer, ByRef confidence As Integer) As Boolean
        value = -1
        confidence = 0
        Dim digits As String = Regex.Replace(If(rawToken, ""), "\D", "")
        If digits = "" Then
            Return False
        End If

        If digits.Length = 3 Then
            value = Integer.Parse(digits)
            confidence = 99
            Return True
        End If

        Dim candidates As New List(Of Tuple(Of Integer, Integer))()
        If digits.Length > 3 Then
            For i As Integer = 0 To digits.Length - 3
                Dim parsed As Integer = Integer.Parse(digits.Substring(i, 3))
                candidates.Add(Tuple.Create(parsed, 72))
            Next
        ElseIf digits.Length = 2 Then
            Dim parsed As Integer = Integer.Parse(digits)
            candidates.Add(Tuple.Create(parsed, 42))
            If referenceValue >= 0 AndAlso referenceValue <= 999 Then
                For prefix As Integer = 0 To 9
                    candidates.Add(Tuple.Create((prefix * 100) + parsed, 62))
                Next
                For suffix As Integer = 0 To 9
                    candidates.Add(Tuple.Create((parsed * 10) + suffix, 62))
                Next
            End If
        ElseIf digits.Length = 1 Then
            Dim parsed As Integer = Integer.Parse(digits)
            candidates.Add(Tuple.Create(parsed, 35))
            If referenceValue >= 0 AndAlso referenceValue <= 999 Then
                For a As Integer = 0 To 9
                    For b As Integer = 0 To 9
                        candidates.Add(Tuple.Create((parsed * 100) + (a * 10) + b, 40))
                        candidates.Add(Tuple.Create((a * 100) + (parsed * 10) + b, 40))
                        candidates.Add(Tuple.Create((a * 100) + (b * 10) + parsed, 40))
                    Next
                Next
            End If
        End If

        Dim bestValue As Integer = -1
        Dim bestConfidence As Integer = -1
        Dim bestDelta As Integer = Integer.MaxValue
        Dim hasReference As Boolean = referenceValue >= 0 AndAlso referenceValue <= 999
        For Each candidate As Tuple(Of Integer, Integer) In candidates
            If candidate.Item1 < 0 OrElse candidate.Item1 > 999 Then
                Continue For
            End If

            Dim delta As Integer = If(hasReference, Math.Abs(candidate.Item1 - referenceValue), 0)
            If bestValue < 0 OrElse
               (hasReference AndAlso delta < bestDelta) OrElse
               ((Not hasReference OrElse delta = bestDelta) AndAlso candidate.Item2 > bestConfidence) Then
                bestValue = candidate.Item1
                bestConfidence = candidate.Item2
                bestDelta = delta
            End If
        Next

        If bestValue < 0 Then
            Return False
        End If

        value = bestValue
        confidence = bestConfidence
        Return True
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
        Dim bounds As Rectangle = GetPolygonBounds(frame, points)
        If bounds = Rectangle.Empty Then
            Return Nothing
        End If

        Dim normalized As List(Of DrawingPoint) = points.Select(Function(pt) New DrawingPoint(Math.Max(0, Math.Min(frame.Width - 1, pt.X)), Math.Max(0, Math.Min(frame.Height - 1, pt.Y)))).ToList()
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

    Private Shared Function GetPolygonBounds(frame As Bitmap, points As List(Of DrawingPoint)) As Rectangle
        If frame Is Nothing OrElse points Is Nothing OrElse points.Count < 3 Then
            Return Rectangle.Empty
        End If

        Dim normalized As List(Of DrawingPoint) = points.Select(Function(pt) New DrawingPoint(Math.Max(0, Math.Min(frame.Width - 1, pt.X)), Math.Max(0, Math.Min(frame.Height - 1, pt.Y)))).ToList()
        If normalized.Count < 3 Then
            Return Rectangle.Empty
        End If

        Dim minX As Integer = normalized.Min(Function(pt) pt.X)
        Dim minY As Integer = normalized.Min(Function(pt) pt.Y)
        Dim maxX As Integer = normalized.Max(Function(pt) pt.X)
        Dim maxY As Integer = normalized.Max(Function(pt) pt.Y)
        If maxX <= minX OrElse maxY <= minY Then
            Return Rectangle.Empty
        End If

        Return New Rectangle(minX, minY, Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1))
    End Function

    Private Function ReadMobNameIfNeeded(frame As Bitmap, region As RectRegion, now As DateTime, Optional forceRefresh As Boolean = False, Optional minIntervalMs As Integer = 650) As String
        If frame Is Nothing Then
            Return ""
        End If

        If _mobNameOcrTask IsNot Nothing AndAlso _mobNameOcrTask.IsCompleted Then
            Try
                Dim candidate As String = NormalizeMobNameDisplay(If(_mobNameOcrTask.Result, "").Trim())
                If Not String.IsNullOrWhiteSpace(candidate) Then
                    _cachedMobName = candidate
                    _lastMobNameDetectedAt = now
                ElseIf _lastMobNameDetectedAt = DateTime.MinValue OrElse
                       (now - _lastMobNameDetectedAt).TotalMilliseconds > 1200 Then
                    _cachedMobName = ""
                End If
                _lastMobNameRead = now
            Catch
            End Try
            _mobNameOcrTask = Nothing
        End If

        If _mobNameOcrTask IsNot Nothing Then
            Return _cachedMobName
        End If

        Dim effectiveMinIntervalMs As Integer = Math.Max(120, minIntervalMs)
        If (Not forceRefresh) AndAlso _mobNameOcrStartedAt <> DateTime.MinValue AndAlso (now - _mobNameOcrStartedAt).TotalMilliseconds < effectiveMinIntervalMs Then
            Return _cachedMobName
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return _cachedMobName
        End If

        Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            _mobNameOcrStartedAt = now
            _mobNameOcrTask = Task.Run(
                Function()
                    Try
                        Return OcrReader.ReadName(crop)
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
            Return _cachedMobName
        Catch
            crop.Dispose()
        End Try

        Return _cachedMobName
    End Function

    Private Function ReadMobNameFromClientRegionIfNeeded(hwnd As IntPtr, region As RectRegion, now As DateTime, Optional forceRefresh As Boolean = False, Optional minIntervalMs As Integer = 650) As String
        If hwnd = IntPtr.Zero OrElse region Is Nothing Then
            Return _cachedMobName
        End If

        Dim crop As Bitmap = CaptureClientRegion(hwnd, region)
        If crop Is Nothing Then
            Return _cachedMobName
        End If

        Try
            Return ReadMobNameIfNeeded(crop, New RectRegion(0, 0, crop.Width, crop.Height), now, forceRefresh, minIntervalMs)
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

        Dim hasMobHpBarSignal As Boolean = mobHpPercent >= Math.Max(0.6, cfg.MobHpPresenceThreshold * 0.7)
        Dim canTrack As Boolean =
            frame IsNot Nothing AndAlso
            hasMobHpBarSignal

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
            Math.Max(0, rect.Left - 8),
            Math.Max(0, rect.Top - 10),
            Math.Min(frame.Width, rect.Right + 8),
            Math.Min(frame.Height, rect.Bottom + 10))
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return _lastMobDetectedMaxHp
        End If

        Dim exactCrop As Bitmap = CropFrameRegion(frame, rect)
        Dim paddedCrop As Bitmap = Nothing
        Try
            If paddedRect.Width > 1 AndAlso paddedRect.Height > 1 AndAlso Not paddedRect.Equals(rect) Then
                paddedCrop = CropFrameRegion(frame, paddedRect)
            End If

            Dim workerExactCrop As Bitmap = exactCrop
            Dim workerPaddedCrop As Bitmap = paddedCrop
            exactCrop = Nothing
            paddedCrop = Nothing
            _lastMobHpTextScan = now
            _mobHpTextOcrTask = Task.Run(
                Function()
                    Using workerExactCrop
                        Dim exactText As String = OcrReader.ReadHpFraction(workerExactCrop)
                        If ParseMobMaxHpFromText(exactText) > 0 Then
                            Return exactText
                        End If
                    End Using

                    If workerPaddedCrop IsNot Nothing Then
                        Using workerPaddedCrop
                            Return OcrReader.ReadHpFraction(workerPaddedCrop)
                        End Using
                    End If

                    Return ""
                End Function)
        Finally
            If exactCrop IsNot Nothing Then
                exactCrop.Dispose()
            End If
            If paddedCrop IsNot Nothing Then
                paddedCrop.Dispose()
            End If
        End Try

        Return _lastMobDetectedMaxHp
    End Function

    Private Shared Function CropFrameRegion(frame As Bitmap, rect As Rectangle) As Bitmap
        Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
        Using g As Graphics = Graphics.FromImage(crop)
            g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
        End Using
        Return crop
    End Function

    Private Function UpdateMobMaxHpTrackingFromClientRegion(cfg As BotConfig, hwnd As IntPtr, region As RectRegion, targetWindowVisible As Boolean, mobHpPercent As Double, now As DateTime) As Integer
        If hwnd = IntPtr.Zero OrElse region Is Nothing Then
            Return _lastMobDetectedMaxHp
        End If

        Dim crop As Bitmap = CaptureClientRegion(hwnd, region)
        If crop Is Nothing Then
            Return _lastMobDetectedMaxHp
        End If

        Try
            Return UpdateMobMaxHpTracking(cfg, crop, New RectRegion(0, 0, crop.Width, crop.Height), targetWindowVisible, mobHpPercent, now)
        Finally
            crop.Dispose()
        End Try
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
        normalized = normalized.Replace("O", "0").Replace("Q", "0").Replace("D", "0")
        normalized = normalized.Replace("I", "1").Replace("L", "1").Replace("|", "1").Replace("!", "1")
        normalized = normalized.Replace("S", "5").Replace("B", "8").Replace("Z", "2")
        normalized = normalized.Replace(",", "").Replace(".", "")
        normalized = normalized.Replace("\", "/").Replace(":", "/").Replace(";", "/")
        normalized = Regex.Replace(normalized, "[^0-9/ ]", " ")
        normalized = Regex.Replace(normalized, "/{2,}", "/")
        normalized = Regex.Replace(normalized, "\s+", " ").Trim()

        Dim fractionMatch As Match = Regex.Match(normalized, "(\d{2,9})\s*/\s*(\d{2,9})")
        If fractionMatch.Success Then
            Return $"{fractionMatch.Groups(1).Value}/{fractionMatch.Groups(2).Value}"
        End If

        Dim spacedPair As Match = Regex.Match(normalized, "(\d{2,9})\s+(\d{2,9})")
        If spacedPair.Success Then
            Return $"{spacedPair.Groups(1).Value}/{spacedPair.Groups(2).Value}"
        End If

        Dim digitsOnly As String = Regex.Replace(normalized, "\D", "")
        If digitsOnly.Length >= 4 AndAlso digitsOnly.Length Mod 2 = 0 Then
            Dim half As Integer = digitsOnly.Length \ 2
            Return $"{digitsOnly.Substring(0, half)}/{digitsOnly.Substring(half)}"
        End If

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
        ' Only skip if an attack/buff key was just sent to avoid immediate input collision.
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

    Private Sub TryHandleArrowUnbundle(cfg As BotConfig, hwnd As IntPtr, clientWidth As Integer, clientHeight As Integer, now As DateTime, actionSent As Boolean)
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse Not cfg.ArrowUnbundleEnabled Then
            Return
        End If

        Dim points As List(Of LootScanPoint) = If(cfg.ArrowUnbundlePoints, New List(Of LootScanPoint)()).
            Where(Function(sourcePoint) sourcePoint IsNot Nothing AndAlso sourcePoint.X >= 0 AndAlso sourcePoint.Y >= 0).
            Select(Function(sourcePoint) New LootScanPoint(sourcePoint.X, sourcePoint.Y)).
            ToList()
        If points.Count = 0 Then
            Return
        End If

        If actionSent AndAlso _lastAttackAction <> DateTime.MinValue AndAlso (now - _lastAttackAction).TotalMilliseconds < 220 Then
            Return
        End If

        Dim intervalMs As Integer = Math.Max(1000, cfg.ArrowUnbundleIntervalMs)
        If _lastArrowUnbundleAt <> DateTime.MinValue AndAlso (now - _lastArrowUnbundleAt).TotalMilliseconds < intervalMs Then
            Return
        End If

        If _arrowUnbundleNextIndex < 0 OrElse _arrowUnbundleNextIndex >= points.Count Then
            _arrowUnbundleNextIndex = 0
        End If

        Dim pt As LootScanPoint = points(_arrowUnbundleNextIndex)
        Dim clickX As Integer = Math.Max(0, Math.Min(Math.Max(0, clientWidth - 1), pt.X))
        Dim clickY As Integer = Math.Max(0, Math.Min(Math.Max(0, clientHeight - 1), pt.Y))
        If DoubleRightClickClientPoint(hwnd, clickX, clickY, 10, 35, 90) Then
            _lastArrowUnbundleAt = now
            _arrowUnbundleNextIndex = (_arrowUnbundleNextIndex + 1) Mod points.Count
            SetLastAction($"Double right-click arrow unbundle ({clickX},{clickY})")
            RaiseEvent LogLine($"Arrow unbundle double right-click sent at {clickX},{clickY}.")
        End If
    End Sub

    Private Function GetCachedPranaExpPercent() As Double
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

        Return _lastExpPercent
    End Function

    Private Function IsStatsOcrDue(now As DateTime) As Boolean
        Dim expDue As Boolean =
            _expOcrTask Is Nothing AndAlso
            (_lastExpOcrAt = DateTime.MinValue OrElse (now - _lastExpOcrAt).TotalMilliseconds >= ExpOcrMinIntervalMs)
        Dim rupiahsDue As Boolean =
            _rupiahsOcrTask Is Nothing AndAlso
            (_lastRupiahsOcrAt = DateTime.MinValue OrElse (now - _lastRupiahsOcrAt).TotalMilliseconds >= RupiahsOcrMinIntervalMs)
        Return expDue OrElse rupiahsDue
    End Function

    Private Function ReadPranaExpPercent(hwnd As IntPtr, frame As Bitmap, pranaExpRegion As RectRegion) As Double
        Dim now As DateTime = DateTime.UtcNow
        GetCachedPranaExpPercent()

        If _expOcrTask IsNot Nothing Then
            Return _lastExpPercent
        End If

        If _lastExpOcrAt <> DateTime.MinValue AndAlso (now - _lastExpOcrAt).TotalMilliseconds < ExpOcrMinIntervalMs Then
            Return _lastExpPercent
        End If

        Dim crop As Bitmap = CaptureOcrRegion(hwnd, frame, pranaExpRegion)
        If crop Is Nothing Then
            Return _lastExpPercent
        End If

        Try
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

    Private Shared Function CaptureOcrRegion(hwnd As IntPtr, frame As Bitmap, region As RectRegion) As Bitmap
        If region Is Nothing Then
            Return Nothing
        End If

        If frame IsNot Nothing Then
            Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
            If rect.Width <= 1 OrElse rect.Height <= 1 Then
                Return Nothing
            End If

            Dim crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Try
                Using g As Graphics = Graphics.FromImage(crop)
                    g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
                End Using
                Return crop
            Catch
                crop.Dispose()
                Return Nothing
            End Try
        End If

        If hwnd = IntPtr.Zero Then
            Return Nothing
        End If

        Return CaptureClientRegion(hwnd, region)
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

    Private Function GetCachedRupiahsTotal() As Long
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

        Return _lastRupiahsTotal
    End Function

    Private Function ReadRupiahsTotal(hwnd As IntPtr, frame As Bitmap, rupiahsRegion As RectRegion) As Long
        Dim now As DateTime = DateTime.UtcNow
        GetCachedRupiahsTotal()

        If _rupiahsOcrTask IsNot Nothing Then
            Return _lastRupiahsTotal
        End If

        If _lastRupiahsOcrAt <> DateTime.MinValue AndAlso (now - _lastRupiahsOcrAt).TotalMilliseconds < RupiahsOcrMinIntervalMs Then
            Return _lastRupiahsTotal
        End If

        Dim crop As Bitmap = CaptureOcrRegion(hwnd, frame, rupiahsRegion)
        If crop Is Nothing Then
            Return _lastRupiahsTotal
        End If

        Try
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

        Dim partyInviteIntervalMs As Integer = Math.Max(250, cfg.PartyInviteScanIntervalMs)
        If _lastPartyInviteScan <> DateTime.MinValue AndAlso (now - _lastPartyInviteScan).TotalMilliseconds < partyInviteIntervalMs Then
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

            If cfg.PixelChangeGateEnabled Then
                Dim signature As ULong = ComputeVisualSignature(crop)
                If signature <> 0UL AndAlso signature = _lastPartyInviteVisualSignature Then
                    crop.Dispose()
                    Return False
                End If
                _lastPartyInviteVisualSignature = signature
            End If

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

    Private Sub PruneRepairMatchTimes(now As DateTime)
        Dim cutoff As DateTime = now.AddMilliseconds(-RepairConfirmWindowMs)
        While _repairMatchTimes.Count > 0 AndAlso _repairMatchTimes.Peek() < cutoff
            _repairMatchTimes.Dequeue()
        End While
    End Sub

    Private Sub ResetRepairMatchWindow()
        _repairMatchTimes.Clear()
        _repairConfirmCount = 0
        _repairLastMatchAt = DateTime.MinValue
    End Sub

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

    Private Function TryHandleDisconnectMessage(cfg As BotConfig, hwnd As IntPtr, frame As Bitmap, now As DateTime, disconnectMessageRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse frame Is Nothing OrElse disconnectMessageRegion Is Nothing Then
            Return _disconnectLatched
        End If

        If _disconnectOcrTask IsNot Nothing AndAlso _disconnectOcrTask.IsCompleted Then
            Try
                _lastDisconnectCandidate = If(_disconnectOcrTask.Result, "").Trim()
            Catch
                _lastDisconnectCandidate = ""
            End Try
            _disconnectOcrTask = Nothing
            ProcessDisconnectOcrResult(_lastDisconnectCandidate, now)
        End If

        If _disconnectOcrTask IsNot Nothing Then
            Return _disconnectLatched
        End If

        If _lastDisconnectScan <> DateTime.MinValue AndAlso (now - _lastDisconnectScan).TotalMilliseconds < DisconnectOcrMinIntervalMs Then
            Return _disconnectLatched
        End If

        Dim rect As Rectangle = disconnectMessageRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return _disconnectLatched
        End If

        Dim crop As New Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb)
        Try
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using

            _lastDisconnectScan = now
            _disconnectOcrTask = Task.Run(
                Function()
                    Try
                        Using enlarged As Bitmap = EnlargeBitmap(crop, 3)
                            Dim text As String = If(OcrReader.ReadScreenTextIsolated(enlarged), "").Trim()
                            If text = "" Then
                                text = If(OcrReader.ReadName(enlarged), "").Trim()
                            End If
                            Return text
                        End Using
                    Catch
                        Return ""
                    Finally
                        crop.Dispose()
                    End Try
                End Function)
        Catch
            crop.Dispose()
        End Try

        Return _disconnectLatched
    End Function

    Private Function TryHandleDisconnectMessageFromClientRegion(cfg As BotConfig, hwnd As IntPtr, now As DateTime, disconnectMessageRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse disconnectMessageRegion Is Nothing Then
            Return _disconnectLatched
        End If

        Dim crop As Bitmap = CaptureClientRegion(hwnd, disconnectMessageRegion)
        If crop Is Nothing Then
            Return _disconnectLatched
        End If

        Try
            Return TryHandleDisconnectMessage(cfg, hwnd, crop, now, New RectRegion(0, 0, crop.Width, crop.Height))
        Finally
            crop.Dispose()
        End Try
    End Function

    Private Sub ProcessDisconnectOcrResult(rawText As String, now As DateTime)
        Dim matched As Boolean = IsDisconnectPrompt(rawText)
        If matched Then
            _disconnectClearCount = 0
            If _disconnectLastMatchAt = DateTime.MinValue OrElse (now - _disconnectLastMatchAt).TotalMilliseconds > DisconnectConfirmWindowMs Then
                _disconnectConfirmCount = 1
            Else
                _disconnectConfirmCount += 1
            End If
            _disconnectLastMatchAt = now

            If Not _disconnectLatched AndAlso _disconnectConfirmCount >= DisconnectConfirmRequiredCount Then
                _disconnectLatched = True
                _disconnectConfirmCount = 0
                RaiseEvent LogLine("Game disconnected message detected by OCR.")
            End If
            Return
        End If

        _disconnectConfirmCount = 0
        _disconnectLastMatchAt = DateTime.MinValue
        _lastDisconnectCandidate = ""
        If _disconnectLatched Then
            _disconnectClearCount += 1
            If _disconnectClearCount >= UnreachableClearRequiredCount Then
                _disconnectLatched = False
                _disconnectClearCount = 0
                RaiseEvent LogLine("Game disconnected message cleared.")
            End If
        End If
    End Sub

    Private Shared Function IsDisconnectPrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeForLooseTextMatch(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("connectiontoserverhasfailed", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("serverhasfailedpleasetryagain", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("sorryconnectiontoserver", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        If AreTextsClose(norm, "sorry connection to server has failed please try again") OrElse
           AreTextsClose(norm, "connection to server has failed please try again") Then
            Return True
        End If

        Dim hasConnection As Boolean = norm.Contains("connection", StringComparison.OrdinalIgnoreCase) OrElse norm.Contains("connect", StringComparison.OrdinalIgnoreCase)
        Dim hasServer As Boolean = norm.Contains("server", StringComparison.OrdinalIgnoreCase)
        Dim hasFailed As Boolean = norm.Contains("failed", StringComparison.OrdinalIgnoreCase) OrElse norm.Contains("fail", StringComparison.OrdinalIgnoreCase)
        Dim hasTryAgain As Boolean = norm.Contains("try again", StringComparison.OrdinalIgnoreCase)
        Return hasConnection AndAlso hasServer AndAlso hasFailed AndAlso hasTryAgain
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
            Dim repairMatched As Boolean = IsRepairPrompt(_lastUnreachableCandidate)
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
                If _unreachableLatched Then
                    _unreachableClearCount += 1
                    If _unreachableClearCount >= UnreachableClearRequiredCount Then
                        _unreachableLatched = False
                        _unreachableClearCount = 0
                    End If
                End If
            End If

            If repairMatched Then
                _repairClearCount = 0
                If Not _repairLatched Then
                    _repairMatchTimes.Enqueue(now)
                    PruneRepairMatchTimes(now)
                    _repairConfirmCount = _repairMatchTimes.Count
                    _repairLastMatchAt = now
                Else
                    _repairConfirmCount = 0
                End If
            Else
                PruneRepairMatchTimes(now)
                _repairConfirmCount = _repairMatchTimes.Count
                If _repairConfirmCount = 0 Then
                    _repairLastMatchAt = DateTime.MinValue
                End If
                If _repairLatched Then
                    _repairClearCount += 1
                    If _repairClearCount >= UnreachableClearRequiredCount Then
                        _repairLatched = False
                        _repairClearCount = 0
                    End If
                End If
            End If

            If Not matched AndAlso Not repairMatched Then
                _lastUnreachableCandidate = ""
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
        If _repairConfirmCount > 0 Then
            PruneRepairMatchTimes(now)
            _repairConfirmCount = _repairMatchTimes.Count
            If _repairConfirmCount = 0 Then
                _repairLastMatchAt = DateTime.MinValue
            End If
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

    Private Function TryHandleUnreachableTargetFromClientRegion(cfg As BotConfig, hwnd As IntPtr, now As DateTime, unreachableTextRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero OrElse unreachableTextRegion Is Nothing Then
            Return False
        End If

        Dim crop As Bitmap = CaptureClientRegion(hwnd, unreachableTextRegion)
        If crop Is Nothing Then
            Return False
        End If

        Try
            Return TryHandleUnreachableTarget(cfg, hwnd, crop, now, New RectRegion(0, 0, crop.Width, crop.Height))
        Finally
            crop.Dispose()
        End Try
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

    Private Shared Function IsRepairPrompt(rawText As String) As Boolean
        If String.IsNullOrWhiteSpace(rawText) Then
            Return False
        End If

        Dim norm As String = NormalizeMobName(rawText)
        If norm = "" Then
            Return False
        End If

        Dim compact As String = norm.Replace(" ", "")
        If compact.Contains("isabouttobreak", StringComparison.OrdinalIgnoreCase) OrElse
           compact.Contains("abouttobreak", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Return norm.Contains("about to break", StringComparison.OrdinalIgnoreCase) OrElse
               AreTextsClose(norm, "is about to break")
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
            If IsPreferredSubstringMatch(normMob, normPreferred) Then
                Return True
            End If
            If AreTextsClose(normMob, normPreferred) Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function IsPreferredSubstringMatch(normMob As String, normPreferred As String) As Boolean
        If String.IsNullOrWhiteSpace(normMob) OrElse String.IsNullOrWhiteSpace(normPreferred) Then
            Return False
        End If

        Dim mobTokens As String() = normMob.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        Dim preferredTokens As String() = normPreferred.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If mobTokens.Length = 0 OrElse preferredTokens.Length = 0 Then
            Return False
        End If

        For Each preferredToken As String In preferredTokens
            If preferredToken.Length < 3 Then
                Continue For
            End If

            For Each mobToken As String In mobTokens
                If mobToken.Length < 3 Then
                    Continue For
                End If

                If mobToken.Contains(preferredToken, StringComparison.OrdinalIgnoreCase) OrElse preferredToken.Contains(mobToken, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If

                If preferredToken.Length >= 4 AndAlso mobToken.Length >= 4 Then
                    Dim windowLength As Integer = Math.Min(mobToken.Length, preferredToken.Length)
                    For start As Integer = 0 To mobToken.Length - windowLength
                        Dim mobWindow As String = mobToken.Substring(start, windowLength)
                        If AreTextsClose(mobWindow, preferredToken) Then
                            Return True
                        End If
                    Next
                End If
            Next
        Next

        If mobTokens.Length >= preferredTokens.Length Then
            For start As Integer = 0 To mobTokens.Length - preferredTokens.Length
                Dim mobWindow As String = String.Join(" ", mobTokens.Skip(start).Take(preferredTokens.Length))
                If AreTextsClose(mobWindow, normPreferred) Then
                    Return True
                End If
            Next
        End If

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

    Private Shared Function TryFindAllowedLootRegionMatch(regions As List(Of OcrReader.OcrTextRegion), allowList As List(Of String), thresholdPercent As Integer, ByRef matchedAllowedName As String, ByRef matchedRegion As OcrReader.OcrTextRegion) As Boolean
        matchedAllowedName = ""
        matchedRegion = Nothing
        If regions Is Nothing OrElse regions.Count = 0 OrElse allowList Is Nothing OrElse allowList.Count = 0 Then
            Return False
        End If

        For Each region In regions
            If region Is Nothing OrElse region.Bounds = Rectangle.Empty OrElse String.IsNullOrWhiteSpace(region.Text) Then
                Continue For
            End If

            Dim localMatchedName As String = ""
            If TryFindAllowedLootMatch(region.Text, allowList, thresholdPercent, localMatchedName) Then
                matchedAllowedName = localMatchedName
                matchedRegion = region
                Return True
            End If
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
        raw = NormalizeMobNameDisplay(raw)
        Dim cleaned As String = Regex.Replace(raw, "[^A-Za-z0-9 '\-]", " ")
        cleaned = Regex.Replace(cleaned, "\s+", " ").Trim().ToLowerInvariant()
        Return cleaned
    End Function

    Private Shared Function NormalizeMobNameDisplay(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If

        Dim cleaned As String = Regex.Replace(raw, "\s+", " ").Trim()
        Dim levelFirst As Match = Regex.Match(cleaned, "^\s*Lv\s*\.?\s*(\d{1,3})\s*(?:\||-|:)?\s*(.+?)\s*$", RegexOptions.IgnoreCase)
        If levelFirst.Success Then
            Dim level As String = levelFirst.Groups(1).Value
            Dim namePart As String = Regex.Replace(levelFirst.Groups(2).Value, "^\s*(?:\||-|:)\s*", "").Trim()
            namePart = Regex.Replace(namePart, "\s*(?:\||-|:)\s*$", "").Trim()
            If namePart <> "" AndAlso Not Regex.IsMatch(namePart, "^Lv\s*\.?\s*\d{1,3}\b", RegexOptions.IgnoreCase) Then
                Return $"{namePart} Lv{level}"
            End If
        End If

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

    Private Function IsRecentTargetSignalHoldActive(now As DateTime, cfg As BotConfig) As Boolean
        Dim graceMs As Integer = GetTargetSignalGraceMs(cfg)
        Dim shortAttackGraceMs As Integer = Math.Max(900, Math.Min(graceMs, Math.Max(1, If(cfg?.RetargetMs, 550)) * 2))
        Return IsRecentUtc(_lastTargetValidAt, now, graceMs) OrElse
            IsRecentUtc(_lastLivingTargetSignalAt, now, graceMs) OrElse
            IsRecentUtc(_lastTargetWindowSeen, now, graceMs) OrElse
            (_combatLockActive AndAlso IsRecentUtc(_combatLockLastSeenAt, now, graceMs)) OrElse
            IsRecentUtc(_lastAttackAction, now, shortAttackGraceMs)
    End Function

    Private Shared Function GetTargetSignalGraceMs(cfg As BotConfig) As Integer
        Dim retargetMs As Integer = If(cfg Is Nothing, 550, Math.Max(1, cfg.RetargetMs))
        Dim configuredWindow As Integer = (retargetMs + RetargetBufferMs) * 3
        Return Math.Max(TargetSignalGraceMinMs, Math.Min(TargetSignalGraceMaxMs, configuredWindow))
    End Function

    Private Shared Function IsRecentUtc(timestamp As DateTime, now As DateTime, windowMs As Integer) As Boolean
        Return timestamp <> DateTime.MinValue AndAlso (now - timestamp).TotalMilliseconds <= Math.Max(1, windowMs)
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
            ClearMobMaxHpTracking()
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

    Private Sub ClearMobMaxHpTracking()
        _lastMobHpTextScan = DateTime.MinValue
        _mobHpTextOcrTask = Nothing
        _lastMobHpText = ""
        _lastMobDetectedMaxHp = -1
    End Sub

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
        Dim configuredNoProgressMs As Integer = Math.Max(1000, If(cfg?.StuckTargetNoProgressRetargetMs, 6000))
        Dim requiredNoProgressMs As Integer = configuredNoProgressMs
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

    Private Shared Function IsOffensiveBuffRole(role As String) As Boolean
        Dim normalized As String = If(role, "").Trim().ToLowerInvariant()
        Return normalized = "buff" OrElse normalized = "high_max_hp"
    End Function

    Private Shared Function IsRepairRole(role As String) As Boolean
        Return String.Equals(role, "repair", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsMonsterFilterWhitelistMode(cfg As BotConfig) As Boolean
        If cfg Is Nothing Then
            Return False
        End If
        Return String.Equals(If(cfg.MonsterFilterMode, "").Trim(), "whitelist", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function GetMonsterFilterConfirmRequiredCount(cfg As BotConfig) As Integer
        If cfg Is Nothing OrElse cfg.MonsterFilterConfirmReads <= 0 Then
            Return TargetNameConfirmRequiredCount
        End If
        Return Math.Max(1, Math.Min(2, cfg.MonsterFilterConfirmReads))
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

    Private Function TrySendSupportActions(cfg As BotConfig, hwnd As IntPtr, hpPercent As Double, mpPercent As Double, Optional hpRegion As RectRegion = Nothing, Optional mpRegion As RectRegion = Nothing) As Boolean
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
            If Not ConfirmSupportActionStillNeeded(cfg, hwnd, action, hpPercent, mpPercent, hpRegion, mpRegion) Then
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

        For Each action In ordered
            If action.Role = "max_health" Then
                Continue For
            End If
            If Not IsSupportTriggered(action, hpPercent, mpPercent) Then
                Continue For
            End If
            If Not ConfirmSupportActionStillNeeded(cfg, hwnd, action, hpPercent, mpPercent, hpRegion, mpRegion) Then
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

    Private Function ConfirmSupportActionStillNeeded(cfg As BotConfig, hwnd As IntPtr, action As ActionRule, hpPercent As Double, mpPercent As Double, hpRegion As RectRegion, mpRegion As RectRegion) As Boolean
        If cfg Is Nothing OrElse action Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim role As String = If(action.Role, "").Trim().ToLowerInvariant()
        If cfg.LiteModeEnabled AndAlso (role = "heal" OrElse role = "max_health" OrElse role = "mana") Then
            Return ConfirmLitePointSupportActionStillNeeded(cfg, hwnd, action, role)
        End If

        Dim targetRegion As RectRegion = If(role = "mana", mpRegion, hpRegion)
        Dim firstSample As Double = If(role = "mana", mpPercent, hpPercent)
        If firstSample <= 0.25R AndAlso Not IsNearZeroSupportConfirmed(role, hwnd, targetRegion) Then
            RaiseEvent LogLine($"Support action skipped: waiting for {GetNearZeroSupportConfirmRequiredCount(role)} consecutive usable {If(role = "mana", "MP", "HP")}=0 frames before {action.KeyName} ({action.Role}).")
            Return False
        End If

        If targetRegion Is Nothing Then
            Return True
        End If

        Dim lastGood As Double = If(role = "mana", _lastGoodMpPercent, _lastGoodHpPercent)
        Dim companionLastGood As Double = If(role = "mana", _lastGoodHpPercent, _lastGoodMpPercent)
        Dim trigger As Double = Math.Max(1, action.TriggerPercent)
        Dim suddenDrop As Boolean = lastGood >= Math.Max(trigger + 20.0R, 65.0R) AndAlso firstSample <= trigger
        Dim bothResourcesZero As Boolean =
            hpPercent <= 0.25R AndAlso
            mpPercent <= 0.25R AndAlso
            (lastGood >= 5.0R OrElse companionLastGood >= 5.0R)

        If bothResourcesZero Then
            RaiseEvent LogLine($"Support action skipped: ignored impossible HP/MP zero pair before {action.KeyName} ({action.Role}).")
            Return False
        End If

        Dim ok As Boolean = False
        Dim secondSample As Double = ComputeClientBarPercent(hwnd, targetRegion, role <> "mana", cfg, ok)
        If Not ok Then
            If suddenDrop Then
                RaiseEvent LogLine($"Support action skipped: {action.KeyName} ({action.Role}) low-bar confirmation capture failed after sudden {If(role = "mana", "MP", "HP")} drop.")
                Return False
            End If
            Return True
        End If

        If secondSample <= trigger Then
            If suddenDrop Then
                Dim fullFrameSample As Double = -1
                If Not ConfirmSuddenSupportDropWithFullFrame(hwnd, targetRegion, role <> "mana", cfg, trigger, fullFrameSample) Then
                    Dim sampleText As String = If(fullFrameSample < 0, "unavailable", $"{fullFrameSample:0.0}%")
                    RaiseEvent LogLine($"Support action skipped: {action.KeyName} ({action.Role}) sudden drop was not confirmed by full-frame read ({sampleText}).")
                    Return False
                End If
            End If
            Return True
        End If

        RaiseEvent LogLine($"Support action skipped: {action.KeyName} ({action.Role}) first read {firstSample:0.0}% but confirmation read {secondSample:0.0}%.")
        If role = "mana" Then
            mpPercent = secondSample
        Else
            hpPercent = secondSample
        End If
        Return False
    End Function

    Private Function ConfirmLitePointSupportActionStillNeeded(cfg As BotConfig, hwnd As IntPtr, action As ActionRule, role As String) As Boolean
        Dim isMana As Boolean = String.Equals(role, "mana", StringComparison.OrdinalIgnoreCase)
        Dim pointX As Integer = If(isMana, cfg.LiteMpCheckPointX, cfg.LiteHpCheckPointX)
        Dim pointY As Integer = If(isMana, cfg.LiteMpCheckPointY, cfg.LiteHpCheckPointY)
        If pointX < 0 OrElse pointY < 0 Then
            Return False
        End If

        Using frame As Bitmap = CaptureClient(hwnd)
            Dim ok As Boolean = False
            Dim sample As Double = ComputeClientPotionPointPercent(
                frame,
                pointX,
                pointY,
                Not isMana,
                cfg,
                If(isMana, cfg.LiteMpCheckColorEnabled, cfg.LiteHpCheckColorEnabled),
                If(isMana, cfg.LiteMpCheckColorArgb, cfg.LiteHpCheckColorArgb),
                ok)

            If Not ok Then
                RaiseEvent LogLine($"Lite AutoPots: confirmation read failed before {action.KeyName} ({action.Role}).")
                Return False
            End If

            If sample <= Math.Max(1, action.TriggerPercent) Then
                Return True
            End If
        End Using

        RaiseEvent LogLine($"Lite AutoPots: skipped {action.KeyName} ({action.Role}) because the selected pixel still matches.")
        Return False
    End Function

    Private Function IsNearZeroSupportConfirmed(role As String, hwnd As IntPtr, targetRegion As RectRegion) As Boolean
        Dim isMana As Boolean = String.Equals(role, "mana", StringComparison.OrdinalIgnoreCase)
        Dim confirmCount As Integer = If(isMana, _mpZeroSupportConfirmCount, _hpZeroSupportConfirmCount)
        If confirmCount < GetNearZeroSupportConfirmRequiredCount(role) Then
            Return False
        End If

        If targetRegion Is Nothing Then
            Using frame As Bitmap = CaptureClient(hwnd)
                Return frame IsNot Nothing AndAlso Not IsLikelyBlackFrame(frame)
            End Using
        End If

        Dim fullFrameSample As Double = -1
        Return ConfirmNearZeroSupportWithFreshFrames(hwnd, targetRegion, Not isMana, _config, fullFrameSample)
    End Function

    Private Shared Function GetNearZeroSupportConfirmRequiredCount(role As String) As Integer
        If String.Equals(role, "mana", StringComparison.OrdinalIgnoreCase) Then
            Return NearZeroManaConfirmRequiredCount
        End If
        Return NearZeroSupportConfirmRequiredCount
    End Function

    Private Function ConfirmNearZeroSupportWithFreshFrames(hwnd As IntPtr, targetRegion As RectRegion, isHp As Boolean, cfg As BotConfig, ByRef samplePercent As Double) As Boolean
        samplePercent = -1
        If hwnd = IntPtr.Zero OrElse targetRegion Is Nothing Then
            Return False
        End If

        For sampleIndex As Integer = 1 To FreshZeroSupportConfirmSamples
            Using freshFrame As Bitmap = CaptureClient(hwnd)
                If freshFrame Is Nothing OrElse IsLikelyBlackFrame(freshFrame) Then
                    samplePercent = -1
                    Return False
                End If

                samplePercent = ComputeBarPercent(freshFrame, targetRegion, isHp, cfg)
                If samplePercent > 0.25R Then
                    Return False
                End If
            End Using

            If sampleIndex < FreshZeroSupportConfirmSamples Then
                Thread.Sleep(FreshZeroSupportConfirmDelayMs)
            End If
        Next

        Return True
    End Function

    Private Function ConfirmSuddenSupportDropWithFullFrame(hwnd As IntPtr, targetRegion As RectRegion, isHp As Boolean, cfg As BotConfig, trigger As Double, ByRef samplePercent As Double) As Boolean
        samplePercent = -1
        If hwnd = IntPtr.Zero OrElse targetRegion Is Nothing Then
            Return False
        End If

        Using cachedFrame As Bitmap = GetLatestLoopFrameClone(700)
            If cachedFrame IsNot Nothing AndAlso Not IsLikelyBlackFrame(cachedFrame) Then
                samplePercent = ComputeBarPercent(cachedFrame, targetRegion, isHp, cfg)
                If samplePercent > trigger Then
                    Return False
                End If
                If samplePercent > 0.25R Then
                    Return True
                End If
            End If
        End Using

        Using freshFrame As Bitmap = CaptureClient(hwnd)
            If freshFrame Is Nothing OrElse IsLikelyBlackFrame(freshFrame) Then
                Return False
            End If

            samplePercent = ComputeBarPercent(freshFrame, targetRegion, isHp, cfg)
            Return samplePercent <= trigger
        End Using
    End Function

    Private Function TrySendRepairAction(cfg As BotConfig, hwnd As IntPtr) As Boolean
        If hwnd = IntPtr.Zero OrElse cfg Is Nothing OrElse cfg.Actions Is Nothing Then
            Return False
        End If
        If _repairLatched OrElse _repairConfirmCount < RepairConfirmRequiredCount Then
            Return False
        End If

        Dim ordered = cfg.Actions.
            Where(Function(a) a.Enabled AndAlso IsRepairRole(a.Role)).
            OrderBy(Function(a) a.Priority).
            ToList()
        If ordered.Count = 0 Then
            Return False
        End If

        For Each action In ordered
            If Not IsReady(action) Then
                Continue For
            End If
            If Not SendKey(hwnd, action.KeyName, FastKeyPressMs) Then
                Continue For
            End If

            MarkKeyUsed(action.KeyName)
            _repairLatched = True
            _repairClearCount = 0
            ResetRepairMatchWindow()
            _repairTriggerCount += 1
            SetLastAction($"{action.KeyName} (repair)")
            RaiseEvent LogLine("Repair role triggered after 5 OCR reads of 'is about to break' inside the 10-minute rolling window.")
            Return True
        Next

        Return False
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
        Dim cfg As BotConfig
        SyncLock _sync
            cfg = _config
        End SyncLock

        Dim hwnd As IntPtr = ResolveGameWindow(cfg)
        If hwnd = IntPtr.Zero Then
            Dim title As String = If(windowTitle, "").Trim()
            If title = "" Then
                Return False
            End If
            hwnd = FindGameWindow(title)
            If hwnd = IntPtr.Zero Then
                Return False
            End If
        End If

        Return TrySendStopAction(cfg, hwnd, context)
    End Function

    Public Function ManualRetarget(windowTitle As String) As Boolean
        Dim cfg As BotConfig
        SyncLock _sync
            cfg = _config
        End SyncLock

        Dim hwnd As IntPtr = ResolveGameWindow(cfg)
        If hwnd = IntPtr.Zero Then
            Dim title As String = If(windowTitle, "").Trim()
            If title = "" Then
                Return False
            End If
            hwnd = FindGameWindow(title)
        End If

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

    Private Function ChooseAttackBurstActions(cfg As BotConfig, hpPercent As Double, mpPercent As Double, targetValid As Boolean, allowBlindAttack As Boolean, highMaxHpAttackActive As Boolean, suppressOffensiveBuffs As Boolean, ByRef reason As String) As List(Of ActionRule)
        Dim ordered = cfg.Actions.Where(Function(a) a.Enabled).OrderBy(Function(a) a.Priority).ToList()
        If ordered.Count = 0 Then
            reason = "No enabled keys."
            Return New List(Of ActionRule)()
        End If

        Dim hasAttackKey As Boolean = False
        Dim statBlocked As Boolean = False
        Dim cooldownBlocked As Boolean = False
        Dim highMaxHpRoleWaitingForLifeRead As Boolean = False
        Dim offensiveBuffBlocked As Boolean = False
        Dim selected As New List(Of ActionRule)()
        Dim usedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each action In ordered
            Dim role As String = If(action.Role, "").Trim().ToLowerInvariant()
            Dim isAttackLike As Boolean =
                role = "attack" OrElse
                role = "buff" OrElse
                role = "special" OrElse
                role = "high_max_hp"
            If Not isAttackLike Then
                Continue For
            End If
            hasAttackKey = True

            If suppressOffensiveBuffs AndAlso IsOffensiveBuffRole(role) Then
                offensiveBuffBlocked = True
                Continue For
            End If

            If role = "high_max_hp" AndAlso Not highMaxHpAttackActive Then
                highMaxHpRoleWaitingForLifeRead = True
                Continue For
            End If

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
            reason = "No enabled attack/buff/high_max_hp keys."
        ElseIf Not targetValid AndAlso Not allowBlindAttack Then
            reason = "No target detected."
        ElseIf offensiveBuffBlocked Then
            reason = "Buff keys paused because monster blacklist blocked the selected target."
        ElseIf highMaxHpRoleWaitingForLifeRead Then
            reason = "High Max HP keys waiting for mob_life_rect Max HP OCR."
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

    Private Sub RecordTiming(bucket As TimingBucket, elapsedMs As Double)
        If bucket Is Nothing Then
            Return
        End If

        SyncLock _perfSync
            bucket.Add(elapsedMs)
        End SyncLock
    End Sub

    Private Function IsAdaptiveOptionalWorkDeferred() As Boolean
        SyncLock _perfSync
            Return _adaptivePerformanceActive
        End SyncLock
    End Function

    Private Sub MarkOptionalWorkDeferred()
        SyncLock _perfSync
            _adaptiveDeferredOptionalScans += 1
        End SyncLock
    End Sub

    Private Sub RecordLoopCompletion(elapsedMs As Double, targetLoopMs As Integer)
        Dim becameActive As Boolean = False
        Dim recovered As Boolean = False
        Dim targetMs As Double = Math.Max(1, targetLoopMs)
        Dim adaptiveEnabled As Boolean = True
        Dim slowMinMs As Integer = 140
        Dim slowMultiplier As Double = 1.8R
        Dim recoveryMultiplier As Double = 1.25R
        Dim slowConfirmCount As Integer = AdaptiveSlowLoopConfirmCount
        Dim recoveryConfirmCount As Integer = AdaptiveRecoveryLoopConfirmCount

        SyncLock _sync
            If _config IsNot Nothing Then
                adaptiveEnabled = _config.AdaptivePerformanceEnabled
                slowMinMs = Math.Max(40, _config.AdaptiveSlowLoopMinMs)
                slowMultiplier = Math.Max(1.0R, _config.AdaptiveSlowLoopMultiplier)
                recoveryMultiplier = Math.Max(1.0R, _config.AdaptiveRecoveryLoopMultiplier)
                slowConfirmCount = Math.Max(1, _config.AdaptiveSlowConfirmCount)
                recoveryConfirmCount = Math.Max(1, _config.AdaptiveRecoveryConfirmCount)
            End If
        End SyncLock

        Dim slowThresholdMs As Double = Math.Max(CDbl(slowMinMs), targetMs * slowMultiplier)
        Dim recoveryThresholdMs As Double = Math.Max(40.0R, targetMs * recoveryMultiplier)

        SyncLock _perfSync
            _loopTiming.Add(elapsedMs)
            If Not adaptiveEnabled Then
                _adaptivePerformanceActive = False
                _adaptiveSlowLoopCount = 0
                _adaptiveRecoveryLoopCount = 0
                Return
            End If

            If elapsedMs >= slowThresholdMs Then
                _adaptiveSlowLoopCount += 1
                _adaptiveRecoveryLoopCount = 0
            ElseIf elapsedMs <= recoveryThresholdMs Then
                _adaptiveRecoveryLoopCount += 1
                If _adaptiveSlowLoopCount > 0 Then
                    _adaptiveSlowLoopCount -= 1
                End If
            End If

            If (Not _adaptivePerformanceActive) AndAlso _adaptiveSlowLoopCount >= slowConfirmCount Then
                _adaptivePerformanceActive = True
                _adaptiveRecoveryLoopCount = 0
                becameActive = True
            ElseIf _adaptivePerformanceActive AndAlso _adaptiveRecoveryLoopCount >= recoveryConfirmCount Then
                _adaptivePerformanceActive = False
                _adaptiveSlowLoopCount = 0
                recovered = True
            End If
        End SyncLock

        If becameActive Then
            RaiseEvent LogLine("Adaptive performance mode active: optional OCR/capture work will be deferred while combat stays live.")
        ElseIf recovered Then
            RaiseEvent LogLine("Adaptive performance mode recovered: optional OCR/capture work resumed.")
        End If
    End Sub

    Private Function BuildPerformanceDiagnosticsText() As String
        SyncLock _perfSync
            Dim text As String =
                $"AdaptivePerformanceActive: {_adaptivePerformanceActive}{Environment.NewLine}" &
                $"AdaptiveDeferredOptionalScans: {_adaptiveDeferredOptionalScans}{Environment.NewLine}" &
                $"CaptureMethod: {_lastCaptureMethodName}{Environment.NewLine}" &
                _loopTiming.Format("LoopTotal") & Environment.NewLine &
                _captureTiming.Format("Capture") & Environment.NewLine &
                _hpMpScanTiming.Format("HP/MP Scan") & Environment.NewLine &
                _mobOcrTiming.Format("Mob OCR") & Environment.NewLine &
                _chatOcrTiming.Format("Chat OCR") & Environment.NewLine &
                _lootScanTiming.Format("Loot Scan")
            Return text
        End SyncLock
    End Function

    Private Sub SetStatus(updateAction As Action(Of BotStatus))
        Dim snapshot As BotStatus = Nothing
        Dim shouldRaise As Boolean = False
        SyncLock _sync
            updateAction(_status)
            _status.AgentEnabled = _config IsNot Nothing AndAlso _config.LevelingAgentEnabled
            _status.AgentState = _agentState.ToString()
            _status.AgentReason = _agentReason
            _status.AgentGuardrailTriggered = _agentGuardrailTriggered
            _status.MapCoordinateText = _lastMapCoordinateText
            _status.MapCoordinateX = _lastMapCoordinateX
            _status.MapCoordinateY = _lastMapCoordinateY
            _status.MapCoordinateDebugLog = _lastMapCoordinateDebugLog
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
            Dim sampleSnapshotLimit As Integer = Math.Min(_routeRecordingSamples.Count, 200)
            Dim sampleStart As Integer = _routeRecordingSamples.Count - sampleSnapshotLimit
            _status.RouteRecordingSamples = _routeRecordingSamples.GetRange(sampleStart, sampleSnapshotLimit).Select(Function(s) New NavigationRouteSample With {.X = s.X, .Y = s.Y, .CapturedAtUtc = s.CapturedAtUtc}).ToList()
            _status.RouteRecordingStatus = _routeRecordingStatus
            _status.RouteRecordingLastSavedPath = _routeRecordingLastSavedPath
            _status.NavigationReturningToStart = _navigationReturnToStartActive
            _status.NavigationReturnTargetLabel = _navigationReturnTargetNodeLabel
            _status.HoldPlaceEnabled = _config IsNot Nothing AndAlso _config.HoldPlaceEnabled
            _status.HoldPlaceActive = _lastHoldPlaceActive
            _status.HoldPlaceTargetX = _lastHoldPlaceTargetX
            _status.HoldPlaceTargetY = _lastHoldPlaceTargetY
            _status.HoldPlaceDistance = _lastHoldPlaceDistance
            _status.HoldPlaceReason = _lastHoldPlaceReason
            _status.PartySize = _lastPartySize
            _status.PartyAliveCount = _lastPartyAliveCount
            _status.PartyAllAlive = _lastPartyAllAlive
            _status.CharacterName = _lastCharacterName
            PruneRepairMatchTimes(DateTime.UtcNow)
            _repairConfirmCount = _repairMatchTimes.Count
            _status.RepairConfirmCount = _repairConfirmCount
            _status.RepairConfirmRequiredCount = RepairConfirmRequiredCount
            _status.RepairConfirmWindowMinutes = Math.Max(1, RepairConfirmWindowMs \ 60000)
            _status.RepairTriggerCount = _repairTriggerCount
            _status.PerformanceDiagnostics = BuildPerformanceDiagnosticsText()
            _status.EngineRestartCount = _engineRestartCount
            _status.EngineLastRestartUtc = _engineLastRestartUtc
            _status.RunStartedAtUtc = If(_status.Running, _loopStartedAt, DateTime.MinValue)
            _status.UpdatedAt = DateTime.UtcNow
            Dim statusSignature As String = BuildStatusRaiseSignature(_status)
            shouldRaise =
                _lastStatusRaisedAt = DateTime.MinValue OrElse
                statusSignature <> _lastStatusRaisedSignature OrElse
                (_status.UpdatedAt - _lastStatusRaisedAt).TotalMilliseconds >= StatusUpdateMinIntervalMs

            If shouldRaise Then
                _lastStatusRaisedAt = _status.UpdatedAt
                _lastStatusRaisedSignature = statusSignature
                snapshot = CloneStatus(_status)
            End If
        End SyncLock
        If shouldRaise Then
            RaiseEvent StatusUpdated(snapshot)
        End If
    End Sub

    Private Shared Function BuildStatusRaiseSignature(status As BotStatus) As String
        If status Is Nothing Then
            Return ""
        End If

        Return $"{status.Running}|{status.RunStartedAtUtc:O}|{status.WindowFound}|{status.NotAttackingReason}|{status.ErrorMessage}|{status.GameDisconnected}|{status.AgentState}|{status.AgentReason}|{status.AgentGuardrailTriggered}|{status.TargetValid}|{status.MobName}|{status.MobHpText}|{status.MapCoordinateText}|{status.MapCoordinateX}|{status.MapCoordinateY}|{status.MapCoordinateConfidence}|{status.MapCoordinateDebugLog}|{status.HoldPlaceActive}|{status.HoldPlaceReason}|{status.HoldPlaceDistance:0.0}|{status.RepairConfirmCount}|{status.RepairTriggerCount}"
    End Function

    Private Function CloneStatus(src As BotStatus) As BotStatus
        Return New BotStatus With {
            .Running = src.Running,
            .RunStartedAtUtc = src.RunStartedAtUtc,
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
            .CharacterName = src.CharacterName,
            .TargetValid = src.TargetValid,
            .MapCoordinateText = src.MapCoordinateText,
            .MapCoordinateX = src.MapCoordinateX,
            .MapCoordinateY = src.MapCoordinateY,
            .MapCoordinateDebugLog = src.MapCoordinateDebugLog,
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
            .NavigationReturningToStart = src.NavigationReturningToStart,
            .NavigationReturnTargetLabel = src.NavigationReturnTargetLabel,
            .HoldPlaceEnabled = src.HoldPlaceEnabled,
            .HoldPlaceActive = src.HoldPlaceActive,
            .HoldPlaceTargetX = src.HoldPlaceTargetX,
            .HoldPlaceTargetY = src.HoldPlaceTargetY,
            .HoldPlaceDistance = src.HoldPlaceDistance,
            .HoldPlaceReason = src.HoldPlaceReason,
            .RouteRecordingEnabled = src.RouteRecordingEnabled,
            .RouteRecordingActive = src.RouteRecordingActive,
            .RouteRecordingMapName = src.RouteRecordingMapName,
            .RouteRecordingName = src.RouteRecordingName,
            .RouteRecordingSampleCount = src.RouteRecordingSampleCount,
            .RouteRecordingStatus = src.RouteRecordingStatus,
            .RouteRecordingLastSavedPath = src.RouteRecordingLastSavedPath,
            .LastAction = src.LastAction,
            .RepairConfirmCount = src.RepairConfirmCount,
            .RepairConfirmRequiredCount = src.RepairConfirmRequiredCount,
            .RepairConfirmWindowMinutes = src.RepairConfirmWindowMinutes,
            .RepairTriggerCount = src.RepairTriggerCount,
            .NotAttackingReason = src.NotAttackingReason,
            .ErrorMessage = src.ErrorMessage,
            .GameDisconnected = src.GameDisconnected,
            .AgentEnabled = src.AgentEnabled,
            .AgentState = src.AgentState,
            .AgentReason = src.AgentReason,
            .AgentGuardrailTriggered = src.AgentGuardrailTriggered,
            .PerformanceDiagnostics = src.PerformanceDiagnostics,
            .EngineRestartCount = src.EngineRestartCount,
            .EngineLastRestartUtc = src.EngineLastRestartUtc,
            .UpdatedAt = src.UpdatedAt
        }
    End Function

    Private Shared Function ResolveGameWindow(cfg As BotConfig) As IntPtr
        If cfg Is Nothing Then
            Return IntPtr.Zero
        End If

        If cfg.SelectedWindowHandle <> IntPtr.Zero Then
            Dim rc As NativeMethods.RECT
            If NativeMethods.IsWindowVisible(cfg.SelectedWindowHandle) AndAlso
               Not NativeMethods.IsIconic(cfg.SelectedWindowHandle) AndAlso
               NativeMethods.GetClientRect(cfg.SelectedWindowHandle, rc) Then
                Dim width As Integer = Math.Max(0, rc.Right - rc.Left)
                Dim height As Integer = Math.Max(0, rc.Bottom - rc.Top)
                If width > 0 AndAlso height > 0 Then
                    Return cfg.SelectedWindowHandle
                End If
            End If
        End If

        Return FindGameWindow(cfg.WindowTitle)
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
        Return TryGetClientScreenRect(hwnd, rect)
    End Function

    Public Shared Function TryGetClientScreenRect(cfg As BotConfig, ByRef rect As Rectangle) As Boolean
        Dim hwnd As IntPtr = ResolveGameWindow(cfg)
        Return TryGetClientScreenRect(hwnd, rect)
    End Function

    Private Shared Function TryGetClientScreenRect(hwnd As IntPtr, ByRef rect As Rectangle) As Boolean
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
        If hwnd = IntPtr.Zero Then
            Return Nothing
        End If

        Dim rc As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rc) Then
            Return Nothing
        End If

        Dim width As Integer = Math.Max(1, rc.Right - rc.Left)
        Dim height As Integer = Math.Max(1, rc.Bottom - rc.Top)
        Dim bmp As New Bitmap(width, height, PixelFormat.Format24bppRgb)

        Try
            If GetCaptureBackendPreferenceCode() = "wgc" AndAlso TryCaptureWithWindowsGraphicsCapture(hwnd, bmp, width, height) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.CopyFromScreen) Then
                Return bmp
            End If

            Dim cachedMethod As CaptureClientMethod
            If TryGetCachedCaptureMethod(hwnd, cachedMethod) AndAlso TryCaptureWithMethod(hwnd, bmp, width, height, cachedMethod) AndAlso AcceptCaptureIfUsable(hwnd, bmp, cachedMethod) Then
                Return bmp
            End If

            ClearCachedCaptureMethod(hwnd)

            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.PrintClientOnly) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_RENDERFULLCONTENT) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.PrintRenderFullContent) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY Or NativeMethods.PW_RENDERFULLCONTENT) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.PrintClientAndRenderFullContent) Then
                Return bmp
            End If

            If TryCaptureWithPrintWindow(hwnd, bmp, 0UI) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.PrintDefault) Then
                Return bmp
            End If

            If TryCaptureWithCopyFromScreen(hwnd, bmp, width, height) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.CopyFromScreen) Then
                Return bmp
            End If

            Thread.Sleep(10)
            If TryCaptureWithCopyFromScreen(hwnd, bmp, width, height) AndAlso AcceptCaptureIfUsable(hwnd, bmp, CaptureClientMethod.CopyFromScreen) Then
                Return bmp
            End If

            bmp.Dispose()
            Return Nothing
        Catch
            bmp.Dispose()
            Return Nothing
        End Try
    End Function

    Private Shared Function TryCaptureWithWindowsGraphicsCapture(hwnd As IntPtr, bmp As Bitmap, width As Integer, height As Integer) As Boolean
        ' Windows Graphics Capture requires a Direct3D interop capture session. This hook keeps
        ' the backend selectable while safely falling back to the cached GDI path on unsupported systems.
        Return False
    End Function

    Private Shared Function TryGetCachedCaptureMethod(hwnd As IntPtr, ByRef method As CaptureClientMethod) As Boolean
        SyncLock _captureMethodSync
            Return _captureMethodByWindow.TryGetValue(hwnd, method)
        End SyncLock
    End Function

    Private Shared Sub SetCachedCaptureMethod(hwnd As IntPtr, method As CaptureClientMethod)
        SyncLock _captureMethodSync
            _captureMethodByWindow(hwnd) = method
        End SyncLock
    End Sub

    Private Shared Sub ClearCachedCaptureMethod(hwnd As IntPtr)
        SyncLock _captureMethodSync
            _captureMethodByWindow.Remove(hwnd)
        End SyncLock
    End Sub

    Private Shared Function AcceptCaptureIfUsable(hwnd As IntPtr, bmp As Bitmap, method As CaptureClientMethod) As Boolean
        If bmp Is Nothing OrElse IsLikelyBlackFrame(bmp) Then
            ClearCachedCaptureMethod(hwnd)
            Return False
        End If

        SetCachedCaptureMethod(hwnd, method)
        Return True
    End Function

    Public Shared Function GetCachedCaptureMethodName(hwnd As IntPtr) As String
        Dim method As CaptureClientMethod
        If TryGetCachedCaptureMethod(hwnd, method) Then
            Return $"{GetCaptureBackendPreferenceName()}:{CaptureMethodName(method)}"
        End If
        Return $"{GetCaptureBackendPreferenceName()}:uncached"
    End Function

    Private Shared Sub SetCaptureBackendPreference(raw As String)
        Dim normalized As String = If(raw, "").Trim().ToLowerInvariant()
        If normalized <> "gdi" AndAlso normalized <> "wgc" Then
            normalized = "auto"
        End If
        SyncLock _captureMethodSync
            _captureBackendPreference = normalized
        End SyncLock
    End Sub

    Private Shared Function GetCaptureBackendPreferenceCode() As String
        SyncLock _captureMethodSync
            Return _captureBackendPreference
        End SyncLock
    End Function

    Private Shared Function GetCaptureBackendPreferenceName() As String
        Select Case GetCaptureBackendPreferenceCode()
            Case "gdi"
                Return "CachedGDI"
            Case "wgc"
                Return "WGCPreferred"
            Case Else
                Return "Auto"
        End Select
    End Function

    Private Shared Function CaptureMethodName(method As CaptureClientMethod) As String
        Select Case method
            Case CaptureClientMethod.PrintClientOnly
                Return "PrintWindow(PW_CLIENTONLY)"
            Case CaptureClientMethod.PrintRenderFullContent
                Return "PrintWindow(PW_RENDERFULLCONTENT)"
            Case CaptureClientMethod.PrintClientAndRenderFullContent
                Return "PrintWindow(PW_CLIENTONLY|PW_RENDERFULLCONTENT)"
            Case CaptureClientMethod.PrintDefault
                Return "PrintWindow(0)"
            Case CaptureClientMethod.CopyFromScreen
                Return "CopyFromScreen"
            Case Else
                Return "unknown"
        End Select
    End Function

    Public Shared Function RunPerformanceBenchmark(cfg As BotConfig, Optional iterations As Integer = 30) As String
        Dim hwnd As IntPtr = ResolveGameWindow(cfg)
        If hwnd = IntPtr.Zero Then
            Return "Benchmark failed: game window not found."
        End If

        SetCaptureBackendPreference(If(cfg?.CaptureBackendPreference, "auto"))
        Dim safeIterations As Integer = Math.Max(3, Math.Min(200, iterations))
        Dim captureTimes As New List(Of Double)()
        Dim regionTimes As New List(Of Double)()
        Dim hpValues As New List(Of Double)()
        Dim mpValues As New List(Of Double)()

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            Return "Benchmark failed: unable to read game client size."
        End If

        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
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
        Dim disconnectMessageRegion As New RectRegion(0, 0, 1, 1)
        Dim mapCoordinateXRegion As New RectRegion(0, 0, 1, 1)
        Dim mapCoordinateYRegion As New RectRegion(0, 0, 1, 1)
        Dim chatRegion As New RectRegion(0, 0, 1, 1)
        ResolveVisionRegions(If(cfg, BotConfig.CreateDefault()), clientWidth, clientHeight, hpRegion, mpRegion, mobNameRegion, mobHpRegion, unreachableTextRegion, pranaExpRegion, rupiahsRegion, partyInviteScanRegion, partyInviteOkRegion, partyListRegion, disconnectMessageRegion, mapCoordinateXRegion, mapCoordinateYRegion, chatRegion)

        For i As Integer = 1 To safeIterations
            Dim captureWatch As Stopwatch = Stopwatch.StartNew()
            Dim frame As Bitmap = CaptureClient(hwnd)
            captureWatch.Stop()
            captureTimes.Add(captureWatch.Elapsed.TotalMilliseconds)
            If frame IsNot Nothing Then
                frame.Dispose()
            End If

            Dim regionWatch As Stopwatch = Stopwatch.StartNew()
            Dim hpOk As Boolean = False
            Dim mpOk As Boolean = False
            hpValues.Add(ComputeClientBarPercent(hwnd, hpRegion, True, cfg, hpOk))
            mpValues.Add(ComputeClientBarPercent(hwnd, mpRegion, False, cfg, mpOk))
            regionWatch.Stop()
            regionTimes.Add(regionWatch.Elapsed.TotalMilliseconds)
        Next

        Dim report As String =
            $"Benchmark iterations: {safeIterations}{Environment.NewLine}" &
            $"Capture backend/method: {GetCachedCaptureMethodName(hwnd)}{Environment.NewLine}" &
            $"Full capture avg/max: {Average(captureTimes):0.0}/{MaxValue(captureTimes):0.0} ms{Environment.NewLine}" &
            $"HP/MP region avg/max: {Average(regionTimes):0.0}/{MaxValue(regionTimes):0.0} ms{Environment.NewLine}" &
            $"Last HP/MP sample: {If(hpValues.Count = 0, 0, hpValues(hpValues.Count - 1)):0.0}/{If(mpValues.Count = 0, 0, mpValues(mpValues.Count - 1)):0.0}%"
        Return report
    End Function

    Private Shared Function Average(values As List(Of Double)) As Double
        If values Is Nothing OrElse values.Count = 0 Then
            Return 0
        End If
        Return values.Average()
    End Function

    Private Shared Function MaxValue(values As List(Of Double)) As Double
        If values Is Nothing OrElse values.Count = 0 Then
            Return 0
        End If
        Return values.Max()
    End Function

    Private Shared Function TryCaptureWithMethod(hwnd As IntPtr, bmp As Bitmap, width As Integer, height As Integer, method As CaptureClientMethod) As Boolean
        Select Case method
            Case CaptureClientMethod.PrintClientOnly
                Return TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY)
            Case CaptureClientMethod.PrintRenderFullContent
                Return TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_RENDERFULLCONTENT)
            Case CaptureClientMethod.PrintClientAndRenderFullContent
                Return TryCaptureWithPrintWindow(hwnd, bmp, NativeMethods.PW_CLIENTONLY Or NativeMethods.PW_RENDERFULLCONTENT)
            Case CaptureClientMethod.PrintDefault
                Return TryCaptureWithPrintWindow(hwnd, bmp, 0UI)
            Case CaptureClientMethod.CopyFromScreen
                Return TryCaptureWithCopyFromScreen(hwnd, bmp, width, height)
            Case Else
                Return False
        End Select
    End Function

    Public Shared Function CaptureClientRegion(hwnd As IntPtr, region As RectRegion) As Bitmap
        If hwnd = IntPtr.Zero OrElse region Is Nothing Then
            Return Nothing
        End If

        Dim rc As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rc) Then
            Return Nothing
        End If

        Dim clientWidth As Integer = Math.Max(1, rc.Right - rc.Left)
        Dim clientHeight As Integer = Math.Max(1, rc.Bottom - rc.Top)
        Dim clamped As Rectangle = region.Clamp(clientWidth, clientHeight)
        If clamped.Width <= 0 OrElse clamped.Height <= 0 Then
            Return Nothing
        End If

        Using fullFrame As Bitmap = CaptureClient(hwnd)
            If fullFrame Is Nothing Then
                Return Nothing
            End If

            Return CropFrameRegion(fullFrame, clamped)
        End Using
    End Function

    Private Shared Function TryCaptureClientRegionWithBitBlt(hwnd As IntPtr, clamped As Rectangle, bmp As Bitmap) As Boolean
        Dim srcHdc As IntPtr = NativeMethods.GetDC(hwnd)
        If srcHdc = IntPtr.Zero Then
            Return False
        End If

        Using g As Graphics = Graphics.FromImage(bmp)
            Dim destHdc As IntPtr = g.GetHdc()
            Try
                Return NativeMethods.BitBlt(destHdc, 0, 0, clamped.Width, clamped.Height, srcHdc, clamped.X, clamped.Y, NativeMethods.SRCCOPY Or NativeMethods.CAPTUREBLT_ROP)
            Finally
                g.ReleaseHdc(destHdc)
                NativeMethods.ReleaseDC(hwnd, srcHdc)
            End Try
        End Using
    End Function

    Private Shared Function ComputeClientBarPercent(hwnd As IntPtr, region As RectRegion, isHp As Boolean, cfg As BotConfig, ByRef success As Boolean) As Double
        success = False
        Dim bmp As Bitmap = CaptureClientRegion(hwnd, region)
        If bmp Is Nothing Then
            Return 0
        End If

        Try
            Dim percent As Double = ComputeBarPercent(bmp, New RectRegion(0, 0, bmp.Width, bmp.Height), isHp, cfg)
            success = True
            Return percent
        Finally
            bmp.Dispose()
        End Try
    End Function

    Private Shared Function ComputeClientPotionPointPercent(frame As Bitmap, clientX As Integer, clientY As Integer, isHp As Boolean, cfg As BotConfig, sampleColorEnabled As Boolean, sampleColorArgb As Integer, ByRef success As Boolean) As Double
        success = False
        If frame Is Nothing Then
            Return 0
        End If

        Dim clientWidth As Integer = frame.Width
        Dim clientHeight As Integer = frame.Height
        If clientX < 0 OrElse clientY < 0 OrElse clientX >= clientWidth OrElse clientY >= clientHeight Then
            Return 0
        End If

        Dim profile As BarColorProfile = CreateBarColorProfile(isHp, cfg)
        If sampleColorEnabled Then
            Try
                Dim sampled As Color = Color.FromArgb(sampleColorArgb)
                profile.UseCustom = True
                profile.TargetR = sampled.R
                profile.TargetG = sampled.G
                profile.TargetB = sampled.B
                profile.Tolerance = Math.Max(10, Math.Min(70, If(cfg IsNot Nothing, cfg.BarColorTolerance, BotConfig.DefaultBarColorTolerance)))
            Catch
                sampleColorEnabled = False
            End Try
        End If

        Using buffer As New BitmapReadBuffer(frame)
            Dim matches As Integer = 0
            Dim validSamples As Integer = 0
            For y As Integer = Math.Max(0, clientY - 3) To Math.Min(clientHeight - 1, clientY + 3)
                For x As Integer = Math.Max(0, clientX - 3) To Math.Min(clientWidth - 1, clientX + 3)
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    validSamples += 1
                    If IsBarColorRgb(r, g, b, profile) Then
                        matches += 1
                    End If
                Next
            Next

            If validSamples <= 0 Then
                Return 0
            End If

            success = True
            Return If(matches >= Math.Max(2, CInt(Math.Ceiling(validSamples * 0.12))), 100.0, 0.0)
        End Using
    End Function

    Private Shared Function TryCaptureWithPrintWindow(hwnd As IntPtr, bmp As Bitmap, flags As UInteger) As Boolean
        If flags = NativeMethods.PW_CLIENTONLY Then
            Dim clientOnlyOk As Boolean = False
            Using g As Graphics = Graphics.FromImage(bmp)
                Dim hdc As IntPtr = g.GetHdc()
                Try
                    clientOnlyOk = NativeMethods.PrintWindow(hwnd, hdc, flags)
                Finally
                    g.ReleaseHdc(hdc)
                End Try
            End Using
            Return clientOnlyOk AndAlso (Not IsLikelyBlackFrame(bmp))
        End If

        Dim windowRect As NativeMethods.RECT
        Dim clientRect As NativeMethods.RECT
        Dim clientOrigin As New NativeMethods.POINT With {.X = 0, .Y = 0}
        If Not NativeMethods.GetWindowRect(hwnd, windowRect) OrElse
           Not NativeMethods.GetClientRect(hwnd, clientRect) OrElse
           Not NativeMethods.ClientToScreen(hwnd, clientOrigin) Then
            Return False
        End If

        Dim outerWidth As Integer = Math.Max(bmp.Width, windowRect.Right - windowRect.Left)
        Dim outerHeight As Integer = Math.Max(bmp.Height, windowRect.Bottom - windowRect.Top)
        Dim offsetX As Integer = clientOrigin.X - windowRect.Left
        Dim offsetY As Integer = clientOrigin.Y - windowRect.Top
        If offsetX < 0 OrElse offsetY < 0 OrElse
           offsetX + bmp.Width > outerWidth OrElse offsetY + bmp.Height > outerHeight Then
            Return False
        End If

        Using outer As New Bitmap(outerWidth, outerHeight, PixelFormat.Format24bppRgb)
            Dim ok As Boolean = False
            Using g As Graphics = Graphics.FromImage(outer)
                Dim hdc As IntPtr = g.GetHdc()
                Try
                    ok = NativeMethods.PrintWindow(hwnd, hdc, flags)
                Finally
                    g.ReleaseHdc(hdc)
                End Try
            End Using
            If Not ok OrElse IsLikelyBlackFrame(outer) Then
                Return False
            End If

            Using g As Graphics = Graphics.FromImage(bmp)
                g.DrawImage(
                    outer,
                    New Rectangle(0, 0, bmp.Width, bmp.Height),
                    New Rectangle(offsetX, offsetY, bmp.Width, bmp.Height),
                    GraphicsUnit.Pixel)
            End Using
        End Using
        Return Not IsLikelyBlackFrame(bmp)
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

    Private NotInheritable Class BitmapReadBuffer
        Implements IDisposable

        Private ReadOnly _bmp As Bitmap
        Private ReadOnly _data As BitmapData
        Private ReadOnly _bytes As Byte()

        Public ReadOnly Property Width As Integer
        Public ReadOnly Property Height As Integer
        Public ReadOnly Property Stride As Integer

        Public Sub New(bmp As Bitmap)
            _bmp = bmp
            Width = bmp.Width
            Height = bmp.Height
            _data = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            Stride = _data.Stride
            Dim length As Integer = Math.Abs(Stride) * Height
            _bytes = ArrayPool(Of Byte).Shared.Rent(length)
            Marshal.Copy(_data.Scan0, _bytes, 0, length)
        End Sub

        Public Sub GetRgb(x As Integer, y As Integer, ByRef r As Integer, ByRef g As Integer, ByRef b As Integer)
            Dim row As Integer = If(Stride >= 0, y * Stride, (Height - 1 - y) * Math.Abs(Stride))
            Dim index As Integer = row + (x * 3)
            b = _bytes(index)
            g = _bytes(index + 1)
            r = _bytes(index + 2)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _bmp.UnlockBits(_data)
            ArrayPool(Of Byte).Shared.Return(_bytes)
        End Sub
    End Class

    Private Structure BarColorProfile
        Public Property IsHp As Boolean
        Public Property UseCustom As Boolean
        Public Property TargetR As Integer
        Public Property TargetG As Integer
        Public Property TargetB As Integer
        Public Property Tolerance As Integer
    End Structure

    Private Shared Function IsLikelyBlackFrame(bmp As Bitmap) As Boolean
        Dim stepX As Integer = Math.Max(1, bmp.Width \ 10)
        Dim stepY As Integer = Math.Max(1, bmp.Height \ 10)
        Dim samples As Integer = 0
        Dim darkSamples As Integer = 0
        Dim sumLuma As Long = 0

        Using buffer As New BitmapReadBuffer(bmp)
            For y As Integer = 0 To bmp.Height - 1 Step stepY
                For x As Integer = 0 To bmp.Width - 1 Step stepX
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    samples += 1
                    Dim luma As Integer = (r * 30 + g * 59 + b * 11) \ 100
                    sumLuma += luma
                    If luma <= 8 Then
                        darkSamples += 1
                    End If
                Next
            Next
        End Using

        If samples = 0 Then
            Return True
        End If

        Dim darkRatio As Double = darkSamples / CDbl(samples)
        Dim avgLuma As Double = sumLuma / CDbl(samples)
        Return darkRatio >= 0.96 AndAlso avgLuma <= 10.0
    End Function

    Private Shared Function ComputeVisualSignature(bmp As Bitmap) As ULong
        If bmp Is Nothing OrElse bmp.Width <= 0 OrElse bmp.Height <= 0 Then
            Return 0UL
        End If

        Dim stepX As Integer = Math.Max(1, bmp.Width \ 48)
        Dim stepY As Integer = Math.Max(1, bmp.Height \ 24)
        Dim hash As ULong = 1469598103934665603UL
        Using buffer As New BitmapReadBuffer(bmp)
            For y As Integer = 0 To bmp.Height - 1 Step stepY
                For x As Integer = 0 To bmp.Width - 1 Step stepX
                    Dim r As Integer = 0
                    Dim g As Integer = 0
                    Dim b As Integer = 0
                    buffer.GetRgb(x, y, r, g, b)
                    Dim luma As Integer = (r * 30 + g * 59 + b * 11) \ 100
                    hash = MixVisualHash(hash, CULng(luma And &HFF))
                    hash = MixVisualHash(hash, CULng((r \ 16) And &HF))
                    hash = MixVisualHash(hash, CULng((g \ 16) And &HF))
                    hash = MixVisualHash(hash, CULng((b \ 16) And &HF))
                Next
            Next
        End Using
        Return hash
    End Function

    Private Shared Function MixVisualHash(hash As ULong, value As ULong) As ULong
        Dim mixed As ULong = hash Xor (value And &HFFFFUL)
        Dim rotate7 As ULong = (mixed << 7) Or (mixed >> 57)
        Dim rotate17 As ULong = (mixed << 17) Or (mixed >> 47)
        Return rotate7 Xor rotate17 Xor 1099511628211UL
    End Function

    Private Shared Function ThresholdLumaBitmap(source As Bitmap, threshold As Integer, Optional invert As Boolean = False) As Bitmap
        Dim output As New Bitmap(Math.Max(1, source.Width), Math.Max(1, source.Height), PixelFormat.Format24bppRgb)
        Using src As New BitmapReadBuffer(source)
            Dim rect As New Rectangle(0, 0, output.Width, output.Height)
            Dim data As BitmapData = Nothing
            Dim bytes As Byte() = Nothing
            Try
                data = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
                Dim length As Integer = Math.Abs(data.Stride) * output.Height
                bytes = ArrayPool(Of Byte).Shared.Rent(length)
                For y As Integer = 0 To output.Height - 1
                    Dim row As Integer = If(data.Stride >= 0, y * data.Stride, (output.Height - 1 - y) * Math.Abs(data.Stride))
                    For x As Integer = 0 To output.Width - 1
                        Dim r As Integer = 0
                        Dim g As Integer = 0
                        Dim b As Integer = 0
                        src.GetRgb(x, y, r, g, b)
                        Dim luma As Integer = (r * 30 + g * 59 + b * 11) \ 100
                        Dim isLight As Boolean = luma >= threshold
                        If invert Then
                            isLight = Not isLight
                        End If
                        Dim value As Byte = If(isLight, CByte(255), CByte(0))
                        Dim index As Integer = row + (x * 3)
                        bytes(index) = value
                        bytes(index + 1) = value
                        bytes(index + 2) = value
                    Next
                Next
                Marshal.Copy(bytes, 0, data.Scan0, length)
            Finally
                If data IsNot Nothing Then
                    output.UnlockBits(data)
                End If
                If bytes IsNot Nothing Then
                    ArrayPool(Of Byte).Shared.Return(bytes)
                End If
            End Try
        End Using
        Return output
    End Function

    Private Shared Function ComputeBarPercent(frame As Bitmap, region As RectRegion, isHp As Boolean, Optional cfg As BotConfig = Nothing) As Double
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

        Dim profile As BarColorProfile = CreateBarColorProfile(isHp, cfg)
        Using buffer As New BitmapReadBuffer(frame)
            Return ComputeBarPercent(buffer, rect, profile)
        End Using
    End Function

    Private Shared Function ComputeMobHpPercent(frame As Bitmap, region As RectRegion, cfg As BotConfig) As Double
        Dim genericPercent As Double = ComputeBarPercent(frame, region, True, Nothing)
        If cfg Is Nothing OrElse Not cfg.CustomBarColorsEnabled Then
            Return genericPercent
        End If

        Return Math.Max(genericPercent, ComputeBarPercent(frame, region, True, cfg))
    End Function

    Private Shared Function ComputeBarPercent(buffer As BitmapReadBuffer, rect As Rectangle, profile As BarColorProfile) As Double
        Dim leadingEdgeRatio As Double = ComputeLeadingEdgeFillRatio(buffer, rect, profile)

        Dim columnMinPixels As Integer = Math.Max(1, CInt(Math.Ceiling(rect.Height * 0.1)))
        Dim gapTolerance As Integer = Math.Max(2, CInt(Math.Ceiling(rect.Width * 0.02)))
        Dim rightMost As Integer = -1
        Dim activeStarted As Boolean = False
        Dim gapCount As Integer = 0

        For x As Integer = 0 To rect.Width - 1
            Dim colored As Integer = 0
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim r As Integer = 0
                Dim g As Integer = 0
                Dim b As Integer = 0
                buffer.GetRgb(px, y, r, g, b)
                If IsBarColorRgb(r, g, b, profile) Then
                    colored += 1
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
            Return ComputeBarPercentAdaptive(buffer, rect, profile)
        End If

        Dim colorPercent As Double = Math.Max(0, Math.Min(100, (rightMost + 1) * 100.0 / rect.Width))
        If colorPercent >= 3.0 AndAlso leadingEdgeRatio < 0.02 Then
            Return 0
        End If
        If colorPercent < 2.0 Then
            Dim adaptive As Double = ComputeBarPercentAdaptive(buffer, rect, profile)
            If adaptive > colorPercent Then
                Return adaptive
            End If
        End If
        Return colorPercent
    End Function

    Private Shared Function ComputeBarPercentAdaptive(frame As Bitmap, rect As Rectangle, isHp As Boolean, Optional cfg As BotConfig = Nothing) As Double
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim profile As BarColorProfile = CreateBarColorProfile(isHp, cfg)
        Using buffer As New BitmapReadBuffer(frame)
            Return ComputeBarPercentAdaptive(buffer, rect, profile)
        End Using
    End Function

    Private Shared Function ComputeBarPercentAdaptive(buffer As BitmapReadBuffer, rect As Rectangle, profile As BarColorProfile) As Double
        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim leadingEdgeRatio As Double = ComputeLeadingEdgeFillRatio(buffer, rect, profile)

        Dim scores(rect.Width - 1) As Long
        Dim maxScore As Long = 0

        For x As Integer = 0 To rect.Width - 1
            Dim score As Long = 0
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim r As Integer = 0
                Dim g As Integer = 0
                Dim b As Integer = 0
                buffer.GetRgb(px, y, r, g, b)
                score += GetBarColorScoreRgb(r, g, b, profile)
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

    Private Shared Function ComputeLeadingEdgeFillRatio(frame As Bitmap, rect As Rectangle, isHp As Boolean, Optional cfg As BotConfig = Nothing) As Double
        If frame Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim profile As BarColorProfile = CreateBarColorProfile(isHp, cfg)
        Using buffer As New BitmapReadBuffer(frame)
            Return ComputeLeadingEdgeFillRatio(buffer, rect, profile)
        End Using
    End Function

    Private Shared Function ComputeLeadingEdgeFillRatio(buffer As BitmapReadBuffer, rect As Rectangle, profile As BarColorProfile) As Double
        Dim edgeCols As Integer = Math.Max(2, Math.Min(rect.Width, CInt(Math.Ceiling(rect.Width * 0.12))))
        Dim colored As Integer = 0
        Dim total As Integer = edgeCols * rect.Height
        If total <= 0 Then
            Return 0
        End If

        For x As Integer = 0 To edgeCols - 1
            Dim px As Integer = rect.Left + x
            For y As Integer = rect.Top To rect.Bottom - 1
                Dim r As Integer = 0
                Dim g As Integer = 0
                Dim b As Integer = 0
                buffer.GetRgb(px, y, r, g, b)
                If IsBarColorRgb(r, g, b, profile) Then
                    colored += 1
                End If
            Next
        Next

        Return colored / CDbl(total)
    End Function

    Private Shared Function HasTargetWindowSignal(frame As Bitmap, mobHpRegion As RectRegion, mobName As String, mobHpPct As Double, cfg As BotConfig) As Boolean
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

        Dim edgeFill As Double = ComputeLeadingEdgeFillRatio(frame, rect, True, Nothing)
        Dim colorFill As Double = ComputeColorFillRatio(frame, rect, True, Nothing)
        If cfg IsNot Nothing AndAlso cfg.CustomBarColorsEnabled Then
            edgeFill = Math.Max(edgeFill, ComputeLeadingEdgeFillRatio(frame, rect, True, cfg))
            colorFill = Math.Max(colorFill, ComputeColorFillRatio(frame, rect, True, cfg))
        End If
        Dim hasName As Boolean = Not String.IsNullOrWhiteSpace(mobName)

        If edgeFill >= 0.04 AndAlso colorFill >= 0.01 Then
            Return True
        End If

        If hasName AndAlso mobHpPct > 0.0 AndAlso edgeFill >= 0.015 AndAlso colorFill >= 0.004 Then
            Return True
        End If

        Return False
    End Function

    Private Shared Function ComputeColorFillRatio(frame As Bitmap, rect As Rectangle, isHp As Boolean, Optional cfg As BotConfig = Nothing) As Double
        If frame Is Nothing OrElse rect.Width <= 0 OrElse rect.Height <= 0 Then
            Return 0
        End If

        Dim profile As BarColorProfile = CreateBarColorProfile(isHp, cfg)
        Using buffer As New BitmapReadBuffer(frame)
            Return ComputeColorFillRatio(buffer, rect, profile)
        End Using
    End Function

    Private Shared Function ComputeColorFillRatio(buffer As BitmapReadBuffer, rect As Rectangle, profile As BarColorProfile) As Double
        Dim colored As Integer = 0
        Dim total As Integer = rect.Width * rect.Height
        If total <= 0 Then
            Return 0
        End If

        For y As Integer = rect.Top To rect.Bottom - 1
            For x As Integer = rect.Left To rect.Right - 1
                Dim r As Integer = 0
                Dim g As Integer = 0
                Dim b As Integer = 0
                buffer.GetRgb(x, y, r, g, b)
                If IsBarColorRgb(r, g, b, profile) Then
                    colored += 1
                End If
            Next
        Next

        Return colored / CDbl(total)
    End Function

    Private Shared Sub ResolveVisionRegions(cfg As BotConfig, frameWidth As Integer, frameHeight As Integer, ByRef hpBar As RectRegion, ByRef mpBar As RectRegion, ByRef mobNameRect As RectRegion, ByRef mobHpRect As RectRegion, ByRef unreachableTextRect As RectRegion, ByRef pranaExpRect As RectRegion, ByRef rupiahsRect As RectRegion, ByRef partyInviteScanRect As RectRegion, ByRef partyInviteOkRect As RectRegion, ByRef partyListRect As RectRegion, ByRef disconnectMessageRect As RectRegion, ByRef mapCoordinateXRect As RectRegion, ByRef mapCoordinateYRect As RectRegion, ByRef chatRect As RectRegion)
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
        disconnectMessageRect = CloneRegion(cfg.DisconnectMessageRect)
        mapCoordinateXRect = CloneRegion(GetEffectiveMapCoordinateXRect(cfg))
        mapCoordinateYRect = CloneRegion(GetEffectiveMapCoordinateYRect(cfg))
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
        ' The redesigned HUD keeps these panels at a fixed top-left pixel size.
        hpBar = CloneRegion(cfg.HpBar)
        mpBar = CloneRegion(cfg.MpBar)
        mobNameRect = CloneRegion(cfg.MobNameRect)
        mobHpRect = CloneRegion(cfg.MobHpRect)
        unreachableTextRect = ScaleRegionLeftTop(cfg.UnreachableTextRect, sx, sy)
        pranaExpRect = ScaleRegionLeftTop(cfg.PranaExpRect, sx, sy)
        rupiahsRect = ScaleRegionLeftTop(cfg.RupiahsRect, sx, sy)
        partyInviteScanRect = ScaleRegionLeftTop(cfg.PartyInviteScanRect, sx, sy)
        partyInviteOkRect = ScaleRegionLeftTop(cfg.PartyInviteOkRect, sx, sy)
        partyListRect = ScaleRegionLeftTop(cfg.PartyListRect, sx, sy)
        disconnectMessageRect = ScaleRegionLeftTop(cfg.DisconnectMessageRect, sx, sy)
        mapCoordinateXRect = ScaleRegionLeftTop(GetEffectiveMapCoordinateXRect(cfg), sx, sy)
        mapCoordinateYRect = ScaleRegionLeftTop(GetEffectiveMapCoordinateYRect(cfg), sx, sy)
        chatRect = ScaleRegionLeftTop(cfg.ChatRect, sx, sy)
    End Sub

    Private Shared Function ResolveMobLifeRegion(cfg As BotConfig, frameWidth As Integer, frameHeight As Integer) As RectRegion
        Dim source As RectRegion = If(cfg Is Nothing OrElse cfg.MobLifeRect Is Nothing, BotConfig.DefaultMobHpRect(), cfg.MobLifeRect)
        If frameWidth <= 0 OrElse frameHeight <= 0 Then
            Return CloneRegion(source)
        End If
        If cfg Is Nothing OrElse
           SameRegion(source, BotConfig.DefaultMobHpRect()) OrElse
           Not IsDefaultVisionLayout(cfg) OrElse
           (frameWidth = BaseClientWidth AndAlso frameHeight = BaseClientHeight) Then
            Return CloneRegion(source)
        End If

        Dim sx As Double = frameWidth / CDbl(BaseClientWidth)
        Dim sy As Double = frameHeight / CDbl(BaseClientHeight)
        Return ScaleRegionRightTop(source, sx, sy, frameWidth)
    End Function

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

    Private Shared Function GetEffectiveMapCoordinateXRect(cfg As BotConfig) As RectRegion
        If cfg Is Nothing Then
            Return BotConfig.DefaultMapCoordinateXRect()
        End If
        If UsesLegacyMapCoordinateRect(cfg) Then
            Return BotConfig.SplitMapCoordinateRect(cfg.MapCoordinateRect, True)
        End If
        If cfg.MapCoordinateXRect Is Nothing Then
            Return BotConfig.DefaultMapCoordinateXRect()
        End If
        Return cfg.MapCoordinateXRect
    End Function

    Private Shared Function GetEffectiveMapCoordinateYRect(cfg As BotConfig) As RectRegion
        If cfg Is Nothing Then
            Return BotConfig.DefaultMapCoordinateYRect()
        End If
        If UsesLegacyMapCoordinateRect(cfg) Then
            Return BotConfig.SplitMapCoordinateRect(cfg.MapCoordinateRect, False)
        End If
        If cfg.MapCoordinateYRect Is Nothing Then
            Return BotConfig.DefaultMapCoordinateYRect()
        End If
        Return cfg.MapCoordinateYRect
    End Function

    Private Shared Function UsesLegacyMapCoordinateRect(cfg As BotConfig) As Boolean
        If cfg Is Nothing OrElse cfg.MapCoordinateRect Is Nothing Then
            Return False
        End If

        Dim xMissingOrDefault As Boolean = cfg.MapCoordinateXRect Is Nothing OrElse SameRegion(cfg.MapCoordinateXRect, BotConfig.DefaultMapCoordinateXRect())
        Dim yMissingOrDefault As Boolean = cfg.MapCoordinateYRect Is Nothing OrElse SameRegion(cfg.MapCoordinateYRect, BotConfig.DefaultMapCoordinateYRect())
        Return xMissingOrDefault AndAlso yMissingOrDefault AndAlso Not SameRegion(cfg.MapCoordinateRect, BotConfig.DefaultMapCoordinateRect())
    End Function

    Private Shared Function IsDefaultVisionLayout(cfg As BotConfig) As Boolean
        Return SameRegion(cfg.HpBar, BotConfig.DefaultHpBarRect()) AndAlso
               SameRegion(cfg.MpBar, BotConfig.DefaultMpBarRect()) AndAlso
               SameRegion(cfg.MobNameRect, BotConfig.DefaultMobNameRect()) AndAlso
               SameRegion(cfg.MobHpRect, BotConfig.DefaultMobHpRect()) AndAlso
               SameRegion(cfg.MobLifeRect, BotConfig.DefaultMobHpRect()) AndAlso
               SameRegion(cfg.UnreachableTextRect, New RectRegion(15, 582, 128, 22)) AndAlso
               SameRegion(cfg.PranaExpRect, New RectRegion(472, 745, 78, 21)) AndAlso
               SameRegion(cfg.RupiahsRect, New RectRegion(560, 745, 110, 21)) AndAlso
               SameRegion(cfg.PartyInviteScanRect, New RectRegion(349, 318, 328, 124)) AndAlso
               SameRegion(cfg.PartyInviteOkRect, New RectRegion(463, 410, 59, 21)) AndAlso
               SameRegion(cfg.PartyListRect, New RectRegion(0, 24, 168, 244)) AndAlso
               SameRegion(cfg.DisconnectMessageRect, BotConfig.DefaultDisconnectMessageRect()) AndAlso
               SameRegion(cfg.DisconnectOkRect, BotConfig.DefaultDisconnectOkRect()) AndAlso
               SameRegion(cfg.MapCoordinateRect, BotConfig.DefaultMapCoordinateRect()) AndAlso
               SameRegion(GetEffectiveMapCoordinateXRect(cfg), BotConfig.DefaultMapCoordinateXRect()) AndAlso
               SameRegion(GetEffectiveMapCoordinateYRect(cfg), BotConfig.DefaultMapCoordinateYRect()) AndAlso
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

    Private Shared Function CreateBarColorProfile(isHp As Boolean, cfg As BotConfig) As BarColorProfile
        Dim profile As New BarColorProfile With {
            .IsHp = isHp,
            .UseCustom = False,
            .TargetR = 0,
            .TargetG = 0,
            .TargetB = 0,
            .Tolerance = BotConfig.DefaultBarColorTolerance
        }

        If cfg Is Nothing OrElse Not cfg.CustomBarColorsEnabled Then
            Return profile
        End If

        Dim target As Color
        Try
            target = Color.FromArgb(If(isHp, cfg.HpBarColorArgb, cfg.MpBarColorArgb))
        Catch
            target = Color.FromArgb(If(isHp, BotConfig.DefaultHpBarColorArgb(), BotConfig.DefaultMpBarColorArgb()))
        End Try

        profile.UseCustom = True
        profile.TargetR = target.R
        profile.TargetG = target.G
        profile.TargetB = target.B
        profile.Tolerance = Math.Max(8, Math.Min(120, cfg.BarColorTolerance))
        Return profile
    End Function

    Private Shared Function IsBarColorRgb(r As Integer, g As Integer, b As Integer, profile As BarColorProfile) As Boolean
        If Not profile.UseCustom Then
            Return If(profile.IsHp, IsHpColorRgb(r, g, b), IsMpColorRgb(r, g, b))
        End If

        Return IsCustomBarColorRgb(r, g, b, profile)
    End Function

    Private Shared Function GetBarColorScoreRgb(r As Integer, g As Integer, b As Integer, profile As BarColorProfile) As Integer
        If Not profile.UseCustom Then
            Dim dominance As Integer
            If profile.IsHp Then
                dominance = r - ((g + b) \ 2)
            Else
                dominance = b - ((r + g) \ 2)
            End If
            Return Math.Max(0, dominance)
        End If

        If Not IsCustomBarColorRgb(r, g, b, profile) Then
            Return 0
        End If

        Dim rgbDelta As Integer =
            Math.Abs(r - profile.TargetR) +
            Math.Abs(g - profile.TargetG) +
            Math.Abs(b - profile.TargetB)
        Return Math.Max(1, 255 - Math.Min(255, rgbDelta \ 2))
    End Function

    Private Shared Function IsCustomBarColorRgb(r As Integer, g As Integer, b As Integer, profile As BarColorProfile) As Boolean
        Dim pxMax As Integer = Math.Max(r, Math.Max(g, b))
        Dim targetMax As Integer = Math.Max(profile.TargetR, Math.Max(profile.TargetG, profile.TargetB))
        If targetMax <= 0 OrElse pxMax <= 0 Then
            Return False
        End If

        Dim intensityFloor As Integer = Math.Max(10, Math.Min(70, targetMax \ 5))
        If pxMax < intensityFloor Then
            Return False
        End If

        Dim dr As Integer = Math.Abs(r - profile.TargetR)
        Dim dg As Integer = Math.Abs(g - profile.TargetG)
        Dim db As Integer = Math.Abs(b - profile.TargetB)
        Dim maxDelta As Integer = Math.Max(dr, Math.Max(dg, db))
        Dim totalDelta As Integer = dr + dg + db
        If maxDelta <= profile.Tolerance AndAlso totalDelta <= CInt(Math.Round(profile.Tolerance * 2.6R)) Then
            Return True
        End If

        Dim normTolerance As Integer = Math.Max(18, Math.Min(95, profile.Tolerance + 18))
        Dim sr As Integer = CInt(Math.Round(r * 255.0R / pxMax))
        Dim sg As Integer = CInt(Math.Round(g * 255.0R / pxMax))
        Dim sb As Integer = CInt(Math.Round(b * 255.0R / pxMax))
        Dim tr As Integer = CInt(Math.Round(profile.TargetR * 255.0R / targetMax))
        Dim tg As Integer = CInt(Math.Round(profile.TargetG * 255.0R / targetMax))
        Dim tb As Integer = CInt(Math.Round(profile.TargetB * 255.0R / targetMax))

        Dim nr As Integer = Math.Abs(sr - tr)
        Dim ng As Integer = Math.Abs(sg - tg)
        Dim nb As Integer = Math.Abs(sb - tb)
        Dim normalizedMaxDelta As Integer = Math.Max(nr, Math.Max(ng, nb))
        Dim normalizedTotalDelta As Integer = nr + ng + nb
        Return normalizedMaxDelta <= normTolerance AndAlso
               normalizedTotalDelta <= CInt(Math.Round(normTolerance * 2.4R))
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

    Private Shared Function IsHpColorRgb(r As Integer, g As Integer, b As Integer) As Boolean
        Return IsHpColor(Color.FromArgb(r, g, b))
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

    Private Shared Function IsMpColorRgb(r As Integer, g As Integer, b As Integer) As Boolean
        Return IsMpColor(Color.FromArgb(r, g, b))
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

    Public Shared Function DoubleRightClickClientPoint(hwnd As IntPtr, x As Integer, y As Integer, Optional moveDelayMs As Integer = 10, Optional downUpDelayMs As Integer = 35, Optional clickGapMs As Integer = 90) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim lParam As Integer = (x And &HFFFF) Or ((y And &HFFFF) << 16)
        Try
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_MOUSEMOVE), IntPtr.Zero, New IntPtr(lParam))
            If moveDelayMs > 0 Then
                Thread.Sleep(moveDelayMs)
            End If

            For i As Integer = 0 To 1
                NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_RBUTTONDOWN), New IntPtr(NativeMethods.MK_RBUTTON), New IntPtr(lParam))
                If downUpDelayMs > 0 Then
                    Thread.Sleep(downUpDelayMs)
                End If
                NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_RBUTTONUP), IntPtr.Zero, New IntPtr(lParam))
                If i = 0 AndAlso clickGapMs > 0 Then
                    Thread.Sleep(clickGapMs)
                End If
            Next

            Return True
        Catch
            Return False
        End Try
    End Function

    Private Function TryExecuteLootNameAutoPickup(hwnd As IntPtr, cfg As BotConfig, matchedItem As String, matchedRegion As OcrReader.OcrTextRegion, lootScanBounds As Rectangle) As Boolean
        If hwnd = IntPtr.Zero OrElse cfg Is Nothing OrElse Not cfg.LootNameAutoPickupEnabled Then
            Return False
        End If

        Dim clientRect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, clientRect) Then
            RaiseEvent LogLine("Loot auto-pick skipped: game client rect unavailable.")
            Return False
        End If

        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
        If matchedRegion Is Nothing OrElse matchedRegion.Bounds = Rectangle.Empty Then
            RaiseEvent LogLine($"Loot auto-pick skipped for {matchedItem}: OCR did not return a loot label position.")
            Return False
        End If

        Dim clientX As Integer = lootScanBounds.X + matchedRegion.Bounds.X + (matchedRegion.Bounds.Width \ 2) + cfg.LootNamePickupOffsetX
        Dim clientY As Integer = lootScanBounds.Y + matchedRegion.Bounds.Bottom + cfg.LootNamePickupOffsetY
        clientX = Math.Max(0, Math.Min(clientWidth - 1, clientX))
        clientY = Math.Max(0, Math.Min(clientHeight - 1, clientY))

        Dim screenPoint As NativeMethods.POINT
        If Not TryMapClientPointToScreen(hwnd, clientX, clientY, screenPoint) Then
            RaiseEvent LogLine($"Loot auto-pick skipped for {matchedItem}: failed to map client point to screen.")
            Return False
        End If

        Dim hadCursor As Boolean = False
        Dim previousCursor As NativeMethods.POINT
        Try
            hadCursor = NativeMethods.GetCursorPos(previousCursor)
        Catch
            hadCursor = False
        End Try

        Try
            NativeMethods.SetForegroundWindow(hwnd)
            Thread.Sleep(ForegroundInputSettleMs)

            If Not NativeMethods.SetCursorPos(screenPoint.X, screenPoint.Y) Then
                RaiseEvent LogLine($"Loot auto-pick skipped for {matchedItem}: SetCursorPos failed.")
                Return False
            End If

            Thread.Sleep(10)
            NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, CUInt(screenPoint.X), CUInt(screenPoint.Y), 0UI, UIntPtr.Zero)
            Thread.Sleep(Math.Max(0, cfg.LootNamePickupMouseHoldMs))
            NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, CUInt(screenPoint.X), CUInt(screenPoint.Y), 0UI, UIntPtr.Zero)

            Dim waitBeforeFMs As Integer = Math.Max(0, cfg.LootNamePickupClickDelayMs)
            If waitBeforeFMs > 0 Then
                Thread.Sleep(waitBeforeFMs)
            End If

            Dim fCount As Integer = Math.Max(1, cfg.LootNamePickupFPressCount)
            Dim gapMs As Integer = Math.Max(0, cfg.LootNamePickupFPressGapMs)
            Dim sentAny As Boolean = False
            For pressIndex As Integer = 1 To fCount
                If SendKey(hwnd, "F", FastKeyPressMs) Then
                    sentAny = True
                End If
                If pressIndex < fCount AndAlso gapMs > 0 Then
                    Thread.Sleep(gapMs)
                End If
            Next

            If sentAny Then
                SetLastAction($"Loot auto-pick ({matchedItem})")
                RaiseEvent LogLine($"Loot auto-pick clicked matched label '{matchedItem}' at client {clientX},{clientY} -> screen {screenPoint.X},{screenPoint.Y}, then pressed F x{fCount}.")
            Else
                RaiseEvent LogLine($"Loot auto-pick click completed for {matchedItem}, but F presses were not sent.")
            End If
            Return sentAny
        Catch ex As Exception
            RaiseEvent LogLine($"Loot auto-pick failed for {matchedItem}: {ex.Message}")
            Return False
        Finally
            If cfg.LootNamePickupRestoreCursor AndAlso hadCursor Then
                Try
                    NativeMethods.SetCursorPos(previousCursor.X, previousCursor.Y)
                Catch
                End Try
            End If
        End Try
    End Function

    Private Shared Function TryMapClientPointToScreen(hwnd As IntPtr, clientX As Integer, clientY As Integer, ByRef screenPoint As NativeMethods.POINT) As Boolean
        screenPoint = New NativeMethods.POINT With {.X = 0, .Y = 0}
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim pt As New NativeMethods.POINT With {.X = clientX, .Y = clientY}
        If Not NativeMethods.ClientToScreen(hwnd, pt) Then
            Return False
        End If

        screenPoint = pt
        Return True
    End Function

    Private Sub TryQueueHardcodedVisionStats(cfg As BotConfig, hwnd As IntPtr, now As DateTime, frame As Bitmap, hpRegion As RectRegion, mpRegion As RectRegion, hpPercent As Double, mpPercent As Double, mobName As String)
        If cfg Is Nothing OrElse hwnd = IntPtr.Zero Then
            Return
        End If

        If Double.IsNaN(hpPercent) OrElse Double.IsNaN(mpPercent) Then
            Return
        End If

        Dim sendNow As Boolean = False
        SyncLock _sync
            If _hardcodedVisionStatsInFlight Then
                Return
            End If

            If Not _hardcodedVisionStatsInitialSent Then
                sendNow = True
            ElseIf _lastHardcodedVisionStatsSentAt = DateTime.MinValue OrElse
                   (now - _lastHardcodedVisionStatsSentAt).TotalMinutes >= HardcodedVisionStatsIntervalMinutes Then
                sendNow = True
            End If

            If sendNow Then
                _hardcodedVisionStatsInitialSent = True
                _lastHardcodedVisionStatsSentAt = now
                _hardcodedVisionStatsInFlight = True
            End If
        End SyncLock

        If Not sendNow Then
            Return
        End If

        Dim actualWindowTitle As String = GetWindowTitle(hwnd)
        If String.IsNullOrWhiteSpace(actualWindowTitle) Then
            actualWindowTitle = If(cfg.WindowTitle, "").Trim()
        End If

        Dim characterNameFromOcr As String = ReadCharacterInfoFromHpBar(frame, hpRegion)
        Dim titleCharacterName As String = ExtractCharacterNameFromWindowTitle(actualWindowTitle, cfg.WindowTitle)
        Dim characterName As String = ResolveVisionStatsCharacterName(characterNameFromOcr, titleCharacterName, actualWindowTitle, cfg.WindowTitle)

        SyncLock _sync
            _lastCharacterName = If(IsKnownCharacterName(characterName), characterName, "")
        End SyncLock

        Dim hpNumbersText As String = ReadBarNumbersFromRegion(frame, hpRegion)
        Dim mpNumbersText As String = ReadBarNumbersFromRegion(frame, mpRegion)
        Dim visionMobName As String = If(String.IsNullOrWhiteSpace(mobName), "none", mobName.Trim())
        Dim body As String =
            $"Window Title: {If(String.IsNullOrWhiteSpace(actualWindowTitle), "unknown", actualWindowTitle)}{Environment.NewLine}" &
            $"Character: {characterName}{Environment.NewLine}" &
            $"HP: {Math.Max(0, hpPercent):0.0}% | Numbers: {If(String.IsNullOrWhiteSpace(hpNumbersText), "n/a", hpNumbersText)}{Environment.NewLine}" &
            $"MP: {Math.Max(0, mpPercent):0.0}% | Numbers: {If(String.IsNullOrWhiteSpace(mpNumbersText), "n/a", mpNumbersText)}{Environment.NewLine}" &
            $"Mob Name: {visionMobName}"

        Task.Run(
            Async Function()
                Try
                    Await SendHardcodedVisionStatsDiscordAsync(body)
                Finally
                    SyncLock _sync
                        _hardcodedVisionStatsInFlight = False
                    End SyncLock
                End Try
            End Function)
    End Sub

    Private Shared Function ReadCharacterInfoFromHpBar(frame As Bitmap, hpRegion As RectRegion) As String
        If frame Is Nothing OrElse hpRegion Is Nothing Then
            Return ""
        End If

        Dim characterRegion As RectRegion = BuildCharacterInfoRegion(hpRegion)
        Dim rect As Rectangle = characterRegion.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return ""
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using
            Using enlarged As Bitmap = EnlargeBitmap(crop, 4)
                Dim text As String = NormalizeCharacterInfoText(OcrReader.ReadScreenTextIsolated(enlarged))
                If text = "" Then
                    text = NormalizeCharacterInfoText(OcrReader.ReadName(enlarged))
                End If
                Return text
            End Using
        End Using
    End Function

    Private Shared Function ReadBarNumbersFromRegion(frame As Bitmap, region As RectRegion) As String
        If frame Is Nothing OrElse region Is Nothing Then
            Return ""
        End If

        Dim rect As Rectangle = region.Clamp(frame.Width, frame.Height)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then
            Return ""
        End If

        Using crop As New Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(crop)
                g.DrawImage(frame, New Rectangle(0, 0, crop.Width, crop.Height), rect, GraphicsUnit.Pixel)
            End Using
            Using enlarged As Bitmap = EnlargeBitmap(crop, 4)
                Dim text As String = NormalizeMobHpText(OcrReader.ReadHpFraction(enlarged))
                If text = "" Then
                    text = NormalizeMobHpText(OcrReader.ReadScreenTextIsolated(enlarged))
                End If
                Return text
            End Using
        End Using
    End Function

    Private Shared Function BuildCharacterInfoRegion(hpRegion As RectRegion) As RectRegion
        Dim width As Integer = Math.Max(1, hpRegion.W + 10)
        Dim height As Integer = Math.Max(1, hpRegion.H + 10)
        Return New RectRegion(hpRegion.X - 5, Math.Max(0, hpRegion.Y - height), width, height)
    End Function

    Private Shared Function EnlargeBitmap(source As Bitmap, scale As Integer) As Bitmap
        Dim safeScale As Integer = Math.Max(1, scale)
        Dim enlarged As New Bitmap(Math.Max(1, source.Width * safeScale), Math.Max(1, source.Height * safeScale), PixelFormat.Format24bppRgb)
        Using g As Graphics = Graphics.FromImage(enlarged)
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.DrawImage(source, New Rectangle(0, 0, enlarged.Width, enlarged.Height), New Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
        End Using
        Return enlarged
    End Function

    Private Shared Function NormalizeCharacterInfoText(raw As String) As String
        Dim cleaned As String = If(raw, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        cleaned = Regex.Replace(cleaned, "\s+", " ")
        Return cleaned
    End Function

    Private Shared Function HasDetectedCharacterName(raw As String) As Boolean
        Dim cleaned As String = NormalizeCharacterInfoText(raw)
        If cleaned = "" Then
            Return False
        End If
        If cleaned.Equals("unknown", StringComparison.OrdinalIgnoreCase) OrElse
           cleaned.Equals("n/a", StringComparison.OrdinalIgnoreCase) OrElse
           cleaned.Equals("none", StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If
        Return Regex.IsMatch(cleaned, "\p{L}|\p{N}")
    End Function

    Private Shared Function ResolveVisionStatsCharacterName(rawOcrText As String, titleCharacterName As String, actualWindowTitle As String, configuredTitle As String) As String
        Dim ocrCandidate As String = CleanCharacterNameCandidate(NormalizeCharacterInfoText(rawOcrText))
        If IsKnownCharacterName(ocrCandidate) AndAlso
           Not LooksLikeWindowTitleText(ocrCandidate, actualWindowTitle, configuredTitle) Then
            Return ocrCandidate
        End If

        Dim titleCandidate As String = CleanCharacterNameCandidate(titleCharacterName)
        If IsKnownCharacterName(titleCandidate) Then
            Return titleCandidate
        End If

        Return "unknown"
    End Function

    Private Shared Function IsKnownCharacterName(candidate As String) As Boolean
        Dim cleaned As String = NormalizeCharacterInfoText(candidate)
        If Not HasDetectedCharacterName(cleaned) Then
            Return False
        End If
        Return Not LooksLikeKathanaGameTitle(cleaned)
    End Function

    Private Shared Function LooksLikeWindowTitleText(candidate As String, actualWindowTitle As String, configuredTitle As String) As Boolean
        Dim cleaned As String = NormalizeForLooseTextMatch(candidate)
        If cleaned = "" Then
            Return False
        End If

        If LooksLikeKathanaGameTitle(candidate) Then
            Return True
        End If

        Dim actualTitle As String = NormalizeForLooseTextMatch(actualWindowTitle)
        If actualTitle <> "" AndAlso (cleaned = actualTitle OrElse actualTitle.Contains(cleaned) OrElse cleaned.Contains(actualTitle)) Then
            Return True
        End If

        Dim configured As String = NormalizeForLooseTextMatch(configuredTitle)
        If configured <> "" AndAlso (cleaned = configured OrElse configured.Contains(cleaned) OrElse cleaned.Contains(configured)) Then
            Return True
        End If

        Return False
    End Function

    Private Shared Function LooksLikeKathanaGameTitle(raw As String) As Boolean
        Dim cleaned As String = NormalizeForLooseTextMatch(raw)
        If cleaned = "" Then
            Return False
        End If

        Return (cleaned.Contains("kathana") AndAlso
                cleaned.Contains("coming") AndAlso
                cleaned.Contains("dark")) OrElse
               (cleaned.Contains("the coming") AndAlso
                cleaned.Contains("dark"))
    End Function

    Private Shared Function NormalizeForLooseTextMatch(raw As String) As String
        Dim cleaned As String = If(raw, "").ToLowerInvariant()
        cleaned = cleaned.Replace("0", "o").Replace("1", "i")
        cleaned = Regex.Replace(cleaned, "[^a-z0-9]+", " ")
        cleaned = Regex.Replace(cleaned, "\s+", " ").Trim()
        Return cleaned
    End Function

    Private Shared Function GetWindowTitle(hwnd As IntPtr) As String
        If hwnd = IntPtr.Zero Then
            Return ""
        End If

        Dim sb As New StringBuilder(512)
        If NativeMethods.GetWindowText(hwnd, sb, sb.Capacity) <= 0 Then
            Return ""
        End If
        Return sb.ToString().Trim()
    End Function

    Private Shared Function ExtractCharacterNameFromWindowTitle(windowTitle As String, configuredTitle As String) As String
        Dim title As String = If(windowTitle, "").Trim()
        If title = "" Then
            Return "unknown"
        End If

        Dim kathanaMarker As String = " - Kathana"
        Dim markerIndex As Integer = title.IndexOf(kathanaMarker, StringComparison.OrdinalIgnoreCase)
        If markerIndex > 0 Then
            Dim candidate As String = CleanCharacterNameCandidate(title.Substring(0, markerIndex))
            If candidate <> "" Then
                Return candidate
            End If
        End If

        Dim configured As String = If(configuredTitle, "").Trim()
        If configured <> "" AndAlso Not title.Equals(configured, StringComparison.OrdinalIgnoreCase) Then
            If title.EndsWith(configured, StringComparison.OrdinalIgnoreCase) Then
                Dim candidate As String = CleanCharacterNameCandidate(title.Substring(0, title.Length - configured.Length))
                If candidate <> "" Then
                    Return candidate
                End If
            ElseIf title.StartsWith(configured, StringComparison.OrdinalIgnoreCase) Then
                Dim candidate As String = CleanCharacterNameCandidate(title.Substring(configured.Length))
                If candidate <> "" Then
                    Return candidate
                End If
            End If
        End If

        If title.IndexOf("Kathana", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
           title.IndexOf("The Coming of the Dark Ages", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "unknown"
        End If

        Return title
    End Function

    Private Shared Function CleanCharacterNameCandidate(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim(" "c, "-"c, ":"c, "|"c, "["c, "]"c)
        If LooksLikeKathanaGameTitle(cleaned) OrElse
           cleaned.Equals("Kathana", StringComparison.OrdinalIgnoreCase) OrElse
           cleaned.Equals("The Coming of the Dark Ages", StringComparison.OrdinalIgnoreCase) Then
            Return ""
        End If
        Return cleaned
    End Function

    Private Shared Async Function SendHardcodedVisionStatsDiscordAsync(body As String) As Task(Of Boolean)
        Dim rawWebhookUrl As String = HardcodedVisionStatsDiscordWebhookUrl.Trim()
        If Not IsLikelyDiscordWebhookUrl(rawWebhookUrl) Then
            Return False
        End If

        Dim payloadText As String = If(body, "").Trim()
        If payloadText.Length > 1900 Then
            payloadText = payloadText.Substring(0, 1897) & "..."
        End If

        Try
            Using request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, NormalizeDiscordWebhookUrl(rawWebhookUrl))
                Dim payload = New With {
                    .username = "KathanaBot",
                    .content = payloadText,
                    .allowed_mentions = New With {
                        .parse = Array.Empty(Of String)()
                    }
                }
                request.Content = New System.Net.Http.StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                Dim response As System.Net.Http.HttpResponseMessage = Await HardcodedVisionStatsHttpClient.SendAsync(request)
                Return response.IsSuccessStatusCode
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Shared Function NormalizeNotificationProviderName(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim().ToLowerInvariant()
        If cleaned = NotificationProviderDiscord Then
            Return NotificationProviderDiscord
        End If
        Return NotificationProviderNtfy
    End Function

    Private Shared Function GetDiscordGlobalWebhookUrl(cfg As BotConfig) As String
        Dim globalWebhook As String = If(cfg IsNot Nothing, If(cfg.DiscordGlobalWebhookUrl, "").Trim(), "")
        If globalWebhook = "" AndAlso cfg IsNot Nothing Then
            globalWebhook = If(cfg.DiscordWebhookUrl, "").Trim()
        End If
        Return globalWebhook
    End Function

    Private Shared Function GetDiscordItemWebhookUrl(cfg As BotConfig) As String
        Dim itemWebhook As String = If(cfg IsNot Nothing, If(cfg.DiscordItemWebhookUrl, "").Trim(), "")
        If itemWebhook = "" Then
            itemWebhook = GetDiscordGlobalWebhookUrl(cfg)
        End If
        Return itemWebhook
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
End Class
