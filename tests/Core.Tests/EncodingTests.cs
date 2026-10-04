using System.Text.Json;
using System.Text.RegularExpressions;
using VisualWeb.Core.Encoding;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class EncodingTests
{
    public static IEnumerable<object[]> Iso2022Vectors()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "iso-2022-jp-decoder.any.js"));
        foreach (Match match in Regex.Matches(source, """^decode\(\[([^\]]*)\], "((?:\\.|[^"])*)", "([^"]*)"\)""", RegexOptions.Multiline))
        {
            var bytes = match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(hex => Convert.ToByte(hex[2..], 16)).ToArray();
            yield return [bytes, Regex.Unescape(match.Groups[2].Value), match.Groups[3].Value];
        }
    }

    [Theory]
    [MemberData(nameof(Iso2022Vectors))]
    public void OfficialIso2022JpVectors(byte[] bytes, string expected, string description)
    {
        Assert.NotEmpty(description);
        Assert.Equal(expected, WebEncoding.ForLabel("ISO-2022-JP").Decode(bytes));
    }

    [Fact]
    public void EveryOfficialLabelResolvesToItsCanonicalEncoding()
    {
        using var labels = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "encodings.json")));
        foreach (var group in labels.RootElement.EnumerateArray())
        {
            foreach (var entry in group.GetProperty("encodings").EnumerateArray())
            {
                var expected = entry.GetProperty("name").GetString();
                foreach (var label in entry.GetProperty("labels").EnumerateArray())
                {
                    Assert.Equal(expected, WebEncoding.ForLabel("\t " + label.GetString()!.ToUpperInvariant() + "\r\n").Name);
                }
            }
        }
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData(" utf-32 ")]
    [InlineData("\u00a0utf-8")]
    public void UnknownLabelsAreExplicitFailures(string label) =>
        Assert.Throws<ArgumentException>(() => WebEncoding.ForLabel(label));

    [Theory]
    [InlineData("UTF-8", "F09F9880", "\U0001F600")]
    [InlineData("UTF-8", "E228A1", "\uFFFD(\uFFFD")]
    [InlineData("UTF-8", "E282", "\uFFFD")]
    [InlineData("UTF-16LE", "3DD800DE", "\U0001F600")]
    [InlineData("UTF-16BE", "D83DDE00", "\U0001F600")]
    [InlineData("UTF-16LE", "00D84100", "\uFFFDA")]
    [InlineData("UTF-16LE", "410042", "A\uFFFD")]
    [InlineData("UTF-16LE", "00D841", "\uFFFD")]
    [InlineData("UTF-16BE", "D80041", "\uFFFD")]
    [InlineData("windows-1252", "8041", "\u20ACA")]
    [InlineData("x-user-defined", "807FFF", "\uF780\u007F\uF7FF")]
    [InlineData("replacement", "4142", "\uFFFD")]
    [InlineData("Shift_JIS", "82A0", "\u3042")]
    [InlineData("EUC-JP", "A4A2", "\u3042")]
    [InlineData("ISO-2022-JP", "1B244224221B2842", "\u3042")]
    [InlineData("Big5", "A440", "\u4E00")]
    [InlineData("Big5", "8140", "\uFFFD@")]
    [InlineData("Big5", "878740", "\uFFFD@")]
    [InlineData("Big5", "8862", "\u00CA\u0304")]
    [InlineData("GBK", "813541", "\uFFFD5A")]
    [InlineData("gb18030", "81308141", "\uFFFD0\u4E04")]
    [InlineData("GBK", "D6D0", "\u4E2D")]
    [InlineData("gb18030", "9439FC36", "\U0001F600")]
    [InlineData("EUC-KR", "B0A1", "\uAC00")]
    public void DecodesUnicodeAndLegacyEncodings(string label, string hex, string expected) =>
        Assert.Equal(expected, WebEncoding.ForLabel(label).Decode(Convert.FromHexString(hex)));

    [Fact]
    public void BomSniffingOverridesFallbackAndStripsOnlyOneBom()
    {
        var result = WebEncoding.DecodeWithBom(Convert.FromHexString("EFBBBFEFBBBF41"), WebEncoding.ForLabel("windows-1252"));
        Assert.Equal("UTF-8", result.Encoding.Name);
        Assert.Equal("\uFEFFA", result.Text);
        var opposite = WebEncoding.DecodeWithBom(Convert.FromHexString("FEFF0041"), WebEncoding.ForLabel("UTF-16LE"));
        Assert.Equal("UTF-16BE", opposite.Encoding.Name);
        Assert.Equal("A", opposite.Text);
    }

    [Fact]
    public void FatalErrorsThrowInsteadOfReplacing() =>
        Assert.Throws<WebDecodingException>(() => WebEncoding.ForLabel("UTF-8").Decode([0xFF], fatal: true));

    [Theory]
    [InlineData("Big5", "81")]
    [InlineData("Shift_JIS", "82")]
    [InlineData("EUC-KR", "81")]
    [InlineData("EUC-JP", "8F")]
    [InlineData("gb18030", "813081")]
    [InlineData("ISO-2022-JP", "1B24")]
    [InlineData("UTF-16LE", "00D8")]
    [InlineData("replacement", "41")]
    public void IncompleteOrInvalidLegacyInputSupportsFatalMode(string label, string hex)
    {
        var encoding = WebEncoding.ForLabel(label);
        var bytes = Convert.FromHexString(hex);
        Assert.Contains("\uFFFD", encoding.Decode(bytes));
        Assert.Throws<WebDecodingException>(() => encoding.Decode(bytes, fatal: true));
    }

    [Fact]
    public void RawDecodingPreservesBomAndEmptyReplacementInput()
    {
        Assert.Equal("\uFEFFA", WebEncoding.ForLabel("UTF-8").Decode(Convert.FromHexString("EFBBBF41")));
        Assert.Equal("", WebEncoding.ForLabel("replacement").Decode([]));
    }

    [Fact]
    public void Gb18030FourByteRangeBoundariesMatchOfficialIndex()
    {
        using var indexes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "indexes.json")));
        var ranges = indexes.RootElement.GetProperty("gb18030-ranges").EnumerateArray()
            .Select(entry => (Pointer: entry[0].GetInt32(), CodePoint: entry[1].GetInt32())).ToArray();
        var encoding = WebEncoding.ForLabel("gb18030");
        for (var i = 0; i < ranges.Length; i++)
        {
            Check(ranges[i].Pointer, ranges[i].CodePoint);
            var last = i + 1 < ranges.Length ? Math.Min(ranges[i + 1].Pointer - 1, 39419) : 1237575;
            if (last >= ranges[i].Pointer)
            {
                Check(last, ranges[i].CodePoint + last - ranges[i].Pointer);
            }
        }

        Check(7457, 0xE7C7);
        foreach (var pointer in new[] { 39420, 188999, 1237576, 126 * 10 * 126 * 10 - 1 })
        {
            Assert.Equal("\uFFFD", encoding.Decode(Bytes(pointer)));
            Assert.Throws<WebDecodingException>(() => encoding.Decode(Bytes(pointer), fatal: true));
        }

        void Check(int pointer, int codePoint) =>
            Assert.Equal(char.ConvertFromUtf32(codePoint), encoding.Decode(Bytes(pointer)));

        static byte[] Bytes(int pointer) =>
            [(byte)(pointer / 12600 + 0x81), (byte)(pointer / 1260 % 10 + 0x30),
                (byte)(pointer / 10 % 126 + 0x81), (byte)(pointer % 10 + 0x30)];
    }

    [Fact]
    public void SingleByteMappingsMatchEveryOfficialIndexEntry()
    {
        using var indexes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "indexes.json")));
        foreach (var index in indexes.RootElement.EnumerateObject().Where(e => e.Value.GetArrayLength() == 128))
        {
            var encoding = WebEncoding.ForLabel(index.Name);
            var pointer = 0;
            foreach (var codePoint in index.Value.EnumerateArray())
            {
                var expected = codePoint.ValueKind == JsonValueKind.Null ? "\uFFFD" : char.ConvertFromUtf32(codePoint.GetInt32());
                Assert.Equal(expected, encoding.Decode([(byte)(pointer++ + 0x80)]));
            }

        }
    }

    [Theory]
    [InlineData("big5", "Big5")]
    [InlineData("gb18030", "gb18030")]
    [InlineData("euc-kr", "EUC-KR")]
    [InlineData("jis0208", "EUC-JP")]
    [InlineData("jis0212", "EUC-JP")]
    [InlineData("jis0208", "Shift_JIS")]
    public void EveryMappedMultibytePointerDecodes(string indexName, string encodingName)
    {
        using var indexes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "indexes.json")));
        var index = indexes.RootElement.GetProperty(indexName);
        var encoding = WebEncoding.ForLabel(encodingName);
        for (var pointer = 0; pointer < index.GetArrayLength(); pointer++)
        {
            var entry = index[pointer];
            if (entry.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            byte[] bytes;
            string expected;
            if (encodingName == "Shift_JIS")
            {
                var lead = pointer / 188;
                var trail = pointer % 188;
                bytes = [(byte)(lead + (lead < 0x1F ? 0x81 : 0xC1)), (byte)(trail + (trail < 0x3F ? 0x40 : 0x41))];
                expected = char.ConvertFromUtf32(pointer is >= 8836 and <= 10715 ? 0xE000 + pointer - 8836 : entry.GetInt32());
            }
            else if (encodingName == "EUC-JP")
            {
                if (pointer >= 94 * 94)
                {
                    continue;
                }

                bytes = indexName == "jis0212" ? [(byte)0x8F, (byte)(pointer / 94 + 0xA1), (byte)(pointer % 94 + 0xA1)]
                    : [(byte)(pointer / 94 + 0xA1), (byte)(pointer % 94 + 0xA1)];
                expected = char.ConvertFromUtf32(entry.GetInt32());
            }
            else
            {
                var columns = indexName == "big5" ? 157 : 190;
                var trail = pointer % columns;
                var offset = indexName == "big5" ? trail < 0x3F ? 0x40 : 0x62
                    : indexName == "euc-kr" ? 0x41 : trail < 0x3F ? 0x40 : 0x41;
                bytes = [(byte)(pointer / columns + 0x81), (byte)(trail + offset)];
                expected = indexName == "big5" ? pointer switch
                {
                    1133 => "\u00CA\u0304",
                    1135 => "\u00CA\u030C",
                    1164 => "\u00EA\u0304",
                    1166 => "\u00EA\u030C",
                    _ => char.ConvertFromUtf32(entry.GetInt32())
                } : char.ConvertFromUtf32(entry.GetInt32());
            }

            Assert.Equal(expected, encoding.Decode(bytes));
        }
    }
}
