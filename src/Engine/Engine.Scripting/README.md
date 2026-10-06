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
task cancellation. Unsupported selector features fail explicitly. `:scope`
uses the receiver (closest: original receiver) as the explicit scoping root;
fragments are virtual roots and documents resolve to the document element.
Filtered `:nth-child(An+B of S)`/`:nth-last-child(An+B of S)` reuse that same
scope and shared matching budget; strict-list syntax and unsupported features
retain distinct SyntaxError/TypeError handling.
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
Phase 11n adds branded Text splitText/wholeText. Split preflights data, subtree,
identity and output budgets before mutation; contiguous reads remain live and
bounded. Original node/listener identity and retained resize remain stable.
Phase 11o adds Node.normalize over a shared iterative native plan, atomically
preflighting descendant, surviving-run and shared task text budgets. Empty
descendant Text nodes disappear; adjacent runs preserve the first nonempty
identity and detached data/listeners, without allocating/recycling handles.
Phase 11p adds isEqualNode with optional null operands, both-wrapper ownership
and iterative structural equality. Both complete non-null trees/payloads are
preflighted against traversal/attribute/shared-text budgets even for identity
or obvious mismatches. No descendant wrappers are allocated.
Phase 11q adds readonly Node.nodeName/ownerDocument and Element.localName/
tagName/namespaceURI/prefix. Native ASCII-only HTML casing, detached ownership,
same document facade and string/callback budgets remain intact; adopted
wrappers cannot expose a foreign document.
Phase 11r adds constant-time hasAttributes and bounded ordered getAttributeNames.
Fresh mutable arrays use captured length-prefixed primitive decoding, preserve
native ordering/recovered names and allocate no node wrappers. Attr/NamedNodeMap
and namespace-aware duplicate qualified names remain deferred.
Phase 11s adds bounded document comment/instruction factories and readonly
branded target, reusing native validation and all shared factory budgets.
Raw CharacterData editing, listener identity and non-Text barriers apply;
pseudoattributes/reactions, automatic resource loading and scheduling are deferred.
Phase 11t adds privately branded, unscopable ChildNode.remove with
parent-descendant/ancestor and cancellation preflight, no identity allocation
or recycling, and retained detached descendants/ownership/listeners.
Phase 11u adds document.doctype and privately branded readonly doctype
name/publicId/systemId. Lookup bounds immediate document children and reuses
wrapper identity; fields share individual/task output and callback budgets.
Detached values remain readable, adopted wrappers reject; no identifier
fetching or parser mode-selection expansion is added.
Phase 11v adds shallow/deep cloneNode on branded non-Document wrappers.
It preflights the copied tree, per-element attributes, shared primitive text,
identity and cancellation before detached native cloning. Parent links and
listeners do not copy; cloning Document facades is explicitly unsupported.
Per-tab V8 isolates supplement, not replace, renderer process confinement.
See the [scripting guide](../../../docs/scripting.md) for exact scope and deployment.

Official document IDs: `ecmascript`, `webidl`, and `html` for the event loop.
Native API references: `clearscript-v8` and `clearscript-v8-constraints`.
Binding sources: `dom` for lookup/textContent, attributes, factories and mutations,
and `html`/`webidl` for title
and string conversion. Full prototype/constructor bindings remain deferred.
See the [standards workflow](../../../docs/standards.md) and
[architecture](../../../docs/architecture/overview.md).
