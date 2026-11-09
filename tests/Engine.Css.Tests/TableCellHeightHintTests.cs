using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableCellHeightHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableCellHeightMapsNonzeroDimensionsAndIgnoresZeroOrInvalidValues()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=table><tr id=row height=13><td id=pixels height=' 007junk'>x</td>"
            + "<th id=percent height='12.5%'>y</th><td id=zero height=0>z</td>"
            + "<td id=invalid height=invalid>w</td><td id=row-target>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(7, "px"), result.Styles[document.GetElementById("pixels")!]["height"]);
        Assert.Equal(new CssLength(12.5, "%"), result.Styles[document.GetElementById("percent")!]["height"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("zero")!]["height"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("invalid")!]["height"]);
        Assert.True(CssStyleEngine.HasSupportedTableCellHeightHint(document.GetElementById("pixels")!));
        Assert.False(CssStyleEngine.HasSupportedTableCellHeightHint(document.GetElementById("row")!));
        Assert.False(CssStyleEngine.HasSupportedTableCellHeightHint(document.GetElementById("table")!));
    }

    [Fact]
    public void TableCellHeightUsesAuthorCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr><td id=cell height=20>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var cell = document.GetElementById("cell")!;
        var author = new CssStyleSource("#cell{height:30px}");

        Assert.Equal(new CssLength(30, "px"),
            CssStyleEngine.Compute(document, [author], includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["height"]);
        Assert.Equal(new CssLength(20, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["height"]);

        cell.RemoveAttribute("height");
        Assert.Equal(new CssKeyword("auto"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["height"]);
        cell.SetAttribute("height", "25");
        Assert.Equal(new CssLength(25, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["height"]);
    }

    [Fact]
    public void TableCellHeightChargesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneCell = HtmlParser.Parse(
            "<!doctype html><table><tr><td height=1>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoCells = HtmlParser.Parse(
            "<!doctype html><table><tr><td height=1>x</td><th height=2>y</th></tr></table>",
            cancellationToken: Cancellation).Document;
        var invalidCell = HtmlParser.Parse(
            "<!doctype html><table><tr><td height=invalid>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneCell, includeUserAgent: false, options: new()
        {
            MaxTableCellHeightAttributeCharacters = 1,
            MaxAssignments = 1
        }, cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoCells, includeUserAgent: false,
            options: new() { MaxTableCellHeightAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoCells, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidCell, includeUserAgent: false,
            options: new() { MaxTableCellHeightAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneCell, includeUserAgent: false,
            options: new() { MaxTableCellHeightAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void TableCellHeightRejectsValuesOutsideFiniteNumericRange()
    {
        var huge = HtmlParser.Parse(
            $"<!doctype html><table><tr><td height='{new string('9', 400)}'>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var tiny = HtmlParser.Parse(
            $"<!doctype html><table><tr><td height='0.{new string('0', 400)}1'>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(huge, includeUserAgent: false, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(tiny, includeUserAgent: false, cancellationToken: Cancellation));
    }
}
