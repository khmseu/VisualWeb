using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class OklchColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("oklch(40.101% 0.12332 21.555)", 125, 35, 41, 255)]
    [InlineData("oklch(59.686% 0.15619 49.7694)", 198, 93, 6, 255)]
    [InlineData("oklch(.65125 .13138 104.097)", 157, 147, 24, 255)]
    [InlineData("oklch(.66016 .15546 134.231)", 104, 166, 57, 255)]
    [InlineData("oklch(72.322% .12403 247.996)", 98, 172, 239, 255)]
    [InlineData("OKLCH(50% none none / none)", 99, 99, 99, 0)]
    [InlineData("oklch(-10 .5 180)", 0, 0, 0, 255)]
    [InlineData("oklch(110 .5 180)", 255, 255, 255, 255)]
    [InlineData("oklch(.5 0 0 / 25%)", 99, 99, 99, 64)]
    public void InSrgbGamutOklchColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Fact]
    public void OklchChromaPercentagesAndHueAnglesUseTheirSpecifiedScales()
    {
        var (_, numberStyle) = Compute("color:oklch(.5 .1 90)");
        var (_, percentageStyle) = Compute("color:oklch(50% 25% 90deg)");
        var (_, gradStyle) = Compute("color:oklch(.5 .1 100grad)");
        var (_, radStyle) = Compute("color:oklch(.5 .1 1.5707963267948966rad)");
        var (_, turnStyle) = Compute("color:oklch(.5 .1 .25turn)");
        Assert.Equal(numberStyle["color"], percentageStyle["color"]);
        Assert.Equal(numberStyle["color"], gradStyle["color"]);
        Assert.Equal(numberStyle["color"], radStyle["color"]);
        Assert.Equal(numberStyle["color"], turnStyle["color"]);
    }

    [Fact]
    public void NegativeAndPowerlessChromaProduceAchromaticColors()
    {
        var (_, negativeStyle) = Compute("color:oklch(.5 -10 180)");
        var (_, powerlessStyle) = Compute("color:oklch(.5 .000004 90)");
        var (_, neutralStyle) = Compute("color:oklch(.5 0 none)");
        Assert.Equal(neutralStyle["color"], negativeStyle["color"]);
        Assert.Equal(neutralStyle["color"], powerlessStyle["color"]);
    }

    [Theory]
    [InlineData("oklch(50%, 0, 0)")]
    [InlineData("oklch(50 0px 0)")]
    [InlineData("oklch(50 .1 1px)")]
    [InlineData("oklch(50 0)")]
    [InlineData("oklch(50 0 0 .5)")]
    [InlineData("oklch(50 0 0 / 0.5 / 1)")]
    [InlineData("oklch(.5 .5 90)")]
    public void UnsupportedOrOutOfGamutOklchColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void OklchWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:oklch(40.101% 0.12332 21.555);" +
            "background:none oklch(.5 0 0 / 50%);" +
            "border:1px solid oklch(59.686% 0.15619 49.7694)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(125, 35, 41), style["color"]);
        Assert.Equal(new CssColor(99, 99, 99, 128), style["background-color"]);
        Assert.Equal(new CssColor(198, 93, 6), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbOklchCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid oklch(.5 .5 90);" +
            "background:blue;background:none oklch(.5 .5 90)");
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
