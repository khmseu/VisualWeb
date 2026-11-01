using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class SearchDefaultsTests
{
    [Fact]
    public void SearchClosesParagraphAndLaysOutOrdinaryBlockChildren()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><p id=p>one<search id=s><div id=d>two</div><form id=f><div id=child>three</div></form></search><div id=after>end</div>", cancellationToken: cancellation).Document;
        var body = document.Body!;
        Assert.Equal(new[] { "p", "search", "div" }, body.ChildNodes.OfType<DomElement>().Select(x => x.LocalName));
        var search = document.GetElementById("s")!;
        Assert.Equal(new[] { "div", "form" }, search.ChildNodes.OfType<DomElement>().Select(x => x.LocalName));
        var styles = CssStyleEngine.Compute(document, [new("*{margin:0;font-size:10px;line-height:10px}")], cancellationToken: cancellation);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200, cancellationToken: cancellation).Root!;
        Assert.Equal(new LayoutRect(0, 0, 120, 10), Find(root, "p")!.Content);
        Assert.Equal(new LayoutRect(0, 10, 120, 20), Find(root, "s")!.Content);
        Assert.Equal(new LayoutRect(0, 10, 120, 10), Find(root, "d")!.Content);
        Assert.Equal(new LayoutRect(0, 20, 120, 10), Find(root, "child")!.Content);
        Assert.Equal(new LayoutRect(0, 30, 120, 10), Find(root, "after")!.Content);
    }

    [Fact]
    public void AuthorInlineWithBlockDescendantStillFailsExactly()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><search style='display:inline'><div>text</div></search>", cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: cancellation);
        var exception = Assert.Throws<UnsupportedLayoutException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200, cancellationToken: cancellation));
        Assert.Equal("Inline-block or block descendants inside inline elements are unsupported.", exception.Message);
    }

    private static LayoutBox? Find(LayoutBox root, string id) => root.Element.GetAttribute("id") == id ? root
        : root.Children.Select(child => Find(child, id)).FirstOrDefault(found => found is not null);

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
}
