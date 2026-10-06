using System.Net;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Net;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class HstsNavigationTests
{
    private static readonly PageViewport Viewport = new(20, 20, 1);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InjectedStoreIsSharedAcrossSourcesButCookiesRemainDisabled()
    {
        var store = new HstsPolicyStore();
        var requests = new List<Uri>();
        HttpResponseMessage Respond(HttpRequestMessage request)
        {
            requests.Add(request.RequestUri!);
            Assert.False(request.Headers.Contains("Cookie"));
            var response = Html();
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "secret=value; Secure");
            return response;
        }

        using var first = new GetPageSource(new Handler(Respond), store);
        using var second = new GetPageSource(new Handler(Respond), store);
        await first.LoadAsync(BrowserUrl.Parse("https://example.test/learn"), Token);
        var page = await second.LoadAsync(BrowserUrl.Parse("http://example.test:80/other?q#f"), Token);
        Assert.Equal("https://example.test/other?q#f", page.Url.Href);
        Assert.Equal("https://example.test", page.Origin.Serialize());
        using var independent = new GetPageSource(new Handler(Respond), new HstsPolicyStore());
        await independent.LoadAsync(BrowserUrl.Parse("http://example.test/independent"), Token);
        Assert.Equal("http", requests[^1].Scheme);
    }

    [Fact]
    public async Task RedirectLearningFeedsAuthoritativeUpgradedUrlAndVisibleDiagnostics()
    {
        var store = new HstsPolicyStore();
        var calls = 0;
        using var source = new GetPageSource(new Handler(request =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            var response = Html();
            if (++calls == 1)
            {
                response.StatusCode = HttpStatusCode.Found;
                response.Headers.TryAddWithoutValidation("Location", "http://child.example.test:8080/final?q#f");
                response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60; includeSubDomains");
            }
            else
            {
                response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=1; max-age=2");
            }
            return response;
        }), store);
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/start"), Token);
        Assert.Equal("https://child.example.test:8080/final?q#f", page.Url.Href);
        Assert.Equal("https://child.example.test:8080", page.Origin.Serialize());
        Assert.Contains(page.Diagnostics, item => item.Contains("Strict-Transport-Security", StringComparison.Ordinal));
    }

    [Fact]
    public void AuthoritativeUpgradedFinalOriginRotatesTransactionallyAndFailureRetainsPriorDocument()
    {
        var store = new HstsPolicyStore();
        var renderers = new List<OriginIsolationTests.FakeRenderer>();
        var failNext = false;
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            var response = Html();
            if (request.RequestUri!.Scheme == "https")
            {
                response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60");
            }
            return response;
        }), store), () =>
        {
            var renderer = new OriginIsolationTests.FakeRenderer { Fail = failNext };
            renderers.Add(renderer);
            return renderer;
        }, isolateOrigins: true);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        Navigate("http://example.test/old");
        Assert.Equal("http://example.test", tab.Origin!.Serialize());
        var oldPage = controller.Page(tab.Id);
        // Another tab/session source can learn without changing the committed tab.
        store.ProcessResponse(BrowserUrl.Parse("https://example.test/"), ["max-age=60"], true, new List<string>());
        failNext = true;
        Navigate("http://example.test/fail");
        Assert.NotNull(tab.Error);
        Assert.Same(oldPage, controller.Page(tab.Id));
        Assert.Equal("http://example.test", tab.Origin.Serialize());
        Assert.Equal("http://example.test/old", tab.History.Current!.Href);
        Assert.Equal(0, renderers[0].Disposals);
        Assert.Equal(1, renderers[1].Disposals);
        failNext = false;
        Navigate("http://example.test/new");
        Assert.Null(tab.Error);
        Assert.Equal("https://example.test/new", tab.History.Current!.Href);
        Assert.Equal("https://example.test", tab.Origin.Serialize());
        Assert.Equal(1, renderers[0].Disposals);
        Navigate("http://example.test/same");
        Assert.Equal(3, renderers.Count); // Upgraded same-origin navigation reuses HTTPS renderer.
        Assert.Equal("https://example.test/same", tab.History.Current.Href);

        void Navigate(string input)
        {
            controller.Navigate(tab.Id, input);
            controller.Pump(_ => Viewport);
            Assert.False(tab.IsLoading);
        }
    }

    private static HttpResponseMessage Html() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("<!doctype html><title>HSTS</title>", System.Text.Encoding.UTF8, "text/html")
    };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
