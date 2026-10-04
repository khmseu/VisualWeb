# VisualWeb

A C# web browser monorepo targeting Linux (X11 and Wayland) and Windows,
on x64 and arm64. Phase 1 establishes the repository and developer tooling;
there is no runnable browser or standards-conformant engine yet.

## Getting started

Install the .NET 10 SDK selected by [global.json](global.json), then run from
the repository root:

```sh
dotnet restore VisualWeb.slnx
dotnet build VisualWeb.slnx --no-restore
dotnet test VisualWeb.slnx --no-build
dotnet run --project tools/SpecCache -- specs/manifest.json
```

The last command caches official standards locally and checks only missing
documents or documents last checked more than 30 days ago. It is deliberately
separate from builds and tests, which do not fetch standards.

Open [VisualWeb.code-workspace](VisualWeb.code-workspace) in VS Code for the
C# Dev Kit recommendation and build/test/cache tasks.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture/overview.md),
[project map](docs/project-map.md), and [approved plan](plans/browser-foundation-plan.md).
AI agents should read [AGENTS.md](AGENTS.md) first.
