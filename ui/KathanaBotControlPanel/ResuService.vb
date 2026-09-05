Imports System.Globalization
Imports System.Text.RegularExpressions

' Local OCR only. Keeping the decision logic separate from input makes payment and blacklist
' rules testable without a game window or sending any keys/clicks.
Friend Class ResuSettings
    Public Property SelectKey As String = "TAB"
    Public Property SelectKeyIntervalMs As Integer = 500
    Public Property ResurrectKey As String = ""
    Public Property PeriodicMessageEnabled As Boolean
    Public Property PeriodicMessageText As String = ""
    Public Property PeriodicMessageIntervalSeconds As Integer = 60
    Public Property ResurrectPressCount As Integer = 10
    Public Property ResurrectBurstSeconds As Decimal = 1D
    Public Property ScanMs As Integer = 500
    Public Property PaymentTimeoutSeconds As Integer = 60
    Public Property MinimumPayment As Long = 1
    Public Property ReferenceWidth As Integer
    Public Property ReferenceHeight As Integer
    Public Property TargetRegion As RectRegion
    Public Property TradeRegion As RectRegion
    Public Property OpenTradeRegion As RectRegion
    Public Property ChatRegion As RectRegion
    Public Property MessageRegion As RectRegion
    Public Property InvitePoint As System.Drawing.Point = New System.Drawing.Point(-1, -1)
    Public Property AcceptPoint As System.Drawing.Point = New System.Drawing.Point(-1, -1)
    ' Examples, editable to match the actual server's wording. Each identity is an exact name,
    ' never a fuzzy OCR match. Payment requires a positive amount, not just a closed window.
    Public Property InvitePattern As String = ResuService.DefaultInvitePattern
    Public Property TradePattern As String = ResuService.DefaultTradePattern
    Public Property ResurrectedPattern As String = "^You resurrected (?<user>[\p{L}\p{N}_-]+)\.?$"
    Public Property PaidPattern As String = "^(?<user>[\p{L}\p{N}_-]+) paid (?<amount>[0-9,]+) rupiahs\.?$"
    Public Property UnpaidPattern As String = "^(?<user>[\p{L}\p{N}_-]+) did not pay\.?$"
    Public Property TradeClosedPattern As String = "^Trade (completed|cancelled|canceled)\.?$"
    Public Property Blacklist As New List(Of ResuBlacklistEntry)
End Class

Friend Class ResuBlacklistEntry
    Public Property Username As String = ""
    Public Property Reason As String = ""
    Public Property AddedUtc As DateTime
End Class

Friend Class ResuObservation
    Public Property TargetName As String = ""
    Public Property InvitationText As String = ""
    Public Property TradeText As String = ""
    Public Property ChatText As String = ""
    Public Property MessageText As String = ""
End Class

Friend Enum ResuAction
    None
    SelectTarget
    Resurrect
    AcceptInvite
    AcceptTrade
End Enum

Friend Class ResuDecision
    Public Property Action As ResuAction
    Public Property Username As String = ""
End Class

