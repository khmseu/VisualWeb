using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class ScopeQueryTests
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

    private static void AssertTrue(V8ScriptHost host, string script)
    {
        host.ExecuteClassic("var ok=false;" + script, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void ElementQueriesScopeToReceiverDescendantsWhileSelectorsMayLookOutside()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let body=document.body, a=document.getElementById('n0'), b=document.getElementById('n1');
            let inner=document.createElement('span'); a.appendChild(inner);
            let direct=body.querySelectorAll(':scope > div');
            ok=body.querySelector(':scope')===null && body.querySelectorAll(':scope').length===0
                && direct.length===2 && direct[0]===a && direct[1]===b
                && body.querySelector(':scope > span')===null && body.querySelector(':scope span')===inner
                && body.querySelector('html > :scope > div > span')===inner
                && a.querySelector(':scope > span')===inner && b.querySelector(':scope > span')===null
                && body.querySelectorAll(':is(:scope) > *').length===2
                && body.querySelectorAll(':not(:scope) > *').length===1 && body.querySelector(':not(:scope) > *')===inner
                && body.querySelectorAll(':where(:scope > div) span').length===1
                && body.querySelector(':scope ~ *')===null && a.querySelector(':scope + div')===null
                && document.documentElement.querySelector(':scope > body > :scope')===null;
            """);
    }

    [Fact]
    public void MatchesAnchorsReceiverAndClosestAnchorsTheOriginalReceiver()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let body=document.body, a=document.getElementById('n0'), b=document.getElementById('n1');
            let inner=document.createElement('span'); a.appendChild(inner);
            ok=a.matches(':scope') && a.matches('body > :scope.item') && !a.matches(':scope > *')
                && !a.matches(':not(:scope)') && b.matches(':scope') && !b.matches('#n0:scope')
                && inner.matches('div > :scope') && inner.matches(':is(:scope)') && !inner.matches(':where(div > :scope > *)')
                && inner.closest(':scope')===inner && inner.closest('div:scope')===null
                && inner.closest(':is(div, body):scope')===null && inner.closest('div:not(:scope)')===a
                && inner.closest('body:not(:scope)')===body && inner.closest(':scope > *')===null
                && inner.closest('body :scope')===inner && a.closest(':scope, body')===a
                && b.closest(':scope + div')===null && b.closest(':not(:scope) > :scope')===b
                && b.closest(':scope')===b;
            """);
    }

    [Fact]
    public void DocumentScopeResolvesDocumentElementAndSelectorsAreReusedAcrossReceivers()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let html=document.documentElement, body=document.body, a=document.getElementById('n0'), s=':scope > *';
            let first=body.querySelectorAll(s).length, nested=a.querySelectorAll(s).length, top=document.querySelectorAll(s);
            ok=document.querySelector(':scope')===html && document.querySelectorAll(':scope').length===1
                && document.querySelector(':scope > body')===body && document.querySelector(':scope > html')===null
                && html.matches(':scope') && first===2 && nested===0 && top.length===2 && top[0]===document.head
                && top[1]===body && body.querySelectorAll(s).length===2 && document.querySelector(':scope div')===a
                && html.querySelector(':scope > body')===body && html.querySelector(':scope')===null;
            """);
    }

    [Fact]
    public void FragmentScopeIsVirtualRootAndDetachedRootsStayScoped()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let f=document.createDocumentFragment(), top=document.createElement('p'), wrap=document.createElement('div');
            let nested=document.createElement('p'); wrap.appendChild(nested); f.appendChild(top); f.appendChild(wrap);
            let section=document.createElement('section'), child=document.createElement('p'); section.appendChild(child);
            ok=f.querySelector(':scope')===null && f.querySelectorAll(':scope > p').length===1
                && f.querySelector(':scope > p')===top && f.querySelectorAll(':scope p').length===2
                && f.querySelector(':not(:scope) > p')===nested && f.querySelector('* > p')===nested
                && f.querySelectorAll(':is(:scope) > *').length===2 && f.querySelector(':scope > :scope')===null
                && top.matches(':scope') && !top.matches(':scope > p') && nested.closest(':scope > div')===null
                && nested.closest('div:not(:scope)')===wrap && wrap.querySelector(':scope > p')===nested
                && section.querySelector(':scope > p')===child && section.querySelector(':scope')===null
                && child.matches(':scope') && !child.matches(':root') && child.closest(':root')===null
                && child.closest('section:not(:scope)')===section && section.querySelector(':root p')===null;
            """);
    }

    [Theory]
    [InlineData("document.querySelector('> div')")]
    [InlineData("document.body.querySelectorAll('+ div')")]
    [InlineData("document.body.matches(':scope >')")]
    [InlineData("document.body.closest(':scope,')")]
    [InlineData("document.createDocumentFragment().querySelector(':not(> p)')")]
    public void RelativeOrDanglingScopeSelectorsThrowSyntaxError(string expression)
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("(()=>{try{" + expression + ";return false}catch(e){return e instanceof SyntaxError}})()", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("document.querySelector(':has(:scope)')")]
    [InlineData("document.body.matches(':scope::before')")]
    [InlineData("document.body.closest(':scope(div)')")]
    [InlineData("document.body.matches.call(document, ':scope')")]
    [InlineData("document.body.closest.call(document.createDocumentFragment(), ':scope')")]
    [InlineData("document.querySelector.call(document.createTextNode('x'), ':scope')")]
    public void UnsupportedScopeFormsAndWrongReceiversThrowTypeError(string expression)
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("(()=>{try{" + expression + ";return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Fact]
    public void ScopedQueriesKeepExactResultTraversalOperationAndCancellationBounds()
    {
        using (var host = new V8ScriptHost(document: Document(1024)))
        {
            Assert.Equal(1024, host.Evaluate("document.querySelectorAll(':scope > body > div').length", Cancellation).Number);
        }
        using (var oversized = new V8ScriptHost(document: Document(1025)))
        {
            Assert.True(oversized.Evaluate("(()=>{try{document.querySelectorAll(':scope > body > div');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        }
        using (var traversal = new V8ScriptHost(document: Document(8190)))
        {
            Assert.True(traversal.Evaluate("(()=>{try{document.querySelector(':scope .none');return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
        }
        using var host2 = new V8ScriptHost(document: Document(400));
        Assert.True(host2.Evaluate("""
            (()=>{try{document.body.querySelectorAll(':scope > missing ~ div');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
        Assert.True(host2.Evaluate("""
            (()=>{try{document.getElementById('n399').closest(':not(:scope):not(:scope ~ *) ~ :scope');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host2.Evaluate("document.body.querySelector(':scope > div')", canceled.Token));
        Assert.Equal(42, host2.Evaluate("42", Cancellation).Number);
    }
}
