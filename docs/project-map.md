# Project map

Platform projects, Core URL/Encoding/MIME, Engine.Net/Html/Dom/Css/Text/Layout/Paint/Content and developer tools
and VisualWeb.Browser/PageRendering/Renderer plus IPC are implemented.
Core.Primitives remains a library placeholder; Engine.Scripting supplies the
bounded V8 host and minimal live DOM bindings. PageRendering supports explicit
opt-in post-parse inline classics. The browser executable is
explicitly development-only, not isolated production browsing.

| Project | Responsibility | Planned dependencies / official sources |
| --- | --- | --- |
| [Platform.Abstractions](../src/Platform/Platform.Abstractions/) | OS-neutral windows, input, surfaces, fonts, process capabilities | Official platform APIs; not web interfaces |
| [Platform.Sdl](../src/Platform/Platform.Sdl/) | Shared SDL windows/input/pixels and managed process/font services | Platform.Abstractions, SDL3-CS + Native |
| [Platform.Linux](../src/Platform/Platform.Linux/) | X11/Wayland selection, XDG paths, fonts and generic processes | Platform.Sdl; renderer confinement is a separate module |
| [Platform.Linux.Sandbox](../src/Platform/Platform.Linux.Sandbox/) | Linux x64 required namespaces/mounts/seccomp and per-worker hard cgroup resource scopes | BCL + bubblewrap/libseccomp/systemd user manager; no SDL; [scope](linux-confinement.md) |
| [Platform.Windows](../src/Platform/Platform.Windows/) | Windows selection, app paths, fonts and generic processes | Platform.Sdl |
| [Platform.Windows.Sandbox](../src/Platform/Platform.Windows.Sandbox/) | AppContainer renderer token, bounded Job Object, and read-only worker storage | Windows APIs; [scope](windows-confinement.md) |
| [Core.Primitives](../src/Core/Core.Primitives/) | Small shared value types | No higher-level dependency |
| [Core.Url](../src/Core/Core.Url/) | Immutable WHATWG URL parsing, resolution and serialization | Vendored Dubzer.WhatwgUrl; `url`, `unicode-idna`, `unicode-bidi` |
| [Core.Encoding](../src/Core/Core.Encoding/) | Encoding labels, whole-buffer decoders and BOM sniffing | Embedded WHATWG indices; `encoding` |
| [Core.Mime](../src/Core/Core.Mime/) | MIME parsing/serialization; context sniffing deferred | .NET BCL; `mime-sniffing` |
| [Engine.Net](../src/Engine/Engine.Net/) | Bounded GET loading, redirects, opt-in host-only cookies; full Fetch policies deferred | Core.Url/Encoding/Mime, BCL HTTP/files/cookies; `fetch`, `http-cookies` |
| [Engine.Html](../src/Engine/Engine.Html/) | First-party tokenizer and bounded static document tree building; advanced recovery deferred | Engine.Dom, embedded official entities; `html` |
| [Engine.Dom](../src/Engine/Engine.Dom/) | Renderer-local HTML nodes, checked mutations/adoption and attributes | .NET BCL; `dom`, `html` |
| [Engine.Css](../src/Engine/Engine.Css/) | First-party CSS syntax, static selectors, finite cascade/computed properties | Engine.Dom only; CSS and selectors documents; [scope](css.md) |
| [Engine.Text](../src/Engine/Engine.Text/) | Owned fonts, explicit face registration, native Latin/LTR shaping/metrics | Platform.Abstractions, HarfBuzzSharp + Linux/Win32 native assets; [scope](text-layout.md) |
| [Engine.Layout](../src/Engine/Engine.Layout/) | Finite block/inline geometry, space wrapping and baseline lines | Engine.Css, Engine.Dom, Engine.Text; [scope](text-layout.md) |
| [Engine.Paint](../src/Engine/Engine.Paint/) | Immutable display lists, Skia CPU pixels and portable presentation | Engine.Layout, Platform.Abstractions, SkiaSharp; [scope](painting.md) |
| [Engine.Scripting](../src/Engine/Engine.Scripting/) | Private V8 host, classic batches and bounded live DOM attributes/Node mutations; full Web IDL deferred | Engine.Dom, ClearScript + Linux/Windows x64/arm64 native assets; [scope](scripting.md) |
| [Engine.Content](../src/Engine/Engine.Content/) | Offline decoded HTML/CSS-to-frame orchestration | Engine.Html/Paint, no loading/shell/backend dependency; [scope](painting.md) |
| [Ipc.Contracts](../src/Ipc/Ipc.Contracts/) | Versioned data-only messages | No DOM/native object dependencies |
| [Ipc.Transport](../src/Ipc/Ipc.Transport/) | Private bounded framed streams, exact reads and pixel validation | Ipc.Contracts; [scope](renderer-processes.md) |
| [VisualWeb.Browser](../src/Apps/VisualWeb.Browser/) | Development windows/tabs/chrome, broker GET/history, asynchronous worker supervision | Platform.Linux/Windows, Engine.Net, PageRendering, IPC; [scope](browser-shell.md) |
| [VisualWeb.PageRendering](../src/Apps/VisualWeb.PageRendering/) | Explicit fonts/CSS collection, optional inline classics and retained DOM repainting | Engine.Content/Scripting/Css/Text/Paint; no networking/backend/chrome |
| [VisualWeb.Renderer](../src/Apps/VisualWeb.Renderer/) | One-tab static renderer executable, optionally Linux-confined | PageRendering, IPC, Platform.Linux.Sandbox; [scope](renderer-processes.md) |
| [SpecCache](../tools/SpecCache/) | Local official documentation cache | .NET BCL only; independent of engine/test data |
| [PlatformSmoke](../tools/PlatformSmoke/) | Explicit native platform integration checks | Platform.Linux/Windows; real SDL |
| [LinuxSandboxSmoke](../tools/LinuxSandboxSmoke/) | Explicit measured file/network/process/namespace denial probe | Platform.Linux.Sandbox; no remote traffic/display |
| [WindowsSandboxSmoke](../tools/WindowsSandboxSmoke/) | Explicit AppContainer/Job Object denial probe | Platform.Windows.Sandbox; no remote traffic/display |
| [V8Smoke](../tools/V8Smoke/) | Explicit native V8 isolation/resource and required-confinement probe | Engine.Scripting, Linux/Windows sandbox composition; no page execution |

