Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private _tradeTab As TabPage
    Private _tradeSource As TextBox
    Private _tradeDetectedItems As CheckedListBox
    Private _tradeItemSearch As TextBox
    Private _tradeSelectAllMatches As Button
    Private _tradeUnselectAll As Button
    Private _tradeChosenItems As HashSet(Of String)
    Private _tradeExtracted As New List(Of TradeListing)()
    Private _tradeAnalyzing As Boolean
    Private _tradeAnalysisCancellation As CancellationTokenSource
    Private _tradeAnalysisGeneration As Integer
    Private _tradeAnalysisPostCount As Integer
    Private _tradeAnalysisSourcePosts As List(Of DiscordTradeMessage)
    Private _tradeAnalysisSourceText As String
    Private _tradeMode As ComboBox
    Private _tradeTemplate As TextBox
    Private _tradeDelay As NumericUpDown
    Private _tradeGrid As DataGridView
    Private _tradeOptions As Control
    Private _tradeStart As Button
    Private _tradeStatus As Label
    Private _tradeCancellation As CancellationTokenSource
    Private _tradeRunning As Boolean
    Private _tradeLoading As Boolean
    Private _tradeForegroundMaintenanceWindow As IntPtr
    Private _tradeForegroundMaintenancePid As UInteger
    Private _tradeForegroundMaintenanceAllowed As Boolean
    Private ReadOnly _tradeStopTimer As New System.Windows.Forms.Timer With {.Interval = 50}

    Private NotInheritable Class TradeAnalysisUiProgress
        Implements IProgress(Of TradeAnalysisProgress)
        Private ReadOnly _display As IProgress(Of TradeAnalysisProgress)
        Private _totalPosts As Integer

        Public Sub New(display As IProgress(Of TradeAnalysisProgress))
            _display = display
        End Sub

        Public ReadOnly Property TotalPosts As Integer
            Get
                Return Volatile.Read(_totalPosts)
            End Get
        End Property

        Public Sub Report(value As TradeAnalysisProgress) Implements IProgress(Of TradeAnalysisProgress).Report
            Volatile.Write(_totalPosts, value.TotalPosts)
            _display.Report(value)
        End Sub
    End Class

    Private Function BuildTradeTab() As TabPage
        Dim tab As New TabPage("Trade") With {.BackColor = ThemeBg}
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = False, .Padding = New Padding(8)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6}
        For Each rowSize As Single In {0, 0, 48, 52, 0, 0}
            body.RowStyles.Add(New RowStyle(If(rowSize = 0, SizeType.AutoSize, SizeType.Percent), rowSize))
        Next
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(New Label With {.Text = "TRADE / DISCORD WHISPERS", .AutoSize = True, .Font = New Font("Segoe UI", 17, FontStyle.Bold), .ForeColor = ThemeAccent})
        body.Controls.Add(New Label With {.Text = "Paste or import posts > Analyze > review checked whispers > Start. Whispers focus the Full game; keep chat closed before Start. Focus restores between messages. Stop / F12 cancels.", .AutoSize = True, .Margin = New Padding(0, 3, 0, 5)})
        Dim options As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1}
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))
        options.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        _tradeOptions = options
        _tradeSource = New TextBox With {.Multiline = True, .ScrollBars = ScrollBars.Vertical, .Dock = DockStyle.Fill, .Height = 180, .MaxLength = DiscordTradeService.MaximumImportedCharacters, .PlaceholderText = "Paste posts or connect Browser capture."}
        _tradeMode = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        _tradeMode.Items.AddRange({"Buy from sellers", "Sell to buyers"})
        _tradeMode.SelectedIndex = 0
        _tradeExtracted = New List(Of TradeListing)()
        _tradeChosenItems = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        _tradeItemSearch = New TextBox With {.Dock = DockStyle.Fill, .PlaceholderText = "Find detected items, e.g. ASBA or ROR (optional)"}
        _tradeSelectAllMatches = New Button With {.Text = "Select all matches", .AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(3, 0, 0, 0), .Enabled = False}
        AddHandler _tradeSelectAllMatches.Click, Sub() SelectAllMatchingTradeItems()
        _tradeUnselectAll = New Button With {.Text = "Unselect all", .AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(3, 0, 0, 0), .Enabled = False}
        AddHandler _tradeUnselectAll.Click, Sub() UnselectAllTradeItems()
        _tradeDetectedItems = New CheckedListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False, .CheckOnClick = True}
        _tradeTemplate = New TextBox With {.Dock = DockStyle.Fill, .Text = TradeService.BuyTemplate, .MaxLength = 220}
        _tradeDelay = New NumericUpDown With {.Minimum = 1, .Maximum = 300, .Value = 5, .Dock = DockStyle.Fill}
        Dim posts As New GroupBox With {.Text = "Discord posts / names", .Dock = DockStyle.Fill, .Padding = New Padding(6)}
        Dim postsBody As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
        postsBody.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        postsBody.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        postsBody.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        postsBody.Controls.Add(InitializeTradeDiscordControls(), 0, 0)
        postsBody.Controls.Add(_tradeSource, 0, 1)
        posts.Controls.Add(postsBody)
        options.Controls.Add(posts, 0, 0)
        Dim items As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        items.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        items.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        items.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        items.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        items.Controls.Add(New Label With {.Text = "Detected items (top 3 auto-selected)", .AutoSize = True}, 0, 0)
        Dim itemFilter As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Margin = New Padding(0)}
        itemFilter.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        itemFilter.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        itemFilter.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        itemFilter.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        itemFilter.Controls.Add(_tradeItemSearch, 0, 0)
        itemFilter.Controls.Add(_tradeSelectAllMatches, 1, 0)
        itemFilter.Controls.Add(_tradeUnselectAll, 2, 0)
        items.Controls.Add(itemFilter, 0, 1)
        items.Controls.Add(_tradeDetectedItems, 0, 2)
        options.Controls.Add(items, 1, 0)
        Dim settings As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 5}
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For i As Integer = 0 To 4
            settings.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        settings.Controls.Add(New Label With {.Text = "I want to", .AutoSize = True}, 0, 0)
        settings.Controls.Add(_tradeMode, 1, 0)
        settings.Controls.Add(New Label With {.Text = "Template", .AutoSize = True}, 0, 1)
        settings.Controls.Add(_tradeTemplate, 1, 1)
        settings.Controls.Add(New Label With {.Text = "Delay (sec)", .AutoSize = True}, 0, 2)
        settings.Controls.Add(_tradeDelay, 1, 2)
        Dim aiKey As New Button With {.Text = "Configure AI key", .AutoSize = True}
        AddHandler aiKey.Click, Sub() ConfigureQuizApiKey()
        Dim parse As New Button With {.Text = "Analyze posts with AI", .AutoSize = True}
        AddHandler parse.Click, AddressOf ParseTradeQueue
        Dim help As New Button With {.Text = "Help", .AutoSize = True}
        AddHandler help.Click, Sub() SilentMessageBox.Show(Me, "Paste character names and messages, or use Browser capture with the local extension and your signed-in Discord web channel. Choose 1 to 10,000 posts in Browser capture, start the local connection and paste its setup into the extension. Capture pauses during analysis, sending, or while checked whispers await review. Review the imported count, then Analyze and review the whisper queue before Start. Discord author names are not verified game character names." & vbCrLf & vbCrLf & "Analyze uses GPT-5 nano with minimal reasoning and the encrypted API key configured in Quiz; the Quiz model setting does not affect Trade. Up to 50 complete posts or 8,000 characters are processed per batch, with up to four requests at once. Completed verified batches are reused during this app session when their exact posts and model are unchanged; cached batches make no API request. Larger imports still take longer. Progress shows processed posts and extracted listings. Sorting, item counts, filtering and queue creation run locally without API charges. There is no automatic fallback to a more expensive model. All batches must complete before results replace your previous reviewed queue; cancellation or errors keep it. Analyze does not send whispers. The app selects the three most common items for Buy/Sell locally, counting distinct characters. Search filters the detected list; checked hidden items stay selected. Select all matches checks visible results; clear the search first to select every item in this mode. Unselect all clears every checked item, including items hidden by the search, and empties the review queue. Repeated posts from one character become one whisper." & vbCrLf & vbCrLf & "{items} inserts matching items per character. Buying matches SELL posts; selling matches BUY posts. Character names are case-sensitive. Edit the queue, then Start whispers (foreground) pauses combat and sends checked rows once in order with the game focused. Between complete messages the queue waits for and restores the same selected game. A focus or input failure while typing stops the queue to protect an unfinished draft; sent rows remain unchecked and are never retried automatically. Stop / F12 cancels; close any unfinished chat draft before retrying unsent rows. After successful completion the previous combat mode resumes (Full if none was running) while its original game window and process remain valid. Running combat then restores that game focus automatically; gameplay input is emitted only while the exact game is foreground." & vbCrLf & vbCrLf & "Chat price scanning uses Regions > chat_rect every 3 seconds. Offers are cheapest first; review OCR quotes. Clear offers removes collected prices.", "Trade Help")
        Dim setupActions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        setupActions.Controls.AddRange({aiKey, parse, help})
        settings.Controls.Add(setupActions, 0, 3)
        settings.SetColumnSpan(setupActions, 2)
        Dim privacy As New Label With {.Text = "AI: GPT-5 nano (lowest cost). Completed results are reused during this app session; sorting and filtering are local. Top 3 items start checked; Select all matches includes more. Review before Start.", .AutoSize = True, .Dock = DockStyle.Fill}
        settings.Controls.Add(privacy, 0, 4)
        settings.SetColumnSpan(privacy, 2)
        options.Controls.Add(settings, 2, 0)
        _tradeGrid = New DataGridView With {.Dock = DockStyle.Top, .Height = 270, .AllowUserToAddRows = True, .AllowUserToDeleteRows = True, .RowHeadersVisible = True, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        _tradeGrid.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Send", .HeaderText = "Send", .FillWeight = 35})
        _tradeGrid.Columns.Add("Character", "Character (exact case)")
        _tradeGrid.Columns.Add("Items", "Items")
        _tradeGrid.Columns.Add("Message", "Whisper message (editable)")
        _tradeGrid.Columns("Message").FillWeight = 230
        _tradeGrid.Columns("Items").ReadOnly = True
        _tradeGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Status", .HeaderText = "Status", .ReadOnly = True, .FillWeight = 65})
        AddHandler _tradeGrid.DefaultValuesNeeded, Sub(sender, e) e.Row.Cells("Send").Value = True
        body.Controls.Add(options)
        body.Controls.Add(BuildTradePriceTables())
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top}
        _tradeStart = New Button With {.Text = "Start whispers (foreground)", .AutoSize = True, .Height = 34}
        AddHandler _tradeStart.Click, AddressOf StartTradeQueue
        Dim stopButton As New Button With {.Text = "Stop / F12", .AutoSize = True, .Height = 34}
        AddHandler stopButton.Click,
            Sub()
                _tradeCancellation?.Cancel()
                _tradeAnalysisCancellation?.Cancel()
                StopTradeBrowserCapture()
                StopTradeDiscordImport()
                _tradePriceScan.Checked = False
            End Sub
        Dim save As New Button With {.Text = "Save settings", .AutoSize = True, .Height = 34}
        AddHandler save.Click, Sub() SavePersistedListState(True)
        actions.Controls.AddRange({_tradeStart, stopButton, save})
        body.Controls.Add(actions)
        _tradeStatus = New Label With {.Text = "Ready. Paste posts and click Analyze posts with AI. No item filter is required.", .AutoSize = True, .MaximumSize = New Size(1050, 0), .ForeColor = ThemeAccent}
        body.Controls.Add(_tradeStatus)
        scroll.Controls.Add(body)
        tab.Controls.Add(scroll)
        AddHandler _tradeMode.SelectedIndexChanged,
            Sub()
                If _tradeLoading Then Return
                If _tradeTemplate.Text = TradeService.BuyTemplate OrElse _tradeTemplate.Text = TradeService.SellTemplate Then
                    _tradeTemplate.Text = If(_tradeMode.SelectedIndex = 0, TradeService.BuyTemplate, TradeService.SellTemplate)
                End If
                PopulateTradeItems(Nothing)
                RebuildAiTradeQueue()
                TradeInputChanged(Me, EventArgs.Empty)
            End Sub
        AddHandler _tradeDetectedItems.ItemCheck,
            Sub(sender, e)
                If _tradeLoading Then Return
                Dim key = DirectCast(_tradeDetectedItems.Items(e.Index), TradeItemFrequency).ItemKey
                If e.NewValue = CheckState.Checked Then
                    _tradeChosenItems.Add(key)
                Else
                    _tradeChosenItems.Remove(key)
                End If
                UpdateTradeSelectionButtons()
                If Not _tradeLoading AndAlso _tradeDetectedItems.IsHandleCreated Then _tradeDetectedItems.BeginInvoke(New Action(Sub() RebuildAiTradeQueue()))
            End Sub
        AddHandler _tradeItemSearch.TextChanged,
            Sub()
                If _tradeLoading Then Return
                PopulateTradeItems(SelectedTradeItems())
                TradeInputChanged(Me, EventArgs.Empty)
            End Sub
        AddHandler _tradeSource.TextChanged,
            Sub()
                If _tradeLoading Then Return
                If Not _tradeCaptureApplying AndAlso _tradeCaptureServer IsNot Nothing Then StopTradeBrowserCapture()
                _tradeAnalysisGeneration += 1
                _tradeAnalysisCancellation?.Cancel()
                _tradeAnalysisPostCount = 0
                _tradeAnalysisSourcePosts = Nothing
                _tradeAnalysisSourceText = Nothing
                ClearTradeDiscordCache()
                _tradeExtracted.Clear()
                PopulateTradeItems(Nothing)
                _tradeGrid.Rows.Clear()
                _tradeStatus.Text = "Posts changed. Click Analyze posts with AI to detect items and rebuild the queue."
            End Sub
        AddHandler _tradeTemplate.TextChanged, Sub() If Not _tradeLoading AndAlso _tradeExtracted.Count > 0 Then RebuildAiTradeQueue()
        For Each box In {_tradeSource, _tradeTemplate}
            AddHandler box.TextChanged, AddressOf TradeInputChanged
        Next
        AddHandler _tradeDelay.ValueChanged, AddressOf TradeInputChanged
        AddHandler _tradeGrid.CellEndEdit, Sub() TradeInputChanged(Me, EventArgs.Empty)
        AddHandler _tradeGrid.UserDeletedRow, Sub() TradeInputChanged(Me, EventArgs.Empty)
        AddHandler _tradeStopTimer.Tick,
            Sub()
                If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
                    _tradeCancellation?.Cancel()
                    _tradeAnalysisCancellation?.Cancel()
                    StopTradeBrowserCapture()
                    StopTradeDiscordImport()
                    _tradePriceScan.Checked = False
                End If
            End Sub
        Return tab
    End Function

    Private Sub TradeInputChanged(sender As Object, e As EventArgs)
        If _tradeLoading OrElse _tradeSource Is Nothing OrElse Not _tradeSource.IsHandleCreated OrElse _tradeRunning OrElse _tradeAnalyzing Then Return
        SavePersistedListState(False)
    End Sub

    Private Function BuildPersistedTradeState() As TradeSettings
        If _tradeSource Is Nothing Then Return New TradeSettings()
        _tradeGrid.EndEdit()
        Return New TradeSettings With {.DiscordText = _tradeSource.Text,
            .ItemSearch = _tradeItemSearch.Text,
            .ExtractedListings = _tradeExtracted.ToList(), .SelectedItemKeys = SelectedTradeItems(),
            .Buying = _tradeMode.SelectedIndex = 0, .MessageTemplate = _tradeTemplate.Text, .DelaySeconds = CInt(_tradeDelay.Value),
            .Recipients = ReadTradeRows(), .PriceOffers = _tradeOffers.ToList(),
            .DiscordChannelUrl = _tradeDiscordChannelUrl, .EncryptedDiscordBotToken = _tradeDiscordEncryptedBotToken,
            .DiscordAutoImport = _tradeDiscordAuto IsNot Nothing AndAlso _tradeDiscordAuto.Checked,
            .DiscordImportMessageCount = _tradeDiscordImportMessageCount}
    End Function

    Private Function ReadTradeRows() As List(Of TradeRecipient)
        Dim result As New List(Of TradeRecipient)()
        For Each row As DataGridViewRow In _tradeGrid.Rows
            If row.IsNewRow Then Continue For
            result.Add(New TradeRecipient With {.Selected = Object.Equals(row.Cells("Send").Value, True),
                .CharacterName = Convert.ToString(row.Cells("Character").Value).Trim(), .Items = Convert.ToString(row.Cells("Items").Value),
                .Message = Convert.ToString(row.Cells("Message").Value)})
        Next
        Return result
    End Function

    Private Sub ApplyPersistedTradeState(settings As TradeSettings)
        If _tradeSource Is Nothing Then Return
        _tradeCancellation?.Cancel()
        _tradeAnalysisCancellation?.Cancel()
        _tradeAnalysisGeneration += 1
        _tradeAnalysisPostCount = 0
        _tradeAnalysisSourcePosts = Nothing
        _tradeAnalysisSourceText = Nothing
        StopTradeBrowserCapture()
        StopTradeDiscordImport(False)
        _tradePriceScan.Checked = False
        _tradePriceGeneration += 1
        _tradeLoading = True
        Try
            settings = If(settings, New TradeSettings())
            ApplyTradeDiscordSettings(settings)
            _tradeSource.Text = NormalizeTradeSourceText(settings.DiscordText)
            _tradeItemSearch.Text = If(settings.ItemSearch, "")
            _tradeMode.SelectedIndex = If(settings.Buying, 0, 1)
            _tradeTemplate.Text = settings.MessageTemplate
            _tradeDelay.Value = Math.Clamp(settings.DelaySeconds, 1, 300)
            _tradeExtracted = If(settings.ExtractedListings, New List(Of TradeListing)())
            PopulateTradeItems(settings.SelectedItemKeys)
            SetTradeRows(If(settings.Recipients, New List(Of TradeRecipient)()))
            _tradeOffers = If(settings.PriceOffers, New List(Of TradePriceOffer)()).Where(Function(o) o IsNot Nothing).ToList()
            RenderTradePrices()
        Finally
            _tradeLoading = False
        End Try
        UpdateTradeDiscordPolling()
    End Sub

    Private Shared Function NormalizeTradeSourceText(value As String) As String
        Return If(value, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Replace(vbLf, vbCrLf)
    End Function

    Private Sub SetTradeRows(rows As IEnumerable(Of TradeRecipient))
        _tradeGrid.Rows.Clear()
        For Each row In rows
            If row IsNot Nothing Then _tradeGrid.Rows.Add(row.Selected, row.CharacterName, row.Items, row.Message, "Ready")
        Next
    End Sub

    Private Function SelectedTradeItems() As List(Of String)
        Return TradeAiService.RankItems(_tradeExtracted, _tradeMode.SelectedIndex = 0).Where(Function(item) _tradeChosenItems.Contains(item.ItemKey)).Select(Function(item) item.ItemKey).ToList()
    End Function

    Private Sub PopulateTradeItems(selected As List(Of String))
        Dim wasLoading = _tradeLoading
        _tradeLoading = True
        Try
            _tradeDetectedItems.Items.Clear()
            Dim ranked = TradeAiService.RankItems(_tradeExtracted, _tradeMode.SelectedIndex = 0)
            Dim chosen = If(selected, ranked.Take(3).Select(Function(item) item.ItemKey).ToList())
            _tradeChosenItems = New HashSet(Of String)(chosen, StringComparer.OrdinalIgnoreCase)
            Dim query = _tradeItemSearch.Text.Trim()
            For Each item In ranked.Where(Function(entry) query.Length = 0 OrElse entry.ItemKey.Contains(query, StringComparison.OrdinalIgnoreCase))
                _tradeDetectedItems.Items.Add(item, _tradeChosenItems.Contains(item.ItemKey))
            Next
            UpdateTradeSelectionButtons()
        Finally
            _tradeLoading = wasLoading
        End Try
    End Sub

    Private Sub UpdateTradeSelectionButtons()
        _tradeSelectAllMatches.Enabled = _tradeDetectedItems.Items.Count > 0
        _tradeUnselectAll.Enabled = _tradeChosenItems IsNot Nothing AndAlso _tradeChosenItems.Count > 0
    End Sub

    Private Sub SelectAllMatchingTradeItems()
        If _tradeLoading OrElse _tradeRunning OrElse _tradeAnalyzing OrElse _tradeDetectedItems.Items.Count = 0 Then Return
        Dim changed As Boolean
        _tradeLoading = True
        Try
            For i As Integer = 0 To _tradeDetectedItems.Items.Count - 1
                If _tradeChosenItems.Add(DirectCast(_tradeDetectedItems.Items(i), TradeItemFrequency).ItemKey) Then changed = True
                _tradeDetectedItems.SetItemChecked(i, True)
            Next
        Finally
            _tradeLoading = False
        End Try
        UpdateTradeSelectionButtons()
        If changed Then RebuildAiTradeQueue()
    End Sub

    Private Sub UnselectAllTradeItems()
        If _tradeLoading OrElse _tradeRunning OrElse _tradeAnalyzing OrElse _tradeChosenItems Is Nothing OrElse _tradeChosenItems.Count = 0 Then Return
        _tradeLoading = True
        Try
            _tradeChosenItems.Clear()
            For i As Integer = 0 To _tradeDetectedItems.Items.Count - 1
                _tradeDetectedItems.SetItemChecked(i, False)
            Next
        Finally
            _tradeLoading = False
        End Try
        UpdateTradeSelectionButtons()
        RebuildAiTradeQueue()
    End Sub

    Private Sub RebuildAiTradeQueue()
        If _tradeLoading OrElse _tradeRunning OrElse _tradeAnalyzing Then Return
        Try
            Dim rows = TradeAiService.BuildQueue(_tradeExtracted, _tradeMode.SelectedIndex = 0, SelectedTradeItems(), _tradeTemplate.Text)
            SetTradeRows(rows)
            _tradeStatus.Text = TradeAnalysisQueueStatus(rows.Count)
            TradeInputChanged(Me, EventArgs.Empty)
        Catch ex As Exception
            _tradeGrid.Rows.Clear()
            _tradeStatus.Text = ex.Message
        End Try
    End Sub

    Private Function TradeAnalysisQueueStatus(recipientCount As Integer) As String
        Dim mode = If(_tradeMode.SelectedIndex = 0, "seller", "buyer")
        Dim rankedCount = TradeAiService.RankItems(_tradeExtracted, _tradeMode.SelectedIndex = 0).Count
        Dim prefix = If(_tradeAnalysisPostCount > 0, $"Analyzed {_tradeAnalysisPostCount:N0} posts. ", "")
        prefix &= $"Extracted {_tradeExtracted.Count:N0} buy/sell listings from {_tradeExtracted.Select(Function(entry) entry.CharacterName).Distinct(StringComparer.Ordinal).Count():N0} character(s). "
        Return prefix & $"{rankedCount:N0} {mode} item(s); {SelectedTradeItems().Count:N0} checked; {recipientCount:N0} character(s) ready. " &
            If(_tradeExtracted.Count = 0, "No clear listings found; include original names and messages.",
                "Top 3 start checked. Clear the filter and Select all matches for all items in this mode. Review before Start.")
    End Function

    Private Sub BeginTradeAnalysis(cancellation As CancellationTokenSource)
        StopTradeDiscordImport(False)
        _tradeAnalysisGeneration += 1
        _tradeAnalysisCancellation = cancellation
        _tradeAnalyzing = True
        UpdateTradeBrowserCaptureState()
        _tradeOptions.Enabled = False
        _tradeGrid.Enabled = False
        _tradeStart.Enabled = False
        _tradeStopTimer.Start()
        _tradeStatus.Text = "GPT-5 nano is reading all posts, with up to four batches at once. Completed results are reused during this app session. Stop / F12 cancels; your existing review queue is kept until completion."
    End Sub

    Private Function IsCurrentTradeAnalysis(cancellation As CancellationTokenSource, sourceText As String, generation As Integer,
                                            Optional allowCancelled As Boolean = False) As Boolean
        Return Not IsDisposed AndAlso Not Disposing AndAlso Not _tradeLoading AndAlso _tradeAnalyzing AndAlso
            Object.ReferenceEquals(_tradeAnalysisCancellation, cancellation) AndAlso _tradeAnalysisGeneration = generation AndAlso
            String.Equals(_tradeSource.Text, sourceText, StringComparison.Ordinal) AndAlso (allowCancelled OrElse Not cancellation.IsCancellationRequested)
    End Function

    Private Sub ReportTradeAnalysisProgress(progress As TradeAnalysisProgress, cancellation As CancellationTokenSource, sourceText As String, generation As Integer)
        If progress Is Nothing OrElse Not IsCurrentTradeAnalysis(cancellation, sourceText, generation) Then Return
        _tradeStatus.Text = $"GPT-5 nano: batch {progress.CompletedBatches:N0}/{progress.TotalBatches:N0}; {progress.ProcessedPosts:N0}/{progress.TotalPosts:N0} posts processed; {progress.ListingCount:N0} buy/sell listings. Stop / F12 cancels. Existing review queue is kept until all batches complete."
    End Sub

    Private Function ApplyCompletedTradeAnalysis(listings As List(Of TradeListing), processedPosts As Integer, cancellation As CancellationTokenSource,
                                                 sourceText As String, generation As Integer) As Boolean
        If Not IsCurrentTradeAnalysis(cancellation, sourceText, generation) Then Return False
        Dim chosen = TradeAiService.RankItems(listings, _tradeMode.SelectedIndex = 0).Take(3).Select(Function(item) item.ItemKey).ToList()
        ' Build the complete queue before replacing any reviewed state (e.g. a bad template can fail).
        Dim rows = TradeAiService.BuildQueue(listings, _tradeMode.SelectedIndex = 0, chosen, _tradeTemplate.Text)
        _tradeExtracted = listings
        _tradeAnalysisPostCount = processedPosts
        PopulateTradeItems(chosen)
        SetTradeRows(rows)
        _tradeStatus.Text = TradeAnalysisQueueStatus(rows.Count)
        Return True
    End Function

    Private Sub EndTradeAnalysis(cancellation As CancellationTokenSource)
        If Not Object.ReferenceEquals(_tradeAnalysisCancellation, cancellation) Then Return
        _tradeAnalyzing = False
        _tradeAnalysisCancellation = Nothing
        If Not IsDisposed AndAlso Not Disposing Then
            _tradeStopTimer.Stop()
            _tradeOptions.Enabled = True
            _tradeGrid.Enabled = True
            _tradeStart.Enabled = True
            Dim analysisStatus = _tradeStatus.Text
            UpdateTradeDiscordPolling()
            UpdateTradeBrowserCaptureState()
            _tradeStatus.Text = analysisStatus
            SavePersistedListState(False)
        End If
    End Sub

    Private Async Sub ParseTradeQueue(sender As Object, e As EventArgs)
        If _tradeRunning OrElse _tradeAnalyzing Then Return
        If _tradeSource.TextLength > TradeAiService.MaximumAnalysisCharacters Then
            _tradeStatus.Text = "Posts exceed the analysis text limit. Existing posts and reviewed whispers are kept. Reduce the source before Analyze."
            Return
        End If
        If String.IsNullOrWhiteSpace(_quizApiKey) Then
            ConfigureQuizApiKey()
            If String.IsNullOrWhiteSpace(_quizApiKey) Then
                _tradeStatus.Text = "Configure the AI key to analyze posts automatically. No item filter is needed."
                Return
            End If
        End If
        Dim cancellation As New CancellationTokenSource()
        Dim sourceText = _tradeSource.Text
        Dim sourcePosts = If(String.Equals(sourceText, _tradeAnalysisSourceText, StringComparison.Ordinal), _tradeAnalysisSourcePosts?.ToList(), Nothing)
        Dim model = TradeAiService.DefaultAnalysisModel
        Dim apiKey = _quizApiKey
        BeginTradeAnalysis(cancellation)
        Dim generation = _tradeAnalysisGeneration
        Dim progress As New TradeAnalysisUiProgress(New Progress(Of TradeAnalysisProgress)(
            Sub(value) ReportTradeAnalysisProgress(value, cancellation, sourceText, generation)))
        Try
            Dim listings = Await Task.Run(Function() TradeAiService.AnalyzeAsync(sourceText, apiKey, model, cancellation.Token,
                progress:=progress, sourcePosts:=sourcePosts), cancellation.Token)
            cancellation.Token.ThrowIfCancellationRequested()
            ApplyCompletedTradeAnalysis(listings, progress.TotalPosts, cancellation, sourceText, generation)
        Catch ex As OperationCanceledException
            If IsCurrentTradeAnalysis(cancellation, sourceText, generation, True) Then _tradeStatus.Text = "AI analysis cancelled. Your previous analyzed items and reviewed whispers are kept."
        Catch ex As Exception
            If IsCurrentTradeAnalysis(cancellation, sourceText, generation, True) Then _tradeStatus.Text = "AI analysis failed; previous analyzed items and reviewed whispers are kept. " & ex.Message
        Finally
            EndTradeAnalysis(cancellation)
            cancellation.Dispose()
        End Try
    End Sub

    Private Function GetTradeStartBlockReason() As String
        If _tradeRunning Then Return "Whispers are already running. Stop / F12 cancels the queue."
        If _tradeAnalyzing Then Return "Wait for AI analysis to finish before starting whispers."
        If Not _quizUnlocked Then Return "Trade is locked. Unlock the extra tabs before starting whispers."
        If _workflowModes IsNot Nothing AndAlso _workflowModes.Current <> OperatingMode.Idle Then Return "Stop the active " & _workflowModes.Current.ToString() & " workflow before starting whispers."
        Return ""
    End Function

    Private Async Function WaitForTradeInputWorkersAsync(cancellation As CancellationToken, initialWorkers As IEnumerable(Of Task),
                                                        Optional timeoutMs As Integer = 10000) As Task
        Dim workers As New HashSet(Of Task)(If(initialWorkers, Enumerable.Empty(Of Task)()).Where(Function(worker) worker IsNot Nothing))
        Dim started = Environment.TickCount64
        Do
            cancellation.ThrowIfCancellationRequested()
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
            For Each engine In {_fullEngine, _liteEngine}
                If engine Is Nothing Then Continue For
                For Each worker In engine.GetInputWorkerTasks()
                    workers.Add(worker)
                Next
            Next
            If workers.All(Function(worker) worker.IsCompleted) Then Return
            If Environment.TickCount64 - started >= timeoutMs Then Throw New InvalidOperationException("Combat input is still stopping. No whisper was started; wait and try again.")
            Await Task.Delay(25, cancellation)
        Loop
    End Function

    Private Shared Async Function WaitForTradeForegroundAsync(hwnd As IntPtr, expectedPid As UInteger, delayMs As Integer, cancellation As CancellationToken,
                                                              Optional foregroundWindow As Func(Of IntPtr) = Nothing,
                                                              Optional restoreForeground As Func(Of IntPtr, UInteger, Boolean) = Nothing) As Task
        If foregroundWindow Is Nothing Then
            foregroundWindow = Function() NativeMethods.GetForegroundWindow()
            If restoreForeground Is Nothing Then restoreForeground = AddressOf WindowsInput.TryRestoreGameForeground
        End If
        Dim started = Environment.TickCount64
        Do
            cancellation.ThrowIfCancellationRequested()
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
            Dim pid As UInteger
            If hwnd = IntPtr.Zero OrElse expectedPid = 0 OrElse NativeMethods.GetWindowThreadProcessId(hwnd, pid) = 0 OrElse pid <> expectedPid Then
                Throw New InvalidOperationException("The selected game window closed or its process changed.")
            End If
            If foregroundWindow() <> hwnd OrElse NativeMethods.IsIconic(hwnd) Then
                If restoreForeground Is Nothing Then Throw New InvalidOperationException("The game lost focus. Whispers stopped; close any unfinished chat draft before retrying the unsent rows.")
                restoreForeground(hwnd, expectedPid)
                cancellation.ThrowIfCancellationRequested()
                If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
                Await Task.Delay(50, cancellation)
                Continue Do
            End If
            Dim remaining = CLng(delayMs) - (Environment.TickCount64 - started)
            If remaining <= 0 Then Return
            Await Task.Delay(CInt(Math.Min(50L, remaining)), cancellation)
        Loop
    End Function

    Private Function ApplyTradeWhisperSubmission(characterName As String, generation As Integer, cancellation As CancellationTokenSource,
                                                 Optional submittedFailure As Boolean = False) As Boolean
        If IsDisposed OrElse Disposing OrElse _tradeLoading OrElse Not _tradeRunning OrElse
            generation <> _tradeAnalysisGeneration OrElse Not Object.ReferenceEquals(_tradeCancellation, cancellation) Then Return False
        For Each gridRow As DataGridViewRow In _tradeGrid.Rows
            If Not gridRow.IsNewRow AndAlso String.Equals(Convert.ToString(gridRow.Cells("Character").Value).Trim(), characterName, StringComparison.Ordinal) Then
                gridRow.Cells("Status").Value = If(submittedFailure, "Sent; input stopped", "Sent to game")
                gridRow.Cells("Send").Value = False
            End If
        Next
        Return True
    End Function

    Private Async Sub StartTradeQueue(sender As Object, e As EventArgs)
        Dim blocked = GetTradeStartBlockReason()
        If blocked.Length > 0 Then
            _tradeStatus.Text = blocked
            Return
        End If
        Dim cancellation As CancellationTokenSource = Nothing
        Dim completed As Boolean = False
        Dim generation = _tradeAnalysisGeneration
        Dim resumeEdition As BotEdition = BotEdition.Full
        Dim resumeWindow As IntPtr = IntPtr.Zero
        Dim resumePid As UInteger
        Try
            _tradeGrid.EndEdit()
            Dim queue = ReadTradeRows().Where(Function(row) row.Selected).ToList()
            If queue.Count = 0 Then Throw New InvalidOperationException("Check at least one recipient in the queue.")
            For Each row In queue
                TradeService.BuildWhisper(row.CharacterName, row.Message)
            Next
            If queue.Select(Function(row) row.CharacterName).Distinct(StringComparer.Ordinal).Count() <> queue.Count Then Throw New InvalidOperationException("The queue contains the same exact character name more than once. Combine that character's items into one row.")
            Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero Then Throw New InvalidOperationException("Select the Full game window first.")
            If _quizSolveInProgress OrElse (chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked) Then Throw New InvalidOperationException("Stop Quiz and let its current action finish before starting Trade.")
            If _resuRunning OrElse _resuBusy Then Throw New InvalidOperationException("Stop RESU and let its current action finish before starting Trade.")
            resumeEdition = If(_liteEngine.IsRunning(), BotEdition.Lite, BotEdition.Full)
            Dim resumeSelected = GetSelectedProcessWindowForEdition(resumeEdition)
            If resumeSelected IsNot Nothing Then
                resumeWindow = resumeSelected.MainWindowHandle
                NativeMethods.GetWindowThreadProcessId(resumeWindow, resumePid)
            End If
            Dim hwnd = selected.MainWindowHandle
            Dim pid As UInteger
            If NativeMethods.GetWindowThreadProcessId(hwnd, pid) = 0 OrElse selected.ProcessId <= 0 OrElse pid <> CUInt(selected.ProcessId) Then
                Throw New InvalidOperationException("The selected game window is no longer available or its process changed. Refresh the Full window list and select the game again.")
            End If
            SavePersistedListState(True)
            If Not _workflowModes.Transition(OperatingMode.Trade, "foreground whisper queue started") Then Throw New InvalidOperationException("Another workflow is active.")
            cancellation = New CancellationTokenSource()
            StopTradeDiscordImport(False)
            _tradeCancellation = cancellation
            _tradeForegroundMaintenanceWindow = hwnd
            _tradeForegroundMaintenancePid = pid
            _tradeForegroundMaintenanceAllowed = False
            _tradeRunning = True
            UpdateTradeBrowserCaptureState()
            _tradeStart.Enabled = False
            _tradeOptions.Enabled = False
            _tradeGrid.Enabled = False
            _tradeStopTimer.Start()
            UpdateMainTabIndicators()
            _tradeStatus.Text = "Stopping combat input before foreground whispers. Focus restores between complete messages; Stop / F12 cancels."
            ' Retain tasks before Stop clears detached scanner references.
            Dim inputWorkers = _fullEngine.GetInputWorkerTasks().Concat(_liteEngine.GetInputWorkerTasks()).ToArray()
            If _fullEngine.IsRunning() Then StopEdition(BotEdition.Full, False, "starting Trade")
            If _liteEngine.IsRunning() Then StopEdition(BotEdition.Lite, False, "starting Trade")
            Await WaitForTradeInputWorkersAsync(cancellation.Token, inputWorkers)
            Dim delay = CInt(_tradeDelay.Value) * 1000
            For i = 0 To queue.Count - 1
                cancellation.Token.ThrowIfCancellationRequested()
                If generation <> _tradeAnalysisGeneration Then Throw New OperationCanceledException()
                _tradeForegroundMaintenanceAllowed = True
                _tradeStatus.Text = $"Waiting for the selected game before whisper {i + 1}/{queue.Count}. Stop / F12 cancels."
                Await WaitForTradeForegroundAsync(hwnd, pid, 0, cancellation.Token, restoreForeground:=AddressOf TryMaintainGameplayForeground)
                cancellation.Token.ThrowIfCancellationRequested()
                _tradeForegroundMaintenanceAllowed = False
                Dim recipient = queue(i)
                _tradeStatus.Text = If(i = 0,
                    $"Starting {recipient.CharacterName} with chat closed. F12 or Stop cancels.",
                    $"Typing {i + 1}/{queue.Count}: {recipient.CharacterName}. F12 or Stop cancels.")
                Dim activateTarget = i = 0
                Dim sent As Boolean
                Dim submittedFailure As TradeWhisperSubmittedException = Nothing
                Try
                    sent = Await Task.Run(Function() TradeService.SendForegroundWhisper(hwnd, pid, recipient.CharacterName, recipient.Message, cancellation.Token, activateTarget:=activateTarget), cancellation.Token)
                Catch ex As TradeWhisperSubmittedException
                    sent = True
                    submittedFailure = ex
                End Try
                If Not sent Then Throw New InvalidOperationException("Could not send to " & recipient.CharacterName & ". Queue stopped; verify the game chat before restarting.")
                If IsDisposed OrElse Disposing Then Return
                If Not ApplyTradeWhisperSubmission(recipient.CharacterName, generation, cancellation, submittedFailure IsNot Nothing) Then Throw New OperationCanceledException()
                If submittedFailure IsNot Nothing Then Throw submittedFailure
                _tradeStatus.Text = $"Sent to game: {recipient.CharacterName} ({i + 1}/{queue.Count})."
                If i < queue.Count - 1 Then
                    _tradeForegroundMaintenanceAllowed = True
                    Await WaitForTradeForegroundAsync(hwnd, pid, delay, cancellation.Token, restoreForeground:=AddressOf TryMaintainGameplayForeground)
                End If
            Next
            cancellation.Token.ThrowIfCancellationRequested()
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
            completed = True
            _tradeStatus.Text = $"Completed: {queue.Count} whisper(s) sent to the game. Sent rows are unchecked."
        Catch ex As OperationCanceledException
            If Not IsDisposed AndAlso generation = _tradeAnalysisGeneration Then _tradeStatus.Text = "Stopped. Completed rows stay unchecked; close any unfinished game chat draft and review remaining rows before restarting."
        Catch ex As Exception
            If Not IsDisposed AndAlso generation = _tradeAnalysisGeneration Then
                _tradeStatus.Text = "Trade stopped: " & ex.Message & " Review game chat before retrying unsent rows."
                SilentMessageBox.Show(Me, ex.Message, "Trade")
            End If
        Finally
            If cancellation IsNot Nothing Then
                _tradeForegroundMaintenanceAllowed = False
                _tradeForegroundMaintenanceWindow = IntPtr.Zero
                _tradeForegroundMaintenancePid = 0UI
                completed = completed AndAlso Not cancellation.IsCancellationRequested
                _workflowModes.Transition(OperatingMode.Idle, If(completed, "all whispers completed", "whispers cancelled or failed"))
                _tradeRunning = False
                _tradeCancellation = Nothing
                cancellation.Dispose()
                If Not IsDisposed AndAlso Not Disposing Then
                    _tradeStopTimer.Stop()
                    _tradeStart.Enabled = True
                    _tradeOptions.Enabled = True
                    _tradeGrid.Enabled = True
                    Dim tradeStatus = _tradeStatus.Text
                    UpdateTradeDiscordPolling()
                    UpdateTradeBrowserCaptureState()
                    _tradeStatus.Text = tradeStatus
                    UpdateMainTabIndicators()
                    SavePersistedListState(False)
                End If
            End If
        End Try
        If completed AndAlso Not IsDisposed AndAlso Not Disposing Then
            Try
                Dim selected = GetSelectedProcessWindowForEdition(resumeEdition)
                Dim currentPid As UInteger
                If resumeWindow = IntPtr.Zero OrElse selected Is Nothing OrElse selected.MainWindowHandle <> resumeWindow OrElse
                    NativeMethods.GetWindowThreadProcessId(resumeWindow, currentPid) = 0 OrElse currentPid <> resumePid Then
                    Throw New InvalidOperationException("The selected game window changed or closed.")
                End If
                StartEdition(resumeEdition, False)
                _tradeStatus.Text &= If(IsEditionRunning(resumeEdition), $" {resumeEdition} bot resumed automatically.", " Bot could not restart; check Diagnostics.")
            Catch ex As Exception
                _tradeStatus.Text &= " Bot restart failed: " & ex.Message
                AppendLog("Trade completed, but bot restart failed: " & ex.Message)
            End Try
        End If
    End Sub
End Class
