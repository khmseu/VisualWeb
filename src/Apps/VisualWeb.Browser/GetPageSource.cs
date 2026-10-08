using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Net;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.Browser;

public interface IPageSource : IDisposable
{
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken);
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade) =>
        LoadAsync(url, cancellationToken);
}

/// <summary>Browser-owned GET navigation, without ambient cookies or renderer network capabilities.</summary>
/// <remarks>Specs: fetch, encoding; <see href="https://fetch.spec.whatwg.org/#scheme-fetch">scheme fetch</see>,
/// <see href="https://encoding.spec.whatwg.org/#decode">BOM-first decode</see>; html,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#encoding-sniffing-algorithm">encoding sniffing</see> subset
/// via <see cref="HtmlEncodingSniffer"/>. Unlike the spec, an unknown Content-Type charset fails visibly unless a BOM decides.
/// Supported classic <c>link rel=stylesheet</c> subresources (spec: html;
/// <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type "stylesheet"</see>) are
/// discovered from the pre-script parse and fetched here with the same tab loader and session HSTS store, without cookies,
/// before the document is published. Each must be an ok text/css response (or a MIME-less file from a file document) decoded
/// per css-syntax <see href="https://www.w3.org/TR/css-syntax-3/#determine-the-fallback-encoding">fallback encoding</see>;
/// unknown labels fail visibly. Any stylesheet failure fails the whole navigation. CSS @import, url() resources, fonts,
/// images and CORS are not fetched. HTTP(S) stylesheet requests are restricted to the final document's fixed origin,
/// including redirects; this deliberate restriction is not CORS or general request authorization.</remarks>
public sealed class GetPageSource : IPageSource
{
    private readonly Engine.Net.ResourceLoader loader;
    /// <summary>Creates a tab-local loader, optionally using a caller-owned session HSTS store.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8">UA processing</see>.
    /// Cookies remain disabled. Standalone sources without a supplied store retain their prior behavior.</remarks>
    public GetPageSource(HttpMessageHandler? handler = null, Engine.Net.HstsPolicyStore? hstsPolicyStore = null) =>
        loader = new(new() { MaxResponseBytes = 4 * 1024 * 1024 }, handler, hstsPolicyStore);
    public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken) =>
        LoadAsync(url, cancellationToken, preventHttpsDowngrade: false);
    public async Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade)
    {
        var response = await loader.LoadAsync(url, cancellationToken: cancellationToken,
            preventHttpsDowngrade: preventHttpsDowngrade).ConfigureAwait(false);
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
        var diagnostics = response.Diagnostics.Append(Describe(sniffed)).ToList();
        var stylesheets = await LoadStylesheetsAsync(response.Url, decoded.Text, sniffed.Encoding, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        return new(response.Url, decoded.Text, response.StatusCode, diagnostics.ToArray())
        { Stylesheets = stylesheets, CharacterEncoding = sniffed.Encoding.Name };
    }

    private async Task<PageStylesheet[]> LoadStylesheetsAsync(BrowserUrl documentUrl, string html, WebEncoding documentEncoding,
        List<string> diagnostics, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> urls;
        try { urls = StaticPageRenderer.DiscoverStylesheets(html, documentUrl, cancellationToken); }
        // The renderer parses identically and reports the same failure; nothing is fetched for an unparsable document.
        catch (Exception exception) when (exception is HtmlLimitException or UnsupportedHtmlException) { return []; }
        if (urls.Count == 0) { return []; }
        var sheets = new PageStylesheet[urls.Count];
        for (var i = 0; i < urls.Count; i++)
        {
            sheets[i] = new(urls[i], await LoadStylesheetAsync(BrowserUrl.Parse(urls[i]), documentUrl, documentEncoding,
                cancellationToken).ConfigureAwait(false));
        }
        diagnostics.Add($"Linked stylesheets: {sheets.Length} browser-fetched classic text/css; @import, url() resources, "
            + "fonts, images and CORS are not loaded.");
        return sheets;
    }

    private async Task<string> LoadStylesheetAsync(BrowserUrl url, BrowserUrl documentUrl, WebEncoding documentEncoding,
        CancellationToken cancellationToken)
    {
        var secureDocument = documentUrl.Protocol == "https:";
        ResourceResponse response;
        try
        {
            response = url.Protocol is "http:" or "https:"
                ? await loader.LoadSameOriginAsync(url, documentUrl.Origin, includeCookies: false, cancellationToken)
                    .ConfigureAwait(false)
                : await loader.LoadAsync(url, includeCookies: false, cancellationToken,
                    preventHttpsDowngrade: secureDocument).ConfigureAwait(false);
        }
        catch (ResourceLoadException exception)
        { throw new PageNavigationException($"Linked stylesheet {url.Href} failed to load: {exception.Message}"); }
        if (secureDocument && response.Url.Protocol == "http:")
        {
            throw new PageNavigationException($"Linked stylesheet {url.Href} is blocked: an HTTPS stylesheet redirected to HTTP.");
        }
        if (response.StatusCode is < 200 or > 299)
        { throw new PageNavigationException($"Linked stylesheet {url.Href} returned HTTP {response.StatusCode}."); }
        if (response.ContentType?.Essence != "text/css"
            && !(response.Url.Protocol == "file:" && documentUrl.Protocol == "file:" && response.ContentType is null))
        {
            throw new PageNavigationException($"Linked stylesheet {url.Href} is not text/css "
                + $"({response.ContentType?.Essence ?? "no Content-Type"}); MIME sniffing and quirks-mode CSS are unsupported.");
        }
        if (response.Body.Length > RendererProtocol.MaxStylesheetBytes)
        { throw new PageNavigationException($"Linked stylesheet {url.Href} exceeds the {RendererProtocol.MaxStylesheetBytes}-byte limit."); }
        var text = response.DecodeText(CssFallbackEncoding(response, documentEncoding, url)).Text;
        if (text.Length > RendererProtocol.MaxStylesheetCharacters)
        { throw new PageNavigationException($"Linked stylesheet {url.Href} exceeds the {RendererProtocol.MaxStylesheetCharacters}-character limit."); }
        return text;
    }

    /// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#determine-the-fallback-encoding">determine
    /// the fallback encoding</see>, with the document's encoding as the HTML environment encoding. A BOM still wins in
    /// decoding. Stricter than the spec: unknown transport or @charset labels fail instead of falling through.</remarks>
    private static WebEncoding CssFallbackEncoding(ResourceResponse response, WebEncoding documentEncoding, BrowserUrl url)
    {
        var body = response.Body.Span;
        if (WebEncoding.SniffBom(body, out _) is { } bom) { return bom; }
        if (response.ContentType?.Parameters.TryGetValue("charset", out var label) == true)
        {
            return WebEncoding.TryForLabel(label, out var transport) ? transport
                : throw new PageNavigationException($"Linked stylesheet {url.Href} has unknown Content-Type charset \"{label}\".");
        }
        ReadOnlySpan<byte> prefix = "@charset \""u8;
        var head = body[..Math.Min(body.Length, 1024)];
        if (head.StartsWith(prefix))
        {
            var labelBytes = head[prefix.Length..];
            var quote = labelBytes.IndexOf((byte)0x22);
            if (quote >= 0 && quote + 1 < labelBytes.Length && labelBytes[quote + 1] == 0x3B
                && !labelBytes[..quote].ContainsAnyInRange((byte)0x80, (byte)0xFF))
            {
                var declared = System.Text.Encoding.ASCII.GetString(labelBytes[..quote]);
                if (!WebEncoding.TryForLabel(declared, out var encoding))
                { throw new PageNavigationException($"Linked stylesheet {url.Href} has unknown @charset \"{declared}\"."); }
                return encoding.Name is "UTF-16BE" or "UTF-16LE" ? WebEncoding.ForLabel("UTF-8") : encoding;
            }
        }
        return documentEncoding;
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
