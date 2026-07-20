namespace KathanaSecurePakBrowser;

public sealed record SecurePakEntry(
    int Index,
    string Path,
    uint NameHash,
    ulong RelativeOffset,
    uint StoredSize,
    uint OriginalSize,
    ushort Flags,
    uint Crc32)
{
    public bool IsCompressed => (Flags & 1) != 0;
    public string FileName => System.IO.Path.GetFileName(Path.Replace('/', '\\'));
    public string Folder
    {
        get
        {
            int separator = Path.LastIndexOf('/');
            return separator < 0 ? string.Empty : Path[..separator];
        }
    }
}
