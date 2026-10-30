using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HslColorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("hsl(0 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(120deg 100% 50%)", 0, 255, 0, 255)]
    [InlineData("hsl(240,100%,50%)", 0, 0, 255, 255)]
    [InlineData("hsla(300 100% 50% / .5)", 255, 0, 255, 128)]
    [InlineData("hsl(.5turn 100 50 / 25%)", 0, 255, 255, 64)]
    [InlineData("hsl(200grad 100% 50%)", 0, 255, 255, 255)]
    [InlineData("hsl(3.141592653589793rad 100% 50%)", 0, 255, 255, 255)]
    [InlineData("hsl(-120deg 100% 50%)", 0, 0, 255, 255)]
    [InlineData("hsl(720deg 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(1e308turn 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(none 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(none none 50%)", 128, 128, 128, 255)]
    [InlineData("hsl(120 0 0 / none)", 0, 0, 0, 0)]
    [InlineData("hsl(120 -1 50)", 128, 128, 128, 255)]
    [InlineData("HSL(30 100% 50%)", 255, 128, 0, 255)]
    [InlineData("hsl(200 80% 60%)", 71, 180, 235, 255)]
    [InlineData("hsl(120/**/100%/**/50%)", 0, 255, 0, 255)]
    [InlineData("hsl(120,100%,50%,50%)", 0, 255, 0, 128)]
    [InlineData("hsla(0,100%,50%)", 255, 0, 0, 255)]
    [InlineData("hsla(300 100% 50% / 2)", 255, 0, 255, 255)]
    [InlineData("hsl(60 100% 50%)", 255, 255, 0, 255)]
    [InlineData("hsl(90 100% 50%)", 128, 255, 0, 255)]
    [InlineData("hsl(150 100% 50%)", 0, 255, 128, 255)]
    [InlineData("hsl(210 100% 50%)", 0, 128, 255, 255)]
    [InlineData("hsl(270 100% 50%)", 128, 0, 255, 255)]
    [InlineData("hsl(330 100% 50%)", 255, 0, 128, 255)]
    [InlineData("hsl(0 100% 0%)", 0, 0, 0, 255)]
    [InlineData("hsl(0 100% 100%)", 255, 255, 255, 255)]
    public void HslFormsComputeStaticSrgbChannels(string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("hsl(0 100%)")]
    [InlineData("hsl(0 100% 50% .5)")]
    [InlineData("hsl(0 100% 50% /)")]
    [InlineData("hsl(0 100% 50% / .5 / 1)")]
    [InlineData("hsl(0 100% 50%, .5)")]
    [InlineData("hsl(0,100%,50%/.5)")]
    [InlineData("hsl(0,100,50)")]
    [InlineData("hsl(none,100%,50%)")]
    [InlineData("hsl(0,100%,50%,none)")]
    [InlineData("hsl(0% 100% 50%)")]
    [InlineData("hsl(0px 100% 50%)")]
    [InlineData("hsl(0 1px 50%)")]
    [InlineData("hsl(1e999 100% 50%)")]
    [InlineData("hsl(0 1e999% 50%)")]
    [InlineData("hsl(0 100% 50% / 1e999)")]
    [InlineData("hsl(0 101% 50%)")]
    [InlineData("hsl(0 100% -1%)")]
    [InlineData("hsl(0 100% 101%)")]
    public void InvalidOrOutOfScopeHslDoesNotOverrideAnEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void HslColorsWorkInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:hsl(0 100% 50%);background:none hsla(240 100% 50%/.5);border:1px solid hsl(120 100% 50%);" +
            "border-color:hsl(0 100% 50%) hsl(120 100% 50%)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
        Assert.Equal(new CssColor(0, 0, 255, 128), style["background-color"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 255, 0), style["border-left-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void InvalidHslCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid hsl(0 101% 50%);background:blue;background:none hsl(0 50%)");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(new CssLength(3, "px"), style["border-top-width"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 0, 255), style["background-color"]);
    }

    [Fact]
    public void HslColorsKeepAssignmentBudgetsAndCascadeImportance()
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        CssStyleSource[] source = [new("p{color:hsl(0 100% 50%)!important;color:hsl(240 100% 50%)}")];
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
