using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class WordSpacingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void WordSpacingDefaultsToInheritedNormal()
    {
        var (result, style) = Compute("");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("normal"), style["word-spacing"]);
    }

    [Theory]
    [InlineData("normal", "normal")]
    [InlineData("0", "0px")]
    [InlineData("2px", "2px")]
    [InlineData("0.5em", "8px")]
    [InlineData("1pt", "1.3333333333333333px")]
    public void WordSpacingAcceptsNormalAndNonnegativeLengths(string value, string expected)
    {
        var (result, style) = Compute("#parent{word-spacing:" + value + "}");

        Assert.Empty(result.Diagnostics);
        if (expected == "normal")
        {
            Assert.Equal(new CssKeyword(expected), style["word-spacing"]);
        }
        else
        {
            var (number, unit) = ParseLength(expected);
            Assert.Equal(new CssLength(number, unit), style["word-spacing"]);
        }
    }

    [Theory]
    [InlineData("initial", "normal")]
    [InlineData("unset", "2px")]
    [InlineData("inherit", "2px")]
    [InlineData("revert", "2px")]
    public void WordSpacingUsesInheritedCascade(string keyword, string expected)
    {
        var (result, style) = Compute("#parent{word-spacing:2px}#child{word-spacing:" + keyword + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(expected == "normal" ? new CssKeyword(expected) : new CssLength(2, "px"),
            style["word-spacing"]);
    }

    [Fact]
    public void WordSpacingResolvesFontRelativeLengthsBeforeInheritance()
    {
        var (result, style) = Compute("#parent{font-size:20px;word-spacing:.5em}#child{font-size:10px}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssLength(10, "px"), style["word-spacing"]);
    }

    [Theory]
    [InlineData("-1px")]
    [InlineData("10%")]
    [InlineData("calc(1px + 1px)")]
    [InlineData("invalid")]
    public void UnsupportedWordSpacingValuesDoNotOverridePriorValues(string value)
    {
        var (result, style) = Compute("#child{word-spacing:3px;word-spacing:" + value + "}");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssLength(3, "px"), style["word-spacing"]);
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declaration)
    {
        var document = HtmlParser.Parse("<!doctype html><div id=parent><p id=child>text</p></div>",
            cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new(declaration)],
            cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("child")!]);
    }

    private static (double Value, string Unit) ParseLength(string value)
    {
        var parts = value[..^2];
        return (double.Parse(parts, System.Globalization.CultureInfo.InvariantCulture), "px");
    }
}
