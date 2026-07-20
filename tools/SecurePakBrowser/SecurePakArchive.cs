using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;
using static Monocypher.Monocypher;

namespace KathanaSecurePakBrowser;

public sealed partial class SecurePakArchive : IDisposable
{
    private const string LoaderPassword = "G-mUzj=6hH_V@Dh%bdE9QYsrraiRpBsW";
    private const int HeaderSize = 68;
    private const int EncryptedHeaderSize = 108;
    private const int SaltSize = 32;
    private const uint PakMagic = 0x214B4150;
    private const uint IndexV2Magic = 0x32584449;
    private const int MaxExtractedFileSize = 1024 * 1024 * 1024;

    private readonly FileStream stream;
    private readonly object streamLock = new();

    private SecurePakArchive(
        string filePath,
        FileStream stream,
        ushort version,
        byte flags,
        uint indexOffset,
        uint indexSize,
        ulong dataOffset,
        uint declaredFileCount,
        uint compressionType,
        ulong declaredDataSize,
        IReadOnlyList<SecurePakEntry> entries)
    {
        FilePath = filePath;
        this.stream = stream;
        Version = version;
        Flags = flags;
        IndexOffset = indexOffset;
        IndexSize = indexSize;
        DataOffset = dataOffset;
        DeclaredFileCount = declaredFileCount;
        CompressionType = compressionType;
        DeclaredDataSize = declaredDataSize;
        Entries = entries;
    }

    public string FilePath { get; }
    public ushort Version { get; }
    public byte Flags { get; }
    public uint IndexOffset { get; }
    public uint IndexSize { get; }
    public ulong DataOffset { get; }
    public uint DeclaredFileCount { get; }
    public uint CompressionType { get; }
    public ulong DeclaredDataSize { get; }
    public IReadOnlyList<SecurePakEntry> Entries { get; }

    public static SecurePakArchive Open(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        FileStream? archiveStream = null;
        try
        {
            archiveStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.RandomAccess);
            if (archiveStream.Length < EncryptedHeaderSize + SaltSize)
            {
                throw new InvalidDataException("The file is too small to be a SecurePak archive.");
            }

            byte[] encryptedHeader = ReadExactlyAt(archiveStream, 0, EncryptedHeaderSize);
            byte[] salt = ReadExactlyAt(archiveStream, archiveStream.Length - SaltSize, SaltSize);
            byte[] masterKey = DeriveMasterKey(Encoding.UTF8.GetBytes(LoaderPassword), salt);
            byte[] header;
            try
            {
                header = DecryptHeader(encryptedHeader, masterKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != PakMagic)
            {
                throw new InvalidDataException("Header authentication succeeded, but the PAK! signature is missing.");
            }

            ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4, 2));
            if (version != 4)
            {
                throw new NotSupportedException($"SecurePak version {version} is not supported by this browser.");
            }

