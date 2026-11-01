namespace VisualWeb.Engine.Css;

/// <summary>Bounded parsing for the HTML legacy font face presentational hint.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3">font face hint</see>.
/// Font-family values use css-fonts <see href="https://www.w3.org/TR/css-fonts-4/#font-family-prop">family syntax</see>.</remarks>
internal static class HtmlLegacyFontFace
{
    internal static CssFontFamilies? Parse(string input, CssOptions options, CancellationToken cancellationToken)
    {
        var tokenizer = new CssTokenizer(input, options, cancellationToken);
        var tokens = new List<CssToken>();
        while (true)
        {
            var token = tokenizer.Read();
            if (token.Kind == CssTokenKind.Eof) { break; }
            tokens.Add(token);
        }
        return tokenizer.Diagnostics.Count == 0 ? CssProperties.ParseFamilies(tokens) : null;
    }
}
