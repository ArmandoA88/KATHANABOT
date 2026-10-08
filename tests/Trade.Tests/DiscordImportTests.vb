Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Module DiscordImportTests
    Private Const GuildId As String = "1483326612538654813"
    Private Const ChannelId As String = "1556796091855278112"
    Private Const ChannelUrl As String = "https://discord.com/channels/1483326612538654813/1556796091855278112"
    Private Const SyntheticCredential As String = "offline-import-fixture-credential"
    Private ReadOnly PrivateFields As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private checks As Integer

    Public Async Function RunAsync() As Task
        TestChannelParsing()
        Await TestSnapshotContent()
        Await TestBoundedSnapshots()
        Await TestFailuresAndCancellation()
        Await DiscordPaginationTests.RunAsync()
        TestEncryptedSettings()
        Console.WriteLine($"PASS: {checks} offline Discord import assertions: channel validation, metadata, Follow/embeds, exact names, snapshot bounds, edits, errors, cancellation and encrypted settings.")
    End Function

    Public Sub RunUiTests()
        Dim initialChecks = checks
        Dim previousInput = WindowsInput.Current
        Dim input As New NoInput()
        WindowsInput.Current = input
        Try
            TestUiApplyAndReview()
            TestUiNativeThousandPostRoundTrip()
            TestUiRetiredReaderControls()
            TestUiSavedConfiguration()
            TestUiBatchedAnalysisGuards()
            TestUiSessionSaveAndLoad()
            TestUiShutdown()
            Check(input.Calls = 0, "Importing posts called the game input backend")
        Finally
            WindowsInput.Current = previousInput
        End Try
        Console.WriteLine($"PASS: {checks - initialChecks} offline Trade UI assertions: retired reader controls/Auto, browser-only toolbar, native 1,000-post CRLF/profile roundtrip, reviewed queue preservation, batch progress/commit ownership and inert legacy metadata.")
    End Sub

    Private Sub TestChannelParsing()
        Dim target = DiscordTradeService.ParseChannel(ChannelUrl)
        Check(target.GuildId = GuildId AndAlso target.ChannelId = ChannelId AndAlso target.ChannelLink = ChannelUrl, "Channel URL lost exact snowflake IDs")
        Dim raw = DiscordTradeService.ParseChannel(ChannelId)
        Check(raw.ChannelId = ChannelId, "Raw channel ID lost precision")
        For Each invalid In {"", "0", "18446744073709551616", "١٢٣", "https://discord.com.evil.example/channels/1/2", "http://discord.com/channels/1/2",
                             "https://user@discord.com/channels/1/2", "https://discord.com/channels/@me/2", "https://discord.gg/example",
                             "https://discord.com/api/webhooks/1/not-a-real-secret", ChannelUrl & "/1556800000000000100", ChannelUrl & "?before=1", ChannelUrl & "#fragment"}
            Dim rejected = False
            Try
                DiscordTradeService.ParseChannel(invalid)
            Catch ex As ArgumentException
                rejected = True
            Catch ex As DiscordTradeException
                rejected = True
            End Try
            Check(rejected, "Unsafe or malformed channel target accepted")
        Next
    End Sub

    Private Async Function TestSnapshotContent() As Task
        Dim newest = Message("1556800000000000103", "MiXeD", "S> YY3", "2026-10-05T11:03:00Z")
        newest("webhook_id") = "1556800000000000900"
        newest("flags") = 2
        newest("author") = New With {.id = "1556800000000000800", .username = "MiXeD", .global_name = "A display name", .bot = True}
        newest("message_reference") = New With {.guild_id = "1483326612538654800", .channel_id = "1556796091855278100", .message_id = "1556800000000000003"}
        newest("embeds") = New Object() {New With {
            .author = New With {.name = "MiXeD"}, .title = "S> Asba Ring", .description = "S> ROR YAKSA",
            .fields = New Object() {New With {.name = "Listing", .value = "S> VOUCHER 1.7kk"}}}}
        Dim middle = Message("1556800000000000102", "IIAKISHAII", "B> YY3", "2026-10-05T11:02:00Z")
        Dim oldest = Message("1556800000000000101", "MiXeD", "S> YY3", "2026-10-05T11:01:00Z")
        Using handler As New SequenceHandler(Reply(Metadata()), Reply(JsonSerializer.Serialize({newest, middle, oldest}))), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, "Bot " & SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(handler.Requests.Count = 2 AndAlso handler.Requests(0).Path = $"/api/v10/channels/{ChannelId}" AndAlso
                  handler.Requests(1).Path = $"/api/v10/channels/{ChannelId}/messages", "Import did not validate channel metadata before reading messages")
            Check(handler.Requests.All(Function(request) request.Method = "GET" AndAlso request.Host = "discord.com" AndAlso request.AuthorizationScheme = "Bot" AndAlso request.AuthorizationParameter = SyntheticCredential), "Read-only import auth/host changed")
            Check(handler.Requests(1).Query = "?limit=100", "Snapshot must request latest100 without a history walk")
            Check(snapshot.ChannelId = ChannelId AndAlso snapshot.GuildId = GuildId AndAlso snapshot.ChannelName = "trade-import" AndAlso snapshot.ChannelLink = ChannelUrl, "Channel identity or display metadata lost")
            Check(snapshot.Messages.Select(Function(row) row.Id).SequenceEqual({"1556800000000000101", "1556800000000000102", "1556800000000000103"}), "Snapshot lost chronological order")
            Check(snapshot.Messages.Select(Function(row) row.AuthorName).SequenceEqual({"MiXeD", "IIAKISHAII", "MiXeD"}), "Bot/Follow import changed or filtered exact-case author names")
            Dim followed = snapshot.Messages.Last()
            Check(followed.IsCrosspost AndAlso followed.SourceGuildId = "1483326612538654800" AndAlso followed.SourceChannelId = "1556796091855278100" AndAlso followed.SourceMessageId = "1556800000000000003", "Follow source attribution lost")
            Check({"S> YY3", "S> Asba Ring", "S> ROR YAKSA", "S> VOUCHER 1.7kk"}.All(Function(part) followed.BodyText.Contains(part, StringComparison.Ordinal)), "Content or useful embed text lost")
            Check(snapshot.ImportedMessageCount = 3 AndAlso snapshot.FetchedMessageCount = 3 AndAlso snapshot.NewestMessageId = newest("id").ToString(), "Snapshot count/cursor metadata wrong")
            Check(snapshot.ImportedText.StartsWith("MiXeD" & vbLf & "S> YY3" & vbLf & vbLf & "IIAKISHAII" & vbLf & "B> YY3" & vbLf & vbLf & "Followed announcement delivery identity (not a verified game character): MiXeD" & vbLf, StringComparison.Ordinal) AndAlso
                  Not snapshot.ImportedText.Contains("Discord message:", StringComparison.Ordinal) AndAlso Not snapshot.ImportedText.Contains("Announcement source:", StringComparison.Ordinal) AndAlso
                  Not snapshot.ImportedText.Contains("2026-10-05T", StringComparison.Ordinal), "Rendered source must preserve chronological names/messages without generated dates or links")
            Check(Not snapshot.ImportedText.Contains(SyntheticCredential, StringComparison.Ordinal), "Credential entered imported text")
            Dim queue = TradeService.ParsePosts(snapshot.ImportedText, "YY3", True, TradeService.BuyTemplate)
            Check(queue.Count = 1 AndAlso queue(0).CharacterName = "MiXeD", "Imported source broke exact-case seller parsing or same-author repost dedup")
            Check(TradeService.ParsePosts(snapshot.ImportedText, "ROR", True, TradeService.BuyTemplate).Count = 0, "Follow delivery identity was treated as a verified game character")
        End Using
    End Function

    Private Async Function TestBoundedSnapshots() As Task
        Dim original = Message("1556800000000000201", "PuLgA", "SELL OLD", "2026-10-05T12:00:00Z")
        Dim edited = Message("1556800000000000201", "PuLgA", "SELL NEW", "2026-10-05T12:00:00Z")
        edited("edited_timestamp") = "2026-10-05T12:05:00Z"
        Dim another = Message("1556800000000000202", "Other", "SELL NEW", "2026-10-05T12:01:00Z")
        Using handler As New SequenceHandler(Reply(Metadata(11)), Reply(JsonSerializer.Serialize({another, original, edited}))), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelId, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(snapshot.ChannelType = 11 AndAlso snapshot.ChannelLink = ChannelUrl, "Raw-ID/thread metadata did not resolve its channel link")
            Check(snapshot.Messages.Count = 2 AndAlso snapshot.Messages.First().BodyText.Contains("SELL NEW", StringComparison.Ordinal) AndAlso Not snapshot.ImportedText.Contains("SELL OLD", StringComparison.Ordinal), "Duplicate ID kept stale edited content")
            Check(snapshot.Messages.Last().AuthorName = "Other", "Distinct IDs with identical content were deduplicated")
        End Using

        Dim many As New List(Of Dictionary(Of String, Object))()
        For index = 1 To 105
            many.Add(Message((1556800000000001000UL + CULng(index)).ToString(Globalization.CultureInfo.InvariantCulture), "Seller" & index, "SELL YY3", DateTimeOffset.Parse("2026-10-05T13:00:00Z").AddSeconds(index).ToString("O")))
        Next
        Using handler As New SequenceHandler(Reply(Metadata(5)), Reply(JsonSerializer.Serialize(many))), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(failure.FailureKind = "invalid-response" AndAlso handler.Requests.Count = 2, "Oversized API page was accepted")
        End Using

        Dim large As New List(Of Dictionary(Of String, Object))()
        For index = 1 To 100
            large.Add(Message((1556800000000002000UL + CULng(index)).ToString(Globalization.CultureInfo.InvariantCulture), "BoundSeller" & index, "SELL " & New String("x"c, 3500), DateTimeOffset.Parse("2026-10-05T14:00:00Z").AddSeconds(index).ToString("O")))
        Next
        Using handler As New SequenceHandler(Reply(Metadata()), Reply(JsonSerializer.Serialize(large))), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(snapshot.ImportedText.Length > 200000 AndAlso snapshot.ImportedText.Length <= DiscordTradeService.MaximumImportedCharacters AndAlso Not snapshot.TextWasLimited AndAlso snapshot.ImportedMessageCount = 100, "Full import was truncated at the separate AI analysis limit")
            Check(snapshot.Messages.All(Function(row) row.BodyText.EndsWith(New String("x"c, 3500), StringComparison.Ordinal)), "Body text was silently cut inside a post")
            Check(snapshot.ImportedText.Contains("BoundSeller100" & vbLf, StringComparison.Ordinal) AndAlso snapshot.ImportedText.Contains("BoundSeller1" & vbLf, StringComparison.Ordinal), "Full import omitted complete older posts")
        End Using

        Using handler As New SequenceHandler(Reply(Metadata()), Reply("[]")), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(snapshot.ImportedText = "" AndAlso snapshot.Messages.Count = 0 AndAlso snapshot.ImportedMessageCount = 0, "Empty channel produced an invented post")
        End Using
        Dim remaining = Reply(JsonSerializer.Serialize({Message("1556800000000000401", "MiXeD", "SELL YY3", "2026-10-05T14:02:00Z")}))
        remaining.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0")
        remaining.Headers.TryAddWithoutValidation("X-RateLimit-Reset-After", "0.75")
        Using handler As New SequenceHandler(Reply(Metadata()), remaining), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(Math.Abs(snapshot.SuggestedRefreshDelaySeconds - 0.75) < 0.0001 AndAlso snapshot.ImportedMessageCount = 1, "Successful exhausted bucket lost its fractional refresh delay")
        End Using
    End Function

    Private Async Function TestFailuresAndCancellation() As Task
        Using cancellation As New CancellationTokenSource(), handler As New SequenceHandler(), transport As New HttpClient(handler)
            cancellation.Cancel()
            Dim stopped = False
            Try
                Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, cancellation.Token, transport, messageLimit:=100)
            Catch ex As OperationCanceledException
                stopped = True
            End Try
            Check(stopped AndAlso handler.Requests.Count = 0, "Cancelled import sent an HTTP request")
        End Using
        Using cancellation As New CancellationTokenSource(), handler As New SequenceHandler(Reply(Metadata())), transport As New HttpClient(handler)
            handler.CancelAfterFirstRequest = cancellation
            Dim stopped = False
            Try
                Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, cancellation.Token, transport, messageLimit:=100)
            Catch ex As OperationCanceledException
                stopped = True
            End Try
            Check(stopped AndAlso handler.Requests.Count = 1, "Cancellation after metadata did not stop before messages")
        End Using
        Using handler As New SequenceHandler(Reply("{}")), transport As New HttpClient(handler)
            Dim rejected = False
            Try
                Await DiscordTradeService.FetchLatestAsync("https://discord.com.evil.example/channels/1/2", SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Catch ex As ArgumentException
                rejected = True
            Catch ex As DiscordTradeException
                rejected = True
            End Try
            Check(rejected AndAlso handler.Requests.Count = 0, "Invalid URL reached a credential-bearing request")
        End Using
        For Each credential In {"", "Bearer synthetic", "mfa.synthetic", "two words"}
            Using handler As New SequenceHandler(), transport As New HttpClient(handler)
                Dim rejected = False
                Try
                    Await DiscordTradeService.FetchLatestAsync(ChannelUrl, credential, CancellationToken.None, transport, messageLimit:=100)
                Catch ex As DiscordTradeException
                    rejected = True
                End Try
                Check(rejected AndAlso handler.Requests.Count = 0, "Invalid or personal-account credential format reached HTTP")
            End Using
        Next
        For Each status In {HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound}
            Using handler As New SequenceHandler(Reply("{""message"":""" & SyntheticCredential & """}", status)), transport As New HttpClient(handler)
                Dim failure = Await ExpectFailure(transport)
                Check(failure.ShouldStopPolling AndAlso handler.Requests.Count = 1, "Terminal access error did not stop polling")
                Check(Not failure.Message.Contains(SyntheticCredential, StringComparison.Ordinal), "HTTP body leaked a credential into the error")
            End Using
        Next
        Using handler As New SequenceHandler(Reply(Metadata()), Reply("{}", HttpStatusCode.TooManyRequests)), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(Not failure.RetryAfterSeconds.HasValue AndAlso Not failure.ShouldStopPolling, "Unknown rate-limit delay was fabricated")
            Check(handler.Requests.Count = 2, "Rate-limit response with no delay caused a guessed retry")
        End Using
        Using handler As New SequenceHandler(Reply(Metadata()), Reply("temporary failure", HttpStatusCode.ServiceUnavailable)), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(Not failure.ShouldStopPolling AndAlso handler.Requests.Count = 2, "Transient failure was treated as permanent or retried immediately")
        End Using
        Dim exhausted = Reply(Metadata())
        exhausted.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0")
        exhausted.Headers.TryAddWithoutValidation("X-RateLimit-Reset-After", "0.001")
        Using handler As New SequenceHandler(exhausted, Reply("[]")), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(snapshot.ImportedMessageCount = 0 AndAlso handler.Requests.Count = 2, "Successful metadata bucket delay did not resume the request")
        End Using
        Using handler As New SequenceHandler(Reply(Metadata(2))), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(failure.ShouldStopPolling AndAlso handler.Requests.Count = 1, "Unsupported channel type was fetched as trade text")
        End Using
        Using handler As New SequenceHandler(Reply("redirect", HttpStatusCode.Found)), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(failure.ShouldStopPolling AndAlso handler.Requests.Count = 1, "Redirect response did not fail before messages")
        End Using
        Dim unreadable = Message("1556800000000000501", "Name", "", "2026-10-05T15:00:00Z")
        Using handler As New SequenceHandler(Reply(Metadata()), Reply(JsonSerializer.Serialize({unreadable}))), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(failure.FailureKind = "no-readable-content" AndAlso failure.ShouldStopPolling, "Withheld ordinary content was treated as an empty usable snapshot")
        End Using
        unreadable("type") = 12
        Using handler As New SequenceHandler(Reply(Metadata()), Reply(JsonSerializer.Serialize({unreadable}))), transport As New HttpClient(handler)
            Dim snapshot = Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
            Check(snapshot.Messages.Count = 1 AndAlso snapshot.ImportedMessageCount = 0 AndAlso snapshot.ImportedText = "", "System-only channel notice was invented as a trade post")
        End Using
        Dim wrongChannel = Message("1556800000000000502", "Name", "SELL YY3", "2026-10-05T15:01:00Z")
        wrongChannel("channel_id") = "1556796091855278000"
        Using handler As New SequenceHandler(Reply(Metadata()), Reply(JsonSerializer.Serialize({wrongChannel}))), transport As New HttpClient(handler)
            Dim failure = Await ExpectFailure(transport)
            Check(failure.FailureKind = "invalid-response" AndAlso failure.ShouldStopPolling, "Response from a different channel was imported")
        End Using
    End Function

    Private Async Function ExpectFailure(transport As HttpClient) As Task(Of DiscordTradeException)
        Try
            Await DiscordTradeService.FetchLatestAsync(ChannelUrl, SyntheticCredential, CancellationToken.None, transport, messageLimit:=100)
        Catch ex As DiscordTradeException
            Return ex
        End Try
        Throw New Exception("Expected a classified offline import failure")
    End Function

    Private Sub TestEncryptedSettings()
        Dim secretType = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.QuizSecretStore", throwOnError:=True)
        Dim encrypted = CStr(secretType.GetMethod("Protect").Invoke(Nothing, {SyntheticCredential}))
        Check(encrypted.StartsWith("dpapi:", StringComparison.Ordinal) AndAlso Not encrypted.Contains(SyntheticCredential, StringComparison.Ordinal), "Synthetic token was not encrypted")
        Dim settings As New TradeSettings With {.DiscordChannelUrl = ChannelUrl, .EncryptedDiscordBotToken = encrypted, .DiscordAutoImport = True, .DiscordImportMessageCount = 250}
        Dim json = JsonSerializer.Serialize(settings)
        Check(Not json.Contains(SyntheticCredential, StringComparison.Ordinal), "Serialized Trade settings exposed a plaintext token")
        Dim restored = JsonSerializer.Deserialize(Of TradeSettings)(json)
        Check(restored.DiscordChannelUrl = ChannelUrl AndAlso restored.DiscordAutoImport AndAlso restored.EncryptedDiscordBotToken = encrypted AndAlso restored.DiscordImportMessageCount = 250, "Importer configuration/count failed profile roundtrip")
        Check(CStr(secretType.GetMethod("Unprotect").Invoke(Nothing, {restored.EncryptedDiscordBotToken})) = SyntheticCredential, "Synthetic token did not decrypt after roundtrip")
        Dim legacy = JsonSerializer.Deserialize(Of TradeSettings)("{""DiscordText"":""old paste""}")
        Check(legacy.DiscordChannelUrl = ChannelUrl AndAlso legacy.EncryptedDiscordBotToken = "" AndAlso Not legacy.DiscordAutoImport AndAlso legacy.DiscordText = "old paste" AndAlso legacy.DiscordImportMessageCount = 10000, "Legacy profile lost its paste/default10000 count or enabled unconfigured auto import")
    End Sub

    Private Sub TestUiApplyAndReview()
        Using fixture As New UiFixture()
            Dim replacement = Snapshot("MiXeD" & vbLf & "SELL YY3")
            fixture.Restore()
            Dim originalTextLimit = fixture.Source.MaxLength
            fixture.Source.MaxLength = 1024
            Check(Not fixture.Apply(Snapshot("")) AndAlso Not fixture.Apply(Snapshot(New String("x"c, 1025))), "Empty or oversized source snapshot replaced posts")
            fixture.Source.MaxLength = originalTextLimit
            Check(fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1, "Rejected source snapshot mutated the reviewed queue")
            replacement.TextWasLimited = True
            Check(fixture.Apply(replacement), "Shared browser source application rejected a valid complete snapshot")
            Check(fixture.Source.Text = WindowsText(replacement.ImportedText) AndAlso fixture.RecipientCount = 0 AndAlso fixture.Items.Items.Count = 0 AndAlso
                  DirectCast(fixture.Field("_tradeExtracted"), List(Of TradeListing)).Count = 0, "New source retained a stale extraction or whisper queue")
            Check(Not CBool(fixture.Field("_tradeRunning")) AndAlso Not CBool(fixture.Field("_tradeAnalyzing")) AndAlso
                  fixture.Status.Text.Contains("review exact game names", StringComparison.Ordinal), "Source application bypassed manual Analyze/review")
            fixture.Restore(replacement.ImportedText)
            Check(fixture.Apply(replacement) AndAlso fixture.RecipientCount = 1 AndAlso fixture.Items.Items.Count = 1, "Unchanged source snapshot erased a reviewed queue")
            Check(fixture.Grid.Rows(0).Cells("Character").Value.ToString() = "PuLgA", "Unchanged source snapshot changed the edited exact-case recipient")
        End Using
    End Sub

    Private Sub TestUiNativeThousandPostRoundTrip()
        Const postCount As Integer = 1000
        Const captureId As String = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        Dim names As New List(Of String)()
        Dim bodies As New List(Of String)()
        Dim messages As New List(Of Dictionary(Of String, Object))()
        Dim expectedBlocks As New List(Of String)()
        Dim firstTimestamp As New DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)
        For number As Integer = 1 To postCount
            Dim suffix = number.ToString("D4", Globalization.CultureInfo.InvariantCulture)
            Dim name = "SeLlEr" & suffix
            Dim body = "SELL YY3 offer " & suffix & " at https://example.invalid/trade/" & suffix
            names.Add(name)
            bodies.Add(body)
            expectedBlocks.Add(name & vbCrLf & body)
            messages.Add(Message((1556800000000000000UL + CULng(number)).ToString(Globalization.CultureInfo.InvariantCulture), name, body,
                                 firstTimestamp.AddSeconds(number).ToString("O", Globalization.CultureInfo.InvariantCulture)))
        Next
        Dim envelope = New With {.format = "KathanaTradeCapture", .version = 1, .captureId = captureId,
            .sourceGuildId = GuildId, .sourceChannelId = ChannelId, .requestedMessageCount = postCount,
            .historyExhausted = False, .messages = messages}
        Dim snapshot = DiscordTradeService.ParseBrowserCaptureJson(ChannelUrl, JsonSerializer.Serialize(envelope), captureId, postCount)
        Dim expectedText = String.Join(vbCrLf & vbCrLf, expectedBlocks)
        Dim originalImportedText = snapshot.ImportedText
        Check(snapshot.ImportedMessageCount = postCount AndAlso snapshot.ImportedText = expectedText.Replace(vbCrLf, vbLf), "Thousand-post parser fixture lost original names, bodies or LF source text")

        Using fixture As New UiFixture(), host As New Form With {.ClientSize = New Drawing.Size(1360, 760), .ShowInTaskbar = False,
                .StartPosition = FormStartPosition.Manual, .Location = New Drawing.Point(-30000, -30000)}, tabs As New TabControl With {.Dock = DockStyle.Fill}
            fixture.Restore()
            fixture.Source.WordWrap = False
            host.Controls.Add(tabs)
            fixture.AttachTo(tabs)
            host.Show()
            Application.DoEvents()
            Check(fixture.Source.IsHandleCreated AndAlso CBool(fixture.Field("_applyingSettings")), "Native multiline fixture has no owned HWND or profile-write guard")
            Check(fixture.Apply(snapshot), "Thousand-post import was rejected by the actual Trade UI")
            Check(fixture.Source.Text = expectedText AndAlso fixture.RecipientCount = 0, "Thousand-post UI text changed names/bodies or retained stale recipients")
            Check(DirectCast(fixture.Field("_tradeAnalysisSourcePosts"), List(Of DiscordTradeMessage)).Count = postCount AndAlso
                  CStr(fixture.Field("_tradeAnalysisSourceText")) = expectedText, "CRLF display import lost structured post identities for batched analysis")
            CheckNativePostLines(fixture.Source, names, bodies)
            Check(fixture.Source.GetLineFromCharIndex(fixture.Source.TextLength - 1) = postCount * 3 - 2, "Native TextBox did not retain all 2,999 hard lines")
            Check(snapshot.ImportedText = originalImportedText, "UI CRLF normalization mutated the service snapshot")

            fixture.SetField("_tradeLoading", True)
            Try
                fixture.SetField("_tradeExtracted", New List(Of TradeListing) From {New TradeListing With {.CharacterName = names.Last(), .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3", .Evidence = bodies.Last()}})
                fixture.Invoke("PopulateTradeItems", New List(Of String) From {"YY3"})
                fixture.Invoke("SetTradeRows", New List(Of TradeRecipient) From {New TradeRecipient With {.CharacterName = names.Last(), .Items = "YY3", .Message = "reviewed thousand-post message", .Selected = True}})
            Finally
                fixture.SetField("_tradeLoading", False)
            End Try
            Dim saved = DirectCast(fixture.Invoke("BuildPersistedTradeState"), TradeSettings)
            Dim json = JsonSerializer.Serialize(saved)
            Dim restored = JsonSerializer.Deserialize(Of TradeSettings)(json)
            fixture.Invoke("ApplyPersistedTradeState", restored)
            Check(fixture.Source.Text = expectedText AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = json, "Thousand-post displayed source/reviewed queue failed JSON profile roundtrip")
            CheckNativePostLines(fixture.Source, names, bodies)
            Check(fixture.Apply(snapshot) AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = json AndAlso fixture.RecipientCount = 1 AndAlso
                  fixture.Grid.Rows(0).Cells("Message").Value.ToString() = "reviewed thousand-post message", "Unchanged LF snapshot erased the CRLF displayed source or reviewed queue")

            Dim mixed = "MiXeD" & vbLf & "SELL YY3" & vbCr & "second line" & vbCrLf & "third line"
            fixture.Restore(mixed)
            Check(fixture.Source.Text = "MiXeD" & vbCrLf & "SELL YY3" & vbCrLf & "second line" & vbCrLf & "third line" AndAlso
                  fixture.Source.GetLineFromCharIndex(fixture.Source.TextLength - 1) = 3, "Restoring old LF/CR/mixed profiles did not produce native hard breaks")
            Dim profileText = fixture.Source.Text
            fixture.Restore(profileText)
            Check(fixture.Source.Text = profileText, "Restoring existing CRLF profile doubled carriage returns")
            fixture.Source.MaxLength = 10
            Check(Not fixture.Apply(DiscordImportTests.Snapshot("MiXeD" & vbLf & "SELL")) AndAlso fixture.Source.Text = profileText AndAlso fixture.RecipientCount = 1, "Normalized CRLF length escaped the import limit or erased review")
        End Using
    End Sub

    Private Sub CheckNativePostLines(source As TextBox, names As IReadOnlyList(Of String), bodies As IReadOnlyList(Of String))
        ' Lines splits LF in managed code even when the native edit control does not render a hard break.
        Dim displayedText = source.Text
        Dim offset As Integer = 0
        For index As Integer = 0 To names.Count - 1
            Dim bodyOffset = offset + names(index).Length + vbCrLf.Length
            Dim nativeLine = index * 3
            Check(source.GetLineFromCharIndex(offset) = nativeLine AndAlso source.GetFirstCharIndexFromLine(nativeLine) = offset AndAlso
                  source.GetLineFromCharIndex(bodyOffset) = nativeLine + 1 AndAlso source.GetFirstCharIndexFromLine(nativeLine + 1) = bodyOffset AndAlso
                  String.Equals(displayedText.Substring(offset, names(index).Length), names(index), StringComparison.Ordinal) AndAlso
                  String.Equals(displayedText.Substring(bodyOffset, bodies(index).Length), bodies(index), StringComparison.Ordinal), "Native hard-line/name/body boundary was lost for post " & (index + 1).ToString(Globalization.CultureInfo.InvariantCulture))
            offset = bodyOffset + bodies(index).Length + vbCrLf.Length * 2
        Next
    End Sub

    Private Function WindowsText(text As String) As String
        Return text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Replace(vbLf, vbCrLf)
    End Function

    Private Iterator Function Descendants(root As Control) As IEnumerable(Of Control)
        For Each child As Control In root.Controls
            Yield child
            For Each nested In Descendants(child)
                Yield nested
            Next
        Next
    End Function

    Private Sub TestUiRetiredReaderControls()
        Using fixture As New UiFixture()
            fixture.Restore()
            For Each name In {"_tradeDiscordConfigure", "_tradeDiscordImport", "_tradeDiscordAuto", "_tradeDiscordCountLabel", "_tradeDiscordTimer", "_tradeDiscordStopTimer", "_tradeDiscordFetch"}
                Check(fixture.Field(name) Is Nothing, "Retired reader UI created a control, timer or fetch delegate: " & name)
            Next
            Check(CBool(fixture.Field("_tradeDiscordShutdown")) AndAlso Not CBool(fixture.Field("_tradeDiscordConfigurationReady")), "Retired reader UI remains enabled")
            Dim controls = Descendants(fixture.UiPage).ToList()
            Check(Not controls.Any(Function(control) control.Text = "Configure Discord" OrElse control.Text = "Import latest" OrElse
                control.Text = "Auto (30 sec)" OrElse control.Text.StartsWith("Posts:", StringComparison.Ordinal)), "Retired reader controls remain in the visible Trade tree")
            Dim capture = DirectCast(fixture.Field("_tradeCaptureButton"), Button)
            Check(capture IsNot Nothing AndAlso controls.Contains(capture) AndAlso capture.Text.Contains("Browser capture", StringComparison.OrdinalIgnoreCase), "Browser capture was removed with the retired reader toolbar")
            Check(capture.Parent.Controls.Count = 1 AndAlso Object.ReferenceEquals(capture.Parent.Controls(0), capture), "Discord toolbar contains an action besides Browser capture")
            Dim requests As Integer
            Dim fetch As Func(Of String, String, CancellationToken, Task(Of DiscordTradeSnapshot)) =
                Function(channel, token, cancellation)
                    requests += 1
                    Return Task.FromException(Of DiscordTradeSnapshot)(New InvalidOperationException("A retired reader must never fetch"))
                End Function
            fixture.SetField("_tradeDiscordFetch", fetch)
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Import(True)
            fixture.Import(False)
            fixture.Invoke("ConfigureTradeDiscord")
            fixture.Invoke("UpdateTradeDiscordPolling")
            Check(requests = 0 AndAlso Not CBool(fixture.Field("_tradeDiscordImporting")) AndAlso
                  fixture.Field("_tradeDiscordCancellation") Is Nothing AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before,
                  "A retired reader entry point fetched, became busy, or changed source/review state")
            Check(Not CBool(fixture.Invoke("ApplyDiscordTradeImport", Snapshot("must not replace"), CLng(fixture.Field("_tradeDiscordGeneration")),
                  CStr(fixture.Field("_tradeDiscordChannelUrl")), fixture.Source.Text)) AndAlso fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1,
                  "A late retired reader snapshot replaced pasted/captured posts or checked whispers")
            Check(fixture.Field("_tradeDiscordTimer") Is Nothing AndAlso fixture.Field("_tradeDiscordStopTimer") Is Nothing, "Retired entry points revived reader polling timers")
        End Using
    End Sub

    Private Sub TestUiSavedConfiguration()
        Using fixture As New UiFixture()
            Dim settings = fixture.Settings()
            settings.DiscordChannelUrl = ChannelId
            settings.DiscordAutoImport = True
            settings.DiscordImportMessageCount = 250
            fixture.Invoke("ApplyPersistedTradeState", settings)
            Dim saved = DirectCast(fixture.Invoke("BuildPersistedTradeState"), TradeSettings)
            Check(saved.DiscordChannelUrl = ChannelId AndAlso Not saved.DiscordAutoImport AndAlso saved.EncryptedDiscordBotToken = settings.EncryptedDiscordBotToken AndAlso
                  saved.DiscordImportMessageCount = 250 AndAlso fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1,
                  "Legacy reader metadata restore lost source/queue metadata or reenabled retired Auto")
            Check(Not CBool(fixture.Field("_tradeDiscordConfigurationReady")) AndAlso fixture.Field("_tradeDiscordAuto") Is Nothing AndAlso
                  fixture.Field("_tradeDiscordTimer") Is Nothing, "A saved Auto preference or encrypted credential started retired reader polling")
            For Each invalid In {0, -7, 10001, Integer.MaxValue}
                settings.DiscordImportMessageCount = invalid
                fixture.Invoke("ApplyPersistedTradeState", settings)
                saved = DirectCast(fixture.Invoke("BuildPersistedTradeState"), TradeSettings)
                Check(saved.DiscordImportMessageCount = Math.Clamp(invalid, 1, 10000) AndAlso Not saved.DiscordAutoImport AndAlso
                      fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1, "Legacy count metadata escaped its bounds, enabled Auto, or erased review")
            Next
            settings.EncryptedDiscordBotToken = "dpapi:not-base64"
            fixture.Invoke("ApplyPersistedTradeState", settings)
            saved = DirectCast(fixture.Invoke("BuildPersistedTradeState"), TradeSettings)
            Check(saved.EncryptedDiscordBotToken = settings.EncryptedDiscordBotToken AndAlso Not saved.DiscordAutoImport AndAlso
                  Not CBool(fixture.Field("_tradeDiscordConfigurationReady")) AndAlso fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1,
                  "Restoring inert malformed legacy encryption attempted reader activation or lost source/queue state")
            Check(Not fixture.Status.Text.Contains(SyntheticCredential, StringComparison.Ordinal), "Legacy synthetic credential entered the status label")
        End Using
    End Sub



    Private Sub TestUiBatchedAnalysisGuards()
        Dim progress As New TradeAnalysisProgress With {.CompletedBatches = 1, .TotalBatches = 50, .ProcessedPosts = 20, .TotalPosts = 1000, .ListingCount = 2}
        Dim completed As New List(Of TradeListing) From {New TradeListing With {.CharacterName = "PuLgA", .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3", .Evidence = "SELL YY3"}}
        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            fixture.Restore()
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim sourceText = fixture.Source.Text
            Dim generation = CInt(fixture.Field("_tradeAnalysisGeneration"))
            Check(CBool(fixture.Invoke("IsCurrentTradeAnalysis", cancellation, sourceText, generation, False)) AndAlso CBool(fixture.Field("_tradeAnalyzing")) AndAlso
                  JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Beginning batched analysis erased reviewed rows, extracted items or selected keys")
            fixture.Info.Text = "awaiting offline progress"
            fixture.Invoke("ReportTradeAnalysisProgress", progress, cancellation, sourceText, generation)
            Check(fixture.Info.Text <> "awaiting offline progress" AndAlso fixture.Info.Text.Contains("20", StringComparison.Ordinal) AndAlso fixture.Info.Text.Contains("cost", StringComparison.Ordinal) AndAlso fixture.Info.Text.Contains("left", StringComparison.Ordinal) AndAlso
                  JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Current batch progress was not displayed or replaced reviewed data with partial results")
            ' A failed service returns no result, so cleanup must retain the complete previous review.
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(Not CBool(fixture.Field("_tradeAnalyzing")) AndAlso fixture.Field("_tradeAnalysisCancellation") Is Nothing AndAlso
                  JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Failed batched analysis cleanup lost the complete previous review")
        End Using

        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            Dim settings = fixture.Settings()
            settings.MessageTemplate = ""
            fixture.Invoke("ApplyPersistedTradeState", settings)
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim rejected As Boolean
            Try
                fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, cancellation, fixture.Source.Text, CInt(fixture.Field("_tradeAnalysisGeneration")))
            Catch ex As TargetInvocationException When TypeOf ex.InnerException Is ArgumentException
                rejected = True
            End Try
            Check(rejected AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Failed final queue construction partly replaced previous listings, selection or edited whispers")
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Failed final commit cleanup erased the previous complete review")
        End Using

        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            fixture.Restore()
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim sourceText = fixture.Source.Text
            Dim generation = CInt(fixture.Field("_tradeAnalysisGeneration"))
            cancellation.Cancel()
            fixture.Status.Text = "keep cancelled review status"
            fixture.Invoke("ReportTradeAnalysisProgress", progress, cancellation, sourceText, generation)
            Check(Not CBool(fixture.Invoke("IsCurrentTradeAnalysis", cancellation, sourceText, generation, False)) AndAlso
                  CBool(fixture.Invoke("IsCurrentTradeAnalysis", cancellation, sourceText, generation, True)), "Cancellation guard lost ownership or allowed a cancelled result")
            Check(fixture.Status.Text = "keep cancelled review status" AndAlso Not CBool(fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, cancellation, sourceText, generation)), "Cancelled batch progress/result replaced the reviewed queue")
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before AndAlso fixture.RecipientCount = 1, "Cancelling batched analysis lost earlier complete listings or edited whispers")
        End Using

        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            fixture.Restore()
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim sourceText = fixture.Source.Text
            Dim generation = CInt(fixture.Field("_tradeAnalysisGeneration"))
            fixture.Source.Text = "NewSource" & vbCrLf & "SELL ROR"
            Dim afterEdit = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Status.Text = "keep edited source status"
            fixture.Invoke("ReportTradeAnalysisProgress", progress, cancellation, sourceText, generation)
            Check(Not CBool(fixture.Invoke("IsCurrentTradeAnalysis", cancellation, sourceText, generation, True)) AndAlso
                  fixture.Status.Text = "keep edited source status" AndAlso Not CBool(fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, cancellation, sourceText, generation)), "Late batched analysis overwrote an edited source or its status")
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = afterEdit, "Late analysis cleanup replaced the user's newer source state")
        End Using

        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            fixture.Restore()
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim sourceText = fixture.Source.Text
            Dim generation = CInt(fixture.Field("_tradeAnalysisGeneration"))
            Dim profile = fixture.Settings()
            profile.Recipients(0).Message = "new profile's reviewed whisper"
            fixture.Invoke("ApplyPersistedTradeState", profile)
            Dim afterRestore = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Status.Text = "keep restored profile status"
            fixture.Invoke("ReportTradeAnalysisProgress", progress, cancellation, sourceText, generation)
            Check(fixture.Source.Text = sourceText AndAlso Not CBool(fixture.Invoke("IsCurrentTradeAnalysis", cancellation, sourceText, generation, True)) AndAlso
                  fixture.Status.Text = "keep restored profile status" AndAlso Not CBool(fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, cancellation, sourceText, generation)), "Same-text profile restore accepted old batch progress/results")
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = afterRestore, "Old analysis cleanup lost the restored profile's reviewed whispers")
        End Using

        Using fixture As New UiFixture(), oldCancellation As New CancellationTokenSource(), currentCancellation As New CancellationTokenSource()
            fixture.Restore()
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Invoke("BeginTradeAnalysis", oldCancellation)
            Dim sourceText = fixture.Source.Text
            Dim oldGeneration = CInt(fixture.Field("_tradeAnalysisGeneration"))
            fixture.Invoke("BeginTradeAnalysis", currentCancellation)
            Dim currentGeneration = CInt(fixture.Field("_tradeAnalysisGeneration"))
            fixture.Status.Text = "current analysis owns this status"
            fixture.Invoke("ReportTradeAnalysisProgress", progress, oldCancellation, sourceText, oldGeneration)
            Check(fixture.Status.Text = "current analysis owns this status" AndAlso Not CBool(fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, oldCancellation, sourceText, oldGeneration)), "Superseded analysis delivered old progress/results")
            fixture.Invoke("EndTradeAnalysis", oldCancellation)
            Check(CBool(fixture.Field("_tradeAnalyzing")) AndAlso Object.ReferenceEquals(fixture.Field("_tradeAnalysisCancellation"), currentCancellation) AndAlso
                  CBool(fixture.Invoke("IsCurrentTradeAnalysis", currentCancellation, sourceText, currentGeneration, False)) AndAlso
                  JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Superseded cleanup released a newer analysis or changed reviewed data")
            Check(CBool(fixture.Invoke("ApplyCompletedTradeAnalysis", completed, 1, currentCancellation, sourceText, currentGeneration)) AndAlso fixture.RecipientCount = 1 AndAlso
                  fixture.Grid.Rows(0).Cells("Character").Value.ToString() = "PuLgA" AndAlso fixture.Grid.Rows(0).Cells("Message").Value.ToString() <> "reviewed message", "Complete current analysis did not commit the new reviewed queue")
            fixture.Invoke("EndTradeAnalysis", currentCancellation)
            Check(Not CBool(fixture.Field("_tradeAnalyzing")) AndAlso fixture.Field("_tradeAnalysisCancellation") Is Nothing, "Completed current analysis retained its busy owner")
        End Using

        ' Pause / show results is only available while a job runs and asks the running job (once) to stop sending batches.
        Using fixture As New UiFixture(), cancellation As New CancellationTokenSource()
            fixture.Restore()
            Dim pause = DirectCast(fixture.Field("_tradePause"), Button)
            Check(pause.Text.Contains("Pause", StringComparison.Ordinal) AndAlso Not pause.Enabled, "Pause button was available with no analysis running")
            fixture.Invoke("PauseTradeAnalysis")
            fixture.Invoke("BeginTradeAnalysis", cancellation)
            Dim control = DirectCast(fixture.Field("_tradeAnalysisControl"), TradeAnalysisControl)
            Check(pause.Enabled AndAlso Not control.PauseRequested, "Pause button was not enabled by a running analysis")
            fixture.Invoke("PauseTradeAnalysis")
            Check(control.PauseRequested AndAlso Not pause.Enabled AndAlso fixture.Status.Text.Contains("Pausing", StringComparison.Ordinal), "Pause did not signal the running analysis")
            fixture.Invoke("EndTradeAnalysis", cancellation)
            Check(Not pause.Enabled, "Pause button stayed enabled after the analysis ended")
        End Using
    End Sub

    Private Function FindButtons(parent As Control) As List(Of Button)
        Dim result As New List(Of Button)()
        For Each child As Control In parent.Controls
            If TypeOf child Is Button Then result.Add(DirectCast(child, Button))
            result.AddRange(FindButtons(child))
        Next
        Return result
    End Function

    Private Sub TestUiSessionSaveAndLoad()
        Dim folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kathana-ui-session-" & Guid.NewGuid().ToString("N"))
        Try
            Using fixture As New UiFixture()
                fixture.Restore()
                Dim buttons = FindButtons(fixture.UiPage).Select(Function(button) button.Text).ToList()
                Check(buttons.Contains("Save session...") AndAlso buttons.Contains("Load session..."), "Trade tab has no Save session / Load session buttons")
                Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
                Dim file = System.IO.Path.Combine(folder, "saved" & TradeSessionStore.FileExtension)
                Check(CBool(fixture.Invoke("SaveTradeSessionTo", file)) AndAlso System.IO.File.Exists(file), "session was not saved")
                Check(fixture.Status.Text.Contains("Session saved", StringComparison.Ordinal) AndAlso fixture.Status.Text.Contains("1 detected listing(s)", StringComparison.Ordinal), "save status did not summarize the session: " & fixture.Status.Text)
                Using document = JsonDocument.Parse(System.IO.File.ReadAllText(file))
                    Check(document.RootElement.GetProperty("Settings").GetProperty("EncryptedDiscordBotToken").GetString() = "" AndAlso Not document.RootElement.GetProperty("Settings").GetProperty("DiscordAutoImport").GetBoolean(), "the saved file kept the Discord token or automatic import")
                End Using

                ' Change everything, then load: the captured posts, detected items, selected items and queue come back.
                fixture.Source.Text = "SomeoneElse" & vbCrLf & "SELL ROR"
                Check(fixture.RecipientCount = 0, "test setup: editing the posts did not clear the queue")
                Check(CBool(fixture.Invoke("LoadTradeSessionFrom", file)), "session did not load: " & fixture.Status.Text)
                Dim after = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
                Check(after = before, "loading did not restore the saved posts, items, selection and whisper queue")
                Check(fixture.Source.Text = UiFixture.OriginalText AndAlso fixture.RecipientCount = 1 AndAlso fixture.Grid.Rows(0).Cells("Message").Value.ToString() = "reviewed message" AndAlso fixture.Items.Items.Count = 1 AndAlso fixture.Items.CheckedItems.Count = 1, "loaded session did not show its posts, detected/selected items and queue")
                Check(fixture.Status.Text.Contains("Session loaded", StringComparison.Ordinal), "load status missing: " & fixture.Status.Text)

                ' A damaged or foreign file never touches the current state.
                Dim damaged = System.IO.Path.Combine(folder, "damaged.json")
                System.IO.File.WriteAllText(damaged, "{""Format"":""Other""}")
                Check(Not CBool(fixture.Invoke("LoadTradeSessionFrom", damaged)) AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before AndAlso fixture.Status.Text.Contains("kept", StringComparison.Ordinal), "a bad session file changed or did not report the current state")
                Check(Not CBool(fixture.Invoke("LoadTradeSessionFrom", System.IO.Path.Combine(folder, "missing.json"))) AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "a missing session file changed the current state")

                ' Busy Trade actions block both save and load.
                For Each fieldName In {"_tradeAnalyzing", "_tradeRunning"}
                    fixture.SetField(fieldName, True)
                    Dim other = System.IO.Path.Combine(folder, "blocked.json")
                    Check(Not CBool(fixture.Invoke("SaveTradeSessionTo", other)) AndAlso Not System.IO.File.Exists(other), "saving while busy was allowed: " & fieldName)
                    Check(Not CBool(fixture.Invoke("LoadTradeSessionFrom", file)), "loading while busy was allowed: " & fieldName)
                    fixture.SetField(fieldName, False)
                Next
                Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "blocked save/load changed the state")
            End Using
        Finally
            Try
                System.IO.Directory.Delete(folder, True)
            Catch
            End Try
        End Try
    End Sub

    Private Sub TestUiShutdown()
        Using fixture As New UiFixture()
            fixture.Restore()
            Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
            fixture.Invoke("ShutdownTradeDiscordImport")
            fixture.Import(True)
            fixture.Import(False)
            fixture.Invoke("UpdateTradeDiscordPolling")
            Check(CBool(fixture.Field("_tradeDiscordShutdown")) AndAlso JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before,
                  "Closing retired reader state changed pasted posts or the reviewed queue")
            Check(fixture.Field("_tradeDiscordTimer") Is Nothing AndAlso fixture.Field("_tradeDiscordStopTimer") Is Nothing AndAlso
                  fixture.Field("_tradeDiscordCancellation") Is Nothing AndAlso Not CBool(fixture.Field("_tradeDiscordImporting")), "Retired reader cleanup revived timers or retained a request")
            Check(Not CBool(fixture.Invoke("ApplyDiscordTradeImport", Snapshot("must not apply"), CLng(fixture.Field("_tradeDiscordGeneration")),
                  CStr(fixture.Field("_tradeDiscordChannelUrl")), fixture.Source.Text)), "Direct retired reader application accepted posts after shutdown")
        End Using
    End Sub



    Private Function Snapshot(text As String) As DiscordTradeSnapshot
        Return New DiscordTradeSnapshot With {.ImportedText = text, .FetchedMessageCount = 1, .ImportedMessageCount = If(text.Length = 0, 0, 1), .ChannelId = ChannelId, .GuildId = GuildId, .ChannelLink = ChannelUrl}
    End Function

    Private Sub WaitUi(work As Task)
        Dim deadline = DateTime.UtcNow.AddSeconds(5)
        While Not work.IsCompleted AndAlso DateTime.UtcNow < deadline
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Check(work.IsCompleted, "Offline UI import did not complete")
        work.GetAwaiter().GetResult()
    End Sub

    Private Class UiFixture
        Implements IDisposable
        Public Const OriginalImportedText As String = "PuLgA" & vbLf & "2026-10-05T15:00:00Z" & vbLf & "SELL YY3"
        Public Const OriginalText As String = "PuLgA" & vbCrLf & "2026-10-05T15:00:00Z" & vbCrLf & "SELL YY3"
        Private ReadOnly form As Object = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Private ReadOnly stopTimer As New System.Windows.Forms.Timer()
        Private ReadOnly page As TabPage
        Private ReadOnly encrypted As String
        Public Sub New()
            Dim secretType = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.QuizSecretStore", throwOnError:=True)
            encrypted = CStr(secretType.GetMethod("Protect").Invoke(Nothing, {SyntheticCredential}))
            ' Supply the handleless base Control wrapper without invoking Form1's real settings/native startup.
            Dim windowField = GetType(Control).GetField("_window", PrivateFields)
            Dim windowConstructor = windowField.FieldType.GetConstructor(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic, Nothing, {GetType(Control)}, Nothing)
            windowField.SetValue(form, windowConstructor.Invoke({form}))
            SetField("_tradeStopTimer", stopTimer)
            ' Successful imports must never write the user's real profile from a reflection fixture.
            SetField("_applyingSettings", True)
            page = DirectCast(Invoke("BuildTradeTab"), TabPage)
        End Sub
        Public Function Settings(Optional sourceText As String = OriginalImportedText) As TradeSettings
            Return New TradeSettings With {.DiscordText = sourceText, .DiscordChannelUrl = ChannelUrl, .EncryptedDiscordBotToken = encrypted,
                .ExtractedListings = New List(Of TradeListing) From {New TradeListing With {.CharacterName = "PuLgA", .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3", .Evidence = "SELL YY3"}},
                .SelectedItemKeys = New List(Of String) From {"YY3"}, .Recipients = New List(Of TradeRecipient) From {New TradeRecipient With {.CharacterName = "PuLgA", .Items = "YY3", .Message = "reviewed message", .Selected = True}}}
        End Function
        Public Sub Restore(Optional text As String = OriginalImportedText)
            Invoke("ApplyPersistedTradeState", Settings(text))
        End Sub
        Public Function Apply(snapshot As DiscordTradeSnapshot) As Boolean
            Return CBool(Invoke("ApplyTradeSourceSnapshot", snapshot, 10000, True, "Browser capture"))
        End Function
        Public Function BeginImport(manual As Boolean) As Task
            Return DirectCast(Invoke("ImportTradeDiscordAsync", manual), Task)
        End Function
        Public Sub Import(manual As Boolean)
            WaitUi(BeginImport(manual))
        End Sub
        Public Function Invoke(name As String, ParamArray values As Object()) As Object
            Return GetType(Form1).GetMethod(name, PrivateFields).Invoke(form, values)
        End Function
        Public Function Field(name As String) As Object
            Return GetType(Form1).GetField(name, PrivateFields).GetValue(form)
        End Function
        Public Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, PrivateFields).SetValue(form, value)
        End Sub
        Public Sub AttachTo(tabs As TabControl)
            tabs.TabPages.Add(page)
        End Sub
        Public ReadOnly Property Source As TextBox
            Get
                Return DirectCast(Field("_tradeSource"), TextBox)
            End Get
        End Property
        Public ReadOnly Property Grid As DataGridView
            Get
                Return DirectCast(Field("_tradeGrid"), DataGridView)
            End Get
        End Property
        Public ReadOnly Property RecipientCount As Integer
            Get
                Return Grid.Rows.Cast(Of DataGridViewRow)().Count(Function(row) Not row.IsNewRow)
            End Get
        End Property
        Public ReadOnly Property Items As CheckedListBox
            Get
                Return DirectCast(Field("_tradeDetectedItems"), CheckedListBox)
            End Get
        End Property
        Public ReadOnly Property Status As Label
            Get
                Return DirectCast(Field("_tradeStatus"), Label)
            End Get
        End Property
        Public ReadOnly Property Info As Label
            Get
                Return DirectCast(Field("_tradeAnalysisInfo"), Label)
            End Get
        End Property
        Public ReadOnly Property UiPage As TabPage
            Get
                Return page
            End Get
        End Property
        Public Sub Dispose() Implements IDisposable.Dispose
            page.Dispose()
            stopTimer.Dispose()
        End Sub
    End Class

    Private Class NoInput
        Implements IWindowsInput
        Public Calls As Integer
        Public Function Post(hwnd As IntPtr, message As UInteger, w As IntPtr, l As IntPtr) As Boolean Implements IWindowsInput.Post
            Calls += 1
            Return False
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Calls += 1
            Return False
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Calls += 1
            Return False
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            Calls += 1
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            Calls += 1
        End Sub
    End Class

    Private Function Metadata(Optional channelType As Integer = 0) As String
        Return JsonSerializer.Serialize(New With {.id = ChannelId, .guild_id = GuildId, .name = "trade-import", .type = channelType})
    End Function
    Private Function Message(id As String, author As String, body As String, timestamp As String) As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {
            {"id", id}, {"channel_id", ChannelId}, {"author", New With {.id = "1556800000000000990", .username = author}},
            {"timestamp", timestamp}, {"content", body}, {"embeds", Array.Empty(Of Object)()}}
    End Function
    Private Function Reply(body As String, Optional status As HttpStatusCode = HttpStatusCode.OK) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function
    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New Exception(reason)
    End Sub

    Private Class RequestObservation
        Public Property Host As String
        Public Property Path As String
        Public Property Query As String
        Public Property Method As String
        Public Property AuthorizationScheme As String
        Public Property AuthorizationParameter As String
    End Class
    Private Class SequenceHandler
        Inherits HttpMessageHandler
        Private ReadOnly responses As Queue(Of HttpResponseMessage)
        Public ReadOnly Requests As New List(Of RequestObservation)()
        Public Property CancelAfterFirstRequest As CancellationTokenSource
        Public Sub New(ParamArray responses As HttpResponseMessage())
            Me.responses = New Queue(Of HttpResponseMessage)(responses)
        End Sub
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellation As CancellationToken) As Task(Of HttpResponseMessage)
            cancellation.ThrowIfCancellationRequested()
            Requests.Add(New RequestObservation With {.Host = request.RequestUri.Host, .Path = request.RequestUri.AbsolutePath, .Query = request.RequestUri.Query,
                .Method = request.Method.Method, .AuthorizationScheme = request.Headers.Authorization?.Scheme, .AuthorizationParameter = request.Headers.Authorization?.Parameter})
            If CancelAfterFirstRequest IsNot Nothing AndAlso Requests.Count = 1 Then CancelAfterFirstRequest.Cancel()
            If responses.Count = 0 Then Throw New Exception("Importer requested an unexpected extra HTTP response")
            Return Task.FromResult(responses.Dequeue())
        End Function
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                While responses.Count > 0
                    responses.Dequeue().Dispose()
                End While
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Module
