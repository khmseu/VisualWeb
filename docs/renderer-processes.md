# Per-tab renderer process separation

Phase 10a added **process separation, not OS confinement**. Phase 10b added
opt-in [Linux x64 confinement](linux-confinement.md); Windows provides a separate
[AppContainer/Job Object profile](windows-confinement.md). Both use
`--require-sandbox` or `requireSandbox: true`; unsupported platforms/configurations
fail closed. Without that option, both development modes retain normal user
permissions. Do not browse hostile content: origin policy, cross-site frame
isolation and production guarantees remain unfinished. Confinement never falls
back to unsandboxed launch.

## Launch

Build the solution first, then run from the repository root:

```sh
dotnet build VisualWeb.slnx
dotnet run --no-build --project src/Apps/VisualWeb.Browser -- \
  --development-multiprocess \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

The renderer DLL or native apphost path and font are trusted operator choices,
not page-controlled options. Keep the renderer's runtime configuration,
dependencies and native assets beside it. DLL launches use the host beside the
active .NET runtime, not whichever `dotnet` appears first on `PATH`; this avoids
accidentally launching .NET 10 workers with an unrelated .NET 8 installation.
Custom runtime layouts can supply the `ProcessPageRenderer` API's explicit
`dotnetPath`; native apphosts need no host lookup. Self-contained/single-file
distribution has not been certified.
Apphost runtime-root configuration follows the official
[.NET host environment reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_root-dotnet_rootx86-dotnet_root_x86-dotnet_root_x64),
cached as `dotnet-host-environment`.

`--development-single-process` remains available, and the two flags are
mutually exclusive. The SDL warning identifies the selected mode and always
says **NO SANDBOX** unless Linux confinement is explicitly required. Controls and page support are unchanged; see the
[shell guide](browser-shell.md).

## Ownership and asynchronous publication

- The browser owns SDL windows/input, chrome, tab identities, history,
  per-tab bounded GET loaders, MIME/encoding policy and process supervision.
- One worker is launched lazily for each tab when rendering is required. It
  owns native page fonts, HTML/DOM, embedded stylesheet collection, CSS,
  layout and Skia painting. No DOM/native handles cross the channel.
- [VisualWeb.PageRendering](../src/Apps/VisualWeb.PageRendering/) shares static
  page policy between local and worker hosts without networking, backend or
  chrome dependencies. Engine.Content remains an offline pipeline.
- The worker has no resource-loading code or window backend. That alone does
  not prevent OS access. Optional Linux confinement is a separate platform policy.
- Main-thread pumps observe asynchronous load/render tasks without blocking
  on worker replies. Only successful, current-generation frames commit URL,
  history, title and content. Stale/canceled results cannot publish.
- Static resizing rerenders retained decoded HTML without a fetch. Opt-in
  inline scripting retains the committed mutated DOM for repainting without
  repeating execution. Superseded resizes
  cannot publish; failed resizes clear wrong-sized frames. Moving tabs keeps
  their worker/state; closing tabs cancels their work and terminates their worker.

The worker's main thread performs native rendering and font disposal.
Browser-side chrome remains a separate main-thread native owner.

## Private stream protocol v3

Each tab has its own inherited stdin/stdout pipe pair. There is no public
socket, shared multiplexed channel or page-selected endpoint. Stdout carries
protocol only; stderr is continuously drained into a bounded 8192-character
diagnostic tail to avoid redirected-pipe deadlocks.
The client uses portable BCL process/pipe APIs because the existing platform
process contract does not expose redirected streams; OS-specific confinement
still belongs in platform backends.

Messages start with twelve bytes: ASCII `VWR1`, little-endian signed int32
metadata byte count, then little-endian signed int32 pixel byte count.
UTF-8 JSON metadata follows, then optional raw tightly packed opaque BGRA.

| Bound | Value |
| --- | ---: |
| Protocol version | 3, explicitly present; v1/v2 rejected |
| Metadata bytes | 32 MiB |
| JSON nesting | 16 |
| Decoded HTML UTF-16 characters | 4 Mi |
| URL/title/status/error/diagnostic characters | 8192 each |
| Response diagnostics | 64 |
| Physical frame pixels | 4,194,304 |
| Raw BGRA bytes | 16 MiB |
| Startup/render/exchange deadline | 30 seconds by default |

The startup `hello` has ID zero and a nullable sandbox profile. Required Linux
confinement confirms `linux-bwrap-seccomp-cgroup-v2`; required Windows
confinement confirms `windows-appcontainer-job-v1`. This metadata requires
protocol v2 originally; phase 11d advances to v3 for document/repaint/script policy
because older receivers strictly reject unknown fields.
Requests have increasing positive IDs;
`render` carries decoded HTML, URL, HTTP status/diagnostics and CSS viewport/
scale, a nonempty document GUID, the last committed document GUID, explicit
inline-script opt-in and retained-document repaint intent. Script policy is
fixed by the first request and cannot change within a tab channel. Publication
acknowledgement pins the worker's prior candidate on the next request; another
candidate cannot evict the committed DOM. These fields are rejected on replies
and handshakes. A reply is `frame` (title/status/dimensions/stride/pixels) or `error`
(explicit page failure). No navigation/fetch command can be initiated by a
worker. No V8/DOM objects cross IPC. Optional post-parse inline execution uses
the [finite scripting policy](scripting.md); input, event loops and display-list
IPC are deferred.

Receivers reject wrong magic/version, missing version/kind, duplicate or
unknown JSON fields, malformed/truncated messages and excessive lengths.
Metadata is validated before pixel allocation. Frames require finite bounded
geometry, exact `width * 4` stride, exact byte count and alpha 255 everywhere.
The browser additionally verifies the request ID and exact physical dimensions
`ceil(CSS viewport * scale)` before copying/publishing a frame.

A channel permits only one reader/writer; the client serializes complete
request/reply exchanges and assigns IDs in wire order. Cancellation or a
protocol failure invalidates the channel rather than attempting resynchronization.
These are per-message/stage budgets, **not** a whole-browser memory cap.

## Failures, restart and validation

Supported page-subset errors are explicit error replies and retain the worker
for the next request. Disconnects, malformed/mismatched replies, startup/render
deadlines and cancellation terminate only the client's owned worker. The next
navigation starts a replacement. Scripted resize after worker loss explicitly
requires reload, rather than reconstructing the DOM by rerunning scripts.
Queue timeouts do not kill an unrelated active
exchange. Idle exits are reported once to the owning tab, preserving its last
frame/history and any pending browser fetch; reload or that pending navigation
can recover. EOF waits for exit and final stderr within the existing deadline.
Other tabs' processes and frames are unaffected.
Phase 10d exercises this lifecycle under actual Linux cgroup pressure through a
private confined test peer: native OOM invalidates the exchange and a later render
starts a fresh scope; CPU-throttled requests remain cancelable/deadline-bounded;
task denial followed by joined threads leaves the channel usable. Canceling a
queued request does not stop the active exchange. Closing a pressured tab
collects only its scope and never publishes its stale result or failure.
Unexpected worker exceptions go to stderr and exit nonzero, not success-shaped
fallback frames.

[IPC tests](../tests/Ipc.Tests/) exercise exact framing, fragmentation, bounds,
strict JSON, truncation, opacity and cancellation. The
[browser tests](../tests/VisualWeb.Browser.Tests/) launch actual .NET workers,
verify exact blue pixels/fractional scale and PID reuse, kill one of two owned
workers, verify controller-level containment/history and reload restart, and
test cancellation and disposal. The private
[test peer](../tests/Renderer.TestPeer/) deterministically supplies wrong IDs,
wrong sizes, transparent frames, partial-message exits, oversized stderr and
startup/render hangs. DLL and native apphost launches both have exact-pixel tests.
The peer's Linux resource mode uses the production confinement bootstrap and
the same kernel-backed exhaustion helpers as LinuxSandboxSmoke. Its trusted
outer supervisor survives the nested worker's OOM long enough to report
`memory.events oom_kill` on stderr; it never parses page content or alters the
production renderer. Controller tests preserve the last committed frame/history
on failed OOM navigation, deliver one tab-local failure and recover on reload.
Async fake renderers separately prove stale navigation/resize and closed-tab
results cannot publish. No test needs remote networking or a display.

Use the same offline native shell smoke in multiprocess mode:

```sh
dotnet run --no-build --project src/Apps/VisualWeb.Browser -- \
  --development-multiprocess \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --smoke --backend dummy
```

Repeat with `--backend x11` or a suitable target backend. Windows/arm64,
Wayland shell keyboard/IME and real desktop high-DPI still require target
validation. Linux x64 and Windows confinement have separate documented profiles.
Official .NET process/pipes and exact-stream
references are in the independently refreshed [standards cache](standards.md);
tests never fetch them.

### Phase-10a validation outcome

Validated on Linux x64: all **39 projects** build and all **11,653 tests** pass
with no failures or skips. Browser has 61 cases and IPC 18; the existing
engine/platform/cache suites remain passing. First-party formatting and editor
diagnostics are clean. Independently, all **46 official cached references**
are fresh.

Actual multiprocess SDL dummy and X11 hidden-window smoke pass, including
reading composed page pixels from the native surface, tab movement, history
and multiple windows. Native renderer apphost launch also passes dummy smoke
and fractional-scale exact-pixel testing. Existing single-process dummy/X11
smoke still passes. Process separation is verified; OS confinement and the
target checks listed above are not delivered by this stage.
