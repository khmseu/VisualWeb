using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class ColorMixFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color-mix(in srgb, red, blue)", 128, 0, 128, 255)]
    [InlineData("color-mix(IN SRGB, red 25%, blue)", 64, 0, 191, 255)]
    [InlineData("color-mix(in srgb, 25% red, blue)", 64, 0, 191, 255)]
    [InlineData("color-mix(in srgb, red 20%, blue 60%)", 64, 0, 191, 204)]
    [InlineData("color-mix(in srgb, red 70%, blue 70%)", 128, 0, 128, 255)]
    [InlineData("color-mix(in srgb, peru 40%, palegoldenrod)", 225, 192, 127, 255)]
    [InlineData("color-mix(in srgb, rgba(255, 0, 0, 0.7) 25%, rgba(0, 255, 0, 0.2) 75%)", 137, 118, 0, 83)]
    [InlineData("color-mix(in srgb, red 0%, blue 0%)", 128, 0, 128, 0)]
    public void SupportedSrgbMixesNormalizeAndComputePremultipliedChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color-mix(white, black)", 99, 99, 99, 255)]
    [InlineData("color-mix(in oklab, white, black)", 99, 99, 99, 255)]
    [InlineData("color-mix(in oklab, red, blue)", 140, 83, 162, 255)]
    [InlineData("color-mix(in oklab, red 25%, blue)", 81, 71, 210, 255)]
    [InlineData("color-mix(in oklab, white 25%, black 25%)", 99, 99, 99, 128)]
    [InlineData("color-mix(in oklab, rgba(255, 255, 255, 0), black)", 0, 0, 0, 128)]
    public void DefaultAndExplicitOklabMixesConvertThroughSrgb(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color-mix(in srgb-linear, red, blue)", 188, 0, 188, 255)]
    [InlineData("color-mix(in srgb-linear, red 20%, blue 60%)", 137, 0, 225, 204)]
    [InlineData("color-mix(in srgb-linear, rgba(255, 0, 0, 0), blue)", 0, 0, 255, 128)]
    [InlineData("color-mix(in srgb-linear, red 0%, blue 0%)", 188, 0, 188, 0)]
    public void ExplicitLinearSrgbMixesEncodePremultipliedChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color-mix(in lab, white, black)", 119, 119, 119, 255)]
    [InlineData("color-mix(in lab, white 25%, black)", 59, 59, 59, 255)]
    [InlineData("color-mix(in lab, green, blue)", 83, 85, 141, 255)]
    [InlineData("color-mix(in lab, rgba(255, 255, 255, 0.5), black)", 79, 79, 79, 192)]
    public void ExplicitLabMixesConvertThroughSrgb(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Fact]
    public void NestedRgbCommasAreNotTreatedAsMixSeparators()
    {
        var (result, style) = Compute("color:color-mix(in srgb, rgb(255, 0, 0) 25%, rgba(0, 0, 255, 1))");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(64, 0, 191), style["color"]);
    }

    [Theory]
    [InlineData("color-mix(in srgb, red -1%, blue)")]
    [InlineData("color-mix(in srgb, red 101%, blue)")]
    [InlineData("color-mix(in srgb, red, blue, green)")]
    [InlineData("color-mix(red, white)")]
    [InlineData("color-mix(in oklab, red, white)")]
    [InlineData("color-mix(in display-p3, red, blue)")]
    [InlineData("color-mix(in srgb, none, blue)")]
    [InlineData("color-mix(in srgb, red, currentColor)")]
    [InlineData("color-mix(in srgb, color-mix(in srgb, red, blue), green)")]
    public void UnsupportedColorMixFormsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void ColorMixWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color-mix(in srgb, red, blue);" +
            "background:none color-mix(in srgb-linear, white 50%, black);" +
            "border:1px solid color-mix(in lab, white, black)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(128, 0, 128), style["color"]);
        Assert.Equal(new CssColor(188, 188, 188), style["background-color"]);
        Assert.Equal(new CssColor(119, 119, 119), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void UnsupportedMixCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color-mix(in oklab, red, white);" +
            "background:blue;background:none color-mix(in srgb, red -1%, blue)");
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(new CssLength(3, "px"), style["border-top-width"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
        Assert.Equal(new CssColor(0, 0, 255), style["background-color"]);
    }

    [Fact]
    public void OutOfGamutLabMixDoesNotOverrideEarlierColor()
    {
        var (result, style) = Compute("color:green;color:color-mix(in lab, red, blue)");
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 128, 0), style["color"]);
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declarations)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("p{" + declarations + "}")], cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("p")!]);
    }
}
