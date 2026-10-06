using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class HstsPolicyStoreTests
{
    private static BrowserUrl Url(string host = "example.test", string scheme = "https") =>
        BrowserUrl.Parse($"{scheme}://{host}/");

    private static List<string> Learn(HstsPolicyStore store, string header, string host = "example.test",
        string scheme = "https", bool authenticated = true)
    {
        var diagnostics = new List<string>();
        store.ProcessResponse(Url(host, scheme), [header], authenticated, diagnostics);
        return diagnostics;
    }

    [Theory]
    [InlineData("max-age=60")]
    [InlineData("MAX-AGE = \"60\" ; INCLUDESUBDOMAINS")]
    [InlineData(" ; max-age=\"\\6\\0\" ;; ")]
    [InlineData("max-age=60; extension=\"a;b\\\"c\"; flag; other=token")]
    [InlineData("\tmax-age\t=\t\"60\"\t;\tother=\"\"; flag")]
    [InlineData("max-age=00060; includeSubDomains")]
    public void ValidGrammarIncludingQuotedUnescapingAndUnknownDirectives(string header)
    {
        var store = new HstsPolicyStore();
        Assert.Empty(Learn(store, header));
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
    }

    [Theory]
    [InlineData("")]
    [InlineData("includeSubDomains")]
    [InlineData("max-age")]
    [InlineData("max-age=-1")]
    [InlineData("max-age=+1")]
    [InlineData("max-age=1.0")]
    [InlineData("max-age=\" 1\"")]
    [InlineData("max-age=\"\"")]
    [InlineData("max-age=１２")]
    [InlineData("max-age=1; MAX-AGE=2")]
    [InlineData("max-age=1; includeSubDomains; includesubdomains")]
    [InlineData("max-age=1; includeSubDomains=true")]
    [InlineData("max-age=1; other=x; OTHER=y")]
    [InlineData("max-age=1; other=")]
    [InlineData("max-age=1; other=\"unterminated")]
    [InlineData("max-age=1, max-age=2")]
    [InlineData("max-age=1; other=\"line\r\nbreak\"")]
    [InlineData("max-age=1; other=\"\u007f\"")]
    [InlineData("max-age=1; other=\"\\é\"")]
    [InlineData("max-age=\"6\\\"0\"")]
    [InlineData("max-age=1; \"other\"=value")]
    public void InvalidGrammarIsIgnoredVisiblyWithoutModifyingExistingPolicy(string header)
    {
        var store = new HstsPolicyStore();
        Assert.Contains(Learn(store, header), item => item.Contains("Strict-Transport-Security", StringComparison.Ordinal));
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
        Learn(store, "max-age=60; includeSubDomains");
        Assert.NotEmpty(Learn(store, header));
        Assert.Equal("https:", store.Upgrade(Url("child.example.test", "http")).Protocol);
    }

    [Fact]
    public void OnlyFirstHeaderIsProcessedEvenIfMalformed()
    {
        var store = new HstsPolicyStore();
        var diagnostics = new List<string>();
        store.ProcessResponse(Url(), ["invalid", "max-age=60"], true, diagnostics);
        Assert.NotEmpty(diagnostics);
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
        store.ProcessResponse(Url(), ["max-age=60", "max-age=0"], true, diagnostics);
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.1")]
    [InlineData("0x7f000001")]
    [InlineData("[::1]")]
    [InlineData("[::ffff:127.0.0.1]")]
    public void IpLiteralsNeverLearnOrUpgrade(string host)
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60; includeSubDomains", host);
        Assert.Equal("http:", store.Upgrade(Url(host, "http")).Protocol);
    }

    [Fact]
    public void InsecureOrUnauthenticatedTransportCannotLearnOrRemove()
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60", scheme: "http");
        Learn(store, "max-age=60", authenticated: false);
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
        Learn(store, "max-age=60");
        Learn(store, "max-age=0", authenticated: false);
        Learn(store, "max-age=0", scheme: "http");
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
    }

    [Fact]
    public void ExactAndParentLabelMatchingUseNormalizedCaseAndIdna()
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60", "BÜCHER.Example");
        Assert.Equal("https:", store.Upgrade(Url("xn--bcher-kva.example", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("a.xn--bcher-kva.example", "http")).Protocol);
        Learn(store, "max-age=60; includeSubDomains", "EXAMPLE.TEST");
        Assert.Equal("https:", store.Upgrade(Url("a.b.Example.Test", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("notexample.test", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("example.test.evil", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("test", "http")).Protocol);
    }

    [Fact]
    public void DnsRootDotDoesNotSplitPoliciesAndPortsAreNotPolicyKeys()
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60; includeSubDomains", "example.test.:8443");
        Assert.Equal("https:", store.Upgrade(Url("child.example.test:8080", "http")).Protocol);
        Assert.Equal("https:", store.Upgrade(Url("EXAMPLE.test.", "http")).Protocol);
        Learn(store, "max-age=0", "example.test:9443");
        Assert.Equal("http:", store.Upgrade(Url("example.test.", "http")).Protocol);
    }

    [Fact]
    public void ExpiredParentDoesNotRemoveIndependentChildPolicy()
    {
        var clock = new Clock();
        var store = new HstsPolicyStore(timeProvider: clock);
        Learn(store, "max-age=5; includeSubDomains");
        Learn(store, "max-age=60", "child.example.test");
        clock.Now += TimeSpan.FromSeconds(5);
        Assert.Equal("http:", store.Upgrade(Url("sibling.example.test", "http")).Protocol);
        Assert.Equal("https:", store.Upgrade(Url("child.example.test", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("deep.child.example.test", "http")).Protocol);
    }

    [Fact]
    public void RemovalOnlyRemovesExactRecordAndRefreshReplacesSubdomainFlag()
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60; includeSubDomains");
        Learn(store, "max-age=60", "child.example.test");
        Learn(store, "max-age=0; includeSubDomains", "child.example.test");
        Assert.Equal("https:", store.Upgrade(Url("child.example.test", "http")).Protocol);
        Learn(store, "max-age=60");
        Assert.Equal("http:", store.Upgrade(Url("child.example.test", "http")).Protocol);
        Learn(store, "max-age=\"\\0\"");
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
    }

    [Fact]
    public void ExpiryRefreshAndSaturatedMaxAgeUseInjectedTime()
    {
        var clock = new Clock();
        var store = new HstsPolicyStore(timeProvider: clock);
        Learn(store, "max-age=5");
        clock.Now += TimeSpan.FromSeconds(4);
        Learn(store, "max-age=5");
        clock.Now += TimeSpan.FromSeconds(4);
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
        clock.Now += TimeSpan.FromSeconds(1);
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
        Learn(store, "max-age=99999999999999999999999999999999999999");
        clock.Now = DateTimeOffset.MaxValue - TimeSpan.FromSeconds(1);
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
        Learn(store, "max-age=2");
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
    }

    [Fact]
    public void CapacityExpiresFirstAndNeverEvictsLiveProtection()
    {
        var clock = new Clock();
        var store = new HstsPolicyStore(maxEntries: 1, timeProvider: clock);
        Learn(store, "max-age=5");
        var error = Assert.Throws<ResourceLoadException>(() => Learn(store, "max-age=5", "other.test"));
        Assert.Equal(ResourceError.HstsCapacity, error.Error);
        Assert.Equal("https:", store.Upgrade(Url(scheme: "http")).Protocol);
        Learn(store, "max-age=5"); // Existing policy refresh is allowed while full.
        Learn(store, "max-age=0", "unknown.test"); // Removal needs no free slot.
        clock.Now += TimeSpan.FromSeconds(5);
        Learn(store, "max-age=60", "other.test");
        Assert.Equal("https:", store.Upgrade(Url("other.test", "http")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url(scheme: "http")).Protocol);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HstsPolicyStore(maxEntries: 0));
    }

    [Fact]
    public async Task ConcurrentWritersReadersAndRemovalsAreSafeAndBounded()
    {
        var store = new HstsPolicyStore(maxEntries: 8);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var host = $"host{i % 8}.test";
            for (var j = 0; j < 100; j++)
            {
                Learn(store, "max-age=60; includeSubDomains", host);
                store.Upgrade(Url($"a.{host}", "http"));
                Learn(store, "max-age=0", host);
            }
        }, TestContext.Current.CancellationToken)));
        for (var i = 0; i < 8; i++) { Learn(store, "max-age=60", $"host{i}.test"); }
        Assert.Throws<ResourceLoadException>(() => Learn(store, "max-age=60", "overflow.test"));
    }

    [Theory]
    [InlineData("http://user:pass@EXAMPLE.test:80/a%2Fb?q=%23#frag", "https://user:pass@example.test/a%2Fb?q=%23#frag")]
    [InlineData("http://example.test:8080/a?b#c", "https://example.test:8080/a?b#c")]
    [InlineData("http://example.test:443/", "https://example.test/")]
    [InlineData("http://example.test/?#", "https://example.test/?#")]
    [InlineData("http://example.test/#?", "https://example.test/#?")]
    public void UpgradePreservesUrlComponentsAndMapsOnlyPort80(string input, string expected)
    {
        var store = new HstsPolicyStore();
        Learn(store, "max-age=60");
        Assert.Equal(expected, store.Upgrade(BrowserUrl.Parse(input)).Href);
        var secure = BrowserUrl.Parse(expected);
        Assert.Same(secure, store.Upgrade(secure));
    }

    internal sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
