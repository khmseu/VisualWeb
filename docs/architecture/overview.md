# Browser architecture

## Scope and portability

VisualWeb implements a browser engine in C#, rather than wrapping an existing
browser's rendering engine. The target is .NET 10 LTS on Windows 10+ and Linux,
x64 and arm64. Native library and OS-version support must be verified when each
backend is introduced; these are target platforms, not phase-1 certifications.

Linux windowing must support **both X11 and Wayland**, using SDL3 behind our
platform abstractions. Prefer Wayland when usable, fall back to X11 when usable,
and allow an explicit backend override. Availability detection must ultimately
check actual initialization, not only environment variables. Do not bake X11
window handles into portable surface/input contracts.

SDL3 handles windowing/input, SkiaSharp handles rasterization, and
HarfBuzzSharp handles text shaping. Native dependencies are not installed in
phase 1. V8 is the planned ECMAScript/WebAssembly runtime; its binding, native
distribution, sandbox compatibility and supported architectures are decided
and tested at the scripting phase.

## Dependency direction

Core primitives are at the bottom. Standards parsers build on core utilities.
DOM and CSS feed layout; text shaping feeds layout; layout feeds painting.
Engine.Content coordinates loading/parsing/rendering without depending on the
browser shell. Platform abstractions expose only OS-facing capabilities.
Platform.Linux and Platform.Windows implement those abstractions.

The shell selects platform backends at the composition root. IPC contracts are
data-only, independent of DOM and native objects. The transport depends only on
contracts. See the [project map](../project-map.md) for each project.

References should be introduced when actually needed; empty phase-1 libraries
intentionally have no speculative dependency graph. These boundaries are the
design constraints for subsequent references.

## Windows, tabs and processes

The **browser process** owns chrome, windows, tab identity, session history and
renderer supervision. Window identity and tab identity are distinct: moving a
tab between windows must not require sharing another tab's mutable state.

The initial production isolation unit is one **renderer process per tab**.
Renderers own a page's DOM, style/layout state and V8 isolates/event loops.
Serialized, versioned IPC carries navigation, input, lifecycle and display
messages, with bounded payloads and validation at every trust boundary.
Browser chrome and privileged OS resources must not be reachable directly by
page scripts. A renderer crash must close/restart only its tab, not the shell.

Eventually site isolation needs separate renderer processes for cross-site
frames and navigation process swaps, not merely one process per tab. Networking
and storage can move behind broker/service processes. The architecture must
not assume every frame in a tab shares a trust principal.

Phase 9 may use an **explicit development-only single-process mode** to test
the shell. Phase 10 introduces IPC, separate processes and OS confinement.
The phase-1 project split is not itself process isolation or a sandbox.
Linux namespaces/seccomp and Windows AppContainer/Job Objects are planned
mechanisms, not interchangeable guarantees. Sandboxing must fail explicitly
when requested guarantees cannot be supplied; process separation alone is
not sufficient for hostile web content.

## Standards growth

Start with static HTML/CSS rendering, documenting exact supported subsets.
Keep URL, encoding, MIME, Fetch, HTML tokenization/tree construction, DOM,
CSS cascade/layout, scripting, and painting separately testable.

Later milestones add Web IDL bindings, the HTML event loop, V8, full Fetch/CORS,
CSP, workers/service workers, storage, broader CSS layout, accessibility,
images/media, canvas, WebGL/WebGPU and WebAssembly. Official documents guide
implementation; WPT provides separate conformance evidence. Neither a cached
document nor an interface citation implies conformance.
