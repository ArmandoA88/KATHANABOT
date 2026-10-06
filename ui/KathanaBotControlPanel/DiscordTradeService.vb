Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Public Class DiscordTradeChannelTarget
    Public Property GuildId As String = ""
    Public Property ChannelId As String = ""
    Public Property ChannelLink As String = ""
End Class

Public Class DiscordTradeMessage
    Public Property Id As String = ""
    Public Property ChannelId As String = ""
    Public Property AuthorName As String = ""
    Public Property AuthorId As String = ""
    Public Property Timestamp As DateTimeOffset
    Public Property EditedTimestamp As DateTimeOffset?
    Public Property MessageType As Integer
    Public Property BodyText As String = ""
    Public Property IsCrosspost As Boolean
    Public Property SourceGuildId As String = ""
    Public Property SourceChannelId As String = ""
    Public Property SourceMessageId As String = ""
End Class

Public Class DiscordTradeSnapshot
    Public Property GuildId As String = ""
    Public Property ChannelId As String = ""
    Public Property ChannelName As String = ""
    Public Property ChannelType As Integer
    Public Property ChannelLink As String = ""
    Public Property Messages As New List(Of DiscordTradeMessage)()
    Public Property ImportedText As String = ""
    Public Property FetchedMessageCount As Integer
    Public Property ImportedMessageCount As Integer
    Public Property RequestedMessageCount As Integer
    Public Property PageCount As Integer
    Public Property HistoryExhausted As Boolean
    Public Property NewestMessageId As String = ""
    Public Property TextWasLimited As Boolean
    Public Property OmittedMessageCount As Integer
    Public Property SuggestedRefreshDelaySeconds As Double
    Public Property Status As String = ""
End Class

Public Class DiscordTradeImportProgress
    Public Property FetchedMessageCount As Integer
    Public Property RequestedMessageCount As Integer
    Public Property PageCount As Integer
End Class

Public Class DiscordTradeException
    Inherits InvalidOperationException
    Public ReadOnly Property FailureKind As String
    Public ReadOnly Property RetryAfterSeconds As Double?
    Public ReadOnly Property ShouldStopPolling As Boolean

    Public Sub New(kind As String, message As String, stopPolling As Boolean, Optional retryAfter As Double? = Nothing)
        MyBase.New(message)
        FailureKind = kind
        ShouldStopPolling = stopPolling
        RetryAfterSeconds = retryAfter
    End Sub
End Class

