using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class CharacterDataTests
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
    public void DataNodeValueAndTextContentShareLiveUtf16StorageAndPreserveIdentity()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('a\u{1f600}b');document.body.appendChild(text);
                let ok=text.length===4&&text.data===text.nodeValue&&text.substringData(1,1)==='\ud83d';
                text.replaceData(1,2,'X');text.insertData(2,'Y');text.appendData('Z');text.deleteData(0,1);
                ok=ok&&text.data==='XYbZ'&&document.body.textContent==='XYbZ'
                    &&text===document.body.firstChild&&text.nodeValue===text.textContent;
                text.nodeValue=null;ok=ok&&text.data==='';
                text.data=null;ok=ok&&text.data==='';text.data=undefined;ok=ok&&text.data==='undefined';
                text.nodeValue=undefined;ok=ok&&text.data==='';
                text.appendData(null);ok=ok&&text.data==='null';
                text.data='a\0\ud800';return ok&&text.length===3&&text.data==='a\0\ud800'})()
            """, Cancellation).Boolean);
        Assert.Equal("a\0\ud800", document.Body!.TextContent);
    }

    [Fact]
    public void NonCharacterNodeValueIsNullAndSetterConvertsButDoesNotMutate()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let converted=false,body=document.body;body.textContent='kept';
                body.nodeValue={toString(){converted=true;return 'ignored'}};
                let fragment=document.createDocumentFragment();
                return converted&&body.nodeValue===null&&document.nodeValue===null&&fragment.nodeValue===null
                    &&body.textContent==='kept'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExistingCommentAndProcessingInstructionWrappersAreCharacterBranded()
    {
        var document = Document();
        document.Body!.AppendChild(document.CreateComment("comment"));
        document.Body.AppendChild(document.CreateProcessingInstruction("target", "pi"));
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let comment=document.body.firstChild,pi=comment.nextSibling;
                comment.replaceData(0,7,'edited');pi.appendData('!');
                return comment.nodeType===8&&comment.data==='edited'&&pi.data==='pi!'&&pi.length===3
                    &&document.body.textContent===''})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UnsignedLongConversionsClampWrapAndRejectBigIntAndSymbols()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('abcd');
                let ok=text.substringData(4294967296,2)==='ab'&&text.substringData(NaN,Infinity)===''
                    &&text.substringData(1.9,-1)==='bcd'&&text.substringData(4,4294967295)==='';
                for(let bad of [-1,5,4294967295]){
                    try{text.replaceData(bad,0,'bad');return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                for(let bad of [1n,Symbol()]){
                    try{text.substringData(bad,1);return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                return ok&&text.data==='abcd'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ConversionOrderAndSideEffectsPrecedeReadingCurrentData()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('initial'),order=[];
                let offset={valueOf(){order.push('offset');return 1}},
                    count={valueOf(){order.push('count');return 2}},
                    data={toString(){order.push('data');text.data='abcd';return 'X'}};
                text.replaceData(offset,count,data);
                let extra={toString(){throw Error('extra converted')}};
                text.appendData('!',extra);
                return order.join(',')==='offset,count,data'&&text.data==='aXd!'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ReceiverChecksPrecedeConversionAndRequiredArgumentsAreEnforced()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let text=document.createTextNode('x'),converted=false,bad={valueOf(){converted=true;return 0},toString(){converted=true;return 'x'}};
                for(let method of ['substringData','appendData','insertData','deleteData','replaceData']){
                    try{text[method].call({},bad,bad,bad);return false}catch(e){if(!(e instanceof TypeError))return false}
                    try{text[method]();return false}catch(e){if(!(e instanceof TypeError))return false}
                }
                let get=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(text),'data').get;
                for(let receiver of [{},document,document.createDocumentFragment()]){
                    try{get.call(receiver);return false}catch(e){if(!(e instanceof TypeError))return false}
                }try{text.appendData(Symbol());return false}catch(e){return !converted&&text.data==='x'}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactDataStorageAndResultLimitsRejectBeforeWritingAndPermitRecovery()
    {
        var document = Document();
        var text = document.CreateTextNode(new string('a', 65536));
        document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild;", Cancellation);
        Assert.True(host.Evaluate("text.length===65536&&text.substringData(0,65536).length===65536", Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{text.appendData('x');return false}catch(e){return e instanceof TypeError&&text.length===65536}})()
            """, Cancellation).Boolean);
        host.ExecuteClassic("text.replaceData(0,1,'x');", Cancellation);
        Assert.StartsWith("x", text.Data);
        text.Data = new string('b', 65537);
        Assert.True(host.Evaluate("""
            (()=>{let failed=false;try{text.substringData(0,65537)}catch(e){failed=e instanceof TypeError}
                text.deleteData(0,1);return failed&&text.length===65536})()
            """, Cancellation).Boolean);
        host.ExecuteClassic("text.data='recovered';", Cancellation);
        Assert.Equal("recovered", text.Data);
    }

    [Fact]
    public void OwnershipAndSharedCallbackLimitsApplyToAllDataPaths()
    {
        var document = Document();
        var text = document.CreateTextNode("kept"); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild,failed=false;", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<2048;++i)text.length;",
            "for(let i=0;i<2048;++i)text.deleteData(0,0);",
            "try{text.appendData('bad')}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed&&text.data==='kept'", Cancellation).Boolean);
        new DomDocument().AdoptNode(text);
        Assert.True(host.Evaluate("""
            (()=>{for(let action of [()=>text.data,()=>text.length,()=>text.nodeValue,()=>text.replaceData(0,0,'bad')]){
                try{action();return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
            }return true})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InFlightCancellationAndTamperedIntrinsicsCannotEscapeDataChecks()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let text=document.createTextNode('abc');
            WeakMap.prototype.get=()=>{throw Error('brand')};globalThis.TypeError=()=>{throw 42};
            text.replaceData(1,1,'X');let ok=text.data==='aXc';
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{text.deleteData(0,0)}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void AggregateInputAndGetterTextBudgetsRejectWithoutPartialEdit()
    {
        var document = Document();
        var text = document.CreateTextNode(new string('a', 65536)); document.Body!.AppendChild(text);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let text=document.body.firstChild,failed=false;", Cancellation);
        host.ExecuteClassic("""
            for(let i=0;i<4;++i)text.data;
            try{text.replaceData(0,1,'b')}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.Equal('a', text.Data[0]);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        host.ExecuteClassic("""
            failed=false;let value='b'.repeat(65536);
            for(let i=0;i<4;++i)text.data=value;
            try{text.data='bad'}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.Equal(new string('b', 65536), text.Data);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
    }
}
