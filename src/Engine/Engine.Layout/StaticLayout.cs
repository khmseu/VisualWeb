using System.Diagnostics.CodeAnalysis;
using System.Text;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Text;

namespace VisualWeb.Engine.Layout;

[SuppressMessage("Sonar", "S2166", Justification = "These public exception types intentionally expose only the failure-category constructor used by the bounded layout API.")]
public sealed class UnsupportedLayoutException(string message) : Exception(message);
[SuppressMessage("Sonar", "S2166", Justification = "These public exception types intentionally expose only the failure-category constructor used by the bounded layout API.")]
public sealed class LayoutLimitException(string message) : Exception(message);

/// <summary>CSS-pixel geometry; no device-pixel snapping or rasterization.</summary>
/// <remarks>Spec: css-box; <see href="https://www.w3.org/TR/css-box-3/#box-model">box model</see>.</remarks>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height);
public readonly record struct LayoutEdges(double Top, double Right, double Bottom, double Left);
public sealed record LayoutTextFragment(DomText Source, ShapedRun Run, double X, double Baseline, CssComputedStyle Style);
/// <summary>Shaped outside list marker, not selectable document text.</summary>
/// <remarks>Specs: css2-lists and css-counter-styles;
/// <see href="https://www.w3.org/TR/CSS22/generate.html#lists">lists</see> and
/// <see href="https://www.w3.org/TR/css-counter-styles-3/#simple-alphabetic">alphabetic styles</see> and
/// <see href="https://www.w3.org/TR/css-counter-styles-3/#simple-numeric">Roman styles</see>.</remarks>
public sealed record LayoutTextMarker(ShapedRun Run, double X, double Baseline);
public sealed record LayoutInlineWidget(DomElement Element, LayoutRect Bounds);
public sealed record LayoutLine(LayoutRect Bounds, double Baseline, IReadOnlyList<LayoutTextFragment> Fragments)
{
    public IReadOnlyList<LayoutInlineWidget> Widgets { get; init; } = Array.Empty<LayoutInlineWidget>();
}
[SuppressMessage("Sonar", "S2094", Justification = "This intentional marker record is the closed layout-flow contract that distinguishes block and line items without mutable state.")]
public abstract record LayoutFlowItem;
public sealed record LayoutBlockItem(LayoutBox Box) : LayoutFlowItem;
public sealed record LayoutLineItem(LayoutLine Line) : LayoutFlowItem;
public sealed record LayoutBox(DomElement Element, LayoutRect Content, LayoutRect PaddingBox,
    LayoutRect BorderBox, LayoutEdges Margin, IReadOnlyList<LayoutBox> Children, IReadOnlyList<LayoutLine> Lines)
{
    public IReadOnlyList<LayoutFlowItem> Flow { get; init; } = Array.Empty<LayoutFlowItem>();
    /// <summary>Outside disc-marker bounds, separate from selectable document text.</summary>
    /// <remarks>Spec: css2-lists; <see href="https://www.w3.org/TR/CSS22/generate.html#lists">lists</see>.
    /// The finite LTR subset uses a 0.3em disc and a 0.5em gap before the content edge.</remarks>
    public LayoutRect? DiscMarker { get; init; }
    /// <summary>Outside hollow circle-marker bounds, separate from selectable document text.</summary>
    /// <remarks>Spec: css2-lists; <see href="https://www.w3.org/TR/CSS22/generate.html#list-style">list style</see>.
    /// The finite LTR subset uses a 0.3em diameter and a 0.5em gap before the content edge.</remarks>
    public LayoutRect? CircleMarker { get; init; }
    /// <summary>Outside square-marker bounds, separate from selectable document text.</summary>
    /// <remarks>Spec: css2-lists; <see href="https://www.w3.org/TR/CSS22/generate.html#list-style">list style</see>.
    /// The finite LTR subset uses a 0.3em square and a 0.5em gap before the content edge.</remarks>
    public LayoutRect? SquareMarker { get; init; }
    public LayoutTextMarker? TextMarker { get; init; }
}
public sealed record LayoutResult(LayoutBox? Root, double ViewportWidth, double ViewportHeight);

public sealed record LayoutOptions
{
    public int MaxDepth { get; init; } = 128;
    public int MaxBoxes { get; init; } = 100_000;
    public int MaxNodes { get; init; } = 1_000_000;
    public int MaxLines { get; init; } = 100_000;
    public int MaxTextCharacters { get; init; } = 1_000_000;
    public int MaxShapedGlyphs { get; init; } = 1_000_000;
    public int MaxShapingCalls { get; init; } = 100_000;
    public long MaxLineBreakOperations { get; init; } = 10_000_000;
    internal void Validate()
    {
        if (MaxDepth <= 0 || MaxBoxes <= 0 || MaxNodes <= 0 || MaxLines <= 0 || MaxTextCharacters <= 0
            || MaxShapedGlyphs <= 0 || MaxShapingCalls <= 0 || MaxLineBreakOperations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayoutOptions), "All layout limits must be positive.");
        }
    }
}

