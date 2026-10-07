using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

/// <summary>Shared browser/renderer policy for the supported classic <c>link rel=stylesheet</c> subset.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
/// "stylesheet"</see> and <see href="https://html.spec.whatwg.org/multipage/semantics.html#the-link-element">the link
/// element</see>. Only persistent, unconditional text/css sheets are supported: alternate/titled/disabled sheets,
/// non-<c>all</c> media, other types, CORS (<c>crossorigin</c>), integrity, referrer policy, the obsolete
/// <c>charset</c> attribute and <c>&lt;base href&gt;</c> overrides throw instead of being ignored. Links without a
/// nonempty href create no resource, as in the spec. URLs resolve against the final document response URL.</remarks>
internal static class LinkedStylesheets
{
    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\r', '\f'];
    private static readonly string[] UnsupportedAttributes = ["disabled", "crossorigin", "integrity", "referrerpolicy", "charset"];

    public static IReadOnlyList<string> Discover(DomDocument document, BrowserUrl documentUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentUrl);
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var baseHref = BaseHref(document);
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Resolve(element, documentUrl, baseHref) is not { } url || !seen.Add(url)) { continue; }
            if (urls.Count >= RendererProtocol.MaxStylesheets)
            { throw new PageNavigationException($"Linked stylesheet count limit ({RendererProtocol.MaxStylesheets}) exceeded."); }
            urls.Add(url);
        }
        return urls.AsReadOnly();
    }

    /// <summary>Returns the serialized request URL, null when the element creates no stylesheet, or throws.</summary>
    public static Func<bool> BaseHref(DomDocument document)
    {
        bool? found = null;
        return () => found ??= document.Descendants().OfType<DomElement>()
            .Any(e => e is { LocalName: "base", NamespaceUri: DomElement.HtmlNamespace } && e.GetAttribute("href") is not null);
    }

    public static string StripCharsetRule(string css)
    {
        const string prefix = "@charset \"";
        if (!css.StartsWith(prefix, StringComparison.Ordinal)) { return css; }
        var labelEnd = css.IndexOf('\"', prefix.Length);
        if (labelEnd < 0 || labelEnd + 1 >= css.Length || css[labelEnd + 1] != ';') { return css; }
        return css[(labelEnd + 2)..];
    }

    public static string? Resolve(DomElement element, BrowserUrl? documentUrl, Func<bool> baseHref)
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
        if (element.GetAttribute("media") is { } media && !media.Trim(AsciiWhitespace).Equals("all", StringComparison.OrdinalIgnoreCase))
        { throw new PageNavigationException("Conditional linked stylesheets (media other than 'all') are unsupported."); }
        if (element.GetAttribute("type") is { Length: > 0 } type && !type.Equals("text/css", StringComparison.OrdinalIgnoreCase))
        { throw new PageNavigationException("Linked stylesheet types other than text/css are unsupported."); }
        if (element.GetAttribute("href") is not { Length: > 0 } href) { return null; }
        if (baseHref())
        { throw new PageNavigationException("<base href> is unsupported for linked stylesheet URL resolution."); }
        if (documentUrl is null) { throw new PageNavigationException("Linked stylesheets require the document URL."); }
        if (href.Length > RendererProtocol.MaxTextCharacters) { throw new PageNavigationException("Linked stylesheet URL limit exceeded."); }
        var parsed = BrowserUrl.ParseResult(href, documentUrl);
        var url = parsed.Url ?? throw new PageNavigationException("Invalid linked stylesheet URL: " + parsed.Error);
        if (url.Href.Length > RendererProtocol.MaxTextCharacters) { throw new PageNavigationException("Linked stylesheet URL limit exceeded."); }
        if (url.Protocol is not ("http:" or "https:" or "data:"))
        {
            throw new PageNavigationException(url.Protocol == "file:"
                ? "Linked file stylesheets are blocked; file documents cannot request local files."
                : $"Linked stylesheet scheme {url.Protocol} is unsupported.");
        }
        return url.Href;
    }
}
