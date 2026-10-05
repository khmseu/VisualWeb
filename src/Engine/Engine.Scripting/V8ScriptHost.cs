using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace VisualWeb.Engine.Scripting;

/// <summary>One owning thread's private V8 isolate and context, with no CLR or browser bindings.</summary>
/// <remarks>References: clearscript-v8; <see href="https://github.com/microsoft/ClearScript">ClearScript</see>.
/// Spec: ecmascript; <see href="https://tc39.es/ecma262/#sec-ecmascript-language-scripts-and-modules">scripts</see>.
/// Deadlines and heap monitoring supplement, not replace, renderer OS resource limits.</remarks>
public sealed class V8ScriptHost : IDisposable
{
    public const int MaxSourceCharacters = 64 * 1024;
    public const int MaxResultCharacters = 16 * 1024;
    public const int MaxBatchScripts = 64;
    public const int MaxBatchSourceCharacters = 256 * 1024;
    public const ulong MaxArrayBufferBytes = 16 * 1024 * 1024;
    public const ulong MonitoredHeapBytes = 32 * 1024 * 1024;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private readonly V8ScriptEngine engine;
    private readonly ScriptObject evaluate;
    private readonly TimeSpan timeout;
    private bool poisoned;
    private bool disposed;

    public V8ScriptHost(TimeSpan? timeout = null)
    {
        this.timeout = timeout ?? TimeSpan.FromSeconds(2);
        if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromSeconds(30)) { throw new ArgumentOutOfRangeException(nameof(timeout)); }
        if (!(OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            throw new PlatformNotSupportedException("The V8 host requires Linux or Windows x64/arm64 native assets.");
        }
        engine = new V8ScriptEngine("VisualWeb", new V8RuntimeConstraints
        {
            MaxNewSpaceSize = 16,
            MaxOldSpaceSize = 128,
            HeapExpansionMultiplier = 1,
            MaxArrayBufferAllocation = MaxArrayBufferBytes
        }, V8ScriptEngineFlags.DisableGlobalMembers);
        try
        {
            engine.AllowReflection = false;
            engine.DocumentSettings.AccessFlags = DocumentAccessFlags.None;
            engine.MaxRuntimeHeapSize = new UIntPtr(MonitoredHeapBytes);
            engine.RuntimeHeapSizeSampleInterval = TimeSpan.FromMilliseconds(10);
            engine.RuntimeHeapSizeViolationPolicy = V8RuntimeViolationPolicy.Interrupt;
            engine.MaxRuntimeStackUsage = new UIntPtr(512 * 1024);
            engine.EnableRuntimeInterruptPropagation = true;
            engine.Execute("delete globalThis.SharedArrayBuffer; delete globalThis.Atomics; delete globalThis.WebAssembly;");
            // Capture pristine intrinsics before scripts can replace global functions.
            evaluate = (ScriptObject)engine.Evaluate("""
                (() => {
                    const run = eval, text = String, same = Object.is;
                    return source => {
                        const value = run(source);
                        if (value === null) return 'null:';
                        const type = typeof value;
                        if (type === 'undefined') return 'undefined:';
                        if (type === 'boolean') return value ? 'boolean:true' : 'boolean:false';
                        if (type === 'number') return 'number:' + (same(value, -0) ? '-0' : text(value));
                        if (type === 'string' || type === 'bigint') {
                            const result = text(value);
                            return result.length > 16384 ? 'limit:' : type + ':' + result;
                        }
                        return 'unsupported:';
                    };
                })()
                """);
        }
        catch { engine.Dispose(); throw; }
    }

    public ScriptValue Evaluate(string source, CancellationToken cancellationToken = default)
    {
        CheckUsable();
        ValidateSource(source);
        return Run(() => Copy(evaluate.InvokeAsFunction(source)), cancellationToken);
    }

    /// <summary>Execute one classic script in the context's persistent global environment.</summary>
    /// <remarks>Spec: ecmascript; <see href="https://tc39.es/ecma262/#sec-scriptevaluation">ScriptEvaluation</see>.
    /// Completion values are deliberately discarded; use Evaluate for copied primitive observations.</remarks>
    public void ExecuteClassic(string source, CancellationToken cancellationToken = default)
    {
        CheckUsable();
        ValidateSource(source);
        Run(() => { engine.Execute(source); return 0; }, cancellationToken);
    }

