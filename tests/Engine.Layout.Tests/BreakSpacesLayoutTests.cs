using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class BreakSpacesLayoutTests
{
    [Fact]
    public void BreakSpacesWrapBetweenAdjacentPreservedSpaces()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a   b</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:break-spaces}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 12, 100,
            cancellationToken: cancellation).Root!, "box")!;

        Assert.Equal(new[] { "a ", "  ", "b" }, box.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
    }

    [Fact]
    public void BreakSpacesTrailingSpacesContributeToRightAlignment()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a  </div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:break-spaces;text-align:right}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var line = Assert.Single(Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 20, 100,
            cancellationToken: cancellation).Root!, "box")!.Lines);

        Assert.Equal(new[] { "a", " ", " " }, line.Fragments.Select(fragment => fragment.Run.Text));
        Assert.Equal(new[] { 5d, 10d, 15d }, line.Fragments.Select(fragment => fragment.X));
    }

    [Fact]
    public void BreakSpacesPreservesSpacesBeforeForcedNewlines()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><div id=box>a  \nb</div>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;font-size:10px;white-space:break-spaces}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 20, 100,
            cancellationToken: cancellation).Root!, "box")!;

        Assert.Equal(new[] { "a  ", "b" }, box.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
        Assert.Equal(new[] { 0d, 5d, 10d }, box.Lines[0].Fragments.Select(fragment => fragment.X));
    }

    [Fact]
    public void BreakSpacesTrailingSpacesContributeToAutomaticTableIntrinsicWidth()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><table id=t><tr><td id=c>a  </td></tr></table>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("*{margin:0;font-size:10px;line-height:10px}td{white-space:break-spaces}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 10, 100,
            cancellationToken: cancellation).Root!;

        Assert.Equal(16d, Find(root, "t")!.Content.Width);
        Assert.Equal(10d, Find(root, "c")!.Content.Width);
    }

    [Theory]
    [InlineData("<div id=box>a\tb</div>")]
    [InlineData("<div id=box>a\fb</div>")]
    [InlineData("<div id=box>a\u2007b</div>")]
    public void BreakSpacesRejectsUnsupportedSeparators(string html)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse($"<!doctype html>{html}",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}#box{margin:0;white-space:break-spaces}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        Assert.Throws<UnsupportedLayoutException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100,
                cancellationToken: cancellation));
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
