using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class LchColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("lch(29.2345% 44.2 27)", 125, 35, 41, 255)]
    [InlineData("lch(52.2345% 72.2 56.2)", 198, 93, 6, 255)]
    [InlineData("lch(62.2345% 59.2 126.2)", 104, 166, 57, 255)]
    [InlineData("lch(50 0 1turn)", 119, 119, 119, 255)]
    [InlineData("LCH(50 none none / none)", 119, 119, 119, 0)]
    [InlineData("lch(50 -10 180deg)", 119, 119, 119, 255)]
    [InlineData("lch(-10 230 0)", 0, 0, 0, 255)]
    [InlineData("lch(110 230 0)", 255, 255, 255, 255)]
    [InlineData("lch(50 0 0 / 25%)", 119, 119, 119, 64)]
    public void InSrgbGamutLchColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Fact]
    public void LchPercentChromaAndHueAnglesUseTheirSpecifiedScales()
    {
        var (_, percentageStyle) = Compute("color:lch(50 6.666666666666667% 90deg)");
        var (_, numberStyle) = Compute("color:lch(50 10 90)");
        var (_, gradStyle) = Compute("color:lch(50 10 100grad)");
        var (_, radStyle) = Compute("color:lch(50 10 1.5707963267948966rad)");
        Assert.Equal(numberStyle["color"], percentageStyle["color"]);
        Assert.Equal(numberStyle["color"], gradStyle["color"]);
        Assert.Equal(numberStyle["color"], radStyle["color"]);
    }

    [Theory]
    [InlineData("lch(50, 0, 0)")]
    [InlineData("lch(50 0px 0)")]
    [InlineData("lch(50 10 1px)")]
    [InlineData("lch(50 0)")]
    [InlineData("lch(50 0 0 .5)")]
    [InlineData("lch(50 0 0 / 0.5 / 1)")]
    [InlineData("lch(50 230 0)")]
    public void UnsupportedOrOutOfGamutLchColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void LchWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:lch(29.2345% 44.2 27);" +
            "background:none lch(50 0 0 / 50%);" +
            "border:1px solid lch(52.2345% 72.2 56.2)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(125, 35, 41), style["color"]);
        Assert.Equal(new CssColor(119, 119, 119, 128), style["background-color"]);
        Assert.Equal(new CssColor(198, 93, 6), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbLchCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid lch(50 230 0);" +
            "background:blue;background:none lch(50 230 0)");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(new CssLength(3, "px"), style["border-top-width"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 0, 255), style["background-color"]);
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declarations)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("p{" + declarations + "}")], cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("p")!]);
    }
}
