using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TextAlignLastTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("auto")]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("center")]
    [InlineData("justify")]
    public void TextAlignLastAcceptsSupportedKeywords(string value)
    {
        var (result, style) = Compute("#child{text-align-last:" + value + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(value), style["text-align-last"]);
    }

    [Fact]
    public void TextAlignLastDefaultsToAuto()
    {
        var (result, style) = Compute("");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("auto"), style["text-align-last"]);
    }

    [Theory]
    [InlineData("initial", "auto")]
    [InlineData("unset", "center")]
    [InlineData("inherit", "center")]
    [InlineData("revert", "center")]
    public void TextAlignLastUsesItsInheritedCascade(string keyword, string expected)
    {
        var (result, style) = Compute("#parent{text-align-last:center}#child{text-align-last:" + keyword + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expected), style["text-align-last"]);
    }

    [Theory]
    [InlineData("match-parent")]
    [InlineData("justify-all")]
    [InlineData("invalid")]
    public void UnsupportedTextAlignLastValuesDoNotOverridePriorValues(string value)
    {
        var (result, style) = Compute("#child{text-align-last:right;text-align-last:" + value + "}");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssKeyword("right"), style["text-align-last"]);
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
