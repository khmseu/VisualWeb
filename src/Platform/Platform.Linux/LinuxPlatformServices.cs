using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Sdl;

namespace VisualWeb.Platform.Linux;

public sealed class LinuxPlatformServices : IPlatformServices
{
    public PlatformPaths Paths { get; }
    public TimeProvider Clock => TimeProvider.System;
    public IFontCatalog Fonts { get; }
    public IProcessLauncher Processes { get; } = new SystemProcessLauncher();

    public LinuxPlatformServices()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Linux services require Linux.");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            throw new PlatformException("Cannot determine the Linux home directory.");
        }

        Paths = ResolvePaths(home, Environment.GetEnvironmentVariable);
        var dataHome = Path.GetDirectoryName(Paths.Data)
            ?? throw new PlatformException("The XDG data path has no parent.");
        Fonts = new FileFontCatalog(["/usr/share/fonts", "/usr/local/share/fonts",
            Path.Combine(home, ".fonts"), Path.Combine(dataHome, "fonts")]);
    }

    public IWindowSystem OpenWindows(string? backendOverride = null) => new SdlWindowSystem(
        LinuxBackendSelector.GetCandidates(
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
            Environment.GetEnvironmentVariable("DISPLAY"),
            backendOverride ?? Environment.GetEnvironmentVariable("VISUALWEB_VIDEO_BACKEND")));

    /// <summary>Resolves app directories following the XDG absolute-path rule.</summary>
    /// <remarks>Spec: xdg-directories; <see href="https://specifications.freedesktop.org/basedir/0.8/#basics">XDG basics</see>.</remarks>
    public static PlatformPaths ResolvePaths(string home, Func<string, string?> getEnvironment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(home);
        ArgumentNullException.ThrowIfNull(getEnvironment);
        if (!Path.IsPathFullyQualified(home))
        {
            throw new ArgumentException("Home directory must be absolute.", nameof(home));
        }

        string Resolve(string variable, string fallback)
        {
            var value = getEnvironment(variable);
            // The XDG specification says relative values must be ignored.
            return !string.IsNullOrEmpty(value) && Path.IsPathFullyQualified(value) ? value : fallback;
        }

        return new(
            Path.Combine(Resolve("XDG_CONFIG_HOME", Path.Combine(home, ".config")), "VisualWeb"),
            Path.Combine(Resolve("XDG_CACHE_HOME", Path.Combine(home, ".cache")), "VisualWeb"),
            Path.Combine(Resolve("XDG_DATA_HOME", Path.Combine(home, ".local", "share")), "VisualWeb"));
    }
}
