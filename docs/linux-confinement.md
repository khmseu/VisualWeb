# Phase 10b: Linux renderer confinement

This stage implements the approved **Linux-first** renderer confinement profile.
It is not a production-safe browser or completion of all cross-platform
phase-10 isolation. Windows and Linux arm64 sandbox-required launches fail
closed. Existing explicit local/unsandboxed development modes remain available.

## Run and prerequisites

Requires Linux x64, a framework-dependent .NET 10 installation, **bubblewrap
0.9.0 or newer** at `/usr/bin/bwrap`, libseccomp.so.2 with the listed syscall
names, and permission to create unprivileged user, mount, PID, network, IPC,
UTS and cgroup namespaces. Older/missing tools or restricted kernel policies
cause visible startup failure, never an unsandboxed retry. No root elevation,
setuid configuration or machine-wide kernel changes are performed by the app.

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

1. The browser launches its trusted renderer bootstrap with cleared environment,
   disabled .NET diagnostics and private stdio. It has not sent page content.
2. The bootstrap exports a libseccomp BPF filter to an anonymous descriptor,
   sets resource limits, and `execv`s the fixed bubblewrap executable without
   shell command construction. Native handles and filters are released on failure.
3. Bubblewrap requires every configured namespace and disabled nested user
   namespaces, drops capabilities, establishes a new session, and installs the
   filter before executing the fresh .NET worker runtime.
4. The worker checks `NoNewPrivs: 1`, `Seccomp: 2`, zero effective capabilities
   and expected mount/runtime settings **before** opening its native page font.
   Only then does it send the profile-confirmed handshake.
5. The browser requires the exact `linux-bwrap-seccomp-v1` profile. An absent/
   different profile, initialization failure or deadline invalidates the worker.
   No fallback is attempted.

The profile string is a configuration confirmation from the **trusted worker
binary**, not remote cryptographic attestation or proof from untrusted code.
The process PID owned by the browser becomes bubblewrap; its confined worker
has a separate namespace PID. `--die-with-parent` terminates the nested worker
if bubblewrap or the browser dies. Existing tab-close/cancel/deadline/crash
supervision still terminates only owned processes; the next render reruns the
entire confinement bootstrap.

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

**There is no cgroup-enforced total native/JIT/RSS memory, CPU or thread quota.**
The managed heap setting is not an OS memory-security guarantee; input/frame
budgets and deadlines do not substitute for whole-process resource accounting.
Kernel vulnerabilities, native exploit resistance, syscall argument tightening,
production font/runtime supply-chain policy, V8 compatibility and further
resource confinement require future work.

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

Origin/CORS/CSP and site isolation, full web standards, Windows confinement,
Linux arm64 and production-safe hostile browsing remain unfinished.

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
