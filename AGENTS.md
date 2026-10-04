# Agent guide

## Find context before changing code

1. Read the [docs index](docs/README.md) and [architecture](docs/architecture/overview.md).
2. Use the [project map](docs/project-map.md) to find the owning subsystem and
   read its README. Platform services, Core URL/Encoding/MIME and developer
   tooling and Engine.Net are implemented; Core.Primitives, other Engine
   projects, IPC and App remain placeholders.
3. Read the [standards index](docs/standards.md). Use the local documents in
   `specs/cache/` first, not repeated network requests. The [manifest](specs/manifest.json)
   maps document IDs to official sources, filenames, and last-check timestamps.
4. Run `dotnet run --project tools/SpecCache -- specs/manifest.json` before
   standards-dependent implementation if documents are missing or older than
   30 days. If refresh fails, report it and identify any stale sources used.
   This workflow is independent from conformance tests; tests must stay offline.
5. Check the [approved roadmap](plans/browser-foundation-plan.md) and
   [decisions](docs/adr/0001-browser-foundation.md). Do not implement later phases
   as part of unrelated work.

## Conventions

- .NET 10, nullable enabled, warnings as errors, file-scoped C# namespaces.
- Centralize NuGet versions in `Directory.Packages.props`; do not add unused
  SDL3, Skia, HarfBuzz or V8 packages before their implementing phases.
- Public web-platform interfaces must cite canonical official spec URLs with
  section anchors in XML docs, plus the local spec ID. Internal and OS-specific
  interfaces should cite their relevant official API documentation, not pretend
  to implement a web standard.
- Describe implemented subsets explicitly; a project name is not a conformance
  claim. Add a manifest entry when introducing a newly referenced standard.
- Read the [core guide](docs/core.md) and [vendored parser notes](third_party/Dubzer.WhatwgUrl/README.md)
  before changing URL/encoding/MIME. Preserve licenses and fixture pins;
  Unicode 17 is the approved stable IDNA baseline. Do not suppress failures.
- Read the [networking guide](docs/networking.md) before using Engine.Net.
  It is privileged GET-only loading, not script-visible Fetch or a sandbox.
  Cookies require explicit opt-in; do not silently weaken unsupported policy.
- Platform-specific code belongs in Platform backends. Engine code must not
  reference those backends or browser chrome. Never share mutable DOM/script
  objects between tabs; future process communication uses IPC contracts.
- Add xUnit v3 tests under `tests/<project>.Tests/` when a subsystem gains code.
  Follow `tests/Tools.SpecCache.Tests/` for package references, project references,
  injected HTTP/time dependencies, and test cancellation tokens.
- Use Conventional Commits when requested to commit.

## Validation commands (repository root)

```sh
dotnet build VisualWeb.slnx
dotnet test tests/Tools.SpecCache.Tests/Tools.SpecCache.Tests.csproj
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet test tests/Core.Tests/Core.Tests.csproj
dotnet test tests/Engine.Net.Tests/Engine.Net.Tests.csproj
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

The solution build is the all-project smoke check. See the [platform guide](docs/platform.md)
for thread-affinity rules, native smoke commands and target validation gaps.
Platform.Tests shares contract/fake tests across the OS implementations.
Ordinary tests never initialize a native display. Native Windows/arm64 and
Wayland keyboard/IME checks still need suitable target environments.
