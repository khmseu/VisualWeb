using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TextTransformTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("none")]
    [InlineData("uppercase")]
    [InlineData("lowercase")]
    public void TextTransformAcceptsSupportedKeywords(string value)
    {
        var (result, style) = Compute("#child{text-transform:" + value + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(value), style["text-transform"]);
    }

    [Fact]
    public void TextTransformDefaultsToInheritedNone()
    {
        var (result, style) = Compute("");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("none"), style["text-transform"]);
    }

    [Theory]
    [InlineData("initial", "none")]
    [InlineData("unset", "uppercase")]
    [InlineData("inherit", "uppercase")]
    [InlineData("revert", "uppercase")]
    public void TextTransformUsesItsInheritedCascade(string keyword, string expected)
    {
        var (result, style) = Compute("#parent{text-transform:uppercase}#child{text-transform:" + keyword + "}");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expected), style["text-transform"]);
    }

    [Theory]
    [InlineData("capitalize")]
    [InlineData("full-width")]
    [InlineData("full-size-kana")]
    [InlineData("invalid")]
    public void UnsupportedTextTransformValuesDoNotOverridePriorValues(string value)
    {
        var (result, style) = Compute("#child{text-transform:lowercase;text-transform:" + value + "}");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssKeyword("lowercase"), style["text-transform"]);
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
