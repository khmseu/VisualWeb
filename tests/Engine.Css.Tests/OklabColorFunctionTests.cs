using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class OklabColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("oklab(40.101% 0.1147 0.0453)", 125, 35, 41, 255)]
    [InlineData("oklab(59.686% 0.1009 0.1192)", 198, 93, 7, 255)]
    [InlineData("oklab(.65125 -.0320 .1274)", 157, 147, 24, 255)]
    [InlineData("oklab(66.016% -.1084 .1114)", 104, 166, 57, 255)]
    [InlineData("oklab(72.322% -.0465 -.1150)", 98, 172, 239, 255)]
    [InlineData("OKLAB(50% none none / none)", 99, 99, 99, 0)]
    [InlineData("oklab(-10 .5 .5)", 0, 0, 0, 255)]
    [InlineData("oklab(110 .5 .5)", 255, 255, 255, 255)]
    [InlineData("oklab(50% 0 0 / 25%)", 99, 99, 99, 64)]
    public void InSrgbGamutOklabColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Fact]
    public void OklabAxisPercentagesUseTheSpecifiedReferenceRange()
    {
        var (_, percentageStyle) = Compute("color:oklab(.5 25% -25%)");
        var (_, numberStyle) = Compute("color:oklab(.5 .1 -.1)");
        Assert.Equal(numberStyle["color"], percentageStyle["color"]);
    }

    [Theory]
    [InlineData("oklab(50%, 0, 0)")]
    [InlineData("oklab(50 0px 0)")]
    [InlineData("oklab(50 0)")]
    [InlineData("oklab(50 0 0 .5)")]
    [InlineData("oklab(50 0 0 / 0.5 / 1)")]
    [InlineData("oklab(.5 .5 .5)")]
    public void UnsupportedOrOutOfGamutOklabColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void OklabWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:oklab(40.101% 0.1147 0.0453);" +
            "background:none oklab(50% 0 0 / 50%);" +
            "border:1px solid oklab(59.686% 0.1009 0.1192)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(125, 35, 41), style["color"]);
        Assert.Equal(new CssColor(99, 99, 99, 128), style["background-color"]);
        Assert.Equal(new CssColor(198, 93, 7), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbOklabCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid oklab(.5 .5 .5);" +
            "background:blue;background:none oklab(.5 .5 .5)");
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
