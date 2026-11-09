using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableCellWidthHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableCellWidthMapsNonzeroDimensionsAndIgnoresZeroOrInvalidValues()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=table width=13><tr id=row width=14><td id=pixels width=' 007junk'>x</td>"
            + "<th id=percent width='12.5%'>y</th><td id=zero width=0>z</td>"
            + "<td id=invalid width=invalid>w</td><td id=table-target>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(7, "px"), result.Styles[document.GetElementById("pixels")!]["width"]);
        Assert.Equal(new CssLength(12.5, "%"), result.Styles[document.GetElementById("percent")!]["width"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("zero")!]["width"]);
        Assert.Equal(new CssKeyword("auto"), result.Styles[document.GetElementById("invalid")!]["width"]);
        Assert.Equal(new CssLength(13, "px"), result.Styles[document.GetElementById("table")!]["width"]);
        Assert.True(CssStyleEngine.HasSupportedTableCellWidthHint(document.GetElementById("pixels")!));
        Assert.False(CssStyleEngine.HasSupportedTableCellWidthHint(document.GetElementById("row")!));
        Assert.False(CssStyleEngine.HasSupportedTableCellWidthHint(document.GetElementById("table")!));
    }

    [Fact]
    public void TableCellWidthUsesAuthorCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr><td id=cell width=20>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var cell = document.GetElementById("cell")!;
        var author = new CssStyleSource("#cell{width:30px}");

        Assert.Equal(new CssLength(30, "px"),
            CssStyleEngine.Compute(document, [author], includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["width"]);
        Assert.Equal(new CssLength(20, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["width"]);

        cell.RemoveAttribute("width");
        Assert.Equal(new CssKeyword("auto"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["width"]);
        cell.SetAttribute("width", "25");
        Assert.Equal(new CssLength(25, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["width"]);
    }

    [Fact]
    public void TableCellWidthChargesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneCell = HtmlParser.Parse(
            "<!doctype html><table><tr><td width=1>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoCells = HtmlParser.Parse(
            "<!doctype html><table><tr><td width=1>x</td><th width=2>y</th></tr></table>",
            cancellationToken: Cancellation).Document;
        var invalidCell = HtmlParser.Parse(
            "<!doctype html><table><tr><td width=invalid>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneCell, includeUserAgent: false, options: new()
        {
            MaxTableCellWidthAttributeCharacters = 1,
            MaxAssignments = 1
        }, cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoCells, includeUserAgent: false,
            options: new() { MaxTableCellWidthAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoCells, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidCell, includeUserAgent: false,
            options: new() { MaxTableCellWidthAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneCell, includeUserAgent: false,
            options: new() { MaxTableCellWidthAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void TableCellWidthRejectsValuesOutsideFiniteNumericRange()
    {
        var huge = HtmlParser.Parse(
            $"<!doctype html><table><tr><td width='{new string('9', 400)}'>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var tiny = HtmlParser.Parse(
            $"<!doctype html><table><tr><td width='0.{new string('0', 400)}1%'>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(huge, includeUserAgent: false, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() =>
            CssStyleEngine.Compute(tiny, includeUserAgent: false, cancellationToken: Cancellation));
    }
}
