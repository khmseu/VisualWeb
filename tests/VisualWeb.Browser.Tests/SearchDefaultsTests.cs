using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class SearchDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchFormMatchesDivThroughNativeRenderingAndRetainedResize(bool process)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        const string prefix = "<!doctype html><style>*{margin:0}body{font-size:16px;line-height:1}</style>";
        const string contents = "<div>Find</div><form action='/find'><input type=search name=q value='a b'><button>Go</button></form>";
        var actual = Document(prefix + "<search id=region>" + contents + "</search>");
        var reference = Document(prefix + "<div id=region>" + contents + "</div>");
        var narrow = new PageViewport(160, 140, 1);
        var wide = new PageViewport(240, 140, 1);
        BrowserPage expected, expectedWide;
        using (var renderer = new StaticPageRenderer(font, 100000))
        {
            expected = await renderer.RenderAsync(reference, narrow, cancellation);
            renderer.CommitDocument(reference.DocumentId);
            expectedWide = await renderer.RenderRetainedAsync(reference, wide, cancellation);
        }
        using IPageRenderer tested = process ? new ProcessPageRenderer(rendererPath, font) : new StaticPageRenderer(font, 100000);
        var page = await tested.RenderAsync(actual, narrow, cancellation);
        Compare(expected, page);
        Assert.Equal(2, page.FormControls.Count);
        Assert.Equal("https://example.test/find", Assert.Single(page.Forms).Action);
        Assert.All(page.FormControls, control => Assert.Equal(0, control.Form));
        Assert.Equal("search", page.FormControls[0].Kind);
        Assert.Equal("q", page.FormControls[0].Name);
        Assert.Equal("a b", page.FormControls[0].Value);
        tested.CommitDocument(actual.DocumentId);
        Compare(expectedWide, await tested.RenderRetainedAsync(actual, wide, cancellation));

        static void Compare(BrowserPage expected, BrowserPage actual)
        {
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            Assert.Equal(expected.FragmentTargets.ToArray(), actual.FragmentTargets.ToArray());
            Assert.Equal(expected.FormControls.Select(x => x.Rect), actual.FormControls.Select(x => x.Rect));
            Assert.Equal(expected.Forms, actual.Forms);
            Assert.Equal(expected.FormControls.Select(x => x.Form), actual.FormControls.Select(x => x.Form));
        }
    }

    private static LoadedPage Document(string html) => new(BrowserUrl.Parse("https://example.test/index.html"), html, 200, []);
}
