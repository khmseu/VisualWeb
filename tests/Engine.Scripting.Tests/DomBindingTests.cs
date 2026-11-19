using VisualWeb.Core.Url;
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
    public void DocumentUrlPropertiesAreReadonlyAndBoundOnlyWhenAUrlIsProvided()
    {
        const string url = "https://example.test/path?query=value#section";
        using var host = new V8ScriptHost(document: Document(), documentUrl: BrowserUrl.Parse(url));

        Assert.True(host.Evaluate("document.URL===document.documentURI&&document.URL==='" + url + "'", Cancellation).Boolean);
        Assert.Equal(ScriptValueKind.String, host.Evaluate("document.URL", Cancellation).Kind);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("'use strict'; document.URL = 'https://changed.test/';", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "Object.getOwnPropertyDescriptor(document, 'documentURI').get.call({})", Cancellation));
        Assert.Equal(url, host.Evaluate("document.documentURI", Cancellation).Text);

        using var unbound = new V8ScriptHost(document: Document());
        Assert.Equal("undefined", unbound.Evaluate("typeof document.URL", Cancellation).Text);
        Assert.Equal("undefined", unbound.Evaluate("typeof document.documentURI", Cancellation).Text);
        Assert.Equal("undefined", unbound.Evaluate("typeof document.baseURI", Cancellation).Text);
        Assert.Throws<ArgumentException>(() => new V8ScriptHost(documentUrl: BrowserUrl.Parse(url)));

        const string baseUrl = "https://example.test/base/";
        using var withBase = new V8ScriptHost(document: Document(), documentUrl: BrowserUrl.Parse(url),
            documentBaseUrlProvider: (_, _) => BrowserUrl.Parse(baseUrl));
        Assert.True(withBase.Evaluate(
            "document.baseURI===document.getElementById('child').baseURI&&document.baseURI==='" + baseUrl + "'",
            Cancellation).Boolean);
        Assert.Throws<ScriptExecutionException>(() =>
            withBase.ExecuteClassic("'use strict'; document.baseURI = 'https://changed.test/';", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => withBase.ExecuteClassic(
            "Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.getElementById('child')), 'baseURI').get.call({})",
            Cancellation));
    }

    [Fact]
    public void AnchorAndAreaHrefPropertiesResolveAndReflectAgainstTheDocumentBase()
    {
        var document = Document();
        var anchor = document.CreateElement("a");
        anchor.SetAttribute("id", "anchor");
        anchor.SetAttribute("href", "../start?query=value#section");
        document.Body!.AppendChild(anchor);
        var area = document.CreateElement("area");
        area.SetAttribute("id", "area");
        document.Body.AppendChild(area);
        var invalid = document.CreateElement("a");
        invalid.SetAttribute("id", "invalid");
        invalid.SetAttribute("href", "http://[");
        document.Body.AppendChild(invalid);
        const string pageUrl = "https://example.test/page";
        var baseUrl = BrowserUrl.Parse("https://example.test/first/sub/");
        using var host = new V8ScriptHost(document: document, documentUrl: BrowserUrl.Parse(pageUrl),
            documentBaseUrlProvider: (_, _) => baseUrl);

        Assert.True(host.Evaluate("""
            (() => {
                const anchor = document.getElementById('anchor');
                const area = document.getElementById('area');
                const invalid = document.getElementById('invalid');
                const div = document.getElementById('parent');
                return anchor.href === 'https://example.test/first/start?query=value#section' &&
                    anchor.toString() === anchor.href &&
                    document.querySelectorAll('a')[0].href === anchor.href &&
                    anchor.origin === 'https://example.test' &&
                    anchor.protocol === 'https:' &&
                    anchor.username === '' && anchor.password === '' &&
                    anchor.host === 'example.test' && anchor.hostname === 'example.test' &&
                    anchor.port === '' && anchor.pathname === '/first/start' &&
                    anchor.search === '?query=value' && anchor.hash === '#section' &&
                    area.href === '' &&
                    area.origin === '' && area.protocol === ':' &&
                    area.username === '' && area.pathname === '' &&
                    invalid.href === 'http://[' &&
                    invalid.protocol === ':' && invalid.pathname === '' &&
                    typeof div.href === 'undefined';
            })()
            """, Cancellation).Boolean);

        baseUrl = BrowserUrl.Parse("https://example.test/changed/");
        Assert.True(host.Evaluate("""
            (() => {
                const anchor = document.getElementById('anchor');
                return anchor.href === 'https://example.test/start?query=value#section';
            })()
            """, Cancellation).Boolean);

        host.ExecuteClassic("""
            (() => {
                const anchor = document.getElementById('anchor');
                const area = document.getElementById('area');
                anchor.href = '../next\uD800';
                area.href = 'https://area.example.test/map';
            })();
            """, Cancellation);
        Assert.Equal("../next\uFFFD", anchor.GetAttribute("href"));
        Assert.Equal("https://area.example.test/map", area.GetAttribute("href"));
        host.ExecuteClassic("""
            (() => {
                const anchor = document.getElementById('anchor');
                anchor.protocol = 'http:';
                anchor.username = 'user';
                anchor.password = 'pass';
                anchor.host = 'cdn.example.test:8443';
                anchor.pathname = '/new path';
                anchor.search = '?next=1';
                anchor.hash = '#done';
            })();
            """, Cancellation);
        Assert.Equal("http://user:pass@cdn.example.test:8443/new%20path?next=1#done", anchor.GetAttribute("href"));
        Assert.Equal("http://user:pass@cdn.example.test:8443/new%20path?next=1#done",
            host.Evaluate("document.getElementById('anchor').toString()", Cancellation).Text);

        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.getElementById('anchor')), 'href').get
                .call(document.getElementById('parent'));
            """, Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "'use strict'; document.getElementById('anchor').origin = 'https://changed.example';", Cancellation));
    }

    [Fact]
    public void BaseHrefPropertyUsesTheFallbackBaseAndReflectsUsvString()
    {
        var document = Document();
        var baseElement = document.CreateElement("base");
        baseElement.SetAttribute("id", "base");
        baseElement.SetAttribute("href", "assets/");
        document.Body!.AppendChild(baseElement);
        var missing = document.CreateElement("base");
        missing.SetAttribute("id", "missing");
        document.Body.AppendChild(missing);
        var invalid = document.CreateElement("base");
        invalid.SetAttribute("id", "invalid");
        invalid.SetAttribute("href", "http://[");
        document.Body.AppendChild(invalid);
        var data = document.CreateElement("base");
        data.SetAttribute("id", "data");
        data.SetAttribute("href", "data:text/html,example");
        document.Body.AppendChild(data);
        const string pageUrl = "https://example.test/dir/page";
        using var host = new V8ScriptHost(document: document, documentUrl: BrowserUrl.Parse(pageUrl),
            documentBaseUrlProvider: (_, _) => BrowserUrl.Parse("https://example.test/current/"));

        Assert.True(host.Evaluate("""
            (() => {
                const base = document.getElementById('base');
                return base.href === 'https://example.test/dir/assets/' &&
                    document.getElementById('missing').href === 'https://example.test/dir/page' &&
                    document.getElementById('invalid').href === 'http://[' &&
                    document.getElementById('data').href === 'data:text/html,example' &&
                    typeof document.getElementById('parent').href === 'undefined' &&
                    document.baseURI === 'https://example.test/current/';
            })()
            """, Cancellation).Boolean);

        host.ExecuteClassic("document.getElementById('base').href = '../changed\\uD800';", Cancellation);
        Assert.Equal("../changed\uFFFD", baseElement.GetAttribute("href"));
        Assert.Equal("https://example.test/changed%EF%BF%BD",
            host.Evaluate("document.getElementById('base').href", Cancellation).Text);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.getElementById('base')), 'href').get
                .call(document.getElementById('parent'));
            """, Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "document.getElementById('base').href = Symbol();", Cancellation));

        var unboundDocument = Document();
        var unboundBase = unboundDocument.CreateElement("base");
        unboundBase.SetAttribute("id", "base");
        unboundDocument.Body!.AppendChild(unboundBase);
        using var unbound = new V8ScriptHost(document: unboundDocument);
        Assert.Equal("undefined", unbound.Evaluate("typeof document.getElementById('base').href", Cancellation).Text);
    }

    [Fact]
    public void BaseTargetPropertyReflectsDomStringWithoutRequiringAPageUrl()
    {
        var document = Document();
        var baseElement = document.CreateElement("base");
        baseElement.SetAttribute("id", "base");
        document.Body!.AppendChild(baseElement);
        using var host = new V8ScriptHost(document: document);

        Assert.True(host.Evaluate("""
            (() => {
                const base = document.getElementById('base');
                return typeof base.target === 'string' &&
                    base.target === '' &&
                    Object.getOwnPropertyDescriptor(Object.getPrototypeOf(base), 'target').enumerable &&
                    Object.getOwnPropertyDescriptor(Object.getPrototypeOf(base), 'target').set !== undefined;
            })()
            """, Cancellation).Boolean);

        host.ExecuteClassic("""
            (() => {
                const base = document.getElementById('base');
                base.target = 42;
                if (base.target !== '42') throw new Error('target did not reflect DOMString');
                base.target = '\uD800';
                if (base.target.length !== 1 || base.target.charCodeAt(0) !== 0xD800)
                    throw new Error('target did not preserve its DOMString code unit');
                base.removeAttribute('target');
                if (base.target !== '') throw new Error('missing target did not return an empty string');
            })();
            """, Cancellation);
        Assert.Null(baseElement.GetAttribute("target"));

        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.getElementById('base')), 'target').get
                .call(document.getElementById('parent'));
            """, Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic(
            "document.getElementById('base').target = Symbol();", Cancellation));
    }

    [Fact]
    public void RepeatedBaseHrefReadsShareTheDomTextBudget()
    {
        var document = Document();
        var baseElement = document.CreateElement("base");
        baseElement.SetAttribute("href", new string('x', 60000));
        document.Body!.AppendChild(baseElement);
        using var host = new V8ScriptHost(document: document, documentUrl: BrowserUrl.Parse("https://example.test/"));

        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            (() => {
                const base = document.querySelector('base');
                for (let i = 0; i < 3; ++i) void base.href;
            })();
            """, Cancellation));
    }

    [Fact]
    public void RepeatedHyperlinkComponentReadsShareTheDomTextBudget()
    {
        using var host = new V8ScriptHost(document: Document(),
            documentUrl: BrowserUrl.Parse("https://example.test/"));

        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("""
            (() => {
                const link = document.createElement('a');
                link.href = 'https://example.test/' + 'x'.repeat(60000);
                for (let i = 0; i < 8; ++i) void link.protocol;
            })();
            """, Cancellation));
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