' Reads a bounded recent-history snapshot. It never sends Discord messages, reads a
' personal-account token, downloads attachments, or invokes AI/game input.
Public NotInheritable Class DiscordTradeService
    Public Const LatestMessageLimit As Integer = 100
    Public Const DefaultImportMessageCount As Integer = 10000
    Public Const MaximumImportMessageCount As Integer = 10000
    Public Const MaximumImportedCharacters As Integer = 100000000
    Public Const MaximumBrowserCaptureBytes As Integer = 67108864
    Private Const MaximumMessagePages As Integer = 101
    Private Const MaximumHttpAttempts As Integer = 122
    Private Const MaximumRetriesPerRequest As Integer = 5
    Private Const MaximumImportSeconds As Integer = 900
    Private Const MaximumMetadataBytes As Integer = 131072
    Private Const MaximumMessageBytes As Integer = 8388608
    Private Shared ReadOnly Client As New HttpClient(New HttpClientHandler With {.AllowAutoRedirect = False}) With {.Timeout = Timeout.InfiniteTimeSpan}

    Private Sub New()
    End Sub

    Public Shared Function ParseChannel(channelLinkOrId As String) As DiscordTradeChannelTarget
        Dim value = If(channelLinkOrId, "").Trim()
        If value.Length = 0 OrElse value.Length > 256 Then Throw InvalidChannel()
        If IsSnowflake(value) Then Return New DiscordTradeChannelTarget With {.ChannelId = value}
        Dim address As Uri = Nothing
        If Not Uri.TryCreate(value, UriKind.Absolute, address) OrElse address.Scheme <> Uri.UriSchemeHttps OrElse
            Not String.Equals(address.Host, "discord.com", StringComparison.OrdinalIgnoreCase) OrElse Not address.IsDefaultPort OrElse
            address.UserInfo.Length <> 0 OrElse address.Query.Length <> 0 OrElse address.Fragment.Length <> 0 OrElse value.Contains("%") Then Throw InvalidChannel()
        Dim match = Regex.Match(address.AbsolutePath, "\A/channels/(?<guild>[1-9][0-9]{0,19})/(?<channel>[1-9][0-9]{0,19})/?\z")
        If Not match.Success OrElse Not IsSnowflake(match.Groups("guild").Value) OrElse Not IsSnowflake(match.Groups("channel").Value) Then Throw InvalidChannel()
        Dim guildId = match.Groups("guild").Value
        Dim channelId = match.Groups("channel").Value
        Return New DiscordTradeChannelTarget With {.GuildId = guildId, .ChannelId = channelId, .ChannelLink = ChannelLink(guildId, channelId)}
    End Function

    Public Shared Async Function FetchLatestAsync(channelLinkOrId As String, botToken As String, cancellation As CancellationToken,
                                                  Optional transport As HttpClient = Nothing,
                                                  Optional messageLimit As Integer = DefaultImportMessageCount,
                                                  Optional progress As IProgress(Of DiscordTradeImportProgress) = Nothing) As Task(Of DiscordTradeSnapshot)
        cancellation.ThrowIfCancellationRequested()
        ValidateMessageLimit(messageLimit)
        Dim target = ParseChannel(channelLinkOrId)
        Dim secret = NormalizeBotToken(botToken)
        Dim requestBudget As New DiscordRequestBudget()
        Using deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation)
            deadline.CancelAfter(TimeSpan.FromSeconds(MaximumImportSeconds))
            Try
                ReportProgress(progress, 0, messageLimit, 0)
                Dim guildId As String, name As String, kind As Integer, refreshDelay As Double
                Using metadata = Await GetJsonWithRetryAsync("channels/" & target.ChannelId, secret, MaximumMetadataBytes, If(transport, Client), requestBudget, deadline.Token).ConfigureAwait(False)
                    Dim channel = metadata.Document.RootElement
                    If channel.ValueKind <> JsonValueKind.Object OrElse RequiredSnowflake(channel, "id") <> target.ChannelId Then Throw InvalidResponse()
                    guildId = RequiredSnowflake(channel, "guild_id")
                    If target.GuildId.Length > 0 AndAlso target.GuildId <> guildId Then Throw New DiscordTradeException("invalid-channel", "The channel does not belong to the server in the saved Discord link.", True)
                    kind = RequiredInteger(channel, "type")
                    If Not {0, 5, 10, 11, 12}.Contains(kind) Then
                        Throw New DiscordTradeException("invalid-channel", "Choose a text, announcement, or individual thread channel. Forum/media boards require a specific post's thread link.", True)
                    End If
                    name = OptionalString(channel, "name")
                    refreshDelay = metadata.RefreshDelaySeconds
                End Using
                Dim unique As New Dictionary(Of String, DiscordTradeMessage)(StringComparer.Ordinal)
                Dim before As ULong, pages As Integer, exhausted As Boolean
                Do While unique.Count < messageLimit
                    If pages >= MaximumMessagePages Then Throw New DiscordTradeException("invalid-response", "Discord history did not advance within the bounded page limit. No imported posts were changed.", True)
                    Await WaitForRateLimitAsync(refreshDelay, deadline.Token).ConfigureAwait(False)
                    Dim pageSize = Math.Min(LatestMessageLimit, messageLimit - unique.Count)
                    Dim route = "channels/" & target.ChannelId & "/messages?limit=" & pageSize.ToString(CultureInfo.InvariantCulture)
                    If before > 0 Then route &= "&before=" & before.ToString(CultureInfo.InvariantCulture)
                    Using messages = Await GetJsonWithRetryAsync(route, secret, MaximumMessageBytes, If(transport, Client), requestBudget, deadline.Token).ConfigureAwait(False)
                        Dim root = messages.Document.RootElement
                        If root.ValueKind <> JsonValueKind.Array OrElse root.GetArrayLength() > pageSize Then Throw InvalidResponse()
                        Dim oldest As ULong = ULong.MaxValue
                        Dim newMessages As Integer = 0
                        For Each entry In root.EnumerateArray()
                            Dim post = ParseMessage(entry, target.ChannelId)
                            Dim numericId = ULong.Parse(post.Id, CultureInfo.InvariantCulture)
                            Dim prior As DiscordTradeMessage = Nothing
                            Dim known = unique.TryGetValue(post.Id, prior)
                            ' Overlap can update an already-seen message, but an
                            ' unknown message must be strictly before the cursor.
                            If before > 0 AndAlso numericId >= before AndAlso Not known Then Throw InvalidResponse()
                            If Not known Then
                                unique.Add(post.Id, post)
                                newMessages += 1
                            ElseIf post.EditedTimestamp.GetValueOrDefault(post.Timestamp) > prior.EditedTimestamp.GetValueOrDefault(prior.Timestamp) Then
                                unique(post.Id) = post
                            End If
                            oldest = Math.Min(oldest, numericId)
                        Next
                        If root.GetArrayLength() > 0 AndAlso (newMessages = 0 OrElse (before > 0 AndAlso oldest >= before)) Then Throw New DiscordTradeException("invalid-response", "Discord history pagination did not advance. No imported posts were changed.", True)
                        pages += 1
                        exhausted = root.GetArrayLength() < pageSize
                        refreshDelay = messages.RefreshDelaySeconds
                        ReportProgress(progress, unique.Count, messageLimit, pages)
                        If exhausted OrElse unique.Count >= messageLimit Then Exit Do
                        before = oldest
                    End Using
                Loop
                Dim result = BuildSnapshot(unique.Values, guildId, target.ChannelId, name, kind, messageLimit, pages, exhausted)
                result.SuggestedRefreshDelaySeconds = refreshDelay
                deadline.Token.ThrowIfCancellationRequested()
                Return result
            Catch ex As OperationCanceledException When Not cancellation.IsCancellationRequested
                Throw New DiscordTradeException("timeout", "Discord history reading exceeded its 15-minute limit. No imported posts were changed.", False)
            Catch ex As HttpRequestException
                Throw New DiscordTradeException("network-error", "Could not reach Discord. Check the connection and try again later.", False)
            Catch ex As IOException
                Throw New DiscordTradeException("network-error", "Discord reading was interrupted. Check the connection and try again later.", False)
            Catch ex As JsonException
                Throw InvalidResponse()
            End Try
        End Using
    End Function

    ' This input comes from the user's explicitly started browser collector. It
    ' contains message bodies only, never a Discord login or request headers.
    Public Shared Function ParseBrowserCaptureJson(sourceChannelUrl As String, json As String, captureId As String,
                                                   messageLimit As Integer) As DiscordTradeSnapshot
        ValidateMessageLimit(messageLimit)
        Dim target = ParseChannel(sourceChannelUrl)
        If target.GuildId.Length = 0 OrElse Not Regex.IsMatch(If(captureId, ""), "\A[0-9A-Fa-f]{64}\z") OrElse
            json Is Nothing OrElse json.Length > MaximumBrowserCaptureBytes OrElse Encoding.UTF8.GetByteCount(json) > MaximumBrowserCaptureBytes Then Throw InvalidBrowserCapture()
        Try
            Using document = JsonDocument.Parse(json, New JsonDocumentOptions With {.MaxDepth = 32})
                Dim root = document.RootElement
                If root.ValueKind <> JsonValueKind.Object OrElse OptionalString(root, "format") <> "KathanaTradeCapture" OrElse
                    RequiredInteger(root, "version") <> 1 OrElse OptionalString(root, "captureId") <> captureId OrElse
                    RequiredSnowflake(root, "sourceGuildId") <> target.GuildId OrElse RequiredSnowflake(root, "sourceChannelId") <> target.ChannelId OrElse
                    RequiredInteger(root, "requestedMessageCount") <> messageLimit Then Throw InvalidBrowserCapture()
                Dim exhausted As JsonElement, entries As JsonElement
                If Not root.TryGetProperty("historyExhausted", exhausted) OrElse Not {JsonValueKind.True, JsonValueKind.False}.Contains(exhausted.ValueKind) OrElse
                    Not root.TryGetProperty("messages", entries) OrElse entries.ValueKind <> JsonValueKind.Array OrElse
                    entries.GetArrayLength() > messageLimit Then Throw InvalidBrowserCapture()
                Dim unique As New Dictionary(Of String, DiscordTradeMessage)(StringComparer.Ordinal)
                For Each entry In entries.EnumerateArray()
                    Dim post = ParseMessage(entry, target.ChannelId)
                    If String.IsNullOrWhiteSpace(post.AuthorName) Then Throw InvalidBrowserCapture()
                    Dim prior As DiscordTradeMessage = Nothing
                    If Not unique.TryGetValue(post.Id, prior) OrElse post.EditedTimestamp.GetValueOrDefault(post.Timestamp) > prior.EditedTimestamp.GetValueOrDefault(prior.Timestamp) Then unique(post.Id) = post
                Next
                Dim result = BuildSnapshot(unique.Values, target.GuildId, target.ChannelId, "Discord browser channel", 0, messageLimit, 0, exhausted.GetBoolean())
                If result.ImportedMessageCount = 0 Then Throw New DiscordTradeException("capture-empty", "The browser capture contained no readable posts. Existing posts are kept.", True)
                result.Status = $"Browser capture imported {result.ImportedMessageCount:N0} readable post(s) from {result.FetchedMessageCount:N0} captured message(s), requested {messageLimit:N0}." &
                    If(result.FetchedMessageCount < messageLimit, If(result.HistoryExhausted, " Discord returned the end of the available history.", " The browser stopped before the requested count; this is a partial capture."), "") &
                    " Review character names before sending whispers."
                Return result
            End Using
        Catch ex As JsonException
            Throw InvalidBrowserCapture()
        Catch ex As DiscordTradeException When ex.FailureKind <> "capture-empty"
            Throw InvalidBrowserCapture()
        End Try
    End Function

    Private Shared Function InvalidBrowserCapture() As DiscordTradeException
        Return New DiscordTradeException("invalid-browser-capture", "The browser capture did not match this channel, session, or message count, or contained invalid message data. Existing posts are kept.", True)
    End Function

    Public Shared Function MergeRecentSnapshot(previous As DiscordTradeSnapshot, recent As DiscordTradeSnapshot, messageLimit As Integer) As DiscordTradeSnapshot
        ValidateMessageLimit(messageLimit)
        If previous Is Nothing OrElse recent Is Nothing OrElse previous.GuildId <> recent.GuildId OrElse previous.ChannelId <> recent.ChannelId OrElse
            Not IsSnowflake(recent.GuildId) OrElse Not IsSnowflake(recent.ChannelId) Then Throw InvalidResponse()
        Dim combined As New Dictionary(Of String, DiscordTradeMessage)(StringComparer.Ordinal)
        Dim fresh = ValidateCachedMessages(recent.Messages, recent.ChannelId)
        If Not recent.HistoryExhausted Then
            Dim oldest = If(fresh.Count = 0, ULong.MaxValue, fresh.Values.Min(Function(post) ULong.Parse(post.Id, CultureInfo.InvariantCulture)))
            For Each post In ValidateCachedMessages(previous.Messages, previous.ChannelId).Values
                If ULong.Parse(post.Id, CultureInfo.InvariantCulture) < oldest Then combined(post.Id) = post
            Next
        End If
        For Each post In fresh.Values
            combined(post.Id) = post ' The current snapshot wins, including edits.
        Next
        Dim exhausted = (recent.HistoryExhausted OrElse previous.HistoryExhausted) AndAlso combined.Count <= messageLimit
        Dim result = BuildSnapshot(combined.Values, recent.GuildId, recent.ChannelId, recent.ChannelName, recent.ChannelType,
                                   messageLimit, recent.PageCount, exhausted)
        result.SuggestedRefreshDelaySeconds = recent.SuggestedRefreshDelaySeconds
        Return result
    End Function

    Private Shared Function ValidateCachedMessages(messages As List(Of DiscordTradeMessage), channelId As String) As Dictionary(Of String, DiscordTradeMessage)
        If messages Is Nothing OrElse messages.Count > MaximumImportMessageCount Then Throw InvalidResponse()
        Dim unique As New Dictionary(Of String, DiscordTradeMessage)(StringComparer.Ordinal)
        For Each post In messages
            If post Is Nothing OrElse Not IsSnowflake(post.Id) OrElse post.ChannelId <> channelId Then Throw InvalidResponse()
            unique(post.Id) = post
        Next
        Return unique
    End Function

    Private Shared Sub ValidateMessageLimit(messageLimit As Integer)
        If messageLimit < 1 OrElse messageLimit > MaximumImportMessageCount Then Throw New DiscordTradeException("invalid-count", "Choose between 1 and 10,000 Discord messages to import.", True)
    End Sub

    Private Shared Sub ReportProgress(progress As IProgress(Of DiscordTradeImportProgress), fetched As Integer, requested As Integer, pages As Integer)
        progress?.Report(New DiscordTradeImportProgress With {.FetchedMessageCount = fetched, .RequestedMessageCount = requested, .PageCount = pages})
    End Sub

    Private Shared Async Function WaitForRateLimitAsync(seconds As Double, cancellation As CancellationToken) As Task
        cancellation.ThrowIfCancellationRequested()
        If seconds <= 0 Then Return
        If seconds >= MaximumImportSeconds Then Throw New DiscordTradeException("rate-limited", "Discord requested a pause longer than this bounded import. Try again after the pause.", False, seconds)
        Await Task.Delay(TimeSpan.FromSeconds(seconds), cancellation).ConfigureAwait(False)
    End Function

    Private Shared Async Function GetJsonWithRetryAsync(route As String, token As String, maximumBytes As Integer, transport As HttpClient,
                                                       budget As DiscordRequestBudget, cancellation As CancellationToken) As Task(Of DiscordResponse)
        Dim retries As Integer
        Do
            cancellation.ThrowIfCancellationRequested()
            budget.Consume()
            Dim rateLimit As DiscordTradeException = Nothing
            Try
                Using requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation)
                    requestDeadline.CancelAfter(TimeSpan.FromSeconds(25))
                    Try
                        Return Await GetJsonAsync(route, token, maximumBytes, transport, requestDeadline.Token).ConfigureAwait(False)
                    Catch ex As OperationCanceledException When Not cancellation.IsCancellationRequested
                        Throw New DiscordTradeException("timeout", "A Discord reading request timed out. No imported posts were changed.", False)
                    End Try
                End Using
            Catch ex As DiscordTradeException When ex.FailureKind = "rate-limited"
                rateLimit = ex
            End Try
            If Not rateLimit.RetryAfterSeconds.HasValue OrElse retries >= MaximumRetriesPerRequest Then Throw rateLimit
            retries += 1
            Await WaitForRateLimitAsync(rateLimit.RetryAfterSeconds.Value, cancellation).ConfigureAwait(False)
        Loop
    End Function

    Private Shared Async Function GetJsonAsync(route As String, token As String, maximumBytes As Integer, transport As HttpClient, cancellation As CancellationToken) As Task(Of DiscordResponse)
        Using request As New HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/" & route)
            request.Headers.Authorization = New AuthenticationHeaderValue("Bot", token)
            request.Headers.UserAgent.ParseAdd("DiscordBot (https://github.com/discord/discord-api-docs, 1.0) KathanaTradeReader/1.0")
            request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
            Using response = Await transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(False)
                Dim code = CInt(response.StatusCode)
                Select Case code
                    Case 401
                        Throw New DiscordTradeException("unauthorized", "Discord rejected the bot token. Configure the reader bot token again.", True)
                    Case 403
                        Throw New DiscordTradeException("forbidden", "The reader bot needs View Channel and Read Message History in the receiving channel.", True)
                    Case 404
                        Throw New DiscordTradeException("channel-unavailable", "The Discord channel is unavailable to this bot. Check the receiving link and its channel permissions.", True)
                    Case 429
                        Dim body = Await ReadBoundedAsync(response.Content, 65536, cancellation).ConfigureAwait(False)
                        Throw New DiscordTradeException("rate-limited", "Discord rate limited the reader. Waiting before the next refresh.", False, RetryDelay(response, body))
                End Select
                If code < 200 OrElse code >= 300 Then Throw New DiscordTradeException("http-error", $"Discord reading failed (HTTP {code}). No imported posts were changed.", code >= 300 AndAlso code < 500)
                Dim bytes = Await ReadBoundedAsync(response.Content, maximumBytes, cancellation).ConfigureAwait(False)
                Dim document = JsonDocument.Parse(bytes, New JsonDocumentOptions With {.MaxDepth = 32})
                Return New DiscordResponse With {.Document = document, .RefreshDelaySeconds = BucketResetDelay(response)}
            End Using
        End Using
    End Function

    Private Shared Async Function ReadBoundedAsync(content As HttpContent, maximumBytes As Integer, cancellation As CancellationToken) As Task(Of Byte())
        If content.Headers.ContentLength.HasValue AndAlso content.Headers.ContentLength.Value > maximumBytes Then Throw New DiscordTradeException("invalid-response", "Discord returned too much data. No imported posts were changed.", True)
        Using source = Await content.ReadAsStreamAsync(cancellation).ConfigureAwait(False), destination As New MemoryStream()
            Dim buffer(8191) As Byte
            Do
                Dim count = Await source.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(False)
                If count = 0 Then Exit Do
                If destination.Length + count > maximumBytes Then Throw New DiscordTradeException("invalid-response", "Discord returned too much data. No imported posts were changed.", True)
                destination.Write(buffer, 0, count)
            Loop
            Return destination.ToArray()
        End Using
    End Function

    Private Shared Function BuildSnapshot(posts As IEnumerable(Of DiscordTradeMessage), guildId As String, channelId As String, name As String, kind As Integer,
                                         messageLimit As Integer, pages As Integer, exhausted As Boolean) As DiscordTradeSnapshot
        Dim recent = posts.OrderByDescending(Function(post) ULong.Parse(post.Id, CultureInfo.InvariantCulture)).Take(messageLimit).ToList()
        Dim result As New DiscordTradeSnapshot With {.GuildId = guildId, .ChannelId = channelId, .ChannelName = name, .ChannelType = kind,
            .ChannelLink = ChannelLink(guildId, channelId), .FetchedMessageCount = recent.Count,
            .RequestedMessageCount = messageLimit, .PageCount = pages, .HistoryExhausted = exhausted,
            .NewestMessageId = If(recent.Count = 0, "", recent(0).Id), .Messages = recent.AsEnumerable().Reverse().ToList()}
        Dim ordinary = recent.Where(Function(post) IsPostType(post.MessageType)).ToList()
        Dim readable = ordinary.Where(Function(post) Not String.IsNullOrWhiteSpace(post.BodyText)).ToList()
        If ordinary.Count > 0 AndAlso readable.Count = 0 Then
            Throw New DiscordTradeException("no-readable-content", "Discord returned posts without readable content. Enable Message Content access for the reader bot, or check whether the posts only contain images.", True)
        End If
        Dim blocks As New List(Of String)()
        Dim used As Integer
        For Each post In readable ' Newest complete posts get priority under the text limit.
            Dim block = FormatPost(post)
            Dim separator = If(blocks.Count = 0, 0, 2)
            If block.Length > MaximumImportedCharacters OrElse used + separator + block.Length > MaximumImportedCharacters Then
                result.TextWasLimited = True
                result.OmittedMessageCount += 1
                Continue For
            End If
            blocks.Add(block)
            used += separator + block.Length
        Next
        blocks.Reverse()
        result.ImportedText = String.Join(vbLf & vbLf, blocks)
        result.ImportedMessageCount = blocks.Count
        Dim counts = $"Fetched {result.FetchedMessageCount:N0} of up to {messageLimit:N0} message(s); imported {blocks.Count:N0} readable post(s) from {name}."
        result.Status = counts & If(blocks.Count = 0, " No published posts with readable text were returned. Check published announcements and the reader bot's channel/history permissions.", "") &
            If(result.HistoryExhausted AndAlso result.FetchedMessageCount < messageLimit, " Reached the end of the available channel history.", "") &
            If(result.TextWasLimited, $" {result.OmittedMessageCount:N0} older/oversize post(s) omitted at the {MaximumImportedCharacters:N0}-character limit.", "")
        Return result
    End Function

    Private Shared Function ParseMessage(entry As JsonElement, channelId As String) As DiscordTradeMessage
        If entry.ValueKind <> JsonValueKind.Object Then Throw InvalidResponse()
        Dim author As JsonElement
        If Not entry.TryGetProperty("author", author) OrElse author.ValueKind <> JsonValueKind.Object Then Throw InvalidResponse()
        Dim timestamp As DateTimeOffset
        If Not DateTimeOffset.TryParse(OptionalString(entry, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, timestamp) Then Throw InvalidResponse()
        Dim post As New DiscordTradeMessage With {.Id = RequiredSnowflake(entry, "id"), .ChannelId = RequiredSnowflake(entry, "channel_id"),
            .AuthorName = OptionalString(author, "username"), .AuthorId = OptionalSnowflake(author, "id"), .Timestamp = timestamp,
            .MessageType = OptionalInteger(entry, "type", 0), .IsCrosspost = (OptionalInteger(entry, "flags", 0) And 2) <> 0}
        If post.ChannelId <> channelId Then Throw InvalidResponse()
        Dim edited = OptionalString(entry, "edited_timestamp")
        If edited.Length > 0 Then
            Dim parsedEdit As DateTimeOffset
            If Not DateTimeOffset.TryParse(edited, CultureInfo.InvariantCulture, DateTimeStyles.None, parsedEdit) Then Throw InvalidResponse()
            post.EditedTimestamp = parsedEdit
        End If
        Dim reference As JsonElement
        If entry.TryGetProperty("message_reference", reference) AndAlso reference.ValueKind = JsonValueKind.Object Then
            post.SourceGuildId = OptionalSnowflake(reference, "guild_id")
            post.SourceChannelId = OptionalSnowflake(reference, "channel_id")
            post.SourceMessageId = OptionalSnowflake(reference, "message_id")
        End If
        If Not IsPostType(post.MessageType) Then Return post
        Dim parts As New List(Of String)()
        AddText(parts, OptionalString(entry, "content"))
        Dim embeds As JsonElement
        If entry.TryGetProperty("embeds", embeds) AndAlso embeds.ValueKind = JsonValueKind.Array Then
            For Each embed In embeds.EnumerateArray()
                If embed.ValueKind <> JsonValueKind.Object Then Throw InvalidResponse()
                AddText(parts, OptionalString(embed, "title"))
                AddText(parts, OptionalString(embed, "description"))
                Dim embedAuthor As JsonElement
                If embed.TryGetProperty("author", embedAuthor) AndAlso embedAuthor.ValueKind = JsonValueKind.Object Then AddText(parts, OptionalString(embedAuthor, "name"), "Embed author: ")
                Dim fields As JsonElement
                If embed.TryGetProperty("fields", fields) AndAlso fields.ValueKind = JsonValueKind.Array Then
                    For Each field In fields.EnumerateArray()
                        If field.ValueKind <> JsonValueKind.Object Then Throw InvalidResponse()
                        Dim fieldName = OptionalString(field, "name"), fieldValue = OptionalString(field, "value")
                        AddText(parts, If(fieldName.Length > 0, fieldName & ": ", "") & fieldValue)
                    Next
                End If
                Dim footer As JsonElement
                If embed.TryGetProperty("footer", footer) AndAlso footer.ValueKind = JsonValueKind.Object Then AddText(parts, OptionalString(footer, "text"), "Embed footer: ")
            Next
        End If
        Dim attachments As JsonElement
        If entry.TryGetProperty("attachments", attachments) AndAlso attachments.ValueKind = JsonValueKind.Array Then
            For Each attachment In attachments.EnumerateArray()
                If attachment.ValueKind <> JsonValueKind.Object Then Throw InvalidResponse()
                Dim link = OptionalString(attachment, "url"), address As Uri = Nothing
                If Uri.TryCreate(link, UriKind.Absolute, address) AndAlso address.Scheme = Uri.UriSchemeHttps Then AddText(parts, link, "Attachment link (not read): ")
            Next
        End If
        post.BodyText = String.Join(vbLf, parts)
        Return post
    End Function

    Friend Shared Function FormatPost(post As DiscordTradeMessage) As String
        Dim author = Regex.Replace(post.AuthorName, "[\r\n\x00-\x1f]", " ").Trim()
        Dim header = If(post.IsCrosspost, "Followed announcement delivery identity (not a verified game character): " & author, author)
        Return header & vbLf & post.BodyText
    End Function

    Private Shared Sub AddText(parts As List(Of String), text As String, Optional prefix As String = "")
        If Not String.IsNullOrWhiteSpace(text) Then parts.Add(prefix & text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf))
    End Sub

    Private Shared Function IsPostType(kind As Integer) As Boolean
        Return {0, 19, 20, 23}.Contains(kind)
    End Function

    Private Shared Function ChannelLink(guildId As String, channelId As String) As String
        Return "https://discord.com/channels/" & guildId & "/" & channelId
    End Function

    Private Shared Function NormalizeBotToken(token As String) As String
        Dim value = If(token, "").Trim()
        If value.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase) Then value = value.Substring(4).Trim()
        If value.Length = 0 OrElse value.Length > 512 OrElse value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) OrElse
            value.StartsWith("mfa.", StringComparison.OrdinalIgnoreCase) OrElse value.Any(Function(ch) Char.IsWhiteSpace(ch) OrElse Char.IsControl(ch) OrElse AscW(ch) > 127) Then
            Throw New DiscordTradeException("unauthorized", "Configure a reader bot token. Personal-account and OAuth bearer tokens are not accepted here.", True)
        End If
        Return value
    End Function

    Private Shared Function IsSnowflake(value As String) As Boolean
        Dim number As ULong
        Return Regex.IsMatch(If(value, ""), "\A[1-9][0-9]{0,19}\z") AndAlso ULong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, number) AndAlso number > 0
    End Function

    Private Shared Function RequiredSnowflake(parent As JsonElement, name As String) As String
        Dim value = OptionalString(parent, name)
        If Not IsSnowflake(value) Then Throw InvalidResponse()
        Return value
    End Function

    Private Shared Function OptionalSnowflake(parent As JsonElement, name As String) As String
        Dim value = OptionalString(parent, name)
        If value.Length > 0 AndAlso Not IsSnowflake(value) Then Throw InvalidResponse()
        Return value
    End Function

    Private Shared Function OptionalString(parent As JsonElement, name As String) As String
        Dim value As JsonElement
        If Not parent.TryGetProperty(name, value) OrElse value.ValueKind = JsonValueKind.Null Then Return ""
        If value.ValueKind <> JsonValueKind.String Then Throw InvalidResponse()
        Return If(value.GetString(), "")
    End Function

    Private Shared Function RequiredInteger(parent As JsonElement, name As String) As Integer
        Dim value As JsonElement, number As Integer
        If Not parent.TryGetProperty(name, value) OrElse value.ValueKind <> JsonValueKind.Number OrElse Not value.TryGetInt32(number) Then Throw InvalidResponse()
        Return number
    End Function

    Private Shared Function OptionalInteger(parent As JsonElement, name As String, fallback As Integer) As Integer
        Dim value As JsonElement
        If Not parent.TryGetProperty(name, value) OrElse value.ValueKind = JsonValueKind.Null Then Return fallback
        Return RequiredInteger(parent, name)
    End Function

    Private Shared Function BucketResetDelay(response As HttpResponseMessage) As Double
        If FirstHeader(response, "X-RateLimit-Remaining") <> "0" Then Return 0
        Dim seconds As Double
        If TrySeconds(FirstHeader(response, "X-RateLimit-Reset-After"), seconds) Then Return seconds
        Return 0
    End Function

    Private Shared Function RetryDelay(response As HttpResponseMessage, body As Byte()) As Double?
        Dim seconds As Double
        Dim value As Double? = Nothing
        If TrySeconds(FirstHeader(response, "Retry-After"), seconds) Then value = seconds
        Dim retryHeader = response.Headers.RetryAfter
        If Not value.HasValue AndAlso retryHeader IsNot Nothing AndAlso retryHeader.Date.HasValue Then value = Math.Max(0, (retryHeader.Date.Value - DateTimeOffset.UtcNow).TotalSeconds)
        Try
            Using document = JsonDocument.Parse(body)
                Dim delay As JsonElement
                If document.RootElement.ValueKind = JsonValueKind.Object AndAlso document.RootElement.TryGetProperty("retry_after", delay) AndAlso delay.ValueKind = JsonValueKind.Number AndAlso
                    delay.TryGetDouble(seconds) AndAlso Double.IsFinite(seconds) AndAlso seconds >= 0 Then value = Math.Max(value.GetValueOrDefault(), seconds)
            End Using
        Catch ex As JsonException
            ' The valid header still controls backoff when the error body is not JSON.
        End Try
        If Not value.HasValue Then
            seconds = BucketResetDelay(response)
            If seconds > 0 Then value = seconds
        End If
        Return value
    End Function

    Private Shared Function TrySeconds(value As String, ByRef seconds As Double) As Boolean
        Return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, seconds) AndAlso Double.IsFinite(seconds) AndAlso seconds >= 0
    End Function

    Private Shared Function FirstHeader(response As HttpResponseMessage, name As String) As String
        Dim values As IEnumerable(Of String) = Nothing
        Return If(response.Headers.TryGetValues(name, values), values.FirstOrDefault(), "")
    End Function

    Private Shared Function InvalidChannel() As DiscordTradeException
        Return New DiscordTradeException("invalid-channel", "Enter a Discord server channel link (https://discord.com/channels/server/channel) or its numeric channel ID.", True)
    End Function

    Private Shared Function InvalidResponse() As DiscordTradeException
        Return New DiscordTradeException("invalid-response", "Discord returned an unexpected response. No imported posts were changed.", True)
    End Function

    Private NotInheritable Class DiscordRequestBudget
        Private attempts As Integer
        Public Sub Consume()
            If attempts >= MaximumHttpAttempts Then Throw New DiscordTradeException("rate-limited", "Discord reading reached its bounded request limit. No imported posts were changed. Try again later.", False)
            attempts += 1
        End Sub
    End Class

    Private NotInheritable Class DiscordResponse
        Implements IDisposable
        Public Property Document As JsonDocument
        Public Property RefreshDelaySeconds As Double
        Public Sub Dispose() Implements IDisposable.Dispose
            Document?.Dispose()
        End Sub
    End Class
End Class
