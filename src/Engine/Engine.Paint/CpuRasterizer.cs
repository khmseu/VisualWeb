using System.Runtime.InteropServices;
using SkiaSharp;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Text;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Engine.Paint;

/// <summary>Explicit paint resources loaded from the exact bytes used by the text shaper.</summary>
/// <remarks>Reference: skia-canvas; <see href="https://api.skia.org/classSkCanvas.html">Skia</see>.
/// Registry and native typefaces stay on their creating renderer thread.</remarks>
public sealed class PaintFontRegistry : IDisposable
{
    private readonly Dictionary<string, SKTypeface> fonts = new(StringComparer.Ordinal);
    private readonly PaintOptions options;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private long bytes;
    private bool disposed;
    public PaintFontRegistry(PaintOptions? options = null)
    {
        this.options = options ?? new();
        this.options.Validate();
    }
    public void Register(TextFont font)
    {
        Check();
        ArgumentNullException.ThrowIfNull(font);
        if (fonts.ContainsKey(font.Identity)) { throw new ArgumentException("Font identity already registered.", nameof(font)); }
        if (fonts.Count >= options.MaxFonts) { throw new PaintLimitException("Paint font count limit exceeded."); }
        if (font.FontDataLength > options.MaxFontBytes || bytes + font.FontDataLength > options.MaxTotalFontBytes)
        {
            throw new PaintLimitException("Paint font byte limit exceeded.");
        }
        var copy = font.CopyFontData();
        using var data = SKData.CreateCopy(copy);
        var typeface = SKTypeface.FromData(data, font.FaceIndex) ?? throw new FontLoadException("Skia rejected the registered shaping font.");
        fonts.Add(font.Identity, typeface);
        bytes += copy.Length;
    }
    internal SKTypeface Resolve(string identity)
    {
        Check();
        return fonts.TryGetValue(identity, out var font) ? font
            : throw new UnsupportedPaintException("Glyph run refers to an unregistered font resource.");
    }
    internal void ValidateAccess() => Check();
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread != Environment.CurrentManagedThreadId) { throw new InvalidOperationException("Paint font resources must stay on their creating thread."); }
    }
    public void Dispose()
    {
        if (disposed) { return; }
        Check();
        foreach (var font in fonts.Values) { font.Dispose(); }
        fonts.Clear();
        disposed = true;
    }
}

/// <summary>Owned opaque BGRA32 pixels with explicit physical dimensions and stride.</summary>
/// <remarks>Reference: sdl-window-surface; portable presentation uses IPixelSurface.</remarks>
public sealed class RasterFrame
{
    private readonly byte[] pixels;
    public PixelSize Size { get; }
    public int Stride { get; }
    public ReadOnlyMemory<byte> Pixels => pixels;
    internal RasterFrame(byte[] pixels, PixelSize size, int stride) { this.pixels = pixels; Size = size; Stride = stride; }
    /// <summary>Copy a validated opaque, tightly packed BGRA frame received from an external producer.</summary>
    public static RasterFrame CopyFrom(ReadOnlySpan<byte> pixels, PixelSize size, int stride, int maxPixels = 4_194_304)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPixels);
        PixelBuffer.Validate(pixels.Length, size, stride);
        if ((long)size.Width * size.Height > maxPixels || (long)size.Width * 4 != stride
            || (long)stride * size.Height != pixels.Length)
        {
            throw new PaintLimitException("External framebuffer dimensions, byte count or stride exceed the exact packed contract.");
        }
        for (var index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] != 255) { throw new ArgumentException("External framebuffer must be opaque.", nameof(pixels)); }
        }
        return new(pixels.ToArray(), size, stride);
    }
    public void Present(IPixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        surface.Present(pixels, Size, Stride);
    }
}

