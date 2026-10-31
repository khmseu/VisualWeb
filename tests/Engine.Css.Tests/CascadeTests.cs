using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class CascadeTests
{
    [Theory]
    [InlineData("red", 255, 0, 0, 255)]
    [InlineData("none", 0, 0, 0, 0)]
    [InlineData("transparent", 0, 0, 0, 0)]
    [InlineData("none LIGHTGRAY", 211, 211, 211, 255)]
    [InlineData("blue none", 0, 0, 255, 255)]
    [InlineData("#1234", 17, 34, 51, 68)]
    [InlineData("rgba(10,20,30,.5) none", 10, 20, 30, 128)]
    [InlineData("none rgb(100%,0%,0%)", 255, 0, 0, 255)]
    public void ColorOnlyBackgroundShorthandUsesExistingColorParser(string value, byte r, byte g, byte b, byte a)
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#child{background:" + value + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(r, g, b, a), Child(result, doc)["background-color"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("red blue")]
    [InlineData("none none")]
    [InlineData("red none none")]
    [InlineData("red repeat")]
    [InlineData("red center")]
    [InlineData("red / cover")]
    [InlineData("red fixed")]
    [InlineData("red border-box")]
    [InlineData("url(image.png) red")]
    [InlineData("linear-gradient(red,blue) red")]
    [InlineData("none, red")]
    [InlineData("inherit red")]
    public void UnsupportedBackgroundShorthandIsAtomic(string value)
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#child{background-color:lime;background:" + value + "}"));
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 255, 0), Child(result, doc)["background-color"]);
    }

    [Theory]
    [InlineData("initial", 0, 0)]
    [InlineData("unset", 0, 0)]
    [InlineData("inherit", 255, 255)]
    [InlineData("revert", 0, 0)]
    public void BackgroundShorthandWideKeywordsFollowNonInheritedLonghand(string keyword, byte red, byte alpha)
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#parent{background:red}#child{background:blue;background:" + keyword + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, 0, 0, alpha), Child(result, doc)["background-color"]);
    }

    [Fact]
    public void BackgroundShorthandRetainsCurrentColorImportanceSourceOrderAndInlineCascade()
    {
        var doc = Document("<div id=parent><p id=child style='color:blue;background:currentColor!important'>Text</p></div>");
        var result = Compute(doc, new CssStyleSource("#child{background:red}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Child(result, doc)["background-color"]);
        var ordinary = Document();
        var ordered = Compute(ordinary, new CssStyleSource("#child{background:red!important;background-color:blue}"));
        Assert.Equal(new CssColor(255, 0, 0), Child(ordered, ordinary)["background-color"]);
        var reset = Compute(ordinary, new CssStyleSource("#child{background-color:red;background:none}"));
        Assert.Equal(new CssColor(0, 0, 0, 0), Child(reset, ordinary)["background-color"]);
    }

    [Fact]
    public void BackgroundShorthandUsesOneExactAssignmentAndInvalidValuesUseNone()
    {
        var doc = Document();
        CssStyleSource[] source = [new("#child{background:red}")];
        Assert.Empty(CssStyleEngine.Compute(doc, source, options: new() { MaxAssignments = 1 },
            includeUserAgent: false, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc,
            [new("#child{background:red;background:none}")], options: new() { MaxAssignments = 1 },
            includeUserAgent: false, cancellationToken: Cancellation));
        var invalid = CssStyleEngine.Compute(doc, [new("#child{background:red repeat;background:blue}")],
            options: new() { MaxAssignments = 1 }, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Single(invalid.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 255), Child(invalid, doc)["background-color"]);
    }

    [Fact]
    public void TableUaDisplaysAndInheritedSpacingUseTheFiniteSeparateModel()
    {
        var doc = Document("<table id=parent><tr><td id=child>x</td></tr></table>");
        var result = Compute(doc, new CssStyleSource("table{border-spacing:.5em}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("table"), result.Styles[doc.GetElementById("parent")!]["display"]);
        Assert.Equal(new CssKeyword("table-cell"), Child(result, doc)["display"]);
        Assert.Equal(new CssKeyword("top"), Child(result, doc)["vertical-align"]);
        Assert.Equal(new CssKeyword("separate"), Child(result, doc)["border-collapse"]);
        Assert.Equal(new CssLength(8, "px"), Child(result, doc)["border-spacing"]);
        Assert.Equal(new CssLength(1, "px"), Child(result, doc)["padding-left"]);
    }

    [Theory]
    [InlineData("border-spacing:-1px")]
    [InlineData("border-spacing:10%")]
    [InlineData("border-spacing:1px 2px")]
    [InlineData("border-collapse:collapse")]
    [InlineData("table-layout:fixed")]
    [InlineData("vertical-align:middle")]
    public void UnsupportedTableCssProducesDiagnostics(string declaration) =>
        Assert.NotEmpty(Compute(Document(), new CssStyleSource("#child{" + declaration + "}")).Diagnostics);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document(string body = "<div id=parent><p id=child class=x>text</p></div>")
        => HtmlParser.Parse("<!doctype html>" + body, cancellationToken: Cancellation).Document;
    private static CssStyleResult Compute(DomDocument doc, params CssStyleSource[] sources)
        => CssStyleEngine.Compute(doc, sources, cancellationToken: Cancellation);
    private static CssComputedStyle Child(CssStyleResult result, DomDocument doc) => result.Styles[doc.GetElementById("child")!];

    [Fact]
    public void UnorderedListDefaultsAndInheritedMarkerSuppressionAreComputed()
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var defaults = Compute(doc);
        Assert.Empty(defaults.Diagnostics);
        Assert.Equal(new CssKeyword("list-item"), Child(defaults, doc)["display"]);
        Assert.Equal(new CssKeyword("disc"), Child(defaults, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(defaults, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(defaults, doc)["list-style-image"]);
        Assert.Equal(new CssLength(40, "px"), defaults.Styles[doc.GetElementById("parent")!]["padding-left"]);
        var suppressed = Compute(doc, new CssStyleSource("#parent{list-style-type:none}"));
        Assert.Empty(suppressed.Diagnostics);
        Assert.Equal(new CssKeyword("none"), Child(suppressed, doc)["list-style-type"]);
    }

    [Theory]
    [InlineData("list-style-type:square")]
    [InlineData("list-style-type:SqUaRe")]
    [InlineData("list-style-type:sq\\75 are")]
    [InlineData("list-style:square")]
    [InlineData("list-style:none square outside")]
    [InlineData("list-style:outside SQUARE none")]
    [InlineData("list-style:square!important")]
    public void SquareListStyleUsesTheCascadeAndInheritance(string declaration)
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var result = Compute(doc, new CssStyleSource("#parent{" + declaration + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("square"), result.Styles[doc.GetElementById("parent")!]["list-style-type"]);
        Assert.Equal(new CssKeyword("square"), Child(result, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(result, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-image"]);
    }

    [Fact]
    public void SquareListStyleKeepsImportanceResetsAndExactAssignmentLimit()
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var important = Compute(doc, new CssStyleSource(
            "#parent{list-style:square} #child{list-style-type:decimal!important;list-style:square}"));
        Assert.Empty(important.Diagnostics);
        Assert.Equal(new CssKeyword("decimal"), Child(important, doc)["list-style-type"]);
        var squareImportant = Compute(doc, new CssStyleSource(
            "#child{list-style:square!important;list-style-type:decimal}"));
        Assert.Empty(squareImportant.Diagnostics);
        Assert.Equal(new CssKeyword("square"), Child(squareImportant, doc)["list-style-type"]);
        var reset = Compute(doc, new CssStyleSource("#child{list-style:square;list-style:outside}"));
        Assert.Empty(reset.Diagnostics);
        Assert.Equal(new CssKeyword("disc"), Child(reset, doc)["list-style-type"]);

        var sources = new[] { new CssStyleSource("#child{list-style:square outside none}") };
        Assert.Empty(CssStyleEngine.Compute(doc, sources, options: new() { MaxAssignments = 3 },
            includeUserAgent: false, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, sources,
            options: new() { MaxAssignments = 2 }, includeUserAgent: false, cancellationToken: Cancellation));
    }

    [Theory]
    [InlineData("initial", "disc")]
    [InlineData("inherit", "square")]
    [InlineData("unset", "square")]
    [InlineData("revert", "square")]
    public void SquareListStyleUsesExistingCssWideKeywordSemantics(string keyword, string expected)
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var result = Compute(doc, new CssStyleSource(
            "#parent{list-style:square}#child{list-style:decimal;list-style:" + keyword + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expected), Child(result, doc)["list-style-type"]);
    }

    [Theory]
    [InlineData("list-style-type:circle")]
    [InlineData("list-style-type:CiRcLe")]
    [InlineData("list-style-type:ci\\72 cle")]
    [InlineData("list-style:CIRCLE")]
    [InlineData("list-style:ci\\72 cle outside none")]
    [InlineData("list-style:none circle outside")]
    [InlineData("list-style:outside CIRCLE none")]
    [InlineData("list-style:circle!important")]
    public void CircleListStyleUsesTheCascadeAndInheritance(string declaration)
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var result = Compute(doc, new CssStyleSource("#parent{" + declaration + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword("circle"), result.Styles[doc.GetElementById("parent")!]["list-style-type"]);
        Assert.Equal(new CssKeyword("circle"), Child(result, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(result, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-image"]);
    }

    [Fact]
    public void CircleListStyleRespectsImportanceResetsNestedDefaultsAndAssignmentLimits()
    {
        var doc = Document("<ul id=parent><li id=child><ul id=nested><li id=inner>x</li></ul></li></ul>");
        var important = Compute(doc, new CssStyleSource(
            "#parent{list-style:circle!important}#child{list-style:square;list-style-type:circle!important;list-style-type:decimal}"));
        Assert.Empty(important.Diagnostics);
        Assert.Equal(new CssKeyword("circle"), Child(important, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("disc"), important.Styles[doc.GetElementById("nested")!]["list-style-type"]);
        Assert.Equal(new CssKeyword("disc"), important.Styles[doc.GetElementById("inner")!]["list-style-type"]);

        var inherited = Compute(doc, new CssStyleSource("#parent{list-style:circle}#nested{list-style:inherit}"));
        Assert.Empty(inherited.Diagnostics);
        Assert.Equal(new CssKeyword("circle"), inherited.Styles[doc.GetElementById("inner")!]["list-style-type"]);
        var childOverride = Compute(doc, new CssStyleSource(
            "#parent{list-style:circle!important}#child{list-style-type:square}"));
        Assert.Equal(new CssKeyword("square"), Child(childOverride, doc)["list-style-type"]);
        var reset = Compute(doc, new CssStyleSource("#child{list-style:circle;list-style:outside}"));
        Assert.Empty(reset.Diagnostics);
        Assert.Equal(new CssKeyword("disc"), Child(reset, doc)["list-style-type"]);
        var overridden = Compute(doc, new CssStyleSource(
            "#child{list-style:circle;list-style-type:decimal!important;list-style:circle}"));
        Assert.Equal(new CssKeyword("decimal"), Child(overridden, doc)["list-style-type"]);

        var sources = new[] { new CssStyleSource("#child{list-style:circle outside none}") };
        Assert.Empty(CssStyleEngine.Compute(doc, sources, options: new() { MaxAssignments = 3 },
            includeUserAgent: false, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, sources,
            options: new() { MaxAssignments = 2 }, includeUserAgent: false, cancellationToken: Cancellation));
    }

    [Theory]
    [InlineData("list-style", "initial", "disc")]
    [InlineData("list-style", "inherit", "circle")]
    [InlineData("list-style", "unset", "circle")]
    [InlineData("list-style", "revert", "circle")]
    [InlineData("list-style-type", "initial", "disc")]
    [InlineData("list-style-type", "inherit", "circle")]
    [InlineData("list-style-type", "unset", "circle")]
    [InlineData("list-style-type", "revert", "circle")]
    public void CircleListStyleUsesNormalCssWideKeywordSemantics(string property, string keyword, string expected)
    {
        var doc = Document("<ul id=parent><li id=child>item</li></ul>");
        var result = Compute(doc, new CssStyleSource(
            "#parent{list-style:circle}#child{list-style:decimal;" + property + ":" + keyword + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expected), Child(result, doc)["list-style-type"]);
    }

    [Theory]
    [InlineData("circle decimal")]
    [InlineData("circle circle")]
    [InlineData("none none circle")]
    [InlineData("circle none none")]
    [InlineData("circle inside")]
    [InlineData("circle url(marker.png)")]
    [InlineData("circle outside outside")]
    [InlineData("inherit circle")]
    public void InvalidCircleShorthandNeverPartiallyOverridesLonghands(string value)
    {
        var doc = Document("<ul id=parent><li id=child>x</li></ul>");
        var result = Compute(doc, new CssStyleSource(
            "#child{list-style:decimal;list-style:" + value + "}"));
        Assert.Single(result.Diagnostics, d => d.Code == "invalid-property-value");
        Assert.Equal(new CssKeyword("decimal"), Child(result, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(result, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-image"]);
    }

    [Fact]
    public void InvalidImportantCircleShorthandCannotOverrideAnEarlierValidStyle()
    {
        var doc = Document("<ul><li id=child>x</li></ul>");
        var result = Compute(doc, new CssStyleSource(
            "#child{list-style:decimal;list-style:circle inside!important}"));
        Assert.Single(result.Diagnostics, d => d.Code == "invalid-property-value");
        Assert.Equal(new CssKeyword("decimal"), Child(result, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(result, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-image"]);
    }

    [Theory]
    [InlineData("list-style-type:lower-alpha")]
    [InlineData("list-style-position:inside")]
    [InlineData("list-style-image:url(marker.png)")]
    public void UnsupportedListStylesProduceDiagnostics(string declaration)
    {
        var doc = Document();
        Assert.NotEmpty(Compute(doc, new CssStyleSource("#child{" + declaration + "}")).Diagnostics);
    }

    [Fact]
    public void OrderedListsDefaultToDecimalAndNestedUnorderedListsResetToDisc()
    {
        var doc = Document("<ol id=parent><li id=child><ul id=nested><li id=inner>x</li></ul></li></ol>");
        var styles = Compute(doc);
        Assert.Empty(styles.Diagnostics);
        Assert.Equal(new CssKeyword("decimal"), Child(styles, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("disc"), styles.Styles[doc.GetElementById("inner")!]["list-style-type"]);
        var overridden = Compute(doc, new CssStyleSource("#nested{list-style-type:decimal}"));
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal(new CssKeyword("decimal"), overridden.Styles[doc.GetElementById("inner")!]["list-style-type"]);
    }

    [Theory]
    [InlineData("none", "none")]
    [InlineData("none none", "none")]
    [InlineData("outside", "disc")]
    [InlineData("decimal outside none", "decimal")]
    [InlineData("none outside disc", "disc")]
    [InlineData("OUTSIDE DECIMAL", "decimal")]
    public void ListStyleShorthandExpandsAllLonghandsAndResolvesNone(string value, string type)
    {
        var doc = Document("<ol><li id=child>x</li></ol>");
        var result = Compute(doc, new CssStyleSource("li{list-style:" + value + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(type), Child(result, doc)["list-style-type"]);
        Assert.Equal(new CssKeyword("outside"), Child(result, doc)["list-style-position"]);
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-image"]);
    }

    [Theory]
    [InlineData("disc decimal")]
    [InlineData("outside outside")]
    [InlineData("none none decimal")]
    [InlineData("none none none")]
    [InlineData("decimal inside")]
    [InlineData("decimal url(marker.png)")]
    [InlineData("inherit decimal")]
    [InlineData("square decimal")]
    [InlineData("square square")]
    [InlineData("square inside")]
    public void InvalidListStyleShorthandNeverPartiallyOverridesLonghands(string value)
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#child{list-style-type:decimal;list-style:" + value + "}"));
        Assert.Single(result.Diagnostics, d => d.Code == "invalid-property-value");
        Assert.Equal(new CssKeyword("decimal"), Child(result, doc)["list-style-type"]);
    }

    [Theory]
    [InlineData("initial", "disc")]
    [InlineData("inherit", "none")]
    [InlineData("unset", "none")]
    [InlineData("revert", "none")]
    public void ListStyleShorthandUsesNormalWideKeywordInheritance(string keyword, string expected)
    {
        var doc = Document("<ul id=parent><li id=child>x</li></ul>");
        var result = Compute(doc, new CssStyleSource("#parent{list-style:none} #child{list-style:decimal;list-style:" + keyword + "}"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssKeyword(expected), Child(result, doc)["list-style-type"]);
    }

    [Fact]
    public void ListStyleShorthandPreservesImportanceOrderAndExactAssignmentBudget()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("#child{list-style:none!important;list-style-type:decimal}"));
        Assert.Equal(new CssKeyword("none"), Child(result, doc)["list-style-type"]);
        var ordered = Compute(doc, new CssStyleSource("#child{list-style:decimal;list-style-type:none;list-style:outside}"));
        Assert.Empty(ordered.Diagnostics);
        Assert.Equal(new CssKeyword("disc"), Child(ordered, doc)["list-style-type"]);
        var sources = new[] { new CssStyleSource("#child{list-style:none}") };
        Assert.Empty(CssStyleEngine.Compute(doc, sources, options: new() { MaxAssignments = 3 },
            includeUserAgent: false, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(doc, sources, options: new() { MaxAssignments = 2 },
            includeUserAgent: false, cancellationToken: Cancellation));
    }

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
    public void UaFormRulesBlockFormsAndHideHiddenInputsImportantly()
    {
        var doc = Document("<form id=f><input id=h type=HiDdEn><input id=t></form>");
        var result = Compute(doc, new CssStyleSource("input{display:block !important}"));
        Assert.Equal(new CssKeyword("block"), result.Styles[doc.GetElementById("f")!]["display"]);
        Assert.Equal(new CssKeyword("none"), result.Styles[doc.GetElementById("h")!]["display"]);
        Assert.Equal(new CssKeyword("block"), result.Styles[doc.GetElementById("t")!]["display"]);
    }

    [Fact]
    public void UnsupportedFeaturesProduceDiagnosticsAndNeverExecuteOrFetch()
    {
        var doc = Document();
        var result = Compute(doc, new CssStyleSource("@import 'http://example.com/x'; @media screen and (min-width:1px){p{color:blue}}" +
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
    public void OverflowAxisLonghandsAreParsedAndComputed()
    {
        var doc = Document("<div id=box></div>");
        var result = Compute(doc, new CssStyleSource("#box{overflow-x:hidden;overflow-y:auto}"));
        var style = result.Styles[doc.GetElementById("box")!];
        Assert.Equal(new CssKeyword("hidden"), style["overflow-x"]);
        Assert.Equal(new CssKeyword("auto"), style["overflow-y"]);
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
