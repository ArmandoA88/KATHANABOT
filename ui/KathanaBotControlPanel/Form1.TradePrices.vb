Imports System.Threading.Tasks

Partial Public Class Form1
    Private _tradePriceGrid As DataGridView
    Private _tradePriceStatus As Label
    Private _tradePriceScan As CheckBox
    Private _tradePriceTimer As System.Windows.Forms.Timer
    Private _tradePriceBusy As Boolean
    Private _tradePriceGeneration As Integer
    Private _tradeOffers As List(Of TradePriceOffer)

    Private Function BuildTradePriceTables() As Control
        _tradeOffers = New List(Of TradePriceOffer)
        Dim tables As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 310, .ColumnCount = 2, .RowCount = 1}
        tables.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        tables.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        tables.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        _tradeGrid.Dock = DockStyle.Fill
        tables.Controls.Add(_tradeGrid, 0, 0)
        Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        right.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        right.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        _tradePriceScan = New CheckBox With {.Text = "Scan chat prices (every 3 sec)", .AutoSize = True}
        Dim clear As New Button With {.Text = "Clear offers", .AutoSize = True}
        AddHandler clear.Click,
            Sub()
                _tradePriceGeneration += 1
                _tradeOffers.Clear()
                RenderTradePrices()
                TradeInputChanged(Me, EventArgs.Empty)
            End Sub
        actions.Controls.AddRange({_tradePriceScan, clear})
        _tradePriceGrid = New DataGridView With {.Dock = DockStyle.Fill, .ReadOnly = True, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        _tradePriceGrid.Columns.Add("Character", "Character")
        _tradePriceGrid.Columns.Add("Item", "Item")
        _tradePriceGrid.Columns.Add("Price", "Price (rupiahs)")
        _tradePriceGrid.Columns("Price").ValueType = GetType(Decimal)
        _tradePriceGrid.Columns("Price").DefaultCellStyle.Format = "N0"
        _tradePriceGrid.Columns.Add("Evidence", "Chat quote (OCR)")
        _tradePriceGrid.Columns.Add("Seen", "Seen")
        For Each column As DataGridViewColumn In _tradePriceGrid.Columns
            column.SortMode = DataGridViewColumnSortMode.NotSortable
        Next
        _tradePriceStatus = New Label With {.AutoSize = True, .Dock = DockStyle.Fill, .Text = "Cheapest first. Uses Regions > chat_rect. OCR quotes need review; unknown items are not inferred."}
        right.Controls.Add(actions, 0, 0)
        right.Controls.Add(_tradePriceGrid, 0, 1)
        right.Controls.Add(_tradePriceStatus, 0, 2)
        tables.Controls.Add(right, 1, 0)
        _tradePriceTimer = New System.Windows.Forms.Timer With {.Interval = 3000}
        AddHandler _tradePriceTimer.Tick, AddressOf ScanTradePrices
        AddHandler _tradePriceScan.CheckedChanged,
            Sub()
                _tradePriceGeneration += 1
                _tradePriceTimer.Enabled = _tradePriceScan.Checked
                If _tradePriceScan.Checked Then ScanTradePrices(Me, EventArgs.Empty)
            End Sub
        AddHandler tables.Disposed, Sub() _tradePriceTimer.Dispose()
        Return tables
    End Function

    Private Sub RenderTradePrices()
        _tradePriceGrid.Rows.Clear()
        For Each offer In _tradeOffers.OrderBy(Function(o) o.Price)
            _tradePriceGrid.Rows.Add(offer.CharacterName, offer.Item, offer.Price, offer.Evidence, offer.Seen.ToString("HH:mm:ss"))
        Next
    End Sub

    Private Async Sub ScanTradePrices(sender As Object, e As EventArgs)
        If _tradePriceBusy OrElse Not _tradePriceScan.Checked OrElse IsDisposed OrElse Disposing Then Return
        If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then
            _tradePriceScan.Checked = False
            Return
        End If
        _tradePriceBusy = True
        Dim generation = _tradePriceGeneration
        Try
            Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse selected.MainWindowHandle = IntPtr.Zero OrElse NativeMethods.IsIconic(selected.MainWindowHandle) Then Throw New InvalidOperationException("Select and restore the Full game window.")
            Dim hwnd = selected.MainWindowHandle
            Dim region = BuildConfig().ChatRect
            If region Is Nothing OrElse region.W <= 0 OrElse region.H <= 0 Then Throw New InvalidOperationException("Calibrate chat_rect in Regions first.")
            Dim recipients = ReadTradeRows()
            Dim text = Await Task.Run(
                Function()
                    Using crop = BotEngine.CaptureClientRegion(hwnd, region)
                        If crop Is Nothing Then Throw New InvalidOperationException("Could not capture chat_rect.")
                        Dim lines = OcrReader.ReadScreenTextRegionsIsolated(crop)
                        Return String.Join(vbLf, lines.OrderBy(Function(line) line.Bounds.Top).ThenBy(Function(line) line.Bounds.Left).Select(Function(line) line.Text))
                    End Using
                End Function)
            If IsDisposed OrElse Disposing OrElse generation <> _tradePriceGeneration Then Return
            Dim current = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If current Is Nothing OrElse current.MainWindowHandle <> hwnd Then Return
            Dim incoming = TradePriceService.Parse(If(text, ""), recipients)
            TradePriceService.Merge(_tradeOffers, incoming)
            RenderTradePrices()
            _tradePriceStatus.Text = $"{_tradeOffers.Count} offers, cheapest first. Last scan {DateTime.Now:HH:mm:ss}: {incoming.Count} price quotes. Prices assumed rupiahs; review OCR."
            If incoming.Count > 0 Then TradeInputChanged(Me, EventArgs.Empty)
        Catch ex As Exception
            If Not IsDisposed AndAlso Not Disposing Then
                _tradePriceStatus.Text = "Price scan: " & ex.Message & " Previous offers kept."
                _tradePriceScan.Checked = False
            End If
        Finally
            _tradePriceBusy = False
        End Try
    End Sub
End Class
