namespace VisualWeb.Platform.Abstractions;

public readonly record struct PixelSize(int Width, int Height);
public readonly record struct WindowId(uint Value);
public sealed record WindowOptions(string Title, int Width = 800, int Height = 600, bool Hidden = false);

/// <summary>Owns native windows and dispatches input on its creating thread.</summary>
/// <remarks>Reference: sdl-video; <see href="https://wiki.libsdl.org/SDL3/CategoryVideo">SDL video</see>.</remarks>
public interface IWindowSystem : IDisposable
{
    string Backend { get; }
    IPlatformWindow CreateWindow(WindowOptions options);
    void PumpEvents();
    event Action? QuitRequested;
}

/// <summary>A window whose close requests are handled by its owner, not automatically accepted.</summary>
/// <remarks>Reference: sdl-create-window; <see href="https://wiki.libsdl.org/SDL3/SDL_CreateWindow">SDL_CreateWindow</see>.</remarks>
public interface IPlatformWindow : IDisposable
{
    WindowId Id { get; }
    PixelSize LogicalSize { get; }
    PixelSize PixelSize { get; }
    float DisplayScale { get; }
    float PixelDensity { get; }
    IPixelSurface Surface { get; }
    event Action<WindowEvent>? EventReceived;
    void SetTitle(string title);
    void SetTextInput(bool enabled);
    void SetClipboardText(string text);
    string GetClipboardText();
}

/// <summary>Presents opaque BGRA32 pixels at the window's current physical pixel size.</summary>
/// <remarks>Rows have explicit stride; the implementation does not retain the supplied memory.
/// Reference: sdl-window-surface; <see href="https://wiki.libsdl.org/SDL3/SDL_GetWindowSurface">SDL_GetWindowSurface</see>.</remarks>
public interface IPixelSurface
{
    void Present(ReadOnlySpan<byte> pixels, PixelSize size, int stride);
}

public abstract record WindowEvent(WindowId Window);
public sealed record CloseRequested(WindowId Window) : WindowEvent(Window);
public sealed record WindowResized(WindowId Window, PixelSize Size) : WindowEvent(Window);
public sealed record WindowExposed(WindowId Window) : WindowEvent(Window);
public sealed record WindowScaleChanged(WindowId Window, float Scale) : WindowEvent(Window);
public sealed record FocusChanged(WindowId Window, bool Focused) : WindowEvent(Window);
/// <summary>Coordinates are window-local logical units, not framebuffer pixels.</summary>
public sealed record PointerMoved(WindowId Window, float X, float Y) : WindowEvent(Window);
public sealed record PointerButtonChanged(WindowId Window, byte Button, bool Pressed, float X, float Y) : WindowEvent(Window);
/// <summary>Wheel amounts, not pointer coordinates: positive X scrolls right and positive Y scrolls down.</summary>
/// <remarks>Reference: sdl-events; <see href="https://wiki.libsdl.org/SDL3/SDL_MouseWheelEvent">SDL wheel event</see>.
/// Native direction is normalized; pointer location is supplied separately by movement/button events.</remarks>
public sealed record PointerScrolled(WindowId Window, float X, float Y) : WindowEvent(Window);
public sealed record KeyChanged(WindowId Window, int ScanCode, uint Key, ushort Modifiers, bool Pressed, bool Repeat) : WindowEvent(Window);
public sealed record TextEntered(WindowId Window, string Text) : WindowEvent(Window);

public sealed record PlatformPaths(string Configuration, string Cache, string Data);

/// <summary>Enumerates installed font files, not CSS font-family matching or shaping.</summary>
/// <remarks>References: windows-fonts; <see href="https://learn.microsoft.com/en-us/windows/win32/gdi/about-fonts">Windows fonts</see>
/// and fontconfig; <see href="https://www.freedesktop.org/wiki/Software/fontconfig/">Fontconfig</see>.</remarks>
public interface IFontCatalog
{
    IReadOnlyList<string> GetFontFiles();
}

public sealed record ProcessLaunchOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?>? Environment = null,
    string? WorkingDirectory = null,
    bool RequireSandbox = false);

/// <summary>Launches child processes without shell interpolation.</summary>
/// <remarks>Reference: dotnet-process-start; <see href="https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo">ProcessStartInfo</see>.
/// Requiring an unavailable sandbox must fail before starting a process.</remarks>
public interface IProcessLauncher
{
    IChildProcess Start(ProcessLaunchOptions options);
}

/// <summary>Owns a child process handle; disposal alone does not terminate the process.</summary>
/// <remarks>Reference: dotnet-process; <see href="https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process">Process</see>.</remarks>
public interface IChildProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    void Terminate();
}

/// <summary>Composition boundary for host OS services.</summary>
/// <remarks>Clock uses the official .NET <see cref="TimeProvider"/> contract.
/// Native confinement is deferred; launchers reject RequireSandbox until implemented.</remarks>
public interface IPlatformServices
{
    PlatformPaths Paths { get; }
    TimeProvider Clock { get; }
    IFontCatalog Fonts { get; }
    IProcessLauncher Processes { get; }
    IWindowSystem OpenWindows(string? backendOverride = null);
}

public sealed class PlatformException(string message) : Exception(message);

public static class PixelBuffer
{
    public static void Validate(int length, PixelSize size, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        if (stride < checked(size.Width * 4))
        {
            throw new ArgumentException("Stride must cover a full BGRA32 row.", nameof(stride));
        }

        if (length < checked((size.Height - 1) * stride + size.Width * 4))
        {
            throw new ArgumentException("Pixel buffer is smaller than the required rows.", nameof(length));
        }
    }
}
