using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class NormalizationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void NormalizeReturnsUndefinedAndPreservesLiveIdentitiesAndDetachedData()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let p=document.body,empty=document.createTextNode(''),a=document.createTextNode('a\ud83d'),
                b=document.createTextNode('\ude00b');p.appendChild(empty);p.appendChild(a);p.appendChild(b);
                if(p.normalize()!==undefined)return false;
                let ok=p.firstChild===a&&p.lastChild===a&&a.data==='a\u{1f600}b'&&a.wholeText===a.data
                    &&empty.parentNode===null&&b.parentNode===null&&b.data==='\ude00b'&&b.getRootNode()===b;
                p.normalize();return ok&&p.firstChild===a&&a.nextSibling===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void NestedDocumentAndDetachedFragmentNormalizationRespectsBarriersAndStaticLists()
    {
        var document = Document(); var body = document.Body!;
        body.AppendChild(document.CreateTextNode("left")); body.AppendChild(document.CreateComment("stop"));
        body.AppendChild(document.CreateTextNode("right"));
        body.AppendChild(document.CreateProcessingInstruction("probe", "data"));
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let p=document.body,comment=p.firstChild.nextSibling,pi=p.lastChild;
                let nested=document.createElement('i');p.appendChild(nested);
                nested.appendChild(document.createTextNode('a'));nested.appendChild(document.createTextNode('b'));
                let snapshot=p.querySelectorAll('i');document.normalize();
                let ok=p.firstChild.data==='left'&&comment.data==='stop'&&pi.data==='data'
                    &&snapshot.item(0)===nested&&nested.firstChild.data==='ab'&&nested.firstChild===nested.lastChild;
                let fragment=document.createDocumentFragment();fragment.appendChild(document.createTextNode(''));
                fragment.appendChild(document.createTextNode(''));fragment.normalize();
                return ok&&!fragment.hasChildNodes()})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void LeafReceiversAreNoopsAndExtraArgumentsAreNotConverted()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let p=document.body,a=document.createTextNode(''),b=document.createTextNode('b');
                p.appendChild(a);p.appendChild(b);let argument={toString(){throw 42},valueOf(){throw 43}};
                a.normalize(argument);b.normalize(argument);
                return p.firstChild===a&&a.nextSibling===b&&b.data==='b'&&p.normalize(argument)===undefined&&p.firstChild===b})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ReceiverBrandsAndAdoptedOwnershipRejectBeforeMutation()
    {
        var document = Document(); var element = document.CreateElement("div"); document.Body!.AppendChild(element);
        element.AppendChild(document.CreateTextNode("a")); element.AppendChild(document.CreateTextNode("b"));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element=document.body.firstChild,normalize=element.normalize;", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{for(let receiver of [null,{},Object.create(element)]){
                try{normalize.call(receiver);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
            }return true})()
            """, Cancellation).Boolean);
        new DomDocument().AdoptNode(element);
        Assert.True(host.Evaluate("""
            (()=>{try{element.normalize();return false}catch(e){return e instanceof TypeError&&!e.GetType}})()
            """, Cancellation).Boolean);
        Assert.Equal(2, element.ChildNodes.Count);
    }

    [Fact]
    public void ExactMergedStorageLimitSucceedsAndLaterOversizedRunFailsAtomically()
    {
        var document = Document(); var body = document.Body!;
        var a = document.CreateTextNode(new string('a', 32768)); var b = document.CreateTextNode(new string('b', 32768));
        body.AppendChild(a); body.AppendChild(b);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("document.body.normalize()", Cancellation);
        Assert.Same(a, Assert.Single(body.ChildNodes)); Assert.Equal(65536, a.Length); Assert.Null(b.ParentNode);
        body.RemoveChild(a);
        var early = document.CreateElement("early"); var late = document.CreateElement("late");
        body.AppendChild(early); body.AppendChild(late);
        foreach (var parent in new[] { early, late })
        { parent.AppendChild(document.CreateTextNode("a")); parent.AppendChild(document.CreateTextNode("b")); }
        early.FirstChild!.TextContent = new string('x', 65536);
        Assert.True(host.Evaluate("""
            (()=>{try{document.body.normalize();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal(2, early.ChildNodes.Count); Assert.Equal(2, late.ChildNodes.Count);
        Assert.Equal("a", late.FirstChild!.TextContent);
    }

    [Fact]
    public void ExactDescendantLimitSucceedsButOverLimitFailsBeforeRemovingAnyEmptyNodes()
    {
        var document = Document(); var body = document.Body!;
        for (var i = 0; i < 8192; i++) { body.AppendChild(document.CreateTextNode("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("document.body.normalize()", Cancellation);
        Assert.Empty(body.ChildNodes);
        for (var i = 0; i < 8193; i++) { body.AppendChild(document.CreateTextNode("")); }
        Assert.True(host.Evaluate("""
            (()=>{try{document.body.normalize();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal(8193, body.ChildNodes.Count);
        foreach (var child in body.ChildNodes.ToArray()) { body.RemoveChild(child); }
        var empty = document.CreateTextNode(""); var nested = document.CreateElement("div");
        body.AppendChild(empty); body.AppendChild(nested);
        for (var i = 0; i < 8191; i++) { nested.AppendChild(document.CreateComment("")); }
        Assert.True(host.Evaluate("""
            (()=>{try{document.body.normalize();return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Same(body, empty.ParentNode); Assert.Equal(8191, nested.ChildNodes.Count);
        nested.RemoveChild(nested.LastChild!);
        host.ExecuteClassic("document.body.normalize()", Cancellation);
        Assert.Same(nested, Assert.Single(body.ChildNodes)); Assert.Null(empty.ParentNode);
    }

    [Fact]
    public void AggregateTextBudgetIsSharedAndRejectsTheEntirePlanBeforeWrites()
    {
        var document = Document(); var body = document.Body!;
        for (var i = 0; i < 4; i++)
        {
            var element = document.CreateElement("div"); body.AppendChild(element);
            element.AppendChild(document.CreateTextNode(new string('x', 32768)));
            element.AppendChild(document.CreateTextNode(new string('y', 32768)));
        }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body", Cancellation);
        host.ExecuteClassic("body.normalize()", Cancellation);
        Assert.All(body.ChildNodes, node => Assert.Single(node.ChildNodes));
        foreach (var element in body.ChildNodes) { ((DomText)element.FirstChild!).SplitText(32768); }
        host.ExecuteClassic("""
            let failed=false;body.firstChild.firstChild.data;
            try{body.normalize()}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        Assert.All(body.ChildNodes, node => Assert.Equal(2, node.ChildNodes.Count));
    }

    [Fact]
    public void NormalizationDoesNotAllocateOrRecycleWrapperIdentitiesAndSharedCallsPrecedeMutation()
    {
        var document = Document(); var body = document.Body!;
        var a = document.CreateTextNode("a"); var b = document.CreateTextNode("b"); body.AppendChild(a); body.AppendChild(b);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let body=document.body,a=body.firstChild,b=a.nextSibling;
            for(let i=0;i<1021;++i)document.createTextNode('');
            body.normalize();
            """, Cancellation);
        Assert.Same(a, Assert.Single(body.ChildNodes));
        Assert.True(host.Evaluate("""
            (()=>{try{document.createTextNode('');return false}catch(e){return e instanceof TypeError&&b.data==='b'&&b.parentNode===null}})()
            """, Cancellation).Boolean);
        body.AppendChild(b);
        host.ExecuteClassicBatch([
            "let failed=false;for(let i=0;i<2048;++i)body.hasChildNodes();",
            "for(let i=0;i<2048;++i)body.hasChildNodes();",
            "try{body.normalize()}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        Assert.Equal(2, body.ChildNodes.Count); Assert.Equal("ab", a.Data);
    }

    [Fact]
    public void SurvivingAndDetachedNodeListenersRemainIndependentAfterNormalization()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        Assert.True(host.Evaluate("""
            (()=>{let p=document.body,a=document.createTextNode('a'),b=document.createTextNode('b'),
                hits=[];p.appendChild(a);p.appendChild(b);
                a.addEventListener('probe',()=>hits.push('a'));b.addEventListener('probe',()=>hits.push('b'));
                p.addEventListener('probe',()=>hits.push('parent'));p.normalize();
                a.dispatchEvent(new Event('probe',{bubbles:true}));b.dispatchEvent(new Event('probe',{bubbles:true}));
                return hits.join(',')==='a,parent,b'&&a.data==='ab'&&b.data==='b'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CapturedIntrinsicsAndCancellationPreserveTheBoundedNodeContract()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let p=document.body;p.appendChild(document.createTextNode('a'));p.appendChild(document.createTextNode('b'));
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            p.normalize();let ok=p.firstChild.data==='ab'&&p.firstChild===p.lastChild;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("while(true){try{p.normalize()}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
