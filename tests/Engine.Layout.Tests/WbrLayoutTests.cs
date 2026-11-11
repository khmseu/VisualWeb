using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class WbrLayoutTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("normal")]
    [InlineData("pre-line")]
    [InlineData("pre-wrap")]
    [InlineData("break-spaces")]
    public void WbrCreatesAnOptionalWrapPointBetweenTextNodes(string whiteSpace)
    {
        var (document, styles) = Page("foo<wbr>bar", $"#p{{white-space:{whiteSpace}}}");
        var paragraph = document.GetElementById("p")!;
        var sources = paragraph.ChildNodes.OfType<DomText>().ToArray();
        var box = Layout(document, styles, 15);

        Assert.Equal(["foo", "bar"], DescribeLines(box));
        Assert.Equal(2, sources.Length);
        Assert.Same(sources[0], Assert.Single(box.Lines[0].Fragments).Source);
        Assert.Same(sources[1], Assert.Single(box.Lines[1].Fragments).Source);
        Assert.Equal(0, Assert.Single(box.Lines[0].Fragments).X);
        Assert.Equal(0, Assert.Single(box.Lines[1].Fragments).X);
    }

    [Fact]
    public void WbrKeepsAnUnbrokenWordShapedAcrossItsSourceBoundary()
    {
        var (document, styles) = Page("ab<wbr>cd");
        var shaper = new KerningShaper();
        var box = Layout(document, styles, 18, shaper);
        var line = Assert.Single(box.Lines);

        Assert.Equal("abcd", Assert.Single(shaper.ShapedTexts, text => text == "abcd"));
        Assert.DoesNotContain("ab", shaper.ShapedTexts);
        Assert.DoesNotContain("cd", shaper.ShapedTexts);
        Assert.Equal(new[] { "ab", "cd" }, line.Fragments.Select(fragment => fragment.Run.Text));
        Assert.Equal(new[] { 0d, 8d }, line.Fragments.Select(fragment => fragment.X));
        Assert.Equal(new[] { 8d, 10d }, line.Fragments.Select(fragment => fragment.Run.Width));
        Assert.Equal(18, line.Fragments.Sum(fragment => fragment.Run.Width));
        Assert.Equal(2, document.GetElementById("p")!.ChildNodes.OfType<DomText>().Count());
    }

    [Theory]
    [InlineData("nowrap")]
    [InlineData("pre")]
    public void WbrDoesNotWrapInNonWrappingWhitespaceModes(string whiteSpace)
    {
        var (document, styles) = Page("foo<wbr>bar", $"#p{{white-space:{whiteSpace}}}");
        var box = Layout(document, styles, 5);

        Assert.Equal(["foobar"], DescribeLines(box));
        Assert.Equal(2, Assert.Single(box.Lines).Fragments.Count);
    }

    [Theory]
    [InlineData("nowrap")]
    [InlineData("pre")]
    public void WbrDoesNotEnableHyphenBreakingInNonWrappingWhitespaceModes(string whiteSpace)
    {
        var (document, styles) = Page("foo<wbr>bar-baz", $"#p{{white-space:{whiteSpace}}}");
        var box = Layout(document, styles, 5);

        Assert.Equal(["foobar-baz"], DescribeLines(box));
    }

    [Fact]
    public void WbrAtLineEdgesDoesNotCreateEmptyLines()
    {
        var (document, styles) = Page("<wbr>foo<wbr>");
        var box = Layout(document, styles, 15);

        Assert.Equal(["foo"], DescribeLines(box));
        Assert.Equal(2, document.GetElementById("p")!.ChildNodes.OfType<DomElement>()
            .Count(element => element.LocalName == "wbr"));
    }

    [Fact]
    public void MultipleWbrOpportunitiesWrapOneWordAcrossSeveralLines()
    {
        var (document, styles) = Page("ab<wbr>cd<wbr>ef");
        var box = Layout(document, styles, 10);

        Assert.Equal(["ab", "cd", "ef"], DescribeLines(box));
        Assert.Equal(3, box.Lines.SelectMany(line => line.Fragments).Select(fragment => fragment.Source)
            .Distinct().Count());
    }

    [Fact]
    public void WbrAndSupportedHyphenOpportunitiesShareGreedyLineBreaking()
    {
        var (document, styles) = Page("foo<wbr>bar-baz");
        var box = Layout(document, styles, 25);

        Assert.Equal(["foo", "bar-", "baz"], DescribeLines(box));
        Assert.Same(document.GetElementById("p")!.ChildNodes.OfType<DomText>().First(),
            Assert.Single(box.Lines[0].Fragments).Source);
        Assert.All(box.Lines.Skip(1).SelectMany(line => line.Fragments), fragment =>
            Assert.Same(document.GetElementById("p")!.ChildNodes.OfType<DomText>().Last(), fragment.Source));
    }

    [Fact]
    public void WbrContributesToAutomaticTableMinimumContentWidth()
    {
        var broken = TableWidth("a<wbr>b");
        var unbroken = TableWidth("ab");

        Assert.Equal(11, broken);
        Assert.Equal(16, unbroken);
    }

    [Fact]
    public void WbrRejectsAStyleBoundaryThatCannotBeShapedAsOneWord()
    {
        var (document, styles) = Page("foo<wbr><span style='font-weight:bold'>bar</span>");

        Assert.Throws<UnsupportedLayoutException>(() => Layout(document, styles, 100));
    }

    [Fact]
    public void WbrRejectsAClusterThatCannotBeAssignedToOneSourceTextNode()
    {
        var (document, styles) = Page("f<wbr>i");

        Assert.Throws<UnsupportedLayoutException>(() => Layout(document, styles, 20, new LigatureShaper()));
    }

    [Fact]
    public void WbrCoalescingUsesTheSharedLineBreakWorkBudget()
    {
        var (document, styles) = Page("foo<wbr>bar");

        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 100,
            options: new() { MaxLineBreakOperations = 5 }));
    }

    private static (DomDocument Document, CssStyleResult Styles) Page(string content, string css = "")
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>" + content + "</p>",
            cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,p{display:block}head{display:none}*{margin:0;font-size:10px;line-height:10px}" + css)],
            includeUserAgent: false, cancellationToken: Cancellation);
        return (document, styles);
    }

    private static LayoutBox Layout(DomDocument document, CssStyleResult styles, double width,
        ITextShaper? shaper = null, LayoutOptions? options = null)
    {
        var root = StaticLayout.Layout(document, styles, shaper ?? new MetricsShaper(), width, 200, options,
            cancellationToken: Cancellation).Root!;
        return Find(root, "p");
    }

    private static double TableWidth(string content)
    {
        var document = HtmlParser.Parse(
            "<!doctype html><table id=t><tr><td>" + content + "</td></tr></table>",
            cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("*{margin:0;font-size:10px;line-height:10px}")], cancellationToken: Cancellation);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 5, 100,
            cancellationToken: Cancellation).Root!;
        return Find(root, "t").Content.Width;
    }

    private static LayoutBox Find(LayoutBox root, string id) => TryFind(root, id)
        ?? throw new InvalidOperationException("Missing layout box " + id);

    private static LayoutBox? TryFind(LayoutBox root, string id) => root.Element.GetAttribute("id") == id ? root
        : root.Children.Select(child => TryFind(child, id)).FirstOrDefault(found => found is not null);

    private static string[] DescribeLines(LayoutBox box) => box.Lines.Select(line => string.Concat(
        line.Fragments.OrderBy(fragment => fragment.X).Select(fragment => fragment.Run.Text))).ToArray();

    private sealed class MetricsShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyphs = text.Select((character, index) => new ShapedGlyph(character, index, request.Size / 2, 0, 0))
                .ToList().AsReadOnly();
            return new(text, "deterministic", request.Size, glyphs, text.Length * request.Size / 2,
                new(request.Size * .8, request.Size * .2, 0));
        }
    }

    private sealed class KerningShaper : ITextShaper
    {
        public List<string> ShapedTexts { get; } = [];

        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShapedTexts.Add(text);
            var glyphs = text.Select((character, index) => new ShapedGlyph(character, index,
                text[index] == 'b' && index + 1 < text.Length && text[index + 1] == 'c'
                    ? 3 : request.Size / 2, 0, 0)).ToList().AsReadOnly();
            return new(text, "kerning-test", request.Size, glyphs, glyphs.Sum(glyph => glyph.Advance),
                new(request.Size * .8, request.Size * .2, 0));
        }
    }

    private sealed class LigatureShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyphs = text == "fi"
                ? Array.AsReadOnly([new ShapedGlyph(100, 0, request.Size, 0, 0)])
                : text.Select((character, index) => new ShapedGlyph(character, index, request.Size / 2, 0, 0))
                    .ToList().AsReadOnly();
            return new(text, "ligature-test", request.Size, glyphs, glyphs.Sum(glyph => glyph.Advance),
                new(request.Size * .8, request.Size * .2, 0));
        }
    }
}
