using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class DomBindingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        var body = document.CreateElement("body"); html.AppendChild(body);
        var parent = document.CreateElement("div"); parent.SetAttribute("id", "parent"); body.AppendChild(parent);
        var child = document.CreateElement("span"); child.SetAttribute("id", "child"); parent.AppendChild(child);
        child.TextContent = "original"; parent.AppendChild(document.CreateComment("ignored"));
        return document;
    }

    [Fact]
    public void TitleAndTextContentAreLiveAndWrappersPreserveIdentity()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("""
            document.title = '  Hello\t world\n ';
            let child = document.getElementById('child');
            child.textContent = 'changed';
            let same = child === document.getElementById('child');
            """, Cancellation);
        Assert.Equal("Hello world", document.Title);
        Assert.Equal("  Hello\t world\n ", document.Head!.FirstChild!.TextContent);
        Assert.Equal("changed", document.GetElementById("child")!.TextContent);
        Assert.Equal("changed", host.Evaluate("document.getElementById('parent').textContent", Cancellation).Text);
        Assert.True(host.Evaluate("same", Cancellation).Boolean);
        document.Title = "native update"; document.GetElementById("child")!.TextContent = "native text";
        Assert.Equal("native update", host.Evaluate("document.title", Cancellation).Text);
        Assert.Equal("native text", host.Evaluate("child.textContent", Cancellation).Text);
    }
    [Fact]
    public void RemovedNodesRemainLiveButLookupUsesConnectedTreeOrder()
    {
        var document = Document();
        var child = document.GetElementById("child")!;
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let child = document.getElementById('child'); document.getElementById('parent').textContent = 'replacement';", Cancellation);
        Assert.Null(child.ParentNode);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("document.getElementById('child')", Cancellation).Kind);
        host.ExecuteClassic("child.textContent = 'detached';", Cancellation);
        Assert.Equal("detached", child.TextContent);
        Assert.Equal("replacement", document.GetElementById("parent")!.TextContent);
        var duplicate = document.CreateElement("div"); duplicate.SetAttribute("id", "parent"); duplicate.TextContent = "later";
        document.Body!.AppendChild(duplicate);
        Assert.Equal("replacement", host.Evaluate("document.getElementById('parent').textContent", Cancellation).Text);
    }
    [Fact]
    public void DomStringConversionsAndIllegalReceiversFollowTheDocumentedSubset()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("document.title = undefined; document.getElementById('child').textContent = null;", Cancellation);
        Assert.Equal("undefined", document.Title);
        Assert.Equal("", document.GetElementById("child")!.TextContent);
        host.ExecuteClassic("document.getElementById('child').textContent = {toString(){return 'converted'}};", Cancellation);
        Assert.Equal("converted", document.GetElementById("child")!.TextContent);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.title = Symbol();", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById(Symbol());", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById('child').textContent = Symbol();", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById();", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById.call({}, 'child');", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.getElementById('child')), 'textContent').get.call({});
            """, Cancellation));
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("document.getElementById('')", Cancellation).Kind);
    }
    [Fact]
    public void PrivateCallbacksAndClrObjectsCannotBeReachedThroughTheFacade()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.Equal("undefined,undefined,undefined,undefined,undefined",
            host.Evaluate("[typeof __visualwebDom,typeof document.GetType,typeof document.getElementById('child').GetType,typeof clr,typeof System].join(',')", Cancellation).Text);
        Assert.False(host.Evaluate("Reflect.ownKeys(globalThis).includes('__visualwebDom')", Cancellation).Boolean);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("Object.getPrototypeOf(document)", Cancellation).Kind);
        Assert.Equal(ScriptValueKind.Null, host.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(document.getElementById('child')))", Cancellation).Kind);
        Assert.Throws<UnsupportedScriptValueException>(() => host.Evaluate("document.getElementById('child')", Cancellation));
        host.ExecuteClassic("""
            Map.prototype.get = Map.prototype.set = WeakMap.prototype.get = WeakMap.prototype.set = () => { throw 'tampered'; };
            String.prototype.slice = () => 'tampered';
            globalThis.TypeError = () => {};
            document.title = 'safe';
            document.getElementById('child').textContent = 'safe';
            """, Cancellation);
        Assert.Equal("safe", host.Evaluate("document.title", Cancellation).Text);
        Assert.Equal("safe", host.Evaluate("document.getElementById('child').textContent", Cancellation).Text);
    }
    [Fact]
    public void DocumentsHaveExclusiveHostOwnershipAndAdoptedNodesAreRejected()
    {
        var document = Document();
        using (var first = new V8ScriptHost(document: document))
        {
            Assert.Throws<InvalidOperationException>(() => new V8ScriptHost(document: document));
            first.ExecuteClassic("let child = document.getElementById('child');", Cancellation);
            var other = Document();
            other.AdoptNode(document.GetElementById("child")!);
            Assert.Throws<ScriptExecutionException>(() => first.ExecuteClassic("child.textContent = 'cross document';", Cancellation));
        }
        using var reused = new V8ScriptHost(document: document);
        Assert.Equal("", reused.Evaluate("document.title", Cancellation).Text);
    }
    [Fact]
    public void MutationBeforeScriptErrorIsNotRolledBackAndConversionErrorsDoNotMutate()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.title = 'before'; throw new Error('fixture');", Cancellation));
        Assert.Equal("before", document.Title);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.title = {toString(){throw 'conversion'}};", Cancellation));
        Assert.Equal("before", document.Title);
    }
    [Fact]
    public void TextAndCallbackLimitsAreExplicitAndResetBetweenExecutions()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("document.getElementById('child').textContent = 'x'.repeat(65536);", Cancellation);
        Assert.Equal(65536, document.GetElementById("child")!.TextContent!.Length);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById('child').textContent = 'x'.repeat(65537);", Cancellation));
        Assert.Equal(65536, document.GetElementById("child")!.TextContent!.Length);
        host.ExecuteClassic("for(let i=0;i<4096;i++) document.getElementById('missing');", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("for(let i=0;i<4097;i++) document.getElementById('missing');", Cancellation));
        Assert.Equal("", host.Evaluate("document.title", Cancellation).Text);
    }
    [Fact]
    public void WrapperCountAndTraversalBudgetsHaveExactBoundaries()
    {
        var document = Document();
        for (var i = 0; i < 1025; i++)
        {
            var element = document.CreateElement("div"); element.SetAttribute("id", "id" + i);
            document.Body!.AppendChild(element);
        }
        using (var host = new V8ScriptHost(timeout: TimeSpan.FromSeconds(30), document: document))
        {
            host.ExecuteClassic("for(let i=0;i<1024;i++) document.getElementById('id'+i);", Cancellation);
            Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.getElementById('id1024');", Cancellation));
            Assert.Equal("", host.Evaluate("document.getElementById('id0').textContent", Cancellation).Text);
        }
        var large = new DomDocument();
        var root = large.CreateElement("html"); large.AppendChild(root);
        for (var i = 0; i < 8191; i++) { root.AppendChild(large.CreateComment("ignored")); }
        using var bounded = new V8ScriptHost(document: large);
        Assert.Equal(ScriptValueKind.Null, bounded.Evaluate("document.getElementById('missing')", Cancellation).Kind);
        root.AppendChild(large.CreateComment("excess"));
        Assert.Throws<ScriptExecutionException>(() => bounded.ExecuteClassic("document.getElementById('missing');", Cancellation));
    }
    [Fact]
    public void AggregateCallbackTextBudgetResetsAndFailedReadsExposeNoPartialValues()
    {
        var document = Document();
        document.GetElementById("child")!.TextContent = new string('x', 65536);
        using var host = new V8ScriptHost(document: document);
        host.ExecuteClassic("let element = document.getElementById('child');", Cancellation);
        host.ExecuteClassic("for(let i=0;i<4;i++) element.textContent;", Cancellation);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("for(let i=0;i<5;i++) element.textContent;", Cancellation));
        document.GetElementById("child")!.TextContent = new string('x', 65537);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("element.textContent;", Cancellation));
        document.GetElementById("child")!.TextContent = "reset";
        Assert.Equal("reset", host.Evaluate("element.textContent", Cancellation).Text);
    }
}
