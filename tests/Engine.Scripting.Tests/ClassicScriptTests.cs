using System.Diagnostics;
using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class ClassicScriptTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ClassicScriptsRetainGlobalLexicalBindingsWithoutCreatingGlobalProperties()
    {
        using var host = new V8ScriptHost();
        host.ExecuteClassic("let count = 1; const limit = 42; class Counter {} var exposed = 7;", Cancellation);
        host.ExecuteClassic("count += limit; globalThis.isCounter = new Counter() instanceof Counter;", Cancellation);
        Assert.Equal(43, host.Evaluate("count", Cancellation).Number);
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.count", Cancellation).Kind);
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.limit", Cancellation).Kind);
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.Counter", Cancellation).Kind);
        Assert.Equal(7, host.Evaluate("globalThis.exposed", Cancellation).Number);
        Assert.True(host.Evaluate("isCounter", Cancellation).Boolean);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("let count = 9;", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("limit = 9;", Cancellation));
        Assert.Equal(43, host.Evaluate("count", Cancellation).Number);
    }

    [Fact]
    public void BatchOrderingUsesOnePersistentEnvironmentAndDiscardsCompletionObjects()
    {
        using var host = new V8ScriptHost();
        host.ExecuteClassicBatch([
            "let order = []; function append(value) { order.push(value); } ({native:'discarded'})",
            "append('first'); let value = 20;",
            "append('second'); value += 22; Symbol('discarded')",
            "append(value);"
        ], Cancellation);
        Assert.Equal("first,second,42", host.Evaluate("order.join(',')", Cancellation).Text);
        host.ExecuteClassic("value++;", Cancellation);
        Assert.Equal(43, host.Evaluate("value", Cancellation).Number);
    }

    [Fact]
    public void SeparateHostsHaveSeparateClassicLexicalEnvironments()
    {
        using var first = new V8ScriptHost();
        using var second = new V8ScriptHost();
        first.ExecuteClassic("let privateValue = 42;", Cancellation);
        second.ExecuteClassic("let privateValue = 7;", Cancellation);
        Assert.Equal(42, first.Evaluate("privateValue", Cancellation).Number);
        Assert.Equal(7, second.Evaluate("privateValue", Cancellation).Number);
    }

    [Theory]
    [InlineData("throw new Error('batch fixture');")]
    [InlineData("let = ;")]
    [InlineData("let prefix = 0;")]
    public void FirstScriptErrorStopsBatchWithoutRollingBackEarlierEffects(string failure)
    {
        using var host = new V8ScriptHost();
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassicBatch([
            "let prefix = 1; globalThis.tailRan = false;",
            failure,
            "tailRan = true; prefix++;"
        ], Cancellation));
        Assert.Equal(1, host.Evaluate("prefix", Cancellation).Number);
        Assert.False(host.Evaluate("tailRan", Cancellation).Boolean);
        host.ExecuteClassic("prefix++;", Cancellation);
        Assert.Equal(2, host.Evaluate("prefix", Cancellation).Number);
    }

    [Fact]
    public void BatchLimitsAreExactAndAllSourcesAreValidatedBeforeExecution()
    {
        using var host = new V8ScriptHost();
        host.ExecuteClassicBatch(Enumerable.Repeat("", V8ScriptHost.MaxBatchScripts).ToArray(), Cancellation);
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassicBatch(
            Enumerable.Repeat("", V8ScriptHost.MaxBatchScripts + 1).ToArray(), Cancellation));
        var exact = Enumerable.Repeat(new string(' ', V8ScriptHost.MaxSourceCharacters), 4).ToArray();
        host.ExecuteClassicBatch(exact, Cancellation);
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassicBatch([.. exact, " "], Cancellation));
        Assert.Throws<ScriptLimitException>(() => host.ExecuteClassicBatch([
            "globalThis.mustNotRun = true;",
            new string(' ', V8ScriptHost.MaxSourceCharacters + 1)
        ], Cancellation));
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.mustNotRun", Cancellation).Kind);
        Assert.Throws<ArgumentNullException>(() => host.ExecuteClassicBatch(["globalThis.mustNotRun = true;", null!], Cancellation));
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.mustNotRun", Cancellation).Kind);
        Assert.Throws<ArgumentNullException>(() => host.ExecuteClassicBatch(null!, Cancellation));
        Assert.Throws<ArgumentNullException>(() => host.ExecuteClassic(null!, Cancellation));
        host.ExecuteClassicBatch([], Cancellation);
    }

    [Fact]
    public void WholeBatchDeadlineInterruptsAndInvalidatesEveryExecutionEntryPoint()
    {
        using var host = new V8ScriptHost(TimeSpan.FromMilliseconds(200));
        var timer = Stopwatch.StartNew();
        Assert.Contains("deadline", Assert.Throws<ScriptLimitException>(() => host.ExecuteClassicBatch([
            "let begin = Date.now(); while (Date.now() - begin < 140) {}",
            "begin = Date.now(); while (Date.now() - begin < 140) {}",
            "globalThis.mustNotRun = true;"
        ], Cancellation)).Message);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.ExecuteClassic("42", Cancellation));
        Assert.Throws<InvalidOperationException>(() => host.ExecuteClassicBatch([], Cancellation));
    }

    [Fact]
    public void CancellationInterruptsClassicScriptAndPreCanceledBatchHasNoEffects()
    {
        using var host = new V8ScriptHost();
        using var preCanceled = new CancellationTokenSource();
        preCanceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassicBatch(["globalThis.mustNotRun = true;"], preCanceled.Token));
        Assert.Equal(ScriptValueKind.Undefined, host.Evaluate("globalThis.mustNotRun", Cancellation).Kind);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.ExecuteClassic("while(true) {}", cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => host.ExecuteClassic("42", Cancellation));
    }

    [Fact]
    public void GlobalIntrinsicReplacementDoesNotChangeClassicExecutionOrPrimitiveCopying()
    {
        using var host = new V8ScriptHost();
        host.ExecuteClassic("let state = 1; globalThis.eval = globalThis.String = Object.is = () => 'tampered';", Cancellation);
        host.ExecuteClassic("state += 41;", Cancellation);
        Assert.Equal(42, host.Evaluate("state", Cancellation).Number);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(host.Evaluate("-0", Cancellation).Number));
    }

    [Fact]
    public void ExecutionAndBatchesPreserveThreadOwnershipAndDisposal()
    {
        var host = new V8ScriptHost();
        Exception? single = null; Exception? batch = null;
        var cancellation = Cancellation;
        var thread = new Thread(() =>
        {
            single = Record.Exception(() => host.ExecuteClassic("42", cancellation));
            batch = Record.Exception(() => host.ExecuteClassicBatch(["42"], cancellation));
        });
        thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(single); Assert.IsType<InvalidOperationException>(batch);
        host.Dispose();
        Assert.Throws<ObjectDisposedException>(() => host.ExecuteClassic("42", Cancellation));
        Assert.Throws<ObjectDisposedException>(() => host.ExecuteClassicBatch([], Cancellation));
    }

    [Fact]
    public void StrictClassicDeclarationsAndTemporalDeadZonesFollowNativeGlobalSemantics()
    {
        using var host = new V8ScriptHost();
        host.ExecuteClassic("'use strict'; var strictGlobal = 42; let lexical = 7;", Cancellation);
        Assert.Equal(42, host.Evaluate("strictGlobal", Cancellation).Number);
        Assert.Equal(7, host.Evaluate("lexical", Cancellation).Number);
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("uninitialized; let uninitialized = 1;", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.Evaluate("uninitialized", Cancellation));
        Assert.Throws<ScriptExecutionException>(() => host.ExecuteClassic("let uninitialized = 2;", Cancellation));
        host.ExecuteClassic("lexical++;", Cancellation);
        Assert.Equal(8, host.Evaluate("lexical", Cancellation).Number);
    }

    [Fact]
    public void ValidatedBatchIsASnapshotRatherThanRereadingMutableCallerSources()
    {
        using var host = new V8ScriptHost();
        var sources = new MutatingSources();
        host.ExecuteClassicBatch(sources, Cancellation);
        Assert.True(sources.Mutated);
        Assert.Equal(42, host.Evaluate("snapshot", Cancellation).Number);
    }

    private sealed class MutatingSources : IReadOnlyList<string>
    {
        private string first = "let snapshot = 20;";
        public bool Mutated { get; private set; }
        public int Count => 2;
        public string this[int index]
        {
            get
            {
                if (index == 0) { return first; }
                if (index != 1) { throw new ArgumentOutOfRangeException(nameof(index)); }
                first = "throw new Error('sources reread');";
                Mutated = true;
                return "snapshot += 22;";
            }
        }
        public IEnumerator<string> GetEnumerator() => throw new NotSupportedException("Snapshot uses indexed bounded access.");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
