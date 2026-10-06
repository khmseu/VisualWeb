using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class RemovalTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void RemoveReturnsUndefinedAndPreservesDetachedSubtreeIdentityAndStaticQuerySnapshots()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,before=document.createTextNode('before'),element=document.createElement('i'),
                text=document.createTextNode('kept'),after=document.createTextNode('after');
                element.id='removed';element.appendChild(text);
                for(let node of [before,element,after])body.appendChild(node);
                let snapshot=body.querySelectorAll('i');
                if(element.remove()!==undefined)return false;
                let ok=body.firstChild===before&&before.nextSibling===after&&after.previousSibling===before
                    &&element.parentNode===null&&element.nextSibling===null&&element.previousSibling===null
                    &&text.parentNode===element&&text.ownerDocument===document&&element.ownerDocument===document
                    &&text.getRootNode()===element&&!text.isConnected&&element.textContent==='kept'
                    &&document.getElementById('removed')===null&&snapshot[0]===element;
                element.remove();body.appendChild(element);
                return ok&&body.lastChild===element&&document.getElementById('removed')===element})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void TextCommentsInstructionsDoctypesAndRootElementsUseTheChildNodeMixin()
    {
        var document = Document(); document.InsertBefore(document.CreateDocumentType("html"), document.DocumentElement);
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let fragment=document.createDocumentFragment(),nodes=[
                document.createTextNode('text'),document.createComment('comment'),document.createProcessingInstruction('probe','data')];
                for(let node of nodes){fragment.appendChild(node);node.remove();node.remove();
                    if(fragment.hasChildNodes()||node.ownerDocument!==document)return false;}
                let doctype=document.firstChild;doctype.remove();doctype.remove();
                let root=document.documentElement;root.remove();
                return document.firstChild===null&&root.parentNode===null&&root.ownerDocument===document
                    &&root.firstChild.parentNode===root&&doctype.nodeType===10})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void DetachedNoopsIgnoreArgumentsAndBorrowedMethodsRejectUnsupportedReceivers()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let element=document.createElement('i'),argument={toString(){throw 42},valueOf(){throw 43}};
                if(element.remove(argument)!==undefined||document.remove!==undefined)return false;
                for(let fake of [{},Object.create(element),null,document,document.createDocumentFragment()]){
                    try{element.remove.call(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                }return true})()
            """, Cancellation).Boolean);
        host.ExecuteClassic("""
            let remove='outer',element=document.createElement('i'),selected;
            with(element){selected=remove}
            """, Cancellation);
        Assert.Equal("outer", host.Evaluate("selected", Cancellation).Text);
        Assert.True(host.Evaluate("element[Symbol.unscopables].remove===true", Cancellation).Boolean);
    }

    [Fact]
    public void AdoptedWrappersRejectEvenWhenDetached()
    {
        var document = Document(); var element = document.CreateElement("i"); document.Body!.AppendChild(element);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element=document.body.firstChild", Cancellation);
        new DomDocument().AdoptNode(element);
        Assert.True(host.Evaluate("""
            (()=>{try{element.remove();return false}catch(e){return e instanceof TypeError&&!e.GetType}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactParentSubtreeBoundSucceedsAndOverLimitRemovalIsAtomic()
    {
        var document = Document(); var body = document.Body!; var target = document.CreateElement("i");
        body.AppendChild(target);
        for (var i = 0; i < 8191; i++) { body.AppendChild(document.CreateComment("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let target=document.body.firstChild;target.remove()", Cancellation);
        Assert.Null(target.ParentNode); Assert.Equal(8191, body.ChildNodes.Count);
        body.InsertBefore(target, body.FirstChild); body.AppendChild(document.CreateComment(""));
        Assert.True(host.Evaluate("""
            (()=>{try{target.remove();return false}catch(e){return e instanceof TypeError&&target.parentNode!==null}})()
            """, Cancellation).Boolean);
        Assert.Same(body, target.ParentNode); Assert.Same(target, body.FirstChild); Assert.Equal(8193, body.ChildNodes.Count);
    }

    [Fact]
    public void ExactAncestorBoundSucceedsAndAnAdditionalAncestorRejectsBeforeRemoval()
    {
        var document = Document(); var target = document.CreateComment("kept"); document.Body!.AppendChild(target);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let target=document.body.firstChild", Cancellation);
        var parent = document.Body!;
        for (var i = 0; i < 8189; i++)
        {
            var next = document.CreateElement("i"); parent.AppendChild(next); parent = next;
        }
        parent.AppendChild(target);
        host.ExecuteClassic("target.remove()", Cancellation); Assert.Null(target.ParentNode);
        var additional = document.CreateElement("i"); parent.AppendChild(additional); additional.AppendChild(target);
        Assert.True(host.Evaluate("""
            (()=>{try{target.remove();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Same(additional, target.ParentNode); Assert.Same(target, additional.FirstChild);
    }

    [Fact]
    public void NestedSubtreeCountsPreventRemovalWithoutReadingPayloadOrAllocatingIdentities()
    {
        var document = Document(); var body = document.Body!; var target = document.CreateElement("i");
        body.AppendChild(target);
        for (var i = 0; i < 8192; i++) { target.AppendChild(document.CreateComment("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let target=document.body.firstChild", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{target.remove();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Same(body, target.ParentNode);
        target.RemoveChild(target.LastChild!);
        ((DomComment)target.FirstChild!).Data = new string('x', 65537);
        host.ExecuteClassic("for(let i=0;i<1022;++i)document.createTextNode('');target.remove()", Cancellation);
        Assert.Null(target.ParentNode); Assert.Equal(8191, target.ChildNodes.Count);
        Assert.True(host.Evaluate("target.ownerDocument===document&&target.remove()===undefined", Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{document.createComment('');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SharedCallbackLimitPrecedesRemovalAndPayloadOutputBudgetIsNotNeeded()
    {
        var document = Document(); var text = document.CreateTextNode(new string('x', 65536)); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild,failed=false", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<2048;++i)text.length",
            "for(let i=0;i<2048;++i)text.length",
            "try{text.remove()}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean); Assert.Same(document.Body, text.ParentNode);
        host.ExecuteClassic("for(let i=0;i<4;++i)text.data;text.remove()", Cancellation);
        Assert.Null(text.ParentNode); Assert.Equal(65536, text.Length);
    }

    [Fact]
    public void RemovedListenersAndSnapshottedDispatchPathsSurviveSelfRemoval()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,element=document.createElement('i'),hits=[];
                body.appendChild(element);element.addEventListener('probe',()=>{hits.push('target');element.remove()});
                body.addEventListener('probe',()=>hits.push('parent'));
                element.dispatchEvent(new Event('probe',{bubbles:true}));
                element.dispatchEvent(new Event('probe',{bubbles:true}));
                return hits.join(',')==='target,parent,target'&&element.parentNode===null&&element.ownerDocument===document})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CapturedChildBrandsAndCancellationCannotBypassHostInvalidation()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let element=document.createElement('i');document.body.appendChild(element);
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            element.remove();let ok=element.parentNode===null;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{element.remove()}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
