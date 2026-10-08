Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Friend Module TradeAiBatchTests
    Private assertions As Integer

    Public Async Function RunAsync() As Task
        assertions = 0
        Await TestCoverage(1000, True)
        Await TestCoverage(1000, True, False)
        Await TestCoverage(10000, False)
        Await TestStableKeysAndReposts()
        Await TestLegacyAndEvidence()
        Await TestSourceIntegrityAndPreflight()
        Await TestRejectedBatchIsSkipped()
        Await TestEstimates()
        Await TestIncompleteBatchIsSplit()
        Await TestPauseShowsPartialResults()
        Await TestFailureAndCancellation()
        Console.WriteLine($"PASS: {assertions:N0} AI batching assertions: complete 1,000/10,000-post coverage, >200k source, whole boundaries, progress, exact source/evidence, stable keys, global repost dedup and atomic failure/cancellation.")
    End Function

    Private Sub Check(value As Boolean, message As String)
        Interlocked.Increment(assertions)
        If Not value Then Throw New InvalidOperationException("AI batch test: " & message)
    End Sub

    Private Function Post(index As Integer, Optional longBody As Boolean = False) As DiscordTradeMessage
        Return New DiscordTradeMessage With {.Id = (1500000000000000000UL + CULng(index)).ToString(), .MessageType = 0,
            .AuthorName = "Seller" & index.ToString("D5"), .BodyText = "S> Tikoy 7D" & If(longBody, " " & New String("."c, 260), "")}
    End Function

    Private Function Block(post As DiscordTradeMessage) As String
        Return If(post.IsCrosspost, "Followed announcement delivery identity (not a verified game character): " & post.AuthorName, post.AuthorName) & vbLf & post.BodyText
    End Function

    Private Function Source(posts As IEnumerable(Of DiscordTradeMessage)) As String
        Return String.Join(vbLf & vbLf, posts.Select(Function(post) Block(post)))
    End Function

    Private Function Entry(name As String, body As String, Optional item As String = "Tikoy 7D", Optional key As String = "TIKOY 7D", Optional intent As String = "sell") As TradeListing
        Return New TradeListing With {.CharacterName = name, .Intent = intent, .ItemText = item, .ItemKey = key, .Evidence = body}
    End Function

    Private Function Api(listings As IEnumerable(Of TradeListing)) As String
        Return JsonSerializer.Serialize(New With {.status = "completed", .output = New Object() {
            New With {.type = "message", .content = New Object() {
                New With {.type = "output_text", .text = JsonSerializer.Serialize(New TradeExtraction With {.Listings = listings.ToList()})}}}}})
    End Function

    Private Class BatchHandler
        Inherits HttpMessageHandler
        Private ReadOnly inputsStore As New List(Of String)()
        Private ReadOnly inputSync As New Object()
        Public ReadOnly Property Inputs As List(Of String)
            Get
                SyncLock inputSync
                    Return inputsStore.ToList()
                End SyncLock
            End Get
        End Property
        Private ReadOnly responder As Func(Of String, Integer, CancellationToken, Task(Of HttpResponseMessage))
        Private busy As Integer

        Public Sub New(responder As Func(Of String, Integer, CancellationToken, Task(Of HttpResponseMessage)))
            Me.responder = responder
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            Check(Interlocked.Increment(busy) <= 4, "requests exceeded the four-worker concurrency bound")
            Try
                Check(request.RequestUri.AbsoluteUri = "https://api.openai.com/v1/responses", "wrong endpoint")
                Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellation))
                    Dim payload = document.RootElement
                    Check(Not payload.GetProperty("store").GetBoolean(), "response storage enabled")
                    Check(payload.GetProperty("model").GetString() = "configured-test-model", "configured model changed")
                    Check(payload.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "structured output disabled")
                    Dim raw = payload.GetProperty("input")(0).GetProperty("content").GetString()
                    Check(raw.Length <= TradeAiService.MaximumBatchCharacters, "batch exceeded character bound")
                    Dim callNumber As Integer
                    SyncLock inputSync
                        inputsStore.Add(raw)
                        callNumber = inputsStore.Count
                    End SyncLock
                    Return Await responder(raw, callNumber, cancellation)
                End Using
            Finally
                Interlocked.Decrement(busy)
            End Try
        End Function
    End Class

    Private Function Ok(listings As IEnumerable(Of TradeListing)) As Task(Of HttpResponseMessage)
        Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(Api(listings), Encoding.UTF8, "application/json")})
    End Function

    Private Class RecordedProgress
        Implements IProgress(Of TradeAnalysisProgress)
        Private ReadOnly updatesStore As New List(Of TradeAnalysisProgress)()
        Public ReadOnly Property Updates As List(Of TradeAnalysisProgress)
            Get
                SyncLock updatesStore
                    Return updatesStore.ToList()
                End SyncLock
            End Get
        End Property
        Public Property Callback As Action(Of TradeAnalysisProgress)
        Public Sub Report(value As TradeAnalysisProgress) Implements IProgress(Of TradeAnalysisProgress).Report
            SyncLock updatesStore
                updatesStore.Add(value)
            End SyncLock
            Callback?.Invoke(value)
        End Sub
    End Class

    Private Async Function TestCoverage(count As Integer, longBodies As Boolean, Optional useKnownPosts As Boolean = True) As Task
        Dim posts = Enumerable.Range(0, count).Select(Function(index) Post(index, longBodies)).ToList()
        If longBodies Then posts(50).BodyText &= vbLf & vbLf & "A paragraph within the same author's body." & vbLf & "Still this author's text."
        Dim raw = Source(posts)
        If longBodies Then Check(raw.Length > 200000, "fixture does not exceed old global limit")
        Dim lookup = posts.ToDictionary(Function(post) post.AuthorName, StringComparer.Ordinal)
        Dim covered As New HashSet(Of String)(StringComparer.Ordinal)
        Dim progress As New RecordedProgress()
        Dim handler As New BatchHandler(
            Function(input, batchNumber, cancellation)
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)\r?$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value).ToList()
                Check(names.Count > 0 AndAlso names.Count <= TradeAiService.MaximumBatchPosts, "batch post count is unbounded or empty")
                Dim listings As New List(Of TradeListing)()
                For Each name In names
                    Dim post = lookup(name)
                    Check(input.Contains(Block(post), StringComparison.Ordinal), "a name/body boundary was split")
                    SyncLock covered
                        Check(covered.Add(name), "a source post was analyzed twice")
                    End SyncLock
                    listings.Add(Entry(name, post.BodyText))
                Next
                Return Ok(listings)
            End Function)
        Using transport As New HttpClient(handler)
            Dim result = Await TradeAiService.AnalyzeAsync(raw, "offline-test-key", "configured-test-model", CancellationToken.None, transport, progress, If(useKnownPosts, posts, Nothing), useCache:=False)
            Check(result.Count = count, "not every source post produced its verified listing")
            Check(covered.Count = count, "source coverage was incomplete")
            Check(progress.Updates(0).CompletedBatches = 0 AndAlso progress.Updates(0).ProcessedPosts = 0, "progress did not start at zero")
            Dim final = progress.Updates.Last()
            Check(final.TotalPosts = count AndAlso final.ProcessedPosts = count AndAlso final.ListingCount = count, "final progress omitted source posts")
            Check(final.CompletedBatches = handler.Inputs.Count AndAlso final.TotalBatches = handler.Inputs.Count, "batch progress totals differ from requests")
            Check(progress.Updates.Select(Function(update) update.ProcessedPosts).SequenceEqual(progress.Updates.Select(Function(update) update.ProcessedPosts).OrderBy(Function(value) value)), "post progress went backward")
            Check(String.Concat(handler.Inputs.OrderBy(Function(input) Integer.Parse(Regex.Match(input, "Seller([0-9]+)").Groups(1).Value))) = raw,
                  "batch boundaries altered or omitted source text")
        End Using
    End Function

    Private Async Function TestStableKeysAndReposts() As Task
        Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
        posts(TradeAiService.MaximumBatchPosts).AuthorName = posts(0).AuthorName
        Dim handler As New BatchHandler(
            Function(input, callNumber, cancellation)
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value).ToList()
                Return Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D", key:=If(names.Count > 1, "TIKOY SEVEN DAYS", "Tikoy 7-day"))))
            End Function)
        Using transport As New HttpClient(handler)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Check(handler.Inputs.Count = 2, "repost fixture did not cross a batch boundary")
            Check(result.Count = TradeAiService.MaximumBatchPosts, "cross-batch same-author repost increased recipients")
            Check(result.All(Function(listing) listing.ItemKey = "TIKOY SEVEN DAYS"), "identical verified spelling did not keep its first alias key")
            Check(TradeAiService.RankItems(result, True).Single().Characters = TradeAiService.MaximumBatchPosts, "reposts increased distinct-character popularity")
        End Using
        Dim variants = "One" & vbLf & "S> Tikoy 7D, Tikoy 30D"
        Dim separate = TradeAiService.ValidateListings({Entry("One", "S> Tikoy 7D, Tikoy 30D", key:="TIKOY"), Entry("One", "S> Tikoy 7D, Tikoy 30D", "Tikoy 30D", "TIKOY")}, variants)
        Check(separate.Count = 2, "different clearly identified variants merged")
        Dim variantPosts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
        variantPosts(TradeAiService.MaximumBatchPosts).AuthorName = variantPosts(0).AuthorName
        variantPosts(TradeAiService.MaximumBatchPosts).BodyText = "S> Tikoy 30D"
        Using transport As New HttpClient(New BatchHandler(Function(input, callNumber, cancellation)
            Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
            Dim item = If(input.Contains("Tikoy 30D", StringComparison.Ordinal), "Tikoy 30D", "Tikoy 7D")
            Return Ok(names.Select(Function(name) Entry(name, "S> " & item, item, "TIKOY")))
        End Function))
            Dim result = Await TradeAiService.AnalyzeAsync(Source(variantPosts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=variantPosts, useCache:=False)
            Check(result.Where(Function(listing) listing.CharacterName = "Seller00000").Count() = 2, "global broad-key collision discarded an exact verified variant")
        End Using
    End Function

    Private Async Function TestLegacyAndEvidence() As Task
        Dim raw = "AliceAPP9/14/2026 10:24 PM" & vbLf & "S> Sword" & vbLf &
            "Chatter" & vbLf & "Online" & vbLf & "asba ring" & vbLf &
            "Bob" & vbLf & "APP" & vbLf & "— 9/15/2026 10:20 AM" & vbLf & "B> Ring" & vbLf &
            "CaraBOT9/16/2026 1:00 PM" & vbLf & "S/T> B.orb || RuMagic || Ror asura"
        Dim valid = New List(Of TradeListing) From {Entry("Alice", "S> Sword", "Sword", "SWORD"),
            Entry("Bob", "B> Ring", "Ring", "RING", "buy"), Entry("Cara", "S/T> B.orb || RuMagic || Ror asura", "RuMagic", "RUMAGIC")}
        Using transport As New HttpClient(New BatchHandler(Function(input, callNumber, cancellation)
            Check(input = raw, "single-batch legacy paste was rewritten")
            Return Ok(valid)
        End Function))
            Check((Await TradeAiService.AnalyzeAsync(raw, "offline-test-key", "configured-test-model", CancellationToken.None, transport, useCache:=False)).Count = 3, "legacy names, status lines or sell/trade intent failed")
        End Using
        Dim wrong = New List(Of TradeListing)(valid) From {Entry("Alice", "B> Ring", "Ring", "RING", "buy"), Entry("BOB", "B> Ring", "Ring", "RING", "buy"), Entry("Chatter", "S> Sword", "Sword", "SWORD")}
        Check(TradeAiService.ValidateListings(wrong, raw).Count = 3, "borrowed evidence or changed-case name was accepted")
        Dim badgeDate = "First" & vbLf & "APP9/14/2026 10:24 PM" & vbLf & "S> Sword" & vbLf &
            "Second" & vbLf & "BOT9/15/2026 11:24 AM" & vbLf & "B> Ring"
        Check(TradeAiService.ValidateListings({Entry("First", "S> Sword", "Sword", "SWORD"), Entry("Second", "B> Ring", "Ring", "RING", "buy")}, badgeDate).Count = 2,
            "separate names followed by combined APP/BOT date metadata lost their boundaries")
        Dim followed = "Followed announcement delivery identity (not a verified game character): Kathana" & vbLf & "S> Sword PM ExactSeller"
        Check(TradeAiService.ValidateListings({Entry("ExactSeller", "S> Sword PM ExactSeller", "Sword", "SWORD")}, followed).Count = 1, "explicit Follow body contact was rejected")
        Dim rejected As Boolean
        Try
            TradeAiService.ValidateListings({Entry("Kathana", "S> Sword PM ExactSeller", "Sword", "SWORD")}, followed)
        Catch ex As InvalidOperationException
            rejected = True
        End Try
        Check(rejected, "Follow delivery identity became a game character")
        Dim inherited = "One" & vbLf & "S> B.orb || RuMagic = 1.5m || Ror asura // B> Ring // T> TradeOnly"
        Dim inheritedItems = New List(Of TradeListing) From {
            Entry("One", "B.orb", "B.orb", "B.ORB"), Entry("One", "RuMagic = 1.5m", "RuMagic", "RUMAGIC"),
            Entry("One", "Ror asura", "Ror asura", "ROR ASURA"), Entry("One", "Ring", "Ring", "RING", "buy"),
            Entry("One", "Ring", "Ring", "RING", "sell"), Entry("One", "TradeOnly", "TradeOnly", "TRADEONLY", "sell")}
        Check(TradeAiService.ValidateListings(inheritedItems, inherited).Count = 4, "inherited sell intent, B.orb, later buy boundary or trade-only boundary failed")
        Dim paragraph = "Alice" & vbLf & "Selling Tikoy" & vbLf & vbLf & "Voucher" & vbLf & "20m" & vbLf & vbLf & "Bob" & vbLf & "B> Ring"
        Dim paragraphListings = {Entry("Alice", "Voucher" & vbLf & "20m", "Voucher", "VOUCHER"), Entry("Bob", "B> Ring", "Ring", "RING", "buy")}
        Check(TradeAiService.ValidateListings(paragraphListings, paragraph).Count = 2, "an inherited item paragraph became an author or a separate real offer was joined")
        Dim payload = TradeAiService.BuildPayload("One" & vbLf & "S> Item", "configured-test-model")
        Check(payload("instructions").GetValue(Of String)().Contains("Voucher is an item only when clearly offered", StringComparison.Ordinal), "payment/exchange Voucher safeguard missing")
    End Function

    Private Async Function TestSourceIntegrityAndPreflight() As Task
        Dim posts = New List(Of DiscordTradeMessage) From {Post(1), Post(2)}
        Dim handler As New BatchHandler(Function(input, callNumber, cancellation) Ok(Array.Empty(Of TradeListing)()))
        Using transport As New HttpClient(handler)
            Dim mismatch = False
            Try
                Await TradeAiService.AnalyzeAsync(Source(posts) & " edited", "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Catch ex As ArgumentException
                mismatch = ex.Message.Contains("match", StringComparison.Ordinal)
            End Try
            Check(mismatch AndAlso handler.Inputs.Count = 0, "stale source data reached the API")
            Dim oversized = New List(Of DiscordTradeMessage) From {Post(3)}
            oversized(0).BodyText &= New String("x"c, TradeAiService.MaximumBatchCharacters)
            Dim tooLarge = False
            Try
                Await TradeAiService.AnalyzeAsync(Source(oversized), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=oversized, useCache:=False)
            Catch ex As ArgumentException
                tooLarge = ex.Message.Contains("8,000", StringComparison.Ordinal)
            End Try
            Check(tooLarge AndAlso handler.Inputs.Count = 0, "oversized individual post was truncated or sent")
            Dim overCount = Enumerable.Range(0, 10001).Select(Function(index) Post(index)).ToList()
            Dim countRejected = False
            Try
                Await TradeAiService.AnalyzeAsync(Source(overCount), "offline-test-key", "configured-test-model", CancellationToken.None, transport, useCache:=False)
            Catch ex As ArgumentException
                countRejected = ex.Message.Contains("10,000", StringComparison.Ordinal)
            End Try
            Check(countRejected AndAlso handler.Inputs.Count = 0, "10,001 fallback posts reached the API")
            Await TradeAiService.AnalyzeAsync(Source(posts).Replace(vbLf, vbCrLf), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts.AsEnumerable().Reverse(), useCache:=False)
            Check(handler.Inputs.Count = 1, "normalized newline/reversed complete snapshot did not match")
        End Using
    End Function

    Private Async Function TestPauseShowsPartialResults() As Task
        Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts * 6).Select(Function(index) Post(index)).ToList()
        ' Pause during the first request: the four batches already sent finish, the last two are never sent.
        Dim control As New TradeAnalysisControl()
        Dim handler As New BatchHandler(
            Async Function(input, callNumber, cancellation)
                Await Task.Delay(50, cancellation) ' keep all four requests in flight before the pause lands
                If callNumber = 1 Then control.RequestPause()
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
                Return Await Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
            End Function)
        Using transport As New HttpClient(handler)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False, control:=control)
            Check(handler.Inputs.Count = 4 AndAlso control.Paused AndAlso control.CompletedBatches = 4 AndAlso control.TotalBatches = 6, "pause did not stop new batches after the in-flight ones")
            Check(control.ProcessedPosts = 200 AndAlso control.TotalPosts = 300 AndAlso result.Count = 200, "paused result did not cover exactly the completed batches")
            Check(result.All(Function(entry) Integer.Parse(entry.CharacterName.Substring(6)) < 200), "paused result included a post from an unsent batch")
        End Using
        ' A pause that arrives during the final batch skipped nothing, so the result is complete and not marked partial.
        Dim late As New TradeAnalysisControl()
        Dim lateHandler As New BatchHandler(
            Function(input, callNumber, cancellation)
                If input.Contains("Seller00299", StringComparison.Ordinal) Then late.RequestPause()
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
                Return Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
            End Function)
        Using transport As New HttpClient(lateHandler)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False, control:=late)
            Check(Not late.Paused AndAlso result.Count = 300 AndAlso late.CompletedBatches = 6, "a pause with nothing left to skip produced a partial result")
        End Using
        ' Pausing before any batch starts returns an empty, paused outcome without a request.
        Dim early As New TradeAnalysisControl()
        early.RequestPause()
        Dim idle As New BatchHandler(Function(input, callNumber, cancellation) Ok(Array.Empty(Of TradeListing)()))
        Using transport As New HttpClient(idle)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False, control:=early)
            Check(idle.Inputs.Count = 0 AndAlso early.Paused AndAlso early.CompletedBatches = 0 AndAlso result.Count = 0, "an immediate pause still sent batches")
        End Using
    End Function

    Private Async Function TestIncompleteBatchIsSplit() As Task
        Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
        Const cutoff As String = "{""status"":""incomplete"",""incomplete_details"":{""reason"":""max_output_tokens""},""output"":[]}"
        ' Any request for more than 10 posts runs out of output budget; smaller halves finish.
        Dim handler As New BatchHandler(
            Function(input, callNumber, cancellation)
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value).ToList()
                If names.Count > 10 Then Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(cutoff, Encoding.UTF8, "application/json")})
                Return Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
            End Function)
        Using transport As New HttpClient(handler)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Check(result.Count = posts.Count AndAlso result.Select(Function(entry) entry.CharacterName).Distinct().Count() = posts.Count, "split halves lost or duplicated listings")
            Check(handler.Inputs.Any(Function(input) Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Count <= 10), "the oversized batch was never split")
            Check(handler.Inputs.All(Function(input) input.Length <= TradeAiService.MaximumBatchCharacters), "a split batch exceeded the character bound")
            Check(String.Concat(handler.Inputs.Where(Function(input) Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Count <= 10).OrderBy(Function(input) Integer.Parse(Regex.Match(input, "Seller([0-9]+)").Groups(1).Value))) = Source(posts),
                  "split batches altered or omitted source text")
        End Using
        ' One post that still cannot finish fails with the model's real reason, without endless splitting.
        Dim stuck As New BatchHandler(Function(input, callNumber, cancellation) Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(cutoff, Encoding.UTF8, "application/json")}))
        Using transport As New HttpClient(stuck)
            Dim lone = posts.Take(1).ToList()
            Dim failed = False
            Try
                Await TradeAiService.AnalyzeAsync(Source(lone), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=lone, useCache:=False)
            Catch ex As InvalidOperationException
                failed = ex.Message.Contains("incomplete", StringComparison.Ordinal) AndAlso ex.Message.Contains("max output tokens", StringComparison.Ordinal)
            End Try
            Check(failed AndAlso stuck.Inputs.Count = 1, "a single unfinishable post did not fail once with the model's reason")
        End Using
    End Function

    Private Async Function TestEstimates() As Task
        ' Before any batch finishes the estimate uses a conservative per-request guess over four-way concurrency.
        Dim start As New TradeAnalysisProgress With {.CompletedBatches = 0, .TotalBatches = 200, .ProcessedPosts = 0, .TotalPosts = 10000, .TotalCharacters = 1000000}
        Check(TradeAiService.EstimateRemainingSeconds(start) = 50 * 20, "initial estimate ignored the four-way concurrency")
        Dim measured As New TradeAnalysisProgress With {.CompletedBatches = 196, .TotalBatches = 200, .TotalPosts = 10000, .ProcessedPosts = 9800, .AverageBatchSeconds = 12}
        Check(TradeAiService.EstimateRemainingSeconds(measured) = 12, "measured latency was not used for the final wave")
        measured.CompletedBatches = 200
        Check(TradeAiService.EstimateRemainingSeconds(measured) = 0, "a finished job still reported time left")
        Dim cost = TradeAiService.EstimateCost(200, 1000000, 2500)
        Check(cost > 0.1D AndAlso cost < 1D, "cost estimate for 10,000 posts on the nano model left its expected range: " & cost)
        Check(TradeAiService.EstimateCost(0, 0, 0) = 0D AndAlso TradeAiService.EstimateCost(100, 500000, 1000) < TradeAiService.EstimateCost(200, 1000000, 2000), "cost is not monotonic")
        Check(TradeAiService.FormatDuration(45) = "45 s" AndAlso TradeAiService.FormatDuration(600) = "10 min" AndAlso TradeAiService.FormatDuration(7200) = "2 h 0 min", "duration formatting changed")
        Check(TradeAiService.FormatCost(0.004D) = "<$0.01" AndAlso TradeAiService.FormatCost(0.284D) = "$0.28", "cost formatting changed")
        Dim text = TradeAiService.DescribeProgress(New TradeAnalysisProgress With {.CompletedBatches = 4, .TotalBatches = 200, .ProcessedPosts = 200, .TotalPosts = 10000, .ListingCount = 47, .TotalCharacters = 1000000, .CompletedCharacters = 40000, .AverageBatchSeconds = 15}, 70)
        Check(text.Contains("batch 4/200") AndAlso text.Contains("200/10,000 posts") AndAlso text.Contains("47 buy/sell listings") AndAlso text.Contains("Elapsed 70 s") AndAlso
              text.Contains("left") AndAlso text.Contains("Est. cost") AndAlso text.Contains("F12"), "progress description lost its figures")
        ' A real run reports measured request time and character totals to the UI.
        Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
        Dim progress As New RecordedProgress()
        Using transport As New HttpClient(New BatchHandler(Async Function(input, callNumber, cancellation)
            Await Task.Delay(30, cancellation)
            Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
            Return Await Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
        End Function))
            Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, progress, posts, useCache:=False)
        End Using
        Dim final = progress.Updates.Last()
        Check(progress.Updates(0).TotalCharacters = Source(posts).Length AndAlso final.CompletedCharacters = final.TotalCharacters AndAlso final.AverageBatchSeconds > 0.02,
              "progress did not carry character totals and measured batch time")
    End Function

    Private Async Function TestRejectedBatchIsSkipped() As Task
        Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
        ' The lone-post batch gets only an item that is not in its source; the full batch is fine.
        Dim mixedBatches As New BatchHandler(
            Function(input, callNumber, cancellation)
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value).ToList()
                If names.Count = 1 Then Return Ok({Entry(names.Single(), "S> Tikoy 7D", "Invented", "INVENTED")})
                Return Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
            End Function)
        Using transport As New HttpClient(mixedBatches)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Check(mixedBatches.Inputs.Count = 2, "rejected-batch fixture did not use two batches")
            Check(result.Count = TradeAiService.MaximumBatchPosts, "one fully rejected batch discarded the verified listings of the other batch")
        End Using
        Dim allRejected As New BatchHandler(
            Function(input, callNumber, cancellation)
                Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
                Return Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D", "Invented", "INVENTED")))
            End Function)
        Using transport As New HttpClient(allRejected)
            Dim failed = False
            Try
                Await TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Catch ex As InvalidOperationException
                failed = ex.Message.Contains("original character", StringComparison.OrdinalIgnoreCase) AndAlso ex.Message.Contains($"{TradeAiService.MaximumBatchPosts + 1:N0} AI listings", StringComparison.Ordinal)
            End Try
            Check(failed, "an analysis where no batch verified anything did not report a source-verification failure")
        End Using
    End Function

    Private Async Function TestFailureAndCancellation() As Task
        For Each mode In {"http", "incomplete", "cancel"}
            Dim posts = Enumerable.Range(0, TradeAiService.MaximumBatchPosts + 1).Select(Function(index) Post(index)).ToList()
            Dim progress As New RecordedProgress()
            Dim firstReported As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            progress.Callback = Sub(update)
                                    If update.CompletedBatches = 1 Then firstReported.TrySetResult(True)
                                End Sub
            Using cancellation As New CancellationTokenSource()
                Dim handler As New BatchHandler(
                    Async Function(input, callNumber, token)
                        If Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Count = 1 Then
                            Await firstReported.Task.WaitAsync(token)
                            If mode = "cancel" Then
                                cancellation.Cancel()
                                token.ThrowIfCancellationRequested()
                            End If
                            Dim response = New HttpResponseMessage(If(mode = "http", HttpStatusCode.TooManyRequests, HttpStatusCode.OK)) With {
                                .Content = New StringContent("{""status"":""incomplete"",""output"":[]}", Encoding.UTF8, "application/json")}
                            Return response
                        End If
                        Dim names = Regex.Matches(input, "(?m)^(Seller[0-9]+)$").Cast(Of Match)().Select(Function(match) match.Groups(1).Value)
                        Return Await Ok(names.Select(Function(name) Entry(name, "S> Tikoy 7D")))
                    End Function)
                Using transport As New HttpClient(handler)
                    Dim task = TradeAiService.AnalyzeAsync(Source(posts), "offline-test-key", "configured-test-model", cancellation.Token, transport, progress, posts, useCache:=False)
                    Dim failed = False
                    Try
                        Await task
                    Catch ex As OperationCanceledException When mode = "cancel"
                        failed = True
                    Catch ex As InvalidOperationException When mode <> "cancel"
                        failed = True
                    End Try
                    Check(failed AndAlso Not task.IsCompletedSuccessfully, mode & " returned a partial result")
                    Check(handler.Inputs.Count = 2, mode & " retried or skipped the failed batch")
                    Check(progress.Updates.Last().CompletedBatches = 1 AndAlso progress.Updates.Last().ProcessedPosts = TradeAiService.MaximumBatchPosts,
                          mode & " claimed unfinished posts as complete")
                End Using
            End Using
        Next
    End Function
End Module
