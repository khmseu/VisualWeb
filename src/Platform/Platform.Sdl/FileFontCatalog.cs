using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Sdl;

public sealed class FileFontCatalog(IEnumerable<string> directories) : IFontCatalog
{
    private readonly string[] roots = directories.ToArray();

    public IReadOnlyList<string> GetFontFiles()
    {
        var files = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var root in roots.Where(Directory.Exists))
        {
            // Do not follow directory symlinks: font folders can contain cycles.
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var path in Directory.EnumerateFiles(root, "*", options))
            {
                if (Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc")
                {
                    files.Add(Path.GetFullPath(path));
                }
            }
        }

        return files.Order(StringComparer.Ordinal).ToArray();
    }
}
