Imports System.Net.Http
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Text.Json
Imports System.Reflection

Module Program
    <STAThread>
    Sub Main()
        TestAiExtraction().GetAwaiter().GetResult()
        TestTradePrices()
        Dim example = "PulgaAPP9/14/2026 10:24 PM" & vbLf & "**SELL ROR YASKA-- SELL LIFE KAKANA 5M --S= VOUCHER 1.7KK- S=ASBA RING 2.5"
        Dim customMessage = "Hi! I would like to buy yy3. how much?"
        Dim customListings As New List(Of TradeListing) From {
            New TradeListing With {.CharacterName = "IIAKISHAII", .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3"},
            New TradeListing With {.CharacterName = "Ambo", .Intent = "sell", .ItemKey = "YY3", .ItemText = "YY3"},
            New TradeListing With {.CharacterName = "Buyer", .Intent = "buy", .ItemKey = "YY3", .ItemText = "YY3"}}
        Dim customQueue = TradeAiService.BuildQueue(customListings, True, {"YY3"}, customMessage)
        Check(customQueue.Select(Function(row) row.CharacterName).SequenceEqual({"IIAKISHAII", "Ambo"}), "Custom message lost exact-case sellers")
        Check(customQueue.All(Function(row) row.Message = customMessage), "Literal custom message was changed")
        Check(TradeAiService.BuildQueue(customListings, True, {"YY3"}, "Buy {items}").All(Function(row) row.Message = "Buy YY3"), "Optional item substitution broke")
        Check(TradeService.ParsePosts("Ambo", "YY3", True, customMessage).Single().Message = customMessage, "Manual-name parser rejects literal message")
        Dim parsed = TradeService.ParsePosts(example, "ROR YAKSA|ROR YASKA, VOUCHER", True, TradeService.BuyTemplate)
        Check(parsed.Count = 1 AndAlso parsed(0).CharacterName = "Pulga", "APP/date header or original capitalization failed")
        Check(parsed(0).Items = "ROR YAKSA, VOUCHER", "Multiple clauses or alias matching failed")
        Check(TradeService.BuildWhisper(parsed(0).CharacterName, parsed(0).Message) = "/whisper Pulga Hi! I want to buy ROR YAKSA, VOUCHER. Can you offer a good price?", "Exact command mismatch")
        Dim mixed = example & vbLf & "BuyerBOT9/15/2026 1:00 PM" & vbLf & "BUY ROR YASKA -- SELL LIFE KAKANA" & vbLf & "PulgaAPP9/16/2026 1:00 PM" & vbLf & "S= VOUCHER"
        Check(TradeService.ParsePosts(mixed, "ROR YASKA, VOUCHER", True, TradeService.BuyTemplate).Count = 1, "Buying included a buyer or duplicated a character")
        Dim sellers = TradeService.ParsePosts(mixed, "ROR YAKSA|ROR YASKA", False, TradeService.SellTemplate)
        Check(sellers.Count = 1 AndAlso sellers(0).CharacterName = "Buyer", "Sell-to-buyers matching failed")
        Dim names = TradeService.ParsePosts("Pulga" & vbLf & "pulga" & vbLf & "Pulga", "ROR", True, TradeService.BuyTemplate)
        Check(names.Count = 2 AndAlso names(1).CharacterName = "pulga", "Case-sensitive exact-name deduplication failed")
        Dim splitHeader = TradeService.ParsePosts("MiXeD" & vbLf & "APP9/14/2026 10:24 PM" & vbLf & "SELL ROR", "ROR", True, TradeService.BuyTemplate)
        Check(splitHeader.Count = 1 AndAlso splitHeader(0).CharacterName = "MiXeD", "Separate name/timestamp header failed")
        Check(TradeService.ParsePosts(example, "NOT LISTED", True, TradeService.BuyTemplate).Count = 0, "Unrelated listing matched")
        For Each invalid In {"Bad Name", "Name" & vbLf & "/command", ""}
            Dim rejected = False
            Try
                TradeService.BuildWhisper(invalid, "Hi")
            Catch ex As ArgumentException
                rejected = True
            End Try
            Check(rejected, "Invalid recipient accepted")
        Next
        Dim settings As New TradeSettings With {.DiscordText = example, .Buying = False, .DelaySeconds = 12, .MessageTemplate = "Selling {items}", .Recipients = parsed}
        Dim restored = JsonSerializer.Deserialize(Of TradeSettings)(JsonSerializer.Serialize(settings))
        Check(restored.DiscordText = example AndAlso restored.Recipients(0).CharacterName = "Pulga" AndAlso restored.DelaySeconds = 12 AndAlso Not restored.Buying, "Profile roundtrip failed")
        settings.ExtractedListings = FixtureListings()
        settings.SelectedItemKeys = New List(Of String) From {"ASBA RING"}
        settings.PriceOffers = TradePriceService.Parse("Pulga: ASBA RING for 1.7kk" & vbLf & "BuyerTwo: ASBA RING for 900k", New List(Of TradeRecipient) From {
            New TradeRecipient With {.CharacterName = "Pulga", .Items = "ASBA RING"}, New TradeRecipient With {.CharacterName = "BuyerTwo", .Items = "ASBA RING"}})
        TestUiPersistence(settings)
        Using window As New ChatProbe(), cancellation As New CancellationTokenSource()
            window.StartPosition = FormStartPosition.Manual
            window.Location = New System.Drawing.Point(-30000, -30000)
            window.Show()
            Application.DoEvents()
            Dim hwnd = window.Handle
            Dim pid = CUInt(Environment.ProcessId)
            Dim command = TradeService.BuildWhisper("PuLgA", "Hi! ROR YAKSA? Good price, please.")
            Dim sending = Task.Run(Function() TradeService.SendWhisper(hwnd, pid, "PuLgA", "Hi! ROR YAKSA? Good price, please.", cancellation.Token, Function() hwnd))
            Pump(sending)
            Check(sending.Result AndAlso window.Messages.SequenceEqual({command}), "Posted whisper lost case, punctuation, or Enter sequencing")
            Dim second = Task.Run(Function() TradeService.SendWhisper(hwnd, pid, "Second", "Hi", cancellation.Token, Function() hwnd))
            Pump(second)
            Check(second.Result AndAlso window.Messages.Count = 2 AndAlso window.Messages(1) = "/whisper Second Hi", "Sequential recipient delivery failed")
            Dim unfocused = Task.Run(
                Function()
                    Try
                        TradeService.SendWhisper(hwnd, pid, "NoFocus", "Must not send", cancellation.Token, Function() IntPtr.Zero)
                        Return False
                    Catch ex As InvalidOperationException
                        Return ex.Message.Contains("lost focus")
                    End Try
                End Function)
            Pump(unfocused)
            Check(unfocused.Result AndAlso window.Messages.Count = 2 AndAlso Not window.ChatOpen, "Focus failure did not stop before opening chat")
            Using stopSource As New CancellationTokenSource(1600)
                Dim stopped = Task.Run(Function() TradeService.SendWhisper(hwnd, pid, "CancelMe", New String("x"c, 120), stopSource.Token, Function() hwnd), stopSource.Token)
                Pump(stopped)
                Check(stopped.IsCanceled AndAlso window.Messages.Count = 2 AndAlso Not window.ChatOpen, "Cancellation submitted partial text or left chat open")
            End Using
            Console.WriteLine("PASS: mock-window whispers preserve exact case/punctuation, Enter/text/Enter order, sequential recipients and cancellation without partial submission.")
        End Using
        Console.WriteLine("PASS: Discord APP/BOT and split headers, item aliases, buy/sell filtering, exact-case deduplication, validation and profile/UI roundtrips.")
    End Sub

    Private Sub TestTradePrices()
        Dim recipients As New List(Of TradeRecipient) From {
            New TradeRecipient With {.CharacterName = "Pulga", .Items = "ASBA RING"},
            New TradeRecipient With {.CharacterName = "Multi", .Items = "ASBA RING, ROR"}}
        Dim offers = TradePriceService.Parse("Pulga: 1.7kk" & vbLf & "Other: selling for 500k" & vbLf & "Multi: 2m" & vbLf & "Pulga: $5" & vbLf & "Pulga: level 90" & vbLf & "Multi: ASBA RING for 1,500,000", recipients)
        Check(offers.Count = 4, "Price parsing included a level/real currency or missed a quote")
        Check(offers(0).Price = 1700000D AndAlso offers(0).Item = "ASBA RING", "Single-item reply association or kk normalization failed")
        Check(offers(2).Item.StartsWith("Unknown"), "Ambiguous multi-item reply was assigned an item")
        Check(offers(3).Price = 1500000D AndAlso offers(3).Item = "ASBA RING", "Explicit item/grouped amount failed")
        Dim merged As New List(Of TradePriceOffer)
        TradePriceService.Merge(merged, offers)
        TradePriceService.Merge(merged, offers)
        Check(merged.Count = 4 AndAlso merged(0).Price = 500000D, "Repeated scans duplicated offers or sorting was lexical")
        TradePriceService.Merge(merged, TradePriceService.Parse("Pulga: 400k", recipients))
        Check(merged.Count = 4 AndAlso merged(0).CharacterName = "Pulga" AndAlso merged(0).Price = 400000D, "Revised offer failed to replace/re-rank")
        TradePriceService.Merge(merged, TradePriceService.Parse("", recipients))
        Check(merged.Count = 4, "Empty scan erased offers")
        Console.WriteLine("PASS: chat price parsing, currency normalization, item ambiguity, repeated scans, price revisions and numeric ranking.")
    End Sub

    Private Const Posts As String = "complex" & vbLf & "APP" & vbLf & "? 9/14/2026 11:08 PM" & vbLf &
        "B> Asba Ring, Demi Mantra III (heart), ROR for DEVAxSAITAMAx" & vbLf &
        "mando1545" & vbLf & "Online" & vbLf & "asba ring" & vbLf &
        "Azshahadin" & vbLf & "APP" & vbLf & "? 9/15/2026 10:20 AM" & vbLf &
        "T> ROTD1 or ROR1 = NOTD1 // B> DM1 DM2 YY1 YY2" & vbLf &
        "BuyerTwoAPP9/16/2026 1:00 PM" & vbLf & "BUY ASBA RING" & vbLf &
        "SellerThreeAPP9/16/2026 2:00 PM" & vbLf & "SELL ASBA RING"

    Private Function FixtureListings() As List(Of TradeListing)
        Return New List(Of TradeListing) From {
            New TradeListing With {.CharacterName = "complex", .Intent = "buy", .ItemText = "Asba Ring", .ItemKey = "ASBA RING", .Evidence = "B> Asba Ring, Demi Mantra III (heart), ROR for DEVAxSAITAMAx"},
            New TradeListing With {.CharacterName = "BuyerTwo", .Intent = "buy", .ItemText = "ASBA RING", .ItemKey = "ASBA RING", .Evidence = "BUY ASBA RING"},
            New TradeListing With {.CharacterName = "Azshahadin", .Intent = "buy", .ItemText = "DM1", .ItemKey = "DM1", .Evidence = "B> DM1 DM2 YY1 YY2"},
            New TradeListing With {.CharacterName = "SellerThree", .Intent = "sell", .ItemText = "ASBA RING", .ItemKey = "ASBA RING", .Evidence = "SELL ASBA RING"}}
    End Function

    Private Function ApiResponse(listings As List(Of TradeListing)) As String
        Return JsonSerializer.Serialize(New With {.status = "completed", .output = New Object() {
            New With {.type = "message", .role = "assistant", .content = New Object() {
                New With {.type = "output_text", .text = JsonSerializer.Serialize(New TradeExtraction With {.Listings = listings})}}}}})
    End Function

    Private Async Function TestAiExtraction() As Task
        Dim fixture = FixtureListings()
        fixture.Add(fixture(0)) ' Reposts must not increase an item's popularity.
        Using transport As New HttpClient(New AiHandler(ApiResponse(fixture)))
            Dim result = Await TradeAiService.AnalyzeAsync(Posts, "test-key", "gpt-5.4-mini", CancellationToken.None, transport)
            Dim ranked = TradeAiService.RankItems(result, False)
            Check(ranked(0).ItemKey = "ASBA RING" AndAlso ranked(0).Characters = 2, "Popularity must count distinct buyers, not reposts or sellers")
            Dim rows = TradeAiService.BuildQueue(result, False, ranked.Take(3).Select(Function(item) item.ItemKey), TradeService.SellTemplate)
            Check(rows.Count = 3 AndAlso rows(0).Items = "Asba Ring", "Automatic queue requires no filter and preserves author item spelling")
            Check(rows.All(Function(row) row.CharacterName <> "mando1545" AndAlso row.CharacterName <> "SellerThree"), "Chatter or wrong-side listings entered the queue")
            Check(TradeAiService.RankItems(result, True).Single().Characters = 1, "Buy/sell ranking leaked across intent")
        End Using
        Dim invalid = FixtureListings()
        invalid.Add(New TradeListing With {.CharacterName = "COMPLEX", .Intent = "buy", .ItemText = "Asba Ring", .ItemKey = "ASBA RING", .Evidence = "B> Asba Ring"})
        invalid.Add(New TradeListing With {.CharacterName = "complex", .Intent = "buy", .ItemText = "INVENTED", .ItemKey = "INVENTED", .Evidence = "BUY INVENTED"})
        Check(TradeAiService.ValidateListings(invalid, Posts).Count = 4, "Changed-case names or invented items were accepted")
        Using transport As New HttpClient(New AiHandler("{}", HttpStatusCode.Unauthorized))
            Dim rejected = False
            Try
                Await TradeAiService.AnalyzeAsync(Posts, "test-key", "gpt-5.4-mini", CancellationToken.None, transport)
            Catch ex As InvalidOperationException
                rejected = ex.Message.Contains("key")
            End Try
            Check(rejected, "API authorization error was not actionable")
        End Using
        Dim incomplete = False
        Try
            TradeAiService.ParseResponse("{""status"":""incomplete"",""output"":[]}", Posts)
        Catch ex As InvalidOperationException
            incomplete = True
        End Try
        Check(incomplete, "Incomplete AI response created a partial queue")
        Dim screenshotRows = TradeService.ParsePosts(Posts, "ASBA", False, TradeService.SellTemplate)
        Check(screenshotRows.Any(Function(row) row.CharacterName = "complex"), "Screenshot's separate APP/date and B> format still fails")
        If Environment.GetCommandLineArgs().Contains("--live-ai") Then Await LiveAiCheck()
        Console.WriteLine("PASS: structured AI request/response, no-filter automatic ranking, distinct-character frequencies, Buy/Sell isolation, source evidence/case validation, API errors, incomplete responses and screenshot format.")
    End Function

    Private Async Function LiveAiCheck() As Task
        Dim key = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        Dim model = "gpt-5.4-mini"
        If String.IsNullOrWhiteSpace(key) Then
            Dim path = CStr(GetType(Form1).GetField("PersistFilePath", BindingFlags.NonPublic Or BindingFlags.Static).GetValue(Nothing))
            If IO.File.Exists(path) Then
                Using document = JsonDocument.Parse(IO.File.ReadAllText(path))
                    Dim quiz As JsonElement, encrypted As JsonElement, savedModel As JsonElement
                    If document.RootElement.TryGetProperty("Quiz", quiz) AndAlso quiz.TryGetProperty("EncryptedApiKey", encrypted) Then
                        Dim secretType = GetType(Form1).Assembly.GetType("KathanaBotControlPanel.QuizSecretStore")
                        key = CStr(secretType.GetMethod("Unprotect").Invoke(Nothing, {encrypted.GetString()}))
                        If quiz.TryGetProperty("Model", savedModel) AndAlso Not String.IsNullOrWhiteSpace(savedModel.GetString()) Then model = savedModel.GetString()
                    End If
                End Using
            End If
        End If
        If String.IsNullOrWhiteSpace(key) Then
            Console.WriteLine("SKIP live AI: no configured API key; offline HTTP integration tests passed.")
            Return
        End If
        Dim listings = Await TradeAiService.AnalyzeAsync(Posts, key, model, CancellationToken.None)
        Check(listings.Any(Function(entry) entry.CharacterName = "complex" AndAlso entry.ItemText.Contains("Asba", StringComparison.OrdinalIgnoreCase)), "Live AI missed screenshot's Asba buyer")
        Check(listings.Any(Function(entry) entry.CharacterName = "Azshahadin" AndAlso entry.ItemText = "DM1"), "Live AI missed separate DM1 buy clause")
        Check(Not listings.Any(Function(entry) entry.CharacterName = "mando1545"), "Live AI treated chatter as a trade listing")
        Check(TradeAiService.RankItems(listings, False).First().Characters = 2, "Live AI common-item ranking failed")
        Console.WriteLine("PASS: live AI extracted the screenshot-format fixture, split item codes, ignored chatter and ranked the shared item first.")
    End Function

    Private Class AiHandler
        Inherits HttpMessageHandler
        Private ReadOnly payload As String
        Private ReadOnly status As HttpStatusCode
        Public Sub New(payload As String, Optional status As HttpStatusCode = HttpStatusCode.OK)
            Me.payload = payload
            Me.status = status
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, token As CancellationToken) As Task(Of HttpResponseMessage)
            Check(request.RequestUri.AbsoluteUri = "https://api.openai.com/v1/responses", "Wrong AI endpoint")
            Using body = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(token))
                Check(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "AI extraction must use a strict schema")
                Check(Not body.RootElement.GetProperty("store").GetBoolean(), "Trade extraction enabled response storage")
                Check(body.RootElement.GetProperty("input")(0).GetProperty("content").GetString() = Posts, "Pasted posts were altered")
            End Using
            Return New HttpResponseMessage(status) With {.Content = New StringContent(payload, Encoding.UTF8, "application/json")}
        End Function
    End Class

    Private Sub TestUiPersistence(settings As TradeSettings)
        Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim formType = GetType(Form1)
        Dim form = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(formType)
        Using timer As New System.Windows.Forms.Timer()
            formType.GetField("_tradeStopTimer", flags).SetValue(form, timer)
            Dim page = DirectCast(formType.GetMethod("BuildTradeTab", flags).Invoke(form, Nothing), TabPage)
            Using page
                formType.GetMethod("ApplyPersistedTradeState", flags).Invoke(form, {settings})
                Dim saved = DirectCast(formType.GetMethod("BuildPersistedTradeState", flags).Invoke(form, Nothing), TradeSettings)
                Check(JsonSerializer.Serialize(saved) = JsonSerializer.Serialize(settings), "Actual Trade UI omitted saved inputs")
                Dim search = DirectCast(formType.GetField("_tradeItemSearch", flags).GetValue(form), TextBox)
                Dim items = DirectCast(formType.GetField("_tradeDetectedItems", flags).GetValue(form), CheckedListBox)
                search.Text = "dm"
                Check(items.Items.Count = 1 AndAlso DirectCast(items.Items(0), TradeItemFrequency).ItemKey = "DM1", "Item search did not match partial names without case sensitivity")
                items.SetItemChecked(0, True)
                search.Text = "no matching item"
                saved = DirectCast(formType.GetMethod("BuildPersistedTradeState", flags).Invoke(form, Nothing), TradeSettings)
                Check(items.Items.Count = 0 AndAlso saved.SelectedItemKeys.Count = 2 AndAlso saved.ItemSearch = search.Text, "Search lost hidden selections or was not persisted")
                formType.GetMethod("ApplyPersistedTradeState", flags).Invoke(form, {saved})
                search.Clear()
                Check(items.CheckedItems.Count = 2, "Clearing a restored search lost selected items")
                formType.GetMethod("ApplyPersistedTradeState", flags).Invoke(form, {settings})
                formType.GetMethod("ApplyDarkTheme", flags).Invoke(form, {page})
                Using host As New Form With {.ClientSize = New Drawing.Size(1360, 760), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Drawing.Point(-30000, -30000)}, tabs As New TabControl With {.Dock = DockStyle.Fill}
                    host.Controls.Add(tabs)
                    tabs.TabPages.Add(page)
                    host.Show()
                    Application.DoEvents()
                    host.CreateControl()
                    tabs.CreateControl()
                    page.CreateControl()
                    host.PerformLayout()
                    tabs.PerformLayout()
                    page.PerformLayout()
                    Dim startButton = DirectCast(formType.GetField("_tradeStart", flags).GetValue(form), Button)
                    Dim priceGrid = DirectCast(formType.GetField("_tradePriceGrid", flags).GetValue(form), DataGridView)
                    For Each control As Control In New Control() {items, startButton, priceGrid}
                        Dim bounds = host.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))
                        Check(host.ClientRectangle.Contains(bounds), "Trade control outside the single-view layout: " & control.GetType().Name)
                    Next
                    Check(items.Height >= items.ItemHeight * 10 + 4, "Detected item list cannot show ten rows")
                    Check(Not DirectCast(page.Controls(0), Panel).VerticalScroll.Visible, "Trade page requires vertical scrolling")
                    Using bitmap As New Drawing.Bitmap(host.ClientSize.Width, host.ClientSize.Height)
                        host.DrawToBitmap(bitmap, host.ClientRectangle)
                        bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "trade-tab.png"))
                    End Using
                    tabs.TabPages.Remove(page)
                End Using
            End Using
        End Using
    End Sub

    Private Sub Pump(work As Task)
        Dim timeout = DateTime.UtcNow.AddSeconds(15)
        While Not work.IsCompleted AndAlso DateTime.UtcNow < timeout
            Application.DoEvents()
            Thread.Sleep(5)
        End While
        Application.DoEvents()
        Check(work.IsCompleted, "Mock send timed out")
        If work.IsFaulted Then Throw work.Exception
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Class ChatProbe
        Inherits Form
        Public ReadOnly Messages As New List(Of String)()
        Public ChatOpen As Boolean
        Private buffer As String = ""
        Private enterDownAt As Long
        Private readyAt As Long
        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = &H100 Then
                If m.WParam.ToInt32() = CInt(Keys.Enter) Then
                    enterDownAt = Environment.TickCount64
                ElseIf m.WParam.ToInt32() = CInt(Keys.Escape) Then
                    ChatOpen = False
                    buffer = ""
                End If
                Return
            ElseIf m.Msg = &H101 AndAlso m.WParam.ToInt32() = CInt(Keys.Enter) Then
                ' Simulate a game that ignores short presses and needs time to open chat.
                If Environment.TickCount64 - enterDownAt >= 80 Then
                    If ChatOpen Then Messages.Add(buffer)
                    ChatOpen = Not ChatOpen
                    readyAt = Environment.TickCount64 + 350
                    buffer = ""
                End If
                Return
            ElseIf m.Msg = &H102 Then
                If ChatOpen AndAlso Environment.TickCount64 >= readyAt AndAlso Not Char.IsControl(ChrW(m.WParam.ToInt32())) Then buffer &= ChrW(m.WParam.ToInt32())
                Return
            End If
            MyBase.WndProc(m)
        End Sub
    End Class
End Module
