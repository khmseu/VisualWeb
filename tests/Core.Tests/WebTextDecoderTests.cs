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
        var multiByteDecoder = new WebTextDecoder("gbk");

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

    [Theory]
    [InlineData("utf-16le", new byte[] { 0x3D, 0xD8, 0x00 }, new byte[] { 0xDE })]
    [InlineData("utf-16be", new byte[] { 0xD8, 0x3D, 0xDE }, new byte[] { 0x00 })]
    public void StreamingUtf16RetainsSplitCodeUnitsAndSurrogatePairs(string label, byte[] firstChunk, byte[] secondChunk)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(firstChunk, stream: true));
        Assert.Equal("\U0001F600", decoder.Decode(secondChunk));
    }

    [Theory]
    [InlineData("utf-16le", new byte[] { 0xFF }, new byte[] { 0xFE, 0x41, 0x00 })]
    [InlineData("utf-16be", new byte[] { 0xFE }, new byte[] { 0xFF, 0x00, 0x41 })]
    public void StreamingUtf16StripsBomSplitAcrossChunksUnlessIgnored(string label, byte[] firstChunk, byte[] secondChunk)
    {
        var decoder = new WebTextDecoder(label);
        var preserveBomDecoder = new WebTextDecoder(label, ignoreBom: true);

        Assert.Equal("", decoder.Decode(firstChunk, stream: true));
        Assert.Equal("A", decoder.Decode(secondChunk));
        Assert.Equal("", preserveBomDecoder.Decode(firstChunk, stream: true));
        Assert.Equal("\uFEFFA", preserveBomDecoder.Decode(secondChunk));
    }

    [Theory]
    [InlineData("utf-16le", new byte[] { 0x00, 0xD8 })]
    [InlineData("utf-16be", new byte[] { 0xD8, 0x00 })]
    public void StreamingUtf16FlushesIncompleteSurrogateAndResets(string label, byte[] highSurrogate)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(highSurrogate, stream: true));
        Assert.Equal("\uFFFD", decoder.Decode());
        Assert.Equal("A", decoder.Decode(label == "utf-16le" ? [0x41, 0x00] : [0x00, 0x41]));
    }

    [Fact]
    public void StreamingUtf16FatalFlushResetsDecoderState()
    {
        var decoder = new WebTextDecoder("utf-16le", fatal: true);

        Assert.Equal("", decoder.Decode([0x00, 0xD8], stream: true));
        Assert.Throws<WebDecodingException>(() => decoder.Decode());
        Assert.Equal("A", decoder.Decode([0x41, 0x00]));
    }

    [Theory]
    [InlineData("utf-16le", new byte[] { 0x00, 0xD8, 0x41, 0x00 })]
    [InlineData("utf-16be", new byte[] { 0xD8, 0x00, 0x00, 0x41 })]
    public void StreamingUtf16ReprocessesCodeUnitAfterUnmatchedHighSurrogate(string label, byte[] input)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("\uFFFDA", decoder.Decode(input, stream: true));
        Assert.Equal("", decoder.Decode());
    }

    [Theory]
    [InlineData("utf-16le", new byte[] { 0x00, 0xD8, 0x41 })]
    [InlineData("utf-16be", new byte[] { 0xD8, 0x00, 0x00 })]
    public void StreamingUtf16FlushesCombinedPendingSurrogateAndByteAsOneError(string label, byte[] input)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(input, stream: true));
        Assert.Equal("\uFFFD", decoder.Decode());
    }

    [Theory]
    [InlineData("Big5", new byte[] { 0xA4 }, new byte[] { 0x40 }, "\u4E00")]
    [InlineData("Big5", new byte[] { 0x88 }, new byte[] { 0x62 }, "\u00CA\u0304")]
    [InlineData("Shift_JIS", new byte[] { 0x82 }, new byte[] { 0xA0 }, "\u3042")]
    [InlineData("EUC-KR", new byte[] { 0xB0 }, new byte[] { 0xA1 }, "\uAC00")]
    public void StreamingLegacyPairEncodingsRetainLeadBytes(string label, byte[] lead, byte[] trail, string expected)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Equal(expected, decoder.Decode(trail));
    }

    [Theory]
    [InlineData("Big5", new byte[] { 0x81 }, new byte[] { 0x40 }, "\uFFFD@")]
    [InlineData("Shift_JIS", new byte[] { 0x82 }, new byte[] { 0x20 }, "\uFFFD ")]
    [InlineData("EUC-KR", new byte[] { 0x81 }, new byte[] { 0x20 }, "\uFFFD ")]
    public void StreamingLegacyPairEncodingsReprocessInvalidAsciiTrail(string label, byte[] lead, byte[] trail, string expected)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Equal(expected, decoder.Decode(trail));
    }

    [Theory]
    [InlineData("Big5", new byte[] { 0x81 }, new byte[] { 0xFF })]
    [InlineData("Shift_JIS", new byte[] { 0x82 }, new byte[] { 0xFD })]
    [InlineData("EUC-KR", new byte[] { 0x81 }, new byte[] { 0xFF })]
    public void StreamingLegacyPairEncodingsConsumeInvalidNonAsciiTrail(string label, byte[] lead, byte[] trail)
    {
        var decoder = new WebTextDecoder(label);

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Equal("\uFFFD", decoder.Decode(trail));
    }

    [Theory]
    [InlineData("Big5", new byte[] { 0x81 }, new byte[] { 0x40 })]
    [InlineData("Shift_JIS", new byte[] { 0x82 }, new byte[] { 0x20 })]
    [InlineData("EUC-KR", new byte[] { 0x81 }, new byte[] { 0x20 })]
    public void StreamingLegacyPairFatalErrorsResetDecoderState(string label, byte[] lead, byte[] invalidTrail)
    {
        var decoder = new WebTextDecoder(label, fatal: true);

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Throws<WebDecodingException>(() => decoder.Decode(invalidTrail));
        Assert.Equal("A", decoder.Decode([0x41]));
    }

    [Theory]
    [InlineData("Big5", new byte[] { 0x81 })]
    [InlineData("Shift_JIS", new byte[] { 0x82 })]
    [InlineData("EUC-KR", new byte[] { 0x81 })]
    public void StreamingLegacyPairFinalFlushesIncompleteLeadAndResets(string label, byte[] lead)
    {
        var decoder = new WebTextDecoder(label);
        var fatalDecoder = new WebTextDecoder(label, fatal: true);

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Equal("\uFFFD", decoder.Decode());
        Assert.Equal("A", decoder.Decode([0x41]));
        Assert.Equal("", fatalDecoder.Decode(lead, stream: true));
        Assert.Throws<WebDecodingException>(() => fatalDecoder.Decode());
        Assert.Equal("A", fatalDecoder.Decode([0x41]));
    }

    [Fact]
    public void StreamingEucJpRetainsTwoAndThreeByteSequencesAcrossChunks()
    {
        var decoder = new WebTextDecoder("EUC-JP");

        Assert.Equal("", decoder.Decode([0xA4], stream: true));
        Assert.Equal("\u3042", decoder.Decode([0xA2], stream: true));
        Assert.Equal("", decoder.Decode([0x8F], stream: true));
        Assert.Equal("", decoder.Decode([0xA2], stream: true));
        Assert.Equal("\u02D8", decoder.Decode([0xAF]));
    }

    [Theory]
    [InlineData(new byte[] { 0x8E }, new byte[] { 0xA6 }, "\uFF66")]
    [InlineData(new byte[] { 0xA4 }, new byte[] { 0x41 }, "\uFFFDA")]
    [InlineData(new byte[] { 0xA4 }, new byte[] { 0xFF }, "\uFFFD")]
    public void StreamingEucJpPreservesSpecialMappingsAndInvalidTrailRules(byte[] lead, byte[] trail, string expected)
    {
        var decoder = new WebTextDecoder("EUC-JP");

        Assert.Equal("", decoder.Decode(lead, stream: true));
        Assert.Equal(expected, decoder.Decode(trail));
    }

    [Fact]
    public void StreamingEucJpFinalFlushResetsIncompleteJis0212State()
    {
        var decoder = new WebTextDecoder("EUC-JP");
        var fatalDecoder = new WebTextDecoder("EUC-JP", fatal: true);

        Assert.Equal("", decoder.Decode([0x8F, 0xA2], stream: true));
        Assert.Equal("\uFFFD", decoder.Decode());
        Assert.Equal("A", decoder.Decode([0x41]));
        Assert.Equal("", fatalDecoder.Decode([0x8F, 0xA2], stream: true));
        Assert.Throws<WebDecodingException>(() => fatalDecoder.Decode());
        Assert.Equal("A", fatalDecoder.Decode([0x41]));
    }
}
