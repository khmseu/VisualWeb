using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Net;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.Browser;

public interface IPageSource : IDisposable
{
    /// <summary>Checks initial page-navigation policy without starting a load; loading still checks every redirect.</summary>
    void ValidateNavigationTarget(BrowserUrl url, bool preventHttpsDowngrade, SecurityOrigin? sameOriginRedirectOrigin) =>
        PageNavigationPolicy.Validate(url, preventHttpsDowngrade, sameOriginRedirectOrigin);
    /// <summary>Authorizes an HTTP(S) page-navigation target and returns the fixed effective origin for its redirect chain.</summary>
    SecurityOrigin AuthorizeNavigationTarget(BrowserUrl url, bool preventHttpsDowngrade)
    {
        ValidateNavigationTarget(url, preventHttpsDowngrade, url.Origin);
        return url.Origin;
    }

    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken);
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade)
    {
        if (preventHttpsDowngrade)
        {
            throw new PageNavigationException("The configured page source does not enforce HTTPS downgrade protection.");
        }
        return LoadAsync(url, cancellationToken);
    }
    Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade,
        SecurityOrigin? sameOriginRedirectOrigin)
    {
        if (sameOriginRedirectOrigin is not null)
        {
            throw new PageNavigationException("The configured page source does not enforce fixed-origin redirects.");
        }
        return LoadAsync(url, cancellationToken, preventHttpsDowngrade);
    }
    Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url, CancellationToken cancellationToken,
        bool preventHttpsDowngrade) =>
        throw new PageNavigationException("The configured page source does not enforce per-hop navigation authorization.");
    Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url,
        Func<BrowserUrl, int, CancellationToken, ValueTask<bool>> authorizeDestination,
        CancellationToken cancellationToken, bool preventHttpsDowngrade) =>
        throw new PageNavigationException("The configured page source does not enforce asynchronous per-hop navigation authorization.");
    Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url,
        Func<BrowserUrl, int, CancellationToken, ValueTask<bool>> authorizeDestination,
        CancellationToken cancellationToken, bool preventHttpsDowngrade,
        Func<BrowserUrl, BrowserUrl>? rewriteDestination) =>
        throw new PageNavigationException("The configured page source does not enforce asynchronous per-hop navigation authorization and destination rewriting.");
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
/// unknown labels fail visibly. Unconditional leading CSS <c>@import</c> rules in embedded and fetched stylesheets are
/// brokered recursively under the same count/character/wire bounds and inserted at their cascade positions. Any stylesheet
/// failure fails the whole navigation. Conditional/layer imports, <c>url()</c> resources, fonts and images are not
/// fetched. Same-origin HTTP(S) stylesheet requests retain fixed-origin redirect checks. Cross-origin HTTP(S) sheets
/// use a credentialless CORS subset (spec: fetch;
/// <see href="https://fetch.spec.whatwg.org/#cors-check">CORS check</see>): the final response must allow the document
/// origin or <c>*</c>, and redirects must remain same-origin with the effective initial stylesheet URL. A bounded
/// final-response Content Security Policy
/// additionally rejects inline script/style content, applies style-src-elem/style-src/default-src to linked/imported
/// stylesheet destinations before requests and redirects, and upgrades insecure stylesheet URLs when the response policy
/// includes <c>upgrade-insecure-requests</c> (spec ID upgrade-insecure-requests;
/// <see href="https://www.w3.org/TR/upgrade-insecure-requests/#upgrade-request">request upgrading</see>). The
/// development shell also applies it to same-origin page-initiated HTTP links and GET form submissions, including
/// redirects; cross-origin link navigation is not upgraded (forms are the exception). Explicit address-bar navigation
/// does not inherit the page's upgrade policy. This is not a complete Fetch/CORS implementation or general request
/// authorization.</remarks>
public sealed class GetPageSource : IPageSource
{
    private readonly Engine.Net.ResourceLoader loader;
    private readonly HstsPolicyStore? hstsPolicyStore;
    /// <summary>Creates a tab-local loader, optionally using a caller-owned session HSTS store.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8">UA processing</see>.
    /// Cookies remain disabled. Standalone sources without a supplied store retain their prior behavior.</remarks>
    public GetPageSource(HttpMessageHandler? handler = null, Engine.Net.HstsPolicyStore? hstsPolicyStore = null)
    {
        this.hstsPolicyStore = hstsPolicyStore;
        loader = new(new() { MaxResponseBytes = 4 * 1024 * 1024 }, handler, hstsPolicyStore);
    }
    public void ValidateNavigationTarget(BrowserUrl url, bool preventHttpsDowngrade, SecurityOrigin? sameOriginRedirectOrigin) =>
        PageNavigationPolicy.Validate(hstsPolicyStore?.Upgrade(url) ?? url, preventHttpsDowngrade, sameOriginRedirectOrigin);
    public SecurityOrigin AuthorizeNavigationTarget(BrowserUrl url, bool preventHttpsDowngrade)
    {
        var effective = hstsPolicyStore?.Upgrade(url) ?? url;
        PageNavigationPolicy.Validate(effective, preventHttpsDowngrade, effective.Origin);
        return effective.Origin;
    }
    public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken) =>
        LoadAsync(url, cancellationToken, preventHttpsDowngrade: false);
    public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade) =>
        LoadAsync(url, cancellationToken, preventHttpsDowngrade, sameOriginRedirectOrigin: null);
    public async Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken, bool preventHttpsDowngrade,
        SecurityOrigin? sameOriginRedirectOrigin)
    {
        var response = sameOriginRedirectOrigin is null
            ? await loader.LoadAsync(url, cancellationToken: cancellationToken,
                preventHttpsDowngrade: preventHttpsDowngrade).ConfigureAwait(false)
            : await loader.LoadSameOriginAsync(url, sameOriginRedirectOrigin, cancellationToken: cancellationToken,
                preventHttpsDowngrade: preventHttpsDowngrade).ConfigureAwait(false);
        return await CreateLoadedPageAsync(response, cancellationToken).ConfigureAwait(false);
    }
    public async Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url, CancellationToken cancellationToken,
        bool preventHttpsDowngrade)
    {
        var response = await loader.LoadAuthorizedNavigationAsync(url, static _ => true,
            cancellationToken: cancellationToken, preventHttpsDowngrade: preventHttpsDowngrade).ConfigureAwait(false);
        return await CreateLoadedPageAsync(response, cancellationToken).ConfigureAwait(false);
    }
    public async Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url,
        Func<BrowserUrl, int, CancellationToken, ValueTask<bool>> authorizeDestination,
        CancellationToken cancellationToken, bool preventHttpsDowngrade)
        => await LoadAuthorizedNavigationAsync(url, authorizeDestination, cancellationToken, preventHttpsDowngrade,
            rewriteDestination: null).ConfigureAwait(false);
    public async Task<LoadedPage> LoadAuthorizedNavigationAsync(BrowserUrl url,
        Func<BrowserUrl, int, CancellationToken, ValueTask<bool>> authorizeDestination,
        CancellationToken cancellationToken, bool preventHttpsDowngrade,
        Func<BrowserUrl, BrowserUrl>? rewriteDestination)
    {
        var response = await loader.LoadAuthorizedNavigationAsync(url, authorizeDestination,
            cancellationToken: cancellationToken, preventHttpsDowngrade: preventHttpsDowngrade,
            rewriteDestination: rewriteDestination).ConfigureAwait(false);
        return await CreateLoadedPageAsync(response, cancellationToken).ConfigureAwait(false);
    }
    private async Task<LoadedPage> CreateLoadedPageAsync(ResourceResponse response, CancellationToken cancellationToken)
    {
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
        IReadOnlyList<string> policyFields = Array.Empty<string>();
        ContentSecurityPolicy? policy = null;
        BrowserUrl? baseUrlOverride = null;
        if (response.Headers.TryGetValue("Content-Security-Policy", out var responsePolicyFields))
        {
            policyFields = responsePolicyFields;
            policy = ContentSecurityPolicy.Parse(policyFields, diagnostics);
            StaticPageRenderer.EnforceInlineContentPolicy(decoded.Text, policy.AllowsInlineScript,
                policy.AllowsInlineStyle, policy.AllowsInlineStyleAttribute, cancellationToken);
            diagnostics.Add("Content-Security-Policy: bounded response policy enforced for inline and external style content.");
            if (policy.UpgradesInsecureRequests)
            {
                diagnostics.Add("Content-Security-Policy upgrade-insecure-requests applies to browser-brokered stylesheet requests and shell-managed GET form submissions; other page navigation is unchanged.");
            }
            if (policy.HasBaseUri)
            {
                var baseHref = StaticPageRenderer.DiscoverBaseHrefUrl(decoded.Text, response.Url, cancellationToken);
                var baseAllowed = baseHref is null || policy.AllowsBaseUri(baseHref, response.Url.Origin);
                baseUrlOverride = baseAllowed ? baseHref ?? response.Url : response.Url;
                diagnostics.Add(baseAllowed
                    ? "Content-Security-Policy base-uri applied to the initial document base URL; later base changes do not affect browser resource resolution."
                    : "Content-Security-Policy base-uri blocked a base URL; the document URL is used instead.");
            }
        }
        var stylesheets = await LoadStylesheetsAsync(response.Url, decoded.Text, sniffed.Encoding, policy, diagnostics,
            cancellationToken, baseUrlOverride).ConfigureAwait(false);
        return new(response.Url, decoded.Text, response.StatusCode, diagnostics.ToArray())
        {
            Stylesheets = stylesheets,
            CharacterEncoding = sniffed.Encoding.Name,
            ContentSecurityPolicyFields = policyFields,
            BaseUrl = baseUrlOverride
        };
    }

    private async Task<PageStylesheet[]> LoadStylesheetsAsync(BrowserUrl documentUrl, string html, WebEncoding documentEncoding,
        ContentSecurityPolicy? policy, List<string> diagnostics, CancellationToken cancellationToken,
        BrowserUrl? baseUrlOverride)
    {
        IReadOnlyList<string> urls;
        IReadOnlyList<string> inlineImports;
        try
        {
            urls = StaticPageRenderer.DiscoverStylesheets(html, documentUrl, cancellationToken, baseUrlOverride);
            inlineImports = StaticPageRenderer.DiscoverInlineStylesheetImports(html, documentUrl, cancellationToken,
                baseUrlOverride);
        }
        // The renderer parses identically and reports the same failure; nothing is fetched for an unparsable document.
        catch (Exception exception) when (exception is HtmlLimitException or UnsupportedHtmlException) { return []; }

        var pending = new Queue<BrowserUrl>();
        var queued = new HashSet<string>(StringComparer.Ordinal);
        void Enqueue(string href)
        {
            if (!queued.Add(href)) { return; }
            if (queued.Count > RendererProtocol.MaxStylesheets)
            {
                throw new PageNavigationException($"Stylesheet and CSS @import count limit ({RendererProtocol.MaxStylesheets}) exceeded.");
            }
            pending.Enqueue(BrowserUrl.Parse(href));
        }
        foreach (var url in urls) { Enqueue(url); }
        foreach (var url in inlineImports) { Enqueue(url); }
        if (pending.Count == 0) { return []; }

        var sheets = new List<PageStylesheet>(pending.Count);
        while (pending.TryDequeue(out var url))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheet = await LoadStylesheetAsync(url, documentUrl, documentEncoding, policy, cancellationToken)
                .ConfigureAwait(false);
            sheets.Add(sheet);
            try { RendererProtocol.ValidateStylesheets(sheets); }
            catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
            foreach (var import in StaticPageRenderer.DiscoverStylesheetImports(sheet.Css,
                BrowserUrl.Parse(sheet.BaseUrl), cancellationToken))
            {
                Enqueue(import);
            }
        }
        diagnostics.Add($"Stylesheets: {sheets.Count} browser-fetched text/css sources including unconditional CSS @import; "
            + "cross-origin sheets use credentialless CORS with same-origin redirect restrictions; conditional imports/layers, "
            + "url() resources, fonts and images are unsupported.");
        return sheets.ToArray();
    }

    private async Task<PageStylesheet> LoadStylesheetAsync(BrowserUrl url, BrowserUrl documentUrl, WebEncoding documentEncoding,
        ContentSecurityPolicy? policy, CancellationToken cancellationToken)
    {
        var secureDocument = documentUrl.Protocol == "https:";
        var hstsUrl = hstsPolicyStore?.Upgrade(url) ?? url;
        var effectiveUrl = policy?.UpgradeStyleUrl(hstsUrl) ?? hstsUrl;
        if (policy is not null && !policy.AllowsStyle(effectiveUrl, documentUrl.Origin))
        {
            throw new PageNavigationException(
                $"Content-Security-Policy blocked linked or imported stylesheet {effectiveUrl.Href}.");
        }
        var sameOriginAllowed = documentUrl.Origin.IsSameOrigin(hstsUrl.Origin)
            || policy?.UpgradesInsecureRequests == true
                && hstsUrl.Protocol == "http:"
                && effectiveUrl.Protocol == "https:"
                && (documentUrl.Origin.IsSameOrigin(url.Origin)
                    || documentUrl.Origin.IsSameOrigin(effectiveUrl.Origin));
        var corsRequired = url.Protocol is "http:" or "https:" && !sameOriginAllowed;
        ResourceResponse response;
        try
        {
            response = url.Protocol is "http:" or "https:"
                ? policy is null
                    ? corsRequired
                        ? await loader.LoadCorsAsync(url, documentUrl.Origin, cancellationToken: cancellationToken,
                            preventHttpsDowngrade: secureDocument).ConfigureAwait(false)
                        : await loader.LoadSameOriginAsync(url, documentUrl.Origin, includeCookies: false, cancellationToken)
                            .ConfigureAwait(false)
                    : corsRequired
                        ? await loader.LoadCorsAsync(url, documentUrl.Origin,
                            (destination, redirectCount) =>
                                policy.AllowsStyle(destination, documentUrl.Origin, redirectCount),
                            cancellationToken, preventHttpsDowngrade: secureDocument,
                            rewriteDestination: policy.UpgradeStyleUrl).ConfigureAwait(false)
                        : await loader.LoadAuthorizedNavigationAsync(url,
                            (destination, redirectCount) =>
                                sameOriginAllowed && effectiveUrl.Origin.IsSameOrigin(destination.Origin)
                                && policy.AllowsStyle(destination, documentUrl.Origin, redirectCount),
                            includeCookies: false, cancellationToken, preventHttpsDowngrade: secureDocument,
                            rewriteDestination: policy.UpgradeStyleUrl).ConfigureAwait(false)
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
        return new(url.Href, text) { BaseUrl = response.Url.Href };
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

internal static class PageNavigationPolicy
{
    internal static void Validate(BrowserUrl url, bool preventHttpsDowngrade, SecurityOrigin? sameOriginRedirectOrigin)
    {
        if (preventHttpsDowngrade && url.Protocol == "http:")
        { throw new PageNavigationException("HTTPS downgrade page navigation is blocked."); }
        if (sameOriginRedirectOrigin is not null && url.Protocol is "http:" or "https:"
            && !sameOriginRedirectOrigin.IsSameOrigin(url.Origin))
        { throw new PageNavigationException("Cross-origin page navigation is blocked."); }
    }
}
