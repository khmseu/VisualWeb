# Agent guide

## Find context before changing code

1. Read the [docs index](docs/README.md) and [architecture](docs/architecture/overview.md).
2. Use the [project map](docs/project-map.md) to find the owning subsystem and
   read its README. Platform services, Core URL/Encoding/MIME and developer
   tooling and Engine.Net/Html/Dom/Css/Text/Layout/Paint/Content are implemented;
   Core.Primitives remains a placeholder; Engine.Scripting has a bounded
   ClearScript/V8 host and live DOM foundation. PageRendering optionally executes
   post-parse inline classics; browser page scripts are disabled by default.
   IPC and VisualWeb.Renderer are implemented.
   VisualWeb.Browser supports local or unsandboxed per-tab process rendering;
   shared static rendering policy lives in VisualWeb.PageRendering.
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
- Read the [HTML and DOM guide](docs/html-dom.md) before changing parsing.
  The tokenizer is first-party; tree building is an explicit static subset.
  Unsupported algorithms must throw, not silently produce an approximate tree.
  Preserve pinned entity data and whole fixture families; DOM stays renderer-local.
- Read the [CSS guide](docs/css.md) before styling. Static selectors and a finite
  property set are implemented; unsupported features must produce diagnostics,
  not approximate matches/values. Preserve whole fixture families and exact
  limit tests. Styles are snapshots; callers order sources and recompute after
  mutations. Engine.Content loading remains later-phase work; layout is a finite subset.
- Read the [text/layout guide](docs/text-layout.md) before formatting pages.
  Fonts use owned native blobs and creating-thread-affine disposal. Register
  actual faces explicitly; no fallback/synthetic styles are implied. Layout
  requires zero CSS diagnostics, zero non-root vertical margins and supported
  Latin/LTR content. Unsupported layouts must throw, not silently approximate.
  Preserve the licensed pinned font, native GC regression and exact geometry tests.
- Read the [painting guide](docs/painting.md) before rasterizing pages. Register
  exact shaping font bytes, retain thread-affine native ownership, and preserve
  ordered anonymous layout flow. Pixels are opaque BGRA with explicit scale;
  offline Content never collects embedded CSS or fetches resources automatically.
  Unsupported paint must fail; preserve independent pixel and native glyph tests.
- Read the [shell guide](docs/browser-shell.md) before navigation/chrome changes.
  Require explicit development-mode acknowledgement and trusted font setup.
  Keep asynchronous loading separate from owning-thread native rendering, preserve
  transactional history and stale-load cancellation, and do not claim isolation.
  Shared app-layer stylesheet discovery must not leak into offline Engine.Content policy.
- Read the [process guide](docs/renderer-processes.md) before IPC/worker changes.
  Preserve exact framing/budgets/identity/viewport checks, per-tab deadlines,
  cancellation and crash recovery. Stdout is protocol only; drain stderr.
  Process separation is not confinement: unsupported sandbox-required launch must refuse.
  Workers use their own native font owners; never publish stale render results.
- Read the [Linux confinement guide](docs/linux-confinement.md) and
  [Windows confinement guide](docs/windows-confinement.md) before sandbox changes.
  Linux x64 uses required namespaces, read-only mounts, cleared environment
  and a default-deny libseccomp filter before content. Never add fallback or
  broad host mounts to make startup pass; test precise syscall/mount guarantees.
  Windows renderers use the documented AppContainer/Job Object profile;
  unsupported Windows versions fail closed and native x64/arm64 validation remains
  required. Required Linux confinement also requires
  verified cgroup v2 memory/swap/task/CPU settings in an owned systemd user scope.
  Managed heap limits are not OS memory quotas; preserve native resource probes
  and targeted scope cleanup. Missing user-manager/controller support must fail closed.
  Run LinuxSandboxSmoke explicitly and WindowsSandboxSmoke on native Windows;
  ordinary standards tests remain offline.
  Phase-10d browser tests use the shared resource probes in a private confined
  peer to verify OOM/reload history, task/channel recovery and CPU-pressure
  cancellation/deadlines/tab-close behavior. Keep fault injection test-only.
- Platform-specific code belongs in Platform backends. Engine code must not
  reference those backends or browser chrome. Never share mutable DOM/script
  objects between tabs; process communication uses data-only IPC contracts.
