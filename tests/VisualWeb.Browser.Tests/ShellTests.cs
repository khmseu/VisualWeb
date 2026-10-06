using System.Net;
using SDL3;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ShellTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HstsSessionStoreIsSharedAcrossTabsAndWindowsButNotIndependentShells(bool multiprocess)
    {
        var requests = new List<Uri>();
        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.False(request.Headers.Contains("Cookie"));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><title>Secure</title><style>body{margin:0}</style>",
                    System.Text.Encoding.UTF8, "text/html")
            };
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60; includeSubDomains");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "secret=value; Secure; Path=/");
            return response;
        });
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var firstWindow = shell.OpenWindow();
        Navigate(shell, firstWindow.ActiveTab!, "https://example.test/learn");
        var secondTab = shell.Controller.CreateTab(firstWindow.Id);
        Navigate(shell, secondTab, "http://example.test:80/second");
        Assert.Equal("https://example.test/second", secondTab.History.Current!.Href);
        Assert.Equal("https://example.test", secondTab.Origin!.Serialize());
        var secondWindow = shell.OpenWindow();
        Navigate(shell, secondWindow.ActiveTab!, "http://child.example.test/third");
        Assert.Equal("https", requests[^1].Scheme);
        shell.Controller.Session.MoveTab(secondTab.Id, secondWindow.Id);
        Navigate(shell, secondTab, "http://example.test/moved");
        Assert.Equal("https", requests[^1].Scheme);
        using var otherSystem = new Windows();
        using var independent = new DevelopmentShell(otherSystem, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var independentWindow = independent.OpenWindow();
        Navigate(independent, independentWindow.ActiveTab!, "http://example.test/independent");
        Assert.Equal("http", requests[^1].Scheme);

        static void Navigate(DevelopmentShell target, BrowserTab tab, string input)
        {
            target.Controller.Navigate(tab.Id, input);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                target.Tick();
                return !tab.IsLoading;
            }, TimeSpan.FromSeconds(20)), "Navigation did not finish.");
            Assert.Null(tab.Error);
        }
    }

    private sealed class HstsHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
    [Fact]
    public void FakePlatformReceivesPagePixelsAndRoutesTabsAddressAndWindowLifecycle()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var first = shell.OpenWindow();
        shell.Tick();
        Assert.NotNull(shell.Controller.Page(first.ActiveTab!.Id));
        var native = system.Items[0];
        Assert.NotNull(native.Pixels);
        Key(SDL.Scancode.T, SDL.Keymod.Ctrl);
        Assert.Equal(2, first.Tabs.Count);
        var tab = first.ActiveTab!;
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Assert.True(native.TextInput);
        var blue = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>");
        system.Events.Enqueue(new TextEntered(native.Id, blue));
        shell.Tick();
        Key(SDL.Scancode.Return);
        Assert.False(native.TextInput);
        Assert.Equal(2, tab.History.Entries.Count);
        var at = 120 * native.Size.Width * 4;
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, native.Pixels!.AsSpan(at, 4).ToArray());
        Key(SDL.Scancode.N, SDL.Keymod.Ctrl);
        Assert.Equal(2, shell.Windows.Count);
        Key(SDL.Scancode.M, SDL.Keymod.Ctrl);
        Assert.Single(first.Tabs);
        Assert.Equal(2, shell.Controller.Session.Windows.Single(w => w.Id != first.Id).Tabs.Count);
        var otherNative = system.Items[1];
        system.Events.Enqueue(new CloseRequested(otherNative.Id));
        shell.Tick();
        Assert.True(otherNative.Disposed);
        Assert.False(native.Disposed);
        Assert.Single(shell.Windows);
        Key(SDL.Scancode.W, SDL.Keymod.Ctrl);
        Assert.True(native.Disposed);
        Assert.Empty(shell.Windows);

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None)
        {
            system.Events.Enqueue(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
    }
    [Fact]
    public void CloseActiveTabRepaintsRemainingTabRatherThanLeavingStalePixels()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var first = window.ActiveTab!;
        var other = shell.Controller.CreateTab(window.Id);
        shell.Controller.Navigate(other.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>"));
        shell.Tick();
        var count = shell.PresentedFrames;
        system.Events.Enqueue(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.W, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        shell.Tick();
        Assert.Equal(first.Id, window.ActiveTabId);
        Assert.True(shell.PresentedFrames > count);
        Assert.NotEqual((byte)255, system.Items[0].Pixels![120 * system.Items[0].Size.Width * 4]);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    [InlineData(1.3)]
    public void ChromeAndPageCompositionMatchesPhysicalSurfaceAtFractionalDensity(double density)
    {
        using var chrome = new ShellChrome(FontPath, 500_000);
        var session = new BrowserSession();
        var window = session.CreateWindow(); session.CreateTab(window.Id);
        var size = new PixelSize(641, 401);
        var viewport = ShellChrome.Viewport(size, density)!.Value;
        using var fonts = new VisualWeb.Engine.Paint.PaintFontRegistry();
        var page = new BrowserPage(VisualWeb.Engine.Paint.CpuRasterizer.Render(
            new(viewport.Width, viewport.Height, []), fonts, viewport.Scale, cancellationToken: TestContext.Current.CancellationToken), "Title", "Ready");
        var frame = chrome.Render(window, page, size, density);
        Assert.Equal(size, frame.Size);
        Assert.Equal(641 * 4, frame.Stride);
        Assert.Equal(641 * 401 * 4, frame.Pixels.Length);
        var header = (int)Math.Ceiling(120 * density);
        Assert.All(frame.Pixels.AsSpan(header * frame.Stride).ToArray(), value => Assert.Equal(255, value));
        Assert.Null(ShellChrome.Viewport(new(641, header), density));
    }
    [Fact]
    public void ChromeHitTestingSeparatesAddressAndControlsAndSanitizesPageTitle()
    {
        using var chrome = new ShellChrome(FontPath, 500_000);
        var session = new BrowserSession();
        var window = session.CreateWindow(); var tab = session.CreateTab(window.Id);
        var editor = new AddressEditor();
        editor.Reset("https://example.com/\u05d0", selectAll: true);
        var frame = chrome.Render(window, null, new(641, 401), 1, editor);
        Assert.Equal(ChromeAction.NewTab, ShellChrome.Hit(frame.Targets, 100, 70)!.Action);
        Assert.Equal(ChromeAction.Address, ShellChrome.Hit(frame.Targets, 220, 70)!.Action);
        Assert.Null(ShellChrome.Hit(frame.Targets, 100, 200));
        Assert.DoesNotContain(frame.Targets, t => t.Action == ChromeAction.Back);
        Assert.Equal(tab.Id, ShellChrome.Hit(frame.Targets, 80, 40)!.Tab);
        Assert.Equal(ChromeAction.CloseTab, ShellChrome.Hit(frame.Targets, 170, 40)!.Action);
    }
    [Fact]
    public void MoveLastTabCreatesWindowWithoutAnUnwantedHomeTabEvenAtTabLimit()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, new() { MaxTabs = 1 });
        var original = shell.OpenWindow();
        shell.Tick();
        var tab = original.ActiveTab!;
        system.Events.Enqueue(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.M, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        shell.Tick();
        var window = Assert.Single(shell.Controller.Session.Windows);
        Assert.NotEqual(original.Id, window.Id);
        Assert.Same(tab, Assert.Single(window.Tabs));
        Assert.True(system.Items[0].Disposed);
        Assert.False(system.Items[1].Disposed);
        Assert.Null(tab.Error);
    }
    [Fact]
    public void TabCloseButtonAndFocusedAddressCaretUseActualEventTargets()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 100, 70));
        shell.Tick();
        Assert.Equal(2, window.Tabs.Count);
        var active = window.ActiveTab!;
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 220, 70));
        system.Events.Enqueue(new TextEntered(native.Id, "data:text/html,a"));
        shell.Tick();
        Assert.True(native.TextInput);
        Assert.Equal("data:text/html,a", active.AddressText);
        system.Events.Enqueue(new KeyChanged(native.Id, (int)SDL.Scancode.Left, 0, 0, true, false));
        system.Events.Enqueue(new TextEntered(native.Id, "b"));
        shell.Tick();
        Assert.Equal("data:text/html,ba", active.AddressText);
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 290, 40));
        shell.Tick();
        Assert.Single(window.Tabs);
        Assert.False(shell.Controller.Session.Contains(active.Id));
        Assert.False(native.TextInput);
    }
    [Fact]
    public void OversizeWindowDoesNotTerminateOtherWindowsAndRecoversOnResize()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var first = shell.OpenWindow(); shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        native.Size = new(4096, 4096);
        shell.Tick();
        Assert.Contains("framebuffer limit", native.Title);
        Assert.False(system.Items[1].Disposed);
        native.Size = new(1000, 720);
        shell.Tick();
        Assert.DoesNotContain("framebuffer limit", native.Title);
        Assert.NotNull(shell.Controller.Page(first.ActiveTab!.Id));
    }
    [Fact]
    public void EveryMultiprocessShellModeEnablesOriginIsolatedNavigationWithoutOptOut()
    {
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        using (var system = new Windows())
        using (var single = new DevelopmentShell(system, FontPath)) { Assert.False(single.Controller.IsolatesOrigins); }
        using (var system = new Windows())
        using (var confined = new DevelopmentShell(system, FontPath, rendererPath: rendererPath, requireSandbox: true))
        {
            Assert.True(confined.Controller.IsolatesOrigins);
        }
        using var unsandboxedSystem = new Windows();
        using var shell = new DevelopmentShell(unsandboxedSystem, FontPath, rendererPath: rendererPath);
        Assert.True(shell.Controller.IsolatesOrigins);
        var tab = shell.OpenWindow().ActiveTab!;
        Wait();
        Assert.Null(tab.Error);
        var home = tab.Origin;
        Assert.NotNull(home);
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>"));
        Wait();
        Assert.Null(tab.Error);
        Assert.False(home!.IsSameOrigin(tab.Origin));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, unsandboxedSystem.Items[0].Pixels!.AsSpan(120 * unsandboxedSystem.Items[0].Size.Width * 4, 4).ToArray());

        void Wait()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                shell.Tick();
                if (!tab.IsLoading) { return; }
                Thread.Sleep(5);
            } while (timer.Elapsed < TimeSpan.FromSeconds(30));
            Assert.Fail("Multiprocess shell navigation timed out.");
        }
    }
    private sealed class Windows : IWindowSystem
    {
        internal List<Window> Items { get; } = [];
        internal Queue<WindowEvent> Events { get; } = [];
        public string Backend => "fake";
        public event Action? QuitRequested;
        internal void Quit() => QuitRequested?.Invoke();
        public IPlatformWindow CreateWindow(WindowOptions options)
        {
            var window = new Window(new((uint)Items.Count + 1), new(options.Width, options.Height));
            Items.Add(window); return window;
        }
        public void PumpEvents()
        {
            while (Events.TryDequeue(out var input)) { Items.Single(w => w.Id == input.Window).Dispatch(input); }
        }
        public void Dispose() { foreach (var window in Items) { window.Dispose(); } QuitRequested = null; }
    }
    private sealed class Window(WindowId id, PixelSize size) : IPlatformWindow, IPixelSurface
    {
        public WindowId Id => id;
        internal PixelSize Size { get; set; } = size;
        public PixelSize LogicalSize => Size;
        public PixelSize PixelSize => Size;
        public float DisplayScale => 1;
        public float PixelDensity => 1;
        public IPixelSurface Surface => this;
        public event Action<WindowEvent>? EventReceived;
        internal bool TextInput { get; private set; }
        internal bool Disposed { get; private set; }
        internal byte[]? Pixels { get; private set; }
        internal string Title { get; private set; } = "";
        internal void Dispatch(WindowEvent input) => EventReceived?.Invoke(input);
        public void SetTitle(string title) => Title = title;
        public void SetTextInput(bool enabled) => TextInput = enabled;
        public void Present(ReadOnlySpan<byte> pixels, PixelSize frameSize, int stride)
        {
            Assert.Equal(Size, frameSize);
            PixelBuffer.Validate(pixels.Length, frameSize, stride);
            Pixels = pixels.ToArray();
        }
        public void Dispose() { Disposed = true; EventReceived = null; }
    }
}
