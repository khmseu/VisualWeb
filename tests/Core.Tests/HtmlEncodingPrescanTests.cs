using VisualWeb.Core.Encoding;
using Xunit;
using TextEncoding = System.Text.Encoding;

namespace VisualWeb.Core.Tests;

/// <summary>Spec-derived cases for the HTML prescan; html5lib encoding fixtures are not pinned locally.</summary>
public sealed class HtmlEncodingPrescanTests
{
    private static HtmlPrescanResult Scan(string latin1) => HtmlEncodingPrescanner.Prescan(TextEncoding.Latin1.GetBytes(latin1));

    [Theory]
    [InlineData("<meta charset=\"windows-1251\">", "windows-1251")]
    [InlineData("<meta charset=koi8-r>", "KOI8-R")]
    [InlineData("<META CHARSET='ISO-8859-2'>", "ISO-8859-2")]
    [InlineData("<MeTa\tChArSeT=Shift_JIS>", "Shift_JIS")]
    [InlineData("<meta/charset=koi8-r>", "KOI8-R")]
    [InlineData("<meta charset = \"koi8-r\" >", "KOI8-R")]
    [InlineData("<meta \n charset\f=\r'koi8-r'/>", "KOI8-R")]
    [InlineData("<meta name=x charset=gbk>", "GBK")]
    [InlineData("<meta charset=\" koi8-r \">", "KOI8-R")]
    public void DirectMetaCharsetIsDetected(string html, string expected)
    {
        var result = Scan(html);
        Assert.Equal(expected, result.Encoding?.Name);
        Assert.Equal(HtmlPrescanSource.MetaCharset, result.Source);
        Assert.Equal(0, result.DeclarationOffset);
    }

    [Theory]
    [InlineData("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1250\">")]
    [InlineData("<meta content=\"text/html; charset=windows-1250\" http-equiv=\"Content-Type\">")]
    [InlineData("<meta http-equiv=CONTENT-TYPE content='TEXT/HTML;CHARSET=WINDOWS-1250'>")]
    [InlineData("<meta http-equiv=content-type content=\"text/html;charset='windows-1250'\">")]
    [InlineData("<meta http-equiv=content-type content='charset=\"windows-1250\"'>")]
    [InlineData("<meta http-equiv=content-type content=\"charset = windows-1250 ; foo\">")]
    [InlineData("<meta http-equiv=content-type content=\"charsetcharset=windows-1250\">")]
    [InlineData("<meta http-equiv=content-type content=\"xcharset=windows-1250\">")]
    public void LegacyPragmaRequiresHttpEquivContentTypeInAnyOrder(string html)
    {
        var result = Scan(html);
        Assert.Equal("windows-1250", result.Encoding?.Name);
        Assert.Equal(HtmlPrescanSource.MetaPragma, result.Source);
    }

    [Theory]
    [InlineData("<meta content=\"text/html; charset=windows-1250\">")]
    [InlineData("<meta http-equiv=refresh content=\"text/html; charset=windows-1250\">")]
    [InlineData("<meta http-equiv=\" content-type\" content=\"charset=windows-1250\">")]
    [InlineData("<meta http-equiv=content-type content=\"text/html\">")]
    [InlineData("<meta http-equiv=content-type content='charset=\"windows-1250'>")]
    [InlineData("<meta http-equiv=content-type content=\"charset=\">")]
    [InlineData("<meta http-equiv=content-type content=\"charset=bogus\">")]
    [InlineData("<meta charset>")]
    [InlineData("<meta charset=>")]
    [InlineData("<meta charset=koi8-r/>")]
    [InlineData("<meta charset=\"koi8-r>")]
    [InlineData("<metacharset=koi8-r>")]
    [InlineData("<meta>charset=koi8-r>")]
    [InlineData("</meta charset=koi8-r>")]
    [InlineData("<meta charset=bogus content=\"charset=koi8-r\" http-equiv=content-type>")]
    [InlineData("<meta http-equiv=refresh http-equiv=content-type content=\"charset=koi8-r\">")]
    [InlineData("<meta content=\"charset=bogus\" content=\"charset=koi8-r\" http-equiv=content-type>")]
    [InlineData("<!-- <meta charset=koi8-r> -->")]
    [InlineData("<!-- -- ><meta charset=koi8-r> -->")]
    [InlineData("<?php echo \"<meta charset=koi8-r>\" ?>")]
    [InlineData("<!DOCTYPE html x=\"<meta charset=koi8-r>\">")]
    [InlineData("<div title=\"<meta charset=koi8-r>\">")]
    [InlineData("<p x='>'<meta charset=koi8-r>")]
    [InlineData("<meta charset=koi8-r ")]
    [InlineData("")]
    [InlineData("plain text")]
    public void NonDeclarationsAndFailuresReturnNoEncoding(string html)
    {
        var result = Scan(html);
        Assert.Null(result.Encoding);
        Assert.Equal(HtmlPrescanSource.None, result.Source);
        Assert.Null(result.DeclarationOffset);
    }

