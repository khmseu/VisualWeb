using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class DomMutationTests
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
    public void AttributesAreLiveCaseInsensitiveAndPreserveMissingVersusEmpty()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let element = document.createElement('DIV');
            element.setAttribute('ID', 'created');
            element.setAttribute('data-empty', '');
            document.body.appendChild(element);
            """, Cancellation);
        var element = document.GetElementById("created")!;
        Assert.Equal("div", element.LocalName);
        Assert.Equal("", host.Evaluate("element.getAttribute('DATA-EMPTY')", Cancellation).Text);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("element.getAttribute('missing')", Cancellation).Kind);
        Assert.True(host.Evaluate("element.hasAttribute('id') && element === document.getElementById('created')", Cancellation).Boolean);
        element.SetAttribute("data-native", "native");
        Assert.Equal("native", host.Evaluate("element.getAttribute('data-native')", Cancellation).Text);
        host.ExecuteClassic("element.setAttribute('id', 'renamed'); element.removeAttribute('DATA-EMPTY');", Cancellation);
        Assert.Null(document.GetElementById("created"));
        Assert.Same(element, document.GetElementById("renamed"));
        Assert.False(host.Evaluate("element.hasAttribute('data-empty')", Cancellation).Boolean);
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("element.removeAttribute('missing')", Cancellation).Kind);
        host.ExecuteClassic("element.setAttribute('data-value', null);", Cancellation);
        Assert.Equal("null", element.GetAttribute("data-value"));
    }

    [Fact]
    public void ElementTextFragmentCreationAndMutationsPreserveNodeIdentityAndOrder()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("document.documentElement.parentNode === document && document.head.parentNode === document.documentElement", Cancellation).Boolean);
        host.ExecuteClassic("""
            let parent = document.createElement('div'), text = document.createTextNode('first');
            let fragment = document.createDocumentFragment(), tail = document.createTextNode('tail');
            fragment.appendChild(text); fragment.appendChild(tail);
            document.body.appendChild(parent);
            let inserted = parent.appendChild(fragment) === fragment;
            let ordered = parent.firstChild === text && text.nextSibling === tail && tail.previousSibling === text;
            let emptied = fragment.firstChild === null && fragment.lastChild === null;
            let replacement = document.createTextNode('replacement');
            let replaced = parent.replaceChild(replacement, text) === text;
            let removed = parent.removeChild(tail) === tail;
            let moved = parent.insertBefore(tail, replacement) === tail;
            let appendAtEnd = parent.insertBefore(replacement, undefined) === replacement;
            """, Cancellation);
        Assert.True(host.Evaluate("inserted && ordered && emptied && replaced && removed && moved && appendAtEnd", Cancellation).Boolean);
        Assert.True(host.Evaluate("text.parentNode === null && tail.parentNode === parent && parent.lastChild === replacement", Cancellation).Boolean);
        Assert.Equal("tailreplacement", document.Body!.FirstChild!.TextContent);
        Assert.Equal(3, host.Evaluate("text.nodeType", Cancellation).Number);
        Assert.Equal(11, host.Evaluate("fragment.nodeType", Cancellation).Number);
        Assert.Equal(9, host.Evaluate("document.nodeType", Cancellation).Number);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("document.textContent", Cancellation).Kind);
        Assert.True(host.Evaluate("parent.isConnected && !text.isConnected && !fragment.isConnected", Cancellation).Boolean);
        host.ExecuteClassic("text.textContent = undefined; fragment.textContent = 'fragment text';", Cancellation);
        Assert.Equal("", host.Evaluate("text.textContent", Cancellation).Text);
        Assert.Equal("fragment text", host.Evaluate("fragment.textContent", Cancellation).Text);
    }

    [Theory]
    [InlineData("document.createElement()")]
    [InlineData("document.createTextNode()")]
    [InlineData("document.createElement(Symbol())")]
    [InlineData("document.createTextNode(Symbol())")]
    [InlineData("document.createElement('div', {is:'custom'})")]
    [InlineData("document.body.setAttribute('x')")]
    [InlineData("document.body.setAttribute(Symbol(), 'x')")]
    [InlineData("document.body.setAttribute('x', Symbol())")]
    [InlineData("document.body.getAttribute()")]
    [InlineData("document.body.removeAttribute()")]
    [InlineData("document.body.hasAttribute()")]
    [InlineData("document.createElement('bad name')")]
    [InlineData("document.body.appendChild({})")]
    [InlineData("document.body.appendChild(null)")]
    [InlineData("document.body.insertBefore(document.createElement('p'))")]
    [InlineData("document.body.replaceChild(document.createElement('p'), null)")]
    [InlineData("document.body.appendChild.call({}, document.body)")]
    [InlineData("document.createElement.call({}, 'p')")]
    [InlineData("document.createDocumentFragment.call({})")]
    [InlineData("document.body.getAttribute.call(document.createTextNode('x'), 'id')")]
    public void RequiredArgumentsConversionsAndReceiverBrandsRejectInvalidCalls(string script)
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(script, Cancellation));
        Assert.Equal(1, host.Evaluate("1", Cancellation).Number);
    }

    [Fact]
    public void HierarchyNotFoundAndConversionFailuresDoNotPartiallyMutateTrees()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            let parent = document.createElement('div'), child = document.createElement('span'), other = document.createElement('p');
            document.body.appendChild(parent); parent.appendChild(child);
            """, Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("child.appendChild(parent);", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.appendChild(other);", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("parent.insertBefore(other, document.body);", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("parent.removeChild(other);", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("parent.replaceChild(other, document.body);", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body.setAttribute('bad name', 'value');", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body.setAttribute('x', {toString(){throw Error('conversion')}});", Cancellation));
        Assert.True(host.Evaluate("child.parentNode === parent && parent.parentNode === document.body && other.parentNode === null", Cancellation).Boolean);
        Assert.False(host.Evaluate("document.body.hasAttribute('x')", Cancellation).Boolean);
        Assert.True(host.Evaluate("parent.insertBefore(child, child) === child && parent.replaceChild(child, child) === child", Cancellation).Boolean);
    }

    [Fact]
    public void NativeAdoptionInvalidatesAllAccessAndForeignArgumentsCannotPassBrands()
    {
        var document = Document();
        var child = document.CreateElement("p"); child.SetAttribute("id", "child"); document.Body!.AppendChild(child);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let child = document.getElementById('child');", Cancellation);
        new DomDocument().AdoptNode(child);
        foreach (var script in new[] { "child.nodeType", "child.parentNode", "child.isConnected", "child.getAttribute('id')",
            "document.body.appendChild(child)", "child.setAttribute('x','y')" })
        {
            Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(script, Cancellation));
        }
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body.appendChild(new Proxy(document.body, {}));", Cancellation));
        Assert.Null(child.ParentNode);
        Assert.Null(child.GetAttribute("x"));
    }

    [Fact]
    public void CapturedIntrinsicsProtectNewMethodsAndNoClrObjectsAreExposed()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("""
            Map.prototype.get = Map.prototype.set = WeakMap.prototype.get = WeakMap.prototype.set = () => {throw 'tampered'};
            String.prototype.slice = String.prototype.indexOf = () => {throw 'tampered'};
            globalThis.TypeError = () => {};
            let node = document.createElement('p');
            node.setAttribute('id','safe');
            document.body.appendChild(node);
            """, Cancellation);
        Assert.True(host.Evaluate("node === document.getElementById('safe') && node.parentNode === document.body", Cancellation).Boolean);
        Assert.Equal("undefined,undefined,undefined,undefined", host.Evaluate(
            "[typeof __visualwebDom,typeof node.GetType,typeof node.parentNode.GetType,typeof document.createTextNode('x').GetType].join(',')", Cancellation).Text);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("node.appendChild({});", Cancellation));
    }

    [Fact]
    public void AttributeCountAndStorageBudgetsHaveExactAtomicBoundaries()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element = document.createElement('div'); for(let i=0;i<128;i++) element.setAttribute('a'+i,'');", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("element.setAttribute('overflow','x');", Cancellation));
        Assert.False(host.Evaluate("element.hasAttribute('overflow')", Cancellation).Boolean);
        host.ExecuteClassic("element.setAttribute('A0','replace'); element.removeAttribute('a127'); element.setAttribute('last','');", Cancellation);
        Assert.Equal("replace", host.Evaluate("element.getAttribute('a0')", Cancellation).Text);
        host.ExecuteClassic("let large = document.createElement('div'); large.setAttribute('x','z'.repeat(65535));", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("large.setAttribute('y','');", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("large.setAttribute('x','z'.repeat(65536));", Cancellation));
        Assert.Equal(65535, host.Evaluate("large.getAttribute('x').length", Cancellation).Number);
        Assert.False(host.Evaluate("large.hasAttribute('y')", Cancellation).Boolean);
        host.ExecuteClassic("large.setAttribute('X','shrunk'); large.setAttribute('y','');", Cancellation);
        Assert.Equal("shrunk", host.Evaluate("large.getAttribute('x')", Cancellation).Text);
        Assert.True(host.Evaluate("large.hasAttribute('y')", Cancellation).Boolean);
    }

    [Fact]
    public void NodeCreationSharesTheExactLifetimeWrapperBudget()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let first = document.createTextNode('first'); for(let i=1;i<1024;i++) document.createDocumentFragment();", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.createElement('div');", Cancellation));
        Assert.Equal("first", host.Evaluate("first.textContent", Cancellation).Text);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body;", Cancellation));
    }

    [Fact]
    public void AggregateArgumentBudgetsAndOversizedAttributeReadsFailWithoutPartialWrites()
    {
        var document = Document();
        document.Body!.SetAttribute("oversized", new string('x', 65537));
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element = document.createElement('div'), text = document.createTextNode('x'.repeat(65536));", Cancellation);
        host.ExecuteClassic("for(let i=0;i<3;i++) text.textContent; element.setAttribute('x','v'.repeat(65535));", Cancellation);
        Assert.Equal(65535, host.Evaluate("element.getAttribute('x').length", Cancellation).Number);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "for(let i=0;i<3;i++) text.textContent; element.setAttribute('x','v'.repeat(65536));", Cancellation));
        Assert.Equal(65535, host.Evaluate("element.getAttribute('x').length", Cancellation).Number);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body.getAttribute('oversized');", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("element.setAttribute('x','x'.repeat(65537));", Cancellation));
        Assert.Equal(65535, host.Evaluate("element.getAttribute('x').length", Cancellation).Number);
    }

    [Fact]
    public void ElementReceiverChecksPrecedeUserConversionHooks()
    {
        using var host = new V8ScriptHost(document: Document());
        host.ExecuteClassic("let converted=false; let method = document.body.setAttribute; let text=document.createTextNode('');", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "method.call(text,{toString(){converted=true; return 'x'}},'y');", Cancellation));
        Assert.False(host.Evaluate("converted", Cancellation).Boolean);
    }

    [Fact]
    public void CommentsDoctypesAndDocumentHaveTheirNativeTextAndRootSemantics()
    {
        var document = Document();
        document.InsertBefore(document.CreateDocumentType("html"), document.DocumentElement);
        var comment = document.CreateComment("comment"); document.Body!.AppendChild(comment);
        using var host = new V8ScriptHost(document: document);
        Assert.Equal(10, host.Evaluate("document.firstChild.nodeType", Cancellation).Number);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("document.firstChild.textContent", Cancellation).Kind);
        Assert.Equal("comment", host.Evaluate("document.body.firstChild.textContent", Cancellation).Text);
        Assert.Equal("", host.Evaluate("document.body.textContent", Cancellation).Text);
        host.ExecuteClassic("document.body.firstChild.textContent='changed'; document.textContent='ignored'; document.firstChild.textContent='ignored';", Cancellation);
        Assert.Equal("changed", comment.Data);
        Assert.Equal("html", document.Doctype!.Name);
        Assert.Same(document.DocumentElement, document.Doctype.NextSibling);
    }

    [Fact]
    public void DestinationSubtreeLimitRejectsGrowthBeforeMovingNodesAndAllowsReorderAtLimit()
    {
        var document = Document();
        var parent = document.CreateElement("div"); parent.SetAttribute("id", "parent"); document.Body!.AppendChild(parent);
        for (var i = 0; i < 8192; i++) { parent.AppendChild(document.CreateTextNode("")); }
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let parent = document.getElementById('parent'), first = parent.firstChild, added = document.createTextNode('new');", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("parent.appendChild(added);", Cancellation));
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("added.parentNode", Cancellation).Kind);
        Assert.Equal(8192, parent.ChildNodes.Count);
        Assert.True(host.Evaluate("parent.appendChild(first) === first && parent.lastChild === first", Cancellation).Boolean);
        Assert.True(host.Evaluate("parent.replaceChild(added, first) === first && added.parentNode === parent", Cancellation).Boolean);
        Assert.Equal(8192, parent.ChildNodes.Count);
    }
}
