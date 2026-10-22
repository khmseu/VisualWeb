using VisualWeb.Core.Url;
using VisualWeb.Engine.Css;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

internal sealed record CssImportResult(IReadOnlyList<BrowserUrl> Imports, string Css);

internal static class CssImports
{
    private enum ImportApplicability
    {
        AppliesToScreen,
        DoesNotApplyToScreen
    }

    public static CssImportResult Parse(string css, BrowserUrl? baseUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(css);
        var parsed = CssSyntax.ParseStyleSheet(css, cancellationToken: cancellationToken);
        var imports = new List<BrowserUrl>();
        var ranges = new List<(int Start, int End)>();
        var sawNonImport = false;

        foreach (var rule in parsed.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(rule.AtName, "import", StringComparison.OrdinalIgnoreCase))
            {
                sawNonImport = true;
                continue;
            }
            if (sawNonImport)
            {
                throw new PageNavigationException("CSS @import rules must precede all other stylesheet rules.");
            }
            if (rule.Block is not null)
            {
                throw new PageNavigationException("Block-form CSS @import rules are unsupported.");
            }
            if (rule.EndOffset <= rule.Offset || rule.EndOffset > parsed.NormalizedText.Length
                || parsed.NormalizedText[rule.EndOffset - 1] != ';')
            {
                throw new PageNavigationException("CSS @import rules must end with a semicolon.");
            }
            if (baseUrl is null)
            {
                throw new PageNavigationException("CSS @import requires a document or stylesheet URL.");
            }

            var (reference, applicability) = ImportReference(rule.Prelude);
            if (applicability != ImportApplicability.AppliesToScreen)
            {
                ranges.Add((rule.Offset, rule.EndOffset));
                continue;
            }

            var result = BrowserUrl.ParseResult(reference, baseUrl);
            var url = result.Url ?? throw new PageNavigationException($"Invalid CSS @import URL: {result.Error}");
            if (url.Href.Length > RendererProtocol.MaxTextCharacters)
            {
                throw new PageNavigationException("CSS @import URL limit exceeded.");
            }
            if (url.Protocol == "file:")
            {
                throw new PageNavigationException("CSS @import file URLs are blocked.");
            }
            if (url.Protocol is not ("http:" or "https:" or "data:"))
            {
                throw new PageNavigationException($"CSS @import scheme {url.Protocol} is unsupported.");
            }
            imports.Add(url);
            ranges.Add((rule.Offset, rule.EndOffset));
        }

        if (ranges.Count == 0) { return new(imports.AsReadOnly(), css); }
        var normalized = parsed.NormalizedText;
        for (var index = ranges.Count - 1; index >= 0; index--)
        {
            var (start, end) = ranges[index];
            if (start < 0 || end <= start || end > normalized.Length)
            {
                throw new PageNavigationException("CSS @import source range is invalid.");
            }
            normalized = normalized.Remove(start, end - start);
        }
        return new(imports.AsReadOnly(), normalized);
    }

    private static (string Reference, ImportApplicability Applicability) ImportReference(IReadOnlyList<CssToken> prelude)
    {
        var tokens = prelude.Where(token => token.Kind != CssTokenKind.Whitespace).ToArray();
        int mediaStart;
        string reference;
        if (tokens.Length >= 1 && tokens[0].Kind is CssTokenKind.String or CssTokenKind.Url)
        {
            reference = tokens[0].Value;
            mediaStart = 1;
        }
        else if (tokens.Length >= 3 && tokens[0].Kind == CssTokenKind.Function
            && tokens[0].Value.Equals("url", StringComparison.OrdinalIgnoreCase)
            && tokens[1].Kind == CssTokenKind.String && tokens[2].Kind == CssTokenKind.CloseParen)
        {
            reference = tokens[1].Value;
            mediaStart = 3;
        }
        else
        {
            throw new PageNavigationException("CSS @import requires a valid string or url() value.");
        }

        if (mediaStart == tokens.Length)
        {
            return (reference, ImportApplicability.AppliesToScreen);
        }

        if (mediaStart == tokens.Length)
        {
            return (reference, ImportApplicability.AppliesToScreen);
        }

        var mediaApplicability = ParseMediaTypes(tokens, mediaStart);
        if (mediaApplicability is not null)
        {
            return (reference, mediaApplicability.Value);
        }

        throw new PageNavigationException("CSS @import supports optional media lists containing only exact all, screen, or print media types.");
    }

    private static ImportApplicability? ParseMediaTypes(IReadOnlyList<CssToken> tokens, int start)
    {
        var appliesToScreen = false;
        var expectMediaType = true;
        for (var index = start; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (expectMediaType)
            {
                if (token.Kind != CssTokenKind.Ident)
                {
                    return null;
                }

                if (token.Value.Equals("all", StringComparison.OrdinalIgnoreCase)
                    || token.Value.Equals("screen", StringComparison.OrdinalIgnoreCase))
                {
                    appliesToScreen = true;
                }
                else if (!token.Value.Equals("print", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                expectMediaType = false;
                continue;
            }

            if (token.Kind != CssTokenKind.Comma)
            {
                return null;
            }

            expectMediaType = true;
        }

        if (expectMediaType)
        {
            return null;
        }

        return appliesToScreen ? ImportApplicability.AppliesToScreen : ImportApplicability.DoesNotApplyToScreen;
    }
}
