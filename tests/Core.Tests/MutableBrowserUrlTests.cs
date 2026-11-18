using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class MutableBrowserUrlTests
{
    [Fact]
    public void SearchParamsObjectHasStableIdentity()
    {
        var url = new MutableBrowserUrl(BrowserUrl.Parse("https://example.org/path?first=1"));
        var searchParams = url.SearchParams;

        url.Search = "?second=2";
        Assert.Same(searchParams, url.SearchParams);

        url.Href = "https://example.net/other?third=3";
        Assert.Same(searchParams, url.SearchParams);
    }

    [Fact]
    public void ReplacingUrlQueryUpdatesTheExistingSearchParamsObject()
    {
        var url = new MutableBrowserUrl(BrowserUrl.Parse("https://example.org/path?first=1&first=2"));
        var searchParams = url.SearchParams;

        url.Search = "?next=a+b&flag";

        Assert.Same(searchParams, url.SearchParams);
        Assert.Equal(new[] { "a b" }, searchParams.GetAll("next"));
        Assert.True(searchParams.Has("flag", ""));
        Assert.Equal(2, searchParams.Size);
        Assert.Equal("https://example.org/path?next=a+b&flag", url.Href);
    }

    [Fact]
    public void SearchParamsMutationsUpdateUrlQueryAndPreserveOtherComponents()
    {
        var initialSnapshot = BrowserUrl.Parse("https://user:pass@example.org:8443/path?q=old#fragment");
        var url = new MutableBrowserUrl(initialSnapshot);

        url.SearchParams.Append("new key", "a&b");

        Assert.Equal(
            "https://user:pass@example.org:8443/path?q=old&new+key=a%26b#fragment",
            url.Href);
        Assert.Equal("https:", url.Snapshot.Protocol);
        Assert.Equal("user", url.Snapshot.Username);
        Assert.Equal("pass", url.Snapshot.Password);
        Assert.Equal("example.org:8443", url.Snapshot.Host);
        Assert.Equal("/path", url.Snapshot.Pathname);
        Assert.Equal("#fragment", url.Snapshot.Hash);
        Assert.Equal("https://user:pass@example.org:8443/path?q=old#fragment", initialSnapshot.Href);
    }

    [Fact]
    public void EmptyQueryAndEmptySearchParamsFollowUrlQuerySemantics()
    {
        var url = new MutableBrowserUrl(BrowserUrl.Parse("https://example.org/path?"));
        var searchParams = url.SearchParams;

        Assert.Equal("", url.Snapshot.Search);
        Assert.Equal(0, searchParams.Size);

        searchParams.Append("a", "1");
        Assert.Equal("https://example.org/path?a=1", url.Href);

        searchParams.Delete("a");
        Assert.Equal("https://example.org/path", url.Href);
        Assert.Same(searchParams, url.SearchParams);

        url.Search = "?";
        Assert.Equal("https://example.org/path?", url.Href);
        Assert.Equal(0, searchParams.Size);

        searchParams.Sort();
        Assert.Equal("https://example.org/path", url.Href);
    }

    [Fact]
    public void SortUpdatesSingleEntryQueryEvenWhenOrderIsUnchanged()
    {
        var url = new MutableBrowserUrl(BrowserUrl.Parse("https://example.org/path?a"));

        url.SearchParams.Sort();

        Assert.Equal("https://example.org/path?a=", url.Href);
    }

    [Fact]
    public void InvalidHrefReplacementLeavesUrlAndSearchParamsUnchanged()
    {
        var url = new MutableBrowserUrl(BrowserUrl.Parse("https://example.org/path?a=1#fragment"));
        var searchParams = url.SearchParams;
        var originalHref = url.Href;

        Assert.Throws<UrlParseException>(() =>
        {
            url.Href = "http://[";
        });

        Assert.Equal(originalHref, url.Href);
        Assert.Equal("1", searchParams.Get("a"));
        Assert.Same(searchParams, url.SearchParams);
    }
}
