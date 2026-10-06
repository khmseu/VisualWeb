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
    public async Task DoctypeMetadataGuidesLifecyclePaintWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!--before--><!DOCTYPE HTML SYSTEM "about:legacy-compat">
            <title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let doctype=document.doctype;
            document.addEventListener('DOMContentLoaded',()=>{
                if(doctype!==document.firstChild.nextSibling||doctype.name!=='html'||doctype.nodeName!==doctype.name
                    ||doctype.publicId!==''||doctype.systemId!=='about:legacy-compat')
                    throw Error('doctype metadata');
                doctype.remove();
                if(document.doctype!==null||doctype.name!=='html'||doctype.ownerDocument!==document)
                    throw Error('detached doctype');
                document.insertBefore(doctype,document.documentElement);
                if(document.doctype!==doctype)throw Error('doctype identity');
                document.getElementById('sheet').textContent='body{margin:0;background-color:blue}';
                queueMicrotask(()=>document.title='Doctype '+doctype.name+' '+doctype.systemId);
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Doctype html about:legacy-compat", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>'use strict';document.doctype.publicId='spoof'</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(rendered.Title, (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeepCloneStylesApplyOnlyWhenInsertedAndSurviveRetainedResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Clone</title>
            <style id="source">body{margin:0;background-color:red}</style>
            <script>
            let source=document.getElementById('source');
            document.addEventListener('DOMContentLoaded',()=>{
                let clone=source.cloneNode(true);
                clone.removeAttribute('id');clone.firstChild.data='body{margin:0;background-color:blue}';
                if(clone.parentNode!==null||clone===source||clone.firstChild===source.firstChild
                    ||clone.firstChild.data.indexOf('blue')<0)throw Error('detached deep clone');
                document.head.appendChild(clone);
                if(clone.parentNode!==document.head||source.parentNode!==document.head)
                    throw Error('clone insertion');
                queueMicrotask(()=>document.title='Cloned '+clone.firstChild.length);
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Cloned 36", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.cloneNode(false)</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal(rendered.Title, (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelfRemovalFeedsLifecyclePaintAndTitleWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style>body{margin:0;background-color:blue}</style>
            <style id="override">body{background-color:red}</style>
            <script>
            let sheet=document.getElementById('override'),title=document.head.querySelector('title'),hits=0;
            sheet.addEventListener('probe',()=>hits++);
            document.addEventListener('DOMContentLoaded',()=>{
                sheet.remove();title.firstChild.remove();
                let comment=document.createComment('ignored'),pi=document.createProcessingInstruction('probe','data');
                title.appendChild(comment);title.appendChild(pi);comment.remove();pi.remove();
                if(sheet.parentNode!==null||sheet.ownerDocument!==document||sheet.firstChild.parentNode!==sheet
                    ||document.getElementById('override')!==null||document.title!=='')
                    throw Error('self removal');
                queueMicrotask(()=>{
                    sheet.dispatchEvent(new Event('probe'));document.title='Removed '+hits;
                });
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Removed 1", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.remove.call(document.createDocumentFragment())</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Removed 1", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CharacterFactoriesPreserveLifecycleTextBarriersAndTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            document.addEventListener('DOMContentLoaded',()=>{
                let sheet=document.getElementById('sheet'),text=sheet.firstChild,tail=text.splitText(text.data.indexOf('red'));
                tail.replaceData(0,3,'blue');
                let comment=document.createComment('ignored'),pi=document.createProcessingInstruction('Probe','ignored');
                sheet.insertBefore(comment,tail);sheet.insertBefore(pi,tail);sheet.normalize();
                if(comment.nextSibling!==pi||pi.nextSibling!==tail||pi.target!=='Probe'
                    ||comment.ownerDocument!==document||pi.ownerDocument!==document
                    ||sheet.textContent!=='body{margin:0;background-color:blue}')
                    throw Error('character factory barriers');
                queueMicrotask(()=>document.title='Created '+comment.nodeName+' '+pi.target);
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Created #comment Probe", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.createProcessingInstruction('bad name','data')</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Created #comment Probe", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttributeInspectionObservesLifecycleOrderAndPreservesTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><style>body{margin:0;background-color:red}body[data-blue]{background-color:blue}</style>
            <body =odd=value>
            <script>
            let body=document.body,snapshot=body.getAttributeNames();
            document.addEventListener('DOMContentLoaded',()=>{
                if(!body.hasAttributes()||snapshot.join(',')!=='=odd')throw Error('recovered attributes');
                body.removeAttribute('=odd');
                if(body.hasAttributes())throw Error('empty attributes');
                body.id='live';body.toggleAttribute('data-blue');body.className='token';
                body.removeAttribute('id');body.id='again';
                if(body.getAttributeNames().join(',')!=='data-blue,class,id'||!body.hasAttributes())
                    throw Error('attribute order');
                snapshot.push('local');
                queueMicrotask(()=>document.title='Attributes '+body.getAttributeNames().length);
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Attributes 3", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.getAttributeNames.call({})</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Attributes 3", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NodeMetadataGuidesLifecycleStyleAndTitleWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let sheet=document.getElementById('sheet');
            document.addEventListener('DOMContentLoaded',()=>{
                if(sheet.nodeName!=='STYLE'||sheet.tagName!=='STYLE'||sheet.localName!=='style'
                    ||sheet.namespaceURI!=='http://www.w3.org/1999/xhtml'||sheet.prefix!==null
                    ||sheet.ownerDocument!==document||sheet.firstChild.nodeName!=='#text'
                    ||sheet.firstChild.ownerDocument!==document||document.ownerDocument!==null)
                    throw Error('metadata');
                sheet.textContent='body{margin:0;background-color:blue}';
                queueMicrotask(()=>{sheet.ownerDocument.title='Metadata '+document.body.tagName;});
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Metadata BODY", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>'use strict';document.body.tagName='spoof'</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Metadata BODY", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StructuralEqualityObservesLifecycleChangesWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let sheet=document.getElementById('sheet'),expected=document.createElement('style');
            expected.id='sheet';expected.textContent=sheet.textContent;
            if(!sheet.isEqualNode(expected)||sheet.isSameNode(expected))throw Error('structural identity');
            document.addEventListener('DOMContentLoaded',()=>{
                sheet.firstChild.replaceData(sheet.textContent.indexOf('red'),3,'blue');
                if(sheet.isEqualNode(expected))throw Error('stale equality');
                expected.textContent='body{margin:0;background-color:blue}';
                if(!sheet.isEqualNode(expected))throw Error('live equality');
                queueMicrotask(()=>{
                    document.title='Equal nodes';let title=document.querySelector('title');
                    if(!title.firstChild.isEqualNode(document.createTextNode('Equal nodes')))throw Error('title equality');
                });
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Equal nodes", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.isEqualNode({})</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Equal nodes", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextNormalizationFeedsLifecycleStyleAndTitleWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let sheet=document.getElementById('sheet'),original=sheet.firstChild;
            document.addEventListener('DOMContentLoaded',()=>{
                let tail=original.splitText(original.data.indexOf('red'));tail.replaceData(0,3,'blue');
                sheet.insertBefore(document.createTextNode(''),original);
                sheet.normalize();
                if(sheet.firstChild!==original||sheet.lastChild!==original||tail.parentNode!==null||tail.data!=='blue}')
                    throw Error('normalization identity');
                queueMicrotask(()=>{
                    let title=document.querySelector('title'),text=title.firstChild;text.data='Normalized title';
                    let end=text.splitText(11);title.normalize();
                    if(title.firstChild!==text||text.nextSibling!==null||end.parentNode!==null)
                        throw Error('title identity');
                });
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Normalized title", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.body.normalize.call({})</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Normalized title", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextSplittingPreservesLifecycleStyleAndTitleRunsWithTransactionalResize(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let original=document.getElementById('sheet').firstChild;
            document.addEventListener('DOMContentLoaded',()=>{
                let tail=original.splitText(original.data.indexOf('red'));
                tail.replaceData(0,3,'blue');
                if(tail.previousSibling!==original||original.wholeText!==document.getElementById('sheet').textContent)
                    throw Error('text run identity');
                queueMicrotask(()=>{
                    let title=document.querySelector('title').firstChild;title.data='Split title';
                    let end=title.splitText(6);end.appendData(' data');
                    if(end.wholeText!=='Split title data')throw Error('title run');
                });
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Split title data", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.createTextNode('x').splitText(2)</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Split title data", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CharacterDataEditsPreserveNodeIdentityAndFeedLifecyclePaintTransactionally(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            let text=document.getElementById('sheet').firstChild,title=document.querySelector('title').firstChild;
            document.addEventListener('DOMContentLoaded',()=>{
                let offset=text.data.indexOf('red');text.replaceData(offset,3,'blue');
                if(document.getElementById('sheet').firstChild!==text||text.substringData(offset,4)!=='blue')throw Error('data identity');
                queueMicrotask(()=>{title.nodeValue='Character';title.appendData(' data');});
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("Character data", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.createTextNode('x').insertData(2,'bad')</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
        Assert.Equal("Character data", (await renderer.RenderAsync(Page(page.Html), Viewport, Cancellation)).Title);
    }

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
    public async Task ScopeQueriesMutateTitleAndRootScopedStyleBeforeTransactionalRetainedPaint(bool process)
    {
        using IPageRenderer renderer = process
            ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: true)
            : new StaticPageRenderer(FontPath, 1000, executeInlineScripts: true);
        var page = Page("""
            <!doctype html><title>Original</title><style id="sheet">body{margin:0;background-color:red}</style>
            <script>
            document.addEventListener('DOMContentLoaded',()=>{
                let head=document.head,sheet=head.querySelector(':scope > style');
                if(sheet.textContent.includes('blue'))throw Error('rerun');
                if(sheet!==document.getElementById('sheet')||!sheet.matches('head > :scope')||sheet.closest(':scope')!==sheet
                    ||document.querySelector(':scope')!==document.documentElement||head.querySelector(':scope')!==null
                    ||document.body.closest('html > :scope')!==document.body)throw Error('scope');
                sheet.textContent=':scope > body{margin:0;background-color:blue}';
                head.querySelector(':scope > title').textContent='scoped '+document.querySelectorAll(':scope > *').length;
            });
            </script>
            """);
        var rendered = await renderer.RenderAsync(page, Viewport, Cancellation);
        Assert.Equal("scoped 2", rendered.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
        renderer.CommitDocument(page.DocumentId);
        var bad = Page("<!doctype html><script>document.title='partial';document.querySelector(':scope >')</script>");
        if (process) { await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        else { await Assert.ThrowsAsync<ScriptExecutionException>(() => renderer.RenderAsync(bad, Viewport, Cancellation)); }
        var retained = await renderer.RenderRetainedAsync(page, new(25, 20, 1), Cancellation);
        Assert.Equal(rendered.Title, retained.Title);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, retained.Frame.Pixels.Span[..4].ToArray());
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
