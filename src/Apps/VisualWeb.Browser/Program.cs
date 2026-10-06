using SDL3;
using VisualWeb.Browser;
using VisualWeb.Platform.Abstractions;
using VisualWeb.Platform.Linux;
using VisualWeb.Platform.Linux.Sandbox;
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
    Console.Error.WriteLine("WARNING: DEVELOPMENT " + (launch.RendererPath is null ? "SINGLE-PROCESS" : "MULTIPROCESS")
        + (launch.RequireSandbox ? " BROWSER. OS renderer confinement required; no origin isolation or production web security."
            : " BROWSER. No sandbox, origin isolation or production web security.") + " Use only trusted content.");
    IPlatformServices platform = OperatingSystem.IsLinux() ? new LinuxPlatformServices()
        : OperatingSystem.IsWindows() ? new WindowsPlatformServices()
        : throw new PlatformNotSupportedException("VisualWeb supports Linux and Windows.");
    using var system = launch.Backend == "dummy" ? new SdlWindowSystem(["dummy"]) : platform.OpenWindows(launch.Backend);
    if (system is SdlWindowSystem sdl)
    {
        foreach (var failure in sdl.InitializationFailures) { Console.Error.WriteLine("Backend fallback: " + failure); }
    }
    if (launch.SkipTextInput) { Console.Error.WriteLine("Smoke text input explicitly skipped; keyboard/IME support is not certified."); }
    if (launch.ExecuteInlineScripts) { Console.Error.WriteLine("WARNING: post-parse inline scripts enabled; no HTML scheduling, CSP, origin isolation or browser event loop."); }
    using var shell = new DevelopmentShell(system, launch.FontPath, hidden: launch.Smoke,
        textInput: !launch.SkipTextInput, rendererPath: launch.RendererPath, requireSandbox: launch.RequireSandbox,
        executeInlineScripts: launch.ExecuteInlineScripts);
    shell.OpenWindow(launch.Url);
    if (!launch.Smoke) { shell.Run(); return 0; }
    BrowserSmoke.Run(shell, launch.RequireSandbox, launch.ExecuteInlineScripts);
    Console.WriteLine($"PASS: {system.Backend}; native page pixels, address focus, tabs, history, multiple windows and lifecycle"
        + (launch.ExecuteInlineScripts ? "; opt-in inline script mutations reach title and pixels" : "")
        + $"; {shell.PresentedFrames} presented frames.");
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
    "Usage: dotnet run --project src/Apps/VisualWeb.Browser -- (--development-single-process | --development-multiprocess --renderer /built/renderer.dll) --font /trusted/font.ttf "
    + "[--require-sandbox] [--enable-inline-scripts] [--url ABSOLUTE_URL] [--backend x11|wayland|windows] [--smoke [--backend dummy] [--skip-text-input]]");

