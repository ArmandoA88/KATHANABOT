using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;
using static Monocypher.Monocypher;

namespace KathanaSecurePakBrowser;

public sealed record SecurePakSaveResult(string FilePath, long FileSize, int ModifiedEntries);

public sealed partial class SecurePakArchive
{
    public SecurePakSaveResult SaveAs(
        string destinationPath,
        IReadOnlyDictionary<int, byte[]> replacements,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(replacements);

        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(destination, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Save to a new .pak path. The open source archive is never overwritten directly.");
        }

        foreach ((int entryIndex, byte[] content) in replacements)
        {
            if ((uint)entryIndex >= Entries.Count)
            {
                throw new ArgumentException($"Replacement index {entryIndex} is outside this archive.", nameof(replacements));
            }
            ArgumentNullException.ThrowIfNull(content);
            if (content.Length > MaxExtractedFileSize)
            {
                throw new InvalidDataException(
                    $"Replacement for {Entries[entryIndex].Path} exceeds the {MaxExtractedFileSize:N0}-byte limit.");
            }
        }

        string? destinationDirectory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(destinationDirectory))
        {
            throw new InvalidOperationException("The destination must have a parent directory.");
        }
        Directory.CreateDirectory(destinationDirectory);
        string temporaryPath = Path.Combine(destinationDirectory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] masterKey = DeriveMasterKey(Encoding.UTF8.GetBytes(LoaderPassword), salt);
        bool completed = false;
        try
        {
            List<WritableEntry> writableEntries = new(Entries.Count);
            using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
                       FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            {
                output.Write(new byte[EncryptedHeaderSize]);
                for (int index = 0; index < Entries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SecurePakEntry entry = Entries[index];
                    ulong relativeOffset = checked((ulong)output.Position - DataOffset);

                    if (replacements.TryGetValue(index, out byte[]? replacement))
                    {
                        WriteReplacementBlock(output, entry, replacement, relativeOffset, writableEntries);
                    }
                    else
                    {
                        CopyOriginalBlock(output, entry);
                        writableEntries.Add(new WritableEntry(entry.Path, entry.NameHash, relativeOffset,
                            entry.StoredSize, entry.OriginalSize, entry.Flags, entry.Crc32));
                    }
                    if ((index + 1) % 100 == 0 || index == Entries.Count - 1)
                    {
                        progress?.Report(index + 1);
                    }
                }

                uint indexOffset = checked((uint)output.Position);
                byte[] indexBytes = BuildIndex(writableEntries, masterKey, Flags);
                uint indexSize = checked((uint)indexBytes.Length);
                output.Write(indexBytes);
                ulong packageSize = checked((ulong)output.Position);
                output.Write(salt);

                byte[] header = BuildHeader(indexOffset, indexSize, packageSize);
                byte[] encryptedHeader = EncryptHeader(header, masterKey);
                output.Position = 0;
                output.Write(encryptedHeader);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destination, overwrite: true);
            completed = true;
            FileInfo saved = new(destination);
            return new SecurePakSaveResult(destination, saved.Length, replacements.Count);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(salt);
            if (!completed && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static uint ComputeContentCrc32(ReadOnlySpan<byte> content) => Crc32.Compute(content);

    private void CopyOriginalBlock(FileStream output, SecurePakEntry entry)
    {
        ulong absoluteOffset = checked(DataOffset + entry.RelativeOffset);
        long remaining = checked(8L + entry.StoredSize);
        byte[] buffer = new byte[Math.Min(1024 * 1024, checked((int)remaining))];
        lock (streamLock)
        {
            stream.Position = checked((long)absoluteOffset);
            while (remaining > 0)
            {
                int requested = (int)Math.Min(buffer.Length, remaining);
                int read = stream.Read(buffer, 0, requested);
                if (read == 0)
                {
                    throw new EndOfStreamException($"Truncated data block for {entry.Path}.");
                }
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }

    private void WriteReplacementBlock(
        FileStream output,
        SecurePakEntry entry,
        byte[] content,
        ulong relativeOffset,
        List<WritableEntry> writableEntries)
    {
        byte[] stored = content;
        bool compressed = false;
        if (entry.IsCompressed && content.Length > 0)
        {
            byte[] candidate = new byte[LZ4Codec.MaximumOutputSize(content.Length)];
            int encoded = LZ4Codec.Encode(content, 0, content.Length,
                candidate, 0, candidate.Length, LZ4Level.L00_FAST);
            if (encoded > 0 && encoded < content.Length)
            {
                stored = candidate.AsSpan(0, encoded).ToArray();
                compressed = true;
            }
        }

        Span<byte> blockHeader = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(blockHeader, checked((uint)stored.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(blockHeader[4..], 0);
        output.Write(blockHeader);
        output.Write(stored);

        ushort flags = (ushort)(entry.Flags & ~1);
        if (compressed) flags |= 1;
        writableEntries.Add(new WritableEntry(entry.Path, entry.NameHash, relativeOffset,
            checked((uint)stored.Length), checked((uint)content.Length), flags, Crc32.Compute(content)));
    }

    private static byte[] BuildIndex(IReadOnlyList<WritableEntry> entries, byte[] masterKey, byte headerFlags)
    {
        using MemoryStream index = new();
        WriteUInt32(index, IndexV2Magic);
        WriteUInt32(index, checked((uint)entries.Count));

        byte[] nameMaterial = masterKey.Concat("names-v4"u8.ToArray()).ToArray();
        byte[] nameKey = new byte[32];
        crypto_blake2b(nameKey.AsSpan(), nameMaterial);
        CryptographicOperations.ZeroMemory(nameMaterial);
        try
        {
            Span<byte> nonce = stackalloc byte[24];
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                WritableEntry entry = entries[entryIndex];
                byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Path);
                if (nameBytes.Length > ushort.MaxValue)
                {
                    throw new InvalidDataException($"Archive path is too long: {entry.Path}");
                }
                if ((headerFlags & 2) != 0)
                {
                    byte[] encryptedName = new byte[nameBytes.Length];
                    nonce.Clear();
                    BinaryPrimitives.WriteUInt32LittleEndian(nonce, (uint)entryIndex);
                    crypto_chacha20_x(encryptedName.AsSpan(), nameBytes, nameKey, nonce, 0);
                    nameBytes = encryptedName;
                }

                WriteUInt16(index, checked((ushort)nameBytes.Length));
                index.Write(nameBytes);
                WriteUInt32(index, entry.NameHash);
                WriteUInt64(index, entry.RelativeOffset);
                WriteUInt32(index, (entry.Flags & 1) != 0 ? entry.StoredSize : 0);
                WriteUInt32(index, entry.OriginalSize);
                WriteUInt16(index, entry.Flags);
                WriteUInt16(index, 0);
                WriteUInt32(index, entry.Crc32);
            }
            return index.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nameKey);
        }
    }

    private byte[] BuildHeader(uint indexOffset, uint indexSize, ulong packageSize)
    {
        byte[] header = new byte[HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, PakMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), Version);
        header[6] = Flags;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), indexOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), indexSize);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16, 8), DataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24, 4), checked((uint)Entries.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28, 4), CompressionType);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(32, 8), packageSize);

        byte[] integrityMaterial = Encoding.ASCII.GetBytes(
            indexOffset.ToString(CultureInfo.InvariantCulture) +
            indexSize.ToString(CultureInfo.InvariantCulture));
        byte[] digest = new byte[32];
        try
        {
            crypto_blake2b(digest.AsSpan(), integrityMaterial);
            digest.AsSpan(0, 28).CopyTo(header.AsSpan(40, 28));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
        return header;
    }

    private static byte[] EncryptHeader(byte[] header, byte[] masterKey)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(24);
        byte[] cipher = new byte[HeaderSize];
        byte[] mac = new byte[16];
        crypto_aead_lock(cipher.AsSpan(), mac.AsSpan(), masterKey, nonce,
            ReadOnlySpan<byte>.Empty, header);

        byte[] encryptedHeader = new byte[EncryptedHeaderSize];
        nonce.CopyTo(encryptedHeader, 0);
        cipher.CopyTo(encryptedHeader, 24);
        mac.CopyTo(encryptedHeader, 24 + HeaderSize);
        return encryptedHeader;
    }

    private static void WriteUInt16(Stream output, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        output.Write(buffer);
    }

    private static void WriteUInt32(Stream output, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        output.Write(buffer);
    }

    private static void WriteUInt64(Stream output, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        output.Write(buffer);
    }

    private sealed record WritableEntry(
        string Path,
        uint NameHash,
        ulong RelativeOffset,
        uint StoredSize,
        uint OriginalSize,
        ushort Flags,
        uint Crc32);
}
