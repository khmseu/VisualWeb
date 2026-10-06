using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class CharacterFactoryTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void CreatedCommentsAndInstructionsHaveLiveCharacterDataIdentityAndMetadata()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let comment=document.createComment('-->\0\ud800'),pi=document.createProcessingInstruction('Probe:Name','a\0\ud800');
                if(comment.nodeType!==8||comment.nodeName!=='#comment'||comment.data!=='-->\0\ud800'
                    ||pi.nodeType!==7||pi.target!=='Probe:Name'||pi.nodeName!==pi.target||pi.data!=='a\0\ud800'
                    ||comment.ownerDocument!==document||pi.ownerDocument!==document
                    ||comment.parentNode!==null||pi.parentNode!==null)return false;
                let fragment=document.createDocumentFragment();fragment.appendChild(comment);fragment.appendChild(pi);
                document.body.appendChild(fragment);pi.appendData('tail');comment.replaceData(0,3,'edited');
                return document.body.firstChild===comment&&comment.nextSibling===pi
                    &&pi.data==='a\0\ud800tail'&&comment.data==='edited\0\ud800'&&document.body.textContent===''
                    &&pi.target==='Probe:Name'&&fragment.firstChild===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ConversionIsOrderedAfterReceiverAndRequiredChecksAndExtraArgumentsAreIgnored()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let order=[],target={toString(){order.push('target');return 'probe'}},
                data={toString(){order.push('data');return 'payload'}},extra={toString(){throw 42}};
                let pi=document.createProcessingInstruction(target,data,extra),comment=document.createComment(null,extra);
                if(order.join(',')!=='target,data'||pi.target!=='probe'||pi.data!=='payload'||comment.data!=='null'
                    ||document.createComment(undefined).data!=='undefined'
                    ||document.createProcessingInstruction(null,undefined).target!=='null')return false;
                order=[];
                for(let action of [
                    ()=>document.createProcessingInstruction(target),
                    ()=>document.createComment(),
                    ()=>document.createProcessingInstruction.call({},target,data),
                    ()=>document.createComment.call({},data)]){
                    try{action();return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                if(order.length!==0)return false;
                try{document.createProcessingInstruction(Symbol(),data);return false}catch(e){if(!(e instanceof TypeError))return false}
                if(order.length!==0)return false;
                try{document.createProcessingInstruction('bad name',data);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                return order.join(',')==='data'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InvalidInitialTargetsAndDataDoNotConsumeIdentitiesOrChangeTheTree()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            for(let target of ['', '1bad','bad name','\0','\ud800']){
                try{document.createProcessingInstruction(target,'data');throw 42}catch(e){if(!(e instanceof TypeError))throw e}
            }
            try{document.createProcessingInstruction('probe','a?>b');throw 42}catch(e){if(!(e instanceof TypeError))throw e}
            for(let i=0;i<1023;++i)document.createComment('');
            let pi=document.createProcessingInstruction('probe','');
            """, Cancellation);
        Assert.True(host.Evaluate("pi.target==='probe'&&pi.parentNode===null", Cancellation).Boolean);
        Assert.Empty(document.Body!.ChildNodes);
        Assert.True(host.Evaluate("""
            (()=>{try{document.createComment('');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void TargetGetterIsReadonlyBrandedCasePreservedAndRejectsAdoptedWrappers()
    {
        var document = Document(); var pi = document.CreateProcessingInstruction("Probe", "data"); document.Body!.AppendChild(pi);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let pi=document.body.firstChild", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{'use strict';let descriptor=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(pi),'target');
                if(descriptor.set!==undefined||descriptor.configurable||!descriptor.enumerable)return false;
                try{pi.target='other';return false}catch(e){if(!(e instanceof TypeError))return false}
                for(let fake of [{},Object.create(pi),document,document.body,document.createComment(''),document.createTextNode('')]){
                    try{descriptor.get.call(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                }
                pi.data='?>';return pi.target==='Probe'&&pi.nodeName==='Probe'&&pi.data==='?>'})()
            """, Cancellation).Boolean);
        new DomDocument().AdoptNode(pi);
        Assert.True(host.Evaluate("""
            (()=>{try{pi.target;return false}catch(e){return e instanceof TypeError&&!e.GetType}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void PerFieldInputAndTargetOutputLimitsAreExactAndFailedInputsReserveNoHandles()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let comment=document.createComment('x'.repeat(65536)),
                pi=document.createProcessingInstruction('P'.repeat(65536),'d'.repeat(65536));
            """, Cancellation);
        Assert.Equal(65536, host.Evaluate("comment.length", Cancellation).Number);
        Assert.Equal(65536, host.Evaluate("pi.target.length", Cancellation).Number);
        Assert.Equal(65536, host.Evaluate("pi.length", Cancellation).Number);
        Assert.True(host.Evaluate("""
            (()=>{for(let i=0;i<4;++i)pi.target;
                try{pi.target;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let action of [
                ()=>document.createComment('x'.repeat(65537)),
                ()=>document.createProcessingInstruction('P'.repeat(65537),''),
                ()=>document.createProcessingInstruction('probe','x'.repeat(65537))]){
                try{action()}catch(e){if(e instanceof TypeError)rejected++}
            }return rejected===3})()
            """, Cancellation).Boolean);
        var document = Document(); document.Body!.AppendChild(document.CreateProcessingInstruction(new string('P', 65537), ""));
        using var oversized = new V8ScriptHost(document: document);
        Assert.True(oversized.Evaluate("""
            (()=>{try{document.body.firstChild.target;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OutputAndSharedCallbackFailuresReserveNoFactoryIdentities()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let seed=document.createComment('x'.repeat(65536)),failed=false", Cancellation);
        host.ExecuteClassic("""
            for(let i=0;i<4;++i)seed.data;
            try{document.createComment('')}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        host.ExecuteClassicBatch([
            "failed=false;for(let i=0;i<2048;++i)seed.length",
            "for(let i=0;i<2048;++i)seed.length",
            "try{document.createProcessingInstruction('probe','')}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        host.ExecuteClassic("""
            for(let i=0;i<1022;++i)document.createComment('');
            let last=document.createProcessingInstruction('probe','');
            """, Cancellation);
        Assert.True(host.Evaluate("last.target==='probe'", Cancellation).Boolean);
    }

    [Fact]
    public void CommentAndInstructionBarriersSurviveNormalizationAndListenersStayIndependent()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        Assert.True(host.Evaluate("""
            (()=>{let p=document.body,a=document.createTextNode('a'),comment=document.createComment('barrier'),
                b=document.createTextNode('b'),pi=document.createProcessingInstruction('probe','raw'),c=document.createTextNode('c');
                for(let node of [a,comment,b,pi,c])p.appendChild(node);
                let hits=[];comment.addEventListener('probe',()=>hits.push('comment'));
                pi.addEventListener('probe',()=>hits.push('pi'));p.normalize();
                comment.dispatchEvent(new Event('probe'));pi.dispatchEvent(new Event('probe'));
                return a.wholeText==='a'&&b.wholeText==='b'&&c.wholeText==='c'&&p.textContent==='abc'
                    &&hits.join(',')==='comment,pi'&&comment.isEqualNode(document.createComment('barrier'))
                    &&pi.isEqualNode(document.createProcessingInstruction('probe','raw'))})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CapturedBrandsAndInFlightCancellationPreserveFactoryHostBoundaries()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            let pi=document.createProcessingInstruction('Probe','data'),comment=document.createComment('data');
            let ok=pi.target==='Probe'&&comment.data==='data'&&pi.ownerDocument===document;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{document.createComment('')}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
