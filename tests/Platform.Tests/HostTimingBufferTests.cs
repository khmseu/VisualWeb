using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Windows.Sandbox;
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

    [Fact]
    public void StagingInventoryUsesBoundedStorageAndSharesOverflowReporting()
    {
        var buffer = new HostTimingBuffer();
        var inventory = new HostStagingInventory(7, new(3, 11), new(1, 2), new(1, 3),
            new(1, 6), new(1, 2), new(2, 9));

        buffer.RecordInventory(inventory);

        var snapshot = Assert.Single(buffer.InventorySnapshot());
        Assert.Equal(inventory, snapshot);

        for (var index = 1; index < HostTimingBuffer.Capacity; index++)
        {
            buffer.RecordInventory(inventory with { Renderer = index });
        }
        buffer.RecordInventory(inventory);

        Assert.Throws<InvalidOperationException>(() => buffer.InventorySnapshot());
        Assert.Throws<InvalidOperationException>(() => buffer.Snapshot());
    }

    [Fact]
    public void StagingCopyInventoryCountsSuccessfulFilesAndOverlappingSymbols()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisualWeb-copy-inventory-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        Directory.CreateDirectory(Path.Combine(source, "runtimes", "win-x64", "native"));
        Directory.CreateDirectory(Path.Combine(source, "runtimes", "win-arm64", "native"));
        try
        {
            WriteSized(Path.Combine(source, "root.dll"), 3);
            WriteSized(Path.Combine(source, "root.pdb"), 2);
            WriteSized(Path.Combine(source, "nested", "app.dll"), 5);
            WriteSized(Path.Combine(source, "nested", "app.pdb"), 7);
            WriteSized(Path.Combine(source, "runtimes", "win-x64", "native", "active.dll"), 11);
            WriteSized(Path.Combine(source, "runtimes", "win-arm64", "native", "other.dll"), 13);
            var linkedFile = Path.Combine(root, "linked.pdb");
            WriteSized(linkedFile, 17);
            File.CreateSymbolicLink(Path.Combine(source, "linked.pdb"), linkedFile);

            var inventory = new WindowsStagingInventoryBuilder("win-x64");
            WindowsStagingCopy.CopyTree(source, destination, inventory, appAssets: true);

            var runtimeRoot = Path.Combine(root, "runtime-source");
            var hostFxr = Path.Combine(runtimeRoot, "host", "fxr");
            var sharedRuntime = Path.Combine(runtimeRoot, "shared", "Microsoft.NETCore.App", "10.0.0");
            Directory.CreateDirectory(Path.Combine(hostFxr, "nested"));
            Directory.CreateDirectory(sharedRuntime);
            WriteSized(Path.Combine(hostFxr, "nested", "hostfxr.dll"), 19);
            WriteSized(Path.Combine(sharedRuntime, "coreclr.dll"), 23);
            WriteSized(Path.Combine(runtimeRoot, "dotnet.exe"), 29);
            WindowsStagingCopy.CopyTree(hostFxr, Path.Combine(root, "runtime-target", "host", "fxr"),
                inventory, appAssets: false);
            WindowsStagingCopy.CopyTree(sharedRuntime,
                Path.Combine(root, "runtime-target", "shared", "Microsoft.NETCore.App", "10.0.0"),
                inventory, appAssets: false);
            WindowsStagingCopy.CopyFile(Path.Combine(runtimeRoot, "dotnet.exe"),
                Path.Combine(root, "runtime-target", "dotnet.exe"), inventory);

            var record = inventory.Snapshot(renderer: 4);
            Assert.Equal(new HostCopyCounts(6, 41), record.App);
            Assert.Equal(new HostCopyCounts(4, 17), record.AppNonRid);
            Assert.Equal(new HostCopyCounts(1, 11), record.AppActiveRid);
            Assert.Equal(new HostCopyCounts(1, 13), record.AppOtherRid);
            Assert.Equal(new HostCopyCounts(2, 9), record.AppSymbols);
            Assert.Equal(new HostCopyCounts(3, 71), record.Runtime);
            Assert.Equal(4, record.Renderer);
            Assert.False(File.Exists(Path.Combine(destination, "linked.pdb")));

            // A disabled inventory still uses the production traversal but records no metadata.
            WindowsStagingCopy.CopyTree(source, output, inventory: null, appAssets: true);
            Assert.Equal(6, Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Count());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void WriteSized(string path, int length) =>
        File.WriteAllBytes(path, Enumerable.Range(0, length).Select(value => (byte)value).ToArray());
}