    [Theory]
    [InlineData("<meta charset=koi8-r charset=windows-1250>", "KOI8-R", HtmlPrescanSource.MetaCharset)]
    [InlineData("<meta charset=windows-1250 content=\"charset=koi8-r\" http-equiv=content-type>", "windows-1250", HtmlPrescanSource.MetaCharset)]
    [InlineData("<meta content=\"charset=koi8-r\" http-equiv=content-type charset=windows-1250>", "windows-1250", HtmlPrescanSource.MetaCharset)]
    [InlineData("<meta content=\"charset=koi8-r\" charset=windows-1250>", "windows-1250", HtmlPrescanSource.MetaCharset)]
    [InlineData("<meta http-equiv=content-type http-equiv=refresh content=\"charset=koi8-r\">", "KOI8-R", HtmlPrescanSource.MetaPragma)]
    [InlineData("<meta CHARSET=koi8-r Charset=bogus>", "KOI8-R", HtmlPrescanSource.MetaCharset)]
    public void DuplicateAttributesKeepTheFirstAndCharsetOverridesPragma(string html, string expected, HtmlPrescanSource source)
    {
        var result = Scan(html);
        Assert.Equal(expected, result.Encoding?.Name);
        Assert.Equal(source, result.Source);
    }

    [Theory]
    [InlineData("<meta charset=bogus><meta charset=koi8-r>", "KOI8-R", 20)]
    [InlineData("<meta charset=koi8-r/><meta charset=windows-1250>", "windows-1250", 22)]
    [InlineData("<meta http-equiv=content-type content=\"charset=bogus\"><meta charset=koi8-r>", "KOI8-R", 54)]
    [InlineData("<!-- <meta charset=koi8-r> --><meta charset=windows-1250>", "windows-1250", 30)]
    [InlineData("<!--><meta charset=koi8-r>", "KOI8-R", 5)]
    [InlineData("<!---><meta charset=koi8-r>", "KOI8-R", 6)]
    [InlineData("<!DOCTYPE html><meta charset=koi8-r>", "KOI8-R", 15)]
    [InlineData("<?xml version=\"1.0\"?><meta charset=koi8-r>", "KOI8-R", 21)]
    [InlineData("</p><meta charset=koi8-r>", "KOI8-R", 4)]
    [InlineData("</ p><meta charset=koi8-r>", "KOI8-R", 5)]
    [InlineData("<title><meta charset=koi8-r></title>", "KOI8-R", 7)]
    [InlineData("<script>\"<meta charset=koi8-r>\"</script>", "KOI8-R", 9)]
    [InlineData("<div title=\"<meta charset=windows-1250>\"><meta charset=koi8-r>", "KOI8-R", 41)]
    [InlineData("<div title=a>b<meta charset=koi8-r>", "KOI8-R", 14)]
    [InlineData("<p x='>'><meta charset=koi8-r>", "KOI8-R", 9)]
    [InlineData("a < b <meta charset=koi8-r>", "KOI8-R", 6)]
    public void ScanningContinuesAfterSkippedTagsAndInvalidDeclarations(string html, string expected, int offset)
    {
        var result = Scan(html);
        Assert.Equal(expected, result.Encoding?.Name);
        Assert.Equal(offset, result.DeclarationOffset);
    }

