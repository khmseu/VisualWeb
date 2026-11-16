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
        var bytes = inventory?.FileLength(source) ?? 0;
        File.Copy(source, destination);
        inventory?.RecordRuntimeFile(bytes);
    }

    private static void CopyTree(string source, string destination, WindowsStagingInventoryBuilder? inventory,
        bool appAssets, string relativeDirectory)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) { continue; }
            var name = Path.GetFileName(entry);
            var target = Path.Combine(destination, name);
            var relativePath = inventory is not null && appAssets
                ? Path.Combine(relativeDirectory, name)
                : string.Empty;
            if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyTree(entry, target, inventory, appAssets, relativePath);
            }
            else
            {
                var bytes = inventory?.FileLength(entry) ?? 0;
                File.Copy(entry, target);
                if (inventory is not null)
                {
                    if (appAssets) { inventory.RecordAppFile(relativePath, bytes); }
                    else { inventory.RecordRuntimeFile(bytes); }
                }
            }
        }
    }
}
