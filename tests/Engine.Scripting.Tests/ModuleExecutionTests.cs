using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class ModuleExecutionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static DomDocument Document()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        html.AppendChild(document.CreateElement("head")); html.AppendChild(document.CreateElement("body"));
        return document;
    }

    private static V8ScriptHost CreateHost(DomDocument? document = null, TimeSpan? timeout = null)
        => new(timeout, document ?? Document(), enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);

    [Fact]
    public void ModuleEvaluationReturnsPromiseForTopLevelAwait()
    {
        using var engine = new V8ScriptEngine("module-test", new V8RuntimeConstraints());
        using var module = engine.Compile(new DocumentInfo { Category = ModuleCategory.Standard }, """
            globalThis.moduleStarted = true;
            await Promise.resolve();
            globalThis.moduleFinished = true;
            """);

        using var result = Assert.IsAssignableFrom<ScriptObject>(engine.Evaluate(module));
        using var observe = (ScriptObject)engine.Evaluate("""
            (() => {
                const then = Promise.prototype.then;
                return promise => {
                    let state = 0;
                    then.call(promise, () => { state = 1; }, () => { state = 2; });
                    return () => state;
                };
            })()
            """);
        using var readState = Assert.IsAssignableFrom<ScriptObject>(observe.InvokeAsFunction(result));
        Assert.Equal(1, readState.InvokeAsFunction());
        Assert.True((bool)engine.Evaluate("globalThis.moduleFinished"));
    }

    [Fact]
    public void ParserModulesAwaitCompletionBeforeReadinessEvents()
    {
        using var host = CreateHost();
        var parserFinished = false;
        host.ExecuteInitialDocumentParserBatch((executeClassic, queueModule, token) =>
        {
            executeClassic("globalThis.order=['classic'];document.addEventListener('DOMContentLoaded',()=>order.push('DOMContentLoaded'));");
            executeClassic("Promise.prototype.then=()=>{throw Error('tampered then')};");
            queueModule("if(document.readyState!=='interactive')throw Error('readiness');order.push('module-start');await Promise.resolve();order.push('module-end');");
            parserFinished = true;
        }, Cancellation);

        Assert.True(parserFinished);
        Assert.Equal("classic,module-start,module-end,DOMContentLoaded",
            host.Evaluate("order.join(',')", Cancellation).Text);
        Assert.Equal("complete", host.Evaluate("document.readyState", Cancellation).Text);
    }

    [Fact]
    public void RejectedTopLevelAwaitFailsInitialDocumentExecution()
    {
        using var host = CreateHost();
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentParserBatch(
            (_, queueModule, _) => queueModule("await Promise.reject(new Error('fixture'));"),
            Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void UnsettledTopLevelAwaitUsesTheSharedDeadline()
    {
        using var host = CreateHost(timeout: TimeSpan.FromMilliseconds(100));
        Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentParserBatch(
            (_, queueModule, _) => queueModule("await new Promise(()=>{});"),
            Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void UnsettledTopLevelAwaitHonorsCallerCancellation()
    {
        using var host = CreateHost();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteInitialDocumentParserBatch(
            (_, queueModule, _) => queueModule("await new Promise(()=>{});"),
            cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Theory]
    [InlineData("import value from './dependency.js';")]
    [InlineData("await import('data:text/javascript,globalThis.moduleImportRan=true');")]
    public void ModuleImportsFailWithoutAResourceLoader(string source)
    {
        using var host = CreateHost();
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteInitialDocumentParserBatch(
            (_, queueModule, _) => queueModule(source),
            Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
    }

    [Fact]
    public void ClassicAndModuleSourcesShareTheScriptCountLimit()
    {
        using var host = CreateHost();
        Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentParserBatch((executeClassic, queueModule, _) =>
        {
            for (var index = 0; index < V8ScriptHost.MaxBatchScripts - 1; index++) { executeClassic("0;"); }
            queueModule("0;");
            queueModule("0;");
        }, Cancellation));
    }

    [Fact]
    public void ClassicAndModuleSourcesShareTheAggregateCharacterLimit()
    {
        using var host = CreateHost();
        Assert.Throws<ScriptLimitException>(() => host.ExecuteInitialDocumentParserBatch((executeClassic, queueModule, _) =>
        {
            for (var index = 0; index < 3; index++)
            {
                executeClassic(new string(' ', V8ScriptHost.MaxSourceCharacters));
            }
            queueModule(new string(' ', V8ScriptHost.MaxSourceCharacters / 2));
            queueModule(new string(' ', V8ScriptHost.MaxSourceCharacters / 2));
            queueModule(" ");
        }, Cancellation));
    }
}