/// <summary>Finite horizontal LTR block/inline layout returning renderer-local geometry snapshots.</summary>
/// <remarks>Specs: css2-visual/css2-sizing;
/// <see href="https://www.w3.org/TR/CSS22/visuren.html#block-formatting">block formatting</see>,
/// <see href="https://www.w3.org/TR/CSS22/visuren.html#inline-formatting">inline formatting</see>.
/// Basic automatic tables follow local ID css2-tables,
/// <see href="https://www.w3.org/TR/CSS22/tables.html#auto-table-layout">automatic table layout</see>,
/// with canonical HTML boxes, separate borders and top-aligned cells only.
/// Internal ASCII-letter hyphens use css-text
/// <see href="https://drafts.csswg.org/css-text-3/#line-breaking">line breaking</see> and unicode-linebreak
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB20a">hyphen rules</see>.
/// Internal ASCII-letter solidus uses the css-text
/// <see href="https://drafts.csswg.org/css-text-3/#line-break-property">interoperable no-break guidance</see>.
/// ASCII infix separators between ASCII letters/digits follow unicode-linebreak
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB15d">LB15d</see>,
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB25">LB25</see> and
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB29">LB29</see>.
/// Internal ASCII @, _ and + between ASCII letters/digits follow unicode-linebreak
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB23">LB23</see>,
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB24">LB24</see>,
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB25">LB25</see> and
/// <see href="https://www.unicode.org/reports/tr14/tr14-55.html#LB28">LB28</see>.
/// These finite cases do not imply general email, URL or Unicode line breaking.
/// Bounded inter-word justification uses css-text
/// <see href="https://drafts.csswg.org/css-text-3/#text-align-property">text alignment</see> and
/// <see href="https://drafts.csswg.org/css-text-3/#text-justify-property">justification methods</see>.
/// Supported legacy list type hints follow html
/// <see href="https://html.spec.whatwg.org/multipage/rendering.html#lists">list rendering</see>.
/// Menu lists follow html <see href="https://html.spec.whatwg.org/multipage/grouping-content.html#the-menu-element">menu</see>.
/// Margin collapse, bidi, floats, non-form replaced elements and complex line breaking fail explicitly.</remarks>
public static partial class StaticLayout
{
    public static LayoutResult Layout(DomDocument document, CssStyleResult styles, ITextShaper shaper,
        double viewportWidth, double viewportHeight, LayoutOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(shaper);
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0) { throw new ArgumentOutOfRangeException(nameof(viewportWidth)); }
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0) { throw new ArgumentOutOfRangeException(nameof(viewportHeight)); }
        var builder = new Builder(styles, shaper, options ?? new(), cancellationToken);
        return new(document.DocumentElement is { } root ? builder.Root(root, viewportWidth, viewportHeight) : null,
            viewportWidth, viewportHeight);
    }

    private sealed partial class Builder
    {
        private static readonly (int Value, string Symbol)[] RomanSymbols =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"),
            (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"),
            (5, "V"), (4, "IV"), (1, "I")
        ];
        private readonly CssStyleResult styles;
        private readonly ITextShaper shaper;
        private readonly LayoutOptions options;
        private readonly CancellationToken cancellation;
        private int nodes, boxes, lines, characters, glyphs, calls;
        private long lineBreakOperations;
        private readonly Dictionary<DomElement, long> ordinals = new(ReferenceEqualityComparer.Instance);

        internal Builder(CssStyleResult styles, ITextShaper shaper, LayoutOptions options, CancellationToken cancellation)
        {
            options.Validate();
            cancellation.ThrowIfCancellationRequested();
            if (styles.Diagnostics.Count > 0)
            {
                throw new UnsupportedLayoutException("Resolve CSS diagnostics before layout; unsupported declarations cannot be rendered approximately.");
            }
            this.styles = styles;
            this.shaper = shaper;
            this.options = options;
            this.cancellation = cancellation;
        }

        internal LayoutBox? Root(DomElement root, double width, double height)
        {
            if (Display(root) == "none") { return null; }
            if (Display(root) != "block") { throw new UnsupportedLayoutException("The initial root element must use display:block."); }
            return Block(root, 0, 0, width, height, 1, isRoot: true);
        }

        private CssComputedStyle Style(DomElement element) => styles.Styles.TryGetValue(element, out var style) ? style
            : throw new InvalidOperationException("Styles are incomplete or stale; recompute for the current DOM.");
        private string Display(DomElement element) => ((CssKeyword)Style(element)["display"]).Value;
        private bool IsBlock(DomElement element) => Display(element) is "block" or "list-item" or "table";
        [SuppressMessage("Sonar", "S1192:String literals should not be duplicated", Justification = "The repeated explicit element and input-kind literals define the finite supported layout grammar at its rejection boundary.")]
        private void Check(DomElement element, int depth, bool block = false, bool tableContext = false)
        {
            cancellation.ThrowIfCancellationRequested();
            if (depth > options.MaxDepth) { throw new LayoutLimitException("Layout nesting limit exceeded."); }
            if (Display(element).StartsWith("table-", StringComparison.Ordinal) && !tableContext)
            { throw new UnsupportedLayoutException("Internal table boxes require the canonical table layout context."); }
            if (Style(element)["vertical-align"] is CssKeyword { Value: not "baseline" }
                && !(tableContext && Display(element) == "table-cell"))
            { throw new UnsupportedLayoutException("Non-baseline vertical alignment is supported only on table cells."); }
            if (element.GetAttribute("dir") is { } dir && !dir.Equals("ltr", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnsupportedLayoutException("dir=rtl/auto requires bidi layout.");
            }
            if (element.GetAttribute("lang") is { Length: > 0 } language && !language.Equals("en", StringComparison.OrdinalIgnoreCase)
                && !language.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnsupportedLayoutException("Language-dependent shaping/segment-break handling beyond English is deferred.");
            }
            if (element.LocalName is "input" or "button" or "textarea" or "select")
            {
                if (!block && Display(element) != "inline-block")
                { throw new UnsupportedLayoutException("Form controls require block or supported inline-block layout."); }
                if (element.LocalName == "input" && DomFormControls.InputType(element) is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "datetime-local" or "month" or "week" or "number" or "range" or "color" or "checkbox" or "radio" or "submit" or "reset" or "button"))
                {
                    throw new UnsupportedLayoutException($"<input type={DomFormControls.InputType(element)}> requires unsupported widget layout.");
                }

                return;
            }
            if (Display(element) == "list-item"
                && (element.LocalName != "li" || element.ParentNode is not DomElement { LocalName: "ul" or "ol" or "menu" }))
            {
                throw new UnsupportedLayoutException("List-item markers require a direct ul, ol or menu parent.");
            }
            if (element.LocalName is "ul" or "ol" or "menu" or "li"
                && (!block || element.LocalName != "menu" && element.GetAttribute("type") is not null
                    && !CssStyleEngine.TryGetListTypeHint(element, out _)))
            {
                throw new UnsupportedLayoutException("List elements require block layout and, where applicable, a supported legacy type attribute.");
            }
            if (element.LocalName is "bdi" or "bdo" or "img" or "select"
                or "video" or "audio" or "canvas" or "iframe" or "object" or "embed"
                or "ruby" || element.LocalName == "table" && (!block || Display(element) != "table")
                || Display(element) == "table" && element.LocalName != "table")
            {
                throw new UnsupportedLayoutException($"<{element.LocalName}> requires unsupported specialized layout.");
            }
        }

        [SuppressMessage("Sonar", "S1192:String literals should not be duplicated", Justification = "The repeated CSS box-model vocabulary is intentionally local to the bounded block-formatting calculation.")]
        private LayoutBox Block(DomElement element, double containingX, double top, double containingWidth,
            double? containingHeight, int depth, bool isRoot = false, bool suppressTopMargin = false,
            bool suppressBottomMargin = false, double? cellWidth = null)
        {
            Check(element, depth, block: true, tableContext: cellWidth is not null);
            Visit();
            if (++boxes > options.MaxBoxes) { throw new LayoutLimitException("Layout box limit exceeded."); }
            var style = Style(element);
            if (element.LocalName is "ul" or "ol" or "menu") { PrepareList(element); }
            var table = Display(element) == "table" ? PlanTable(element, containingWidth, depth) : null;
            var padding = Edges(style, "padding", containingWidth);
            var border = Edges(style, "border", containingWidth, "-width");
            var margin = Edges(style, "margin", containingWidth);
            var horizontal = padding.Left + padding.Right + border.Left + border.Right;
            var sizing = ((CssKeyword)style["box-sizing"]).Value;
            var width = cellWidth ?? table?.Width ?? ContentWidth(style, containingWidth);
            var leftAuto = style["margin-left"] is CssKeyword { Value: "auto" };
            var rightAuto = style["margin-right"] is CssKeyword { Value: "auto" };
            var extra = containingWidth - width - horizontal - margin.Left - margin.Right;
            if (extra >= 0)
            {
                if (leftAuto && rightAuto) { margin = margin with { Left = extra / 2, Right = extra / 2 }; }
                else if (leftAuto) { margin = margin with { Left = extra }; }
                else { margin = margin with { Right = margin.Right + extra }; }
            }
            else { margin = margin with { Right = margin.Right + extra }; }
            var borderX = containingX + margin.Left;
            var contentX = borderX + border.Left + padding.Left;
            var vertical = padding.Top + padding.Bottom + border.Top + border.Bottom;
            var specifiedHeight = Dimension(style["height"], containingHeight);
            var definiteHeight = specifiedHeight is { } h ? Math.Max(0, h - (sizing == "border-box" ? vertical : 0)) : (double?)null;
            if (element.LocalName == "textarea" && definiteHeight is null)
            { definiteHeight = TextareaDimension(element, "rows", 2, 64) * 20; }
            var minHeight = Dimension(style["min-height"], containingHeight) ?? 0;
            var maxHeight = Dimension(style["max-height"], containingHeight) ?? double.PositiveInfinity;
            if (sizing == "border-box")
            {
                minHeight = Math.Max(0, minHeight - vertical);
                maxHeight = double.IsPositiveInfinity(maxHeight) ? maxHeight : Math.Max(0, maxHeight - vertical);
            }
            if (definiteHeight is { } definite) { definiteHeight = Math.Max(minHeight, Math.Min(definite, maxHeight)); }
            var collapseTopChild = !isRoot && cellWidth is null && padding.Top == 0 && border.Top == 0
                ? EdgeBlockChild(element, first: true) : null;
            var collapseBottomChild = cellWidth is null && padding.Bottom == 0 && border.Bottom == 0
                && definiteHeight is null && minHeight == 0 ? EdgeBlockChild(element, first: false) : null;
            if (suppressTopMargin) { margin = margin with { Top = 0 }; }
            else if (collapseTopChild is not null)
            {
                margin = margin with { Top = CollapseMargins(margin.Top, TopMarginChain(collapseTopChild, width)) };
            }
            if (suppressBottomMargin) { margin = margin with { Bottom = 0 }; }
            else if (collapseBottomChild is not null)
            {
                margin = margin with { Bottom = CollapseMargins(margin.Bottom, BottomMarginChain(collapseBottomChild, width)) };
            }
            var borderY = top + margin.Top;
            var contentY = borderY + border.Top + padding.Top;
            var childBoxes = new List<LayoutBox>();
            var blockLines = new List<LayoutLine>();
            var flow = new List<LayoutFlowItem>();
            var inline = new List<InlineUnit>();
            var cursor = contentY;
            double? previousBlockBottomMargin = null;
            if (element.LocalName == "input")
            {
                var (ascent, descent) = Extents(Shape("", style), style);
                cursor += ascent + descent;
            }
            if (table is not null)
            {
                var arranged = ArrangeTable(table, contentX, contentY, depth);
                childBoxes.AddRange(arranged.Groups);
                flow.AddRange(arranged.Groups.Select(group => new LayoutBlockItem(group)));
                cursor += arranged.Height;
            }
            foreach (var child in table is not null || element.LocalName is "input" or "textarea" or "select" ? [] : element.ChildNodes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (child is DomElement e && Display(e) == "none") { continue; }
                if (child is DomElement block && IsBlock(block))
                {
                    Flush();
                    var childTopMargin = Edges(Style(block), "margin", width).Top;
                    var childTop = previousBlockBottomMargin is { } previousBottomMargin
                        ? cursor + CollapseMargins(previousBottomMargin, childTopMargin) - previousBottomMargin - childTopMargin
                        : cursor;
                    var childBox = Block(block, contentX, childTop, width, definiteHeight, depth + 1,
                        suppressTopMargin: block == collapseTopChild, suppressBottomMargin: block == collapseBottomChild);
                    childBoxes.Add(childBox);
                    flow.Add(new LayoutBlockItem(childBox));
                    cursor = childBox.BorderBox.Y + childBox.BorderBox.Height + childBox.Margin.Bottom;
                    previousBlockBottomMargin = childBox.Margin.Bottom;
                }
                else { Gather(child, style, inline, depth + 1); }
            }
            Flush();
            LayoutRect? discMarker = null;
            LayoutRect? circleMarker = null;
            LayoutRect? squareMarker = null;
            LayoutTextMarker? textMarker = null;
            if (Display(element) == "list-item" && style["list-style-type"] is CssKeyword { Value: not "none" } markerType)
            {
                var size = ((CssLength)style["font-size"]).Value;
                var isTextMarker = markerType.Value is "decimal" or "decimal-leading-zero" or "lower-alpha" or "upper-alpha"
                    or "lower-latin" or "upper-latin" or "lower-roman" or "upper-roman";
                var text = isTextMarker ? FormatListMarker(ordinals[element], markerType.Value) : "";
                ChargeCharacters(text.Length);
                var run = Shape(text, style);
                var (ascent, descent) = Extents(run, style);
                var baseline = FirstBaseline(flow) ?? contentY + ascent;
                if (flow.Count == 0) { cursor += ascent + descent; }
                if (markerType.Value is "disc" or "circle" or "square")
                {
                    var bounds = Rect(contentX - 0.8 * size, baseline - 0.5 * size, 0.3 * size, 0.3 * size);
                    if (markerType.Value == "disc") { discMarker = bounds; }
                    else if (markerType.Value == "circle") { circleMarker = bounds; }
                    else { squareMarker = bounds; }
                }
                else if (isTextMarker)
                {
                    textMarker = new(run, Finite(contentX - 0.5 * size - run.Width), baseline);
                }
                else { throw new UnsupportedLayoutException($"Unsupported list marker '{markerType.Value}'."); }
            }
            var autoHeight = cursor - contentY;
            var contentHeight = cellWidth is not null ? Math.Max(definiteHeight ?? 0, autoHeight) : definiteHeight ?? autoHeight;
            contentHeight = Math.Max(minHeight, Math.Min(contentHeight, maxHeight));
            var content = Rect(contentX, contentY, width, contentHeight);
            var paddingBox = Rect(contentX - padding.Left, contentY - padding.Top,
                width + padding.Left + padding.Right, contentHeight + padding.Top + padding.Bottom);
            var borderBox = Rect(borderX, borderY, width + horizontal, contentHeight + vertical);
            return new(element, content, paddingBox, borderBox, margin, childBoxes.AsReadOnly(), blockLines.AsReadOnly())
            {
                Flow = flow.AsReadOnly(),
                DiscMarker = discMarker,
                CircleMarker = circleMarker,
                SquareMarker = squareMarker,
                TextMarker = textMarker
            };

            void Flush()
            {
                if (inline.Count == 0) { return; }
                var formatted = Inline(inline, style, contentX, cursor, width);
                blockLines.AddRange(formatted);
                flow.AddRange(formatted.Select(line => new LayoutLineItem(line)));
                if (formatted.Count > 0)
                {
                    cursor = formatted[^1].Bounds.Y + formatted[^1].Bounds.Height;
                    previousBlockBottomMargin = null;
                }

                inline.Clear();
            }
        }

        private static string FormatListMarker(long ordinal, string markerType)
        {
            var roman = markerType is "lower-roman" or "upper-roman";
            if (markerType is "decimal" or "decimal-leading-zero" || ordinal <= 0 || roman && ordinal > 3999)
            {
                var number = ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return markerType == "decimal-leading-zero" && ordinal is >= 0 and <= 9
                    ? "0" + number + "." : number + ".";
            }

            if (roman)
            {
                // The longest value in the supported Roman range is 15 letters, plus the period.
                Span<char> result = stackalloc char[16];
                var length = 0;
                var lower = markerType == "lower-roman";
                foreach (var (value, symbol) in RomanSymbols)
                {
                    while (ordinal >= value)
                    {
                        foreach (var character in symbol)
                        {
                            result[length++] = lower ? char.ToLowerInvariant(character) : character;
                        }
                        ordinal -= value;
                    }
                }
                result[length++] = '.';
                return new string(result[..length]);
            }

            // An Int64 needs at most 14 bijective base-26 letters plus the period.
            Span<char> buffer = stackalloc char[15];
            var index = buffer.Length;
            buffer[--index] = '.';
            var first = markerType is "lower-alpha" or "lower-latin" ? 'a' : 'A';
            while (ordinal > 0)
            {
                ordinal--;
                buffer[--index] = (char)(first + ordinal % 26);
                ordinal /= 26;
            }
            return new string(buffer[index..]);
        }

        private void PrepareList(DomElement list)
        {
            var items = new List<DomElement>();
            foreach (var child in list.ChildNodes)
            {
                Visit();
                if (child is DomElement { LocalName: "li" } item && Display(item) == "list-item")
                { items.Add(item); }
            }
            var reversed = list.LocalName == "ol" && list.GetAttribute("reversed") is not null;
            var numbering = list.LocalName == "ol" && ParseListInteger(list.GetAttribute("start")) is { } start
                ? start : reversed ? items.Count : 1L;
            foreach (var item in items)
            {
                cancellation.ThrowIfCancellationRequested();
                if (ParseListInteger(item.GetAttribute("value")) is { } value) { numbering = value; }
                ordinals.Add(item, numbering);
                numbering += reversed ? -1 : 1;
            }
        }

        // HTML integer parsing accepts a sign and a leading digit run, not BCL whole-string parsing.
        private long? ParseListInteger(string? raw)
        {
            if (raw is null) { return null; }
            ChargeCharacters(raw.Length);
            var index = 0;
            while (index < raw.Length && raw[index] is ' ' or '\t' or '\n' or '\f' or '\r')
            { cancellation.ThrowIfCancellationRequested(); index++; }
            var negative = index < raw.Length && raw[index] == '-';
            if (index < raw.Length && raw[index] is '+' or '-') { index++; }
            if (index == raw.Length || !char.IsAsciiDigit(raw[index])) { return null; }
            var maximum = negative ? 2147483648L : int.MaxValue;
            var value = 0L;
            while (index < raw.Length && char.IsAsciiDigit(raw[index]))
            {
                cancellation.ThrowIfCancellationRequested();
                value = value * 10 + raw[index++] - '0';
                if (value > maximum) { throw new LayoutLimitException("List numbering attributes exceed the supported signed 32-bit range."); }
            }
            return negative ? -value : value;
        }

        private void ChargeCharacters(int count)
        {
            if (count > options.MaxTextCharacters - characters) { throw new LayoutLimitException("Layout text limit exceeded."); }
            characters += count;
        }

        private double? FirstBaseline(IReadOnlyList<LayoutFlowItem> flow)
        {
            var pending = new Stack<LayoutFlowItem>(flow.Reverse());
            while (pending.TryPop(out var item))
            {
                cancellation.ThrowIfCancellationRequested();
                if (item is LayoutLineItem line) { return line.Line.Baseline; }
                if (item is LayoutBlockItem block)
                {
                    foreach (var child in block.Box.Flow.Reverse()) { pending.Push(child); }
                }
            }
            return null;
        }

        private DomElement? EdgeBlockChild(DomElement parent, bool first)
        {
            var children = first ? parent.ChildNodes : parent.ChildNodes.Reverse();
            foreach (var node in children)
            {
                cancellation.ThrowIfCancellationRequested();
                if (node is DomComment || node is DomText text && IsCollapsibleWhitespace(text.Data)) { continue; }
                if (node is not DomElement element) { return null; }
                if (Display(element) == "none") { continue; }
                return IsBlock(element) ? element : null;
            }
            return null;
        }

        private double TopMarginChain(DomElement element, double containingWidth)
        {
            var margins = new List<double>();
            var current = element;
            var currentWidth = containingWidth;
            for (var depth = 0; ; depth++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (depth >= options.MaxDepth) { throw new LayoutLimitException("Top margin collapse depth limit exceeded."); }
                var style = Style(current);
                margins.Add(Edges(style, "margin", currentWidth).Top);
                var padding = Edges(style, "padding", currentWidth);
                var border = Edges(style, "border", currentWidth, "-width");
                if (padding.Top != 0 || border.Top != 0 || EdgeBlockChild(current, first: true) is not { } child) { break; }
                currentWidth = ContentWidth(style, currentWidth);
                current = child;
            }
            return CollapseMargins(margins);
        }

        private double BottomMarginChain(DomElement element, double containingWidth)
        {
            var margins = new List<double>();
            var current = element;
            var currentWidth = containingWidth;
            for (var depth = 0; ; depth++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (depth >= options.MaxDepth) { throw new LayoutLimitException("Bottom margin collapse depth limit exceeded."); }
                var style = Style(current);
                margins.Add(Edges(style, "margin", currentWidth).Bottom);
                if (style["height"] is not CssKeyword { Value: "auto" }
                    || style["min-height"] is not CssLength { Value: 0, Unit: "px" }) { break; }
                var padding = Edges(style, "padding", currentWidth);
                var border = Edges(style, "border", currentWidth, "-width");
                if (padding.Bottom != 0 || border.Bottom != 0 || EdgeBlockChild(current, first: false) is not { } child) { break; }
                currentWidth = ContentWidth(style, currentWidth);
                current = child;
            }
            return CollapseMargins(margins);
        }

        private static bool IsCollapsibleWhitespace(string value) => value.All(character =>
            character is ' ' or '\t' or '\n' or '\r' or '\f');

        private static double CollapseMargins(double first, double second)
            => Math.Max(0, Math.Max(first, second)) + Math.Min(0, Math.Min(first, second));

        private static double CollapseMargins(IReadOnlyList<double> margins)
            => Math.Max(0, margins.Max()) + Math.Min(0, margins.Min());

        [SuppressMessage("Sonar", "S1192:String literals should not be duplicated", Justification = "The explicit inline-layout element and whitespace vocabulary keeps the finite supported grammar visible at this traversal boundary.")]
        private void Gather(DomNode node, CssComputedStyle inheritedStyle, List<InlineUnit> output, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            Visit();
            if (depth > options.MaxDepth) { throw new LayoutLimitException("Inline nesting limit exceeded."); }
            if (node is DomText text)
            {
                ChargeCharacters(text.Data.Length);
                var whitespace = ((CssKeyword)inheritedStyle["white-space"]).Value;
                if (whitespace is not ("normal" or "nowrap" or "pre" or "pre-line"))
                {
                    throw new UnsupportedLayoutException($"white-space:{whitespace} is outside the initial layout subset.");
                }
                var canWrap = whitespace is not ("pre" or "nowrap");
                var word = new StringBuilder();
                foreach (var c in text.Data.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (c is ' ' or '\n' or '\t' or '\f')
                    {
                        EmitWord();
                        if (c == '\n' && whitespace is "pre" or "pre-line")
                        {
                            output.Add(new(text, inheritedStyle, "", UnitKind.Break, false));
                        }
                        else if (whitespace == "pre")
                        {
                            if (c != ' ') { throw new UnsupportedLayoutException("Preserved tabs/form feeds require deferred tab-stop handling."); }
                            output.Add(new(text, inheritedStyle, " ", UnitKind.PreservedSpace, false));
                        }
                        else { output.Add(new(text, inheritedStyle, " ", UnitKind.Space, whitespace != "nowrap")); }
                    }
                    else { word.Append(c); }
                }
                EmitWord();
                void EmitWord()
                {
                    if (word.Length == 0) { return; }
                    var value = word.ToString();
                    TextFont.ValidateLatin(value, cancellation);
                    var hasHyphenBreaks = false;
                    if (canWrap)
                    {
                        for (var index = 0; index < value.Length; index++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            var c = value[index];
                            if (c == '/')
                            {
                                if (index == 0 || index == value.Length - 1
                                    || !char.IsAsciiLetter(value[index - 1]) || !char.IsAsciiLetter(value[index + 1]))
                                {
                                    throw new UnsupportedLayoutException("Only internal solidus between ASCII letters is supported in wrapping text.");
                                }
                                continue;
                            }
                            if (c is '\\' or >= '\u2010' and <= '\u2015')
                            {
                                throw new UnsupportedLayoutException("Punctuation line-break opportunities are deferred.");
                            }
                            if (c == '-')
                            {
                                if (index == 0 || index == value.Length - 1
                                    || !char.IsAsciiLetter(value[index - 1]) || !char.IsAsciiLetter(value[index + 1]))
                                {
                                    throw new UnsupportedLayoutException("Only internal hyphens between ASCII letters support line breaking.");
                                }
                                hasHyphenBreaks = true;
                                continue;
                            }
                            if (index > 0 && index < value.Length - 1 && !char.IsLetterOrDigit(c)
                                && char.GetUnicodeCategory(c) is not System.Globalization.UnicodeCategory.NonSpacingMark
                                && c is not ('\'' or '\u2019' or '\u00A0')
                                && !(c is '.' or ',' or ':' or ';' or '@' or '_' or '+'
                                    && char.IsAsciiLetterOrDigit(value[index - 1]) && char.IsAsciiLetterOrDigit(value[index + 1])))
                            {
                                throw new UnsupportedLayoutException("Internal punctuation needs deferred Unicode line-break analysis.");
                            }
                        }
                    }
                    output.Add(new(text, inheritedStyle, value, UnitKind.Word, canWrap) { HasHyphenBreaks = hasHyphenBreaks });
                    word.Clear();
                }
            }
            else if (node is DomElement element)
            {
                if (Display(element) == "none") { return; }
                Check(element, depth);
                if (element.LocalName is "input" or "button" or "textarea" or "select" && Display(element) == "inline-block")
                {
                    output.Add(new(null, Style(element), "", UnitKind.Widget, true) { Widget = element });
                    return;
                }
                if (Display(element) != "inline")
                {
                    throw new UnsupportedLayoutException("Inline-block or block descendants inside inline elements are unsupported.");
                }
                var style = Style(element);
                if (style["white-space"] != inheritedStyle["white-space"])
                {
                    throw new UnsupportedLayoutException("Mixed inline whitespace modes require deferred no-break group analysis.");
                }
                foreach (var side in new[] { "top", "right", "bottom", "left" })
                {
                    if (Dimension(style["margin-" + side], 1) is not (null or 0)
                        || Dimension(style["padding-" + side], 1) is not (null or 0)
                        || Dimension(style["border-" + side + "-width"], 1) is not (null or 0))
                    {
                        throw new UnsupportedLayoutException("Decorated inline boxes require deferred inline-fragment box geometry.");
                    }
                }
                if (element.LocalName == "br")
                {
                    output.Add(new(null, style, "", UnitKind.Break, false));
                    return;
                }
                if (style["background-color"] is not CssColor { Alpha: 0 })
                {
                    throw new UnsupportedLayoutException("Inline backgrounds require deferred inline-fragment paint geometry.");
                }
                output.Add(new(null, style, "", UnitKind.Strut, false));
                foreach (var child in element.ChildNodes) { Gather(child, style, output, depth + 1); }
                output.Add(new(null, style, "", UnitKind.EndStrut, false));
            }
        }

        private List<LayoutLine> Inline(List<InlineUnit> input, CssComputedStyle blockStyle, double x, double y, double width)
        {
            var output = new List<LayoutLine>();
            var pending = new List<Placed>();
            var activeInline = new List<CssComputedStyle>();
            var advance = 0.0;
            var hasContent = false;
            InlineUnit? space = null;
            var previousWord = false;
            var previousWidget = false;
            foreach (var unit in input)
            {
                cancellation.ThrowIfCancellationRequested();
                if (unit.Kind == UnitKind.Strut)
                {
                    activeInline.Add(unit.Style);
                    pending.Add(new(null, Shape("", unit.Style), unit.Style, advance));
                    continue;
                }
                if (unit.Kind == UnitKind.EndStrut)
                {
                    activeInline.RemoveAt(activeInline.Count - 1);
                    continue;
                }
                if (unit.Kind == UnitKind.Space)
                {
                    space ??= unit;
                    previousWord = false;
                    continue;
                }
                if (unit.Kind == UnitKind.Break)
                {
                    space = null;
                    Emit(true);
                    previousWord = false;
                    previousWidget = false;
                    continue;
                }
                if (unit.Kind == UnitKind.Widget)
                {
                    var widget = unit.Widget ?? throw new InvalidOperationException("Inline widget is missing its element.");
                    var (widgetWidth, widgetHeight) = WidgetSize(unit.Style, width, widget);
                    var widgetGap = space is not null && hasContent ? Shape(" ", space.Style) : null;
                    if (hasContent && (space?.Wrap == true || previousWidget)
                        && advance + (widgetGap?.Width ?? 0) + widgetWidth > width)
                    {
                        Emit(false, softWrap: true);
                        widgetGap = null;
                    }
                    if (widgetGap is not null)
                    {
                        pending.Add(new(space!.Source!, widgetGap, space.Style, advance) { JustificationSpace = true });
                        advance += widgetGap.Width;
                    }
                    space = null;
                    pending.Add(new(null, Shape("", unit.Style), unit.Style, advance, widget, widgetWidth, widgetHeight));
                    advance += widgetWidth;
                    hasContent = true;
                    previousWord = false;
                    previousWidget = true;
                    continue;
                }
                if (previousWord && unit.Kind == UnitKind.Word)
                {
                    throw new UnsupportedLayoutException("Text-node/style boundaries inside an unbroken word require cross-boundary shaping.");
                }
                if (unit.HasHyphenBreaks)
                {
                    PlaceHyphenatedWord(unit);
                    previousWord = true;
                    previousWidget = false;
                    continue;
                }
                var run = Shape(unit.Text, unit.Style);
                var gap = space is not null && hasContent ? Shape(" ", space.Style) : null;
                var breakable = space?.Wrap == true || unit.Kind == UnitKind.PreservedSpace && unit.Wrap;
                if (hasContent && breakable && advance + (gap?.Width ?? 0) + run.Width > width)
                {
                    Emit(false, softWrap: true);
                    gap = null;
                }
                if (gap is not null)
                {
                    pending.Add(new(space!.Source!, gap, space.Style, advance) { JustificationSpace = true });
                    advance += gap.Width;
                }
                space = null;
                pending.Add(new(unit.Source!, run, unit.Style, advance));
                advance += run.Width;
                hasContent = true;
                previousWord = unit.Kind == UnitKind.Word;
                previousWidget = false;
            }
            if (hasContent) { Emit(false); }
            return output;

            void PlaceHyphenatedWord(InlineUnit unit)
            {
                var remaining = unit.Text;
                var gap = space is not null && hasContent ? Shape(" ", space.Style) : null;
                while (true)
                {
                    ChargeLineBreakWork(remaining.Length);
                    var whole = Shape(remaining, unit.Style);
                    var available = width - advance - (gap?.Width ?? 0);
                    if (whole.Width <= available)
                    {
                        Place(whole);
                        return;
                    }

                    ChargeLineBreakWork(remaining.Length);
                    ShapedRun? first = null;
                    ShapedRun? best = null;
                    var firstEnd = 0;
                    var bestEnd = 0;
                    for (var index = 0; index < remaining.Length; index++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (remaining[index] != '-') { continue; }
                        var end = index + 1;
                        ChargeLineBreakWork(end);
                        var candidate = Shape(remaining[..end], unit.Style);
                        if (first is null) { first = candidate; firstEnd = end; }
                        if (candidate.Width <= available) { best = candidate; bestEnd = end; }
                    }
                    if (best is null && hasContent && (space?.Wrap == true || previousWidget))
                    {
                        Emit(false, softWrap: true);
                        space = null;
                        gap = null;
                        previousWidget = false;
                        continue;
                    }
                    if (best is null)
                    {
                        best = first;
                        bestEnd = firstEnd;
                    }
                    if (best is null)
                    {
                        Place(whole);
                        return;
                    }
                    Place(best);
                    Emit(false, softWrap: true);
                    ChargeLineBreakWork(remaining.Length - bestEnd);
                    remaining = remaining[bestEnd..];
                    gap = null;
                    previousWidget = false;
                }

                void Place(ShapedRun run)
                {
                    if (gap is not null)
                    {
                        pending.Add(new(space!.Source!, gap, space.Style, advance) { JustificationSpace = true });
                        advance += gap.Width;
                    }
                    space = null;
                    pending.Add(new(unit.Source!, run, unit.Style, advance));
                    advance += run.Width;
                    hasContent = true;
                }
            }

            void Emit(bool force, bool softWrap = false)
            {
                if (!force && !hasContent) { return; }
                if (++lines > options.MaxLines) { throw new LayoutLimitException("Layout line limit exceeded."); }
                var strut = Shape("", blockStyle);
                var (ascent, descent) = Extents(strut, blockStyle);
                foreach (var placed in pending)
                {
                    if (placed.Widget is not null)
                    {
                        ascent = Math.Max(ascent, placed.Height);
                        continue;
                    }
                    var extents = Extents(placed.Run, placed.Style);
                    ascent = Math.Max(ascent, extents.Ascent);
                    descent = Math.Max(descent, extents.Descent);
                }
                var align = ((CssKeyword)blockStyle["text-align"]).Value;
                if (align == "justify" && softWrap && width > advance) { Justify(); }
                var offset = align is "right" or "end" ? width - advance : align == "center" ? (width - advance) / 2 : 0;
                var baseline = y + ascent;
                var fragments = pending.Where(p => p.Source is not null).Select(p => new LayoutTextFragment(p.Source!, p.Run,
                    x + offset + p.Advance, baseline, p.Style)).ToList().AsReadOnly();
                var widgets = pending.Where(placed => placed.Widget is not null).Select(placed =>
                    new LayoutInlineWidget(placed.Widget!, Rect(x + offset + placed.Advance, y + ascent - placed.Height,
                        placed.Width, placed.Height))).ToArray();
                output.Add(new LayoutLine(Rect(x, y, width, ascent + descent), Finite(baseline), fragments)
                { Widgets = Array.AsReadOnly(widgets) });
                y += ascent + descent;
                pending.Clear();
                advance = 0;
                hasContent = false;
                foreach (var style in activeInline) { pending.Add(new(null, Shape("", style), style, 0)); }
            }

            void Justify()
            {
                ChargeLineBreakWork(pending.Count);
                var opportunities = 0;
                foreach (var placed in pending)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (placed.Run.Text.Contains('\u00a0'))
                    {
                        throw new UnsupportedLayoutException("Justification of no-break spaces is unsupported.");
                    }
                    if (placed.JustificationSpace) { opportunities++; }
                }
                if (opportunities == 0) { return; }
                var slack = width - advance;
                var shift = 0.0;
                var seen = 0;
                for (var index = 0; index < pending.Count; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var placed = pending[index] with { Advance = Finite(pending[index].Advance + shift) };
                    if (placed.JustificationSpace)
                    {
                        if (placed.Run.Glyphs.Count == 0)
                        { throw new UnsupportedLayoutException("Justified spaces require a shaped glyph."); }
                        var nextShift = slack * (++seen / (double)opportunities);
                        var addition = nextShift - shift;
                        var glyphs = placed.Run.Glyphs.ToArray();
                        glyphs[^1] = glyphs[^1] with { Advance = Finite(glyphs[^1].Advance + addition) };
                        placed = placed with
                        {
                            Run = placed.Run with
                            {
                                Glyphs = Array.AsReadOnly(glyphs),
                                Width = Finite(placed.Run.Width + addition)
                            }
                        };
                        shift = nextShift;
                    }
                    pending[index] = placed;
                }
            }
        }

        private static int TextareaDimension(DomElement element, string attribute, int fallback, int maximum)
        {
            if (element.GetAttribute(attribute) is not { } raw) { return fallback; }
            var value = raw.TrimStart(' ', '\t', '\n', '\f', '\r');
            var digits = value.TakeWhile(char.IsAsciiDigit).Count();
            if (digits == 0 || !int.TryParse(value.AsSpan(0, digits), out var parsed) || parsed == 0) { return fallback; }
            if (parsed > maximum) { throw new LayoutLimitException($"<textarea> {attribute} exceeds the supported limit ({maximum})."); }
            return parsed;
        }

        /// <summary>
        /// Rows drawn by the bounded select subset: the leading non-negative integer of <c>size</c>
        /// (https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-non-negative-integers,
        /// local spec ID <c>html</c>) when it is 1..12, otherwise one compact row. Multiple selects use the same
        /// explicit-size rule; an absent size stays compact rather than using the HTML default of four rows.
        /// </summary>
        private static int SelectRows(DomElement element)
        {
            if (element.GetAttribute("size") is not { } raw) { return 1; }
            var value = raw.TrimStart(' ', '\t', '\n', '\f', '\r');
            var rows = 0;
            var digits = 0;
            foreach (var character in value)
            {
                if (!char.IsAsciiDigit(character)) { break; }
                rows = rows * 10 + character - '0';
                digits++;
                if (rows > 12) { return 1; }
            }
            return digits > 0 && rows >= 1 ? rows : 1;
        }

        private static (double Width, double Height) WidgetSize(CssComputedStyle style, double containingWidth, DomElement element)
        {
            var margin = Edges(style, "margin", containingWidth);
            if (margin != default)
            { throw new UnsupportedLayoutException("Margins on inline form controls require deferred inline-block margin layout."); }
            var padding = Edges(style, "padding", containingWidth);
            var border = Edges(style, "border", containingWidth, "-width");
            var horizontal = padding.Left + padding.Right + border.Left + border.Right;
            var vertical = padding.Top + padding.Bottom + border.Top + border.Bottom;
            var sizing = ((CssKeyword)style["box-sizing"]).Value;
            var minWidth = Dimension(style["min-width"], containingWidth) ?? 0;
            var maxWidth = Dimension(style["max-width"], containingWidth) ?? double.PositiveInfinity;
            var minHeight = Dimension(style["min-height"], null) ?? 0;
            var maxHeight = Dimension(style["max-height"], null) ?? double.PositiveInfinity;
            var width = Dimension(style["width"], containingWidth) ?? (element.LocalName == "textarea"
                ? TextareaDimension(element, "cols", 20, 128) * 8 : 160);
            var defaultHeight = 20;
            if (element.LocalName == "textarea") { defaultHeight = TextareaDimension(element, "rows", 2, 64) * 20; }
            else if (element.LocalName == "select") { defaultHeight = SelectRows(element) * 20; }
            var height = Dimension(style["height"], null) ?? defaultHeight;
            if (sizing == "border-box")
            {
                minWidth = Math.Max(0, minWidth - horizontal);
                maxWidth = double.IsPositiveInfinity(maxWidth) ? maxWidth : Math.Max(0, maxWidth - horizontal);
                minHeight = Math.Max(0, minHeight - vertical);
                maxHeight = double.IsPositiveInfinity(maxHeight) ? maxHeight : Math.Max(0, maxHeight - vertical);
                width = Math.Max(0, width - horizontal);
                height = Math.Max(0, height - vertical);
            }
            width = Math.Max(minWidth, Math.Min(width, maxWidth));
            height = Math.Max(minHeight, Math.Min(height, maxHeight));
            return (Finite(width + horizontal), Finite(height + vertical));
        }

        private ShapedRun Shape(string text, CssComputedStyle style)
        {
            if (++calls > options.MaxShapingCalls) { throw new LayoutLimitException("Layout shaping-call limit exceeded."); }
            var request = new TextFontRequest(((CssFontFamilies)style["font-family"]).Names,
                ((CssLength)style["font-size"]).Value, ((CssNumber)style["font-weight"]).Value,
                ((CssKeyword)style["font-style"]).Value);
            var run = shaper.Shape(text, request, cancellation);
            glyphs = checked(glyphs + run.Glyphs.Count);
            if (glyphs > options.MaxShapedGlyphs) { throw new LayoutLimitException("Layout shaped glyph limit exceeded."); }
            if (!double.IsFinite(run.Width) || run.Width < 0 || !double.IsFinite(run.Metrics.Ascent)
                || !double.IsFinite(run.Metrics.Descent) || !double.IsFinite(run.Metrics.LineGap)
                || run.Metrics.Ascent < 0 || run.Metrics.Descent < 0)
            {
                throw new InvalidOperationException("Text shaper returned invalid horizontal metrics.");
            }
            return run;
        }

        private void ChargeLineBreakWork(int count)
        {
            cancellation.ThrowIfCancellationRequested();
            if (count > options.MaxLineBreakOperations - lineBreakOperations)
            { throw new LayoutLimitException("Layout line-break processing limit exceeded."); }
            lineBreakOperations += count;
        }

        private void Visit()
        {
            cancellation.ThrowIfCancellationRequested();
            if (++nodes > options.MaxNodes) { throw new LayoutLimitException("Layout visited node limit exceeded."); }
        }

        private static (double Ascent, double Descent) Extents(ShapedRun run, CssComputedStyle style)
        {
            var height = style["line-height"] switch
            {
                CssNumber number => number.Value * run.FontSize,
                CssLength length => length.Value,
                _ => run.Metrics.Ascent + run.Metrics.Descent + run.Metrics.LineGap
            };
            var halfLeading = (height - run.Metrics.Ascent - run.Metrics.Descent) / 2;
            return (Finite(run.Metrics.Ascent + halfLeading), Finite(run.Metrics.Descent + halfLeading));
        }
        private static LayoutEdges Edges(CssComputedStyle style, string prefix, double width, string suffix = "")
            => new(Dimension(style[prefix + "-top" + suffix], width) ?? 0,
                Dimension(style[prefix + "-right" + suffix], width) ?? 0,
                Dimension(style[prefix + "-bottom" + suffix], width) ?? 0,
                Dimension(style[prefix + "-left" + suffix], width) ?? 0);
        private static double ContentWidth(CssComputedStyle style, double containingWidth)
        {
            var padding = Edges(style, "padding", containingWidth);
            var border = Edges(style, "border", containingWidth, "-width");
            var margin = Edges(style, "margin", containingWidth);
            var horizontal = padding.Left + padding.Right + border.Left + border.Right;
            var sizing = ((CssKeyword)style["box-sizing"]).Value;
            var width = Dimension(style["width"], containingWidth) is { } specified
                ? Math.Max(0, specified - (sizing == "border-box" ? horizontal : 0))
                : Math.Max(0, containingWidth - horizontal - margin.Left - margin.Right);
            var minWidth = Dimension(style["min-width"], containingWidth) ?? 0;
            var maxWidth = Dimension(style["max-width"], containingWidth) ?? double.PositiveInfinity;
            if (sizing == "border-box")
            {
                minWidth = Math.Max(0, minWidth - horizontal);
                maxWidth = double.IsPositiveInfinity(maxWidth) ? maxWidth : Math.Max(0, maxWidth - horizontal);
            }
            return Math.Max(minWidth, Math.Min(width, maxWidth));
        }

        private static double? Dimension(CssValue value, double? reference) => value switch
        {
            CssLength { Unit: "px" } length => Finite(length.Value),
            CssLength { Unit: "%" } length => reference is { } size ? Finite(length.Value / 100 * size) : null,
            CssKeyword => null,
            _ => throw new UnsupportedLayoutException("Layout expected a computed px/percentage/keyword dimension.")
        };
        private static double Finite(double value) => double.IsFinite(value) ? value
            : throw new LayoutLimitException("Layout geometry exceeds finite numeric range.");
        private static LayoutRect Rect(double x, double y, double width, double height)
            => new(Finite(x), Finite(y), Finite(width), Finite(height));
        private enum UnitKind { Word, Space, PreservedSpace, Break, Strut, EndStrut, Widget }
        private sealed record InlineUnit(DomText? Source, CssComputedStyle Style, string Text, UnitKind Kind, bool Wrap)
        {
            public DomElement? Widget { get; init; }
            public bool HasHyphenBreaks { get; init; }
        }
        private sealed record Placed(DomText? Source, ShapedRun Run, CssComputedStyle Style, double Advance,
            DomElement? Widget = null, double Width = 0, double Height = 0)
        {
            public bool JustificationSpace { get; init; }
        }
    }
}
