using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class AnyLinkSelectorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("a", null, false)]
    [InlineData("a", "", true)]
    [InlineData("A", "https://example.test/", true)]
    [InlineData("a", "javascript:alert(1)", true)]
    [InlineData("a", "http://[", true)]
    [InlineData("area", null, false)]
    [InlineData("area", "", true)]
    [InlineData("AREA", "#target", true)]
    [InlineData("link", "https://example.test/", false)]
    [InlineData("form", "", false)]
    [InlineData("div", "", false)]
    public void AnyLinkMatchesHtmlAnchorOrAreaByAttributePresence(string name, string? href, bool expected)
    {
        var element = new DomDocument().CreateElement(name);
        if (href is not null) { element.SetAttribute("HrEf", href); }
        var selector = CssSelectorList.Parse(":ANY-LINK", cancellationToken: Cancellation);
        Assert.Equal(expected, selector.Match(element, Cancellation) is not null);
    }

    [Theory]
    [InlineData(":any-link", 0, 1, 0)]
    [InlineData("a:any-link", 0, 1, 1)]
    [InlineData(":where(:any-link)", 0, 0, 0)]
    [InlineData(":is(:any-link, #id)", 1, 0, 0)]
    [InlineData(":not(:any-link)", 0, 1, 0)]
    [InlineData(":any\\2d link", 0, 1, 0)]
    public void AnyLinkUsesOrdinaryPseudoClassSpecificity(string selector, int ids, int classes, int types)
    {
        Assert.Equal(new CssSpecificity(ids, classes, types),
            Assert.Single(CssSelectorList.Parse(selector, cancellationToken: Cancellation).Specificities));
    }

    [Fact]
    public void ReusedSelectorObservesAttributeMutationsAndCascadeRecomputation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><a id=live href>first</a><a id=plain>second</a><a id=priority href class=preferred>third</a>",
            cancellationToken: Cancellation).Document;
        var live = document.GetElementById("live")!;
        var selector = CssSelectorList.Parse(":any-link", cancellationToken: Cancellation);
        CssStyleSource[] source = [new(":any-link{color:blue}a{color:red}a.preferred{color:green}")];
        var initial = CssStyleEngine.Compute(document, source, cancellationToken: Cancellation);
        Assert.Empty(initial.Diagnostics);
        Assert.NotNull(selector.Match(live, Cancellation));
        Assert.Equal(new CssColor(0, 0, 255), initial.Styles[live]["color"]);
        Assert.Equal(new CssColor(255, 0, 0), initial.Styles[document.GetElementById("plain")!]["color"]);
        Assert.Equal(new CssColor(0, 128, 0), initial.Styles[document.GetElementById("priority")!]["color"]);

        live.RemoveAttribute("href");
        Assert.Null(selector.Match(live, Cancellation));
        var changed = CssStyleEngine.Compute(document, source, cancellationToken: Cancellation);
        Assert.Empty(changed.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), initial.Styles[live]["color"]);
        Assert.Equal(new CssColor(255, 0, 0), changed.Styles[live]["color"]);
        live.SetAttribute("href", "");
        Assert.NotNull(selector.Match(live, Cancellation));
    }

    [Fact]
    public void FragmentScopeRemainsFeaturelessAndSupportsDetachedLinks()
    {
        var document = new DomDocument();
        var fragment = document.CreateDocumentFragment();
        var anchor = document.CreateElement("a");
        var area = document.CreateElement("area");
        var wrapper = document.CreateElement("div");
        anchor.SetAttribute("href", "");
        area.SetAttribute("href", "x");
        fragment.AppendChild(anchor);
        fragment.AppendChild(wrapper);
        wrapper.AppendChild(area);
        var candidates = new[] { anchor, wrapper, area };
        var scope = CssSelectorScope.For(fragment);

        var direct = CssSelectorList.Parse(":scope > :any-link", cancellationToken: Cancellation);
        Assert.Equal(new[] { anchor }, direct.Filter(candidates, scope, Cancellation));
        var nested = CssSelectorList.Parse(":not(:any-link) > :any-link", cancellationToken: Cancellation);
        Assert.Equal(new[] { area }, nested.Filter(candidates, scope, Cancellation));
    }

    [Fact]
    public void LogicalAndFilteredNthSelectorsUseCurrentLinkMembership()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var first = document.CreateElement("a");
        var plain = document.CreateElement("a");
        var second = document.CreateElement("area");
        first.SetAttribute("href", "");
        plain.SetAttribute("class", "fallback");
        second.SetAttribute("href", "x");
        parent.AppendChild(first); parent.AppendChild(plain); parent.AppendChild(second);
        var candidates = new[] { first, plain, second };
        var logical = CssSelectorList.Parse(":is(:any-link, .fallback)", cancellationToken: Cancellation);
        var filtered = CssSelectorList.Parse(":nth-child(2 of :any-link)", cancellationToken: Cancellation);
        Assert.Equal(candidates, logical.Filter(candidates, Cancellation));
        Assert.Equal(new[] { second }, filtered.Filter(candidates, Cancellation));
        first.RemoveAttribute("href");
        Assert.Equal(new[] { plain, second }, logical.Filter(candidates, Cancellation));
        Assert.Empty(filtered.Filter(candidates, Cancellation));
    }

    [Fact]
    public void MatchingUsesTheSharedOperationAndCandidateBudgets()
    {
        var document = new DomDocument();
        var anchor = document.CreateElement("a");
        anchor.SetAttribute("href", "");
        var plain = document.CreateElement("a");
        var candidates = new[] { anchor, plain };
        var exact = CssSelectorList.Parse(":any-link", new() { MaxMatchOperations = 4 }, Cancellation);
        Assert.Equal(new[] { anchor }, exact.Filter(candidates, Cancellation));
        var limited = CssSelectorList.Parse(":any-link", new() { MaxMatchOperations = 3 }, Cancellation);
        Assert.Throws<CssLimitException>(() => limited.Filter(candidates, Cancellation).ToList());
        var candidateLimited = CssSelectorList.Parse(":any-link", new() { MaxElements = 1 }, Cancellation);
        Assert.Throws<CssLimitException>(() => candidateLimited.Filter(candidates, Cancellation).ToList());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => exact.Match(anchor, canceled.Token));
    }

    [Theory]
    [InlineData(":link")]
    [InlineData(":visited")]
    [InlineData(":any-link()")]
    public void VisitedStateAndFunctionalSyntaxRemainUnsupported(string selector)
        => Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));
}
