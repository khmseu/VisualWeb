using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class SecurityOriginTests
{
    private static SecurityOrigin O(string url) => BrowserUrl.Parse(url).Origin;

    [Theory]
    [InlineData("http://EXAMPLE.com/a", "http://example.com:80/b")]
    [InlineData("https://example.com", "https://example.com:443/?q#f")]
    [InlineData("ws://example.com", "ws://example.com:80")]
    [InlineData("wss://example.com", "wss://example.com:443")]
    [InlineData("ftp://example.com", "ftp://example.com:21")]
    [InlineData("http://bücher.example/", "http://xn--bcher-kva.example/")]
    [InlineData("http://0x7f.1/", "http://127.0.0.1/")]
    [InlineData("http://[0:0:0:0:0:0:0:1]/", "http://[::1]:80/")]
    [InlineData("http://u:p@example.com/", "http://example.com/")]
    [InlineData("http://EXAMPLE.COM:80/", "http://example.com/")]
    [InlineData("https://EXAMPLE.com:443/", "https://example.com/")]
    [InlineData("http://EXAMPLE.com:8080/", "http://example.com:8080/")]
    [InlineData("http://B\u00FCcher.example:8080/", "http://xn--bcher-kva.example:8080/")]
    [InlineData("http://%45xample.com:8080/", "http://example.com:8080/")]
    [InlineData("http://0x7f.1:8080/", "http://127.0.0.1:8080/")]
    public void NormalizedTupleOriginsAreSame(string left, string right)
    {
        Assert.Equal(O(left), O(right));
        Assert.True(O(left).IsSameOrigin(O(right)));
        Assert.Equal(O(left).GetHashCode(), O(right).GetHashCode());
        Assert.False(O(left).IsOpaque);
    }

    [Theory]
    [InlineData("http://example.com", "https://example.com")]
    [InlineData("http://example.com", "http://example.com:8080")]
    [InlineData("https://example.com", "https://example.com:444")]
    [InlineData("http://example.com", "http://www.example.com")]
    [InlineData("http://example.com", "ws://example.com")]
    [InlineData("http://[::1]/", "http://[::2]/")]
    public void DifferentTuplesAreNotSame(string left, string right)
    {
        Assert.NotEqual(O(left), O(right));
        Assert.False(O(left).IsSameOrigin(O(right)));
    }

    [Fact]
    public void EffectivePortAndComponentsAreExposed()
    {
        var origin = O("https://Bücher.example/");
        Assert.Equal("https", origin.Scheme);
        Assert.Equal("xn--bcher-kva.example", origin.Host);
        Assert.Equal(443, origin.Port);
        Assert.Equal("https://xn--bcher-kva.example", origin.Serialize());
        Assert.Equal("http://[::1]:8080", O("http://[::1]:8080/").Serialize());
    }

    [Theory]
    [InlineData("data:text/plain,x")]
    [InlineData("about:blank")]
    [InlineData("javascript:1")]
    [InlineData("file:///etc/passwd")]
    [InlineData("blob:null/1")]
    [InlineData("blob:file:///x")]
    [InlineData("blob:blob:https://example.com/x")]
    [InlineData("blob:ftp://example.com/x")]
    [InlineData("blob:/relative")]
    [InlineData("mailto:a@b.example")]
    public void NonTupleUrlsHaveOpaqueOrigins(string url)
    {
        var parsed = BrowserUrl.Parse(url);
        Assert.True(parsed.Origin.IsOpaque);
        Assert.Equal("null", parsed.SerializedOrigin);
        Assert.Equal("null", parsed.Origin.Serialize());
        Assert.Null(parsed.Origin.Scheme);
        Assert.Null(parsed.Origin.Host);
    }

    [Fact]
    public void OpaqueOriginsAreUniqueButStableByIdentity()
    {
        var a = O("data:text/plain,x");
        var b = O("data:text/plain,x");
        Assert.NotEqual(a, b);
        Assert.False(a.IsSameOrigin(b));
        Assert.False(a == b);
        Assert.Equal("null", a.Serialize());
        Assert.Equal(a.Serialize(), b.Serialize());

        var url = BrowserUrl.Parse("about:blank");
        Assert.Same(url.Origin, url.Origin);
        Assert.Equal(url.Origin, url.Origin);
        var copy = url.Origin;
        Assert.True(copy.IsSameOrigin(url.Origin));
        Assert.False(url.Origin.IsSameOrigin(BrowserUrl.Parse("about:blank").Origin));
        Assert.NotEqual(SecurityOrigin.CreateOpaque(), SecurityOrigin.CreateOpaque());
        Assert.Single(new HashSet<SecurityOrigin> { a, a });
        Assert.Equal(2, new HashSet<SecurityOrigin> { a, b }.Count);
    }

    [Fact]
    public void NullAndOtherTypesAreNeverEqual()
    {
        Assert.False(O("http://example.com").IsSameOrigin(null));
        Assert.False(O("http://example.com").Equals("http://example.com"));
        Assert.False(O("data:,").Equals((object?)null));
        Assert.False(O("data:,").Equals(O("http://example.com")));
    }

    [Fact]
    public void BlobUrlsInheritOnlyHttpOrigin()
    {
        Assert.Equal(O("https://example.com"), O("blob:https://example.com/d0360e2f-caee-469f-9a2f-87d5b0456f6f?q#f"));
        Assert.Equal(O("http://example.com:81"), O("blob:http://example.com:81/id"));
        Assert.NotEqual(O("https://example.com"), O("blob:http://example.com/id"));
    }

    [Fact]
    public void FileOriginsAreNeverSameEvenForTheSameUrl()
    {
        var file = BrowserUrl.Parse("file:///tmp/a");
        Assert.True(file.Origin.IsOpaque);
        Assert.False(file.Origin.IsSameOrigin(BrowserUrl.Parse("file:///tmp/a").Origin));
        Assert.False(file.Origin.IsSameOrigin(BrowserUrl.Parse("file:///tmp/b").Origin));
    }

    [Fact]
    public void ResolvedUrlsGetOriginsFromTheirOwnComponents()
    {
        var baseUrl = BrowserUrl.Parse("https://example.com/a/b");
        Assert.Equal(baseUrl.Origin, baseUrl.Resolve("/c").Origin);
        Assert.NotEqual(baseUrl.Origin, baseUrl.Resolve("http://example.com/").Origin);
        Assert.True(baseUrl.Resolve("data:,").Origin.IsOpaque);
    }
}
