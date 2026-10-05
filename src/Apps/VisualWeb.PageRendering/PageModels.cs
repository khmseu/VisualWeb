using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;

namespace VisualWeb.PageRendering;

public sealed class PageNavigationException(string message) : Exception(message);
public sealed record LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics)
{
    public Guid DocumentId { get; init; } = Guid.NewGuid();
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
