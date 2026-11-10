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
        Assert.Equal(new CssKeyword("solid"), style["text-decoration-style"]);
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
        Assert.Equal(new CssKeyword(keyword == "revert" ? "underline" : "none"), style["text-decoration-line"]);
        if (property == "text-decoration")
        {
            Assert.Equal(new CssKeyword("solid"), style["text-decoration-style"]);
            Assert.Equal(keyword == "inherit" ? new CssColor(0, 0, 0) : style["color"],
                style["text-decoration-color"]);
        }
    }

    [Theory]
    [InlineData("text-decoration:underline")]
    [InlineData("text-decoration-line:underline")]
    [InlineData("text-decoration:UNDERLINE")]
    [InlineData("text-decoration:line-through")]
    [InlineData("text-decoration-line:line-through")]
    [InlineData("text-decoration:LINE-THROUGH")]
    public void SupportedVisibleLineValuesAreAccepted(string declaration)
    {
        var (result, style) = Compute(declaration);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(declaration.Contains("line-through", StringComparison.OrdinalIgnoreCase)
            ? "line-through" : "underline"), style["text-decoration-line"]);
    }

    [Theory]
    [InlineData("text-decoration-style:solid", "solid", "underline")]
    [InlineData("text-decoration-style:DOUBLE", "double", "underline")]
    [InlineData("text-decoration:underline double", "double", "underline")]
    [InlineData("text-decoration:double underline", "double", "underline")]
    [InlineData("text-decoration:double", "double", "none")]
    [InlineData("text-decoration:underline", "solid", "underline")]
    [InlineData("text-decoration:none solid", "solid", "none")]
    [InlineData("text-decoration:none red", "solid", "none")]
    public void SupportedDecorationStylesAreAcceptedAndShorthandResetsOmittedStyle(
        string declaration, string expectedStyle, string expectedLine)
    {
        var (result, style) = Compute(declaration);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expectedStyle), style["text-decoration-style"]);
        Assert.Equal(new CssKeyword(expectedLine), style["text-decoration-line"]);
    }

    [Theory]
    [InlineData("text-decoration:underline double red", "underline", "double")]
    [InlineData("text-decoration:double red underline", "underline", "double")]
    [InlineData("text-decoration:red underline double", "underline", "double")]
    [InlineData("text-decoration:red", "none", "solid")]
    public void TextDecorationShorthandAcceptsSupportedComponentsInAnyOrder(
        string declaration, string expectedLine, string expectedStyle)
    {
        var (result, style) = Compute(declaration);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expectedLine), style["text-decoration-line"]);
        Assert.Equal(new CssKeyword(expectedStyle), style["text-decoration-style"]);
        Assert.Equal(new CssColor(255, 0, 0), style["text-decoration-color"]);
    }

    [Fact]
    public void TextDecorationShorthandResetsOmittedColorToCurrentColor()
    {
        var (result, style) = Compute("color:blue;text-decoration-color:red;text-decoration:underline");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), style["text-decoration-color"]);
    }

    [Theory]
    [InlineData("initial")]
    [InlineData("inherit")]
    [InlineData("unset")]
    [InlineData("revert")]
    public void DecorationStyleCssWideKeywordsUseTheExistingCascade(string keyword)
    {
        var (result, style) = Compute("text-decoration-style:" + keyword);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("solid"), style["text-decoration-style"]);
    }

    [Fact]
    public void DecorationStyleIsNonInheritedButSupportsInherit()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><div id=parent><span id=child>Text</span><span id=inherited>Text</span></div>",
            cancellationToken: Cancellation).Document;
        var parent = document.GetElementById("parent")!;
        var child = document.GetElementById("child")!;
        var inherited = document.GetElementById("inherited")!;
        var result = CssStyleEngine.Compute(document,
            [new("div{text-decoration-style:double}#inherited{text-decoration-style:inherit}")],
            cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("double"), result.Styles[parent]["text-decoration-style"]);
        Assert.Equal(new CssKeyword("solid"), result.Styles[child]["text-decoration-style"]);
        Assert.Equal(new CssKeyword("double"), result.Styles[inherited]["text-decoration-style"]);
    }

    [Theory]
    [InlineData("del")]
    [InlineData("s")]
    [InlineData("strike")]
    public void DeletionElementsHaveLineThroughUaDefaults(string tag)
    {
        var document = HtmlParser.Parse($"<!doctype html><{tag} id=x>Removed</{tag}>",
            cancellationToken: Cancellation).Document;
        var element = document.GetElementById("x")!;
        var defaults = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        var overridden = CssStyleEngine.Compute(document, [new($"{tag}{{text-decoration:none}}")],
            cancellationToken: Cancellation);

        Assert.Empty(defaults.Diagnostics);
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal(new CssKeyword("line-through"), defaults.Styles[element]["text-decoration-line"]);
        Assert.Equal(new CssKeyword("none"), overridden.Styles[element]["text-decoration-line"]);
    }

    [Theory]
    [InlineData("ins")]
    [InlineData("u")]
    public void InsertionElementsHaveUnderlineUaDefaults(string tag)
    {
        var document = HtmlParser.Parse($"<!doctype html><{tag} id=x>Inserted</{tag}>",
            cancellationToken: Cancellation).Document;
        var element = document.GetElementById("x")!;
        var defaults = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        var overridden = CssStyleEngine.Compute(document, [new($"{tag}{{text-decoration:none}}")],
            cancellationToken: Cancellation);

        Assert.Empty(defaults.Diagnostics);
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal(new CssKeyword("underline"), defaults.Styles[element]["text-decoration-line"]);
        Assert.Equal(new CssKeyword("none"), overridden.Styles[element]["text-decoration-line"]);
    }

    [Fact]
    public void LinksUnderlineByDefaultAndTheAuthorCanDisableTheDecoration()
    {
        var document = HtmlParser.Parse("<!doctype html><a id=link href='/next'>Visit</a>",
            cancellationToken: Cancellation).Document;
        var link = document.GetElementById("link")!;
        var ua = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        var author = CssStyleEngine.Compute(document, [new("a{text-decoration:none}")],
            cancellationToken: Cancellation);
        var disabled = CssStyleEngine.Compute(document, includeUserAgent: false,
            cancellationToken: Cancellation);

        Assert.Equal(new CssKeyword("underline"), ua.Styles[link]["text-decoration-line"]);
        Assert.Empty(author.Diagnostics);
        Assert.Equal(new CssKeyword("none"), author.Styles[link]["text-decoration-line"]);
        Assert.Equal(new CssKeyword("none"), disabled.Styles[link]["text-decoration-line"]);
    }

    [Fact]
    public void DecorationColorDefaultsToCurrentColorAndDoesNotInherit()
    {
        const string html = "<div id=parent><a id=link href='/next'>Visit</a></div>";
        var document = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var parent = document.GetElementById("parent")!;
        var link = document.GetElementById("link")!;
        var result = CssStyleEngine.Compute(document,
            [new("div{color:blue;text-decoration-color:red}a{color:green}")],
            includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), result.Styles[parent]["text-decoration-color"]);
        Assert.Equal(new CssColor(0, 128, 0), result.Styles[link]["text-decoration-color"]);

        var inherited = CssStyleEngine.Compute(document,
            [new("div{color:blue;text-decoration-color:red}a{color:green;text-decoration-color:inherit}")],
            includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Empty(inherited.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), inherited.Styles[link]["text-decoration-color"]);
    }

    [Fact]
    public void DecorationColorAcceptsExplicitColorsAndCssWideKeywords()
    {
        var (_, explicitColor) = Compute("color:blue;text-decoration-color:red");
        var (_, initial) = Compute("color:blue;text-decoration-color:initial");
        var (_, current) = Compute("color:blue;text-decoration-color:currentColor");

        Assert.Equal(new CssColor(255, 0, 0), explicitColor["text-decoration-color"]);
        Assert.Equal(new CssColor(0, 0, 255), initial["text-decoration-color"]);
        Assert.Equal(new CssColor(0, 0, 255), current["text-decoration-color"]);
    }

    [Theory]
    [InlineData("text-decoration:overline")]
    [InlineData("text-decoration:blink")]
    [InlineData("text-decoration:none underline")]
    [InlineData("text-decoration:none none")]
    [InlineData("text-decoration:red blue")]
    [InlineData("text-decoration:none red blue")]
    [InlineData("text-decoration:underline double solid")]
    [InlineData("text-decoration:none,none")]
    [InlineData("text-decoration:inherit none")]
    [InlineData("text-decoration:revert-layer")]
    [InlineData("text-decoration-line:overline")]
    [InlineData("text-decoration-line:revert-layer")]
    [InlineData("text-decoration-style:dotted")]
    [InlineData("text-decoration-style:dashed")]
    [InlineData("text-decoration-style:wavy")]
    [InlineData("text-decoration-style:groove")]
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
        var exact = new CssOptions { MaxAssignments = 3 };
        Assert.Empty(CssStyleEngine.Compute(document, [new("a{text-decoration:none}")],
            includeUserAgent: false, options: exact, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document,
            [new("a{text-decoration:none;text-decoration-line:none}")],
            includeUserAgent: false, options: exact, cancellationToken: Cancellation));
        var invalid = CssStyleEngine.Compute(document,
            [new("a{text-decoration:overline;text-decoration:none}")],
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
        Assert.Equal(new CssKeyword("solid"), initial.Styles[element]["text-decoration-style"]);
        var propertyCount = initial.Styles[element].Properties.Count;
        var exact = new CssOptions { MaxAssignments = propertyCount };
        var reset = CssStyleEngine.Compute(document, [new("a{all:initial}")], includeUserAgent: false,
            options: exact, cancellationToken: Cancellation);
        Assert.Empty(reset.Diagnostics);
        Assert.Equal(new CssKeyword("none"), reset.Styles[element]["text-decoration-line"]);
        Assert.Equal(new CssKeyword("solid"), reset.Styles[element]["text-decoration-style"]);
        Assert.Equal(reset.Styles[element]["color"], reset.Styles[element]["text-decoration-color"]);
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
