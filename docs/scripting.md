# V8 host and classic-script foundation (phases 11a–11b)

The approved embedding is **Microsoft ClearScript V8 7.5.1.1**, with matching
native packages for Linux/Windows x64 and arm64. Engine.Scripting now provides a
small renderer-local native host, not a web-browser JavaScript environment.
**The static browser still does not execute page scripts.** DOM/Web IDL bindings,
HTML script scheduling and event loops, modules, timers, workers and script-visible
network/storage APIs are deferred. No renderer IPC shape or browser launch mode
changes in this stage.

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

The host installs **no CLR objects/types**, enables no reflection/debugger,
adds no browser bindings and disables document file/web loading. SharedArrayBuffer,
Atomics and WebAssembly globals are removed in this initial subset: blocking
shared-memory waits and Wasm native compilation are not certified by these
execution limits. No task/promise/array host-conversion flags are enabled.
Native ECMAScript built-ins are not a browser event loop or a sandbox.

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
