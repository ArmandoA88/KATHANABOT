Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Linq
Imports System.Runtime.InteropServices
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
    Public Property RetargetMs As Integer = 700
    Public Property MobHpPresenceThreshold As Double = 1.0
    Public Property HpBar As RectRegion = New RectRegion(11, 25, 151, 11)
    Public Property MpBar As RectRegion = New RectRegion(3, 40, 161, 11)
    Public Property MobNameRect As RectRegion = New RectRegion(862, 0, 162, 23)
    Public Property MobHpRect As RectRegion = New RectRegion(857, 24, 165, 11)
    Public Property BypassHpMpLimits As Boolean = False
    Public Property BypassStuckTarget As Boolean = True
    Public Property StuckTargetMs As Integer = 2200
    Public Property DeniedMobs As List(Of String) = New List(Of String)()
    Public Property Actions As List(Of ActionRule) = New List(Of ActionRule)()

    Public Shared Function CreateDefault() As BotConfig
        Dim cfg As New BotConfig()
        Dim keys As String() = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"}
        For i As Integer = 0 To keys.Length - 1
            Dim keyName As String = keys(i)
            Dim isPrimary As Boolean = i < 10
            Dim enabled As Boolean = (keyName = "1" OrElse keyName = "6")
            Dim role As String
            If keyName = "6" Then
                role = "heal"
            ElseIf isPrimary Then
                role = "attack"
            Else
                role = "special"
            End If
            Dim trigger As Integer = If(keyName = "6", 75, 40)
            cfg.Actions.Add(New ActionRule() With {
                .KeyName = keyName,
                .Enabled = enabled,
                .Role = role,
                .Priority = (i + 1) * 10,
                .CooldownMs = 500,
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
    Public Property MobName As String = ""
    Public Property TargetValid As Boolean
    Public Property LastAction As String = ""
    Public Property NotAttackingReason As String = ""
    Public Property ErrorMessage As String = ""
    Public Property UpdatedAt As DateTime = DateTime.UtcNow
End Class

Friend Module NativeMethods
    Friend Const PW_CLIENTONLY As UInteger = 1UI
    Friend Const WM_KEYDOWN As Integer = &H100
    Friend Const WM_KEYUP As Integer = &H101

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
End Module

Public Class BotEngine
    Public Event StatusUpdated(status As BotStatus)
    Public Event LogLine(line As String)
    Private Const AllowBlindAttackWhenTargetMissing As Boolean = True

    Private ReadOnly _sync As New Object()
    Private _config As BotConfig = BotConfig.CreateDefault()
    Private _status As New BotStatus()
    Private _cts As CancellationTokenSource
    Private _task As Task
    Private _lastRetarget As DateTime = DateTime.MinValue
    Private _lastAttackAction As DateTime = DateTime.MinValue
    Private _lastMobHpSample As Double = -1
    Private _lastMobHpMovement As DateTime = DateTime.MinValue
    Private ReadOnly _lastKeyTime As New Dictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly KeyMap As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
        {"0", &H30}, {"1", &H31}, {"2", &H32}, {"3", &H33}, {"4", &H34}, {"5", &H35},
        {"6", &H36}, {"7", &H37}, {"8", &H38}, {"9", &H39}, {"E", &H45},
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

            Dim hpPct As Double = ComputeBarPercent(frame, cfg.HpBar, True)
            Dim mpPct As Double = ComputeBarPercent(frame, cfg.MpBar, False)
            Dim mobHpPct As Double = ComputeBarPercent(frame, cfg.MobHpRect, True)
            Dim mobName As String = ""
            Dim targetValid As Boolean = mobHpPct >= cfg.MobHpPresenceThreshold

            Dim now As DateTime = DateTime.UtcNow
            TrackMobHpMovement(targetValid, mobHpPct, now)

            Dim reason As String = ""
            Dim actionSent As Boolean = False
            Dim forcedRetarget As Boolean = False

            If ShouldBypassStuckTarget(cfg, targetValid, now) Then
                If SendKey(hwnd, "E", 35) Then
                    _lastRetarget = now
                    SetLastAction("E (stuck target bypass)")
                    reason = "Stuck target bypass sent retarget."
                    forcedRetarget = True
                End If
            End If

            If Not forcedRetarget Then
                Dim chosen As ActionRule = ChooseAction(cfg, hpPct, mpPct, targetValid, AllowBlindAttackWhenTargetMissing, reason)
                If chosen IsNot Nothing AndAlso SendKey(hwnd, chosen.KeyName, 35) Then
                    SyncLock _sync
                        _lastKeyTime(chosen.KeyName) = DateTime.UtcNow
                    End SyncLock
                    SetLastAction($"{chosen.KeyName} ({chosen.Role})")
                    actionSent = True
                    reason = ""
                    If chosen.Role = "attack" OrElse chosen.Role = "special" Then
                        _lastAttackAction = now
                    End If
                End If
            End If

            If Not targetValid AndAlso Not actionSent Then
                If (now - _lastRetarget).TotalMilliseconds >= cfg.RetargetMs Then
                    If SendKey(hwnd, "E", 35) Then
                        _lastRetarget = now
                        SetLastAction("E (retarget)")
                        If String.IsNullOrWhiteSpace(reason) Then
                            reason = "No target detected. Retarget key sent."
                        End If
                    End If
                ElseIf String.IsNullOrWhiteSpace(reason) Then
                    reason = "No target detected. Waiting retarget cooldown."
                End If
            End If

            frame.Dispose()

            SetStatus(Sub(s)
                          s.WindowFound = True
                          s.HpPercent = Math.Round(hpPct, 1)
                          s.MpPercent = Math.Round(mpPct, 1)
                          s.MobHpPercent = Math.Round(mobHpPct, 1)
                          s.MobName = mobName
                          s.TargetValid = targetValid
                          s.NotAttackingReason = If(actionSent, "", reason)
                          s.ErrorMessage = ""
                      End Sub)

            Await Task.Delay(Math.Max(20, cfg.LoopMs), token)
        End While
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

        For Each action In ordered
            If action.Role = "heal" AndAlso hpPercent <= action.TriggerPercent AndAlso IsReady(action) Then
                reason = ""
                Return action
            End If
        Next

        For Each action In ordered
            If action.Role = "mana" AndAlso mpPercent <= action.TriggerPercent AndAlso IsReady(action) Then
                reason = ""
                Return action
            End If
        Next

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
            .MobName = src.MobName,
            .TargetValid = src.TargetValid,
            .LastAction = src.LastAction,
            .NotAttackingReason = src.NotAttackingReason,
            .ErrorMessage = src.ErrorMessage,
            .UpdatedAt = src.UpdatedAt
        }
    End Function

    Public Shared Function FindGameWindow(windowTitle As String) As IntPtr
        Dim exact As IntPtr = NativeMethods.FindWindow(Nothing, windowTitle)
        If exact <> IntPtr.Zero Then
            Return exact
        End If

        Dim found As IntPtr = IntPtr.Zero
        NativeMethods.EnumWindows(
            Function(hWnd As IntPtr, _lParam As IntPtr) As Boolean
                If Not NativeMethods.IsWindowVisible(hWnd) Then
                    Return True
                End If
                Dim sb As New StringBuilder(512)
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity)
                Dim title As String = sb.ToString()
                If title.IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    found = hWnd
                    Return False
                End If
                Return True
            End Function, IntPtr.Zero)
        Return found
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
            Using g As Graphics = Graphics.FromImage(bmp)
                Dim hdc As IntPtr = g.GetHdc()
                Dim ok As Boolean = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_CLIENTONLY)
                g.ReleaseHdc(hdc)
                If ok Then
                    Return bmp
                End If
            End Using

            Dim pt As New NativeMethods.POINT With {.X = 0, .Y = 0}
            If Not NativeMethods.ClientToScreen(hwnd, pt) Then
                bmp.Dispose()
                Return Nothing
            End If

            Using g As Graphics = Graphics.FromImage(bmp)
                g.CopyFromScreen(pt.X, pt.Y, 0, 0, New Size(width, height))
            End Using
            Return bmp
        Catch
            bmp.Dispose()
            Return Nothing
        End Try
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

        Dim columnMinPixels As Integer = Math.Max(1, CInt(Math.Ceiling(rect.Height * 0.12)))
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
            Return 0
        End If
        Return Math.Max(0, Math.Min(100, (rightMost + 1) * 100.0 / rect.Width))
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
End Class
