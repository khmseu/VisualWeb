using VisualWeb.Core.Url;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LinkColorDefaultsTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LinkColorMatchesIndependentPixelsThroughHitSelectionResizeAndFragmentHistory(
        bool process, bool authorOverride)
    {
        double width;
        using (var font = new TextFont(FontPath))
        {
            width = Math.Ceiling(Math.Max(font.Shape("well-", 20, Cancellation).Width,
                font.Shape("known", 20, Cancellation).Width));
            Assert.True(font.Shape("well-known", 20, Cancellation).Width > width);
        }
        var narrow = new PageViewport(width, 120, 1);
        var wide = new PageViewport(200, 120, 1);
        var actualDocument = Document(Markup(authorOverride ? "a{color:#008000}" : "", ""));
        var referenceDocument = Document(Markup(authorOverride ? "" : "a{color:#0000ee}",
            authorOverride ? " style='color:#008000'" : ""));
        var blackDocument = Document(Markup("a{color:#000000}", ""));
        BrowserPage expectedNarrow, expectedWide, blackWide;
        using (var reference = new StaticPageRenderer(FontPath, 100000))
        {
            expectedNarrow = reference.Render(referenceDocument, narrow, Cancellation);
            reference.CommitDocument(referenceDocument.DocumentId);
            expectedWide = reference.Render(referenceDocument, wide, Cancellation, reuseDocument: true);
            blackWide = reference.Render(blackDocument, wide, Cancellation);
        }
        Assert.NotEqual(blackWide.Frame.Pixels.ToArray(), expectedWide.Frame.Pixels.ToArray());

        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => process
            ? new ProcessPageRenderer(RendererPath, FontPath)
            : new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.test/index.html");
        source.Requests[0].Completion.SetResult(actualDocument);
        PumpUntil(() => controller.Page(tab.Id) is not null, narrow);

        var first = controller.Page(tab.Id)!;
        AssertEquivalent(expectedNarrow, first, 2);
        Assert.True(controller.SelectAllVisibleText(tab.Id));
        Assert.Equal("well-\nknown", controller.SelectedText(tab.Id));
        controller.Resize(tab.Id, wide);
        PumpUntil(() => !ReferenceEquals(first, controller.Page(tab.Id)), wide);
        var resized = controller.Page(tab.Id)!;
        AssertEquivalent(expectedWide, resized, 1);
        Assert.True(controller.SelectAllVisibleText(tab.Id));
        Assert.Equal("well-known", controller.SelectedText(tab.Id));
        Assert.Single(source.Requests);

        var hit = Assert.Single(Assert.Single(resized.LinkTargets).Rects);
        Assert.True(controller.ActivateLink(tab.Id, hit.X + hit.Width / 2, hit.Y + hit.Height / 2, wide));
        Assert.EndsWith("#top", tab.History.Current!.Href, StringComparison.Ordinal);
        AssertEquivalent(expectedWide, controller.Page(tab.Id)!, 1);
        controller.Resize(tab.Id, narrow);
        PumpUntil(() => !ReferenceEquals(resized, controller.Page(tab.Id)), narrow);
        var afterVisit = controller.Page(tab.Id)!;
        AssertEquivalent(expectedNarrow, afterVisit, 2);
        controller.Back(tab.Id);
        Assert.Equal("https://example.test/index.html", tab.History.Current!.Href);
        controller.Resize(tab.Id, wide);
        PumpUntil(() => !ReferenceEquals(afterVisit, controller.Page(tab.Id)), wide);
        AssertEquivalent(expectedWide, controller.Page(tab.Id)!, 1);
        Assert.Single(source.Requests);

        static void AssertEquivalent(BrowserPage expected, BrowserPage actual, int lineCount)
        {
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            var reference = Assert.Single(expected.LinkTargets);
            var link = Assert.Single(actual.LinkTargets);
            Assert.Equal("https://example.test/index.html#top", link.Url);
            Assert.Equal(reference.Rects.ToArray(), link.Rects.ToArray());
            Assert.Equal(lineCount, link.Rects.Select(rect => rect.Y).Distinct().Count());
            Assert.All(link.Rects, rect => Assert.True(link.Contains(rect.X + rect.Width / 2, rect.Y + rect.Height / 2)));
        }

        void PumpUntil(Func<bool> condition, PageViewport viewport)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => viewport);
                if (condition()) { return; }
                Thread.Sleep(5);
            } while (DateTime.UtcNow < deadline);
            Assert.Null(tab.Error);
            Assert.True(condition());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptHrefMutationRecomputesColorsAndRetainedResizeDoesNotRerunScript(bool process)
    {
        const string common = "*{margin:0}body{font-size:20px;line-height:1.5}";
        var actual = Document("""
            <!doctype html><title>before</title><style>*{margin:0}body{font-size:20px;line-height:1.5}</style>
            <a id=first href="https://example.test/old"><span>First</span></a><br><a id=second><span>Second</span></a>
            <script>
            let first=document.getElementById('first'), second=document.getElementById('second');
            if (first.hasAttribute('href')) {
                first.removeAttribute('href'); second.setAttribute('href','https://example.test/next');
                document.title='after';
            } else {
                first.setAttribute('href','https://example.test/old'); second.removeAttribute('href');
                document.title='reran';
            }
            </script>
            """);
        var referenceDocument = Document("<!doctype html><title>after</title><style>" + common
            + "#first{color:#000000}#second{color:#0000ee}</style>"
            + "<a id=first><span>First</span></a><br><a id=second href='https://example.test/next'><span>Second</span></a>");
        var blackDocument = Document("<!doctype html><title>after</title><style>" + common
            + "a{color:#000000}</style>"
            + "<a id=first><span>First</span></a><br><a id=second href='https://example.test/next'><span>Second</span></a>");
        var firstViewport = new PageViewport(160, 100, 1);
        var wideViewport = new PageViewport(200, 100, 1);
        BrowserPage expected, expectedRetained, black;
        using (var reference = new StaticPageRenderer(FontPath, 100000))
        {
            expected = reference.Render(referenceDocument, firstViewport, Cancellation);
            reference.CommitDocument(referenceDocument.DocumentId);
            expectedRetained = reference.Render(referenceDocument, wideViewport, Cancellation, reuseDocument: true);
            black = reference.Render(blackDocument, firstViewport, Cancellation);
        }
        Assert.NotEqual(black.Frame.Pixels.ToArray(), expected.Frame.Pixels.ToArray());

        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 100000, executeInlineScripts: true);
        var rendered = await renderer.RenderAsync(actual, firstViewport, Cancellation);
        AssertMutation(expected, rendered);
        renderer.CommitDocument(actual.DocumentId);
        var retained = await renderer.RenderRetainedAsync(actual, wideViewport, Cancellation);
        AssertMutation(expectedRetained, retained);

        static void AssertMutation(BrowserPage expected, BrowserPage actual)
        {
            Assert.Equal("after", actual.Title);
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            var link = Assert.Single(actual.LinkTargets);
            Assert.Equal("https://example.test/next", link.Url);
            Assert.Equal(Assert.Single(expected.LinkTargets).Rects.ToArray(), link.Rects.ToArray());
            Assert.All(link.Rects, rect => Assert.True(link.Contains(rect.X + rect.Width / 2, rect.Y + rect.Height / 2)));
        }
    }

    private static string Markup(string rule, string inline) =>
        "<!doctype html><style>*{margin:0}body{font-size:20px;line-height:1.5}" + rule
        + "</style><a id=top href='#top'" + inline + "><span>well-known</span></a>";

    private static LoadedPage Document(string html) =>
        new(BrowserUrl.Parse("https://example.test/index.html"), html, 200, []);
}
