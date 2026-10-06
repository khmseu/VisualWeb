# VisualWeb

A C# web browser monorepo targeting Linux (X11 and Wayland) and Windows,
on x64 and arm64. Repository tooling, platform services, core standards
utilities, resource loading, static HTML/DOM/CSS, text/layout and CPU painting are implemented;
a development-only browser shell supports local or per-tab worker rendering.
Optional Linux renderer confinement is available; it is **not safe for hostile content**.

## Getting started

Install the .NET 10 SDK selected by [global.json](global.json), then run from
the repository root:

```sh
dotnet restore VisualWeb.slnx
dotnet build VisualWeb.slnx --no-restore
dotnet test VisualWeb.slnx --no-build
dotnet run --project tools/SpecCache -- specs/manifest.json
```

Launch the offline welcome page with the pinned development font:

```sh
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

See the [browser shell guide](docs/browser-shell.md) for tabs, multiple windows,
navigation controls, explicit font setup and the finite supported page subset.
The [renderer process guide](docs/renderer-processes.md) describes launching
the explicitly unsandboxed multiprocess mode, IPC validation and crash recovery.
Phase 10a provides process separation; phase 10b adds opt-in
[Linux x64 renderer confinement](docs/linux-confinement.md). Linux ARM64 is
included in the native build, test and smoke CI matrix, but Linux ARM64
confinement, origin policy and production-safe browsing remain unfinished. Both
modes are unsandboxed unless `--require-sandbox` is explicitly selected.
Required Linux confinement includes per-worker hard cgroup memory/swap/task/CPU
limits and needs a systemd user manager with the corresponding controllers.

The [platform guide](docs/platform.md) describes window/input/OS services and
explicit [native smoke checks](tools/PlatformSmoke/README.md).
The [core guide](docs/core.md) documents the standards utilities, offline
conformance fixtures, vendored URL parser and current limitations.
The [networking guide](docs/networking.md) covers bounded GET loading,
redirects, opt-in session cookies and the privileged-caller boundary.
The [HTML and DOM guide](docs/html-dom.md) describes the tokenizer, supported
tree-construction subset, renderer-local mutations and explicit limitations.
The [CSS guide](docs/css.md) covers static selectors, cascade, computed styles,
the initial property set and integration/validation boundaries.
The [text/layout guide](docs/text-layout.md) covers native HarfBuzz shaping,
font ownership and the finite LTR block/inline geometry subset.
The [painting guide](docs/painting.md) covers immutable display lists, opaque
BGRA frames, surface presentation and offline decoded-HTML-to-frame rendering.
The [scripting guide](docs/scripting.md) covers the new bounded ClearScript/V8
host, classic batches, live title/ID/text/attribute and Node creation/mutation
bindings, finite native microtask checkpoints and bounded synchronous synthetic
Event/EventTarget dispatch and finite document readiness/DOMContentLoaded, plus native confinement
probes. Page scripts remain disabled by default. `--enable-inline-scripts`
also exposes bounded DOM queries with static NodeList snapshots and Element
matches/closest, reusing the current CSS selector subset, plus reflected
className, bounded live classList token mutation/iteration and tree
inspection/element navigation, reflected IDs and checked attribute toggles. It
also supports bounded CharacterData/nodeValue editing that preserves text-node
identity, plus bounded Text splitting, contiguous wholeText reads and atomic
descendant Text normalization preserving surviving/detached identities, plus
bounded structural Node equality over ordered trees and unordered attributes,
readonly Node/Element name, namespace and owner-document metadata, and bounded
ordered attribute-name snapshots/presence inspection. It
explicitly enables a bounded post-parse inline classic subset in either
development mode; mutations feed style/layout/paint, and resize retains the
committed scripted DOM without reexecution. External scripts, HTML scheduling,
full Web IDL, event loops and production web security remain deferred.

The SpecCache command caches official standards locally and checks only missing
documents or documents last checked more than 30 days ago. It is deliberately
separate from builds and tests, which do not fetch standards.

Open [VisualWeb.code-workspace](VisualWeb.code-workspace) in VS Code for the
C# Dev Kit recommendation and build/test/cache tasks.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture/overview.md),
[project map](docs/project-map.md), and [approved plan](plans/browser-foundation-plan.md).
AI agents should read [AGENTS.md](AGENTS.md) first.
