using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class FontColorHintTests
{
    [Fact]
    public void ParsedFontHintReachesLayoutAndNativePixels()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:28px;line-height:1}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><p><font color=chucknorris><span>Sample</span></font></p>",
            styles, text, fonts, 220, 60, cancellationToken: cancellation);
        var reference = OfflinePageRenderer.Render("<!doctype html><p><span style='color:#c00000'><span>Sample</span></span></p>",
            styles, text, fonts, 220, 60, cancellationToken: cancellation);
        var black = OfflinePageRenderer.Render("<!doctype html><p><span><span>Sample</span></span></p>",
            styles, text, fonts, 220, 60, cancellationToken: cancellation);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(new CssColor(192, 0, 0), Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>()).Color);
        Assert.Equal(reference.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.NotEqual(black.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }
}
