# Engine.Scripting

Phase-11a ClearScript/V8 native host with one thread-owned isolate/context per
instance, bounded source and copied primitives, heap/stack/ArrayBuffer settings,
deadline/cancellation interruption and invalidated-context refusal.
Phase 11b adds classic execution with native persistent global lexical state
and prevalidated ordered batches sharing one deadline. Primitive observations
remain a separate indirect-eval operation. No HTML script scheduling is implied.
Plain hosts install no CLR objects or DOM bindings. Optional phase-11c live
document.title/getElementById/element textContent facades use one privately
captured primitive-only delegate, never CLR node/type/exception exposure.
Phase 11e extends this bridge with bounded attributes, element/text/fragment
creation, document roots and branded Node navigation/mutation. Native checked
algorithms preserve tree invariants; all non-document wrappers share one
lifetime identity budget. Attribute/tree/resource failures precede mutation.
The static browser still
executes no page scripts by default. PageRendering's phase-11d opt-in composes
these APIs for post-parse inline classics, not HTML scheduling. Full Web IDL
and event loops remain deferred. Phase 11f optionally installs bounded native
queueMicrotask with Promise FIFO ordering and verified per-script checkpoints.
One execution shares its deadline/DOM budget across all callbacks; task failures
invalidate the host. Inline pages enable it under the existing script opt-in,
then dispose the host before paint. Persistent event loops/rejection reporting
remain deferred.
Per-tab V8 isolates supplement, not replace, renderer process confinement.
See the [scripting guide](../../../docs/scripting.md) for exact scope and deployment.

Official document IDs: `ecmascript`, `webidl`, and `html` for the event loop.
Native API references: `clearscript-v8` and `clearscript-v8-constraints`.
Binding sources: `dom` for lookup/textContent, attributes, factories and mutations,
and `html`/`webidl` for title
and string conversion. Full prototype/constructor bindings remain deferred.
See the [standards workflow](../../../docs/standards.md) and
[architecture](../../../docs/architecture/overview.md).
