using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class MediaRuleTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", true)]
    [InlineData("all", true)]
    [InlineData("screen", true)]
    [InlineData("PRINT", false)]
    [InlineData("print, screen", true)]
    [InlineData("all,print", true)]
    [InlineData("print,print", false)]
    [InlineData("ScReEn", true)]
    [InlineData("\\73 creen", true)]
    [InlineData("screen/**/,/**/print", true)]
    public void MediaTypeListsApplyAccordingToScreenPolicy(string media, bool applies)
    {
        var (document, result) = Compute("p{color:red}@media " + media + "{p{color:blue}}");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(applies ? new CssColor(0, 0, 255) : new CssColor(255, 0, 0), Color(document, result));
    }

    [Theory]
    [InlineData("@MEDIA")]
    [InlineData("@\\6d edia")]
    public void MediaRuleNameUsesCssIdentifierCaseAndEscapes(string name)
    {
        var (document, result) = Compute("p{color:red}" + name + " screen{p{color:blue}}");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Color(document, result));
    }

    [Fact]
    public void NestedMediaRulesRequireEveryEnclosingCondition()
    {
        var (document, result) = Compute("""
            p{color:red}
            @media all {
                @media print { p{color:lime} }
                @media screen { p{color:blue} }
            }
            @media print { @media screen { p{color:lime} } }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Color(document, result));
    }

    [Fact]
    public void MediaRulesKeepTheirCascadePositionSpecificityAndImportance()
    {
        var document = Document();
        CssStyleSource[] sources =
        [
            new("p{color:red}@media screen{p{color:blue}#subject{background-color:lime}}p{color:green}"),
            new("@media all{p{color:blue!important}}p{color:red}")
        ];
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Color(document, result));
        Assert.Equal(new CssColor(0, 255, 0), result.Styles[document.GetElementById("subject")!]["background-color"]);
    }

    [Fact]
    public void InactivePrintRulesDoNotEvaluateUnsupportedContents()
    {
        var (document, result) = Compute(
            "p{color:blue}@media print{@import 'never.css';p:hover{display:grid;color:bad}}");
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Color(document, result));
    }

    [Theory]
    [InlineData("screen and (min-width:1px)")]
    [InlineData("not screen")]
    [InlineData("only screen")]
    [InlineData("screen,unknown")]
    [InlineData("screen,(width:1px)")]
    [InlineData("screen,,print")]
    [InlineData(",screen")]
    [InlineData("screen,")]
    [InlineData("(prefers-color-scheme:dark)")]
    [InlineData("tv")]
    public void UnsupportedMediaQueriesDiagnoseTheWholeRuleWithoutApplyingIt(string media)
    {
        var (document, result) = Compute("p{color:red}@media " + media + "{p{color:blue}}");
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-at-rule");
        Assert.Equal(new CssColor(255, 0, 0), Color(document, result));
    }

    [Fact]
    public void MissingBlocksAndUnsupportedActiveNestedRulesAreDiagnosed()
    {
        var (_, missing) = Compute("@media screen;");
        Assert.Contains(missing.Diagnostics, d => d.Code == "unsupported-at-rule");
        var (_, nested) = Compute("@media all{@supports (display:block){p{color:blue}}}");
        Assert.Contains(nested.Diagnostics, d => d.Code == "unsupported-at-rule");
        var (_, import) = Compute("@media screen{@import 'never.css';p{color:blue}}");
        Assert.Contains(import.Diagnostics, d => d.Code == "unsupported-at-rule");
        var (_, malformed) = Compute("@media screen{p{color:blue}");
        Assert.Contains(malformed.Diagnostics, d => d.Code == "eof-in-block");
    }

    [Fact]
    public void CdoAndCdcAreNotDiscardedAsTopLevelTokensInsideMedia()
    {
        var (_, result) = Compute("@media screen{<!--p{color:blue}-->}");
        Assert.Contains(result.Diagnostics, d => d.Code is "invalid-selector" or "eof-in-qualified-rule");
    }

    [Fact]
    public void NestedDiagnosticsKeepOriginalStylesheetOffsets()
    {
        const string css = "p{color:red}\r\n@media screen { p { color:bogus } }";
        var (_, result) = Compute(css);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "invalid-property-value");
        Assert.Equal(css.Replace("\r\n", "\n", StringComparison.Ordinal).IndexOf("color:bogus", StringComparison.Ordinal),
            diagnostic.Offset);
    }

    [Fact]
    public void NestedRuleWorkAndRuleCountsHaveExactSharedLimits()
    {
        var document = Document();
        CssStyleSource[] source = [new("@media screen{p{color:blue}}")];
        var exact = new CssOptions { MaxRules = 2, MaxNestedRuleOperations = 6, MaxAssignments = 1 };
        Assert.Empty(CssStyleEngine.Compute(document, source, includeUserAgent: false, options: exact,
            cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxRules = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxNestedRuleOperations = 5 }, cancellationToken: Cancellation));
        CssStyleSource[] twice = [source[0], source[0]];
        var shared = exact with { MaxNestedRuleOperations = 12, MaxAssignments = 2 };
        Assert.Empty(CssStyleEngine.Compute(document, twice, includeUserAgent: false, options: shared,
            cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, twice, includeUserAgent: false,
            options: shared with { MaxNestedRuleOperations = 11 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, twice, includeUserAgent: false,
            options: shared with { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Empty(CssStyleEngine.Compute(document, [new("@media print{p{color:blue}}")], includeUserAgent: false,
            options: exact with { MaxRules = 1, MaxNestedRuleOperations = 1 }, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxNestedRuleOperations = 0 }, cancellationToken: Cancellation));
    }

    [Fact]
    public void NestedMediaDepthAndRepeatedTokenVisitsHaveExactLimits()
    {
        var document = Document();
        CssStyleSource[] source = [new("@media all{@media all{p{color:blue}}}")];
        var exact = new CssOptions { MaxRules = 3, MaxDepth = 3, MaxNestedRuleOperations = 17 };
        Assert.Empty(CssStyleEngine.Compute(document, source, includeUserAgent: false, options: exact,
            cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxRules = 2 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxDepth = 2 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, source, includeUserAgent: false,
            options: exact with { MaxNestedRuleOperations = 16 }, cancellationToken: Cancellation));
    }

    [Fact]
    public void MediaTypeHelperDistinguishesUnsupportedListsAndObservesCancellation()
    {
        var tokens = CssSyntax.ParseStyleSheet("@media screen,(width:1px){}", cancellationToken: Cancellation).Rules[0].Prelude;
        Assert.False(CssMediaTypeList.TryMatchesScreen(tokens, out var matches, cancellationToken: Cancellation));
        Assert.False(matches);
        Assert.Throws<ArgumentNullException>(() => CssMediaTypeList.TryMatchesScreen(null!, out _, cancellationToken: Cancellation));
        Assert.Throws<ArgumentException>(() => CssMediaTypeList.TryMatchesScreen([null!], out _, cancellationToken: Cancellation));
        Assert.Throws<ArgumentException>(() => CssMediaTypeList.TryMatchesScreen(
            [new(CssTokenKind.Ident, null!, 0, "")], out _, cancellationToken: Cancellation));
        var valid = CssSyntax.ParseStyleSheet("@media screen,print{}", cancellationToken: Cancellation).Rules[0].Prelude;
        Assert.True(CssMediaTypeList.TryMatchesScreen(valid, out matches, new CssOptions { MaxTokens = 3 }, Cancellation));
        Assert.True(matches);
        Assert.Throws<CssLimitException>(() => CssMediaTypeList.TryMatchesScreen(valid, out _,
            new CssOptions { MaxTokens = 2 }, Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            CssMediaTypeList.TryMatchesScreen([], out _, cancellationToken: canceled.Token));
    }

    private static DomDocument Document() => HtmlParser.Parse("<!doctype html><p id=subject>text</p>",
        cancellationToken: Cancellation).Document;

    private static (DomDocument Document, CssStyleResult Result) Compute(string css)
    {
        var document = Document();
        return (document, CssStyleEngine.Compute(document, [new(css)], includeUserAgent: false,
            cancellationToken: Cancellation));
    }

    private static CssValue Color(DomDocument document, CssStyleResult result)
        => result.Styles[document.GetElementById("subject")!]["color"];
}
