Imports System.Text.Json

Module Program
    Private _passed As Integer
    Private _clock As DateTime = New DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)

    <STAThread>
    Sub Main()
        Test("stable target required", AddressOf StableTarget)
        Test("blank target OCR waits before selecting again", AddressOf BlankTargetWaits)
        Test("target username extraction tolerates nameplate noise", AddressOf TargetUsernameExtraction)
        Test("blacklisted name skipped, case insensitive", AddressOf BlockedTarget)
        Test("confirmed payment releases pending customer", AddressOf Payment)
        Test("paying customer remains eligible for future resurrection", AddressOf RepeatCustomer)
        Test("wrong payer and substring name do not pay", AddressOf WrongPayer)
        Test("insufficient or malformed payment does not pay", AddressOf InsufficientPayment)
        Test("stale chat cannot pay for a new resurrection", AddressOf StalePayment)
        Test("one OCR scan cannot establish payment", AddressOf SingleScan)
        Test("confirmed nonpayment is persisted as a blacklist entry", AddressOf Nonpayment)
        Test("unrelated nonpayment does not blacklist", AddressOf WrongNonpayment)
        Test("payment deadline blacklists only confirmed resurrection", AddressOf PaymentTimeout)
        Test("unconfirmed resurrection never blacklists", AddressOf NoResurrectionConfirmation)
        Test("resurrection confirmation must name the pending player", AddressOf WrongConfirmation)
        Test("paused monitoring does not expire payment", AddressOf PausedMonitoring)
        Test("long OCR gaps do not expire payment", AddressOf SlowMonitoring)
        Test("any recognized trade is accepted", AddressOf AnyTradeAccepted)
        Test("trade OK repeats and stops when dialog disappears", AddressOf RepeatTrade)
        Test("completion stops clicks without assuming payment", AddressOf TradeCompletion)
        Test("cancellation stops clicks without assuming payment", AddressOf TradeCancellation)
        Test("trade is accepted without a pending resurrection", AddressOf TradeWithoutPendingResurrection)
        Test("manual blacklist during a transaction stops input", AddressOf BlockDuringTrade)
        Test("manual character-name list parsing", AddressOf ManualNames)
        Test("resurrection burst schedule", AddressOf ResurrectionBurst)
        Test("retarget toggles and configured role survive JSON roundtrip", AddressOf RetargetSettingsPersistence)
        Test("leveling navigation uses waypoint radius", AddressOf LevelingWaypointRadius)
        Test("leveling defaults are responsive and persist", AddressOf LevelingResponsiveDefaults)
        Test("leveling travel immediately learns map direction", AddressOf LevelingTravelDirectionCorrection)
        Test("leveling travel requires new coordinates between movement bursts", AddressOf LevelingTravelFreshCoordinateGate)
        Test("route recording samples survive status snapshots", AddressOf RouteRecordingSamplesInStatus)
        Test("loot grid maps OCR points to stable mesh cells", AddressOf LootGridGeometry)
        Test("loot pickup unavailable prompt tolerates OCR variants", AddressOf LootUnavailablePrompt)
        Test("blocked loot cell yields to nearest alternative then retries", AddressOf LootAlternativeSelection)
        Test("loot pickup requires a centered fresh observation", AddressOf CenteredLootPickup)
        Test("pickup timer cannot fire without a scanner observation", AddressOf PickupTimerWaitsForScanner)
        Test("hold-place slow loops cannot starve scheduled loot scans", AddressOf LootScannerSurvivesSlowLoops)
        Test("window freeze alerts confirm, deduplicate, recover and rearm", AddressOf WindowFreezeTransitions)
        Test("window freeze monitoring resets across gaps and target changes", AddressOf WindowFreezeResets)
        Test("window responsiveness monitor starts and stops independently", AddressOf WindowFreezeLifecycle)
        Test("native hung window triggers background freeze and recovery alerts", AddressOf NativeWindowFreeze)
        Test("overlay defaults match factory and embedded settings and preserve saved edits", AddressOf OverlayDefaultsAndPersistence)
        Test("overlay runtime coordinates stay equal to the calibration table", AddressOf OverlayRuntimeCoordinates)
        Test("arrow holds serialize directions and use release-based intervals", AddressOf ArrowHoldSchedule)
        Test("arrow hold settings persist and clamp invalid timings", AddressOf ArrowHoldPersistence)
        Test("auto-loot foreground toggle requires active Full auto-loot", AddressOf AutoLootForegroundToggle)
        Test("default loot list merges without losing custom entries", AddressOf DefaultLootCatalog)
        Test("dashboard icon ignores stale telemetry from either edition", AddressOf DashboardRunIcon)
        Test("invalid message patterns rejected", AddressOf InvalidPatterns)
        Test("settings and blacklist survive JSON roundtrip", AddressOf Persistence)
        Console.WriteLine($"Passed {_passed} RESU tests.")
    End Sub

    Private Sub Test(name As String, action As Action)
        action()
        _passed += 1
        Console.WriteLine("PASS " & name)
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Sub DashboardRunIcon()
        ' Exercise the real refresh path without starting engines or loading personal settings.
        Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
        Dim formType = GetType(Form1)
        Dim form = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(formType)
        Dim full As New BotEngine()
        Dim lite As New BotEngine()
        formType.GetField("_fullEngine", flags).SetValue(form, full)
        formType.GetField("_liteEngine", flags).SetValue(form, lite)
        Dim buttonType = formType.GetNestedType("CircleButton", Reflection.BindingFlags.NonPublic)
        Using button = DirectCast(Activator.CreateInstance(buttonType, True), System.Windows.Forms.Control)
            formType.GetField("btnDashPlayPause", flags).SetValue(form, button)
            Dim refresh = formType.GetMethod("UpdateDashboardUi", flags)
            Dim playing = buttonType.GetProperty("IsPlaying")
            Dim fullState = DirectCast(GetType(BotEngine).GetField("_status", flags).GetValue(full), BotStatus)
            Dim liteState = DirectCast(GetType(BotEngine).GetField("_status", flags).GetValue(lite), BotStatus)
            fullState.Running = True
            refresh.Invoke(form, New Object() {New BotStatus With {.Running = False}, BotEdition.Lite})
            Check(CBool(playing.GetValue(button)), "Stopped Lite telemetry cannot turn a running Full button into Start")
            fullState.Running = False
            refresh.Invoke(form, New Object() {New BotStatus With {.Running = True}, BotEdition.Full})
            Check(Not CBool(playing.GetValue(button)), "Queued running telemetry cannot leave Stop visible after stopping")
            liteState.Running = True
            refresh.Invoke(form, New Object() {New BotStatus With {.Running = False}, BotEdition.Full})
            Check(CBool(playing.GetValue(button)), "Running Lite must show Stop even when viewing Full")
            liteState.Running = False
            refresh.Invoke(form, New Object() {Nothing, BotEdition.Lite})
            Check(Not CBool(playing.GetValue(button)), "Missing telemetry must still synchronize the button")
        End Using
    End Sub

    Private Sub DefaultLootCatalog()
        Dim defaults = DefaultLootItems.Create()
        Check(defaults.Count = 81 AndAlso defaults.Distinct(StringComparer.OrdinalIgnoreCase).Count() = 81, "All 81 requested defaults must be unique")
        Dim merged = DefaultLootItems.Merge(New String() {"My Custom Item", "kindjal of thunder", "Mara's Kaja"})
        Check(merged.Count = 82 AndAlso merged.Contains("My Custom Item"), "Merge must retain custom items without duplicate default names or apostrophe variants")
        Check(DefaultLootItems.Merge(merged).SequenceEqual(merged), "Migration must be idempotent")
        Dim config As New BotConfig()
        Check(config.LootAllowedNames.SequenceEqual(defaults), "New configurations must contain all defaults")
        config.LootAllowedNames.Clear()
        Check(New BotConfig().LootAllowedNames.Count = 81, "Configurations must have independent editable lists")
    End Sub

    Private Sub CheckOverlayDefaults(cfg As BotConfig)
        Dim actual = New RectRegion() {cfg.HpBar, cfg.MpBar, cfg.MobNameRect, cfg.MobHpRect, cfg.MobLifeRect,
            cfg.UnreachableTextRect, cfg.PranaExpRect, cfg.RupiahsRect, cfg.PartyInviteScanRect,
            cfg.ResurrectDialogScanRect, cfg.DeathMessageScanRect, cfg.PartyListRect, cfg.DisconnectMessageRect,
            cfg.DisconnectOkRect, cfg.MapCoordinateXRect, cfg.MapCoordinateYRect, cfg.ChatRect, cfg.BuffAreaRect}
        Dim expected = New String() {"3,23,215,14", "3,39,216,10", "4,57,167,17", "3,75,215,12", "0,78,217,12",
            "11,505,400,97", "661,751,44,16", "1222,268,71,12", "516,325,328,122",
            "516,325,328,122", "516,325,328,122", "0,230,167,270", "516,325,328,122",
            "745,414,40,15", "859,499,25,18", "891,499,26,16", "13,604,392,104", "238,0,395,36"}
        For i = 0 To actual.Length - 1
            Dim r = actual(i)
            Check($"{r.X},{r.Y},{r.W},{r.H}" = expected(i), "Overlay default mismatch at index " & i)
        Next
        Check(String.Join("|", cfg.LootScanPoints.Select(Function(p) $"{p.X},{p.Y}")) = "457,736|31,112|1348,52|1362,717", "Loot Scan Area factory points mismatch")
    End Sub

    Private Sub OverlayDefaultsAndPersistence()
        CheckOverlayDefaults(New BotConfig())
        CheckOverlayDefaults(BotConfig.CreateDefault())
        CheckOverlayDefaults(JsonSerializer.Deserialize(Of BotConfig)("{}"))
        Using stream = GetType(BotConfig).Assembly.GetManifestResourceStream("KathanaBotControlPanel.DefaultUserSettings.json")
            Using json = JsonDocument.Parse(stream)
                CheckOverlayDefaults(JsonSerializer.Deserialize(Of BotConfig)(json.RootElement.GetProperty("Full").GetProperty("SavedConfig").GetRawText()))
            End Using
        End Using
        Dim cfg As New BotConfig()
        Dim properties = New String() {"ResurrectDialogScanRect", "DeathMessageScanRect", "PartyListRect", "DisconnectMessageRect", "DisconnectOkRect"}
        For Each name In properties
            GetType(BotConfig).GetProperty(name).SetValue(cfg, New RectRegion(41, 52, 63, 74))
        Next
        Dim loaded = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(cfg))
        BotConfig.MigrateLegacyVisionLayout(loaded)
        For Each name In properties
            Dim r = DirectCast(GetType(BotConfig).GetProperty(name).GetValue(loaded), RectRegion)
            Check($"{r.X},{r.Y},{r.W},{r.H}" = "41,52,63,74", "Saved user values must survive loading/migration for " & name)
            GetType(BotConfig).GetProperty(name).SetValue(loaded, Nothing)
        Next
        BotConfig.MigrateLegacyVisionLayout(loaded)
        CheckOverlayDefaults(loaded)
        loaded.PartyListRect.X = 99
        CheckOverlayDefaults(New BotConfig())
    End Sub

    Private Sub OverlayRuntimeCoordinates()
        Dim cfg As New BotConfig()
        Dim flags = Reflection.BindingFlags.Static Or Reflection.BindingFlags.NonPublic
        For Each size In New Integer() {1024, 1366, 1920}
            Dim args As Object() = {cfg, size, 1080, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing}
            GetType(BotEngine).GetMethod("ResolveVisionRegions", flags).Invoke(Nothing, args)
            Dim party = DirectCast(args(11), RectRegion)
            Dim disconnect = DirectCast(args(12), RectRegion)
            Check($"{party.X},{party.Y},{party.W},{party.H}" = "0,230,167,270", "Party region must not silently scale")
            Check($"{disconnect.X},{disconnect.Y},{disconnect.W},{disconnect.H}" = "516,325,328,122", "Disconnect region must not silently scale")
            Dim exp = DirectCast(args(8), RectRegion)
            Dim rupiah = DirectCast(args(9), RectRegion)
            Check($"{exp.X},{exp.Y},{exp.W},{exp.H}" = "661,751,44,16", "EXP region must not silently scale")
            Check($"{rupiah.X},{rupiah.Y},{rupiah.W},{rupiah.H}" = "1222,268,71,12", "Rupiah region must not silently scale")
            Dim ok = BotEngine.ResolveDisconnectOkRegion(cfg, size, 1080)
            Check($"{ok.X},{ok.Y},{ok.W},{ok.H}" = "745,414,40,15", "Disconnect OK must not silently scale")
        Next
    End Sub

    Private Sub NativeWindowFreeze()
        Using ready As New System.Threading.ManualResetEventSlim(), release As New System.Threading.ManualResetEventSlim(),
              frozen As New System.Threading.ManualResetEventSlim(), recovered As New System.Threading.ManualResetEventSlim()
            Dim window As System.Windows.Forms.Form = Nothing
            Dim hwnd As IntPtr
            Dim worker As New System.Threading.Thread(Sub()
                                                         Using testWindow As New System.Windows.Forms.Form()
                                                             window = testWindow
                                                             hwnd = testWindow.Handle
                                                             testWindow.BeginInvoke(New Action(Sub()
                                                                                                  ready.Set()
                                                                                                  Dim limit = Environment.TickCount64 + 50000
                                                                                                  While Not release.IsSet AndAlso Environment.TickCount64 < limit
                                                                                                      System.Threading.Thread.Sleep(20)
                                                                                                  End While
                                                                                              End Sub))
                                                             System.Windows.Forms.Application.Run()
                                                         End Using
                                                     End Sub)
            worker.IsBackground = True
            worker.SetApartmentState(System.Threading.ApartmentState.STA)
            Dim monitor As New GameWindowResponsivenessMonitor(Sub(hung)
                                                                   If hung Then
                                                                       frozen.Set()
                                                                   Else
                                                                       recovered.Set()
                                                                   End If
                                                               End Sub)
            worker.Start()
            Try
                Check(ready.Wait(TimeSpan.FromSeconds(5)), "Test window must start")
                monitor.Start(New BotConfig With {.SelectedWindowHandle = hwnd})
                Check(frozen.Wait(TimeSpan.FromSeconds(35)), "Independent timer must detect an actually hung Windows message loop")
                release.Set()
                Check(recovered.Wait(TimeSpan.FromSeconds(10)), "Resumed message loop must trigger recovery")
            Finally
                monitor.Stop()
                release.Set()
                If window IsNot Nothing AndAlso window.IsHandleCreated Then
                    window.BeginInvoke(New Action(Sub() System.Windows.Forms.Application.ExitThread()))
                End If
                worker.Join(5000)
            End Try
        End Using
    End Sub

    Private Sub WindowFreezeTransitions()
        Dim state As New GameWindowResponsivenessState()
        Dim hwnd As New IntPtr(123)
        For Each at In New Long() {0, 5000, 10000}
            Check(state.Observe(hwnd, True, at) = 0, "Short stalls must not alert")
        Next
        Check(state.Observe(hwnd, True, 15000) = 1, "Continuous 15-second hang must alert")
        For Each at In New Long() {20000, 25000, 30000}
            Check(state.Observe(hwnd, True, at) = 0, "One alert per incident")
        Next
        Check(state.Observe(hwnd, False, 35000) = -1, "Recovery must notify once")
        Check(state.Observe(hwnd, False, 40000) = 0, "Healthy window must not repeat recovery")
        For Each at In New Long() {45000, 50000, 55000}
            Check(state.Observe(hwnd, True, at) = 0, "New incident needs confirmation")
        Next
        Check(state.Observe(hwnd, True, 60000) = 1, "New incident must rearm")
        Dim reminderState As New GameWindowResponsivenessState()
        reminderState.Observe(hwnd, True, 0)
        reminderState.Observe(hwnd, True, 5000)
        reminderState.Observe(hwnd, True, 10000)
        Check(reminderState.Observe(hwnd, True, 15000) = 1, "Reminder test must confirm the initial freeze")
        Check(reminderState.Observe(hwnd, True, 314999) = 0, "An unresolved freeze must wait the full five minutes")
        Check(reminderState.Observe(hwnd, True, 315000) = 1, "An unresolved freeze must repeat after five minutes")
        Check(reminderState.Observe(hwnd, True, 320000) = 0, "A freeze reminder must not repeat early")
    End Sub

    Private Sub WindowFreezeResets()
        Dim state As New GameWindowResponsivenessState()
        Dim hwnd As New IntPtr(123)
        state.Observe(hwnd, True, 0)
        state.Observe(hwnd, True, 5000)
        Check(state.Observe(hwnd, False, 10000) = 0, "Brief stall recovery must not alert")
        state.Observe(hwnd, True, 15000)
        Check(state.Observe(hwnd, True, 60000) = 0, "Sleep or missing samples must not count as a hang")
        state.Observe(hwnd, True, 65000)
        Check(state.Observe(New IntPtr(456), True, 70000) = 0, "Switching window must reset confirmation")
        Check(state.Observe(IntPtr.Zero, False, 75000) = 0, "Closed window is not a recovered window")
        Check(state.Observe(hwnd, True, 80000) = 0, "Reappearing window needs fresh confirmation")
        state.Reset()
        Check(state.Observe(hwnd, False, 85000) = 0, "Stopped monitoring must not send recovery")
    End Sub

    Private Sub WindowFreezeLifecycle()
        Dim notifications As Integer = 0
        Dim monitor As New GameWindowResponsivenessMonitor(Sub(hung) notifications += 1)
        Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
        Dim monitorType = GetType(GameWindowResponsivenessMonitor)
        Using window As New System.Windows.Forms.Form()
            Dim cfg As New BotConfig With {.SelectedWindowHandle = window.Handle}
            monitor.Start(cfg)
            Check(monitorType.GetField("_timer", flags).GetValue(monitor) IsNot Nothing, "Start must create an independent timer")
            monitorType.GetMethod("CheckWindow", flags).Invoke(monitor, New Object() {Nothing})
            Check(notifications = 0, "Responsive native window must not alert")
            monitor.Stop()
            Check(monitorType.GetField("_timer", flags).GetValue(monitor) Is Nothing, "Stop must dispose timer")
            monitorType.GetMethod("CheckWindow", flags).Invoke(monitor, New Object() {Nothing})
            Check(notifications = 0, "Queued callback after stop must not alert")
        End Using
    End Sub

    Private Sub LootScannerSurvivesSlowLoops()
        Dim engine As New BotEngine()
        Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
        Dim engineType = GetType(BotEngine)
        Dim cfg As New BotConfig With {.HoldPlaceEnabled = True, .LootScannerEnabled = True, .LootScannerIntervalMs = 1000}
        engineType.GetField("_config", flags).SetValue(engine, cfg)
        Dim completeLoop = engineType.GetMethod("RecordLoopCompletion", flags)
        Dim due = engineType.GetMethod("IsLootScannerCaptureDue", flags)
        Dim hwnd As New IntPtr(123)
        Dim now = _clock
        For scan = 1 To 30
            completeLoop.Invoke(engine, New Object() {750.0R, 80})
            now = now.AddSeconds(1)
            Check(CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now})), "Scheduled loot must continue throughout slow hold-place loops")
            engineType.GetField("_lastRightAltAt", flags).SetValue(engine, now)
            Check(Not CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now.AddMilliseconds(999)})), "Scanner must respect its interval")
        Next
        Check(CBool(engineType.GetField("_adaptivePerformanceActive", flags).GetValue(engine)), "Regression must exercise sustained adaptive mode")
        now = now.AddSeconds(1)
        Check(Not CBool(due.Invoke(engine, New Object() {cfg, hwnd, IntPtr.Zero, now})), "Focus loss must block scans")
        engineType.GetField("_lootScannerCapturePending", flags).SetValue(engine, True)
        Check(Not CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now})), "Pending capture must not overlap")
        engineType.GetField("_lootScannerCapturePending", flags).SetValue(engine, False)
        Dim pending As New System.Threading.Tasks.TaskCompletionSource(Of Boolean)()
        engineType.GetField("_lootScannerProcessingTask", flags).SetValue(engine, pending.Task)
        Check(Not CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now})), "Busy OCR must not overlap")
        pending.SetResult(True)
        Check(CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now})), "Completed OCR must allow the next scan")
        cfg.LootScannerEnabled = False
        Check(Not CBool(due.Invoke(engine, New Object() {cfg, hwnd, hwnd, now})), "Disabled scanner must stay off")
    End Sub

    Private Sub AutoLootForegroundToggle()
        Dim config As New BotConfig()
        Check(Not BotEngine.ShouldForceAutoLootForeground(config), "Foreground forcing defaults off")
        config.AutoLootForceForeground = True
        Check(BotEngine.ShouldForceAutoLootForeground(config), "Enabled scanner permits foreground forcing")
        config.LootScannerEnabled = False
        Check(Not BotEngine.ShouldForceAutoLootForeground(config), "Inactive auto-loot must not take focus")
        config.LootPickupEnabled = True
        Check(BotEngine.ShouldForceAutoLootForeground(config), "Enabled pickup permits foreground forcing")
        config.LiteModeEnabled = True
        Check(Not BotEngine.ShouldForceAutoLootForeground(config), "Lite mode must not take focus for Full auto-loot")
        Dim loaded = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(config))
        Check(loaded.AutoLootForceForeground, "Foreground toggle must survive profile JSON")
    End Sub

    Private Sub ArrowHoldSchedule()
        Dim schedule As New AutoLootArrowHoldSchedule()
        Dim settings As New AutoLootArrowHoldSettings With {.LeftEnabled = True, .RightEnabled = True, .LeftIntervalMs = 2000, .RightIntervalMs = 5000}
        Dim now As New DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc)
        Check(schedule.TryStart(settings, now) = &H25, "Left starts first when both are initially due")
        Check(schedule.TryStart(settings, now.AddSeconds(1)) = 0, "Right cannot start while Left is held")
        schedule.Complete(now.AddSeconds(1))
        Check(schedule.TryStart(settings, now.AddSeconds(1)) = &H27, "Right starts after Left releases")
        schedule.Complete(now.AddSeconds(2))
        Check(schedule.TryStart(settings, now.AddSeconds(2.9)) = 0, "Left interval starts at release, not press")
        Check(schedule.TryStart(settings, now.AddSeconds(3)) = &H25, "Left uses its shorter independent interval")
        schedule.Complete(now.AddSeconds(3.5))
        settings.LeftEnabled = False
        Check(schedule.TryStart(settings, now.AddSeconds(6.9)) = 0, "Disabled Left stays off and Right waits its full interval")
        Check(schedule.TryStart(settings, now.AddSeconds(7)) = &H27, "Right uses its own five-second interval")
        schedule.Complete(now.AddSeconds(8))
        settings.RightEnabled = False
        Check(schedule.TryStart(settings, now.AddHours(1)) = 0, "Both disabled must produce no holds")
    End Sub

    Private Sub ArrowHoldPersistence()
        Dim config As New BotConfig With {.AutoLootArrowHolds = New AutoLootArrowHoldSettings With {
            .LeftEnabled = True, .RightEnabled = True, .LeftHoldMs = 700, .RightHoldMs = 1500,
            .LeftIntervalMs = 3300, .RightIntervalMs = 12900}}
        Dim loaded = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(config)).AutoLootArrowHolds
        Check(loaded.LeftEnabled AndAlso loaded.RightEnabled, "Enable switches must survive profile JSON")
        Check(loaded.LeftHoldMs = 700 AndAlso loaded.RightHoldMs = 1500 AndAlso loaded.LeftIntervalMs = 3300 AndAlso loaded.RightIntervalMs = 12900, "All independent timing values must survive profile JSON")
        Dim legacy = JsonSerializer.Deserialize(Of BotConfig)("{}")
        Check(Not legacy.AutoLootArrowHolds.LeftEnabled AndAlso Not legacy.AutoLootArrowHolds.RightEnabled, "Older profiles must leave arrow holds disabled")
        loaded.LeftHoldMs = -1
        loaded.RightHoldMs = Integer.MaxValue
        loaded.LeftIntervalMs = 0
        loaded.RightIntervalMs = Integer.MaxValue
        Check(loaded.HoldMs(&H25) = 100 AndAlso loaded.HoldMs(&H27) = 60000, "Hold duration must be bounded")
        Check(loaded.IntervalMs(&H25) = 100 AndAlso loaded.IntervalMs(&H27) = 3600000, "Wait interval must be bounded")
    End Sub

    Private Sub PickupTimerWaitsForScanner()
        Dim engine As New BotEngine()
        Dim config As New BotConfig With {.LootPickupEnabled = True, .LootScannerEnabled = True, .LootPickupIntervalMs = 1000,
            .LootAllowedNames = New List(Of String) From {"Test loot"}}
        Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
        Dim pickup = GetType(BotEngine).GetMethod("TryHandleLootPickup", flags)
        Dim lastPickup = GetType(BotEngine).GetField("_lastLootPickup", flags)
        pickup.Invoke(engine, New Object() {config, New IntPtr(-1), DateTime.UtcNow, False})
        Check(DirectCast(lastPickup.GetValue(engine), DateTime) = DateTime.MinValue, "An elapsed timer alone must not attempt F")
    End Sub

    Private Sub CenteredLootPickup()
        Dim size As New System.Drawing.Size(1000, 800)
        Check(BotEngine.IsLootPickupCentered(New System.Drawing.Point(500, 400), size), "Screen center must permit pickup")
        Check(BotEngine.IsLootPickupCentered(New System.Drawing.Point(550, 440), size), "Central 10% boundary must permit pickup")
        Check(Not BotEngine.IsLootPickupCentered(New System.Drawing.Point(551, 400), size), "Distant horizontal loot must not interrupt walking")
        Check(Not BotEngine.IsLootPickupCentered(New System.Drawing.Point(500, 441), size), "Distant vertical loot must not interrupt walking")
        Check(Not BotEngine.IsLootPickupCentered(New System.Drawing.Point(100, 100), size), "An offset scan area's center is not the screen center")
        Check(Not BotEngine.IsLootPickupCentered(System.Drawing.Point.Empty, System.Drawing.Size.Empty), "Missing frame must not permit pickup")
        Check(BotEngine.IsLootPickupCentered(New System.Drawing.Point(960, 540), New System.Drawing.Size(1920, 1080)), "Center must scale with window resolution")
        Dim now = DateTime.UtcNow
        Check(BotEngine.IsLootPickupObservationFresh(now.AddMilliseconds(-500), now), "Fresh OCR must permit pickup")
        Check(Not BotEngine.IsLootPickupObservationFresh(now.AddSeconds(-2), now), "Old OCR must not trigger F during later movement")
        Check(Not BotEngine.IsLootPickupObservationFresh(DateTime.MinValue, now), "No detection must never permit timed F")
        Check(Not BotEngine.IsLootPickupObservationFresh(now.AddSeconds(1), now), "Future timestamps must be rejected")
    End Sub

    Private Sub LootGridGeometry()
        Dim dense = BotEngine.GetLootGridCellAtPoint(800, 800, New System.Drawing.Point(799, 799), 40, 40)
        Check(dense = New System.Drawing.Rectangle(780, 780, 20, 20), "40x40 must retain all 1600 cells")
        Dim capped = BotEngine.GetLootGridCellAtPoint(800, 800, New System.Drawing.Point(799, 799), 99, 99)
        Check(capped = dense, "Oversized grids must clamp to 40x40")
        Dim middle As System.Drawing.Rectangle = BotEngine.GetLootGridCellAtPoint(600, 400, New System.Drawing.Point(350, 250), 6, 4)
        Check(middle = New System.Drawing.Rectangle(300, 200, 100, 100), "Point must map to its 6x4 cell")

        Dim last As System.Drawing.Rectangle = BotEngine.GetLootGridCellAtPoint(603, 401, New System.Drawing.Point(9999, 9999), 6, 4)
        Check(last.Right = 603 AndAlso last.Bottom = 401, "Out-of-range OCR points must clamp to the final cell")

        Dim first As System.Drawing.Rectangle = BotEngine.GetLootGridCellAtPoint(603, 401, New System.Drawing.Point(-50, -50), 6, 4)
        Check(first.Left = 0 AndAlso first.Top = 0, "Negative OCR points must clamp to the first cell")
    End Sub

    Private Sub LootUnavailablePrompt()
        Check(BotEngine.IsLootPickupUnavailablePrompt("Cannot pick up item yet"), "Exact pickup warning must match")
        Check(BotEngine.IsLootPickupUnavailablePrompt("Cannot pickup item yet."), "Missing space and punctuation must match")
        Check(BotEngine.IsLootPickupUnavailablePrompt("Cannot pick up itern yet"), "Minor OCR errors must match fuzzily")
        Check(Not BotEngine.IsLootPickupUnavailablePrompt("Unable to reach target"), "Combat unreachable text must stay separate")
    End Sub

    Private Sub LootAlternativeSelection()
        Dim method = GetType(BotEngine).GetMethod("TryFindAllowedLootGridMatch", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Static)
        Dim regions As New List(Of OcrReader.OcrTextRegion) From {
            New OcrReader.OcrTextRegion With {.Text = "Strong Ara", .Bounds = New System.Drawing.Rectangle(40, 40, 20, 10)},
            New OcrReader.OcrTextRegion With {.Text = "Agni Kisu", .Bounds = New System.Drawing.Rectangle(240, 40, 20, 10)},
            New OcrReader.OcrTextRegion With {.Text = "Hima Zalas", .Bounds = New System.Drawing.Rectangle(440, 40, 20, 10)}
        }
        Dim allowed As New List(Of String) From {"Strong Ara", "Agni Kisu", "Hima Zalas"}
        Dim blocked As New System.Drawing.Rectangle(0, 0, 100, 100)
        Dim characterPoint As New System.Drawing.Point(500, 50)
        Dim nearestArgs As Object() = {regions, allowed, 80, 1000, 100, 10, 1, "", System.Drawing.Rectangle.Empty, System.Drawing.Point.Empty, System.Drawing.Rectangle.Empty, characterPoint}
        Check(CBool(method.Invoke(Nothing, nearestArgs)), "A valid nearest item must be selected")
        Check(CStr(nearestArgs(7)) = "Hima Zalas", "Normal scans must select the allowed item closest to screen center")

        Dim args As Object() = {regions, allowed, 80, 1000, 100, 10, 1, "", System.Drawing.Rectangle.Empty, System.Drawing.Point.Empty, blocked, characterPoint}
        Check(CBool(method.Invoke(Nothing, args)), "A valid alternative must be selected")
        Check(CStr(args(7)) = "Hima Zalas" AndAlso DirectCast(args(8), System.Drawing.Rectangle).X = 400, "Alternative closest to the character at screen center must win")

        Dim onlyBlocked As New List(Of OcrReader.OcrTextRegion) From {regions(0)}
        args = New Object() {onlyBlocked, allowed, 80, 1000, 100, 10, 1, "", System.Drawing.Rectangle.Empty, System.Drawing.Point.Empty, blocked, characterPoint}
        Check(CBool(method.Invoke(Nothing, args)), "Blocked item must remain retryable when no alternative exists")
        Check(CStr(args(7)) = "Strong Ara" AndAlso DirectCast(args(8), System.Drawing.Rectangle) = blocked, "The same blocked cell must be retried")
    End Sub

    Private Function Tick(service As ResuService, Optional chat As String = "", Optional messages As String = "", Optional trade As String = "", Optional target As String = "Alice", Optional seconds As Double = 1) As ResuDecision
        _clock = _clock.AddSeconds(seconds)
        Return service.Observe(New ResuObservation With {.TargetName = target, .InvitationText = trade, .ChatText = chat, .MessageText = messages, .TradeText = trade}, _clock)
    End Function

    Private Function ActualTradeWindowText() As String
        Return "Trade" & vbCrLf & "Trade" & vbCrLf & "RADION (Lv 100) ENFERMERAJOY" & vbCrLf & "Rupiah Rupiah" & vbCrLf & "Trade Trade Cancel"
    End Function

    Private Function Casting(settings As ResuSettings, Optional history As String = "") As ResuService
        Dim service As New ResuService(settings)
        Tick(service, chat:=history)
        Dim selectAction = Tick(service, chat:=history)
        Check(selectAction.Action = ResuAction.SelectTarget, "Must select before reading a target")
        service.ActionSucceeded(selectAction)
        Tick(service, chat:=history)
        Dim cast = Tick(service, chat:=history)
        Check(cast.Action = ResuAction.Resurrect AndAlso cast.Username = "Alice", "Expected a stable Alice target")
        service.ActionSucceeded(cast)
        Return service
    End Function

    Private Sub Confirm(service As ResuService, Optional chat As String = "")
        Tick(service, chat:=chat, messages:="You resurrected Alice.")
        Tick(service, chat:=chat, messages:="You resurrected Alice.")
    End Sub

    Private Sub StableTarget()
        Dim service As New ResuService(New ResuSettings())
        Tick(service)
        service.ActionSucceeded(Tick(service))
        Check(Tick(service).Action = ResuAction.None, "First target scan must not cast")
        Check(Tick(service, target:="Bob").Action = ResuAction.None, "Changing target must restart confirmation")
        Check(Tick(service, target:="Bob").Username = "Bob", "Second stable target scan should cast")
        Check(ResuService.CleanUsername("Alice" & vbLf & "Bob") = "", "Multiline OCR cannot be a username")
    End Sub

    Private Sub BlankTargetWaits()
        Dim service As New ResuService(New ResuSettings())
        Tick(service)
        Dim selectAction = Tick(service)
        service.ActionSucceeded(selectAction)
        For index = 1 To 3
            Check(Tick(service, target:="").Action = ResuAction.None, "A temporary blank target read must keep the current target")
        Next
        Check(Tick(service, target:="").Action = ResuAction.SelectTarget, "Four consecutive blank reads should select another target")
    End Sub

    Private Sub TargetUsernameExtraction()
        Check(ResuService.ExtractTargetUsername("Lv. 120 | Alice") = "Alice", "Leading level text should be removed")
        Check(ResuService.ExtractTargetUsername("[Alice]") = "Alice", "Surrounding OCR punctuation should be ignored")
        Check(ResuService.ExtractTargetUsername("Alice Lv120") = "Alice", "Trailing level text should be removed")
        Check(ResuService.ExtractTargetUsername("Guild Alice") = "", "Ambiguous multi-token OCR must not guess an identity")
    End Sub

    Private Sub BlockedTarget()
        Dim settings As New ResuSettings()
        settings.Blacklist.Add(New ResuBlacklistEntry With {.Username = "ALICE"})
        Dim service As New ResuService(settings)
        Tick(service)
        service.ActionSucceeded(Tick(service))
        Check(Tick(service, target:="alice").Action = ResuAction.SelectTarget, "Blocked target must be skipped")
        Check(Not service.IsBlocked("Alice2"), "Partial names must not match blacklist")
    End Sub

    Private Sub Payment()
        Dim settings As New ResuSettings With {.MinimumPayment = 1000}
        Dim service = Casting(settings)
        Confirm(service)
        Tick(service, chat:="aLiCe paid 1,000 rupiahs.")
        Tick(service, chat:="aLiCe paid 1,000 rupiahs.")
        Check(service.PendingUsername = "", "Payment must settle transaction")
        Check(settings.Blacklist.Count = 0, "Payer must not be blacklisted")
    End Sub

    Private Sub WrongPayer()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        For Each name In {"Bob", "Alice2"}
            Tick(service, chat:=name & " paid 100 rupiahs.")
            Tick(service, chat:=name & " paid 100 rupiahs.")
        Next
        Check(service.PendingUsername = "Alice", "Other players cannot settle Alice's debt")
    End Sub

    Private Sub RepeatCustomer()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        Tick(service, chat:="Alice paid 100 rupiahs.")
        Tick(service, chat:="Alice paid 100 rupiahs.")
        service.ActionSucceeded(Tick(service))
        Check(Tick(service).Action = ResuAction.SelectTarget, "Avoid immediately recasting on the same player")
        Tick(service, seconds:=30)
        Check(Tick(service).Action = ResuAction.Resurrect, "Paying player must become eligible again after cooldown")
    End Sub

    Private Sub InsufficientPayment()
        Dim service = Casting(New ResuSettings With {.MinimumPayment = 100})
        Confirm(service)
        For Each amount In {"0", "99", "999999999999999999999999999999"}
            Tick(service, chat:="Alice paid " & amount & " rupiahs.")
            Tick(service, chat:="Alice paid " & amount & " rupiahs.")
        Next
        Check(service.PendingUsername = "Alice", "Insufficient and overflowing amounts cannot settle")
    End Sub

    Private Sub StalePayment()
        Const history As String = "Alice paid 100 rupiahs."
        Dim service = Casting(New ResuSettings(), history)
        Confirm(service, history)
        Tick(service, chat:=history)
        Tick(service, chat:=history)
        Check(service.PendingUsername = "Alice", "Visible old payment cannot settle")
    End Sub

    Private Sub SingleScan()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        Tick(service, chat:="Alice paid 100 rupiahs.")
        Check(service.PendingUsername = "Alice", "A payment requires two consistent OCR scans")
        Tick(service)
        Tick(service, chat:="Alice paid 100 rupiahs.")
        Check(service.PendingUsername = "Alice", "Non-consecutive payment scans must not settle")
    End Sub

    Private Sub Nonpayment()
        Dim settings As New ResuSettings()
        Dim service = Casting(settings)
        Confirm(service)
        Tick(service, chat:="Alice did not pay.")
        Tick(service, chat:="Alice did not pay.")
        Check(service.IsBlocked("Alice") AndAlso service.BlacklistChanged, "Nonpayer should enter blacklist")
        Check(service.PendingUsername = "", "Blacklisting should finish the transaction")
        Check(settings.Blacklist.Count = 1 AndAlso settings.Blacklist(0).Reason.Contains("Explicit"), "Keep reason for review")
    End Sub

    Private Sub WrongNonpayment()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        Tick(service, chat:="Bob did not pay.")
        Tick(service, chat:="Bob did not pay.")
        Check(Not service.IsBlocked("Bob") AndAlso Not service.IsBlocked("Alice"), "Unrelated messages must not blacklist")
    End Sub

    Private Sub PaymentTimeout()
        Dim service = Casting(New ResuSettings With {.PaymentTimeoutSeconds = 10})
        Confirm(service)
        For index = 1 To 10
            Tick(service)
        Next
        Check(service.IsBlocked("Alice"), "Confirmed resurrection should expire into blacklist")
    End Sub

    Private Sub NoResurrectionConfirmation()
        Dim settings As New ResuSettings With {.PaymentTimeoutSeconds = 10}
        Dim service = Casting(settings)
        For index = 1 To 16
            Tick(service)
        Next
        Check(service.PendingUsername = "" AndAlso settings.Blacklist.Count = 0, "Failed/unconfirmed casts must not blacklist")
    End Sub

    Private Sub WrongConfirmation()
        Dim settings As New ResuSettings With {.PaymentTimeoutSeconds = 10}
        Dim service = Casting(settings)
        For index = 1 To 16
            Tick(service, messages:="You resurrected Bob.")
        Next
        Check(settings.Blacklist.Count = 0, "Wrong-player confirmation must not start a debt")
    End Sub

    Private Sub PausedMonitoring()
        Dim service = Casting(New ResuSettings With {.PaymentTimeoutSeconds = 10})
        Confirm(service)
        For index = 1 To 5
            Tick(service)
        Next
        service.PauseMonitoring()
        Tick(service, seconds:=3600)
        Check(Not service.IsBlocked("Alice") AndAlso service.PendingUsername = "Alice", "Focus pause must not consume deadline")
    End Sub

    Private Sub SlowMonitoring()
        Dim service = Casting(New ResuSettings With {.PaymentTimeoutSeconds = 10})
        Confirm(service)
        Tick(service, seconds:=60)
        Check(Not service.IsBlocked("Alice"), "A long OCR gap must not count as monitored time")
    End Sub

    Private Sub AnyTradeAccepted()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        Check(Tick(service, trade:="Request trade with Bob?").Action = ResuAction.AcceptInvite, "Accept an invitation regardless of the pending resurrection name")
        Check(Tick(service, trade:="Message" & vbCrLf & "Request trade with Alice?" & vbCrLf & "OK  Cancel").Action = ResuAction.AcceptInvite, "Accept the game's actual invitation wording inside a multi-line dialog")
        Dim actualWindow = ActualTradeWindowText()
        Check(Tick(service, trade:=actualWindow).Action = ResuAction.AcceptTrade, "Recognize the actual trade window whose confirmation button is labeled Trade")
        Check(Tick(service, trade:="Gold  Item" & vbCrLf & "0K  Cancel").Action = ResuAction.AcceptTrade, "An OCR-visible OK button must identify an open trade when its title is unreadable")
        Check(ResuService.MatchesTrade(New ResuSettings(), "Request trade with Alice ?", "Alice", True), "Invitation OCR may insert whitespace before punctuation")
        Dim legacy As New ResuSettings With {.InvitePattern = ResuService.LegacyDefaultInvitePattern}
        Check(ResuService.MatchesTrade(legacy, "Trade request from Alice", "Alice", True), "User-supplied legacy patterns must remain usable")
        Check(ResuService.HasTradeType(New ResuSettings(), actualWindow, False), "Trade revalidation must recognize the actual window layout")
    End Sub

    Private Sub RepeatTrade()
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        For index = 1 To 3
            Dim click = Tick(service, trade:=ActualTradeWindowText())
            Check(click.Action = ResuAction.AcceptTrade, "Trade OK should repeat while visible")
            service.ActionSucceeded(click)
        Next
        Check(Tick(service).Action = ResuAction.None, "No blind clicks after the trade disappears")
    End Sub

    Private Sub TradeCompletion()
        ClosedTrade("Trade completed.")
    End Sub

    Private Sub TradeCancellation()
        ClosedTrade("Trade cancelled.")
    End Sub

    Private Sub ClosedTrade(message As String)
        Dim service = Casting(New ResuSettings())
        Confirm(service)
        Check(Tick(service, trade:=ActualTradeWindowText(), messages:=message).Action = ResuAction.None, "First closure read must stop clicks immediately")
        Check(Tick(service, trade:=ActualTradeWindowText(), messages:=message).Action = ResuAction.None, "Completion/cancellation must stop repeated clicks")
        Check(Tick(service, trade:=ActualTradeWindowText(), messages:=message).Action = ResuAction.None, "Closed trade must remain stopped")
        Check(service.PendingUsername = "Alice", "Closed/empty trades are not proof of payment")
    End Sub

    Private Sub TradeWithoutPendingResurrection()
        Dim service As New ResuService(New ResuSettings())
        Tick(service)
        Check(Tick(service, trade:="Items" & vbCrLf & "OK Cancel").Action = ResuAction.AcceptTrade, "Accept an OCR-visible trade OK button even without a pending resurrection")
    End Sub

    Private Sub BlockDuringTrade()
        Dim settings As New ResuSettings()
        Dim service = Casting(settings)
        settings.Blacklist.Add(New ResuBlacklistEntry With {.Username = "Alice"})
        Check(Tick(service, trade:="Trade with Alice").Action = ResuAction.None, "Manual blacklist should prevent further input")
        Check(service.PendingUsername = "", "Blocked transaction should clear")
    End Sub

    Private Sub InvalidPatterns()
        For Each settings In {New ResuSettings With {.PaidPattern = "paid"}, New ResuSettings With {.TradePattern = "("}, New ResuSettings With {.MinimumPayment = 0}, New ResuSettings With {.SelectKeyIntervalMs = 49}, New ResuSettings With {.SelectKeyIntervalMs = 10001}, New ResuSettings With {.PeriodicMessageEnabled = True, .PeriodicMessageText = ""}, New ResuSettings With {.PeriodicMessageIntervalSeconds = 0}, New ResuSettings With {.PeriodicMessageIntervalSeconds = 86401}, New ResuSettings With {.PeriodicMessageText = New String("x"c, 201)}, New ResuSettings With {.ResurrectPressCount = 0}, New ResuSettings With {.ResurrectPressCount = 101}, New ResuSettings With {.ResurrectBurstSeconds = 31D}}
            Dim rejected = False
            Try
                Dim service As New ResuService(settings)
            Catch ex As ArgumentException
                rejected = True
            End Try
            Check(rejected, "Invalid settings must fail before input starts")
        Next
    End Sub

    Private Sub ManualNames()
        Dim invalid As New List(Of String)()
        Dim names = ResuService.ParseManualCharacterNames(" Alice,BOB;alice" & vbCrLf & "Bad Name" & vbLf & "Player_3-Alt", invalid)
        Check(names.SequenceEqual({"Alice", "BOB", "Player_3-Alt"}), "Manual names should support lines, commas, semicolons, and case-insensitive deduplication")
        Check(invalid.SequenceEqual({"Bad Name"}), "Invalid character names should be reported")
    End Sub

    Private Sub ResurrectionBurst()
        Check(ResuService.ResurrectionBurstOffsetMs(0, 10, 1D) = 0, "First key press should be immediate")
        Check(ResuService.ResurrectionBurstOffsetMs(9, 10, 1D) = 1000, "Last key press should begin at the configured duration")
        Check(ResuService.ResurrectionBurstOffsetMs(5, 11, 2D) = 1000, "Intermediate presses should be spread evenly")
        Check(ResuService.ResurrectionBurstOffsetMs(0, 1, 30D) = 0, "A single press should not wait")
        Check(ResuService.ResurrectionBurstOffsetMs(4, 5, 0D) = 0, "Zero duration should schedule the fastest burst")
    End Sub

    Private Sub LevelingWaypointRadius()
        Dim node As New NavigationNode With {.X = 100, .Y = 100}
        Check(BotEngine.IsNavigationNodeReached(103, 104, node, 5), "A 3-4-5 offset must count as reached")
        Check(Not BotEngine.IsNavigationNodeReached(104, 104, node, 5), "A point outside the radius must not count as reached")
        Check(BotEngine.IsNavigationNodeReached(100, 100, node, 0), "Zero radius must still permit exact matches")
        Check(Not BotEngine.IsNavigationNodeReached(-1, 100, node, 100), "Unknown coordinates must never count as reached")
    End Sub

    Private Sub LevelingResponsiveDefaults()
        Dim cfg As New BotConfig()
        Check(cfg.RetargetMs = 300 AndAlso cfg.ForcedRetargetMs = 300, "Default targeting must respond quickly")
        Check(cfg.LevelingMaxNoTargetSeconds = 30, "No-target guardrail must not wait ten minutes")
        Check(cfg.NavigationWaypointReachRadius = 6, "Navigation needs a small arrival tolerance")
        Check(cfg.NavigationMoveBurstMs = 240 AndAlso cfg.NavigationResampleIntervalMs = 500, "Travel must use short, frequently corrected movement")
        Check(cfg.NavigationStallTimeoutMs = 3500 AndAlso cfg.NavigationRepathOnStuck, "Travel must recover promptly")
        Dim loaded = JsonSerializer.Deserialize(Of BotConfig)(JsonSerializer.Serialize(cfg))
        Check(loaded.NavigationMoveBurstMs = 240 AndAlso loaded.NavigationResampleIntervalMs = 500 AndAlso loaded.NavigationWaypointReachRadius = 6, "Recommended values must persist")
        Using stream = GetType(BotConfig).Assembly.GetManifestResourceStream("KathanaBotControlPanel.DefaultUserSettings.json")
            Using json = JsonDocument.Parse(stream)
                Dim embedded = JsonSerializer.Deserialize(Of BotConfig)(json.RootElement.GetProperty("Full").GetProperty("SavedConfig").GetRawText())
                Check(embedded.RetargetMs = 300 AndAlso embedded.ForcedRetargetMs = 300, "First-run targeting defaults must match")
                Check(embedded.LevelingMaxNoTargetSeconds = 30 AndAlso embedded.NavigationWaypointReachRadius = 6, "First-run leveling defaults must match")
                Check(embedded.NavigationMoveBurstMs = 240 AndAlso embedded.NavigationResampleIntervalMs = 500 AndAlso embedded.NavigationStallTimeoutMs = 3500, "First-run travel defaults must match")
            End Using
        End Using
    End Sub

    Private Sub LevelingTravelDirectionCorrection()
        Dim engine As New BotEngine()
        Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
        Dim engineType As Type = GetType(BotEngine)
        Dim cfg As New BotConfig With {
            .LevelingAgentEnabled = True,
            .NavigationEnabled = True,
            .NavigationTravelExecutionEnabled = True
        }
        engineType.GetField("_config", flags).SetValue(engine, cfg)
        engineType.GetField("_lastTravelInputKey", flags).SetValue(engine, "W")
        engineType.GetField("_lastTravelInputDesiredDirection", flags).SetValue(engine, "N")
        engineType.GetField("_lastTravelInputPoseX", flags).SetValue(engine, 100)
        engineType.GetField("_lastTravelInputPoseY", flags).SetValue(engine, 100)
        engineType.GetField("_lastTravelInputAt", flags).SetValue(engine, _clock)
        engineType.GetField("_lastTravelInputIsHoldCorrection", flags).SetValue(engine, False)

        engineType.GetMethod("ObserveNavigationOrientation", flags).Invoke(engine, New Object() {_clock.AddMilliseconds(100), 101, 100})

        Check(CInt(engineType.GetField("_navigationRotationQuarterTurns", flags).GetValue(engine)) = 1,
              "Leveling must correct its movement mapping after the first reliable coordinate change")
    End Sub

    Private Sub RouteRecordingSamplesInStatus()
        Dim engine As New BotEngine()
        Dim source As New BotStatus With {
            .NavigationNextWaypointX = 124,
            .NavigationNextWaypointY = 457,
            .RouteRecordingSampleCount = 2,
            .RouteRecordingSamples = New List(Of NavigationRouteSample) From {
                New NavigationRouteSample With {.X = 123, .Y = 456, .CapturedAtUtc = _clock},
                New NavigationRouteSample With {.X = 124, .Y = 457, .CapturedAtUtc = _clock.AddSeconds(1)}
            }
        }
        Dim cloneMethod = GetType(BotEngine).GetMethod("CloneStatus", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        Dim snapshot As BotStatus = DirectCast(cloneMethod.Invoke(engine, New Object() {source}), BotStatus)

        Check(snapshot.RouteRecordingSamples.Count = 2, "The UI status snapshot must contain recorded breadcrumb coordinates")
        Check(snapshot.RouteRecordingSamples(0).X = 123 AndAlso snapshot.RouteRecordingSamples(1).Y = 457,
              "The breadcrumb coordinates must remain intact")
        Check(Not Object.ReferenceEquals(snapshot.RouteRecordingSamples, source.RouteRecordingSamples),
              "The status snapshot must own a safe copy of the breadcrumb list")
        Check(snapshot.NavigationNextWaypointX = 124 AndAlso snapshot.NavigationNextWaypointY = 457,
              "The UI status snapshot must identify the breadcrumb the bot is walking toward")
    End Sub

    Private Sub LevelingTravelFreshCoordinateGate()
        Dim movedAt As DateTime = _clock.AddSeconds(1)
        Check(BotEngine.NavigationMovementNeedsFreshCoordinate(movedAt, _clock),
              "Travel must not repeat a direction from an older coordinate")
        Check(BotEngine.NavigationMovementNeedsFreshCoordinate(movedAt, movedAt),
              "Travel must wait when no coordinate was accepted after movement")
        Check(Not BotEngine.NavigationMovementNeedsFreshCoordinate(movedAt, movedAt.AddMilliseconds(1)),
              "Travel may steer again after a fresh coordinate is accepted")
        Dim routeStart As New NavigationNode With {.X = 100, .Y = 100}
        Dim routeEnd As New NavigationNode With {.X = 120, .Y = 100}
        Check(Math.Abs(BotEngine.DistanceFromRouteSegment(110, 104, routeStart, routeEnd) - 4.0) < 0.01,
              "Wander leash distance must be measured from the route segment")
        Check(Math.Abs(BotEngine.DistanceFromRouteSegment(125, 100, routeStart, routeEnd) - 5.0) < 0.01,
              "Wander leash must account for passing beyond a route endpoint")
        Check((New BotConfig()).MapCoordinateScanIntervalMs = 250,
              "Leveling needs fast coordinate scans for route correction")
    End Sub

    Private Sub RetargetSettingsPersistence()
        Dim defaults As New BotConfig()
        Check(defaults.NormalRetargetEnabled AndAlso defaults.ForcedRetargetEnabled, "Both automatic retarget modes must default to enabled")
        defaults.NormalRetargetEnabled = False
        defaults.ForcedRetargetEnabled = False
        defaults.LootGridColumns = 40
        defaults.LootGridRows = 40
        defaults.ResuHoldPlaceOnlyModeEnabled = True
        defaults.Actions = New List(Of ActionRule) From {
            New ActionRule With {.Enabled = True, .KeyName = "E", .Role = "retarget", .CooldownMs = 750, .Priority = 1}
        }
        Dim json As String = JsonSerializer.Serialize(defaults)
        Dim loaded As BotConfig = JsonSerializer.Deserialize(Of BotConfig)(json)
        Check(Not loaded.NormalRetargetEnabled AndAlso Not loaded.ForcedRetargetEnabled, "Disabled automatic retarget modes must persist")
        Check(loaded.LootGridColumns = 40 AndAlso loaded.LootGridRows = 40, "Loot mesh dimensions must persist in the profile config")
        Check(Not json.Contains(NameOf(BotConfig.ResuHoldPlaceOnlyModeEnabled), StringComparison.Ordinal) AndAlso Not loaded.ResuHoldPlaceOnlyModeEnabled, "Runtime RESU Hold-only mode must never persist in a profile")
        Check(loaded.Actions.Count = 1 AndAlso loaded.Actions(0).Enabled AndAlso loaded.Actions(0).KeyName = "E" AndAlso loaded.Actions(0).Role = "retarget", "Configured retarget action must persist")
    End Sub

    Private Sub Persistence()
        Dim settings As New ResuSettings With {.SelectKey = "1", .SelectKeyIntervalMs = 750, .ResurrectKey = "F7", .BuffKeys = New List(Of ResuBuffKeySetting) From {New ResuBuffKeySetting With {.Enabled = True, .KeyName = "F8"}, New ResuBuffKeySetting With {.Enabled = False, .KeyName = "F9"}, New ResuBuffKeySetting With {.Enabled = True, .KeyName = "0"}}, .PeriodicMessageEnabled = True, .PeriodicMessageText = "Selling resurrection service", .PeriodicMessageIntervalSeconds = 45, .ResurrectPressCount = 25, .ResurrectBurstSeconds = 3.5D, .ReferenceWidth = 1024, .ReferenceHeight = 768, .AcceptPoint = New System.Drawing.Point(400, 500), .TradeRegion = New RectRegion(300, 200, 400, 400), .OpenTradeRegion = New RectRegion(100, 120, 700, 500)}
        settings.Blacklist.Add(New ResuBlacklistEntry With {.Username = "Alice", .Reason = "Unpaid", .AddedUtc = _clock})
        Dim loaded = JsonSerializer.Deserialize(Of ResuSettings)(JsonSerializer.Serialize(settings))
        Check(loaded.SelectKey = "1" AndAlso loaded.ResurrectKey = "F7", "Keys must persist")
        Check(loaded.BuffKeys.Count = 3 AndAlso loaded.BuffKeys(0).Enabled AndAlso loaded.BuffKeys(0).KeyName = "F8" AndAlso Not loaded.BuffKeys(1).Enabled AndAlso loaded.BuffKeys(2).KeyName = "0", "Optional RESU buff keys and toggles must persist")
        Check(loaded.SelectKeyIntervalMs = 750, "Select target key interval must persist")
        Check(loaded.PeriodicMessageEnabled AndAlso loaded.PeriodicMessageText = "Selling resurrection service" AndAlso loaded.PeriodicMessageIntervalSeconds = 45, "Periodic message settings must persist")
        Check(loaded.ResurrectPressCount = 25 AndAlso loaded.ResurrectBurstSeconds = 3.5D, "Resurrection spam settings must persist")
        Check(loaded.AcceptPoint.X = 400 AndAlso loaded.AcceptPoint.Y = 500 AndAlso loaded.TradeRegion.W = 400, "Calibration must persist")
        Check(loaded.OpenTradeRegion.X = 100 AndAlso loaded.OpenTradeRegion.W = 700, "Overlay 6 open-trade region must persist separately")
        Check(loaded.Blacklist(0).Username = "Alice" AndAlso loaded.Blacklist(0).Reason = "Unpaid", "Blacklist must persist")
    End Sub
End Module
