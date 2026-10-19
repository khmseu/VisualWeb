using System.Diagnostics;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LinkClickEventTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly PageViewport Viewport = new(200, 60, 1);
    private const string Listeners = """
        <!doctype html><title>Start</title><style>*{margin:0}</style>
        <p id=p><a id=a href="/next">go</a></p>
        <script>
        let log=[];
        const a=document.getElementById('a');
        document.addEventListener('click',e=>log.push('capture:'+e.eventPhase),true);
        a.addEventListener('click',e=>log.push('a:'+e.isTrusted+':'+e.bubbles+':'+e.cancelable+':'+(e.target===a)));
        document.getElementById('p').addEventListener('click',e=>log.push('p:'+e.eventPhase));
        document.addEventListener('click',e=>{
            log.push('doc:'+e.eventPhase);
            if(log.length<5)e.preventDefault();
            queueMicrotask(()=>document.title=log.length+':'+log.slice(0,4).join(','));
        });
        </script>
        """;
    private static LoadedPage Document(string html, string url = "https://example.com/start") => new(BrowserUrl.Parse(url), html, 200, []);
    private static IPageRenderer Renderer(bool process, bool scripts) => process
        ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: scripts)
        : new StaticPageRenderer(FontPath, 100000, scripts);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialListenersPersistPropagateAndPreventDefaultSuppressesOnlyCanceledNavigation(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        source.Requests[0].Completion.SetResult(Document(Listeners));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var committed = controller.Page(tab.Id)!;
        Assert.Equal("Start", tab.Title);
        var link = Assert.Single(committed.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => !ReferenceEquals(committed, controller.Page(tab.Id)) || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.Equal("4:capture:1,a:true:true:true:true,p:3,doc:3", tab.Title);
        Assert.Single(source.Requests);
        Assert.Single(tab.History.Entries);
        Assert.False(tab.IsLoading);
        Assert.Equal("https://example.com/start", tab.AddressText);

        var repainted = controller.Page(tab.Id)!;
        link = Assert.Single(repainted.LinkTargets);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => source.Requests.Count == 2 || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
        Assert.Equal("8:capture:1,a:true:true:true:true,p:3,doc:3", tab.Title);
        source.Requests[1].Completion.SetResult(Document("<!doctype html><title>Next</title>", "https://example.com/next"));
        PumpUntil(controller, () => !tab.IsLoading);
        Assert.Equal("Next", tab.Title);
        Assert.Equal(2, tab.History.Entries.Count);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    public void FocusedLinkActivationDispatchesCancelableClickAndPreservesTabModifiers(
        bool process, bool forceNewTab, bool activateNewTab, bool cancelDefault)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        var html = $$"""
            <!doctype html><title>Start</title><style>*{margin:0}</style>
            <a href="/next" target="_self">go</a>
            <script>document.addEventListener('click',e=>{document.title='Clicked';{{(cancelDefault ? "e.preventDefault();" : "")}}})</script>
            """;
        source.Requests[0].Completion.SetResult(Document(html));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        controller.FocusPage(tab.Id);
        controller.FocusLink(tab.Id, 0);

        Assert.True(controller.ActivateFocusedLink(tab.Id, Viewport, forceNewTab, activateNewTab));

        if (cancelDefault)
        {
            PumpUntil(controller, () => tab.Title == "Clicked");
            Assert.Single(source.Requests);
            Assert.Single(window.Tabs);
            Assert.Equal(tab.Id, window.ActiveTabId);
            return;
        }

        PumpUntil(controller, () => source.Requests.Count == 2);
        if (!forceNewTab)
        {
            Assert.Single(window.Tabs);
            Assert.Equal(tab.Id, window.ActiveTabId);
            Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
            return;
        }

        var opened = Assert.Single(window.Tabs, candidate => candidate.Id != tab.Id);
        Assert.Equal(activateNewTab ? opened.Id : tab.Id, window.ActiveTabId);
        Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CtrlPrimaryLinkActivationDispatchesCancelableClickBeforeBackgroundTab(bool process, bool cancelDefault)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        source.Requests[0].Completion.SetResult(Document($$"""
            <!doctype html><title>Start</title><style>*{margin:0}</style>
            <a href="/next">go</a>
            <script>document.addEventListener('click',e=>{document.title='Clicked';if({{(cancelDefault ? "true" : "false")}})e.preventDefault()})</script>
            """));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport,
            forceNewTab: true, activateNewTab: false, dispatchClickEvent: true));

        if (cancelDefault)
        {
            PumpUntil(controller, () => tab.Title == "Clicked");
            Assert.Single(source.Requests);
            Assert.Single(window.Tabs);
        }
        else
        {
            PumpUntil(controller, () => source.Requests.Count == 2);
            var background = Assert.Single(window.Tabs, candidate => candidate.Id != tab.Id);
            Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
            Assert.True(background.IsLoading);
        }
        Assert.Equal(tab.Id, window.ActiveTabId);
        Assert.Null(tab.Error);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StaticModeAndNonPrimaryActivationKeepImmediateNavigation(bool process, bool scripts)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, scripts));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        source.Requests[0].Completion.SetResult(Document(Listeners));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        // Without the opt-in, primary activation stays synchronous; with it, other activation paths do not dispatch.
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: !scripts));
        Assert.Equal(2, source.Requests.Count);
        Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
        Assert.Equal("Start", tab.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListenerFailureInvalidatesHostWithoutRepaintOrNavigationUntilReload(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        source.Requests[0].Completion.SetResult(Document("""
            <!doctype html><title>Start</title><style>*{margin:0}</style><a href="/next">go</a>
            <script>document.addEventListener('click',()=>{document.title='partial';throw Error('boom')});</script>
            """));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var committed = controller.Page(tab.Id)!;
        var link = Assert.Single(committed.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => tab.Error is not null);
        Assert.StartsWith("Page click event failed: ", tab.Error);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("Start", tab.Title);
        Assert.Single(source.Requests);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => tab.Error?.Contains("invalidated", StringComparison.Ordinal) == true);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Single(source.Requests);

        controller.Reload(tab.Id);
        source.Requests[1].Completion.SetResult(Document(Listeners));
        PumpUntil(controller, () => !tab.IsLoading);
        Assert.Null(tab.Error);
        link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        var reloaded = controller.Page(tab.Id);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => !ReferenceEquals(reloaded, controller.Page(tab.Id)) || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.StartsWith("4:", tab.Title, StringComparison.Ordinal);
        Assert.Equal(2, source.Requests.Count);
    }

    [Fact]
    public void RendererBoundsClicksValidatesLinksAndReleasesReplacedHosts()
    {
        using var renderer = new StaticPageRenderer(FontPath, 100000, executeInlineScripts: true);
        Assert.True(renderer.DispatchesLinkClicks);
        var page = Document("""
            <!doctype html><style>*{margin:0}</style><a href="/next">go</a>
            <script>let count=0;document.addEventListener('click',()=>++count);</script>
            """);
        var rendered = renderer.Render(page, Viewport, Cancellation);
        var url = Assert.Single(rendered.LinkTargets).Url;
        Assert.Throws<PageNavigationException>(() => renderer.DispatchLinkClick(page.DocumentId, 0, url, Cancellation));
        renderer.CommitDocument(page.DocumentId);
        Assert.Throws<PageNavigationException>(() => renderer.DispatchLinkClick(page.DocumentId, 1, url, Cancellation));
        Assert.Throws<PageNavigationException>(() => renderer.DispatchLinkClick(page.DocumentId, 0, url + "x", Cancellation));
        Assert.Throws<PageNavigationException>(() => renderer.DispatchLinkClick(Guid.NewGuid(), 0, url, Cancellation));
        for (var count = 0; count < StaticPageRenderer.MaxLinkClicksPerDocument; count++)
        { Assert.True(renderer.DispatchLinkClick(page.DocumentId, 0, url, Cancellation)); }
        Assert.Throws<ScriptLimitException>(() => renderer.DispatchLinkClick(page.DocumentId, 0, url, Cancellation));
        Assert.Throws<PageNavigationException>(() => renderer.DispatchLinkClick(page.DocumentId, 0, url, Cancellation));
        Assert.Contains("limit", Assert.Throws<PageNavigationException>(() =>
            renderer.Render(page, Viewport, Cancellation, reuseDocument: true)).Message, StringComparison.Ordinal);

        var next = Document(page.Html);
        renderer.Render(next, Viewport, Cancellation);
        renderer.CommitDocument(next.DocumentId);
        Assert.False(renderer.HasDocument(page.DocumentId));
        Assert.True(renderer.DispatchLinkClick(next.DocumentId, 0, url, Cancellation));
    }

    [Fact]
    public async Task StaticRendererRejectsClicksAndWorkerExitLosesRetainedHost()
    {
        using (var plain = new StaticPageRenderer(FontPath, 100000))
        {
            Assert.False(plain.DispatchesLinkClicks);
            Assert.Throws<PageNavigationException>(() => plain.DispatchLinkClick(Guid.NewGuid(), 0, "https://example.com/", Cancellation));
        }
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true);
        var page = Document(Listeners);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        var url = Assert.Single(rendered.LinkTargets).Url;
        Assert.False(await renderer.DispatchLinkClickAsync(page.DocumentId, 0, url, Cancellation));
        Assert.Equal("4:capture:1,a:true:true:true:true,p:3,doc:3",
            (await renderer.RenderRetainedAsync(page, Viewport, Cancellation)).Title);
        using (var worker = Process.GetProcessById(renderer.ProcessId!.Value)) { worker.Kill(); await worker.WaitForExitAsync(Cancellation); }
        await Assert.ThrowsAsync<RendererProcessException>(() => renderer.DispatchLinkClickAsync(page.DocumentId, 0, url, Cancellation));
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchLinkClickAsync(page.DocumentId, 0, url, Cancellation));
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchLinkClickAsync(Guid.NewGuid(), 0, url, Cancellation));
    }

    private static void PumpUntil(BrowserController controller, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            controller.Pump(_ => Viewport);
            if (condition()) { return; }
            Thread.Sleep(5);
        } while (DateTime.UtcNow < deadline);
        Assert.True(condition());
    }
}
