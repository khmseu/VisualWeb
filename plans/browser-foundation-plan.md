## Plan: Monorepo Foundation for a C# Web Browser

Establish a .NET 10 monorepo with one project per browser subsystem behind standards-referencing interfaces, then build a minimal static HTML/CSS browser with tabs and multiple windows on Linux (X11 + Wayland) and Windows. The architecture is multi-process from day one (one renderer process per tab), with V8 as the planned JavaScript engine, and a locally cached, monthly-refreshed library of the official specifications that both humans and AI agents can discover.

**Phase 1 delivered**
- .NET 10.0.401 SDK pin, shared build/package configuration, a 22-project solution and VS Code tasks.
- Browser subsystems are empty libraries; app entry points and runtime isolation remain later-phase work. No premature scripting API or native dependency is added.
- Standards cache tool, offline xUnit v3 tests, and 17 locally cached official documents. Missing documents are fetched; documents last checked more than 30 days ago are conditionally revalidated.
- Root AGENTS.md, indexed architecture/standards/decision docs and per-project READMEs.
- The solution build is the all-project smoke check rather than a recursive build inside a unit test. Runtime execution applies only to implemented tooling.

**Decisions (approved)**
- Phase 9 delivered: SDL-drawn address/tab chrome, GET static HTML navigation,
  transactional URL-only history, tabs and multiple windows in an explicitly
  development-only single process. The user approved embedded stylesheet
  collection and an explicit trusted font file; linked stylesheets, interaction,
  scrolling, persistence and sandboxing remain deferred. Each tab owns its
  loader/native font resources; generation checks reject stale loads.
  Linux x64 validation: all 36 projects build and all 11,616 tests pass
  (42 Browser tests; Content grows to 5). Actual SDL dummy and X11 hidden-window
  smoke checks pass, including composed native surface pixels. First-party
  formatting/editor diagnostics are clean; all 43 cached references remain fresh.
  Wayland keyboard/IME and Windows/arm64 shell execution remain target checks. See the
  [shell guide](../docs/browser-shell.md) for commands, policy and validation.
- Phase 8 delivered: immutable background/solid-border/glyph display lists,
  SkiaSharp CPU rasterization to opaque BGRA frames with explicit scale and
  viewport clipping, portable surface presentation and an offline decoded
  HTML/CSS-to-frame pipeline. The user approved deferring networking integration,
  images, advanced borders/compositing and browser UI with explicit failures.
  Exact shaping font bytes are registered separately for paint; ordered layout
  flow preserves anonymous inline groups. Linux x64 validation: all 35 projects
  build and all 11,573 tests pass, including 24 Paint and 4 Content tests.
  First-party formatting/editor diagnostics are clean; independently, all
  43 references are fresh. See the [painting guide](../docs/painting.md).
- Phase 7 delivered: HarfBuzzSharp with Linux/Windows native assets, owned fonts
  and Latin/LTR shaping; finite horizontal block/inline layout with sizing,
  whitespace, space wrapping and baseline geometry. The user approved deferring
  advanced bidi/Unicode breaks, margin collapse, floats, inline-block and replaced
  elements with explicit failure. Native shaping uses a pinned licensed test font.
  Linux x64 validation: all 33 projects build and all 11,545 tests pass,
  including 17 Text and 39 Layout tests with actual native shaping. Formatting
  and editor diagnostics are clean; independently, all 41 references are fresh.
  See the [text/layout guide](../docs/text-layout.md) for restrictions and evidence.
- Phase 6 delivered: first-party static CSS syntax, HTML selectors and
  UA/user/author/inline cascade with typed computed styles. The user approved
  a finite block/inline property set, CSS-wide keywords and inheritance;
  custom properties, conditional rules, layers, flex/grid and animations remain
  deferred with explicit diagnostics. No resource loading or layout is added.
  Linux x64 validation: all 31 projects build and all 11,489 tests pass
  (including 203 CSS tests and all 67 pinned WPT An+B cases). First-party
  formatting/editor diagnostics are clean; independently, all 37 references
  are fresh.
  See the [CSS guide](../docs/css.md) for exact scope and offline evidence.
- Phase 5 delivered: first-party whole-string HTML tokenizer, renderer-local
  core DOM and an explicit static-document tree-building subset. The user chose
  our own parser instead of an existing managed HTML parser.
  Pinned html5lib token/tree fixtures and official named-reference data provide
  offline evidence. Unsupported advanced tree algorithms throw explicitly;
  scripting, fragment parsing and full HTML recovery remain future work.
  Linux x64 validation: all 30 projects build and all 11,286 tests pass;
  formatting and editor diagnostics are clean. Independently, all 34 cached
  standards documents are fresh.
  See the [HTML and DOM guide](../docs/html-dom.md) for limits and validation.
- Phase 4 delivered: GET-only HTTP(S)/local file/data loader, explicit redirects,
  bounded response buffering, cancellation/deadline, and opt-in session cookies.
  The user approved defaults of 20 redirects and 32 MiB bodies; general methods,
  request bodies, full Fetch policies and persistence remain deferred.
  Cookies use a deliberately restrictive host-only BCL-backed subset.
  All 28 projects build; all 4,067 tests pass, including 210 networking checks.
  See the [networking guide](../docs/networking.md) for scope and validation.
