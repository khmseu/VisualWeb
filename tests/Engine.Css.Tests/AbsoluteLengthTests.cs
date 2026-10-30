using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class AbsoluteLengthTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("1in", 96)]
    [InlineData("2.54cm", 96)]
    [InlineData("25.4mm", 96)]
    [InlineData("101.6Q", 96)]
    [InlineData("72pt", 96)]
    [InlineData("6pc", 96)]
    [InlineData("0.5IN", 48)]
    [InlineData("12\\70 t", 16)]
    public void AbsoluteLengthsComputeToCssPixels(string input, double expected)
    {
        var (result, style) = Compute("width:" + input);
        Assert.Empty(result.Diagnostics);
        var length = Assert.IsType<CssLength>(style["width"]);
        Assert.Equal("px", length.Unit);
        Assert.Equal(expected, length.Value, 10);
    }

    [Theory]
    [InlineData("width", "1in", 96)]
    [InlineData("height", "2pc", 32)]
    [InlineData("min-width", "18pt", 24)]
    [InlineData("max-height", "36pt", 48)]
    [InlineData("margin-left", "-.5in", -48)]
    [InlineData("padding-right", "1pc", 16)]
    [InlineData("border-left-width", ".75pt", 1)]
    [InlineData("border-spacing", "6pt", 8)]
    [InlineData("font-size", "12pt", 16)]
    [InlineData("line-height", "18pt", 24)]
    public void SupportedLengthPropertiesShareAbsoluteUnitConversion(string property, string input, double expected)
    {
        var (result, style) = Compute("border-style:solid;" + property + ":" + input);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssLength(expected, "px"), style[property]);
    }

    [Fact]
    public void ShorthandsExpandAbsoluteLengthsBeforeTheNormalCascade()
    {
        var (result, style) = Compute(
            "margin:-.5in 6pt;padding:1pc 12pt;border:.75pt solid red;font:12pt/18pt serif;width:1in!important;width:2in");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssLength(-48, "px"), style["margin-top"]);
        Assert.Equal(new CssLength(8, "px"), style["margin-left"]);
        Assert.Equal(new CssLength(16, "px"), style["padding-top"]);
        Assert.Equal(new CssLength(16, "px"), style["padding-right"]);
        Assert.Equal(new CssLength(1, "px"), style["border-left-width"]);
        Assert.Equal(new CssLength(16, "px"), style["font-size"]);
        Assert.Equal(new CssLength(24, "px"), style["line-height"]);
        Assert.Equal(new CssLength(96, "px"), style["width"]);
    }

    [Fact]
    public void InheritedAbsoluteFontAndLineHeightAreAlreadyComputedPixels()
    {
        var document = HtmlParser.Parse("<!doctype html><div style='font-size:12pt;line-height:18pt'><p id=p>Text</p></div>",
            cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        var style = result.Styles[document.GetElementById("p")!];
        Assert.Equal(new CssLength(16, "px"), style["font-size"]);
        Assert.Equal(new CssLength(24, "px"), style["line-height"]);
    }

    [Theory]
    [InlineData("width", "-1in")]
    [InlineData("padding-left", "-1pt")]
    [InlineData("border-spacing", "-1mm")]
    [InlineData("font-size", "-1pc")]
    [InlineData("width", "1furlong")]
    [InlineData("width", "1e999pt")]
    public void InvalidLengthsRemainDiagnosticsAndDoNotOverrideValidValues(string property, string input)
    {
        var (result, style) = Compute(property + ":10px;" + property + ":" + input);
        Assert.Single(result.Diagnostics);
        Assert.Equal(new CssLength(10, "px"), style[property]);
    }

    [Theory]
    [InlineData("width")]
    [InlineData("font-size")]
    public void SelectedAbsoluteLengthOverflowFailsExplicitly(string property)
    {
        Assert.Throws<CssLimitException>(() => Compute(property + ":1e308in"));
    }

    [Fact]
    public void AbsoluteLengthsUseTheExistingExpandedAssignmentLimit()
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        CssStyleSource[] source = [new("p{margin:1pt}")];
        Assert.Empty(CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: new() { MaxAssignments = 4 }, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: new() { MaxAssignments = 3 }, cancellationToken: Cancellation));
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declarations)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("p{" + declarations + "}")], cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("p")!]);
    }
}
