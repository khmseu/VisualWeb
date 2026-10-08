using System.Net;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LinkTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Html = """
        <!doctype html><style>*{margin:0}div{height:80px}</style>
        <a href='../next?q=1#part'><span>one two three four five six</span></a>
        <a>not a link</a><div></div><a href='#end'>bottom</a>
        """;
    private static LoadedPage Document(string html = Html) =>
        new(BrowserUrl.Parse("https://example.com/final/index.html"), html, 200, []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FragmentedAnchorsAreSingleOrderedFocusTargets(bool process)
    {
        using IPageRenderer renderer = process ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 100000);
        var page = await renderer.RenderAsync(Document("""
            <!doctype html><style>*{margin:0}</style>
            <a href='/same'><span>one two three four five six</span></a>
            <a href='/same' target=' _BLANK '>another</a> <a>ignored</a> <a href='/last'>last</a>
            """), new(80, 300, 1), Cancellation);
        Assert.Equal(3, page.LinkTargets.Count);
        Assert.True(page.LinkTargets[0].Rects.Count > 1);
        Assert.Equal(["https://example.com/same", "https://example.com/same", "https://example.com/last"],
            page.LinkTargets.Select(link => link.Url));
        Assert.False(page.LinkTargets[0].OpenInNewTab);
        Assert.True(page.LinkTargets[1].OpenInNewTab);
        Assert.False(page.LinkTargets[2].OpenInNewTab);
        foreach (var rect in page.LinkTargets[0].Rects)
        { Assert.True(page.LinkTargets[0].Contains(rect.X + rect.Width / 2, rect.Y + rect.Height / 2)); }
    }

    [Fact]
    public void KeyboardFocusWrapsIsTabLocalAndResetsOnlyOnSuccessfulCommit()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new TargetsRenderer());
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id);
        var second = controller.CreateTab(window.Id);
        foreach (var tab in new[] { first, second })
        {
            controller.Navigate(tab.Id, "https://example.com/");
            source.Requests[^1].Completion.SetResult(Document());
            controller.Pump(_ => new(100, 50, 1));
        }
        controller.FocusPage(first.Id);
        Assert.Equal(1, controller.MoveLinkFocus(first.Id, backwards: true));
        Assert.Equal(0, controller.MoveLinkFocus(first.Id));
        Assert.Equal(1, controller.MoveLinkFocus(first.Id));
        Assert.Equal(-1, controller.FocusedLinkIndex(second.Id));
        Assert.False(controller.PageHasFocus(second.Id));
        controller.Session.MoveTab(first.Id, controller.Session.CreateWindow().Id);
        Assert.Equal(1, controller.FocusedLinkIndex(first.Id));
        Assert.True(controller.PageHasFocus(first.Id));
        Assert.False(controller.ActivateFocusedLink(first.Id, new(200, 50, 1)));
        Assert.Equal(2, source.Requests.Count);
        Assert.True(controller.ActivateFocusedLink(first.Id));
        Assert.Equal("https://example.com/last", source.Requests[^1].Url.Href);
        source.Requests[^1].Completion.SetException(new PageNavigationException("failed"));
        controller.Pump(_ => new(100, 50, 1));
        Assert.Equal(1, controller.FocusedLinkIndex(first.Id));
        Assert.True(controller.ActivateFocusedLink(first.Id));
        source.Requests[^1].Completion.SetResult(Document());
        controller.Pump(_ => new(100, 50, 1));
        Assert.Equal(-1, controller.FocusedLinkIndex(first.Id));
        controller.FocusLink(first.Id, int.MaxValue);
        Assert.Equal(1, controller.FocusedLinkIndex(first.Id));
        controller.Resize(first.Id, new(200, 50, 1));
        controller.Pump(_ => new(200, 50, 1));
        Assert.Equal(-1, controller.FocusedLinkIndex(first.Id));
        controller.CloseTab(first.Id);
        Assert.Equal(-1, controller.FocusedLinkIndex(second.Id));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LinksFollowFinalDomAndVisibleRetainedGeometry(bool process, bool scripts)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: scripts)
            : new StaticPageRenderer(FontPath, 100000, scripts);
        var document = Document(Html + (scripts ? """
            <script id=run>
            document.querySelector('a').setAttribute('href', './changed#script');
            document.getElementById('run').textContent = 'throw new Error("rerun")';
            </script>
            """ : ""));
        var viewport = new PageViewport(80, 50, 2);
        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        Assert.NotEmpty(first.LinkTargets);
        var expected = scripts ? "https://example.com/final/changed#script" : "https://example.com/next?q=1#part";
        Assert.All(first.LinkTargets, link => Assert.Equal(expected, link.Url));
        Assert.True(first.LinkTargets.SelectMany(link => link.Rects).Select(rect => rect.Y).Distinct().Count() > 1);
        var wide = await renderer.RenderRetainedAsync(document, new(300, 50, 1), Cancellation);
        Assert.Single(wide.LinkTargets.SelectMany(link => link.Rects).Select(rect => rect.Y).Distinct());
        var bottom = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 1e9 }, Cancellation);
        Assert.Contains(bottom.LinkTargets, link => link.Url == document.Url.Href + "#end");
        Assert.All(bottom.LinkTargets.SelectMany(link => link.Rects), link =>
        {
            Assert.InRange(link.X, 0, viewport.Width);
            Assert.InRange(link.Y, 0, viewport.Height);
            Assert.InRange(link.X + link.Width, 0, viewport.Width);
            Assert.InRange(link.Y + link.Height, 0, viewport.Height);
        });
        var empty = await renderer.RenderAsync(Document("<!doctype html><style>*{margin:0}</style><p>no links</p>"), viewport, Cancellation);
        Assert.Empty(empty.LinkTargets);
    }

    [Fact]
    public void ControllerUsesCommittedVisibleTargetsAndPreservesThemOnFailure()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(100, 50, 1);
        Assert.False(controller.ActivateLink(tab.Id, 1, 1));
        controller.Navigate(tab.Id, "https://example.com/redirect");
        source.Requests[0].Completion.SetResult(Document(Html.Replace("#end", "/next", StringComparison.Ordinal)));
        controller.Pump(_ => viewport);
        controller.Scroll(tab.Id, double.MaxValue);
        var page = controller.Page(tab.Id)!;
        var origin = tab.Origin;
        var offset = controller.ScrollY(tab.Id);
        var link = page.LinkTargets.Last();
        Assert.False(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, new(200, 50, 1)));
        Assert.False(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, viewport with { Scale = 2 }));
        Assert.False(controller.ActivateLink(tab.Id, -1, link.Y));
        Assert.False(controller.ActivateLink(tab.Id, 99, 49));
        Assert.True(controller.ActivateLink(tab.Id, link.X + link.Width / 2, link.Y + link.Height / 2));
        Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
        source.Requests[1].Completion.SetException(new PageNavigationException("failed link"));
        controller.Pump(_ => viewport);
        Assert.Same(page, controller.Page(tab.Id));
        Assert.Same(origin, tab.Origin);
        Assert.Equal(offset, controller.ScrollY(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));
        source.Requests[2].Completion.SetResult(Document("<!doctype html><style>*{margin:0}</style><p>new page</p>"));
        controller.Pump(_ => viewport);
        Assert.Empty(controller.Page(tab.Id)!.LinkTargets);
        Assert.Equal(0, controller.ScrollY(tab.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FragmentNavigationUpdatesHistoryAndScrollsRetainedDocument(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source,
            () => process ? new ProcessPageRenderer(RendererPath, FontPath) : new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(120, 50, 1);
        controller.Navigate(tab.Id, "https://example.com/page");
        var html = "<!doctype html><style>*{margin:0}#spacer{height:80px}#target{height:100px}</style>"
            + "<a href='#target'>jump</a><div id='spacer'></div><div id='target'>target</div>";
        source.Requests[0].Completion.SetResult(new(BrowserUrl.Parse("https://example.com/page"), html, 200, []));
        PumpUntilComplete();
        var committed = controller.Page(tab.Id)!;
        var target = Assert.Single(committed.FragmentTargets, item => item.Id == "target");
        Assert.True(target.Y > 0);
        var link = Assert.Single(committed.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, viewport));

        Assert.Single(source.Requests);
        Assert.Equal("https://example.com/page#target", tab.History.Current!.Href);
        Assert.Equal(tab.History.Current.Href, tab.AddressText);
        PumpUntilComplete(committed);
        Assert.NotSame(committed, controller.Page(tab.Id));
        Assert.Equal(committed.FragmentTargets, controller.Page(tab.Id)!.FragmentTargets);
        Assert.Equal(target.Y, controller.ScrollY(tab.Id));

        void PumpUntilComplete(BrowserPage? previous = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((tab.IsLoading || previous is not null && ReferenceEquals(previous, controller.Page(tab.Id)))
                && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void OpaqueDocumentCanNavigateToItsOwnFragment()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(120, 50, 1);
        var documentUrl = BrowserUrl.Parse("data:text/html,document");
        controller.Navigate(tab.Id, documentUrl.Href);
        source.Requests[0].Completion.SetResult(new(documentUrl,
            "<!doctype html><style>*{margin:0}#target{height:100px}</style>"
            + "<a href='#target'>jump</a><div id='target'>target</div>", 200, []));
        controller.Pump(_ => viewport);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, viewport));

        Assert.Single(source.Requests);
        Assert.EndsWith("#target", tab.History.Current!.Href, StringComparison.Ordinal);
        Assert.True(controller.ScrollY(tab.Id) > 0);
    }

    [Fact]
    public void EmptyFragmentScrollsToTopWithoutReloading()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(120, 50, 1);
        controller.Navigate(tab.Id, "https://example.com/page");
        source.Requests[0].Completion.SetResult(new(BrowserUrl.Parse("https://example.com/page"),
            "<!doctype html><style>*{margin:0}#spacer{height:100px}</style><div id='spacer'></div><a href='#'>top</a>", 200, []));
        controller.Pump(_ => viewport);
        controller.Scroll(tab.Id, double.MaxValue);
        var page = controller.Page(tab.Id)!;
        Assert.True(controller.ScrollY(tab.Id) > 0);

        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, viewport));

        Assert.Single(source.Requests);
        Assert.Equal("https://example.com/page#", tab.History.Current!.Href);
        Assert.Equal(0, controller.ScrollY(tab.Id));
        Assert.NotSame(page, controller.Page(tab.Id));
        Assert.Equal(page.FragmentTargets, controller.Page(tab.Id)!.FragmentTargets);
    }

    [Theory]
    [InlineData("../next", "https://example.com/next")]
    [InlineData("//other.example/path", "https://other.example/path")]
    public void SupportedDestinationsUseNormalNavigation(string href, string absolute)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/redirect");
        source.Requests[0].Completion.SetResult(Document(
            $"<!doctype html><style>*{{margin:0}}</style><a href='{href}' target='_self'>link</a>"));
        controller.Pump(_ => new(100, 50, 1));
        Assert.Null(tab.Error);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.Equal(absolute, link.Url);
        Assert.False(link.OpenInNewTab);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));
        Assert.Equal(absolute, source.Requests[1].Url.Href);
        Assert.Single(controller.Session.Windows.Single().Tabs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlankTargetOpensDestinationInNewTab(bool keyboard)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var window = controller.Session.CreateWindow();
        var original = controller.CreateTab(window.Id);
        controller.Navigate(original.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document(
            "<!doctype html><style>*{margin:0}</style><a href='/next' target='_blank'>open</a>"));
        controller.Pump(_ => new(100, 50, 1));
        var originalPage = controller.Page(original.Id);

        if (keyboard)
        {
            controller.FocusPage(original.Id);
            controller.FocusLink(original.Id, 0);
            Assert.True(controller.ActivateFocusedLink(original.Id));
        }
        else
        {
            var link = Assert.Single(originalPage!.LinkTargets);
            Assert.True(controller.ActivateLink(original.Id, link.X + 1, link.Y + 1));
        }

        var opened = window.ActiveTab!;
        Assert.NotNull(opened);
        Assert.NotEqual(original.Id, opened.Id);
        Assert.Equal("https://example.com/next", source.Requests[1].Url.Href);
        Assert.True(opened.IsLoading);
        source.Requests[1].Completion.SetResult(new(source.Requests[1].Url,
            "<!doctype html><style>*{margin:0}</style><p>new tab</p>", 200, []));
        controller.Pump(_ => new(100, 50, 1));
        Assert.False(opened.IsLoading);
        Assert.Null(opened.Error);
        Assert.Equal("https://example.com/next", opened.History.Current!.Href);
        Assert.NotSame(originalPage, controller.Page(opened.Id));
        Assert.Same(originalPage, controller.Page(original.Id));
        Assert.Single(original.History.Entries);
        Assert.Equal(2, window.Tabs.Count);
    }

    [Fact]
    public void BlankTargetCannotBypassSecurePageDowngradePolicy()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var window = controller.Session.CreateWindow();
        var tab = controller.CreateTab(window.Id);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document(
            "<!doctype html><style>*{margin:0}</style><a href='http://example.com/clear' target='_blank'>open</a>"));
        controller.Pump(_ => new(100, 50, 1));
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);

        Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));

        Assert.Single(window.Tabs);
        Assert.Single(source.Requests);
    }

    [Fact]
    public void LegacyPageSourceFailsClosedForSecurePageHttpLink()
    {
        var source = new LegacyPageSource();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://secure.example/");
        controller.Pump(_ => new(100, 50, 1));
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        var history = tab.History.Current;
        var link = Assert.Single(committed!.LinkTargets);

        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab.Id,
            link.X + link.Width / 2, link.Y + link.Height / 2));

        Assert.Contains("does not enforce HTTPS downgrade protection", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, source.LoadCount);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Same(history, tab.History.Current);
        Assert.False(tab.IsLoading);
    }

    private sealed class LegacyPageSource : IPageSource
    {
        internal int LoadCount { get; private set; }

        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult(new LoadedPage(url,
                "<!doctype html><style>*{margin:0}</style><a href='http://outside.example/'>insecure</a>", 200, []));
        }

        public void Dispose() { }
    }

    [Fact]
    public void ExplicitAddressBarDataUrlNavigationRemainsAvailable()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);

        controller.Navigate(tab.Id, "data:text/html,explicit");

        Assert.Equal("data:text/html,explicit", Assert.Single(source.Requests).Url.Href);
    }

    [Fact]
    public void PageInitiatedDataUrlNavigationIsBlockedTransactionally()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/");
        var committedDocument = Document("<!doctype html><style>*{margin:0}</style><a href='data:text/html,untrusted'>open</a>");
        source.Requests[0].Completion.SetResult(committedDocument);
        controller.Pump(_ => new(100, 50, 1));
        var page = controller.Page(tab.Id)!;
        var history = tab.History.Current;
        var link = Assert.Single(page.LinkTargets);

        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab.Id,
            link.X + link.Width / 2, link.Y + link.Height / 2));

        Assert.Contains("Page-initiated data URL navigation is blocked", error.Message, StringComparison.Ordinal);
        Assert.Single(source.Requests);
        Assert.Same(page, controller.Page(tab.Id));
        Assert.Same(history, tab.History.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SecurePageCannotFollowHttpLinkOrDowngradeRedirect(bool redirect)
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath == "/document")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"<!doctype html><style>*{{margin:0}}</style><a href='{(redirect ? "https://secure.example/link" : "http://secure.example/link")}'>open</a>",
                        System.Text.Encoding.UTF8, "text/html")
                };
            }
            return new(HttpStatusCode.Found)
            { Headers = { Location = new Uri("http://secure.example/insecure") } };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(100, 50, 1);
        controller.Navigate(tab.Id, "https://secure.example/document");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        var link = Assert.Single(committed!.LinkTargets);

        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1, viewport));
        PumpUntilComplete();

        Assert.Contains("Secure transport policy blocks HTTP loads", tab.Error, StringComparison.Ordinal);
        Assert.Equal(redirect ? 2 : 1, requests.Count);
        Assert.All(requests, request => Assert.Equal("https", request.Scheme));
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("https://secure.example/document", tab.History.Current!.Href);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public async Task RectangleUsesShapedTextWidthAndLineHeightNotContainerWidth()
    {
        using var font = new TextFont(FontPath);
        using var renderer = new StaticPageRenderer(FontPath, 100000);
        var page = await renderer.RenderAsync(Document("""
            <!doctype html><style>*{margin:0}body{font-size:20px;line-height:30px}</style>
            <a href='./next'><span>link</span></a>
            """), new(300, 100, 1), Cancellation);
        var link = Assert.Single(page.LinkTargets);
        Assert.Equal(0, link.X);
        Assert.Equal(0, link.Y);
        Assert.Equal(font.Shape("link", 20, Cancellation).Width, link.Width);
        Assert.Equal(30, link.Height);
        Assert.True(link.Contains(link.X, link.Y));
        Assert.False(link.Contains(link.X + link.Width, link.Y));
        Assert.False(link.Contains(link.X, link.Y + link.Height));
    }

    [Fact]
    public void OverlappingTargetsChooseLastPaintedAndNoHitLeavesStateAlone()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new TargetsRenderer());
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => new(100, 50, 1));
        var address = tab.AddressText;
        Assert.False(controller.ActivateLink(tab.Id, 20, 20));
        Assert.Equal(address, tab.AddressText);
        Assert.Null(tab.Error);
        Assert.Single(source.Requests);
        Assert.True(controller.ActivateLink(tab.Id, 1, 1));
        Assert.Equal("https://example.com/last", source.Requests[1].Url.Href);
    }

    private sealed class TargetsRenderer : IPageRenderer
    {
        public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            using var fonts = new PaintFontRegistry();
            var frame = CpuRasterizer.Render(new(viewport.Width, viewport.Height, []), fonts,
                viewport.Scale, cancellationToken: cancellationToken);
            return Task.FromResult(new BrowserPage(frame, "Targets", "Ready")
            {
                ScrollHeight = viewport.Height,
                LinkTargets = [new(0, 0, 10, 10, "https://example.com/first"), new(0, 0, 10, 10, "https://example.com/last")]
            });
        }
        public void Dispose() { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkLimitsAreExplicitRenderFailuresAndKeepRetainedDocument(bool process)
    {
        using IPageRenderer renderer = process ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 100000);
        var viewport = new PageViewport(100, 50, 1);
        var old = Document("<!doctype html><style>*{margin:0}</style><a href='./old'>old</a>");
        var first = await renderer.RenderAsync(old, viewport, Cancellation);
        renderer.CommitDocument(old.DocumentId);
        var huge = Document("<!doctype html><style>*{margin:0}</style><a href='data:,"
            + new string('x', 8192) + "'>huge</a>");
        var error = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(huge, viewport, Cancellation));
        Assert.Contains("URL limit", error.Message);
        var retained = await renderer.RenderRetainedAsync(old, viewport, Cancellation);
        Assert.Equal(Assert.Single(first.LinkTargets).Url, Assert.Single(retained.LinkTargets).Url);
        Assert.Equal(first.LinkTargets[0].Rects, retained.LinkTargets[0].Rects);
    }

    [Fact]
    public async Task RendererEnforcesExactAnchorCountLimit()
    {
        using var renderer = new StaticPageRenderer(FontPath, 2_000_000);
        var html = "<!doctype html><style>*{margin:0}</style>"
            + string.Concat(Enumerable.Repeat("<a href='./x'>x</a> ", RendererProtocol.MaxLinkTargets));
        var viewport = new PageViewport(10000, 200, 1);
        var page = await renderer.RenderAsync(Document(html), viewport, Cancellation);
        Assert.Equal(RendererProtocol.MaxLinkTargets, page.LinkTargets.Count);
        var failure = await Assert.ThrowsAsync<PageNavigationException>(() =>
            renderer.RenderAsync(Document(html + "<a href='./x'>x</a>"), viewport, Cancellation));
        Assert.Contains("count limit", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PerAnchorRectangleLimitIsExactAndPreservesCommittedDocument(bool process)
    {
        using IPageRenderer renderer = process ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 100000);
        var viewport = new PageViewport(40, 2000, 1);
        var html = "<!doctype html><style>*{margin:0}a{display:block}</style><a href='./x'>"
            + string.Concat(Enumerable.Repeat("<div>x</div>", RendererProtocol.MaxLinkRects));
        var document = Document(html + "</a>");
        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        Assert.Equal(RendererProtocol.MaxLinkRects, Assert.Single(first.LinkTargets).Rects.Count);
        renderer.CommitDocument(document.DocumentId);
        var failure = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document(html + "<div>x</div></a>"), viewport, Cancellation));
        Assert.Contains("per-anchor rectangle limit", failure.Message);
        var retained = await renderer.RenderRetainedAsync(document, viewport, Cancellation);
        Assert.Equal(first.LinkTargets[0].Rects, Assert.Single(retained.LinkTargets).Rects);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/")]
    [InlineData("mailto:test@example.com")]
    public void UnsupportedLinkSchemesFailBeforeLoading(string url)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document($"<!doctype html><style>*{{margin:0}}</style><a href='{url}'>link</a>"));
        controller.Pump(_ => new(100, 50, 1));
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));
        Assert.Single(source.Requests);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
