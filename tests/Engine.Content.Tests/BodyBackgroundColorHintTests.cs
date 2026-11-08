using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class BodyBackgroundColorHintTests
{
    [Fact]
    public void BodyBgcolorHintReachesNativeBackgroundPixels()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        const string content = "<!doctype html><body><div style='height:20px'></div></body>";
        CssStyleSource[] baseStyles = [new("*{margin:0}")];

        var actual = OfflinePageRenderer.Render(content.Replace("<body>", "<body bgcolor=chucknorris>"),
            baseStyles, text, fonts, 40, 30, cancellationToken: TestContext.Current.CancellationToken);
        var expected = OfflinePageRenderer.Render(content,
            [new("*{margin:0}body{background-color:#c00000}")],
            text, fonts, 40, 30, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(new byte[] { 0, 0, 192, 255 }, actual.Frame.Pixels.Span[..4].ToArray());
    }
}
