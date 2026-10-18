using System.Net;
using System.Text;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class AuthorizedNavigationLoadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AuthorizerChecksInitialAndCrossOriginRedirectDestinations()
    {
        var authorized = new List<string>();
        var requests = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return requests.Count == 1
                ? Redirect("https://outside.example/final")
                : Text("ok");
        }));

        var response = await loader.LoadAuthorizedNavigationAsync(
            BrowserUrl.Parse("https://source.example/start"),
            url => { authorized.Add(url.Href); return true; },
            cancellationToken: Token);

        Assert.Equal(["https://source.example/start", "https://outside.example/final"], authorized);
        Assert.Equal(authorized, requests);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Fact]
    public async Task InsecureNavigationRequestsSendUpgradeHeaderOnHttpHopsOnly()
    {
        var requests = new List<(string Url, string? UpgradeHeader)>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            var upgradeHeader = request.Headers.TryGetValues("Upgrade-Insecure-Requests", out var values)
                ? Assert.Single(values)
                : null;
            requests.Add((url, upgradeHeader));
            return requests.Count switch
            {
                1 => Redirect("https://source.example/secure"),
                2 => Redirect("http://source.example/insecure"),
                _ => Text("ok")
            };
        }));

        await loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("http://source.example/start"),
            static _ => true, cancellationToken: Token, isNavigationRequest: true);

        Assert.Equal([
            ("http://source.example/start", "1"),
            ("https://source.example/secure", null),
            ("http://source.example/insecure", "1")
        ], requests);
    }

    [Fact]
    public async Task NonNavigationHttpLoadsDoNotSendUpgradeHeader()
    {
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            Assert.False(request.Headers.Contains("Upgrade-Insecure-Requests"));
            return Text("ok");
        }));

        await loader.LoadAsync(BrowserUrl.Parse("http://source.example/resource"), cancellationToken: Token);
    }

    [Fact]
    public async Task AuthorizerReceivesZeroForInitialUrlAndIncrementedRedirectCounts()
    {
        var authorized = new List<(string Url, int RedirectCount)>();
        var requests = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            requests.Add(url);
            return requests.Count < 3
                ? Redirect(requests.Count == 1
                    ? "https://source.example/next"
                    : "https://source.example/final")
                : Text("ok");
        }));

        var response = await loader.LoadAuthorizedNavigationAsync(
            BrowserUrl.Parse("https://source.example/start"),
            (url, redirectCount) =>
            {
                authorized.Add((url.Href, redirectCount));
                return true;
            },
            cancellationToken: Token);

        Assert.Equal(
            [
                ("https://source.example/start", 0),
                ("https://source.example/next", 1),
                ("https://source.example/final", 2)
            ],
            authorized);
        Assert.Equal(authorized.Select(item => item.Url), requests);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Fact]
    public async Task AsyncAuthorizerCanPauseARedirectBeforeTransport()
    {
        var requests = new List<string>();
        var redirectReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRedirect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return requests.Count == 1 ? Redirect("https://outside.example/final") : Text("ok");
        }));

        var load = loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("https://source.example/start"),
            async (destination, count, cancellationToken) =>
            {
                if (count == 1)
                {
                    Assert.Equal("https://outside.example/final", destination.Href);
                    redirectReached.TrySetResult();
                    await allowRedirect.Task.WaitAsync(cancellationToken);
                }
                return true;
            }, cancellationToken: Token);

        await redirectReached.Task.WaitAsync(Token);
        Assert.Equal(["https://source.example/start"], requests);
        allowRedirect.TrySetResult();
        var response = await load;

        Assert.Equal(["https://source.example/start", "https://outside.example/final"], requests);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Fact]
    public async Task InitialDenialPreventsTransport()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("https://denied.example/"), _ => false,
                includeCookies: true, cancellationToken: Token));

        Assert.Equal(ResourceError.NavigationDenied, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RedirectDenialPreventsDestinationTransport()
    {
        var authorized = new List<string>();
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Redirect("https://denied.example/final");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("https://allowed.example/start"), url =>
            {
                authorized.Add(url.Href);
                return url.Hostname == "allowed.example";
            }, includeCookies: true, cancellationToken: Token));

        Assert.Equal(ResourceError.NavigationDenied, error.Error);
        Assert.Equal(["https://allowed.example/start", "https://denied.example/final"], authorized);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task HstsAndBadPortChecksPrecedeAuthorization()
    {
        var store = new HstsPolicyStore();
        store.ProcessResponse(BrowserUrl.Parse("https://example.test/"), ["max-age=60"], true, new List<string>());
        var authorized = new List<string>();
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("ok");
        }), hstsPolicyStore: store);

        var response = await loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("http://example.test/start"), url =>
        {
            authorized.Add(url.Href);
            return true;
        }, cancellationToken: Token);
        var blocked = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("http://example.test:25/"), url =>
            {
                authorized.Add(url.Href);
                return true;
            }, cancellationToken: Token));

        Assert.Equal("https://example.test/start", response.Url.Href);
        Assert.Equal(ResourceError.BlockedPort, blocked.Error);
        Assert.Equal(["https://example.test/start"], authorized);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RewriteRunsAfterHstsAndBeforeAuthorizationForInitialAndRedirectDestinations()
    {
        var authorized = new List<string>();
        var requests = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return requests.Count == 1
                ? Redirect("http://example.test/final")
                : Text("ok");
        }));

        var response = await loader.LoadAuthorizedNavigationAsync(
            BrowserUrl.Parse("http://example.test/start"),
            url => { authorized.Add(url.Href); return true; },
            cancellationToken: Token,
            rewriteDestination: url => url.Protocol == "http:"
                ? BrowserUrl.Parse("https://" + url.Href["http://".Length..])
                : url);

        Assert.Equal(["https://example.test/start", "https://example.test/final"], authorized);
        Assert.Equal(authorized, requests);
        Assert.Equal("https://example.test/final", response.Url.Href);
    }

    [Fact]
    public async Task RewriteCannotReturnANonHttpDestination()
    {
        var authorized = 0;
        var requests = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            requests++;
            return Text("unexpected");
        }));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse("http://example.test/"), _ =>
            {
                authorized++;
                return true;
            }, cancellationToken: Token, rewriteDestination: _ => BrowserUrl.Parse("data:text/plain,denied")));

        Assert.Equal(ResourceError.NavigationDenied, error.Error);
        Assert.Equal(0, authorized);
        Assert.Equal(0, requests);
    }

    [Theory]
    [InlineData("data:text/plain,blocked")]
    [InlineData("file:///tmp/blocked")]
    public async Task NonHttpDestinationsAreDeniedBeforeAuthorization(string input)
    {
        var authorized = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ => Text("leak")));

        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAuthorizedNavigationAsync(BrowserUrl.Parse(input), _ => { authorized++; return true; },
                cancellationToken: Token));

        Assert.Equal(ResourceError.NavigationDenied, error.Error);
        Assert.Equal(0, authorized);
    }

    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.Found)
    {
        Headers = { Location = new Uri(location) }
    };

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8, "text/plain")
    };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
