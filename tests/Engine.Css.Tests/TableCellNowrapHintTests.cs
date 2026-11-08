using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableCellNowrapHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableCellNowrapMapsBooleanAttributeToWhiteSpace()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr><td id=cell nowrap=false>x</td>"
            + "<th id=header nowrap=''>y</th></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        foreach (var id in new[] { "cell", "header" })
        {
            var element = document.GetElementById(id)!;
            Assert.Equal(new CssKeyword("nowrap"), result.Styles[element]["white-space"]);
            Assert.True(CssStyleEngine.HasSupportedTableCellNowrapHint(element));
        }
    }

    [Fact]
    public void TableCellNowrapUsesAuthorCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table><tr><td id=cell nowrap>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var cell = document.GetElementById("cell")!;
        var author = new CssStyleSource("#cell{white-space:normal}");

        Assert.Equal(new CssKeyword("normal"),
            CssStyleEngine.Compute(document, [author], includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["white-space"]);

        cell.RemoveAttribute("nowrap");
        Assert.Equal(new CssKeyword("normal"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["white-space"]);

        cell.SetAttribute("nowrap", "false");
        Assert.Equal(new CssKeyword("nowrap"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["white-space"]);
    }

    [Fact]
    public void TableCellNowrapChargesTheSharedAssignmentLimit()
    {
        var oneCell = HtmlParser.Parse(
            "<!doctype html><table><tr><td nowrap>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoCells = HtmlParser.Parse(
            "<!doctype html><table><tr><td nowrap>x</td><th nowrap>y</th></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneCell, includeUserAgent: false, options: new() { MaxAssignments = 1 },
            cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoCells, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
    }

    [Fact]
    public void TableCellNowrapHintIsLimitedToTdAndTh()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=table nowrap><tbody><tr id=row nowrap>"
            + "<td id=cell nowrap>x</td></tr></tbody></table>",
            cancellationToken: Cancellation).Document;

        Assert.False(CssStyleEngine.HasSupportedTableCellNowrapHint(document.GetElementById("table")!));
        Assert.False(CssStyleEngine.HasSupportedTableCellNowrapHint(document.GetElementById("row")!));
        Assert.True(CssStyleEngine.HasSupportedTableCellNowrapHint(document.GetElementById("cell")!));
    }
}
