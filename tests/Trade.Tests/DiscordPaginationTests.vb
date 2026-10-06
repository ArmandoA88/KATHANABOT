Imports System.Globalization
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Module DiscordPaginationTests
    Private Const GuildId As String = "1483326612538654813"
    Private Const ChannelId As String = "1556796091855278112"
    Private Const ChannelUrl As String = "https://discord.com/channels/1483326612538654813/1556796091855278112"
    Private Const Credential As String = "offline-pagination-fixture-credential"
    Private Const BaseId As ULong = 1556800000000100000UL
    Private checks As Integer

    Public Async Function RunAsync() As Task
        Await TestSelectedCounts()
        Await TestOverlappingPages()
        Await TestBoundsAndCancellation()
        Await TestRateLimits()
        Await TestMaximumHistory()
        TestRecentMerge()
        Console.WriteLine($"PASS: {checks} offline Discord history assertions: selectable counts, before cursors, page progress, chronological exact-ID deduplication, retries, cancellation, bounded responses, 10000 posts and incremental merging.")
    End Function

    Private Async Function TestSelectedCounts() As Task
        Check(DiscordTradeService.DefaultImportMessageCount = 10000 AndAlso DiscordTradeService.LatestMessageLimit = 100, "Default import count or Discord page size changed")
        For Each count In {1, 3, 101, 250}
            Dim replies As New List(Of HttpResponseMessage) From {Metadata()}
            Dim remaining = count
            While remaining > 0
                Dim take = Math.Min(100, remaining)
                replies.Add(Page(remaining, take))
                remaining -= take
            End While
            Dim progress As New RecordingProgress()
            Using handler As New QueueHandler(replies), transport As New HttpClient(handler)
                Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=count, progress:=progress)
                Check(snapshot.RequestedMessageCount = count AndAlso snapshot.FetchedMessageCount = count AndAlso snapshot.ImportedMessageCount = count AndAlso snapshot.Messages.Count = count, "Selected history count was not honored")
                Check(snapshot.PageCount = CInt(Math.Ceiling(count / 100.0)) AndAlso handler.Requests.Count = snapshot.PageCount + 1, "History pagination added missing or extra requests")
                Check(snapshot.Messages.First().Id = Id(1) AndAlso snapshot.Messages.Last().Id = Id(count) AndAlso snapshot.Messages.Select(Function(row) row.Id).Distinct(StringComparer.Ordinal).Count() = count, "History lost chronological order or exact-ID uniqueness")
                Dim pending = count
                For index = 1 To handler.Requests.Count - 1
                    Dim request = handler.Requests(index)
                    Dim take = Math.Min(100, pending)
                    Check(request.Limit = take AndAlso request.Before = If(index = 1, "", Id(pending + 1)), "Final partial page or before cursor was wrong")
                    pending -= take
                Next
                Check(progress.Rows.Count > 0 AndAlso progress.Rows.All(Function(row) row.RequestedMessageCount = count AndAlso row.FetchedMessageCount >= 0 AndAlso row.FetchedMessageCount <= count), "Progress escaped selected count bounds")
                Check(progress.Rows.Last().FetchedMessageCount = count AndAlso progress.Rows.Last().PageCount = snapshot.PageCount, "Final progress did not report completed pages and count")
            End Using
        Next

        Using handler As New QueueHandler({Metadata(), Page(150, 100), Page(50, 17)}), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=250)
            Check(snapshot.Messages.Count = 117 AndAlso snapshot.HistoryExhausted AndAlso handler.Requests.Count = 3, "Short page did not stop at available history")
        End Using
        Using handler As New QueueHandler({Metadata(), Page(100, 100), JsonReply("[]")}), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=101)
            Check(snapshot.Messages.Count = 100 AndAlso snapshot.HistoryExhausted AndAlso handler.Requests.Count = 3, "Empty final page invented old messages or continued requesting")
        End Using
        For Each invalidCount In {-1, 0, 10001, Integer.MaxValue}
            Using handler As New QueueHandler(Array.Empty(Of HttpResponseMessage)()), transport As New HttpClient(handler)
                Dim rejected = False
                Try
                    Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=invalidCount)
                Catch ex As ArgumentException
                    rejected = True
                Catch ex As DiscordTradeException
                    rejected = True
                End Try
                Check(rejected AndAlso handler.Requests.Count = 0, "Out-of-range import count reached HTTP")
            End Using
        Next
    End Function

    Private Async Function TestOverlappingPages() As Task
        Dim secondPage = Posts(150, 99)
        Dim edited = Post(151, "SELL EDITED")
        edited("edited_timestamp") = "2026-10-06T00:00:00Z"
        secondPage.Insert(0, edited)
        Using handler As New QueueHandler({Metadata(), Page(250, 100), JsonReply(JsonSerializer.Serialize(secondPage)), Page(51, 51)}), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=250)
            Check(snapshot.Messages.Count = 250 AndAlso snapshot.FetchedMessageCount = 250 AndAlso snapshot.PageCount = 3, "Overlapping pages counted a duplicate against the selected count")
            Check(snapshot.Messages.Single(Function(row) row.Id = Id(151)).BodyText = "SELL EDITED" AndAlso snapshot.Messages.Single(Function(row) row.Id = Id(151)).AuthorName = "Seller151", "Page overlap lost the latest edit or exact author")
            Check(handler.Requests.Last().Limit = 51 AndAlso handler.Requests.Last().Before = Id(52), "Overlap did not adjust remaining count or descending cursor")
        End Using
        Using handler As New QueueHandler({Metadata(), Page(200, 100), Page(200, 100)}), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 250)
            Check(failure.FailureKind = "invalid-response" AndAlso handler.Requests.Count = 3, "A non-progressing page looped or produced a partial success")
        End Using
        Dim invalid = Posts(100, 99)
        invalid.Insert(0, Post(1001))
        Using handler As New QueueHandler({Metadata(), Page(200, 100), JsonReply(JsonSerializer.Serialize(invalid))}), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 250)
            Check(failure.FailureKind = "invalid-response" AndAlso handler.Requests.Count = 3, "An unknown message newer than before cursor was accepted")
        End Using
    End Function

    Private Async Function TestBoundsAndCancellation() As Task
        Using handler As New QueueHandler({Metadata(), Page(2, 2)}), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 1)
            Check(failure.FailureKind = "invalid-response" AndAlso handler.Requests.Count = 2, "A response longer than requested page size escaped its bound")
        End Using
        Dim tooLarge = JsonReply("[]")
        tooLarge.Content.Headers.ContentLength = 8388609
        Using handler As New QueueHandler({Metadata(), tooLarge}), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 250)
            Check(failure.FailureKind = "invalid-response" AndAlso handler.Requests.Count = 2, "Oversized response body was accepted")
        End Using
        Using cancellation As New CancellationTokenSource(), handler As New QueueHandler({Metadata(), Page(250, 100)}), transport As New HttpClient(handler)
            Dim progress As New RecordingProgress With {.OnReport =
                Sub(row)
                    If row.PageCount = 1 Then cancellation.Cancel()
                End Sub}
            Dim stopped = False
            Try
                Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, cancellation.Token, transport, messageLimit:=250, progress:=progress)
            Catch ex As OperationCanceledException
                stopped = True
            End Try
            Check(stopped AndAlso handler.Requests.Count = 2 AndAlso progress.Rows.Any(Function(row) row.FetchedMessageCount = 100), "Cancellation between pages requested more history or returned partial success")
        End Using
        Using handler As New QueueHandler({Metadata(), Page(250, 100), JsonReply("unavailable", HttpStatusCode.ServiceUnavailable)}), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 250)
            Check(Not failure.ShouldStopPolling AndAlso handler.Requests.Count = 3, "Transient page failure retried without instruction or returned partial success")
        End Using
    End Function

    Private Async Function TestRateLimits() As Task
        Using handler As New QueueHandler({Metadata(), Page(250, 100), RateLimited(), Page(150, 100), Page(50, 50)}), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=250)
            Check(snapshot.Messages.Count = 250 AndAlso snapshot.PageCount = 3 AndAlso handler.Requests.Count = 5, "Instructed rate retry did not complete the selected history")
            Check(handler.Requests(2).Before = Id(151) AndAlso handler.Requests(3).Before = Id(151) AndAlso handler.Requests(2).Limit = handler.Requests(3).Limit, "Rate retry advanced its cursor before a page succeeded")
        End Using
        Dim replies As New List(Of HttpResponseMessage) From {Metadata()}
        For attempt = 1 To 6
            replies.Add(RateLimited())
        Next
        Using handler As New QueueHandler(replies), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 250)
            Check(failure.FailureKind = "rate-limited" AndAlso Not failure.ShouldStopPolling AndAlso handler.Requests.Count = 7, "Repeated429 did not stop after five instructed retries")
            Check(failure.RetryAfterSeconds.HasValue AndAlso failure.RetryAfterSeconds.Value = 0.001, "Retry ceiling lost Discord's fractional delay")
        End Using
        Dim totalBudgetReplies As New List(Of HttpResponseMessage) From {Metadata()}
        For remaining = 10000 To 100 Step -100
            totalBudgetReplies.Add(RateLimited())
            totalBudgetReplies.Add(Page(remaining, 100))
        Next
        Using handler As New QueueHandler(totalBudgetReplies), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport, 10000)
            Check(failure.FailureKind = "rate-limited" AndAlso Not failure.ShouldStopPolling AndAlso handler.Requests.Count = 122, "Repeated per-page delays escaped the overall HTTP attempt bound")
        End Using
    End Function

    Private Async Function TestMaximumHistory() As Task
        Dim replies As New List(Of HttpResponseMessage) From {Metadata()}
        For remaining = 10000 To 100 Step -100
            replies.Add(Page(remaining, 100))
        Next
        Dim progress As New RecordingProgress()
        Using handler As New QueueHandler(replies), transport As New HttpClient(handler)
            ' Omit messageLimit deliberately: old profiles and first use must default to10000.
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, progress:=progress)
            Check(snapshot.RequestedMessageCount = 10000 AndAlso snapshot.Messages.Count = 10000 AndAlso snapshot.FetchedMessageCount = 10000 AndAlso snapshot.ImportedMessageCount = 10000, "Default history did not retain10000 posts")
            Check(snapshot.PageCount = 100 AndAlso handler.Requests.Count = 101 AndAlso handler.Requests.Skip(1).All(Function(request) request.Limit = 100), "Maximum history exceeded or skipped100 bounded pages")
            Check(snapshot.Messages.First().Id = Id(1) AndAlso snapshot.Messages.Last().Id = Id(10000) AndAlso snapshot.ImportedText.Length > 200000 AndAlso Not snapshot.TextWasLimited, "Maximum history was out of order or truncated at AI limit")
            Check(progress.Rows.Last().FetchedMessageCount = 10000 AndAlso progress.Rows.Last().PageCount = 100, "Maximum history progress stopped early")
        End Using
    End Function

    Private Sub TestRecentMerge()
        Dim previous = Snapshot(Enumerable.Range(1, 250).Select(Function(index) CreateRow(index)).ToList(), 250, False)
        Dim recentRows = Enumerable.Range(152, 100).Select(Function(index) CreateRow(index)).ToList()
        recentRows.RemoveAll(Function(row) row.Id = Id(220))
        recentRows.Add(CreateRow(252))
        recentRows.Single(Function(row) row.Id = Id(200)).BodyText = "SELL REVISED"
        recentRows.Single(Function(row) row.Id = Id(200)).EditedTimestamp = DateTimeOffset.Parse("2026-10-06T00:00:00Z", CultureInfo.InvariantCulture)
        Dim recent = Snapshot(recentRows, 100, False)
        recent.PageCount = 1
        Dim merged = DiscordTradeService.MergeRecentSnapshot(previous, recent, 250)
        Check(merged.Messages.Count = 250 AndAlso merged.RequestedMessageCount = 250 AndAlso merged.FetchedMessageCount = 250 AndAlso merged.PageCount = 1, "Recent refresh lost cache count or network-page metadata")
        Check(merged.Messages.Any(Function(row) row.Id = Id(2)) AndAlso Not merged.Messages.Any(Function(row) row.Id = Id(1)) AndAlso merged.Messages.Last().Id = Id(252), "Recent merge lost older history or failed selected-count trim")
        Check(Not merged.Messages.Any(Function(row) row.Id = Id(220)) AndAlso merged.Messages.Single(Function(row) row.Id = Id(200)).BodyText = "SELL REVISED", "Recent merge retained a deleted recent post or stale edit")
        Check(merged.Messages.Select(Function(row) row.Id).Distinct(StringComparer.Ordinal).Count() = 250 AndAlso merged.Messages.Select(Function(row) ULong.Parse(row.Id, CultureInfo.InvariantCulture)).SequenceEqual(merged.Messages.Select(Function(row) ULong.Parse(row.Id, CultureInfo.InvariantCulture)).OrderBy(Function(idValue) idValue)), "Merged history duplicated IDs or changed chronological order")
        Dim completeRecent = Snapshot(New List(Of DiscordTradeMessage) From {CreateRow(400), CreateRow(401)}, 100, True)
        Dim complete = DiscordTradeService.MergeRecentSnapshot(previous, completeRecent, 250)
        Check(complete.Messages.Count = 2 AndAlso complete.HistoryExhausted AndAlso complete.Messages.First().Id = Id(400), "Complete short refresh retained obsolete cached history")
        Dim small = DiscordTradeService.MergeRecentSnapshot(previous, recent, 10)
        Check(small.Messages.Count = 10 AndAlso small.Messages.Last().Id = Id(252), "Changed selected count did not trim merged history")
        Dim mismatch = Snapshot(New List(Of DiscordTradeMessage) From {CreateRow(1)}, 100, True)
        mismatch.ChannelId = "1556796091855278000"
        Dim rejected = False
        Try
            DiscordTradeService.MergeRecentSnapshot(previous, mismatch, 250)
        Catch ex As DiscordTradeException
            rejected = True
        Catch ex As ArgumentException
            rejected = True
        End Try
        Check(rejected, "Recent merge crossed channel identity")
    End Sub

    Private Function Snapshot(rows As List(Of DiscordTradeMessage), requested As Integer, exhausted As Boolean) As DiscordTradeSnapshot
        Return New DiscordTradeSnapshot With {.GuildId = GuildId, .ChannelId = ChannelId, .ChannelLink = ChannelUrl, .ChannelName = "trade-history", .Messages = rows,
            .RequestedMessageCount = requested, .FetchedMessageCount = rows.Count, .HistoryExhausted = exhausted, .PageCount = 1}
    End Function
    Private Function CreateRow(index As Integer) As DiscordTradeMessage
        Return New DiscordTradeMessage With {.Id = Id(index), .ChannelId = ChannelId, .AuthorId = "1556800000000000990", .AuthorName = "Seller" & index,
            .BodyText = "SELL YY3", .Timestamp = DateTimeOffset.Parse("2026-10-05T12:00:00Z", CultureInfo.InvariantCulture).AddSeconds(index)}
    End Function
    Private Function Id(index As Integer) As String
        Return (BaseId + CULng(index)).ToString(CultureInfo.InvariantCulture)
    End Function
    Private Function Post(index As Integer, Optional body As String = "SELL YY3") As Dictionary(Of String, Object)
        Dim row = CreateRow(index)
        Return New Dictionary(Of String, Object) From {{"id", row.Id}, {"channel_id", ChannelId}, {"author", New With {.id = row.AuthorId, .username = row.AuthorName}},
            {"timestamp", row.Timestamp.ToString("O", CultureInfo.InvariantCulture)}, {"content", body}, {"embeds", Array.Empty(Of Object)()}}
    End Function
    Private Function Posts(newest As Integer, count As Integer) As List(Of Dictionary(Of String, Object))
        Return Enumerable.Range(newest - count + 1, count).Reverse().Select(Function(index) Post(index)).ToList()
    End Function
    Private Function Page(newest As Integer, count As Integer) As HttpResponseMessage
        Return JsonReply(JsonSerializer.Serialize(Posts(newest, count)))
    End Function
    Private Function Metadata() As HttpResponseMessage
        Return JsonReply(JsonSerializer.Serialize(New With {.id = ChannelId, .guild_id = GuildId, .name = "trade-history", .type = 0}))
    End Function
    Private Function RateLimited() As HttpResponseMessage
        Return JsonReply("{""retry_after"":0.001,""global"":true}", HttpStatusCode.TooManyRequests)
    End Function
    Private Function JsonReply(body As String, Optional status As HttpStatusCode = HttpStatusCode.OK) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function
    Private Async Function ExpectFailure(transport As HttpClient, selectedCount As Integer) As Task(Of DiscordTradeException)
        Try
            Await DiscordTradeService.FetchLatestAsync(ChannelUrl, Credential, CancellationToken.None, transport, messageLimit:=selectedCount)
        Catch ex As DiscordTradeException
            Return ex
        End Try
        Throw New Exception("Expected a bounded offline history failure")
    End Function
    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New Exception(reason)
    End Sub

    Private Class RecordingProgress
        Implements IProgress(Of DiscordTradeImportProgress)
        Public ReadOnly Rows As New List(Of DiscordTradeImportProgress)()
        Public Property OnReport As Action(Of DiscordTradeImportProgress)
        Public Sub Report(value As DiscordTradeImportProgress) Implements IProgress(Of DiscordTradeImportProgress).Report
            Rows.Add(New DiscordTradeImportProgress With {.FetchedMessageCount = value.FetchedMessageCount, .RequestedMessageCount = value.RequestedMessageCount, .PageCount = value.PageCount})
            OnReport?.Invoke(value)
        End Sub
    End Class
    Private Class Request
        Public Property Limit As Integer
        Public Property Before As String = ""
    End Class
    Private Class QueueHandler
        Inherits HttpMessageHandler
        Private ReadOnly replies As Queue(Of HttpResponseMessage)
        Public ReadOnly Requests As New List(Of Request)()
        Public Sub New(responses As IEnumerable(Of HttpResponseMessage))
            replies = New Queue(Of HttpResponseMessage)(responses)
        End Sub
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            cancellation.ThrowIfCancellationRequested()
            Check(request.Method = HttpMethod.Get AndAlso request.RequestUri.Host = "discord.com" AndAlso request.Headers.Authorization.Scheme = "Bot" AndAlso request.Headers.Authorization.Parameter = Credential, "History request changed host, read-only method or bot authentication")
            Dim observation As New Request()
            For Each part In request.RequestUri.Query.TrimStart("?"c).Split("&"c)
                Dim pair = part.Split("="c, 2)
                If pair.Length <> 2 Then Continue For
                If pair(0) = "limit" Then observation.Limit = Integer.Parse(pair(1), CultureInfo.InvariantCulture)
                If pair(0) = "before" Then observation.Before = pair(1)
            Next
            Requests.Add(observation)
            If replies.Count = 0 Then Throw New Exception("History importer requested unexpected extra HTTP response")
            Return Task.FromResult(replies.Dequeue())
        End Function
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                While replies.Count > 0
                    replies.Dequeue().Dispose()
                End While
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Module
