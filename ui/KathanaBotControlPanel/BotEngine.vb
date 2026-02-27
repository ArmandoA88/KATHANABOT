Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks

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

Public Class BotConfig
    Public Property WindowTitle As String = "Kathana - The Coming of the Dark Ages"
    Public Property LoopMs As Integer = 80
    Public Property RetargetMs As Integer = 550
    Public Property MobHpPresenceThreshold As Double = 1.0
    Public Property HpBar As RectRegion = New RectRegion(11, 25, 151, 11)
    Public Property MpBar As RectRegion = New RectRegion(3, 40, 161, 11)
    Public Property MobNameRect As RectRegion = New RectRegion(860, 711, 162, 23)
    Public Property MobHpRect As RectRegion = New RectRegion(859, 737, 165, 11)
    Public Property PranaExpRect As RectRegion = New RectRegion(472, 745, 78, 21)
    Public Property PartyInviteScanRect As RectRegion = New RectRegion(349, 318, 328, 124)
    Public Property PartyInviteOkRect As RectRegion = New RectRegion(463, 410, 59, 21)
    Public Property BypassHpMpLimits As Boolean = False
    Public Property BypassStuckTarget As Boolean = True
    Public Property StuckTargetMs As Integer = 2200
    Public Property DeniedMobs As List(Of String) = New List(Of String)()
    Public Property LootPickupEnabled As Boolean = False
    Public Property LootPickupIntervalMs As Integer = 4000
    Public Property LootPickupVerifyDelayMs As Integer = 200
    Public Property LootAllowedNames As List(Of String) = New List(Of String)()
    Public Property PartyAutoAcceptEnabled As Boolean = True
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
End Class

