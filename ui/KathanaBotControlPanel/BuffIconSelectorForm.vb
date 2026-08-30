Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports DrawingPoint = System.Drawing.Point

Friend Class BuffIconSelectorForm
    Inherits Form

    <DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(vKey As Integer) As Short
    End Function

    Private Const DefaultHintText As String = "Set Icon size to match one buff icon's true pixel size (no overlap with neighbors) before capturing."

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedName As String = ""

    <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property SelectedRelativePath As String = ""

    Private ReadOnly _gameHwnd As IntPtr
    Private ReadOnly _defaultCategories As String() = {"Library", "General", "Naga - Kimnara", "Ashura - Rakshasa", "Yaksa - Gandharva", "Deva - Garuda", "Other"}
    Private _entries As New List(Of BuffIconLibraryEntry)()
    Private _selectedCategory As String = "library"
    Private _selectedTile As PictureBox = Nothing
    Private _isCapturing As Boolean = False
    Private _capturingCategory As String = ""
    Private _captureLeftMouseWasDown As Boolean = False
    Private ReadOnly _captureTimer As New Timer() With {.Interval = 45}

    Private lstCategories As ListBox
    Private txtSearch As TextBox
    Private nudIconSize As NumericUpDown
    Private flowIcons As FlowLayoutPanel
    Private btnApply As Button
    Private btnCancel As Button
    Private lblHint As Label

    ' Category identity is the sanitized folder name (what's actually on disk, e.g. "ashura_rakshasa"),
    ' with a separate friendly DisplayName for the list ("Ashura - Rakshasa"). Earlier versions compared
    ' the pretty display name directly against on-disk folder names when filtering icons - since
    ' SaveBuffIconToLibrary always sanitizes the category before creating the folder, that comparison
    ' never matched, so icons saved under a default category silently vanished from that category's view
    ' (while a second, ugly duplicate entry for the raw sanitized folder appeared in the list instead).
    Private NotInheritable Class CategoryItem
        Public Property DisplayName As String = ""
        Public Property Key As String = ""
        Public Overrides Function ToString() As String
            Return DisplayName
        End Function
    End Class

    Public Sub New(gameHwnd As IntPtr, Optional suggestedIconSize As Integer = 40)
        _gameHwnd = gameHwnd
        Text = "Buff Icon Selector"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(880, 640)
        MinimumSize = New Size(640, 480)
        BackColor = Color.FromArgb(25, 25, 25)
        ForeColor = Color.Gainsboro

        BuildUi()
        BotEngine.EnsureBuffIconLibraryExists()
        nudIconSize.Value = Math.Max(nudIconSize.Minimum, Math.Min(nudIconSize.Maximum, CDec(suggestedIconSize)))
        ReloadLibrary()

        AddHandler _captureTimer.Tick, AddressOf CaptureTimerTick
        _captureTimer.Start()
        AddHandler FormClosed, Sub(sender As Object, e As FormClosedEventArgs) _captureTimer.Stop()
    End Sub

    Private Sub BuildUi()
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 4, .Padding = New Padding(8)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        Controls.Add(root)

        Dim leftPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        leftPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        leftPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        lstCategories = New ListBox() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(35, 35, 35), .ForeColor = Color.Gainsboro, .BorderStyle = BorderStyle.FixedSingle}
        AddHandler lstCategories.SelectedIndexChanged, AddressOf CategorySelectedChanged
        leftPanel.Controls.Add(lstCategories, 0, 0)
        Dim btnAddCategory As New Button() With {.Text = "Add Category", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        AddHandler btnAddCategory.Click, AddressOf AddCategoryClicked
        leftPanel.Controls.Add(btnAddCategory, 0, 1)
        root.Controls.Add(leftPanel, 0, 0)
        root.SetRowSpan(leftPanel, 3)

        Dim searchRow As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 5, .RowCount = 1}
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55.0F))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 104.0F))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 72.0F))
        txtSearch = New TextBox() With {.Dock = DockStyle.Fill, .PlaceholderText = "Search icons..."}
        AddHandler txtSearch.TextChanged, AddressOf SearchTextChanged
        searchRow.Controls.Add(txtSearch, 0, 0)
        Dim lblIconSize As New Label() With {.Text = "Icon size (px):", .AutoSize = True, .ForeColor = Color.LightSteelBlue, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(8, 4, 3, 0)}
        searchRow.Controls.Add(lblIconSize, 1, 0)
        nudIconSize = New NumericUpDown() With {.Minimum = 16, .Maximum = 128, .Value = 40, .Dock = DockStyle.Fill}
        searchRow.Controls.Add(nudIconSize, 2, 0)
        Dim btnOpenFolder As New Button() With {.Text = "Open Icon Folder", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 95, 140), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Margin = New Padding(5, 0, 3, 0)}
        AddHandler btnOpenFolder.Click, AddressOf OpenLibraryFolderClicked
        searchRow.Controls.Add(btnOpenFolder, 3, 0)
        Dim btnRefresh As New Button() With {.Text = "Refresh", .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Margin = New Padding(3, 0, 0, 0)}
        AddHandler btnRefresh.Click, Sub(_sender As Object, _e As EventArgs) ReloadLibrary()
        searchRow.Controls.Add(btnRefresh, 4, 0)
        root.Controls.Add(searchRow, 1, 0)

        Dim raceFolderNotice As New Label() With {
            .Text = "CAN'T FIND A SKILL IN YOUR RACE FOLDER? CHECK LIBRARY - it contains the complete collection, including shared, alternate-name, and unclassified icons.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(126, 62, 0),
            .ForeColor = Color.LightYellow,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleCenter,
            .Padding = New Padding(8, 3, 8, 3),
            .Margin = New Padding(0, 3, 0, 3)
        }
        root.Controls.Add(raceFolderNotice, 1, 1)

        flowIcons = New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .AutoScroll = True, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .BackColor = Color.FromArgb(20, 20, 20)}
        root.Controls.Add(flowIcons, 1, 2)

        lblHint = New Label() With {
            .Text = $"Portable icon library: {BotEngine.BuffIconLibraryRoot}",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.LightSteelBlue,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        root.Controls.Add(lblHint, 0, 3)

        Dim buttonRow As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
        btnCancel = New Button() With {.Text = "Cancel", .AutoSize = True, .DialogResult = DialogResult.Cancel, .BackColor = Color.FromArgb(70, 70, 70), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        btnApply = New Button() With {.Text = "Apply", .AutoSize = True, .Enabled = False, .BackColor = Color.FromArgb(30, 120, 80), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        AddHandler btnApply.Click, AddressOf ApplyClicked
        buttonRow.Controls.Add(btnCancel)
        buttonRow.Controls.Add(btnApply)
        root.Controls.Add(buttonRow, 1, 3)

        CancelButton = btnCancel
    End Sub

    Private Sub OpenLibraryFolderClicked(sender As Object, e As EventArgs)
        Try
            BotEngine.EnsureBuffIconLibraryExists()
            Process.Start(New ProcessStartInfo(BotEngine.BuffIconLibraryRoot) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, $"Unable to open the icon folder: {ex.Message}", "Buff Icon Library", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub ReloadLibrary()
        BotEngine.EnsureBuffIconLibraryExists()
        _entries = BotEngine.ScanBuffIconLibrary()

        Dim orderedKeys As New List(Of String)()
        Dim displayByKey As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each pretty As String In _defaultCategories
            Dim key As String = BotEngine.SanitizeBuffIconIdentifier(pretty)
            If Not displayByKey.ContainsKey(key) Then
                displayByKey(key) = pretty
                orderedKeys.Add(key)
            End If
        Next
        For Each entry As BuffIconLibraryEntry In _entries
            If Not displayByKey.ContainsKey(entry.Category) Then
                displayByKey(entry.Category) = entry.Category
                orderedKeys.Add(entry.Category)
            End If
        Next

        Dim previousKey As String = _selectedCategory
        lstCategories.Items.Clear()
        For Each key As String In orderedKeys
            lstCategories.Items.Add(New CategoryItem() With {.Key = key, .DisplayName = displayByKey(key)})
        Next

        Dim indexToSelect As Integer = -1
        For i As Integer = 0 To lstCategories.Items.Count - 1
            Dim item As CategoryItem = CType(lstCategories.Items(i), CategoryItem)
            If String.Equals(item.Key, previousKey, StringComparison.OrdinalIgnoreCase) Then
                indexToSelect = i
                Exit For
            End If
        Next
        lstCategories.SelectedIndex = If(indexToSelect >= 0, indexToSelect, If(lstCategories.Items.Count > 0, 0, -1))
        RefreshIconGrid()
    End Sub

    Private Sub CategorySelectedChanged(sender As Object, e As EventArgs)
        Dim item As CategoryItem = TryCast(lstCategories.SelectedItem, CategoryItem)
        _selectedCategory = If(item IsNot Nothing, item.Key, BotEngine.SanitizeBuffIconIdentifier("General"))
        RefreshIconGrid()
    End Sub

    Private Sub SearchTextChanged(sender As Object, e As EventArgs)
        RefreshIconGrid()
    End Sub

    Private Sub AddCategoryClicked(sender As Object, e As EventArgs)
        Dim enteredName As String = Microsoft.VisualBasic.Interaction.InputBox("New category name:", "Add Category", "")
        If String.IsNullOrWhiteSpace(enteredName) Then
            Return
        End If

        Dim safeName As String = BotEngine.SanitizeBuffIconIdentifier(enteredName)
        Directory.CreateDirectory(Path.Combine(BotEngine.BuffIconLibraryRoot, safeName))
        _selectedCategory = safeName
        ReloadLibrary()
    End Sub

    Private Sub RefreshIconGrid()
        flowIcons.SuspendLayout()
        For Each ctrl As Control In flowIcons.Controls
            ctrl.Dispose()
        Next
        flowIcons.Controls.Clear()
        _selectedTile = Nothing

        Dim searchText As String = If(txtSearch IsNot Nothing, txtSearch.Text, "").Trim()
        Dim matching As New List(Of BuffIconLibraryEntry)()
        For Each entry As BuffIconLibraryEntry In _entries
            If Not String.IsNullOrWhiteSpace(searchText) Then
                If entry.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    matching.Add(entry)
                End If
            ElseIf String.Equals(entry.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase) Then
                matching.Add(entry)
            End If
        Next

        For Each entry As BuffIconLibraryEntry In matching
            flowIcons.Controls.Add(BuildIconTile(entry))
        Next
        flowIcons.Controls.Add(BuildAddTile())
        flowIcons.ResumeLayout()
    End Sub

    Private Function BuildIconTile(entry As BuffIconLibraryEntry) As Control
        Dim tile As New TableLayoutPanel() With {.Width = 92, .Height = 112, .Margin = New Padding(5), .RowCount = 2, .ColumnCount = 1, .Tag = entry}
        tile.RowStyles.Add(New RowStyle(SizeType.Absolute, 84.0F))
        tile.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))

        Dim pic As New PictureBox() With {
            .Width = 80,
            .Height = 80,
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BorderStyle = BorderStyle.FixedSingle,
            .BackColor = Color.FromArgb(35, 35, 35),
            .Cursor = Cursors.Hand,
            .Tag = entry,
            .Anchor = AnchorStyles.None
        }
        Dim preview As Image = BotEngine.LoadBuffIconPreview(entry.RelativePath)
        If preview IsNot Nothing Then
            pic.Image = preview
        End If
        AddHandler pic.Click, AddressOf IconTileClicked
        tile.Controls.Add(pic, 0, 0)

        Dim lbl As New Label() With {
            .Text = entry.Name,
            .Width = 92,
            .Height = 22,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.Gainsboro,
            .Font = New Font("Segoe UI", 8.0F),
            .AutoEllipsis = True
        }
        tile.Controls.Add(lbl, 0, 1)
        Return tile
    End Function

    Private Function BuildAddTile() As Control
        Dim tile As New TableLayoutPanel() With {.Width = 92, .Height = 112, .Margin = New Padding(5), .RowCount = 2, .ColumnCount = 1}
        tile.RowStyles.Add(New RowStyle(SizeType.Absolute, 84.0F))
        tile.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))

        Dim addButton As New Button() With {
            .Width = 80,
            .Height = 80,
            .Text = "+",
            .Font = New Font("Segoe UI", 20.0F, FontStyle.Bold),
            .BackColor = Color.FromArgb(45, 95, 140),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Anchor = AnchorStyles.None
        }
        AddHandler addButton.Click, AddressOf AddIconClicked
        tile.Controls.Add(addButton, 0, 0)

        Dim lbl As New Label() With {
            .Text = "Add Icon",
            .Width = 92,
            .Height = 22,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.Gainsboro,
            .Font = New Font("Segoe UI", 8.0F)
        }
        tile.Controls.Add(lbl, 0, 1)
        Return tile
    End Function

    Private Sub IconTileClicked(sender As Object, e As EventArgs)
        Dim pic As PictureBox = TryCast(sender, PictureBox)
        If pic Is Nothing Then
            Return
        End If
        Dim entry As BuffIconLibraryEntry = TryCast(pic.Tag, BuffIconLibraryEntry)
        If entry Is Nothing Then
            Return
        End If

        If _selectedTile IsNot Nothing Then
            _selectedTile.BorderStyle = BorderStyle.FixedSingle
        End If
        pic.BorderStyle = BorderStyle.Fixed3D
        _selectedTile = pic

        SelectedName = entry.Name
        SelectedRelativePath = entry.RelativePath
        btnApply.Enabled = True
    End Sub

    Private Sub AddIconClicked(sender As Object, e As EventArgs)
        Dim menu As New ContextMenuStrip()
        Dim captureItem As New ToolStripMenuItem("Capture from Game")
        AddHandler captureItem.Click, AddressOf CaptureFromGameClicked
        menu.Items.Add(captureItem)
        Dim importItem As New ToolStripMenuItem("Import from File...")
        AddHandler importItem.Click, AddressOf ImportFromFileClicked
        menu.Items.Add(importItem)

        Dim addButton As Button = TryCast(sender, Button)
        If addButton IsNot Nothing Then
            menu.Show(addButton, New DrawingPoint(0, addButton.Height))
        Else
            menu.Show(Cursor.Position)
        End If
    End Sub

    Private Sub CaptureFromGameClicked(sender As Object, e As EventArgs)
        If _gameHwnd = IntPtr.Zero Then
            MessageBox.Show(Me, "Select a Full game process window on the main window first.", "Buff Icon Selector", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        _capturingCategory = _selectedCategory
        _isCapturing = True
        _captureLeftMouseWasDown = False
        lblHint.Text = "Click the buff icon inside the game window to capture it..."
        NativeMethods.SetForegroundWindow(_gameHwnd)
    End Sub

    Private Sub CaptureTimerTick(sender As Object, e As EventArgs)
        Try
            If Not _isCapturing OrElse _gameHwnd = IntPtr.Zero Then
                Return
            End If

            Dim leftDown As Boolean = (GetAsyncKeyState(CInt(Keys.LButton)) And &H8000S) <> 0
            If leftDown AndAlso Not _captureLeftMouseWasDown Then
                Dim screenPoint As NativeMethods.POINT
                If NativeMethods.GetCursorPos(screenPoint) Then
                    Dim hoveredWindow As IntPtr = NativeMethods.WindowFromPoint(screenPoint)
                    Dim hoveredRoot As IntPtr = If(hoveredWindow <> IntPtr.Zero, NativeMethods.GetAncestor(hoveredWindow, NativeMethods.GA_ROOT), IntPtr.Zero)
                    If hoveredRoot <> _gameHwnd Then
                        _captureLeftMouseWasDown = leftDown
                        Return
                    End If

                    Dim clientPoint As NativeMethods.POINT = screenPoint
                    If NativeMethods.ScreenToClient(_gameHwnd, clientPoint) Then
                        Dim clientRect As NativeMethods.RECT
                        If Not NativeMethods.GetClientRect(_gameHwnd, clientRect) Then
                            _captureLeftMouseWasDown = leftDown
                            Return
                        End If

                        Dim clientWidth As Integer = Math.Max(1, clientRect.Right - clientRect.Left)
                        Dim clientHeight As Integer = Math.Max(1, clientRect.Bottom - clientRect.Top)
                        If clientPoint.X < 0 OrElse clientPoint.Y < 0 OrElse clientPoint.X >= clientWidth OrElse clientPoint.Y >= clientHeight Then
                            _captureLeftMouseWasDown = leftDown
                            Return
                        End If

                        _isCapturing = False
                        _captureLeftMouseWasDown = leftDown
                        FinishCapture(clientPoint.X, clientPoint.Y)
                    End If
                End If
            End If

            _captureLeftMouseWasDown = leftDown
        Catch
            _isCapturing = False
        End Try
    End Sub

    Private Sub FinishCapture(clientX As Integer, clientY As Integer)
        Dim size As Integer = CInt(nudIconSize.Value)
        Dim half As Integer = size \ 2
        Dim region As New RectRegion(clientX - half, clientY - half, size, size)
        Using crop As Bitmap = BotEngine.CaptureClientRegion(_gameHwnd, region)
            lblHint.Text = DefaultHintText
            If crop Is Nothing Then
                MessageBox.Show(Me, "Unable to capture the icon at that location.", "Buff Icon Selector", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not ConfirmCapturedIcon(crop) Then
                Return
            End If

            Dim enteredName As String = Microsoft.VisualBasic.Interaction.InputBox("Name this buff icon:", "Add Icon", "")
            If String.IsNullOrWhiteSpace(enteredName) Then
                Return
            End If

            Dim relativePath As String = BotEngine.SaveBuffIconToLibrary(_capturingCategory, enteredName, crop)
            _selectedCategory = _capturingCategory
            ReloadLibrary()
            SelectMatchingTile(relativePath)
        End Using
    End Sub

    ' Shows the just-captured crop enlarged so the user can visually confirm it's a clean, complete icon
    ' with no sliver of a neighboring icon bled in at an edge - a contaminated reference stops matching
    ' the moment any OTHER buff in the row changes order, which looks like "it keeps recasting even
    ' though the buff is clearly still there."
    Private Function ConfirmCapturedIcon(crop As Bitmap) As Boolean
        Using confirmForm As New Form() With {
            .Text = "Confirm Captured Icon",
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(260, 260),
            .MinimumSize = New Size(260, 260),
            .MaximumSize = New Size(260, 260),
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .BackColor = Color.FromArgb(25, 25, 25),
            .ForeColor = Color.Gainsboro
        }
            Dim pic As New PictureBox() With {
                .Dock = DockStyle.Top,
                .Height = 160,
                .SizeMode = PictureBoxSizeMode.Zoom,
                .BorderStyle = BorderStyle.FixedSingle,
                .BackColor = Color.FromArgb(35, 35, 35),
                .Image = crop
            }
            confirmForm.Controls.Add(pic)

            Dim lbl As New Label() With {
                .Text = "Does this show exactly one complete buff icon, with no piece of a neighboring icon?",
                .Dock = DockStyle.Top,
                .Height = 40,
                .TextAlign = ContentAlignment.MiddleCenter,
                .ForeColor = Color.LightSteelBlue
            }
            confirmForm.Controls.Add(lbl)
            lbl.BringToFront()

            Dim buttonRow As New FlowLayoutPanel() With {.Dock = DockStyle.Bottom, .FlowDirection = FlowDirection.RightToLeft, .Height = 40}
            Dim btnRetry As New Button() With {.Text = "Retry", .AutoSize = True, .DialogResult = DialogResult.Cancel, .BackColor = Color.FromArgb(120, 45, 45), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
            Dim btnUse As New Button() With {.Text = "Use This", .AutoSize = True, .DialogResult = DialogResult.OK, .BackColor = Color.FromArgb(30, 120, 80), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
            buttonRow.Controls.Add(btnRetry)
            buttonRow.Controls.Add(btnUse)
            confirmForm.Controls.Add(buttonRow)
            buttonRow.BringToFront()
            confirmForm.AcceptButton = btnUse
            confirmForm.CancelButton = btnRetry

            Return confirmForm.ShowDialog(Me) = DialogResult.OK
        End Using
    End Function

    Private Sub ImportFromFileClicked(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "Import buff icon"
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
            dialog.CheckFileExists = True
            dialog.Multiselect = False

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try
                Dim size As Integer = CInt(nudIconSize.Value)
                Using sourceImage As Image = Image.FromFile(dialog.FileName)
                    Using resized As New Bitmap(sourceImage, New Size(size, size))
                        If Not ConfirmCapturedIcon(resized) Then
                            Return
                        End If

                        Dim enteredName As String = Microsoft.VisualBasic.Interaction.InputBox("Name this buff icon:", "Add Icon", Path.GetFileNameWithoutExtension(dialog.FileName))
                        If String.IsNullOrWhiteSpace(enteredName) Then
                            Return
                        End If

                        Dim relativePath As String = BotEngine.SaveBuffIconToLibrary(_selectedCategory, enteredName, resized)
                        ReloadLibrary()
                        SelectMatchingTile(relativePath)
                    End Using
                End Using
            Catch ex As Exception
                MessageBox.Show(Me, $"Unable to import that image: {ex.Message}", "Buff Icon Selector", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Using
    End Sub

    Private Sub SelectMatchingTile(relativePath As String)
        For Each ctrl As Control In flowIcons.Controls
            Dim entry As BuffIconLibraryEntry = TryCast(ctrl.Tag, BuffIconLibraryEntry)
            If entry IsNot Nothing AndAlso String.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase) Then
                For Each child As Control In ctrl.Controls
                    Dim pic As PictureBox = TryCast(child, PictureBox)
                    If pic IsNot Nothing Then
                        IconTileClicked(pic, EventArgs.Empty)
                    End If
                Next
                Exit For
            End If
        Next
    End Sub

    Private Sub ApplyClicked(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(SelectedRelativePath) Then
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
