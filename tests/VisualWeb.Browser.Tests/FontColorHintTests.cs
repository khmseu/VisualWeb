using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class FontColorHintTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptMutationAndRetainedResizePreserveFontHint(bool process)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var actual = Document("""
            <!doctype html><title>before</title><style>*{margin:0}body{font-size:20px;line-height:1.5}</style>
            <font id=target color=blue><span>Sample</span></font>
            <script>
            let target=document.getElementById('target');
            if (target.getAttribute('color') === 'blue') {
                target.setAttribute('color','chucknorris'); document.title='after';
            } else { target.setAttribute('color','green'); document.title='reran'; }
            </script>
            """);
        var referenceDocument = Document("""
            <!doctype html><title>after</title><style>*{margin:0}body{font-size:20px;line-height:1.5}</style>
            <span style="color:#c00000"><span>Sample</span></span>
            """);
        var narrow = new PageViewport(160, 100, 1);
        var wide = new PageViewport(200, 100, 1);
        BrowserPage expected, expectedWide;
        using (var reference = new StaticPageRenderer(FontPath, 100000))
        {
            expected = reference.Render(referenceDocument, narrow, cancellation);
            reference.CommitDocument(referenceDocument.DocumentId);
            expectedWide = reference.Render(referenceDocument, wide, cancellation, reuseDocument: true);
        }
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 100000, executeInlineScripts: true);
        var rendered = await renderer.RenderAsync(actual, narrow, cancellation);
        AssertEquivalent(expected, rendered);
        renderer.CommitDocument(actual.DocumentId);
        var retained = await renderer.RenderRetainedAsync(actual, wide, cancellation);
        AssertEquivalent(expectedWide, retained);
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
