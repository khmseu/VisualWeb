using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class LabColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("lab(29.2345% 39.3825 20.0664)", 125, 35, 41, 255)]
    [InlineData("lab(52.2345 40.1645 59.9971)", 198, 93, 6, 255)]
    [InlineData("lab(29.2345% 39.3825 20.0664 / 25%)", 125, 35, 41, 64)]
    [InlineData("LAB(50 none none / none)", 119, 119, 119, 0)]
    [InlineData("lab(-10 200 -200)", 0, 0, 0, 255)]
    [InlineData("lab(110 -200 200)", 255, 255, 255, 255)]
    public void InSrgbGamutLabColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Fact]
    public void LabAxisPercentagesUseTheSpecifiedReferenceRange()
    {
        var (_, percentageStyle) = Compute("color:lab(50 10% -10%)");
        var (_, numberStyle) = Compute("color:lab(50 12.5 -12.5)");
        Assert.Equal(numberStyle["color"], percentageStyle["color"]);
    }

    [Theory]
    [InlineData("lab(50, 0, 0)")]
    [InlineData("lab(50 0px 0)")]
    [InlineData("lab(50 0)")]
    [InlineData("lab(50 0 0 .5)")]
    [InlineData("lab(50 0 0 / 0.5 / 1)")]
    [InlineData("lab(50 200 200)")]
    public void UnsupportedOrOutOfGamutLabColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void LabWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:lab(29.2345% 39.3825 20.0664);" +
            "background:none lab(50 0 0 / 50%);" +
            "border:1px solid lab(52.2345 40.1645 59.9971)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(125, 35, 41), style["color"]);
        Assert.Equal(new CssColor(119, 119, 119, 128), style["background-color"]);
        Assert.Equal(new CssColor(198, 93, 6), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbLabCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid lab(50 200 200);" +
            "background:blue;background:none lab(50 200 200)");
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
