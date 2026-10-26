using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class ClickDispatchTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static (DomDocument Document, DomElement Anchor) Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        var body = document.CreateElement("body"); html.AppendChild(body);
        var paragraph = document.CreateElement("p"); paragraph.SetAttribute("id", "p"); body.AppendChild(paragraph);
        var anchor = document.CreateElement("a"); anchor.SetAttribute("id", "a"); anchor.SetAttribute("href", "#x");
        paragraph.AppendChild(anchor);
        return (document, anchor);
    }
    private static V8ScriptHost Host(DomDocument document, TimeSpan? timeout = null)
        => new(timeout, document, enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);

    [Fact]
    public void InitialListenersPersistAndReceiveTrustedBubblingCancelableClickInOrder()
    {
        var (document, anchor) = Document();
        using var host = Host(document);
        host.ExecuteInitialDocumentBatch(["""
            let order=[], saved;
            const a=document.getElementById('a'), p=document.getElementById('p');
            document.addEventListener('click',e=>order.push('doc-capture:'+e.eventPhase),true);
            a.addEventListener('click',e=>{saved=e;order.push('a:'+e.eventPhase+':'+e.isTrusted+':'+e.bubbles+':'+e.cancelable+':'+e.composed+':'+(e.target===a));
                queueMicrotask(()=>order.push('micro'));});
            p.addEventListener('click',e=>order.push('p:'+e.eventPhase+':'+(e.currentTarget===p)));
            document.body.addEventListener('click',()=>order.push('body'));
            document.addEventListener('click',e=>order.push('doc:'+e.eventPhase+':'+e.defaultPrevented));
            """], Cancellation);
        Assert.True(host.DispatchTrustedClick(anchor, Cancellation));
        Assert.Equal("doc-capture:1,a:2:true:true:true:true:true,p:3:true,body,doc:3:false,micro",
            host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("saved.type==='click' && saved.target===document.getElementById('a') && saved.currentTarget===null && saved.eventPhase===0", Cancellation).Boolean);
        Assert.True(host.DispatchTrustedClick(anchor, Cancellation));
        Assert.Equal(2, host.Evaluate("order.filter(x=>x==='body').length", Cancellation).Number);
    }

    [Fact]
    public void PreventDefaultCancelsAndTamperingCannotForgeOrSuppressTrustedClick()
    {
        var (document, anchor) = Document();
        using var host = Host(document);
        host.ExecuteInitialDocumentBatch(["""
            let seen=0, trusted=false;
            document.addEventListener('click',e=>{++seen;trusted=e.isTrusted;e.preventDefault();});
            EventTarget.prototype.dispatchEvent=()=>{throw Error('tampered')};
            globalThis.Event=null;
            """], Cancellation);
        Assert.False(host.DispatchTrustedClick(anchor, Cancellation));
        Assert.True(host.Evaluate("seen===1 && trusted", Cancellation).Boolean);
    }

    [Fact]
    public void ClickRequiresCompletedLifecycleAndConnectedOwnedTarget()
    {
        var (document, anchor) = Document();
        using var host = Host(document);
        Assert.Throws<InvalidOperationException>(() => host.DispatchTrustedClick(anchor, Cancellation));
        host.ExecuteInitialDocumentBatch([], Cancellation);
        Assert.Throws<ArgumentException>(() => host.DispatchTrustedClick(Document().Anchor, Cancellation));
        Assert.Throws<ArgumentException>(() => host.DispatchTrustedClick(document.CreateElement("a"), Cancellation));
        using var plain = new V8ScriptHost(document: Document().Document, enableEvents: true, enableMicrotasks: true);
        Assert.Throws<InvalidOperationException>(() => plain.DispatchTrustedClick(anchor, Cancellation));
        Assert.True(host.DispatchTrustedClick(anchor, Cancellation));
    }

    [Fact]
    public void ListenerFailureAndBudgetsInvalidateTheHost()
    {
        var (document, anchor) = Document();
        using var failing = Host(document);
        failing.ExecuteInitialDocumentBatch(["document.addEventListener('click',()=>{throw Error('boom')});"], Cancellation);
        Assert.Throws<ScriptExecutionException>(() => failing.DispatchTrustedClick(anchor, Cancellation));
        Assert.Throws<InvalidOperationException>(() => failing.Evaluate("1", Cancellation));

        var (looping, loopAnchor) = Document();
        using var slow = Host(looping, TimeSpan.FromMilliseconds(200));
        slow.ExecuteInitialDocumentBatch(["document.addEventListener('click',()=>{for(;;){}});"], Cancellation);
        Assert.Throws<ScriptLimitException>(() => slow.DispatchTrustedClick(loopAnchor, Cancellation));
        Assert.Throws<InvalidOperationException>(() => slow.DispatchTrustedClick(loopAnchor, Cancellation));

        var (nested, nestedAnchor) = Document();
        using var limited = Host(nested);
        limited.ExecuteInitialDocumentBatch(["""
            const a=document.getElementById('a');
            a.addEventListener('click',()=>a.dispatchEvent(new Event('click')));
            """], Cancellation);
        Assert.Throws<ScriptLimitException>(() => limited.DispatchTrustedClick(nestedAnchor, Cancellation));
        Assert.Throws<InvalidOperationException>(() => limited.Evaluate("1", Cancellation));
    }

    [Fact]
    public void TrustedFocusAndBlurUseTheirNonBubblingComposedPlainEventFlags()
    {
        var (document, anchor) = Document();
        using var host = Host(document);
        host.ExecuteInitialDocumentBatch(["""
            let log=[];
            const a=document.getElementById('a'), p=document.getElementById('p');
            document.addEventListener('focus',e=>log.push('doc-capture:'+e.eventPhase),true);
            document.addEventListener('focus',()=>log.push('doc-bubble'));
            p.addEventListener('focus',()=>log.push('parent-bubble'));
            a.addEventListener('blur',e=>log.push('blur:'+e.isTrusted+':'+e.bubbles+':'+e.cancelable+':'+e.composed+':'+(e.target===a)+':'+(typeof FocusEvent==='undefined')));
            a.addEventListener('focus',e=>{
                log.push('focus:'+e.isTrusted+':'+e.bubbles+':'+e.cancelable+':'+e.composed+':'+(e.target===a)+':'+(typeof FocusEvent==='undefined'));
                e.preventDefault();log.push('defaultPrevented:'+e.defaultPrevented);
            });
            """], Cancellation);

        host.DispatchTrustedFocusEvent(anchor, "blur", Cancellation);
        host.DispatchTrustedFocusEvent(anchor, "focus", Cancellation);

        Assert.Equal("blur:true:false:false:true:true:true,doc-capture:1,focus:true:false:false:true:true:true,defaultPrevented:false",
            host.Evaluate("log.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("log.every(x=>!x.includes('bubble'))", Cancellation).Boolean);
    }

    [Fact]
    public void TrustedFocusDoesNotInvokeClickListeners()
    {
        var (document, anchor) = Document();
        using var host = Host(document);
        host.ExecuteInitialDocumentBatch(["""
            let log=[];
            document.addEventListener('click',()=>{log.push('click');throw Error('click listener ran')});
            document.addEventListener('focus',()=>log.push('document'),true);
            document.getElementById('a').addEventListener('focus',()=>log.push('target'));
            """], Cancellation);

        host.DispatchTrustedFocusEvent(anchor, "focus", Cancellation);

        Assert.Equal("document,target", host.Evaluate("log.join(',')", Cancellation).Text);
    }
}
