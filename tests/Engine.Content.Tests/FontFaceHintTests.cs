using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class FontFaceHintTests
{
    [Fact]
    public void FamilyListSelectsAnExplicitlyRegisteredFaceAndSurvivesRetainedResize()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var regular = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        using var bold = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans-Bold.ttf"));
        var text = new FontSet();
        text.Register("serif", regular);
        text.Register("monospace", bold);
        using var fonts = new PaintFontRegistry();
        fonts.Register(regular);
        fonts.Register(bold);
        CssStyleSource[] styles =
        [
            new("body{margin:0;font-family:serif;font-size:28px;line-height:1}p{margin:0}")
        ];
        const string actualHtml = "<!doctype html><p><font face='Missing, monospace'>Sample text</font></p>";
        const string expectedHtml = "<!doctype html><p><span style='font-family:Missing, monospace'>Sample text</span></p>";

        var actual = OfflinePageRenderer.Render(actualHtml, styles, text, fonts, 180, 70,
            cancellationToken: cancellation);
        var expected = OfflinePageRenderer.Render(expectedHtml, styles, text, fonts, 180, 70,
            cancellationToken: cancellation);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.All(actual.DisplayList.Commands.OfType<DrawGlyphRun>(),
            run => Assert.Equal(bold.Identity, run.FontIdentity));
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());

        var resized = OfflinePageRenderer.RenderParsed(actual.Html, styles, text, fonts, 100, 70,
            cancellationToken: cancellation);
        var expectedResized = OfflinePageRenderer.RenderParsed(expected.Html, styles, text, fonts, 100, 70,
            cancellationToken: cancellation);
        Assert.Same(actual.Html.Document, resized.Html.Document);
        Assert.Equal(expectedResized.Frame.Pixels.ToArray(), resized.Frame.Pixels.ToArray());
    }

    [Fact]
    public void UnregisteredFaceDoesNotFallBackImplicitly()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var regular = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", regular);
        using var fonts = new PaintFontRegistry();
        fonts.Register(regular);

        Assert.Throws<UnsupportedTextException>(() =>
            OfflinePageRenderer.Render("<!doctype html><p><font face=Missing>Sample</font></p>",
                [new("body{margin:0;font-family:serif;font-size:28px;line-height:1}p{margin:0}")],
                text, fonts, 180, 70, cancellationToken: cancellation));
    }
}