- Phase 3 delivered: immutable URL parsing/resolution/serialization, all
  Encoding Standard labels and whole-buffer decoders with BOM precedence,
  MIME parsing/serialization, and pinned offline conformance tests.
  The user approved Dubzer.WhatwgUrl behind our API, then MIT source vendoring
  and algorithm fixes after the package failed WPT cases. Unicode 17 and its
  matching official IDNA corpus are the approved stable baseline.
  Streaming/encoders/HTML charset prescan, URL mutation/security-origin identity
  and context-sensitive MIME sniffing remain future work. All 27 projects build
  and all 3,857 tests pass. See the [core guide](../docs/core.md).
- Phase 2 delivered: portable contracts, shared Platform.Sdl implementation,
  Linux/Windows composition roots, shared managed platform tests and explicit
  native smoke tooling. SDL3-CS and SDL3-CS.Native are pinned to matching 3.4.2.
  Linux x64 dummy/X11 and headless Wayland surfaces/lifecycle are validated;
  Wayland IME, native Windows, high-DPI desktops and arm64 remain target checks.
  OS confinement remains phase 10; sandbox-required launches are rejected.
- Runtime: .NET 10 LTS, C# latest; targets Windows 10+ and Linux, x64 and arm64.
- Windowing/input: SDL3 behind `Platform.Abstractions`, with both X11 and Wayland backends supported and selectable on Linux (auto-detect via `WAYLAND_DISPLAY`/`DISPLAY`, override via config/env).
- Graphics/text: SkiaSharp for rasterization, HarfBuzzSharp for shaping.
- JavaScript: V8 (planned; embedding approach — e.g. ClearScript or a custom native binding — decided when the scripting phase starts). Only the `Engine.Scripting` abstraction is created now.
- Tests: xUnit v3; WPT-derived data used where available.
- Standards: every public interface documents the spec section it implements (URL + section anchor) in XML doc comments.
- Standards cache: `specs/` holds local copies of the referenced specifications, with a manifest recording source URL and fetch date; a refresh tool re-downloads entries older than 30 days. This is independent of the test suites.
- Agent discoverability: root `AGENTS.md` plus `docs/` index pointing at architecture docs, the spec manifest, project map, build/test commands, and conventions; per-project `README.md` files.

**Repository Layout**
- `src/Platform/` — `Platform.Abstractions`, `Platform.Linux` (X11 + Wayland via SDL3), `Platform.Windows`.
- `src/Core/` — `Core.Primitives`, `Core.Url`, `Core.Encoding`, `Core.Mime`.
- `src/Engine/` — `Engine.Net`, `Engine.Html`, `Engine.Dom`, `Engine.Css`, `Engine.Text`, `Engine.Layout`, `Engine.Paint`, `Engine.Scripting`, `Engine.Content`.
- `src/Ipc/` — message contracts and transports (in-process, cross-process).
- `src/Apps/` — `VisualWeb.Browser` (UI process), `VisualWeb.Renderer` (tab process).
- `tools/` — `SpecCache` refresh tool.
- `tests/` — one test project per source project; later a WPT harness.
- `specs/` — cached standards documents + `manifest.json`.
- `docs/` — architecture, ADRs, project map, spec index.

