using KathanaSecurePakBrowser;
using System.Reflection;
using System.Security.Cryptography;

string work = Path.Combine(Path.GetTempPath(), "SecurePak-PackProbe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
void Reject(Action action, string name)
{
    try { action(); } catch (Exception e) when (e is InvalidDataException or InvalidOperationException or OperationCanceledException) { checks++; return; }
    throw new Exception("Expected rejection: " + name);
}
try
{
    string seed = Path.Combine(work, "seed.bin"), original = Path.Combine(work, "original.pak");
    File.WriteAllBytes(seed, new byte[108]);
    byte[][] contents = [Enumerable.Repeat((byte)65, 4096).ToArray(), RandomNumberGenerator.GetBytes(1024), [], [1, 2, 3], [4, 5, 6]];
    string[] names = ["resource/map/test.txt", "system/binary.dat", "empty.txt", "resource/map/test.txt", "RESOURCE/MAP/TEST.TXT"];
    var entries = names.Select((name, i) => new SecurePakEntry(i, name, (uint)(100 + i), 0, 0, 0, 1, 0)).ToArray();
    using (FileStream stream = File.OpenRead(seed))
    using (var template = (SecurePakArchive)Activator.CreateInstance(typeof(SecurePakArchive), BindingFlags.Instance | BindingFlags.NonPublic,
        null, [seed, stream, (ushort)4, (byte)2, (uint)108, (uint)0, (ulong)108, (uint)entries.Length, (uint)1, (ulong)108, entries], null)!)
        template.SaveAs(original, contents.Select((b, i) => (b, i)).ToDictionary(x => x.i, x => x.b));
    byte[] originalHash = SHA256.HashData(File.ReadAllBytes(original));
    using var source = SecurePakArchive.Open(original);
    string extracted = Path.Combine(work, "extracted");
    Directory.CreateDirectory(extracted);
    foreach (var entry in source.Entries) source.ExtractEntry(entry, source.GetSafeExtractionPath(extracted, entry));
    byte[] edited = Enumerable.Repeat((byte)90, 8192).ToArray();
    File.WriteAllBytes(source.GetSafeExtractionPath(extracted, source.Entries[0]), edited);
    string output = Path.Combine(work, "packed.pak");
    var result = source.PackFolder(extracted, output);
    Check(result.ModifiedEntries == 5, "full folder count");
    using (var packed = SecurePakArchive.Open(output))
    {
        Check(packed.Version == source.Version && packed.Flags == source.Flags && packed.CompressionType == source.CompressionType, "encryption/compression metadata");
        for (int i = 0; i < contents.Length; i++)
        {
            Check(packed.Entries[i].Path == names[i] && packed.Entries[i].NameHash == source.Entries[i].NameHash, "path/hash retained");
            Check(packed.ReadEntry(packed.Entries[i]).SequenceEqual(i == 0 || i >= 3 ? edited : contents[i]), "exact round trip");
        }
        Check(packed.Entries[0].IsCompressed, "compressible data compressed");
        Check(!packed.Entries[2].IsCompressed, "empty file stored");
    }
    File.Delete(source.GetSafeExtractionPath(extracted, source.Entries[1]));
    source.PackFolder(extracted, output);
    using (var packed = SecurePakArchive.Open(output))
        Check(packed.ReadEntry(packed.Entries[1]).SequenceEqual(contents[1]), "missing file preserved");
    File.Delete(source.GetSafeExtractionPath(extracted, source.Entries[0]));
    source.PackFolder(extracted, output);
    using (var packed = SecurePakArchive.Open(output))
    {
        Check(packed.Entries.Count == contents.Length, "duplicates retained in index");
        foreach (int i in new[] { 0, 3, 4 })
            Check(packed.ReadEntry(packed.Entries[i]).SequenceEqual(contents[i]), "absent duplicate retains distinct original content");
    }
    byte[] savedHash = SHA256.HashData(File.ReadAllBytes(output));
    File.WriteAllText(Path.Combine(extracted, "unknown.txt"), "unknown");
    Reject(() => source.PackFolder(extracted, output), "unknown path");
    Check(SHA256.HashData(File.ReadAllBytes(output)).SequenceEqual(savedHash), "failure preserves destination");
    File.Delete(Path.Combine(extracted, "unknown.txt"));
    Reject(() => source.PackFolder(extracted, original), "source overwrite");
    Reject(() => source.PackFolder(extracted, Path.Combine(extracted, "output.pak")), "output in source folder");
    string empty = Path.Combine(work, "empty"); Directory.CreateDirectory(empty);
    Reject(() => source.PackFolder(empty, output), "empty folder");
    using var cancel = new CancellationTokenSource();
    Reject(() => source.PackFolder(extracted, output, new CancelProgress(cancel), cancel.Token), "cancel before publish");
    Check(SHA256.HashData(File.ReadAllBytes(output)).SequenceEqual(savedHash), "cancellation preserves destination");
    Check(!Directory.EnumerateFiles(work, "*.tmp").Any(), "temporary files removed");
    Check(SHA256.HashData(File.ReadAllBytes(original)).SequenceEqual(originalHash), "original unchanged");
    Console.WriteLine($"PASS: {checks} encrypted folder-pack round-trip and failure checks.");
}
finally
{
    // Only the unique directory created by this test is removed.
    Directory.Delete(work, recursive: true);
}
sealed class CancelProgress(CancellationTokenSource source) : IProgress<int>
{
    public void Report(int value) => source.Cancel();
}
