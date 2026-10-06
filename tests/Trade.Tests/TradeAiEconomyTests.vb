Imports System.Collections.Concurrent
Imports System.Diagnostics
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Friend Module TradeAiEconomyTests
    Private checks As Integer

    Public Async Function RunAsync() As Task
        checks = 0
        TestEconomyPayload()
        Await TestBoundedConcurrencyAndOrder()
        Await TestFailureDrainsWorkers()
        Await TestCancellationStopsDispatch()
        Await TestSuccessfulCacheCopiesAndKeys()
        Await TestEmptySuccessIsCached()
        Await TestCacheValidationContext()
        Await TestFailuresAreNotCached()
        Await TestBoundedCacheEviction()
        Console.WriteLine($"PASS: {checks} offline AI economy assertions: nano/minimal payload, four overlapping workers, deterministic out-of-order completion, first-error/cancel draining, atomic results, and success-only cache replay/isolation.")
    End Function

    Private Sub Check(condition As Boolean, reason As String)
        Interlocked.Increment(checks)
        If Not condition Then Throw New InvalidOperationException("AI economy test: " & reason)
    End Sub

    Private Sub TestEconomyPayload()
        Check(TradeAiService.DefaultAnalysisModel = "gpt-5-nano", "Trade does not expose the economical default model")
        Check(TradeAiService.MaximumBatchPosts = 50 AndAlso TradeAiService.MaximumBatchCharacters = 8000,
              "Trade batching lost its fifty-post/eight-thousand-character limits")
        Dim nano = TradeAiService.BuildPayload("Seller" & vbLf & "S> Tikoy 7D", TradeAiService.DefaultAnalysisModel)
        Check(nano("model").GetValue(Of String)() = "gpt-5-nano" AndAlso nano("reasoning")("effort").GetValue(Of String)() = "minimal",
              "Nano analysis does not request minimal reasoning")
        Check(Not nano("store").GetValue(Of Boolean)() AndAlso nano("text")("format")("strict").GetValue(Of Boolean)(),
              "Economy payload changed response privacy or strict structured extraction")
        Check(nano("service_tier").GetValue(Of String)() = "default", "Economy payload can inherit premium project processing")
        Check(TradeAiService.BuildPayload("Seller" & vbLf & "S> Tikoy 7D", "configured-test-model")("model").GetValue(Of String)() = "configured-test-model",
              "An explicit test/model selection was ignored")
    End Sub

    Private Function CreatePosts(count As Integer) As List(Of DiscordTradeMessage)
        Dim tag = Guid.NewGuid().ToString("N").Substring(0, 8)
        Return Enumerable.Range(0, count).Select(Function(index) New DiscordTradeMessage With {
            .Id = (1500000000000000000UL + CULng(index)).ToString(), .MessageType = 0,
            .AuthorName = "E" & tag & "_" & index.ToString("D5"), .BodyText = "S> Tikoy 7D"}).ToList()
    End Function

    Private Function Source(posts As IEnumerable(Of DiscordTradeMessage)) As String
        Return String.Join(vbLf & vbLf, posts.Select(Function(post) post.AuthorName & vbLf & post.BodyText))
    End Function

    Private Function Listings(raw As String, key As String) As List(Of TradeListing)
        Return Regex.Matches(raw, "(?m)^(E[a-f0-9]{8}_[0-9]+)\r?$").Cast(Of Match)().Select(Function(match) New TradeListing With {
            .CharacterName = match.Groups(1).Value, .Intent = "sell", .ItemText = "Tikoy 7D", .ItemKey = key, .Evidence = "S> Tikoy 7D"}).ToList()
    End Function

    Private Function Reply(listings As IEnumerable(Of TradeListing)) As HttpResponseMessage
        Dim body = JsonSerializer.Serialize(New With {.status = "completed", .output = New Object() {
            New With {.type = "message", .content = New Object() {
                New With {.type = "output_text", .text = JsonSerializer.Serialize(New TradeExtraction With {.Listings = listings.ToList()})}}}}})
        Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function

    Private Async Function TestBoundedConcurrencyAndOrder() As Task
        Dim posts = CreatePosts(TradeAiService.MaximumBatchPosts * 6)
        Dim progress As New RecordedProgress()
        Dim handler As New ControlledHandler()
        Using transport As New HttpClient(handler)
            Dim analysis = TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, progress, posts, useCache:=False)
            Await UntilAsync(Function() handler.Requests.Count = 4, "four initial workers did not overlap")
            Check(handler.Active = 4 AndAlso handler.MaximumActive = 4 AndAlso Not analysis.IsCompleted,
                  "The implementation did not overlap exactly four bounded initial requests")
            handler.Complete(3)
            Await UntilAsync(Function() handler.Requests.Count = 5, "completion did not start the next bounded batch")
            handler.Complete(2)
            Await UntilAsync(Function() handler.Requests.Count = 6, "a freed worker did not process the remaining batch")
            handler.Complete(1)
            handler.Complete(5)
            handler.Complete(4)
            Await UntilAsync(Function() handler.Active = 1, "out-of-order workers did not settle")
            Check(Not analysis.IsCompleted AndAlso progress.Updates.Last().CompletedBatches > 0,
                  "Analysis returned a partial result or suppressed progress until every request finished")
            handler.Complete(0)
            Dim result = Await analysis.WaitAsync(TimeSpan.FromSeconds(3))
            Check(result.Select(Function(listing) listing.CharacterName).SequenceEqual(posts.Select(Function(post) post.AuthorName)),
                  "Concurrent completion changed deterministic original-post result order")
            Check(result.All(Function(listing) listing.ItemKey = "FIRST BATCH KEY"), "Out-of-order completion selected a later alias as the stable item key")
            Check(handler.MaximumActive = 4 AndAlso handler.Requests.Count = 6 AndAlso handler.Active = 0,
                  "Concurrent analysis exceeded its bound or left requests running after success")
            Dim updates = progress.Updates
            Check(updates.First().CompletedBatches = 0 AndAlso updates.First().ProcessedPosts = 0,
                  "Concurrent analysis did not report initial zero progress")
            Check(updates.Select(Function(value) value.CompletedBatches).SequenceEqual(updates.Select(Function(value) value.CompletedBatches).OrderBy(Function(value) value)) AndAlso
                  updates.Select(Function(value) value.ProcessedPosts).SequenceEqual(updates.Select(Function(value) value.ProcessedPosts).OrderBy(Function(value) value)),
                  "Concurrent progress counters/report delivery went backward")
            Dim final = updates.Last()
            Check(final.CompletedBatches = 6 AndAlso final.TotalBatches = 6 AndAlso final.ProcessedPosts = posts.Count AndAlso
                  final.TotalPosts = posts.Count AndAlso final.ListingCount = result.Count, "Concurrent progress omitted completed source posts or final listings")
        End Using
    End Function

    Private Async Function TestFailureDrainsWorkers() As Task
        Dim posts = CreatePosts(TradeAiService.MaximumBatchPosts * 6)
        Dim handler As New ControlledHandler With {.DelayCancellationCleanup = True}
        Using transport As New HttpClient(handler)
            Dim analysis = TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts, useCache:=False)
            Await UntilAsync(Function() handler.Requests.Count = 4, "failure fixture did not start four workers")
            handler.Requests(0).Response.SetResult(New HttpResponseMessage(HttpStatusCode.TooManyRequests) With {.Content = New StringContent("{}")})
            Await UntilAsync(Function() Enumerable.Range(1, 3).All(Function(index) handler.Requests(index).CancellationObserved.Task.IsCompleted),
                "First batch failure did not cancel the other started workers")
            Check(Not analysis.IsCompleted AndAlso handler.Requests.Count = 4,
                  "A failure returned before draining started workers or dispatched remaining batches")
            For index = 1 To 3
                handler.Requests(index).CleanupRelease.SetResult(True)
            Next
            Dim failed As Boolean
            Try
                Await analysis.WaitAsync(TimeSpan.FromSeconds(3))
            Catch ex As InvalidOperationException
                failed = ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) OrElse ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase)
            End Try
            Check(failed AndAlso analysis.IsFaulted AndAlso handler.Active = 0 AndAlso handler.Requests.Count = 4,
                  "First substantive failure was hidden by internal cancellation, returned partial results, retried, or left active workers")
        End Using
    End Function

    Private Async Function TestCancellationStopsDispatch() As Task
        Dim posts = CreatePosts(TradeAiService.MaximumBatchPosts * 6)
        Dim handler As New ControlledHandler With {.DelayCancellationCleanup = True}
        Using transport As New HttpClient(handler), cancellation As New CancellationTokenSource()
            Dim analysis = TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                cancellation.Token, transport, sourcePosts:=posts, useCache:=False)
            Await UntilAsync(Function() handler.Requests.Count = 4, "cancellation fixture did not start four workers")
            cancellation.Cancel()
            Await UntilAsync(Function() handler.Requests.Values.All(Function(request) request.CancellationObserved.Task.IsCompleted),
                "Caller cancellation did not reach every active request")
            Check(Not analysis.IsCompleted AndAlso handler.Requests.Count = 4, "Cancellation did not drain all started requests or launched remaining batches")
            For Each request In handler.Requests.Values
                request.CleanupRelease.SetResult(True)
            Next
            Dim cancelled As Boolean
            Try
                Await analysis.WaitAsync(TimeSpan.FromSeconds(3))
            Catch ex As OperationCanceledException
                cancelled = True
            End Try
            Check(cancelled AndAlso analysis.IsCanceled AndAlso handler.Active = 0 AndAlso handler.Requests.Count = 4,
                  "Cancelled parallel analysis returned listings, reported a job timeout, or left requests active")
        End Using
    End Function

    Private Async Function TestSuccessfulCacheCopiesAndKeys() As Task
        Dim posts = CreatePosts(2)
        Dim handler As New ImmediateHandler(Function(raw, callNumber, cancellation) Task.FromResult(Reply(Listings(raw, "CACHE ORIGINAL"))))
        Using transport As New HttpClient(handler)
            Dim result = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts)
            Check(handler.Calls = 1 AndAlso result.Count = 2, "Successful cache fixture did not make its initial request")
            result(0).ItemKey = "MUTATED"
            result(0).Evidence = "MUTATED"
            result.Clear()
            Dim progress As New RecordedProgress()
            Dim replay = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, progress, posts)
            Check(handler.Calls = 1 AndAlso replay.Count = 2 AndAlso replay.All(Function(listing) listing.ItemKey = "CACHE ORIGINAL" AndAlso listing.Evidence = "S> Tikoy 7D"),
                  "Success cache replay performed HTTP or reused caller-mutated listing/list objects")
            Check(progress.Updates.Last().ProcessedPosts = 2 AndAlso progress.Updates.Last().CompletedBatches = 1,
                  "Cached completion omitted progress or skipped posts")
            Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", "changed-explicit-model", CancellationToken.None, transport, sourcePosts:=posts)
            Check(handler.Calls = 2, "A model change reused the wrong cached extraction")
            posts(0).BodyText &= " updated source"
            Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel, CancellationToken.None, transport, sourcePosts:=posts)
            Check(handler.Calls = 3, "Changed exact source text reused the old cached extraction")
            Using cancellation As New CancellationTokenSource()
                cancellation.Cancel()
                Dim cancelled As Boolean
                Try
                    Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel, cancellation.Token, transport, sourcePosts:=posts)
                Catch ex As OperationCanceledException
                    cancelled = True
                End Try
                Check(cancelled AndAlso handler.Calls = 3, "A cancelled cached request returned results or performed HTTP")
            End Using
        End Using
    End Function

    Private Async Function TestFailuresAreNotCached() As Task
        For Each mode In {"invalid", "incomplete", "http", "cancel"}
            Dim posts = CreatePosts(2)
            Using cancellation As New CancellationTokenSource()
                Dim handler As New ImmediateHandler(
                    Function(raw, callNumber, token)
                        If callNumber = 1 Then
                            If mode = "cancel" Then
                                cancellation.Cancel()
                                token.ThrowIfCancellationRequested()
                            End If
                            If mode = "invalid" Then Return Task.FromResult(Reply({New TradeListing With {.CharacterName = "AbsentSeller", .Intent = "sell",
                                .ItemText = "Tikoy 7D", .ItemKey = "TIKOY 7D", .Evidence = "S> Tikoy 7D"}}))
                            Dim response = New HttpResponseMessage(If(mode = "http", HttpStatusCode.TooManyRequests, HttpStatusCode.OK)) With {
                                .Content = New StringContent("{""status"":""incomplete"",""output"":[]}", Encoding.UTF8, "application/json")}
                            Return Task.FromResult(response)
                        End If
                        Return Task.FromResult(Reply(Listings(raw, "VERIFIED")))
                    End Function)
                Using transport As New HttpClient(handler)
                    Dim failed As Boolean
                    Try
                        Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                            cancellation.Token, transport, sourcePosts:=posts)
                    Catch ex As OperationCanceledException When mode = "cancel"
                        failed = True
                    Catch ex As InvalidOperationException When mode <> "cancel"
                        failed = True
                    End Try
                    Check(failed AndAlso handler.Calls = 1, mode & " did not fail atomically without retry")
                    Dim retry = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                        CancellationToken.None, transport, sourcePosts:=posts)
                    Check(handler.Calls = 2 AndAlso retry.Count = 2, mode & " failure was cached or skipped instead of re-requested")
                    Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                        CancellationToken.None, transport, sourcePosts:=posts)
                    Check(handler.Calls = 2, "Verified success after " & mode & " failure was not cached")
                End Using
            End Using
        Next
    End Function

    Private Async Function TestEmptySuccessIsCached() As Task
        Dim posts = CreatePosts(1)
        posts(0).BodyText = "Thanks for the update."
        Dim handler As New ImmediateHandler(Function(raw, callNumber, cancellation) Task.FromResult(Reply(Array.Empty(Of TradeListing)())))
        Using transport As New HttpClient(handler)
            Dim first = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts)
            Dim replay = Await TradeAiService.AnalyzeAsync(Source(posts), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=posts)
            Check(first.Count = 0 AndAlso replay.Count = 0 AndAlso handler.Calls = 1,
                  "A verified successful no-listings batch was treated as a cache miss")
        End Using
    End Function

    Private Async Function TestBoundedCacheEviction() As Task
        Dim first = CreatePosts(1)
        Dim latest As List(Of DiscordTradeMessage) = Nothing
        Dim handler As New ImmediateHandler(Function(raw, callNumber, cancellation) Task.FromResult(Reply(Listings(raw, "BOUNDED CACHE"))))
        Using transport As New HttpClient(handler)
            Await TradeAiService.AnalyzeAsync(Source(first), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=first)
            For index = 1 To 512
                latest = CreatePosts(1)
                Await TradeAiService.AnalyzeAsync(Source(latest), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                    CancellationToken.None, transport, sourcePosts:=latest)
            Next
            Check(handler.Calls = 513, "Unique tiny batches reused unrelated cached source data")
            Await TradeAiService.AnalyzeAsync(Source(latest), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=latest)
            Check(handler.Calls = 513, "Cache eviction discarded the most recent successful batch")
            Await TradeAiService.AnalyzeAsync(Source(first), "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=first)
            Check(handler.Calls = 514, "Success cache retained its oldest batch beyond the 512-entry bound")
        End Using
    End Function

    Private Async Function TestCacheValidationContext() As Task
        Dim separate = CreatePosts(2)
        Dim raw = Source(separate)
        Dim joined = New List(Of DiscordTradeMessage) From {New DiscordTradeMessage With {
            .Id = separate(0).Id, .MessageType = 0, .AuthorName = separate(0).AuthorName,
            .BodyText = separate(0).BodyText & vbLf & vbLf & separate(1).AuthorName & vbLf & separate(1).BodyText}}
        Check(Source(joined) = raw, "Cache-context fixture did not retain identical request text")
        Dim handler As New ImmediateHandler(Function(input, callNumber, cancellation) Task.FromResult(Reply(Listings(input, "CONTEXT KEY"))))
        Using transport As New HttpClient(handler)
            Dim first = Await TradeAiService.AnalyzeAsync(raw, "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=separate)
            Dim changed = Await TradeAiService.AnalyzeAsync(raw, "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=joined)
            Check(first.Count = 2 AndAlso changed.Count = 1 AndAlso changed(0).CharacterName = separate(0).AuthorName AndAlso handler.Calls = 2,
                  "Identical raw text with different post boundaries reused stale author/evidence validation")
            Dim separateReplay = Await TradeAiService.AnalyzeAsync(raw, "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=separate)
            Dim joinedReplay = Await TradeAiService.AnalyzeAsync(raw, "offline-economy-key", TradeAiService.DefaultAnalysisModel,
                CancellationToken.None, transport, sourcePosts:=joined)
            Check(separateReplay.Count = 2 AndAlso joinedReplay.Count = 1 AndAlso handler.Calls = 2,
                  "Cache context changes overwrote another validated result subset or repeated HTTP")
        End Using
    End Function

    Private Async Function UntilAsync(condition As Func(Of Boolean), reason As String) As Task
        Dim elapsed = Stopwatch.StartNew()
        While Not condition() AndAlso elapsed.ElapsedMilliseconds < 3000
            Await Task.Delay(1).ConfigureAwait(False)
        End While
        Check(condition(), reason)
    End Function

    Private NotInheritable Class RecordedProgress
        Implements IProgress(Of TradeAnalysisProgress)
        Private ReadOnly values As New List(Of TradeAnalysisProgress)()
        Public ReadOnly Property Updates As List(Of TradeAnalysisProgress)
            Get
                SyncLock values
                    Return values.ToList()
                End SyncLock
            End Get
        End Property
        Public Sub Report(value As TradeAnalysisProgress) Implements IProgress(Of TradeAnalysisProgress).Report
            SyncLock values
                values.Add(value)
            End SyncLock
        End Sub
    End Class

    Private NotInheritable Class PendingRequest
        Public Property Raw As String
        Public ReadOnly Response As New TaskCompletionSource(Of HttpResponseMessage)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public ReadOnly CancellationObserved As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public ReadOnly CleanupRelease As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
    End Class

    Private NotInheritable Class ControlledHandler
        Inherits HttpMessageHandler
        Public ReadOnly Requests As New ConcurrentDictionary(Of Integer, PendingRequest)()
        Public Property DelayCancellationCleanup As Boolean
        Private activeStore As Integer
        Private maximumStore As Integer
        Public ReadOnly Property Active As Integer
            Get
                Return Volatile.Read(activeStore)
            End Get
        End Property
        Public ReadOnly Property MaximumActive As Integer
            Get
                Return Volatile.Read(maximumStore)
            End Get
        End Property
        Public Sub Complete(index As Integer)
            Dim request = Requests(index)
            request.Response.SetResult(Reply(Listings(request.Raw, If(index = 0, "FIRST BATCH KEY", "LATER BATCH KEY"))))
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            Dim raw As String
            Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellation).ConfigureAwait(False))
                raw = document.RootElement.GetProperty("input")(0).GetProperty("content").GetString()
            End Using
            Dim first = Integer.Parse(Regex.Match(raw, "(?m)^E[a-f0-9]{8}_([0-9]+)").Groups(1).Value)
            Dim index = first \ TradeAiService.MaximumBatchPosts
            Dim current = Interlocked.Increment(activeStore)
            Dim pending As New PendingRequest With {.Raw = raw}
            SyncLock Requests
                maximumStore = Math.Max(maximumStore, current)
                Check(Requests.TryAdd(index, pending), "A controlled batch was retried or dispatched twice")
            End SyncLock
            Try
                Dim interrupted As OperationCanceledException = Nothing
                Try
                    Return Await pending.Response.Task.WaitAsync(cancellation).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    pending.CancellationObserved.TrySetResult(True)
                    interrupted = ex
                End Try
                If DelayCancellationCleanup Then Await pending.CleanupRelease.Task.ConfigureAwait(False)
                Throw interrupted
            Finally
                Interlocked.Decrement(activeStore)
            End Try
        End Function
    End Class

    Private NotInheritable Class ImmediateHandler
        Inherits HttpMessageHandler
        Private ReadOnly responder As Func(Of String, Integer, CancellationToken, Task(Of HttpResponseMessage))
        Private callStore As Integer
        Public Sub New(responder As Func(Of String, Integer, CancellationToken, Task(Of HttpResponseMessage)))
            Me.responder = responder
        End Sub
        Public ReadOnly Property Calls As Integer
            Get
                Return Volatile.Read(callStore)
            End Get
        End Property
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellation).ConfigureAwait(False))
                Dim raw = document.RootElement.GetProperty("input")(0).GetProperty("content").GetString()
                Return Await responder(raw, Interlocked.Increment(callStore), cancellation).ConfigureAwait(False)
            End Using
        End Function
    End Class
End Module
