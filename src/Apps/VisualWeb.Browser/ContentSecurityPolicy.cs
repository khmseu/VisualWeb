using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

/// <summary>Bounded response-header Content Security Policy source-list subset.</summary>
/// <remarks>Spec: csp3; <see href="https://www.w3.org/TR/CSP3/#parse-serialized-policy">parse a serialized CSP</see>,
/// <see href="https://www.w3.org/TR/CSP3/#framework-directive-source-list">source lists</see>, and
/// <see href="https://www.w3.org/TR/CSP3/#directive-default-src">default-src</see>.
/// Supports only default-src, script-src, and style-src with 'none', 'self', or exact HTTP(S) origins.</remarks>
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

    internal bool AllowsInlineScript() => policies.All(policy => policy.Directive("script-src") is null);
    internal bool AllowsInlineStyle() => policies.All(policy => policy.Directive("style-src") is null);
    internal bool AllowsScript(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.Directive("script-src"), resource, documentOrigin));
    internal bool AllowsStyle(BrowserUrl resource, SecurityOrigin documentOrigin) =>
        policies.All(policy => Allows(policy.Directive("style-src"), resource, documentOrigin));

    private static bool Allows(SourceList? sources, BrowserUrl resource, SecurityOrigin documentOrigin) =>
        sources is null
        || sources.Self && documentOrigin.IsSameOrigin(resource.Origin)
        || sources.Origins.Any(origin => origin.IsSameOrigin(resource.Origin));

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
            if (name is not ("default-src" or "script-src" or "style-src"))
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
        if (tokens.Length == 0) { return new(false, []); }
        var hasNone = false;
        foreach (var token in tokens)
        {
            if (token.Equals("'none'", StringComparison.OrdinalIgnoreCase)) { hasNone = true; break; }
        }
        if (hasNone)
        {
            if (tokens.Length != 1)
            { throw new PageNavigationException($"Content-Security-Policy {directive} cannot combine 'none' with other sources."); }
            return new(false, []);
        }

        var self = false;
        var origins = new List<SecurityOrigin>();
        foreach (var token in tokens)
        {
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
                    $"Content-Security-Policy {directive} source '{token}' is unsupported; use 'none', 'self', or an exact HTTP(S) origin.");
            }
            if (!origins.Any(origin => origin.IsSameOrigin(parsed.Origin))) { origins.Add(parsed.Origin); }
        }
        return new(self, origins.ToArray());
    }

    private sealed record SourceList(bool Self, SecurityOrigin[] Origins);
    private sealed class Policy(IReadOnlyDictionary<string, SourceList> directives)
    {
        internal SourceList? Directive(string name) =>
            directives.GetValueOrDefault(name) ?? directives.GetValueOrDefault("default-src");
    }
}
