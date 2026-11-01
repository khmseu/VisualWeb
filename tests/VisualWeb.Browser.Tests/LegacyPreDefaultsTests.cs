using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LegacyPreDefaultsTests
{
    [Theory]
    [InlineData(false, "xmp")]
    [InlineData(true, "xmp")]
    [InlineData(false, "plaintext")]
    [InlineData(true, "plaintext")]
    public async Task RawMarkupRemainsLiteralThroughRenderingAndRetainedResize(bool process, string tag)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        const string prefix = "<!doctype html><title>before</title><style>*{margin:0}body{font-size:16px;line-height:1}</style>"
            + "<a id=top href='#top'>live</a><script>document.title='after'</script>";
        const string literal = "a  b\n<a href='/fake'>fake</a><script>document.title='bad'</script>";
        string suffix = tag == "plaintext" ? "</plaintext><a href='/after'>after</a>" : "";
        var actual = Document(prefix + "<" + tag + " id=raw>" + literal + suffix + (tag == "xmp" ? "</xmp>" : ""));
        var escaped = (literal + suffix).Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal);
        var referenceDocument = Document(prefix + "<pre id=raw>" + escaped + "</pre>");
        var narrow = new PageViewport(160, 140, 1);
        var wide = new PageViewport(240, 140, 1);
        BrowserPage expected, expectedWide;
        using (var reference = new StaticPageRenderer(font, 100000, executeInlineScripts: true))
        {
            expected = await reference.RenderAsync(referenceDocument, narrow, cancellation);
            reference.CommitDocument(referenceDocument.DocumentId);
            expectedWide = await reference.RenderRetainedAsync(referenceDocument, wide, cancellation);
        }
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(rendererPath, font, executeInlineScripts: true)
            : new StaticPageRenderer(font, 100000, executeInlineScripts: true);
        var rendered = await renderer.RenderAsync(actual, narrow, cancellation);
        AssertEquivalent(expected, rendered);
        renderer.CommitDocument(actual.DocumentId);
        AssertEquivalent(expectedWide, await renderer.RenderRetainedAsync(actual, wide, cancellation));

        static void AssertEquivalent(BrowserPage expected, BrowserPage actual)
        {
            Assert.Equal("after", actual.Title);
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            Assert.Equal(expected.FragmentTargets.ToArray(), actual.FragmentTargets.ToArray());
            var link = Assert.Single(actual.LinkTargets);
            Assert.Equal("https://example.test/index.html#top", link.Url);
            Assert.Equal(Assert.Single(expected.LinkTargets).Rects.ToArray(), link.Rects.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListingMarkupRemainsLiveWithPreGeometry(bool process)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        const string prefix = "<!doctype html><style>*{margin:0}body{font-size:16px;line-height:1}</style>";
        const string content = "\n<a id=top href='#top'>a  b</a>\nc d";
        var actual = Document(prefix + "<listing id=raw>" + content + "</listing>");
        var referenceDocument = Document(prefix + "<pre id=raw>" + content + "</pre>");
        var viewport = new PageViewport(160, 140, 1);
        BrowserPage expected;
        using (var reference = new StaticPageRenderer(font, 100000))
        {
            expected = await reference.RenderAsync(referenceDocument, viewport, cancellation);
        }
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(rendererPath, font)
            : new StaticPageRenderer(font, 100000);
        var rendered = await renderer.RenderAsync(actual, viewport, cancellation);
        Assert.Equal(expected.Frame.Pixels.ToArray(), rendered.Frame.Pixels.ToArray());
        Assert.Equal(expected.TextTargets.ToArray(), rendered.TextTargets.ToArray());
        Assert.Equal(expected.FragmentTargets.ToArray(), rendered.FragmentTargets.ToArray());
        var link = Assert.Single(rendered.LinkTargets);
        Assert.Equal("https://example.test/index.html#top", link.Url);
        Assert.Equal(Assert.Single(expected.LinkTargets).Rects.ToArray(), link.Rects.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListingScriptRunsOnlyWhenOptedInAndDoesNotRerunOnResize(bool process)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        var page = Document("""
            <!doctype html><title>before</title><style>body{font-size:23px;line-height:23px}</style>
            <listing id=target><a id=live>live</a><script>
            if (document.title === 'before') {
                document.title = 'listing ran';
                document.getElementById('live').setAttribute('href', '#target');
            } else {
                document.title = 'reran';
            }
            </script></listing>
            """);
        var viewport = new PageViewport(160, 140, 1);
        using IPageRenderer disabled = process
            ? new ProcessPageRenderer(rendererPath, font)
            : new StaticPageRenderer(font, 100000);
        var staticPage = await disabled.RenderAsync(page, viewport, cancellation);
        Assert.Equal("before", staticPage.Title);
        Assert.Empty(staticPage.LinkTargets);

        using IPageRenderer enabled = process
            ? new ProcessPageRenderer(rendererPath, font, executeInlineScripts: true)
            : new StaticPageRenderer(font, 100000, executeInlineScripts: true);
        var rendered = await enabled.RenderAsync(page, viewport, cancellation);
        Assert.Equal("listing ran", rendered.Title);
        Assert.Equal("https://example.test/index.html#target", Assert.Single(rendered.LinkTargets).Url);
        enabled.CommitDocument(page.DocumentId);
        var retained = await enabled.RenderRetainedAsync(page, new PageViewport(240, 140, 1), cancellation);
        Assert.Equal("listing ran", retained.Title);
        Assert.Equal("https://example.test/index.html#target", Assert.Single(retained.LinkTargets).Url);
    }

    private static LoadedPage Document(string html) =>
        new(BrowserUrl.Parse("https://example.test/index.html"), html, 200, []);
}
