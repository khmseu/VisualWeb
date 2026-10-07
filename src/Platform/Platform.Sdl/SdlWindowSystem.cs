using System.Runtime.InteropServices;
using SDL3;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Sdl;

public sealed record BackendSelection(string Backend, IReadOnlyList<string> InitializationFailures);

public sealed class SdlWindowSystem : IWindowSystem
{
    private static readonly object OwnershipLock = new();
    private static bool active;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<WindowId, SdlWindow> windows = [];
    private readonly string? previousDriverHint;
    private bool disposed;

    public string Backend { get; }
    public IReadOnlyList<string> InitializationFailures { get; }
    public event Action? QuitRequested;

    public SdlWindowSystem(IReadOnlyList<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        lock (OwnershipLock)
        {
            if (active)
            {
                throw new InvalidOperationException("Only one SDL window system may own video/events in a process.");
            }

            active = true;
        }

        var initialized = false;
        var hintRead = false;
        var ready = false;
        try
        {
            previousDriverHint = SDL.GetHint("SDL_VIDEO_DRIVER");
            hintRead = true;
            SDL.SetMainReady();
            var selection = SelectBackend(candidates, candidate =>
            {
                if (!SDL.SetHintWithPriority("SDL_VIDEO_DRIVER", candidate, SDL.HintPriority.Override))
                {
                    return "Could not select video driver: " + SDL.GetError();
                }

                if (SDL.InitSubSystem(SDL.InitFlags.Video))
                {
                    return null;
                }

                var error = SDL.GetError();
                SDL.QuitSubSystem(SDL.InitFlags.Video);
                return error;
            });
            initialized = true;
            Backend = SDL.GetCurrentVideoDriver()
                ?? throw new PlatformException("SDL did not report an initialized video driver.");
            if (Backend != selection.Backend)
            {
                throw new PlatformException($"Requested {selection.Backend}, but SDL initialized {Backend}.");
            }

            InitializationFailures = selection.InitializationFailures;
            ready = true;
        }
        finally
        {
            if (!ready)
            {
                if (initialized)
                {
                    SDL.QuitSubSystem(SDL.InitFlags.Video);
                }

                if (hintRead)
                {
                    RestoreDriverHint();
                }

                lock (OwnershipLock)
                {
                    active = false;
                }
            }
        }
    }

    public static BackendSelection SelectBackend(IReadOnlyList<string> candidates, Func<string, string?> tryInitialize)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(tryInitialize);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one backend is required.", nameof(candidates));
        }

        var failures = new List<string>();
        foreach (var candidate in candidates)
        {
            if (candidate is not ("wayland" or "x11" or "windows" or "dummy"))
            {
                throw new ArgumentException($"Unsupported SDL backend: {candidate}", nameof(candidates));
            }

            var error = tryInitialize(candidate);
            if (error is null)
            {
                return new(candidate, failures.ToArray());
            }

            failures.Add($"{candidate}: {error}");
        }

        throw new PlatformException("No video backend initialized. " + string.Join("; ", failures));
    }

    public IPlatformWindow CreateWindow(WindowOptions options)
    {
        EnsureAccess();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Height);
        var flags = SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity;
        if (options.Hidden)
        {
            flags |= SDL.WindowFlags.Hidden;
        }

        var handle = SDL.CreateWindow(options.Title, options.Width, options.Height, flags);
        if (handle == IntPtr.Zero)
        {
            throw Error("Create window");
        }

        var window = new SdlWindow(this, handle, new(SDL.GetWindowID(handle)));
        windows.Add(window.Id, window);
        return window;
    }

    public void PumpEvents()
    {
        EnsureAccess();
        while (SDL.PollEvent(out SDL.Event native))
        {
            if ((SDL.EventType)native.Type == SDL.EventType.Quit)
            {
                QuitRequested?.Invoke();
                if (disposed)
                {
                    return;
                }

                continue;
            }

            var translated = Translate(native);
            if (translated is not null && windows.TryGetValue(translated.Window, out var window))
            {
                window.Dispatch(translated);
                if (disposed)
                {
                    return;
                }
            }
        }
    }

    private WindowEvent? Translate(SDL.Event native)
    {
        var type = (SDL.EventType)native.Type;
        var windowId = new WindowId(native.Window.WindowID);
        return type switch
        {
            SDL.EventType.WindowCloseRequested => new CloseRequested(windowId),
            SDL.EventType.WindowExposed => new WindowExposed(windowId),
            SDL.EventType.WindowPixelSizeChanged => new WindowResized(windowId, new(native.Window.Data1, native.Window.Data2)),
            SDL.EventType.WindowDisplayScaleChanged when windows.TryGetValue(windowId, out var window) =>
                new WindowScaleChanged(windowId, window.DisplayScale),
            SDL.EventType.WindowFocusGained => new FocusChanged(windowId, true),
            SDL.EventType.WindowFocusLost => new FocusChanged(windowId, false),
            SDL.EventType.MouseMotion => new PointerMoved(new(native.Motion.WindowID), native.Motion.X, native.Motion.Y),
            SDL.EventType.MouseButtonDown or SDL.EventType.MouseButtonUp => new PointerButtonChanged(
                new(native.Button.WindowID), native.Button.Button, type == SDL.EventType.MouseButtonDown,
                native.Button.X, native.Button.Y),
            SDL.EventType.MouseWheel => new PointerScrolled(new(native.Wheel.WindowID),
                native.Wheel.X * (native.Wheel.Direction == SDL.MouseWheelDirection.Flipped ? -1 : 1),
                native.Wheel.Y * (native.Wheel.Direction == SDL.MouseWheelDirection.Flipped ? 1 : -1)),
            SDL.EventType.KeyDown or SDL.EventType.KeyUp => new KeyChanged(new(native.Key.WindowID),
                (int)native.Key.Scancode, (uint)native.Key.Key, (ushort)native.Key.Mod,
                type == SDL.EventType.KeyDown, native.Key.Repeat),
            SDL.EventType.TextInput => new TextEntered(new(native.Text.WindowID),
                Marshal.PtrToStringUTF8(native.Text.Text) ?? throw new PlatformException("SDL text input was null.")),
            _ => null
        };
    }

    internal void EnsureAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Environment.CurrentManagedThreadId != thread)
        {
            throw new InvalidOperationException("Window operations must run on the window system's creating thread.");
        }
    }

    internal void Remove(WindowId id) => windows.Remove(id);
    internal static PlatformException Error(string action) => new($"{action}: {SDL.GetError()}");

    private void RestoreDriverHint()
    {
        if (previousDriverHint is null)
        {
            SDL.ResetHint("SDL_VIDEO_DRIVER");
        }
        else
        {
            SDL.SetHintWithPriority("SDL_VIDEO_DRIVER", previousDriverHint, SDL.HintPriority.Override);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        EnsureAccess();
        foreach (var window in windows.Values.ToArray())
        {
            window.Dispose();
        }

        SDL.QuitSubSystem(SDL.InitFlags.Video);
        RestoreDriverHint();
        disposed = true;
        lock (OwnershipLock)
        {
            active = false;
        }
    }
}
