using System.Net;
using System.Text;
using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Net;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class ResourceLoaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("data:,hello%20world#ignored", "hello world", "text/plain;charset=US-ASCII")]
    [InlineData("data:;charset=UTF-8,%E2%82%AC", "\u20AC", "text/plain;charset=UTF-8")]
    [InlineData("data:text/plain;base64,aGVsbG8", "hello", "text/plain")]
    [InlineData("data:text/plain; BASE64, YQ%3D%3D%0A", "a", "text/plain")]
    [InlineData("data:bad,abc?query", "abc?query", "text/plain;charset=US-ASCII")]
    [InlineData("data:,a+b%2B%ZZ", "a+b+%ZZ", "text/plain;charset=US-ASCII")]
    public async Task DataUrlDecodes(string input, string expected, string mime)
    {
        using var loader = new ResourceLoader();
        var response = await loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: Token);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expected, response.DecodeText(WebEncoding.ForLabel("UTF-8")).Text);
        Assert.Equal(mime, response.ContentType!.Serialize());
    }

    [Theory]
    [InlineData("data:hello", ResourceError.InvalidDataUrl)]
    [InlineData("data:;base64,A", ResourceError.InvalidDataUrl)]
    [InlineData("data:;base64,YQ===", ResourceError.InvalidDataUrl)]
    [InlineData("data:;base64,_w==", ResourceError.InvalidDataUrl)]
    [InlineData("ftp://example.org/", ResourceError.UnsupportedScheme)]
    [InlineData("file://remote.example/test", ResourceError.RemoteFile)]
    [InlineData("file:////remote.example/test", ResourceError.RemoteFile)]
    [InlineData("https://user:secret@example.org/", ResourceError.UrlCredentials)]
    public async Task InvalidInputsFailExplicitly(string input, ResourceError error)
    {
        using var loader = new ResourceLoader();
        var exception = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: Token));
        Assert.Equal(error, exception.Error);
    }

    [Fact]
    public async Task LocalFileLoadsAndMissingFileFails()
    {
        var file = Path.Combine(Path.GetTempPath(), "visualweb-net-" + Guid.NewGuid() + " \u00E4.txt");
        try
        {
            await File.WriteAllTextAsync(file, "local", Token);
            using var loader = new ResourceLoader();
            var response = await loader.LoadAsync(BrowserUrl.Parse(new Uri(file).AbsoluteUri + "?unused#fragment"), cancellationToken: Token);
            Assert.Equal("local", Encoding.UTF8.GetString(response.Body.Span));
            Assert.Null(response.ContentType);
            File.Delete(file);
            var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadAsync(BrowserUrl.Parse(new Uri(file).AbsoluteUri), cancellationToken: Token));
            Assert.Equal(ResourceError.FileAccess, error.Error);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task HttpRedirectsResolveRelativeAndInheritFragment(int status)
    {
        var seen = new List<string>();
        using var handler = new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return seen.Count == 1 ? Redirect(status, "../next") : Text("done");
        });
        using var loader = new ResourceLoader(handler: handler);
        var response = await loader.LoadAsync(BrowserUrl.Parse("https://example.org/a/start#old"), cancellationToken: Token);
        Assert.Equal(new[] { "https://example.org/a/start", "https://example.org/next" }, seen);
        Assert.Equal("https://example.org/next#old", response.Url.Href);
        Assert.True(response.Redirected);
        Assert.Equal("done", Encoding.UTF8.GetString(response.Body.Span));
    }

    [Theory]
    [InlineData("/next#", "https://example.org/next#")]
    [InlineData("/next#new", "https://example.org/next#new")]
    public async Task ExplicitRedirectFragmentOverridesOriginal(string location, string expected)
    {
        var count = 0;
        using var handler = new Handler(_ => count++ == 0 ? Redirect(302, location) : Text("ok"));
        using var loader = new ResourceLoader(handler: handler);
        var response = await loader.LoadAsync(BrowserUrl.Parse("https://example.org/#old"), cancellationToken: Token);
        Assert.Equal(expected, response.Url.Href);
    }

    [Theory]
    [InlineData("data:,secret", ResourceError.UnsupportedRedirect)]
    [InlineData("file:///tmp/secret", ResourceError.UnsupportedRedirect)]
    [InlineData("https://[", ResourceError.InvalidRedirect)]
    [InlineData("https://u:p@example.org/", ResourceError.UrlCredentials)]
    public async Task UnsafeOrInvalidRedirectsAreRejected(string location, ResourceError error)
    {
        using var handler = new Handler(_ => Redirect(302, location));
        using var loader = new ResourceLoader(handler: handler);
        var exception = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(error, exception.Error);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    public async Task RedirectLimitAllowsExactlyConfiguredCount(int limit)
    {
        var calls = 0;
        using var handler = new Handler(_ => ++calls <= limit ? Redirect(302, "/next") : Text("ok"));
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxRedirects = limit }, handler);
        Assert.True((await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token)).Redirected);
        Assert.Equal(limit + 1, calls);
        calls = 0;
        using var zero = new ResourceLoader(new ResourceLoaderOptions { MaxRedirects = 0 }, new Handler(_ => Redirect(302, "/")));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            zero.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(ResourceError.RedirectLimit, error.Error);
    }

    [Fact]
    public async Task DefaultRedirectLimitStopsBeforeTwentyFirstHop()
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Redirect(302, "/next");
        }));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(ResourceError.RedirectLimit, error.Error);
        Assert.Equal(21, calls);
    }

    [Fact]
    public async Task RedirectWithoutLocationAndHttpErrorsReturnResponses()
    {
        using var handler = new Handler(_ => Text("not found", 404));
        using var loader = new ResourceLoader(handler: handler);
        Assert.Equal(404, (await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token)).StatusCode);
        using var redirect = new ResourceLoader(handler: new Handler(_ => Text("no location", 302)));
        Assert.Equal(302, (await redirect.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpBodyLimitChecksDeclaredAndActualLength(bool declared)
    {
        using var handler = new Handler(_ =>
        {
            var response = Text("12345");
            if (!declared)
            {
                response.Content = new StreamContent(new TestStream(Encoding.ASCII.GetBytes("12345")));
                Assert.Null(response.Content.Headers.ContentLength);
            }

            return response;
        });
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxResponseBytes = 4 }, handler);
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(ResourceError.BodyLimit, error.Error);
    }

    [Theory]
    [InlineData("data:,1234", true)]
    [InlineData("data:,12345", false)]
    [InlineData("data:;base64,MTIzNA==", true)]
    [InlineData("data:;base64,MTIzNDU=", false)]
    public async Task DataBodyLimitIsExact(string input, bool valid)
    {
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxResponseBytes = 4 });
        if (valid)
        {
            Assert.Equal(4, (await loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: Token)).Body.Length);
        }
        else
        {
            Assert.Equal(ResourceError.BodyLimit, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: Token))).Error);
        }
    }

    [Fact]
    public async Task CookiesAreStoredOnRedirectAndRestrictedToHostPathAndSecureScheme()
    {
        var cookies = new List<string>();
        var count = 0;
        using var handler = new Handler(request =>
        {
            cookies.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : "");
            var response = count++ == 0 ? Redirect(302, "/private/next") : Text("ok");
            if (count == 1)
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", "session=one; Path=/private; Secure; HttpOnly");
            }

            return response;
        });
        using var loader = new ResourceLoader(handler: handler);
        await loader.LoadAsync(BrowserUrl.Parse("https://example.org/private/start"), includeCookies: true, cancellationToken: Token);
        await loader.LoadAsync(BrowserUrl.Parse("https://other.example/private/"), includeCookies: true, cancellationToken: Token);
        await loader.LoadAsync(BrowserUrl.Parse("http://example.org/private/"), includeCookies: true, cancellationToken: Token);
        await loader.LoadAsync(BrowserUrl.Parse("https://example.org/public/"), includeCookies: true, cancellationToken: Token);
        Assert.Equal(new[] { "", "session=one", "", "", "" }, cookies);
    }

    [Fact]
    public async Task CookiesAreOmittedByDefault()
    {
        var seen = new List<string>();
        using var handler = new Handler(request =>
        {
            seen.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : "");
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "id=1; Path=/");
            return response;
        });
        using var loader = new ResourceLoader(handler: handler);
        var url = BrowserUrl.Parse("https://example.org/");
        await loader.LoadAsync(url, cancellationToken: Token);
        await loader.LoadAsync(url, includeCookies: true, cancellationToken: Token);
        await loader.LoadAsync(url, includeCookies: true, cancellationToken: Token);
        await loader.LoadAsync(url, cancellationToken: Token);
        Assert.Equal(new[] { "", "", "id=1", "" }, seen);
    }

    [Fact]
    public async Task UnsupportedOrMalformedCookieAndMimeHaveDiagnostics()
    {
        using var handler = new Handler(_ =>
        {
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", new[] { "id=1; Domain=example.org", "=bad" });
            response.Content.Headers.Remove("Content-Type");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "invalid");
            return response;
        });
        using var loader = new ResourceLoader(handler: handler);
        var response = await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), includeCookies: true, cancellationToken: Token);
        Assert.Null(response.ContentType);
        Assert.Equal(3, response.Diagnostics.Count);
    }

    [Fact]
    public async Task CancellationIsNotConvertedToNetworkFailure()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cts.Cancel();
        using var loader = new ResourceLoader();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("data:,ok"), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task CookiesAreIsolatedBetweenLoadersAndDeletionIsHonored()
    {
        var seen = new List<string>();
        var calls = 0;
        HttpResponseMessage Respond(HttpRequestMessage request)
        {
            seen.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : "");
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", ++calls == 3 ? "id=; Max-Age=0; Path=/" : "id=1; Path=/");
            return response;
        }

        using var first = new ResourceLoader(handler: new Handler(Respond));
        using var second = new ResourceLoader(handler: new Handler(Respond));
        var url = BrowserUrl.Parse("https://example.org/");
        await first.LoadAsync(url, true, Token);
        await second.LoadAsync(url, true, Token);
        await first.LoadAsync(url, true, Token);
        await first.LoadAsync(url, true, Token);
        Assert.Equal(new[] { "", "", "id=1", "" }, seen);
    }

    [Theory]
    [InlineData("id=1; Secure", "http://example.org/")]
    [InlineData("__Secure-id=1", "https://example.org/")]
    [InlineData("__sEcUrE-id=1", "https://example.org/")]
    [InlineData("__Host-id=1; Secure; Path=/; Path=/wrong", "https://example.org/")]
    [InlineData("__Host-id=1; Secure; Path=/wrong; Path=/", "https://example.org/")]
    [InlineData("id=1; SameSite=Lax", "https://example.org/")]
    [InlineData("id=1; Partitioned; Secure", "https://example.org/")]
    public async Task UnsupportedCookiePoliciesAreNotSilentlyAccepted(string value, string input)
    {
        using var handler = new Handler(_ =>
        {
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", value);
            return response;
        });
        using var loader = new ResourceLoader(handler: handler);
        Assert.Single((await loader.LoadAsync(BrowserUrl.Parse(input), true, Token)).Diagnostics);
    }

    [Fact]
    public async Task InsecureRequestsCannotOverwriteSecureCookies()
    {
        var seen = new List<string>();
        using var handler = new Handler(request =>
        {
            seen.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : "");
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie",
                request.RequestUri!.Scheme == "https" ? "id=secure; Secure; Path=/" : "id=insecure; Path=/");
            return response;
        });
        using var loader = new ResourceLoader(handler: handler);
        await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), true, Token);
        Assert.Single((await loader.LoadAsync(BrowserUrl.Parse("http://example.org/"), true, Token)).Diagnostics);
        await loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), true, Token);
        Assert.Equal(new[] { "", "", "id=secure" }, seen);
    }

    [Fact]
    public async Task FileBodyLimitIsEnforced()
    {
        var file = Path.Combine(Path.GetTempPath(), "visualweb-net-" + Guid.NewGuid() + ".txt");
        try
        {
            await File.WriteAllTextAsync(file, "12345", Token);
            using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxResponseBytes = 4 });
            Assert.Equal(ResourceError.BodyLimit, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadAsync(BrowserUrl.Parse(new Uri(file).AbsoluteUri), cancellationToken: Token))).Error);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineCoversHeadersAndBody(bool body)
    {
        using var stream = new TestStream([], stall: true);
        using HttpMessageHandler handler = body ? new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(stream) })
            : new AsyncHandler(async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Text("unreachable");
            });
        using var loader = new ResourceLoader(new ResourceLoaderOptions { Timeout = TimeSpan.FromMilliseconds(100) }, handler);
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(ResourceError.Timeout, error.Error);
        if (body)
        {
            Assert.True(stream.IsDisposed);
        }
    }

    [Fact]
    public async Task CallerCancellationDuringSendRemainsCancellation()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var handler = new AsyncHandler(async token =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Text("unreachable");
        });
        using var loader = new ResourceLoader(handler: handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TransportAndBodyFailuresHaveExplicitNetworkDiagnostics()
    {
        using var transport = new ResourceLoader(handler: new Handler(_ => throw new HttpRequestException("test")));
        Assert.Equal(ResourceError.Network, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
            transport.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token))).Error);
        using var stream = new TestStream([], fail: true);
        using var body = new ResourceLoader(handler: new Handler(_ =>
            new(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        Assert.Equal(ResourceError.Network, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
            body.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token))).Error);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task StreamStopsAtOneByteBeyondLimitAndIsDisposed()
    {
        using var stream = new TestStream(new byte[100]);
        using var loader = new ResourceLoader(new ResourceLoaderOptions { MaxResponseBytes = 4 },
            new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("https://example.org/"), cancellationToken: Token));
        Assert.Equal(5, stream.BytesRead);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task DisposedLoaderAndInvalidOptionsFailExplicitly()
    {
        var defaults = new ResourceLoaderOptions();
        Assert.Equal(20, defaults.MaxRedirects);
        Assert.Equal(32 * 1024 * 1024, defaults.MaxResponseBytes);
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.Timeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceLoader(new() { MaxResponseBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceLoader(new() { MaxRedirects = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceLoader(new() { Timeout = TimeSpan.Zero }));
        var loader = new ResourceLoader();
        loader.Dispose();
        loader.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("data:,ok"), cancellationToken: Token));
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

    private sealed class TestStream(byte[] bytes, bool stall = false, bool fail = false) : Stream
    {
        public int BytesRead { get; private set; }
        public bool IsDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (fail)
            {
                throw new IOException("test body failure");
            }

            var count = Math.Min(buffer.Length, bytes.Length - BytesRead);
            bytes.AsSpan(BytesRead, count).CopyTo(buffer);
            BytesRead += count;
            return count;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stall)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return Read(buffer.Span);
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
