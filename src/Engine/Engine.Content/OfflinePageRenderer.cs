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
}
public sealed record RenderedPage(HtmlParseResult Html, CssStyleResult Styles, LayoutResult Layout,
    DisplayList DisplayList, RasterFrame Frame);

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
        ArgumentNullException.ThrowIfNull(settings.Html);
        ArgumentNullException.ThrowIfNull(settings.Css);
        ArgumentNullException.ThrowIfNull(settings.Layout);
        ArgumentNullException.ThrowIfNull(settings.Paint);
        if (!double.IsFinite(settings.Scale) || settings.Scale <= 0) { throw new ArgumentOutOfRangeException(nameof(options), "Raster scale must be finite and positive."); }
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0 || !double.IsFinite(viewportHeight) || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport must be finite and positive.");
        }
        var parsed = HtmlParser.Parse(html, settings.Html, cancellationToken);
        var styles = CssStyleEngine.Compute(parsed.Document, sources, settings.IncludeUserAgentStyle, settings.Css, cancellationToken);
        var layout = StaticLayout.Layout(parsed.Document, styles, text, viewportWidth, viewportHeight, settings.Layout, cancellationToken);
        var list = DisplayListBuilder.Build(layout, styles, settings.Paint, cancellationToken);
        var frame = CpuRasterizer.Render(list, fonts, settings.Scale, options: settings.Paint, cancellationToken: cancellationToken);
        return new(parsed, styles, layout, list, frame);
    }
}
