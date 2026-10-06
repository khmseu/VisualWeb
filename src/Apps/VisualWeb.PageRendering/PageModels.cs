using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;

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

    public void Deconstruct(out BrowserUrl Url, out string Html, out int StatusCode, out IReadOnlyList<string> Diagnostics)
    {
        Url = this.Url;
        Html = this.Html;
        StatusCode = this.StatusCode;
        Diagnostics = this.Diagnostics;
    }
}
public readonly record struct PageViewport(double Width, double Height, double Scale);
public sealed record BrowserPage(RasterFrame Frame, string Title, string Status);

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
