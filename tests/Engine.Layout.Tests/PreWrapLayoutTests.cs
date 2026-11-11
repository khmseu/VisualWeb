using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class PreWrapLayoutTests
{
    [Fact]
    public void PreWrapPreservesSpaceRunsAndWrapsAfterTheRun()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>aa   bb</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:pre-wrap;text-align:right}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 12, 100,
            cancellationToken: cancellation).Root!;
        var box = Find(root, "box")!;

        Assert.Equal(2, box.Lines.Count);
        Assert.Equal(new[] { "aa   ", "bb" }, box.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
        Assert.Equal(2, box.Lines[0].Fragments[0].X);
        Assert.Equal(new[] { 12d, 17d, 22d }, box.Lines[0].Fragments.Skip(1).Select(fragment => fragment.X));
        Assert.Equal(2, box.Lines[1].Fragments[0].X);
        Assert.Equal("aa   bb", document.GetElementById("box")!.TextContent);
    }

    [Fact]
    public void PreWrapHangsTrailingSpacesWhenAligningTheFinalLine()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a  </div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:pre-wrap;text-align:right;word-spacing:2px}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 12, 100,
            cancellationToken: cancellation).Root!;
        var line = Assert.Single(Find(root, "box")!.Lines);

        Assert.Equal(new[] { "a", " ", " " }, line.Fragments.Select(fragment => fragment.Run.Text));
        Assert.Equal(new[] { 7d, 12d, 19d }, line.Fragments.Select(fragment => fragment.X));
        Assert.Equal(new[] { 5d, 7d, 7d }, line.Fragments.Select(fragment => fragment.Run.Width));
        Assert.Equal("a  ", document.GetElementById("box")!.TextContent);
    }

    [Fact]
    public void PreWrapRetainsHyphenBreaksAfterPreservedSpaceRuns()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a  good-day</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:pre-wrap}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 40, 100,
            cancellationToken: cancellation).Root!, "box")!;

        Assert.Equal(2, box.Lines.Count);
        Assert.Equal(new[] { "a  good-", "day" }, box.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
    }

    [Fact]
    public void PreWrapPreservesNewlinesAsForcedBreaks()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a\nb</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;white-space:pre-wrap}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100,
            cancellationToken: cancellation).Root!;
        var box = Find(root, "box")!;

        Assert.Equal(new[] { "a", "b" }, box.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
        Assert.Equal("a\nb", document.GetElementById("box")!.TextContent);
    }

    [Theory]
    [InlineData("a \u00a0b")]
    [InlineData("a \u035cb")]
    public void PreWrapRejectsSpaceRunsAdjacentToUnicodeGlue(string content)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse($"<!doctype html><div id=box>{content}</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;white-space:pre-wrap}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var exception = Assert.Throws<UnsupportedLayoutException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100,
                cancellationToken: cancellation));

        Assert.Contains("Unicode glue", exception.Message);
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
