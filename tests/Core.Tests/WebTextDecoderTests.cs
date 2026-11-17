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
}
