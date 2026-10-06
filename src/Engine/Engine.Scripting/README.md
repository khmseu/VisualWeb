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
Phase 11g optionally installs native Event/EventTarget and synthetic Node/
document dispatch with snapshotted propagation paths, listener identity/once/
passive semantics and shared invocation/depth limits. Callback errors and
caught quota violations invalidate tasks. No automatic browser events or
new CLR callbacks are installed; plain hosts remain unchanged.
Phase 11h optionally enables readonly document readiness and a one-shot
ExecuteInitialDocumentBatch with interactive/DOMContentLoaded/complete events,
private trusted dispatch and native checkpoints between stages. Requires
document/events/microtasks; all stages share the initial task budgets.
This finite post-parse approximation does not add Window/load or retain V8.
Phase 11i adds document/element/fragment queries, static branded NodeList
snapshots and Element matches/closest via Engine.Css's bounded selector matcher.
Result identities are preflighted atomically and native matching observes
task cancellation. Unsupported selector features fail explicitly.
Phase 11j adds reflected className and same-object live classList token facades,
with ordered-set mutation, indexed access/iteration, atomic validation and
shared callback/text/deadline budgets. It reuses the primitive attribute bridge;
full DOMTokenList WebIDL and DOMException objects remain deferred.
Phase 11k adds bounded contains/isSameNode/getRootNode, hasChildNodes,
parentElement and element-only child/sibling navigation. Live results preserve
wrapper identity, ownership and shared task budgets; shadow DOM and live
children/childNodes collections remain deferred.
Phase 11l adds reflected id and toggleAttribute with DOMString/Boolean
conversion, native validation and existing atomic attribute storage limits.
Mutated IDs and toggled attributes feed live lookup/selectors and final paint.
Phase 11m adds nodeValue and branded CharacterData data/length/substring/edit
bindings with UTF-16 unsigned offsets and atomic result/storage limits.
Lifecycle edits preserve text-node identity and feed final styles/title/paint.
Monotonic stage/return checks reject expired finite tasks even when the native
interrupt timer callback is delayed; no timeout values are relaxed.
Per-tab V8 isolates supplement, not replace, renderer process confinement.
See the [scripting guide](../../../docs/scripting.md) for exact scope and deployment.

Official document IDs: `ecmascript`, `webidl`, and `html` for the event loop.
Native API references: `clearscript-v8` and `clearscript-v8-constraints`.
Binding sources: `dom` for lookup/textContent, attributes, factories and mutations,
and `html`/`webidl` for title
and string conversion. Full prototype/constructor bindings remain deferred.
See the [standards workflow](../../../docs/standards.md) and
[architecture](../../../docs/architecture/overview.md).
