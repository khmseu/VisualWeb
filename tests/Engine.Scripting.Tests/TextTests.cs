using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class TextTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void SplitUsesUtf16AndPreservesOriginalAndReturnedIdentitiesAndSiblingLinks()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,text=document.createTextNode('a\u{1f600}b'),after=document.createElement('i');
                body.appendChild(text);body.appendChild(after);let tail=text.splitText(2);
                return text.data==='a\ud83d'&&tail.data==='\ude00b'&&text!==tail&&body.firstChild===text
                    &&text.nextSibling===tail&&tail.previousSibling===text&&tail.nextSibling===after
                    &&tail.parentNode===body&&tail.getRootNode()===document&&tail.wholeText==='a\u{1f600}b'
                    &&body.textContent==='a\u{1f600}b'&&tail.length===2})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void WholeTextIsLiveIncludesEmptyNodesAndStopsAtNonTextSiblings()
    {
        var document = Document();
        document.Body!.AppendChild(document.CreateTextNode("left"));
        document.Body.AppendChild(document.CreateComment("stop"));
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,a=document.createTextNode('a'),empty=document.createTextNode(''),b=document.createTextNode('b');
                body.appendChild(a);body.appendChild(empty);body.appendChild(b);body.appendChild(document.createElement('stop'));
                let ok=empty.wholeText==='ab'&&body.firstChild.wholeText==='left';
                b.data='B';ok=ok&&a.wholeText==='aB';body.removeChild(a);
                return ok&&empty.wholeText==='B'&&a.wholeText==='a'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void DetachedAndFragmentSplitsAndUnsignedOffsetsFollowCurrentData()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('abc'),tail=text.splitText(4294967296);
                let ok=text.data===''&&tail.data==='abc'&&tail.parentNode===null&&text.wholeText==='';
                let fragment=document.createDocumentFragment();fragment.appendChild(tail);
                let accessed=false,offset={valueOf(){accessed=true;tail.data='changed';return 3}};
                let ending=tail.splitText(offset);
                let empty=ending.splitText(ending.length);
                return ok&&accessed&&tail.data==='cha'&&ending.data==='nged'&&empty.data===''
                    &&ending.parentNode===fragment&&tail.wholeText==='changed'&&empty.wholeText==='changed'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InvalidOffsetsArgumentsAndReceiversRejectBeforeAnyTreeMutation()
    {
        var document = Document();
        document.Body!.AppendChild(document.CreateComment("comment"));
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('abc');document.body.appendChild(text);
                for(let argument of [-1,4,1n,Symbol()]){
                    try{text.splitText(argument);return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                try{text.splitText();return false}catch(e){if(!(e instanceof TypeError))return false}
                let converted=false,argument={valueOf(){converted=true;return 1}};
                for(let receiver of [{},document.body.firstChild,document.createDocumentFragment()]){
                    try{text.splitText.call(receiver,argument);return false}catch(e){if(!(e instanceof TypeError))return false}
                    let get=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(text),'wholeText').get;
                    try{get.call(receiver);return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                return !converted&&text.data==='abc'&&text.nextSibling===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void WholeTextResultAndTextSplitStorageBoundsAreExact()
    {
        var document = Document(); var body = document.Body!;
        var a = document.CreateTextNode(new string('a', 32768));
        var b = document.CreateTextNode(new string('b', 32768)); body.AppendChild(a); body.AppendChild(b);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild;", Cancellation);
        Assert.Equal(65536, host.Evaluate("text.wholeText.length", Cancellation).Number);
        b.Data += "x";
        Assert.True(host.Evaluate("""
            (()=>{try{text.wholeText;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        body.RemoveChild(b);
        a.Data = new string('x', 65536);
        host.ExecuteClassic("let tail=text.splitText(32768);", Cancellation);
        Assert.Equal(32768, a.Length);
        Assert.Equal(65536, host.Evaluate("tail.wholeText.length", Cancellation).Number);
        body.RemoveChild(a.NextSibling!);
        a.Data = new string('x', 65537);
        Assert.True(host.Evaluate("""
            (()=>{try{text.splitText(1);return false}catch(e){return e instanceof TypeError&&text.length===65537&&text.nextSibling===null}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void WholeTextSiblingAndSplitDestinationBoundsAreExactAndAtomic()
    {
        var document = Document(); var body = document.Body!;
        var text = document.CreateTextNode("kept"); body.AppendChild(text);
        for (var i = 1; i < 8191; i++) { body.AppendChild(document.CreateTextNode("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild;", Cancellation);
        host.ExecuteClassic("let tail=text.splitText(2);", Cancellation);
        Assert.Equal(8192, body.ChildNodes.Count);
        Assert.Equal("kept", host.Evaluate("text.wholeText", Cancellation).Text);
        Assert.True(host.Evaluate("""
            (()=>{try{text.splitText(1);return false}catch(e){return e instanceof TypeError&&text.data==='ke'&&text.nextSibling===tail}})()
            """, Cancellation).Boolean);
        body.AppendChild(document.CreateComment("extra"));
        Assert.True(host.Evaluate("""
            (()=>{try{text.wholeText;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SplitIdentityCapacityFailsBeforeMutation()
    {
        var document = Document(); var text = document.CreateTextNode("kept");
        document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let text=document.body.firstChild;
            for(let i=0;i<1022;++i)document.createTextNode('');
            """, Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{text.splitText(2);return false}catch(e){return e instanceof TypeError&&text.data==='kept'&&text.nextSibling===null}})()
            """, Cancellation).Boolean);
        Assert.Single(document.Body.ChildNodes);
        Assert.Equal("kept", text.Data);
    }

    [Fact]
    public void DetachedSplitCanReserveTheExactLastIdentity()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let text=document.createTextNode('ab');
            for(let i=0;i<1022;++i)document.createTextNode('');
            let tail=text.splitText(1);
            """, Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{if(text.data!=='a'||tail.data!=='b')return false;
                try{tail.splitText(0);return false}catch(e){return e instanceof TypeError&&tail.data==='b'}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void AggregateTextAndCallBudgetFailuresDoNotSplitOrRewriteData()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let text=document.createTextNode('a'.repeat(65536)),failed=false;", Cancellation);
        host.ExecuteClassic("""
            for(let i=0;i<4;++i)text.wholeText;
            try{text.splitText(1)}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed&&text.length===65536&&text.parentNode===null", Cancellation).Boolean);
        host.ExecuteClassicBatch([
            "failed=false;for(let i=0;i<2048;++i)text.length;",
            "for(let i=0;i<2048;++i)text.length;",
            "try{text.splitText(1)}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed&&text.length===65536", Cancellation).Boolean);
    }

    [Fact]
    public void OriginalListenersSurviveSplitAndOwnershipBrandsRejectAdoption()
    {
        var document = Document(); var text = document.CreateTextNode("kept"); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document, enableEvents: true);
        host.ExecuteClassic("""
            let text=document.body.firstChild,hits=0;text.addEventListener('probe',()=>hits++);
            let tail=text.splitText(2);text.dispatchEvent(new Event('probe'));tail.dispatchEvent(new Event('probe'));
            """, Cancellation);
        Assert.Equal(1, host.Evaluate("hits", Cancellation).Number);
        new DomDocument().AdoptNode(text);
        host.ExecuteClassic("""
            let rejected=0;
            for(let action of [()=>text.splitText(0),()=>text.wholeText]){
                try{action()}catch(e){if(e instanceof TypeError&&!e.GetType)rejected++}
            }
            """, Cancellation);
        Assert.Equal(2, host.Evaluate("rejected", Cancellation).Number);
    }

    [Fact]
    public void IntrinsicTamperingAndInFlightCancellationCannotBypassTextBrands()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let text=document.createTextNode('abc');
            WeakMap.prototype.get=()=>{throw Error('brand')};globalThis.TypeError=()=>{throw 42};
            let tail=text.splitText(1),ok=text.data==='a'&&tail.data==='bc'&&tail.wholeText==='bc';
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{text.wholeText}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
