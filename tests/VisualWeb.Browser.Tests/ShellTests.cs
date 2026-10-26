using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using SDL3;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ShellTests
{
    [Fact]
    public void OptedInPrimaryPointerLinkDispatchesClickBeforeDefaultNavigation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, executeInlineScripts: true,
            pageTransportFactory: () => new NavigationHandler(request =>
            {
                var url = request.RequestUri!.AbsoluteUri;
                requests.Enqueue(url);
                return Html("<!doctype html><title>Start</title><style>*{margin:0}</style>"
                    + "<a href='/next'>open</a><script>document.addEventListener('click',e=>{document.title='Clicked';e.preventDefault()})</script>");
            }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        Wait(() => !tab.IsLoading && tab.Title == "Clicked");

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void OptedInEnterLinkActivationDispatchesCancelableClickBeforeNavigation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, executeInlineScripts: true,
            pageTransportFactory: () => new NavigationHandler(request =>
            {
                var url = request.RequestUri!.AbsoluteUri;
                requests.Enqueue(url);
                return Html("<!doctype html><title>Start</title><style>*{margin:0}</style>"
                    + "<a href='/next' target='_self'>open</a><script>document.addEventListener('click',e=>{document.title='Clicked';e.preventDefault()})</script>");
            }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Controller.FocusPage(tab.Id);
        shell.Controller.FocusLink(tab.Id, 0);

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.Title == "Clicked");

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptedInCtrlPrimaryLinkDispatchesCancelableClickBeforeBackgroundTab(bool cancelDefault)
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, executeInlineScripts: true,
            pageTransportFactory: () => new NavigationHandler(request =>
            {
                var url = request.RequestUri!.AbsoluteUri;
                requests.Enqueue(url);
                return Html("<!doctype html><title>Start</title><style>*{margin:0}</style>"
                    + "<a href='/next'>open</a><script>document.addEventListener('click',e=>{document.title='Clicked';"
                    + (cancelDefault ? "e.preventDefault()" : "") + "})</script>");
            }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var tab = window.ActiveTab!;
        var native = system.Items[0];
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.LCtrl, 0, (ushort)SDL.Keymod.Ctrl, true, false));

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));

        if (cancelDefault) { Wait(() => tab.Title == "Clicked"); }
        else { Wait(() => requests.Count == 2); }
        Assert.Equal("https://source.example/page", requests.First());
        Assert.Equal(cancelDefault ? 1 : 2, requests.Count);
        Assert.Equal(tab.Id, window.ActiveTabId);
        Assert.Equal(cancelDefault ? 1 : 2, window.Tabs.Count);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void CrossOriginLinkAndRedirectWaitForShellConfirmation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url switch
            {
                "https://source.example/page" => Html("<!doctype html><style>*{margin:0}</style>"
                    + "<a href='https://outside.example/start?token=private'>open</a>"),
                "https://outside.example/start?token=private" => Redirect("https://source.example/return"),
                "https://source.example/return" => Redirect("https://third.example/final"),
                _ => Html("<!doctype html><title>Final</title>")
            };
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        shell.Tick();

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.AddressText);
        ApproveUntilRequestCount(2);
        Wait(() => requests.Count == 2);
        shell.Tick();
        Assert.Equal(["https://source.example/page", "https://outside.example/start?token=private"], requests);
        ApproveUntilRequestCount(3);
        Assert.Equal("https://source.example/page", tab.AddressText);
        ApproveUntilRequestCount(4);
        Wait(() => !tab.IsLoading);

        Assert.Equal([
            "https://source.example/page",
            "https://outside.example/start?token=private",
            "https://source.example/return",
            "https://third.example/final"
        ], requests);
        Assert.Equal("https://third.example/final", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void ApproveUntilRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                if (!shell.HasPendingNavigationConfirmation(tab.Id))
                {
                    Thread.Sleep(5);
                    continue;
                }
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    [Fact]
    public void FileAddressBarNavigationWaitsForConfirmationAndShowsPathWithoutQueryOrFragment()
    {
        var path = Path.Combine(Path.GetTempPath(), "visualweb-" + Guid.NewGuid().ToString("N") + ".html");
        var fileTarget = new Uri(path).AbsoluteUri;
        var address = fileTarget + "?private=value#section";
        try
        {
            using var system = new Windows();
            using var shell = new DevelopmentShell(system, FontPath);
            var window = shell.OpenWindow(empty: true);
            var tab = shell.Controller.CreateTab(window.Id);
            var native = system.Items[0];

            TypeAddress(address);
            Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

            Assert.False(File.Exists(path));
            Assert.Equal("Allow local file navigation to " + fileTarget,
                shell.PendingNavigationConfirmationMessage(tab.Id));
            Assert.Null(tab.History.Current);

            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
            Wait(() => !tab.IsLoading);
            Assert.Null(shell.Controller.Page(tab.Id));
            Assert.Null(tab.History.Current);
            Assert.Contains("denied", tab.Error);

            TypeAddress(address);
            Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
            Assert.Equal("Allow local file navigation to " + fileTarget,
                shell.PendingNavigationConfirmationMessage(tab.Id));
            File.WriteAllText(path, "<!doctype html><title>Local file</title>");
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

            Assert.Equal("Local file", tab.Title);
            Assert.NotNull(tab.History.Current);
            Assert.Null(tab.Error);

            void TypeAddress(string value)
            {
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
                shell.Dispatch(new TextEntered(native.Id, value));
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
            }
            void Wait(Func<bool> condition)
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
                Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
            }
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void ShellLaunchAndFileHistoryLoadsRequireConfirmation()
    {
        var path = Path.Combine(Path.GetTempPath(), "visualweb-" + Guid.NewGuid().ToString("N") + ".html");
        var fileUrl = new Uri(path).AbsoluteUri;
        File.WriteAllText(path, "<!doctype html><title>Local file</title>");
        try
        {
            using var system = new Windows();
            using var shell = new DevelopmentShell(system, FontPath);
            var window = shell.OpenWindow(fileUrl);
            var tab = window.ActiveTab!;
            var native = system.Items[0];

            WaitForPrompt("Allow local file navigation to " + fileUrl);
            ApproveAndWait(fileUrl);

            const string firstData = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3Efirst%3C/title%3E";
            const string secondData = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3Esecond%3C/title%3E";
            Navigate(firstData);
            Navigate(fileUrl);
            Navigate(secondData);

            shell.Controller.BackWithShellConfirmation(tab.Id);
            WaitForPrompt("Allow local file navigation to " + fileUrl);
            Assert.Equal(secondData, tab.History.Current!.Href);
            ApproveAndWait(fileUrl);

            shell.Controller.BackWithShellConfirmation(tab.Id);
            WaitForPrompt("Allow data URL navigation");
            Assert.Equal(fileUrl, tab.History.Current!.Href);
            ApproveAndWait(firstData);

            shell.Controller.ForwardWithShellConfirmation(tab.Id);
            WaitForPrompt("Allow local file navigation to " + fileUrl);
            ApproveAndWait(fileUrl);

            shell.Controller.ReloadWithShellConfirmation(tab.Id);
            WaitForPrompt("Allow local file navigation to " + fileUrl);
            ApproveAndWait(fileUrl);

            void Navigate(string url)
            {
                shell.Controller.NavigateWithShellConfirmation(tab.Id, url);
                if (url == fileUrl)
                {
                    WaitForPrompt("Allow local file navigation to " + fileUrl);
                    ApproveAndWait(fileUrl);
                }
                else if (url.StartsWith("data:", StringComparison.Ordinal))
                {
                    WaitForPrompt("Allow data URL navigation");
                    ApproveAndWait(url);
                }
                else { Wait(() => !tab.IsLoading && tab.History.Current?.Href == url); }
            }
            void WaitForPrompt(string expectedMessage)
            {
                Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
                Assert.Equal(expectedMessage, shell.PendingNavigationConfirmationMessage(tab.Id));
            }
            void ApproveAndWait(string expected)
            {
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                Wait(() => !tab.IsLoading && tab.History.Current?.Href == expected);
            }
            void Wait(Func<bool> condition)
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
                Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
            }
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void ExplicitDataAddressBarNavigationWaitsForConfirmationAndHidesPayload()
    {
        const string address = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3EAddress%3C/title%3E";
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow(empty: true);
        var tab = shell.Controller.CreateTab(window.Id);
        var native = system.Items[0];

        TypeAddress(address);
        WaitForPrompt();
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        Assert.Null(tab.History.Current);
        Assert.Null(shell.Controller.Page(tab.Id));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        Wait(() => !tab.IsLoading);
        Assert.Null(tab.History.Current);
        Assert.Null(shell.Controller.Page(tab.Id));
        Assert.Contains("Data URL navigation was denied", tab.Error);

        TypeAddress(address);
        WaitForPrompt();
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == address
            && shell.Controller.Page(tab.Id) is not null);
        Assert.Equal("Address", tab.Title);
        Assert.Null(tab.Error);

        const string lowLevelAddress = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3ELowLevel%3C/title%3E";
        shell.Controller.Navigate(tab.Id, lowLevelAddress);
        Assert.False(shell.HasPendingNavigationConfirmation(tab.Id));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == lowLevelAddress
            && shell.Controller.Page(tab.Id) is not null);
        Assert.Equal("LowLevel", tab.Title);

        void TypeAddress(string value)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, value));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void WaitForPrompt()
        {
            Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void DataLaunchAndCrossDocumentHistoryWaitForConfirmationButWelcomeDoesNot()
    {
        const string first = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3EFirst%3C/title%3E";
        const string second = "data:text/html,%3C!doctype%20html%3E%3Ctitle%3ESecond%3C/title%3E";
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);

        var welcome = shell.OpenWindow();
        var welcomeTab = welcome.ActiveTab!;
        WaitFor(welcomeTab, () => !welcomeTab.IsLoading
            && welcomeTab.History.Current?.Href == DevelopmentShell.HomeUrl);
        Assert.False(shell.HasPendingNavigationConfirmation(welcomeTab.Id));

        shell.Controller.NavigateWithShellConfirmation(welcomeTab.Id, first);
        WaitForPrompt(welcomeTab);
        Approve(welcomeTab, system.Items[0].Id, first);
        shell.Controller.BackWithShellConfirmation(welcomeTab.Id);
        WaitFor(welcomeTab, () => !welcomeTab.IsLoading
            && welcomeTab.History.Current?.Href == DevelopmentShell.HomeUrl);
        Assert.False(shell.HasPendingNavigationConfirmation(welcomeTab.Id));

        var launchWindow = shell.OpenWindow(first);
        var tab = launchWindow.ActiveTab!;
        var native = system.Items[1].Id;
        WaitForPrompt(tab);
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        Assert.Null(tab.History.Current);
        Approve(tab, native, first);

        shell.Controller.NavigateWithShellConfirmation(tab.Id, second);
        WaitForPrompt(tab);
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        Approve(tab, native, second);

        shell.Controller.BackWithShellConfirmation(tab.Id);
        WaitForPrompt(tab);
        Assert.Equal(second, tab.History.Current!.Href);
        Approve(tab, native, first);

        shell.Controller.ForwardWithShellConfirmation(tab.Id);
        WaitForPrompt(tab);
        Approve(tab, native, second);

        shell.Controller.ReloadWithShellConfirmation(tab.Id);
        WaitForPrompt(tab);
        Approve(tab, native, second);

        void WaitForPrompt(BrowserTab target)
        {
            WaitFor(target, () => shell.HasPendingNavigationConfirmation(target.Id));
        }
        void Approve(BrowserTab target, WindowId windowId, string expected)
        {
            shell.Dispatch(new KeyChanged(windowId, (int)SDL.Scancode.Return, 0, 0, true, false));
            WaitFor(target, () => !target.IsLoading && target.History.Current?.Href == expected
                && shell.Controller.Page(target.Id) is not null);
        }
        void WaitFor(BrowserTab target, Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{target.Error} | {target.Status} | {target.AddressText}");
        }
    }

    [Fact]
    public void CrossOriginAddressBarNavigationWaitsForShellConfirmation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page"
                ? Html("<!doctype html><title>Source</title>")
                : Html("<!doctype html><title>Destination</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        TypeAddress("https://outside.example/destination");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.Equal("https://outside.example/destination", tab.AddressText);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Equal(["https://source.example/page", "https://outside.example/destination"], requests);
        Assert.Equal("https://outside.example/destination", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void BackAndForwardConfirmCrossOriginHistoryDestinations()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return Html("<!doctype html><title>" + (url.Contains("outside.example", StringComparison.Ordinal)
                ? "Outside" : "Source") + "</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        TypeAddress("https://outside.example/page");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
        ApprovePendingConfirmation();
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://outside.example/page");

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Left, 0, (ushort)SDL.Keymod.Alt, true, false));
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Equal("https://outside.example/page", tab.History.Current!.Href);
        Assert.Equal("https://outside.example/page", tab.AddressText);
        Assert.Equal(2, requests.Count);
        ApprovePendingConfirmation();
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://source.example/page");

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Right, 0, (ushort)SDL.Keymod.Alt, true, false));
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.Equal("https://source.example/page", tab.AddressText);
        Assert.Equal(3, requests.Count);
        ApprovePendingConfirmation();
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://outside.example/page");

        Assert.Equal([
            "https://source.example/page",
            "https://outside.example/page",
            "https://source.example/page",
            "https://outside.example/page"
        ], requests);
        Assert.Null(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void ApprovePendingConfirmation()
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void ReloadConfirmsCrossOriginRedirectBeforeFollowingIt()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page" && requests.Count > 1
                ? Redirect("https://outside.example/final")
                : Html("<!doctype html><title>Page</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.F5, 0, 0, true, false));
        Wait(() => requests.Count == 2 && shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Equal(["https://source.example/page", "https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.Equal("https://source.example/page", tab.AddressText);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://outside.example/final");

        Assert.Equal([
            "https://source.example/page",
            "https://source.example/page",
            "https://outside.example/final"
        ], requests);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void ReloadBlocksHttpsDowngradeRedirect()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page" && requests.Count > 1
                ? Redirect("http://source.example/insecure")
                : Html("<!doctype html><title>Secure</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.F5, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Equal(["https://source.example/page", "https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.False(shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Contains("Secure transport", tab.Error, StringComparison.OrdinalIgnoreCase);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void BackFromHttpsBlocksHttpHistoryTarget()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return Html("<!doctype html><title>Page</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "http://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        TypeAddress("https://secure.example/page");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://secure.example/page");

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Left, 0, (ushort)SDL.Keymod.Alt, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Equal(["http://source.example/page", "https://secure.example/page"], requests);
        Assert.Equal("https://secure.example/page", tab.History.Current!.Href);
        Assert.False(shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Contains("Secure transport", tab.Error, StringComparison.OrdinalIgnoreCase);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void AddressBarNavigationConfirmsCrossOriginRedirectsFromSameOriginDestinations()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url switch
            {
                "https://source.example/page" => Html("<!doctype html><title>Source</title>"),
                "https://source.example/redirect" => Redirect("https://outside.example/final"),
                _ => Html("<!doctype html><title>Destination</title>")
            };
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        TypeAddress("https://source.example/redirect");
        Wait(() => requests.Count == 2 && shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Equal(["https://source.example/page", "https://source.example/redirect"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Equal([
            "https://source.example/page",
            "https://source.example/redirect",
            "https://outside.example/final"
        ], requests);
        Assert.Equal("https://outside.example/final", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void CancelingCrossOriginAddressBarNavigationPreventsItsRequest()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return Html("<!doctype html><title>Source</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        TypeAddress("https://outside.example/denied");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.NotNull(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void InitialAddressBarNavigationWaitsForConfirmationAndEachCrossOriginRedirect()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://first.example/start"
                ? Redirect("https://outside.example/final")
                : Html("<!doctype html><title>Destination</title>");
        }));
        var window = shell.OpenWindow(empty: true);
        var tab = shell.Controller.CreateTab(window.Id);
        var native = system.Items[0];

        TypeAddress("https://first.example/start");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Empty(requests);
        Assert.Null(shell.Controller.Page(tab.Id));
        Assert.Null(tab.History.Current);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => requests.Count == 1 && shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Equal(["https://first.example/start"], requests);
        Assert.Null(tab.History.Current);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://outside.example/final");

        Assert.Equal(["https://first.example/start", "https://outside.example/final"], requests);
        Assert.Equal("https://outside.example/final", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void CancelingInitialAddressBarConfirmationPreventsItsRequest()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            requests.Enqueue(request.RequestUri!.AbsoluteUri);
            return Html("<!doctype html><title>Unexpected</title>");
        }));
        var window = shell.OpenWindow(empty: true);
        var tab = shell.Controller.CreateTab(window.Id);
        var native = system.Items[0];

        TypeAddress("https://first.example/denied");
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Empty(requests);
        Assert.Null(shell.Controller.Page(tab.Id));
        Assert.Null(tab.History.Current);
        Assert.NotNull(tab.Error);

        void TypeAddress(string address)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
            shell.Dispatch(new TextEntered(native.Id, address));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void AutomaticallyStartedHttpAddressWaitsForConfirmationBeforeTransport()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://launch.example/start"
                ? Redirect("https://outside.example/final")
                : Html("<!doctype html><title>Destination</title>");
        }));

        var window = shell.OpenWindow("https://launch.example/start");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        Assert.Empty(requests);
        Assert.Null(shell.Controller.Page(tab.Id));
        Assert.Null(tab.History.Current);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => requests.Count == 1 && shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Equal(["https://launch.example/start"], requests);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://outside.example/final");

        Assert.Equal(["https://launch.example/start", "https://outside.example/final"], requests);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void CancelingAutomaticallyStartedHttpAddressPreventsItsRequest()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            requests.Enqueue(request.RequestUri!.AbsoluteUri);
            return Html("<!doctype html><title>Unexpected</title>");
        }));

        var window = shell.OpenWindow("https://launch.example/cancel");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => shell.HasPendingNavigationConfirmation(tab.Id));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        Wait(() => !tab.IsLoading);

        Assert.Empty(requests);
        Assert.Null(tab.History.Current);
        Assert.Null(shell.Controller.Page(tab.Id));
        Assert.NotNull(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url switch
            {
                "http://source.example/page" => HtmlWithPolicy(
                    "<!doctype html><a href='http://source.example:80/first?query=1#section'>open</a>"),
                "https://source.example/first?query=1" or "http://source.example:80/first?query=1" =>
                    Redirect("http://source.example:80/redirect?next=2"),
                "https://source.example/redirect?next=2" or "http://source.example:80/redirect?next=2" =>
                    Html("<!doctype html><title>Final</title>"),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            };
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "http://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        shell.Controller.FocusPage(tab.Id);
        shell.Controller.FocusLink(tab.Id, 0);

        Assert.True(shell.Controller.ActivateFocusedLink(tab.Id));
        Assert.Equal(["http://source.example/page"], requests);
        ApproveUntilRequestCount(2);
        Wait(() => !tab.IsLoading && tab.History.Current?.Href == "https://source.example/redirect?next=2#section");

        Assert.Equal([
            "http://source.example/page",
            "https://source.example/first?query=1",
            "https://source.example/redirect?next=2"
        ], requests);
        Assert.Null(tab.Error);

        void ApproveUntilRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText} | {string.Join(", ", requests)}");
        }
        HttpResponseMessage HtmlWithPolicy(string html)
        {
            var response = Html(html);
            response.Headers.Add("Content-Security-Policy", "upgrade-insecure-requests");
            return response;
        }
    }

    [Fact]
    public void CancelingCrossOriginNavigationPreventsItsRequest()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page"
                ? Html("<!doctype html><style>*{margin:0}</style>"
                    + "<a href='https://outside.example/secret'>open</a>")
                : Html("<!doctype html><title>External</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        shell.Tick();

        Assert.Equal(["https://source.example/page"], requests);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        shell.Tick();
        Wait(() => !tab.IsLoading);

        Assert.Equal(["https://source.example/page"], requests);
        Assert.Equal("https://source.example/page", tab.History.Current!.Href);
        Assert.NotNull(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    [Fact]
    public void CrossOriginLinkOpenedInNewTabUsesTheSourceDocumentForConfirmation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page"
                ? Html("<!doctype html><style>*{margin:0}</style>"
                    + "<a target=_blank href='https://outside.example/page?private=yes'>open</a>")
                : Html("<!doctype html><title>External</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var sourceTab = window.ActiveTab!;
        Wait(() => !sourceTab.IsLoading && shell.Controller.Page(sourceTab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(sourceTab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        shell.Tick();

        var destinationTab = window.ActiveTab ?? throw new InvalidOperationException("The target tab was not activated.");
        Assert.NotEqual(sourceTab.Id, destinationTab.Id);
        Assert.Empty(destinationTab.AddressText);
        Assert.Equal(["https://source.example/page"], requests);
        ApproveUntilRequestCount(2);
        Wait(() => !destinationTab.IsLoading);

        Assert.Equal("https://outside.example/page?private=yes", destinationTab.History.Current!.Href);
        Assert.Null(destinationTab.Error);
        Assert.Equal(["https://source.example/page", "https://outside.example/page?private=yes"], requests);

        void ApproveUntilRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition());
        }
    }

    [Fact]
    public void CrossOriginFormWaitsForConfirmationWithoutRevealingQueryInAddressBar()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            return url == "https://source.example/page"
                ? Html("<!doctype html><style>*{margin:0}input{display:block;height:20px}</style>"
                    + "<form action='https://outside.example/send'><input name=secret value=private><input type=submit></form>")
                : Html("<!doctype html><title>Sent</title>");
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Controller.FocusPage(tab.Id);
        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Assert.True(shell.Controller.ActivateFocusedLink(tab.Id));
        shell.Tick();

        Assert.Equal("https://source.example/page", tab.AddressText);
        Assert.Equal(["https://source.example/page"], requests);
        ApproveUntilRequestCount(2);
        Wait(() => !tab.IsLoading);

        Assert.Equal("https://outside.example/send?secret=private", requests.ToArray()[1]);
        Assert.Equal("https://outside.example/send?secret=private", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void ApproveUntilRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    [Fact]
    public void UpgradeInsecureRequestsUpgradesGetFormActionAndRedirectBeforeConfirmation()
    {
        var requests = new ConcurrentQueue<string>();
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: () => new NavigationHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Enqueue(url);
            if (url == "https://source.example/page")
            {
                var response = Html("<!doctype html><style>*{margin:0}input{display:block;height:20px}</style>"
                    + "<form action='http://outside.example/send'><input name=secret value=private><input type=submit></form>");
                response.Headers.Add("Content-Security-Policy",
                    "upgrade-insecure-requests; form-action https://outside.example");
                return response;
            }
            return url switch
            {
                "https://outside.example/send?secret=private" => Redirect("http://third.example/final"),
                "https://third.example/final" => Html("<!doctype html><title>Final</title>"),
                _ => throw new InvalidOperationException("An insecure form request escaped UIR.")
            };
        }));
        var window = OpenWindowAndApproveInitialNavigation(shell, system, "https://source.example/page");
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Controller.FocusPage(tab.Id);
        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Assert.True(shell.Controller.ActivateFocusedLink(tab.Id));
        shell.Tick();

        Assert.Equal("https://source.example/page", tab.AddressText);
        Assert.Equal(["https://source.example/page"], requests);
        ApproveUntilRequestCount(2);
        WaitForRequestCount(2);
        Assert.Equal("https://outside.example/send?secret=private", requests.ToArray()[1]);
        shell.Tick();
        Assert.Equal("https://source.example/page", tab.AddressText);
        ApproveUntilRequestCount(3);
        Wait(() => !tab.IsLoading);

        Assert.Equal([
            "https://source.example/page",
            "https://outside.example/send?secret=private",
            "https://third.example/final"
        ], requests);
        Assert.Equal("https://third.example/final", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void ApproveUntilRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                if (!shell.HasPendingNavigationConfirmation(tab.Id))
                {
                    Thread.Sleep(5);
                    continue;
                }
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void WaitForRequestCount(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (requests.Count < count && DateTime.UtcNow < deadline)
            {
                shell.Tick();
                Thread.Sleep(5);
            }
            Assert.True(requests.Count >= count);
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    [Fact]
    public void SpaceTogglesFocusedCheckboxAndUpdatesItsShellOverlay()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=checkbox name=ok checked></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 0));
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        shell.Tick();
        var checkedPixels = native.Pixels!.ToArray();
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        shell.Tick();
        Assert.False(shell.Controller.FormControlChecked(tab.Id, 0));
        Assert.NotEqual(checkedPixels, native.Pixels!.ToArray());
    }

    [Fact]
    public void EmptyFieldPlaceholderIsDrawnByTheShellAndNotSubmitted()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input name=q placeholder=Search></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        var page = shell.Controller.Page(tab.Id)!;
        var control = Assert.Single(page.FormControls);
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var placeholderPixels = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], -1, -1)).Pixels;
        var emptyPixels = chrome.Render(window, page with
        {
            FormControls = [control with { Placeholder = "" }]
        }, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], -1, -1)).Pixels;
        var valuePixels = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["query"], -1, -1)).Pixels;

        Assert.Equal("Search", control.Placeholder);
        Assert.NotEqual(placeholderPixels, emptyPixels);
        Assert.NotEqual(placeholderPixels, valuePixels);
        Assert.Equal("", shell.Controller.FormControlValue(tab.Id, 0));
    }

    [Fact]
    public void ColorInputDrawsItsBrowserOwnedSwatch()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=color value=#123456></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);

        var page = shell.Controller.Page(tab.Id)!;
        var rect = Assert.Single(page.FormControls).Rect!;
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var pixels = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["#123456"], -1, -1)).Pixels;
        var stride = system.Items[0].Size.Width * 4;
        var offset = ((int)(ShellChrome.Height + rect.Y + rect.Height / 2) * stride) + ((int)rect.X + 8) * 4;

        Assert.Equal((byte)0x56, pixels[offset]);
        Assert.Equal((byte)0x34, pixels[offset + 1]);
        Assert.Equal((byte)0x12, pixels[offset + 2]);
    }

    [Fact]
    public void PointerFocusesColorInputForCommittedText()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=color value=#112233></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 10)));

        Assert.True(shell.Controller.EditingFormControl(tab.Id));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DateTimeLocalPointerPlacesCaretAndAcceptsCommittedText(bool multiprocess, bool readOnly)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;width:220px;height:24px}</style>" +
            $"<form><input type=datetime-local value=2024-01-01T09:00 {(readOnly ? "readonly" : "")}></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        var field = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + 4), (float)(ShellChrome.Height + field.Y + 8)));
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.Null(tab.Error);
        if (readOnly)
        {
            Assert.False(shell.Controller.EditingFormControl(tab.Id));
            Assert.False(native.TextInput);
            shell.Dispatch(new TextEntered(native.Id, "x"));
            Assert.Equal("2024-01-01T09:00", shell.Controller.FormControlValue(tab.Id, 0));
            return;
        }
        Assert.True(shell.Controller.EditingFormControl(tab.Id));
        Assert.Equal(0, shell.Controller.FormControlCaret(tab.Id));
        Assert.Empty(shell.Controller.SelectedTextRects(tab.Id));
        Assert.True(native.TextInput);
        shell.Dispatch(new TextEntered(native.Id, "x"));
        Assert.Equal("x2024-01-01T09:00", shell.Controller.FormControlValue(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + field.Width - 4), (float)(ShellChrome.Height + field.Y + 8)));
        Assert.Equal(17, shell.Controller.FormControlCaret(tab.Id));
    }

    [Fact]
    public void ListboxRowsExposeShellOwnedOptionTargets()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select size=3><option>Fast</option><option>Slow</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);

        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var frame = chrome.Render(window, shell.Controller.Page(tab.Id)!, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["Fast"], -1, -1, SelectIndices: [0]));
        Assert.Equal([0, 1], frame.Targets.Where(target => target.Action == ChromeAction.SelectListboxOption)
            .Select(target => target.OptionIndex));
    }

    [Fact]
    public void PointerSelectsAnEnabledListboxOption()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select size=3><option>Fast</option><option>Slow</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        shell.Tick();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 25)));

        Assert.Equal(1, shell.Controller.SelectedOptionIndex(tab.Id, 0));
    }

    [Fact]
    public void ScrolledListboxRowsMapTargetsThroughFirstVisibleOption()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 6).Select(index => $"<option>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select size=3>" + options + "</select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);

        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var page = shell.Controller.Page(tab.Id)!;
        int[] Rows(int first) => chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
                forms: new ShellFormState(["0"], -1, -1, SelectIndices: [0], ListboxFirstOptions: [first])).Targets
            .Where(target => target.Action == ChromeAction.SelectListboxOption).Select(target => target.OptionIndex).ToArray();
        Assert.Equal([2, 3, 4], Rows(2));
        Assert.Equal([3, 4, 5], Rows(99));
        Assert.Equal([0, 1, 2], Rows(-5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListboxWheelPointerKeyboardAndRepaintShareFirstVisibleOption(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 8).Select(index =>
            index == 4 ? "<option disabled>4</option>" : $"<option>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}p{height:100px}div{height:2000px}</style><p></p><form><select size=3>"
            + options + "</select></form><div></div>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var x = (float)(rect.X + 4);
        float Row(double top, int row) => (float)(ShellChrome.Height + top + 5 + row * 20);

        shell.Dispatch(new PointerMoved(native.Id, x, Row(rect.Y, 1)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        shell.Tick();
        Assert.Equal(3, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, Row(rect.Y, 0)));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, Row(rect.Y, 0)));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, Row(rect.Y, 1)));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, Row(rect.Y, 1)));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        shell.Dispatch(new PointerScrolled(native.Id, 0, float.MaxValue));
        shell.Tick();
        Assert.Equal(5, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -1));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -100));
        shell.Tick();
        Assert.Equal(0, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        Key(SDL.Scancode.End);
        Assert.Equal(7, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        Assert.Equal(5, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Key(SDL.Scancode.Up);
        Key(SDL.Scancode.Up);
        Key(SDL.Scancode.Up);
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        Assert.Equal(3, shell.Controller.ListboxFirstOption(tab.Id, 0));

        shell.Dispatch(new PointerMoved(native.Id, x, (float)(ShellChrome.Height + rect.Y + rect.Height + 40)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Wait(() => shell.Controller.Page(tab.Id)!.FormControls[0].Rect!.Y != rect.Y);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Assert.Equal(3, shell.Controller.ListboxFirstOption(tab.Id, 0));
        var moved = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        Assert.Equal(rect.Y - 48, moved.Y);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, Row(moved.Y, 2)));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, Row(moved.Y, 2)));
        Assert.Equal(5, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        Assert.Equal(3, shell.Controller.ListboxFirstOption(tab.Id, 0));

        void Key(SDL.Scancode scan)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, 0, true, false));
            shell.Tick();
        }
        void Wait(Func<bool>? ready = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); }
            while ((tab.IsLoading || ready?.Invoke() == false) && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MultipleListboxRendersSelectedRowsCursorAndOffsetTargets()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 6).Select(index => $"<option>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple size=3>" + options + "</select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var page = shell.Controller.Page(tab.Id)!;
        var rect = page.FormControls[0].Rect!;
        var frame = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], 0, -1, SelectIndices: [-1], SelectSets: [new HashSet<int> { 2, 4 }],
                ListboxFirstOptions: [2], ListboxCursorOptions: [3]));
        Assert.Equal([2, 3, 4], frame.Targets.Where(target => target.Action == ChromeAction.SelectListboxOption)
            .Select(target => target.OptionIndex));
        Assert.DoesNotContain(frame.Targets, target => target.Action == ChromeAction.SelectOption);
        Assert.Equal(new byte[] { 249, 213, 176, 255 }, Pixel(frame, (int)rect.X + 3, Row(0)));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(frame, (int)rect.X + 3, Row(1)));
        Assert.Equal(new byte[] { 160, 96, 32, 255 }, Pixel(frame, (int)rect.X + 1, Row(1)));
        Assert.Equal(new byte[] { 249, 213, 176, 255 }, Pixel(frame, (int)rect.X + 3, Row(2)));

        var unfocused = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], -1, -1, SelectIndices: [-1], SelectSets: [new HashSet<int> { 2, 4 }],
                ListboxFirstOptions: [2], ListboxCursorOptions: [3]));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(unfocused, (int)rect.X + 1, Row(1)));

        int Row(int row) => (int)(ShellChrome.Height + rect.Y + 1 + row * 20 + 10);
        static byte[] Pixel(ShellFrame frame, int x, int y) => frame.Pixels.AsSpan(y * frame.Stride + x * 4, 4).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleListboxWheelPointerAndKeyboardToggleWithoutPopup(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 8).Select(index =>
            index == 4 ? "<option disabled>4</option>" : $"<option>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}div{height:2000px}</style><form><select multiple size=3 name=m>"
            + options + "</select></form><div></div>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var x = (float)(rect.X + 4);
        float Row(int row) => (float)(ShellChrome.Height + rect.Y + 5 + row * 20);

        shell.Dispatch(new PointerMoved(native.Id, x, Row(1)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        shell.Tick();
        Assert.Equal(3, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        Click(Row(0));
        Click(Row(2));
        Assert.Equal([3, 5], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Click(Row(1));
        Assert.Equal([3, 5], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Click(Row(0));
        Assert.Equal([5], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Assert.Equal(3, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));

        Key(SDL.Scancode.Space);
        Assert.Equal([3, 5], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Return);
        Key(SDL.Scancode.Space);
        Assert.Equal([5], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.End);
        Assert.Equal(7, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal(5, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Key(SDL.Scancode.Home);
        Assert.Equal(0, shell.Controller.ListboxFirstOption(tab.Id, 0));
        Key(SDL.Scancode.Down);
        Key(SDL.Scancode.Space, SDL.Keymod.Shift);
        Key(SDL.Scancode.Space, repeat: true);
        Assert.Equal([5, 7], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([1, 5, 7], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        shell.Tick();
        var below = (int)(ShellChrome.Height + rect.Y + rect.Height + 10);
        Assert.Equal(new byte[] { 255, 255, 255, 255 },
            native.Pixels!.AsSpan((below * native.Size.Width + (int)x) * 4, 4).ToArray());
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        void Click(float y)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        }
        void Key(SDL.Scancode scan, SDL.Keymod modifiers = 0, bool repeat = false)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)modifiers, true, repeat));
            shell.Tick();
        }
    }

    [Fact]
    public void PasswordOverlayDoesNotRevealFieldValue()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=password value=secret></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        var page = shell.Controller.Page(tab.Id)!;
        var control = Assert.Single(page.FormControls);
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var first = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["secret"], 0, 6)).Pixels;
        var second = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["hidden"], 0, 6)).Pixels;
        var unfocusedSecret = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["secret"], -1, -1)).Pixels;
        var unfocusedHidden = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["hidden"], -1, -1)).Pixels;
        var oneScalar = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["a"], -1, -1)).Pixels;
        var supplementaryScalar = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["💩"], -1, -1)).Pixels;

        Assert.Equal("password", control.Kind);
        Assert.Equal(first, second);
        Assert.Equal(unfocusedSecret, unfocusedHidden);
        Assert.Equal(oneScalar, supplementaryScalar);
    }

    [Fact]
    public void MultipleSelectPopupHighlightsEverySelectedOption()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}select{display:block;height:20px}</style>"
            + "<form><select multiple><option selected>Fast</option><option selected>Slow</option></select></form>"));
        Wait(shell, tab);

        var page = shell.Controller.Page(tab.Id)!;
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var selectedOne = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["Fast"], -1, -1, SelectIndices: [0], OpenSelect: 0)).Pixels;
        var selectedBoth = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["Fast"], -1, -1, SelectIndices: [0],
                SelectSets: [new HashSet<int> { 0, 1 }], OpenSelect: 0)).Pixels;

        Assert.NotEqual(selectedOne, selectedBoth);

        static void Wait(DevelopmentShell shell, BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MultipleSelectPopupAllowsConsecutivePointerToggles()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}select{display:block;height:20px}</style>"
            + "<form><select multiple><option>Fast</option><option>Slow</option></select></form>"));
        Wait(shell, tab);

        var page = shell.Controller.Page(tab.Id)!;
        var control = Assert.Single(page.FormControls);
        var viewport = ShellChrome.Viewport(native.PixelSize, native.Density)!.Value;
        var popup = ShellChrome.PopupLayout(page, 0, viewport, 0)!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(control.Rect!.X + 1), (float)(ShellChrome.Height + control.Rect.Y + 1)));
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(popup.Bounds.X + 1), (float)(popup.Bounds.Y + popup.RowHeight / 2)));
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(popup.Bounds.X + 1), (float)(popup.Bounds.Y + popup.RowHeight * 1.5)));

        Assert.Equal([0, 1], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        static void Wait(DevelopmentShell shell, BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MultipleSelectFieldShowsItsSelectionCount()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}select{display:block;height:20px}</style>"
            + "<form><select multiple><option selected>Fast</option><option selected>Slow</option></select></form>"));
        Wait(shell, tab);

        var page = shell.Controller.Page(tab.Id)!;
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var oneSelected = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["Fast"], -1, -1, SelectIndices: [0],
                SelectSets: [new HashSet<int> { 0 }])).Pixels;
        var twoSelected = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["Fast"], -1, -1, SelectIndices: [0],
                SelectSets: [new HashSet<int> { 0, 1 }])).Pixels;

        Assert.NotEqual(oneSelected, twoSelected);

        static void Wait(DevelopmentShell shell, BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void EnterActivatesFocusedResetAndRestoresShellOwnedFormState()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input,button{display:block;height:20px}</style>" +
            "<form><input name=text value=original><input type=checkbox name=checked checked>" +
            "<input type=range name=level min=1 max=9 step=2 value=5><button type=reset>Reset</button></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Assert.Equal("original-edited", shell.Controller.InsertFormText(tab.Id, "-edited"));
        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Assert.True(shell.Controller.ToggleFocusedCheckable(tab.Id));
        Assert.True(shell.Controller.FocusControl(tab.Id, 2));
        Assert.True(shell.Controller.AdjustFocusedRange(tab.Id, 1));
        Assert.True(shell.Controller.FocusControl(tab.Id, 3));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));

        Assert.Equal(3, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.Equal("original", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        Assert.Equal("5", shell.Controller.FormControlValue(tab.Id, 2));
        Assert.StartsWith("data:text/html,", tab.History.Current!.Href);
        Assert.Null(tab.Error);
    }

    [Fact]
    public void SpaceActivatesFocusedSubmitAndResetControls()
    {
        using var system = new Windows();
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "https://forms.example/form");
        Wait();

        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Assert.Equal("original-edited", shell.Controller.InsertFormText(tab.Id, "-edited"));
        Assert.True(shell.Controller.FocusControl(tab.Id, 2));
        Key(SDL.Scancode.Space);
        Assert.Equal("original", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.StartsWith("https://forms.example/form", tab.History.Current!.Href);
        Assert.Single(requests);

        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Key(SDL.Scancode.Space);
        Wait();
        Assert.Equal("https://forms.example/form?q=original", tab.History.Current!.Href);
        Assert.Equal("https://forms.example/form?q=original", requests[^1].AbsoluteUri);

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var html = request.RequestUri!.Query.Length == 0
                ? "<!doctype html><style>*{margin:0}input,button{display:block;height:20px}</style>"
                    + "<form><input name=q value=original><button type=submit>Save</button><button type=reset>Reset</button></form>"
                : "<!doctype html><style>*{margin:0}</style><title>Saved</title>done";
            return new(HttpStatusCode.OK) { Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html") };
        });
        void Key(SDL.Scancode scan) => shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlClickOpensVisibleLinkInBackgroundTab(bool process)
    {
        using var system = new Windows();
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: process ? RendererPath : null,
            pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var sourceTab = window.ActiveTab!;
        var sourceUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style>"
            + "<input type=checkbox><a href='https://example.com/target' target=_blank>open target</a>");
        shell.Controller.Navigate(sourceTab.Id, sourceUrl);
        Wait(sourceTab);
        var page = shell.Controller.Page(sourceTab.Id)!;
        var checkbox = Assert.Single(page.FormControls);
        var link = Assert.Single(page.LinkTargets);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.LCtrl, 0,
            (ushort)SDL.Keymod.Ctrl, true, false));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(checkbox.Rect!.X + checkbox.Rect.Width / 2),
            (float)(ShellChrome.Height + checkbox.Rect.Y + checkbox.Rect.Height / 2)));
        Assert.False(shell.Controller.FormControlChecked(sourceTab.Id, 0));
        Assert.Single(window.Tabs);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(link.X + link.Width / 2), (float)(ShellChrome.Height + link.Y + link.Height / 2)));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.LCtrl, 0, 0, false, false));
        shell.Tick();

        Assert.Equal(2, window.Tabs.Count);
        Assert.Equal(sourceTab.Id, window.ActiveTabId);
        var backgroundTab = Assert.Single(window.Tabs, tab => tab.Id != sourceTab.Id);
        Assert.Empty(requests);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        shell.Tick();
        Wait(backgroundTab);
        Assert.Equal("https://example.com/target", backgroundTab.History.Current!.Href);
        Assert.Equal(sourceUrl, sourceTab.History.Current!.Href);
        Assert.Equal(["https://example.com/target"], requests.Select(uri => uri.AbsoluteUri));

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><style>*{margin:0}</style><p>target</p>",
                    System.Text.Encoding.UTF8, "text/html"),
            };
        });
        void Wait(BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MiddleClickOpensVisibleLinkInNewActiveTab(bool process)
    {
        using var system = new Windows();
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: process ? RendererPath : null,
            pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var sourceTab = window.ActiveTab!;
        var sourceUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='https://example.com/target' target=_self>open target</a>");
        shell.Controller.Navigate(sourceTab.Id, sourceUrl);
        Wait(sourceTab);
        var link = Assert.Single(shell.Controller.Page(sourceTab.Id)!.LinkTargets);

        shell.Dispatch(new PointerButtonChanged(native.Id, 2, true,
            (float)(link.X + link.Width / 2), (float)(ShellChrome.Height + link.Y + link.Height / 2)));
        shell.Tick();

        Assert.Equal(2, window.Tabs.Count);
        Assert.NotEqual(sourceTab.Id, window.ActiveTabId);
        var destinationTab = window.ActiveTab!;
        Assert.Empty(requests);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        shell.Tick();
        Wait(destinationTab);
        Assert.Equal("https://example.com/target", destinationTab.History.Current!.Href);
        Assert.Equal(sourceUrl, sourceTab.History.Current!.Href);
        Assert.Equal(["https://example.com/target"], requests.Select(uri => uri.AbsoluteUri));

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><style>*{margin:0}</style><p>target</p>",
                    System.Text.Encoding.UTF8, "text/html"),
            };
        });
        void Wait(BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptedInMiddleClickDispatchesCancelableAuxClick(bool process)
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, executeInlineScripts: true,
            rendererPath: process ? RendererPath : null);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var html = "<!doctype html><title>Start</title><style>*{margin:0}</style>"
            + "<a href='https://example.com/target'>open target</a>"
            + "<script>document.addEventListener('auxclick',e=>{document.title=e.type+':'+e.isTrusted; e.preventDefault()})</script>";
        var sourceUrl = "data:text/html," + Uri.EscapeDataString(html);
        shell.Controller.Navigate(tab.Id, sourceUrl);
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);

        shell.Dispatch(new PointerButtonChanged(native.Id, 2, true,
            (float)(link.X + link.Width / 2), (float)(ShellChrome.Height + link.Y + link.Height / 2)));
        Wait(() => !tab.IsLoading && tab.Title == "auxclick:true");

        Assert.Single(window.Tabs);
        Assert.Equal(tab.Id, window.ActiveTabId);
        Assert.Equal(sourceUrl, tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        }
    }

    [Fact]
    public void ArrowKeysMoveWithinRadioGroupAndSpaceSelectsFocusedRadio()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=radio name=mode value=a><input type=radio name=mode value=b></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Right, 0, 0, true, false));
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        Assert.False(shell.Controller.FormControlChecked(tab.Id, 0));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    [InlineData(true, 2)]
    public void KeyboardLinksTraverseFromAddressOutlineEveryRectAndActivate(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><title>Keyboard destination</title><p>done</p>");
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            $"<!doctype html><style>*{{margin:0}}</style><a href='{destination}'>one <span>two</span> three</a> " +
            "<a href='javascript:alert(1)'>unsupported</a>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var original = page.Frame.Pixels.ToArray();
        var header = (int)Math.Ceiling(ShellChrome.Height * density);
        Assert.Equal(original, native.Pixels!.Skip(header * page.Frame.Stride));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        shell.Tick();
        Assert.False(native.TextInput);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Assert.Equal(original, page.Frame.Pixels.ToArray());
        foreach (var rect in page.LinkTargets[0].Rects)
        {
            var left = (int)Math.Floor(rect.X * density);
            var right = (int)Math.Ceiling((rect.X + rect.Width) * density) - 1;
            var top = header + (int)Math.Floor(rect.Y * density);
            var bottom = header + (int)Math.Ceiling((rect.Y + rect.Height) * density) - 1;
            for (var x = left; x <= right; x++) { BorderPixel(x, top); BorderPixel(x, bottom); }
            for (var y = top; y <= bottom; y++) { BorderPixel(left, y); BorderPixel(right, y); }
        }
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.Return);
        Assert.Contains("Unsupported link URL scheme: javascript:", tab.Error);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(original, native.Pixels!.Skip(header * page.Frame.Stride));
        Key(SDL.Scancode.Return);
        WaitForPrompt();
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        Key(SDL.Scancode.Return);
        Wait();
        Assert.Equal(-1, shell.Controller.FocusedLinkIndex(tab.Id));
        var addressBarDocument = shell.Controller.Page(tab.Id)!;
        var committedUrl = tab.History.Current!.Href;
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Key(SDL.Scancode.Return);
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(addressBarDocument, shell.Controller.Page(tab.Id));
        Assert.Equal(committedUrl, tab.History.Current!.Href);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));

        void BorderPixel(int x, int y) => Assert.Equal(new byte[] { 128, 96, 64, 255 },
            native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(4));
        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
        void WaitForPrompt()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); }
            while (!shell.HasPendingNavigationConfirmation(tab.Id) && DateTime.UtcNow < deadline);
            Assert.True(shell.HasPendingNavigationConfirmation(tab.Id));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerClickFocusesTextFieldWithoutStartingPageTextSelection(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;width:160px;height:24px}</style>" +
            "<form><input name=q value=start></form>"));
        Wait();

        var field = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + 4), (float)(ShellChrome.Height + field.Y + 8)));

        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(shell.Controller.EditingFormControl(tab.Id));
        Assert.Equal(0, shell.Controller.FormControlCaret(tab.Id));
        Assert.Empty(shell.Controller.SelectedTextRects(tab.Id));
        Assert.True(native.TextInput);
        shell.Dispatch(new TextEntered(native.Id, "typed"));
        Assert.Equal("typedstart", shell.Controller.FormControlValue(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + field.Width - 4), (float)(ShellChrome.Height + field.Y + 8)));
        Assert.Equal("typedstart".Length, shell.Controller.FormControlCaret(tab.Id));
        shell.Dispatch(new TextEntered(native.Id, "!"));
        Assert.Equal("typedstart!", shell.Controller.FormControlValue(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerClickPlacesTextareaCaretOnClickedVisualLine(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}textarea{width:160px;height:40px}</style><textarea>first\nsecond</textarea>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        var textarea = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;

        Click(textarea.X + 4, ShellChrome.Height + textarea.Y + 8);
        Assert.Equal(0, shell.Controller.FormControlCaret(tab.Id));
        shell.Dispatch(new TextEntered(system.Items[0].Id, "A"));
        Assert.Equal("Afirst\nsecond", shell.Controller.FormControlValue(tab.Id, 0));

        Click(textarea.X + 4, ShellChrome.Height + textarea.Y + 23);
        Assert.Equal(7, shell.Controller.FormControlCaret(tab.Id));
        shell.Dispatch(new TextEntered(system.Items[0].Id, "B"));
        Assert.Equal("Afirst\nBsecond", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Empty(shell.Controller.SelectedTextRects(tab.Id));

        void Click(double x, double y) => shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, (float)x, (float)y));
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleSelectPopupKeepsDisabledOptionsInertAndDismissesWithEscape(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Blocked</option>" +
            "<optgroup label=Unavailable disabled><option value=group-blocked>Group blocked</option></optgroup>" +
            "<option value=three>Three</option></select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 30)));
        Assert.Equal(0, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 50)));
        Assert.Equal(0, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 70)));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 10)));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnterAndSpaceToggleFocusedSelectPopupForKeyboardOptionPicking(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Blocked</option>" +
            "<option value=three>Three</option></select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        Key(SDL.Scancode.Return);
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 50)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        Key(SDL.Scancode.Space);
        shell.Tick();
        Key(SDL.Scancode.Escape);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 10)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleSelectKeyboardTogglesRetainPopupAndDismissWithoutReverting(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple name=mode>" +
            "<option selected disabled>Blocked</option><option value=one selected>One</option>" +
            "<option disabled>Blocked too</option><option value=three>Three</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        Key(SDL.Scancode.Return);
        Key(SDL.Scancode.Down);
        Assert.Equal([1], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space, repeat: true);
        Assert.Equal([1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Space);
        Assert.Equal([3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Down);
        shell.Tick();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, RowPixel(0)); // Disabled default is not selected.
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, RowPixel(1)); // Deselected default stays unselected.
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Escape);
        Key(SDL.Scancode.Space); // Opens, rather than toggling, the closed popup.
        Assert.Equal([3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Return); // Dismisses without modifying selection.
        Key(SDL.Scancode.Space);
        Assert.Equal([1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Escape);
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + rect.Height + 30)));
        Assert.Equal([3], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Key(SDL.Scancode code, bool repeat = false) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, repeat));
        byte[] RowPixel(int row) => native.Pixels!.AsSpan(
            ((int)(popupY + row * 20 + 5) * native.Size.Width + (int)(rect.X + 4)) * 4, 4).ToArray();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ControlASelectsEnabledOptionsInFocusedMultipleSelect(bool multiprocess, bool listbox)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var size = listbox ? " size=3" : "";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple" + size + ">" +
            "<option disabled>Blocked</option><option selected>One</option><option>Two</option>" +
            "<option disabled>Blocked too</option><option>Three</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        if (!listbox) { Key(SDL.Scancode.Space); }
        Key(SDL.Scancode.A, (ushort)SDL.Keymod.Ctrl);
        Assert.Equal([1, 2, 4], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Assert.Equal(1, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));

        Key(SDL.Scancode.Space);
        Assert.Equal([2, 4], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Key(SDL.Scancode code, ushort modifiers = 0) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, modifiers, true, false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShiftArrowReplacesMultipleSelectWithEnabledRange(bool multiprocess, bool listbox)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var size = listbox ? " size=3" : "";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple" + size + ">" +
            "<option selected>Zero</option><option>One</option><option disabled>Blocked</option>" +
            "<option>Three</option><option selected>Four</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        if (!listbox) { Key(SDL.Scancode.Return); }
        Key(SDL.Scancode.Down, (ushort)SDL.Keymod.Shift);
        Assert.Equal(1, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0, 1], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Down, (ushort)SDL.Keymod.Shift);
        Assert.Equal(3, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0, 1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Up, (ushort)SDL.Keymod.Shift);
        Assert.Equal(1, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0, 1], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        Key(SDL.Scancode.Down);
        Assert.Equal(3, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0, 1], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Up, (ushort)SDL.Keymod.Shift);
        Assert.Equal(1, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([1, 3], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Key(SDL.Scancode code, ushort modifiers = 0) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, modifiers, true, false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShiftHomeEndExtendMultipleSelectToEnabledEndpoints(bool multiprocess, bool listbox)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var size = listbox ? " size=3" : "";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple" + size + ">" +
            "<option>Zero</option><option selected>One</option><option disabled>Blocked</option>" +
            "<option>Three</option><option selected>Four</option></select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        if (!listbox) { Key(SDL.Scancode.Return); }
        Key(SDL.Scancode.End, (ushort)SDL.Keymod.Shift);
        Assert.Equal(4, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([1, 3, 4], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Home, (ushort)SDL.Keymod.Shift);
        Assert.Equal(0, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0, 1], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Key(SDL.Scancode code, ushort modifiers = 0) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, modifiers, true, false));
    }

    [Fact]
    public void MultipleSelectPopupPageKeysKeepCursorVisibleWithoutSelecting()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 20).Select(index =>
            $"<option value={index}{(index is 0 or 12 ? " disabled" : "")}>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple>" + options + "</select></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Key(SDL.Scancode.Pagedown);
        Assert.Equal(14, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Empty(shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([14], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        shell.Dispatch(new TextEntered(native.Id, "19"));
        Assert.Equal(19, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([14], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Space);
        Assert.Equal([14, 19], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        Key(SDL.Scancode.Pageup);
        Assert.Equal(6, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Key(SDL.Scancode.End);
        Assert.Equal(19, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([14, 19], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        shell.Tick();
        var page = shell.Controller.Page(tab.Id)!;
        var rect = page.FormControls[0].Rect!;
        var popup = ShellChrome.PopupLayout(page, 0,
            ShellChrome.Viewport(native.PixelSize, native.PixelDensity)!.Value, 8)!;
        // The final cursor must be in view: click its last displayed popup row.
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4),
            (float)(popup.Bounds.Y + (popup.VisibleOptions - 1) * popup.RowHeight + 10)));
        Assert.Equal([14], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
    }

    [Fact]
    public void SingleSelectPopupHighlightsHoveredOptionsAndClearsHighlightOutsideMenu()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Blocked</option>" +
            "<option value=three>Three</option></select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupX = rect.X + 8;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();

        shell.Dispatch(new PointerMoved(native.Id, (float)popupX, (float)(popupY + 30)));
        shell.Tick();
        Assert.Equal(new byte[] { 230, 230, 230, 255 }, Pixel((int)(rect.X + rect.Width / 2), (int)popupY + 30));
        shell.Dispatch(new PointerMoved(native.Id, (float)popupX, (float)(popupY + 50)));
        shell.Tick();
        Assert.Equal(new byte[] { 245, 224, 205, 255 }, Pixel((int)(rect.X + rect.Width / 2), (int)popupY + 50));
        shell.Dispatch(new PointerMoved(native.Id, 5, 5));
        shell.Tick();
        Assert.NotEqual(new byte[] { 230, 230, 230, 255 }, Pixel((int)(rect.X + rect.Width / 2), (int)popupY + 30));
        Assert.Equal(0, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        byte[] Pixel(int x, int y) => native.Pixels!.AsSpan(y * native.Size.Width * 4 + x * 4, 4).ToArray();
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void SingleSelectPopupPageKeysMoveByVisibleOptionsAndSkipDisabledEntries()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 20).Select(index =>
            $"<option value={index}{(index == 12 ? " disabled" : "")}>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" + options + "</select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();

        Key(SDL.Scancode.Pagedown);
        Assert.Equal(13, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        Key(SDL.Scancode.Pageup);
        Assert.Equal(0, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void SingleSelectTypeaheadMatchesPrefixesSkipsDisabledOptionsAndCyclesRepeatedLetters()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Three hidden</option>" +
            "<option value=three>Three</option><option value=tiger>Tiger</option></select></form>"));
        Wait();
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        shell.Dispatch(new TextEntered(native.Id, "t"));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new TextEntered(native.Id, "h"));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        Key(SDL.Scancode.Return);
        Key(SDL.Scancode.Escape);
        shell.Dispatch(new TextEntered(native.Id, "t"));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new TextEntered(native.Id, "t"));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new TextEntered(native.Id, "z"));
        Assert.Equal(3, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MultipleSelectTypeaheadMovesCursorWithoutTogglingSelection()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select multiple name=mode>" +
            "<option selected>Alpha</option><option disabled>Beta blocked</option>" +
            "<option>Bravo</option><option>Blue</option></select></form>"));
        Wait();
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        shell.Dispatch(new TextEntered(native.Id, "b"));
        Assert.Equal(2, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0], shell.Controller.SelectedOptionIndices(tab.Id, 0));
        shell.Dispatch(new TextEntered(native.Id, "b"));
        Assert.Equal(3, shell.Controller.ActiveSelectOptionIndex(tab.Id, 0));
        Assert.Equal([0], shell.Controller.SelectedOptionIndices(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void SingleSelectPopupWheelScrollsToOptionsOutsideTheInitialWindow()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 20).Select(index =>
            $"<option value={index}>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" + options + "</select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupX = rect.X + 4;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)popupX, (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();
        shell.Dispatch(new PointerMoved(native.Id, (float)popupX, (float)(popupY + 10)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -4));
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)popupX, (float)(popupY + 230)));
        Assert.Equal(19, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlASelectionCanBeReplacedByClipboardPaste(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;width:160px;height:24px}</style>" +
            "<form><input name=q value=original></form>"));
        Wait();
        var field = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + 8), (float)(ShellChrome.Height + field.Y + 8)));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.A, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.True(shell.Controller.FormControlSelectAll(tab.Id));
        shell.Tick();
        var pixelX = (int)field.X + (int)field.Width - 2;
        var pixelY = (int)ShellChrome.Height + (int)field.Y + 2;
        Assert.Equal(new byte[] { 249, 213, 176, 255 }, native.Pixels!
            .Skip(pixelY * native.PixelSize.Width * 4 + pixelX * 4).Take(4));

        native.SetClipboardText("replacement");
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.V, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("replacement", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.False(shell.Controller.FormControlSelectAll(tab.Id));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    public void FormFieldsTakeTabFocusAndBlockPageInitiatedDataSubmission(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Result</title><style>*{margin:0}</style><p>done</p><!--");
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0} input{display:block;width:150px;height:24px}</style>" +
            $"<a href='{destination}'>link</a><form action='{destination}'><input name=q value=a>" +
            "<input type=submit value=Send></form><div style='height:2000px'></div>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var original = page.Frame.Pixels.ToArray();
        var header = (int)Math.Ceiling(ShellChrome.Height * density);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(native.TextInput);
        shell.Tick();
        Assert.Equal(original, page.Frame.Pixels.ToArray());
        var field = page.FormControls[0].Rect!;
        var row = header + (int)Math.Floor(field.Y * density);
        var left = (int)Math.Floor(field.X * density);
        Assert.Equal(new byte[] { 128, 96, 64, 255 }, native.Pixels!.Skip(row * page.Frame.Stride + left * 4).Take(4));
        Assert.NotEqual(original.Skip(((int)Math.Floor((field.Y + 4) * density)) * page.Frame.Stride).Take(page.Frame.Stride),
            native.Pixels!.Skip((header + (int)Math.Floor((field.Y + 4) * density)) * page.Frame.Stride).Take(page.Frame.Stride));
        shell.Dispatch(new TextEntered(native.Id, "b c"));
        Assert.Equal("ab c", shell.Controller.FormControlValue(tab.Id, 0));
        Key(SDL.Scancode.Backspace);
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.End);
        Key(SDL.Scancode.Pagedown);
        Key(SDL.Scancode.Down);
        Key(SDL.Scancode.Up);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        Assert.Equal("ab ", shell.Controller.FormControlValue(tab.Id, 0));
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Key(SDL.Scancode.Return);
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.Equal(initialUrl, tab.History.Current!.Href);
        Assert.Equal("ab ", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(native.TextInput);

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextareaEnterInsertsLineAndTabEnterSubmits(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "https://forms.example/form");
        Wait();
        var textarea = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(textarea.X + textarea.Width - 4), (float)(ShellChrome.Height + textarea.Y + 8)));

        Assert.True(native.TextInput);
        Key(SDL.Scancode.Return);
        shell.Dispatch(new TextEntered(native.Id, "second"));
        Key(SDL.Scancode.Up);
        shell.Dispatch(new TextEntered(native.Id, "!"));
        Key(SDL.Scancode.Down);
        shell.Dispatch(new TextEntered(native.Id, "?"));
        Key(SDL.Scancode.Home);
        shell.Dispatch(new TextEntered(native.Id, "^"));
        Key(SDL.Scancode.End);
        shell.Dispatch(new TextEntered(native.Id, "$"));
        Assert.Equal("first!\n^second?$", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Single(tab.History.Entries);
        Assert.Single(requests);
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Return);
        Wait();
        Assert.Equal("Result", tab.Title);
        Assert.Equal("https://forms.example/message?body=first%21%0D%0A%5Esecond%3F%24&send=yes", tab.History.Current!.Href);
        Assert.Equal(2, requests.Count);

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var html = request.RequestUri!.AbsolutePath == "/form"
                ? "<!doctype html><style>*{margin:0}</style><form action='/message'>" +
                  "<textarea name=body>first</textarea><button name=send value=yes>Send</button></form>"
                : "<!doctype html><style>*{margin:0}</style><title>Result</title><p>sent</p>";
            return new(HttpStatusCode.OK) { Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html") };
        });
        void Key(SDL.Scancode scan) => shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputButtonIsPaintedFocusedAndInertByClickOrEnter(bool click)
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>body{margin:0}</style><form action='https://example.com/save'><input name=q value=x>"
            + "<input type=button name=b value=Press></form>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.True(tab.Error is null, tab.Error);
        var page = shell.Controller.Page(tab.Id)!;
        var button = page.FormControls[1];
        Assert.Equal("inert", button.Kind);
        Assert.Equal("Press", button.Label);
        shell.Tick();
        var header = (int)Math.Ceiling(ShellChrome.Height * native.Density);
        var bounds = button.Rect!;
        var left = (int)Math.Floor(bounds.X * native.Density);
        var right = (int)Math.Ceiling((bounds.X + bounds.Width) * native.Density);
        var top = header + (int)Math.Floor(bounds.Y * native.Density);
        var bottom = header + (int)Math.Ceiling((bounds.Y + bounds.Height) * native.Density);
        Assert.Contains(Enumerable.Range(top, bottom - top).SelectMany(y => Enumerable.Range(left, right - left)
            .Select(x => native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(3).ToArray())),
            pixel => pixel.All(channel => channel < 60));

        if (click)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
                (float)(bounds.X + 1), (float)(ShellChrome.Height + bounds.Y + 1)));
        }
        else
        {
            Assert.True(shell.Controller.FocusControl(tab.Id, 1));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        }
        shell.Tick();
        Assert.Null(tab.Error);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);

        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        Assert.Null(tab.Error);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnstyledFormButtonIsPaintedAndBlocksDataActionByClickOrEnter(bool click)
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0}</style><title>Submitted</title>done<!--");
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            $"<!doctype html><style>body{{margin:0}}</style><form action='{destination}'><input name=q value=x><button name=go value=1>Search</button></form>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var button = page.FormControls[1];
        Assert.Equal("Search", button.Label);
        var bounds = button.Rect;
        Assert.NotNull(bounds);
        shell.Tick();
        var header = (int)Math.Ceiling(ShellChrome.Height * native.Density);
        var left = (int)Math.Floor(bounds.X * native.Density);
        var right = (int)Math.Ceiling((bounds.X + bounds.Width) * native.Density);
        var top = header + (int)Math.Floor(bounds.Y * native.Density);
        var bottom = header + (int)Math.Ceiling((bounds.Y + bounds.Height) * native.Density);
        Assert.Contains(Enumerable.Range(top, bottom - top).SelectMany(y => Enumerable.Range(left, right - left)
            .Select(x => native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(3).ToArray())),
            pixel => pixel.All(channel => channel < 60));

        if (click)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
                (float)(bounds.X + 1), (float)(ShellChrome.Height + bounds.Y + 1)));
        }
        else
        {
            shell.Controller.FocusControl(tab.Id, 0);
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        }
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisibleLinkHoverDrawsBrowserOwnedOutlineWithoutChangingPagePixels(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='#destination'>visible link</a><p id=destination>Done</p>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var rect = Assert.Single(page.LinkTargets).Rects[0];
        var before = native.Pixels!.ToArray();
        var x = (float)(rect.X + rect.Width / 2);
        var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);

        shell.Dispatch(new PointerMoved(native.Id, x, y));
        shell.Tick();

        var headerPixels = (int)Math.Ceiling(ShellChrome.Height * native.Density);
        var pixelX = (int)Math.Floor(rect.X * native.Density);
        var pixelY = headerPixels + (int)Math.Floor(rect.Y * native.Density);
        Assert.Equal(new byte[] { 80, 130, 190, 255 }, Pixel(pixelX, pixelY));
        Assert.Equal(new byte[] { 255, 255, 255, 255 },
            page.Frame.Pixels.Span.Slice((pixelY - headerPixels) * page.Frame.Stride + pixelX * 4, 4).ToArray());

        shell.Dispatch(new PointerMoved(native.Id, 5, 5));
        shell.Tick();
        Assert.Equal(before.AsSpan(pixelY * native.Size.Width * 4 + pixelX * 4, 4).ToArray(), Pixel(pixelX, pixelY));

        byte[] Pixel(int px, int py) => native.Pixels!.AsSpan(py * native.Size.Width * 4 + px * 4, 4).ToArray();
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragStartingOnLinkSelectsTextInsteadOfActivatingIt(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>body{margin:0}p{margin:0}</style>" +
            "<a href='#destination'>Hello world!</a><p id=destination>Destination</p>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var initialUrl = tab.History.Current!.Href;
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - first.Y) <= 0.5).Rect;
        var startX = first.X + first.Width / 2;
        var endX = last.X + last.Width / 2;
        var y = ShellChrome.Height + first.Y + first.Height / 2;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)startX, (float)y));
        shell.Dispatch(new PointerMoved(native.Id, (float)endX, (float)y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, (float)endX, (float)y));

        Assert.Equal("Hello world!", shell.Controller.SelectedText(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrimaryLinkClickActivatesOnlyAfterPointerRelease(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='#destination'>go</a><p id=destination>target</p>"));
        Wait();
        var link = Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(ShellChrome.Height + link.Y + link.Height / 2);
        var initialUrl = tab.History.Current!.Href;

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        Assert.Equal(initialUrl, tab.History.Current!.Href);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));

        Assert.EndsWith("#destination", tab.History.Current!.Href, StringComparison.Ordinal);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragSelectsVisibleTextAndCtrlCCopiesSelection(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>body{margin:0} p{margin:0} input{display:block;height:20px}</style>"
            + "<input type=checkbox><p>Hello world!</p><div style='height:1000px'></div>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        Assert.Equal("Hello world!", string.Concat(page.TextTargets.Select(target => target.Text)));
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets[^1].Rect;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(first.X + first.Width / 2),
            (float)(ShellChrome.Height + first.Y + first.Height / 2)));
        shell.Dispatch(new PointerMoved(native.Id, (float)(last.X + last.Width / 2),
            (float)(ShellChrome.Height + last.Y + last.Height / 2)));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, (float)(last.X + last.Width / 2),
            (float)(ShellChrome.Height + last.Y + last.Height / 2)));
        Assert.Equal("Hello world!", shell.Controller.SelectedText(tab.Id));
        shell.Tick();
        var pixelX = (int)Math.Floor(first.X * native.Density);
        var pixelY = (int)Math.Floor(first.Y * native.Density);
        var pageOffset = pixelY * page.Frame.Stride + pixelX * 4;
        var chromeOffset = ((int)Math.Ceiling(ShellChrome.Height * native.Density) + pixelY) * page.Frame.Stride + pixelX * 4;
        Assert.False(page.Frame.Pixels.Span.Slice(pageOffset, 4).SequenceEqual(native.Pixels!.AsSpan(chromeOffset, 4)));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.C, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("Hello world!", native.ClipboardText);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        Assert.Equal("", shell.Controller.SelectedText(tab.Id));
        Assert.Equal("Hello world!", native.ClipboardText);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.A, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("Hello world!", shell.Controller.SelectedText(tab.Id));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.C, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("Hello world!", native.ClipboardText);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.LCtrl, 0, 0, false, false));
        var checkbox = Assert.Single(shell.Controller.Page(tab.Id)!.FormControls);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(checkbox.Rect!.X + 2),
            (float)(ShellChrome.Height + checkbox.Rect.Y + checkbox.Rect.Height / 2)));
        Assert.Equal("", shell.Controller.SelectedText(tab.Id));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 0));
        shell.Controller.Scroll(tab.Id, 20);
        var scrollDeadline = DateTime.UtcNow.AddSeconds(30);
        while (shell.Controller.SelectedText(tab.Id).Length > 0 && DateTime.UtcNow < scrollDeadline)
        { shell.Tick(); Thread.Sleep(5); }
        Assert.Equal("", shell.Controller.SelectedText(tab.Id));
        Assert.True(shell.Controller.ScrollY(tab.Id) > 0);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerAndKeyboardSelectionUseExactHarfBuzzClusters(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>office</p><p>a\u0301bc</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        Assert.Equal(new[] { "o", "ffi", "c", "e", "a\u0301", "b", "c" },
            page.TextTargets.Select(target => target.Text));
        var ligature = Assert.Single(page.TextTargets, target => target.Text == "ffi");
        Click(ligature.Rect);
        Assert.Equal("ffi", shell.Controller.SelectedText(tab.Id));

        Key(SDL.Scancode.Right);
        Assert.Equal("ffic", shell.Controller.SelectedText(tab.Id));
        Key(SDL.Scancode.Left);
        Assert.Equal("ffi", shell.Controller.SelectedText(tab.Id));
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.LShift, 0, 0, false, false));

        var ordinaryCharacter = Assert.Single(page.TextTargets, target => target.Text == "o");
        Click(ordinaryCharacter.Rect);
        Assert.Equal("o", shell.Controller.SelectedText(tab.Id));

        var combiningCluster = Assert.Single(page.TextTargets, target => target.Text == "a\u0301");
        Click(combiningCluster.Rect);
        Assert.Equal("a\u0301", shell.Controller.SelectedText(tab.Id));

        void Click(PageLinkRect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y));
        }
        void Key(SDL.Scancode scan) =>
            shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)scan, 0,
                (ushort)SDL.Keymod.Shift, true, false));
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DoubleClickSelectsVisibleSameLineWordByShapingCluster(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>office a\u0301bc word42! end</p><p>start</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var targets = shell.Controller.Page(tab.Id)!.TextTargets;
        var ligature = Assert.Single(targets, target => target.Text == "ffi");
        DoubleClick(ligature.Rect);
        Assert.Equal("office", shell.Controller.SelectedText(tab.Id));

        var combiningCluster = Assert.Single(targets, target => target.Text == "a\u0301");
        DoubleClick(combiningCluster.Rect);
        Assert.Equal("a\u0301bc", shell.Controller.SelectedText(tab.Id));

        var digit = Assert.Single(targets, target => target.Text == "4");
        DoubleClick(digit.Rect);
        Assert.Equal("word42", shell.Controller.SelectedText(tab.Id));
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.C, 0,
            (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("word42", system.Items[0].ClipboardText);
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.LCtrl, 0, 0, false, false));

        var punctuation = Assert.Single(targets, target => target.Text == "!");
        DoubleClick(punctuation.Rect);
        Assert.Equal("!", shell.Controller.SelectedText(tab.Id));

        var firstParagraphLast = targets.Last(target => target.Text == "d");
        DoubleClick(firstParagraphLast.Rect);
        Assert.Equal("end", shell.Controller.SelectedText(tab.Id));

        var secondParagraph = targets.Last(target => target.Text == "s");
        DoubleClick(secondParagraph.Rect);
        Assert.Equal("start", shell.Controller.SelectedText(tab.Id));

        void DoubleClick(PageLinkRect rect)
        {
            Click(rect, 1);
            Click(rect, 2);
        }
        void Click(PageLinkRect rect, byte clickCount)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y, clickCount));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y, clickCount));
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TripleClickSelectsVisibleVisualLine(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first word!</p><p>next line</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var targets = shell.Controller.Page(tab.Id)!.TextTargets;
        var firstLine = Assert.Single(targets, target => target.Text == "fi");
        var nextLine = targets.First(target => target.Text == "n");
        Assert.True(firstLine.Rect.Y < nextLine.Rect.Y);
        TripleClick(firstLine.Rect);
        Assert.Equal("first word!", shell.Controller.SelectedText(tab.Id));

        TripleClick(nextLine.Rect);
        Assert.Equal("next line", shell.Controller.SelectedText(tab.Id));

        void TripleClick(PageLinkRect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            for (byte clickCount = 1; clickCount <= 3; clickCount++)
            {
                shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y, clickCount));
                shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y, clickCount));
            }
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftArrowExtendsTextSelectionByShapingCluster(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first</p><p>second</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - first.Y) <= 0.5).Rect;
        Drag(first, last);
        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.Right, 0,
            (ushort)SDL.Keymod.Shift, true, false));

        Assert.Equal("first\ns", shell.Controller.SelectedText(tab.Id));
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.Left, 0,
            (ushort)SDL.Keymod.Shift, true, false));
        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));

        void Drag(PageLinkRect start, PageLinkRect end)
        {
            var x1 = (float)(start.X + start.Width / 2);
            var y1 = (float)(ShellChrome.Height + start.Y + start.Height / 2);
            var x2 = (float)(end.X + end.Width / 2);
            var y2 = (float)(ShellChrome.Height + end.Y + end.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x1, y1));
            shell.Dispatch(new PointerMoved(system.Items[0].Id, x2, y2));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x2, y2));
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftArrowDoesNotExtendPageSelectionWhenFormControlHasFocus(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first</p><input type=checkbox><p>second</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - first.Y) <= 0.5).Rect;
        Drag(first, last);
        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.Right, 0,
            (ushort)SDL.Keymod.Shift, true, false));

        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));

        void Drag(PageLinkRect start, PageLinkRect end)
        {
            var x1 = (float)(start.X + start.Width / 2);
            var y1 = (float)(ShellChrome.Height + start.Y + start.Height / 2);
            var x2 = (float)(end.X + end.Width / 2);
            var y2 = (float)(ShellChrome.Height + end.Y + end.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x1, y1));
            shell.Dispatch(new PointerMoved(system.Items[0].Id, x2, y2));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x2, y2));
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftHomeAndEndExtendSelectionToCurrentLineBoundaries(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first second</p><p>third</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var secondC = Assert.Single(page.TextTargets, target => target.Text == "c");
        Click(secondC.Rect);
        Assert.Equal("c", shell.Controller.SelectedText(tab.Id));
        Key(SDL.Scancode.End);
        Assert.Equal("cond", shell.Controller.SelectedText(tab.Id));
        Key(SDL.Scancode.Home);
        Assert.Equal("first sec", shell.Controller.SelectedText(tab.Id));

        void Click(PageLinkRect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y));
        }
        void Key(SDL.Scancode scan)
        {
            shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)scan, 0,
                (ushort)SDL.Keymod.Shift, true, false));
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftVerticalArrowsExtendSelectionToAdjacentVisualLine(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first line</p><p>second line</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var firstI = page.TextTargets.First(target => target.Text == "i");
        Click(firstI.Rect);
        Assert.Equal("i", shell.Controller.SelectedText(tab.Id));
        Key(SDL.Scancode.Down);
        Assert.Contains('\n', shell.Controller.SelectedText(tab.Id));
        Key(SDL.Scancode.Up);
        Assert.DoesNotContain('\n', shell.Controller.SelectedText(tab.Id));

        void Click(PageLinkRect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y));
        }
        void Key(SDL.Scancode scan) =>
            shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)scan, 0,
                (ushort)SDL.Keymod.Shift, true, false));
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftClickExtendsClusterSelectionInsteadOfActivatingLink(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><p>first</p>" +
            "<p><a href='#destination'>second</a></p><p id=destination>done</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var first = page.TextTargets[0].Rect;
        var firstLineEnd = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - first.Y) <= 0.5).Rect;
        var linkRect = Assert.Single(page.LinkTargets).Rects[0];
        var second = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - linkRect.Y) <= 0.5).Rect;
        Drag(first, firstLineEnd);
        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.A, 0,
            (ushort)SDL.Keymod.Shift, true, false));
        Click(second);
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.A, 0, 0, false, false));

        Assert.Equal("first\nsecond", shell.Controller.SelectedText(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.Null(tab.Error);

        void Drag(PageLinkRect start, PageLinkRect end)
        {
            var x1 = (float)(start.X + start.Width / 2);
            var y1 = (float)(ShellChrome.Height + start.Y + start.Height / 2);
            var x2 = (float)(end.X + end.Width / 2);
            var y2 = (float)(ShellChrome.Height + end.Y + end.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x1, y1));
            shell.Dispatch(new PointerMoved(system.Items[0].Id, x2, y2));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x2, y2));
        }
        void Click(PageLinkRect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(ShellChrome.Height + rect.Y + rect.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x, y));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x, y));
        }

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftClickMissKeepsExistingClusterSelection(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}div{height:140px}</style><p>first</p><div></div><p>second</p>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var page = shell.Controller.Page(tab.Id)!;
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets.Last(target => Math.Abs(target.Rect.Y - first.Y) <= 0.5).Rect;
        Drag(first, last);
        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));

        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.A, 0,
            (ushort)SDL.Keymod.Shift, true, false));
        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, 40, (float)(ShellChrome.Height + 90)));
        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, 40, (float)(ShellChrome.Height + 90)));
        shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.A, 0, 0, false, false));

        Assert.Equal("first", shell.Controller.SelectedText(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.Null(tab.Error);

        void Drag(PageLinkRect start, PageLinkRect end)
        {
            var x1 = (float)(start.X + start.Width / 2);
            var y1 = (float)(ShellChrome.Height + start.Y + start.Height / 2);
            var x2 = (float)(end.X + end.Width / 2);
            var y2 = (float)(ShellChrome.Height + end.Y + end.Height / 2);
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, x1, y1));
            shell.Dispatch(new PointerMoved(system.Items[0].Id, x2, y2));
            shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, x2, y2));
        }

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Fact]
    public void UnsupportedFormSubmissionIsReportedInTheTabWithoutNavigation()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0} input{display:block}</style><form method=post><input name=q></form>"));
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading);
        var page = shell.Controller.Page(tab.Id);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Key(SDL.Scancode.Return);
        Assert.Contains("Unsupported form method: post", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CtrlPageUpAndPageDownCycleTabs(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var first = window.ActiveTab!;
        var second = shell.Controller.CreateTab(window.Id);

        Key(SDL.Scancode.Pageup);
        Assert.Equal(second.Id, window.ActiveTabId);
        Key(SDL.Scancode.Pageup, SDL.Keymod.Ctrl);
        Assert.Equal(first.Id, window.ActiveTabId);
        Key(SDL.Scancode.Pagedown, SDL.Keymod.Ctrl);
        Assert.Equal(second.Id, window.ActiveTabId);

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None)
        {
            shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyboardFocusTraversesChromeAndRestoresIndependentTabFocus(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var first = window.ActiveTab!;
        Load(first);
        Key(SDL.Scancode.Tab);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(-1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Key(SDL.Scancode.Tab);
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        var second = shell.Controller.CreateTab(window.Id);
        Load(second);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(second.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(first.Id, window.ActiveTabId);
        Assert.True(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(second.Id, window.ActiveTabId);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(second.Id));
        Assert.True(shell.Controller.PageHasFocus(second.Id));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, 499, 300));
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(second.Id));

        void Load(BrowserTab tab)
        {
            shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
                "<!doctype html><style>*{margin:0}</style><a href='#one'>one</a> <a href='#two'>two</a>"));
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabTraversalHonorsTabIndexForSupportedPageTargets(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0} a,input{display:block}</style>" +
            "<a href='#default' tabindex='invalid'>default</a>" +
            "<input name=zero tabindex=0>" +
            "<a href='#two' tabindex=2>two</a>" +
            "<input name=one tabindex=1>" +
            "<a href='#negative' tabindex=-1>negative</a>" +
            "<a href='#one' tabindex=1>one</a>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);

        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.Equal(5, shell.Controller.PageFocusCount(tab.Id));

        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Key(SDL.Scancode.Tab);
        Assert.Equal(3, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Key(SDL.Scancode.Tab);
        Assert.False(shell.Controller.PageHasFocus(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
    }

    [Fact]
    public void TabIndexOutsideSupportedRangeFailsNavigationExplicitly()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><a href='#target' tabindex=2147483648>oversized</a>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);

        Assert.False(tab.IsLoading);
        Assert.Contains("tabindex exceeds the supported signed 32-bit range", tab.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TabTraversesDrawnChromeBeforeLinksAndNoLinksWrapInChrome()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var targets = chrome.Render(window, shell.Controller.Page(tab.Id), native.Size, native.Density).Targets;
        Assert.Empty(shell.Controller.Page(tab.Id)!.LinkTargets);
        foreach (var target in targets)
        {
            Key(SDL.Keymod.None);
            shell.Tick();
            Assert.False(shell.Controller.PageHasFocus(tab.Id));
            Assert.Equal(target.Action == ChromeAction.Address, native.TextInput);
            var offset = (int)target.Bounds.Y * native.Size.Width * 4 + (int)target.Bounds.X * 4;
            Assert.Equal(new byte[] { 128, 96, 64, 255 }, native.Pixels!.Skip(offset).Take(4));
        }
        Key(SDL.Keymod.None);
        Assert.False(native.TextInput);
        Key(SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Assert.Single(tab.History.Entries);

        void Key(SDL.Keymod mod) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Tab, 0, (ushort)mod, true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrimaryLinkClickRequiresReleaseOverTheSameAnchor(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='#one' id=one>first</a> " +
            "<a href='#two' id=two>second</a>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var links = shell.Controller.Page(tab.Id)!.LinkTargets;
        var first = links[0].Rects[0];
        var second = links[1].Rects[0];
        var native = system.Items[0];
        var firstX = (float)(first.X + first.Width / 2);
        var firstY = (float)(ShellChrome.Height + first.Y + first.Height / 2);
        var secondX = (float)(second.X + second.Width / 2);
        var secondY = (float)(ShellChrome.Height + second.Y + second.Height / 2);

        Click(firstX, firstY, 500, 300);
        Assert.Single(tab.History.Entries);
        Click(firstX, firstY, secondX, secondY);
        Assert.Single(tab.History.Entries);
        Click(firstX, firstY, firstX, firstY);
        Assert.Equal("#one", new Uri(tab.History.Current!.Href).Fragment);
        Assert.Equal(2, tab.History.Entries.Count);
        Assert.Null(tab.Error);

        void Click(float downX, float downY, float upX, float upY)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, downX, downY));
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, upX, upY));
        }

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(condition());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonLinkPrimaryClickClearsFocusedPageTarget(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}div{height:160px}</style><a href='#one'>first</a><div></div>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        shell.Controller.FocusLink(tab.Id, 0);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));

        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, 50, (float)(ShellChrome.Height + 120)));

        Assert.True(shell.Controller.PageHasFocus(tab.Id));
        Assert.Equal(-1, shell.Controller.FocusedLinkIndex(tab.Id));
        Assert.Equal(-1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.Null(tab.Error);

        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(condition());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedLinkClickReportsVisibleErrorWithoutReplacingPage(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='javascript:alert(1)'>link</a>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (tab.IsLoading && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        var old = shell.Controller.Page(tab.Id)!;
        var link = Assert.Single(old.LinkTargets);
        var clickX = (float)(link.X + 1);
        var clickY = (float)(ShellChrome.Height + link.Y + 1);
        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true, clickX, clickY));
        Assert.Null(tab.Error);
        Assert.Same(old, shell.Controller.Page(tab.Id));
        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, false, clickX, clickY));
        Assert.Contains("Unsupported link URL scheme: javascript:", tab.Error);
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.False(tab.IsLoading);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 1.25)]
    [InlineData(true, 1.25)]
    public void PrimaryPageClicksUseLogicalCoordinatesAndBlockDataDestinations(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>*{margin:0}</style><title>Clicked</title><p>destination</p>");
        var html = $"<!doctype html><style>*{{margin:0}}div{{height:800px}}</style><div></div><a href='{destination}'>visible link</a>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Controller.Scroll(tab.Id, double.MaxValue);
        Wait(() => shell.Controller.Page(tab.Id)!.LinkTargets.Any(link => link.Rects.Count > 0));
        var old = shell.Controller.Page(tab.Id)!;
        var link = old.LinkTargets[0];
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(link.Y + link.Height / 2 + ShellChrome.Height);
        shell.Dispatch(new PointerButtonChanged(native.Id, 3, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 2, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, 110));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, -1, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, 500, y));
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.False(tab.IsLoading);
        Assert.Single(tab.History.Entries);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Equal("data:text/html," + Uri.EscapeDataString(html), tab.History.Current!.Href);
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.NotEqual(0, shell.Controller.ScrollY(tab.Id));
        Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready() && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CtrlBoundaryKeysDoNotScrollWhileAFormControlHasFocus(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}div{height:2000px}</style><input type=checkbox><div></div>"));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        Key(SDL.Scancode.End, SDL.Keymod.Ctrl);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Home, SDL.Keymod.Ctrl);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod)
        {
            shell.Dispatch(new KeyChanged(system.Items[0].Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (!ready() && DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerticalScrollbarCanJumpAndDrag(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var html = "<!doctype html><style>*{margin:0}div{height:2000px;background-color:red}</style><div></div>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var x = native.Size.Width - 5;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, 250));
        Wait(() => !tab.IsLoading);
        Assert.True(shell.Controller.ScrollY(tab.Id) > 0);
        Assert.NotEqual(new byte[] { 255, 0, 0, 255 }, native.Pixels!.AsSpan(150 * native.Size.Width * 4 + x * 4, 4).ToArray());

        shell.Dispatch(new PointerMoved(native.Id, x, native.Size.Height - 1));
        Wait(() => !tab.IsLoading);
        var maximum = shell.Controller.Page(tab.Id)!.ScrollHeight - ShellChrome.Viewport(native.Size, 1)!.Value.Height;
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, native.Size.Height - 1));

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready() && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WheelArrowAndPageKeysScrollActivePageButNotChromeOrEditableFields(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var html = "<!doctype html><style>*{margin:0}div{height:2000px;background-color:red}</style><div></div>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Dispatch(new PointerMoved(native.Id, 10, 200));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Wait(() => !tab.IsLoading);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerMoved(native.Id, 220, 70));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        shell.Dispatch(new PointerMoved(native.Id, 10, 200));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Key(SDL.Scancode.Pagedown);
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Home, SDL.Keymod.Ctrl);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Escape);
        Key(SDL.Scancode.Down);
        Assert.Equal(96, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Up);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Pagedown);
        Assert.True(shell.Controller.ScrollY(tab.Id) > 48);
        Key(SDL.Scancode.End, SDL.Keymod.Ctrl);
        var maximum = 2000 - ShellChrome.Viewport(native.Size, 1)!.Value.Height;
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Home, SDL.Keymod.Ctrl);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Pagedown);
        Assert.True(shell.Controller.ScrollY(tab.Id) > 0);
        Key(SDL.Scancode.End);
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, float.MaxValue));
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Pageup);
        Assert.True(shell.Controller.ScrollY(tab.Id) < maximum);
        Key(SDL.Scancode.Home);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, float.NaN));
        Assert.Contains("finite", tab.Error);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            do { shell.Tick(); Thread.Sleep(10); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static BrowserWindow OpenWindowAndApproveInitialNavigation(DevelopmentShell shell, Windows system, string url)
    {
        var window = shell.OpenWindow(url);
        var tab = window.ActiveTab ?? throw new InvalidOperationException("The opened window has no active tab.");
        var native = system.Items[0];
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((tab.IsLoading || shell.Controller.Page(tab.Id) is null) && DateTime.UtcNow < deadline)
        {
            shell.Tick();
            if (shell.HasPendingNavigationConfirmation(tab.Id))
            {
                shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
            }
            Thread.Sleep(5);
        }
        Assert.True(!tab.IsLoading && shell.Controller.Page(tab.Id) is not null,
            $"{tab.Error} | {tab.Status} | {tab.AddressText}");
        Assert.Null(tab.Error);
        return window;
    }

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
        [SuppressMessage("Sonar", "S1144:Unused private types or members should be removed", Justification = "HttpClient dispatches this response fixture through the HttpMessageHandler override.")]
        [SuppressMessage("Sonar", "S1172:Unused method parameters should be removed", Justification = "HttpMessageHandler requires a cancellation token; this deterministic fixture completes synchronously.")]
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
        Assert.True(shell.HasPendingNavigationConfirmation(tab.Id));
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        Key(SDL.Scancode.Return);
        WaitForNavigation();
        Assert.Equal(2, tab.History.Entries.Count);
        var at = 120 * native.Size.Width * 4;
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, native.Pixels.AsSpan(at, 4).ToArray());
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
        void WaitForNavigation()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }
    [Fact]
    public void TextareaViewportFollowsCaretBeyondItsVisibleRows()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style>" +
            "<form><textarea>one\ntwo\nthree\nfour\nfive</textarea></form>"));
        Wait();
        var control = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        Assert.True(shell.Controller.FocusControl(tab.Id, 0),
            $"Controls: {shell.Controller.Page(tab.Id)!.FormControls.Count}, rectangle: {shell.Controller.Page(tab.Id)!.FormControls.FirstOrDefault()?.Rect}");
        shell.Tick();
        var before = Capture();
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Up);
        shell.Tick();
        var after = Capture();
        Assert.NotEqual(before, after);
        Assert.Equal(3, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerMoved(native.Id, (float)(control.X + 5), (float)(ShellChrome.Height + control.Y + 5)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -1));
        Assert.Equal(0, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Assert.Equal(3, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        byte[] Capture()
        {
            var pixels = native.Pixels ?? throw new InvalidOperationException("Shell pixels were not presented.");
            var left = (int)Math.Floor(control.X * native.Density);
            var top = (int)Math.Ceiling(ShellChrome.Height * native.Density) + (int)Math.Floor(control.Y * native.Density);
            var width = (int)Math.Ceiling(control.Width * native.Density);
            var height = (int)Math.Ceiling(control.Height * native.Density);
            var result = new byte[height * width * 4];
            for (var row = 0; row < height; row++)
            {
                pixels.AsSpan((top + row) * native.Size.Width * 4 + left * 4, width * 4)
                    .CopyTo(result.AsSpan(row * width * 4));
            }
            return result;
        }
        void Key(SDL.Scancode code) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void TextareaSoftWrapCreatesVisualCaretRows()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><textarea cols=5 rows=2>abcdefghij\nz</textarea>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Tick();
        Assert.True(shell.Controller.ScrollTextareaAt(tab.Id, rect.X + 2, rect.Y + 2, 300));
        Assert.True(shell.Controller.TextareaFirstLine(tab.Id, 0) > 0);
        Assert.True(shell.Controller.ScrollTextareaAt(tab.Id, rect.X + 2, rect.Y + 2, -300));
        Assert.Equal(0, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Up);
        Assert.InRange(shell.Controller.FormControlCaret(tab.Id), 1, 10);
        Key(SDL.Scancode.End);
        Assert.Equal(10, shell.Controller.FormControlCaret(tab.Id));
        Key(SDL.Scancode.Down);
        Assert.Equal(12, shell.Controller.FormControlCaret(tab.Id));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MiddleClickClosesBackgroundTabWithoutActivatingIt()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var active = window.ActiveTab!;
        var background = shell.Controller.CreateTab(window.Id, activate: false);
        shell.Controller.Navigate(background.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Background</title>"));
        Wait(background);
        var presentedFrames = shell.PresentedFrames;

        system.Events.Enqueue(new PointerButtonChanged(system.Items[0].Id, 2, true, 230, 40));
        shell.Tick();

        Assert.Single(window.Tabs);
        Assert.Equal(active.Id, window.ActiveTabId);
        Assert.False(shell.Controller.Session.Contains(background.Id));
        Assert.True(shell.PresentedFrames > presentedFrames);

        void Wait(BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void MiddleClickClosingActiveTabSelectsFollowingTabAtItsFormerPosition()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var first = window.ActiveTab!;
        var second = shell.Controller.CreateTab(window.Id, activate: false);
        shell.Controller.Navigate(second.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Second</title>"));
        Wait(second);
        var third = shell.Controller.CreateTab(window.Id, activate: false);
        shell.Controller.Navigate(third.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Third</title>"));
        Wait(third);

        system.Events.Enqueue(new PointerButtonChanged(system.Items[0].Id, 1, true, 230, 40));
        shell.Tick();
        Assert.Equal(second.Id, window.ActiveTabId);
        var presentedFrames = shell.PresentedFrames;

        system.Events.Enqueue(new PointerButtonChanged(system.Items[0].Id, 2, true, 230, 40));
        shell.Tick();

        Assert.Equal([first.Id, third.Id], window.Tabs.Select(tab => tab.Id));
        Assert.Equal(third.Id, window.ActiveTabId);
        Assert.True(shell.PresentedFrames > presentedFrames);

        void Wait(BrowserTab tab)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
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
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftSelectionsCopyAndReplaceTextAcrossShellEditors(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><input name=q value=hello>" +
            "<textarea name=t>alpha\nbeta\nomega</textarea></form>"));
        Wait();

        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Key(SDL.Scancode.Left, SDL.Keymod.Shift);
        Key(SDL.Scancode.Left, SDL.Keymod.Shift);
        Assert.Equal((3, 5), shell.Controller.FormControlSelectionRange(tab.Id));
        Assert.Equal("lo", shell.Controller.FormControlSelectedText(tab.Id));
        shell.Tick();
        Assert.True(HasSelectionColor(shell.Controller.Page(tab.Id)!.FormControls[0].Rect!));

        Key(SDL.Scancode.C, SDL.Keymod.Ctrl);
        Assert.Equal("lo", native.GetClipboardText());
        native.SetClipboardText("X");
        Key(SDL.Scancode.V, SDL.Keymod.Ctrl);
        Assert.Equal("helX", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Equal("", shell.Controller.FormControlSelectedText(tab.Id));

        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Key(SDL.Scancode.Home, SDL.Keymod.Shift);
        Assert.Equal((11, 16), shell.Controller.FormControlSelectionRange(tab.Id));
        Key(SDL.Scancode.Up, SDL.Keymod.Shift);
        Assert.Equal((6, 16), shell.Controller.FormControlSelectionRange(tab.Id));
        Assert.Equal("beta\nomega", shell.Controller.FormControlSelectedText(tab.Id));
        shell.Tick();
        Assert.True(HasSelectionColor(shell.Controller.Page(tab.Id)!.FormControls[1].Rect!));
        Key(SDL.Scancode.C, SDL.Keymod.Ctrl);
        Assert.Equal("beta\nomega", native.GetClipboardText());
        native.SetClipboardText("X");
        Key(SDL.Scancode.V, SDL.Keymod.Ctrl);
        Assert.Equal("alpha\nX", shell.Controller.FormControlValue(tab.Id, 1));

        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        shell.Dispatch(new TextEntered(native.Id, "abcdef"));
        Key(SDL.Scancode.Left, SDL.Keymod.Shift);
        Key(SDL.Scancode.Left, SDL.Keymod.Shift);
        shell.Tick();
        Assert.True(HasAddressSelectionColor());
        Key(SDL.Scancode.C, SDL.Keymod.Ctrl);
        Assert.Equal("ef", native.GetClipboardText());
        native.SetClipboardText("XY");
        Key(SDL.Scancode.V, SDL.Keymod.Ctrl);
        Assert.Equal("abcdXY", tab.AddressText);

        void Key(SDL.Scancode code, SDL.Keymod modifiers = 0) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, (ushort)modifiers, true, false));
        bool HasSelectionColor(PageLinkRect rect)
        {
            return ScanSelectionColor((int)Math.Floor(rect.X), (int)Math.Floor(ShellChrome.Height + rect.Y),
                (int)Math.Ceiling(rect.X + rect.Width), (int)Math.Ceiling(ShellChrome.Height + rect.Y + rect.Height));
        }
        bool HasAddressSelectionColor() =>
            ScanSelectionColor(196, 69, native.PixelSize.Width, 87);
        bool ScanSelectionColor(int left, int top, int right, int bottom)
        {
            var pixels = native.Pixels ?? throw new InvalidOperationException("Shell pixels were not presented.");
            left = Math.Max(0, left);
            right = Math.Min(native.PixelSize.Width, right);
            top = Math.Max(0, top);
            bottom = Math.Min(native.PixelSize.Height, bottom);
            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    var offset = (y * native.PixelSize.Width + x) * 4;
                    if (pixels[offset] == 249 && pixels[offset + 1] == 213
                        && pixels[offset + 2] == 176 && pixels[offset + 3] == 255) { return true; }
                }
            }
            return false;
        }
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
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
    public void ControlVPastesClipboardTextIntoSelectedAddressAndNavigates()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait();

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Pasted address</title><p>done</p>");
        native.SetClipboardText(destination);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.V, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal(destination, tab.AddressText);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        WaitForPrompt();
        Assert.Equal("Allow data URL navigation",
            shell.PendingNavigationConfirmationMessage(tab.Id));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait();
        Assert.Equal("Pasted address", tab.Title);

        void WaitForPrompt()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); }
            while (!shell.HasPendingNavigationConfirmation(tab.Id) && DateTime.UtcNow < deadline);
            Assert.True(shell.HasPendingNavigationConfirmation(tab.Id));
        }
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void AddressEditorRangeOperationsPreserveScalarBoundaries()
    {
        var editor = new AddressEditor();
        editor.Reset("a💩b");
        editor.Left();
        editor.Left(extendSelection: true);
        Assert.Equal((1, 3), (editor.SelectionStart, editor.SelectionEnd));
        Assert.Equal("💩", editor.SelectedText);

        editor.Right();
        Assert.Equal(3, editor.Caret);
        Assert.False(editor.HasSelection);
        editor.Left();
        editor.Right(extendSelection: true);
        Assert.Equal("axb", editor.Insert("x", 16));
        Assert.Equal("axb", editor.Text);

        editor.Reset("abc");
        editor.Home();
        editor.Right(extendSelection: true);
        Assert.Equal("bc", editor.Delete());
        editor.Right(extendSelection: true);
        Assert.Equal("b", editor.SelectedText);
        Assert.Equal("c", editor.Backspace());
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
        Assert.False(home.IsSameOrigin(tab.Origin));
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
        [SuppressMessage("Sonar", "S2325:Methods and properties that don't access instance data should be static", Justification = "IWindowSystem requires an instance backend identifier.")]
        public string Backend => "fake";
        [SuppressMessage("Sonar", "S1186:Methods should not be empty", Justification = "The IWindowSystem contract requires a quit subscription point; this fixture drives only explicit window events.")]
        event Action? IWindowSystem.QuitRequested
        {
            add { }
            remove { }
        }
        public IPlatformWindow CreateWindow(WindowOptions options)
        {
            var window = new Window(new((uint)Items.Count + 1), new(options.Width, options.Height));
            Items.Add(window); return window;
        }
        public void PumpEvents()
        {
            while (Events.TryDequeue(out var input)) { Items.Single(w => w.Id == input.Window).Dispatch(input); }
        }
        public void Dispose() { foreach (var window in Items) { window.Dispose(); } }
    }
    private sealed class Window(WindowId id, PixelSize size) : IPlatformWindow, IPixelSurface
    {
        public WindowId Id => id;
        internal PixelSize Size { get; set; } = size;
        internal float Density { get; set; } = 1;
        public PixelSize LogicalSize => new((int)(Size.Width / Density), (int)(Size.Height / Density));
        public PixelSize PixelSize => Size;
        [SuppressMessage("Sonar", "S2325:Methods and properties that don't access instance data should be static", Justification = "IPlatformWindow requires an instance display-scale property.")]
        public float DisplayScale => 1;
        public float PixelDensity => Density;
        public IPixelSurface Surface => this;
        [SuppressMessage("Sonar", "S3264:Events should not have empty accessors", Justification = "IPlatformWindow requires this event; Dispatch invokes subscribed handlers for the fixture's queued platform events.")]
        public event Action<WindowEvent>? EventReceived;
        internal bool TextInput { get; private set; }
        internal bool Disposed { get; private set; }
        internal byte[]? Pixels { get; private set; }
        internal string Title { get; private set; } = "";
        internal void Dispatch(WindowEvent input) => EventReceived?.Invoke(input);
        public void SetTitle(string title) => Title = title;
        public void SetTextInput(bool enabled) => TextInput = enabled;
        internal string ClipboardText { get; private set; } = "";
        public void SetClipboardText(string text) => ClipboardText = text;
        public string GetClipboardText() => ClipboardText;
        public void Present(ReadOnlySpan<byte> pixels, PixelSize frameSize, int stride)
        {
            Assert.Equal(Size, frameSize);
            PixelBuffer.Validate(pixels.Length, frameSize, stride);
            Pixels = pixels.ToArray();
        }
        public void Dispose() { Disposed = true; EventReceived = null; }
    }
    private sealed class NavigationHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
    };
    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.Found)
    {
        Headers = { Location = new Uri(location) }
    };
}
