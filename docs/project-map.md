# Project map

Platform projects and developer tools are implemented. Core, Engine, IPC and
App projects remain library placeholders; app projects become executables when
their real entry points are implemented. Do not add dummy browser entry points.

| Project | Responsibility | Planned dependencies / official sources |
| --- | --- | --- |
| [Platform.Abstractions](../src/Platform/Platform.Abstractions/) | OS-neutral windows, input, surfaces, fonts, process capabilities | Official platform APIs; not web interfaces |
| [Platform.Sdl](../src/Platform/Platform.Sdl/) | Shared SDL windows/input/pixels and managed process/font services | Platform.Abstractions, SDL3-CS + Native |
| [Platform.Linux](../src/Platform/Platform.Linux/) | X11/Wayland selection, XDG paths, fonts and processes; confinement deferred | Platform.Sdl |
| [Platform.Windows](../src/Platform/Platform.Windows/) | Windows selection, app paths, fonts and processes; confinement deferred | Platform.Sdl |
| [Core.Primitives](../src/Core/Core.Primitives/) | Small shared value types | No higher-level dependency |
| [Core.Url](../src/Core/Core.Url/) | WHATWG URL parsing and serialization | Core.Primitives; `url` |
| [Core.Encoding](../src/Core/Core.Encoding/) | Encoding labels, decoders and sniffing | Core.Primitives; `encoding` |
| [Core.Mime](../src/Core/Core.Mime/) | MIME parsing/sniffing | Core.Primitives; `mime-sniffing` |
| [Engine.Net](../src/Engine/Engine.Net/) | Resource loading/Fetch, later cookies and policies | Core utilities; `fetch`, `html` |
| [Engine.Html](../src/Engine/Engine.Html/) | Tokenization and tree construction | Engine.Dom, Core.Encoding; `html` |
| [Engine.Dom](../src/Engine/Engine.Dom/) | Document tree and mutations | Core utilities; `dom`, `html` |
| [Engine.Css](../src/Engine/Engine.Css/) | CSS parsing, selectors, cascade/computed values | Engine.Dom; CSS and selectors documents |
| [Engine.Text](../src/Engine/Engine.Text/) | Fonts, shaping and text metrics | Platform.Abstractions, HarfBuzz; CSS Fonts/Text |
| [Engine.Layout](../src/Engine/Engine.Layout/) | Layout tree, block/inline geometry | Engine.Css, Engine.Dom, Engine.Text; CSS layout |
| [Engine.Paint](../src/Engine/Engine.Paint/) | Display lists and Skia rasterization | Engine.Layout, Platform.Abstractions |
| [Engine.Scripting](../src/Engine/Engine.Scripting/) | V8 host boundary, future Web IDL bindings | `ecmascript`, `webidl`, HTML event loop |
| [Engine.Content](../src/Engine/Engine.Content/) | Per-page orchestration | Engine subsystems, no shell/backend dependency |
| [Ipc.Contracts](../src/Ipc/Ipc.Contracts/) | Versioned data-only messages | No DOM/native object dependencies |
| [Ipc.Transport](../src/Ipc/Ipc.Transport/) | In-process development and cross-process transports | Ipc.Contracts |
| [VisualWeb.Browser](../src/Apps/VisualWeb.Browser/) | Windows, tabs, chrome, privileged brokers | Platform composition, IPC |
| [VisualWeb.Renderer](../src/Apps/VisualWeb.Renderer/) | One-tab renderer process | Engine.Content, IPC, restricted platform capabilities |
| [SpecCache](../tools/SpecCache/) | Local official documentation cache | .NET BCL only; independent of engine/test data |
| [PlatformSmoke](../tools/PlatformSmoke/) | Explicit native platform integration checks | Platform.Linux/Windows; real SDL |

Active suites: [Tools.SpecCache.Tests](../tests/Tools.SpecCache.Tests/) and the
shared [Platform.Tests](../tests/Platform.Tests/) contract/OS-service suite.
Add project-specific suites as implementations arrive, using the
[test convention](../tests/README.md).
