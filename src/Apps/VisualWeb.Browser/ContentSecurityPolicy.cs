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
/// <see href="https://www.w3.org/TR/CSP3/#directive-default-src">default-src</see>.
/// Supports default-src, script-src, and style-src with 'none', 'self', exact HTTP(S) origins,
/// data:/http:/https: resource scheme sources (including HTTP-to-HTTPS upgrades), strict nonce-source
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
            if (field.Length > MaxFieldCharacters)
            { throw new PageNavigationException($"Content-Security-Policy field length limit ({MaxFieldCharacters}) exceeded."); }
            policies[fieldIndex] = ParsePolicy(field, diagnostics);
        }
        return new(policies);
    }

    internal bool AllowsInlineScript() => policies.All(policy => AllowsAllInline(policy.Directive("script-src")));
    internal bool AllowsInlineScript(string? nonce) => policies.All(policy => AllowsInlineNonce(policy.Directive("script-src"), nonce));
    internal bool AllowsInlineScript(string? nonce, string source) => AllowsInline("script-src", nonce, source);
    internal bool AllowsInlineStyle() => policies.All(policy => AllowsAllInline(policy.Directive("style-src")));
    internal bool AllowsInlineStyle(string? nonce) => policies.All(policy => AllowsInlineNonce(policy.Directive("style-src"), nonce));
    internal bool AllowsInlineStyle(string? nonce, string source) => AllowsInline("style-src", nonce, source);
    internal bool AllowsInlineStyleAttribute() => policies.All(policy => AllowsAllInline(policy.StyleAttributeDirective()));
    internal bool AllowsInlineStyleAttribute(string source) => AllowsInline("style-src", null, source, styleAttribute: true);
    internal bool AllowsScript(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.Directive("script-src"), resource, documentOrigin));
    internal bool AllowsStyle(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.Directive("style-src"), resource, documentOrigin));

    private static bool AllowsInlineNonce(SourceList? sources, string? nonce) => AllowsAllInline(sources)
        || nonce is { Length: > 0 } && sources!.Nonces.Contains(nonce, StringComparer.Ordinal);

    private static bool AllowsAllInline(SourceList? sources) => sources is null
        || sources.UnsafeInline && sources.Nonces.Length == 0 && sources.Hashes.Length == 0;

    private bool AllowsInline(string directiveName, string? nonce, string source, bool styleAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sourceLists = policies.Select(policy => styleAttribute
            ? policy.StyleAttributeDirective()
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

    private static bool Allows(SourceList? sources, BrowserUrl resource, SecurityOrigin documentOrigin) =>
        sources is null
        || sources.Self && documentOrigin.IsSameOrigin(resource.Origin)
        || sources.Origins.Any(origin => origin.IsSameOrigin(resource.Origin))
        || sources.Schemes.Any(scheme => SchemeMatches(scheme, resource.Protocol));

    private static bool SchemeMatches(string sourceScheme, string resourceScheme) =>
        sourceScheme.Equals(resourceScheme, StringComparison.OrdinalIgnoreCase)
        || sourceScheme.Equals("http:", StringComparison.OrdinalIgnoreCase)
            && resourceScheme.Equals("https:", StringComparison.OrdinalIgnoreCase);

    private static Policy ParsePolicy(string field, IList<string> diagnostics)
    {
        var directives = new Dictionary<string, SourceList>(StringComparer.OrdinalIgnoreCase);
        var serialized = field.Split(';');
        if (serialized.Length > MaxDirectives)
        { throw new PageNavigationException($"Content-Security-Policy directive count limit ({MaxDirectives}) exceeded."); }
        foreach (var item in serialized)
        {
            var parts = item.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { continue; }
            var name = parts[0].ToLowerInvariant();
            if (name is not ("default-src" or "script-src" or "style-src" or "style-src-attr"))
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
        return new(directives);
    }

    private static SourceList ParseSources(string directive, ReadOnlySpan<string> tokens)
    {
        if (tokens.Length > MaxSources)
        { throw new PageNavigationException($"Content-Security-Policy {directive} source count limit ({MaxSources}) exceeded."); }
        if (tokens.Length == 0) { return new(false, [], [], [], [], false, false); }
        var hasNone = false;
        foreach (var token in tokens)
        {
            if (token.Equals("'none'", StringComparison.OrdinalIgnoreCase)) { hasNone = true; break; }
        }
        if (hasNone)
        {
            if (tokens.Length != 1)
            { throw new PageNavigationException($"Content-Security-Policy {directive} cannot combine 'none' with other sources."); }
            return new(false, [], [], [], [], false, false);
        }

        var self = false;
        var origins = new List<SecurityOrigin>();
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
            var parsed = BrowserUrl.ParseResult(token).Url;
            if (parsed is null || parsed.Protocol is not ("http:" or "https:")
                || parsed.Username.Length != 0 || parsed.Password.Length != 0
                || parsed.Pathname != "/" || parsed.Search.Length != 0 || parsed.Hash.Length != 0)
            {
                throw new PageNavigationException(
                    $"Content-Security-Policy {directive} source '{token}' is unsupported; use 'none', 'self', 'unsafe-inline', 'unsafe-hashes', data:/http:/https:, or an exact HTTP(S) origin.");
            }
            if (!origins.Any(origin => origin.IsSameOrigin(parsed.Origin))) { origins.Add(parsed.Origin); }
        }
        return new(self, origins.ToArray(), nonces.ToArray(), hashes.ToArray(), schemes.ToArray(), unsafeInline, unsafeHashes);
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
    private sealed record SourceList(bool Self, SecurityOrigin[] Origins, string[] Nonces, HashSource[] Hashes,
        string[] Schemes, bool UnsafeInline, bool UnsafeHashes);
    private sealed class Policy(IReadOnlyDictionary<string, SourceList> directives)
    {
        internal SourceList? Directive(string name) =>
            directives.GetValueOrDefault(name) ?? directives.GetValueOrDefault("default-src");

        internal SourceList? StyleAttributeDirective() =>
            directives.GetValueOrDefault("style-src-attr") ?? Directive("style-src");
    }
}
