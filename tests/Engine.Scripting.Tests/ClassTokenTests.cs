using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class ClassTokenTests
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
    public void ClassNameAndSameObjectListReflectExternalAndDetachedMutations()
    {
        var document = Document();
        var body = document.Body!;
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let body=document.body, list=body.classList;
            let initial=list===body.classList && body.className==='' && list.value==='' && list.length===0;
            body.className=' a\tb a\nC\r\f ';
            let parsed=list.length===3 && list[0]==='a' && list[1]==='b' && list[2]==='C'
                && list.contains('C') && !list.contains('c') && list.value===body.className;
            body.setAttribute('class','external');
            let live=list[0]==='external';
            document.documentElement.removeChild(body);
            list.add('detached');
            let detached=body.className==='external detached';
            body.removeAttribute('class');
            let removed=list.length===0 && list.value==='' && list[0]===undefined;
            body.classList='forwarded';
            let forwarded=list.value==='forwarded';
            list.value=' raw  raw ';
            let raw=body.className===' raw  raw ' && `${list}`===' raw  raw ' && list.length===1;
            """, Cancellation);
        Assert.True(host.Evaluate("initial&&parsed&&live&&detached&&removed&&forwarded&&raw", Cancellation).Boolean);
        Assert.Equal(" raw  raw ", body.GetAttribute("class"));
    }

    [Fact]
    public void OrderedSetMutationsDeduplicateAndNormalizeOnlyWhenRequired()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let body=document.body, list=body.classList;
            list.add();list.remove();list.toggle('missing',false);
            let absent=!body.hasAttribute('class');
            body.className=' a  b a ';
            let noOp=list.toggle('a',true) && !list.toggle('missing',false)
                && !list.replace('missing','x') && body.className===' a  b a ';
            list.add('b','c','c');let add=list.value==='a b c';
            list.remove('a','absent');let remove=list.value==='b c';
            let toggle=!list.toggle('b') && list.toggle('d',undefined) && list.toggle('e',{}) && !list.toggle('e',0);
            let final=list.value==='c d';
            list.remove('c','d');let empty=body.hasAttribute('class') && list.value==='';
            body.className=' x x ';list.add();let zero=list.value==='x';
            body.className=' y y ';list.remove();zero=zero&&list.value==='y';
            """, Cancellation);
        Assert.True(host.Evaluate("absent&&noOp&&add&&remove&&toggle&&final&&empty&&zero", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("a b c", "a", "c", "c b")]
    [InlineData("a b c", "c", "a", "a b")]
    [InlineData("a b c", "b", "x", "a x c")]
    [InlineData(" a a b ", "a", "a", "a b")]
    public void ReplacePreservesFirstSlotOfMergedTokens(string initial, string oldToken, string newToken, string expected)
    {
        var document = Document();
        document.Body!.SetAttribute("class", initial);
        using var host = new V8ScriptHost(document: document);
        var oldSource = System.Text.Json.JsonSerializer.Serialize(oldToken);
        var newSource = System.Text.Json.JsonSerializer.Serialize(newToken);
        Assert.True(host.Evaluate($"document.body.classList.replace({oldSource},{newSource})", Cancellation).Boolean);
        Assert.Equal(expected, host.Evaluate("document.body.className", Cancellation).Text);
    }

    [Fact]
    public void ValidationAndConversionCompleteBeforeMutationWithCorrectErrorNames()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let body=document.body,list=body.classList;body.className='old';
            let converted=false, empty=false, whitespace=false, precedence=false, symbol=false;
            try{list.add('',{toString(){converted=true;return 'new'}})}catch(e){empty=e.name==='SyntaxError'}
            try{list.remove('old','bad\tvalue')}catch(e){whitespace=e.name==='InvalidCharacterError'}
            try{list.replace('bad value','')}catch(e){precedence=e.name==='SyntaxError'}
            try{list.add('new',Symbol())}catch(e){symbol=e instanceof TypeError}
            let atomic=body.className==='old';
            let contains=!list.contains('')&&!list.contains('a b');
            let supports=false;try{list.supports('anything')}catch(e){supports=e instanceof TypeError}
            let ignored={toString(){throw Error('extra argument converted')}};
            let extra=list.replace('old','new',ignored);
            """, Cancellation);
        Assert.True(host.Evaluate("converted&&empty&&whitespace&&precedence&&symbol&&atomic&&contains&&supports&&extra", Cancellation).Boolean);
        Assert.Equal("new", host.Evaluate("document.body.className", Cancellation).Text);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\f")]
    public void MutatorsRejectEachAsciiWhitespaceCharacterAtomically(string whitespace)
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let bad='a'+" + System.Text.Json.JsonSerializer.Serialize(whitespace) + "+'b';", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{let list=document.body.classList;for(let method of ['add','remove','toggle','replace']){
                try{method==='replace'?list.replace('old',bad):list[method](bad);return false}
                catch(e){if(e.name!=='InvalidCharacterError')return false}
            }return !document.body.hasAttribute('class')})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void NonAsciiWhitespaceAndNulAreOrdinaryCaseSensitiveTokenCharacters()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let list=document.body.classList;list.add('a\u00a0b','x\0y','A','a');
                return list.length===4&&list.contains('a\u00a0b')&&list.contains('x\0y')})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void IndexedPropertiesAndUnsignedItemConversionAreLiveReadonlyAndEnumerable()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let list=document.body.classList;list.value='a b';
                let descriptor=Object.getOwnPropertyDescriptor(list,'0');
                let ok=Object.keys(list).join(',')==='0,1' && '1' in list && !('2' in list)
                    && list.item(0)==='a' && list.item(-1)===null && list.item(4294967296)==='a'
                    && list['01']===undefined && list[4294967295]===undefined && !descriptor.writable
                    && descriptor.enumerable && !Reflect.set(list,'0','bad') && !Reflect.deleteProperty(list,'0')
                    && !Reflect.defineProperty(list,'0',{value:'bad'}) && !Reflect.setPrototypeOf(list,{});
                list.remove('a');return ok&&list[0]==='b'&&list[1]===undefined&&Object.keys(list).length===1})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void IterationAndForEachObserveLiveChangesAndPreserveCallbackReceiver()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let list=document.body.classList;list.value='a b';
            let iterator=list.values(), first=iterator.next().value;
            list.add('c');let remaining=[...iterator].join(',');
            let keys=[...list.keys()].join(','), entries=[...list.entries()], self={};
            let seen=[];
            list.forEach(function(token,index,owner){
                if(this!==self||owner!==list||token!==list[index])throw Error('callback arguments');
                seen.push(token);if(index===0)list.add('d');
            },self);
            let live=first==='a'&&remaining==='b,c'&&keys==='0,1,2'&&entries[1][1]==='b'
                &&seen.join(',')==='a,b,c,d'&&[...list].join(',')==='a,b,c,d'&&iterator.next().done;
            """, Cancellation);
        Assert.True(host.Evaluate("live", Cancellation).Boolean);
    }

    [Fact]
    public void BrandsPrecedeArgumentConversionAndAllRequiredArgumentsAreChecked()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let list=document.body.classList, converted=false, argument={toString(){converted=true;return 'a'}};
                let methods=['contains','add','remove','toggle','replace','supports','item','forEach','values','keys','entries','toString'];
                for(let method of methods){try{list[method].call({},argument,argument);return false}catch(e){if(!(e instanceof TypeError))return false}}
                for(let method of ['contains','toggle','replace','supports','item']){try{list[method]();return false}catch(e){if(!(e instanceof TypeError))return false}}
                let get=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.body),'classList').get;
                try{get.call(document.createTextNode('x'));return false}catch(e){if(!(e instanceof TypeError))return false}
                return !converted})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void TokenAndArgumentLimitsAreExactAndFailuresDoNotPartiallyWrite()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let list=document.body.classList, tokens=Array.from({length:1024},(_,i)=>'t'+i);
            list.add(...tokens);let exact=list.length===1024&&list[1023]==='t1023', before=list.value;
            let overflow=false,args=false;
            try{list.add('extra')}catch(e){overflow=e instanceof TypeError}
            try{list.remove(...Array(1025).fill('t0'))}catch(e){args=e instanceof TypeError}
            let atomic=list.value===before;
            list.remove('t0');list.add('extra');let recovery=list.length===1024&&list[1023]==='extra';
            """, Cancellation);
        Assert.True(host.Evaluate("exact&&overflow&&args&&atomic&&recovery", Cancellation).Boolean);
    }

    [Fact]
    public void OversizedExternalTokenSetsFailExplicitlyAndCanBeReplacedThroughRawValue()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let list=document.body.classList;
            document.body.className=Array.from({length:1025},(_,i)=>'t'+i).join(' ');
            let failed=false;try{list.length}catch(e){failed=e instanceof TypeError}
            list.value='recovered';
            """, Cancellation);
        Assert.True(host.Evaluate("failed&&document.body.classList.length===1", Cancellation).Boolean);
    }

    [Fact]
    public void TextStorageAndSharedInputBudgetsRejectBeforeMutation()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let list=document.body.classList;list.add('a'.repeat(65531));", Cancellation);
        Assert.True(host.Evaluate("list.value.length===65531&&list.length===1", Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{list.add('b');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal(65531, host.Evaluate("list.value.length", Cancellation).Number);
        host.ExecuteClassic("document.body.className='';", Cancellation);
        Assert.True(host.Evaluate("""
            (()=>{let list=document.body.classList, token='a'.repeat(65536),failed=false;
                for(let i=0;i<5;++i){try{list.contains(token)}catch(e){failed=e instanceof TypeError;break}}
                return failed})()
            """, Cancellation).Boolean);
        Assert.Equal("", host.Evaluate("document.body.className", Cancellation).Text);
    }

    [Fact]
    public void CapturedIntrinsicsAndPrivateStorageSurvivePrototypeAndGlobalTampering()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let list=document.body.classList;
            WeakMap.prototype.get=WeakMap.prototype.set=()=>{throw Error('weak map')};
            String.prototype.split=String.prototype.slice=Array.prototype.join=()=>{throw Error('strings')};
            Object.defineProperty(Object.prototype,'0',{get(){throw Error('inherited index')}});
            Object.defineProperty(Object.prototype,'get',{get(){throw Error('descriptor pollution')}});
            globalThis.String=globalThis.Proxy=globalThis.TypeError=globalThis.Error=()=>{throw 42};
            list.add('a','b');list.replace('a','c');
            let error=false;try{list.add('bad value')}catch(e){error=e.name==='InvalidCharacterError'}
            let ok=list[0]==='c'&&list.contains('b')&&list.value==='c b'&&error;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void AdoptedElementInvalidatesSavedListOperationsWithoutAClrObject()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let list=document.body.classList;", Cancellation);
        var other = new DomDocument();
        other.AppendChild(document.DocumentElement!.LastChild!);
        Assert.True(host.Evaluate("""
            (()=>{try{list.add('bad');return false}catch(e){return e instanceof TypeError&&typeof e.GetType==='undefined'}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InfiniteForEachCallbackIsInterruptedAndInvalidatesHost()
    {
        using var host = new V8ScriptHost(TimeSpan.FromMilliseconds(150), document: Document());
        host.ExecuteClassic("document.body.className='a';", Cancellation);
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassic("document.body.classList.forEach(()=>{while(true){}});", Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void CallbackBudgetIsExactSharedAcrossSourcesAndResetOnNextTask()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let list=document.body.classList,failed=false;", Cancellation);
        host.ExecuteClassic("for(let i=0;i<2048;++i)list.contains('x');", Cancellation);
        host.ExecuteClassicBatch([
            "for(let i=0;i<1024;++i)list.contains('x');",
            "for(let i=0;i<1024;++i)list.contains('x');",
            "try{list.contains('x')}catch(e){failed=e instanceof TypeError&&e.message.includes('callback count')}"
        ], Cancellation);
        Assert.True(host.Evaluate("failed&&!list.contains('x')", Cancellation).Boolean);
    }

    [Fact]
    public void ArgumentConversionSideEffectsAreReadBeforeAtomicTokenMutation()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let body=document.body,list=body.classList;body.className='initial';
                list.add({toString(){body.className='converted';return 'new'}});
                return body.className==='converted new'})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void InFlightCancellationInvalidatesNativeTokenWork()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let list=document.body.classList;list.value=Array.from({length:1024},(_,i)=>'t'+i).join(' ');", Cancellation);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("""
            while(true){try{list.contains('absent')}catch(e){if(e.GetType)throw Error('CLR leak')}}
            """, canceled.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }
}
