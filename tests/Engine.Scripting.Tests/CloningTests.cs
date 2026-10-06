using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class CloningTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void ClonesUseFreshIdentityPreservePayloadOrderAndDetachOriginalAndCloneTrees()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        Assert.True(host.Evaluate("""
            (()=>{let source=document.createElement('MiXeD');source.setAttribute('a','one');source.setAttribute('b','two');
                let text=document.createTextNode('data'),comment=document.createComment('ignored');
                source.appendChild(text);source.appendChild(comment);document.body.appendChild(source);
                let hits=0;source.addEventListener('probe',()=>hits++);
                let shallow=source.cloneNode(),deep=source.cloneNode(true);
                if(shallow===source||deep===source||shallow.parentNode!==null||deep.parentNode!==null
                    ||shallow.localName!=='mixed'||shallow.childNodes!==undefined
                    ||shallow.hasChildNodes()||deep.firstChild.data!=='data'||deep.firstChild===text
                    ||deep.lastChild.data!=='ignored'||deep.firstChild.ownerDocument!==document
                    ||deep.getAttributeNames().join(',')!=='a,b')return false;
                deep.dispatchEvent(new Event('probe'));source.dispatchEvent(new Event('probe'));
                let snapshot=document.querySelectorAll('mixed');source.remove();
                document.body.appendChild(deep);
                return hits===1&&source.parentNode===null&&text.parentNode===source
                    &&deep.parentNode===document.body&&snapshot.length===1&&snapshot[0]===source
                    &&document.body.lastChild===deep&&deep.nodeName==='MIXED'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ShallowAndDeepCloningCoverCharacterDataInstructionsDoctypesAndFragments()
    {
        var document = Document(); document.InsertBefore(document.CreateDocumentType("HTML", "PUBLIC", "SYSTEM"), document.DocumentElement);
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let doctype=document.doctype,d=doctype.cloneNode();
                if(d===doctype||d.parentNode!==null||d.name!=='HTML'||d.publicId!=='PUBLIC'||d.systemId!=='SYSTEM')return false;
                let comment=document.createComment('raw'),pi=document.createProcessingInstruction('Probe','data');
                let fragment=document.createDocumentFragment();fragment.appendChild(comment);fragment.appendChild(pi);
                let shallow=fragment.cloneNode(),deep=fragment.cloneNode(true);
                if(shallow.hasChildNodes()||deep.firstChild.data!=='raw'||deep.lastChild.target!=='Probe'
                    ||deep.firstChild===comment||deep.lastChild===pi||deep.ownerDocument!==document)return false;
                return comment.parentNode===fragment&&pi.parentNode===fragment&&d.nodeType===10
                    &&document.doctype===doctype})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CloneBooleanUsesToBooleanAndReceiverChecksPrecedeAnyWork()
    {
        using var host = new V8ScriptHost(document: Document());
        var result = host.Evaluate("""
            (()=>{let source=document.createElement('i'),child=document.createTextNode('x');source.appendChild(child);
                let calls=0,deep=source.cloneNode({valueOf(){calls++;throw 1},toString(){calls++;throw 2}});
                let detail=[calls,deep.hasChildNodes(),deep.firstChild&&deep.firstChild.data,deep.firstChild===child,
                    source.cloneNode(0).hasChildNodes()];
                let shallow=source.cloneNode(0);
                detail.push(shallow.firstChild===null);
                for(let receiver of [{},Object.create(source),null,document]){
                    try{source.cloneNode.call(receiver,true);detail.push('accepted')}
                    catch(e){detail.push(e instanceof TypeError&&!e.GetType)}
                }
                return detail.join(',')})()
            """, Cancellation).Text;
        Assert.Equal("0,true,x,false,false,true,true,true,true,true", result);
    }

    [Fact]
    public void UnsupportedDocumentCloneRejectsExplicitlyWithoutCreatingAnIdentity()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{try{document.cloneNode(false);return false}catch(e){return e instanceof TypeError&&document.documentElement.localName==='html'}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void DeepCloneEnforcesAtomicNodeAttributeAndSharedTextPreflight()
    {
        var document = Document(); var root = document.CreateElement("div"); document.Body!.AppendChild(root);
        for (var i = 0; i < 8191; i++) { root.AppendChild(document.CreateComment("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let root=document.body.firstChild", Cancellation);
        Assert.True(host.Evaluate("root.cloneNode(true).lastChild!==null", Cancellation).Boolean);
        root.AppendChild(document.CreateComment(""));
        Assert.True(host.Evaluate("""
            (()=>{try{root.cloneNode(true);return false}catch(e){return e instanceof TypeError&&root.childElementCount===0}})()
            """, Cancellation).Boolean);
        Assert.Same(document.Body, root.ParentNode); Assert.Equal(8192, root.ChildNodes.Count);
        root.RemoveChild(root.LastChild!);
        ((DomComment)root.FirstChild!).Data = new string('x', 65537);
        Assert.True(host.Evaluate("""
            (()=>{try{root.cloneNode(true);return false}catch(e){return e instanceof TypeError&&root.parentNode===document.body}})()
            """, Cancellation).Boolean);
        Assert.Equal(8191, root.ChildNodes.Count);
    }

    [Fact]
    public void ShallowCloneNeedNotScanDescendantsButEveryCopiedAttributeValueIsBounded()
    {
        var document = Document(); var root = document.CreateElement("div"); document.Body!.AppendChild(root);
        for (var i = 0; i < 8192; i++) { root.AppendChild(document.CreateComment("")); }
        root.SetAttribute("large", new string('x', 65537));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let root=document.body.firstChild", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{root.cloneNode();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        root.RemoveAttribute("large");
        Assert.True(host.Evaluate("!root.cloneNode().hasChildNodes()&&root.childElementCount===0", Cancellation).Boolean);
    }

    [Fact]
    public void WrapperCapacityFailureDoesNotChangeSourceOrReserveAnIdentity()
    {
        var document = Document(); var source = document.CreateElement("div"); document.Body!.AppendChild(source);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let source=document.body.firstChild;for(let i=0;i<1022;++i)document.createTextNode('')", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{source.cloneNode();return false}catch(e){return e instanceof TypeError&&source.parentNode===document.body}})()
            """, Cancellation).Boolean);
        source.Remove();
        Assert.True(host.Evaluate("""
            (()=>{try{source.cloneNode();return false}catch(e){
                return e instanceof TypeError&&source.parentNode===null&&source.ownerDocument===document
            }})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OversizedCloneOutputDoesNotRegisterTheCopyAndCapturedBrandRemainsPrivate()
    {
        var document = Document(); var source = document.CreateElement("div"); document.Body!.AppendChild(source);
        source.SetAttribute("payload", new string('x', 65536));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let source=document.body.firstChild,failed=false;
            WeakMap.prototype.get=()=>{throw 42};
            """, Cancellation);
        host.ExecuteClassic("for(let i=0;i<3;++i)source.getAttribute('payload');try{source.cloneNode()}catch(e){failed=e.name==='TypeError'}", Cancellation);
        Assert.True(host.Evaluate("failed&&source.parentNode===document.body", Cancellation).Boolean);
        source.Remove();
        Assert.True(host.Evaluate("source.parentNode===null&&source.ownerDocument===document", Cancellation).Boolean);
    }

    [Fact]
    public void CancellationDuringCloningInvalidatesTheHostWithoutChangingSource()
    {
        var document = Document(); var root = document.CreateElement("div"); document.Body!.AppendChild(root);
        for (var i = 0; i < 8191; i++) { root.AppendChild(document.CreateComment("payload")); }
        using var host = new V8ScriptHost(document: document);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(1));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("document.body.firstChild.cloneNode(true)", canceled.Token));
        Assert.Same(document.Body, root.ParentNode); Assert.Equal(8191, root.ChildNodes.Count);
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
