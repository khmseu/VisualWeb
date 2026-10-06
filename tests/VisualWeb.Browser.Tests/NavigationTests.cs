using System.Net;
using System.Text;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class NavigationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    [Fact]
    public async Task RedirectedNavigationUsesTheFinalResponseUrlOrigin()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            if (requests.Count == 1)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://destination.example:8443/final");
                return redirect;
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("<p>final</p>", Encoding.UTF8, "text/html") };
        }));
        var initialUrl = BrowserUrl.Parse("https://initial.example/start");
        var page = await source.LoadAsync(initialUrl, Cancellation);

        Assert.Equal(["https://initial.example/start", "https://destination.example:8443/final"], requests);
        Assert.Equal("https://destination.example:8443/final", page.Url.Href);
        Assert.Equal("https://destination.example:8443", page.Origin.Serialize());
        Assert.True(page.Origin.IsSameOrigin(page.Url.Origin));
        Assert.False(page.Origin.IsSameOrigin(initialUrl.Origin));
    }

    [Fact]
    public async Task TopLevelNavigationRemainsUnrestrictedAcrossOriginsAfterSameOriginLoaderMode()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            if (requests.Count == 2)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("http://third.example:8080/final");
                return redirect;
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("<p>page</p>", Encoding.UTF8, "text/html") };
        }));
        var first = await source.LoadAsync(BrowserUrl.Parse("https://first.example/"), Cancellation);
        var second = await source.LoadAsync(BrowserUrl.Parse("https://second.example/start"), Cancellation);

        Assert.Equal(["https://first.example/", "https://second.example/start", "http://third.example:8080/final"], requests);
        Assert.Equal("https://first.example", first.Origin.Serialize());
        Assert.Equal("http://third.example:8080", second.Origin.Serialize());
        Assert.False(second.Origin.IsSameOrigin(first.Origin));
    }

    [Fact]
    public async Task RepeatedDataLoadsUsingTheSameUrlHaveDistinctDocumentOrigins()
    {
        using var source = new GetPageSource();
        var url = BrowserUrl.Parse("data:text/html,%3Cp%3Ehello%3C/p%3E");
        var first = await source.LoadAsync(url, Cancellation);
        var second = await source.LoadAsync(url, Cancellation);

        Assert.Same(url, first.Url);
        Assert.Same(url, second.Url);
        Assert.True(first.Origin.IsOpaque);
        Assert.False(first.Origin.IsSameOrigin(second.Origin));
        Assert.NotEqual(first.DocumentId, second.DocumentId);
    }

    [Fact]
    public async Task RepeatedFileLoadsUsingTheSameUrlHaveDistinctDocumentOrigins()
    {
        using var source = new GetPageSource();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "<!doctype html><p>local</p>", Cancellation);
            var url = BrowserUrl.Parse(new Uri(path).AbsoluteUri);
            var first = await source.LoadAsync(url, Cancellation);
            var second = await source.LoadAsync(url, Cancellation);

            Assert.Same(url, first.Url);
            Assert.Same(url, second.Url);
            Assert.True(first.Origin.IsOpaque);
            Assert.False(first.Origin.IsSameOrigin(second.Origin));
            Assert.NotEqual(first.DocumentId, second.DocumentId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task HttpNavigationUsesHeaderEncodingAndBomPrecedenceWithoutCookies()
    {
        using var source = new GetPageSource(new Handler(request =>
        {
            Assert.False(request.Headers.Contains("Cookie"));
            var response = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new ByteArrayContent([0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("<!doctype html><p>café</p>")])
            };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html;charset=windows-1252");
            return response;
        }));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.com"), Cancellation);
        Assert.Contains("café", page.Html);
        Assert.Equal(404, page.StatusCode);
        Assert.Contains(page.Diagnostics, d => d.Contains("UTF-8", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("http://example.com:0/", 0)]
    [InlineData("https://example.com:6000/", 0)]
    [InlineData("http://example.com/redirect", 1)]
    public async Task BadPortNavigationFailsWithExplicitBlockedPortError(string address, int expectedRequests)
    {
        var requests = 0;
        using var source = new GetPageSource(new Handler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Content = new StringContent("") };
            response.Headers.TryAddWithoutValidation("Location", "https://example.com:10080/");
            return response;
        }));
        var error = await Assert.ThrowsAsync<VisualWeb.Engine.Net.ResourceLoadException>(() =>
            source.LoadAsync(BrowserUrl.Parse(address), Cancellation));
        Assert.Equal(VisualWeb.Engine.Net.ResourceError.BlockedPort, error.Error);
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal(expectedRequests, requests);
    }
    [Theory]
    [InlineData("image/png")]
    [InlineData("text/plain")]
    [InlineData("text/html;charset=not-an-encoding")]
    public async Task UnsupportedMimeAndCharsetDoNotProduceHtmlPages(string mime)
    {
        using var source = new GetPageSource(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<p>not html</p>") };
            response.Content.Headers.Remove("Content-Type");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", mime);
            return response;
        }));
        await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.com"), Cancellation));
    }
    [Fact]
    public async Task HtmlDataUrlAndExplicitLocalFileNavigateOffline()
    {
        using var source = new GetPageSource();
        var data = await source.LoadAsync(BrowserUrl.Parse("data:text/html,%3Cp%3Ehello%3C/p%3E"), Cancellation);
        Assert.Equal("<p>hello</p>", data.Html);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "<!doctype html><p>local</p>", Cancellation);
            var file = await source.LoadAsync(BrowserUrl.Parse(new Uri(path).AbsoluteUri), Cancellation);
            Assert.Contains("local", file.Html);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void EmbeddedStylesUseDocumentOrderAndRenderToExactNativePixels()
    {
        using var renderer = new StaticPageRenderer(FontPath, 1000);
        var html = "<!doctype html><title>Test</title><style>body{margin:0;background-color:red}</style>"
            + "<style>body{background-color:blue}</style>";
        var page = renderer.Render(new(BrowserUrl.Parse("data:text/html,test"), html, 200, []), new(20, 20, 1), Cancellation);
        Assert.Equal("Test", page.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, page.Frame.Pixels.Span[..4].ToArray());
    }
    [Theory]
    [InlineData("<link rel='alternate stylesheet' href='https://example.com/x'>")]
    [InlineData("<style media='screen'>p{color:red}</style>")]
    [InlineData("<style title='alternate'>p{color:red}</style>")]
    [InlineData("<style type='text/other'>p{color:red}</style>")]
    [InlineData("<style type=' '>p{color:red}</style>")]
    public void DeferredStylesheetSurfacesThrowInsteadOfSilentlySkipping(string html)
    {
        var document = HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
        Assert.Throws<PageNavigationException>(() => StaticPageRenderer.CollectStyles(document, cancellationToken: Cancellation));
    }
    [Fact]
    public void EmbeddedSourceBudgetsAreExactAndAuthorStylesAreNotOverridden()
    {
        var parsed = HtmlParser.Parse("<!doctype html><style>a{color:red}</style><style>b{color:red}</style>", cancellationToken: Cancellation);
        Assert.Equal(2, StaticPageRenderer.CollectStyles(parsed.Document, new() { MaxStyleSources = 2, MaxInputCharacters = 24 }, Cancellation).Count);
        Assert.Throws<CssLimitException>(() => StaticPageRenderer.CollectStyles(parsed.Document, new() { MaxStyleSources = 1 }, Cancellation));
        Assert.Throws<CssLimitException>(() => StaticPageRenderer.CollectStyles(parsed.Document, new() { MaxInputCharacters = 23 }, Cancellation));
        using var renderer = new StaticPageRenderer(FontPath, 1000);
        Assert.Throws<VisualWeb.Engine.Layout.UnsupportedLayoutException>(() => renderer.Render(
            new(BrowserUrl.Parse("data:text/html,test"), "<!doctype html><p>Author margin</p>", 200, []), new(20, 20, 1), Cancellation));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
