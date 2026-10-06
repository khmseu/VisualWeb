using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class MetadataTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void HtmlNamesUseAsciiCasingAndExposeNullPrefixesEvenForColonLocalNames()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let element=document.createElement('MiXeD:\u00c9\u0131');
                return element.localName==='mixed:\u00c9\u0131'&&element.tagName==='MIXED:\u00c9\u0131'
                    &&element.nodeName===element.tagName&&element.namespaceURI==='http://www.w3.org/1999/xhtml'
                    &&element.prefix===null&&element.ownerDocument===document
                    &&document.documentElement.localName==='html'&&document.body.tagName==='BODY'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void EverySupportedNodeNameAndOwnerUsesTheSameDocumentFacade()
    {
        var document = Document();
        document.InsertBefore(document.CreateDocumentType("HTML"), document.DocumentElement);
        document.Body!.AppendChild(document.CreateComment("data"));
        document.Body.AppendChild(document.CreateProcessingInstruction("Probe", "data"));
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let comment=document.body.firstChild,pi=comment.nextSibling,doctype=document.firstChild,
                text=document.createTextNode('data'),fragment=document.createDocumentFragment();
                return document.nodeName==='#document'&&document.ownerDocument===null
                    &&doctype.nodeName==='HTML'&&comment.nodeName==='#comment'&&pi.nodeName==='Probe'
                    &&text.nodeName==='#text'&&fragment.nodeName==='#document-fragment'
                    &&[doctype,comment,pi,text,fragment,document.body].every(n=>n.ownerDocument===document)
                    &&text.localName===undefined&&comment.tagName===undefined&&fragment.namespaceURI===undefined})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void RemovalNormalizationAndFragmentMovesDoNotChangeOwnershipOrNames()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let fragment=document.createDocumentFragment(),element=document.createElement('i'),
                a=document.createTextNode('a'),b=document.createTextNode('b');
                element.appendChild(a);element.appendChild(b);fragment.appendChild(element);
                document.body.appendChild(fragment);element.normalize();document.body.removeChild(element);
                return fragment.ownerDocument===document&&element.ownerDocument===document
                    &&a.ownerDocument===document&&b.ownerDocument===document&&b.parentNode===null
                    &&element.nodeName==='I'&&a.nodeName==='#text'&&b.nodeName==='#text'&&!element.isConnected})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ReadonlyGettersRejectWritesAndForgedOrNonElementReceivers()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{'use strict';let element=document.body,text=document.createTextNode('x');
                for(let property of ['nodeName','ownerDocument','localName','tagName','namespaceURI','prefix']){
                    let descriptor=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(element),property);
                    if(!descriptor.enumerable||descriptor.set!==undefined||descriptor.configurable)return false;
                    try{element[property]='spoof';return false}catch(e){if(!(e instanceof TypeError))return false}
                    for(let fake of [{},Object.create(element),null]){
                        try{descriptor.get.call(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                    }
                    if(property!=='nodeName'&&property!=='ownerDocument'){
                        for(let fake of [text,document,document.createDocumentFragment()]){
                            try{descriptor.get.call(fake);return false}catch(e){if(!(e instanceof TypeError))return false}
                        }
                    }
                }return element.nodeName==='BODY'&&element.ownerDocument===document})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void NativeAdoptionInvalidatesAllMetadataInsteadOfLeakingAnotherDocument()
    {
        var document = Document(); var element = document.CreateElement("i"); document.Body!.AppendChild(element);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element=document.body.firstChild", Cancellation);
        new DomDocument().AdoptNode(element);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let property of ['nodeName','ownerDocument','localName','tagName','namespaceURI','prefix']){
                try{element[property]}catch(e){if(e instanceof TypeError&&!e.GetType)rejected++}
            }return rejected===6})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void NameLengthAndAggregateOutputLimitsAreExactForNativeElements()
    {
        var document = Document(); var element = document.CreateElement(new string('a', 65536)); document.Body!.AppendChild(element);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element=document.body.firstChild,failed=false", Cancellation);
        Assert.Equal(65536, host.Evaluate("element.nodeName.length", Cancellation).Number);
        Assert.Equal(65536, host.Evaluate("element.tagName.length", Cancellation).Number);
        Assert.Equal(65536, host.Evaluate("element.localName.length", Cancellation).Number);
        host.ExecuteClassic("""
            element.nodeName;element.tagName;element.localName;element.nodeName;
            try{element.localName}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        document.Body.RemoveChild(element);
        document.Body.AppendChild(document.CreateElement(new string('a', 65537)));
        Assert.True(host.Evaluate("""
            (()=>{let oversized=document.body.firstChild,rejected=0;
                for(let property of ['nodeName','tagName','localName']){
                    try{oversized[property]}catch(e){if(e instanceof TypeError)rejected++}
                }return rejected===3&&oversized.ownerDocument===document&&oversized.prefix===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void NativeInstructionAndDoctypeNamesUseOutputBudgetsNotCaseConversion()
    {
        var document = Document(); var instruction = document.CreateProcessingInstruction(new string('P', 65536), "");
        document.Body!.AppendChild(instruction);
        document.InsertBefore(document.CreateDocumentType(new string('D', 65536)), document.DocumentElement);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let instruction=document.body.firstChild,doctype=document.firstChild", Cancellation);
        Assert.True(host.Evaluate("instruction.nodeName.length===65536&&doctype.nodeName.length===65536", Cancellation).Boolean);
        document.Body.RemoveChild(instruction);
        document.Body.AppendChild(document.CreateProcessingInstruction(new string('P', 65537), ""));
        document.RemoveChild(document.FirstChild!);
        document.InsertBefore(document.CreateDocumentType(new string('D', 65537)), document.DocumentElement);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let node of [document.body.firstChild,document.firstChild]){
                try{node.nodeName}catch(e){if(e instanceof TypeError)rejected++}
            }return rejected===2})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void FullIdentityCapacityAllowsOwnerDocumentAndMetadataWithoutNewHandles()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let element=document.createElement('i');
            for(let i=0;i<1023;++i)document.createTextNode('');
            """, Cancellation);
        Assert.True(host.Evaluate("""
            element.ownerDocument===document&&element.nodeName==='I'&&element.localName==='i'
                &&element.prefix===null&&document.ownerDocument===null
            """, Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{document.createTextNode('');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void MetadataSharesCallbackBudgetAndPreservesCapturedBrandsUnderCancellation()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let element=document.createElement('i'),failed=false", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<2048;++i)element.ownerDocument",
            "for(let i=0;i<2048;++i)element.prefix",
            "try{element.nodeName}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        host.ExecuteClassic("""
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            let ok=element.tagName==='I'&&element.ownerDocument===document;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{element.ownerDocument}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
