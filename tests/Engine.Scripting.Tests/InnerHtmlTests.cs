using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class InnerHtmlTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html");
        document.AppendChild(html);
        html.AppendChild(document.CreateElement("head"));
        html.AppendChild(document.CreateElement("body"));
        return document;
    }

    [Fact]
    public void GetterAndSetterUseHtmlSerializationAndContextualParsing()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (() => {
                const body = document.body;
                body.innerHTML = '<p title="a&amp;&quot;">A &amp; B<br><script>globalThis.fragmentRan=true;if(a<b){x&y}</script></p>';
                return body.innerHTML === '<p title="a&amp;&quot;">A &amp; B<br><script>globalThis.fragmentRan=true;if(a<b){x&y}</script></p>'
                    && body.firstChild.localName === 'p' && typeof fragmentRan === 'undefined';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SetterUsesContextAndTemplateContentAndNullConversion()
    {
        var document = Document();
        using var host = new V8ScriptHost(document: document);
        var actual = host.Evaluate("""
            (() => {
                const table = document.createElement('table');
                table.innerHTML = '<tr><td>cell</td></tr>';
                const textarea = document.createElement('textarea');
                textarea.innerHTML = '<b>&amp;';
                const undefinedValue = document.createElement('div');
                undefinedValue.innerHTML = undefined;
                const select = document.createElement('select');
                select.innerHTML = '<option>one<option>two';
                const template = document.createElement('template');
                template.innerHTML = '<b>inert</b>';
                document.body.appendChild(table);
                document.body.appendChild(textarea);
                document.body.appendChild(select);
                document.body.appendChild(template);
                table.innerHTML = null;
                return [table.innerHTML, textarea.innerHTML, undefinedValue.innerHTML, select.innerHTML, template.innerHTML,
                    String(template.firstChild), String(template.content)].join('|');
            })()
            """, Cancellation).Text;
        Assert.Equal("|&lt;b&gt;&amp;|undefined|<option>one</option><option>two</option>|<b>inert</b>|null|undefined", actual);
        Assert.Empty(document.Body!.ChildNodes.OfType<DomElement>().First(e => e.LocalName == "table").ChildNodes);
        var templateElement = Assert.IsType<DomTemplateElement>(document.Body.Descendants().OfType<DomTemplateElement>().Single());
        Assert.Same(templateElement.Content.OwnerDocument, templateElement.Content.FirstChild!.OwnerDocument);
    }

    [Fact]
    public void ReceiverBrandPrecedesConversionAndSymbolConversionErrorsPropagate()
    {
        using var host = new V8ScriptHost(document: Document());
        Assert.True(host.Evaluate("""
            (() => {
                let converted = false;
                const value = {toString() {converted = true; return '<b>ok</b>';}};
                const descriptor = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(document.body), 'innerHTML');
                let badReceiver = false, symbol = false, preserved = false;
                try { descriptor.set.call({}, value); } catch (e) { badReceiver = e instanceof TypeError; }
                try { document.body.innerHTML = Symbol(); } catch (e) { symbol = e instanceof TypeError; }
                const marker = {};
                try { document.body.innerHTML = {toString() {throw marker;}}; } catch (e) { preserved = e === marker; }
                return badReceiver && symbol && preserved && !converted && document.body.innerHTML === '';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UnsupportedParseAndCanceledOrLimitedReplacementLeaveOldChildren()
    {
        var document = Document();
        var frame = document.CreateElement("frame");
        document.Body!.AppendChild(frame);
        frame.AppendChild(document.CreateTextNode("old"));
        var oldText = frame.FirstChild;
        using var host = new V8ScriptHost(document: document);
        Assert.True(host.Evaluate("""
            (() => {
                const frame = document.body.firstChild;
                let unsupported = false;
                try { frame.innerHTML = '<b>new</b>'; }
                catch (e) { unsupported = e instanceof TypeError && e.message.includes('not implemented'); }
                return unsupported && frame.textContent === 'old';
            })()
            """, Cancellation).Boolean);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("document.body.innerHTML='<b>new</b>'", canceled.Token));
        Assert.Same(frame, document.Body.FirstChild);
        Assert.Same(oldText, frame.FirstChild);
        Assert.Equal("old", Assert.IsType<DomText>(oldText).Data);
    }

    [Fact]
    public void InputAndAttributeLimitsFailBeforeReplacingExistingChildren()
    {
        var document = Document();
        var body = document.Body!;
        var old = document.CreateTextNode("old");
        body.AppendChild(old);
        using var host = new V8ScriptHost(document: document);
        var tooManyAttributes = "<i " + string.Join(" ", Enumerable.Range(0, 129).Select(i => $"a{i}=x")) + ">new</i>";
        host.ExecuteClassic("let failed=false;try{document.body.innerHTML="
            + System.Text.Json.JsonSerializer.Serialize(tooManyAttributes)
            + ";}catch(e){failed=e instanceof TypeError}", Cancellation);
        Assert.True(host.Evaluate("failed&&document.body.innerHTML==='old'", Cancellation).Boolean);
        Assert.Same(old, body.FirstChild);

        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("document.body.innerHTML='x'.repeat(65537)", Cancellation));
        Assert.Same(old, body.FirstChild);
        Assert.Equal("old", old.Data);
    }
}
