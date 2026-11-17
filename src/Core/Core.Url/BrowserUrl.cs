using System.Globalization;
using System.Text;
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

    /// <summary>Parses a URL, returning <see langword="null"/> instead of throwing when parsing fails.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-parse">URL.parse()</see>.
    /// A non-null base string is parsed first. Invalid input or an invalid base returns <see langword="null"/>.</remarks>
    public static BrowserUrl? ParseOrNull(string input, string? baseInput = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        BrowserUrl? baseUrl = null;
        if (baseInput is not null)
        {
            baseUrl = ParseResult(baseInput).Url;
            if (baseUrl is null) { return null; }
        }

        return ParseResult(input, baseUrl).Url;
    }

    /// <summary>Returns a new URL by parsing an absolute URL string, following the URL <c>href</c> setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-href">URL.href setter</see>.
    /// The immutable base instance is unchanged. The value is parsed without a base URL, so relative references fail.</remarks>
    public BrowserUrl WithHref(string value) => Parse(value);

    /// <summary>Returns the URL's serialized value for JSON serialization.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-tojson">URL.toJSON()</see>.</remarks>
    public string ToJson() => Href;

    /// <summary>Returns a new URL with its scheme changed as by the URL <c>protocol</c> setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-protocol">URL.protocol setter</see>.
    /// Invalid scheme input and forbidden special/non-special transitions leave this immutable snapshot unchanged.
    /// The setter value is consumed through its first colon; the existing serialized remainder is reparsed.</remarks>
    public BrowserUrl WithProtocol(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) { return this; }

        var scheme = value[..colon];
        if (scheme.Length == 0 || !char.IsAsciiLetter(scheme[0])
            || scheme.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '+' and not '-' and not '.'))
        {
            return this;
        }

        scheme = scheme.ToLowerInvariant();
        var currentScheme = Protocol[..^1];
        var currentSpecial = IsSpecialScheme(currentScheme);
        var newSpecial = IsSpecialScheme(scheme);
        if (currentSpecial != newSpecial || currentScheme == "file" && scheme != "file"
            || scheme == "file" && Hostname.Length == 0)
        {
            return this;
        }

        var colonInHref = Href.IndexOf(':', StringComparison.Ordinal);
        return ParseResult(scheme + Href[colonInHref..]).Url ?? this;
    }

    /// <summary>Returns a new URL with its username set following the URL username setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-username">URL.username setter</see>.
    /// URLs without a host, or with the <c>file:</c> scheme, are unchanged.</remarks>
    public BrowserUrl WithUsername(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (CannotHaveCredentials) { return this; }

        return WithCredentials(PercentEncodeUserInfo(value), Password);
    }

    /// <summary>Returns a new URL with its password set following the URL password setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-password">URL.password setter</see>.
    /// URLs without a host, or with the <c>file:</c> scheme, are unchanged.</remarks>
    public BrowserUrl WithPassword(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (CannotHaveCredentials) { return this; }

        return WithCredentials(Username, PercentEncodeUserInfo(value));
    }

    private bool CannotHaveCredentials => Host.Length == 0 || Protocol == "file:";

    private BrowserUrl WithCredentials(string username, string password)
    {
        var authorityStart = Protocol.Length + 2;
        var authorityEnd = Href.Length;
        for (var index = authorityStart; index < Href.Length; index++)
        {
            if (Href[index] is '/' or '?' or '#')
            {
                authorityEnd = index;
                break;
            }
        }

        var atSign = Href.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
        var hostStart = atSign < authorityStart ? authorityStart : atSign + 1;
        var result = new StringBuilder(Href.Length + username.Length + password.Length);
        result.Append(Href, 0, authorityStart);
        if (username.Length > 0 || password.Length > 0)
        {
            result.Append(username);
            if (password.Length > 0) { result.Append(':').Append(password); }
            result.Append('@');
        }

        result.Append(Href, hostStart, Href.Length - hostStart);
        return Parse(result.ToString());
    }

    private static string PercentEncodeUserInfo(string value)
    {
        var result = new StringBuilder(value.Length);
        Span<byte> utf8Bytes = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;
            if (codePoint > 0x7E || codePoint <= 0x1F
                || codePoint is 0x20 or 0x22 or 0x23 or 0x2F or 0x3A or 0x3B or 0x3D or 0x3F
                    or 0x40 or 0x5B or 0x5C or 0x5D or 0x5E or 0x60 or 0x7B or 0x7D or 0x7C)
            {
                var byteCount = rune.EncodeToUtf8(utf8Bytes);
                for (var index = 0; index < byteCount; index++)
                {
                    result.Append('%').Append(utf8Bytes[index].ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            else
            {
                result.Append(rune.ToString());
            }
        }

        return result.ToString();
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

    /// <summary>Returns a new URL whose query is set following the URL search setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-search">URL.search setter</see>. An empty value
    /// removes the query; otherwise one leading <c>?</c> is removed and the rest is UTF-8 percent-encoded with the query
    /// (or special-query) set. With a state override, <c>#</c> is data and tabs/newlines are encoded rather than stripped.
    /// The fragment is retained. This immutable facade returns a fresh snapshot, so an opaque origin gets a new identity
    /// and there is no linked <see cref="UrlSearchParams"/> to update.</remarks>
    public BrowserUrl WithSearch(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var fragmentStart = Href.IndexOf('#', StringComparison.Ordinal);
        var fragment = fragmentStart < 0 ? "" : Href[fragmentStart..];
        var withoutFragment = fragmentStart < 0 ? Href : Href[..fragmentStart];
        var queryStart = withoutFragment.IndexOf('?', StringComparison.Ordinal);
        var prefix = queryStart < 0 ? withoutFragment : withoutFragment[..queryStart];
        if (value.Length == 0) { return Parse(prefix + fragment); }

        var query = value[0] == '?' ? value[1..] : value;
        var rebuilt = new System.Text.StringBuilder(prefix).Append('?');
        foreach (var character in query)
        {
            if (character <= ' ' || character is '\u007F' or '"' or '#' or '<' or '>')
            {
                rebuilt.Append('%').Append(((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                rebuilt.Append(character);
            }
        }

        return Parse(rebuilt.Append(fragment).ToString());
    }

    /// <summary>Creates a detached <see cref="UrlSearchParams"/> parsed from this URL's query.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-searchparams">URL.searchParams</see>.
    /// The result is a snapshot with no live link back to this immutable URL; use <see cref="WithSearchParams"/> to apply changes.</remarks>
    public UrlSearchParams CreateSearchParams() => new(Search);

    /// <summary>Returns a new URL whose query is the serialization of <paramref name="parameters"/>.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#concept-urlsearchparams-update">URLSearchParams update steps</see>.
    /// An empty serialization removes the query. The fragment is retained.</remarks>
    public BrowserUrl WithSearchParams(UrlSearchParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return WithSearch(parameters.ToString());
    }

    public BrowserUrl Resolve(string relative) => Parse(relative, this);
    public override string ToString() => Href;

    private static bool IsSpecialScheme(string scheme) =>
        scheme is "ftp" or "file" or "http" or "https" or "ws" or "wss";
}

public sealed record UrlParseResult(BrowserUrl? Url, string? Error)
{
    public bool Success => Url is not null;
}

public sealed class UrlParseException(string message) : FormatException(message);
