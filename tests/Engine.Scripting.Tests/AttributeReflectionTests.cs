using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class AttributeReflectionTests
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
    public void IdReflectsLiveRawAttributeAndFeedsLookupSelectorsAndDetachedNodes()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body;
                let ok=body.id===''&&!body.hasAttribute('id');
                body.id='live';
                ok=ok&&body.getAttribute('id')==='live'&&document.getElementById('live')===body
                    &&document.querySelector('#live')===body&&body.matches('#live');
                body.setAttribute('ID','external');ok=ok&&body.id==='external';
                body.removeAttribute('id');ok=ok&&body.id==='';
                body.id=null;ok=ok&&body.id==='null';
                body.id=undefined;ok=ok&&body.id==='undefined';
                body.id=' raw \0id ';ok=ok&&body.getAttribute('id')===' raw \0id ';
                body.parentNode.removeChild(body);body.id='detached';
                return ok&&body.id==='detached'&&document.getElementById('detached')===null})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void TogglePreservesValuesNormalizesNamesAndUsesOptionalBooleanForce()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body;
                let ok=!body.toggleAttribute('DATA-X',false)&&!body.hasAttribute('data-x')
                    &&body.toggleAttribute('DATA-X')&&body.getAttribute('data-x')==='';
                body.setAttribute('data-x','kept');
                ok=ok&&body.toggleAttribute('Data-X',{})&&body.getAttribute('data-x')==='kept'
                    &&!body.toggleAttribute('DATA-X',undefined)&&body.toggleAttribute('DATA-X',1)
                    &&!body.toggleAttribute('DATA-X',null)&&body.toggleAttribute('DATA-X',true)
                    &&!body.toggleAttribute('DATA-X',0);
                let force={valueOf(){throw Error('force coerced')}};
                return ok&&body.toggleAttribute('data-x',force)})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ToggleReadsCurrentAttributeAfterNameConversionAndIgnoresExtraArguments()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,converted=false;
                let name={toString(){converted=true;body.setAttribute('data-x','converted');return 'DATA-X'}};
                let ignored={toString(){throw Error('extra converted')}};
                return !body.toggleAttribute(name,undefined,ignored)&&converted&&!body.hasAttribute('data-x')})()
            """, Cancellation).Boolean);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad name")]
    [InlineData("bad\0name")]
    [InlineData("bad/name")]
    [InlineData("bad=name")]
    [InlineData("bad>name")]
    public void InvalidToggleNamesRejectForcedNoOpsAndDoNotMutate(string name)
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let name=" + System.Text.Json.JsonSerializer.Serialize(name) + ";document.body.id='kept';", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{try{document.body.toggleAttribute(name,false);return false}
                catch(e){return e instanceof TypeError&&typeof e.GetType==='undefined'&&document.body.id==='kept'}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void BrandsPrecedeConversionAndSymbolsAndMissingArgumentsAreRejected()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,converted=false,name={toString(){converted=true;return 'x'}};
                let descriptor=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(body),'id');
                for(let action of [()=>body.toggleAttribute.call({},name),()=>descriptor.set.call({},name),
                    ()=>descriptor.get.call({}),()=>body.toggleAttribute(),()=>body.toggleAttribute(Symbol()),
                    ()=>{body.id=Symbol()},()=>body.toggleAttribute.call(document,name)]){
                    try{action();return false}catch(e){if(!(e instanceof TypeError))return false}
                }return !converted&&!body.hasAttribute('id')})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ExactAttributeCapacityAllowsNoOpsAndRemovalButRejectsAdditionAtomically()
    {
        var document = Document();
        for (var i = 0; i < 128; i++) { document.Body!.SetAttribute("a" + i, "value"); }
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,failed=false;
                try{body.toggleAttribute('new')}catch(e){failed=e instanceof TypeError}
                let ok=failed&&!body.hasAttribute('new')&&!body.toggleAttribute('missing',false)
                    &&body.toggleAttribute('a0',true)&&body.getAttribute('a0')==='value'
                    &&!body.toggleAttribute('a0',false)&&body.toggleAttribute('new');
                return ok&&body.getAttribute('new')===''})()
            """, Cancellation).Boolean);
        Assert.Equal(128, document.Body!.Attributes.Count);
    }

    [Fact]
    public void ExactStorageBudgetPreflightsIdAndEmptyAttributeNameBeforeWriting()
    {
        var document = Document();
        document.Body!.SetAttribute("fill", new string('x', 65527));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body;body.id='abc';", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{let id=false,toggle=false;
                try{body.id='abcd'}catch(e){id=e instanceof TypeError}
                try{body.toggleAttribute('x')}catch(e){toggle=e instanceof TypeError}
                return id&&toggle&&body.id==='abc'&&!body.hasAttribute('x')})()
            """, Cancellation).Boolean);
        Assert.Equal("abc", document.Body.GetAttribute("id"));
    }

    [Fact]
    public void SharedCallbacksResetAndAdoptedWrappersRejectAllReflectionPaths()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let body=document.body,failed=false;", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<2048;++i)body.toggleAttribute('x',false);",
            "for(let i=0;i<2048;++i)body.id;",
            "try{body.id='late'}catch(e){failed=e instanceof TypeError}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed&&body.id===''", Cancellation).Boolean);
        new DomDocument().AdoptNode(document.Body!);
        Assert.True(host.Evaluate("""
            (()=>{for(let action of [()=>body.id,()=>{body.id='bad'},()=>body.toggleAttribute('x',false)]){
                try{action();return false}catch(e){if(!(e instanceof TypeError))return false}
            }return true})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void ReflectionUsesCapturedBrandsDespiteIntrinsicTampering()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let body=document.body;
            WeakMap.prototype.get=WeakMap.prototype.set=()=>{throw Error('brand')};
            globalThis.String=globalThis.TypeError=()=>{throw 42};
            body.id='safe';let ok=body.toggleAttribute('DATA-SAFE')&&body.hasAttribute('data-safe')&&body.id==='safe';
            try{body.toggleAttribute('bad name',false);ok=false}catch(e){ok=ok&&e.name==='TypeError'}
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void AggregateNameBudgetAndInFlightCancellationDoNotCreateForcedAbsentAttributes()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let body=document.body,failed=false;", Cancellation);
        host.ExecuteClassic("""
            let name='x'.repeat(65536);
            for(let i=0;i<5;++i){try{body.toggleAttribute(name,false)}catch(e){failed=e instanceof TypeError;break}}
            """, Cancellation);
        Assert.True(host.Evaluate("failed&&!body.hasAttribute('x')", Cancellation).Boolean);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
            "while(true){try{body.toggleAttribute('absent',false)}catch{}}", canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
