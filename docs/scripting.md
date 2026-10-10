# V8 host, DOM bindings and opt-in inline pages (phases 11a–11v)

The approved embedding is **Microsoft ClearScript V8 7.5.1.1**, with matching
native packages for Linux/Windows x64 and arm64. Engine.Scripting now provides a
small renderer-local native host, not a web-browser JavaScript environment.
**Page scripts remain disabled by default.** Phase 11d explicitly opts into the
post-parse inline subset below. Full DOM/Web IDL bindings,
Full HTML script scheduling and event loops, modules, timers, workers and script-visible
network/storage APIs are deferred. IPC v3 carries only document identities and
trusted script/repaint policy, never DOM or V8 objects.

## Ownership and values

Each [V8ScriptHost](../src/Engine/Engine.Scripting/V8ScriptHost.cs) creates its
own V8 isolate and context. Construction, evaluation and native disposal stay on
the creating thread. Cancellation may interrupt from another thread; it does
not grant other threads access to the engine. No engines, DOM nodes, script
objects or host handles may cross tabs/processes.

The primitive observation entry point, `Evaluate`, evaluates bounded source
through a private function's captured **indirect eval**. Its eval-local
`let`/`const` declarations do not persist, and strict eval's `var` bindings remain
eval-local. It can observe lexical bindings established by classic execution.

Phase 11b adds `ExecuteClassic` and `ExecuteClassicBatch`, which use native
script execution in the context's persistent **global lexical environment**.
Global `let`/`const`/class bindings persist between scripts without becoming
global-object properties. Native global declaration checks, strict mode,
redeclaration failures, temporal dead zones and runtime errors are preserved.
Completion values are deliberately discarded, avoiding native-object conversion
or arbitrary serialization. Use `Evaluate` only to observe copied primitives.

Batches snapshot and validate **all input/count/character budgets** before any
script executes, then run scripts in caller order under **one shared deadline**.
This validation does not precompile later scripts: syntax, declaration and
runtime errors stop execution at that script. Earlier effects (and effects
before a runtime error) are not rolled back; later scripts never run. Empty
batches are valid. A changed caller collection is not reread during execution.
This foundation is not HTML script preparation, parser blocking, scheduling,
error-event reporting, navigation lifecycle or an event loop.

Results are copied before ClearScript's managed conversion:

- Undefined and null remain distinct.
- Boolean and Number retain their types; Number preserves NaN, infinities,
  negative zero and double precision.
- String retains UTF-16 code units, including NUL and lone surrogates.
- BigInt is copied as an exact invariant decimal string.
- Objects, functions, symbols and promises are explicitly rejected. There is
  no implicit `toString`, JSON serialization or native-object return.

The copier captures pristine eval/String/Object.is functions before any script
executes. Replacing those global functions cannot spoof result tags or values.
This is necessary because ClearScript normally turns symbols into strings and
narrows negative zero to integer zero.

The plain host installs **no CLR objects/types**, enables no reflection/debugger,
adds no browser bindings and disables document file/web loading. Optional
phase-11c DOM bindings use the private primitive-only callback described below.
SharedArrayBuffer,
Atomics and WebAssembly globals are removed in this initial subset: blocking
shared-memory waits and Wasm native compilation are not certified by these
execution limits. No task/promise/array host-conversion flags are enabled.
Native ECMAScript built-ins are not a browser event loop or a sandbox.

## Finite native microtask checkpoints (phase 11f)

Pass `enableMicrotasks: true` to opt into a native-JavaScript `queueMicrotask`
adapter. Inline page execution enables it automatically, still behind
`--enable-inline-scripts`; plain hosts do not install that global.
The adapter captures pristine Promise operations and enqueues callbacks on
V8's **same native FIFO** as Promise reactions. ClearScript drains that queue
at native script return; the host verifies completion after each classic source
before the next source runs, and before returning an evaluation/single script.
This is a finite renderer-local execution task, not a persistent HTML event loop.

One evaluation, single classic script, or whole classic batch shares one
deadline, cancellation registration, DOM callback budget and microtask budget.
The queue permits **1,024 pending callbacks and 4,096 total callbacks per task**,
including recursive enqueueing and all sources in a batch. Counters reset on
the next host execution. Native Promise jobs themselves are bounded by
deadline/heap/process limits, not counted against these queueMicrotask quotas.

The callback must be callable; otherwise enqueueing throws TypeError without
invalidating an otherwise successful task. Callbacks receive no arguments,
with undefined `this` (ordinary non-strict functions apply their normal
ECMAScript this conversion). Callback return values are ignored, including
thenables. An evaluation's copied completion value precedes checkpoint effects;
those effects are visible on the next evaluation.

Callback throws and quota violations latch task failure, even when an enqueue
TypeError is caught by caller code. Remaining queueMicrotask wrappers skip,
later classic scripts do not run, and the host is invalidated. Other Promise
jobs can still have effects while V8 drains its native queue; there is no DOM
rollback. Opt-in microtask hosts also invalidate on native syntax/runtime errors
to prevent later reuse of failed-task state. Page navigation publishes nothing
on failure, preserving the previous committed DOM/frame/history. Successful
checkpoint DOM/style/title changes reach paint and retained resize.

No new CLR callback is installed: the private task controller and queued
functions remain native JavaScript closures. Ordinary Promise rejection
reporting, HTML error events, timers, automatic browser events, external scripts and a
persistent host after page execution remain deferred.

## Bounded synchronous synthetic events (phase 11g)

Pass `enableEvents: true` to install native-JavaScript `Event` and `EventTarget`.
With an optional bound document, document and all Node facades also expose
`addEventListener`, `removeEventListener` and `dispatchEvent`. Inline pages
enable this subset under the existing `--enable-inline-scripts` opt-in.
Plain hosts remain unchanged. There are no new CLR callbacks or script objects
stored in the native DOM: listener identities, callbacks, Event brands and state
stay in private JavaScript closures in the owning isolate.

