using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class FontFaceHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("serif", "serif")]
    [InlineData("Missing, monospace", "Missing|monospace")]
    [InlineData("Noto Sans, sans-serif", "Noto Sans|sans-serif")]
    [InlineData("'Family, Name', serif", "Family, Name|serif")]
    public void ParsesFontFamilyLists(string input, string expected)
    {
        var (document, font) = Create();
        font.SetAttribute("face", input);

        Assert.Equal(expected.Split('|'), Families(Compute(document), font));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("serif,,monospace")]
    [InlineData("serif,")]
    [InlineData("serif !important")]
    [InlineData("url(example)")]
    [InlineData("serif; color:red")]
    public void InvalidValuesDoNotCreateHints(string input)
    {
        var (document, font) = Create();
        font.SetAttribute("face", input);

        var result = Compute(document);

        Assert.Equal(["serif"], Families(result, font));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void CascadeInheritanceAndMutationAreRecomputed()
    {
        var (document, font, child) = CreateWithChild();
        font.SetAttribute("face", "monospace");
        var hinted = Compute(document);
        Assert.Equal(["serif"], Families(hinted, (DomElement)font.ParentNode!));
        Assert.Equal(["monospace"], Families(hinted, font));
        Assert.Equal(["monospace"], Families(hinted, child));

        var authored = Compute(document, new CssStyleSource("font{font-family:cursive}"));
        Assert.Equal(["cursive"], Families(authored, font));
        var important = Compute(document, new CssStyleSource("font{font-family:cursive!important}"));
        Assert.Equal(["cursive"], Families(important, font));

        font.SetAttribute("style", "font-family:system-ui");
        Assert.Equal(["system-ui"], Families(Compute(document), font));
        font.RemoveAttribute("style");
        font.SetAttribute("face", "sans-serif");
        Assert.Equal(["sans-serif"], Families(Compute(document), child));
        font.RemoveAttribute("face");
        Assert.Equal(["serif"], Families(Compute(document), child));
    }

    [Fact]
    public void AggregateRawAttributeAndAssignmentLimitsAreIndependent()
    {
        var (document, font) = Create();
        font.SetAttribute("face", "serif");
        var other = document.CreateElement("font");
        other.SetAttribute("face", "monospace");
        font.AppendChild(other);

        CssStyleResult Calculate(int characters, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new() { MaxFontFaceAttributeCharacters = characters, MaxAssignments = assignments },
                cancellationToken: Cancellation);

        Assert.Equal(["monospace"], Families(Calculate(14, 2), other));
        Assert.Throws<CssLimitException>(() => Calculate(13, 2));
        Assert.Throws<CssLimitException>(() => Calculate(14, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 2));

        font.SetAttribute("face", "bad,,");
        other.RemoveAttribute("face");
        Assert.Equal(["serif"], Families(Calculate(5, 1), font));
        Assert.Throws<CssLimitException>(() => Calculate(4, 1));

        font.SetAttribute("face", "\U0001f600");
        Assert.NotNull(Calculate(2, 1));
        Assert.Throws<CssLimitException>(() => Calculate(1, 1));

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            CssStyleEngine.Compute(document, cancellationToken: canceled.Token));
    }

    private static (DomDocument Document, DomElement Font) Create()
    {
        var document = HtmlParser.Parse(
            "<div><font id=f></font></div>",
            cancellationToken: Cancellation).Document;
        return (document, document.GetElementById("f")!);
    }

    private static (DomDocument Document, DomElement Font, DomElement Child) CreateWithChild()
    {
        var (document, font) = Create();
        var child = document.CreateElement("span");
        font.AppendChild(child);
        return (document, font, child);
    }

    private static CssStyleResult Compute(DomDocument document, params CssStyleSource[] sources) =>
        CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation);

    private static IReadOnlyList<string> Families(CssStyleResult result, DomElement element) =>
        Assert.IsType<CssFontFamilies>(result.Styles[element]["font-family"]).Names;
}
