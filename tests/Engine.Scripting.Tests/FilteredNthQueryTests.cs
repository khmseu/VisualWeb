using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class FilteredNthQueryTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document(int children = 4)
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        var body = document.CreateElement("body"); html.AppendChild(body);
        for (var i = 0; i < children; i++)
        {
            var child = document.CreateElement("div"); child.SetAttribute("id", "n" + i);
            if (i != 1) { child.SetAttribute("class", "pick"); }
            body.AppendChild(child);
        }
        return document;
    }

    private static void AssertTrue(V8ScriptHost host, string script)
    {
        host.ExecuteClassic("var ok=false;" + script, Cancellation);
        Assert.True(host.Evaluate("ok", Cancellation).Boolean);
    }

    [Fact]
    public void QueriesFilterInTreeOrderAndReturnStaticNodeLists()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let body=document.body,a=document.getElementById('n0'),c=document.getElementById('n2'),d=document.getElementById('n3');
            body.insertBefore(document.createTextNode('gap'),c);
            body.insertBefore(document.createComment('gap'),c);
            let list=body.querySelectorAll(':nth-child(2n of :scope > .pick)');
            let before=body.querySelector(':nth-last-child(2 of .pick)');
            d.setAttribute('class','');
            a.remove();
            ok=before===c && Object.isFrozen(list) && list.length===1 && list[0]===c && list.item(1)===null
                && body.querySelector(':nth-child(2 of .pick)')===null
                && document.querySelector(':nth-last-child(1 of body > .pick)')===c
                && document.querySelectorAll(':nth-child(1 of .pick,#n2)').length===1;
            """);
    }

    [Fact]
    public void MatchesAndClosestKeepOriginalScopeThroughoutSiblingFilteringAndAncestorTraversal()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let body=document.body,a=document.getElementById('n0'),c=document.getElementById('n2');
            let inner=document.createElement('span'); c.appendChild(inner);
            ok=c.matches(':nth-child(2 of .pick)') && c.matches(':nth-child(1 of :scope)')
                && !c.matches(':nth-last-child(2 of :scope)') && !c.matches(':nth-child(1 of #n0)')
                && c.closest(':nth-child(1 of :scope)')===c
                && inner.closest('div:nth-child(1 of :scope)')===null
                && inner.closest('div:nth-child(2 of .pick)')===c
                && inner.closest('body:nth-last-child(1 of head + body)')===body
                && a.matches(':nth-last-child(3 of .pick)');
            """);
    }

    [Fact]
    public void FragmentsAndDetachedSingletonsKeepTheirScopeAndMembershipRules()
    {
        using var host = new V8ScriptHost(document: Document());
        AssertTrue(host, """
            let f=document.createDocumentFragment(),a=document.createElement('p'),b=document.createElement('p');
            f.appendChild(a);f.appendChild(document.createComment('gap'));f.appendChild(b);
            let lone=document.createElement('section'),child=document.createElement('p');lone.appendChild(child);
            ok=f.querySelector(':nth-child(2 of :scope > p)')===b
                && f.querySelector(':nth-last-child(1 of :scope > p)')===b
                && a.matches(':nth-child(1 of :scope)') && b.matches(':nth-child(1 of :scope)')
                && lone.matches(':nth-last-child(1 of section)') && !lone.matches(':nth-last-child(1 of p)')
                && lone.querySelector(':nth-child(1 of :scope > p)')===child
                && child.closest('section:nth-child(1 of :scope)')===null;
            """);
    }

    [Theory]
    [InlineData("document.querySelector(':nth-child(2 of .pick,???)')")]
    [InlineData("document.body.querySelectorAll(':nth-last-child(2 of)')")]
    [InlineData("document.body.matches(':nth-child(2of .pick)')")]
    [InlineData("document.body.closest(':nth-child(1 of > div)')")]
    [InlineData("document.createDocumentFragment().querySelector(':nth-child(1 of div,)')")]
    public void MalformedStrictListsThrowSyntaxError(string expression)
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("(()=>{try{" + expression + ";return false}catch(e){return e instanceof SyntaxError}})()", Cancellation).Boolean);
    }

    [Theory]
    [InlineData("document.querySelector(':nth-child(1 of :has(div))')")]
    [InlineData("document.body.querySelectorAll(':nth-child(1 of :hover)')")]
    [InlineData("document.body.matches(':nth-last-child(1 of ::before)')")]
    [InlineData("document.body.closest(':nth-of-type(1 of div)')")]
    [InlineData("document.body.closest(':nth-last-of-type(1 of div)')")]
    public void UnsupportedSelectorsStillThrowTypeError(string expression)
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("(()=>{try{" + expression + ";return false}catch(e){return e instanceof TypeError}})()", Cancellation).Boolean);
    }

    [Fact]
    public void NestedSiblingFilteringCannotResetSharedOperationBudgetAndCancellationRecovers()
    {
        using var host = new V8ScriptHost(document: Document(200));
        Assert.True(host.Evaluate("""
            (()=>{try{document.body.querySelectorAll(':nth-child(n of .pick)');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{document.getElementById('n199').matches(':nth-child(n of :nth-child(n of .pick))');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
        Assert.True(host.Evaluate("""
            (()=>{try{document.getElementById('n199').closest(':nth-child(n of :nth-child(n of .pick))');return false}
                catch(e){return e instanceof TypeError && e.message.includes('operation limit')}})()
            """, Cancellation).Boolean);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.Evaluate("document.body.querySelector(':nth-child(1 of .pick)')", canceled.Token));
        Assert.Equal(42, host.Evaluate("42", Cancellation).Number);
    }

    [Fact]
    public void FilteredQueriesKeepExactResultTraversalAndNestingBounds()
    {
        static DomDocument Leaves(int count)
        {
            var doc = Document(0);
            var body = doc.Descendants().OfType<DomElement>().Single(e => e.LocalName == "body");
            for (var i = 0; i < count; i++)
            {
                var parent = doc.CreateElement("div"); parent.AppendChild(doc.CreateElement("span"));
                body.AppendChild(parent);
            }
            return doc;
        }
        using (var exact = new V8ScriptHost(document: Leaves(1024)))
        {
            Assert.Equal(1024, exact.Evaluate("document.querySelectorAll('span:nth-child(1 of span)').length", Cancellation).Number);
        }
        using (var oversized = new V8ScriptHost(document: Leaves(1025)))
        {
            Assert.True(oversized.Evaluate("""
                (()=>{try{document.querySelectorAll('span:nth-last-child(1 of span)');return false}
                    catch(e){return e instanceof TypeError}})()
                """, Cancellation).Boolean);
        }
        using (var traversal = new V8ScriptHost(document: Leaves(4095)))
        {
            Assert.True(traversal.Evaluate("""
                (()=>{try{document.querySelector('missing:nth-child(1 of span)');return false}
                    catch(e){return e instanceof TypeError}})()
                """, Cancellation).Boolean);
        }
        using var nested = new V8ScriptHost(document: Document());
        Assert.True(nested.Evaluate("""
            (()=>{let s='div';for(let i=0;i<64;i++)s=':nth-child(1 of '+s+')';
                try{document.querySelector(s);return false}catch(e){return e instanceof TypeError}})()
            """, Cancellation).Boolean);
    }
}
