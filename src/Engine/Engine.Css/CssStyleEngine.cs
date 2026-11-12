using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
/// Screen-only <see href="https://www.w3.org/TR/css-conditional-3/#at-ruledef-media">@media</see> type lists use
/// css-conditional-3 and css-mediaqueries-5. The bounded text-decoration subset uses css-text-decoration
/// <see href="https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-line">line values</see> and
/// <see href="https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration">shorthand</see> plus
/// <see href="https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-color">decoration color</see>,
/// and css-text-decoration-4 <see href="https://drafts.csswg.org/css-text-decor-4/#propdef-text-decoration-thickness">line thickness</see>.
/// Finite marker values use css2-lists
/// <see href="https://www.w3.org/TR/CSS22/generate.html#propdef-list-style-type">list style types</see>.
/// Alphabetic and Roman values use css-counter-styles
/// <see href="https://www.w3.org/TR/css-counter-styles-3/#simple-alphabetic">alphabetic styles</see> and
/// <see href="https://www.w3.org/TR/css-counter-styles-3/#simple-numeric">numeric styles</see>.
/// Supported HTML list type and alignment hints enter the
/// <see href="https://www.w3.org/TR/css-cascade-5/#preshint">author presentational hint origin</see>.
/// Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">alignment hints</see>.
/// Center-element and div alignment hints use
/// <see href="https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3">flow-content rules</see>.
/// Table centering hints use the table alignment rule in tables-2.
/// HTML font color hints use the <see href="https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3">phrasing rule</see>
/// and <see href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-a-legacy-colour-value">legacy color parsing</see>.
/// HTML body link-color hints use the <see href="https://html.spec.whatwg.org/multipage/rendering.html#the-page">page rule</see>,
/// the same legacy color parsing, and the static <c>:link</c> matcher without history access.
/// HTML font face hints use the phrasing rule and
/// <see href="https://www.w3.org/TR/css-fonts-4/#font-family-prop">CSS font-family syntax</see>.
/// Spec: html, css-fonts.
/// Spec: html; font size uses <see href="https://html.spec.whatwg.org/multipage/rendering.html#rules-for-parsing-a-legacy-font-size">legacy font size parsing</see>
/// and the <see href="https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3">phrasing hint</see>;
/// spec: css-fonts; <see href="https://www.w3.org/TR/css-fonts-4/#font-size-prop">font-size</see>.
/// No loading, mutation observation, dynamic state, layers or scripting.</remarks>
public static class CssStyleEngine
{
    /// <summary>Minimal static UA defaults, not the complete HTML rendering stylesheet.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#rendering">rendering</see>,
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3">flow content defaults</see>,
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#lists">list defaults</see>,
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#hiddenCSS">hidden elements</see>,
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3">phrasing defaults</see>,
    /// including the <c>mark</c> highlight colors and
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#the-hr-element-2">horizontal-rule defaults</see>.
    /// Property semantics use css-box, css-backgrounds, css-sizing, css-display, css-fonts and css-text.</remarks>
    public const string UserAgentStyleSheet = """
        html, body, div, center, p, section, article, aside, nav, header, footer, main, address,
        blockquote, pre, listing, xmp, plaintext, h1, h2, h3, h4, h5, h6, ul, ol, menu, li, dl, dt, dd, figure, figcaption, hr, form, search { display: block }
        input, button, textarea, select { display: inline-block }
        input[type=hidden i] { display: none !important }
        head, title, base, link, meta, style, script { display: none }
        [hidden]:not([hidden=until-found i]):not(embed) { display: none }
        hr { color: gray; border-style: inset; border-width: 1px; margin-top: .5em; margin-bottom: .5em; margin-left: auto; margin-right: auto }
        dir { display: block; padding-left: 40px }
        body { margin: 8px }
        p, blockquote, pre, listing, xmp, plaintext, dir, menu, ul, ol, dl, figure { margin-top: 1em; margin-bottom: 1em }
        :is(dir, dl, menu, ol, ul) :is(dir, dl, menu, ol, ul) { margin-top: 0; margin-bottom: 0 }
        blockquote, figure { margin-left: 40px; margin-right: 40px }
        ul, ol, menu { padding-left: 40px }
        dir, menu, ul { list-style-type: disc }
        ol { list-style-type: decimal }
        :is(dir, menu, ol, ul) :is(dir, menu, ul) { list-style-type: circle }
        :is(dir, menu, ol, ul) :is(dir, menu, ol, ul) :is(dir, menu, ul) { list-style-type: square }
        li { display: list-item }
        table { display: table; border-spacing: 2px }
        caption { display: table-caption }
        colgroup { display: table-column-group }
        col { display: table-column }
        tbody { display: table-row-group }
        thead { display: table-header-group }
        tfoot { display: table-footer-group }
        tr { display: table-row }
        td, th { display: table-cell; padding: 1px; vertical-align: top }
        dd { margin-left: 40px }
        h1 { font-size: 2em; margin-top: .67em; margin-bottom: .67em; font-weight: bold }
        h2 { font-size: 1.5em; margin-top: .83em; margin-bottom: .83em; font-weight: bold }
        h3 { font-size: 1.17em; margin-top: 1em; margin-bottom: 1em; font-weight: bold }
        h4 { margin-top: 1.33em; margin-bottom: 1.33em; font-weight: bold }
        h5 { font-size: .83em; margin-top: 1.67em; margin-bottom: 1.67em; font-weight: bold }
        h6 { font-size: .67em; margin-top: 2.33em; margin-bottom: 2.33em; font-weight: bold }
        b, strong { font-weight: bolder }
        i, em, cite, dfn, var { font-style: italic }
        address { font-style: italic }
        pre, listing, xmp, plaintext { white-space: pre; font-family: monospace }
        code, kbd, samp, tt { font-family: monospace }
        big { font-size: larger }
        small { font-size: smaller }
        :link { color: #0000EE; text-decoration: underline }
        ins, u { text-decoration: underline }
        del, s, strike { text-decoration: line-through }
        mark { background-color: yellow; color: black }
        """;

