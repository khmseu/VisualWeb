# Common HTML/DOM Support Phase 6 Completion Report

## Phase 6 Complete: Low-Cost Adjacent Features

Completed Phase 6 at the prioritized bounded scope by adding opt-in script
reflection for the commonly used `HTMLElement.hidden` property. The getter
reflects absent, ordinary hidden, and `until-found` states; the nullable setter
follows the HTML/Web IDL conversion branches and reuses existing attribute
storage and callback limits. The feature adds no renderer, IPC, or trust path.
`hidden=until-found` reveal remains fail-closed and deferred as HDF-014;
`outerHTML` and `insertAdjacentHTML()` remain deferred as HDF-015.

**Files created/changed:**

- `src/Engine/Engine.Scripting/DomBindings.cs`
- `tests/Engine.Scripting.Tests/AttributeReflectionTests.cs`
- `tests/VisualWeb.Browser.Tests/HiddenRenderingTests.cs`
- `docs/html-dom.md`
- `docs/scripting.md`
- `docs/standards.md`
- `plans/Missing.md`
- `plans/html-dom-deferred.md`
- `plans/whatwg-html-dom-completion-roadmap.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`

**Functions created/changed:**

- Private DOM bridge operations for hidden-state reads and writes
- The script-facing `HTMLElement.hidden` property descriptor
- Shared ASCII-insensitive matching for the `until-found` keyword

**Tests created/changed:**

- `HiddenGetterReflectsLivePresenceAndUntilFoundState`
- `HiddenSetterHandlesNullableBooleanNumberAndStringValues`
- `HiddenReceiverBrandPrecedesConversionAndGetterBranding`
- Attribute-capacity and storage-budget cases for hidden writes
- Local/process rendering after script-driven hidden-state mutation
- Local/process fail-closed regression for script-set `hidden=until-found`

**Review Status:** Self-review completed; no correctness issues found in the
hidden reflection bridge or focused regression coverage.

**Validation:**

- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors
- Focused `AttributeReflectionTests` — 18 passed
- Focused local/process hidden rendering tests — passed
- Full Browser test project — 1,445 passed before the additional regression;
  the serialized solution run passed all 1,447 Browser tests
- `dotnet test VisualWeb.slnx --no-build -m:1` — passed across all projects
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed
- Local and required-confinement `V8Smoke` — passed
- `git diff --check` — passed

The first parallelized solution test run had two V8 execution-deadline
failures. Each affected test passed when run alone, and the complete
serialized solution rerun passed. No source change was needed in response.
No commit was made: the staged diff was empty at the commit check.

**Deferred work:** HDF-014 records the reveal/`beforematch` lifecycle;
HDF-015 records parent-context markup insertion and replacement. Phase 7
remains the next roadmap phase.
