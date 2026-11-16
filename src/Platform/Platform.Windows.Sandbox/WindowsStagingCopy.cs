using System.Diagnostics;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Windows.Sandbox;

internal sealed class WindowsStagingInventoryBuilder(string activeWindowsRid)
{
    private long appFiles;
    private long appBytes;
    private long nonRidFiles;
    private long nonRidBytes;
    private long activeRidFiles;
    private long activeRidBytes;
    private long otherRidFiles;
    private long otherRidBytes;
    private long symbolFiles;
    private long symbolBytes;
    private long runtimeFiles;
    private long runtimeBytes;

    internal long FileLength(string path) => new FileInfo(path).Length;

    internal void RecordAppFile(string relativePath, long bytes)
    {
        appFiles = checked(appFiles + 1);
        appBytes = checked(appBytes + bytes);

        var segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && string.Equals(segments[0], "runtimes", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(segments[1], activeWindowsRid, StringComparison.OrdinalIgnoreCase))
            {
                activeRidFiles = checked(activeRidFiles + 1);
                activeRidBytes = checked(activeRidBytes + bytes);
            }
            else
            {
                otherRidFiles = checked(otherRidFiles + 1);
                otherRidBytes = checked(otherRidBytes + bytes);
            }
        }
        else
        {
            nonRidFiles = checked(nonRidFiles + 1);
            nonRidBytes = checked(nonRidBytes + bytes);
        }

        if (string.Equals(Path.GetExtension(relativePath), ".pdb", StringComparison.OrdinalIgnoreCase))
        {
            symbolFiles = checked(symbolFiles + 1);
            symbolBytes = checked(symbolBytes + bytes);
        }
    }

    internal void RecordRuntimeFile(long bytes)
    {
        runtimeFiles = checked(runtimeFiles + 1);
        runtimeBytes = checked(runtimeBytes + bytes);
    }

    internal HostStagingInventory Snapshot(long renderer) => new(renderer,
        new(appFiles, appBytes),
        new(nonRidFiles, nonRidBytes),
        new(activeRidFiles, activeRidBytes),
        new(otherRidFiles, otherRidBytes),
        new(symbolFiles, symbolBytes),
        new(runtimeFiles, runtimeBytes));
}

internal static class WindowsStagingCopy
{
    internal static void CopyTree(string source, string destination,
        WindowsStagingInventoryBuilder? inventory, bool appAssets)
    {
        CopyTree(source, destination, inventory, appAssets, string.Empty);
    }

    internal static void CopyFile(string source, string destination, WindowsStagingInventoryBuilder? inventory)
    {
        CopyFile(source, destination, inventory, measurement: null);
    }

    internal static void CopyRuntime(string hostFxrSource, string hostFxrDestination,
        string runtimeSource, string runtimeDestination, string hostSource, string hostDestination,
        WindowsStagingInventoryBuilder? inventory, HostTimingBuffer? timing, long renderer)
    {
        var hostFxrMeasurement = timing is null ? null : new RuntimeCopyMeasurement(timing, renderer, "hostfxr");
        try { CopyTree(hostFxrSource, hostFxrDestination, inventory, false, string.Empty, hostFxrMeasurement); }
        finally { hostFxrMeasurement?.Record(); }

        var runtimeMeasurement = timing is null ? null : new RuntimeCopyMeasurement(timing, renderer, "shared-runtime");
        try { CopyTree(runtimeSource, runtimeDestination, inventory, false, string.Empty, runtimeMeasurement); }
        finally { runtimeMeasurement?.Record(); }

        var hostMeasurement = timing is null ? null : new RuntimeCopyMeasurement(timing, renderer, "dotnet");
        try { CopyFile(hostSource, hostDestination, inventory, hostMeasurement); }
        finally { hostMeasurement?.Record(); }
    }

