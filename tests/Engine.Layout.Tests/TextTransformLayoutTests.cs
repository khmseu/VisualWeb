using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class TextTransformLayoutTests
{
    [Fact]
    public void UppercaseAndLowercaseTransformShapedTextWithoutMutatingDom()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse(
            "<!doctype html><p id=upper>MiXeD e\u0301 42</p><p id=lower>MiXeD Link</p>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("body{margin:0}p{margin:0}#upper{text-transform:uppercase}#lower{text-transform:lowercase}")],
            cancellationToken: cancellation);

        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200,
            cancellationToken: cancellation).Root!;

        Assert.Equal("MIXED E\u0301 42", Text(Find(root, "upper")!));
        Assert.Equal("mixed link", Text(Find(root, "lower")!));
        Assert.Equal("MiXeD e\u0301 42", document.GetElementById("upper")!.TextContent);
        Assert.Equal("MiXeD Link", document.GetElementById("lower")!.TextContent);
    }

    [Fact]
    public void NonAsciiLettersFailExplicitlyWhenCaseMappingIsRequested()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><p style='text-transform:uppercase'>café</p>",
            cancellationToken: cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: cancellation);

        var error = Assert.Throws<UnsupportedLayoutException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200,
                cancellationToken: cancellation));

        Assert.Equal("Non-ASCII text-transform case mapping is outside the ASCII-only subset.", error.Message);
    }

    private static string Text(LayoutBox box) => string.Concat(box.Lines.SelectMany(line => line.Fragments)
        .Select(fragment => fragment.Run.Text));

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
