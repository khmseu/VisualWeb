using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Sdl;

namespace VisualWeb.Platform.Windows;

public sealed class WindowsPlatformServices : IPlatformServices
{
    public PlatformPaths Paths { get; }
    public TimeProvider Clock => TimeProvider.System;
    public IFontCatalog Fonts { get; }
    public IProcessLauncher Processes { get; } = new SystemProcessLauncher();

    public WindowsPlatformServices()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows services require Windows.");
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(local) || string.IsNullOrEmpty(roaming))
        {
            throw new PlatformException("Cannot determine Windows application data paths.");
        }

        Paths = new(Path.Combine(roaming, "VisualWeb"), Path.Combine(local, "VisualWeb", "Cache"),
            Path.Combine(local, "VisualWeb", "Data"));
        Fonts = new FileFontCatalog([
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(local, "Microsoft", "Windows", "Fonts")]);
    }

    public IWindowSystem OpenWindows(string? backendOverride = null)
    {
        var backend = backendOverride ?? Environment.GetEnvironmentVariable("VISUALWEB_VIDEO_BACKEND") ?? "windows";
        if (backend != "windows")
        {
            throw new ArgumentException("Windows backend must be 'windows'.", nameof(backendOverride));
        }

        return new SdlWindowSystem(["windows"]);
    }
}
