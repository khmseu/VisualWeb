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
        Assert.True(first.LinkTargets.Select(link => link.Y).Distinct().Count() > 1);
        var wide = await renderer.RenderRetainedAsync(document, new(300, 50, 1), Cancellation);
        Assert.Single(wide.LinkTargets.Select(link => link.Y).Distinct());
        var bottom = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 1e9 }, Cancellation);
        Assert.Contains(bottom.LinkTargets, link => link.Url == document.Url.Href + "#end");
        Assert.All(bottom.LinkTargets, link =>
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
        source.Requests[0].Completion.SetResult(Document());
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
        Assert.Equal("https://example.com/final/index.html#end", source.Requests[1].Url.Href);
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
    [InlineData("../next", "https://example.com/next")]
    [InlineData("#part", "https://example.com/final/index.html#part")]
    [InlineData("", "https://example.com/final/index.html")]
    [InlineData("//other.example/path", "https://other.example/path")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("file:///tmp/page.html#part", "file:///tmp/page.html#part")]
    [InlineData("data:text/html,hello#part", "data:text/html,hello#part")]
    public void SupportedDestinationsUseNormalNavigation(string href, string absolute)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/redirect");
        source.Requests[0].Completion.SetResult(Document(
            $"<!doctype html><style>*{{margin:0}}</style><a href='{href}' target='_blank'>link</a>"));
        controller.Pump(_ => new(100, 50, 1));
        Assert.Null(tab.Error);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.Equal(absolute, link.Url);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));
        Assert.Equal(absolute, source.Requests[1].Url.Href);
        Assert.Single(controller.Session.Windows.Single().Tabs);
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
        Assert.Equal(first.LinkTargets, retained.LinkTargets);
    }

    [Fact]
    public async Task RendererEnforcesExactRectangleCountLimit()
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
}
