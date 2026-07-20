using System.Text;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using static Monocypher.Monocypher;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: HeaderProbe <data.pak>");
    return 2;
}

const string password = "G-mUzj=6hH_V@Dh%bdE9QYsrraiRpBsW";
await using FileStream archive = new(args[0], FileMode.Open, FileAccess.Read, FileShare.Read);
byte[] encryptedHeader = new byte[108];
archive.Position = 0;
await archive.ReadExactlyAsync(encryptedHeader);
byte[] salt = new byte[32];
archive.Position = archive.Length - salt.Length;
await archive.ReadExactlyAsync(salt);

byte[] nonce = encryptedHeader[..24];
byte[] cipher = encryptedHeader[24..92];
byte[] mac = encryptedHeader[92..];
byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

IntPtr passwordPointer = Marshal.AllocHGlobal(passwordBytes.Length);
IntPtr saltPointer = Marshal.AllocHGlobal(salt.Length);
try
{
    Marshal.Copy(passwordBytes, 0, passwordPointer, passwordBytes.Length);
    Marshal.Copy(salt, 0, saltPointer, salt.Length);
    crypto_argon2_config config = new()
    {
        algorithm = 2,
        nb_blocks = 0x4000,
        nb_passes = 3,
        nb_lanes = 1
    };
    crypto_argon2_inputs inputs = new()
    {
        pass = passwordPointer,
        salt = saltPointer,
        pass_size = (uint)passwordBytes.Length,
        salt_size = (uint)salt.Length
    };
    crypto_argon2_extras extras = new();
    byte[] workArea = new byte[config.nb_blocks * 1024];
    byte[] key = new byte[32];
    crypto_argon2(key.AsSpan(), workArea.AsSpan(), config, inputs, extras);

    byte[] plain = new byte[cipher.Length];
    int result = crypto_aead_unlock(plain.AsSpan(), mac, key, nonce,
        ReadOnlySpan<byte>.Empty, cipher);
    Console.WriteLine($"unlock={result}");
    Console.WriteLine(Convert.ToHexString(key));
    Console.WriteLine(Convert.ToHexString(plain));
    uint indexOffset = BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(8, 4));
    uint indexSize = BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(12, 4));
    byte[] expectedIntegrity = plain.AsSpan(40, 28).ToArray();
    string[] integrityCandidates =
    [
        $"{indexOffset}{indexSize}",
        $"{indexSize}{indexOffset}",
        $"{indexOffset}:{indexSize}",
        $"{indexSize}:{indexOffset}"
    ];
    foreach (string candidate in integrityCandidates)
    {
        byte[] digest = new byte[32];
        crypto_blake2b(digest.AsSpan(), Encoding.ASCII.GetBytes(candidate));
        Console.WriteLine($"integrity '{candidate}'={Convert.ToHexString(digest.AsSpan(0, 28))} " +
            $"match={digest.AsSpan(0, 28).SequenceEqual(expectedIntegrity)}");
    }
    archive.Position = indexOffset;
    byte[] indexPrefix = new byte[Math.Min(indexSize, 128)];
    await archive.ReadExactlyAsync(indexPrefix);
    ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(indexPrefix.AsSpan(8, 2));
    byte[] encryptedName = indexPrefix.AsSpan(10, nameLength).ToArray();
    byte[] nameMaterial = key.Concat("names-v4"u8.ToArray()).ToArray();
    byte[] nameKey = new byte[32];
    crypto_blake2b(nameKey.AsSpan(), nameMaterial);
    byte[] nameNonce = new byte[24];
    byte[] name = new byte[nameLength];
    crypto_chacha20_x(name.AsSpan(), encryptedName, nameKey, nameNonce, 0);
    Console.WriteLine($"index={indexOffset} size={indexSize} count={BinaryPrimitives.ReadUInt32LittleEndian(indexPrefix.AsSpan(4, 4))}");
    Console.WriteLine($"first-name={Encoding.UTF8.GetString(name)}");
    return result == 0 && plain.AsSpan(0, 4).SequenceEqual("PAK!"u8) ? 0 : 1;
}
finally
{
    Marshal.FreeHGlobal(passwordPointer);
    Marshal.FreeHGlobal(saltPointer);
}
