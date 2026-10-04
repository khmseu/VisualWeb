using System.Text;
using System.Text.Json;
using Dubzer.WhatwgUrl.Uts46;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class UrlTests
{
    [Theory]
    [InlineData(0x3F8CD)]
    [InlineData(0x3E8AC)]
    public void UnassignedUnicode17CodePointsUseDefaultLtrDirectionAndAreDisallowed(int point)
    {
        Assert.Equal(Direction.L, new Rune(point).GetDirection());
        Assert.Equal(IdnaStatus.Disallowed, Idna.FindMapping((uint)point).Status);
    }

    public static IEnumerable<object[]> Cases()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "urltestdata.json")));
        foreach (var entry in json.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            yield return [entry.Clone()];
        }
    }

    public static IEnumerable<object[]> DomainCases()
    {
        foreach (var file in new[] { "toascii.json", "IdnaTestV2.json" })
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", file)));
            foreach (var entry in json.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object
                && e.GetProperty("input").GetRawText() != "\"\""))
            {
                yield return [entry.Clone()];
            }
        }
    }

    [Theory]
    [MemberData(nameof(DomainCases))]
    public void OfficialDomainToAsciiFixture(JsonElement entry)
    {
        var input = ReadScalarString(entry.GetProperty("input"));
        var result = BrowserUrl.ParseResult("https://" + input + "/");
        var expected = entry.GetProperty("output");
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.False(result.Success);
        }
        else
        {
            Assert.True(result.Success, result.Error);
            Assert.Equal(expected.GetString(), result.Url!.Hostname);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OfficialUrlParsingFixture(JsonElement entry)
    {
        var input = entry.GetProperty("input").GetString()!;
        var baseInput = entry.GetProperty("base");
        BrowserUrl? baseUrl = baseInput.ValueKind == JsonValueKind.Null ? null : BrowserUrl.Parse(baseInput.GetString()!);
        var result = BrowserUrl.ParseResult(input, baseUrl);
        if (entry.TryGetProperty("failure", out var failure) && failure.GetBoolean())
        {
            Assert.False(result.Success, input);
            Assert.NotNull(result.Error);
            return;
        }

        Assert.True(result.Success, $"{input}: {result.Error}");
        var url = Assert.IsType<BrowserUrl>(result.Url);
        Assert.Equal(entry.GetProperty("href").GetString(), url.Href);
        Assert.Equal(entry.GetProperty("protocol").GetString(), url.Protocol);
        Assert.Equal(entry.GetProperty("username").GetString(), url.Username);
        Assert.Equal(entry.GetProperty("password").GetString(), url.Password);
        Assert.Equal(entry.GetProperty("host").GetString(), url.Host);
        Assert.Equal(entry.GetProperty("hostname").GetString(), url.Hostname);
        Assert.Equal(entry.GetProperty("port").GetString(), url.Port);
        Assert.Equal(entry.GetProperty("pathname").GetString(), url.Pathname);
        Assert.Equal(entry.GetProperty("search").GetString(), url.Search);
        Assert.Equal(entry.GetProperty("hash").GetString(), url.Hash);
        if (entry.TryGetProperty("origin", out var origin))
        {
            Assert.Equal(origin.GetString(), url.SerializedOrigin);
        }
    }

    [Fact]
    public void ResolvingRelativeUrl_DoesNotMutateBase()
    {
        var baseUrl = BrowserUrl.Parse("https://example.org/a/b?c#d");
        var resolved = baseUrl.Resolve("../next");
        Assert.Equal("https://example.org/next", resolved.Href);
        Assert.Equal("https://example.org/a/b?c#d", baseUrl.Href);
    }

    [Fact]
    public void InvalidInput_HasDiagnosticAndThrowingConvenience()
    {
        Assert.NotNull(BrowserUrl.ParseResult("https://[").Error);
        Assert.Throws<UrlParseException>(() => BrowserUrl.Parse("https://["));
    }

    [Fact]
    public void OpaqueOrigin_IsNotAReusableSecurityIdentity()
    {
        var url = BrowserUrl.Parse("data:text/plain,hello");
        Assert.Equal("null", url.SerializedOrigin);
    }

    [Fact]
    public void LoneSurrogatesAreReplacedWithUnicodeReplacementCharacter()
    {
        Assert.Equal("https://example.org/%EF%BF%BD?q=%EF%BF%BD",
            BrowserUrl.Parse("https://example.org/\uD800?q=\uDC00").Href);
        Assert.Equal(BrowserUrl.ParseResult("https://\uFFFD/").Success,
            BrowserUrl.ParseResult("https://\uD800/").Success);
    }

    private static string ReadScalarString(JsonElement value)
    {
        // WPT includes lone UTF-16 surrogates, which System.Text.Json.GetString rejects.
        // JavaScript URL arguments are USVStrings: replace those surrogates with U+FFFD.
        var raw = value.GetRawText().AsSpan()[1..^1];
        var output = new StringBuilder();
        for (var index = 0; index < raw.Length; index++)
        {
            var c = raw[index];
            if (c == '\\')
            {
                c = raw[++index] switch
                {
                    'u' => (char)Convert.ToUInt16(raw.Slice(index + 1, 4).ToString(), 16),
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    var escaped => escaped
                };
                if (raw[index] == 'u')
                {
                    index += 4;
                }
            }

            output.Append(c);
        }

        return string.Concat(output.ToString().EnumerateRunes().Select(rune => rune.ToString()));
    }
}
