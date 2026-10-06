using System.Net;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class HstsResourceLoaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static BrowserUrl Url(string value) => BrowserUrl.Parse(value);
    private static HttpResponseMessage Response(int status = 200, string? sts = null, string? location = null)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("ok") };
        if (sts is not null) { response.Headers.TryAddWithoutValidation("Strict-Transport-Security", sts); }
        if (location is not null) { response.Headers.TryAddWithoutValidation("Location", location); }
        return response;
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task EverySecureResponseStatusLearnsWithoutCookies(int status)
    {
        var store = new HstsPolicyStore();
        using var loader = new ResourceLoader(handler: new Handler(_ => Response(status, "max-age=60")), hstsPolicyStore: store);
        await loader.LoadAsync(Url("https://example.test/"), cancellationToken: Token);
        Assert.Equal("https:", store.Upgrade(Url("http://example.test/")).Protocol);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task EachRedirectStatusLearnsBeforeUpgradingItsOwnDestination(int status)
    {
        var calls = 0;
        var store = new HstsPolicyStore();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            return ++calls == 1 ? Response(status, "max-age=60", "http://example.test/next") : Response();
        }), hstsPolicyStore: store);
        var response = await loader.LoadAsync(Url("https://example.test/start#inherited"), cancellationToken: Token);
        Assert.Equal("https://example.test/next#inherited", response.Url.Href);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CapacityFailureIsExplicitAndDoesNotEvictOrSendCookiesToAnotherDestination()
    {
        var store = new HstsPolicyStore(maxEntries: 1);
        store.ProcessResponse(Url("https://retained.test/"), ["max-age=60"], true, new List<string>());
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Response(302, "max-age=60", "http://new.test/destination");
        }), hstsPolicyStore: store);
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(Url("https://new.test/"), includeCookies: true, cancellationToken: Token));
        Assert.Equal(ResourceError.HstsCapacity, error.Error);
        Assert.Equal(1, calls);
        Assert.Equal("https:", store.Upgrade(Url("http://retained.test/")).Protocol);
        Assert.Equal("http:", store.Upgrade(Url("http://new.test/")).Protocol);
    }

    [Fact]
    public async Task StandaloneDefaultIsOptInAndHttpCannotLearn()
    {
        using var standalone = new ResourceLoader(handler: new Handler(request =>
        {
            Assert.Equal("http", request.RequestUri!.Scheme);
            return Response(sts: "max-age=60");
        }));
        await standalone.LoadAsync(Url("http://example.test/"), cancellationToken: Token);
        await standalone.LoadAsync(Url("http://example.test/"), cancellationToken: Token);
        var store = new HstsPolicyStore();
        using var configured = new ResourceLoader(handler: new Handler(_ => Response(sts: "max-age=60")), hstsPolicyStore: store);
        await configured.LoadAsync(Url("http://example.test/"), cancellationToken: Token);
        Assert.Equal("http:", store.Upgrade(Url("http://example.test/")).Protocol);
    }

    [Fact]
    public async Task InitialAndRedirectUpgradeBeforeTransportPortPathAndSecureCookies()
    {
        var store = new HstsPolicyStore();
        var requests = new List<(string Url, string? Cookie)>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            requests.Add((request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Cookie", out var values) ? string.Join(";", values) : null));
            if (requests.Count == 1)
            {
                var response = Response(302, "max-age=60; includeSubDomains", "http://example.test:8080/path?query#new");
                response.Headers.TryAddWithoutValidation("Set-Cookie", "session=secure; Secure; Path=/");
                return response;
            }
            return Response();
        }), hstsPolicyStore: store);
        var redirected = await loader.LoadAsync(Url("https://example.test/start#old"), includeCookies: true, cancellationToken: Token);
        Assert.Equal("https://example.test:8080/path?query#new", redirected.Url.Href);
        Assert.True(redirected.Redirected);
        await loader.LoadAsync(Url("http://example.test:80/again?x#fragment"), includeCookies: true, cancellationToken: Token);
        Assert.Equal(("https://example.test:8080/path?query", "session=secure"), requests[1]);
        Assert.Equal(("https://example.test/again?x", "session=secure"), requests[2]);
    }

    [Fact]
    public async Task UpgradeComparesFixedOriginWithoutRebindingAndBlocksBadPortsWithoutIo()
    {
        var store = new HstsPolicyStore();
        store.ProcessResponse(Url("https://example.test/"), ["max-age=60"], true, new List<string>());
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ => { calls++; return Response(); }), hstsPolicyStore: store);
        var http = Url("http://example.test/");
        var denied = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(http, http.Origin, includeCookies: true, cancellationToken: Token));
        Assert.Equal(ResourceError.SameOriginDenied, denied.Error);
        var blocked = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(Url("http://example.test:25/"), http.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.BlockedPort, blocked.Error);
        Assert.Equal(0, calls);
        var allowed = await loader.LoadSameOriginAsync(http, Url("https://example.test/").Origin, cancellationToken: Token);
        Assert.Equal("https:", allowed.Url.Protocol);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("http://example.test:25/", ResourceError.BlockedPort)]
    [InlineData("http://other.test/", ResourceError.SameOriginDenied)]
    public async Task RedirectDestinationDenialSendsNothingToDestination(string location, ResourceError expected)
    {
        var calls = 0;
        var store = new HstsPolicyStore();
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Response(302, "max-age=60; includeSubDomains", location);
        }), hstsPolicyStore: store);
        var initial = Url("https://example.test/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(initial, initial.Origin, includeCookies: true, cancellationToken: Token));
        Assert.Equal(expected, error.Error);
        Assert.Equal(1, calls);
        Assert.Equal("https:", store.Upgrade(Url("http://example.test/")).Protocol);
    }

    [Fact]
    public async Task MalformedHeaderDiagnosticsSurviveRedirectsAndFirstHeaderWins()
    {
        var calls = 0;
        var store = new HstsPolicyStore();
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            var response = ++calls == 1 ? Response(302, location: "/next") : Response();
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security",
                calls == 1 ? ["max-age=1; MAX-AGE=2", "max-age=60"] : new[] { "max-age=60", "max-age=0" });
            return response;
        }), hstsPolicyStore: store);
        var result = await loader.LoadAsync(Url("https://example.test/"), cancellationToken: Token);
        Assert.Contains(result.Diagnostics, item => item.Contains("Strict-Transport-Security", StringComparison.Ordinal));
        Assert.Equal("https:", store.Upgrade(Url("http://example.test/")).Protocol);
    }

    [Fact]
    public async Task TransportFailureNeverLearnsOrFallsBackToHttp()
    {
        var calls = 0;
        var store = new HstsPolicyStore();
        store.ProcessResponse(Url("https://example.test/"), ["max-age=60"], true, new List<string>());
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            calls++;
            Assert.Equal("https", request.RequestUri!.Scheme);
            throw new HttpRequestException("certificate validation failed");
        }), hstsPolicyStore: store);
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(Url("http://example.test/"), cancellationToken: Token));
        Assert.Equal(ResourceError.Network, error.Error);
        Assert.Equal(1, calls);
        Assert.Equal("https:", store.Upgrade(Url("http://example.test/")).Protocol);
        await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(Url("https://unknown.test/"), cancellationToken: Token));
        Assert.Equal("http:", store.Upgrade(Url("http://unknown.test/")).Protocol);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
