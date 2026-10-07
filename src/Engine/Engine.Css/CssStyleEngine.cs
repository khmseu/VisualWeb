using System.Collections.ObjectModel;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Css;

/// <summary>Static cascade origins; animation/transition origins are deferred.</summary>
/// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#cascading-origins">origins</see>.</remarks>
public enum CssOrigin { UserAgent, User, Author }
public sealed record CssStyleSource(string Text, CssOrigin Origin = CssOrigin.Author);

/// <summary>A computed snapshot, not a live style object or a layout-used-value result.</summary>
/// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#computed">computed values</see>.
/// Percentages and auto sizing are deliberately retained for layout.</remarks>
public sealed class CssComputedStyle
{
    public IReadOnlyDictionary<string, CssValue> Properties { get; }
    public CssValue this[string name] => Properties[name];
    internal CssComputedStyle(Dictionary<string, CssValue> properties) => Properties = new ReadOnlyDictionary<string, CssValue>(properties);
}

public sealed record CssStyleResult(IReadOnlyDictionary<DomElement, CssComputedStyle> Styles,
    IReadOnlyList<CssDiagnostic> Diagnostics);

/// <summary>Renderer-local static style computation with explicit unsupported-feature diagnostics.</summary>
/// <remarks>Spec: css-cascade; <see href="https://www.w3.org/TR/css-cascade-5/#cascade">cascade</see>.
/// No loading, mutation observation, dynamic state, layers, conditions or scripting.</remarks>
public static class CssStyleEngine
{
    /// <summary>Minimal static UA defaults, not the complete HTML rendering stylesheet.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#rendering">rendering</see>.
    /// Property semantics use css-box, css-backgrounds, css-sizing, css-display, css-fonts and css-text.</remarks>
    public const string UserAgentStyleSheet = """
        html, body, div, p, section, article, aside, nav, header, footer, main, address,
        blockquote, pre, h1, h2, h3, h4, h5, h6, ul, ol, li, dl, dt, dd, figure, figcaption, hr, form { display: block }
        input, button, textarea { display: inline-block }
        input[type=hidden i] { display: none !important }
        head, title, base, link, meta, style, script { display: none }
        body { margin: 8px }
        p, blockquote, pre, ul, ol, dl, figure { margin-top: 1em; margin-bottom: 1em }
        blockquote { margin-left: 40px; margin-right: 40px }
        ul, ol { padding-left: 40px }
        dd { margin-left: 40px }
        h1 { font-size: 2em; margin-top: .67em; margin-bottom: .67em; font-weight: bold }
        h2 { font-size: 1.5em; margin-top: .83em; margin-bottom: .83em; font-weight: bold }
        h3 { font-size: 1.17em; margin-top: 1em; margin-bottom: 1em; font-weight: bold }
        h4 { margin-top: 1.33em; margin-bottom: 1.33em; font-weight: bold }
        h5 { font-size: .83em; margin-top: 1.67em; margin-bottom: 1.67em; font-weight: bold }
        h6 { font-size: .67em; margin-top: 2.33em; margin-bottom: 2.33em; font-weight: bold }
        b, strong { font-weight: bolder }
        i, em, cite, var { font-style: italic }
        pre { white-space: pre; font-family: monospace }
        code, kbd, samp { font-family: monospace }
        """;

    public static CssStyleResult Compute(DomDocument document, IEnumerable<CssStyleSource>? sources = null,
        bool includeUserAgent = true, CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new CssContext(options, cancellationToken);
        var rules = new List<StyleRule>();
        var order = 0;
        var sourceCount = 0;
        if (includeUserAgent) { Compile(new(UserAgentStyleSheet, CssOrigin.UserAgent)); }
        foreach (var source in sources ?? []) { Compile(source); }
        var styles = new Dictionary<DomElement, CssComputedStyle>();
        foreach (var node in document.Descendants())
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (node is not DomElement element) { continue; }
            if (styles.Count >= context.Options.MaxElements) { throw new CssLimitException("CSS styled element limit exceeded."); }
            var candidates = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                if (rule.Selectors.Match(element, context) is not { } specificity) { continue; }
                foreach (var assignment in rule.Assignments) { Add(assignment, rule.Origin, false, specificity, rule.Order); }
            }
            if (element.GetAttribute("style") is { } inline)
            {
                var declarations = CssSyntax.ParseDeclarations(inline, context.Options, cancellationToken);
                Import(declarations.Diagnostics);
                var assignments = CssProperties.Expand(declarations.Declarations, context);
                foreach (var assignment in assignments) { Add(assignment, CssOrigin.Author, true, new(), order); }
            }
            var parent = element.ParentNode is DomElement p ? styles[p] : null;
            var computed = new Dictionary<string, CssValue>(StringComparer.Ordinal);
            foreach (var (name, property) in CssProperties.Registry)
            {
                var inherited = parent is null ? property.Initial : parent[name];
                var value = Winner(name, property, inherited);
                computed[name] = value;
            }
            var parentSize = parent?["font-size"] is CssLength ps ? ps.Value : 16;
            var rootSize = document.DocumentElement is { } root && styles.TryGetValue(root, out var rootStyle)
                && rootStyle["font-size"] is CssLength rs ? rs.Value : 16;
            computed["font-size"] = ResolveFontSize(computed["font-size"], parentSize, rootSize);
            var fontSize = ((CssLength)computed["font-size"]).Value;
            if (element == document.DocumentElement) { rootSize = fontSize; }
            if (computed["font-weight"] is CssKeyword relative)
            {
                var parentWeight = parent?["font-weight"] is CssNumber pw ? pw.Value : 400;
                computed["font-weight"] = new CssNumber(relative.Value == "bolder"
                    ? parentWeight < 350 ? 400 : parentWeight < 550 ? 700 : Math.Max(900, parentWeight)
                    : parentWeight < 550 ? Math.Min(100, parentWeight) : parentWeight < 750 ? 400 : 700);
            }
            if (computed["color"] is CssKeyword { Value: "currentcolor" })
            {
                computed["color"] = parent?["color"] ?? CssProperties.Registry["color"].Initial;
            }
            foreach (var name in computed.Keys.ToArray())
            {
                if (computed[name] is CssKeyword { Value: "currentcolor" }) { computed[name] = computed["color"]; }
                else if (name != "font-size" && computed[name] is CssLength length)
                {
                    var resolved = length.Unit switch
                    {
                        "em" => new CssLength(length.Value * fontSize, "px"),
                        "rem" => new CssLength(length.Value * rootSize, "px"),
                        "%" when name == "line-height" => new CssLength(length.Value / 100 * fontSize, "px"),
                        _ => length
                    };
                    if (!double.IsFinite(resolved.Value)) { throw new CssLimitException("Computed CSS length exceeds finite numeric range."); }
                    computed[name] = resolved;
                }
            }
            foreach (var side in new[] { "top", "right", "bottom", "left" })
            {
                if (computed["border-" + side + "-style"] is CssKeyword { Value: "none" or "hidden" })
                {
                    computed["border-" + side + "-width"] = new CssLength(0, "px");
                }
            }
            styles.Add(element, new(computed));

