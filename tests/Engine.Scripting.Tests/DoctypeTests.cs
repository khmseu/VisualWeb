using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class DoctypeTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static DomDocument Document(string name = "HTML", string publicId = "", string systemId = "")
    {
        var document = new DomDocument();
        document.AppendChild(document.CreateComment("before"));
        document.AppendChild(document.CreateDocumentType(name, publicId, systemId));
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void LookupSharesTreeIdentityAndMetadataPreservesRawUtf16WithoutElementCasing()
    {
        using var host = new V8ScriptHost(document: Document("MiXeD:\u00c9", "PUBLIC\0\ud800", "https://example.invalid/a\0\udfff"));
        Assert.True(host.Evaluate("""
            (()=>{let doctype=document.doctype;
                return doctype===document.firstChild.nextSibling&&doctype===document.doctype
                    &&doctype.name==='MiXeD:\u00c9'&&doctype.nodeName===doctype.name&&doctype.nodeType===10
                    &&doctype.publicId==='PUBLIC\0\ud800'&&doctype.systemId==='https://example.invalid/a\0\udfff'
                    &&doctype.ownerDocument===document&&doctype.parentNode===document
                    &&doctype.textContent===null&&doctype.nodeValue===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void AbsentIdentifiersAreEmptyAndLookupTracksRemovalAndReinsertion()
    {
        var document = Document(); var originalMode = document.Mode;
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let doctype=document.doctype;
                if(doctype.publicId!==''||doctype.systemId!=='')return false;
                doctype.remove();
                if(document.doctype!==null||doctype.name!=='HTML'||doctype.ownerDocument!==document||doctype.isConnected)return false;
                document.insertBefore(doctype,document.documentElement);
                return document.doctype===doctype&&doctype.isConnected&&doctype.parentNode===document})()
            """, Cancellation).Boolean);
        Assert.Equal(originalMode, document.Mode);
        using var emptyHost = new V8ScriptHost(document: new DomDocument());
        Assert.True(emptyHost.Evaluate("document.doctype===null", Cancellation).Boolean);
    }

    [Fact]
    public void ReadonlyGettersRejectStrictWritesAndForgedOrWrongInterfaceReceivers()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{'use strict';let doctype=document.doctype;
                for(let property of ['name','publicId','systemId']){
                    let descriptor=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(doctype),property);
                    if(!descriptor.enumerable||descriptor.set!==undefined||descriptor.configurable)return false;
                    try{doctype[property]='spoof';return false}catch(e){if(!(e instanceof TypeError))return false}
                    for(let fake of [{},Object.create(doctype),null,document,document.body,
                        document.createDocumentFragment(),document.createTextNode(''),document.createComment(''),
                        document.createProcessingInstruction('probe','')]){
                        try{descriptor.get.call(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                    }
                }
                let descriptor=Object.getOwnPropertyDescriptor(document,'doctype');
                if(!descriptor.enumerable||descriptor.set!==undefined||descriptor.configurable)return false;
                try{document.doctype=null;return false}catch(e){if(!(e instanceof TypeError))return false}
                for(let fake of [{},Object.create(document),null,doctype]){
                    try{descriptor.get.call(fake);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                }return doctype.name==='HTML'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void AdoptedDoctypeGettersRejectWithoutExposingForeignOwnership()
    {
        var document = Document(); var doctype = document.Doctype!;
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let doctype=document.doctype", Cancellation);
        new DomDocument().AdoptNode(doctype);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let property of ['name','publicId','systemId','ownerDocument']){
                try{doctype[property]}catch(e){if(e instanceof TypeError&&!e.GetType)rejected++}
            }return rejected===4&&document.doctype===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactIndividualAndSharedOutputLimitsApplyToEachFieldIndependently()
    {
        var document = Document(new string('D', 65536), new string('P', 65536), new string('S', 65536));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let doctype=document.doctype,failed=false", Cancellation);
        Assert.True(host.Evaluate("""
            doctype.name.length===65536&&doctype.publicId.length===65536&&doctype.systemId.length===65536
            """, Cancellation).Boolean);
        host.ExecuteClassic("""
            doctype.name;doctype.publicId;doctype.systemId;doctype.name;
            try{doctype.publicId}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        document.Doctype!.Remove();
        document.InsertBefore(document.CreateDocumentType(new string('D', 65537), new string('P', 65537), new string('S', 65537)),
            document.DocumentElement);
        Assert.True(host.Evaluate("""
            (()=>{let oversized=document.doctype,rejected=0;
                for(let property of ['name','publicId','systemId']){
                    try{oversized[property]}catch(e){if(e instanceof TypeError)rejected++}
                }return rejected===3&&oversized.ownerDocument===document})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OversizedOtherFieldsDoNotBlockLookupOrSmallMetadata()
    {
        using var host = new V8ScriptHost(document: Document("HTML", new string('P', 65537), "small"));
        Assert.True(host.Evaluate("""
            (()=>{let doctype=document.doctype;
                try{doctype.publicId;return false}catch(e){if(!(e instanceof TypeError))return false}
                return document.doctype===doctype&&doctype.name==='HTML'&&doctype.systemId==='small'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactDocumentChildScanBoundRejectsTheNextChildWithoutTraversingElementSubtrees()
    {
        var document = new DomDocument();
        for (var i = 0; i < 8191; i++) { document.AppendChild(document.CreateComment("")); }
        var doctype = document.CreateDocumentType("html"); document.AppendChild(doctype);
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("document.doctype===document.lastChild&&document.doctype.name==='html'", Cancellation).Boolean);
        var extra = document.CreateComment(""); document.AppendChild(extra);
        Assert.True(host.Evaluate("""
            (()=>{try{document.doctype;return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Same(document, doctype.ParentNode); Assert.Equal(8193, document.ChildNodes.Count);
        extra.Remove(); doctype.Remove();
        Assert.True(host.Evaluate("document.doctype===null", Cancellation).Boolean);
        foreach (var child in document.ChildNodes.ToArray()) { child.Remove(); }
        var html = document.CreateElement("html"); document.AppendChild(html); document.InsertBefore(doctype, html);
        for (var i = 0; i < 8193; i++) { html.AppendChild(document.CreateComment("")); }
        Assert.Equal("html", host.Evaluate("document.doctype.name", Cancellation).Text);
    }

    [Fact]
    public void RepeatedLookupAndFieldReadsAllocateNoNewIdentitiesAndCapacityFailureReservesNone()
    {
        var document = Document(); var original = document.Doctype!;
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let doctype=document.doctype;for(let i=0;i<1023;++i)document.createTextNode('')", Cancellation);
        Assert.True(host.Evaluate("""
            document.doctype===doctype&&doctype.name==='HTML'&&doctype.publicId===''&&doctype.systemId===''
            """, Cancellation).Boolean);
        original.Remove();
        Assert.True(host.Evaluate("document.doctype===null", Cancellation).Boolean);
        var replacement = document.CreateDocumentType("new"); document.InsertBefore(replacement, document.DocumentElement);
        Assert.True(host.Evaluate("""
            (()=>{try{document.doctype;return false}catch(e){return e instanceof TypeError&&doctype.name==='HTML'}})()
            """, Cancellation).Boolean);
        Assert.Same(replacement, document.Doctype);
        replacement.Remove(); document.InsertBefore(original, document.DocumentElement);
        Assert.True(host.Evaluate("document.doctype===doctype", Cancellation).Boolean);
    }

    [Fact]
    public void LookupOutputFailureRegistersNoNewIdentityAndDoesNotChangeTheNativeTree()
    {
        var document = Document("HTML", new string('P', 65536));
        var original = document.Doctype!;
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let doctype=document.doctype,failed=false", Cancellation);
        original.Remove();
        var replacement = document.CreateDocumentType("new"); document.InsertBefore(replacement, document.DocumentElement);
        host.ExecuteClassic("""
            for(let i=0;i<4;++i)doctype.publicId;
            try{document.doctype}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean); Assert.Same(replacement, document.Doctype);
        host.ExecuteClassic("let replacement=document.doctype;for(let i=0;i<1022;++i)document.createTextNode('')", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{document.createTextNode('');return false}catch(e){
                return e instanceof TypeError&&document.doctype===replacement&&replacement.name==='new'
            }})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void LookupAndAllFieldsConsumeTheSharedCallbackBudget()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let doctype=document.doctype,failed=false", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<512;++i){document.doctype;doctype.name;doctype.publicId;doctype.systemId}",
            "for(let i=0;i<512;++i){document.doctype;doctype.name;doctype.publicId;doctype.systemId}",
            "try{doctype.systemId}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        Assert.Equal("HTML", host.Evaluate("doctype.name", Cancellation).Text);
    }

    [Fact]
    public void CapturedBrandsAndCanceledTasksRetainThePrivateBridgeContract()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let doctype=document.doctype;
            WeakMap.prototype.get=()=>{throw 42};globalThis.TypeError=()=>{throw 43};
            let ok=document.doctype===doctype&&doctype.name==='HTML'&&doctype.publicId===''&&doctype.systemId==='';
            """, Cancellation);
        Assert.True(host.Evaluate("ok&&typeof __visualwebDom==='undefined'", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{doctype.name;document.doctype}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
