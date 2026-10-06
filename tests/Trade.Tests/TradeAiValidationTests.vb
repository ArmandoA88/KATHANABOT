Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Friend Module TradeAiValidationTests
    Private assertions As Integer

    Public Async Function RunAsync() As Task
        assertions = 0
        TestSourceSpellingAndWhitespace()
        TestHistoricalMixedTradePosts()
        TestAuthorHeaderEvidence()
        TestUnsupportedChangesAndBoundaries()
        TestPostAndIntentOwnership()
        TestFollowDeliveryIdentity()
        TestAdditionalExplicitMarkers()
        TestInvalidAndMixedCollections()
        Await TestApiNormalizationAndCache()
        Await TestApiRejectionDoesNotRetryOrCache()
        Console.WriteLine($"PASS: {assertions:N0} offline AI source-validation assertions: case/whitespace recovery with original spelling, author-header evidence, exact variants and post/intent ownership, Follow contacts, explicit trade markers, mixed-invalid filtering, API failure and cache replay.")
    End Function

    Private Sub Check(condition As Boolean, reason As String)
        Interlocked.Increment(assertions)
        If Not condition Then Throw New InvalidOperationException("AI source-validation test: " & reason)
    End Sub

    Private Function Entry(author As String, intent As String, item As String, evidence As String, Optional key As String = Nothing) As TradeListing
        Return New TradeListing With {.CharacterName = author, .Intent = intent, .ItemText = item,
            .ItemKey = If(key, item.ToUpperInvariant()), .Evidence = evidence}
    End Function

    Private Function Post(author As String, body As String) As String
        Return author & vbLf & body
    End Function

    Private Function Accepted(raw As String, candidate As TradeListing, expectedItem As String, expectedEvidence As String) As TradeListing
        Dim actual = TradeAiService.ValidateListings({candidate}, raw)
        Check(actual.Count = 1, "a source-supported listing was discarded")
        Dim result = actual.Single()
        Check(result.CharacterName = candidate.CharacterName AndAlso result.Intent = candidate.Intent, "recipient case or intent changed during validation")
        Check(result.ItemText = expectedItem, "item spelling/spacing was not recovered from the source: " & result.ItemText)
        Check(result.Evidence = expectedEvidence, "evidence was not recovered as its original contiguous source excerpt")
        Check(result.ItemKey = candidate.ItemKey, "validation changed the separately chosen display key")
        Check(Not Object.ReferenceEquals(result, candidate), "validation retained a caller-owned mutable listing")
        Return result
    End Function

    Private Sub Rejected(raw As String, candidate As TradeListing, reason As String)
        Dim failed As Boolean
        Try
            TradeAiService.ValidateListings({candidate}, raw)
        Catch ex As InvalidOperationException
            failed = ex.Message.Contains("original character", StringComparison.OrdinalIgnoreCase) AndAlso
                ex.Message.Contains("No partial queue", StringComparison.OrdinalIgnoreCase)
        End Try
        Check(failed, reason)
    End Sub

    Private Sub TestSourceSpellingAndWhitespace()
        Dim body = "S> D.   KHANDIVA+11   MAX AS = 130M"
        Dim candidate = Entry("xanzy", "sell", "d. khandiva+11", "s> d. khandiva+11 max as = 130m", "D. KHANDIVA+11")
        Accepted(Post("xanzy", body), candidate, "D. KHANDIVA+11", body)
        Check(candidate.ItemText = "d. khandiva+11" AndAlso candidate.Evidence = "s> d. khandiva+11 max as = 130m",
              "validation mutated the model's input objects")

        Dim wrapped = "S> D." & vbLf & "  KHANDIVA+11 MAX AS"
        Accepted(Post("xanzy", wrapped), Entry("xanzy", "sell", "d. khandiva+11", "s> d. khandiva+11 max as"),
            "D. KHANDIVA+11", wrapped)
        Accepted(Post("xanzy", wrapped), Entry("xanzy", "sell", "d." & vbLf & "khandiva+11", "s> d. khandiva+11 max as", "D. KHANDIVA+11"),
            "D. KHANDIVA+11", wrapped)

        Dim tabbed = "S> Tikoy" & vbTab & "7D = 7M"
        Accepted(Post("Cali", tabbed), Entry("Cali", "sell", "TIKOY 7d", "s> tikoy 7d = 7m"), "Tikoy 7D", tabbed)

        Dim crlf = "S> ROR" & vbCrLf & "Asura+9"
        Accepted("HalaAngKABAW" & vbCrLf & crlf, Entry("HalaAngKABAW", "sell", "ror asura+9", "s> ror asura+9"),
            "ROR Asura+9", crlf.Replace(vbCrLf, vbLf))

        Dim spaces = "S>  Ror   Asura with stats"
        Accepted(Post("HalaAngKABAW", spaces), Entry("HalaAngKABAW", "sell", "ROR ASURA", "ror asura with stats"),
            "Ror Asura", "Ror   Asura with stats")
    End Sub

    Private Sub TestHistoricalMixedTradePosts()
        Dim body = "S> B.orb = 5.5m || RuMagic = 1.5m || Ror asura with stats || B> rhodacapa+9"
        Dim raw = Post("HalaAngKABAW", body)
        Dim listingSpecs = New (Intent As String, Item As String, Expected As String)() {
            ("sell", "b.ORB", "B.orb"), ("sell", "rumagic", "RuMagic"),
            ("sell", "ROR ASURA", "Ror asura"), ("buy", "RHODACAPA+9", "rhodacapa+9")}
        For Each spec In listingSpecs
            Accepted(raw, Entry("HalaAngKABAW", spec.Intent, spec.Item, body.ToUpperInvariant()), spec.Expected, body)
        Next
        Dim all = TradeAiService.ValidateListings(listingSpecs.Select(Function(spec) Entry("HalaAngKABAW", spec.Intent, spec.Item, body.ToUpperInvariant())), raw)
        Check(all.Count = 4 AndAlso all.Where(Function(value) value.Intent = "sell").Count() = 3 AndAlso all.Where(Function(value) value.Intent = "buy").Count() = 1,
              "mixed-item historical post lost an offered item or its later explicit buy clause")

        Dim other = "B> Skeleton necklace 1bun = 1.5m or trade to my nameless tapeline || S> ror asu"
        Accepted(Post("HalaAngKABAW", other), Entry("HalaAngKABAW", "buy", "SKELETON NECKLACE", other.ToLowerInvariant()), "Skeleton necklace", other)
        Accepted(Post("HalaAngKABAW", other), Entry("HalaAngKABAW", "sell", "ROR ASU", other.ToUpperInvariant()), "ror asu", other)
        Rejected(Post("HalaAngKABAW", other), Entry("HalaAngKABAW", "sell", "Skeleton necklace", other), "a buy item became a sell listing in a mixed historical post")
    End Sub

    Private Sub TestAuthorHeaderEvidence()
        Dim body = "SELLING Tikoy 7D - 7m each"
        Dim raw = Post("Cali", body)
        Accepted(raw, Entry("Cali", "sell", "TIKOY 7d", raw.ToLowerInvariant()), "Tikoy 7D", raw)
        Accepted(raw, Entry("Cali", "sell", "Tikoy 7D", "Cali" & vbLf & "SELLING Tikoy 7D"), "Tikoy 7D", "Cali" & vbLf & "SELLING Tikoy 7D")
        Rejected(raw, Entry("cali", "sell", "Tikoy 7D", raw), "model lowercasing of a recipient was accepted")
        Rejected(raw, Entry("CALI", "sell", "Tikoy 7D", body), "model uppercasing of a recipient was accepted")
        Dim metadata = "Cali" & vbLf & "APP" & vbLf & "8:07 AM" & vbLf & body
        Accepted(metadata, Entry("Cali", "sell", "tikoy 7D", metadata.ToLowerInvariant()), "Tikoy 7D", metadata)
    End Sub

    Private Sub TestUnsupportedChangesAndBoundaries()
        Dim raw = Post("Seller", "S> D.KHANDIVA+11 || Tikoy 7DAYS || D.Potra+90")
        Dim body = "S> D.KHANDIVA+11 || Tikoy 7DAYS || D.Potra+90"
        For Each item In {"D KHANDIVA+11", "D.KHANDIVA+12", "KHANDIVA D.+11", "D.KHANDIVA", "Tikoy 7D", "D.Potra+9", "Invented+11"}
            Rejected(raw, Entry("Seller", "sell", item, body), "invented/reordered punctuation, digits, omitted upgrade, or partial token was accepted: " & item)
        Next
        For Each evidence In {"S> D KHANDIVA+11", "S> D.KHANDIVA+12", "S> KHANDIVA D.+11", "S> D.KHANDIVA+11 || D.Potra+90 || Tikoy 7DAYS"}
            Rejected(raw, Entry("Seller", "sell", "D.KHANDIVA+11", evidence), "edited/moved evidence was accepted: " & evidence)
        Next
        Accepted(raw, Entry("Seller", "sell", "tikoy 7days", body), "Tikoy 7DAYS", body)
        Accepted(raw, Entry("Seller", "sell", "d.potra+90", body), "D.Potra+90", body)
        Rejected(raw, Entry("Seller", "sell", "KHANDIVA+11", body), "an item-code prefix was omitted across a joining period")
        Rejected(Post("Seller", "S> B.orb"), Entry("Seller", "sell", "orb", "S> B.orb"), "a joining period allowed an internal item substring")
        Accepted(Post("Seller", "S> Tikoy 7D."), Entry("Seller", "sell", "tikoy 7d", "S> Tikoy 7D."), "Tikoy 7D", "S> Tikoy 7D.")
    End Sub

    Private Sub TestPostAndIntentOwnership()
        Dim alice = Post("Alice", "S> Tikoy 7D")
        Dim bob = Post("Bob", "S> RuMagic")
        Dim raw = alice & vbLf & vbLf & bob
        Rejected(raw, Entry("Alice", "sell", "RuMagic", "S> RuMagic"), "item/evidence borrowed from another author was accepted")
        Rejected(raw, Entry("Alice", "sell", "Tikoy 7D", raw), "evidence spanning authors was accepted")
        Rejected(raw, Entry("Bob", "sell", "Tikoy 7D", "S> Tikoy 7D"), "source author was transferred to another post")
        Rejected(raw, Entry("Alice", "buy", "Tikoy 7D", "S> Tikoy 7D"), "a sell-only post accepted a buy listing")
        Rejected(Post("Tikoy", "S> RuMagic"), Entry("Tikoy", "sell", "Tikoy", "Tikoy" & vbLf & "S> RuMagic"),
            "an item occurring only in the post's own author header was accepted")

        Dim repeated = "S> Tikoy 7D || B> Tikoy 7D"
        Rejected(Post("Cali", repeated), Entry("Cali", "buy", "Tikoy 7D", "S> Tikoy 7D"), "a repeated later buy occurrence validated a sell-only evidence excerpt")
        Rejected(Post("Cali", repeated), Entry("Cali", "sell", "Tikoy 7D", "B> Tikoy 7D"), "a repeated earlier sell occurrence validated a buy-only evidence excerpt")
        Accepted(Post("Cali", repeated), Entry("Cali", "sell", "tikoy 7d", repeated), "Tikoy 7D", repeated)
        Accepted(Post("Cali", repeated), Entry("Cali", "buy", "tikoy 7d", repeated), "Tikoy 7D", repeated)

        Dim tradeOnly = "T> D.KHANDIVA+11 to your Tikoy 7D"
        Rejected(Post("xxxxxxx1", tradeOnly), Entry("xxxxxxx1", "sell", "D.KHANDIVA+11", tradeOnly), "pure trade-only offer became sell")
        Rejected(Post("xxxxxxx1", tradeOnly), Entry("xxxxxxx1", "buy", "Tikoy 7D", tradeOnly), "pure trade-only exchange target became buy")
    End Sub

    Private Sub TestFollowDeliveryIdentity()
        Const header As String = "Followed announcement delivery identity (not a verified game character): Kathana"
        Dim anonymous = header & vbLf & "S> Tikoy 7D"
        Rejected(anonymous, Entry("Kathana", "sell", "Tikoy 7D", "S> Tikoy 7D"), "Follow delivery identity was accepted as a game recipient")
        Dim body = "S> Tikoy 7D || PM RealSeller"
        Dim raw = header & vbLf & body
        Accepted(raw, Entry("RealSeller", "sell", "TIKOY 7d", body.ToLowerInvariant()), "Tikoy 7D", body)
        Rejected(raw, Entry("realseller", "sell", "Tikoy 7D", body), "explicit Follow contact case was changed")
        Rejected(raw, Entry("Kathana", "sell", "Tikoy 7D", body), "Follow delivery name displaced its explicit contact")
        Dim namedItem = "S> ROR for DEVAxSAITAMAx"
        Rejected(header & vbLf & namedItem, Entry("DEVAxSAITAMAx", "sell", "ROR", namedItem), "a character in an item description was treated as an explicit Follow contact")
        Dim contact = "S> RuMagic || IGN: ExactIGN"
        Accepted(header & vbLf & contact, Entry("ExactIGN", "sell", "rumagic", contact), "RuMagic", contact)
    End Sub

    Private Sub TestAdditionalExplicitMarkers()
        For Each marker In {"S-", "S/", "S/T>", "SELL", "SELLING", "WTS", "for sale"}
            Dim body = marker & " Tikoy 7D"
            Accepted(Post("Cali", body), Entry("Cali", "sell", "tikoy 7d", body.ToUpperInvariant()), "Tikoy 7D", body)
            Rejected(Post("Cali", body), Entry("Cali", "buy", "Tikoy 7D", body), "sell marker was accepted as buy: " & marker)
        Next
        For Each marker In {"B-", "B/", "BUY", "BUYING", "WTB"}
            Dim body = marker & " Tikoy 7D"
            Accepted(Post("Cali", body), Entry("Cali", "buy", "tikoy 7d", body.ToLowerInvariant()), "Tikoy 7D", body)
            Rejected(Post("Cali", body), Entry("Cali", "sell", "Tikoy 7D", body), "buy marker was accepted as sell: " & marker)
        Next
        For Each marker In {"SELL-", "WTS-", "BUY-"}
            Dim body = marker & "Tikoy 7D"
            Accepted(Post("Cali", body), Entry("Cali", If(marker.StartsWith("BUY", StringComparison.Ordinal), "buy", "sell"), "tikoy 7d", body.ToLowerInvariant()),
                "Tikoy 7D", body)
        Next
        Dim tradeOnly = "T- D.Potra+9"
        Rejected(Post("YunaSkye", tradeOnly), Entry("YunaSkye", "sell", "D.Potra+9", tradeOnly), "T- trade-only marker became sell")
        Dim mixed = "T- D.Potra+9 || B- Tikoy 7D"
        Accepted(Post("YunaSkye", mixed), Entry("YunaSkye", "buy", "TIKOY 7d", mixed), "Tikoy 7D", mixed)
        For Each word In {"BUYBACK", "SELLER", "BUYERS", "SELLINGLY"}
            Dim body = word & " Tikoy 7D"
            Rejected(Post("Cali", body), Entry("Cali", If(word.StartsWith("BUY", StringComparison.Ordinal), "buy", "sell"), "Tikoy 7D", body),
                "non-marker word prefix was interpreted as an offer/request: " & word)
        Next
    End Sub

    Private Sub TestInvalidAndMixedCollections()
        Dim body = "S> Tikoy 7D"
        Dim raw = Post("Cali", body)
        Dim valid = Entry("Cali", "sell", "tikoy 7d", body)
        Dim invalid = Entry("Cali", "sell", "Invented", body)
        Dim mixed = TradeAiService.ValidateListings({invalid, valid, Nothing, Entry("cali", "sell", "Tikoy 7D", body)}, raw)
        Check(mixed.Count = 1 AndAlso mixed.Single().ItemText = "Tikoy 7D", "mixed valid/invalid results failed entirely or retained unsupported entries")
        Check(TradeAiService.ValidateListings(Array.Empty(Of TradeListing)(), raw).Count = 0, "a genuinely empty extraction was treated as invalid")
        Dim deduplicated = TradeAiService.ValidateListings({valid, Entry("Cali", "sell", "TIKOY 7D", body.ToLowerInvariant())}, raw)
        Check(deduplicated.Count = 1, "normalization introduced case-only duplicate listings")
        Rejected(raw, invalid, "an all-invalid extraction did not report a source-verification failure")
        Rejected(raw, Entry("Cali", "trade", "Tikoy 7D", body), "an unsupported intent entered results")
        Rejected(raw, Entry("APP", "sell", "Tikoy 7D", body), "metadata became a recipient")
    End Sub

    Private Function Reply(listings As IEnumerable(Of TradeListing)) As HttpResponseMessage
        Dim response = JsonSerializer.Serialize(New With {.status = "completed", .output = New Object() {
            New With {.type = "message", .content = New Object() {
                New With {.type = "output_text", .text = JsonSerializer.Serialize(New TradeExtraction With {.Listings = listings.ToList()})}}}}})
        Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(response, Encoding.UTF8, "application/json")}
    End Function

    Private NotInheritable Class MockHandler
        Inherits HttpMessageHandler
        Private ReadOnly respond As Func(Of Integer, HttpResponseMessage)
        Public Property Calls As Integer

        Public Sub New(callback As Func(Of Integer, HttpResponseMessage))
            respond = callback
        End Sub

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            cancellation.ThrowIfCancellationRequested()
            Calls += 1
            Return Task.FromResult(respond(Calls))
        End Function
    End Class

    Private Function Signature(listings As IEnumerable(Of TradeListing)) As String
        Return JsonSerializer.Serialize(listings)
    End Function

    Private Async Function TestApiNormalizationAndCache() As Task
        Dim author = "V" & Guid.NewGuid().ToString("N").Substring(0, 16)
        Dim body = "S> B.orb || RuMagic || B> Rhodacapa+9"
        Dim raw = Post(author, body)
        Dim posts As New List(Of DiscordTradeMessage) From {New DiscordTradeMessage With {
            .Id = "1556808682858086542", .MessageType = 0, .AuthorName = author, .BodyText = body}}
        Dim handler As New MockHandler(Function(callNumber) Reply({
            Entry(author, "sell", "b.ORB", (author & vbLf & body).ToUpperInvariant()),
            Entry(author, "sell", "RUMAGIC", body.ToLowerInvariant()),
            Entry(author, "buy", "rhodacapa+9", body.ToLowerInvariant()),
            Entry(author, "sell", "Invented", body)}))
        Using transport As New HttpClient(handler)
            Dim uncached = Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Check(handler.Calls = 1 AndAlso uncached.Count = 3, "uncached API validation lost safe listings or retained an invalid item")
            Check(uncached.Select(Function(value) value.ItemText).SequenceEqual({"B.orb", "RuMagic", "Rhodacapa+9"}), "API returned model spellings instead of original item variants")
            Check(uncached(0).Evidence = raw AndAlso uncached(1).Evidence = body, "API did not recover author-header/body evidence")
            Dim cached = Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts)
            Check(handler.Calls = 2 AndAlso Signature(uncached) = Signature(cached), "cache-enabled validation differs from uncached results")
            Dim replay = Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts)
            Check(handler.Calls = 2 AndAlso Signature(cached) = Signature(replay), "normalized cache replay repeated HTTP or altered source-supported results")
            Dim queue = TradeAiService.BuildQueue(replay, True, {"B.ORB", "RUMAGIC"}, "Buy {items}")
            Check(queue.Count = 1 AndAlso queue.Single().CharacterName = author AndAlso queue.Single().Message = "Buy B.orb, RuMagic",
                  "normalized validated output did not preserve recipient/item spelling in reviewed whispers")
        End Using
    End Function

    Private Async Function TestApiRejectionDoesNotRetryOrCache() As Task
        Dim author = "V" & Guid.NewGuid().ToString("N").Substring(0, 16)
        Dim body = "S> Tikoy 7DAYS"
        Dim raw = Post(author, body)
        Dim handler As New MockHandler(Function(callNumber) Reply({Entry(author, "sell", If(callNumber = 1, "Tikoy 7D", "tikoy 7days"), body)}))
        Using transport As New HttpClient(handler)
            Dim failed As Boolean
            Try
                Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel, CancellationToken.None, transport)
            Catch ex As InvalidOperationException
                failed = ex.Message.Contains("original character", StringComparison.OrdinalIgnoreCase) AndAlso ex.Message.Contains("No partial queue", StringComparison.OrdinalIgnoreCase)
            End Try
            Check(failed AndAlso handler.Calls = 1, "an all-rejected API batch returned partial results or spent another request automatically")
            Dim recovered = Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel, CancellationToken.None, transport)
            Check(handler.Calls = 2 AndAlso recovered.Count = 1 AndAlso recovered.Single().ItemText = "Tikoy 7DAYS", "validation failure was cached or a later source-supported response remained rejected")
            Dim replay = Await TradeAiService.AnalyzeAsync(raw, "offline-validation-key", TradeAiService.DefaultAnalysisModel, CancellationToken.None, transport)
            Check(handler.Calls = 2 AndAlso Signature(recovered) = Signature(replay), "only the subsequent successful validation was not cached correctly")
        End Using
    End Function
End Module
