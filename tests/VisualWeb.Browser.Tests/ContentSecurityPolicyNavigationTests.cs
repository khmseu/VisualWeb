using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ContentSecurityPolicyNavigationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FinalResponsePolicyBlocksInlineScriptBeforeStylesheetRequests()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/start" => Redirect("/page"),
                "/page" => Html("<script>document.title='blocked'</script><link rel=stylesheet href=site.css>",
                    "script-src 'self'"),
                _ => throw new InvalidOperationException("A stylesheet request escaped CSP validation.")
            };
        }));

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/start"), Cancellation));

        Assert.Contains("inline script", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["https://example.test/start", "https://example.test/page"], requests);
    }

    [Fact]
    public async Task MatchingNonceAllowsInlineClassicScript()
    {
        using var source = Serving("<script nonce='YWJj'>document.title='allowed'</script>", "script-src 'nonce-YWJj'");

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Contains("nonce='YWJj'", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MatchingHashAllowsExactInlineClassicScript()
    {
        using var source = Serving("<script>document.title='hashed'</script>",
            "script-src 'sha256-8Q56ftgtlXxkQr5RiA6Pw+WlboQEeqNCt3JbdRNi/+I='");

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Contains("document.title='hashed'", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyContentHashDoesNotAuthorizeNonemptyInlineScript()
    {
        using var source = Serving("<script>document.title='not empty'</script>",
            "script-src 'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='");

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("inline script", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HashMismatchBlocksInlineClassicScript()
    {
        using var source = Serving("<script>document.title='changed'</script>",
            "script-src 'sha256-8Q56ftgtlXxkQr5RiA6Pw+WlboQEeqNCt3JbdRNi/+I='");

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("inline script", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MatchingStyleHashAllowsOnlyStyleElementContent()
    {
        using var style = Serving("<style>body{color:red}</style>",
            "style-src 'sha512-fmZ7KIvKJWaEORLQ2z2++C7Hlc1fyYRXraeMLFwi38W00+Az3xZ+yFQsve16eKZwL0+yPrJQ0W/s+IT2XZ3i0Q=='");
        var page = await style.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Contains("body{color:red}", page.Html, StringComparison.Ordinal);

        using var attribute = Serving("<p style='body{color:red}'>blocked</p>",
            "style-src 'sha256-FcQqt3aNlV7AZnGV4zkQRVeCeJOxbMPnQSx258L803E='");
        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            attribute.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        Assert.Contains("inline style", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsafeHashesAuthorizesOnlyExactStyleAttributeText()
    {
        using var source = Serving("<p style='color:red'>allowed</p>",
            "style-src 'unsafe-hashes' 'sha256-8f935d27GvUutRyY9yWScUMiFUk4WTdZURISiYfPOeQ='");

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Contains("style='color:red'", page.Html, StringComparison.Ordinal);

        using var mismatch = Serving("<p style='color:blue'>blocked</p>",
            "style-src 'unsafe-hashes' 'sha256-8f935d27GvUutRyY9yWScUMiFUk4WTdZURISiYfPOeQ='");
        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            mismatch.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        Assert.Contains("inline style", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonMatchingNonceStillBlocksInlineClassicScript()
    {
        using var source = Serving("<script nonce='YWJk'>document.title='blocked'</script>", "script-src 'nonce-YWJj'");

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("inline script", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MatchingNonceAllowsStyleElementButNotStyleAttribute()
    {
        using var style = Serving("<style nonce='c3R5bGU='>body{color:red}</style>", "style-src 'nonce-c3R5bGU='");
        var page = await style.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Contains("nonce='c3R5bGU='", page.Html, StringComparison.Ordinal);

        using var attribute = Serving("<p style='color:red'>blocked</p>", "style-src 'nonce-c3R5bGU='");
        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            attribute.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        Assert.Contains("inline style", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<style>body{color:red}</style>")]
    [InlineData("<p style='color:red'>blocked</p>")]
    [InlineData("<p style>blocked</p>")]
    public async Task StyleDirectiveBlocksInlineStyleElementsAndAttributes(string markup)
    {
        using var source = Serving(markup, "style-src 'self'");

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("inline style", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefaultDirectiveBlocksBothInlineContentTypes()
    {
        using var scripts = Serving("<script></script>", "default-src 'none'");
        using var styles = Serving("<style></style>", "default-src 'none'");

        await Assert.ThrowsAsync<PageNavigationException>(() =>
            scripts.LoadAsync(BrowserUrl.Parse("https://example.test/script"), Cancellation));
        await Assert.ThrowsAsync<PageNavigationException>(() =>
            styles.LoadAsync(BrowserUrl.Parse("https://example.test/style"), Cancellation));
    }

    [Fact]
    public async Task MissingApplicableDirectiveAllowsInlineContentAndPublishesDiagnostics()
    {
        using var source = Serving("<script>document.title='allowed'</script>", "style-src 'self'; img-src 'none'");

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Contains(page.Diagnostics, item => item.Contains("img-src", StringComparison.Ordinal));
        Assert.Contains(page.Diagnostics, item => item.Contains("inline and external style", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StyleDirectiveBlocksLinkedStylesheetBeforeRequest()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return Html("<link rel=stylesheet href=site.css>", "style-src 'none'");
        }));

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("Content-Security-Policy", error.Message, StringComparison.Ordinal);
        Assert.Equal(["https://example.test/"], requests);
    }

    [Fact]
    public async Task StyleDirectiveAllowsSelfAndChecksImportedDataBeforeLoad()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/" => Html("<link rel=stylesheet href=site.css>", "style-src 'self'"),
                "/site.css" => Css("@import 'data:text/css,p{color:red}';"),
                _ => throw new InvalidOperationException("A denied imported stylesheet was requested.")
            };
        }));

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));

        Assert.Contains("Content-Security-Policy", error.Message, StringComparison.Ordinal);
        Assert.Contains("data:text/css", error.Message, StringComparison.Ordinal);
        Assert.Equal(["https://example.test/", "https://example.test/site.css"], requests);
    }

    [Fact]
    public async Task ExplicitDataSchemeAllowsDataImportedStylesheet()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/" => Html("<link rel=stylesheet href=site.css>", "style-src 'self' data:"),
                "/site.css" => Css("@import 'data:text/css,p%7Bcolor%3Ared%7D';"),
                _ => throw new InvalidOperationException("A data stylesheet unexpectedly used HTTP transport.")
            };
        }));

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Equal(2, page.Stylesheets.Count);
        Assert.Equal("p{color:red}", page.Stylesheets[1].Css);
        Assert.Equal(["https://example.test/", "https://example.test/site.css"], requests);
    }

    [Fact]
    public async Task HttpSchemeSourceAllowsSameOriginHttpsStylesheetUpgrade()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/" => Html("<link rel=stylesheet href=site.css>", "style-src http:"),
                "/site.css" => Css("p{color:red}"),
                _ => throw new InvalidOperationException("Unexpected stylesheet request.")
            };
        }));

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Single(page.Stylesheets);
        Assert.Equal(["https://example.test/", "https://example.test/site.css"], requests);
    }

    [Fact]
    public async Task ExactDocumentOriginAllowsLinkedAndImportedStylesheets()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath switch
            {
                "/" => Html("<link rel=stylesheet href=site.css>", "style-src https://example.test"),
                "/site.css" => Css("@import 'nested.css';"),
                "/nested.css" => Css("p{color:red}"),
                _ => throw new InvalidOperationException("Unexpected stylesheet request.")
            };
        }));

        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);

        Assert.Equal(2, page.Stylesheets.Count);
        Assert.Equal(["https://example.test/", "https://example.test/site.css", "https://example.test/nested.css"], requests);
    }

    private static GetPageSource Serving(string html, params string[] policies) =>
        new(new Handler(_ => Html(html, policies)));

    private static HttpResponseMessage Html(string body, params string[] policies)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!doctype html>" + body, Encoding.UTF8)
        };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
        response.Headers.TryAddWithoutValidation("Content-Security-Policy", policies);
        return response;
    }

    private static HttpResponseMessage Css(string body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8)
        };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/css");
        return response;
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
