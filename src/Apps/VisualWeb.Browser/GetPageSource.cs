using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

public interface IPageSource : IDisposable
{
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken);
}

/// <summary>Browser-owned GET navigation, without ambient cookies or renderer network capabilities.</summary>
/// <remarks>Specs: fetch, encoding; <see href="https://fetch.spec.whatwg.org/#scheme-fetch">scheme fetch</see>,
/// <see href="https://encoding.spec.whatwg.org/#decode">BOM-first decode</see>.</remarks>
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
