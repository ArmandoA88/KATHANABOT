using System.Buffers.Binary;
using System.Text;

if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--dump"))
{
    Console.Error.WriteLine("Usage: SettingsProbe <settings.cfg> [--dump]");
    return 2;
}

byte[] file = await File.ReadAllBytesAsync(args[0]);
if (file.Length < 16 || !file.AsSpan(0, 4).SequenceEqual("KTCF"u8))
    throw new InvalidDataException("Not a KTCF settings file.");

uint version = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4, 4));
uint plainLength = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(8, 4));
uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12, 4));
if (plainLength == 0 || plainLength > 0x01000000 || file.Length != 16L + plainLength)
    throw new InvalidDataException($"Invalid KTCF payload length {plainLength} for a {file.Length}-byte file.");

byte[] plain = file.AsSpan(16, checked((int)plainLength)).ToArray();
uint state = version ^ 0xA5A5A5A5u;
for (int index = 0; index < plain.Length; index++)
{
    state = unchecked(state * 0x0019660Du + 0x3C6EF35Fu);
    plain[index] ^= (byte)(state >> 16);
}

uint actualCrc = ComputeCrc32(plain);
Console.WriteLine($"file={Path.GetFullPath(args[0])}");
Console.WriteLine($"magic=KTCF version={version} plaintext={plainLength} stored-crc={storedCrc:X8} actual-crc={actualCrc:X8} valid={storedCrc == actualCrc}");
Console.WriteLine($"plaintext-head={Convert.ToHexString(plain.AsSpan(0, Math.Min(64, plain.Length)))}");

string text = Encoding.UTF8.GetString(plain);
Dictionary<string, Dictionary<string, string>> sections = ParseIni(text);
PrintAutoHuntProfiles(sections);

if (args.Length == 2)
{
    Console.WriteLine("----- plaintext -----");
    Console.Write(text);
}

return storedCrc == actualCrc ? 0 : 1;

static uint ComputeCrc32(ReadOnlySpan<byte> data)
{
    uint crc = 0xFFFFFFFFu;
    foreach (byte item in data)
    {
        crc ^= item;
        for (int bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
    }
    return ~crc;
}

static Dictionary<string, Dictionary<string, string>> ParseIni(string text)
{
    Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string>? current = null;
    foreach (string sourceLine in text.Split('\n'))
    {
        string line = sourceLine.Trim().TrimEnd('\r');
        if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
        if (line.StartsWith('[') && line.EndsWith(']'))
        {
            string name = line[1..^1];
            current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[name] = current;
            continue;
        }

        int separator = line.IndexOf('=');
        if (current is not null && separator > 0)
            current[line[..separator].Trim()] = line[(separator + 1)..].Trim();
    }
    return result;
}

static void PrintAutoHuntProfiles(Dictionary<string, Dictionary<string, string>> sections)
{
    string[] profileSections = sections.Keys
        .Where(name => name.StartsWith("AUTOHUNT_", StringComparison.OrdinalIgnoreCase))
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($"autohunt-profiles={profileSections.Length}");
    foreach (string sectionName in profileSections)
    {
        string character = sectionName["AUTOHUNT_".Length..];
        Dictionary<string, string> profile = sections[sectionName];
        sections.TryGetValue($"QUICKSLOT_{character}", out Dictionary<string, string>? quickslots);
        Console.WriteLine($"[{sectionName}]");
        for (int index = 0; index < 6; index++)
            PrintResourceSetting(profile, quickslots, $"atkIdx{index}");
        for (int index = 0; index < 8; index++)
            PrintResourceSetting(profile, quickslots, $"buffIdx{index}");
        PrintResourceSetting(profile, quickslots, "hpPotionIndex");
        PrintSetting(profile, "hpThreshPct");
        PrintResourceSetting(profile, quickslots, "tpPotionIndex");
        PrintSetting(profile, "tpThreshPct");
        PrintResourceSetting(profile, quickslots, "healSkillIndex");
        PrintSetting(profile, "healSkillThreshPct");
        PrintResourceSetting(profile, quickslots, "repairItemIndex");
        foreach (string key in new[] { "repairIntervalMin", "autoLoot", "autoSit", "maxRangeIdx", "autoAssist", "skipElites", "uiPosX", "uiPosY", "version" })
            PrintSetting(profile, key);
    }
}

static void PrintResourceSetting(Dictionary<string, string> profile, Dictionary<string, string>? quickslots, string key)
{
    if (!profile.TryGetValue(key, out string? value)) return;
    string locations = ResolveQuickslots(quickslots, value);
    Console.WriteLine($"  {key}={value}{(locations.Length == 0 ? "" : $" quickslots={locations}")}");
}

static void PrintSetting(Dictionary<string, string> profile, string key)
{
    if (profile.TryGetValue(key, out string? value)) Console.WriteLine($"  {key}={value}");
}

static string ResolveQuickslots(Dictionary<string, string>? quickslots, string resourceId)
{
    if (quickslots is null || resourceId == "0") return "";
    List<string> matches = new();
    for (int bar = 0; bar < 5; bar++)
    {
        for (int slot = 0; slot < 10; slot++)
        {
            string indexKey = $"slot_{bar}_{slot}_index";
            if (!quickslots.TryGetValue(indexKey, out string? candidate) || candidate != resourceId) continue;
            string inventoryKey = $"slot_{bar}_{slot}_inven";
            string inventory = quickslots.TryGetValue(inventoryKey, out string? inventoryValue)
                ? $",inventory={inventoryValue}"
                : "";
            matches.Add($"bar{bar}/slot{slot}{inventory}");
        }
    }
    return string.Join(';', matches);
}
