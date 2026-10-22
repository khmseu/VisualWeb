using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

/// <summary>Shared browser/renderer policy for the supported classic <c>link rel=stylesheet</c> subset.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
/// "stylesheet"</see> and <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-link-element">the link
/// element</see>, and css-mediaqueries-5;
/// <see href="https://www.w3.org/TR/mediaqueries-5/#media-types">media types</see>. Only persistent text/css sheets
/// matching <c>all</c>/<c>screen</c> media types are collected. Exact <c>print</c> media types are recognized as
/// non-applicable to this screen-only renderer and ignored. Comma-separated media lists are supported only when every
/// entry is an exact media type in the supported subset; conditional media queries are unsupported. Alternate/titled/
/// disabled sheets, other types, CORS (<c>crossorigin</c>), integrity, referrer policy and the obsolete
/// <c>charset</c> attribute throw instead of being ignored. Links without a nonempty href create no resource, as in
/// the spec. URLs resolve against the document base URL, falling back to the final response URL.</remarks>
internal static class LinkedStylesheets
{
    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\r', '\f'];
    private static readonly string[] UnsupportedAttributes = ["disabled", "crossorigin", "integrity", "referrerpolicy", "charset"];

    public static IReadOnlyList<string> Discover(DomDocument document, BrowserUrl documentUrl,
        CancellationToken cancellationToken, BrowserUrl? baseUrlOverride = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentUrl);
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var baseUrl = baseUrlOverride ?? BaseUrl(document, documentUrl, cancellationToken);
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Resolve(element, documentUrl, baseUrl) is not { } url || !seen.Add(url)) { continue; }
            if (urls.Count >= RendererProtocol.MaxStylesheets)
            { throw new PageNavigationException($"Linked stylesheet count limit ({RendererProtocol.MaxStylesheets}) exceeded."); }
            urls.Add(url);
        }
        return urls.AsReadOnly();
    }

    /// <summary>Returns the document base URL from the first base element with an href, or the response URL fallback.</summary>
    public static BrowserUrl BaseUrl(DomDocument document, BrowserUrl documentUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentUrl);
        return BaseHrefUrl(document, documentUrl, cancellationToken) ?? documentUrl;
    }

    /// <summary>Returns the valid URL from the first base element with an href, or null when no usable href exists.</summary>
    public static BrowserUrl? BaseHrefUrl(DomDocument document, BrowserUrl documentUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentUrl);
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is not { LocalName: "base", NamespaceUri: DomElement.HtmlNamespace }
                || element.GetAttribute("href") is not { } href) { continue; }
            if (href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("HTML base URL limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(href, documentUrl).Url;
            if (parsed is null || parsed.Protocol is "data:" or "javascript:") { return null; }
            if (parsed.Href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("HTML base URL limit exceeded."); }
            return parsed;
        }
        return null;
    }

    public static bool SupportsMedia(string? media) => ParseMediaList(media) is not null;

    public static bool AppliesToScreen(string? media)
    {
        var mediaTypes = ParseMediaList(media);
        if (mediaTypes is null)
        {
            return false;
        }

        return mediaTypes.Contains("all", StringComparer.OrdinalIgnoreCase)
            || mediaTypes.Contains("screen", StringComparer.OrdinalIgnoreCase);
    }

    public static string StripCharsetRule(string css)
    {
        const string prefix = "@charset \"";
        if (!css.StartsWith(prefix, StringComparison.Ordinal)) { return css; }
        var labelEnd = css.IndexOf('\"', prefix.Length);
        if (labelEnd < 0 || labelEnd + 1 >= css.Length || css[labelEnd + 1] != ';') { return css; }
        return css[(labelEnd + 2)..];
    }

    public static string? Resolve(DomElement element, BrowserUrl? documentUrl, BrowserUrl? baseUrl)
    {
        if (element is not { LocalName: "link", NamespaceUri: DomElement.HtmlNamespace }) { return null; }
        var rel = (element.GetAttribute("rel") ?? "").Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries);
        if (!rel.Any(token => token.Equals("stylesheet", StringComparison.OrdinalIgnoreCase))) { return null; }
        if (rel.Any(token => token.Equals("alternate", StringComparison.OrdinalIgnoreCase)))
        { throw new PageNavigationException("Alternate linked stylesheets are unsupported."); }
        if (element.GetAttribute("title") is { Length: > 0 })
        { throw new PageNavigationException("Named/alternate linked stylesheet sets are unsupported."); }
        if (UnsupportedAttributes.FirstOrDefault(name => element.GetAttribute(name) is not null) is { } unsupported)
        { throw new PageNavigationException($"Linked stylesheet attribute '{unsupported}' is unsupported."); }
        if (!SupportsMedia(element.GetAttribute("media")))
        {
            throw new PageNavigationException("Linked stylesheet media must be absent or a comma-separated list containing only exact 'all', 'screen', or 'print' media types.");
        }
        if (element.GetAttribute("type") is { Length: > 0 } type && !type.Equals("text/css", StringComparison.OrdinalIgnoreCase))
        { throw new PageNavigationException("Linked stylesheet types other than text/css are unsupported."); }
        if (!AppliesToScreen(element.GetAttribute("media"))) { return null; }
        if (element.GetAttribute("href") is not { Length: > 0 } href) { return null; }
        if (documentUrl is null) { throw new PageNavigationException("Linked stylesheets require the document URL."); }
        if (href.Length > RendererProtocol.MaxTextCharacters) { throw new PageNavigationException("Linked stylesheet URL limit exceeded."); }
        var parsed = BrowserUrl.ParseResult(href, baseUrl ?? documentUrl);
        var url = parsed.Url ?? throw new PageNavigationException("Invalid linked stylesheet URL: " + parsed.Error);
        if (url.Href.Length > RendererProtocol.MaxTextCharacters) { throw new PageNavigationException("Linked stylesheet URL limit exceeded."); }
        if (documentUrl.Protocol is ("file:" or "data:") && url.Protocol is ("http:" or "https:"))
        {
            throw new PageNavigationException($"Linked network stylesheets are blocked from {documentUrl.Protocol} documents.");
        }
        if (url.Protocol is not ("http:" or "https:" or "data:"))
        {
            throw new PageNavigationException(url.Protocol == "file:"
                ? "Linked file stylesheets are blocked; file documents cannot request local files."
                : $"Linked stylesheet scheme {url.Protocol} is unsupported.");
        }
        return url.Href;
    }

    private static IReadOnlyList<string>? ParseMediaList(string? media)
    {
        if (media is null)
        {
            return ["all"];
        }

        var entries = media.Split(',', StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
        {
            return null;
        }

        var values = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var token = entry.Trim(AsciiWhitespace);
            if (token.Length == 0)
            {
                return null;
            }

            if (!token.Equals("all", StringComparison.OrdinalIgnoreCase)
                && !token.Equals("screen", StringComparison.OrdinalIgnoreCase)
                && !token.Equals("print", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            values.Add(token);
        }

        return values.AsReadOnly();
    }
}
