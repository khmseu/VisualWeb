using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class LegacyPreDefaultsTests
{
    [Theory]
    [InlineData("listing")]
    [InlineData("xmp")]
    [InlineData("plaintext")]
    public void NativePixelsMatchPreWithExplicitlyRegisteredMonospaceFace(string tag)
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("monospace", font);
        using var paint = new PaintFontRegistry();
        paint.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:20px;line-height:1}")];
        const string content = " a  b\nc d";
        var expected = OfflinePageRenderer.Render("<!doctype html><pre>" + content,
            styles, text, paint, 120, 80, cancellationToken: TestContext.Current.CancellationToken);
        var actual = OfflinePageRenderer.Render("<!doctype html><" + tag + ">" + content,
            styles, text, paint, 120, 80, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.NotEmpty(actual.DisplayList.Commands.OfType<DrawGlyphRun>());
    }

    [Theory]
    [InlineData("listing")]
    [InlineData("xmp")]
    [InlineData("plaintext")]
    public void MissingMonospaceFaceFailsWithoutFallback(string tag)
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var paint = new PaintFontRegistry();
        paint.Register(font);
        Assert.Throws<UnsupportedTextException>(() =>
            OfflinePageRenderer.Render("<!doctype html><" + tag + ">text",
                [new("*{margin:0}")], text, paint, 120, 80,
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
