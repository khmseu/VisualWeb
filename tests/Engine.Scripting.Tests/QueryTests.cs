using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class QueryTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document(int children = 2)
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        var body = document.CreateElement("body"); html.AppendChild(body);
        for (var i = 0; i < children; i++)
        {
            var child = document.CreateElement("div"); child.SetAttribute("id", "n" + i);
            child.SetAttribute("class", "item"); body.AppendChild(child);
        }
        return document;
    }

    [Fact]
    public void QueriesUseTreeOrderDeduplicateBranchesAndShareLiveWrapperIdentities()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let a=document.getElementById('n0'), b=document.getElementById('n1');
            let all=document.querySelectorAll('#n1,div,.item');
            let ok=document.querySelector('div')===a && all.length===2 && all[0]===a && all[1]===b
                && document.querySelector('.missing')===null && document.querySelectorAll('.missing').length===0
                && document.body.querySelector('body')===null && document.body.querySelector('html body > div')===a
                && a.matches('body > .item:first-child') && !a.matches('#n1') && a.closest('div')===a
                && a.closest('body')===document.body && a.closest('.missing')===null;
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void StaticNodeListRemainsStableAfterMutationWithItemForEachAndIterators()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let list=document.querySelectorAll('.item'), a=list[0], b=list[1], seen=[];
            document.body.removeChild(a);b.removeAttribute('class');
            let added=document.createElement('div');added.setAttribute('class','item');document.body.appendChild(added);
            let receiver={};
            list.forEach(function(node,index,owner){if(this!==receiver||owner!==list)throw Error('receiver');seen.push(node===list[index]);},receiver);
            let ok=list.length===2 && list[0]===a && list[1]===b && list.item(0)===a && list.item(99)===null
                && list.item(-1)===null && list.item(4294967296)===a && [...list][1]===b
                && [...list.keys()].join(',')==='0,1' && [...list.entries()][0][1]===a && seen.every(Boolean)
                && document.querySelectorAll('.item')[0]===added && Object.isFrozen(list);
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void DetachedElementAndFragmentQueriesUseCurrentTreeWithoutIncludingReceiver()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            let fragment=document.createDocumentFragment(), outer=document.createElement('div'), inner=document.createElement('span');
            outer.appendChild(inner);fragment.appendChild(outer);
            let ok=fragment.querySelector('div > span')===inner && fragment.querySelectorAll('*').length===2
                && outer.querySelector('div')===null && inner.closest('div')===outer && inner.matches('div > span');
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("")]
    [InlineData("div,")]
    [InlineData("[")]
    [InlineData("#123")]
    [InlineData("div:not(???,div)")]
    public void InvalidSelectorsThrowSyntaxErrorWithoutSilentlyReturningEmpty(string selector)
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let selector=" + System.Text.Json.JsonSerializer.Serialize(selector) + ";", Cancellation);
        Assert.True(host.Evaluate("(()=>{try{document.querySelectorAll(selector);return false}catch(e){return e instanceof SyntaxError}})()", Cancellation).Boolean);
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Theory]
    [InlineData(":scope")]
    [InlineData(":hover")]
    [InlineData(":has(div)")]
    [InlineData("div::before")]
    [InlineData("svg|a")]
    public void UnsupportedSelectorFeaturesFailExplicitly(string selector)
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let selector=" + System.Text.Json.JsonSerializer.Serialize(selector) + ";", Cancellation);
        Assert.True(host.Evaluate("(()=>{try{document.querySelector(selector);return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
    }

    [Fact]
    public void ReceiverChecksPrecedeConversionAndNodeListMethodsRequireBrandedReceivers()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (()=>{let converted=false, selector={toString(){converted=true;return '*'}};
                try{document.querySelector.call({},selector)}catch(e){if(converted||!(e instanceof TypeError))return false}
                let text=document.createTextNode('x');try{text.querySelector(selector)}catch(e){if(converted)return false}
                let list=document.querySelectorAll('div');
                try{list.item.call({},0)}catch(e){if(!(e instanceof TypeError))return false}
                try{list.forEach(null)}catch(e){if(!(e instanceof TypeError))return false}
                try{document.querySelector()}catch(e){if(!(e instanceof TypeError))return false}
                try{document.querySelector(Symbol())}catch(e){return e instanceof TypeError}
                return false;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void CapturedIntrinsicsAndPrivateStorageResistPrototypeTampering()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            String.prototype[Symbol.split]=()=>{throw Error('tampered')};
            String.prototype.indexOf=()=>{throw Error('tampered')};String.prototype.slice=()=>{throw Error('tampered')};
            Map.prototype.get=()=>{throw Error('tampered')};WeakMap.prototype.get=()=>{throw Error('tampered')};
            Object.prototype[0]={forged:true};
            let list=document.querySelectorAll('div'), length=list.length;
            let ok=length===2 && list.item(2)===null && list[0]===document.querySelector('#n0');
            """, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void ExactResultLimitsAndFailedReservationsDoNotConsumeWrapperCapacity()
    {
        using (var host = new V8ScriptHost(document: Document(1024)))
        {
            Assert.Equal(1024, host.Evaluate("document.querySelectorAll('div').length", Cancellation).Number);
        }
        using var oversized = new V8ScriptHost(document: Document(1025));
        Assert.True(oversized.Evaluate("(()=>{try{document.querySelectorAll('div');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.Equal(1024, oversized.Evaluate("document.querySelectorAll('div:not(#n1024)').length", Cancellation).Number);
        using var reserved = new V8ScriptHost(document: Document(1024));
        reserved.ExecuteClassic("let body=document.body;", Cancellation);
        Assert.True(reserved.Evaluate("(()=>{try{document.querySelectorAll('div');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.Equal(1023, reserved.Evaluate("document.querySelectorAll('div:not(#n1023)').length", Cancellation).Number);
    }

    [Fact]
    public void SelectorLengthAndDepthLimitsAreExplicitAndCancellationDoesNotPoisonBeforeExecution()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.Equal(2, host.Evaluate("document.querySelectorAll(' '.repeat(4095)+'*').length-3", Cancellation).Number);
        Assert.True(host.Evaluate("(()=>{try{document.querySelector(' '.repeat(4096)+'*');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.True(host.Evaluate("(()=>{try{document.querySelector(':not('.repeat(64)+'div'+')'.repeat(64));return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.False(host.Evaluate("document.body.matches(':not('.repeat(63)+'*'+')'.repeat(63))", Cancellation).Boolean);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.Evaluate("document.querySelector('*')", canceled.Token));
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Theory]
    [InlineData(8189, false)]
    [InlineData(8190, true)]
    public void ExactTraversalBudgetAppliesEvenWhenNoElementsMatch(int children, bool excessive)
    {
        using var host = new V8ScriptHost(document: Document(children));
        if (excessive)
        {
            Assert.True(host.Evaluate("(()=>{try{document.querySelectorAll('.none');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        }
        else { Assert.Equal(0, host.Evaluate("document.querySelectorAll('.none').length", Cancellation).Number); }
    }

    [Fact]
    public void WholeQueryMatchingBudgetCannotResetForEachCandidate()
    {
        using var host = new V8ScriptHost(document: Document(400));
        Assert.True(host.Evaluate("""
            (()=>{try{document.querySelectorAll('missing ~ div');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void AdoptedWrappersRejectQueriesAndSnapshotValuesRemainOpaque()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let list=document.querySelectorAll('div'), node=list[0];", Cancellation);
        var other = new DomDocument(); other.AdoptNode(document.GetElementById("n0")!);
        Assert.True(host.Evaluate("""
            (()=>{if(list.item(0)!==node)return false;
                try{node.matches('div');return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
        Assert.Equal("undefined,undefined", host.Evaluate("[typeof list.GetType,typeof node.GetType].join(',')", Cancellation).Text);
    }

    [Fact]
    public void InFlightNativeQueryCancellationAndCallbackDeadlineInvalidateHost()
    {
        using (var host = new V8ScriptHost(document: Document(400)))
        {
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
            canceled.CancelAfter(TimeSpan.FromMilliseconds(150));
            Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic(
                "while(true){try{document.querySelectorAll('missing ~ div')}catch(e){if(e.GetType)throw Error('CLR leak')}}", canceled.Token));
            Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        }
        using var deadline = new V8ScriptHost(TimeSpan.FromMilliseconds(150), document: Document());
        Assert.Throws<ScriptLimitException>(() => deadline.ExecuteClassic(
            "document.querySelectorAll('div').forEach(()=>{while(true){}})", Cancellation));
        Assert.Throws<InvalidOperationException>(() => deadline.Evaluate("42", Cancellation));
    }
}
