using VisualWeb.Engine.Content;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;

namespace VisualWeb.PageRendering;

/// <summary>Tab-owned static rendering policy shared by local and process hosts.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-style-element">style element</see>.
/// No network, platform backend or browser chrome dependency.</remarks>
public sealed class StaticPageRenderer : IPageRenderer
{
    private readonly TextFont font;
    private readonly FontSet text = new();
    private readonly PaintFontRegistry paint;
    private readonly PageRenderOptions options;
    private bool disposed;
    public StaticPageRenderer(string fontPath, int maxPixels)
    {
        options = new() { Paint = new() { MaxPixels = maxPixels } };
        font = new(fontPath);
        paint = new(options.Paint);
        try
        {
            foreach (var family in new[] { "serif", "sans-serif", "monospace", "VisualWeb" }) { text.Register(family, font); }
            paint.Register(font);
        }
        catch { paint.Dispose(); font.Dispose(); throw; }
    }
    public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        Task.FromResult(Render(page, viewport, cancellationToken));
    public BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var parsed = HtmlParser.Parse(page.Html, options.Html, cancellationToken);
        var sources = CollectStyles(parsed.Document, options.Css, cancellationToken);
        var rendered = OfflinePageRenderer.RenderParsed(parsed, sources, text, paint,
            viewport.Width, viewport.Height, options with { Scale = viewport.Scale }, cancellationToken);
        var title = parsed.Document.Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title")?.TextContent;
        var status = $"Response {page.StatusCode}; HTML diagnostics: {parsed.Errors.Count}. " + string.Join(" ", page.Diagnostics);
        return new(rendered.Frame, string.IsNullOrWhiteSpace(title) ? page.Url.Href : title, status);
    }
    public static IReadOnlyList<CssStyleSource> CollectStyles(DomDocument document, CssOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var limits = options ?? new();
        if (limits.MaxStyleSources <= 0 || limits.MaxInputCharacters <= 0) { throw new ArgumentOutOfRangeException(nameof(options)); }
        var sources = new List<CssStyleSource>();
        long characters = 0;
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "link" && (element.GetAttribute("rel") ?? "").Split([' ', '\t', '\n', '\r', '\f'],
                StringSplitOptions.RemoveEmptyEntries).Any(token => token.Equals("stylesheet", StringComparison.OrdinalIgnoreCase)))
            {
                throw new PageNavigationException("Linked stylesheets are deferred; no stylesheet was fetched.");
            }
            if (element.LocalName != "style") { continue; }
            var type = element.GetAttribute("type");
            if (type is { Length: > 0 } && !type.Equals("text/css", StringComparison.OrdinalIgnoreCase))
            {
                throw new PageNavigationException("Non-CSS style elements are unsupported.");
            }
            if (element.GetAttribute("media") is { Length: > 0 } media
                && !media.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                throw new PageNavigationException("Conditional embedded stylesheets are deferred.");
            }
            if (element.GetAttribute("title") is { Length: > 0 })
            {
                throw new PageNavigationException("Named/alternate stylesheet sets are deferred.");
            }
            if (sources.Count >= limits.MaxStyleSources) { throw new CssLimitException("Embedded stylesheet source limit exceeded."); }
            var css = string.Concat(element.ChildNodes.OfType<DomText>().Select(node => node.Data));
            characters += css.Length;
            if (characters > limits.MaxInputCharacters) { throw new CssLimitException("Embedded stylesheet character limit exceeded."); }
            sources.Add(new(css));
        }
        return sources.AsReadOnly();
    }
    public static bool IsRenderFailure(Exception exception) => exception is PageNavigationException or HtmlLimitException
        or UnsupportedHtmlException or CssLimitException or UnsupportedCssException or LayoutLimitException or UnsupportedLayoutException
        or TextLimitException or UnsupportedTextException or FontLoadException or PaintLimitException or UnsupportedPaintException;
    public void Dispose()
    {
        if (disposed) { return; }
        paint.Dispose(); font.Dispose(); disposed = true;
    }
}