    [Theory]
    [InlineData("<meta charset=bogus>", "bogus")]
    [InlineData("<meta http-equiv=content-type content=\"charset=nope\">", "nope")]
    public void InvalidLabelsAreReportedAsIgnored(string html, string label)
    {
        var result = Scan(html);
        Assert.Null(result.Encoding);
        Assert.Equal(new[] { label }, result.IgnoredLabels);
    }

    [Theory]
    [InlineData("<meta charset=utf-16>", "UTF-8")]
    [InlineData("<meta charset=utf-16be>", "UTF-8")]
    [InlineData("<meta charset=UTF-16LE>", "UTF-8")]
    [InlineData("<meta http-equiv=content-type content=\"charset=utf-16\">", "UTF-8")]
    [InlineData("<meta charset=x-user-defined>", "windows-1252")]
    [InlineData("<meta http-equiv=content-type content=\"charset=x-user-defined\">", "windows-1252")]
    [InlineData("<meta charset=latin1>", "windows-1252")]
    [InlineData("<meta charset=iso-2022-kr>", "replacement")]
    public void MetaResultsAreNormalized(string html, string expected) => Assert.Equal(expected, Scan(html).Encoding?.Name);

    [Theory]
    [InlineData("<?xml version=\"1.0\" encoding=\"koi8-r\"?>", "KOI8-R")]
    [InlineData("<?xml encoding = 'KOI8-R' ?><p>", "KOI8-R")]
    [InlineData("<?xml version='1.0' encoding='utf-16'?>", "UTF-8")]
    [InlineData("<?xml version='1.0' encoding='x-user-defined'?>", "x-user-defined")]
    [InlineData("<?xml encoding='koi8-r'?><meta charset=bogus>", "KOI8-R")]
    public void XmlDeclarationIsUsedWhenPrescanAborts(string html, string expected)
    {
        var result = Scan(html);
        Assert.Equal(expected, result.Encoding?.Name);
        Assert.Equal(HtmlPrescanSource.XmlDeclaration, result.Source);
        Assert.Equal(0, result.DeclarationOffset);
    }

    [Theory]
    [InlineData(" <?xml encoding='koi8-r'?>")]
    [InlineData("<?XML encoding='koi8-r'?>")]
    [InlineData("<?xml ENCODING='koi8-r'?>")]
    [InlineData("<?xml encoding='koi 8'?>")]
    [InlineData("<?xml encoding=koi8-r?>")]
    [InlineData("<?xml encoding='koi8-r")]
    [InlineData("<?xml version='1.0'?> encoding='koi8-r'")]
    [InlineData("<?xml encoding='bogus'?>")]
    public void MalformedXmlDeclarationsFail(string html) => Assert.Null(Scan(html).Encoding);

    [Fact]
    public void MetaDeclarationWinsOverXmlDeclaration()
    {
        var result = Scan("<?xml encoding='koi8-r'?><meta charset=windows-1250>");
        Assert.Equal("windows-1250", result.Encoding?.Name);
        Assert.Equal(HtmlPrescanSource.MetaCharset, result.Source);
    }

    [Fact]
    public void Utf16XmlPrefixesAreDetectedWithoutNormalization()
    {
        var little = HtmlEncodingPrescanner.Prescan([0x3C, 0, 0x3F, 0, 0x78, 0]);
        var big = HtmlEncodingPrescanner.Prescan([0, 0x3C, 0, 0x3F, 0, 0x78]);
        Assert.Equal(("UTF-16LE", HtmlPrescanSource.Utf16XmlPrefix), (little.Encoding?.Name, little.Source));
        Assert.Equal(("UTF-16BE", HtmlPrescanSource.Utf16XmlPrefix), (big.Encoding?.Name, big.Source));
        Assert.Null(HtmlEncodingPrescanner.Prescan([0x3C, 0, 0x3F, 0, 0x78]).Encoding);
    }

    private const string Declaration = "<meta charset=koi8-r>";

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void CompleteDeclarationMustEndWithinTheFirst1024Bytes(int overflow, bool detected)
    {
        var bytes = TextEncoding.ASCII.GetBytes(new string(' ', 1024 - Declaration.Length + overflow) + Declaration + " <p>");
        var result = HtmlEncodingPrescanner.Prescan(bytes);
        Assert.Equal(detected ? "KOI8-R" : null, result.Encoding?.Name);
        Assert.Equal(HtmlEncodingPrescanner.ByteLimit, result.ScannedByteCount);
    }

