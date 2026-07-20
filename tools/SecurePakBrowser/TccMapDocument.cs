using System.Buffers.Binary;
using System.Text;

namespace KathanaSecurePakBrowser;

public readonly record struct TccMapCell(ushort X, ushort Y, ushort MapValue, ushort Flags);

public sealed class TccMapDocument
{
    public const int HeaderSize = 0x42;
    public const int CellSize = 8;
    private const int MaximumCellCount = 16 * 1024 * 1024;

    private readonly byte[] source;
    private readonly ushort[] mapValues;
    private readonly ushort[] flags;

    private TccMapDocument(
        byte[] source,
        string version,
        string created,
        string signature,
        byte layoutVersion,
        ushort width,
        ushort height,
        ushort[] mapValues,
        ushort[] flags)
    {
        this.source = source;
        this.mapValues = mapValues;
        this.flags = flags;
        Version = version;
        Created = created;
        Signature = signature;
        LayoutVersion = layoutVersion;
        Width = width;
        Height = height;
    }

    public string Version { get; }
    public string Created { get; }
    public string Signature { get; }
    public byte LayoutVersion { get; }
    public ushort Width { get; }
    public ushort Height { get; }
    public int CellCount => mapValues.Length;

    public static TccMapDocument Parse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length < HeaderSize)
        {
            throw new InvalidDataException("The TCC file is smaller than its 66-byte header.");
        }

        string version = ReadFixedAscii(content.AsSpan(0x00, 4));
        string created = ReadFixedAscii(content.AsSpan(0x04, 25));
        string signature = ReadFixedAscii(content.AsSpan(0x1D, 28));
        if (!string.Equals(signature, "TANTRA_MAP", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected TANTRA_MAP signature, found '{signature}'.");
        }

        byte layoutVersion = content[0x39];
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(0x3A, 2));
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(0x3C, 2));
        uint declaredCount = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(0x3E, 4));
        long calculatedCount = checked((long)width * height);
        if (width == 0 || height == 0 || calculatedCount != declaredCount || declaredCount > MaximumCellCount)
        {
            throw new InvalidDataException(
                $"Invalid TCC dimensions/count: {width} x {height}, declared {declaredCount:N0} cells.");
        }

        long expectedLength = checked(HeaderSize + calculatedCount * CellSize);
        if (content.LongLength != expectedLength)
        {
            throw new InvalidDataException(
                $"TCC size mismatch: expected {expectedLength:N0} bytes, found {content.LongLength:N0}.");
        }

        ushort[] mapValues = new ushort[declaredCount];
        ushort[] flags = new ushort[declaredCount];
        for (int index = 0; index < declaredCount; index++)
        {
            int offset = HeaderSize + index * CellSize;
            ushort expectedX = (ushort)(index % width);
            ushort expectedY = (ushort)(index / width);
            ushort storedX = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset, 2));
            ushort storedY = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset + 2, 2));
            if (storedX != expectedX || storedY != expectedY)
            {
                throw new InvalidDataException(
                    $"TCC coordinate mismatch at cell {index:N0}: expected ({expectedX}, {expectedY}), " +
                    $"found ({storedX}, {storedY}).");
            }
            mapValues[index] = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset + 4, 2));
            flags[index] = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset + 6, 2));
        }

        return new TccMapDocument(content.ToArray(), version, created, signature,
            layoutVersion, width, height, mapValues, flags);
    }

    public TccMapCell GetCell(int x, int y)
    {
        int index = GetIndex(x, y);
        return new TccMapCell((ushort)x, (ushort)y, mapValues[index], flags[index]);
    }

    public void SetCell(int x, int y, ushort mapValue, ushort cellFlags)
    {
        int index = GetIndex(x, y);
        mapValues[index] = mapValue;
        flags[index] = cellFlags;
    }

    public bool IsCellModified(int x, int y)
    {
        int index = GetIndex(x, y);
        int offset = HeaderSize + index * CellSize;
        ushort originalValue = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 4, 2));
        ushort originalFlags = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 6, 2));
        return mapValues[index] != originalValue || flags[index] != originalFlags;
    }

    public byte[] Serialize()
    {
        byte[] result = source.ToArray();
        for (int index = 0; index < CellCount; index++)
        {
            int offset = HeaderSize + index * CellSize;
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 4, 2), mapValues[index]);
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 6, 2), flags[index]);
        }
        return result;
    }

    private int GetIndex(int x, int y)
    {
        if ((uint)x >= Width || (uint)y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside {Width} x {Height}.");
        }
        return checked(y * Width + x);
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0) bytes = bytes[..terminator];
        return Encoding.ASCII.GetString(bytes);
    }
}
