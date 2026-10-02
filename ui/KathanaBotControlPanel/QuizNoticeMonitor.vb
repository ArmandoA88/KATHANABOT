Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

' Local text matching only. This path has no reference to the quiz answer API.
Public NotInheritable Class QuizRaffleNotice
    Public ReadOnly Property Kind As String
    Public ReadOnly Property Timing As String
    Public Sub New(kind As String, timing As String)
        Me.Kind = kind
        Me.Timing = timing
    End Sub
    Public ReadOnly Property Title As String
        Get
            Return Kind & " notice"
        End Get
    End Property
    Public ReadOnly Property Body As String
        Get
            Return Kind & " detected on the game screen. " & If(Timing.Length > 0, "Time mentioned: " & Timing & ".", "No time mentioned.")
        End Get
    End Property
End Class

Public NotInheritable Class QuizNoticeDetector
    Private Const Number As String = "(?:\d{1,4}(?:[.,]\d+)?|(?:twenty|thirty|forty|fifty|sixty)(?:[ -](?:one|two|three|four|five|six|seven|eight|nine))?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen)"
    Private Const Unit As String = "(?:hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)(?=\d|\b)"
    Private Shared ReadOnly Keywords As New Regex("\b(?:quiz(?:zes|es)?|raffles?)\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    Private Shared ReadOnly Duration As New Regex("\b" & Number & "\s*" & Unit & "(?:\s*(?:and\s+)?" & Number & "\s*" & Unit & "){0,2}", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    Private Shared ReadOnly Clock As New Regex("\b(?:\d{1,2}:\d{2}(?::\d{2})?(?:\s*[ap]\.?m\.?)?|\d{1,2}\s*[ap]\.?m\.?)\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    Private Shared ReadOnly TimePrefix As New Regex("(?:(?:will\s+)?(?:starts?|begins?|opens?|ends?|closes?|starting|beginning)\s+)?(?:(?:in|after|for|within|at|remaining|duration|time\s+left|countdown)\s*:?\s*)?$", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)
    Private Shared ReadOnly Continuation As New Regex("^(?:(?:will\s+)?(?:start|begin|open|end|close)s?\b|in\b|after\b|at\b|time\s+left\b|countdown\b|" & Number & "\s*" & Unit & ")", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)

    Public Shared Function Detect(lines As IEnumerable(Of String)) As List(Of QuizRaffleNotice)
        Dim rows = If(lines, Enumerable.Empty(Of String)()).SelectMany(Function(s) Regex.Split(If(s, ""), "(?<=[.!?;|])\s+")) _
            .Select(Function(s) Regex.Replace(s, "\s+", " ").Trim()).ToArray()
        Dim found As New Dictionary(Of String, QuizRaffleNotice)(StringComparer.OrdinalIgnoreCase)
        For index = 0 To rows.Length - 1
            Dim line = rows(index)
            Dim matches = Keywords.Matches(line)
            For occurrence = 0 To matches.Count - 1
                Dim keyword = matches(occurrence)
                Dim kind = If(keyword.Value.StartsWith("quiz", StringComparison.OrdinalIgnoreCase), "Quiz", "Raffle")
                Dim start = If(occurrence = 0, 0, keyword.Index)
                Dim finish = If(occurrence + 1 < matches.Count, matches(occurrence + 1).Index, line.Length)
                Dim context = line.Substring(start, finish - start)
                Dim timing = ExtractTiming(context)
                ' A wrapped announcement may put its countdown on the next OCR
                ' line. Do not borrow unrelated UI timers or another event's time.
                If timing.Length = 0 AndAlso occurrence = matches.Count - 1 AndAlso index + 1 < rows.Length AndAlso
                    Not Keywords.IsMatch(rows(index + 1)) AndAlso Continuation.IsMatch(rows(index + 1)) Then
                    timing = ExtractTiming(rows(index + 1))
                End If
                If Not found.ContainsKey(kind) OrElse (found(kind).Timing.Length = 0 AndAlso timing.Length > 0) Then
                    found(kind) = New QuizRaffleNotice(kind, timing)
                End If
            Next
        Next
        Return found.Values.ToList()
    End Function

    Private Shared Function ExtractTiming(text As String) As String
        Dim match = Duration.Match(text)
        If Not match.Success Then
            match = Clock.Match(text)
            If Not match.Success Then Return ""
            Dim beforeClock = text.Substring(0, match.Index).TrimEnd()
            If TimePrefix.Match(beforeClock & " ").Value.Trim().Length = 0 AndAlso
                Not Regex.IsMatch(beforeClock, "^(?:quiz(?:zes|es)?|raffles?)\s*[:\-]?$", RegexOptions.IgnoreCase) Then Return ""
        End If
        Dim prefix = TimePrefix.Match(text.Substring(0, match.Index)).Value.Trim()
        Dim suffix = Regex.Match(text.Substring(match.Index + match.Length), "^\s+(?:remaining|left)\b", RegexOptions.IgnoreCase).Value.Trim()
        Return String.Join(" ", {prefix, match.Value.Trim(), suffix}.Where(Function(s) s.Length > 0))
    End Function
End Class

Public NotInheritable Class QuizNoticeMonitor
    Private NotInheritable Class SeenNotice
        Public LastSeen As DateTime = DateTime.MinValue
        Public LastSent As DateTime = DateTime.MinValue
        Public NextAttempt As DateTime = DateTime.MinValue
        Public SentTiming As String = ""
    End Class
    Private ReadOnly seen As New Dictionary(Of String, SeenNotice)(StringComparer.Ordinal)

    Public Async Function ProcessAsync(windowKey As String, lines As IEnumerable(Of String), topic As String, now As DateTime,
        send As Func(Of QuizRaffleNotice, String, CancellationToken, Task(Of Boolean)), cancellation As CancellationToken) As Task(Of Integer)
        cancellation.ThrowIfCancellationRequested()
        Dim sent = 0
        For Each stale In seen.Where(Function(p) now - p.Value.LastSeen > TimeSpan.FromDays(1)).Select(Function(p) p.Key).ToArray()
            seen.Remove(stale)
        Next
        For Each notice In QuizNoticeDetector.Detect(lines)
            cancellation.ThrowIfCancellationRequested()
            Dim key = windowKey & "|" & notice.Kind
            If Not seen.ContainsKey(key) Then seen(key) = New SeenNotice()
            Dim state = seen(key)
            Dim absent = now - state.LastSeen >= TimeSpan.FromSeconds(30)
            state.LastSeen = now
            Dim newTiming = state.SentTiming.Length = 0 AndAlso notice.Timing.Length > 0
            If now < state.NextAttempt OrElse (state.LastSent <> DateTime.MinValue AndAlso Not newTiming AndAlso
                Not (absent AndAlso now - state.LastSent >= TimeSpan.FromMinutes(2))) Then Continue For
            If String.IsNullOrWhiteSpace(topic) Then Continue For
            state.NextAttempt = now.AddMinutes(1) ' Bound retries if notification delivery fails.
            If Await send(notice, topic, cancellation) Then
                state.LastSent = now
                state.SentTiming = notice.Timing
                state.NextAttempt = now.AddSeconds(10)
                sent += 1
            End If
        Next
        Return sent
    End Function
End Class
