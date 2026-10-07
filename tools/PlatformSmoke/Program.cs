using SDL3;
using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Linux;
using VisualWeb.Platform.Sdl;
using VisualWeb.Platform.Windows;

var skipTextInput = args.Contains("--skip-text-input");
var backends = args.Where(argument => argument != "--skip-text-input").ToArray();
if (backends.Length > 1 || (backends.Length == 1 && backends[0] is not ("dummy" or "x11" or "wayland" or "windows")))
{
    Console.Error.WriteLine("Usage: PlatformSmoke [dummy|x11|wayland|windows] [--skip-text-input]");
    return 2;
}

try
{
    IPlatformServices platform = OperatingSystem.IsLinux() ? new LinuxPlatformServices()
        : OperatingSystem.IsWindows() ? new WindowsPlatformServices()
        : throw new PlatformNotSupportedException("PlatformSmoke supports Linux and Windows.");
    using var system = backends is ["dummy"] ? new SdlWindowSystem(["dummy"])
        : platform.OpenWindows(backends.SingleOrDefault());
    if (system is SdlWindowSystem nativeSystem)
    {
        foreach (var failure in nativeSystem.InitializationFailures)
        {
            Console.Error.WriteLine("Backend fallback: " + failure);
        }
    }

    using var first = system.CreateWindow(new("VisualWeb platform smoke A", 64, 48));
    using var second = system.CreateWindow(new("VisualWeb platform smoke B", 64, 48));
    Require(first.Id != second.Id, "Windows must have distinct IDs.");
    Require(first.DisplayScale > 0, "Invalid display scale.");
    Require(first.PixelDensity > 0 && first.LogicalSize.Width > 0, "Invalid logical size or pixel density.");
    first.SetTitle("VisualWeb platform smoke");
    if (skipTextInput)
    {
        Console.Error.WriteLine("Text input validation explicitly skipped; compositor protocol support is not certified.");
    }
    else
    {
        first.SetTextInput(true);
        first.SetTextInput(false);
    }

    var size = first.PixelSize;
    var pixels = new byte[checked(size.Width * size.Height * 4)];
    for (var offset = 0; offset < pixels.Length; offset += 4)
    {
        pixels[offset] = 17;
        pixels[offset + 1] = 83;
        pixels[offset + 2] = 211;
        pixels[offset + 3] = 255;
    }

    first.Surface.Present(pixels, size, checked(size.Width * 4));
    var surface = SDL.GetWindowSurface(SDL.GetWindowFromID(first.Id.Value));
    Require(SDL.ReadSurfacePixel(surface, 0, 0, out var red, out var green, out var blue, out _), SDL.GetError());
    Require(red == 211 && green == 83 && blue == 17, $"Wrong pixel order: R={red}, G={green}, B={blue}.");

    var firstEvents = new List<WindowEvent>();
    var secondEvents = new List<WindowEvent>();
    first.EventReceived += firstEvents.Add;
    second.EventReceived += secondEvents.Add;
    var close = new SDL.Event
    {
        Window = new SDL.WindowEvent
        {
            Type = SDL.EventType.WindowCloseRequested,
            WindowID = second.Id.Value
        }
    };
    Require(SDL.PushEvent(ref close), SDL.GetError());
    var pointer = new SDL.Event
    {
        Motion = new SDL.MouseMotionEvent
        {
            Type = SDL.EventType.MouseMotion,
            WindowID = first.Id.Value,
            X = 12.5f,
            Y = 9.5f
        }
    };
    Require(SDL.PushEvent(ref pointer), SDL.GetError());
    foreach (var direction in new[] { SDL.MouseWheelDirection.Normal, SDL.MouseWheelDirection.Flipped })
    {
        var wheel = new SDL.Event
        {
            Wheel = new SDL.MouseWheelEvent
            {
                Type = SDL.EventType.MouseWheel,
                WindowID = first.Id.Value,
                X = 2,
                Y = -3,
                Direction = direction
            }
        };
        Require(SDL.PushEvent(ref wheel), SDL.GetError());
    }
    system.PumpEvents();
    Require(secondEvents.OfType<CloseRequested>().Any(), "Close event was not routed to second window.");
    Require(!firstEvents.OfType<CloseRequested>().Any(), "Close event leaked into first window.");
    Require(firstEvents.OfType<PointerMoved>().Any(e => e.X == 12.5f && e.Y == 9.5f),
        "Pointer event coordinates were not translated.");
    Require(!secondEvents.OfType<PointerMoved>().Any(e => e.X == 12.5f && e.Y == 9.5f),
        "Pointer event leaked into second window.");
    var wheels = firstEvents.OfType<PointerScrolled>().ToArray();
    Require(wheels.Length == 2 && wheels[0].X == 2 && wheels[0].Y == 3
        && wheels[1].X == -2 && wheels[1].Y == -3, "Wheel direction was not normalized to positive down.");
    Require(!secondEvents.OfType<PointerScrolled>().Any(), "Wheel event leaked into second window.");
    second.Dispose();
    size = first.PixelSize;
    pixels = new byte[checked(size.Width * size.Height * 4)];
    first.Surface.Present(pixels, size, checked(size.Width * 4));
    Require(!firstEvents.OfType<CloseRequested>().Any(), "Closing second window affected first.");

    Exception? threadError = null;
    var thread = new Thread(() =>
    {
        try
        {
            system.PumpEvents();
        }
        catch (InvalidOperationException exception)
        {
            threadError = exception;
        }
    });
    thread.Start();
    thread.Join();
    Require(threadError is not null, "Cross-thread video access was not rejected.");
    system.Dispose();
    using var reopened = new SdlWindowSystem([system.Backend]);
    using var recreated = reopened.CreateWindow(new("Reinitialize", 32, 32, Hidden: true));
    Console.WriteLine($"PASS: {system.Backend}; BGRA pixels, multi-window event routing, lifecycle, thread affinity and reinitialization.");
    Console.WriteLine($"OS fonts found: {platform.Fonts.GetFontFiles().Count}");
    return 0;
}
catch (Exception exception) when (exception is PlatformException or PlatformNotSupportedException
    or DllNotFoundException or EntryPointNotFoundException or InvalidOperationException
    or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Platform smoke failed: " + exception.Message);
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