Friend NotInheritable Class ResuService
    Public Const LegacyDefaultInvitePattern As String = "^Trade request from (?<user>[\p{L}\p{N}_-]+)$"
    Public Const DefaultInvitePattern As String = "\bRequest\s+trade\s+with\s+(?<user>[\p{L}\p{N}_-]+)"
    Public Const LegacyDefaultTradePattern As String = "^Trade with (?<user>[\p{L}\p{N}_-]+)$"
    Public Const DefaultTradePattern As String = "^(?!.*\bRequest\s+trade\s+with\b).*\bTrade\b(?=[\s\S]*(?:\bRupiah\b|\bCancel\b))"
    Private ReadOnly _settings As ResuSettings
    Private ReadOnly _invite As Regex
    Private ReadOnly _trade As Regex
    Private ReadOnly _resurrected As Regex
    Private ReadOnly _paid As Regex
    Private ReadOnly _unpaid As Regex
    Private ReadOnly _closed As Regex
    Private ReadOnly _seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private _previousLines As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private _initialized As Boolean
    Private _selected As Boolean
    Private _target As String = ""
    Private _targetScans As Integer
    Private _emptyTargetScans As Integer
    Private _pending As String = ""
    Private _confirmed As Boolean
    Private _waitSeconds As Double
    Private _lastObservation As DateTime
    Private _lastAttempted As String = ""
    Private _targetRetryAfter As DateTime
    Private _tradeClosed As Boolean

    Public Property Status As String = "Waiting for the first OCR scan."
    Public Property BlacklistChanged As Boolean
    Public ReadOnly Property PendingUsername As String
        Get
            Return _pending
        End Get
    End Property

    Public Sub New(settings As ResuSettings)
        _settings = settings
        _invite = Compile(settings.InvitePattern, True)
        _trade = Compile(settings.TradePattern, False)
        _resurrected = Compile(settings.ResurrectedPattern, True)
        _paid = Compile(settings.PaidPattern, True, True)
        _unpaid = Compile(settings.UnpaidPattern, True)
        _closed = Compile(settings.TradeClosedPattern, False)
        If settings.PaymentTimeoutSeconds < 10 OrElse settings.MinimumPayment < 1 Then Throw New ArgumentException("Set a payment timeout of at least 10 seconds and a positive minimum payment.")
        If settings.SelectKeyIntervalMs < 50 OrElse settings.SelectKeyIntervalMs > 10000 Then Throw New ArgumentException("Select target key interval must be between 50 and 10,000 milliseconds.")
        If settings.PeriodicMessageIntervalSeconds < 1 OrElse settings.PeriodicMessageIntervalSeconds > 86400 Then Throw New ArgumentException("Periodic message interval must be between 1 and 86,400 seconds.")
        If settings.PeriodicMessageEnabled AndAlso String.IsNullOrWhiteSpace(settings.PeriodicMessageText) Then Throw New ArgumentException("Type a periodic message or turn periodic messaging off.")
        If If(settings.PeriodicMessageText, "").Length > 200 Then Throw New ArgumentException("Periodic message text cannot exceed 200 characters.")
        If settings.ResurrectPressCount < 1 OrElse settings.ResurrectPressCount > 100 Then Throw New ArgumentException("Resurrection key presses must be between 1 and 100.")
        If settings.ResurrectBurstSeconds < 0D OrElse settings.ResurrectBurstSeconds > 30D Then Throw New ArgumentException("Resurrection burst duration must be between 0 and 30 seconds.")
    End Sub

    Private Shared Function Compile(pattern As String, needsUser As Boolean, Optional needsAmount As Boolean = False) As Regex
        If String.IsNullOrWhiteSpace(pattern) Then Throw New ArgumentException("Every message pattern must be configured.")
        Dim result As New Regex(pattern, RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))
        If needsUser AndAlso Not result.GetGroupNames().Contains("user") Then Throw New ArgumentException("Message patterns need a (?<user>...) group for the username.")
        If needsAmount AndAlso Not result.GetGroupNames().Contains("amount") Then Throw New ArgumentException("The paid pattern needs an (?<amount>...) group for the payment.")
        Return result
    End Function

    Public Shared Function CleanUsername(value As String) As String
        value = If(value, "").Trim()
        If Regex.IsMatch(value, "\A[\p{L}\p{N}_-]{1,32}\z") Then Return value
        Return ""
    End Function

    Public Shared Function ExtractTargetUsername(value As String) As String
        Dim cleaned = Regex.Replace(If(value, ""), "\s+", " ").Trim()
        If cleaned.Length = 0 Then Return ""
        cleaned = Regex.Replace(cleaned, "^Lv\s*\.?\s*\d{1,3}\s*(?:[|:\-]\s*)?", "", RegexOptions.IgnoreCase).Trim()
        cleaned = Regex.Replace(cleaned, "\s*(?:[|:\-]\s*)?Lv\s*\.?\s*\d{1,3}$", "", RegexOptions.IgnoreCase).Trim()
        Dim exact = CleanUsername(cleaned)
        If exact.Length > 0 Then Return exact
        Dim matches = Regex.Matches(cleaned, "[\p{L}\p{N}_-]{1,32}").Cast(Of Match)().Select(Function(item) item.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        If matches.Count = 1 Then Return matches(0)
        Return ""
    End Function

    Public Shared Function ParseManualCharacterNames(value As String, invalid As List(Of String)) As List(Of String)
        If invalid Is Nothing Then Throw New ArgumentNullException(NameOf(invalid))
        Dim names As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each candidate In If(value, "").Split({ControlChars.Cr, ControlChars.Lf, ","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)
            Dim trimmed = candidate.Trim()
            If trimmed.Length = 0 Then Continue For
            Dim name = CleanUsername(trimmed)
            If name.Length = 0 Then
                invalid.Add(trimmed)
            ElseIf seen.Add(name) Then
                names.Add(name)
            End If
        Next
        Return names
    End Function

    ' Scheduled from first key-down to the start of the last key press. Key-hold time may make
    ' the final release a few milliseconds later than the configured span.
    Public Shared Function ResurrectionBurstOffsetMs(pressIndex As Integer, pressCount As Integer, durationSeconds As Decimal) As Integer
        If pressCount < 1 OrElse pressCount > 100 Then Throw New ArgumentOutOfRangeException(NameOf(pressCount))
        If pressIndex < 0 OrElse pressIndex >= pressCount Then Throw New ArgumentOutOfRangeException(NameOf(pressIndex))
        If durationSeconds < 0D OrElse durationSeconds > 30D Then Throw New ArgumentOutOfRangeException(NameOf(durationSeconds))
        If pressCount = 1 Then Return 0
        Return CInt(Math.Round(pressIndex * durationSeconds * 1000D / (pressCount - 1), MidpointRounding.AwayFromZero))
    End Function

    Public Function IsBlocked(username As String) As Boolean
        Return _settings.Blacklist.Any(Function(entry) String.Equals(entry.Username, username, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Sub PauseMonitoring()
        _lastObservation = DateTime.MinValue
        _previousLines.Clear()
        _targetScans = 0
        _emptyTargetScans = 0
    End Sub

    Public Shared Function HasTrade(settings As ResuSettings, text As String) As Boolean
        Return HasTradeType(settings, text, True) OrElse HasTradeType(settings, text, False)
    End Function

    Public Shared Function HasTradeType(settings As ResuSettings, text As String, invitation As Boolean) As Boolean
        Dim invite = Compile(settings.InvitePattern, True)
        If invitation Then Return PatternMatches(invite, text)
        Dim trade = Compile(settings.TradePattern, False)
        If PatternMatches(trade, text) Then Return True
        ' Invitation dialogs also contain OK/Cancel. Keep them routed to InvitePoint; after that
        ' dialog disappears, an OK/0K label anywhere in the calibrated trade region identifies the
        ' open trade window even when OCR cannot read its title or character name.
        Return Not PatternMatches(invite, text) AndAlso (HasActualTradeWindowText(text) OrElse HasOkButtonText(text))
    End Function

    Public Shared Function MatchesTrade(settings As ResuSettings, text As String, username As String, invitation As Boolean) As Boolean
        Return NamedMatch(Compile(If(invitation, settings.InvitePattern, settings.TradePattern), True), text, username).Success
    End Function

    Private Shared Function Lines(value As String) As IEnumerable(Of String)
        Return If(value, "").Replace(vbCr, "").Split(ChrW(10)).Select(Function(line) Regex.Replace(line.Trim(), "\s+", " ")).Where(Function(line) line.Length > 0)
    End Function

    Private Shared Function TradeMatchCandidates(value As String) As IEnumerable(Of String)
        Dim candidates = Lines(value).ToList()
        Dim flattened = Regex.Replace(If(value, "").Trim(), "\s+", " ")
        If flattened.Length > 0 AndAlso Not candidates.Contains(flattened, StringComparer.OrdinalIgnoreCase) Then candidates.Add(flattened)
        Return candidates
    End Function

    Private Shared Function PatternMatches(pattern As Regex, text As String) As Boolean
        Return TradeMatchCandidates(text).Any(Function(candidate) pattern.IsMatch(candidate))
    End Function

    Private Shared Function HasOkButtonText(text As String) As Boolean
        Return Regex.IsMatch(If(text, ""), "(?<![\p{L}\p{N}])(?:O|0)K(?![\p{L}\p{N}])", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    End Function

    Private Shared Function HasActualTradeWindowText(text As String) As Boolean
        Dim flattened = Regex.Replace(If(text, "").Trim(), "\s+", " ")
        If flattened.Length = 0 Then Return False
        Dim tradeCount = Regex.Matches(flattened, "\bTrade\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant).Count
        Dim rupiahCount = Regex.Matches(flattened, "\bRupiah\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant).Count
        Dim hasCancel = Regex.IsMatch(flattened, "\bCancel\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
        Return (tradeCount >= 2 AndAlso (rupiahCount >= 1 OrElse hasCancel)) OrElse (tradeCount >= 1 AndAlso rupiahCount >= 2)
    End Function

    Private Shared Function NamedMatch(pattern As Regex, text As String, username As String) As Match
        For Each line In TradeMatchCandidates(text)
            Dim match = pattern.Match(line)
            If match.Success AndAlso String.Equals(CleanUsername(match.Groups("user").Value), username, StringComparison.OrdinalIgnoreCase) Then Return match
        Next
        Return Match.Empty
    End Function

    ' Only new lines, seen on two consecutive scans, may settle a transaction. Old chat/history
    ' visible when RESU starts is seeded into _seen and cannot pay for a future resurrection.
    Private Function FreshEvent(pattern As Regex, linesNow As HashSet(Of String), Optional username As String = "") As Match
        For Each line In linesNow
            If _seen.Contains(line) OrElse Not _previousLines.Contains(line) Then Continue For
            Dim match = pattern.Match(line)
            If Not match.Success Then Continue For
            If username.Length > 0 AndAlso Not String.Equals(CleanUsername(match.Groups("user").Value), username, StringComparison.OrdinalIgnoreCase) Then Continue For
            _seen.Add(line)
            Return match
        Next
        Return Match.Empty
    End Function

    Public Function Observe(observation As ResuObservation, now As DateTime) As ResuDecision
        Dim allLines As New HashSet(Of String)(Lines(observation.ChatText & vbLf & observation.MessageText), StringComparer.OrdinalIgnoreCase)
        Dim systemLines As New HashSet(Of String)(Lines(observation.MessageText), StringComparer.OrdinalIgnoreCase)
        Dim elapsed = If(_lastObservation = DateTime.MinValue, 0, (now - _lastObservation).TotalSeconds)
        _lastObservation = now
        ' Suspended/minimized/slow OCR time does not count as observed nonpayment.
        If elapsed < 0 OrElse elapsed > 5 Then elapsed = 0
        Try
            If Not _initialized Then
                _seen.UnionWith(allLines)
                _initialized = True
                Return New ResuDecision()
            End If
            If _pending.Length > 0 Then
                _waitSeconds += elapsed
                If Not _confirmed Then
                    If FreshEvent(_resurrected, systemLines, _pending).Success Then
                        _confirmed = True
                        _waitSeconds = 0
                        Status = $"Resurrection confirmed: {_pending}. Waiting for payment."
                    ElseIf _waitSeconds >= 15 Then
                        Status = $"No resurrection confirmation for {_pending}; no blacklist entry added."
                        ClearPending()
                        Return New ResuDecision()
                    End If
                End If
                If _confirmed Then
                    Dim paid = FreshEvent(_paid, allLines, _pending)
                    Dim amount As Long
                    If paid.Success AndAlso Long.TryParse(paid.Groups("amount").Value.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, amount) AndAlso amount >= _settings.MinimumPayment Then
                        Status = $"Payment confirmed: {_pending}, {amount:N0} rupiahs."
                        ClearPending()
                        Return New ResuDecision()
                    End If
                    If FreshEvent(_unpaid, allLines, _pending).Success Then
                        BlockPending("Explicit nonpayment message")
                        Return New ResuDecision()
                    End If
                    If _waitSeconds >= _settings.PaymentTimeoutSeconds Then
                        BlockPending($"No confirmed payment after {_settings.PaymentTimeoutSeconds} seconds of monitoring")
                        Return New ResuDecision()
                    End If
                End If
                If IsBlocked(_pending) Then
                    Status = $"Blocked: {_pending}."
                    ClearPending()
                    Return New ResuDecision()
                End If
                Dim invite = PatternMatches(_invite, observation.InvitationText)
                Dim trade = PatternMatches(_trade, observation.TradeText) OrElse (Not invite AndAlso (HasActualTradeWindowText(observation.TradeText) OrElse HasOkButtonText(observation.TradeText)))
                ' Stopping clicks is reversible, so even the first fresh closure read stops input.
                ' Payment and blacklist decisions still require two consecutive reads.
                If systemLines.Any(Function(line) Not _seen.Contains(line) AndAlso _closed.IsMatch(line)) Then _tradeClosed = True
                If Not invite AndAlso Not trade Then _tradeClosed = False
                If Not _tradeClosed Then
                    If invite Then Return New ResuDecision With {.Action = ResuAction.AcceptInvite, .Username = _pending}
                    If trade Then Return New ResuDecision With {.Action = ResuAction.AcceptTrade, .Username = _pending}
                End If
                If _confirmed Then Status = $"Waiting for {_pending}: {Math.Max(0, _settings.PaymentTimeoutSeconds - CInt(_waitSeconds))}s remaining."
                Return New ResuDecision()
            End If

            ' Trade acceptance is intentionally independent from resurrection/payment identity.
            ' Any visible recognized dialog is accepted; payment and blacklist decisions still use
            ' the exact pending resurrection customer.
            If PatternMatches(_invite, observation.InvitationText) Then Return New ResuDecision With {.Action = ResuAction.AcceptInvite}
            If PatternMatches(_trade, observation.TradeText) OrElse HasActualTradeWindowText(observation.TradeText) OrElse HasOkButtonText(observation.TradeText) Then Return New ResuDecision With {.Action = ResuAction.AcceptTrade}
            If Not _selected Then Return New ResuDecision With {.Action = ResuAction.SelectTarget}
            Dim name = ExtractTargetUsername(observation.TargetName)
            Dim coolingDown = String.Equals(name, _lastAttempted, StringComparison.OrdinalIgnoreCase) AndAlso now < _targetRetryAfter
            If name.Length = 0 Then
                _target = ""
                _targetScans = 0
                _emptyTargetScans += 1
                If _emptyTargetScans < 4 Then
                    Status = $"Waiting for target-name OCR ({_emptyTargetScans}/4); keeping the current target."
                    Return New ResuDecision()
                End If
                _emptyTargetScans = 0
                Status = "Target name remained unreadable; selecting another target."
                Return New ResuDecision With {.Action = ResuAction.SelectTarget}
            End If
            _emptyTargetScans = 0
            If IsBlocked(name) OrElse coolingDown Then
                Status = If(IsBlocked(name), $"Skipping blacklisted player: {name}.", $"Skipping recently attempted player: {name}.")
                Return New ResuDecision With {.Action = ResuAction.SelectTarget}
            End If
            If String.Equals(name, _target, StringComparison.OrdinalIgnoreCase) Then
                _targetScans += 1
            Else
                _target = name
                _targetScans = 1
            End If
            Status = $"Reading target: {name}."
            If _targetScans >= 2 Then Return New ResuDecision With {.Action = ResuAction.Resurrect, .Username = name}
            Return New ResuDecision()
        Finally
            ' Seed history outside a transaction as well; delayed/old payments cannot be reused.
            If _pending.Length = 0 Then _seen.UnionWith(allLines)
            _previousLines = allLines
        End Try
    End Function

    Public Sub ActionSucceeded(decision As ResuDecision)
        Select Case decision.Action
            Case ResuAction.SelectTarget
                _selected = True
                _target = ""
                _targetScans = 0
                _emptyTargetScans = 0
            Case ResuAction.Resurrect
                _seen.Clear()
                _seen.UnionWith(_previousLines)
                _pending = decision.Username
                _lastAttempted = _pending
                _targetRetryAfter = _lastObservation.AddSeconds(30)
                _confirmed = False
                _waitSeconds = 0
                _tradeClosed = False
                Status = $"Cast on {_pending}; waiting for resurrection confirmation in the message region."
            Case ResuAction.AcceptInvite, ResuAction.AcceptTrade
                Status = If(decision.Username.Length > 0,
                    $"Accepting visible trade for {decision.Username}; waiting for completion/payment text.",
                    "Accepting the visible trade.")
        End Select
    End Sub

    Private Sub ClearPending()
        _pending = ""
        _confirmed = False
        _selected = False
        _targetScans = 0
        _emptyTargetScans = 0
        _waitSeconds = 0
    End Sub

    Private Sub BlockPending(reason As String)
        If Not IsBlocked(_pending) Then
            _settings.Blacklist.Add(New ResuBlacklistEntry With {.Username = _pending, .Reason = reason, .AddedUtc = DateTime.UtcNow})
            BlacklistChanged = True
        End If
        Status = $"Blacklisted {_pending}: {reason}."
        ClearPending()
    End Sub
End Class
