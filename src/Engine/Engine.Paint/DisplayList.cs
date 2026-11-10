using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Text;

namespace VisualWeb.Engine.Paint;

public sealed class PaintLimitException(string message) : Exception(message);
public sealed class UnsupportedPaintException(string message) : Exception(message);

public sealed record PaintOptions
{
    public const double MaxDocumentHeight = 10_000_000;
    public const double MaxDocumentWidth = 10_000_000;
    public int MaxCommands { get; init; } = 1_000_000;
    public int MaxGlyphs { get; init; } = 1_000_000;
    public int MaxDepth { get; init; } = 128;
    public int MaxPixels { get; init; } = 16_777_216;
    public int MaxFonts { get; init; } = 256;
    public int MaxFontBytes { get; init; } = 32 * 1024 * 1024;
    public long MaxTotalFontBytes { get; init; } = 128 * 1024 * 1024;
    internal void Validate()
    {
        if (MaxCommands <= 0 || MaxGlyphs <= 0 || MaxDepth <= 0 || MaxPixels <= 0
            || MaxFonts <= 0 || MaxFontBytes <= 0 || MaxTotalFontBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PaintOptions), "All paint limits must be positive.");
        }
    }
}

public abstract record PaintCommand;
public sealed record FillRectangle(LayoutRect Bounds, CssColor Color) : PaintCommand;
/// <summary>A bounded underline using the registered font's OpenType underline metrics.</summary>
/// <remarks>Spec: css-text-decoration; <see href="https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-line">underline</see>.</remarks>
public sealed record DrawUnderline(string FontIdentity, double FontSize, CssColor Color,
    double X, double Baseline, double Width) : PaintCommand;
/// <summary>A bounded line-through using the registered font's OpenType strikeout metrics.</summary>
/// <remarks>Spec: css-text-decoration; <see href="https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-line">line-through</see>.</remarks>
public sealed record DrawLineThrough(string FontIdentity, double FontSize, CssColor Color,
    double X, double Baseline, double Width) : PaintCommand;
/// <summary>A bounded solid ellipse used by disc list markers.</summary>
/// <remarks>Spec: css2-lists; <see href="https://www.w3.org/TR/CSS22/generate.html#list-style">list style</see>.</remarks>
public sealed record FillEllipse(LayoutRect Bounds, CssColor Color) : PaintCommand;
/// <summary>A bounded, single-composite ellipse outline used by hollow circle list markers.</summary>
/// <remarks>Spec: css2-lists; <see href="https://www.w3.org/TR/CSS22/generate.html#list-style">list style</see>.
/// Bounds enclose the entire stroke; the interior is not painted. Raster preflight requires finite,
/// positive bounds and a finite positive stroke narrower than half the smaller dimension, with
/// representable inset geometry and device-scaled stroke.</remarks>
public sealed record StrokeEllipse(LayoutRect Bounds, CssColor Color, double StrokeWidth) : PaintCommand;
public sealed record PushScrollClip(LayoutRect Clip, double OffsetY) : PaintCommand;
public sealed record PopScrollClip : PaintCommand;
public readonly record struct PaintGlyph(ushort Id, double X, double Y);
public sealed record DrawGlyphRun : PaintCommand
{
    public string FontIdentity { get; }
    public double FontSize { get; }
    public CssColor Color { get; }
    public IReadOnlyList<PaintGlyph> Glyphs { get; }
    public DrawGlyphRun(string fontIdentity, double fontSize, CssColor color, IEnumerable<PaintGlyph> glyphs,
        PaintOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontIdentity);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(glyphs);
        FontIdentity = fontIdentity;
        FontSize = fontSize;
        Color = color;
        var limits = options ?? new();
        limits.Validate();
        var copy = new List<PaintGlyph>();
        foreach (var glyph in glyphs)
        {
            if (copy.Count >= limits.MaxGlyphs) { throw new PaintLimitException("Glyph command limit exceeded."); }
            copy.Add(glyph);
        }
        Glyphs = copy.AsReadOnly();
    }
}

