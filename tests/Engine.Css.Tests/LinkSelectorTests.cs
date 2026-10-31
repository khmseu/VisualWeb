using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class LinkSelectorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("a", null, false)]
    [InlineData("a", "", true)]
    [InlineData("A", "https://example.test/visited", true)]
    [InlineData("a", "javascript:alert(1)", true)]
    [InlineData("a", "http://[", true)]
    [InlineData("area", null, false)]
    [InlineData("area", "", true)]
    [InlineData("AREA", "#target", true)]
    [InlineData("link", "https://example.test/", false)]
    [InlineData("form", "", false)]
    [InlineData("div", "", false)]
    public void LinkMatchesHtmlHyperlinksWithoutExposingHistory(string name, string? href, bool expected)
    {
        var element = new DomDocument().CreateElement(name);
        if (href is not null) { element.SetAttribute("HrEf", href); }
        var link = CssSelectorList.Parse(":LINK", cancellationToken: Cancellation);
        var anyLink = CssSelectorList.Parse(":any-link", cancellationToken: Cancellation);
        Assert.Equal(expected, link.Match(element, Cancellation) is not null);
        Assert.Equal(anyLink.Match(element, Cancellation), link.Match(element, Cancellation));
    }

    [Theory]
    [InlineData(":link", 0, 1, 0)]
    [InlineData("a:link", 0, 1, 1)]
    [InlineData(":where(:link)", 0, 0, 0)]
    [InlineData(":is(:link, #id)", 1, 0, 0)]
    [InlineData(":not(:link)", 0, 1, 0)]
    [InlineData(":l\\69 nk", 0, 1, 0)]
    public void LinkUsesOrdinaryPseudoClassSpecificity(string selector, int ids, int classes, int types)
    {
        Assert.Equal(new CssSpecificity(ids, classes, types),
            Assert.Single(CssSelectorList.Parse(selector, cancellationToken: Cancellation).Specificities));
    }

    [Fact]
    public void LinkMatchingAndCascadeFollowCurrentHrefWithoutMutatingPriorSnapshots()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><a id=live href>first</a><a id=plain>second</a><a id=priority href class=preferred>third</a>",
            cancellationToken: Cancellation).Document;
        var live = document.GetElementById("live")!;
        var selector = CssSelectorList.Parse(":link", cancellationToken: Cancellation);
        CssStyleSource[] source = [new(":link{color:blue}a{color:red}a.preferred{color:green}")];
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
    public void LinkComposesWithFragmentScopeAndFilteredNthSelectors()
    {
        var document = new DomDocument();
        var fragment = document.CreateDocumentFragment();
        var first = document.CreateElement("a");
        var wrapper = document.CreateElement("div");
        var second = document.CreateElement("area");
        first.SetAttribute("href", "");
        second.SetAttribute("href", "x");
        fragment.AppendChild(first);
        fragment.AppendChild(wrapper);
        fragment.AppendChild(second);
        var candidates = new[] { first, wrapper, second };
        var scope = CssSelectorScope.For(fragment);

        var direct = CssSelectorList.Parse(":scope > :link", cancellationToken: Cancellation);
        var logical = CssSelectorList.Parse(":is(:link, div)", cancellationToken: Cancellation);
        var filtered = CssSelectorList.Parse(":nth-child(2 of :link)", cancellationToken: Cancellation);
        Assert.Equal(new[] { first, second }, direct.Filter(candidates, scope, Cancellation));
        Assert.Equal(candidates, logical.Filter(candidates, scope, Cancellation));
        Assert.Equal(new[] { second }, filtered.Filter(candidates, scope, Cancellation));
        first.RemoveAttribute("href");
        Assert.Equal(new[] { second }, direct.Filter(candidates, scope, Cancellation));
        Assert.Empty(filtered.Filter(candidates, scope, Cancellation));
    }

    [Fact]
    public void LinkMatchingUsesSharedOperationAndCandidateBudgetsAndCancellation()
    {
        var document = new DomDocument();
        var anchor = document.CreateElement("a");
        anchor.SetAttribute("href", "");
        var plain = document.CreateElement("a");
        var candidates = new[] { anchor, plain };
        var exact = CssSelectorList.Parse(":link", new() { MaxMatchOperations = 4 }, Cancellation);
        Assert.Equal(new[] { anchor }, exact.Filter(candidates, Cancellation));
        var limited = CssSelectorList.Parse(":link", new() { MaxMatchOperations = 3 }, Cancellation);
        Assert.Throws<CssLimitException>(() => limited.Filter(candidates, Cancellation).ToList());
        var candidateLimited = CssSelectorList.Parse(":link", new() { MaxElements = 1 }, Cancellation);
        Assert.Throws<CssLimitException>(() => candidateLimited.Filter(candidates, Cancellation).ToList());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => exact.Match(anchor, canceled.Token));
    }

    [Fact]
    public void VisitedSelectorsRemainExplicitAndRejectTheWholeMixedRule()
    {
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":visited", cancellationToken: Cancellation));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":link()", cancellationToken: Cancellation));
        var document = HtmlParser.Parse("<!doctype html><a id=link href>x</a>", cancellationToken: Cancellation).Document;
        CssStyleSource[] source = [new(":link{color:blue}:link,:visited{color:red}")];
        var result = CssStyleEngine.Compute(document, source, cancellationToken: Cancellation);
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "unsupported-selector");
        Assert.Equal(new CssColor(0, 0, 255), result.Styles[document.GetElementById("link")!]["color"]);
    }
}
