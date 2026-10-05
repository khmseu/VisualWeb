using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;

namespace VisualWeb.PageRendering;

public sealed class PageNavigationException(string message) : Exception(message);
public sealed record LoadedPage(BrowserUrl Url, string Html, int StatusCode, IReadOnlyList<string> Diagnostics);
public readonly record struct PageViewport(double Width, double Height, double Scale);
public sealed record BrowserPage(RasterFrame Frame, string Title, string Status);

public interface IPageRenderer : IDisposable
{
    Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken);
    string? TakeFailure() => null;
}
