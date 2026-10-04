using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class SelectorTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document() => HtmlParser.Parse(
        "<!doctype html><div id=box><p id=a class='item X' data-word='Abc def' lang=en-US>A</p><!--gap--><p id=b class=item>B</p><span id=c></span></div>",
        cancellationToken: Cancellation).Document;

    [Theory]
    [InlineData("p", "a,b")]
    [InlineData("#a", "a")]
    [InlineData(".item", "a,b")]
    [InlineData("div > p + p", "b")]
    [InlineData("p ~ span", "c")]
    [InlineData("body p", "a,b")]
    [InlineData("[data-word]", "a")]
    [InlineData("[data-word~='def']", "a")]
    [InlineData("[data-word^='ab' i]", "a")]
    [InlineData("[data-word^='ab'i]", "a")]
    [InlineData("[data-word^='ab' s]", "")]
    [InlineData("[lang|=en]", "a")]
    [InlineData("[data-word$='def']", "a")]
    [InlineData("[data-word*='bc']", "a")]
    [InlineData("[data-word^='']", "")]
    [InlineData("p:first-child", "a")]
    [InlineData("p:last-child", "")]
    [InlineData("p:nth-child(2)", "b")]
    [InlineData("p:nth-last-child(2)", "b")]
    [InlineData("p:nth-of-type(2)", "b")]
    [InlineData("p:last-of-type", "b")]
    [InlineData("span:only-of-type", "c")]
    [InlineData("span:empty", "c")]
    [InlineData(":is(#a,#b)", "a,b")]
    [InlineData("p:not(#a)", "b")]
    [InlineData(":where(#a)", "a")]
    [InlineData(":is(???,#a)", "a")]
    [InlineData(":is()", "")]
    [InlineData(".x", "")]
    public void StaticSelectorsMatchExpectedElements(string selector, string expected)
    {
        var parsed = CssSelectorList.Parse(selector, cancellationToken: Cancellation);
        var actual = Document().Descendants().OfType<DomElement>().Where(e => parsed.Match(e, Cancellation) is not null)
            .Select(e => e.GetAttribute("id")).Where(id => id is not null);
        Assert.Equal(expected, string.Join(",", actual));
    }

    [Theory]
    [InlineData("#a", 1, 0, 0)]
    [InlineData("div.item > p[data-word]:first-child", 0, 3, 2)]
    [InlineData(":is(p,#a)", 1, 0, 0)]
    [InlineData(":not(.item,#a)", 1, 0, 0)]
    [InlineData("div:where(#a)", 0, 0, 1)]
    [InlineData("p:nth-child(odd)", 0, 1, 1)]
    public void SpecificityIsLexicographicAndFunctional(string selector, int ids, int classes, int types)
    {
        Assert.Equal(new CssSpecificity(ids, classes, types),
            Assert.Single(CssSelectorList.Parse(selector, cancellationToken: Cancellation).Specificities));
        Assert.True(new CssSpecificity(1, 0, 0).CompareTo(new(0, 100, 100)) > 0);
    }

    [Fact]
    public void MatchingListUsesSpecificityOfMatchingBranchesOnly()
    {
        var p = Document().GetElementById("a")!;
        Assert.Equal(new CssSpecificity(0, 0, 1), CssSelectorList.Parse("#no,p", cancellationToken: Cancellation).Match(p, Cancellation));
        Assert.Equal(new CssSpecificity(1, 0, 0), CssSelectorList.Parse(":is(#no,p)", cancellationToken: Cancellation).Match(p, Cancellation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("p,")]
    [InlineData("p >")]
    [InlineData(".")]
    [InlineData("#123")]
    [InlineData("[x=1]")]
    [InlineData("p:not(???,p)")]
    [InlineData("p:nth-child(1.0)")]
    [InlineData("p:nth-child(2/**/n)")]
    [InlineData("p:nth-child(+/**/2n)")]
    public void InvalidSelectorListsThrow(string selector)
    {
        Assert.Throws<FormatException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));
    }

    [Theory]
    [InlineData("p:hover")]
    [InlineData("p::before")]
    [InlineData("p:has(span)")]
    [InlineData("p:nth-child(2 of .item)")]
    [InlineData("svg|a")]
    [InlineData("[*|x]")]
    [InlineData("[ns|x]")]
    [InlineData("|p")]
    public void UnsupportedSelectorsAreNotApproximateMatches(string selector)
    {
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(selector, cancellationToken: Cancellation));
    }

    [Fact]
    public void HtmlComparisonUsesAsciiOnlyAndQuirksIsExplicit()
    {
        var doc = Document();
        var p = doc.GetElementById("a")!;
        p.SetAttribute("type", "TEXT");
        Assert.NotNull(CssSelectorList.Parse("[type=text]", cancellationToken: Cancellation).Match(p, Cancellation));
        Assert.Null(CssSelectorList.Parse(".x", cancellationToken: Cancellation).Match(p, Cancellation));
        doc.Mode = DomDocumentMode.Quirks;
        Assert.NotNull(CssSelectorList.Parse(".x", cancellationToken: Cancellation).Match(p, Cancellation));
        p.SetAttribute("data-word", "\u00C4");
        Assert.Null(CssSelectorList.Parse("[data-word='\u00E4' i]", cancellationToken: Cancellation).Match(p, Cancellation));
    }

    [Fact]
    public void EmptyUsesCurrentSelectorsWhitespaceRule()
    {
        var doc = new DomDocument();
        var element = doc.CreateElement("div");
        element.AppendChild(doc.CreateTextNode(" \n\t"));
        element.AppendChild(doc.CreateComment("x"));
        var selector = CssSelectorList.Parse(":empty", cancellationToken: Cancellation);
        Assert.NotNull(selector.Match(element, Cancellation));
        element.AppendChild(doc.CreateTextNode("x"));
        Assert.Null(selector.Match(element, Cancellation));
    }

    [Fact]
    public void SelectorLimitsAndCancellationAreEnforced()
    {
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse("p > p", new() { MaxDepth = 1 }, Cancellation));
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse(":is(:is(p))", new() { MaxDepth = 2 }, Cancellation));
        var selector = CssSelectorList.Parse("div p", new() { MaxMatchOperations = 1 }, Cancellation);
        Assert.Throws<CssLimitException>(() => selector.Match(Document().GetElementById("a")!, Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssSelectorList.Parse("p", cancellationToken: canceled.Token));
        var element = new DomDocument().CreateElement("p");
        Assert.NotNull(CssSelectorList.Parse("p", new() { MaxMatchOperations = 2, MaxDepth = 1 }, Cancellation).Match(element, Cancellation));
        Assert.Throws<CssLimitException>(() => CssSelectorList.Parse("p", new() { MaxMatchOperations = 1 }, Cancellation).Match(element, Cancellation));
    }

    [Fact]
    public void EscapedEofHashCanMatchDespiteRecoverableTokenizerDiagnostic()
    {
        var element = new DomDocument().CreateElement("p");
        element.SetAttribute("id", "foo\uFFFD");
        var selector = CssSelectorList.Parse("#foo\\", cancellationToken: Cancellation);
        Assert.Single(selector.Diagnostics, d => d.Code == "eof-in-escape");
        Assert.NotNull(selector.Match(element, Cancellation));
    }

    [Fact]
    public void FixtureFamilyHasExpectedUnfilteredCaseCount()
    {
        Assert.Equal(67, OfficialAnB().Count());
        Assert.Equal("E11B74D37E03DEDA5595AC3A26ECFCF10C4EFEB08ACFED9371CA56FFFE5695DB",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "anb-parsing.html")))));
    }

    [Fact]
    public void AnBIntegerBoundsAreExplicitRatherThanOverflowing()
    {
        var element = new DomDocument().CreateElement("p");
        Assert.NotNull(CssSelectorList.Parse(":nth-child(-2147483648n+1)", cancellationToken: Cancellation).Match(element, Cancellation));
        Assert.Throws<UnsupportedCssException>(() => CssSelectorList.Parse(":nth-child(2147483648)", cancellationToken: Cancellation));
    }

    public static IEnumerable<object[]> OfficialAnB()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "anb-parsing.html"));
        foreach (Match match in Regex.Matches(source, """testANB\(("(?:[^"\\]|\\.)*"), ("(?:[^"\\]|\\.)*")\);"""))
        {
            yield return [JsonSerializer.Deserialize<string>(match.Groups[1].Value)!,
                JsonSerializer.Deserialize<string>(match.Groups[2].Value)!];
        }
    }

    [Theory]
    [MemberData(nameof(OfficialAnB))]
    public void AllPinnedWptAnBParsingCases(string input, string expected)
    {
        if (expected == "parse error")
        {
            Assert.Throws<FormatException>(() => CssSelectorList.Parse($":nth-child({input})", cancellationToken: Cancellation));
            return;
        }
        var selector = CssSelectorList.Parse($":nth-child({input})", cancellationToken: Cancellation);
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        doc.AppendChild(root);
        for (var index = 0; index < 30; index++) { root.AppendChild(doc.CreateElement("p")); }
        var canonical = Regex.Match(expected, @"^(-?\d*)n([+-]\d+)?$");
        var a = canonical.Success ? canonical.Groups[1].Value switch
        {
            "" => 1,
            "-" => -1,
            var number => int.Parse(number, System.Globalization.CultureInfo.InvariantCulture)
        } : 0;
        var b = canonical.Success
            ? canonical.Groups[2].Success ? int.Parse(canonical.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 0
            : int.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        for (var index = 1; index <= root.ChildNodes.Count; index++)
        {
            var shouldMatch = a == 0 ? index == b : (index - b) % a == 0 && (index - b) / a >= 0;
            Assert.Equal(shouldMatch, selector.Match(Assert.IsType<DomElement>(root.ChildNodes[index - 1]), Cancellation) is not null);
        }
    }
}
