using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HwbColorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("hwb(0 0% 0%)", 255, 0, 0, 255)]
    [InlineData("hwb(150 20% 10%)", 51, 230, 140, 255)]
    [InlineData("hwb(45 40% 80%)", 85, 85, 85, 255)]
    [InlineData("hwb(240deg 0 0 / 50%)", 0, 0, 255, 128)]
    [InlineData("hwb(-.3333333333333333turn 0% 0%)", 0, 0, 255, 255)]
    [InlineData("hwb(0 20 10)", 230, 51, 51, 255)]
    [InlineData("hwb(0 200% 10%)", 243, 243, 243, 255)]
    [InlineData("HWB(none none none / none)", 255, 0, 0, 0)]
    [InlineData("hwb(150/**/20%/**/10%)", 51, 230, 140, 255)]
    public void HwbFormsComputeStaticSrgbChannels(string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("hwb(0, 0%, 0%)")]
    [InlineData("hwb(0 0% 0% .5)")]
    [InlineData("hwb(0 0% 0% /)")]
    [InlineData("hwb(0 0px 0%)")]
    [InlineData("hwb(1e999 0% 0%)")]
    [InlineData("hwb(0 0% 0% / none / 1)")]
    public void InvalidHwbDoesNotOverrideAnEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void HwbWorksInColorLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:hwb(0 0% 0%);background:none hwb(240 0% 0% / 50%);border:1px solid hwb(120 0% 0%);" +
            "border-color:hwb(0 0% 0%) hwb(120 0% 0%)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
        Assert.Equal(new CssColor(0, 0, 255, 128), style["background-color"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 255, 0), style["border-left-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void InvalidHwbCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid hwb(0,0%,0%);background:blue;background:none hwb(0 0% 0% .5)");
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
