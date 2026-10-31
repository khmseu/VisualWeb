using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class ContentTests
{
    [Fact]
    public void MenuListMatchesEquivalentUnorderedListNativePixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string items = "<li>one</li><li>two<menu><li>three</li></menu></li>";
        const string referenceItems = "<li>one</li><li>two<ul><li>three</li></ul></li>";
        CssStyleSource[] style = [new("html,body{margin:0}*{font-size:20px;line-height:20px;color:red}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><menu>" + items + "</menu>",
            style, text, fonts, 160, 120, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render("<!doctype html><ul>" + referenceItems + "</ul>",
            style, text, fonts, 160, 120, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Fact]
    public void LeadingZeroListMarkerMatchesIndependentlyPositionedNativeTextPixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        CssStyleSource[] style = [new("*{margin:0;font-size:20px;line-height:20px;color:red}ol{list-style:decimal-leading-zero}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><ol><li></li></ol>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(
            "<!doctype html><div style='width:30px;text-align:right'>01.</div>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        var marker = Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        var ordinaryText = Assert.Single(expected.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(ordinaryText.Glyphs, marker.Glyphs);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Contains(actual.Frame.Pixels.ToArray(), channel => channel < 255);
    }

    [Fact]
    public void RomanListMarkerMatchesIndependentlyPositionedNativeTextPixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        CssStyleSource[] style = [new("*{margin:0;font-size:20px;line-height:20px;color:red}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><ol type=i start=4><li></li></ol>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(
            "<!doctype html><div style='width:30px;text-align:right'>iv.</div>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        var marker = Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        var ordinaryText = Assert.Single(expected.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(ordinaryText.Glyphs, marker.Glyphs);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Contains(actual.Frame.Pixels.ToArray(), channel => channel < 255);
    }

    [Fact]
    public void AlphabeticListMarkerMatchesIndependentlyPositionedNativeTextPixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        CssStyleSource[] style = [new("*{margin:0;font-size:20px;line-height:20px;color:red}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><ol type=a start=27><li></li></ol>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(
            "<!doctype html><div style='width:30px;text-align:right'>aa.</div>",
            style, text, fonts, 100, 30, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        var marker = Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        var ordinaryText = Assert.Single(expected.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(ordinaryText.Glyphs, marker.Glyphs);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Contains(actual.Frame.Pixels.ToArray(), channel => channel < 255);
    }

    [Fact]
    public void ListTypeHintProducesTheSameNativePixelsAsAuthoredCss()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string style = "*{margin:0;font-size:20px;line-height:20px;color:red}";
        var actual = OfflinePageRenderer.Render("<!doctype html><ul type=CiRcLe><li></li><li></li></ul>",
            [new(style)], text, fonts, 100, 40, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render("<!doctype html><ul><li></li><li></li></ul>",
            [new(style + "ul{list-style-type:circle}")], text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(2, actual.DisplayList.Commands.OfType<StrokeEllipse>().Count());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Fact]
    public void NestedUaListMarginsMatchExplicitZeroMarginPixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li>one<ul id=nested><li>two</li></ul></li><li>three</li></ul>";
        const string style = "html,body{margin:0}*{font-size:20px;line-height:20px;color:red}";
        var actual = OfflinePageRenderer.Render(html, [new(style)], text, fonts, 160, 100,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, [new(style + "#nested{margin-top:0;margin-bottom:0}")],
            text, fonts, 160, 100, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Fact]
    public void NestedDefaultListMarkersProduceDistinctNativePixels()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li><ul><li><ul><li></li></ul></li></ul></li></ul>";
        CssStyleSource[] styles = [new("*{margin:0;font-size:20px;line-height:20px;color:red}")];
        var page = OfflinePageRenderer.Render(html, styles, text, fonts, 160, 30, cancellationToken: Cancellation);
        Assert.Empty(page.Styles.Diagnostics);
        var disc = Assert.Single(page.DisplayList.Commands.OfType<FillEllipse>());
        var circle = Assert.Single(page.DisplayList.Commands.OfType<StrokeEllipse>());
        var square = Assert.Single(page.DisplayList.Commands.OfType<FillRectangle>());
        Assert.Equal(24, disc.Bounds.X);
        Assert.Equal(64, circle.Bounds.X);
        Assert.Equal(104, square.Bounds.X);
        Assert.Equal(disc.Bounds.Y, circle.Bounds.Y);
        Assert.Equal(circle.Bounds.Y, square.Bounds.Y);
        var y = (int)Math.Floor(circle.Bounds.Y + circle.Bounds.Height / 2);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(27, y));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(67, y));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(107, y));

        byte[] Pixel(int x, int row) => page.Frame.Pixels.Span.Slice(row * page.Frame.Stride + x * 4, 4).ToArray();
    }

    [Fact]
    public void CircleMarkersKeepTransparentCentersAndSurviveRetainedResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li></li><li></li></ul>";
        CssStyleSource[] styles =
        [
            new("*{margin:0;font-size:20px;line-height:20px}"
                + "ul{list-style:circle;color:rgba(255,0,0,.5);background:blue}")
        ];
        var rendered = OfflinePageRenderer.Render(html, styles, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(rendered.Styles.Diagnostics);
        var markers = rendered.DisplayList.Commands.OfType<StrokeEllipse>().ToArray();
        Assert.Equal(2, markers.Length);
        Assert.All(markers, marker =>
        {
            Assert.Equal(new CssColor(255, 0, 0, 128), marker.Color);
            Assert.Equal(24, marker.Bounds.X);
            Assert.Equal(6, marker.Bounds.Width);
            Assert.Equal(6, marker.Bounds.Height);
            Assert.Equal(1.5, marker.StrokeWidth);
            var x = (int)Math.Floor(marker.Bounds.X + marker.Bounds.Width / 2);
            var y = (int)Math.Floor(marker.Bounds.Y + marker.Bounds.Height / 2);
            var offset = y * rendered.Frame.Stride + x * 4;
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[offset..(offset + 4)].ToArray());
            Assert.Contains(Enumerable.Range((int)Math.Floor(marker.Bounds.Y), 7)
                .SelectMany(row => Enumerable.Range(24, 6).Select(column => row * rendered.Frame.Stride + column * 4)),
                index => rendered.Frame.Pixels.Span[index + 2] > 0);
        });
        Assert.Equal(markers[0].Bounds.Y + 20, markers[1].Bounds.Y);
        var resized = OfflinePageRenderer.RenderParsed(rendered.Html, styles, text, fonts, 80, 40,
            cancellationToken: Cancellation);
        Assert.Same(rendered.Html.Document, resized.Html.Document);
        Assert.Equal(markers, resized.DisplayList.Commands.OfType<StrokeEllipse>());
    }

    [Fact]
    public void SquareMarkersRenderSolidNativePixelsAndSurviveRetainedResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><ul><li></li><li></li></ul>";
        CssStyleSource[] styles = [new("*{margin:0;font-size:20px;line-height:20px}ul{list-style:square;color:red}")];
        var rendered = OfflinePageRenderer.Render(html, styles, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(rendered.Styles.Diagnostics);
        var markers = rendered.DisplayList.Commands.OfType<FillRectangle>().ToArray();
        Assert.Equal(2, markers.Length);
        Assert.All(markers, marker =>
        {
            Assert.Equal(new CssColor(255, 0, 0), marker.Color);
            Assert.Equal(24, marker.Bounds.X);
            Assert.Equal(6, marker.Bounds.Width);
            Assert.Equal(6, marker.Bounds.Height);
        });
        Assert.Equal(markers[0].Bounds.Y + 20, markers[1].Bounds.Y);
        var redPixels = Enumerable.Range(0, rendered.Frame.Size.Width * rendered.Frame.Size.Height).Count(index =>
        {
            var offset = index * 4;
            return rendered.Frame.Pixels.Span[offset] == 0 && rendered.Frame.Pixels.Span[offset + 1] == 0
                && rendered.Frame.Pixels.Span[offset + 2] == 255;
        });
        Assert.Equal(72, redPixels);
        var resized = OfflinePageRenderer.RenderParsed(rendered.Html, styles, text, fonts, 80, 40,
            cancellationToken: Cancellation);
        Assert.Same(rendered.Html.Document, resized.Html.Document);
        Assert.Equal(markers, resized.DisplayList.Commands.OfType<FillRectangle>());
    }

    [Fact]
    public void ScreenMediaRulesMatchUnconditionalNativePixelsOnRetainedResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div>Sample</div>";
        var conditional = new[] { new CssStyleSource(
            "*{margin:0}@media print{div{display:grid;background:red}}@media all{@media screen{div{width:80px;height:20px;background:blue}}}") };
        var unconditional = new[] { new CssStyleSource("*{margin:0}div{width:80px;height:20px;background:blue}") };
        var actual = OfflinePageRenderer.Render(html, conditional, text, fonts, 100, 40, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, unconditional, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, actual.Frame.Pixels.Span.Slice(5 * actual.Frame.Stride + 70 * 4, 4).ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, conditional, text, fonts, 90, 40, cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, unconditional, text, fonts, 90, 40,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData(":any-link")]
    [InlineData(":link")]
    public void LinkStylesReachNativePixelsAndRecomputeAfterHrefRemoval(string selector)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><a id=live href>Link</a><a>Plain</a>";
        var actualStyles = new[] { new CssStyleSource(
            "*{margin:0}a{display:block;width:80px;height:20px;background:red}" + selector + "{background:blue}") };
        var equivalentStyles = new[] { new CssStyleSource(
            "*{margin:0}a{display:block;width:80px;height:20px;background:red}a[href]{background:blue}") };
        var actual = OfflinePageRenderer.Render(html, actualStyles, text, fonts, 100, 45, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, equivalentStyles, text, fonts, 100, 45, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, actual.Frame.Pixels.Span.Slice(5 * actual.Frame.Stride + 70 * 4, 4).ToArray());
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, actual.Frame.Pixels.Span.Slice(25 * actual.Frame.Stride + 70 * 4, 4).ToArray());

        var resized = OfflinePageRenderer.RenderParsed(actual.Html, actualStyles, text, fonts, 90, 45, cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, equivalentStyles, text, fonts, 90, 45,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
        actual.Html.Document.GetElementById("live")!.RemoveAttribute("href");
        var removed = OfflinePageRenderer.RenderParsed(actual.Html, actualStyles, text, fonts, 90, 45, cancellationToken: Cancellation);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, removed.Frame.Pixels.Span.Slice(5 * removed.Frame.Stride + 70 * 4, 4).ToArray());
    }

    [Fact]
    public void HslColorsMatchRgbPixelsAndRemainStableOnResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div>Sample</div>";
        var hsl = new[] { new CssStyleSource(
            "*{margin:0}div{width:80px;height:24px;color:hsl(0 100% 50%);background:hsla(240 100% 50% / 50%);border:1px solid hsl(120 100% 50%)}") };
        var rgb = new[] { new CssStyleSource(
            "*{margin:0}div{width:80px;height:24px;color:rgb(255,0,0);background:rgba(0,0,255,.5);border:1px solid rgb(0,255,0)}") };
        var actual = OfflinePageRenderer.Render(html, hsl, text, fonts, 100, 40, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, rgb, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, hsl, text, fonts, 90, 40, cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, rgb, text, fonts, 90, 40,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Fact]
    public void ModernRgbColorsMatchLegacyPixelsAndRemainStableOnResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><div>Sample</div>";
        var modern = new[] { new CssStyleSource(
            "*{margin:0}div{width:80px;height:24px;color:rgb(100% 0 0);background:rgb(25% 0 100% / 50%);border:1px solid rgb(0 0 0)}") };
        var legacy = new[] { new CssStyleSource(
            "*{margin:0}div{width:80px;height:24px;color:rgb(255,0,0);background:rgba(25%,0%,100%,.5);border:1px solid rgb(0,0,0)}") };
        var actual = OfflinePageRenderer.Render(html, modern, text, fonts, 100, 40, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, legacy, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(new byte[] { 255, 127, 159, 255 }, actual.Frame.Pixels.Span.Slice(20 * actual.Frame.Stride + 70 * 4, 4).ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, modern, text, fonts, 90, 40, cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, legacy, text, fonts, 90, 40,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

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
    public void HyphenWrapMatchesExplicitNativeLineBreakAndReflowsRetainedDocument()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var narrowWidth = Math.Ceiling(Math.Max(font.Shape("well-", 16, Cancellation).Width,
            font.Shape("known", 16, Cancellation).Width));
        var wideWidth = Math.Ceiling(font.Shape("well-known", 16, Cancellation).Width) + 1;
        Assert.True(wideWidth > narrowWidth);
        CssStyleSource[] styles = [new("*{margin:0}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><p>well-known</p>", styles, text, fonts,
            narrowWidth, 60, cancellationToken: Cancellation);
        CssStyleSource[] explicitBreakStyles = [new("*{margin:0}p{white-space:pre}")];
        var expected = OfflinePageRenderer.Render("<!doctype html><p>well-<br>known</p>", explicitBreakStyles,
            text, fonts, narrowWidth, 60, cancellationToken: Cancellation);
        Assert.Equal(2, actual.DisplayList.Commands.OfType<DrawGlyphRun>().Count());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, styles, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        var wide = OfflinePageRenderer.Render("<!doctype html><p>well-known</p>", styles, text, fonts,
            wideWidth, 60, cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(wide.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("-10")]
    [InlineData("-1,000.25")]
    [InlineData("2026-10-31")]
    [InlineData("1/2")]
    [InlineData("24/7")]
    [InlineData("a-1")]
    public void BoundedNumericTextUnbrokenRunsMatchNativeNonwrappingPixelsAndReflow(string value)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var html = "<!doctype html><p>" + value + "</p>";
        var wordWidth = font.Shape(value, 16, Cancellation).Width;
        var narrowWidth = Math.Floor(wordWidth / 2);
        var wideWidth = Math.Ceiling(wordWidth) + 1;
        CssStyleSource[] wrapping = [new("*{margin:0}p{white-space:normal}")];
        CssStyleSource[] reference = [new("*{margin:0}p{white-space:nowrap}")];
        var actual = OfflinePageRenderer.Render(html, wrapping, text, fonts, narrowWidth, 80,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, reference, text, fonts, narrowWidth, 80,
            cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.True(actual.ScrollWidth > narrowWidth);
        Assert.Equal(expected.ScrollWidth, actual.ScrollWidth);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Contains(actual.Frame.Pixels.ToArray(), channel => channel < 255);

        var resized = OfflinePageRenderer.RenderParsed(actual.Html, wrapping, text, fonts, wideWidth, 80,
            cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, reference, text, fonts, wideWidth, 80,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expectedResized.ScrollWidth, resized.ScrollWidth);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("a-1-b", "a-1-|b", "a-", "a-1-")]
    [InlineData("1-a-2-b", "1-|a-2-|b", "1-", "a-2-")]
    public void BoundedNumericTextHyphenBreaksMatchExplicitNativePixelsAndReflow(
        string value, string segments, string fitting, string overflowing)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var narrowWidth = Math.Ceiling(Math.Max(font.Shape(fitting, 16, Cancellation).Width,
            font.Shape("b", 16, Cancellation).Width));
        var wideWidth = Math.Ceiling(font.Shape(value, 16, Cancellation).Width) + 1;
        Assert.True(font.Shape(overflowing, 16, Cancellation).Width > narrowWidth);
        Assert.True(wideWidth > narrowWidth);
        CssStyleSource[] styles = [new("*{margin:0}")];
        CssStyleSource[] explicitBreakStyles = [new("*{margin:0}p{white-space:pre}")];
        var html = "<!doctype html><p>" + value + "</p>";
        var actual = OfflinePageRenderer.Render(html, styles, text, fonts, narrowWidth, 100,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render("<!doctype html><p>"
            + string.Join("<br>", segments.Split('|')) + "</p>", explicitBreakStyles, text, fonts,
            narrowWidth, 100, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(segments.Split('|').Length, actual.DisplayList.Commands.OfType<DrawGlyphRun>().Count());
        Assert.Equal(expected.ScrollWidth, actual.ScrollWidth);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());

        var resized = OfflinePageRenderer.RenderParsed(actual.Html, styles, text, fonts, wideWidth, 100,
            cancellationToken: Cancellation);
        var wide = OfflinePageRenderer.Render(html, styles, text, fonts, wideWidth, 100,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(wide.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Fact]
    public void JustifiedTextMatchesIndependentRightAlignedNativePixelsAndReflows()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var firstWidth = font.Shape("aa", 16, Cancellation).Width;
        var secondWidth = font.Shape("bb", 16, Cancellation).Width;
        var thirdWidth = font.Shape("cc", 16, Cancellation).Width;
        var spaceWidth = font.Shape(" ", 16, Cancellation).Width;
        var width = Math.Ceiling(firstWidth + spaceWidth + secondWidth + spaceWidth / 2);
        Assert.True(width < firstWidth + secondWidth + thirdWidth + 2 * spaceWidth);
        CssStyleSource[] justified = [new("*{margin:0}p{text-align:justify}")];
        CssStyleSource[] right = [new("*{margin:0}p{text-align:right}")];
        CssStyleSource[] left = [new("*{margin:0}p{text-align:left}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><p>aa bb cc</p>", justified,
            text, fonts, width, 80, cancellationToken: Cancellation);
        var reference = OfflinePageRenderer.Render("<!doctype html><p>bb</p>", right,
            text, fonts, width, 80, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        var glyphRuns = actual.DisplayList.Commands.OfType<DrawGlyphRun>().ToArray();
        Assert.Equal(4, glyphRuns.Length);
        Assert.Equal(Assert.Single(reference.DisplayList.Commands.OfType<DrawGlyphRun>()).Glyphs, glyphRuns[2].Glyphs);
        var startX = (int)Math.Ceiling(width - secondWidth);
        var rowBytes = (int)(width - startX) * 4;
        for (var y = 0; y < 20; y++)
        {
            Assert.Equal(reference.Frame.Pixels.Span.Slice(y * reference.Frame.Stride + startX * 4, rowBytes).ToArray(),
                actual.Frame.Pixels.Span.Slice(y * actual.Frame.Stride + startX * 4, rowBytes).ToArray());
        }
        Assert.Contains(reference.Frame.Pixels.ToArray(), value => value == 0);

        var resized = OfflinePageRenderer.RenderParsed(actual.Html, justified, text, fonts, 200, 80,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.RenderParsed(actual.Html, left, text, fonts, 200, 80,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expected.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("normal", "first.last+tag@example.com")]
    [InlineData("pre-line", "first.last+tag@example.com")]
    [InlineData("normal", "user_name@example.com")]
    [InlineData("pre-line", "user_name@example.com")]
    public void AsciiEmailRunsMatchNonwrappingNativePixelsAcrossRetainedResize(string mode, string address)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var html = "<!doctype html><p>" + address + "</p>";
        var wordWidth = font.Shape(address, 16, Cancellation).Width;
        var narrowWidth = Math.Floor(wordWidth / 2);
        var wideWidth = Math.Ceiling(wordWidth) + 1;
        CssStyleSource[] wrapping = [new("*{margin:0}p{white-space:" + mode + "}")];
        CssStyleSource[] reference = [new("*{margin:0}p{white-space:nowrap}")];
        var actual = OfflinePageRenderer.Render(html, wrapping, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, reference, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.True(actual.ScrollWidth > narrowWidth);
        Assert.Equal(expected.ScrollWidth, actual.ScrollWidth);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Contains(actual.Frame.Pixels.ToArray(), channel => channel < 255);
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, wrapping, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, reference, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expectedResized.ScrollWidth, resized.ScrollWidth);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("1,000.25")]
    [InlineData("12:30")]
    public void AsciiInfixRunsMatchNonwrappingNativePixelsAcrossRetainedResize(string value)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        var html = "<!doctype html><p>" + value + "</p>";
        var wordWidth = font.Shape(value, 16, Cancellation).Width;
        var narrowWidth = Math.Floor(wordWidth / 2);
        var wideWidth = Math.Ceiling(wordWidth) + 1;
        CssStyleSource[] wrapping = [new("*{margin:0}p{white-space:normal}")];
        CssStyleSource[] reference = [new("*{margin:0}p{white-space:nowrap}")];
        var actual = OfflinePageRenderer.Render(html, wrapping, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, reference, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.True(actual.ScrollWidth > narrowWidth);
        Assert.Equal(expected.ScrollWidth, actual.ScrollWidth);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, wrapping, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, reference, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expectedResized.ScrollWidth, resized.ScrollWidth);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Fact]
    public void SolidusWordMatchesNonwrappingNativePixelsAndRetainedResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><p>and/or</p>";
        var wordWidth = font.Shape("and/or", 16, Cancellation).Width;
        var narrowWidth = Math.Floor(wordWidth / 2);
        var wideWidth = Math.Ceiling(wordWidth) + 1;
        CssStyleSource[] wrapping = [new("*{margin:0}p{white-space:normal}")];
        CssStyleSource[] reference = [new("*{margin:0}p{white-space:nowrap}")];
        var actual = OfflinePageRenderer.Render(html, wrapping, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, reference, text, fonts, narrowWidth, 60,
            cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.True(actual.ScrollWidth > narrowWidth);
        Assert.Equal(expected.ScrollWidth, actual.ScrollWidth);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, wrapping, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, reference, text, fonts, wideWidth, 60,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Single(resized.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expectedResized.ScrollWidth, resized.ScrollWidth);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Fact]
    public void NoTextDecorationMatchesUndecoratedNativePixelsAndRetainedResize()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet(); text.Register("serif", font);
        using var fonts = new PaintFontRegistry(); fonts.Register(font);
        const string html = "<!doctype html><a href='/next'>Visit <span>here</span></a>";
        CssStyleSource[] withNone = [new("*{margin:0}a{text-decoration:none}span{text-decoration-line:none}")];
        CssStyleSource[] ordinary = [new("*{margin:0}")];
        var actual = OfflinePageRenderer.Render(html, withNone, text, fonts, 100, 40, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(html, ordinary, text, fonts, 100, 40, cancellationToken: Cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var resized = OfflinePageRenderer.RenderParsed(actual.Html, withNone, text, fonts, 60, 40,
            cancellationToken: Cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, ordinary, text, fonts, 60, 40,
            cancellationToken: Cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
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
