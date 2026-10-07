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
/// a stale origin. This is identity only, not inherited/sandbox origin selection or policy enforcement.
/// Renderer reconstruction creates a worker-local origin; no browser principal is sent over IPC.</remarks>
public sealed record LoadedPage
{
    public LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics)
    {
        ArgumentNullException.ThrowIfNull(Url);
        this.Url = Url;
        this.Html = Html;
        this.StatusCode = StatusCode;
        this.Diagnostics = Diagnostics;
        Origin = Url.Origin.IsOpaque ? SecurityOrigin.CreateOpaque() : Url.Origin;
    }

    public BrowserUrl Url { get; }
    public SecurityOrigin Origin { get; }
    public string Html { get; init; }
    public int StatusCode { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; }
    public Guid DocumentId { get; init; } = Guid.NewGuid();
    /// <summary>Canonical WHATWG encoding name used to decode the document; selects the form submission encoding.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#pick-an-encoding-for-the-form">pick
    /// an encoding for the form</see>. Browser-only metadata; it is not sent to renderers.</remarks>
    public string CharacterEncoding { get; init; } = "UTF-8";
    private readonly IReadOnlyList<PageStylesheet> stylesheets = Array.Empty<PageStylesheet>();
    /// <summary>Browser-fetched linked stylesheet texts keyed by resolved request URL; published with the document.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
    /// "stylesheet"</see>. A bounded immutable data snapshot (<see cref="RendererProtocol.ValidateStylesheets"/>);
    /// renderers never fetch, so links absent from this collection fail rendering.</remarks>
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
}
public sealed record BrowserPage(RasterFrame Frame, string Title, string Status)
{
    public IReadOnlyList<PageLinkTarget> LinkTargets { get; init; } = Array.Empty<PageLinkTarget>();
    /// <summary>Bounded form owners in tree order; see <see cref="RendererProtocol.ValidateForms"/>.</summary>
    public IReadOnlyList<PageForm> Forms { get; init; } = Array.Empty<PageForm>();
    /// <summary>Supported controls in tree order with initial values; the browser owns edits and submission.</summary>
    public IReadOnlyList<PageFormControl> FormControls { get; init; } = Array.Empty<PageFormControl>();
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

public interface IPageRenderer : IDisposable
{
    Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken);
    /// <summary>Repaint a committed document without repeating script execution.</summary>
    Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken);
    /// <summary>Pin a successfully published document; unpublished candidates cannot evict it.</summary>
    void CommitDocument(Guid documentId) { }
    string? TakeFailure() => null;
}
