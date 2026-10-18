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
    public void LinkNavigationUsesTheExistingSessionHstsLoaderAndCommittedOrigin()
    {
        var store = new HstsPolicyStore();
        var requests = new List<Uri>();
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <!doctype html><style>*{margin:0}</style>
                    <a href='http://example.test/next#part'>link</a>
                    """, System.Text.Encoding.UTF8, "text/html")
            };
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60");
            return response;
        }), store), () => new StaticPageRenderer(font, 100000), isolateOrigins: true);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(100, 50, 1);
        controller.Navigate(tab.Id, "https://example.test/start");
        controller.Pump(_ => viewport);
        Assert.Null(tab.Error);
        var link = Assert.Single(controller.Page(tab.Id)!.LinkTargets);
        Assert.True(controller.ActivateLink(tab.Id, link.X + 1, link.Y + 1));
        controller.Pump(_ => viewport);
        Assert.Null(tab.Error);
        Assert.Equal("https://example.test/next#part", tab.History.Current!.Href);
        Assert.Equal("https://example.test", tab.Origin!.Serialize());
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("https", request.Scheme));
    }

    [Fact]
    public void CrossOriginFormAuthorizationUsesHstsResolvedTargetOrigin()
    {
        var store = new HstsPolicyStore();
        store.ProcessResponse(BrowserUrl.Parse("https://outside.test/"), ["max-age=60"], true, new List<string>());
        var requests = new List<Uri>();
        var font = Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Host == "source.test"
                    ? """
                        <!doctype html><style>*{margin:0}input{display:block;height:20px}</style>
                        <form action='http://outside.test/save'><input name=q value=private><button>Go</button></form>
                        """
                    : "<!doctype html>", System.Text.Encoding.UTF8, "text/html")
            };
        }), store), () => new StaticPageRenderer(font, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 100, 1);
        controller.Navigate(tab.Id, "http://source.test/start");
        PumpUntilComplete();
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));

        Assert.True(controller.ActivateFocusedLink(tab.Id));
        PumpUntilComplete();

        Assert.Equal(["http://source.test/start", "https://outside.test/save?q=private"],
            requests.Select(request => request.AbsoluteUri));
        Assert.Equal("https://outside.test/save?q=private", tab.History.Current!.Href);
        Assert.Equal("https://outside.test", tab.Origin!.Serialize());

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public async Task MainDocumentHttpNavigationSignalsUpgradeSupportButStylesheetsDoNot()
    {
        var requests = new List<(string Url, string? UpgradeHeader)>();
        using var source = new GetPageSource(new Handler(request =>
        {
            var url = request.RequestUri!;
            var upgradeHeader = request.Headers.TryGetValues("Upgrade-Insecure-Requests", out var values)
                ? Assert.Single(values)
                : null;
            requests.Add((url.AbsoluteUri, upgradeHeader));
            return url.AbsolutePath == "/site.css"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("*{margin:0}", System.Text.Encoding.UTF8, "text/css")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<!doctype html><link rel=stylesheet href=/site.css><p>page</p>",
                        System.Text.Encoding.UTF8, "text/html")
                };
        }));

        await source.LoadAsync(BrowserUrl.Parse("http://example.test/"), Token);

        Assert.Equal([
            ("http://example.test/", "1"),
            ("http://example.test/site.css", null)
        ], requests);
    }

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
