using System.Diagnostics;
using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class HostTests
{
    [Theory]
    [InlineData("undefined", ScriptValueKind.Undefined)]
    [InlineData("null", ScriptValueKind.Null)]
    [InlineData("true", ScriptValueKind.Boolean)]
    [InlineData("21 * 2", ScriptValueKind.Number)]
    [InlineData("'hello'", ScriptValueKind.String)]
    [InlineData("9007199254740993n", ScriptValueKind.BigInt)]
    public void CopiesExactPrimitiveResults(string source, ScriptValueKind kind)
    {
        using var host = new V8ScriptHost();
        var result = host.Evaluate(source, TestContext.Current.CancellationToken);
        Assert.Equal(kind, result.Kind);
        if (kind == ScriptValueKind.Number) { Assert.Equal(42, result.Number); }
        if (kind == ScriptValueKind.Boolean) { Assert.True(result.Boolean); }
        if (kind == ScriptValueKind.String) { Assert.Equal("hello", result.Text); }
        if (kind == ScriptValueKind.BigInt) { Assert.Equal("9007199254740993", result.Text); }
    }
    [Fact]
    public void IsolatesRetainOnlyTheirOwnStateWithoutClrOrBrowserGlobals()
    {
        using var first = new V8ScriptHost();
        using var second = new V8ScriptHost();
        first.Evaluate("globalThis.secret = 42", TestContext.Current.CancellationToken);
        Assert.Equal(42, first.Evaluate("secret", TestContext.Current.CancellationToken).Number);
        Assert.Equal(ScriptValueKind.Undefined, second.Evaluate("globalThis.secret", TestContext.Current.CancellationToken).Kind);
        Assert.Equal("undefined,undefined,undefined,undefined,undefined,undefined,undefined",
            first.Evaluate("[typeof host, typeof clr, typeof System, typeof document, typeof fetch, typeof setTimeout, typeof require].join(',')", TestContext.Current.CancellationToken).Text);
    }
    [Theory]
    [InlineData("({a:1})")]
    [InlineData("() => 1")]
    [InlineData("Symbol('test')")]
    [InlineData("Promise.resolve(1)")]
    public void NativeObjectsNeverEscapeTheHost(string source)
    {
        using var host = new V8ScriptHost();
        Assert.Throws<UnsupportedScriptValueException>(() => host.Evaluate(source, TestContext.Current.CancellationToken));
        Assert.Equal(42, host.Evaluate("42", TestContext.Current.CancellationToken).Number);
    }
    [Fact]
    public void PreservesNonFiniteNumbersAndNegativeZero()
    {
        using var host = new V8ScriptHost();
        Assert.True(double.IsNaN(host.Evaluate("NaN", TestContext.Current.CancellationToken).Number));
        Assert.Equal(double.PositiveInfinity, host.Evaluate("Infinity", TestContext.Current.CancellationToken).Number);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(host.Evaluate("-0", TestContext.Current.CancellationToken).Number));
    }
    [Fact]
    public void SourceAndResultBudgetsHaveExactBoundaries()
    {
        using var host = new V8ScriptHost();
        Assert.Equal(1, host.Evaluate("1" + new string(' ', V8ScriptHost.MaxSourceCharacters - 1), TestContext.Current.CancellationToken).Number);
        Assert.Throws<ScriptLimitException>(() => host.Evaluate(new string(' ', V8ScriptHost.MaxSourceCharacters + 1), TestContext.Current.CancellationToken));
        Assert.Equal(V8ScriptHost.MaxResultCharacters, host.Evaluate($"'x'.repeat({V8ScriptHost.MaxResultCharacters})", TestContext.Current.CancellationToken).Text!.Length);
        Assert.Throws<ScriptLimitException>(() => host.Evaluate($"'x'.repeat({V8ScriptHost.MaxResultCharacters + 1})", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void SyntaxAndScriptErrorsAreExplicitAndDoNotPoisonNormalContexts()
    {
        using var host = new V8ScriptHost();
        Assert.Throws<ScriptExecutionException>(() => host.Evaluate("const =", TestContext.Current.CancellationToken));
        Assert.Contains("fixture", Assert.Throws<ScriptExecutionException>(() => host.Evaluate("throw new Error('fixture')", TestContext.Current.CancellationToken)).Message);
        Assert.Equal(42, host.Evaluate("42", TestContext.Current.CancellationToken).Number);
    }
    [Fact]
    public void DeadlineInterruptsInfiniteExecutionAndInvalidatesTheContext()
    {
        using var host = new V8ScriptHost(TimeSpan.FromMilliseconds(150));
        var timer = Stopwatch.StartNew();
        Assert.Contains("deadline", Assert.Throws<ScriptLimitException>(() => host.Evaluate("while(true) {}", TestContext.Current.CancellationToken)).Message);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", TestContext.Current.CancellationToken));
        using var fresh = new V8ScriptHost();
        Assert.Equal(42, fresh.Evaluate("42", TestContext.Current.CancellationToken).Number);
    }
    [Fact]
    public void CancellationInterruptsRunningScriptButPreCanceledCallsDoNotPoison()
    {
        using var host = new V8ScriptHost();
        using var preCanceled = new CancellationTokenSource();
        preCanceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => host.Evaluate("42", preCanceled.Token));
        Assert.Equal(42, host.Evaluate("42", TestContext.Current.CancellationToken).Number);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        Assert.ThrowsAny<OperationCanceledException>(() => host.Evaluate("while(true) {}", cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => host.Evaluate("42", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void StackAndExternalArrayBufferLimitsFailExplicitly()
    {
        using var host = new V8ScriptHost();
        Assert.Throws<ScriptExecutionException>(() => host.Evaluate("function recurse(){return recurse()} recurse()", TestContext.Current.CancellationToken));
        Assert.Throws<ScriptExecutionException>(() => host.Evaluate($"new ArrayBuffer({V8ScriptHost.MaxArrayBufferBytes + 1})", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void OwnershipAndDisposalAreThreadAffine()
    {
        var host = new V8ScriptHost();
        Exception? evaluation = null; Exception? disposal = null;
        var thread = new Thread(() =>
        {
            evaluation = Record.Exception(() => host.Evaluate("42", TestContext.Current.CancellationToken));
            disposal = Record.Exception(host.Dispose);
        });
        thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(evaluation); Assert.IsType<InvalidOperationException>(disposal);
        Assert.Equal(42, host.Evaluate("42", TestContext.Current.CancellationToken).Number);
        host.Dispose(); host.Dispose();
        Assert.Throws<ObjectDisposedException>(() => host.Evaluate("42", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void PrimitiveCopyingSurvivesGlobalIntrinsicReplacement()
    {
        using var host = new V8ScriptHost();
        host.Evaluate("globalThis.eval = globalThis.String = Object.is = () => 'tampered'; undefined", TestContext.Current.CancellationToken);
        Assert.Equal(42, host.Evaluate("42", TestContext.Current.CancellationToken).Number);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(host.Evaluate("-0", TestContext.Current.CancellationToken).Number));
        Assert.Equal("9007199254740993", host.Evaluate("9007199254740993n", TestContext.Current.CancellationToken).Text);
    }
    [Fact]
    public void BlockingSharedMemoryAndWasmAreNotExposed()
    {
        using var host = new V8ScriptHost();
        Assert.Equal("undefined,undefined,undefined",
            host.Evaluate("[typeof SharedArrayBuffer, typeof Atomics, typeof WebAssembly].join(',')", TestContext.Current.CancellationToken).Text);
        Assert.Throws<ScriptExecutionException>(() => host.Evaluate("import value from 'file:///host-secret';", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void BigIntAndStringCopyingHaveExactShapesAndLimits()
    {
        using var host = new V8ScriptHost();
        Assert.Equal("string:injected\0\uD800", host.Evaluate("'string:injected\\0\\uD800'", TestContext.Current.CancellationToken).Text);
        Assert.Equal(V8ScriptHost.MaxResultCharacters,
            host.Evaluate("10n ** 16383n", TestContext.Current.CancellationToken).Text!.Length);
        Assert.Throws<ScriptLimitException>(() => host.Evaluate("10n ** 16384n", TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30001)]
    public void InvalidDeadlineLimitsFailBeforeNativeStartup(int milliseconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new V8ScriptHost(TimeSpan.FromMilliseconds(milliseconds)));
}
