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
    private sealed class LinkTargetData(string url, bool openInNewTab, double documentY)
    {
        public string Url { get; } = url;
        public bool OpenInNewTab { get; } = openInNewTab;
        public double DocumentY { get; set; } = documentY;
        public List<PageLinkRect> Rects { get; } = [];
        public bool IsBlockBoxAnchor { get; set; }
        public PageLinkRect? BlockRectangle { get; set; }
    }
    private static readonly HtmlParserOptions DocumentHtmlOptions = new();
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];
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
        var (links, controlGeometry, textTargets) = CollectLinks(parsed.Document, rendered.Layout, page.Url,
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
        {
            ScrollHeight = rendered.ScrollHeight,
            LinkTargets = links,
            FragmentTargets = CollectFragmentTargets(rendered.Layout, rendered.ScrollHeight, cancellationToken),
            Forms = forms,
            FormControls = controls,
            TextTargets = textTargets
        };
    }
    private static (IReadOnlyList<PageLinkTarget> Links, IReadOnlyDictionary<DomElement, (PageLinkRect, int)> Controls,
        IReadOnlyList<PageTextTarget> TextTargets)
        CollectLinks(DomDocument document, LayoutResult layout, BrowserUrl url, double scrollY, CancellationToken cancellationToken)
    {
        var baseTarget = FindBaseTarget(document, cancellationToken);
        var baseUrl = FindBaseUrl(document, url, cancellationToken);
        var linkData = new List<LinkTargetData>();
        var controls = new Dictionary<DomElement, (PageLinkRect, int)>();
        var anchors = new Dictionary<DomElement, LinkTargetData>();
        var textTargets = new List<PageTextTarget>();
        long urlBytes = 0;
        long textBytes = 0;
        List<PageLinkTarget> links = [];
        if (layout.Root is { } root) { Visit(root); }
        links = linkData.Select(target => new PageLinkTarget(target.Rects.AsReadOnly(), target.Url,
            target.OpenInNewTab, target.DocumentY)).ToList();
        try
        {
            RendererProtocol.ValidateLinks(links, layout.ViewportWidth, layout.ViewportHeight);
            RendererProtocol.ValidateTextTargets(textTargets, layout.ViewportWidth, layout.ViewportHeight);
        }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return (links.AsReadOnly(), controls, textTargets.AsReadOnly());

        LinkTargetData GetLinkTarget(DomElement anchor, string href, double documentY)
        {
            if (anchors.TryGetValue(anchor, out var existing)) { return existing; }
            if (linkData.Count >= RendererProtocol.MaxLinkTargets)
            { throw new PageNavigationException("Renderer link count limit exceeded."); }
            if (href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(href, baseUrl);
            var destination = parsed.Url?.Href ?? throw new PageNavigationException("Invalid link URL: " + parsed.Error);
            if (destination.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer link URL limit exceeded."); }
            urlBytes += System.Text.Encoding.UTF8.GetByteCount(destination);
            if (urlBytes > RendererProtocol.MaxLinkMetadataBytes)
            { throw new PageNavigationException("Renderer link metadata byte limit exceeded."); }
            var target = new LinkTargetData(destination,
                IsBlankTarget(anchor.GetAttribute("target"), baseTarget), documentY);
            anchors.Add(anchor, target);
            linkData.Add(target);
            return target;
        }

        void AddLinkRectangle(LinkTargetData target, PageLinkRect rect)
        {
            if (target.Rects.Count >= RendererProtocol.MaxLinkRects)
            { throw new PageNavigationException("Renderer per-anchor rectangle limit exceeded."); }
            target.Rects.Add(rect);
        }

        void Visit(LayoutBox box)
        {
            foreach (var item in box.Flow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is LayoutBlockItem block)
                {
                    if (block.Box.Element is { LocalName: "a", NamespaceUri: DomElement.HtmlNamespace } anchor
                        && anchor.GetAttribute("href") is { } href
                        && block.Box.BorderBox.Width > 0 && block.Box.BorderBox.Height > 0)
                    {
                        var border = block.Box.BorderBox;
                        var target = GetLinkTarget(anchor, href, border.Y);
                        target.IsBlockBoxAnchor = true;
                        var left = Math.Clamp(border.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(border.X + border.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(border.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(border.Y + border.Height - scrollY, 0, layout.ViewportHeight);
                        if (right > left && bottom > top)
                        {
                            var rect = new PageLinkRect(left, top, right - left, bottom - top);
                            AddLinkRectangle(target, rect);
                            target.BlockRectangle = rect;
                        }
                    }
                    if (block.Box.Element.LocalName is "input" or "button" or "select")
                    {
                        var border = block.Box.BorderBox;
                        var left = Math.Clamp(border.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(border.X + border.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(border.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(border.Y + border.Height - scrollY, 0, layout.ViewportHeight);
                        if (right > left && bottom > top)
                        { controls[block.Box.Element] = (new(left, top, right - left, bottom - top), linkData.Count); }
                    }
                    Visit(block.Box);
                }
                else if (item is LayoutLineItem line)
                {
                    var linksBeforeLine = linkData.Count;
                    var lineAnchors = new Dictionary<DomElement, double>();
                    var lineNewAnchors = new HashSet<DomElement>();
                    foreach (var fragment in line.Line.Fragments)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        DomElement? anchor = null;
                        for (var node = fragment.Source.ParentNode; node is not null; node = node.ParentNode)
                        {
                            if (node is DomElement { LocalName: "a", NamespaceUri: DomElement.HtmlNamespace } element)
                            { anchor = element; break; }
                        }
                        LinkTargetData? target = null;
                        if (anchor?.GetAttribute("href") is { } href)
                        {
                            if (!anchors.TryGetValue(anchor, out target))
                            {
                                target = GetLinkTarget(anchor, href, line.Line.Bounds.Y);
                                lineNewAnchors.Add(anchor);
                            }
                            if (target.IsBlockBoxAnchor)
                            {
                                if (target.BlockRectangle is { } blockRectangle)
                                { target.Rects.Remove(blockRectangle); }
                                target.BlockRectangle = null;
                                target.IsBlockBoxAnchor = false;
                                target.DocumentY = line.Line.Bounds.Y;
                            }
                        }
                        var left = Math.Clamp(fragment.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(fragment.X + fragment.Run.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(line.Line.Bounds.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(line.Line.Bounds.Y + line.Line.Bounds.Height - scrollY, 0, layout.ViewportHeight);
                        if (right <= left || bottom <= top) { continue; }
                        var rect = new PageLinkRect(left, top, right - left, bottom - top);
                        var text = fragment.Run.Text;
                        if (textTargets.Count >= RendererProtocol.MaxTextTargets)
                        { throw new PageNavigationException("Renderer text target count limit exceeded."); }
                        textBytes += System.Text.Encoding.UTF8.GetByteCount(text);
                        if (textBytes > RendererProtocol.MaxTextMetadataBytes)
                        { throw new PageNavigationException("Renderer text target byte limit exceeded."); }
                        textTargets.Add(new(text, rect));
                        if (target is null) { continue; }
                        AddLinkRectangle(target, rect);
                        lineAnchors[anchor!] = lineAnchors.TryGetValue(anchor!, out var firstX) ? Math.Min(firstX, fragment.X) : fragment.X;
                    }
                    foreach (var widget in line.Line.Widgets)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var bounds = widget.Bounds;
                        var left = Math.Clamp(bounds.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(bounds.X + bounds.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(bounds.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(bounds.Y + bounds.Height - scrollY, 0, layout.ViewportHeight);
                        if (right > left && bottom > top)
                        {
                            var beforeLink = linksBeforeLine + lineNewAnchors.Count(anchor =>
                                lineAnchors.TryGetValue(anchor, out var anchorX) && anchorX < bounds.X);
                            controls[widget.Element] = (new(left, top, right - left, bottom - top), beforeLink);
                        }
                    }
                }
            }
        }
    }
    private static string? FindBaseTarget(DomDocument document, CancellationToken cancellationToken)
    {
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "base" && element.GetAttribute("target") is { } target) { return target; }
        }
        return null;
    }

    private static BrowserUrl FindBaseUrl(DomDocument document, BrowserUrl fallback, CancellationToken cancellationToken)
    {
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName != "base" || element.GetAttribute("href") is not { } href) { continue; }
            if (href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer base URL limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(href, fallback).Url;
            if (parsed is null || parsed.Protocol is "data:" or "javascript:") { return fallback; }
            if (parsed.Href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer base URL limit exceeded."); }
            return parsed;
        }
        return fallback;
    }

    private static bool IsBlankTarget(string? target, string? baseTarget)
    {
        var name = (target ?? baseTarget)?.Trim(AsciiWhitespace);
        if (string.IsNullOrEmpty(name)) { return false; }
        if (name.All(character => character <= 0x7f))
        {
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) { return true; }
            if (name.Equals("_self", StringComparison.OrdinalIgnoreCase)
                || name.Equals("_parent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("_top", StringComparison.OrdinalIgnoreCase)) { return false; }
        }
        throw new PageNavigationException("Unsupported named hyperlink target.");
    }

    private static IReadOnlyList<PageFragmentTarget> CollectFragmentTargets(LayoutResult layout, double scrollHeight,
        CancellationToken cancellationToken)
    {
        var targets = new List<PageFragmentTarget>();
        var namedTargets = new List<PageFragmentTarget>();
        var elements = new HashSet<DomElement>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        long bytes = 0;
        if (layout.Root is { } root) { Visit(root); }
        targets.AddRange(namedTargets.Where(target => !ids.Contains(target.Id)));
        try { RendererProtocol.ValidateFragmentTargets(targets, scrollHeight); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return targets.AsReadOnly();

        void Register(DomElement element, double y)
        {
            if (!elements.Add(element)) { return; }
            AddTarget(element.GetAttribute("id"), ids, targets, y, allowEmpty: true);
            if (element.LocalName == "a")
            { AddTarget(element.GetAttribute("name"), names, namedTargets, y, allowEmpty: false); }
        }

        void AddTarget(string? id, HashSet<string> seen, List<PageFragmentTarget> destination, double y, bool allowEmpty)
        {
            if (id is null || !allowEmpty && id.Length == 0 || !seen.Add(id)) { return; }
            if (targets.Count + namedTargets.Count >= RendererProtocol.MaxFragmentTargets)
            { throw new PageNavigationException("Renderer fragment target count limit exceeded."); }
            if (id.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer fragment target ID limit exceeded."); }
            bytes += System.Text.Encoding.UTF8.GetByteCount(id);
            if (bytes > RendererProtocol.MaxFragmentMetadataBytes)
            { throw new PageNavigationException("Renderer fragment target metadata byte limit exceeded."); }
            destination.Add(new(id, Math.Clamp(y, 0, scrollHeight)));
        }

        void Visit(LayoutBox box)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Register(box.Element, box.BorderBox.Y);
            foreach (var item in box.Flow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is LayoutBlockItem block) { Visit(block.Box); }
                else if (item is LayoutLineItem line)
                {
                    foreach (var fragment in line.Line.Fragments)
                    {
                        var ancestors = new Stack<DomElement>();
                        for (var node = fragment.Source.ParentNode; node is not null; node = node.ParentNode)
                        {
                            if (node is DomElement element) { ancestors.Push(element); }
                        }
                        while (ancestors.TryPop(out var element)) { Register(element, line.Line.Bounds.Y); }
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
    /// <summary>Import URLs referenced by supported embedded stylesheets, resolved against the document base URL.</summary>
    /// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#at-import">import rules</see>.
    /// Only unconditional imports before all other rules are supported; conditions and cascade layers fail visibly.</remarks>
    public static IReadOnlyList<string> DiscoverInlineStylesheetImports(string html, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default)
    {
        var document = HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document;
        var baseUrl = LinkedStylesheets.BaseUrl(document, documentUrl, cancellationToken);
        var imports = new List<string>();
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName != "style") { continue; }
            var css = InlineCss(element);
            imports.AddRange(CssImports.Parse(css, baseUrl, cancellationToken).Imports.Select(url => url.Href));
        }
        return imports.AsReadOnly();
    }
    /// <summary>Applies fixed inline-content decisions to a document before publication.</summary>
    /// <remarks>Spec: csp3;
    /// <see href="https://www.w3.org/TR/CSP3/#should-block-inline">should element's inline type behavior be blocked</see>.</remarks>
    public static void EnforceInlineContentPolicy(string html, bool allowInlineScripts, bool allowInlineStyles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (allowInlineScripts && allowInlineStyles) { return; }
        EnforceInlineContentPolicy(html, (_, _) => allowInlineScripts, (_, _) => allowInlineStyles,
            allowInlineStyles, cancellationToken);
    }

    /// <summary>Rejects inline script or style markup disabled by browser-selected response policy.</summary>
    /// <remarks>Spec: csp3;
    /// <see href="https://www.w3.org/TR/CSP3/#should-block-inline">should element's inline type behavior be blocked</see>.
    /// Matching nonce callbacks apply to script/style elements; style attributes remain nonce/hash-ineligible.</remarks>
    public static void EnforceInlineContentPolicy(string html, Func<string?, bool> allowsInlineScripts,
        Func<string?, bool> allowsInlineStyles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(allowsInlineScripts);
        ArgumentNullException.ThrowIfNull(allowsInlineStyles);
        EnforceInlineContentPolicy(html, (nonce, _) => allowsInlineScripts(nonce),
            (nonce, _) => allowsInlineStyles(nonce), allowsInlineStyles(null), cancellationToken);
    }

    /// <summary>Applies nonce/hash decisions to inline elements and blocks disallowed style attributes.</summary>
    /// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#should-block-inline">inline type checks</see>.</remarks>
    public static void EnforceInlineContentPolicy(string html, Func<string?, string, bool> allowsInlineScripts,
        Func<string?, string, bool> allowsInlineStyles, bool allowInlineStyleAttributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(allowsInlineScripts);
        ArgumentNullException.ThrowIfNull(allowsInlineStyles);
        var document = HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document;
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "script" && element.GetAttribute("src") is null
                && !allowsInlineScripts(element.GetAttribute("nonce"), element.TextContent ?? string.Empty))
            {
                throw new PageNavigationException("Content-Security-Policy blocked an inline script element.");
            }
            if ((element.LocalName == "style"
                    && !allowsInlineStyles(element.GetAttribute("nonce"), element.TextContent ?? string.Empty))
                || (element.GetAttribute("style") is not null && !allowInlineStyleAttributes))
            {
                throw new PageNavigationException("Content-Security-Policy blocked inline style content.");
            }
        }
    }
    /// <summary>Import URLs in one stylesheet, resolved against its final response URL.</summary>
    /// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#at-import">import rules</see>.
    /// Only unconditional imports before all other rules are supported; conditions and cascade layers fail visibly.</remarks>
    public static IReadOnlyList<string> DiscoverStylesheetImports(string css, BrowserUrl stylesheetUrl,
        CancellationToken cancellationToken = default) =>
        CssImports.Parse(LinkedStylesheets.StripCharsetRule(css), stylesheetUrl, cancellationToken)
            .Imports.Select(url => url.Href).ToArray();
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
        var provided = (stylesheets ?? []).ToDictionary(sheet => sheet.Url, sheet => sheet, StringComparer.Ordinal);
        var sources = new List<CssStyleSource>();
        long characters = 0;
        var baseUrl = documentUrl is null ? null : LinkedStylesheets.BaseUrl(document, documentUrl, cancellationToken);
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LinkedStylesheets.Resolve(element, documentUrl, baseUrl) is { } url)
            {
                if (!provided.TryGetValue(url, out var linked))
                {
                    throw new PageNavigationException($"Linked stylesheet {url} was not provided by the browser; "
                        + "renderers never fetch and links added or changed by scripts are unsupported.");
                }
                Expand(LinkedStylesheets.StripCharsetRule(linked.Css), BrowserUrl.Parse(linked.BaseUrl),
                    "Linked", [linked.Url, linked.BaseUrl]);
                continue;
            }
            if (element.LocalName != "style") { continue; }
            Expand(InlineCss(element), baseUrl, "Embedded", []);
        }
        return sources.AsReadOnly();

        void Expand(string css, BrowserUrl? sourceBaseUrl, string kind, HashSet<string> ancestors)
        {
            characters += css.Length;
            if (characters > limits.MaxInputCharacters) { throw new CssLimitException(kind + " stylesheet character limit exceeded."); }
            var parsed = CssImports.Parse(css, sourceBaseUrl, cancellationToken);
            foreach (var import in parsed.Imports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ancestors.Contains(import.Href)) { continue; }
                if (!provided.TryGetValue(import.Href, out var imported))
                {
                    throw new PageNavigationException($"Imported stylesheet {import.Href} was not provided by the browser; "
                        + "renderers never fetch.");
                }
                var nextAncestors = new HashSet<string>(ancestors, StringComparer.Ordinal)
                {
                    import.Href,
                    imported.BaseUrl
                };
                Expand(LinkedStylesheets.StripCharsetRule(imported.Css), BrowserUrl.Parse(imported.BaseUrl), kind, nextAncestors);
            }
            if (sources.Count >= limits.MaxStyleSources) { throw new CssLimitException(kind + " stylesheet source limit exceeded."); }
            sources.Add(new(parsed.Css));
        }
    }

    private static string InlineCss(DomElement element)
    {
        var type = element.GetAttribute("type");
        if (type is { Length: > 0 } && !type.Equals("text/css", StringComparison.OrdinalIgnoreCase))
        {
            throw new PageNavigationException("Non-CSS style elements are unsupported.");
        }
        if (!LinkedStylesheets.SupportsMedia(element.GetAttribute("media")))
        {
            throw new PageNavigationException("Embedded stylesheet media must be absent, 'all', or the single 'screen' media type.");
        }
        if (element.GetAttribute("title") is { Length: > 0 })
        {
            throw new PageNavigationException("Named/alternate stylesheet sets are deferred.");
        }
        return string.Concat(element.ChildNodes.OfType<DomText>().Select(node => node.Data));
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