    /// <summary>Execute a prevalidated snapshot of classic scripts in order under one deadline.</summary>
    /// <remarks>Spec: ecmascript; <see href="https://tc39.es/ecma262/#sec-scriptevaluation">ScriptEvaluation</see>.
    /// Stops at the first error; earlier side effects are not rolled back. This is not HTML scheduling.</remarks>
    public void ExecuteClassicBatch(IReadOnlyList<string> sources, CancellationToken cancellationToken = default)
    {
        CheckUsable();
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count > MaxBatchScripts) { throw new ScriptLimitException("Classic-script batch count limit exceeded."); }
        var snapshot = new string[sources.Count];
        var characters = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sources[index];
            ValidateSource(source);
            characters = checked(characters + source.Length);
            if (characters > MaxBatchSourceCharacters) { throw new ScriptLimitException("Classic-script batch character limit exceeded."); }
            snapshot[index] = source;
        }
        Run(() =>
        {
            foreach (var source in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                engine.Execute(source);
            }
            return 0;
        }, cancellationToken);
    }

    private static void ValidateSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > MaxSourceCharacters) { throw new ScriptLimitException("Script source character limit exceeded."); }
    }

    private T Run<T>(Func<T> execute, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var interrupt = deadline.Token.Register(engine.Interrupt);
        deadline.CancelAfter(timeout);
        try
        {
            T result;
            try { result = execute(); }
            finally
            {
                // Drain an in-flight interrupt before returning or preserving a nonfatal-error context.
                deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                interrupt.Dispose();
            }
            deadline.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception exception) when (exception is ScriptInterruptedException or OperationCanceledException)
        {
            poisoned = true;
            if (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("V8 execution canceled; isolate invalidated.", exception, cancellationToken); }
            throw new ScriptLimitException(deadline.IsCancellationRequested
                ? "V8 execution deadline exceeded; isolate invalidated."
                : "V8 execution interrupted by a resource limit; isolate invalidated.", exception);
        }
        catch (ScriptEngineException exception)
        {
            if (deadline.IsCancellationRequested)
            {
                poisoned = true;
                if (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException("V8 execution canceled; isolate invalidated.", exception, cancellationToken); }
                throw new ScriptLimitException("V8 execution deadline exceeded; isolate invalidated.", exception);
            }
            if (exception.IsFatal) { poisoned = true; }
            throw new ScriptExecutionException("V8 execution failed: " + Bounded(exception.Message), exception);
        }
    }

    private static ScriptValue Copy(object? value)
    {
        if (value is not string copied) { throw new InvalidOperationException("V8 primitive copier returned an invalid value."); }
        var separator = copied.IndexOf(':');
        if (separator < 0) { throw new InvalidOperationException("V8 primitive copier returned an invalid tag."); }
        var text = copied[(separator + 1)..];
        return copied[..separator] switch
        {
            "undefined" => new(ScriptValueKind.Undefined),
            "null" => new(ScriptValueKind.Null),
            "boolean" => new(ScriptValueKind.Boolean, Boolean: text == "true"),
            "number" => new(ScriptValueKind.Number, Number: text switch
            {
                "NaN" => double.NaN,
                "Infinity" => double.PositiveInfinity,
                "-Infinity" => double.NegativeInfinity,
                "-0" => BitConverter.Int64BitsToDouble(long.MinValue),
                _ => double.Parse(text, CultureInfo.InvariantCulture)
            }),
            "string" => new(ScriptValueKind.String, Text: text),
            "bigint" => new(ScriptValueKind.BigInt, Text: text),
            "limit" => throw new ScriptLimitException("Script result character limit exceeded."),
            "unsupported" => throw new UnsupportedScriptValueException("Only copied primitives may leave the V8 context; objects, functions and symbols are unsupported."),
            _ => throw new InvalidOperationException("V8 primitive copier returned an unknown tag.")
        };
    }

    private static string Bounded(string text) => text.Length <= MaxResultCharacters ? text : text[..MaxResultCharacters];
    private void Check()
    {
        if (thread != Environment.CurrentManagedThreadId) { throw new InvalidOperationException("V8 host must stay on its creating thread."); }
        ObjectDisposedException.ThrowIf(disposed, this);
    }
    private void CheckUsable()
    {
        Check();
        if (poisoned) { throw new InvalidOperationException("Interrupted V8 host is invalid; dispose it and create a fresh isolate."); }
    }
    public void Dispose()
    {
        if (disposed) { return; }
        Check();
        disposed = true;
        try { evaluate.Dispose(); }
        finally { engine.Dispose(); }
    }
}
