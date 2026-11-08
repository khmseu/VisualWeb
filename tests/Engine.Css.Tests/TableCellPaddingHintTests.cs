using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableCellPaddingHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableCellpaddingMapsToAllFourPaddingPropertiesOfAssociatedCells()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table cellpadding=' +07junk'><tbody><tr>"
            + "<td id=cell>x</td><th id=header>y</th></tr></tbody></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        foreach (var id in new[] { "cell", "header" })
        {
            var style = result.Styles[document.GetElementById(id)!];
            Assert.Equal(new CssLength(7, "px"), style["padding-top"]);
            Assert.Equal(new CssLength(7, "px"), style["padding-right"]);
            Assert.Equal(new CssLength(7, "px"), style["padding-bottom"]);
            Assert.Equal(new CssLength(7, "px"), style["padding-left"]);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("-5")]
    public void InvalidTableCellpaddingAddsNoCellHint(string value)
    {
        var document = HtmlParser.Parse(
            $"<!doctype html><table cellpadding='{value}'><tr><td id=cell>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        var style = result.Styles[document.GetElementById("cell")!];

        Assert.Equal(new CssLength(0, "px"), style["padding-top"]);
        Assert.Equal(new CssLength(0, "px"), style["padding-right"]);
        Assert.Equal(new CssLength(0, "px"), style["padding-bottom"]);
        Assert.Equal(new CssLength(0, "px"), style["padding-left"]);
    }

    [Fact]
    public void ZeroTableCellpaddingOverridesTheUserAgentCellPadding()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table cellpadding=0><tr><td id=cell>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(0, "px"), result.Styles[document.GetElementById("cell")!]["padding-top"]);
    }

    [Fact]
    public void TableCellpaddingUsesAuthorHintCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=t cellpadding=4><tr>"
            + "<td id=cell style='padding-bottom:11px'>x</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var table = document.GetElementById("t")!;
        var cell = document.GetElementById("cell")!;
        var author = new CssStyleSource("#cell{padding-top:5px;padding-left:9px}");

        var result = CssStyleEngine.Compute(document, [author], includeUserAgent: false,
            cancellationToken: Cancellation);
        Assert.Equal(new CssLength(5, "px"), result.Styles[cell]["padding-top"]);
        Assert.Equal(new CssLength(4, "px"), result.Styles[cell]["padding-right"]);
        Assert.Equal(new CssLength(11, "px"), result.Styles[cell]["padding-bottom"]);
        Assert.Equal(new CssLength(9, "px"), result.Styles[cell]["padding-left"]);

        table.SetAttribute("cellpadding", "2");
        Assert.Equal(new CssLength(2, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["padding-right"]);
        table.RemoveAttribute("cellpadding");
        Assert.Equal(new CssLength(0, "px"),
            CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation)
                .Styles[cell]["padding-right"]);
    }

    [Fact]
    public void NearestTableCellpaddingAppliesToNestedTableCells()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table cellpadding=2><tr><td id=outer>"
            + "<table cellpadding=4><tr><td id=inner>x</td></tr></table>"
            + "</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(2, "px"), result.Styles[document.GetElementById("outer")!]["padding-left"]);
        Assert.Equal(new CssLength(4, "px"), result.Styles[document.GetElementById("inner")!]["padding-left"]);
    }

    [Fact]
    public void TableCellpaddingSharesAssignmentAndAggregateRawAttributeLimits()
    {
        var oneTable = HtmlParser.Parse(
            "<!doctype html><table cellpadding=1><tr><td>x</td><td>y</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var twoTables = HtmlParser.Parse(
            "<!doctype html><table cellpadding=1><tr><td>x</td></tr></table>"
            + "<table cellpadding=1><tr><td>y</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var invalidTable = HtmlParser.Parse(
            "<!doctype html><table cellpadding=invalid><tr><td>x</td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleEngine.Compute(oneTable, includeUserAgent: false, options: new()
        {
            MaxTableCellPaddingAttributeCharacters = 1,
            MaxAssignments = 8
        }, cancellationToken: Cancellation);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(oneTable, includeUserAgent: false,
            options: new() { MaxTableCellPaddingAttributeCharacters = 1, MaxAssignments = 7 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoTables, includeUserAgent: false,
            options: new() { MaxTableCellPaddingAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidTable, includeUserAgent: false,
            options: new() { MaxTableCellPaddingAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneTable, includeUserAgent: false,
            options: new() { MaxTableCellPaddingAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void TableCellpaddingDoesNotEscapeItsTableOrApplyToCellAttributes()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table cellpadding=3><tr><td id=outer>x</td></tr></table>"
            + "<table><tr><td id=other cellpadding=7>y</td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssLength(3, "px"), result.Styles[document.GetElementById("outer")!]["padding-left"]);
        Assert.Equal(new CssLength(0, "px"), result.Styles[document.GetElementById("other")!]["padding-left"]);
    }
}
