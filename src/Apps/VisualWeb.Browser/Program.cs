using SDL3;
using VisualWeb.Browser;
using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Linux;
using VisualWeb.Platform.Sdl;
using VisualWeb.Platform.Windows;

if (args is ["--help"])
{
    Usage();
    return 0;
}
try
{
    var launch = BrowserLaunchOptions.Parse(args);
    Console.Error.WriteLine("WARNING: DEVELOPMENT SINGLE-PROCESS BROWSER. No sandbox, origin isolation or production web security. Use only trusted content.");
    IPlatformServices platform = OperatingSystem.IsLinux() ? new LinuxPlatformServices()
        : OperatingSystem.IsWindows() ? new WindowsPlatformServices()
        : throw new PlatformNotSupportedException("VisualWeb supports Linux and Windows.");
    using var system = launch.Backend == "dummy" ? new SdlWindowSystem(["dummy"]) : platform.OpenWindows(launch.Backend);
    if (system is SdlWindowSystem sdl)
    {
        foreach (var failure in sdl.InitializationFailures) { Console.Error.WriteLine("Backend fallback: " + failure); }
    }
    if (launch.SkipTextInput) { Console.Error.WriteLine("Smoke text input explicitly skipped; keyboard/IME support is not certified."); }
    using var shell = new DevelopmentShell(system, launch.FontPath, hidden: launch.Smoke, textInput: !launch.SkipTextInput);
    shell.OpenWindow(launch.Url);
    if (!launch.Smoke) { shell.Run(); return 0; }
    BrowserSmoke.Run(shell);
    Console.WriteLine($"PASS: {system.Backend}; native page pixels, address focus, tabs, history, multiple windows and lifecycle; {shell.PresentedFrames} presented frames.");
    return 0;
}
catch (Exception exception) when (BrowserController.IsPageFailure(exception) || exception is ArgumentException
    or PlatformException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException
    or InvalidOperationException)
{
    Console.Error.WriteLine("VisualWeb failed: " + exception.Message);
    Usage();
    return 1;
}

static void Usage() => Console.Error.WriteLine(
    "Usage: dotnet run --project src/Apps/VisualWeb.Browser -- --development-single-process --font /trusted/font.ttf "
    + "[--url ABSOLUTE_URL] [--backend x11|wayland|windows] [--smoke [--backend dummy] [--skip-text-input]]");

