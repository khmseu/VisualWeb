using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class LinkColorDefaultsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly CssColor LinkBlue = new(0, 0, 238);
    private static readonly CssColor ParentColor = new(18, 52, 86);

    [Theory]
    [InlineData("a", null, false)]
    [InlineData("a", "", true)]
    [InlineData("A", "#previously-visited", true)]
    [InlineData("a", "javascript:alert(1)", true)]
    [InlineData("a", "http://[invalid", true)]
    [InlineData("area", null, false)]
    [InlineData("area", "", true)]
    [InlineData("AREA", "https://example.test/visited", true)]
    [InlineData("link", "https://example.test/", false)]
    [InlineData("div", "", false)]
    public void DefaultColorDependsOnHtmlHrefPresenceNotItsValueOrHistory(string name, string? href, bool hyperlink)
    {
        var document = Parse("<div id=parent style='color:#123456'></div>");
        var target = document.CreateElement(name);
        document.GetElementById("parent")!.AppendChild(target);
        if (href is not null) { target.SetAttribute("HrEf", href); }

        var enabled = Compute(document, true);
        var disabled = Compute(document, false);
        Assert.Equal(hyperlink ? LinkBlue : ParentColor, enabled.Styles[target]["color"]);
        Assert.Equal(ParentColor, disabled.Styles[target]["color"]);
    }

    [Fact]
    public void HrefMutationRequiresRecomputationAndDescendantsInheritTheNewSnapshot()
    {
        var document = Parse("<div style='color:#123456'><a id=target><span id=child>text</span></a><area id=area></div>");
        var target = document.GetElementById("target")!;
        var child = document.GetElementById("child")!;
        var area = document.GetElementById("area")!;
        var before = Compute(document, true);
        Assert.Equal(ParentColor, before.Styles[target]["color"]);
        Assert.Equal(ParentColor, before.Styles[child]["color"]);
        Assert.Equal(ParentColor, before.Styles[area]["color"]);

        target.SetAttribute("HREF", "");
        area.SetAttribute("href", "javascript:blocked()");
        var linked = Compute(document, true);
        Assert.Equal(LinkBlue, linked.Styles[target]["color"]);
        Assert.Equal(LinkBlue, linked.Styles[child]["color"]);
        Assert.Equal(LinkBlue, linked.Styles[area]["color"]);
        Assert.Equal(ParentColor, before.Styles[target]["color"]);
        Assert.Equal(ParentColor, before.Styles[child]["color"]);

        target.SetAttribute("href", "http://[invalid");
        area.RemoveAttribute("HREF");
        var changed = Compute(document, true);
        Assert.Equal(LinkBlue, changed.Styles[target]["color"]);
        Assert.Equal(LinkBlue, changed.Styles[child]["color"]);
        Assert.Equal(ParentColor, changed.Styles[area]["color"]);
        target.RemoveAttribute("href");
        var unlinked = Compute(document, true);
        Assert.Equal(ParentColor, unlinked.Styles[target]["color"]);
        Assert.Equal(ParentColor, unlinked.Styles[child]["color"]);
        Assert.Equal(LinkBlue, linked.Styles[target]["color"]);
        Assert.Equal(LinkBlue, linked.Styles[child]["color"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultColorUsesNormalOriginSpecificityInlineImportanceAndRevert(bool includeUserAgent)
    {
        var document = Parse("<div style='color:#123456'><a id=target class=preferred href><span id=child>text</span></a></div>");
        var target = document.GetElementById("target")!;
        var defaultColor = includeUserAgent ? LinkBlue : ParentColor;
        CssStyleSource user = new("a{color:green}", CssOrigin.User);
        CssStyleSource author = new("a{color:red}");
        CssStyleSource revert = new("a{color:revert}");
        CssStyleSource userImportant = new("a{color:#345678!important}", CssOrigin.User);
        CssStyleSource authorImportant = new("a{color:#56789a!important}");

        AssertColors(Compute(document, includeUserAgent), defaultColor);
        AssertColors(Compute(document, includeUserAgent, user), new(0, 128, 0));
        AssertColors(Compute(document, includeUserAgent, user, author), new(255, 0, 0));
        AssertColors(Compute(document, includeUserAgent, new CssStyleSource("a{color:red}a{color:green}")), new(0, 128, 0));
        AssertColors(Compute(document, includeUserAgent, new CssStyleSource("#target{color:#010203}a{color:red}")), new(1, 2, 3));
        AssertColors(Compute(document, includeUserAgent, new CssStyleSource(":LiNk{color:#abc123}")), new(171, 193, 35));
        AssertColors(Compute(document, includeUserAgent, revert), defaultColor);
        AssertColors(Compute(document, includeUserAgent, user, revert), new(0, 128, 0));
        AssertColors(Compute(document, includeUserAgent,
            new("a{color:revert!important}", CssOrigin.User), authorImportant), defaultColor);

        target.SetAttribute("style", "color:#234567");
        AssertColors(Compute(document, includeUserAgent, user, author), new(35, 69, 103));
        AssertColors(Compute(document, includeUserAgent, user, authorImportant), new(86, 120, 154));
        AssertColors(Compute(document, includeUserAgent, userImportant, authorImportant), new(52, 86, 120));
        target.SetAttribute("style", "color:revert");
        AssertColors(Compute(document, includeUserAgent, user), new(0, 128, 0));
        AssertColors(Compute(document, includeUserAgent), defaultColor);

        void AssertColors(CssStyleResult result, CssColor expected)
        {
            Assert.Equal(expected, result.Styles[target]["color"]);
            Assert.Equal(expected, result.Styles[document.GetElementById("child")!]["color"]);
        }
    }

    [Fact]
    public void UnsupportedVisitedRuleCannotChangeThePrivacyPreservingDefault()
    {
        var document = Parse("<a id=target href='#visited'>text</a>");
        var result = CssStyleEngine.Compute(document,
            [new(":link,:visited{color:red}")], cancellationToken: Cancellation);

        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "unsupported-selector");
        Assert.Equal(LinkBlue, result.Styles[document.GetElementById("target")!]["color"]);
        Assert.Throws<UnsupportedCssException>(() =>
            CssSelectorList.Parse(":visited", cancellationToken: Cancellation));
    }

    [Fact]
    public void UserAgentRuleSharesExactAssignmentAndMatchingBudgetsAndCancellation()
    {
        var document = Parse("<a id=target href>text</a>");
        // The page matches 59 UA declarations; margin and padding expansion add six assignments.
        // The four elements (html, head, body, a) each check 107 top-level selectors and
        // 11 unmatched :is() subject alternatives, at two operations per simple selector.
        const int assignments = 65;
        const long matches = 944; // 4 * (107 + 11) * 2
        // The author rule adds one assignment and one two-operation selector per element.
        const int authorAssignments = 66;
        const long authorMatches = 952;
        CssStyleSource[] author = [new("a{color:red}")];

        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments - 1 }));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = matches - 1 }));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxAssignments = assignments }, author)); // 66 - 1
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = matches }, author));
        Assert.Throws<CssLimitException>(() => Calculate(new() { MaxMatchOperations = authorMatches - 1 }, author));
        var exact = Calculate(new() { MaxAssignments = assignments, MaxMatchOperations = matches });
        Assert.Empty(exact.Diagnostics);
        Assert.Equal(LinkBlue, exact.Styles[document.GetElementById("target")!]["color"]);
        var exactWithAuthor = Calculate(new() { MaxAssignments = authorAssignments, MaxMatchOperations = authorMatches }, author);
        Assert.Empty(exactWithAuthor.Diagnostics);
        Assert.Equal(new CssColor(255, 0, 0), exactWithAuthor.Styles[document.GetElementById("target")!]["color"]);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            CssStyleEngine.Compute(document, cancellationToken: canceled.Token));

        CssStyleResult Calculate(CssOptions options, CssStyleSource[]? sources = null) =>
            CssStyleEngine.Compute(document, sources, options: options, cancellationToken: Cancellation);
    }

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;

    private static CssStyleResult Compute(DomDocument document, bool includeUserAgent, params CssStyleSource[] sources)
    {
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: includeUserAgent,
            cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        return result;
    }
}
