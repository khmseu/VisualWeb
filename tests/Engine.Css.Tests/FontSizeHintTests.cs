using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class FontSizeHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("1", 10)]
    [InlineData("2", 13)]
    [InlineData("3", 16)]
    [InlineData("4", 18)]
    [InlineData("5", 24)]
    [InlineData("6", 32)]
    [InlineData("7", 48)]
    [InlineData("0", 10)]
    [InlineData("999999999999999999999999999999", 48)]
    [InlineData("+0", 16)]
    [InlineData("-0", 16)]
    [InlineData("+1", 18)]
    [InlineData("-1", 13)]
    [InlineData("+999999999999999999999", 48)]
    [InlineData("-999999999999999999999", 10)]
    [InlineData(" \t\r\n\f+00004suffix", 48)]
    [InlineData("0000002.9", 13)]
    [InlineData("3-8", 16)]
    public void ParsesLegacySize(string input, int pixels)
    {
        var (document, font) = Create();
        font.SetAttribute("size", input);
        Assert.Equal(new CssLength(pixels, "px"), Size(document, font));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("+-1")]
    [InlineData("--1")]
    [InlineData("+ 1")]
    [InlineData("\u00a01")]
    [InlineData("\u0661")]
    public void InvalidInherits(string input)
    {
        var (document, font) = Create();
        font.SetAttribute("size", input);
        Assert.Equal(new CssLength(23, "px"), Size(document, font));
    }

    [Fact]
    public void LongLeadingZeroesAreNotSaturated()
    {
        var (document, font) = Create();
        font.SetAttribute("size", "+" + new string('0', 100_000) + "1");
        Assert.Equal(new CssLength(18, "px"), Size(document, font));
        font.SetAttribute("size", new string(' ', 100_000) + "-00000000000000000000000002tail");
        Assert.Equal(new CssLength(10, "px"), Size(document, font));
    }

    [Fact]
    public void CascadeInheritanceMutationAndColor()
    {
        var (document, font) = Create();
        font.SetAttribute("size", "+1");
        font.SetAttribute("color", "red");
        font.SetAttribute("face", "monospace");
        var child = document.CreateElement("span");
        font.AppendChild(child);
        var plain = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssLength(18, "px"), plain.Styles[child]["font-size"]);
        Assert.Equal(new CssColor(255, 0, 0), plain.Styles[child]["color"]);
        Assert.Equal(new CssLength(18, "px"), CssStyleEngine.Compute(document, cancellationToken: Cancellation).Styles[child]["font-size"]);
        Assert.Equal(["serif"], Assert.IsType<CssFontFamilies>(plain.Styles[(DomElement)font.ParentNode!]["font-family"]).Names);
        Assert.Equal(["monospace"], Assert.IsType<CssFontFamilies>(plain.Styles[font]["font-family"]).Names);
        Assert.Equal(["monospace"], Assert.IsType<CssFontFamilies>(plain.Styles[child]["font-family"]).Names);
        Assert.Equal(new CssLength(18, "px"), Size(document, font, new CssStyleSource("font{font-size:30px}", CssOrigin.User)));
        Assert.Equal(new CssLength(18, "px"), Size(document, font, new CssStyleSource("font{font-size:30px}", CssOrigin.UserAgent)));
        Assert.Equal(new CssLength(31, "px"), Size(document, font, new CssStyleSource("font{font-size:31px}")));
        Assert.Equal(new CssLength(30, "px"), Size(document, font, new("font{font-size:30px}", CssOrigin.User), new("font{font-size:revert}")));
        Assert.Equal(new CssLength(30, "px"), Size(document, font, new("font{font-size:30px!important}", CssOrigin.User), new("font{font-size:31px!important}")));
        Assert.Equal(new CssLength(30, "px"), Size(document, font, new("font{font-size:30px!important}", CssOrigin.UserAgent), new("font{font-size:31px!important}")));
        font.SetAttribute("style", "font-size:31px");
        Assert.Equal(new CssLength(31, "px"), Size(document, font));
        Assert.Equal(new CssLength(30, "px"), Size(document, font, new CssStyleSource("font{font-size:30px!important}")));
        font.SetAttribute("style", "font-size:31px!important");
        Assert.Equal(new CssLength(31, "px"), Size(document, font, new CssStyleSource("font{font-size:30px!important}")));
        Assert.Equal(new CssLength(30, "px"), Size(document, font, new CssStyleSource("font{font-size:30px!important}", CssOrigin.User)));
        font.SetAttribute("style", "font-size:revert");
        Assert.Equal(new CssLength(23, "px"), Size(document, font));
        font.RemoveAttribute("style");
        font.SetAttribute("size", "-1");
        Assert.Equal(new CssLength(13, "px"), Size(document, font));
        font.RemoveAttribute("size");
        Assert.Equal(new CssLength(23, "px"), Size(document, font));
    }

    [Fact]
    public void IndependentAggregateAndSharedAssignmentLimits()
    {
        var (document, font) = Create();
        font.SetAttribute("size", "3");
        font.SetAttribute("color", "red");
        var other = document.CreateElement("font");
        other.SetAttribute("size", "+1");
        font.AppendChild(other);
        CssStyleResult Calculate(int sizes, int colors, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new() { MaxFontSizeAttributeCharacters = sizes, MaxFontColorAttributeCharacters = colors, MaxAssignments = assignments },
                cancellationToken: Cancellation);
        Assert.Equal(new CssLength(18, "px"), Calculate(3, 3, 4).Styles[other]["font-size"]);
        Assert.Throws<CssLimitException>(() => Calculate(2, 3, 4));
        Assert.Throws<CssLimitException>(() => Calculate(3, 2, 4));
        Assert.Throws<CssLimitException>(() => Calculate(3, 3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 3, 4));
        other.SetAttribute("size", "bad");
        Assert.Equal(new CssLength(16, "px"), Calculate(4, 3, 3).Styles[other]["font-size"]);
        Assert.Throws<CssLimitException>(() => Calculate(3, 3, 3));
        other.SetAttribute("size", "");
        Assert.Equal(new CssLength(16, "px"), Calculate(1, 3, 3).Styles[other]["font-size"]);
        other.SetAttribute("size", "\U0001f600");
        Assert.Equal(new CssLength(16, "px"), Calculate(3, 3, 3).Styles[other]["font-size"]);
        Assert.Throws<CssLimitException>(() => Calculate(2, 3, 3));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(document, cancellationToken: canceled.Token));
    }

    private static (DomDocument Document, DomElement Font) Create()
    {
        var document = HtmlParser.Parse("<div style='font-size:23px'><font id=f></font></div>",
            cancellationToken: Cancellation).Document;
        return (document, document.GetElementById("f")!);
    }

    private static CssValue Size(DomDocument document, DomElement font, params CssStyleSource[] sources) =>
        CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation).Styles[font]["font-size"];
}