namespace VisualWeb.Browser
{
    public sealed record BrowserLaunchOptions(string FontPath, string? Url, string? Backend, bool Smoke, bool SkipTextInput)
    {
        public static BrowserLaunchOptions Parse(IReadOnlyList<string> args)
        {
            string? font = null, url = null, backend = null;
            var development = false;
            var smoke = false;
            var skip = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Count; index++)
            {
                var arg = args[index];
                if (!seen.Add(arg)) { throw new ArgumentException("Duplicate option: " + arg); }
                switch (arg)
                {
                    case "--development-single-process": development = true; break;
                    case "--smoke": smoke = true; break;
                    case "--skip-text-input": skip = true; break;
                    case "--font": font = Value(); break;
                    case "--url": url = Value(); break;
                    case "--backend": backend = Value(); break;
                    default: throw new ArgumentException("Unknown option: " + arg);
                }
                string Value()
                {
                    if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Missing value for " + arg);
                    }
                    return args[index];
                }
            }
            if (!development) { throw new ArgumentException("Explicit --development-single-process acknowledgement is required."); }
            if (string.IsNullOrWhiteSpace(font)) { throw new ArgumentException("An explicit trusted --font path is required."); }
            if (backend is not (null or "x11" or "wayland" or "windows" or "dummy")) { throw new ArgumentException("Unsupported video backend."); }
            if (!smoke && (skip || backend == "dummy")) { throw new ArgumentException("Dummy backend and text-input exclusion are restricted to smoke checks."); }
            if (smoke && url is not null) { throw new ArgumentException("Smoke checks use offline fixtures, not --url."); }
            if (url is not null) { _ = Core.Url.BrowserUrl.Parse(url); }
            return new(Path.GetFullPath(font), url, backend, smoke, skip);
        }
    }

    internal static class BrowserSmoke
    {
        internal static void Run(DevelopmentShell shell)
        {
            Wait(shell, () => shell.Controller.Session.Windows.SelectMany(w => w.Tabs).All(t => !t.IsLoading));
            var first = shell.Controller.Session.Windows.Single();
            Require(first.ActiveTab?.Error is null && shell.Controller.Page(first.ActiveTab!.Id) is not null, "Initial page failed.");
            var native = shell.Windows.Single().Native;
            Push(SDL.Scancode.L, SDL.Keymod.Ctrl);
            shell.Tick();
            Push(SDL.Scancode.Escape);
            Push(SDL.Scancode.T, SDL.Keymod.Ctrl);
            shell.Tick();
            Wait(shell, () => first.Tabs.All(t => !t.IsLoading));
            Require(first.Tabs.Count == 2, "New tab shortcut failed.");
            var tab = first.ActiveTab!;
            var blue = "data:text/html;charset=utf-8," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>");
            shell.Controller.Navigate(tab.Id, blue);
            Wait(shell, () => !tab.IsLoading);
            Require(tab.Error is null && tab.History.Entries.Count == 2, "Navigation did not commit.");
            var pixel = shell.Controller.Page(tab.Id)!.Frame.Pixels.Span;
            Require(pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255, "Page did not reach native BGRA pixels.");
            var handle = SDL.GetWindowFromID(native.Value);
            Require(SDL.GetWindowSizeInPixels(handle, out _, out var nativeHeight), SDL.GetError());
            var surface = SDL.GetWindowSurface(handle);
            Require(SDL.ReadSurfacePixel(surface, 0, nativeHeight - 1, out var red, out var green, out var nativeBlue, out _), SDL.GetError());
            Require(red == 0 && green == 0 && nativeBlue == 255, "Composed page pixels did not reach the SDL surface.");
            shell.Controller.Back(tab.Id);
            Wait(shell, () => !tab.IsLoading);
            Require(tab.History.Index == 0, "Back traversal failed.");
            shell.Controller.Forward(tab.Id);
            Wait(shell, () => !tab.IsLoading);
            Require(tab.History.Index == 1, "Forward traversal failed.");
            Push(SDL.Scancode.N, SDL.Keymod.Ctrl);
            shell.Tick();
            Require(shell.Windows.Count == 2, "New window shortcut failed.");
            Push(SDL.Scancode.M, SDL.Keymod.Ctrl);
            shell.Tick();
            var other = shell.Controller.Session.Windows.Single(w => w.Id != first.Id);
            Require(other.Tabs.Any(t => t.Id == tab.Id) && !first.Tabs.Any(t => t.Id == tab.Id), "Move tab failed.");
            shell.Controller.CloseWindow(other.Id);
            shell.Tick();
            Require(shell.Windows.Count == 1 && shell.Controller.Session.Contains(first.ActiveTab!.Id), "Closing another window affected the first.");
            Require(shell.PresentedFrames >= 3, "Frames were not presented.");

            void Push(SDL.Scancode scan, SDL.Keymod modifier = SDL.Keymod.None)
            {
                var input = new SDL.Event
                {
                    Key = new SDL.KeyboardEvent
                    {
                        Type = SDL.EventType.KeyDown,
                        WindowID = native.Value,
                        Scancode = scan,
                        Mod = modifier
                    }
                };
                Require(SDL.PushEvent(ref input), SDL.GetError());
            }
        }
        private static void Wait(DevelopmentShell shell, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            do
            {
                shell.Tick();
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (DateTime.UtcNow < deadline);
            throw new InvalidOperationException("Browser smoke timed out.");
        }
        private static void Require(bool condition, string error)
        {
            if (!condition) { throw new InvalidOperationException(error); }
        }
    }
}