- Read the [scripting guide](docs/scripting.md) before V8 work. Preserve private
  thread-owned isolates, exact primitive copying and invalidation on interruption;
  never expose CLR DOM objects/types or weaken OS confinement for native startup.
  The approved live DOM foundation uses one hidden primitive-only delegate
  captured and deleted by trusted bootstrap; do not expose it on facades or
  return CLR objects/exceptions. Preserve receiver brands, identity/ownership
  checks and exact callback/text/traversal budgets. Phase 11e extends the hidden
  primitive contract for attributes, element/text/fragment factories and Node
  mutation/navigation. All non-document wrappers share one lifetime budget;
  mutations preflight native tree/attribute work and fail without partial writes.
  Never schedule dynamically appended/changed script elements.
  Classic execution preserves native global lexical state; batches snapshot
  all source budgets before running in order under one shared deadline. Do not
  replace this with wrapped eval or imply rollback after script errors.
  Phase-11d page execution requires explicit `--enable-inline-scripts`, snapshots
  inline classic sources after parsing and recomputes styles/paint after mutation.
  Keep script errors transactional and committed DOM pinned for resize; never
  rerun scripts to hide lost worker state. IPC v3 carries identities/policy only.
  Phase 11f uses optional native Promise-backed queueMicrotask, verified checkpoints
  after each classic source, and one shared task deadline/DOM/queue budget. Preserve
  1,024 pending/4,096 total limits and failure latching/invalidation; never replace
  native FIFO ordering with a separate queue or claim a persistent HTML event loop.
  Phase 11g adds optional native Event/EventTarget and synchronous Node/document
  listener propagation on snapshotted ancestor paths. Preserve listener brands,
  capture/once/passive/removal semantics, 1,024 entries/path targets, 4,096 shared
  invocations, 32 nested dispatches and caught-failure latching. No automatic
  input/load events, on* handlers, AbortSignal or persistent listeners on resize.
  Phase 11h opts inline pages into one-shot loading/interactive/DOMContentLoaded/
  complete readiness with private dispatch and checkpoints between stages.
  Sources and lifecycle callbacks share one deadline and all task budgets.
  Keep readyState readonly, lifecycle trust internal, failures transactional,
  and no Window/load event or retained V8/listeners. Empty opted-in batches
  still complete lifecycle; static mode does not construct V8.
  Phase 11i reuses Engine.Css for bounded querySelector/querySelectorAll and
  Element matches/closest. Preserve static NodeList identities, atomic result
  reservation, SyntaxError vs unsupported TypeError, receiver/ownership guards,
  linked cancellation and one shared matching budget per query. No :scope,
  live collections, new parser or dynamically scheduled scripts.
  Phase 11j adds className/live classList over the primitive attribute bridge.
  Preserve ordered-set and live iteration semantics, atomic argument/token/
  storage preflight, private receiver brands and shared callback/text budgets.
  Token syntax errors are native named errors, not yet DOMException objects.
  Phase 11k adds bounded tree inspection and element-only navigation. Preserve
  inclusive containment, detached/fragment roots, options getter ordering,
  both-operand ownership, exact ancestor/child scans and lifetime identity limits.
  No shadow DOM or live children/childNodes collections are implied.
  Phase 11l adds reflected id and toggleAttribute. Preserve live conversion
  ordering, forced no-op validation/value retention, ordered attributes and
  shared native storage/callback limits. No custom-element reactions are implied.
  Phase 11m adds nodeValue and bounded CharacterData editing. Preserve UTF-16/
  unsigned offset semantics, conversion order, atomic storage/result budgets
  and identity. Observers/range/PI pseudoattribute reactions remain deferred.
  HTML scheduling/full DOM/generated Web IDL bindings are deferred. Run V8Smoke explicitly;
  Windows/arm64 guarantees require native target evidence.
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
dotnet test tests/Engine.Dom.Tests/Engine.Dom.Tests.csproj
dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj
dotnet test tests/Engine.Css.Tests/Engine.Css.Tests.csproj
dotnet test tests/Engine.Text.Tests/Engine.Text.Tests.csproj
dotnet test tests/Engine.Layout.Tests/Engine.Layout.Tests.csproj
dotnet test tests/Engine.Paint.Tests/Engine.Paint.Tests.csproj
dotnet test tests/Engine.Content.Tests/Engine.Content.Tests.csproj
dotnet test tests/Engine.Scripting.Tests/Engine.Scripting.Tests.csproj
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet test tests/Ipc.Tests/Ipc.Tests.csproj
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

The solution build is the all-project smoke check. See the [platform guide](docs/platform.md)
for thread-affinity rules, native smoke commands and target validation gaps.
Platform.Tests shares contract/fake tests across the OS implementations.
Ordinary tests never initialize a native display. Native Windows/arm64 and
Wayland keyboard/IME checks still need suitable target environments.