            void Add(CssAssignment assignment, CssOrigin origin, bool isInline, CssSpecificity specificity, int sourceOrder)
            {
                if (!candidates.TryGetValue(assignment.Name, out var list)) { candidates[assignment.Name] = list = []; }
                list.Add(new(assignment.Value, origin, assignment.Important, isInline, specificity, sourceOrder, list.Count));
            }

            CssValue Winner(string name, CssProperty property, CssValue inherited)
            {
                if (!candidates.TryGetValue(name, out var list)) { return property.Inherited ? inherited : property.Initial; }
                foreach (var candidate in list.OrderByDescending(c => c.Rank).ThenByDescending(c => c.Inline)
                    .ThenByDescending(c => c.Specificity).ThenByDescending(c => c.Order).ThenByDescending(c => c.DeclarationOrder).ToList())
                {
                    if (candidate.Value is CssKeyword { Value: "revert" })
                    {
                        list = list.Where(c => c.Origin < candidate.Origin).ToList();
                        candidates[name] = list;
                        return Winner(name, property, inherited);
                    }
                    return candidate.Value switch
                    {
                        CssKeyword { Value: "initial" } => property.Initial,
                        CssKeyword { Value: "inherit" } => inherited,
                        CssKeyword { Value: "unset" } => property.Inherited ? inherited : property.Initial,
                        _ => candidate.Value
                    };
                }
                return property.Inherited ? inherited : property.Initial;
            }
        }
        return new(new ReadOnlyDictionary<DomElement, CssComputedStyle>(styles), context.Diagnostics.AsReadOnly());

        void Compile(CssStyleSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (++sourceCount > context.Options.MaxStyleSources) { throw new CssLimitException("CSS source count limit exceeded."); }
            if (!Enum.IsDefined(source.Origin)) { throw new ArgumentOutOfRangeException(nameof(sources), "Unknown CSS origin."); }
            var sheet = CssSyntax.ParseStyleSheet(source.Text, context.Options, cancellationToken);
            Import(sheet.Diagnostics);
            foreach (var rule in sheet.Rules)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                if (rule.AtName is not null)
                {
                    context.Error("unsupported-at-rule", rule.Offset, $"@{rule.AtName} is not evaluated in the static subset.");
                    continue;
                }
                CssSelectorList selectors;
                try { selectors = CssSelectorList.Compile(rule.Prelude, context); }
                catch (FormatException exception)
                {
                    context.Error("invalid-selector", rule.Offset, exception.Message);
                    continue;
                }
                catch (UnsupportedCssException exception)
                {
                    context.Error("unsupported-selector", exception.Offset, exception.Message);
                    continue;
                }
                var declarations = CssSyntax.Declarations(rule.Block ?? [], context);
                if (rules.Count >= context.Options.MaxRules) { throw new CssLimitException("CSS compiled rule limit exceeded."); }
                rules.Add(new(selectors, CssProperties.Expand(declarations, context).AsReadOnly(), source.Origin, order++));
            }
        }

        void Import(IEnumerable<CssDiagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics) { context.Error(diagnostic.Code, diagnostic.Offset, diagnostic.Message); }
        }
    }

    private static CssLength ResolveFontSize(CssValue value, double parentSize, double rootSize)
    {
        var number = value switch
        {
            CssLength { Unit: "px" } length => length.Value,
            CssLength { Unit: "em" } length => length.Value * parentSize,
            CssLength { Unit: "rem" } length => length.Value * rootSize,
            CssLength { Unit: "%" } length => length.Value / 100 * parentSize,
            CssKeyword { Value: "larger" } => parentSize * 1.2,
            CssKeyword { Value: "smaller" } => parentSize / 1.2,
            _ => throw new InvalidOperationException("Unexpected font-size value.")
        };
        if (!double.IsFinite(number)) { throw new CssLimitException("Computed font-size exceeds finite numeric range."); }
        return new(number, "px");
    }

    private sealed record StyleRule(CssSelectorList Selectors, IReadOnlyList<CssAssignment> Assignments, CssOrigin Origin, int Order);
    private sealed record Candidate(CssValue Value, CssOrigin Origin, bool Important, bool Inline,
        CssSpecificity Specificity, int Order, int DeclarationOrder)
    {
        internal int Rank => Important ? Origin switch { CssOrigin.UserAgent => 5, CssOrigin.User => 4, _ => 3 } : (int)Origin;
    }
}
