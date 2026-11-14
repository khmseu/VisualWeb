using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class SrgbLinearColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(srgb-linear 0 0 0)", 0, 0, 0, 255)]
    [InlineData("color(srgb-linear 1 1 1)", 255, 255, 255, 255)]
    [InlineData("color(srgb-linear 0.5 0.25 0 / 50%)", 188, 137, 0, 128)]
    [InlineData("color(srgb-linear 50% 25% 0%)", 188, 137, 0, 255)]
    [InlineData("color(srgb-linear 0.435 0.017 0.055)", 176, 35, 66, 255)]
    [InlineData("color(srgb-linear none 0.0031308 0 / none)", 0, 10, 0, 0)]
    [InlineData("COLOR(SRGB-LINEAR 0.5 0.5 0.5 / 2)", 188, 188, 188, 255)]
    public void InGamutLinearSrgbFormsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(display-p3 1 0 0)")]
    [InlineData("color(srgb-linear 1.01 0 0)")]
    [InlineData("color(srgb-linear 0 -0.01 0)")]
    [InlineData("color(srgb-linear 101% 0 0)")]
    [InlineData("color(srgb-linear 1px 0 0)")]
    [InlineData("color(srgb-linear 0.5 0.5)")]
    [InlineData("color(srgb-linear 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(srgb-linear, 1, 0, 0)")]
    [InlineData("color(srgb-linear 1e999 0 0)")]
    public void UnsupportedOrOutOfGamutLinearFormsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void LinearSrgbColorWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(srgb-linear 1 0 0);" +
            "background:none color(srgb-linear 0 0 1 / 50%);" +
            "border:1px solid color(srgb-linear 0 1 0);" +
            "border-color:color(srgb-linear 1 0 0) color(srgb-linear 0 1 0)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
        Assert.Equal(new CssColor(0, 0, 255, 128), style["background-color"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 255, 0), style["border-left-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void InvalidLinearSrgbColorCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(srgb-linear 1.1 0 0);" +
            "background:blue;background:none color(display-p3 1 0 0)");
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