/// <summary>CPU Skia rendering, with explicit CSS-to-physical scale, viewport clipping and opaque backdrop.</summary>
/// <remarks>Reference: skia-canvas; <see href="https://api.skia.org/classSkCanvas.html">canvas API</see>.
/// No GPU, OS window, resource loading, CSS compositing or font fallback.</remarks>
public static class CpuRasterizer
{
    public static RasterFrame Render(DisplayList list, PaintFontRegistry fonts, double scale = 1,
        CssColor? backdrop = null, PaintOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(fonts);
        fonts.ValidateAccess();
        var limits = options ?? new();
        limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(scale) || scale <= 0) { throw new ArgumentOutOfRangeException(nameof(scale)); }
        var clear = backdrop ?? new CssColor(255, 255, 255);
        if (clear.Alpha != 255) { throw new ArgumentException("Raster backdrop must be opaque.", nameof(backdrop)); }
        var width = Math.Ceiling(list.Width * scale);
        var height = Math.Ceiling(list.Height * scale);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width > int.MaxValue || height > int.MaxValue
            || width * height > limits.MaxPixels || width * height > int.MaxValue / 4)
        {
            throw new PaintLimitException("Raster pixel/byte limit exceeded.");
        }
        var size = new PixelSize((int)width, (int)height);
        if (size.Width == 0 || size.Height == 0) { throw new PaintLimitException("Raster scale underflows the physical viewport."); }
        var stride = checked(size.Width * 4);
        var deviceScale = Float(scale);
        var viewport = new SKRect(0, 0, Float(list.Width), Float(list.Height));
        long glyphCount = 0;
        if (list.Commands.Count > limits.MaxCommands) { throw new PaintLimitException("Raster command limit exceeded."); }
        // Validate every command/resource before allocating a framebuffer or drawing partial output.
        foreach (var command in list.Commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (command)
            {
                case FillRectangle fill:
                    ArgumentNullException.ThrowIfNull(fill.Color);
                    Rect(fill.Bounds);
                    break;
                case DrawGlyphRun run:
                    var typeface = fonts.Resolve(run.FontIdentity);
                    if (run.FontSize < 0 || run.FontSize > 4096) { throw new ArgumentOutOfRangeException(nameof(list), "Invalid glyph font size."); }
                    Float(run.FontSize);
                    foreach (var glyph in run.Glyphs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (++glyphCount > limits.MaxGlyphs) { throw new PaintLimitException("Raster glyph limit exceeded."); }
                        if (glyph.Id == 0 || glyph.Id >= typeface.GlyphCount) { throw new UnsupportedPaintException("Glyph is absent from the registered font."); }
                        Float(glyph.X); Float(glyph.Y);
                    }
                    break;
                default: throw new UnsupportedPaintException("Unsupported paint command.");
            }
        }
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        if (bitmap.GetPixels() == IntPtr.Zero) { throw new PaintLimitException("Skia could not allocate the raster framebuffer."); }
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Color(clear));
        canvas.Scale(deviceScale);
        canvas.ClipRect(viewport);
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        foreach (var command in list.Commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (command is FillRectangle fill)
            {
                paint.Color = Color(fill.Color);
                paint.IsAntialias = false;
                canvas.DrawRect(Rect(fill.Bounds), paint);
            }
            else if (command is DrawGlyphRun run && run.Glyphs.Count > 0)
            {
                using var font = new SKFont(fonts.Resolve(run.FontIdentity), Float(Math.Round(run.FontSize * 64, MidpointRounding.AwayFromZero) / 64))
                {
                    Edging = SKFontEdging.Antialias,
                    Subpixel = true,
                    Hinting = SKFontHinting.None
                };
                using var builder = new SKTextBlobBuilder();
                builder.AddPositionedRun(run.Glyphs.Select(g => g.Id).ToArray(), font,
                    run.Glyphs.Select(g => new SKPoint(Float(g.X), Float(g.Y))).ToArray());
                using var blob = builder.Build() ?? throw new InvalidOperationException("Skia could not build the glyph blob.");
                paint.Color = Color(run.Color);
                paint.IsAntialias = true;
                canvas.DrawText(blob, 0, 0, paint);
            }
        }
        canvas.Flush();
        cancellationToken.ThrowIfCancellationRequested();
        var pixels = new byte[checked(stride * size.Height)];
        for (var row = 0; row < size.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Marshal.Copy(IntPtr.Add(bitmap.GetPixels(), checked(row * bitmap.RowBytes)), pixels, row * stride, stride);
        }
        return new(pixels, size, stride);
    }
    private static SKColor Color(CssColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);
    private static float Float(double value)
    {
        if (!double.IsFinite(value) || value is < -1e9 or > 1e9)
        {
            throw new PaintLimitException("Paint coordinate exceeds the supported finite range.");
        }
        var result = (float)value;
        if (value != 0 && result == 0) { throw new PaintLimitException("Paint coordinate underflows Skia's numeric range."); }
        return result;
    }
    private static SKRect Rect(LayoutRect rect)
    {
        if (rect.Width < 0 || rect.Height < 0) { throw new ArgumentException("Paint rectangle sizes cannot be negative."); }
        return new(Float(rect.X), Float(rect.Y), Float(rect.X + rect.Width), Float(rect.Y + rect.Height));
    }
}
