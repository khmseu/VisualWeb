using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class Rec2020ColorFunctionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("color(rec2020 0.4 0.3 0.2)", 108, 63, 34, 255)]
    [InlineData("color(rec2020 50% 50% 50% / 25%)", 120, 120, 120, 64)]
    [InlineData("color(rec2020 1 1 1)", 255, 255, 255, 255)]
    [InlineData("COLOR(REC2020 none none none / none)", 0, 0, 0, 0)]
    public void InSrgbGamutRec2020ColorsConvertToStaticSrgbChannels(
        string input, byte red, byte green, byte blue, byte alpha)
    {
        var (result, style) = Compute("color:" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue, alpha), style["color"]);
    }

    [Theory]
    [InlineData("color(rec2020 1 0 0)")]
    [InlineData("color(rec2020 1.01 0 0)")]
    [InlineData("color(rec2020 0 -0.01 0)")]
    [InlineData("color(rec2020 1px 0 0)")]
    [InlineData("color(rec2020 0.5 0.5)")]
    [InlineData("color(rec2020 0.5 0.5 0.5 / 0.5 / 1)")]
    [InlineData("color(rec2020, 1, 0, 0)")]
    public void UnsupportedOrOutOfGamutRec2020ColorsDoNotOverrideEarlierColor(string input)
    {
        var (result, style) = Compute("color:red;color:" + input);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
    }

    [Fact]
    public void Rec2020WorksInLonghandsAndAtomicShorthands()
    {
        var (result, style) = Compute(
            "color:color(rec2020 0.4 0.3 0.2);" +
            "background:none color(rec2020 50% 50% 50% / 50%);" +
            "border:1px solid color(rec2020 0.4 0.3 0.2)");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(108, 63, 34), style["color"]);
        Assert.Equal(new CssColor(120, 120, 120, 128), style["background-color"]);
        Assert.Equal(new CssColor(108, 63, 34), style["border-top-color"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
    }

    [Fact]
    public void OutOfSrgbRec2020CannotPartiallyApplyShorthands()
    {
        var (result, style) = Compute(
            "border:3px solid red;border:1px solid color(rec2020 1 0 0);" +
            "background:blue;background:none color(rec2020 1 0 0)");
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
