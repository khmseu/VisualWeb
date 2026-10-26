using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;

namespace VisualWeb.Engine.Content;

public sealed record PageRenderOptions
{
    public HtmlParserOptions Html { get; init; } = new();
    public CssOptions Css { get; init; } = new();
    public LayoutOptions Layout { get; init; } = new();
    public PaintOptions Paint { get; init; } = new();
    public bool IncludeUserAgentStyle { get; init; } = true;
    public double Scale { get; init; } = 1;
    public double ScrollY { get; init; }
    public double ScrollX { get; init; }
}
public sealed record RenderedPage(HtmlParseResult Html, CssStyleResult Styles, LayoutResult Layout,
    DisplayList DisplayList, RasterFrame Frame)
{
    public double ScrollHeight { get; init; }
    public double ScrollWidth { get; init; }
}

/// <summary>Decoded HTML and explicitly ordered CSS to a renderer-local CPU frame.</summary>
/// <remarks>Stages use html, css-cascade, css2-visual and css2-paint standards.
/// See <see href="https://html.spec.whatwg.org/multipage/parsing.html">HTML parsing</see>,
/// <see href="https://www.w3.org/TR/css-cascade-5/#cascade">CSS cascade</see> and
/// <see href="https://www.w3.org/TR/CSS22/zindex.html#painting-order">static paint order</see>.
/// No decoding policy, networking, automatic style/link extraction, scripts or browser navigation.</remarks>
public static class OfflinePageRenderer
{
    public static RenderedPage Render(string html, IEnumerable<CssStyleSource> sources, ITextShaper text,
        PaintFontRegistry fonts, double viewportWidth, double viewportHeight, PageRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fonts);
        var settings = options ?? new();
        Validate(settings, viewportWidth, viewportHeight);
        var parsed = HtmlParser.Parse(html, settings.Html, cancellationToken);
        return RenderParsed(parsed, sources, text, fonts, viewportWidth, viewportHeight, settings, cancellationToken);
    }

    /// <summary>Render a caller-parsed document without parsing its string a second time.</summary>
    /// <remarks>Caller-provided ordered sources may be collected from the static DOM; no resource discovery runs here.</remarks>
    public static RenderedPage RenderParsed(HtmlParseResult parsed, IEnumerable<CssStyleSource> sources, ITextShaper text,
        PaintFontRegistry fonts, double viewportWidth, double viewportHeight, PageRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(parsed.Document);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fonts);
        var settings = options ?? new();
        Validate(settings, viewportWidth, viewportHeight);
        var styles = CssStyleEngine.Compute(parsed.Document, sources, settings.IncludeUserAgentStyle, settings.Css, cancellationToken);
        if (styles.Diagnostics.Count > 0)
        {
            throw new UnsupportedLayoutException("Resolve CSS diagnostics before rendering: "
                + string.Join(" ", styles.Diagnostics.Take(5).Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        }
        var layout = StaticLayout.Layout(parsed.Document, styles, text, viewportWidth, viewportHeight, settings.Layout, cancellationToken);
        var bottom = layout.Root is { } root ? root.BorderBox.Y + root.BorderBox.Height : 0;
        var scrollHeight = Math.Max(viewportHeight, bottom);
        if (!double.IsFinite(bottom) || bottom < 0 || scrollHeight > PaintOptions.MaxDocumentHeight)
        { throw new PaintLimitException("Document scroll height exceeds the CSS-pixel bound."); }
        var scrollWidth = Math.Max(viewportWidth, MeasureScrollWidth(layout.Root, viewportWidth, cancellationToken));
        if (!double.IsFinite(scrollWidth) || scrollWidth > PaintOptions.MaxDocumentWidth)
        { throw new PaintLimitException("Document scroll width exceeds the CSS-pixel bound."); }
        var list = DisplayListBuilder.Build(layout, styles, settings.Paint, cancellationToken, documentWidth: scrollWidth);
        var offset = Math.Min(settings.ScrollY, scrollHeight - viewportHeight);
        var offsetX = Math.Min(settings.ScrollX, scrollWidth - viewportWidth);
        var frame = CpuRasterizer.Render(list, fonts, settings.Scale, options: settings.Paint,
            cancellationToken: cancellationToken, scrollY: offset, scrollX: offsetX);
        return new(parsed, styles, layout, list, frame) { ScrollHeight = scrollHeight, ScrollWidth = scrollWidth };
    }
    private static void Validate(PageRenderOptions settings, double viewportWidth, double viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(settings.Html);
        ArgumentNullException.ThrowIfNull(settings.Css);
        ArgumentNullException.ThrowIfNull(settings.Layout);
        ArgumentNullException.ThrowIfNull(settings.Paint);
        if (!double.IsFinite(settings.Scale) || settings.Scale <= 0) { throw new ArgumentOutOfRangeException(nameof(settings), "Raster scale must be finite and positive."); }
        if (!double.IsFinite(settings.ScrollY) || settings.ScrollY < 0 || settings.ScrollY > 1e9
            || !double.IsFinite(settings.ScrollX) || settings.ScrollX < 0 || settings.ScrollX > 1e9)
        { throw new ArgumentOutOfRangeException(nameof(settings), "Scroll offset must be finite, nonnegative and bounded."); }
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0 || !double.IsFinite(viewportHeight) || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport must be finite and positive.");
        }
    }

    private static double MeasureScrollWidth(LayoutBox? root, double viewportWidth, CancellationToken cancellationToken)
    {
        if (root is null) { return viewportWidth; }
        var right = viewportWidth;
        var stack = new Stack<LayoutBox>();
        stack.Push(root);
        while (stack.TryPop(out var box))
        {
            cancellationToken.ThrowIfCancellationRequested();
            right = Math.Max(right, box.BorderBox.X + box.BorderBox.Width);
            foreach (var line in box.Lines)
            {
                right = Math.Max(right, line.Bounds.X + line.Bounds.Width);
                foreach (var fragment in line.Fragments)
                { right = Math.Max(right, fragment.X + fragment.Run.Width); }
                foreach (var widget in line.Widgets)
                { right = Math.Max(right, widget.Bounds.X + widget.Bounds.Width); }
            }
            foreach (var child in box.Children) { stack.Push(child); }
        }
        return right;
    }
}
