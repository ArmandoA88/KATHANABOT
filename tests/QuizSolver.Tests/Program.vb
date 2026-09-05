Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Private _passed As Integer
    Private Const Source As String = "https://kathana.gitbook.io/wiki/beginner-guide/status-effects-and-chakra"

    Sub Main()
        Test("game answer needs real web evidence", AddressOf GameEvidence)
        Test("general knowledge can avoid search when confident", AddressOf GeneralKnowledge)
        Test("only personal GM trivia may be guessed", AddressOf GmGuesses)
        Test("invalid mappings cannot cause random clicks", AddressOf InvalidMapping)
        Test("unresolved and low-confidence answers are skipped", AddressOf Unresolved)
        Test("search source is matched against actual tool output", AddressOf SearchSources)
        Test("URL citations can supply source evidence", AddressOf CitationSources)
        Test("invented URLs never become clickable evidence", AddressOf InventedSources)
        Test("claimed search without a tool call is rejected", AddressOf FakeSearch)
        Test("unsafe source URL schemes are rejected", AddressOf UnsafeUrls)
        Test("incomplete responses and refusals are rejected", AddressOf IncompleteResponses)
        Test("structured output is parsed after reasoning and narration", AddressOf MixedOutput)
        Test("old result schema cannot silently bypass new policy", AddressOf LegacyResult)
        Test("request enables bounded low-context web search", AddressOf Payload)
        Test("game lookup normally finishes in one request", Sub() SingleRequest().GetAwaiter().GetResult())
        Test("omitted game search gets one mandatory-search retry", Sub() ForcedSearch().GetAwaiter().GetResult())
        Test("mandatory-search retry cannot loop forever", Sub() BoundedRetry().GetAwaiter().GetResult())
        Test("general knowledge and GM guesses need no search retry", Sub() NoUnneededRetry().GetAwaiter().GetResult())
        Test("priority fallback preserves search and evidence schema", Sub() PriorityFallback().GetAwaiter().GetResult())
        Test("unsupported search does not silently fall back to guessing", Sub() UnsupportedSearch().GetAwaiter().GetResult())
        Test("rate-limit response does not cause a retry storm", Sub() RateLimit().GetAwaiter().GetResult())
        Test("caller cancellation stops before sending a request", Sub() CancelBeforeSend().GetAwaiter().GetResult())
        Test("in-flight cancellation propagates without guessing", Sub() CancelInFlight().GetAwaiter().GetResult())
        Console.WriteLine($"Passed {_passed} quiz tests; no live API requests or game input were sent.")
    End Sub

    Private Sub Test(name As String, action As Action)
        action()
        _passed += 1
        Console.WriteLine("PASS " & name)
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Function Answer(Optional category As String = "game", Optional basis As String = "web") As QuizSolveResult
        Return New QuizSolveResult With {.QuestionText = "Fixture question?", .AnswerText = "Choice B", .ButtonNumber = 2, .GridColumn = 8, .GridRow = 8, .Confidence = 0.95,
            .Category = category, .AnswerBasis = basis, .CanAnswer = True, .SourceUrl = If(basis = "web", Source, ""), .Evidence = "Fixture evidence directly supports choice B.",
            .SearchPerformed = basis = "web", .SourceVerified = basis = "web", .IsGuess = basis = "guess"}
    End Function

    Private Function Allowed(answer As QuizSolveResult, Optional count As Integer = 4) As Boolean
        Dim reason As String = ""
        Dim result = QuizAnswerPolicy.CanClick(answer, count, reason)
        If Not result Then Check(reason.Length > 0, "Rejected answers must explain why")
        Return result
    End Function

    Private Function Wire(answer As QuizSolveResult, Optional searched As Boolean = True, Optional toolSource As String = Source, Optional citation As String = "", Optional status As String = "completed") As String
        Dim output As New JsonArray()
        output.Add(New JsonObject From {{"type", "reasoning"}, {"summary", New JsonArray()}})
        If searched Then
            Dim sources As New JsonArray()
            If toolSource.Length > 0 Then sources.Add(New JsonObject From {{"type", "url"}, {"url", toolSource}})
            output.Add(New JsonObject From {{"type", "web_search_call"}, {"status", "completed"}, {"action", New JsonObject From {{"type", "search"}, {"sources", sources}}}})
        End If
        Dim annotations As New JsonArray()
        If citation.Length > 0 Then annotations.Add(New JsonObject From {{"type", "url_citation"}, {"url", citation}, {"title", "Fixture source"}, {"start_index", 0}, {"end_index", 10}})
        output.Add(New JsonObject From {
            {"type", "message"}, {"role", "assistant"}, {"status", "completed"},
            {"content", New JsonArray(New JsonObject From {{"type", "output_text"}, {"text", JsonSerializer.Serialize(answer)}, {"annotations", annotations}})}
        })
        Return New JsonObject From {{"status", status}, {"output", output}}.ToJsonString()
    End Function

    Private Sub GameEvidence()
        Check(Allowed(Answer()), "Sourced game answer should be eligible")
        Check(Not Allowed(Answer("game", "knowledge")), "Game facts need search even when confident")
        Dim result = Answer()
        result.SourceVerified = False
        Check(Not Allowed(result), "Unverified source must not be sufficient")
        result = Answer()
        result.Evidence = ""
        Check(Not Allowed(result), "A URL without supporting evidence is insufficient")
    End Sub

    Private Sub GeneralKnowledge()
        Check(Allowed(Answer("general", "knowledge")), "Confident general knowledge may answer directly")
        Dim result = Answer("general", "knowledge")
        result.Confidence = 0.7
        Check(Not Allowed(result), "Uncertain general knowledge needs evidence")
        Check(Allowed(Answer("general", "web")), "General knowledge may use search")
    End Sub

    Private Sub GmGuesses()
        Dim result = Answer("gm_personal", "guess")
        result.Confidence = 0.2
        Check(Allowed(result) AndAlso result.IsGuess, "Personal GM guesses are explicitly allowed")
        Check(Not Allowed(Answer("game", "guess")), "Game guesses must be rejected")
        Check(Not Allowed(Answer("general", "guess")), "General guesses must be rejected")
        result = QuizOpenAiClient.ParseResponse(Wire(Answer("gm_personal", "knowledge"), False))
        Check(result.IsGuess AndAlso result.AnswerBasis = "guess", "Unsourced GM claims must be labeled guesses")
    End Sub

    Private Sub InvalidMapping()
        For Each index In {-1, 0, 5}
            Dim result = Answer("gm_personal", "guess")
            result.ButtonNumber = index
            Check(Not Allowed(result), "Even GM guesses need a valid detected button")
        Next
    End Sub

    Private Sub Unresolved()
        Dim result = Answer()
        result.CanAnswer = False
        Check(Not Allowed(result), "Unresolved answers must be skipped")
        result = Answer()
        result.Confidence = 0.74
        Check(Not Allowed(result), "Weak web evidence must be skipped")
        result = Answer()
        result.Confidence = Double.NaN
        Check(Not Allowed(result), "Non-finite confidence must be skipped")
        result = Answer()
        result.QuestionText = ""
        Check(Not Allowed(result), "Unreadable question must be skipped")
    End Sub

    Private Sub SearchSources()
        Dim result = QuizOpenAiClient.ParseResponse(Wire(Answer()))
        Check(result.SearchPerformed AndAlso result.SourceVerified AndAlso result.SourceUrl = Source AndAlso Allowed(result), "Tool-backed source should be recognized")
    End Sub

    Private Sub CitationSources()
        Dim result = QuizOpenAiClient.ParseResponse(Wire(Answer(), True, "", Source))
        Check(result.SourceVerified AndAlso Allowed(result), "Annotation URL should count when a search actually ran")
    End Sub

    Private Sub InventedSources()
        Dim result = QuizOpenAiClient.ParseResponse(Wire(Answer(), True, "https://example.com/unrelated"))
        Check(Not result.SourceVerified AndAlso result.SourceUrl = "" AndAlso Not Allowed(result), "Invented URLs must be cleared and rejected")
    End Sub

    Private Sub FakeSearch()
        Dim result = QuizOpenAiClient.ParseResponse(Wire(Answer(), False, "", Source))
        Check(Not result.SearchPerformed AndAlso Not result.SourceVerified AndAlso Not Allowed(result), "An annotation alone cannot fake an executed search")
    End Sub

    Private Sub UnsafeUrls()
        For Each url In {"javascript:alert(1)", "file:///C:/Windows/win.ini", "http://localhost/", "https://name:password@example.com/", "", "not a URL"}
            Check(QuizOpenAiClient.NormalizeSourceUrl(url) = "", "Unsafe URL was accepted: " & url)
        Next
    End Sub

    Private Sub IncompleteResponses()
        ExpectInvalid(Sub() QuizOpenAiClient.ParseResponse(Wire(Answer(), status:="incomplete")))
        Dim refusal = New JsonObject From {{"status", "completed"}, {"output", New JsonArray(New JsonObject From {{"type", "message"}, {"role", "assistant"}, {"content", New JsonArray(New JsonObject From {{"type", "refusal"}, {"refusal", "No answer"}})}})}}
        ExpectInvalid(Sub() QuizOpenAiClient.ParseResponse(refusal.ToJsonString()))
    End Sub

    Private Sub MixedOutput()
        Dim payload = JsonNode.Parse(Wire(Answer()))
        payload("output").AsArray().Insert(1, New JsonObject From {{"type", "message"}, {"role", "assistant"}, {"content", New JsonArray(New JsonObject From {{"type", "output_text"}, {"text", "Checking the wiki..."}})}})
        Check(Allowed(QuizOpenAiClient.ParseResponse(payload.ToJsonString())), "Intermediate narration must not be deserialized as the answer")
    End Sub

    Private Sub LegacyResult()
        Check(Not Allowed(New QuizSolveResult With {.QuestionText = "Q", .AnswerText = "A", .ButtonNumber = 1, .Confidence = 1}), "Old schema lacks the required evidence policy")
    End Sub

    Private Sub Payload()
        Dim payload = QuizOpenAiClient.BuildPayload("gpt-5.4-mini", "clean", "annotated", True)
        Check(payload("tools")(0)("type").GetValue(Of String)() = "web_search", "Enable the supported search tool")
        Check(payload("tools")(0)("search_context_size").GetValue(Of String)() = "low", "Use small search context for speed")
        Check(payload("max_tool_calls").GetValue(Of Integer)() = 2, "Bound search actions")
        Check(payload("reasoning")("effort").GetValue(Of String)() = "low", "Allow evidence assessment without long reasoning")
        Check(payload("include")(0).GetValue(Of String)() = "web_search_call.action.sources", "Request actual source metadata")
        Check(payload("text")("format")("schema")("properties")("can_answer") IsNot Nothing, "Model must be able to abstain")
        Check(payload("input")(0)("content").AsArray().Count = 3, "Keep clean and annotated image inputs")
        Dim mini = QuizOpenAiClient.BuildPayload("gpt-5-mini", "clean", "annotated", False)
        Check(mini("reasoning")("effort").GetValue(Of String)() = "low", "Older mini also needs supported reasoning, not none")
    End Sub

    Private Function Reply(body As String, Optional status As HttpStatusCode = HttpStatusCode.OK) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body)}
    End Function

    Private Async Function Solve(handler As FakeHandler, Optional token As CancellationToken = Nothing) As Task(Of QuizSolveResult)
        Using client As New HttpClient(handler)
            Return Await QuizOpenAiClient.SolveRequestAsync("fixture-not-a-real-key", "gpt-5.4-mini", "clean", "annotated", token, client)
        End Using
    End Function

    Private Async Function SingleRequest() As Task
        Dim handler As New FakeHandler(Reply(Wire(Answer())))
        Check(Allowed(Await Solve(handler)), "Expected a supported answer")
        Check(handler.Requests.Count = 1, "Normal game answer must not require extra extraction calls")
    End Function

    Private Async Function ForcedSearch() As Task
        Dim handler As New FakeHandler(Reply(Wire(Answer("game", "knowledge"), False)), Reply(Wire(Answer())))
        Check(Allowed(Await Solve(handler)), "Mandatory-search retry should recover a grounded result")
        Check(handler.Requests.Count = 2 AndAlso handler.Requests(1)("tool_choice").GetValue(Of String)() = "required", "Second request must force the search")
    End Function

    Private Async Function BoundedRetry() As Task
        Dim handler As New FakeHandler(Reply(Wire(Answer("game", "knowledge"), False)), Reply(Wire(Answer("game", "knowledge"), False)))
        Check(Not Allowed(Await Solve(handler)), "A second unsupported answer remains ineligible")
        Check(handler.Requests.Count = 2, "Do not endlessly retry an ignored tool")
    End Function

    Private Async Function NoUnneededRetry() As Task
        For Each result In {Answer("general", "knowledge"), Answer("gm_personal", "guess")}
            Dim handler As New FakeHandler(Reply(Wire(result, False)))
            Check(Allowed(Await Solve(handler)) AndAlso handler.Requests.Count = 1, "No unnecessary search for straightforward knowledge or GM trivia")
        Next
    End Function

    Private Async Function PriorityFallback() As Task
        Dim handler As New FakeHandler(Reply("service_tier priority unavailable", HttpStatusCode.BadRequest), Reply(Wire(Answer())))
        Check(Allowed(Await Solve(handler)), "Priority fallback should still produce evidence")
        Check(handler.Requests.Count = 2 AndAlso handler.Requests(1)("service_tier") Is Nothing, "Drop only priority on its specific error")
        Check(handler.Requests(1)("tools")(0)("type").GetValue(Of String)() = "web_search", "Fallback must preserve search")
    End Function

    Private Async Function UnsupportedSearch() As Task
        Dim handler As New FakeHandler(Reply("web_search is unsupported for this model", HttpStatusCode.BadRequest))
        Await ExpectInvalidAsync(Function() Solve(handler))
        Check(handler.Requests.Count = 1, "Never retry unsupported search as a blind guess")
    End Function

    Private Async Function RateLimit() As Task
        Dim handler As New FakeHandler(Reply("rate limited", HttpStatusCode.TooManyRequests))
        Await ExpectInvalidAsync(Function() Solve(handler))
        Check(handler.Requests.Count = 1, "Rate limits must return to the UI backoff")
    End Function

    Private Async Function CancelBeforeSend() As Task
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Dim handler As New FakeHandler()
            Await ExpectCancelledAsync(Function() Solve(handler, cancellation.Token))
            Check(handler.Requests.Count = 0, "Canceled solve must send nothing")
        End Using
    End Function

    Private Async Function CancelInFlight() As Task
        Using cancellation As New CancellationTokenSource()
            Dim handler As New FakeHandler() With {.CancelOnSend = cancellation}
            Await ExpectCancelledAsync(Function() Solve(handler, cancellation.Token))
            Check(handler.Requests.Count = 1, "Cancellation must not trigger a retry")
        End Using
    End Function

    Private Sub ExpectInvalid(action As Action)
        Try
            action()
        Catch ex As InvalidOperationException
            Return
        End Try
        Throw New Exception("Expected a rejected response")
    End Sub

    Private Async Function ExpectInvalidAsync(action As Func(Of Task(Of QuizSolveResult))) As Task
        Try
            Await action()
        Catch ex As InvalidOperationException
            Return
        End Try
        Throw New Exception("Expected a failed request")
    End Function

    Private Async Function ExpectCancelledAsync(action As Func(Of Task(Of QuizSolveResult))) As Task
        Try
            Await action()
        Catch ex As OperationCanceledException
            Return
        End Try
        Throw New Exception("Expected cancellation")
    End Function

    Private Class FakeHandler
        Inherits HttpMessageHandler
        Private ReadOnly _responses As Queue(Of HttpResponseMessage)
        Public ReadOnly Requests As New List(Of JsonNode)()
        Public Property CancelOnSend As CancellationTokenSource

        Public Sub New(ParamArray responses As HttpResponseMessage())
            _responses = New Queue(Of HttpResponseMessage)(responses)
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Requests.Add(JsonNode.Parse(Await request.Content.ReadAsStringAsync(cancellationToken)))
            If CancelOnSend IsNot Nothing Then
                CancelOnSend.Cancel()
                cancellationToken.ThrowIfCancellationRequested()
            End If
            If _responses.Count = 0 Then Throw New Exception("Unexpected extra request; no live HTTP transport is installed")
            Return _responses.Dequeue()
        End Function
    End Class
End Module
