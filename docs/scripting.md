# V8 host, DOM bindings and opt-in inline pages (phases 11a–11i)

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

| Event budget | Bound |
| --- | ---: |
| Registered listener entries per host, across all targets | 1,024 |
| Listener invocations per evaluation/single script/whole batch | 4,096 |
| Nested simultaneous dispatches | 32 |
| Snapshotted targets per ancestor path, including target/document | 1,024 |
| UTF-16 event/listener type characters | 65,536 |

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

| Binding budget | Value |
| --- | ---: |
| Retained non-document node wrappers per host, including detached/created nodes | 1,024 |
| Callbacks per evaluation/single script/whole batch | 4,096 |
| Descendants per traversal/destination subtree; siblings per scan | 8,192 |
| Ancestor chain per connectivity/mutation check, including receiver | 8,192 |
| UTF-16 text per callback argument/result | 65,536 |
| Aggregate callback argument/result UTF-16 characters per execution | 262,144 |
| Attributes per script-mutated element | 128 |
| Stored attribute name/value UTF-16 characters per script-mutated element | 65,536 |

Budgets are checked before writes; traversal/text assembly is bounded. Counters
reset per execution, but wrapper identities persist until host disposal.
Caller conversion hooks run in V8 under the normal deadline. Host callbacks
are synchronous finite DOM work, not network/storage or arbitrary CLR execution.
These limits do not substitute for process quotas or constitute an exploit
boundary. Phase 11d installs these bindings only for explicitly enabled inline
page execution; this remains trusted-content development tooling.

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

| Per-query budget | Bound |
| --- | ---: |
| Selector UTF-16 characters | 4,096 |
| CSS tokens | 8,192 |
| Selector chain/nesting/evaluation depth | 64 |
| Shared matching operations across all candidates | 65,536 |
| Traversed descendants or inclusive ancestors | 8,192 |
| Static result entries | 1,024 |

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
explicitly with TypeError instead of silently returning no matches: `:scope`,
dynamic state, namespaces, `:has`, filtered nth-child and pseudo-elements remain
deferred. Rejecting pseudo-elements rather than returning an empty list is an
intentional finite-subset deviation. No selector cache, live collections,
innerHTML or dynamic script scheduling is added. Inline pages gain these APIs
under the existing opt-in; lifecycle, retention and confinement are unchanged.

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

Add `--enable-inline-scripts` to either explicit development browser mode.
It composes with `--require-sandbox` without changing OS profiles, mounts,
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

| Bound | Value |
| --- | ---: |
| Input UTF-16 characters per evaluation/classic script | 65,536 |
| Classic scripts per batch | 64 |
| Aggregate UTF-16 characters per batch | 262,144 |
| Copied String/BigInt UTF-16 characters | 16,384 |
| Evaluation/single-script/whole-batch deadline | 2 seconds by default; configurable `(0, 30]` seconds |
| Monitored V8 heap | 32 MiB, sampled every 10 ms |
| Old-space constraint | 128 MiB, no on-demand expansion |
| New-space constraint | 16 MiB |
| External ArrayBuffer allocation | 16 MiB per isolate |
| Script stack growth | 512 KiB |

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
Linux arm64 required confinement continues to fail closed.

Windows uses the existing AppContainer/Job Object staging and limits. Native
Windows x64/arm64 CI now runs host tests and the required-confinement V8 probe;
Linux results are not Windows certification. Linux CI runs host tests and local
V8 smoke on x64/arm64; its runner does not certify required Linux confinement.

Official references are cached as `clearscript-v8`, `clearscript-v8-constraints`
and `ecmascript`; `html` and `webidl` describe deferred integration. The pinned
ClearScript 7.5.1 source documents the API used by its 7.5.1.1 packaging update.
Cache refresh is independent of native tests.

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
