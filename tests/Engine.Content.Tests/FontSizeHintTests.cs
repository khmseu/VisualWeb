using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class FontSizeHintTests
{
    [Fact]
    public void SimplePageMatchesExplicitCssMetricsAndNativePixels()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:23px;line-height:1}")];
        var actual = OfflinePageRenderer.Render("<!doctype html><p><font id=f size=+1 color=red><span>Sample page</span></font></p>",
            styles, text, fonts, 220, 80, cancellationToken: cancellation);
        var reference = OfflinePageRenderer.Render("<!doctype html><p><span style='font-size:large;color:red'><span>Sample page</span></span></p>",
            styles, text, fonts, 220, 80, cancellationToken: cancellation);
        var different = OfflinePageRenderer.Render("<!doctype html><p><span style='font-size:23px;color:red'><span>Sample page</span></span></p>",
            styles, text, fonts, 220, 80, cancellationToken: cancellation);
        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(new CssLength(18, "px"), actual.Styles.Styles[actual.Html.Document.GetElementById("f")!]["font-size"]);
        Assert.Equal(reference.Layout.Root!.Content, actual.Layout.Root!.Content);
        Assert.Equal(reference.Layout.Root.BorderBox, actual.Layout.Root.BorderBox);
        Assert.Equal(reference.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.NotEqual(different.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        var actualRuns = actual.DisplayList.Commands.OfType<DrawGlyphRun>().ToArray();
        var referenceRuns = reference.DisplayList.Commands.OfType<DrawGlyphRun>().ToArray();
        Assert.NotEmpty(actualRuns);
        Assert.Equal(referenceRuns.Length, actualRuns.Length);
        for (var i = 0; i < actualRuns.Length; i++)
        {
            Assert.Equal(18, actualRuns[i].FontSize);
            Assert.Equal(referenceRuns[i].FontIdentity, actualRuns[i].FontIdentity);
            Assert.Equal(referenceRuns[i].Color, actualRuns[i].Color);
            Assert.Equal(referenceRuns[i].Glyphs.ToArray(), actualRuns[i].Glyphs.ToArray());
        }
    }
}
