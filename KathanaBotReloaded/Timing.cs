namespace KathanaBotReloaded;

public sealed class KeySlot(string key)
{
    public string Key { get; set; } = key;
    public bool Enabled { get; set; }
    public decimal Seconds { get; set; } = 1;
    public string Role { get; set; } = "Attack";
    public decimal Threshold { get; set; } = 50;
    [System.Text.Json.Serialization.JsonIgnore] public long Due { get; set; }
}

public sealed class KeySchedule(List<KeySlot> slots)
{
    public long NextAllowed { get; private set; }
    public static int Vary(int milliseconds, int minimum = 20) =>
        Random.Shared.Next(Math.Max(minimum, milliseconds - 500), checked(milliseconds + 501));

    public void Start(long now)
    {
        NextAllowed = now;
        foreach (var slot in slots)
            slot.Due = now + (long)(slot.Seconds * 1000);
    }

    public KeySlot? Due(long now, Func<KeySlot, bool>? eligible = null) => now < NextAllowed ? null :
        slots.Where(s => s.Enabled && s.Due <= now && (eligible?.Invoke(s) ?? true))
            .OrderBy(s => s.Role switch { "Heal" => 0, "Mana" => 1, "Repair" => 2, _ => 3 })
            .ThenBy(s => s.Due).FirstOrDefault();

    public void Complete(KeySlot slot, long now)
    {
        slot.Due = now + (long)(slot.Seconds * 1000);
        NextAllowed = now;
    }

    public static void SelfTest()
    {
        foreach (int baseline in new[] { 100, 500, 1000, 2000 })
        {
            var values = Enumerable.Range(0, 10000).Select(_ => Vary(baseline)).ToArray();
            if (values.Any(n => n < Math.Max(20, baseline - 500) || n > baseline + 500) || values.Distinct().Count() < 100)
                throw new Exception("Random timing range or freshness failed.");
        }
        var slots = new List<KeySlot> { new("E") { Enabled = true }, new("R") { Enabled = true }, new("F") };
        var schedule = new KeySchedule(slots);
        schedule.Start(0);
        if (schedule.Due(0) != null) throw new Exception("Initial wait missing.");
        var first = schedule.Due(2000) ?? throw new Exception("No due key.");
        schedule.Complete(first, 2000);
        if (schedule.Due(2000) is null) throw new Exception("Ready skills must not wait for a global gap.");
        if (schedule.Due(4000) == first) throw new Exception("Other keys starved.");
        if (schedule.NextAllowed != 2000) throw new Exception("An artificial global gap remains.");
    }
}
