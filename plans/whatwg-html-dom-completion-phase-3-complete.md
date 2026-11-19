## Phase 3 Complete: Cross-subsystem inertness and limits

Phase 3 verifies that template contents remain inert across parsing, rendering, script scheduling, and bounded DOM cloning. No production code changes were needed.

**Files created/changed:**

- `tests/Engine.Dom.Tests/CloningTests.cs`
- `tests/Engine.Html.Tests/TreeTests.cs`
- `tests/VisualWeb.Browser.Tests/InlineScriptTests.cs`

**Functions created/changed:**

- None; this phase adds regression coverage only.

**Tests created/changed:**

- `DeepClone_template_cancels_before_preflighting_pending_content` verifies cancellation at the template-content fragment boundary and confirms the source tree is unchanged.
- `TemplateContentElementHonorsExactParserDepthLimit` verifies the exact parser depth boundary for template descendants.
- `TemplateContentStylesAndScriptsRemainInertOnRetainedResize` verifies inert template styles/scripts in static and process rendering, ordinary document-tree exclusion, and retained resize behavior.

**Review Status:** APPROVED for the Phase 3 coverage; the added cancellation regression was directly reviewed.

**Validation:**

- Engine.Dom.Tests: 64 passed.
- Engine.Html.Tests: 7,295 passed.
- VisualWeb.Browser.Tests: 1,396 passed.
- `dotnet test VisualWeb.slnx --no-build`: passed.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`: passed.
- `git diff --check`: passed.
- An initial full test attempt hit a V8 deadline in the existing `TreeInspectionFindsLiveElementsDuringLifecycleAndPreservesFailedNavigation` process test. Its isolated rerun passed 2/2, followed by a passing full solution run.

**Git Commit Message:**

```text
test(html): verify template inertness and limits

Add parser-depth, retained-rendering and cancellation regressions for template contents.
Verify styles and scripts remain inert across static and process rendering.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```
