using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Platform.Tests;

public sealed class HostTimingBufferTests
{
    [Fact]
    public void ConcurrentRecordsAreCopiedAndCapacityFailureIsDeferred()
    {
        var buffer = new HostTimingBuffer();
        Parallel.For(0, HostTimingBuffer.Capacity, index =>
            buffer.Record(new("fixture", index, 1, TimeSpan.FromMilliseconds(index))));
        var snapshot = buffer.Snapshot();
        Assert.Equal(HostTimingBuffer.Capacity, snapshot.Length);
        Assert.Equal(HostTimingBuffer.Capacity, snapshot.Select(record => record.Renderer).Distinct().Count());
        snapshot[0] = default;
        Assert.All(buffer.Snapshot(), record => Assert.Equal("fixture", record.Phase));

        // Full buffers must still accept instrumentation calls during return/cleanup.
        buffer.Record(new("overflow", 0, 0, TimeSpan.Zero));
        buffer.Record(new("cleanup", 0, 0, TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => buffer.Snapshot());
    }
}