/// <summary>Immutable data-only commands, without DOM, layout owners or native handles.</summary>
/// <remarks>Spec: css2-paint; <see href="https://www.w3.org/TR/CSS22/zindex.html">painting order</see>.
/// Explicit font resource identities must be resolved by the recipient; this is not an IPC protocol yet.</remarks>
public sealed class DisplayList
{
    public double Width { get; }
    public double Height { get; }
    public IReadOnlyList<PaintCommand> Commands { get; }
    public DisplayList(double width, double height, IEnumerable<PaintCommand> commands, PaintOptions? options = null)
    {
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be finite and positive.");
        }
        ArgumentNullException.ThrowIfNull(commands);
        Width = width;
        Height = height;
        var limits = options ?? new();
        limits.Validate();
        var copy = new List<PaintCommand>();
        long glyphCount = 0;
        foreach (var command in commands)
        {
            if (copy.Count >= limits.MaxCommands) { throw new PaintLimitException("Display command limit exceeded."); }
            ArgumentNullException.ThrowIfNull(command);
            if (command is DrawGlyphRun run)
            {
                glyphCount += run.Glyphs.Count;
                if (glyphCount > limits.MaxGlyphs) { throw new PaintLimitException("Display glyph limit exceeded."); }
            }
            copy.Add(command);
        }
        Commands = copy.AsReadOnly();
    }
}

/// <summary>Generate block and inline-fragment backgrounds, physical solid borders and positioned glyph runs.</summary>
/// <remarks>Specs: css2-paint/css-backgrounds;
/// <see href="https://www.w3.org/TR/CSS22/zindex.html">normal-flow painting</see>,
/// <see href="https://www.w3.org/TR/css-backgrounds-3/#special-backgrounds">canvas backgrounds</see>.</remarks>
public static class DisplayListBuilder
{
    public readonly record struct ScrollClipState(LayoutRect Clip, double OffsetY);

