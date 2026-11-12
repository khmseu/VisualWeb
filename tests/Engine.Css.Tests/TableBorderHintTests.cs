using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableBorderHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse(html, cancellationToken: Cancellation).Document;

    [Theory]
    [InlineData(" +03junk", 3, true)]
    [InlineData("0", 0, false)]
    [InlineData("invalid", 1, true)]
    [InlineData("-2", 1, true)]
    public void TableBorderMapsWidthAndConditionalRaisedCellBorders(string value, double width, bool visible)
    {
        var document = Parse(
            $"<!doctype html><table id=table border='{value}'><tr><td id=cell>x</td></tr></table>");
        var result = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        var table = result.Styles[document.GetElementById("table")!];
        var cell = result.Styles[document.GetElementById("cell")!];

        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Assert.Equal(new CssLength(width, "px"), table[$"border-{side}-width"]);
            Assert.Equal(new CssKeyword(visible ? "outset" : "none"), table[$"border-{side}-style"]);
            if (visible)
            {
                Assert.Equal(new CssLength(1, "px"), cell[$"border-{side}-width"]);
                Assert.Equal(new CssKeyword("inset"), cell[$"border-{side}-style"]);
            }
            else
            {
                Assert.Equal(new CssLength(0, "px"), cell[$"border-{side}-width"]);
                Assert.Equal(new CssKeyword("none"), cell[$"border-{side}-style"]);
            }
        }
    }

    [Fact]
    public void AuthorCssOverridesTableAndCellBorderHints()
    {
        var document = Parse(
            "<!doctype html><table id=table border=2><tr><td id=cell>x</td></tr></table>");
        var result = CssStyleEngine.Compute(document,
            [new CssStyleSource("table{border:5px solid}td{border:2px dashed}")],
            cancellationToken: Cancellation);

        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Assert.Equal(new CssLength(5, "px"),
                result.Styles[document.GetElementById("table")!][$"border-{side}-width"]);
            Assert.Equal(new CssKeyword("solid"),
                result.Styles[document.GetElementById("table")!][$"border-{side}-style"]);
            Assert.Equal(new CssLength(2, "px"),
                result.Styles[document.GetElementById("cell")!][$"border-{side}-width"]);
            Assert.Equal(new CssKeyword("dashed"),
                result.Styles[document.GetElementById("cell")!][$"border-{side}-style"]);
        }
    }

    [Fact]
    public void BorderWidthsAreHintsAndConditionalStylesComeFromTheUaRules()
    {
        var document = Parse(
            "<!doctype html><table id=table border=2><tr><td id=cell>x</td></tr></table>");
        var withoutUa = CssStyleEngine.Compute(document,
            [new CssStyleSource("table{border-style:solid}")],
            includeUserAgent: false, cancellationToken: Cancellation);
        var tableWithoutUa = withoutUa.Styles[document.GetElementById("table")!];
        var cellWithoutUa = withoutUa.Styles[document.GetElementById("cell")!];
        Assert.Equal(new CssLength(2, "px"), tableWithoutUa["border-top-width"]);
        Assert.Equal(new CssKeyword("solid"), tableWithoutUa["border-top-style"]);
        Assert.Equal(new CssKeyword("none"), cellWithoutUa["border-top-style"]);

        var withUserOverride = CssStyleEngine.Compute(document,
            [new CssStyleSource("table{border-top-style:solid}", CssOrigin.User)],
            cancellationToken: Cancellation);
        var tableWithUserOverride = withUserOverride.Styles[document.GetElementById("table")!];
        Assert.Equal(new CssKeyword("solid"), tableWithUserOverride["border-top-style"]);
        Assert.Equal(new CssKeyword("outset"), tableWithUserOverride["border-right-style"]);

        var withLaterUaRule = CssStyleEngine.Compute(document,
            [new CssStyleSource("table[border]{border-right-style:groove}", CssOrigin.UserAgent)],
            cancellationToken: Cancellation);
        Assert.Equal(new CssKeyword("groove"),
            withLaterUaRule.Styles[document.GetElementById("table")!]["border-right-style"]);
    }

    [Fact]
    public void NestedTablesUseTheirOwnBorderAttributes()
    {
        var document = Parse(
            "<!doctype html><table border=5><tr><td id=outer>"
            + "<table id=inner border=2><tr><td id=inner-cell>x</td></tr></table>"
            + "</td></tr></table>");
        var result = CssStyleEngine.Compute(document, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(1, "px"),
            result.Styles[document.GetElementById("outer")!]["border-left-width"]);
        Assert.Equal(new CssLength(2, "px"),
            result.Styles[document.GetElementById("inner")!]["border-left-width"]);
        Assert.Equal(new CssKeyword("inset"),
            result.Styles[document.GetElementById("inner-cell")!]["border-left-style"]);
    }

    [Fact]
    public void TableBorderSharesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneTable = Parse(
            "<!doctype html><table border=1><tr><td>x</td></tr><tr><td>y</td></tr></table>");
        var twoTables = Parse(
            "<!doctype html><table border=1><tr><td>x</td></tr></table>"
            + "<table border=1><tr><td>y</td></tr></table>");
        var twoInvalidTables = Parse(
            "<!doctype html><table border=x><tr><td>x</td></tr></table>"
            + "<table border=x><tr><td>y</td></tr></table>");

        CssStyleEngine.Compute(oneTable, includeUserAgent: false, options: new()
        {
            MaxTableBorderAttributeCharacters = 1,
            MaxAssignments = 4
        }, cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(oneTable, includeUserAgent: false,
            options: new() { MaxTableBorderAttributeCharacters = 1, MaxAssignments = 3 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoTables, includeUserAgent: false,
            options: new() { MaxTableBorderAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoInvalidTables, includeUserAgent: false,
            options: new() { MaxTableBorderAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneTable, includeUserAgent: false,
            options: new() { MaxTableBorderAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }
}
