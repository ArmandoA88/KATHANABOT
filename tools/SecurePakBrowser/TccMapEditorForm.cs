namespace KathanaSecurePakBrowser;

public sealed class TccMapEditorForm : Form
{
    private readonly TccMapDocument document;
    private readonly TccMapCanvas canvas = new();
    private readonly Panel mapPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(24, 24, 24) };
    private readonly NumericUpDown xInput = new() { Minimum = 0, Maximum = ushort.MaxValue, Width = 85 };
    private readonly NumericUpDown yInput = new() { Minimum = 0, Maximum = ushort.MaxValue, Width = 85 };
    private readonly NumericUpDown mapValueInput = new() { Minimum = 0, Maximum = ushort.MaxValue, Width = 105 };
    private readonly NumericUpDown flagsInput = new() { Minimum = 0, Maximum = ushort.MaxValue, Width = 105, Hexadecimal = true };
    private readonly ComboBox viewModeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
    private readonly ComboBox zoomBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly ComboBox flagsPresetBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 185 };
    private readonly NumericUpDown brushSizeInput = new() { Minimum = 1, Maximum = 31, Increment = 2, Value = 1, Width = 60 };
    private readonly CheckBox paintModeBox = new() { Text = "Paint mode", AutoSize = true };
    private readonly Button undoButton = new() { Text = "Undo", AutoSize = true, Enabled = false };
    private readonly Button redoButton = new() { Text = "Redo", AutoSize = true, Enabled = false };
    private readonly Label selectedLabel = new() { AutoSize = true, Text = "No cell selected" };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Stack<List<CellChange>> undoStack = new();
    private readonly Stack<List<CellChange>> redoStack = new();
    private readonly HashSet<int> modifiedCells = new();
    private Dictionary<int, CellChange>? activeStroke;
    private int selectedX = -1;
    private int selectedY = -1;
    private bool painting;
    private bool allowClose;

    public TccMapEditorForm(SecurePakEntry entry, TccMapDocument document)
    {
        this.document = document;
        Text = $"TANTRA_MAP Editor — {entry.Path}";
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        BuildInterface();
        canvas.LoadDocument(document);
        SelectAndSample(0, 0, center: false);
        FormClosing += OnFormClosing;
        KeyDown += OnEditorKeyDown;
    }

    public byte[]? ResultContent { get; private set; }

    private void BuildInterface()
    {
        viewModeBox.Items.AddRange(["Flags", "Map value"]);
        viewModeBox.SelectedIndex = 0;
        zoomBox.Items.AddRange(["1x", "2x", "4x", "8x", "16x"]);
        zoomBox.SelectedIndex = 0;
        flagsPresetBox.Items.AddRange([
            "Observed: 0x0000",
            "Observed: 0x0010",
            "Observed: 0x4000",
            "Observed: 0x4010",
            "Custom"
        ]);
        flagsPresetBox.SelectedIndex = 1;

        FlowLayoutPanel toolbar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6, 5, 6, 5),
            WrapContents = false
        };
        toolbar.Controls.Add(undoButton);
        toolbar.Controls.Add(redoButton);
        toolbar.Controls.Add(Spacer());
        toolbar.Controls.Add(new Label { Text = "View:", AutoSize = true, Margin = new Padding(5, 7, 2, 0) });
        toolbar.Controls.Add(viewModeBox);
        toolbar.Controls.Add(new Label { Text = "Zoom:", AutoSize = true, Margin = new Padding(10, 7, 2, 0) });
        toolbar.Controls.Add(zoomBox);
        toolbar.Controls.Add(Spacer());
        toolbar.Controls.Add(paintModeBox);
        toolbar.Controls.Add(new Label { Text = "Brush:", AutoSize = true, Margin = new Padding(10, 7, 2, 0) });
        toolbar.Controls.Add(brushSizeInput);
        toolbar.Controls.Add(new Label
        {
            Text = "Left: select/paint   Right: sample",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(14, 7, 0, 0)
        });

        mapPanel.Controls.Add(canvas);
        Panel inspector = BuildInspector();
        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 930,
            FixedPanel = FixedPanel.Panel2
        };
        split.Panel1.Controls.Add(mapPanel);
        split.Panel2.Controls.Add(inspector);

        Panel statusPanel = new() { Dock = DockStyle.Bottom, Height = 26, Padding = new Padding(7, 2, 7, 2) };
        statusPanel.Controls.Add(statusLabel);
        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(statusPanel);

        undoButton.Click += (_, _) => Undo();
        redoButton.Click += (_, _) => Redo();
        viewModeBox.SelectedIndexChanged += (_, _) => canvas.SetViewMode(
            viewModeBox.SelectedIndex == 0 ? TccMapViewMode.Flags : TccMapViewMode.MapValue);
        zoomBox.SelectedIndexChanged += (_, _) =>
        {
            canvas.SetZoom(1 << zoomBox.SelectedIndex);
            CenterSelectedCell();
        };
        flagsPresetBox.SelectedIndexChanged += (_, _) =>
        {
            ushort? preset = flagsPresetBox.SelectedIndex switch
            {
                0 => 0x0000,
                1 => 0x0010,
                2 => 0x4000,
                3 => 0x4010,
                _ => null
            };
            if (preset.HasValue) flagsInput.Value = preset.Value;
        };
        flagsInput.ValueChanged += (_, _) =>
        {
            ushort value = (ushort)flagsInput.Value;
            int presetIndex = value switch { 0x0000 => 0, 0x0010 => 1, 0x4000 => 2, 0x4010 => 3, _ => 4 };
            if (flagsPresetBox.SelectedIndex != presetIndex) flagsPresetBox.SelectedIndex = presetIndex;
        };
        canvas.CellMouseDown += OnCellMouseDown;
        canvas.CellMouseMove += OnCellMouseMove;
        canvas.CellMouseUp += OnCellMouseUp;
    }

    private Panel BuildInspector()
    {
        Panel panel = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
        TableLayoutPanel fields = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 0
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label header = new()
        {
            Text = $"{document.Signature} v{document.Version}\r\n" +
                   $"Layout {document.LayoutVersion} | {document.Width} × {document.Height}\r\n" +
                   $"{document.CellCount:N0} cells\r\n{document.Created}",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 14)
        };
        panel.Controls.Add(fields);
        panel.Controls.Add(header);
        header.Dock = DockStyle.Top;

        AddRow(fields, "Selected", selectedLabel);
        AddRow(fields, "Go to X", xInput);
        AddRow(fields, "Go to Y", yInput);
        Button goButton = new() { Text = "Go to cell", AutoSize = true };
        goButton.Click += (_, _) => SelectAndSample((int)xInput.Value, (int)yInput.Value, center: true);
        AddRow(fields, string.Empty, goButton);
        AddRow(fields, "Map value", mapValueInput);
        AddRow(fields, "Flags (hex)", flagsInput);
        AddRow(fields, "Flag preset", flagsPresetBox);

        Button applyCellButton = new() { Text = "Apply values to selected cell", AutoSize = true };
        applyCellButton.Click += (_, _) => ApplyToSelectedCell();
        AddRow(fields, string.Empty, applyCellButton);

        Label legend = new()
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Margin = new Padding(0, 18, 0, 8),
            Text = "Flags view colors:\r\n" +
                   "■ Blue   0x0000\r\n" +
                   "■ Dark   0x0010\r\n" +
                   "■ Green  0x4000\r\n" +
                   "■ Orange 0x4010\r\n" +
                   "■ Magenta other\r\n\r\n" +
                   "Flag meanings are not named because their gameplay behavior has not been proven. " +
                   "Sample known cells before painting."
        };
        AddRow(fields, string.Empty, legend);

        Button applyButton = new() { Text = "Apply map changes", AutoSize = true };
        Button cancelButton = new() { Text = "Cancel", AutoSize = true };
        applyButton.Click += (_, _) =>
        {
            ResultContent = document.Serialize();
            allowClose = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) => Close();
        FlowLayoutPanel bottom = new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        bottom.Controls.Add(cancelButton);
        bottom.Controls.Add(applyButton);
        panel.Controls.Add(bottom);
        return panel;
    }

    private void OnCellMouseDown(object? sender, TccMapCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            SelectAndSample(e.X, e.Y, center: false);
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        if (!paintModeBox.Checked)
        {
            SelectAndSample(e.X, e.Y, center: false);
            return;
        }

        painting = true;
        activeStroke = new Dictionary<int, CellChange>();
        PaintBrush(e.X, e.Y);
    }

    private void OnCellMouseMove(object? sender, TccMapCellMouseEventArgs e)
    {
        TccMapCell hover = document.GetCell(e.X, e.Y);
        statusLabel.Text = $"Hover ({e.X}, {e.Y}) | value {hover.MapValue} | flags 0x{hover.Flags:X4} | " +
                           $"{modifiedCells.Count:N0} changed cells";
        if (painting && (Control.MouseButtons & MouseButtons.Left) != 0) PaintBrush(e.X, e.Y);
    }

    private void OnCellMouseUp(object? sender, TccMapCellMouseEventArgs e)
    {
        if (!painting) return;
        painting = false;
        CommitActiveStroke();
    }

    private void SelectAndSample(int x, int y, bool center)
    {
        selectedX = x;
        selectedY = y;
        canvas.SelectCell(x, y);
        TccMapCell cell = document.GetCell(x, y);
        xInput.Maximum = document.Width - 1;
        yInput.Maximum = document.Height - 1;
        xInput.Value = x;
        yInput.Value = y;
        mapValueInput.Value = cell.MapValue;
        flagsInput.Value = cell.Flags;
        selectedLabel.Text = $"({x}, {y})";
        statusLabel.Text = $"Selected ({x}, {y}) | value {cell.MapValue} | flags 0x{cell.Flags:X4} | " +
                           $"{modifiedCells.Count:N0} changed cells";
        if (center) CenterSelectedCell();
    }

    private void ApplyToSelectedCell()
    {
        if (selectedX < 0 || selectedY < 0) return;
        activeStroke = new Dictionary<int, CellChange>();
        PaintCell(selectedX, selectedY, (ushort)mapValueInput.Value, (ushort)flagsInput.Value);
        CommitActiveStroke();
        SelectAndSample(selectedX, selectedY, center: false);
    }

    private void PaintBrush(int centerX, int centerY)
    {
        int size = (int)brushSizeInput.Value;
        if ((size & 1) == 0) size++;
        int radius = size / 2;
        ushort mapValue = (ushort)mapValueInput.Value;
        ushort cellFlags = (ushort)flagsInput.Value;
        for (int y = Math.Max(0, centerY - radius); y <= Math.Min(document.Height - 1, centerY + radius); y++)
        for (int x = Math.Max(0, centerX - radius); x <= Math.Min(document.Width - 1, centerX + radius); x++)
            PaintCell(x, y, mapValue, cellFlags);

        selectedX = centerX;
        selectedY = centerY;
        canvas.SelectCell(centerX, centerY);
        selectedLabel.Text = $"({centerX}, {centerY})";
        statusLabel.Text = $"Painting value {mapValue}, flags 0x{cellFlags:X4} | " +
                           $"{modifiedCells.Count:N0} changed cells";
    }

    private void PaintCell(int x, int y, ushort mapValue, ushort cellFlags)
    {
        int index = y * document.Width + x;
        TccMapCell old = document.GetCell(x, y);
        if (old.MapValue == mapValue && old.Flags == cellFlags) return;

        if (!activeStroke!.TryGetValue(index, out CellChange change))
        {
            change = new CellChange(index, x, y, old.MapValue, old.Flags, mapValue, cellFlags);
        }
        else
        {
            change = change with { NewValue = mapValue, NewFlags = cellFlags };
        }
        activeStroke[index] = change;
        document.SetCell(x, y, mapValue, cellFlags);
        UpdateModifiedState(index, x, y);
        canvas.RefreshCell(x, y);
    }

    private void CommitActiveStroke()
    {
        if (activeStroke is { Count: > 0 })
        {
            undoStack.Push(activeStroke.Values.ToList());
            redoStack.Clear();
        }
        activeStroke = null;
        UpdateUndoButtons();
    }

    private void Undo()
    {
        if (undoStack.Count == 0) return;
        List<CellChange> changes = undoStack.Pop();
        foreach (CellChange change in changes)
        {
            document.SetCell(change.X, change.Y, change.OldValue, change.OldFlags);
            UpdateModifiedState(change.Index, change.X, change.Y);
            canvas.RefreshCell(change.X, change.Y);
        }
        redoStack.Push(changes);
        UpdateUndoButtons();
        if (selectedX >= 0) SelectAndSample(selectedX, selectedY, center: false);
    }

    private void Redo()
    {
        if (redoStack.Count == 0) return;
        List<CellChange> changes = redoStack.Pop();
        foreach (CellChange change in changes)
        {
            document.SetCell(change.X, change.Y, change.NewValue, change.NewFlags);
            UpdateModifiedState(change.Index, change.X, change.Y);
            canvas.RefreshCell(change.X, change.Y);
        }
        undoStack.Push(changes);
        UpdateUndoButtons();
        if (selectedX >= 0) SelectAndSample(selectedX, selectedY, center: false);
    }

    private void UpdateModifiedState(int index, int x, int y)
    {
        if (document.IsCellModified(x, y)) modifiedCells.Add(index);
        else modifiedCells.Remove(index);
    }

    private void UpdateUndoButtons()
    {
        undoButton.Enabled = undoStack.Count > 0;
        redoButton.Enabled = redoStack.Count > 0;
        statusLabel.Text = $"{modifiedCells.Count:N0} changed cells | undo {undoStack.Count:N0} | redo {redoStack.Count:N0}";
    }

    private void CenterSelectedCell()
    {
        if (selectedX < 0 || selectedY < 0) return;
        int targetX = Math.Max(0, selectedX * canvas.Zoom - mapPanel.ClientSize.Width / 2);
        int targetY = Math.Max(0, selectedY * canvas.Zoom - mapPanel.ClientSize.Height / 2);
        mapPanel.AutoScrollPosition = new Point(targetX, targetY);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            Redo();
            e.SuppressKeyPress = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || modifiedCells.Count == 0) return;
        if (MessageBox.Show(this,
                $"Discard changes to {modifiedCells.Count:N0} map cell(s)?",
                "Unsaved TCC map changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private static Control Spacer() => new Label { Width = 12, AutoSize = false };

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 7)
        }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, row);
    }

    private readonly record struct CellChange(
        int Index,
        int X,
        int Y,
        ushort OldValue,
        ushort OldFlags,
        ushort NewValue,
        ushort NewFlags);
}
