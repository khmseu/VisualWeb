namespace VisualWeb.Platform.Abstractions;

/// <summary>One host wall-clock measurement; no process or page payload is included.</summary>
public readonly record struct HostTiming(string Phase, long Renderer, long Request, TimeSpan Elapsed);

/// <summary>Bounded data-only storage for optional host diagnostics, without callbacks or I/O.</summary>
/// <remarks>Recording is allocation-free after construction. Overflow is latched and reported by
/// <see cref="Snapshot"/> rather than throwing from an ownership-critical instrumentation scope.</remarks>
public sealed class HostTimingBuffer
{
    public const int Capacity = 4096;
    private readonly object sync = new();
    private readonly HostTiming[] records = new HostTiming[Capacity];
    private int count;
    private bool overflowed;

    public void Record(HostTiming timing)
    {
        lock (sync)
        {
            if (count < records.Length) { records[count++] = timing; }
            else { overflowed = true; }
        }
    }

    /// <summary>Copies completed measurements, or explicitly reports overflow. Does not drain storage.</summary>
    public HostTiming[] Snapshot()
    {
        lock (sync)
        {
            if (overflowed) { throw new InvalidOperationException("Host timing buffer capacity exceeded; diagnostics are incomplete."); }
            return records[..count];
        }
    }
}
