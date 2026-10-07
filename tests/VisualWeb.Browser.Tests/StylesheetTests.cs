using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Net;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class StylesheetTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static readonly PageViewport Viewport = new(20, 10, 1);
    private static readonly byte[] Blue = [255, 0, 0, 255];
    private static readonly byte[] Red = [0, 0, 255, 255];
    private static readonly byte[] Lime = [0, 255, 0, 255];

    [Fact]
    public async Task BrowserFetchesLinkedStylesheetsRelativeToTheFinalRedirectedDocumentUrl()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            Assert.False(request.Headers.Contains("Cookie"));
            return request.RequestUri.AbsoluteUri switch
            {
                "https://initial.example/start" => Redirect("https://final.example/dir/page"),
                "https://final.example/dir/page" => Text("<!doctype html><link rel=stylesheet href=a.css>"
                    + "<link rel='Preload StyleSheet' href='/b.css#x' type=TEXT/CSS media=' ALL '>", "text/html"),
                "https://final.example/dir/a.css" => Redirect("https://cdn.example/moved.css"),
                "https://cdn.example/moved.css" => Text("p{color:red}", "text/css"),
                "https://final.example/b.css" => Text("body{margin:0}", "text/css; charset=utf-8"),
                _ => new(HttpStatusCode.NotFound)
            };
        }));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://initial.example/start"), Cancellation);
        Assert.Equal(["https://initial.example/start", "https://final.example/dir/page", "https://final.example/dir/a.css",
            "https://cdn.example/moved.css", "https://final.example/b.css"], requests);
        Assert.Equal([new("https://final.example/dir/a.css", "p{color:red}"), new PageStylesheet("https://final.example/b.css#x", "body{margin:0}")],
            page.Stylesheets);
        Assert.Contains(page.Diagnostics, d => d.StartsWith("Linked stylesheets: 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DocumentsWithoutLinkedStylesheetsMakeNoSubresourceRequests()
    {
        var requests = 0;
        using var source = new GetPageSource(new Handler(_ =>
        {
            requests++;
            return Text("<!doctype html><link rel=stylesheet><link rel=stylesheet href=''><link rel=icon href=i.css>", "text/html");
        }));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Equal(1, requests);
        Assert.Empty(page.Stylesheets);
    }

    [Fact]
    public async Task CharacterReferenceEncodedRelIsDiscoveredAndFetched()
    {
        var requests = 0;
        using var source = new GetPageSource(new Handler(_ =>
        {
            requests++;
            return requests == 1
                ? Text("<!doctype html><link rel=&#115;tylesheet href=s.css>", "text/html")
                : Text("body{margin:0}", "text/css");
        }));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Equal(2, requests);
        Assert.Equal(new PageStylesheet("https://example.test/s.css", "body{margin:0}"), Assert.Single(page.Stylesheets));
    }

    [Fact]
    public void InlineAndLinkedSourcesKeepDocumentOrder()
    {
        var document = HtmlParser.Parse("""
            <!doctype html><style>a{}</style><link rel=stylesheet href=one.css><style>b{}</style>
            <link rel=stylesheet href=two.css><link rel=stylesheet href=one.css><style>c{}</style>
            """, cancellationToken: Cancellation).Document;
        var url = BrowserUrl.Parse("https://example.test/dir/page");
        Assert.Equal(["https://example.test/dir/one.css", "https://example.test/dir/two.css"],
            StaticPageRenderer.DiscoverStylesheets(document, url, Cancellation));
        var sources = StaticPageRenderer.CollectStyles(document, null, Cancellation, url,
            [new("https://example.test/dir/two.css", "two{}"), new("https://example.test/dir/one.css", "one{}")]);
        Assert.Equal(["a{}", "one{}", "b{}", "two{}", "one{}", "c{}"], sources.Select(source => source.Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalAndProcessRenderersApplyIdenticalInterleavedCascade(bool process)
    {
        using IPageRenderer renderer = process ? new ProcessPageRenderer(RendererPath, FontPath) : new StaticPageRenderer(FontPath, 100000);
        Assert.Equal(Blue, await Pixel(renderer, "<style>body{margin:0;background-color:red}</style><link rel=stylesheet href=s.css>",
            "body{background-color:blue}"));
        Assert.Equal(Red, await Pixel(renderer, "<link rel=stylesheet href=s.css><style>body{margin:0;background-color:red}</style>",
            "body{background-color:blue}"));
        Assert.Equal(Lime, await Pixel(renderer, "<style>body{margin:0}</style><link rel=stylesheet href=s.css>"
            + "<link rel=stylesheet href=t.css>", "body{background-color:blue}", "body{background-color:lime}"));
        var ex = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Page("<style>body{margin:0}</style><link rel=stylesheet href=missing.css>", "body{}"), Viewport, Cancellation));
        Assert.Contains("not provided", ex.Message);
    }

    [Fact]
    public async Task LocalAndProcessFramesAreByteIdentical()
    {
        var page = Page("<style>body{margin:0}</style><link rel=stylesheet href=s.css><p>Text</p><link rel=stylesheet href=t.css>",
            "p{color:red;margin:0}", "body{background-color:lime}");
        using var process = new ProcessPageRenderer(RendererPath, FontPath);
        var actual = await process.RenderAsync(page, new(60, 30, 1), Cancellation);
        BrowserPage expected;
        // Native font owners are thread-affine: create, render and dispose without an intervening await.
        using (var local = new StaticPageRenderer(FontPath, 100000)) { expected = local.Render(page, new(60, 30, 1), Cancellation); }
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(expected.Status, actual.Status);
    }

    [Theory]
    [InlineData("<link rel='alternate stylesheet' title=x href=s.css>")]
    [InlineData("<link rel=stylesheet title=named href=s.css>")]
    [InlineData("<link rel=stylesheet disabled href=s.css>")]
    [InlineData("<link rel=stylesheet media=screen href=s.css>")]
    [InlineData("<link rel=stylesheet media='' href=s.css>")]
    [InlineData("<link rel=stylesheet type=text/plain href=s.css>")]
    [InlineData("<link rel=stylesheet type='text/css; charset=utf-8' href=s.css>")]
    [InlineData("<link rel=stylesheet crossorigin href=s.css>")]
    [InlineData("<link rel=stylesheet integrity=sha256-x href=s.css>")]
    [InlineData("<link rel=stylesheet referrerpolicy=no-referrer href=s.css>")]
    [InlineData("<link rel=stylesheet charset=windows-1252 href=s.css>")]
    [InlineData("<base href=/other/><link rel=stylesheet href=s.css>")]
    [InlineData("<link rel=stylesheet href=s.css><base href=/other/>")]
    [InlineData("<link rel=stylesheet href='https://[bad/'>")]
    [InlineData("<link rel=stylesheet href='file:///etc/passwd'>")]
    [InlineData("<link rel=stylesheet href='javascript:alert(1)'>")]
    [InlineData("<link rel=stylesheet href='about:blank'>")]
    [InlineData("<link rel=stylesheet href='http://example.test:6000/s.css'>")]
    public async Task UnsupportedLinkedSheetSemanticsFailExplicitlyWithoutFetching(string markup)
    {
        var requests = new List<Uri>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count == 1 ? Text("<!doctype html>" + markup, "text/html") : Text("p{}", "text/css");
        }));
        await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("http://example.test/page"), Cancellation));
        Assert.Single(requests);
        var document = HtmlParser.Parse("<!doctype html>" + markup, cancellationToken: Cancellation).Document;
        Assert.Throws<PageNavigationException>(() => StaticPageRenderer.CollectStyles(document, null, Cancellation,
            BrowserUrl.Parse("http://example.test/page"), [new("http://example.test/s.css", "p{}")]));
    }

    [Fact]
    public async Task LocalFileDocumentCannotLoadLinkedFileStylesheets()
    {
        var htmlPath = Path.GetTempFileName();
        var cssPath = htmlPath + ".css";
        try
        {
            await File.WriteAllTextAsync(htmlPath, "<!doctype html><link rel=stylesheet href='" + Path.GetFileName(cssPath) + "'>", Cancellation);
            await File.WriteAllTextAsync(cssPath, "body{color:red}", Cancellation);
            using var source = new GetPageSource();

            var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
                source.LoadAsync(BrowserUrl.Parse(new Uri(htmlPath).AbsoluteUri), Cancellation));

            Assert.Contains("Linked file stylesheets are blocked", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(htmlPath);
            File.Delete(cssPath);
        }
    }

    [Theory]
    [InlineData("http://internal.example/style.css")]
    [InlineData("https://internal.example/style.css")]
    public async Task LocalFileDocumentCannotTriggerNetworkStylesheetRequests(string stylesheetUrl)
    {
        var htmlPath = Path.GetTempFileName();
        var requests = new List<string>();
        try
        {
            await File.WriteAllTextAsync(htmlPath,
                "<!doctype html><link rel=stylesheet href='" + stylesheetUrl + "'>", Cancellation);
            using var source = new GetPageSource(new Handler(request =>
            {
                requests.Add(request.RequestUri!.AbsoluteUri);
                return Text("body{color:red}", "text/css");
            }));

            var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
                source.LoadAsync(BrowserUrl.Parse(new Uri(htmlPath).AbsoluteUri), Cancellation));

            Assert.Contains("blocked from file: documents", error.Message, StringComparison.Ordinal);
            Assert.Empty(requests);
        }
        finally { File.Delete(htmlPath); }
    }

    [Fact]
    public async Task OpaqueDataDocumentCannotTriggerNetworkStylesheetRequests()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return Text("body{color:red}", "text/css");
        }));
        var html = "<!doctype html><link rel=stylesheet href='https://internal.example/style.css'>";

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("data:text/html," + Uri.EscapeDataString(html)), Cancellation));

        Assert.Contains("blocked from data: documents", error.Message, StringComparison.Ordinal);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task HttpsDocumentDoesNotFetchHttpStylesheet()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return Text("<!doctype html><link rel=stylesheet href='http://insecure.example/s.css'>", "text/html");
        }));

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://secure.example/"), Cancellation));

        Assert.Contains("HTTPS", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["https://secure.example/"], requests);
    }

    [Fact]
    public async Task HttpsStylesheetRedirectToHttpIsBlockedBeforeDowngradeRequest()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri!.AbsoluteUri switch
            {
                "https://secure.example/" => Text("<!doctype html><link rel=stylesheet href=s.css>", "text/html"),
                "https://secure.example/s.css" => Redirect("http://insecure.example/s.css"),
                _ => throw new InvalidOperationException("An insecure stylesheet request was sent.")
            };
        }));

        var error = await Assert.ThrowsAsync<PageNavigationException>(() =>
            source.LoadAsync(BrowserUrl.Parse("https://secure.example/"), Cancellation));

        Assert.Contains("HTTPS", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["https://secure.example/", "https://secure.example/s.css"], requests);
    }

    [Theory]
    [InlineData("text/plain", 200)]
    [InlineData(null, 200)]
    [InlineData("text/html", 200)]
    [InlineData("text/css", 404)]
    [InlineData("text/css", 500)]
    [InlineData("text/css; charset=bogus-label", 200)]
    public async Task StylesheetResponsesRequireOkTextCssAndKnownCharsets(string? contentType, int status)
    {
        using var source = Serving("<!doctype html><link rel=stylesheet href=s.css>", _ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new ByteArrayContent("p{}"u8.ToArray()) };
            if (contentType is not null) { response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType); }
            return response;
        });
        await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
    }

    [Theory]
    [InlineData("text/css; charset=windows-1252", new byte[] { 0x70, 0xE9 }, "windows-1252 HTML", "p\u00e9")]
    [InlineData("text/css", new byte[] { 0x40, 0x63, 0x68, 0x61, 0x72, 0x73, 0x65, 0x74, 0x20, 0x22, 0x6C, 0x61, 0x74, 0x69, 0x6E, 0x31, 0x22, 0x3B, 0xE9 },
        "utf-8 HTML", "@charset \"latin1\";\u00e9")]
    [InlineData("text/css", new byte[] { 0x40, 0x63, 0x68, 0x61, 0x72, 0x73, 0x65, 0x74, 0x20, 0x22, 0x75, 0x74, 0x66, 0x2D, 0x31, 0x36, 0x22, 0x3B, 0xC3, 0xA9 },
        "windows-1252 HTML", "@charset \"utf-16\";\u00e9")]
    [InlineData("text/css; charset=windows-1252", new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xA9 }, "utf-8 HTML", "\u00e9")]
    [InlineData("text/css", new byte[] { 0xE9 }, "windows-1252 HTML", "\u00e9")]
    [InlineData("text/css", new byte[] { 0xC3, 0xA9 }, "utf-8 HTML", "\u00e9")]
    public async Task StylesheetDecodingFollowsBomTransportCharsetDeclarationAndDocumentEncoding(string contentType, byte[] body,
        string document, string expected)
    {
        var html = document == "utf-8 HTML" ? "text/html; charset=utf-8" : "text/html; charset=windows-1252";
        using var source = new GetPageSource(new Handler(request => request.RequestUri!.AbsolutePath == "/s.css"
            ? Bytes(body, contentType) : Text("<!doctype html><link rel=stylesheet href=s.css>", html)));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Equal(expected, Assert.Single(page.Stylesheets).Css);
    }

    [Theory]
    [InlineData("text/css; charset=utf-7")]
    [InlineData("text/css")]
    public async Task UnknownCharsetDeclarationsFailVisibly(string contentType)
    {
        var body = contentType == "text/css" ? "@charset \"bogus\";p{}"u8.ToArray() : "p{}"u8.ToArray();
        using var source = Serving("<!doctype html><link rel=stylesheet href=s.css>", _ => Bytes(body, contentType));
        var failure = await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        Assert.Contains("charset", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SheetCountCharacterByteAndWireLimitsAreEnforcedBeforePublication()
    {
        var links = string.Concat(Enumerable.Range(0, 33).Select(i => $"<link rel=stylesheet href={i}.css>"));
        var requests = 0;
        using (var source = Serving("<!doctype html>" + links, _ => { requests++; return Text("", "text/css"); }))
        {
            await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
            Assert.Equal(0, requests);
        }
        var duplicates = string.Concat(Enumerable.Repeat("<link rel=stylesheet href=same.css>", 40));
        using (var source = Serving("<!doctype html>" + duplicates, _ => Text("p{}", "text/css")))
        {
            Assert.Single((await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation)).Stylesheets);
        }
        using (var source = Serving("<!doctype html><link rel=stylesheet href=s.css>", _ => Text(new string('a', 256 * 1024 + 1), "text/css")))
        {
            await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        }
        using (var source = Serving("<!doctype html><link rel=stylesheet href=s.css>", _ => Bytes(new byte[1024 * 1024 + 1], "text/css")))
        {
            await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        }
        var five = string.Concat(Enumerable.Range(0, 5).Select(i => $"<link rel=stylesheet href={i}.css>"));
        using (var source = Serving("<!doctype html>" + five, _ => Text(new string('a', 256 * 1024), "text/css")))
        {
            await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        }
        using (var source = Serving($"<!doctype html><link rel=stylesheet href=/{new string('a', 8192)}>", _ => Text("", "text/css")))
        {
            await Assert.ThrowsAsync<PageNavigationException>(() => source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation));
        }
    }

    [Fact]
    public async Task ImportsAndCssUrlsAreNeverFetchedAndFailRenderingVisibly()
    {
        var requests = new List<string>();
        using var source = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath == "/s.css"
                ? Text("@import 'other.css';body{margin:0;background-image:url(i.png)}", "text/css")
                : Text("<!doctype html><link rel=stylesheet href=s.css>", "text/html");
        }));
        var page = await source.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Equal(["/", "/s.css"], requests);
        using var renderer = new StaticPageRenderer(FontPath, 100000);
        var failure = Assert.ThrowsAny<Exception>(() => renderer.Render(page, Viewport, Cancellation));
        Assert.True(StaticPageRenderer.IsRenderFailure(failure), failure.ToString());
        Assert.Equal(["/", "/s.css"], requests);
    }

    [Fact]
    public async Task LeadingCharsetRuleIsConsumedBeforeCssParsing()
    {
        using IPageRenderer renderer = new StaticPageRenderer(FontPath, 100000);
        var page = Page("<link rel=stylesheet href=s.css>", "@charset \"utf-8\";body{margin:0;background-color:blue}");
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal(Blue, rendered.Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public async Task StylesheetRequestsUseTheSharedSessionHstsStoreWithoutCookies()
    {
        var store = new HstsPolicyStore();
        var requests = new List<Uri>();
        HttpResponseMessage Respond(HttpRequestMessage request)
        {
            requests.Add(request.RequestUri!);
            Assert.False(request.Headers.Contains("Cookie"));
            var response = request.RequestUri!.AbsolutePath == "/s.css" ? Text("body{margin:0}", "text/css")
                : Text("<!doctype html><link rel=stylesheet href='http://example.test/s.css'>", "text/html");
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "secret=1; Secure");
            return response;
        }
        using var first = new GetPageSource(new Handler(Respond), store);
        var page = await first.LoadAsync(BrowserUrl.Parse("https://example.test/"), Cancellation);
        Assert.Equal("http://example.test/s.css", Assert.Single(page.Stylesheets).Url);
        Assert.Equal(["https://example.test/", "https://example.test/s.css"], requests.Select(uri => uri.AbsoluteUri));
        using var other = new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return request.RequestUri!.AbsolutePath == "/s.css" ? Text("p{}", "text/css")
                : Text("<!doctype html><link rel=stylesheet href='http://example.test/s.css'>", "text/html");
        }), store);
        await other.LoadAsync(BrowserUrl.Parse("https://unrelated.test/"), Cancellation);
        Assert.Equal("https://example.test/s.css", requests[^1].AbsoluteUri);
    }

    [Fact]
    public async Task ScriptIntroducedOrChangedLinksFailVisiblyInBothRendererModes()
    {
        foreach (var process in new[] { false, true })
        {
            using IPageRenderer renderer = process
                ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
                : new StaticPageRenderer(FontPath, 100000, executeInlineScripts: true);
            var added = Page("<style>body{margin:0}</style><script>let l=document.createElement('link');"
                + "l.setAttribute('rel','stylesheet');l.setAttribute('href','new.css');document.head.appendChild(l)</script>");
            var changed = Page("<style>body{margin:0}</style><link id=l rel=stylesheet href=s.css>"
                + "<script>document.getElementById('l').setAttribute('href','other.css')</script>", "body{}");
            foreach (var page in new[] { added, changed })
            {
                var failure = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(page, Viewport, Cancellation));
                Assert.Contains("not provided", failure.Message);
            }
        }
    }

    [Fact]
    public async Task RetainedRenderRejectsMismatchedStylesheetSnapshots()
    {
        using var renderer = new StaticPageRenderer(FontPath, 100000);
        var page = Page("<style>body{margin:0}</style><link rel=stylesheet href=s.css>", "body{background-color:blue}");
        await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        var forged = page with { Stylesheets = [new("https://example.test/s.css", "body{background-color:red}")] };
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderRetainedAsync(forged, Viewport, Cancellation));
        Assert.Equal(Blue, (await renderer.RenderRetainedAsync(page, Viewport, Cancellation)).Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void LoadedPageStylesheetCollectionIsBoundedData()
    {
        var url = BrowserUrl.Parse("https://example.test/");
        Assert.Empty(new LoadedPage(url, "", 200, []).Stylesheets);
        Assert.Throws<PageNavigationException>(() => new LoadedPage(url, "", 200, [])
        { Stylesheets = Enumerable.Range(0, 33).Select(i => new PageStylesheet($"https://example.test/{i}.css", "")).ToArray() });
        Assert.Throws<PageNavigationException>(() => new LoadedPage(url, "", 200, []) { Stylesheets = [new("relative.css", "")] });
        var source = new[] { new PageStylesheet("https://example.test/a.css", "a{}") };
        var page = new LoadedPage(url, "", 200, []) { Stylesheets = source };
        source[0] = new("https://example.test/b.css", "b{}");
        Assert.Equal("https://example.test/a.css", page.Stylesheets[0].Url);
    }

    [Fact]
    public void FailedStylesheetNavigationPreservesTheCommittedPage()
    {
        var failStylesheet = false;
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/s.css" => failStylesheet ? new HttpResponseMessage(HttpStatusCode.NotFound) : Text("body{background-color:blue}", "text/css"),
            "/first" => Text("<!doctype html><title>First</title><style>body{margin:0}</style><link rel=stylesheet href=s.css>", "text/html"),
            _ => Text("<!doctype html><title>Second</title><style>body{margin:0}</style><link rel=stylesheet href=s.css>", "text/html")
        })), () => new StaticPageRenderer(FontPath, 100000), isolateOrigins: true);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.test/first");
        controller.Pump(_ => Viewport);
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        Assert.Equal(Blue, committed!.Frame.Pixels.Span[..4].ToArray());
        var origin = tab.Origin;
        failStylesheet = true;
        controller.Navigate(tab.Id, "https://other.test/second");
        controller.Pump(_ => Viewport);
        Assert.False(tab.IsLoading);
        Assert.Contains("stylesheet", tab.Error);
        Assert.Equal("https://example.test/first", tab.History.Current!.Href);
        Assert.Same(origin, tab.Origin);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("First", tab.Title);
    }

    private static async Task<byte[]> Pixel(IPageRenderer renderer, string markup, params string[] sheets)
    {
        var rendered = await renderer.RenderAsync(Page(markup, sheets), Viewport, Cancellation);
        return rendered.Frame.Pixels.Span[..4].ToArray();
    }

    private static LoadedPage Page(string markup, params string[] sheets) =>
        new(BrowserUrl.Parse("https://example.test/page"), "<!doctype html>" + markup, 200, [])
        {
            Stylesheets = sheets.Select((css, index) => new PageStylesheet(
                "https://example.test/" + (char)('s' + index) + ".css", css)).ToArray()
        };

    private static GetPageSource Serving(string html, Func<HttpRequestMessage, HttpResponseMessage> stylesheet) =>
        new(new Handler(request => request.RequestUri!.AbsolutePath == "/" ? Text(html, "text/html") : stylesheet(request)));

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static HttpResponseMessage Text(string body, string contentType) => Bytes(Encoding.UTF8.GetBytes(body), contentType);

    private static HttpResponseMessage Bytes(byte[] body, string contentType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
