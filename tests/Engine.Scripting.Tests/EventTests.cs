using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class EventTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        var body = document.CreateElement("body"); html.AppendChild(body);
        var child = document.CreateElement("div"); child.SetAttribute("id", "child"); body.AppendChild(child);
        return document;
    }

    [Fact]
    public void EventsAreOptionalNativeBrandedAndNeverExposeClrCallbacks()
    {
        using var plain = new V8ScriptHost(document: Document());
        Assert.Equal("undefined,undefined,undefined", plain.Evaluate("[typeof Event,typeof EventTarget,typeof document.dispatchEvent].join(',')", Cancellation).Text);
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let target=new EventTarget(), ev=new Event('x',{bubbles:1,cancelable:true,composed:true}), called=0;
            target.addEventListener('x',function(e){'use strict';if(this===target && e===ev)++called});
            let result=target.dispatchEvent(ev);
            """, Cancellation);
        Assert.True(host.Evaluate("result && called===1 && ev.target===target && ev.currentTarget===null && ev.eventPhase===0 && ev.composedPath().length===0 && !ev.isTrusted && ev.composed", Cancellation).Boolean);
        Assert.Equal("undefined,undefined,undefined", host.Evaluate("[typeof Event.GetType,typeof target.GetType,typeof __visualwebEvents].join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("Event.AT_TARGET===2 && ev.AT_TARGET===2 && ev.NONE===0", Cancellation).Boolean);
    }

    [Fact]
    public void NodeCaptureTargetBubbleAndPathAreSnapshottedBeforeTreeMutation()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        host.ExecuteClassic("""
            let child=document.getElementById('child'), body=document.body, order=[], seen;
            function listen(node,label,capture){node.addEventListener('x',e=>{
                order.push(label+e.eventPhase); if(node===document && capture){seen=e.composedPath();body.removeChild(child);}
                if(e.target!==child||e.currentTarget!==node)throw Error('identity');
            },capture)}
            listen(document,'doc',true);listen(body,'body',true);listen(child,'child-c',true);
            listen(child,'child-b',false);listen(body,'body-b',false);listen(document,'doc-b',false);
            child.dispatchEvent(new Event('x',{bubbles:true}));
            """, Cancellation);
        Assert.Equal("doc1,body1,child-c2,child-b2,body-b3,doc-b3", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("seen.length===4 && seen[0]===child && seen[1]===body && seen[3]===document && child.parentNode===null", Cancellation).Boolean);
    }

    [Fact]
    public void NonBubblingStillCapturesAndTargetCaptureStopPreventsTargetBubble()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        host.ExecuteClassic("""
            let child=document.getElementById('child'), order=[];
            document.addEventListener('x',()=>order.push('capture'),true);
            document.addEventListener('x',()=>order.push('bubble'));
            child.addEventListener('x',()=>order.push('target'));
            child.dispatchEvent(new Event('x'));
            let stopped=new Event('stop');
            child.addEventListener('stop',e=>{e.stopPropagation();order.push('stop');},true);
            child.addEventListener('stop',()=>order.push('same phase'),true);
            child.addEventListener('stop',()=>order.push('wrong phase'));
            child.dispatchEvent(stopped);
            """, Cancellation);
        Assert.Equal("capture,target,stop,same phase", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("!stopped.cancelBubble && stopped.eventPhase===0", Cancellation).Boolean);
    }

    [Fact]
    public void CancellationPassiveImmediateStopAndRedispatchPreserveEventState()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(), passive=new Event('p',{cancelable:true}), active=new Event('a',{cancelable:true}), count=0;
            t.addEventListener('p',e=>{e.preventDefault();e.returnValue=false;},{passive:true});
            let passiveResult=t.dispatchEvent(passive);
            t.addEventListener('a',e=>{e.preventDefault();e.stopImmediatePropagation();++count});
            t.addEventListener('a',()=>count+=100);
            let activeResult=t.dispatchEvent(active), second=t.dispatchEvent(active);
            let noCancel=new Event('a');t.dispatchEvent(noCancel);
            """, Cancellation);
        Assert.True(host.Evaluate("passiveResult && !passive.defaultPrevented && !activeResult && !second && active.defaultPrevented && !noCancel.defaultPrevented && count===3 && !active.cancelBubble", Cancellation).Boolean);
    }

    [Fact]
    public void ListenerIdentityRemovalSnapshotsOnceAndCallbackObjectsFollowNativeOrdering()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(), order=[], obj={handleEvent(e){if(this!==obj)throw Error('receiver');order.push('object')}};
            const later=()=>order.push('removed'), added=()=>order.push('added');
            const first=()=>{order.push('first');t.removeEventListener('x',later);t.addEventListener('x',added)};
            t.addEventListener('x',first);t.addEventListener('x',first,{once:true});t.addEventListener('x',later);
            t.addEventListener('x',obj,{once:true});
            t.dispatchEvent(new Event('x'));t.dispatchEvent(new Event('x'));
            let once=0;
            t.addEventListener('once',()=>{++once;t.dispatchEvent(new Event('once'));},{once:true});
            t.dispatchEvent(new Event('once'));
            """, Cancellation);
        Assert.Equal("first,object,first,added", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.Equal(1, host.Evaluate("once", Cancellation).Number);
    }

    [Fact]
    public void ListenerAddedInCaptureCanRunInLaterBubblePhase()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(), n=0;
            t.addEventListener('x',()=>t.addEventListener('x',()=>++n),true);
            t.dispatchEvent(new Event('x'));
            """, Cancellation);
        Assert.Equal(1, host.Evaluate("n", Cancellation).Number);
    }

    [Theory]
    [InlineData("Event('x')")]
    [InlineData("new Event()")]
    [InlineData("new Event(Symbol())")]
    [InlineData("new Event('x',42)")]
    [InlineData("t.addEventListener('x',42)")]
    [InlineData("t.addEventListener('x',()=>{},{signal:null})")]
    [InlineData("t.addEventListener('x')")]
    [InlineData("t.removeEventListener('x')")]
    [InlineData("t.dispatchEvent({})")]
    [InlineData("EventTarget.prototype.dispatchEvent.call({},new Event('x'))")]
    [InlineData("Event.prototype.preventDefault.call({})")]
    public void InvalidArgumentsAndReceiversThrowCatchableTypeErrors(string source)
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("let t=new EventTarget();", Cancellation);
        Assert.True(host.Evaluate("(()=>{try{" + source + ";return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Fact]
    public void RedispatchingActiveEventRejectsWithoutPoisoningSuccessfulListener()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(), rejected=false, e=new Event('x');
            t.addEventListener('x',()=>{try{t.dispatchEvent(e)}catch(ex){rejected=ex instanceof TypeError}});
            t.dispatchEvent(e);
            """, Cancellation);
        Assert.True(host.Evaluate("rejected && e.currentTarget===null", Cancellation).Boolean);
    }

    [Fact]
    public void CaughtListenerErrorsLatchTaskFailureAndStopFollowingCallbacksAndScripts()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document, enableEvents: true);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassicBatch([
            "document.addEventListener('x',()=>{document.title='before';throw Error('fixture')});document.addEventListener('x',()=>document.title='later');try{document.dispatchEvent(new Event('x'))}catch{};",
            "document.title='next script';"
        ], Cancellation));
        Assert.Equal("before", document.Title);
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void ExactRetainedListenerLimitsReleaseOnRemoveAndOnceAndLatchExcess()
    {
        using (var host = new V8ScriptHost(enableEvents: true))
        {
            host.ExecuteClassic("""
                let t=new EventTarget(), callbacks=[], n=0;
                for(let i=0;i<1024;i++){callbacks[i]=()=>++n;t.addEventListener('x',callbacks[i],{once:true})}
                t.addEventListener('x',callbacks[0]);
                t.dispatchEvent(new Event('x'));
                for(let i=0;i<1024;i++)t.addEventListener('y',callbacks[i]);
                for(let i=0;i<1024;i++)t.removeEventListener('y',callbacks[i]);
                t.addEventListener('z',()=>{});
                """, Cancellation);
            Assert.Equal(1024, host.Evaluate("n", Cancellation).Number);
        }
        using var excessive = new V8ScriptHost(enableEvents: true);
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteClassic("let t=new EventTarget();try{for(let i=0;i<1025;i++)t.addEventListener('x',()=>{});}catch{}", Cancellation));
        Assert.Throws<InvalidOperationException>(() => excessive.Evaluate("42", Cancellation));
    }

    [Fact]
    public void InvocationLimitsResetPerTaskButRemainSharedAcrossClassicBatch()
    {
        using (var host = new V8ScriptHost(enableEvents: true))
        {
            host.ExecuteClassic("let t=new EventTarget(),n=0;t.addEventListener('x',()=>++n);for(let i=0;i<4096;i++)t.dispatchEvent(new Event('x'));", Cancellation);
            Assert.Equal(4096, host.Evaluate("n", Cancellation).Number);
            host.ExecuteClassic("for(let i=0;i<4096;i++)t.dispatchEvent(new Event('x'));", Cancellation);
            Assert.Equal(8192, host.Evaluate("n", Cancellation).Number);
        }
        using var excessive = new V8ScriptHost(enableEvents: true);
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteClassicBatch([
            "let t=new EventTarget();t.addEventListener('x',()=>{});for(let i=0;i<4096;i++)t.dispatchEvent(new Event('x'));",
            "try{t.dispatchEvent(new Event('x'))}catch{};"
        ], Cancellation));
    }

    [Fact]
    public void ExactNestedDispatchLimitAndCaughtExcessAreEnforced()
    {
        using (var host = new V8ScriptHost(enableEvents: true))
        {
            host.ExecuteClassic("let t=new EventTarget(),n=0;t.addEventListener('x',()=>{if(++n<32)t.dispatchEvent(new Event('x'))});t.dispatchEvent(new Event('x'));", Cancellation);
            Assert.Equal(32, host.Evaluate("n", Cancellation).Number);
        }
        using var excessive = new V8ScriptHost(enableEvents: true);
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteClassic("let t=new EventTarget();t.addEventListener('x',()=>{try{t.dispatchEvent(new Event('x'))}catch{}});t.dispatchEvent(new Event('x'));", Cancellation));
    }

    [Fact]
    public void MicrotasksRunAfterDispatchAndStillShareEventInvocationBudget()
    {
        using var host = new V8ScriptHost(enableEvents: true, enableMicrotasks: true);
        host.ExecuteClassicBatch([
            "let t=new EventTarget(),order=[];t.addEventListener('x',()=>{order.push('listener');queueMicrotask(()=>order.push('micro'))});t.dispatchEvent(new Event('x'));order.push('sync');",
            "order.push('next');"
        ], Cancellation);
        Assert.Equal("listener,sync,micro,next", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic("""
            let budget=new EventTarget();budget.addEventListener('x',()=>{});
            for(let i=0;i<4096;i++)budget.dispatchEvent(new Event('x'));
            queueMicrotask(()=>budget.dispatchEvent(new Event('x')));
            """, Cancellation));
    }

    [Fact]
    public void IntrinsicTamperingCannotHijackPrivateListenerListsOrPath()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(), e=new Event('x'), n=0;
            t.addEventListener('x',()=>++n,{once:true});
            WeakMap.prototype.get=()=>{throw 'tampered'};
            WeakMap.prototype.set=()=>{throw 'tampered'};
            Array.prototype.slice=()=>{throw 'tampered'};
            Array.prototype.splice=()=>{throw 'tampered'};
            Array.prototype.constructor={get [Symbol.species](){throw 'tampered'}};
            Object.defineProperty(Array.prototype,'0',{set(){throw 'tampered'},configurable:true});
            globalThis.TypeError=null;
            t.dispatchEvent(e);t.dispatchEvent(e);
            """, Cancellation);
        Assert.Equal(1, host.Evaluate("n", Cancellation).Number);
    }

    [Fact]
    public void AdoptedNodeEventReceiversRejectBeforeArgumentConversion()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document, enableEvents: true);
        host.ExecuteClassic("let child=document.getElementById('child'),converted=false;child.addEventListener('x',()=>{});", Cancellation);
        var other = new DomDocument(); other.AdoptNode(document.GetElementById("child")!);
        Assert.True(host.Evaluate("(()=>{try{child.addEventListener({toString(){converted=true;return 'x'}},()=>{})}catch(e){return !converted && e instanceof TypeError}})()", Cancellation).Boolean);
    }

    [Fact]
    public void InfiniteListenerInterruptsAndFreshHostRecovers()
    {
        using var host = new V8ScriptHost(TimeSpan.FromMilliseconds(150), enableEvents: true);
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic("let t=new EventTarget();t.addEventListener('x',()=>{while(true){}});t.dispatchEvent(new Event('x'));", Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        using var recovered = new V8ScriptHost(enableEvents: true);
        Assert.Equal(42, recovered.Evaluate("42", Cancellation).Number);
    }

    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    public void ExactAncestorPathLimitPreflightsBeforeAnyListenerRuns(int elements, bool excessive)
    {
        var document = new DomDocument();
        DomNode current = document;
        for (var i = 0; i < elements; i++)
        {
            var next = document.CreateElement("div"); current.AppendChild(next); current = next;
        }
        ((DomElement)current).SetAttribute("id", "leaf");
        using var host = new V8ScriptHost(document: document, enableEvents: true);
        host.ExecuteClassic("document.addEventListener('x',()=>document.title='called',true);let leaf=document.getElementById('leaf');", Cancellation);
        if (excessive)
        {
            Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic("try{leaf.dispatchEvent(new Event('x',{bubbles:true}))}catch{}", Cancellation));
            Assert.Equal("", document.Title);
        }
        else
        {
            host.ExecuteClassic("let pathLength=0;document.addEventListener('x',e=>pathLength=e.composedPath().length,true);leaf.dispatchEvent(new Event('x',{bubbles:true}));", Cancellation);
            Assert.Equal(1024, host.Evaluate("pathLength", Cancellation).Number);
        }
    }

    [Fact]
    public void ListenerObjectGetterErrorsLatchAndNullCallbacksDoNotConsumeCapacity()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget();
            for(let i=0;i<2048;i++)t.addEventListener('x',null);
            t.addEventListener('x',{get handleEvent(){throw Error('getter')}});
            """, Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("try{t.dispatchEvent(new Event('x'))}catch{}", Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void CaptureIdentityAndRemovedReaddedCallbacksAreDistinctForCurrentSnapshot()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            let t=new EventTarget(),order=[],fn=()=>order.push('fn');
            t.addEventListener('x',()=>{t.removeEventListener('x',fn);t.addEventListener('x',fn)});
            t.addEventListener('x',fn);t.addEventListener('x',fn,true);
            t.removeEventListener('x',fn,{capture:true});
            t.dispatchEvent(new Event('x'));
            let e=new Event('z',{cancelable:true});
            e.cancelBubble=true;e.cancelBubble=false;
            let before=e.cancelBubble;t.dispatchEvent(e);e.returnValue=false;
            """, Cancellation);
        Assert.Equal("", host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.True(host.Evaluate("before && !e.cancelBubble && e.defaultPrevented && !e.returnValue", Cancellation).Boolean);
    }

    [Fact]
    public void CallerMutationOfComposedPathAndForgedPropertiesCannotChangeDispatch()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        host.ExecuteClassic("""
            let child=document.getElementById('child'),order=[],e=new Event('x',{bubbles:true});
            Object.defineProperty(e,'type',{value:'forged'});
            document.addEventListener('x',value=>{let path=value.composedPath();path.length=0;order.push('capture')},true);
            child.addEventListener('x',()=>order.push('target'));
            document.addEventListener('x',()=>order.push('bubble'));
            child.dispatchEvent(e);
            """, Cancellation);
        Assert.Equal("capture,target,bubble", host.Evaluate("order.join(',')", Cancellation).Text);
    }

    [Fact]
    public void PreCancellationAndListenerCancellationRespectTaskOwnership()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("new EventTarget();", canceled.Token));
        host.ExecuteClassic("let t=new EventTarget();t.addEventListener('x',()=>{while(true){}});", Cancellation);
        using var inFlight = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        inFlight.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("t.dispatchEvent(new Event('x'));", inFlight.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void MissingDictionariesIgnorePollutedObjectPrototypeAndCallbacksIgnoreReturns()
    {
        using var host = new V8ScriptHost(enableEvents: true);
        host.ExecuteClassic("""
            Object.prototype.capture=true;Object.prototype.once=true;Object.prototype.passive=true;
            Object.prototype.signal=null;Object.prototype.bubbles=true;Object.prototype.cancelable=true;
            let t=new EventTarget(), count=-1, assimilated=false, receiver=false, e=new Event('x');
            function fn(value){'use strict';count=arguments.length;receiver=this===t && value===e;
                return {then(){assimilated=true}};}
            t.addEventListener('x',fn);
            t.dispatchEvent(e);t.removeEventListener('x',fn);
            let plain=!e.bubbles && !e.cancelable;
            """, Cancellation);
        Assert.True(host.Evaluate("count===1 && receiver && !assimilated && plain", Cancellation).Boolean);
    }

    [Fact]
    public void ExactEventTypeBudgetAndCaughtExcessAreEnforced()
    {
        using (var host = new V8ScriptHost(enableEvents: true))
        {
            Assert.Equal(65536, host.Evaluate("new Event('x'.repeat(65536)).type.length", Cancellation).Number);
        }
        using var excessive = new V8ScriptHost(enableEvents: true);
        Assert.Throws<ScriptLimitException>(() => excessive.ExecuteClassic("try{new Event('x'.repeat(65537))}catch{}", Cancellation));
        Assert.Throws<InvalidOperationException>(() => excessive.Evaluate("42", Cancellation));
    }

    [Fact]
    public void DetachedFragmentsHaveTheirOwnPropagationPathAndHostStateIsPrivate()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        host.ExecuteClassic("""
            let fragment=document.createDocumentFragment(), text=document.createTextNode('x'), order=[];
            fragment.appendChild(text);
            document.addEventListener('x',()=>order.push('wrong document'));
            fragment.addEventListener('x',e=>order.push('fragment'+e.composedPath().length),true);
            text.addEventListener('x',()=>order.push('text'));
            text.dispatchEvent(new Event('x',{bubbles:true}));
            """, Cancellation);
        Assert.Equal("fragment2,text", host.Evaluate("order.join(',')", Cancellation).Text);
        using var other = new V8ScriptHost(enableEvents: true);
        Assert.Equal("undefined", other.Evaluate("typeof fragment", Cancellation).Text);
        Assert.True(other.Evaluate("new EventTarget().dispatchEvent(new Event('x'))", Cancellation).Boolean);
    }
}
