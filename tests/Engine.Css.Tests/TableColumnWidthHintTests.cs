using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableColumnWidthHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableColumnWidthMapsDimensionsAndTargetsOnlyColumns()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=table><colgroup id=group width=30>"
            + "<col id=pixels width=' 007junk'><col id=zero width=0>"
            + "<col id=percent width='12.5%'><col id=invalid width=invalid></colgroup>"
            + "<tr><td id=cell width=10>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(7, "px"), result.Styles[document.GetElementById("pixels")!]["width"]);
        Assert.Equal(new CssLength(0, "px"), result.Styles[document.GetElementById("zero")!]["width"]);
        Assert.Equal(new CssLength(12.5, "%"), result.Styles[document.GetElementById("percent")!]["width"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("invalid")!]["width"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("group")!]["width"]);
        Assert.True(CssStyleEngine.HasSupportedTableColumnWidthHint(document.GetElementById("pixels")!));
        Assert.False(CssStyleEngine.HasSupportedTableColumnWidthHint(document.GetElementById("group")!));
        Assert.False(CssStyleEngine.HasSupportedTableColumnWidthHint(document.GetElementById("table")!));
        Assert.False(CssStyleEngine.HasSupportedTableColumnWidthHint(document.GetElementById("cell")!));
    }

    [Fact]
    public void TableColumnWidthUsesAuthorCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><colgroup><col id=column width=20></colgroup>"
            + "<tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var column = document.GetElementById("column")!;

        Assert.Equal(new CssLength(30, "px"),
            CssStyleEngine.Compute(document, [new("#column{width:30px}")], includeUserAgent: false,
                cancellationToken: Cancellation).Styles[column]["width"]);
        Assert.Equal(new CssLength(20, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[column]["width"]);

        column.RemoveAttribute("width");
        Assert.Equal(new CssKeyword("auto"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[column]["width"]);
        column.SetAttribute("width", "25");
        Assert.Equal(new CssLength(25, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[column]["width"]);
    }

    [Fact]
    public void TableColumnWidthChargesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneColumn = HtmlParser.Parse(
            "<!doctype html><table><colgroup><col width=1></colgroup><tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoColumns = HtmlParser.Parse(
            "<!doctype html><table><colgroup><col width=1><col width=2></colgroup>"
            + "<tr><td>x</td><td>y</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var invalidColumn = HtmlParser.Parse(
            "<!doctype html><table><colgroup><col width=invalid></colgroup><tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneColumn, includeUserAgent: false, options: new()
        {
            MaxTableColumnWidthAttributeCharacters = 1,
            MaxAssignments = 1
        }, cancellationToken: Cancellation);
        var limit = Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(twoColumns, includeUserAgent: false,
            options: new() { MaxTableColumnWidthAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Equal("CSS table-column width attribute character limit exceeded.", limit.Message);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoColumns, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidColumn, includeUserAgent: false,
            options: new() { MaxTableColumnWidthAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneColumn, includeUserAgent: false,
            options: new() { MaxTableColumnWidthAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void TableColumnWidthRejectsValuesOutsideFiniteNumericRange()
    {
        var huge = HtmlParser.Parse(
            $"<!doctype html><table><colgroup><col width='{new string('9', 400)}'></colgroup>"
            + "<tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var tiny = HtmlParser.Parse(
            $"<!doctype html><table><colgroup><col width='0.{new string('0', 400)}1%'></colgroup>"
            + "<tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(huge, includeUserAgent: false, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(tiny, includeUserAgent: false, cancellationToken: Cancellation));
    }
}
