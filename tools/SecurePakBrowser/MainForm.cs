using System.Text;

namespace KathanaSecurePakBrowser;

internal sealed class MainForm : Form
{
    private const int MaxReplacementSize = 1024 * 1024 * 1024;
    private const int MaxTextEditorSize = 32 * 1024 * 1024;
    private readonly string? initialPath;
    private readonly TreeView folderTree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ListView fileList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false,
        MultiSelect = true
    };
    private readonly TextBox searchBox = new() { Width = 280, PlaceholderText = "Search paths..." };
    private readonly TextBox detailsBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = SystemColors.Window
    };
    private readonly ToolStripStatusLabel statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripProgressBar progressBar = new() { Visible = false, Width = 180 };
    private readonly ToolStripButton extractSelectedButton = new("Extract selected") { Enabled = false };
    private readonly ToolStripButton extractAllButton = new("Extract all") { Enabled = false };
    private readonly ToolStripButton replaceSelectedButton = new("Replace file...") { Enabled = false };
    private readonly ToolStripButton editTccButton = new("Edit TCC map...") { Enabled = false };
    private readonly ToolStripButton editTextButton = new("Edit text...") { Enabled = false };
    private readonly ToolStripButton revertButton = new("Revert selected") { Enabled = false };
    private readonly ToolStripButton saveAsButton = new("Save modified PAK...") { Enabled = false };
    private readonly Dictionary<int, byte[]> replacements = new();
    private SecurePakArchive? archive;
    private bool busy;

    public MainForm(string? initialPath)
    {
        this.initialPath = initialPath;
        Text = "Kathana SecurePak Editor";
        Width = 1220;
        Height = 760;
        MinimumSize = new Size(860, 520);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        BuildInterface();
        Shown += async (_, _) => await OpenInitialArchiveAsync();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) => archive?.Dispose();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
    }

    private void BuildInterface()
    {
        ToolStrip toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(5, 3, 5, 3) };
        ToolStripButton openButton = new("Open data.pak");
        openButton.Click += async (_, _) => await ChooseArchiveAsync();
        extractSelectedButton.Click += async (_, _) => await ExtractSelectedAsync();
        extractAllButton.Click += async (_, _) => await ExtractAllAsync();
        replaceSelectedButton.Click += async (_, _) => await ReplaceSelectedAsync();
        editTccButton.Click += async (_, _) => await EditSelectedTccAsync();
        editTextButton.Click += async (_, _) => await EditSelectedTextAsync();
        revertButton.Click += (_, _) => RevertSelected();
        saveAsButton.Click += async (_, _) => await SaveArchiveAsAsync();
        ToolStripControlHost searchHost = new(searchBox) { Margin = new Padding(12, 0, 4, 0) };
        toolbar.Items.AddRange([
            openButton,
            new ToolStripSeparator(),
            extractSelectedButton,
            extractAllButton,
            new ToolStripSeparator(),
            replaceSelectedButton,
            editTccButton,
            editTextButton,
            revertButton,
            saveAsButton,
            new ToolStripSeparator(),
            new ToolStripLabel("Find:"),
            searchHost,
            new ToolStripLabel("Source stays unchanged until Save As") { ForeColor = Color.DarkGreen }
        ]);

        fileList.Columns.Add("Name", 270);
        fileList.Columns.Add("Folder", 340);
        fileList.Columns.Add("Original size", 110, HorizontalAlignment.Right);
        fileList.Columns.Add("Stored size", 110, HorizontalAlignment.Right);
        fileList.Columns.Add("Method", 85);
        fileList.Columns.Add("CRC32", 90);
        fileList.Columns.Add("Status", 80);

        SplitContainer rightSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 475 };
        rightSplit.Panel1.Controls.Add(fileList);
        rightSplit.Panel2.Controls.Add(detailsBox);
        SplitContainer mainSplit = new() { Dock = DockStyle.Fill, SplitterDistance = 300 };
        mainSplit.Panel1.Controls.Add(folderTree);
        mainSplit.Panel2.Controls.Add(rightSplit);

        StatusStrip statusStrip = new();
        statusStrip.Items.Add(statusLabel);
        statusStrip.Items.Add(progressBar);
        Controls.Add(mainSplit);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        folderTree.AfterSelect += (_, _) => RefreshFileList();
        searchBox.TextChanged += (_, _) => RefreshFileList();
        fileList.SelectedIndexChanged += (_, _) => UpdateSelection();
        fileList.DoubleClick += async (_, _) => await OpenSelectedEntryAsync();
    }

    private async Task OpenInitialArchiveAsync()
    {
        string? candidate = initialPath;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            string besideExecutable = Path.Combine(AppContext.BaseDirectory, "data.pak");
            string inWorkingDirectory = Path.Combine(Environment.CurrentDirectory, "data.pak");
            candidate = File.Exists(besideExecutable) ? besideExecutable :
                File.Exists(inWorkingDirectory) ? inWorkingDirectory : null;
        }
        if (candidate is not null && File.Exists(candidate))
        {
            await LoadArchiveAsync(candidate);
        }
        else
        {
            statusLabel.Text = "Open or drag in the data.pak that belongs to KathanaGame.exe.";
        }
    }

    private async Task ChooseArchiveAsync()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "SecurePak archives (*.pak)|*.pak|All files (*.*)|*.*",
            Title = "Open Kathana data.pak",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadArchiveAsync(dialog.FileName);
        }
    }

    private async Task LoadArchiveAsync(string path, bool discardChanges = false)
    {
        if (busy) return;
        if (!discardChanges && replacements.Count > 0 && MessageBox.Show(this,
                $"Discard {replacements.Count:N0} unsaved modification(s) and open another archive?",
                "Unsaved modifications", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        SetBusy(true, "Authenticating header and decoding filenames...");
        try
        {
            SecurePakArchive loaded = await Task.Run(() => SecurePakArchive.Open(path));
            SecurePakArchive? previous = archive;
            archive = loaded;
            previous?.Dispose();
            replacements.Clear();
            BuildFolderTree();
            extractAllButton.Enabled = true;
            UpdateArchiveCaption();
            statusLabel.Text = $"{loaded.Entries.Count:N0} files | SecurePak v{loaded.Version} | " +
                $"index {FormatSize(loaded.IndexSize)} | source protected";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not open SecurePak",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Archive was not opened.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BuildFolderTree()
    {
        folderTree.BeginUpdate();
        folderTree.Nodes.Clear();
        TreeNode root = new(Path.GetFileName(archive!.FilePath)) { Tag = string.Empty };
        folderTree.Nodes.Add(root);
        Dictionary<string, TreeNode> nodes = new(StringComparer.OrdinalIgnoreCase) { [string.Empty] = root };

        foreach (SecurePakEntry entry in archive.Entries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            string[] parts = entry.Folder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;
            foreach (string part in parts)
            {
                string next = current.Length == 0 ? part : current + "/" + part;
                if (!nodes.ContainsKey(next))
                {
                    TreeNode node = new(part) { Tag = next };
                    nodes[current].Nodes.Add(node);
                    nodes[next] = node;
                }
                current = next;
            }
        }
        root.Expand();
        folderTree.SelectedNode = root;
        folderTree.EndUpdate();
    }

    private void RefreshFileList()
    {
        if (archive is null) return;
        string folder = folderTree.SelectedNode?.Tag as string ?? string.Empty;
        string query = searchBox.Text.Trim();
        bool rootSelected = folder.Length == 0;

        IEnumerable<SecurePakEntry> entries = archive.Entries;
        if (!rootSelected)
        {
            string prefix = folder + "/";
            entries = entries.Where(entry => entry.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                entry.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Length != 0)
        {
            entries = entries.Where(entry => entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        fileList.BeginUpdate();
        fileList.Items.Clear();
        foreach (SecurePakEntry entry in entries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            bool modified = replacements.TryGetValue(entry.Index, out byte[]? replacement);
            ListViewItem item = new(entry.FileName) { Tag = entry };
            item.SubItems.Add(entry.Folder);
            item.SubItems.Add(FormatSize(modified ? checked((ulong)replacement!.Length) : entry.OriginalSize));
            item.SubItems.Add(modified ? "on save" : FormatSize(entry.StoredSize));
            item.SubItems.Add(modified ? "Rebuild" : entry.IsCompressed ? "LZ4" : "Stored");
            item.SubItems.Add(modified
                ? SecurePakArchive.ComputeContentCrc32(replacement!).ToString("X8")
                : entry.Crc32.ToString("X8"));
            item.SubItems.Add(modified ? "Modified" : string.Empty);
            if (modified)
            {
                item.ForeColor = Color.DarkOrange;
                item.Font = new Font(fileList.Font, FontStyle.Bold);
            }
            fileList.Items.Add(item);
        }
        fileList.EndUpdate();
        statusLabel.Text = $"Showing {fileList.Items.Count:N0} of {archive.Entries.Count:N0} files | " +
            $"{replacements.Count:N0} modified";
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        bool hasSelection = archive is not null && fileList.SelectedItems.Count > 0;
        bool hasSingleSelection = fileList.SelectedItems.Count == 1;
        bool selectedIsTcc = GetSelectedEntry() is SecurePakEntry selectedEntry &&
            string.Equals(Path.GetExtension(selectedEntry.Path), ".tcc", StringComparison.OrdinalIgnoreCase);
        extractSelectedButton.Enabled = !busy && hasSelection;
        replaceSelectedButton.Enabled = !busy && hasSingleSelection;
        editTccButton.Enabled = !busy && selectedIsTcc;
        editTextButton.Enabled = !busy && hasSingleSelection && !selectedIsTcc;
        revertButton.Enabled = !busy && fileList.SelectedItems.Cast<ListViewItem>()
            .Any(item => item.Tag is SecurePakEntry selected && replacements.ContainsKey(selected.Index));
        saveAsButton.Enabled = !busy && archive is not null && replacements.Count > 0;
        if (fileList.SelectedItems.Count != 1 || fileList.SelectedItems[0].Tag is not SecurePakEntry entry)
        {
            detailsBox.Text = fileList.SelectedItems.Count > 1 ? $"{fileList.SelectedItems.Count:N0} files selected." : string.Empty;
            return;
        }
        bool modified = replacements.TryGetValue(entry.Index, out byte[]? replacement);
        uint currentCrc = modified ? SecurePakArchive.ComputeContentCrc32(replacement!) : entry.Crc32;
        ulong currentSize = modified ? checked((ulong)replacement!.Length) : entry.OriginalSize;
        detailsBox.Text = string.Join(Environment.NewLine, [
            $"Path: {entry.Path}",
            $"Index: {entry.Index:N0}",
            $"Status: {(modified ? "modified in editor" : "unchanged")}",
            $"Current size: {currentSize:N0} bytes",
            $"Archive size: {entry.OriginalSize:N0} bytes",
            $"Stored size: {(modified ? "calculated during Save As" : $"{entry.StoredSize:N0} bytes")}",
            $"Compression: {(modified ? "rebuilt during Save As" : entry.IsCompressed ? "LZ4" : "none")}",
            $"Flags: 0x{entry.Flags:X4}",
            $"Data offset: 0x{(archive!.DataOffset + entry.RelativeOffset):X}",
            $"Name hash: 0x{entry.NameHash:X8}",
            $"Current CRC32: {currentCrc:X8}"
        ]);
    }

    private async Task ExtractSelectedAsync()
    {
        if (archive is null || fileList.SelectedItems.Count == 0 || busy) return;
        List<SecurePakEntry> selected = fileList.SelectedItems.Cast<ListViewItem>()
            .Select(item => (SecurePakEntry)item.Tag!).ToList();

        if (selected.Count == 1)
        {
            SecurePakEntry entry = selected[0];
            using SaveFileDialog dialog = new()
            {
                FileName = entry.FileName,
                Title = "Extract file (the archive remains unchanged)",
                Filter = "All files (*.*)|*.*",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await ExtractOneAsync(entry, dialog.FileName);
            return;
        }

        using FolderBrowserDialog folderDialog = new()
        {
            Description = "Choose a folder for the selected files. Archive folders will be preserved.",
            UseDescriptionForTitle = true
        };
        if (folderDialog.ShowDialog(this) != DialogResult.OK) return;
        await ExtractEntriesAsync(selected, folderDialog.SelectedPath);
    }

    private async Task ExtractAllAsync()
    {
        if (archive is null || busy) return;
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose a folder for all files. The data.pak archive will not be modified.",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await ExtractEntriesAsync(archive.Entries, dialog.SelectedPath);
        }
    }

    private async Task ExtractOneAsync(SecurePakEntry entry, string destination)
    {
        SetBusy(true, $"Extracting {entry.Path}...");
        try
        {
            await Task.Run(() => WriteCurrentEntry(entry, destination));
            statusLabel.Text = $"Extracted the current version of {entry.Path}.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Extraction failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExtractEntriesAsync(IReadOnlyCollection<SecurePakEntry> entries, string destinationRoot)
    {
        SetBusy(true, $"Extracting {entries.Count:N0} files...");
        progressBar.Visible = true;
        progressBar.Minimum = 0;
        progressBar.Maximum = entries.Count;
        progressBar.Value = 0;
        try
        {
            int completed = 0;
            IProgress<int> progress = new Progress<int>(value =>
            {
                progressBar.Value = value;
                statusLabel.Text = $"Extracting {value:N0} / {entries.Count:N0} files...";
            });
            await Task.Run(() =>
            {
                foreach (SecurePakEntry entry in entries)
                {
                    string destination = archive!.GetSafeExtractionPath(destinationRoot, entry);
                    WriteCurrentEntry(entry, destination);
                    progress.Report(++completed);
                }
            });
            statusLabel.Text = $"Extracted {completed:N0} current file versions.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Extraction stopped",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            progressBar.Visible = false;
            SetBusy(false);
        }
    }

    private async Task OpenSelectedEntryAsync()
    {
        SecurePakEntry? entry = GetSelectedEntry();
        if (entry is not null && string.Equals(Path.GetExtension(entry.Path), ".tcc", StringComparison.OrdinalIgnoreCase))
        {
            await EditSelectedTccAsync();
        }
        else
        {
            await PreviewSelectedAsync();
        }
    }

    private async Task PreviewSelectedAsync()
    {
        if (archive is null || fileList.SelectedItems.Count != 1 || busy ||
            fileList.SelectedItems[0].Tag is not SecurePakEntry entry)
        {
            return;
        }
        SetBusy(true, $"Reading and verifying {entry.Path}...");
        try
        {
            byte[] content = await Task.Run(() => ReadCurrentEntry(entry));
            using PreviewForm preview = new(entry, content);
            preview.ShowDialog(this);
            statusLabel.Text = $"Previewed the current version of {entry.Path}.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Preview failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ReplaceSelectedAsync()
    {
        if (archive is null || busy || GetSelectedEntry() is not SecurePakEntry entry) return;
        using OpenFileDialog dialog = new()
        {
            Title = $"Choose replacement for {entry.Path}",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        FileInfo source = new(dialog.FileName);
        if (source.Length > MaxReplacementSize)
        {
            MessageBox.Show(this, $"Replacement files are limited to {FormatSize(MaxReplacementSize)}.",
                "Replacement is too large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true, $"Reading replacement for {entry.Path}...");
        try
        {
            byte[] content = await File.ReadAllBytesAsync(dialog.FileName);
            ApplyReplacement(entry, content);
            statusLabel.Text = $"Replaced {entry.Path} in the editor. Use Save modified PAK to rebuild the archive.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not replace file",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EditSelectedTccAsync()
    {
        if (archive is null || busy || GetSelectedEntry() is not SecurePakEntry entry ||
            !string.Equals(Path.GetExtension(entry.Path), ".tcc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetBusy(true, $"Parsing TANTRA_MAP data from {entry.Path}...");
        try
        {
            byte[] content = await Task.Run(() => ReadCurrentEntry(entry));
            TccMapDocument map = await Task.Run(() => TccMapDocument.Parse(content));
            SetBusy(false);
            using TccMapEditorForm editor = new(entry, map);
            if (editor.ShowDialog(this) == DialogResult.OK && editor.ResultContent is byte[] edited)
            {
                ApplyReplacement(entry, edited);
                statusLabel.Text = $"Edited TANTRA_MAP {entry.Path}. Use Save modified PAK to rebuild the archive.";
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not edit TCC map",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EditSelectedTextAsync()
    {
        if (archive is null || busy || GetSelectedEntry() is not SecurePakEntry entry) return;
        SetBusy(true, $"Opening {entry.Path} for text editing...");
        try
        {
            byte[] content = await Task.Run(() => ReadCurrentEntry(entry));
            if (content.Length > MaxTextEditorSize)
            {
                MessageBox.Show(this,
                    $"The built-in text editor is limited to {FormatSize(MaxTextEditorSize)}. Use Replace file instead.",
                    "File is too large", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!TextFileCodec.TryDecode(content, out string text, out TextFileEncoding encoding))
            {
                MessageBox.Show(this,
                    "This file is not valid UTF-8/UTF-16/UTF-32 text. Use Replace file to modify it with an external editor.",
                    "Binary or unsupported text encoding", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(false);
            using TextEditorForm editor = new(entry, text, encoding);
            if (editor.ShowDialog(this) == DialogResult.OK && editor.ResultContent is byte[] edited)
            {
                ApplyReplacement(entry, edited);
                statusLabel.Text = $"Edited {entry.Path}. Use Save modified PAK to rebuild the archive.";
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not edit file",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RevertSelected()
    {
        if (busy) return;
        int reverted = 0;
        foreach (ListViewItem item in fileList.SelectedItems)
        {
            if (item.Tag is SecurePakEntry entry && replacements.Remove(entry.Index)) reverted++;
        }
        if (reverted == 0) return;
        UpdateArchiveCaption();
        RefreshFileList();
        statusLabel.Text = $"Reverted {reverted:N0} modification(s). {replacements.Count:N0} remain.";
    }

    private async Task SaveArchiveAsAsync()
    {
        if (archive is null || busy || replacements.Count == 0) return;
        string sourceDirectory = Path.GetDirectoryName(archive.FilePath) ?? Environment.CurrentDirectory;
        string sourceName = Path.GetFileNameWithoutExtension(archive.FilePath);
        using SaveFileDialog dialog = new()
        {
            Title = "Save rebuilt SecurePak archive",
            Filter = "SecurePak archives (*.pak)|*.pak|All files (*.*)|*.*",
            InitialDirectory = sourceDirectory,
            FileName = sourceName + ".modified.pak",
            DefaultExt = "pak",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string destination = Path.GetFullPath(dialog.FileName);
        if (string.Equals(destination, archive.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                "Choose a different filename. The editor protects the currently open source archive from direct overwrite.",
                "Source archive is protected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Dictionary<int, byte[]> snapshot = replacements.ToDictionary(pair => pair.Key, pair => pair.Value);
        SetBusy(true, $"Rebuilding {Path.GetFileName(destination)}...");
        progressBar.Visible = true;
        progressBar.Minimum = 0;
        progressBar.Maximum = archive.Entries.Count;
        progressBar.Value = 0;
        try
        {
            IProgress<int> progress = new Progress<int>(value =>
            {
                progressBar.Value = value;
                statusLabel.Text = $"Rebuilding archive: {value:N0} / {archive.Entries.Count:N0} files...";
            });
            SecurePakSaveResult result = await Task.Run(() => archive.SaveAs(destination, snapshot, progress));
            SecurePakArchive reopened = await Task.Run(() => SecurePakArchive.Open(result.FilePath));
            SecurePakArchive previous = archive;
            archive = reopened;
            previous.Dispose();
            replacements.Clear();
            BuildFolderTree();
            UpdateArchiveCaption();
            statusLabel.Text = $"Saved and reopened {Path.GetFileName(result.FilePath)} | " +
                $"{result.ModifiedEntries:N0} modified file(s) | {FormatSize((ulong)result.FileSize)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not save modified archive",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "The modified archive was not saved. Pending changes remain in the editor.";
        }
        finally
        {
            progressBar.Visible = false;
            SetBusy(false);
        }
    }

    private void ApplyReplacement(SecurePakEntry entry, byte[] content)
    {
        replacements[entry.Index] = content;
        UpdateArchiveCaption();
        RefreshFileList();
    }

    private SecurePakEntry? GetSelectedEntry() =>
        fileList.SelectedItems.Count == 1 ? fileList.SelectedItems[0].Tag as SecurePakEntry : null;

    private byte[] ReadCurrentEntry(SecurePakEntry entry) =>
        replacements.TryGetValue(entry.Index, out byte[]? replacement) ? replacement : archive!.ReadEntry(entry);

    private void WriteCurrentEntry(SecurePakEntry entry, string destinationPath)
    {
        byte[] content = ReadCurrentEntry(entry);
        string fullDestination = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullDestination, content);
    }

    private void UpdateArchiveCaption()
    {
        string modifiedMarker = replacements.Count > 0 ? " *" : string.Empty;
        Text = archive is null
            ? "Kathana SecurePak Editor"
            : $"Kathana SecurePak Editor — {Path.GetFileName(archive.FilePath)}{modifiedMarker}";
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (replacements.Count == 0) return;
        if (MessageBox.Show(this,
                $"Discard {replacements.Count:N0} unsaved modification(s) and close?",
                "Unsaved modifications", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        UseWaitCursor = value;
        searchBox.Enabled = !value;
        folderTree.Enabled = !value;
        fileList.Enabled = !value;
        extractSelectedButton.Enabled = !value && fileList.SelectedItems.Count > 0;
        extractAllButton.Enabled = !value && archive is not null;
        replaceSelectedButton.Enabled = !value && GetSelectedEntry() is not null;
        bool selectedIsTcc = GetSelectedEntry() is SecurePakEntry selectedEntry &&
            string.Equals(Path.GetExtension(selectedEntry.Path), ".tcc", StringComparison.OrdinalIgnoreCase);
        editTccButton.Enabled = !value && selectedIsTcc;
        editTextButton.Enabled = !value && GetSelectedEntry() is not null && !selectedIsTcc;
        revertButton.Enabled = !value && fileList.SelectedItems.Cast<ListViewItem>()
            .Any(item => item.Tag is SecurePakEntry selected && replacements.ContainsKey(selected.Index));
        saveAsButton.Enabled = !value && archive is not null && replacements.Count > 0;
        if (message is not null) statusLabel.Text = message;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 &&
            string.Equals(Path.GetExtension(files[0]), ".pak", StringComparison.OrdinalIgnoreCase))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
        {
            await LoadArchiveAsync(files[0]);
        }
    }

    private static string FormatSize(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes:N0} B" : $"{value:N2} {units[unit]}";
    }

    private sealed class PreviewForm : Form
    {
        public PreviewForm(SecurePakEntry entry, byte[] content)
        {
            Text = $"Preview — {entry.Path}";
            Width = 900;
            Height = 650;
            StartPosition = FormStartPosition.CenterParent;

            string extension = Path.GetExtension(entry.Path).ToLowerInvariant();
            if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico")
            {
                try
                {
                    using MemoryStream input = new(content, writable: false);
                    using Image source = Image.FromStream(input);
                    PictureBox picture = new()
                    {
                        Dock = DockStyle.Fill,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = new Bitmap(source),
                        BackColor = Color.FromArgb(35, 35, 35)
                    };
                    FormClosed += (_, _) => picture.Image?.Dispose();
                    Controls.Add(picture);
                    return;
                }
                catch
                {
                    // Fall back to a hexadecimal preview for unsupported image variants.
                }
            }

            TextBox text = new()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font(FontFamily.GenericMonospace, 10)
            };
            if (extension is ".txt" or ".xml" or ".json" or ".ini" or ".cfg" or ".lua" or
                ".csv" or ".html" or ".css" or ".vert" or ".frag" or ".glsl")
            {
                text.Text = Encoding.UTF8.GetString(content);
            }
            else
            {
                text.Text = BuildHexPreview(content);
            }
            Controls.Add(text);
        }

        private static string BuildHexPreview(byte[] content)
        {
            const int limit = 64 * 1024;
            int length = Math.Min(content.Length, limit);
            StringBuilder builder = new(length * 4);
            for (int offset = 0; offset < length; offset += 16)
            {
                int lineLength = Math.Min(16, length - offset);
                builder.Append(offset.ToString("X8")).Append("  ");
                for (int column = 0; column < 16; column++)
                {
                    builder.Append(column < lineLength ? content[offset + column].ToString("X2") : "  ").Append(' ');
                }
                builder.Append(" ");
                for (int column = 0; column < lineLength; column++)
                {
                    byte value = content[offset + column];
                    builder.Append(value is >= 32 and <= 126 ? (char)value : '.');
                }
                builder.AppendLine();
            }
            if (content.Length > limit)
            {
                builder.AppendLine().Append($"Preview truncated at {limit:N0} of {content.Length:N0} bytes.");
            }
            return builder.ToString();
        }
    }
}
