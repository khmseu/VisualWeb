using System.Diagnostics;
using System.Globalization;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Browser;

/// <summary>Optional bounded host-side smoke wall-clock diagnostics, exported explicitly after cleanup.</summary>
public sealed class SmokeTimings
{
    internal HostTimingBuffer Buffer { get; } = new();
    private long nextRenderer;

    internal long NextRenderer() => Interlocked.Increment(ref nextRenderer);

    internal Scope Measure(string phase, long renderer = 0, long request = 0) =>
        new(this, phase, renderer, request);

    internal void Record(string phase, long renderer, long request, TimeSpan elapsed) =>
        Buffer.Record(new(phase, renderer, request, elapsed));

    /// <summary>Exports a snapshot in invariant culture. Writer and capacity failures propagate explicitly.</summary>
    /// <remarks>No writer is retained or invoked by instrumentation scopes. Call after owned resources are
    /// cleaned up, not from an exception-unwinding finally; export must not replace an operation failure.
    /// Concurrent recording is supported; callers serialize exports to their own writer.</remarks>
    public void WriteTo(TextWriter output)
    {
        var records = Buffer.Snapshot();
        var inventories = Buffer.InventorySnapshot();
        foreach (var record in records)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"smoke-timing phase={record.Phase} renderer={record.Renderer} request={record.Request} ms={record.Elapsed.TotalMilliseconds:F3}"));
        }
        foreach (var inventory in inventories)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"smoke-inventory v=1 renderer={inventory.Renderer} units=files,bytes " +
                $"app-files={inventory.App.Files} app-bytes={inventory.App.Bytes} " +
                $"app-non-rid-files={inventory.AppNonRid.Files} app-non-rid-bytes={inventory.AppNonRid.Bytes} " +
                $"app-active-rid-files={inventory.AppActiveRid.Files} app-active-rid-bytes={inventory.AppActiveRid.Bytes} " +
                $"app-other-rid-files={inventory.AppOtherRid.Files} app-other-rid-bytes={inventory.AppOtherRid.Bytes} " +
                $"app-symbol-files={inventory.AppSymbols.Files} app-symbol-bytes={inventory.AppSymbols.Bytes} " +
                $"runtime-files={inventory.Runtime.Files} runtime-bytes={inventory.Runtime.Bytes}"));
        }
    }

    internal readonly struct Scope(SmokeTimings? owner, string phase, long renderer, long request) : IDisposable
    {
        private readonly long start = owner is null ? 0 : Stopwatch.GetTimestamp();
        public void Dispose()
        {
            if (owner is not null) { owner.Record(phase, renderer, request, Stopwatch.GetElapsedTime(start)); }
        }
    }
}