Public Class BotStatus
    Public Property Running As Boolean
    Public Property WindowFound As Boolean
    Public Property HpPercent As Double
    Public Property MpPercent As Double
    Public Property MobHpPercent As Double
    Public Property ExpPercent As Double
    Public Property ExpPerHour As Double = -1
    Public Property MobName As String = ""
    Public Property TargetValid As Boolean
    Public Property LastAction As String = ""
    Public Property NotAttackingReason As String = ""
    Public Property ErrorMessage As String = ""
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
    Private Const NoTargetRetargetMs As Integer = 300
    Private Const FirstHitWindowMs As Integer = 800
    Private Const BlacklistLockWindowMs As Integer = 800
    Private Const TargetNameConfirmMinGapMs As Integer = 120
    Private Const TargetNameConfirmRequiredCount As Integer = 2
    Private Const ExpRateSampleMs As Integer = 60000
    Private Const ExpOcrMinIntervalMs As Integer = 900
    Private Const PartyInviteOcrMinIntervalMs As Integer = 900
    Private Const BaseClientWidth As Integer = 1024
    Private Const BaseClientHeight As Integer = 768

    Private ReadOnly _sync As New Object()
    Private _config As BotConfig = BotConfig.CreateDefault()
    Private _status As New BotStatus()
    Private _cts As CancellationTokenSource
    Private _task As Task
    Private _lastRetarget As DateTime = DateTime.MinValue
    Private _lastAttackAction As DateTime = DateTime.MinValue
    Private _lastMobHpSample As Double = -1
    Private _lastMobHpMovement As DateTime = DateTime.MinValue
    Private _lastMobNameRead As DateTime = DateTime.MinValue
    Private _cachedMobName As String = ""
    Private _lastPeriodicSnapshot As DateTime = DateTime.MinValue
    Private _lastLootPickup As DateTime = DateTime.MinValue
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
    Private _lastExpPercent As Double = -1
    Private _lastExpOcrAt As DateTime = DateTime.MinValue
    Private _expOcrTask As Task(Of Double) = Nothing
    Private _lastExpRateSampleAt As DateTime = DateTime.MinValue
    Private _lastExpRateSamplePercent As Double = -1
    Private _lastExpPerHour As Double = -1
    Private ReadOnly _lootRandom As New Random()
    Private ReadOnly _lastKeyTime As New Dictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
    Private _lastGoodHpPercent As Double = -1
    Private _lastGoodMpPercent As Double = -1
    Private _lastGoodMobHpPercent As Double = -1
    Private _lastGoodMobName As String = ""
    Private _zeroSpikeHoldCount As Integer = 0
    Private _zeroPairConfirmCount As Integer = 0

    Private Shared ReadOnly KeyMap As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
        {"0", &H30}, {"1", &H31}, {"2", &H32}, {"3", &H33}, {"4", &H34}, {"5", &H35},
        {"6", &H36}, {"7", &H37}, {"8", &H38}, {"9", &H39}, {"E", &H45}, {"F", &H46}, {"W", &H57}, {"S", &H53},
        {"ENTER", &HD}, {"RETURN", &HD},
        {"F1", &H70}, {"F2", &H71}, {"F3", &H72}, {"F4", &H73}, {"F5", &H74},
        {"F6", &H75}, {"F7", &H76}, {"F8", &H77}, {"F9", &H78}, {"F10", &H79}
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
            _lastRetarget = DateTime.MinValue
            _lastAttackAction = DateTime.MinValue
            _lastMobHpSample = -1
            _lastMobHpMovement = DateTime.MinValue
            _lastMobNameRead = DateTime.MinValue
            _cachedMobName = ""
            _lastPeriodicSnapshot = DateTime.MinValue
            _lastLootPickup = DateTime.MinValue
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
            _lastExpPercent = -1
            _lastExpOcrAt = DateTime.MinValue
            _expOcrTask = Nothing
            _lastExpRateSampleAt = DateTime.MinValue
            _lastExpRateSamplePercent = -1
            _lastExpPerHour = -1
            _lastGoodHpPercent = -1
            _lastGoodMpPercent = -1
            _lastGoodMobHpPercent = -1
            _lastGoodMobName = ""
            _zeroSpikeHoldCount = 0
            _zeroPairConfirmCount = 0
            _task = Task.Run(Sub() LoopAsync(_cts.Token).GetAwaiter().GetResult())
        End SyncLock
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
        End SyncLock
        RaiseEvent LogLine("Bot loop stopped.")
    End Sub

    Public Function CaptureSnapshot() As Bitmap
        Dim cfg As BotConfig
        SyncLock _sync
            cfg = _config
        End SyncLock

        Dim hwnd As IntPtr = FindGameWindow(cfg.WindowTitle)
        If hwnd = IntPtr.Zero Then
            Return Nothing
        End If

        Return CaptureClient(hwnd)
    End Function

    Private Async Function LoopAsync(token As CancellationToken) As Task
        While Not token.IsCancellationRequested
            Dim cfg As BotConfig
            SyncLock _sync
                cfg = _config
            End SyncLock

            Dim hwnd As IntPtr = FindGameWindow(cfg.WindowTitle)
            If hwnd = IntPtr.Zero Then
                SetStatus(Sub(s)
                              s.WindowFound = False
                              s.HpPercent = 0
                              s.MpPercent = 0
                              s.MobHpPercent = 0
                              s.ExpPercent = 0
                              s.ExpPerHour = -1
                              s.MobName = ""
                              s.TargetValid = False
                              s.NotAttackingReason = "Window not found."
                              s.ErrorMessage = "Game window not found."
                          End Sub)
                Await Task.Delay(450, token)
                Continue While
            End If

            Dim frame As Bitmap = CaptureClient(hwnd)
            If frame Is Nothing Then
                SetStatus(Sub(s)
                              s.WindowFound = True
                              s.NotAttackingReason = "Capture failed."
                              s.ErrorMessage = "Unable to capture game client."
                          End Sub)
                Await Task.Delay(120, token)
                Continue While
            End If

            Dim hpRegion As New RectRegion(0, 0, 1, 1)
            Dim mpRegion As New RectRegion(0, 0, 1, 1)
            Dim mobNameRegion As New RectRegion(0, 0, 1, 1)
            Dim mobHpRegion As New RectRegion(0, 0, 1, 1)
            Dim pranaExpRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteScanRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteOkRegion As New RectRegion(0, 0, 1, 1)
            ResolveVisionRegions(cfg, frame.Width, frame.Height, hpRegion, mpRegion, mobNameRegion, mobHpRegion, pranaExpRegion, partyInviteScanRegion, partyInviteOkRegion)

            Dim hpPct As Double = ComputeBarPercent(frame, hpRegion, True)
            Dim mpPct As Double = ComputeBarPercent(frame, mpRegion, False)
            Dim mobHpPct As Double = ComputeBarPercent(frame, mobHpRegion, True)
            Dim expPct As Double = ReadPranaExpPercent(frame, pranaExpRegion)
            Dim captureGlitch As Boolean = IsLikelyVisionCaptureGlitch(frame, hpRegion, mpRegion, hpPct, mpPct)

            If captureGlitch Then
                For retry As Integer = 1 To 2
                    frame.Dispose()
                    Thread.Sleep(12)
                    frame = CaptureClient(hwnd)
                    If frame Is Nothing Then
                        Exit For
                    End If

                    ResolveVisionRegions(cfg, frame.Width, frame.Height, hpRegion, mpRegion, mobNameRegion, mobHpRegion, pranaExpRegion, partyInviteScanRegion, partyInviteOkRegion)
                    hpPct = ComputeBarPercent(frame, hpRegion, True)
                    mpPct = ComputeBarPercent(frame, mpRegion, False)
                    mobHpPct = ComputeBarPercent(frame, mobHpRegion, True)
                    expPct = ReadPranaExpPercent(frame, pranaExpRegion)
                    captureGlitch = IsLikelyVisionCaptureGlitch(frame, hpRegion, mpRegion, hpPct, mpPct)
                    If Not captureGlitch Then
                        Exit For
                    End If
                Next

                If frame Is Nothing Then
                    SetStatus(Sub(s)
                                  s.WindowFound = True
                                  s.NotAttackingReason = "Capture failed."
                                  s.ErrorMessage = "Unable to capture game client."
                              End Sub)
                    Await Task.Delay(120, token)
                    Continue While
                End If
            End If

            Dim now As DateTime = DateTime.UtcNow
            SavePeriodicSnapshot(frame, now)
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
            Dim targetWindowVisible As Boolean = HasTargetWindowSignal(frame, mobHpRegion, mobName, mobHpPct)
            Dim deniedTarget As Boolean = IsDeniedMob(mobName, cfg.DeniedMobs)
            Dim normMobName As String = NormalizeMobName(mobName)

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
            Dim canTrackFirstHitTarget As Boolean = targetWindowVisible AndAlso (mobHpPct >= cfg.MobHpPresenceThreshold) AndAlso (Not deniedTarget) AndAlso (Not missingNameBlockedByFilter)
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
                targetWindowVisible AndAlso
                (mobHpPct >= cfg.MobHpPresenceThreshold) AndAlso
                (Not deniedTarget) AndAlso
                (Not missingNameBlockedByFilter) AndAlso
                (Not nameConfirmationBlockedByFilter) AndAlso
                (Not blacklistLockActive)
            TrackMobHpMovement(targetValid, mobHpPct, now)

            Dim reason As String = ""
            Dim actionSent As Boolean = TryHandlePartyInvite(cfg, hwnd, frame, now, partyInviteScanRegion, partyInviteOkRegion)
            If actionSent Then
                reason = "Party invite detected and accepted."
            End If
            Dim forcedRetarget As Boolean = False

            If (Not _firstHitPending) AndAlso ShouldBypassStuckTarget(cfg, targetValid, now) Then
                If SendKey(hwnd, "E", 35) Then
                    _lastRetarget = now
                    SetLastAction("E (stuck target bypass)")
                    reason = "Stuck target bypass sent retarget."
                    forcedRetarget = True
                End If
            End If

            If Not forcedRetarget AndAlso Not actionSent Then
                Dim supportSent As Boolean = TrySendSupportActions(cfg, hwnd, hpPct, mpPct)
                If supportSent Then
                    actionSent = True
                    reason = ""
                Else
                    Dim allowBlindAttack As Boolean = AllowBlindAttackWhenTargetMissing AndAlso (Not deniedTarget)
                    Dim chosen As ActionRule = ChooseAction(cfg, hpPct, mpPct, targetValid, allowBlindAttack, reason)
                    If chosen IsNot Nothing AndAlso SendKey(hwnd, chosen.KeyName, 35) Then
                        MarkKeyUsed(chosen.KeyName)
                        SetLastAction($"{chosen.KeyName} ({chosen.Role})")
                        actionSent = True
                        reason = ""
                        If chosen.Role = "attack" OrElse chosen.Role = "special" Then
                            _lastAttackAction = now
                            _firstHitPending = False
                            _firstHitWindowUntil = DateTime.MinValue
                        End If
                    End If
                End If
            End If

            If Not targetValid AndAlso Not actionSent Then
                If _firstHitPending Then
                    If String.IsNullOrWhiteSpace(reason) Then
                        If firstHitWindowActive Then
                            reason = $"First-hit attack window active ({FirstHitWindowMs}ms). Waiting to send first attack."
                        Else
                            reason = "Waiting to send first attack before retarget."
                        End If
                    End If
                Else
                    Dim retargetDelayMs As Integer = Math.Max(100, NoTargetRetargetMs)
                    If (now - _lastRetarget).TotalMilliseconds >= retargetDelayMs Then
                        If SendKey(hwnd, "E", 35) Then
                            _lastRetarget = now
                            SetLastAction("E (retarget)")
                            If String.IsNullOrWhiteSpace(reason) Then
                                If deniedTarget Then
                                    reason = $"Monster filter blocked target '{If(String.IsNullOrWhiteSpace(mobName), "unknown", mobName)}'. Retarget key sent."
                                ElseIf blacklistLockActive Then
                                    reason = $"Monster filter lock active ({BlacklistLockWindowMs}ms). Retarget key sent."
                                ElseIf missingNameBlockedByFilter Then
                                    reason = "Monster filter waiting for mob name OCR. Retarget key sent."
                                ElseIf nameConfirmationBlockedByFilter Then
                                    reason = "Monster filter waiting for 2x name confirmation. Retarget key sent."
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
                        ElseIf nameConfirmationBlockedByFilter Then
                            reason = "Monster filter waiting for 2x name confirmation. Waiting retarget cooldown."
                        ElseIf Not targetWindowVisible Then
                            reason = "No target window detected. Waiting 300ms retarget cooldown."
                        Else
                            reason = "No target detected. Waiting 300ms retarget cooldown."
                        End If
                    End If
                End If
            End If

            TryHandleLootPickup(cfg, hwnd, now, actionSent OrElse _firstHitPending)

            frame.Dispose()

            SetStatus(Sub(s)
                          s.WindowFound = True
                          s.HpPercent = Math.Round(hpPct, 1)
                          s.MpPercent = Math.Round(mpPct, 1)
                          s.MobHpPercent = Math.Round(mobHpPct, 1)
                          s.ExpPercent = Math.Round(Math.Max(0, If(expPct < 0, 0, expPct)), 2)
                          s.ExpPerHour = If(expPerHour < 0, -1, Math.Round(expPerHour, 2))
                          s.MobName = mobName
                          s.TargetValid = targetValid
                          s.NotAttackingReason = If(actionSent, "", reason)
                          s.ErrorMessage = ""
                      End Sub)

            Await Task.Delay(Math.Max(20, cfg.LoopMs), token)
        End While
    End Function

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

        If captureGlitch OrElse suspiciousZeroSpike Then
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

    Private Sub SavePeriodicSnapshot(frame As Bitmap, now As DateTime)
        If frame Is Nothing Then
            Return
        End If
        If _lastPeriodicSnapshot <> DateTime.MinValue AndAlso (now - _lastPeriodicSnapshot).TotalMinutes < 15 Then
            Return
        End If

        _lastPeriodicSnapshot = now
        Try
            Dim picturesRoot As String = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            If String.IsNullOrWhiteSpace(picturesRoot) Then
                picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            End If
            Dim galleryDir As String = Path.Combine(picturesRoot, "KathanaBot")
            Directory.CreateDirectory(galleryDir)

            Dim fileName As String = $"kathana_{now:yyyyMMdd_HHmmss}.png"
            Dim fullPath As String = Path.Combine(galleryDir, fileName)
            frame.Save(fullPath, ImageFormat.Png)
            RaiseEvent LogLine("Snapshot saved: " & fullPath)
        Catch ex As Exception
            RaiseEvent LogLine("Snapshot save failed: " & ex.Message)
        End Try
    End Sub

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

    Private Sub TryHandleLootPickup(cfg As BotConfig, hwnd As IntPtr, now As DateTime, actionSent As Boolean)
        If Not cfg.LootPickupEnabled Then
            Return
        End If
        If hwnd = IntPtr.Zero Then
            Return
        End If
        If actionSent Then
            Return
        End If
        If cfg.LootAllowedNames Is Nothing OrElse cfg.LootAllowedNames.Count = 0 Then
            Return
        End If

        Dim intervalMs As Integer = Math.Max(1000, cfg.LootPickupIntervalMs)
        If _lastLootPickup <> DateTime.MinValue AndAlso (now - _lastLootPickup).TotalMilliseconds < intervalMs Then
            Return
        End If

        If _lastRetarget <> DateTime.MinValue AndAlso (now - _lastRetarget).TotalMilliseconds < 320 Then
            Return
        End If

        _lastLootPickup = now
        If Not SendKey(hwnd, "F", 35) Then
            Return
        End If

        RaiseEvent LogLine("Loot scan sent (F).")
        Thread.Sleep(Math.Max(120, cfg.LootPickupVerifyDelayMs))

        Dim verifyFrame As Bitmap = CaptureClient(hwnd)
        If verifyFrame Is Nothing Then
            RaiseEvent LogLine("Loot scan skipped: capture failed.")
            Return
        End If

        Try
            Dim hpRegion As New RectRegion(0, 0, 1, 1)
            Dim mpRegion As New RectRegion(0, 0, 1, 1)
            Dim mobNameRegion As New RectRegion(0, 0, 1, 1)
            Dim mobHpRegion As New RectRegion(0, 0, 1, 1)
            Dim pranaExpRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteScanRegion As New RectRegion(0, 0, 1, 1)
            Dim partyInviteOkRegion As New RectRegion(0, 0, 1, 1)
            ResolveVisionRegions(cfg, verifyFrame.Width, verifyFrame.Height, hpRegion, mpRegion, mobNameRegion, mobHpRegion, pranaExpRegion, partyInviteScanRegion, partyInviteOkRegion)

            Dim selectedName As String = ReadMobNameIfNeeded(verifyFrame, mobNameRegion, DateTime.UtcNow, True)
            If IsAllowedLootName(selectedName, cfg.LootAllowedNames) Then
                SetLastAction($"F (loot accepted: {If(String.IsNullOrWhiteSpace(selectedName), "unknown", selectedName)})")
                Thread.Sleep(700)
                Return
            End If

            Dim rejectKey As String = If(_lootRandom.Next(0, 2) = 0, "W", "S")
            If SendKey(hwnd, rejectKey, 35) Then
                SetLastAction($"{rejectKey} (loot rejected: {If(String.IsNullOrWhiteSpace(selectedName), "unknown", selectedName)})")
            End If
        Catch ex As Exception
            RaiseEvent LogLine("Loot scan error: " & ex.Message)
        Finally
            verifyFrame.Dispose()
        End Try
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

    Private Function TryHandlePartyInvite(cfg As BotConfig, hwnd As IntPtr, frame As Bitmap, now As DateTime, partyInviteScanRegion As RectRegion, partyInviteOkRegion As RectRegion) As Boolean
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

        If IsPartyInvitePrompt(_lastPartyInviteCandidate) Then
            If ClickClientRegionCenter(hwnd, partyInviteOkRegion, frame.Width, frame.Height) Then
                _lastPartyInviteAccept = now
                SetLastAction($"Click OK (party invite accepted: {If(String.IsNullOrWhiteSpace(_lastPartyInviteCandidate), "detected", _lastPartyInviteCandidate)})")
                RaiseEvent LogLine("Party invite detected and auto-accepted.")
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
            Return True
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

    Private Shared Function IsAllowedLootName(rawName As String, allowList As List(Of String)) As Boolean
        If String.IsNullOrWhiteSpace(rawName) OrElse allowList Is Nothing OrElse allowList.Count = 0 Then
            Return False
        End If

        Dim normName As String = NormalizeMobName(rawName)
        If normName = "" Then
            Return False
        End If

        For Each entry In allowList
            Dim normAllowed As String = NormalizeMobName(entry)
            If normAllowed = "" Then
                Continue For
            End If
            If normName.Equals(normAllowed, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            If normName.Contains(normAllowed, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function NormalizeMobName(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If
        Dim cleaned As String = Regex.Replace(raw, "[^A-Za-z0-9 '\-]", " ")
        cleaned = Regex.Replace(cleaned, "\s+", " ").Trim().ToLowerInvariant()
        Return cleaned
    End Function

    Private Sub TrackMobHpMovement(targetValid As Boolean, mobHpPct As Double, now As DateTime)
        If Not targetValid Then
            _lastMobHpSample = -1
            _lastMobHpMovement = DateTime.MinValue
            Return
        End If

        If _lastMobHpSample < 0 Then
            _lastMobHpSample = mobHpPct
            _lastMobHpMovement = now
            Return
        End If

        If Math.Abs(mobHpPct - _lastMobHpSample) >= 0.8 Then
            _lastMobHpSample = mobHpPct
            _lastMobHpMovement = now
        End If
    End Sub

    Private Function ShouldBypassStuckTarget(cfg As BotConfig, targetValid As Boolean, now As DateTime) As Boolean
        If Not cfg.BypassStuckTarget Then
            Return False
        End If
        If Not targetValid Then
            Return False
        End If
        If _lastAttackAction = DateTime.MinValue OrElse _lastMobHpMovement = DateTime.MinValue Then
            Return False
        End If

        Dim sinceAttackMs As Double = (now - _lastAttackAction).TotalMilliseconds
        If sinceAttackMs > Math.Max(6000, cfg.StuckTargetMs * 3) Then
            Return False
        End If

        Dim sinceHpMoveMs As Double = (now - _lastMobHpMovement).TotalMilliseconds
        If sinceHpMoveMs < cfg.StuckTargetMs Then
            Return False
        End If

        Dim retargetCooldownMs As Integer = Math.Max(250, cfg.RetargetMs \ 2)
        Return (now - _lastRetarget).TotalMilliseconds >= retargetCooldownMs
    End Function

    Private Function TrySendSupportActions(cfg As BotConfig, hwnd As IntPtr, hpPercent As Double, mpPercent As Double) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim ordered = cfg.Actions.
            Where(Function(a) a.Enabled AndAlso (a.Role = "heal" OrElse a.Role = "mana")).
            OrderBy(Function(a) a.Priority).
            ToList()
        If ordered.Count = 0 Then
            Return False
        End If

        Dim sentAny As Boolean = False
        For Each action In ordered
            Dim triggered As Boolean =
                (action.Role = "heal" AndAlso hpPercent <= action.TriggerPercent) OrElse
                (action.Role = "mana" AndAlso mpPercent <= action.TriggerPercent)
            If Not triggered Then
                Continue For
            End If
            If Not IsReady(action) Then
                Continue For
            End If
            If Not SendKey(hwnd, action.KeyName, 35) Then
                Continue For
            End If

            MarkKeyUsed(action.KeyName)
            SetLastAction($"{action.KeyName} ({action.Role})")
            sentAny = True
        Next

        Return sentAny
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

        If SendKey(hwnd, "E", 35) Then
            _lastRetarget = DateTime.UtcNow
            SetLastAction("E (manual retarget)")
            Return True
        End If
        Return False
    End Function

    Private Function ChooseAction(cfg As BotConfig, hpPercent As Double, mpPercent As Double, targetValid As Boolean, allowBlindAttack As Boolean, ByRef reason As String) As ActionRule
        Dim ordered = cfg.Actions.Where(Function(a) a.Enabled).OrderBy(Function(a) a.Priority).ToList()
        If ordered.Count = 0 Then
            reason = "No enabled keys."
            Return Nothing
        End If

        Dim hasAttackKey As Boolean = False
        Dim statBlocked As Boolean = False
        Dim cooldownBlocked As Boolean = False

        For Each action In ordered
            If action.Role <> "attack" AndAlso action.Role <> "special" Then
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
                reason = ""
                Return action
            End If
        Next

        If Not hasAttackKey Then
            reason = "No enabled attack/special keys."
        ElseIf Not targetValid AndAlso Not allowBlindAttack Then
            reason = "No target detected."
        ElseIf statBlocked AndAlso (Not cfg.BypassHpMpLimits) Then
            reason = "HP/MP limits blocked all attack keys."
        ElseIf cooldownBlocked Then
            reason = "All attack keys are on cooldown."
        Else
            reason = "No eligible attack key."
        End If

        Return Nothing
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
            .ExpPercent = src.ExpPercent,
            .ExpPerHour = src.ExpPerHour,
            .MobName = src.MobName,
            .TargetValid = src.TargetValid,
            .LastAction = src.LastAction,
            .NotAttackingReason = src.NotAttackingReason,
            .ErrorMessage = src.ErrorMessage,
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

    Private Shared Sub ResolveVisionRegions(cfg As BotConfig, frameWidth As Integer, frameHeight As Integer, ByRef hpBar As RectRegion, ByRef mpBar As RectRegion, ByRef mobNameRect As RectRegion, ByRef mobHpRect As RectRegion, ByRef pranaExpRect As RectRegion, ByRef partyInviteScanRect As RectRegion, ByRef partyInviteOkRect As RectRegion)
        hpBar = CloneRegion(cfg.HpBar)
        mpBar = CloneRegion(cfg.MpBar)
        mobNameRect = CloneRegion(cfg.MobNameRect)
        mobHpRect = CloneRegion(cfg.MobHpRect)
        pranaExpRect = CloneRegion(cfg.PranaExpRect)
        partyInviteScanRect = CloneRegion(cfg.PartyInviteScanRect)
        partyInviteOkRect = CloneRegion(cfg.PartyInviteOkRect)

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
        pranaExpRect = ScaleRegionLeftTop(cfg.PranaExpRect, sx, sy)
        partyInviteScanRect = ScaleRegionLeftTop(cfg.PartyInviteScanRect, sx, sy)
        partyInviteOkRect = ScaleRegionLeftTop(cfg.PartyInviteOkRect, sx, sy)
    End Sub

    Private Shared Function IsDefaultVisionLayout(cfg As BotConfig) As Boolean
        Return SameRegion(cfg.HpBar, New RectRegion(11, 25, 151, 11)) AndAlso
               SameRegion(cfg.MpBar, New RectRegion(3, 40, 161, 11)) AndAlso
               SameRegion(cfg.MobNameRect, New RectRegion(860, 711, 162, 23)) AndAlso
               SameRegion(cfg.MobHpRect, New RectRegion(859, 737, 165, 11)) AndAlso
               SameRegion(cfg.PranaExpRect, New RectRegion(472, 745, 78, 21)) AndAlso
               SameRegion(cfg.PartyInviteScanRect, New RectRegion(349, 318, 328, 124)) AndAlso
               SameRegion(cfg.PartyInviteOkRect, New RectRegion(463, 410, 59, 21))
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

    Public Shared Function SendKey(hwnd As IntPtr, keyName As String, pressMs As Integer) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim vk As Integer
        If Not KeyMap.TryGetValue(keyName.ToUpperInvariant(), vk) Then
            Return False
        End If

        Dim scan As UInteger = NativeMethods.MapVirtualKey(CUInt(vk), 0UI)
        Dim lparamDown As Integer = 1 Or (CInt(scan) << 16)
        Dim lparamUp As Integer = lparamDown Or (1 << 30) Or (1 << 31)

        Try
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_KEYDOWN), New IntPtr(vk), New IntPtr(lparamDown))
            Thread.Sleep(Math.Max(5, pressMs))
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_KEYUP), New IntPtr(vk), New IntPtr(lparamUp))
            Return True
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

    Public Shared Function ClickClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim lParam As Integer = (x And &HFFFF) Or ((y And &HFFFF) << 16)
        Try
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_MOUSEMOVE), IntPtr.Zero, New IntPtr(lParam))
            Thread.Sleep(10)
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_LBUTTONDOWN), New IntPtr(NativeMethods.MK_LBUTTON), New IntPtr(lParam))
            Thread.Sleep(25)
            NativeMethods.PostMessage(hwnd, CUInt(NativeMethods.WM_LBUTTONUP), IntPtr.Zero, New IntPtr(lParam))
            Return True
        Catch
            Return False
        End Try
    End Function
End Class
