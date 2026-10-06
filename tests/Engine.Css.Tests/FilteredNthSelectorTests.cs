using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class FilteredNthSelectorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document() => HtmlParser.Parse("""
        <!doctype html><div id=box>
        <p id=a class=pick></p>text<!--gap--><span id=b></span>
        <span id=c class=pick></span><p id=d class=pick></p><p id=e></p><p id=f class=pick></p>
        </div>
        """, cancellationToken: Cancellation).Document;

    private static string Matches(string selector, DomDocument document, CssSelectorScope? scope = null)
        => string.Join(",", CssSelectorList.Parse(selector, cancellationToken: Cancellation)
            .Filter(document.Descendants().OfType<DomElement>(), scope, Cancellation)
            .Select(e => e.GetAttribute("id")).Where(id => id is not null));

    [Theory]
    [InlineData(":nth-child(odd of .pick)", "a,d")]
    [InlineData(":nth-child(even of .pick)", "c,f")]
    [InlineData(":nth-child(-n+2 of .pick)", "a,c")]
    [InlineData(":nth-child(0n+2 of .pick)", "c")]
    [InlineData(":nth-child(2n-1 of .pick)", "a,d")]
    [InlineData(":nth-child(-2147483648n+1 of .pick)", "a")]
    [InlineData(":nth-child(2147483647n+2 of .pick)", "c")]
    [InlineData(":nth-child(0 of .pick)", "")]
    [InlineData(":nth-child(-2 of .pick)", "")]
    [InlineData(":nth-last-child(1 of .pick)", "f")]
    [InlineData(":nth-last-child(2 of .pick)", "d")]
    [InlineData(":nth-last-child(-n+2 of .pick)", "d,f")]
    [InlineData(":nth-last-child(2n of .pick)", "a,d")]
    [InlineData(":nth-last-child(5 of .pick)", "")]
    [InlineData(":nth-last-child(1 of .absent)", "")]
    [InlineData(":nth-last-child(n of .pick)", "a,c,d,f")]
    [InlineData(".pick:nth-child(2)", "")]
    [InlineData(":nth-child(2 of #box > p)", "d")]
    [InlineData(":nth-child(2 of #a ~ .pick)", "d")]
    [InlineData(":nth-child(2 of #a + span, #box > p.pick)", "b")]
    [InlineData(":nth-child(2 of .pick, #c, .pick)", "c")]
    [InlineData(":nth-child(1 of :nth-last-child(2 of .pick))", "d")]
    [InlineData(":nth-child(2 of :is(.pick, ???))", "c")]
    [InlineData(":nth-child(2 of :not(#a, #b, #e))", "d")]
    public void FilteredInclusiveElementSiblingsHaveOneBasedIndices(string selector, string expected)
        => Assert.Equal(expected, Matches(selector, Document()));

    [Theory]
    [InlineData(":nth-child(2n OF .pick)", "c,f")]
    [InlineData(":nth-child(2/**/of/**/.pick)", "c")]
    [InlineData(":nth-child(2 of.pick)", "c")]
    [InlineData(":nth-child(+n- 2 of .pick)", "a,c,d,f")]
    [InlineData(":nth-child(2n + 0 of .pick)", "c,f")]
    [InlineData(":nth-child(2 \\6f f .pick)", "c")]
    [InlineData(":NTH-LAST-CHILD(2 OF.pick)", "d")]
    [InlineData(":nth-child(2 of [class=pick], [data-word='of'])", "c")]
    public void OfKeywordUsesTokensAndExistingAnBGrammar(string selector, string expected)
        => Assert.Equal(expected, Matches(selector, Document()));

    [Theory]
    [InlineData(":nth-child(of .pick)")]
    [InlineData(":nth-child(2of .pick)")]
    [InlineData(":nth-child(n of)")]
    [InlineData(":nth-child(n of .pick,)")]
    [InlineData(":nth-child(n of , .pick)")]
    [InlineData(":nth-child(n of .pick, ???)")]
    [InlineData(":nth-child(n of > .pick)")]
    [InlineData(":nth-child(n of .pick >)")]
    [InlineData(":nth-child(2 n of .pick)")]
    [InlineData(":nth-child(1.0 of .pick)")]
    [InlineData(":nth-child(2n+-1 of .pick)")]
    [InlineData(":nth-last-child(2 of .pick, :not(???))")]
    public void OfListsAreStrictAndAnBMustBeValid(string selector)
        => Assert.Throws<FormatException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));

    [Theory]
    [InlineData(":nth-child(1 of :has(p))")]
    [InlineData(":nth-last-child(1 of :hover)")]
    [InlineData(":nth-child(1 of p::before)")]
    [InlineData(":nth-child(1 of :is(p, :hover))")]
    [InlineData(":nth-of-type(1 of p)")]
    [InlineData(":nth-last-of-type(1 of p)")]
    public void UnsupportedArgumentsRemainExplicit(string selector)
        => Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));

    [Theory]
    [InlineData(":nth-child(2 of .pick, #missing)", 1, 1, 0)]
    [InlineData("p:nth-last-child(1 of div > .pick)", 0, 2, 2)]
    [InlineData(":nth-child(2 of :where(#missing, .pick))", 0, 1, 0)]
    [InlineData(":nth-child(2 of :nth-child(n of #missing, .pick))", 1, 2, 0)]
    public void SpecificityAddsMaximumArgumentEvenWhenThatBranchDoesNotMatch(string selector, int ids, int classes, int types)
    {
        var parsed = CssSelectorList.Parse(selector, cancellationToken: Cancellation);
        Assert.Equal(new CssSpecificity(ids, classes, types), Assert.Single(parsed.Specificities));
        var match = parsed.Filter(Document().Descendants().OfType<DomElement>(), Cancellation).First();
        Assert.Equal(new CssSpecificity(ids, classes, types), parsed.Match(match, Cancellation));
    }

    [Fact]
    public void CascadeUsesArgumentSpecificityAndReportsInvalidOrUnsupportedLists()
    {
        var doc = Document();
        var result = CssStyleEngine.Compute(doc, [new("""
            :nth-child(2 of .pick,#missing){color:blue}
            #c{color:red}
            :nth-child(2 of .pick,???){color:green}
            :nth-child(2 of :hover){color:green}
            """, CssOrigin.Author)], cancellationToken: Cancellation);
        Assert.Equal(new CssColor(0, 0, 255), result.Styles[doc.GetElementById("c")!]["color"]);
        Assert.Equal(2, result.Diagnostics.Count);
    }

    [Fact]
    public void QuirksAppliesInsideFilterAndTypeNamedOfIsNotAnotherClause()
    {
        var doc = Document();
        doc.GetElementById("a")!.SetAttribute("class", "PICK");
        Assert.Equal("d", Matches(":nth-child(2 of .pick)", doc));
        doc.Mode = DomDocumentMode.Quirks;
        Assert.Equal("c", Matches(":nth-child(2 of .pick)", doc));
        var of = doc.CreateElement("of");
        doc.GetElementById("box")!.AppendChild(of);
        Assert.NotNull(CssSelectorList.Parse(":nth-child(1 of of)", cancellationToken: Cancellation).Match(of, Cancellation));
    }

    [Fact]
    public void DetachedSingletonMustBelongToFilterBeforeForwardOrReverseArithmetic()
    {
        var doc = new DomDocument();
        var element = doc.CreateElement("p");
        foreach (var name in new[] { "nth-child", "nth-last-child" })
        {
            Assert.NotNull(CssSelectorList.Parse($":{name}(1 of p)", cancellationToken: Cancellation).Match(element, Cancellation));
            Assert.Null(CssSelectorList.Parse($":{name}(1 of span)", cancellationToken: Cancellation).Match(element, Cancellation));
            Assert.Null(CssSelectorList.Parse($":{name}(2 of p)", cancellationToken: Cancellation).Match(element, Cancellation));
        }
    }

    [Fact]
    public void FiltersKeepOriginalScopeAcrossSiblingsCallsAndNestedSelectors()
    {
        var doc = Document();
        var box = doc.GetElementById("box")!;
        var c = doc.GetElementById("c")!;
        Assert.Equal("c", Matches(":nth-child(2 of :scope > .pick)", doc, CssSelectorScope.For(box)));
        Assert.Equal("", Matches(":nth-child(2 of :scope > .pick)", doc, CssSelectorScope.For(c)));
        var parsed = CssSelectorList.Parse(":nth-child(1 of :scope)", cancellationToken: Cancellation);
        Assert.NotNull(parsed.Match(c, CssSelectorScope.For(c), Cancellation));
        Assert.Null(parsed.Match(c, CssSelectorScope.For(box), Cancellation));
        Assert.NotNull(parsed.Match(c, CssSelectorScope.For(c), Cancellation));
        Assert.Equal("box", Matches(":nth-child(1 of :scope > body > *)", doc, CssSelectorScope.For(doc)));
        var fragment = doc.CreateDocumentFragment();
        var first = doc.CreateElement("p"); var second = doc.CreateElement("p");
        fragment.AppendChild(first); fragment.AppendChild(doc.CreateComment("gap")); fragment.AppendChild(second);
        var nested = CssSelectorList.Parse(":nth-last-child(1 of :is(:scope > p))", cancellationToken: Cancellation);
        Assert.Null(nested.Match(first, CssSelectorScope.For(fragment), Cancellation));
        Assert.NotNull(nested.Match(second, CssSelectorScope.For(fragment), Cancellation));
    }

    [Fact]
    public void NestedFilteringSharesExactOperationCandidateAndDepthBudgets()
    {
        var doc = new DomDocument();
        var elements = new[] { doc.CreateElement("p"), doc.CreateElement("p") };
        var exact = CssSelectorList.Parse(":nth-child(1 of *)", new() { MaxMatchOperations = 8 }, Cancellation);
        Assert.Equal(elements, exact.Filter(elements, Cancellation));
        var limited = CssSelectorList.Parse(":nth-child(1 of *)", new() { MaxMatchOperations = 7 }, Cancellation);
        Assert.Throws<CssLimitException>(() => limited.Filter(elements, Cancellation).ToList());
        var candidates = CssSelectorList.Parse(":nth-child(1 of *)", new() { MaxElements = 1 }, Cancellation);
        Assert.Throws<CssLimitException>(() => candidates.Filter(elements, Cancellation).ToList());
        var parent = doc.CreateElement("div"); parent.AppendChild(elements[0]);
        var depth = CssSelectorList.Parse(":nth-child(1 of div > p)", new() { MaxDepth = 2 }, Cancellation);
        Assert.Throws<CssLimitException>(() => depth.Match(elements[0], Cancellation));
        Assert.NotNull(CssSelectorList.Parse(":nth-child(1 of div > p)", new() { MaxDepth = 3 }, Cancellation)
            .Match(elements[0], Cancellation));
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(":nth-child(1 of :nth-child(1 of p))",
            new() { MaxDepth = 2 }, Cancellation));
    }

    [Fact]
    public void ParsingAndMatchingHonorCancellationIncludingLazySharedFiltering()
    {
        var doc = new DomDocument();
        var element = doc.CreateElement("p");
        var parsed = CssSelectorList.Parse(":nth-last-child(1 of p)", cancellationToken: Cancellation);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => CssSelectorList.Parse(":nth-child(1 of p)", cancellationToken: canceled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => parsed.Match(element, canceled.Token));
        using var midway = new CancellationTokenSource();
        IEnumerable<DomElement> Candidates()
        {
            yield return element;
            midway.Cancel();
            yield return element;
        }
        Assert.ThrowsAny<OperationCanceledException>(() => parsed.Filter(Candidates(), midway.Token).ToList());
    }

    [Fact]
    public void FilterArgumentsRemainInsideInputTokenAndIntegerBounds()
    {
        const string selector = ":nth-child(1 of p)";
        Assert.Single(CssSelectorList.Parse(selector, new() { MaxInputCharacters = selector.Length }, Cancellation).Specificities);
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(selector,
            new() { MaxInputCharacters = selector.Length - 1 }, Cancellation));
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(selector, new() { MaxTokens = 3 }, Cancellation));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":nth-child(2147483648 of p)", cancellationToken: Cancellation));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":nth-last-child(-2147483649n of p)", cancellationToken: Cancellation));
    }
}
