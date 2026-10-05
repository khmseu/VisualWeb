# V8 host foundation (phase 11a)

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

The only entry point evaluates bounded source through a private function's
captured **indirect eval**. Script global properties and `var` declarations may
persist in that context; eval-local `let`/`const` declarations do not implement
HTML's persistent script-global lexical environment. This is a host probe API,
not the future HTML classic-script evaluation algorithm.

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
| Input UTF-16 characters per evaluation | 65,536 |
| Copied String/BigInt UTF-16 characters | 16,384 |
| Evaluation deadline | 2 seconds by default; configurable `(0, 30]` seconds |
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
interruption and fresh-isolate recovery.

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
