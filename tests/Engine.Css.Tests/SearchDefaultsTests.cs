using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class SearchDefaultsTests
{
    [Fact]
    public void MatchingSearchSharesExactSelectorAndAssignmentBudgetsAndCancellation()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><search id=x>text</search>", cancellationToken: cancellation).Document;
        var search = document.GetElementById("x")!;
        // html, head, body and search each evaluate all 118 top-level selectors,
        // plus 11 :is() subject alternatives; each simple selector takes two steps.
        // The matching search type selector and the unmatched mark selector are included in those checks.
        const long matches = 4 * (118 + 11) * 2;
        // UA mark, link-decoration, phrasing-decoration, dir, caption and column-display assignments are
        // compiled with the other matching UA declarations. Four-sided shorthands bring the total to 76.
        const int assignments = 76;
        // One author selector is checked on all four elements and adds one assignment.
        const long authorMatches = matches + 4 * 2;
        CssStyleSource[] author = [new("search{display:inline}")];

        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments - 1 }));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = matches - 1 }));
        var exact = Calculate(new() { MaxAssignments = assignments, MaxMatchOperations = matches });
        Assert.Empty(exact.Diagnostics);
        Assert.Equal("block", Assert.IsType<CssKeyword>(exact.Styles[search]["display"]).Value);

        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments }, author));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = authorMatches - 1 }, author));
        var overridden = Calculate(new() { MaxAssignments = assignments + 1, MaxMatchOperations = authorMatches }, author);
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal("inline", Assert.IsType<CssKeyword>(overridden.Styles[search]["display"]).Value);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(document, cancellationToken: canceled.Token));

        CssStyleResult Calculate(CssOptions options, CssStyleSource[]? sources = null) =>
            CssStyleEngine.Compute(document, sources, options: options, cancellationToken: cancellation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchDisplayUsesNormalCascadeAndHidden(bool includeUserAgent)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><search id=x>text</search>", cancellationToken: cancellation).Document;
        var element = document.GetElementById("x")!;
        CssStyleSource user = new("search{display:inline}", CssOrigin.User);
        CssStyleSource author = new("search{display:block}");
        CssStyleSource important = new("search{display:inline!important}", CssOrigin.User);
        CssStyleSource authorImportant = new("search{display:block!important}");
        var fallback = includeUserAgent ? "block" : "inline";
        Check(fallback);
        Check("inline", user);
        Check("block", user, author);
        Check("inline", important, authorImportant);
        Check(fallback, new CssStyleSource("search{display:revert}"));
        Check("inline", user, new CssStyleSource("search{display:revert}"));
        element.SetAttribute("style", "display:inline");
        Check("inline", author);
        Check("block", authorImportant);
        element.SetAttribute("style", "display:revert");
        Check("inline", user);
        element.RemoveAttribute("style");
        element.SetAttribute("hidden", "");
        Check(includeUserAgent ? "none" : "inline");
        Check("block", author);

        void Check(string expected, params CssStyleSource[] sources)
        {
            var result = CssStyleEngine.Compute(document, sources, includeUserAgent, cancellationToken: cancellation);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(expected, Assert.IsType<CssKeyword>(result.Styles[element]["display"]).Value);
        }
    }
}
