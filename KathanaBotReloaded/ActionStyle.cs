namespace KathanaBotReloaded;

internal static class ActionStyle
{
    public static readonly string[] KeysAvailable = "1234567890ERF".Select(c => c.ToString()).Concat(Enumerable.Range(1, 10).Select(n => $"F{n}")).ToArray();
    public static ushort VirtualKey(string key) => key.Length == 1 ? (ushort)key[0] :
        key.StartsWith("F") && int.TryParse(key.AsSpan(1), out int n) && n is >= 1 and <= 10 ? (ushort)(0x70 + n - 1) : throw new ArgumentException("Unsupported key", nameof(key));
    public static Color ColorFor(string role, bool enabled = true) => !enabled ? Color.Gray : role switch
    {
        "Heal" => Color.Firebrick, "Mana" => Color.RoyalBlue, "Attack" => Color.FromArgb(180, 70, 0),
        "Buff" => Color.Purple, "Repair" => Color.FromArgb(145, 105, 0), "Target" => Color.Teal,
        "Loot" => Color.ForestGreen, _ => Color.Gray
    };
}
