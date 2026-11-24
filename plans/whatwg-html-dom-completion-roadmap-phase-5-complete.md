# Common HTML/DOM Support Phase 5 Completion Report

## Phase 5 Complete: Common Scripting and Document Lifecycle

Closed Phase 5 at the prioritized bounded scripting/lifecycle scope. Existing
opt-in parser-position classic execution, finite readiness/events/microtasks
and import-free inline modules remain within the supported subset. This
increment corrects inline classic script attributes: when no `src` is present,
`async` and `defer` have no scheduling effect, and execution remains at the
parser position. Scripts remain disabled by default.

External classic `async`/`defer` scheduling is not implemented. Correct
deferred fetching must proceed without blocking parsing and integrate
document-order execution before `DOMContentLoaded`; async scripts additionally
need completion-order scheduling. The existing browser-owned broker is a
parser-pause, sequential request/response path. Serial fetch after parsing
would change fetch timing, so this behavior remains explicitly deferred as
HDF-010 rather than approximated. External modules/imports, module `async`,
legacy `for`/`event`, a persistent HTML event loop and broader Window/load
lifecycle behavior remain unsupported.

**Files created/changed:**

- `src/Apps/VisualWeb.PageRendering/InlinePageScripts.cs`
- `tests/VisualWeb.Browser.Tests/InlineScriptTests.cs`
- `src/Apps/VisualWeb.PageRendering/README.md`
- `docs/README.md`
- `docs/html-dom.md`
- `docs/scripting.md`
- `docs/standards.md`
- `plans/html-dom-deferred.md`
- `plans/whatwg-html-dom-completion-roadmap.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`

**Functions created/changed:**

- `InlinePageScripts.ValidateSchedulingAttributes`
- `InlinePageScripts.ReadClassicSource`
- `InlinePageScripts.Collect` now accepts inline classic `async`/`defer`
  while remaining an explicitly post-parse utility

**Tests created/changed:**

- `InlineClassicSchedulingAttributesDoNotDelayParserPositionExecution`
  verifies loading readiness, parser order, visibility of later markup and
  microtask checkpointing in local and process rendering
- `ExternalClassicSchedulingAttributesFailWithoutBrokerRequest`
  verifies external scheduling remains rejected before broker access
- `UnsupportedInlineSchedulingAttributesStillFail` preserves module-async and
  legacy `for`/`event` boundaries
- Classification/preflight cases cover inline and external attribute forms

**Review Status:** APPROVED.

**Validation:**

- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors
- `dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj --no-build` — 1,445 passed
- `dotnet test VisualWeb.slnx --no-build` — passed
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed
- `git diff --check` — passed
- Pre-commit formatting hook for `42059666` — passed

The implementation and support documentation are committed as `42059666`.
This phase-completion report and its roadmap/Kontext status updates remain
uncommitted. Phase 6 has not started.

**Deferred scheduling work:** HDF-010 in `plans/html-dom-deferred.md` records
the external classic async/defer and full module-loading requirements,
dependencies and revisit conditions.
