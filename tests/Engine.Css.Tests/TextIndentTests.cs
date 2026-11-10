using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TextIndentTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("0", 0, "px")]
    [InlineData("12px", 12, "px")]
    [InlineData("-1em", -16, "px")]
    [InlineData("25%", 25, "%")]
    public void TextIndentAcceptsBoundedLengthPercentages(string value, double expected, string unit)
    {
        var (result, style) = Compute("#child{text-indent:" + value + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssLength(expected, unit), style["text-indent"]);
    }

    [Theory]
    [InlineData("initial", 0)]
    [InlineData("unset", 12)]
    [InlineData("inherit", 12)]
    [InlineData("revert", 12)]
    public void TextIndentUsesItsInheritedCascade(string keyword, double expected)
    {
        var (result, style) = Compute("#parent{text-indent:12px}#child{text-indent:" + keyword + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssLength(expected, "px"), style["text-indent"]);
    }

    [Theory]
    [InlineData("2px hanging")]
    [InlineData("2px each-line")]
    [InlineData("calc(2px + 1px)")]
    public void UnsupportedTextIndentComponentsDoNotOverridePriorValues(string value)
    {
        var (result, style) = Compute("#child{text-indent:7px;text-indent:" + value + "}");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssLength(7, "px"), style["text-indent"]);
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declaration)
    {
        var document = HtmlParser.Parse("<!doctype html><div id=parent><p id=child>text</p></div>",
            cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new(declaration)],
            cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("child")!]);
    }
}
