using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

public sealed class PageNavigationException(string message) : Exception(message);
/// <summary>A loaded document with a fixed final response URL and immutable origin identity.</summary>
/// <remarks>Specs: url, html;
/// <see href="https://url.spec.whatwg.org/#concept-url-origin">URL origin</see> and
/// <see href="https://html.spec.whatwg.org/multipage/browsers.html#concept-origin-opaque">opaque origin</see>.
/// Each construction represents a new document: tuple origins come from the final URL, while opaque
/// origins are fresh even when that URL instance is reused. Record copies retain the same document
/// identity and origin. URL replacement requires a new document, preventing a copied URL from leaving
/// a stale origin. The explicit about:blank factory can select the initiating document's origin for the
/// supported top-level page-navigation case. This is identity only, not sandbox selection or policy
/// enforcement. Renderer reconstruction creates a worker-local origin; no browser principal is sent over IPC.</remarks>
public sealed record LoadedPage
{
    public LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics)
        : this(Url, Html, StatusCode, Diagnostics, originOverride: null)
    {
    }

    private LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics,
        SecurityOrigin? originOverride)
    {
        ArgumentNullException.ThrowIfNull(Url);
        this.Url = Url;
        this.Html = Html;
        this.StatusCode = StatusCode;
        this.Diagnostics = Diagnostics;
        Origin = originOverride ?? (Url.Origin.IsOpaque ? SecurityOrigin.CreateOpaque() : Url.Origin);
    }

    /// <summary>Creates a blank document with either its inherited creator origin or a fresh opaque origin.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsers.html#determining-the-origin"/>.
    /// This explicit factory is limited to <c>about:blank</c>. Callers must supply the committed creator origin
    /// only for supported page-initiated creation; browser-UI navigation passes no creator and remains opaque.
    /// This does not implement sandbox flags or nested browsing contexts.</remarks>
    public static LoadedPage CreateAboutBlank(BrowserUrl url, SecurityOrigin? creatorOrigin = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAboutBlank) { throw new ArgumentException("The URL must match about:blank.", nameof(url)); }
        return new(url, "<!doctype html><html><head></head><body></body></html>", 200, Array.Empty<string>(),
            creatorOrigin ?? SecurityOrigin.CreateOpaque());
    }

    public BrowserUrl Url { get; }
    public SecurityOrigin Origin { get; }
    /// <summary>Optional browser-approved document base URL override, retained for local and process rendering.</summary>
    /// <remarks>Specs: html, csp3;
    /// <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-base-element">base element</see> and
    /// <see href="https://www.w3.org/TR/CSP3/#directive-base-uri">base-uri</see>.
    /// Data only; null preserves renderer-side base discovery.</remarks>
    public BrowserUrl? BaseUrl { get; init; }
    public string Html { get; init; }
    public int StatusCode { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; }
    private readonly IReadOnlyList<string> contentSecurityPolicyFields = Array.Empty<string>();
    /// <summary>Immutable response policy fields retained for browser-owned form navigation checks.</summary>
    /// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#directive-form-action">form-action</see>.
    /// Browser metadata only; renderer processes receive no policy callbacks or mutable policy state.</remarks>
    public IReadOnlyList<string> ContentSecurityPolicyFields
    {
        get => contentSecurityPolicyFields;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            contentSecurityPolicyFields = Array.AsReadOnly(value.ToArray());
        }
    }
    public Guid DocumentId { get; init; } = Guid.NewGuid();
    /// <summary>Canonical WHATWG encoding name used to decode the document; selects the form submission encoding.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#pick-an-encoding-for-the-form">pick
    /// an encoding for the form</see>. Browser-only metadata; it is not sent to renderers.</remarks>
    public string CharacterEncoding { get; init; } = "UTF-8";
    private readonly IReadOnlyList<PageStylesheet> stylesheets = Array.Empty<PageStylesheet>();
    /// <summary>Browser-fetched linked and imported stylesheet texts keyed by request URL; published with the document.</summary>
    /// <remarks>Specs: html, css-cascade; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link
    /// type "stylesheet"</see> and <see href="https://www.w3.org/TR/css-cascade-5/#at-import">CSS @import</see>.
    /// A bounded immutable data snapshot (<see cref="RendererProtocol.ValidateStylesheets"/>); renderers never fetch,
    /// so missing sources fail rendering.</remarks>
    public IReadOnlyList<PageStylesheet> Stylesheets
    {
        get => stylesheets;
        init
        {
            var copy = value?.ToArray();
            try { RendererProtocol.ValidateStylesheets(copy); }
            catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
            stylesheets = Array.AsReadOnly(copy!);
        }
    }

    public void Deconstruct(out BrowserUrl Url, out string Html, out int StatusCode, out IReadOnlyList<string> Diagnostics)
    {
        Url = this.Url;
        Html = this.Html;
        StatusCode = this.StatusCode;
        Diagnostics = this.Diagnostics;
    }
}
/// <summary>Unchanged CSS viewport geometry and a vertical canvas offset.</summary>
/// <remarks>Spec: css2-visual;
/// <see href="https://www.w3.org/TR/CSS22/visuren.html#viewport">viewport</see>.</remarks>
public readonly record struct PageViewport(double Width, double Height, double Scale)
{
    public double ScrollY { get; init; }
    public IReadOnlyList<PageScrollOffset> ScrollOffsets { get; init; } = Array.Empty<PageScrollOffset>();
}
public sealed record BrowserPage(RasterFrame Frame, string Title, string Status)
{
    public IReadOnlyList<PageLinkTarget> LinkTargets { get; init; } = Array.Empty<PageLinkTarget>();
    /// <summary>Rendered element IDs and named-anchor targets with document-space vertical offsets for browser-owned fragment scrolling.</summary>
    public IReadOnlyList<PageFragmentTarget> FragmentTargets { get; init; } = Array.Empty<PageFragmentTarget>();
    /// <summary>Bounded form owners in tree order; see <see cref="RendererProtocol.ValidateForms"/>.</summary>
    public IReadOnlyList<PageForm> Forms { get; init; } = Array.Empty<PageForm>();
    /// <summary>Supported controls in tree order with initial values; the browser owns edits and submission.</summary>
    public IReadOnlyList<PageFormControl> FormControls { get; init; } = Array.Empty<PageFormControl>();
    /// <summary>Visible shaped text clusters in paint order, with browser-only selection geometry.</summary>
    public IReadOnlyList<PageTextTarget> TextTargets { get; init; } = Array.Empty<PageTextTarget>();
    /// <summary>Visible vertical nested scroll containers for browser-owned wheel routing.</summary>
    public IReadOnlyList<PageScrollContainer> ScrollContainers { get; init; } = Array.Empty<PageScrollContainer>();
    private readonly double scrollHeight;
    public double ScrollHeight
    {
        get => scrollHeight;
        init
        {
            if (!double.IsFinite(value) || value < 0 || value > PaintOptions.MaxDocumentHeight)
            { throw new ArgumentOutOfRangeException(nameof(value), "Document scroll height exceeds the CSS-pixel bound."); }
            scrollHeight = value;
        }
    }
}