            byte flags = header[6];
            uint indexOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
            uint indexSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, 4));
            ulong dataOffset = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16, 8));
            uint declaredFileCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24, 4));
            uint compressionType = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28, 4));
            ulong declaredDataSize = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(32, 8));

            ValidateArchiveRanges(archiveStream.Length, dataOffset, indexOffset, indexSize);
            ValidateHeaderIntegrity(header, indexOffset, indexSize);
            byte[] index = ReadExactlyAt(archiveStream, indexOffset, checked((int)indexSize));
            IReadOnlyList<SecurePakEntry> entries;
            try
            {
                entries = ParseIndex(index, masterKey, flags, dataOffset, indexOffset, declaredFileCount);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }

            SecurePakArchive archive = new(fullPath, archiveStream, version, flags, indexOffset,
                indexSize, dataOffset, declaredFileCount, compressionType, declaredDataSize, entries);
            archiveStream = null;
            return archive;
        }
        catch
        {
            archiveStream?.Dispose();
            throw;
        }
    }

    public byte[] ReadEntry(SecurePakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if ((uint)entry.Index >= Entries.Count || !ReferenceEquals(Entries[entry.Index], entry))
        {
            throw new ArgumentException("The entry does not belong to this archive.", nameof(entry));
        }
        if (entry.OriginalSize > MaxExtractedFileSize)
        {
            throw new InvalidDataException($"Refusing to allocate a file larger than {MaxExtractedFileSize:N0} bytes.");
        }

        ulong absoluteOffset = checked(DataOffset + entry.RelativeOffset);
        byte[] blockHeader;
        byte[] stored;
        lock (streamLock)
        {
            blockHeader = ReadExactlyAt(stream, checked((long)absoluteOffset), 8);
            uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader.AsSpan(0, 4));
            if (blockSize != entry.StoredSize)
            {
                throw new InvalidDataException(
                    $"Stored-size mismatch for {entry.Path}: index={entry.StoredSize}, block={blockSize}.");
            }
            stored = ReadExactlyAt(stream, checked((long)absoluteOffset + 8), checked((int)entry.StoredSize));
        }

        byte[] result;
        if (entry.IsCompressed)
        {
            if (CompressionType != 1)
            {
                throw new NotSupportedException($"Compression type {CompressionType} is not supported.");
            }
            result = new byte[checked((int)entry.OriginalSize)];
            int decoded = LZ4Codec.Decode(stored, 0, stored.Length, result, 0, result.Length);
            if (decoded != result.Length)
            {
                throw new InvalidDataException(
                    $"LZ4 output-size mismatch for {entry.Path}: expected {result.Length}, decoded {decoded}.");
            }
        }
        else
        {
            if (entry.OriginalSize != entry.StoredSize)
            {
                throw new InvalidDataException(
                    $"Uncompressed size mismatch for {entry.Path}: stored={entry.StoredSize}, original={entry.OriginalSize}.");
            }
            result = stored;
        }

        uint actualCrc = Crc32.Compute(result);
        if (actualCrc != entry.Crc32)
        {
            throw new InvalidDataException(
                $"CRC32 mismatch for {entry.Path}: expected {entry.Crc32:X8}, got {actualCrc:X8}.");
        }
        return result;
    }

    public void ExtractEntry(SecurePakEntry entry, string destinationPath)
    {
        byte[] content = ReadEntry(entry);
        string fullDestination = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(fullDestination, content);
    }

    public string GetSafeExtractionPath(string rootDirectory, SecurePakEntry entry)
    {
        string root = Path.GetFullPath(rootDirectory);
        string normalized = entry.Path.Replace('\\', '/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment =>
                segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidDataException($"Unsafe archive path: {entry.Path}");
        }
        string candidate = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Archive path escapes the extraction folder: {entry.Path}");
        }
        return candidate;
    }

    public void Dispose() => stream.Dispose();

    private static byte[] DeriveMasterKey(byte[] password, byte[] salt)
    {
        GCHandle passwordHandle = default;
        GCHandle saltHandle = default;
        byte[] workArea = new byte[0x4000 * 1024];
        byte[] key = new byte[32];
        try
        {
            passwordHandle = GCHandle.Alloc(password, GCHandleType.Pinned);
            saltHandle = GCHandle.Alloc(salt, GCHandleType.Pinned);
            crypto_argon2_config config = new()
            {
                algorithm = 2,
                nb_blocks = 0x4000,
                nb_passes = 3,
                nb_lanes = 1
            };
            crypto_argon2_inputs inputs = new()
            {
                pass = passwordHandle.AddrOfPinnedObject(),
                salt = saltHandle.AddrOfPinnedObject(),
                pass_size = (uint)password.Length,
                salt_size = (uint)salt.Length
            };
            crypto_argon2(key.AsSpan(), workArea.AsSpan(), config, inputs, new crypto_argon2_extras());
            return key;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(workArea);
            CryptographicOperations.ZeroMemory(password);
            if (passwordHandle.IsAllocated) passwordHandle.Free();
            if (saltHandle.IsAllocated) saltHandle.Free();
        }
    }

    private static byte[] DecryptHeader(byte[] encryptedHeader, byte[] key)
    {
        ReadOnlySpan<byte> nonce = encryptedHeader.AsSpan(0, 24);
        ReadOnlySpan<byte> cipher = encryptedHeader.AsSpan(24, HeaderSize);
        ReadOnlySpan<byte> mac = encryptedHeader.AsSpan(24 + HeaderSize, 16);
        byte[] header = new byte[HeaderSize];
        int result = crypto_aead_unlock(header.AsSpan(), mac, key, nonce,
            ReadOnlySpan<byte>.Empty, cipher);
        if (result != 0)
        {
            throw new CryptographicException(
                "SecurePak header authentication failed. This data.pak does not match the analyzed KathanaGame.exe loader.");
        }
        return header;
    }

    private static IReadOnlyList<SecurePakEntry> ParseIndex(
        byte[] index,
        byte[] masterKey,
        byte headerFlags,
        ulong dataOffset,
        uint indexOffset,
        uint declaredFileCount)
    {
        if (index.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(index) != IndexV2Magic)
        {
            throw new InvalidDataException("The archive does not contain an IDX2 index.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(4, 4));
        if (count != declaredFileCount)
        {
            throw new InvalidDataException($"File-count mismatch: header={declaredFileCount}, index={count}.");
        }
        if (count > 10_000_000)
        {
            throw new InvalidDataException("The index declares an unreasonable number of files.");
        }

        byte[] nameKey = new byte[32];
        byte[] nameMaterial = masterKey.Concat("names-v4"u8.ToArray()).ToArray();
        crypto_blake2b(nameKey.AsSpan(), nameMaterial);
        CryptographicOperations.ZeroMemory(nameMaterial);

        List<SecurePakEntry> entries = new(checked((int)count));
        UTF8Encoding strictUtf8 = new(false, true);
        int cursor = 8;
        try
        {
            for (int entryIndex = 0; entryIndex < count; entryIndex++)
            {
                EnsureAvailable(index, cursor, 2, "filename length");
                ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(index.AsSpan(cursor, 2));
                cursor += 2;
                EnsureAvailable(index, cursor, checked(nameLength + 28), "IDX2 entry");

                byte[] nameBytes = index.AsSpan(cursor, nameLength).ToArray();
                if ((headerFlags & 2) != 0)
                {
                    byte[] decryptedName = new byte[nameBytes.Length];
                    byte[] nonce = new byte[24];
                    BinaryPrimitives.WriteUInt32LittleEndian(nonce, (uint)entryIndex);
                    crypto_chacha20_x(decryptedName.AsSpan(), nameBytes, nameKey, nonce, 0);
                    nameBytes = decryptedName;
                }
                string path = strictUtf8.GetString(nameBytes).Replace('\\', '/');
                ValidateDisplayPath(path);
                cursor += nameLength;

                ReadOnlySpan<byte> tail = index.AsSpan(cursor, 28);
                uint nameHash = BinaryPrimitives.ReadUInt32LittleEndian(tail[..4]);
                ulong relativeOffset = BinaryPrimitives.ReadUInt64LittleEndian(tail.Slice(4, 8));
                uint storedSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.Slice(12, 4));
                uint originalSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.Slice(16, 4));
                ushort entryFlags = BinaryPrimitives.ReadUInt16LittleEndian(tail.Slice(20, 2));
                uint crc = BinaryPrimitives.ReadUInt32LittleEndian(tail.Slice(24, 4));
                cursor += 28;

                // IDX2 writes zero in the packed-size field for stored entries; their
                // actual on-disk payload size is the original size (and is repeated in
                // the eight-byte data-block prefix).
                uint effectiveStoredSize = storedSize == 0 ? originalSize : storedSize;

                ulong absoluteOffset = checked(dataOffset + relativeOffset);
                ulong blockEnd = checked(absoluteOffset + 8UL + effectiveStoredSize);
                if (absoluteOffset < dataOffset || blockEnd > indexOffset)
                {
                    throw new InvalidDataException($"Entry {entryIndex} points outside the data section: {path}");
                }
                entries.Add(new SecurePakEntry(entryIndex, path, nameHash, relativeOffset,
                    effectiveStoredSize, originalSize, entryFlags, crc));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nameKey);
        }
        return entries;
    }

    private static void ValidateArchiveRanges(long archiveLength, ulong dataOffset, uint indexOffset, uint indexSize)
    {
        ulong length = checked((ulong)archiveLength);
        if (dataOffset < EncryptedHeaderSize || dataOffset >= indexOffset ||
            checked((ulong)indexOffset + indexSize + SaltSize) != length)
        {
            throw new InvalidDataException("SecurePak header offsets do not match the archive length.");
        }
    }

    private static void ValidateHeaderIntegrity(byte[] header, uint indexOffset, uint indexSize)
    {
        byte[] material = Encoding.ASCII.GetBytes(
            indexOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            indexSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        byte[] digest = new byte[32];
        try
        {
            crypto_blake2b(digest.AsSpan(), material);
            if (!CryptographicOperations.FixedTimeEquals(digest.AsSpan(0, 28), header.AsSpan(40, 28)))
            {
                throw new InvalidDataException("SecurePak header integrity check failed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void ValidateDisplayPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0 ||
            path.StartsWith('/') || path.Contains(':') ||
            path.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"The index contains an unsafe filename: {path}");
        }
    }

    private static void EnsureAvailable(byte[] source, int offset, int length, string description)
    {
        if (offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException($"Truncated {description} at index byte {offset}.");
        }
    }

    private static byte[] ReadExactlyAt(FileStream source, long offset, int count)
    {
        byte[] result = new byte[count];
        source.Position = offset;
        source.ReadExactly(result);
        return result;
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }
            return ~crc;
        }

        private static uint[] BuildTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
                }
                table[i] = value;
            }
            return table;
        }
    }
}
