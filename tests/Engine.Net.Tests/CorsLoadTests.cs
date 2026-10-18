using System.Net;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Net;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class CorsLoadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("https://document.example/", "https://cdn.example/style.css", "https://document.example")]
    [InlineData("https://document.example/", "https://cdn.example/style.css", "*")]
    public async Task CorsLoadSendsOriginAndAcceptsExactOrWildcardAllowOrigin(
        string requestOriginUrl, string resourceUrl, string allowedOrigin)
    {
        var seenHeaders = new List<string?>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seenHeaders.Add(request.Headers.GetValues("Origin").Single());
            Assert.False(request.Headers.Contains("Cookie"));
            return CorsResponse(allowedOrigin);
        }));

        var response = await loader.LoadCorsAsync(BrowserUrl.Parse(resourceUrl),
            BrowserUrl.Parse(requestOriginUrl).Origin, cancellationToken: Token);

        Assert.Equal(["https://document.example"], seenHeaders);
        Assert.Equal(200, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://other.example")]
    [InlineData("null")]
    [InlineData("*, https://document.example")]
    public async Task CorsLoadRejectsMissingMalformedOrMismatchedAllowOrigin(string? allowOrigin)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (allowOrigin is not null)
            {
                response.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin",
                    allowOrigin.Split(", ", StringSplitOptions.None));
            }
            return response;
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("https://cdn.example/style.css"),
            BrowserUrl.Parse("https://document.example/").Origin, cancellationToken: Token));

        Assert.Equal(ResourceError.CorsDenied, error.Error);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("data:text/plain,opaque")]
    [InlineData("ftp://document.example/")]
    public async Task CorsLoadRejectsOpaqueOrNonHttpRequestOriginsBeforeTransport(string requestOriginUrl)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return CorsResponse("*");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("https://cdn.example/style.css"),
            BrowserUrl.Parse(requestOriginUrl).Origin, cancellationToken: Token));

        Assert.Equal(ResourceError.CorsDenied, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CorsLoadRejectsNonHttpResourceBeforeTransport()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return CorsResponse("*");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("data:text/plain,secret"),
            BrowserUrl.Parse("https://document.example/").Origin, cancellationToken: Token));

        Assert.Equal(ResourceError.CorsDenied, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CorsLoadBlocksBadPortsBeforeTransport()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return CorsResponse("*");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("https://cdn.example:25/style.css"),
            BrowserUrl.Parse("https://document.example/").Origin, cancellationToken: Token));

        Assert.Equal(ResourceError.BlockedPort, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CorsLoadKeepsSameOriginRedirectsAndAuthorizesEveryHop()
    {
        var seen = new List<(string Url, string Origin)>();
        var authorized = new List<int>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add((request.RequestUri!.AbsoluteUri, request.Headers.GetValues("Origin").Single()));
            return seen.Count == 1 ? Redirect("https://cdn.example/moved.css") : CorsResponse("https://document.example");
        }));

        var response = await loader.LoadCorsAsync(BrowserUrl.Parse("https://cdn.example/style.css"),
            BrowserUrl.Parse("https://document.example/").Origin,
            (destination, redirectCount) =>
            {
                authorized.Add(redirectCount);
                return destination.Hostname == "cdn.example";
            }, Token);

        Assert.Equal([
            ("https://cdn.example/style.css", "https://document.example"),
            ("https://cdn.example/moved.css", "https://document.example")
        ], seen);
        Assert.Equal([0, 1], authorized);
        Assert.Equal("https://cdn.example/moved.css", response.Url.Href);
    }

    [Fact]
    public async Task CorsLoadBlocksCrossOriginRedirectBeforeSendingTheNextRequest()
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Redirect("https://attacker.example/steal.css");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("https://cdn.example/style.css"),
            BrowserUrl.Parse("https://document.example/").Origin, cancellationToken: Token));

        Assert.Equal(ResourceError.CorsDenied, error.Error);
        Assert.Equal(["https://cdn.example/style.css"], seen);
    }

    [Fact]
    public async Task CorsLoadFailsWhenBrowserAuthorizerRejectsARedirect()
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Redirect("https://cdn.example/moved.css");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => loader.LoadCorsAsync(
            BrowserUrl.Parse("https://cdn.example/style.css"),
            BrowserUrl.Parse("https://document.example/").Origin,
            (destination, redirectCount) => redirectCount == 0 && destination.Pathname == "/style.css",
            Token));

        Assert.Equal(ResourceError.NavigationDenied, error.Error);
        Assert.Equal(["https://cdn.example/style.css"], seen);
    }

    private static HttpResponseMessage CorsResponse(string allowedOrigin)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin", allowedOrigin);
        return response;
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