    public static DisplayList Build(LayoutResult layout, CssStyleResult styles, PaintOptions? options = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<LayoutBox, ScrollClipState>? scrollClips = null, double? documentWidth = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(styles);
        var limits = options ?? new();
        limits.Validate();
        var canvasWidth = documentWidth ?? layout.ViewportWidth;
        if (!double.IsFinite(canvasWidth) || canvasWidth < layout.ViewportWidth || canvasWidth > PaintOptions.MaxDocumentWidth)
        { throw new ArgumentOutOfRangeException(nameof(documentWidth), "Canvas width must be within the bounded document extent."); }
        cancellationToken.ThrowIfCancellationRequested();
        if (styles.Diagnostics.Count != 0) { throw new UnsupportedPaintException("Resolve CSS diagnostics before generating paint commands."); }
        var commands = new List<PaintCommand>();
        long glyphCount = 0;
        if (layout.Root is { } root)
        {
            var rootColor = Color(root, "background-color");
            var propagated = root;
            if (root.Element.LocalName == "html" && rootColor.Alpha == 0
                && root.Children.FirstOrDefault(b => b.Element.LocalName == "body") is { } body)
            {
                rootColor = Color(body, "background-color");
                propagated = body;
            }
            // Canvas background must cover the visible region after a bounded scroll translation.
            if (rootColor.Alpha > 0)
            {
                Add(new FillRectangle(new(0, 0, canvasWidth,
                Math.Max(layout.ViewportHeight, root.BorderBox.Y + root.BorderBox.Height)), rootColor));
            }
            Background(root, 1);
            Text(root, 1);

            void Background(LayoutBox box, int depth)
            {
                Check(depth);
                BackgroundLayer(box);
                if (box.Element.LocalName == "table")
                {
                    WithClip(box, () => TableLayers(box, depth));
                    return;
                }
                WithClip(box, () =>
                {
                    foreach (var child in box.Children) { Background(child, depth + 1); }
                });
            }
            void TableLayers(LayoutBox table, int depth)
            {
                foreach (var caption in table.Children.Where(child => child.Element.LocalName == "caption"))
                { Background(caption, depth + 1); }
                PaintTableColumnLayers(table.TableColumnGroupBackgrounds);
                PaintTableColumnLayers(table.TableColumnBackgrounds);
                var groups = table.Children.Where(child => child.Element.LocalName != "caption").ToArray();
                foreach (var group in groups)
                {
                    Check(depth + 1);
                    BackgroundLayer(group);
                    WithClip(group, () =>
                    {
                        foreach (var row in group.Children)
                        {
                            Check(depth + 2);
                            BackgroundLayer(row);
                        }
                    });
                }
                foreach (var group in groups)
                {
                    WithClip(group, () =>
                    {
                        foreach (var row in group.Children)
                        {
                            WithClip(row, () =>
                            {
                                foreach (var cell in row.Children)
                                { Background(cell, depth + 3); }
                            });
                        }
                    });
                }

                void PaintTableColumnLayers(IReadOnlyList<LayoutTableBackground> backgrounds)
                {
                    foreach (var background in backgrounds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Add(new FillRectangle(background.Bounds, background.Color));
                    }
                }
            }
            void BackgroundLayer(LayoutBox box)
            {
                if (box != root && box != propagated && Color(box, "background-color") is { Alpha: > 0 } background)
                { Add(new FillRectangle(box.TableBoxGeometry?.BorderBox ?? box.BorderBox, background)); }
                Border(box);
            }
            void WithClip(LayoutBox box, Action paintChildren)
            {
                if (scrollClips is not null && scrollClips.TryGetValue(box, out var clip))
                {
                    Add(new PushScrollClip(clip.Clip, clip.OffsetY));
                    paintChildren();
                    Add(new PopScrollClip());
                }
                else { paintChildren(); }
            }
        }
        return new(layout.ViewportWidth, layout.ViewportHeight, commands, limits);

        CssColor Color(LayoutBox box, string property) => Style(box)[property] as CssColor
            ?? throw new UnsupportedPaintException($"Unresolved paint color '{property}'.");
        CssComputedStyle Style(LayoutBox box) => styles.Styles.TryGetValue(box.Element, out var style) ? style
            : throw new InvalidOperationException("Paint styles are incomplete or stale.");
        void Check(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > limits.MaxDepth) { throw new PaintLimitException("Paint tree depth limit exceeded."); }
        }
        void Add(PaintCommand command)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (commands.Count >= limits.MaxCommands) { throw new PaintLimitException("Display command limit exceeded."); }
            commands.Add(command);
        }
        void Border(LayoutBox box)
        {
            var style = Style(box);
            var b = box.TableBoxGeometry?.BorderBox ?? box.BorderBox;
            var p = box.TableBoxGeometry?.PaddingBox ?? box.PaddingBox;
            var sides = new[]
            {
                ("top", new LayoutRect(b.X, b.Y, b.Width, p.Y - b.Y), p.Y - b.Y),
                ("bottom", new LayoutRect(b.X, p.Y + p.Height, b.Width, b.Y + b.Height - p.Y - p.Height), b.Y + b.Height - p.Y - p.Height),
                ("left", new LayoutRect(b.X, p.Y, p.X - b.X, p.Height), p.X - b.X),
                ("right", new LayoutRect(p.X + p.Width, p.Y, b.X + b.Width - p.X - p.Width, p.Height), b.X + b.Width - p.X - p.Width)
            };
            var visible = sides.Where(s => s.Item3 > 0 && b.Width > 0 && b.Height > 0).ToList();
            if (visible.Count == 0) { return; }
            CssColor? borderColor = null;
            foreach (var (side, bounds, thickness) in visible)
            {
                var sideStyle = style["border-" + side + "-style"] as CssKeyword
                    ?? throw new UnsupportedPaintException("Only solid, inset, outset, and dashed visible borders are supported.");
                if (sideStyle.Value is not ("solid" or "inset" or "outset" or "dashed"))
                {
                    throw new UnsupportedPaintException("Only solid, inset, outset, and dashed visible borders are supported.");
                }
                var color = Color(box, "border-" + side + "-color");
                if (borderColor is not null && color != borderColor)
                {
                    throw new UnsupportedPaintException("Different side colors require deferred border-corner joins.");
                }
                borderColor = color;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    if (sideStyle.Value == "dashed")
                    {
                        var horizontal = side is "top" or "bottom";
                        var extent = horizontal ? bounds.Width : bounds.Height;
                        var dashLength = thickness * 3;
                        var period = thickness * 5;
                        for (var index = 0; ; index++)
                        {
                            var offset = index * period;
                            if (offset >= extent) { break; }
                            var length = Math.Min(dashLength, extent - offset);
                            var dash = horizontal
                                ? new LayoutRect(bounds.X + offset, bounds.Y, length, bounds.Height)
                                : new LayoutRect(bounds.X, bounds.Y + offset, bounds.Width, length);
                            Add(new FillRectangle(dash, color));
                        }
                        continue;
                    }
                    var paintColor = sideStyle.Value switch
                    {
                        "inset" => ShadeColor(side, color, darkTopLeft: true),
                        "outset" => ShadeColor(side, color, darkTopLeft: false),
                        _ => color
                    };
                    Add(new FillRectangle(bounds, paintColor));
                }
            }
        }
        static CssColor ShadeColor(string side, CssColor color, bool darkTopLeft)
        {
            var darkSide = darkTopLeft
                ? side is "top" or "left"
                : side is "bottom" or "right";
            byte Shade(byte channel) => darkSide
                ? (byte)(channel / 2)
                : (byte)(channel + (byte.MaxValue - channel + 1) / 2);
            return new(Shade(color.Red), Shade(color.Green), Shade(color.Blue), color.Alpha);
        }
        void Text(LayoutBox box, int depth)
        {
            Check(depth);
            if (box.DiscMarker is { Width: > 0, Height: > 0 } marker)
            {
                Add(new FillEllipse(marker, Color(box, "color")));
            }
            if (box.CircleMarker is { Width: > 0, Height: > 0 } circle)
            {
                Add(new StrokeEllipse(circle, Color(box, "color"), Math.Min(circle.Width, circle.Height) / 4));
            }
            if (box.SquareMarker is { Width: > 0, Height: > 0 } square)
            {
                Add(new FillRectangle(square, Color(box, "color")));
            }
            if (box.TextMarker is { } textMarker)
            {
                DrawText(textMarker.Run, textMarker.X, textMarker.Baseline, Color(box, "color"));
            }
            if (scrollClips is not null && scrollClips.TryGetValue(box, out var clip))
            {
                Add(new PushScrollClip(clip.Clip, clip.OffsetY));
            }
            if (box.Flow.Count != box.Children.Count + box.Lines.Count)
            {
                throw new InvalidOperationException("Layout flow is missing or inconsistent.");
            }
            var children = new HashSet<LayoutBox>(box.Children, ReferenceEqualityComparer.Instance);
            var lines = new HashSet<LayoutLine>(box.Lines, ReferenceEqualityComparer.Instance);
            foreach (var item in box.Flow)
            {
                Check(depth);
                if (item is LayoutBlockItem child)
                {
                    if (!children.Remove(child.Box)) { throw new InvalidOperationException("Layout flow contains an absent or duplicate block."); }
                    Text(child.Box, depth + 1);
                }
                else if (item is LayoutLineItem line)
                {
                    if (!lines.Remove(line.Line)) { throw new InvalidOperationException("Layout flow contains an absent or duplicate line."); }
                    foreach (var background in line.Line.InlineBackgrounds)
                    {
                        Add(new FillRectangle(background.Bounds, background.Color));
                    }
                    foreach (var fragment in line.Line.Fragments)
                    {
                        DrawText(fragment.Run, fragment.X, fragment.Baseline,
                            fragment.Style["color"] as CssColor ?? throw new UnsupportedPaintException("Unresolved text color."));
                        if (fragment.Run.Width > 0)
                        {
                            foreach (var (decoration, decorationLine) in Decorations(fragment.Source))
                            {
                                var color = decoration["color"] as CssColor
                                    ?? throw new UnsupportedPaintException("Unresolved text decoration color.");
                                if (decorationLine == "underline")
                                {
                                    Add(new DrawUnderline(fragment.Run.FontIdentity, fragment.Run.FontSize,
                                        color, fragment.X, fragment.Baseline, fragment.Run.Width));
                                }
                                else
                                {
                                    Add(new DrawLineThrough(fragment.Run.FontIdentity, fragment.Run.FontSize,
                                        color, fragment.X, fragment.Baseline, fragment.Run.Width));
                                }
                            }
                        }
                    }
                }
                else { throw new InvalidOperationException("Unknown layout flow item."); }
            }
            if (children.Count != 0 || lines.Count != 0) { throw new InvalidOperationException("Layout flow omits a block or line."); }
            if (scrollClips is not null && scrollClips.TryGetValue(box, out _))
            {
                Add(new PopScrollClip());
            }
        }
        void DrawText(ShapedRun run, double x, double baseline, CssColor color)
        {
            var glyphs = new List<PaintGlyph>();
            foreach (var glyph in run.Glyphs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++glyphCount > limits.MaxGlyphs) { throw new PaintLimitException("Display glyph limit exceeded."); }
                if (glyph.GlyphId is 0 or > ushort.MaxValue) { throw new UnsupportedPaintException("Glyph ID is outside Skia's supported OpenType range."); }
                glyphs.Add(new((ushort)glyph.GlyphId, x + glyph.OffsetX, baseline + glyph.OffsetY));
                x += glyph.Advance;
            }
            if (glyphs.Count != 0) { Add(new DrawGlyphRun(run.FontIdentity, run.FontSize, color, glyphs, limits)); }
        }
        IEnumerable<(CssComputedStyle Style, string Line)> Decorations(DomText source)
        {
            var depth = 0;
            DomNode? current = source.ParentNode;
            while (current is DomElement element)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++depth > limits.MaxDepth) { throw new PaintLimitException("Text decoration ancestry limit exceeded."); }
                var style = styles.Styles.TryGetValue(element, out var computed) ? computed
                    : throw new InvalidOperationException("Paint styles are incomplete or stale.");
                if (style["text-decoration-line"] is CssKeyword { Value: "underline" or "line-through" } line)
                { yield return (style, line.Value); }
                current = element.ParentNode;
            }
        }
    }
}
