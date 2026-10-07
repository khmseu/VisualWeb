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
    private readonly bool executeInlineScripts;
    private DocumentState? committed;
    private DocumentState? candidate;
    private bool disposed;
    public StaticPageRenderer(string fontPath, int maxPixels, bool executeInlineScripts = false)
    {
        this.executeInlineScripts = executeInlineScripts;
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
            if (state.Source.Html != page.Html || state.Source.Url.Href != page.Url.Href)
            { throw new PageNavigationException("Retained document identity does not match its source."); }
        }
        else
        {
            var fresh = HtmlParser.Parse(page.Html, options.Html, cancellationToken);
            var scripts = executeInlineScripts ? InlinePageScripts.Execute(fresh.Document, cancellationToken) : 0;
            state = new(page, fresh, scripts);
        }
        var parsed = state.Parsed;
        var sources = CollectStyles(parsed.Document, options.Css, cancellationToken);
        var rendered = OfflinePageRenderer.RenderParsed(parsed, sources, text, paint,
            viewport.Width, viewport.Height, options with { Scale = viewport.Scale, ScrollY = viewport.ScrollY }, cancellationToken);
        var links = CollectLinks(rendered.Layout, page.Url,
            Math.Min(viewport.ScrollY, rendered.ScrollHeight - viewport.Height), cancellationToken);
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
        { ScrollHeight = rendered.ScrollHeight, LinkTargets = links };
    }
    private static IReadOnlyList<PageLinkTarget> CollectLinks(LayoutResult layout, BrowserUrl url, double scrollY,
        CancellationToken cancellationToken)
    {
        var links = new List<PageLinkTarget>();
        var destinations = new Dictionary<DomElement, string>();
        long urlBytes = 0;
        if (layout.Root is { } root) { Visit(root); }
        try { RendererProtocol.ValidateLinks(links, layout.ViewportWidth, layout.ViewportHeight); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return links.AsReadOnly();

        void Visit(LayoutBox box)
        {
            foreach (var item in box.Flow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is LayoutBlockItem block) { Visit(block.Box); }
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
                        if (links.Count >= RendererProtocol.MaxLinkTargets)
                        { throw new PageNavigationException("Renderer link count limit exceeded."); }
                        if (!destinations.TryGetValue(anchor, out var destination))
                        {
                            if (href.Length > RendererProtocol.MaxTextCharacters)
                            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
                            var parsed = BrowserUrl.ParseResult(href, url);
                            destination = parsed.Url?.Href ?? throw new PageNavigationException("Invalid link URL: " + parsed.Error);
                            if (destination.Length > RendererProtocol.MaxTextCharacters)
                            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
                            destinations.Add(anchor, destination);
                        }
                        urlBytes += System.Text.Encoding.UTF8.GetByteCount(destination);
                        if (urlBytes > RendererProtocol.MaxLinkMetadataBytes)
                        { throw new PageNavigationException("Renderer link metadata byte limit exceeded."); }
                        links.Add(new(left, top, right - left, bottom - top, destination));
                    }
                }
            }
        }
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
        or TextLimitException or UnsupportedTextException or FontLoadException or PaintLimitException or UnsupportedPaintException
        or ScriptExecutionException or ScriptLimitException;
    public void Dispose()
    {
        if (disposed) { return; }
        paint.Dispose(); font.Dispose(); committed = null; candidate = null; disposed = true;
    }
}
