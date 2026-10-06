Imports System.Globalization
Imports System.Net
Imports System.Net.Http
Imports System.Net.Sockets
Imports System.Reflection
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Module DiscordBrowserCaptureTests
    Private Const GuildId As String = "1363839027114934315"
    Private Const ChannelId As String = "1486808135883555037"
    Private Const SourceUrl As String = "https://discord.com/channels/" & GuildId & "/" & ChannelId
    Private Const SyntheticCaptureId As String = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    Private Const PrivateBodyMarker As String = "private-body-fixture-that-must-not-appear-in-errors"
    Private checks As Integer
    Private Const PrivateFields As BindingFlags = BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic

    Public Async Function RunAsync() As Task
        checks = 0
        TestParserScopeAndContent()
        TestParserBoundsAndDeduplication()
        TestParserFailuresAreRedacted()
        Await TestLoopbackCaptureAsync()
        Await TestPauseRejectAndCallbackFailuresAsync()
        Console.WriteLine($"PASS: {checks} offline browser-capture assertions: exact source/session, 10,000-post bounds, embeds/edits, redacted failures, loopback capability, pause, one-shot delivery and stopped sessions.")
    End Function

    Public Sub RunUiTests()
        Dim initialChecks = checks
        TestUiCaptureGuards()
        TestUiCaptureSourceAndLifecycle()
        TestUiCaptureResumesWithoutReplacingAnalysisStatus()
        TestCaptureDialogAndRender()
        Console.WriteLine($"PASS: {checks - initialChecks} offline browser-capture UI assertions: source/generation/count guards, reviewed-queue preservation, edits/profile/stop/close invalidation and connection dialog.")
    End Sub

    Private Sub TestUiCaptureResumesWithoutReplacingAnalysisStatus()
        Using fixture As New CaptureUiFixture(), cancellation As New CancellationTokenSource()
            fixture.PrepareCapture()
            Dim server = DirectCast(fixture.Field("_tradeCaptureServer"), DiscordBrowserCaptureServer)
            Try
                Check(server.IsRunning AndAlso Not server.Paused AndAlso Not CBool(fixture.Field("_tradeCaptureCompleted")) AndAlso
                      fixture.Grid.Rows.Cast(Of DataGridViewRow)().All(Function(row) row.IsNewRow OrElse Not Object.Equals(row.Cells("Send").Value, True)), "Capture status fixture is not an unfinished unpaused local session with no checked whispers")
                Dim before = JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState"))
                fixture.Invoke("BeginTradeAnalysis", cancellation)
                Check(server.Paused AndAlso CBool(fixture.Field("_tradeCapturePaused")), "Beginning analysis did not pause the unfinished local capture")
                Const failureStatus As String = "AI analysis failed; previous analyzed items and reviewed whispers are kept. Offline failure."
                Dim status = DirectCast(fixture.Field("_tradeStatus"), Label)
                status.Text = failureStatus
                fixture.Invoke("EndTradeAnalysis", cancellation)
                Check(server.IsRunning AndAlso Not server.Paused AndAlso Not CBool(fixture.Field("_tradeCapturePaused")) AndAlso status.Text = failureStatus,
                      "Resuming capture replaced the final analysis failure with a capture-resumed status")
                Check(JsonSerializer.Serialize(fixture.Invoke("BuildPersistedTradeState")) = before, "Capture resume after failed analysis changed the previous complete review")
            Finally
                fixture.Invoke("StopTradeBrowserCapture")
            End Try
        End Using
    End Sub

    Public Sub RenderDialogOnly()
        checks = 0
        TestCaptureDialogAndRender()
        Console.WriteLine($"PASS: {checks} browser-capture dialog/render assertions.")
    End Sub

    Private Sub TestUiCaptureGuards()
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            Dim replacement = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            For Each fieldName In {"_tradeLoading", "_tradeAnalyzing", "_tradeRunning", "_tradeDiscordConfiguring", "_tradeDiscordImporting"}
                fixture.SetField(fieldName, True)
                Check(Not fixture.Apply(replacement) AndAlso fixture.Source.Text = CaptureUiFixture.OriginalText AndAlso fixture.RecipientCount = 1, "Busy capture changed posts or reviewed whispers: " & fieldName)
                fixture.SetField(fieldName, False)
            Next
            fixture.Grid.Rows(0).Cells("Send").Value = True
            Check(Not fixture.Apply(replacement) AndAlso fixture.RecipientCount = 1, "Checked review queue was overwritten by capture")
            fixture.Grid.Rows(0).Cells("Send").Value = False
            Check(Not fixture.Apply(replacement, CLng(fixture.Field("_tradeCaptureGeneration")) - 1), "Stale capture generation replaced posts")
            Dim wrongSource = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            wrongSource.ChannelId = "1486808135883555038"
            Check(Not fixture.Apply(wrongSource), "Wrong source channel replaced posts")
            wrongSource = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            wrongSource.GuildId = "1363839027114934316"
            Check(Not fixture.Apply(wrongSource), "Wrong source guild replaced posts")
            Dim wrongCount = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            wrongCount.RequestedMessageCount = 9
            Check(Not fixture.Apply(wrongCount) AndAlso fixture.Source.Text = CaptureUiFixture.OriginalText, "Capture with a mismatched requested count replaced posts")
            wrongCount = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            wrongCount.FetchedMessageCount = 11
            Check(Not fixture.Apply(wrongCount), "Capture fetched-count exceeded selected limit")
            wrongCount = UiSnapshot("OtherSeller" & vbLf & "SELL ROR")
            wrongCount.ImportedMessageCount = 2
            Check(Not fixture.Apply(wrongCount), "Capture imported-count exceeded fetched messages")
            Dim empty = UiSnapshot("")
            empty.ImportedMessageCount = 0
            Check(Not fixture.Apply(empty) AndAlso fixture.RecipientCount = 1, "Empty capture cleared reviewed queue")
            Check(fixture.Apply(UiSnapshot(CaptureUiFixture.OriginalImportedText)) AndAlso fixture.Source.Text = CaptureUiFixture.OriginalText AndAlso fixture.RecipientCount = 1 AndAlso fixture.Grid.Rows(0).Cells("Message").Value.ToString() = "reviewed message", "Unchanged LF capture lost the CRLF displayed source or reviewed whisper queue")
            Check(CBool(fixture.Field("_tradeCaptureCompleted")) AndAlso Not fixture.Apply(replacement), "Completed UI capture accepted a second replacement")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            DirectCast(fixture.Field("_tradeCaptureCancellation"), CancellationTokenSource).Cancel()
            Check(Not fixture.Apply(UiSnapshot("replacement")) AndAlso fixture.Source.Text = CaptureUiFixture.OriginalText, "Cancelled capture replaced posts")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            DirectCast(fixture.Field("_tradeCaptureServer"), DiscordBrowserCaptureServer).Stop()
            Check(Not fixture.Apply(UiSnapshot("replacement")) AndAlso fixture.Source.Text = CaptureUiFixture.OriginalText AndAlso fixture.RecipientCount = 1, "Stopped server's queued callback replaced posts")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            GetType(DiscordBrowserCaptureServer).GetField("expiresAt", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(fixture.Field("_tradeCaptureServer"), DateTimeOffset.UtcNow.AddSeconds(-1))
            Check(Not fixture.Apply(UiSnapshot("replacement")) AndAlso fixture.RecipientCount = 1, "Expired server's queued callback replaced reviewed queue")
        End Using
    End Sub

    Private Sub TestUiCaptureSourceAndLifecycle()
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            Check(fixture.Apply(UiSnapshot("NewSeller" & vbLf & "SELL ROR")) AndAlso fixture.Source.Text = "NewSeller" & vbCrLf & "SELL ROR" AndAlso fixture.RecipientCount = 0, "Changed capture lost CRLF message breaks or retained stale whisper recipients")
            Check(fixture.Field("_tradeCaptureServer") IsNot Nothing AndAlso CBool(fixture.Field("_tradeCaptureCompleted")), "Applying capture text invalidated its own session")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            Dim generation = CLng(fixture.Field("_tradeCaptureGeneration"))
            fixture.Source.Text &= vbLf & "user edit"
            Check(fixture.Field("_tradeCaptureServer") Is Nothing AndAlso CLng(fixture.Field("_tradeCaptureGeneration")) > generation, "Editing posts left browser capture active")
            Check(Not fixture.Apply(UiSnapshot("replacement"), generation) AndAlso fixture.Source.Text.EndsWith("user edit", StringComparison.Ordinal), "Late capture overwrote a source edit")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            Dim generation = CLng(fixture.Field("_tradeCaptureGeneration"))
            fixture.Invoke("StopTradeBrowserCapture")
            Check(fixture.Field("_tradeCaptureServer") Is Nothing AndAlso fixture.Field("_tradeCaptureCancellation") Is Nothing AndAlso CLng(fixture.Field("_tradeCaptureGeneration")) > generation, "Stop retained active browser capture")
            Check(Not fixture.Apply(UiSnapshot("replacement"), generation) AndAlso fixture.RecipientCount = 1, "Stopped capture replaced reviewed whispers")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            fixture.Invoke("ApplyPersistedTradeState", fixture.Settings())
            Check(fixture.Field("_tradeCaptureServer") Is Nothing AndAlso fixture.RecipientCount = 1, "Loading profile retained browser capture or lost reviewed queue")
        End Using
        Using fixture As New CaptureUiFixture()
            fixture.PrepareCapture()
            fixture.Invoke("ShutdownTradeBrowserCapture")
            Check(CBool(fixture.Field("_tradeCaptureShutdown")) AndAlso fixture.Field("_tradeCaptureTimer") Is Nothing AndAlso fixture.Field("_tradeCaptureServer") Is Nothing, "Close did not dispose browser capture timers/session")
            Check(Not fixture.Apply(UiSnapshot("replacement")) AndAlso fixture.RecipientCount = 1, "Closed capture replaced reviewed whispers")
        End Using
    End Sub

    Private Sub TestCaptureDialogAndRender()
        Dim dialogType = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.DiscordTradeCaptureDialog", throwOnError:=True)
        Dim starts As Integer, stops As Integer, dialog As Form = Nothing
        Dim setup = JsonSerializer.Serialize(New With {.format = "KathanaCaptureConnection", .version = 1, .receiverUrl = "http://127.0.0.1:48123/kathana-capture/" & SyntheticCaptureId & "/", .sourceUrl = SourceUrl, .messageLimit = 10000})
        Dim start As Func(Of String, Integer, String) = Function(source, count)
                                                          starts += 1
                                                          Check(source = SourceUrl AndAlso count = 10000, "Dialog start lost channel/count")
                                                          Return setup
                                                      End Function
        Dim stopAction As Action = Sub()
                                       stops += 1
                                       If dialog IsNot Nothing Then dialogType.GetMethod("UpdateConnection").Invoke(dialog, {""})
                                   End Sub
        Using instance = DirectCast(Activator.CreateInstance(dialogType, {SourceUrl, 10000, start, stopAction}), Form)
            dialog = instance
            Dim countBox = DirectCast(dialogType.GetField("_countBox", PrivateFields).GetValue(dialog), NumericUpDown)
            Dim connectionBox = DirectCast(dialogType.GetField("_connectionBox", PrivateFields).GetValue(dialog), TextBox)
            Dim sourceBox = DirectCast(dialogType.GetField("_sourceBox", PrivateFields).GetValue(dialog), TextBox)
            Dim startButton = DirectCast(dialogType.GetField("_startButton", PrivateFields).GetValue(dialog), Button)
            Dim copyButton = DirectCast(dialogType.GetField("_copyButton", PrivateFields).GetValue(dialog), Button)
            Check(countBox.Minimum = 1 AndAlso countBox.Maximum = 10000 AndAlso countBox.Value = 10000 AndAlso countBox.ThousandsSeparator, "Dialog did not expose 1..10,000 posts")
            Check(sourceBox.Text = SourceUrl AndAlso connectionBox.ReadOnly AndAlso Not copyButton.Enabled, "Disconnected dialog contains a stale setup")
            GetType(Control).GetMethod("OnClick", PrivateFields).Invoke(startButton, {EventArgs.Empty})
            Check(starts = 1 AndAlso connectionBox.Text = setup AndAlso copyButton.Enabled AndAlso Not startButton.Enabled, "Dialog did not expose its explicit started connection")
            countBox.Value = 250
            Check(stops = 1 AndAlso connectionBox.TextLength = 0 AndAlso startButton.Enabled, "Count change did not invalidate active connection")
            dialogType.GetMethod("UpdateConnection").Invoke(dialog, {setup})
            sourceBox.Text = "https://discord.com/channels/" & GuildId & "/1486808135883555038"
            Check(stops = 2 AndAlso connectionBox.TextLength = 0, "Source change retained old connection")
            sourceBox.Text = SourceUrl
            countBox.Value = 10000
            dialogType.GetMethod("UpdateConnection").Invoke(dialog, {setup})
            dialog.StartPosition = FormStartPosition.Manual
            dialog.Location = New System.Drawing.Point(-32000, -32000)
            dialog.Show()
            Application.DoEvents()
            dialog.PerformLayout()
            For Each control As Control In dialog.Controls
                Check(control.Left >= 0 AndAlso control.Top >= 0 AndAlso control.Right <= dialog.ClientSize.Width AndAlso control.Bottom <= dialog.ClientSize.Height, "Capture dialog clips a control: " & control.Name)
            Next
            Using picture As New System.Drawing.Bitmap(dialog.Width, dialog.Height)
                dialog.DrawToBitmap(picture, New System.Drawing.Rectangle(0, 0, picture.Width, picture.Height))
                picture.Save(System.IO.Path.Combine(AppContext.BaseDirectory, "trade-browser-capture.png"), System.Drawing.Imaging.ImageFormat.Png)
            End Using
            Check(stops = 2, "Rendering connection dialog changed capture state")
            dialog.Close()
            Check(stops = 2, "Closing the connection dialog unexpectedly stopped an explicitly started capture")
        End Using
    End Sub

    Private Function UiSnapshot(text As String) As DiscordTradeSnapshot
        Return New DiscordTradeSnapshot With {.GuildId = GuildId, .ChannelId = ChannelId, .ChannelLink = SourceUrl, .RequestedMessageCount = 10, .FetchedMessageCount = 1, .ImportedMessageCount = 1, .ImportedText = text}
    End Function

    Private Class CaptureUiFixture
        Implements IDisposable
        Public Const OriginalImportedText As String = "PuLgA" & vbLf & "2026-10-05T15:00:00Z" & vbLf & "SELL YY3"
        Public Const OriginalText As String = "PuLgA" & vbCrLf & "2026-10-05T15:00:00Z" & vbCrLf & "SELL YY3"
        Private ReadOnly form As Object = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Private ReadOnly stopTimer As New System.Windows.Forms.Timer()
        Private ReadOnly page As TabPage

        Public Sub New()
            Dim windowField = GetType(Control).GetField("_window", PrivateFields)
            Dim windowConstructor = windowField.FieldType.GetConstructor(PrivateFields, Nothing, {GetType(Control)}, Nothing)
            windowField.SetValue(form, windowConstructor.Invoke({form}))
            SetField("_tradeStopTimer", stopTimer)
            SetField("_applyingSettings", True)
            page = DirectCast(Invoke("BuildTradeTab"), TabPage)
            Invoke("ApplyPersistedTradeState", Settings())
        End Sub

        Public Sub PrepareCapture()
            Invoke("StopTradeBrowserCapture")
            Dim server As New DiscordBrowserCaptureServer(SourceUrl, 10, Function(snapshot) Task.FromResult(True))
            server.Start()
            SetField("_tradeCaptureServer", server)
            SetField("_tradeCaptureCancellation", New CancellationTokenSource())
            SetField("_tradeCaptureSourceUrl", SourceUrl)
            SetField("_tradeCaptureMessageCount", 10)
            SetField("_tradeCaptureExpectedSourceText", Source.Text)
        End Sub

        Public Function Settings() As TradeSettings
            Return New TradeSettings With {.DiscordText = OriginalImportedText, .DiscordImportMessageCount = 10,
                .ExtractedListings = New List(Of TradeListing) From {New TradeListing With {.CharacterName = "PuLgA", .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3", .Evidence = "SELL YY3"}},
                .SelectedItemKeys = New List(Of String) From {"YY3"}, .Recipients = New List(Of TradeRecipient) From {New TradeRecipient With {.CharacterName = "PuLgA", .Items = "YY3", .Message = "reviewed message", .Selected = False}}}
        End Function

        Public Function Apply(snapshot As DiscordTradeSnapshot, Optional generation As Long? = Nothing) As Boolean
            Return CBool(Invoke("ApplyTradeBrowserCapture", snapshot, If(generation, CLng(Field("_tradeCaptureGeneration")))))
        End Function

        Public Function Invoke(name As String, ParamArray values As Object()) As Object
            Return GetType(Form1).GetMethod(name, PrivateFields).Invoke(form, values)
        End Function

        Public Function Field(name As String) As Object
            Return GetType(Form1).GetField(name, PrivateFields).GetValue(form)
        End Function

        Public Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, PrivateFields).SetValue(form, value)
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

        Public Sub Dispose() Implements IDisposable.Dispose
            page.Dispose()
            stopTimer.Dispose()
        End Sub
    End Class

    Private Sub TestParserScopeAndContent()
        Dim embedded = Message(3, "YoNeX", "")
        embedded("embeds") = {New With {.type = "rich", .title = "BUY DARK XTRAL PM YOUR PRICE", .description = "SELL ROR 15 heart", .fields = {New With {.name = "Contact", .value = "YoNeX"}}}}
        Dim payload = Envelope(SyntheticCaptureId, 10, {Message(2, "MissBeautiful", "T>Ring of brave"), embedded}, True)
        Dim result = DiscordTradeService.ParseBrowserCaptureJson(SourceUrl, JsonSerializer.Serialize(payload), SyntheticCaptureId, 10)
        Check(result.GuildId = GuildId AndAlso result.ChannelId = ChannelId AndAlso result.ChannelLink = SourceUrl, "Capture lost its exact source")
        Check(result.FetchedMessageCount = 2 AndAlso result.ImportedMessageCount = 2 AndAlso result.RequestedMessageCount = 10, "Capture counts are inaccurate")
        Check(result.HistoryExhausted AndAlso result.NewestMessageId = MessageId(3), "Capture completion or newest message changed")
        Check(result.Messages.Select(Function(post) post.Id).SequenceEqual({MessageId(2), MessageId(3)}), "Capture text is not chronological")
        Check(result.Messages.Last().AuthorName = "YoNeX" AndAlso result.ImportedText.Contains("YoNeX" & vbLf, StringComparison.Ordinal), "Capture changed exact author case")
        Check(result.ImportedText.Contains("BUY DARK XTRAL PM YOUR PRICE", StringComparison.Ordinal) AndAlso result.ImportedText.Contains("Contact: YoNeX", StringComparison.Ordinal), "Embedded trade text was lost")
        Check(result.ImportedText = "MissBeautiful" & vbLf & "T>Ring of brave" & vbLf & vbLf & "YoNeX" & vbLf & "BUY DARK XTRAL PM YOUR PRICE" & vbLf & "SELL ROR 15 heart" & vbLf & "Contact: YoNeX", "Capture text must contain only exact author names and messages")
        Check(Not result.ImportedText.Contains("Followed announcement delivery identity", StringComparison.Ordinal), "Original webhook post was mislabeled as a Follow delivery")
        payload("historyExhausted") = False
        Dim partialCapture = DiscordTradeService.ParseBrowserCaptureJson(SourceUrl, JsonSerializer.Serialize(payload), SyntheticCaptureId, 10)
        Check(Not partialCapture.HistoryExhausted AndAlso partialCapture.FetchedMessageCount < partialCapture.RequestedMessageCount, "Partial capture was reported as complete history")
    End Sub

    Private Sub TestParserBoundsAndDeduplication()
        Dim olderEdit = Message(4, "CaSeName", "old version")
        olderEdit("edited_timestamp") = "2026-10-05T23:22:00Z"
        Dim newerEdit = Message(4, "CaSeName", "new version")
        newerEdit("edited_timestamp") = "2026-10-05T23:23:00Z"
        Dim result = DiscordTradeService.ParseBrowserCaptureJson(SourceUrl, JsonSerializer.Serialize(Envelope(SyntheticCaptureId, 4, {Message(1, "first", "old post"), newerEdit, olderEdit, Message(3, "third", "third post")}, False)), SyntheticCaptureId, 4)
        Check(result.Messages.Count = 3 AndAlso result.Messages.Select(Function(post) post.Id).SequenceEqual({MessageId(1), MessageId(3), MessageId(4)}), "Capture duplicated a post or changed chronological order")
        Check(result.Messages.Last().BodyText = "new version" AndAlso Not result.ImportedText.Contains("old version", StringComparison.Ordinal), "An older duplicate replaced the newer edit")
        ExpectParserFailure(Envelope(SyntheticCaptureId, 2, {Message(1, "first", "one"), Message(2, "second", "two"), Message(3, "third", "three")}, False), expectedCount:=2)
        Dim history As New List(Of Dictionary(Of String, Object))()
        For index = 1 To 10000
            history.Add(Message(index, "Seller" & index.ToString(CultureInfo.InvariantCulture), "SELL TIKOY " & index.ToString(CultureInfo.InvariantCulture)))
        Next
        Dim all = DiscordTradeService.ParseBrowserCaptureJson(SourceUrl, JsonSerializer.Serialize(Envelope(SyntheticCaptureId, 10000, history, False)), SyntheticCaptureId, 10000)
        Check(all.Messages.Count = 10000 AndAlso all.FetchedMessageCount = 10000 AndAlso all.ImportedMessageCount = 10000, "Capture failed to retain 10,000 unique posts")
        Check(all.ImportedText.Length > 200000 AndAlso Not all.TextWasLimited, "Capture incorrectly used the separate AI analysis limit")
        Check(all.Messages.First().Id = MessageId(1) AndAlso all.Messages.Last().Id = MessageId(10000), "10,000-post capture lost first or last message")
        For Each count In {0, 10001, Integer.MaxValue}
            ExpectParserFailure(Envelope(SyntheticCaptureId, count, {Message(1, "one", "SELL one")}, False), expectedCount:=count)
        Next
        history.Add(Message(10001, "extra", "SELL extra"))
        ExpectParserFailure(Envelope(SyntheticCaptureId, 10000, history, False), expectedCount:=10000)
    End Sub

    Private Sub TestParserFailuresAreRedacted()
        Dim payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload("sourceGuildId") = "1363839027114934316"
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload("sourceChannelId") = "1486808135883555038"
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload("captureId") = New String("f"c, 64)
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 9, {Message(1, "seller", PrivateBodyMarker)}, False)
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload.Remove("historyExhausted")
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload("historyExhausted") = "true"
        ExpectParserFailure(payload)
        payload = Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
        payload("messages") = New With {.privateContent = PrivateBodyMarker}
        ExpectParserFailure(payload)
        For Each field In {"id", "channel_id", "timestamp"}
            Dim invalid = Message(1, "seller", PrivateBodyMarker)
            invalid(field) = If(field = "channel_id", "1486808135883555038", "invalid-" & PrivateBodyMarker)
            ExpectParserFailure(Envelope(SyntheticCaptureId, 10, {invalid}, False))
        Next
        Dim nested = Message(1, "seller", PrivateBodyMarker)
        nested("author") = "invalid-" & PrivateBodyMarker
        ExpectParserFailure(Envelope(SyntheticCaptureId, 10, {nested}, False))
        ExpectParserFailure(Envelope(SyntheticCaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False), source:="https://discord.com/channels/" & GuildId & "/1486808135883555038")
        Try
            DiscordTradeService.ParseBrowserCaptureJson(SourceUrl, "{" & PrivateBodyMarker, SyntheticCaptureId, 10)
            Throw New Exception("Malformed capture JSON was accepted")
        Catch ex As DiscordTradeException
            Check(Not ex.Message.Contains(PrivateBodyMarker, StringComparison.Ordinal) AndAlso Not ex.Message.Contains(SyntheticCaptureId, StringComparison.Ordinal), "Malformed JSON leaked raw capture content")
        End Try
    End Sub

    Private Async Function TestLoopbackCaptureAsync() As Task
        Dim accepted As DiscordTradeSnapshot = Nothing
        Dim received As Integer
        Using server As New DiscordBrowserCaptureServer(SourceUrl, 10, Function(snapshot)
                                                                         received += 1
                                                                         accepted = snapshot
                                                                         Return Task.FromResult(True)
                                                                     End Function)
            server.Start()
            Check(server.IsRunning, "Loopback receiver did not start")
            Dim endpoint As New Uri(server.ReceiverUrl)
            Check(endpoint.Scheme = "http" AndAlso endpoint.Host = "127.0.0.1" AndAlso endpoint.Port > 0, "Receiver is not isolated to IPv4 loopback")
            Check(server.CaptureId.Length = 64 AndAlso server.CaptureId.All(Function(character) "0123456789abcdefABCDEF".Contains(character)), "Capture capability is not a 256-bit hex value")
            Using connection = JsonDocument.Parse(server.ConnectionJson)
                Check(connection.RootElement.GetProperty("receiverUrl").GetString() = server.ReceiverUrl AndAlso server.ReceiverUrl.Contains(server.CaptureId, StringComparison.Ordinal), "Connection capability differs from receiver session")
                Check(connection.RootElement.GetProperty("sourceUrl").GetString() = SourceUrl AndAlso connection.RootElement.GetProperty("messageLimit").GetInt32() = 10, "Connection lost its exact source and requested count")
                Check(Not server.ConnectionJson.Contains("Authorization", StringComparison.OrdinalIgnoreCase) AndAlso Not server.ConnectionJson.Contains("botToken", StringComparison.OrdinalIgnoreCase) AndAlso Not server.ConnectionJson.Contains("cookies", StringComparison.OrdinalIgnoreCase), "Connection exported Discord credentials")
            End Using
            Using client As New HttpClient(New HttpClientHandler With {.AllowAutoRedirect = False}) With {.Timeout = TimeSpan.FromSeconds(10)}
                Using state = Await client.GetAsync(server.ReceiverUrl & "state")
                    Check(state.StatusCode = HttpStatusCode.OK, "Receiver state request failed")
                    Dim body = Await state.Content.ReadAsStringAsync()
                    Using status = JsonDocument.Parse(body)
                        Check(status.RootElement.GetProperty("active").GetBoolean() AndAlso status.RootElement.GetProperty("messageLimit").GetInt32() = 10 AndAlso status.RootElement.GetProperty("sourceChannelId").GetString() = ChannelId, "Receiver state does not describe its exact capture")
                    End Using
                    Check(Not body.Contains(server.CaptureId, StringComparison.Ordinal), "State response exposed its capability")
                End Using
                Using request As New HttpRequestMessage(HttpMethod.Get, server.ReceiverUrl & "state")
                    request.Headers.TryAddWithoutValidation("Origin", "https://discord.com")
                    Using response = Await client.SendAsync(request)
                        Check(response.StatusCode <> HttpStatusCode.OK, "Ordinary web origin accessed the capture receiver")
                    End Using
                End Using
                Dim wrongUrl = server.ReceiverUrl.Replace(server.CaptureId, New String("f"c, 64), StringComparison.Ordinal)
                Check(wrongUrl <> server.ReceiverUrl, "Receiver URL does not contain its per-session capability")
                Using response = Await client.GetAsync(wrongUrl & "state")
                    Check(response.StatusCode = HttpStatusCode.NotFound, "Wrong capture capability reached state")
                End Using
                Using response = Await client.GetAsync(server.ReceiverUrl & "capture")
                    Check(response.StatusCode = HttpStatusCode.MethodNotAllowed, "Capture route accepted a GET")
                End Using
                Using response = Await client.GetAsync(server.ReceiverUrl & "state?ignored=true")
                    Check(response.StatusCode = HttpStatusCode.NotFound, "Capture receiver accepted a changed capability URL")
                End Using
                Using request As New HttpRequestMessage(HttpMethod.Get, server.ReceiverUrl & "state")
                    request.Headers.Host = "unrelated.example:" & endpoint.Port.ToString(CultureInfo.InvariantCulture)
                    Using response = Await client.SendAsync(request)
                        Check(response.StatusCode = HttpStatusCode.Forbidden, "Loopback receiver accepted a non-loopback Host")
                    End Using
                End Using
                Using request As New HttpRequestMessage(HttpMethod.Post, server.ReceiverUrl & "capture")
                    request.Headers.TryAddWithoutValidation("Origin", "chrome-extension://" & New String("a"c, 32) & ".evil.example")
                    request.Content = JsonContent(JsonSerializer.Serialize(Envelope(server.CaptureId, 10, {Message(1, "seller", "SELL ROR")}, False)))
                    Using response = Await client.SendAsync(request)
                        Check(response.StatusCode = HttpStatusCode.Forbidden AndAlso received = 0, "Lookalike extension origin applied capture content")
                    End Using
                End Using
                Await CheckOversizeHeaderAsync(endpoint)
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent("{" & PrivateBodyMarker))
                    Check(response.StatusCode = HttpStatusCode.BadRequest AndAlso received = 0, "Malformed capture reached callback")
                    Check(Not (Await response.Content.ReadAsStringAsync()).Contains(PrivateBodyMarker, StringComparison.Ordinal), "HTTP error leaked submitted payload")
                End Using
                Dim incorrect = Envelope(server.CaptureId, 10, {Message(1, "seller", PrivateBodyMarker)}, False)
                incorrect("sourceChannelId") = "1486808135883555038"
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(JsonSerializer.Serialize(incorrect)))
                    Check(response.StatusCode = HttpStatusCode.BadRequest AndAlso received = 0, "Wrong source channel reached callback")
                End Using
                Dim payload = JsonSerializer.Serialize(Envelope(server.CaptureId, 10, {Message(1, "CaseSeller", "SELL ROR")}, False))
                Using request As New HttpRequestMessage(HttpMethod.Post, server.ReceiverUrl & "capture")
                    request.Headers.TryAddWithoutValidation("Origin", "chrome-extension://" & New String("a"c, 32))
                    request.Content = JsonContent(payload)
                    Using response = Await client.SendAsync(request)
                        Check(response.StatusCode = HttpStatusCode.OK AndAlso received = 1 AndAlso accepted IsNot Nothing AndAlso accepted.Messages.Single().AuthorName = "CaseSeller", "Valid extension capture was not delivered once")
                        Check(Not response.Headers.Contains("Authorization") AndAlso Not (Await response.Content.ReadAsStringAsync()).Contains(server.CaptureId, StringComparison.Ordinal), "Capture response leaked credentials/capability")
                    End Using
                End Using
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(payload))
                    Check(response.StatusCode = HttpStatusCode.Conflict AndAlso received = 1, "Repeated final capture was delivered twice")
                End Using
            End Using
            server.Stop()
            Check(Not server.IsRunning, "Stop did not synchronously invalidate receiver session")
        End Using
    End Function

    Private Async Function TestPauseRejectAndCallbackFailuresAsync() As Task
        Dim callbacks As Integer
        Using server As New DiscordBrowserCaptureServer(SourceUrl, 10, Function(snapshot)
                                                                         callbacks += 1
                                                                         Return Task.FromResult(False)
                                                                     End Function)
            server.Start()
            Using client As New HttpClient With {.Timeout = TimeSpan.FromSeconds(10)}
                Dim payload = JsonSerializer.Serialize(Envelope(server.CaptureId, 10, {Message(1, "seller", "SELL ROR")}, False))
                server.Paused = True
                Using response = Await client.GetAsync(server.ReceiverUrl & "state")
                    Using state = JsonDocument.Parse(Await response.Content.ReadAsStringAsync())
                        Check(state.RootElement.GetProperty("paused").GetBoolean(), "Receiver state failed to expose its paused status")
                    End Using
                End Using
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(payload))
                    Check(response.StatusCode = HttpStatusCode.Conflict AndAlso callbacks = 0, "Paused receiver applied posts")
                End Using
                server.Paused = False
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(payload))
                    Check(response.StatusCode = HttpStatusCode.Conflict AndAlso callbacks = 1, "UI rejection was reported as a successful import")
                End Using
                server.Stop()
                Check(Not server.IsRunning, "Stopped rejected session stayed active")
                Try
                    Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(payload))
                        Check(response.StatusCode <> HttpStatusCode.OK AndAlso callbacks = 1, "Stopped receiver delivered a late capture")
                    End Using
                Catch ex As HttpRequestException
                    Check(callbacks = 1, "Stopped-port request reached callback")
                End Try
            End Using
        End Using
        Using server As New DiscordBrowserCaptureServer(SourceUrl, 10, Function(snapshot) Task.FromException(Of Boolean)(New InvalidOperationException(PrivateBodyMarker)))
            server.Start()
            Using client As New HttpClient With {.Timeout = TimeSpan.FromSeconds(10)}
                Using response = Await client.PostAsync(server.ReceiverUrl & "capture", JsonContent(JsonSerializer.Serialize(Envelope(server.CaptureId, 10, {Message(1, "seller", "SELL ROR")}, False))))
                    Check(response.StatusCode = HttpStatusCode.InternalServerError AndAlso Not (Await response.Content.ReadAsStringAsync()).Contains(PrivateBodyMarker, StringComparison.Ordinal), "Callback failure exposed its raw exception")
                End Using
            End Using
        End Using
        Using server As New DiscordBrowserCaptureServer(SourceUrl, 10, Function(snapshot) Task.FromResult(True))
            server.Start()
            GetType(DiscordBrowserCaptureServer).GetField("expiresAt", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(server, DateTimeOffset.UtcNow.AddSeconds(-1))
            Check(Not server.IsRunning, "Expired capture capability remained active")
            Using client As New HttpClient With {.Timeout = TimeSpan.FromSeconds(10)}
                Using response = Await client.GetAsync(server.ReceiverUrl & "state")
                    Check(response.StatusCode = HttpStatusCode.Gone, "Expired session did not reject browser requests")
                End Using
            End Using
        End Using
    End Function

    Private Async Function CheckOversizeHeaderAsync(endpoint As Uri) As Task
        ' Send only a header declaring the oversize body: this verifies the
        ' receiver rejects it without allocating/transferring 64 MB of data.
        Using cancellation As New CancellationTokenSource(TimeSpan.FromSeconds(10))
            Using client As New TcpClient()
                Await client.ConnectAsync(IPAddress.Loopback, endpoint.Port, cancellation.Token)
                Using stream = client.GetStream()
                    Dim header = "POST " & endpoint.AbsolutePath & "capture HTTP/1.1" & vbCrLf &
                        "Host: 127.0.0.1:" & endpoint.Port.ToString(CultureInfo.InvariantCulture) & vbCrLf &
                        "Content-Type: application/json" & vbCrLf &
                        "Content-Length: " & (CLng(DiscordTradeService.MaximumBrowserCaptureBytes) + 1).ToString(CultureInfo.InvariantCulture) & vbCrLf &
                        "Connection: close" & vbCrLf & vbCrLf
                    Await stream.WriteAsync(Encoding.ASCII.GetBytes(header).AsMemory(), cancellation.Token)
                    Dim response(511) As Byte
                    Dim length = Await stream.ReadAsync(response.AsMemory(), cancellation.Token)
                    Check(Encoding.ASCII.GetString(response, 0, length).StartsWith("HTTP/1.1 413", StringComparison.Ordinal), "Oversize Content-Length was not rejected before reading its body")
                End Using
            End Using
        End Using
    End Function

    Private Function Envelope(captureId As String, count As Integer, messages As IEnumerable(Of Dictionary(Of String, Object)), exhausted As Boolean) As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {{"format", "KathanaTradeCapture"}, {"version", 1}, {"captureId", captureId}, {"sourceGuildId", GuildId}, {"sourceChannelId", ChannelId}, {"requestedMessageCount", count}, {"historyExhausted", exhausted}, {"messages", messages.ToArray()}}
    End Function

    Private Function Message(number As Integer, author As String, body As String) As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {{"id", MessageId(number)}, {"channel_id", ChannelId}, {"type", 0}, {"flags", 0}, {"content", body}, {"timestamp", "2026-10-05T23:21:54.100000+00:00"}, {"edited_timestamp", Nothing}, {"author", New With {.id = "1486810317966282916", .username = author, .bot = True}}, {"embeds", Array.Empty(Of Object)()}, {"attachments", Array.Empty(Of Object)()}}
    End Function

    Private Function MessageId(number As Integer) As String
        Return (1556800000000000000UL + CULng(number)).ToString(CultureInfo.InvariantCulture)
    End Function

    Private Function JsonContent(json As String) As StringContent
        Return New StringContent(json, Encoding.UTF8, "application/json")
    End Function

    Private Sub ExpectParserFailure(payload As Dictionary(Of String, Object), Optional expectedCount As Integer = 10, Optional source As String = SourceUrl)
        Try
            DiscordTradeService.ParseBrowserCaptureJson(source, JsonSerializer.Serialize(payload), SyntheticCaptureId, expectedCount)
            Throw New Exception("Invalid browser capture was accepted")
        Catch ex As DiscordTradeException
            Check(Not ex.Message.Contains(PrivateBodyMarker, StringComparison.Ordinal) AndAlso Not ex.Message.Contains(SyntheticCaptureId, StringComparison.Ordinal), "Capture failure leaked a private body or capability")
        End Try
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub
End Module
