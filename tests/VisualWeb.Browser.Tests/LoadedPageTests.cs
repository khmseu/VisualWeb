using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class LoadedPageTests
{
    [Theory]
    [InlineData("data:text/html,test")]
    [InlineData("file:///document.html")]
    [InlineData("about:blank")]
    public void NewDocumentsHaveFreshOpaqueOriginsEvenWhenTheUrlIsReused(string href)
    {
        var url = BrowserUrl.Parse(href);
        var first = new LoadedPage(url, "test", 200, []);
        var second = new LoadedPage(url, "test", 200, []);

        Assert.True(first.Origin.IsOpaque);
        Assert.Same(first.Origin, first.Origin);
        Assert.False(first.Origin.IsSameOrigin(url.Origin));
        Assert.False(first.Origin.IsSameOrigin(second.Origin));
        Assert.NotEqual(first.DocumentId, second.DocumentId);
    }

    [Fact]
    public void TupleOriginsUseTheFinalUrlAndCompareByEffectivePort()
    {
        var first = new LoadedPage(BrowserUrl.Parse("https://example.com:443/a"), "", 200, []);
        var second = new LoadedPage(BrowserUrl.Parse("https://example.com/b"), "", 200, []);
        var otherPort = new LoadedPage(BrowserUrl.Parse("https://example.com:8443/b"), "", 200, []);

        Assert.Same(first.Url.Origin, first.Origin);
        Assert.Equal(first.Origin, second.Origin);
        Assert.True(first.Origin.IsSameOrigin(second.Origin));
        Assert.False(first.Origin.IsSameOrigin(otherPort.Origin));
        Assert.NotEqual(first.DocumentId, second.DocumentId);
    }

    [Theory]
    [InlineData("https://EXAMPLE.COM:443/a", "https://example.com/b")]
    [InlineData("http://EXAMPLE.com:8080/a", "http://example.com:8080/b")]
    [InlineData("http://B\u00FCcher.example:8080/a", "http://xn--bcher-kva.example:8080/b")]
    [InlineData("http://%45xample.com:8080/a", "http://example.com:8080/b")]
    [InlineData("http://0x7f.1:8080/a", "http://127.0.0.1:8080/b")]
    public void TupleOriginsNormalizeHostsWithExplicitPorts(string left, string right)
    {
        var first = new LoadedPage(BrowserUrl.Parse(left), "", 200, []);
        var second = new LoadedPage(BrowserUrl.Parse(right), "", 200, []);

        Assert.Equal(second.Origin.Host, first.Origin.Host);
        Assert.Equal(first.Origin, second.Origin);
        Assert.True(first.Origin.IsSameOrigin(second.Origin));
    }

    [Theory]
    [InlineData("data:text/html,test")]
    [InlineData("https://example.com/test")]
    public void OrdinaryRecordCopiesRetainUrlOriginAndDocumentIdentity(string href)
    {
        var page = new LoadedPage(Url: BrowserUrl.Parse(href), Html: "", StatusCode: 200, Diagnostics: []);
        var copy = page with { Html = "metadata copy", StatusCode = 201, Diagnostics = ["updated"] };
        var (url, html, statusCode, diagnostics) = copy;

        Assert.NotSame(page, copy);
        Assert.Same(page.Url, copy.Url);
        Assert.Same(page.Origin, copy.Origin);
        Assert.Equal(page.DocumentId, copy.DocumentId);
        Assert.Same(copy.Url, url);
        Assert.Equal("metadata copy", html);
        Assert.Equal(201, statusCode);
        Assert.Equal(["updated"], diagnostics);
    }

    [Fact]
    public void UrlAndOriginCannotBeChangedIndependentlyByRecordInitialization()
    {
        Assert.Null(typeof(LoadedPage).GetProperty(nameof(LoadedPage.Url))!.SetMethod);
        Assert.Null(typeof(LoadedPage).GetProperty(nameof(LoadedPage.Origin))!.SetMethod);
    }
}
