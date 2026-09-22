Imports System.Threading
Imports System.Reflection
Imports System.Text.Json
Imports System.Drawing
Module Program
    Private ReadOnly Flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    <STAThread>
    Sub Main()
        Dim keys As New List(Of String)
        Dim times As New List(Of Long)
        Dim watch = Diagnostics.Stopwatch.StartNew()
        Using cancel As New CancellationTokenSource
            Dim send As Func(Of String, Boolean) = Function(key)
                                                      keys.Add(key)
                                                      times.Add(watch.ElapsedMilliseconds)
                                                      If keys.Count = 6 Then cancel.Cancel()
                                                      Return True
                                                  End Function
            Try
                DirectKpSequence.RunAsync(send, Function() True, Function() 200, cancel.Token).GetAwaiter().GetResult()
            Catch ex As OperationCanceledException
            End Try
            Check(keys.SequenceEqual({"E", "R", "E", "R", "E", "R"}), "E/R order changed")
            For index = 1 To times.Count - 1
                Check(times(index) - times(index - 1) >= 95, "Key spacing shorter than 100 ms tolerance")
            Next
            Check(Not DirectKpSequence.SendPair(send, Function() True, cancel.Token), "Cancelled stream sent keys")
        End Using
        keys.Clear()
        Using cancel As New CancellationTokenSource
            DirectKpSequence.SendPair(Function(key)
                                          keys.Add(key)
                                          cancel.Cancel()
                                          Return True
                                      End Function, Function() True, cancel.Token)
            Check(keys.SequenceEqual({"E"}), "Stop after E did not cancel pending R")
        End Using
        keys.Clear()
        DirectKpSequence.SendPair(Function(key)
                                      keys.Add(key)
                                      Return False
                                  End Function, Function() True, CancellationToken.None)
        Check(keys.SequenceEqual({"E"}), "Failed E still sent R")
        keys.Clear()
        DirectKpSequence.SendPair(Function(key)
                                      keys.Add(key)
                                      Return True
                                  End Function, Function() keys.Count = 0, CancellationToken.None)
        Check(keys.SequenceEqual({"E"}), "Pause after E did not suppress R")
        keys.Clear()
        DirectKpSequence.SendPair(Function(key)
                                      keys.Add(key)
                                      Return True
                                  End Function, Function() False, CancellationToken.None)
        Check(keys.Count = 0, "Paused stream sent keys")
        Dim cfg As New BotConfig With {.DirectKpEnabled = True, .DirectKpIntervalMs = 600, .NormalRetargetEnabled = True, .ForcedRetargetEnabled = True,
            .Actions = New List(Of ActionRule) From {New ActionRule With {.Enabled = True, .Role = "retarget", .KeyName = "3"}}}
        Dim engine As New BotEngine
        For Each forced As Boolean In {False, True}
            Check(Not CBool(GetType(BotEngine).GetMethod("TrySendRetargetKey", Flags).Invoke(engine, {New IntPtr(123), cfg, DateTime.UtcNow, "test", forced})), "Automatic retarget was not bypassed")
        Next
        Check(Not CBool(GetType(BotEngine).GetMethod("TrySendConfiguredRetargetAction", Flags).Invoke(engine, {New IntPtr(123), cfg, "test"})), "Configured retarget was not bypassed")
        GetType(BotEngine).GetField("_config", Flags).SetValue(engine, cfg)
        Check(Not engine.ManualRetarget("test"), "Manual retarget was not bypassed")
        Dim restored = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(cfg))
        Check(Not restored.DirectKpEnabled AndAlso restored.DirectKpIntervalMs = 600, "Profile restored active mode or lost interval")
        cfg.Actions.Add(New ActionRule With {.Enabled = True, .Role = "attack", .KeyName = "4"})
        Dim attackArgs As Object() = {cfg, 100.0R, 100.0R, False, False, False, True, ""}
        Dim attacks = DirectCast(GetType(BotEngine).GetMethod("ChooseAttackBurstActions", Flags).Invoke(engine, attackArgs), List(Of ActionRule))
        Check(attacks.Any(Function(action) action.KeyName = "4"), "Configured attacks blocked by missing target in Direct KP")
        Using notice As New DirectKpNoticeForm("WARNING: monster filters and target-based features may not work correctly.")
            notice.StartPosition = FormStartPosition.Manual
            notice.Location = New Point(-30000, -30000)
            AddHandler notice.Shown, Sub()
                                         Using bitmap As New Bitmap(notice.Width, notice.Height)
                                             notice.DrawToBitmap(bitmap, New Rectangle(0, 0, notice.Width, notice.Height))
                                             bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "kp-notice.png"))
                                         End Using
                                     End Sub
            watch.Restart()
            Check(notice.ShowDialog() = DialogResult.OK AndAlso watch.ElapsedMilliseconds >= 5000, "Warning activated before five seconds")
        End Using
        Using notice As New DirectKpNoticeForm("Cancel test"), timer As New System.Windows.Forms.Timer With {.Interval = 50}
            AddHandler timer.Tick, Sub()
                                      timer.Stop()
                                      notice.DialogResult = DialogResult.Cancel
                                      notice.Close()
                                  End Sub
            timer.Start()
            Check(notice.ShowDialog() = DialogResult.Cancel, "Warning cannot be cancelled")
        End Using
        TestSettingsSafety()
        TestSidebarStartupLayout()
        TestSkillCards()
        RenderControls()
        Console.WriteLine("PASS: E/R timing/order, repeated cycles, stop between keys, input failure, pause, targeting bypass, interval persistence and Direct KP layout.")
    End Sub
    Private Sub TestSettingsSafety()
        Dim form = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        GetType(Form1).GetField("_applyingSettings", Flags).SetValue(form, True)
        ' No controls, timers or engines: partial-load handlers must return without touching them.
        GetType(Form1).GetMethod("PushLiveConfig", Flags).Invoke(form, Nothing)
        GetType(Form1).GetMethod("SavePersistedListState", Flags).Invoke(form, {True, True})
        Dim stateType = GetType(Form1).GetNestedType("PersistedAppState", BindingFlags.NonPublic)
        Dim emptyJson = CStr(GetType(Form1).GetMethod("BuildEmptySettingsJson", BindingFlags.Static Or BindingFlags.NonPublic).Invoke(Nothing, Nothing))
        Check(JsonSerializer.Deserialize(emptyJson, stateType) IsNot Nothing, "Empty settings cannot reload")
        Using empty = JsonDocument.Parse(emptyJson)
            Dim full = empty.RootElement.GetProperty("Full")
            Check(Not full.GetProperty("ArrowUnbundleEnabled").GetBoolean(), "Reset enables unbundling")
            Check(Not full.GetProperty("PartyInviteAutoAcceptEnabled").GetBoolean(), "Reset enables party acceptance")
            Check(full.GetProperty("BuffWatchSlots").GetArrayLength() = 0, "Reset retains buff slots")
            Check(full.GetProperty("LootNames").GetArrayLength() = 0, "Reset retains loot names")
            Dim blankConfig = JsonSerializer.Deserialize(Of BotConfig)(full.GetProperty("SavedConfig").GetRawText())
            Check(blankConfig.HpBar.X = 0 AndAlso blankConfig.HpBar.W = 1, "Reset retains calibration")
        End Using
        Dim state = Activator.CreateInstance(stateType, True)
        stateType.GetProperty("LiteDirectKpSelected").SetValue(state, True)
        Dim json = JsonSerializer.Serialize(state, stateType)
        Dim restored = JsonSerializer.Deserialize(json, stateType)
        Check(CBool(stateType.GetProperty("LiteDirectKpSelected").GetValue(restored)), "Saved Lite mode lost")
        Using panel As New Panel(), toggle As New CheckBox With {.Checked = True}, number As New NumericUpDown With {.Minimum = 20, .Value = 80}, text As New TextBox With {.Text = "old setup"}
            panel.Controls.AddRange({toggle, number, text})
            GetType(Form1).GetMethod("ClearSetupControls", Flags).Invoke(form, {panel})
            Check(Not toggle.Checked AndAlso number.Value = 20 AndAlso text.Text = "", "Reset failed to clear setup controls")
            Check(number.Text <> "", "Reset cleared the numeric editor's internal text")
        End Using
        Dim alert = GetType(Form1).GetMethod("GetSurfaceAlertState", BindingFlags.Static Or BindingFlags.NonPublic)
        Dim dead As New BotStatus With {.WindowFound = True, .HpPercent = 0}
        Check(CInt(alert.Invoke(Nothing, {dead, True, True, True})) = 2, "Zero HP did not flash red")
        Check(CInt(alert.Invoke(Nothing, {dead, True, True, False})) = 3, "Zero HP did not alternate flash")
        dead.HpPercent = 100
        Check(CInt(alert.Invoke(Nothing, {dead, True, True, True})) = 0, "Recovered HP did not clear alert")
        Check(CInt(alert.Invoke(Nothing, {dead, False, True, True})) = 1, "Stopped bot is not red")
        dead.HpPercent = 0
        dead.ErrorMessage = "capture failed"
        Check(CInt(alert.Invoke(Nothing, {dead, True, True, True})) = 0, "Invalid capture triggered death alert")
        Dim cardType = GetType(Form1).GetNestedType("DashboardCard", BindingFlags.NonPublic)
        Using card = DirectCast(Activator.CreateInstance(cardType, True), Panel)
            card.Size = New Size(360, 200)
            cardType.GetMethod("SetSecondaryHeight").Invoke(card, {32})
            cardType.GetMethod("SetDailyProjection").Invoke(card, {"1,234,567/day"})
            cardType.GetMethod("SetGraphHeight").Invoke(card, {64})
            DirectCast(cardType.GetField("_sparkline", Flags).GetValue(card), Control).Visible = True
            card.PerformLayout()
            Dim daily = DirectCast(cardType.GetField("_lblDaily", Flags).GetValue(card), Label)
            Dim wallet = DirectCast(cardType.GetField("_lblValue", Flags).GetValue(card), Label)
            Check(daily.Font.Size = wallet.Font.Size, "Daily projection does not match wallet size")
            Check(wallet.Height >= wallet.Font.Height, "Daily projection crowds wallet text")
            Check(daily.Height >= daily.Font.Height, "Daily projection text clips vertically")
        End Using
        Dim cfg As New BotConfig With {.HpBar = New RectRegion(11, 25, 151, 11), .MpBar = New RectRegion(3, 40, 161, 11),
            .MobNameRect = New RectRegion(860, 711, 162, 23), .MobHpRect = New RectRegion(859, 737, 165, 11),
            .UnreachableTextRect = New RectRegion(15, 582, 128, 22)}
        Dim expected = JsonSerializer.Serialize(cfg.HpBar) & JsonSerializer.Serialize(cfg.MobHpRect) & JsonSerializer.Serialize(cfg.UnreachableTextRect)
        For index = 1 To 20
            cfg = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(cfg))
            BotConfig.MigrateLegacyVisionLayout(cfg)
            Check(expected = JsonSerializer.Serialize(cfg.HpBar) & JsonSerializer.Serialize(cfg.MobHpRect) & JsonSerializer.Serialize(cfg.UnreachableTextRect), "Saved calibration changed on reload")
        Next
    End Sub

    Private Sub TestSidebarStartupLayout()
        Dim tabType = GetType(Form1).GetNestedType("SidebarTabControl", BindingFlags.NonPublic)
        Using host As New Form With {.ClientSize = New Size(1450, 900), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}, tabs = DirectCast(Activator.CreateInstance(tabType, True), TabControl)
            tabs.Dock = DockStyle.Fill
            tabs.Alignment = TabAlignment.Left
            tabs.SizeMode = TabSizeMode.Fixed
            tabs.ItemSize = New Size(70, 122)
            For index = 0 To 11
                tabs.TabPages.Add(New TabPage(If(index = 0, "Home", "Page " & index)))
            Next
            Dim home = tabs.TabPages(0)
            Dim content As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.Navy}
            Dim status As New Label With {.Text = "Ready", .Dock = DockStyle.Fill}
            content.Controls.Add(status)
            home.Controls.Add(content)
            host.Controls.Add(tabs)
            host.Show()
            For Each size In {New Size(1450, 900), New Size(1900, 970), New Size(1200, 750)}
                host.ClientSize = size
                Application.DoEvents()
                tabType.GetMethod("FitTabsToHeight").Invoke(tabs, Nothing)
                Application.DoEvents()
                status.Text = "FULL RUNNING"
                tabs.Refresh()
                Check(tabs.SelectedTab Is home, "Layout changed selected tab")
                Check(home.Bounds = tabs.DisplayRectangle, "Home retained stale native page bounds")
                Check(content.Bounds = home.ClientRectangle AndAlso status.Text = "FULL RUNNING", "Home children did not fill the page/update after startup layout")
            Next
        End Using
    End Sub
    Private Sub TestSkillCards()
        Using grid As New DataGridView With {.AllowUserToAddRows = False}
            grid.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Enabled"})
            grid.Columns.Add("Key", "Key")
            grid.Columns.Add("CooldownSec", "CooldownSec")
            Dim roles As New DataGridViewComboBoxColumn With {.Name = "Role"}
            roles.Items.AddRange({"attack", "retarget/assist", "heal", "max_health", "mana", "buff", "high_max_hp", "repair", "stop"})
            grid.Columns.Add(roles)
            grid.Columns.Add("Priority", "Priority")
            grid.Columns.Add("TriggerPercent", "TriggerPercent")
            grid.Rows.Add(True, "1", "0.3", "attack", "10", "80")
            grid.Rows.Add(False, "F9", "5", "repair", "20", "1")
            Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
            Using group As New GroupBox, skillHost As New Panel
                skillHost.Controls.Add(grid)
                group.Controls.Add(skillHost)
                GetType(Form1).GetField("dgvCombat", Flags).SetValue(owner, grid)
                GetType(Form1).GetField("_combatSkillHost", Flags).SetValue(owner, skillHost)
                GetType(Form1).GetField("_combatSkillsGroup", Flags).SetValue(owner, group)
                GetType(Form1).GetField("_directKpEnabled", Flags).SetValue(owner, True)
                GetType(Form1).GetMethod("UpdateLightCombatEditor", Flags).Invoke(owner, Nothing)
                Check(group.Text.Contains("LITE Direct KP"), "Mode label missing")
                GetType(Form1).GetField("_directKpEnabled", Flags).SetValue(owner, False)
                GetType(Form1).GetMethod("UpdateLightCombatEditor", Flags).Invoke(owner, Nothing)
                Check(group.Text.StartsWith("Full") AndAlso CStr(grid.Rows(0).Cells("CooldownSec").Value) = "0.3", "Returning to Full lost a skill value")
                skillHost.Controls.Remove(grid)
            End Using
            Using cards As New CombatSkillCards(grid) With {.Dock = DockStyle.Fill}, host As New Form With {.ClientSize = New Size(500, 400), .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}
                host.Controls.Add(cards)
                host.Show()
                Application.DoEvents()
                Dim first = cards.Controls(0)
                Dim role = first.Controls.OfType(Of ComboBox)().Single()
                Check(role.Items.Count = 9, "Light editor lost Full roles")
                role.SelectedItem = "high_max_hp"
                first.Controls.OfType(Of TextBox)().Single(Function(box) box.Name = "CooldownSec").Text = "0.75"
                first.Controls.OfType(Of TextBox)().Single(Function(box) box.Name = "Priority").Text = "42"
                first.Controls.OfType(Of CheckBox)().Single().Checked = False
                Check(CStr(grid.Rows(0).Cells("Role").Value) = "high_max_hp" AndAlso CStr(grid.Rows(0).Cells("CooldownSec").Value) = "0.75" AndAlso CStr(grid.Rows(0).Cells("Priority").Value) = "42" AndAlso Not CBool(grid.Rows(0).Cells("Enabled").Value), "Card edits did not reach original Full rows")
                grid.Rows(0).Cells("TriggerPercent").Value = "65"
                Check(first.Controls.OfType(Of TextBox)().Single(Function(box) box.Name = "TriggerPercent").Text = "65", "Profile/grid changes did not refresh card")
                grid.Rows.Add(True, "F10", "2", "buff", "30", "1")
                Check(cards.Controls.Count = 3, "Added profile row missing")
                Using bitmap As New Bitmap(host.Width, host.Height)
                    host.DrawToBitmap(bitmap, New Rectangle(0, 0, host.Width, host.Height))
                    bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "light-skill-cards.png"))
                End Using
            End Using
        End Using
    End Sub
    Private Sub RenderControls()
        Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim panel = DirectCast(GetType(Form1).GetMethod("BuildDirectKpControls", Flags).Invoke(owner, Nothing), Control)
        Using host As New Form With {.ClientSize = New Size(530, 250), .ShowInTaskbar = False, .Location = New Point(-30000, -30000), .StartPosition = FormStartPosition.Manual}
            host.Controls.Add(panel)
            host.Show()
            Application.DoEvents()
            Dim button = DirectCast(GetType(Form1).GetField("_directKpButton", Flags).GetValue(owner), Button)
            Dim input = DirectCast(GetType(Form1).GetField("_directKpInterval", Flags).GetValue(owner), NumericUpDown)
            Check(input.Minimum = 200, "Unsafe minimum cycle")
            Check(button.Text = "LITE Direct KP: OFF", "Initial state is not off")
            Check(button.MinimumSize.Height = 38 AndAlso button.Height <= 38, "KP button exceeds standard button height")
            Dim offColor = button.BackColor
            GetType(Form1).GetMethod("ApplyDarkTheme", Flags).Invoke(owner, {panel})
            Check(button.BackColor = offColor, "Theme hid the toggle state")
            GetType(Form1).GetField("_directKpEnabled", Flags).SetValue(owner, True)
            GetType(Form1).GetMethod("UpdateDirectKpButton", Flags).Invoke(owner, Nothing)
            Check(button.Text = "LITE Direct KP: ON" AndAlso button.BackColor <> offColor, "On/off appearance missing")
            For Each control As Control In panel.Controls
                Check(host.ClientRectangle.Contains(host.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "Control clipped")
            Next
            Using bitmap As New Bitmap(host.Width, host.Height)
                host.DrawToBitmap(bitmap, New Rectangle(0, 0, host.Width, host.Height))
                bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "direct-kp.png"))
            End Using
        End Using
    End Sub
    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub
End Module
