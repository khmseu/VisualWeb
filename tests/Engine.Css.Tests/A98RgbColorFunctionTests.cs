using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class A98RgbColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(a98-rgb 0.44091 0.49971 0.37408)", 106, 128, 93, 255)]
    [InlineData("color(a98-rgb 50% 50% 50% / 25%)", 129, 129, 129, 64)]
    [InlineData("color(a98-rgb 1 1 1)", 255, 255, 255, 255)]
    [InlineData("COLOR(A98-RGB none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutA98ColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(a98-rgb 1 0 0)")]
    [InlineData("color(a98-rgb 1.01 0 0)")]
    [InlineData("color(a98-rgb 0 -0.01 0)")]
    [InlineData("color(a98-rgb 1px 0 0)")]
    [InlineData("color(a98-rgb 0.5 0.5)")]
    [InlineData("color(a98-rgb 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(a98-rgb, 1, 0, 0)")]
    public void UnsupportedOrOutOfGamutA98ColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void A98WorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(a98-rgb 0.44091 0.49971 0.37408);" +
            "background:none color(a98-rgb 50% 50% 50% / 50%);" +
            "border:1px solid color(a98-rgb 0.44091 0.49971 0.37408)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(106, 128, 93), style["color"]);
        Assert.Equal(new CssColor(129, 129, 129, 128), style["background-color"]);
        Assert.Equal(new CssColor(106, 128, 93), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbA98CannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(a98-rgb 1 0 0);" +
            "background:blue;background:none color(a98-rgb 1 0 0)");
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
