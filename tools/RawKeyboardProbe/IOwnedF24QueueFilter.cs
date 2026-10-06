namespace RawKeyboardProbe;

internal interface IOwnedF24QueueFilter : IDisposable
{
    bool EnableSuppression(int expectedBaselinePairs);
    OwnedF24QueueFilterSnapshot Snapshot();
}

internal sealed record OwnedF24MarkerSample
{
    public long Tick { get; init; }
    public string ActualFullExtraInfo { get; init; } = "";
    public bool MatchesOwnedMarker { get; init; }
    public bool Up { get; init; }
    public uint Message { get; init; }
    public string Stage { get; init; } = "baseline";
}

/// <summary>Preallocated observations of at most 16 owned-window F24 callbacks.</summary>
internal sealed class OwnedF24MarkerRecorder
{
    private readonly RawSample[] samples = new RawSample[16];
    private int count;

    // The owning GUI thread calls both methods. Recording does not allocate or
    // format strings inside a hook; Snapshot produces the report afterwards.
    public void Record(ulong actual, bool matches, bool up, uint message, bool filtered)
    {
        if (count >= samples.Length) return;
        samples[count++] = new RawSample(Environment.TickCount64, actual, matches, up, message, filtered);
    }

    public OwnedF24MarkerSample[] Snapshot()
    {
        var result = new OwnedF24MarkerSample[count];
        for (var i = 0; i < result.Length; i++)
        {
            var sample = samples[i];
            result[i] = new OwnedF24MarkerSample
            {
                Tick = sample.Tick,
                ActualFullExtraInfo = $"0x{sample.Actual:X16}",
                MatchesOwnedMarker = sample.Matches,
                Up = sample.Up,
                Message = sample.Message,
                Stage = sample.Filtered ? "filtered" : "baseline"
            };
        }
        return result;
    }

    private readonly record struct RawSample(long Tick, ulong Actual, bool Matches, bool Up, uint Message, bool Filtered);
}
