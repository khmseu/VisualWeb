using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class NoTextDecorationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("text-decoration:none")]
    [InlineData("text-decoration-line:none")]
    [InlineData("text-decoration:NoNe")]
    [InlineData("text-decoration:n\\6f ne")]
    [InlineData("text-decoration:/**/none!important")]
    [InlineData("text-decoration-line:NONE")]
    public void NoneProducesTheInitialNonInheritedLineValue(string declaration)
    {
        var (result, style) = Compute(declaration);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("none"), style["text-decoration-line"]);
    }

    [Theory]
    [InlineData("text-decoration", "initial")]
    [InlineData("text-decoration", "inherit")]
    [InlineData("text-decoration", "unset")]
    [InlineData("text-decoration", "revert")]
    [InlineData("text-decoration-line", "initial")]
    [InlineData("text-decoration-line", "inherit")]
    [InlineData("text-decoration-line", "unset")]
    [InlineData("text-decoration-line", "revert")]
    public void CssWideKeywordsUseTheExistingCascade(string property, string keyword)
    {
        var (result, style) = Compute(property + ":" + keyword);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("none"), style["text-decoration-line"]);
    }

    [Theory]
    [InlineData("text-decoration:underline")]
    [InlineData("text-decoration:overline")]
    [InlineData("text-decoration:line-through")]
    [InlineData("text-decoration:blink")]
    [InlineData("text-decoration:none underline")]
    [InlineData("text-decoration:none solid")]
    [InlineData("text-decoration:none red")]
    [InlineData("text-decoration:none none")]
    [InlineData("text-decoration:none,none")]
    [InlineData("text-decoration:inherit none")]
    [InlineData("text-decoration:revert-layer")]
    [InlineData("text-decoration-line:underline")]
    [InlineData("text-decoration-line:overline")]
    [InlineData("text-decoration-line:revert-layer")]
    [InlineData("text-decoration-style:solid")]
    [InlineData("text-decoration-color:red")]
    [InlineData("text-decoration-thickness:1px")]
    public void VisibleOrBroaderDecorationsRemainDiagnostics(string declaration)
    {
        var (result, style) = Compute("text-decoration:none;" + declaration);
        Assert.Single(result.Diagnostics);
        Assert.Equal(new CssKeyword("none"), style["text-decoration-line"]);
    }

    [Fact]
    public void NoneUsesTheExistingSharedAssignmentLimit()
    {
        var document = HtmlParser.Parse("<!doctype html><a id=link>Visit</a>", cancellationToken: Cancellation).Document;
        var exact = new CssOptions { MaxAssignments = 1 };
        Assert.Empty(CssStyleEngine.Compute(document, [new("a{text-decoration:none}")],
            includeUserAgent: false, options: exact, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document,
            [new("a{text-decoration:none;text-decoration-line:none}")],
            includeUserAgent: false, options: exact, cancellationToken: Cancellation));
        var invalid = CssStyleEngine.Compute(document,
            [new("a{text-decoration:underline;text-decoration:none}")],
            includeUserAgent: false, options: exact, cancellationToken: Cancellation);
        Assert.Single(invalid.Diagnostics);
    }

    [Fact]
    public void InitialValueAndAllShorthandIncludeTheNewLonghandAtTheExactLimit()
    {
        var document = HtmlParser.Parse("<!doctype html><a id=link>Visit</a>", cancellationToken: Cancellation).Document;
        var element = document.GetElementById("link")!;
        var initial = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssKeyword("none"), initial.Styles[element]["text-decoration-line"]);
        var propertyCount = initial.Styles[element].Properties.Count;
        var exact = new CssOptions { MaxAssignments = propertyCount };
        var reset = CssStyleEngine.Compute(document, [new("a{all:initial}")], includeUserAgent: false,
            options: exact, cancellationToken: Cancellation);
        Assert.Empty(reset.Diagnostics);
        Assert.Equal(new CssKeyword("none"), reset.Styles[element]["text-decoration-line"]);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, [new("a{all:initial}")],
            includeUserAgent: false, options: exact with { MaxAssignments = propertyCount - 1 }, cancellationToken: Cancellation));
    }

    private static (CssStyleResult Result, CssComputedStyle Style) Compute(string declaration)
    {
        var document = HtmlParser.Parse("<!doctype html><div><a id=link href='/next'>Visit</a></div>",
            cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("a{" + declaration + "}")], cancellationToken: Cancellation);
        return (result, result.Styles[document.GetElementById("link")!]);
    }
}
