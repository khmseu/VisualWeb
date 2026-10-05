# Linux renderer confinement

This guide documents the approved Linux x64 renderer confinement profile.
It is not a production-safe browser or completion of all cross-platform
phase-10 isolation. Linux arm64 sandbox-required launches fail closed. Existing
explicit local/unsandboxed development modes remain available. Windows has a
separate [AppContainer/Job Object profile](windows-confinement.md).
Phase 10c strengthens required confinement with per-worker cgroup v2 memory,
swap, task and CPU limits; see [hard resource accounting](#hard-resource-accounting).

## Run and prerequisites

Requires Linux x64, a framework-dependent .NET 10 installation, **bubblewrap
0.9.0 or newer** at `/usr/bin/bwrap`, libseccomp.so.2 with the listed syscall
names, and permission to create unprivileged user, mount, PID, network, IPC,
UTS and cgroup namespaces. Older/missing tools or restricted kernel policies
cause visible startup failure, never an unsandboxed retry. No root elevation,
setuid configuration or machine-wide kernel changes are performed by the app.
Hard resource accounting additionally requires systemd-run/systemctl at their
standard `/usr/bin` paths, a reachable systemd **user** manager, cgroup v2 and
delegated memory, pids and CPU controllers. Both the CLI/API and bootstrap
fail closed if the required controls cannot be installed and read back.

Build the solution, then run:

```sh
dotnet run --no-build --project src/Apps/VisualWeb.Browser -- \
  --development-multiprocess --require-sandbox \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

The DLL, its containing **dedicated trusted deployment directory**, runtime,
font and bubblewrap/libseccomp installations are trusted configuration.
Do not select a directory containing unrelated private files: the worker can
read its entire deployment directory. A supplied native apphost can launch
the same bootstrap, but the confined worker is always the framework-dependent
renderer DLL. Custom/self-contained/single-file deployment is not supported.

`--require-sandbox` is valid only with explicit multiprocess mode. Chrome says
**LINUX CONFINEMENT REQUIRED**, not production security. If initialization
fails, the tab receives a diagnostic and no page is sent to an unconfined
renderer. Without the flag the existing **NO SANDBOX** mode is unchanged.
The browser itself is privileged and unconfined; its GET broker and SDL chrome
remain outside the renderer sandbox.

## Bootstrap and ownership

[Platform.Linux.Sandbox](../src/Platform/Platform.Linux.Sandbox/) is a dedicated
OS-specific library with no SDL, browser or engine dependency. A new project
avoids importing window backends/native UI assets into the worker.

1. The browser launches the trusted bootstrap in a unique transient systemd
   user scope with required cgroup limits, cleared environment, disabled .NET
   diagnostics and private stdio. It has not sent page content.
2. The bootstrap exports a libseccomp BPF filter to an anonymous descriptor,
   sets resource limits, and `execv`s the fixed bubblewrap executable without
   shell command construction. Native handles and filters are released on failure.
3. Bubblewrap requires every configured namespace and disabled nested user
   namespaces, drops capabilities, establishes a new session, and installs the
   filter before executing the fresh .NET worker runtime.
4. The worker checks `NoNewPrivs: 1`, `Seccomp: 2`, zero effective capabilities
   and expected mount/runtime settings **before** opening its native page font.
   It also verifies read-only kernel cgroup limits. Only then does it send the
   profile-confirmed handshake.
5. The browser requires the exact `linux-bwrap-seccomp-cgroup-v2` profile. An absent/
   different profile, initialization failure or deadline invalidates the worker.
   No fallback is attempted.

The profile string is a configuration confirmation from the **trusted worker
binary**, not remote cryptographic attestation or proof from untrusted code.
The browser owns the scope launch process and tracks its unique scope unit.
The bootstrap becomes bubblewrap; its worker has a separate namespace PID.
`--die-with-parent` terminates the nested worker if bubblewrap or its parent
dies. Tab-close/cancel/deadline/crash cleanup stops the **owned unit**, then
terminates any remaining owned launcher tree. The next render creates a fresh
unit and reruns the complete resource/confinement bootstrap.

## Filesystem and environment

There is no bind of the host root, home, session sockets, browser working
directory or host temporary directory.

| Sandbox path | Source / access |
| --- | --- |
| `/app` | Trusted renderer deployment directory, read-only |
| `/font/font` | One explicitly selected trusted font file, read-only |
| `/runtime/dotnet` | Active runtime's host, read-only |
| `/runtime/host/fxr` | Matching installation's host resolver directory, read-only |
| `/runtime/shared/Microsoft.NETCore.App/<version>` | Only the active shared runtime directory, read-only |
| `/usr/lib`, optional `/usr/lib64` | Host native system libraries, read-only; no `/usr/bin` |
| `/lib`, optional `/lib64` | Symlinks into the mounted system library trees |
| `/etc/ld.so.cache` | Native loader cache only, read-only when present |
| `/proc` | Private PID namespace procfs, read-only |
| `/resource-limits` | Only this worker's kernel cgroup directory, read-only |
| `/dev` | Bubblewrap's synthetic minimal devices, read-only mount |
| `/tmp` | Private 64 MiB tmpfs, writable, discarded on worker exit |
| `/` | Read-only mount skeleton |

Read-only device mounting still permits normal I/O to the synthetic null/random
devices; it prevents creating extra writable files in that mount. The system
library trees and trusted deployment files are deliberately readable, not
hidden secrets. Mounts are not content-supplied paths.

Bubblewrap clears the environment again, installing only explicit language,
private HOME/TMPDIR, runtime root and .NET runtime configuration values.
Host credentials, proxies, display/session sockets and .NET profiler/startup
hook settings are not inherited. Runtime diagnostics are disabled before the
confined runtime starts.

## Syscalls and resource limits

The x64 libseccomp policy defaults to **EPERM** for unlisted syscalls and rejects
other architectures. Allowed calls support file access within mount policy,
stdio, managed/native memory and JIT, synchronization, thread signals,
time/identity queries, polling and private temporary-file operations.

Socket creation (including Unix sockets), networking, ptrace/process-memory
access, mount/unmount, unshare/setns, BPF, io_uring, fork/vfork and process
clones are not allowed. `clone` requires `CLONE_THREAD`, zero exit signal and
no namespace/ptrace/untraced flags. `clone3` returns ENOSYS for libc's fallback
to the filtered `clone`. CPU-affinity changes are limited to the calling
thread. CoreCLR synchronization uses membarrier and a bounded locked page.

`execve` remains allowed for initial runtime startup; replacing the worker
does not remove its inherited seccomp, namespaces or resource limits.
There is no exposed general executable tree or permission to create a child
process. Seccomp alone is not the filesystem sandbox: it is combined with
namespace/mount policy.

| Bound | Value |
| --- | ---: |
| RLIMIT_CORE | 0 |
| RLIMIT_NOFILE | 256 |
| RLIMIT_FSIZE | 64 MiB |
| RLIMIT_MEMLOCK | 1 MiB |
| Private tmpfs | 64 MiB |
| Managed GC heap hard limit | 256 MiB |
| .NET configured processor count | 2 |
| Per-exchange startup/render deadline | Existing 30 seconds |

Phase 10b alone had no cgroup-enforced total native/JIT memory, CPU or task quota.
Required confinement now enforces the following additional phase-10c limits.
The managed heap setting is not the OS memory guarantee; input/frame budgets
and deadlines do not substitute for cgroup accounting.
Kernel vulnerabilities, native exploit resistance, syscall argument tightening,
production font/runtime supply-chain policy, V8 compatibility and broader
isolation still require future work.

## Hard resource accounting

| cgroup control | Required value / meaning |
| --- | --- |
| `memory.max` | 536870912 bytes (512 MiB) |
| `memory.swap.max` | 0 (no cgroup swap allowance) |
| `pids.max` | 64 tasks, including threads and trusted bootstrap/reaper |
| `cpu.max` | `100000 100000`: at most one CPU's bandwidth per 100 ms period |

The transient scope applies before bootstrap execution and covers its entire
descendant tree: native resident allocation, JIT/runtime memory, page cache,
private tmpfs and managed heaps contribute to kernel memory accounting. This
is **per worker**, not an aggregate browser budget; chrome, loaders and other
tabs have separate ownership. Kernel `memory.max` can have documented transient
overshoots and reclaim behavior; it is not a virtual-address-space reservation
limit or a guarantee that every byte of all kernel objects is charged.

The bootstrap requires unified membership in its generated
`visualweb-renderer-<unique>.scope`, reads the exact settings, then mounts only
that scope directory read-only for the worker to verify again. The scope
launcher needs the user's XDG/DBus manager transport; bubblewrap clears these
variables and mounts no session socket, so page code cannot reconfigure limits.
Missing manager/controller support, wrong/max values and out-of-scope direct
bootstrap invocation fail **before** page/native font initialization.

OOMPolicy is explicitly `continue`: the kernel kills the memory-heavy task
inside its bounded scope; browser EOF/crash handling then stops the entire
owned scope. There is no request to terminate the user's slice or other tabs.
Scope cleanup uses bounded five-second systemctl commands and treats a missing
collected unit as success only after checking `LoadState=not-found`. Other
cleanup failures are surfaced; they are never silently reported as success.
`--collect` removes failed/inactive units; no global systemd configuration changes
or administrator services are installed.

Tasks are limited by the kernel, not just .NET processor settings. CPUQuota is
a rate limit, not an accumulated lifetime CPU-seconds cap. Existing rendering
deadlines remain necessary; idle-worker lifetime and browser-wide scheduling
policy are not introduced here. The strengthened profile rejects older phase-10b
workers even though IPC metadata remains v2.

## Verification

The explicitly run [LinuxSandboxSmoke](../tools/LinuxSandboxSmoke/) uses the
same launcher/filter, creates and cleans up only an owned host canary, and
checks that it cannot read that canary or inherited secret environment,
write exposed library/app/font/runtime/device mounts, or see host processes.
It executes real syscalls and requires EPERM for Internet/Unix sockets,
fork/process-clone, ptrace, mount, unshare/setns, BPF and io_uring. Private
temporary-file read/write must still work.

```sh
dotnet run --project tools/LinuxSandboxSmoke -- \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
dotnet run --project tools/LinuxSandboxSmoke -- \
  --resources --font tests/Engine.Text.Tests/Data/NotoSans.ttf
dotnet run --no-build --project src/Apps/VisualWeb.Browser -- \
  --development-multiprocess --require-sandbox \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --smoke --backend dummy
```

Repeat shell smoke with `--backend x11`. The browser suite has actual confined
frame/fractional-density/crash-restart tests on Linux x64; those tests require
the prerequisites above. Other platforms assert fail-closed behavior instead.
The platform suite checks exact policy construction; IPC tests cover protocol
v2, which adds the handshake profile and rejects v1. Build browser and worker
together; there is no mixed-version compatibility fallback.

Official sources `bubblewrap`, `linux-seccomp`, `libseccomp`,
`linux-resource-limits` and .NET host/process documents are locally cached and
refreshed independently from tests. No test fetches standards or remote pages.
See the [process guide](renderer-processes.md) for transactional ownership.
The resource mode uses bounded, owned scopes and verifies **kernel evidence**:
`pids.events max` increments when thread creation is denied,
`cpu.stat nr_throttled` increases under concurrent work, and unmanaged touched
pages trigger `memory.events oom_kill`. A trusted supervisor in the same scope
survives to read the OOM counter; each probe scope is stopped/collected afterwards.
No host-wide pressure or unrestricted allocation test is performed.

### Resource-exhaustion lifecycle (phase 10d)

Browser integration now drives the shared native resource probes through a
private **test-only** peer's profile-confirmed IPC channel. This peer uses the
same scope limits and namespace/seccomp bootstrap as production. A trusted outer
supervisor retains the scope long enough to read the kernel OOM counter after
the nested native allocator is killed, then writes evidence to stderr and exits.
No fault-injection commands or resource-test hooks are added to the production
renderer.

Measured behaviors include:

- Native OOM produces an exchange failure, clears the failed worker/scope and
  allows a fresh scoped render. Another actual renderer keeps its PID, scope
  and exact pixels.
- Failed OOM navigation preserves committed history and the previous frame;
  the UI receives one failure for that tab and reload recovers without adding
  a history entry.
- Kernel task denial is followed by joining the probe threads; subsequent
  replies use the same confined process/channel.
- CPU-throttled exchanges can be canceled or expire at their deadline. A
  canceled queued request does not terminate the active worker. Cleanup
  collects the owned scope and a later request starts a new one.
- Closing a tab during CPU pressure terminates its scope without publishing a
  stale frame/failure or changing the other tab's renderer and history.

Supervisor references are detached before disposal so a failed cleanup cannot
leave a disposed connection available to later polling or renderer diagnostics.
Cleanup errors still surface; this is not a retry or weaker launch fallback.

Linux arm64 rendering is covered by its native CI target, but Linux arm64
confinement remains unsupported. Origin/CORS/CSP and site isolation, full web
standards and production-safe hostile browsing remain unfinished. Windows
confinement is documented separately in [windows-confinement.md](windows-confinement.md).

### Phase-10b validation outcome

Validated on Linux x64: **41 projects** build, and **11,656 tests** pass without
failures or skips (Browser 63, Platform 20, IPC 18; existing engine/cache suites
remain passing). Formatting and editor diagnostics are clean. All **50 cached
official references** are independently fresh.

The native denial probe passes, including host-file/environment/process
invisibility, read-only mounts, exact EPERM syscall denials and usable private
temporary storage. Required-confinement SDL dummy and X11 smoke pass with
actual composed page pixels/history/multiple windows; native-apphost bootstrap
also passes. Explicit unsandboxed multiprocess and single-process dummy smoke
remain passing. A concurrent renderer-heavy validation run exposed the old
10-second smoke timeout; the harness now uses monotonic time and allows the
30-second renderer deadline to report an actual failure before its own
35-second timeout. No frame/pixel or denial assertion was weakened.

These results verify the documented Linux profile on this host, not Windows,
arm64, Wayland shell input, production-safe browsing or a hard native-memory cap.

### Phase-10c validation outcome

Validated on Linux x64: all **41 projects** build and **11,659 tests** pass with
no failures or skips (Browser 64, Platform 22, IPC 18; engine/cache suites remain
passing). First-party formatting and editor diagnostics are clean; all **53
official cached references** are independently fresh.

Real scoped workers return exact pixels, survive reload/restart and clean up
their unique units without terminating another tab. Missing user-manager
transport fails before starting the target command. Wrong/unlimited kernel
values and direct unscoped bootstrap are rejected.

The resource probes pass with kernel evidence for task denial, CPU throttling
and resident native-memory OOM while the supervisor survives. All previous
file/network/process/namespace denial probes remain passing, including denial
of writes to the resource-control mount. Confined X11 and native-apphost dummy
shell smoke pass; existing unsandboxed/local smoke remains passing. No owned
renderer/probe processes or transient scopes remain after validation.

This closes the documented Linux per-worker hard-resource gap; it does not
certify other targets, production web security or a whole-browser resource budget.

### Phase-10d validation outcome

All **41 projects** build on Linux x64. The complete **70-test browser suite**
passes without failures or skips, including six new resource-lifecycle cases.
The shared native memory/task/CPU probes and existing confinement-denial probe
pass independently, and required-confinement X11 shell smoke presents exact
page pixels with tabs/history/multiple windows. Changed-file formatting passes.
The standards cache remains independently fresh (53 documents); no renderer or
probe scopes/processes remain after validation.

The editor additionally reports pre-existing code-quality suggestions in
ProcessPageRenderer outside this stage's changes; the build has no compiler
errors. These tests cover supervision under bounded Linux resource pressure,
not Windows/arm64, V8 or production web security.
