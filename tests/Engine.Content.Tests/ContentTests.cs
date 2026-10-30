using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class ContentTests
{
    [Fact]
    public void AbsoluteLengthsMatchPixelStylesThroughNativeRenderingAndResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div>Sample</div>";
        var absolute = new[] { new CssStyleSource(
            "*{margin:0}div{width:1in;height:18pt;border:.75pt solid darkslategrey;background:lightgray;font:12pt/18pt serif}") };
        var pixels = new[] { new CssStyleSource(
            "*{margin:0}div{width:96px;height:24px;border:1px solid darkslategrey;background:lightgray;font:16px/24px serif}") };
        var actual = OfflinePageRenderer.Render(html, absolute, text, fonts, 120, 45, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, pixels, text, fonts, 120, 45, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, absolute, text, fonts, 110, 45, cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, pixels, text, fonts, 110, 45,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("lightgray")]
    [InlineData("none lightgray")]
    [InlineData("currentColor")]
    [InlineData("none")]
    public void BackgroundShorthandMatchesLonghandNativePixelsAndRetainedResize(string background)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var shorthand = new[] { new CssStyleSource("*{margin:0}div{height:20px;color:lightgray;background:" + background + "}") };
        var color = background == "none" ? "transparent" : "lightgray";
        const string html = "<!doctype html><div></div>";
        var result = OfflinePageRenderer.Render(html, shorthand, text, fonts, 40, 30, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html,
            [new("*{margin:0}div{height:20px;background-color:" + color + "}")],
            text, fonts, 40, 30, cancellationToken: Cancellation);
        Assert.Empty(result.Styles.Diagnostics);
        Assert.Equal(expected.Frame.Pixels.ToArray(), result.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(result.Html, shorthand, text, fonts, 30, 30, cancellationToken: Cancellation);
        Assert.Same(result.Html.Document, resized.Html.Document);
        Assert.Equal(result.Frame.Pixels.Span[..4].ToArray(), resized.Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void StandardNamedBackgroundAndBorderColorsReachExactNativePixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var result = OfflinePageRenderer.Render("<!doctype html><div></div>",
            [new("*{margin:0}div{height:20px;background-color:lightgray;border:2px solid darkslategrey}")],
            text, fonts, 40, 30, cancellationToken: Cancellation);
        Assert.Empty(result.Styles.Diagnostics);
        Assert.Equal(new byte[] { 79, 79, 47, 255 }, result.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 211, 211, 211, 255 },
            result.Frame.Pixels.Span.Slice(3 * result.Frame.Stride + 3 * 4, 4).ToArray());
    }

    [Theory]
    [InlineData("pre")]
    [InlineData("nowrap")]
    public void NonWrappingUrlTextReachesNativePixelsAndRetainedResize(string mode)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string url = "https://example.test/a-b?x=1";
        var styles = new[] { new CssStyleSource("*{margin:0}p{white-space:" + mode + "}") };
        var result = OfflinePageRenderer.Render("<!doctype html><p>" + url + "</p>", styles,
            text, fonts, 40, 40, cancellationToken: Cancellation);
        var resized = OfflinePageRenderer.RenderParsed(result.Html, styles, text, fonts, 60, 40,
            cancellationToken: Cancellation);
        Assert.NotEmpty(Assert.Single(result.DisplayList.Commands.OfType<DrawGlyphRun>()).Glyphs);
        Assert.NotEmpty(Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>()).Glyphs);
        Assert.True(result.ScrollWidth > 60);
        Assert.Equal(result.ScrollWidth, resized.ScrollWidth);
        Assert.Same(result.Html.Document, resized.Html.Document);
        Assert.Contains(result.Frame.Pixels.ToArray().Where((_, index) => index % 4 != 3), value => value == 0);
    }

    [Fact]
    public void AutomaticTablesPaintNativeCellBackgroundsAndRetainTheirParsedDocument()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><table><tr><td id=a>A</td></tr><tr><td id=b>B</td></tr></table>";
        var styles = new[] { new CssStyleSource("""
            *{margin:0;line-height:20px}table{border-spacing:0}td{padding:0;width:40px;height:20px}
            #a{background-color:red}#b{background-color:blue}
            """) };
        var result = OfflinePageRenderer.Render(html, styles, text, fonts, 100, 40, cancellationToken: Cancellation);
        var resized = OfflinePageRenderer.RenderParsed(result.Html, styles, text, fonts, 80, 40, cancellationToken: Cancellation);
        Assert.Empty(result.Styles.Diagnostics);
        Assert.Same(result.Html.Document, resized.Html.Document);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, result.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, result.Frame.Pixels.Span.Slice(20 * result.Frame.Stride, 4).ToArray());
        Assert.Equal(2, result.DisplayList.Commands.OfType<DrawGlyphRun>().Count());
        Assert.Equal(40, result.ScrollHeight);
        Assert.Equal(80, resized.Frame.Size.Width);
    }

    [Fact]
    public void SimpleLegacyDoctypeDocumentReachesAnOfflineNativeFrame()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = """
            <!doctype html PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "https://invalid.example/never-fetch.dtd">
            <p>Simple document</p>
            """;
        var result = OfflinePageRenderer.Render(html, [], text, fonts, 200, 80, cancellationToken: Cancellation);
        Assert.Equal(VisualWeb.Engine.Dom.DomDocumentMode.LimitedQuirks, result.Html.Document.Mode);
        Assert.Contains(result.Html.Errors, error => error.Code == "invalid-doctype");
        Assert.NotEmpty(result.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(200, result.Frame.Size.Width);
    }

    [Fact]
    public void CommonListStyleNoneCssRendersWithoutDiagnosticsOrMarkers()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li>Navigation</li></ul>";
        var result = OfflinePageRenderer.Render(html, [new("ul{list-style:none}*{margin:0}")],
            text, fonts, 200, 80, cancellationToken: Cancellation);
        Assert.Empty(result.Styles.Diagnostics);
        Assert.Empty(result.DisplayList.Commands.OfType<FillEllipse>());
        Assert.Single(result.DisplayList.Commands.OfType<DrawGlyphRun>());
    }

    [Fact]
    public void OrderedListsReachNativePixelsAndRetainTheirParsedNumberingOnResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ol reversed start=5><li>First</li><li value=-1>Second</li></ol>";
        var styles = new[] { new CssStyleSource("*{margin:0;font-size:16px;line-height:20px}li{color:red}") };
        var first = OfflinePageRenderer.Render(html, styles, text, fonts, 200, 80, cancellationToken: Cancellation);
        var resized = OfflinePageRenderer.RenderParsed(first.Html, styles, text, fonts, 160, 80,
            cancellationToken: Cancellation);
        Assert.Empty(first.Styles.Diagnostics);
        Assert.Equal(4, first.DisplayList.Commands.OfType<DrawGlyphRun>().Count());
        Assert.Equal(4, resized.DisplayList.Commands.OfType<DrawGlyphRun>().Count());
        Assert.Same(first.Html.Document, resized.Html.Document);
        var markerPixels = Enumerable.Range(0, 40).SelectMany(y => Enumerable.Range(0, 40).Select(x => (y * 200 + x) * 4));
        Assert.Contains(markerPixels, offset => first.Frame.Pixels.Span[offset + 2] == 255
            && first.Frame.Pixels.Span[offset + 1] < 255);
    }

    [Fact]
    public void SimpleUnorderedListReachesNativeFrameAndRetainedRepaint()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li>First item</li><li>Second item</li></ul>";
        var styles = new[] { new CssStyleSource("*{font-size:16px;line-height:20px}") };
        var first = OfflinePageRenderer.Render(html, styles, text, fonts, 200, 80, cancellationToken: Cancellation);
        var resized = OfflinePageRenderer.RenderParsed(first.Html, styles, text, fonts, 160, 80,
            cancellationToken: Cancellation);
        Assert.Empty(first.Styles.Diagnostics);
        Assert.Equal(2, first.DisplayList.Commands.OfType<FillEllipse>().Count());
        Assert.Equal(2, resized.DisplayList.Commands.OfType<FillEllipse>().Count());
        Assert.Same(first.Html.Document, resized.Html.Document);
        Assert.Contains(first.Frame.Pixels.ToArray().Where((_, index) => index % 4 != 3), value => value == 0);
        Assert.All(first.Frame.Pixels.ToArray().Where((_, index) => index % 4 == 3), alpha => Assert.Equal(255, alpha));
    }

    [Fact]
    public void ScrollingMovesTextOnlyInPaintAndKeepsLayoutViewportAndOpaqueBackground()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div></div><p>Below viewport</p>";
        var styles = new[] { new CssStyleSource(Css + " div{height:40px} body{background-color:blue} p{color:white}") };
        var first = OfflinePageRenderer.Render(html, styles, text, fonts, 200, 20,
            new() { IncludeUserAgentStyle = false }, Cancellation);
        var scrolled = OfflinePageRenderer.RenderParsed(first.Html, styles, text, fonts, 200, 20,
            new() { IncludeUserAgentStyle = false, ScrollY = 40 }, Cancellation);
        Assert.Same(first.Html.Document, scrolled.Html.Document);
        Assert.Equal(first.Layout.Root!.BorderBox, scrolled.Layout.Root!.BorderBox);
        Assert.Equal(200, scrolled.Layout.ViewportWidth); Assert.Equal(20, scrolled.Layout.ViewportHeight);
        Assert.Equal(60, scrolled.ScrollHeight);
        Assert.Equal(first.Frame.Size, scrolled.Frame.Size);
        Assert.Equal(first.DisplayList.Commands.OfType<DrawGlyphRun>().SelectMany(run => run.Glyphs),
            scrolled.DisplayList.Commands.OfType<DrawGlyphRun>().SelectMany(run => run.Glyphs));
        var pixels = scrolled.Frame.Pixels.ToArray();
        Assert.Contains(pixels.Where((_, index) => index % 4 == 1), channel => channel > 0);
        Assert.All(pixels.Where((_, index) => index % 4 == 3), alpha => Assert.Equal(255, alpha));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels[^4..]);
    }

    [Fact]
    public void HorizontalScrollingRevealsOverflowWithoutChangingLayoutOrFrameDimensions()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div></div>";
        var styles = new[]
        {
            new CssStyleSource(Css + "html{background-color:blue}div{margin-left:200px;width:100px;height:20px;background-color:red}")
        };
        var first = OfflinePageRenderer.Render(html, styles, text, fonts, 200, 20,
            new() { IncludeUserAgentStyle = false }, Cancellation);
        var scrolled = OfflinePageRenderer.RenderParsed(first.Html, styles, text, fonts, 200, 20,
            new() { IncludeUserAgentStyle = false, ScrollX = 100 }, Cancellation);

        Assert.Equal(300, first.ScrollWidth);
        Assert.Equal(first.Layout.Root!.BorderBox, scrolled.Layout.Root!.BorderBox);
        Assert.Equal(first.Frame.Size, scrolled.Frame.Size);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, first.Frame.Pixels.Span[(199 * 4)..(200 * 4)].ToArray());
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, scrolled.Frame.Pixels.Span[(100 * 4)..(101 * 4)].ToArray());
    }

    [Fact]
    public void ExcessiveDocumentHeightFailsBeforeRasterAllocation()
    {
        using var fonts = new PaintFontRegistry();
        Assert.Throws<PaintLimitException>(() => OfflinePageRenderer.Render("<!doctype html><div></div>",
            [new(Css + " div{height:10000001px}")], new FontSet(), fonts, 20, 20,
            new() { IncludeUserAgentStyle = false }, Cancellation));
    }

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
        Assert.Throws<VisualWeb.Engine.Layout.UnsupportedLayoutException>(() => OfflinePageRenderer.Render("<img>",
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
