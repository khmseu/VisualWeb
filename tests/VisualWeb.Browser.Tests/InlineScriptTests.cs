using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class InlineScriptTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static readonly PageViewport Viewport = new(20, 20, 1);
    private const string Html = """
        <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
        <script>let color = 'blue'; let target = document.getElementById('sheet'); document.title = '  Script\t title '; </script>
        <script>target.textContent = 'body{margin:0;background-color:'+color+'}';</script>
        """;
    private static LoadedPage Page(string html = Html) => new(BrowserUrl.Parse("data:text/html,script"), html, 200, []);

    [Fact]
    public void OptInMutationsReachTitleAndExactPixelsWhileDefaultRemainsStatic()
    {
        using var disabled = new StaticPageRenderer(FontPath, 1000);
        var original = disabled.Render(Page(), Viewport, Cancellation);
        Assert.Equal("Original", original.Title);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, original.Frame.Pixels.Span[..4].ToArray());
        using var enabled = new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var changed = enabled.Render(Page(), Viewport, Cancellation);
        Assert.Equal("Script title", changed.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, changed.Frame.Pixels.Span[..4].ToArray());
        Assert.Contains("Post-parse inline scripts: 2", changed.Status);
    }

    [Fact]
    public void CompleteDocumentSourcesAreSnapshottedBeforeEarlierScriptsMutateThem()
    {
        using var renderer = new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = renderer.Render(Page("""
            <!doctype html><style>body{margin:0}</style>
            <script>document.getElementById('later').textContent = "throw Error('must not execute mutated source')";</script>
            <script id="later">document.title = 'original snapshot';</script>
            """), Viewport, Cancellation);
        Assert.Equal("original snapshot", page.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" type='' language='not-js'")]
    [InlineData(" type=' \tTEXT/JAVASCRIPT\r\n '")]
    [InlineData(" type='application/ecmascript'")]
    [InlineData(" language='JavaScript1.5'")]
    public void ClassicTypeAndLegacyLanguageSelectionUsesHtmlRules(string attributes)
    {
        var parsed = HtmlParser.Parse("<!doctype html><script" + attributes + ">document.title='classic';</script>",
            cancellationToken: Cancellation);
        Assert.Equal("document.title='classic';", Assert.Single(InlinePageScripts.Collect(parsed.Document, Cancellation)));
    }

    [Theory]
    [InlineData(" type='application/json'")]
    [InlineData(" type='text/javascript;charset=utf-8'")]
    [InlineData(" type=' '")]
    [InlineData(" type='\u00a0text/javascript\u00a0'")]
    [InlineData(" type='text/java\u017fcript'")]
    [InlineData(" language='javascript '")]
    [InlineData(" nomodule src='https://example.com/ignored.js'")]
    public void DataBlocksAndNoModuleClassicsStayInert(string attributes)
    {
        var parsed = HtmlParser.Parse("<!doctype html><script" + attributes + ">throw 'must not run';</script>",
            cancellationToken: Cancellation);
        Assert.Empty(InlinePageScripts.Collect(parsed.Document, Cancellation));
    }

    [Theory]
    [InlineData(" src=''")]
    [InlineData(" src='https://example.com/script.js'")]
    [InlineData(" type='module'")]
    [InlineData(" type='IMPORTMAP'")]
    [InlineData(" type='speculationrules'")]
    [InlineData(" async")]
    [InlineData(" defer")]
    [InlineData(" for='window' event='onload'")]
    public void DeferredExecutableFeaturesFailDuringWholeDocumentPreflight(string attributes)
    {
        var parsed = HtmlParser.Parse("<!doctype html><title>before</title><script>document.title='mutated';</script><script"
            + attributes + ">throw 'fixture';</script>", cancellationToken: Cancellation);
        Assert.Throws<PageNavigationException>(() => InlinePageScripts.Collect(parsed.Document, Cancellation));
        Assert.Equal("before", parsed.Document.Title);
    }

    [Fact]
    public void ExactSourceCountAndCharacterBudgetsRejectBeforeExecuting()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        var sources = new List<DomElement>();
        for (var i = 0; i < V8ScriptHost.MaxBatchScripts; i++)
        {
            var script = document.CreateElement("script"); script.TextContent = "0;";
            html.AppendChild(script); sources.Add(script);
        }
        Assert.Equal(64, InlinePageScripts.Collect(document, Cancellation).Count);
        var excess = document.CreateElement("script"); excess.TextContent = "0;"; html.AppendChild(excess);
        Assert.Throws<ScriptLimitException>(() => InlinePageScripts.Collect(document, Cancellation));
        html.RemoveChild(excess);
        foreach (var source in sources.Skip(4)) { html.RemoveChild(source); }
        foreach (var source in sources.Take(4)) { source.TextContent = new string(' ', V8ScriptHost.MaxSourceCharacters); }
        Assert.Equal(262144, InlinePageScripts.Collect(document, Cancellation).Sum(s => s.Length));
        sources[0].TextContent += " ";
        Assert.Throws<ScriptLimitException>(() => InlinePageScripts.Collect(document, Cancellation));
        sources[0].TextContent = new string(' ', V8ScriptHost.MaxSourceCharacters);
        html.AppendChild(excess);
        Assert.Throws<ScriptLimitException>(() => InlinePageScripts.Collect(document, Cancellation));
    }

    [Fact]
    public void ResizeRetainsScriptedDomAndFailedCandidatesCannotEvictCommittedDocument()
    {
        using var renderer = new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page();
        var first = renderer.Render(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        // A later successful but unpublished candidate must not replace the committed document.
        renderer.Render(Page("<!doctype html><style>body{margin:0}</style>"), Viewport, Cancellation);
        Assert.Throws<ScriptExecutionException>(() => renderer.Render(
            Page("<!doctype html><script>document.title='bad'; throw Error('fixture');</script>"), Viewport, Cancellation));
        var resized = renderer.Render(page, new(25, 20, 1), Cancellation, reuseDocument: true);
        Assert.Equal(first.Title, resized.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, resized.Frame.Pixels.Span[..4].ToArray());
        Assert.Throws<PageNavigationException>(() => renderer.Render(page with { Html = "<!doctype html>" },
            Viewport, Cancellation, reuseDocument: true));
        Assert.Throws<PageNavigationException>(() => renderer.Render(Page(), Viewport, Cancellation, reuseDocument: true));
        // Fresh navigations use separate global lexical environments even with identical HTML.
        Assert.Equal(first.Title, renderer.Render(Page(), Viewport, Cancellation).Title);
    }

    [Fact]
    public void ScriptLimitsErrorsAndCancellationLeaveRendererAbleToRenderFreshPages()
    {
        using var renderer = new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        Assert.Throws<ScriptExecutionException>(() => renderer.Render(Page("<!doctype html><script>throw Error('fixture');</script>"), Viewport, Cancellation));
        Assert.Throws<ScriptLimitException>(() => renderer.Render(Page("<!doctype html><script>while(true){}</script>"), Viewport, Cancellation));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => renderer.Render(Page(), Viewport, canceled.Token));
        Assert.Equal("Script title", renderer.Render(Page(), Viewport, Cancellation).Title);
    }

    [Fact]
    public async Task WorkerOptInRetainsCommittedDomAcrossFailedPagesAndUsesFreshNavigationContexts()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true);
        var page = Page();
        var first = await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        var pid = renderer.ProcessId;
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Page("<!doctype html><script>throw Error('fixture');</script>"), Viewport, Cancellation));
        var resized = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(first.Title, resized.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, resized.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(pid, renderer.ProcessId);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResizeKeepsNondeterministicScriptOutputInsteadOfExecutingAgain(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("<!doctype html><style>body{margin:0}</style><script>document.title=String(Math.random());</script>");
        var first = await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal(first.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
    }

    [Fact]
    public async Task WorkerWithoutOptInLeavesScriptsInert()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath);
        var original = await renderer.RenderAsync(Page(), Viewport, Cancellation);
        Assert.Equal("Original", original.Title);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, original.Frame.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public async Task RequiredConfinementInlineScriptsMutatePixelsAndRecoverAfterDeadline()
    {
        if (!OperatingSystem.IsWindows() && (!OperatingSystem.IsLinux()
            || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64))
        { return; }
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, requireSandbox: true, executeInlineScripts: true);
        var page = Page();
        var first = await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal("Script title", first.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, first.Frame.Pixels.Span[..4].ToArray());
        var pid = renderer.ProcessId;
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Page("<!doctype html><script>while(true){}</script>"), Viewport, Cancellation));
        Assert.Equal(first.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
        Assert.Equal(pid, renderer.ProcessId);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Fact]
    public async Task LostWorkerDocumentRequiresReloadInsteadOfRerunningScriptsOnResize()
    {
        using var renderer = new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true);
        var page = Page();
        await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        using (var worker = System.Diagnostics.Process.GetProcessById(Assert.IsType<int>(renderer.ProcessId)))
        {
            worker.Kill();
            await worker.WaitForExitAsync(Cancellation);
        }
        Assert.NotNull(renderer.TakeFailure());
        var error = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation));
        Assert.Contains("reload", error.Message);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Fact]
    public void LaunchOptInIsExplicitAndControllerPreservesHistoryOnScriptFailure()
    {
        Assert.False(BrowserLaunchOptions.Parse(["--development-single-process", "--font", FontPath]).ExecuteInlineScripts);
        Assert.True(BrowserLaunchOptions.Parse(["--development-single-process", "--font", FontPath, "--enable-inline-scripts"]).ExecuteInlineScripts);
        Assert.True(BrowserLaunchOptions.Parse(["--development-multiprocess", "--font", FontPath,
            "--renderer", RendererPath, "--enable-inline-scripts"]).ExecuteInlineScripts);
        using var controller = new BrowserController(() => new GetPageSource(),
            () => new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        Navigate(Html);
        var first = controller.Page(tab.Id);
        Assert.Equal("Script title", tab.Title);
        Navigate("<!doctype html><script>throw Error('fixture');</script>");
        Assert.NotNull(tab.Error); Assert.Same(first, controller.Page(tab.Id)); Assert.Single(tab.History.Entries);
        controller.Resize(tab.Id, new(25, 20, 1));
        Assert.Equal(first!.Title, controller.Page(tab.Id)!.Title);
        controller.Reload(tab.Id);
        controller.Pump(_ => Viewport);
        Assert.Null(tab.Error); Assert.Single(tab.History.Entries);
        void Navigate(string html)
        {
            controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
            controller.Pump(_ => Viewport);
        }
    }
}
