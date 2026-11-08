using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class TableBackgroundColorHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void TableBgcolorMapsAllCanonicalTableLayersToBackgroundColor()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=t bgcolor=chucknorris>"
            + "<thead id=thead bgcolor=green><tr id=head-row bgcolor=blue>"
            + "<th id=head-cell bgcolor=#abc></th></tr></thead>"
            + "<tbody id=tbody bgcolor=yellow><tr id=body-row bgcolor=white>"
            + "<td id=body-cell bgcolor=#123456></td></tr></tbody>"
            + "<tfoot id=tfoot bgcolor=black><tr id=foot-row bgcolor=navy>"
            + "<td id=foot-cell bgcolor=lime></td></tr></tfoot></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        AssertBackground(document, result, "t", new(192, 0, 0));
        AssertBackground(document, result, "thead", new(0, 128, 0));
        AssertBackground(document, result, "head-row", new(0, 0, 255));
        AssertBackground(document, result, "head-cell", new(170, 187, 204));
        AssertBackground(document, result, "tbody", new(255, 255, 0));
        AssertBackground(document, result, "body-row", new(255, 255, 255));
        AssertBackground(document, result, "body-cell", new(18, 52, 86));
        AssertBackground(document, result, "tfoot", new(0, 0, 0));
        AssertBackground(document, result, "foot-row", new(0, 0, 128));
        AssertBackground(document, result, "foot-cell", new(0, 255, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("transparent")]
    public void EmptyAndTransparentTableBgcolorValuesAddNoHint(string value)
    {
        var document = HtmlParser.Parse(
            $"<!doctype html><table id=t bgcolor='{value}'><tr><td id=c bgcolor='{value}'></td></tr></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        AssertBackground(document, result, "t", new(0, 0, 0, 0));
        AssertBackground(document, result, "c", new(0, 0, 0, 0));
    }

    [Fact]
    public void TableBgcolorUsesPresentationalHintCascadeAndRecomputesAfterMutation()
    {
        var document = HtmlParser.Parse("<!doctype html><table id=t bgcolor=red><tr><td></td></tr></table>",
            cancellationToken: Cancellation).Document;
        var table = document.GetElementById("t")!;

        Assert.Equal(new CssColor(255, 0, 0), Background(document, table,
            new CssStyleSource("table{background-color:blue}", CssOrigin.User)));
        Assert.Equal(new CssColor(0, 128, 0), Background(document, table,
            new CssStyleSource("table{background-color:green}")));
        table.SetAttribute("style", "background-color:yellow");
        Assert.Equal(new CssColor(255, 255, 0), Background(document, table));

        table.RemoveAttribute("style");
        table.SetAttribute("bgcolor", "#abc");
        Assert.Equal(new CssColor(170, 187, 204), Background(document, table));
        table.RemoveAttribute("bgcolor");
        Assert.Equal(new CssColor(0, 0, 0, 0), Background(document, table));
    }

    [Fact]
    public void TableBgcolorBoundsRawValuesAndHintAssignmentsAcrossTableLayers()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=t bgcolor=red><tr><td id=c bgcolor=transparent></td></tr></table>",
            cancellationToken: Cancellation).Document;

        CssStyleResult Calculate(int characters, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false, options: new()
            {
                MaxTableBackgroundColorAttributeCharacters = characters,
                MaxAssignments = assignments
            }, cancellationToken: Cancellation);

        AssertBackground(document, Calculate(14, 1), "t", new(255, 0, 0));
        Assert.Throws<CssLimitException>(() => Calculate(13, 1));

        document.GetElementById("c")!.SetAttribute("bgcolor", "blue");
        Assert.Throws<CssLimitException>(() => Calculate(7, 1));
        AssertBackground(document, Calculate(7, 2), "c", new(0, 0, 255));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 1));
    }

    [Fact]
    public void TableBgcolorDoesNotApplyToUnlistedElementsOrConsumeItsBudget()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><div id=d bgcolor=red></div><table id=t bgcolor=blue></table>",
            cancellationToken: Cancellation).Document;

        var result = CssStyleEngine.Compute(document, includeUserAgent: false,
            options: new() { MaxTableBackgroundColorAttributeCharacters = 4 },
            cancellationToken: Cancellation);

        AssertBackground(document, result, "d", new(0, 0, 0, 0));
        AssertBackground(document, result, "t", new(0, 0, 255));
    }

    private static void AssertBackground(DomDocument document, CssStyleResult result, string id, CssColor expected) =>
        Assert.Equal(expected, result.Styles[document.GetElementById(id)!]["background-color"]);

    private static CssColor Background(DomDocument document, DomElement element, params CssStyleSource[] sources) =>
        (CssColor)CssStyleEngine.Compute(document, sources, includeUserAgent: false,
            cancellationToken: Cancellation).Styles[element]["background-color"];
}
