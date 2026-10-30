using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class ModernRgbTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("rgb(255 0 128)", 255, 0, 128, 255)]
    [InlineData("rgba(100% 0% 50%)", 255, 0, 128, 255)]
    [InlineData("RGB(100% 0 50%)", 255, 0, 128, 255)]
    [InlineData("rgb( 255 \t 0 \n 128 / 50% )", 255, 0, 128, 128)]
    [InlineData("rgba(255 0 128/.5)", 255, 0, 128, 128)]
    [InlineData("rgb(255 0 0/none)", 255, 0, 0, 0)]
    [InlineData("rgb(none 50% NONE / 1)", 0, 128, 0, 255)]
    [InlineData("rgb(-10 260 50% / -1)", 0, 255, 128, 0)]
    [InlineData("rgb(0.5 0 0 / 200%)", 1, 0, 0, 255)]
    [InlineData("r\\67 b(255 0 0)", 255, 0, 0, 255)]
    [InlineData("rgb(255/**/ 0 0)", 255, 0, 0, 255)]
    [InlineData("rgb(255/**/0 0)", 255, 0, 0, 255)]
    [InlineData("rgb(255 0/**/0)", 255, 0, 0, 255)]
    [InlineData("rgb(1%2%3%)", 3, 5, 8, 255)]
    [InlineData("rgb(1-2-3)", 1, 0, 0, 255)]
    [InlineData("rgb(none/**/none/**/none)", 0, 0, 0, 255)]
    public void ModernSyntaxComputesStaticSrgbChannels(string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("rgb(255,0,128)", 255, 0, 128, 255)]
    [InlineData("rgba(100%,0%,50%,.5)", 255, 0, 128, 128)]
    [InlineData("rgb(255, 0, 0, 50%)", 255, 0, 0, 128)]
    public void LegacyCommaSyntaxRemainsSupported(string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("rgb(255 0)")]
    [InlineData("rgb(255 0 0 .5)")]
    [InlineData("rgb(255 0 0 /)")]
    [InlineData("rgb(255 0 0 / .5 / 1)")]
    [InlineData("rgb(255 0 0, .5)")]
    [InlineData("rgb(255,0,0/.5)")]
    [InlineData("rgb(255,0 0)")]
    [InlineData("rgb(100%,0,0)")]
    [InlineData("rgb(none,0,0)")]
    [InlineData("rgb(1e999 0 0)")]
    [InlineData("rgb(1px 0 0)")]
    public void InvalidRgbSyntaxDoesNotOverrideAnEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void ModernColorsWorkInLonghandsAndAtomicBorderAndBackgroundShorthands()
    {
        var (result, style) = Compute(
            "color:rgb(100% 0 0);background:none rgba(0 50% 255/.5);border:1px solid rgb(0 0 255);" +
            "border-color:rgb(255 0 0) rgb(0 255 0)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
        Assert.Equal(new CssColor(0, 128, 255, 128), style["background-color"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 255, 0), style["border-left-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void AdjacentColorTokensWorkInShorthands()
    {
        var (result, style) = Compute("border:1px solid rgb(255/**/0 0);background:rgb(1%2%3%)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(3, 5, 8), style["background-color"]);
    }

    [Fact]
    public void InvalidModernColorsCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid rgb(0 0 0, .5);background:blue;background:none rgb(1 2)");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(new CssLength(3, "px"), style["border-top-width"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 0, 255), style["background-color"]);
    }

    [Fact]
    public void ModernColorsKeepAssignmentBudgetsAndCascadeImportance()
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        CssStyleSource[] source = [new("p{color:rgb(255 0 0)!important;color:rgb(0 0 255)}")];
        var result = CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: new() { MaxAssignments = 2 }, cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), result.Styles[document.GetElementById("p")!]["color"]);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declarations)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("p{" + declarations + "}")], cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("p")!]);
    }
}