The implemented subset follows DOM's
[dispatch](https://dom.spec.whatwg.org/#concept-event-dispatch) and
[listener invocation](https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke)
algorithms for ordinary trees without shadow DOM:

- `new Event(type, {bubbles, cancelable, composed})` creates an untrusted event.
  Type is required and uses DOMString conversion. Events expose type, target,
  currentTarget, eventPhase, bubbles/cancelable/composed, defaultPrevented,
  unforgeable isTrusted (false for script-dispatched events) and the four phase constants.
- `new EventTarget()` creates an independent target with no parent. DOM
  dispatch snapshots the current ancestor path up to document before calling
  listeners. Detached nodes/fragments use their own current tree; mutations
  during dispatch do not rewrite the path.
- Capture runs root-to-target, then non-capture at target, then ancestor
  bubbling if enabled. Each target/phase clones its listener list: additions
  can participate in a later phase, removals suppress pending callbacks, and
  remove/re-add is a new entry. Duplicate type/callback/capture triples are
  ignored, without updating once/passive.
- Listeners may be functions (`this` is currentTarget) or callback objects
  whose live `handleEvent` is called with the object as `this`. Return values
  and thenables are ignored. Null/undefined callbacks are inert.
- Boolean options select capture; dictionary options support capture, once
  and passive. Primitive union values use Boolean conversion. Removal reads
  only capture. A supplied signal other than undefined rejects explicitly:
  AbortSignal integration is deferred. Passive defaults to false in this
  synthetic subset, not HTML's input-specific default-passive policy.
- Once removes before invocation, including nested dispatch. Passive listeners
  cannot cancel. `preventDefault`, `stopPropagation`,
  `stopImmediatePropagation`, cancelBubble and returnValue are supported.
  `dispatchEvent` returns false exactly when canceled; canceled state persists
  on redispatch. Reentrant dispatch of the same Event rejects.
- `composedPath()` returns a fresh path copy only during dispatch. Cleanup
  resets currentTarget, phase, path and propagation flags even after errors;
  target and canceled state remain. There are no shadow roots/retargeting,
  so composed has no shadow-boundary effect.

| Event budget                                                     |  Bound |
| ---------------------------------------------------------------- | -----: |
| Registered listener entries per host, across all targets         |  1,024 |
| Listener invocations per evaluation/single script/whole batch    |  4,096 |
| Nested simultaneous dispatches                                   |     32 |
| Snapshotted targets per ancestor path, including target/document |  1,024 |
| UTF-16 event/listener type characters                            | 65,536 |

Explicit removal/once releases listener capacity. Garbage collection of an
unreachable standalone target does not refund accounting; that conservative
host-lifetime quota cannot be bypassed by discarding targets. Node identity,
callback/traversal/text limits continue to apply. Invocation counts reset once
per host execution, not per source or checkpoint. Listener dispatch from
microtasks/Promise reactions shares the same counts, deadline and DOM budget.
Microtasks do not run between synchronous listener invocations; they drain at
native script return, before the next classic source/paint.

Listener exceptions (including handleEvent getters) and event quotas latch task
failure, even when script catches the thrown error. Later listeners/dispatches
and classic sources do not run; the host is invalidated at its checkpoint.
Arbitrary caller code or native Promise jobs can still have effects before
that checkpoint; there is no DOM rollback. Page failure publishes nothing and
preserves the previous committed frame/history/DOM. This deliberately differs
from HTML's report-exception-and-continue policy. Argument/receiver/options
errors alone remain catchable TypeErrors; InvalidState/DOMException classes are
not yet exposed. Native syntax/runtime errors invalidate event-enabled hosts.

This is not complete Web IDL Event/EventTarget conformance. Node facades retain
their minimal null-root prototypes rather than becoming EventTarget instances.
Legacy initEvent, timeStamp, CustomEvent/specialized events, automatic
load/input events, `on*` handlers, default actions, Window,
AbortSignal and a persistent event loop are deferred. All hosts/listeners are
disposed after initial page execution; retained resize runs no events.

## Finite document readiness (phase 11h)

Enable `enableDocumentLifecycle: true` on a host with a bound document, events
and microtasks, then call `ExecuteInitialDocumentBatch` exactly once.
Inline page rendering uses this path behind the existing script opt-in.
Event-enabled hosts without lifecycle retain their previous behavior and
do not expose readyState. Preconditions and the full source snapshot/budgets
are validated before execution; rejected inputs/pre-cancellation do not consume
the one-shot lifecycle.

This finite post-parse sequence references HTML's
[current document readiness](https://html.spec.whatwg.org/#current-document-readiness)
and [the end](https://html.spec.whatwg.org/#the-end):

1. All initial classic sources and their native microtasks observe
   `document.readyState === 'loading'`, despite the already parsed DOM.
2. Transition to `interactive`, dispatch a nonbubbling/noncancelable
   `readystatechange`, and drain/verify the native microtask checkpoint.
3. Dispatch exactly one bubbling/noncancelable `DOMContentLoaded` at document,
   still interactive, then drain/verify its native checkpoint.
4. Transition to `complete`, dispatch another nonbubbling/noncancelable
   `readystatechange`, and drain/verify its native checkpoint before painting.

readyState is a readonly nonconfigurable document property with receiver checks.
Lifecycle Event instances have isTrusted true; public dispatchEvent always
resets it to false, including redispatch of a saved lifecycle event. A script
creating an event with a lifecycle name cannot advance readiness or consume the
private lifecycle. Dispatch uses captured internal constructors/algorithms,
not replaceable document methods, Event globals or caller-supplied option
objects. Listener lists and normal phase/cancellation/cleanup rules still apply.

All sources, lifecycle listeners and native jobs share **one navigation
deadline, DOM callback budget, listener invocation budget and microtask quota**.
The three lifecycle stages do not consume source slots/characters and do not
reset counters. Failures at a stage/checkpoint prevent subsequent stages and
invalidate the host; a failure before completion publishes no candidate and
preserves the committed page. Event listeners that attempt to enqueue beyond
the microtask quota report a resource-limit failure, not a masked listener error.
All final DOM/title/style changes feed paint; successful resize retains only
DOM, never listeners/jobs, and does not repeat readiness events.

Even an opted-in page with zero eligible sources creates/disposes a host and
completes this lifecycle; static mode still creates no V8 host. Navigation/reload
uses a fresh host/readiness state. This is intentionally **not full HTML
scheduling**: DOMContentLoaded is normally a queued global task, readiness
depends on parser/deferred/async scripts and subresources, and complete normally
precedes Window load. Here no external resources/modules/deferred scripts are
supported, the complete transition means only this finite execution finished,
and there is no Window/load dispatch, timing API, persistent task queue, parser
interleaving, error-report-and-continue, or input event delivery.

## Minimal live DOM bindings (phase 11c)

Pass an explicit renderer-local `DomDocument` to the host's optional `document`
parameter to install this finite foundation:

- `document.title`: first HTML title in tree order; reads strip/collapse ASCII
  whitespace, writes replace text (or create a title in an existing head).
  No head/title means a no-op setter, not an invented document skeleton.
- `document.getElementById(id)`: required DOMString argument, case-sensitive
  first connected match, empty ID returns null, repeated matches return the
  same JavaScript wrapper.
- Element `textContent`: live descendant Text content excluding comments;
  writes replace children using existing checked DOM operations. Null/undefined
  become empty text; other inputs use DOMString conversion, including rejecting
  symbols. Detached wrappers remain live; adoption into another document rejects
  subsequent access.

Native mutations by the document owner are immediately visible on the next
callback. Script mutations are immediate, including within the same script;
effects before an error are not rolled back. Classic global lexical state may
retain element wrappers across scripts. A document is leased exclusively to one
host until disposal; the owner must not mutate it concurrently or share it
between tabs. The bridge is not a document thread-safety mechanism.

The approved live-binding approach deliberately adds **one private CLR delegate**
with a fixed primitive-only contract. Phase 11e extends that contract to
`(operation, target identity, node identity, reference identity, name, value) -> string`;
identities are integers and all other values are strings. Trusted
bootstrap captures it in a closure and deletes its temporary global **before
any caller script**. Native document/nodes themselves are never imported
into ClearScript. A string status/value crosses back, never a native node, CLR
exception, type or arbitrary object. A separate string status distinguishes null
from empty text. Ordinary bridge failures become JavaScript
TypeErrors with explicit diagnostics. No callback or identity table is exposed
as a property of a wrapper.

Facades are native JavaScript objects with private WeakMap brands, stable
identity and captured pristine intrinsics. Document/element receivers are
checked; borrowing a getter/method onto an arbitrary object fails. Prototypes
are intentionally minimal/frozen and have null roots; document has no prototype.
This is **not full Web IDL prototype/constructor conformance**. Window,
Document/Element/Node constructors, observers and generated IDL bindings
remain unavailable. Optional phase-11g synthetic events are described above.
Phase-11e factories/accessors/mutations are listed below.
Expandos are ordinary JavaScript properties, not native DOM mutations.

| Binding budget                                                                 |   Value |
| ------------------------------------------------------------------------------ | ------: |
| Retained non-document node wrappers per host, including detached/created nodes |   1,024 |
| Callbacks per evaluation/single script/whole batch                             |   4,096 |
| Descendants per traversal/destination subtree; siblings per scan               |   8,192 |
| Ancestor chain per connectivity/mutation check, including receiver             |   8,192 |
| UTF-16 text per callback argument/result                                       |  65,536 |
| Aggregate callback argument/result UTF-16 characters per execution             | 262,144 |
| Attributes per script-mutated element                                          |     128 |
| Stored attribute name/value UTF-16 characters per script-mutated element       |  65,536 |

Budgets are checked before writes; traversal/text assembly is bounded. Counters
reset per execution, but wrapper identities persist until host disposal.
Caller conversion hooks run in V8 under the normal deadline. Host callbacks
are synchronous finite DOM work, not network/storage or arbitrary CLR execution.
These limits do not substitute for process quotas or constitute an exploit
boundary. Phase 11d installs these bindings only for explicitly enabled inline
page execution; this remains trusted-content development tooling.

## DocumentType lookup and metadata (phase 11u)

`document.doctype` returns the attached DocumentType wrapper or null, skipping
preceding comments and reusing any wrapper reached through Node navigation.
Its readonly `name`, `publicId` and `systemId` getters return the native raw
UTF-16 fields, preserving case, NUL and lone surrogates; absent identifiers
are empty strings. HTML parsing still folds parsed names independently.
DocumentType `nodeName` equals `name`, while `nodeValue` and `textContent`
remain null. Removal makes `document.doctype` null but retains detached
metadata/ownership; reinsertion returns the same wrapper. Native adoption
invalidates all old-wrapper getters.

Lookup checks **8,192 immediate document children** before scanning, including
when absent or already wrapped. It neither traverses element descendants nor
reads metadata payloads. A first successful lookup uses the existing **1,024
lifetime non-document identity** budget; repeated lookups/field reads allocate
no identities. Capacity or lookup-output failure reserves no new identity.
Each field read independently shares **65,536 characters per output**,
**262,144 task input/output characters**, **4,096 callbacks** and the existing
deadline/cancellation. An oversized field does not block reading other fields
or looking up the node; absent lookup needs no identity.

Getters are enumerable, nonconfigurable and setter-free. A private DocumentType
brand rejects forged, inherited or borrowed wrong-interface receivers, and
managed ownership rejects adopted wrappers. The minimal shared non-element
prototype exposes field getters on other node wrappers, but access rejects
instead of pretending those interfaces implement DocumentType.

Identifiers are inert strings: no DTD/resource fetching, mode changes or
document implementation factories are introduced. The static parser continues
to reject unsupported legacy PUBLIC/non-compatible SYSTEM mode selection;
only its already-supported identifiers are accepted on rendered pages.
Full WebIDL constructors/prototypes, DOMImplementation and scheduling remain
deferred.

## ChildNode self-removal (phase 11t)

Element, Text, Comment, ProcessingInstruction and DocumentType wrappers expose
`remove()` through a private ChildNode brand. Attached nodes delegate the
native checked parent removal; detached valid nodes are no-ops. Completion is
`undefined`; extra arguments are ignored without conversion. Documents have
no method. The minimal shared non-element prototype exposes it on fragments,
but fragment, fake, inherited and borrowed unsupported receivers reject.
Current-document ownership is checked even for detached no-ops; adopted
wrappers cannot mutate another document.

Before any attached removal, the bridge checks **8,192 parent ancestors**
including the parent, and **8,192 descendants of the parent** (excluding that
parent, including the removed subtree), with cancellation before writing.
This conservative bound matches existing tree-mutation traversal policy.
Over-limit removal leaves the tree unchanged. No node payload is read, no
identity is allocated/recycled, and no output text is charged. Existing
**4,096 callbacks**, shared task deadline/cancellation and lifetime identities
still apply. Detached no-ops require no tree traversal.

Sibling links repair immediately; removed subtree/data/owner/wrapper identity,
query snapshots and synthetic listeners survive. Dispatch keeps its snapshotted
path if a listener removes its target, while a later detached dispatch has no
former parent. `Symbol.unscopables.remove` keeps `with(node)` from capturing
unqualified remove names. Full WebIDL prototypes, mutation observers, live
ranges, custom-element reactions and persistent scheduling remain deferred.

## Comment and ProcessingInstruction factories (phase 11s)

Document exposes `createComment(data)` and
`createProcessingInstruction(target, data)`. Both return fresh detached Node
wrappers owned by the same bound document. Comments preserve arbitrary UTF-16
data, including NUL, lone surrogates and markup-like `-->` sequences; this is
node construction, not HTML parsing. ProcessingInstruction validates the target
as an XML Name and rejects `?>` in initial data using the existing native
factory. Empty targets explicitly report the same native InvalidCharacter
diagnostic as other invalid names rather than escaping as a BCL exception.
The bridge maps native validation to its existing TypeError string diagnostic,
not a full DOMException object.

Receiver and required-argument checks precede DOMString conversion. Instruction
target converts before data; **both conversions finish before native validation**,
so user conversion effects are retained on a later validation failure.
Null/undefined stringify normally. Extra arguments are ignored without
conversion. Factories share **65,536 characters per input field**, **262,144
shared task input/output characters**, **1,024 lifetime non-document identities**
and **4,096 callbacks**, plus deadline/cancellation. Input, capacity, native
validation and primitive output-budget failures register no wrapper identity
and do not alter a tree.

Instruction wrappers expose readonly, case-preserved `target` through a private
ProcessingInstruction brand and native ownership validation. It shares normal
string output budgets. The minimal non-element prototype also exposes this
getter on other wrappers, but invoking it on a non-instruction explicitly
rejects, consistent with existing CharacterData/Text subset brands.
Comments/instructions use existing CharacterData editing, metadata, equality,
tree navigation/mutation and synthetic listener machinery. They are non-Text
barriers for wholeText/normalize and excluded from element descendant textContent.
Editing instruction data remains raw and does not rerun factory validation
or change target.

Current DOM ProcessingInstruction pseudoattribute parsing/APIs and reactions,
special XML stylesheet behavior, full constructors/WebIDL and automatic
events remain deferred. Inserting these nodes never schedules scripts or
loads resources. Lifecycle changes feed final style/title/paint while retained
resize and transactional failure remain unchanged.
Sources: cached `dom`,
[createComment](https://dom.spec.whatwg.org/#dom-document-createcomment),
[createProcessingInstruction](https://dom.spec.whatwg.org/#dom-document-createprocessinginstruction)
and [target](https://dom.spec.whatwg.org/#dom-processinginstruction-target).

## Ordered attribute inspection (phase 11r)

Branded Element wrappers expose `hasAttributes()` and `getAttributeNames()`.
Presence reads only the native attribute count; name inspection returns a
**fresh mutable JavaScript Array** in native attribute-list order. Replacement
keeps a name's position, removal deletes it, and readdition appends it. Returned
arrays are snapshots: user edits and later attribute changes are independent.
Extra arguments are ignored without conversion. Borrowed methods reject
non-elements, forged receivers and wrappers adopted into another document.

The existing OrderedDictionary is the source of truth; no live NamedNodeMap
or Attr objects are added. HTML names retain ASCII folding; parser-recovered
names are copied without public-setter revalidation. Names containing colons,
commas, quotes, backslashes or lone surrogates survive a private length-prefixed
UTF-16 wire format. The decoder captures string/defineProperty/brand intrinsics
and defines own writable/configurable/enumerable array entries directly, avoiding
inherited index setters, push/species/JSON hooks or replacement Array globals.

Name scans permit **128 attributes** and **65,536 encoded output characters**,
including decimal length prefixes and separators; this deliberately means
a single 65,530-character name fits exactly, while 65,531 does not. Encoded
output charges the existing **262,144 shared task text budget**. Values are
neither read nor copied, so oversized native values do not prevent name
inspection. hasAttributes remains a constant-time Boolean read even when
the native list exceeds the name-scan cap. Both methods share **4,096 callbacks**,
ownership, cancellation and host deadlines. No wrapper slots are allocated.
Failure never changes attributes or exposes a partial snapshot.

Namespace-aware duplicate qualified names, NamedNodeMap/Attr, full WebIDL and
dynamic script scheduling remain deferred. Lifecycle snapshots observe live
attribute mutations before final styles/title/paint; retained resize is unchanged.
Sources: cached `dom`,
[hasAttributes](https://dom.spec.whatwg.org/#dom-element-hasattributes) and
[getAttributeNames](https://dom.spec.whatwg.org/#dom-element-getattributenames).

## Live Node and Element metadata (phase 11q)

All Node facades expose readonly `nodeName` and `ownerDocument`; Element
facades also expose readonly `localName`, `tagName`, `namespaceURI` and `prefix`.
Each getter validates its private receiver brand and current native ownership.
Names follow the existing HTML-only native model: element local names use
ASCII lowercase, nodeName/tagName use ASCII uppercase, and non-ASCII UTF-16
code units are preserved. A colon in a createElement local name does not
create a namespace prefix. All supported elements use the HTML namespace and
return null prefix; namespace-aware factories and XML documents are deferred.

Document returns `"#document"` and null ownerDocument. Text, Comment and
DocumentFragment return their standard `#` names; ProcessingInstruction
returns its case-preserved target, and DocumentType its case-preserved name.
Every non-document wrapper returns the **same bound document facade** as
ownerDocument, including detached, removed and normalized-away nodes. Adoption
outside that document rejects all metadata getters; it never returns a
foreign document facade. Element-only properties are absent on non-element
facades, and borrowed Element getters reject them explicitly.

Getters are enumerable, getter-only and nonconfigurable, following the
existing minimal facade pattern (not full WebIDL prototype conformance).
String results charge the existing **65,536 per-result / 262,144 shared-task
text limits**. Element name output budget is checked before allocating uppercase
output. ownerDocument uses the existing document identity, without consuming
a non-document wrapper slot even at the **1,024 lifetime identity** cap.
All getters share the **4,096 callbacks**, deadline/cancellation and ownership
checks. These constant-node reads do not traverse parents or descendants.
No new CLR callbacks, mutable native objects, namespace machinery or dynamic
script scheduling are added. Lifecycle reads can guide style/title updates
while transactional navigation and retained resize remain unchanged.

Sources: cached `dom`, [nodeName](https://dom.spec.whatwg.org/#dom-node-nodename),
[ownerDocument](https://dom.spec.whatwg.org/#dom-node-ownerdocument),
[tagName](https://dom.spec.whatwg.org/#dom-element-tagname),
[localName](https://dom.spec.whatwg.org/#dom-element-localname),
[namespaceURI](https://dom.spec.whatwg.org/#dom-element-namespaceuri) and
[prefix](https://dom.spec.whatwg.org/#dom-element-prefix).

## Bounded structural Node equality (phase 11p)

All branded Node wrappers expose `isEqualNode(otherNode = null)`. Omitted,
undefined and null arguments return false after validating the receiver's
ownership. Non-null operands must be genuine Node wrappers owned by the same
bound document; no object/string conversion is attempted. Extra arguments are
ignored. Both operand brands/ownership are checked before comparison, including
self-comparison and obvious interface mismatches.

The shared iterative native algorithm compares supported node interfaces,
element HTML namespace/local name, unordered raw attribute names/values,
Text/Comment data, ProcessingInstruction target/data, DocumentType name/public/
system IDs, and children at identical tree indices. UTF-16 data is exact;
adjacent Text segmentation matters even when textContent matches. Identity,
parent/connection, owner document, listeners and document quirks mode do not
affect structural equality. Native C# IsEqualNode supports different documents;
script facades deliberately retain their existing single-document ownership
boundary. Namespace-aware Attr, CDATA and shadow trees remain deferred.

For non-null operands, both **complete trees** are preflighted before payload
comparison or identity/mismatch shortcuts. Each permits **8,192 nodes including
the root**, **128 attributes per element**, **65,536 aggregate stored attribute
name/value characters per element**, and **65,536 characters per individual
payload field**. Namespace/local names, attributes, CharacterData, instruction
targets and doctype fields on **both** trees charge the existing **262,144
shared task text budget**, even when comparing a node with itself. The Boolean
primitive also charges output budget. Null comparison does not scan a tree.
These intentional work bounds reject oversized native trees explicitly rather
than returning a misleading false result.

No identities are allocated, recycled or exposed for descendants. Comparison
is read-only and cancellation-aware, uses no recursion and shares the existing
callback/deadline budget. Native API cancellation is optional; the internal
bounded overload is available only to Engine.Scripting. Lifecycle equality
observes live edits before final paint without altering retained resize or
dynamic script scheduling.
Source: cached `dom`, [isEqualNode](https://dom.spec.whatwg.org/#dom-node-isequalnode)
and [node equality](https://dom.spec.whatwg.org/#concept-node-equals).

## Bounded descendant Text normalization (phase 11o)

All branded Node wrappers expose zero-argument `normalize()`, returning
undefined. It removes empty **descendant** Text nodes and merges adjacent Text
siblings into the first nonempty Text node, without crossing elements, comments
or processing instructions. Documents, fragments and nested element subtrees
are supported. Normalizing a leaf is a no-op: the receiver and its siblings
are not normalized. Extra arguments are ignored without conversion.

The survivor retains its identity/listeners; removed wrappers remain valid,
detached, with their original data, document ownership and listeners. UTF-16
code units concatenate unchanged, including surrogate halves. Repeating
normalization is idempotent. No new wrapper identities are allocated or
recycled, even at the **1,024 lifetime identity** limit.

The native DOM shares one iterative, whole-subtree plan with the bridge via
an assembly-internal bounded overload. The public C# Normalize accepts optional
cancellation. Script normalization preflights **8,192 descendants**, including
non-Text nodes, and **65,536 code units per surviving run**, even a singleton.
Each surviving run's data charges the shared **262,144-character task budget**
before any write; normalizing a large subtree cannot bypass text-work limits
merely because the operation returns undefined. All **4,096 callback**, ownership,
deadline and cancellation checks still apply. Limit or preflight cancellation
failure leaves every run and parent unchanged. Commit has no callbacks or
cancellation points and compacts affected child lists without quadratic removals.

Observers, live-range repair, CDATA and custom-element reactions remain
deferred. No dynamic script scheduling or persistent hosts are added.
Lifecycle normalization feeds final style/title/paint and retained resize
without reexecution.
Source: cached `dom`, [normalize](https://dom.spec.whatwg.org/#dom-node-normalize).

## Bounded Text splitting and contiguous reads (phase 11n)

Branded Text wrappers expose `splitText(offset)` and readonly `wholeText`.
Split uses WebIDL unsigned-long conversion after receiver/required-argument
checks, then reads the current data (including conversion side effects).
The original node retains the prefix and its listeners/identity; a fresh Text
wrapper contains the suffix and is inserted immediately after the original
when attached. Detached splits leave both nodes detached. Offsets zero and
length produce empty nodes, not normalization/removal. UTF-16 splits can
separate surrogate halves. Invalid offsets report the existing bridge TypeError
with an IndexSize diagnostic, not yet a DOMException.

`wholeText` concatenates the live contiguous Text sibling run in tree order,
including empty text nodes, and stops at any non-Text node. Detached nodes
return their own data. The C# DOM exposes SplitText, WholeText and a shared
contiguous-run enumeration; callers must not mutate during enumeration.
Comments, processing instructions and fragments are not Text receivers.

Before splitting, the bridge checks the **65,536-code-unit original data**
cap, **1,024-wrapper lifetime capacity**, parent ancestor limits and the
**8,192-descendant destination subtree** limit including the new node.
It reserves output text budget before mutation and registers the new identity
without a second fallible budget check. Rejected splits leave text/tree/
identity capacity unchanged; prior user conversion effects are not rolled back.
`wholeText` requires at most **8,192 immediate siblings** and a **65,536
aggregate code-unit result**. All operations share DOM callback/text limits,
host deadline/cancellation and ownership checks. No new CLR callback or
cross-tab object is exposed.

Observers/live-range repair, CDATA and full WebIDL Text
constructors remain deferred. Lifecycle style/title splits preserve content
and feed final paint; retained resize never reruns splitting.
Sources: cached `dom`, [splitText](https://dom.spec.whatwg.org/#dom-text-splittext)
and [wholeText](https://dom.spec.whatwg.org/#dom-text-wholetext).

## Bounded CharacterData editing (phase 11m)

Branded text, comment and processing-instruction wrappers expose `data`,
readonly UTF-16 `length`, `substringData`, `appendData`, `insertData`,
`deleteData` and `replaceData`. Node wrappers also expose `nodeValue`:
CharacterData reads/writes the same storage as textContent; other supported
nodes read null and ignore writes after argument conversion. In-place edits
preserve node identity, parent/sibling links and event registrations.
The C# DOM provides `Length`, `SubstringData` and `ReplaceData` primitives;
script adapters compose those rather than copy/replace nodes.

Offsets/counts follow WebIDL unsigned-long conversion: truncate/wrap to 32 bits,
NaN/infinity become zero, and Symbol/BigInt numeric conversion fails.
Offsets above the current length fail explicitly; offset equal to length is
valid, and counts clamp at the end without unsigned addition overflow.
All operations count UTF-16 code units, allowing surrogate halves and NUL.
Receiver/required-argument checks precede conversion. Offset, count and data
convert in argument order before reading current storage, so conversion side
effects remain visible. Extra arguments are ignored. `data` uses
LegacyNullToEmptyString: null becomes empty, while undefined becomes
`"undefined"`. Nullable nodeValue maps both null/undefined to empty data;
method DOMString arguments stringify null/undefined normally.

Bridge writes preflight the resulting **65,536-code-unit data storage** limit
before allocation/mutation. Substring results have the same limit; length can
inspect larger native data, and bounded deletion/full replacement can recover
it. Existing shared **4,096 callbacks / 262,144 input-output text characters**
and execution deadline/cancellation still apply. Native offset failures use
`DomError.IndexSize`; script failures use the existing primitive bridge
TypeError diagnostic, **not yet an IndexSizeError DOMException**.
No CLR node/string-operation objects cross the bridge.

This remains the existing static DOM subset: no observers, live-range repair,
custom-element reactions or new
dynamic script execution. ProcessingInstruction edits update raw data only;
the engine does not implement processing-instruction pseudoattribute parsing
or reactions. Shared non-element prototypes reject non-CharacterData receivers
instead of pretending those APIs apply. Lifecycle edits to style/title text
feed final styling/paint; retained resize does not rerun scripts.
Sources: cached `dom`,
[CharacterData](https://dom.spec.whatwg.org/#interface-characterdata),
[replace data](https://dom.spec.whatwg.org/#concept-cd-replace) and
[nodeValue](https://dom.spec.whatwg.org/#dom-node-nodevalue).

## Reflected ID and attribute toggling (phase 11l)

Bound HTML Element wrappers expose reflected `id` and `toggleAttribute(name,
force?)`. An absent ID reads as the empty string; writes use DOMString
conversion and preserve raw UTF-16 text, including whitespace and NUL. Null
and undefined become `"null"` and `"undefined"`, not an empty value.
Live ID changes immediately affect ID lookup and CSS matching; detached nodes
retain their reflected value without participating in document lookup.

Toggle converts the name after checking the Element receiver and required
argument, then applies Boolean force conversion. Omitted/undefined force
toggles; false forces absence; true forces presence without replacing an
existing value. Objects are truthy without invoking valueOf/toString.
Names follow the existing native HTML attribute validation and ASCII folding,
including validation for forced no-ops. Invalid names produce the existing
native-bridge TypeError diagnostic, not yet a DOMException. Conversion effects
precede the live attribute read; extra arguments are ignored.

The C# DOM also exposes checked `DomElement.ToggleAttribute`. Attribute
storage now uses an ordered dictionary: replacement preserves position,
while removal/readdition appends at the end instead of recycling a removed
dictionary slot. This applies consistently to native and script mutations.
The bridge reuses the existing **128-attribute / 65,536 aggregate stored
name/value character** preflight for new toggle attributes and ID writes.
Forced no-ops and removals remain available at capacity; rejected additions
do not partially write. Shared DOM call/text limits, receiver ownership and
host cancellation/deadlines remain unchanged. No new CLR callback, script
scheduling, boolean-attribute-specific behavior or custom-element reactions
are added. Final lifecycle mutations feed selectors/paint and retained resize.
Source: cached `dom`, [id](https://dom.spec.whatwg.org/#dom-element-id) and
[toggleAttribute](https://dom.spec.whatwg.org/#dom-element-toggleattribute).

## Bounded tree inspection (phase 11k)

Bound Node facades expose `contains`, `isSameNode`, `getRootNode`,
`hasChildNodes` and readonly `parentElement`. Containment is inclusive, with
null/undefined arguments returning false; `contains` requires an argument,
while omitted `isSameNode` defaults to null. Both operands must be branded
nodes owned by the bound document. Identity, detached trees and fragment roots
use the same lifetime wrappers as queries/mutations. Null comparisons still
validate receiver ownership; adopted wrappers cannot bypass checks.

`getRootNode` accepts an optional WebIDL-style dictionary: null/undefined is
empty, other primitive values throw TypeError, and object/function dictionaries
read `composed` before inspecting the tree. Getter failures/side effects remain
visible. With shadow DOM deferred, ordinary and composed roots are identical.
This does not approximate traversal across an unimplemented shadow tree.

Document, Element and DocumentFragment provide readonly `firstElementChild`,
`lastElementChild` and `childElementCount`. Element and CharacterData wrappers
provide readonly `previousElementSibling`/`nextElementSibling`, skipping text
and comments. These values are live and preserve wrapper identity. Minimal
shared non-element prototypes may expose properties on unsupported receivers,
but those getters throw rather than supply success-shaped defaults.
Live `children`/`childNodes` collections and full WebIDL prototypes remain
deferred.

Ancestor scans allow **8,192 inclusive nodes**, check cancellation per node,
and short-circuit when containment succeeds. Child/sibling scans require at
most **8,192 total immediate children**, including non-elements, before
producing results. Returned identities retain the existing **1,024-wrapper**
lifetime cap. The shared DOM callback/text limits and host deadline still
apply. Resource failures do not mutate the tree. Lifecycle inspection can
drive class/style/title changes before paint; retained resize never reruns it.
Sources: cached `dom`, [Node](https://dom.spec.whatwg.org/#interface-node),
[ParentNode](https://dom.spec.whatwg.org/#interface-parentnode) and
[NonDocumentTypeChildNode](https://dom.spec.whatwg.org/#interface-nondocumenttypechildnode).

## Live bounded class tokens (phase 11j)

Bound HTML Element wrappers expose `className` and a same-object `classList`.
Both use the existing hidden primitive attribute bridge; no CLR objects or
additional host callbacks are installed. The native-JavaScript list reads the
current attribute on every operation, including external attribute changes,
detachment and changes during iteration. Adopted elements remain rejected.
`className`, `classList.value`, the stringifier and assignment to `classList`
(forwarded to `value`) preserve raw attribute whitespace and duplicates.

Token reads parse an ordered, case-sensitive set using only ASCII whitespace.
Supported operations are readonly `length`/numeric indices, `item` with unsigned
long conversion (out of range returns null), `contains`, variadic `add`/`remove`,
`toggle` with optional Boolean force, `replace`, `forEach`, `keys`, `values`,
`entries` and the default iterator. Iteration is live rather than a snapshot;
callbacks receive `(token, index, list)` with their supplied receiver.
`supports` always throws TypeError because `class` has no supported vocabulary.
`contains` does not reject empty or whitespace-bearing tokens.

Mutators convert all relevant arguments before token validation and read the
attribute after conversion, so conversion side effects are visible. Validation,
capacity checks and serialization complete before the attribute is written.
Zero-argument add/remove normalize existing attributes, but do not create an
absent empty attribute. Forced no-op toggle and unsuccessful replace do not
normalize. Replacement merges existing tokens at their first ordered-set slot.
Earlier user conversion/callback effects are not rolled back.

Limits are **1,024 unique tokens**, **1,024 variadic arguments**, and **65,536
aggregate argument/serialized UTF-16 characters per operation**. Raw value
writes retain the existing native attribute storage/count preflight. The
existing shared **4,096 DOM callbacks and 262,144 callback text characters per
execution task** also apply, including input validation and repeated live
reads. Iterating a large list can exhaust these shared budgets before its end.
All native work/callbacks share the host deadline and cancellation. An external
attribute with too many tokens fails token operations explicitly; raw value
reads/writes remain available to inspect or replace it.

Empty mutation tokens throw native SyntaxError; ASCII whitespace tokens throw
a native Error named `InvalidCharacterError`. These are **not yet DOMException
objects**. Captured intrinsics, private null-root storage and receiver brands
resist prototype/global tampering. Numeric properties are readonly and
enumerable; a private Proxy supplies live descriptors and membership.
This is a finite DOMTokenList-like facade, not full WebIDL: global
DOMTokenList/Element constructors, expandos, prototype replacement,
defineProperty overrides, and sealing/freezing a live list are deferred.
Lifecycle class mutations participate in CSS queries and final paint; retained
resize still uses the mutated DOM without script reexecution.
Source: cached `dom`, [DOMTokenList](https://dom.spec.whatwg.org/#interface-domtokenlist)
and [Element.classList](https://dom.spec.whatwg.org/#dom-element-classlist).

## Bounded DOM queries (phase 11i)

Bound document, Element and DocumentFragment facades expose `querySelector`
and `querySelectorAll`; Elements also expose `matches` and `closest`.
They reuse Engine.Css's first-party static selector parser/matcher, not a
second parser or JavaScript traversal. See DOM's
[scope queries](https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall)
and [closest](https://dom.spec.whatwg.org/#dom-element-closest).
Queries require a DOMString selector argument and receiver validation precedes
caller conversion hooks. Current detached trees are searchable; adopted
wrappers fail under the existing ownership rules.

Descendants are searched in tree order, excluding the query receiver itself.
Selector-list branches deduplicate naturally through one match per candidate.
Selector ancestor/sibling matching can look outside the receiver subtree;
only returned candidates are scoped. `matches` tests the receiver and `closest`
searches inclusive ancestors nearest-first. Missing results are null/empty.
No DOM mutation or script execution is performed by native matching.

`querySelectorAll` returns a branded static NodeList-like facade with readonly
indices and length, `item` (Web IDL unsigned-long index conversion and null when
out of range), `forEach`, values/keys/entries and iteration. Every entry shares
the existing live Node wrapper identity. The list retains its original sequence
after removal/reordering/new nodes; wrappers still observe live node changes.
The facade is frozen, with a minimal null-root prototype and no global NodeList
constructor: this is not full Web IDL inheritance/expando conformance.
Caller changes to array/string/map intrinsics cannot alter private list storage.
No CLR collections, nodes or exceptions cross the bridge.

| Per-query budget                                 |  Bound |
| ------------------------------------------------ | -----: |
| Selector UTF-16 characters                       |  4,096 |
| CSS tokens                                       |  8,192 |
| Selector chain/nesting/evaluation depth          |     64 |
| Shared matching operations across all candidates | 65,536 |
| Traversed descendants or inclusive ancestors     |  8,192 |
| Static result entries                            |  1,024 |

Query results also consume the existing **1,024 host-lifetime Node identities**,
including all previously created/detached wrappers. Full-list result and identity
capacity plus serialized output text are preflighted before new identities are
reserved; failed oversized queries cannot consume partial identity capacity.
Queries share existing per-task DOM callback/aggregate text budgets. Native CSS
parsing/matching observes the host's linked deadline/cancellation token;
caller conversion and NodeList callbacks execute in V8 under that deadline.
Resource/ownership errors are explicit catchable TypeErrors, as with other DOM
bridge operations. An uncaught query error rejects page publication.

Supported syntax is the existing [static CSS selector subset](css.md): types,
IDs/classes, attributes, combinators, structural pseudo-classes and supported
functional selectors. Invalid selector syntax/tokenization throws a native
JavaScript SyntaxError, not yet a DOMException. Unsupported features fail
explicitly with TypeError instead of silently returning no matches: dynamic
state, namespaces, `:has` and pseudo-elements remain
deferred. `:scope` is supported as described below. Rejecting pseudo-elements
rather than returning an empty list is an
intentional finite-subset deviation. No selector cache, live collections,
innerHTML or dynamic script scheduling is added. Inline pages gain these APIs
under the existing opt-in; lifecycle, retention and confinement are unchanged.

## Scoped selector queries (`:scope`)

Queries now pass an explicit immutable Engine.Css `CssSelectorScope` per call,
following DOM's [scope-match a selectors string](https://dom.spec.whatwg.org/#scope-match-a-selectors-string),
[matches](https://dom.spec.whatwg.org/#dom-element-matches) and
[closest](https://dom.spec.whatwg.org/#dom-element-closest) plus Selectors'
[:scope](https://drafts.csswg.org/selectors-4/#the-scope-pseudo) (cached `dom`,
`selectors`). There is no JavaScript matcher, facade change or new bridge field.

- Element `querySelector`/`querySelectorAll`: the receiver is the scoping root;
  only descendants are candidates, so `:scope` alone returns null/empty while
  `:scope > div` returns children. Left-hand compounds may still match
  ancestors outside the receiver subtree (`html > :scope > div`).
- Document queries: `:scope` resolves to the document element.
- DocumentFragment queries: the fragment is a featureless **virtual scoping
  root**, never a result. `:scope > p` finds top-level elements; `:scope` and
  `* > p` do not match the fragment. `:is`/`:where`/`:not` follow Selectors'
  featureless rule (`:not(.x)` cannot match the virtual root; `:not(:scope)` can
  be evaluated against it). No synthetic root element is created.
- `matches` scopes to the receiver. `closest` scopes **every** inclusive
  ancestor test to the original receiver, so `inner.closest('div:scope')` is
  null rather than the nearest div.
- Detached receivers keep explicit scoping; there is no `:root` for them.

Leading/dangling combinators (`> div`, `:scope >`, `:scope,`) are invalid
SyntaxErrors, not relative selectors. `:scope()`/`:has(:scope)`/pseudo-elements
remain unsupported TypeErrors. Scope checks and virtual-root combinator steps
consume the same per-query operation/depth budget and linked cancellation;
traversal, result and identity limits are unchanged. Reused selector strings
or `CssSelectorList` instances retain no scope between queries or tabs.

Filtered `:nth-child(An+B of S)` and `:nth-last-child(An+B of S)` use the same
CSS matcher for queries, `matches` and `closest`. `S` is a strict complex-real
selector list; invalid branches throw SyntaxError, while unsupported features
still throw TypeError. Filtering uses inclusive element siblings, including a
detached singleton, and never rebinds `:scope` to each sibling or ancestor.
Nested filters share the existing operation/depth budget and cancellation;
static NodeList, traversal/result/identity bounds and atomic publication stay
unchanged. `of` clauses on the of-type variants remain unsupported.

## Bounded attributes and Node mutation (phase 11e)

The optional bound host now also provides:

- `document.documentElement`, `head`, `body`: live nullable root accessors,
  using existing HTML-only native DOM behavior. `body` has no setter.
- `document.createElement(name)`, `createTextNode(data)`,
  `createDocumentFragment()`: detached renderer-local nodes. Element/text
  factories require a DOMString argument. Element names use the existing native
  HTML-only name validation/ASCII lowercasing subset; namespace APIs, customized
  built-ins and `createElement` options are deferred (explicit options other
  than undefined reject).
- Element `getAttribute`, `hasAttribute`, `setAttribute`, `removeAttribute`:
  required DOMString arguments, native ASCII-only name folding and validation,
  missing versus empty values preserved. Names/values are not interpreted as
  script, network or event-handler commands. `setAttribute`/`removeAttribute`
  return undefined.
- Node `nodeType`, `parentNode`, `firstChild`, `lastChild`, `previousSibling`,
  `nextSibling`, `isConnected` and `textContent`. Element and fragment text
  getters concatenate descendant Text, excluding comments; CharacterData
  wrappers read/write their own data; Document/DocumentType return null and
  ignore writes. Null/undefined text setters become empty text.
- Node `appendChild`, `insertBefore`, `removeChild`, `replaceChild`: checked
  existing native DOM operations, including move semantics, fragment draining,
  cycle/document-hierarchy rejection and not-found reference checks.
  `appendChild`/`insertBefore` return the input node (including an emptied
  fragment); removal/replacement return the removed node. `insertBefore`
  requires two arguments; null/undefined reference means append.

Document has a null prototype and its Node surface as own properties.
Element and other Node wrappers each use a frozen, null-root minimal prototype;
the common Node surface is shared as methods/accessors, not a full Web IDL
inheritance hierarchy. Private Node/Element brands reject spoofed receivers and
plain objects/proxies as node arguments. Element receiver checks precede caller
string conversion hooks. Node arguments must belong to this host's identity
table; native adoption into another document rejects every subsequent access.
All access paths share wrapper identity, including roots, navigation, lookup
and newly created nodes. Detached wrappers stay valid and consume the lifetime
wrapper budget; GC does not replenish it. Native nodes and types never cross
the callback.

Mutation checks bound source/destination descendant walks and parent/source
sibling scans before native adoption/removal/insertion. Insert/replace must
leave at most 8,192 descendants in the receiver's subtree; replacing or
reordering nodes at the exact limit is permitted. These are per-operation/tree
budgets, not a document-wide node cap. Script attribute writes check the full
resulting count and name/value storage before modifying anything. Existing
native/parser attributes are not silently truncated; oversized native values
fail bounded reads and removal can explicitly reduce an oversized attribute set.
Callback text accounting includes both attribute name and value, and identities
returned to JavaScript. Input/conversion/budget/hierarchy failures do not
partially perform the requested mutation; independent side effects in caller
conversion hooks remain ordinary script effects.

This does not add `childNodes`/`children` collections, `innerHTML`, selectors,
clone/import/adopt APIs, shadow DOM, events or observers. Appending or changing
script elements **never schedules execution**. Phase 11d still executes only its
initial source snapshot, including an initially collected script detached by an
earlier script. New embedded styles and inline attribute/tree changes feed the
post-script style/layout/paint pass, and the mutated DOM remains retained for
resize without repeating execution. Unsupported CSS/layout/resource features
still fail explicitly; this is not broader HTML element behavior.

## Opt-in inline page execution (phase 11d)

Add `--enable-inline-scripts` to an explicit browser mode.
It preserves the CLI's mandatory confinement policy (or explicit trusted-content
development opt-out) and composes with redundant `--require-sandbox` without changing OS profiles, mounts,
syscall policy or resource quotas. The API equivalent is
`executeInlineScripts: true` on the shared page renderer/process client.
Without this option, script elements remain inert and existing static rendering
behavior is preserved. Engine.Content itself remains offline and script-free.

The page renderer parses the complete document, snapshots all eligible sources
in connected tree order, validates their entire count/character budget, then
executes one classic batch in a fresh document-bound V8 host. Later elements are
already visible to earlier scripts. Sources modified or detached by an earlier
script still run from the snapshot. This is deliberately **not parser-blocking
HTML script preparation/scheduling**. Global lexical state persists between the
batch's scripts, but not between navigations or tabs.

After execution and the finite phase-11h readiness events/checkpoints, the host
is disposed. Stylesheets are collected from the
mutated DOM, and style/layout/display lists/pixels are computed afresh. Live
title changes become the page/tab/window title; a script may change an embedded
style's `textContent` before stylesheet collection. All existing CSS/layout/paint
subset failures remain enforced; scripts do not bypass them.

Classification uses HTML's type/legacy-language selection and ASCII matching
against the complete JavaScript MIME essence list. Missing/empty `type` and
default/empty `language` select classic scripts. Type values strip only ASCII
whitespace; MIME parameters do not match an essence. Other data-block types
remain inert, and classic `nomodule` elements are skipped. Empty inline classic
sources do not consume a source slot. Executable `src` attributes (including an
empty value), modules/import maps/speculation rules and
`async`/`defer`/legacy `for`/`event` scheduling attributes fail during preflight;
no subresource is fetched. Rejecting inline async/defer rather than ignoring
them is an explicit restriction of this foundation.

The existing host limits apply unchanged: **64 sources, 65,536 UTF-16 characters
per source, 262,144 aggregate source characters and one two-second batch
deadline**, plus the live-binding budgets above. Caller cancellation, runtime/
syntax errors, unsupported executable features and resource limits fail the
navigation explicitly. Unlike HTML's error-report-and-continue behavior, later
scripts do not run after an error and no partially mutated page is published.
The previously committed frame/history/title remains intact.

Each successful navigation/reload gets a new `LoadedPage.DocumentId` and fresh
DOM/host. Successful page publication pins its DOM; the renderer retains at most
the committed document and one successful unpublished candidate. Resizes use
`RenderRetainedAsync` and recompute styles/layout/paint from that DOM without
rerunning scripts or refetching. Failed/stale candidates cannot evict the pinned
page. `IPageRenderer.CommitDocument` makes publication explicit; the process
client conveys the acknowledgement on its next serialized request.

If cancellation/crash terminates a worker, its DOM is lost. A later scripted
resize fails with an explicit reload requirement instead of silently executing
scripts again. Reload/new navigation may create a replacement worker/context.
Static-mode resize behavior is unchanged. Retained DOM never crosses IPC.

There is still no Window/global browser API, automatic event-handler dispatch, timer,
HTML event loop, module loader, origin/CSP enforcement or script-visible
network/storage. Finite native microtask checkpoints are not an HTML event-loop
scheduler; there is no live host remaining after initial execution. Do not use
this mode for hostile content, even with OS confinement.

## Bounds and failure

Execution stages and final return additionally check a monotonic elapsed clock.
The cancellation timer remains responsible for interrupting in-flight native
work, but a delayed timer callback cannot make an expired finite task succeed
or start the next source/lifecycle stage. The timeout is shared and never reset.

| Bound                                                 |                                                Value |
| ----------------------------------------------------- | ---------------------------------------------------: |
| Input UTF-16 characters per evaluation/classic script |                                               65,536 |
| Classic scripts per batch                             |                                                   64 |
| Aggregate UTF-16 characters per batch                 |                                              262,144 |
| Copied String/BigInt UTF-16 characters                |                                               16,384 |
| Evaluation/single-script/whole-batch deadline         | 2 seconds by default; configurable `(0, 30]` seconds |
| Monitored V8 heap                                     |                          32 MiB, sampled every 10 ms |
| Old-space constraint                                  |                      128 MiB, no on-demand expansion |
| New-space constraint                                  |                                               16 MiB |
| External ArrayBuffer allocation                       |                                   16 MiB per isolate |
| Script stack growth                                   |                                              512 KiB |

Pre-canceled calls do not execute or invalidate a usable host. An in-flight
cancellation, deadline interruption or fatal engine error invalidates the
host; dispose it and create a fresh isolate rather than resuming partially
executed state. Normal syntax/runtime errors and unsupported/oversized return
values are explicit exceptions but leave the plain context available.
Opt-in microtask/event hosts instead invalidate on native engine errors or
latched queue/listener failure, as described above.
All execution entry points share the same cancellation/invalidation path.
Interrupt callbacks are drained on success **and failure** before an operation
returns, preventing a late callback from interrupting later execution.

ClearScript reports monitored heap exhaustion as a fatal ScriptEngineException,
wrapped by ScriptExecutionException; the host refuses subsequent evaluation.
Its ArrayBuffer and stack limits produce ordinary script errors. None of these
settings is a process/native-memory quota: V8 can overshoot sampled limits,
allocate native/JIT memory or terminate the worker at a hard engine constraint.
The browser's existing per-renderer OS quotas, IPC deadlines and crash/restart
supervision remain required before eventual page execution. In-process local
use is for trusted development inputs only.

## Native deployment and confinement validation

The engine project pins matching managed/native packages centrally. Consumers
receive the native runtime assets transitively, without a hand-written library
search path or a dependency on a platform backend. The dedicated
[V8Smoke](../tools/V8Smoke/) tool composes the host with the existing confinement
launchers. It verifies actual evaluation, isolate separation, primitive limits,
external allocation rejection, monitored heap invalidation, infinite-loop
interruption and fresh-isolate recovery. Phase 11b also probes native classic
lexical persistence/isolate separation and whole-batch deadline interruption.
Phase 11c probes live title/text mutations and hidden callback state through
the same unchanged confinement launcher.

```sh
dotnet build VisualWeb.slnx
dotnet test tests/Engine.Scripting.Tests/Engine.Scripting.Tests.csproj --no-build
dotnet run --no-build --project tools/V8Smoke -- --local
dotnet run --no-build --project tools/V8Smoke -- \
  --require-sandbox --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

Linux x64 uses the **unchanged** bubblewrap/seccomp/cgroup profile, read-only
deployment and existing cleared runtime environment. The supplied font is a
launcher mount prerequisite, not a scripting dependency. There is no syscall
allowlist relaxation, writable host mount, debugger socket or unconfined retry.
Linux ARM64 now uses the same required confinement guarantees with an
architecture-specific syscall policy. Successful native ARM64 confined V8
startup/execution has not been observed here; see the
[Linux confinement evidence boundary](linux-confinement.md#arm64-implementation-and-evidence-boundary).

Windows uses the existing AppContainer/Job Object staging and limits. Native
Windows x64/arm64 CI now runs host tests and the required-confinement V8 probe;
Linux results are not Windows certification. Linux CI runs host tests and local
V8 smoke on x64/arm64; its runner does not certify required Linux confinement.

Official references are cached as `clearscript-v8`, `clearscript-v8-constraints`
and `ecmascript`; `html` and `webidl` describe deferred integration. The pinned
ClearScript 7.5.1 source documents the API used by its 7.5.1.1 packaging update.
Cache refresh is independent of native tests.

## Detached node cloning (phase 11v)

Branded non-Document wrappers expose `cloneNode(deep = false)`. The Boolean
argument uses JavaScript ToBoolean (no string/value conversion), and extra
arguments are ignored. A successful call returns a new detached native node
with a fresh wrapper identity; source parent/siblings/attributes/data remain
unchanged. Elements preserve ordered attributes and names, CharacterData and
PI/doctype nodes preserve raw payloads, fragments clone children when deep,
and native Document clones preserve mode while assigning the clone as owner
of cloned descendants. Parent links and synthetic event listeners are not
copied. Script elements in clones are inert and are not added to the initial
script-source snapshot.

Before allocating a copy, the bridge checks available wrapper identity and
preflights the entire copied tree for deep calls (**8,192 nodes including the
root**), each element's **128 attribute/65,536 stored-character** caps, every
individual primitive (**65,536 UTF-16 units**) and the shared **262,144
task-character** budget, with cancellation checkpoints. Shallow calls inspect
only the root's payload/attributes, not descendants. The final opaque identity
output and **4,096 callback** budget still apply; no identity is registered
until output preflight succeeds. A failed preflight leaves source state
unchanged, and no inaccessible clone is attached to the document. Later
navigation of copied descendants consumes the normal lifetime identity cap.

The script bridge explicitly rejects cloning its canonical Document facade:
it is the single bound document and a cloned document's tree/owner facade is
not separately script-bindable. The native Engine.Dom API does support
independent shallow/deep Document copies with mode and owner remapping. Full
Document bindings, events/listener cloning, custom-element cloning steps and
WebIDL constructor semantics remain deferred.

## Phase-11a validation outcome

On Linux x64, **45 projects build** and **99 selected tests pass** without
failures or skips: 24 native scripting tests and the complete 75-test browser
suite. Local and required-confinement V8 smoke pass, including monitored heap
invalidation and deadline recovery. Confined X11 browser smoke still presents
exact page pixels with tabs/history/multiple windows. New-file formatting and
editor diagnostics are clean, all **58 official references** are independently
cached/fresh, and no owned V8/renderer workers or resource scopes remain.

Linux arm64 and Windows x64/arm64 native assets are present in deployment and
their CI checks are wired, but were not executed on this Linux x64 host.

## Phase-11b validation outcome

On Linux x64, all **45 projects build** and **37 scripting tests pass** without
failures or skips (24 existing host tests and 13 classic-script cases). Local
and unchanged required-confinement V8 probes pass, including persistent global
lexical state, isolate separation, monitored heap invalidation and whole-batch
deadline recovery. Changed-file formatting and editor diagnostics are clean.
No browser rendering or IPC behavior changes were made; native Windows/ARM
evidence still requires the already-wired target CI.

## Phase-11c validation outcome

On Linux x64, all **45 projects build** and **65 selected tests pass** without
failures or skips: the complete 46-test scripting suite and 19-test native DOM
suite. Coverage includes live title/text mutation, stable detached wrappers,
exclusive document ownership, adoption rejection, receiver/conversion errors,
callback privacy and exact wrapper, traversal, callback and text budgets.
The wrapper-count test uses an explicit 30-second host deadline so its repeated
tree lookups measure identity capacity rather than the default execution timeout.

Local and unchanged required-confinement V8 probes pass with live DOM callbacks
and no exposed CLR node/type or temporary callback global. Changed-file
formatting, editor diagnostics and diff whitespace checks are clean; no owned
V8/renderer workers or resource scopes remain. Browser page-script execution
remains disabled. Native Windows/ARM execution was not performed on this host.

## Phase-11d validation outcome

On Linux x64, all **45 projects build** and **172 distinct selected tests pass**
without failures or skips: 107 browser cases (including 32 inline-page cases),
19 IPC cases and all 46 scripting cases. The final source-snapshot mutation case
was added and validated separately after the complete regression run.
Coverage includes disabled-by-default rendering, exact source budgets/type
classification, live title/style mutations reaching pixels, fresh navigation
contexts, retained DOM on resize, transactional errors, worker-loss reload
requirements and deadline recovery inside unchanged required confinement.

Opt-in single-process SDL dummy and required-confined multiprocess X11 browser
smoke pass, checking actual native pixels, scripted titles, navigation history,
tabs and multiple windows. Formatting, editor diagnostics and diff whitespace
checks are clean; no owned renderer workers or resource scopes remain.
Independently, all **58 official references** are cached/fresh with no network
refresh needed. Native Windows/ARM checks are wired in target CI but were not
executed on this Linux x64 host. Page scripts remain disabled unless explicitly
enabled; external scripts, HTML scheduling/event loops and production web
security remain unfinished.

## Phase-11e validation outcome

On Linux x64, all **45 projects build** and **207 selected tests pass** without
failures or skips: 77 scripting, 19 native DOM and 111 browser cases.
The new cases cover live attributes and missing/empty distinctions, branded
element/text/fragment creation, node identity and navigation, fragment draining/
move/replace semantics, receiver/conversion/hierarchy/adoption failures and
exact lifetime-wrapper, attribute-storage and destination-subtree bounds.
Local/process page tests verify new inline attributes and inserted style/layout
nodes reach exact pixels, newly inserted scripts stay inert, and initially
collected scripts still execute after detachment.

Local and unchanged required-confined V8 probes pass with attribute/Node/
fragment mutations. Opt-in local SDL dummy and required-confined X11 shell
smokes pass with script-mutated title/pixels and tab/history/window lifecycle.
Formatting, editor diagnostics and whitespace checks are clean; no owned
renderer/V8 workers or resource scopes remain. All **58 official references**
are independently cached/fresh with no network refresh needed.
Windows/ARM checks remain target-CI work, not evidence from this Linux x64 host.
The inline-only initial snapshot, disabled-by-default browser policy and
unfinished HTML event loop/security boundaries are unchanged.

## Phase-11f validation outcome

On Linux x64, all **45 projects build**. **207 distinct selected tests pass**
without failures/skips: 92 scripting and 115 browser cases. After the final
observation/checkpoint fix, all 92 scripting and 40 inline-page cases were rerun.
Coverage includes native FIFO/nested Promise ordering, checkpoints between
classic sources, exact 1,024 pending/4,096 recursive/shared-batch limits, caught
quota latching, callback arguments/ignored thenables, intrinsic tampering,
shared DOM budgets, isolate separation and explicit task invalidation.
Infinite native callbacks and recursively replenished Promise jobs interrupt
under the shared deadline; in-flight cancellation also invalidates the host.
Observation errors cannot mask a latched callback failure.

Local/process pages verify exact checkpoint title/blue pixels, retained resize
and failed-task preservation/recovery. Local and unchanged required-confined V8
probes pass. Opt-in SDL dummy and required-confined X11 browser smokes pass
with microtask-driven title/pixels and tab/history/window lifecycle.
Formatting, editor diagnostics and whitespace checks are clean; no owned
workers/resource scopes remain. All **58 official references** are independently
cached/fresh without network refresh. Windows/ARM guarantees still require
native target evidence. External scripts, timers, events, ordinary Promise
rejection reporting and a persistent HTML event loop remain deferred.

## Phase-11g validation outcome

On Linux x64, all **45 projects build** and **246 distinct selected tests pass**
without failures or skips: 127 scripting and 119 browser cases. Following the
final empty-dictionary/prototype hardening, all 127 scripting and 44 inline-page
cases were rerun. Event coverage measures exact listener, invocation, nested
dispatch, ancestor-path and type-character boundaries, propagation/once/passive/
removal/addition semantics, mutation during dispatch, callback objects and
ignored returns, cleanup/redispatch, failure latching, detached/adopted nodes,
intrinsic/prototype tampering, private isolates and deadline/cancellation.

Local/process pages verify exact synthetic-event/listener-microtask title/blue
pixels and retained resize, plus caught-listener-failure preservation and fresh
navigation recovery. Local and unchanged required-confined V8 probes pass with
capture/target/bubble/cancellation and listener-enqueued microtask mutation.
Opt-in SDL dummy and required-confined X11 shell smokes pass with event-driven
title/pixels and tab/history/window lifecycle. Formatting, editor diagnostics
and whitespace checks are clean; no owned worker/resource scopes remain.
All **58 official references** are independently cached/fresh with no network
refresh needed. Windows/ARM evidence remains native target-CI work. Automatic
browser events, handlers/default actions, AbortSignal, external scripts, timers,
Promise rejection reporting and a persistent HTML event loop remain deferred.

## Phase-11h validation outcome

On Linux x64, all **45 projects build** and **265 selected tests pass** without
failures or skips: 140 scripting and 125 browser cases. Lifecycle cases cover
loading/interactive/DOMContentLoaded/complete ordering and trusted event flags,
readonly readiness, private dispatch despite caller/prototype tampering,
one-shot/prevalidation/empty-batch behavior, stage checkpoints and source
failure suppression. Exact shared invocation/microtask limits, shared DOM
budgets, whole-navigation deadlines and in-flight lifecycle cancellation are
verified; checkpoint failures stop later stages even through native Promise jobs.

Local/process pages verify final readiness-listener/microtask title/blue pixels,
retained resize, empty-source pages, transactional DOMContentLoaded/complete
failures and fresh navigation recovery. Local and unchanged required-confined
V8 probes pass with finite readiness checkpoints. One concurrent native heap
probe reached its existing deadline while reporting heap exhaustion; rerunning
local/confined probes without competing validation passed without changing
limits. SDL dummy and required-confined X11 shell smokes pass with
DOMContentLoaded-driven title/pixels and tab/history/window lifecycle.
Formatting, editor diagnostics and whitespace checks are clean; no owned
workers/resource scopes remain. All **58 official references** are independently
cached/fresh without network refresh. Windows/ARM still require native target
evidence. This remains a finite post-parse approximation: Window/load, input
events, timers, external scripts, ordinary Promise rejection reporting and a
persistent event loop are deferred.

## Phase-11i validation outcome

On Linux x64, all **45 projects build** and **493 distinct selected tests pass**
without failures or skips: 162 scripting, 204 CSS and 127 browser cases.
Following cancellation hardening, all 162 scripting and 52 inline-page cases
were rerun; the final exact selector-depth and reserved-identity checks also
pass with the full scripting suite and both lifecycle-query integration cases.
Coverage includes scoped tree order, static lists with live identities,
matches/closest, invalid and unsupported selectors, detached/adopted nodes,
intrinsic tampering, atomic identity reservation, exact traversal/result/depth
bounds, shared matching budgets and deadline/cancellation invalidation.

Local/process pages verify lifecycle-query title/blue pixels, retained resize
and transactional query-failure preservation. Local and unchanged
required-confined V8 probes pass. SDL dummy and required-confined X11 browser
smokes pass with query-driven mutations and tab/history/window lifecycle,
presenting 10 and 15 frames respectively. Initial concurrent validation
encountered existing default deadlines; sequential reruns passed without
weakening limits. Formatting and whitespace checks are clean; the editor reports
style/complexity advisories in the DOM bridge, not build failures. No owned
workers/resource scopes remain. All **58 official references**
are independently cached/fresh without network refresh. Windows/ARM still
require native target evidence. Unsupported selectors, full WebIDL NodeList,
external scripts and a persistent event loop remain deferred.

## Phase-11j validation outcome

On Linux x64, all **45 projects build** and **316 selected tests pass** without
failures or skips: 187 scripting and 129 browser cases. The 25 class-token cases
cover same-object reflection, external/detached changes, ordered normalization
and replacement, force/no-op behavior, conversion side effects and atomic
validation failures, ASCII/non-ASCII whitespace, readonly indexed descriptors,
live iteration, receiver/adoption/tampering checks, exact token/argument/storage
and shared callback boundaries, shared input text budgets and interruption.
Large-value assertions use separate execution tasks to respect, rather than
weaken, the existing aggregate DOM text limit.

Local/process lifecycle class-selector fixtures verify exact blue pixels,
title, retained resize and transactional failed-navigation preservation with
fresh-page recovery. Local and unchanged required-confined V8 probes pass with
class token mutation/query/iteration. SDL dummy and required-confined X11
browser smokes pass with lifecycle-driven class changes, presenting 10 and 15
frames respectively and exercising tabs/history/windows. Formatting, editor
diagnostics and whitespace checks are clean; no owned workers/resource scopes
remain. All **58 official references** are independently cached/fresh without
network refresh. Windows/ARM still require native target evidence. Full
DOMTokenList WebIDL/DOMException objects, external scripts and persistent V8
event loops remain deferred.

## Phase-11k validation outcome

On Linux x64, all **45 projects build** and **328 selected tests pass** without
failures or skips: 197 scripting and 131 browser cases. Ten tree-inspection
cases measure inclusive containment/identity, detached/fragment roots, live
element-only navigation, dictionary getter ordering and failures, receiver/
both-operand adoption checks, intrinsic tampering, exact 8,192-node ancestor/
child/sibling boundaries, 1,024-wrapper capacity, shared callbacks and in-flight
cancellation. Lifecycle fixtures in local and process renderers verify exact
title/blue pixels, retained resize and failed-navigation preservation/recovery.

Local and unchanged required-confined V8 tree-inspection probes pass.
The first local run hit a fatal native OOM during the pre-existing heap stress
probe; isolated local/confined reruns passed with no limit changes. This
transient native failure remains a reason to require renderer process isolation,
not a guarantee that monitored V8 heap interruption always precedes native OOM.
SDL dummy and required-confined X11 shell smokes pass, presenting 10 and 15
frames respectively. Formatting, editor diagnostics and whitespace checks are
clean; no owned workers/resource scopes remain. All **58 official references**
are independently cached/fresh without network refresh. Windows/ARM still
require native target evidence. Shadow DOM, live child collections, full
WebIDL and persistent script event loops remain deferred.

## Phase-11l validation outcome

On Linux x64, all **45 projects build** and **7,782 selected tests pass**
without failures or skips: 31 DOM, 7,202 HTML, 204 CSS, 212 scripting and 133
browser cases. Twelve native toggle/order cases and 15 reflection cases cover
live ID lookup/selector effects, raw DOMString conversion, optional Boolean
force, forced no-op value retention/validation, name conversion side effects,
invalid receivers/symbols/adoption, intrinsic tampering, exact attribute/
storage/shared callback bounds, aggregate input text and cancellation.
Attribute removal/readdition now appends in order; native replacement keeps
position. Dependent parser/style suites verify the ordered-storage change.

Local/process lifecycle fixtures verify exact ID/attribute-selector title/blue
pixels, retained resize and failed-navigation preservation/fresh recovery.
Local and unchanged required-confined V8 probes pass with reflected ID and
attribute-toggle selector checks. SDL dummy and required-confined X11 shell
smokes pass with lifecycle-driven ID/toggle/class mutations, presenting 10
and 15 frames respectively. Formatting, editor diagnostics and whitespace
checks are clean; no owned workers/resource scopes remain. All **58 official
references** are independently cached/fresh without network refresh.
Windows/ARM still require native target evidence. Full WebIDL/DOMException,
custom-element reactions and persistent script event loops remain deferred.

## Phase-11m validation outcome

On Linux x64, all **45 projects build** and **391 distinct selected tests pass**
without final failures or skips: 34 DOM, 222 scripting and 135 browser cases.
Three native and ten script CharacterData cases cover UTF-16/surrogate/NUL
storage, unsigned offset conversion/count clamping, data/nodeValue reflection,
comment/PI branding, ordered conversion side effects, receiver/adoption/
tampering, exact storage/result/shared input-output/callback bounds and
cancellation. After the final null-versus-undefined conversion correction,
all 222 scripting and 60 inline-page cases were rerun successfully.

Regression validation exposed finite whole-batch/lifecycle tasks succeeding
when the cancellation timer was delayed. Monotonic stage/return checks now
reject expired tasks without relaxing deadlines; both existing deadline
regressions and the complete scripting/browser suites pass.
Local/process lifecycle style/title edits preserve text-node identity and
produce exact blue pixels, retained resize and transactional failure/recovery.
Local and unchanged required-confined V8 probes pass with in-place
CharacterData edits. SDL dummy and required-confined X11 shell smokes pass,
presenting 10 and 15 frames respectively. Formatting, editor diagnostics and
whitespace checks are clean; no owned workers/resource scopes remain.
All **58 official references** are independently cached/fresh without network
refresh. Windows/ARM still require native target evidence. Observers,
live-range/PI pseudoattribute reactions, full WebIDL/DOMException objects
and persistent script event loops remain deferred.

## Phase-11n validation outcome

On Linux x64, all **45 projects build** and **407 selected tests pass** without
failures or skips: 37 DOM, 233 scripting and 137 browser cases. Three native
and eleven scripted Text cases cover UTF-16/surrogate splitting, detached/
fragment/empty boundary behavior, contiguous runs with non-Text barriers,
conversion order, brands/adoption/intrinsic tampering, original listeners,
exact data/result/sibling/subtree/identity bounds, atomic output/shared-call
failures and cancellation.

Local/process lifecycle fixtures split style/title text and verify fresh suffix
and original identities, exact blue pixels/title, retained resize and
transactional failed-navigation preservation/recovery. Local and unchanged
required-confined V8 probes pass with split/wholeText sibling identity checks.
SDL dummy and required-confined X11 shell smokes pass, presenting 10 and 15
frames respectively. Formatting, editor diagnostics and whitespace checks are
clean; no owned workers/resource scopes remain. All **58 official references**
are independently cached/fresh without network refresh. Windows/ARM still
require native target evidence. CDATA, observers/live-range repair,
normalization, full WebIDL and persistent script event loops remain deferred.

## Phase-11o validation outcome

On Linux x64, all **45 projects build** and **423 selected tests pass** without
failures or skips: 41 DOM, 243 scripting and 139 browser cases. Four native
and ten scripted normalization cases cover UTF-16 concatenation, first-nonempty
identity, detached data/ownership/listeners, barriers and nested document/
fragment trees, leaf no-ops, idempotence, ignored arguments, receiver brands,
adoption, exact surviving-run storage/descendant/shared-text/callback limits,
whole-subtree atomic rejection, full identity capacity, captured intrinsics
and cancellation. Native deep trees use iterative traversal.

Local/process lifecycle fixtures normalize style/title runs and verify exact
blue pixels/title, original and detached identities, retained resize and
transactional failed-navigation preservation/recovery. Local and unchanged
required-confined V8 probes pass with normalization identity/data checks.
SDL dummy and required-confined X11 shell smokes pass, presenting 10 and 15
frames. The first X11 run hit the existing five-second systemd scope-stop
timeout; inspection confirmed the scope inactive and no workers remaining,
then an isolated rerun passed without changing any confinement setting.

Formatting, editor diagnostics and whitespace checks are clean. All **58
official references** are independently cached/fresh without network refresh.
Windows/ARM still require native target evidence. Observers/live-range repair,
CDATA, custom-element reactions, full WebIDL and persistent event loops remain
deferred; page scripting is still opt-in.

## Phase-11p validation outcome

On Linux x64, all **45 projects build** and **441 selected tests pass** without
failures or skips: 46 DOM, 254 scripting and 141 browser cases. Five native
and eleven scripted equality cases cover unordered attribute insertion,
ordered children/Text segmentation, exact UTF-16 data, supported leaf
interfaces/doctype fields, cross-document native comparison/document mode,
null/default arguments, brand/ownership rejection, exact tree/attribute/
individual/shared-text/Boolean-output/callback limits, full identity capacity,
listener independence, captured intrinsics and cancellation. Native deep
comparison uses iterative traversal.

Local/process lifecycle fixtures compare live style/title edits and verify
exact blue pixels/title, distinct structural/identity semantics, retained
resize and transactional failed-navigation preservation/recovery. Local and
unchanged required-confined V8 probes pass with Text equality/null/self checks.
SDL dummy and required-confined X11 shell smokes pass, presenting 10 and 15
frames respectively. Formatting, editor diagnostics and whitespace checks are
clean; no owned workers/resource scopes remain. All **58 official references**
are independently cached/fresh without network refresh. Windows/ARM still
require native target evidence. Cloning, namespace-aware Attr/CDATA, shadow
DOM, full WebIDL and persistent event loops remain deferred.

## Phase-11q validation outcome

On Linux x64, all **45 projects build** and **455 selected tests pass** without
failures or skips: 49 DOM, 263 scripting and 143 browser cases. Three native
and nine scripted metadata cases cover every supported Node name/owner,
ASCII-only HTML casing with non-ASCII/colon names, detached/normalized ownership,
native adoption and script rejection, readonly descriptors/borrowed receiver
brands, exact element/instruction/doctype name output limits, shared text and
callback exhaustion, full identity capacity, captured intrinsics and
cancellation. Element uppercase allocation follows output-budget preflight.

Local/process lifecycle fixtures verify metadata-guided style/title changes,
exact blue pixels/title, transactional readonly-assignment failure, retained
resize and fresh-navigation recovery. Local and unchanged required-confined
V8 probes pass with Element/Text/Document metadata and owner identity checks.
SDL dummy and required-confined X11 shell smokes pass, presenting 10 and 15
frames respectively. Formatting, editor diagnostics and whitespace checks are
clean; no owned workers/resource scopes remain. All **58 official references**
are independently cached/fresh without network refresh. Windows/ARM still
require native target evidence. Namespace-aware factories/XML documents,
full WebIDL and persistent event loops remain deferred.

## Phase-11r validation outcome

On Linux x64, all **45 projects build** and **416 selected tests pass** without
failures or skips: 271 scripting and 145 browser cases. Eight scripted
attribute-inspection cases cover fresh mutable ordered arrays, replacement/
removal/readdition, unusual UTF-16 names, ignored arguments, receiver brands/
adoption, exact count/encoded/shared-text/callback limits, oversized native
values, full identity capacity, captured intrinsics/inherited index setters
and cancellation. No native DOM algorithm changes are introduced.

Local/process lifecycle fixtures preserve recovered parser attribute names,
observe ordered live mutations, and verify exact blue pixels/title,
transactional bad-receiver failure, retained resize and fresh-navigation
recovery. Local and unchanged required-confined V8 probes pass with attribute
presence/fresh-name checks. SDL dummy and required-confined X11 shell smokes
pass, presenting 10 and 15 frames respectively. Formatting, editor diagnostics
and whitespace checks are clean; no owned workers/resource scopes remain.
All **58 official references** are independently cached/fresh without network
refresh. Windows/ARM still require native target evidence. Attr/NamedNodeMap,
namespace-aware duplicates, full WebIDL and persistent event loops remain
deferred; page scripting is still opt-in.

## Phase-11s validation outcome

On Linux x64, all **45 projects build** and **7,679 selected tests pass** without
failures or skips: 51 DOM, 7,202 HTML, 279 scripting and 147 browser cases.
Two native and eight scripted factory cases cover raw UTF-16 comments,
XML Name/initial instruction data validation (including empty-target DOM
errors), required receivers and ordered conversions, readonly target brands/
adoption, exact field/target/shared-output/callback/identity limits, failure
without identity reservation, non-Text barriers, listener independence,
captured intrinsics and cancellation.

Local/process lifecycle fixtures create inert comment/instruction style-text
barriers and verify exact blue pixels/title, document/sibling identity,
transactional invalid-target failure, retained resize and fresh-navigation
recovery. Local and unchanged required-confined V8 probes pass with factory/
target/CharacterData checks. SDL dummy and required-confined X11 shell smokes
pass, presenting 10 and 15 frames respectively. Formatting, editor diagnostics
and whitespace checks are clean; no owned workers/resource scopes remain.
All **58 official references** are independently cached/fresh without network
refresh. Windows/ARM still require native target evidence. Instruction
pseudoattributes/reactions, special stylesheet loading, full constructors/
WebIDL and persistent event loops remain deferred.

## Phase-11t validation outcome

On Linux x64, all **45 projects build** and **491 selected tests pass** without
failures or skips: 53 DOM, 289 scripting and 149 browser cases.
Two native and ten scripted removal cases cover valid ChildNode types,
detached/repeated no-ops, first/middle/last sibling repair, root/doctype removal,
retained data/subtree/ownership/query identities, argument nonconversion,
unsupported/fake/adopted brands, unscopables and intrinsic tampering.
Exact **8,192/8,193 parent-descendant and ancestor limits**, **1,024 lifetime
identities** and **4,096 shared callbacks** are checked against atomic native
tree state. Oversized unread payloads and exhausted shared output budgets do
not block removal; removed identities are not recycled. Listener/path retention
and cancellation/host invalidation also pass.

Local/process lifecycle fixtures remove overriding styles and title text,
then verify exact blue pixels/title, retained detached listeners, failed-candidate
preservation, resize without reexecution and fresh-navigation recovery.
Local and unchanged required-confined V8 probes pass with native self-removal
checks. SDL dummy and required-confined X11 shell smokes pass, presenting 10
and 15 frames respectively. Formatting, changed-file editor diagnostics and
whitespace checks are clean; no owned workers/resource scopes remain.
All **58 official references** are independently fresh without network refresh.
Native Windows/ARM evidence, full WebIDL, observers/live ranges/custom-element
reactions and persistent event loops remain deferred; scripting is still opt-in.

## Phase-11u validation outcome

On Linux x64, all **45 projects build** and **506 selected tests pass** without
failures or skips: 55 DOM, 300 scripting and 151 browser cases.
Two native and eleven scripted doctype cases cover leading-comment lookup,
same-wrapper identity, null absence, raw case/UTF-16 fields, readonly getters,
wrong/fake/inherited/adopted receivers, removal/reinsertion and unchanged mode.
Exact **65,536/65,537 individual output**, **262,144 shared output**,
**8,192/8,193 document children**, **1,024 lifetime identities** and **4,096
callbacks** are verified. Lookup ignores oversized descendant trees/metadata;
individual reads ignore other fields; absent lookup consumes no identity.
Failed lookup output reserves no identity and leaves the tree unchanged.
Captured brands, private-callback invisibility and cancellation invalidation pass.

Local/process lifecycle fixtures inspect the parser-supported
`about:legacy-compat` system identifier through a leading comment, remove and
reinsert the doctype, and verify exact blue pixels/title, transactional strict
readonly-write failure, retained resize and fresh-navigation recovery.
The initial PUBLIC fixture explicitly hit existing unsupported legacy-mode
selection; it was corrected without weakening parser behavior.
Local and unchanged required-confined V8 metadata probes pass. SDL dummy and
required-confined X11 shell smokes pass with 10 and 15 presented frames.
Formatting, changed-file editor diagnostics and whitespace checks are clean;
no owned workers/resource scopes remain. All **58 official references** are
independently fresh without network refresh.
Native Windows/ARM evidence, legacy parser mode selection, DTD fetching,
DOMImplementation, full WebIDL and persistent event loops remain deferred;
page scripting remains opt-in.

## Phase-11v validation outcome

On Linux x64, all **45 projects build** and **520 selected tests pass** without
failures or skips: 58 DOM, 309 scripting and 153 browser cases. Three native
and nine scripted clone cases cover shallow/deep interfaces, ordered attributes,
CharacterData/PI/doctype data, fragments, native Document mode/owner remapping,
detached roots, fresh identity, uncopied listeners, Boolean conversion,
receiver brands, and explicit script Document rejection.
Exact **8,192/8,193 subtree nodes**, **128 attributes**, **65,536 per-field/
attribute storage**, **262,144 shared task characters**, and **1,024 wrapper
identities** are exercised. Shallow clones skip descendants; failed limits
leave source trees and wrapper allocation unchanged. Cancellation during a
bounded deep copy interrupts V8 and preserves the source tree.

Local/process lifecycle tests show a detached deep-cloned style has no effect
until insertion, then applies exact blue pixels; failed Document-clone
candidates preserve the committed state, and retained resize/fresh navigation
preserve prior lifecycle guarantees. Local and unchanged required-confined V8
probes pass. SDL dummy and required-confined X11 shell smokes pass with 10/15
presented frames. Formatting, changed-file editor diagnostics and whitespace
checks are clean; no owned workers/resource scopes remain. All **58 official
references** are independently fresh without network refresh. Native
Windows/ARM evidence, script Document cloning, listener/custom-element clone
steps, full WebIDL and persistent event loops remain deferred; page scripts are
still opt-in.
