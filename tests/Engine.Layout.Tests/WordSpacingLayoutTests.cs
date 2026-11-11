using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class WordSpacingLayoutTests
{
    [Fact]
    public void WordSpacingChangesCollapsedSpaceAdvanceAndCanAffectWrapping()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><p id=spaced>aa bb</p>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}p{margin:0;font-size:10px;word-spacing:6px}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 30, 200,
            cancellationToken: cancellation).Root!;
        var box = Find(root, "spaced")!;

        Assert.Equal(2, box.Lines.Count);
        Assert.Equal("aabb", string.Concat(box.Lines.SelectMany(line => line.Fragments)
            .Select(fragment => fragment.Run.Text)));
        Assert.Equal("aa", string.Concat(box.Lines[0].Fragments.Select(fragment => fragment.Run.Text)));
        Assert.Equal("bb", string.Concat(box.Lines[1].Fragments.Select(fragment => fragment.Run.Text)));
        Assert.Equal("aa bb", document.GetElementById("spaced")!.TextContent);
    }

    [Fact]
    public void WordSpacingExpandsEachPreservedAsciiSpace()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><p id=preserved>a  b</p>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}p{margin:0;font-size:10px;white-space:pre;word-spacing:2px}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 200,
            cancellationToken: cancellation).Root!;
        var box = Find(root, "preserved")!;
        var line = Assert.Single(box.Lines);

        Assert.Equal(new[] { 5d, 7d, 7d, 5d }, line.Fragments.Select(fragment => fragment.Run.Width));
        Assert.Equal(24, line.Fragments.Sum(fragment => fragment.Run.Width));
        Assert.Equal("a  b", document.GetElementById("preserved")!.TextContent);
    }

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

    private static LayoutBox? Find(LayoutBox box, string id)
    {
        if (box.Element.GetAttribute("id") == id) { return box; }
        foreach (var child in box.Children)
        {
            if (Find(child, id) is { } found) { return found; }
        }
        return null;
    }
}
