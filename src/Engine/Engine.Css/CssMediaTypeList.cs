namespace VisualWeb.Engine.Css;

/// <summary>Bounded static media-type lists for a screen-only rendering environment.</summary>
/// <remarks>Spec: css-mediaqueries-5;
/// <see href="https://www.w3.org/TR/mediaqueries-5/#media-types">media types</see> and
/// <see href="https://www.w3.org/TR/mediaqueries-5/#typedef-media-query-list">media query lists</see>.
/// Only empty lists and comma-separated all/screen/print identifiers are supported. Callers must surface a false
/// return as an unsupported query rather than treating it as a nonmatching list. The input token count is bounded
/// by <see cref="CssOptions.MaxTokens"/>.</remarks>
public static class CssMediaTypeList
{
    private static readonly CssOptions DefaultOptions = new();

    public static bool TryMatchesScreen(IReadOnlyList<CssToken> tokens, out bool matches,
        CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        cancellationToken.ThrowIfCancellationRequested();
        var limits = options ?? DefaultOptions;
        limits.Validate();
        var count = tokens.Count;
        if (count > limits.MaxTokens) { throw new CssLimitException("CSS media token limit exceeded."); }
        matches = false;
        var applies = false;
        var sawToken = false;
        var expectType = true;
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = tokens[index];
            if (token is null) { throw new ArgumentException("Media tokens cannot contain null entries.", nameof(tokens)); }
            if (token.Kind == CssTokenKind.Whitespace) { continue; }
            sawToken = true;
            if (expectType)
            {
                if (token.Kind != CssTokenKind.Ident) { return false; }
                if (token.Value is null) { throw new ArgumentException("Media token values cannot be null.", nameof(tokens)); }
                if (token.Value.Length is not (3 or 5 or 6)) { return false; }
                var name = CssText.Lower(token.Value);
                if (name is "all" or "screen") { applies = true; }
                else if (name != "print") { return false; }
                expectType = false;
            }
            else
            {
                if (token.Kind != CssTokenKind.Comma) { return false; }
                expectType = true;
            }
        }
        if (!sawToken) { matches = true; return true; }
        if (expectType) { return false; }
        matches = applies;
        return true;
    }
}
