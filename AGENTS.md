# Agent guide

## Repository paths

- Anchor repository file paths at the repository root, not its parent directory.
  In this workspace the root is `/bigdata/KAI/projects/VisualWeb`; all repository
  paths are beneath it.
- Resolve project directories through the project map or filesystem before using
  them; do not infer a directory from a project name. For example,
  `VisualWeb.Browser` is at `src/Apps/VisualWeb.Browser/` beneath the repository
  root, never at `/bigdata/KAI/projects/VisualWeb.Browser`.

## Find context before changing code

1. Read the [docs index](docs/README.md) and [architecture](docs/architecture/overview.md).
2. Use the [project map](docs/project-map.md) to find the owning subsystem and
   read its README. Platform services, Core URL/Encoding/MIME and developer
   tooling and Engine.Net/Html/Dom/Css/Text/Layout/Paint/Content are implemented;
   Core.Primitives remains a placeholder; Engine.Scripting has a bounded
   ClearScript/V8 host and live DOM foundation. PageRendering optionally executes
   post-parse inline classics; browser page scripts are disabled by default.
   IPC and VisualWeb.Renderer are implemented.
   VisualWeb.Browser CLI requires supported confinement for per-tab process rendering
   by default; local/unsandboxed development requires explicit trusted-content opt-out;
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
  Session HSTS (official ID `rfc6797`) is explicit loader/store injection; every
  DevelopmentShell owns one shared bounded memory-only store across tabs/windows
  in all modes, with cookies still off. Upgrade initial/redirect destinations
  before transport/cookies/bad-port/fixed-origin checks; never rebind caller
  origins or bypass certificate validation. Learn only first STS field from
  authenticated HTTPS, never IP/HTTP. Malformed fields need visible diagnostics;
  expire before capacity errors, never evict active protection. Persistence,
  preload/public-suffix policy and SOP/CSP remain deferred.
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
  Recommend CLI `--multiprocess`; it and legacy `--development-multiprocess`
  require supported confinement before SDL/content/worker startup, no fallback.
  Unsandboxed development (including single-process) requires the explicit
  `--allow-unsandboxed-development` flag as a trusted-content acknowledgement.
  If that flag is combined with normal mode or with `--require-sandbox`, the CLI
  must reject the invocation with an error. Preserve low-level library/test defaults.
  Require an explicit mode and trusted font setup.
  Keep asynchronous loading separate from owning-thread native rendering, preserve
  transactional history and stale-load cancellation, and do not claim isolation.
  Multiprocess shells always enable controller origin isolation: compare final
  LoadedPage origins with IsSameOrigin, reuse same-origin, rotate cross-origin and
  every new opaque document into an operation-owned candidate promoted only after
  render/commit/history success; dispose failed/stale/closed candidates and keep
  resize on the committed renderer. Not site/frame isolation or SOP/CORS/CSP.
  Shared app-layer stylesheet discovery must not leak into offline Engine.Content policy.
  Linked stylesheets are fetched only by the browser's tab GetPageSource (shared HSTS,
  cookies off, bounded) and sent as data; renderers never fetch and fail on unprovided links.
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
  checks and exact callback/text/traversal budgets. See the scripting phase rules
  below for feature-specific constraints.
- Add xUnit v3 tests under `tests/<project>.Tests/` when a subsystem gains code.
  Follow `tests/Tools.SpecCache.Tests/` for package references, project references,
  injected HTTP/time dependencies, and test cancellation tokens.
- Use Conventional Commits when requested to commit.

## Scripting phase rules

### Phase 11e — Live DOM foundation

- Extend the hidden primitive contract for attributes, element/text/fragment
  factories, and Node mutation/navigation.
- All non-document wrappers share one lifetime budget. Preflight native tree and
  attribute work so mutations fail without partial writes.
- Never schedule dynamically appended or changed script elements.
- Preserve native global lexical state; batches snapshot all source budgets before
  running in order under one shared deadline. Do not use wrapped eval or imply
  rollback after script errors.

### Phase 11d — Inline page execution

- Require explicit `--enable-inline-scripts`; snapshot inline classic sources
  after parsing and recompute styles and paint after mutation.
- Keep script errors transactional and the committed DOM pinned for resize.
- Never rerun scripts to hide lost worker state; IPC v3 carries identities and
  policy only.

### Phase 11f — Microtasks

- Use optional native Promise-backed queueMicrotask with verified checkpoints
  after each classic source and one shared task deadline/DOM/queue budget.
- Preserve the 1,024 pending and 4,096 total limits plus failure
  latching/invalidation.
- Never replace native FIFO ordering with a separate queue or claim a persistent
  HTML event loop.

### Phase 11g — Events

- Add optional native Event/EventTarget and synchronous Node/document listener
  propagation on snapshotted ancestor paths.
- Preserve listener brands and capture/once/passive/removal semantics; enforce
  1,024 entries/path targets, 4,096 shared invocations, 32 nested dispatches,
  and caught-failure latching.
- Do not add automatic input/load events, on* handlers, AbortSignal, or
  persistent listeners on resize.

### Phase 11h — Document readiness

- Opt inline pages into one-shot loading/interactive/DOMContentLoaded/complete
  readiness with private dispatch and checkpoints between stages.