    /// <summary>Finds a supported HTML list marker hint without accepting unsupported list types.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#lists">list type hints</see>.
    /// A false result means the attribute is absent or unsupported; layout rejects unsupported attributes.</remarks>
    public static bool TryGetListTypeHint(DomElement element, [NotNullWhen(true)] out string? marker)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.LocalName is not ("ol" or "ul" or "li"))
        {
            marker = null;
            return false;
        }
        var type = element.GetAttribute("type");
        marker = element.LocalName switch
        {
            "ol" or "li" when type == "1" => "decimal",
            "ol" or "li" when type == "a" => "lower-alpha",
            "ol" or "li" when type == "A" => "upper-alpha",
            "ol" or "li" when type == "i" => "lower-roman",
            "ol" or "li" when type == "I" => "upper-roman",
            "ul" or "li" when type is { Length: 4 or 6 } => CssText.Lower(type) switch
            {
                "disc" => "disc",
                "circle" => "circle",
                "square" => "square",
                "none" => "none",
                _ => null
            },
            _ => null
        };
        return marker is not null;
    }

    /// <summary>Reports whether the bounded centered-table alignment hint applies.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table alignment hints</see>.</remarks>
    public static bool HasSupportedTableCenterAlignmentHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName == "table"
            && element.GetAttribute("align") is { Length: 6 } align
            && CssText.Lower(align) == "center";
    }

    /// <summary>Reports whether the bounded HTML table border hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/tables.html#tables-2">table border rendering</see>.</remarks>
    public static bool HasSupportedTableBorderHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName == "table" && element.GetAttribute("border") is not null;
    }

    /// <summary>Reports whether a supported alignment hint applies to an HTML table cell.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-cell alignment hints</see>.</remarks>
    public static bool HasSupportedTableCellAlignmentHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("td" or "th") && GetAlignHint(element) is not null;
    }

    /// <summary>Reports whether the bounded HTML table-cell nowrap hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-cell nowrap hint</see>.</remarks>
    public static bool HasSupportedTableCellNowrapHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("td" or "th") && element.GetAttribute("nowrap") is not null;
    }

    /// <summary>Reports whether the bounded HTML table-cell height hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-cell height hint</see>.</remarks>
    public static bool HasSupportedTableCellHeightHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("td" or "th") && element.GetAttribute("height") is not null;
    }

    /// <summary>Reports whether the bounded HTML table-row height hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-row height hint</see>.</remarks>
    public static bool HasSupportedTableRowHeightHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName == "tr" && element.GetAttribute("height") is not null;
    }

    /// <summary>Reports whether the bounded HTML table-column width hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table column width hint</see>.</remarks>
    public static bool HasSupportedTableColumnWidthHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName == "col" && element.GetAttribute("width") is not null;
    }

    /// <summary>Reports whether the bounded HTML table-cell width hint applies.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-cell width hint</see>.</remarks>
    public static bool HasSupportedTableCellWidthHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("td" or "th") && element.GetAttribute("width") is not null;
    }

    /// <summary>Reports whether a supported alignment hint applies to an HTML table row or row group.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table row and row-group alignment hints</see>.</remarks>
    public static bool HasSupportedTableRowAlignmentHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("thead" or "tbody" or "tfoot" or "tr") && GetAlignHint(element) is not null;
    }

    /// <summary>Reports whether a supported legacy vertical-alignment hint applies to an HTML table cell.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table-cell alignment hints</see>.</remarks>
    public static bool HasSupportedTableCellValignHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("td" or "th") && GetValignHint(element) is not null;
    }

    /// <summary>Reports whether a supported legacy vertical-alignment hint applies to an HTML table row or row group.</summary>
    /// <remarks>Spec: html;
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table row and row-group alignment hints</see>.</remarks>
    public static bool HasSupportedTableRowValignHint(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.LocalName is ("thead" or "tbody" or "tfoot" or "tr") && GetValignHint(element) is not null;
    }

    /// <summary>Finds the supported flow-content text alignment hints.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3">flow content hints</see> and
    /// <see href="https://html.spec.whatwg.org/multipage/rendering.html#tables-2">table rendering hints</see>.</remarks>
    private static string? GetAlignHint(DomElement element)
    {
        if (element.LocalName == "center") { return "center"; }
        var supportsMiddleAlias = element.LocalName is
            ("div" or "td" or "th" or "thead" or "tbody" or "tfoot" or "tr");
        if (!supportsMiddleAlias
            && element.LocalName is not ("p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"))
        {
            return null;
        }
        var align = element.GetAttribute("align");
        if (supportsMiddleAlias)
        {
            return align is { Length: 4 or 5 or 6 or 7 or 9 } ? CssText.Lower(align) switch
            {
                "left" => "left",
                "right" => "right",
                "center" or "middle" or "absmiddle" => "center",
                "justify" => "justify",
                _ => null
            } : null;
        }
        return align is { Length: 4 or 5 or 6 or 7 } ? CssText.Lower(align) switch
        {
            "left" => "left",
            "right" => "right",
            "center" => "center",
            "justify" => "justify",
            _ => null
        } : null;
    }

    private static string? GetTableAncestorValignHint(DomElement element)
    {
        if (element.ParentNode is not DomElement { LocalName: "tr" } row) { return null; }
        if (GetValignHint(row) is { } rowAlignment) { return rowAlignment; }
        return row.ParentNode is DomElement { LocalName: "thead" or "tbody" or "tfoot" } group
            ? GetValignHint(group) : null;
    }

    private static DomElement? GetTableBorderCellRuleTable(DomElement element, out CssSpecificity specificity)
    {
        specificity = default;
        if (element.LocalName is not ("td" or "th")
            || element.ParentNode is not DomElement { LocalName: "tr" } row) { return null; }
        if (row.ParentNode is DomElement { LocalName: "table" } table)
        {
            specificity = new(0, 1, 3);
            return table;
        }
        if (row.ParentNode is DomElement { LocalName: "thead" or "tbody" or "tfoot" } group
            && group.ParentNode is DomElement { LocalName: "table" } groupTable)
        {
            specificity = new(0, 1, 4);
            return groupTable;
        }
        return null;
    }

    private static string? GetValignHint(DomElement element)
    {
        if (element.LocalName is not ("td" or "th" or "thead" or "tbody" or "tfoot" or "tr")) { return null; }
        var valign = element.GetAttribute("valign");
        return valign is { Length: 3 or 6 or 8 } ? CssText.Lower(valign) switch
        {
            "baseline" => "baseline",
            "top" => "top",
            "middle" => "middle",
            "bottom" => "bottom",
            _ => null
        } : null;
    }

    private static CssBorderSpacing? GetCellspacingHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "table" || element.GetAttribute("cellspacing") is not { } cellspacing) { return null; }
        context.TableSpacingCharacters(cellspacing.Length);
        if (!TryParseNonNegativeInteger(cellspacing, context.Cancellation, out var value)) { return null; }
        var length = new CssLength(value, "px");
        return new(length, length);
    }

    private readonly record struct TableBorderHint(CssLength Width, bool HasVisibleBorder);

    private static TableBorderHint? GetTableBorderHint(DomElement table, CssContext context,
        Dictionary<DomElement, TableBorderHint?> cache)
    {
        if (cache.TryGetValue(table, out var hint)) { return hint; }
        hint = null;
        if (table.LocalName == "table" && table.GetAttribute("border") is { } border)
        {
            context.TableBorderCharacters(border.Length);
            var parsed = TryParseNonNegativeInteger(border, context.Cancellation, out var value, "table border");
            if (!parsed) { value = 1; }
            hint = new TableBorderHint(new CssLength(value, "px"), !parsed || value != 0);
        }
        cache.Add(table, hint);
        return hint;
    }

    private static CssLength? GetTableWidthHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "table" || element.GetAttribute("width") is not { } width) { return null; }
        context.TableWidthCharacters(width.Length);
        return TryParseNonZeroDimensionValue(width, context.Cancellation, "HTML table width attribute");
    }

    private static CssLength? GetTableCellHeightHint(DomElement element, CssContext context)
    {
        if (element.LocalName is not ("td" or "th") || element.GetAttribute("height") is not { } height)
        { return null; }
        context.TableCellHeightCharacters(height.Length);
        return TryParseNonZeroDimensionValue(height, context.Cancellation, "HTML table-cell height attribute");
    }

    private static CssLength? GetTableRowHeightHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "tr" || element.GetAttribute("height") is not { } height) { return null; }
        context.TableRowHeightCharacters(height.Length);
        return TryParseDimensionValue(height, context.Cancellation, "HTML table-row height attribute");
    }

    private static CssLength? GetTableColumnWidthHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "col" || element.GetAttribute("width") is not { } width) { return null; }
        context.TableColumnWidthCharacters(width.Length);
        return TryParseDimensionValue(width, context.Cancellation, "HTML table-column width attribute");
    }

    private static CssLength? GetHorizontalRuleWidthHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "hr" || element.GetAttribute("width") is not { } width) { return null; }
        context.HorizontalRuleWidthCharacters(width.Length);
        return TryParseDimensionValue(width, context.Cancellation, "HTML horizontal-rule width attribute");
    }

    private static string? GetHorizontalRuleAlignmentHint(DomElement element)
    {
        if (element.LocalName != "hr") { return null; }
        var align = element.GetAttribute("align");
        return align is { Length: 4 or 5 or 6 } ? CssText.Lower(align) switch
        {
            "left" => "left",
            "right" => "right",
            "center" => "center",
            _ => null
        } : null;
    }

    private static CssLength? GetTableCellWidthHint(DomElement element, CssContext context)
    {
        if (element.LocalName is not ("td" or "th") || element.GetAttribute("width") is not { } width)
        { return null; }
        context.TableCellWidthCharacters(width.Length);
        return TryParseNonZeroDimensionValue(width, context.Cancellation, "HTML table-cell width attribute");
    }

    private static CssLength? GetTableCellPaddingHint(DomElement table, CssContext context,
        Dictionary<DomElement, CssLength?> cache)
    {
        if (cache.TryGetValue(table, out var hint)) { return hint; }
        hint = null;
        if (table.GetAttribute("cellpadding") is { } cellpadding)
        {
            context.TableCellPaddingCharacters(cellpadding.Length);
            if (TryParseNonNegativeInteger(cellpadding, context.Cancellation, out var value, "table cellpadding"))
            { hint = new(value, "px"); }
        }
        cache.Add(table, hint);
        return hint;
    }

    private static CssColor? GetBodyBackgroundColorHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "body" || element.GetAttribute("bgcolor") is not { } bgcolor) { return null; }
        context.BodyBackgroundColorCharacters(bgcolor.Length);
        return HtmlLegacyColor.Parse(bgcolor, context.Cancellation);
    }

    private static CssColor? GetTableBackgroundColorHint(DomElement element, CssContext context)
    {
        if (element.LocalName is not ("table" or "thead" or "tbody" or "tfoot" or "tr" or "td" or "th")
            || element.GetAttribute("bgcolor") is not { } bgcolor) { return null; }
        context.TableBackgroundColorCharacters(bgcolor.Length);
        return HtmlLegacyColor.Parse(bgcolor, context.Cancellation);
    }

    private static CssColor? GetBodyTextColorHint(DomElement element, CssContext context)
    {
        if (element.LocalName != "body" || element.GetAttribute("text") is not { } text) { return null; }
        context.BodyTextColorCharacters(text.Length);
        return HtmlLegacyColor.Parse(text, context.Cancellation);
    }

    private static List<CssColor> GetBodyLinkColorHints(DomDocument document, CssContext context)
    {
        var colors = new List<CssColor>();
        var elementCount = 0;
        foreach (var node in document.Descendants())
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (node is not DomElement element) { continue; }
            if (++elementCount > context.Options.MaxElements)
            { throw new CssLimitException("CSS styled element limit exceeded."); }
            if (element.LocalName != "body" || element.GetAttribute("link") is not { } link) { continue; }
            context.BodyLinkColorCharacters(link.Length);
            if (HtmlLegacyColor.Parse(link, context.Cancellation) is { } color) { colors.Add(color); }
        }
        return colors;
    }

    private static CssLength? TryParseNonZeroDimensionValue(string input, CancellationToken cancellationToken,
        string attributeName)
    {
        var dimension = TryParseDimensionValue(input, cancellationToken, attributeName);
        return dimension is { Value: not 0 } ? dimension : null;
    }

    private static CssLength? TryParseDimensionValue(string input, CancellationToken cancellationToken,
        string attributeName)
    {
        var position = 0;
        while (position < input.Length && IsAsciiWhitespace(input[position]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            position++;
        }
        if (position == input.Length || input[position] is not (>= '0' and <= '9')) { return null; }

        var value = 0.0;
        while (position < input.Length && input[position] is >= '0' and <= '9')
        {
            cancellationToken.ThrowIfCancellationRequested();
            value = value * 10 + input[position++] - '0';
            if (!double.IsFinite(value))
            { throw new CssLimitException($"{attributeName} exceeds finite numeric range."); }
        }

        if (position < input.Length && input[position] == '.')
        {
            position++;
            if (position < input.Length && input[position] is >= '0' and <= '9')
            {
                var divisor = 1.0;
                var hasNonzeroFractionDigit = false;
                while (position < input.Length && input[position] is >= '0' and <= '9')
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var digit = input[position++] - '0';
                    hasNonzeroFractionDigit |= digit != 0;
                    divisor *= 10;
                    value += digit / divisor;
                }
                if (value == 0 && hasNonzeroFractionDigit)
                { throw new CssLimitException($"{attributeName} is below finite numeric range."); }
            }
        }

        return new(value, position < input.Length && input[position] == '%' ? "%" : "px");
    }

    private static bool TryParseNonNegativeInteger(string input, CancellationToken cancellationToken, out double value,
        string attribute = "table cellspacing")
    {
        value = 0;
        var position = 0;
        while (position < input.Length && IsAsciiWhitespace(input[position]))
        {
            if ((position & 0xFF) == 0) { cancellationToken.ThrowIfCancellationRequested(); }
            position++;
        }
        if (position == input.Length) { return false; }

        var negative = input[position] == '-';
        if (input[position] is '+' or '-') { position++; }
        var start = position;
        while (position < input.Length && input[position] is >= '0' and <= '9')
        {
            if ((position & 0xFF) == 0) { cancellationToken.ThrowIfCancellationRequested(); }
            var digit = input[position] - '0';
            if (negative && digit > 0) { return false; }
            value = value * 10 + digit;
            if (!double.IsFinite(value)) { throw new CssLimitException($"Computed {attribute} exceeds finite numeric range."); }
            position++;
        }

        if (position == start) { return false; }
        if (negative && value > 0) { return false; }
        if (negative) { value = 0; }
        return true;
    }

    private static bool IsAsciiWhitespace(char value) => value is '\t' or '\n' or '\f' or '\r' or ' ';

    public static CssStyleResult Compute(DomDocument document, IEnumerable<CssStyleSource>? sources = null,
        bool includeUserAgent = true, CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new CssContext(options, cancellationToken);
        var rules = new List<StyleRule>();
        var order = 0;
        var sourceCount = 0;
        var userAgentOrder = -1;
        if (includeUserAgent)
        {
            Compile(new(UserAgentStyleSheet, CssOrigin.UserAgent));
            userAgentOrder = order - 1;
        }
        foreach (var source in sources ?? []) { Compile(source); }
        var bodyLinkColors = GetBodyLinkColorHints(document, context);
        var linkSelector = bodyLinkColors.Count == 0 ? null
            : CssSelectorList.Compile(CssSyntax.Tokens(":link", context), context);
        var styles = new Dictionary<DomElement, CssComputedStyle>();
        var tableCellPaddingHints = new Dictionary<DomElement, CssLength?>();
        var tableBorderHints = new Dictionary<DomElement, TableBorderHint?>();
        var tableAncestors = new Dictionary<DomElement, DomElement?>();
        foreach (var node in document.Descendants())
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (node is not DomElement element) { continue; }
            if (styles.Count >= context.Options.MaxElements) { throw new CssLimitException("CSS styled element limit exceeded."); }
            DomElement? tableAncestor = element.LocalName == "table" ? element : null;
            if (tableAncestor is null && element.ParentNode is DomElement tableParent)
            { tableAncestors.TryGetValue(tableParent, out tableAncestor); }
            if (tableAncestor is not null) { tableAncestors.Add(element, tableAncestor); }
            var candidates = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
            if (element.LocalName == "table")
            { _ = GetTableCellPaddingHint(element, context, tableCellPaddingHints); }
            foreach (var rule in rules)
            {
                if (rule.Selectors.Match(element, context) is not { } specificity) { continue; }
                foreach (var assignment in rule.Assignments) { Add(assignment, rule.Origin, false, specificity, rule.Order); }
            }
            if (linkSelector?.Match(element, context) is not null)
            {
                foreach (var bodyLinkColor in bodyLinkColors)
                {
                    context.Assignment();
                    Add(new("color", bodyLinkColor, false, 0), CssOrigin.Author, false, new(), -1,
                        presentationalHint: true);
                }
            }
            if (TryGetListTypeHint(element, out var marker))
            {
                context.Assignment();
                Add(new("list-style-type", new CssKeyword(marker), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (HasSupportedTableCenterAlignmentHint(element))
            {
                context.Assignment();
                Add(new("margin-left", new CssKeyword("auto"), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
                context.Assignment();
                Add(new("margin-right", new CssKeyword("auto"), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetCellspacingHint(element, context) is { } cellSpacing)
            {
                context.Assignment();
                Add(new("border-spacing", cellSpacing, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetTableWidthHint(element, context) is { } tableWidth)
            {
                context.Assignment();
                Add(new("width", tableWidth, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (element.LocalName == "table" && GetTableBorderHint(element, context, tableBorderHints) is { } tableBorder)
            {
                foreach (var side in new[] { "top", "right", "bottom", "left" })
                {
                    context.Assignment();
                    Add(new($"border-{side}-width", tableBorder.Width, false, 0), CssOrigin.Author, false,
                        new(), -1, presentationalHint: true);
                }
                if (includeUserAgent && tableBorder.HasVisibleBorder)
                {
                    foreach (var side in new[] { "top", "right", "bottom", "left" })
                    {
                        context.Assignment();
                        Add(new($"border-{side}-style", new CssKeyword("outset"), false, 0), CssOrigin.UserAgent,
                            false, new(0, 1, 1), userAgentOrder);
                    }
                }
            }
            if (GetTableCellHeightHint(element, context) is { } cellHeight)
            {
                context.Assignment();
                Add(new("height", cellHeight, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetTableRowHeightHint(element, context) is { } rowHeight)
            {
                context.Assignment();
                Add(new("height", rowHeight, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetTableColumnWidthHint(element, context) is { } columnWidth)
            {
                context.Assignment();
                Add(new("width", columnWidth, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetHorizontalRuleWidthHint(element, context) is { } horizontalRuleWidth)
            {
                context.Assignment();
                Add(new("width", horizontalRuleWidth, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetHorizontalRuleAlignmentHint(element) is { } horizontalRuleAlignment)
            {
                CssValue leftMargin = horizontalRuleAlignment == "left"
                    ? new CssLength(0, "px") : new CssKeyword("auto");
                CssValue rightMargin = horizontalRuleAlignment == "right"
                    ? new CssLength(0, "px") : new CssKeyword("auto");
                context.Assignment();
                Add(new("margin-left", leftMargin, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
                context.Assignment();
                Add(new("margin-right", rightMargin, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            var horizontalRuleColorAttribute = element.LocalName == "hr" ? element.GetAttribute("color") : null;
            var horizontalRuleNoShadeAttribute = element.LocalName == "hr" ? element.GetAttribute("noshade") : null;
            if (element.LocalName == "hr"
                && (horizontalRuleColorAttribute is not null || horizontalRuleNoShadeAttribute is not null))
            {
                foreach (var side in new[] { "top", "right", "bottom", "left" })
                {
                    context.Assignment();
                    Add(new($"border-{side}-style", new CssKeyword("solid"), false, 0), CssOrigin.Author, false,
                        new(), -1, presentationalHint: true);
                }
            }
            if (element.LocalName == "hr" && element.GetAttribute("size") is { } horizontalRuleSizeAttribute)
            {
                context.HorizontalRuleSizeCharacters(horizontalRuleSizeAttribute.Length);
                if (TryParseNonNegativeInteger(horizontalRuleSizeAttribute, context.Cancellation,
                    out var horizontalRuleSize, "horizontal-rule size"))
                {
                    if (horizontalRuleColorAttribute is not null || horizontalRuleNoShadeAttribute is not null)
                    {
                        var borderWidth = new CssLength(horizontalRuleSize / 2, "px");
                        foreach (var side in new[] { "top", "right", "bottom", "left" })
                        {
                            context.Assignment();
                            Add(new($"border-{side}-width", borderWidth, false, 0), CssOrigin.Author, false,
                                new(), -1, presentationalHint: true);
                        }
                    }
                    else if (horizontalRuleSize == 1)
                    {
                        context.Assignment();
                        Add(new("border-bottom-width", new CssLength(0, "px"), false, 0), CssOrigin.Author, false,
                            new(), -1, presentationalHint: true);
                    }
                    else if (horizontalRuleSize > 1)
                    {
                        context.Assignment();
                        Add(new("height", new CssLength(horizontalRuleSize - 2, "px"), false, 0),
                            CssOrigin.Author, false, new(), -1, presentationalHint: true);
                    }
                }
            }
            if (horizontalRuleColorAttribute is not null)
            {
                context.HorizontalRuleColorCharacters(horizontalRuleColorAttribute.Length);
                if (HtmlLegacyColor.Parse(horizontalRuleColorAttribute, context.Cancellation) is { } horizontalRuleColor)
                {
                    context.Assignment();
                    Add(new("color", horizontalRuleColor, false, 0), CssOrigin.Author, false, new(), -1,
                        presentationalHint: true);
                }
            }
            if (GetTableCellWidthHint(element, context) is { } cellWidth)
            {
                context.Assignment();
                Add(new("width", cellWidth, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            var borderRuleSpecificity = default(CssSpecificity);
            var borderRuleTable = includeUserAgent
                ? GetTableBorderCellRuleTable(element, out borderRuleSpecificity)
                : null;
            if (borderRuleTable is not null
                && GetTableBorderHint(borderRuleTable, context, tableBorderHints) is { HasVisibleBorder: true })
            {
                foreach (var side in new[] { "top", "right", "bottom", "left" })
                {
                    context.Assignment();
                    Add(new($"border-{side}-width", new CssLength(1, "px"), false, 0), CssOrigin.UserAgent, false,
                        borderRuleSpecificity, userAgentOrder);
                    context.Assignment();
                    Add(new($"border-{side}-style", new CssKeyword("inset"), false, 0), CssOrigin.UserAgent, false,
                        borderRuleSpecificity, userAgentOrder);
                }
            }
            if (element.LocalName is ("td" or "th")
                && tableAncestor is { } table
                && GetTableCellPaddingHint(table, context, tableCellPaddingHints) is { } cellPadding)
            {
                context.Assignment();
                Add(new("padding-top", cellPadding, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
                context.Assignment();
                Add(new("padding-right", cellPadding, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
                context.Assignment();
                Add(new("padding-bottom", cellPadding, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
                context.Assignment();
                Add(new("padding-left", cellPadding, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (HasSupportedTableCellNowrapHint(element))
            {
                context.Assignment();
                Add(new("white-space", new CssKeyword("nowrap"), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetAlignHint(element) is { } align)
            {
                context.Assignment();
                Add(new("text-align", new CssKeyword(align), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            var verticalAlign = GetValignHint(element);
            if (verticalAlign is null && element.LocalName is ("td" or "th"))
            { verticalAlign = GetTableAncestorValignHint(element); }
            if (verticalAlign is not null)
            {
                context.Assignment();
                Add(new("vertical-align", new CssKeyword(verticalAlign), false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetBodyBackgroundColorHint(element, context) is { } bodyBackgroundColor)
            {
                context.Assignment();
                Add(new("background-color", bodyBackgroundColor, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetTableBackgroundColorHint(element, context) is { } tableBackgroundColor)
            {
                context.Assignment();
                Add(new("background-color", tableBackgroundColor, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (GetBodyTextColorHint(element, context) is { } bodyTextColor)
            {
                context.Assignment();
                Add(new("color", bodyTextColor, false, 0), CssOrigin.Author, false, new(), -1,
                    presentationalHint: true);
            }
            if (element.LocalName == "font" && element.GetAttribute("color") is { } colorAttribute)
            {
                context.FontColorCharacters(colorAttribute.Length);
                if (HtmlLegacyColor.Parse(colorAttribute, context.Cancellation) is { } color)
                {
                    context.Assignment();
                    Add(new("color", color, false, 0), CssOrigin.Author, false, new(), -1,
                        presentationalHint: true);
                }
            }
            if (element.LocalName == "font" && element.GetAttribute("size") is { } sizeAttribute)
            {
                context.FontSizeCharacters(sizeAttribute.Length);
                if (HtmlLegacyFontSize.Parse(sizeAttribute, context.Cancellation) is { } size)
                {
                    context.Assignment();
                    Add(new("font-size", size, false, 0), CssOrigin.Author, false, new(), -1,
                        presentationalHint: true);
                }
            }
            if (element.LocalName == "font" && element.GetAttribute("face") is { } faceAttribute)
            {
                context.FontFaceCharacters(faceAttribute.Length);
                if (HtmlLegacyFontFace.Parse(faceAttribute, context.Options, context.Cancellation) is { } families)
                {
                    context.Assignment();
                    Add(new("font-family", families, false, 0), CssOrigin.Author, false, new(), -1,
                        presentationalHint: true);
                }
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
                        "%" when name == "text-decoration-thickness" => new CssLength(length.Value / 100 * fontSize, "px"),
                        _ => length
                    };
                    if (!double.IsFinite(resolved.Value)) { throw new CssLimitException("Computed CSS length exceeds finite numeric range."); }
                    computed[name] = resolved;
                }
                else if (computed[name] is CssBorderSpacing spacing)
                {
                    computed[name] = new CssBorderSpacing(
                        ResolveBorderSpacingLength(spacing.Horizontal, fontSize, rootSize),
                        ResolveBorderSpacingLength(spacing.Vertical, fontSize, rootSize));
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

            void Add(CssAssignment assignment, CssOrigin origin, bool isInline, CssSpecificity specificity, int sourceOrder,
                bool presentationalHint = false)
            {
                if (!candidates.TryGetValue(assignment.Name, out var list)) { candidates[assignment.Name] = list = []; }
                list.Add(new(assignment.Value, origin, assignment.Important, isInline, specificity, sourceOrder, list.Count,
                    presentationalHint));
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
            var syntaxRuleCount = sheet.Rules.Count;
            CompileRules(sheet.Rules, 0);

            void CompileRules(IReadOnlyList<CssRule> sourceRules, int depth)
            {
                if (depth > context.Options.MaxDepth) { throw new CssLimitException("CSS conditional rule depth limit exceeded."); }
                foreach (var rule in sourceRules)
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    if (rule.AtName is not null)
                    {
                        if (CssText.Lower(rule.AtName) != "media")
                        {
                            context.Error("unsupported-at-rule", rule.Offset, $"@{rule.AtName} is not evaluated in the static subset.");
                            continue;
                        }
                        if (rule.Block is null || !CssMediaTypeList.TryMatchesScreen(rule.Prelude, out var matches,
                            context.Options, cancellationToken))
                        {
                            context.Error("unsupported-at-rule", rule.Offset,
                                "@media requires a block and an empty or all/screen/print media-type list.");
                            continue;
                        }
                        if (matches)
                        {
                            var nested = CssSyntax.Rules(rule.Block, context, topLevel: false, ref syntaxRuleCount);
                            CompileRules(nested, depth + 1);
                        }
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
        }

        void Import(IEnumerable<CssDiagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics) { context.Error(diagnostic.Code, diagnostic.Offset, diagnostic.Message); }
        }
    }

    private static CssLength ResolveBorderSpacingLength(CssLength length, double fontSize, double rootSize)
    {
        var resolved = length.Unit switch
        {
            "em" => new CssLength(length.Value * fontSize, "px"),
            "rem" => new CssLength(length.Value * rootSize, "px"),
            _ => length
        };
        if (!double.IsFinite(resolved.Value)) { throw new CssLimitException("Computed CSS length exceeds finite numeric range."); }
        return resolved;
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
        CssSpecificity Specificity, int Order, int DeclarationOrder, bool PresentationalHint)
    {
        // Hints rank below author rules but remain in the author origin for revert.
        internal int Rank => Important ? Origin switch { CssOrigin.UserAgent => 6, CssOrigin.User => 5, _ => 4 }
            : PresentationalHint ? 2 : Origin switch { CssOrigin.UserAgent => 0, CssOrigin.User => 1, _ => 3 };
    }
}
