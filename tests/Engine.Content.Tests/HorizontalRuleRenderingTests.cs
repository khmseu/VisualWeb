using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class HorizontalRuleRenderingTests
{
    [Fact]
    public void DefaultHorizontalRuleProducesInsetPixels()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        var cancellation = TestContext.Current.CancellationToken;

        var page = OfflinePageRenderer.Render("<!doctype html><hr>", [], text, fonts, 80, 40,
            cancellationToken: cancellation);

        Assert.Empty(page.Styles.Diagnostics);
        var fills = page.DisplayList.Commands.OfType<FillRectangle>().OrderBy(fill => fill.Bounds.Y).ToArray();
        Assert.Equal(2, fills.Length);
        Assert.Equal(new CssColor(64, 64, 64), fills[0].Color);
        Assert.Equal(new CssColor(192, 192, 192), fills[1].Color);
        Assert.Equal(fills[0].Bounds.Y + 1, fills[1].Bounds.Y);
        Assert.Equal((byte)64, Pixel(page.Frame, fills[0]));
        Assert.Equal((byte)192, Pixel(page.Frame, fills[1]));
    }

    private static byte Pixel(RasterFrame frame, FillRectangle fill)
    {
        var x = (int)(fill.Bounds.X + fill.Bounds.Width / 2);
        var y = (int)(fill.Bounds.Y + fill.Bounds.Height / 2);
        return frame.Pixels.Span[y * frame.Stride + x * 4 + 2];
    }
}