    private static void CopyFile(string source, string destination, WindowsStagingInventoryBuilder? inventory,
        RuntimeCopyMeasurement? measurement)
    {
        var bytes = inventory is null ? 0
            : measurement is null ? inventory.FileLength(source)
            : measurement.FileLength(inventory, source);
        if (measurement is null) { File.Copy(source, destination); }
        else { measurement.CopyFile(source, destination); }
        inventory?.RecordRuntimeFile(bytes);
    }

    private static void CopyTree(string source, string destination, WindowsStagingInventoryBuilder? inventory,
        bool appAssets, string relativeDirectory, RuntimeCopyMeasurement? measurement = null)
    {
        if (measurement is null) { Directory.CreateDirectory(destination); }
        else { measurement.CreateDirectory(destination); }

        var entries = measurement is null
            ? Directory.EnumerateFileSystemEntries(source)
            : measurement.Enumerate(source);
        var enumerator = measurement is null
            ? entries.GetEnumerator()
            : measurement.GetEnumerator(entries);
        using (enumerator)
        {
            while (measurement is null ? enumerator.MoveNext() : measurement.MoveNext(enumerator))
            {
                var entry = enumerator.Current;
                var attributes = measurement is null
                    ? File.GetAttributes(entry)
                    : measurement.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) { continue; }
                var name = Path.GetFileName(entry);
                var target = Path.Combine(destination, name);
                var relativePath = inventory is not null && appAssets
                    ? Path.Combine(relativeDirectory, name)
                    : string.Empty;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    CopyTree(entry, target, inventory, appAssets, relativePath, measurement);
                }
                else
                {
                    var bytes = inventory is null ? 0
                        : measurement is null ? inventory.FileLength(entry)
                        : measurement.FileLength(inventory, entry);
                    if (measurement is null) { File.Copy(entry, target); }
                    else { measurement.CopyFile(entry, target); }
                    if (inventory is not null)
                    {
                        if (appAssets) { inventory.RecordAppFile(relativePath, bytes); }
                        else { inventory.RecordRuntimeFile(bytes); }
                    }
                }
            }
        }
    }

    private sealed class RuntimeCopyMeasurement(HostTimingBuffer buffer, long renderer, string component)
    {
        private readonly long totalStart = Stopwatch.GetTimestamp();
        private TimeSpan copyElapsed;
        private TimeSpan metadataElapsed;

        internal void CreateDirectory(string path)
        {
            var start = Stopwatch.GetTimestamp();
            try { Directory.CreateDirectory(path); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal IEnumerable<string> Enumerate(string path)
        {
            var start = Stopwatch.GetTimestamp();
            try { return Directory.EnumerateFileSystemEntries(path); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal IEnumerator<string> GetEnumerator(IEnumerable<string> entries)
        {
            var start = Stopwatch.GetTimestamp();
            try { return entries.GetEnumerator(); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal bool MoveNext(IEnumerator<string> entries)
        {
            var start = Stopwatch.GetTimestamp();
            try { return entries.MoveNext(); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal FileAttributes GetAttributes(string path)
        {
            var start = Stopwatch.GetTimestamp();
            try { return File.GetAttributes(path); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal long FileLength(WindowsStagingInventoryBuilder inventory, string path)
        {
            var start = Stopwatch.GetTimestamp();
            try { return inventory.FileLength(path); }
            finally { metadataElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal void CopyFile(string source, string destination)
        {
            var start = Stopwatch.GetTimestamp();
            try { File.Copy(source, destination); }
            finally { copyElapsed += Stopwatch.GetElapsedTime(start); }
        }

        internal void Record()
        {
            var prefix = "windows.copy-runtime." + component;
            buffer.Record(new(prefix, renderer, 0, Stopwatch.GetElapsedTime(totalStart)));
            buffer.Record(new(prefix + ".copy", renderer, 0, copyElapsed));
            buffer.Record(new(prefix + ".metadata", renderer, 0, metadataElapsed));
        }

    }
}
