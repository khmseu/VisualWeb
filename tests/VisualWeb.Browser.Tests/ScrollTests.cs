using System.Diagnostics;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ScrollTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Html = """
        <!doctype html><style>*{margin:0} div{height:40px}
        #top{background-color:red} #bottom{background-color:blue}</style>
        <div id=top></div><div id=bottom></div>
        """;
    private static LoadedPage Document(string html = Html) => new(BrowserUrl.Parse("https://example.com/"), html, 200, []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnorderedListMarkersSurviveLocalAndProcessRetainedRepaint(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 10000);
        var document = Document("""
            <!doctype html><style>*{margin:0;font-size:20px;line-height:20px}li{color:red}</style>
            <ul><li></li><li></li></ul>
            """);
        var viewport = new PageViewport(100, 20, 1);
        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        Assert.Equal(40, first.ScrollHeight);
        Assert.Contains(Enumerable.Range(0, first.Frame.Size.Height).SelectMany(y => Enumerable.Range(20, 20)
            .Select(x => (y * first.Frame.Size.Width + x) * 4)),
            offset => first.Frame.Pixels.Span[offset] == 0 && first.Frame.Pixels.Span[offset + 1] == 0
                && first.Frame.Pixels.Span[offset + 2] == 255);
        var scrolled = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 20 }, Cancellation);
        Assert.Equal(first.Frame.Pixels.ToArray(), scrolled.Frame.Pixels.ToArray());
        var resized = await renderer.RenderRetainedAsync(document, viewport with { Width = 80 }, Cancellation);
        Assert.Equal(40, resized.ScrollHeight);
        Assert.Equal(80, resized.Frame.Size.Width);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RetainedScrollPaintsBelowViewportWithoutChangingGeometryOrRerunningScripts(bool process, bool scripts)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: scripts)
            : new StaticPageRenderer(FontPath, 10000, scripts);
        var document = Document(Html + (scripts ? """
            <script id=run>document.title = document.title + ' once';
            document.getElementById('run').textContent = 'throw new Error("rerun")';</script>
            """ : ""));
        var viewport = new PageViewport(20, 20, 2);
        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        var pid = (renderer as ProcessPageRenderer)?.ProcessId;
        Assert.Equal(80, first.ScrollHeight);
        var bottom = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 1e9 }, Cancellation);
        Assert.Equal(first.Frame.Size, bottom.Frame.Size);
        Assert.Equal(first.Frame.Stride, bottom.Frame.Stride);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, bottom.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(first.Title, bottom.Title);
        Assert.Equal(pid, (renderer as ProcessPageRenderer)?.ProcessId);
        var top = await renderer.RenderRetainedAsync(document, viewport, Cancellation);
        Assert.Equal(first.Frame.Pixels.ToArray(), top.Frame.Pixels.ToArray());
        await Assert.ThrowsAsync<PageNavigationException>(() =>
            renderer.RenderRetainedAsync(document with { Html = document.Html + "<!-- changed -->" }, viewport, Cancellation));
        var retained = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 40 }, Cancellation);
        Assert.Equal(first.Title, retained.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HorizontalRetainedScrollRevealsDocumentOverflowWithoutChangingFrameSize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 10000);
        var document = Document("""
            <!doctype html><style>*{margin:0}html{background-color:blue}
            #wide{margin-left:80px;width:20px;height:20px;background-color:red}</style><div id=wide></div>
            """);
        var viewport = new PageViewport(50, 20, 1);

        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        var scrolled = await renderer.RenderRetainedAsync(document, viewport with { ScrollX = 50 }, Cancellation);

        Assert.Equal(100, first.ScrollWidth);
        Assert.Equal(first.Frame.Size, scrolled.Frame.Size);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, first.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, scrolled.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, scrolled.Frame.Pixels.Span[(29 * 4)..(30 * 4)].ToArray());
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, scrolled.Frame.Pixels.Span[(30 * 4)..(31 * 4)].ToArray());
    }

    [Fact]
    public void ControllerClampsPreservesResizeAndOnlyResetsOnSuccessfulNavigation()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 10000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);
        controller.Scroll(tab.Id, 20);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => viewport);
        controller.Scroll(tab.Id, 0);
        controller.Scroll(tab.Id, double.MaxValue);
        Assert.Equal(60, controller.ScrollY(tab.Id));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, controller.Page(tab.Id)!.Frame.Pixels.Span[..4].ToArray());
        viewport = new(20, 40, 1);
        controller.Resize(tab.Id, viewport);
        Assert.Equal(40, controller.ScrollY(tab.Id));
        controller.Navigate(tab.Id, "https://example.com/fail");
        source.Requests[1].Completion.SetException(new PageNavigationException("failed"));
        controller.Pump(_ => viewport);
        Assert.Equal(40, controller.ScrollY(tab.Id));
        controller.Scroll(tab.Id, -double.MaxValue);
        Assert.Equal(0, controller.ScrollY(tab.Id));
        controller.Scroll(tab.Id, 20);
        controller.Navigate(tab.Id, "https://example.com/new");
        source.Requests[2].Completion.SetResult(Document());
        controller.Pump(_ => viewport);
        Assert.Equal(0, controller.ScrollY(tab.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Scroll(tab.Id, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Scroll(tab.Id, double.PositiveInfinity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossDocumentHistoryTraversalRestoresEachEntryScrollPosition(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 10000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);
        var firstUrl = BrowserUrl.Parse("https://example.com/first");
        var secondUrl = BrowserUrl.Parse("https://example.com/second");

        controller.Navigate(tab.Id, firstUrl.Href);
        source.Requests[0].Completion.SetResult(new(firstUrl, Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        ScrollTo(40);

        controller.Navigate(tab.Id, secondUrl.Href);
        source.Requests[1].Completion.SetResult(new(secondUrl, Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        ScrollTo(20);

        controller.Back(tab.Id);
        Assert.Equal(firstUrl.Href, source.Requests[2].Url.Href);
        source.Requests[2].Completion.SetResult(new(firstUrl, Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        Assert.Equal(40, controller.ScrollY(tab.Id));

        controller.Forward(tab.Id);
        Assert.Equal(secondUrl.Href, source.Requests[3].Url.Href);
        source.Requests[3].Completion.SetResult(new(secondUrl, Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        Assert.Equal(20, controller.ScrollY(tab.Id));

        void ScrollTo(double offset)
        {
            var previous = controller.Page(tab.Id);
            controller.Scroll(tab.Id, offset);
            PumpUntil(() => !ReferenceEquals(previous, controller.Page(tab.Id)));
            Assert.Equal(offset, controller.ScrollY(tab.Id));
        }

        void PumpUntil(Func<bool> complete)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => viewport);
                if (complete()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("History traversal rendering timed out.");
        }
    }

    [Fact]
    public void CrossDocumentHistoryTraversalClampsScrollWhenTheRestoredPageShrinks()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 10000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);
        var firstUrl = BrowserUrl.Parse("https://example.com/first");
        var secondUrl = BrowserUrl.Parse("https://example.com/second");

        controller.Navigate(tab.Id, firstUrl.Href);
        source.Requests[0].Completion.SetResult(new(firstUrl, Html, 200, []));
        controller.Pump(_ => viewport);
        controller.Scroll(tab.Id, 40);
        controller.Navigate(tab.Id, secondUrl.Href);
        source.Requests[1].Completion.SetResult(new(secondUrl, Html, 200, []));
        controller.Pump(_ => viewport);

        controller.Back(tab.Id);
        source.Requests[2].Completion.SetResult(new(firstUrl, "<!doctype html><style>*{margin:0}</style>short", 200, []));
        controller.Pump(_ => viewport);
        controller.Pump(_ => viewport);
        Assert.Equal(0, controller.ScrollY(tab.Id));
    }

    [Fact]
    public void TabsOwnTheirScrollAcrossMovesAndShortPagesDoNotScroll()
    {
        var sources = new List<ControllerTests.Source>();
        using var controller = new BrowserController(
            () => { var source = new ControllerTests.Source(); sources.Add(source); return source; },
            () => new StaticPageRenderer(FontPath, 10000));
        var window = controller.Session.CreateWindow();
        var first = controller.CreateTab(window.Id);
        var second = controller.CreateTab(window.Id);
        foreach (var tab in new[] { first, second }) { controller.Navigate(tab.Id, "https://example.com/"); }
        sources[0].Requests[0].Completion.SetResult(Document());
        sources[1].Requests[0].Completion.SetResult(Document("<!doctype html><style>*{margin:0}</style>"));
        controller.Pump(_ => new(20, 20, 1));
        controller.Scroll(first.Id, 15);
        controller.Scroll(second.Id, 200);
        controller.Session.MoveTab(first.Id, controller.Session.CreateWindow().Id);
        Assert.Equal(15, controller.ScrollY(first.Id));
        Assert.Equal(0, controller.ScrollY(second.Id));
    }

    [Fact]
    public void SupersededScrollResizeAndNavigationCannotPublishStaleFrames()
    {
        var source = new ControllerTests.Source();
        var renderer = new PendingRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => viewport);
        renderer.Complete(0);
        controller.Pump(_ => viewport);
        controller.Scroll(tab.Id, 10);
        controller.Scroll(tab.Id, 15);
        Assert.True(renderer.Tokens[1].IsCancellationRequested);
        Assert.Equal(25, renderer.Viewports[2].ScrollY);
        renderer.Complete(2);
        renderer.Complete(1);
        controller.Pump(_ => viewport);
        Assert.Equal(25, controller.ScrollY(tab.Id));
        Assert.Equal("25", controller.Page(tab.Id)!.Title);
        viewport = new(30, 40, 1);
        controller.Resize(tab.Id, viewport);
        controller.Scroll(tab.Id, 10);
        Assert.True(renderer.Tokens[3].IsCancellationRequested);
        Assert.Equal(viewport with { ScrollY = 35 }, renderer.Viewports[4]);
        renderer.Complete(4, height: 50);
        renderer.Complete(3);
        controller.Pump(_ => viewport);
        Assert.Equal(10, controller.ScrollY(tab.Id));
        controller.Scroll(tab.Id, -5);
        controller.Navigate(tab.Id, "https://example.com/fail");
        Assert.True(renderer.Tokens[5].IsCancellationRequested);
        renderer.Complete(5);
        source.Requests[1].Completion.SetException(new PageNavigationException("failed"));
        controller.Pump(_ => viewport);
        Assert.Equal(10, controller.ScrollY(tab.Id));
        Assert.Equal(2, source.Requests.Count);
    }

    [Fact]
    public void NavigationViewportRaceRepaintsCandidateWithoutRepeatingInitialRender()
    {
        var source = new ControllerTests.Source();
        var renderer = new PendingRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => new(20, 20, 1));
        renderer.Complete(0);
        controller.Pump(_ => new(30, 25, 2));
        Assert.Null(controller.Page(tab.Id));
        Assert.Equal(1, renderer.InitialCalls);
        Assert.Equal(new(30, 25, 2), renderer.Viewports[1]);
        renderer.Complete(1);
        controller.Pump(_ => null);
        Assert.Null(controller.Page(tab.Id));
        Assert.True(tab.IsLoading);
        controller.Pump(_ => new(30, 25, 2));
        Assert.Equal(60, controller.Page(tab.Id)!.Frame.Size.Width);
        Assert.Single(tab.History.Entries);
    }

    [Fact]
    public async Task CanceledInFlightRetainedProcessExchangeDrainsAndKeepsDocumentAndChannel()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true);
        var document = Document(Html + "<script>document.title += ' once';</script>");
        var viewport = new PageViewport(20, 20, 1);
        var first = await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        var pid = renderer.ProcessId;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var stale = renderer.RenderRetainedAsync(document, viewport with { ScrollY = 5 }, cancel.Token);
        cancel.Cancel();
        var current = renderer.RenderRetainedAsync(document, viewport with { ScrollY = 40 }, Cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stale);
        var page = await current;
        Assert.Equal(pid, renderer.ProcessId);
        Assert.Equal(first.Title, page.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void FailedScrollKeepsLastPublishedFrameAndPosition()
    {
        var source = new ControllerTests.Source();
        var renderer = new PendingRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => new(20, 20, 1));
        renderer.Complete(0);
        controller.Pump(_ => new(20, 20, 1));
        var first = controller.Page(tab.Id);
        controller.Scroll(tab.Id, 10);
        renderer.Tasks[1].SetException(new PageNavigationException("scroll failed"));
        controller.Pump(_ => new(20, 20, 1));
        Assert.Same(first, controller.Page(tab.Id));
        Assert.Equal(0, controller.ScrollY(tab.Id));
        Assert.Contains("Scroll rendering failed", tab.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedNewDocumentCannotEvictCommittedScrollDom(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 10000);
        var document = Document();
        var viewport = new PageViewport(20, 20, 1);
        await renderer.RenderAsync(document, viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document("<!doctype html><link rel=stylesheet href=https://example.com/style.css>"), viewport, Cancellation));
        var bottom = await renderer.RenderRetainedAsync(document, viewport with { ScrollY = 40 }, Cancellation);
        Assert.Equal(80, bottom.ScrollHeight);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, bottom.Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void WidthReflowClampsToNewExtentAndLargerViewportRetainsDocumentWithoutNavigation()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var html = "<!doctype html><style>*{margin:0;font-size:10px;line-height:10px}</style><p>"
            + string.Join(" ", Enumerable.Repeat("word", 50)) + "</p>";
        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document(html));
        controller.Pump(_ => new(50, 20, 1));
        controller.Scroll(tab.Id, double.MaxValue);
        var previous = controller.ScrollY(tab.Id);
        Assert.True(previous > 0);
        controller.Resize(tab.Id, new(500, 20, 1));
        Assert.True(controller.ScrollY(tab.Id) < previous);
        Assert.Equal(Math.Max(0, controller.Page(tab.Id)!.ScrollHeight - 20), controller.ScrollY(tab.Id));
        controller.Resize(tab.Id, new(500, 200, 1));
        Assert.Equal(0, controller.ScrollY(tab.Id));
        Assert.Single(source.Requests); Assert.Single(tab.History.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HorizontalScrollClampsOnResizeAndRestoresPerHistoryEntry(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);
        var firstUrl = BrowserUrl.Parse("https://example.com/first");
        var secondUrl = BrowserUrl.Parse("https://example.com/second");
        var wide = Document("""
            <!doctype html><style>*{margin:0}#wide{margin-left:80px;width:40px;height:20px}</style><div id=wide></div>
            """);

        controller.Navigate(tab.Id, firstUrl.Href);
        source.Requests[0].Completion.SetResult(new(firstUrl, wide.Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        Assert.Equal(120, controller.Page(tab.Id)!.ScrollWidth);
        ScrollTo(90);

        viewport = new(50, 20, 1);
        controller.Resize(tab.Id, viewport);
        PumpUntil(() => controller.ScrollX(tab.Id) == 70);
        Assert.Equal(70, controller.ScrollX(tab.Id));

        controller.Navigate(tab.Id, secondUrl.Href);
        source.Requests[1].Completion.SetResult(new(secondUrl, "<!doctype html><style>*{margin:0}</style>short", 200, []));
        PumpUntil(() => !tab.IsLoading);
        Assert.Equal(0, controller.ScrollX(tab.Id));

        controller.Back(tab.Id);
        source.Requests[2].Completion.SetResult(new(firstUrl, wide.Html, 200, []));
        PumpUntil(() => !tab.IsLoading);
        Assert.Equal(70, controller.ScrollX(tab.Id));

        void ScrollTo(double offset)
        {
            var previous = controller.Page(tab.Id);
            controller.ScrollBy(tab.Id, offset, 0);
            PumpUntil(() => !ReferenceEquals(previous, controller.Page(tab.Id)));
            Assert.Equal(offset, controller.ScrollX(tab.Id));
        }

        void PumpUntil(Func<bool> complete)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => viewport);
                if (complete()) { return; }
                Thread.Sleep(10);
            } while (timer.Elapsed < TimeSpan.FromSeconds(15));
            Assert.Fail("Horizontal scroll operation timed out.");
        }
    }

    [Fact]
    public void FocusingAnOffscreenLinkScrollsItIntoTheHorizontalViewport()
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(40, 20, 1);
        var document = Document("""
            <!doctype html><style>*{margin:0}#spacer{margin-left:90px}</style>
            <div id=spacer><a href=/next>target</a></div>
            """);

        controller.Navigate(tab.Id, document.Url.Href);
        source.Requests[0].Completion.SetResult(document);
        controller.Pump(_ => viewport);
        var target = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.True(target.DocumentX >= viewport.Width, $"documentX={target.DocumentX}; rects={target.Rects.Count}");
        Assert.Equal(0, controller.ScrollX(tab.Id));
        Assert.Equal(0, controller.MoveLinkFocus(tab.Id));
        controller.Pump(_ => viewport);

        Assert.True(controller.ScrollX(tab.Id) > 0, $"documentX={target.DocumentX}; rects={target.Rects.Count}");
        Assert.Contains(controller.Page(tab.Id)!.LinkTargets[0].Rects,
            rect => rect.X >= 0 && rect.X < viewport.Width);
    }

    [Fact]
    public void NestedOverflowWheelConsumesContainerThenChainsRemainingToDocument()
    {
        var source = new ControllerTests.Source();
        var renderer = new PendingRenderer();
        using var controller = new BrowserController(() => source, () => renderer);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(20, 20, 1);

        controller.Navigate(tab.Id, "https://example.com/");
        source.Requests[0].Completion.SetResult(Document());
        controller.Pump(_ => viewport);
        renderer.Complete(0, scrollContainers: [new(0, -1, new(0, 0, 20, 10), 0, 20)]);
        controller.Pump(_ => viewport);

        var remaining = controller.ScrollOverflowAt(tab.Id, 5, 5, 30);
        Assert.Equal(10, remaining);
        Assert.Equal(0, controller.ScrollY(tab.Id));
        Assert.Contains(renderer.Viewports[1].ScrollOffsets, offset => offset.Index == 0 && offset.Offset == 20);
        renderer.Complete(1, scrollContainers: [new(0, -1, new(0, 0, 20, 10), 20, 20)]);
        controller.Pump(_ => viewport);

        controller.Scroll(tab.Id, remaining);
        renderer.Complete(2, scrollContainers: [new(0, -1, new(0, 0, 20, 10), 20, 20)]);
        controller.Pump(_ => viewport);
        Assert.True(controller.ScrollY(tab.Id) > 0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(10_000_001)]
    public void BrowserPageRejectsInvalidScrollHeight(double height)
    {
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(new(1, 1, []), fonts, cancellationToken: Cancellation);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrowserPage(frame, "", "") { ScrollHeight = height });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(10_000_001)]
    public void BrowserPageRejectsInvalidScrollWidth(double width)
    {
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(new(1, 1, []), fonts, cancellationToken: Cancellation);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrowserPage(frame, "", "") { ScrollWidth = width });
    }

    private sealed class PendingRenderer : IPageRenderer
    {
        internal List<TaskCompletionSource<BrowserPage>> Tasks { get; } = [];
        internal List<CancellationToken> Tokens { get; } = [];
        internal List<PageViewport> Viewports { get; } = [];
        internal int InitialCalls { get; private set; }
        public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            InitialCalls++;
            return RenderRetainedAsync(page, viewport, cancellationToken);
        }
        public Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            Viewports.Add(viewport); Tokens.Add(cancellationToken); Tasks.Add(new());
            return Tasks[^1].Task;
        }
        internal void Complete(int index, double height = 80, IReadOnlyList<PageScrollContainer>? scrollContainers = null)
        {
            using var fonts = new PaintFontRegistry();
            var viewport = Viewports[index];
            Tasks[index].SetResult(new(CpuRasterizer.Render(new(viewport.Width, viewport.Height, []), fonts,
                viewport.Scale, cancellationToken: Cancellation), viewport.ScrollY.ToString(System.Globalization.CultureInfo.InvariantCulture), "Ready")
            {
                ScrollHeight = height,
                ScrollContainers = scrollContainers ?? Array.Empty<PageScrollContainer>()
            });
        }
        public void Dispose() { }
    }
}
