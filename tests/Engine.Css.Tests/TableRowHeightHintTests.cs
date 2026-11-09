using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableRowHeightHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableRowHeightMapsDimensionsAndTargetsOnlyRows()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr id=pixels height=' 007junk'><td>x</td></tr>"
            + "<tr id=zero height=0><td>y</td></tr><tr id=percent height='12.5%'><td>z</td></tr>"
            + "<tr id=invalid height=invalid><td>w</td></tr></table>"
            + "<table><tbody id=group height=20><tr id=cell><td id=cell-height height=10>v</td></tr></tbody></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(7, "px"), result.Styles[document.GetElementById("pixels")!]["height"]);
        Assert.Equal(new CssLength(0, "px"), result.Styles[document.GetElementById("zero")!]["height"]);
        Assert.Equal(new CssLength(12.5, "%"), result.Styles[document.GetElementById("percent")!]["height"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("invalid")!]["height"]);
        Assert.True(CssStyleEngine.HasSupportedTableRowHeightHint(document.GetElementById("pixels")!));
        Assert.False(CssStyleEngine.HasSupportedTableRowHeightHint(document.GetElementById("group")!));
        Assert.False(CssStyleEngine.HasSupportedTableRowHeightHint(document.GetElementById("cell-height")!));
    }

    [Fact]
    public void TableRowHeightUsesAuthorCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr id=row height=20><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var row = document.GetElementById("row")!;

        Assert.Equal(new CssLength(30, "px"),
            CssStyleEngine.Compute(document, [new("#row{height:30px}")], includeUserAgent: false,
                cancellationToken: Cancellation).Styles[row]["height"]);
        Assert.Equal(new CssLength(20, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[row]["height"]);

        row.RemoveAttribute("height");
        Assert.Equal(new CssKeyword("auto"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[row]["height"]);
        row.SetAttribute("height", "0");
        Assert.Equal(new CssLength(0, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[row]["height"]);
    }

    [Fact]
    public void TableRowHeightChargesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneRow = HtmlParser.Parse(
            "<!doctype html><table><tr height=1><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoRows = HtmlParser.Parse(
            "<!doctype html><table><tr height=1><td>x</td></tr><tr height=2><td>y</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var invalidRow = HtmlParser.Parse(
            "<!doctype html><table><tr height=invalid><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneRow, includeUserAgent: false, options: new()
        {
            MaxTableRowHeightAttributeCharacters = 1,
            MaxAssignments = 1
        }, cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoRows, includeUserAgent: false,
            options: new() { MaxTableRowHeightAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoRows, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidRow, includeUserAgent: false,
            options: new() { MaxTableRowHeightAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneRow, includeUserAgent: false,
            options: new() { MaxTableRowHeightAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void TableRowHeightRejectsValuesOutsideFiniteNumericRange()
    {
        var huge = HtmlParser.Parse(
            $"<!doctype html><table><tr height='{new string('9', 400)}'><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var tiny = HtmlParser.Parse(
            $"<!doctype html><table><tr height='0.{new string('0', 400)}1'><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(huge, includeUserAgent: false, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(tiny, includeUserAgent: false, cancellationToken: Cancellation));
    }
}
