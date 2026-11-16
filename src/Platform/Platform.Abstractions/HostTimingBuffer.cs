namespace VisualWeb.Platform.Abstractions;

/// <summary>One host wall-clock measurement; no process or page payload is included.</summary>
public readonly record struct HostTiming(string Phase, long Renderer, long Request, TimeSpan Elapsed);

/// <summary>One fixed-code error from optional bounded diagnostics.</summary>
public enum HostTimingDiagnosticErrorCode
{
    WorkerBatchMalformed,
    WorkerBatchDuplicate,
    WorkerBatchOverlong,
    WorkerBatchPartial,
    WorkerBatchMissing,
    WorkerDrainTimeout
}

public readonly record struct HostTimingDiagnosticError(long Renderer, HostTimingDiagnosticErrorCode Code);

/// <summary>Successful staged file count and copied bytes.</summary>
public readonly record struct HostCopyCounts(long Files, long Bytes);

/// <summary>Bounded aggregate of staged application/runtime files; symbol counts overlap app partitions.</summary>
public readonly record struct HostStagingInventory(
    long Renderer,
    HostCopyCounts App,
    HostCopyCounts AppNonRid,
    HostCopyCounts AppActiveRid,
    HostCopyCounts AppOtherRid,
    HostCopyCounts AppSymbols,
    HostCopyCounts Runtime);

/// <summary>Bounded data-only storage for optional host diagnostics, without callbacks or I/O.</summary>
/// <remarks>Recording is allocation-free after construction. Overflow is latched and reported by a
/// snapshot rather than throwing from an ownership-critical instrumentation scope.</remarks>
public sealed class HostTimingBuffer
{
    public const int Capacity = 4096;
    private readonly object sync = new();
    private readonly HostTiming[] records = new HostTiming[Capacity];
    private readonly HostStagingInventory[] inventories = new HostStagingInventory[Capacity];
    private readonly HostTimingDiagnosticError[] errors = new HostTimingDiagnosticError[Capacity];
    private int count;
    private int inventoryCount;
    private int errorCount;
    private bool overflowed;

    public void Record(HostTiming timing)
    {
        lock (sync)
        {
            if (count < records.Length) { records[count++] = timing; }
            else { overflowed = true; }
        }
    }

    public void RecordInventory(HostStagingInventory inventory)
    {
        lock (sync)
        {
            if (inventoryCount < inventories.Length) { inventories[inventoryCount++] = inventory; }
            else { overflowed = true; }
        }
    }

    public void RecordDiagnosticError(HostTimingDiagnosticError error)
    {
        lock (sync)
        {
            if (errorCount < errors.Length) { errors[errorCount++] = error; }
            else { overflowed = true; }
        }
    }

    /// <summary>Copies completed measurements, or explicitly reports overflow. Does not drain storage.</summary>
    public HostTiming[] Snapshot()
    {
        lock (sync)
        {
            if (overflowed) { throw new InvalidOperationException("Host diagnostic buffer capacity exceeded; diagnostics are incomplete."); }
            return records[..count];
        }
    }

    /// <summary>Copies completed staging aggregates, or explicitly reports any diagnostic overflow.</summary>
    public HostStagingInventory[] InventorySnapshot()
    {
        lock (sync)
        {
            if (overflowed) { throw new InvalidOperationException("Host diagnostic buffer capacity exceeded; diagnostics are incomplete."); }
            return inventories[..inventoryCount];
        }
    }

    /// <summary>Copies fixed-code diagnostic errors, or explicitly reports any diagnostic overflow.</summary>
    public HostTimingDiagnosticError[] DiagnosticErrorSnapshot()
    {
        lock (sync)
        {
            if (overflowed) { throw new InvalidOperationException("Host diagnostic buffer capacity exceeded; diagnostics are incomplete."); }
            return errors[..errorCount];
        }
    }
}
