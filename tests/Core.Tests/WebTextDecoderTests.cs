using VisualWeb.Core.Encoding;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class WebTextDecoderTests
{
    [Fact]
    public void DefaultsToUtf8AndDecodesEmptyInput()
    {
        var decoder = new WebTextDecoder();

        Assert.Equal("utf-8", decoder.Encoding);
        Assert.False(decoder.Fatal);
        Assert.False(decoder.IgnoreBom);
        Assert.Equal("", decoder.Decode());
    }

    [Fact]
    public void DecodesTheSelectedLegacyEncoding()
    {
        var decoder = new WebTextDecoder("windows-1252");

        Assert.Equal("windows-1252", decoder.Encoding);
        Assert.Equal("\u20AC", decoder.Decode([0x80]));
    }

    [Theory]
    [InlineData("unknown-encoding")]
    [InlineData("replacement")]
    public void RejectsUnknownAndReplacementEncodings(string label) =>
        Assert.Throws<ArgumentException>(() => new WebTextDecoder(label));

    [Fact]
    public void FatalOptionRejectsMalformedInput()
    {
        var decoder = new WebTextDecoder(fatal: true);

        Assert.True(decoder.Fatal);
        Assert.Throws<WebDecodingException>(() =>
        {
            decoder.Decode([0xFF]);
        });
    }

    [Fact]
    public void DefaultErrorModeReplacesMalformedInput()
    {
        var decoder = new WebTextDecoder();

        Assert.Equal("\uFFFD", decoder.Decode([0xFF]));
    }

    [Fact]
    public void StripsOneLeadingBomUnlessIgnoreBomIsSet()
    {
        var bytes = Convert.FromHexString("EFBBBFEFBBBF41");

        Assert.Equal("\uFEFFA", new WebTextDecoder().Decode(bytes));
        Assert.Equal("\uFEFF\uFEFFA", new WebTextDecoder(ignoreBom: true).Decode(bytes));
        Assert.Equal("A", new WebTextDecoder("utf-16").Decode(Convert.FromHexString("FFFE4100")));
        Assert.Equal("\uFEFFA", new WebTextDecoder("utf-16", ignoreBom: true).Decode(Convert.FromHexString("FFFE4100")));
    }

    [Fact]
    public void StreamingUtf8RetainsIncompleteScalarsUntilNextChunk()
    {
        var decoder = new WebTextDecoder();

        Assert.Equal("", decoder.Decode([0xF0, 0x9F], stream: true));
        Assert.Equal("\U0001F600", decoder.Decode([0x98, 0x80], stream: true));
        Assert.Equal("", decoder.Decode());
    }

    [Fact]
    public void StreamingUtf8FlushesIncompleteInputAndResetsForNextDecode()
    {
        var decoder = new WebTextDecoder();

        Assert.Equal("", decoder.Decode([0xE2], stream: true));
        Assert.Equal("\uFFFD", decoder.Decode());
        Assert.Equal("A", decoder.Decode([0x41]));
    }

    [Fact]
    public void StreamingUtf8ReplacesMalformedSequencesAndReprocessesFollowingBytes()
    {
        var decoder = new WebTextDecoder();

        Assert.Equal("\uFFFD(\uFFFD", decoder.Decode([0xE2, 0x28, 0xA1], stream: false));
    }

    [Fact]
    public void StreamingUtf8PreservesBomHandlingAcrossChunks()
    {
        var decoder = new WebTextDecoder();
        var preserveBomDecoder = new WebTextDecoder(ignoreBom: true);

        Assert.Equal("", decoder.Decode([0xEF, 0xBB], stream: true));
        Assert.Equal("A", decoder.Decode([0xBF, 0x41], stream: false));
        Assert.Equal("", preserveBomDecoder.Decode([0xEF, 0xBB], stream: true));
        Assert.Equal("\uFEFFA", preserveBomDecoder.Decode([0xBF, 0x41], stream: false));
    }

    [Fact]
    public void StreamingUtf8RejectsFatalErrorsAndUnsupportedEncodings()
    {
        var fatalDecoder = new WebTextDecoder(fatal: true);
        var multiByteDecoder = new WebTextDecoder("shift_jis");

        Assert.Equal("", fatalDecoder.Decode([0xE2], stream: true));
        Assert.Throws<WebDecodingException>(() => fatalDecoder.Decode());
        Assert.Equal("A", fatalDecoder.Decode([0x41]));
        Assert.Throws<NotSupportedException>(() => multiByteDecoder.Decode([0x41], stream: true));
        Assert.Equal("A", multiByteDecoder.Decode([0x41]));
    }

    [Fact]
    public void StreamingSingleByteEncodingDecodesChunksAndFlushes()
    {
        var decoder = new WebTextDecoder("windows-1252");

        Assert.Equal("\u20AC", decoder.Decode([0x80], stream: true));
        Assert.Equal("A", decoder.Decode([0x41], stream: true));
        Assert.Equal("", decoder.Decode());
        Assert.Equal("\u20AC", decoder.Decode([0x80]));
    }

    [Fact]
    public void StreamingSingleByteFatalErrorsResetDecoderState()
    {
        var decoder = new WebTextDecoder("iso-8859-3", fatal: true);

        Assert.Throws<WebDecodingException>(() => decoder.Decode([0xA5], stream: true));
        Assert.Equal("A", decoder.Decode([0x41]));
    }
}
