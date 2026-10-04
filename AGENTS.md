# Agent guide

## Find context before changing code

1. Read the [docs index](docs/README.md) and [architecture](docs/architecture/overview.md).
2. Use the [project map](docs/project-map.md) to find the owning subsystem and
   read its README. Projects are currently placeholders except SpecCache.
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
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore
```

The solution build is the all-project smoke check. Windows/native UI validation
will need real Windows and Linux X11/Wayland environments once backends exist;
a cross-platform managed build alone does not prove native portability.
