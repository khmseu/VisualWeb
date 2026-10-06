using System.Net;
using System.Text;
using System.Text.Json;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

/// <summary>Fetch <see href="https://fetch.spec.whatwg.org/#block-bad-port">bad-port blocking</see> against the
/// independently pinned table in Data/bad-ports.json (extracted from the cached official standard).</summary>
public sealed class BadPortTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly int[] PinnedPorts = LoadPinnedPorts();

    private static int[] LoadPinnedPorts()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "bad-ports.json")));
        Assert.Equal("https://fetch.spec.whatwg.org/#bad-port", json.RootElement.GetProperty("source").GetString());
        return json.RootElement.GetProperty("badPorts").EnumerateArray().Select(row => row.GetProperty("port").GetInt32()).ToArray();
    }

    public static TheoryData<string, int, bool> EveryPinnedPort()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (var port in LoadPinnedPorts())
        {
            foreach (var scheme in new[] { "http", "https" })
            {
                data.Add(scheme, port, false);
                data.Add(scheme, port, true);
            }
        }

        return data;
    }

    [Fact]
    public void PinnedFixtureIsTheCompleteOfficialTable()
    {
        Assert.Equal(83, PinnedPorts.Length);
        Assert.Equal(PinnedPorts.Order().Distinct(), PinnedPorts);
        Assert.Equal(0, PinnedPorts[0]);
        Assert.Equal(10080, PinnedPorts[^1]);
        Assert.Equal(new[] { 6566, 6665, 6666, 6667, 6668, 6669, 6679, 6697 }, PinnedPorts.Where(port => port is > 6000 and < 10080));
    }

    [Theory]
    [MemberData(nameof(EveryPinnedPort))]
    public async Task EveryPinnedPortIsBlockedBeforeTransportOrCookieWork(string scheme, int port, bool sameOrigin)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var url = BrowserUrl.Parse($"{scheme}://example.org:{port}/path");
        Assert.Equal(port.ToString(System.Globalization.CultureInfo.InvariantCulture), url.Port);
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => sameOrigin
            ? loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token)
            : loader.LoadAsync(url, includeCookies: true, Token));
        Assert.Equal(ResourceError.BlockedPort, error.Error);
        Assert.Contains(port.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task ExhaustivePortSweepBlocksExactlyThePinnedTable(string scheme)
    {
        var sent = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            sent++;
            return Text("");
        }));
        var blocked = new List<int>();
        for (var port = 0; port <= 65535; port++)
        {
            try
            {
                await loader.LoadAsync(BrowserUrl.Parse($"{scheme}://example.org:{port}/"), cancellationToken: Token);
            }
            catch (ResourceLoadException exception) when (exception.Error == ResourceError.BlockedPort)
            {
                blocked.Add(port);
            }
        }

        Assert.Equal(PinnedPorts, blocked);
        Assert.Equal(65536 - PinnedPorts.Length, sent);
    }

    [Theory]
    [InlineData("http://example.org/", "http://example.org/")]
    [InlineData("http://example.org:80/", "http://example.org/")]
    [InlineData("http://example.org:00080/", "http://example.org/")]
    [InlineData("https://example.org/", "https://example.org/")]
    [InlineData("https://example.org:443/", "https://example.org/")]
    [InlineData("http://example.org:443/", "http://example.org:443/")]
    [InlineData("https://example.org:80/", "https://example.org:80/")]
    [InlineData("http://example.org:2/", "http://example.org:2/")]
    [InlineData("http://example.org:6/", "http://example.org:6/")]
    [InlineData("http://example.org:8/", "http://example.org:8/")]
    [InlineData("https://example.org:24/", "https://example.org:24/")]
    [InlineData("https://example.org:26/", "https://example.org:26/")]
    [InlineData("http://example.org:5999/", "http://example.org:5999/")]
    [InlineData("http://example.org:6001/", "http://example.org:6001/")]
    [InlineData("http://example.org:6664/", "http://example.org:6664/")]
    [InlineData("http://example.org:6670/", "http://example.org:6670/")]
    [InlineData("https://example.org:10079/", "https://example.org:10079/")]
    [InlineData("https://example.org:10081/", "https://example.org:10081/")]
    [InlineData("http://example.org:3000/", "http://example.org:3000/")]
    [InlineData("http://example.org:8080/", "http://example.org:8080/")]
    [InlineData("https://example.org:8443/", "https://example.org:8443/")]
    [InlineData("https://example.org:65535/", "https://example.org:65535/")]
    [InlineData("http://[0:0::1]:80/x", "http://[::1]/x")]
    [InlineData("https://[0:0:0:0:0:0:0:1]:443/x", "https://[::1]/x")]
    [InlineData("http://[::1]:8080/x", "http://[::1]:8080/x")]
    public async Task DefaultsAdjacentAndCustomPortsAreAllowedOnBothEntryPoints(string input, string expected)
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Text("ok");
        }));
        var url = BrowserUrl.Parse(input);
        var unrestricted = await loader.LoadAsync(url, cancellationToken: Token);
        var restricted = await loader.LoadSameOriginAsync(url, url.Origin, cancellationToken: Token);
        Assert.Equal(new[] { expected, expected }, seen);
        Assert.Equal("ok", Encoding.UTF8.GetString(unrestricted.Body.Span));
        Assert.Equal("ok", Encoding.UTF8.GetString(restricted.Body.Span));
    }

    [Theory]
    [InlineData("HTTP://EXAMPLE.org:0000/", "0")]
    [InlineData("hTtPs://%65xample.ORG:21/a", "21")]
    [InlineData("http://example.org:06000/%2e%2e/x", "6000")]
    [InlineData("https://b\u00FCcher.example:22/", "22")]
    [InlineData("http://0x7f.1:25/", "25")]
    [InlineData("http://[0:0::1]:6000/", "6000")]
    [InlineData("https://[::FFFF:127.0.0.1]:10080/", "10080")]
    [InlineData("http://[::1]:0/", "0")]
    public async Task NormalizedUrlsWithBadPortsAreBlockedOnBothEntryPoints(string input, string port)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var url = BrowserUrl.Parse(input);
        Assert.Equal(port, url.Port);
        foreach (var load in new Func<Task>[]
        {
            () => loader.LoadAsync(url, includeCookies: true, Token),
            () => loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token),
            () => loader.LoadSameOriginAsync(url, BrowserUrl.Parse("https://other.example/").Origin, cancellationToken: Token),
        })
        {
            var error = await Assert.ThrowsAsync<ResourceLoadException>(load);
            Assert.Equal(ResourceError.BlockedPort, error.Error);
        }

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("ftp://example.org:21/", ResourceError.UnsupportedScheme)]
    [InlineData("ws://example.org:6000/", ResourceError.UnsupportedScheme)]
    [InlineData("wss://example.org:25/", ResourceError.UnsupportedScheme)]
    [InlineData("gopher://example.org:70/", ResourceError.UnsupportedScheme)]
    public async Task NonHttpSchemesKeepTheirExistingErrors(string input, ResourceError expected)
    {
        var calls = 0;
        using var loader = new ResourceLoader(handler: new Handler(_ =>
        {
            calls++;
            return Text("leak");
        }));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: Token));
        Assert.Equal(expected, error.Error);
        var url = BrowserUrl.Parse(input);
        var denied = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadSameOriginAsync(url, BrowserUrl.Parse("https://example.org/").Origin, cancellationToken: Token));
        Assert.Equal(ResourceError.SameOriginDenied, denied.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DataAndFileUrlsAreUnaffected()
    {
        using var loader = new ResourceLoader(handler: new Handler(_ => throw new InvalidOperationException()));
        Assert.Equal("a:21", Encoding.UTF8.GetString(
            (await loader.LoadAsync(BrowserUrl.Parse("data:,a:21"), cancellationToken: Token)).Body.Span));
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "file", Token);
            var response = await loader.LoadAsync(BrowserUrl.Parse(new Uri(path).AbsoluteUri), cancellationToken: Token);
            Assert.Equal("file", Encoding.UTF8.GetString(response.Body.Span));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(301, false)]
    [InlineData(302, false)]
    [InlineData(303, false)]
    [InlineData(307, false)]
    [InlineData(308, false)]
    [InlineData(301, true)]
    [InlineData(302, true)]
    [InlineData(303, true)]
    [InlineData(307, true)]
    [InlineData(308, true)]
    public async Task RedirectsToBadPortsStopBeforeTheNextRequest(int status, bool sameOrigin)
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return Redirect(status, "//example.org:25/mail");
        }));
        var url = BrowserUrl.Parse("http://example.org/start");
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() => sameOrigin
            ? loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token)
            : loader.LoadAsync(url, includeCookies: true, Token));
        Assert.Equal(ResourceError.BlockedPort, error.Error);
        Assert.Equal(new[] { "http://example.org/start" }, seen);
    }

    [Fact]
    public async Task MultiHopRedirectStopsAtTheFirstBadPortWithoutCookieWorkForIt()
    {
        var seen = new List<(string Url, string? Cookie)>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add((request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null));
            var response = seen.Count switch
            {
                1 => Redirect(302, "http://example.org:8080/one"),
                2 => Redirect(307, "https://example.org:8443/two"),
                3 => Redirect(308, "HTTP://EXAMPLE.ORG:6667/irc"),
                _ => Text("unreachable"),
            };
            response.Headers.TryAddWithoutValidation("Set-Cookie", $"hop{seen.Count}=v");
            return response;
        }));
        var error = await Assert.ThrowsAsync<ResourceLoadException>(() =>
            loader.LoadAsync(BrowserUrl.Parse("http://example.org/start"), includeCookies: true, Token));
        Assert.Equal(ResourceError.BlockedPort, error.Error);
        Assert.Equal(new[] { "http://example.org/start", "http://example.org:8080/one", "https://example.org:8443/two" },
            seen.Select(hop => hop.Url));
        Assert.Equal(new[] { null, "hop1=v", "hop1=v; hop2=v" }, seen.Select(hop => hop.Cookie));
    }

    [Fact]
    public async Task BlockedInitialLoadNeitherSendsNorMutatesStoredCookies()
    {
        var seen = new List<(string Url, string? Cookie)>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add((request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null));
            var response = Text("ok");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "session=1");
            return response;
        }));
        await loader.LoadAsync(BrowserUrl.Parse("http://example.org/"), includeCookies: true, Token);
        foreach (var blocked in new[] { "http://example.org:6000/", "http://example.org:0/" })
        {
            var url = BrowserUrl.Parse(blocked);
            Assert.Equal(ResourceError.BlockedPort, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadAsync(url, includeCookies: true, Token))).Error);
            Assert.Equal(ResourceError.BlockedPort, (await Assert.ThrowsAsync<ResourceLoadException>(() =>
                loader.LoadSameOriginAsync(url, url.Origin, includeCookies: true, Token))).Error);
        }

        await loader.LoadAsync(BrowserUrl.Parse("http://example.org:8080/"), includeCookies: true, Token);
        Assert.Equal(new[] { ("http://example.org/", (string?)null), ("http://example.org:8080/", "session=1") }, seen);
    }

    [Fact]
    public async Task AllowedCustomPortRedirectChainsStillSucceed()
    {
        var seen = new List<string>();
        using var loader = new ResourceLoader(handler: new Handler(request =>
        {
            seen.Add(request.RequestUri!.AbsoluteUri);
            return seen.Count switch
            {
                1 => Redirect(301, "http://example.org:6001/"),
                2 => Redirect(303, "http://example.org:10081/"),
                _ => Text("done"),
            };
        }));
        var response = await loader.LoadAsync(BrowserUrl.Parse("http://example.org:5999/"), cancellationToken: Token);
        Assert.Equal(new[] { "http://example.org:5999/", "http://example.org:6001/", "http://example.org:10081/" }, seen);
        Assert.Equal("done", Encoding.UTF8.GetString(response.Body.Span));
        Assert.True(response.Redirected);
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
}
