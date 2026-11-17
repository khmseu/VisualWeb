using Dubzer.WhatwgUrl;

namespace VisualWeb.Core.Url;

/// <summary>Immutable snapshot of a parsed WHATWG URL.</summary>
/// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#concept-basic-url-parser">basic URL parser</see>
/// and <see href="https://url.spec.whatwg.org/#concept-url-serializer">URL serializer</see>.
/// Parsing is delegated to the managed MIT-licensed Dubzer.WhatwgUrl implementation.
/// SerializedOrigin is for display/serialization, not an opaque-origin security identity; use <see cref="Origin"/>.
/// Origin is computed once per snapshot, so an opaque origin keeps its identity across reads of the same instance
/// but a re-parse yields a different one. See <see href="https://url.spec.whatwg.org/#concept-url-origin">origin of a URL</see>.
/// <see cref="IsAboutBlank"/> matches the HTML Standard URL predicate; it does not select a document's inherited or
/// sandboxed origin.</remarks>
public sealed class BrowserUrl
{
    public string Href { get; }
    public string Protocol { get; }
    public string Username { get; }
    public string Password { get; }
    public string Host { get; }
    public string Hostname { get; }
    public string Port { get; }
    public string Pathname { get; }
    public string Search { get; }
    public string Hash { get; }
    public string SerializedOrigin { get; }
    public SecurityOrigin Origin { get; }
    /// <summary>Whether this URL matches the HTML Standard's <c>about:blank</c> predicate.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/urls-and-fetching.html#matches-about:blank"/>.
    /// Query and fragment are allowed; the path must be exactly <c>blank</c>, with no host or credentials.</remarks>
    public bool IsAboutBlank => Protocol == "about:" && Pathname == "blank"
        && Username.Length == 0 && Password.Length == 0 && Host.Length == 0;

    private BrowserUrl(DomUrl url)
    {
        Href = url.Href;
        Protocol = url.Protocol;
        Username = url.Username;
        Password = url.Password;
        Host = url.Host;
        Hostname = url.Hostname;
        Port = url.Port;
        Pathname = url.Pathname;
        Search = url.Search;
        Hash = url.Hash;
        SerializedOrigin = url.Origin;
        Origin = ComputeOrigin(url, allowBlob: true);
    }

    private static SecurityOrigin ComputeOrigin(DomUrl url, bool allowBlob)
    {
        switch (url.Protocol)
        {
            case "http:" or "https:" or "ftp:" or "ws:" or "wss:":
                return SecurityOrigin.FromTuple(url.Protocol[..^1], url.Hostname, url.Port);
            case "blob:" when allowBlob:
                try
                {
                    var inner = new DomUrl(url.Pathname);
                    return inner.Protocol is "http:" or "https:"
                        ? ComputeOrigin(inner, allowBlob: false)
                        : SecurityOrigin.CreateOpaque();
                }
                catch (InvalidUrlException)
                {
                    return SecurityOrigin.CreateOpaque();
                }
            default:
                return SecurityOrigin.CreateOpaque();
        }
    }

    public static UrlParseResult ParseResult(string input, BrowserUrl? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            return new(new BrowserUrl(new DomUrl(input, baseUrl?.Href)), null);
        }
        catch (InvalidUrlException exception)
        {
            return new(null, $"{exception.UrlError}: {exception.Message}");
        }
    }

    public static BrowserUrl Parse(string input, BrowserUrl? baseUrl = null)
    {
        var result = ParseResult(input, baseUrl);
        return result.Url ?? throw new UrlParseException(result.Error!);
    }

    /// <summary>Determines whether <paramref name="input"/> parses, optionally against a string base URL.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-canparse">URL.canParse()</see>.
    /// A non-null base is parsed first and any base failure, including an empty base string, returns <see langword="false"/>.</remarks>
    public static bool CanParse(string input, string? baseInput = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        BrowserUrl? baseUrl = null;
        if (baseInput is not null)
        {
            baseUrl = ParseResult(baseInput).Url;
            if (baseUrl is null) { return false; }
        }

        return ParseResult(input, baseUrl).Success;
    }

    /// <summary>Returns a new URL whose fragment is set following the URL hash setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-hash">URL.hash setter</see>. An empty value
    /// removes the fragment (opaque-path trailing spaces follow the vendored parser's re-parse behavior); otherwise one
    /// leading <c>#</c> is removed and the rest is UTF-8 percent-encoded with the fragment percent-encode set (tabs and
    /// newlines are encoded, not stripped). This immutable facade returns a fresh snapshot, so an opaque origin gets a new identity.</remarks>
    public BrowserUrl WithHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var fragmentStart = Href.IndexOf('#', StringComparison.Ordinal);
        var withoutFragment = fragmentStart < 0 ? Href : Href[..fragmentStart];
        if (value.Length == 0) { return Parse(withoutFragment); }

        var fragment = value[0] == '#' ? value[1..] : value;
        var encoded = new System.Text.StringBuilder(withoutFragment).Append('#');
        foreach (var character in fragment)
        {
            if (character <= ' ' || character is '\u007F' or '"' or '<' or '>' or '`')
            {
                encoded.Append('%').Append(((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                encoded.Append(character);
            }
        }

        return Parse(encoded.ToString());
    }

    public BrowserUrl Resolve(string relative) => Parse(relative, this);
    public override string ToString() => Href;
}

public sealed record UrlParseResult(BrowserUrl? Url, string? Error)
{
    public bool Success => Url is not null;
}

public sealed class UrlParseException(string message) : FormatException(message);
