using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class ProPhotoRgbColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(prophoto-rgb 0.36589 0.41717 0.31333)", 106, 128, 93, 255)]
    [InlineData("color(prophoto-rgb 0.02 0.02 0.02)", 4, 4, 4, 255)]
    [InlineData("color(prophoto-rgb 0.5 0.5 0.5 / 25%)", 146, 146, 146, 64)]
    [InlineData("COLOR(PROPHOTO-RGB none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutProPhotoColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(prophoto-rgb 0.88 0.45 0.10)")]
    [InlineData("color(prophoto-rgb 1.01 0 0)")]
    [InlineData("color(prophoto-rgb 0 -0.01 0)")]
    [InlineData("color(prophoto-rgb 1px 0 0)")]
    [InlineData("color(prophoto-rgb 0.5 0.5)")]
    [InlineData("color(prophoto-rgb 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(prophoto-rgb, 1, 0, 0)")]
    [InlineData("color(prophoto-rgb 1e999 0 0)")]
    public void UnsupportedOrOutOfGamutProPhotoColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void ProPhotoWorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(prophoto-rgb 0.36589 0.41717 0.31333);" +
            "background:none color(prophoto-rgb 0.5 0.5 0.5 / 50%);" +
            "border:1px solid color(prophoto-rgb 0.36589 0.41717 0.31333)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(106, 128, 93), style["color"]);
        Assert.Equal(new CssColor(146, 146, 146, 128), style["background-color"]);
        Assert.Equal(new CssColor(106, 128, 93), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbProPhotoCannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(prophoto-rgb 0.88 0.45 0.10);" +
            "background:blue;background:none color(prophoto-rgb 0.88 0.45 0.10)");
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
