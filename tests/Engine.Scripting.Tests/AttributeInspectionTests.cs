using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class AttributeInspectionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument(); var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void NamesAreFreshMutableOrderedArraysWithLiveReplacementRemovalAndReaddition()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let element=document.createElement('i'),empty=element.getAttributeNames();
                if(element.hasAttributes()||!Array.isArray(empty)||empty.length!==0)return false;
                element.setAttribute('B','first');element.id='second';element.className='raw';
                let snapshot=element.getAttributeNames();element.setAttribute('b','replacement');
                if(snapshot.join(',')!=='b,id,class'||element.getAttributeNames().join(',')!=='b,id,class')return false;
                element.removeAttribute('id');element.id='again';
                if(element.getAttributeNames().join(',')!=='b,class,id')return false;
                snapshot[0]='changed';snapshot.push('local');snapshot.splice(1,1);
                let next=element.getAttributeNames();
                return element.hasAttributes()&&next!==snapshot&&next.join(',')==='b,class,id'
                    &&element.getAttribute('b')==='replacement'&&empty.length===0})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void LengthPrefixedNamesPreserveSeparatorsQuotesAndSurrogates()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let element=document.body,names=['x:y','comma,name','quote"name','back\\name','\ud800','\u00c9'];
                for(let name of names)element.setAttribute(name,'ignored');
                let snapshot=element.getAttributeNames();
                return snapshot.length===names.length&&names.every((name,index)=>snapshot[index]===name)})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void IgnoredArgumentsAndBorrowedMethodsRespectBrandsAndAdoptedOwnership()
    {
        var document = Document(); var element = document.CreateElement("i"); document.Body!.AppendChild(element);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element=document.body.firstChild", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{let argument={toString(){throw 42},valueOf(){throw 43}};
                if(element.hasAttributes(argument)||element.getAttributeNames(argument).length!==0)return false;
                for(let receiver of [{},Object.create(element),null,document,document.createTextNode(''),document.createDocumentFragment()]){
                    for(let method of [element.hasAttributes,element.getAttributeNames]){
                        try{method.call(receiver);return false}catch(e){if(!(e instanceof TypeError)||e.GetType)return false}
                    }
                }return true})()
            """, Cancellation).Boolean);
        new DomDocument().AdoptNode(element);
        Assert.True(host.Evaluate("""
            (()=>{let rejected=0;for(let method of [element.hasAttributes,element.getAttributeNames]){
                try{method.call(element)}catch(e){if(e instanceof TypeError&&!e.GetType)rejected++}
            }return rejected===2})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CountAndEncodedResultLimitsAreExactAndPresenceRemainsConstantTime()
    {
        var document = Document(); var body = document.Body!;
        for (var i = 0; i < 128; i++) { body.SetAttribute("n" + i, ""); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body", Cancellation);
        Assert.Equal(128, host.Evaluate("body.getAttributeNames().length", Cancellation).Number);
        body.SetAttribute("extra", "");
        Assert.True(host.Evaluate("""
            (()=>{try{body.getAttributeNames();return false}catch(e){return e instanceof TypeError&&body.hasAttributes()}})()
            """, Cancellation).Boolean);
        foreach (var name in body.Attributes.Keys.ToArray()) { body.RemoveAttribute(name); }
        var exact = new string('a', 65530); body.SetAttribute(exact, "");
        Assert.Equal(65530, host.Evaluate("body.getAttributeNames()[0].length", Cancellation).Number);
        body.RemoveAttribute(exact); body.SetAttribute(new string('a', 65531), "");
        Assert.True(host.Evaluate("""
            (()=>{try{body.getAttributeNames();return false}catch(e){return e instanceof TypeError&&body.hasAttributes()}})()
            """, Cancellation).Boolean);
        Assert.Single(body.Attributes);
    }

    [Fact]
    public void NamesDoNotReadOversizedAttributeValuesOrConsumeWrapperCapacity()
    {
        var document = Document(); document.Body!.SetAttribute("native", new string('x', 65537));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let body=document.body;for(let i=0;i<1023;++i)document.createTextNode('');
            """, Cancellation);
        Assert.True(host.Evaluate("body.hasAttributes()&&body.getAttributeNames().join(',')==='native'", Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{document.createTextNode('');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SharedTextAndCallbackExhaustionRejectWithoutMutatingAttributes()
    {
        var document = Document(); var name = new string('a', 65530); document.Body!.SetAttribute(name, "kept");
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body,failed=false", Cancellation);
        host.ExecuteClassic("""
            for(let i=0;i<4;++i)body.getAttributeNames();
            try{body.getAttributeNames()}catch(e){failed=e instanceof TypeError}
            """, Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        host.ExecuteClassicBatch([
            "failed=false;for(let i=0;i<2048;++i)body.hasAttributes()",
            "for(let i=0;i<2048;++i)body.hasAttributes()",
            "try{body.getAttributeNames()}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed", Cancellation).Boolean);
        Assert.Equal("kept", document.Body.GetAttribute(name));
    }

    [Fact]
    public void SnapshotConstructionUsesCapturedIntrinsicsAndAvoidsInheritedIndexSetters()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let element=document.createElement('i');element.setAttribute('a:b','');element.id='value';
            let setterCalls=0;
            Object.defineProperty(Array.prototype,'0',{configurable:true,set(){setterCalls++}});
            Array.prototype.push=()=>{throw 41};String.prototype.slice=()=>{throw 42};
            String.prototype.indexOf=()=>{throw 43};WeakMap.prototype.get=()=>{throw 44};
            globalThis.Array=()=>{throw 45};Object.defineProperty=()=>{throw 46};
            let names=element.getAttributeNames(),ok=names.length===2&&names[0]==='a:b'&&names[1]==='id'&&setterCalls===0;
            delete Object.getPrototypeOf(names)[0];
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void InFlightCancellationInvalidatesInspectionHost()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let element=document.body;element.id='kept'", Cancellation);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{element.getAttributeNames()}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
