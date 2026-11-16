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

    [Fact]
    public void RuntimeCopyMeasuresFixedComponentAndOperationAggregates()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisualWeb-runtime-copy-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "output");
        var hostFxr = Path.Combine(source, "host", "fxr");
        var runtime = Path.Combine(source, "shared", "Microsoft.NETCore.App", "10.0.0");
        Directory.CreateDirectory(Path.Combine(hostFxr, "nested"));
        Directory.CreateDirectory(Path.Combine(runtime, "nested"));
        try
        {
            WriteSized(Path.Combine(hostFxr, "nested", "hostfxr.dll"), 19);
            WriteSized(Path.Combine(runtime, "nested", "coreclr.dll"), 23);
            WriteSized(Path.Combine(source, "dotnet.exe"), 29);
            var linkedFile = Path.Combine(root, "linked.dll");
            WriteSized(linkedFile, 31);
            File.CreateSymbolicLink(Path.Combine(hostFxr, "linked.dll"), linkedFile);

            var diagnostics = new HostTimingBuffer();
            var inventory = new WindowsStagingInventoryBuilder("win-x64");
            WindowsStagingCopy.CopyRuntime(hostFxr, Path.Combine(output, "host", "fxr"),
                runtime, Path.Combine(output, "shared", "Microsoft.NETCore.App", "10.0.0"),
                Path.Combine(source, "dotnet.exe"), Path.Combine(output, "dotnet.exe"),
                inventory, diagnostics, renderer: 37);

            Assert.True(File.Exists(Path.Combine(output, "host", "fxr", "nested", "hostfxr.dll")));
            Assert.True(File.Exists(Path.Combine(output, "shared", "Microsoft.NETCore.App", "10.0.0", "nested", "coreclr.dll")));
            Assert.True(File.Exists(Path.Combine(output, "dotnet.exe")));
            Assert.False(File.Exists(Path.Combine(output, "host", "fxr", "linked.dll")));
            Assert.Equal(new HostCopyCounts(3, 71), inventory.Snapshot(37).Runtime);

            var records = diagnostics.Snapshot();
            var components = new[] { "hostfxr", "shared-runtime", "dotnet" };
            Assert.Equal(components.Length * 3, records.Length);
            foreach (var component in components)
            {
                var matching = records.Where(record =>
                    record.Phase.StartsWith($"windows.copy-runtime.{component}", StringComparison.Ordinal)).ToArray();
                Assert.Equal(3, matching.Length);
                Assert.Equal(37, Assert.Single(matching.Select(record => record.Renderer).Distinct()));
                Assert.Contains(matching, record => record.Phase.EndsWith(".copy", StringComparison.Ordinal));
                Assert.Contains(matching, record => record.Phase.EndsWith(".metadata", StringComparison.Ordinal));
                var total = Assert.Single(matching, record => record.Phase == $"windows.copy-runtime.{component}");
                var copy = Assert.Single(matching, record => record.Phase.EndsWith(".copy", StringComparison.Ordinal));
                var metadata = Assert.Single(matching, record => record.Phase.EndsWith(".metadata", StringComparison.Ordinal));
                Assert.True(total.Elapsed >= copy.Elapsed + metadata.Elapsed);
                Assert.All(matching, record => Assert.True(record.Elapsed >= TimeSpan.Zero));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RuntimeCopyDisabledDiagnosticsPreserveOutputAndDoNotRecord()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisualWeb-runtime-copy-disabled-" + Guid.NewGuid().ToString("N"));
        var hostFxr = Path.Combine(root, "source", "host", "fxr");
        var runtime = Path.Combine(root, "source", "shared", "Microsoft.NETCore.App", "10.0.0");
        var host = Path.Combine(root, "source", "dotnet.exe");
        var plainOutput = Path.Combine(root, "plain");
        var measuredOutput = Path.Combine(root, "measured");
        Directory.CreateDirectory(hostFxr);
        Directory.CreateDirectory(runtime);
        try
        {
            WriteSized(Path.Combine(hostFxr, "hostfxr.dll"), 19);
            WriteSized(Path.Combine(runtime, "coreclr.dll"), 23);
            WriteSized(host, 29);
            var diagnostics = new HostTimingBuffer();

            WindowsStagingCopy.CopyRuntime(hostFxr, Path.Combine(plainOutput, "host", "fxr"),
                runtime, Path.Combine(plainOutput, "shared", "Microsoft.NETCore.App", "10.0.0"),
                host, Path.Combine(plainOutput, "dotnet.exe"), inventory: null, timing: null, renderer: 0);
            Assert.Empty(diagnostics.Snapshot());
            WindowsStagingCopy.CopyRuntime(hostFxr, Path.Combine(measuredOutput, "host", "fxr"),
                runtime, Path.Combine(measuredOutput, "shared", "Microsoft.NETCore.App", "10.0.0"),
                host, Path.Combine(measuredOutput, "dotnet.exe"), inventory: null, timing: diagnostics, renderer: 8);

            var plainFiles = Directory.GetFiles(plainOutput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(plainOutput, path)).Order().ToArray();
            var measuredFiles = Directory.GetFiles(measuredOutput, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(measuredOutput, path)).Order().ToArray();
            Assert.Equal(plainFiles, measuredFiles);
            foreach (var relativePath in plainFiles)
            {
                Assert.Equal(File.ReadAllBytes(Path.Combine(plainOutput, relativePath)),
                    File.ReadAllBytes(Path.Combine(measuredOutput, relativePath)));
            }
            Assert.Equal(9, diagnostics.Snapshot().Length);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RuntimeCopyFailureRetainsSuccessfulInventoryAndDoesNotCopyLaterComponents()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisualWeb-runtime-copy-failure-" + Guid.NewGuid().ToString("N"));
        var hostFxr = Path.Combine(root, "hostfxr");
        var runtime = Path.Combine(root, "runtime");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(hostFxr);
        Directory.CreateDirectory(runtime);
        try
        {
            WriteSized(Path.Combine(hostFxr, "hostfxr.dll"), 19);
            WriteSized(Path.Combine(runtime, "blocked"), 23);
            var host = Path.Combine(root, "dotnet.exe");
            WriteSized(host, 29);
            Directory.CreateDirectory(Path.Combine(output, "shared"));
            WriteSized(Path.Combine(output, "shared", "blocked"), 1);
            var inventory = new WindowsStagingInventoryBuilder("win-x64");
            var diagnostics = new HostTimingBuffer();

            Assert.Throws<IOException>(() => WindowsStagingCopy.CopyRuntime(
                hostFxr, Path.Combine(output, "host", "fxr"),
                runtime, Path.Combine(output, "shared"),
                host, Path.Combine(output, "dotnet.exe"), inventory, diagnostics, renderer: 11));

            Assert.True(File.Exists(Path.Combine(output, "host", "fxr", "hostfxr.dll")));
            Assert.False(File.Exists(Path.Combine(output, "dotnet.exe")));
            Assert.Equal(new HostCopyCounts(1, 19), inventory.Snapshot(11).Runtime);
            Assert.Equal(6, diagnostics.Snapshot().Length);
            Assert.Contains(diagnostics.Snapshot(), record => record.Phase == "windows.copy-runtime.shared-runtime");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FullRuntimeDiagnosticBufferCannotInterruptCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisualWeb-runtime-copy-full-" + Guid.NewGuid().ToString("N"));
        var hostFxr = Path.Combine(root, "hostfxr");
        var runtime = Path.Combine(root, "runtime");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(hostFxr);
        Directory.CreateDirectory(runtime);
        try
        {
            WriteSized(Path.Combine(hostFxr, "hostfxr.dll"), 19);
            WriteSized(Path.Combine(runtime, "coreclr.dll"), 23);
            var host = Path.Combine(root, "dotnet.exe");
            WriteSized(host, 29);
            var diagnostics = new HostTimingBuffer();
            for (var index = 0; index < HostTimingBuffer.Capacity; index++)
            {
                diagnostics.Record(new("full", index, 0, TimeSpan.Zero));
            }

            WindowsStagingCopy.CopyRuntime(hostFxr, Path.Combine(output, "host", "fxr"),
                runtime, Path.Combine(output, "shared"),
                host, Path.Combine(output, "dotnet.exe"), inventory: null, timing: diagnostics, renderer: 5);

            Assert.True(File.Exists(Path.Combine(output, "host", "fxr", "hostfxr.dll")));
            Assert.True(File.Exists(Path.Combine(output, "shared", "coreclr.dll")));
            Assert.True(File.Exists(Path.Combine(output, "dotnet.exe")));
            Assert.Throws<InvalidOperationException>(() => diagnostics.Snapshot());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void WriteSized(string path, int length) =>
        File.WriteAllBytes(path, Enumerable.Range(0, length).Select(value => (byte)value).ToArray());
}
