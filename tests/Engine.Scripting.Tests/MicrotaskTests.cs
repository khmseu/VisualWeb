using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class MicrotaskTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void QueueMicrotaskIsOptionalAndNativePromiseOrderingDrainsBeforeNextScript()
    {
        using var plain = new V8ScriptHost();
        Assert.Equal("undefined", plain.Evaluate("typeof queueMicrotask", Cancellation).Text);
        using var host = new V8ScriptHost(enableMicrotasks: true);
        host.ExecuteClassicBatch([
            "let order=[]; queueMicrotask(()=>{order.push('q1'); queueMicrotask(()=>order.push('nested'));}); Promise.resolve().then(()=>order.push('promise')); queueMicrotask(()=>order.push('q2')); order.push('sync');",
            "order.push('next');"
        ], Cancellation);
        Assert.Equal("sync,q1,promise,q2,nested,next", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("queueMicrotask(()=>order.push('eval'))", Cancellation).Kind);
        Assert.Equal("eval", host.Evaluate("order[order.length-1]", Cancellation).Text);
    }

    [Fact]
    public void CallbackArgumentsThisAndThenablesHaveNativeCallableSemantics()
    {
        using var host = new V8ScriptHost(enableMicrotasks: true);
        host.ExecuteClassic("""
            let count=-1, receiver=false, ignored=false;
            queueMicrotask(function(){'use strict'; count=arguments.length; receiver=this===undefined;
                return {then(){ignored=true}}; });
            """, Cancellation);
        Assert.Equal(0, host.Evaluate("count", Cancellation).Number);
        Assert.True(host.Evaluate("receiver && !ignored", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("queueMicrotask()")]
    [InlineData("queueMicrotask(null)")]
    [InlineData("queueMicrotask(42)")]
    [InlineData("queueMicrotask({call(){}})")]
    public void InvalidCallbacksThrowInScriptAndCannotBeMistakenForCallableObjects(string source)
    {
        using var host = new V8ScriptHost(enableMicrotasks: true);
        Assert.True(host.Evaluate("(()=>{try{" + source + ";return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Fact]
    public void ExactPendingLimitAndPerExecutionRecursiveLimitAreEnforcedAndLatched()
    {
        using (var host = new V8ScriptHost(enableMicrotasks: true))
        {
            host.ExecuteClassic("let n=0; for(let i=0;i<1024;i++) queueMicrotask(()=>n++);", Cancellation);
            Assert.Equal(1024, host.Evaluate("n", Cancellation).Number);
            host.ExecuteClassic("for(let i=0;i<1024;i++) queueMicrotask(()=>n++);", Cancellation);
            Assert.Equal(2048, host.Evaluate("n", Cancellation).Number);
        }
        using (var host = new V8ScriptHost(enableMicrotasks: true))
        {
            Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic(
                "try{for(let i=0;i<1025;i++) queueMicrotask(()=>{});}catch{}", Cancellation));
            Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        }
        using (var host = new V8ScriptHost(enableMicrotasks: true))
        {
            host.ExecuteClassic("let n=0; function again(){if(++n<4096)queueMicrotask(again)}; queueMicrotask(again);", Cancellation);
            Assert.Equal(4096, host.Evaluate("n", Cancellation).Number);
        }
        using var excessive = new V8ScriptHost(enableMicrotasks: true);
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteClassic("function again(){queueMicrotask(again)}; queueMicrotask(again);", Cancellation));
    }

    [Fact]
    public void TaskBudgetIsSharedAcrossBatchAndCallbackFailureStopsFollowingScripts()
    {
        using var limit = new V8ScriptHost(enableMicrotasks: true);
        Assert.Throws<ScriptLimitException>(() => limit.ExecuteClassicBatch([
            "for(let i=0;i<1024;i++)queueMicrotask(()=>{});",
            "for(let i=0;i<1024;i++)queueMicrotask(()=>{});",
            "for(let i=0;i<1024;i++)queueMicrotask(()=>{});",
            "for(let i=0;i<1024;i++)queueMicrotask(()=>{});",
            "queueMicrotask(()=>{});"
        ], Cancellation));
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html); html.AppendChild(document.CreateElement("head"));
        using var host = new V8ScriptHost(document: document, enableMicrotasks: true);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassicBatch([
            "queueMicrotask(()=>{document.title='before';throw Error('failure')});queueMicrotask(()=>document.title='later callback');",
            "document.title='later script';"
        ], Cancellation));
        Assert.Equal("before", document.Title);
        Assert.Throws<InvalidOperationException>(() => host.ExecuteClassic("42", Cancellation));
    }

    [Fact]
    public void IntrinsicTamperingAndClrAccessCannotReachThePrivateController()
    {
        using var host = new V8ScriptHost(enableMicrotasks: true);
        host.ExecuteClassic("""
            let value=0; Promise.prototype.then = ()=>{throw 'tampered'};
            Promise.prototype.constructor = {get [Symbol.species](){throw 'tampered'}};
            globalThis.Promise = null; globalThis.TypeError = null;
            queueMicrotask(()=>value=42);
            """, Cancellation);
        Assert.Equal(42, host.Evaluate("value", Cancellation).Number);
        Assert.Equal("undefined,undefined,undefined", host.Evaluate("[typeof queueMicrotask.GetType,typeof __visualwebMicrotasks,typeof clr].join(',')", Cancellation).Text);
    }

    [Fact]
    public void InfiniteNativePromiseAndQueueCallbacksRemainWithinSharedDeadline()
    {
        foreach (var source in new[] { "queueMicrotask(()=>{while(true){}});", "Promise.resolve().then(()=>{while(true){}});", "function again(){Promise.resolve().then(again)};again();" })
        {
            using var host = new V8ScriptHost(TimeSpan.FromMilliseconds(150), enableMicrotasks: true);
            Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic(source, Cancellation));
            Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        }
        using var recovered = new V8ScriptHost(enableMicrotasks: true);
        Assert.Equal(42, recovered.Evaluate("42", Cancellation).Number);
    }

    [Fact]
    public void PreCancellationAndNativeCallbackCancellationHaveExplicitOwnership()
    {
        using var host = new V8ScriptHost(enableMicrotasks: true);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("queueMicrotask(()=>{});", canceled.Token));
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
        using var inFlight = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        inFlight.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("queueMicrotask(()=>{while(true){}});", inFlight.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void PromiseReactionsCanEnqueueMicrotasksAndCopiedResultsPrecedeCheckpointEffects()
    {
        using var host = new V8ScriptHost(enableMicrotasks: true);
        host.ExecuteClassic("let value=0; Promise.resolve().then(()=>queueMicrotask(()=>value=1));", Cancellation);
        Assert.Equal(1, host.Evaluate("value", Cancellation).Number);
        Assert.Equal(1, host.Evaluate("queueMicrotask(()=>value=2); value", Cancellation).Number);
        Assert.Equal(2, host.Evaluate("value", Cancellation).Number);
    }

    [Fact]
    public void MicrotasksShareDomBudgetWithSynchronousWorkAndIsolatesNeverShareJobs()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html); html.AppendChild(document.CreateElement("head"));
        using var host = new V8ScriptHost(document: document, enableMicrotasks: true);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            document.title='before';
            for(let i=0;i<4094;i++) void document.title;
            queueMicrotask(()=>{void document.title; document.title='over budget';});
            """, Cancellation));
        Assert.Equal("before", document.Title);
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        using var other = new V8ScriptHost(enableMicrotasks: true);
        other.ExecuteClassic("let own=0; queueMicrotask(()=>own=42);", Cancellation);
        Assert.Equal(42, other.Evaluate("own", Cancellation).Number);
        Assert.Equal("undefined", other.Evaluate("typeof document", Cancellation).Text);
    }

    [Fact]
    public void NativeScriptErrorsInvalidateTaskHostsButPromiseRejectionReportingRemainsDeferred()
    {
        using var failed = new V8ScriptHost(enableMicrotasks: true);
        Assert.Throws<ScriptExecutionException>(() => failed.ExecuteClassic("throw Error('fixture');", Cancellation));
        Assert.Throws<InvalidOperationException>(() => failed.Evaluate("42", Cancellation));
        using var host = new V8ScriptHost(enableMicrotasks: true);
        host.ExecuteClassic("let value=0;Promise.reject(Error('deferred'));queueMicrotask(()=>value=42);", Cancellation);
        Assert.Equal(42, host.Evaluate("value", Cancellation).Number);
    }

    [Fact]
    public void ObservationErrorsCannotMaskLatchedCallbackFailureOrLeaveTasksActive()
    {
        using var failed = new V8ScriptHost(enableMicrotasks: true);
        Assert.Throws<ScriptExecutionException>(() => failed.Evaluate("queueMicrotask(()=>{throw Error('fixture')}); ({})", Cancellation));
        Assert.Throws<InvalidOperationException>(() => failed.Evaluate("42", Cancellation));
        using var host = new V8ScriptHost(enableMicrotasks: true);
        Assert.Throws<UnsupportedScriptValueException>(() => host.Evaluate("queueMicrotask(()=>{}); ({})", Cancellation));
        Assert.Throws<ScriptLimitException>(() => host.Evaluate("queueMicrotask(()=>{}); 'x'.repeat(16385)", Cancellation));
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }
}
