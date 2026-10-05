# VisualWeb

A C# web browser monorepo targeting Linux (X11 and Wayland) and Windows,
on x64 and arm64. Repository tooling, platform services, core standards
utilities, resource loading, static HTML/DOM/CSS, text/layout and CPU painting are implemented;
a development-only single-process browser shell is now runnable. It is **not
sandboxed or safe for hostile content**.

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

The SpecCache command caches official standards locally and checks only missing
documents or documents last checked more than 30 days ago. It is deliberately
separate from builds and tests, which do not fetch standards.

Open [VisualWeb.code-workspace](VisualWeb.code-workspace) in VS Code for the
C# Dev Kit recommendation and build/test/cache tasks.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture/overview.md),
[project map](docs/project-map.md), and [approved plan](plans/browser-foundation-plan.md).
AI agents should read [AGENTS.md](AGENTS.md) first.
