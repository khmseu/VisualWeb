using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

/// <summary>Bounded response-header Content Security Policy source-list subset.</summary>
/// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#parse-serialized-policy">parse a serialized CSP</see>,
/// <see href="https://www.w3.org/TR/CSP3/#framework-directive-source-list">source lists</see>,
/// <see href="https://www.w3.org/TR/CSP3/#match-nonce-to-source-list">nonce matching</see>,
/// <see href="https://www.w3.org/TR/CSP3/#match-element-to-source-list">element hash matching</see>,
/// <see href="https://www.w3.org/TR/CSP3/#scheme-part-match">scheme-source matching</see>, and
/// <see href="https://www.w3.org/TR/CSP3/#directive-default-src">default-src</see>, and
/// <see href="https://www.w3.org/TR/CSP3/#directive-style-src-elem">style-src-elem</see>,
/// <see href="https://www.w3.org/TR/CSP3/#directive-form-action">form-action</see>,
/// <see href="https://www.w3.org/TR/CSP3/#directive-base-uri">base-uri</see>; upgrade-insecure-requests
/// follows <see href="https://www.w3.org/TR/upgrade-insecure-requests/#upgrade-request">request upgrading</see>
/// for browser-brokered stylesheets and shell-managed GET form submissions (local source ID
/// <c>upgrade-insecure-requests</c>); other navigation requests are not upgraded.
/// Supports default-src, script-src, script-src-elem, style-src, style-src-elem, style-src-attr, form-action,
/// and base-uri with 'none', 'self', '*', exact HTTP(S) origins, HTTP(S) wildcard subdomain hosts,
/// data:/http:/https: resource scheme sources (including HTTP-to-HTTPS upgrades), the valueless
/// upgrade-insecure-requests directive, strict nonce-source
/// matching, SHA-256/384/512 inline-element hashes, and 'unsafe-inline' when not overridden by a nonce or hash.</remarks>
internal sealed class ContentSecurityPolicy
{
    private const int MaxFields = 16;
    private const int MaxFieldCharacters = 8192;
    private const int MaxDirectives = 64;
    private const int MaxSources = 64;
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];
    private readonly Policy[] policies;

    private ContentSecurityPolicy(Policy[] policies) => this.policies = policies;
    internal bool UpgradesInsecureRequests => policies.Any(policy => policy.UpgradesInsecureRequests);
    internal bool HasBaseUri => policies.Any(policy => policy.HasBaseUri);

    internal static ContentSecurityPolicy Parse(IReadOnlyList<string> fields, IList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (fields.Count > MaxFields)
        { throw new PageNavigationException($"Content-Security-Policy field count limit ({MaxFields}) exceeded."); }

        var policies = new Policy[fields.Count];
        for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
        {
            var field = fields[fieldIndex];
            ArgumentNullException.ThrowIfNull(field);
            if (field.Length > MaxFieldCharacters)
            { throw new PageNavigationException($"Content-Security-Policy field length limit ({MaxFieldCharacters}) exceeded."); }
            policies[fieldIndex] = ParsePolicy(field, diagnostics);
        }
        return new(policies);
    }

    internal bool AllowsInlineScript() => policies.All(policy => AllowsAllInline(policy.ScriptElementDirective()));
    internal bool AllowsInlineScript(string? nonce) => policies.All(policy => AllowsInlineNonce(policy.ScriptElementDirective(), nonce));
    internal bool AllowsInlineScript(string? nonce, string source) => AllowsInline("script-src-elem", nonce, source);
    internal bool AllowsInlineStyle() => policies.All(policy => AllowsAllInline(policy.StyleElementDirective()));
    internal bool AllowsInlineStyle(string? nonce) => policies.All(policy => AllowsInlineNonce(policy.StyleElementDirective(), nonce));
    internal bool AllowsInlineStyle(string? nonce, string source) => AllowsInline("style-src-elem", nonce, source);
    internal bool AllowsInlineStyleAttribute() => policies.All(policy => AllowsAllInline(policy.StyleAttributeDirective()));
    internal bool AllowsInlineStyleAttribute(string source) => AllowsInline("style-src", null, source, styleAttribute: true);
    internal bool AllowsScript(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.ScriptElementDirective(), resource, documentOrigin));
    internal bool AllowsStyle(BrowserUrl resource, SecurityOrigin documentOrigin, int redirectCount = 0) =>
        policies.All(policy => Allows(policy.StyleElementDirective(), resource, documentOrigin, redirectCount));
    internal bool AllowsFormAction(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.FormActionDirective(), resource, documentOrigin));
    internal bool AllowsBaseUri(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.BaseUriDirective(), resource, documentOrigin));
    internal BrowserUrl UpgradeNavigationUrl(BrowserUrl url) =>
        UpgradesInsecureRequests && url.Protocol == "http:" ? UpgradeHttpUrl(url) : url;
    internal BrowserUrl UpgradeStyleUrl(BrowserUrl url) => UpgradeNavigationUrl(url);
    internal BrowserUrl UpgradeFormSubmissionUrl(BrowserUrl url) => UpgradeNavigationUrl(url);

    private static bool AllowsInlineNonce(SourceList? sources, string? nonce) => AllowsAllInline(sources)
        || nonce is { Length: > 0 } && sources!.Nonces.Contains(nonce, StringComparer.Ordinal);

    private static bool AllowsAllInline(SourceList? sources) => sources is null
        || sources.UnsafeInline && sources.Nonces.Length == 0 && sources.Hashes.Length == 0;

    private bool AllowsInline(string directiveName, string? nonce, string source, bool styleAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sourceLists = policies.Select(policy => styleAttribute
            ? policy.StyleAttributeDirective()
            : directiveName == "script-src-elem"
                ? policy.ScriptElementDirective()
                : directiveName == "style-src-elem"
                    ? policy.StyleElementDirective()
                    : policy.Directive(directiveName)).ToArray();
        var algorithms = sourceLists.Where(list => list is not null && (!styleAttribute || list.UnsafeHashes))
            .SelectMany(list => list!.Hashes)
            .Select(hash => hash.Algorithm)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var matchingHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (algorithms.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(source);
            foreach (var algorithm in algorithms)
            {
                matchingHashes.Add(algorithm, algorithm switch
                {
                    "sha256" => Convert.ToBase64String(SHA256.HashData(bytes)),
                    "sha384" => Convert.ToBase64String(SHA384.HashData(bytes)),
                    "sha512" => Convert.ToBase64String(SHA512.HashData(bytes)),
                    _ => throw new InvalidOperationException("Unsupported CSP hash algorithm."),
                });
            }
        }

        return sourceLists.All(list => AllowsAllInline(list)
            || !styleAttribute && nonce is { Length: > 0 } && list is not null
                && list.Nonces.Contains(nonce, StringComparer.Ordinal)
            || list is not null && (!styleAttribute || list.UnsafeHashes)
                && list.Hashes.Any(hash => string.Equals(hash.Expected, matchingHashes[hash.Algorithm], StringComparison.Ordinal)));
    }

    private static bool Allows(SourceList? sources, BrowserUrl resource, SecurityOrigin documentOrigin,
        int redirectCount = 0) =>
        sources is null
        || sources.Self && SelfMatches(documentOrigin, resource.Origin)
        || sources.Origins.Any(origin => OriginSourceMatches(origin, resource.Origin))
        || sources.Hosts.Any(host => HostSourceMatches(host, resource, documentOrigin, redirectCount))
        || sources.Schemes.Any(scheme => SchemeMatches(scheme, resource.Protocol));

    private static bool SelfMatches(SecurityOrigin origin, SecurityOrigin resource) =>
        origin.IsSameOrigin(resource)
        || !origin.IsOpaque && !resource.IsOpaque
            && string.Equals(origin.Host, resource.Host, StringComparison.Ordinal)
            && (origin.Port == resource.Port
                || origin.Port == DefaultPort(origin.Scheme!) && resource.Port == DefaultPort(resource.Scheme!))
            && SchemePartMatches(origin.Scheme!, resource.Scheme!);

    private static bool OriginSourceMatches(SecurityOrigin source, SecurityOrigin resource) =>
        !source.IsOpaque && !resource.IsOpaque
        && string.Equals(source.Host, resource.Host, StringComparison.Ordinal)
        && SchemePartMatches(source.Scheme!, resource.Scheme!)
        && (source.Port == resource.Port
            || source.Port == DefaultPort(source.Scheme!) && resource.Port == DefaultPort(resource.Scheme!));

    private static bool HostSourceMatches(HostSource source, BrowserUrl resource, SecurityOrigin documentOrigin,
        int redirectCount)
    {
        if (source.AnyHost)
        {
            return resource.Protocol is "http:" or "https:"
                || documentOrigin.Scheme is { } documentScheme
                && string.Equals(documentScheme + ":", resource.Protocol, StringComparison.Ordinal);
        }

        var origin = resource.Origin;
        if (origin.IsOpaque || origin.Host is null || IPAddress.TryParse(origin.Host, out _))
        {
            return false;
        }

        var scheme = source.Scheme ?? documentOrigin.Scheme;
        if (scheme is null || !SchemePartMatches(scheme, origin.Scheme!))
        {
            return false;
        }

        var portMatches = source.AnyPort
            || (source.Port is { } port
                ? port == origin.Port
                : origin.Port == DefaultPort(origin.Scheme!));
        var hostMatches = source.Subdomains
            ? origin.Host.EndsWith("." + source.Host, StringComparison.Ordinal)
            : string.Equals(source.Host, origin.Host, StringComparison.Ordinal);
        return portMatches && hostMatches && PathPartMatches(source.Path, resource.Pathname, redirectCount);
    }

    private static bool PathPartMatches(string sourcePath, string resourcePath, int redirectCount)
    {
        if (sourcePath.Length == 0 || redirectCount > 0 || sourcePath == "/" && resourcePath.Length == 0)
        {
            return true;
        }

        var exact = !sourcePath.EndsWith("/", StringComparison.Ordinal);
        var sourceSegments = sourcePath.Split('/');
        var resourceSegments = resourcePath.Split('/');
        if (sourceSegments.Length > resourceSegments.Length
            || exact && sourceSegments.Length != resourceSegments.Length)
        {
            return false;
        }
        if (!exact)
        {
            sourceSegments = sourceSegments[..^1];
        }

        for (var index = 0; index < sourceSegments.Length; index++)
        {
            if (!PercentDecodePathSegment(sourceSegments[index])
                .AsSpan()
                .SequenceEqual(PercentDecodePathSegment(resourceSegments[index])))
            {
                return false;
            }
        }
        return true;
    }

    private static byte[] PercentDecodePathSegment(string segment)
    {
        var decoded = new List<byte>(segment.Length);
        for (var index = 0; index < segment.Length; index++)
        {
            if (segment[index] == '%' && index + 2 < segment.Length
                && TryHexDigit(segment[index + 1], out var high)
                && TryHexDigit(segment[index + 2], out var low))
            {
                decoded.Add((byte)((high << 4) | low));
                index += 2;
            }
            else
            {
                decoded.Add((byte)segment[index]);
            }
        }
        return [.. decoded];
    }

    private static bool TryHexDigit(char character, out int value)
    {
        value = character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'A' and <= 'F' => character - 'A' + 10,
            >= 'a' and <= 'f' => character - 'a' + 10,
            _ => -1,
        };
        return value >= 0;
    }

    private static bool SchemeMatches(string sourceScheme, string resourceScheme) =>
        sourceScheme.Equals(resourceScheme, StringComparison.OrdinalIgnoreCase)
        || sourceScheme.Equals("http:", StringComparison.OrdinalIgnoreCase)
            && resourceScheme.Equals("https:", StringComparison.OrdinalIgnoreCase);

    private static bool SchemePartMatches(string sourceScheme, string resourceScheme) =>
        sourceScheme.Equals(resourceScheme, StringComparison.OrdinalIgnoreCase)
        || sourceScheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && resourceScheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private static int DefaultPort(string scheme) => scheme switch
    {
        "http" => 80,
        "https" => 443,
        _ => 0,
    };

    private static BrowserUrl UpgradeHttpUrl(BrowserUrl url)
    {
        var credentials = url.Username.Length == 0 && url.Password.Length == 0 ? ""
            : url.Username + (url.Password.Length == 0 ? "" : ":" + url.Password) + "@";
        var port = url.Port.Length == 0 || url.Port == "80" ? "" : ":" + url.Port;
        var fragmentAt = url.Href.IndexOf('#');
        var queryAt = url.Href.IndexOf('?');
        var search = url.Search.Length != 0 ? url.Search
            : queryAt >= 0 && (fragmentAt < 0 || queryAt < fragmentAt) ? "?" : "";
        var hash = url.Hash.Length != 0 ? url.Hash : fragmentAt >= 0 ? "#" : "";
        return BrowserUrl.Parse($"https://{credentials}{url.Hostname}{port}{url.Pathname}{search}{hash}");
    }

    private static Policy ParsePolicy(string field, IList<string> diagnostics)
    {
        var directives = new Dictionary<string, SourceList>(StringComparer.OrdinalIgnoreCase);
        var upgradesInsecureRequests = false;
        var serialized = field.Split(';');
        if (serialized.Length > MaxDirectives)
        { throw new PageNavigationException($"Content-Security-Policy directive count limit ({MaxDirectives}) exceeded."); }
        foreach (var item in serialized)
        {
            var parts = item.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { continue; }
            var name = parts[0].ToLowerInvariant();
            if (name == "upgrade-insecure-requests")
            {
                if (upgradesInsecureRequests)
                {
                    diagnostics.Add("Duplicate Content-Security-Policy directive 'upgrade-insecure-requests' was ignored after its first occurrence.");
                }
                upgradesInsecureRequests = true;
                continue;
            }
            if (name is not ("default-src" or "script-src" or "script-src-elem" or "style-src" or "style-src-elem"
                or "style-src-attr" or "form-action" or "base-uri"))
            {
                diagnostics.Add($"Content-Security-Policy directive '{parts[0]}' is unsupported and was ignored.");
                continue;
            }
            if (directives.ContainsKey(name))
            {
                diagnostics.Add($"Duplicate Content-Security-Policy directive '{name}' was ignored after its first occurrence.");
                continue;
            }
            directives.Add(name, ParseSources(name, parts.AsSpan(1)));
        }
        return new(directives, upgradesInsecureRequests);
    }

    private static SourceList ParseSources(string directive, ReadOnlySpan<string> tokens)
    {
        if (tokens.Length > MaxSources)
        { throw new PageNavigationException($"Content-Security-Policy {directive} source count limit ({MaxSources}) exceeded."); }
        if (tokens.Length == 0) { return new(false, [], [], [], [], [], false, false); }
        var hasNone = false;
        foreach (var token in tokens)
        {
            if (token.Equals("'none'", StringComparison.OrdinalIgnoreCase)) { hasNone = true; break; }
        }
        if (hasNone)
        {
            if (tokens.Length != 1)
            { throw new PageNavigationException($"Content-Security-Policy {directive} cannot combine 'none' with other sources."); }
            return new(false, [], [], [], [], [], false, false);
        }

        var self = false;
        var origins = new List<SecurityOrigin>();
        var hosts = new List<HostSource>();
        var nonces = new List<string>();
        var hashes = new List<HashSource>();
        var schemes = new List<string>();
        var unsafeInline = false;
        var unsafeHashes = false;
        foreach (var token in tokens)
        {
            if (token.StartsWith("'sha256-", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("'sha384-", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("'sha512-", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseHashSource(token, out var hash))
                {
                    throw new PageNavigationException($"Content-Security-Policy {directive} contains an invalid hash source.");
                }
                hashes.Add(hash);
                continue;
            }
            if (token.StartsWith("'nonce-", StringComparison.Ordinal))
            {
                if (!TryParseNonceSource(token, out var nonce))
                {
                    throw new PageNavigationException($"Content-Security-Policy {directive} contains an invalid nonce source.");
                }
                nonces.Add(nonce);
                continue;
            }
            if (token.Equals("data:", StringComparison.OrdinalIgnoreCase)
                || token.Equals("http:", StringComparison.OrdinalIgnoreCase)
                || token.Equals("https:", StringComparison.OrdinalIgnoreCase))
            {
                schemes.Add(token.ToLowerInvariant());
                continue;
            }
            if (token.Equals("'unsafe-inline'", StringComparison.OrdinalIgnoreCase))
            {
                unsafeInline = true;
                continue;
            }
            if (token.Equals("'unsafe-hashes'", StringComparison.OrdinalIgnoreCase))
            {
                unsafeHashes = true;
                continue;
            }
            if (token.Equals("'self'", StringComparison.OrdinalIgnoreCase))
            {
                self = true;
                continue;
            }
            if (token == "*")
            {
                hosts.Add(HostSource.Any);
                continue;
            }
            if (TryParseDnsHostSource(token, out var hostSource))
            {
                hosts.Add(hostSource);
                continue;
            }
            var parsed = BrowserUrl.ParseResult(token).Url;
            if (parsed is null || parsed.Protocol is not ("http:" or "https:")
                || parsed.Username.Length != 0 || parsed.Password.Length != 0
                || parsed.Hostname.Contains('*')
                || parsed.Pathname != "/" || parsed.Search.Length != 0 || parsed.Hash.Length != 0)
            {
                throw new PageNavigationException(
                    $"Content-Security-Policy {directive} source '{token}' is unsupported; use 'none', 'self', 'unsafe-inline', 'unsafe-hashes', data:/http:/https:, an exact HTTP(S) origin, or a bounded HTTP(S) DNS host source.");
            }
            if (!origins.Any(origin => origin.IsSameOrigin(parsed.Origin))) { origins.Add(parsed.Origin); }
        }
        return new(self, origins.ToArray(), hosts.ToArray(), nonces.ToArray(), hashes.ToArray(), schemes.ToArray(),
            unsafeInline, unsafeHashes);
    }

    private static bool TryParseDnsHostSource(string token, out HostSource source)
    {
        source = default;
        string? scheme = null;
        var schemeSeparator = token.IndexOf("://", StringComparison.OrdinalIgnoreCase);
        var hostStart = 0;
        if (schemeSeparator >= 0)
        {
            scheme = token[..schemeSeparator];
            if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            hostStart = schemeSeparator + 3;
        }

        var hostAndPath = token[hostStart..];
        var subdomains = hostAndPath.StartsWith("*.", StringComparison.Ordinal);
        if (subdomains)
        {
            hostAndPath = hostAndPath[2..];
        }
        else if (hostAndPath.StartsWith('*'))
        {
            return false;
        }
        var pathStart = hostAndPath.IndexOf('/');
        var host = pathStart < 0 ? hostAndPath : hostAndPath[..pathStart];
        var sourcePath = pathStart < 0 ? "" : hostAndPath[pathStart..];
        if (!subdomains && sourcePath.Length == 0)
        {
            return false;
        }
        var sourcePort = (int?)null;
        var anyPort = false;
        var portSeparator = host.IndexOf(':');
        if (portSeparator >= 0)
        {
            var portText = host[(portSeparator + 1)..];
            host = host[..portSeparator];
            if (portText == "*")
            {
                anyPort = true;
            }
            else if (portText.Length == 0 || portText.Any(character => character is < '0' or > '9')
                || !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort)
                || parsedPort > 65535)
            {
                return false;
            }
            else
            {
                sourcePort = parsedPort;
            }
        }
        if (host.Length == 0 || host[0] == '.' || host.Contains("..", StringComparison.Ordinal)
            || host.Any(character =>
            !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.')))
        {
            return false;
        }

        if (sourcePath.Any(character => character is < '!' or > '~' or ';' or ',')
            || sourcePath.Contains('?') || sourcePath.Contains('#')
            || !HasValidPercentEscapes(sourcePath))
        {
            return false;
        }

        var parsed = BrowserUrl.ParseResult($"https://{host}{sourcePath}").Url;
        if (parsed is null || parsed.Protocol != "https:"
            || parsed.Search.Length != 0 || parsed.Hash.Length != 0
            || IPAddress.TryParse(parsed.Origin.Host, out _))
        {
            return false;
        }

        if (parsed.Pathname != (sourcePath.Length == 0 ? "/" : sourcePath))
        {
            return false;
        }

        source = new(scheme?.ToLowerInvariant(), parsed.Origin.Host!, sourcePort, anyPort, sourcePath, subdomains);
        return true;
    }

    private static bool HasValidPercentEscapes(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }
            if (index + 2 >= value.Length || !TryHexDigit(value[index + 1], out _)
                || !TryHexDigit(value[index + 2], out _))
            {
                return false;
            }
            index += 2;
        }
        return true;
    }

    private static bool TryParseHashSource(string token, out HashSource hash)
    {
        hash = default;
        var (prefix, algorithm) = token.StartsWith("'sha256-", StringComparison.OrdinalIgnoreCase)
            ? ("'sha256-", "sha256")
            : token.StartsWith("'sha384-", StringComparison.OrdinalIgnoreCase)
                ? ("'sha384-", "sha384")
                : token.StartsWith("'sha512-", StringComparison.OrdinalIgnoreCase)
                    ? ("'sha512-", "sha512")
                    : ("", "");
        if (prefix.Length == 0 || !token.EndsWith('\'') || token.Length <= prefix.Length + 1)
        {
            return false;
        }

        var value = token[prefix.Length..^1];
        var unpaddedLength = value.TrimEnd('=').Length;
        var paddingLength = value.Length - unpaddedLength;
        if (unpaddedLength == 0 || paddingLength > 2)
        {
            return false;
        }
        for (var index = 0; index < unpaddedLength; index++)
        {
            var character = value[index];
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'
                or '+' or '/' or '-' or '_'))
            {
                return false;
            }
        }

        hash = new(algorithm, value.Replace('-', '+').Replace('_', '/'));
        return true;
    }

    private static bool TryParseNonceSource(string token, out string nonce)
    {
        nonce = "";
        const string prefix = "'nonce-";
        if (!token.StartsWith(prefix, StringComparison.Ordinal) || !token.EndsWith('\'')
            || token.Length <= prefix.Length + 1)
        {
            return false;
        }

        var value = token[prefix.Length..^1];
        var unpaddedLength = value.TrimEnd('=').Length;
        if (unpaddedLength == 0 || value.Length - unpaddedLength > 2)
        {
            return false;
        }
        for (var index = 0; index < unpaddedLength; index++)
        {
            var character = value[index];
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'
                or '+' or '/' or '-' or '_'))
            {
                return false;
            }
        }

        nonce = value;
        return true;
    }

    private readonly record struct HashSource(string Algorithm, string Expected);
    private readonly record struct HostSource(string? Scheme, string Host, int? Port, bool AnyPort, string Path,
        bool Subdomains, bool AnyHost = false)
    {
        internal static HostSource Any { get; } = new(null, "", null, false, "", false, true);
    }
    private sealed record SourceList(bool Self, SecurityOrigin[] Origins, HostSource[] Hosts, string[] Nonces,
        HashSource[] Hashes, string[] Schemes, bool UnsafeInline, bool UnsafeHashes);
    private sealed class Policy(IReadOnlyDictionary<string, SourceList> directives, bool upgradesInsecureRequests)
    {
        internal bool UpgradesInsecureRequests => upgradesInsecureRequests;
        internal bool HasBaseUri => directives.ContainsKey("base-uri");
        internal SourceList? Directive(string name) =>
            directives.GetValueOrDefault(name) ?? directives.GetValueOrDefault("default-src");

        internal SourceList? ScriptElementDirective() =>
            directives.GetValueOrDefault("script-src-elem") ?? Directive("script-src");

        internal SourceList? StyleElementDirective() =>
            directives.GetValueOrDefault("style-src-elem") ?? Directive("style-src");

        internal SourceList? StyleAttributeDirective() =>
            directives.GetValueOrDefault("style-src-attr") ?? Directive("style-src");

        internal SourceList? FormActionDirective() => directives.GetValueOrDefault("form-action");
        internal SourceList? BaseUriDirective() => directives.GetValueOrDefault("base-uri");
    }
}
