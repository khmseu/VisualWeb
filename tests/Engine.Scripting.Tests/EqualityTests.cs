using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class EqualityTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void StructuralEqualityIsLiveAndIndependentOfIdentityConnectionAndAttributeOrder()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let a=document.createElement('div'),b=document.createElement('DIV');
                a.setAttribute('id','same');a.setAttribute('class','raw');
                b.setAttribute('class','raw');b.setAttribute('id','same');document.body.appendChild(a);
                let ok=a!==b&&!a.isSameNode(b)&&a.isEqualNode(b)&&b.isEqualNode(a)&&a.isEqualNode(a);
                b.id='different';ok=ok&&!a.isEqualNode(b);b.id='same';
                b.className='Raw';ok=ok&&!a.isEqualNode(b);b.className='raw';
                b.removeAttribute('id');return ok&&!a.isEqualNode(b)})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OrderedChildrenAndTextSegmentationRemainSignificantUntilNormalization()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let a=document.createDocumentFragment(),b=document.createDocumentFragment();
                a.appendChild(document.createTextNode('a\0\ud800b'));
                b.appendChild(document.createTextNode('a\0'));b.appendChild(document.createTextNode('\ud800b'));
                let ok=a.textContent===b.textContent&&!a.isEqualNode(b);
                b.normalize();ok=ok&&a.isEqualNode(b);b.firstChild.data='a\0\ud801b';
                ok=ok&&!a.isEqualNode(b);b.firstChild.data='a\0\ud800b';
                a.appendChild(document.createElement('i'));b.insertBefore(document.createElement('i'),b.firstChild);
                return ok&&!a.isEqualNode(b)&&!a.isEqualNode(document.createElement('div'))})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SupportedNativeLeafInterfacesExposeExactPayloadEquality()
    {
        var document = Document(); var body = document.Body!;
        foreach (var node in new DomNode[] {
            document.CreateTextNode("data"), document.CreateComment("data"), document.CreateComment("data"),
            document.CreateProcessingInstruction("probe", "data"), document.CreateProcessingInstruction("probe", "data"),
            document.CreateProcessingInstruction("other", "data") }) { body.AppendChild(node); }
        document.InsertBefore(document.CreateDocumentType("html", "public", "system"), document.DocumentElement);
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let text=document.body.firstChild,a=text.nextSibling,b=a.nextSibling,pi=b.nextSibling,
                equal=pi.nextSibling,other=equal.nextSibling,doctype=document.firstChild;
                let ok=!text.isEqualNode(a)&&a.isEqualNode(b)&&pi.isEqualNode(equal)&&!pi.isEqualNode(other)
                    &&doctype.isEqualNode(doctype)&&!doctype.isEqualNode(text)&&document.isEqualNode(document);
                equal.data='changed';return ok&&!pi.isEqualNode(equal)})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OptionalNullUndefinedAndExtraArgumentsDoNotTriggerConversionAndBrandsRejectFakes()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let node=document.createTextNode('a'),convert={toString(){throw 42},valueOf(){throw 43}};
                let ok=!node.isEqualNode()&&!node.isEqualNode(null)&&!node.isEqualNode(undefined)
                    &&node.isEqualNode(node,convert);
                for(let fake of [{},Object.create(node),convert,1,'node',Symbol()]){
                    try{node.isEqualNode(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                    try{node.isEqualNode.call(fake,node);return false}catch(e){if(!(e instanceof TypeError))return false}
                }return ok})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void BothAdoptedOperandsAreRejectedEvenForNullSelfOrEarlyMismatch()
    {
        var document = Document(); var text = document.CreateTextNode("a"); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild,other=document.createElement('i')", Cancellation);
        new DomDocument().AdoptNode(text);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let action of [
                ()=>text.isEqualNode(null),()=>text.isEqualNode(text),()=>other.isEqualNode(text),()=>text.isEqualNode(other)]){
                try{action()}catch(e){if(e instanceof TypeError&&!e.GetType)rejected++}
            }return rejected===4})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactTreeScanCapacityIncludesRootsAndCannotBeBypassedByIdentityOrMismatch()
    {
        var document = Document(); var body = document.Body!;
        for (var i = 0; i < 8191; i++) { body.AppendChild(document.CreateTextNode("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body,other=document.createTextNode('')", Cancellation);
        Assert.True(host.Evaluate("body.isEqualNode(body)", Cancellation).Boolean);
        body.AppendChild(document.CreateTextNode(""));
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let action of [
                ()=>body.isEqualNode(body),()=>body.isEqualNode(other),()=>other.isEqualNode(body)]){
                try{action()}catch(e){if(e instanceof TypeError)rejected++}
            }return rejected===3})()
            """, Cancellation).Boolean);
        Assert.Equal(8192, body.ChildNodes.Count);
    }

    [Fact]
    public void ExactCharacterAndSharedTaskBudgetsChargeBothOperandsAndBooleanResults()
    {
        var document = Document(); var text = document.CreateTextNode(new string('x', 65536)); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild,failed=false", Cancellation);
        Assert.True(host.Evaluate("text.isEqualNode(text)", Cancellation).Boolean);
        host.ExecuteClassic("""
            text.isEqualNode(text);
            try{text.isEqualNode(text)}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        text.Data += "x";
        Assert.True(host.Evaluate("""
            (()=>{try{text.isEqualNode(text);return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal(65537, text.Length);
    }

    [Fact]
    public void ExactAggregateCapacityIncludesBooleanOutputAndDoesNotExposeLargeNativePayloads()
    {
        var document = Document(); var body = document.Body!;
        var first = document.CreateTextNode(new string('a', 65535)); var second = document.CreateTextNode(new string('b', 65535));
        var fragment = document.CreateDocumentFragment(); fragment.AppendChild(first); fragment.AppendChild(second);
        body.AppendChild(fragment);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let fragment=document.createDocumentFragment(),body=document.body;
            fragment.appendChild(body.firstChild);fragment.appendChild(body.firstChild);
            """, Cancellation);
        Assert.True(host.Evaluate("fragment.isEqualNode(fragment)", Cancellation).Boolean);
        first.Data += "x";
        Assert.True(host.Evaluate("""
            (()=>{try{fragment.isEqualNode(fragment);return false}catch(e){return e instanceof TypeError&&!e.GetType}})()
            """, Cancellation).Boolean);
        Assert.Equal(65536, first.Length); Assert.Equal(65535, second.Length);
    }

    [Fact]
    public void NativeAttributeCountAndStorageBoundsAreExactForBothOperands()
    {
        var document = Document(); var body = document.Body!;
        var a = document.CreateElement("a"); var b = document.CreateElement("a"); body.AppendChild(a); body.AppendChild(b);
        for (var i = 0; i < 128; i++) { a.SetAttribute("n" + i, ""); b.SetAttribute("n" + i, ""); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let a=document.body.firstChild,b=a.nextSibling", Cancellation);
        Assert.True(host.Evaluate("a.isEqualNode(b)", Cancellation).Boolean);
        b.SetAttribute("extra", "");
        Assert.True(host.Evaluate("""
            (()=>{try{a.isEqualNode(b);return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        foreach (var element in new[] { a, b })
        {
            foreach (var name in element.Attributes.Keys.ToArray()) { element.RemoveAttribute(name); }
            element.SetAttribute("a", new string('x', 65535));
        }
        Assert.True(host.Evaluate("a.isEqualNode(b)", Cancellation).Boolean);
        b.SetAttribute("a", new string('x', 65536));
        Assert.True(host.Evaluate("""
            (()=>{try{a.isEqualNode(b);return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal(65536, b.GetAttribute("a")!.Length);
    }

    [Fact]
    public void EqualityAllocatesNoWrappersAtFullIdentityCapacityAndSharesCallbackLimits()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let a=document.createElement('i'),b=document.createElement('i');
            for(let i=0;i<1022;++i)document.createTextNode('');
            """, Cancellation);
        Assert.True(host.Evaluate("a.isEqualNode(b)", Cancellation).Boolean);
        host.ExecuteClassicBatch([
            "let failed=false;for(let i=0;i<2048;++i)a.isEqualNode(null)",
            "for(let i=0;i<2048;++i)a.isEqualNode(null)",
            "try{a.isEqualNode(b)}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
    }

    [Fact]
    public void ListenersDoNotAffectEqualityAndIntrinsicTamperingCannotBypassBrandsOrCancellation()
    {
        using var host = new V8ScriptHost(document: Document(), enableEvents: true);
        host.ExecuteClassic("""
            let a=document.createTextNode('a'),b=document.createTextNode('a'),hits=0;
            a.addEventListener('probe',()=>hits++);
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            let equal=a.isEqualNode(b);a.dispatchEvent(new Event('probe'));b.dispatchEvent(new Event('probe'));
            """, Cancellation);
        Assert.True(host.Evaluate("equal&&hits===1", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("while(true){try{a.isEqualNode(b)}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
