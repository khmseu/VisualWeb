using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class BodyLinkColorHintTests
{
    [Fact]
    public void BodyLinkHintChangesInheritedGlyphColorAndRenderedPixels()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        const string page = "<!doctype html><body><p><a href='#target'>Sample</a></p></body>";
        CssStyleSource[] baseStyles = [new("*{margin:0}body{font-size:28px;line-height:1}")];

        var actual = OfflinePageRenderer.Render(page.Replace("<body>", "<body link=chucknorris>"),
            baseStyles, text, fonts, 220, 60, cancellationToken: TestContext.Current.CancellationToken);
        var expected = OfflinePageRenderer.Render(page,
            [.. baseStyles, new CssStyleSource("a:link{color:#c00000}")],
            text, fonts, 220, 60, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(new CssColor(192, 0, 0),
            Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>()).Color);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }
}