Active suites: [Tools.SpecCache.Tests](../tests/Tools.SpecCache.Tests/),
shared [Platform.Tests](../tests/Platform.Tests/) contract/OS-service tests, and
[Core.Tests](../tests/Core.Tests/) with pinned official data and regressions, plus
[Engine.Net.Tests](../tests/Engine.Net.Tests/) with fake handlers, WPT data URLs
and owned loopback HTTP integration, [Engine.Dom.Tests](../tests/Engine.Dom.Tests/)
for mutations, and [Engine.Html.Tests](../tests/Engine.Html.Tests/) for pinned
html5lib token/tree corpora and explicit subset/limit regressions.
The [Engine.Css.Tests](../tests/Engine.Css.Tests/) suite covers static styling,
pinned WPT An+B cases and resource-limit regressions.
The [Engine.Text.Tests](../tests/Engine.Text.Tests/) and
[Engine.Layout.Tests](../tests/Engine.Layout.Tests/) suites cover native fonts,
ownership/shaping, deterministic geometry and native integration without a display.
The [Engine.Paint.Tests](../tests/Engine.Paint.Tests/) and
[Engine.Content.Tests](../tests/Engine.Content.Tests/) suites add exact pixels,
glyph command preservation, native Skia rendering and offline page integration.
The [VisualWeb.Browser.Tests](../tests/VisualWeb.Browser.Tests/) suite covers
shell state, transactional navigation, MIME/encoding/styles and CPU chrome/page
composition with fake window events, real renderer processes, per-tab crash/restart,
deadlines and faulty peers. [Ipc.Tests](../tests/Ipc.Tests/) covers framing/bounds.
The private [Renderer.TestPeer](../tests/Renderer.TestPeer/) is a protocol-fault
fixture, not an application. Native shell smoke is separate.
The [Engine.Scripting.Tests](../tests/Engine.Scripting.Tests/) suite verifies
native primitive conversion, private context state, exact limits, interruption
and ownership. [V8Smoke](../tools/V8Smoke/) separately verifies heap failure and
native host compatibility under the existing renderer confinement policies.
Add project-specific suites as implementations arrive, using the
[test convention](../tests/README.md).
