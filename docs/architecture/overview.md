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

SDL3 now handles windowing/input and software pixel presentation through
Platform.Sdl. Engine.Text now uses HarfBuzzSharp with Linux/Windows native assets;
Engine.Paint uses SkiaSharp CPU rasterization with explicit exact-byte font resources.
V8 is embedded through ClearScript with matching Linux/Windows x64/arm64 native
assets. Phase 11a establishes private thread-owned isolates, copied primitive
results and native confinement probes. Phase 11c adds minimal live DOM facades;
phase 11d optionally executes inline classics after whole-document parsing.
Full DOM/Web IDL, HTML scheduling/event loops and WebAssembly integration remain deferred; see the
[scripting guide](../scripting.md) for scope and target evidence.

## Dependency direction

Core primitives are at the bottom. Standards parsers build on core utilities.
DOM and CSS feed layout; text shaping feeds layout; layout feeds painting.
Engine.Content coordinates loading/parsing/rendering without depending on the
browser shell. Platform abstractions expose only OS-facing capabilities.
Platform.Linux and Platform.Windows implement those abstractions.

The shell selects platform backends at the composition root. IPC contracts are
data-only, independent of DOM and native objects. The transport depends only on
contracts. See the [project map](../project-map.md) for each project.

References are introduced when actually needed. Platform.Linux and
Platform.Windows reference Platform.Sdl, which implements portable contracts
and references Platform.Abstractions. Other empty libraries still have no
speculative dependency graph. See the [platform guide](../platform.md).
Core.Url references the vendored managed URL parser; its mutable URL objects
never cross the immutable BrowserUrl facade. Core.Encoding embeds official
tables; Core.Mime uses the BCL. See the [core guide](../core.md) for scope.
Engine.Net references those three core libraries and portable BCL HTTP/file
services. Its session cookie store belongs to a loader instance, not global
renderer state. It has no platform-backend or browser-shell dependency.
See the [networking guide](../networking.md) before integrating it with pages.
Engine.Html consumes already-decoded strings and references Engine.Dom only.
Engine.Dom is independent of parsing, networking, platform backends and chrome.
The HTML parser uses a checked internal attribute path: HTML error recovery can
produce names that public DOM setters reject. No script/event callbacks run
during parsing or mutation. See the [HTML/DOM guide](../html-dom.md).
Engine.Css references Engine.Dom only. Callers supply decoded stylesheet sources
in document order; the style engine also handles inline attributes and an
optional minimal UA sheet. Computed styles are snapshots, not live DOM observers.
Unsupported CSS surfaces produce explicit diagnostics; no import/resource
loading or layout runs. See the [CSS guide](../css.md).
Engine.Text references Platform.Abstractions for explicit OS font enumeration,
not OS backends. Fonts are renderer-local native owners with managed run output.
Engine.Layout references DOM/CSS/Text and returns finite block/inline geometry,
rejecting unsupported formatting algorithms. See the
[text/layout guide](../text-layout.md). Neither project fetches resources or
initializes a display. Engine.Paint consumes layout/styles and emits data-only
commands then opaque BGRA frames. Engine.Content coordinates decoded HTML and
caller CSS through those stages, without fetching resources, creating windows
or collecting embedded styles. See the [painting guide](../painting.md).

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

Phase 9 implements an **explicit development-only single-process mode** to test
the shell. BrowserController uses independent tab loaders/renderers and URL-only
history; asynchronous GET completion returns to the main thread for native
rendering and SDL presentation. Phase 10a also supplies explicit unsandboxed
multiprocess development mode, one worker per tab, bounded private IPC,
asynchronous publication, deadlines and tab-local crash/restart handling.
GET/MIME/decoding remain browser-owned; shared VisualWeb.PageRendering owns
stylesheet discovery and font/rendering policy, independent of chrome/networking.
Engine.Content stays offline and script-free. Opt-in inline scripts execute only
in PageRendering; successful mutated DOM is retained for resize, while fresh
navigation/reload uses a new isolate. Document publication identities/policy
travel through IPC v3, not DOM/native objects. See the [shell guide](../browser-shell.md) and
[process guide](../renderer-processes.md). Phase 10b adds optional required
Linux x64 namespace/mount/seccomp confinement through the SDL-independent
Platform.Linux.Sandbox bootstrap; see the [confinement guide](../linux-confinement.md).
Phase 10c additionally requires verified kernel memory/swap/task/CPU limits
in a separate owned systemd user scope per renderer, without global configuration.
Windows adds an optional AppContainer/Job Object renderer profile with
per-process/total commit bounds, a CPU cap and denied temp/profile writes.
Windows job objects provide no thread-count or per-job swap limit; Linux arm64,
production isolation and broader Windows guarantees remain unfinished.
Unsupported required requests fail closed.
The phase-1 project split is not itself process isolation or a sandbox.
Linux namespaces/seccomp/cgroups and Windows AppContainer/Job Objects are
available for their exact documented profiles. These are not
interchangeable guarantees. Sandboxing must fail explicitly
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
