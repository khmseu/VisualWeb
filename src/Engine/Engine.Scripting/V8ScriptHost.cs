using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Scripting;

/// <summary>One thread's private V8 context, optionally with minimal primitive-only DOM facades.</summary>
/// <remarks>References: clearscript-v8; <see href="https://github.com/microsoft/ClearScript">ClearScript</see>.
/// Spec: ecmascript; <see href="https://tc39.es/ecma262/#sec-ecmascript-language-scripts-and-modules">scripts</see>.
/// Deadlines and heap monitoring supplement, not replace, renderer OS resource limits.</remarks>
public sealed class V8ScriptHost : IDisposable
{
    private long executionStarted;
    public const int MaxSourceCharacters = 64 * 1024;
    public const int MaxResultCharacters = 16 * 1024;
    public const int MaxBatchScripts = 64;
    public const int MaxBatchSourceCharacters = 256 * 1024;
    public const int MaxPendingMicrotasks = 1024;
    public const int MaxMicrotasksPerExecution = 4096;
    public const int MaxEventListeners = 1024;
    public const int MaxEventInvocationsPerExecution = 4096;
    public const int MaxNestedEventDispatches = 32;
    public const ulong MaxArrayBufferBytes = 16 * 1024 * 1024;
    public const ulong MonitoredHeapBytes = 32 * 1024 * 1024;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private readonly V8ScriptEngine engine;
    private readonly ScriptObject evaluate;
    private readonly TimeSpan timeout;
    private readonly DomBindings? dom;
    private readonly ScriptObject? microtasks;
    private readonly ScriptObject? events;
    private readonly bool documentLifecycle;
    private bool initialDocumentExecuted;
    private bool poisoned;
    private bool disposed;

    public V8ScriptHost(TimeSpan? timeout = null, DomDocument? document = null, bool enableMicrotasks = false,
        bool enableEvents = false, bool enableDocumentLifecycle = false)
    {
        if (enableDocumentLifecycle && (document is null || !enableEvents || !enableMicrotasks))
        { throw new ArgumentException("Document lifecycle requires a bound document, events and microtasks."); }
        documentLifecycle = enableDocumentLifecycle;
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
            if (enableEvents) { events = (ScriptObject)engine.Evaluate(EventBindings.Bootstrap); }
            if (document is not null)
            {
                dom = new(document);
                engine.AddHostObject("__visualwebDom", new Func<string, int, int, int, string, string, string>(dom.Invoke));
                using var installDom = (ScriptObject)engine.Evaluate(DomBindings.Bootstrap);
                using var installClasses = (ScriptObject)engine.Evaluate(ClassTokenBindings.Bootstrap);
                installDom.InvokeAsFunction(events, documentLifecycle, installClasses);
            }
            if (enableMicrotasks) { microtasks = (ScriptObject)engine.Evaluate(MicrotaskBindings.Bootstrap); }
        }
        catch { dom?.Dispose(); engine.Dispose(); throw; }
    }

    public ScriptValue Evaluate(string source, CancellationToken cancellationToken = default)
    {
        CheckUsable();
        ValidateSource(source);
        return Copy(Run(() => evaluate.InvokeAsFunction(source), cancellationToken));
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
        => ExecuteBatch(sources, false, cancellationToken);

    /// <summary>Execute initial inline classics and finite document readiness events under one task budget.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/#the-end">the end</see>.
    /// Post-parse approximation only: no parser-blocking scripts, Window/load or persistent event loop.</remarks>
    public void ExecuteInitialDocumentBatch(IReadOnlyList<string> sources, CancellationToken cancellationToken = default)
    {
        CheckUsable();
        if (!documentLifecycle) { throw new InvalidOperationException("Document lifecycle was not enabled for this host."); }
        if (initialDocumentExecuted) { throw new InvalidOperationException("Initial document lifecycle has already executed."); }
        ExecuteBatch(sources, true, cancellationToken);
    }

    private void ExecuteBatch(IReadOnlyList<string> sources, bool completeDocument, CancellationToken cancellationToken)
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
            if (completeDocument) { initialDocumentExecuted = true; }
            foreach (var source in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckExecutionDeadline();
                engine.Execute(source);
                CheckEvents("checkpoint");
                CheckMicrotasks("checkpoint");
            }
            if (completeDocument)
            {
                foreach (var stage in new[] { "interactive", "dom-content-loaded", "complete" })
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CheckEvents(stage);
                    CheckEvents("checkpoint");
                    CheckMicrotasks("checkpoint");
                }
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
        executionStarted = Stopwatch.GetTimestamp();
        deadline.CancelAfter(timeout);
        dom?.Begin(deadline.Token);
        try
        {
            T result;
            try
            {
                CheckEvents("begin");
                CheckMicrotasks("begin");
                result = execute();
                CheckEvents("end");
                CheckMicrotasks("end");
            }
            catch (ScriptEngineException exception) when (!exception.IsFatal && !deadline.IsCancellationRequested)
            {
                CheckEvents("end");
                CheckMicrotasks("end");
                throw;
            }
            finally
            {
                // Drain an in-flight interrupt before returning or preserving a nonfatal-error context.
                deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                interrupt.Dispose();
            }
            deadline.Token.ThrowIfCancellationRequested();
            CheckExecutionDeadline();
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
            if (exception.IsFatal || microtasks is not null || events is not null) { poisoned = true; }
            throw new ScriptExecutionException("V8 execution failed: " + Bounded(exception.Message), exception);
        }
    }

    private void CheckExecutionDeadline()
    {
        // Timer callbacks can be delayed; never accept an expired finite task at a checkpoint.
        if (Stopwatch.GetElapsedTime(executionStarted) < timeout) { return; }
        poisoned = true;
        throw new ScriptLimitException("V8 execution deadline exceeded; isolate invalidated.");
    }

    private void CheckMicrotasks(string operation)
    {
        CheckExecutionDeadline();
        if (microtasks is null) { return; }
        var status = microtasks.InvokeAsFunction(operation);
        if (status is int code && code == 0) { return; }
        poisoned = true;
        if (status is int limit && limit == 2)
        { throw new ScriptLimitException("queueMicrotask resource limit exceeded; isolate invalidated."); }
        throw new ScriptExecutionException("queueMicrotask callback failed or native checkpoint remained pending; isolate invalidated.");
    }

    private void CheckEvents(string operation)
    {
        CheckExecutionDeadline();
        if (events is null) { return; }
        var status = events.InvokeAsFunction(operation);
        if (status is int code && code == 0) { return; }
        poisoned = true;
        if (status is int limit && limit == 2)
        { throw new ScriptLimitException("Event resource limit exceeded; isolate invalidated."); }
        CheckMicrotasks("checkpoint");
        throw new ScriptExecutionException("Event listener failed; isolate invalidated.");
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
        try
        {
            try
            {
                try { events?.Dispose(); }
                finally { microtasks?.Dispose(); }
            }
            finally { evaluate.Dispose(); }
        }
        finally
        {
            try { engine.Dispose(); }
            finally { dom?.Dispose(); }
        }
    }
}
