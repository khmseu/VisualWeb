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
        foreach (var record in Buffer.Snapshot())
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"smoke-timing phase={record.Phase} renderer={record.Renderer} request={record.Request} ms={record.Elapsed.TotalMilliseconds:F3}"));
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
