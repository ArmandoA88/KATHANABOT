using KathanaSecurePakBrowser;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: BrowserProbe <data.pak> [--all|--rebuild-test|--find=<path-text>|--grep=<term1|term2>|--extract=<archive-path>::<destination-file>]");
    return 2;
}

using SecurePakArchive archive = SecurePakArchive.Open(args[0]);
Console.WriteLine($"version={archive.Version} files={archive.Entries.Count} compression={archive.CompressionType}");
Console.WriteLine($"first={archive.Entries[0].Path}");
Console.WriteLine($"last={archive.Entries[^1].Path}");

if (args.Length == 2 && args[1].StartsWith("--find=", StringComparison.OrdinalIgnoreCase))
{
    string query = args[1]["--find=".Length..];
    SecurePakEntry[] matches = archive.Entries
        .Where(entry => entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
        .ToArray();
    foreach (SecurePakEntry entry in matches)
    {
        Console.WriteLine($"match[{entry.Index}]={entry.Path} original={entry.OriginalSize} " +
            $"stored={entry.StoredSize} flags=0x{entry.Flags:X4} crc={entry.Crc32:X8}");
    }
    return matches.Length > 0 ? 0 : 1;
}

if (args.Length == 2 && args[1].StartsWith("--grep=", StringComparison.OrdinalIgnoreCase))
{
    string[] queries = args[1]["--grep=".Length..]
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (queries.Length == 0)
    {
        Console.Error.WriteLine("--grep requires one or more terms separated by | characters.");
        return 2;
    }

    int scanned = 0;
    int matched = 0;
    foreach (SecurePakEntry entry in archive.Entries)
    {
        byte[] content = archive.ReadEntry(entry);
        string[] hits = queries.Where(query => ContainsTextBytes(content, query)).ToArray();
        scanned++;
        if (hits.Length == 0) continue;

        matched++;
        Console.WriteLine($"content-match[{entry.Index}]={entry.Path} size={entry.OriginalSize} terms={string.Join(',', hits)}");
    }

    Console.WriteLine($"content-search-scanned={scanned} matched={matched}");
    return matched > 0 ? 0 : 1;
}

if (args.Length == 2 && args[1].StartsWith("--extract=", StringComparison.OrdinalIgnoreCase))
{
    string specification = args[1]["--extract=".Length..];
    int separator = specification.IndexOf("::", StringComparison.Ordinal);
    if (separator <= 0 || separator >= specification.Length - 2)
    {
        Console.Error.WriteLine("Usage: BrowserProbe <data.pak> --extract=<archive-path>::<destination-file>");
        return 2;
    }

    string archivePath = specification[..separator];
    string destination = specification[(separator + 2)..];
    SecurePakEntry? entry = archive.Entries.FirstOrDefault(candidate =>
        candidate.Path.Equals(archivePath, StringComparison.OrdinalIgnoreCase));
    if (entry is null)
    {
        Console.Error.WriteLine($"Archive entry was not found: {archivePath}");
        return 1;
    }

    archive.ExtractEntry(entry, destination);
    Console.WriteLine($"extracted[{entry.Index}]={entry.Path} -> {Path.GetFullPath(destination)} size={entry.OriginalSize}");
    return 0;
}

if (args.Length == 2 && args[1].StartsWith("--tcc-test=", StringComparison.OrdinalIgnoreCase))
{
    string tccPath = args[1]["--tcc-test=".Length..];
    byte[] original = await File.ReadAllBytesAsync(tccPath);
    TccMapDocument map = TccMapDocument.Parse(original);
    int x = map.Width / 2;
    int y = map.Height / 2;
    TccMapCell before = map.GetCell(x, y);
    ushort newValue = before.MapValue == ushort.MaxValue ? (ushort)0 : (ushort)(before.MapValue + 1);
    ushort newFlags = (ushort)(before.Flags ^ 0x0010);
    map.SetCell(x, y, newValue, newFlags);
    if (!map.IsCellModified(x, y)) throw new InvalidDataException("TCC change tracking failed.");

    byte[] serialized = map.Serialize();
    TccMapDocument reopened = TccMapDocument.Parse(serialized);
    TccMapCell after = reopened.GetCell(x, y);
    if (serialized.Length != original.Length || after.MapValue != newValue || after.Flags != newFlags)
        throw new InvalidDataException("TCC edited values did not round-trip.");

    int cellOffset = TccMapDocument.HeaderSize + (y * map.Width + x) * TccMapDocument.CellSize;
    int[] changedOffsets = original.Select((value, index) => (value, index))
        .Where(item => item.value != serialized[item.index])
        .Select(item => item.index)
        .ToArray();
    if (changedOffsets.Any(offset => offset < cellOffset + 4 || offset > cellOffset + 7))
        throw new InvalidDataException("TCC serialization changed bytes outside the selected cell values.");

    map.SetCell(x, y, before.MapValue, before.Flags);
    if (map.IsCellModified(x, y) || !map.Serialize().SequenceEqual(original))
        throw new InvalidDataException("TCC revert did not reproduce the original file.");

    Console.WriteLine($"tcc={map.Signature} version={map.Version} layout={map.LayoutVersion} " +
        $"size={map.Width}x{map.Height} cells={map.CellCount}");
    Console.WriteLine($"edited-cell=({x},{y}) value={before.MapValue}->{newValue} " +
        $"flags=0x{before.Flags:X4}->0x{newFlags:X4} changed-bytes={changedOffsets.Length}");
    Console.WriteLine("tcc-test=passed");
    return 0;
}

if (args.Length == 2 && args[1].StartsWith("--tcc-ui-smoke=", StringComparison.OrdinalIgnoreCase))
{
    string tccPath = args[1]["--tcc-ui-smoke=".Length..];
    TccMapDocument map = TccMapDocument.Parse(await File.ReadAllBytesAsync(tccPath));
    FileInfo tccFile = new(tccPath);
    SecurePakEntry entry = new(0, "resource/map/Jina8thCave/MAP_Jina8thCave.tcc",
        0, 0, checked((uint)tccFile.Length), checked((uint)tccFile.Length), 0, 0);
    Exception? uiError = null;
    string? title = null;
    Thread uiThread = new(() =>
    {
        try
        {
            ApplicationConfiguration.Initialize();
            using TccMapEditorForm form = new(entry, map);
            form.WindowState = FormWindowState.Minimized;
            form.ShowInTaskbar = false;
            form.Shown += (_, _) =>
            {
                title = form.Text;
                form.BeginInvoke(form.Close);
            };
            Application.Run(form);
        }
        catch (Exception exception)
        {
            uiError = exception;
        }
    });
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start();
    if (!uiThread.Join(TimeSpan.FromSeconds(30)))
        throw new TimeoutException("TCC editor UI did not complete its smoke test.");
    if (uiError is not null) throw new InvalidOperationException("TCC editor UI failed.", uiError);
    if (title is null || !title.StartsWith("TANTRA_MAP Editor", StringComparison.Ordinal))
        throw new InvalidOperationException("TCC editor window title was not initialized.");
    Console.WriteLine($"tcc-ui-title={title}");
    Console.WriteLine("tcc-ui-smoke=passed");
    return 0;
}

if (args.Length == 2 && args[1] == "--tcc-rebuild-test")
{
    SecurePakEntry tccEntry = archive.Entries.Single(entry =>
        entry.Path.EndsWith("/MAP_Jina8thCave.tcc", StringComparison.OrdinalIgnoreCase));
    byte[] tccContent = archive.ReadEntry(tccEntry);
    TccMapDocument tcc = TccMapDocument.Parse(tccContent);
    int x = tcc.Width / 2;
    int y = tcc.Height / 2;
    TccMapCell oldCell = tcc.GetCell(x, y);
    ushort newValue = oldCell.MapValue == ushort.MaxValue ? (ushort)0 : (ushort)(oldCell.MapValue + 1);
    ushort newFlags = (ushort)(oldCell.Flags ^ 0x0010);
    tcc.SetCell(x, y, newValue, newFlags);
    byte[] editedTcc = tcc.Serialize();

    string rebuiltPath = Path.Combine(Path.GetTempPath(), $"KathanaSecurePak-tcc-{Guid.NewGuid():N}.pak");
    try
    {
        archive.SaveAs(rebuiltPath, new Dictionary<int, byte[]> { [tccEntry.Index] = editedTcc });
        using SecurePakArchive rebuilt = SecurePakArchive.Open(rebuiltPath);
        SecurePakEntry rebuiltEntry = rebuilt.Entries[tccEntry.Index];
        byte[] rebuiltContent = rebuilt.ReadEntry(rebuiltEntry);
        if (!rebuiltContent.SequenceEqual(editedTcc))
            throw new InvalidDataException("Edited TCC bytes did not survive the SecurePak rebuild.");
        TccMapCell rebuiltCell = TccMapDocument.Parse(rebuiltContent).GetCell(x, y);
        if (rebuiltCell.MapValue != newValue || rebuiltCell.Flags != newFlags)
            throw new InvalidDataException("Edited TCC cell did not survive the SecurePak rebuild.");

        for (int index = 0; index < rebuilt.Entries.Count; index++)
            rebuilt.ReadEntry(rebuilt.Entries[index]);

        Console.WriteLine($"tcc-entry={rebuiltEntry.Path} size={rebuiltContent.Length} crc={rebuiltEntry.Crc32:X8}");
        Console.WriteLine($"rebuilt-cell=({x},{y}) value={newValue} flags=0x{newFlags:X4}");
        Console.WriteLine($"rebuilt-verified={rebuilt.Entries.Count}/{rebuilt.Entries.Count}");
        Console.WriteLine("tcc-rebuild-test=passed");
        return 0;
    }
    finally
    {
        if (File.Exists(rebuiltPath)) File.Delete(rebuiltPath);
    }
}

if (args.Length == 2 && args[1] == "--rebuild-test")
{
    string rebuiltPath = Path.Combine(Path.GetTempPath(), $"KathanaSecurePak-rebuild-{Guid.NewGuid():N}.pak");
    try
    {
        byte[] replacement = archive.ReadEntry(archive.Entries[0]);
        replacement[^1] ^= 0x5A;
        SecurePakSaveResult result = archive.SaveAs(rebuiltPath,
            new Dictionary<int, byte[]> { [0] = replacement });
        Console.WriteLine($"rebuilt={result.FilePath} bytes={result.FileSize} modified={result.ModifiedEntries}");

        using SecurePakArchive rebuilt = SecurePakArchive.Open(rebuiltPath);
        if (rebuilt.Entries.Count != archive.Entries.Count ||
            !rebuilt.Entries.Select(entry => entry.Path).SequenceEqual(archive.Entries.Select(entry => entry.Path)))
        {
            throw new InvalidDataException("The rebuilt index does not match the original paths.");
        }
        if (!rebuilt.ReadEntry(rebuilt.Entries[0]).SequenceEqual(replacement))
        {
            throw new InvalidDataException("The modified entry did not round-trip.");
        }

        for (int index = 0; index < rebuilt.Entries.Count; index++)
        {
            rebuilt.ReadEntry(rebuilt.Entries[index]);
            if ((index + 1) % 1000 == 0 || index == rebuilt.Entries.Count - 1)
            {
                Console.WriteLine($"rebuilt-verified={index + 1}/{rebuilt.Entries.Count}");
            }
        }
        Console.WriteLine("rebuild-test=passed");
        return 0;
    }
    finally
    {
        if (File.Exists(rebuiltPath)) File.Delete(rebuiltPath);
    }
}

IEnumerable<int> indexes = args.Length == 2 && args[1] == "--all"
    ? Enumerable.Range(0, archive.Entries.Count)
    : new[] { 0, archive.Entries.Count / 3, archive.Entries.Count / 2, archive.Entries.Count - 1 }.Distinct();
int verified = 0;
foreach (int index in indexes)
{
    SecurePakEntry entry = archive.Entries[index];
    byte[] content = archive.ReadEntry(entry);
    verified++;
    if (args.Length == 1 || verified % 1000 == 0 || verified == archive.Entries.Count)
    {
        Console.WriteLine($"verified[{index}]={entry.Path} size={content.Length} crc={entry.Crc32:X8}");
    }
}
return 0;

static bool ContainsTextBytes(ReadOnlySpan<byte> content, string query)
{
    if (string.IsNullOrWhiteSpace(query)) return false;

    byte[] ascii = System.Text.Encoding.ASCII.GetBytes(query);
    if (ContainsAsciiIgnoreCase(content, ascii)) return true;

    byte[] utf16Le = System.Text.Encoding.Unicode.GetBytes(query);
    if (ContainsAsciiIgnoreCase(content, utf16Le, unicodeStride: 2)) return true;

    byte[] utf16Be = System.Text.Encoding.BigEndianUnicode.GetBytes(query);
    return ContainsAsciiIgnoreCase(content, utf16Be, unicodeStride: 2);
}

static bool ContainsAsciiIgnoreCase(ReadOnlySpan<byte> content, ReadOnlySpan<byte> query, int unicodeStride = 1)
{
    if (query.Length == 0 || content.Length < query.Length) return false;
    for (int offset = 0; offset <= content.Length - query.Length; offset++)
    {
        bool equal = true;
        for (int index = 0; index < query.Length; index++)
        {
            byte expected = query[index];
            byte actual = content[offset + index];
            if (unicodeStride == 1 || index % unicodeStride == 0)
            {
                if (expected is >= (byte)'A' and <= (byte)'Z') expected = (byte)(expected + 32);
                if (actual is >= (byte)'A' and <= (byte)'Z') actual = (byte)(actual + 32);
            }
            if (actual == expected) continue;
            equal = false;
            break;
        }
        if (equal) return true;
    }
    return false;
}
