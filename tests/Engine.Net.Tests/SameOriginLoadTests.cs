using System.Net;
using System.Text;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class SameOriginLoadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("https://example.org/", "https://EXAMPLE.org:443/a?b#c", "https://example.org/a?b")]
    [InlineData("http://example.org:80/", "http://example.org/x", "http://example.org/x")]
    [InlineData("https://example.org:8443/", "https://example.org:8443/x", "https://example.org:8443/x")]
    [InlineData("https://b\u00FCcher.example/", "https://xn--bcher-kva.example/x", "https://xn--bcher-kva.example/x")]
    [InlineData("http://0x7f.1/", "http://127.0.0.1:80/x", "http://127.0.0.1/x")]
    [InlineData("http://[0:0::1]/", "http://[::1]:80/x", "http://[::1]/x")]
    public async Task SameOriginRequestsLoadWithNormalizedOriginComparison(string origin, string input, string sent)
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Text("ok");
        }));
        var response = await loader.LoadSameOriginAsync(BrowserUrl.Parse(input), BrowserUrl.Parse(origin).Origin,
            cancellationToken: Token);
        Assert.Equal(new[] { sent }, seen);
        Assert.Equal(200, response.StatusCode);
        Assert.False(response.Redirected);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Theory]
    [InlineData("https://example.org/", "http://example.org/")]
    [InlineData("http://example.org/", "https://example.org/")]
    [InlineData("https://example.org/", "https://www.example.org/")]
    [InlineData("https://www.example.org/", "https://example.org/")]
    [InlineData("https://example.org/", "https://example.org:8443/")]
    [InlineData("https://example.org:444/", "https://example.org/")]
    [InlineData("http://127.0.0.1/", "http://localhost/")]
    [InlineData("http://[::1]/", "http://127.0.0.1/")]
    public async Task SchemeHostOrPortMismatchIsDeniedWithoutTransportOrCookieWork(string origin, string input)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(BrowserUrl.Parse(input), BrowserUrl.Parse(origin).Origin, includeCookies: true, Token));
        Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("data:text/plain,secret")]
    [InlineData("file:///etc/hostname")]
    [InlineData("blob:https://example.org/00000000-0000-0000-0000-000000000000")]
    [InlineData("ftp://example.org/")]
    [InlineData("ws://example.org/")]
    [InlineData("about:blank")]
    [InlineData("javascript:alert(1)")]
    public async Task NonHttpUrlsAreDeniedBeforeAnyIo(string input)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var url = BrowserUrl.Parse(input);
        foreach (var origin in new[] { url.Origin, BrowserUrl.Parse("https://example.org/").Origin })
        {
            var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadSameOriginAsync(url, origin, cancellationToken: Token));
            Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        }

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("ftp://example.org/")]
    [InlineData("ws://example.org/")]
    [InlineData("wss://example.org/")]
    public async Task NonHttpTupleRequestOriginsAreDenied(string origin)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(BrowserUrl.Parse("https://example.org/"), BrowserUrl.Parse(origin).Origin,
                cancellationToken: Token));
        Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OpaqueRequestOriginsAreDeniedEvenForTheSameUrlInstance()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var data = BrowserUrl.Parse("data:text/plain,secret");
        var file = BrowserUrl.Parse("file:///tmp/secret");
        Assert.True(data.Origin.IsSameOrigin(data.Origin));
        foreach (var (url, origin) in new[]
        {
            (data, data.Origin),
            (file, file.Origin),
            (BrowserUrl.Parse("https://example.org/"), SecurityOrigin.CreateOpaque()),
            (BrowserUrl.Parse("https://example.org/"), data.Origin),
        })
        {
            var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadSameOriginAsync(url, origin, includeCookies: true, Token));
            Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        }

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task InvalidApiInputThrowsArgumentNull()
    {
        using var loader = new ResourceLoader(handler: new Handler(_ => Text("unused")));
        var origin = BrowserUrl.Parse("https://example.org/").Origin;
        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.LoadSameOriginAsync(null!, origin, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            loader.LoadSameOriginAsync(BrowserUrl.Parse("https://example.org/"), null!, cancellationToken: Token));
        loader.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            loader.LoadSameOriginAsync(BrowserUrl.Parse("https://example.org/"), origin, cancellationToken: Token));
    }

    [Fact]
    public async Task UrlCredentialsAreRejectedBeforeIo()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var url = BrowserUrl.Parse("https://user:pass@example.org/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.UrlCredentials, error.Error);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task SameOriginRelativeRedirectsAreFollowed(int status)
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return seen.Count switch
            {
                1 => Redirect(status, "../next"),
                2 => Redirect(status, "https://EXAMPLE.org:443/final?q"),
                _ => Text("done"),
            };
        }));
        var url = BrowserUrl.Parse("https://example.org/a/start#frag");
        var response = await loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token);
        Assert.Equal(new[] { "https://example.org/a/start", "https://example.org/next", "https://example.org/final?q" }, seen);
        Assert.Equal("https://example.org/final?q#frag", response.Url.Href);
        Assert.True(response.Redirected);
        Assert.Equal("done", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Theory]
    [InlineData(301, "https://other.example/")]
    [InlineData(302, "http://example.org/")]
    [InlineData(303, "https://example.org:8443/")]
    [InlineData(307, "//www.example.org/")]
    [InlineData(308, "https://other.example/")]
    [InlineData(302, "https://user:pass@other.example/")]
    public async Task CrossOriginRedirectsAreDeniedBeforeTheNextRequest(int status, string location)
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Redirect(status, location);
        }));
        var url = BrowserUrl.Parse("https://example.org/start");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token));
        Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        Assert.Equal(new[] { "https://example.org/start" }, seen);
    }

    [Fact]
    public async Task SameOriginRedirectWithCredentialsIsRejectedBeforeTheNextRequest()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Redirect(302, "https://user:pass@example.org/next");
        }));
        var url = BrowserUrl.Parse("https://example.org/start");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.UrlCredentials, error.Error);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("data:,secret", ResourceError.UnsupportedRedirect)]
    [InlineData("file:///tmp/secret", ResourceError.UnsupportedRedirect)]
    public async Task NonHttpRedirectsRemainRejected(string location, ResourceError expected)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Redirect(302, location);
        }));
        var url = BrowserUrl.Parse("https://example.org/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(expected, error.Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RequestOriginStaysFixedAcrossRedirectsNotThePrecedingUrl()
    {
        // The request origin is the caller's origin, not the starting URL's origin and not each hop's origin.
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Text("unreachable");
        }));
        var caller = BrowserUrl.Parse("https://caller.example/").Origin;
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(BrowserUrl.Parse("https://other.example/"), caller, cancellationToken: Token));
        Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        Assert.Empty(seen);
    }

    [Fact]
    public async Task MultiHopCookiesAreExactAndCrossToSameBounceNeverLeaks()
    {
        var seen = new List<(string Url, string Cookie)>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add((request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : ""));
            var response = seen.Count switch
            {
                1 => Redirect(302, "/second"),
                2 => Redirect(307, "/third"),
                3 => Redirect(308, "https://evil.example/bounce?to=https://example.org/fourth"),
                _ => Text("leak"),
            };
            if (seen.Count == 1)
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", "a=1; Path=/; Secure");
            }
            else if (seen.Count == 2)
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", "b=2; Path=/third");
            }

            return response;
        }));
        var url = BrowserUrl.Parse("https://example.org/first");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token));
        Assert.Equal(ResourceError.SameOriginDenied, error.Error);
        Assert.Equal(new[]
        {
            ("https://example.org/first", ""),
            ("https://example.org/second", "a=1"),
            ("https://example.org/third", "b=2; a=1"),
        }, seen);

        // Cookies stored during restricted hops remain in this loader's session only.
        var response = await loader.LoadSameOriginAsync(BrowserUrl.Parse("https://example.org/fourth"), url.Origin,
            includeCookies: true, Token);
        Assert.Equal("leak", Encoding.UTF8.GetString(response.Body.Span));
        Assert.Equal(("https://example.org/fourth", "a=1"), seen[^1]);
    }

    [Fact]
    public async Task CookiesRemainOmittedByDefaultOnRestrictedLoads()
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : "");
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "id=1; Path=/");
            return response;
        }));
        var url = BrowserUrl.Parse("https://example.org/");
        await loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token);
        await loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token);
        await loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token);
        await loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token);
        Assert.Equal(new[] { "", "", "id=1", "" }, seen);
    }

    [Fact]
    public async Task RedirectLimitIsRetained()
    {
        var calls = 0;
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxRedirects = 2 }, new Handler(_ =>
        {
            calls++;
            return Redirect(302, "/next");
        }));
        var url = BrowserUrl.Parse("https://example.org/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.RedirectLimit, error.Error);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task BodyLimitIsRetained()
    {
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxResponseBytes = 4 }, new Handler(_ => Text("12345")));
        var url = BrowserUrl.Parse("https://example.org/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.BodyLimit, error.Error);
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellation()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var loader = new ResourceLoader(handler: new AsyncHandler(async token =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Text("unreachable");
        }));
        var url = BrowserUrl.Parse("https://example.org/");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: cts.Token));
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: canceled.Token));
    }

    [Fact]
    public async Task DeadlineIsRetained()
    {
        using var loader = new ResourceLoader(new ResourceLoaderOptions { Timeout = TimeSpan.FromMilliseconds(50) },
            new AsyncHandler(async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Text("unreachable");
            }));
        var url = BrowserUrl.Parse("https://example.org/");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.Timeout, error.Error);
    }

    [Fact]
    public async Task DefaultLoadRemainsUnrestrictedTrustedNavigation()
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return seen.Count == 1 ? Redirect(302, "http://other.example:8080/final") : Text("ok");
        }));
        var response = await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token);
        Assert.Equal(new[] { "https://example.org/", "http://other.example:8080/final" }, seen);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body.Span));
        Assert.Equal("data", Encoding.UTF8.GetString(
            (await loader.LoadAsync(BrowserUrl.Parse("data:,data"), cancellationToken: Token)).Body.Span));
    }

    private static HttpResponseMessage Text(string body, int status = 200) =>
        new((HttpStatusCode)status) { Content = new StringContent(body) };

    private static HttpResponseMessage Redirect(int status, string location)
    {
        var response = Text("", status);
        response.Headers.TryAddWithoutValidation("Location", location);
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class AsyncHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(cancellationToken);
    }
}
