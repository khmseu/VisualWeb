using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class ScopeSelectorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document() => HtmlParser.Parse(
        "<!doctype html><div id=box><p id=a class=item>A</p><p id=b class=item><span id=c></span></p></div><p id=d></p>",
        cancellationToken: Cancellation).Document;
    private static List<DomElement> All(DomDocument document) => document.Descendants().OfType<DomElement>().ToList();
    private static string Ids(IEnumerable<DomElement> elements)
        => string.Join(",", elements.Select(e => e.GetAttribute("id") ?? e.LocalName));
    private static string Matches(string selector, DomDocument document, CssSelectorScope? scope)
        => Ids(CssSelectorList.Parse(selector, cancellationToken: Cancellation).Filter(All(document), scope, Cancellation));

    [Theory]
    [InlineData(":scope", "html")]
    [InlineData(":scope > *", "head,body")]
    [InlineData(":root:scope", "html")]
    [InlineData(":not(:scope)", "head,body,box,a,b,c,d")]
    public void WithoutScopingRootScopeIsTheDocumentRoot(string selector, string expected)
    {
        var document = Document();
        Assert.Equal(expected, Matches(selector, document, null));
        var parsed = CssSelectorList.Parse(selector, cancellationToken: Cancellation);
        Assert.Equal(expected, Ids(All(document).Where(e => parsed.Match(e, Cancellation) is not null)));
    }

    [Theory]
    [InlineData(":scope", "box")]
    [InlineData(":scope > p", "a,b")]
    [InlineData(":scope p", "a,b")]
    [InlineData(":scope > p > span", "c")]
    [InlineData("body :scope > p", "a,b")]
    [InlineData("html > body > :scope span", "c")]
    [InlineData(":scope + p", "d")]
    [InlineData(":scope ~ *", "d")]
    [InlineData("#a:scope", "")]
    [InlineData("div:scope > .item:first-child", "a")]
    [InlineData(":is(:scope) > p", "a,b")]
    [InlineData(":where(:scope > p)", "a,b")]
    [InlineData(":not(:scope) > p", "d")]
    [InlineData("p:not(:scope > p)", "d")]
    [InlineData(":is(#box, :scope) span", "c")]
    [InlineData(":scope :scope", "")]
    public void ExplicitElementScopeMatchesThatElementAndCombinatorsMayLookOutside(string selector, string expected)
    {
        var document = Document();
        Assert.Equal(expected, Matches(selector, document, CssSelectorScope.For(document.GetElementById("box")!)));
    }

    [Theory]
    [InlineData(":scope", "html")]
    [InlineData(":scope > body", "body")]
    [InlineData(":scope > html", "")]
    [InlineData(":scope div", "box")]
    public void DocumentScopingRootResolvesScopeToTheDocumentElement(string selector, string expected)
    {
        var document = Document();
        Assert.Equal(expected, Matches(selector, document, CssSelectorScope.For(document)));
    }

    [Fact]
    public void ReusedSelectorListsAndScopesNeverRetainAnotherCallsScope()
    {
        var document = Document();
        var selector = CssSelectorList.Parse(":scope > *", cancellationToken: Cancellation);
        var box = CssSelectorScope.For(document.GetElementById("box")!);
        var body = CssSelectorScope.For(document.Body!);
        var other = Document();
        var otherScope = CssSelectorScope.For(other.GetElementById("b")!);
        for (var round = 0; round < 2; round++)
        {
            Assert.Equal("a,b", Ids(selector.Filter(All(document), box, Cancellation)));
            Assert.Equal("c", Ids(selector.Filter(All(other), otherScope, Cancellation)));
            Assert.Equal("box,d", Ids(selector.Filter(All(document), body, Cancellation)));
            Assert.Equal("head,body", Ids(selector.Filter(All(document), Cancellation)));
            Assert.Null(selector.Match(document.GetElementById("a")!, Cancellation));
            Assert.NotNull(selector.Match(document.GetElementById("a")!, box, Cancellation));
            Assert.Null(selector.Match(other.GetElementById("c")!, box, Cancellation));
        }
        var lazy = selector.Filter(All(document), box, Cancellation);
        Assert.Equal("box,d", Ids(selector.Filter(All(document), body, Cancellation)));
        Assert.Equal("a,b", Ids(lazy));
        Assert.Same(document.Body, body.Root);
    }

    [Fact]
    public void DetachedRootsUseExplicitScopeButHaveNoDefaultRoot()
    {
        var document = new DomDocument();
        var root = document.CreateElement("section");
        var child = document.CreateElement("p");
        var grandchild = document.CreateElement("span");
        root.AppendChild(child); child.AppendChild(grandchild);
        var detached = new List<DomElement> { root, child, grandchild };
        var scope = CssSelectorList.Parse(":scope", cancellationToken: Cancellation);
        Assert.Empty(scope.Filter(detached, Cancellation));
        Assert.Equal("section", Ids(scope.Filter(detached, CssSelectorScope.For(root), Cancellation)));
        Assert.Equal("span", Ids(CssSelectorList.Parse("section :scope > *", cancellationToken: Cancellation)
            .Filter(detached, CssSelectorScope.For(child), Cancellation)));
    }

    [Theory]
    [InlineData(":scope", "")]
    [InlineData(":scope > p", "top,second")]
    [InlineData(":scope p", "top,nested,second")]
    [InlineData(":scope > div > p", "nested")]
    [InlineData(":is(:scope) > p", "top,second")]
    [InlineData(":where(:scope, .x) > p", "top,second")]
    [InlineData(":not(:scope) > p", "nested")]
    [InlineData(":not(.x) > p", "nested")]
    [InlineData(":not(:not(:scope)) > p", "top,second")]
    [InlineData("* > p", "nested")]
    [InlineData(":scope > :scope", "")]
    [InlineData("div :scope > p", "")]
    [InlineData(":is(div :scope) > p", "")]
    [InlineData(":not(div :scope) > p", "top,nested,second")]
    [InlineData(":scope + p", "")]
    public void FragmentScopeIsAFeaturelessVirtualRootNotAnElement(string selector, string expected)
    {
        var document = new DomDocument();
        var fragment = document.CreateDocumentFragment();
        var top = Element(document, "p", "top");
        var wrapper = Element(document, "div", "wrapper");
        var nested = Element(document, "p", "nested");
        var second = Element(document, "p", "second");
        wrapper.AppendChild(nested);
        fragment.AppendChild(top); fragment.AppendChild(wrapper); fragment.AppendChild(second);
        var candidates = new List<DomElement> { top, wrapper, nested, second };
        var parsed = CssSelectorList.Parse(selector, cancellationToken: Cancellation);
        Assert.Equal(expected, Ids(parsed.Filter(candidates, CssSelectorScope.For(fragment), Cancellation)));
        var unrelated = document.CreateDocumentFragment();
        Assert.Empty(CssSelectorList.Parse(":scope > p", cancellationToken: Cancellation)
            .Filter(candidates, CssSelectorScope.For(unrelated), Cancellation));
    }

    [Theory]
    [InlineData(":scope", 0, 1, 0)]
    [InlineData(":scope > p", 0, 1, 1)]
    [InlineData("div:scope.item", 0, 2, 1)]
    [InlineData(":where(:scope)", 0, 0, 0)]
    [InlineData(":is(:scope, #a)", 1, 0, 0)]
    [InlineData(":not(:scope)", 0, 1, 0)]
    public void ScopeHasPseudoClassSpecificity(string selector, int ids, int classes, int types)
        => Assert.Equal(new CssSpecificity(ids, classes, types),
            Assert.Single(CssSelectorList.Parse(selector, cancellationToken: Cancellation).Specificities));

    [Theory]
    [InlineData("> p")]
    [InlineData("+ p")]
    [InlineData(":scope >")]
    [InlineData(":scope,")]
    [InlineData(":not(> p)")]
    public void RelativeOrDanglingScopeSelectorsAreInvalid(string selector)
        => Assert.Throws<FormatException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));

    [Fact]
    public void ScopeConstructionRejectsNonRootNodeKinds()
    {
        var document = new DomDocument();
        Assert.Throws<ArgumentNullException>(() => CssSelectorScope.For(null!));
        Assert.Throws<ArgumentException>(() => CssSelectorScope.For(document.CreateTextNode("x")));
        Assert.Throws<ArgumentException>(() => CssSelectorScope.For(document.CreateComment("x")));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":scope(p)", cancellationToken: Cancellation));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":has(:scope)", cancellationToken: Cancellation));
    }

    [Fact]
    public void ScopeChecksAndVirtualRootCombinatorsShareBudgetAndCancellation()
    {
        var document = Document();
        var box = CssSelectorScope.For(document.GetElementById("box")!);
        var a = document.GetElementById("a")!;
        var exact = CssSelectorList.Parse(":scope > p", new() { MaxMatchOperations = 5 }, Cancellation);
        Assert.NotNull(exact.Match(a, box, Cancellation));
        var limited = CssSelectorList.Parse(":scope > p", new() { MaxMatchOperations = 4 }, Cancellation);
        Assert.Throws<CssLimitException>(() => limited.Match(a, box, Cancellation));
        var fragment = document.CreateDocumentFragment();
        var top = document.CreateElement("p");
        fragment.AppendChild(top);
        var virtualScope = CssSelectorScope.For(fragment);
        Assert.NotNull(CssSelectorList.Parse(":scope > p", new() { MaxMatchOperations = 5 }, Cancellation).Match(top, virtualScope, Cancellation));
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(":scope > p", new() { MaxMatchOperations = 4 }, Cancellation)
            .Match(top, virtualScope, Cancellation));
        var all = All(document);
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(":scope p", new() { MaxElements = 2 }, Cancellation)
            .Filter(all, box, Cancellation).ToList());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => exact.Match(a, box, canceled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => exact.Filter(all, box, canceled.Token).ToList());
    }

    [Fact]
    public void StylesheetScopeRulesUseTheDocumentRootWithPseudoClassSpecificity()
    {
        var document = Document();
        var result = CssStyleEngine.Compute(document,
            [new(":scope > body p{color:blue} p{color:red} :scope{color:green} html{color:red}")], cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), result.Styles[document.GetElementById("a")!]["color"]);
        Assert.Equal(new CssColor(0, 128, 0), result.Styles[document.DocumentElement!]["color"]);
    }

    private static DomElement Element(DomDocument document, string name, string id)
    {
        var element = document.CreateElement(name);
        element.SetAttribute("id", id);
        return element;
    }
}
