using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Content;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;

namespace VisualWeb.Browser;

public sealed class PageNavigationException(string message) : Exception(message);
public sealed record LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics);
public readonly record struct PageViewport(double Width, double Height, double Scale);
public sealed record BrowserPage(RasterFrame Frame, string Title, string Status);

public interface IPageSource : IDisposable
{
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken);
}
public interface IPageRenderer : IDisposable
{
    BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken);
}

/// <summary>Development GET navigation, with explicit MIME/encoding policy and no ambient cookies.</summary>
/// <remarks>Specs: fetch, encoding, mime-sniffing;
/// <see href="https://fetch.spec.whatwg.org/#scheme-fetch">scheme fetch</see>,
/// <see href="https://encoding.spec.whatwg.org/#decode">BOM-first decode</see>.
/// No HTML charset prescan, MIME sniffing, origin policy or resource discovery.</remarks>
public sealed class GetPageSource : IPageSource
{
    private readonly Engine.Net.ResourceLoader loader;
    public GetPageSource(HttpMessageHandler? handler = null) =>
        loader = new(new() { MaxResponseBytes = 4 * 1024 * 1024 }, handler);
    public async Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
    {
        var response = await loader.LoadAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.ContentType?.Essence != "text/html"
            && !(response.Url.Protocol == "file:" && response.ContentType is null))
        {
            throw new PageNavigationException("Only text/html (or explicitly selected local HTML files) can be rendered; MIME sniffing is deferred.");
        }
        var encoding = WebEncoding.ForLabel("utf-8");
        if (response.ContentType?.Parameters.TryGetValue("charset", out var label) == true)
        {
            try { encoding = WebEncoding.ForLabel(label); }
            catch (ArgumentException exception) { throw new PageNavigationException(exception.Message); }
        }
        var decoded = response.DecodeText(encoding);
        return new(response.Url, decoded.Text, response.StatusCode,
            response.Diagnostics.Append("Encoding: " + decoded.Encoding.Name + "; no HTML charset prescan.").ToArray());
    }
    public void Dispose() => loader.Dispose();
}

/// <summary>Tab-owned native fonts and static rendering with embedded stylesheet collection.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-style-element">style element</see>.
/// Conditional/alternate/linked stylesheets are explicitly unsupported.</remarks>
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
    public BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var parsed = HtmlParser.Parse(page.Html, options.Html, cancellationToken);
        var sources = CollectStyles(parsed.Document, options.Css, cancellationToken);
        var rendered = OfflinePageRenderer.RenderParsed(parsed, sources, text, paint,
            viewport.Width, viewport.Height, options with { Scale = viewport.Scale }, cancellationToken);
        var title = parsed.Document.Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title")?.TextContent;
        var status = $"Response {page.StatusCode}; HTML diagnostics: {parsed.Errors.Count}. "
            + string.Join(" ", page.Diagnostics);
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
    public void Dispose()
    {
        if (disposed) { return; }
        paint.Dispose();
        font.Dispose();
        disposed = true;
    }
}
