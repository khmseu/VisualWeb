using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Text;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Engine.Paint.Tests;

public sealed class PaintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly CssColor Red = new(255, 0, 0);
    private static readonly CssColor Blue = new(0, 0, 255);
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");

    private static DisplayList Page(string html, string css = "", PaintOptions? options = null)
    {
        var document = HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,div,p{display:block} head{display:none} *{margin:0; font-size:10px;line-height:10px}" + css)],
            includeUserAgent: false, cancellationToken: Cancellation);
        var layout = StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: Cancellation);
        return DisplayListBuilder.Build(layout, styles, options, Cancellation);
    }
    private static (byte R, byte G, byte B, byte A) Pixel(RasterFrame frame, int x, int y)
    {
        var pixels = frame.Pixels.Span;
        var at = y * frame.Stride + x * 4;
        return (pixels[at + 2], pixels[at + 1], pixels[at], pixels[at + 3]);
    }

    [Fact]
    public void UnorderedListDiscUsesCurrentColorAndBoundedEllipseCommand()
    {
        var list = Page("<ul><li></li></ul>", "ul{display:block;padding-left:20px}li{display:list-item;color:red}");
        Assert.Equal(new FillEllipse(new(12, 3, 3, 3), Red), Assert.Single(list.Commands));
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(frame, 13, 4));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 11, 4));
        Assert.Throws<PaintLimitException>(() => Page("<ul><li></li><li></li></ul>",
            "ul{display:block;padding-left:20px}li{display:list-item}", new() { MaxCommands = 1 }));
    }

    [Fact]
    public void SquareMarkersUseCurrentColorRectanglePixelsAndCommandBudget()
    {
        const string css = "ul{display:block;padding-left:20px;list-style:square}li{display:list-item;color:red}";
        var list = Page("<ul><li></li></ul>", css);
        Assert.Equal(new FillRectangle(new(12, 3, 3, 3), Red), Assert.Single(list.Commands));
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(frame, 12, 3));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(frame, 14, 5));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 11, 3));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 15, 5));
        Assert.Equal(2, Page("<ul><li></li><li></li></ul>", css, new() { MaxCommands = 2 }).Commands.Count);
        Assert.Throws<PaintLimitException>(() => Page("<ul><li></li><li></li></ul>",
            css, new() { MaxCommands = 1 }));
        Assert.Empty(Page("<ul><li></li></ul>", css + "li{font-size:0}").Commands);
        var withText = Page("<ul><li>x</li></ul>", css);
        Assert.IsType<FillRectangle>(withText.Commands[0]);
        Assert.IsType<DrawGlyphRun>(withText.Commands[1]);
    }

    [Fact]
    public void TranslucentSquareMarkersCompositeOverTheExistingBackground()
    {
        const string css = "ul{display:block;padding-left:20px;list-style:square;background:blue}"
            + "li{display:list-item;color:rgba(255,0,0,.5)}";
        var list = Page("<ul><li></li></ul>", css);
        Assert.Equal(2, list.Commands.Count);
        Assert.IsType<FillRectangle>(list.Commands[0]);
        Assert.Equal(new FillRectangle(new(12, 3, 3, 3), new CssColor(255, 0, 0, 128)), list.Commands[1]);
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal(((byte)128, (byte)0, (byte)127, (byte)255), Pixel(frame, 13, 4));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(frame, 11, 4));
    }

    [Fact]
    public void CircleMarkersUseOneCurrentColorOutlineCommandAndTheExistingBudget()
    {
        const string css = "ul{display:block;padding-left:20px;list-style:circle}li{display:list-item;color:red}";
        var list = Page("<ul><li></li></ul>", css, new() { MaxCommands = 1 });
        Assert.Equal(new StrokeEllipse(new(12, 3, 3, 3), Red, 0.75), Assert.Single(list.Commands));
        using var fonts = new PaintFontRegistry();
        var normal = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(normal, 13, 4));
        Assert.True(Pixel(normal, 13, 3).G < 255);
        var frame = CpuRasterizer.Render(list, fonts, scale: 4, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 53, 17));
        Assert.True(Pixel(frame, 53, 13).G < 255);
        var twoMarkers = Page("<ul><li></li><li></li></ul>", css, new() { MaxCommands = 2 });
        Assert.Equal(2, twoMarkers.Commands.Count);
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(twoMarkers, fonts,
            options: new() { MaxCommands = 1 }, cancellationToken: Cancellation));
        Assert.Throws<PaintLimitException>(() => Page("<ul><li></li><li></li></ul>",
            css, new() { MaxCommands = 1 }));
        var withText = Page("<ul><li>x</li></ul>", css);
        Assert.IsType<StrokeEllipse>(withText.Commands[0]);
        Assert.IsType<DrawGlyphRun>(withText.Commands[1]);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            CpuRasterizer.Render(list, fonts, cancellationToken: canceled.Token));
    }

    [Theory]
    [InlineData(0, 0, 255, 255)]
    [InlineData(20, 200, 30, 128)]
    public void CircleRingCompositesOnceAndLeavesTheActualBackgroundInItsCenter(byte r, byte g, byte b, byte a)
    {
        var background = new FillRectangle(new(0, 0, 24, 24), new CssColor(r, g, b, a));
        var markerColor = new CssColor(255, 0, 0, 128);
        var marker = new StrokeEllipse(new(4, 4, 16, 16), markerColor, 4);
        var backdrop = new CssColor(25, 85, 160);
        using var fonts = new PaintFontRegistry();
        var behind = CpuRasterizer.Render(new(24, 24, [background]), fonts,
            backdrop: backdrop, cancellationToken: Cancellation);
        var once = CpuRasterizer.Render(new(24, 24,
            [background, new FillRectangle(new(0, 0, 24, 24), markerColor)]), fonts,
            backdrop: backdrop, cancellationToken: Cancellation);
        var outlined = CpuRasterizer.Render(new(24, 24, [background, marker]), fonts,
            backdrop: backdrop, cancellationToken: Cancellation);
        var small = CpuRasterizer.Render(new(24, 24,
            [background, new StrokeEllipse(new(4, 4, 3, 3), markerColor, 0.75)]), fonts,
            backdrop: backdrop, cancellationToken: Cancellation);
        Assert.Equal(Pixel(behind, 5, 5), Pixel(small, 5, 5));
        Assert.NotEqual(Pixel(behind, 5, 4), Pixel(small, 5, 4));
        for (var y = 11; y <= 12; y++)
        {
            for (var x = 11; x <= 12; x++) { Assert.Equal(Pixel(behind, x, y), Pixel(outlined, x, y)); }
        }
        Assert.Equal(Pixel(once, 11, 5), Pixel(outlined, 11, 5));
        Assert.NotEqual(Pixel(behind, 11, 5), Pixel(outlined, 11, 5));
        for (var y = 0; y < 24; y++)
        {
            for (var x = 0; x < 24; x++)
            {
                if (x < 4 || x >= 20 || y < 4 || y >= 20)
                {
                    Assert.Equal(Pixel(behind, x, y), Pixel(outlined, x, y));
                }
            }
        }
    }

    [Fact]
    public void CircleOutlineHonorsScaleScrollAndAncestorClip()
    {
        var background = new FillRectangle(new(0, 0, 100, 100), Blue);
        var marker = new StrokeEllipse(new(0, 2, 12, 12), Red, 2);
        var clippedList = new DisplayList(12, 12,
            [background, new PushScrollClip(new(2, 2, 6, 6), 2), marker, new PopScrollClip()]);
        using var fonts = new PaintFontRegistry();
        var actual = CpuRasterizer.Render(clippedList, fonts, scale: 2, scrollX: 1, scrollY: 2,
            cancellationToken: Cancellation);
        var uncut = CpuRasterizer.Render(new(12, 12,
            [background, new PushScrollClip(new(-100, -100, 200, 200), 2), marker, new PopScrollClip()]),
            fonts, scale: 2, scrollX: 1, scrollY: 2, cancellationToken: Cancellation);
        var behind = CpuRasterizer.Render(new(12, 12, [background]), fonts, scale: 2,
            scrollX: 1, scrollY: 2, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(actual, 2, 1));
        Assert.Equal(Pixel(behind, 10, 8), Pixel(actual, 10, 8));
        Assert.NotEqual(Pixel(behind, 16, 0), Pixel(uncut, 16, 0));
        for (var y = 0; y < 24; y++)
        {
            for (var x = 0; x < 24; x++)
            {
                var insideClip = x is >= 2 and < 14 && y < 12;
                if (!insideClip) { Assert.Equal(Pixel(behind, x, y), Pixel(actual, x, y)); }
            }
        }
    }

    [Theory]
    [InlineData(8_388_608, 0, 1)]
    [InlineData(0, 8_388_608, 1)]
    [InlineData(8_388_608, 8_388_608, 1)]
    [InlineData(0, 8_388_608, 100)]
    [InlineData(8_388_608.375, 8_388_608.125, 100)]
    [InlineData(100_000_000, 9_999_994, 1)]
    public void CircleOutlineRetainsPrecisionAtLargeScrolledCoordinates(double x, double y, double scale)
    {
        var local = new StrokeEllipse(new(2, 2, 3, 3), Red, 0.75);
        var distant = new StrokeEllipse(new(x + 2, y + 2, 3, 3), Red, 0.75);
        using var fonts = new PaintFontRegistry();
        var expected = CpuRasterizer.Render(new(8, 8, [local]), fonts, scale: scale, cancellationToken: Cancellation);
        var actual = CpuRasterizer.Render(new(8, 8, [distant]), fonts, scale: scale,
            scrollX: x, scrollY: y, cancellationToken: Cancellation);
        Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());

        var offscreen = CpuRasterizer.Render(new(8, 8, [distant]), fonts, cancellationToken: Cancellation);
        Assert.All(offscreen.Pixels.ToArray(), value => Assert.Equal(255, value));
    }

    [Fact]
    public void CircleOutlineRetainsPrecisionInsideALargeNestedScrollOffset()
    {
        const double offset = 8_388_608;
        var clip = new LayoutRect(0, 0, 8, 8);
        var marker = new StrokeEllipse(new(2, 2, 3, 3), Red, 0.75);
        using var fonts = new PaintFontRegistry();
        var expected = CpuRasterizer.Render(new(8, 8, [new PushScrollClip(clip, 0), marker, new PopScrollClip()]),
            fonts, scale: 100, cancellationToken: Cancellation);
        var actual = CpuRasterizer.Render(new(8, 8,
            [new PushScrollClip(clip, offset), marker with { Bounds = marker.Bounds with { Y = offset + 2 } }, new PopScrollClip()]),
            fonts, scale: 100, cancellationToken: Cancellation);
        Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
    }

    [Fact]
    public void CircleOutlinesRestoreOffsetsWhenNestedScrollClipsEnd()
    {
        const double outerOffset = 3_000_000;
        const double innerOffset = 5_388_608;
        var clip = new LayoutRect(0, 0, 8, 8);
        var red = new StrokeEllipse(new(1, 1, 2, 2), Red, 0.5);
        var blue = new StrokeEllipse(new(3, 3, 2, 2), Blue, 0.5);
        var green = new StrokeEllipse(new(5, 5, 2, 2), new(0, 128, 0), 0.5);
        using var fonts = new PaintFontRegistry();
        var expected = CpuRasterizer.Render(new(8, 8,
            [new PushScrollClip(clip, 0), new PushScrollClip(clip, 0), red, new PopScrollClip(), blue,
                new PopScrollClip(), green]), fonts, scale: 100, cancellationToken: Cancellation);
        var actual = CpuRasterizer.Render(new(8, 8,
            [new PushScrollClip(clip, outerOffset),
                new PushScrollClip(clip with { Y = outerOffset }, innerOffset),
                red with { Bounds = red.Bounds with { Y = outerOffset + innerOffset + 1 } },
                new PopScrollClip(), blue with { Bounds = blue.Bounds with { Y = outerOffset + 3 } },
                new PopScrollClip(), green]), fonts, scale: 100, cancellationToken: Cancellation);
        Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
    }

    [Fact]
    public void CircleOutlineIsClippedToItsFractionalDeclaredBounds()
    {
        var bounds = new LayoutRect(2.25, 2.25, 10.5, 10.5);
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(new(16, 16, [new StrokeEllipse(bounds, Red, 2)]), fonts,
            scale: 2, cancellationToken: Cancellation);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (x + 1 <= bounds.X * 2 || x >= (bounds.X + bounds.Width) * 2
                    || y + 1 <= bounds.Y * 2 || y >= (bounds.Y + bounds.Height) * 2)
                {
                    Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, x, y));
                }
            }
        }
    }

    [Fact]
    public void OutlineCommandAlsoKeepsANonCircularEllipseHollow()
    {
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(new(24, 16,
            [new StrokeEllipse(new(3, 4, 16, 8), Red, 1.5)]), fonts, scale: 2, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 22, 16));
        Assert.True(Pixel(frame, 22, 9).G < 255);
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 5, 16));
    }

    [Fact]
    public void CircleOutlineRestoresFillStateForLaterRectanglesEllipsesAndGlyphs()
    {
        using var font = new TextFont(FontPath);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var glyph = Assert.Single(font.Shape("M", 20, Cancellation).Glyphs);
        PaintCommand[] later =
        [
            new FillRectangle(new(20, 1, 8, 8), Blue),
            new FillEllipse(new(20, 15, 8, 8), Blue),
            new DrawGlyphRun(font.Identity, 20, new(0, 0, 0),
                [new((ushort)glyph.GlyphId, 35 + glyph.OffsetX, 25 + glyph.OffsetY)])
        ];
        var reference = CpuRasterizer.Render(new(80, 40, later), fonts, cancellationToken: Cancellation);
        var actual = CpuRasterizer.Render(new(80, 40,
            [new StrokeEllipse(new(1, 1, 16, 16), Red, 4), .. later]), fonts, cancellationToken: Cancellation);
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(actual, 24, 5));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(actual, 24, 19));
        Assert.Contains(Enumerable.Range(0, 40).SelectMany(y => Enumerable.Range(35, 40)
            .Select(x => Pixel(reference, x, y))), pixel => pixel.R < 255);
        for (var y = 0; y < 40; y++)
        {
            for (var x = 20; x < 80; x++) { Assert.Equal(Pixel(reference, x, y), Pixel(actual, x, y)); }
        }
    }

    [Fact]
    public void CircleOutlinePreflightsGeometryAndStrokeBeforeDrawing()
    {
        using var fonts = new PaintFontRegistry();
        void Render(StrokeEllipse marker) => CpuRasterizer.Render(
            new(10, 10, [new FillRectangle(new(0, 0, 1, 1), Blue), marker]),
            fonts, cancellationToken: Cancellation);
        Assert.Throws<PaintLimitException>(() => Render(new(new(double.NaN, 0, 4, 4), Red, 1)));
        Assert.Throws<PaintLimitException>(() => Render(new(new(0, 0, double.PositiveInfinity, 4), Red, 1)));
        Assert.Throws<PaintLimitException>(() => Render(new(new(1_000_000_001, 0, 4, 4), Red, 1)));
        Assert.Throws<PaintLimitException>(() => Render(new(new(0, 10_000_000, 4, 4), Red, 1)));
        Assert.Throws<ArgumentException>(() => Render(new(new(0, 0, -1, 4), Red, 1)));
        Assert.Throws<ArgumentException>(() => Render(new(new(0, 0, 0, 4), Red, 1)));
        Assert.Throws<ArgumentException>(() => Render(new(new(0, 0, 4, -1), Red, 1)));
        Assert.Throws<ArgumentException>(() => Render(new(new(0, 0, 4, 0), Red, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render(new(new(0, 0, 4, 4), Red, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render(new(new(0, 0, 4, 4), Red, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render(new(new(0, 0, 4, 4), Red, double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render(new(new(0, 0, 4, 4), Red, double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Render(new(new(0, 0, 4, 4), Red, 2)));
        Assert.Throws<PaintLimitException>(() => Render(new(new(0, 0, 4, 4), Red, 1.999999999)));
        Assert.Throws<PaintLimitException>(() => Render(new(new(0, 0, 4, 4), Red, double.Epsilon)));
        Assert.Throws<ArgumentNullException>(() => Render(new(new(0, 0, 4, 4), null!, 1)));
    }

    [Fact]
    public void CircleOutlineRejectsStrokeUnderflowAfterDeviceScaling()
    {
        using var fonts = new PaintFontRegistry();
        var marker = new StrokeEllipse(new(0, 0, 1e-8, 1e-8), Red, 2.5e-9);
        Assert.Throws<PaintLimitException>(() =>
            CpuRasterizer.Render(new(10, 10, [marker]), fonts, scale: 1e-40, cancellationToken: Cancellation));
        var supported = CpuRasterizer.Render(new(10, 10, [marker]), fonts,
            scale: 1e-20, cancellationToken: Cancellation);
        Assert.Equal(new PixelSize(1, 1), supported.Size);
    }

    [Fact]
    public void EllipseRasterizationValidatesGeometryBeforeDrawing()
    {
        using var fonts = new PaintFontRegistry();
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(
            new DisplayList(10, 10, [new FillEllipse(new(double.NaN, 0, 3, 3), Red)]),
            fonts, cancellationToken: Cancellation));
        Assert.Throws<ArgumentException>(() => CpuRasterizer.Render(
            new DisplayList(10, 10, [new FillEllipse(new(0, 0, -1, 3), Red)]),
            fonts, cancellationToken: Cancellation));
    }

    [Fact]
    public void DecimalMarkersShareTextPaintBudgetsAndRemainSeparateFromContent()
    {
        const string css = "ol{display:block;padding-left:40px}li{display:list-item;list-style-type:decimal;color:red}";
        var list = Page("<ol start=12><li>x</li><li></li></ol>", css);
        var runs = list.Commands.OfType<DrawGlyphRun>().ToArray();
        Assert.Equal(3, runs.Length);
        Assert.Equal(new ushort[] { '1', '2', '.' }, runs[0].Glyphs.Select(g => g.Id));
        Assert.Equal(new[] { 20.0, 25, 30 }, runs[0].Glyphs.Select(g => g.X));
        Assert.All(runs[0].Glyphs, glyph => Assert.Equal(8, glyph.Y));
        Assert.Equal(Red, runs[0].Color);
        Assert.Equal((ushort)'x', Assert.Single(runs[1].Glyphs).Id);
        Assert.Equal(new ushort[] { '1', '3', '.' }, runs[2].Glyphs.Select(g => g.Id));
        Assert.Throws<PaintLimitException>(() => Page("<ol><li></li></ol>", css, new() { MaxGlyphs = 1 }));
        Assert.Throws<PaintLimitException>(() => Page("<ol><li></li><li></li></ol>", css, new() { MaxCommands = 1 }));
    }

    [Fact]
    public void AlphabeticMarkersUsePositionedGlyphCommandsAndSharedPaintLimits()
    {
        const string css = "ol{display:block;padding-left:40px;list-style-type:lower-alpha}li{display:list-item;color:red}";
        var list = Page("<ol start=26><li>x</li><li></li></ol>", css);
        var runs = list.Commands.OfType<DrawGlyphRun>().ToArray();
        Assert.Equal(3, runs.Length);
        Assert.Equal(new ushort[] { 'z', '.' }, runs[0].Glyphs.Select(glyph => glyph.Id));
        Assert.Equal(new[] { 25.0, 30 }, runs[0].Glyphs.Select(glyph => glyph.X));
        Assert.Equal((ushort)'x', Assert.Single(runs[1].Glyphs).Id);
        Assert.Equal(new ushort[] { 'a', 'a', '.' }, runs[2].Glyphs.Select(glyph => glyph.Id));
        Assert.Equal(new[] { 20.0, 25, 30 }, runs[2].Glyphs.Select(glyph => glyph.X));
        Assert.All(runs[2].Glyphs, glyph => Assert.Equal(18, glyph.Y));
        Assert.Equal(Red, runs[2].Color);
        Assert.Throws<PaintLimitException>(() => Page("<ol start=27><li></li></ol>", css, new() { MaxGlyphs = 2 }));
        Assert.Throws<PaintLimitException>(() =>
            Page("<ol><li></li><li></li></ol>", css, new() { MaxCommands = 1 }));
    }

    [Fact]
    public void BackgroundAndSolidBordersHaveExactNonOverlappingCommands()
    {
        var list = Page("<div></div>", "div{width:10px;height:10px;padding:2px;border:1px solid red;background-color:blue}");
        Assert.Equal(5, list.Commands.Count);
        Assert.Equal(new FillRectangle(new(0, 0, 16, 16), Blue), list.Commands[0]);
        Assert.Equal(new FillRectangle(new(0, 0, 16, 1), Red), list.Commands[1]);
        Assert.Equal(new FillRectangle(new(0, 15, 16, 1), Red), list.Commands[2]);
        Assert.Equal(new FillRectangle(new(0, 1, 1, 14), Red), list.Commands[3]);
        Assert.Equal(new FillRectangle(new(15, 1, 1, 14), Red), list.Commands[4]);
    }

    [Theory]
    [InlineData("html{background-color:red} body{background-color:blue}", 255, 0, 0)]
    [InlineData("body{background-color:blue}", 0, 0, 255)]
    [InlineData("body{background-color:blue;display:none}", 255, 255, 255)]
    public void CanvasBackgroundPropagationCoversViewport(string css, byte r, byte g, byte b)
    {
        var list = Page("", css);
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal((r, g, b, (byte)255), Pixel(frame, 99, 99));
    }

    [Fact]
    public void TextFlowOrderIsDocumentOrderNotGeometrySort()
    {
        var list = Page("<div>a<p>b</p>c</div>");
        Assert.Equal(new[] { (ushort)'a', (ushort)'b', (ushort)'c' },
            list.Commands.OfType<DrawGlyphRun>().Select(run => Assert.Single(run.Glyphs).Id));
        Assert.Equal(new[] { 8.0, 18, 28 }, list.Commands.OfType<DrawGlyphRun>().Select(run => run.Glyphs[0].Y));
    }

    [Fact]
    public void DisplayCommandsOwnTheirGlyphAndCommandCollections()
    {
        var glyphs = new List<PaintGlyph> { new(1, 2, 3) };
        var run = new DrawGlyphRun("font", 16, Red, glyphs);
        glyphs.Clear();
        var commands = new List<PaintCommand> { run };
        var list = new DisplayList(10, 10, commands);
        commands.Clear();
        Assert.Single(run.Glyphs);
        Assert.Single(list.Commands);
    }

    [Theory]
    [InlineData("div{border:1px dashed red}")]
    [InlineData("div{border:1px solid red;border-left-color:blue}")]
    [InlineData("div{border:1px solid red;border-left-color:blue;height:0}")]
    public void UnsupportedBorderStylesAndCornerColorsFail(string css)
    {
        Assert.Throws<UnsupportedPaintException>(() => Page("<div></div>", "div{height:10px}" + css));
    }

    [Fact]
    public void DisplayCommandGlyphAndDepthLimitsAreExact()
    {
        Assert.Single(Page("<p>a</p>", options: new() { MaxCommands = 1, MaxGlyphs = 1 }).Commands);
        Assert.Throws<PaintLimitException>(() => Page("<p>ab</p>", options: new() { MaxGlyphs = 1 }));
        Assert.Throws<PaintLimitException>(() => Page("<p>a b</p>", options: new() { MaxCommands = 1 }));
        Assert.Throws<PaintLimitException>(() => Page("<p>a</p>", options: new() { MaxDepth = 2 }));
        Assert.Throws<PaintLimitException>(() => new DisplayList(10, 10,
            [new FillRectangle(new(0, 0, 1, 1), Red), new FillRectangle(new(1, 1, 1, 1), Red)], new() { MaxCommands = 1 }));
        Assert.Throws<PaintLimitException>(() => new DrawGlyphRun("font", 16, Red, [new(1, 0, 0), new(2, 0, 0)], new() { MaxGlyphs = 1 }));
    }

    [Fact]
    public void RasterMatchesIndependentReferenceImageExactly()
    {
        var list = new DisplayList(4, 4,
        [
            new FillRectangle(new(0, 0, 2, 2), Red),
            new FillRectangle(new(2, 2, 2, 2), Blue)
        ]);
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Data", "quadrants.rgba"))
            .Where(line => !line.StartsWith('#')).SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(token => Convert.ToByte(token, 16)).ToArray();
        var actual = new List<byte>();
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var pixel = Pixel(frame, x, y);
                actual.AddRange([pixel.R, pixel.G, pixel.B, pixel.A]);
            }
        }
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScaleClipAlphaAndBgraStrideAreExplicit()
    {
        var list = new DisplayList(3, 2,
        [
            new FillRectangle(new(-2, -2, 3, 3), Red),
            new FillRectangle(new(1, 0, 2, 2), new CssColor(0, 0, 255, 128))
        ]);
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(list, fonts, scale: 2, cancellationToken: Cancellation);
        Assert.Equal(new PixelSize(6, 4), frame.Size);
        Assert.Equal(24, frame.Stride);
        Assert.Equal((byte)0, frame.Pixels.Span[0]);
        Assert.Equal((byte)255, frame.Pixels.Span[2]);
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(frame, 0, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 0, 3));
        Assert.Equal(((byte)127, (byte)127, (byte)255, (byte)255), Pixel(frame, 5, 3));
        Assert.All(Enumerable.Range(0, 24), i => Assert.Equal(255, frame.Pixels.Span[i * 4 + 3]));
        var fractional = CpuRasterizer.Render(new(2.5, 1.5, []), fonts, cancellationToken: Cancellation);
        Assert.Equal(new PixelSize(3, 2), fractional.Size);
    }

    [Fact]
    public void PortablePresentationPassesOwnedFrameToSurfaceAndPropagatesFailure()
    {
        using var fonts = new PaintFontRegistry();
        var frame = CpuRasterizer.Render(new(2, 2, []), fonts, cancellationToken: Cancellation);
        var surface = new Surface();
        frame.Present(surface);
        Assert.Equal(frame.Size, surface.Size);
        Assert.Equal(frame.Stride, surface.Stride);
        Assert.Equal(frame.Pixels.ToArray(), surface.Pixels);
        Assert.Throws<PlatformException>(() => frame.Present(new FailedSurface()));
    }

    [Fact]
    public void NativeGlyphsRasterizeWithoutReshapingOrFallback()
    {
        using var font = new TextFont(FontPath);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var run = font.Shape("office", 20, Cancellation);
        var x = 5.0;
        var glyphs = new List<PaintGlyph>();
        foreach (var glyph in run.Glyphs)
        {
            glyphs.Add(new((ushort)glyph.GlyphId, x + glyph.OffsetX, 25 + glyph.OffsetY));
            x += glyph.Advance;
        }
        var list = new DisplayList(100, 40, [new DrawGlyphRun(font.Identity, 20, new(0, 0, 0), glyphs)]);
        font.Dispose();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        var changed = 0;
        for (var y = 0; y < 40; y++)
        {
            for (var px = 0; px < 100; px++)
            {
                if (Pixel(frame, px, y).R < 255) { changed++; }
            }
        }
        Assert.InRange(changed, 100, 1000);
        Assert.Equal(frame.Pixels.ToArray(), CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation).Pixels.ToArray());
    }

    [Fact]
    public void FontPixelAndInvalidCommandBoundariesFailBeforeDrawing()
    {
        using var font = new TextFont(FontPath);
        using var limited = new PaintFontRegistry(new() { MaxFontBytes = font.FontDataLength - 1 });
        Assert.Throws<PaintLimitException>(() => limited.Register(font));
        using var fonts = new PaintFontRegistry(new() { MaxFontBytes = font.FontDataLength, MaxTotalFontBytes = font.FontDataLength });
        fonts.Register(font);
        Assert.Throws<ArgumentException>(() => fonts.Register(font));
        var list = new DisplayList(2, 2, []);
        Assert.Equal(16, CpuRasterizer.Render(list, fonts, options: new() { MaxPixels = 4 }, cancellationToken: Cancellation).Pixels.Length);
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(list, fonts, options: new() { MaxPixels = 3 }, cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CpuRasterizer.Render(list, fonts, scale: double.NaN, cancellationToken: Cancellation));
        Assert.Throws<ArgumentException>(() => CpuRasterizer.Render(list, fonts, backdrop: new(0, 0, 0, 0), cancellationToken: Cancellation));
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(new(1, 1, [new FillRectangle(new(double.NaN, 0, 1, 1), Red)]), fonts, cancellationToken: Cancellation));
        Assert.Throws<UnsupportedPaintException>(() => CpuRasterizer.Render(new(1, 1, [new DrawGlyphRun("absent", 16, Red, [])]), fonts, cancellationToken: Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CpuRasterizer.Render(list, fonts, cancellationToken: canceled.Token));
    }

    [Fact]
    public void CombiningOffsetsAndExactShapedLigatureIdsReachDisplayCommands()
    {
        var document = HtmlParser.Parse("<!doctype html><p>ffi x\u0301</p>", cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,p{display:block} head{display:none} *{margin:0;font-size:20px;line-height:30px}")],
            includeUserAgent: false, cancellationToken: Cancellation);
        using var font = new TextFont(FontPath);
        var shaper = new FontSet();
        shaper.Register("serif", font);
        var layout = StaticLayout.Layout(document, styles, shaper, 100, 60, cancellationToken: Cancellation);
        var list = DisplayListBuilder.Build(layout, styles, cancellationToken: Cancellation);
        var commands = list.Commands.OfType<DrawGlyphRun>().ToArray();
        var fragments = layout.Root!.Children.Single().Children.Single().Lines.SelectMany(l => l.Fragments).ToArray();
        Assert.Equal(fragments.Length, commands.Length);
        Assert.Single(commands[0].Glyphs);
        Assert.True(commands[^1].Glyphs.Count > 1);
        Assert.Contains(fragments[^1].Run.Glyphs, g => g.OffsetX != 0 || g.OffsetY != 0);
        for (var index = 0; index < fragments.Length; index++)
        {
            var fragment = fragments[index];
            var x = fragment.X;
            for (var glyph = 0; glyph < fragment.Run.Glyphs.Count; glyph++)
            {
                var shaped = fragment.Run.Glyphs[glyph];
                Assert.Equal(new PaintGlyph((ushort)shaped.GlyphId, x + shaped.OffsetX, fragment.Baseline + shaped.OffsetY),
                    commands[index].Glyphs[glyph]);
                x += shaped.Advance;
            }
        }
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Contains(frame.Pixels.ToArray(), b => b < 255);
    }

    [Fact]
    public void FontRegistryAndCopiesKeepThreadOwnershipAndDisposalExplicit()
    {
        using var font = new TextFont(FontPath);
        using var same = new TextFont(FontPath);
        Assert.Equal(font.Identity, same.Identity);
        var bytes = font.CopyFontData();
        bytes[0] ^= 255;
        Assert.NotEqual(bytes[0], font.CopyFontData()[0]);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        Assert.Throws<ArgumentException>(() => fonts.Register(same));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CpuRasterizer.Render(new(1, 1, []), fonts); }
            catch (InvalidOperationException exception) { failure = exception; }
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(failure);
        fonts.Dispose();
        fonts.Dispose();
        Assert.Throws<ObjectDisposedException>(() => CpuRasterizer.Render(new(1, 1, []), fonts, cancellationToken: Cancellation));
        Assert.Throws<ObjectDisposedException>(() => fonts.Register(font));
    }

    [Fact]
    public void FractionalFontSizesUseTheSameQuantizationAsHarfBuzz()
    {
        using var font = new TextFont(FontPath);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var shaped = font.Shape("AV", 16.007, Cancellation);
        var glyphs = shaped.Glyphs.Select(g => new PaintGlyph((ushort)g.GlyphId, 0, 25)).ToArray();
        var actual = new DisplayList(50, 30, [new DrawGlyphRun(font.Identity, 16.007, Red, glyphs)]);
        var expected = new DisplayList(50, 30, [new DrawGlyphRun(font.Identity, 16, Red, glyphs)]);
        Assert.Equal(CpuRasterizer.Render(expected, fonts, cancellationToken: Cancellation).Pixels.ToArray(),
            CpuRasterizer.Render(actual, fonts, cancellationToken: Cancellation).Pixels.ToArray());
    }

    [Fact]
    public void BlockBackgroundsPrecedeAllAnonymousInlineTextGroups()
    {
        var list = Page("<div>a<p>b</p>c</div>", "div{background-color:red} p{background-color:blue}");
        Assert.IsType<FillRectangle>(list.Commands[0]);
        Assert.IsType<FillRectangle>(list.Commands[1]);
        Assert.All(list.Commands.Skip(2), command => Assert.IsType<DrawGlyphRun>(command));
    }

    [Fact]
    public void TransparentBorderCornersAreNotDoubleBlended()
    {
        using var fonts = new PaintFontRegistry();
        var list = Page("<div></div>", "div{width:10px;height:10px;border:2px solid rgba(255,0,0,0.5)}");
        var frame = CpuRasterizer.Render(list, fonts, cancellationToken: Cancellation);
        Assert.Equal(Pixel(frame, 0, 0), Pixel(frame, 0, 5));
        Assert.Equal(((byte)255, (byte)127, (byte)127, (byte)255), Pixel(frame, 0, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 3, 3));
    }

    [Fact]
    public void FractionalRectanglesUseDeviceCoverageWithoutCssBorderSnapping()
    {
        using var fonts = new PaintFontRegistry();
        var list = new DisplayList(3, 1, [new FillRectangle(new(.25, 0, .5, 1), Red)]);
        var frame = CpuRasterizer.Render(list, fonts, scale: 2, cancellationToken: Cancellation);
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(frame, 1, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), Pixel(frame, 2, 0));
    }

    [Fact]
    public void InvalidFlowCannotDuplicateLinesOrOmitBlocks()
    {
        var document = HtmlParser.Parse("<!doctype html><p>a<br>b</p>", cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,p{display:block} head{display:none} *{margin:0}")], includeUserAgent: false, cancellationToken: Cancellation);
        var layout = StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: Cancellation);
        var body = layout.Root!.Children.Single();
        var p = body.Children.Single();
        var forgedP = p with { Flow = [new LayoutLineItem(p.Lines[0]), new LayoutLineItem(p.Lines[0])] };
        var forgedBody = body with { Children = [forgedP], Flow = [new LayoutBlockItem(forgedP)] };
        var forgedRoot = layout.Root with { Children = [forgedBody], Flow = [new LayoutBlockItem(forgedBody)] };
        Assert.Throws<InvalidOperationException>(() => DisplayListBuilder.Build(layout with { Root = forgedRoot }, styles,
            cancellationToken: Cancellation));
    }

    [Fact]
    public void RasterGlyphLimitAndMissingGlyphsHaveExactBoundaries()
    {
        using var font = new TextFont(FontPath);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var id = (ushort)Assert.Single(font.Shape("a", 16, Cancellation).Glyphs).GlyphId;
        var glyphs = new[] { new PaintGlyph(id, 0, 20), new PaintGlyph(id, 10, 20) };
        var list = new DisplayList(30, 30, [new DrawGlyphRun(font.Identity, 16, Red, glyphs)]);
        Assert.NotEmpty(CpuRasterizer.Render(list, fonts, options: new() { MaxGlyphs = 2 }, cancellationToken: Cancellation).Pixels.ToArray());
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(list, fonts, options: new() { MaxGlyphs = 1 }, cancellationToken: Cancellation));
        Assert.Throws<UnsupportedPaintException>(() => CpuRasterizer.Render(new(1, 1,
            [new DrawGlyphRun(font.Identity, 16, Red, [new(ushort.MaxValue, 0, 0)])]), fonts, cancellationToken: Cancellation));
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(new(1, 1,
            [new DrawGlyphRun(font.Identity, double.NaN, Red, [new(id, 0, 0)])]), fonts, cancellationToken: Cancellation));
    }

    [Fact]
    public void ZeroFontSizeDoesNotSubstituteNativeDefaultSize()
    {
        using var font = new TextFont(FontPath);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var id = (ushort)Assert.Single(font.Shape("a", 0, Cancellation).Glyphs).GlyphId;
        var frame = CpuRasterizer.Render(new(20, 20, [new DrawGlyphRun(font.Identity, 0, Red, [new(id, 0, 10)])]),
            fonts, cancellationToken: Cancellation);
        Assert.All(frame.Pixels.ToArray(), b => Assert.Equal(255, b));
    }

    private sealed class MetricsShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(text, "test", request.Size, text.Select((c, i) => new ShapedGlyph(c, i, 5, 0, 0)).ToArray(),
                text.Length * 5, new(8, 2, 0));
        }
    }
    private sealed class Surface : IPixelSurface
    {
        internal byte[] Pixels { get; private set; } = [];
        internal PixelSize Size { get; private set; }
        internal int Stride { get; private set; }
        public void Present(ReadOnlySpan<byte> pixels, PixelSize size, int stride)
        {
            PixelBuffer.Validate(pixels.Length, size, stride);
            Pixels = pixels.ToArray(); Size = size; Stride = stride;
        }
    }
    private sealed class FailedSurface : IPixelSurface
    {
        public void Present(ReadOnlySpan<byte> pixels, PixelSize size, int stride) => throw new PlatformException("Presentation failed.");
    }
}
