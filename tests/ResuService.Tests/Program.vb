Imports System.Text.Json

Module Program
    Private _passed As Integer
    Private _clock As DateTime = New DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)

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

    Private Sub Persistence()
        Dim settings As New ResuSettings With {.SelectKey = "1", .SelectKeyIntervalMs = 750, .ResurrectKey = "F7", .PeriodicMessageEnabled = True, .PeriodicMessageText = "Selling resurrection service", .PeriodicMessageIntervalSeconds = 45, .ResurrectPressCount = 25, .ResurrectBurstSeconds = 3.5D, .ReferenceWidth = 1024, .ReferenceHeight = 768, .AcceptPoint = New System.Drawing.Point(400, 500), .TradeRegion = New RectRegion(300, 200, 400, 400), .OpenTradeRegion = New RectRegion(100, 120, 700, 500)}
        settings.Blacklist.Add(New ResuBlacklistEntry With {.Username = "Alice", .Reason = "Unpaid", .AddedUtc = _clock})
        Dim loaded = JsonSerializer.Deserialize(Of ResuSettings)(JsonSerializer.Serialize(settings))
        Check(loaded.SelectKey = "1" AndAlso loaded.ResurrectKey = "F7", "Keys must persist")
        Check(loaded.SelectKeyIntervalMs = 750, "Select target key interval must persist")
        Check(loaded.PeriodicMessageEnabled AndAlso loaded.PeriodicMessageText = "Selling resurrection service" AndAlso loaded.PeriodicMessageIntervalSeconds = 45, "Periodic message settings must persist")
        Check(loaded.ResurrectPressCount = 25 AndAlso loaded.ResurrectBurstSeconds = 3.5D, "Resurrection spam settings must persist")
        Check(loaded.AcceptPoint.X = 400 AndAlso loaded.AcceptPoint.Y = 500 AndAlso loaded.TradeRegion.W = 400, "Calibration must persist")
        Check(loaded.OpenTradeRegion.X = 100 AndAlso loaded.OpenTradeRegion.W = 700, "Overlay 6 open-trade region must persist separately")
        Check(loaded.Blacklist(0).Username = "Alice" AndAlso loaded.Blacklist(0).Reason = "Unpaid", "Blacklist must persist")
    End Sub
End Module
