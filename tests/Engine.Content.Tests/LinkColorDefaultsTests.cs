using VisualWeb.Engine.Css;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class LinkColorDefaultsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");

    [Theory]
    [InlineData("")]
    [InlineData("/next")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://[invalid")]
    public void DefaultLinkAndDescendantMatchExplicitBlueNativePixelsWithoutDecoration(string href)
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:28px;line-height:1}a{text-decoration:none}")];
        var actual = OfflinePageRenderer.Render(
            "<!doctype html><p><a href='" + href + "'><span>Sample</span></a></p>",
            styles, text, fonts, 220, 60, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(
            "<!doctype html><p><span style='color:#0000ee'><span>Sample</span></span></p>",
            styles, text, fonts, 220, 60, cancellationToken: Cancellation);
        var plain = OfflinePageRenderer.Render(
            "<!doctype html><p><span><span>Sample</span></span></p>",
            styles, text, fonts, 220, 60, cancellationToken: Cancellation);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 238), Assert.Single(actual.DisplayList.Commands.OfType<DrawGlyphRun>()).Color);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.NotEqual(plain.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Fact]
    public void AuthorColorAndDisabledUserAgentMatchIndependentNativePixelReferences()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] common =
            [new("*{margin:0}html,body,p{display:block}body{font-size:28px;line-height:1}a{text-decoration:none}")];
        const string link = "<!doctype html><p><a href='/next'><span>Sample</span></a></p>";
        var overridden = OfflinePageRenderer.Render(link,
            [.. common, new("a{color:#cb2040}")], text, fonts, 220, 60, cancellationToken: Cancellation);
        var explicitRed = OfflinePageRenderer.Render(
            "<!doctype html><p><span style='color:#cb2040'><span>Sample</span></span></p>",
            common, text, fonts, 220, 60, cancellationToken: Cancellation);
        var defaultBlue = OfflinePageRenderer.Render(link, common, text, fonts, 220, 60,
            cancellationToken: Cancellation);
        var withoutUa = OfflinePageRenderer.Render(link, common, text, fonts, 220, 60,
            new() { IncludeUserAgentStyle = false }, Cancellation);
        var explicitBlack = OfflinePageRenderer.Render(
            "<!doctype html><p><span style='color:#000000'><span>Sample</span></span></p>",
            common, text, fonts, 220, 60, new() { IncludeUserAgentStyle = false }, Cancellation);

        Assert.Equal(new CssColor(203, 32, 64), Assert.Single(overridden.DisplayList.Commands.OfType<DrawGlyphRun>()).Color);
        Assert.Equal(explicitRed.Frame.Pixels.ToArray(), overridden.Frame.Pixels.ToArray());
        Assert.NotEqual(defaultBlue.Frame.Pixels.ToArray(), overridden.Frame.Pixels.ToArray());
        Assert.Equal(new CssColor(0, 0, 0), Assert.Single(withoutUa.DisplayList.Commands.OfType<DrawGlyphRun>()).Color);
        Assert.Equal(explicitBlack.Frame.Pixels.ToArray(), withoutUa.Frame.Pixels.ToArray());
    }
}
