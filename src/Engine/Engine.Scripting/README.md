# Engine.Scripting

Phase-11a ClearScript/V8 native host with one thread-owned isolate/context per
instance, bounded source and copied primitives, heap/stack/ArrayBuffer settings,
deadline/cancellation interruption and invalidated-context refusal.
No CLR objects or browser/DOM bindings are installed. The static browser still
does not execute page scripts; HTML scheduling, Web IDL and event loops are deferred.
Per-tab V8 isolates supplement, not replace, renderer process confinement.
See the [scripting guide](../../../docs/scripting.md) for exact scope and deployment.

Official document IDs: `ecmascript`, `webidl`, and `html` for the event loop.
Native API references: `clearscript-v8` and `clearscript-v8-constraints`.
See the [standards workflow](../../../docs/standards.md) and
[architecture](../../../docs/architecture/overview.md).
