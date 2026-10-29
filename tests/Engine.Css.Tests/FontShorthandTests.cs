using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class FontShorthandTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Reset = "font-style:normal;font-weight:400;font-size:16px;line-height:normal;font-family:serif;";
    private const string Previous = "font-style:italic;font-weight:700;font-size:13px;line-height:2;font-family:Previous;";
    private static readonly string[] Properties = ["font-style", "font-weight", "font-size", "line-height", "font-family"];

    [Theory]
    [InlineData("16px serif", "")]
    [InlineData("italic 16px serif", "font-style:italic")]
    [InlineData("bold 16px serif", "font-weight:700")]
    [InlineData("italic bold 16px/24px serif", "font-style:italic;font-weight:700;line-height:24px")]
    [InlineData("700 italic 16px / 1.5 serif", "font-style:italic;font-weight:700;line-height:1.5")]
    [InlineData("normal 16px serif", "")]
    [InlineData("normal normal normal normal 16px serif", "")]
    [InlineData("normal italic normal bold 16px serif", "font-style:italic;font-weight:700")]
    [InlineData("oblique 16px serif", "font-style:oblique")]
    [InlineData("bolder larger/normal serif", "font-weight:bolder;font-size:larger")]
    [InlineData("lighter .75em/150% serif", "font-weight:lighter;font-size:.75em;line-height:150%")]
    [InlineData("125%/150% \"Open Sans\", serif", "font-size:125%;line-height:150%;font-family:\"Open Sans\",serif")]
    [InlineData("medium monospace", "font-family:monospace")]
    [InlineData("0/0 sans-serif", "font-size:0;line-height:0;font-family:sans-serif")]
    [InlineData("ITALIC BOLD 1rem/2 serif", "font-style:italic;font-weight:700;font-size:1rem;line-height:2")]
    [InlineData("16px/1 Open Sans, sans-serif", "line-height:1;font-family:Open Sans,sans-serif")]
    public void ShorthandMatchesExistingLonghandsAndResetsOmittedFields(string shorthand, string overrides)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var element = document.GetElementById("p")!;
        var actual = CssStyleEngine.Compute(document, [new("p{" + Previous + "font:" + shorthand + "}")], cancellationToken: Cancellation);
        var expected = CssStyleEngine.Compute(document, [new("p{" + Reset + overrides + "}")], cancellationToken: Cancellation);
        Assert.Empty(actual.Diagnostics);
        Assert.Empty(expected.Diagnostics);
        EqualFonts(expected.Styles[element], actual.Styles[element]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("serif")]
    [InlineData("16px")]
    [InlineData("bold 16px")]
    [InlineData("italic italic 16px serif")]
    [InlineData("bold 700 16px serif")]
    [InlineData("normal normal normal normal normal 16px serif")]
    [InlineData("small-caps 16px serif")]
    [InlineData("condensed 16px serif")]
    [InlineData("75% 16px serif")]
    [InlineData("oblique 15deg 16px serif")]
    [InlineData("16px / serif")]
    [InlineData("16px / -1 serif")]
    [InlineData("16px / 1 / 2 serif")]
    [InlineData("-16px serif")]
    [InlineData("inherit 16px serif")]
    [InlineData("16px serif,")]
    [InlineData("16px ,serif")]
    [InlineData("caption")]
    [InlineData("menu")]
    [InlineData("status-bar")]
    [InlineData("calc(16px) serif")]
    public void InvalidOrUnsupportedFontIsAnAtomicWholeDeclarationDiagnostic(string shorthand)
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var element = document.GetElementById("p")!;
        var actual = CssStyleEngine.Compute(document, [new("p{" + Previous + "font:" + shorthand + "}")], cancellationToken: Cancellation);
        var expected = CssStyleEngine.Compute(document, [new("p{" + Previous + "}")], cancellationToken: Cancellation);
        Assert.NotEmpty(actual.Diagnostics);
        EqualFonts(expected.Styles[element], actual.Styles[element]);
    }

    [Theory]
    [InlineData("initial", false)]
    [InlineData("inherit", true)]
    [InlineData("unset", true)]
    [InlineData("revert", true)]
    public void WideKeywordsApplyToEverySupportedInheritedFontLonghand(string keyword, bool inherited)
    {
        var document = HtmlParser.Parse("<!doctype html><div id=parent><p id=p>Text</p></div>", cancellationToken: Cancellation).Document;
        var element = document.GetElementById("p")!;
        var source = "div{" + Previous + "}p{font:10px sans-serif;font:" + keyword + "}";
        var actual = CssStyleEngine.Compute(document, [new(source)], cancellationToken: Cancellation);
        var expected = CssStyleEngine.Compute(document, [new("div{" + Previous + "}p{" + (inherited ? "" : Reset) + "}")],
            cancellationToken: Cancellation);
        Assert.Empty(actual.Diagnostics);
        EqualFonts(expected.Styles[element], actual.Styles[element]);
    }

    [Fact]
    public void ShorthandUsesExactlyFiveAssignmentsAndInvalidFontsUseNone()
    {
        var document = HtmlParser.Parse("<!doctype html><p>Text</p>", cancellationToken: Cancellation).Document;
        var options = new CssOptions { MaxAssignments = 5 };
        Assert.Empty(CssStyleEngine.Compute(document, [new("p{font:16px serif}")], options: options,
            includeUserAgent: false, cancellationToken: Cancellation).Diagnostics);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document, [new("p{font:16px serif}")],
            options: new() { MaxAssignments = 4 }, includeUserAgent: false, cancellationToken: Cancellation));
        var invalid = CssStyleEngine.Compute(document, [new("p{font:small-caps 12px serif;font:16px serif}")],
            options: options, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Single(invalid.Diagnostics);
    }

    [Fact]
    public void ShorthandPreservesInlineImportanceAndLaterLonghandOrdering()
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p style='font:italic 700 20px/2 serif!important;font-size:12px'>Text</p>",
            cancellationToken: Cancellation).Document;
        var result = CssStyleEngine.Compute(document, [new("p{font:10px monospace!important}")], cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        var style = result.Styles[document.GetElementById("p")!];
        Assert.Equal(new CssLength(20, "px"), style["font-size"]);
        Assert.Equal(new CssNumber(700), style["font-weight"]);
        Assert.Equal(new CssKeyword("italic"), style["font-style"]);
        Assert.Equal(new CssNumber(2), style["line-height"]);
        var ordered = CssStyleEngine.Compute(document,
            [new("p{font:16px serif!important;font-size:24px!important}")], cancellationToken: Cancellation);
        Assert.Equal(new CssLength(20, "px"), ordered.Styles[document.GetElementById("p")!]["font-size"]);
        var ordinary = HtmlParser.Parse("<!doctype html><p id=p>Text</p>", cancellationToken: Cancellation).Document;
        var later = CssStyleEngine.Compute(ordinary, [new("p{font:16px serif;font-size:24px}")], cancellationToken: Cancellation);
        Assert.Equal(new CssLength(24, "px"), later.Styles[ordinary.GetElementById("p")!]["font-size"]);
    }

    private static void EqualFonts(CssComputedStyle expected, CssComputedStyle actual)
    {
        foreach (var property in Properties.Where(property => property != "font-family"))
        { Assert.Equal(expected[property], actual[property]); }
        Assert.Equal(Assert.IsType<CssFontFamilies>(expected["font-family"]).Names,
            Assert.IsType<CssFontFamilies>(actual["font-family"]).Names);
    }
}
