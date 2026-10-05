using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HarfBuzzSharp;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Engine.Text;

public sealed class TextLimitException(string message) : Exception(message);
public sealed class UnsupportedTextException(string message) : Exception(message);
public sealed class FontLoadException(string message) : Exception(message);

public sealed record TextOptions
{
    public int MaxFontBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxRunCharacters { get; init; } = 65_536;
    public int MaxGlyphs { get; init; } = 65_536;
    public double MaxFontSize { get; init; } = 4096;
    internal void Validate()
    {
        if (MaxFontBytes <= 0 || MaxRunCharacters <= 0 || MaxGlyphs <= 0
            || !double.IsFinite(MaxFontSize) || MaxFontSize <= 0 || MaxFontSize > int.MaxValue / 64.0)
        {
            throw new ArgumentOutOfRangeException(nameof(TextOptions), "Text limits must be positive and native-scale-safe.");
        }
    }
}

/// <summary>Horizontal font extents and shaped placements in CSS pixels, with UTF-16 clusters.</summary>
/// <remarks>Reference: harfbuzz-font; <see href="https://harfbuzz.github.io/harfbuzz-hb-font.html#hb-font-get-h-extents">horizontal extents</see>.
/// Glyph offsets use a screen coordinate system: positive y points down.</remarks>
public sealed record TextMetrics(double Ascent, double Descent, double LineGap);
public sealed record ShapedGlyph(uint GlyphId, int Cluster, double Advance, double OffsetX, double OffsetY);
public sealed record ShapedRun(string Text, string FontIdentity, double FontSize,
    IReadOnlyList<ShapedGlyph> Glyphs, double Width, TextMetrics Metrics);
public sealed record TextFontRequest(IReadOnlyList<string> Families, double Size, double Weight = 400, string Style = "normal");

/// <summary>Injectable text measurement/shaping boundary for deterministic layout tests.</summary>
/// <remarks>Spec: css-fonts; <see href="https://www.w3.org/TR/css-fonts-4/#font-matching-algorithm">font matching</see>.
/// This subset requires configured faces and has no system fallback or synthetic styles.</remarks>
public interface ITextShaper
{
    ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Owned OpenType face; native objects never escape the renderer-local wrapper.</summary>
/// <remarks>Reference: harfbuzz-font; <see href="https://harfbuzz.github.io/harfbuzz-hb-font.html">font API</see>.
/// Loading font bytes in-process is not a native-code sandbox.</remarks>
public sealed class TextFont : IDisposable
{
    private readonly Blob blob;
    private readonly Face face;
    private readonly TextOptions options;
    private readonly byte[] fontData;
    public int FaceIndex { get; }
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool disposed;
    public string Identity { get; }
    public int FontDataLength { get { Check(); return fontData.Length; } }

    public TextFont(string path, int faceIndex = 0, TextOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.options = options ?? new();
        this.options.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);
        using var stream = File.OpenRead(path);
        if (stream.Length > this.options.MaxFontBytes) { throw new TextLimitException("Font byte limit exceeded."); }
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        fontData = bytes;
        FaceIndex = faceIndex;
        if (stream.ReadByte() != -1) { throw new TextLimitException("Font grew beyond the bounded read."); }
        if (bytes.Length == 0) { throw new FontLoadException("Font file is empty."); }
        // FromStream uses a temporary managed pin with ReadOnly ownership; duplicate into native storage instead.
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { blob = new Blob(pin.AddrOfPinnedObject(), bytes.Length, MemoryMode.Duplicate); }
        finally { pin.Free(); }
        var loaded = false;
        try
        {
            if (faceIndex >= blob.FaceCount) { throw new FontLoadException("Font face index is absent or the file is not a supported font."); }
            face = new Face(blob, faceIndex);
            if (face.GlyphCount == 0 || face.UnitsPerEm <= 0)
            {
                face.Dispose();
                throw new FontLoadException("Font has no usable glyphs or units-per-em.");
            }
            face.MakeImmutable();
            loaded = true;
        }
        finally
        {
            if (!loaded) { blob.Dispose(); }
        }
        Identity = "sha256:" + Convert.ToHexString(SHA256.HashData(fontData)) + "#" + faceIndex;
    }

    /// <summary>Copy the exact loaded font bytes for an explicitly configured paint font resource.</summary>
    /// <remarks>The recipient owns its copy; this does not reopen a path or share native handles.</remarks>
    public byte[] CopyFontData()
    {
        Check();
        return fontData.ToArray();
    }

