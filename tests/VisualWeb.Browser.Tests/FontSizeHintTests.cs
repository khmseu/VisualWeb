using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class FontSizeHintTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptMutationAndRetainedResizePreserveFontSize(bool process)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var actual = Document("""
            <!doctype html><title>before</title><style>*{margin:0}body{font-size:23px;line-height:1.5}</style>
            <font id=target size=2><span>Sample page</span></font>
            <script>
            let target=document.getElementById('target');
            if (target.getAttribute('size') === '2') {
                target.setAttribute('size','+1'); document.title='after';
            } else { target.setAttribute('size','7'); document.title='reran'; }
            </script>
            """);
        var reference = Document("""
            <!doctype html><title>after</title><style>*{margin:0}body{font-size:23px;line-height:1.5}</style>
            <span style="font-size:large"><span>Sample page</span></span>
            """);
        var narrow = new PageViewport(160, 100, 1);
        var wide = new PageViewport(200, 100, 1);
        BrowserPage expected, expectedWide;
        using (var renderer = new StaticPageRenderer(FontPath, 100000))
        {
            expected = renderer.Render(reference, narrow, cancellation);
            renderer.CommitDocument(reference.DocumentId);
            expectedWide = renderer.Render(reference, wide, cancellation, reuseDocument: true);
        }
        using IPageRenderer tested = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 100000, executeInlineScripts: true);
        var rendered = await tested.RenderAsync(actual, narrow, cancellation);
        AssertEquivalent(expected, rendered);
        tested.CommitDocument(actual.DocumentId);
        AssertEquivalent(expectedWide, await tested.RenderRetainedAsync(actual, wide, cancellation));
    }

    private static void AssertEquivalent(BrowserPage expected, BrowserPage actual)
    {
        Assert.Equal("after", actual.Title);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
    }

    private static LoadedPage Document(string html) =>
        new(BrowserUrl.Parse("https://example.test/index.html"), html, 200, []);
}
