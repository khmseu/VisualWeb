namespace VisualWeb.Platform.Linux;

public static class LinuxBackendSelector
{
    public static IReadOnlyList<string> GetCandidates(
        string? waylandDisplay, string? x11Display, string? backendOverride = null)
    {
        if (backendOverride is not null)
        {
            if (backendOverride is not ("x11" or "wayland"))
            {
                throw new ArgumentException("Linux backend must be 'x11' or 'wayland'.", nameof(backendOverride));
            }

            return [backendOverride];
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(waylandDisplay))
        {
            candidates.Add("wayland");
        }

        if (!string.IsNullOrWhiteSpace(x11Display))
        {
            candidates.Add("x11");
        }

        if (candidates.Count == 0)
        {
            throw new PlatformNotSupportedException("No Wayland or X11 display was advertised. Set WAYLAND_DISPLAY or DISPLAY.");
        }

        return candidates;
    }
}