namespace VisualWeb.Browser
{
    public sealed record BrowserLaunchOptions(string FontPath, string? Url, string? Backend, bool Smoke, bool SkipTextInput,
        string? RendererPath = null, bool RequireSandbox = false, bool ExecuteInlineScripts = false)
    {
        public static BrowserLaunchOptions Parse(IReadOnlyList<string> args)
        {
            string? font = null, url = null, backend = null, renderer = null;
            var development = false;
            var multiprocess = false;
            var smoke = false;
            var skip = false;
            var sandbox = false;
            var scripts = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Count; index++)
            {
                var arg = args[index];
                if (!seen.Add(arg)) { throw new ArgumentException("Duplicate option: " + arg); }
                switch (arg)
                {
                    case "--development-single-process": development = true; break;
                    case "--development-multiprocess": multiprocess = true; break;
                    case "--renderer": renderer = Value(); break;
                    case "--require-sandbox": sandbox = true; break;
                    case "--enable-inline-scripts": scripts = true; break;
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
            if (development == multiprocess) { throw new ArgumentException("Choose exactly one explicit development process mode."); }
            if (multiprocess && string.IsNullOrWhiteSpace(renderer)) { throw new ArgumentException("Multiprocess mode requires an explicit built --renderer path."); }
            if (development && renderer is not null) { throw new ArgumentException("--renderer requires multiprocess mode."); }
            if (sandbox)
            {
                if (!multiprocess) { throw new ArgumentException("--require-sandbox requires explicit multiprocess mode."); }
                if (OperatingSystem.IsLinux()) { LinuxRendererResources.RequireSupport(); }
                else if (OperatingSystem.IsWindows()) { VisualWeb.Platform.Windows.Sandbox.WindowsRendererSandbox.RequireSupport(); }
                else { throw new PlatformNotSupportedException("Renderer confinement is unavailable on this platform."); }
            }
            if (string.IsNullOrWhiteSpace(font)) { throw new ArgumentException("An explicit trusted --font path is required."); }
            if (backend is not (null or "x11" or "wayland" or "windows" or "dummy")) { throw new ArgumentException("Unsupported video backend."); }
            if (!smoke && (skip || backend == "dummy")) { throw new ArgumentException("Dummy backend and text-input exclusion are restricted to smoke checks."); }
            if (smoke && url is not null) { throw new ArgumentException("Smoke checks use offline fixtures, not --url."); }
            if (url is not null) { _ = Core.Url.BrowserUrl.Parse(url); }
            return new(Path.GetFullPath(font), url, backend, smoke, skip, renderer is null ? null : Path.GetFullPath(renderer), sandbox, scripts);
        }
    }

    internal static class BrowserSmoke
    {
        internal static void Run(DevelopmentShell shell, bool requireSandbox, bool executeInlineScripts)
        {
            Wait(shell, () => shell.Controller.Session.Windows.SelectMany(w => w.Tabs).All(t => !t.IsLoading));
            var first = shell.Controller.Session.Windows.Single();
            Require(first.ActiveTab?.Error is null && shell.Controller.Page(first.ActiveTab!.Id) is not null, "Initial page failed.");
            var native = shell.Windows.Single().Native;
            var window = SDL.GetWindowFromID(native.Value);
            var warning = requireSandbox
                ? OperatingSystem.IsWindows() ? "WINDOWS APP CONTAINER REQUIRED" : "LINUX CONFINEMENT REQUIRED"
                : "NO SANDBOX";
            Require(SDL.GetWindowTitle(window).Contains(warning, StringComparison.Ordinal),
                "Native window title does not identify the active confinement mode.");
            Push(SDL.Scancode.L, SDL.Keymod.Ctrl);
            shell.Tick();
            Push(SDL.Scancode.Escape);
            Push(SDL.Scancode.T, SDL.Keymod.Ctrl);
            shell.Tick();
            Wait(shell, () => first.Tabs.All(t => !t.IsLoading));
            Require(first.Tabs.Count == 2, "New tab shortcut failed.");
            var tab = first.ActiveTab!;
            var blue = "data:text/html;charset=utf-8," + Uri.EscapeDataString(executeInlineScripts
                ? "<!doctype html><style id='sheet'>body{margin:0;background-color:red}</style><script>document.body.addEventListener('paint',()=>queueMicrotask(()=>{document.title='Script blue'; document.body.setAttribute('style','background-color:blue'); let sheet=document.createElement('style'); sheet.appendChild(document.createTextNode('body{margin:0}')); document.head.appendChild(sheet);}),{once:true}); document.body.dispatchEvent(new Event('paint'));</script>"
                : "<!doctype html><style>body{margin:0;background-color:blue}</style>");
            shell.Controller.Navigate(tab.Id, blue);
            Wait(shell, () => !tab.IsLoading);
            Require(tab.Error is null && tab.History.Entries.Count == 2, "Navigation did not commit.");
            var pixel = shell.Controller.Page(tab.Id)!.Frame.Pixels.Span;
            Require(pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255, "Page did not reach native BGRA pixels.");
            if (executeInlineScripts) { Require(tab.Title == "Script blue", "Scripted title did not commit."); }
            Require(SDL.GetWindowSizeInPixels(window, out _, out var nativeHeight), SDL.GetError());
            var surface = SDL.GetWindowSurface(window);
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
            // Let the 30-second renderer deadline report a real failure before the smoke harness times out.
            var timer = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                shell.Tick();
                if (ready()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(35));
            throw new InvalidOperationException("Browser smoke timed out.");
        }
        private static void Require(bool condition, string error)
        {
            if (!condition) { throw new InvalidOperationException(error); }
        }
    }
}
