using System.Text;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Text;

namespace VisualWeb.Engine.Layout;

public sealed class UnsupportedLayoutException(string message) : Exception(message);
public sealed class LayoutLimitException(string message) : Exception(message);

/// <summary>CSS-pixel geometry; no device-pixel snapping or rasterization.</summary>
/// <remarks>Spec: css-box; <see href="https://www.w3.org/TR/css-box-3/#box-model">box model</see>.</remarks>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height);
public readonly record struct LayoutEdges(double Top, double Right, double Bottom, double Left);
public sealed record LayoutTextFragment(DomText Source, ShapedRun Run, double X, double Baseline, CssComputedStyle Style);
public sealed record LayoutInlineWidget(DomElement Element, LayoutRect Bounds);
public sealed record LayoutLine(LayoutRect Bounds, double Baseline, IReadOnlyList<LayoutTextFragment> Fragments)
{
    public IReadOnlyList<LayoutInlineWidget> Widgets { get; init; } = Array.Empty<LayoutInlineWidget>();
}
public abstract record LayoutFlowItem;
public sealed record LayoutBlockItem(LayoutBox Box) : LayoutFlowItem;
public sealed record LayoutLineItem(LayoutLine Line) : LayoutFlowItem;
public sealed record LayoutBox(DomElement Element, LayoutRect Content, LayoutRect PaddingBox,
    LayoutRect BorderBox, LayoutEdges Margin, IReadOnlyList<LayoutBox> Children, IReadOnlyList<LayoutLine> Lines)
{
    public IReadOnlyList<LayoutFlowItem> Flow { get; init; } = Array.Empty<LayoutFlowItem>();
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
    internal void Validate()
    {
        if (MaxDepth <= 0 || MaxBoxes <= 0 || MaxNodes <= 0 || MaxLines <= 0 || MaxTextCharacters <= 0
            || MaxShapedGlyphs <= 0 || MaxShapingCalls <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayoutOptions), "All layout limits must be positive.");
        }
    }
}

