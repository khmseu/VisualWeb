using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class TreeInspectionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void InclusiveContainmentIdentityAndRootFollowLiveDetachedAndFragmentTrees()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let body=document.body, node=document.createElement('div'), text=document.createTextNode('x');
            node.appendChild(text);body.appendChild(node);
            let connected=document.contains(text)&&body.contains(node)&&node.contains(node)&&!text.contains(node)
                &&!body.contains(null)&&!body.contains(undefined)&&body.isSameNode(document.body)
                &&!body.isSameNode()&&!body.isSameNode(undefined)&&!body.isSameNode(node)
                &&document.getRootNode()===document&&text.getRootNode()===document&&text.parentElement===node
                &&document.documentElement.parentElement===null&&document.parentElement===null
                &&body.hasChildNodes()&&!text.hasChildNodes();
            body.removeChild(node);
            let detached=text.getRootNode()===node&&node.getRootNode()===node&&!document.contains(node);
            let fragment=document.createDocumentFragment();fragment.appendChild(node);
            let fragmented=text.getRootNode({composed:true})===fragment&&node.parentElement===null&&fragment.contains(text);
            body.appendChild(fragment);
            let moved=text.getRootNode()===document&&fragment.getRootNode()===fragment&&!fragment.hasChildNodes()
                &&!fragment.contains(node)&&node.parentElement===body;
            """, Cancellation);
        Assert.True(host.Evaluate("connected&&detached&&fragmented&&moved", Cancellation).Boolean);
    }

    [Fact]
    public void ElementNavigationSkipsTextAndCommentsAndTracksMutations()
    {
        var document = Document();
        document.Body!.AppendChild(document.CreateComment("before"));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let body=document.body,a=document.createElement('a'),b=document.createElement('b'),text=document.createTextNode('gap');
            body.appendChild(a);body.appendChild(text);body.appendChild(b);body.appendChild(document.createTextNode('tail'));
            let ok=body.firstElementChild===a&&body.lastElementChild===b&&body.childElementCount===2
                &&body.firstChild.nextElementSibling===a&&a.nextElementSibling===b&&b.previousElementSibling===a
                &&text.previousElementSibling===a&&text.nextElementSibling===b
                &&a.previousElementSibling===null&&b.nextElementSibling===null
                &&document.firstElementChild===document.documentElement&&document.childElementCount===1;
            body.removeChild(a);ok=ok&&body.firstElementChild===b&&text.previousElementSibling===null;
            let fragment=document.createDocumentFragment();fragment.appendChild(a);
            ok=ok&&fragment.firstElementChild===a&&fragment.lastElementChild===a&&fragment.childElementCount===1
                &&a.previousElementSibling===null&&a.nextElementSibling===null;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void OptionsConversionPrecedesRootScanAndBrandsPrecedeGetters()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let node=document.createElement('div');document.body.appendChild(node);
                let accessed=0,options={get composed(){accessed++;document.body.removeChild(node);return true}};
                let ok=node.getRootNode(options)===node&&accessed===1&&node.getRootNode(null)===node
                    &&node.getRootNode(()=>{})===node;
                for(let bad of [true,1,'x',Symbol(),1n]){
                    try{node.getRootNode(bad);return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                try{node.getRootNode.call({},options);return false}catch(e){if(!(e instanceof TypeError))return false}
                try{node.getRootNode({get composed(){throw Error('getter')}});return false}
                catch(e){ok=ok&&e.message==='getter'}
                return ok&&accessed===1})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InvalidNodeArgumentsAndParentOrSiblingReceiversFailExplicitly()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let node=document.body,text=document.createTextNode('x'),fragment=document.createDocumentFragment();
                for(let bad of [{},1,'x',Symbol(),Object.create(node)]){
                    for(let method of ['contains','isSameNode']){
                        try{node[method](bad);return false}catch(e){if(!(e instanceof TypeError))return false}
                    }
                }
                try{node.contains();return false}catch(e){if(!(e instanceof TypeError))return false}
                for(let property of ['firstElementChild','lastElementChild','childElementCount']){
                    try{text[property];return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                try{fragment.nextElementSibling;return false}catch(e){if(!(e instanceof TypeError))return false}
                for(let method of ['contains','isSameNode','getRootNode','hasChildNodes']){
                    try{node[method].call({},node);return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                return true})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ChildAndSiblingScanBoundsAreExactAndRecoverAfterNativeMutation()
    {
        var document = Document();
        var body = document.Body!;
        var first = document.CreateElement("div"); body.AppendChild(first);
        for (var i = 1; i < 8192; i++) { body.AppendChild(document.CreateTextNode("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body,first=body.firstElementChild;", Cancellation);
        Assert.True(host.Evaluate("body.childElementCount===1&&body.lastElementChild===first&&first.nextElementSibling===null", Cancellation).Boolean);
        var extra = document.CreateElement("span"); body.AppendChild(extra);
        Assert.True(host.Evaluate("""
            (()=>{for(let property of ['firstElementChild','lastElementChild','childElementCount']){
                try{body[property];return false}catch(e){if(!(e instanceof TypeError))return false}
            }try{first.nextElementSibling;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        body.RemoveChild(extra);
        Assert.Equal(1, host.Evaluate("body.childElementCount", Cancellation).Number);
    }

    [Fact]
    public void AncestorScanBoundIsExactButInclusiveContainsCanShortCircuit()
    {
        var document = new DomDocument();
        var outer = document.CreateElement("div"); document.AppendChild(outer);
        var deepest = outer;
        for (var i = 0; i < 8190; i++)
        {
            var next = document.CreateElement("div"); deepest.AppendChild(next); deepest = next;
        }
        deepest.SetAttribute("id", "deep");
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let deep=document.getElementById('deep');", Cancellation);
        Assert.True(host.Evaluate("deep.getRootNode()===document&&document.contains(deep)", Cancellation).Boolean);
        document.RemoveChild(outer);
        var inserted = document.CreateElement("section"); document.AppendChild(inserted); inserted.AppendChild(outer);
        Assert.True(host.Evaluate("""
            (()=>{let root=false,contains=false;
                try{deep.getRootNode()}catch(e){root=e instanceof TypeError}
                try{document.contains(deep)}catch(e){contains=e instanceof TypeError}
                return root&&contains&&deep.contains(deep)&&deep.parentElement.contains(deep)})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SavedWrappersAndBothNodeOperandsRejectAdoption()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body,html=document.documentElement;", Cancellation);
        new DomDocument().AdoptNode(document.Body!);
        Assert.True(host.Evaluate("""
            (()=>{for(let operation of [()=>body.getRootNode(),()=>body.contains(null),()=>body.isSameNode(),
                ()=>html.contains(body),()=>html.isSameNode(body),()=>body.parentElement,()=>body.childElementCount]){
                    try{operation();return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                }return true})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SharedCallBudgetAndCancellationApplyToInspection()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let body=document.body,failed=false;", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<2048;++i)body.contains(body);",
            "for(let i=0;i<2048;++i)body.getRootNode();",
            "try{body.hasChildNodes()}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed&&body.getRootNode()===document", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("while(true){try{body.contains(body)}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void RootResultsRespectLifetimeIdentityCapacityWithoutChangingTree()
    {
        var document = Document();
        var outer = document.CreateElement("section");
        var inner = document.CreateElement("div"); inner.SetAttribute("id", "inner");
        outer.AppendChild(inner); document.Body!.AppendChild(outer);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let inner=document.getElementById('inner');
            for(let i=0;i<1023;++i)document.createElement('i');
            """, Cancellation);
        document.Body.RemoveChild(outer);
        Assert.True(host.Evaluate("""
            (()=>{try{inner.getRootNode();return false}catch(e){
                return e instanceof TypeError&&e.message.includes('identity')&&inner.contains(inner)}})()
            """, Cancellation).Boolean);
        Assert.Same(outer, inner.ParentNode);
    }

    [Fact]
    public void CapturedBrandsAndNavigationSurviveGlobalAndPrototypeTampering()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let body=document.body,html=document.documentElement;
            WeakMap.prototype.get=WeakMap.prototype.set=()=>{throw Error('brands')};
            globalThis.TypeError=()=>{throw 42};
            let accessed=false,ok=body.getRootNode({get composed(){accessed=true;return true}})===document;
            let rejected=false;try{body.contains({})}catch(e){rejected=e.name==='TypeError'}
            ok=ok&&accessed&&rejected&&html.contains(body)&&html.lastElementChild===body
                &&body.previousElementSibling===document.head;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }
}
