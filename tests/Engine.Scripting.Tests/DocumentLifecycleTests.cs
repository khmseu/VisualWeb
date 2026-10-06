using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class DocumentLifecycleTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }
    private static V8ScriptHost Host(DomDocument? document = null, TimeSpan? timeout = null)
        => new(timeout, document ?? Document(), enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);

    [Fact]
    public void ReadinessAndPrivateEventsFollowFiniteOrderWithCheckpointsBetweenStages()
    {
        using var host = Host();
        host.ExecuteInitialDocumentBatch([
            """
            let order=[document.readyState], saved;
            document.addEventListener('readystatechange',e=>{
                order.push('ready:'+document.readyState+':'+e.isTrusted+':'+e.bubbles+':'+e.cancelable);
                queueMicrotask(()=>order.push('micro:'+document.readyState));
            });
            document.addEventListener('DOMContentLoaded',e=>{
                saved=e;order.push('dom:'+document.readyState+':'+e.isTrusted+':'+e.bubbles+':'+e.cancelable);
                queueMicrotask(()=>{order.push('dom-micro');document.title='lifecycle'});
            });
            queueMicrotask(()=>order.push('script-micro:'+document.readyState));
            """,
            "order.push('next:'+document.readyState);"
        ], Cancellation);
        Assert.Equal("loading,script-micro:loading,next:loading,ready:interactive:true:false:false,micro:interactive,dom:interactive:true:true:false,dom-micro,ready:complete:true:false:false,micro:complete",
            host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("saved.target===document && saved.currentTarget===null && saved.eventPhase===0 && saved.composedPath().length===0", Cancellation).Boolean);
        Assert.Equal("lifecycle", host.Evaluate("document.title", Cancellation).Text);
        Assert.Equal("complete", host.Evaluate("document.readyState", Cancellation).Text);
        Assert.Throws<InvalidOperationException>(() => host.ExecuteInitialDocumentBatch([], Cancellation));
    }

    [Fact]
    public void CallerTamperingCannotSuppressPrivateDispatchOrForgeTrustedEvents()
    {
        using var host = Host();
        host.ExecuteInitialDocumentBatch([
            """
            let order=[], saved, realEvent=Event, realDispatch=EventTarget.prototype.dispatchEvent;
            document.addEventListener('DOMContentLoaded',e=>{saved=e;order.push('dom')});
            document.addEventListener('readystatechange',()=>order.push(document.readyState));
            document.dispatchEvent=()=>{throw Error('tampered')};
            EventTarget.prototype.dispatchEvent=()=>{throw Error('tampered')};
            Event.prototype.type='wrong';globalThis.Event=null;
            document.readyState='forged';
            """
        ], Cancellation);
        Assert.Equal("interactive,dom,complete", host.Evaluate("order.join(',')", Cancellation).Text);
        host.ExecuteClassic("""
            let t=new EventTarget(),trusted;
            t.addEventListener('DOMContentLoaded',e=>trusted=e.isTrusted);
            realDispatch.call(t,saved,true);
            """, Cancellation);
        Assert.False(host.Evaluate("trusted || saved.isTrusted", Cancellation).Boolean);
        Assert.True(host.Evaluate("(()=>{try{Object.defineProperty(document,'readyState',{value:'bad'});return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("readystatechange")]
    [InlineData("DOMContentLoaded")]
    public void LifecycleListenerErrorsInvalidateAndPreventCompletion(string type)
    {
        var document = Document();
        using var host = Host(document);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentBatch([
            $"document.addEventListener('{type}',()=>{{document.title=document.readyState;throw Error('fixture')}});"
        ], Cancellation));
        Assert.Equal("interactive", document.Title);
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void LifecycleAndSourceCallbacksShareInvocationAndMicrotaskBudgets()
    {
        using (var host = Host())
        {
            host.ExecuteInitialDocumentBatch([
                "let t=new EventTarget();t.addEventListener('x',()=>{});for(let i=0;i<4093;i++)t.dispatchEvent(new Event('x'));document.addEventListener('readystatechange',()=>{});document.addEventListener('DOMContentLoaded',()=>{});"
            ], Cancellation);
            Assert.Equal("complete", host.Evaluate("document.readyState", Cancellation).Text);
        }
        using (var host = Host())
        {
            Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentBatch([
                "let t=new EventTarget();t.addEventListener('x',()=>{});for(let i=0;i<4094;i++)t.dispatchEvent(new Event('x'));document.addEventListener('readystatechange',()=>{});document.addEventListener('DOMContentLoaded',()=>{});"
            ], Cancellation));
        }
        using var excessive = Host();
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteInitialDocumentBatch([
            "let n=0;function again(){if(++n<4096)queueMicrotask(again)};queueMicrotask(again);document.addEventListener('DOMContentLoaded',()=>queueMicrotask(()=>{}));"
        ], Cancellation));
    }

    [Fact]
    public void EmptyBatchesCompleteAndPrevalidationOrPreCancellationDoesNotConsumeLifecycle()
    {
        using var host = Host();
        Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentBatch([new string('x', V8ScriptHost.MaxSourceCharacters + 1)], Cancellation));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteInitialDocumentBatch([], canceled.Token));
        Assert.Equal("loading", host.Evaluate("document.readyState", Cancellation).Text);
        host.ExecuteInitialDocumentBatch([], Cancellation);
        Assert.Equal("complete", host.Evaluate("document.readyState", Cancellation).Text);
        using var plain = new V8ScriptHost(document: Document(), enableEvents: true);
        Assert.Equal("undefined", plain.Evaluate("typeof document.readyState", Cancellation).Text);
        Assert.Throws<InvalidOperationException>(() => plain.ExecuteInitialDocumentBatch([], Cancellation));
        Assert.Throws<ArgumentException>(() => new V8ScriptHost(enableDocumentLifecycle: true));
    }

    [Fact]
    public void LifecycleNativeAndMicrotaskCallbacksRemainUnderSharedDeadline()
    {
        foreach (var callback in new[] { "()=>{while(true){}}", "()=>queueMicrotask(()=>{while(true){}})" })
        {
            using var host = Host(timeout: TimeSpan.FromMilliseconds(150));
            Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentBatch([
                $"document.addEventListener('DOMContentLoaded',{callback});"
            ], Cancellation));
            Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        }
    }

    [Fact]
    public void SourceFailureNeverDispatchesLifecycleAndSyntheticNamesDoNotAdvanceReadiness()
    {
        var document = Document();
        using var host = Host(document);
        host.ExecuteClassic("let count=0;document.addEventListener('DOMContentLoaded',()=>{++count;document.title=document.readyState});document.dispatchEvent(new Event('DOMContentLoaded'));", Cancellation);
        Assert.Equal("loading", document.Title);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentBatch(["throw Error('fixture');"], Cancellation));
        Assert.Equal("loading", document.Title);
    }

    [Fact]
    public void PromiseReactionListenerFailuresAtReadinessCheckpointPreventNextStage()
    {
        var document = Document();
        using var host = Host(document);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentBatch([
            """
            let t=new EventTarget();t.addEventListener('x',()=>{document.title='failed';throw Error('fixture')});
            document.addEventListener('readystatechange',()=>{
                if(document.readyState==='interactive')Promise.resolve().then(()=>{try{t.dispatchEvent(new Event('x'))}catch{}});
            });
            document.addEventListener('DOMContentLoaded',()=>document.title='wrong stage');
            """
        ], Cancellation));
        Assert.Equal("failed", document.Title);
    }

    [Fact]
    public void InternalLifecycleDictionaryIgnoresPrototypePollution()
    {
        using var host = Host();
        host.ExecuteInitialDocumentBatch([
            """
            let flags=[];
            Object.prototype.cancelable=true;Object.prototype.composed=true;Object.prototype.bubbles=true;
            document.addEventListener('readystatechange',e=>flags.push(!e.bubbles&&!e.cancelable&&!e.composed&&e.isTrusted));
            document.addEventListener('DOMContentLoaded',e=>flags.push(e.bubbles&&!e.cancelable&&!e.composed&&e.isTrusted));
            """
        ], Cancellation);
        Assert.True(host.Evaluate("flags.length===3&&flags.every(value=>value)", Cancellation).Boolean);
    }

    [Fact]
    public void LifecycleDoesNotResetDeadlineAfterInitialSources()
    {
        using var host = Host(timeout: TimeSpan.FromMilliseconds(250));
        Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentBatch([
            """
            function burn(){let start=Date.now();while(Date.now()-start<175){}}
            document.addEventListener('DOMContentLoaded',burn);
            burn();
            """
        ], Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void LifecycleSharesDomCallbackBudgetAndCompleteFailuresInvalidate()
    {
        using (var host = Host())
        {
            Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentBatch([
                "for(let i=0;i<4096;i++)void document.title;"
            ], Cancellation));
            Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        }
        var document = Document();
        using var complete = Host(document);
        Assert.Throws<ScriptExecutionException>(() => complete.ExecuteInitialDocumentBatch([
            "document.addEventListener('readystatechange',()=>{if(document.readyState==='complete'){document.title='complete failure';throw Error('fixture')}});"
        ], Cancellation));
        Assert.Equal("complete failure", document.Title);
        Assert.Throws<InvalidOperationException>(() => complete.Evaluate("42", Cancellation));
    }

    [Fact]
    public void InFlightLifecycleCancellationInvalidatesAndFreshDocumentRecovers()
    {
        using var host = Host();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteInitialDocumentBatch([
            "document.addEventListener('DOMContentLoaded',()=>{while(true){}});"
        ], cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        using var next = Host();
        next.ExecuteInitialDocumentBatch([], Cancellation);
        Assert.Equal("complete", next.Evaluate("document.readyState", Cancellation).Text);
    }
}
