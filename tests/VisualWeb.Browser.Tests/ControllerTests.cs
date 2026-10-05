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
    internal sealed class Source : IPageSource
    {
        internal sealed record Request(TaskCompletionSource<LoadedPage> Completion, CancellationToken Token);
        internal List<Request> Requests { get; } = [];
        internal bool Disposed { get; private set; }
        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
        {
            var request = new Request(new(), cancellationToken);
            Requests.Add(request);
            return request.Completion.Task;
        }
        public void Dispose() => Disposed = true;
    }
    internal sealed class Renderer : IPageRenderer
    {
        internal bool Fail { get; set; }
        internal int Calls { get; private set; }
        public BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Fail) { throw new UnsupportedHtmlException("Unsupported page."); }
            return Blank(viewport);
        }
        public void Dispose() { }
    }
}
