using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class XyzD50ColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(xyz-d50 0.2005 0.14089 0.4472)", 118, 84, 205, 255)]
    [InlineData("color(xyz-d50 96.42956764295677% 100% 82.51046025104602%)", 255, 255, 255, 255)]
    [InlineData("color(xyz-d50 50% 50% 50% / 25%)", 190, 185, 207, 64)]
    [InlineData("COLOR(XYZ-D50 none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutXyzD50ColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(xyz-d50 1 0 0)")]
    [InlineData("color(xyz-d50 0.5 0.5)")]
    [InlineData("color(xyz-d50 1px 0 0)")]
    [InlineData("color(xyz-d50 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(xyz-d50, 1, 0, 0)")]
    [InlineData("color(xyz-d50 1e999 0 0)")]
    public void UnsupportedOrOutOfGamutXyzD50ColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void XyzD50WorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(xyz-d50 0.2005 0.14089 0.4472);" +
            "background:none color(xyz-d50 50% 50% 50% / 50%);" +
            "border:1px solid color(xyz-d50 0.2005 0.14089 0.4472)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(118, 84, 205), style["color"]);
        Assert.Equal(new CssColor(190, 185, 207, 128), style["background-color"]);
        Assert.Equal(new CssColor(118, 84, 205), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbXyzD50CannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(xyz-d50 1 0 0);" +
            "background:blue;background:none color(xyz-d50 1 0 0)");
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
