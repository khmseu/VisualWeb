using VisualWeb.Core.Url;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Paint;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ControllerTests
{
    private static readonly PageViewport Viewport = new(20, 20, 1);
    private static LoadedPage Document(string path) => new(BrowserUrl.Parse("https://example.com/" + path),
        "<!doctype html>", 200, []);
    private static BrowserPage Blank(PageViewport viewport)
    {
        using var fonts = new PaintFontRegistry();
        return new(CpuRasterizer.Render(new(viewport.Width, viewport.Height, []), fonts, viewport.Scale),
            "Title", "Ready");
    }
    [Fact]
    public void OpaqueOriginsAreFreshForReloadAndSeparateTabsButRetainedForRepaint()
    {
        var sources = new List<Source>();
        var renderers = new List<Renderer>();
        using var controller = new BrowserController(
            () => { var source = new Source(); sources.Add(source); return source; },
            () => { var renderer = new Renderer(); renderers.Add(renderer); return renderer; });
        var window = controller.Session.CreateWindow();
        var firstTab = controller.CreateTab(window.Id);
        var secondTab = controller.CreateTab(window.Id);
        var url = BrowserUrl.Parse("data:text/html,test");
        var firstDocument = new LoadedPage(url, "<!doctype html>", 200, []);
        controller.Navigate(firstTab.Id, url.Href);
        sources[0].Requests[0].Completion.SetResult(firstDocument);
        controller.Pump(_ => Viewport);

        controller.Resize(firstTab.Id, new(30, 20, 1));
        Assert.Same(firstDocument, Assert.Single(renderers[0].RetainedPages));
        Assert.Same(firstDocument.Origin, renderers[0].RetainedPages[0].Origin);
        Assert.Single(sources[0].Requests);

        var secondDocument = new LoadedPage(url, "<!doctype html>", 200, []);
        controller.Navigate(secondTab.Id, url.Href);
        sources[1].Requests[0].Completion.SetResult(secondDocument);
        controller.Pump(_ => Viewport);
        Assert.Same(secondDocument, Assert.Single(renderers[1].Pages));
        Assert.False(firstDocument.Origin.IsSameOrigin(secondDocument.Origin));

        var reloadedDocument = new LoadedPage(url, "<!doctype html>", 200, []);
        controller.Reload(firstTab.Id);
        sources[0].Requests[1].Completion.SetResult(reloadedDocument);
        controller.Pump(_ => Viewport);
        Assert.Same(reloadedDocument, renderers[0].Pages[^1]);
        Assert.False(firstDocument.Origin.IsSameOrigin(reloadedDocument.Origin));
        Assert.False(secondDocument.Origin.IsSameOrigin(reloadedDocument.Origin));
        controller.Resize(firstTab.Id, new(40, 20, 1));
        Assert.Same(reloadedDocument, renderers[0].RetainedPages[^1]);
        Assert.Same(reloadedDocument.Origin, renderers[0].RetainedPages[^1].Origin);
    }
    [Fact]
    public void CommittedOriginStartsNullAndChangesOnlyWithPublishedTupleNavigations()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        Assert.Null(tab.Origin);
        Assert.False(typeof(BrowserTab).GetProperty(nameof(BrowserTab.Origin))!.SetMethod!.IsPublic);

        controller.Navigate(tab.Id, "https://first.example/a");
        Assert.Null(tab.Origin);
        source.Requests[^1].Completion.SetException(new PageNavigationException("Load failed"));
        controller.Pump(_ => Viewport);
        Assert.Null(tab.Origin);
        controller.Navigate(tab.Id, "https://first.example/a");
        source.Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://first.example/a"), "", 200, []));
        renderer.Fail = true;
        controller.Pump(_ => Viewport);
        Assert.Null(tab.Origin);
        Assert.Null(controller.Page(tab.Id));

        renderer.Fail = false;
        var first = Navigate("https://first.example/a", "https://first.example:443/redirected");
        Assert.Same(first.Origin, tab.Origin);
        Assert.Equal("https://first.example", tab.Origin!.Serialize());
        var sameOrigin = Navigate("https://first.example/b", "https://FIRST.example/c");
        Assert.Same(sameOrigin.Origin, tab.Origin);
        Assert.True(first.Origin.IsSameOrigin(tab.Origin));
        var crossOrigin = Navigate("https://first.example/d", "https://second.example:8443/e");
        Assert.Same(crossOrigin.Origin, tab.Origin);
        Assert.Equal("https://second.example:8443", tab.Origin!.Serialize());
        Assert.False(first.Origin.IsSameOrigin(tab.Origin));

        controller.Navigate(tab.Id, "https://third.example/");
        source.Requests[^1].Completion.SetException(new IOException("Load failed"));
        controller.Pump(_ => Viewport);
        Assert.Same(crossOrigin.Origin, tab.Origin);
        controller.Navigate(tab.Id, "https://third.example/");
        source.Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://third.example/"), "", 200, []));
        renderer.Fail = true;
        controller.Pump(_ => Viewport);
        Assert.Same(crossOrigin.Origin, tab.Origin);
        Assert.Equal("https://second.example:8443/e", tab.History.Current!.Href);
        controller.Back(tab.Id);
        controller.Pump(_ => Viewport);
        Assert.Same(crossOrigin.Origin, tab.Origin);
        renderer.Fail = false;
        controller.Back(tab.Id);
        source.Requests[^1].Completion.SetResult(sameOrigin);
        controller.Pump(_ => Viewport);
        Assert.Same(sameOrigin.Origin, tab.Origin);
        LoadedPage Navigate(string address, string final)
        {
            var page = new LoadedPage(BrowserUrl.Parse(final), "", 200, []);
            controller.Navigate(tab.Id, address);
            source.Requests[^1].Completion.SetResult(page);
            controller.Pump(_ => Viewport);
            Assert.Null(tab.Error);
            return page;
        }
    }
    [Fact]
    public void RetainedRepaintAndTabMoveKeepTheSameOpaqueOriginWhileReloadAndFileDocumentsReplaceIt()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        var url = BrowserUrl.Parse("data:text/html,test");
        var document = new LoadedPage(url, "", 200, []);
        controller.Navigate(tab.Id, url.Href);
        source.Requests[^1].Completion.SetResult(document);
        controller.Pump(_ => Viewport);
        var origin = tab.Origin;
        Assert.NotNull(origin);
        Assert.True(origin.IsOpaque);
        Assert.Same(document.Origin, origin);

        controller.Resize(tab.Id, new(30, 20, 1));
        Assert.Same(origin, tab.Origin);
        renderer.Fail = true;
        controller.Resize(tab.Id, new(40, 20, 1));
        Assert.Contains("Resize", tab.Error);
        Assert.Same(origin, tab.Origin);
        renderer.Fail = false;
        controller.Resize(tab.Id, new(50, 20, 1));
        Assert.Same(origin, tab.Origin);
        var other = controller.Session.CreateWindow();
        controller.Session.MoveTab(tab.Id, other.Id);
        Assert.Same(origin, controller.Session.Tab(tab.Id).Origin);
        controller.Pump(_ => Viewport);
        Assert.Same(origin, tab.Origin);

        controller.Reload(tab.Id);
        Assert.Same(origin, tab.Origin);
        var reloaded = new LoadedPage(url, "", 200, []);
        source.Requests[^1].Completion.SetResult(reloaded);
        controller.Pump(_ => Viewport);
        Assert.Same(reloaded.Origin, tab.Origin);
        Assert.False(origin.IsSameOrigin(tab.Origin));

        var file = BrowserUrl.Parse("file:///document.html");
        var fileDocument = new LoadedPage(file, "", 200, []);
        controller.Navigate(tab.Id, file.Href);
        source.Requests[^1].Completion.SetResult(fileDocument);
        controller.Pump(_ => Viewport);
        Assert.Same(fileDocument.Origin, tab.Origin);
        Assert.True(tab.Origin!.IsOpaque);
        controller.Reload(tab.Id);
        var fileReloaded = new LoadedPage(file, "", 200, []);
        source.Requests[^1].Completion.SetResult(fileReloaded);
        controller.Pump(_ => Viewport);
        Assert.Same(fileReloaded.Origin, tab.Origin);
        Assert.False(fileDocument.Origin.IsSameOrigin(tab.Origin));
    }
    [Fact]
    public void RejectedDocumentCommitLeavesOriginHistoryAndPublishedPageUntouched()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var first = new LoadedPage(BrowserUrl.Parse("https://first.example/"), "", 200, []);
        controller.Navigate(tab.Id, first.Url.Href);
        source.Requests[^1].Completion.SetResult(first);
        controller.Pump(_ => Viewport);
        var page = controller.Page(tab.Id);
        Assert.Equal([first.DocumentId], renderer.Committed);

        renderer.RejectCommit = true;
        controller.Navigate(tab.Id, "https://second.example/");
        source.Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://second.example/"), "", 200, []));
        controller.Pump(_ => Viewport);
        Assert.Contains("rejected", tab.Error);
        Assert.False(tab.IsLoading);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Same(page, controller.Page(tab.Id));
        Assert.Equal([first.Url.Href], tab.History.Entries.Select(entry => entry.Href));

        controller.Resize(tab.Id, new(30, 20, 1));
        Assert.Contains("rejected", tab.Error);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Single(renderer.Committed);
        renderer.RejectCommit = false;
        controller.Resize(tab.Id, new(40, 20, 1));
        Assert.Null(tab.Error);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Equal([first.DocumentId, first.DocumentId], renderer.Committed);
    }
    [Fact]
    public void LegacyMetaCharsetPageIsDecodedBeforeRendering()
    {
        var renderer = new Renderer();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(_ =>
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(System.Text.Encoding.Latin1.GetBytes("<!doctype html><meta charset=windows-1252><p>\u0080</p>"))
            };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html");
            return response;
        })), () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        PumpUntilIdle(controller, tab, () => controller.Navigate(tab.Id, "https://legacy.example/"));
        Assert.Null(tab.Error);
        var page = Assert.Single(renderer.Pages);
        Assert.EndsWith("<p>\u20AC</p>", page.Html, StringComparison.Ordinal);
        Assert.Contains(page.Diagnostics, d => d.StartsWith("Encoding: windows-1252 from <meta charset> prescan", StringComparison.Ordinal));
    }
    [Fact]
    public void BadPortNavigationReportsExplicitErrorAndPreservesCommittedState()
    {
        var requests = new List<string>();
        var renderer = new Renderer();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("<!doctype html>") };
            if (request.RequestUri.AbsolutePath == "/redirect")
            {
                response.StatusCode = System.Net.HttpStatusCode.Found;
                response.Headers.TryAddWithoutValidation("Location", "http://first.example:6667/irc");
            }
            response.Content.Headers.Remove("Content-Type");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html");
            return response;
        })), () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        PumpUntilIdle(controller, tab, () => controller.Navigate(tab.Id, "https://first.example:8443/"));
        Assert.Null(tab.Error);
        var page = controller.Page(tab.Id);
        var origin = tab.Origin;
        Assert.NotNull(page);
        Assert.Equal("https://first.example:8443", origin!.Serialize());

        foreach (var (address, port) in new[] { ("http://first.example:25/", "25"), ("https://first.example:8443/redirect", "6667") })
        {
            PumpUntilIdle(controller, tab, () => controller.Navigate(tab.Id, address));
            Assert.Contains("bad port", tab.Error, StringComparison.Ordinal);
            Assert.Contains(port, tab.Error, StringComparison.Ordinal);
            Assert.Same(origin, tab.Origin);
            Assert.Same(page, controller.Page(tab.Id));
            Assert.Equal(["https://first.example:8443/"], tab.History.Entries.Select(entry => entry.Href));
        }

        Assert.Equal(["https://first.example:8443/", "https://first.example:8443/redirect"], requests);
        Assert.Single(renderer.Committed);
    }
    private static void PumpUntilIdle(BrowserController controller, BrowserTab tab, Action start)
    {
        start();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (tab.IsLoading)
        {
            Assert.True(DateTime.UtcNow < deadline, "Navigation did not finish.");
            controller.Pump(_ => Viewport);
            Thread.Sleep(1);
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
    [Fact]
    public void NavigationOnlyCommitsAfterSuccessfulLoadAndRender()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var window = controller.Session.CreateWindow(); var tab = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        Assert.True(tab.IsLoading); Assert.Empty(tab.History.Entries);
        source.Requests[0].Completion.SetResult(Document("redirect"));
        controller.Pump(_ => Viewport);
        Assert.False(tab.IsLoading);
        Assert.Equal("/redirect", tab.History.Current!.Pathname);
        var original = controller.Page(tab.Id);
        controller.Navigate(tab.Id, Document("bad").Url.Href);
        source.Requests[1].Completion.SetResult(Document("bad"));
        renderer.Fail = true;
        controller.Pump(_ => Viewport);
        Assert.Contains("Unsupported", tab.Error);
        Assert.Single(tab.History.Entries);
        Assert.Same(original, controller.Page(tab.Id));
        Assert.Equal(Document("bad").Url.Href, tab.AddressText);
    }
    [Fact]
    public void FailedBackTraversalPreservesCommittedHistoryIndexAndForwardBranch()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        Commit("a"); Commit("b"); Commit("c");
        controller.Back(tab.Id);
        source.Requests[^1].Completion.SetException(new PageNavigationException("Load failed"));
        controller.Pump(_ => Viewport);
        Assert.Equal(2, tab.History.Index);
        controller.Back(tab.Id);
        source.Requests[^1].Completion.SetResult(Document("b"));
        controller.Pump(_ => Viewport);
        Assert.Equal(1, tab.History.Index);
        Assert.True(tab.History.CanGoForward);
        controller.Reload(tab.Id);
        source.Requests[^1].Completion.SetResult(Document("b-redirect"));
        controller.Pump(_ => Viewport);
        Assert.Equal(3, tab.History.Entries.Count);
        Assert.Equal("/b-redirect", tab.History.Current!.Pathname);
        void Commit(string path)
        {
            controller.Navigate(tab.Id, Document(path).Url.Href);
            source.Requests[^1].Completion.SetResult(Document(path));
            controller.Pump(_ => Viewport);
        }
    }
    [Fact]
    public void StaleLoadsAndClosedTabsNeverPublishDocuments()
    {
        var sources = new List<Source>();
        using var controller = new BrowserController(() => { var source = new Source(); sources.Add(source); return source; }, () => new Renderer());
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        var other = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        controller.Navigate(tab.Id, Document("b").Url.Href);
        Assert.True(sources[0].Requests[0].Token.IsCancellationRequested);
        sources[0].Requests[1].Completion.SetResult(Document("b"));
        controller.Pump(_ => Viewport);
        sources[0].Requests[0].Completion.SetResult(Document("a"));
        controller.Pump(_ => Viewport);
        Assert.Equal("/b", tab.History.Current!.Pathname);
        controller.Navigate(other.Id, Document("c").Url.Href);
        controller.CloseTab(other.Id);
        sources[1].Requests[0].Completion.SetResult(Document("c"));
        controller.Pump(_ => throw new InvalidOperationException("Closed tab must not request a viewport"));
        Assert.True(controller.Session.Contains(tab.Id));
        Assert.True(sources[1].Disposed);
    }
    [Fact]
    public void MinimizedViewportDefersRenderingAndResizeDoesNotReload()
    {
        var source = new Source(); var renderer = new Renderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        source.Requests[0].Completion.SetResult(Document("a"));
        controller.Pump(_ => null);
        Assert.True(tab.IsLoading); Assert.Equal(0, renderer.Calls);
        controller.Pump(_ => Viewport);
        controller.Resize(tab.Id, new(30, 20, 1));
        controller.Resize(tab.Id, new(30, 20, 1));
        Assert.Equal(2, renderer.Calls);
        Assert.Single(source.Requests);
        renderer.Fail = true;
        controller.Resize(tab.Id, new(40, 20, 1));
        Assert.Null(controller.Page(tab.Id));
        Assert.Contains("Resize", tab.Error);
        renderer.Fail = false;
        controller.Resize(tab.Id, new(30, 20, 1));
        Assert.NotNull(controller.Page(tab.Id));
        Assert.Null(tab.Error);
    }
    [Fact]
    public void PendingAddressAndOwnershipLimitsFailExplicitly()
    {
        var source = new Source();
        using var controller = new BrowserController(() => source, () => new Renderer(), new() { MaxPendingLoads = 1, MaxAddressCharacters = 40 });
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        Assert.Throws<BrowserLimitException>(() => controller.Navigate(tab.Id, Document("b").Url.Href));
        Assert.False(source.Requests[0].Token.IsCancellationRequested);
        Assert.Throws<BrowserLimitException>(() => controller.SetAddress(tab.Id, new string('x', 41)));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { controller.Pump(_ => Viewport); }
            catch (InvalidOperationException exception) { failure = exception; }
        });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(failure);
        controller.Dispose();
        Assert.True(source.Requests[0].Token.IsCancellationRequested);
        source.Requests[0].Completion.SetCanceled(TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => controller.Pump(_ => Viewport));
    }
    [Fact]
    public void AsyncRenderDoesNotBlockPumpOrPublishAStaleNavigation()
    {
        var source = new Source(); var renderer = new AsyncRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        source.Requests[0].Completion.SetResult(Document("a"));
        controller.Pump(_ => Viewport);
        Assert.True(tab.IsLoading); Assert.Empty(tab.History.Entries);
        controller.Navigate(tab.Id, Document("b").Url.Href);
        Assert.True(renderer.Tokens[0].IsCancellationRequested);
        source.Requests[1].Completion.SetResult(Document("b"));
        controller.Pump(_ => Viewport);
        renderer.Tasks[1].SetResult(Blank(Viewport));
        controller.Pump(_ => Viewport);
        Assert.Equal("/b", tab.History.Current!.Pathname);
        renderer.Tasks[0].SetResult(Blank(Viewport));
        controller.Pump(_ => Viewport);
        Assert.Equal("/b", tab.History.Current!.Pathname);
        Assert.Single(tab.History.Entries);
    }
    [Fact]
    public void OriginCommitsOnlyAfterRenderAndStaleOrSupersededResultsCannotReplaceIt()
    {
        var source = new Source(); var renderer = new AsyncRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var first = new LoadedPage(BrowserUrl.Parse("https://first.example/"), "", 200, []);
        controller.Navigate(tab.Id, first.Url.Href);
        source.Requests[^1].Completion.SetResult(first);
        controller.Pump(_ => Viewport);
        Assert.Null(tab.Origin);
        renderer.Tasks[^1].SetResult(Blank(Viewport));
        controller.Pump(_ => Viewport);
        Assert.Same(first.Origin, tab.Origin);

        var loaded = new LoadedPage(BrowserUrl.Parse("https://loaded.example/"), "", 200, []);
        controller.Navigate(tab.Id, loaded.Url.Href);
        source.Requests[^1].Completion.SetResult(loaded);
        controller.Pump(_ => Viewport);
        Assert.Same(loaded, renderer.Pages[^1]);
        Assert.Same(first.Origin, tab.Origin);
        var loadedRender = renderer.Tasks[^1];

        var superseding = new LoadedPage(BrowserUrl.Parse("https://superseding.example/"), "", 200, []);
        controller.Navigate(tab.Id, superseding.Url.Href);
        var supersedingLoad = source.Requests[^1].Completion;
        controller.Navigate(tab.Id, "https://latest.example/");
        loadedRender.SetResult(Blank(Viewport));
        supersedingLoad.SetResult(superseding);
        controller.Pump(_ => Viewport);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Equal(2, renderer.Tasks.Count);
        source.Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://latest.example/"), "", 200, []));
        controller.Pump(_ => Viewport);
        renderer.Tasks[^1].SetException(new PageNavigationException("Render failed"));
        controller.Pump(_ => Viewport);
        Assert.Contains("Render failed", tab.Error);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Equal(first.Url.Href, tab.History.Current!.Href);

        controller.Resize(tab.Id, new(12, 12, 1));
        var staleResize = renderer.Tasks[^1];
        controller.Navigate(tab.Id, "https://after-resize.example/");
        staleResize.SetResult(Blank(new(12, 12, 1)));
        controller.Pump(_ => Viewport);
        Assert.Same(first.Origin, tab.Origin);
        source.Requests[^1].Completion.SetCanceled(TestContext.Current.CancellationToken);
        controller.Pump(_ => Viewport);
        Assert.Same(first.Origin, tab.Origin);
        Assert.Single(renderer.Committed);
    }
    [Fact]
    public void SupersededResizeAndClosedTabCannotPublishLateRenderResults()
    {
        var source = new Source(); var renderer = new AsyncRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, Document("a").Url.Href);
        source.Requests[0].Completion.SetResult(Document("a"));
        controller.Pump(_ => Viewport);
        renderer.Tasks[0].SetResult(Blank(Viewport));
        controller.Pump(_ => Viewport);
        controller.Resize(tab.Id, new(12, 12, 1));
        controller.Resize(tab.Id, new(15, 15, 1));
        Assert.True(renderer.Tokens[1].IsCancellationRequested);
        renderer.Tasks[2].SetResult(Blank(new(15, 15, 1)));
        controller.Pump(_ => new(15, 15, 1));
        renderer.Tasks[1].SetResult(Blank(new(12, 12, 1)));
        controller.Pump(_ => new(15, 15, 1));
        Assert.Equal(15, controller.Page(tab.Id)!.Frame.Size.Width);
        Assert.Single(source.Requests); Assert.Single(tab.History.Entries);
        controller.Resize(tab.Id, new(18, 18, 1));
        controller.Resize(tab.Id, new(15, 15, 1));
        Assert.True(renderer.Tokens[3].IsCancellationRequested);
        renderer.Tasks[3].SetResult(Blank(new(18, 18, 1)));
        controller.Pump(_ => new(15, 15, 1));
        Assert.Equal(15, controller.Page(tab.Id)!.Frame.Size.Width);
        Assert.Equal(4, renderer.Tasks.Count);
        controller.Resize(tab.Id, new(19, 19, 1));
        controller.CloseTab(tab.Id);
        Assert.True(renderer.Tokens[4].IsCancellationRequested);
        renderer.Tasks[4].SetException(new PageNavigationException("Late closed-tab render failure"));
        controller.Pump(_ => Viewport);
        Assert.False(controller.Session.Contains(tab.Id));
        Assert.Empty(window.Tabs);
    }
    private sealed class AsyncRenderer : IPageRenderer
    {
        internal List<TaskCompletionSource<BrowserPage>> Tasks { get; } = [];
        internal List<CancellationToken> Tokens { get; } = [];
        internal List<LoadedPage> Pages { get; } = [];
        internal List<Guid> Committed { get; } = [];
        public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            Pages.Add(page);
            var task = new TaskCompletionSource<BrowserPage>();
            Tasks.Add(task);
            Tokens.Add(cancellationToken);
            return task.Task;
        }
        public void CommitDocument(Guid documentId) => Committed.Add(documentId);
        public void Dispose() { }
    }
    internal sealed class Source : IPageSource
    {
        internal sealed record Request(TaskCompletionSource<LoadedPage> Completion, CancellationToken Token)
        {
            internal required BrowserUrl Url { get; init; }
        }
        internal List<Request> Requests { get; } = [];
        internal bool Disposed { get; private set; }
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
        {
            var request = new Request(new(), cancellationToken) { Url = url };
            Requests.Add(request);
            return request.Completion.Task;
        }
        public void Dispose() => Disposed = true;
    }
    internal sealed class Renderer : IPageRenderer
    {
        internal bool Fail { get; set; }
        internal int Calls { get; private set; }
        internal List<LoadedPage> Pages { get; } = [];
        internal List<LoadedPage> RetainedPages { get; } = [];
        internal bool RejectCommit { get; set; }
        internal List<Guid> Committed { get; } = [];
        public void CommitDocument(Guid documentId)
        {
            if (RejectCommit) { throw new PageNavigationException("Document commit rejected."); }
            Committed.Add(documentId);
        }
        public BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Fail) { throw new UnsupportedHtmlException("Unsupported page."); }
            return Blank(viewport);
        }
        public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            Pages.Add(page);
            return Task.FromResult(Render(page, viewport, cancellationToken));
        }
        public Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            RetainedPages.Add(page);
            return Task.FromResult(Render(page, viewport, cancellationToken));
        }
        public void Dispose() { }
    }
}
