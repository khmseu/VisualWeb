using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HiddenDefaultsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null, "block")]
    [InlineData("", "none")]
    [InlineData("hidden", "none")]
    [InlineData("HIDDEN", "none")]
    [InlineData("invalid", "none")]
    [InlineData(" until-found", "none")]
    [InlineData("until-found ", "none")]
    [InlineData("until-found\t", "none")]
    [InlineData("unt\u0131l-found", "none")]
    [InlineData("unt\u0130l-found", "none")]
    [InlineData("until-found", "block")]
    [InlineData("UnTiL-FoUnD", "block")]
    public void OrdinaryHiddenAndUntilFoundUseExactAsciiKeywords(string? value, string expected)
    {
        var document = Parse("<div id=target></div>");
        var target = document.GetElementById("target")!;
        if (value is not null) { target.SetAttribute("HiDdEn", value); }

        Assert.Equal(expected, Display(Compute(document), target));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hidden")]
    [InlineData("until-found")]
    [InlineData("UnTiL-FoUnD")]
    public void EmbedIsExcludedFromTheOrdinaryHiddenRule(string value)
    {
        var document = Parse("<embed id=target>");
        var target = document.GetElementById("target")!;
        target.SetAttribute("hidden", value);
        Assert.Equal("inline", Display(Compute(document), target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenUsesNormalCascadeAndCanBeOverridden(bool includeUserAgent)
    {
        var document = Parse("<div id=target hidden></div>");
        var target = document.GetElementById("target")!;
        var fallback = includeUserAgent ? "none" : "inline";
        CssStyleSource user = new("div{display:block}", CssOrigin.User);
        CssStyleSource author = new("div{display:inline}");
        CssStyleSource authorImportant = new("div{display:block!important}");
        CssStyleSource userImportant = new("div{display:inline-block!important}", CssOrigin.User);

        AssertDisplay(fallback);
        AssertDisplay("block", user);
        AssertDisplay("inline", user, author);
        AssertDisplay("inline-block", userImportant, authorImportant);
        AssertDisplay(fallback, new CssStyleSource("div{display:revert}"));
        AssertDisplay("block", user, new CssStyleSource("div{display:revert}"));
        AssertDisplay(fallback, new CssStyleSource("div{display:revert!important}", CssOrigin.User), authorImportant);

        target.SetAttribute("style", "display:block");
        AssertDisplay("block", user, author);
        AssertDisplay("block", user, authorImportant);
        AssertDisplay("inline-block", userImportant, authorImportant);
        target.SetAttribute("style", "display:inline!important");
        AssertDisplay("inline", user, authorImportant);
        AssertDisplay("inline-block", userImportant, authorImportant);
        target.SetAttribute("style", "display:revert");
        AssertDisplay("block", user);
        AssertDisplay(fallback);

        void AssertDisplay(string expected, params CssStyleSource[] sources) =>
            Assert.Equal(expected, Display(Compute(document, includeUserAgent, sources), target));
    }

    [Fact]
    public void HiddenInputTypeKeepsItsSeparateImportantUaRule()
    {
        var document = Parse("<input id=target type=HiDdEn hidden>");
        var target = document.GetElementById("target")!;
        target.SetAttribute("style", "display:block!important");
        Assert.Equal("none", Display(Compute(document, true, new CssStyleSource("input{display:inline!important}")), target));
        Assert.Equal("block", Display(Compute(document, false), target));
    }

    [Fact]
    public void AttributeMutationRecomputesSnapshotsWithoutInheritingDisplay()
    {
        var document = Parse("<div id=target><span id=child>text</span></div>");
        var target = document.GetElementById("target")!;
        var child = document.GetElementById("child")!;
        var initial = Compute(document);
        target.SetAttribute("HiDdEn", "");
        var hidden = Compute(document);
        target.SetAttribute("hidden", "until-found");
        var untilFound = Compute(document);
        target.SetAttribute("hidden", " until-found ");
        var invalid = Compute(document);
        target.RemoveAttribute("HIDDEN");
        var removed = Compute(document);

        Assert.Equal("block", Display(initial, target));
        Assert.Equal("none", Display(hidden, target));
        Assert.Equal("block", Display(untilFound, target));
        Assert.Equal("none", Display(invalid, target));
        Assert.Equal("block", Display(removed, target));
        Assert.Equal("inline", Display(hidden, child));
        Assert.Equal("none", Display(hidden, target));
    }

    [Fact]
    public void MatchedHiddenRuleSharesExactCssBudgetsAndCancellation()
    {
        var document = Parse("<a id=target hidden href>text</a>");
        // Four elements: the hidden selector costs 2 operations on each nonmatch and
        // 8 on the hidden anchor, including both :not() argument matches.
        const int assignments = 65;
        const long matches = 854; // previous 840 + 3 * 2 + 8
        CssStyleSource[] author = [new("a{display:block}")];
        const long authorMatches = 862;

        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments - 1 }));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = matches - 1 }));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments }, author));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = authorMatches - 1 }, author));
        var exact = Calculate(new() { MaxAssignments = assignments, MaxMatchOperations = matches });
        Assert.Empty(exact.Diagnostics);
        Assert.Equal("none", Display(exact, document.GetElementById("target")!));
        var withAuthor = Calculate(new() { MaxAssignments = assignments + 1, MaxMatchOperations = authorMatches }, author);
        Assert.Empty(withAuthor.Diagnostics);
        Assert.Equal("block", Display(withAuthor, document.GetElementById("target")!));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(document, cancellationToken: canceled.Token));

        CssStyleResult Calculate(CssOptions options, CssStyleSource[]? sources = null) =>
            CssStyleEngine.Compute(document, sources, options: options, cancellationToken: Cancellation);
    }

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;

    private static CssStyleResult Compute(DomDocument document, bool includeUserAgent = true, params CssStyleSource[] sources)
    {
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: includeUserAgent,
            cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        return result;
    }

    private static string Display(CssStyleResult result, DomElement element) =>
        Assert.IsType<CssKeyword>(result.Styles[element]["display"]).Value;
}
