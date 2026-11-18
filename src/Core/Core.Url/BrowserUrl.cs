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
        if (IsTupleOriginScheme(url.Protocol))
        {
            return SecurityOrigin.FromTuple(url.Protocol[..^1], url.Hostname, url.Port);
        }

        if (url.Protocol != "blob:" || !allowBlob) { return SecurityOrigin.CreateOpaque(); }

        try
        {
            var inner = new DomUrl(url.Pathname);
            return inner.Protocol is "http:" or "https:" or "file:"
                ? ComputeOrigin(inner, allowBlob: false)
                : SecurityOrigin.CreateOpaque();
        }
        catch (InvalidUrlException)
        {
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
    /// The setter value is parsed with an appended colon, so a colon is optional; ASCII tabs and newlines are removed
    /// as by the basic URL parser. The existing serialized remainder is reparsed.</remarks>
    public BrowserUrl WithProtocol(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = RemoveAsciiTabsAndNewlines(value);
        var colon = value.IndexOf(':', StringComparison.Ordinal);

        var scheme = colon < 0 ? value : value[..colon];
        if (scheme.Length == 0 || !char.IsAsciiLetter(scheme[0])
            || scheme.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '+' and not '-' and not '.'))
        {
            return this;
        }

        scheme = scheme.ToLowerInvariant();
        var currentScheme = Protocol[..^1];
        var currentSpecial = IsSpecialScheme(currentScheme);
        var newSpecial = IsSpecialScheme(scheme);
        var includesCredentials = Username.Length > 0 || Password.Length > 0;
        if (currentSpecial != newSpecial
            || scheme == "file" && (includesCredentials || Port.Length > 0)
            || currentScheme == "file" && Host.Length == 0)
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
        if (CannotHaveUsernamePasswordOrPort) { return this; }

        return WithCredentials(PercentEncodeUserInfo(value), Password);
    }

    /// <summary>Returns a new URL with its password set following the URL password setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-password">URL.password setter</see>.
    /// URLs without a host, or with the <c>file:</c> scheme, are unchanged.</remarks>
    public BrowserUrl WithPassword(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (CannotHaveUsernamePasswordOrPort) { return this; }

        return WithCredentials(Username, PercentEncodeUserInfo(value));
    }

    /// <summary>Returns a new URL with its host set following the URL host setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-host">URL.host setter</see>.
    /// The setter preserves the current port when the input omits a valid port and leaves this snapshot unchanged
    /// when the host cannot be parsed.</remarks>
    public BrowserUrl WithHost(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = RemoveAsciiTabsAndNewlines(value);
        if (HasOpaquePath) { return this; }

        var special = IsSpecialScheme(Protocol[..^1]);
        var hostEnd = FindHostStateEnd(value, special, out var portSeparator);
        var isFile = Protocol == "file:";
        if (isFile && portSeparator >= 0 && portSeparator < hostEnd) { return this; }

        var hostnameInput = portSeparator >= 0 && portSeparator < hostEnd ? value[..portSeparator] : value[..hostEnd];
        var hostCandidate = ParseHostnameCandidate(hostnameInput, Protocol);
        if (hostCandidate is null) { return this; }

        int? port = Port.Length == 0 ? null : int.Parse(Port, CultureInfo.InvariantCulture);
        if (!isFile && portSeparator >= 0 && portSeparator < hostEnd)
        {
            var portInput = value[(portSeparator + 1)..hostEnd];
            if (TryParsePortPrefix(portInput, out var portValue))
            {
                var portCandidate = ParseResult(
                    $"{Protocol}//{hostnameInput}:{portValue.ToString(CultureInfo.InvariantCulture)}/").Url;
                if (portCandidate is not null)
                {
                    port = portCandidate.Port.Length == 0
                        ? null
                        : int.Parse(portCandidate.Port, CultureInfo.InvariantCulture);
                }
            }
        }

        var updated = new BrowserUrl(new DomUrl(Href).WithHost(hostCandidate.Hostname, port));
        return updated.Href == Href ? this : updated;
    }

    /// <summary>Returns a new URL with its hostname set following the URL hostname setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-hostname">URL.hostname setter</see>.
    /// The current port is preserved; opaque-path URLs are unchanged.</remarks>
    public BrowserUrl WithHostname(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = RemoveAsciiTabsAndNewlines(value);
        if (HasOpaquePath) { return this; }

        var hostEnd = FindHostStateEnd(value, IsSpecialScheme(Protocol[..^1]), out var portSeparator);
        if (portSeparator >= 0 && portSeparator < hostEnd) { return this; }

        var hostCandidate = ParseHostnameCandidate(value[..hostEnd], Protocol);
        if (hostCandidate is null) { return this; }

        int? port = Port.Length == 0 ? null : int.Parse(Port, CultureInfo.InvariantCulture);
        var updated = new BrowserUrl(new DomUrl(Href).WithHost(hostCandidate.Hostname, port));
        return updated.Href == Href ? this : updated;
    }

    /// <summary>Returns a new URL with its port set following the URL port setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-port">URL.port setter</see>.
    /// An empty value clears the port. The leading ASCII digit run is parsed as the port; a missing digit run,
    /// an out-of-range prefix, or a URL that cannot have a port leaves this snapshot unchanged.</remarks>
    public BrowserUrl WithPort(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (CannotHaveUsernamePasswordOrPort) { return this; }
        if (value.Length == 0) { return new BrowserUrl(new DomUrl(Href).WithHost(Hostname, null)); }

        value = RemoveAsciiTabsAndNewlines(value);
        if (!TryParsePortPrefix(value, out var portValue)) { return this; }

        var portCandidate = ParseResult(
            $"{Protocol}//{Hostname}:{portValue.ToString(CultureInfo.InvariantCulture)}/").Url;
        if (portCandidate is null) { return this; }

        int? port = portCandidate.Port.Length == 0
            ? null
            : int.Parse(portCandidate.Port, CultureInfo.InvariantCulture);
        return new BrowserUrl(new DomUrl(Href).WithHost(Hostname, port));
    }

    /// <summary>Returns a new URL with its pathname set following the URL pathname setter.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-pathname">URL.pathname setter</see>.
    /// Opaque-path URLs are unchanged; the existing query and fragment are preserved.</remarks>
    public BrowserUrl WithPathname(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (HasOpaquePath) { return this; }

        var special = IsSpecialScheme(Protocol[..^1]);
        var hasAuthority = Href.StartsWith(Protocol + "//", StringComparison.Ordinal);
        var urlPrefix = Protocol;
        if (hasAuthority)
        {
            var authorityStart = Protocol.Length + 2;
            var authorityEnd = authorityStart
                + FindHostStateEnd(Href[authorityStart..], special, out _);
            urlPrefix = Href[..authorityEnd];
        }

        var pathname = RemoveAsciiTabsAndNewlines(value)
            .Replace("?", "%3F", StringComparison.Ordinal)
            .Replace("#", "%23", StringComparison.Ordinal);
        if (pathname.Length == 0)
        {
            if (!hasAuthority) { pathname = "/"; }
        }
        else if (pathname[0] != '/' && !(special && pathname[0] == '\\'))
        {
            pathname = "/" + pathname;
        }

        var suffixStart = Href.IndexOfAny(['?', '#'], urlPrefix.Length);
        var suffix = suffixStart < 0 ? "" : Href[suffixStart..];
        var candidate = urlPrefix + pathname + suffix;
        if (suffix.Length == 0)
        {
            candidate = Parse(candidate + "#").Href;
            candidate = candidate[..^1];
        }

        return Parse(candidate);
    }

    private bool HasOpaquePath =>
        Host.Length == 0 && !Href.AsSpan(Protocol.Length).StartsWith("/", StringComparison.Ordinal);

    private bool AuthorityIncludesCredentials
    {
        get
        {
            var prefix = Protocol + "//";
            if (!Href.StartsWith(prefix, StringComparison.Ordinal)) { return false; }

            var authorityStart = prefix.Length;
            var authorityEnd = authorityStart
                + FindHostStateEnd(Href[authorityStart..], IsSpecialScheme(Protocol[..^1]), out _);
            return authorityEnd > authorityStart && Href.AsSpan(authorityStart, authorityEnd - authorityStart).Contains('@');
        }
    }

    private static int FindHostStateEnd(string value, bool special, out int portSeparator)
    {
        var inBrackets = false;
        portSeparator = -1;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '/' or '?' or '#' || special && character == '\\') { return index; }
            if (character == '[') { inBrackets = true; }
            else if (character == ']') { inBrackets = false; }
            else if (character == ':' && !inBrackets && portSeparator < 0) { portSeparator = index; }
        }

        return value.Length;
    }

    private static string RemoveAsciiTabsAndNewlines(string value)
    {
        if (value.IndexOfAny(['\t', '\n', '\r']) < 0) { return value; }

        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is not '\t' and not '\n' and not '\r') { result.Append(character); }
        }

        return result.ToString();
    }

    private static bool TryParsePortPrefix(string value, out ushort port)
    {
        var digitCount = 0;
        while (digitCount < value.Length && char.IsAsciiDigit(value[digitCount])) { digitCount++; }
        if (digitCount == 0)
        {
            port = 0;
            return false;
        }

        return ushort.TryParse(value[..digitCount], NumberStyles.None, CultureInfo.InvariantCulture, out port);
    }

    private BrowserUrl? ParseHostnameCandidate(string hostname, string parsingProtocol)
    {
        if (hostname.Contains('@')) { return null; }
        if (hostname.Length == 0 && (Port.Length > 0 || AuthorityIncludesCredentials)) { return null; }

        return ParseResult($"{parsingProtocol}//{hostname}/").Url;
    }

    private bool CannotHaveUsernamePasswordOrPort => Host.Length == 0 || Protocol == "file:";

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
    /// removes the fragment; otherwise one leading <c>#</c> is removed and the rest is UTF-8 percent-encoded with the
    /// fragment percent-encode set. ASCII tabs and newlines are removed by the basic URL parser. This immutable facade
    /// returns a fresh snapshot, so an opaque origin gets a new identity.</remarks>
    public BrowserUrl WithHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var fragmentStart = Href.IndexOf('#', StringComparison.Ordinal);
        var withoutFragment = fragmentStart < 0 ? Href : Href[..fragmentStart];
        if (value.Length == 0) { return Parse(withoutFragment); }

        var fragment = RemoveAsciiTabsAndNewlines(value[0] == '#' ? value[1..] : value);
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
    /// (or special-query) set. With a state override, <c>#</c> is data and ASCII tabs/newlines are removed by the basic URL parser.
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

        var query = RemoveAsciiTabsAndNewlines(value[0] == '?' ? value[1..] : value);
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

    private static bool IsTupleOriginScheme(string protocol) =>
        protocol is "http:" or "https:" or "ftp:" or "ws:" or "wss:";
}

public sealed record UrlParseResult(BrowserUrl? Url, string? Error)
{
    public bool Success => Url is not null;
}

public sealed class UrlParseException(string message) : FormatException(message);
