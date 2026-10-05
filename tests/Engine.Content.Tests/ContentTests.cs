using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class ContentTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private const string Css = "html,body,p,div{display:block} head{display:none} *{margin:0;font-size:16px;line-height:20px}";

    [Fact]
    public void DecodedHtmlReachesNativeFrameAndRetainsAllStageResults()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var result = OfflinePageRenderer.Render("<!doctype html><p id=x>office</p>", [new(Css + " p{color:red}")],
            text, fonts, 100, 50, new() { IncludeUserAgentStyle = false, Scale = 2 }, Cancellation);
        Assert.NotNull(result.Html.Document.GetElementById("x"));
        Assert.Empty(result.Styles.Diagnostics);
        Assert.NotNull(result.Layout.Root);
        var run = Assert.Single(result.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.True(run.Glyphs.Count < 6);
        Assert.Equal(new VisualWeb.Platform.Abstractions.PixelSize(200, 100), result.Frame.Size);
        Assert.Contains(result.Frame.Pixels.ToArray(), value => value < 255);
    }

    [Fact]
    public void EmbeddedStylesAreNotAutomaticallyCollectedAndCallerSourceOrderIsHonored()
    {
        using var fonts = new PaintFontRegistry();
        var result = OfflinePageRenderer.Render("<!doctype html><style>div{background-color:red}</style><div></div>",
            [new(Css + " div{width:10px;height:10px;background-color:red}"), new("div{background-color:blue}")],
            new EmptyShaper(), fonts, 20, 20, new() { IncludeUserAgentStyle = false }, Cancellation);
        Assert.Equal(new CssColor(0, 0, 255), Assert.Single(result.DisplayList.Commands.OfType<FillRectangle>()).Color);
    }

    [Fact]
    public void UnsupportedCssHtmlLayoutAndImportsNeverBecomeApproximateFrames()
    {
        using var fonts = new PaintFontRegistry();
        Assert.Throws<UnsupportedPaintException>(() => OfflinePageRenderer.Render("<!doctype html><div></div>",
            [new(Css + " div{border:1px dashed}")], new EmptyShaper(), fonts, 20, 20,
            new() { IncludeUserAgentStyle = false }, Cancellation));
        Assert.Throws<VisualWeb.Engine.Layout.UnsupportedLayoutException>(() => OfflinePageRenderer.Render("<!doctype html><div></div>",
            [new(Css + " @import 'https://example.com/x'")], new EmptyShaper(), fonts, 20, 20,
            new() { IncludeUserAgentStyle = false }, Cancellation));
        Assert.Throws<VisualWeb.Engine.Html.UnsupportedHtmlException>(() => OfflinePageRenderer.Render("<table></table>",
            [new(Css)], new EmptyShaper(), fonts, 20, 20, cancellationToken: Cancellation));
    }

    [Fact]
    public void OptionsCancellationAndBlankHiddenPageAreExplicit()
    {
        using var fonts = new PaintFontRegistry();
        var result = OfflinePageRenderer.Render("<!doctype html>", [new("html{display:none}")],
            new EmptyShaper(), fonts, 3, 2, cancellationToken: Cancellation);
        Assert.Null(result.Layout.Root);
        Assert.Empty(result.DisplayList.Commands);
        Assert.All(result.Frame.Pixels.ToArray(), value => Assert.Equal(255, value));
        Assert.Throws<PaintLimitException>(() => OfflinePageRenderer.Render("<!doctype html>", [new("html{display:none}")],
            new EmptyShaper(), fonts, 3, 2, new() { Paint = new() { MaxPixels = 5 } }, Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => OfflinePageRenderer.Render("", [], new EmptyShaper(), fonts,
            20, 20, cancellationToken: canceled.Token));
    }
    private sealed class EmptyShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This page should not shape text.");
    }

    [Fact]
    public void CallerParsedDocumentIsRetainedAndValidatesViewportBeforeStyleProcessing()
    {
        var parsed = VisualWeb.Engine.Html.HtmlParser.Parse("<!doctype html>", cancellationToken: Cancellation);
        using var fonts = new PaintFontRegistry();
        var page = OfflinePageRenderer.RenderParsed(parsed, [new("html{display:none}")], new EmptyShaper(), fonts,
            2, 2, cancellationToken: Cancellation);
        Assert.Same(parsed, page.Html);
        Assert.Throws<ArgumentOutOfRangeException>(() => OfflinePageRenderer.RenderParsed(parsed, [],
            new EmptyShaper(), fonts, double.NaN, 2, cancellationToken: Cancellation));
        Assert.Throws<ArgumentNullException>(() => OfflinePageRenderer.RenderParsed(parsed, [],
            new EmptyShaper(), fonts, 2, 2, new() { Css = null! }, Cancellation));
    }
}