    /// <summary>Shape a bounded Latin/LTR run with real OpenType substitutions and positioning.</summary>
    /// <remarks>Reference: harfbuzz-shaping;
    /// <see href="https://harfbuzz.github.io/harfbuzz-hb-shape.html#hb-shape">hb_shape</see>.</remarks>
    public ShapedRun Shape(string text, double size, CancellationToken cancellationToken = default)
    {
        Check();
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(size) || size < 0) { throw new ArgumentOutOfRangeException(nameof(size)); }
        if (size > options.MaxFontSize || text.Length > options.MaxRunCharacters) { throw new TextLimitException("Text run/size limit exceeded."); }
        ValidateLatin(text, cancellationToken);
        using var font = new Font(face);
        font.SetFunctionsOpenType();
        var scale = checked((int)Math.Round(size * 64, MidpointRounding.AwayFromZero));
        font.SetScale(scale, scale);
        if (!font.TryGetHorizontalFontExtents(out var extents)) { throw new FontLoadException("Font has no horizontal metrics."); }
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16(text);
        buffer.Direction = Direction.LeftToRight;
        buffer.Script = Script.Latin;
        buffer.Language = new Language("en");
        foreach (var rune in text.EnumerateRunes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!font.TryGetNominalGlyph(rune.Value, out _))
            {
                throw new UnsupportedTextException($"Configured font lacks U+{rune.Value:X}; fallback is not implemented.");
            }
        }
        font.Shape(buffer);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length > options.MaxGlyphs) { throw new TextLimitException("Shaped glyph limit exceeded."); }
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        var glyphs = new List<ShapedGlyph>(infos.Length);
        var width = 0.0;
        for (var index = 0; index < infos.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var advance = positions[index].XAdvance / 64.0;
            if (infos[index].Codepoint == 0) { throw new UnsupportedTextException("Shaping produced a missing glyph."); }
            glyphs.Add(new(infos[index].Codepoint, checked((int)infos[index].Cluster), advance,
                positions[index].XOffset / 64.0, -positions[index].YOffset / 64.0));
            width += advance;
        }
        return new(text, Identity, size, glyphs.AsReadOnly(), width,
            new(extents.Ascender / 64.0, -extents.Descender / 64.0, extents.LineGap / 64.0));
    }

    /// <summary>Reject text needing script itemization, bidi, control handling or font fallback.</summary>
    public static void ValidateLatin(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var index = 0; index < text.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Rune.TryGetRuneAt(text, index, out var rune)) { throw new UnsupportedTextException("Text contains a lone UTF-16 surrogate."); }
            var value = rune.Value;
            if (!(value is >= 0x20 and <= 0x7E or >= 0xA0 and <= 0x24F or >= 0x300 and <= 0x36F
                or >= 0x2010 and <= 0x2027 or >= 0x2030 and <= 0x205E or >= 0xFB00 and <= 0xFB06)
                || value is 0xAD or 0x2028 or 0x2029)
            {
                throw new UnsupportedTextException($"U+{value:X} requires unsupported text/script/bidi handling.");
            }
            index += rune.Utf16SequenceLength;
        }
    }

    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread != Environment.CurrentManagedThreadId) { throw new InvalidOperationException("Font access must remain on its creating renderer thread."); }
    }
    public void Dispose()
    {
        if (disposed) { return; }
        Check();
        disposed = true;
        face.Dispose();
        blob.Dispose();
    }
}

/// <summary>Explicit family/face registration using only portable OS font enumeration.</summary>
/// <remarks>Spec: css-fonts; <see href="https://www.w3.org/TR/css-fonts-4/#font-matching-algorithm">font selection</see>.
/// Registered faces are borrowed; their owner disposes them after the shaper is no longer used.</remarks>
public sealed class FontSet : ITextShaper
{
    private readonly List<Entry> entries = [];
    public void Register(string family, TextFont font, double weight = 400, string style = "normal")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(font);
        if (!double.IsFinite(weight) || weight is < 1 or > 1000) { throw new ArgumentOutOfRangeException(nameof(weight)); }
        if (style is not ("normal" or "italic" or "oblique")) { throw new ArgumentOutOfRangeException(nameof(style)); }
        if (entries.Any(e => e.Family.Equals(family, StringComparison.OrdinalIgnoreCase) && e.Weight == weight && e.Style == style))
        {
            throw new ArgumentException("Family/weight/style already registered.", nameof(family));
        }
        entries.Add(new(family, font, weight, style));
    }

    public TextFont LoadFromCatalog(IFontCatalog catalog, string path, int faceIndex = 0, TextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (!catalog.GetFontFiles().Any(file => comparer.Equals(Path.GetFullPath(file), fullPath)))
        {
            throw new FontLoadException("Requested font is not present in the supplied OS font catalog.");
        }
        return new(fullPath, faceIndex, options);
    }

    public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Families);
        if (!double.IsFinite(request.Weight) || request.Weight is < 1 or > 1000) { throw new ArgumentOutOfRangeException(nameof(request)); }
        if (request.Style is not ("normal" or "italic" or "oblique") || !double.IsFinite(request.Size) || request.Size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var family in request.Families)
        {
            var entry = entries.FirstOrDefault(e => e.Family.Equals(family, StringComparison.OrdinalIgnoreCase)
                && e.Weight == request.Weight && e.Style == request.Style);
            if (entry is not null) { return entry.Font.Shape(text, request.Size, cancellationToken); }
        }
        throw new UnsupportedTextException("No configured exact family/weight/style face; CSS fallback and synthetic styles are deferred.");
    }
    private sealed record Entry(string Family, TextFont Font, double Weight, string Style);
}
