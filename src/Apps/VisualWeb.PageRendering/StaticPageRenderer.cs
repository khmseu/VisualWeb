using VisualWeb.Core.Url;
using VisualWeb.Engine.Content;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Scripting;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;

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
    private sealed record DocumentState(LoadedPage Source, HtmlParseResult Parsed, int Scripts);
    private static readonly HtmlParserOptions DocumentHtmlOptions = new();
    private readonly bool executeInlineScripts;
    private DocumentState? committed;
    private DocumentState? candidate;
    private bool disposed;
    public StaticPageRenderer(string fontPath, int maxPixels, bool executeInlineScripts = false)
    {
        this.executeInlineScripts = executeInlineScripts;
        options = new() { Html = DocumentHtmlOptions, Paint = new() { MaxPixels = maxPixels } };
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
    public Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        Task.FromResult(Render(page, viewport, cancellationToken, reuseDocument: true));
    public bool HasDocument(Guid documentId) => committed?.Source.DocumentId == documentId || candidate?.Source.DocumentId == documentId;
    public void CommitDocument(Guid documentId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (candidate?.Source.DocumentId == documentId) { committed = candidate; candidate = null; }
        else if (committed?.Source.DocumentId != documentId) { throw new PageNavigationException("Document is not retained."); }
    }
    public BrowserPage Render(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken, bool reuseDocument = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(page);
        cancellationToken.ThrowIfCancellationRequested();
        if (page.DocumentId == Guid.Empty) { throw new PageNavigationException("Document identity must be nonempty."); }
        DocumentState state;
        if (reuseDocument)
        {
            state = committed?.Source.DocumentId == page.DocumentId ? committed
                : candidate?.Source.DocumentId == page.DocumentId ? candidate
                : throw new PageNavigationException("Retained document was lost; reload explicitly rather than reparsing or rerunning scripts.");
            if (state.Source.Html != page.Html || state.Source.Url.Href != page.Url.Href
                || !state.Source.Stylesheets.SequenceEqual(page.Stylesheets))
            { throw new PageNavigationException("Retained document identity does not match its source."); }
        }
        else
        {
            var fresh = HtmlParser.Parse(page.Html, options.Html, cancellationToken);
            var scripts = executeInlineScripts ? InlinePageScripts.Execute(fresh.Document, cancellationToken) : 0;
            state = new(page, fresh, scripts);
        }
        var parsed = state.Parsed;
        var sources = CollectStyles(parsed.Document, options.Css, cancellationToken, state.Source.Url, state.Source.Stylesheets);
        var rendered = OfflinePageRenderer.RenderParsed(parsed, sources, text, paint,
            viewport.Width, viewport.Height, options with { Scale = viewport.Scale, ScrollY = viewport.ScrollY }, cancellationToken);
        var (links, controlGeometry) = CollectLinks(rendered.Layout, page.Url,
            Math.Min(viewport.ScrollY, rendered.ScrollHeight - viewport.Height), cancellationToken);
        var (forms, controls) = PageForms.Collect(parsed.Document, page.Url, controlGeometry, links.Count,
            rendered.Layout.ViewportWidth, rendered.Layout.ViewportHeight, cancellationToken);
        var title = executeInlineScripts ? parsed.Document.Title
            : parsed.Document.Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title")?.TextContent;
        var status = $"Response {page.StatusCode}; HTML diagnostics: {parsed.Errors.Count}. " + string.Join(" ", page.Diagnostics);
        cancellationToken.ThrowIfCancellationRequested();
        if (!reuseDocument) { candidate = state; }
        if (executeInlineScripts)
        {
            status += $" Post-parse inline scripts: {state.Scripts}; no HTML scheduling/event loop.";
        }
        return new(rendered.Frame, string.IsNullOrWhiteSpace(title) ? page.Url.Href : title, status)
        { ScrollHeight = rendered.ScrollHeight, LinkTargets = links, Forms = forms, FormControls = controls };
    }
    private static (IReadOnlyList<PageLinkTarget> Links, IReadOnlyDictionary<DomElement, (PageLinkRect, int)> Controls)
        CollectLinks(LayoutResult layout, BrowserUrl url, double scrollY, CancellationToken cancellationToken)
    {
        var links = new List<PageLinkTarget>();
        var controls = new Dictionary<DomElement, (PageLinkRect, int)>();
        var anchors = new Dictionary<DomElement, List<PageLinkRect>>();
        long urlBytes = 0;
        if (layout.Root is { } root) { Visit(root); }
        try { RendererProtocol.ValidateLinks(links, layout.ViewportWidth, layout.ViewportHeight); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return (links.AsReadOnly(), controls);

        void Visit(LayoutBox box)
        {
            foreach (var item in box.Flow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is LayoutBlockItem block)
                {
                    if (block.Box.Element.LocalName is "input" or "button")
                    {
                        var border = block.Box.BorderBox;
                        var left = Math.Clamp(border.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(border.X + border.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(border.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(border.Y + border.Height - scrollY, 0, layout.ViewportHeight);
                        if (right > left && bottom > top)
                        { controls[block.Box.Element] = (new(left, top, right - left, bottom - top), links.Count); }
                    }
                    Visit(block.Box);
                }
                else if (item is LayoutLineItem line)
                {
                    foreach (var fragment in line.Line.Fragments)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        DomElement? anchor = null;
                        for (var node = fragment.Source.ParentNode; node is not null; node = node.ParentNode)
                        {
                            if (node is DomElement { LocalName: "a", NamespaceUri: DomElement.HtmlNamespace } element)
                            { anchor = element; break; }
                        }
                        if (anchor?.GetAttribute("href") is not { } href) { continue; }
                        var left = Math.Clamp(fragment.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(fragment.X + fragment.Run.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(line.Line.Bounds.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(line.Line.Bounds.Y + line.Line.Bounds.Height - scrollY, 0, layout.ViewportHeight);
                        if (right <= left || bottom <= top) { continue; }
                        if (!anchors.TryGetValue(anchor, out var target))
                        {
                            if (links.Count >= RendererProtocol.MaxLinkTargets)
                            { throw new PageNavigationException("Renderer link count limit exceeded."); }
                            if (href.Length > RendererProtocol.MaxTextCharacters)
                            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
                            var parsed = BrowserUrl.ParseResult(href, url);
                            var destination = parsed.Url?.Href ?? throw new PageNavigationException("Invalid link URL: " + parsed.Error);
                            if (destination.Length > RendererProtocol.MaxTextCharacters)
                            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
                            urlBytes += System.Text.Encoding.UTF8.GetByteCount(destination);
                            if (urlBytes > RendererProtocol.MaxLinkMetadataBytes)
                            { throw new PageNavigationException("Renderer link metadata byte limit exceeded."); }
                            target = [];
                            anchors.Add(anchor, target);
                            links.Add(new(target.AsReadOnly(), destination));
                        }
                        if (target.Count >= RendererProtocol.MaxLinkRects)
                        { throw new PageNavigationException("Renderer per-anchor rectangle limit exceeded."); }
                        target.Add(new(left, top, right - left, bottom - top));
                    }
                }
            }
        }
    }
    /// <summary>Unique resolved request URLs of supported linked stylesheets in document order, before scripts run.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
    /// "stylesheet"</see>. Used by the browser to broker fetches with the renderer's exact parser options;
    /// unsupported link semantics throw.</remarks>
    public static IReadOnlyList<string> DiscoverStylesheets(string html, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default) =>
        DiscoverStylesheets(HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document, documentUrl, cancellationToken);
    /// <inheritdoc cref="DiscoverStylesheets(string, BrowserUrl, CancellationToken)"/>
    public static IReadOnlyList<string> DiscoverStylesheets(DomDocument document, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default) => LinkedStylesheets.Discover(document, documentUrl, cancellationToken);
    /// <summary>Embedded and browser-provided linked author sources in document (cascade) order.</summary>
    /// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#cascade-order">order of appearance</see>.
    /// Links whose resolved URL is absent from <paramref name="stylesheets"/> (for example, added or changed by scripts)
    /// throw; nothing is fetched here.</remarks>
    public static IReadOnlyList<CssStyleSource> CollectStyles(DomDocument document, CssOptions? options = null,
        CancellationToken cancellationToken = default, BrowserUrl? documentUrl = null,
        IReadOnlyList<PageStylesheet>? stylesheets = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var limits = options ?? new();
        if (limits.MaxStyleSources <= 0 || limits.MaxInputCharacters <= 0) { throw new ArgumentOutOfRangeException(nameof(options)); }
        var provided = (stylesheets ?? []).ToDictionary(sheet => sheet.Url, sheet => sheet.Css, StringComparer.Ordinal);
        var sources = new List<CssStyleSource>();
        long characters = 0;
        var baseHref = LinkedStylesheets.BaseHref(document);
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LinkedStylesheets.Resolve(element, documentUrl, baseHref) is { } url)
            {
                if (!provided.TryGetValue(url, out var linked))
                {
                    throw new PageNavigationException($"Linked stylesheet {url} was not provided by the browser; "
                        + "renderers never fetch and links added or changed by scripts are unsupported.");
                }
                Add(LinkedStylesheets.StripCharsetRule(linked), "Linked");
                continue;
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
            Add(string.Concat(element.ChildNodes.OfType<DomText>().Select(node => node.Data)), "Embedded");
        }
        return sources.AsReadOnly();

        void Add(string css, string kind)
        {
            if (sources.Count >= limits.MaxStyleSources) { throw new CssLimitException(kind + " stylesheet source limit exceeded."); }
            characters += css.Length;
            if (characters > limits.MaxInputCharacters) { throw new CssLimitException(kind + " stylesheet character limit exceeded."); }
            sources.Add(new(css));
        }
    }
    public static bool IsRenderFailure(Exception exception) => exception is PageNavigationException or HtmlLimitException
        or UnsupportedHtmlException or CssLimitException or UnsupportedCssException or LayoutLimitException or UnsupportedLayoutException
        or TextLimitException or UnsupportedTextException or FontLoadException or PaintLimitException or UnsupportedPaintException
        or ScriptExecutionException or ScriptLimitException;
    public void Dispose()
    {
        if (disposed) { return; }
        paint.Dispose(); font.Dispose(); committed = null; candidate = null; disposed = true;
    }
}
