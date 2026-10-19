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
    /// <summary>One retained document; opted-in documents also own a creating-thread-affine V8 host and listeners.</summary>
    private sealed class DocumentState(LoadedPage source, HtmlParseResult parsed, int scripts, V8ScriptHost? host)
    {
        public LoadedPage Source { get; } = source;
        public HtmlParseResult Parsed { get; } = parsed;
        public int Scripts { get; } = scripts;
        public V8ScriptHost? Host { get; private set; } = host;
        /// <summary>Anchor elements and destinations of the latest successful render, in link-target order.</summary>
        public IReadOnlyList<(DomElement Anchor, string Url)> Links { get; set; } = [];
        public int LinkEvents { get; set; }
        public string? Failure { get; private set; }
        public void Fail(string message) { Failure ??= message; Release(); }
        public void Release()
        {
            var retained = Host;
            Host = null;
            retained?.Dispose();
        }
    }
    /// <summary>Strict bound on trusted click and auxclick events dispatched into one retained document host.</summary>
    public const int MaxLinkEventsPerDocument = 1024;
    public const int MaxLinkClicksPerDocument = MaxLinkEventsPerDocument;
    private sealed class LinkTargetData(string url, bool openInNewTab, string? targetName, double documentY)
    {
        public string Url { get; } = url;
        public bool OpenInNewTab { get; } = openInNewTab;
        public string? TargetName { get; } = targetName;
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
        if (candidate?.Source.DocumentId == documentId)
        {
            var previous = committed;
            committed = candidate; candidate = null;
            previous?.Release();
        }
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
                || state.Source.BaseUrl?.Href != page.BaseUrl?.Href
                || !state.Source.Stylesheets.SequenceEqual(page.Stylesheets))
            { throw new PageNavigationException("Retained document identity does not match its source."); }
            if (state.Failure is { } failure) { throw new PageNavigationException(failure); }
        }
        else
        {
            var fresh = HtmlParser.Parse(page.Html, options.Html, cancellationToken);
            var (host, scripts) = executeInlineScripts ? InlinePageScripts.Execute(fresh.Document, cancellationToken) : (null, 0);
            state = new(page, fresh, scripts, host);
        }
        try { return Publish(state, page, viewport, cancellationToken, reuseDocument); }
        catch { if (!reuseDocument && !ReferenceEquals(candidate, state)) { state.Release(); } throw; }
    }
    public bool DispatchesLinkClicks => executeInlineScripts;
    public Task<bool> DispatchLinkClickAsync(Guid documentId, int linkIndex, string url, CancellationToken cancellationToken) =>
        DispatchLinkEventAsync(documentId, linkIndex, url, PageLinkEventType.Click, cancellationToken);
    public Task<bool> DispatchLinkEventAsync(Guid documentId, int linkIndex, string url, PageLinkEventType eventType,
        CancellationToken cancellationToken) =>
        Task.FromResult(DispatchLinkEvent(documentId, linkIndex, url, eventType, cancellationToken));
    /// <summary>Dispatch a trusted link click into the committed document's retained host on the creating thread.</summary>
    public bool DispatchLinkClick(Guid documentId, int linkIndex, string url, CancellationToken cancellationToken) =>
        DispatchLinkEvent(documentId, linkIndex, url, PageLinkEventType.Click, cancellationToken);
    /// <summary>Dispatch a typed trusted link event into the committed document's retained host on the creating thread.</summary>
    /// <remarks>Specs: dom, pointerevents4;
    /// <see href="https://dom.spec.whatwg.org/#concept-event-dispatch">dispatch</see> and
    /// <see href="https://www.w3.org/TR/pointerevents4/#click-auxclick-and-contextmenu">click and auxclick</see>.
    /// The index and destination must match the latest successful render. Returns false when canceled. Failures
    /// and the shared per-document link event bound invalidate the host and make later retained repaints fail explicitly.</remarks>
    public bool DispatchLinkEvent(Guid documentId, int linkIndex, string url, PageLinkEventType eventType,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(url);
        cancellationToken.ThrowIfCancellationRequested();
        if (eventType is not (PageLinkEventType.Click or PageLinkEventType.AuxClick))
        { throw new ArgumentOutOfRangeException(nameof(eventType)); }
        if (!executeInlineScripts) { throw new PageNavigationException("Page link events require the inline-script opt-in."); }
        var state = committed?.Source.DocumentId == documentId ? committed
            : throw new PageNavigationException("Clicked document is not the committed retained document; reload explicitly.");
        if (state.Failure is { } failure) { throw new PageNavigationException(failure); }
        var host = state.Host ?? throw new PageNavigationException("Retained page script host is unavailable; reload explicitly.");
        if (linkIndex < 0 || linkIndex >= state.Links.Count || state.Links[linkIndex].Url != url
            || !state.Links[linkIndex].Anchor.IsConnected || state.Links[linkIndex].Anchor.OwnerDocument != state.Parsed.Document)
        { throw new PageNavigationException("Clicked link no longer matches the retained document; no event was dispatched."); }
        if (state.LinkEvents >= MaxLinkEventsPerDocument)
        {
            const string limit = "Per-document link event limit exceeded; page script host invalidated. Reload explicitly.";
            state.Fail(limit);
            throw new ScriptLimitException(limit);
        }
        state.LinkEvents++;
        try
        {
            return eventType switch
            {
                PageLinkEventType.Click => host.DispatchTrustedClick(state.Links[linkIndex].Anchor, cancellationToken),
                PageLinkEventType.AuxClick => host.DispatchTrustedAuxClick(state.Links[linkIndex].Anchor, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(eventType))
            };
        }
        catch (Exception exception)
        {
            state.Fail("Page script host invalidated by a failed link event; reload explicitly. " + exception.Message);
            throw;
        }
    }
    private BrowserPage Publish(DocumentState state, LoadedPage page, PageViewport viewport, CancellationToken cancellationToken,
        bool reuseDocument)
    {
        var parsed = state.Parsed;
        var sources = CollectStyles(parsed.Document, options.Css, cancellationToken, state.Source.Url,
            state.Source.Stylesheets, state.Source.BaseUrl);
        var rendered = OfflinePageRenderer.RenderParsed(parsed, sources, text, paint,
            viewport.Width, viewport.Height, options with { Scale = viewport.Scale, ScrollY = viewport.ScrollY }, cancellationToken);
        var (links, anchors, controlGeometry, labelGeometry, textTargets) = CollectLinks(parsed.Document, rendered.Layout, page.Url,
            Math.Min(viewport.ScrollY, rendered.ScrollHeight - viewport.Height), cancellationToken, page.BaseUrl);
        var (forms, controls) = PageForms.Collect(parsed.Document, page.Url, controlGeometry, labelGeometry, links.Count,
            rendered.Layout.ViewportWidth, rendered.Layout.ViewportHeight, cancellationToken, page.BaseUrl);
        var title = executeInlineScripts ? parsed.Document.Title
            : parsed.Document.Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title")?.TextContent;
        var status = $"Response {page.StatusCode}; HTML diagnostics: {parsed.Errors.Count}. " + string.Join(" ", page.Diagnostics);
        if (executeInlineScripts)
        {
            status += $" Post-parse inline scripts: {state.Scripts}; link-click listeners retained; no HTML scheduling/event loop.";
        }
        var result = new BrowserPage(rendered.Frame, string.IsNullOrWhiteSpace(title) ? page.Url.Href : title, status)
        {
            ScrollHeight = rendered.ScrollHeight,
            LinkTargets = links,
            FragmentTargets = CollectFragmentTargets(rendered.Layout, rendered.ScrollHeight, cancellationToken),
            Forms = forms,
            FormControls = controls,
            TextTargets = textTargets
        };
        cancellationToken.ThrowIfCancellationRequested();
        // Publication is the final non-throwing step: link identities and candidate ownership change together.
        state.Links = anchors.Zip(links, (anchor, link) => (anchor, link.Url)).ToArray();
        if (!reuseDocument)
        {
            var replaced = candidate;
            candidate = state;
            replaced?.Release();
        }
        return result;
    }
    private static (IReadOnlyList<PageLinkTarget> Links, IReadOnlyList<DomElement> Anchors,
        IReadOnlyDictionary<DomElement, (PageLinkRect, int)> Controls,
        IReadOnlyDictionary<DomElement, IReadOnlyList<PageLinkRect>> Labels,
        IReadOnlyList<PageTextTarget> TextTargets)
        CollectLinks(DomDocument document, LayoutResult layout, BrowserUrl url, double scrollY,
            CancellationToken cancellationToken, BrowserUrl? baseUrlOverride)
    {
        var baseTarget = FindBaseTarget(document, cancellationToken);
        var baseUrl = baseUrlOverride ?? FindBaseUrl(document, url, cancellationToken);
        var linkData = new List<LinkTargetData>();
        var controls = new Dictionary<DomElement, (PageLinkRect, int)>();
        var labelRectangles = new Dictionary<DomElement, List<PageLinkRect>>();
        var labelRectCount = 0;
        var anchors = new Dictionary<DomElement, LinkTargetData>();
        var anchorOrder = new List<DomElement>();
        var textTargets = new List<PageTextTarget>();
        long urlBytes = 0;
        long textBytes = 0;
        List<PageLinkTarget> links = [];
        if (layout.Root is { } root) { Visit(root); }
        links = linkData.Select(target => new PageLinkTarget(target.Rects.AsReadOnly(), target.Url,
            target.OpenInNewTab, target.TargetName, target.DocumentY)).ToList();
        try
        {
            RendererProtocol.ValidateLinks(links, layout.ViewportWidth, layout.ViewportHeight);
            RendererProtocol.ValidateTextTargets(textTargets, layout.ViewportWidth, layout.ViewportHeight);
        }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return (links.AsReadOnly(), anchorOrder.AsReadOnly(), controls,
            labelRectangles.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<PageLinkRect>)pair.Value.ToArray()),
            textTargets.AsReadOnly());

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
            var (openInNewTab, targetName) = ResolveTarget(anchor.GetAttribute("target"), baseTarget);
            var target = new LinkTargetData(destination, openInNewTab, targetName, documentY);
            anchors.Add(anchor, target);
            anchorOrder.Add(anchor);
            linkData.Add(target);
            return target;
        }

        void AddLinkRectangle(LinkTargetData target, PageLinkRect rect)
        {
            if (target.Rects.Count >= RendererProtocol.MaxLinkRects)
            { throw new PageNavigationException("Renderer per-anchor rectangle limit exceeded."); }
            target.Rects.Add(rect);
        }

        void AddLabelRectangle(DomElement label, PageLinkRect rect)
        {
            if (!labelRectangles.TryGetValue(label, out var rects))
            {
                if (labelRectangles.Count >= RendererProtocol.MaxFormLabelTargets)
                { throw new PageNavigationException("Renderer associated-label target count limit exceeded."); }
                rects = [];
                labelRectangles.Add(label, rects);
            }
            if (rects.Count > 0)
            {
                var previous = rects[^1];
                if (Math.Abs(previous.Y - rect.Y) <= 0.5 && Math.Abs(previous.Height - rect.Height) <= 0.5
                    && rect.X <= previous.X + previous.Width + 0.5 && rect.X + rect.Width >= previous.X - 0.5)
                {
                    var left = Math.Min(previous.X, rect.X);
                    var right = Math.Max(previous.X + previous.Width, rect.X + rect.Width);
                    rects[^1] = new(left, previous.Y, right - left, previous.Height);
                    return;
                }
            }
            if (rects.Count >= RendererProtocol.MaxFormLabelRectsPerControl
                || ++labelRectCount > RendererProtocol.MaxFormLabelRects)
            { throw new PageNavigationException("Renderer associated-label rectangle count limit exceeded."); }
            rects.Add(rect);
        }

        void SetBlockRectangle(LinkTargetData target, PageLinkRect rect, double documentY)
        {
            target.DocumentY = Math.Min(target.DocumentY, documentY);
            target.IsBlockBoxAnchor = true;
            if (target.BlockRectangle is null)
            {
                AddLinkRectangle(target, rect);
                target.BlockRectangle = rect;
                return;
            }

            var mergedLeft = Math.Min(target.BlockRectangle.X, rect.X);
            var mergedTop = Math.Min(target.BlockRectangle.Y, rect.Y);
            var mergedRight = Math.Max(target.BlockRectangle.X + target.BlockRectangle.Width, rect.X + rect.Width);
            var mergedBottom = Math.Max(target.BlockRectangle.Y + target.BlockRectangle.Height, rect.Y + rect.Height);
            var merged = new PageLinkRect(mergedLeft, mergedTop, mergedRight - mergedLeft, mergedBottom - mergedTop);
            var index = target.Rects.IndexOf(target.BlockRectangle);
            if (index >= 0) { target.Rects[index] = merged; }
            else { AddLinkRectangle(target, merged); }
            target.BlockRectangle = merged;
        }

        void AddTextTargets(ShapedRun run, double fragmentX, double top, double bottom)
        {
            var clusters = run.Glyphs.GroupBy(glyph => glyph.Cluster).OrderBy(group => group.Key).ToArray();
            if (clusters.Length == 0)
            {
                if (run.Text.Length > 0 && run.Width > 0)
                { throw new PageNavigationException("Shaped text has no selection clusters."); }
                return;
            }
            if (clusters[0].Key != 0)
            { throw new PageNavigationException("Shaped text selection clusters do not start at the beginning of the run."); }

            var expectedStart = 0;
            var clusterX = fragmentX;
            for (var index = 0; index < clusters.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cluster = clusters[index];
                var start = cluster.Key;
                var end = index + 1 < clusters.Length ? clusters[index + 1].Key : run.Text.Length;
                if (start != expectedStart || end <= start || end > run.Text.Length)
                { throw new PageNavigationException("Shaped text selection clusters are not a contiguous UTF-16 partition."); }

                var clusterWidth = cluster.Sum(glyph => glyph.Advance);
                if (!double.IsFinite(clusterWidth) || clusterWidth < 0)
                { throw new PageNavigationException("Shaped text selection cluster has invalid width."); }
                var left = Math.Clamp(clusterX, 0, layout.ViewportWidth);
                var right = Math.Clamp(clusterX + clusterWidth, 0, layout.ViewportWidth);
                if (clusterWidth > 0 && right > left && bottom > top)
                {
                    var text = run.Text[start..end];
                    if (textTargets.Count >= RendererProtocol.MaxTextTargets)
                    { throw new PageNavigationException("Renderer text target count limit exceeded."); }
                    textBytes += System.Text.Encoding.UTF8.GetByteCount(text);
                    if (textBytes > RendererProtocol.MaxTextMetadataBytes)
                    { throw new PageNavigationException("Renderer text target byte limit exceeded."); }
                    textTargets.Add(new(text, new(left, top, right - left, bottom - top)));
                }
                clusterX += clusterWidth;
                expectedStart = end;
            }
            if (expectedStart != run.Text.Length)
            { throw new PageNavigationException("Shaped text selection clusters do not cover the run."); }
        }

        static DomElement? FindAnchor(DomNode? node)
        {
            for (; node is not null; node = node.ParentNode)
            {
                if (node is DomElement { LocalName: "a", NamespaceUri: DomElement.HtmlNamespace } anchor) { return anchor; }
            }

            return null;
        }

        static DomElement? FindLabel(DomNode? node)
        {
            for (; node is not null; node = node.ParentNode)
            {
                if (node is not DomElement { NamespaceUri: DomElement.HtmlNamespace } element) { continue; }
                if (element.LocalName == "label") { return element; }
                if (element.LocalName is "a" or "button" or "input" or "select" or "textarea") { return null; }
            }
            return null;
        }

        void Visit(LayoutBox box)
        {
            foreach (var item in box.Flow)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is LayoutBlockItem block)
                {
                    if (block.Box.BorderBox.Width > 0 && block.Box.BorderBox.Height > 0
                        && FindAnchor(block.Box.Element) is { } anchor
                        && anchor.GetAttribute("href") is { } href)
                    {
                        var border = block.Box.BorderBox;
                        var target = GetLinkTarget(anchor, href, border.Y);
                        var left = Math.Clamp(border.X, 0, layout.ViewportWidth);
                        var right = Math.Clamp(border.X + border.Width, 0, layout.ViewportWidth);
                        var top = Math.Clamp(border.Y - scrollY, 0, layout.ViewportHeight);
                        var bottom = Math.Clamp(border.Y + border.Height - scrollY, 0, layout.ViewportHeight);
                        if (right > left && bottom > top && !target.Rects.Except(target.BlockRectangle is null ? [] : [target.BlockRectangle]).Any())
                        { SetBlockRectangle(target, new(left, top, right - left, bottom - top), border.Y); }
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
                        var anchor = FindAnchor(fragment.Source.ParentNode);
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
                        AddTextTargets(fragment.Run, fragment.X, top, bottom);
                        if (FindLabel(fragment.Source.ParentNode) is { } label) { AddLabelRectangle(label, rect); }
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

    /// <summary>Returns the valid URL selected by the first parsed base element with an href.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-base-element">base element</see>.
    /// Returns null for an absent, invalid, data, or javascript href, matching the response-URL fallback.</remarks>
    public static BrowserUrl? DiscoverBaseHrefUrl(string html, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(documentUrl);
        var document = HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document;
        return LinkedStylesheets.BaseHrefUrl(document, documentUrl, cancellationToken);
    }

    private static (bool OpenInNewTab, string? TargetName) ResolveTarget(string? target, string? baseTarget)
    {
        var name = (target ?? baseTarget)?.Trim(AsciiWhitespace);
        if (string.IsNullOrEmpty(name)) { return (false, null); }
        if (name.All(character => character <= 0x7f))
        {
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) { return (true, null); }
            if (name.Equals("_self", StringComparison.OrdinalIgnoreCase)
                || name.Equals("_parent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("_top", StringComparison.OrdinalIgnoreCase)) { return (false, null); }
            if (name.IndexOfAny(AsciiWhitespace) < 0) { return (false, name); }
        }
        throw new PageNavigationException("Unsupported hyperlink target.");
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
        CancellationToken cancellationToken = default, BrowserUrl? baseUrlOverride = null) =>
        DiscoverStylesheets(HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document, documentUrl,
            cancellationToken, baseUrlOverride);
    /// <inheritdoc cref="DiscoverStylesheets(string, BrowserUrl, CancellationToken, BrowserUrl)"/>
    public static IReadOnlyList<string> DiscoverStylesheets(DomDocument document, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default, BrowserUrl? baseUrlOverride = null) =>
        LinkedStylesheets.Discover(document, documentUrl, cancellationToken, baseUrlOverride);
    /// <summary>Import URLs referenced by supported embedded stylesheets, resolved against the document base URL.</summary>
    /// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#at-import">import rules</see>.
    /// Only unconditional imports before all other rules are supported; conditions and cascade layers fail visibly.</remarks>
    public static IReadOnlyList<string> DiscoverInlineStylesheetImports(string html, BrowserUrl documentUrl,
        CancellationToken cancellationToken = default, BrowserUrl? baseUrlOverride = null)
    {
        var document = HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document;
        var baseUrl = baseUrlOverride ?? LinkedStylesheets.BaseUrl(document, documentUrl, cancellationToken);
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
            _ => allowInlineStyles, cancellationToken);
    }

    /// <summary>Rejects inline script or style markup disabled by browser-selected response policy.</summary>
    /// <remarks>Spec: csp3;
    /// <see href="https://www.w3.org/TR/CSP3/#should-block-inline">should element's inline type behavior be blocked</see>.
    /// Nonces apply only to script/style elements; hashes also apply to style attributes with 'unsafe-hashes'.</remarks>
    public static void EnforceInlineContentPolicy(string html, Func<string?, bool> allowsInlineScripts,
        Func<string?, bool> allowsInlineStyles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(allowsInlineScripts);
        ArgumentNullException.ThrowIfNull(allowsInlineStyles);
        EnforceInlineContentPolicy(html, (nonce, _) => allowsInlineScripts(nonce),
            (nonce, _) => allowsInlineStyles(nonce), _ => allowsInlineStyles(null), cancellationToken);
    }

    /// <summary>Applies nonce/hash decisions to inline elements and blocks disallowed style attributes.</summary>
    /// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#should-block-inline">inline type checks</see>.</remarks>
    public static void EnforceInlineContentPolicy(string html, Func<string?, string, bool> allowsInlineScripts,
        Func<string?, string, bool> allowsInlineStyles, bool allowInlineStyleAttributes,
        CancellationToken cancellationToken = default) => EnforceInlineContentPolicy(html, allowsInlineScripts,
            allowsInlineStyles, _ => allowInlineStyleAttributes, cancellationToken);

    /// <summary>Applies inline-element checks and source-sensitive style-attribute authorization.</summary>
    /// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#match-element-to-source-list">match an element to a source list</see>.</remarks>
    public static void EnforceInlineContentPolicy(string html, Func<string?, string, bool> allowsInlineScripts,
        Func<string?, string, bool> allowsInlineStyles, Func<string, bool> allowsInlineStyleAttributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(allowsInlineScripts);
        ArgumentNullException.ThrowIfNull(allowsInlineStyles);
        ArgumentNullException.ThrowIfNull(allowsInlineStyleAttributes);
        var document = HtmlParser.Parse(html, DocumentHtmlOptions, cancellationToken).Document;
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "script" && element.GetAttribute("src") is null
                && !allowsInlineScripts(element.GetAttribute("nonce"), element.TextContent ?? string.Empty))
            {
                throw new PageNavigationException("Content-Security-Policy blocked an inline script element.");
            }
            var styleAttribute = element.GetAttribute("style");
            if ((element.LocalName == "style"
                    && !allowsInlineStyles(element.GetAttribute("nonce"), element.TextContent ?? string.Empty))
                || (styleAttribute is not null && !allowsInlineStyleAttributes(styleAttribute)))
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
        IReadOnlyList<PageStylesheet>? stylesheets = null, BrowserUrl? baseUrlOverride = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var limits = options ?? new();
        if (limits.MaxStyleSources <= 0 || limits.MaxInputCharacters <= 0) { throw new ArgumentOutOfRangeException(nameof(options)); }
        var provided = (stylesheets ?? []).ToDictionary(sheet => sheet.Url, sheet => sheet, StringComparer.Ordinal);
        var sources = new List<CssStyleSource>();
        long characters = 0;
        var baseUrl = baseUrlOverride ?? (documentUrl is null
            ? null
            : LinkedStylesheets.BaseUrl(document, documentUrl, cancellationToken));
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
        disposed = true;
        var retained = committed;
        var pending = candidate;
        committed = null; candidate = null;
        try
        {
            try { pending?.Release(); }
            finally { retained?.Release(); }
        }
        finally { paint.Dispose(); font.Dispose(); }
    }
}