    [Theory]
    [InlineData("<meta charset=koi8-r", ">")]
    [InlineData("<meta charset=\"koi8-r\"", ">")]
    [InlineData("<meta charset=koi8-r", " >")]
    [InlineData("<meta http-equiv=content-type content=\"charset=koi8-r\"", ">")]
    [InlineData("<meta charset", "=koi8-r>")]
    [InlineData("<!--", "--><meta charset=koi8-r>")]
    public void IncompleteDeclarationsAtTheBudgetAbortWithoutReadingFurther(string head, string tail)
    {
        var bytes = TextEncoding.ASCII.GetBytes(new string(' ', 1024 - head.Length) + head + tail);
        Assert.Null(HtmlEncodingPrescanner.Prescan(bytes).Encoding);
        Assert.Equal("KOI8-R", HtmlEncodingPrescanner.Prescan(TextEncoding.ASCII.GetBytes(head + tail)).Encoding?.Name);
    }

    [Theory]
    [InlineData(1023, true)]
    [InlineData(1024, false)]
    public void XmlDeclarationEndMustBeWithinTheFirst1024Bytes(int end, bool detected)
    {
        var head = "<?xml encoding='koi8-r'";
        var bytes = TextEncoding.ASCII.GetBytes(head + new string(' ', end - head.Length) + ">");
        Assert.Equal(detected ? "KOI8-R" : null, HtmlEncodingPrescanner.Prescan(bytes).Encoding?.Name);
    }

    [Theory]
    [InlineData(501, true)]
    [InlineData(502, false)]
    public void BudgetCountsBytesNotCharacters(int accentedCharacters, bool detected)
    {
        var bytes = TextEncoding.UTF8.GetBytes(new string('é', accentedCharacters) + Declaration);
        Assert.Equal(detected, bytes.Length <= 1024);
        Assert.True(accentedCharacters + Declaration.Length < 1024);
        Assert.Equal(detected ? "KOI8-R" : null, HtmlEncodingPrescanner.Prescan(bytes).Encoding?.Name);
    }

    [Fact]
    public void ShorterInputsAreScannedWholly()
    {
        var result = Scan("<p>");
        Assert.Equal(3, result.ScannedByteCount);
        Assert.Empty(result.IgnoredLabels);
    }

    [Theory]
    [InlineData("text/html; charset=koi8-r", "KOI8-R")]
    [InlineData("charset='koi8-r'", "KOI8-R")]
    [InlineData("CHARSET=\"KOI8-R\" x", "KOI8-R")]
    [InlineData("charset;charset=koi8-r", "KOI8-R")]
    [InlineData("charset=koi8-r;q", "KOI8-R")]
    [InlineData("charset=\tkoi8-r\nfoo", "KOI8-R")]
    [InlineData("charset=\"koi8-r", null)]
    [InlineData("charset=", null)]
    [InlineData("charset=bogus", null)]
    [InlineData("text/html", null)]
    [InlineData("chars et=koi8-r", null)]
    public void MetaContentExtraction(string content, string? expected) =>
        Assert.Equal(expected, HtmlEncodingPrescanner.ExtractFromMetaContent(content)?.Name);

    [Theory]
    [InlineData("char\u017Fet")]
    [InlineData("CHAR\u017FET")]
    [InlineData("char\uFF53et")]
    [InlineData("char\uFF33et")]
    public void MetaContentExtractionRejectsNonAsciiKeywordLookalikes(string keyword)
    {
        Assert.Null(HtmlEncodingPrescanner.ExtractFromMetaContent($"{keyword}=koi8-r"));
        Assert.Equal("windows-1251",
            HtmlEncodingPrescanner.ExtractFromMetaContent($"{keyword}=koi8-r; charset=windows-1251")?.Name);
    }

    [Theory]
    [InlineData("char\u017Fet")]
    [InlineData("CHAR\u017FET")]
    [InlineData("char\uFF53et")]
    [InlineData("char\uFF33et")]
    public void PrescanRejectsUtf8KeywordLookalikes(string keyword)
    {
        var bytes = TextEncoding.UTF8.GetBytes($"<meta http-equiv=content-type content='{keyword}=koi8-r'>");
        var result = HtmlEncodingPrescanner.Prescan(bytes);

        Assert.Null(result.Encoding);
        Assert.Equal(HtmlPrescanSource.None, result.Source);
        Assert.Empty(result.IgnoredLabels);
    }

