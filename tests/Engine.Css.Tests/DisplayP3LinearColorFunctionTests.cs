using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class DisplayP3LinearColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(display-p3-linear 0.3081 0.014 0.0567)", 165, 5, 66, 255)]
    [InlineData("color(display-p3-linear 50% 50% 50% / 25%)", 188, 188, 188, 64)]
    [InlineData("color(display-p3-linear 1 1 1)", 255, 255, 255, 255)]
    [InlineData("COLOR(DISPLAY-P3-LINEAR none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutLinearDisplayP3ColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(display-p3-linear 1 0 0)")]
    [InlineData("color(display-p3-linear 1.01 0 0)")]
    [InlineData("color(display-p3-linear 0 -0.01 0)")]
    [InlineData("color(display-p3-linear 1px 0 0)")]
    [InlineData("color(display-p3-linear 0.5 0.5)")]
    [InlineData("color(display-p3-linear 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(display-p3-linear, 1, 0, 0)")]
    public void UnsupportedOrOutOfGamutLinearDisplayP3ColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void LinearDisplayP3WorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(display-p3-linear 0.3081 0.014 0.0567);" +
            "background:none color(display-p3-linear 50% 50% 50% / 50%);" +
            "border:1px solid color(display-p3-linear 0.3081 0.014 0.0567)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(165, 5, 66), style["color"]);
        Assert.Equal(new CssColor(188, 188, 188, 128), style["background-color"]);
        Assert.Equal(new CssColor(165, 5, 66), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbLinearDisplayP3CannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(display-p3-linear 1 0 0);" +
            "background:blue;background:none color(display-p3-linear 1 0 0)");
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
