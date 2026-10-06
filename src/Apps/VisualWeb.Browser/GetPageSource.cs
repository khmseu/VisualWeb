using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

public interface IPageSource : IDisposable
{
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken);
}

/// <summary>Browser-owned GET navigation, without ambient cookies or renderer network capabilities.</summary>
/// <remarks>Specs: fetch, encoding; <see href="https://fetch.spec.whatwg.org/#scheme-fetch">scheme fetch</see>,
/// <see href="https://encoding.spec.whatwg.org/#decode">BOM-first decode</see>; html,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#encoding-sniffing-algorithm">encoding sniffing</see> subset
/// via <see cref="HtmlEncodingSniffer"/>. Unlike the spec, an unknown Content-Type charset fails visibly unless a BOM decides.</remarks>
public sealed class GetPageSource : IPageSource
{
    private readonly Engine.Net.ResourceLoader loader;
    /// <summary>Creates a tab-local loader, optionally using a caller-owned session HSTS store.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8">UA processing</see>.
    /// Cookies remain disabled. Standalone sources without a supplied store retain their prior behavior.</remarks>
    public GetPageSource(HttpMessageHandler? handler = null, Engine.Net.HstsPolicyStore? hstsPolicyStore = null) =>
        loader = new(new() { MaxResponseBytes = 4 * 1024 * 1024 }, handler, hstsPolicyStore);
    public async Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
    {
        var response = await loader.LoadAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.ContentType?.Essence != "text/html"
            && !(response.Url.Protocol == "file:" && response.ContentType is null))
        {
            throw new PageNavigationException("Only text/html (or explicitly selected local HTML files) can be rendered; MIME sniffing is deferred.");
        }
        string? label = null;
        response.ContentType?.Parameters.TryGetValue("charset", out label);
        var sniffed = HtmlEncodingSniffer.Sniff(response.Body.Span, label, WebEncoding.ForLabel("UTF-8"));
        if (sniffed.Source != HtmlEncodingSource.ByteOrderMark && sniffed.UnsupportedTransportLabel is { } unsupported)
        {
            throw new PageNavigationException($"Unknown web encoding label: {unsupported}");
        }

        var decoded = response.DecodeText(sniffed.Encoding);
        return new(response.Url, decoded.Text, response.StatusCode,
            response.Diagnostics.Append(Describe(sniffed)).ToArray());
    }

    private static string Describe(HtmlEncodingSniffResult sniffed)
    {
        var name = sniffed.Encoding.Name;
        var text = sniffed.Source switch
        {
            HtmlEncodingSource.ByteOrderMark => $"Encoding: {name} from byte order mark (certain)",
            HtmlEncodingSource.TransportLayer => $"Encoding: {name} from Content-Type charset (certain)",
            HtmlEncodingSource.Prescan => $"Encoding: {name} from {Declaration(sniffed.Prescan!.Source)} prescan at byte "
                + $"{sniffed.Prescan.DeclarationOffset} (tentative)",
            _ => $"Encoding: {name} from default fallback; no BOM, Content-Type charset or declaration in the first "
                + $"{HtmlEncodingPrescanner.ByteLimit} bytes (tentative)",
        };
        if (sniffed.UnsupportedTransportLabel is { } unsupported)
        {
            text += $"; ignored unknown Content-Type charset \"{unsupported}\"";
        }

        if (sniffed.Prescan?.IgnoredLabels is { Count: > 0 } ignored)
        {
            text += "; ignored unknown <meta> labels: " + string.Join(", ", ignored.Select(item => $"\"{item}\""));
        }

        return text + "; no statistical/locale sniffing or reparse on later declarations.";
    }

    private static string Declaration(HtmlPrescanSource source) => source switch
    {
        HtmlPrescanSource.MetaCharset => "<meta charset>",
        HtmlPrescanSource.MetaPragma => "<meta http-equiv content-type>",
        HtmlPrescanSource.XmlDeclaration => "<?xml encoding>",
        _ => "UTF-16 <?x prefix",
    };

    public void Dispose() => loader.Dispose();
}