- Sources and lifecycle callbacks share one deadline and all task budgets.
- Keep readyState readonly, lifecycle trust internal, and failures transactional.
  Do not retain Window/load events or V8/listeners; empty opted-in batches still
  complete lifecycle, while static mode does not construct V8.

### Phase 11i — Selectors

- Reuse Engine.Css for bounded querySelector/querySelectorAll and Element
  matches/closest. Preserve static NodeList identities, atomic result reservation,
  SyntaxError versus unsupported TypeError, receiver/ownership guards, linked
  cancellation, and one shared matching budget per query.
- Use a per-call CssSelectorScope for `:scope`: the receiver for queries, the
  original receiver for closest, and a featureless virtual root for fragments.
- Filtered nth-child/nth-last-child requires strict complex selector lists,
  inclusive element siblings, and pseudo-class plus maximum argument specificity.
  Nested filtering retains the original scope and shared budgets/cancellation;
  of-type variants still reject of clauses.
- Do not add live collections, :has, a new parser, or dynamically scheduled
  scripts.

### Phase 11j — Classes

- Implement className/live classList over the primitive attribute bridge.
- Preserve ordered-set and live iteration semantics, atomic argument/token/storage
  preflight, private receiver brands, and shared callback/text budgets.
- Token syntax errors are native named errors, not DOMException objects.

### Phase 11k — Tree inspection

- Provide bounded tree inspection and element-only navigation with inclusive
  containment, detached/fragment roots, options getter ordering, both-operand
  ownership, exact ancestor/child scans, and lifetime identity limits.
- Do not imply shadow DOM or live children/childNodes collections.

### Phase 11l — Reflected attributes

- Add reflected id and toggleAttribute with live conversion ordering, forced
  no-op validation/value retention, ordered attributes, and shared native
  storage/callback limits.
- Do not add custom-element reactions.

### Phase 11m — CharacterData editing

- Support nodeValue and bounded CharacterData editing with UTF-16/unsigned offset
  semantics, conversion order, atomic storage/result budgets, and identity.
- Observers, ranges, and PI pseudoattribute reactions remain deferred.

### Phase 11n — Text splitting

- Add Text splitText/wholeText. Preflight output, identity, data, and tree
  capacity before splitting; preserve original listeners and UTF-16 boundaries.
- WholeText stops at non-Text siblings; do not imply range repair.

### Phase 11o — Normalization

- Implement Node.normalize with a shared iterative native plan. Preflight the
  entire subtree and all surviving Text runs before writes; preserve the first
  nonempty identity and detached data/listeners.
- Charge shared task text budgets even for singleton runs and never recycle
  removed wrapper identities. Observers, live-range repair, and dynamic
  scheduling remain unsupported.

### Phase 11p — Node equality

- Implement isEqualNode using shared iterative native equality. Preflight both
  complete non-null trees, including roots, and both payloads before identity or
  mismatch shortcuts.
- Ignore attribute order, but preserve child order and Text segmentation; keep
  both operand ownership checks and allocate no descendant wrappers.
- Native cross-document comparison is not a script ownership exception.

### Phase 11q — Node metadata

- Add readonly nodeName/ownerDocument and Element name/namespace metadata.
- Preserve ASCII-only HTML name casing, null prefixes, and same-document facade
  ownership for detached nodes. Preflight uppercase name allocation and shared
  text limits; never expose an adopted foreign document.

### Phase 11r — Attribute inspection

- Add hasAttributes/getAttributeNames over native ordered attributes.
- Return fresh mutable arrays through captured length-prefixed decoding, not live
  CLR collections or Attr objects. Preserve recovered names, shared encoded
  output/count limits, and prototype-tampering defenses.

### Phase 11s — Comment and instruction factories

- Add comment/instruction factories and a branded readonly target. Convert target
  then data before native validation and share all factory input/output/identity
  limits.
- Empty XML targets must report DOM InvalidCharacter, not BCL exceptions.
  Preserve raw editing, non-Text barriers, and inert insertion; instruction
  pseudoattributes/reactions remain deferred.

### Phase 11t — ChildNode removal

- Add branded ChildNode.remove with pre-write parent-subtree/ancestor/cancellation
  checks and unscopable lookup.
- Preserve detached identity, ownership, descendants, and listeners; allocate or
  recycle no handles.

### Phase 11u — Doctype

- Add document.doctype and privately branded readonly name/publicId/systemId.
- Bound the document child scan and per-field/shared output; reuse existing
  identities, reject adopted wrappers, and do not fetch identifiers or broaden
  parser mode selection.

### Phase 11v — Cloning

- Add native deep/shallow cloneNode for supported non-Document wrappers.
  Preflight the complete copied tree, attributes, and primitive payloads before
  allocating detached copies; enforce per-element/shared traversal/text,
  identity/output, and cancellation limits.
- Never copy parents or listeners. Script Document cloning remains unsupported by
  the single-document bridge; native Document clones preserve mode and remap
  cloned owners.

HTML scheduling, full DOM, and generated Web IDL bindings remain deferred. Run
V8Smoke explicitly; Windows/arm64 guarantees require native target evidence.

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
