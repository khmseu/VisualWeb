# V8 host, DOM bindings and opt-in inline pages (phases 11a–11d)

The approved embedding is **Microsoft ClearScript V8 7.5.1.1**, with matching
native packages for Linux/Windows x64 and arm64. Engine.Scripting now provides a
small renderer-local native host, not a web-browser JavaScript environment.
**Page scripts remain disabled by default.** Phase 11d explicitly opts into the
post-parse inline subset below. Full DOM/Web IDL bindings,
HTML script scheduling and event loops, modules, timers, workers and script-visible
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
with a fixed `(operation, integer identity, string) -> string` contract. Trusted
bootstrap captures it in a closure and deletes its temporary global **before
any caller script**. The document and elements themselves are never imported
into ClearScript. A string status/value crosses back, never a native node, CLR
exception, type or arbitrary object. Ordinary bridge failures become JavaScript
TypeErrors with explicit diagnostics. No callback or identity table is exposed
as a property of a wrapper.

Facades are native JavaScript objects with private WeakMap brands, stable
identity and captured pristine intrinsics. Document/element receivers are
checked; borrowing a getter/method onto an arbitrary object fails. Prototypes
are intentionally minimal/frozen and have null roots; document has no prototype.
This is **not full Web IDL prototype/constructor conformance**. Window,
Document/Element constructors, `document.body`, attributes, tree-mutation
methods, events, observers and generated IDL bindings remain unavailable.
Expandos are ordinary JavaScript properties, not native DOM mutations.

| Binding budget | Value |
| --- | ---: |
| Retained element wrappers per host | 1,024 |
| Callbacks per evaluation/single script/whole batch | 4,096 |
| Traversed nodes per lookup/text/title operation | 8,192 |
| UTF-16 text per callback argument/result | 65,536 |
| Aggregate callback argument/result UTF-16 characters per execution | 262,144 |

Budgets are checked before writes; traversal/text assembly is bounded. Counters
reset per execution, but wrapper identities persist until host disposal.
Caller conversion hooks run in V8 under the normal deadline. Host callbacks
are synchronous finite DOM work, not network/storage or arbitrary CLR execution.
These limits do not substitute for process quotas or constitute an exploit
boundary. Phase 11d installs these bindings only for explicitly enabled inline
page execution; this remains trusted-content development tooling.

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

After execution, the host is disposed. Stylesheets are collected from the
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

There is still no Window/global browser API, event-handler dispatch, timer,
HTML event loop, module loader, origin/CSP enforcement or script-visible
network/storage. Native ECMAScript built-ins are not an HTML task/microtask
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
values are explicit exceptions but leave the context available.
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