    [Fact]
    public void MetaContentExtractionRejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => HtmlEncodingPrescanner.ExtractFromMetaContent(null!));
}

public sealed class HtmlEncodingSnifferTests
{
    private static readonly WebEncoding Utf8 = WebEncoding.ForLabel("UTF-8");
    private static readonly byte[] LegacyMeta = TextEncoding.ASCII.GetBytes("<meta charset=koi8-r><p>");

    [Fact]
    public void BomWinsOverTransportAndMeta()
    {
        var result = HtmlEncodingSniffer.Sniff([0xFF, 0xFE, .. LegacyMeta], "windows-1252", Utf8);
        Assert.Equal(("UTF-16LE", HtmlEncodingSource.ByteOrderMark, HtmlEncodingConfidence.Certain),
            (result.Encoding.Name, result.Source, result.Confidence));
        Assert.Null(result.Prescan);
    }

    [Fact]
    public void BomWinsOverUnsupportedTransportLabelWhichRemainsVisible()
    {
        var result = HtmlEncodingSniffer.Sniff([0xEF, 0xBB, 0xBF, .. LegacyMeta], "not-an-encoding", Utf8);
        Assert.Equal(("UTF-8", HtmlEncodingSource.ByteOrderMark), (result.Encoding.Name, result.Source));
        Assert.Equal("not-an-encoding", result.UnsupportedTransportLabel);
    }

    [Fact]
    public void SupportedTransportCharsetWinsOverMeta()
    {
        var result = HtmlEncodingSniffer.Sniff(LegacyMeta, " Windows-1252 ", Utf8);
        Assert.Equal(("windows-1252", HtmlEncodingSource.TransportLayer, HtmlEncodingConfidence.Certain),
            (result.Encoding.Name, result.Source, result.Confidence));
        Assert.Null(result.UnsupportedTransportLabel);
    }

    [Fact]
    public void TransportUtf16IsNotNormalized() =>
        Assert.Equal("UTF-16LE", HtmlEncodingSniffer.Sniff(LegacyMeta, "utf-16", Utf8).Encoding.Name);

    [Theory]
    [InlineData(null, null)]
    [InlineData("bogus", "bogus")]
    [InlineData("", "")]
    public void PrescanIsTentativeWhenTransportHasNoSupportedCharset(string? label, string? unsupported)
    {
        var result = HtmlEncodingSniffer.Sniff(LegacyMeta, label, Utf8);
        Assert.Equal(("KOI8-R", HtmlEncodingSource.Prescan, HtmlEncodingConfidence.Tentative),
            (result.Encoding.Name, result.Source, result.Confidence));
        Assert.Equal(HtmlPrescanSource.MetaCharset, result.Prescan!.Source);
        Assert.Equal(unsupported, result.UnsupportedTransportLabel);
    }

    [Fact]
    public void DefaultIsUsedWithoutAnyDeclaration()
    {
        var result = HtmlEncodingSniffer.Sniff("<p>é"u8, null, WebEncoding.ForLabel("windows-1252"));
        Assert.Equal(("windows-1252", HtmlEncodingSource.Default, HtmlEncodingConfidence.Tentative),
            (result.Encoding.Name, result.Source, result.Confidence));
        Assert.Equal(HtmlPrescanSource.None, result.Prescan!.Source);
    }

    [Fact]
    public void TryForLabelUsesAsciiCaseInsensitivityOnly()
    {
        Assert.True(WebEncoding.TryForLabel("\tKOI8-R ", out var encoding));
        Assert.Equal("KOI8-R", encoding!.Name);
        Assert.False(WebEncoding.TryForLabel("\u212Aoi8-r", out _));
        Assert.False(WebEncoding.TryForLabel("bogus", out var missing));
        Assert.Null(missing);
        Assert.Throws<ArgumentException>(() => WebEncoding.ForLabel("\u212Aoi8-r"));
    }
}
