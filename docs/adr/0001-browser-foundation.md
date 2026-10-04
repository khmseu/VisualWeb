# ADR 0001: Browser monorepo foundation

Status: accepted for phase 1.

## Decision

- One .NET 10 solution with subsystem projects, central package versions,
  nullable types and warnings as errors.
- Linux X11 **and** Wayland plus Windows, targeting x64 and arm64.
- SDL3 for windowing/input, SkiaSharp for rasterization, HarfBuzzSharp for shaping,
  all behind OS-neutral boundaries.
- V8 for the eventual scripting runtime. ClearScript versus a custom native
  binding is deferred until cross-platform packaging/sandbox requirements can
  be validated.
- Separate browser and renderer composition roots, one renderer process per
  tab initially; future cross-site frame isolation. Runtime isolation is phase
  10, not a guarantee of this scaffold.
- xUnit v3 for unit tests; future WPT-based conformance suites.
- Official standards cited at interface boundaries, locally cached with a
  30-day refresh policy independent of tests.
- Root AGENTS.md, docs index and per-project ownership READMEs for discoverability.

## Consequences

Empty projects do not claim implemented behavior or require native packages.
Native support must later be verified on all target platforms/architectures.
Documentation refresh needs internet access, while tests remain deterministic
and offline after package restore. Local document bodies are untracked;
timestamps/validators are tracked in the manifest. Refresh failure is visible
and does not destroy usable older documents.
