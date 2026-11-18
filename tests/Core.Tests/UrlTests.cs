using System.Text;
using System.Text.Json;
using Dubzer.WhatwgUrl;
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
    public void UrlSettersMatchPinnedWptVectors()
    {
        using var fixture = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "url-setters-wpt.json")));
        var caseCount = 0;

        foreach (var property in fixture.RootElement.EnumerateObject())
        {
            if (property.NameEquals("comment")) { continue; }

            foreach (var entry in property.Value.EnumerateArray())
            {
                var href = ReadScalarString(entry.GetProperty("href"));
                var value = ReadScalarString(entry.GetProperty("new_value"));
                var url = BrowserUrl.Parse(href);
                var updated = property.Name switch
                {
                    "protocol" => url.WithProtocol(value),
                    "username" => url.WithUsername(value),
                    "password" => url.WithPassword(value),
                    "host" => url.WithHost(value),
                    "hostname" => url.WithHostname(value),
                    "port" => url.WithPort(value),
                    "pathname" => url.WithPathname(value),
                    "search" => url.WithSearch(value),
                    "hash" => url.WithHash(value),
                    "href" => url.WithHref(value),
                    _ => throw new InvalidOperationException($"Unexpected URL setter fixture: {property.Name}")
                };

                foreach (var expected in entry.GetProperty("expected").EnumerateObject())
                {
                    var actual = expected.Name switch
                    {
                        "href" => updated.Href,
                        "protocol" => updated.Protocol,
                        "username" => updated.Username,
                        "password" => updated.Password,
                        "host" => updated.Host,
                        "hostname" => updated.Hostname,
                        "port" => updated.Port,
                        "pathname" => updated.Pathname,
                        "search" => updated.Search,
                        "hash" => updated.Hash,
                        "origin" => updated.SerializedOrigin,
                        _ => throw new InvalidOperationException(
                            $"Unexpected URL setter expectation: {expected.Name}")
                    };
                    Assert.True(
                        string.Equals(expected.Value.GetString(), actual, StringComparison.Ordinal),
                        $"{property.Name}: {href} with {value}; expected {expected.Name} "
                            + $"{expected.Value.GetString()}, actual {actual}");
                }

                caseCount++;
            }
        }

        Assert.Equal(278, caseCount);
    }

    [Theory]
    [InlineData("http://EXAMPLE.COM:80/", "http://example.com/", "example.com")]
    [InlineData("https://EXAMPLE.com:443/", "https://example.com/", "example.com")]
    [InlineData("http://EXAMPLE.com:8080/", "http://example.com:8080/", "example.com:8080")]
    [InlineData("wss://Example.COM:444/", "wss://example.com:444/", "example.com:444")]
    [InlineData("http://B\u00FCcher.example:8080/", "http://xn--bcher-kva.example:8080/", "xn--bcher-kva.example:8080")]
    [InlineData("http://%45xample.com:8080/", "http://example.com:8080/", "example.com:8080")]
    [InlineData("http://0x7f.1:8080/", "http://127.0.0.1:8080/", "127.0.0.1:8080")]
    [InlineData("http://0x7F.1:80/", "http://127.0.0.1/", "127.0.0.1")]
    [InlineData("foo://EXAMPLE.com:8080/", "foo://EXAMPLE.com:8080/", "EXAMPLE.com:8080")]
    [InlineData("foo://%45xample:1/", "foo://%45xample:1/", "%45xample:1")]
    public void HostsFollowedByExplicitPortsUseSchemeHostParsing(string input, string href, string host)
    {
        var url = BrowserUrl.Parse(input);
        Assert.Equal(href, url.Href);
        Assert.Equal(host, url.Host);
    }

    [Theory]
    [InlineData("http://ex%25ample.com:8080/")]
    [InlineData("http://ex%7Fample.com:80/")]
    [InlineData("https://ex%00ample.com:443/")]
    [InlineData("http://0x100000000:8080/")]
    public void SpecialForbiddenHostsFollowedByExplicitPortsAreRejected(string input)
    {
        Assert.False(BrowserUrl.ParseResult(input).Success);
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
    public void TryCreateUsesUnicodeParserForUnicodeStringBase()
    {
        const string baseUrl = "https://example.org/a/😀/b";

        Assert.True(DomUrl.TryCreate("", baseUrl, out var sameUrl));
        Assert.Equal("https://example.org/a/%F0%9F%98%80/b", sameUrl!.Href);
        Assert.True(DomUrl.TryCreate("child", baseUrl, out var childUrl));
        Assert.Equal("https://example.org/a/%F0%9F%98%80/child", childUrl!.Href);
    }

    [Fact]
    public void QueryEncodingOverrideAppliesOnlyToEligibleSpecialSchemes()
    {
        const string input = "https://example.org/?é'";

        Assert.Equal("https://example.org/?%C3%A9%27", BrowserUrl.Parse(input).Href);
        Assert.Equal(
            "https://example.org/?%E9%27",
            BrowserUrl.Parse(input, encoding: System.Text.Encoding.Latin1).Href);
        Assert.Equal(
            "https://example.org/?%E9%27#%C3%B6",
            BrowserUrl.Parse("https://example.org/?é'#ö", encoding: System.Text.Encoding.Latin1).Href);
        Assert.Equal(
            "file:///example?%E9",
            BrowserUrl.Parse("file:///example?é", encoding: System.Text.Encoding.Latin1).Href);
        Assert.Equal(
            "https://example.org/?a%00",
            BrowserUrl.Parse("https://example.org/?a", encoding: System.Text.Encoding.Unicode).Href);
        Assert.Equal(
            "foo://example.org/?%C3%A9",
            BrowserUrl.Parse("foo://example.org/?é", encoding: System.Text.Encoding.Latin1).Href);
        Assert.Equal(
            "ws://example.org/?%C3%A9",
            BrowserUrl.Parse("ws://example.org/?é", encoding: System.Text.Encoding.Latin1).Href);
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
        Assert.Same(url.Origin, url.Origin);
        Assert.False(url.Origin.IsSameOrigin(BrowserUrl.Parse("data:text/plain,hello").Origin));
    }

    [Fact]
    public void TupleAndBlobOriginsUseNormalizedUrlOriginComponents()
    {
        var https = BrowserUrl.Parse("https://example.org:443/a");
        var defaultPortHttps = BrowserUrl.Parse("https://example.org/b");
        var ftp = BrowserUrl.Parse("ftp://example.org/a");
        var blob = BrowserUrl.Parse("blob:https://example.org:443/id");

        Assert.Equal("https://example.org", https.Origin.Serialize());
        Assert.True(https.Origin.IsSameOrigin(defaultPortHttps.Origin));
        Assert.Equal(21, ftp.Origin.Port);
        Assert.True(https.Origin.IsSameOrigin(blob.Origin));
        Assert.True(https.Origin.IsSameOrigin(https.WithPathname("/next").Origin));
    }

    [Fact]
    public void BlobOriginsInheritOnlyHttpAndHttpsTupleOrigins()
    {
        var http = BrowserUrl.Parse("blob:http://example.org/id");
        var https = BrowserUrl.Parse("blob:https://example.org:443/id");
        var ftp = BrowserUrl.Parse("blob:ftp://example.org:21/id");
        var ws = BrowserUrl.Parse("blob:ws://example.org:80/id");
        var wss = BrowserUrl.Parse("blob:wss://example.org:443/id");

        Assert.True(http.Origin.IsSameOrigin(BrowserUrl.Parse("http://example.org/").Origin));
        Assert.True(https.Origin.IsSameOrigin(BrowserUrl.Parse("https://example.org/").Origin));
        Assert.True(ftp.Origin.IsOpaque);
        Assert.True(ws.Origin.IsOpaque);
        Assert.True(wss.Origin.IsOpaque);
    }

    [Fact]
    public void BlobEntryOriginOverridesPathFallbackAndSurvivesQueryAndFragmentUpdates()
    {
        var entryOrigin = BrowserUrl.Parse("https://entry.example/").Origin;
        var blob = BrowserUrl.Parse(
            "blob:https://path.example/id",
            blobUrlEntryOrigin: entryOrigin);

        Assert.Same(entryOrigin, blob.Origin);
        Assert.Same(entryOrigin, blob.WithSearch("?q=1").Origin);
        Assert.Same(entryOrigin, blob.WithHash("#part").Origin);
        Assert.True(BrowserUrl.Parse("blob:https://path.example/id").Origin.IsSameOrigin(
            BrowserUrl.Parse("https://path.example/").Origin));

        var opaqueEntry = SecurityOrigin.CreateOpaque();
        var opaqueBlob = BrowserUrl.Parse(
            "blob:https://path.example/id",
            blobUrlEntryOrigin: opaqueEntry);
        Assert.Same(opaqueEntry, opaqueBlob.Origin);
    }

    [Fact]
    public void DisplaySerializationOmitsCredentialsAndRestrictsBidirectionalUrls()
    {
        Assert.Equal(
            "https://bücher.example/path",
            BrowserUrl.Parse("https://user:secret@xn--bcher-kva.example/path").ToDisplayString());
        Assert.Equal(
            "https://example.org",
            BrowserUrl.Parse("https://example.org/%E2%80%AEevil?q=1#part").ToDisplayString());
        Assert.Equal(
            "https://مثال.example",
            BrowserUrl.Parse("https://مثال.example/path").ToDisplayString());
        Assert.Equal(
            "blob:https://entry.example",
            BrowserUrl.Parse(
                "blob:https://path.example/id",
                blobUrlEntryOrigin: BrowserUrl.Parse("https://entry.example/").Origin).ToDisplayString());
    }

    [Fact]
    public void FileAndOpaqueBlobOriginsAreStableOnlyWithinOneSnapshot()
    {
        var file = BrowserUrl.Parse("file:///tmp/page");
        var blobFile = BrowserUrl.Parse("blob:file:///tmp/id");

        Assert.True(file.Origin.IsOpaque);
        Assert.Same(file.Origin, file.Origin);
        Assert.False(file.Origin.IsSameOrigin(BrowserUrl.Parse("file:///tmp/page").Origin));
        Assert.True(blobFile.Origin.IsOpaque);
        Assert.False(blobFile.Origin.IsSameOrigin(BrowserUrl.Parse("blob:file:///tmp/id").Origin));
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

    [Theory]
    [InlineData("https://example.org/a", null, true)]
    [InlineData("/a?b#c", "https://example.org/x/y", true)]
    [InlineData("/a", null, false)]
    [InlineData("/a", "", false)]
    [InlineData("/a", "not a url", false)]
    [InlineData("https://[", "https://example.org/", false)]
    [InlineData("https://example.org/", "not a url", false)]
    [InlineData("", "https://example.org/x?q#f", true)]
    public void CanParseReportsParseSuccessAndRejectsInvalidBases(string input, string? baseInput, bool expected)
    {
        Assert.Equal(expected, BrowserUrl.CanParse(input, baseInput));
    }

    [Fact]
    public void CanParseRejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.CanParse(null!));
    }

    [Theory]
    [InlineData("https://example.org/a", null, "https://example.org/a")]
    [InlineData("../next?q#f", "https://example.org/a/b", "https://example.org/next?q#f")]
    [InlineData("/next", "https://example.org/a", "https://example.org/next")]
    [InlineData("https://[", null, null)]
    [InlineData("/next", null, null)]
    [InlineData("/next", "", null)]
    [InlineData("/next", "invalid base", null)]
    public void ParseOrNullParsesInputAndReturnsNullOnInputOrBaseFailure(
        string input, string? baseInput, string? expected)
    {
        Assert.Equal(expected, BrowserUrl.ParseOrNull(input, baseInput)?.Href);
    }

    [Fact]
    public void ParseOrNullRejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.ParseOrNull(null!));
    }

    [Theory]
    [InlineData("https://example.org/a?q#f", "https://other.example/b?x#y")]
    [InlineData("https://example.org/a", "data:text/plain,hello")]
    [InlineData("https://example.org/a", "file:///tmp/page")]
    public void WithHrefParsesAnAbsoluteUrlWithoutMutatingTheOriginal(string originalInput, string newValue)
    {
        var original = BrowserUrl.Parse(originalInput);

        Assert.Equal(BrowserUrl.Parse(newValue).Href, original.WithHref(newValue).Href);
        Assert.Equal(BrowserUrl.Parse(originalInput).Href, original.Href);
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("")]
    [InlineData("https://[")]
    public void WithHrefRejectsInvalidOrRelativeValues(string value)
    {
        Assert.Throws<UrlParseException>(() => BrowserUrl.Parse("https://example.org/").WithHref(value));
    }

    [Fact]
    public void WithHrefRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithHref(null!));
    }

    [Theory]
    [InlineData("https://example.org/a?q=x%20y#f")]
    [InlineData("data:text/plain,hello")]
    public void ToJsonReturnsTheSerializedHref(string input)
    {
        var url = BrowserUrl.Parse(input);

        Assert.Equal(url.Href, url.ToJson());
    }

    [Theory]
    [InlineData("http://example.org:443/a?q#h", "https:", "https://example.org/a?q#h")]
    [InlineData("https://example.org/a", "HTTP:ignored", "http://example.org/a")]
    [InlineData("foo://example.org/a", "bar:rest", "bar://example.org/a")]
    [InlineData("data:text/plain,hello", "javascript:", "javascript:text/plain,hello")]
    [InlineData("https://example.org/a", "1http:", "https://example.org/a")]
    [InlineData("https://example.org/a", "http", "http://example.org/a")]
    [InlineData("http://example.org/a", "h\nttps", "https://example.org/a")]
    [InlineData("file://server.example/a", "https", "https://server.example/a")]
    [InlineData("file:///a", "https", "file:///a")]
    [InlineData("https://example.org/a", "file", "file://example.org/a")]
    [InlineData("https://user:secret@example.org/a", "file", "https://user:secret@example.org/a")]
    [InlineData("https://example.org:8080/a", "file", "https://example.org:8080/a")]
    [InlineData("https://example.org/a", "foo:", "https://example.org/a")]
    [InlineData("foo://example.org/a", "https:", "foo://example.org/a")]
    [InlineData("file:///a", "http:", "file:///a")]
    public void WithProtocolFollowsSchemeSetterRules(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        Assert.Equal(expected, original.WithProtocol(value).Href);
        Assert.Equal(input, original.Href);
    }

    [Fact]
    public void WithProtocolRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithProtocol(null!));
    }

    [Theory]
    [InlineData("https://old:secret@example.org:8080/a?q#f", "new user:@/?#[]|", "https://new%20user%3A%40%2F%3F%23%5B%5D%7C:secret@example.org:8080/a?q#f")]
    [InlineData("https://old:secret@example.org/", "", "https://:secret@example.org/")]
    [InlineData("https://old@example.org/", "é😀", "https://%C3%A9%F0%9F%98%80@example.org/")]
    [InlineData("foo://old@example.org/path", "%not-escaped", "foo://%not-escaped@example.org/path")]
    public void WithUsernameFollowsTheUsernameSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithUsername(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Fact]
    public void WithUsernameReplacesUnpairedSurrogates()
    {
        var unpairedSurrogate = new string('\uD800', 1);

        Assert.Equal("https://%EF%BF%BD@example.org/", BrowserUrl.Parse("https://example.org/").WithUsername(unpairedSurrogate).Href);
    }

    [Theory]
    [InlineData("https://old:secret@example.org:8080/a?q#f", "new:@/?#[]|", "https://old:new%3A%40%2F%3F%23%5B%5D%7C@example.org:8080/a?q#f")]
    [InlineData("https://old:secret@example.org/", "", "https://old@example.org/")]
    [InlineData("https://example.org/", "secret", "https://:secret@example.org/")]
    [InlineData("https://old@example.org/", "é😀", "https://old:%C3%A9%F0%9F%98%80@example.org/")]
    public void WithPasswordFollowsThePasswordSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithPassword(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Fact]
    public void CredentialSettersPreserveTupleOrigin()
    {
        var original = BrowserUrl.Parse("https://old:secret@example.org/");

        Assert.True(original.Origin.IsSameOrigin(original.WithUsername("new").Origin));
        Assert.True(original.Origin.IsSameOrigin(original.WithPassword("new").Origin));
    }

    [Fact]
    public void CredentialSettersDoNothingWhenUrlCannotHaveCredentials()
    {
        foreach (var input in new[] { "file:///tmp/page", "data:text/plain,hello", "mailto:user@example.org" })
        {
            var original = BrowserUrl.Parse(input);

            Assert.Same(original, original.WithUsername("user"));
            Assert.Same(original, original.WithPassword("password"));
        }
    }

    [Fact]
    public void CredentialSettersRejectNull()
    {
        var url = BrowserUrl.Parse("https://example.org/");

        Assert.Throws<ArgumentNullException>(() => url.WithUsername(null!));
        Assert.Throws<ArgumentNullException>(() => url.WithPassword(null!));
    }

    [Theory]
    [InlineData("https://user:secret@old.example:8443/a?#", "new.example", "https://user:secret@new.example:8443/a?#")]
    [InlineData("https://user:secret@old.example:8443/a?q#f", "EXAMPLE.COM:443", "https://user:secret@example.com/a?q#f")]
    [InlineData("https://old.example:8443/a", "[2001:db8::1]", "https://[2001:db8::1]:8443/a")]
    [InlineData("foo:/a?q#f", "new.example", "foo://new.example/a?q#f")]
    [InlineData("foo://old.example/a", "", "foo:///a")]
    [InlineData("file:///a", "server.example", "file://server.example/a")]
    [InlineData("file:///a", "localhost", "file:///a")]
    public void WithHostFollowsTheHostSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithHost(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Theory]
    [InlineData("https://user:secret@example.org:8443/a?q#f", "EXAMPLE.COM", "https://user:secret@example.com:8443/a?q#f")]
    [InlineData("https://example.org:8443/a", "[2001:db8::1]", "https://[2001:db8::1]:8443/a")]
    [InlineData("foo:/old?q#f", "new.example", "foo://new.example/old?q#f")]
    [InlineData("file:///C:/old", "server.example", "file://server.example/C:/old")]
    [InlineData("file:///C:/old", "localhost", "file:///C:/old")]
    [InlineData("https://example.org/a?q#f", "new.example/ignored?x#f", "https://new.example/a?q#f")]
    public void WithHostnameFollowsTheHostnameSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithHostname(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Fact]
    public void WithHostnameDoesNothingForOpaquePathsAndInvalidValues()
    {
        var opaque = BrowserUrl.Parse("data:text/plain,hello");
        Assert.Same(opaque, opaque.WithHostname("example.org"));

        var hierarchical = BrowserUrl.Parse("https://old.example:8443/path");
        foreach (var value in new[] { "", "new.example:443", "[2001:db8::1]:443", "bad host" })
        {
            Assert.Same(hierarchical, hierarchical.WithHostname(value));
        }
    }

    [Fact]
    public void WithHostnameRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithHostname(null!));
    }

    [Fact]
    public void WithHostStopsAtTheFirstHostStateDelimiter()
    {
        var original = BrowserUrl.Parse("https://old.example:8443/keep?old#keep");

        Assert.Equal(
            "https://new.example:8443/keep?old#keep",
            original.WithHost("new.example/ignored?ignored#ignored").Href);
        Assert.Equal(
            "https://new.example:8443/keep?old#keep",
            original.WithHost("new.example\\ignored").Href);
    }

    [Fact]
    public void WithHostNoOpsForOpaquePathsAndInvalidValues()
    {
        var opaque = BrowserUrl.Parse("data:text/plain,hello");
        Assert.Same(opaque, opaque.WithHost("example.org"));

        var hierarchical = BrowserUrl.Parse("https://old.example:8443/path");
        foreach (var value in new[] { "", ":80", "user@new.example" })
        {
            Assert.Same(hierarchical, hierarchical.WithHost(value));
        }

        var nonSpecialWithPort = BrowserUrl.Parse("foo://old.example:8443/path");
        Assert.Same(nonSpecialWithPort, nonSpecialWithPort.WithHost(""));
        var nonSpecialWithoutPort = BrowserUrl.Parse("foo://path/to");
        Assert.Same(nonSpecialWithoutPort, nonSpecialWithoutPort.WithHost(":80"));

        var nonSpecialWithCredentials = BrowserUrl.Parse("foo://user@old.example/path");
        Assert.Same(nonSpecialWithCredentials, nonSpecialWithCredentials.WithHost(""));
    }

    [Fact]
    public void WithHostUsesFileHostStateForLocalhostAndPortValues()
    {
        var emptyHost = BrowserUrl.Parse("file:///a");
        var nonEmptyHost = BrowserUrl.Parse("file://server.example/a");

        Assert.Equal("file:///a", emptyHost.WithHost("localhost").Href);
        Assert.Same(emptyHost, emptyHost.WithHost("server.example:80"));
        Assert.Equal("file:///a", nonEmptyHost.WithHost("localhost").Href);
        Assert.Same(nonEmptyHost, nonEmptyHost.WithHost("server.example:80"));
        Assert.Same(nonEmptyHost, nonEmptyHost.WithHost("C:"));
    }

    [Theory]
    [InlineData("new.example:", "https://new.example:8443/path")]
    [InlineData("new.example:bad", "https://new.example:8443/path")]
    [InlineData("new.example:65536", "https://new.example:8443/path")]
    [InlineData("new.example:65536junk", "https://new.example:8443/path")]
    [InlineData("new.example:80junk", "https://new.example:80/path")]
    [InlineData("new.example:443junk", "https://new.example/path")]
    [InlineData("new.example:\t8\n0", "https://new.example:80/path")]
    public void WithHostCommitsThePortPrefixAndRetainsItWhenPortParsingFails(string value, string expected)
    {
        var original = BrowserUrl.Parse("https://old.example:8443/path");

        Assert.Equal(expected, original.WithHost(value).Href);
    }

    [Fact]
    public void WithHostRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithHost(null!));
    }

    [Theory]
    [InlineData("https://user:secret@example.org:8080/a?q#f", "443", "https://user:secret@example.org/a?q#f")]
    [InlineData("https://example.org/a", "0", "https://example.org:0/a")]
    [InlineData("https://example.org/a", "80junk", "https://example.org:80/a")]
    [InlineData("https://example.org/a", "443junk", "https://example.org/a")]
    [InlineData("https://example.org/a", "80 ", "https://example.org:80/a")]
    [InlineData("https://example.org:8080/a?q#f", "", "https://example.org/a?q#f")]
    [InlineData("foo://example.org/a", "443", "foo://example.org:443/a")]
    [InlineData("https://example.org/a", "8\t0", "https://example.org:80/a")]
    [InlineData("https://example.org/a", "00080", "https://example.org:80/a")]
    public void WithPortFollowsThePortSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithPort(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("65536")]
    [InlineData("65536junk")]
    [InlineData("-1")]
    [InlineData(" 80")]
    [InlineData("\t")]
    [InlineData("٨")]
    public void WithPortNoOpsForInvalidValues(string value)
    {
        var original = BrowserUrl.Parse("https://example.org:8080/path");

        Assert.Same(original, original.WithPort(value));
    }

    [Fact]
    public void WithPortDoesNothingWhenTheUrlCannotHaveAPort()
    {
        foreach (var input in new[] { "file:///a", "file://server.example/a", "data:text/plain,hello", "mailto:user@example.org" })
        {
            var original = BrowserUrl.Parse(input);

            Assert.Same(original, original.WithPort("80"));
            Assert.Same(original, original.WithPort(""));
        }
    }

    [Fact]
    public void WithPortRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithPort(null!));
    }

    [Theory]
    [InlineData("https://user:secret@example.org:8443/a/old?q#f", "/a b/../c", "https://user:secret@example.org:8443/c?q#f")]
    [InlineData("https://example.org/a?q#f", "", "https://example.org/?q#f")]
    [InlineData("https://example.org/a?q#f", "/new?x#y", "https://example.org/new%3Fx%23y?q#f")]
    [InlineData("https://example.org/a?q#f", "\\next", "https://example.org/next?q#f")]
    [InlineData("https://example.org/a?#", "/new", "https://example.org/new?#")]
    [InlineData("https://example.org/a?q#f", "/a\tb", "https://example.org/ab?q#f")]
    [InlineData("https://example.org/a", "/new ", "https://example.org/new%20")]
    [InlineData("https://example.org/a", "/new\u0001", "https://example.org/new%01")]
    [InlineData("foo://example.org/a?q#f", "new", "foo://example.org/new?q#f")]
    [InlineData("foo:/old?q#f", "new", "foo:/new?q#f")]
    [InlineData("non-spec:/", "//p", "non-spec:/.//p")]
    [InlineData("file:///C:/old", "D|/new", "file:///D:/new")]
    public void WithPathnameFollowsThePathnameSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithPathname(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(input, original.Href);
    }

    [Fact]
    public void WithPathnameDoesNothingForOpaquePaths()
    {
        var original = BrowserUrl.Parse("data:text/plain,hello");

        Assert.Same(original, original.WithPathname("/new"));
    }

    [Fact]
    public void WithPathnameRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithPathname(null!));
    }

    [Theory]
    [InlineData("https://example.org/p?q#old", "new", "https://example.org/p?q#new")]
    [InlineData("https://example.org/p", "#new", "https://example.org/p#new")]
    [InlineData("https://example.org/p", "##x", "https://example.org/p##x")]
    [InlineData("https://example.org/p?q#old", "", "https://example.org/p?q")]
    [InlineData("https://example.org/p", "#", "https://example.org/p#")]
    [InlineData("https://example.org/p", "a b\t\nc\u007F\"<>`", "https://example.org/p#a%20bc%7F%22%3C%3E%60")]
    [InlineData("https://example.org/p", "\u00E9\uD83D\uDE00", "https://example.org/p#%C3%A9%F0%9F%98%80")]
    [InlineData("data:text/plain,a#frag", "", "data:text/plain,a")]
    [InlineData("data:text/plain,a?q#frag", "x y", "data:text/plain,a?q#x%20y")]
    public void WithHashFollowsTheHashSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        var updated = original.WithHash(value);

        Assert.Equal(expected, updated.Href);
        Assert.Equal(BrowserUrl.Parse(input).Href, original.Href);
    }

    [Fact]
    public void WithHashReplacesUnpairedSurrogates()
    {
        var unpairedSurrogate = new string('\uD800', 1);

        Assert.Equal("https://example.org/p#%EF%BF%BD", BrowserUrl.Parse("https://example.org/p").WithHash(unpairedSurrogate).Href);
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithHash(null!));
    }

    [Theory]
    [InlineData("https://example.org/p?q#h", "a=b", "https://example.org/p?a=b#h")]
    [InlineData("https://example.org/p#h", "?x", "https://example.org/p?x#h")]
    [InlineData("https://example.org/p?q#h", "", "https://example.org/p#h")]
    [InlineData("https://example.org/p?q", "?", "https://example.org/p?")]
    [InlineData("https://example.org/p?q", "??", "https://example.org/p??")]
    [InlineData("https://example.org/p", "a b#c\td\ne\u007F\"<>", "https://example.org/p?a%20b%23cde%7F%22%3C%3E")]
    [InlineData("https://example.org/p", "it's", "https://example.org/p?it%27s")]
    [InlineData("foo://example.org/p", "it's", "foo://example.org/p?it's")]
    [InlineData("https://example.org/p", "\u00E9\uD83D\uDE00", "https://example.org/p?%C3%A9%F0%9F%98%80")]
    [InlineData("data:text/plain,a?q#frag", "x y", "data:text/plain,a?x%20y#frag")]
    public void WithSearchFollowsTheSearchSetter(string input, string value, string expected)
    {
        var original = BrowserUrl.Parse(input);

        Assert.Equal(expected, original.WithSearch(value).Href);
        Assert.Equal(BrowserUrl.Parse(input).Href, original.Href);
    }

    [Fact]
    public void WithSearchReplacesUnpairedSurrogatesAndRejectsNull()
    {
        var unpairedSurrogate = new string('\uD800', 1);

        Assert.Equal("https://example.org/p?%EF%BF%BD", BrowserUrl.Parse("https://example.org/p").WithSearch(unpairedSurrogate).Href);
        Assert.Throws<ArgumentNullException>(() => BrowserUrl.Parse("https://example.org/").WithSearch(null!));
    }

    [Fact]
    public void SearchAndHashStateOverridesRemoveAsciiTabsAndNewlines()
    {
        var original = BrowserUrl.Parse("https://example.org/p?old#old");

        Assert.Equal("https://example.org/p?a%20bc#old", original.WithSearch("a b\t\nc").Href);
        Assert.Equal("https://example.org/p?old#a%20bc", original.WithHash("a b\t\nc").Href);
        Assert.Equal("https://example.org/p?old#old", original.Href);
    }

    [Fact]
    public void SearchAndHashRemoveLeadingTabsBeforeStateOverrideParsing()
    {
        var url = BrowserUrl.Parse("https://example.org/p");

        Assert.Equal("https://example.org/p??x", url.WithSearch("\t?x").Href);
        Assert.Equal("https://example.org/p##x", url.WithHash("\t#x").Href);
    }

    [Fact]
    public void EmptyQueryDelimiterIsSerializedButNotExposedBySearchGetter()
    {
        var url = BrowserUrl.Parse("https://example.org/p?");

        Assert.Equal("https://example.org/p?", url.Href);
        Assert.Equal("", url.Search);
        Assert.Equal("https://example.org/p?", url.WithSearch("?").Href);
    }

    [Fact]
    public void CreateSearchParamsParsesTheQueryAsADetachedSnapshot()
    {
        var url = BrowserUrl.Parse("https://example.org/p?a=1&b=x+y&a=2#h");
        var parameters = url.CreateSearchParams();

        Assert.Equal(["1", "2"], parameters.GetAll("a"));
        Assert.Equal("x y", parameters.Get("b"));

        parameters.Append("c", "3");
        Assert.Equal("https://example.org/p?a=1&b=x+y&a=2#h", url.Href);
        Assert.Equal(0, BrowserUrl.Parse("https://example.org/p").CreateSearchParams().Size);
        Assert.Equal(0, BrowserUrl.Parse("https://example.org/p?").CreateSearchParams().Size);
    }

    [Fact]
    public void WithSearchParamsAppliesTheSerializationAndRetainsTheFragment()
    {
        var url = BrowserUrl.Parse("https://example.org/p?old#h");
        var parameters = new UrlSearchParams("a=1&a=2");
        parameters.Append("q r", "\u00E9#&'");

        Assert.Equal("https://example.org/p?a=1&a=2&q+r=%C3%A9%23%26%27#h", url.WithSearchParams(parameters).Href);
        Assert.Equal("https://example.org/p#h", url.WithSearchParams(new UrlSearchParams()).Href);
        Assert.Equal("https://example.org/p?old#h", url.Href);
        Assert.Throws<ArgumentNullException>(() => url.WithSearchParams(null!));
    }
}
