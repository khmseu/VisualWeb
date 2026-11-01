using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class SearchDefaultsTests
{
    [Fact]
    public void OfflineNativeSearchFormMatchesOrdinaryBlock()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:20px;line-height:1}")];
        const string contents = "<div>Find</div><form><input type=search name=q value=abc><button>Go</button></form>";
        var cancellation = TestContext.Current.CancellationToken;
        var actual = OfflinePageRenderer.Render("<!doctype html><search>" + contents + "</search>", styles, text, fonts, 220, 100, cancellationToken: cancellation);
        var expected = OfflinePageRenderer.Render("<!doctype html><div>" + contents + "</div>", styles, text, fonts, 220, 100, cancellationToken: cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }
}
