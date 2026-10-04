using System.Security.Cryptography;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Engine.Text.Tests;

public sealed class TextTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void FontDigestAndNativeHorizontalMetricsArePinned()
    {
        Assert.Equal("BFB7BB691513F12E734DC346C03A03F784912432D7E3FA8E56EFCF906FE86B3D",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FontPath))));
        using var font = new TextFont(FontPath);
        var run = font.Shape("Hello", 16, Cancellation);
        Assert.Equal("Hello", run.Text);
        Assert.InRange(run.Width, 35, 45);
        Assert.Equal(17.109375, run.Metrics.Ascent);
        Assert.Equal(4.6875, run.Metrics.Descent);
        Assert.Equal(0, run.Metrics.LineGap);
        Assert.Equal(run.Glyphs.Sum(g => g.Advance), run.Width);
        Assert.Equal(5, run.Glyphs.Count);
        Assert.All(run.Glyphs, g => Assert.NotEqual(0u, g.GlyphId));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, run.Glyphs.Select(g => g.Cluster));
    }

    [Fact]
    public void LigaturesCombiningMarksAndKerningUseNativeShaping()
    {
        using var font = new TextFont(FontPath);
        var ffi = font.Shape("ffi", 20, Cancellation);
        Assert.Single(ffi.Glyphs);
        Assert.Equal(0, ffi.Glyphs[0].Cluster);
        var composed = font.Shape("a\u0301", 20, Cancellation);
        Assert.Single(composed.Glyphs);
        Assert.Equal(0, composed.Glyphs[0].Cluster);
        Assert.True(font.Shape("AV", 20, Cancellation).Width < font.Shape("A", 20, Cancellation).Width + font.Shape("V", 20, Cancellation).Width);
    }

    [Fact]
    public void SizeScaleIsLinearWithinNativeRoundingAndEmptyRunsHaveMetrics()
    {
        using var font = new TextFont(FontPath);
        var small = font.Shape("sample", 10, Cancellation);
        var large = font.Shape("sample", 20, Cancellation);
        Assert.InRange(Math.Abs(large.Width - small.Width * 2), 0, .1);
        var empty = font.Shape("", 16, Cancellation);
        Assert.Empty(empty.Glyphs);
        Assert.Equal(0, empty.Width);
        Assert.Equal(font.Shape("a", 16, Cancellation).Metrics, empty.Metrics);
        Assert.Equal(0, font.Shape("a", 0, Cancellation).Width);
    }

    [Theory]
    [InlineData("\u05D0")]
    [InlineData("\u0627")]
    [InlineData("\u4E00")]
    [InlineData("\u202E")]
    [InlineData("\u200D")]
    [InlineData("\u00AD")]
    [InlineData("\n")]
    [InlineData("\t")]
    public void UnsupportedScriptsBidiAndControlsFail(string text)
    {
        using var font = new TextFont(FontPath);
        Assert.Throws<UnsupportedTextException>(() => font.Shape(text, 16, Cancellation));
    }

    [Fact]
    public void FontRunGlyphAndSizeLimitsUseExactBoundaries()
    {
        var length = new FileInfo(FontPath).Length;
        Assert.Throws<TextLimitException>(() => new TextFont(FontPath, options: new() { MaxFontBytes = (int)length - 1 }));
        using var font = new TextFont(FontPath, options: new() { MaxFontBytes = (int)length, MaxRunCharacters = 1, MaxGlyphs = 1, MaxFontSize = 16 });
        Assert.Single(font.Shape("a", 16, Cancellation).Glyphs);
        Assert.Throws<TextLimitException>(() => font.Shape("aa", 16, Cancellation));
        Assert.Throws<TextLimitException>(() => font.Shape("a", 16.01, Cancellation));
        using var glyphLimited = new TextFont(FontPath, options: new() { MaxGlyphs = 1 });
        Assert.Throws<TextLimitException>(() => glyphLimited.Shape("ab", 16, Cancellation));
    }

    [Fact]
    public void InvalidFilesFacesAndOptionsAreNotSuccess()
    {
        Assert.Throws<FontLoadException>(() => new TextFont(FontPath, faceIndex: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextFont(FontPath, faceIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextFont(FontPath, options: new() { MaxFontBytes = 0 }));
        var invalid = Path.GetTempFileName();
        try { Assert.Throws<FontLoadException>(() => new TextFont(invalid)); }
        finally { File.Delete(invalid); }
        using var font = new TextFont(FontPath);
        Assert.Throws<ArgumentOutOfRangeException>(() => font.Shape("a", double.NaN, Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => font.Shape("a", -1, Cancellation));
        Assert.Throws<UnsupportedTextException>(() => font.Shape(new string('\uD800', 1), 16, Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => font.Shape("a", 16, canceled.Token));
    }

    [Fact]
    public void FontRegistrationAndPortableCatalogAreExplicit()
    {
        var set = new FontSet();
        using var font = set.LoadFromCatalog(new Catalog([FontPath]), FontPath);
        set.Register("Example", font);
        set.Register("serif", font);
        var request = new TextFontRequest(["missing", "Example"], 16);
        Assert.Equal(font.Shape("hello", 16, Cancellation).Width, set.Shape("hello", request, Cancellation).Width);
        Assert.Throws<UnsupportedTextException>(() => set.Shape("hello", request with { Weight = 700 }, Cancellation));
        Assert.Throws<UnsupportedTextException>(() => set.Shape("hello", request with { Style = "italic" }, Cancellation));
        Assert.Throws<FontLoadException>(() => set.LoadFromCatalog(new Catalog([]), FontPath));
        Assert.Throws<ArgumentException>(() => set.Register("example", font));
    }

    [Fact]
    public void DisposalIsIdempotentAndOwnershipRemainsExplicit()
    {
        var font = new TextFont(FontPath);
        font.Dispose();
        font.Dispose();
        Assert.Throws<ObjectDisposedException>(() => font.Shape("a", 16, Cancellation));
    }

    [Fact]
    public void FontBytesRemainNativeOwnedAcrossCompactingCollections()
    {
        using var font = new TextFont(FontPath);
        var expected = font.Shape("office AV", 16, Cancellation);
        for (var index = 0; index < 4; index++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            var actual = font.Shape("office AV", 16, Cancellation);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Glyphs, actual.Glyphs);
        }
    }

    [Fact]
    public void FontThreadAffinityFailsBeforeNativeAccess()
    {
        using var font = new TextFont(FontPath);
        Exception? failure = null;
        var cancellation = Cancellation;
        var thread = new Thread(() =>
        {
            try { font.Shape("a", 16, cancellation); }
            catch (InvalidOperationException exception) { failure = exception; }
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(failure);
    }
    private sealed class Catalog(IReadOnlyList<string> files) : IFontCatalog
    {
        public IReadOnlyList<string> GetFontFiles() => files;
    }
}