/// <summary>Finite horizontal LTR block/inline layout returning renderer-local geometry snapshots.</summary>
/// <remarks>Specs: css2-visual/css2-sizing;
/// <see href="https://www.w3.org/TR/CSS22/visuren.html#block-formatting">block formatting</see>,
/// <see href="https://www.w3.org/TR/CSS22/visuren.html#inline-formatting">inline formatting</see>.
/// Margin collapse, bidi, floats, non-form replaced elements and complex line breaking fail explicitly.</remarks>
public static class StaticLayout
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

    private sealed class Builder
    {
        private readonly CssStyleResult styles;
        private readonly ITextShaper shaper;
        private readonly LayoutOptions options;
        private readonly CancellationToken cancellation;
        private int nodes, boxes, lines, characters, glyphs, calls;

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
        private void Check(DomElement element, int depth, bool block = false)
        {
            cancellation.ThrowIfCancellationRequested();
            if (depth > options.MaxDepth) { throw new LayoutLimitException("Layout nesting limit exceeded."); }
            if (element.GetAttribute("dir") is { } dir && !dir.Equals("ltr", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnsupportedLayoutException("dir=rtl/auto requires bidi layout.");
            }
            if (element.GetAttribute("lang") is { Length: > 0 } language && !language.Equals("en", StringComparison.OrdinalIgnoreCase)
                && !language.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnsupportedLayoutException("Language-dependent shaping/segment-break handling beyond English is deferred.");
            }
            if (element.LocalName is "input" or "button")
            {
                if (!block && Display(element) != "inline-block")
                { throw new UnsupportedLayoutException("Form controls require block or supported inline-block layout."); }
                if (element.LocalName == "input" && DomFormControls.InputType(element) is not ("text" or "search" or "tel" or "submit"))
                {
                    throw new UnsupportedLayoutException($"<input type={DomFormControls.InputType(element)}> requires unsupported widget layout.");
                }

                return;
            }
            if (element.LocalName is "bdi" or "bdo" or "img" or "textarea" or "select"
                or "video" or "audio" or "canvas" or "iframe" or "object" or "embed" or "ul" or "ol" or "li"
                or "table" or "ruby")
            {
                throw new UnsupportedLayoutException($"<{element.LocalName}> requires unsupported specialized layout.");
            }
        }

        private LayoutBox Block(DomElement element, double containingX, double top, double containingWidth,
            double? containingHeight, int depth, bool isRoot = false, bool suppressTopMargin = false,
            bool suppressBottomMargin = false)
        {
            Check(element, depth, block: true);
            Visit();
            if (++boxes > options.MaxBoxes) { throw new LayoutLimitException("Layout box limit exceeded."); }
            var style = Style(element);
            var padding = Edges(style, "padding", containingWidth);
            var border = Edges(style, "border", containingWidth, "-width");
            var margin = Edges(style, "margin", containingWidth);
            var horizontal = padding.Left + padding.Right + border.Left + border.Right;
            var sizing = ((CssKeyword)style["box-sizing"]).Value;
            var width = ContentWidth(style, containingWidth);
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
            var minHeight = Dimension(style["min-height"], containingHeight) ?? 0;
            var maxHeight = Dimension(style["max-height"], containingHeight) ?? double.PositiveInfinity;
            if (sizing == "border-box")
            {
                minHeight = Math.Max(0, minHeight - vertical);
                maxHeight = double.IsPositiveInfinity(maxHeight) ? maxHeight : Math.Max(0, maxHeight - vertical);
            }
            if (definiteHeight is { } definite) { definiteHeight = Math.Max(minHeight, Math.Min(definite, maxHeight)); }
            var collapseTopChild = !isRoot && padding.Top == 0 && border.Top == 0
                ? EdgeBlockChild(element, first: true) : null;
            var collapseBottomChild = padding.Bottom == 0 && border.Bottom == 0
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
                // Spec: html; https://html.spec.whatwg.org/multipage/rendering.html#the-input-element-as-a-text-entry-widget
                // The value/label is shell-painted, so the box reserves exactly one strut line of content height.
                var (ascent, descent) = Extents(Shape("", style), style);
                cursor += ascent + descent;
            }
            foreach (var child in element.LocalName == "input" ? [] : element.ChildNodes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (child is DomElement e && Display(e) == "none") { continue; }
                if (child is DomElement block && Display(block) == "block")
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
            var autoHeight = cursor - contentY;
            var contentHeight = definiteHeight ?? autoHeight;
            contentHeight = Math.Max(minHeight, Math.Min(contentHeight, maxHeight));
            var content = Rect(contentX, contentY, width, contentHeight);
            var paddingBox = Rect(contentX - padding.Left, contentY - padding.Top,
                width + padding.Left + padding.Right, contentHeight + padding.Top + padding.Bottom);
            var borderBox = Rect(borderX, borderY, width + horizontal, contentHeight + vertical);
            return new(element, content, paddingBox, borderBox, margin, childBoxes.AsReadOnly(), blockLines.AsReadOnly())
            {
                Flow = flow.AsReadOnly()
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

        private DomElement? EdgeBlockChild(DomElement parent, bool first)
        {
            var children = first ? parent.ChildNodes : parent.ChildNodes.Reverse();
            foreach (var node in children)
            {
                cancellation.ThrowIfCancellationRequested();
                if (node is DomComment || node is DomText text && IsCollapsibleWhitespace(text.Data)) { continue; }
                if (node is not DomElement element) { return null; }
                if (Display(element) == "none") { continue; }
                return Display(element) == "block" ? element : null;
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

        private void Gather(DomNode node, CssComputedStyle inheritedStyle, List<InlineUnit> output, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            Visit();
            if (depth > options.MaxDepth) { throw new LayoutLimitException("Inline nesting limit exceeded."); }
            if (node is DomText text)
            {
                characters = checked(characters + text.Data.Length);
                if (characters > options.MaxTextCharacters) { throw new LayoutLimitException("Layout text limit exceeded."); }
                var whitespace = ((CssKeyword)inheritedStyle["white-space"]).Value;
                if (whitespace is not ("normal" or "nowrap" or "pre" or "pre-line"))
                {
                    throw new UnsupportedLayoutException($"white-space:{whitespace} is outside the initial layout subset.");
                }
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
                    if (value.Any(c => c is '-' or '/' or '\\' or >= '\u2010' and <= '\u2015'))
                    {
                        throw new UnsupportedLayoutException("Hyphen/punctuation line-break opportunities are deferred.");
                    }
                    for (var index = 1; index < value.Length - 1; index++)
                    {
                        var c = value[index];
                        if (!char.IsLetterOrDigit(c) && char.GetUnicodeCategory(c) is not System.Globalization.UnicodeCategory.NonSpacingMark
                            && c is not ('\'' or '\u2019' or '\u00A0')
                            && !(c == '.' && char.IsAsciiDigit(value[index - 1]) && char.IsAsciiDigit(value[index + 1])))
                        {
                            throw new UnsupportedLayoutException("Internal punctuation needs deferred Unicode line-break analysis.");
                        }
                    }
                    output.Add(new(text, inheritedStyle, value, UnitKind.Word, whitespace is not ("pre" or "nowrap")));
                    word.Clear();
                }
            }
            else if (node is DomElement element)
            {
                if (Display(element) == "none") { return; }
                Check(element, depth);
                if (element.LocalName == "button" && DomFormControls.ButtonType(element) != "submit"
                    && Display(element) == "inline-block")
                {
                    var buttonStyle = Style(element);
                    foreach (var child in element.ChildNodes) { Gather(child, buttonStyle, output, depth + 1); }
                    return;
                }
                if (element.LocalName is "input" or "button" && Display(element) == "inline-block")
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
                    var (widgetWidth, widgetHeight) = WidgetSize(unit.Style, width);
                    var widgetGap = space is not null && hasContent ? Shape(" ", space.Style) : null;
                    if (hasContent && (space?.Wrap == true || previousWidget)
                        && advance + (widgetGap?.Width ?? 0) + widgetWidth > width)
                    {
                        Emit(false);
                        widgetGap = null;
                    }
                    if (widgetGap is not null)
                    {
                        pending.Add(new(space!.Source!, widgetGap, space.Style, advance));
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
                var run = Shape(unit.Text, unit.Style);
                var gap = space is not null && hasContent ? Shape(" ", space.Style) : null;
                var breakable = space?.Wrap == true || unit.Kind == UnitKind.PreservedSpace && unit.Wrap;
                if (hasContent && breakable && advance + (gap?.Width ?? 0) + run.Width > width)
                {
                    Emit(false);
                    gap = null;
                }
                if (gap is not null)
                {
                    pending.Add(new(space!.Source!, gap, space.Style, advance));
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

            void Emit(bool force)
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
                if (align == "justify") { throw new UnsupportedLayoutException("Justification is deferred."); }
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
        }

        private static (double Width, double Height) WidgetSize(CssComputedStyle style, double containingWidth)
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
            var width = Dimension(style["width"], containingWidth) ?? 160;
            var height = Dimension(style["height"], null) ?? 20;
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
        }
        private sealed record Placed(DomText? Source, ShapedRun Run, CssComputedStyle Style, double Advance,
            DomElement? Widget = null, double Width = 0, double Height = 0);
    }
}
