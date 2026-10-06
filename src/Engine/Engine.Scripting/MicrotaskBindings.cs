namespace VisualWeb.Engine.Scripting;

/// <summary>Private queueMicrotask adapter on V8's native Promise job queue.</summary>
/// <remarks>Specs: html, webidl;
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#microtask-queuing">microtask queuing</see>.
/// ClearScript's native entry-point return drains Promise jobs; no managed callbacks are exposed.</remarks>
internal static class MicrotaskBindings
{
    internal const string Bootstrap = """
        (() => {
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const then = Promise.prototype.then, TypeErrorCtor = TypeError;
            const resolved = Promise.resolve();
            Object.defineProperty(resolved, 'constructor', {value: undefined});
            let active = false, pending = 0, queued = 0, executed = 0, failure = 0;
            Object.defineProperty(globalThis, 'queueMicrotask', {
                enumerable: true, configurable: true, writable: true,
                value: function(callback) {
                    if (arguments.length === 0 || typeof callback !== 'function')
                        throw new TypeErrorCtor('queueMicrotask requires a callable argument');
                    if (!active || failure) throw new TypeErrorCtor('Microtask task is unavailable');
                    if (pending >= 1024 || queued >= 4096) {
                        failure = 2;
                        throw new TypeErrorCtor('queueMicrotask resource limit exceeded');
                    }
                    ++pending; ++queued;
                    apply(then, resolved, () => {
                        --pending;
                        if (!active || failure) return;
                        if (++executed > 4096) { failure = 2; return; }
                        try { callback(); }
                        catch { if (!failure) failure = 1; }
                    });
                }
            });
            return operation => {
                if (operation === 'begin') {
                    if (pending !== 0) return 3;
                    queued = executed = failure = 0; active = true;
                    return 0;
                }
                if (operation === 'end') active = false;
                return failure || (pending !== 0 ? 3 : 0);
            };
        })()
        """;
}