**Phases (10)**
1. **Phase 1: Repository Scaffolding, Spec Cache, Agent Docs**
    - **Objective:** Buildable empty monorepo with shared build config, standards cache tooling, and agent-discoverable documentation.
    - **Files/Functions to Modify/Create:** `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `VisualWeb.slnx`, `AGENTS.md`, `docs/README.md`, `docs/architecture/overview.md`, `docs/adr/`, `specs/manifest.json`, `tools/SpecCache/` (manifest model, staleness check, downloader), `tests/Tools.SpecCache.Tests/`, update `VisualWeb.code-workspace`.
    - **Tests to Write:** `Manifest_RoundTrips`, `Entry_OlderThan30Days_IsStale`, `Entry_Fresh_IsNotStale`, `Refresh_DownloadsOnlyStaleEntries`, `Refresh_FailedDownload_KeepsExistingCopy`, `Solution_AllProjectsBuild` smoke test.
    - **Steps:**
        1. Write SpecCache tests using a fake HTTP handler and fake clock; run, see them fail.
        2. Implement manifest model, staleness rule, refresh logic; run tests to green.
        3. Add root build config, solution, AGENTS.md and docs index; seed manifest with WHATWG URL, Encoding, MIME Sniffing, Fetch, HTML, DOM and CSS specs (Syntax, Cascade, Selectors, Values, Box, Display, Inline, Text, Fonts).
        4. Build and run all tests.
2. **Phase 2: Platform Abstractions and Backends**
    - **Objective:** Interfaces for windows, events, surfaces, clock, fonts, processes; SDL3-backed Linux (X11 + Wayland) and Windows backends; test fakes.
    - **Files/Functions to Modify/Create:** `src/Platform/*`, `tests/Platform.*.Tests`.
    - **Tests to Write:** `BackendSelector_PrefersWaylandWhenAvailable`, `BackendSelector_FallsBackToX11`, `BackendSelector_HonorsOverride`, `FakeWindow_DispatchesEvents`, `ProcessLauncher_PassesArgumentsAndEnvironment`.
    - **Steps:** Tests first against fakes and selector logic; implement abstractions; implement SDL3 backends; run tests.
3. **Phase 3: URL, Encoding, MIME**
    - **Objective:** WHATWG URL parser, Encoding Standard decoders with sniffing, MIME type parsing.
    - **Files/Functions to Modify/Create:** `src/Core/Core.Url`, `Core.Encoding`, `Core.Mime`, tests.
    - **Tests to Write:** Data-driven tests from WPT `urltestdata.json`, encoding label/decoder tests, MIME parse tests.
    - **Steps:** Import test data; red; implement per spec sections; green.
4. **Phase 4: Networking**
    - **Objective:** Fetch-shaped resource loader for `http(s)`, `file`, `data` with redirects and basic cookies.
    - **Files/Functions to Modify/Create:** `src/Engine/Engine.Net`, tests.
    - **Tests to Write:** `DataUrl_Decodes`, `FileUrl_Loads`, `Http_FollowsRedirects_UpToLimit`, `Cookies_StoredAndSent`.
    - **Steps:** Red with fake handlers; implement over `HttpClient`; green.
5. **Phase 5: HTML Parsing and DOM**
    - **Objective:** Spec tokenizer, tree-construction subset, core DOM node types.
    - **Files/Functions to Modify/Create:** `src/Engine/Engine.Html`, `Engine.Dom`, tests.
    - **Tests to Write:** html5lib tokenizer and tree-construction data-driven tests; DOM tree mutation tests.
    - **Steps:** Red; implement; green.
6. **Phase 6: CSS Parsing, Selectors, Cascade**
    - **Objective:** CSS Syntax parser, selector matching, cascade, inheritance, computed values, UA stylesheet.
    - **Files/Functions to Modify/Create:** `src/Engine/Engine.Css`, tests.
    - **Tests to Write:** Tokenizer/parser tests, selector specificity/matching tests, cascade order tests.
    - **Steps:** Red; implement; green.
7. **Phase 7: Text and Layout**
    - **Objective:** Font loading and HarfBuzz shaping; block and inline formatting contexts with line breaking.
    - **Files/Functions to Modify/Create:** `src/Engine/Engine.Text`, `Engine.Layout`, tests.
    - **Tests to Write:** Shaping tests with a bundled test font; block layout box geometry; line breaking.
    - **Steps:** Red; implement; green.
8. **Phase 8: Paint**
    - **Objective:** Display list generation, Skia rasterization, presentation to a platform surface.
    - **Files/Functions to Modify/Create:** `src/Engine/Engine.Paint`, `Engine.Content`, tests.
    - **Tests to Write:** Display list contents for sample documents; pixel tests against reference images.
    - **Steps:** Red; implement; green.
9. **Phase 9: Browser Shell (single-process)**
    - **Objective:** Address bar, navigation history, tab strip, multiple windows.
    - **Files/Functions to Modify/Create:** `src/Apps/VisualWeb.Browser`, tests.
    - **Tests to Write:** Session history navigation, tab lifecycle, window lifecycle model tests.
    - **Steps:** Red; implement; green.
10. **Phase 10: Multi-process Isolation**
    - **Objective:** IPC, one renderer process per tab, OS sandbox hooks (Linux namespaces/seccomp, Windows AppContainer/Job Objects), crash containment.
    - **Files/Functions to Modify/Create:** `src/Ipc/*`, `src/Apps/VisualWeb.Renderer`, platform sandbox APIs, tests.
    - **Tests to Write:** Message round-trip serialization, transport tests, `RendererCrash_OnlyAffectsItsTab`.
    - **Steps:** Red; implement; green.

**Post-MVP Roadmap**
- Approved phase 10a delivers explicit unsandboxed process separation first:
  private bounded/versioned IPC, browser-owned GET, renderer-owned native page
  stages, validated pixels, asynchronous publication, deadlines and per-tab
  crash/restart behavior. Single-process development remains available and
  sandbox-required launches refuse. See [process scope](../docs/renderer-processes.md).
  This does **not** complete phase 10: Linux/Windows confinement and target
  certification remain a separate unfinished stage.
- V8 integration and WebIDL-generated DOM bindings; HTML event loop.
- Full Fetch (CORS, CSP, caching), service workers.
- Flexbox, grid, tables, floats, positioning, transforms, animations.
- Images, media, canvas, WebGL/WebGPU, WebAssembly (via V8).
- Storage (cookies, localStorage, IndexedDB), workers.
- Site isolation (process per site), accessibility tree, GPU compositing.
- WPT conformance runner in CI.

**Open Questions**
1. V8 embedding: ClearScript or a custom native binding? Decide at the scripting phase.
