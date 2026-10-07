using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;

namespace VisualWeb.Engine.Paint;

public sealed class PaintLimitException(string message) : Exception(message);
public sealed class UnsupportedPaintException(string message) : Exception(message);

public sealed record PaintOptions
{
    public const double MaxDocumentHeight = 10_000_000;
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

/// <summary>Generate static backgrounds, physical solid borders and positioned glyph runs.</summary>
/// <remarks>Specs: css2-paint/css-backgrounds;
/// <see href="https://www.w3.org/TR/CSS22/zindex.html">normal-flow painting</see>,
/// <see href="https://www.w3.org/TR/css-backgrounds-3/#special-backgrounds">canvas backgrounds</see>.</remarks>
public static class DisplayListBuilder
{
    public static DisplayList Build(LayoutResult layout, CssStyleResult styles, PaintOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(styles);
        var limits = options ?? new();
        limits.Validate();
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
                Add(new FillRectangle(new(0, 0, layout.ViewportWidth,
                Math.Max(layout.ViewportHeight, root.BorderBox.Y + root.BorderBox.Height)), rootColor));
            }
            Background(root, 1);
            Text(root, 1);

            void Background(LayoutBox box, int depth)
            {
                Check(depth);
                if (box != root && box != propagated && Color(box, "background-color") is { Alpha: > 0 } background)
                {
                    Add(new FillRectangle(box.BorderBox, background));
                }
                Border(box);
                foreach (var child in box.Children) { Background(child, depth + 1); }
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
            var b = box.BorderBox;
            var p = box.PaddingBox;
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
            foreach (var (side, bounds, _) in visible)
            {
                if (style["border-" + side + "-style"] is not CssKeyword { Value: "solid" })
                {
                    throw new UnsupportedPaintException("Only solid visible borders are supported.");
                }
                var color = Color(box, "border-" + side + "-color");
                if (borderColor is not null && color != borderColor)
                {
                    throw new UnsupportedPaintException("Different side colors require deferred border-corner joins.");
                }
                borderColor = color;
                if (bounds.Width > 0 && bounds.Height > 0) { Add(new FillRectangle(bounds, color)); }
            }
        }
        void Text(LayoutBox box, int depth)
        {
            Check(depth);
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
                    foreach (var fragment in line.Line.Fragments)
                    {
                        var run = fragment.Run;
                        var glyphs = new List<PaintGlyph>();
                        var x = fragment.X;
                        foreach (var glyph in run.Glyphs)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (++glyphCount > limits.MaxGlyphs) { throw new PaintLimitException("Display glyph limit exceeded."); }
                            if (glyph.GlyphId is 0 or > ushort.MaxValue) { throw new UnsupportedPaintException("Glyph ID is outside Skia's supported OpenType range."); }
                            glyphs.Add(new((ushort)glyph.GlyphId, x + glyph.OffsetX, fragment.Baseline + glyph.OffsetY));
                            x += glyph.Advance;
                        }
                        if (glyphs.Count != 0)
                        {
                            Add(new DrawGlyphRun(run.FontIdentity, run.FontSize,
                                fragment.Style["color"] as CssColor ?? throw new UnsupportedPaintException("Unresolved text color."),
                                glyphs, limits));
                        }
                    }
                }
                else { throw new InvalidOperationException("Unknown layout flow item."); }
            }
            if (children.Count != 0 || lines.Count != 0) { throw new InvalidOperationException("Layout flow omits a block or line."); }
        }
    }
}
