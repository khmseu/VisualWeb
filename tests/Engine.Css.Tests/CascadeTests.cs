using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class CascadeTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document(string body = "<div id=parent><p id=child class=x>text</p></div>")
        => HtmlParser.Parse("<!doctype html>" + body, cancellationToken: Cancellation).Document;
    private static CssStyleResult Compute(DomDocument doc, params CssStyleSource[] sources)
        => CssStyleEngine.Compute(doc, sources, cancellationToken: Cancellation);
    private static CssComputedStyle Child(CssStyleResult result, DomDocument doc) => result.Styles[doc.GetElementById("child")!];

    [Theory]
    [InlineData(false, false, false, CssOrigin.Author)]
    [InlineData(false, true, false, CssOrigin.User)]
    [InlineData(false, true, true, CssOrigin.User)]
    [InlineData(true, true, true, CssOrigin.UserAgent)]
    [InlineData(false, false, true, CssOrigin.Author)]
    public void OriginAndImportantPrecedence(bool uaImportant, bool userImportant, bool authorImportant, CssOrigin winner)
    {
        var doc = Document();
        var result = Compute(doc,
            new("p{color:red" + (uaImportant ? "!important" : "") + "}", CssOrigin.UserAgent),
            new("p{color:green" + (userImportant ? "!important" : "") + "}", CssOrigin.User),
            new("p{color:blue" + (authorImportant ? "!important" : "") + "}", CssOrigin.Author));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(winner switch
        {
            CssOrigin.UserAgent => new CssColor(255, 0, 0),
            CssOrigin.User => new CssColor(0, 128, 0),
            _ => new CssColor(0, 0, 255)
        }, Child(result, doc)["color"]);
    }

    [Fact]
    public void SpecificitySourceAndDeclarationOrderAreIndependent()
    {
        var doc = Document();
        var result = Compute(doc, new("#child{color:red} p{color:blue}"),
            new(".x{margin-left:1px;margin-left:2px} .x{margin-top:3px}"),
            new(".x{margin-top:4px}"));
        var style = Child(result, doc);
        Assert.Equal(new CssColor(255, 0, 0), style["color"]);
        Assert.Equal(new CssLength(2, "px"), style["margin-left"]);
        Assert.Equal(new CssLength(4, "px"), style["margin-top"]);
    }

    [Fact]
    public void InlineWinsAuthorSpecificityButNotImportantOrigins()
    {
        var doc = Document("<p id=child style='color:blue;margin-left:2px!important'></p>");
        var result = Compute(doc, new("#child{color:red;margin-left:1px!important}"),
            new("p{color:green!important}", CssOrigin.User));
        Assert.Equal(new CssColor(0, 128, 0), Child(result, doc)["color"]);
        Assert.Equal(new CssLength(2, "px"), Child(result, doc)["margin-left"]);
    }

    [Fact]
    public void InheritanceWideKeywordsAndOriginRevert()
    {
        var doc = Document();
        var result = Compute(doc, new("p{color:blue;margin-left:3px}", CssOrigin.User),
            new("#parent{color:red;padding-left:12px} #child{color:revert; padding-left:inherit; margin-left:initial; display:unset}"));
        var style = Child(result, doc);
        Assert.Equal(new CssColor(0, 0, 255), style["color"]);
        Assert.Equal(new CssLength(12, "px"), style["padding-left"]);
        Assert.Equal(new CssLength(0, "px"), style["margin-left"]);
        Assert.Equal(new CssKeyword("inline"), style["display"]);
    }

    [Fact]
    public void RevertImportantUserRollsBackPastAuthorToUa()
    {
        var doc = Document();
        var result = Compute(doc, new("p{color:red}", CssOrigin.UserAgent),
            new("p{color:revert!important}", CssOrigin.User), new("p{color:blue!important}"));
        Assert.Equal(new CssColor(255, 0, 0), Child(result, doc)["color"]);
    }

    [Fact]
    public void ShorthandExpansionResetsMissingPartsAndKeepsLonghandOrder()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{margin:1px 2px 3px; margin-left:4px; padding:5%; border:2px solid red; border-left:blue}"));
        Assert.Empty(result.Diagnostics);
        var style = Child(result, doc);
        Assert.Equal(new CssLength(1, "px"), style["margin-top"]);
        Assert.Equal(new CssLength(2, "px"), style["margin-right"]);
        Assert.Equal(new CssLength(3, "px"), style["margin-bottom"]);
        Assert.Equal(new CssLength(4, "px"), style["margin-left"]);
        Assert.Equal(new CssLength(5, "%"), style["padding-top"]);
        Assert.Equal(new CssLength(2, "px"), style["border-top-width"]);
        Assert.Equal(new CssKeyword("none"), style["border-left-style"]);
        Assert.Equal(new CssLength(0, "px"), style["border-left-width"]);
    }

    [Fact]
    public void InvalidShorthandDoesNotPartiallyOverrideValidLonghands()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{padding-left:8px;padding:1px -2px;margin:1px 2px 3px 4px 5px}"));
        Assert.Equal(2, result.Diagnostics.Count(d => d.Code == "invalid-property-value"));
        Assert.Equal(new CssLength(8, "px"), Child(result, doc)["padding-left"]);
    }

    [Fact]
    public void RelativeFontsLengthsCurrentColorAndUnitlessLineHeight()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("html{font-size:20px} #parent{font-size:150%;color:red;font-weight:400;line-height:1.5}" +
            "p{font-size:2em;margin-left:2rem;padding-left:1em;color:currentColor;border:1px solid; font-weight:bolder}"));
        Assert.Empty(result.Diagnostics);
        var style = Child(result, doc);
        Assert.Equal(new CssLength(60, "px"), style["font-size"]);
        Assert.Equal(new CssLength(40, "px"), style["margin-left"]);
        Assert.Equal(new CssLength(60, "px"), style["padding-left"]);
        Assert.Equal(new CssNumber(1.5), style["line-height"]);
        Assert.Equal(new CssNumber(700), style["font-weight"]);
        Assert.Equal(new CssColor(255, 0, 0), style["border-top-color"]);
    }

    [Fact]
    public void PercentageLineHeightComputesBeforeInheritance()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#parent{font-size:20px;line-height:150%} #child{font-size:40px}"));
        Assert.Equal(new CssLength(30, "px"), Child(result, doc)["line-height"]);
    }

    [Theory]
    [InlineData("#abc", 170, 187, 204, 255)]
    [InlineData("#abcd", 170, 187, 204, 221)]
    [InlineData("#11223344", 17, 34, 51, 68)]
    [InlineData("rgb(255,0,128)", 255, 0, 128, 255)]
    [InlineData("rgba(100%,0%,50%,.5)", 255, 0, 128, 128)]
    [InlineData("rgb(999,-20,0)", 255, 0, 0, 255)]
    [InlineData("transparent", 0, 0, 0, 0)]
    public void SupportedColorValues(string value, byte r, byte g, byte b, byte a)
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource($"p{{color:{value}}}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(r, g, b, a), Child(result, doc)["color"]);
    }

    [Fact]
    public void FontFamilyRetainsOrderedMultiwordAndQuotedNames()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{font-family:Example Sans, 'Other Font', monospace}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new[] { "Example Sans", "Other Font", "monospace" },
            Assert.IsType<CssFontFamilies>(Child(result, doc)["font-family"]).Names);
    }

    [Fact]
    public void UaDefaultsAreAppliedAndCanBeDisabled()
    {
        var doc = Document();
        var result = Compute(doc);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("block"), Child(result, doc)["display"]);
        Assert.Equal(new CssLength(8, "px"), result.Styles[doc.Body!]["margin-left"]);
        Assert.Equal(new CssKeyword("none"), result.Styles[doc.Head!]["display"]);
        var without = CssStyleEngine.Compute(doc, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssKeyword("inline"), Child(without, doc)["display"]);
    }

    [Fact]
    public void UnsupportedFeaturesProduceDiagnosticsAndNeverExecuteOrFetch()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("@import 'http://example.com/x'; @media screen{p{color:blue}}" +
            "p:hover{color:blue} p{--x:red; color:var(--x);display:grid;width:calc(1px + 2px);animation:test; color:red}"));
        Assert.Equal(2, result.Diagnostics.Count(d => d.Code == "unsupported-at-rule"));
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-selector");
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-property");
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-value");
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-property-value");
        Assert.Equal(new CssColor(255, 0, 0), Child(result, doc)["color"]);
    }

    [Fact]
    public void InvalidValuesDoNotWinCascade()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{color:red; color:no-such-color!important; width:10px; width:1e999px; padding-left:-1px}"));
        Assert.Equal(new CssColor(255, 0, 0), Child(result, doc)["color"]);
        Assert.Equal(new CssLength(10, "px"), Child(result, doc)["width"]);
        Assert.Equal(3, result.Diagnostics.Count(d => d.Code == "invalid-property-value"));
    }

    [Fact]
    public void ComputedSnapshotsDoNotChangeAfterDomMutation()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource(".x{color:red}"));
        doc.GetElementById("child")!.RemoveAttribute("class");
        Assert.Equal(new CssColor(255, 0, 0), Child(result, doc)["color"]);
        Assert.Equal(new CssColor(0, 0, 0), Child(Compute(doc, new CssStyleSource(".x{color:red}")), doc)["color"]);
    }

    [Fact]
    public void AllShorthandAndElementBudget()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{color:red;all:initial; margin:inherit}"));
        Assert.Equal(new CssColor(0, 0, 0), Child(result, doc)["color"]);
        var count = doc.Descendants().OfType<DomElement>().Count();
        Assert.Equal(count, CssStyleEngine.Compute(doc, options: new() { MaxElements = count }, cancellationToken: Cancellation).Styles.Count);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, options: new() { MaxElements = count - 1 }, cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compute(doc, new CssStyleSource("", (CssOrigin)42)));
    }

    [Fact]
    public void FunctionalColorsWorkInsideBorderShorthands()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("p{border:1px solid rgb(255,0,0); border-color:rgb(0,0,255) #abc}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Child(result, doc)["border-top-color"]);
        Assert.Equal(new CssColor(170, 187, 204), Child(result, doc)["border-left-color"]);
    }

    [Fact]
    public void AggregateSourcesRulesAssignmentsAndMatchDepthAreBounded()
    {
        var doc = Document();
        Assert.Single(CssSyntax.ParseStyleSheet("p{}", new() { MaxRules = 1 }, Cancellation).Rules);
        Assert.Throws<CssLimitException>(() => CssSyntax.ParseStyleSheet("p{}div{}", new() { MaxRules = 1 }, Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, [new(""), new("")],
            includeUserAgent: false, options: new() { MaxStyleSources = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, [new("p{}"), new("div{}")],
            includeUserAgent: false, options: new() { MaxRules = 1 }, cancellationToken: Cancellation));
        Assert.Empty(CssStyleEngine.Compute(doc, [new("p{margin:1px}")], includeUserAgent: false,
            options: new() { MaxAssignments = 4 }, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, [new("p{margin:1px}")], includeUserAgent: false,
            options: new() { MaxAssignments = 3 }, cancellationToken: Cancellation));
        var selector = CssSelectorList.Parse(":is(body p)", new() { MaxDepth = 2 }, Cancellation);
        Assert.Throws<CssLimitException>(() => selector.Match(doc.GetElementById("child")!, Cancellation));
    }

    [Theory]
    [InlineData(1, 400, 1)]
    [InlineData(100, 400, 100)]
    [InlineData(349, 400, 100)]
    [InlineData(350, 700, 100)]
    [InlineData(549, 700, 100)]
    [InlineData(550, 900, 400)]
    [InlineData(749, 900, 400)]
    [InlineData(750, 900, 700)]
    [InlineData(899, 900, 700)]
    [InlineData(900, 900, 700)]
    [InlineData(1000, 1000, 700)]
    public void RelativeFontWeightUsesExactStandardThresholds(int parent, int bolder, int lighter)
    {
        var doc = Document("<div id=parent><p id=child></p><span id=lighter></span></div>");
        var result = Compute(doc, new CssStyleSource($"#parent{{font-weight:{parent}}} #child{{font-weight:bolder}} #lighter{{font-weight:lighter}}"));
        Assert.Equal(new CssNumber(bolder), Child(result, doc)["font-weight"]);
        Assert.Equal(new CssNumber(lighter), result.Styles[doc.GetElementById("lighter")!]["font-weight"]);
    }

    [Fact]
    public void RootRelativeFontSizeUsesInitialSizeAndOverflowFailsExplicitly()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("html{font-size:2rem} p{margin-left:1rem}"));
        Assert.Equal(new CssLength(32, "px"), Child(result, doc)["margin-left"]);
        Assert.Throws<CssLimitException>(() => Compute(doc, new CssStyleSource("html{font-size:1e308px} p{padding-left:100em}")));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(doc, cancellationToken: source.Token));
    }
}
