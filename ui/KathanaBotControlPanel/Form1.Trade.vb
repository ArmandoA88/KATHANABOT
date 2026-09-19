Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private _tradeTab As TabPage
    Private _tradeSource As TextBox
    Private _tradeDetectedItems As CheckedListBox
    Private _tradeItemSearch As TextBox
    Private _tradeChosenItems As HashSet(Of String)
    Private _tradeExtracted As New List(Of TradeListing)()
    Private _tradeAnalyzing As Boolean
    Private _tradeAnalysisCancellation As CancellationTokenSource
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
    Private ReadOnly _tradeStopTimer As New System.Windows.Forms.Timer With {.Interval = 50}

    Private Function BuildTradeTab() As TabPage
        Dim tab As New TabPage("Trade") With {.BackColor = ThemeBg}
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = False, .Padding = New Padding(8)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6}
        For Each rowSize As Single In {0, 0, 48, 52, 0, 0}
            body.RowStyles.Add(New RowStyle(If(rowSize = 0, SizeType.AutoSize, SizeType.Percent), rowSize))
        Next
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(New Label With {.Text = "TRADE / DISCORD WHISPERS", .AutoSize = True, .Font = New Font("Segoe UI", 17, FontStyle.Bold), .ForeColor = ThemeAccent})
        body.Controls.Add(New Label With {.Text = "Paste posts > Analyze > review checked whispers > Start. Select the Full game window with chat closed. F12 stops.", .AutoSize = True, .Margin = New Padding(0, 3, 0, 5)})
        Dim options As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1}
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))
        options.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        _tradeOptions = options
        _tradeSource = New TextBox With {.Multiline = True, .ScrollBars = ScrollBars.Vertical, .Dock = DockStyle.Fill, .Height = 180, .MaxLength = 200000, .PlaceholderText = "Paste the Discord posts here, including their authors and item listings."}
        _tradeMode = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        _tradeMode.Items.AddRange({"Buy from sellers", "Sell to buyers"})
        _tradeMode.SelectedIndex = 0
        _tradeExtracted = New List(Of TradeListing)()
        _tradeChosenItems = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        _tradeItemSearch = New TextBox With {.Dock = DockStyle.Fill, .PlaceholderText = "Find detected items, e.g. ASBA or ROR (optional)"}
        _tradeDetectedItems = New CheckedListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False, .CheckOnClick = True}
        _tradeTemplate = New TextBox With {.Dock = DockStyle.Fill, .Text = TradeService.BuyTemplate, .MaxLength = 220}
        _tradeDelay = New NumericUpDown With {.Minimum = 1, .Maximum = 300, .Value = 5, .Dock = DockStyle.Fill}
        Dim posts As New GroupBox With {.Text = "Discord posts / names", .Dock = DockStyle.Fill, .Padding = New Padding(6)}
        posts.Controls.Add(_tradeSource)
        options.Controls.Add(posts, 0, 0)
        Dim items As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        items.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        items.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        items.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        items.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        items.Controls.Add(New Label With {.Text = "Detected items (top 3 auto-selected)", .AutoSize = True}, 0, 0)
        items.Controls.Add(_tradeItemSearch, 0, 1)
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
        AddHandler help.Click, Sub() SilentMessageBox.Show(Me, "Analyze sends pasted posts to OpenAI using the encrypted key/model configured in Quiz. It does not send whispers. AI selects the three most common items for Buy/Sell, counting distinct characters. Search filters the detected list; checked hidden items stay selected. Change checkboxes to rebuild the queue." & vbCrLf & vbCrLf & "{items} inserts matching items per character. Buying matches SELL posts; selling matches BUY posts. Character names are case-sensitive. Edit the queue, then Start sends checked rows once in order. Stop / F12 cancels. After successful completion the previous combat mode resumes (Full if none was running)." & vbCrLf & vbCrLf & "Chat price scanning uses Regions > chat_rect every 3 seconds. Offers are cheapest first; review OCR quotes. Clear offers removes collected prices.", "Trade Help")
        Dim setupActions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        setupActions.Controls.AddRange({aiKey, parse, help})
        settings.Controls.Add(setupActions, 0, 3)
        settings.SetColumnSpan(setupActions, 2)
        Dim privacy As New Label With {.Text = "Analyze sends pasted posts to OpenAI. Review the queue before Start. {items} inserts matching items.", .AutoSize = True, .Dock = DockStyle.Fill}
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
        _tradeStart = New Button With {.Text = "Start whispers", .AutoSize = True, .Height = 34}
        AddHandler _tradeStart.Click, AddressOf StartTradeQueue
        Dim stopButton As New Button With {.Text = "Stop / F12", .AutoSize = True, .Height = 34}
        AddHandler stopButton.Click,
            Sub()
                _tradeCancellation?.Cancel()
                _tradeAnalysisCancellation?.Cancel()
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
            .Recipients = ReadTradeRows(), .PriceOffers = _tradeOffers.ToList()}
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
        _tradePriceScan.Checked = False
        _tradePriceGeneration += 1
        _tradeLoading = True
        Try
            settings = If(settings, New TradeSettings())
            _tradeSource.Text = settings.DiscordText
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
    End Sub

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
        Finally
            _tradeLoading = wasLoading
        End Try
    End Sub

    Private Sub RebuildAiTradeQueue()
        If _tradeLoading OrElse _tradeRunning Then Return
        Try
            Dim rows = TradeAiService.BuildQueue(_tradeExtracted, _tradeMode.SelectedIndex = 0, SelectedTradeItems(), _tradeTemplate.Text)
            SetTradeRows(rows)
            _tradeStatus.Text = If(rows.Count > 0, $"AI found {TradeAiService.RankItems(_tradeExtracted, _tradeMode.SelectedIndex = 0).Count} item(s). {SelectedTradeItems().Count} selected; {rows.Count} character(s) ready. Review the queue, then Start.",
                If(_tradeExtracted.Count = 0, "AI found no clear buy/sell listings in these posts. Include the original authors and listing text.",
                    "No recipients for this selection. Check an item above or switch Buy/Sell; only the opposite-side listings are contacted."))
            TradeInputChanged(Me, EventArgs.Empty)
        Catch ex As Exception
            _tradeGrid.Rows.Clear()
            _tradeStatus.Text = ex.Message
        End Try
    End Sub

    Private Async Sub ParseTradeQueue(sender As Object, e As EventArgs)
        If _tradeRunning OrElse _tradeAnalyzing Then Return
        If String.IsNullOrWhiteSpace(_quizApiKey) Then
            ConfigureQuizApiKey()
            If String.IsNullOrWhiteSpace(_quizApiKey) Then
                _tradeStatus.Text = "Configure the AI key to analyze posts automatically. No item filter is needed."
                Return
            End If
        End If
        Dim cancellation As New CancellationTokenSource()
        _tradeAnalysisCancellation = cancellation
        _tradeAnalyzing = True
        _tradeOptions.Enabled = False
        _tradeGrid.Enabled = False
        _tradeStart.Enabled = False
        _tradeGrid.Rows.Clear()
        _tradeExtracted.Clear()
        PopulateTradeItems(Nothing)
        _tradeStopTimer.Start()
        _tradeStatus.Text = "AI is reading authors, Buy/Sell intent and item names from all posts... Stop or F12 cancels."
        Try
            Dim model = If(cboQuizModel?.SelectedItem?.ToString(), DefaultQuizModel)
            Dim listings = Await TradeAiService.AnalyzeAsync(_tradeSource.Text, _quizApiKey, model, cancellation.Token)
            cancellation.Token.ThrowIfCancellationRequested()
            If IsDisposed OrElse Disposing Then Return
            _tradeExtracted = listings
            PopulateTradeItems(Nothing)
            RebuildAiTradeQueue()
        Catch ex As OperationCanceledException
            If Not IsDisposed Then _tradeStatus.Text = "AI analysis cancelled. No whispers were sent."
        Catch ex As Exception
            If Not IsDisposed Then _tradeStatus.Text = "AI analysis failed: " & ex.Message
        Finally
            _tradeAnalyzing = False
            _tradeAnalysisCancellation = Nothing
            cancellation.Dispose()
            If Not IsDisposed AndAlso Not Disposing Then
                _tradeStopTimer.Stop()
                _tradeOptions.Enabled = True
                _tradeGrid.Enabled = True
                _tradeStart.Enabled = True
                SavePersistedListState(False)
            End If
        End Try
    End Sub

    Private Async Sub StartTradeQueue(sender As Object, e As EventArgs)
        If _tradeRunning OrElse _tradeAnalyzing OrElse Not _quizUnlocked Then Return
        Dim cancellation As CancellationTokenSource = Nothing
        Dim completed As Boolean = False
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
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero OrElse NativeMethods.IsIconic(selected.MainWindowHandle) Then Throw New InvalidOperationException("Select and restore the Full game window first.")
            If _quizSolveInProgress OrElse (chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked) Then Throw New InvalidOperationException("Stop Quiz and let its current action finish before starting Trade.")
            If _resuRunning OrElse _resuBusy Then Throw New InvalidOperationException("Stop RESU and let its current action finish before starting Trade.")
            resumeEdition = If(_liteEngine.IsRunning(), BotEdition.Lite, BotEdition.Full)
            Dim resumeSelected = GetSelectedProcessWindowForEdition(resumeEdition)
            If resumeSelected IsNot Nothing Then
                resumeWindow = resumeSelected.MainWindowHandle
                NativeMethods.GetWindowThreadProcessId(resumeWindow, resumePid)
            End If
            If _fullEngine.IsRunning() Then StopEdition(BotEdition.Full, False, "starting Trade")
            If _liteEngine.IsRunning() Then StopEdition(BotEdition.Lite, False, "starting Trade")
            Dim hwnd = selected.MainWindowHandle
            Dim pid As UInteger
            If NativeMethods.GetWindowThreadProcessId(hwnd, pid) = 0 Then Throw New InvalidOperationException("The selected game window is no longer available.")
            SavePersistedListState(True)
            cancellation = New CancellationTokenSource()
            _tradeCancellation = cancellation
            If Not _workflowModes.Transition(OperatingMode.Trade, "whisper queue started") Then Throw New InvalidOperationException("Another workflow is active.")
            _tradeRunning = True
            _tradeStart.Enabled = False
            _tradeOptions.Enabled = False
            _tradeGrid.Enabled = False
            _tradeStopTimer.Start()
            UpdateMainTabIndicators()
            Dim delay = CInt(_tradeDelay.Value) * 1000
            For i = 0 To queue.Count - 1
                cancellation.Token.ThrowIfCancellationRequested()
                Dim recipient = queue(i)
                _tradeStatus.Text = $"Typing {i + 1}/{queue.Count}: {recipient.CharacterName}. F12 or Stop cancels."
                Dim sent = Await Task.Run(Function() TradeService.SendWhisper(hwnd, pid, recipient.CharacterName, recipient.Message, cancellation.Token), cancellation.Token)
                If Not sent Then Throw New InvalidOperationException("Could not send to " & recipient.CharacterName & ". Queue stopped; verify the game chat before restarting.")
                If IsDisposed OrElse Disposing Then Return
                For Each gridRow As DataGridViewRow In _tradeGrid.Rows
                    If Not gridRow.IsNewRow AndAlso String.Equals(Convert.ToString(gridRow.Cells("Character").Value).Trim(), recipient.CharacterName, StringComparison.Ordinal) Then
                        gridRow.Cells("Status").Value = "Sent to game"
                        gridRow.Cells("Send").Value = False
                    End If
                Next
                _tradeStatus.Text = $"Sent to game: {recipient.CharacterName} ({i + 1}/{queue.Count})."
                If i < queue.Count - 1 Then Await Task.Delay(delay, cancellation.Token)
            Next
            cancellation.Token.ThrowIfCancellationRequested()
            If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
            completed = True
            _tradeStatus.Text = $"Completed: {queue.Count} whisper(s) sent to the game. Sent rows are unchecked."
        Catch ex As OperationCanceledException
            If Not IsDisposed Then _tradeStatus.Text = "Stopped. Completed rows stay unchecked; review remaining rows before restarting."
        Catch ex As Exception
            If Not IsDisposed Then
                _tradeStatus.Text = "Trade stopped: " & ex.Message
                SilentMessageBox.Show(Me, ex.Message, "Trade")
            End If
        Finally
            If cancellation IsNot Nothing Then
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
                    NativeMethods.GetWindowThreadProcessId(resumeWindow, currentPid) = 0 OrElse currentPid <> resumePid OrElse NativeMethods.IsIconic(resumeWindow) Then
                    Throw New InvalidOperationException("The selected game window changed, closed, or was minimized.")
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
