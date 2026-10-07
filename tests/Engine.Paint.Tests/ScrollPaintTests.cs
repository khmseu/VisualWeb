using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Engine.Paint.Tests;

public sealed class ScrollPaintTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void TranslationUsesCssPixelsUnderFixedViewportClipAndOpaqueScaledFrame(double scale)
    {
        using var fonts = new PaintFontRegistry();
        var list = new DisplayList(10, 10,
            [new FillRectangle(new LayoutRect(0, 20, 10, 10), new CssColor(0, 0, 255))]);
        var frame = CpuRasterizer.Render(list, fonts, scale: scale,
            cancellationToken: TestContext.Current.CancellationToken, scrollY: 20);
        Assert.Equal(new PixelSize((int)(10 * scale), (int)(10 * scale)), frame.Size);
        Assert.Equal(40 * scale, frame.Stride);
        for (var index = 0; index < frame.Pixels.Length; index += 4)
        {
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, frame.Pixels.Span.Slice(index, 4).ToArray());
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e9 + 1)]
    public void InvalidOffsetsFailExplicitly(double offset)
    {
        using var fonts = new PaintFontRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() => CpuRasterizer.Render(new(10, 10, []), fonts,
            cancellationToken: TestContext.Current.CancellationToken, scrollY: offset));
    }

    [Fact]
    public void ExcessiveOffscreenRectangleCannotBypassHeightBudgetThroughClipping()
    {
        using var fonts = new PaintFontRegistry();
        Assert.Throws<PaintLimitException>(() => CpuRasterizer.Render(new(10, 10,
            [new FillRectangle(new(0, 0, 10, PaintOptions.MaxDocumentHeight + 1), new(0, 0, 255))]), fonts,
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
