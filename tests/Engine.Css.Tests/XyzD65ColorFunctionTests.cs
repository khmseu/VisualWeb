using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class XyzD65ColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(xyz-d65 0.21661 0.14602 0.59452)", 118, 84, 205, 255)]
    [InlineData("color(xyz 95.04559270516716% 100% 108.90577507598784%)", 255, 255, 255, 255)]
    [InlineData("color(xyz-d65 50% 50% 50% / 25%)", 204, 183, 180, 64)]
    [InlineData("COLOR(XYZ-D65 none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutXyzColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(xyz-d65 1 0 0)")]
    [InlineData("color(xyz-d65 0.5 0.5)")]
    [InlineData("color(xyz-d65 1px 0 0)")]
    [InlineData("color(xyz-d65 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(xyz-d65, 1, 0, 0)")]
    [InlineData("color(xyz-d50 0.5 0.5 0.5)")]
    [InlineData("color(xyz-d65 1e999 0 0)")]
    public void UnsupportedOrOutOfGamutXyzColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void XyzWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(xyz-d65 0.21661 0.14602 0.59452);" +
            "background:none color(xyz 95.04559270516716% 100% 108.90577507598784% / 50%);" +
            "border:1px solid color(xyz-d65 0.21661 0.14602 0.59452)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(118, 84, 205), style["color"]);
        Assert.Equal(new CssColor(255, 255, 255, 128), style["background-color"]);
        Assert.Equal(new CssColor(118, 84, 205), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbXyzCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(xyz-d65 1 0 0);" +
            "background:blue;background:none color(xyz-d65 1 0 0)");
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
