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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReflectedIdAndToggledAttributeFeedLifecycleSelectorsAndTransactionalPaint(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}#live[data-blue]{background-color:blue}</style>
            <script>
            document.addEventListener('DOMContentLoaded',()=>{
                let body=document.body;body.id='live';
                if(!body.toggleAttribute('DATA-BLUE')||document.getElementById('live')!==body
                    ||document.querySelector('#live[data-blue]')!==body)throw Error('attribute reflection');
                queueMicrotask(()=>document.title='reflected attributes');
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("reflected attributes", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.toggleAttribute('bad name',false)</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("reflected attributes", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreeInspectionFindsLiveElementsDuringLifecycleAndPreservesFailedNavigation(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}body.blue{background-color:blue}</style>
            <script>
            document.addEventListener('DOMContentLoaded',()=>{
                let html=document.firstElementChild,body=html.lastElementChild;
                body.appendChild(document.createTextNode(''));
                if(!body.isSameNode(document.body)||!html.contains(body)||body.getRootNode({composed:true})!==document
                    ||body.parentElement!==html||body.previousElementSibling!==document.head
                    ||html.childElementCount!==2||!body.hasChildNodes())throw Error('tree inspection');
                queueMicrotask(()=>{body.classList.add('blue');document.title='inspected tree'});
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("inspected tree", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.getRootNode(true)</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("inspected tree", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveClassTokensFeedSelectorsPaintAndRetainedResizeTransactionally(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}body.blue{background-color:blue}</style>
            <script>
            let list=document.body.classList;
            document.addEventListener('DOMContentLoaded',()=>{
                document.body.className='red red';
                list.replace('red','blue');
                queueMicrotask(()=>{
                    if(document.querySelector('.blue')!==document.body||!document.body.matches('body.blue')
                        ||list[0]!=='blue'||list!==document.body.classList)throw Error('class identity');
                    document.title='class tokens';
                });
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("class tokens", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var resized = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, resized.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, resized.Frame.Pixels.Span[..4].ToArray());
        var bad = Page("""
            <!doctype html><script>
            document.body.classList.add('candidate','bad value');
            </script>
            """);
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, Viewport, Cancellation)).Title);
        Assert.Equal("class tokens", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LifecycleQueriesMutateStyleAndSnapshotNodesBeforePainting(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let snapshot=document.querySelectorAll('style');
            document.addEventListener('DOMContentLoaded',()=>{
                if(snapshot.item(0)!==document.querySelector('#sheet'))throw Error('identity');
                snapshot.forEach(node=>node.textContent='body{margin:0;background-color:blue}');
                if(!snapshot[0].matches('head > style')||snapshot[0].closest('head')!==document.head)throw Error('match');
                document.title='queried lifecycle';
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("queried lifecycle", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
        var bad = Page("<!doctype html><script>document.querySelector('[')</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiniteReadinessListenersAndCheckpointsFeedFinalPaintAndRetainedResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}</style>
            <script>
            let order=[document.readyState];
            document.addEventListener('readystatechange',()=>{
                order.push(document.readyState);
                queueMicrotask(()=>{order.push('micro:'+document.readyState);document.title=order.join(',');});
            });
            document.addEventListener('DOMContentLoaded',e=>{
                if(!e.isTrusted||!e.bubbles||e.cancelable)throw Error('lifecycle event');
                order.push('dom');queueMicrotask(()=>document.body.setAttribute('style','background-color:blue'));
            },{once:true});
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("loading,interactive,micro:interactive,dom,complete,micro:complete", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadinessFailureCannotPublishAndFreshPageCanRecover(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var good = Page();
        await renderer.RenderAsync(good, Viewport, Cancellation); renderer.CommitDocument(good.DocumentId);
        var failure = Page("""
            <!doctype html><script>
            document.addEventListener('DOMContentLoaded',()=>{document.title='bad';throw Error('fixture')});
            </script>
            """);
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        Assert.Equal("Script title", (await renderer.RenderRetainedAsync(good, new(25, 20, 1), Cancellation)).Title);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteLifecycleFailurePreservesPublishedPageAndEmptySourcePagesRemainValid(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var good = Page("<!doctype html><title>Empty scripts</title><style>body{margin:0;background-color:blue}</style>");
        var rendered = await renderer.RenderAsync(good, Viewport, Cancellation);
        Assert.Equal("Empty scripts", rendered.Title);
        renderer.CommitDocument(good.DocumentId);
        var failure = Page("""
            <!doctype html><script>
            document.addEventListener('readystatechange',()=>{
                if(document.readyState==='complete'){document.title='bad';throw Error('complete fixture')}
            });
            </script>
            """);
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(good, new(25, 20, 1), Cancellation);
        Assert.Equal("Empty scripts", retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticDomEventsAndListenerMicrotasksFeedPixelsAndRetainedResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}</style>
            <script>
            let order=[];
            document.addEventListener('paint',()=>order.push('capture'),true);
            document.body.addEventListener('paint',e=>{
                order.push('target');e.preventDefault();
                queueMicrotask(()=>{order.push('micro');document.body.setAttribute('style','background-color:blue');});
            },{once:true});
            document.addEventListener('paint',()=>order.push('bubble'));
            if(document.body.dispatchEvent(new Event('paint',{bubbles:true,cancelable:true})))throw Error('cancel');
            order.push('sync');
            </script>
            <script>order.push('next');document.title=order.join(',');</script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("capture,target,bubble,sync,micro,next", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaughtListenerFailureCannotPublishCandidateOrEvictCommittedPage(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var good = Page();
        await renderer.RenderAsync(good, Viewport, Cancellation); renderer.CommitDocument(good.DocumentId);
        var failure = Page("""
            <!doctype html><script>
            document.addEventListener('x',()=>{document.title='bad';throw Error('fixture')});
            try{document.dispatchEvent(new Event('x'))}catch{}
            </script>
            """);
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        Assert.Equal("Script title", (await renderer.RenderRetainedAsync(good, new(25, 20, 1), Cancellation)).Title);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeMicrotasksRunBetweenScriptsAndFeedRetainedPagePixels(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}</style>
            <script>
            let order=[];
            queueMicrotask(()=>{order.push('queue');document.body.setAttribute('style','background-color:blue');
                queueMicrotask(()=>order.push('nested'));});
            Promise.resolve().then(()=>order.push('promise'));
            order.push('sync');
            </script>
            <script>order.push('next');document.title=order.join(',');</script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("sync,queue,promise,nested,next", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal(rendered.Title, (await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MicrotaskFailurePreservesCommittedDomAndWorkerCanRenderNewTask(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var good = Page();
        await renderer.RenderAsync(good, Viewport, Cancellation); renderer.CommitDocument(good.DocumentId);
        var failure = Page("<!doctype html><script>queueMicrotask(()=>{throw Error('fixture')});</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(failure, Viewport, Cancellation)); }
        Assert.Equal("Script title", (await renderer.RenderRetainedAsync(good, new(25, 20, 1), Cancellation)).Title);
        Assert.Equal("Script title", (await renderer.RenderAsync(Page(), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttributeAndTreeMutationsRecomputeStylesAndNewLayoutWithoutRunningDynamicScripts(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:white}</style>
            <script>
            document.body.setAttribute('style','background-color:blue');
            let box = document.createElement('div');
            box.setAttribute('style','height:4px;background-color:red');
            let fragment = document.createDocumentFragment();
            fragment.appendChild(box); document.body.appendChild(fragment);
            let dynamic = document.createElement('script');
            dynamic.textContent = "throw Error('dynamic scripts must remain inert')";
            document.head.appendChild(dynamic);
            let css = document.createElement('style');
            css.appendChild(document.createTextNode('div{width:10px}'));
            document.head.appendChild(css);
            document.title = 'new tree';
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        renderer.CommitDocument(page.DocumentId);
        Assert.Equal("new tree", rendered.Title);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span.Slice(15 * 4, 4).ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span.Slice(rendered.Frame.Stride * 10, 4).ToArray());
        var resize = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal("new tree", resize.Title);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, resize.Frame.Pixels.Span[..4].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovedInitialScriptsStillExecuteTheirPreflightSnapshot(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var rendered = await renderer.RenderAsync(Page("""
            <!doctype html><style>body{margin:0}</style>
            <script>let later = document.getElementById('later'); later.parentNode.removeChild(later);</script>
            <script id="later">document.title = 'detached snapshot';</script>
            """), Viewport, Cancellation);
        Assert.Equal("detached snapshot", rendered.Title);
    }

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