/// <summary>The finite trusted link event dispatched before a browser-owned link activation default.</summary>
/// <remarks>Spec: pointerevents4; <see href="https://www.w3.org/TR/pointerevents4/#click-auxclick-and-contextmenu">click and auxclick events</see>.
/// Only <c>Click</c> and <c>AuxClick</c> are supported; neither event includes pointer metadata in this subset.</remarks>
public enum PageLinkEventType { Click, AuxClick }

public interface IPageRenderer : IDisposable
{
    Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken);
    /// <summary>Repaint a committed document without repeating script execution.</summary>
    Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken);
    /// <summary>Pin a successfully published document; unpublished candidates cannot evict it.</summary>
    void CommitDocument(Guid documentId) { }
    /// <summary>Whether committed documents retain an opted-in script host that receives trusted link events.</summary>
    bool DispatchesLinkClicks => false;
    /// <summary>Dispatch a trusted, bubbling, cancelable click or auxclick Event at a committed rendered link anchor.</summary>
    /// <remarks>Specs: dom, pointerevents4;
    /// <see href="https://dom.spec.whatwg.org/#concept-event-dispatch">dispatch</see> and
    /// <see href="https://www.w3.org/TR/pointerevents4/#click-auxclick-and-contextmenu">click and auxclick</see>.
    /// Data only: the link is identified by its rendered index and destination; no DOM or V8 object crosses the
    /// boundary. Completes with false when a listener canceled the event, suppressing browser-owned activation.
    /// Both are plain Event objects, not the PointerEvent required by the full specification.</remarks>
    Task<bool> DispatchLinkClickAsync(Guid documentId, int linkIndex, string url, CancellationToken cancellationToken) =>
        DispatchLinkEventAsync(documentId, linkIndex, url, PageLinkEventType.Click, cancellationToken);
    /// <summary>Dispatch one of the bounded trusted link event types into a committed retained document.</summary>
    /// <remarks>Spec: pointerevents4; <see href="https://www.w3.org/TR/pointerevents4/#click-auxclick-and-contextmenu">click and auxclick</see>.</remarks>
    Task<bool> DispatchLinkEventAsync(Guid documentId, int linkIndex, string url, PageLinkEventType eventType,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This renderer does not dispatch page link events.");
    string? TakeFailure() => null;
}
