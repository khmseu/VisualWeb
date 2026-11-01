using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class FontColorHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("red", 255, 0, 0)]
    [InlineData(" \tReBeccAPurple\r\n", 102, 51, 153)]
    [InlineData("#aBc", 170, 187, 204)]
    [InlineData("#123456", 18, 52, 86)]
    [InlineData("chucknorris", 192, 0, 0)]
    [InlineData("currentcolor", 192, 224, 0)]
    [InlineData("#1234", 18, 52, 0)]
    [InlineData("#12345678", 18, 69, 120)]
    [InlineData("rgb(255,0,0)", 0, 37, 0)]
    [InlineData("  ", 0, 0, 0)]
    [InlineData("\u00a0red\u00a0", 0, 237, 0)]
    [InlineData("#", 0, 0, 0)]
    [InlineData("\U0001f600f00", 0, 240, 0)]
    public void ParsesLegacyColor(string input, byte red, byte green, byte blue)
    {
        var (document, font) = Create();
        font.SetAttribute("color", input);
        Assert.Equal(new CssColor(red, green, blue), Color(document, font));
    }

    [Fact]
    public void LoneSurrogatesAreReplacedAsNonHexCharacters()
    {
        var (document, font) = Create();
        font.SetAttribute("color", string.Concat('\ud800', "f00"));
        Assert.Equal(new CssColor(15, 0, 0), Color(document, font));
        font.SetAttribute("color", string.Concat('\udc00', "f00"));
        Assert.Equal(new CssColor(15, 0, 0), Color(document, font));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" transparent ")]
    [InlineData("TrAnSpArEnT")]
    public void FailureInherits(string input)
    {
        var (document, font) = Create();
        font.SetAttribute("color", input);
        Assert.Equal(new CssColor(18, 52, 86), Color(document, font));
    }

    [Fact]
    public void TruncationAndLeadingZeroRules()
    {
        var (document, font) = Create();
        font.SetAttribute("color", new string('0', 126) + "ff" + "ffff");
        Assert.Equal(new CssColor(0, 0, 255), Color(document, font));
        font.SetAttribute("color", new string('0', 127) + "\U0001f600ff");
        Assert.Equal(new CssColor(0, 0, 0), Color(document, font));
        font.SetAttribute("color", "000000001000000002000000003");
        Assert.Equal(new CssColor(1, 2, 3), Color(document, font));
    }

    [Fact]
    public void CascadeInheritanceMutationAndUnsupportedAttributes()
    {
        var (document, font) = Create();
        font.SetAttribute("color", "red");
        font.SetAttribute("face", "monospace");
        font.SetAttribute("size", "7");
        var child = document.CreateElement("span");
        font.AppendChild(child);
        var plain = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssColor(255, 0, 0), plain.Styles[child]["color"]);
        Assert.Equal(plain.Styles[font.ParentNode as DomElement ?? throw new InvalidOperationException()]["font-family"], plain.Styles[font]["font-family"]);
        Assert.Equal(new CssLength(48, "px"), plain.Styles[font]["font-size"]);
        Assert.Equal(new CssColor(255, 0, 0), Color(document, font, new CssStyleSource("font{color:blue}", CssOrigin.User)));
        Assert.Equal(new CssColor(255, 0, 0), Color(document, font, new CssStyleSource("font{color:blue}", CssOrigin.UserAgent)));
        Assert.Equal(new CssColor(0, 128, 0), Color(document, font, new CssStyleSource("font{color:green}")));
        Assert.Equal(new CssColor(0, 0, 255), Color(document, font, new("font{color:blue}", CssOrigin.User), new("font{color:revert}")));
        Assert.Equal(new CssColor(0, 0, 255), Color(document, font, new("font{color:blue!important}", CssOrigin.User), new("font{color:red!important}")));
        Assert.Equal(new CssColor(0, 0, 255), Color(document, font, new("font{color:blue!important}", CssOrigin.UserAgent), new("font{color:red!important}")));
        font.SetAttribute("style", "color:green");
        Assert.Equal(new CssColor(0, 128, 0), Color(document, font));
        font.SetAttribute("style", "color:revert");
        Assert.Equal(new CssColor(18, 52, 86), Color(document, font));
        font.SetAttribute("style", "");
        font.SetAttribute("color", "#abc");
        Assert.Equal(new CssColor(170, 187, 204), Color(document, font));
        font.RemoveAttribute("color");
        Assert.Equal(new CssColor(18, 52, 86), Color(document, font));
    }

    [Fact]
    public void AggregateCharacterAndAssignmentLimitsAreShared()
    {
        var (document, font) = Create();
        font.SetAttribute("color", "red");
        var other = document.CreateElement("font");
        other.SetAttribute("color", "blue");
        font.AppendChild(other);
        CssStyleResult Calculate(int chars, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new() { MaxFontColorAttributeCharacters = chars, MaxAssignments = assignments },
                cancellationToken: Cancellation);
        Assert.Equal(new CssColor(0, 0, 255), Calculate(7, 3).Styles[other]["color"]);
        Assert.Throws<CssLimitException>(() => Calculate(6, 3));
        Assert.Throws<CssLimitException>(() => Calculate(7, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 3));
        other.SetAttribute("color", "");
        Assert.Equal(new CssColor(255, 0, 0), Calculate(3, 2).Styles[other]["color"]);
        other.SetAttribute("color", "transparent");
        Assert.Equal(new CssColor(255, 0, 0), Calculate(14, 2).Styles[other]["color"]);
        Assert.Throws<CssLimitException>(() => Calculate(13, 2));
        other.SetAttribute("color", new string(' ', 100_000));
        Assert.Throws<CssLimitException>(() => Calculate(100_002, 2));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(document, cancellationToken: canceled.Token));
    }

    private static (DomDocument Document, DomElement Font) Create()
    {
        var document = HtmlParser.Parse("<div style='color:#123456'><font id=f></font></div>",
            cancellationToken: Cancellation).Document;
        return (document, document.GetElementById("f")!);
    }

    private static CssValue Color(DomDocument document, DomElement font, params CssStyleSource[] sources) =>
        CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation).Styles[font]["color"];
}
