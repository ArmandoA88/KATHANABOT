Imports System.Reflection
Imports System.Drawing
Imports System.Text.Json
Imports System.Threading

Module Program
    Private Const InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private Const StaticFlags As BindingFlags = BindingFlags.Static Or BindingFlags.NonPublic
    <STAThread>
    Sub Main()
        TestBackground()
        TestKeyboardOnlyPolicy()
        TestKeysAndHold()
        TestSkillUi()
        Console.WriteLine("PASS: common input routing, compatibility and keyboard-only policies, shortcut sequences, cancellable loot hold, editable skill colors and profile roundtrip.")
    End Sub

    Private Sub TestKeyboardOnlyPolicy()
        Dim disabledNames = {"AutoLootForceForeground", "LootScannerEnabled", "LootPickupEnabled", "ArrowUnbundleEnabled",
                             "AutoPartyInviteEnabled", "AutoPartyMessageEnabled", "PartyAskEnabled", "AskForResurrectEnabled",
                             "LootRejectClickEnabled", "ResurrectAutoAcceptEnabled", "PartyInviteAutoAcceptEnabled",
                             "PartyRessAutoAcceptEnabled", "FullSupportTankEnabled", "FullSupportIndividualEnabled", "FullSupportPartyResurrectEnabled"}
        Dim retainedNames = {"EvadeDadatiEnabled", "LevelingAgentEnabled", "NavigationEnabled", "NavigationTravelExecutionEnabled", "HoldPlaceEnabled",
                             "LootAfterKillEnabled", "FullSupportModeEnabled", "FullSupportPartyHealEnabled", "FullSupportAssistEnabled",
                             "FullSupportSelfSurvivalEnabled", "BuffWatchEnabled", "BuffWatchSelfClickEnabled"}
        Dim profile As New BotConfig With {
            .AutoLootArrowHolds = New AutoLootArrowHoldSettings With {.LeftEnabled = True, .RightEnabled = True, .LeftHoldMs = 750, .RightHoldMs = 900},
            .Actions = New List(Of ActionRule) From {
                New ActionRule With {.Enabled = True, .KeyName = "CTRL+1"}, New ActionRule With {.Enabled = True, .KeyName = "ALT+2"},
                New ActionRule With {.Enabled = True, .KeyName = "W"}, New ActionRule With {.Enabled = True, .KeyName = "RALT"}, New ActionRule With {.Enabled = True, .KeyName = "8"}},
            .BuffWatchSlots = New List(Of BuffWatchSlot) From {New BuffWatchSlot With {.Enabled = True, .Name = "self buff", .KeyName = "CTRL+3", .SelfClickBeforeCast = True}}}
        For Each name In disabledNames.Concat(retainedNames)
            GetType(BotConfig).GetProperty(name).SetValue(profile, True)
        Next
        Dim profileJson = JsonSerializer.Serialize(profile)
        Dim runtimeConfig = JsonSerializer.Deserialize(Of BotConfig)(profileJson)
        Dim disabled = BackgroundModePolicy.ApplyKeyboardOnly(runtimeConfig)
        Check(New HashSet(Of String)(disabled).SetEquals(disabledNames), "keyboard-only policy must report every suppressed mouse/text feature")
        For Each name In disabledNames
            Check(Not CBool(GetType(BotConfig).GetProperty(name).GetValue(runtimeConfig)), "keyboard-only runtime left an unsupported feature enabled: " & name)
            Check(CBool(GetType(BotConfig).GetProperty(name).GetValue(profile)), "keyboard-only runtime changed a saved profile preference: " & name)
        Next
        For Each name In retainedNames
            Check(CBool(GetType(BotConfig).GetProperty(name).GetValue(runtimeConfig)), "keyboard-only runtime disabled a supported keyboard feature: " & name)
        Next
        Check(runtimeConfig.AutoLootArrowHolds.LeftEnabled AndAlso runtimeConfig.AutoLootArrowHolds.RightEnabled AndAlso
              runtimeConfig.AutoLootArrowHolds.LeftHoldMs = 750 AndAlso runtimeConfig.AutoLootArrowHolds.RightHoldMs = 900, "keyboard-only policy must preserve configured arrow holds")
        Check(runtimeConfig.Actions.All(Function(action) action.Enabled) AndAlso runtimeConfig.Actions.Select(Function(action) action.KeyName).SequenceEqual({"CTRL+1", "ALT+2", "W", "RALT", "8"}),
              "keyboard-only policy must preserve combat actions, chords and movement keys")
        Check(runtimeConfig.BuffWatchSlots.Count = 1 AndAlso runtimeConfig.BuffWatchSlots(0).Enabled AndAlso runtimeConfig.BuffWatchSlots(0).SelfClickBeforeCast AndAlso
              runtimeConfig.BuffWatchSlots(0).KeyName = "CTRL+3", "keyboard-only policy must preserve buff self-target key actions")
        Check(JsonSerializer.Serialize(profile) = profileJson AndAlso BackgroundModePolicy.ApplyKeyboardOnly(runtimeConfig).Count = 0,
              "runtime policy must leave profile values intact and report no repeated changes")

        Dim originalInput = WindowsInput.Current
        WindowsInput.Current = Nothing
        Try
            Check(WindowsInput.KeyboardOnlyMode, "production keyboard-only policy must be active")
            Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
            Using enabled As New CheckBox With {.Checked = True}
                GetType(Form1).GetField("chkAutoRelaunchGame", InstanceFlags).SetValue(owner, enabled)
                GetType(Form1).GetMethod("ScheduleGameRelaunch", InstanceFlags).Invoke(owner, {"keyboard-only regression", False, True})
                Check(Not CBool(GetType(Form1).GetField("_autoRelaunchPending", InstanceFlags).GetValue(owner)) AndAlso enabled.Checked,
                      "keyboard-only mode must reject even forced relaunch without changing the saved checkbox")
            End Using
        Finally
            WindowsInput.Current = originalInput
        End Try
    End Sub
    Private Sub Check(value As Boolean, message As String)
        If Not value Then Throw New Exception(message)
    End Sub
    Private Sub TestBackground()
        Dim cfg As New BotConfig With {.AutoLootForceForeground = True, .LootScannerEnabled = True, .HoldPlaceEnabled = True, .LootAfterKillEnabled = True,
            .Actions = New List(Of ActionRule) From {New ActionRule With {.KeyName = "CTRL+1"}, New ActionRule With {.KeyName = "8"}}}
        Dim disabled = BackgroundModePolicy.Apply(cfg)
        Check(disabled.Contains("Skill CTRL+1") AndAlso disabled.Contains("Max Range movement"), "disabled features are not reported")
        Check(Not cfg.Actions(0).Enabled AndAlso cfg.Actions(1).Enabled AndAlso cfg.LootAfterKillEnabled, "background policy must retain ordinary combat and looting")
        Check(Not cfg.LootScannerEnabled AndAlso Not cfg.AutoLootForceForeground, "foreground features remain on")
        Dim fake As New InputRecorder
        WindowsInput.Current = fake
        WindowsInput.BackgroundOnly = True
        Try
            Check(Not WindowsInput.BackgroundOnly, "legacy profiles must not re-enable background injection")
            Check(WindowsInput.Current.Activate(New IntPtr(123)), "injected foreground activation failed")
            Check(BotEngine.SendKey(New IntPtr(123), "W", 5), "movement did not use common input path")
            fake.Messages.Clear()
            Check(BotEngine.SendKey(New IntPtr(123), "8", 5, forceBackgroundPost:=True), "legacy callers did not use common input path")
            Check(fake.Messages.SequenceEqual({&H100UI, &H101UI}), "common key request sequence changed")
        Finally
            WindowsInput.BackgroundOnly = False
            WindowsInput.Current = Nothing
        End Try
    End Sub
    Private Sub TestKeysAndHold()
        For Each key In {"CTRL+0", "control + 9", "ALT+5"}
            Check(BotEngine.IsSupportedKeyName(key), "shortcut rejected: " & key)
        Next
        For Each key In {"CTRL+F1", "ALT+10", "CTRL+ALT+1", "foo"}
            Check(Not BotEngine.IsSupportedKeyName(key), "unsupported shortcut accepted")
        Next
        Dim fake As New InputRecorder
        WindowsInput.Current = fake
        Try
            Dim chord = GetType(BotEngine).GetMethod("SendPhysicalSkillShortcut", StaticFlags)
            Check(CBool(chord.Invoke(Nothing, {17, 49, 5, CancellationToken.None})), "chord failed")
            Check(fake.Physical.SequenceEqual({"17:0", "49:0", "49:2", "17:2"}), "modifier/key release order wrong")
            Dim watch = Diagnostics.Stopwatch.StartNew()
            Check(BotEngine.SendKey(New IntPtr(123), "F", 150), "held F failed")
            Check(watch.ElapsedMilliseconds >= 140, "F was tapped instead of held")
            fake.Messages.Clear()
            Using cancel As New CancellationTokenSource()
                fake.OnDown = Sub() cancel.Cancel()
                watch.Restart()
                Check(Not BotEngine.SendKey(New IntPtr(123), "F", 5000, cancellationToken:=cancel.Token), "cancelled hold returned success")
                Check(watch.ElapsedMilliseconds < 1000 AndAlso fake.Messages.SequenceEqual({&H100UI, &H101UI}), "cancel did not promptly release F")
            End Using
            Check(JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(New BotConfig With {.LootAfterKillHoldMs = 1750})).LootAfterKillHoldMs = 1750, "hold duration lost")
        Finally
            WindowsInput.Current = Nothing
        End Try
    End Sub
    Private Sub TestSkillUi()
        Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        GetType(Form1).GetField("_applyingSettings", InstanceFlags).SetValue(owner, True)
        Using group = DirectCast(GetType(Form1).GetMethod("BuildCombatSkillsGroup", InstanceFlags).Invoke(owner, Nothing), Control)
            Dim grid = DirectCast(GetType(Form1).GetField("dgvCombat", InstanceFlags).GetValue(owner), DataGridView)
            Dim add = GetType(Form1).GetMethod("AddEditableSkillRow", InstanceFlags)
            Dim fixedCount = DirectCast(GetType(Form1).GetField("PrimaryKeys", StaticFlags).GetValue(Nothing), String()).Length + DirectCast(GetType(Form1).GetField("FunctionKeys", StaticFlags).GetValue(Nothing), String()).Length
            Dim baseCount = fixedCount + DirectCast(GetType(Form1).GetField("CustomCombatDefaultKeys", StaticFlags).GetValue(Nothing), String()).Length
            For index = 0 To baseCount - 1
                Dim row = DirectCast(add.Invoke(owner, {"1"}), DataGridViewRow)
                row.Cells("Key").ReadOnly = index < fixedCount
            Next
            Dim custom = DirectCast(add.Invoke(owner, {"ALT+7"}), DataGridViewRow)
            custom.Cells("Enabled").Value = True
            Check(Not custom.Cells("Key").ReadOnly AndAlso grid.Rows(0).Cells("Key").ReadOnly, "custom keys are not editable independently")
            Dim format = GetType(Form1).GetMethod("FormatCombatSkill", InstanceFlags)
            Dim active = New DataGridViewCellFormattingEventArgs(2, custom.Index, "1", GetType(String), New DataGridViewCellStyle)
            format.Invoke(owner, {grid, active})
            Check(active.CellStyle.BackColor.G > active.CellStyle.BackColor.R, "enabled row is not green")
            Dim editable = New DataGridViewCellFormattingEventArgs(1, custom.Index, "ALT+7", GetType(String), New DataGridViewCellStyle)
            format.Invoke(owner, {grid, editable})
            Check(editable.CellStyle.BackColor.B > editable.CellStyle.BackColor.G, "editable key is not purple")
            Dim saved = GetType(Form1).GetMethod("GetPersistedCombatActions", InstanceFlags).Invoke(owner, Nothing)
            Dim restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(saved, saved.GetType()), saved.GetType())
            grid.Rows.RemoveAt(custom.Index)
            GetType(Form1).GetMethod("ApplyPersistedCombatActions", InstanceFlags).Invoke(owner, {restored})
            Check(grid.Rows.Count = baseCount + 1 AndAlso CStr(grid.Rows(baseCount).Cells("Key").Value) = "ALT+7" AndAlso CBool(grid.Rows(baseCount).Cells("Enabled").Value), "added skill lost after profile roundtrip")
            group.Size = New Size(720, 460)
            Using host As New Form With {.ClientSize = New Size(720, 460), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}
                host.Controls.Add(group)
                host.Show()
                Application.DoEvents()
                group.PerformLayout()
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, baseCount - 5)
                Using bitmap As New Bitmap(720, 460)
                    group.DrawToBitmap(bitmap, New Rectangle(0, 0, 720, 460))
                    bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "skill-controls.png"))
                End Using
            End Using
        End Using
    End Sub
    Private Class InputRecorder
        Implements IWindowsInput
        Public Messages As New List(Of UInteger)
        Public Physical As New List(Of String)
        Public GlobalCalls As Integer
        Public OnDown As Action
        Public Function Post(hwnd As IntPtr, message As UInteger, w As IntPtr, l As IntPtr) As Boolean Implements IWindowsInput.Post
            Messages.Add(message)
            If message = &H100UI Then OnDown?.Invoke()
            Return True
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            GlobalCalls += 1
            Return True
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            GlobalCalls += 1
            Return True
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            GlobalCalls += 1
            Physical.Add(key & ":" & flags)
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            GlobalCalls += 1
        End Sub
    End Class
End Module
